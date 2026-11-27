using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Supplies unusable binding authority to the production readiness service.</summary>
internal sealed class McpCliGatewayQueryBindingReader(string scenario) : IProviderReadinessBindingReader
{
    /// <inheritdoc/>
    public Task<OrganizationProviderBinding?> GetAsync(ProviderReadinessBindingReadRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (scenario == "profile-drift")
        {
            throw new ProviderBindingProfileDriftException();
        }

        return Task.FromResult<OrganizationProviderBinding?>(new(
            scenario == "tenant-mismatch" ? "other-tenant" : request.ManagedTenantId,
            request.ManagedTenantId,
            scenario == "reference-mismatch" ? "opaque_OtherProvider" : request.ProviderBindingRef,
            "github",
            McpCliGatewayQueryFixture.Credential,
            OrganizationProviderBindingPolicy.Empty,
            OrganizationProviderBindingPolicy.Empty,
            request.CorrelationId,
            McpCliGatewayQueryFixture.TaskId,
            "fixture-key",
            "fixture-fingerprint",
            "configured",
            DateTimeOffset.UtcNow));
    }
}
