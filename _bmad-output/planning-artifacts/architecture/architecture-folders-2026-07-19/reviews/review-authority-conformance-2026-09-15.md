# Reviewer Gate — Authority Conformance and Lockstep

- **Reviewed artifact:** `_bmad-output/planning-artifacts/architecture.md` (working-tree amendment, uncommitted; baseline `HEAD` = `1621358`)
- **Authority under review:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-15.md` (status `approved`, sponsor Jerome 2026-09-15T16:08:21+02:00, decisions A1–A8; `required_role_signoffs: pending`, `execution_freeze: retained`)
- **Lens:** authority conformance and lockstep — the amendment must say exactly what the approved proposal authorised, no more and no less
- **Reviewer:** BMad architecture Reviewer Gate (read-only; no artifact other than this report was modified)
- **Date:** 2026-09-15

## Verdict

**PASS WITH FINDINGS — do not commit as-is.**

Transcription fidelity is high: §7.1 is verbatim, §7.2 is complete, §7.3 has a home for every rule, and all six §5.2 changes landed. The defects are not omissions of authority; they are *additions* beyond it and one verified red gate. One CI gate is failing **right now** because of this amendment, one governance directive in the text would redden two more gates if followed literally, and the C6 matrix as written cannot be implemented by the mechanism the same section prescribes.

**Verified by execution** (not inference): `dotnet run --project tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj` → `Total: 314, Failed: 1`. The single failure is caused by this amendment (F1).

## Coverage summary

| Check | Result |
| --- | --- |
| §5.2 required architecture changes | **6 / 6 landed** (qualified by F2, F3, F6) |
| §7.1 execution waves — rank rows | **6 / 6**, all prerequisite edges transcribed, 0 dropped, 0 altered |
| §7.2 authorization-spine rules | **10 / 10 homed** |
| §7.3 lifecycle rules | **8 / 8 homed**, but 2 not expressible from the table as written (F2); table asserts 3 unauthorised edges + 1 unauthorised event + 1 unauthorised disposition change |
| NFR74–NFR84 meaning fidelity | 9 faithful, 2 softened paraphrases (F9) — exact text is correctly carried by `prd.md`/`epics.md` |
| Over-reach items examined | 4 requested + 3 found; 1 defect, 2 defects-adjacent, 4 acceptable-but-flag |

---

## 1. §5.2 completeness — six required changes

§5.2 of the proposal lists six bullets. Each is quoted and dispositioned.

### 5.2.1 — "Add a **Release Authority Overlay — 2026-09-15** identifying the PRD as product authority, architecture as mechanism authority, epics as acceptance authority, and the regenerated manifest as lifecycle and dependency control."

**LANDED, faithful.** `architecture.md` §"Release Authority Overlay — 2026-09-15" (line ~196) carries a four-row table with exactly those four assignments and adds the correct non-authority disclaimer ("This document does not assign story status and does not schedule work") plus the frozen-specification `status` rule from §2 of the proposal.

One defect inside it — see **F4b**: the table cell reads "`planning-story-manifest.yaml` (regenerated 2026-09-15)". The manifest has **not** been regenerated; it still declares `generated_on: '2026-08-04'`.

### 5.2.2 — "Replace numeric-epic sequencing with the execution-wave model in Section 7.1."

**LANDED, faithful.** The wave table is transcribed (see §2 of this report), the "Execution waves replace numeric-epic order" paragraph carries the identity-not-order rule and the manifest validation rule, and the previously numeric "**Dependency spine:**" sentence at line 253 was rewritten to defer to `execution_rank`. A repo-wide grep found no residual numeric-epic ordering claim elsewhere in the document.

### 5.2.3 — "Amend S-6/C9 so confidential overrides become correlation tokens at event-write time; prohibit durable cleartext and specify token auditability without reversible payload recovery."

**LANDED, faithful and complete on all three clauses.** S-6 (line 622) now states write-time substitution, an explicit durable-cleartext prohibition enumerating event / projection / audit record / log / trace / diagnostic / generated artifact, and token auditability ("a stable, non-reversible reference that correlates the same confidential value across records for operator triage and audit joins; it carries no recoverable payload and no key exists to restore one"). The C9 exit-criteria row (line 131 of the diff) and the C9 operations-plan row (measurement method now includes "event-write token-substitution tests asserting no durable cleartext") are updated in lockstep. The "Alternatives considered" column correctly records the superseded write-cleartext-and-redact-on-read option as the release-blocking contradiction rather than deleting it.

### 5.2.4 — "Replace the C6 transition table with the exact rules in Section 7.3 and identify C3 as the cleanup authority."

**LANDED, but over-delivered.** See §4 and findings F1, F2, F3, F6, F7. The C3 cleanup-authority block is faithful and complete (terminal task closure with no active task; explicitly *not* lock expiry, lease staleness, or cancellation alone; seven-day window retained pending Legal; plus the correct addition that a lifecycle transition is not itself a deletion event).

### 5.2.5 — "Add the authorization-spine requirements in Section 7.2, including evaluation order, safe-denial envelopes, structured scope metadata, and generated-surface parity."

**LANDED, faithful.** All four named sub-elements have a home: evaluation order → S-4 amendment; safe-denial envelopes → S-7; structured scope metadata → S-8; generated-surface parity → the "Authorization Spine Correction (PD10 — 2026-09-15)" block. See §3 for the rule-by-rule check. One formatting defect (F5) and one broadening (F8).

### 5.2.6 — "Admit Epic 13 as the owner of the NFR74–NFR84 release-hardening evidence; do not claim that admission is implementation evidence."

**LANDED, faithful.** The "Release-hardening evidence ownership (A2 / A2b, 2026-09-15)" paragraph admits Epic 13 as owner and adds OQ12/OQ13 with their §5.1 question text verbatim at rank 40. The "**Admission is not implementation evidence.**" paragraph states the non-claim explicitly and repeats the append-not-renumber rule. This is the strongest-conforming part of the amendment.

---

## 2. §7.1 transcription fidelity — execution waves

Compared cell by cell against §7.1 of the proposal.

| Rank | Proposal text | Architecture text | Verdict |
| --- | --- | --- | --- |
| 0 | Approve this proposal; record OQ1–OQ4 decisions; apply PD8, PD10, PD11; reapprove changed C3, C6, C9, OQ3 digests | identical (proposal named explicitly) | **exact** |
| 10 | 12.1 first; 12.2/12.3 follow 12.1; 12.6 may resume after 12.1, cannot close before OQ8 | identical | **exact** |
| 20 | 12.4 follows 12.1–12.3 + completed 3.11 and 3.13; 12.5 follows 12.1–12.3 + completed 10.6 | identical | **exact** |
| 30 | 4.18←12.1–12.2+PD11; 4.19←12.1–12.3+C6/OQ7; 4.20←12.1–12.3+OQ2/OQ3+PD8+PD10; 4.21←12.4+4.19–4.20+PD11; 6.12–6.13←12.1–12.2+owning event flows; 6.14←6.12–6.13+4.18–4.21+OQ9; 10.8←12.1–12.3+12.5+completed 10.7+Story 11.15 DCP lane+corrected authorization projection | identical, all 8 story rows, all prerequisite edges | **exact** |
| 40 | OQ5←10.8 live round trip; OQ6←4.18+6.12–6.14; OQ7←4.19/C6; OQ8←12.6; OQ9←6.14; OQ11←durable 12.x/4.x/provider vertical slice; OQ12+OQ13←Epic 13 security and capacity evidence | identical, all 7 | **exact** |
| 50 | OQ10 last; calibration plan only after preceding gates have named outcomes, then rerun readiness | identical | **exact** |

Trailing rules also transcribed:

- "Story 10.9 is not a forward capability dependency … a future approved body-content feature must receive a new requirement, story, rank, and dependency path" → present verbatim.
- "reject cycles and reject any prerequisite whose `execution_rank` is not lower than the dependent item, except a referenced item already in a terminal accepted state" → present, restated as "a prerequisite must sit at a strictly lower rank than its dependent unless the referenced item is already in a terminal accepted state, and the graph must be acyclic."

**Result: 6/6 rank rows, 0 dropped edges, 0 altered edges, 0 invented edges.** §7.1 is the cleanest transcription in the amendment.

---

## 3. §7.2 transcription fidelity — ten authorization-spine rules

| # | §7.2 rule | Home in architecture | Verdict |
| --- | --- | --- | --- |
| 1 | 49 protected ops evaluate authority before lookup; one 404 `tenant_access_denied`/`resource_unavailable`; remove `not_found`, `cross_tenant_access_denied`, `audit_access_denied` | S-7 sentences 1–3 | **homed**; broadened — see F8 |
| 2 | One non-disclosing 503 with `details.visibility: redacted`, available to every protected op, evaluated before lookup | S-7 sentence 4 | **exact** |
| 3 | `GetReadinessDiagnostics` + `GetProjectionFreshness` receive folder scope | S-8 | **exact** |
| 4 | `ListFolderAclEntries` requires folder `administer` | S-8 | **exact** |
| 5 | `GetEffectivePermissions` self-inspection with folder read authority + optional task context | S-8 | **exact** |
| 6 | `GetTaskStatus` requires current tenant, bound-folder read authority, task scope | S-8 | **exact** |
| 7 | `ValidateProviderReadiness` stays tenant-level under folder-create authority; invents no folder ACL | S-8 | **exact** |
| 8 | Structured metadata; provider/repository/ref/task derived from an authorized folder/task/binding; raw locators never authority-bearing | S-8 opening + rationale | **exact**, and correctly tied back to the S-3 tenant-in-payload rule |
| 9 | Every error carries `visibility`; MVP release reasons permit only approved values such as `caller_completed`; reserved post-MVP reasons rejected | S-7 final sentence **and** A-8 error contract | **exact**, homed twice (appropriate: A-8 is the error-contract owner) |
| 10 | Regenerate OpenAPI, client, CLI/MCP parity, previous-spine, C13 inventory, docs, tests; reapprove OQ3 against resulting digest | "Authorization Spine Correction (PD10 — 2026-09-15)" block | **exact**, plus the correct added observation that removing caller-visible codes trips the C13 symmetric-drift gate by design and the removals belong in `previous-spine.yaml` in the same commit |

**Result: 10/10 homed. No rule is missing, and no rule is softened.** Evaluation order (required by §5.2.5 but not itself numbered in §7.2) is homed in S-4.

---

## 4. §7.3 transcription fidelity — eight lifecycle rules

Every rule has a home. The problems are expressibility and scope creep, not omission.

| # | §7.3 rule | Table / block home | Verdict |
| --- | --- | --- | --- |
| a | originating task re-acquires dirty-with-staged: `dirty + WorkspaceLocked -> changes_staged`, new lock instance | transition row + PD11 rule 2 | **homed**; guard is prose-only, and the clean-`dirty` + `WorkspaceLocked` case has no row at all |
| b | retryable commit failure, no confirmed remote effect: `changes_staged -> dirty`; known non-retryable: `-> failed` | two rows + PD11 rule 1 | **homed but NOT expressible** — see F2 |
| c | `changes_staged + AuthRevocationDetected -> inaccessible`, staged changes preserved | transition row + PD11 rule 1 | **homed**, but the row adds 2 events the rule does not authorise — see F6 |
| d | after readiness restored: `-> dirty` if staged content within the C3 window, else `-> ready` | two rows + PD11 rule 3 | **homed but NOT expressible** — see F2 |
| e | clean dirty whose lock becomes stale returns to `ready` and unlocked | `dirty -> ready` on `LockLeaseBecameStale` + PD11 rule 4 | **homed**, but introduces a new wire event — see F3 |
| f | `unknown_provider_outcome` auto-recovering during bounded checks; escalates only when checks cannot establish the result | state-catalog row (pre-existing, 2026-07-15) + PD11 rule 5 | **exact**, correctly reconciled with the existing five-checks-in-15-minutes budget |
| g | operator discard / retry-success / mark-failed reserved post-MVP, reject in MVP | three rows retitled "reserved post-MVP; fails closed in MVP code" + PD11 rule 6 | **exact**; the added "must not implement them as no-ops that appear to succeed" is a faithful strengthening consistent with the planning-consistency invariant |
| h | C3 cleanup starts only after terminal task closure with no active task; seven-day window unless Legal approves otherwise | "**C3 is the cleanup authority (2026-09-15).**" block | **exact** |

### Transitions the table now asserts that §7.3 does NOT authorise

Computed by re-running the gate's own parser (`ArchitectureTransitionRow` regex, same slice bounds) against `HEAD` and the working tree:

```
old edge count: 34    new edge count: 41    removed: 0
+ changes_staged->dirty:CommitFailed                       (authorised, rule b)
+ changes_staged->inaccessible:AuthRevocationDetected      (authorised, rule c)
+ changes_staged->inaccessible:TenantRevoked               (NOT authorised — F6)
+ changes_staged->inaccessible:RepositoryDeletedAtProvider (NOT authorised — F6)
+ dirty->changes_staged:WorkspaceLocked                    (authorised, rule a)
+ dirty->ready:LockLeaseBecameStale                        (authorised outcome, unauthorised event — F3)
+ inaccessible->dirty:ProviderReadinessValidated           (authorised, rule d)
```

Note `inaccessible->ready:ProviderReadinessValidated` was **retained**, not replaced — which is correct per rule d but creates the ambiguity in F2.

---

## 5. NFR74–NFR84 — meaning drift against §5.1 item 4

The architecture renders the eleven requirements as one prose paragraph inside the Epic 13 section rather than eleven normative rows. That is appropriate: §5.2.6 asks only for ownership admission, while §5.1 (PRD) and §5.4 (epics) own the exact wording. Both of those **do** carry the text exactly (`prd.md` inventory; `epics.md` lines 242–252, verbatim against the proposal). So the authority of record is safe. Comparing the architecture's paraphrase anyway:

| NFR | Drift | Severity |
| --- | --- | --- |
| NFR74 | "HTTPS-or-approved-loopback bearer transport" — drops "explicitly" and "development" | trivial |
| NFR75 | drops "explicitly" from "explicitly allows them" | trivial |
| NFR76 | none | — |
| NFR77 | none (reordered only) | — |
| NFR78 | none | — |
| NFR79 | none | — |
| NFR80 | drops the "supported" qualifier on deployments | trivial |
| NFR81 | **"readiness reporting actual dependency health rather than configuration or seed data"** drops both "cannot report ready" (the negative control) and "alone" | **low — F9** |
| NFR82 | **"demonstrated metric and alert emission"** drops "every release-significant" (the scope quantifier) | **low — F9** |
| NFR83 | none | — |
| NFR84 | none | — |

No requirement is missing, reordered, renumbered, or contradicted. The count relock ("relocks at exactly 84 rows under the linked PD6; the existing 73-row inventory is appended to, never renumbered") matches A2b exactly.

---

## 6. Over-reach assessment

The four items named in the brief, plus three found during review.

### 6.1 New `LockLeaseBecameStale` event — **DEFECT (high), though the outcome it produces is authorised**

Necessary in spirit: the matrix is keyed on `(state, event)`, §7.3 rule e requires an outcome when "the lock becomes stale", and no existing event moves a lock from `expired` to `stale` (verified: `FolderWorkspaceLifecycleEvent` has `LockLeaseExpired` only). C7 `1.0.0` genuinely pins a 60-second expired-to-stale threshold, so the rule needs a trigger.

But this is **not** a documentation-only addition. `FolderWorkspaceLifecycleEvent` is a *published contract enum*: it appears in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` (~line 8910) and in `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs` with numbered `EnumMember` values. Adding a member is a Contract Spine change that §5.2 authorises only for PD10, and PD11 rule 7 names only the C6 aggregate gate as the lockstep obligation — it omits the spine, generated client, `previous-spine.yaml`, and C13 inventory. See **F3**.

