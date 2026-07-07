---
title: '11.1 establish refactor baseline and governance pin map'
type: 'chore'
created: '2026-07-07T12:00:00+02:00'
status: 'done'
baseline_revision: '5ce25e47dee8f7e63e7dbe3bcfc3b2e5d777b0b4'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-11-context.md'
  - '{project-root}/fable_Folders_changes.md'
warnings: []
---

<intent-contract>

## Intent

**Problem:** Epic 11 is about deleting or replacing local platform copies, but the current build, test, route, package, workflow, submodule, and governance state is not yet captured against the actual current HEAD. Without that baseline, later refactors can hide unrelated gate drift or stale assumptions from the older audit at `533806b`.

**Approach:** Create a Story 11.1 baseline artifact that records current evidence, pin maps, known blockers, and audit drift before any substantive refactor. Run the focused baseline and governance commands that are safe in this repository, record exact outcomes, and avoid changing product behavior or shared submodule contents.

## Boundaries & Constraints

**Always:** Record the actual current HEAD, branch, clean/dirty state, submodule SHAs, gate/report paths, route/package/workflow pins, and any command blockers with exact commands. Preserve metadata-only diagnostics and root-only submodule policy. Treat `fable_Folders_changes.md` as an audit seed, not as current truth when HEAD or gate output disagrees.

**Block If:** The working tree becomes dirty from unrelated user changes before implementation starts; a required baseline command cannot be run or summarized at all; evidence shows a product behavior change is required to make Story 11.1 pass.

**Never:** Do not refactor production code, do not hand-edit generated client/parity artifacts, do not initialize nested submodules recursively, do not revert unrelated submodule pointer changes, and do not modify files inside `references/` for this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| CURRENT_HEAD_BASELINE | HEAD differs from audit seed `533806b` | Baseline artifact records actual HEAD and explicit drift from `533806b` | No error; stale audit facts are labeled as historical |
| GATE_GREEN | Focused gate command exits 0 | Command, exit status, report path, and relevant counts/pins are recorded | No error expected |
| GATE_BLOCKED | Command fails because of local env/DCP/tooling | Artifact records exact command, exit status, and blocker without fabricating green evidence | Continue unless no baseline evidence can be produced |
| SUBMODULE_DRIFT | `git submodule status` differs from audit notes | Actual SHA map is recorded and unrelated pointer changes are not hidden or reverted | No recursive update; no submodule edits |

</intent-contract>

## Code Map

- `fable_Folders_changes.md` -- Historical audit seed with baseline checklist, duplicate-code inventory, and governance pin map; must be reconciled against current HEAD.
- `_bmad-output/implementation-artifacts/11-1-establish-refactor-baseline-and-governance-pin-map.md` -- New Story 11.1 evidence artifact to create.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- Current Epic 11/story status source to capture; update only if this run completes the story.
- `_bmad-output/gates/*/latest.json` -- Focused gate reports that commands may refresh and the baseline artifact should cite.
- `tests/tools/run-baseline-ci-gates.ps1` -- Baseline build/unit gate and current project-count evidence.
- `tests/tools/run-contract-parity-ci-gates.ps1`, `tests/tools/run-security-redaction-ci-gates.ps1`, `tests/tools/run-safety-invariant-gates.ps1`, `tests/tools/run-governance-completeness-gates.ps1`, `tests/tools/run-dapr-policy-conformance-gates.ps1`, `tests/tools/run-release-package-gates.ps1` -- Focused governance/package evidence lanes.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` -- Canonical solution/reference/dependency inventory lock.
- `.github/workflows/ci.yml`, `.github/workflows/contract-spine.yml`, `.github/workflows/nightly-drift.yml`, `.github/workflows/policy-conformance.yml`, `.github/workflows/release-packages.yml` -- Workflow pin surfaces to map, including submodule-init policy.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `tests/fixtures/parity-contract.json` -- Route/parity operation counts to record without editing.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/implementation-artifacts/11-1-establish-refactor-baseline-and-governance-pin-map.md` -- Create a story evidence artifact with sections for HEAD/branch/tree state, audit-drift notes, submodule SHA map, solution/project/package inventory, route/parity counts, workflow pins, focused gate results, known blockers, and next-story handoff constraints.
- [x] `_bmad-output/gates/*/latest.json` -- Refresh or cite focused gate reports produced by the verification commands; do not sanitize away failures or reference-pending statuses.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- Capture current `epic-11` and `11-1-establish-refactor-baseline-and-governance-pin-map` status in the evidence artifact; do not claim completion before review.
- [x] `tests/tools/run-baseline-ci-gates.ps1` -- Run as evidence and record counts/status in the artifact; do not change the script unless a command itself proves a stale pin that belongs to Story 11.1.
- [x] `.github/workflows/ci.yml`, `.github/workflows/contract-spine.yml`, `.github/workflows/nightly-drift.yml`, `.github/workflows/policy-conformance.yml`, `.github/workflows/release-packages.yml`, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `tests/fixtures/parity-contract.json`, `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` -- Record current workflow, route, parity, release-package, and scaffold pins in the artifact without product-code edits.

