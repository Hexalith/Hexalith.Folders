using Hexalith.Folders.Authorization;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Queries.FolderAccess;

/// <summary>
/// Result of <see cref="ListFolderAclEntriesQueryHandler"/>. Metadata-only.
/// </summary>
/// <param name="Code">Outcome code.</param>
/// <param name="Entries">ACL entries (empty unless allowed).</param>
/// <param name="Freshness">Read freshness metadata.</param>
/// <param name="CorrelationId">Correlation id.</param>
/// <param name="AuthorizationDenial">Layered-authorization denial detail, when applicable.</param>
public sealed record ListFolderAclEntriesQueryResult(
    ListFolderAclEntriesQueryResultCode Code,
    IReadOnlyList<FolderAclEntryView> Entries,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
