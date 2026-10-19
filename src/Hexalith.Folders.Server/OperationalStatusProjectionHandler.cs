using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Server;

/// <summary>Named SDK operational projection with side-effect-free complete-prefix rebuild planning.</summary>
public sealed class OperationalStatusProjectionHandler(OperationalStatusProjectionWriter writer,
    IOrganizationStreamSequenceReader sequenceReader)
    : IAsyncDomainSharedProjectionStreamingRebuildHandler
{
    /// <inheritdoc/>
    public string Domain => FoldersServerModule.DomainName;
    /// <inheritdoc/>
    public string ProjectionType => OperationalStatusKeys.ProjectionType;
    /// <inheritdoc/>
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;
    /// <inheritdoc/>
    public async Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId,
        CancellationToken cancellationToken)
    {
        try
        {
            OperationalStatusEvent[] events = Read(request);
            var result = await writer.ApplyAsync(request.TenantId, request.AggregateId, dispatchId,
                events, cancellationToken).ConfigureAwait(false);
            return ReadModelBatchProjectionResultMapper.Map(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationalStatusUnavailableException)
        {
            return DomainProjectionHandlerResult.Indeterminate("operational_store_unavailable");
        }
        catch (Exception failure) when (failure is InvalidOperationException or ArgumentException or System.Text.Json.JsonException or Hexalith.EventStore.Client.Events.EventPayloadEvolutionException)
        {
            return DomainProjectionHandlerResult.Failed("operational_source_invalid");
        }
        catch (Exception)
        {
            return DomainProjectionHandlerResult.Retryable("operational_store_unavailable");
        }
    }
    /// <inheritdoc/>
    public async Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(ProjectionRequest request, string operationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        OperationalStatusEvent[] events = Read(request);
        return new(OperationalStatusKeys.StoreName, await writer.PrepareRebuildAsync(request.TenantId,
            request.AggregateId, events,
            await sequenceReader.GetCurrentSequenceAsync(request.TenantId, request.AggregateId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false));
    }
    /// <inheritdoc/>
    public string RebuildStoreName => OperationalStatusKeys.StoreName;
    /// <inheritdoc/>
    public bool EpochEnabled => true;
    /// <inheritdoc/>
    public string OrdinaryWriterId => OperationalStatusGeneration.WriterId;
    /// <inheritdoc/>
    public SharedProjectionScope CreateScope(string tenantId) => OperationalStatusGeneration.Scope(tenantId);
    /// <inheritdoc/>
    public Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(SharedProjectionDelivery delivery,
        long generation, SharedProjectionEpochCoordinator coordinator, CancellationToken cancellationToken)
        => writer.FoldCatchUpAsync(delivery, generation, cancellationToken);
    /// <inheritdoc/>
    public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
        DomainSharedProjectionRebuildIdentity identity, CancellationToken cancellationToken)
        => Task.FromResult(new DomainSharedProjectionRebuildCandidate("{}"u8));
    /// <inheritdoc/>
    public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate, ProjectionRequest aggregateHistory, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Operational rebuild requires the SDK bounded partition transport.");
    /// <inheritdoc/>
    public Task<DomainProjectionRebuildPlan> FinalizeAsync(DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Operational rebuild requires the SDK bounded streaming transport.");
    /// <inheritdoc/>
    public Task<ReadOnlyMemory<byte>> PreparePartitionAsync(DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Operational owner histories require bounded chunks.");
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadOnlyMemory<byte>>> PreparePartitionChunksAsync(DomainSharedProjectionRebuildIdentity identity,
        ProjectionRequest aggregateHistory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(aggregateHistory);
        if (aggregateHistory.TenantId != identity.TenantId || aggregateHistory.Domain != Domain)
            throw new InvalidOperationException("Operational rebuild partition scope conflicts.");
        var plan = await PrepareRebuildAsync(aggregateHistory, identity.OperationId, cancellationToken).ConfigureAwait(false);
        List<ReadOnlyMemory<byte>> chunks = [];
        List<SharedProjectionMutation> page = [];
        int bytes = 0;
        foreach (ReadModelBatchOperation operation in plan.Operations)
        {
            SharedProjectionMutation mutation = SharedProjectionMutation.FromOperation(operation);
            int size = JsonSerializer.SerializeToUtf8Bytes(mutation).Length;
            if (page.Count > 0 && (page.Count == 16 || bytes + size > 32 * 1024))
            {
                chunks.Add(JsonSerializer.SerializeToUtf8Bytes(page));
                page.Clear();
                bytes = 0;
            }
            page.Add(mutation);
            bytes += size;
        }
        if (page.Count > 0) chunks.Add(JsonSerializer.SerializeToUtf8Bytes(page));
        return chunks;
    }
    /// <inheritdoc/>
    public Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(DomainSharedProjectionRebuildIdentity identity,
        IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Operational partitions require SDK final-operation streaming.");
    /// <inheritdoc/>
    public async IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(
        DomainSharedProjectionRebuildIdentity identity, IDomainSharedProjectionPartitionCursor partitions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        await foreach (var partition in partitions.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            SharedProjectionMutation[] operations = JsonSerializer.Deserialize<SharedProjectionMutation[]>(partition.State.Span)
                ?? throw new InvalidOperationException("Operational rebuild partition is unreadable.");
            foreach (SharedProjectionMutation operation in operations)
            {
                if (operation.Kind != ReadModelBatchOperationKind.Write)
                    throw new InvalidOperationException("Unexpected operational rebuild operation.");
                yield return ReadModelBatchOperation.Write(operation.Key, JsonNode.Parse(operation.CanonicalValue)!.AsObject(),
                    ReadModelBatchConcurrency.LastWrite, operation.TimeToLive);
            }
        }
    }
    private static OperationalStatusEvent[] Read(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Domain != FoldersServerModule.DomainName || request.Events is null || request.Events.Length == 0)
            throw new InvalidOperationException("Invalid operational projection input.");
        return request.Events.Select(OperationalStatusEventReader.ReadEffective).ToArray();
    }
}
