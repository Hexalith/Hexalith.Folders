using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.FileContext;
using Hexalith.Folders.Queries.Folders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hexalith.Folders.Server.Content;

/// <summary>Shapes only policy-visible source-bound SDK content metadata and exact bounded body ranges.</summary>
internal sealed class AuthoritativeWorkspaceFileContextSource(IAuthoritativeContentStore store,
    WorkspaceContentStateReader source, IWorkspaceFilePolicySource policies, IHttpContextAccessor http, TimeProvider clock,
    IWorkspaceFileSensitivityClassifier sensitivities, ILogger<AuthoritativeWorkspaceFileContextSource> logger,
    IContentAuthority authority)
    : IWorkspaceFileContextSource
{
    public async Task<WorkspaceFileContextSourceResult> QueryAsync(WorkspaceFileContextSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        string family = request.Kind.ToString().ToLowerInvariant();
        string phase = "policy";
        WorkspaceFileContextSourceResult Failure(WorkspaceFileContextSourceStatus status)
            => WorkspaceFileContextSourceResult.Unavailable(clock.GetUtcNow()) with { Status = status };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            var token = deadline.Token;
            http.HttpContext!.Items[WorkspaceContentAuthority.TaskItem] = request.TaskId;
            var scope = new ContentScope(request.ManagedTenantId, "folders", request.FolderId, request.WorkspaceId);
            await authority.AuthorizeAsync(http.HttpContext!.User, scope, ContentOperation.Read, scope, token).ConfigureAwait(false);
            var policy = policies.Capture();
            if (policy.ProtectionMode != "non_confidential") return Failure(WorkspaceFileContextSourceStatus.NotFoundSafe);
            http.HttpContext!.Items[WorkspaceContentAuthority.ActionItem] = request.ActionToken;
            // Direct paths are checked before any namespace or body observation.
            if (request.Paths.Any(path => policy.Classify(path.Path) is "excluded" or "restricted"))
                return Failure(WorkspaceFileContextSourceStatus.NotFoundSafe);
            phase = "source";
            var current = await source.ReadAsync(scope, token).ConfigureAwait(false);
            if (!current.State.IsCreated || current.State.LifecycleState != FolderLifecycleState.Active
                || current.State.WorkspaceId != request.WorkspaceId) return Failure(WorkspaceFileContextSourceStatus.NotFoundSafe);
            phase = "namespace";
            var bindings = await store.ListAsync(http.HttpContext.User, scope, token).ConfigureAwait(false);
            var visible = new List<(ContentBinding Binding, PathMetadata Path)>();
            foreach (var binding in bindings)
            {
                phase = "binding";
                var metadata = binding.Change.ResourceMetadata ?? throw new ContentUnavailableException();
                var path = WorkspaceContentAuthority.MetadataPath(metadata);
                string classification = policy.Classify(path);
                if (classification is "excluded" or "restricted") continue;
                var sensitivity = await sensitivities.ClassifyAsync(path, token).ConfigureAwait(false);
                if (sensitivity.Decision == WorkspaceFileSensitivityDecision.Unavailable) throw new ContentUnavailableException();
                if (sensitivity.Decision == WorkspaceFileSensitivityDecision.Redacted) continue;
                if (!current.State.FileIdentities.TryGetValue(binding.Change.FileId, out var identity) || !identity.Active
                    || !current.State.ContentPublications.Values.Any(value => value.FileId == binding.Change.FileId
                        && value.Version == binding.Change.Version && value.ReceiptId == binding.Receipt.Id))
                    throw new ContentUnavailableException();
                classification = classification == "metadata_only" || metadata.GetValueOrDefault("contentReadable") != "true"
                    ? "metadata_only" : "content_allowed";
                visible.Add((binding, path with { PathPolicyClass = classification }));
            }
            // Alias checks use accepted spelling at every component. No target is selected for an ambiguous namespace.
            foreach (var path in request.Paths)
            foreach (var entry in visible)
            {
                string[] wanted = path.Path.NormalizedPath.Split('/'), actual = entry.Path.NormalizedPath.Split('/');
                for (int index = 0; index < Math.Min(wanted.Length, actual.Length); index++)
                {
                    if (!wanted[index].Equals(actual[index], StringComparison.OrdinalIgnoreCase)) break;
                    if (wanted[index] != actual[index]) return Failure(WorkspaceFileContextSourceStatus.PathValidationFailed);
                }
            }
            if (request.Paths.Count > 0)
            {
                if (request.Paths.Any(path => !visible.Any(entry => entry.Path.NormalizedPath == path.Path.NormalizedPath)))
                    return Failure(WorkspaceFileContextSourceStatus.NotFoundSafe);
                visible = visible.Where(entry => request.Paths.Any(path => path.Path.NormalizedPath == entry.Path.NormalizedPath)).ToList();
            }
            if (request.Kind == WorkspaceFileContextQueryKind.Range)
            {
                var entry = visible.SingleOrDefault();
                if (entry.Binding is null || entry.Path.PathPolicyClass != "content_allowed") return Failure(WorkspaceFileContextSourceStatus.NotFoundSafe);
                long start = request.StartOffset ?? -1, end = request.EndOffset ?? -1, length = entry.Binding.Receipt.ByteLength;
                if (start < 0 || end < start || end - start > 262144) return Failure(WorkspaceFileContextSourceStatus.InputLimitExceeded);
                // Sensitivity classification awaited current authority: namespace-dependent 416 must
                // undergo the same final checks as a content-bearing result.
                _ = await store.ListAsync(http.HttpContext.User, scope, token).ConfigureAwait(false);
                var rangeAuthority = await source.ReadAsync(scope, token).ConfigureAwait(false);
                if (rangeAuthority.Head != current.Head || !policies.IsCurrent(policy)) return Failure(WorkspaceFileContextSourceStatus.PolicyUnavailable);
                if (start > length || start == length && end > start) return Failure(WorkspaceFileContextSourceStatus.RangeUnsatisfiable);
                phase = "body";
                var body = await store.ReadAsync(http.HttpContext.User, new(scope, entry.Binding.Change.FileId,
                    entry.Binding.Change.IncarnationId, entry.Binding.Change.Version, 1048576), token).ConfigureAwait(false);
                if (!WorkspaceFileContentClassification.IsContentReadable(body.Bytes)) throw new ContentUnavailableException();
                long count = Math.Min(end, length) - start;
                if (!policies.IsCurrent(policy)) return Failure(WorkspaceFileContextSourceStatus.PolicyUnavailable);
                var finalRange = await source.ReadAsync(scope, token).ConfigureAwait(false);
                if (finalRange.Head != current.Head || !policies.IsCurrent(policy)) return Failure(WorkspaceFileContextSourceStatus.PolicyUnavailable);
                return new(WorkspaceFileContextSourceStatus.Available, [], entry.Path,
                    new(start, end, count, count < end - start), Convert.ToBase64String(body.Bytes.AsSpan((int)start, (int)count)), null,
                    new(family, 1, 1, count, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, false, "not_truncated"),
                    new("snapshot_per_task", clock.GetUtcNow(), Watermark(scope, current.Head), false, "authoritative_content"));
            }
            if (request.Cursor is not null) return Failure(WorkspaceFileContextSourceStatus.ValidationFailed);
            if (request.Kind == WorkspaceFileContextQueryKind.Glob)
                visible = visible.Where(entry => WorkspaceFilePolicySnapshot.Matches(request.GlobPattern ?? string.Empty, entry.Path.NormalizedPath)).ToList();
            if (request.Kind == WorkspaceFileContextQueryKind.Search)
            {
                var matches = new List<(ContentBinding Binding, PathMetadata Path)>();
                foreach (var entry in visible.Where(entry => entry.Path.PathPolicyClass == "content_allowed"))
                {
                    var body = await store.ReadAsync(http.HttpContext.User, new(scope, entry.Binding.Change.FileId,
                        entry.Binding.Change.IncarnationId, entry.Binding.Change.Version, 1048576), token).ConfigureAwait(false);
                    if (!WorkspaceFileContentClassification.IsContentReadable(body.Bytes)) throw new ContentUnavailableException();
                    if (new UTF8Encoding(false, true).GetString(body.Bytes).Contains(request.QueryText ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        matches.Add(entry);
                }
                visible = matches;
            }
            var items = visible.OrderBy(entry => entry.Path.NormalizedPath, StringComparer.Ordinal).Take(request.Limit)
                .Select(entry => new WorkspaceFileContextItem(entry.Path, "file", entry.Binding.Receipt.ByteLength,
                    "tenant_sensitive", "not_redacted") { FileId = entry.Binding.Change.FileId, Version = entry.Binding.Change.Version }).ToList();
            bool truncated = visible.Count > items.Count;
            string reason = truncated ? "result_count_limit" : "not_truncated";
            long bytes = JsonSerializer.SerializeToUtf8Bytes(items, JsonSerializerOptions.Web).LongLength;
            while (bytes > 1040000 && request.Kind != WorkspaceFileContextQueryKind.Metadata && items.Count > 0)
            {
                items.RemoveAt(items.Count - 1); truncated = true; reason = "response_budget_limit";
                bytes = JsonSerializer.SerializeToUtf8Bytes(items, JsonSerializerOptions.Web).LongLength;
            }
            if (bytes > 1040000) return Failure(WorkspaceFileContextSourceStatus.ResponseLimitExceeded);
            if (!policies.IsCurrent(policy)) return Failure(WorkspaceFileContextSourceStatus.PolicyUnavailable);
            // Reconfirm current authorization and suppression after every awaited source/body operation, before shaping disclosure.
            _ = await store.ListAsync(http.HttpContext.User, scope, token).ConfigureAwait(false);
            var final = await source.ReadAsync(scope, token).ConfigureAwait(false);
            if (final.Head != current.Head || !policies.IsCurrent(policy)) return Failure(WorkspaceFileContextSourceStatus.PolicyUnavailable);
            return new(WorkspaceFileContextSourceStatus.Available, items, null, null, null,
                new(null, request.Limit, truncated, truncated ? reason : null),
                new(family, request.Limit, items.Count, bytes, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    truncated, reason),
                new("snapshot_per_task", clock.GetUtcNow(), Watermark(scope, current.Head), false, "authoritative_content"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Failure(WorkspaceFileContextSourceStatus.Timeout); }
        catch (WorkspaceContentPolicyException exception)
        { return Failure(exception.Code == "resource_unavailable" ? WorkspaceFileContextSourceStatus.NotFoundSafe : WorkspaceFileContextSourceStatus.PolicyUnavailable); }
        catch (UnauthorizedAccessException) { return Failure(WorkspaceFileContextSourceStatus.NotFoundSafe); }
        catch (Exception exception)
        {
            logger.LogWarning("Authoritative workspace content dependency is unavailable. Phase={Phase}; FailureType={FailureType}.", phase, exception.GetType().Name);
            return Failure(WorkspaceFileContextSourceStatus.Unavailable);
        }
    }
    private static string Watermark(ContentScope scope, long head)
        => "source_" + Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { scope, head })));
}
