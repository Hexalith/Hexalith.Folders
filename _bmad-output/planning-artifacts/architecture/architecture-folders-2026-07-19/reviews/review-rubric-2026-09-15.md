# Rubric Walker Review — architecture.md amendment 2026-09-15

- **Reviewer role:** Rubric Walker, BMad architecture Reviewer Gate
- **Subject:** `_bmad-output/planning-artifacts/architecture.md` (1833 lines), uncommitted amendment `+85 / -19`
- **Baseline for the diff:** `git diff -- _bmad-output/planning-artifacts/architecture.md` at HEAD `1621358`
- **Review date:** 2026-09-15
- **Mode:** read-only; no file outside this report was modified
- **Verdict:** **FAIL**

## Checklist verdict at a glance

| # | Checklist item | Verdict |
| --- | --- | --- |
| 1 | Fixes the real divergence points for epics/stories, misses none | **FAIL** |
| 2 | Every rule is enforceable, with mechanism + owner + gate | **FAIL** |
| 3 | Nothing deferred could let two units diverge | **FAIL** |
| 4 | Named technology verified-current and fits | **PASS** (with note — see §4) |
| 5 | Ratifies the brownfield codebase rather than contradicting it | **FAIL** (worst item) |
| 6 | Every dimension the altitude owns is decided, deferred, or an open question | **FAIL** |
| 7 | Internal consistency after the edit | **FAIL** |
| 8 | Readability / structure / findability at this size | **FAIL** |

## What the amendment actually adds

1. `§"Release Authority Overlay — 2026-09-15"` (lines 197–232): four-artifact authority split, an `execution_rank` wave table (ranks 0–50), a superseded-approval-digest table (OQ3 matrix, C3, C6, C9).
2. Story `12.6` added to Epic 12 (line 251); dependency-spine prose re-pointed at execution waves (line 253).
3. Epic 13 `NFR74`–`NFR84` ownership + `OQ12`/`OQ13` + the "admission is not implementation evidence" rule (lines 259–263).
4. C3 / C6 / C9 exit-criteria rows and their Exit Criteria Operations Plan rows flipped to approval-pending with added measurement methods (lines 300–333).
5. Workspace State Transition Matrix: `dirty` disposition rewritten, five new/split transitions, three operator transitions re-labelled "reserved post-MVP; fails closed", seven normative PD11 rules, a new `LockLeaseBecameStale` event, and a C3 cleanup-authority paragraph (lines 354–415).
6. Security decisions: S-4 evaluation order, S-6 rewritten to write-time correlation-token substitution, **new S-7** (denial envelopes) and **new S-8** (derived authorization scope), plus the "Authorization Spine Correction (PD10)" paragraph (lines 620–627).
7. A-8 gains "`visibility` is a required field on every error" (line 640).

Three of these — the state machine, the denial contract, the confidential-value mechanism — are assertions about running software. All three were checked against the code.

---

## Findings

Severity legend: **critical** = blocks gate pass; **high** = must fix before the document is used to drive stories; **medium** = fix in this revision; **low** = editorial.

---

### CRITICAL-1 — Three new normative sections describe systems that do not exist, with none of the document's own honesty labels and no owning story

**Checklist items:** 5 (primary), 1, 2, 3
**Locations:** lines 354–415 (PD11 matrix + rules), 620–627 (S-6, S-7, S-8, PD10 paragraph), 640 (A-8)

The document already has a well-developed honesty convention and uses it elsewhere:

- line 238 — "Directory-tree entries such as `WorkingCopyManager.cs` and `IdempotencyRecordStore.cs` … are **target structure / aspirational** until the owning Epic 12 stories land them."
- line 240 — the **planning-consistency invariant** ("safe-empty, fail-closed, seed-only, unavailable, and NoOp paths … must never be counted as completed product capability").
- line 350 — `preparing` is annotated "aspirational until Epic 12 Story 12.3 lands the durable content path".
- lines 181, 190, 194 — "safe-empty is mandatory fail-safe behavior but is **not** completed product capability", with the owning story named each time.

**None of the three new mechanisms carries that label, and none names an owning story or execution rank.** They are written in the present indicative, as though describing the system. Ground truth:

#### (a) Workspace state machine — `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`

The shipped switch has 34 rows. Against the amended matrix:

