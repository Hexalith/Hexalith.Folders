namespace Hexalith.Folders.Server.Authorization;

/// <summary>Inputs to the unrouted PD10 v2 protected-operation authorization seam.</summary>
/// <param name="Authenticated">Whether authentication succeeded.</param>
/// <param name="AccessState">Canonical access state.</param>
/// <param name="AuthorityEvidence">Freshness and usability of authority evidence.</param>
/// <param name="OperationFamily">Protected operation family.</param>
/// <param name="FamilyGrantSatisfied">Whether the actor/family grant conjunct is satisfied.</param>
/// <param name="FolderScopeRequired">Whether this operation requires a folder authorization boundary.</param>
/// <param name="FolderAuthorityEstablished">Whether fresh authority for the route folder was established.</param>
/// <param name="TaskBindingRequired">Whether task-to-folder binding must be proven.</param>
/// <param name="TaskBelongsToFolder">Whether the task belongs to the authorized route folder.</param>
internal sealed record V2AuthorizationContext(
    bool Authenticated,
    V2AccessState AccessState,
    V2AuthorityEvidenceState AuthorityEvidence,
    V2ProtectedOperationFamily OperationFamily,
    bool FamilyGrantSatisfied,
    bool FolderScopeRequired,
    bool FolderAuthorityEstablished,
    bool TaskBindingRequired,
    bool TaskBelongsToFolder);
