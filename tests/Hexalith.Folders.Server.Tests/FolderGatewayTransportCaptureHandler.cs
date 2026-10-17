using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.Folders.Server.Tests;

internal sealed class FolderGatewayTransportCaptureHandler : HttpMessageHandler
{
    public Uri? Target { get; private set; }

    public string? Authorization { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Target = request.RequestUri;
        Authorization = request.Headers.Authorization?.ToString();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = JsonContent.Create(new SubmitCommandResponse("correlation-a", MessageId: "message-a")),
        });
    }
}
