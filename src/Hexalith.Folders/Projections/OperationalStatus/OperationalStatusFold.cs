using System.Collections.Frozen;
using System.Globalization;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Queries.Folders;

namespace Hexalith.Folders.Projections.OperationalStatus;

/// <summary>Pure deterministic fold shared by live delivery and complete-prefix rebuild.</summary>
public static class OperationalStatusFold
{
    /// <summary>Folds ordered positions, rejecting gaps and conflicting duplicates before producing candidates.</summary>
    public static OperationalStatusFoldResult Apply(string tenantId, string folderId,
        OperationalStatusCheckpoint? current, IEnumerable<OperationalStatusEvent> events, bool bounded = false)
    {
        ArgumentNullException.ThrowIfNull(events);
        FolderStreamName stream = FolderStreamName.Create(tenantId, folderId);
        if (current is not null && !Valid(current, tenantId, folderId))
        {
            throw new InvalidOperationException("Malformed operational checkpoint.");
        }
        FolderState state = current?.State ?? FolderState.Empty;
        bounded |= current?.SchemaVersion == 2;
        long sequence = current?.Sequence ?? 0;
        string digest = current?.SourceDigest ?? OperationalStatusKeys.Digest(tenantId, folderId);
        DateTimeOffset observed = current?.ObservedAt ?? DateTimeOffset.UnixEpoch;
        Dictionary<long, string> receipts = current is null ? [] : new(current.EventDigests);
        Dictionary<string, long> closedSequences = current is null ? new(StringComparer.Ordinal) : new(current.TaskClosedSequences, StringComparer.Ordinal);
        Dictionary<long, string> messages = current is null ? [] : new(current.MessageIds);
        HashSet<string> messageIds = new(messages.Values, StringComparer.Ordinal);
        Dictionary<string, string> workspaceEvidence = current is null ? new(StringComparer.Ordinal) : new(current.WorkspaceEvidence, StringComparer.Ordinal);
        Dictionary<string, string> taskEvidence = current is null ? new(StringComparer.Ordinal) : new(current.TaskEvidence, StringComparer.Ordinal);
        Dictionary<string, OperationalWorkspaceStatus> workspaces = new(StringComparer.Ordinal);
        Dictionary<string, TaskStatusReadModelSnapshot> tasks = new(StringComparer.Ordinal);
        Dictionary<long, OperationalStatusViewBinding> requiredViews = [];
        foreach (OperationalStatusEvent item in events)
        {
            if (item.Sequence <= sequence)
            {
                if (!receipts.TryGetValue(item.Sequence, out string? existing) || existing != item.Digest
                    || !messages.TryGetValue(item.Sequence, out string? message) || message != item.MessageId)
                {
                    throw new InvalidOperationException("Operational source identity conflict.");
                }
                continue;
            }
            if (item.Sequence != sequence + 1 || item.Digest is not { Length: 64 }
                || string.IsNullOrWhiteSpace(item.MessageId) || !messageIds.Add(item.MessageId) || item.Timestamp == default)
            {
                throw new InvalidOperationException("Operational source sequence gap.");
            }
            if (item.Event is { } value)
            {
                if (value.OrganizationId != tenantId)
                {
                    throw new InvalidOperationException("Foreign operational source event.");
                }
                ValidateWorkspaceEvent(state, value);
                state = state.Apply([value], stream);
                if (!ValidState(state)) throw new InvalidOperationException("Malformed operational state.");
                foreach (string binding in state.TaskClosures.Keys)
                    closedSequences.TryAdd(binding, item.Sequence);
            }
            sequence = item.Sequence;
            receipts.Add(sequence, item.Digest);
            messages.Add(sequence, item.MessageId);
            digest = OperationalStatusKeys.Digest(digest, item.Digest);
            observed = item.Timestamp > observed ? item.Timestamp : observed;
            requiredViews[item.Sequence] = new(state.WorkspaceId,
                state.WorkspaceLifecycleState is null ? null : state.WorkspaceTaskId);
            if (item.Event is not null && state.WorkspaceLifecycleState is not null)
            {
                // Rejected requests advance source evidence, never rewrite task outcomes.
                if (!string.IsNullOrWhiteSpace(state.WorkspaceId))
                {
                    workspaces[state.WorkspaceId] = Workspace(state, sequence, digest, observed);
                    workspaceEvidence[state.WorkspaceId] = OperationalStatusIntegrity.Digest(workspaces[state.WorkspaceId]);
                }
                if (!string.IsNullOrWhiteSpace(state.WorkspaceTaskId))
                {
                    TaskStatusReadModelSnapshot task = Task(state, sequence, observed, closedSequences);
                    tasks[task.TaskId] = task;
                    taskEvidence[task.TaskId] = OperationalStatusIntegrity.Digest(task);
                }
            }
        }
        if (sequence == 0)
        {
            throw new InvalidOperationException("Operational source has no folder anchor.");
        }
        // Do not duplicate command keys, ACLs, display labels or paths into operational storage.
        state = state with
        {
            DisplayName = null,
            Description = null,
            PathLabel = null,
            Tags = [],
            AccessOverrides = FrozenDictionary<FolderAccessEntryKey, FolderAccessOverride>.Empty,
            IdempotencyFingerprints = FrozenDictionary<string, string>.Empty,
            RepositoryBindingIdempotencyKey = null,
            RepositoryBindingIdempotencyFingerprint = null,
            ArchiveIdempotencyKey = null,
            BranchRefPolicy = state.BranchRefPolicy is { } policy ? policy with { IdempotencyKey = null!, IdempotencyFingerprint = null! } : null,
        };
        OperationalStatusCheckpoint checkpoint = new(bounded ? 2 : 1, tenantId, folderId, sequence, digest, observed, state, receipts)
        {
            TaskClosedSequences = closedSequences,
            MessageIds = messages,
            WorkspaceEvidence = workspaceEvidence,
            TaskEvidence = taskEvidence,
        };
        checkpoint = checkpoint with { IntegrityDigest = OperationalStatusIntegrity.Checkpoint(checkpoint) };
        return new(checkpoint, state.IsCreated ? Lifecycle(state, sequence, observed) : null!, workspaces, tasks, requiredViews);
    }

