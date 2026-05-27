namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderSupportEvidenceQueryResult(
    ProviderSupportEvidenceQueryResultCode Code,
    IReadOnlyList<ProviderSupportEvidenceItem> Items,
    ProviderSupportEvidencePage Page,
    ProviderReadinessFreshness Freshness,
    string CorrelationId,
    string ReasonCode);
