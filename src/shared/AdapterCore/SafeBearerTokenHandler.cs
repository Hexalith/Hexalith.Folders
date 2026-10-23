using System.Net.Http.Headers;

namespace Hexalith.Folders.AdapterCore;

/// <summary>Validates the actual outgoing URI before acquiring and attaching bearer credentials.</summary>
internal class SafeBearerTokenHandler : DelegatingHandler
{
    private readonly Func<string?> _resolveToken;

    /// <summary>Initializes a handler with a lazy credential source.</summary>
    protected SafeBearerTokenHandler(Func<string?> resolveToken)
    {
        ArgumentNullException.ThrowIfNull(resolveToken);
        _resolveToken = resolveToken;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        BearerTransportPolicy.Validate(request.RequestUri);
        cancellationToken.ThrowIfCancellationRequested();

        string? token = _resolveToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
