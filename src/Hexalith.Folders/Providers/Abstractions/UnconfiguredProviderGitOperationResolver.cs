namespace Hexalith.Folders.Providers.Abstractions;

internal sealed class UnconfiguredProviderGitOperationResolver : IProviderGitOperationResolver
{
    public ValueTask<ProviderGitChangeSetResolutionResult> ResolveChangeSetAsync(
        ProviderFileChangeSetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderGitChangeSetResolutionResult.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_file_change_set_source_unconfigured"));
    }

    public ValueTask<ProviderGitCommitResolutionResult> ResolveCommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderGitCommitResolutionResult.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_commit_source_unconfigured"));
    }

    public ValueTask<ProviderGitStatusResolutionResult> ResolveStatusAsync(
        ProviderMutationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderGitStatusResolutionResult.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_status_source_unconfigured"));
    }
}
