using System.Globalization;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Queries;

/// <summary>Reads explicit native File identity using isolated persisted metadata and two strict owner authorization passes.</summary>
public sealed class FolderFileOwnerReadQueryHandler(LayeredFolderAuthorizationService authorization,
    IHttpContextAccessor accessor, IReadModelStore store, TimeProvider? clock = null) : IDomainQueryHandler
{
    /// <inheritdoc/>
    public string Domain => "folders";
    /// <inheritdoc/>
    public string QueryType => FolderFileOwnerReadQuery.QueryType;

    /// <inheritdoc/>
    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken = default)
    {
        return await ExecuteReadAsync(query, false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Shares identical authorization and persisted-read behavior for the explicit inverse mapping.</summary>
    internal async Task<QueryResult> ExecuteReadAsync(QueryEnvelope query, bool byReference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query); cancellationToken.ThrowIfCancellationRequested();
        string queryType = byReference ? FolderFileReferenceReadQuery.QueryType : QueryType;
        QueryResult UnavailableResult() => byReference
            ? QueryResult.FromPayload(JsonSerializer.SerializeToElement(new FolderFileReferenceReadResult(FolderOwnerReadOutcome.Unavailable,
                null, null, null, null), JsonSerializerOptions.Web), queryType)
            : Unavailable();
        if (!FolderOwnerQueryBinding.IsVerified(query, accessor) || query.QueryType != queryType)
            return QueryResult.Failure("safe-denial");
        LayeredFolderAuthorizationContext context = new(query.TenantId, query.UserId, "actor_present", "read_metadata",
            LayeredFolderOperationPolicy.StrictRead(), EventStoreClaimTransformEvidence.Allowed(query.TenantId, query.UserId,
                query.CallerAuthority!.Permissions), query.AggregateId, query.CorrelationId, null, null, null);
        try
        {
            var first = await authorization.AuthorizeCurrentMetadataReadAsync(context, cancellationToken).ConfigureAwait(false);
            if (!first.IsAllowed) return first.Decision.Retryable ? UnavailableResult() : QueryResult.Failure("safe-denial");
            // Local Tenant age or cached membership cannot establish existence-disclosure authority.
            // Require complete native authority before looking up either protected identity namespace.
            if (FolderOwnerReadQueryHandler.Evidence(first.AllowedContext, (clock ?? TimeProvider.System).GetUtcNow()) is null)
                return UnavailableResult();
            string? ownerId;
            try
            {
                if (query.Payload.Length > 8192) return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
                using JsonDocument payload = JsonDocument.Parse(query.Payload);
                if (payload.RootElement.ValueKind != JsonValueKind.Object
                    || payload.RootElement.EnumerateObject().Count() != 1
                    || !payload.RootElement.TryGetProperty(byReference ? "projectFileReferenceId" : "ownerFileId", out var id) || id.ValueKind != JsonValueKind.String
                    || !(byReference ? FolderFileIdentityState.IsProjectReference(ownerId = id.GetString()) : FolderFileIdentityState.IsCanonical(ownerId = id.GetString()))
                    || query.Paging is not null || query.EntityId is not null && query.EntityId != query.AggregateId)
                    return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
            }
            catch (JsonException) { return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope); }
            string key = FolderFileIdentityReadModel.Key(query.TenantId, query.AggregateId);
            ReadModelEntry<FolderFileIdentityReadModel> entry = await store.GetAsync<FolderFileIdentityReadModel>("statestore", key, cancellationToken).ConfigureAwait(false);
            var row = entry.Value;
            if (row is null) return UnavailableResult();
            if (row.TenantId != query.TenantId || row.FolderId != query.AggregateId || row.Files is null
                || row.Files.Count > FolderFileIdentityState.MaximumIdentities || row.LastSequence <= 0 || string.IsNullOrEmpty(entry.ETag)
                || row.Files.Any(item => item is null || !FolderFileIdentityState.IsCanonical(item.OwnerFileId)
                    || item.ProjectFileReferenceId is not null && !FolderFileIdentityState.IsProjectReference(item.ProjectFileReferenceId))
                || row.Files.Select(item => item.OwnerFileId).Distinct(StringComparer.Ordinal).Count() != row.Files.Count
                || row.Files.Where(item => item.ProjectFileReferenceId is not null).Select(item => item.ProjectFileReferenceId)
                    .Distinct(StringComparer.Ordinal).Count() != row.Files.Count(item => item.ProjectFileReferenceId is not null))
                return UnavailableResult();
            row = row with { Files = row.Files.ToArray() };
            var matches = row.Files.Where(item => byReference ? item.ProjectFileReferenceId == ownerId && item.Active : item.OwnerFileId == ownerId).ToArray();
            var final = await authorization.AuthorizeCurrentMetadataReadAsync(context, cancellationToken).ConfigureAwait(false);
            if (!final.IsAllowed) return final.Decision.Retryable ? UnavailableResult() : QueryResult.Failure("safe-denial");
            if (!FolderOwnerReadQueryHandler.SameRevisions(first.AllowedContext, final.AllowedContext)
                || final.AllowedContext?.ResourceVersion != row.LastSequence.ToString(CultureInfo.InvariantCulture)) return UnavailableResult();
            var confirmed = await store.GetAsync<FolderFileIdentityReadModel>("statestore", key, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(confirmed.ETag) || confirmed.ETag != entry.ETag
                || confirmed.Value is not { } finalRow || finalRow.TenantId != row.TenantId || finalRow.FolderId != row.FolderId
                || finalRow.LastSequence != row.LastSequence || finalRow.ProjectedAt != row.ProjectedAt
                || finalRow.Files is null || !finalRow.Files.SequenceEqual(row.Files)) return UnavailableResult();
            // The persisted confirmation itself awaits I/O. Recheck actor authority afterward so
            // a revoke/regrant during that last resource read cannot borrow the earlier Allowed result.
            var disclosure = await authorization.AuthorizeCurrentMetadataReadAsync(context, cancellationToken).ConfigureAwait(false);
            if (!disclosure.IsAllowed) return disclosure.Decision.Retryable ? UnavailableResult() : QueryResult.Failure("safe-denial");
            if (!FolderOwnerReadQueryHandler.SameRevisions(final.AllowedContext, disclosure.AllowedContext)
                || disclosure.AllowedContext?.ResourceVersion != row.LastSequence.ToString(CultureInfo.InvariantCulture)) return UnavailableResult();
            FolderOwnerReadEvidence? evidence = FolderOwnerReadQueryHandler.Evidence(disclosure.AllowedContext, (clock ?? TimeProvider.System).GetUtcNow());
            if (evidence is null) return UnavailableResult();
            if (matches.Length > 1) return UnavailableResult();
            if (matches.Length == 0 || !matches[0].Active
                || byReference && !FolderFileIdentityState.IsProjectReference(matches[0].ProjectFileReferenceId))
                return QueryResult.Failure("safe-denial");
            cancellationToken.ThrowIfCancellationRequested();
            return byReference
                ? QueryResult.FromPayload(JsonSerializer.SerializeToElement(new FolderFileReferenceReadResult(FolderOwnerReadOutcome.Allowed,
                    matches[0].OwnerFileId, row.FolderId, matches[0].ProjectFileReferenceId, evidence), JsonSerializerOptions.Web), queryType)
                : Result(new(FolderOwnerReadOutcome.Allowed, evidence));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return UnavailableResult(); }
    }
    private static QueryResult Unavailable() => Result(new(FolderOwnerReadOutcome.Unavailable, null));
    private static QueryResult Result(FolderFileOwnerReadResult result)
        => QueryResult.FromPayload(JsonSerializer.SerializeToElement(result, JsonSerializerOptions.Web), FolderFileOwnerReadQuery.QueryType);
}
