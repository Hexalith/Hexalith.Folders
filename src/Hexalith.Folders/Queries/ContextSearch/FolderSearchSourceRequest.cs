namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// A Memories-free request to the search source, carrying only the authoritative scope the handler already
/// authorized. The source constrains the index query by these values as defense in depth; the handler remains the
/// load-bearing security boundary and re-checks every hit before hydration.
/// </summary>
/// <param name="ManagedTenantId">The authoritative managed tenant id (primary security-trim key).</param>
/// <param name="OrganizationId">The authoritative organization id, or empty when not resolved.</param>
/// <param name="FolderId">The authorized folder id.</param>
/// <param name="WorkspaceId">The workspace id scoping the search.</param>
/// <param name="PrincipalId">The authoritative principal id.</param>
/// <param name="ActionToken">The authorized action token.</param>
/// <param name="TaskId">The optional task id.</param>
/// <param name="CorrelationId">The optional correlation id.</param>
/// <param name="AuthorizationWatermark">The freshness watermark from the authorization decision.</param>
/// <param name="QueryText">The raw search text (never audited).</param>
/// <param name="Limit">The effective C4 result limit.</param>
/// <param name="Offset">The pagination offset.</param>
public sealed record FolderSearchSourceRequest(
    string ManagedTenantId,
    string OrganizationId,
    string FolderId,
    string WorkspaceId,
    string PrincipalId,
    string ActionToken,
    string? TaskId,
    string? CorrelationId,
    string? AuthorizationWatermark,
    string QueryText,
    int Limit,
    int Offset);
