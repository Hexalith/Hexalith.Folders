using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.DomainService;
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
        VerifiedQueryCaller? caller = VerifiedQueryCaller.Get(httpContext);
        bool verifiedRead = caller?.Query.CallerAuthority?.Origin == "delegated-owner-query"
            && caller.Query.TenantId == request.SafeContext.AuthoritativeTenantId
            && caller.Query.UserId == request.SafeContext.PrincipalId
            && caller.Query.AggregateId == request.SafeContext.OperationScope
            && caller.Query.CallerAuthority.Permissions.Contains(request.SafeContext.ActionToken, StringComparer.Ordinal);
        bool allowed = request.OperationPolicyClass switch
        {
            "mutation" => (receipt is not null
                && string.Equals(receipt.Tenant, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal)
                && string.Equals(receipt.Subject, request.SafeContext.PrincipalId, StringComparison.Ordinal)
                && receipt.Permissions.Contains(request.SafeContext.ActionToken, StringComparer.Ordinal))
                || (httpContext is not null && (HistoricalMutationAuthorization.IsDirectPublicMutation(httpContext)
                    || Content.WorkspaceContentRetentionEndpoints.Matches(httpContext))
                    && MatchesValidatedHuman(httpContext, request)),
            "strict_read" or "bounded_diagnostic_read" => verifiedRead
                || (httpContext is not null && (MatchesValidatedHuman(httpContext, request)
                    || request.OperationPolicyClass == "strict_read" && MatchesStrictContextDelegation(httpContext, request))),
            _ => false,
        };
        return Task.FromResult(allowed
            ? EventStoreAuthorizationValidationResult.Allowed(null)
            : EventStoreAuthorizationValidationResult.Denied());
    }

    private static bool MatchesStrictContextDelegation(HttpContext context, EventStoreAuthorizationValidationRequest request)
    {
        if (context.User.Identity?.IsAuthenticated != true || request.SafeContext.ActionToken != "mutate_files"
            || string.IsNullOrWhiteSpace(request.SafeContext.AuthoritativeTenantId)) return false;
        string[] actors = context.User.FindAll("sub").Select(static claim => claim.Value).ToArray();
        string[] tenants = context.User.FindAll("eventstore:tenant").Select(static claim => claim.Value).ToArray();
        string[] delegators = context.User.FindAll("eventstore:delegator").Select(static claim => claim.Value).ToArray();
        return actors.Length > 0 && actors.All(static value => !string.IsNullOrWhiteSpace(value))
            && actors.Distinct(StringComparer.Ordinal).Count() == 1
            && string.Equals(actors[0], request.SafeContext.ActorSafeIdentifier, StringComparison.Ordinal)
            && tenants.Length > 0 && tenants.All(value => string.Equals(value, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal))
            && delegators.Length > 0 && delegators.All(static value => !string.IsNullOrWhiteSpace(value))
            && delegators.Distinct(StringComparer.Ordinal).Count() == 1
            && !string.Equals(delegators[0], actors[0], StringComparison.Ordinal)
            && string.Equals(delegators[0], request.SafeContext.PrincipalId, StringComparison.Ordinal)
            && context.User.HasClaim("eventstore:permission", request.SafeContext.ActionToken)
            && context.User.HasClaim("eventstore:delegator-permission", request.SafeContext.ActionToken);
    }

    private static bool MatchesValidatedHuman(HttpContext context, EventStoreAuthorizationValidationRequest request)
        => context.User.Identity?.IsAuthenticated == true
            && context.User.FindAll("eventstore:tenant").Any(claim =>
                string.Equals(claim.Value, request.SafeContext.AuthoritativeTenantId, StringComparison.Ordinal))
            && string.Equals(context.User.FindFirstValue("sub"), request.SafeContext.PrincipalId, StringComparison.Ordinal)
            && context.User.FindAll("eventstore:permission").Any(claim =>
                string.Equals(claim.Value, request.SafeContext.ActionToken, StringComparison.Ordinal));
}
