using System.Collections.Frozen;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;

namespace Hexalith.Folders.Projections.SemanticIndexing;

public sealed record SemanticIndexingBridgeProjection
{
    private SemanticIndexingBridgeProjection(IReadOnlyDictionary<string, SemanticIndexingBridgeEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyDictionary<string, SemanticIndexingBridgeEntry> Entries { get; }

    public static SemanticIndexingBridgeProjection Empty { get; } = new(FrozenDictionary<string, SemanticIndexingBridgeEntry>.Empty);

    public static SemanticIndexingBridgeProjection FromEntries(IEnumerable<SemanticIndexingBridgeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new SemanticIndexingBridgeProjection(entries
            .Where(static entry => entry is not null)
            .ToFrozenDictionary(static entry => entry.Identity.ReadModelKey, StringComparer.Ordinal));
    }

    public SemanticIndexingBridgeProjection Apply(IEnumerable<FolderProjectionEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(envelopes);

        Dictionary<string, SemanticIndexingBridgeEntry> entries = new(Entries, StringComparer.Ordinal);
        IEnumerable<FolderProjectionEnvelope> ordered = envelopes
            .Where(static envelope => envelope is not null)
            .Where(static envelope => envelope.Event is not null)
            .OrderBy(static envelope => envelope.Sequence)
            .ThenBy(static envelope => EventIdempotencyKey(envelope.Event), StringComparer.Ordinal)
            .ThenBy(static envelope => EventFingerprint(envelope.Event), StringComparer.Ordinal);

        foreach (FolderProjectionEnvelope envelope in ordered)
        {
            if (!string.Equals(envelope.ManagedTenantId, envelope.Event.ManagedTenantId, StringComparison.Ordinal))
            {
                continue;
            }

            switch (envelope.Event)
            {
                case WorkspaceFileMutationAccepted accepted:
                    ApplyFileMutation(entries, envelope, accepted);
                    break;
                case WorkspaceCommitSucceeded succeeded:
                    ApplyCommitSucceeded(entries, envelope, succeeded);
                    break;
                case WorkspaceCommitFailed failed:
                    ApplyCommitFailed(entries, envelope, failed);
                    break;
                case FolderArchived archived:
                    ApplyFolderArchived(entries, envelope, archived);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"SemanticIndexingBridgeProjection received unsupported event type '{envelope.Event.GetType().FullName}' at sequence {envelope.Sequence}.");
            }
        }

        return new SemanticIndexingBridgeProjection(entries.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public SemanticIndexingBridgeEntry? Get(string key)
        => Entries.TryGetValue(key, out SemanticIndexingBridgeEntry? entry) ? entry : null;

    public static SemanticIndexingBridgeEntry ApplyIndexingResult(SemanticIndexingBridgeEntry current, SemanticIndexingResultUpdate update)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(update);

        if (!IdentityMatches(current.Identity, update.Identity))
        {
            return current;
        }

        if (current.Status is SemanticIndexingBridgeStatus.Tombstoned)
        {
            return current;
        }

        return current with
        {
            Status = update.Status,
            StatusCode = update.Status.ToStatusCode(),
            ReasonCode = update.ReasonCode,
            Retryable = update.Retryable,
            CorrelationId = update.CorrelationId,
            TaskId = update.TaskId,
            StatusObservedAt = update.ObservedAt,
            Evidence = current.Evidence with
            {
                PublishedEventId = update.PublishedEventId,
                ResultFingerprint = update.ResultFingerprint,

                // Retain the exact published document so a later archive soft-delete can re-send it (the Memories
                // upsert is a destructive full-field overwrite); preserve any prior value when this update carries none.
                IndexedText = update.IndexedText ?? current.Evidence.IndexedText,
                IndexedAttributes = update.IndexedAttributes ?? current.Evidence.IndexedAttributes,
            },
        };
    }

    /// <summary>
    /// Records a removal/archive egress outcome against a <c>Tombstoned</c> entry. Evidence-only: the status is never
    /// changed (no Tombstoned -&gt; Indexed regression) and the update is ignored when its watermark is older than the
    /// current entry's freshness watermark, so a stale/out-of-order removal cannot overwrite a newer state (AC6).
    /// </summary>
    public static SemanticIndexingBridgeEntry ApplyRemovalEvidence(SemanticIndexingBridgeEntry current, SemanticIndexingRemovalEvidenceUpdate update)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(update);

        if (!IdentityMatches(current.Identity, update.Identity))
        {
            return current;
        }

        // The evidence-only path applies only to tombstoned entries and must never resurrect a non-tombstoned one.
        if (current.Status is not SemanticIndexingBridgeStatus.Tombstoned)
        {
            return current;
        }

        if (update.Watermark < current.Freshness.Watermark)
        {
            return current;
        }

        return current with
        {
            ReasonCode = update.ReasonCode,
            Retryable = update.Retryable,
            CorrelationId = update.CorrelationId,
            TaskId = update.TaskId,
            StatusObservedAt = update.ObservedAt,
            Evidence = current.Evidence with
            {
                PublishedEventId = update.PublishedEventId ?? current.Evidence.PublishedEventId,
                ResultFingerprint = update.ResultFingerprint,
            },
        };
    }

