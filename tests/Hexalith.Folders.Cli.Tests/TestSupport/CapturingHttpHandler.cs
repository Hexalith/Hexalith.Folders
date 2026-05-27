using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hexalith.Folders.Cli.Tests.TestSupport;

/// <summary>
/// Fake <see cref="HttpMessageHandler"/> that records the outgoing request and returns a canned response.
/// Used with the real generated client to assert wire-level behavior (header propagation, transport shape)
/// without a live server, mirroring the Story 5.1 sample tests.
/// </summary>
internal sealed class CapturingHttpHandler(HttpStatusCode statusCode, string responseJson) : HttpMessageHandler
{
    /// <summary>Gets the captured request, if any.</summary>
    public HttpRequestMessage? Request { get; private set; }

    /// <summary>Gets the captured request body, if any.</summary>
    public string? RequestBody { get; private set; }

    /// <summary>Returns the first value of a captured request header, or <see langword="null"/>.</summary>
    /// <param name="name">The header name.</param>
    /// <returns>The header value, or <see langword="null"/>.</returns>
    public string? Header(string name)
        => Request is not null && Request.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        if (request.Content is not null)
        {
            RequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };
    }
}
