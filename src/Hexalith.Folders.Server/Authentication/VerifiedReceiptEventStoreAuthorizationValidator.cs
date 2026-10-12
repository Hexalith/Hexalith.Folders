using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>Checks the verified process receipt for mutations and the validated bearer for direct reads.</summary>
public sealed class VerifiedReceiptEventStoreAuthorizationValidator(IHttpContextAccessor httpContextAccessor)
    : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        HumanAuthorityReceiptClaims? receipt =
            httpContext?.Items[HumanAuthorityReceiptVerifier.VerifiedClaimsItemKey]
                as HumanAuthorityReceiptClaims;
        bool allowed = request.OperationPolicyClass switch
        {
            "mutation" => receipt is not null
                && string.Equals(receipt.Tenant, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal)
                && string.Equals(receipt.Subject, request.SafeContext.PrincipalId, StringComparison.Ordinal)
                && receipt.Permissions.Contains(request.SafeContext.ActionToken, StringComparer.Ordinal),
            "strict_read" or "bounded_diagnostic_read" => httpContext?.User.Identity?.IsAuthenticated == true
                && httpContext.User.FindAll("eventstore:tenant").Any(claim =>
                    string.Equals(claim.Value, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal))
                && string.Equals(httpContext.User.FindFirstValue("sub"), request.SafeContext.PrincipalId,
                    StringComparison.Ordinal)
                && httpContext.User.FindAll("eventstore:permission").Any(claim =>
                    string.Equals(claim.Value, request.SafeContext.ActionToken, StringComparison.Ordinal)),
            _ => false,
        };
        return Task.FromResult(allowed
            ? EventStoreAuthorizationValidationResult.Allowed(null)
            : EventStoreAuthorizationValidationResult.Denied());
    }
}