    /// <summary>Checks the stored checkpoint identity and required source evidence.</summary>
    public static bool Valid(OperationalStatusCheckpoint value, string tenantId, string folderId)
    {
        if (value?.SchemaVersion == 2)
            return value.ManagedTenantId == tenantId && value.FolderId == folderId && value.Sequence > 0
                && value.SourceDigest is { Length: 64 } && value.ObservedAt != default
                && value.State is not null && (value.State.IsCreated
                    ? value.State.ManagedTenantId == tenantId && value.State.OrganizationId == tenantId
                        && value.State.FolderId == folderId && ValidState(value.State)
                    : OperationalStatusIntegrity.Digest(value.State) == OperationalStatusIntegrity.Digest(FolderState.Empty))
                && value.EventDigests is { Count: 1 } && value.EventDigests.TryGetValue(value.Sequence, out string? tail)
                && tail is { Length: 64 } && value.MessageIds is { Count: 1 }
                && value.MessageIds.TryGetValue(value.Sequence, out string? message) && !string.IsNullOrWhiteSpace(message)
                && value.State.TaskClosures is { Count: 0 } && value.TaskClosedSequences is { Count: 0 }
                && value.WorkspaceEvidence is { Count: 0 } && value.TaskEvidence is { Count: 0 }
                && value.IntegrityDigest == OperationalStatusIntegrity.Checkpoint(value);
        if (value is null || value.SchemaVersion != 1 || value.ManagedTenantId != tenantId || value.FolderId != folderId
            || value.Sequence < 1 || value.SourceDigest is not { Length: 64 } || value.EventDigests is null
            || value.EventDigests.Count != value.Sequence || value.ObservedAt == default || value.TaskClosedSequences is null
            || value.State is not { TaskClosures: not null } || value.State.IsCreated && (value.State.ManagedTenantId != tenantId
                || value.State.OrganizationId != tenantId || value.State.FolderId != folderId)
            || value.MessageIds is null || value.MessageIds.Count != value.Sequence
            || value.MessageIds.Values.Any(string.IsNullOrWhiteSpace)
            || value.MessageIds.Values.Distinct(StringComparer.Ordinal).LongCount() != value.Sequence
            || value.WorkspaceEvidence is null || value.TaskEvidence is null || value.State.IsCreated && !ValidState(value.State)
            || value.WorkspaceEvidence.Any(static item => string.IsNullOrWhiteSpace(item.Key) || item.Value is not { Length: 64 })
            || value.TaskEvidence.Any(static item => string.IsNullOrWhiteSpace(item.Key) || item.Value is not { Length: 64 })
            || value.TaskClosedSequences.Count != value.State.TaskClosures.Count
            || value.TaskClosedSequences.Values.Distinct().Count() != value.TaskClosedSequences.Count
            || value.State.TaskClosures.Any(item => !value.TaskClosedSequences.TryGetValue(item.Key, out long position)
                || position < 1 || position > value.Sequence
                || !value.WorkspaceEvidence.ContainsKey(item.Value.WorkspaceId) || !value.TaskEvidence.ContainsKey(item.Value.TaskId)))
            return false;
        string digest = OperationalStatusKeys.Digest(tenantId, folderId);
        for (long sequence = 1; sequence <= value.Sequence; sequence++)
        {
            if (!value.EventDigests.TryGetValue(sequence, out string? item) || item is not { Length: 64 } || !value.MessageIds.ContainsKey(sequence)) return false;
            digest = OperationalStatusKeys.Digest(digest, item);
        }
        return digest == value.SourceDigest && value.IntegrityDigest == OperationalStatusIntegrity.Checkpoint(value);
    }

