using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

/// <summary>
/// Resolves the fixed operation sources used by the isolated Forgejo integration fixture.
/// </summary>
internal sealed class ForgejoStaticOperationSourceResolver : IProviderOperationSourceResolver
{
    /// <summary>
    /// Gets the staged-file source exposed to the fixture.
    /// </summary>
    public ProviderFileMutationResolvedSource? FileMutationSource { get; init; }

    /// <summary>
    /// Gets or sets the commit source exposed to the fixture.
    /// </summary>
    public ProviderCommitResolvedSource? CommitSource { get; set; }

    /// <summary>
    /// Gets or sets the status source exposed to the fixture.
    /// </summary>
    public ProviderOperationStatusResolvedSource? StatusSource { get; set; }

    /// <inheritdoc />
    public ValueTask<ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>> ResolveFileMutationAsync(
        ProviderFileMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(FileMutationSource is null
            ? ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>.Failure(
                ProviderFailureCategory.ProviderConfigurationMissing,
                "provider_file_mutation_source_unconfigured")
            : ProviderOperationSourceResolutionResult<ProviderFileMutationResolvedSource>.Success(FileMutationSource));
    }

    /// <inheritdoc />
    public ValueTask<ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>> ResolveCommitAsync(
        ProviderCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CommitSource is null
            ? ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>.Failure(
                ProviderFailureCategory.ProviderConfigurationMissing,
                "provider_commit_source_unconfigured")
            : ProviderOperationSourceResolutionResult<ProviderCommitResolvedSource>.Success(CommitSource));
    }

    /// <inheritdoc />
    public ValueTask<ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>> ResolveStatusAsync(
        ProviderOperationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(StatusSource is null
            ? ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>.Failure(
                ProviderFailureCategory.ProviderConfigurationMissing,
                "provider_operation_status_source_unconfigured")
            : ProviderOperationSourceResolutionResult<ProviderOperationStatusResolvedSource>.Success(StatusSource));
    }
}
