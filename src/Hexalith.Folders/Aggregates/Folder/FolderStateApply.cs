using System.Collections.Frozen;

namespace Hexalith.Folders.Aggregates.Folder;

public static class FolderStateApply
{
    // The expected stream name is the authoritative identity loaded by the caller.
    // Apply enforces that every event matches it, including the very first event, so a
    // misrouted event cannot poison `state` before the `IsCreated` guard would have fired.
    public static FolderState Apply(FolderState state, IFolderEvent folderEvent, FolderStreamName expectedStreamName)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(folderEvent);
        ArgumentNullException.ThrowIfNull(expectedStreamName);

        FolderStreamName actual = FolderStreamName.Create(folderEvent.ManagedTenantId, folderEvent.FolderId);
        if (!string.Equals(actual.Value, expectedStreamName.Value, StringComparison.Ordinal))
        {
            // Exception message uses stable identifiers only (no event payload echo) to avoid
            // turning a corrupt-stream signal into a log-injection vector.
            throw new InvalidOperationException(
                $"Foreign folder event in Apply: result code {FolderResultCode.TenantMismatch}.");
        }

        // Dedupe identical replays: if the same idempotency key and fingerprint are already
        // recorded in state, the event has already been applied and any state changes have
        // already taken effect. Skipping prevents silent reapplication of state mutations.
        if (!string.IsNullOrWhiteSpace(folderEvent.IdempotencyKey)
            && state.IdempotencyFingerprints.TryGetValue(folderEvent.IdempotencyKey, out string? existingFingerprint)
            && string.Equals(existingFingerprint, folderEvent.IdempotencyFingerprint, StringComparison.Ordinal))
        {
            return state;
        }

