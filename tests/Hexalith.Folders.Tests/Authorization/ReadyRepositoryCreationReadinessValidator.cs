using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Queries.ProviderReadiness;

namespace Hexalith.Folders.Tests.Authorization;

internal sealed class ReadyRepositoryCreationReadinessValidator(DateTimeOffset now) : IRepositoryCreationReadinessValidator
{
    public int Calls { get; private set; }

    public Task<ProviderReadinessValidationResult> ValidateAsync(
        ProviderReadinessValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(new ProviderReadinessValidationResult(
            ProviderReadinessResultCode.Allowed,
            "ready",
            "success",
            "none",
            Retryable: false,
            RetryAfter: null,
            RemediationCategory: "none",
            request.CorrelationId ?? "correlation-a",
            ProviderReference: request.ProviderBindingRef,
            request.ProviderBindingRef,
            CapabilityProfileRef: "profile-a",
            Evidence: null,
            new ProviderReadinessFreshness("snapshot_per_task", now, "tenant-a:7", Stale: false),
            ProviderFailureCategory.None,
            "none"));
    }
}
