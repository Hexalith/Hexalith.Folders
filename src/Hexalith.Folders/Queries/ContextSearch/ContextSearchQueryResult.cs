using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// The metadata-only outcome of a context search. On a denied/absent/cross-tenant target, <see cref="Items"/> is
/// empty and <see cref="Freshness"/> reports <c>Stale</c> so denied and absent are externally indistinguishable.
/// </summary>
/// <param name="Code">The canonical result code.</param>
/// <param name="Items">The metadata-only hits (empty on any non-<c>Allowed</c> outcome).</param>
/// <param name="NextCursor">The opaque cursor for the next page, or null when there is none.</param>
/// <param name="Limits">The C4 query-bound metadata.</param>
/// <param name="Freshness">The read-consistency/freshness metadata.</param>
/// <param name="CorrelationId">The echoed correlation id.</param>
/// <param name="TaskId">The echoed task id.</param>
/// <param name="AuthorizationDenial">The layered-authorization denial, when the outcome was an authorization deny.</param>
public sealed record ContextSearchQueryResult(
    ContextSearchResultCode Code,
    IReadOnlyList<ContextSearchItem> Items,
    string? NextCursor,
    ContextSearchLimits Limits,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
