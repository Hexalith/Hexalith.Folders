namespace Hexalith.Folders.Providers.GitHub;

internal sealed class PooledGitHubHttpMessageHandler(HttpMessageHandler innerHandler) : HttpMessageHandler
{
    private readonly HttpMessageInvoker _invoker = new(
        innerHandler ?? throw new ArgumentNullException(nameof(innerHandler)),
        disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _invoker.SendAsync(request, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _invoker.Dispose();
        }

        base.Dispose(disposing);
    }
}
