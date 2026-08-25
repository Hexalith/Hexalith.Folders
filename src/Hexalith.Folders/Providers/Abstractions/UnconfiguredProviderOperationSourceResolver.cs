namespace Hexalith.Folders.Providers.Abstractions;

internal sealed class UnconfiguredProviderOperationSourceResolver : IProviderOperationSourceResolver
{
    public ValueTask<ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>> ResolveFileMutationAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_file_mutation_source_unconfigured"));
    }

    public ValueTask<ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>> ResolveCommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_commit_source_unconfigured"));
    }

    public ValueTask<ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>> ResolveStatusAsync(
        ProviderOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>.Failure(
            ProviderFailureCategory.ProviderConfigurationMissing,
            "provider_operation_status_source_unconfigured"));
    }
}
