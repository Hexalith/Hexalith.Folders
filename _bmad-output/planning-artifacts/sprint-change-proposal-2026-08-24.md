---
project: Folders
date: 2026-08-24
workflow: bmad-correct-course
mode: batch
status: implemented
scope: minor
approval_basis: User explicitly requested resolution, governance synchronization, and complete Contracts.Tests verification.
---

# Sprint Change Proposal — Story 10.6 NFR Traceability Reconciliation

## 1. Issue Summary

Story 10.6 is implementation-complete but cannot close because its AC11 full-project gate reproduces two
planning-conformance failures. The current PRD and epics inventories contain 73 aligned NFR bullets, while
the Story 7.16 traceability bridge, conformance constants, focused gate, and committed report still describe
the earlier 70-row inventory. The first current PRD bullet derives SHA-256 prefix `1d924a03811d`; the
published trace row retains the pre-reconciliation prefix `f33274b9da03`.

The drift was introduced in two stages:

- Commit `da3d1115945b` reconciled the PRD on 2026-07-15 and changed its NFR inventory from 70 to 73.
- Commit `84071f2346d0` synchronized `epics.md` to the same 73 clauses on 2026-08-04, but did not migrate the
  Story 7.16 traceability bridge or its tests.

This is planning/evidence drift. It is not a Story 10.6 implementation or wire-behavior defect.

## 2. Impact Analysis

### Epic and story impact

- Epic 10 remains valid and needs no scope, ordering, or acceptance-criteria change.
- Story 10.6 implementation, REST/OpenAPI/SDK/CLI/MCP behavior, Dapr policy, and materializer behavior remain
  unchanged.
- Story 10.6 can move from `in-progress` to `done` only after the full
  `Hexalith.Folders.Contracts.Tests` project passes.
- Story 7.16 remains the semantic traceability owner. Its bridge must be migrated from the superseded 70-row
  source snapshot to the current 73-row source authority.

### Meaning and ownership of the net three-row increase

The cardinality change is not three bullets appended to the Verification Expectations category. It is a
net change produced by five new clauses and two consolidations:

| Current clause | Intended meaning | Delivery/evidence ownership |
| --- | --- | --- |
| NFR7 | Revalidate current authority before every mutation or asynchronous protected side effect; revocation fails closed. | Architecture/Security through C7 and workspace-lock delivery; reference-pending until executable revalidation evidence closes. |
| NFR8 | Apply C9 sensitivity tiers, including tenant-confidential write-time correlation tokens and visibly distinct disclosure states. | Security plus Audit/Projection delivery; reference-pending for the projection-side confidential override. |
| NFR16 | Treat an unconfirmed external effect as `unknown_provider_outcome` before bounded checks can escalate it to `reconciliation_required`. | Lifecycle/Provider Reconciliation; contract/state evidence is automated. |
| NFR23 | Require provider-confirmed durable remote/ref update before a workspace is successfully committed. | Provider/Delivery through Story 12.4; reference-pending until the real Git path is evidenced. |
| NFR31 | Emit bounded metadata-only audit evidence for query-limit decisions. | Contracts/Audit under C4; current structural/handler evidence applies. |

The old two-bullet audit-classification/protection pair was consolidated into current NFR54, and the old
two-bullet “retention must be defined” pair was consolidated into the binding C3 policy at current NFR60.
Therefore `+5 - 2 = +3`. Current NFR71–NFR73 are the prior verification bullets renumbered; their ownership
remains Contracts, Release Readiness, and Security respectively.

### Artifact conflicts

- `_bmad-output/planning-artifacts/prd.md`: authoritative and already correct at 73 bullets; no requirement
  text change.
- `_bmad-output/planning-artifacts/epics.md`: already identical to the PRD NFR inventory; no requirement text
  change.
- `_bmad-output/planning-artifacts/architecture.md`: no architectural decision change.
- `_bmad-output/planning-artifacts/ux-design-specification.md`: no UX change.
- `docs/exit-criteria/nfr-traceability.md`: migrate all row numbers, hashes, category ranges, ownership, and
  source-authority prose to the current 73-row inventory.
- `NfrTraceabilityConformanceTests.cs`: retain exact-cardinality, exact-text, stable-hash, evidence,
  negative-control, and fail-closed assertions while updating their authority to 73 and correcting the epics
  heading parser.
- `run-nfr-traceability-gates.ps1` and its committed report: synchronize method names, total, and owned gaps.
- Sprint/story governance: close the reconciliation action and Story 10.6 only after the complete Contracts
  project passes.

### Technical and MVP impact

There is no product or MVP scope change and no runtime technical impact. The change affects planning
traceability and release evidence only. Existing release-blocking gaps remain visible, and newly exposed
requirements are not mislabeled as implemented when their delivery evidence is still pending.

## 3. Recommended Approach

