using Hexalith.Folders.Providers.GitHub;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubApiClientFactory(
    IGitHubApiClient client,
    Exception? exception = null) : IGitHubApiClientFactory
{
    public int Calls { get; private set; }

    public GitHubApiClientRequest? LastRequest { get; private set; }

    public bool CredentialWasAvailableAtCreation { get; private set; }

    public static RecordingGitHubApiClientFactory Throws(Exception failure)
        => new(RecordingGitHubApiClient.Success(), failure);

    public ValueTask<IGitHubApiClient> CreateAsync(
        GitHubApiClientRequest request,
        GitHubCredentialLease credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        LastRequest = request;
        CredentialWasAvailableAtCreation = !string.IsNullOrWhiteSpace(credential.AccessToken);
        if (exception is not null)
        {
            throw exception;
        }

        return ValueTask.FromResult(client);
    }
}
