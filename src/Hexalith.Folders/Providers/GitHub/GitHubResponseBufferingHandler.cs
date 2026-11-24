namespace Hexalith.Folders.Providers.GitHub;

/// <summary>Reads Octokit response bodies while the HTTP request deadline is still active.</summary>
internal sealed class GitHubResponseBufferingHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
