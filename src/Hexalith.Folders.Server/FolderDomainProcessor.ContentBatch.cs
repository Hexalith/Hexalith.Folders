using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.DomainService.Content;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.FileContext;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Folders.Server;

public sealed partial class FolderDomainProcessor
{
    private async Task<DomainResult> ProcessContentBatchAsync(CommandEnvelope envelope, JsonElement root, JsonElement items)
    {
        if (_actorFolderRepository is not ActorFoldedFolderRepository
            || contentBatch is null || authoritativeContent is null || httpContextAccessor?.HttpContext is null
            || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() is < 1 or > 100
            || !root.TryGetProperty("workspaceId", out JsonElement workspace) || workspace.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("requestSchemaVersion", out JsonElement schema) || schema.ValueKind != JsonValueKind.String || schema.GetString() != "v1"
            || !root.TryGetProperty("taskId", out var task) || task.ValueKind != JsonValueKind.String || task.GetString() != ReadRequiredTaskId(envelope)
            || root.EnumerateObject().Any(property => property.Name is not ("items" or "workspaceId" or "taskId" or "requestSchemaVersion")))
            return Rejection(envelope, FolderResultCode.ValidationFailed, null);
        var paths = new List<string>();
        var operations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var payloads = new List<byte[]>();
        long totalBytes = 0;
        try
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
                var node = JsonNode.Parse(item.GetRawText())!.AsObject();
                node["workspaceId"] = workspace.GetString(); node["requestSchemaVersion"] = "v1";
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(node);
                FileMutationPayload mutation = JsonSerializer.Deserialize<FileMutationPayload>(payload, PayloadJsonOptions)!;
                string path = mutation.PathMetadata?.NormalizedPath ?? string.Empty;
                if (mutation.OperationId is null || !operations.Add(mutation.OperationId) || paths.Any(existing =>
                    existing.Equals(path, StringComparison.OrdinalIgnoreCase)
                    || existing.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(existing + "/", StringComparison.OrdinalIgnoreCase)))
                    return Rejection(envelope, FolderResultCode.PathPolicyDenied, null);
                foreach (string existing in paths)
                {
                    string[] left = existing.Split('/'), right = path.Split('/');
                    for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
                    {
                        if (!left[index].Equals(right[index], StringComparison.OrdinalIgnoreCase)) break;
                        if (left[index] != right[index]) return Rejection(envelope, FolderResultCode.PathPolicyDenied, null);
                    }
                }
                paths.Add(path); payloads.Add(payload);
                if (mutation.FileOperationKind != "remove") totalBytes = checked(totalBytes + (mutation.ByteLength ?? -10485761));
                if (totalBytes is < 0 or > 10485760) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or OverflowException)
        { return Rejection(envelope, FolderResultCode.ValidationFailed, null); }
        contentBatch.Begin();
        try
        {
            var events = new List<IEventPayload>();
            for (int index = 0; index < payloads.Count; index++)
            {
                using JsonDocument item = JsonDocument.Parse(payloads[index]);
                string operationId = item.RootElement.GetProperty("operationId").GetString()!;
                var single = envelope with { MessageId = WorkspaceContentBinding.BatchItemKey(envelope.MessageId, operationId), Payload = payloads[index] };
                DomainResult result = await ProcessSingleFileMutationAsync(single).ConfigureAwait(false);
                if (!result.IsSuccess) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
                events.AddRange(result.Events);
            }
            ContentPublicationRequest publication = contentBatch.Complete();
            if (publication.Changes.Count != payloads.Count) return Rejection(envelope, FolderResultCode.ValidationFailed, null);
            await authoritativeContent.PrepareAsync(httpContextAccessor.HttpContext.User, publication, CancellationToken.None).ConfigureAwait(false);
            _ = await authoritativeContent.ListAsync(httpContextAccessor.HttpContext.User, publication.Scope, CancellationToken.None).ConfigureAwait(false);
            var policySource = httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IWorkspaceFilePolicySource>();
            var finalPolicy = policySource.Capture();
            if (publication.Changes.Any(change => change.ResourceMetadata?.GetValueOrDefault("policyDigest") != finalPolicy.Digest)
                || !policySource.IsCurrent(finalPolicy)) return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null);
            var finalState = _actorFolderRepository.Load(FolderStreamName.Create(publication.Scope.TenantId, publication.Scope.AggregateId));
            if (finalState.WorkspaceLockFence != publication.LockFence || finalState.WorkspaceLockHolderTaskId != publication.TaskId
                || finalState.IsTaskClosed(publication.Scope.WorkspaceId, publication.TaskId))
                return Rejection(envelope, FolderResultCode.LockNotOwned, null);
            if (finalState.WorkspaceLockExpiresAt is null || finalState.WorkspaceLockExpiresAt <= httpContextAccessor.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow())
                return Rejection(envelope, FolderResultCode.LockExpired, null);
            // EventStore appends the complete actor decision transactionally. No preparation is visible beforehand.
            return new DomainResult(events);
        }
        catch (ContentConflictException) { return Rejection(envelope, FolderResultCode.AppendConflict, null); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { return Rejection(envelope, FolderResultCode.PolicyEvidenceUnavailable, null); }
        finally { contentBatch.End(); }
    }
}
