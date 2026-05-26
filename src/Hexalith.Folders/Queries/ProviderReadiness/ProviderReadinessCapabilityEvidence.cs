namespace Hexalith.Folders.Queries.ProviderReadiness;

public sealed record ProviderReadinessCapabilityEvidence(
    string RepositoryCreation,
    string ExistingRepositoryBinding,
    string BranchRefPolicy,
    string FileOperations,
    string CommitStatus,
    string ProviderErrors,
    string FailureBehavior);
