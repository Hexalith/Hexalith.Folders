using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Contracts;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Queries;

/// <summary>Reauthorizes the originating actor against current owner evidence through the existing exact-action stack.</summary>
public sealed class FolderOwnerReadQueryHandler(LayeredFolderAuthorizationService authorization, IHttpContextAccessor accessor) : IDomainQueryHandler
{
    public string Domain => "folders";
    public string QueryType => FolderOwnerReadQuery.QueryType;

    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        VerifiedQueryCaller? caller = VerifiedQueryCaller.Get(accessor.HttpContext);
        if (caller is null || !ReferenceEquals(caller.Query, query) || query.CallerAuthority?.Origin != "delegated-owner-query")
            return QueryResult.Failure("safe-denial");
        LayeredFolderAuthorizationResult result = await authorization.AuthorizeAsync(new(
            query.TenantId, query.UserId, "actor_present", "read_metadata", LayeredFolderOperationPolicy.StrictRead(),
            EventStoreClaimTransformEvidence.Allowed(query.TenantId, query.UserId, query.CallerAuthority.Permissions),
            query.AggregateId, query.CorrelationId, null, null, null), cancellationToken).ConfigureAwait(false);
        if (!result.IsAllowed)
        {
            if (result.Decision.FreshnessClass == "stale") return Success(FolderOwnerReadOutcome.Stale);
            if (!result.Decision.Retryable) return QueryResult.Failure("safe-denial");
            return Success(FolderOwnerReadOutcome.Unavailable);
        }
        try
        {
            using JsonDocument payload = JsonDocument.Parse(query.Payload);
            if (payload.RootElement.ValueKind != JsonValueKind.Object || payload.RootElement.EnumerateObject().Any()
                || query.EntityId != query.AggregateId || query.Paging is not null) return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }
        catch (JsonException) { return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope); }
        return Success(FolderOwnerReadOutcome.Allowed);
    }

    private static QueryResult Success(FolderOwnerReadOutcome outcome)
        => QueryResult.FromPayload(JsonSerializer.SerializeToElement(new FolderOwnerReadResult(outcome), JsonSerializerOptions.Web), FolderOwnerReadQuery.QueryType);
}
