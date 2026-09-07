---
title: 'Story 10.7: EventStore-backed search bridge and deployed Server registration'
type: 'feature'
created: '2026-09-07'
status: 'done'
baseline_commit: 'ee80dd0d762a7d251dc80746651350bd35fa7d02'
route: 'dispatch'
review_loop_iteration: 0
story_key: '10-7-eventstore-backed-search-bridge-and-deployed-server-registra'
context:
  - '_bmad-output/implementation-artifacts/epic-10-context.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Deployed Server hydrates search/status through `UnavailableSemanticIndexingBridgeReadModel`, so Memories hits become `Allowed` + zero items and indexing-status is `ReadModelUnavailable`. The durable `IReadModelStore` adapter exists only in Workers, which Server must not reference.

**Approach:** Move `EventStoreSemanticIndexingBridgeStore` into `Hexalith.Folders` with `Hexalith.EventStore.Client`, register it from `AddFoldersContextSearchFacade` instead of Unavailable (Workers keep the same type as writer), and prove empty-checkpoint replay plus restart reload on persisted `statestore` keys. Public wires, authorization, and C9 stay unchanged. Story 10.8 remains the FR58 live round-trip.

## Decisions

- **Home (Q1-A):** The store type lives in `src/Hexalith.Folders/Projections/SemanticIndexing/`. Add `Hexalith.EventStore.Client` to packable `Hexalith.Folders`. Update `ScaffoldContractTests`. No new library. No Server/Workers type split.
- **Done-bar (Q2-A):** Done means relocate, Server registration, and hermetic empty-checkpoint/restart against an in-process `IReadModelStore` double. DCP `aspire run` and Stories 12.1–12.3 stay with Story 10.8. Unavailable/safe-empty is not completion.

## Boundaries & Constraints

**Always:** Persist only via `IReadModelStore` + `ReadModelWritePolicy` on Dapr `statestore` with today's tenant-prefixed keys. Authorize before any bridge read. Hydration drops wrong-tenant, unauthorized, non-live, removed, archived/tombstoned, conflict/corrupt, and identity-mismatch hits. Store timeout → `ReadModelUnavailable`. Metadata-only. EventStore adapter is the only `IsAvailable` true implementation. Keep Unavailable as the un-overridden core `TryAdd` default. Wire-preserve REST/SDK/CLI/MCP (`ImplementedRestOperationCount = 49`). Core stays Memories-free. One type per file; XML docs on public members.

