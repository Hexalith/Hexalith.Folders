using System.Net;
using System.Net.Http.Json;

namespace Hexalith.Folders.AppHost.Tests;

internal sealed class RecordingDurabilityHttpHandler(Func<int, HttpResponseMessage> response) : HttpMessageHandler
{
    internal List<(string Method, string Uri, byte[] Body, Dictionary<string, string> Headers)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method.Method, request.RequestUri!.ToString(),
            request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false),
            request.Headers.ToDictionary(static header => header.Key, static header => string.Join(",", header.Value))));
        HttpResponseMessage result = response(Requests.Count);
        result.Headers.RetryAfter = new(TimeSpan.Zero);
        return result;
    }

    internal static HttpResponseMessage Problem(HttpStatusCode status, string code, string category, bool retryable)
        => new(status) { Content = JsonContent.Create(new { code, category, retryable }) };
}
