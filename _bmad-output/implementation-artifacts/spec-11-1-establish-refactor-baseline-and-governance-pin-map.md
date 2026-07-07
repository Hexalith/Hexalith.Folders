---
title: '11.1 establish refactor baseline and governance pin map'
type: 'chore'
created: '2026-07-07T12:00:00+02:00'
status: 'in-progress'
baseline_revision: 'a63aba85cba9832013d8e2740601966c03c454c2'
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
- [ ] `_bmad-output/implementation-artifacts/11-1-establish-refactor-baseline-and-governance-pin-map.md` -- Create a story evidence artifact with sections for HEAD/branch/tree state, audit-drift notes, submodule SHA map, solution/project/package inventory, route/parity counts, workflow pins, focused gate results, known blockers, and next-story handoff constraints.
- [ ] `_bmad-output/gates/*/latest.json` -- Refresh or cite focused gate reports produced by the verification commands; do not sanitize away failures or reference-pending statuses.
- [ ] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- Capture current `epic-11` and `11-1-establish-refactor-baseline-and-governance-pin-map` status in the evidence artifact; do not claim completion before review.
- [ ] `tests/tools/run-baseline-ci-gates.ps1` -- Run as evidence and record counts/status in the artifact; do not change the script unless a command itself proves a stale pin that belongs to Story 11.1.
- [ ] `.github/workflows/ci.yml`, `.github/workflows/contract-spine.yml`, `.github/workflows/nightly-drift.yml`, `.github/workflows/policy-conformance.yml`, `.github/workflows/release-packages.yml`, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `tests/fixtures/parity-contract.json`, `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` -- Record current workflow, route, parity, release-package, and scaffold pins in the artifact without product-code edits.

**Acceptance Criteria:**
- Given the current repository HEAD, when Story 11.1 baseline evidence is generated, then the artifact records the actual commit, branch, clean/dirty state, submodule SHAs, and explicit drift from audit seed `533806b`.
- Given baseline verification commands run, when each command completes or fails, then the artifact records the exact command, status, report path or blocker, and no pass is fabricated.
- Given package, route, workflow, and scaffold pins exist, when the pin map is written, then it names the files that must move in lockstep during later Epic 11 stories.
- Given known DCP/AppHost and reference-pending issues, when they affect verification, then they are recorded as known blockers rather than hidden by broad skips or unrelated edits.
- Given unrelated submodule pointer changes exist or appear, when the story completes, then those changes are not reverted, hidden, or replaced by recursive submodule initialization.
- Given this is Story 11.1, when implementation completes, then no production refactor, OpenAPI/generated-client hand edit, or `references/` submodule content edit is included.

## Spec Change Log

## Review Triage Log

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
