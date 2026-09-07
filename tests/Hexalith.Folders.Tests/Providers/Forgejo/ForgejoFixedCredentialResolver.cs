using Hexalith.Folders.Providers.Forgejo;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

/// <summary>
/// Supplies an isolated fixture token to the production-resolved Forgejo adapter.
/// </summary>
internal sealed class ForgejoFixedCredentialResolver(string accessToken) : IForgejoCredentialResolver
{
    /// <inheritdoc />
    public ValueTask<ForgejoCredentialResolutionResult> ResolveAsync(
        ForgejoCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            ForgejoCredentialResolutionResult.Success(ForgejoCredentialLease.CreateForTesting(accessToken)));
    }
}
