---
baseline_commit: 40cc5e1
---

# Story 10.6: Replace the fail-closed content materializer with a metadata-derived materializer under C4/C9

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> [!IMPORTANT]
> **Load-bearing decisions for this story (read before writing code):**
>
> 1. **This is the one component that makes FR58 real.** The Epic 10 pipeline is complete end-to-end *except* the piece that produces indexable content. The registered `ISemanticIndexingContentMaterializer` is `FailClosedSemanticIndexingContentMaterializer` (`FoldersWorkersModule.cs:79`), which **always** returns `Unavailable("content_materializer_unavailable", retryable:true)`. So every authorized, policy-passing mutation dead-ends at materialization, records a retryable `Failed` bridge entry, and **nothing real ever reaches `folders-index`**. This story replaces that placeholder with a **metadata-derived** materializer.
>
> 2. **THE SILENT-BREAK TRAP — the materializer MUST emit the facade's security-trim attributes.** The port publishes the materializer's `CuratedText`/`CuratedAttributes` **verbatim** and adds nothing (`MemoriesSemanticIndexingPort.cs:42-50`). The Story 10.5 query facade finds and tenant-trims hits **only** by the attributes `folders.managedTenantId`, `folders.organizationId`, `folders.folderId`, `folders.workspaceId`, and `folders.status = active` (`MemoriesFolderSearchSource.cs:120-125`). **No current upsert path emits any of these** (only the archive fallback does, `MemoriesSemanticIndexingPort.cs:201-215`). A materializer that emits only content-classification attributes will publish a `SearchIndexEntryChanged` that **passes this story's "publishes a real entry" AC but is invisible to the facade** — a silent end-to-end break. The new materializer therefore MUST put the five identity keys + `folders.status = active` into `CuratedAttributes`, using the shared `FoldersSemanticIndexingAttributes` constants (`src/Hexalith.Folders/Projections/SemanticIndexing/FoldersSemanticIndexingAttributes.cs`). See AC2 + Dev Notes "The attribute-emission gap".
>
> 3. **Metadata-derived only — NO file content is read.** `CuratedText`/`CuratedAttributes` are built solely from mutation *metadata* evidence already present on the materialization request (opaque `fv-…` file-version id, tenant/org/folder/workspace ids, size/type classification, media type, path-policy outcome). **Never** a raw file path, file body, content snippet, or source URI (C9). Authorized real-content (body-text) materialization is a **C9-gated follow-up (Security + PM sign-off)** — recorded in `sprint-status.yaml`, not shipped here (AC9).
>
> 4. **Do NOT re-implement C4.** Size/type gating already exists in two places (upstream policy-evaluator Gate 5 + downstream process-manager gate). The materializer must set `ContentType`/`LengthBytes`/classifications *consistently with the declared evidence* so those gates stay green — it must not add a third divergent copy of the size/type rules. See AC4 + Dev Notes "C4 semantics".
>
> 5. **Internal worker behavior only — wire-preserving.** No REST/OpenAPI/envelope/parity/SDK/CLI/MCP change. This story is bound by Story 11.1 §10 wire-preservation (`ImplementedRestOperationCount = 49`) and its §12 annotation: **10.6 must land before Epic 11 Story 11.10**, which rewrites the same Workers code and must then preserve this new behavior (not re-freeze the placeholder). [Source: `11-1-establish-refactor-baseline-and-governance-pin-map.md:181-185`]
>
> 6. **Live proof stays BLOCKED-PENDING the DCP lane.** The real `aspire run` 6-service round-trip is a standing Epic 9/10 environment blocker (CLI 13.4.5 / DCP `--tls-cert-file` mismatch), NOT a code defect. 10.6 proves the path at the worker/port boundary (unit + Tier-3 opt-in harness); it carries the same blocker forward and introduces no new one. [Source: `10-4-*.md:15,283`; `sprint-status.yaml` epic-10 action item, `status: open`]

## Story

As a developer or AI-agent consumer,
I want authorized folder mutations to produce real curated search-index text and attributes from mutation metadata evidence,
so that the Memories search index is actually populated on live mutations — and the results are discoverable through the Story 10.5 facade — without ever leaking raw content, paths, snippets, or source URIs.

## Context & Scope Boundary

Epic 10 built the Folders→Memories search-index pipeline: Story 10.1 (worker-only Memories dependency + `ISemanticIndexingPort`), 10.2 (Folders-owned bridge projection/read model), 10.3 (publish `SearchIndexEntryChanged` on file-write/commit, authorized async), 10.4 (`SearchIndexEntryRemoved` on removal + archive soft-delete re-send + hybrid hard-delete), 10.5 (authorized query facade + indexing-status console). Every stage is `done`.

