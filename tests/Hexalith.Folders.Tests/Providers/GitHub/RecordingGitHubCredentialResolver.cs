using Hexalith.Folders.Providers.Abstractions;
using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubCredentialResolver : IGitHubCredentialResolver
{
    private readonly Func<GitHubCredentialResolutionResult> _resultFactory;
    private GitHubCredentialResolutionResult? _lastResult;

    private RecordingGitHubCredentialResolver(Func<GitHubCredentialResolutionResult> resultFactory)
    {
        _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
    }

    public int Calls { get; private set; }

    public GitHubCredentialResolutionRequest? LastRequest { get; private set; }

    /// <summary>
    /// True only when a lease was actually handed out and then cleared. Testing the token for
    /// emptiness alone would also report "disposed" for a failure-shaped resolver that never issued
    /// a lease, so the assertion could pass without any disposal happening.
    /// </summary>
    public bool CredentialIsDisposed
        => _lastResult?.Credential is { } credential && string.IsNullOrEmpty(credential.AccessToken);

    public static RecordingGitHubCredentialResolver Success(string token)
    {
        GitHubCredentialResolutionResult result = GitHubCredentialResolutionResult.Success(GitHubCredentialLease.CreateForTesting(token));
        return new(() => result);
    }

    public static RecordingGitHubCredentialResolver SuccessPerCall(string token)
        => new(() => GitHubCredentialResolutionResult.Success(GitHubCredentialLease.CreateForTesting(token)));

    public static RecordingGitHubCredentialResolver Failure(
        ProviderFailureCategory category,
        string reasonCode,
        TimeSpan? retryAfter = null)
    {
        GitHubCredentialResolutionResult result = GitHubCredentialResolutionResult.Failure(category, reasonCode, retryAfter);
        return new(() => result);
    }

    public ValueTask<GitHubCredentialResolutionResult> ResolveAsync(
        GitHubCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        LastRequest = request;
        _lastResult = _resultFactory();
        return ValueTask.FromResult(_lastResult);
    }
}
