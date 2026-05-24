namespace Hexalith.Folders.Providers.GitHub;

internal sealed record GitHubPermissionEvidence(
    bool SupportsRepositoryCreation,
    bool SupportsRepositoryBinding,
    bool SupportsBranchRefInspection,
    bool SupportsFileMutation,
    bool SupportsCommit,
    bool SupportsStatus,
    bool SupportsMetadata);

