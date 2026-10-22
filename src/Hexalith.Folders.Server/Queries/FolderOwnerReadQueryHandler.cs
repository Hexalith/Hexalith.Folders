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
        try
        {
            using JsonDocument payload = JsonDocument.Parse(query.Payload);
            if (payload.RootElement.ValueKind != JsonValueKind.Object || payload.RootElement.EnumerateObject().Any()
                || query.EntityId != query.AggregateId || query.Paging is not null) return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope);
        }
        catch (JsonException) { return QueryResult.Failure(QueryAdapterFailureReason.InvalidEnvelope); }
        LayeredFolderAuthorizationResult result = await authorization.AuthorizeCurrentMetadataReadAsync(new(
            query.TenantId, query.UserId, "actor_present", "read_metadata", LayeredFolderOperationPolicy.StrictRead(),
            EventStoreClaimTransformEvidence.Allowed(query.TenantId, query.UserId, query.CallerAuthority.Permissions),
            query.AggregateId, query.CorrelationId, null, null, null), cancellationToken).ConfigureAwait(false);
        if (!result.IsAllowed)
        {
            if (result.Decision.FreshnessClass == "stale") return Success(FolderOwnerReadOutcome.Stale);
            if (!result.Decision.Retryable) return QueryResult.Failure("safe-denial");
            return Success(FolderOwnerReadOutcome.Unavailable);
        }
        LayeredFolderAuthorizationResult final = await authorization.AuthorizeCurrentMetadataReadAsync(new(
            query.TenantId, query.UserId, "actor_present", "read_metadata", LayeredFolderOperationPolicy.StrictRead(),
            EventStoreClaimTransformEvidence.Allowed(query.TenantId, query.UserId, query.CallerAuthority.Permissions),
            query.AggregateId, query.CorrelationId, null, null, null), cancellationToken).ConfigureAwait(false);
        if (!final.IsAllowed) return final.Decision.Retryable ? Success(FolderOwnerReadOutcome.Unavailable) : QueryResult.Failure("safe-denial");
        if (!SameRevisions(result.AllowedContext, final.AllowedContext)) return Success(FolderOwnerReadOutcome.Unavailable);
        FolderOwnerReadEvidence? evidence = FolderOwnerQueryBinding.IsVerified(query, accessor)
            ? Evidence(final.AllowedContext, (accessor.HttpContext?.RequestServices.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System).GetUtcNow())
            : null;
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new FolderOwnerReadResult(FolderOwnerReadOutcome.Allowed)
        { Evidence = evidence }, JsonSerializerOptions.Web), FolderOwnerReadQuery.QueryType);
    }

    /// <summary>Compares exact native revision observations, including a revoke/regrant that ends Allowed.</summary>
    internal static bool SameRevisions(LayeredFolderAuthorizationAllowedContext? first, LayeredFolderAuthorizationAllowedContext? final)
        => first is not null && final is not null && first.AuthoritativeTenantId == final.AuthoritativeTenantId
            && first.PrincipalId == final.PrincipalId && first.OperationScope == final.OperationScope
            && first.FreshnessWatermark == final.FreshnessWatermark && first.ResourceVersion == final.ResourceVersion
            && first.PermissionVersion == final.PermissionVersion && first.NativeTenantRevision == final.NativeTenantRevision;

    /// <summary>Never substitutes local Tenant age or a preauthorization receipt for native current authority.</summary>
    internal static FolderOwnerReadEvidence? Evidence(LayeredFolderAuthorizationAllowedContext? context, DateTimeOffset authorizedAt)
        => context is { ResourceVersion: not null, PermissionVersion: not null, NativeTenantRevision: not null, LastVerifiedAt: not null }
            ? new(JsonSerializer.Serialize(new[] { context.PermissionVersion, context.NativeTenantRevision }),
                context.ResourceVersion, context.ResourceVersion, context.LastVerifiedAt.Value, authorizedAt)
            : null;

    private static QueryResult Success(FolderOwnerReadOutcome outcome)
        => QueryResult.FromPayload(JsonSerializer.SerializeToElement(new FolderOwnerReadResult(outcome), JsonSerializerOptions.Web), FolderOwnerReadQuery.QueryType);
}
