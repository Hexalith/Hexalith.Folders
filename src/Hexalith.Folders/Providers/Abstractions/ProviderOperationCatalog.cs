namespace Hexalith.Folders.Providers.Abstractions;

public static class ProviderOperationCatalog
{
    public const string ReadinessValidation = "readiness_validation";
    public const string RepositoryCreation = "repository_creation";
    public const string RepositoryBinding = "repository_binding";
    public const string BranchRefInspection = "branch_ref_inspection";
    public const string WorkspacePreparation = "workspace_preparation";
    public const string FileMutationSupport = "file_mutation_support";
    public const string CommitSupport = "commit_support";
    public const string StatusQuery = "status_query";
    public const string CleanupExpiration = "cleanup_expiration";
    public const string ProviderSupportEvidence = "provider_support_evidence";

    public static IReadOnlyList<string> CanonicalOperationIds { get; } =
    [
        ReadinessValidation,
        RepositoryCreation,
        RepositoryBinding,
        BranchRefInspection,
        WorkspacePreparation,
        FileMutationSupport,
        CommitSupport,
        StatusQuery,
        CleanupExpiration,
        ProviderSupportEvidence,
    ];
}
