using System.Collections.Concurrent;
using System.Net;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Counts separately owned test transports while real SDK resilience and invocation guards execute.</summary>
/// <param name="calls">The shared per-target observation counts.</param>
internal sealed class HostDefaultsInvocationHandler(ConcurrentDictionary<string, int> calls) : HttpMessageHandler
{
    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string target = request.RequestUri!.AbsolutePath.Split('/')[3];
        calls.AddOrUpdate(target, 1, static (_, count) => count + 1);
        return Task.FromResult(new HttpResponseMessage(target == "target-a" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK));
    }
}
