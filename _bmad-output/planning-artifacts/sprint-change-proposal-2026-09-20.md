---
project: Folders
date: 2026-09-20
last_updated: 2026-09-21
workflow: bmad-correct-course
mode: batch
status: a6b-approved-pre-a8-passing-a8-pending
scope: major
approval_required: true
source_artifacts_modified: candidate-governance-tooling-and-overlays-only
sprint_status_modified: false
general_execution_hold: true
sprint_status_regeneration_authorized: false
ordinary_story_execution_authorized: false
---

# Sprint Change Proposal — Post-Generation Approval and Freeze-Gate Reconciliation

## 1. Issue Summary

The September planning relock produced a version-2 manifest and PD10 v2 candidate. The two-stage gate-order
amendment is explicitly approved. Earlier concurrent repository writes invalidated the first A6b approval,
but the replacement candidate is now byte-stable, validated, and explicitly approved for Product,
Architecture, and Security. Sprint planning remains **FAIL** because A8 and its governed finalization have not
been approved or executed:

- two frozen Story 3.14 specifications carry conflicting workflow statuses (`blocked` and `ready-for-dev`);
- Jerome's invalidated 75-artifact A6b approval is preserved as history, and the replacement 148-artifact
  package now has separate current Product, Architecture, and Security attestations;
- two regeneration passes produced identical digests for the 148-artifact replacement package;
- the full 56-project solution build passed with zero warnings and errors;
- the corrected focused inventory passed 53 of 53 tests—the former 55-test total duplicated two tests in reporting;
- `RELOCK-SECTION9` passes in its pre-A8 form with the hold retained;
- A8 is not ready for a Product, Architecture, and Delivery decision; and
- `EXT-ES-EVENT-EVOLUTION` and `EXT-ES-RECOVERY` remain pending external platform releases.

This correction records current truth without starting implementation. It preserves every completed evidence
artifact, keeps both Story 3.14 specifications byte-stable, leaves `sprint-status.yaml` unchanged, and retains
the general execution hold.

## 2. Impact Analysis

### Epic and story impact

- **Epic 3:** Story 3.14 is canonically `backlog`, rank 31, execution `held`, evidence `open`. Neither frozen
  specification is lifecycle authority.
- **Epics 1, 4, 10, 12, and 13:** no story becomes executable. Story 1.17 still depends on A6b and A8; Stories
  12.1–12.2 retain `EXT-ES-EVENT-EVOLUTION`; Stories 13.5 and 13.7 retain `EXT-ES-RECOVERY`; Story 3.14 retains
  Stories 12.1, 12.2, and 12.4 plus A8.
- **Completed evidence:** unchanged. The canonical lifecycle values for completed stories remain intact.

### Artifact impact

The current PRD, architecture, UX, epics, manifest, C3, C6, C9, and NFR artifacts remain planning authority.
The A6b register overlay preserves Jerome's exact historical approval and marks it invalidated for changed
bytes. It separately records the explicit replacement-package approval against the stable current digests.
The pre-A8 Section 9 result passes, and the A8 package is ready for a separate decision without inferring one.
Final tracker/provenance reconciliation remains prohibited.

### Technical and release impact

No product code, runtime route, deployment, or release changes are authorized. The v2 candidate remains
non-routed. The external EventStore prerequisites remain external-owner-controlled and pending.

## 3. Recommended Approach

Use the approved two-stage governance adjustment. Jerome's earlier `yes` remains attached only to the exact
bytes originally presented and separately approves the A8 ordering protocol; it was not transferred to the
replacement package. Jerome's new `yes` explicitly approves the replacement A6b package for Product,
Architecture, and Security against the presented exact digests. Quiescence, two-pass regeneration, the full
build, all 53 focused tests, and pre-A8 Section 9 now pass. A8 remains a separate Product, Architecture, and
Delivery decision and is not approved by the A6b response.

This is a **Major** correction because it changes approval sequencing across Product, Architecture, Security,
and Delivery. Effort is low for planning artifacts and validation, but governance risk is high if the ordering
cycle is bypassed or approvals are inferred.

Rollback is not viable: the current planning snapshots and generated candidate contain valid evidence. MVP
scope does not change.

## 4. Detailed Change Proposals

