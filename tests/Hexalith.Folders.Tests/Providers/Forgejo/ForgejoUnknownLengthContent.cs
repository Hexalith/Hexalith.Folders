using System.Net;

namespace Hexalith.Folders.Tests.Providers.Forgejo;

/// <summary>
/// Streams bytes without declaring a content length for response-boundary tests.
/// </summary>
internal sealed class ForgejoUnknownLengthContent(byte[] bytes) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => stream.WriteAsync(bytes).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
