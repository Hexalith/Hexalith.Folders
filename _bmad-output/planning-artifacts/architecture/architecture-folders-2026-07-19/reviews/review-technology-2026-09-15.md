# Reviewer Gate — Technology / Currency & Reality-Check Lens

- **Artifact under review:** `_bmad-output/planning-artifacts/architecture.md` (2026-09-15 amendment applying §5.2 of `sprint-change-proposal-2026-09-15.md`)
- **Diff reviewed:** `git diff -- _bmad-output/planning-artifacts/architecture.md` at working tree, branch `main`, HEAD `1621358`
- **Reviewer lens:** Verify every committed decision was researched or reality-checked rather than asserted. In this **brownfield** repo, "reality-checked" means: checked against the actual code, the pinned package versions, the published contract artifacts, and the sibling Hexalith submodules under `references/`.
- **Date:** 2026-09-15
- **Verdict:** **PASS WITH FINDINGS** — every version, digest and inventory number in the amendment checks out exactly against the real files, and the S-7/S-8/PD11 substance is a faithful transcription of the approved proposal. Four HIGH findings are statements *about the repository and its CI* that were asserted rather than checked, and they should be corrected before this document is cited as relock authority.

---

## 1. What I verified and what held (no finding)

I checked these because the amendment leans on them; all were **correct**:

| Claim | Location | Verified against | Result |
| --- | --- | --- | --- |
| OQ3 matrix `1.0.0` SHA-256 `5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7` | line 227 | `sha256sum docs/contract/authorization-matrix.md`; `docs/contract/oq3-authorization-evidence.yaml:8` | **exact match** |
| C12 catalog `1.0.0` SHA-256 `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a` | line 312 | `sha256sum docs/contract/provider-compatibility-catalog.md`; `docs/exit-criteria/c0-c13-governance-evidence.yaml:210`; `docs/contract/oq4-provider-compatibility-evidence.yaml:8` | **exact match** |
| Proposal digest `5d12ae4d…f2004104` (cited from prd.md, relied on here) | prd.md:95 | `sha256sum _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-15.md` | **exact match** |
| Sponsor approval "Jerome, 2026-09-15 16:08:21+02:00, decisions A1–A8" | line 202 | proposal front matter `approved_at: 2026-09-15T16:08:21+02:00`; §8 register rows A1, A2, A2b, A3, A4, A5, A6, A6b, A7, A7b, A8 | **correct**, incl. the A2b/A6b/A7b sub-decisions cited later |
| C7 `1.0.0` timing: 30 s renewal / 15 s revalidation / 60 s revocation SLO / **inclusive** 60 s expired→stale | line 413 + C7 row | `docs/exit-criteria/c7-lock-authorization-timing.md:9-12, 25-28` (`now >= expiresAt + 60 seconds`) | **correct, including the inclusivity** |
| "49 protected Contract Spine operations" | line 624 (S-7) | `grep -c "^      operationId:" src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` = **49**; `docs/contract/authorization-matrix.md:15, 102`; gap `G1` ("403 on 49 of 49") confirms all 49 are protected | **correct** |
| S-8's six operation ids exist verbatim | line 625 | spine: `ListFolderAclEntries` L412, `GetEffectivePermissions` L569, `ValidateProviderReadiness` L790, `GetTaskStatus` L3874, `GetReadinessDiagnostics` L4640, `GetProjectionFreshness` L5200 | **all six exist** |
| S-8's characterisation of *current* scoping | line 625 | `GetReadinessDiagnostics`/`GetProjectionFreshness` sit at `/api/v1/ops-console/…` with **no** `{folderId}` — genuinely "tenant-only exceptions"; `ListFolderAclEntries`/`GetEffectivePermissions` are `/folders/{folderId}/…`; `GetTaskStatus` is `/tasks/{taskId}/status`; `ValidateProviderReadiness` is `/provider-readiness/validations` | **accurate** |
| S-7 removed codes exist today | line 624 | `CanonicalErrorCategory` enum in the spine contains `not_found`, `cross_tenant_access_denied`, `audit_access_denied`, `tenant_access_denied`, `resource_unavailable`; matrix gaps `G2`/`G1` quantify them | **all five exist** |
| S-7 kept envelope shape | line 624 | matrix L145 `safe-denial-404` → `404` / category `tenant_access_denied` / code `resource_unavailable` / retryable `false` / `no_action` / visibility `redacted`; L148 "Post-authentication 403 is retired from the canonical design" | **correct** |
| `caller_completed` is an approved MVP release reason | line 624 | spine enum L8780; `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:666`; `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3290` | **correct** |
| `previous-spine.yaml` exists | line 627 | `tests/fixtures/previous-spine.yaml` (168 lines), consumed by `tests/tools/parity-oracle-generator/Program.cs` and `tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:91` | **exists** (but see **F2** for what it actually covers) |
| Story 12.6 title | line 252 | `_bmad-output/planning-artifacts/epics.md:2699` — "Story 12.6: Implement durable all-mutations idempotency and expired-key precedence"; `sprint-status.yaml:238` `12-6-…: in-progress` | **exact title match** |
| 12.6 "A-9 EventStore-owned admission actor and D-7 retention" | line 252 | A-9 row (line 643) does name the EventStore-owned admission actor; D-7 (line 608) is the replay/consumed-key retention decision; `docs/exit-criteria/oq8-idempotency-design.md` exists | **correct** |
| NFR74–NFR84, OQ12/OQ13, 84-row relock | lines 261-263 | `epics.md` +NFR74…NFR84; `docs/exit-criteria/nfr-traceability.md` — 84 unique NFR ids, rows 120-130, categories at 149-150; `prd.md:1087-1088` OQ12/OQ13 wording matches the amendment verbatim | **correct** |
| "at most five read-only checks within 15 minutes" (PD11 rule 5) | line 411 | pre-existing 2026-07-15 text, asserted by `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs:78-79` ("five read-only checks", "15-minute window") | **grounded, not invented** |
| Referenced source paths | C9/C10/C6 governance rows | `src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs`, `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs`, `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` all exist | **correct** |
| A-6 Octokit `14.0.0` (adjacent version claim) | line 638 | `references/Hexalith.Builds/Props/Directory.Packages.props:264` `<PackageVersion Include="Octokit" Version="14.0.0" />`, consumed by `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs` | **pin matches the doc** |
| S-7/S-8 fidelity to the approved proposal | lines 624-625 | proposal §7.2 items 1-9 | **faithful transcription, no drift** |
| PD11 rules 1-6 + C3 cleanup paragraph fidelity | lines 407-415 | proposal §7.3 bullets | **faithful transcription** |

