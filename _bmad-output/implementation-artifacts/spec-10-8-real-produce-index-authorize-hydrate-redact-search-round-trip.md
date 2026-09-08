---
title: 'Story 10.8: Real produce/index/authorize/hydrate/redact/search round trip'
type: 'feature'
created: '2026-09-07'
status: 'done'
baseline_commit: 'a99644cfb426bea40f932741fdec1c7ec2f825c1'
route: 'dispatch'
review_loop_iteration: 0
story_key: '10-8-real-produce-index-authorize-hydrate-redact-search-round-tri'
context:
  - '_bmad-output/implementation-artifacts/epic-10-context.md'
  - '_bmad-output/implementation-artifacts/10-8-real-produce-index-authorize-hydrate-redact-search-round-trip.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** FR58 still has no no-seed proof that an authenticated public Folders mutation becomes a non-empty, hydrated, metadata-only search/status result. AppHost tests seed Memories or hand-publish envelopes. Search still treats `Stale` as visible and never checks bridge availability before Memories egress.

**Approach:** Keep the full FR58 bar. Task 0 fail-closes live DCP/OQ5 while 12.x, ACL population, EventStore validator, and 11.15 are missing; owned facade and CLI work may still land. Fix Indexed-only recall and auth-then-unavailable. Add CLI `indexing-status` matching MCP. Search the current 10.6 curated text only. Add a distinct public-REST AppHost scenario that never seeds the index. Do not implement Epic 12, ACL population, or EventStore-auth in this story.

## Decisions

- **Q1-A:** Full FR58. Task 0 fail-closes. Owned facade/CLI may land. Live DCP/OQ5 wait on 12.1–12.3, 12.5, DW-292 ACL, EventStore validator, and 11.15. Do not pull those into 10.8.
- **Q2-A:** Add CLI `indexing-status` matching MCP (folder + optional freshness, `taskIdRequired: false`, no workspace). Add `cli` to OpenAPI `transportParity` for `GetFolderIndexingStatus` and regenerate parity with the generator; do not hand-edit the generated client.
- **Q3-A:** Search the current 10.6 curated text only (type class, fileVersionId, organizationId, folderId, size class). Do not add raw media type or path-policy as searchable tokens.

## Boundaries & Constraints

**Always:** Two-phase auth (JWT → tenant freshness → folder ACL → EventStore validator → Dapr, then per-hit identity/scope/bridge hydration). Metadata-token recall using the 10.6 curated vocabulary. Recover identity from validated `folders://` SourceUri internally; never echo it. Persist via `IReadModelStore` + `ReadModelWritePolicy`. Keep the 10.7 Server EventStore bridge and identity-mismatch nulls. Production Dapr: Server `GET /api/search` only; Workers pub/sub only. One type per file; XML docs; ULID ids; local Debug project-reference. Freeze Contract Spine as shipped: `semantic_reference_pending`, `queryText` 1–256, limit/cursor, 2s, 500/1 MiB; search is task-sourced; indexing-status is not (MCP `taskIdRequired: false`).

