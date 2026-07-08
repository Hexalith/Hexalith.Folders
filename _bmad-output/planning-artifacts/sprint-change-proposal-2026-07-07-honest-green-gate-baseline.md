# Sprint Change Proposal — Honest-Green UI E2E + Accessibility Gate Baseline

- **Date:** 2026-07-07
- **Author:** Amelia (Developer) via `bmad-correct-course`
- **Change scope classification:** Moderate (backlog/AC reorganization + one new conformance test; no PRD/architecture/MVP change)
- **Mode:** Incremental
- **Status:** Approved and applied

---

## Section 1 — Issue Summary

**Problem statement.** The open Epic 8 action item — *"Preserve full UI E2E and accessibility gates as part of the honest-green baseline; keep no-filter and forbidden-substring conformance checks intact when CI changes"* (owner Murat / Amelia, priority high) — had **no durable enforcement anchor in the governance register**, even though the runtime enforcement already exists and is green.

**Discovery context.** The "when CI changes" risk is live and imminent: **Epic 11 (in-progress)** is a platform-focus refactor whose stories deliberately touch CI and gate wiring — **11.3** (apply wire-preserving repo hygiene and *fragile-gate fixes*), **11.7** (consolidate test helpers → moves the FQNs the CI-workflow conformance tests pin), **11.11** (harden UI below the shell → lives next to `Hexalith.Folders.UI.E2E.Tests`), and **11.13** (final cleanup + verification).

**Evidence.** The four gate invariants are enforced today by conformance tests in `tests/Hexalith.Folders.Contracts.Tests/Deployment/`:

| Invariant | Enforcer (green today) |
| --- | --- |
| Full 63-test UI E2E lane | `e2e-gates` job (`ci.yml`) + `run-e2e-ci-gates.ps1` + `E2eCiWorkflowConformanceTests` |
| Accessibility (axe / WCAG 2.2 AA) | `accessibility-gates` job + `run-accessibility-ci-gates.ps1` + `AccessibilityCiWorkflowConformanceTests` + `ConsoleAxeWcagGateTests` |
| **No-filter** (lane never narrowed) | `E2eGateScriptProvisionsBrowserRunsFullLaneAndFailsClosed` — `run-e2e-ci-gates.ps1` must not contain `--filter` / `-namespace` / `FullyQualifiedName`; report `scope=full-ui-e2e-lane` |
| **Forbidden-substring (AD7)** | `*WorkflowAvoidsForbiddenInfrastructureSubstrings` — `ci.yml` + gate scripts must not contain `upload-artifact` / `secrets.` / `services:` / `dotnet publish` / `docker` / `playwright install` / `--recursive` |

**The precise gap.** Story 11.1's governance pin map (the register the refactor consults) under-specified this surface:
- The **"Workflow / gate wiring"** row named only `ContractParityCiWorkflowConformanceTests` + `SecurityRedactionCiWorkflowConformanceTests` — not the two E2E/accessibility conformance classes.
- The **"E2E lane"** row pinned the *scripts* + `ScaffoldContractTests` but named neither the no-filter nor the forbidden-substring invariant, nor the conformance classes that enforce them.

So a CI change in 11.3/11.7/11.11/11.13 could weaken or delete these enforcers without tripping a pin-map row, and no Epic 11 story carried an explicit AC to preserve them.

---

## Section 2 — Impact Analysis

- **Epic impact.** No epic added, removed, or resequenced. Epic 8 remains `done` — this closes its last open action item. Epic 11 remains in-progress; four of its stories gain one acceptance criterion each.
- **Story impact.** Story 11.1 (evidence artifact / pin map): two rows strengthened + one §10 handoff constraint. Stories 11.3, 11.7, 11.11, 11.13: one new AC each.
- **Artifact conflicts.** PRD **N/A** (no FR/scope change). Architecture **N/A** (gate topology unchanged). UI/UX **N/A**. Impacted artifacts: `11-1-…-governance-pin-map.md`, `epics.md`, `sprint-status.yaml`, plus one new test file.
- **Technical impact.** One new consolidated conformance test (`HonestGreenGateBaselineConformanceTests`). No production code, no CI-workflow change, no gate-script change. Build clean (0 warnings); 4/4 new tests pass; 42/42 across the CI-workflow conformance classes.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (Hybrid).** Effort **Low**, risk **Low**.

Rationale: the enforcement already exists and is green; the only real gap is that the *governance register* and *Epic 11 story ACs* did not explicitly protect it. The hybrid combines (a) pinning the existing enforcers + naming the two invariants in the register, (b) placing explicit "gates survive CI change" ACs on the four CI-touching Epic 11 stories, and (c) a new consolidated belt-and-suspenders guard so a single weakened class cannot silently pass. Rollback (Option 2) and MVP review (Option 3) are not applicable — nothing is being reverted and MVP scope is untouched.