The digest/version discipline in this amendment is genuinely good: three independent SHA-256 values were reproduced byte-exact, and the C7 boundary inclusivity was quoted correctly rather than paraphrased. The findings below are all in a different class — claims about *what the repository and its CI currently do*.

---

## 2. Findings

### F1 — HIGH — `planning-story-manifest.yaml` is declared "regenerated 2026-09-15" with an `execution_rank` field it does not contain

**Claim (line 206):**
> `| **Lifecycle & dependency control** | `planning-story-manifest.yaml` (regenerated 2026-09-15) | The canonical `story_lifecycle_status`, execution waves, prerequisite edges, and the decision/evidence split on open questions |`

**Claim (line 210):**
> "`execution_rank` in the manifest is the sole scheduling authority… **Manifest validation rejects a cycle and rejects any dependency on an equal or later rank.**"

**Verified against:**
- `_bmad-output/planning-artifacts/planning-story-manifest.yaml:3` — `generated_on: '2026-08-04'`. Its `provenance:` list stops at `sprint-change-proposal-2026-08-04.md`; the 2026-09-15 proposal is absent.
- `git status --porcelain` — the manifest is **not** modified in this working tree; `git log -1` on it points at `0e3f2a9`.
- `grep -rn "execution_rank"` across the repo returns hits **only** in `sprint-change-proposal-2026-09-15.md` (lines 234, 301, 315), `prd.md:159`, `architecture.md:210, 253`, and the `.memlog.md`. **Zero hits in the manifest.**
- Proposal **§5.5** lists the manifest work as a *required post-approval action*, not an accomplished one: "add `execution_waves`, `execution_rank`, and explicit prerequisite edges from Section 7.1; fail validation on a cycle or on a dependency from a story to an equal or later execution rank". Proposal §5.2 likewise says "the **regenerated** manifest" as a forward reference.
- No manifest-validation tool exists. The only non-`references/` file that mentions the manifest path at all is `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs`; there is no cycle check and no rank check anywhere.

**Why it matters:** this is the one row of the authority overlay that points at a *machine-readable* artifact, and the overlay's whole purpose is to say which file wins on lifecycle and scheduling. As written, the architecture delegates scheduling authority to a field that does not exist, in a file that still carries an August snapshot, and asserts a validation behaviour that is not implemented. Anyone resolving a sequencing dispute by opening the manifest will find the superseded model.

