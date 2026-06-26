using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.SemanticIndexing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hexalith.Folders.Workers.SemanticIndexing;

public sealed class EventStoreSemanticIndexingBridgeStore : ISemanticIndexingBridgeReadModel, ISemanticIndexingBridgeWriter
{
    public const string StateStoreName = "statestore";

    private readonly ILogger<EventStoreSemanticIndexingBridgeStore> _logger;
    private readonly IReadModelStore _store;

    public EventStoreSemanticIndexingBridgeStore(IReadModelStore store)
        : this(store, NullLogger<EventStoreSemanticIndexingBridgeStore>.Instance)
    {
    }

    public EventStoreSemanticIndexingBridgeStore(
        IReadModelStore store,
        ILogger<EventStoreSemanticIndexingBridgeStore> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _logger = logger;
    }

    public bool IsAvailable => true;

    public async Task<SemanticIndexingBridgeEntry?> GetFileVersionAsync(
        SemanticIndexingFileVersionIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        ReadModelEntry<SemanticIndexingBridgeEntry> result = await _store
            .GetAsync<SemanticIndexingBridgeEntry>(StateStoreName, identity.ReadModelKey, cancellationToken)
            .ConfigureAwait(false);

        return result.Value;
    }

    public async Task<SemanticIndexingBridgeEntry?> GetFileVersionByIdAsync(
        string managedTenantId,
        string folderId,
        string fileVersionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileVersionId);

        ReadModelEntry<SemanticIndexingBridgeEntry> result = await _store
            .GetAsync<SemanticIndexingBridgeEntry>(
                StateStoreName,
                SemanticIndexingBridgeKeys.FileVersion(managedTenantId, folderId, fileVersionId),
                cancellationToken)
            .ConfigureAwait(false);

