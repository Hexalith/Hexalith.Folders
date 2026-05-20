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
            if (envelope.Event is FolderCreated created)
            {
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
                    ArchiveReasonCode: null,
                    ArchiveActorPrincipalId: null,
                    ArchiveCorrelationId: null,
                    ArchiveTaskId: null,
                    ArchiveIdempotencyKey: null,
                    ArchivedAt: null,
                    envelope.Sequence);
            }
            else if (envelope.Event is FolderArchived archived
                && folders.TryGetValue(key, out FolderListItem? current))
            {
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
            }
        }

        return new FolderListProjection(folders.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public bool Contains(string managedTenantId, string folderId)
        => Folders.ContainsKey(Key(managedTenantId, folderId));

    public FolderListItem? Get(string managedTenantId, string folderId)
        => Folders.TryGetValue(Key(managedTenantId, folderId), out FolderListItem? item) ? item : null;

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
