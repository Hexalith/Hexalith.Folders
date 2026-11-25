using System.Globalization;
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.Folders.Projections.TenantAccess;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Publishes bounded operational records through SDK journal and generation protocols.</summary>
public sealed class OperationalStatusProjectionWriter
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);
    private readonly SharedProjectionEpochCoordinator _coordinator;
    private readonly IUtcClock _clock;
    private readonly INativeSourceScanner? _scanner;

    /// <summary>Creates a writer using the platform's recoverable batch and generation services.</summary>
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
    public OperationalStatusProjectionWriter(IReadModelStore store, IReadModelBatchStore batches,
        IReadModelPendingBatchRecoveryStore recovery, IUtcClock? clock = null)
        : this(store, batches, recovery, clock, null) { }

    /// <summary>Creates a writer with independently enrolled native identity authority.</summary>
    public OperationalStatusProjectionWriter(IReadModelStore store, IReadModelBatchStore batches,
        IReadModelPendingBatchRecoveryStore recovery, IUtcClock? clock = null, INativeSourceScanner? scanner = null)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        _clock = clock ?? new SystemUtcClock();
        _scanner = scanner;
        _coordinator = new(store, batches, new OperationalStatusTimeProvider(_clock));
    }

    /// <summary>Uses the actual registered SDK coordinator and independently enrolled native scanner.</summary>
    internal OperationalStatusProjectionWriter(SharedProjectionEpochCoordinator coordinator, IUtcClock? clock, INativeSourceScanner scanner)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _clock = clock ?? new SystemUtcClock();
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
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
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionEventDto source = item.Source ?? throw new InvalidOperationException("Original operational source metadata is missing.");
            OperationalStatusEvent verified = OperationalStatusEventReader.ReadStored(source);
            if (verified.Event is null
                && FoldersRejectionSource.ReadOwner(source.Payload, tenantId, folderId, source.CorrelationId) != FoldersSourceOwnerKind.Folder)
                throw new InvalidOperationException("Non-Folder rejection targets a folder operational stream.");
            if (verified.Digest != item.Digest || verified.MessageId != item.MessageId || verified.Sequence != item.Sequence)
                throw new InvalidOperationException("Operational source identity conflict.");
            if (source.GlobalPosition <= 0) throw new InvalidOperationException("Original operational source position is missing.");
            if (Expired(SourceTime(item, item.Timestamp)))
            {
                try
                {
                    var retained = await _coordinator.ReadAsync<OperationalStatusCheckpoint>(scope,
                        OperationalStatusKeys.Checkpoint(tenantId, folderId), cancellationToken).ConfigureAwait(false);
                    if (retained.IsAvailable && !retained.IsStale && !retained.IsDegraded
                        && retained.Value is { } anchor && OperationalStatusFold.Valid(anchor, tenantId, folderId)
                        && anchor.Sequence >= source.SequenceNumber)
                    {
                        RequireTailIdentity(anchor, item);
                        await RequireNativeIdentityAsync(tenantId, folderId, item, anchor, cancellationToken).ConfigureAwait(false);
                        continue; // C3-expired replay must not refresh rows, receipts, or SDK writer metadata.
                    }
                }
                catch (InvalidOperationException exception) when (exception.Message == "The shared projection has no epoch state.") { }
            }
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
            RequireTailIdentity(current!, source);
            if (Expired(SourceTime(source, source.Timestamp)))
            {
                await RequireNativeIdentityAsync(tenantId, folderId, source, current, cancellationToken).ConfigureAwait(false);
                return [];
            }
            if (receipt is null || message != receipt || receipt.EventDigest != source.Digest
                || receipt.MessageId != source.MessageId || receipt.Sequence != source.Sequence || !receipt.Valid())
                throw new InvalidOperationException("Operational source identity conflict.");
            // A replay after view loss never acknowledges missing or damaged required views.
            await RequireCurrentViewsAsync(scope, generation, current!, receipt.RequiredViews, cancellationToken).ConfigureAwait(false);
            return [];
        }
        if (current is { SchemaVersion: < 4 })
            throw new OperationalStatusUnavailableException(); // Old compacted anchors have no complete retained closure inventory; rebuild from source.
        if (receipt is not null || message is not null) throw new InvalidOperationException("Operational message identity is reused.");
        await RequireNativeIdentityAsync(tenantId, folderId, source, current, cancellationToken).ConfigureAwait(false);
        // The remote scan can outlive a concurrent writer's lawful completion. Observe the selected
        // checkpoint again before combining it with current views; do not plan from the old anchor.
        OperationalStatusCheckpoint? confirmed = (await ReadAsync<OperationalStatusCheckpoint>(scope, generation,
            OperationalStatusKeys.Checkpoint(tenantId, folderId), cancellationToken).ConfigureAwait(false)).Value;
        if (confirmed?.IntegrityDigest != current?.IntegrityDigest)
        {
            if (confirmed is null || !OperationalStatusFold.Valid(confirmed, tenantId, folderId)
                || source.Sequence > confirmed.Sequence) throw new OperationalStatusUnavailableException();
            RequireTailIdentity(confirmed, source);
            if (Expired(SourceTime(source, source.Timestamp))) return [];
            receipt = (await ReadAsync<OperationalStatusReceipt>(scope, generation,
                OperationalStatusKeys.Receipt(tenantId, folderId, source.Sequence), cancellationToken).ConfigureAwait(false)).Value;
            message = (await ReadAsync<OperationalStatusReceipt>(scope, generation,
                OperationalStatusKeys.Message(tenantId, folderId, source.MessageId), cancellationToken).ConfigureAwait(false)).Value;
            if (receipt is null || message != receipt || receipt.EventDigest != source.Digest
                || receipt.MessageId != source.MessageId || receipt.Sequence != source.Sequence || !receipt.Valid())
                throw new InvalidOperationException("Operational source identity conflict.");
            await RequireCurrentViewsAsync(scope, generation, confirmed, receipt.RequiredViews, cancellationToken).ConfigureAwait(false);
            return [];
        }
        if (source.Event is null && current is not null)
            await RequireCurrentViewsAsync(scope, generation, current, null, cancellationToken).ConfigureAwait(false);
        OperationalStatusFoldResult next = OperationalStatusFold.Apply(tenantId, folderId, current, [source], bounded: true);
        Dictionary<string, string> taskEvidence = new(next.Checkpoint.TaskEvidence, StringComparer.Ordinal);
        List<ReadModelBatchOperation> operations = [];
        if (next.Lifecycle is not null) AddView(OperationalStatusKeys.Lifecycle(tenantId, folderId),
            next.Lifecycle, next.Checkpoint.Sequence, next.Checkpoint.SourceDigest, next.Lifecycle.Freshness.ObservedAt, operations);
        foreach ((string workspaceId, OperationalWorkspaceStatus workspace) in next.Workspaces)
            AddView(OperationalStatusKeys.Workspace(tenantId, folderId, workspaceId), workspace, workspace.Sequence,
                workspace.SourceDigest, workspace.Workspace.Freshness.ObservedAt, operations);
        foreach ((string taskId, TaskStatusReadModelSnapshot proposed) in next.Tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = OperationalStatusKeys.Task(tenantId, folderId, taskId);
            TaskStatusReadModelSnapshot task = proposed;
            string completionKey = OperationalStatusKeys.TaskCompletion(tenantId, folderId, taskId);
            OperationalStatusTaskCompletion? completion = (await ReadAsync<OperationalStatusTaskCompletion>(scope, generation,
                completionKey, cancellationToken).ConfigureAwait(false)).Value;
            if (completion is not null && !completion.Valid(tenantId, folderId, taskId, next.Checkpoint.Sequence))
                throw new OperationalStatusUnavailableException();
            var closed = next.Checkpoint.State.TaskClosures.Values.Where(value => value.TaskId == taskId)
                .OrderBy(value => next.Checkpoint.TaskClosedSequences[Hexalith.Folders.Aggregates.Folder.FolderState.TaskBindingKey(value.WorkspaceId, taskId)])
                .FirstOrDefault();
            if (closed is not null)
            {
                long closedSequence = next.Checkpoint.TaskClosedSequences[Hexalith.Folders.Aggregates.Folder.FolderState.TaskBindingKey(closed.WorkspaceId, taskId)];
                if (completion is null || closedSequence < completion.Sequence)
                {
                    completion = OperationalStatusTaskCompletion.Create(tenantId, folderId, closed, closedSequence, next.Checkpoint.SourceDigest);
                    operations.Add(Write(completionKey, completion));
                }
            }
            if (completion is not null) task = completion.Restore(task);
            taskEvidence[taskId] = OperationalStatusIntegrity.Digest(task);
            if (Expired(task.Freshness.ObservedAt)) continue;
            OperationalStatusViewEvidence? evidence = (await ReadAsync<OperationalStatusViewEvidence>(scope, generation,
                OperationalStatusKeys.Evidence(key), cancellationToken).ConfigureAwait(false)).Value;
            TaskStatusReadModelSnapshot? prior = (await ReadAsync<TaskStatusReadModelSnapshot>(scope, generation, key, cancellationToken).ConfigureAwait(false)).Value;
            if (evidence is not null && (prior is null || evidence.ValueDigest != OperationalStatusIntegrity.Digest(prior)))
                throw new InvalidOperationException("Operational task requires source repair.");
            if (prior?.TerminalState is "ready" or "committed" && evidence is not null)
            {
                var applicability = (await ReadAsync<OperationalStatusViewApplicability>(scope, generation,
                    OperationalStatusKeys.Applicability(key), cancellationToken).ConfigureAwait(false)).Value;
                var priorReceipt = (await ReadAsync<OperationalStatusReceipt>(scope, generation,
                    OperationalStatusKeys.Receipt(tenantId, folderId, evidence.Sequence), cancellationToken).ConfigureAwait(false)).Value;
                if (applicability is null || !applicability.Matches(key, next.Checkpoint.Sequence, evidence)
                    || priorReceipt is null || !priorReceipt.Valid() || priorReceipt.Sequence != evidence.Sequence
                    || priorReceipt.SourceDigest != evidence.SourceDigest || completion is null
                    || OperationalStatusIntegrity.Digest(prior) != OperationalStatusIntegrity.Digest(task)
                    || current?.State.WorkspaceTaskId == taskId && current.SchemaVersion >= 4
                        && (!current.TaskEvidence.TryGetValue(taskId, out string? currentDigest) || currentDigest != evidence.ValueDigest))
                    throw new OperationalStatusUnavailableException();
                task = prior;
                taskEvidence[taskId] = OperationalStatusIntegrity.Digest(task);
            }
            else if (prior?.TerminalState is "ready" or "committed" && completion is null)
                throw new OperationalStatusUnavailableException();
            long position = long.Parse(task.Freshness.ProjectionWatermark!, CultureInfo.InvariantCulture);
            string prefix = completion?.SourceDigest ?? (position == source.Sequence ? next.Checkpoint.SourceDigest : evidence!.SourceDigest);
            AddView(key, task, position, prefix, task.Freshness.ObservedAt, operations);
        }
        OperationalStatusReceipt original = OperationalStatusReceipt.Create(source.Sequence, source.MessageId, source.Digest, next.Checkpoint.SourceDigest,
            next.RequiredViews[source.Sequence]);
        AddRetained(OperationalStatusKeys.Receipt(tenantId, folderId, source.Sequence), original, SourceTime(source, next.Checkpoint.ObservedAt), operations);
        AddRetained(OperationalStatusKeys.Message(tenantId, folderId, source.MessageId), original, SourceTime(source, next.Checkpoint.ObservedAt), operations);
        operations.Add(Write(OperationalStatusKeys.Checkpoint(tenantId, folderId),
            OperationalStatusCompaction.Compact(next.Checkpoint with { TaskEvidence = taskEvidence })));
        return operations;
    }

    private async Task RequireNativeIdentityAsync(string tenantId, string folderId, OperationalStatusEvent source,
        OperationalStatusCheckpoint? current, CancellationToken cancellationToken)
    {
        if (_scanner is null) throw new OperationalStatusUnavailableException();
        var identity = new AggregateIdentity(tenantId, "folders", folderId);
        NativeSourceScanResult<OperationalStatusNativeSourceFacts> native = await _scanner.ScanAsync(identity,
            () => new OperationalStatusNativeSourceFold(tenantId, folderId, candidate: source, checkpointSequence: current?.Sequence), cancellationToken).ConfigureAwait(false);
        if (native.Observation is null || native.Value is not { } facts || facts.Sequence != native.Observation.Proof.HeadSequence)
            throw new OperationalStatusUnavailableException();
        if (current is not null && facts.CheckpointSourceDigest != current.SourceDigest)
            throw new InvalidOperationException("Operational checkpoint contradicts the current native prefix.");
        if (facts.MessageReused || facts.CandidateConflict) throw new InvalidOperationException("Operational message identity conflicts with its native source.");
        if (!facts.CandidateFound) throw new OperationalStatusUnavailableException();
    }

    private async Task RequireCurrentViewsAsync(SharedProjectionScope scope, long generation,
        OperationalStatusCheckpoint checkpoint, OperationalStatusViewBinding? requiredViews, CancellationToken cancellationToken)
    {
        if (!checkpoint.State.IsCreated) return;
        FolderLifecycleStatusReadModelSnapshot? lifecycle = (await ReadAsync<FolderLifecycleStatusReadModelSnapshot>(scope, generation,
            OperationalStatusKeys.Lifecycle(checkpoint.ManagedTenantId, checkpoint.FolderId), cancellationToken).ConfigureAwait(false)).Value;
        if (lifecycle is null && !Expired(checkpoint.ObservedAt)
            || lifecycle is not null && !OperationalStatusFold.MatchesLifecycle(checkpoint, lifecycle))
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
            if (value is null && proof is null) continue; // Paired C3 expiry is safe absence.
            var applicability = (await ReadAsync<OperationalStatusViewApplicability>(scope, generation,
                OperationalStatusKeys.Applicability(key), cancellationToken).ConfigureAwait(false)).Value;
            bool isTask = key == OperationalStatusKeys.Task(checkpoint.ManagedTenantId, checkpoint.FolderId, checkpoint.State.WorkspaceTaskId ?? "")
                || originalTask is not null && key == OperationalStatusKeys.Task(checkpoint.ManagedTenantId, checkpoint.FolderId, originalTask);
            TaskStatusReadModelSnapshot? taskValue = isTask ? value?.Deserialize<TaskStatusReadModelSnapshot>(WireJson) : null;
            OperationalWorkspaceStatus? workspaceValue = isTask ? null : value?.Deserialize<OperationalWorkspaceStatus>(WireJson);
            string? actual = value is null ? null : isTask ? OperationalStatusIntegrity.Digest(taskValue)
                : OperationalStatusIntegrity.Digest(workspaceValue);
            OperationalStatusReceipt? receipt = proof is null ? null : (await ReadAsync<OperationalStatusReceipt>(scope,
                generation, OperationalStatusKeys.Receipt(checkpoint.ManagedTenantId, checkpoint.FolderId, proof.Sequence),
                cancellationToken).ConfigureAwait(false)).Value;
            if (value is null || proof is null || proof.ValueDigest != actual || receipt is null
                || applicability is null || !applicability.Matches(key, checkpoint.Sequence, proof)
                || !receipt.Valid() || proof.Sequence < 1 || proof.Sequence > checkpoint.Sequence || receipt.Sequence != proof.Sequence
                || receipt.SourceDigest != proof.SourceDigest)
                throw new OperationalStatusUnavailableException();
            if (checkpoint.SchemaVersion >= 4
                && (key == OperationalStatusKeys.Task(checkpoint.ManagedTenantId, checkpoint.FolderId, checkpoint.State.WorkspaceTaskId ?? "")
                    ? !checkpoint.TaskEvidence.TryGetValue(checkpoint.State.WorkspaceTaskId ?? "", out string? taskDigest) || taskDigest != actual
                    : key == OperationalStatusKeys.Workspace(checkpoint.ManagedTenantId, checkpoint.FolderId, checkpoint.State.WorkspaceId ?? "")
                        && (!checkpoint.WorkspaceEvidence.TryGetValue(checkpoint.State.WorkspaceId ?? "", out string? workspaceDigest) || workspaceDigest != actual)))
                throw new OperationalStatusUnavailableException();
            bool historical = taskValue is not null ? taskValue.TaskId != checkpoint.State.WorkspaceTaskId
                : workspaceValue is not null && workspaceValue.Workspace.WorkspaceId != checkpoint.State.WorkspaceId;
            if (historical)
            {
                if (_scanner is null) throw new OperationalStatusUnavailableException();
                var source = await _scanner.ScanAsync(new AggregateIdentity(checkpoint.ManagedTenantId, "folders", checkpoint.FolderId),
                    () => new OperationalStatusNativeSourceFold(checkpoint.ManagedTenantId, checkpoint.FolderId, key,
                        checkpointSequence: checkpoint.Sequence, viewCutSequence: checkpoint.Sequence), cancellationToken).ConfigureAwait(false);
                if (source.Observation is null || source.Value is not { } facts
                    || facts.CheckpointSourceDigest != checkpoint.SourceDigest || facts.ValueDigest != actual
                    || facts.ViewSequence != proof.Sequence || facts.ViewSourceDigest != proof.SourceDigest)
                    throw new OperationalStatusUnavailableException();
                string checkpointKey = OperationalStatusKeys.Checkpoint(checkpoint.ManagedTenantId, checkpoint.FolderId);
                string evidenceKey = OperationalStatusKeys.Evidence(key);
                string applicabilityKey = OperationalStatusKeys.Applicability(key);
                // Physical generation reads also run during SDK catch-up, when current public
                // ReadMany correctly reports stale. Fence their generation before and after.
                if ((await _coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false)).ActiveGeneration != generation)
                    throw new OperationalStatusUnavailableException();
                var afterCheckpoint = (await ReadAsync<OperationalStatusCheckpoint>(scope, generation, checkpointKey, cancellationToken).ConfigureAwait(false)).Value;
                var afterValue = (await ReadAsync<System.Text.Json.Nodes.JsonObject>(scope, generation, key, cancellationToken).ConfigureAwait(false)).Value;
                var afterEvidence = (await ReadAsync<OperationalStatusViewEvidence>(scope, generation, evidenceKey, cancellationToken).ConfigureAwait(false)).Value;
                var afterApplicability = (await ReadAsync<OperationalStatusViewApplicability>(scope, generation, applicabilityKey, cancellationToken).ConfigureAwait(false)).Value;
                if ((await _coordinator.GetStatusAsync(scope, cancellationToken).ConfigureAwait(false)).ActiveGeneration != generation
                    || afterCheckpoint is null || !OperationalStatusFold.Valid(afterCheckpoint, checkpoint.ManagedTenantId, checkpoint.FolderId)
                    || afterCheckpoint?.IntegrityDigest != checkpoint.IntegrityDigest
                    || !System.Text.Json.Nodes.JsonNode.DeepEquals(value, afterValue)
                    || afterEvidence != proof || afterApplicability != applicability)
                    throw new OperationalStatusUnavailableException();
            }
            if (taskValue is not null)
            {
                var completion = (await ReadAsync<OperationalStatusTaskCompletion>(scope, generation,
                    OperationalStatusKeys.TaskCompletion(checkpoint.ManagedTenantId, checkpoint.FolderId, taskValue.TaskId),
                    cancellationToken).ConfigureAwait(false)).Value;
                if (completion is not null
                    ? !completion.Valid(checkpoint.ManagedTenantId, checkpoint.FolderId, taskValue.TaskId, checkpoint.Sequence)
                        || OperationalStatusIntegrity.Digest(completion.Restore(taskValue)) != actual
                    : taskValue.TerminalState is "ready" or "committed")
                    throw new OperationalStatusUnavailableException();
            }
            else
            {
                if (workspaceValue?.Workspace.AcceptedCommandState?.TaskId is { } taskId)
                {
                    var completion = (await ReadAsync<OperationalStatusTaskCompletion>(scope, generation,
                        OperationalStatusKeys.TaskCompletion(checkpoint.ManagedTenantId, checkpoint.FolderId, taskId),
                        cancellationToken).ConfigureAwait(false)).Value;
                    if (completion is not null && (!completion.Valid(checkpoint.ManagedTenantId, checkpoint.FolderId, taskId, checkpoint.Sequence)
                        || completion.Closure.WorkspaceId == workspaceValue.Workspace.WorkspaceId && workspaceValue.Sequence < completion.Sequence))
                        throw new OperationalStatusUnavailableException();
                }
            }
        }

    }

    private static void RequireTailIdentity(OperationalStatusCheckpoint checkpoint, OperationalStatusEvent source)
    {
        if (source.Sequence == checkpoint.Sequence && (checkpoint.MessageIds[source.Sequence] != source.MessageId
            || checkpoint.EventDigests[source.Sequence] != source.Digest))
            throw new InvalidOperationException("Operational source identity conflict.");
    }

    /// <summary>Builds logical operations from complete authoritative source, preserving valid prefix conflicts.</summary>
    public async Task<IReadOnlyList<ReadModelBatchOperation>> PrepareRebuildAsync(string tenantId, string folderId,
        IReadOnlyList<OperationalStatusEvent> events, long sourceHead, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        IEnumerable<OperationalStatusEvent> source = ObserveCancellation(events, cancellationToken);
        OperationalStatusFoldResult next = OperationalStatusFold.Apply(tenantId, folderId, null, source);
        cancellationToken.ThrowIfCancellationRequested();
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
        foreach (OperationalStatusEvent item in source.DistinctBy(static item => item.Sequence).TakeWhile(item => item.Sequence <= current?.Sequence))
            retainedPrefix = OperationalStatusKeys.Digest(retainedPrefix, item.Digest);
        // Corrupt derived checkpoints have no authority. Only valid source anchors constrain repair.
        if (current is not null && OperationalStatusFold.Valid(current, tenantId, folderId)
            && (current.Sequence > sourceHead || !next.Checkpoint.EventDigests.TryGetValue(current.Sequence, out string? digest)
                || current.EventDigests[current.Sequence] != digest || current.SourceDigest != retainedPrefix))
            throw new InvalidOperationException("Operational rebuild contradicts a valid retained source anchor.");
        List<ReadModelBatchOperation> operations = [];
        if (next.Lifecycle is not null) AddView(OperationalStatusKeys.Lifecycle(tenantId, folderId),
            next.Lifecycle, next.Checkpoint.Sequence, next.Checkpoint.SourceDigest, next.Lifecycle.Freshness.ObservedAt, operations);
        foreach ((string id, OperationalWorkspaceStatus value) in next.Workspaces.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddView(OperationalStatusKeys.Workspace(tenantId, folderId, id), value, value.Sequence, value.SourceDigest,
                value.Workspace.Freshness.ObservedAt, operations);
        }
        Dictionary<long, string> prefixes = [];
        string prefix = OperationalStatusKeys.Digest(tenantId, folderId);
        DateTimeOffset observed = DateTimeOffset.UnixEpoch;
        foreach (OperationalStatusEvent item in source.DistinctBy(static item => item.Sequence))
        {
            observed = item.Timestamp > observed ? item.Timestamp : observed;
            prefix = OperationalStatusKeys.Digest(prefix, item.Digest);
            prefixes[item.Sequence] = prefix;
            OperationalStatusReceipt receipt = OperationalStatusReceipt.Create(item.Sequence, item.MessageId, item.Digest, prefix, next.RequiredViews[item.Sequence]);
            AddRetained(OperationalStatusKeys.Receipt(tenantId, folderId, item.Sequence), receipt, SourceTime(item, observed), operations);
            AddRetained(OperationalStatusKeys.Message(tenantId, folderId, item.MessageId), receipt, SourceTime(item, observed), operations);
        }
        foreach ((string id, TaskStatusReadModelSnapshot task) in next.Tasks.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            long position = long.Parse(task.Freshness.ProjectionWatermark!, CultureInfo.InvariantCulture);
            AddView(OperationalStatusKeys.Task(tenantId, folderId, id), task, position, prefixes[position],
                task.Freshness.ObservedAt, operations);
        }
        foreach (var closed in next.Checkpoint.State.TaskClosures.Values
            .OrderBy(value => next.Checkpoint.TaskClosedSequences[Hexalith.Folders.Aggregates.Folder.FolderState.TaskBindingKey(value.WorkspaceId, value.TaskId)])
            .DistinctBy(static value => value.TaskId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            long position = next.Checkpoint.TaskClosedSequences[Hexalith.Folders.Aggregates.Folder.FolderState.TaskBindingKey(closed.WorkspaceId, closed.TaskId)];
            operations.Add(Write(OperationalStatusKeys.TaskCompletion(tenantId, folderId, closed.TaskId),
                OperationalStatusTaskCompletion.Create(tenantId, folderId, closed, position, prefixes[position])));
        }
        cancellationToken.ThrowIfCancellationRequested();
        operations.Add(Write(OperationalStatusKeys.Checkpoint(tenantId, folderId), OperationalStatusCompaction.Compact(next.Checkpoint)));
        return operations;
    }

    private static IEnumerable<OperationalStatusEvent> ObserveCancellation(IEnumerable<OperationalStatusEvent> events, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        foreach (OperationalStatusEvent item in events)
        {
            token.ThrowIfCancellationRequested();
            yield return item;
        }
        token.ThrowIfCancellationRequested();
    }

    private async Task<ReadModelEntry<T>> ReadAsync<T>(SharedProjectionScope scope, long generation, string key,
        CancellationToken cancellationToken) where T : class
    {
        try { return await _coordinator.ReadGenerationAsync<T>(scope, generation, key, cancellationToken).ConfigureAwait(false); }
        catch (SharedProjectionReadUnavailableException exception) when (IsMissingValue(exception)
            && !key.Contains(":checkpoint:", StringComparison.Ordinal) && !key.Contains(":task-completion:", StringComparison.Ordinal))
        {
            return new(null, null);
        }
    }

    private static bool IsMissingValue(SharedProjectionReadUnavailableException exception)
        => exception.InnerException is InvalidOperationException
        { Message: "A selected copy-on-write value is missing." or "An inherited copy-on-write value is missing." };

    // A position's identity evidence must survive every view stamped from that original event.
    private static DateTimeOffset SourceTime(OperationalStatusEvent source, DateTimeOffset observed)
        => source.Event?.OccurredAt is { } occurred && occurred > observed ? occurred : observed;

    private bool Expired(DateTimeOffset observedAt) => _clock.UtcNow - observedAt >= OperationalStatusKeys.Retention;

    private static ReadModelBatchOperation Write<T>(string key, T value) where T : class
        => ReadModelBatchOperation.Write(key, value, ReadModelBatchConcurrency.LastWrite);

    private void AddRetained<T>(string key, T value, DateTimeOffset observedAt,
        List<ReadModelBatchOperation> operations) where T : class
    {
        DateTimeOffset deadline = observedAt + OperationalStatusKeys.Retention;
        if (deadline > _clock.UtcNow)
            operations.Add(ReadModelBatchOperation.WriteExpiring(key, value, ReadModelBatchConcurrency.LastWrite, deadline));
    }

    private void AddView<T>(string key, T value, long position, string digest, DateTimeOffset observedAt,
        List<ReadModelBatchOperation> operations) where T : class
    {
        OperationalStatusViewEvidence evidence = new(position, digest, OperationalStatusIntegrity.Digest(value));
        AddRetained(key, value, observedAt, operations);
        AddRetained(OperationalStatusKeys.Evidence(key), evidence, observedAt, operations);
        // Applicability is domain metadata written in the same SDK batch/generation. It expires
        // at the identical source deadline; it never extends C3 or replaces closure authority.
        if (value is OperationalWorkspaceStatus or TaskStatusReadModelSnapshot)
            AddRetained(OperationalStatusKeys.Applicability(key), OperationalStatusViewApplicability.Create(key, evidence), observedAt, operations);
    }
}
