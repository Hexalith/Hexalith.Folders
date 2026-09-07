using System.Net;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

/// <summary>
/// Throws a selected exception while an HTTP response body is read.
/// </summary>
/// <param name="exception">The deterministic response-read exception.</param>
internal sealed class ForgejoThrowingHttpContent(Exception exception) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => Task.FromException(exception);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
