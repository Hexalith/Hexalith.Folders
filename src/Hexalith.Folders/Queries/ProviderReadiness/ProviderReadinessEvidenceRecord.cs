namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessEvidenceRecord(
    string ManagedTenantId,
    string? OrganizationId,
    string ProviderBindingRef,
    string? ProviderFamily,
    string? ProviderKey,
    string? CapabilityProfileRef,
    string Status,
    string ReasonCode,
    bool Retryable,
    string RemediationCategory,
    DateTimeOffset ObservedAt,
    string? FreshnessWatermark,
    string CorrelationId,
    string DiagnosticJson);
