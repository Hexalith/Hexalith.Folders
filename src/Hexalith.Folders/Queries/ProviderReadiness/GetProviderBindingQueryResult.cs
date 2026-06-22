namespace Hexalith.Folders.Queries.ProviderReadiness;

/// <summary>
/// Result of <see cref="GetProviderBindingQueryHandler"/>. Metadata-only: never carries the credential reference,
/// only a redaction marker is surfaced by the transport layer.
/// </summary>
/// <param name="Code">Outcome code.</param>
/// <param name="ProviderBindingRef">Provider-binding reference (present only when allowed).</param>
/// <param name="ProviderFamilyRef">Provider family reference derived from the bound provider kind (present only when allowed).</param>
/// <param name="CapabilityProfileRef">Capability profile reference (defaulted; not persisted on the binding).</param>
/// <param name="Freshness">Read freshness metadata.</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="ReasonCode">Safe reason code.</param>
public sealed record GetProviderBindingQueryResult(
    GetProviderBindingQueryResultCode Code,
    string? ProviderBindingRef,
    string? ProviderFamilyRef,
    string? CapabilityProfileRef,
    ProviderReadinessFreshness Freshness,
    string CorrelationId,
    string ReasonCode);