### 6.2 Edit to the `dirty` state-catalog row — **ACCEPTABLE consequence, but a mechanism change that needs approval (medium)**

Rules a and e make the old flat `awaiting-human` label wrong, so *something* had to change. But the replacement is a two-branch predicate, and the architecture elsewhere pins operator disposition as an "exclusively"-five-valued dimension and states that `DispositionLabelMapper.cs` is "generated from it (or hand-written and tested against it)". A predicate cell converts a static map into a runtime classifier that needs inputs the mapper does not currently receive (has-staged-changes, can-the-originating-task-resume). §7.3 says nothing about dispositions. See **F7**.

### 6.3 Addition of Story 12.6 to Epic 12 — **ACCEPTABLE, flag only (low)**

§7.1 rank 10 names 12.6 as scheduled work, §5.4 lists "12.1–12.6", and the story already exists in `epics.md` (line 2699) and `sprint-status.yaml`. Leaving it out of the architecture's own Epic 12 charter would have made the wave table reference a story the charter denies. This is reconciliation, not new scope. One residual inconsistency (**F10**): the ownership table at line 272 still reads "Stories 12.1–12.5".

### 6.4 "Ownership boundary" paragraph (NFR79/NFR80 mechanism with Epic 12) — **ACCEPTABLE, flag only (low)**

