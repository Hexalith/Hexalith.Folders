---
title: 'Refresh the Story 1.17 Platform qualification proposal'
type: 'chore'
created: '2026-10-17'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The dated packet refuses two changed source bindings, masking mutation assertions. Builds 4.32.0 publication verifies; Platform acceptance remains absent.

**Approach:** Create a separate current offline proposal with reconciled dependencies/security constraints and independently runnable safety tests.

## Boundaries & Constraints

**Always:** Work in Folders. Preserve historical packet/receipt/approval bytes, candidate manifests and tracker. Retain HOLD, V1Only, unset T0, fourteen unexecuted staging checks and unconfirmed first-install scope. Distinguish local source observations from artifact provenance and Platform acceptance.

**Never:** Change dependencies/gitlinks, accept a tool/range, execute staging, publish, deploy, stage/commit/push, initialize nested submodules, or change the approved migration scope. Story 1.17 remains open/backlog.

## I/O & Edge-Case Matrix

| Scenario | Input | Expected behavior | Failure |
|----------|-------|-------------------|---------|
| Current proposal | Reviewed bindings | Consistency only; HOLD and zero execution | Bounded diagnostic |
| Source drift | Missing/changed allowlisted input | Refuse consistency | Relative source path |
| Activation claim | Route, authority, T0 or completed check | Refuse edited proposal | Specific diagnostic |
| Release/source mismatch | Published versus local Builds | Separate identities, unresolved admission | Never substitute artifacts |

</frozen-after-approval>

## Code Map

- `eng/story117-platform-qualification/`: immutable configuration, checks, verifier and thirteen mutation tests. Two of 25 hashes differ: Builds catalog and Platform spine.
- `eng/story117-builds-publication/verify.py`: passing publication proof. Its receipt protects 42 files including the old packet; preserve them.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: local EventStore 3.124.0 versus published Builds 4.32.0's 3.123.0 and old packet's 3.121.0. Change proposal data only.
- Platform's `ARCHITECTURE-SPINE.md` path is in old sourceInputs. AD-9 adds canonical hosting, local bindings, mTLS, deny-default ACLs and declarative subscriptions; include these without claiming live enforcement.
- `references/Hexalith.Platform/docs/implementation/story-1-17-admission-preparation-2026-10-16.json`: historical handoff. Platform Stories 1.8/1.9, tool manifest, intake, owner releases/attestations and hosting remain prerequisites.
- Existing `spec-1-17-complete-pd10-v2-migration-and-closure.md` in this directory remains in progress with its frozen scope unchanged.

## Tasks & Acceptance

Bare task filenames resolve under `eng/story117-platform-qualification-current/`; the evidence path is repository-relative.

**Execution:**
- [ ] `configuration.proposed.json` -- record current canonical revisions/actual source states, all 25 bindings and observed catalog selections. Distinguish published Builds from local catalog inputs; retain unresolved admission/operational fields.
- [ ] `staging-checks.json` -- retain fourteen finite, owned, unexecuted checks; reconcile AD-9 requirements without granting execution authority.
- [ ] `verify.py` -- check allowlisted sources, strict JSON/types, proposal controls and dependency consistency offline. Accept no arguments granting execution/approval; use no subprocess/network calls.
- [ ] `test_verify.py` -- cover every matrix row and existing authority, check-removal and disclosure guards. Use controlled disposable source fixtures for mutation assertions; reset each subtest even on failure. Separately prove current-source binding and missing/changed-source refusal.
- [ ] `README.md` -- document new commands, reviewed deltas, historical supersession limits and exact remaining owner prerequisites.
- [ ] `_bmad-output/implementation-artifacts/evidence/story-1-17-current-qualification-2026-10-17.json` -- retain exact commands/results, source/proposal hashes and preservation results. Label historical refusals accurately.

**Acceptance Criteria:**
- Given current inputs, when the new diagnostic/tests run, then both pass with HOLD and zero staging execution.
- Given the historical publication receipt, when its existing verifier runs afterward, then its protected inputs still verify unchanged.
- Given absent Platform acceptance or operational evidence, when the proposal is inspected, then it leaves those prerequisites unresolved and Story 1.17 open.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Run Python 3 with PYTHONDONTWRITEBYTECODE=1:
- `python3 eng/story117-platform-qualification-current/verify.py`
- `python3 -m unittest discover -s eng/story117-platform-qualification-current -p 'test_*.py' -v`
- `python3 eng/story117-builds-publication/verify.py`
- `git diff --check`

No .NET build or Aspire lifecycle applies. Historical validation exits 1 on the Builds fingerprint; 13 tests report 21 failures and 8 errors. Preserve those results.