**Acceptance Criteria:**
- Given the current repository HEAD, when Story 11.1 baseline evidence is generated, then the artifact records the actual commit, branch, clean/dirty state, submodule SHAs, and explicit drift from audit seed `533806b`.
- Given baseline verification commands run, when each command completes or fails, then the artifact records the exact command, status, report path or blocker, and no pass is fabricated.
- Given package, route, workflow, and scaffold pins exist, when the pin map is written, then it names the files that must move in lockstep during later Epic 11 stories.
- Given known DCP/AppHost and reference-pending issues, when they affect verification, then they are recorded as known blockers rather than hidden by broad skips or unrelated edits.
- Given unrelated submodule pointer changes exist or appear, when the story completes, then those changes are not reverted, hidden, or replaced by recursive submodule initialization.
- Given this is Story 11.1, when implementation completes, then no production refactor, OpenAPI/generated-client hand edit, or `references/` submodule content edit is included.

## Spec Change Log

- 2026-07-07 (implement): `baseline_revision` advanced from `a63aba8` to current HEAD `5ce25e4` (prior runs blocked pre-implementation on a transient dirty tree; tree clean on this run). Factual pin correction recorded in the evidence artifact: the parity oracle fixture is `tests/fixtures/parity-contract.yaml` (no bare `parity-contract.json` exists; the only `.json` sibling is the schema `parity-contract.schema.json`); Code Map/Tasks reference to `parity-contract.json` is read-as-`.yaml`. No `<intent-contract>` change.

## Review Triage Log

