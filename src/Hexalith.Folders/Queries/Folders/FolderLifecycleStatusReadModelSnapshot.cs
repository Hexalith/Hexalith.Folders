namespace Hexalith.Folders.Queries.Folders;

public sealed record FolderLifecycleStatusReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    FolderLifecycleProjectionState LifecycleState,
    FolderRepositoryBindingStatus BindingStatus,
    string? RepositoryBindingId,
    string? ProviderBindingRef,
    FolderLifecycleFreshness Freshness,
    FolderLifecycleEvidenceScope EvidenceScope,
    IReadOnlyList<string> DiagnosticSentinels);
