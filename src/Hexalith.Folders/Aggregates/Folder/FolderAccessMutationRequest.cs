using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>
/// Request to grant or revoke folder ACL operations. Tenant and organization are resolved from
/// authorization evidence; the caller supplies the resolved access operations.
/// </summary>
/// <param name="AuthoritativeTenantId">Authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">Authoritative caller principal id.</param>
/// <param name="ClaimTransformEvidence">EventStore claim-transform evidence for the manage-access action token.</param>
/// <param name="FolderId">Folder the ACL operations apply to.</param>
/// <param name="Intent">Whether the operations grant or revoke (selects the command type).</param>
/// <param name="Operations">Resolved access operations (one per ACL entry).</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="TaskId">Task id.</param>
/// <param name="IdempotencyKey">Idempotency key.</param>
/// <param name="PayloadTenantId">Optional client-asserted payload tenant id (comparison input only).</param>
/// <param name="ClientControlledTenantValues">Client-asserted tenant signals (comparison inputs only).</param>
/// <param name="ClientControlledPrincipalValues">Client-asserted principal signals (comparison inputs only).</param>
public sealed record FolderAccessMutationRequest(
    string? AuthoritativeTenantId,
    string PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    FolderAccessOperationIntent Intent,
    IReadOnlyList<FolderAccessOperation> Operations,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues);
