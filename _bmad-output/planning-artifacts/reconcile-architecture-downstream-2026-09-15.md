---
date: 2026-09-15
workflow: bmad-architecture (update)
source_authority: _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-15.md
applied_to: _bmad-output/planning-artifacts/architecture.md
status: architecture-applied; C6 gate lockstep applied and green; remaining downstream lockstep owed
freeze: retained
---

# Downstream Reconciliation — Architecture Update 2026-09-15

`architecture.md` has been amended to apply §5.2 of the approved 2026-09-15 planning-authority
relock. This note records what that amendment now obliges elsewhere. **It changes nothing outside
`architecture.md`.** Every item below is owed work, not completed work.

## 1. What the architecture now asserts

| § | New / amended | Nature |
| --- | --- | --- |
| Release Authority Overlay — 2026-09-15 | new section | Four-way authority split; execution-wave table; superseded-digest table; target-state reality table |
| Durable Data Plane — Epic 12 | amended | Story 12.6 added; sequencing re-pointed at execution ranks |
| Security & Operational Hardening — Epic 13 | amended | NFR74–NFR84 band admission; per-row ownership deferred to `nfr-traceability.md`; OQ12/OQ13 |
| S-4, S-6, A-8 | amended in place | Evaluation order; correlation tokens at event-write time; required `visibility` |
| **S-7, S-8** | **new decisions** | Denial envelopes; derived scope metadata |
| Workspace State Transition Matrix (C6) | amended | 8 transition rows changed/added; 7-rule PD11 block; C3 cleanup authority |
| Exit criteria C3 / C6 / C9 + ops plan | amended | Marked superseded / approval-pending |

Decision IDs are stable. `D-`, `A-1`–`A-11`, `S-1`–`S-6`, `C-`, `F-`, `I-` keep their meanings;
`S-7` and `S-8` are the only new IDs.

## 2. C6 gate lockstep — applied and green

The PD11 rows took the C6 matrix from 34 to 41 positive transition edges, which failed
`ConsumerDocsConformanceTests.WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix`. Scope was
extended (Jerome, 2026-09-15) to land the lockstep in the same change set:

- `docs/diagrams/workspace-lifecycle.md` — 7 new edges (`changes_staged`→`dirty` on `CommitFailed`;
  `changes_staged`→`inaccessible` on the three revocation events; `dirty`→`changes_staged`;
  `dirty`→`ready` on `LockLeaseBecameStale`; `inaccessible`→`dirty`), the `dirty` disposition row and
  state label, and a guard note recording that four pairs are guard-discriminated and the diagram
  cannot show the guard.
- `docs/exit-criteria/c6-transition-matrix-mapping.md` — `dirty` disposition, and
  `LockLeaseBecameStale` added to the event vocabulary (23 → 24).
- `ConsumerDocsConformanceTests.cs` — pinned counts 34 → 41 and 23 → 24.

**Verified:** `Hexalith.Folders.Contracts.Tests` 314/314 pass; `DispositionLabelMapperTests` 37/37 pass;
`ExitCriteriaDecisionArtifactTests` passes. `Hexalith.Folders.Testing.Tests` has 3 failures, all
`ScaffoldContractTests` (`Hexalith.Folders.EventStore` project / `.slnx` / build-config drift) —
pre-existing and unrelated to this change set.

**Still code-side target state, not touched here:** `FolderStateTransitions.cs:157` maps
`Dirty => AwaitingHuman` and `FolderStateTransitionsTests.cs:193` pins it, so the code still carries the
pre-PD11 unconditional disposition. `unknown_provider_outcome` remains `awaiting-human` in the mapping
doc and diagram against the architecture's `auto-recovering` — pre-existing drift that PD11 rule 5
resolves, left for the owning story with `DispositionLabelMapper.cs`.

## 3. Lockstep owed, by owner

### Already done (verified, not by this run)
- `prd.md` — all eleven §5.1 edits, including NFR74–NFR84.
- `docs/exit-criteria/nfr-traceability.md` — 84 rows, two new categories, all `reference-pending`.
- `tests/.../NfrTraceabilityConformanceTests.cs` — `NfrTotal` 73→84, category ranges added.
- `tests/tools/run-nfr-traceability-gates.ps1`.

### Owed — Architecture / Security
- **Contract Spine + `docs/contract/authorization-matrix.md`** — the ten §7.2 rules, matrix gaps
  `G1`–`G11` including `G4`; then regenerate OpenAPI, generated client, CLI/MCP parity fixtures,
  `previous-spine.yaml`, C13 inventory, docs, tests. **Reapprove OQ3 against the new digest (A6b).**
  The superseded digest is `5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7`.
