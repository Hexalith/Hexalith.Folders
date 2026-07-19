namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class RecordingGitHubHttpMessageHandler(
    Func<RecordedGitHubHttpRequest, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
{
    private readonly Func<RecordedGitHubHttpRequest, CancellationToken, Task<HttpResponseMessage>> _responseFactory =
        responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));

    public List<RecordedGitHubHttpRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> contentHeaders = request.Content is null
            ? []
            : request.Content.Headers;
        Dictionary<string, string[]> headers = request.Headers
            .Concat(contentHeaders)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        RecordedGitHubHttpRequest recorded = new(
            request.Method,
            request.RequestUri ?? throw new InvalidOperationException("The GitHub request URI is required."),
            headers,
            body);
        Requests.Add(recorded);
        return await _responseFactory(recorded, cancellationToken).ConfigureAwait(false);
    }
}
