using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record FolderLifecycleStatusQueryResult(
    FolderLifecycleStatusResultCode Code,
    string? FolderId,
    string? LifecycleState,
    bool Archived,
    string? RepositoryBindingId,
    string? ProviderBindingRef,
    string AuthorizationOutcome,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    string OperationId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