---

## Section 4 — Detailed Change Proposals (all applied)

**Story 11.1 governance pin map** (`_bmad-output/implementation-artifacts/11-1-establish-refactor-baseline-and-governance-pin-map.md`)
1. **"Workflow / gate wiring" row** — now enumerates all five CI-workflow conformance classes (`Baseline`, `ContractParity`, `SecurityRedaction`, `E2e`, `Accessibility`) + the new `HonestGreenGateBaselineConformanceTests`, notes the two blocking `ci.yml` gate jobs, and adds *"No CI change may drop a gate job, weaken an assertion, or delete a conformance class."*
2. **"E2E lane" row → recast as "UI E2E + accessibility honest-green gate baseline"** — names the two pinning conformance classes and both must-hold invariants: **(a) no-filter** and **(b) forbidden-substring (AD7)**, with the concrete forbidden set.
3. **§10 handoff constraints** — new bullet binding the baseline to 11.3/11.7/11.11/11.13: gates may be re-homed but never weakened or masked.

**Epic 11 acceptance criteria** (`_bmad-output/planning-artifacts/epics.md`)
4. **Story 11.3** — AC: honest-green baseline preserved when CI changes (both jobs present+blocking, full-63 un-narrowed, AD7 absent, conformance classes green); explicitly closes this Epic 8 action item.
5. **Story 11.7** — AC: test-helper moves don't weaken the baseline; any conformance-class / `UI.E2E.Tests` rename moves its FQN pins in the same commit.
6. **Story 11.11** — AC: UI hardening below the shell doesn't shrink accessibility/E2E coverage (all 63 cases, no `--filter`, no skipped Accessibility cases).
7. **Story 11.13** — AC: final verification confirms the baseline end-to-end or blocks with evidence, never silently relaxes it.

**New consolidated guard test** (`tests/Hexalith.Folders.Contracts.Tests/Deployment/HonestGreenGateBaselineConformanceTests.cs`)
8. Four `[Fact]`s: (1) both `e2e-gates` + `accessibility-gates` jobs present and blocking (no `continue-on-error: true`; both gate scripts invoked); (2) the E2E gate script never narrows the full lane; (3) `ci.yml` + both gate scripts free of the AD7 set + `--recursive`; (4) compile-time `typeof` references to all five sibling conformance classes (deletion → build break) + reflective assertions that the two UI classes keep their defining `[Fact]` methods. **Verified GREEN** (0 warnings; 4/4; 42/42 across CI-workflow conformance classes).

**Sprint status** (`_bmad-output/implementation-artifacts/sprint-status.yaml`)
9. Epic 8 action item flipped `open → done` with a resolution note; `last_updated` header appended.

---

## Section 5 — Implementation Handoff

**Scope: Moderate — but already implemented in this session.** All nine edits are applied and verified; no further dev handoff is required to close the action item.

- **Developer (Amelia):** completed the pin-map, epics AC, sprint-status, and new-test edits; verified build + focused test run green.
- **Test Architect (Murat):** co-owner — the new `HonestGreenGateBaselineConformanceTests` + the four existing conformance classes are the durable enforcement; the Epic 11 ACs will re-exercise them when 11.3/11.7/11.11/11.13 run their normal gate lanes.
- **Follow-through:** the ACs on 11.3/11.7/11.11/11.13 are the live obligation — each of those stories, when developed, must keep this suite green (it rides their existing `ci.yml` gate runs; no new lane needed).

**Success criteria (met):** action item closed; governance register + Epic 11 ACs explicitly protect the gates; new guard authored and green; no CI-workflow or gate-script behavior changed.

---

## Checklist (Change Navigation)

- **§1 Trigger & context** — [x] Trigger = open Epic 8 action item; core problem = governance-register gap under imminent Epic 11 CI-refactor risk.
- **§2 Epic impact** — [x] No epic added/removed/resequenced; four Epic 11 stories gain one AC each.
- **§3 Artifact conflicts** — [x] PRD/Architecture/UX **N/A**; pin map + epics + sprint-status + one new test impacted.
- **§4 Path forward** — [x] Option 1 Direct Adjustment (Hybrid); Options 2/3 not viable/applicable.
- **§5 Proposal components** — [x] all sections authored.
- **§6 Final review & handoff** — [x] user approved (`a`) all nine edits; sprint-status updated; handoff = self-contained (already implemented).
