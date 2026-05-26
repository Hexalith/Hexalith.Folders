namespace Hexalith.Folders.Queries.ProviderReadiness;

public interface IProviderReadinessEvidenceStore
{
    Task StoreAsync(ProviderReadinessEvidenceRecord evidence, CancellationToken cancellationToken = default);
}
