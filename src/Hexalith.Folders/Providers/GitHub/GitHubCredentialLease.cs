namespace Hexalith.Folders.Providers.GitHub;

internal sealed class GitHubCredentialLease : IAsyncDisposable
{
    private readonly Func<ValueTask>? _disposeAction;

    internal GitHubCredentialLease(string accessToken, Func<ValueTask>? disposeAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        AccessToken = accessToken;
        _disposeAction = disposeAction;
    }

    internal string AccessToken { get; private set; }

    public static GitHubCredentialLease CreateForTesting(
        string accessToken,
        Func<ValueTask>? disposeAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return new(accessToken, disposeAction);
    }

    public async ValueTask DisposeAsync()
    {
        AccessToken = string.Empty;
        if (_disposeAction is not null)
        {
            await _disposeAction().ConfigureAwait(false);
        }
    }
}
