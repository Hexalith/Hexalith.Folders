# Reviewer Gate — Code-Reality / Brownfield Lens (RE-GATE, pass 2)

**Target:** `_bmad-output/planning-artifacts/architecture.md` (1907 lines; pass-2 state, uncommitted, `+180/-73` vs HEAD across both passes)
**Prior reviews:** `reviews/review-code-reality-2026-09-16.md` (FAIL — 4C/4H/4M/2L) → `reviews/review-code-reality-update-2026-09-16.md` (FAIL — 2C/5H/7M/2L)
**Repo HEAD:** `de281e7` (`main`) · **Date:** 2026-09-16 · **Run:** VALIDATE (no edits applied)
**Live verification run this pass:** `Hexalith.Folders.Contracts.Tests` — **314/314 pass, 0 failed, 0 skipped** (7.1s, xUnit v3 in-process runner)

---

## Verdict

**FAIL — 2 critical / 5 high / 7 medium / 5 low.**

Pass 2 is a real improvement on the two things that mattered most. The Story 12.4 anchor is now byte-correct (`FoldersServerServiceCollectionExtensions.cs:67` is exactly the `IWorkspaceCommitExecutor` → `UnavailableWorkspaceCommitExecutor` line — I counted it), the degraded-mode over-claim was *reversed* rather than patched, and the two "Deployed-Server limitation" sites were found stale and closed against `done` stories and a real registration. Five of the eight false claims I flagged are closed at the root. The `.slnx` 14/17 counts are exact, the 13-adapter count is exact, the C9 gate now names the Dapr state store, the package-management selector is now correctly `Configuration`-driven, and — worth stating plainly — **the pass-2 edits to `c6-transition-matrix-mapping.md` did not break the doc gates**: I ran the suite and all 314 Contracts.Tests pass, including the 41-edge, 24-event and 49-operation pins.

But the failure mode has reproduced a third time, at the same rate. **Pass 1 introduced eight false claims; pass 2 removed five of them and introduced eight more.** Two are critical. Both sit in the exact sentences the pass was written to fix:

- **S-7's vocabulary-ownership sentence.** Pass 1 said the PD10 vocabulary was already pinned by `parity-contract.schema.json`. That was false. Pass 2 over-rotated to "the schema closes exactly two axes" and "`code`, `clientAction`, and `visibility` have no closed vocabulary **anywhere**." Both halves are false. The schema closes **fourteen** enums, and the shipped spine declares `clientAction` as a closed 7-member enum at `hexalith.folders.v1.yaml:7678` and `visibility` as a **required** closed 2-member enum at `:7744-7748` — the second of which the same sentence names three clauses earlier. Two consecutive passes have now shipped a false provenance claim on the same sentence, in opposite directions.
- **The bridge arbitration.** Pass 1's "the projection is sole writer" was correctly identified as falsified by the code. Pass 2's replacement — "a field split, **enforced by the interface**", with the projection writing "only ever `Stale`" — is also falsified by the code. `ISemanticIndexingBridgeWriter` carries **both** halves (`ApplyFolderEventsAsync` *and* `RecordIndexingResultAsync`); both implementations implement the whole interface; and `SemanticIndexingBridgeProjection` writes `Tombstoned` as well as `Stale`. The interface unifies the two roles rather than partitioning them.

The structural remedy did not move. Of the eight bare-but-verified-absent tree entries I itemised, **one** gained a marker. `DaprPolicyConformance/` is now *immediately above* a `NOT BUILT` sibling, which reads as a deliberate contrast and is worse than the uniform silence it replaced. The half-applied honesty labelling closed 5 of 12 sites and I found a 13th; the survivors include a **checked box** and the CI-gates enumeration.

One finding below is new to this pass and is the most useful thing I have to report: the merge-blocking C6 event-vocabulary gate compares **document to document** and never to `FolderWorkspaceLifecycleEvent`, so a live 24-vs-23 divergence has been green at HEAD the whole time.

---

## Findings

### RG-1 — CRITICAL — S-7's corrected vocabulary-ownership sentence is false on both halves, and one half is contradicted three clauses earlier in the same sentence

architecture.md line 657 (S-7, pass-2 text):

> **Vocabulary ownership, honestly scoped:** `tests/fixtures/parity-contract.schema.json` closes exactly two axes — `canonical_error_category` and `mcp_failure_kind` … The `code`, `clientAction`, and `visibility` axes have **no closed vocabulary anywhere**; giving them one is part of this correction, not a property it can rely on.

**Half one — "closes exactly two axes" — false.** I enumerated every `enum` in the file programmatically. It closes **fourteen**:

| Location | Members |
| --- | --- |
| `$defs/canonical_error_category` | 50 |
| `$defs/mcp_failure_kind` | 49 |
| `$defs/cli_exit_code` | 15 |
| `$defs/adapter_name` | 5 |
| `properties/operation_family` | 5 |
| `properties/read_consistency_class` | 4 |
| `transport_parity/auth_outcome_class` | 6 |
| `transport_parity/idempotency_key_rule` | 3 |
| `behavioral_parity/pre_sdk_error_class` | 5 |
| `behavioral_parity/{idempotency_key,correlation_id,task_id,credential}_sourcing` | 6 / 7 / 4 / 7 |
| `outcome_mapping/items/pre_sdk_error_class` | 5 |

`cli_exit_code` being closed matters directly: the very next clause of the same S-7 row reasons about `cli_exit_code` values, which the sentence has just declared the schema does not govern.

**Half two — "no closed vocabulary anywhere" — false for two of the three axes, in the shipped spine:**

- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7732-7737` — `clientAction: { type: string, enum: [retry, revise_request, no_action] }`
- `:7674-7682` — a **second, different** `clientAction` enum with seven members: `retry, revise_request, check_credentials, wait_for_reconciliation, contact_operator, no_action, refresh_state_then_submit_with_new_key`
- `:7741-7748` — `details.visibility`, **`required`**, `enum: [redacted, metadata_only]`
- `extensions/hexalith-extension-vocabulary.yaml` — four more single-member `clientAction` enums and five single-member `code` enums, including `:655 code: { type: string, enum: [resource_unavailable] }`, which is S-7's own `safe-denial-404` code

The `visibility` claim is self-refuting: three clauses earlier the same sentence states *"the shipped `DetailsVisibility` set is `{redacted, metadata_only}`."* A closed set is a closed vocabulary.

**Why critical.** The sentence exists to tell the PD10 owner what they must build versus what they can rely on, and it gets the answer backwards a second time. The truth is more actionable than either extreme: `clientAction` and `visibility` **already have closed enums that PD10 must amend** — and amending them is a breaking spine change, which is precisely the A-11 rollout dimension this document flags as open two paragraphs later. Worse, the correction would have surfaced a live inconsistency it now cannot see: **the spine carries two incompatible `clientAction` enums**, and S-7's approved 401 value `check_credentials` is a member of the seven-member one at `:7678` but **not** of the three-member one at `:7735`. The honest sentence is: *the schema closes fourteen vocabularies but none of them is a `code`, `clientAction` or `visibility` axis for the canonical envelope; the spine closes `clientAction` twice, incompatibly, and `visibility` once; `code` is pattern-constrained only. All three are PD10 amendment targets.*

---

### RG-2 — CRITICAL — The replacement bridge arbitration is falsified by the same code that falsified the one it replaced

architecture.md line 676 (pass-2 text, replacing pass 1's rejected "sole writer" rule):

> The arbitration is therefore a **field split, enforced by the interface**: … The **projection** … creates and invalidates entries from domain events and sets `Stale`. It never writes an egress outcome. … The **egress writer** (`ISemanticIndexingBridgeWriter`, registered in Workers only …) owns the egress outcome … Neither writes the other's field, and **the interface makes that structural rather than conventional**.

Three defects, in ascending order of consequence.

**(a) "only ever writes `Stale`" is false.** `SemanticIndexingBridgeProjection.cs` writes `SemanticIndexingBridgeStatus.Stale` at `:202` and `SemanticIndexingBridgeStatus.Tombstoned` at `:296-297` and `:361`. Two statuses, not one.

**(b) `SemanticIndexingBridgeProjection` is not a writer at all.** It is a `sealed record` with an immutable `IReadOnlyDictionary<string, SemanticIndexingBridgeEntry> Entries` and a pure `Apply(IEnumerable<FolderProjectionEnvelope>)` returning a new instance (`:8-40`). It is never registered in DI and persists nothing. Assigning it ownership of "entry existence" as a *writer* in a two-writer arbitration mis-describes what the type is.

**(c) The interface does not enforce the split — it spans it.** `ISemanticIndexingBridgeWriter` declares **three** methods:

```
Task<IReadOnlyList<SemanticIndexingBridgeEntry>> ApplyFolderEventsAsync(...)   // the projection half
Task<SemanticIndexingBridgeEntry?> RecordIndexingResultAsync(...)              // the egress half
Task<SemanticIndexingBridgeEntry?> RecordRemovalEvidenceAsync(...)
```

and **both** implementations implement the whole interface: `EventStoreSemanticIndexingBridgeStore.cs:17` and `InMemorySemanticIndexingBridgeStore.cs:5`, each `: ISemanticIndexingBridgeReadModel, ISemanticIndexingBridgeWriter`. So a component holding the writer registration can write *both* halves; a component without it (Server) can write *neither*. The document's supporting sentence — *"a component without the writer registration cannot record an outcome at all"* — is true but proves only half-containment, and the conclusion it is offered for ("neither writes the other's field … structural rather than conventional") does not follow from it.

The parts that **do** verify: `FoldersWorkersModule.cs:73-76` registers `EventStoreSemanticIndexingBridgeStore` as the writer ✓; `FoldersServerServiceCollectionExtensions.cs:130` documents and `FoldersContextSearchFacadeRegistrationTests.cs:102` asserts that the Server resolves no writer ✓; `RecordIndexingResultAsync` is watermark-gated and refuses `Tombstoned → Indexed` regression (`SemanticIndexingBridgeProjection.cs:83`, `:112`) ✓.

**Why critical.** This is the rule Story 12.5's reconciler must obey, and it is the second consecutive arbitration written against a model of the code that the code does not have. The as-built enforcement is *registration scope* (Workers-only) plus *watermark gating inside `RecordIndexingResultAsync`* — both real, both defensible, neither structural in the interface. A 12.5 implementer told the interface partitions the fields will not add the guard that actually does the work.

---

### RG-3 — HIGH — `LockLeaseBecameStale` is described three mutually exclusive ways, and the gate that should catch it compares two documents to each other and never to the code

Three statements in the same document:

| Line | Statement |
| --- | --- |
| 242 | "`LockLeaseBecameStale` does not exist." |
| 413 | A live C6 matrix row: `dirty` → `ready` on `LockLeaseBecameStale`. |
| 438 | "**`LockLeaseBecameStale` is a new event** added to the architecture event vocabulary **by this correction**." |

Line 438 is false at HEAD. `git show HEAD:` on the three doc-side artifacts:

- `docs/diagrams/workspace-lifecycle.md` — **3 occurrences at HEAD**, and the file is *unmodified in this working tree*
- `docs/exit-criteria/c6-transition-matrix-mapping.md` — 1 at HEAD
- `_bmad-output/planning-artifacts/architecture.md` — 5 at HEAD

The event was already in the approved vocabulary and the shipped lifecycle diagram before this update touched anything. What is genuinely absent is the **code enum** (`FolderWorkspaceLifecycleEvent.cs` — 23 members, `LockLeaseExpired` at `:21` is the only lease event) and the **OpenAPI spine** (`grep` → 0 occurrences in `hexalith.folders.v1.yaml` and in the generated client).

**The structurally interesting part.** `ConsumerDocsConformanceTests.ParseC6EventVocabulary()` reads `c6-transition-matrix-mapping.md` and asserts `.Count.ShouldBe(24)` — and it passes, because the doc lists 24. `WorkspaceLifecycleDiagramEventsEqualC6Vocabulary` then compares that to the diagram. `ExitCriteriaDecisionArtifactTests.C6MappingArtifactMirrorsArchitectureVocabularyBidirectionally` compares the mapping doc to architecture.md against a hard-coded 23-name `C6Events` array (`:109-135`, `ShouldContain`, so a 24th passes silently). **No gate anywhere compares any of this to `FolderWorkspaceLifecycleEvent`.** A 24-document / 23-code divergence has been green on `main` the entire time, and the document's own line 670 (*"`FolderWorkspaceLifecycleEvent` is a published OpenAPI enum … adding the event touches the spine, the client, `previous-spine.yaml`, and the C13 inventory"*) is correct about the obligation while line 438 mis-scopes where the gap is.

Fix: line 242 → "does not exist **in `FolderWorkspaceLifecycleEvent` or the OpenAPI spine**"; line 438 → "is already in the approved C6 vocabulary and the lifecycle diagram; the correction adds it to the aggregate enum and the spine"; and record the doc↔doc-only gate as the reason the drift was invisible.

---

### RG-4 — HIGH — The `oasdiff` correction inverted: the three documents named as "still owed the correction" were corrected in the same pass, and the one that still owes it is unnamed

architecture.md line 696 (A-7, pass-2 text):

> the 2026-05 draft named `oasdiff` as the classifier, which was never adopted and appears in **no build, test, or tooling artifact**. **Three published documents still name it and are owed the same correction:** `docs/adrs/0003-provider-abstraction-and-capability-model.md`, `docs/runbooks/provider-drift.md`, and `docs/runbooks/index.md`.

All three were edited in this same working tree, in this same pass. `git diff` confirms each substitution:

- `docs/adrs/0003-…md:23` → "the nightly drift classifier (`tests/tools/forgejo-drift/`) classifies…"
- `docs/runbooks/index.md:13` → "additive, breaking, and unknown **provider schema** drift"
- `docs/runbooks/provider-drift.md:3` → "the nightly drift lane (`tests/tools/run-nightly-drift-gates.ps1` driving `tests/tools/forgejo-drift/`)"

`git grep oasdiff` over tracked files outside `references/` and `_bmad-output/` now returns **zero hits**. The architecture asserts a remediation debt that its own commit discharged.

Meanwhile the debt that *does* remain is not named: **`_bmad-output/planning-artifacts/epics.md:327`** — *"Nightly oasdiff schema-diff job classifies additive (warn) vs breaking (fail)"* — inside `AR-PROVIDER-04`, i.e. an acceptance-requirement line in the planning authority this document is required to stay in lockstep with. Three story files carry it too (`3-4-…md`, `7-8-…md`, `7-17-…md`).

The first clause ("no build, test, or tooling artifact") is now correct and verified ✓, as is the as-built substitution (`run-nightly-drift-gates.ps1` + `tests/tools/forgejo-drift/` both present ✓). I also confirmed **no test pins the replaced phrasing**, so the three doc edits are gate-safe — `grep` for `oasdiff` and for the substituted phrases across `tests/**/*.cs` returns nothing, and Contracts.Tests is 314/314 green.

---

### RG-5 — HIGH — Line 253 still states the claim that line 264 was rewritten to retract, eleven lines apart

- **253:** "…uploaded file content is base64-decoded then discarded; **the provider Git write path throws `NotImplementedException`**; no component transitions a task to `completed`…"
- **264 (pass-2 correction):** "There are **no** `NotImplementedException` workspace-executor methods; the **single** `NotImplementedException` in `src/` is the deliberate live-readiness seam in `OctokitGitHubApiClient.cs:60`, a *readiness probe* and not a write path, and **provider write operations are implemented**."

`grep -rn "NotImplementedException" src/ --include=*.cs` returns exactly one hit — `OctokitGitHubApiClient.cs:60`, inside `GetReadinessAsync` ✓. Line 264 is right; line 253 is wrong.

Line 253 is pre-existing (it is line 251 at HEAD) and is a quotation of the 2026-07-14 audit. That is exactly why it needed the same edit: pass 2 corrected the derived statement and left the quoted premise standing, so the document's "plain language" summary of durable-state status — the paragraph an executive reader stops at — still carries the falsehood, and the correction is 11 lines below it under a story heading. This is the pass-1 failure shape (UPD-11) at close range rather than across 1,000 lines, and it is the same shape the memlog records pass 2 catching in itself on the Story 10.9 edit.

Same paragraph, unverified this pass and worth the same treatment: "uploaded file content is base64-decoded then discarded" and "no component transitions a task to `completed`" are audit-quoted present-tense claims about `src/` that nothing in this document re-verifies at HEAD.

---

### RG-6 — HIGH — The half-applied honesty labelling is ~40% closed; the survivors include a checked box and the CI-gates enumeration

Pass 2 closed five sites and did it well — line 1849 (defence-in-depth) now explicitly names **all three** downgrades in one sentence, which is the model the rest should follow; 1760, 1763, 1081 and 1876 are likewise corrected. Nine remain, and one of them I had not previously flagged:

| # | Line | Still asserts | Reality |
| --- | --- | --- | --- |
| C10 | **116** | concern #13: "build-time/CI lint check enforces it as a hard gate" | no lint job in `ci.yml`; no `Caching/`, no `*TenantPrefixedCacheKey*` — **not previously flagged** |
| C10 | 330 | criterion statement: "Cache-key tenant-prefix **lint** enforcement (CI/build-time gate, naming convention, tooling)" | as above |
| C10 | 766 | I-5: "pipeline gates: build, format, lint (**including C10 cache-key tenant-prefix lint**)" | as above |
| C10 | 1100 | "**Build-time (lint):** Cache-key tenant-prefix lint (C10)" | as above |
| C10 | 1748 | "cache-key tenant-prefix (C10 **lint**)" | as above |
| I-8 | 793 | Phase 6: "provider rate-limit handling **with chaos-test gate**" | no `RateLimiting/`, no `*TokenBucket*` |
| I-3+I-8 | **1102** | "**CI gates:** … Dapr-policy conformance negative tests; **provider-rate-limit chaos test**; …" | the Dapr suite is schedule-only (line 764's own retraction); the chaos test does not exist |
| I-8 | 1587 | "`Hexalith.Folders.Workers` owns process managers / reconcilers / **rate-limit buckets** / tenant-event handlers" | `ls src/Hexalith.Folders.Workers/` → `RepositoryProvisioning`, `SemanticIndexing`, `Tenants`, `Properties` |
| I-8 | **1821** | `- [x] Performance considerations addressed (PRD budgets + F-7 console budget + **provider-rate-limit chaos**)` | a **ticked validation checkbox** crediting a gate that does not exist |

1102 and 1821 are the worst two placements available: an enumerated CI-gate list and a checked box are the forms a reader quotes without reading around them.

---

### RG-7 — HIGH — The structural remedy is one-eighth applied, and the one marker added made an adjacent entry read *more* as-built

Pass 2 added exactly one new marker — `ProviderRateLimitChaos/ # NOT BUILT` at 1542. Everything else I itemised is unchanged. Verified against the filesystem this pass:

