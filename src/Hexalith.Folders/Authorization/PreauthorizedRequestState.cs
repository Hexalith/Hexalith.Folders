namespace Hexalith.Folders.Authorization;

/// <summary>Identifies one request whose authorization was already established by an outer transport boundary.</summary>
public sealed record PreauthorizedRequestState(
    string TenantId,
    string PrincipalId,
    string? FolderId,
    string? FreshnessWatermark,
    string? OrganizationId,
    string CandidateActionToken,
    string HistoricalActionToken,
    string? DelegatorPrincipalId);
