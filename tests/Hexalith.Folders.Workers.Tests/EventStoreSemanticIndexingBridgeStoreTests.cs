using Hexalith.EventStore.Client.Projections;
using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.SemanticIndexing;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Workers.Tests;

public sealed class EventStoreSemanticIndexingBridgeStoreTests
{
    private const string StoreName = EventStoreSemanticIndexingBridgeStore.StateStoreName;
    private static readonly DateTimeOffset OccurredAt = new(2026, 6, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ApplyFolderEventsAsyncShouldPersistTenantPrefixedFileVersionAndFolderIndexKeys()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        WorkspaceFileMutationAccepted mutation = Mutation();
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(mutation);

        IReadOnlyList<SemanticIndexingBridgeEntry> persisted = await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, mutation)],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        SemanticIndexingBridgeEntry entry = persisted.ShouldHaveSingleItem();
        entry.Identity.ReadModelKey.ShouldBe(identity.ReadModelKey);
        entry.Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
        entry.ReasonCode.ShouldBe("folders_file_version_changed");

        SemanticIndexingBridgeEntry? reloaded = await bridgeStore.GetFileVersionAsync(
            identity,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        reloaded.ShouldBe(entry);
        readModelStore.Keys.ShouldAllBe(key => key.StartsWith("statestore:", StringComparison.Ordinal));
        readModelStore.Keys.ShouldContain($"statestore:{identity.ReadModelKey}");
        readModelStore.Keys.ShouldContain("statestore:tenant-a:semantic-indexing:folder:folder-a:file-versions");
    }

    [Fact]
    public async Task ApplyFolderEventsAsyncShouldDropTenantMismatchedFileEventsWithoutWriting()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);

        IReadOnlyList<SemanticIndexingBridgeEntry> persisted = await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-b", 1, Mutation())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        persisted.ShouldBeEmpty();
        readModelStore.Keys.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFileVersionAsyncShouldReturnNullWhenStoredIdentityReadModelKeyDoesNotMatchLookup()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        SemanticIndexingFileVersionIdentity lookup = LookupIdentity();
        await readModelStore.SaveAsync(
            StoreName,
            lookup.ReadModelKey,
            SwappedIdentityEntry(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        SemanticIndexingBridgeEntry? loaded = await bridgeStore.GetFileVersionAsync(
            lookup,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task GetFileVersionByIdAsyncShouldReturnNullWhenStoredIdentityDoesNotMatchLookup()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        SemanticIndexingFileVersionIdentity lookup = LookupIdentity();
        await readModelStore.SaveAsync(
            StoreName,
            lookup.ReadModelKey,
            SwappedIdentityEntry(),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        SemanticIndexingBridgeEntry? loaded = await bridgeStore.GetFileVersionByIdAsync(
            lookup.ManagedTenantId,
            lookup.FolderId,
            lookup.FileVersionId,
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyFolderEventsAsyncShouldReloadVersionStatusAndRemovalOnNewInstanceOverSameStore()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore writer = new(readModelStore);
        WorkspaceFileMutationAccepted mutation = Mutation();
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(mutation);

        IReadOnlyList<SemanticIndexingBridgeEntry> emptyCheckpoint = await writer.ApplyFolderEventsAsync(
            [
                new FolderProjectionEnvelope("tenant-a", 1, mutation),
                new FolderProjectionEnvelope("tenant-a", 2, Archived()),
            ],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        SemanticIndexingBridgeEntry archived = emptyCheckpoint.Last();
        archived.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        archived.ReasonCode.ShouldBe("folder_archived");

        SemanticIndexingBridgeEntry? recorded = await writer.RecordRemovalEvidenceAsync(
            new SemanticIndexingRemovalEvidenceUpdate(
                archived.Identity,
                "memories_accepted",
                retryable: false,
                "correlation-removal-restart",
                "task-removal-restart",
                "folders://tenant-a/published-restart",
                "result-fingerprint-removal-restart",
                archived.Freshness.Watermark,
                OccurredAt.AddMinutes(3)),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        recorded.ShouldNotBeNull();
        recorded.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        recorded.ReasonCode.ShouldBe("memories_accepted");

        string[] persistedKeys = readModelStore.Keys.Order(StringComparer.Ordinal).ToArray();
        persistedKeys.ShouldContain($"statestore:{identity.ReadModelKey}");
        persistedKeys.ShouldContain("statestore:tenant-a:semantic-indexing:folder:folder-a:file-versions");

        EventStoreSemanticIndexingBridgeStore reloaded = new(readModelStore);
        SemanticIndexingBridgeEntry version = (await reloaded.GetFileVersionAsync(
            identity,
            TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
        version.Identity.ReadModelKey.ShouldBe(identity.ReadModelKey);
        version.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        version.ReasonCode.ShouldBe("memories_accepted");
        version.Evidence.PublishedEventId.ShouldBe("folders://tenant-a/published-restart");
        version.Freshness.Watermark.ShouldBe(2);

        SemanticIndexingBridgeEntry listed = (await reloaded.ListFolderAsync(
            "tenant-a",
            "folder-a",
            TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldHaveSingleItem();
        listed.ShouldBe(version);
        readModelStore.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(persistedKeys);
    }

    [Fact]
    public async Task ApplyFolderEventsAsyncShouldBeEquivalentUnderDuplicateDelivery()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        FolderProjectionEnvelope[] envelopes =
        [
            new FolderProjectionEnvelope("tenant-a", 1, Mutation()),
            new FolderProjectionEnvelope("tenant-a", 2, Archived()),
        ];

        await bridgeStore.ApplyFolderEventsAsync(envelopes, TestContext.Current.CancellationToken).ConfigureAwait(true);
        string[] firstKeys = readModelStore.Keys.Order(StringComparer.Ordinal).ToArray();
        SemanticIndexingBridgeEntry first = (await bridgeStore.GetFileVersionByIdAsync(
            "tenant-a",
            "folder-a",
            SemanticIndexingFileVersionIdentity.From(Mutation()).FileVersionId,
            TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();

        IReadOnlyList<SemanticIndexingBridgeEntry> secondApply = await bridgeStore.ApplyFolderEventsAsync(
            envelopes,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        SemanticIndexingBridgeEntry second = (await bridgeStore.GetFileVersionAsync(
            first.Identity,
            TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();

        readModelStore.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(firstKeys);
        secondApply.Select(static entry => entry.Identity.ReadModelKey).Distinct(StringComparer.Ordinal)
            .ShouldBe([first.Identity.ReadModelKey]);
        second.Identity.ReadModelKey.ShouldBe(first.Identity.ReadModelKey);
        second.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        second.ReasonCode.ShouldBe("folder_archived");
        second.Freshness.Watermark.ShouldBe(first.Freshness.Watermark);
    }

    [Fact]
    public async Task ApplyFolderEventsAsyncShouldApplyFolderScopedArchiveThroughPersistedIndex()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        WorkspaceFileMutationAccepted mutation = Mutation();
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(mutation);

        await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, mutation)],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        IReadOnlyList<SemanticIndexingBridgeEntry> updated = await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 2, Archived())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        SemanticIndexingBridgeEntry archived = updated.ShouldHaveSingleItem();
        archived.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        archived.ReasonCode.ShouldBe("folder_archived");

        SemanticIndexingBridgeEntry? reloaded = await bridgeStore.GetFileVersionAsync(
            identity,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        reloaded.ShouldNotBeNull();
        reloaded.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        reloaded.Freshness.Watermark.ShouldBe(2);
    }

    [Fact]
    public async Task ApplyFolderEventsAsyncShouldTombstoneKnownFileVersionWhenRemoveHasNoContentHash()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        WorkspaceFileMutationAccepted mutation = Mutation();
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(mutation);

        await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, mutation)],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        IReadOnlyList<SemanticIndexingBridgeEntry> updated = await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 2, Mutation(fileOperationKind: "remove", contentHashReference: null))],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        SemanticIndexingBridgeEntry tombstoned = updated.ShouldHaveSingleItem();
        tombstoned.Identity.ReadModelKey.ShouldBe(identity.ReadModelKey);
        tombstoned.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        tombstoned.ReasonCode.ShouldBe("folder_file_removed");
        tombstoned.Identity.ContentHashReference.ShouldBe("sha256:a");

        SemanticIndexingBridgeEntry? reloaded = await bridgeStore.GetFileVersionAsync(
            identity,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        reloaded.ShouldNotBeNull();
        reloaded.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        reloaded.Freshness.Watermark.ShouldBe(2);
    }

    [Fact]
    public async Task RecordIndexingResultAsyncShouldProtectNewerPersistedFileVersionFromStaleResult()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        WorkspaceFileMutationAccepted mutation = Mutation();
        SemanticIndexingFileVersionIdentity currentIdentity = SemanticIndexingFileVersionIdentity.From(mutation);
        SemanticIndexingFileVersionIdentity staleIdentity = SemanticIndexingFileVersionIdentity.From(Mutation(contentHashReference: "sha256:older"));

        await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, mutation)],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        SemanticIndexingBridgeEntry? staleResult = await bridgeStore.RecordIndexingResultAsync(
            new SemanticIndexingResultUpdate(
                staleIdentity,
                SemanticIndexingBridgeStatus.Indexed,
                "memories_accepted",
                retryable: false,
                "correlation-result-stale",
                "task-result-stale",
                "folders://tenant-a/published-stale",
                "result-fingerprint-stale",
                OccurredAt.AddMinutes(5)),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        staleResult.ShouldBeNull();
        SemanticIndexingBridgeEntry current = (await bridgeStore.GetFileVersionAsync(
            currentIdentity,
            TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
        current.Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
        current.Evidence.PublishedEventId.ShouldBeNull();
    }

    [Fact]
    public async Task RecordRemovalEvidenceAsyncShouldRecordOutcomeOnTombstonedEntryWithoutStatusRegression()
    {
        InMemoryReadModelStoreDouble readModelStore = new();
        EventStoreSemanticIndexingBridgeStore bridgeStore = new(readModelStore);
        WorkspaceFileMutationAccepted mutation = Mutation();
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(mutation);

        // Index the file version (records PublishedEventId evidence), then remove it (tombstone preserving evidence).
        await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, mutation)],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        await bridgeStore.RecordIndexingResultAsync(
            new SemanticIndexingResultUpdate(
                identity,
                SemanticIndexingBridgeStatus.Indexed,
                "memories_accepted",
                retryable: false,
                "correlation-index-a",
                "task-index-a",
                "folders://tenant-a/published-a",
                "result-fingerprint-a",
                OccurredAt.AddMinutes(1)),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        IReadOnlyList<SemanticIndexingBridgeEntry> removed = await bridgeStore.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 2, Mutation(fileOperationKind: "remove", contentHashReference: null))],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        SemanticIndexingBridgeEntry tombstoned = removed.ShouldHaveSingleItem();
        tombstoned.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        tombstoned.Evidence.PublishedEventId.ShouldBe("folders://tenant-a/published-a");

        SemanticIndexingBridgeEntry? recorded = await bridgeStore.RecordRemovalEvidenceAsync(
            new SemanticIndexingRemovalEvidenceUpdate(
                tombstoned.Identity,
                "memories_accepted",
                retryable: false,
                "correlation-removal-a",
                "task-removal-a",
                "folders://tenant-a/published-a",
                "result-fingerprint-removal-a",
                tombstoned.Freshness.Watermark,
                OccurredAt.AddMinutes(3)),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        recorded.ShouldNotBeNull();
        recorded.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        recorded.ReasonCode.ShouldBe("memories_accepted");

        SemanticIndexingBridgeEntry? reloaded = await bridgeStore.GetFileVersionAsync(
            identity,
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        reloaded.ShouldNotBeNull();
        reloaded.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        reloaded.Evidence.PublishedEventId.ShouldBe("folders://tenant-a/published-a");
        readModelStore.Keys.ShouldContain($"statestore:{identity.ReadModelKey}");
    }

    private static SemanticIndexingFileVersionIdentity LookupIdentity()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            "operation-a",
            "path-digest-a",
            "fv-lookup",
            "sha256:a",
            "folders://tenant-a/organizations/organization-a/folders/folder-a/workspaces/workspace-a/file-versions/fv-lookup");

    private static SemanticIndexingBridgeEntry SwappedIdentityEntry()
        => new(
            new SemanticIndexingFileVersionIdentity(
                "tenant-b",
                "organization-b",
                "folder-b",
                "workspace-b",
                "operation-b",
                "path-digest-b",
                "fv-foreign",
                "sha256:b",
                "folders://tenant-b/organizations/organization-b/folders/folder-b/workspaces/workspace-b/file-versions/fv-foreign"),
            SemanticIndexingBridgeStatus.Indexed,
            "poisoned_identity",
            retryable: false,
            "correlation-poisoned",
            "task-poisoned",
            OccurredAt);

    private static WorkspaceFileMutationAccepted Mutation(
        string fileOperationKind = "add",
        string? contentHashReference = "sha256:a")
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            FolderWorkspaceLifecycleEvent.FileMutated,
            fileOperationKind == "remove" ? "operation-remove-a" : "operation-a",
            fileOperationKind,
            fileOperationKind == "remove" ? "metadataOnlyRemoval" : "PutFileInline",
            "tenant-sensitive",
            "path-digest-a",
            contentHashReference,
            fileOperationKind == "remove" ? null : 128,
            fileOperationKind == "remove" ? null : "text/plain",
            fileOperationKind == "remove" ? null : "inline_decoded",
            fileOperationKind == "remove" ? null : 128,
            "principal-a",
            "correlation-a",
            "task-a",
            fileOperationKind == "remove" ? "idempotency-remove-a" : "idempotency-a",
            fileOperationKind == "remove" ? "fingerprint-remove-a" : "fingerprint-a",
            OccurredAt);

    private static FolderArchived Archived()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            FolderArchiveReasonCode.CallerRequested,
            "principal-a",
            "correlation-archive-a",
            "task-archive-a",
            "idempotency-archive-a",
            "fingerprint-archive-a",
            OccurredAt.AddMinutes(1));

    private sealed class InMemoryReadModelStoreDouble : IReadModelStore
    {
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private long _etagSequence;

        public IReadOnlyCollection<string> Keys => _entries.Keys.Order(StringComparer.Ordinal).ToArray();

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(
            string storeName,
            string key,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();

            return _entries.TryGetValue(Compose(storeName, key), out Entry? entry)
                ? Task.FromResult(new ReadModelEntry<TValue>((TValue)entry.Value, entry.ETag))
                : Task.FromResult(new ReadModelEntry<TValue>(null, null));
        }

        public Task SaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            cancellationToken.ThrowIfCancellationRequested();

            _entries[Compose(storeName, key)] = new Entry(value, NextETag());
            return Task.CompletedTask;
        }

        public Task<bool> TrySaveAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            CancellationToken cancellationToken = default)
            where TValue : class
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(etag);
            cancellationToken.ThrowIfCancellationRequested();

            string composite = Compose(storeName, key);
            bool exists = _entries.TryGetValue(composite, out Entry? current);
            bool matches = exists
                ? string.Equals(current!.ETag, etag, StringComparison.Ordinal)
                : etag.Length == 0;
            if (!matches)
            {
                return Task.FromResult(false);
            }

            _entries[composite] = new Entry(value, NextETag());
            return Task.FromResult(true);
        }

        private static string Compose(string storeName, string key) => $"{storeName}:{key}";

        private string NextETag() => Interlocked.Increment(ref _etagSequence).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private sealed record Entry(object Value, string ETag);
    }
}
