---
title: 'Story 10.9: Authorized body-content materialization — C9 gated'
type: 'feature'
created: '2026-09-08'
status: 'in-progress'
baseline_commit: '472c690390fd7a7399da8cdeaeb6a6ea36734366'
route: 'dispatch'
review_loop_iteration: 0
story_key: '10-9-authorized-body-content-materialization-c9-gated'
context:
  - '_bmad-output/implementation-artifacts/epic-10-context.md'
  - '_bmad-output/implementation-artifacts/10-6-replace-fail-closed-content-materializer-with-metadata-derived.md'
  - '_bmad-output/implementation-artifacts/spec-10-8-real-produce-index-authorize-hydrate-redact-search-round-trip.md'
  - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 10.6–10.8 index metadata tokens only. Body-text materialization is still off. C9 body-content approval is open. Without it the capability must stay unavailable and must not block FR58.

**Approach:** Keep 10.6 registered. Prove file bodies are not read and not published. Record live-path blockers. Do not add a body materializer, content reader, or C9 policy. Story stays incomplete.

## Decisions

- **Q1-A:** Unavailable-only. No body materializer type. Tests lock the 10.6 DI default and that bodies are not read.
- **Q2-B:** Do not invent C9 scope, source classes, redaction, retention, deletion, access, or egress.
- **Q3-C:** No body publication. Memories `Text` stays 10.6 metadata tokens.
- **Q4-A:** Block on 12.3. Do not add a Folders content reader or request bytes.
- **Q5-A:** Fail-close live DCP. Hermetic Worker tests may land. Do not mark the story `done`. No new AppHost scenario.

## Boundaries & Constraints

**Always:** Policy/auth before any content access. `MetadataDerivedSemanticIndexingContentMaterializer` stays the registered default. `FailClosedSemanticIndexingContentMaterializer` stays constructible and unregistered. Published docs still emit facade trim keys. C4 stays in the policy evaluator and process-manager gates. Prune via 10.4. Bridge via `IReadModelStore` + `ReadModelWritePolicy`. Workers pub/sub only. Task 0 records C9 approval, 12.3, 12.5, 12.6 admission≠emission, DW-292, DenyAll validator, and 11.15.

**Never:** Invent C9 policy. Read bodies from filesystem, blob, EF, or a new store. Extend the materializer request with bytes. Register a body materializer. RAG/`IngestAsync`. Hand-edit SDK or change REST/CLI/MCP. Implement 12.1–12.5, 11.15, DW-292, or EventStore validator. Put bodies or snippets on the search facade or in Memories `Text`. Claim body-content complete from unavailable, mock, seed, in-memory, or skipped DCP. Flip sprint-status to `done`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No C9 approval | Production DI | Metadata-derived registered; request has no bytes; `CuratedText` is metadata tokens | Body unavailable; FR58 unchanged |
| Body-shaped fixture | Path, secret, snippet in surrounding context | `CuratedText`/`attributes` omit them | Existing C9 assertions stay green |
| Unauthorized / redacted | Policy deny | Materializer not called | Safe skip/fail; no existence/body leak |
| Binary / oversize / bad type | After allow | Skip `content_too_large` / `content_type_unsupported` | No publish |
| Live DCP requested | 12.3/12.5/C9 missing | Do not run a body round-trip | Story incomplete; no seed/fake store |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:79` — keep `TryAddSingleton<ISemanticIndexingContentMaterializer, MetadataDerivedSemanticIndexingContentMaterializer>`.
- `src/Hexalith.Folders.Workers/SemanticIndexing/MetadataDerivedSemanticIndexingContentMaterializer.cs` — metadata tokens only; do not read content.
- `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingContentMaterializer.cs` — request has no body bytes; do not extend.
- `src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingContentMaterializer.cs` — keep constructible; do not re-register.
- `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs:162-220` — policy then materialize; do not add a content read.
- `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs:42-53` — `Text` = `CuratedText`; must stay metadata-only.
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs:43` — already asserts MetadataDerived; add no content-store dependency.
- `tests/Hexalith.Folders.Workers.Tests/MetadataDerivedSemanticIndexingContentMaterializerTests.cs` — extend C9 coverage with a body-shaped fixture.
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs` — existing policy-deny and C4 skips; reuse, do not duplicate.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:205,295-303` — set `10-9-…` in-progress; leave C9 action `open`; do not set `done`.
- Do not change: generated SDK, OpenAPI, CLI/MCP, Dapr policy, EventStore bridge, `ContextSearchQueryHandler`, `FolderDomainProcessor`, AppHost tests.

## Tasks & Acceptance

**Execution:**
- [x] Task 0 — In Implementation Notes, record C9-body-content-approval (open), 12.3, 12.5, 12.6 admission≠emission, DW-292, DenyAll validator, and 11.15 with current sprint-status. Body capability stays unavailable; story incomplete.
- [x] `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs` — assert registered materializer is `MetadataDerivedSemanticIndexingContentMaterializer` and its constructor/DI takes no content-store or byte-source dependency.
- [x] `tests/Hexalith.Folders.Workers.Tests/MetadataDerivedSemanticIndexingContentMaterializerTests.cs` — body-shaped fixture (file body, path, secret, snippet) still yields 10.6 metadata tokens only; nothing of the body crosses `CuratedText`, attributes, or `ContentBytes`.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — set `10-9-authorized-body-content-materialization-c9-gated` to `in-progress`. Do not set `done`. Leave the epic-10 C9 Security/PM action `open`.

