using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubCredentialResolver(
    GitHubCredentialResolutionResult result,
    Exception? exception = null) : IGitHubCredentialResolver
{
    public int Calls { get; private set; }

    public GitHubCredentialResolutionRequest? LastRequest { get; private set; }

    public bool CredentialIsDisposed => string.IsNullOrEmpty(result.Credential?.AccessToken);

    public static RecordingGitHubCredentialResolver Success(string token)
        => new(GitHubCredentialResolutionResult.Success(GitHubCredentialLease.CreateForTesting(token)));

    public static RecordingGitHubCredentialResolver Success(
        string token,
        Func<ValueTask> disposeAction)
        => new(GitHubCredentialResolutionResult.Success(GitHubCredentialLease.CreateForTesting(token, disposeAction)));

    public static RecordingGitHubCredentialResolver Throws(Exception failure)
        => new(GitHubCredentialResolutionResult.Failure(
            ProviderFailureCategory.ProviderUnavailable,
            "unused"), failure);

    public static RecordingGitHubCredentialResolver Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
        => new(GitHubCredentialResolutionResult.Failure(category, reasonCode, retryAfter));

    public ValueTask<GitHubCredentialResolutionResult> ResolveAsync(
        GitHubCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        LastRequest = request;
        if (exception is not null)
        {
            throw exception;
        }

        return ValueTask.FromResult(result);
    }
}
