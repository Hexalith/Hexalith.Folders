using System.Reflection;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.Folders.Authorization;

namespace Hexalith.Folders.Server.Tests.Provisioning;

/// <summary>Adapts native actor results to the EventStore stream response shape within the isolated fixture.</summary>
internal sealed class ProvisioningQualificationStreams(ProvisioningQualificationActorHost host)
    : IEventStoreGatewayClient, IOrganizationStreamSequenceReader
{
    // Use the pinned SDK's exact raw-envelope codec. A fixture-defined digest cannot qualify native proof reads.
    private static readonly MethodInfo EventDigest = typeof(EventEnvelope).Assembly
        .GetType("Hexalith.EventStore.Server.Events.SourceProofCodec", throwOnError: true)!
        .GetMethod("ComputeEventHash", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The pinned EventStore source digest method is unavailable.");

    internal bool OmitNativeProof { get; set; }
    internal Func<StreamReadRequest, CancellationToken, Task>? BeforeReply { get; set; }

    public Task<long> GetCurrentSequenceAsync(string tenantId, string aggregateId, CancellationToken cancellationToken = default)
        => host.Actor(tenantId, aggregateId).GetCurrentSequenceAsync().WaitAsync(cancellationToken);

    public async Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.RequireNativeProof)
        {
            throw new InvalidOperationException("The G-2 fixture requires an actor-native source proof.");
        }

        bool metadataOnly = request.FromSequence == 0 && request.ToSequence == 0;
        IAggregateActor actor = host.Actor(request.Tenant, request.AggregateId!);
        SourceNativeReadBatch batch = await actor.ReadNativeSourceBatchAsync(request.FromSequence, request.ToSequence,
            request.PageSize + 1, 2_097_152, metadataOnly).WaitAsync(cancellationToken).ConfigureAwait(true);
        if (batch.Before is not { Status: SourceProofInspectionStatus.Available, Exists: true, Erased: false, Proof: { } proof }
            || (metadataOnly ? batch.After != batch.Before : batch.Range is not { ReasonCode: null }))
        {
            throw new EventStoreGatewayException(503, "An authoritative native prefix is unavailable.");
        }

        EventEnvelope[] source = batch.Range?.Events ?? [];
        StreamReadEvent[] events = source.Take(request.PageSize)
            .Select(item => new StreamReadEvent(item.SequenceNumber, item.EventTypeName, item.Payload, item.SerializationFormat,
                item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId, item.Timestamp, item.UserId)
            {
                PayloadVersion = item.PayloadVersion,
                GlobalPosition = item.GlobalPosition,
                NativeEventDigest = (string)EventDigest.Invoke(null, [item])!,
            })
            .ToArray();
        if (BeforeReply is { } hook)
        {
            await hook(request, cancellationToken).ConfigureAwait(true);
        }

        return new(request.Tenant, request.Domain, request.AggregateId, events,
            new StreamReadMetadata(request.FromSequence, request.ToSequence, events.LastOrDefault()?.SequenceNumber,
                proof.HeadSequence, events.Length, source.Length > request.PageSize, null)
            {
                NativeProof = OmitNativeProof ? null : new(proof.CodecVersion, proof.TenantId, proof.Domain, proof.AggregateId, proof.SourceIncarnation,
                    proof.HeadSequence, proof.HeadMessageId, proof.CumulativeDigest, proof.RetainedFloor, proof.Erased),
            });
    }

    public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request, string? ifNoneMatch = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request, string? ifNoneMatch = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
