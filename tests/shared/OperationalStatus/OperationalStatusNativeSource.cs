using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Folders.Tests.OperationalStatus;

/// <summary>Independent original-source fixture. It never reads the derived store; the actual SDK scanner verifies its bounded pages.</summary>
internal sealed class OperationalStatusNativeSource : IEventStoreGatewayClient
{
    private readonly Dictionary<string, SortedDictionary<long, StreamReadEvent>> _streams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _observedHeads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NativeSourceProof> _proofs = new(StringComparer.Ordinal);
    /// <summary>Scanner.</summary>
    public INativeSourceScanner Scanner => new NativeSourceScanner(this, TimeProvider.System);
    /// <summary>On Read.</summary>
    public Action? OnRead { get; set; }
    /// <summary>Unavailable.</summary>
    public bool Unavailable { get; set; }

    /// <summary>Remember.</summary>
    public void Remember(string tenant, string folder, ProjectionEventDto value, bool force = false)
    {
        var identity = new AggregateIdentity(tenant, "folders", folder);
        lock (_streams)
        {
            if (!_streams.TryGetValue(identity.ActorId, out var stream)) _streams[identity.ActorId] = stream = new();
            // Observed native positions are immutable. Constructing a conflicting delivery must
            // not silently replace the independent source fixture used to validate that delivery.
            if (!force && _observedHeads.TryGetValue(identity.ActorId, out long observed) && value.SequenceNumber <= observed) return;
            stream[value.SequenceNumber] = new(value.SequenceNumber, value.EventTypeName, value.Payload.ToArray(), value.SerializationFormat,
                value.MetadataVersion ?? 1, value.MessageId!, value.CorrelationId, null, value.Timestamp, value.UserId)
                { GlobalPosition = value.GlobalPosition, PayloadVersion = value.StoredPayloadVersion,
                    NativeEventDigest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))) };
            _proofs.Remove(identity.ActorId);
        }
    }

    /// <summary>Read Native Source Scan Page Async.</summary>
    public Task<StreamReadPage> ReadNativeSourceScanPageAsync(StreamReadRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnRead?.Invoke();
        if (Unavailable) throw new HttpRequestException("Fixture source unavailable.");
        var identity = new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId!);
        lock (_streams)
        {
            if (!_streams.TryGetValue(identity.ActorId, out var stream)) throw new HttpRequestException("Fixture source absent.");
            if (!_proofs.TryGetValue(identity.ActorId, out var proof))
            {
                proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
                foreach (var item in stream.Values)
                    proof = NativeSourceProofCodec.ExtendDigest(proof, identity, item.SequenceNumber, item.MessageId, item.NativeEventDigest!);
                _proofs[identity.ActorId] = proof;
            }
            _observedHeads[identity.ActorId] = proof.HeadSequence;
            StreamReadEvent[] events = request.ToSequence == 0 ? [] : stream.Values
                .Where(item => item.SequenceNumber > request.FromSequence && item.SequenceNumber <= request.ToSequence)
                .Take(request.PageSize).ToArray();
            return Task.FromResult(new StreamReadPage(identity.TenantId, identity.Domain, identity.AggregateId, events,
                new(request.FromSequence, request.ToSequence, events.LastOrDefault()?.SequenceNumber, proof.HeadSequence,
                    events.Length, events.Length > 0 && events[^1].SequenceNumber < request.ToSequence, null) { NativeProof = proof }));
        }
    }

    /// <summary>Submit Command Async.</summary>
    public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Submit Query Async.</summary>
    public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Native source fixture.</summary>
    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Read Stream Async.</summary>
    public Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