    private static bool IdentityMatches(SemanticIndexingFileVersionIdentity current, SemanticIndexingFileVersionIdentity candidate)
        => string.Equals(current.ManagedTenantId, candidate.ManagedTenantId, StringComparison.Ordinal)
            && string.Equals(current.FolderId, candidate.FolderId, StringComparison.Ordinal)
            && string.Equals(current.FileVersionId, candidate.FileVersionId, StringComparison.Ordinal)
            && string.Equals(current.ContentHashReference, candidate.ContentHashReference, StringComparison.Ordinal)
            && string.Equals(current.SourceUri, candidate.SourceUri, StringComparison.Ordinal);

    private static void ApplyFileMutation(
        Dictionary<string, SemanticIndexingBridgeEntry> entries,
        FolderProjectionEnvelope envelope,
        WorkspaceFileMutationAccepted accepted)
    {
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(accepted);
        if (string.Equals(accepted.FileOperationKind, "remove", StringComparison.Ordinal))
        {
            string[] affectedKeys = PathKeys(entries, identity);
            if (affectedKeys.Length == 0)
            {
                // No previously-indexed entry matched this path: tombstone with the remove-event identity (which has no
                // file-version content hash and no prior index evidence), so the removal egress treats it as a never-
                // indexed no-op.
                entries[identity.ReadModelKey] = Tombstone(identity, null, envelope, accepted);
                return;
            }

            foreach (string affectedKey in affectedKeys)
            {
                SemanticIndexingBridgeEntry affectedCurrent = entries[affectedKey];
                if (ShouldIgnoreCurrent(affectedCurrent, envelope, accepted.IdempotencyFingerprint))
                {
                    continue;
                }

                // Preserve the matched entry's Identity AND its index-time Evidence (PublishedEventId + curated
                // text/attributes) so the removal egress can target the exact upserted document (decision (C)) and an
                // archive re-send can reuse the original document (decision (A)).
                entries[affectedKey] = Tombstone(affectedCurrent.Identity, affectedCurrent.Evidence, envelope, accepted);
            }

            return;
        }

        string key = identity.ReadModelKey;
        if (entries.TryGetValue(key, out SemanticIndexingBridgeEntry? current)
            && ShouldIgnoreCurrent(current, envelope, accepted.IdempotencyFingerprint))
        {
            return;
        }

        entries[key] = new SemanticIndexingBridgeEntry(
            identity,
            SemanticIndexingBridgeStatus.Stale,
            "folders_file_version_changed",
            retryable: true,
            accepted.CorrelationId,
            accepted.TaskId,
            accepted.OccurredAt,
            evidence: SemanticIndexingEvidence.FromMutation(accepted),
            freshness: Freshness(envelope, accepted.IdempotencyFingerprint));
    }

    private static void ApplyCommitSucceeded(
        Dictionary<string, SemanticIndexingBridgeEntry> entries,
        FolderProjectionEnvelope envelope,
        WorkspaceCommitSucceeded succeeded)
    {
        foreach (string key in FolderKeys(entries, succeeded.ManagedTenantId, succeeded.FolderId))
        {
            SemanticIndexingBridgeEntry current = entries[key];
            if (current.Freshness.Watermark > envelope.Sequence)
            {
                continue;
            }

            entries[key] = current with
            {
                CommitEvidence = new SemanticIndexingCommitEvidence(
                    succeeded: true,
                    succeeded.OperationId,
                    succeeded.ProviderOutcomeCategory,
                    failureCategory: null,
                    succeeded.CorrelationId,
                    succeeded.TaskId,
                    succeeded.OccurredAt),
                Freshness = Freshness(envelope, succeeded.IdempotencyFingerprint),
            };
        }
    }

    private static void ApplyCommitFailed(
        Dictionary<string, SemanticIndexingBridgeEntry> entries,
        FolderProjectionEnvelope envelope,
        WorkspaceCommitFailed failed)
    {
        foreach (string key in FolderKeys(entries, failed.ManagedTenantId, failed.FolderId))
        {
            SemanticIndexingBridgeEntry current = entries[key];
            if (current.Freshness.Watermark > envelope.Sequence)
            {
                continue;
            }

            entries[key] = current with
            {
                CommitEvidence = new SemanticIndexingCommitEvidence(
                    succeeded: false,
                    failed.OperationId,
                    failed.ProviderOutcomeCategory,
                    failed.FailureCategory,
                    failed.CorrelationId,
                    failed.TaskId,
                    failed.OccurredAt),
                Freshness = Freshness(envelope, failed.IdempotencyFingerprint),
            };
        }
    }

