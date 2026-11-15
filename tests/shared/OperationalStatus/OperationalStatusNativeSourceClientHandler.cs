using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.Folders.Tests.OperationalStatus;

/// <summary>Enrols original test source envelopes independently of derived rows before publishing their callbacks.</summary>
/// <param name="source">The same explicit source instance supplied to the test host's actual native scanner.</param>
internal sealed class OperationalStatusNativeSourceClientHandler(OperationalStatusNativeSource source)
    : DelegatingHandler(new HttpClientHandler())
{
    /// <summary>Preserves the original request and callback authentication while supplying its original-source fixture.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/folders/events" && request.Content is not null)
        {
            EventStoreDomainEventEnvelope? envelope = null;
            try
            {
                envelope = await request.Content.ReadFromJsonAsync<EventStoreDomainEventEnvelope>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                // Malformed requests still reach the unchanged endpoint and its negative assertions.
            }
            if (envelope is { Domain: "folders", Payload: not null, SequenceNumber: > 0 }
                && !string.IsNullOrWhiteSpace(envelope.TenantId) && !string.IsNullOrWhiteSpace(envelope.AggregateId)
                && !string.IsNullOrWhiteSpace(envelope.MessageId) && !string.IsNullOrWhiteSpace(envelope.EventTypeName))
            {
                try
                {
                    source.Remember(envelope.TenantId, envelope.AggregateId,
                        new ProjectionEventDto(envelope.EventTypeName, envelope.Payload, envelope.SerializationFormat,
                            envelope.SequenceNumber, envelope.Timestamp, envelope.CorrelationId, envelope.MessageId, envelope.UserId,
                            envelope.GlobalPosition ?? 0)
                        { MetadataVersion = envelope.MetadataVersion, StoredPayloadVersion = envelope.PayloadVersion });
                }
                catch (ArgumentException)
                {
                    // Invalid source identities remain requests to the unchanged endpoint's validation.
                }
            }
        }
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