- **`previous-spine.yaml` drift fixture** — it declares `known_omissions: No status-code surface`, so
  it will *not* catch the removal of 403 and the three error codes. Add a status-code and
  error-vocabulary surface, or the breaking change ships unguarded.
- **`FolderAuthorizationDenialMapper`** — collapse the non-canonical denial categories into S-7.
- **`FolderStateTransitions.cs`**, the lifecycle
  tests, `docs/diagrams/workspace-lifecycle.md`, and `DispositionLabelMapper.cs` — one model, per §2.
- **`FolderWorkspaceLifecycleEvent`** — `LockLeaseBecameStale` is a published OpenAPI enum member, so
  adding it touches the spine and generated client, not only this document.
- **C9 correlation-token evidence** — the pinned HMAC derivation, the shared write-path tokenizer, and
  the proof that no durable cleartext survives.

### Owed — Legal / Product / Security
- **`docs/exit-criteria/c3-retention.md`** — the terminal-task-closure trigger and the seven-day
  window (A7b), plus the unreachable-window conflict in §4 below.

### Owed — Governance
- **`docs/exit-criteria/c0-c13-governance-evidence.yaml`** — it currently cannot express supersession:
  no `pending` status exists, `ApprovalBackedCriteriaCarryFreshExactApprovalRecords` pins C3 to
  `approved`, and `GovernanceEvidenceReferencePendingCriteriaStaySurfaced` asserts no criterion is
  `reference_pending`. Extend the vocabulary with `superseded-pending-reapproval` and teach both tests
  to accept it — YAML + schema + tests in one commit. The `{C3, C4, C7, C12}` NFR hard-pin is
  untouched and **no NFR row changes status.**

### Owed — Product / Delivery
- **`epics.md`** — §5.4 beyond the NFR mirroring that is already done: retitle Story 10.9, execution
  ranks and prerequisites on 4.18–4.21 / 6.12–6.14 / 10.8 / 12.1–12.6, lifecycle ACs to §7.3, the
  84-count. **And a story that owns the PD10 spine correction — there is none today.** `epics.md` has
  zero references to PD8, PD10, or PD11, while Story 13.2 owns NFR76 and will otherwise build a
  second deny-by-default shape beside S-7.
- **`planning-story-manifest.yaml`** — not regenerated; still `generated_on: '2026-08-04'` with no
  `execution_rank`, `execution_waves`, `story_lifecycle_status`, or OQ11–OQ13. Until it carries them,
  the architecture's wave table is transitional authority that the manifest supersedes.
- **`sprint-status.yaml`** — Story 10.8 still `done` (canonical: `in-progress`); Story 10.9 still
  `review` (canonical: `done`, only after the title/scope correction).
- **`ux-design-specification.md`** — untouched since 2026-07-07; §5.3 owes the confidential-override
  correlation reference, the four distinct value states, `unknown_provider_outcome` as auto-recovering,
  non-disclosing authority/resource-unavailable errors, and refreshed provenance.

## 4. Open items the architecture records but cannot settle

1. **§7.1's rank rule contradicts its own table.** Six equal-rank prerequisites (12.2/12.3/12.6 behind
   12.1 at rank 10; 4.21 behind 4.19–4.20 and 6.14 behind 6.12–6.13 at rank 30). 3.11, 3.13, 10.6,
   10.7, 11.15 and **all of Epic 13** are unranked while rank 40 depends on Epic 13. Architecture's
   assumption: rank orders waves, prerequisite edges order work within a wave. → Delivery + PM.
2. **PD8 vs the operational path.** The canonical serializing identity, the Story 12.4 Git executor,
   and NFR79 restart recovery all need the real ref, not a token. Architecture's assumption:
   tokenization covers evidence and observability surfaces only. → Security + Architecture, in C9.
3. **The C3 staged-content window is unreachable as written.** PD11 rule 3 gates recovery on "within
   the C3 window", but that window starts at terminal task closure with no active task — and an
   `inaccessible` workspace still has a live task. → Legal + Product + Security, in A7b.
4. **Migration for a breaking wire change.** A-11 says a breaking change gets a new major version;
   PD10 removes caller-visible error codes and retires 403. `v2` or an in-place `v1` security
   exception, the deprecation window, and the client-rollout order are all undecided. → Architecture + PM.

## 5. Freeze

Unchanged. §9 of the approved proposal governs release; nothing in this amendment lifts it, and the
role-specific attestations for A1–A8 remain required inputs.