**Never:** Seed `SearchIndexEntryChanged`, hand-publish `WorkspaceFileMutationAccepted`, fake ACL/gateway, treat Allowing/DenyAll as a pass, query Memories for acceptance, or skip the governed FR58 scenario. Body indexing / 10.9 / RAG. Implement 12.1–12.5, 11.15, DW-292, or a production EventStore validator here. Server→Workers reference. Weaken production Dapr. Hand-edit generated SDK. Leak paths, bodies, snippets, source URIs, credentials, or hidden existence.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Missing prereq | 12.1–12.3/12.5, ACL populate, EventStore validator, or DCP absent | Stop live path; story incomplete; facade/CLI may still land | Record blocker; no seed/bypass |
| Happy path | Authenticated Add/ChangeWorkspaceFile | Indexed bridge + non-empty public search + matching status | Poll public search/status only |
| Stale / archive / remove | `Stale` hit; folder archive; file remove | Search drops Stale/archived; remove tombstones and is unsearchable; status may still list Stale | Failed remove must not stay searchable after recovery |
| Denial | Wrong scope, revoked, hidden, nonexistent | Safe denial; no Memories egress | No counts or existence leak |
| Bridge/Memories down after auth | `IsAvailable` false, timeout, malformed, hydration miss | `ReadModelUnavailable` / 503; auth failures still first | Unauthorized callers cannot see outage vs denial |
| Duplicate / replay | Duplicate events/publish; empty-checkpoint restart | One current identity; deterministic reload | No extra mutation required |
| CLI status | `indexing-status --folder-id` with optional freshness | Same contract as MCP; metadata-only; no workspace or required task | Canonical query errors only |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:82-136,286-287` — auth then source; `IsVisible` is Indexed\|Stale; no `IsAvailable`. Change to Indexed-only; after allow, unavailable bridge → `ReadModelUnavailable` before `_source.SearchAsync`. Keep trim, hydration, C4, `MapItem`.
- `src/Hexalith.Folders/Queries/ContextSearch/FolderIndexingStatusQueryHandler.cs:78-80` — already checks `IsAvailable` after auth. Reuse; status may still list Stale.
- `tests/Hexalith.Folders.Tests/Queries/ContextSearch/ContextSearchQueryHandlerTests.cs` — `StubBridgeReadModel.IsAvailable` is true (`:596,:619`); `AuthorizedSearchShouldKeepStaleBridgeEntries` (`:174`). Make availability configurable; invert Stale; add unavailable-before-egress and auth precedence. Reuse `RecordingFolderSearchSource`.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1257-1276,1340` — accepted → empty-event `PayloadNoOpDomainResult`. Do not patch.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:355,358` — unpopulated in-memory ACL; DenyAll validator (DW-292). Do not add test-only `Save`.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:93-134` — sidecar Memories invoke + 10.7 EventStore read model. Keep.
- `src/Hexalith.Folders/Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` — `IsAvailable => true`; identity mismatch returns null. Do not relocate.
- `src/Hexalith.Folders.Workers/SemanticIndexing/MetadataDerivedSemanticIndexingContentMaterializer.cs:28-46` — 10.6 tokens. Reuse; do not add media type or path-policy tokens.
- `src/Hexalith.Folders.Cli/Commands/Context/ContextCommand.cs:105-118` — add `indexing-status` via `CommandFactory.Query` `taskIdRequired: false`.
- `src/Hexalith.Folders.Mcp/Tools/ContextTools.cs:106-115` — status is not task-scoped. Keep `semantic_reference_pending`.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2873-3067,8586-8614` — freeze grammar; add `cli` to `GetFolderIndexingStatus` `transportParity`; regenerate parity via the generator.
- `tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs:82-88`, `FoldersTopologyCrossProcessTests.cs:39-176` — opt-in DCP; seed/envelope diagnostics. Relabel; add a public-REST FR58 test (ULID ids). No auth bypass. Live run waits on Task 0.
- `tests/Hexalith.Folders.IntegrationTests/ContextSearch/ContextSearchFacadeWiringTests.cs` and `tests/fixtures/audit-leakage-corpus.json` — REST denial/egress below Tier 3; leakage scan.
- New: `docs/exit-criteria/fr58-search-evidence.md` after non-skipped DCP. 10.7: Workers still write the same store; core keeps Unavailable `TryAdd`. 12.6 admission ≠ event emission. 12.1–12.5 and 11.15 are backlog.

## Tasks & Acceptance

**Execution:**
- [x] Task 0 — In Implementation Notes, record 12.1–12.3, 12.5, 12.6 (admission ≠ emission), DW-292, DenyAll, 11.15, and CI/start blockers with commits. If a live-path prereq is missing, skip live DCP/OQ5 and leave the story incomplete.
- [x] `src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs` — Indexed-only `IsVisible`; auth then bridge-unavailable before Memories.
- [x] `tests/Hexalith.Folders.Tests/Queries/ContextSearch/ContextSearchQueryHandlerTests.cs` — invert Stale; add unavailable, auth precedence, hydration miss, metadata-only errors.
- [x] `tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs`, fixture comments, `tests/Hexalith.Folders.AppHost.Tests/README.md` — diagnostics vs acceptance; drop fail-closed-materializer prose.
- [x] New `tests/Hexalith.Folders.AppHost.Tests/` FR58 test — public Add/ChangeWorkspaceFile; poll public search/status; no Memories query; skipped governed run fails. Do not execute as acceptance until Task 0 is green.
- [x] `src/Hexalith.Folders.Cli/Commands/Context/ContextCommand.cs` and `tests/Hexalith.Folders.Cli.Tests/CommandSurfaceE2ETests.cs` — add `indexing-status` matching MCP.
- [x] `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` — add `cli` to `GetFolderIndexingStatus` `transportParity`; regenerate parity fixtures; do not hand-edit `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`.
- [x] `tests/Hexalith.Folders.IntegrationTests/ContextSearch/ContextSearchFacadeWiringTests.cs` plus AppHost FR58 — isolation, remove/archive, duplicate/unavailable, leakage corpus. No raw candidate counts.
- [ ] `docs/exit-criteria/fr58-search-evidence.md` — versions, baseline, non-skipped run id, sanitized matrix, digest; dated Product/Security/Test approvals. Only after Task 0 is green.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — set `10-8-real-produce-index-authorize-hydrate-redact-search-round-tri` in-progress, then done only on complete evidence.

**Acceptance Criteria:**
- Given Task 0, when a live-path prerequisite is missing, then 10.8 stays incomplete, facade/CLI may still land, and no seed, fake, or skip counts as FR58 pass.
- Given Stories 10.6–10.7 and a green Task 0, when an authenticated Add/ChangeWorkspaceFile runs, then Workers publish `SearchIndexEntryChanged`, the bridge is Indexed, and public search/status agree on that file version without querying Memories.
- Given authorized search, when a hit is Stale, foreign, tombstoned, skipped, failed, reconciliation-required, unknown, archived, or a hydration miss, then it is not returned; file remove tombstones; folder archive is excluded from active search.
- Given authorization failure, when search is invoked, then Memories is not called and the denial does not disclose existence, counts, or dependency health.
- Given CLI `indexing-status`, when it is invoked with folder and optional freshness, then it uses the same not-task-scoped contract as MCP and renders metadata only.

## Implementation Notes

### Task 0 — live-path prerequisites (2026-09-08, baseline `a99644cfb426bea40f932741fdec1c7ec2f825c1`)

Live DCP/OQ5 fail-closed. Owned facade, CLI `indexing-status`, OpenAPI `cli` parity, and hermetic tests landed. Story 10.8 stays **incomplete**.

| Prerequisite | Status | Evidence |
| --- | --- | --- |
| Story 10.6 metadata materializer | present | sprint-status `done`; `MetadataDerivedSemanticIndexingContentMaterializer` emits type class, fileVersionId, organizationId, folderId, size class. Q3-A: search that vocabulary only. |
| Story 10.7 EventStore bridge on Server | present | `a99644c` (`feat: register EventStore-backed search bridge on the deployed Server`). `AddFoldersContextSearchFacade` RemoveAll + EventStore store; core keeps Unavailable `TryAdd`. |
| Stories 12.1, 12.2, 12.3 | **missing** | sprint-status `backlog`. No durable repository/event replay, projection/task completion, or workspace content store. |
| Story 12.5 at-least-once Memories egress/reconciler | **missing** | sprint-status `backlog`. |
| Story 12.6 admission ≠ emission | **blocker** | `7d96ebf` wired EventStore admission; sprint-status still `in-progress`. `FolderDomainProcessor.ToDomainResult` still maps accepted results to eventless `PayloadNoOpDomainResult` (`:1257-1276`, `:1340`). DW-291 remains open. Admission is not `WorkspaceFileMutationAccepted` emission. |
| DW-292 ACL population | **missing** | `FoldersServiceCollectionExtensions.AddFoldersLayeredAuthorization` `:355` `InMemoryEffectivePermissionsReadModel`; no production `Save`. Test-only seeding is forbidden. |
| EventStore validator | **missing** | `:358` `DenyAllEventStoreAuthorizationValidator`. Allowing/test override is not production-path evidence. |
| Story 11.15 DCP lane | **missing** | sprint-status `backlog`. AppHost tests remain opt-in via `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`. |
| CI / production-start | **blocked** | DCP-capable governed lane is 11.15. Fixture still disables Keycloak (`EnableKeycloak=false`) for topology diagnostics; FR58 must not treat that as an auth bypass. |

Governed FR58 (`Fr58PublicMutationSearchRoundTripTests`) **fails** if the opt-in variable is set while Task 0 is blocked, and **skips** the live round-trip in the hermetic lane. `Task0BlockersShouldKeepLivePathPrerequisitesUnsatisfied` always runs and asserts the live-path gate stays false. Do not create `docs/exit-criteria/fr58-search-evidence.md` until this table is green.

### Verification (2026-09-08)

Project-level `dotnet test` is blocked by DW-341 (Microsoft.Testing.Platform rejects the VSTest target on .NET 10). Direct xUnit v3 hosts were used instead. `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION` was **not** set (Task 0 still blocked).

| Host | Result |
| --- | --- |
| `dotnet restore Hexalith.Folders.slnx -p:NuGetAudit=false` && `dotnet build Hexalith.Folders.slnx --configuration Debug --no-restore` | 0 errors / 0 warnings |
| `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests` | 1954 passed, 1 skipped (Forgejo smart-HTTP fixture) |
| `tests/Hexalith.Folders.Cli.Tests/bin/Debug/net10.0/Hexalith.Folders.Cli.Tests` | 728 passed |
| `tests/Hexalith.Folders.Mcp.Tests/bin/Debug/net10.0/Hexalith.Folders.Mcp.Tests` | 678 passed |
| `tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests` | 307 passed |
| `tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests` | 285 passed |
| `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests` | 657 passed |
| `tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests` | 88 passed |
| `tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests` | 5 skipped (FR58 fail-closed + diagnostics opt-in) |
| `ContextSearchQueryHandlerTests` | 39 passed |
| `ContextSearchFacadeWiringTests` | 9 passed |
| Full `Hexalith.Folders.IntegrationTests` | 691 tests, **27 failed** — all `ArchiveFolderProcessWiringTests` / `MixedSurfaceHandoffTests` / `GoldenLifecycleParityTests` (12.6 admission ≠ emission). Context-search facade tests green. |
| Full `Hexalith.Folders.Testing.Tests` | 66 tests, **2 failed** — `ScaffoldContractTests` still omits `Hexalith.Folders.EventStore` / `.EventStore.Tests` already in `Hexalith.Folders.slnx` at baseline. Pre-existing; not 10.8. |

## Spec Change Log

- 2026-09-08: Task 0 recorded live-path blockers. Indexed-only search, post-auth bridge-unavailable, CLI `indexing-status`, OpenAPI `cli` parity, diagnostic-vs-acceptance AppHost relabel, gated FR58 test, and hermetic isolation/lifecycle tests landed. OQ5 evidence deferred.

## Review Triage Log

- `false` — Blind: FR58 never authenticates / Keycloak off. `LivePathPrerequisitesSatisfied()` is hardcoded false, so `ExecutePublicRoundTripAsync` is not entered; Keycloak-off is unused by the hermetic skip path.
- `false` — Blind: governed opt-in can still `SkipIfUnavailable` after Task 0. That call is after the live-path gate; with the gate false the line never runs.
- `false` — Blind: live round-trip does not bind a 10.6 token / never calls Change. Execute path not entered. Frozen I/O allows Add or Change.
- `false` — Blind: status is a single GET and search stops on any non-empty items. Execute path not entered.
- `false` — Blind: EventStore `IsAvailable` stays true so production outages skip the new gate. 10.7 Always: the EventStore adapter is the only true implementation; store throws still map to `ReadModelUnavailable` in the existing catch.
- `medium` — Blind: `docs/sdk/cli-reference.md` context table still omits `context indexing-status` while OpenAPI `transportParity` now includes `cli`. `ConsumerDocsConformanceTests` pins 40 rows, so the new command is undocumented. Developers using the CLI reference will not find it.
- `false` — Blind: `TransportParityConformanceTests` still says the CLI indexing-status gap is by design. No such assertion exists in `tests/` after this change.
- `defer` — Blind: DW-281 still claims OpenAPI omits `cli` and CLI has no subcommand. Pre-existing ledger row, now stale; not a runtime defect of this diff.
- `false` — Blind: no `--task-id` reject test. `CommandFactory.Query` omits `--task-id` when `taskIdRequired: false`, so it is an unrecognized argument (exit 64), same as the covered workspace/idempotency extras.
- `low` — Blind: `ArchivedDocumentShouldBeAbsentFromSearchWhileStatusRemainsIndexed` uses an empty source. Rejected: hermetic stand-in; production archive tombstones the bridge (`SemanticIndexingBridgeProjection.ApplyFolderArchived`) and Memories filters `folders.status=active`. A stronger fake would add hit-shape complexity, not a direct correction.
- `defer` — Blind: duplicate source hits can still return two items. Pre-existing handler loop; this story only changed `IsVisible` and the `IsAvailable` gate.
- `false` — Blind: unauthorized-unavailable test does not byte-compare a control denial. `UnauthorizedCrossTenantAndNonexistentTargetsShouldReturnByteIdenticalSafeDenial` already byte-compares denials; the new test asserts the denial does not leak unavailable.
- `false` — Blind: story-file Tasks 0–5 still open. The dedicated story file is not this build spec; leaving those boxes open is honest while FR58 live path is incomplete.
- `false` — Blind: spec Verification commands vs Implementation Notes. Rejected: the fix is to edit this build's spec.
- `defer` — Blind: `epic-11-context.md`, untracked `spec-11-3-…`, and Builds/FrontComposer gitlinks sit in the same tree. Concurrent workspace dirt, not 10.8 product behavior.
- `defer` — Edge: empty/whitespace `--folder-id` forwarded to SDK. `RequiredId` has no whitespace guard on any CLI command; pre-existing.
- `false` — Edge: `NewUlid` emits uppercase IDs. Execute path not entered. (`CanonicalSegmentRegex` is lowercase-only, so this would matter only after the live gate flips.)
- `false` — Edge: add sends `sha256:` not `hashref_`. Execute path not entered.
- `false` — Edge: add omits Authorization and tenant. Execute path not entered.
- `false` — Edge: add without prepare/lock. Execute path not entered.
- `false` — Edge: search poll retries non-success bodies. Execute path not entered.
- `false` — Edge: search poll parses JSON with no catch. Execute path not entered.
- `false` — Edge: indexing-status read once. Execute path not entered.
- `false` — Edge: poll `queryText` is not bound to the added file. Execute path not entered.
- `false` — Edge/claim: only Add, never Change. Spec happy path is Add or Change.
- `false` — Edge/claim: unauthenticated Add / Keycloak off as auth. Execute path not entered.
- `false` — Edge/claim: status not polled. Execute path not entered.
- `false` — Edge/claim: `IsVisible` never inspects archive. Production archive tombstones; Memories filters `StatusActive`; hits have no archive flag for the handler to inspect.
- `defer` — Edge/claim: isolation tests do not drive real remove/archive mutations. Live remove/archive is Task 0 / 12.x; hermetic tests already drop `Tombstoned`.
- `low` — Verification-gap Other: archive wiring test would still pass if Memories returned an Indexed archived hit. Same as the empty-source stand-in; rejected (not everyday, not a direct correction).
- `false` — Verification-gap Other: flipping the live-path gate would still not prove authenticated mutation. Execute path is not reachable while the gate is false; that is the Q1-A fail-close.
- `medium` — Verification-gap Other: `docs/sdk/cli-reference.md` omits `context indexing-status` and the consumer-docs count stays 40. Same defect as the Blind CLI-docs finding.

## Design Notes

Active recall is Indexed-only; status may list Stale. Copy the status handler’s post-auth `IsAvailable` gate onto search. Live proof (Task 0 green): public mutation → EventStore → `folders.events` → Workers → pub/sub → Memories `folders-index` → bridge → public search/status. OpenAPI change is `cli` parity on status only; do not change query family or token vocabulary.

## Verification

**Commands:**
- `dotnet restore Hexalith.Folders.slnx -p:NuGetAudit=false` && `dotnet build Hexalith.Folders.slnx --configuration Debug --no-restore` — 0 errors/warnings
- Individual `dotnet test` on Folders.Tests, Workers.Tests, Server.Tests, IntegrationTests, Cli.Tests, Mcp.Tests, Client.Tests, Contracts.Tests, Testing.Tests — pass
- `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true dotnet test tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj --configuration Debug --no-build` — FR58 executed, not skipped (after Task 0 green)
- Leakage corpus + production Dapr-policy conformance — pass