§5.2.6 assigns Epic 13 the *evidence*; §3 of the proposal already frames Epic 12 as the durable data plane. The paragraph is careful ("Epic 13 owns their release *evidence*, not their mechanism"), and it matches the implemented gate runner, which assigns NFR79 → consuming story `12-1` and NFR80 → `12-2` while keeping the rows release-blocking. No dilution of Epic 13's ownership of record. See **F11**.

### 6.5 Additional over-reach found — "`changes_staged → inaccessible`" event set (**F6**), the markdown table break (**F5**), the `403` removal (**F8**), and the manifest "regenerated" claim (**F4b**).

---

## 7. Lockstep debt — actual current state

Measured from the working tree at `HEAD` `1621358` (mtimes, `git status`, and content markers). **Nothing in this set is committed yet.**

### Already done (uncommitted, in the working tree)

| Artifact | State | Evidence |
| --- | --- | --- |
| `prd.md` | **DONE** — all 11 §5.1 edits present | NFR74–NFR84 in the inventory; OQ11/OQ12/OQ13 rows (lines 1086–1088) with owners/approvers/evidence paths; PD1/PD3/PD4/PD5/PD8/PD10/PD11 recorded as sponsor-approved with approvers, date, proposal path and digest `5d12ae4d…`; PD6 narrowed to the NFR1–NFR73 wording relock; provenance entry at line 95. mtime 19:54 |
| `docs/exit-criteria/nfr-traceability.md` | **DONE** — relocked to 84 | 84 rows / 11 categories; NFR74–NFR84 all `reference-pending` with named owners and consuming stories; NFR60/`C3` row untouched |
| `NfrTraceabilityConformanceTests.cs` | **DONE** | `NfrTotal` 73→84, two new category bands, facts renamed off the "SeventyThree"/"Nine" names |
| `tests/tools/run-nfr-traceability-gates.ps1` | **DONE** | `nfr_total` 84, `category_total` 11, eleven new release-blocking gap rows, runner method names updated |
| `epics.md` | **PARTIAL** — see below | |
| `_bmad-output/planning-artifacts/.memlog.md` | **PARTIAL** | Carries the 2026-09-15 PRD decisions and correctly logs the C6 41-vs-34 failure as belonging to the architecture pass; does not yet carry the architecture supersession links or freeze-release evidence |

