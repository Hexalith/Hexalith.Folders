using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// An authorized request to search the Memories search index (<c>folders-index</c>) for file versions a caller is
/// permitted to see, scoped to one tenant/folder/workspace. Client-controlled tenant/principal values are
/// comparison inputs only; the authoritative tenant and principal come from authenticated context and the
/// EventStore claim-transform evidence.
/// </summary>
/// <param name="FolderId">The opaque folder identifier whose indexed content is searched.</param>
/// <param name="WorkspaceId">The opaque workspace identifier scoping the search.</param>
/// <param name="AuthoritativeTenantId">The authoritative managed tenant id from authenticated context.</param>
/// <param name="PrincipalId">The authoritative principal id from authenticated context.</param>
/// <param name="ClaimTransformEvidence">The EventStore claim-transform evidence for the action.</param>
/// <param name="CorrelationId">The optional caller-provided correlation id.</param>
/// <param name="TaskId">The optional caller-provided task id (task-scoped query).</param>
/// <param name="ClientControlledTenantValues">Client-supplied tenant values used only for comparison.</param>
/// <param name="ClientControlledPrincipalValues">Client-supplied principal values used only for comparison.</param>
/// <param name="QueryText">The raw search text (request payload only; never audited or echoed).</param>
/// <param name="Limit">The optional caller-requested result limit (bounded by C4).</param>
/// <param name="Cursor">The optional opaque, non-secret pagination cursor from a prior call.</param>
public sealed record ContextSearchQuery(
    string? FolderId,
    string? WorkspaceId,
    string? AuthoritativeTenantId,
    string? PrincipalId,
    EventStoreClaimTransformEvidence ClaimTransformEvidence,
    string? CorrelationId,
    string? TaskId,
    IReadOnlyDictionary<string, string?>? ClientControlledTenantValues,
    IReadOnlyDictionary<string, string?>? ClientControlledPrincipalValues,
    string? QueryText,
    int? Limit = null,
    string? Cursor = null);
