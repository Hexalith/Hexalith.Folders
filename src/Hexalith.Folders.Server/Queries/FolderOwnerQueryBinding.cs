using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Queries;

/// <summary>Binds only SDK-verified Projects delegated queries to their exact originating authority.</summary>
internal static class FolderOwnerQueryBinding
{
    /// <summary>Checks verified caller identity without consuming payload authority.</summary>
    internal static bool IsVerified(QueryEnvelope query, IHttpContextAccessor accessor)
    {
        VerifiedQueryCaller? caller = VerifiedQueryCaller.Get(accessor.HttpContext);
        OriginatingQueryContext? origin = caller?.OriginatingContext;
        return caller is not null && ReferenceEquals(caller.Query, query)
            && query.CallerAuthority?.Origin == "delegated-owner-query" && origin is not null
            && query.Domain == "folders" && query.AuthenticatedWorkloadId == "projects" && query.IsDelegated
            && query.OriginalActorId == query.UserId && origin.OriginalActorId == query.UserId
            && !query.IsGlobalAdmin && !string.IsNullOrWhiteSpace(query.TenantId)
            && !string.IsNullOrWhiteSpace(query.UserId) && query.UserId == query.UserId.Trim()
            && !string.IsNullOrWhiteSpace(query.AggregateId) && query.AggregateId == query.AggregateId.Trim()
            && (origin.IsDelegated ? !string.IsNullOrWhiteSpace(origin.DelegationId) : origin.DelegationId is null)
            && (origin.Scopes is null || origin.Scopes.Count == 0 || origin.Scopes.Contains("projects.read", StringComparer.Ordinal))
            && (origin.Audience is null || origin.Audience.Count == 0 || origin.Audience.Contains("hexalith-eventstore", StringComparer.Ordinal));
    }
}