| Amended matrix row (lines 384–396) | Code today |
| --- | --- |
| `changes_staged` → `dirty` on retryable `CommitFailed` | **Does not exist.** `(ChangesStaged, CommitFailed)` → `Failed`, unconditionally. There is no retryability input to `Transition(...)` at all; `WorkspaceCommitFailed.FailureCategory` is stored in `FolderState` and never read by the transition function. |
| `changes_staged` → `inaccessible` on `AuthRevocationDetected` / `TenantRevoked` / `RepositoryDeletedAtProvider` | **Does not exist.** All three reject from `ChangesStaged`. |
| `dirty` → `changes_staged` on `WorkspaceLocked` by the originating task | **Does not exist.** `(Dirty, WorkspaceLocked)` rejects. |
| `dirty` → `ready` on `LockLeaseBecameStale` (clean) | **Does not exist** — and neither does the event. |
| `inaccessible` → `dirty` (staged content in the C3 window) | **Does not exist.** `Inaccessible` has exactly one outgoing row: `ProviderReadinessValidated` → `Ready`, unguarded. |
| `OperatorDiscardRequested` / `OperatorRetrySucceeded` / `OperatorMarkedFailed` "reserved post-MVP; fails closed in MVP code" (PD11 rule 6) | **Inverted.** All three are implemented as *accepted, state-changing* transitions (`Dirty→Failed`, `Failed→Ready`, `ReconciliationRequired→Failed`) and are **published caller-visible values** in `hexalith.folders.v1.yaml` (`WorkspaceTransitionAttempt.eventName`, lines 8913/8914/8919) and in the generated SDK's ordinal-valued enum (`HexalithFoldersClient.g.cs:14654-14673`). |
| `dirty` disposition = conditional `degraded-but-serving` / `awaiting-human` | **Flat `AwaitingHuman`** in `FolderStateTransitions.cs:157`, `DispositionLabelMapper.cs:33`, and pinned on the wire as `("dirty","awaiting_human")` in `OpsConsoleDiagnosticsEndpointTests.cs:492`. |
| PD11 rule 5 — `unknown_provider_outcome` is `auto-recovering` | Code returns `AwaitingHuman` (`FolderStateTransitions.cs:161`). (Pre-existing drift from the 2026-07-15 text at line 358; the amendment restates it without noting the divergence.) |
| `LockLeaseBecameStale` (PD11 rule 7) | **Exists nowhere** in `src/`, `tests/`, the OpenAPI spine, the SDK, or `docs/`. Its only occurrences are architecture.md:390 and :413. There is also no `stale` member in any *lock-lease* vocabulary in `src/`. |

Worse than silence: the existing CI gate **actively asserts the opposite**. `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs` → `EveryUnlistedStateEventPairShouldRejectWithoutChangingState` iterates 11 states × 23 events and requires `(ChangesStaged, AuthRevocationDetected)`, `(ChangesStaged, TenantRevoked)`, `(ChangesStaged, RepositoryDeletedAtProvider)` and `(Dirty, WorkspaceLocked)` to reject. `StateCatalogAndEventVocabularyShouldMatchC6MappingDocument` pins the 23-event literal. `DispositionLabelParityTests` joins `FolderStateTransitions` to `DispositionLabelMapper`. Landing PD11 is a coordinated multi-project change; the document treats it as a statement of fact.

#### (b) Denial contract — S-7 / A-8

| S-7 / A-8 claim | Code today |
| --- | --- |
| "the post-authentication 403 … **removed** from protected-operation responses" | 403 is the `_ =>` **default** in `FolderCanonicalErrorMapper.StatusFor`, the default in `FolderAuthorizationDenialMapper.StatusAndCategory` (L48–73), declared on **49 of 50 operations** in the spine, and asserted by 6 `[InlineData]` rows in `tests/Hexalith.Folders.Server.Tests/SafeAuthorizationDenialMappingTests.cs`. |
| "`not_found`, `cross_tenant_access_denied`, `audit_access_denied` … **removed**" | All three are live `CanonicalErrorCategory` members (idx 36, 6, 8) in the spine, the generated SDK, the CLI mapper, the MCP mapper, `tests/fixtures/parity-contract.yaml`, and `docs/operations/canonical-error-catalog.md`. `audit_access_denied` (403) and `not_found` (404) are **actively emitted** by `AuditEndpoints.cs:459-474`. |
| "`visibility` is a **required field on every error**" | False at the contract level: the generic `ProblemDetails` schema (`hexalith.folders.v1.yaml:7632-7689`) does not declare `visibility` at all and does not require it; `details` is an open `additionalProperties` map. `visibility` is required only on the 8 OQ2 `ExactFileProblem` descendants. At the implementation level it exists as a **constant** `details.visibility = "metadata_only"` (`FolderProblemDetailsFactory.cs:207-214`, `FolderAuthorizationDenialMapper.cs:36`) — never `redacted`, never top-level. |
| "Authority that is absent, stale, **malformed**, or unavailable returns one non-disclosing 503" | Two of four. Stale/unavailable → 503 `read_model_unavailable` pre-lookup (correct). **Malformed → 403 `authorization_denied`** (`FolderAuthorizationDenialMapper.cs:69-70`, asserted at `SafeAuthorizationDenialMappingTests.cs:19`). There is no unified authority-unavailable envelope; the code emits `read_model_unavailable` / `policy_evidence_unavailable`, the OQ3 matrix requires `projection_unavailable` + `visibility: redacted`. |
| "one HTTP 404 `tenant_access_denied` / `resource_unavailable` shape" | `resource_unavailable` is **not** a `CanonicalErrorCategory`; it is a `code` used only by the OQ2 file family, with zero occurrences in the parity oracle. |

The one thing S-7 *does* ratify: authorization genuinely runs before resource lookup today (`LayeredFolderAuthorizationService`, `AuthorizationOrder.cs`, `FoldersDomainServiceRequestHandler.cs:63-88`). That should be stated as ratification so implementers do not rebuild it.

Note also that `docs/contract/authorization-matrix.md` **already** records this divergence honestly as gaps `G1`–`G3` with counts, and its own conformance test (`AuthorizationMatrixContractTests.cs:196-199`) guards *the document*, not the server. The architecture, which sits above it, is less honest than the artifact it cites.

