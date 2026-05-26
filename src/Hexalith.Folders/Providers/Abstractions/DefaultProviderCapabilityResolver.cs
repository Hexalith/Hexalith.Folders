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

        IGitProvider? provider = _providers.FirstOrDefault(p =>
            string.Equals(p.ProviderFamily, providerFamily, StringComparison.Ordinal)
            && string.Equals(p.ProviderKey, providerKey, StringComparison.Ordinal));

        return Task.FromResult(provider);
    }
}