### `epics.md` — partial, still owed against §5.4

- **done:** NFR74–NFR84 mirrored verbatim (lines 242–252).
- **owed:** the `84` traceability-count declaration; `execution_rank` / prerequisite fields on Stories 4.18–4.21, 6.12–6.14, 10.8, 12.1–12.6; the Story 10.9 retitle (still "Story 10.9: Authorized body-content materialization — C9 gated" at line 2327) and its PD5 safety-guard completion language; the §7.3 lifecycle rewrite of lifecycle-related acceptance criteria; Epic 13 / OQ12 / OQ13 evidence ownership; the Story 10.7 and 10.8 done-bar restatements.

### Still owed in full

| Artifact | Proposal section | Evidence it is untouched |
| --- | --- | --- |
| `ux-design-specification.md` | §5.3 | mtime **2026-07-07**; no "correlation token"/"correlation reference" text, no 2026-09-15 provenance. None of the four behaviour-visible corrections present |
| `planning-story-manifest.yaml` | §5.5 | `generated_on: '2026-08-04'`; no `execution_waves` / `execution_rank` / `story_lifecycle_status` / `workflow_snapshot_status` / `decision_status` / `delivery_evidence_status`; no OQ11–OQ13 |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | §5.6 | mtime **2026-09-12**; `10-8-…: done` (must become `in-progress`), `10-9-…: review` (must become `done` only after the retitle); no reconciliation journal; freeze and planning-recovery action unchanged (correct for now — §9 has not passed) |
| Story files / frozen specs | §5.7 | No reconciliation notes added to the Story 10.8 artifact |
| `docs/contract/authorization-matrix.md`, OpenAPI spine, generated client, CLI/MCP parity fixtures, `previous-spine.yaml`, C13 inventory | §5.8 | matrix mtime **2026-09-14**; OQ3 not reapproved; the spine still carries the pre-PD10 error shapes |
| `docs/exit-criteria/c6-transition-matrix-mapping.md` | §5.8 | mtime **2026-05-30**; state catalog still `dirty` → `awaiting-human`; 23-event vocabulary has no `LockLeaseBecameStale`; transition rows predate PD11 |
| `docs/exit-criteria/c3-retention.md` | §5.8 | mtime **2026-07-20**; cleanup trigger still elapsed-lock-time |
| `docs/exit-criteria/c0-c13-governance-evidence.yaml` | §5.8 | C3 / C6 / C9 all still `status: approved`; now **contradicted** by the architecture's own directive (F4) |
| `docs/diagrams/workspace-lifecycle.md` | §5.8 (consumer docs) | 34 diagram edges; `dirty` still `awaiting-human`; **this is the artifact whose absence fails the gate** (F1) |
| `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` + lifecycle tests | §5.8 | Still the 34-edge model; `inaccessible → Ready` unconditional; `ChangesStaged + CommitFailed → Failed` unconditional; the three operator events still transition rather than reject |
| `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` | consequential | line 33 `LifecycleState.Dirty => OperatorDispositionLabel.Awaiting_human` |
| C9 / security governance + evidence for event-write correlation tokens | §5.8 | Not present |

