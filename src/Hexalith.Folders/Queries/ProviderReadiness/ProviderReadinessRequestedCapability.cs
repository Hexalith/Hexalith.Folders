namespace Hexalith.Folders.Queries.ProviderReadiness;

public enum ProviderReadinessRequestedCapability
{
    RepositoryCreation,
    ExistingRepositoryBinding,
    BranchRefPolicy,
    WorkspacePreparation,
    FileOperations,
    CommitStatus,
    ProviderErrors,
    FailureBehavior,
}
