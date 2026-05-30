using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class DaprBackedForgejoCredentialResolver(IProviderCredentialReferenceResolver referenceResolver) : IForgejoCredentialResolver
{
    private readonly IProviderCredentialReferenceResolver _referenceResolver =
        referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));

    public async ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
        ForgejoCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProviderCredentialReferenceResolutionResult result = await _referenceResolver.ResolveAsync(
            new ProviderCredentialReferenceResolutionRequest(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                ForgejoProviderConstants.ProviderFamily,
                ForgejoProviderConstants.ProviderKey,
                request.CredentialMode,
                request.AuthorizationEvidenceFingerprint,
                request.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? ForgejoCredentialResolutionResult.Success(new ForgejoCredentialLease(result.AccessToken!))
            : ForgejoCredentialResolutionResult.Failure(result.FailureCategory, result.ReasonCode, result.RetryAfter);
    }
}
