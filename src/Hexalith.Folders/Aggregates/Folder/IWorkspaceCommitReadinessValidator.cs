using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Aggregates.Folder;

public interface IWorkspaceCommitReadinessValidator
{
    Task<ProviderReadinessValidationResult> ValidateAsync(
        ProviderReadinessValidationRequest request,
        CancellationToken cancellationToken = default);
}
