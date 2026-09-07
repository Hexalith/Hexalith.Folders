namespace Hexalith.Folders.Tests.Providers.Forgejo;

/// <summary>
/// Returns one preconfigured response for a Forgejo HTTP boundary test.
/// </summary>
internal sealed class ForgejoStaticHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}
