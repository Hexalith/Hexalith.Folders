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
                RepositoryBindingId = null,
                ProviderBindingRef = null,
                RepositoryProfileRef = null,
                BranchRefPolicyRef = null,
                RepositoryBindingFailureCategory = null,
                RepositoryBindingOutcomeCategory = null,
                RepositoryBindingUpdatedAt = null,
                RepositoryBindingActorPrincipalId = null,
                RepositoryBindingCorrelationId = null,
                RepositoryBindingTaskId = null,
                RepositoryBindingIdempotencyKey = null,
                RepositoryBindingIdempotencyFingerprint = null,
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
            RepositoryBindingRequested requested => state with
            {
                RepositoryBindingState = FolderRepositoryBindingState.BindingRequested,
                RepositoryBindingId = requested.RepositoryBindingId,
                ProviderBindingRef = requested.ProviderBindingRef,
                RepositoryProfileRef = requested.RepositoryProfileRef,
                BranchRefPolicyRef = requested.BranchRefPolicyRef,
                RepositoryBindingFailureCategory = null,
                RepositoryBindingOutcomeCategory = null,
                RepositoryBindingUpdatedAt = requested.OccurredAt,
                RepositoryBindingActorPrincipalId = requested.ActorPrincipalId,
                RepositoryBindingCorrelationId = requested.CorrelationId,
                RepositoryBindingTaskId = requested.TaskId,
                RepositoryBindingIdempotencyKey = requested.IdempotencyKey,
                RepositoryBindingIdempotencyFingerprint = requested.IdempotencyFingerprint,
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            RepositoryBound bound => state with
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
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            RepositoryBindingFailed failed => state with
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
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            ProviderOutcomeUnknown unknown => state with
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
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            // Unknown event types fail loudly. Silently no-op'ing would let a future event
            // type poison the idempotency ledger on cold replay against an older code path.
            _ => throw new InvalidOperationException(
                $"Unhandled folder event type: result code {FolderResultCode.StateTransitionInvalid}."),
        };
    }

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
