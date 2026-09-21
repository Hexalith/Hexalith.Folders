namespace Hexalith.Folders.Server.Authorization;

/// <summary>
/// Carries already-derived authority facts for one protected v2 candidate operation.
/// </summary>
/// <param name="IsAuthenticated">Whether authentication completed successfully.</param>
/// <param name="AccessState">The canonical access state being evaluated.</param>
/// <param name="AuthorityEvidence">Freshness and usability of the authority evidence.</param>
/// <param name="TenantAllowed">Whether the authenticated principal has fresh tenant authority.</param>
/// <param name="FolderAllowed">Whether the principal has the required fresh folder grant.</param>
/// <param name="FamilyAllowed">Whether the operation-family grant is satisfied.</param>
/// <param name="ScopeAllowed">Whether every derived scope dimension is bound to the authorized parent.</param>
/// <param name="DelegationAllowed">Whether both actor and delegator independently satisfy the required grant.</param>
/// <param name="RequiresTaskFolderBinding">Whether task-to-folder binding must be proven after parent authorization.</param>
internal sealed record Pd10AuthorizationContext(
    bool IsAuthenticated,
    V2AccessState AccessState,
    Pd10AuthorityEvidenceState AuthorityEvidence,
    bool TenantAllowed,
    bool FolderAllowed,
    bool FamilyAllowed,
    bool ScopeAllowed,
    bool DelegationAllowed,
    bool RequiresTaskFolderBinding = false);
