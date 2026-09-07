using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Providers.Abstractions;

public sealed class DefaultProviderCapabilityResolver(IEnumerable<IGitProvider> providers) : IProviderCapabilityResolver
{
    private readonly IReadOnlyList<IGitProvider> _providers = providers?.ToArray() ?? throw new ArgumentNullException(nameof(providers));

    public Task<IGitProvider?> ResolveAsync(
        string providerFamily,
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool canonicalGitHubIdentity = string.Equals(providerFamily, GitHubProviderConstants.ProviderFamily, StringComparison.Ordinal)
            && string.Equals(providerKey, GitHubProviderConstants.ProviderKey, StringComparison.Ordinal);
        IGitProvider[] matchingProviders = _providers
            .Where(p => string.Equals(p.ProviderFamily, providerFamily, StringComparison.Ordinal)
                && string.Equals(p.ProviderKey, providerKey, StringComparison.Ordinal))
            .ToArray();
        IGitProvider? provider = canonicalGitHubIdentity
            ? _providers.LastOrDefault(static provider => provider is GitHubProvider)
            : matchingProviders.LastOrDefault(static provider => provider is ICanonicalProviderAdapter)
                ?? matchingProviders.FirstOrDefault();

        return Task.FromResult(provider);
    }
}