**Never:** Server→Workers reference. Hand-rolled Dapr state or EF. Treat safe-empty, in-memory, seed, fake, NoOp, or Unavailable as done. Change Memories invoke. Implement 10.8, 10.9, or 12.1–12.3. Rename `Projections/SemanticIndexing`. Edit generated SDK. Leak paths, bodies, snippets, source URIs, or hidden existence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Empty-checkpoint replay | Empty store; mutation then archive envelopes | Version/status/removal records persist | Tenant-mismatched envelope writes nothing |
| Restart | New adapter over the same backing store | Reloaded entries match keys/status | N/A |
| Server registration | `AddFoldersContextSearchFacade` | Read model is EventStore store; `IsAvailable` true | Unavailable remains the core default |
| Authorized hydration | Allowed caller; Indexed/Stale entry | Hit kept after `GetFileVersionByIdAsync` | Auth first; denial is safe-denial |
| Dropped candidates | Tombstoned/wrong-scope/missing | Hit dropped; no existence leak | Throw → `ReadModelUnavailable` |
| Duplicate delivery | Same ordered events twice | Equivalent state; no duplicate keys | N/A |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Workers/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` — current home; move to `src/Hexalith.Folders/Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs`. `StateStoreName = "statestore"`; read + apply/index/remove writes.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:61-76` — `AddEventStoreReadModelStore()`; `RemoveAll`; singleton as read+write.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:148-167` — queries `TryAddScoped` Unavailable; in-memory bridge for hermetic core (keep).
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:88-127` — facade wires Memories then queries; bridge still Unavailable. Caller `FoldersServerModule.cs:74`.
- `src/Hexalith.Folders/Projections/SemanticIndexing/UnavailableSemanticIndexingBridgeReadModel.cs` — keep fail-safe.
- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs` — auth then `GetFileVersionByIdAsync`; `IsVisible` = Indexed\|Stale. Do not change behavior.
- `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQueryHandler.cs:78-96` — `IsAvailable` false / throw → `ReadModelUnavailable`.
- `src/Hexalith.Folders/Hexalith.Folders.csproj` — add `Hexalith.EventStore.Client` (FromSource project ref / package, same pattern as Server).
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:171-178` — today Folders → Contracts only; pin EventStore.Client. Server already has EventStore.Client.
- `tests/Hexalith.Folders.Workers.Tests/EventStoreSemanticIndexingBridgeStoreTests.cs` — persist/archive; no second-instance restart proof yet.
- `tests/Hexalith.Folders.Server.Tests/FoldersContextSearchFacadeRegistrationTests.cs` — Memories URL only.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders/Hexalith.Folders.csproj` — add `Hexalith.EventStore.Client` (FromSource condition, same as Server). No Memories package.
- [x] `src/Hexalith.Folders/Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` — move from Workers; namespace `Hexalith.Folders.Projections.SemanticIndexing`; keep keys/write policy; add XML docs. Delete the Workers copy.
- [x] `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` — keep `RemoveAll` + singleton read+write of the moved type.
- [x] `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` — after queries: `AddEventStoreReadModelStore()`, `RemoveAll<ISemanticIndexingBridgeReadModel>()`, singleton EventStore read model. No writer on Server. Update the comment.
- [x] `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` — pin Folders → EventStore.Client + Contracts. No Memories on core.
- [x] `tests/Hexalith.Folders.Workers.Tests/EventStoreSemanticIndexingBridgeStoreTests.cs` — empty-checkpoint apply; new instance reloads version/status/removal; duplicate delivery equivalent; tenant mismatch writes nothing. Update usings.
- [x] `tests/Hexalith.Folders.Server.Tests/FoldersContextSearchFacadeRegistrationTests.cs` — facade resolves EventStore store, `IsAvailable` true, ValidateOnBuild/ValidateScopes.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — set `10-7-eventstore-backed-search-bridge-and-deployed-server-registra` in-progress then done on hermetic evidence. Do not wait for DCP or 12.1–12.3.

**Acceptance Criteria:**
- Given Stories 10.2 and 10.6, when the EventStore bridge is Server-referenceable and registered in `AddFoldersContextSearchFacade`, then the deployed Server default is not Unavailable and `IsAvailable` is true.
- Given an empty `IReadModelStore`, when mutation then archive envelopes are applied and a new instance reads the same keys, then version/status/removal records reload without duplicate-delivery corruption.
- Given an authorized search/status call, when hydration runs, then authorization preceded observation and non-live/wrong-tenant/missing/timeout cases are dropped or classified with metadata-only evidence.
- Given hermetic restart and Server registration evidence, when this story is marked done, then NoOp, in-memory core default, seed, fake, Unavailable, or safe-empty alone is not completion, and DCP/12.1–12.3 remain 10.8.

## Implementation Notes

Hermetic evidence 2026-09-07:

- Relocated `EventStoreSemanticIndexingBridgeStore` into packable `Hexalith.Folders` with `Hexalith.EventStore.Client` (FromSource/package, same as Server). No Memories on core.
- `AddFoldersContextSearchFacade` now overrides the core Unavailable `TryAdd` after `AddFoldersContextSearchQueries` via `AddEventStoreReadModelStore` + `RemoveAll` + singleton read model. No writer on Server. Workers still register the same type as read+write.
- Empty-checkpoint mutation+archive, second-instance reload of version/status/removal, duplicate-delivery key equivalence, and tenant-mismatch no-write are covered in Workers.Tests. Server facade ValidateOnBuild/ValidateScopes resolves EventStore with `IsAvailable` true; core queries still resolve Unavailable.
- 2026-09-07 review patch: `GetFileVersionAsync` / `GetFileVersionByIdAsync` return null when the stored identity does not match the lookup, matching `ListFolderAsync`. Workers store tests 10/10.

## Spec Change Log

- 2026-09-07: Implemented Story 10.7 (EventStore-backed search bridge + deployed Server registration). Public wires, authorization, and C9 unchanged. DCP/`aspire run` and Stories 12.1–12.3 remain Story 10.8.

## Review Triage Log

- `false` — Blind: sprint `done` vs spec `in-review` vs epic deployed-evidence AC. Frozen Q2-A makes hermetic restart the done-bar; DCP/12.x stay 10.8. Spec `in-review` is step-04; sprint `done` matches the spec task.
- `low` — Blind: `architecture.md` Query Facade still narrates the pre-10.7 Unavailable default. Rejected: the same paragraph already assigns 10.7 the relocate/register job; fixing planning-manifest/architecture is more than a direct code correction and is not everyday user-facing.
- `false` — Blind: rewrite the spec Code Map. Rejected: findings whose fix is to edit this build's spec are out of triage.
- `medium` — Blind/edge: `GetFileVersionAsync` / `GetFileVersionByIdAsync` return `result.Value` with no identity match. `ListFolderAsync` already filters poisoned index rows; search hydration uses the unguarded point-get and does not re-check tenant/folder/file-version on the entry. A swapped payload at the tenant-prefixed key can be returned. Patch: return null when identity does not match the lookup.
- `defer` — Blind/edge: `ApplyFolderScopedEventAsync` does not apply the `ListFolderAsync` tenant/folder filter. Pre-existing write path; Server does not register the writer.
- `low` — Blind: nested `SemanticIndexingBridgeFolderIndex` and interpolated folder-index key. Rejected: private nested storage DTO; folder-index string must stay today's key. Extracting a type or adding a key helper would add files/API without changing behavior.
- `defer` — Blind: tombstoned keys stay in the folder index (N+1 growth). Pre-existing write/index behavior; not introduced by the Server read registration.
- `false` — Blind: Server registers the writable concrete type. Spec required `TryAddSingleton<EventStoreSemanticIndexingBridgeStore>` mapped only to `ISemanticIndexingBridgeReadModel`; tests assert `ISemanticIndexingBridgeWriter` is null; handlers inject the interface.
- `false` — Blind: adapter tests live in Workers.Tests and facade tests do not resolve handlers. Spec Design Notes keep adapter tests there; handler I/O-matrix rows are covered by Folders.Tests that ran 1951/1951.
- `defer` — Blind: `Project` throws with the raw read-model key. Pre-existing write-path exception; Server read registration does not call `Project`.
- `false` — Blind: `IsAvailable` is hardcoded true. Frozen Always: the EventStore adapter is the only `IsAvailable` true implementation; store throws still map to `ReadModelUnavailable`.
- `false` — Edge: null `FolderProjectionEnvelope.Event`. `Event` is non-nullable `IFolderEvent`; a null event is not a legal envelope.
- `defer` — Edge: deserialized folder index with null `EntryKeys`. Pre-existing DTO; writers always construct `[]`. Poisoned JSON only.

## Design Notes

Override **after** `AddFoldersContextSearchQueries()` (`TryAddScoped` Unavailable). Mirror Workers: `RemoveAll` then singleton. Server is read-only.

Hermetic restart: two adapters, one store double. First applies from empty; second reads the same keys. Not an Aspire recycle.

Keep adapter tests in `Hexalith.Folders.Workers.Tests` (already has the in-memory `IReadModelStore` double). Keep the `SemanticIndexing` folder; do not rename to `Projections/Search/`.

## Verification

**Commands:**
- `dotnet restore Hexalith.Folders.slnx -p:NuGetAudit=false` && `dotnet build Hexalith.Folders.slnx --no-restore` — 0 errors
- Focused: Workers.Tests, Server.Tests, Folders.Tests, Testing.Tests — all pass including new restart/registration facts
- `dotnet format Hexalith.Folders.slnx whitespace --verify-no-changes --include src tests` — no Folders-owned diffs