### 2026-07-07 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 0, medium 1, low 9)
- defer: 0
- reject: 1
- addressed_findings:
  - `[medium]` `[patch]` `submodules: false` occurrence count was stated as 13 in §6 and §9 while the per-workflow breakdown and grep both yield 12 (ci ×6 + release-packages ×3 + contract-spine ×1 + nightly-drift ×1 + policy-conformance ×1) — corrected both sites to 12 (grep-verified).
  - `[low]` `[patch]` §7 gate #3 build PASS attributed the transient `MSB3883` failure to a specific external IDE build host as fact — reworded to "external cause inferred, not captured" and noted the clean retry was not re-verified in-session.
  - `[low]` `[patch]` §7 blanket "the baseline is deterministic/reproducible" overstated given gates #3/#11/#12a do not reproduce clean standalone — scoped the claim to the passing gates #4/#6/#7/#8/#9/#10.
  - `[low]` `[patch]` §5 `diagnostics_count: 0` sourced from a fixture comment, not an enforced field — relabelled as a documentation-header value (value 0 is correct).
  - `[low]` `[patch]` §5 "mirrored in `Client.Tests`" implied the same `ImplementedRestOperationCount` symbol — corrected to `ParityScenarios.ExpectedOperationCount = 49` at `tests/shared/Parity/ParityScenarios.cs:20` (verified: value mirrored, constant differs).
  - `[low]` `[patch]` §9 pin-map cited an unqualified `TransportParityConformanceTests.cs` (two files share the class name) — qualified with the `Server.Tests/` path plus the Client.Tests mirror note.
  - `[low]` `[patch]` §9 "5 packable … Cli + ServiceDefaults excluded" overloaded "packable" (7 src csproj carry `IsPackable=true`) — clarified as the 5-project release/publish set vs manifest-level exclusion (grep-verified 7 `IsPackable=true`).
  - `[low]` `[patch]` spec change-log wording implied a bare `parity-contract.json` exists — corrected to name the only `.json` sibling `parity-contract.schema.json`.
  - `[low]` `[patch]` §4 import phrasing understated the `$(Hexalith*BuildPackageProps)` MSBuild-property indirection — added the indirection note (250-entry count unchanged/correct).
  - `[low]` `[patch]` §2 `d50b603` labelled "pre-Epic-11" with no structural-impact note — added "structural claims re-verified at HEAD".
  - reject (no action): committed `release-packages/latest.json` reads `passed` while a standalone run fails on stale capacity-calibration evidence — the artifact already discloses this divergence honestly (gate #11 + blocker #2); carried forward as a residual risk, not an artifact defect.

## Design Notes

The evidence artifact should be concise but machine-auditable. Prefer tables with columns `Surface`, `Pinned files`, `Current evidence`, and `Later-story impact` for the governance map. Keep command output summarized; do not paste full logs unless needed to explain a blocker.

## Verification

**Commands:**
- `git rev-parse HEAD && git status --short --branch && git submodule status` -- expected: current pin map captured exactly.
- `dotnet restore Hexalith.Folders.slnx` -- expected: restore succeeds or exact blocker recorded.
- `dotnet build Hexalith.Folders.slnx --configuration Release --no-restore` -- expected: zero warnings/errors or exact blocker recorded.
- `pwsh ./tests/tools/run-baseline-ci-gates.ps1` -- expected: passed gate report or exact blocker recorded.
- `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --filter FullyQualifiedName~ScaffoldContractTests` -- expected: ScaffoldContractTests green.
- `pwsh ./tests/tools/run-contract-parity-ci-gates.ps1 -SkipRestoreBuild` -- expected: passed or exact blocker recorded.
- `pwsh ./tests/tools/run-security-redaction-ci-gates.ps1 -SkipRestoreBuild` -- expected: passed or exact blocker recorded.
- `pwsh ./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild` -- expected: passed or exact blocker recorded.
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- expected: passed or exact blocker recorded.
- `pwsh ./tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` -- expected: passed or exact blocker recorded.
- `pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId $(git rev-parse HEAD)` -- expected: passed or exact blocker recorded.
- `dotnet format whitespace --verify-no-changes` and `dotnet format analyzers --verify-no-changes --severity warn` -- expected: no changes needed or exact blocker recorded.

## Auto Run Result

Status: done

**Summary of implemented change:** Story 11.1 (chore, documentation-only) produced the Epic 11 refactor baseline evidence artifact `11-1-establish-refactor-baseline-and-governance-pin-map.md` at HEAD `5ce25e4`: HEAD/branch/clean-tree state; drift vs audit seed `533806b` (4 commits ahead; EventStore/Memories/Tenants submodule bumps preserved, not reverted); 8-module non-recursive SHA map; solution/project/package inventory (50 `.slnx` projects, central package management via the Builds submodule); route/parity counts (49 REST operations / 49 parity ops); workflow pins (5 workflows, `submodules: false` ×12); focused gate results; known blockers; an 8-row governance pin map naming lockstep files for later Epic 11 stories; next-story handoff constraints; and captured sprint status (`epic-11`/`11-1` = `backlog`, not flipped). No product code, generated artifacts, or `references/` content changed.

**Files changed:**
- `11-1-establish-refactor-baseline-and-governance-pin-map.md` (new) — baseline evidence + governance pin-map artifact.
- `spec-11-1-establish-refactor-baseline-and-governance-pin-map.md` (modified) — `baseline_revision`→`5ce25e4`, five Execution tasks marked done, Spec Change Log + Review Triage Log + this Auto Run Result; `status`→`done`.

**Review findings breakdown:** 10 patches applied (1 medium: `submodules: false` count 13→12 in §6/§9; 9 low: build-PASS external-cause attribution softened, reproducibility claim scoped to passing gates, `diagnostics_count` framing, Client.Tests parity-constant name `ParityScenarios.ExpectedOperationCount`, §9 unqualified test path qualified, "packable" vs `IsPackable=true` (7) clarified, change-log `.json` wording, import-indirection note, `d50b603` structural-impact note). 0 deferred. 1 rejected (committed `release-packages/latest.json` `passed` vs standalone fail — already honestly disclosed in the artifact). No intent_gap / bad_spec → no re-derivation loopback (`review_loop_iteration` = 0).

**Follow-up review recommendation:** false — all fixes are localized, tree-verified factual/wording corrections to a documentation-only artifact with no behavior/API/security/data impact.

**Verification performed:** restore PASS; `build -c Release` 0W/0E (clean retry after a transient concurrent-build file-lock); baseline-ci PASS (9 unit-test projects); ScaffoldContractTests 10/0/0; contract-parity, security-redaction, safety-invariants, governance-completeness, dapr-policy gates PASS; format analyzers PASS; `release-package` and solution-root `format whitespace` BLOCKED for env/ordering reasons (recorded honestly, not masked). Post-review re-verification: no residual `×13`; corrected counts grep-verified (12); `ParityScenarios.ExpectedOperationCount` and 7×`IsPackable=true` confirmed; working tree scoped to the two `_bmad-output` files only.

**Residual risks:**
- `run-release-package-gates.ps1` fails standalone on `stale-capacity-calibration-evidence` (CI regenerates capacity-calibration in-job first); committed `release-packages/latest.json` reads the CI-representative `passed`; packages pack clean.
- Solution-root `dotnet format whitespace` flags only `references/**` submodule EOL (0 Folders-owned files; `references/` out of scope).
- DCP/AppHost live-boot (Epic 10 AC9) remains env-blocked; AppHost.Tests skip cleanly (Tier-3).
- Reference-pending NFR rows C3/C4/C7/C12 remain hard-pinned as owned, surfaced gaps.