| Tree entry | Line | Marked? | On disk |
| --- | --- | --- | --- |
| `WorkspaceWorkflows/` + `WorkspacePreparationWorkflow.cs`, `WorkspaceCleanupWorkflow.cs`, `WorkingCopyManager.cs` | 1469-1472 | no | **absent** |
| `RepositoryWorkflows/` | 1473 | no | dir is `RepositoryProvisioning/` |
| `CommitWorkflows/` + `CommitWorkflow.cs`, `CommitReconciler.cs` | 1476-1478 | no | **absent** |
| `SearchIndexing/` | 1479 | no | dir is `SemanticIndexing/` |
| `RateLimiting/` ×3 | 1480-1482 | **`NOT BUILT`** | absent ✓ |
| `tests/…/DaprPolicyConformance/` | **1541** | no | **absent** |
| `tests/…/ProviderRateLimitChaos/` | 1542 | **`NOT BUILT`** | absent ✓ |
| `tests/Hexalith.Folders.Tests/{Idempotency,Caching}/` | 1526-1527 | no | absent (real: `Aggregates`, `Authorization`, `Observability`, `Projections`, `Providers`, `Queries`) |
| `tests/tools/policy-conformance/` | 1562 | no | **absent** (real: `forgejo-drift`, `parity-oracle-generator`, `pattern-examples`) |
| `src/Hexalith.Folders/Search/` | 1331 | no | **absent** |
| `src/Hexalith.Folders/Idempotency/` ×3 | 1332-1335 | no | **absent** |

