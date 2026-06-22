using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Aggregates.Folder;

/// <summary>
/// Request to create a plain (non repository-backed) folder. Identity, tenant, and organization
/// are resolved from authorization evidence; the caller supplies only display metadata.
/// </summary>
/// <param name="AuthoritativeTenantId">Authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">Authoritative caller principal id.</param>
/// <param name="ClaimTransformEvidence">EventStore claim-transform evidence for the create action token.</param>
/// <param name="FolderId">Server-assigned canonical folder id (deterministic per idempotency key).</param>
/// <param name="DisplayName">Folder display name from the request metadata.</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="TaskId">Task id.</param>
/// <param name="IdempotencyKey">Idempotency key.</param>
/// <param name="PayloadTenantId">Optional client-asserted payload tenant id (comparison input only).</param>
/// <param name="ClientControlledTenantValues">Client-asserted tenant signals (comparison inputs only).</param>
/// <param name="ClientControlledPrincipalValues">Client-asserted principal signals (comparison inputs only).</param>
public sealed record FolderCreationRequest(
    string? AuthoritativeTenantId,
    string PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string FolderId,
    string DisplayName,
    string CorrelationId,
    string TaskId,
    string IdempotencyKey,
    string? PayloadTenantId,
    IReadOnlyDictionary<string, string?> ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?> ClientControlledPrincipalValues);