#### (c) Confidential-value mechanism — S-6

| S-6 claim | Code today |
| --- | --- |
| a per-tenant `confidential` **tier** | No such tier exists. The spine enum `SensitiveMetadataTier` (`hexalith.folders.v1.yaml:8155-8162`) is `public_metadata | tenant_sensitive | credential_sensitive | secret`. `grep -rni confidential src/` returns **zero** hits. |
| correlation token substituted **at event-write time** | No tokenizer exists anywhere. `FolderAuditSanitizer.cs` runs post-hoc on the **telemetry** track only (`FolderDomainProcessor.cs:1174`, `FolderAuditEndpointFilter.cs:54`); the domain write path (`ProcessCoreAsync → ToDomainResult`) applies nothing. The sanitizer's only mechanism is a drop filter (`IsSensitiveDiagnosticValue`) that nulls or substitutes `"tenant_unknown"`. |
| "Cleartext confidential values are never made durable" | `FolderCreated` persists raw `DisplayName`, `Description`, `PathLabel`, `Tags`; `PathMetadata` persists raw `NormalizedPath` and `DisplayName` into `MutateWorkspaceFile`. |
| "stable, non-reversible reference that correlates the same value across records" | Nothing of the kind. The only `correlation_`-prefixed value (`ProviderReadinessValidationService.cs:737`) is a fresh random GUID — it *destroys* linkage. |
| surfaces render a **withheld** state distinct from redacted/unavailable/absent | `withheld` is not a state. `FieldDisclosure` = `Visible | Redacted | Unknown | Missing`; the wire enums `RedactionVisibility` and `FolderAuditRedactionState` have exactly two members (`metadata_only`, `redacted`). `ConsoleStatusText.cs:45` uses "withheld" as a *synonym* for redacted. |

The repo's own traceability already knows: `docs/exit-criteria/nfr-traceability.md:54` marks NFR8 reference-pending with *"the C9 tenant-confidential projection write-time correlation-token override lacks production implementation evidence."* The architecture asserts the same mechanism as present tense.

**Fix (all three):** for each of PD11, PD10 (S-7/S-8/A-8) and PD8 (S-6), add a one-paragraph *Current-code divergence* block in the document's existing voice — e.g. "**Status (2026-09-15): target state.** `FolderStateTransitions.cs` implements N of the rows below and currently accepts the three operator events; the divergence is a defect owned by Story X at rank Y." Name the owning story and rank for each, or record explicitly that the story does not yet exist. Where the code is already correct (pre-lookup authorization order), say so, so implementers do not rewrite it.

---

### CRITICAL-2 — The migration/rollout dimension of a breaking wire-contract change is silent, contradicts A-11, and the one mechanism it does name does not do what it claims

**Checklist items:** 6 (primary), 2, 7
**Locations:** line 627 (PD10 paragraph), line 643 (A-11), lines 624–625 (S-7/S-8)

The amendment introduces a breaking change to the public `v1` wire contract across at least six axes:

1. removal of three caller-visible `CanonicalErrorCategory` members (which are **ordinal-valued** in the generated SDK — every subsequent member renumbers);
2. retirement of post-authentication 403 on 49 of 50 operations;
3. `visibility` promoted to required on every error;
4. a new 503 authority-unavailable envelope with a new code/visibility pair;
5. S-8 adds required folder scope to `GetReadinessDiagnostics` / `GetProjectionFreshness` and a task parameter to `GetEffectivePermissions` — **request-shape** breaking changes the PD10 paragraph does not mention at all;
6. PD11 rule 6 turns three published `WorkspaceTransitionAttempt.eventName` enum values into rejections.

The document says **nothing** about: deprecation-window length; dual-serving or a compatibility shim; the order in which server, SDK, CLI, MCP, UI and third-party REST callers roll; what a deployed CLI pinned to exit code 73 does after `not_found` disappears; or what a strict generated client does when it meets an unknown required field.

It also **contradicts A-11** (line 643): *"breaking changes get a new major version"*. PD10 changes `v1` in place with no reconciliation of that rule.

And the one migration mechanism it names is factually wrong. Line 627: *"the removals belong in `previous-spine.yaml` as deprecation-window entries in the same commit"* and *"it trips the C13 symmetric-drift gate by design."* `tests/fixtures/previous-spine.yaml` tracks **operation inventory only** — 49 `operation_id:` entries plus `approved_additions` — and is consumed by `tests/tools/parity-oracle-generator/Program.cs:253-404`. Removing *error categories* will not trip it. What actually breaks is the parity-oracle generator's canonical-category coverage check and the ordinal-valued SDK enum. The prescribed remediation therefore records nothing and gates nothing.

**Fix:** add a `§"Contract Migration & Rollout (PD10)"` subsection under API & Communication Patterns that decides: (a) in-place `v1` amendment vs `v2` (and why A-11 is or is not being overridden); (b) deprecation window with a date or release count; (c) surface rollout order and the minimum client version; (d) the correct gate for error-vocabulary removal (parity-oracle category coverage + SDK enum golden-file), replacing the `previous-spine.yaml` claim; (e) whether the SDK enum becomes string-valued to survive future removals. Fold S-8's request-shape changes into the same breaking-change inventory.

---

