namespace Hexalith.Folders.Providers.Abstractions;

internal interface IProviderOperationSourceResolver
{
    ValueTask<ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>> ResolveFileMutationAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>> ResolveCommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>> ResolveStatusAsync(
        ProviderOperationStatusRequest request,
        CancellationToken cancellationToken = default);
}
