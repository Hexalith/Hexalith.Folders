using System.Collections.Frozen;

using Hexalith.Folders.Aggregates.Folder;

namespace Hexalith.Folders.Projections.FolderList;

public sealed record FolderListProjection
{
    // Primary constructor is private so the only way to obtain a `FolderListProjection`
    // is through `Empty` or `Apply`, both of which return a `FrozenDictionary` backing.
    // This prevents a caller from constructing a projection over a mutable dictionary
    // and bypassing the canonicalization invariants enforced in `Apply`.
    private FolderListProjection(IReadOnlyDictionary<string, FolderListItem> folders)
    {
        Folders = folders;
    }

    public IReadOnlyDictionary<string, FolderListItem> Folders { get; }

    public static FolderListProjection Empty { get; } = new(FrozenDictionary<string, FolderListItem>.Empty);

    public FolderListProjection Apply(IEnumerable<FolderProjectionEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(envelopes);

        Dictionary<string, FolderListItem> folders = new(Folders, StringComparer.Ordinal);

        IEnumerable<FolderProjectionEnvelope> ordered = envelopes
            .Where(static envelope => envelope is not null)
            .Where(static envelope => IsEnvelopeWellFormed(envelope))
            // Secondary keys make replay deterministic when two envelopes share the
            // same `Sequence`: idempotency key first (durable per-event identity), then
            // fingerprint (durable per-content identity). Without the tiebreaker the
            // last-write-wins outcome would depend on the source enumerable's order.
            .OrderBy(static envelope => envelope.Sequence)
            .ThenBy(static envelope => envelope.Event.IdempotencyKey, StringComparer.Ordinal)
            .ThenBy(static envelope => envelope.Event.IdempotencyFingerprint, StringComparer.Ordinal);

        foreach (FolderProjectionEnvelope envelope in ordered)
        {
            // Envelope and event tenants must agree. `FolderStateApply` already enforces
            // this for the aggregate; the projection adds the same guard so a misrouted
            // event cannot land in a different tenant's list bucket.
            if (!string.Equals(envelope.ManagedTenantId, envelope.Event.ManagedTenantId, StringComparison.Ordinal))
            {
                continue;
            }

            string key = Key(envelope.ManagedTenantId, envelope.Event.FolderId);
            switch (envelope.Event)
            {
                case FolderCreated created:
                    folders[key] = new FolderListItem(
                        envelope.ManagedTenantId,
                        created.OrganizationId,
                        created.FolderId,
                        created.DisplayName,
                        created.Description,
                        created.PathLabel,
                        CanonicalizeTags(created.Tags),
                        created.LifecycleState,
                        created.RepositoryBindingState,
                        RepositoryBindingId: null,
                        ProviderBindingRef: null,
                        RepositoryProfileRef: null,
                        ExternalRepositoryRefFingerprint: null,
                        BranchRefPolicyRef: null,
                        RepositoryBindingFailureCategory: null,
                        RepositoryBindingOutcomeCategory: null,
                        RepositoryBindingUpdatedAt: null,
                        ArchiveReasonCode: null,
                        ArchiveActorPrincipalId: null,
                        ArchiveCorrelationId: null,
                        ArchiveTaskId: null,
                        ArchiveIdempotencyKey: null,
                        ArchivedAt: null,
                        envelope.Sequence);
                    break;

                case FolderArchived archived:
                    if (!folders.TryGetValue(key, out FolderListItem? current))
                    {
                        // An archive event for a key the projection has not yet
                        // observed a creation for is a replay-order anomaly. Fail loudly
                        // so callers/integration tests catch missing/mid-stream replay
                        // rather than silently dropping the archive evidence.
                        throw new InvalidOperationException(
                            $"FolderListProjection received a FolderArchived envelope at sequence "
                            + $"{envelope.Sequence} for {key} before any FolderCreated event.");
                    }

                    folders[key] = current with
                    {
                        LifecycleState = FolderLifecycleState.Archived,
                        ArchiveReasonCode = archived.ArchiveReasonCode,
                        ArchiveActorPrincipalId = archived.ActorPrincipalId,
                        ArchiveCorrelationId = archived.CorrelationId,
                        ArchiveTaskId = archived.TaskId,
                        ArchiveIdempotencyKey = archived.IdempotencyKey,
                        ArchivedAt = archived.OccurredAt,
                        Sequence = envelope.Sequence,
                    };
                    break;

                case RepositoryBindingRequested requested:
                    folders[key] = RequireCurrent(folders, key, envelope) with
                    {
                        RepositoryBindingState = FolderRepositoryBindingState.BindingRequested,
                        RepositoryBindingId = requested.RepositoryBindingId,
                        ProviderBindingRef = requested.ProviderBindingRef,
                        RepositoryProfileRef = requested.RepositoryProfileRef,
                        ExternalRepositoryRefFingerprint = null,
                        BranchRefPolicyRef = requested.BranchRefPolicyRef,
                        RepositoryBindingFailureCategory = null,
                        RepositoryBindingOutcomeCategory = null,
                        RepositoryBindingUpdatedAt = requested.OccurredAt,
                        Sequence = envelope.Sequence,
                    };
                    break;

                case ExistingRepositoryBindingRequested requested:
                    folders[key] = RequireCurrent(folders, key, envelope) with
                    {
                        RepositoryBindingState = FolderRepositoryBindingState.BindingRequested,
                        RepositoryBindingId = requested.RepositoryBindingId,
                        ProviderBindingRef = requested.ProviderBindingRef,
                        RepositoryProfileRef = null,
                        ExternalRepositoryRefFingerprint = requested.ExternalRepositoryRefFingerprint,
                        BranchRefPolicyRef = requested.BranchRefPolicyRef,
                        RepositoryBindingFailureCategory = null,
                        RepositoryBindingOutcomeCategory = null,
                        RepositoryBindingUpdatedAt = requested.OccurredAt,
                        Sequence = envelope.Sequence,
                    };
                    break;

                case RepositoryBound bound:
                    folders[key] = RequireCurrent(folders, key, envelope) with
                    {
                        RepositoryBindingState = FolderRepositoryBindingState.Bound,
                        RepositoryBindingId = bound.RepositoryBindingId,
                        ProviderBindingRef = bound.ProviderBindingRef,
                        RepositoryBindingFailureCategory = null,
                        RepositoryBindingOutcomeCategory = null,
                        RepositoryBindingUpdatedAt = bound.OccurredAt,
                        Sequence = envelope.Sequence,
                    };
                    break;

                case RepositoryBindingFailed failed:
                    folders[key] = RequireCurrent(folders, key, envelope) with
                    {
                        RepositoryBindingState = FolderRepositoryBindingState.Failed,
                        RepositoryBindingId = failed.RepositoryBindingId,
                        ProviderBindingRef = failed.ProviderBindingRef,
                        RepositoryBindingFailureCategory = failed.FailureCategory,
                        RepositoryBindingOutcomeCategory = null,
                        RepositoryBindingUpdatedAt = failed.OccurredAt,
                        Sequence = envelope.Sequence,
                    };
                    break;

                case ProviderOutcomeUnknown unknown:
                    folders[key] = RequireCurrent(folders, key, envelope) with
                    {
                        RepositoryBindingState = unknown.ReconciliationRequired
                            ? FolderRepositoryBindingState.ReconciliationRequired
                            : FolderRepositoryBindingState.UnknownProviderOutcome,
                        RepositoryBindingId = unknown.RepositoryBindingId,
                        ProviderBindingRef = unknown.ProviderBindingRef,
                        RepositoryBindingFailureCategory = null,
                        RepositoryBindingOutcomeCategory = unknown.OutcomeCategory,
                        RepositoryBindingUpdatedAt = unknown.OccurredAt,
                        Sequence = envelope.Sequence,
                    };
                    break;

                default:
                    // Diverging from FolderStateApply (which throws on unknown event types)
                    // would let new event types replay as no-ops in the projection while the
                    // aggregate fails loudly. Throw to keep the two in sync.
                    throw new InvalidOperationException(
                        $"FolderListProjection received an unsupported event type "
                        + $"'{envelope.Event.GetType().FullName}' at sequence {envelope.Sequence}.");
            }
        }

        return new FolderListProjection(folders.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public bool Contains(string managedTenantId, string folderId)
        => Folders.ContainsKey(Key(managedTenantId, folderId));

    public FolderListItem? Get(string managedTenantId, string folderId)
        => Folders.TryGetValue(Key(managedTenantId, folderId), out FolderListItem? item) ? item : null;

    private static FolderListItem RequireCurrent(
        IReadOnlyDictionary<string, FolderListItem> folders,
        string key,
        FolderProjectionEnvelope envelope)
    {
        if (folders.TryGetValue(key, out FolderListItem? current))
        {
            return current;
        }

        throw new InvalidOperationException(
            $"FolderListProjection received a repository binding envelope at sequence "
            + $"{envelope.Sequence} for {key} before any FolderCreated event.");
    }

    private static bool IsEnvelopeWellFormed(FolderProjectionEnvelope envelope)
        => envelope.Event is not null
            && FolderStreamName.TryCreate(envelope.ManagedTenantId, envelope.Event.FolderId, out _, out _);

    // The projection key is the canonical stream-name shape. Validating envelope
    // identifiers above ensures the key cannot be ambiguous (no segment may itself
    // contain `:folders:` or any other separator-bearing string).
    private static string Key(string managedTenantId, string folderId)
        => $"{managedTenantId}:folders:{folderId}";

    private static IReadOnlyList<string> CanonicalizeTags(IReadOnlyList<string> tags)
        => tags
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .Select(static tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