**The one missing piece is the content producer.** The `ISemanticIndexingContentMaterializer` seam is fully wired into the process manager (`SemanticIndexingProcessManager.cs:170-185`), but its only implementation is the fail-closed placeholder. Consequently the real mutation→index path can be exercised *only* by seeding through the worker port in tests; a genuine `WorkspaceFileMutationAccepted` produces a `Failed` bridge entry and no search-index document. This was a **deliberate deferral** recorded as an open, high-priority Epic 10 retro action item; Jerome pulled it into the active plan via `bmad-correct-course` (2026-07-07, `sprint-change-proposal-2026-07-07-content-materializer.md`), choosing the **Hybrid** strategy: ship metadata-derived now, defer authorized real-content to a C9-gated follow-up.

Story 10.6 replaces `FailClosedSemanticIndexingContentMaterializer` with a `MetadataDerivedSemanticIndexingContentMaterializer` that returns `Available` curated text/attributes built entirely from the metadata already on the materialization request — closing the pipeline so an authorized mutation yields a real, facade-discoverable `SearchIndexEntryChanged`.

**In scope:**

1. **New materializer** `MetadataDerivedSemanticIndexingContentMaterializer : ISemanticIndexingContentMaterializer` (`src/Hexalith.Folders.Workers/SemanticIndexing/`), returning `Available` via the 8-arg factory with curated text + the full curated-attribute set (identity security-trim keys + `folders.status=active` + content classifications).
2. **Registration** in `FoldersWorkersModule.AddFoldersSemanticIndexingWorkers` replacing the fail-closed default at `FoldersWorkersModule.cs:79`; **`FailClosedSemanticIndexingContentMaterializer` retained** in the tree as an explicit, still-constructible fallback (not auto-registered).
3. **Optional consolidation** — promote the three content-classification attribute keys (`folders.contentDescriptor`, `folders.sizeClassification`, `folders.typeClassification`) to constants on `FoldersSemanticIndexingAttributes` and reference them from the new materializer (they are inline literals today; every other `folders.*` key is a shared constant — see Dev Notes).
4. **Tests in lockstep**: flip the registration-type assertion in `SemanticIndexingWorkerRegistrationTests`; add a new materializer unit-test class (Available happy path, attribute completeness, C9 corpus, C4 classification, determinism); add a real-materializer worker/port-boundary test proving the published `SearchIndexEntryChanged.Attributes` carry the facade filter keys; confirm `SemanticIndexingProcessManagerTests` / `SemanticIndexingEndpointE2ETests` stay green (they inject fakes) and extend where they can now assert the real curated attributes.
5. **Governance sync**: flip the materializer action item to `done` and add a NEW `action_items:` entry recording the C9-gated real-content follow-up (Security + PM). No planning-artifact prose changes are required — `prd.md:688`, `architecture.md:140`, `epics.md:1906,1972-1990`, and `11-1-*.md:177-187` were already authored by the correct-course workflow.

**Out of scope:**

1. **Reading file content / body-text materialization.** No filesystem, provider, blob, or content-store read. The materializer derives everything from the request's metadata fields. Authorized real-content materialization is the recorded C9-gated follow-up (AC9) — NOT this story.
2. **Any wire change.** No REST route, OpenAPI op, envelope, parity-oracle, SDK/CLI/MCP, or Dapr-policy change. `ImplementedRestOperationCount` stays 49. (Story 11.1 §10 invariant.)
3. **Changing the port, the bridge projection, the policy evaluator, the removal/archive egress, or the C4 gate logic.** The materializer plugs into the existing seam; it must not alter the surrounding orchestration. (One narrow exception is permitted only if the team chooses the "port owns identity attributes" alternative in Dev Notes — but the recommended design keeps the port untouched.)
4. **Server-side EventStore-backed bridge read model.** The deployed facade's `Unavailable` read-model limitation is a separate, re-homed item (Epic 11 Story 11.10, `architecture.md:178`). 10.6 *populates* the index; that item makes the deployed Server facade *read* it. FR58 end-to-end needs both + the DCP live lane.
5. **Live `aspire run` sign-off.** Inherits the standing DCP/`--tls-cert-file` blocker; the local bar is unit + Tier-3-opt-in structural proof.
6. **`SemanticIndexing`→`SearchIndexing` rename.** Tracked separately (low-priority action item); keep the retained naming.

## Acceptance Criteria

1. **Metadata-derived materializer replaces the fail-closed default.** A new `MetadataDerivedSemanticIndexingContentMaterializer : ISemanticIndexingContentMaterializer` is implemented and registered in `FoldersWorkersModule.AddFoldersSemanticIndexingWorkers` in place of the fail-closed type at `FoldersWorkersModule.cs:79`. `FailClosedSemanticIndexingContentMaterializer` remains in the source tree as an explicit, constructible fallback (kept `internal sealed`, no longer the auto-registered default). An authorized, policy-passing stale mutation now yields a `SemanticIndexingContentMaterializationResult` with `Status == Available` and the process manager publishes a real `SearchIndexEntryChanged` (bridge status `Indexed`, reason `memories_accepted`) instead of recording `Failed`/`content_materializer_unavailable`. [Source: epics.md:1980-1982; SemanticIndexingProcessManager.cs:186-251]

