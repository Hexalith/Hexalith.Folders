namespace Hexalith.Folders.Providers.Abstractions;

public interface IProviderCapabilityResolver
{
    Task<IGitProvider?> ResolveAsync(
        string providerFamily,
        string providerKey,
        CancellationToken cancellationToken = default);
}