**Correction:** change line 206 to `` `planning-story-manifest.yaml` (regeneration pending per proposal §5.5; the committed manifest is the superseded 2026-08-04 snapshot) `` and rewrite the second sentence of line 210 as a requirement rather than a fact — e.g. "Manifest validation **must** reject a cycle and **must** reject any dependency on an equal or later rank; neither the `execution_rank` field nor that validation exists yet (proposal §5.5)." Alternatively, land the manifest regeneration in the same change set so the claim becomes true.

---

### F2 — HIGH — The C13 "symmetric-drift gate" will **not** trip on the PD10 error-code removals, and `previous-spine.yaml` has no mechanism for the deprecation entries the doc prescribes

**Claim (line 627, end of §"Authorization Spine Correction (PD10 — 2026-09-15)"):**
> "Because the correction *removes* caller-visible error codes, it trips the C13 symmetric-drift gate by design: the removals belong in `previous-spine.yaml` as deprecation-window entries in the same commit."

**Verified against:**
- `tests/fixtures/previous-spine.yaml:7-9` declares its own scope, and it excludes exactly what PD10 changes:
  ```yaml
  known_omissions:
    - No request/response schema fingerprints
    - No status-code surface
  ```
  Each `operations:` entry carries only `operation_id`, `method`, `path`.
- `tests/tools/parity-oracle-generator/Program.cs` — the drift identity is `string identity = method + " " + path + " " + operationId;` and the gate throws only on *removed*, *renamed*, or *moved* operationIds, plus unlisted *additions* via `approved_additions:`. Response codes and error categories are never read.
- `HasApprovedDeprecation(YamlMappingNode operation, …)` takes a `deprecation:` mapping **on an operation entry** (requiring `approved`, `rationale`, `approval_reference`, `effective_date`, `approval_source`). There is no schema slot for an error-code or status-code deprecation, and nothing to attach one to — PD10 removes no operationId.

Net effect: PD10 removes `not_found`/`cross_tenant_access_denied`/`audit_access_denied` responses and the post-auth 403 from 49 operations while the operation inventory stays identical, so the symmetric-drift gate stays green and silent through the most dangerous part of a breaking security change.

**The gate that *does* observe this** is `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.AuthorizationMatrixGapCountsTrackTheContractSpineDeclarations` (lines 495-521), which counts `403`/`404`/`503` status declarations and `not_found`/`cross_tenant_access_denied`/`audit_access_denied` categories across the spine and pins the results into the `G1`/`G2`/`G3` gap prose. It will go red the moment the removals land, which is the intended tripwire.

Note also that the proposal does **not** make this claim: §7.2 item 10 says only "Regenerate OpenAPI, generated client, CLI/MCP parity, **previous-spine comparison**, C13 inventory, docs, and tests." The "trips the symmetric-drift gate by design" mechanism and the "deprecation-window entries" instruction are additions made in this document and were not checked against the generator.

**Correction:** replace the final sentence of line 627 with something accurate, e.g.: "Because the correction changes response surfaces rather than the operation inventory, the `previous-spine.yaml` symmetric-drift gate — which compares only `(method, path, operationId)` and explicitly omits status-code and schema fingerprints — will **not** detect it. The tripwire is `AuthorizationMatrixContractTests.AuthorizationMatrixGapCountsTrackTheContractSpineDeclarations`, whose `G1`/`G2`/`G3` counts must be updated in the same commit as the matrix digest; `previous-spine.yaml` is re-captured only if an operationId is added, removed, renamed, or moved."

---

### F3 — HIGH — PD11 rule 7's CI claim about `LockLeaseBecameStale` is wrong in mechanism, and the same-commit obligation it states is already broken by this change

**Claim (line 413, PD11 rule 7):**
> "**`LockLeaseBecameStale` is a new event** added to the architecture event vocabulary by this correction… Rule 4 depends on it, so the C6 aggregate gate must cover it in the same commit that lands this matrix — an event in the vocabulary with no asserted outcome fails CI by design."

**Verified against `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs`:**

(a) **The stated mechanism does not exist.** `EveryUnlistedStateEventPairShouldRejectWithoutChangingState` (line 81) enumerates `Enum.GetValues<FolderWorkspaceLifecycleEvent>()` and asserts that every pair *not* in `PositiveTransitionCases` **rejects** with `FolderResultCode.StateTransitionInvalid`. Adding `LockLeaseBecameStale` to the enum without adding the transition therefore makes that test **pass**, and it passes by positively asserting that `(Dirty, LockLeaseBecameStale)` rejects — the exact opposite of PD11 rule 4 and of the new matrix row at line 390. There is no "uncovered vocabulary addition fails CI" rule; a missing outcome is silently reinterpreted as an explicit rejection.

