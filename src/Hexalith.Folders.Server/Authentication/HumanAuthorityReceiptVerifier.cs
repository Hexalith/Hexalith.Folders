using System.Security.Claims;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Hexalith.Folders.Server.Authentication;

/// <summary>Bridges a verified EventStore human receipt into one /process invocation.</summary>
public sealed class HumanAuthorityReceiptVerifier(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    internal const string VerifiedClaimsItemKey = "Folders:VerifiedHumanAuthorityReceipt";
    private const string SigningKeySetting = "Authentication:HumanAuthorityReceipt:SigningKey";
    private const string WorkloadSetting = "Authentication:HumanAuthorityReceipt:ExpectedWorkload";

    public bool TryEnter(DomainServiceRequest request, out IDisposable? scope)
    {
        ArgumentNullException.ThrowIfNull(request);
        scope = null;
        HttpContext? context = httpContextAccessor.HttpContext;
        string expectedWorkload = configuration[WorkloadSetting] ?? "eventstore";
        if (context?.User.Identity?.IsAuthenticated != true
            || string.IsNullOrWhiteSpace(expectedWorkload)
            || expectedWorkload.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character))
            || !string.Equals(
                context.User.FindFirstValue(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType),
                expectedWorkload,
                StringComparison.Ordinal))
        {
            return false;
        }

        string? signingKey = configuration[SigningKeySetting];
        if (string.IsNullOrWhiteSpace(signingKey)
            || !HumanAuthorityReceiptCodec.TryVerify(
                request.HumanAuthorityReceipt,
                request.Command,
                signingKey,
                timeProvider.GetUtcNow(),
                out HumanAuthorityReceiptClaims? receipt))
        {
            return false;
        }

        ClaimsPrincipal originalPrincipal = context.User;
        List<Claim> claims =
        [
            new(TenantContextOptions.EventStoreTenantClaimType, receipt!.Tenant),
            new(TenantContextOptions.SubjectClaimType, receipt.Subject),
        ];
        claims.AddRange(receipt.Permissions.Select(static permission =>
            new Claim("eventstore:permission", permission)));
        context.Items[VerifiedClaimsItemKey] = receipt;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "EventStoreHumanAuthorityReceipt"));
        scope = new ReceiptScope(context, originalPrincipal);
        return true;
    }

    private sealed class ReceiptScope(HttpContext context, ClaimsPrincipal originalPrincipal) : IDisposable
    {
        public void Dispose()
        {
            context.User = originalPrincipal;
            _ = context.Items.Remove(VerifiedClaimsItemKey);
        }
    }
}