**Lines 1541 and 1542 are now adjacent, and only the second is marked.** Before pass 2 both were bare and the banner covered them equally; now the contrast is explicit and a reader reasonably infers that the unmarked one was checked and found present. The same applies at 1562, still sitting between `forgejo-drift/ # … (as-built)` and `parity-oracle-generator/`.

Two residuals from UPD-7 are also untouched: `Hexalith.Folders.EventStore.Tests` **exists on disk** (`tests/Hexalith.Folders.EventStore.Tests/`) and is still absent from the detailed `tests/` tree (1519-1542) though present in the high-level one; and line 1331's new comment routes deployed bridge projections to `src/Hexalith.Folders/Projections/Search/`, which does not exist (real: `Projections/SemanticIndexing/`).

The banner is the right instrument. The fix remains what it was: mark everything the edit verified, in the block where it verified it — or mark nothing and let the banner carry it.

---

### RG-8 — MEDIUM — "Neither `kind` nor `daprd` appears in any workflow, test, or tooling artifact" is still false, in a *narrowed* claim that pass 2 chose to keep absolute

architecture.md line 764 (I-3, pass-2 softening). `kind` verifies ✓ — no hits in `.github/workflows/`. `daprd` does not:

- `tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:295` — *"the daprd process exposes it host-reachably"* — a **test artifact**, the exact category the sentence excludes
- `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml:23` — *"REDIS_HOST left the literal string as the hostname and daprd failed with…"*