        return result.Value;
    }

    public async Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ListFolderAsync(
        string managedTenantId,
        string folderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);

        // IReadModelStore has no scan/enumerate primitive, so the tenant-prefixed folder index record is the only
        // safe enumeration mechanism: read the index, then fan out a point GetAsync per entry key (read-only — no
        // ReadModelWritePolicy). The index key is tenant-prefixed by construction; each loaded entry is additionally
        // guarded against the requested (managedTenantId, folderId) so a poisoned index can never surface a foreign
        // tenant's or folder's entry (belt-and-braces, mirroring IsSamePath's defense).
        ReadModelEntry<SemanticIndexingBridgeFolderIndex> index = await _store
            .GetAsync<SemanticIndexingBridgeFolderIndex>(StateStoreName, FolderIndexKey(managedTenantId, folderId), cancellationToken)
            .ConfigureAwait(false);
        if (index.Value is null)
        {
            return [];
        }

        List<SemanticIndexingBridgeEntry> entries = [];
        foreach (string key in index.Value.EntryKeys.Order(StringComparer.Ordinal))
        {
            ReadModelEntry<SemanticIndexingBridgeEntry> current = await _store
                .GetAsync<SemanticIndexingBridgeEntry>(StateStoreName, key, cancellationToken)
                .ConfigureAwait(false);
            if (current.Value is { } entry
                && string.Equals(entry.Identity.ManagedTenantId, managedTenantId, StringComparison.Ordinal)
                && string.Equals(entry.Identity.FolderId, folderId, StringComparison.Ordinal))
            {
                entries.Add(entry);
            }
        }

        return entries
            .OrderBy(static entry => entry.Identity.ReadModelKey, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ApplyFolderEventsAsync(
        IReadOnlyCollection<FolderProjectionEnvelope> envelopes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelopes);

        List<SemanticIndexingBridgeEntry> persisted = [];
        foreach (FolderProjectionEnvelope envelope in envelopes
            .Where(static envelope => envelope is not null)
            .OrderBy(static envelope => envelope.Sequence)
            .ThenBy(static envelope => envelope.Event?.GetType().Name, StringComparer.Ordinal))
        {
            if (!string.Equals(envelope.ManagedTenantId, envelope.Event.ManagedTenantId, StringComparison.Ordinal))
            {
                continue;
            }

            switch (envelope.Event)
            {
                case WorkspaceFileMutationAccepted accepted:
                    IReadOnlyList<SemanticIndexingBridgeEntry> fileEntries = IsRemove(accepted)
                        ? await ApplyRemoveFileEventAsync(envelope, accepted, cancellationToken).ConfigureAwait(false)
                        : [await ApplyFileEventAsync(envelope, accepted, cancellationToken).ConfigureAwait(false)];
                    foreach (SemanticIndexingBridgeEntry entry in fileEntries)
                    {
                        await AddToFolderIndexAsync(entry, cancellationToken).ConfigureAwait(false);
                        persisted.Add(entry);
                    }

                    break;
                case WorkspaceCommitSucceeded succeeded:
                    persisted.AddRange(await ApplyFolderScopedEventAsync(envelope, succeeded.ManagedTenantId, succeeded.FolderId, cancellationToken).ConfigureAwait(false));
                    break;
                case WorkspaceCommitFailed failed:
                    persisted.AddRange(await ApplyFolderScopedEventAsync(envelope, failed.ManagedTenantId, failed.FolderId, cancellationToken).ConfigureAwait(false));
                    break;
                case FolderArchived archived:
                    persisted.AddRange(await ApplyFolderScopedEventAsync(envelope, archived.ManagedTenantId, archived.FolderId, cancellationToken).ConfigureAwait(false));
                    break;
                default:
                    SemanticIndexingBridgeProjection.Empty.Apply([envelope]);
                    break;
            }
        }

        return persisted;
    }

    public async Task<SemanticIndexingBridgeEntry?> RecordIndexingResultAsync(
        SemanticIndexingResultUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        ReadModelEntry<SemanticIndexingBridgeEntry> existing = await _store
            .GetAsync<SemanticIndexingBridgeEntry>(StateStoreName, update.Identity.ReadModelKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing.Value is null)
        {
            return null;
        }

        return await ReadModelWritePolicy.UpdateAsync<SemanticIndexingBridgeEntry>(
            _store,
            StateStoreName,
            update.Identity.ReadModelKey,
            current => current is null ? existing.Value : SemanticIndexingBridgeProjection.ApplyIndexingResult(current, update),
            new ReadModelWriteContext(
                "folders semantic indexing bridge",
                nameof(SemanticIndexingBridgeEntry),
                update.CorrelationId),
            _logger,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<SemanticIndexingBridgeEntry?> RecordRemovalEvidenceAsync(
        SemanticIndexingRemovalEvidenceUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        ReadModelEntry<SemanticIndexingBridgeEntry> existing = await _store
            .GetAsync<SemanticIndexingBridgeEntry>(StateStoreName, update.Identity.ReadModelKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing.Value is null)
        {
            return null;
        }

        return await ReadModelWritePolicy.UpdateAsync<SemanticIndexingBridgeEntry>(
            _store,
            StateStoreName,
            update.Identity.ReadModelKey,
            current => current is null ? existing.Value : SemanticIndexingBridgeProjection.ApplyRemovalEvidence(current, update),
            new ReadModelWriteContext(
                "folders semantic indexing bridge removal evidence",
                nameof(SemanticIndexingBridgeEntry),
                update.CorrelationId),
            _logger,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<SemanticIndexingBridgeEntry> ApplyFileEventAsync(
        FolderProjectionEnvelope envelope,
        WorkspaceFileMutationAccepted accepted,
        CancellationToken cancellationToken)
    {
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(accepted);
        return await ReadModelWritePolicy.UpdateAsync<SemanticIndexingBridgeEntry>(
            _store,
            StateStoreName,
            identity.ReadModelKey,
            current => Project(current, envelope, identity.ReadModelKey),
            new ReadModelWriteContext(
                "folders semantic indexing bridge",
                nameof(SemanticIndexingBridgeEntry),
                accepted.CorrelationId),
            _logger,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ApplyRemoveFileEventAsync(
        FolderProjectionEnvelope envelope,
        WorkspaceFileMutationAccepted accepted,
        CancellationToken cancellationToken)
    {
        SemanticIndexingFileVersionIdentity removeIdentity = SemanticIndexingFileVersionIdentity.From(accepted);
        ReadModelEntry<SemanticIndexingBridgeFolderIndex> index = await _store
            .GetAsync<SemanticIndexingBridgeFolderIndex>(StateStoreName, FolderIndexKey(removeIdentity.ManagedTenantId, removeIdentity.FolderId), cancellationToken)
            .ConfigureAwait(false);

        List<SemanticIndexingBridgeEntry> updated = [];
        if (index.Value is not null)
        {
            foreach (string key in index.Value.EntryKeys.Order(StringComparer.Ordinal))
            {
                ReadModelEntry<SemanticIndexingBridgeEntry> current = await _store
                    .GetAsync<SemanticIndexingBridgeEntry>(StateStoreName, key, cancellationToken)
                    .ConfigureAwait(false);
                if (current.Value is null || !IsSamePath(current.Value.Identity, removeIdentity))
                {
                    continue;
                }

                SemanticIndexingBridgeEntry entry = await ReadModelWritePolicy.UpdateAsync<SemanticIndexingBridgeEntry>(
                    _store,
                    StateStoreName,
                    key,
                    loaded => Project(loaded ?? current.Value, envelope, key),
                    new ReadModelWriteContext(
                        "folders semantic indexing bridge",
                        nameof(SemanticIndexingBridgeEntry),
                        accepted.CorrelationId),
                    _logger,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                updated.Add(entry);
            }
        }

        if (updated.Count == 0)
        {
            updated.Add(await ApplyFileEventAsync(envelope, accepted, cancellationToken).ConfigureAwait(false));
        }

        return updated;
    }

    private async Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ApplyFolderScopedEventAsync(
        FolderProjectionEnvelope envelope,
        string managedTenantId,
        string folderId,
        CancellationToken cancellationToken)
    {
        ReadModelEntry<SemanticIndexingBridgeFolderIndex> index = await _store
            .GetAsync<SemanticIndexingBridgeFolderIndex>(StateStoreName, FolderIndexKey(managedTenantId, folderId), cancellationToken)
            .ConfigureAwait(false);
        if (index.Value is null)
        {
            return [];
        }

        List<SemanticIndexingBridgeEntry> updated = [];
        foreach (string key in index.Value.EntryKeys.Order(StringComparer.Ordinal))
        {
            ReadModelEntry<SemanticIndexingBridgeEntry> current = await _store
                .GetAsync<SemanticIndexingBridgeEntry>(StateStoreName, key, cancellationToken)
                .ConfigureAwait(false);
            if (current.Value is null)
            {
                continue;
            }

            SemanticIndexingBridgeEntry entry = await ReadModelWritePolicy.UpdateAsync<SemanticIndexingBridgeEntry>(
                _store,
                StateStoreName,
                key,
                loaded => Project(loaded ?? current.Value, envelope, key),
                new ReadModelWriteContext(
                    "folders semantic indexing bridge",
                    nameof(SemanticIndexingBridgeEntry),
                    current.Value.CorrelationId),
                _logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            updated.Add(entry);
        }

        return updated;
    }

    private Task<SemanticIndexingBridgeFolderIndex> AddToFolderIndexAsync(
        SemanticIndexingBridgeEntry entry,
        CancellationToken cancellationToken)
        => ReadModelWritePolicy.UpdateAsync<SemanticIndexingBridgeFolderIndex>(
            _store,
            StateStoreName,
            FolderIndexKey(entry.Identity.ManagedTenantId, entry.Identity.FolderId),
            current => (current ?? new SemanticIndexingBridgeFolderIndex(entry.Identity.ManagedTenantId, entry.Identity.FolderId)).Add(entry.Identity.ReadModelKey),
            new ReadModelWriteContext(
                "folders semantic indexing bridge folder index",
                nameof(SemanticIndexingBridgeFolderIndex),
                entry.CorrelationId),
            _logger,
            cancellationToken: cancellationToken);

    private static SemanticIndexingBridgeEntry Project(
        SemanticIndexingBridgeEntry? current,
        FolderProjectionEnvelope envelope,
        string expectedKey)
    {
        SemanticIndexingBridgeProjection projection = SemanticIndexingBridgeProjection
            .FromEntries(current is null ? [] : [current])
            .Apply([envelope]);
        return projection.Get(expectedKey)
            ?? current
            ?? throw new InvalidOperationException(
                $"Semantic indexing bridge event produced no read model for key '{expectedKey}'.");
    }

    private static string FolderIndexKey(string managedTenantId, string folderId)
        => $"{managedTenantId}:semantic-indexing:folder:{folderId}:file-versions";

    private static bool IsRemove(WorkspaceFileMutationAccepted accepted)
        => string.Equals(accepted.FileOperationKind, "remove", StringComparison.Ordinal);

    private static bool IsSamePath(
        SemanticIndexingFileVersionIdentity current,
        SemanticIndexingFileVersionIdentity candidate)
        => string.Equals(current.ManagedTenantId, candidate.ManagedTenantId, StringComparison.Ordinal)
            && string.Equals(current.OrganizationId, candidate.OrganizationId, StringComparison.Ordinal)
            && string.Equals(current.FolderId, candidate.FolderId, StringComparison.Ordinal)
            && string.Equals(current.WorkspaceId, candidate.WorkspaceId, StringComparison.Ordinal)
            && string.Equals(current.PathMetadataDigest, candidate.PathMetadataDigest, StringComparison.Ordinal);

    private sealed record SemanticIndexingBridgeFolderIndex(
        string ManagedTenantId,
        string FolderId,
        IReadOnlyList<string> EntryKeys)
    {
        public SemanticIndexingBridgeFolderIndex(string managedTenantId, string folderId)
            : this(managedTenantId, folderId, [])
        {
        }

        public SemanticIndexingBridgeFolderIndex Add(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            return this with
            {
                EntryKeys = EntryKeys
                    .Append(key)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
            };
        }
    }
}