    private static void ValidateWorkspaceEvent(FolderState state, IFolderEvent value)
    {
        string? workspace = value switch
        {
            WorkspacePreparationRequested item => item.WorkspaceId,
            FolderWorkspaceLifecycleEventRecorded item => item.WorkspaceId,
            WorkspaceLockAcquired item => item.WorkspaceId,
            WorkspaceLockReleased item => item.WorkspaceId,
            WorkspaceFileMutationAccepted item => item.WorkspaceId,
            WorkspaceCommitSucceeded item => item.WorkspaceId,
            WorkspaceCommitFailed item => item.WorkspaceId,
            WorkspaceCommitOutcomeUnknown item => item.WorkspaceId,
            _ => null,
        };
        bool workspaceEvent = value is WorkspacePreparationRequested or FolderWorkspaceLifecycleEventRecorded
            or WorkspaceLockAcquired or WorkspaceLockReleased or WorkspaceFileMutationAccepted
            or WorkspaceCommitSucceeded or WorkspaceCommitFailed or WorkspaceCommitOutcomeUnknown;
        if (workspaceEvent && (string.IsNullOrWhiteSpace(workspace)
                || value is not WorkspacePreparationRequested && workspace != state.WorkspaceId)
            || value is WorkspaceLockAcquired acquired && (acquired.WorkspaceLifecycleEvent != FolderWorkspaceLifecycleEvent.WorkspaceLocked
                || acquired.HolderTaskId != acquired.TaskId || string.IsNullOrWhiteSpace(acquired.LockId)
                || acquired.RequestedLeaseSeconds <= 0 || acquired.AcquiredAt == default
                || acquired.AcquiredAt != acquired.OccurredAt || acquired.EffectiveAt != acquired.AcquiredAt
                || acquired.ExpiresAt <= acquired.EffectiveAt
                || acquired.ExpiresAt - acquired.EffectiveAt != TimeSpan.FromSeconds(acquired.RequestedLeaseSeconds))
            || value is WorkspaceLockReleased released && (released.WorkspaceLifecycleEvent != FolderWorkspaceLifecycleEvent.WorkspaceLockReleased
                || released.HolderTaskId != released.TaskId || released.LockId != state.WorkspaceLockId
                || released.HolderTaskId != state.WorkspaceLockHolderTaskId)
            || value is WorkspaceFileMutationAccepted mutation && mutation.WorkspaceLifecycleEvent != FolderWorkspaceLifecycleEvent.FileMutated
            || value is WorkspaceCommitSucceeded succeeded && (succeeded.WorkspaceLifecycleEvent != FolderWorkspaceLifecycleEvent.CommitSucceeded
                || string.IsNullOrWhiteSpace(succeeded.CommitReference))
            || value is WorkspaceCommitFailed failed && failed.WorkspaceLifecycleEvent != FolderWorkspaceLifecycleEvent.CommitFailed
            || value is WorkspaceCommitOutcomeUnknown unknown && unknown.WorkspaceLifecycleEvent != FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown)
        {
            throw new InvalidOperationException("Malformed operational workspace evidence.");
        }
    }

