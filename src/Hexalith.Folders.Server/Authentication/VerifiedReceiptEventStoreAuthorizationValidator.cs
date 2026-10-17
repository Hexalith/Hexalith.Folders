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
            "mutation" => (receipt is not null
                && string.Equals(receipt.Tenant, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal)
                && string.Equals(receipt.Subject, request.SafeContext.PrincipalId, StringComparison.Ordinal)
                && receipt.Permissions.Contains(request.SafeContext.ActionToken, StringComparer.Ordinal))
                || (httpContext is not null && HistoricalMutationAuthorization.IsDirectPublicMutation(httpContext)
                    && MatchesValidatedHuman(httpContext, request)),
            "strict_read" or "bounded_diagnostic_read" => httpContext is not null
                && MatchesValidatedHuman(httpContext, request),
            _ => false,
        };
        return Task.FromResult(allowed
            ? EventStoreAuthorizationValidationResult.Allowed(null)
            : EventStoreAuthorizationValidationResult.Denied());
    }

    private static bool MatchesValidatedHuman(HttpContext context, EventStoreAuthorizationValidationRequest request)
        => context.User.Identity?.IsAuthenticated == true
            && context.User.FindAll("eventstore:tenant").Any(claim =>
                string.Equals(claim.Value, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal))
            && string.Equals(context.User.FindFirstValue("sub"), request.SafeContext.PrincipalId, StringComparison.Ordinal)
            && context.User.FindAll("eventstore:permission").Any(claim =>
                string.Equals(claim.Value, request.SafeContext.ActionToken, StringComparison.Ordinal));
}
