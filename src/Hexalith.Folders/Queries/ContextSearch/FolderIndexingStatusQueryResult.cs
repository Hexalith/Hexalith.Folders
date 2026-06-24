using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Queries.ContextSearch;

/// <summary>
/// The metadata-only outcome of the indexing-status projection read. On any non-<c>Allowed</c> outcome
/// <see cref="Items"/> is empty and <see cref="Freshness"/> reports <c>Stale</c>, so denied and absent are
/// externally indistinguishable.
/// </summary>
/// <param name="Code">The canonical result code.</param>
/// <param name="Items">The metadata-only status entries (empty on any non-<c>Allowed</c> outcome).</param>
/// <param name="IsTruncated">Whether the folder has more entries than the bounded page returned.</param>
/// <param name="Freshness">The read-consistency/freshness metadata.</param>
/// <param name="CorrelationId">The echoed correlation id.</param>
/// <param name="TaskId">The echoed task id.</param>
/// <param name="AuthorizationDenial">The layered-authorization denial, when the outcome was an authorization deny.</param>
public sealed record FolderIndexingStatusQueryResult(
    FolderIndexingStatusResultCode Code,
    IReadOnlyList<FolderIndexingStatusItem> Items,
    bool IsTruncated,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
