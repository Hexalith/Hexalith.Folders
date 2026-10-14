using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Supplies the authorized binding already referenced by the actor wire command.</summary>
internal sealed class ProcessProviderBindingReader : IProviderReadinessBindingReader
{
    /// <inheritdoc/>
    public Task<OrganizationProviderBinding?> GetAsync(ProviderReadinessBindingReadRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult<OrganizationProviderBinding?>(new("tenant-a", "tenant-a", "binding-a", "github",
            "credential-a", OrganizationProviderBindingPolicy.Empty, OrganizationProviderBindingPolicy.Empty,
            "correlation-a", "task-a", "binding-key", "binding-fingerprint", "configured", DateTimeOffset.UtcNow));
}
