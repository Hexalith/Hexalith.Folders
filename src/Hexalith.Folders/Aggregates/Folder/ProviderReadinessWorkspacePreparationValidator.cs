using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class ProviderReadinessWorkspacePreparationValidator(ProviderReadinessValidationService service)
    : IWorkspacePreparationReadinessValidator
{
    private readonly ProviderReadinessValidationService _service =
        service ?? throw new ArgumentNullException(nameof(service));

    public Task<ProviderReadinessValidationResult> ValidateAsync(
        ProviderReadinessValidationRequest request,
        CancellationToken cancellationToken = default)
        => _service.ValidateAsync(request, cancellationToken);
}
