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

        return EventStoreClaimTransformEvidence.Allowed(tenantId, principalId, permissions);
    }
}
