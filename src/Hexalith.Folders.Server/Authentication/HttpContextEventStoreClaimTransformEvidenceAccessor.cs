using System.Security.Claims;

using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Authentication;

public sealed class HttpContextEventStoreClaimTransformEvidenceAccessor(IHttpContextAccessor httpContextAccessor)
    : IEventStoreClaimTransformEvidenceAccessor
{
    private const string TenantClaimType = "eventstore:tenant";
    private const string PermissionClaimType = "eventstore:permission";

    public EventStoreClaimTransformEvidence GetEvidence(string actionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionToken);

        PreauthorizedRequestState? preauthorized = PreauthorizedRequestContext.Current;
        if (preauthorized is not null)
        {
            if (!string.Equals(preauthorized.HistoricalActionToken, actionToken, StringComparison.Ordinal))
            {
                return EventStoreClaimTransformEvidence.MalformedEvidence();
            }

            return EventStoreClaimTransformEvidence.Allowed(
                preauthorized.TenantId,
                preauthorized.PrincipalId,
                [preauthorized.CandidateActionToken, preauthorized.HistoricalActionToken]);
        }

        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
        {
            return EventStoreClaimTransformEvidence.Missing();
        }

        string? tenantId = user.FindFirstValue(TenantClaimType);
        string? principalId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(principalId))
        {
            return EventStoreClaimTransformEvidence.MalformedEvidence();
        }

        IEnumerable<string> permissions = user.FindAll(PermissionClaimType)
            .Select(static claim => claim.Value);

        EventStoreClaimTransformEvidence actor = EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, permissions);
        string[] delegators = user.FindAll("eventstore:delegator").Select(static claim => claim.Value).ToArray();
        string[] delegatedPermissions = user.FindAll("eventstore:delegator-permission").Select(static claim => claim.Value).ToArray();
        if (delegators.Length == 0 && delegatedPermissions.Length == 0)
        {
            return actor;
        }

        bool complete = delegators.Length > 0 && delegators.All(static value => !string.IsNullOrWhiteSpace(value))
            && delegators.Distinct(StringComparer.Ordinal).Count() == 1
            && !string.Equals(delegators[0], principalId, StringComparison.Ordinal)
            && delegatedPermissions.Length > 0 && delegatedPermissions.All(static value => !string.IsNullOrWhiteSpace(value));
        return actor with
        {
            Delegator = complete ? EventStoreClaimTransformEvidence.Allowed(tenantId, delegators[0], delegatedPermissions)
                : EventStoreClaimTransformEvidence.MalformedEvidence(),
        };
    }
}