Use a direct adjustment:

1. Preserve the 73 authoritative PRD/epics clauses.
2. Rebuild the trace table in source order, migrating evidence semantically rather than shifting rows
   mechanically.
3. Update every SHA-256 prefix, including NFR1 to `1d924a03811d`.
4. Keep exact 73-row conformance and all negative controls; do not make the expected count dynamic and do not
   relax evidence or ownership assertions.
5. Regenerate the focused metadata-only report.
6. Run the complete Contracts.Tests project.
7. Only on green, mark Story 10.6 and its blocker action done while leaving unrelated Epic 10 work open.

Effort is low, risk is low, and timeline impact is limited to the reconciliation and verification run.
Rollback or MVP redefinition would discard approved planning decisions and is not justified.

## 4. Detailed Change Proposals

### PRD and epics

**Old:** PRD and epics each contain 73 matching NFR clauses, but downstream evidence assumes 70.

**New:** Keep both clause inventories unchanged and make downstream traceability consume all 73.

**Rationale:** The July 15 authority explicitly enumerates NFR1–NFR73, the August 4 epics reconciliation
matches it exactly, and the planning-story manifest names Story 7.16 as semantic traceability owner.

### Traceability bridge and conformance gate

**Old:** 70 rows; obsolete category ranges; stale hashes and numbering; parser expects the superseded
`### NonFunctional Requirements` heading.

**New:** 73 exact rows; current category ranges; all hashes re-derived; parser expects
`### Non-Functional Requirements`; exact row IDs NFR1–NFR73; updated focused gate/report.

**Rationale:** This restores one-for-one, fail-closed traceability without weakening any test.

### Sprint governance

**Old:** Story 10.6 and the reconciliation action are `in-progress`/`open` solely because Contracts.Tests is
282/284.

**New:** After a complete green Contracts.Tests run, set Story 10.6 and the reconciliation action to `done`,
synchronize the planning-story manifest, and record the successful gate. Leave Stories 10.7–10.9 and other
Epic 10 action items unchanged.

**Rationale:** Governance should reflect the actual terminal condition and must not overstate broader Epic 10
completion.

## 5. Implementation Handoff

Classification: **Minor — direct Developer implementation**.

Developer responsibilities:

- apply the trace/test/gate reconciliation without touching Story 10.6 runtime code or wire artifacts;
- preserve all owned reference-pending gaps;
- run the focused traceability gate and the complete Contracts.Tests project;
- close only the Story 10.6 blocker when the broad project is green.

Success criteria:

- PRD and epics parse as identical NFR1–NFR73 inventories;
- all 73 trace rows carry the current derived hash, category, evidence, status, and owner;
- NFR1 derives and publishes `1d924a03811d`;
- the conformance suite retains exact cardinality and negative controls;
- the full Contracts.Tests project passes;
- Story 10.6 runtime/wire behavior is unchanged;
- sprint status truthfully closes Story 10.6 while retaining unrelated open work.

## Change Navigation Checklist Record

- [x] 1.1–1.3 Trigger, problem, and evidence identified.
- [x] 2.1–2.5 Epic impact assessed; no epic restructure, new epic, or resequencing required.
- [x] 3.1 PRD reviewed; authoritative clauses preserved.
- [x] 3.2 Architecture reviewed; no decision change required.
- [N/A] 3.3 UX reviewed; no UI/UX impact.
- [x] 3.4 Traceability tests, focused gate/report, and sprint governance identified as secondary artifacts.
- [x] 4.1 Direct adjustment selected: low effort, low risk.
- [N/A] 4.2 Rollback rejected; no implementation rollback is justified.
- [N/A] 4.3 MVP review rejected; product scope is unchanged.
- [x] 4.4 Recommended path selected and justified.
- [x] 5.1–5.5 Proposal, action plan, and Developer handoff completed.
- [x] 6.1–6.2 Proposal reviewed against source history and current artifacts.
- [x] 6.3 Approval recorded from the user's explicit resolution instruction.
- [N/A] 6.4 No epic/story inventory addition, removal, or renumbering is proposed.
- [x] 6.5 Handoff and success criteria defined.

## Execution Outcome

Implementation completed on 2026-08-24 by the Developer handoff recipient.

- PRD and epics were preserved unchanged at 73 identical NFR clauses.
- The traceability bridge, exact-cardinality tests, focused gate, and report were migrated to NFR1–NFR73.
- The focused NFR traceability gate passed 16/16.
- The complete `Hexalith.Folders.Contracts.Tests` project passed 284/284 after final governance updates.
- Story 10.6 and its reconciliation action moved to `done`; the planning manifest records component completion
  without claiming FR58 production closure.
- No runtime, REST, OpenAPI, SDK, CLI, MCP, Dapr-policy, or Story 10.6 implementation file changed.
