using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Supplies current admitted readiness for the actor outcome wire regression.</summary>
internal sealed class ProcessBindingReadinessValidator : IRepositoryBindingReadinessValidator
{
    /// <inheritdoc/>
    public Task<ProviderReadinessValidationResult> ValidateAsync(ProviderReadinessValidationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ProviderReadinessValidationResult(ProviderReadinessResultCode.Allowed,
            "ready", "success", "none", false, null, "none", request.CorrelationId ?? "correlation-a",
            "binding-a", "binding-a", "profile-a", null,
            new("snapshot_per_task", DateTimeOffset.UtcNow, "tenant-a:1", false), ProviderFailureCategory.None, "success"));
}
