using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.Folders.Projections.OperationalStatus;

namespace Hexalith.Folders.Server;

/// <summary>Named SDK operational projection with side-effect-free complete-prefix rebuild planning.</summary>
public sealed class OperationalStatusProjectionHandler(OperationalStatusProjectionWriter writer)
    : IAsyncDomainSharedProjectionStreamingRebuildHandler
{
    private static readonly DiagnosticListener _foldDiagnostics = new("Hexalith.Folders.OperationalStatus");

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
            cancellationToken.ThrowIfCancellationRequested();
            if (OrganizationAclOperationalSource.IsCompletePrefix(request))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return DomainProjectionHandlerResult.Completed();
            }
            OperationalStatusEvent[] events = Read(request, cancellationToken);
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
        cancellationToken.ThrowIfCancellationRequested();
        if (OrganizationAclOperationalSource.IsCompletePrefix(request))
            throw new InvalidOperationException("Organization ACL source has no Folder operations; use bounded shared partition transport.");
        OperationalStatusEvent[] events = Read(request, cancellationToken);
        // The SDK verifies original global positions against the sealed capture before invoking us.
        // Domain folding verifies the contiguous 1..N owner prefix, independently of later appends.
        long capturedSequence = events.LongLength;
        return new(OperationalStatusKeys.StoreName, await writer.PrepareRebuildAsync(request.TenantId,
            request.AggregateId, events, capturedSequence, cancellationToken).ConfigureAwait(false));
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
    public async Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(SharedProjectionDelivery delivery,
        long generation, SharedProjectionEpochCoordinator coordinator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (_foldDiagnostics.IsEnabled("FoldCatchUp"))
            _foldDiagnostics.Write("FoldCatchUp", new KeyValuePair<string, long>(delivery.SourceStream, delivery.EnvelopePosition));
        ProjectionRequest request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Operational delivery is unreadable.");
        if (request.Domain != "folders" || request.AggregateId != delivery.SourceStream || request.Events.Length != 1)
            throw new InvalidOperationException("Operational delivery scope conflicts.");
        if (OrganizationAclOperationalSource.IsCompletePrefix(request))
        {
            if (request.Events[0].GlobalPosition != delivery.EnvelopePosition || request.Events[0].SequenceNumber != delivery.OwnerSequenceNumber)
                throw new InvalidOperationException("Organization ACL delivery position differs from its source.");
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }
        return await writer.FoldCatchUpAsync(delivery, generation, cancellationToken).ConfigureAwait(false);
    }
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
        if (OrganizationAclOperationalSource.IsCompletePrefix(aggregateHistory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [JsonSerializer.SerializeToUtf8Bytes(Array.Empty<SharedProjectionMutation>())];
        }
        var plan = await PrepareRebuildAsync(aggregateHistory, identity.OperationId, cancellationToken).ConfigureAwait(false);
        List<ReadOnlyMemory<byte>> chunks = [];
        List<SharedProjectionMutation> page = [];
        int bytes = 0;
        foreach (ReadModelBatchOperation operation in plan.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SharedProjectionMutation mutation = SharedProjectionMutation.FromOperation(operation);
            int size = JsonSerializer.SerializeToUtf8Bytes(mutation).Length;
            cancellationToken.ThrowIfCancellationRequested();
            if (page.Count > 0 && (page.Count == 16 || bytes + size > 32 * 1024))
            {
                chunks.Add(JsonSerializer.SerializeToUtf8Bytes(page));
                page.Clear();
                bytes = 0;
            }
            page.Add(mutation);
            bytes += size;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (page.Count > 0) chunks.Add(JsonSerializer.SerializeToUtf8Bytes(page));
        cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            SharedProjectionMutation[] operations = JsonSerializer.Deserialize<SharedProjectionMutation[]>(partition.State.Span)
                ?? throw new InvalidOperationException("Operational rebuild partition is unreadable.");
            foreach (SharedProjectionMutation operation in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operation.Kind != ReadModelBatchOperationKind.Write)
                    throw new InvalidOperationException("Unexpected operational rebuild operation.");
                JsonObject value = JsonNode.Parse(operation.CanonicalValue)!.AsObject();
                if (operation.ExpiresAtUtc is { } deadline)
                {
                    if (operation.TimeToLive is not null)
                        throw new InvalidOperationException("Operational rebuild retention modes cannot be combined.");
                    yield return ReadModelBatchOperation.WriteExpiring(operation.Key, value,
                        ReadModelBatchConcurrency.LastWrite, deadline);
                }
                else
                {
                    yield return ReadModelBatchOperation.Write(operation.Key, value,
                        ReadModelBatchConcurrency.LastWrite, operation.TimeToLive);
                }
            }
        }
    }
    private static OperationalStatusEvent[] Read(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Domain != FoldersServerModule.DomainName || request.Events is null || request.Events.Length == 0)
            throw new InvalidOperationException("Invalid operational projection input.");
        return request.Events.Select(item =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return OperationalStatusEventReader.ReadEffective(item);
        }).ToArray();
    }
}