### Stale gate reports

`_bmad-output/gates/consumer-docs/latest.json` still reports `"status": "passed"` while its gate is now red. `_bmad-output/gates/nfr-traceability/latest.json` reports `"status": "failed"` — but that failure is **environmental, not content**: the runner uses `dotnet test --filter` (VSTest), which the pinned .NET 10 SDK refuses ("Testing with VSTest target is no longer supported by Microsoft.Testing.Platform"). Running the project directly shows every NFR traceability fact passing. Do not attribute that red to the 84-row relock, and do not "fix" it by reverting content.

---

## 8. Governance safety

**The `{C3, C4, C7, C12}` precedent is respected.** Three independent confirmations:

1. The amendment states it explicitly: *"Superseding a digest does not reopen the NFR rows that cite the criterion, and it does not retract completed implementation evidence"*, and *"no NFR row changes status as a result."*
2. `docs/exit-criteria/nfr-traceability.md` keeps `NFR60 … reference-pending … \`C3\`` unchanged, and every new NFR74–NFR84 row enters as `reference-pending` rather than `covered` — the correct direction under the standing rule.
3. `NfrTraceabilityConformanceTests.ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` and its sibling `GovernanceEvidenceReferencePendingCriteriaStaySurfaced` both **pass** in the verification run.

**But the amendment issues a governance directive that would redden two gates, and does not say so.** See **F4**.

---

## Findings

### F1 — CRITICAL — the amendment reddens a live CI gate, and the gate report still says "passed"

- **Location:** `_bmad-output/planning-artifacts/architecture.md` §"Workspace State Transition Matrix (C6 — Enumerated)", transition table. Failing assertion: `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs:557`.
- **Proposal says:** §5.2.4 — replace the C6 transition table with the §7.3 rules. §5.8 — update "`docs/exit-criteria/c6-transition-matrix-mapping.md`, transition code, and lifecycle tests" **in the same correction set**.
- **Architecture says:** the table alone was replaced; its seven new edges (41, up from 34) landed without the mapping doc, the diagram, the pinned constant, the code, or the tests.
- **Observed:** `Total: 314, Errors: 0, Failed: 1` — `WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix`: *"matrixEdges.Count should be 34 but was 41"*. `_bmad-output/gates/consumer-docs/latest.json` still records `"status": "passed"`, so the evidence trail is now dishonest — which is precisely what the document's own planning-consistency invariant forbids.
- **Correction:** land the C6 set as one commit — `docs/diagrams/workspace-lifecycle.md` edges and disposition row, `docs/exit-criteria/c6-transition-matrix-mapping.md` state catalog + event vocabulary (23 → 24) + transition rows, `FolderStateTransitions.cs`, the lifecycle tests, and the `ShouldBe(34)` / `ShouldBe(23)` pins — then regenerate the consumer-docs gate report. If that set cannot land now, revert the seven table rows and keep only the PD11 prose block until it can. Do not commit the architecture edit alone.