### 4.1 Story 3.14 lifecycle conflict — recorded, no frozen file edits

**Artifacts:**

- `_bmad-output/implementation-artifacts/spec-3-14-complete-asynchronous-repository-creation-and-binding.md`
- `_bmad-output/implementation-artifacts/spec-3-14-complete-asynchronous-repository-creation-and-binding-2.md`
- `_bmad-output/planning-artifacts/story-3-14-lifecycle-reconciliation-2026-09-20.yaml`

**OLD:**

- first frozen spec: `status: blocked`, SHA-256
  `d21aa8200d1b322dc2487eac26c274c63d2062739f015f3129501e8a9170d652`;
- second frozen spec: `status: ready-for-dev`, SHA-256
  `5eec57125efe867f8ae2ffbf916b233d20248e7888b9a994f8f9a07447a765ee`;
- no explicit reconciliation record states which status controls execution.

**NEW:**

- both statuses are retained only as `workflow_snapshot_status`;
- `epics.md#Story-3.14` is the canonical definition;
- `planning-story-manifest.yaml` controls execution with `story_lifecycle_status: backlog`,
  `execution_rank: 31`, `execution_authorization_status: held`, and `delivery_evidence_status: open`;
- the unchanged tracker already agrees on `backlog`;
- reconciliation record SHA-256:
  `c04325dc7b4958675f91441482a1d6a025dad6ba68a94774aa86518ae4bb9fda`.

**Rationale:** This follows the existing authority precedence without deleting, choosing between, or rewriting
human-owned intent snapshots.

### 4.2 A6b/OQ3 replacement package — explicitly approved

**Artifact:** `_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml`

**OLD:**

```yaml
approval_readiness: awaiting-generated-target-digests
sha256: pending-1.17-GENERATE
approvals: []
```

**NEW:**

```yaml
approval_readiness: exact-bound-artifacts-approved
authorization_matrix_sha256: d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420
conformance_set_file_sha256: ef01adde04ad15a6218cf8d5d8163af7fcebcb86e5bd4d3972eb7107600772e5
declared_candidate_set_sha256: bc6ee8a51086e0dce51b61c28ce20d563c9652fac827f331e623a21a163f9e86
artifact_count: 148
approval_status: approved
approvals: [Product, Architecture, Security]
```

Current register SHA-256:
`e30495072d871fbb9ea2f66126ea14e6e80aa31a1d22fe6648c78b03b2514ad8`.

**Validation:** the explicit inventory excludes planning and implementation workflow records, so governance
records do not recursively invalidate it. Regeneration at `2026-09-21T08:21:48+02:00` and
`2026-09-21T08:21:54+02:00` produced byte-identical matrix, candidate-contract, conformance-set, and declared
candidate-set digests. The full solution build passed 56 projects with zero warnings and errors. The focused
inventory passed 53 of 53 tests: 2 PD10 conformance-set, 5 PD10 v2 candidate-contract, 7 authorization-matrix,
22 governance-completeness, and 17 NFR-traceability tests. The prior 55-test total was a reporting error: the
historical drift run was 51 passes plus 2 failures, not 53 passes plus 2 failures.

**Decision recorded and bounded:** Jerome's 2026-09-20 A6b approval against matrix SHA-256
`1d60f21874e0c2e4e44ebc839786d8f65e76ea56c748afb26376e5996cf0d7ef` and conformance-set SHA-256
`649ecfffd95b54ce086777496d612af2793e6b8d254d4985f35b0cbc85ae90ad` remains invalidated history. On
2026-09-21 Jerome explicitly approved the replacement package for Product, Architecture, and Security against
matrix SHA-256 `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420` and conformance-set SHA-256
`ef01adde04ad15a6218cf8d5d8163af7fcebcb86e5bd4d3972eb7107600772e5`. The replacement approval has no A8
effect and authorizes no tracker regeneration, hold removal, ordinary story execution, or v2 exposure.

### 4.3 RELOCK-SECTION9 result — passing pre-A8

**Artifact:** `_bmad-output/planning-artifacts/relock-section9-result-2026-09-17.yaml`

**OLD:** required artifact absent.

**NEW:** digest-valid pre-A8 result, SHA-256
`247b197c772df185af9710257c1042714ddb2d04a6836ed43ba4a0e3745c92bc`, with:

