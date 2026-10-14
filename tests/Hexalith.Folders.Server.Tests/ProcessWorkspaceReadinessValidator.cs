using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Returns an unknown readiness outcome without dispatching a provider effect.</summary>
internal sealed class ProcessWorkspaceReadinessValidator(ProviderFailureCategory failure) : IWorkspacePreparationReadinessValidator
{
    /// <inheritdoc/>
    public Task<ProviderReadinessValidationResult> ValidateAsync(ProviderReadinessValidationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ProviderReadinessValidationResult(ProviderReadinessResultCode.Allowed,
            "unavailable", "unknown_outcome", "none", false, null, "none", request.CorrelationId ?? "corr",
            "binding-a", "binding-a", "profile-a", null,
            new("snapshot_per_task", DateTimeOffset.UtcNow, "tenant-a:1", false), failure, "unknown_outcome"));
}