2. **Curated attributes carry the facade security-trim keys + `folders.status = active` (load-bearing).** The `Available` result is built via the 8-arg `Available(...)` factory whose `curatedAttributes` — published verbatim by the port (`MemoriesSemanticIndexingPort.cs:43,50`) — contain, using the shared `FoldersSemanticIndexingAttributes` constants: `ManagedTenantIdAttribute`, `OrganizationIdAttribute`, `FolderIdAttribute`, `WorkspaceIdAttribute`, `FileVersionIdAttribute` (values from `request.Identity`), and `StatusAttribute = StatusActive`, plus the content classifications (`folders.contentDescriptor`/`folders.sizeClassification`/`folders.typeClassification`). A worker/port-boundary test drives the **real** materializer → **real** `MemoriesSemanticIndexingPort` → a recording `DaprClient` and asserts the published `SearchIndexEntryChanged.Attributes` contain all five identity keys + `folders.status=active`, so the Story 10.5 facade's `BuildAttributeFilters` (`MemoriesFolderSearchSource.cs:120-125`) can find and tenant-trim the document. **A published doc missing any of these keys is invisible to the facade — this AC is the explicit guard against that silent break.** [Source: MemoriesFolderSearchSource.cs:116-129; MemoriesSemanticIndexingPort.cs:42-50; FoldersSemanticIndexingAttributes.cs:25-50]

3. **C9 sanitization — no raw path, body, snippet, or source URI.** `CuratedText` and every `CuratedAttributes` value are built ONLY from non-sensitive metadata tokens (the opaque `fv-…` file-version id, tenant/org/folder/workspace ids, size/type classification, media type, path-policy outcome label). They contain **no** raw file path, **no** file body, **no** content snippet, and **no** source URI (`request.Identity.SourceUri` and `PathMetadataDigest` raw values are NOT echoed into `Text` or any attribute). Asserted against a sensitive-path corpus: given mutation evidence whose surrounding context includes raw paths (`C:/…`, `/etc/…`, `file://…`), secrets, and snippet-shaped strings, neither `Text` nor any attribute value contains them (reuse the inline `ShouldNotContain("C:/", Case.Sensitive)` idiom, `SemanticIndexingWorkerRegistrationTests.cs:109`). [Source: epics.md:1984,1986; architecture.md:137,504; project-context.md:116,153]

4. **C4 size/type gates remain green — no third divergent copy.** The materializer sets `ContentType` from the declared `ExpectedMediaType`, `LengthBytes` from the declared original size (`ObservedByteLength ?? ExpectedByteLength`), `ContentBytes` = UTF-8 of the curated descriptor (small, non-null — required by the factory and by the downstream `ContentBytes!.LongLength` gate), and `SizeClassification`/`TypeClassification` derived deterministically from those. It does **not** re-implement C4 enforcement: the upstream policy-evaluator Gate 5 (`FailClosedSemanticIndexingPolicyEvaluator.cs:110-135`) and the downstream process-manager gate (`SemanticIndexingProcessManager.cs:199-220`) stay authoritative and consistent — `content_too_large` / `content_type_unsupported` still fire on oversize / unsupported originals (`MaxInlineIngestionBytes = 262144`, the `text/*` + json/xml/yaml/markdown allow-list). All existing C4 tests stay green. [Source: SemanticIndexingProcessManager.cs:199-220,320-326; FoldersSemanticIndexingDefaults.cs:7]

5. **Idempotent / replay-stable output.** For the same `SemanticIndexingContentMaterializationRequest`, the materializer returns byte-identical `CuratedText` and `CuratedAttributes` on every call — deterministic, ordinal-ordered dictionaries, `CultureInfo.InvariantCulture` formatting, no `DateTime`/random/environment input. The CloudEvent id (`request.Source.ToUriString()`) and the bridge result fingerprint (`SemanticIndexingProcessManager.cs:348-361`) are unchanged and stable across at-least-once redelivery. [Source: epics.md:1986; project-context.md:59-60]

6. **Non-Available and no-content paths preserved.** A stale entry with `ContentHashReference == null` still short-circuits to a no-op before materialization (`SemanticIndexingProcessManager.cs:70-74`). Policy-denied entries still record `Skipped`/`Failed` before the materializer is reached (`:165-168`). The materializer is invoked only after policy `IsAllowed`, and if it ever returns `Skipped`/`Unavailable` the existing mapping (`:186-197`) is unchanged. Metadata-only mutations produce no search-index document. [Source: SemanticIndexingProcessManager.cs:66-77,162-197]

7. **Lockstep test updates.** `SemanticIndexingWorkerRegistrationTests.cs:42` asserts the new concrete type (`GetRequiredService<ISemanticIndexingContentMaterializer>().ShouldBeOfType<MetadataDerivedSemanticIndexingContentMaterializer>()`, mirroring the bridge-store assertions at `:61-62`). A new `MetadataDerivedSemanticIndexingContentMaterializerTests` covers ACs 2–5. `SemanticIndexingProcessManagerTests` and `SemanticIndexingEndpointE2ETests` (which inject `RecordingContentMaterializer` fakes) still pass unchanged; where a test can now assert the real curated attributes end-to-end at the worker/port boundary, it does. No test asserts the removed `content_materializer_unavailable` default (none does today — verified). [Source: agent test audit; SemanticIndexingWorkerRegistrationTests.cs:42,61-62; SemanticIndexingProcessManagerTests.cs:292-308]

