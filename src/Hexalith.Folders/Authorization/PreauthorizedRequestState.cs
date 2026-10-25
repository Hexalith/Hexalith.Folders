namespace Hexalith.Folders.Authorization;

/// <summary>Identifies one request whose authorization was already established by an outer transport boundary.</summary>
internal sealed record PreauthorizedRequestState(
    string TenantId,
    string PrincipalId,
    string? FolderId,
    string? FreshnessWatermark,
    string? OrganizationId,
    string CandidateActionToken,
    string HistoricalActionToken,
    string? DelegatorPrincipalId)
{
    /// <summary>Gets the task scope established by the outer protected-operation boundary.</summary>
    public string? TaskId { get; init; }
}