The test that *would* fail is `StateCatalogAndEventVocabularyShouldMatchC6MappingDocument` (line 139), which hard-pins a literal 23-name list against `FolderStateTransitions.EventVocabulary`. That is a name-drift pin, not an outcome-coverage gate — and if the list is regenerated to include the new name without the transition being added, CI goes green with the model inverted.

(b) **The same-commit rule is already violated.** This change set touches only planning artifacts plus the NFR traceability gate. Untouched: `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` (no `LockLeaseBecameStale` in `EventVocabulary`), the `FolderWorkspaceLifecycleEvent` enum, `FolderStateTransitionsTests.cs`, and `docs/exit-criteria/c6-transition-matrix-mapping.md` — whose own header rule reads "any vocabulary change must update architecture and aggregate tests in the same change", and whose line 36 still lists the old 23-event vocabulary.

(c) For completeness: `LockLeaseBecameStale` is **not** in the approved proposal. §7.3 says only "a clean dirty workspace whose lock becomes stale returns to `ready` and unlocked". Naming the event is legitimate mechanism authority; the CI claim attached to it is what went unverified.

**Correction:** (i) restate rule 7's last clause accurately — the coverage obligation is enforced by the `StateCatalogAndEventVocabularyShouldMatchC6MappingDocument` name pin plus the `PositiveTransitionCases` table, and `EveryUnlistedStateEventPairShouldRejectWithoutChangingState` will happily bless an uncovered event as "rejects"; (ii) either land the enum + `EventVocabulary` + `PositiveTransitionCases` + `c6-transition-matrix-mapping.md:36` edits in this change set, or restate the obligation as owed to the A7/PD11 implementation story and name the four files; (iii) consider strengthening the gate so a vocabulary member with no row in either the positive table or an explicit reserved-rejection list fails — which is what the doc believes already happens.

---

### F4 — HIGH — Three new matrix rows are not functions of `(state, event)`, while the doc still declares that they are — and the documented implementation shape is wrong

**Claims:**
- line 342: "Every `(currentState, event)` pair has a defined outcome."
- line 419: "`…FolderStateTransitions.cs` implements this matrix as a switch expression over `(currentState, eventType)` returning `DomainResult`."

**Verified against `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`:**
- The real signature is `Transition(FolderWorkspaceLifecycleState? currentState, FolderWorkspaceLifecycleEvent attemptedEvent, FolderWorkspaceDirtyResolution? dirtyResolution = null)`, switching over a **3-tuple** and returning **`FolderWorkspaceTransitionResult`**.
- `DomainResult` does not exist in this repository at all — the only definition is `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainResult.cs`, and `FolderStateTransitions.cs` does not use it. (Pre-existing text, but PD11 now makes it load-bearing.)

The new matrix introduces **three key collisions** that a `(state, event)` switch cannot express:

| Line | Row | Colliding row |
| --- | --- | --- |
| 383 / 384 | `changes_staged` + `CommitFailed` → `failed` (non-retryable) | → `dirty` (retryable, no confirmed remote effect) |
| 395 / 396 | `inaccessible` + `ProviderReadinessValidated` → `dirty` (staged content in C3 window) | → `ready` (clean / window elapsed) |
| 389 | `dirty` + `WorkspaceLocked` → `changes_staged` | qualified by "**by the originating task**" — requester identity is not in the key |

The codebase already solved exactly this problem once: `FolderWorkspaceDirtyResolution?` is the third discriminator that disambiguates `unknown_provider_outcome` + `ReconciliationCompletedDirty` into `committed` vs `failed`, and `UnknownOutcomeDirtyReconciliationShouldRejectWithoutExplicitResolution` pins that an unresolved call rejects. The amendment adds three more such cases without adding a discriminator column, so the matrix as written is ambiguous and the C6 gate cannot assert it.

**Correction:** add a fourth "discriminator" column to the valid-transitions table naming the resolution input for each colliding row (e.g. `commitFailureClass: retryable|non-retryable`, `stagedContentPresent: true|false`, `lockRequester: originating-task|other`), following the `FolderWorkspaceDirtyResolution` precedent; and fix line 419 to the real shape — "a switch expression over `(currentState, attemptedEvent, resolution)` returning `FolderWorkspaceTransitionResult`". Line 342's "every `(currentState, event)` pair" should read "every `(currentState, event, resolution)` triple".

