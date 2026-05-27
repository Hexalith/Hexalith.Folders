namespace Hexalith.Folders.Queries.Folders;

public sealed record BranchRefPolicyReadModelSnapshot(
    string ManagedTenantId,
    string FolderId,
    string RepositoryBindingId,
    string PolicyRef,
    string DefaultRef,
    IReadOnlyList<string> AllowedRefPatterns,
    IReadOnlyList<string> ProtectedRefPatterns,
    FolderLifecycleFreshness Freshness,
    FolderLifecycleEvidenceScope EvidenceScope);
