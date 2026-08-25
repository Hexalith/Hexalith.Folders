using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Testing.Providers;

public sealed class RecordingProviderCapabilityResolver(IGitProvider provider) : IProviderCapabilityResolver
{
    private readonly IGitProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public int Calls { get; private set; }

    public int ProviderCalls { get; private set; }

    /// <summary>Gets the last binding request forwarded to the provider.</summary>
    public ProviderRepositoryBindingRequest? LastBindingRequest { get; private set; }

    /// <summary>Gets the last creation request forwarded to the provider.</summary>
    public ProviderRepositoryCreationRequest? LastCreationRequest { get; private set; }

    /// <summary>Gets the last file-mutation request forwarded to the provider.</summary>
    public ProviderFileMutationRequest? LastFileMutationRequest { get; private set; }

    /// <summary>Gets the last commit request forwarded to the provider.</summary>
    public ProviderCommitRequest? LastCommitRequest { get; private set; }

    /// <summary>Gets the last status request forwarded to the provider.</summary>
    public ProviderOperationStatusRequest? LastStatusRequest { get; private set; }

    public Task<IGitProvider?> ResolveAsync(
        string providerFamily,
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult<IGitProvider?>(new RecordingGitProvider(_provider, this));
    }

    private sealed class RecordingGitProvider(IGitProvider inner, RecordingProviderCapabilityResolver owner) : IGitProvider
    {
        public string ProviderFamily => inner.ProviderFamily;

        public string ProviderKey => inner.ProviderKey;

        public async Task<ProviderCapabilityDiscoveryResult> DiscoverCapabilitiesAsync(
            ProviderCapabilityDiscoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            owner.ProviderCalls++;
            return await inner.DiscoverCapabilitiesAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProviderRepositoryCreationResult> CreateRepositoryAsync(
            ProviderRepositoryCreationRequest request,
            CancellationToken cancellationToken = default)
        {
            owner.ProviderCalls++;
            owner.LastCreationRequest = request;
            return await inner.CreateRepositoryAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProviderRepositoryBindingResult> ValidateRepositoryBindingAsync(
            ProviderRepositoryBindingRequest request,
            CancellationToken cancellationToken = default)
        {
            owner.ProviderCalls++;
            owner.LastBindingRequest = request;
            return await inner.ValidateRepositoryBindingAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProviderFileMutationResult> StageFileChangesAsync(
            ProviderFileMutationRequest request,
            CancellationToken cancellationToken = default)
        {
            owner.ProviderCalls++;
            owner.LastFileMutationRequest = request;
            return await inner.StageFileChangesAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProviderCommitResult> CommitAsync(
            ProviderCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            owner.ProviderCalls++;
            owner.LastCommitRequest = request;
            return await inner.CommitAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ProviderOperationStatusResult> GetOperationStatusAsync(
            ProviderOperationStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            owner.ProviderCalls++;
            owner.LastStatusRequest = request;
            return await inner.GetOperationStatusAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public ProviderCapabilityComparisonResult CompareCapabilityProfiles(
            ProviderCapabilityProfile current,
            ProviderCapabilityProfile candidate)
            => inner.CompareCapabilityProfiles(current, candidate);
    }
}
