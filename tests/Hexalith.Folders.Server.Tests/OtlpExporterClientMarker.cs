namespace Hexalith.Folders.Server.Tests;

/// <summary>Marks requests issued through a named exporter client in the composed HTTP factory.</summary>
/// <param name="clientName">The name of the exporting client.</param>
internal sealed class OtlpExporterClientMarker(string clientName) : DelegatingHandler
{
    /// <summary>Gets the header observed by the test collector.</summary>
    internal const string HeaderName = "X-Test-Otlp-Client";

    /// <inheritdoc/>
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(HeaderName, clientName);
        return base.Send(request, cancellationToken);
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(HeaderName, clientName);
        return base.SendAsync(request, cancellationToken);
    }
}
