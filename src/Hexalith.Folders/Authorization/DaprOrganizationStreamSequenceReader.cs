using System.Globalization;
using System.Net.Http.Headers;

using Microsoft.Extensions.Configuration;

namespace Hexalith.Folders.Authorization;

/// <summary>Reads only the actor's sequence metadata through the local Dapr sidecar.</summary>
public sealed class DaprOrganizationStreamSequenceReader(HttpClient httpClient, IConfiguration? configuration = null)
    : IOrganizationStreamSequenceReader
{
    public async Task<long> GetCurrentSequenceAsync(string tenantId, string organizationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        // Organization ACL commands are already inside this same EventStore actor.
        // Calling it through Dapr here would wait for the actor to release itself.
        if (OrganizationActorSequenceScope.TryGet(tenantId, organizationId, out long currentSequence))
        {
            return currentSequence;
        }

        if (FolderActorSequenceScope.TryGet(tenantId, organizationId, out currentSequence))
        {
            return currentSequence;
        }

        string endpoint = Environment.GetEnvironmentVariable("DAPR_HTTP_ENDPOINT")
            ?? $"http://localhost:{Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500"}";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? baseAddress)
            || baseAddress.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException("The local Dapr HTTP endpoint is unavailable.");
        }

        string actorId = Uri.EscapeDataString($"{tenantId}:folders:{organizationId}");
        Uri requestUri = new(baseAddress,
            $"/v1.0/actors/AggregateActor/{actorId}/method/GetCurrentSequenceAsync");
        using HttpRequestMessage request = new(HttpMethod.Post, requestUri)
        {
            Content = new StringContent("{}"),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        string? apiToken = configuration?["DAPR_API_TOKEN"]
            ?? Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            request.Headers.Add("dapr-api-token", apiToken);
        }
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > 32)
            {
                throw new InvalidOperationException("The organization actor sequence response is oversized.");
            }

            string value = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (value.Length > 32
                || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
                || sequence < 0)
            {
                throw new InvalidOperationException("The organization actor sequence response is malformed.");
            }

            return sequence;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The organization actor sequence request timed out.", ex);
        }
    }
}
