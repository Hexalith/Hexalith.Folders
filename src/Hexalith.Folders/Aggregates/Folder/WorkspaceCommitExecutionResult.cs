namespace Hexalith.Folders.Aggregates.Folder;

public sealed record WorkspaceCommitExecutionResult(
    WorkspaceCommitExecutionStatus Status,
    string ProviderOutcomeCategory,
    string? CommitReference,
    string? FailureCategory,
    string? ReconciliationReference)
{
    public static WorkspaceCommitExecutionResult Succeeded(
        string commitReference,
        string providerOutcomeCategory = "success")
        => new(
            WorkspaceCommitExecutionStatus.Succeeded,
            providerOutcomeCategory,
            commitReference,
            FailureCategory: null,
            ReconciliationReference: null);

    public static WorkspaceCommitExecutionResult KnownFailure(string failureCategory)
        => new(
            WorkspaceCommitExecutionStatus.KnownFailure,
            failureCategory,
            CommitReference: null,
            FailureCategory: failureCategory,
            ReconciliationReference: null);

    public static WorkspaceCommitExecutionResult UnknownOutcome(string reconciliationReference)
        => new(
            WorkspaceCommitExecutionStatus.UnknownOutcome,
            "unknown_provider_outcome",
            CommitReference: null,
            FailureCategory: null,
            ReconciliationReference: reconciliationReference);

    public static WorkspaceCommitExecutionResult ReconciliationRequired(string reconciliationReference)
        => new(
            WorkspaceCommitExecutionStatus.ReconciliationRequired,
            "reconciliation_required",
            CommitReference: null,
            FailureCategory: null,
            ReconciliationReference: reconciliationReference);
}
