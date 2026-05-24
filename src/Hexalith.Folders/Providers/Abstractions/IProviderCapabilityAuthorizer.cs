namespace Hexalith.Folders.Providers.Abstractions;

public interface IProviderCapabilityAuthorizer
{
    Task<ProviderCapabilityAuthorizationResult> AuthorizeAsync(
        ProviderCapabilityDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}