8. **Wire-preservation (Story 11.1 §10).** No change to any REST route, OpenAPI op, request/response envelope, parity-oracle row, generated SDK, CLI, MCP tool, or Dapr access-control policy. `ScaffoldContractTests`, contract-spine, parity, and Dapr-policy-conformance lanes stay green; `ImplementedRestOperationCount` stays 49. The Story 11.1 §12 delta is honored: Story 11.10 must rebase on and preserve this new metadata-derived behavior. [Source: 11-1-*.md:148,161,181-185]

9. **C9-gated real-content follow-up recorded — not silently dropped.** In `sprint-status.yaml`: the existing epic-10 materializer action item (`status: in-progress`, owned by 10.6) flips to `done`; a NEW `action_items:` entry is added (`status: open`, owner including **Security + PM**, matching C9's decision authority) recording that **authorized real-content (body-text) materialization** is deferred behind an explicit C9 content-exposure sign-off, with an inline `#` correct-course provenance comment — mirroring the existing entry format (`sprint-status.yaml:290-304`). [Source: epics.md:1990; architecture.md:140,234; sprint-change-proposal-2026-07-07-content-materializer.md §5]

10. **Live proof carried as the standing DCP blocker.** Real mutation→curated-text→index is proven at the worker/port boundary (unit + the Tier-3 opt-in `Hexalith.Folders.AppHost.Tests` harness, `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`-gated, 3 SKIP by default). The live `aspire run` 6-service round-trip stays BLOCKED-PENDING the DCP-capable lane; no new blocker is introduced. [Source: 10-4-*.md:98-99,283; sprint-status.yaml epic-10 open item]

11. **Verification passes.** `dotnet restore` + `dotnet build Hexalith.Folders.slnx --no-restore` succeed 0W/0E (warnings-as-errors). Narrowed lanes green: `Hexalith.Folders.Workers.Tests`, `Hexalith.Folders.Tests`, `Hexalith.Folders.Testing.Tests`, and `Hexalith.Folders.Contracts.Tests` (contract-spine / parity / Dapr-policy conformance unchanged). `dotnet format whitespace --verify-no-changes` clean over Folders-owned `src`/`tests`; `AppHost.Tests` skip clean. If a lane is blocked by local DCP/Aspire env state, record the blocker and do NOT mark implementation complete. [Source: project-context.md:125; 10-4-*.md:73]

## Tasks / Subtasks

- [x] Task 1 — Implement `MetadataDerivedSemanticIndexingContentMaterializer` (AC 1, 2, 3, 4, 5)
  - [x] Add `src/Hexalith.Folders.Workers/SemanticIndexing/MetadataDerivedSemanticIndexingContentMaterializer.cs` (`internal sealed class : ISemanticIndexingContentMaterializer`). Guard `request` non-null; `cancellationToken.ThrowIfCancellationRequested()` (mirror the fail-closed shape).
  - [x] Derive `ContentType` = `request.ExpectedMediaType` (present by the time materialization runs — Gate 5 requires it; still handle null defensively → `Skipped("content_descriptor_unavailable")` or a safe default consistent with the evaluator).
  - [x] Derive `LengthBytes` = `request.ObservedByteLength ?? request.ExpectedByteLength ?? 0`; `SizeClassification` from a small stable byte-threshold bucket; `TypeClassification` from the media-type family. Deterministic, invariant-culture.
  - [x] Build `CuratedText` from non-sensitive tokens only (e.g. `"{typeClassification} {request.Identity.FileVersionId}"` optionally plus org/folder id tokens + media type). No path/body/snippet/source-URI.
  - [x] Build `CuratedAttributes` (ordinal dict) = the five identity keys from `request.Identity` (`ManagedTenantId`/`OrganizationId`/`FolderId`/`WorkspaceId`/`FileVersionId`) via `FoldersSemanticIndexingAttributes.*Attribute` + `StatusAttribute = StatusActive` + `folders.contentDescriptor`/`folders.sizeClassification`/`folders.typeClassification`.
  - [x] Set `ContentBytes` = `Encoding.UTF8.GetBytes(curatedText)` (non-null, small); return via the 8-arg `SemanticIndexingContentMaterializationResult.Available(...)`.
- [x] Task 2 — Register + retain fallback (AC 1)
  - [x] Change `FoldersWorkersModule.cs:79` to `TryAddSingleton<ISemanticIndexingContentMaterializer, MetadataDerivedSemanticIndexingContentMaterializer>()`; keep `FailClosedSemanticIndexingContentMaterializer` in the tree (do not delete).
- [x] Task 3 — (Optional) centralize content-classification attribute keys (AC 2)
  - [x] Add `ContentDescriptorAttribute`/`SizeClassificationAttribute`/`TypeClassificationAttribute` constants to `FoldersSemanticIndexingAttributes` and reference them from the new materializer; optionally align the `Available` 6-arg auto-factory literals (`ISemanticIndexingContentMaterializer.cs:98-100`). Low-risk consolidation; skip only if it widens scope.
- [x] Task 4 — Tests (AC 2, 3, 4, 5, 7)
  - [x] Flip `SemanticIndexingWorkerRegistrationTests.cs:42` to `.ShouldBeOfType<MetadataDerivedSemanticIndexingContentMaterializer>()`.
  - [x] Add `tests/Hexalith.Folders.Workers.Tests/MetadataDerivedSemanticIndexingContentMaterializerTests.cs`: Available happy path; `CuratedAttributes` contains all five identity keys + `folders.status=active` + classifications; C9 corpus (no `C:/`, `/etc/`, `file://`, secret, snippet in `Text` or any attribute value); C4 `ContentType`/`LengthBytes`/classifications match declared evidence; determinism (two calls → equal `CuratedText`/`CuratedAttributes`). xUnit v3 `[Fact]`, Shouldly, `TestContext.Current.CancellationToken`; build the request via a local factory (shape at `ISemanticIndexingContentMaterializer.cs:12-23`).
  - [x] Add a worker/port-boundary test (extend `SemanticIndexingWorkerRegistrationTests` or a new class) driving the **real** materializer → real `MemoriesSemanticIndexingPort` → recording `DaprClient` (NSubstitute, per the file's existing `Substitute.For<DaprClient>()` idiom) and asserting the published `SearchIndexEntryChanged.Attributes` carry the facade filter keys + `folders.status=active`, `Text` leaks no raw path. Mind the `Shouldly.Case` vs `Memories.Contracts.V1.Case` alias collision (`SemanticIndexingWorkerRegistrationTests.cs:16-22`).
  - [x] Confirm `SemanticIndexingProcessManagerTests` + `SemanticIndexingEndpointE2ETests` still green (they inject `RecordingContentMaterializer`); no change required unless an assertion referenced the old default (none does).
- [x] Task 5 — Governance sync (AC 8, 9)
  - [x] `sprint-status.yaml`: flip the materializer action item `in-progress → done`; add the new `open` C9-gated real-content follow-up entry (owner incl. Security + PM) with a `#` provenance comment; refresh `last_updated`. Do NOT touch prose already authored in `prd.md`/`architecture.md`/`epics.md`/`11-1-*.md`.
- [x] Task 6 — Build + verify (AC 10, 11)
  - [x] `dotnet build Hexalith.Folders.slnx`; then Workers.Tests, Folders.Tests, Testing.Tests, Contracts.Tests; `dotnet format whitespace --verify-no-changes` over Folders-owned src/tests; AppHost.Tests skip clean. Record any DCP/Aspire env blocker; carry the live round-trip forward as the standing DCP blocker.

## Dev Notes

### The seam is fully wired — you are filling one method

`SemanticIndexingProcessManager.ProcessEntryAsync` (`SemanticIndexingProcessManager.cs:158-251`) already: (1) runs the policy gate, (2) calls `_contentMaterializer.MaterializeAsync(new SemanticIndexingContentMaterializationRequest(...))` with metadata-only inputs (`:170-185`), (3) on non-`Available` records `Skipped`/`Failed` and stops, (4) on `Available` runs the C4 size/type gate on `materialized.LengthBytes`/`ContentBytes`/`ContentType`, (5) parses source identity, (6) `BuildRequest` copies `materialized.CuratedText!`/`CuratedAttributes!` into `SemanticIndexingContentDescriptor` (`:266-273`), (7) publishes via `MemoriesSemanticIndexingPort.IndexFileVersionAsync`, which sets `Text = request.Content.CuratedText` and `Attributes = request.Content.CuratedAttributes` **verbatim** (`MemoriesSemanticIndexingPort.cs:42-50`). **You only implement the materializer method.** Everything downstream is done.

Materialization request fields you receive (`ISemanticIndexingContentMaterializer.cs:12-23`, built at `SemanticIndexingProcessManager.cs:172-183`): `Identity` (a `SemanticIndexingFileVersionIdentity` with `ManagedTenantId`/`OrganizationId`/`FolderId`/`WorkspaceId`/`FileVersionId`/`OperationId`/`PathMetadataDigest`/`ContentHashReference?`/`SourceUri`), `ContentHashReference`, `PathPolicyClass?`, `ExpectedByteLength?`, `ExpectedMediaType?`, `TransportEvidenceKind?`, `ObservedByteLength?`, `SensitivityClassification` (= `"tenant_sensitive"` on allow), `PathPolicyOutcome` (= `"accepted_mutation_authorized"`), `CorrelationId`, `TaskId`. **No bytes.** `FileVersionId` is an opaque `fv-{sha256}` handle (`SemanticIndexingFileVersionIdentity.cs:83-103`) — safe to index.

### The attribute-emission gap (why AC2 is the whole point)

The port's create/update path (`IndexFileVersionAsync`) forwards `CuratedAttributes` unchanged and adds **nothing** — not the identity keys, not `folders.status=active`. Today the only place those attributes are written is the archive soft-delete fallback (`BuildArchivedAttributes`, `MemoriesSemanticIndexingPort.cs:201-215`), and `folders.status=active` (`FoldersSemanticIndexingDefaults.StatusActive`) is a declared constant that is **never written anywhere** on the live path. The Story 10.5 facade filters hits on exactly `folders.managedTenantId/organizationId/folderId/workspaceId` + `folders.status=active` (`MemoriesFolderSearchSource.cs:120-125`). So if the new materializer emits only `folders.contentDescriptor/sizeClassification/typeClassification` (as the `Available` 6-arg auto-factory would), every published doc is filtered out by the facade → search still returns zero → the story looks done but delivers nothing.

**Therefore: emit the full curated-attribute set from the materializer** (identity keys via `FoldersSemanticIndexingAttributes` constants + `StatusActive` + content classifications), using the 8-arg `Available(byte[], contentType, lengthBytes, reasonCode, sizeClassification, typeClassification, curatedText, curatedAttributes)` overload (`ISemanticIndexingContentMaterializer.cs:103-129`). Emitting all five identity keys also makes the archive re-send correct: `BuildArchivedAttributes` clones the preserved `IndexedAttributes` and only flips `folders.status→archived` when they are present (`:203-214`), so a complete upsert attribute set keeps the archive path off its legacy identity-reconstruction branch.

**Design fork (recommended vs alternative).** *Recommended:* the materializer owns the identity+status attributes (matches the epic AC wording "folder/org identity", keeps all curated-attribute construction in one testable place, leaves the port untouched — respecting the "don't change the port" scope). *Alternative:* extend `MemoriesSemanticIndexingPort.IndexFileVersionAsync` to inject the identity+status attributes from `request` (mirroring `BuildArchivedAttributes`). If you choose the alternative you touch the port (a scope widening) and must ensure the injected attributes are also echoed into the returned `IndexedAttributes` so the bridge preserves them for archive. Prefer the recommended path unless review directs otherwise; record the choice in the Dev Agent Record.

### C4 semantics — set values, don't re-enforce

C4 is enforced twice already and you must keep both consistent:
- **Upstream, pre-materializer** — `FailClosedSemanticIndexingPolicyEvaluator.EvaluateAsync` Gate 5 (`FailClosedSemanticIndexingPolicyEvaluator.cs:110-135`): needs a length signal + `MediaType`, else `Failed("content_descriptor_unavailable")`; `ByteLength/ObservedByteLength > 262144` → `Skipped("content_too_large")`; unsupported media type → `Skipped("content_type_unsupported")`. So by the time your materializer runs, size ≤ cap and type is supported.
- **Downstream, post-materializer** — `SemanticIndexingProcessManager.cs:199-220`: `materialized.LengthBytes > cap || materialized.ContentBytes!.LongLength > cap` → `Skipped("content_too_large")`; `!IsSupportedInlineContentType(materialized.ContentType!)` → `Skipped("content_type_unsupported")`.

Set `LengthBytes` = the **original** declared size (so `content_too_large` remains meaningful and consistent with the upstream gate) and `ContentType` = the **original** declared media type (so the type gate stays consistent). `ContentBytes` is only ever read for `.LongLength` here and is **never published** — make it the small UTF-8 of the curated descriptor (satisfies the factory's non-null requirement and never trips the OR clause). Do **not** add a third size/type copy inside the materializer; the two existing gates are the authority. (`IsSupportedInlineContentType` is duplicated at `SemanticIndexingProcessManager.cs:320-326` and `FailClosedSemanticIndexingPolicyEvaluator.cs:154-161` — do not add a third.)

### C9 — what is safe vs forbidden

Safe to place in `Text`/attributes: the opaque `fv-…` file-version id, `ManagedTenantId`/`OrganizationId`/`FolderId`/`WorkspaceId` (ids, and the tenant id is the caller's own — it is the security-trim key), `MediaType`, and the derived size/type classifications. **Forbidden** (C9, `architecture.md:137,504`): raw file path, file body, content snippet, source URI. Note `request.Identity.SourceUri` is a sanitized `folders://…` handle used by the port as the CloudEvent id — that is fine as the id, but do **not** echo it into `Text` or an attribute value. `PathMetadataDigest` is a digest, not a path — safe but unnecessary; don't surface it. The source-identity type already rejects `file:`/drive-letters/backslashes (`SemanticIndexingSourceIdentity.cs:16-31`), and 10.1 fixed a raw drive-letter leak — keep that hardening intact. The C9 assertion idiom in the suite is inline `entry.Text.ShouldNotContain("C:/", Case.Sensitive)` (`SemanticIndexingWorkerRegistrationTests.cs:109`); there is no shared corpus helper — build a small local one in the new test.

### Registration + fallback

`FoldersWorkersModule.cs:79` is `TryAddSingleton<ISemanticIndexingContentMaterializer, FailClosedSemanticIndexingContentMaterializer>()`. Change the concrete type to the metadata-derived one. `TryAddSingleton` means a prior registration wins, so the change is a straight type swap. Keep `FailClosedSemanticIndexingContentMaterializer` (`internal sealed`) in the tree as the documented fallback — the E2E harness swaps materializers via `RemoveAll` + `AddSingleton` (`SemanticIndexingEndpointE2ETests.cs:270-277`), so nothing depends on fail-closed being the default. Sibling lifetime is `Singleton`; the materializer is stateless, so `Singleton` is correct.

### Test conventions (match exactly)

xUnit v3 (`[Fact]` only), Shouldly (`.ShouldBe`, `.ShouldBeOfType<T>`, `.ShouldContain(x, Case.Sensitive)`, `.ShouldNotContain(...)`, `.ShouldContainKey/ShouldNotContainKey`), NSubstitute only for `DaprClient`. Test classes `public sealed class`; fakes `private sealed class Recording*`; method names are full behavioral PascalCase sentences (e.g. `MaterializeAsyncShouldReturnAvailableWithFacadeSecurityTrimAttributes`). Hermetic — no Dapr sidecar, Memories server, network, secrets, seed, or nested submodule init. The happy-path process-manager fixture `ProcessFolderEventsAsyncShouldApplyEventsThenIndexEligibleStaleEntry` (`SemanticIndexingProcessManagerTests.cs:20-65`) already models a successful `Available(...)`-with-curated-attributes flow — reuse its shape. The port-publish assertion model is `SemanticIndexingPortShouldPublishCuratedSearchIndexEntryChangedCloudEvent` (`SemanticIndexingWorkerRegistrationTests.cs:82-131`), including the `folders.status == "active"` (`:119`) and `ShouldNotContain("C:/")` (`:109`) assertions — the new worker-boundary test should assert the same attribute set is produced by the **real** materializer, not a hand-built request.

### Previous story intelligence

- **10.3** designed the descriptor-derived `Text` (`Content.IndexingTextDescriptor` + non-sensitive identity tokens + `TypeClassification`, fallback `"{typeClassification} {fileVersionId}"`) and removed `ContentBytes` from egress so no bytes reach the wire; and its D1 trap (green tests over a non-functional live binding) is why AC2/AC10 insist on a real-materializer→real-port boundary assertion, not just a fake. [10-3-*.md:333, :12/:274]
- **10.4** already sketched this exact materializer (Dev Notes option (a)): produce curated `Text` + size/type classification "from the accepted event's safe metadata (`ByteLength`, `MediaType`, `PathPolicyClass`, `PathMetadataDigest`, `ContentHashReference`) without reading file content." [10-4-*.md:182] It also established archive = destructive full-document re-send from preserved `IndexedText`/`IndexedAttributes` with only `folders.status` flipped — which is why the upsert attribute set must be complete. [10-4-*.md:57,96]
- **10.5** promoted the `folders.*` keys to the shared `FoldersSemanticIndexingAttributes` contract precisely to stop producer/consumer drift; the new materializer is the producer side of that contract — use the constants, never string literals. [10-5-*.md dev record; FoldersSemanticIndexingAttributes.cs]
- Authorization order: folder ACL + action freshness are re-checked by the policy evaluator BEFORE the materializer is reached — the materializer must never assume authorization from `PathPolicyClass` alone; it runs only on allow. [10-3-*.md:130]

### Architecture & dependency guardrails

- Repository config is authoritative: .NET SDK `10.0.300`/`net10.0`, central package versions in `Directory.Packages.props` (xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0`, Dapr `1.17.9`, Aspire `13.4.6`). No inline `Version` attributes.
- Dependency direction unchanged: `Hexalith.Folders.Workers` references `Hexalith.Memories.Contracts` only; the new materializer lives in Workers and touches no Memories client. Core (`Hexalith.Folders`) stays Memories-free; the shared attribute contract is in `Hexalith.Folders/Projections/SemanticIndexing/` (core), which Workers already reference.
- File-scoped namespace, `internal sealed`, nullable-safe boundaries (`ArgumentNullException.ThrowIfNull`), ordinal dictionaries/`StringComparer.Ordinal`, `CultureInfo.InvariantCulture` formatting, `cancellationToken.ThrowIfCancellationRequested()`. One primary type per file; file name matches the type.
- Metadata-only is non-negotiable across events/logs/traces/audit — the curated `Text`/`Attributes` are wire output and must be metadata-only.

### References

- Story + epic: `epics.md:1972-1990` (Story 10.6 AC blocks), `epics.md:1900-1910` (Epic 10 + reopen note), `prd.md:686,688` (FR58 + two-increment scope note).
- Governance: `architecture.md:137` (CloudEvent raw-path prohibition), `:140` (metadata-derived decision + C9-gated follow-up), `:178` (deployed-Server `Unavailable` limitation), `:204,229` (C4), `:209,234,504` (C9/S-6); `11-1-establish-refactor-baseline-and-governance-pin-map.md:177-187` (§12 delta, 10.6-before-11.10); `sprint-change-proposal-2026-07-07-content-materializer.md` (driving proposal).
- Producer seam: `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingContentMaterializer.cs` (contract + factories), `FailClosedSemanticIndexingContentMaterializer.cs` (placeholder to replace), `SemanticIndexingProcessManager.cs:158-279` (invocation + C4 gate + BuildRequest), `MemoriesSemanticIndexingPort.cs:32-90,201-215` (verbatim publish + archive attribute fallback), `FailClosedSemanticIndexingPolicyEvaluator.cs:110-135,146-161` (upstream C4 + sensitivity), `FoldersSemanticIndexingDefaults.cs:7,27-33`, `FoldersWorkersModule.cs:61-85` (registration).
- Shared contract + identity: `src/Hexalith.Folders/Projections/SemanticIndexing/FoldersSemanticIndexingAttributes.cs`, `SemanticIndexingFileVersionIdentity.cs:9-104`.
- Consumer (must stay discoverable): `src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs:116-129` (facade `BuildAttributeFilters`).
- Tests: `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs:42,61-62,82-131`, `SemanticIndexingProcessManagerTests.cs:20-65,292-308,623-641`, `SemanticIndexingEndpointE2ETests.cs:61-95,265-278`, `Hexalith.Folders.AppHost.Tests` (Tier-3, `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`).
- Prior stories: `_bmad-output/implementation-artifacts/10-3-*.md`, `10-4-*.md`, `10-5-*.md`; `epic-10-retro-2026-06-27.md:106,120`.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-14 Task 1 RED: focused Workers test build failed on the intentionally missing materializer and classification keys.
- 2026-07-14 Task 1 GREEN: new materializer tests passed 6/6; full `Hexalith.Folders.Workers.Tests` passed 70/70.
- 2026-07-14 Task 2 RED/GREEN: registration assertion first resolved the fail-closed default (1 expected failure), then passed after the default registration swap; full Workers suite passed 70/70.
- 2026-07-14 Task 3 RED/GREEN: tests first referenced the missing shared classification constants (5 expected compile errors); constants and both materialization factories were aligned; full Workers suite passed 70/70.
- 2026-07-14 Task 4: added real-materializer process-manager and real-materializer-to-real-port CloudEvent evidence; full Workers suite, including endpoint E2E tests, passed 72/72.
- 2026-07-14 Task 5 RED/GREEN: ledger check first observed `in-progress`; governance sync then verified materializer=`done`, C9 follow-up=`open`, owner includes Security + PM. Diff inventory confirms no wire-contract or planning-prose files changed.
- 2026-07-14 Aspire baseline: AppHost 13.4.6 built 0W/0E and `folders-workers` reached Healthy; topology stopped cleanly before source builds.
- 2026-07-14 C9 hardening RED/GREEN: an adversarial supported `text/*` media type containing drive-path and secret-shaped tokens first leaked through curated text; curated output was reduced to derived classifications plus validated identity tokens, and the focused materializer suite passed 8/8.
- 2026-07-14 Task 6: restore/build passed 0W/0E; Workers 73/73, Folders 1377/1377, Testing 61/61, Contracts 283/283; format/analyzers and the nine-category baseline CI gate passed; AppHost opt-in suite skipped 4/4 without its environment flag.

### Completion Notes List

- Task 1: implemented deterministic metadata-derived materialization with facade security-trim identity/status attributes, metadata-only curated text, original C4 evidence, and defensive fail-closed handling for a missing media type.
- Task 2: registered the metadata-derived singleton as the default while retaining the fail-closed materializer as an explicit constructible fallback.
- Task 3: centralized the three content-classification keys in the shared producer/facade attribute contract and removed their remaining production literals.
- Task 4: covered metadata availability, facade-visible attributes, C9 leakage corpus, C4 evidence/classification, replay stability, cancellation/null guards, real process-manager indexing, and real Dapr-port publication.
- Task 5: closed the delivered Epic 10 materializer action and recorded the authorized body-text implementation as an explicit C9 Security+PM sign-off dependency.
- Task 6: verified the complete solution and canonical CI lanes; AppHost startup reached a healthy Workers resource, while the full mutation round-trip remains the existing opt-in DCP-capable-lane evidence item rather than a new implementation blocker.

### File List

- `_bmad-output/gates/baseline-ci/latest.json`
- `_bmad-output/implementation-artifacts/10-6-replace-fail-closed-content-materializer-with-metadata-derived.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders.Workers/SemanticIndexing/MetadataDerivedSemanticIndexingContentMaterializer.cs`
- `tests/Hexalith.Folders.Workers.Tests/MetadataDerivedSemanticIndexingContentMaterializerTests.cs`
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingWorkerRegistrationTests.cs`
- `src/Hexalith.Folders/Projections/SemanticIndexing/FoldersSemanticIndexingAttributes.cs`
- `src/Hexalith.Folders.Workers/SemanticIndexing/ISemanticIndexingContentMaterializer.cs`
- `tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs`

## Change Log

| Date | Change | Author |
| --- | --- | --- |
| 2026-07-14 | Implemented and verified metadata-derived semantic-index content materialization; retained the fail-closed fallback; synchronized Epic 10 governance. | Administrator (via dev-story) |
