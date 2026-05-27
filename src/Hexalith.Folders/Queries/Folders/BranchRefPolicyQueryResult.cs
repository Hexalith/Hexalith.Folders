using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Queries.Folders;

public sealed record BranchRefPolicyQueryResult(
    BranchRefPolicyQueryResultCode Code,
    string? FolderId,
    string? RepositoryBindingId,
    string? PolicyRef,
    string? DefaultRef,
    IReadOnlyList<string> AllowedRefPatterns,
    IReadOnlyList<string> ProtectedRefPatterns,
    string AuthorizationOutcome,
    FolderLifecycleFreshness Freshness,
    string? CorrelationId,
    string? TaskId,
    LayeredFolderAuthorizationResult? AuthorizationDenial);