    private static FolderLifecycleFreshness Freshness(long sequence, DateTimeOffset observed, string consistency = "read_your_writes")
        => new(consistency, observed, sequence.ToString(CultureInfo.InvariantCulture), false, null);
    private static FolderLifecycleEvidenceScope Evidence(FolderState state, string action)
        => new(state.ManagedTenantId, null, action, state.WorkspaceTaskId, state.WorkspaceCorrelationId, null);
    private static FolderLifecycleStatusReadModelSnapshot Lifecycle(FolderState state, long sequence, DateTimeOffset observed)
        => new(state.ManagedTenantId!, state.FolderId!, state.LifecycleState == FolderLifecycleState.Archived
            ? FolderLifecycleProjectionState.Archived : FolderLifecycleProjectionState.Active,
            state.RepositoryBindingState switch
            {
                FolderRepositoryBindingState.BindingRequested => FolderRepositoryBindingStatus.BindingRequested,
                FolderRepositoryBindingState.Bound => FolderRepositoryBindingStatus.Bound,
                FolderRepositoryBindingState.Failed => FolderRepositoryBindingStatus.Failed,
                FolderRepositoryBindingState.UnknownProviderOutcome => FolderRepositoryBindingStatus.UnknownProviderOutcome,
                FolderRepositoryBindingState.ReconciliationRequired => FolderRepositoryBindingStatus.ReconciliationRequired,
                _ => FolderRepositoryBindingStatus.Unbound,
            }, state.RepositoryBindingId, state.ProviderBindingRef, Freshness(sequence, observed, "eventually_consistent"),
            new(state.ManagedTenantId, null, "read_metadata", null, null, null), []);

    /// <summary>Checks lifecycle state and its always-current observation against the source fold.</summary>
    internal static bool MatchesLifecycle(OperationalStatusCheckpoint checkpoint, FolderLifecycleStatusReadModelSnapshot value)
    {
        FolderLifecycleStatusReadModelSnapshot expected = Lifecycle(checkpoint.State, checkpoint.Sequence, checkpoint.ObservedAt);
        return OperationalStatusIntegrity.Digest(value) == OperationalStatusIntegrity.Digest(expected);
    }

    /// <summary>Checks every workspace concern, including historical rows, against retained source evidence.</summary>
    internal static bool MatchesCurrentWorkspace(OperationalStatusCheckpoint checkpoint, OperationalWorkspaceStatus value)
        => checkpoint.WorkspaceEvidence.TryGetValue(value.Workspace.WorkspaceId, out string? evidence)
            && evidence == OperationalStatusIntegrity.Digest(value);

    /// <summary>Checks every task field against its original materialized source generation.</summary>
    internal static bool MatchesTask(OperationalStatusCheckpoint checkpoint, TaskStatusReadModelSnapshot value)
        => checkpoint.TaskEvidence.TryGetValue(value.TaskId, out string? evidence)
            && evidence == OperationalStatusIntegrity.Digest(value);

    private static bool ValidState(FolderState state)
        => state.LifecycleState is { } lifecycle && Enum.IsDefined(lifecycle)
            && (state.RepositoryBindingState is null || Enum.IsDefined(state.RepositoryBindingState.Value))
            && (state.WorkspaceLifecycleState is null || Enum.IsDefined(state.WorkspaceLifecycleState.Value))
            && (state.WorkspaceOperatorDisposition is null || Enum.IsDefined(state.WorkspaceOperatorDisposition.Value))
            && (state.WorkspaceLifecycleEvent is null || Enum.IsDefined(state.WorkspaceLifecycleEvent.Value))
            && (state.ArchiveReasonCode is null || Enum.IsDefined(state.ArchiveReasonCode.Value))
            && state.TaskClosures is not null && state.TaskClosures.All(static item => item.Value is { } closure
                && !string.IsNullOrWhiteSpace(closure.WorkspaceId) && !string.IsNullOrWhiteSpace(closure.TaskId)
                && !string.IsNullOrWhiteSpace(closure.OperationId) && !string.IsNullOrWhiteSpace(closure.CorrelationId)
                && closure.TerminalState is "ready" or "committed" && closure.ClosedAt != default
                && item.Key == FolderState.TaskBindingKey(closure.WorkspaceId, closure.TaskId));