**Acceptance Criteria:**
- Given no recorded C9 approval, when Workers resolve `ISemanticIndexingContentMaterializer`, then it is the 10.6 metadata-derived type, it has no content-store dependency, and file bodies are not read.
- Given a body-shaped materialization fixture, when `MaterializeAsync` runs, then `CuratedText`, attributes, and `ContentBytes` stay 10.6 metadata tokens and contain no body, path, secret, or snippet.
- Given policy deny or C4 skip, when an entry is processed, then existing Worker tests still pass (materializer not called on deny; no publish on skip).
- Given missing 12.3, 12.5, or C9 approval, when live DCP is considered, then no body round-trip runs and the story remains incomplete.

## Implementation Notes

### Task 0 — body-content remaining unavailable (2026-09-08, baseline `472c690390fd7a7399da8cdeaeb6a6ea36734366`)

No C9 body-content approval is recorded. `MetadataDerivedSemanticIndexingContentMaterializer` remains the registered default. No body materializer, content reader, or C9 policy was added. File bodies are not read. Memories `Text` stays 10.6 metadata tokens. Story 10.9 stays **incomplete**. Do not treat hermetic tests, mocks, seeds, or skipped DCP as body-content completion.

| Prerequisite | Status | Evidence |
| --- | --- | --- |
| C9-body-content-approval | **open** | `sprint-status.yaml` epic-10 action “Authorize and implement real-content (body-text) materialization only after explicit C9 content-exposure sign-off”; owner Security / PM / Amelia; `status: open`. |
| Story 12.3 durable workspace file content store / content read source | **missing** | sprint-status `12-3-durable-workspace-file-content-store-and-content-read-source: backlog`. No Folders content reader; materializer request still has no body bytes. |
| Story 12.5 at-least-once Memories egress/reconciler | **missing** | sprint-status `12-5-at-least-once-memories-egress-and-reconciler: backlog`. |
| Story 12.6 admission ≠ emission | **blocker** | sprint-status `12-6-implement-durable-all-mutations-idempotency-and-expired-key: in-progress`. `FolderDomainProcessor.ToDomainResult` still maps accepted results to eventless `PayloadNoOpDomainResult` (`:1257-1276`, `:1340`). DW-291 remains open. Admission is not `WorkspaceFileMutationAccepted` emission. |
| DW-292 ACL population | **missing** | `FoldersServiceCollectionExtensions.AddFoldersLayeredAuthorization` `:355` `InMemoryEffectivePermissionsReadModel`; no production `Save`. Test-only seeding is forbidden. |
| EventStore DenyAll validator | **blocker** | `:358` `DenyAllEventStoreAuthorizationValidator`. Allowing/test override is not production-path evidence. |
| Story 11.15 DCP lane | **missing** | sprint-status `11-15-maintain-the-dcp-capable-cross-repository-verification-lane: backlog`. No new AppHost body round-trip. Live DCP fail-closes. |

`development_status.10-9-authorized-body-content-materialization-c9-gated` is `in-progress` (not `done`). Existing Worker policy-deny and C4 skip tests remain the coverage for unauthorized/redacted and binary/oversize/bad-type; they were not duplicated.

### Verification (2026-09-08)

Project-level `dotnet test` is blocked by DW-341. Direct xUnit v3 host was used. `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION` was **not** set (Task 0 still blocked; no body round-trip).

| Host | Result |
| --- | --- |
| `dotnet restore Hexalith.Folders.slnx -p:NuGetAudit=false` && `dotnet build Hexalith.Folders.slnx --configuration Debug --no-restore` | 0 errors / 0 warnings |
| `tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests` | 90 passed (2 new: DI no content-store; body-shaped metadata-only fixture) |

## Spec Change Log

- 2026-09-08: Task 0 recorded C9/12.3/12.5/12.6/DW-292/DenyAll/11.15 blockers. Hermetic Worker tests lock the 10.6 DI default (no content-store/byte-source) and a body-shaped fixture that still emits metadata tokens only. Story remains incomplete; C9 Security/PM action stays open.

## Review Triage Log

## Design Notes

This increment is the epic AC “absent approvals the feature remains unavailable,” not body indexing. A later story after Security/PM sign-off, a new FR if Memories `Text` would hold bodies, and 12.3 content reads can add the real materializer. Do not scaffold it here.

## Verification

**Commands:**
- `dotnet restore Hexalith.Folders.slnx -p:NuGetAudit=false` && `dotnet build Hexalith.Folders.slnx --configuration Debug --no-restore` — 0 errors/warnings
- Direct xUnit v3 host `tests/Hexalith.Folders.Workers.Tests` — pass (project-level `dotnet test` is DW-341)
