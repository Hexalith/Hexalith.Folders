using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Result of <see cref="WorkspaceTransitionEvidenceQueryHandler"/>. Metadata-only.
/// </summary>
/// <param name="Code">Outcome code.</param>
/// <param name="Snapshot">The transition evidence snapshot (present only when allowed).</param>
/// <param name="Freshness">Read freshness metadata.</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="TaskId">Task id.</param>
/// <param name="AuthorizationDenial">Layered-authorization denial detail, when applicable.</param>
public sealed record WorkspaceTransitionEvidenceQueryResult(
    WorkspaceTransitionEvidenceQueryResultCode Code,
    WorkspaceTransitionEvidenceSnapshot? Snapshot,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