    private static void ApplyFolderArchived(
        Dictionary<string, SemanticIndexingBridgeEntry> entries,
        FolderProjectionEnvelope envelope,
        FolderArchived archived)
    {
        foreach (string key in FolderKeys(entries, archived.ManagedTenantId, archived.FolderId))
        {
            SemanticIndexingBridgeEntry current = entries[key];
            if (current.Freshness.Watermark > envelope.Sequence)
            {
                continue;
            }

            entries[key] = current with
            {
                Status = SemanticIndexingBridgeStatus.Tombstoned,
                StatusCode = SemanticIndexingBridgeStatus.Tombstoned.ToStatusCode(),
                ReasonCode = "folder_archived",
                Retryable = false,
                CorrelationId = archived.CorrelationId,
                TaskId = archived.TaskId,
                StatusObservedAt = archived.OccurredAt,
                Freshness = Freshness(envelope, archived.IdempotencyFingerprint),
            };
        }
    }

    private static IEnumerable<string> FolderKeys(
        IReadOnlyDictionary<string, SemanticIndexingBridgeEntry> entries,
        string managedTenantId,
        string folderId)
        => entries
            .Where(pair => string.Equals(pair.Value.Identity.ManagedTenantId, managedTenantId, StringComparison.Ordinal)
                && string.Equals(pair.Value.Identity.FolderId, folderId, StringComparison.Ordinal))
            .Select(static pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] PathKeys(
        IReadOnlyDictionary<string, SemanticIndexingBridgeEntry> entries,
        SemanticIndexingFileVersionIdentity identity)
        => entries
            .Where(pair => string.Equals(pair.Value.Identity.ManagedTenantId, identity.ManagedTenantId, StringComparison.Ordinal)
                && string.Equals(pair.Value.Identity.OrganizationId, identity.OrganizationId, StringComparison.Ordinal)
                && string.Equals(pair.Value.Identity.FolderId, identity.FolderId, StringComparison.Ordinal)
                && string.Equals(pair.Value.Identity.WorkspaceId, identity.WorkspaceId, StringComparison.Ordinal)
                && string.Equals(pair.Value.Identity.PathMetadataDigest, identity.PathMetadataDigest, StringComparison.Ordinal))
            .Select(static pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool ShouldIgnoreCurrent(
        SemanticIndexingBridgeEntry current,
        FolderProjectionEnvelope envelope,
        string fingerprint)
    {
        if (IsDuplicate(current, fingerprint) || current.Freshness.Watermark > envelope.Sequence)
        {
            return true;
        }

        if (current.Freshness.Watermark == envelope.Sequence)
        {
            throw new InvalidOperationException(
                $"SemanticIndexingBridgeProjection received conflicting event evidence for key '{current.Identity.ReadModelKey}' at sequence {envelope.Sequence}.");
        }

        return false;
    }

    private static bool IsDuplicate(SemanticIndexingBridgeEntry current, string fingerprint)
        => string.Equals(current.Freshness.LastEventFingerprint, fingerprint, StringComparison.Ordinal);

    private static SemanticIndexingBridgeEntry Tombstone(
        SemanticIndexingFileVersionIdentity identity,
        SemanticIndexingEvidence? evidence,
        FolderProjectionEnvelope envelope,
        WorkspaceFileMutationAccepted accepted)
        => new(
            identity,
            SemanticIndexingBridgeStatus.Tombstoned,
            "folder_file_removed",
            retryable: false,
            accepted.CorrelationId,
            accepted.TaskId,
            accepted.OccurredAt,
            evidence: evidence,
            freshness: Freshness(envelope, accepted.IdempotencyFingerprint));

    private static SemanticIndexingProjectionFreshness Freshness(FolderProjectionEnvelope envelope, string fingerprint)
        => new(envelope.Sequence, envelope.Event.GetType().Name, fingerprint, EventObservedAt(envelope.Event));

    private static DateTimeOffset EventObservedAt(IFolderEvent evt)
        => evt switch
        {
            WorkspaceFileMutationAccepted accepted => accepted.OccurredAt,
            WorkspaceCommitSucceeded succeeded => succeeded.OccurredAt,
            WorkspaceCommitFailed failed => failed.OccurredAt,
            FolderArchived archived => archived.OccurredAt,
            _ => DateTimeOffset.UnixEpoch,
        };

    private static string EventIdempotencyKey(IFolderEvent evt)
        => evt switch
        {
            WorkspaceFileMutationAccepted accepted => accepted.IdempotencyKey,
            WorkspaceCommitSucceeded succeeded => succeeded.IdempotencyKey,
            WorkspaceCommitFailed failed => failed.IdempotencyKey,
            FolderArchived archived => archived.IdempotencyKey,
            _ => string.Empty,
        };

    private static string EventFingerprint(IFolderEvent evt)
        => evt switch
        {
            WorkspaceFileMutationAccepted accepted => accepted.IdempotencyFingerprint,
            WorkspaceCommitSucceeded succeeded => succeeded.IdempotencyFingerprint,
            WorkspaceCommitFailed failed => failed.IdempotencyFingerprint,
            FolderArchived archived => archived.IdempotencyFingerprint,
            _ => string.Empty,
        };
}
