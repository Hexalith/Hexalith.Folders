using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class UnavailableWorkspaceCommitExecutor : IWorkspaceCommitExecutor
{
    public Task<WorkspaceCommitExecutionResult> CommitAsync(
        WorkspaceCommitExecutionRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(WorkspaceCommitExecutionResult.KnownFailure(
            ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode()));
}
