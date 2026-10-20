using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Authorization;
using Hexalith.Folders.Projections.OperationalStatus;
using Hexalith.Folders.Projections.TenantAccess;

namespace Hexalith.Folders.Queries.Folders;

/// <summary>Reads committed operational metadata after the query layer has authorized the caller.</summary>
public sealed class EventStoreOperationalStatusReadModel(SharedProjectionEpochCoordinator coordinator, IUtcClock clock,
    IOrganizationStreamSequenceReader sequenceReader) : IFolderLifecycleStatusReadModel, IWorkspaceLockStatusReadModel,
    IWorkspaceStatusReadModel, IWorkspaceCleanupStatusReadModel, ITaskStatusReadModel
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public async Task<FolderLifecycleStatusReadModelResult> GetAsync(FolderLifecycleStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            (var value, var freshness, string status) = await ReadAsync<FolderLifecycleStatusReadModelSnapshot>(
                OperationalStatusKeys.Lifecycle(request.ManagedTenantId, request.FolderId), request.ManagedTenantId, request.FolderId,
                value => value.ManagedTenantId == request.ManagedTenantId && value.FolderId == request.FolderId,
                value => value.FolderId!, value => value.Freshness, cancellationToken, requireView: true, requireCurrentSequence: true,
                sourceEvidence: static (value, checkpoint) => OperationalStatusFold.MatchesLifecycle(checkpoint, value)).ConfigureAwait(false);
            if (status == "missing") return FolderLifecycleStatusReadModelResult.NotFound(freshness);
            if (status == "stale") return new(FolderLifecycleStatusReadModelStatus.Stale, null, freshness);
            if (value is null) return FolderLifecycleStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
            var snapshot = value with
            {
                Freshness = freshness with { ReadConsistency = request.ReadConsistency },
                EvidenceScope = new(request.ManagedTenantId, request.PrincipalId, request.ActionToken,
                    request.TaskId, request.CorrelationId, request.AuthorizationWatermark),
            };

            if (snapshot.Freshness.Stale)
            {
                return new(FolderLifecycleStatusReadModelStatus.Stale, null, snapshot.Freshness);
            }
            return FolderLifecycleStatusReadModelResult.Available(snapshot);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return FolderLifecycleStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
        }
    }
    /// <inheritdoc/>
    public async Task<WorkspaceLockStatusReadModelResult> GetAsync(WorkspaceLockStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            (var value, var freshness, string status) = await ReadAsync<OperationalWorkspaceStatus>(
                OperationalStatusKeys.Workspace(request.ManagedTenantId, request.FolderId, request.WorkspaceId), request.ManagedTenantId, request.FolderId,
                value => value.SchemaVersion == 1 && value.Workspace.ManagedTenantId == request.ManagedTenantId && value.Workspace.FolderId == request.FolderId && value.Workspace.WorkspaceId == request.WorkspaceId && value.Lock.ManagedTenantId == request.ManagedTenantId && value.Lock.FolderId == request.FolderId && value.Lock.WorkspaceId == request.WorkspaceId && value.Cleanup.ManagedTenantId == request.ManagedTenantId && value.Cleanup.FolderId == request.FolderId && value.Cleanup.WorkspaceId == request.WorkspaceId,
                value => value.Lock.FolderId!, value => value.Lock.Freshness, cancellationToken,
                requireKnownView: checkpoint => KnownWorkspace(checkpoint, request.WorkspaceId),
                sourceEvidence: WorkspaceEvidence).ConfigureAwait(false);
            if (status == "missing") return WorkspaceLockStatusReadModelResult.NotFound(freshness);
            if (status == "stale") return new(WorkspaceLockStatusReadModelStatus.Stale, null, freshness);
            if (value is null) return WorkspaceLockStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
            var snapshot = value.Lock with
            {
                Freshness = freshness with { ReadConsistency = request.ReadConsistency },
                EvidenceScope = new(request.ManagedTenantId, request.PrincipalId, request.ActionToken,
                    request.TaskId, request.CorrelationId, request.AuthorizationWatermark),
            };

            if (snapshot.Freshness.Stale)
            {
                return new(WorkspaceLockStatusReadModelStatus.Stale, null, snapshot.Freshness);
            }
            return WorkspaceLockStatusReadModelResult.Available(snapshot);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return WorkspaceLockStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
        }
    }
    /// <inheritdoc/>
    public async Task<WorkspaceStatusReadModelResult> GetAsync(WorkspaceStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            (var value, var freshness, string status) = await ReadAsync<OperationalWorkspaceStatus>(
                OperationalStatusKeys.Workspace(request.ManagedTenantId, request.FolderId, request.WorkspaceId), request.ManagedTenantId, request.FolderId,
                value => value.SchemaVersion == 1 && value.Workspace.ManagedTenantId == request.ManagedTenantId && value.Workspace.FolderId == request.FolderId && value.Workspace.WorkspaceId == request.WorkspaceId && value.Lock.ManagedTenantId == request.ManagedTenantId && value.Lock.FolderId == request.FolderId && value.Lock.WorkspaceId == request.WorkspaceId && value.Cleanup.ManagedTenantId == request.ManagedTenantId && value.Cleanup.FolderId == request.FolderId && value.Cleanup.WorkspaceId == request.WorkspaceId,
                value => value.Workspace.FolderId!, value => value.Workspace.Freshness, cancellationToken,
                requireKnownView: checkpoint => KnownWorkspace(checkpoint, request.WorkspaceId),
                sourceEvidence: WorkspaceEvidence).ConfigureAwait(false);
            if (status == "missing") return WorkspaceStatusReadModelResult.NotFound(freshness);
            if (status == "stale") return new(WorkspaceStatusReadModelStatus.Stale, null, freshness);
            if (value is null) return WorkspaceStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
            var snapshot = value.Workspace with
            {
                Freshness = freshness with { ReadConsistency = request.ReadConsistency },
                EvidenceScope = new(request.ManagedTenantId, request.PrincipalId, request.ActionToken,
                    request.TaskId, request.CorrelationId, request.AuthorizationWatermark),
            };

            if (snapshot.Freshness.Stale)
            {
                return new(WorkspaceStatusReadModelStatus.Stale, null, snapshot.Freshness);
            }
            snapshot = snapshot with
            {
                ProviderOutcome = snapshot.ProviderOutcome with { Freshness = snapshot.Freshness },
                ProjectionLag = new((long)Math.Max(0, (clock.UtcNow - snapshot.Freshness.ObservedAt).TotalMilliseconds), "projection"),
            };
            return WorkspaceStatusReadModelResult.Available(snapshot);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return WorkspaceStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
        }
    }
    /// <inheritdoc/>
    public async Task<WorkspaceCleanupStatusReadModelResult> GetAsync(WorkspaceCleanupStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            (var value, var freshness, string status) = await ReadAsync<OperationalWorkspaceStatus>(
                OperationalStatusKeys.Workspace(request.ManagedTenantId, request.FolderId, request.WorkspaceId), request.ManagedTenantId, request.FolderId,
                value => value.SchemaVersion == 1 && value.Workspace.ManagedTenantId == request.ManagedTenantId && value.Workspace.FolderId == request.FolderId && value.Workspace.WorkspaceId == request.WorkspaceId && value.Lock.ManagedTenantId == request.ManagedTenantId && value.Lock.FolderId == request.FolderId && value.Lock.WorkspaceId == request.WorkspaceId && value.Cleanup.ManagedTenantId == request.ManagedTenantId && value.Cleanup.FolderId == request.FolderId && value.Cleanup.WorkspaceId == request.WorkspaceId,
                value => value.Cleanup.FolderId!, value => value.Cleanup.Freshness, cancellationToken,
                requireKnownView: checkpoint => KnownWorkspace(checkpoint, request.WorkspaceId),
                sourceEvidence: WorkspaceEvidence).ConfigureAwait(false);
            if (status == "missing") return WorkspaceCleanupStatusReadModelResult.NotFound(freshness);
            if (status == "stale") return new(WorkspaceCleanupStatusReadModelStatus.Stale, null, freshness);
            if (value is null) return WorkspaceCleanupStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
            if (request.TaskId is not null && value.Cleanup.TaskId != request.TaskId)
                return WorkspaceCleanupStatusReadModelResult.Unavailable("task_scope_mismatch", clock.UtcNow);
            var snapshot = value.Cleanup with
            {
                TaskId = request.TaskId,
                CorrelationId = request.CorrelationId,
                Freshness = freshness with { ReadConsistency = request.ReadConsistency },
                EvidenceScope = new(request.ManagedTenantId, request.PrincipalId, request.ActionToken,
                    request.TaskId, request.CorrelationId, request.AuthorizationWatermark),
            };

            if (snapshot.Freshness.Stale)
            {
                return new(WorkspaceCleanupStatusReadModelStatus.Stale, null, snapshot.Freshness);
            }
            return WorkspaceCleanupStatusReadModelResult.Available(snapshot);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return WorkspaceCleanupStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
        }
    }
    /// <inheritdoc/>
    public async Task<TaskStatusReadModelResult> GetAsync(TaskStatusReadModelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.AuthorizedFolderId) || string.IsNullOrWhiteSpace(request.AuthorizationWatermark))
        {
            return TaskStatusReadModelResult.Unavailable("task_folder_authority_unavailable", clock.UtcNow);
        }
        try
        {
            (var value, var freshness, string status) = await ReadAsync<TaskStatusReadModelSnapshot>(
                OperationalStatusKeys.Task(request.ManagedTenantId, request.AuthorizedFolderId, request.TaskId), request.ManagedTenantId, request.AuthorizedFolderId,
                value => value.ManagedTenantId == request.ManagedTenantId && value.TaskId == request.TaskId && value.FolderId == request.AuthorizedFolderId,
                value => value.FolderId!, value => value.Freshness, cancellationToken,
                requireKnownView: checkpoint => checkpoint.TaskEvidence.ContainsKey(request.TaskId),
                sourceEvidence: TaskEvidence).ConfigureAwait(false);
            if (status == "missing") return TaskStatusReadModelResult.NotFound(freshness);
            if (status == "stale") return new(TaskStatusReadModelStatus.Stale, null, freshness);
            if (value is null) return TaskStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
            var snapshot = value with
            {
                Freshness = freshness with { ReadConsistency = request.ReadConsistency },
                EvidenceScope = new(request.ManagedTenantId, request.PrincipalId, request.ActionToken,
                    request.TaskId, request.CorrelationId, request.AuthorizationWatermark),
            };

            if (snapshot.Freshness.Stale)
            {
                return new(TaskStatusReadModelStatus.Stale, null, snapshot.Freshness);
            }
            return TaskStatusReadModelResult.Available(snapshot);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return TaskStatusReadModelResult.Unavailable("read_model_unavailable", clock.UtcNow);
        }
    }

    private static bool KnownWorkspace(OperationalStatusCheckpoint checkpoint, string workspaceId)
        => checkpoint.WorkspaceEvidence.ContainsKey(workspaceId);

    private static bool WorkspaceEvidence(OperationalWorkspaceStatus value, OperationalStatusCheckpoint checkpoint)
    {
        if (value.Sequence < 1 || value.Sequence > checkpoint.Sequence) return false;
        string watermark = value.Sequence.ToString(CultureInfo.InvariantCulture);
        if (value.Lock.Freshness.ProjectionWatermark != watermark
            || value.Workspace.Freshness.ProjectionWatermark != watermark
            || value.Cleanup.Freshness.ProjectionWatermark != watermark) return false;
        // Rejections advance the checkpoint without changing a workspace. Its original
        // materialized source prefix must still match the validated retained receipts.
        string digest = OperationalStatusKeys.Digest(checkpoint.ManagedTenantId, checkpoint.FolderId);
        for (long sequence = 1; sequence <= value.Sequence; sequence++)
            digest = OperationalStatusKeys.Digest(digest, checkpoint.EventDigests[sequence]);
        return value.SourceDigest == digest && OperationalStatusFold.MatchesCurrentWorkspace(checkpoint, value);
    }

    private static bool TaskEvidence(TaskStatusReadModelSnapshot value, OperationalStatusCheckpoint checkpoint)
        => OperationalStatusFold.MatchesTask(checkpoint, value);

    private async Task<(T? Value, FolderLifecycleFreshness Freshness, string Status)> ReadAsync<T>(string key,
        string tenantId, string? folderId, Func<T, bool> identity, Func<T, string> folder,
        Func<T, FolderLifecycleFreshness> sourceFreshness, CancellationToken cancellationToken, bool requireView = false,
        Func<OperationalStatusCheckpoint, bool>? requireKnownView = null, bool requireCurrentSequence = false,
        Func<T, OperationalStatusCheckpoint, bool>? sourceEvidence = null) where T : class
    {
        FolderLifecycleFreshness absent = new("eventually_consistent", clock.UtcNow, null, false, null);
        if (folderId is null) return (null, absent, "missing");
        SharedProjectionScope scope = OperationalStatusGeneration.Scope(tenantId);
        string checkpointKey = OperationalStatusKeys.Checkpoint(tenantId, folderId);
        string evidenceKey = OperationalStatusKeys.Evidence(key);
        SharedProjectionReadResult<IReadOnlyDictionary<string, JsonObject?>> first;
        try { first = await ReadSelectedAsync(scope, [key, checkpointKey, evidenceKey], checkpointKey, cancellationToken).ConfigureAwait(false); }
        catch (InvalidOperationException exception) when (exception.Message == "The shared projection has no epoch state.")
        {
            long emptyHead = await sequenceReader.GetCurrentSequenceAsync(tenantId, folderId, cancellationToken).ConfigureAwait(false);
            return (null, absent with { Stale = emptyHead > 0, ReasonCode = emptyHead > 0 ? "projection_stale" : null }, emptyHead > 0 ? "stale" : "missing");
        }
        if (!first.IsAvailable || first.IsStale || first.IsDegraded || first.Value is null)
            throw new OperationalStatusUnavailableException();
        OperationalStatusViewEvidence? evidence = first.Value[evidenceKey]?.Deserialize<OperationalStatusViewEvidence>(WireJson);
        string? receiptKey = evidence is null ? null : OperationalStatusKeys.Receipt(tenantId, folderId, evidence.Sequence);
        var selected = receiptKey is null ? first : await ReadSelectedAsync(
            scope, [key, checkpointKey, evidenceKey, receiptKey], checkpointKey, cancellationToken).ConfigureAwait(false);
        if (!selected.IsAvailable || selected.IsStale || selected.IsDegraded || selected.Value is null
            || selected.Generation != first.Generation)
            throw new OperationalStatusUnavailableException();
        T? row = selected.Value[key]?.Deserialize<T>(WireJson);
        OperationalStatusCheckpoint? anchor = selected.Value[checkpointKey]?.Deserialize<OperationalStatusCheckpoint>(WireJson);
        evidence = selected.Value[evidenceKey]?.Deserialize<OperationalStatusViewEvidence>(WireJson);
        OperationalStatusReceipt? receipt = receiptKey is null ? null : selected.Value[receiptKey]?.Deserialize<OperationalStatusReceipt>(WireJson);
        if (row is not null && !identity(row)) throw new InvalidOperationException("Foreign operational view.");
        long head = await sequenceReader.GetCurrentSequenceAsync(tenantId, folderId, cancellationToken).ConfigureAwait(false);
        if (anchor is not { } checkpoint)
        {
            if (row is not null) throw new InvalidOperationException("Operational checkpoint is missing.");
            return (null, absent with { Stale = head > 0, ReasonCode = head > 0 ? "projection_stale" : null }, head > 0 ? "stale" : "missing");
        }
        if (!OperationalStatusFold.Valid(checkpoint, tenantId, folderId)
            || head < checkpoint.Sequence || checkpoint.ObservedAt - clock.UtcNow > ClockSkewTolerance)
            throw new InvalidOperationException("Operational checkpoint is invalid.");
        if (row is null)
        {
            if (evidence is not null || requireView && checkpoint.State.IsCreated
                && clock.UtcNow - checkpoint.ObservedAt < OperationalStatusKeys.Retention)
                throw new InvalidOperationException("Known operational view is missing.");
            return (null, absent, "missing");
        }
        FolderLifecycleFreshness freshness = sourceFreshness(row);
        if (freshness is null || !long.TryParse(freshness.ProjectionWatermark, NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
            || sequence < 1 || sequence > checkpoint.Sequence
            || requireCurrentSequence && sequence != checkpoint.Sequence
            || (requireCurrentSequence
                ? !OperationalStatusFold.MatchesLifecycle(checkpoint, (FolderLifecycleStatusReadModelSnapshot)(object)row)
                : evidence is null || receipt is null || !receipt.Valid() || evidence.Sequence != sequence || receipt.Sequence != sequence
                    || evidence.SourceDigest != receipt.SourceDigest || evidence.ValueDigest != OperationalStatusIntegrity.Digest(row))
            || freshness.ObservedAt - clock.UtcNow > ClockSkewTolerance)
            throw new InvalidOperationException("Operational view is invalid.");
        if (clock.UtcNow - freshness.ObservedAt >= OperationalStatusKeys.Retention)
            return (null, freshness, "missing");
        if (head > checkpoint.Sequence)
            return (null, absent with { Stale = true, ReasonCode = "projection_stale" }, "stale");
        return (row, freshness, "available");
    }

    private async Task<SharedProjectionReadResult<IReadOnlyDictionary<string, JsonObject?>>> ReadSelectedAsync(
        SharedProjectionScope scope, string[] keys, string checkpointKey, CancellationToken cancellationToken)
    {
        try { return await coordinator.ReadManyAsync<JsonObject>(scope, keys, cancellationToken).ConfigureAwait(false); }
        catch (SharedProjectionReadUnavailableException exception) when (exception.InnerException is InvalidOperationException
        { Message: "A selected copy-on-write value is missing." or "An inherited copy-on-write value is missing." })
        {
            // The SDK retains copy-on-write pointers after physical TTL expiry. Read the same selected
            // generation, preserving checkpoint and full-row evidence checks and rejecting promotion races.
            var before = await coordinator.ReadManyAsync<JsonObject>(scope, [checkpointKey], cancellationToken).ConfigureAwait(false);
            if (!before.IsAvailable || before.IsStale || before.IsDegraded || before.Value is null)
                throw new OperationalStatusUnavailableException();
            Dictionary<string, JsonObject?> values = new(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                try
                {
                    values[key] = (await coordinator.ReadGenerationAsync<JsonObject>(scope, before.Generation,
                        key, cancellationToken).ConfigureAwait(false)).Value;
                }
                catch (SharedProjectionReadUnavailableException missing) when (key != checkpointKey
                    && missing.InnerException is InvalidOperationException
                    { Message: "A selected copy-on-write value is missing." or "An inherited copy-on-write value is missing." })
                {
                    values[key] = null;
                }
            }
            var after = await coordinator.ReadManyAsync<JsonObject>(scope, [checkpointKey], cancellationToken).ConfigureAwait(false);
            if (!after.IsAvailable || after.IsStale || after.IsDegraded || after.Value is null
                || after.Generation != before.Generation
                || before.Value[checkpointKey]?.ToJsonString() != after.Value[checkpointKey]?.ToJsonString())
                throw new OperationalStatusUnavailableException();
            return new(values, before.Generation, false, true);
        }
    }

}
