using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// An authorized request for the metadata-only indexing-status of every file version in a tenant-scoped folder,
/// backing the read-only console projection. Client-controlled tenant/principal values are comparison inputs only.
/// </summary>
/// <param name="FolderId">The opaque folder identifier whose indexing statuses are requested.</param>
/// <param name="AuthoritativeTenantId">The authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">The authoritative principal id from authenticated context.</param>
/// <param name="ClaimTransformEvidence">The EventStore claim-transform evidence for the action.</param>
/// <param name="CorrelationId">The optional caller-provided correlation id.</param>
/// <param name="TaskId">The optional caller-provided task id.</param>
/// <param name="ClientControlledTenantValues">Client-supplied tenant values used only for comparison.</param>
/// <param name="ClientControlledPrincipalValues">Client-supplied principal values used only for comparison.</param>
public sealed record FolderIndexingStatusQuery(
    string? FolderId,
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues);
