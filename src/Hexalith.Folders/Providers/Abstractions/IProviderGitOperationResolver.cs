namespace Hexalith.Folders.Providers.Abstractions;

internal interface IProviderGitOperationResolver
{
    ValueTask<ProviderGitChangeSetResolutionResult> ResolveChangeSetAsync(
        ProviderFileChangeSetRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ProviderGitCommitResolutionResult> ResolveCommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ProviderGitStatusResolutionResult> ResolveStatusAsync(
        ProviderMutationStatusRequest request,
        CancellationToken cancellationToken = default);
}