        return folderEvent switch
        {
            FolderCreated created => state with
            {
                IsCreated = true,
                ManagedTenantId = created.ManagedTenantId,
                OrganizationId = created.OrganizationId,
                FolderId = created.FolderId,
                DisplayName = created.DisplayName,
                Description = created.Description,
                PathLabel = created.PathLabel,
                Tags = CanonicalizeTags(created.Tags),
                LifecycleState = created.LifecycleState,
                RepositoryBindingState = created.RepositoryBindingState,
                WorkspaceLifecycleState = null,
                WorkspaceOperatorDisposition = null,
                WorkspaceId = null,
                WorkspacePolicyRef = null,
                WorkspaceLifecycleEvent = null,
                WorkspaceOperationId = null,
                WorkspaceCorrelationId = null,
                WorkspaceTaskId = null,
                WorkspaceLifecycleUpdatedAt = null,
                WorkspaceLockId = null,
                WorkspaceLockIntent = null,
                WorkspaceLockRequestedLeaseSeconds = null,
                WorkspaceLockHolderTaskId = null,
                WorkspaceLockAcquiredAt = null,
                WorkspaceLockEffectiveAt = null,
                WorkspaceLockExpiresAt = null,
                WorkspaceLockRetryEligibilityBasis = null,
                RepositoryBindingId = null,
                ProviderBindingRef = null,
                RepositoryProfileRef = null,
                ExternalRepositoryRefFingerprint = null,
                BranchRefPolicyRef = null,
                BranchRefPolicy = null,
                RepositoryBindingFailureCategory = null,
                RepositoryBindingOutcomeCategory = null,
                RepositoryBindingUpdatedAt = null,
                RepositoryBindingActorPrincipalId = null,
                RepositoryBindingCorrelationId = null,
                RepositoryBindingTaskId = null,
                RepositoryBindingIdempotencyKey = null,
                RepositoryBindingIdempotencyFingerprint = null,
                WorkspaceCommitReference = null,
                WorkspaceCommitFailureCategory = null,
                WorkspaceCommitOutcomeCategory = null,
                WorkspaceCommitAuthorMetadataReference = null,
                WorkspaceCommitBranchRefTarget = null,
                WorkspaceCommitMessageClassification = null,
                WorkspaceCommitChangedPathMetadataDigest = null,
                WorkspaceCommitReconciliationReference = null,
                WorkspaceCommitReconciliationRequired = false,
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            FolderAccessGranted granted => state with
            {
                AccessOverrides = RecordGrant(state.AccessOverrides, granted),
                AccessSequence = AdvanceWatermark(state.AccessSequence, granted.AccessSequence),
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            FolderAccessRevoked revoked => state with
            {
                AccessOverrides = RecordRevoke(state.AccessOverrides, revoked),
                AccessSequence = AdvanceWatermark(state.AccessSequence, revoked.AccessSequence),
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            FolderArchived archived => state with
            {
                LifecycleState = FolderLifecycleState.Archived,
                ArchiveReasonCode = archived.ArchiveReasonCode,
                ArchiveActorPrincipalId = archived.ActorPrincipalId,
                ArchiveCorrelationId = archived.CorrelationId,
                ArchiveTaskId = archived.TaskId,
                ArchiveIdempotencyKey = archived.IdempotencyKey,
                ArchivedAt = archived.OccurredAt,
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            RepositoryBindingRequested requested => ApplyRepositoryBindingRequested(state, requested),
            ExistingRepositoryBindingRequested requested => ApplyExistingRepositoryBindingRequested(state, requested),
            RepositoryBound bound => ApplyRepositoryBound(state, bound),
            BranchRefPolicyConfigured configured => state with
            {
                BranchRefPolicy = new BranchRefPolicyMetadata(
                    configured.RepositoryBindingId,
                    configured.PolicyRef,
                    configured.DefaultRef,
                    configured.AllowedRefPatterns.ToArray(),
                    configured.ProtectedRefPatterns.ToArray(),
                    configured.ActorPrincipalId,
                    configured.CorrelationId,
                    configured.TaskId,
                    configured.IdempotencyKey,
                    configured.IdempotencyFingerprint,
                    configured.OccurredAt),
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            RepositoryBindingFailed failed => ApplyRepositoryBindingFailed(state, failed),
            ProviderOutcomeUnknown unknown => ApplyProviderOutcomeUnknown(state, unknown),
            WorkspacePreparationRequested requested => ApplyWorkspacePreparationRequested(state, requested),
            FolderWorkspaceLifecycleEventRecorded recorded => ApplyWorkspaceLifecycleEvent(state, recorded),
            WorkspaceLockAcquired acquired => ApplyWorkspaceLockAcquired(state, acquired),
            WorkspaceLockReleased released => ApplyWorkspaceLockReleased(state, released),
            WorkspaceFileMutationAccepted accepted => ApplyWorkspaceFileMutationAccepted(state, accepted),
            WorkspaceCommitSucceeded succeeded => ApplyWorkspaceCommitSucceeded(state, succeeded),
            WorkspaceCommitFailed failed => ApplyWorkspaceCommitFailed(state, failed),
            WorkspaceCommitOutcomeUnknown unknown => ApplyWorkspaceCommitOutcomeUnknown(state, unknown),
            // Unknown event types fail loudly. Silently no-op'ing would let a future event
            // type poison the idempotency ledger on cold replay against an older code path.
            _ => throw new InvalidOperationException(
                $"Unhandled folder event type: result code {FolderResultCode.StateTransitionInvalid}."),
        };
    }

    private static FolderState ApplyRepositoryBindingRequested(FolderState state, RepositoryBindingRequested requested)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            FolderWorkspaceLifecycleEvent.RepositoryBindingRequested);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, requested, transition, null, requested.RepositoryBindingId) with
        {
            RepositoryBindingState = FolderRepositoryBindingState.BindingRequested,
            RepositoryBindingId = requested.RepositoryBindingId,
            ProviderBindingRef = requested.ProviderBindingRef,
            RepositoryProfileRef = requested.RepositoryProfileRef,
            ExternalRepositoryRefFingerprint = null,
            BranchRefPolicyRef = requested.BranchRefPolicyRef,
            BranchRefPolicy = null,
            RepositoryBindingFailureCategory = null,
            RepositoryBindingOutcomeCategory = null,
            RepositoryBindingUpdatedAt = requested.OccurredAt,
            RepositoryBindingActorPrincipalId = requested.ActorPrincipalId,
            RepositoryBindingCorrelationId = requested.CorrelationId,
            RepositoryBindingTaskId = requested.TaskId,
            RepositoryBindingIdempotencyKey = requested.IdempotencyKey,
            RepositoryBindingIdempotencyFingerprint = requested.IdempotencyFingerprint,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, requested),
        };
    }