- all A1-A7b records present, including the explicit replacement A6b Product, Architecture, and Security attestations;
- FR/NFR lockstep, NFR traceability 17/17, 72-node/248-edge DAG, active-context scan, and manifest
  parse/vocabulary/identity checks passing;
- the exact tracker delta validated as Story 10.8 `done -> in-progress`, Story 10.9 `review -> done`, and
  missing backlog rows 1.17, 4.22, 12.7, and 13.7;
- `passing: true`, `status: pass-pre-a8`,
  `final_freeze_removal_validation_complete: false`, and hold retained.

**Rationale:** The separately approved two-stage protocol resolves the order cycle. Candidate reproducibility,
build, focused-test validation, and explicit A6b approval all pass. A8 remains a separate pending decision.

### 4.4 A8 ordering deadlock — protocol approved; preparation ready; A8 pending

**Artifacts:**

- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-15.md` Section 9 (historical authority,
  unchanged);
- `_bmad-output/planning-artifacts/authority-relock/2026-09-17/A8-FREEZE.md` (pending target, unchanged);
- `_bmad-output/planning-artifacts/authority-relock/2026-09-17/A8-PROTOCOL-APPROVAL-2026-09-20.yaml`
  (approved ordering record, SHA-256
  `fe803cfa6a8b4c005a229e5ed4096ffa60148964ecab0598c62e3c14b8ff3750`);
- `_bmad-output/planning-artifacts/authority-relock/2026-09-17/A8-FREEZE-PREPARATION-2026-09-20.yaml`
  (ready but unapproved preparation package, SHA-256
  `05c7d590783e0507399c74fc96aa760fa994b0aa10acd9e24d2ad3addd70d973`).

**OLD:**

1. Section 9 requires A8 approval and final sprint-tracker agreement before it passes.
2. `DEC-A8-HOLD` requires `RELOCK-SECTION9` first.
3. Sprint-status regeneration is forbidden until A8.

No legal execution order satisfies all three rules.

**NEW — approved amendment:**

1. **Pre-A8 validation:** after A6b approval, Section 9 validates A1–A7b, all current exact digests, the
   manifest/DAG, and the deterministic tracker delta. It does not require A8 or a future tracker digest.
2. **A8 authorization:** Product, Architecture, and Delivery approve only the exact tracker/provenance
   finalization. A8 approval does not itself remove the hold or authorize ordinary stories.
3. **Post-A8 finalization:** regenerate the tracker, reconcile provenance, rerun final Section 9, and remove
   the hold/close recovery only if the final result passes. Otherwise retain the hold.

**Decision recorded:** Jerome explicitly approved this sequencing amendment on 2026-09-20. The amendment
resolves ordering only; it does not supply Product, Architecture, or Delivery signatures for A8.

### 4.5 Exact post-A8 tracker changes — proposed, not executed

**Artifact:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Current SHA-256:** `98e779e58c2e5ebea18cd669c4913ffb410b1977a832f4b34cf4948523b7dc7e`.

**Proposed authorized delta only:**

- Story 10.8: `done -> in-progress`;
- Story 10.9: `review -> done` under the already-approved metadata-only scope;
- add Stories 1.17, 4.22, 12.7, and 13.7 as `backlog`;
- leave Story 3.14 `backlog`;
- leave every epic status unchanged;
- update `last_updated` and append evidence-linked reconciliation entries.

The future tracker digest is deliberately absent: it cannot be known without performing the currently
prohibited regeneration.

### 4.6 Manifest and approval overlays — proposed after explicit approvals

**Artifact:** `_bmad-output/planning-artifacts/planning-story-manifest.yaml`

**OLD:** immutable generation snapshot embeds the September 17 register digest and A2b/A3/A6b/A8 pending.

**NEW — proposed finalization:** preserve the generation snapshot in provenance, then produce a new revision
that binds the current approval register, accepted A2b/A3, any explicitly approved A6b/A8 records, the final
Section 9 result, and the regenerated tracker. Do not hand-edit only disputed rows.

Every external edge remains. A8 satisfies only the universal hold prerequisite; it does not satisfy
`EXT-ES-EVENT-EVOLUTION`, `EXT-ES-RECOVERY`, or any incomplete story prerequisite.

### 4.7 External EventStore dependencies — preserve unchanged

**OLD and NEW:**

- `EXT-ES-EVENT-EVOLUTION`: `acceptance_status: pending`, `external-owner-controlled`;
- `EXT-ES-RECOVERY`: `acceptance_status: pending`, `external-owner-controlled`.

Acceptance still requires an immutable released version/commit, exact evidence digests, named approvers, and
approval dates. Placeholder or Folders-local declarations remain invalid. Stories 12.1–12.2, 13.5, 13.7, and
all transitive dependents remain non-executable until their respective node is accepted.

### 4.8 Remaining artifact reconciliation inventory

| Artifact | Current treatment | Exact proposed change |
| --- | --- | --- |
| Both Story 3.14 specs | Preserved byte-for-byte | None unless a human later authorizes modification/deletion. |
| `epics.md` Story 3.14 | Canonical and already correct | None. |
| `lifecycle-reconciliation-2026-09-17.yaml` | Immutable A3-bound generation journal | Do not rewrite; use the new Story 3.14 overlay. |
| `planning-authority-relock-approval-register.yaml` | A6b approved against stable exact digests; A8 pending | Preserve both the three invalidated historical role records and the three current replacement-package attestations. |
| `A6B-OQ3-PD10.md` | Approved decision payload with a stable generated binding in the register | Do not rewrite; the current approval is bound through the register. |
| `A8-FREEZE.md` | Pending target with circular ordering | Supersede its ordering through this proposal only if the two-stage protocol is explicitly approved. |
| `c3-retention.md` | Governing header says A7b approved; one closing sentence still says blocked on A7b | Replace only that stale sentence with “bound to approved A7b; runtime remains Story 4.22” during final provenance reconciliation. |
| `implementation-readiness.md` | Historical 2026-09-17 FAIL | Do not overwrite; create a new dated readiness assessment only after final Section 9 validation. |
| Planning `.memlog.md` | No current-run entry | Append decisions and final digests only after the corresponding human decisions occur. |
| `sprint-status.yaml` | Stale by policy | Apply only the delta in §4.5 after A8 authorization. |
| PRD, architecture, UX, epics, C3/C6/C9/NFR authority | Exact current inputs | Preserve until final provenance binding; do not rewrite product intent. |

## 5. Exact Observed Digest Set for Ready A8 Preparation

The unapproved A8 preparation package is ready at SHA-256
`05c7d590783e0507399c74fc96aa760fa994b0aa10acd9e24d2ad3addd70d973` and binds 18 exact observed
artifacts, including the passing pre-A8 result. This is readiness evidence, not an A8 approval:

| Artifact | SHA-256 |
| --- | --- |
| PRD | `743e8f7a001f67a136d817154af0f25773fab21ef6e6bc5adf08c1e3694d731c` |
| Architecture | `74ef242f6bfb77458818ab08a8acc25f547d6c891d29d36ab4954f7ca879973e` |
| UX | `d6a72eb0eb60dc60086d57725188cca35da910eb7cdde3333f17253d8014a17b` |
| Epics | `a863dc5a6f1b44986fa2dadb9c21f1657c02700b8506368b167d651274e9dbcc` |
| Manifest | `28a454ebff0d0475886d407a2342fa041f019bbcea49f7fa6e58108dafdaa5af` |
| Current sprint tracker input | `98e779e58c2e5ebea18cd669c4913ffb410b1977a832f4b34cf4948523b7dc7e` |
| Approval register | `e30495072d871fbb9ea2f66126ea14e6e80aa31a1d22fe6648c78b03b2514ad8` |
| C3 | `d33d13768bf03ee6baa995c6ef5c55a6e34f47312e29309f52a754d926dfdba1` |
| C6 | `a1d8b0357e42f046e2b3511f1a4c4b2457b9c945b12119bbfaf2ac180355efb6` |
| C9 payload | `fcf1277876d155cbf128703f289eacb7601490e504f61874be92293a141ba489` |
| Authorization matrix 2.0.0 | `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420` |
| Byte-stable conformance-set file | `ef01adde04ad15a6218cf8d5d8163af7fcebcb86e5bd4d3972eb7107600772e5` |
| NFR traceability | `d490ef1178d74198982035b3b3adbaa4cc5a479f32b79711fc04b9789d7cd588` |
| Lifecycle journal | `08e14c3df216c598942cbb9f585c67c4fe84b0d0f5e3f7de61d1c36a63ce9aec` |
| Story 3.14 reconciliation | `c04325dc7b4958675f91441482a1d6a025dad6ba68a94774aa86518ae4bb9fda` |
| Section 9 pre-A8 result | `247b197c772df185af9710257c1042714ddb2d04a6836ed43ba4a0e3745c92bc` |
| Approved A8 protocol record | `fe803cfa6a8b4c005a229e5ed4096ffa60148964ecab0598c62e3c14b8ff3750` |
| C0–C13 governance evidence | `ffe3d9a013a24646c0c7257effd620dd3f0602f2009b5d9381ddab28741d59cb` |

The generated sprint-tracker output and final post-A8 Section 9 output are intentionally not fabricated. They
become bindable only after A8 authorization permits their creation under the continuing hold.

## 6. Implementation Handoff

Classification: **Major — Product Manager and Solution Architect coordination with Security and Delivery.**

The explicit A6b Product, Architecture, and Security decision is recorded. A8 is eligible for a separate
Product, Architecture, and Delivery decision, but none is requested or inferred in this turn. No tracker
regeneration, provenance finalization, hold removal, or Developer story implementation is authorized.

Success criteria:

1. Candidate generation is byte-stable across a quiescent window and all required builds/tests pass.
   **Met: byte-identical two-pass generation, 56-project build clean, 53/53 focused tests passed.**
2. A6b has explicit Product, Architecture, and Security approvals against the newly presented exact two file
   digests. **Met by explicit approval on 2026-09-21.**
3. The Section 9/A8/tracker sequence has an explicit human-approved non-circular protocol. **Met.**
4. Any A8 approval has Product, Architecture, and Delivery signatures and binds exact available artifacts.
5. Sprint tracking is not regenerated before A8 authorization.
6. The general hold remains true until the final Section 9 result passes.
7. External EventStore nodes remain pending and continue to block every direct and transitive dependent.

## Change Navigation Checklist Record

- [x] 1.1–1.3 Trigger and evidence: current sprint-planning FAIL, duplicate Story 3.14 statuses, missing Section 9 result, and pending approvals.
- [x] 2.1–2.5 Epic impact: stable story identities retained; no resequencing or scope addition; external edges preserved.
- [x] 3.1 PRD: no product-intent conflict or edit required.
- [x] 3.2 Architecture: current mechanism authority retained; only approval ordering requires a new decision.
- [N/A] 3.3 UX: no behavior change proposed.
- [x] 3.4 Approval register, manifest, tracker, lifecycle evidence, Section 9, A8, C3, and external nodes assessed.
- [x] 4.1 Direct governance adjustment is viable; low editing effort, high approval-integrity risk.
- [x] 4.2 Rollback rejected because it would discard valid evidence.
- [N/A] 4.3 MVP scope remains unchanged.
- [x] 4.4 Recommended freeze path completed through stable A6b validation and explicit approval; A8 remains separate.
- [x] 5.1–5.5 Proposal, exact deltas, digest package, and non-implementation handoff complete.
- [x] 6.1–6.2 Governance artifact integrity, candidate reproducibility, full build, and corrected 53-test inventory are validated.
- [x] 6.3 A6b Product, Architecture, and Security approval is explicitly recorded; A8 remains pending and uninferred.
- [N/A] 6.4 Sprint-status regeneration is prohibited in this workflow.
- [x] 6.5 Handoff and success criteria are explicit.

## 7. Decision Recorded and Current Boundary

Jerome explicitly approved A6b for Product, Architecture, and Security against these exact files:

- `docs/contract/authorization-matrix.md` — SHA-256
  `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420`
- `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` — SHA-256
  `ef01adde04ad15a6218cf8d5d8163af7fcebcb86e5bd4d3972eb7107600772e5`

The conformance set declares candidate-set SHA-256
`bc6ee8a51086e0dce51b61c28ce20d563c9652fac827f331e623a21a163f9e86` for 148 artifacts.

No A8 approval is requested or recorded in this turn. The general hold, sprint-status regeneration prohibition,
v2 exposure prohibition, and both external EventStore dependencies remain unchanged.