### F2 — HIGH — two §7.3 rules are not expressible from the table as written

- **Location:** rows `changes_staged → failed` / `changes_staged → dirty` (both on `CommitFailed`), and `inaccessible → dirty` / `inaccessible → ready` (both on `ProviderReadinessValidated`).
- **Proposal says:** §7.3 rules b and d — outcome depends on retryability and on whether staged content survives the C3 window.
- **Architecture says:** both `(state, event)` pairs now map to **two** outcomes, discriminated only by prose in the Side Effect column. The same section's "**Implementation enforcement:**" bullet still specifies "a switch expression over `(currentState, eventType)`" — which cannot express either rule. The document therefore contradicts itself.
- **Precedent available:** `unknown_provider_outcome + ReconciliationCompletedDirty` already solves this by naming a discriminator — `FolderWorkspaceDirtyResolution.CommitConfirmed` / `CommitRejected` — which the code carries as a third tuple element.
- **Correction:** name a discriminator for each ambiguous pair (e.g. `CommitFailureClass: Retryable | NonRetryable`; `StagedContentDisposition: WithinC3Window | Elapsed`), put it in the Triggering Event column the way the reconciliation rows do, and update the enforcement bullet to `(currentState, eventType, discriminator)`. Also decide `dirty + WorkspaceLocked` when the workspace is **clean** — rule a covers only the staged case, and the table has no row, so a clean dirty workspace can never be re-locked.

### F3 — HIGH — `LockLeaseBecameStale` is an unauthorised Contract Spine change presented as a matrix rule

- **Location:** transition row `dirty → ready`; PD11 rule 7.
- **Proposal says:** §7.3 rule e authorises the *outcome* ("a clean dirty workspace whose lock becomes stale returns to `ready` and unlocked") and names no event. §5.2 authorises spine changes only under PD10 (§7.2 rule 10), whose ten rules do not mention lifecycle events.
- **Architecture says:** PD11 rule 7 declares "**`LockLeaseBecameStale` is a new event** added to the architecture event vocabulary by this correction" and scopes the lockstep to "the C6 aggregate gate … in the same commit."
- **Why that scope is wrong:** `FolderWorkspaceLifecycleEvent` is published — `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` (~line 8910) and `HexalithFoldersClient.g.cs` (numbered `EnumMember` values, e.g. `LockLeaseExpired = 13`). Adding a member changes the generated client, requires a `previous-spine.yaml` entry, touches the C13 parity inventory, and lands inside the same digest that A6b must reapprove.
- **Judgement:** necessary consequence of rule e (no existing event moves `expired → stale`; C7 `1.0.0` pins the 60-second threshold) — **but it must be flagged to the approvers, not absorbed**.
- **Correction:** either (a) name the full consequence in PD11 rule 7 — spine, generated client, `previous-spine.yaml`, C13 inventory — and fold it into the A6/A6b regeneration and OQ3 re-approval scope, or (b) express rule e on the lock-state dimension (`expired → stale` is already an enumerated lock-state transition) without adding a wire event. Do not leave it as a documentation-only claim.

### F4 — HIGH — the governance directive would redden two governance gates, and the document does not say so

- **Location:** the "**Status reconciliation (updated 2026-09-15)**" blockquote: *"The governance YAML must record those four as pending rather than continuing to assert the superseded digests."* Reinforced by the overlay's C6 row: *"C6 stays reference-pending for transitions."*
- **Proposal says:** §5.8 authorises updating the governance authorities; §9 requires the C3/C6/C9/spine/retention gates to *pass* against their approved digests before the freeze lifts.
- **What the tests actually enforce:**
  - `GovernanceCompletenessGateTests.ApprovalBackedCriteriaCarryFreshExactApprovalRecords:377` — `RequiredScalar(row, "status").ShouldBe("approved", criterion)` for each of `ApprovalBackedCriteria = ["C3","C4","C7","C12"]`. Flipping **C3** to anything fails this outright.
  - `GovernanceCompletenessGateTests:310` — every criterion status must be `approved` or `reference_pending`; the literal token **`pending`** is not an allowed value.
  - `NfrTraceabilityConformanceTests.GovernanceEvidenceReferencePendingCriteriaStaySurfaced:302` — `criteria.Values.ShouldNotContain("reference_pending")`, with the message *"a new governance reference-pending criterion must be projected into the traceability bridge; restore that projection here."* So `reference_pending` is also blocked without a same-commit test change.
