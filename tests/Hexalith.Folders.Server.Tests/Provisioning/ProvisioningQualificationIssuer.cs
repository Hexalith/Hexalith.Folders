using System.Text;

using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Issues test-only assertions consumed by the real EventStore lookup verifier.</summary>
internal sealed class ProvisioningQualificationIssuer : IWorkloadAssertionIssuer
{
    public bool CanBindResources => true;

    public ValueTask<string?> IssueAsync(WorkloadAssertionRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(Sign(request, ProvisioningQualificationActorHost.ApplicationId));
    }

    internal static string Sign(WorkloadAssertionRequest request, string workload)
    {
        var claims = request.Bindings?.ToDictionary(binding => binding.Key, binding => (object)binding.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, object>(StringComparer.Ordinal);
        claims[EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = workload;
        claims[EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = request.Operation;
        claims[EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] = "asserted-actor";
        DateTime now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = ProvisioningQualificationActorHost.Issuer,
            Audience = request.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ProvisioningQualificationActorHost.SigningKey)),
                SecurityAlgorithms.HmacSha256),
            Claims = claims,
        });
    }
}
