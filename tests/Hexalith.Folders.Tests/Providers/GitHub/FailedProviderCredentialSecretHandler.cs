using System.Net;
using System.Net.Http.Headers;

namespace Hexalith.Folders.Tests.Providers.GitHub;

internal sealed class FailedProviderCredentialSecretHandler(string grpcStatus) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Version = HttpVersion.Version20,
            Content = new ByteArrayContent([]),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
        response.TrailingHeaders.Add("grpc-status", grpcStatus);
        response.TrailingHeaders.Add("grpc-message", "synthetic-dependency-failure");
        return Task.FromResult(response);
    }
}