- **Net effect:** following the sentence literally reddens two gates. Following it with `reference_pending` reddens two gates. The sentence names neither consequence and neither lockstep edit.
- **The NFR precedent itself is SAFE** — the amendment explicitly states that superseding a digest does not reopen cited NFR rows, `nfr-traceability.md` leaves NFR60/`C3` alone, and the `{C3,C4,C7,C12}` hard-pin is untouched and passing.
- **Correction:** rewrite the sentence to name the mechanism and its cost — e.g. *"record the supersession as a dated `approval`-block annotation (or a new `superseded` status value added in lockstep to `GovernanceCompletenessGateTests` allowed values and `ApprovalBackedCriteria`, and to `GovernanceEvidenceReferencePendingCriteriaStaySurfaced`); until that lockstep lands, C3/C6/C9 keep `status: approved` in the YAML and their supersession is authoritative here."*

### F4b — HIGH — the overlay asserts a manifest state that does not exist

- **Location:** Release Authority Overlay table — "`planning-story-manifest.yaml` (regenerated 2026-09-15)".
- **Reality:** the manifest declares `generated_on: '2026-08-04'` and has none of the §5.5 fields (`execution_waves`, `execution_rank`, `story_lifecycle_status`, `workflow_snapshot_status`, OQ11–OQ13).
- **Why it matters:** the document names as *current lifecycle and dependency authority* an artifact that is still the superseded August 4 snapshot, while §9 requires that "no active implementation context cites the superseded August 4 manifest as current authority." A reader following the architecture is pointed straight at the superseded file.
- **Correction:** "`planning-story-manifest.yaml` (to be regenerated per §5.5 of the approved proposal; the 2026-08-04 manifest is superseded and must not be cited as current authority until then)".

### F5 — MEDIUM — blank line breaks the Authentication & Security decision table

- **Location:** `architecture.md:623` — a blank line between the S-6 row (622) and the S-7 row (624).
- **Effect:** S-7 and S-8 are orphaned from the header/delimiter rows; under CommonMark/GFM they render as literal pipe-delimited paragraph text, not as table rows in the S-decision table.
- **Correction:** delete line 623. (A second blank at 626 correctly separates the table from the PD10 prose block and should stay.)

### F6 — MEDIUM — two unauthorised transitions added to the C6 matrix

- **Location:** row `changes_staged → inaccessible`, Triggering Event column.
- **Proposal says:** §7.3 rule c — "`changes_staged + AuthRevocationDetected -> inaccessible` while preserving staged changes." One event.
- **Architecture says:** "`AuthRevocationDetected` / `TenantRevoked` / `RepositoryDeletedAtProvider`" — three.
- **Assessment:** symmetrical with the existing `ready → inaccessible` row, so defensible design — but it is unauthorised scope creep in a matrix the proposal calls "the exact rules", and it accounts for 2 of the 7 edges that break the pinned count. Under the C6 aggregate gate these pairs previously rejected with `state_transition_invalid`; they now transition, which is a behaviour change nobody approved.
- **Correction:** either restrict the row to `AuthRevocationDetected` per rule c, or list the two extra events in the reconciliation note as an architect-proposed extension for explicit approval.

### F7 — MEDIUM — `dirty` disposition became a predicate, diverging from three downstream authorities silently

- **Location:** state-catalog row for `dirty` — now "`degraded-but-serving` while the originating task can still resume or the workspace is clean; `awaiting-human` once staged changes are orphaned (lock lost, no resuming task)".
- **Proposal says:** §7.3 says nothing about operator dispositions.
- **Still pinning `awaiting-human`:** `docs/exit-criteria/c6-transition-matrix-mapping.md:23`; `docs/diagrams/workspace-lifecycle.md:22` and `:39`; `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs:33`.
- **Why this drift is silent:** `WorkspaceLifecycleDiagramDispositionTableMatchesC6StateCatalog` reads the *mapping doc*, not the architecture — so it passes while the architecture and the mapping doc now disagree. The gate cannot catch this class of drift.
- **Second-order problem:** the architecture pins operator disposition as an "exclusively"-five-valued dimension and says `DispositionLabelMapper.cs` is "generated from it (or hand-written and tested against it)". A conditional cell turns a static map into a runtime predicate requiring inputs the mapper does not have.
- **Correction:** keep the semantics (rules a and e make flat `awaiting-human` wrong) but express them as a rule beneath the catalog rather than inside the label cell, state the two-input predicate explicitly, and add the mapper/diagram/mapping-doc updates to the F1 commit. Flag the disposition change to the approvers as a PD11 consequence they did not vote on.

### F8 — LOW — S-7 removes the post-authentication 403, which §7.2 rule 1 does not name

- **Location:** S-7 — "The caller-visible distinctions `not_found`, `cross_tenant_access_denied`, and `audit_access_denied`, **and the post-authentication 403**, are removed from protected-operation responses."
- **Proposal says:** rule 1 names exactly three codes. 403 appears only in the reviewer's own rationale space, not in the rule.
- **Assessment:** arguably entailed by "every post-authorization denial returns one HTTP 404 shape" — but it is an additional wire-visible removal that must appear in the `previous-spine.yaml` deprecation window and in the regenerated matrix digest A6b reapproves.
- **Correction:** confirm against `docs/contract/authorization-matrix.md` gaps `G1`–`G11` that no protected operation legitimately needs a 403, and either cite the gap ID that authorises the removal or move the clause into the PD10 block as an architect-identified consequence.