    private static FolderState ApplyExistingRepositoryBindingRequested(
        FolderState state,
        ExistingRepositoryBindingRequested requested)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            FolderWorkspaceLifecycleEvent.RepositoryBindingRequested);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, requested, transition, null, requested.RepositoryBindingId) with
        {
            RepositoryBindingState = FolderRepositoryBindingState.BindingRequested,
            RepositoryBindingId = requested.RepositoryBindingId,
            ProviderBindingRef = requested.ProviderBindingRef,
            RepositoryProfileRef = null,
            ExternalRepositoryRefFingerprint = requested.ExternalRepositoryRefFingerprint,
            BranchRefPolicyRef = requested.BranchRefPolicyRef,
            BranchRefPolicy = null,
            RepositoryBindingFailureCategory = null,
            RepositoryBindingOutcomeCategory = null,
            RepositoryBindingUpdatedAt = requested.OccurredAt,
            RepositoryBindingActorPrincipalId = requested.ActorPrincipalId,
            RepositoryBindingCorrelationId = requested.CorrelationId,
            RepositoryBindingTaskId = requested.TaskId,
            RepositoryBindingIdempotencyKey = requested.IdempotencyKey,
            RepositoryBindingIdempotencyFingerprint = requested.IdempotencyFingerprint,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, requested),
        };
    }

    private static FolderState ApplyRepositoryBound(FolderState state, RepositoryBound bound)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            FolderWorkspaceLifecycleEvent.RepositoryBound);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, bound, transition, state.WorkspaceId, bound.RepositoryBindingId) with
        {
            RepositoryBindingState = FolderRepositoryBindingState.Bound,
            RepositoryBindingId = bound.RepositoryBindingId,
            ProviderBindingRef = bound.ProviderBindingRef,
            RepositoryBindingFailureCategory = null,
            RepositoryBindingOutcomeCategory = null,
            RepositoryBindingUpdatedAt = bound.OccurredAt,
            RepositoryBindingCorrelationId = bound.CorrelationId,
            RepositoryBindingTaskId = bound.TaskId,
            RepositoryBindingIdempotencyKey = bound.IdempotencyKey,
            RepositoryBindingIdempotencyFingerprint = bound.IdempotencyFingerprint,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, bound),
        };
    }

    private static FolderState ApplyRepositoryBindingFailed(FolderState state, RepositoryBindingFailed failed)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            FolderWorkspaceLifecycleEvent.RepositoryBindingFailed);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, failed, transition, state.WorkspaceId, failed.RepositoryBindingId) with
        {
            RepositoryBindingState = FolderRepositoryBindingState.Failed,
            RepositoryBindingId = failed.RepositoryBindingId,
            ProviderBindingRef = failed.ProviderBindingRef,
            RepositoryBindingFailureCategory = failed.FailureCategory,
            RepositoryBindingOutcomeCategory = null,
            RepositoryBindingUpdatedAt = failed.OccurredAt,
            RepositoryBindingCorrelationId = failed.CorrelationId,
            RepositoryBindingTaskId = failed.TaskId,
            RepositoryBindingIdempotencyKey = failed.IdempotencyKey,
            RepositoryBindingIdempotencyFingerprint = failed.IdempotencyFingerprint,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, failed),
        };
    }

    private static FolderState ApplyProviderOutcomeUnknown(FolderState state, ProviderOutcomeUnknown unknown)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            FolderWorkspaceLifecycleEvent.ProviderOutcomeUnknown);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, unknown, transition, state.WorkspaceId, unknown.RepositoryBindingId) with
        {
            RepositoryBindingState = unknown.ReconciliationRequired
                ? FolderRepositoryBindingState.ReconciliationRequired
                : FolderRepositoryBindingState.UnknownProviderOutcome,
            RepositoryBindingId = unknown.RepositoryBindingId,
            ProviderBindingRef = unknown.ProviderBindingRef,
            RepositoryBindingFailureCategory = null,
            RepositoryBindingOutcomeCategory = unknown.OutcomeCategory,
            RepositoryBindingUpdatedAt = unknown.OccurredAt,
            RepositoryBindingCorrelationId = unknown.CorrelationId,
            RepositoryBindingTaskId = unknown.TaskId,
            RepositoryBindingIdempotencyKey = unknown.IdempotencyKey,
            RepositoryBindingIdempotencyFingerprint = unknown.IdempotencyFingerprint,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, unknown),
        };
    }

    private static FolderState ApplyWorkspaceLifecycleEvent(
        FolderState state,
        FolderWorkspaceLifecycleEventRecorded recorded)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            recorded.WorkspaceLifecycleEvent,
            recorded.DirtyResolution);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, recorded, transition, recorded.WorkspaceId, recorded.OperationId) with
        {
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, recorded),
        };
    }

    private static FolderState ApplyWorkspaceLockAcquired(FolderState state, WorkspaceLockAcquired acquired)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            acquired.WorkspaceLifecycleEvent);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, acquired, transition, acquired.WorkspaceId, acquired.LockId) with
        {
            WorkspaceLockId = acquired.LockId,
            WorkspaceLockIntent = acquired.LockIntent,
            WorkspaceLockRequestedLeaseSeconds = acquired.RequestedLeaseSeconds,
            WorkspaceLockHolderTaskId = acquired.HolderTaskId,
            WorkspaceLockAcquiredAt = acquired.AcquiredAt,
            WorkspaceLockEffectiveAt = acquired.EffectiveAt,
            WorkspaceLockExpiresAt = acquired.ExpiresAt,
            WorkspaceLockRetryEligibilityBasis = acquired.RetryEligibilityBasis,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, acquired),
        };
    }

    private static FolderState ApplyWorkspaceLockReleased(FolderState state, WorkspaceLockReleased released)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            released.WorkspaceLifecycleEvent);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, released, transition, released.WorkspaceId, released.LockId) with
        {
            WorkspaceLockId = null,
            WorkspaceLockIntent = null,
            WorkspaceLockRequestedLeaseSeconds = null,
            WorkspaceLockHolderTaskId = null,
            WorkspaceLockAcquiredAt = null,
            WorkspaceLockEffectiveAt = null,
            WorkspaceLockExpiresAt = null,
            WorkspaceLockRetryEligibilityBasis = null,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, released),
        };
    }

    private static FolderState ApplyWorkspaceFileMutationAccepted(FolderState state, WorkspaceFileMutationAccepted accepted)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            accepted.WorkspaceLifecycleEvent);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, accepted, transition, accepted.WorkspaceId, accepted.OperationId) with
        {
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, accepted),
        };
    }

    private static FolderState ApplyWorkspaceCommitSucceeded(FolderState state, WorkspaceCommitSucceeded succeeded)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            succeeded.WorkspaceLifecycleEvent);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, succeeded, transition, succeeded.WorkspaceId, succeeded.OperationId) with
        {
            WorkspaceCommitReference = succeeded.CommitReference,
            WorkspaceCommitFailureCategory = null,
            WorkspaceCommitOutcomeCategory = succeeded.ProviderOutcomeCategory,
            WorkspaceCommitAuthorMetadataReference = succeeded.AuthorMetadataReference,
            WorkspaceCommitBranchRefTarget = succeeded.BranchRefTarget,
            WorkspaceCommitMessageClassification = succeeded.CommitMessageClassification,
            WorkspaceCommitChangedPathMetadataDigest = succeeded.ChangedPathMetadataDigest,
            WorkspaceCommitReconciliationReference = null,
            WorkspaceCommitReconciliationRequired = false,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, succeeded),
        };
    }

    private static FolderState ApplyWorkspaceCommitFailed(FolderState state, WorkspaceCommitFailed failed)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            failed.WorkspaceLifecycleEvent);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, failed, transition, failed.WorkspaceId, failed.OperationId) with
        {
            WorkspaceCommitReference = null,
            WorkspaceCommitFailureCategory = failed.FailureCategory,
            WorkspaceCommitOutcomeCategory = failed.ProviderOutcomeCategory,
            WorkspaceCommitAuthorMetadataReference = failed.AuthorMetadataReference,
            WorkspaceCommitBranchRefTarget = failed.BranchRefTarget,
            WorkspaceCommitMessageClassification = failed.CommitMessageClassification,
            WorkspaceCommitChangedPathMetadataDigest = failed.ChangedPathMetadataDigest,
            WorkspaceCommitReconciliationReference = null,
            WorkspaceCommitReconciliationRequired = false,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, failed),
        };
    }

    private static FolderState ApplyWorkspaceCommitOutcomeUnknown(FolderState state, WorkspaceCommitOutcomeUnknown unknown)
    {
        FolderWorkspaceTransitionResult transition = FolderStateTransitions.Transition(
            state.WorkspaceLifecycleState,
            unknown.WorkspaceLifecycleEvent);
        if (!transition.IsAccepted)
        {
            return state;
        }

        return WithWorkspaceTransition(state, unknown, transition, unknown.WorkspaceId, unknown.OperationId) with
        {
            WorkspaceCommitReference = null,
            WorkspaceCommitFailureCategory = null,
            WorkspaceCommitOutcomeCategory = unknown.ProviderOutcomeCategory,
            WorkspaceCommitAuthorMetadataReference = unknown.AuthorMetadataReference,
            WorkspaceCommitBranchRefTarget = unknown.BranchRefTarget,
            WorkspaceCommitMessageClassification = unknown.CommitMessageClassification,
            WorkspaceCommitChangedPathMetadataDigest = unknown.ChangedPathMetadataDigest,
            WorkspaceCommitReconciliationReference = unknown.ReconciliationReference,
            WorkspaceCommitReconciliationRequired = unknown.ReconciliationRequired,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, unknown),
        };
    }

    private static FolderState ApplyWorkspacePreparationRequested(
        FolderState state,
        WorkspacePreparationRequested requested)
        => state with
        {
            WorkspaceId = requested.WorkspaceId,
            WorkspacePolicyRef = requested.WorkspacePolicyRef,
            WorkspaceOperationId = requested.WorkspaceId,
            WorkspaceCorrelationId = requested.CorrelationId,
            WorkspaceTaskId = requested.TaskId,
            WorkspaceLifecycleUpdatedAt = requested.OccurredAt,
            IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, requested),
        };

    private static FolderState WithWorkspaceTransition(
        FolderState state,
        IFolderEvent folderEvent,
        FolderWorkspaceTransitionResult transition,
        string? workspaceId,
        string? operationId)
        => state with
        {
            WorkspaceLifecycleState = transition.NextState,
            WorkspaceOperatorDisposition = transition.OperatorDisposition,
            WorkspaceId = string.IsNullOrWhiteSpace(workspaceId) ? state.WorkspaceId : workspaceId,
            WorkspaceLifecycleEvent = transition.AttemptedEvent,
            WorkspaceOperationId = string.IsNullOrWhiteSpace(operationId) ? state.WorkspaceOperationId : operationId,
            WorkspaceCorrelationId = folderEvent.CorrelationId,
            WorkspaceTaskId = folderEvent.TaskId,
            WorkspaceLifecycleUpdatedAt = folderEvent.OccurredAt,
        };

    private static IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> RecordGrant(
        IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> current,
        FolderAccessGranted granted)
    {
        FolderAccessEntryKey key = new(
            granted.ManagedTenantId,
            granted.FolderId,
            granted.PrincipalKind,
            granted.PrincipalId,
            granted.Action);

        // Replace the override only if the new event is at least as recent as the recorded
        // sequence. Stale or out-of-order events keep the existing override intact rather
        // than letting a smaller sequence regress per-override metadata.
        if (current.TryGetValue(key, out FolderAccessOverride? existing)
            && granted.AccessSequence < existing.AccessSequence)
        {
            return current;
        }

        // Preserve prior revocation history across a grant→revoke→grant cycle so C7
        // freshness checks can still see that this tuple was revoked at a known sequence.
        IReadOnlyList<FolderAccessRevocationRecord> history = existing?.RevocationHistory ?? [];

        return ReplaceOverride(
            current,
            key,
            new FolderAccessOverride(
                key,
                IsGranted: true,
                granted.AccessSequence,
                granted.OccurredAt,
                OperationIntent: "grant",
                granted.ActorPrincipalId,
                granted.CorrelationId,
                granted.TaskId,
                granted.IdempotencyKey,
                history));
    }

    private static IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> RecordRevoke(
        IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> current,
        FolderAccessRevoked revoked)
    {
        FolderAccessEntryKey key = new(
            revoked.ManagedTenantId,
            revoked.FolderId,
            revoked.PrincipalKind,
            revoked.PrincipalId,
            revoked.Action);

        if (current.TryGetValue(key, out FolderAccessOverride? existing)
            && revoked.AccessSequence < existing.AccessSequence)
        {
            return current;
        }

        FolderAccessRevocationRecord record = new(
            revoked.AccessSequence,
            revoked.OccurredAt,
            revoked.ActorPrincipalId,
            revoked.CorrelationId,
            revoked.TaskId,
            revoked.IdempotencyKey);

        IReadOnlyList<FolderAccessRevocationRecord> history = existing is null
            ? [record]
            : [.. existing.RevocationHistory, record];

        return ReplaceOverride(
            current,
            key,
            new FolderAccessOverride(
                key,
                IsGranted: false,
                revoked.AccessSequence,
                revoked.OccurredAt,
                OperationIntent: "revoke",
                revoked.ActorPrincipalId,
                revoked.CorrelationId,
                revoked.TaskId,
                revoked.IdempotencyKey,
                history));
    }

    // The state record exposes a frozen dictionary, so each event has to produce a fresh
    // frozen snapshot. We accept the freeze cost per event because each transition is the
    // unit of observable consistency; callers replay events one-at-a-time through Apply.
    private static IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> ReplaceOverride(
        IReadOnlyDictionary<FolderAccessEntryKey, FolderAccessOverride> current,
        FolderAccessEntryKey key,
        FolderAccessOverride @override)
    {
        Dictionary<FolderAccessEntryKey, FolderAccessOverride> next = new(current)
        {
            [key] = @override,
        };
        return next.ToFrozenDictionary();
    }

    private static long AdvanceWatermark(long current, long candidate)
        => candidate > current ? candidate : current;

    private static IReadOnlyDictionary<string, string> RecordIdempotency(
        IReadOnlyDictionary<string, string> current,
        IFolderEvent folderEvent)
    {
        if (string.IsNullOrWhiteSpace(folderEvent.IdempotencyKey))
        {
            return current;
        }

        Dictionary<string, string> next = new(current, StringComparer.Ordinal)
        {
            [folderEvent.IdempotencyKey] = folderEvent.IdempotencyFingerprint,
        };
        return next.ToFrozenDictionary(StringComparer.Ordinal);
    }

    // Re-canonicalize on apply so events produced by a future buggy writer with mixed-case,
    // unsorted, or duplicate tags do not propagate that drift into state.
    private static IReadOnlyList<string> CanonicalizeTags(IReadOnlyList<string> tags)
        => tags
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