---

### F5 — MEDIUM — Three transition rows describe MVP code behaviour in the present tense that the code contradicts today, repeating a 2026-07-15 mis-statement

**Claims (lines 392, 394, 403):**
> "`dirty` → `failed` | `OperatorDiscardRequested` — **reserved post-MVP; fails closed in MVP code** | MVP: rejected with `state_transition_invalid`; state unchanged" (and the same phrasing for `OperatorRetrySucceeded` and `OperatorMarkedFailed`)

plus PD11 rule 6 (line 412): "MVP code must not implement them as no-ops that appear to succeed."

**Verified against the code and tests:** all three are implemented today as *accepted* transitions —
`(Dirty, OperatorDiscardRequested, null) => Failed`, `(Failed, OperatorRetrySucceeded, null) => Ready`, `(ReconciliationRequired, OperatorMarkedFailed, null) => Failed` in `FolderStateTransitions.cs` — and `FolderStateTransitionsTests.PositiveTransitionCases` lines **39**, **41** and **49** assert exactly those successes. The predecessor text made the same unchecked claim ("post-MVP — currently rejected per concern 'no silent repair'") while the code already accepted the transition, so this is a repeated assertion-from-memory rather than a fresh error.

**Correction:** phrase the Side Effect cells as an obligation with the drift named, e.g. "**MUST** reject with `state_transition_invalid`, state unchanged (A7/PD11 correction owed; `FolderStateTransitions.cs` currently accepts this transition and `FolderStateTransitionsTests.cs:39/41/49` pin the acceptance)". PD11 already grants "a divergence is a defect in the code" — but stating the divergence explicitly is what stops an implementer from reading the table as a description of today.

---

### F6 — MEDIUM — The new conditional `dirty` disposition is not expressible in the documented disposition contract, and the C6 mapping artifact named as co-authoritative was not updated

**Claim (line 352):**
> "`dirty` | `degraded-but-serving` while the originating task can still resume or the workspace is clean; `awaiting-human` once staged changes are orphaned…"

**Claim (line 407, PD11 normative preamble):** "This matrix, `docs/exit-criteria/c6-transition-matrix-mapping.md`, `…/FolderStateTransitions.cs`, and the lifecycle tests express **one** model."

**Verified against:**
- `FolderStateTransitions.GetOperatorDisposition(FolderWorkspaceLifecycleState state, bool hasProjectionLagEvidence = false)` returns a single value and hard-codes `Dirty => AwaitingHuman`. There is no input that could carry "the originating task can still resume" or "the workspace is clean", so the new cell cannot be implemented without a signature change. (The `hasProjectionLagEvidence` parameter, added for the `ready` cell, is the existing precedent for how to do this.)
- `FolderStateTransitionsTests.OperatorDispositionShouldMatchC6StateCatalog` pins `Dirty → AwaitingHuman` **and** `UnknownProviderOutcome → AwaitingHuman` via `InlineData`; `Transition` additionally asserts `result.OperatorDisposition` on every positive and negative case, so the conditional value has to be threaded through there too.
- `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs:33` maps `LifecycleState.Dirty => OperatorDispositionLabel.Awaiting_human`.
- `docs/exit-criteria/c6-transition-matrix-mapping.md` — **not modified** by this change. Its state-catalog table still reads `dirty | awaiting-human` and `unknown_provider_outcome | awaiting-human`, and its event vocabulary (line 36) still lists the old 23 events. It is named in the amended C6 governance row (line 361) as one of three artifacts that must assert the PD11 rules identically.

Related, pre-existing: PD11 rule 5's "`unknown_provider_outcome` is `auto-recovering`" restates a 2026-07-15 change that never reached the code (`UnknownProviderOutcome => AwaitingHuman`), the test `InlineData`, or the C6 mapping doc. The amendment re-asserts it as settled without flagging that three of the four artifacts it declares co-authoritative disagree.

**Correction:** (i) extend `GetOperatorDisposition` in the doc's Implementation-enforcement bullet to name the additional inputs (resumability / staged-content presence), following the `hasProjectionLagEvidence` precedent; (ii) update `docs/exit-criteria/c6-transition-matrix-mapping.md` in the same change set, or add it to the explicitly-owed list; (iii) add one sentence recording that `dirty` and `unknown_provider_outcome` dispositions are known code/test/mapping divergences owed under A7/PD11, so the "one model" statement is not read as a description of the present.

