using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.Folders.Authorization;

using Microsoft.AspNetCore.Http;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>Admits the EventStore validation layer only for the verified process receipt.</summary>
public sealed class VerifiedReceiptEventStoreAuthorizationValidator(IHttpContextAccessor httpContextAccessor)
    : IEventStoreAuthorizationValidator
{
    public Task<EventStoreAuthorizationValidationResult> ValidateAsync(
        EventStoreAuthorizationValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        HumanAuthorityReceiptClaims? receipt =
            httpContextAccessor.HttpContext?.Items[HumanAuthorityReceiptVerifier.VerifiedClaimsItemKey]
                as HumanAuthorityReceiptClaims;
        bool allowed = receipt is not null
            && string.Equals(request.OperationPolicyClass, "mutation", StringComparison.Ordinal)
            && string.Equals(receipt.Tenant, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal)
            && string.Equals(receipt.Subject, request.SafeContext.PrincipalId, StringComparison.Ordinal)
            && receipt.Permissions.Contains(request.SafeContext.ActionToken, StringComparer.Ordinal);
        return Task.FromResult(allowed
            ? EventStoreAuthorizationValidationResult.Allowed(null)
            : EventStoreAuthorizationValidationResult.Denied());
    }
}
