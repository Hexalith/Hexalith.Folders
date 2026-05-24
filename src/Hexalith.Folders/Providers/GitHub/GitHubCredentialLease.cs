namespace Hexalith.Folders.Providers.GitHub;

internal sealed class GitHubCredentialLease : IAsyncDisposable
{
    private GitHubCredentialLease(string accessToken)
    {
        AccessToken = accessToken;
    }

    internal string AccessToken { get; private set; }

    public static GitHubCredentialLease CreateForTesting(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return new(accessToken);
    }

    public ValueTask DisposeAsync()
    {
        AccessToken = string.Empty;
        return ValueTask.CompletedTask;
    }
}

