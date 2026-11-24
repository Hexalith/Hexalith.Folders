using System.Net;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class StalledGitHubResponseContent(CancellationToken testDeadline) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(testDeadline, cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, linked.Token).ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
