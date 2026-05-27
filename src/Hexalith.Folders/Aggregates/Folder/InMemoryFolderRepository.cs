using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Hexalith.Folders.Queries.Folders;

[assembly: InternalsVisibleTo("Hexalith.Folders.Tests")]
[assembly: InternalsVisibleTo("Hexalith.Folders.IntegrationTests")]
[assembly: InternalsVisibleTo("Hexalith.Folders.Server.Tests")]

namespace Hexalith.Folders.Aggregates.Folder;

public sealed class InMemoryFolderRepository : IFolderRepository
{
    private readonly ConcurrentDictionary<string, string> _idempotencyFingerprints = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FolderState> _states = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastObservedAt = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly InMemoryBranchRefPolicyReadModel? _branchRefPolicyReadModel;
    private readonly InMemoryFolderLifecycleStatusReadModel? _lifecycleReadModel;
    private readonly InMemoryWorkspaceLockStatusReadModel? _workspaceLockStatusReadModel;
    private readonly InMemoryWorkspaceStatusReadModel? _workspaceStatusReadModel;
    private readonly InMemoryWorkspaceCleanupStatusReadModel? _workspaceCleanupStatusReadModel;
    private readonly TimeProvider _timeProvider;

    public InMemoryFolderRepository(
        IFolderLifecycleStatusReadModel? lifecycleReadModel = null,
        IBranchRefPolicyReadModel? branchRefPolicyReadModel = null,
        TimeProvider? timeProvider = null,
        IWorkspaceLockStatusReadModel? workspaceLockStatusReadModel = null,
        IWorkspaceStatusReadModel? workspaceStatusReadModel = null,
        IWorkspaceCleanupStatusReadModel? workspaceCleanupStatusReadModel = null)
    {
        // Lifecycle snapshot writes go through the concrete in-memory read-model. Fail loud
        // if a different IFolderLifecycleStatusReadModel implementation was injected so the
        // wiring drift is visible at startup rather than silently dropping snapshots.
        if (lifecycleReadModel is not null && lifecycleReadModel is not InMemoryFolderLifecycleStatusReadModel)
        {
            throw new ArgumentException(
                $"InMemoryFolderRepository requires {nameof(InMemoryFolderLifecycleStatusReadModel)}; received {lifecycleReadModel.GetType().Name}.",
                nameof(lifecycleReadModel));
        }

        if (branchRefPolicyReadModel is not null && branchRefPolicyReadModel is not InMemoryBranchRefPolicyReadModel)
        {
            throw new ArgumentException(
                $"InMemoryFolderRepository requires {nameof(InMemoryBranchRefPolicyReadModel)}; received {branchRefPolicyReadModel.GetType().Name}.",
                nameof(branchRefPolicyReadModel));
        }

        if (workspaceLockStatusReadModel is not null && workspaceLockStatusReadModel is not InMemoryWorkspaceLockStatusReadModel)
        {
            throw new ArgumentException(
                $"InMemoryFolderRepository requires {nameof(InMemoryWorkspaceLockStatusReadModel)}; received {workspaceLockStatusReadModel.GetType().Name}.",
                nameof(workspaceLockStatusReadModel));
        }

        if (workspaceStatusReadModel is not null && workspaceStatusReadModel is not InMemoryWorkspaceStatusReadModel)
        {
            throw new ArgumentException(
                $"InMemoryFolderRepository requires {nameof(InMemoryWorkspaceStatusReadModel)}; received {workspaceStatusReadModel.GetType().Name}.",
                nameof(workspaceStatusReadModel));
        }

        if (workspaceCleanupStatusReadModel is not null && workspaceCleanupStatusReadModel is not InMemoryWorkspaceCleanupStatusReadModel)
        {
            throw new ArgumentException(
                $"InMemoryFolderRepository requires {nameof(InMemoryWorkspaceCleanupStatusReadModel)}; received {workspaceCleanupStatusReadModel.GetType().Name}.",
                nameof(workspaceCleanupStatusReadModel));
        }

        _branchRefPolicyReadModel = (InMemoryBranchRefPolicyReadModel?)branchRefPolicyReadModel;
        _lifecycleReadModel = (InMemoryFolderLifecycleStatusReadModel?)lifecycleReadModel;
        _workspaceLockStatusReadModel = (InMemoryWorkspaceLockStatusReadModel?)workspaceLockStatusReadModel;
        _workspaceStatusReadModel = (InMemoryWorkspaceStatusReadModel?)workspaceStatusReadModel;
        _workspaceCleanupStatusReadModel = (InMemoryWorkspaceCleanupStatusReadModel?)workspaceCleanupStatusReadModel;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // Internal test affordance: read-only counter visible to test assemblies via
    // InternalsVisibleTo. Production code cannot see this property.
    internal int EventsAppended { get; private set; }

    public FolderStreamName CreateStreamName(string managedTenantId, string folderId)
        => FolderStreamName.Create(managedTenantId, folderId);

    public FolderState Load(FolderStreamName streamName)
    {
        ArgumentNullException.ThrowIfNull(streamName);

        return _states.TryGetValue(streamName.Value, out FolderState? state)
            ? state
            : FolderState.Empty;
    }

    public FolderAppendOutcome AppendIfFingerprintAbsent(
        FolderStreamName streamName,
        string idempotencyKey,
        string fingerprint,
        IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            // Recording an empty-events fingerprint would let a later distinct command with
            // the same idempotency key falsely conflict against a "no-op" ledger entry.
            throw new ArgumentException("Append requires at least one event.", nameof(events));
        }

        // Capture the observation time before taking the lock so it reflects the request's
        // arrival ordering rather than the lock-acquisition ordering. Combined with the
        // per-folder monotonic clamp in SaveLifecycleSnapshot, this keeps the projection's
        // freshness watermark non-decreasing across concurrent writers.
        DateTimeOffset observedAt = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            string ledgerKey = LedgerKey(streamName, idempotencyKey);
            if (_idempotencyFingerprints.TryGetValue(ledgerKey, out string? priorFingerprint))
            {
                return string.Equals(priorFingerprint, fingerprint, StringComparison.Ordinal)
                    ? FolderAppendOutcome.FingerprintMatched
                    : FolderAppendOutcome.FingerprintConflict;
            }

            FolderState current = Load(streamName);
            FolderState next = current.Apply(events, streamName);
            _states[streamName.Value] = next;
            _idempotencyFingerprints[ledgerKey] = fingerprint;
            EventsAppended += events.Count;
            SaveLifecycleSnapshot(next, observedAt);
            SaveBranchRefPolicySnapshot(next, observedAt);
            SaveWorkspaceLockStatusSnapshot(next, observedAt);
            SaveWorkspaceStatusSnapshot(next, observedAt);
            SaveWorkspaceCleanupStatusSnapshot(next, observedAt);
            return FolderAppendOutcome.Appended;
        }
    }

    public FolderIdempotencyLookupResult TryGetIdempotencyFingerprint(
        FolderStreamName streamName,
        string idempotencyKey,
        out string? fingerprint)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return _idempotencyFingerprints.TryGetValue(LedgerKey(streamName, idempotencyKey), out fingerprint)
            ? FolderIdempotencyLookupResult.Found
            : FolderIdempotencyLookupResult.Missing;
    }

    public void Seed(FolderStreamName streamName, IReadOnlyList<IFolderEvent> events)
    {
        ArgumentNullException.ThrowIfNull(streamName);
        ArgumentNullException.ThrowIfNull(events);

        DateTimeOffset observedAt = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            // Symmetric duplicate-seed guard: throw if the stream already has materialized
            // state. Fixture authors get fail-loud feedback on accidental double-seed of the
            // state map (the ledger map below is guarded by the same fail-loud contract).
            if (_states.ContainsKey(streamName.Value))
            {
                throw new InvalidOperationException(
                    $"Seed would overwrite an existing state for stream '{streamName.Value}'. Compose all seeded events into a single Seed call per stream.");
            }

            FolderState seeded = FolderState.Empty.Apply(events, streamName);
            _states[streamName.Value] = seeded;
            foreach (IFolderEvent folderEvent in events)
            {
                if (string.IsNullOrWhiteSpace(folderEvent.IdempotencyKey))
                {
                    continue;
                }

                string ledgerKey = LedgerKey(streamName, folderEvent.IdempotencyKey);
                // Silent overwrite of an existing ledger entry would mask real idempotency
                // races; require seed callers to use unique keys per stream.
                if (_idempotencyFingerprints.ContainsKey(ledgerKey))
                {
                    throw new InvalidOperationException(
                        $"Seed would overwrite an existing idempotency ledger entry for stream '{streamName.Value}' and key '{folderEvent.IdempotencyKey}'.");
                }

                _idempotencyFingerprints[ledgerKey] = folderEvent.IdempotencyFingerprint;
            }

            SaveLifecycleSnapshot(seeded, observedAt);
            SaveBranchRefPolicySnapshot(seeded, observedAt);
            SaveWorkspaceLockStatusSnapshot(seeded, observedAt);
            SaveWorkspaceStatusSnapshot(seeded, observedAt);
            SaveWorkspaceCleanupStatusSnapshot(seeded, observedAt);
        }
    }

    // Internal test affordance — production code cannot see this method (InternalsVisibleTo
    // exposes it only to the test assemblies). Resets the appended-event counter so a test
    // fixture can assert deltas across phases without splitting state between instances.
    internal void ResetAppendCounters() => EventsAppended = 0;

    private static string LedgerKey(FolderStreamName streamName, string idempotencyKey)
        => $"{streamName.Value}|{idempotencyKey}";

    private void SaveLifecycleSnapshot(FolderState state, DateTimeOffset observedAt)
    {
        if (_lifecycleReadModel is null
            || !state.IsCreated
            || string.IsNullOrWhiteSpace(state.ManagedTenantId)
            || string.IsNullOrWhiteSpace(state.FolderId))
        {
            return;
        }

        // For persisted lifecycle and repository-binding transitions, use the event
        // timestamp. Otherwise use the caller-supplied observation time so unbound active
        // snapshots still reflect write time rather than a sentinel epoch zero.
        DateTimeOffset rawObservedAt = state.ArchivedAt
            ?? state.RepositoryBindingUpdatedAt
            ?? observedAt;

        // Clamp per-folder so a slow clock or a test-time fake cannot move the watermark
        // backwards across concurrent writers. The monotonic guarantee is per-stream, not
        // global; that matches the freshness contract consumers rely on.
        string folderKey = LifecycleKey(state.ManagedTenantId, state.FolderId);
        DateTimeOffset clamped = _lastObservedAt.AddOrUpdate(
            folderKey,
            rawObservedAt,
            (_, previous) => previous > rawObservedAt ? previous : rawObservedAt);

        string? evidencePrincipalId = state.LifecycleState == FolderLifecycleState.Archived
            ? state.ArchiveActorPrincipalId
            : state.RepositoryBindingActorPrincipalId ?? state.ArchiveActorPrincipalId;
        string? evidenceTaskId = state.LifecycleState == FolderLifecycleState.Archived
            ? state.ArchiveTaskId
            : state.RepositoryBindingTaskId ?? state.ArchiveTaskId;
        string? evidenceCorrelationId = state.LifecycleState == FolderLifecycleState.Archived
            ? state.ArchiveCorrelationId
            : state.RepositoryBindingCorrelationId ?? state.ArchiveCorrelationId;

        _lifecycleReadModel.Save(new FolderLifecycleStatusReadModelSnapshot(
            state.ManagedTenantId,
            state.FolderId,
            state.LifecycleState == FolderLifecycleState.Archived
                ? FolderLifecycleProjectionState.Archived
                : FolderLifecycleProjectionState.Active,
            MapBindingStatus(state.RepositoryBindingState),
            RepositoryBindingId: state.RepositoryBindingId,
            ProviderBindingRef: state.ProviderBindingRef,
            new FolderLifecycleFreshness("eventually_consistent", clamped, "in-memory-folder-repository", Stale: false, ReasonCode: null),
            new FolderLifecycleEvidenceScope(
                state.ManagedTenantId,
                evidencePrincipalId,
                "read_metadata",
                evidenceTaskId,
                evidenceCorrelationId,
                AuthorizationWatermark: null),
            []));
    }

    private void SaveBranchRefPolicySnapshot(FolderState state, DateTimeOffset observedAt)
    {
        if (_branchRefPolicyReadModel is null
            || !state.IsCreated
            || state.BranchRefPolicy is null
            || string.IsNullOrWhiteSpace(state.ManagedTenantId)
            || string.IsNullOrWhiteSpace(state.FolderId))
        {
            return;
        }

        BranchRefPolicyMetadata policy = state.BranchRefPolicy;
        string folderKey = LifecycleKey(state.ManagedTenantId, state.FolderId);
        DateTimeOffset rawObservedAt = policy.ConfiguredAt > observedAt ? policy.ConfiguredAt : observedAt;
        DateTimeOffset clamped = _lastObservedAt.AddOrUpdate(
            folderKey,
            rawObservedAt,
            (_, previous) => previous > rawObservedAt ? previous : rawObservedAt);

        _branchRefPolicyReadModel.Save(new BranchRefPolicyReadModelSnapshot(
            state.ManagedTenantId,
            state.FolderId,
            policy.RepositoryBindingId,
            policy.PolicyRef,
            policy.DefaultRef,
            policy.AllowedRefPatterns,
            policy.ProtectedRefPatterns,
            new FolderLifecycleFreshness("eventually_consistent", clamped, "in-memory-folder-repository", Stale: false, ReasonCode: null),
            new FolderLifecycleEvidenceScope(
                state.ManagedTenantId,
                policy.ActorPrincipalId,
                "read_branch_ref_policy",
                policy.TaskId,
                policy.CorrelationId,
                AuthorizationWatermark: null)));
    }

    private void SaveWorkspaceLockStatusSnapshot(FolderState state, DateTimeOffset observedAt)
    {
        if (_workspaceLockStatusReadModel is null
            || !state.IsCreated
            || string.IsNullOrWhiteSpace(state.ManagedTenantId)
            || string.IsNullOrWhiteSpace(state.FolderId)
            || string.IsNullOrWhiteSpace(state.WorkspaceId)
            || state.WorkspaceLifecycleState is null)
        {
            return;
        }

        string folderKey = LifecycleKey(state.ManagedTenantId, state.FolderId);
        DateTimeOffset rawObservedAt = state.WorkspaceLifecycleUpdatedAt ?? observedAt;
        DateTimeOffset clamped = _lastObservedAt.AddOrUpdate(
            folderKey,
            rawObservedAt,
            (_, previous) => previous > rawObservedAt ? previous : rawObservedAt);

        _workspaceLockStatusReadModel.Save(new WorkspaceLockStatusReadModelSnapshot(
            state.ManagedTenantId,
            state.FolderId,
            state.WorkspaceId,
            FolderStateTransitions.ToWireName(state.WorkspaceLifecycleState.Value),
            string.IsNullOrWhiteSpace(state.WorkspaceLockId) ? "unlocked" : "locked",
            state.WorkspaceLockId,
            state.WorkspaceLockHolderTaskId,
            state.WorkspaceLockAcquiredAt,
            state.WorkspaceLockEffectiveAt,
            state.WorkspaceLockExpiresAt,
            state.WorkspaceLockRetryEligibilityBasis,
            state.WorkspaceCorrelationId,
            state.WorkspaceTaskId,
            new FolderLifecycleFreshness("read_your_writes", clamped, "in-memory-folder-repository", Stale: false, ReasonCode: null),
            new FolderLifecycleEvidenceScope(
                state.ManagedTenantId,
                state.WorkspaceLockHolderTaskId ?? state.WorkspaceTaskId,
                WorkspaceLockStatusQueryHandler.ActionToken,
                state.WorkspaceTaskId,
                state.WorkspaceCorrelationId,
                AuthorizationWatermark: null)));
    }

    private void SaveWorkspaceStatusSnapshot(FolderState state, DateTimeOffset observedAt)
    {
        if (_workspaceStatusReadModel is null
            || !state.IsCreated
            || string.IsNullOrWhiteSpace(state.ManagedTenantId)
            || string.IsNullOrWhiteSpace(state.FolderId)
            || string.IsNullOrWhiteSpace(state.WorkspaceId)
            || state.WorkspaceLifecycleState is null)
        {
            return;
        }

        string folderKey = LifecycleKey(state.ManagedTenantId, state.FolderId);
        DateTimeOffset rawObservedAt = state.WorkspaceLifecycleUpdatedAt ?? observedAt;
        DateTimeOffset clamped = _lastObservedAt.AddOrUpdate(
            folderKey,
            rawObservedAt,
            (_, previous) => previous > rawObservedAt ? previous : rawObservedAt);

        string currentState = FolderStateTransitions.ToWireName(state.WorkspaceLifecycleState.Value);
        FolderLifecycleFreshness freshness = new(
            "read_your_writes",
            clamped,
            "in-memory-folder-repository",
            Stale: false,
            ReasonCode: null);
        WorkspaceStatusRetryEligibility retryEligibility = RetryEligibilityFor(currentState);
        string operationId = string.IsNullOrWhiteSpace(state.WorkspaceOperationId)
            ? "workspace_status_operation"
            : state.WorkspaceOperationId;
        WorkspaceAcceptedCommandState? acceptedCommandState =
            !string.IsNullOrWhiteSpace(state.WorkspaceTaskId)
            && !string.IsNullOrWhiteSpace(operationId)
            && state.WorkspaceLifecycleUpdatedAt is not null
                ? new(state.WorkspaceTaskId, operationId, AcceptedStateFor(currentState), state.WorkspaceLifecycleUpdatedAt.Value)
                : null;

        _workspaceStatusReadModel.Save(new WorkspaceStatusReadModelSnapshot(
            state.ManagedTenantId,
            state.FolderId,
            state.WorkspaceId,
            currentState,
            acceptedCommandState,
            new WorkspaceProjectedState(currentState, "projection", clamped),
            new WorkspaceProviderOutcome(
                operationId,
                ProviderOutcomeStateFor(currentState),
                ProviderOutcomeCategoryFor(currentState, state.WorkspaceCommitOutcomeCategory ?? state.RepositoryBindingFailureCategory),
                "provref_workspace_status",
                retryEligibility,
                null,
                freshness),
            retryEligibility,
            null,
            freshness,
            new WorkspaceProjectionLag(0, "projection"),
            LastFailureCategoryFor(currentState, state.WorkspaceCommitFailureCategory ?? state.RepositoryBindingFailureCategory),
            new FolderLifecycleEvidenceScope(
                state.ManagedTenantId,
                state.WorkspaceLockHolderTaskId ?? state.WorkspaceTaskId,
                WorkspaceStatusQueryHandler.ActionToken,
                state.WorkspaceTaskId,
                state.WorkspaceCorrelationId,
                AuthorizationWatermark: null)));
    }

    private void SaveWorkspaceCleanupStatusSnapshot(FolderState state, DateTimeOffset observedAt)
    {
        if (_workspaceCleanupStatusReadModel is null
            || !state.IsCreated
            || string.IsNullOrWhiteSpace(state.ManagedTenantId)
            || string.IsNullOrWhiteSpace(state.FolderId)
            || string.IsNullOrWhiteSpace(state.WorkspaceId)
            || state.WorkspaceLifecycleState is null)
        {
            return;
        }

        string folderKey = LifecycleKey(state.ManagedTenantId, state.FolderId);
        DateTimeOffset rawObservedAt = state.WorkspaceLifecycleUpdatedAt ?? observedAt;
        DateTimeOffset clamped = _lastObservedAt.AddOrUpdate(
            folderKey,
            rawObservedAt,
            (_, previous) => previous > rawObservedAt ? previous : rawObservedAt);

        string currentState = FolderStateTransitions.ToWireName(state.WorkspaceLifecycleState.Value);
        (string cleanupStatus, string reasonCode, WorkspaceStatusRetryEligibility retryEligibility) = CleanupVisibilityFor(currentState);
        FolderLifecycleFreshness freshness = new(
            "read_your_writes",
            clamped,
            "in-memory-folder-repository",
            Stale: false,
            ReasonCode: null);

        _workspaceCleanupStatusReadModel.Save(new WorkspaceCleanupStatusReadModelSnapshot(
            state.ManagedTenantId,
            state.FolderId,
            state.WorkspaceId,
            state.WorkspaceTaskId,
            cleanupStatus,
            reasonCode,
            retryEligibility,
            freshness,
            state.WorkspaceCorrelationId,
            clamped,
            currentState is "requested" or "preparing" ? null : clamped,
            new FolderLifecycleEvidenceScope(
                state.ManagedTenantId,
                state.WorkspaceLockHolderTaskId ?? state.WorkspaceTaskId,
                WorkspaceCleanupStatusQueryHandler.ActionToken,
                state.WorkspaceTaskId,
                state.WorkspaceCorrelationId,
                AuthorizationWatermark: null)));
    }

    private static string LifecycleKey(string managedTenantId, string folderId)
        => $"{managedTenantId}|{folderId}";

    private static FolderRepositoryBindingStatus MapBindingStatus(FolderRepositoryBindingState? state)
        => state switch
        {
            null or FolderRepositoryBindingState.Unbound => FolderRepositoryBindingStatus.Unbound,
            FolderRepositoryBindingState.BindingRequested => FolderRepositoryBindingStatus.BindingRequested,
            FolderRepositoryBindingState.Bound => FolderRepositoryBindingStatus.Bound,
            FolderRepositoryBindingState.Failed => FolderRepositoryBindingStatus.Failed,
            FolderRepositoryBindingState.UnknownProviderOutcome => FolderRepositoryBindingStatus.UnknownProviderOutcome,
            FolderRepositoryBindingState.ReconciliationRequired => FolderRepositoryBindingStatus.ReconciliationRequired,
            _ => FolderRepositoryBindingStatus.Unknown,
        };

    private static WorkspaceStatusRetryEligibility RetryEligibilityFor(string currentState)
        => currentState switch
        {
            "dirty" => new(true, "dirty_workspace"),
            "failed" => new(true, "failed_operation"),
            "unknown_provider_outcome" => new(true, "unknown_provider_outcome"),
            "reconciliation_required" => new(true, "reconciliation_required"),
            "locked" => new(false, "workspace_locked"),
            "inaccessible" => new(false, "tenant_access_denied"),
            _ => new(false, "retry_not_required"),
        };

    private static (string Status, string ReasonCode, WorkspaceStatusRetryEligibility RetryEligibility) CleanupVisibilityFor(string currentState)
        => currentState switch
        {
            "requested" or "preparing" => ("pending", "workspace_lifecycle_in_progress", new(false, "cleanup_not_applicable")),
            "committed" => ("succeeded", "workspace_committed", new(false, "retry_not_required")),
            "failed" => ("failed", "failed_operation", new(true, "failed_operation")),
            "dirty" => ("status_only", "dirty_workspace", new(true, "dirty_workspace")),
            "locked" => ("status_only", "workspace_locked", new(false, "workspace_locked")),
            "changes_staged" => ("status_only", "changes_staged", new(false, "cleanup_not_applicable")),
            "unknown_provider_outcome" => ("status_only", "unknown_provider_outcome", new(true, "unknown_provider_outcome")),
            "reconciliation_required" => ("status_only", "reconciliation_required", new(true, "reconciliation_required")),
            "inaccessible" => ("status_only", "tenant_access_denied", new(false, "tenant_access_denied")),
            _ => ("status_only", "cleanup_status_only", new(false, "cleanup_not_applicable")),
        };

    private static string AcceptedStateFor(string currentState)
        => currentState is "failed" or "inaccessible" ? "failed" : currentState == "committed" ? "completed" : "accepted";

    private static string ProviderOutcomeStateFor(string currentState)
        => currentState switch
        {
            "requested" or "preparing" => "pending",
            "failed" or "inaccessible" => "known_failure",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            _ => "known_success",
        };

    private static string ProviderOutcomeCategoryFor(string currentState, string? failureCategory)
        => currentState switch
        {
            "failed" => string.IsNullOrWhiteSpace(failureCategory) ? "failed_operation" : failureCategory,
            "inaccessible" => "tenant_access_denied",
            "unknown_provider_outcome" => "unknown_provider_outcome",
            "reconciliation_required" => "reconciliation_required",
            _ => "success",
        };

    private static string? LastFailureCategoryFor(string currentState, string? failureCategory)
        => currentState is "failed" or "inaccessible" or "unknown_provider_outcome" or "reconciliation_required"
            ? ProviderOutcomeCategoryFor(currentState, failureCategory)
            : null;
}