    private static OperationalWorkspaceStatus Workspace(FolderState state, long sequence, string digest, DateTimeOffset observed)
    {
        string current = FolderStateTransitions.ToWireName(state.WorkspaceLifecycleState!.Value);
        FolderLifecycleFreshness freshness = Freshness(sequence, observed);
        WorkspaceStatusRetryEligibility retry = Retry(current);
        string operation = state.WorkspaceOperationId ?? state.WorkspaceId!;
        string? failure = Failure(state, current);
        string providerState = state.WorkspaceCommitReference is not null || state.WorkspaceCommitOutcomeCategory == "success"
            ? "known_success" : state.WorkspaceCommitFailureCategory is not null ? "known_failure"
            : state.WorkspaceCommitReconciliationRequired ? "reconciliation_required"
            : state.WorkspaceCommitOutcomeCategory == "unknown_provider_outcome" ? "unknown_provider_outcome"
            : current switch
            {
                "failed" or "inaccessible" => "known_failure",
                "unknown_provider_outcome" => "unknown_provider_outcome",
                "reconciliation_required" => "reconciliation_required",
                _ => "pending",
            };
        string providerCategory = state.WorkspaceCommitFailureCategory ?? state.WorkspaceCommitOutcomeCategory
            ?? failure ?? "redacted";
        WorkspaceStatusReadModelSnapshot workspace = new(state.ManagedTenantId!, state.FolderId!, state.WorkspaceId!, current,
            new(state.WorkspaceTaskId!, operation, current == "committed" ? "completed" : current is "failed" or "inaccessible" ? "failed" : "accepted",
                state.WorkspaceLifecycleUpdatedAt ?? observed), new(current, "projection", observed),
            new(state.WorkspaceCommitOperationId ?? operation, providerState, providerCategory,
                "provref_unavailable", retry, null, freshness, state.WorkspaceCommitChangedPathMetadataDigest,
                state.WorkspaceCommitReference is null ? null : "opaque_reference", state.WorkspaceCommitReconciliationReference),
            retry, null, freshness, new(null, "unavailable"), failure, Evidence(state, WorkspaceStatusQueryHandler.ActionToken));
        WorkspaceLockStatusReadModelSnapshot locked = new(state.ManagedTenantId!, state.FolderId!, state.WorkspaceId!, current,
            state.WorkspaceLockId is null ? "unlocked" : "locked", state.WorkspaceLockId, state.WorkspaceLockHolderTaskId,
            state.WorkspaceLockAcquiredAt, state.WorkspaceLockEffectiveAt, state.WorkspaceLockExpiresAt,
            state.WorkspaceLockRetryEligibilityBasis, state.WorkspaceCorrelationId, state.WorkspaceTaskId, freshness,
            Evidence(state, WorkspaceLockStatusQueryHandler.ActionToken));
        WorkspaceCleanupStatusReadModelSnapshot cleanup = new(state.ManagedTenantId!, state.FolderId!, state.WorkspaceId!,
            state.WorkspaceTaskId, "status_only", "cleanup_status_only", new(false, "cleanup_not_applicable"), freshness,
            state.WorkspaceCorrelationId, observed, null, Evidence(state, WorkspaceCleanupStatusQueryHandler.ActionToken));
        return new(1, sequence, digest, locked, workspace, cleanup);
    }

    private static TaskStatusReadModelSnapshot Task(FolderState state, long sequence, DateTimeOffset observed,
        IReadOnlyDictionary<string, long> closedSequences)
    {
        string current = FolderStateTransitions.ToWireName(state.WorkspaceLifecycleState!.Value);
        FolderTaskClosure? closed = state.TaskClosures.Values.Where(value => value.TaskId == state.WorkspaceTaskId)
            .OrderBy(value => closedSequences[FolderState.TaskBindingKey(value.WorkspaceId, value.TaskId)]).FirstOrDefault();
        return new(state.ManagedTenantId!, state.WorkspaceTaskId!, closed?.TerminalState ?? current,
            closed?.TerminalState ?? (current is "failed" or "inaccessible" ? current : null),
            closed?.OperationId ?? state.WorkspaceOperationId, closed is null ? Failure(state, current) : null,
            closed is null ? Retry(current) : new(false, "retry_not_required"), null,
            Freshness(closed is null ? sequence : closedSequences[FolderState.TaskBindingKey(closed.WorkspaceId, closed.TaskId)],
                closed?.ClosedAt ?? observed, "eventually_consistent"),
            Evidence(state, TaskStatusQueryHandler.ActionToken) with { CorrelationId = closed?.CorrelationId ?? state.WorkspaceCorrelationId }, state.FolderId);
    }
    private static WorkspaceStatusRetryEligibility Retry(string current) => current switch
    {
        "failed" => new(false, "failed_operation"),
        "dirty" => new(true, "dirty_workspace"),
        "unknown_provider_outcome" or "reconciliation_required" => new(false, current),
        "locked" => new(false, "workspace_locked"),
        "inaccessible" => new(false, "tenant_access_denied"),
        _ => new(false, "retry_not_required"),
    };
    private static string? Failure(FolderState state, string current) => current switch
    {
        "failed" => state.WorkspaceCommitFailureCategory ?? state.RepositoryBindingFailureCategory ?? "failed_operation",
        "inaccessible" => "tenant_access_denied",
        "unknown_provider_outcome" or "reconciliation_required" => state.WorkspaceCommitOutcomeCategory ?? state.RepositoryBindingOutcomeCategory ?? current,
        _ => null,
    };
}
