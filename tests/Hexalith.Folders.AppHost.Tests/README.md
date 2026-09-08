# Hexalith.Folders.AppHost.Tests

Tier-3 Aspire integration harness. It boots the full Folders `AppHost` topology through
`DistributedApplicationTestingBuilder` and proves the cross-process publish/subscribe wiring comes up together:

- `eventstore` publishes managed-tenant folder domain events to the single `folders.events` topic (Story 10.3 D1
  `EventStore:Publisher:TopicOverrides:folders` override).
- `folders-workers` subscribes `folders.events` on `/folders/events` (semantic indexing) and `memories-events`.
- `memories` subscribes `memories-events` (the curated search index).

Story 10.6 replaced the fail-closed content materializer with
`MetadataDerivedSemanticIndexingContentMaterializer`. Curated index text is the approved metadata-token
vocabulary (type class, fileVersionId, organizationId, folderId, size class).

## Running the harness

These tests require a **DCP-capable host**: Docker running plus a Dapr runtime (`dapr init`). They are **opt-in**
and skip everywhere else so the hermetic full-solution lane stays green without infrastructure:

```bash
HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet test tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj
```

Without the variable set, `AspireFoldersAppHostFixture` reports unavailable and diagnostic tests call
`SkipIfUnavailable()`. The wider environment currently has a known Aspire CLI/DCP boot mismatch (Epic 9 residual);
Story 11.15 owns the dedicated DCP-capable verification lane.

## Diagnostic tests versus Story 10.8 / FR58 acceptance

`EventStoreSidecarShouldPublishFolderEnvelopeToWorkerSubscriberTopic` and
`SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` are **component diagnostics**. The first hand-publishes a
`WorkspaceFileMutationAccepted` envelope. The second seeds `SearchIndexEntryChanged` and queries Memories
directly. Neither is FR58 completion evidence.

Story 10.8 acceptance is `Fr58PublicMutationSearchRoundTripTests`: an authenticated public
`AddWorkspaceFile` / `ChangeWorkspaceFile` call, then polling **only** public Folders search and indexing-status.
It must not seed the index, hand-publish domain envelopes, fake ACL/gateway, or query Memories for acceptance.
A governed run (`HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true`) that skips this scenario is a failure.

Live DCP/OQ5 stays incomplete while Task 0 prerequisites are missing: Stories 12.1–12.3 and 12.5, DW-292
effective-permissions population, a production EventStore authorization validator (DenyAll is not a pass),
Story 12.6 admission ≠ event emission (`PayloadNoOpDomainResult`), and Story 11.15. Facade and CLI work may
land independently.

## Story 10.4 AC9 — seeded index lifecycle diagnostic

`SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` proves the live publish → route → index → search → remove path
against the real `folders-index` by **seeding** `SearchIndexEntryChanged` through the worker pub/sub component:

1. a syntactic `GET /api/search?tenantId=folders-index&axis=syntactic&query=…` returns exactly one hit whose
   `ScoredResult.SourceUri` echoes the published `cloudevent.id`;
2. after a `SearchIndexEntryRemoved`, the search returns zero hits (no stale entry);
3. after a `SearchIndexEntryChanged{folders.status=archived}`, the document remains and is filterable as archived
   (and no longer matches the active filter).

It runs **only** on a DCP-capable lane (it resolves the `folders-workers` Dapr sidecar to publish, and skips cleanly
if that endpoint is unavailable).

## Extending

`AspireFoldersAppHostFixture` is reusable. Do not promote diagnostic seeding or direct Memories queries to
Story 10.8 acceptance.
