using System.Globalization;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Publishes bounded operational records through SDK journal and generation protocols.</summary>
public sealed class OperationalStatusProjectionWriter
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);
    private readonly SharedProjectionEpochCoordinator _coordinator;

    /// <summary>Creates a writer using the platform's recoverable batch and generation services.</summary>
    public OperationalStatusProjectionWriter(IReadModelStore store, IReadModelBatchStore batches,
        IReadModelPendingBatchRecoveryStore recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        _coordinator = new(store, batches);
    }

    /// <summary>Persists each source delivery before permitting acknowledgment.</summary>
    public async Task<ReadModelBatchResult> ApplyAsync(string tenantId, string folderId, string dispatchId,
        IReadOnlyList<OperationalStatusEvent> events, CancellationToken cancellationToken = default)
    {
        try { return await ApplyCoreAsync(tenantId, folderId, dispatchId, events, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is SharedProjectionWriteUnavailableException or SharedProjectionReadUnavailableException
            or IOException or HttpRequestException or TimeoutException)
        {
            return ReadModelBatchResult.Indeterminate(string.Empty, "operational_store_unavailable");
        }
    }

    private async Task<ReadModelBatchResult> ApplyCoreAsync(string tenantId, string folderId, string dispatchId,
        IReadOnlyList<OperationalStatusEvent> events, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        ArgumentNullException.ThrowIfNull(events);
        bool changed = false;
        SharedProjectionScope scope = OperationalStatusGeneration.Scope(tenantId);
        foreach (OperationalStatusEvent item in events)
        {
            ProjectionEventDto source = item.Source ?? throw new InvalidOperationException("Original operational source metadata is missing.");
            OperationalStatusEvent verified = OperationalStatusEventReader.ReadStored(source);
            if (verified.Digest != item.Digest || verified.MessageId != item.MessageId || verified.Sequence != item.Sequence)
                throw new InvalidOperationException("Operational source identity conflict.");
            if (source.GlobalPosition <= 0) throw new InvalidOperationException("Original operational source position is missing.");
            SharedProjectionLease lease = await _coordinator.RegisterWriterAsync(scope,
                OperationalStatusGeneration.WriterId, cancellationToken).ConfigureAwait(false);
            // Resume retained SDK preparations before reading any fresh fold/CAS evidence.
            await _coordinator.CatchUpAsync(scope, (value, generation, token) => FoldCatchUpAsync(value, generation, token),
                cancellationToken).ConfigureAwait(false);
            // Validate domain metadata before admitting any mutation into the SDK journal.
            SharedProjectionEpochStatus status = await _coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false);
            await PrepareDeliveryAsync(tenantId, folderId, item, status.ActiveGeneration, cancellationToken).ConfigureAwait(false);
            var before = await _coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
                OperationalStatusKeys.Checkpoint(tenantId, folderId), cancellationToken).ConfigureAwait(false);
            if (before.IsAvailable && !before.IsStale && !before.IsDegraded && before.Value?.Sequence >= source.SequenceNumber)
                continue;
            changed = true;
            ProjectionRequest request = new(tenantId, "folders", folderId, [source]);
            SharedProjectionDelivery delivery = new(folderId, source.GlobalPosition,
                JsonSerializer.SerializeToUtf8Bytes(request, WireJson), JsonSerializer.SerializeToUtf8Bytes(source.GlobalPosition, WireJson),
                OwnerSequenceNumber: source.SequenceNumber);
            SharedProjectionJournalResult journal = await _coordinator.JournalAsync(scope, lease, delivery, cancellationToken).ConfigureAwait(false);
            if (journal == SharedProjectionJournalResult.IdentityConflict)
                throw new InvalidOperationException("Operational delivery identity conflict.");
            if (journal is SharedProjectionJournalResult.StaleLease or SharedProjectionJournalResult.Backpressure
                or SharedProjectionJournalResult.DeliveryGap or SharedProjectionJournalResult.RebuildRequired)
                return ReadModelBatchResult.Indeterminate(string.Empty, "operational_delivery_retry");
            await _coordinator.CatchUpAsync(scope,
                (value, generation, token) => FoldCatchUpAsync(value, generation, token), cancellationToken).ConfigureAwait(false);
            SharedProjectionReadResult<OperationalStatusCheckpoint> selected = await _coordinator.ReadAsync<OperationalStatusCheckpoint>(
                scope, OperationalStatusKeys.Checkpoint(tenantId, folderId), cancellationToken).ConfigureAwait(false);
            if (!selected.IsAvailable || selected.IsStale || selected.Value is not { } checkpoint || checkpoint.Sequence < item.Sequence)
                return ReadModelBatchResult.Indeterminate(string.Empty, "operational_delivery_incomplete");
        }
        return changed ? ReadModelBatchResult.Completed(dispatchId) : ReadModelBatchResult.AlreadyCompleted(dispatchId);
    }

    /// <summary>Folds one immutable SDK journal entry into a bounded set of logical domain writes.</summary>
    public async Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(SharedProjectionDelivery delivery,
        long generation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ProjectionRequest request = JsonSerializer.Deserialize<ProjectionRequest>(delivery.CanonicalPayload, WireJson)
            ?? throw new InvalidOperationException("Operational delivery is unreadable.");
        if (request.Domain != "folders" || request.AggregateId != delivery.SourceStream || request.Events.Length != 1)
            throw new InvalidOperationException("Operational delivery scope conflicts.");
        OperationalStatusEvent source = OperationalStatusEventReader.ReadEffective(request.Events[0]);
        return await PrepareDeliveryAsync(request.TenantId, request.AggregateId, source, generation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ReadModelBatchOperation>> PrepareDeliveryAsync(string tenantId, string folderId,
        OperationalStatusEvent source, long generation, CancellationToken cancellationToken)
    {
        SharedProjectionScope scope = OperationalStatusGeneration.Scope(tenantId);
        OperationalStatusCheckpoint? current = (await ReadAsync<OperationalStatusCheckpoint>(
            scope, generation, OperationalStatusKeys.Checkpoint(tenantId, folderId), cancellationToken).ConfigureAwait(false)).Value;
        if (current is not null && !OperationalStatusFold.Valid(current, tenantId, folderId))
            throw new InvalidOperationException("Malformed operational checkpoint; complete source rebuild required.");
        OperationalStatusReceipt? receipt = (await ReadAsync<OperationalStatusReceipt>(scope, generation,
            OperationalStatusKeys.Receipt(tenantId, folderId, source.Sequence), cancellationToken).ConfigureAwait(false)).Value;
        OperationalStatusReceipt? message = (await ReadAsync<OperationalStatusReceipt>(scope, generation,
            OperationalStatusKeys.Message(tenantId, folderId, source.MessageId), cancellationToken).ConfigureAwait(false)).Value;
        if (source.Sequence <= (current?.Sequence ?? 0))
        {
            if (receipt is null || message != receipt || receipt.EventDigest != source.Digest
                || receipt.MessageId != source.MessageId || receipt.Sequence != source.Sequence || !receipt.Valid())
                throw new InvalidOperationException("Operational source identity conflict.");
            // A replay after view loss never acknowledges missing or damaged required views.
            await RequireCurrentViewsAsync(scope, generation, current!, receipt.RequiredViews, cancellationToken).ConfigureAwait(false);
            return [Write(OperationalStatusKeys.Receipt(tenantId, folderId, source.Sequence), receipt)];
        }
        if (receipt is not null || message is not null) throw new InvalidOperationException("Operational message identity is reused.");
        if (source.Event is null && current is not null)
            await RequireCurrentViewsAsync(scope, generation, current, null, cancellationToken).ConfigureAwait(false);
        OperationalStatusFoldResult next = OperationalStatusFold.Apply(tenantId, folderId, current, [source], bounded: true);
        List<ReadModelBatchOperation> operations = [];
        if (next.Lifecycle is not null) operations.Add(Write(OperationalStatusKeys.Lifecycle(tenantId, folderId), next.Lifecycle, view: true));
        foreach ((string workspaceId, OperationalWorkspaceStatus workspace) in next.Workspaces)
            AddView(OperationalStatusKeys.Workspace(tenantId, folderId, workspaceId), workspace, workspace.Sequence,
                workspace.SourceDigest, operations);
        foreach ((string taskId, TaskStatusReadModelSnapshot proposed) in next.Tasks)
        {
            string key = OperationalStatusKeys.Task(tenantId, folderId, taskId);
            TaskStatusReadModelSnapshot task = proposed;
            OperationalStatusViewEvidence? evidence = (await ReadAsync<OperationalStatusViewEvidence>(scope, generation,
                OperationalStatusKeys.Evidence(key), cancellationToken).ConfigureAwait(false)).Value;
            TaskStatusReadModelSnapshot? prior = (await ReadAsync<TaskStatusReadModelSnapshot>(scope, generation, key, cancellationToken).ConfigureAwait(false)).Value;
            if (evidence is not null && (prior is null || evidence.ValueDigest != OperationalStatusIntegrity.Digest(prior)))
                throw new InvalidOperationException("Operational task requires source repair.");
            if (prior?.TerminalState is "ready" or "committed") task = prior;
            long position = long.Parse(task.Freshness.ProjectionWatermark!, CultureInfo.InvariantCulture);
            string prefix = position == source.Sequence ? next.Checkpoint.SourceDigest : evidence!.SourceDigest;
            AddView(key, task, position, prefix, operations);
        }
        OperationalStatusReceipt original = OperationalStatusReceipt.Create(source.Sequence, source.MessageId, source.Digest, next.Checkpoint.SourceDigest,
            next.RequiredViews[source.Sequence]);
        operations.Add(Write(OperationalStatusKeys.Receipt(tenantId, folderId, source.Sequence), original));
        operations.Add(Write(OperationalStatusKeys.Message(tenantId, folderId, source.MessageId), original));
        operations.Add(Write(OperationalStatusKeys.Checkpoint(tenantId, folderId), OperationalStatusCompaction.Compact(next.Checkpoint)));
        return operations;
    }

    private async Task RequireCurrentViewsAsync(SharedProjectionScope scope, long generation,
        OperationalStatusCheckpoint checkpoint, OperationalStatusViewBinding? requiredViews, CancellationToken cancellationToken)
    {
        if (!checkpoint.State.IsCreated) return;
        FolderLifecycleStatusReadModelSnapshot? lifecycle = (await ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope, generation,
            OperationalStatusKeys.Lifecycle(checkpoint.ManagedTenantId, checkpoint.FolderId), cancellationToken).ConfigureAwait(false)).Value;
        if (lifecycle is null || !OperationalStatusFold.MatchesLifecycle(checkpoint, lifecycle))
            throw new OperationalStatusUnavailableException();
        string? originalWorkspace = requiredViews?.WorkspaceId;
        string? originalTask = requiredViews?.TaskId;
        foreach (string key in new[]
        {
            checkpoint.State.WorkspaceId is { } workspace ? OperationalStatusKeys.Workspace(checkpoint.ManagedTenantId, checkpoint.FolderId, workspace) : null,
            checkpoint.State.WorkspaceTaskId is { } task ? OperationalStatusKeys.Task(checkpoint.ManagedTenantId, checkpoint.FolderId, task) : null,
            originalWorkspace is { } historicWorkspace ? OperationalStatusKeys.Workspace(checkpoint.ManagedTenantId, checkpoint.FolderId, historicWorkspace) : null,
            originalTask is { } historicTask ? OperationalStatusKeys.Task(checkpoint.ManagedTenantId, checkpoint.FolderId, historicTask) : null,
        }.OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var value = (await ReadAsync<System.Text.Json.Nodes.JsonObject>(scope, generation, key, cancellationToken).ConfigureAwait(false)).Value;
            var proof = (await ReadAsync<OperationalStatusViewEvidence>(scope, generation,
                OperationalStatusKeys.Evidence(key), cancellationToken).ConfigureAwait(false)).Value;
            string? actual = value is null ? null : key == OperationalStatusKeys.Task(checkpoint.ManagedTenantId, checkpoint.FolderId, checkpoint.State.WorkspaceTaskId ?? "")
                || originalTask is not null && key == OperationalStatusKeys.Task(checkpoint.ManagedTenantId, checkpoint.FolderId, originalTask)
                ? OperationalStatusIntegrity.Digest(value.Deserialize<TaskStatusReadModelSnapshot>(WireJson))
                : OperationalStatusIntegrity.Digest(value.Deserialize<OperationalWorkspaceStatus>(WireJson));
            OperationalStatusReceipt? receipt = proof is null ? null : (await ReadAsync<OperationalStatusReceipt>(scope,
                generation, OperationalStatusKeys.Receipt(checkpoint.ManagedTenantId, checkpoint.FolderId, proof.Sequence),
                cancellationToken).ConfigureAwait(false)).Value;
            if (value is null || proof is null || proof.ValueDigest != actual || receipt is null
                || !receipt.Valid() || proof.Sequence < 1 || proof.Sequence > checkpoint.Sequence || receipt.Sequence != proof.Sequence
                || receipt.SourceDigest != proof.SourceDigest)
                throw new OperationalStatusUnavailableException();
        }

    }

    /// <summary>Builds logical operations from complete authoritative source, preserving valid prefix conflicts.</summary>
    public async Task<IReadOnlyList<ReadModelBatchOperation>> PrepareRebuildAsync(string tenantId, string folderId,
        IReadOnlyList<OperationalStatusEvent> events, long sourceHead, CancellationToken cancellationToken = default)
    {
        OperationalStatusFoldResult next = OperationalStatusFold.Apply(tenantId, folderId, null, events);
        if (sourceHead != next.Checkpoint.Sequence) throw new InvalidOperationException("Incomplete operational rebuild source.");
        SharedProjectionScope scope = OperationalStatusGeneration.Scope(tenantId);
        SharedProjectionEpochStatus? status = null;
        try { status = await _coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false); }
        catch (InvalidOperationException exception) when (exception.Message == "The shared projection has no epoch state.") { }
        OperationalStatusCheckpoint? current = null;
        try
        {
            current = (await ReadAsync<OperationalStatusCheckpoint>(scope, status?.ActiveGeneration ?? 0,
                OperationalStatusKeys.Checkpoint(tenantId, folderId), cancellationToken).ConfigureAwait(false)).Value;
        }
        catch (SharedProjectionReadUnavailableException exception) when (exception.InnerException is InvalidOperationException
        { Message: "A selected copy-on-write value is missing." or "An inherited copy-on-write value is missing." })
        { }
        catch (JsonException) { } // Complete source can replace unreadable derived checkpoint bytes.
        string retainedPrefix = OperationalStatusKeys.Digest(tenantId, folderId);
        foreach (OperationalStatusEvent item in events.DistinctBy(static item => item.Sequence).TakeWhile(item => item.Sequence <= current?.Sequence))
            retainedPrefix = OperationalStatusKeys.Digest(retainedPrefix, item.Digest);
        // Corrupt derived checkpoints have no authority. Only valid source anchors constrain repair.
        if (current is not null && OperationalStatusFold.Valid(current, tenantId, folderId)
            && (current.Sequence > sourceHead || !next.Checkpoint.EventDigests.TryGetValue(current.Sequence, out string? digest)
                || current.EventDigests[current.Sequence] != digest || current.SourceDigest != retainedPrefix))
            throw new InvalidOperationException("Operational rebuild contradicts a valid retained source anchor.");
        List<ReadModelBatchOperation> operations = [];
        if (next.Lifecycle is not null) operations.Add(Write(OperationalStatusKeys.Lifecycle(tenantId, folderId), next.Lifecycle, view: true));
        foreach ((string id, OperationalWorkspaceStatus value) in next.Workspaces.OrderBy(static item => item.Key, StringComparer.Ordinal))
            AddView(OperationalStatusKeys.Workspace(tenantId, folderId, id), value, value.Sequence, value.SourceDigest, operations);
        Dictionary<long, string> prefixes = [];
        string prefix = OperationalStatusKeys.Digest(tenantId, folderId);
        foreach (OperationalStatusEvent item in events.DistinctBy(static item => item.Sequence))
        {
            prefix = OperationalStatusKeys.Digest(prefix, item.Digest);
            prefixes[item.Sequence] = prefix;
            OperationalStatusReceipt receipt = OperationalStatusReceipt.Create(item.Sequence, item.MessageId, item.Digest, prefix, next.RequiredViews[item.Sequence]);
            operations.Add(Write(OperationalStatusKeys.Receipt(tenantId, folderId, item.Sequence), receipt));
            operations.Add(Write(OperationalStatusKeys.Message(tenantId, folderId, item.MessageId), receipt));
        }
        foreach ((string id, TaskStatusReadModelSnapshot task) in next.Tasks.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            long position = long.Parse(task.Freshness.ProjectionWatermark!, CultureInfo.InvariantCulture);
            AddView(OperationalStatusKeys.Task(tenantId, folderId, id), task, position, prefixes[position], operations);
        }
        operations.Add(Write(OperationalStatusKeys.Checkpoint(tenantId, folderId), OperationalStatusCompaction.Compact(next.Checkpoint)));
        return operations;
    }

    private Task<ReadModelEntry<T>> ReadAsync<T>(SharedProjectionScope scope, long generation, string key,
        CancellationToken cancellationToken) where T : class
        => _coordinator.ReadGenerationAsync<T>(scope, generation, key, cancellationToken);

    private static ReadModelBatchOperation Write<T>(string key, T value, bool view = false) where T : class
        => ReadModelBatchOperation.Write(key, value, ReadModelBatchConcurrency.LastWrite,
            view ? OperationalStatusKeys.Retention : null);

    private static void AddView<T>(string key, T value, long position, string digest,
        List<ReadModelBatchOperation> operations) where T : class
    {
        operations.Add(Write(key, value, view: true));
        operations.Add(Write(OperationalStatusKeys.Evidence(key),
            new OperationalStatusViewEvidence(position, digest, OperationalStatusIntegrity.Digest(value))));
    }

}
