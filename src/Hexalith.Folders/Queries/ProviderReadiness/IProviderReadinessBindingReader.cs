using Hexalith.Folders.Aggregates.Organization;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public interface IProviderReadinessBindingReader
{
    Task<OrganizationProviderBinding?> GetAsync(
        ProviderReadinessBindingReadRequest request,
        CancellationToken cancellationToken = default);
}