### CRITICAL-3 — The scheduling authority the document delegates to does not exist in the form claimed, so the document's own prose is the only copy of the schedule

**Checklist items:** 2, 3, 7
**Locations:** lines 229–231 (authority table + "this document does not schedule work"), lines 233–242 (wave table), line 253

Line 229 names `planning-story-manifest.yaml` "(regenerated 2026-09-15)" as authority for `story_lifecycle_status`, `execution_rank`, execution waves and prerequisite edges. Line 231: *"This document does not assign story status and does not schedule work."* Line 233: *"`execution_rank` in the manifest is the sole scheduling authority."*

The manifest is at `_bmad-output/planning-artifacts/planning-story-manifest.yaml`, is **unmodified in the working tree**, carries `generated_on: '2026-08-04'`, uses the older field name `lifecycle_status`, and contains **zero** occurrences of `execution_rank`, `execution_waves`, or `story_lifecycle_status`. §5.5 of the approved proposal mandates the regeneration; it has not been applied.

Consequences, all of them divergence-enabling:

- Every reference in this document (and `prd.md:159`) to `execution_rank` in the manifest is a dangling pointer.
- The prose wave table at lines 235–242 is, in practice, the **only** machine-readable-ish statement of the schedule — directly contradicting line 231 two lines above it.
- Even once the manifest is regenerated, the document holds a second full copy of the wave/prerequisite graph with **no lockstep rule** saying which wins on drift or that they must move together. Compare the document's own careful lockstep rule for the NFR-traceability hard-pin at line 316.

**Fix:** either (a) regenerate the manifest first and reduce the prose table to a pointer plus the 5 wave *names* (no per-story prerequisite edges), or (b) if the table must stay, add an explicit lockstep sentence naming the manifest as the tiebreaker and requiring both to change in one commit. Remove or correct "(regenerated 2026-09-15)".

---

### HIGH-1 — S-6's confidentiality claim states an intention with no mechanism, and cannot hold for the data classes it names

