using Hexalith.Folders.Providers.Abstractions;

namespace Hexalith.Folders.Providers.GitHub;

internal sealed class DaprBackedGitHubCredentialResolver(IProviderCredentialReferenceResolver referenceResolver) : IGitHubCredentialResolver
{
    private readonly IProviderCredentialReferenceResolver _referenceResolver =
        referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));

    public async ValueTask<GitHubCredentialResolutionResult> ResolveAsync(
        GitHubCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ProviderCredentialReferenceResolutionResult result = await _referenceResolver.ResolveAsync(
            new ProviderCredentialReferenceResolutionRequest(
                request.ManagedTenantId,
                request.OrganizationId,
                request.ProviderBindingRef,
                request.CredentialReferenceId,
                GitHubProviderConstants.ProviderFamily,
                GitHubProviderConstants.ProviderKey,
                request.CredentialMode,
                request.AuthorizationEvidenceFingerprint,
                request.CorrelationId),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? GitHubCredentialResolutionResult.Success(GitHubCredentialLease.CreateForTesting(result.AccessToken!))
            : GitHubCredentialResolutionResult.Failure(result.FailureCategory, result.ReasonCode, result.RetryAfter);
    }
}
