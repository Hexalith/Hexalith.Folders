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
                IdempotencyFingerprints = RecordIdempotency(state.IdempotencyFingerprints, folderEvent),
            },
            // Unknown event types fail loudly. Silently no-op'ing would let a future event
            // type poison the idempotency ledger on cold replay against an older code path.
            _ => throw new InvalidOperationException(
                $"Unhandled folder event type: result code {FolderResultCode.StateTransitionInvalid}."),
        };
    }

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
