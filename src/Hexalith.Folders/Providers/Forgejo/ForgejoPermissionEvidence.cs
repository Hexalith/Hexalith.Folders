namespace Hexalith.Folders.Providers.Forgejo;

internal sealed record ForgejoPermissionEvidence(
    bool SupportsRepositoryCreation,
    bool SupportsRepositoryBinding,
    bool SupportsBranchRefInspection,
    bool SupportsFileMutation,
    bool SupportsCommit,
    bool SupportsStatus,
    bool SupportsMetadata,
    bool SupportsPagination,
    bool SupportsContentsApi,
    string RequiredScopePosture);
