using System.Text.Json;

using Hexalith.Folders.Aggregates.Folder;
using Hexalith.Folders.Projections.FolderList;
using Hexalith.Folders.Projections.SemanticIndexing;

using Shouldly;
using Xunit;

namespace Hexalith.Folders.Tests.Projections.SemanticIndexing;

public sealed class SemanticIndexingBridgeProjectionTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 6, 23, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddOrChangeMutationShouldCreateTenantPrefixedMetadataOnlyStaleEntry()
    {
        WorkspaceFileMutationAccepted mutation = Mutation();

        SemanticIndexingBridgeEntry entry = Apply(mutation).Entries.Values.ShouldHaveSingleItem();

        entry.Identity.ReadModelKey.ShouldStartWith("tenant-a:");
        entry.Identity.ReadModelKey.ShouldContain(":semantic-indexing:file-version:folder-a:fv-");
        entry.Identity.SourceUri.ShouldStartWith("folders://tenant-a/");
        entry.Identity.SourceUri.ShouldNotContain("file://");
        entry.Identity.SourceUri.ShouldNotContain('\\');
        entry.Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
        entry.StatusCode.ShouldBe("stale");
        entry.Retryable.ShouldBeTrue();
        entry.ReasonCode.ShouldBe("folders_file_version_changed");
        entry.Freshness.Watermark.ShouldBe(1);
        entry.Freshness.LastEventFingerprint.ShouldBe("fingerprint-a");

        string json = JsonSerializer.Serialize(entry);
        json.ShouldNotContain("raw/path", Case.Sensitive);
        json.ShouldNotContain("file://", Case.Sensitive);
        json.ShouldNotContain("commit-message", Case.Sensitive);
    }

    [Fact]
    public void DerivedFileVersionIdentityShouldBeDeterministicAndContentSensitive()
    {
        SemanticIndexingFileVersionIdentity first = SemanticIndexingFileVersionIdentity.From(Mutation(contentHashReference: "sha256:a"));
        SemanticIndexingFileVersionIdentity second = SemanticIndexingFileVersionIdentity.From(Mutation(contentHashReference: "sha256:a"));
        SemanticIndexingFileVersionIdentity changed = SemanticIndexingFileVersionIdentity.From(Mutation(contentHashReference: "sha256:b"));

        second.FileVersionId.ShouldBe(first.FileVersionId);
        changed.FileVersionId.ShouldNotBe(first.FileVersionId);
        first.ReadModelKey.ShouldStartWith("tenant-a:");
    }

    [Fact]
    public void DuplicateDeliveryShouldBeIdempotentAndTenantMismatchShouldNotMutate()
    {
        WorkspaceFileMutationAccepted mutation = Mutation();

        SemanticIndexingBridgeProjection duplicateProjection = SemanticIndexingBridgeProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, mutation),
                new FolderProjectionEnvelope("tenant-a", 1, mutation),
            ]);
        SemanticIndexingBridgeProjection mismatchProjection = SemanticIndexingBridgeProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-b", 1, mutation),
            ]);

        duplicateProjection.Entries.Count.ShouldBe(1);
        mismatchProjection.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveAndArchiveEventsShouldTombstoneKnownEntriesWithoutDeletingEvidence()
    {
        SemanticIndexingBridgeEntry removed = Apply(Mutation(fileOperationKind: "remove", contentHashReference: null)).Entries.Values.ShouldHaveSingleItem();
        SemanticIndexingFileVersionIdentity addedIdentity = SemanticIndexingFileVersionIdentity.From(Mutation());
        SemanticIndexingBridgeProjection removedAfterAdd = SemanticIndexingBridgeProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Mutation()),
                new FolderProjectionEnvelope("tenant-a", 2, Mutation(fileOperationKind: "remove", contentHashReference: null)),
            ]);
        SemanticIndexingBridgeEntry archived = SemanticIndexingBridgeProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Mutation()),
                new FolderProjectionEnvelope("tenant-a", 2, Archived()),
            ]).Entries.Values.ShouldHaveSingleItem();

        removed.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        removed.StatusCode.ShouldBe("tombstoned");
        removed.Retryable.ShouldBeFalse();
        removed.ReasonCode.ShouldBe("folder_file_removed");
        removedAfterAdd.Entries.Count.ShouldBe(1);
        SemanticIndexingBridgeEntry removedKnownVersion = removedAfterAdd.Get(addedIdentity.ReadModelKey).ShouldNotBeNull();
        removedKnownVersion.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        removedKnownVersion.ReasonCode.ShouldBe("folder_file_removed");
        removedKnownVersion.Identity.ContentHashReference.ShouldBe("sha256:a");
        archived.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        archived.ReasonCode.ShouldBe("folder_archived");
        archived.Identity.ContentHashReference.ShouldBe("sha256:a");
    }

    [Fact]
    public void FolderArchiveAfterFileRemoveShouldNotResurrectHardDeletedEntry()
    {
        // Hybrid-removal invariant (AC1/AC4): a file removal is a hard delete and must stay removed. A folder archive
        // arriving AFTER the remove (higher sequence) must NOT flip the already-tombstoned entry to "folder_archived",
        // because that routes it through the soft-delete egress and re-publishes (resurrects) the document the hard
        // delete already dropped. The entry's reason must stay folder_file_removed.
        SemanticIndexingBridgeEntry entry = SemanticIndexingBridgeProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Mutation()),
                new FolderProjectionEnvelope("tenant-a", 2, Mutation(fileOperationKind: "remove", contentHashReference: null)),
                new FolderProjectionEnvelope("tenant-a", 3, Archived()),
            ]).Entries.Values.ShouldHaveSingleItem();

        entry.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        entry.ReasonCode.ShouldBe("folder_file_removed");
    }

    [Fact]
    public void FolderArchiveShouldNotReindexEntryAlreadyHardDeletedAfterRemovalEvidenceRecorded()
    {
        // The realistic cross-batch sequence: a file is removed (tombstone folder_file_removed), then the removal egress
        // publishes SearchIndexEntryRemoved and records its outcome, OVERWRITING ReasonCode to "memories_accepted" while
        // freezing Status=Tombstoned. A later FolderArchived must still treat the entry as a hard delete and leave it
        // untouched — the guard keys off Status (Tombstoned), not the now-mutated ReasonCode.
        SemanticIndexingBridgeEntry tombstoned = Apply(Mutation(fileOperationKind: "remove", contentHashReference: null)).Entries.Values.ShouldHaveSingleItem();
        SemanticIndexingBridgeEntry afterRemovalEgress = SemanticIndexingBridgeProjection.ApplyRemovalEvidence(
            tombstoned,
            RemovalEvidence(tombstoned.Identity, "memories_accepted", watermark: tombstoned.Freshness.Watermark));
        afterRemovalEgress.ReasonCode.ShouldBe("memories_accepted");

        SemanticIndexingBridgeEntry afterArchive = SemanticIndexingBridgeProjection
            .FromEntries([afterRemovalEgress])
            .Apply([new FolderProjectionEnvelope("tenant-a", 2, Archived())])
            .Entries.Values.ShouldHaveSingleItem();

        // The archive did not re-route the already-removed entry through the soft-delete egress.
        afterArchive.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        afterArchive.ReasonCode.ShouldBe("memories_accepted");
    }

    [Fact]
    public void CommitEventsShouldAttachMetadataOnlyEvidenceWithoutMarkingIndexed()
    {
        SemanticIndexingBridgeEntry entry = SemanticIndexingBridgeProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Mutation()),
                new FolderProjectionEnvelope("tenant-a", 2, CommitSucceeded()),
            ]).Entries.Values.ShouldHaveSingleItem();

        entry.Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
        entry.CommitEvidence.ShouldNotBeNull();
        entry.CommitEvidence.Succeeded.ShouldBe(true);
        entry.CommitEvidence.ProviderOutcomeCategory.ShouldBe("provider_commit_accepted");
        entry.CommitEvidence.FailureCategory.ShouldBeNull();

        string json = JsonSerializer.Serialize(entry);
        json.ShouldNotContain("commit-reference-a", Case.Sensitive);
        json.ShouldNotContain("main", Case.Sensitive);
        json.ShouldNotContain("commit-message", Case.Sensitive);
    }

    [Fact]
    public async Task InMemoryBridgeStoreShouldApplyLaterFolderScopedEventsToKnownEntries()
    {
        InMemorySemanticIndexingBridgeStore store = new();
        WorkspaceFileMutationAccepted mutation = Mutation();
        SemanticIndexingFileVersionIdentity identity = SemanticIndexingFileVersionIdentity.From(mutation);

        await store.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 1, mutation)],
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        await store.ApplyFolderEventsAsync(
            [new FolderProjectionEnvelope("tenant-a", 2, Archived())],
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        SemanticIndexingBridgeEntry entry = (await store.GetFileVersionAsync(
            identity,
            TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull();
        entry.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        entry.ReasonCode.ShouldBe("folder_archived");
    }

    [Fact]
    public void IndexingResultShouldUpdateMatchingCurrentVersionOnly()
    {
        SemanticIndexingBridgeEntry current = Apply(Mutation()).Entries.Values.ShouldHaveSingleItem();
        SemanticIndexingBridgeEntry indexed = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            new SemanticIndexingResultUpdate(
                current.Identity,
                SemanticIndexingBridgeStatus.Indexed,
                "memories_accepted",
                retryable: false,
                "correlation-index-a",
                "task-index-a",
                "published-a",
                "result-fingerprint-a",
                OccurredAt.AddMinutes(5)));
        SemanticIndexingBridgeEntry staleResult = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            new SemanticIndexingResultUpdate(
                SemanticIndexingFileVersionIdentity.From(Mutation(contentHashReference: "sha256:b")),
                SemanticIndexingBridgeStatus.Indexed,
                "memories_accepted",
                retryable: false,
                "correlation-index-b",
                "task-index-b",
                "published-b",
                "result-fingerprint-b",
                OccurredAt.AddMinutes(5)));
        SemanticIndexingBridgeEntry tombstoned = Apply(Mutation(fileOperationKind: "remove", contentHashReference: null)).Entries.Values.ShouldHaveSingleItem();
        SemanticIndexingBridgeEntry ignoredAfterTombstone = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            tombstoned,
            new SemanticIndexingResultUpdate(
                tombstoned.Identity,
                SemanticIndexingBridgeStatus.Indexed,
                "memories_accepted",
                retryable: false,
                "correlation-index-c",
                "task-index-c",
                "published-c",
                "result-fingerprint-c",
                OccurredAt.AddMinutes(5)));

        indexed.Status.ShouldBe(SemanticIndexingBridgeStatus.Indexed);
        indexed.Evidence.PublishedEventId.ShouldBe("published-a");
        staleResult.ShouldBe(current);
        ignoredAfterTombstone.ShouldBe(tombstoned);
    }

    [Fact]
    public void OutOfOrderFolderScopedEventShouldNotOverwriteNewerWatermark()
    {
        // A file mutation lands at a high sequence, establishing watermark 5. A later-delivered but
        // lower-sequence FolderArchived (sequence 2) must be ignored by the watermark guard so a
        // re-delivered/out-of-order tombstone cannot regress a newer file-version state.
        SemanticIndexingBridgeProjection afterMutation = SemanticIndexingBridgeProjection.Empty.Apply(
            [new FolderProjectionEnvelope("tenant-a", 5, Mutation())]);

        SemanticIndexingBridgeProjection afterOutOfOrderArchive = afterMutation.Apply(
            [new FolderProjectionEnvelope("tenant-a", 2, Archived())]);

        SemanticIndexingBridgeEntry entry = afterOutOfOrderArchive.Entries.Values.ShouldHaveSingleItem();
        entry.Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
        entry.ReasonCode.ShouldBe("folders_file_version_changed");
        entry.Freshness.Watermark.ShouldBe(5);
    }

    [Fact]
    public void CommitFailedShouldAttachFailureEvidenceWithoutLeakingBranchOrAuthorMetadata()
    {
        SemanticIndexingBridgeEntry entry = SemanticIndexingBridgeProjection.Empty.Apply(
            [
                new FolderProjectionEnvelope("tenant-a", 1, Mutation()),
                new FolderProjectionEnvelope("tenant-a", 2, CommitFailed()),
            ]).Entries.Values.ShouldHaveSingleItem();

        // Commit failure is correlation/status evidence only; it must not flip the bridge to Failed.
        entry.Status.ShouldBe(SemanticIndexingBridgeStatus.Stale);
        entry.CommitEvidence.ShouldNotBeNull();
        entry.CommitEvidence.Succeeded.ShouldBe(false);
        entry.CommitEvidence.FailureCategory.ShouldBe("provider_rejected");
        entry.CommitEvidence.ProviderOutcomeCategory.ShouldBe("provider_commit_rejected");

        string json = JsonSerializer.Serialize(entry);
        json.ShouldNotContain("author-ref-a", Case.Sensitive);
        json.ShouldNotContain("release", Case.Sensitive);
        json.ShouldNotContain("commit-message", Case.Sensitive);
    }

    [Fact]
    public void UnsupportedEventTypeShouldFailLoudWithMetadataOnlyDiagnostics()
    {
        const string secretPayloadMarker = "raw-secret-payload";
        UnsupportedFolderEvent unsupported = new("tenant-a", secretPayloadMarker);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => SemanticIndexingBridgeProjection.Empty.Apply(
                [new FolderProjectionEnvelope("tenant-a", 7, unsupported)]));

        exception.Message.ShouldContain("unsupported event type");
        exception.Message.ShouldContain(typeof(UnsupportedFolderEvent).FullName!);
        exception.Message.ShouldContain("7");
        // Fail-loud diagnostics must stay metadata-only: no event payload contents leak into the message.
        exception.Message.ShouldNotContain(secretPayloadMarker, Case.Sensitive);
    }

    [Fact]
    public void IndexingResultShouldRecordFailedSkippedAndReconciliationOutcomes()
    {
        SemanticIndexingBridgeEntry current = Apply(Mutation()).Entries.Values.ShouldHaveSingleItem();

        SemanticIndexingBridgeEntry failed = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            ResultUpdate(current.Identity, SemanticIndexingBridgeStatus.Failed, "memories_rejected", retryable: true));
        SemanticIndexingBridgeEntry skipped = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            ResultUpdate(current.Identity, SemanticIndexingBridgeStatus.Skipped, "policy_too_large", retryable: false));
        SemanticIndexingBridgeEntry reconcile = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            ResultUpdate(current.Identity, SemanticIndexingBridgeStatus.ReconciliationRequired, "unknown_memories_outcome", retryable: true));

        failed.Status.ShouldBe(SemanticIndexingBridgeStatus.Failed);
        failed.StatusCode.ShouldBe("failed");
        failed.Retryable.ShouldBeTrue();
        skipped.Status.ShouldBe(SemanticIndexingBridgeStatus.Skipped);
        skipped.StatusCode.ShouldBe("skipped");
        skipped.Retryable.ShouldBeFalse();
        reconcile.Status.ShouldBe(SemanticIndexingBridgeStatus.ReconciliationRequired);
        reconcile.StatusCode.ShouldBe("reconciliation_required");
        reconcile.Evidence.PublishedEventId.ShouldBe("published-x");
    }

    [Fact]
    public void IndexingResultShouldIgnoreTenantOrFolderMismatch()
    {
        SemanticIndexingBridgeEntry current = Apply(Mutation()).Entries.Values.ShouldHaveSingleItem();
        SemanticIndexingFileVersionIdentity otherTenant = SemanticIndexingFileVersionIdentity.From(Mutation(managedTenantId: "tenant-b"));
        SemanticIndexingFileVersionIdentity otherFolder = SemanticIndexingFileVersionIdentity.From(Mutation(folderId: "folder-b"));

        SemanticIndexingBridgeEntry afterTenantMismatch = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            ResultUpdate(otherTenant, SemanticIndexingBridgeStatus.Indexed, "memories_accepted", retryable: false));
        SemanticIndexingBridgeEntry afterFolderMismatch = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            ResultUpdate(otherFolder, SemanticIndexingBridgeStatus.Indexed, "memories_accepted", retryable: false));

        afterTenantMismatch.ShouldBe(current);
        afterFolderMismatch.ShouldBe(current);
    }

    [Fact]
    public void RemoveShouldPreserveIndexTimeEvidenceForPreviouslyIndexedEntry()
    {
        // A previously-indexed file version carries its published cloudevent id + curated text/attributes as evidence.
        // The remove tombstone MUST preserve that evidence so the removal egress can target the exact upserted document
        // (decision (C)) and an archive re-send can reuse the original document (decision (A)).
        SemanticIndexingBridgeEntry current = Apply(Mutation()).Entries.Values.ShouldHaveSingleItem();
        SemanticIndexingBridgeEntry indexed = SemanticIndexingBridgeProjection.ApplyIndexingResult(
            current,
            new SemanticIndexingResultUpdate(
                current.Identity,
                SemanticIndexingBridgeStatus.Indexed,
                "memories_accepted",
                retryable: false,
                "correlation-index-a",
                "task-index-a",
                "folders://tenant-a/published-a",
                "result-fingerprint-a",
                OccurredAt.AddMinutes(5),
                indexedText: "authorized-file-version text",
                indexedAttributes: new Dictionary<string, string>(StringComparer.Ordinal) { ["folders.status"] = "active" }));

        SemanticIndexingBridgeEntry tombstoned = SemanticIndexingBridgeProjection
            .FromEntries([indexed])
            .Apply([new FolderProjectionEnvelope("tenant-a", 2, Mutation(fileOperationKind: "remove", contentHashReference: null))])
            .Entries.Values.ShouldHaveSingleItem();

        tombstoned.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        tombstoned.ReasonCode.ShouldBe("folder_file_removed");
        tombstoned.Evidence.PublishedEventId.ShouldBe("folders://tenant-a/published-a");
        tombstoned.Evidence.IndexedText.ShouldBe("authorized-file-version text");
        tombstoned.Evidence.IndexedAttributes.ShouldNotBeNull();
        tombstoned.Evidence.IndexedAttributes!["folders.status"].ShouldBe("active");
    }

    [Fact]
    public void ApplyRemovalEvidenceShouldRecordOutcomeAndFreezeTombstonedStatus()
    {
        SemanticIndexingBridgeEntry tombstoned = Apply(Mutation(fileOperationKind: "remove", contentHashReference: null)).Entries.Values.ShouldHaveSingleItem();

        SemanticIndexingBridgeEntry recorded = SemanticIndexingBridgeProjection.ApplyRemovalEvidence(
            tombstoned,
            RemovalEvidence(tombstoned.Identity, "memories_accepted", watermark: tombstoned.Freshness.Watermark));

        // Evidence-only: the status is frozen at Tombstoned (no regression to Indexed) and the outcome is recorded.
        recorded.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        recorded.StatusCode.ShouldBe("tombstoned");
        recorded.ReasonCode.ShouldBe("memories_accepted");
        recorded.Evidence.PublishedEventId.ShouldBe("folders://tenant-a/published-a");

        string json = JsonSerializer.Serialize(recorded);
        json.ShouldNotContain("file://", Case.Sensitive);
        json.ShouldNotContain("raw/path", Case.Sensitive);
    }

    [Fact]
    public void ApplyRemovalEvidenceShouldPreserveTombstoneIntentWhenRetryable()
    {
        SemanticIndexingBridgeEntry tombstoned = Apply(Mutation(fileOperationKind: "remove", contentHashReference: null)).Entries.Values.ShouldHaveSingleItem();

        SemanticIndexingBridgeEntry recorded = SemanticIndexingBridgeProjection.ApplyRemovalEvidence(
            tombstoned,
            RemovalEvidence(tombstoned.Identity, "memories_publish_error", tombstoned.Freshness.Watermark, retryable: true));

        recorded.Status.ShouldBe(SemanticIndexingBridgeStatus.Tombstoned);
        recorded.ReasonCode.ShouldBe("folder_file_removed");
        recorded.Retryable.ShouldBeTrue();
    }

    [Fact]
    public void ApplyRemovalEvidenceShouldIgnoreStaleWatermark()
    {
        SemanticIndexingBridgeEntry tombstoned = SemanticIndexingBridgeProjection.Empty.Apply(
            [new FolderProjectionEnvelope("tenant-a", 5, Mutation(fileOperationKind: "remove", contentHashReference: null))]).Entries.Values.ShouldHaveSingleItem();

        SemanticIndexingBridgeEntry ignored = SemanticIndexingBridgeProjection.ApplyRemovalEvidence(
            tombstoned,
            RemovalEvidence(tombstoned.Identity, "memories_accepted", watermark: 2));

        // An older/out-of-order removal result must not overwrite a newer file-version state.
        ignored.ShouldBe(tombstoned);
    }

    [Fact]
    public void ApplyRemovalEvidenceShouldNotResurrectNonTombstonedEntry()
    {
        SemanticIndexingBridgeEntry stale = Apply(Mutation()).Entries.Values.ShouldHaveSingleItem();

        SemanticIndexingBridgeEntry unchanged = SemanticIndexingBridgeProjection.ApplyRemovalEvidence(
            stale,
            RemovalEvidence(stale.Identity, "memories_accepted", watermark: stale.Freshness.Watermark));

        unchanged.ShouldBe(stale);
    }

    [Fact]
    public void StatusVocabularyShouldExposeStableCodes()
    {
        SemanticIndexingBridgeStatus.Unknown.ToStatusCode().ShouldBe("unknown");
        SemanticIndexingBridgeStatus.Indexed.ToStatusCode().ShouldBe("indexed");
        SemanticIndexingBridgeStatus.Stale.ToStatusCode().ShouldBe("stale");
        SemanticIndexingBridgeStatus.Skipped.ToStatusCode().ShouldBe("skipped");
        SemanticIndexingBridgeStatus.Failed.ToStatusCode().ShouldBe("failed");
        SemanticIndexingBridgeStatus.Tombstoned.ToStatusCode().ShouldBe("tombstoned");
        SemanticIndexingBridgeStatus.ReconciliationRequired.ToStatusCode().ShouldBe("reconciliation_required");
    }

    private static SemanticIndexingBridgeProjection Apply(WorkspaceFileMutationAccepted mutation)
        => SemanticIndexingBridgeProjection.Empty.Apply([new FolderProjectionEnvelope("tenant-a", 1, mutation)]);

    private static WorkspaceFileMutationAccepted Mutation(
        string fileOperationKind = "add",
        string? contentHashReference = "sha256:a",
        string managedTenantId = "tenant-a",
        string folderId = "folder-a")
        => new(
            managedTenantId,
            "organization-a",
            folderId,
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

    private static WorkspaceCommitSucceeded CommitSucceeded()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            FolderWorkspaceLifecycleEvent.CommitSucceeded,
            "operation-commit-a",
            "commit-reference-a",
            "provider_commit_accepted",
            "author-ref-a",
            "main",
            "commit-message-safe",
            "path-digest-a",
            "principal-a",
            "correlation-commit-a",
            "task-commit-a",
            "idempotency-commit-a",
            "fingerprint-commit-a",
            OccurredAt.AddMinutes(1));

    private static WorkspaceCommitFailed CommitFailed()
        => new(
            "tenant-a",
            "organization-a",
            "folder-a",
            "workspace-a",
            FolderWorkspaceLifecycleEvent.CommitFailed,
            "operation-commit-a",
            "provider_rejected",
            "provider_commit_rejected",
            "author-ref-a",
            "release",
            "commit-message-safe",
            "path-digest-a",
            "principal-a",
            "correlation-commit-a",
            "task-commit-a",
            "idempotency-commit-a",
            "fingerprint-commit-a",
            OccurredAt.AddMinutes(1));

    private static SemanticIndexingResultUpdate ResultUpdate(
        SemanticIndexingFileVersionIdentity identity,
        SemanticIndexingBridgeStatus status,
        string reasonCode,
        bool retryable)
        => new(
            identity,
            status,
            reasonCode,
            retryable,
            "correlation-result-x",
            "task-result-x",
            "published-x",
            "result-fingerprint-x",
            OccurredAt.AddMinutes(5));

    private static SemanticIndexingRemovalEvidenceUpdate RemovalEvidence(
        SemanticIndexingFileVersionIdentity identity,
        string reasonCode,
        long watermark,
        bool retryable = false)
        => new(
            identity,
            reasonCode,
            retryable,
            "correlation-removal-x",
            "task-removal-x",
            "folders://tenant-a/published-a",
            "result-fingerprint-removal-x",
            watermark,
            OccurredAt.AddMinutes(6));

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
            OccurredAt.AddMinutes(2));

    private sealed record UnsupportedFolderEvent(string ManagedTenantId, string SecretPayload) : IFolderEvent
    {
        public string OrganizationId => "organization-a";

        public string FolderId => "folder-a";

        public string CorrelationId => "correlation-unsupported-a";

        public string TaskId => "task-unsupported-a";

        public string IdempotencyKey => "idempotency-unsupported-a";

        public string IdempotencyFingerprint => "fingerprint-unsupported-a";

        public DateTimeOffset OccurredAt => SemanticIndexingBridgeProjectionTests.OccurredAt.AddMinutes(3);
    }
}
