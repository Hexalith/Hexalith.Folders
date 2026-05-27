namespace Hexalith.Folders.Queries.ProviderReadiness;

public interface IProviderSupportEvidenceReadModel
{
    Task<ProviderSupportEvidenceReadModelResult> QueryAsync(
        ProviderSupportEvidenceReadModelRequest request,
        CancellationToken cancellationToken = default);
}