The rest of the I-3 rewrite is excellent and remains verified: `policy-conformance.yml` is `schedule:` + `workflow_dispatch:` only and cannot block a merge ✓; the as-built suite is static-fixture ✓; `tests/tools/run-scheduled-policy-conformance-gates.ps1` exists ✓. The deferral citation (Story 7.1's record, with its explicit *"do not silently drop the live-gate requirement"*) is now carried, which was the point ✓.

The pattern worth naming: pass 2 narrowed the absolute from "anywhere in the repository" to "in any workflow, test, or tooling artifact" without re-running the search against the narrowed scope. Drop "test" from the list, or say "no gate runs `daprd`", which is both true and stronger.

---

### RG-9 — MEDIUM — S-7's "the oracle has no row for" is false and is refuted by the next clause of its own sentence

architecture.md line 657:

> Today the oracle and this document's own canonical table have **no row** for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable`; the shipped `parity-contract.yaml` **maps them to exit 65 and 72**…

Both halves of that sentence cannot be true. `tests/fixtures/parity-contract.yaml` carries all three:

| Row | Line | `cli_exit_code` | `mcp_failure_kind` |
| --- | --- | --- | --- |
| `authentication_failure` | 56-58 | 65 | `authentication_failure` |
| `projection_unavailable` | 596-598 | 72 | `projection_unavailable` |
| `read_model_unavailable` | 604-606 | 72 | `read_model_unavailable` |

Every *fact* in the clause is right — 65 and 72 are exact, `not_found` → 73 is exact (`:1023-1025`), the `auth_outcome_class` set is exactly `{audit_access_denied, folder_acl_denied, tenant_access_denied}` ✓, the matrix genuinely carries no `cli_exit_code` or `mcp_failure_kind` column (`grep` → 0) ✓, and architecture.md line 730 does define exit 72 as `reconciliation_required` / *"not retryable until cleared"* ✓. Only the framing is wrong, and it costs the section its actual conclusion.

The stated conclusion — *"CLI and MCP **can** disagree about an authority outage"* — is not what the evidence shows, because CLI and MCP are derived from the **same oracle row** and cannot diverge from each other. The real defect is sharper: **exit 72 is shared by four categories** — `projection_stale`, `projection_unavailable`, `read_model_unavailable`, and `reconciliation_required` — so a CLI caller cannot distinguish the retryable 503 authority outage from the non-retryable reconciliation state, and this document's own definition of 72 tells them not to retry. That is a genuine caller-visible collision, and it survives the correction as written.

Correct it to: *the rows exist and collide; exit 72 is overloaded across four categories, one of which this document defines as non-retryable, contradicting the 503's `retryable: true`. The regeneration must split them.*

---

### RG-10 — MEDIUM — Requirements-to-Structure: nine of nine probed target paths are absent; the banner names two

Banner at 1609 still names only `Caching/TenantPrefixedCacheKey.cs` and the `ci.yml` lint job. Probed this pass:

| Row | Line | Target | On disk |
| --- | --- | --- | --- |
| FR1–FR3 | 1613 | `docs/contract-terms.md` | **absent** (real vocabulary: `docs/contract/`, 19 files, still never named in the tree) |
| FR24–FR31 | 1617 | `src/Hexalith.Folders.Workers/WorkspaceWorkflows/` | **absent** |
| FR32–FR36 | 1618 | `src/Hexalith.Folders/Queries/Context/` | **absent** (real: `Queries/FileContext/`, `Queries/ContextSearch/`) |
| FR37–FR42 | 1619 | `src/Hexalith.Folders/Idempotency/`, `Workers/CommitWorkflows/` | **both absent** — adapters are in `src/Hexalith.Folders.EventStore/`, as the document's own boundary bullet at 1588 says |
| FR43–FR46 | 1620 | `src/Hexalith.Folders/Projections/WorkspaceStatus/` | **absent** (`Projections/` = `FolderAccess`, `FolderList`, `SemanticIndexing`, `TenantAccess`) |
| FR53–FR57 | 1627 | `src/Hexalith.Folders/Projections/Audit/`; `src/Hexalith.Folders.Server/Endpoints/AuditEndpoints.cs` | **both absent** — real: `Queries/Audit/` and `src/Hexalith.Folders.Server/AuditEndpoints.cs` (repo root of the Server project, not an `Endpoints/` folder) |
| FR58 | 1628 | `src/Hexalith.Folders/Search/`, `Workers/SearchIndexing/`, `Projections/Search/` | **all three absent** |

The document routes an implementer to `src/Hexalith.Folders/Idempotency/` in two places (1619, and cross-cutting concern #21) while its own as-built boundary bullet says idempotency lands in `Hexalith.Folders.EventStore` — and line 1876 instructs implementers to "respect project structure and boundaries."

---

### RG-11 — MEDIUM — REAL-7 untouched: `FolderWorkspaceDirtyResolution` is still named as the guard discriminator for four pairs it does not serve, while the one pair it does serve is still missing from the list

architecture.md line 446: *"a switch expression over **`(currentState, eventType, resolution)`** … where `resolution` is the `FolderWorkspaceDirtyResolution` discriminator (per Step 5 §"Process Patterns"). PD11 makes **four pairs** guard-discriminated…"* followed by the four PD11 pairs.

As-built: `FolderWorkspaceDirtyResolution.cs:6-12` has exactly two members (`CommitConfirmed`, `CommitRejected`) and is consumed at exactly two sites — `FolderStateTransitions.cs:110` and `:112`, both for `(UnknownProviderOutcome, ReconciliationCompletedDirty)`. That pair appears in **none** of the four. All four PD11 pairs are `null`-resolution arms today (`:90-91` `(ChangesStaged, CommitFailed, null) => Failed`; `:107-108` `(Inaccessible, ProviderReadinessValidated, null) => Ready`). The *"(per Step 5 §'Process Patterns')"* cross-reference is still dangling.

`docs/exit-criteria/c6-transition-matrix-mapping.md` now carries a four-row "Guard Discriminators" table with the same four pairs and the same omission, so the two artifacts are consistently wrong together — which is the shape that survives review longest.

**Verified positively and worth recording:** the `stagedByTaskId` predicate unification landed and is genuinely identical across both files — architecture line 447 and the mapping doc's table row both read *staged changes present **AND** `stagedByTaskId` equals the server-resolved task of the re-acquiring command, both conjuncts*, with `stagedByPrincipal` demoted to audit evidence read by no guard, and both note neither field exists in `src/` ✓ (`grep -rni stagedBy src/` → 0). The three-column C6 header was correctly preserved and the 41-edge pin still passes ✓.

---

### RG-12 — MEDIUM — The PD10 reality table still understates `visibility` while S-7 states it correctly, 414 lines apart

Line 243: *"`visibility` exists **only as the constant** `details.visibility=\"metadata_only\"`, not as a required top-level error field."*
Line 657 (pass 2): *"`visibility` today is a **`details` field** carrying `redacted`, and the shipped `DetailsVisibility` set is `{redacted, metadata_only}`."*

The spine settles it: `hexalith.folders.v1.yaml:7738-7748` declares `details` with `required: [visibility]` and `visibility: { type: string, enum: [redacted, metadata_only] }`. It is a **required two-member enum**, and all three canonical envelopes use `redacted`, not `metadata_only`. Line 657 is right, line 243 is wrong, and 243 is in the table a reader consults to learn what is true today. Same correction, applied once.

The rest of the PD10 reality row verifies: G1's "403 on 49 of 49, 404 on 46 of 49" is verbatim from `authorization-matrix.md:341` ✓ and 49 is independently pinned by `ConsumerDocsConformanceTests` (`spineOps.Count.ShouldBe(49)`) ✓. `audit_access_denied` and `not_found` are emitted at `src/Hexalith.Folders.Server/AuditEndpoints.cs:461-470` ✓; `cross_tenant_access_denied` is **not** emitted there (it lives in `WorkspaceStatusQueryHandler.cs:34` and `ConsoleStatusText.cs:36`), so attributing all three to `AuditEndpoints.cs` is one name too many.

---

### RG-13 — MEDIUM — "`withheld` has zero occurrences anywhere" is a false absolute, and the collision it hides is the interesting part

architecture.md line 657: *"`withheld` has **zero occurrences anywhere**, as this document's own reality table records."*

Seven tracked occurrences outside `references/` and `_bmad-output/`. As a *vocabulary token* the claim holds — none is an enum member ✓ — but one of them is load-bearing for PD8:

- **`src/Hexalith.Folders.UI/Services/ConsoleStatusText.cs:45`** — `["redacted"] = "The requested evidence is withheld by tenant policy."`
- `docs/ux/ops-console-wireflows.md:256` — the `redacted` row renders *"Hidden by tenant policy — value exists but is **withheld**"*
- `docs/contract/safety-invariant-ci-gates.md:59` — *"`redacted`: a value exists for an authorized audience but is deliberately **withheld**"*
- plus `TenantAccessState.cs:25`, `TrustDimensionState.cs:28`, `MetadataOnlyFolderTreeTests.cs:78`

So the shipped console **already uses the word "withheld" as the user-facing rendering of `redacted`**, and an approved contract document already defines `redacted` that way. PD8 proposes `withheld` as a *distinct* render state that must be distinguishable from `redacted`. That is a naming collision with shipped UI copy and an approved contract definition, and it is exactly the kind of thing a "zero occurrences" sweep is supposed to surface. Say "zero occurrences as a vocabulary token, but the word is already the shipped UI rendering of `redacted` in `ConsoleStatusText.cs:45` — PD8 must rename one of them."

---

### RG-14 — MEDIUM — The NFR74 fix left two paragraphs saying the same thing four lines apart

Removing the false "no Epic 13 story owns NFR74" was correct ✓ (`nfr-traceability.md:120` → `13-2`; `sprint-status.yaml:243` carries the story). But the replacement did not displace the earlier paragraph:

- **278:** "**Per-row ownership is the traceability table, not the band.** … As recorded there: Epic 13 owns NFR74 and NFR76 (`13-2`), NFR75 (`13-1`), NFR77 (`13-3`), NFR78 and NFR84 (`13-6`), NFR81 (`13-4`), NFR82 (`13-5`); NFR79 → `12-1`, NFR80 → `12-2`; NFR83 → `7-16`."
- **286:** "**Per-row NFR ownership, since band-level admission is not per-row ownership** (`nfr-traceability.md` assigns 74 and 76 to 13-2, 75 to 13-1, 77 to 13-3, 78 and 84 to 13-6, 81 to 13-4, 82 to 13-5 — while NFR79/NFR80 are Epic 12 substrate mechanisms and NFR83 belongs to 7-16). Band-level admission is not per-row ownership…"

Same eleven assignments, same source, same conclusion, and 286 says "band-level admission is not per-row ownership" twice inside itself. Delete 286. (The underlying facts verify: all eleven `NFR74`–`NFR84` rows carry `—` for gates at `nfr-traceability.md:120-130` ✓.)

---

### RG-15 — LOW — One bare `49` survives, though it is gate-backed

Line 684: *"**The 49 protected operations** are protected because each one opts in."* The generated-denominator rule at 704 forbids transcribing it; the count is correct and, unlike when I first raised this, I can now confirm it is **pinned by a merge-blocking test** (`ConsumerDocsConformanceTests`: `spineOps.Count.ShouldBe(49, "the spine must carry the canonical 49-operation surface")`). Line 243's use is a quotation of the matrix and is fine. So: either drop the bare 49 at 684, or amend the rule at 704 to say "49 is transcribed in two places and pinned by `ConsumerDocsConformanceTests`" — the second is honest and cheaper.

---

### RG-16 — LOW — REAL-12 partially closed; two count mismatches remain

Closed ✓: line 1814 now reads `- [~] Cross-cutting concerns mapped — **12 of 22**`, with the remainder named as unrouted. That is the right form. Open:

- **1847** — "11 states × disposition labels × **~30 transitions**" vs the 41 edges the C6 gate pins
- **55** — "58 functional requirements across **12 capability blocks**", followed by eleven named blocks

---

### RG-17 — LOW — "Minor Gaps" still lists two shipped artifacts as gaps

Lines 1788-1789 still describe `docs/adrs/0000-template.md` and `docs/runbooks/tenant-deletion.md` as work to be authored. Both files exist. (`docs/runbooks/index.md` — which pass 2 edited — enumerates the runbook set one directory away.)

---

### RG-18 — LOW — `release-packages.yml` builds in both modes, not only the NuGet one

Line 536 is otherwise **correct and closes UPD-8 properly** ✓ — I re-derived `Directory.Build.props:15-22` and the conclusion holds: `Configuration` is the selector, `Debug`/unset → project references, `Release` → packages, and a plain `dotnet build -c Release` resolves packages with nobody passing a flag. Two small residuals:

1. `release-packages.yml` restores and builds in the **default (source) mode** at `:60,:63` before its `-p:UseNuGetDeps=true` lanes at `:131,:134` and `:180,:183`. "is what `release-packages.yml` passes" is true of three of five invocations.
2. The props file's own comment names `-p:UseHexalithProjectReferences=true|false` as the forcing switch, and that condition is evaluated *before* `Configuration`; the row names the derived alias `UseNuGetDeps` instead. Both work; naming the one the file documents avoids a second convention.
3. "the `Hexalith*Version` properties in `Directory.Packages.props`" is still ambiguous between the repo-root file (one `PackageVersion`: `LibGit2Sharp 0.32.0`) and `references/Hexalith.Builds/Props/Directory.Packages.props` (where the `Hexalith*Version` properties live). Line 1746 names the right one; make them agree.

---

## Re-gate of the eight pass-1 false claims

| # | Pass-1 false claim | Pass-2 disposition | Verdict |
| --- | --- | --- | --- |
| 1 | Parity-schema pins the PD10 vocabulary | Rewritten to "closes exactly two axes / no closed vocabulary anywhere" | **NOT CLOSED — replaced by a new false claim (RG-1, CRITICAL)** |
| 2 | "No workspace-executor type exists" | Rewritten to fail-closed-not-throwing, `FoldersServerServiceCollectionExtensions.cs:67` | **CLOSED** ✓ line 67 verified exact; residual RG-5 |
| 3 | "NFR74 unowned" | Removed; per-row ownership stated | **CLOSED** ✓ residual RG-14 |
| 4 | "`oasdiff` exists nowhere" | Softened; three docs edited | **NOT CLOSED — inverted into a new false claim (RG-4, HIGH)** |
| 5 | "The matrix settles stale-authority degraded mode" | Reversed to an open item citing `:76` vs `:146` + `allowBoundedStale: true` | **CLOSED** ✓ — the strongest correction in the pass |
| 6 | Package-management default | Corrected to `Configuration`-driven | **CLOSED** ✓ residual RG-18 |
| 7 | "One adapter per mutating command" | "13 adapters keyed on the domain command, not one-per-spine-operation" | **CLOSED** ✓ 13 verified exactly |
| 8 | "Neither `kind` nor `daprd` appears anywhere" | Narrowed to "no workflow, test, or tooling artifact" | **PARTIAL — still false for `daprd` (RG-8)** |

**5 of 8 closed, 1 partial, 2 replaced with new falsehoods.**

## Re-gate of the pass-2-specific claims the brief named

| Claim | Verdict |
| --- | --- |
| Deployed-Server limitation **CLOSED** (both sites, 149-153 and 183) | **TRUE** ✓ `sprint-status.yaml:203-204` → 10-7 `done`, 10-8 `done`; `FoldersServerServiceCollectionExtensions.cs:131` `AddEventStoreReadModelStore()`, `:132` `RemoveAll<ISemanticIndexingBridgeReadModel>()`, `:133-134` binds `EventStoreSemanticIndexingBridgeStore`; `:130` comment + `FoldersContextSearchFacadeRegistrationTests.cs:102` assert no writer. Note 10-9 is `review`, not `done` — the document correctly avoids claiming otherwise |
| Bridge field split (projection writes only `Stale`; `Indexed` only via `RecordIndexingResultAsync`) | **FALSE two ways** — RG-2 |
| `.slnx` **14 src / 17 test**, `ScaffoldContractTests` red on its two newest | **TRUE** ✓ counted 14 and 17 exactly; `ScaffoldContractTests.cs` has **zero** occurrences of `Hexalith.Folders.EventStore` while `.slnx` declares both projects, and `:124` asserts exact-set equality. UPD-12 closed |
| C9 verification method names the **Dapr state store** | **TRUE** ✓ line 356: *"no durable cleartext in the **Dapr state store** (explicitly in scope — the PD8 carve-out's only candidate home, so a gate that omits it certifies green over the exact hole)"* |
| Residual I-3/I-8/C10 in 12 other locations | **PARTIAL — 5 closed, 9 open, one new found (line 116)** — RG-6 |
| Structural remedy / `NOT BUILT` marker asymmetry | **1 of 8 applied, and the one added worsened an adjacency** — RG-7 |

---

## New false claims pass 2 introduced

Statements about the codebase that the pass-2 edit added and that are not true at HEAD `de281e7`:

1. **"`parity-contract.schema.json` closes exactly two axes — `canonical_error_category` and `mcp_failure_kind`"** (line 657). It closes **fourteen** enums: four `$defs` (`canonical_error_category` 50, `mcp_failure_kind` 49, `cli_exit_code` 15, `adapter_name` 5) plus ten per-property enums. — RG-1
2. **"The `code`, `clientAction`, and `visibility` axes have no closed vocabulary anywhere"** (line 657). `hexalith.folders.v1.yaml:7674-7682` (7-member `clientAction`), `:7732-7737` (3-member `clientAction`), `:7741-7748` (`required` 2-member `visibility`); plus nine single-member `code`/`clientAction` enums in `hexalith-extension-vocabulary.yaml`. Contradicted three clauses earlier in the same sentence. — RG-1
3. **"Today the oracle … ha[s] no row for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable`"** (line 657). Rows at `parity-contract.yaml:56-58`, `:596-598`, `:604-606` — refuted by the next clause of the same sentence, which reports their exit codes. — RG-9
4. **"`SemanticIndexingBridgeProjection` only ever writes `Stale`"** (line 676). It writes `Tombstoned` at `:296-297` and `:361`. — RG-2
5. **"Neither writes the other's field, and the interface makes that structural rather than conventional"** (line 676). `ISemanticIndexingBridgeWriter` declares `ApplyFolderEventsAsync` **and** `RecordIndexingResultAsync`; `EventStoreSemanticIndexingBridgeStore:17` and `InMemorySemanticIndexingBridgeStore:5` each implement the whole interface. — RG-2
6. **"Three published documents still name [`oasdiff`] and are owed the same correction"** (line 696). All three were corrected in this same working tree; `git grep oasdiff` outside `references/`/`_bmad-output/` returns zero. The document that still owes it — `epics.md:327` — is not named. — RG-4
7. **"`withheld` has zero occurrences anywhere"** (line 657). Seven tracked occurrences, including `ConsoleStatusText.cs:45`, where "withheld" is the shipped rendering of `redacted`. — RG-13
8. **"`LockLeaseBecameStale` is a new event added to the architecture event vocabulary by this correction"** (line 438). Present at HEAD in `architecture.md` (×5), `c6-transition-matrix-mapping.md` (×1) and the **unmodified** `docs/diagrams/workspace-lifecycle.md` (×3). — RG-3

Two further statements are narrowed-but-still-wrong rather than newly invented: "no workflow, test, or tooling artifact" for `daprd` (RG-8), and the survival of line 253's `NotImplementedException` premise under a correction that retracts it (RG-5).

---

## Carried forward — unchanged

| Prior | Sev | Status at this HEAD |
| --- | --- | --- |
| REAL-7 / UPD — `FolderWorkspaceDirtyResolution` misattributed | HIGH | **Open**, now mirrored into `c6-transition-matrix-mapping.md` — RG-11 |
| REAL-10 / UPD — `visibility` understated at line 243 | MEDIUM | **Open**, and now self-contradicted by line 657 — RG-12 |
| UPD-13 — Requirements-to-Structure rows | MEDIUM | **Open**, 9 of 9 probed paths absent — RG-10 |
| REAL-12 — validation-results counts | MEDIUM | **Partially closed** (1814 fixed); 1847 and 55 open — RG-16 |
| REAL-13 — "Minor Gaps" lists shipped artifacts | LOW | **Open** — RG-17 |
| REAL-14 — provider adapters described as projects (1584/1589), "mechanically enforceable via project references" (1750) | LOW | **Open**, untouched |
| UPD-14 — "13 src / 11 test-load" | MEDIUM | **CLOSED** ✓ line 1774 now defers to `.slnx` with the counts and the red test named |
| UPD-12 — `.slnx` authority vs `ScaffoldContractTests` | MEDIUM | **CLOSED** ✓ the conflict is now disclosed in the same sentence as the authority |
| UPD-16 — bare `49` | LOW | **Mostly closed**; one survivor, gate-backed — RG-15 |

---

## Closure tally across both passes

| Bucket | Count |
| --- | --- |
| Pass-1 false claims closed at the root | **5 of 8** |
| Pass-1 false claims partially closed | 1 (`daprd`) |
| Pass-1 false claims replaced by new false claims | 2 (parity schema → RG-1; `oasdiff` → RG-4) |
| Prior structural findings closed | **2** (UPD-12 `.slnx` disclosure, UPD-14 counts) |
| Prior structural findings partially closed | 2 (UPD-11 labelling 5/13; REAL-12 counts 1/3) |
| Prior structural findings untouched | 4 (UPD-6 markers 1/8, UPD-13 routing, REAL-7, REAL-10) |
| New false claims introduced by pass 2 | **8** |
| Criticals carried or newly raised | **2** |

Prior-review disposition: of 16 findings, **4 closed**, 3 partially closed, 7 open, 2 closed-and-reopened-differently.

---

## Notes for the gate owner

1. **RG-1 and RG-2 are the two that must not ship**, for the same reason as last pass: both are in material a downstream owner will *act on*. The PD10 owner is told there is no `clientAction`/`visibility` vocabulary to amend when the shipped spine has two incompatible `clientAction` enums and a required `visibility` enum; the Story 12.5 implementer is told the interface partitions the bridge fields when it unifies them. Both corrections are one sentence, and — as last time — the correct sentence is almost present in the surrounding text.
2. **The pass-2 fix rate on *new* text is the number to watch.** Pass 1 introduced 8 false claims in ~91 changed lines; pass 2 closed 5 of them and introduced 8 more in ~89 changed lines. Two of the three sentences that failed twice (S-7 provenance, the bridge arbitration) failed in *opposite directions* on the second attempt — over-claiming, then over-retracting. Both would have been caught by running the same `grep` against the rewritten sentence that justified the rewrite. A third pass that only edits should be expected to introduce roughly as many again; the defect is in the verify-after-edit step, not in the editor's judgment.
3. **RG-3 is the finding to escalate past this document.** A merge-blocking gate that compares `c6-transition-matrix-mapping.md` to `docs/diagrams/workspace-lifecycle.md` to `architecture.md` and never to `FolderWorkspaceLifecycleEvent` will stay green through any doc-only vocabulary change. The 24-vs-23 divergence is live on `main` today. That is a gate defect with an owning story implication (PD11), not a prose defect.
4. **Cheapest high-value fixes, in order:** delete line 253's clause (RG-5, one clause); delete line 286 (RG-14, one paragraph); add `NOT BUILT` to the seven bare tree entries (RG-7, seven comments); correct lines 1102 and 1821 (RG-6, the two most citable survivors); correct line 696's "three documents" to name `epics.md:327` (RG-4, one clause).
5. **Positive, and worth preserving verbatim as the model for the rest:** line 1849's defence-in-depth sentence, which names all three downgrades in one place instead of retracting them piecemeal; line 1774's deferral to `.slnx` *with* the failing test disclosed; and line 674's degraded-mode reversal, which states the contradiction, cites both sides, names the shipped third answer, and routes it — rather than picking a winner it has no authority to pick.
6. **Verification status of this re-gate:** all filesystem, `grep`, `git show HEAD:`, and JSON-schema facts above were derived at HEAD `de281e7` with the working tree as-is. `Hexalith.Folders.Contracts.Tests` was executed: 314/314 pass. `ScaffoldContractTests` was *not* executed (it lives in `Hexalith.Folders.Testing.Tests`); its red status is established by static inspection of `ExpectedSolutionProjects` against `.slnx`, as the document itself states.