**Checklist items:** 2 (primary), 4, 5
**Location:** line 622 (S-6), line 333 (C9 ops-plan row), line 1569 (concern #17 structure mapping)

S-6 requires a token that is simultaneously (i) stable, (ii) correlatable across records, (iii) non-reversible, and (iv) backed by **no key** ("no key exists to restore one"). For the *named* data classes — paths, branch names, repository names, commit messages — an unkeyed deterministic digest over low-entropy, highly guessable inputs is recoverable by dictionary attack in seconds. Property (iii) does not follow from (i)+(ii)+(iv); it is asserted.

Second, a globally stable token is a **cross-tenant correlation oracle**: the same branch name in two tenants yields the same token, which violates cross-cutting concern #1 (every key tenant-scoped) and #13 (cache-key tenant-prefix invariant). S-6 says nothing about tenant scoping.

Third, the decision names **no primitive at all** — no hash/HMAC choice, no salt or pepper, no key or token versioning, no rotation story, no length/encoding. Contrast A-9 in the same document, which pins *"versioned HMAC-SHA-256 opaque-key digest and collision-verification tag … partitioned by managed tenant"*. The idempotency decision gets a full cryptographic contract; the confidentiality decision gets prose.

Fourth, it has **no owner component**. The C9 ops-plan row (line 333) still names `Hexalith.Folders/Observability/FolderAuditSanitizer.cs` as the artifact, and the cross-cutting mapping (line 1569) still maps concern #17 there. That file runs on the *telemetry* path after the fact and is never invoked on the write path. The project structure tree (lines 1110–1502) has no entry for a write-time tokenizer.

Fifth, naming collision: "correlation token" collides with `correlationId` (A-10, concern #3, `X-Correlation-Id`), which the document elsewhere insists is *request-scoped and never an equivalence participant*. Two different things called "correlation X" in one contract will be conflated.

**Fix:** pin the primitive the way A-9 does — e.g. *"versioned HMAC-SHA-256 over the normalized value under a per-managed-tenant key held in the Dapr secret store; token = `conf_{version}_{hex}`; key rotation invalidates correlation across the rotation boundary and that is accepted"* — or, if unkeyed stability is genuinely required, state the residual dictionary-recovery risk and get it approved as such. Rename to avoid `correlation`. Give it a home in the structure tree on the write path and repoint the C9 artifact location. Note that a keyed design re-opens the "key management" alternative S-6 currently rejects — that trade needs to be made explicitly, not assumed away.

---

### HIGH-2 — The C6 totality gate cannot enforce the transitions PD11 adds, and PD11 leaves one (state, event) pair with no outcome

**Checklist items:** 2 (primary), 3, 7
**Locations:** line 343 (totality rule), lines 383–396 (guarded rows), line 420 (CI gate), line 1805 (handoff restatement)

The matrix's enforcement contract is keyed on `(state, event)`: line 343 — *"Pairs not listed are **rejected**"*; line 420 — *"for every state in the catalog and every event in the architecture event vocabulary, **at least one** test asserts the documented outcome"*; line 1805 repeats it in the handoff.

PD11 introduces four **guard-discriminated** pairs whose outcome is no longer a function of `(state, event)`:

| Pair | Guard | Outcomes |
| --- | --- | --- |
| `(changes_staged, CommitFailed)` | retryable + no confirmed remote effect | `dirty` else `failed` |
| `(inaccessible, ProviderReadinessValidated)` | staged content within the C3 window | `dirty` else `ready` |
| `(dirty, WorkspaceLocked)` | lock requested by the **originating** task | `changes_staged` else (undeclared) |
| `(dirty, LockLeaseBecameStale)` | workspace is **clean** | `ready` else **undeclared** |

Two failures follow:

1. **The gate passes on a half-implementation.** "At least one test per cell" is satisfied by covering one branch. The very distinction PD11 exists to enforce — staged work is never silently lost — is the branch a lazy implementation would skip.
2. **`(dirty-with-staged-changes, LockLeaseBecameStale)` has no defined outcome.** The default rule says unlisted pairs reject with `state_transition_invalid` — but a lease going stale is *time passing*, not a caller command. It cannot be "rejected". C6's own totality requirement ("every (state, event) pair → outcome") is violated by the amendment that claims to complete it.

Related: PD11 rule 7 says *"the C6 aggregate gate must cover it in the same commit that lands this matrix"* — the matrix landed in this commit and `LockLeaseBecameStale` exists nowhere in code, contract or tests. The rule is violated by its own amendment.

**Fix:** declare the guard as a third dimension of the matrix (the document already has precedent — `FolderWorkspaceDirtyResolution` discriminates `(UnknownProviderOutcome, ReconciliationCompletedDirty)` in both the doc and the code). Restate the gate as *"every `(state, event, guard-branch)` triple asserted"*, enumerate the else-branch of all four guards, and add the missing `(dirty-with-staged-changes, LockLeaseBecameStale)` row.

---

### HIGH-3 — The amendment orders a governance change that breaks an existing CI gate, without the lockstep rule the document itself established for exactly this case

**Checklist items:** 2, 7
**Location:** line 314 ("the governance YAML **must record those four as pending**"), line 316 (the existing lockstep precedent)

`docs/exit-criteria/c0-c13-governance-evidence.yaml` is unmodified and records all 14 criteria as `status: approved`, including C3 (L94), C6, C9. `docs/contract/oq3-authorization-evidence.yaml` still asserts `matrix_sha256: 5ffabd71…` as design-approved.

Flipping C3/C6/C9 to `reference_pending` as line 314 directs will **fail** `NfrTraceabilityConformanceTests.GovernanceEvidenceReferencePendingCriteriaStaySurfaced`, which asserts `criteria.Values.ShouldNotContain("reference_pending", …)` on the stated justification *"Every C0-C13 criterion is approved … so there is no governance-pending set left to project."*

The document's own §"Governance-approval / NFR-traceability decoupling precedent" (line 316) already codifies the right pattern: *"change the row, the traceability document's … section, and the test's `{ C3, C4, C7, C12 }` hard-pin set together"*. The 2026-09-15 amendment issues a governance cascade without applying its own convention.

**Fix:** extend line 314 with the lockstep set for this cascade — governance YAML status + `oq3-authorization-evidence.yaml` + `GovernanceEvidenceReferencePendingCriteriaStaySurfaced` (and its comment) in one commit — and state which of the four supersessions are recorded where, given that OQ3 is not represented in the C0–C13 YAML at all.

---

### HIGH-4 — Surviving passages now contradict the new material

**Checklist item:** 7
Each row below is a concrete contradiction introduced by this edit.

| # | Location | Contradiction |
| --- | --- | --- |
| 4a | lines 674, 680 vs 624 | The canonical CLI exit-code table still lists `73 | not_found`, and the MCP `kind` set still lists `not_found` — codes S-7 removes. There is **no** exit code, MCP kind, or canonical category anywhere for the new 503 authority envelope, for `resource_unavailable`, or for S-6's `withheld`. The CLI table and the MCP set are the level-below's actual contract; they were not touched. |
| 4b | line 624 vs lines 680 and 1674 | S-7 hard-codes *"All **49** protected Contract Spine operations"* while line 680 rules *"never hard-coded counts (2026-07-15)"* and line 1674 repeats it. The count is also already stale: the spine has 50 unique `operationId`s (`previous-spine.yaml` = 49 + `approved_additions: [GetWorkspaceCleanupStatus]`). |
| 4c | line 387 vs lines 354, 408 | `changes_staged → dirty` on `LockLeaseExpired` still carries side effect *"Operator intervention required"*, while the rewritten `dirty` row and PD11 rule 2 say the originating task resumes and `dirty` is not inherently awaiting-human. |
| 4d | concern #17 (line 119) vs S-6 | #17 still describes per-tenant policy as *"(hash/truncate/redact/expose) … applied uniformly across audit, projections, and console responses"* — read-side framing, and "hash" not token substitution. Concern #11 (line 113) and F-5 (line 697) still model only redacted-vs-unknown; the UX state list (line 287) has no `withheld`. |
| 4e | concern #20 (line 122) + S-4 vs S-7 / NFR76 | #20: *"local tenant-access projection allows **read paths to continue under bounded staleness**"*. S-7 and NFR76: authority that is **stale** returns 503 / denies by default on **every** protected operation. Degraded mode is either a decided capability or it is not. |
| 4f | lines 741, 743 | Spine Authoring Checklist still shows `[x] C3 … Legal-approved 2026-06-24` (now approval-pending per A7b) and `[ ] C6 Workspace State Transition Matrix enumerated` (unchecked, though the matrix exists and was just amended). |
| 4g | line 1706 (Gap Analysis), line 1710 (Important Gaps) | Still framed at 2026-07-14/15. Does not list the PD10 breaking spine remediation, the four superseded approvals, OQ11–OQ13, or the NFR74–NFR84 admission. Line 1710 still calls C3 a phase-deferred quantitative target rather than a reopened approval. |
| 4h | line 1765 (Readiness Assessment) | Header says "(updated 2026-09-12)" and the blocker list predates the amendment; the frontmatter says `implementationReadiness: 'not-ready (2026-07-14/15)'`; `updated:` says 2026-09-15. Three dates, none of which reflect this change. Rank 50 promises a readiness rerun but the section is untouched. |
| 4i | line 1773 (Key Strengths) | *"`FolderStateTransitions.cs` translates 1:1; matrix-coverage CI gate prevents drift."* Both halves are now false — PD11 exists precisely because the code diverges, and HIGH-2 shows the gate cannot see guarded branches. |
| 4j | lines 1682, 1684 (Requirements Coverage) | Lists exactly **nine** NFR categories. The PRD now has **eleven** `###` subsections (Edge Security & Deployment Hardening; Durable Operation & Release Evidence) and 84 NFRs; `nfr-traceability.md` and `NfrTraceabilityConformanceTests.Categories` both moved to 11. |
| 4k | line 58 (Requirements Overview) | *"**Nine** NFR categories drive architecture"* — same staleness at the top of the document. (The FR claim at line 54, "58 functional requirements", is **still correct**: the PRD has FR1–FR58 contiguous. The `Requirements to Structure Mapping` FR blocks are likewise still accurate.) |
| 4l | line 1537 mapping + structure tree (1110–1502) + line 1761 | No structural home exists for the S-6 write-time tokenizer, the S-7 authority-availability pre-check, or S-8 scope derivation; concern #17 still maps to `Observability/FolderAuditSanitizer.cs`. Yet the Completeness Checklist still checks *"Requirements to structure mapping complete"*, and NFR74–NFR84 appear in no mapping at all. |
| 4m | line 1802 (Implementation Handoff) | The must-pass gate list is unchanged: no PD11 three-way conformance assertion, no C9 event-write token-substitution tests, no S-7/S-8 denial-envelope gate — all three of which the amendment introduces as measurement methods at lines 327/330/333. |
| 4n | lines 959–960 vs line 415 | The Cleanup process pattern **already** said (2026-07-15) *"automatic only after task-terminal closure … with no active task or lock"*. Line 415 re-announces the same rule as the PD11/A7b change, in a different section, without cross-referencing it. Worse, line 960 says `dirty` is **never cleanup-eligible**, while the new `inaccessible → ready` row expires staged content at the C3 window and `changes_staged → inaccessible` promises staged changes are *"preserved intact"* with no window — three incompatible retention statements about the same staged content. |

---

### HIGH-5 — Divergence points the amendment misses

**Checklist item:** 1
The amendment fixes real divergences (the denial contract, the lifecycle model, and write-time confidentiality are genuinely the right things to fix at this altitude). It misses these:

1. **Gap `G4` — the effective-permission action catalog.** `docs/contract/authorization-matrix.md` records that the deployed catalog maps file-content-read and context-search to `read` where the matrix requires `write`, and repository-bind to `write` where folder-create applies. That is a live authorization divergence with no architecture rule. S-8 corrects *scoping* but not *level*.
2. **Misattribution of the PD10 remediation.** Line 627 says *"The matrix already records this remediation as gaps `G1` through `G11`."* Only 8 of 11 are PD10-owned: `G4` → "Story 12.1 and Epic 13 runtime authorization work", `G6` → OQ9 incident-access evidence, `G7` → Story 12.1 durable persistence + C7 runtime evidence. Folding them into PD10 leaves three gaps with two claimed owners.
3. **Non-canonical categories on the denial path.** `FolderAuthorizationDenialMapper` emits `not_found_to_caller`, `policy_denied`, `authorization_denied`, `policy_evidence_unavailable` — **none of which is a `CanonicalErrorCategory` member**. They deserialize as unknown in the typed SDK and fall through to CLI exit `1` / MCP `internal_error`. S-7 rewrites this exact path and does not mention them.
4. **Vocabulary extensions required by S-6.** `SensitiveMetadataTier` needs a `confidential` member; `RedactionVisibility` / `FolderAuditRedactionState` / `FieldDisclosure` need `withheld`. All are wire or UI enums; all are breaking additions; none appears in the PD10/PD8 change set.
5. **Published operations documentation.** `docs/operations/audit-and-redaction.md:213` still specifies *"hashed at the projection layer"* — the model S-6 supersedes. The PD10 paragraph lists "the published docs" for the spine correction; PD8 has no equivalent lockstep list.

---

### MEDIUM-1 — Escape hatches with no defined set, artifact, approver or gate

**Checklist items:** 3 (primary), 2, 6
**Locations:** line 624 (S-7), line 259 (NFR75, OQ12, OQ13)

- *"MVP release reasons permit only approved values **such as** `caller_completed`"* — a non-exhaustive enumeration inside a normative contract clause. Two implementers will pick different sets. "Release reason" is also an undefined term at first use (presumably lock-release reason).
- *"reserved post-MVP reasons are rejected"* — the reserved set is never listed, so "rejected" is unenforceable.
- NFR75: *"unless an **approved deployment policy** explicitly allows them"* — names no policy artifact, no approver, no gate. This is the escape hatch on an SSRF control (HXF-SEC-002).
- OQ12/OQ13 turn on *"one **supported deployment profile**"*. The term appears nowhere else in the document; §Infrastructure & Deployment (I-1…I-9) defines hosting decisions but never a named, enumerable deployment profile. Two stories can close OQ12 and OQ13 against different profiles.

**Fix:** enumerate the approved release-reason set and the reserved set; name the deployment-policy artifact, its approver and its gate; add a `Supported Deployment Profile` definition to §Infrastructure & Deployment (or declare it an open question with an owner).

---

### MEDIUM-2 — PD10/PD11/PD8 implementation is unowned at every execution rank

**Checklist items:** 2, 3
**Location:** lines 235–242 (wave table), line 253

Rank 0 ("Authority relock") reads *"Approve the 2026-09-15 proposal; record the OQ1–OQ4 decisions; **apply PD8, PD10, and PD11**; reapprove the changed C3, C6, C9, and OQ3 digests"* — everything else in that wave is an approval action. But "apply PD10" means regenerating the OpenAPI spine, the NSwag client, the CLI/MCP mappers, the parity fixtures, the C13 inventory, the published docs and the tests across 49 operations; "apply PD11" means a coordinated change to `FolderStateTransitions.cs`, `FolderStateApply.cs`, `FolderAggregate.cs`, `DispositionLabelMapper.cs`, the spine enum, the SDK, and five test classes; "apply PD8" means building a write-time tokenizer that does not exist. Ranks 30–40 list stories that *follow* PD8/PD10/PD11 (4.20, 4.19, 4.21) but none that *owns* them.

**Fix:** either charter the implementation stories and give them ranks (10 or 20, since rank-30 stories depend on them), or state explicitly that rank 0 "apply" means document-level application only and name the rank at which the code lands.

---

### MEDIUM-3 — `dirty`'s conditional disposition cannot be generated from the table the document says generates it

**Checklist items:** 2, 5
**Locations:** line 354 (state catalog row), line 421 (generation rule)

Line 421: *"Operator-disposition mapping is sourced from this table; `Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` is generated from it (or hand-written and tested against it)."* The new `dirty` cell is a two-branch predicate over inputs the table never names (has-staged-changes, originating-task-resumable). `DispositionLabelMapper` is a flat `state → label` function today and `DispositionLabelParityTests` pins it that way across every SDK `LifecycleState`. `ready`'s existing conditional is precedented but takes one declared input (projection lag vs C2); `dirty`'s takes two undeclared ones.

**Fix:** declare the disposition inputs as an explicit signature (`(state, projectionLag, hasStagedChanges, resumableTask) → disposition`) and update line 421 and the parity test contract accordingly.

---

### MEDIUM-4 — Stale canonical-category count adjacent to the changed text

**Checklist item:** 7
**Location:** line 680

*"the full `CanonicalErrorCategory` enum (**43** post-SDK members)"* — the enum has **49** members (`hexalith.folders.v1.yaml:11054-11103`, `HexalithFoldersClient.g.cs:13324-13474`), and `McpFailureKind` has 51. Pre-existing, but the amendment changes that exact set and should have reconciled it — particularly since the same paragraph is the one forbidding hard-coded denominators.

---

### LOW-1 — The two new security decisions are not in a table

**Checklist item:** 8
**Location:** line 623 (blank line), lines 624–625

Line 623 is empty, orphaning S-7 and S-8 from the `| # | Decision | Choice | Rationale | Alternatives considered |` header at line 616. In GFM a table body with no preceding header row is not a table: S-7 and S-8 will render as literal pipe-delimited paragraph text. The two most consequential new decisions in the amendment are the two that will not render.

Also at line 620, the PD10 evaluation-order sentence is appended to the S-4 cell with no separating punctuation: *"…emits metadata-only audit evidence **Evaluation order (PD10, 2026-09-15):** authority-unavailability…"*.

**Fix:** delete line 623; add a period before "**Evaluation order**" at line 620.

---

### LOW-2 — The amendment is scattered and partly duplicative

**Checklist item:** 8
The 2026-09-15 material lives in five widely separated places: 197–232, 251–263, 300–333, 354–415, 620–627. The **Release Authority Overlay** — which governs the whole document — sits inside `## Project Context Analysis`, roughly 400 lines before the exit criteria it supersedes and 1400 before the decisions it governs. Nothing near the top of the document tells a reader that S-6 has been rewritten or that S-7/S-8 exist.

Duplication: the wave table (235–242) restates §7.1 of the proposal and what the manifest is supposed to hold; the C3 cleanup rule (415) restates the Cleanup process pattern (959–960); the supersession table (48–53 of the diff) restates the C3/C6/C9 criteria rows (300–306) and the status-reconciliation note (314). Three copies of the C3 supersession, two of the cleanup rule, two of the schedule.

**Fix:** keep one authoritative statement per rule and cross-reference the rest. Add a short "Amendment index — 2026-09-15" near the frontmatter listing the seven changed sections with line anchors, or hoist the Release Authority Overlay to immediately after the document title.

---

### LOW-3 — Frontmatter readiness stamp not advanced

**Checklist item:** 7
`implementationReadiness: 'not-ready (2026-07-14/15)'` while `updated: '2026-09-15'` and §Readiness says "(updated 2026-09-12)". Pick one date convention and advance it, or state that readiness is deliberately frozen at the 2026-07-14/15 assessment until the rank-50 rerun.

---

## Item-by-item rationale

**1 — FAIL.** The three divergences it picks are the right ones, and the S-4/S-7 evaluation-order rule, the S-8 derived-scope rule, and the PD11 "staged work is never silently lost" rule are genuinely good spine decisions that will stop stories diverging. But it misses `G4`, the CLI/MCP/exit-code reconciliation (HIGH-4a), the non-canonical denial categories, the four enum vocabulary extensions, and `docs/operations/audit-and-redaction.md`. See HIGH-5.

**2 — FAIL.** S-6 states an intention with no mechanism, no primitive, no owner component and a gate pointed at the wrong file (HIGH-1). The C6 totality gate cannot see the new guards (HIGH-2). The PD10 migration mechanism names a gate that does not cover error vocabulary (CRITICAL-2). The governance cascade breaks an existing gate with no lockstep rule (HIGH-3). PD8/PD10/PD11 have no owning story at any rank (MEDIUM-2).

**3 — FAIL.** "Reserved post-MVP" for the three operator operations is a legitimate deferral *shape* — fail-closed, inspectable, explicitly "not a no-op that appears to succeed" — but today the code accepts them and the spine publishes them, so the deferral is currently a divergence, not a boundary. "Approval-pending" is handled well ("superseding a digest does not retract completed implementation evidence"), as is "admission is not implementation evidence" and the body-content guard ("a future approved body-content capability requires a new requirement, story, rank, and dependency path" — a good rule). The failures are MEDIUM-1's undefined sets and the unowned implementation.

**4 — PASS.** Nothing newly named is stale or ill-fitting: RFC 9457, OpenAPI 3.1, NSwag, HMAC-SHA-256 (A-9), Aspire 13.4.6, Octokit 14.0.0, MCP SDK 1.3.0 all stand. The related problem — S-6 naming *no* primitive where one is mandatory — is charged to item 2, not here.

**5 — FAIL, and this is the decisive one.** See CRITICAL-1. The document has a working honesty convention and did not apply it to the three largest new assertions, in a repository whose own contract artifact (`authorization-matrix.md`, gaps G1–G3) and own traceability file (`nfr-traceability.md:54`, NFR8) are more honest about the same gaps than the architecture is.

**6 — FAIL.** Deployment/hosting (I-1, I-2), infra/provider strategy (D-3, D-5, A-6, A-7, C12) and operations (I-6, I-7, I-8, Phase 9 runbooks) are all decided. **Migration/rollout of the breaking contract change is entirely silent** (CRITICAL-2), and two operational terms the amendment leans on — "supported deployment profile" and "approved deployment policy" — are undefined (MEDIUM-1).

**7 — FAIL.** Fourteen concrete contradictions in HIGH-4, spanning every section the gate asked about: Query Facade (no contradiction found — it is consistent with the new material and correctly labelled), Ops Console limitation (consistent), Production Projection & Evidence Ownership (consistent), Architecture Readiness Assessment (4h), Requirements to Structure Mapping (4l), cross-cutting concerns (4d, 4e), Implementation Handoff (4m), plus Gap Analysis (4g), Key Strengths (4i), the CLI/MCP tables (4a), the state matrix itself (4c), the Cleanup pattern (4n), and the count claims (4b, 4j, 4k). On counts specifically: **FR = 58 is still correct**; **NFR categories = nine is now wrong (eleven)** at lines 58 and 1684; the "84 rows / 73-row inventory appended" arithmetic in the new text is correct and matches `nfr-traceability.md` (84 rows, declared at line 240) and `NfrTraceabilityConformanceTests.NfrTotal = 84`.

**8 — FAIL.** A broken table hides the two most important new decisions (LOW-1); the governing overlay is buried in a context-analysis section 1400 lines from what it governs; the C3 rule, the schedule, and the supersession list each exist in two or three copies (LOW-2).

---

## Minimum set to reach PASS WITH FINDINGS

1. Add target-state labels, current-code divergence statements, and owning story + rank for PD8, PD10 and PD11 (CRITICAL-1).
2. Add the `§"Contract Migration & Rollout (PD10)"` subsection and reconcile with A-11; correct the `previous-spine.yaml` claim (CRITICAL-2).
3. Regenerate the manifest or fix the delegation and add a lockstep rule for the duplicated schedule; remove "(regenerated 2026-09-15)" (CRITICAL-3).
4. Pin the S-6 primitive, tenant-scope it, give it a component home, and rename away from "correlation" (HIGH-1).
5. Make the C6 gate guard-aware and close the `(dirty-with-staged-changes, LockLeaseBecameStale)` hole (HIGH-2).
6. Add the governance-cascade lockstep set (HIGH-3).
7. Work HIGH-4's fourteen rows and HIGH-5's five misses.
8. Delete line 623.
