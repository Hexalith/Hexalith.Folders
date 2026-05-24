namespace Hexalith.Folders.Providers.Forgejo;

internal sealed class ForgejoCredentialLease : IAsyncDisposable
{
    private ForgejoCredentialLease(string accessToken)
    {
        AccessToken = accessToken;
    }

    internal string AccessToken { get; private set; }

    public static ForgejoCredentialLease CreateForTesting(string accessToken)
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
