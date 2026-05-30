namespace Hexalith.Folders.Providers.Abstractions;

internal interface IProviderCredentialReferenceResolver
{
    ValueTask<ProviderCredentialReferenceResolutionResult> ResolveAsync(
        ProviderCredentialReferenceResolutionRequest request,
        CancellationToken cancellationToken = default);
}
