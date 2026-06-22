using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

/// <summary>
/// Query for a workspace's most recent C6 lifecycle transition evidence.
/// </summary>
/// <param name="AuthoritativeTenantId">Authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">Authoritative caller principal id.</param>
/// <param name="ClaimTransformEvidence">EventStore claim-transform evidence for the read action token.</param>
/// <param name="FolderId">Folder id.</param>
/// <param name="WorkspaceId">Workspace id.</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="TaskId">Task id.</param>
/// <param name="ClientControlledTenantValues">Client-asserted tenant signals (comparison inputs only).</param>
/// <param name="ClientControlledPrincipalValues">Client-asserted principal signals (comparison inputs only).</param>
public sealed record WorkspaceTransitionEvidenceQuery(
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    string WorkspaceId,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues);
