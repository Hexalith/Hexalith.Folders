using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessValidationResult(
    ProviderReadinessResultCode Code,
    string Status,
    string ReasonCode,
    string SafeRemediationCode,
    bool Retryable,
    TimeSpan? RetryAfter,
    string RemediationCategory,
    string CorrelationId,
    string? ProviderReference,
    string? ProviderBindingRef,
    string? CapabilityProfileRef,
    ProviderReadinessCapabilityEvidence? Evidence,
    ProviderReadinessFreshness Freshness,
    ProviderFailureCategory FailureCategory,
    string CategoryCode);