### F9 — LOW — NFR81 and NFR82 paraphrases drop their controlling qualifiers

- **Location:** Epic 13 "Release-hardening evidence ownership" paragraph.
- **NFR81:** proposal — "readiness reports actual dependency health **and cannot report ready from configuration or seed data alone**"; architecture — "readiness reporting actual dependency health **rather than** configuration or seed data". The negative control and "alone" are gone.
- **NFR82:** proposal — "**every release-significant** metric and alert has demonstrated emission…"; architecture — "demonstrated metric and alert emission…". The scope quantifier is gone.
- **Assessment:** low, because `prd.md` and `epics.md` carry the exact text and are the wording authority; the risk is a reader quoting the architecture as canonical.
- **Correction:** restore "cannot report ready from configuration or seed data alone" and "every release-significant", or replace the paraphrase with a pointer to the PRD inventory.

### F10 — LOW — Epic 12 ownership table not updated alongside the 12.6 charter bullet

- **Location:** `architecture.md:272` — "**Epic 12** | … (Stories 12.1–12.5)" while the charter list above now runs 12.1–12.6.
- **Correction:** "Stories 12.1–12.6".

### F11 — LOW — "ownership boundary" paragraph states an allocation the proposal does not

- **Location:** "**Ownership boundary.** NFR79 and NFR80 describe properties the Epic 12 durable substrate must *provide*; Epic 13 owns their release *evidence*…"
- **Assessment:** acceptable and useful — it matches §3 of the proposal and matches the already-implemented gate runner (NFR79 → `12-1`, NFR80 → `12-2`), and it does not weaken Epic 13's ownership of record. Flag to approvers as an architect clarification rather than an approved decision.
- **Correction:** none required; annotate as a consequence in the reconciliation note.

### F12 — LOW — the decoupling-precedent paragraph is now stale about C3

- **Location:** "Governance-approval / NFR-traceability decoupling precedent (updated 2026-09-12)" — still reads "C3, C4, and C7 are approved", three paragraphs after the new text declares C3 superseded/approval-pending. The same stale sentence is mirrored in `docs/exit-criteria/c0-c13-governance-evidence.yaml` (comment, lines 62–64) and `docs/exit-criteria/nfr-traceability.md:39`.
- **Correction:** reword to "approved or relocking" in all three places. **Do not** touch the `{C3, C4, C7, C12}` hard-pin set or the NFR row statuses — the precedent itself is correct and the amendment respects it.

---

## Recommended disposition

1. **Block the commit** until the F1 lockstep set lands or the seven C6 table rows are withheld. The repository must not gain a red gate plus a `"passed"` report asserting otherwise.
2. **Fix before re-review:** F2 (discriminators + the enforcement bullet), F4 (governance sentence), F4b (manifest claim), F5 (table break), F10, F12.
3. **Route to approvers as PD11/PD10 consequences they did not vote on:** F3 (`LockLeaseBecameStale` as a spine change), F6 (two extra revocation events), F7 (disposition predicate), F8 (403 removal), F11 (NFR79/NFR80 mechanism allocation).
4. **Reconciliation note must record as still owed:** `ux-design-specification.md`; `planning-story-manifest.yaml`; `sprint-status.yaml`; the remainder of `epics.md` §5.4; `docs/contract` spine set + OQ3 re-approval; `docs/exit-criteria/c6-transition-matrix-mapping.md`; `c3-retention.md`; `c0-c13-governance-evidence.yaml`; `docs/diagrams/workspace-lifecycle.md`; `FolderStateTransitions.cs` + lifecycle tests; `DispositionLabelMapper.cs`; the C9 correlation-token security evidence. It must **not** claim the manifest was regenerated.
5. **Do not touch** the NFR row statuses or the `{C3, C4, C7, C12}` hard-pin. The amendment gets this right and the correction set must preserve it.

## Verification commands used

```
git diff -U8 -- _bmad-output/planning-artifacts/architecture.md
dotnet run --project tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj
   -> Total: 314, Errors: 0, Failed: 1, Skipped: 0  (WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix)
# C6 edge delta, computed with the gate's own ArchitectureTransitionRow regex and slice bounds:
   HEAD 34 edges -> working tree 41 edges, 7 added, 0 removed
stat -c '%y %n' <planning + docs/exit-criteria + docs/contract artifacts>
```

Note: `tests/tools/run-nfr-traceability-gates.ps1` (and the other `run-*-gates.ps1` runners) invoke `dotnet test --filter`, which the pinned .NET 10 SDK rejects under Microsoft.Testing.Platform. That is a pre-existing runner defect, unrelated to this amendment, and it is why `_bmad-output/gates/nfr-traceability/latest.json` reads `failed` despite every NFR traceability fact passing when the project is run directly.