---

### F7 — MEDIUM — `visibility` is specified in two incompatible wire positions, and the OQ2 closed problem schemas are missing from the regeneration list

**Claims:**
- line 624 (S-7): "…returns one non-disclosing HTTP 503 envelope carrying **`details.visibility: redacted`**… **`visibility` is a required field on every error.**"
- line 640 (A-8): "**`visibility` is a required field on every error (PD10, 2026-09-15);**"

**Verified against the spine (`ProblemDetails`, line 7632):** `visibility` is **neither** a declared property **nor** in `required:`. Its `required` list is `type, title, status, category, code, message, correlationId, retryable, clientAction, details`. All 96 `visibility` occurrences in the spine sit inside free-form `details` / `redaction` sub-objects. So `details.visibility` is expressible today under `details`'s `additionalProperties: {string|number|boolean}`, but "a required field on every error" is a different, unstated schema change.

The two readings are not interchangeable, and one of them has an unlisted blast radius: `ExactFileProblem` (immediately below `ProblemDetails`) is `additionalProperties: false` with its own closed `required` list, and is projected by `src/Hexalith.Folders.Client/Serialization/Oq2ProblemProjection.cs`. A new top-level required `visibility` is a breaking change to the OQ2 exact-file problem contract, which the §"Authorization Spine Correction" regeneration list (line 627) does not mention. The proposal (§7.2 item 9) is equally ambiguous — "Every error includes the required visibility field" — so this was inherited, not introduced, but the architecture is the mechanism authority and is the right place to resolve it.

**Correction:** state the wire position explicitly in both S-7 and A-8 — either "`visibility` is a required top-level `ProblemDetails` field" or "`details.visibility` is required on every error" — and add `ExactFileProblem` / the OQ2 problem projections to the PD10 regeneration list at line 627 if the top-level reading is chosen.

---

### F8 — LOW — A stray blank line splits the Authentication & Security decision table, orphaning S-7 and S-8

**Location:** `architecture.md:623` is empty, between the `| S-6 | … |` row (622) and `| S-7 | … |` (624). Confirmed with `sed -n '614,626p' … | cat -A`.

**Effect:** Markdown ends the S-1…S-6 table at line 622. S-7 and S-8 then begin a new table with no header row and no delimiter row, so most renderers emit them as a headerless fragment or as literal pipe-delimited text. The two decisions that carry the PD10 security correction are the ones that render wrong.

**Correction:** delete line 623.

---

### F9 — LOW — Acceptance obligations created by the amendment did not reach `epics.md`, the artifact the overlay names as acceptance authority

**Claim (line 204):** "| **Acceptance** | `epics.md` | Story acceptance criteria and the evidence each story must produce |"

The amendment creates four concrete new test obligations in the governance rows: the C6 three-way PD11 assertion plus "the three reserved operator operations are asserted to reject" (line 361), the C9 "event-write token-substitution tests asserting no durable cleartext in any event, projection, audit record, log, trace, or export" (line 365), and the C3 "cleanup-trigger test asserting no deletion before terminal task closure with no active task" (line 357). The `epics.md` change in this working tree adds **only** the NFR74–NFR84 bullets; none of those acceptance obligations landed anywhere.

**Correction:** either route the four obligations into the owning stories in `epics.md` in lockstep, or add one line to the overlay recording them as owed acceptance edits under A5/A7/A7b so the gap is visible rather than implied.

---

## 3. Reviewer notes

- **Nothing in this amendment appears to have been asserted from training data about external technology.** Every external-technology touchpoint I checked (Octokit 14.0.0, the `Microsoft.AspNetCore.Authentication.JwtBearer` S-2 parameters, RFC 9457 Problem Details, oasdiff) is either unchanged by this diff or matches the repository's own pins. The failure mode here is the brownfield one: claims about *this repository's* current state and CI behaviour were written from the approved proposal's intent rather than read off the files.
- **F1, F2 and F3 share one root cause:** each states a *pending* obligation in the perfect tense or as an existing safety net. The proposal is careful about this (§5.5 and §7.2 item 10 are both written as instructions); the architecture flattened them into assertions. A single editorial pass converting "is / trips / rejects" to "must / is owed / does not yet" would close all three.
- **F4 is the one substantive design gap.** The PD11 model is richer than the state machine it is supposed to specify, and the missing discriminator column is the difference between a matrix the C6 gate can assert and one it cannot. The repository already contains the pattern to copy.
