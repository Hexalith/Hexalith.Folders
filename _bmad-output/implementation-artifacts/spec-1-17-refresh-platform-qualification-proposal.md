---
title: 'Refresh the Story 1.17 Platform qualification proposal'
type: 'chore'
created: '2026-10-17'
baseline_commit: 'd06799dd70334366dddaee6ace6b9a472f78c93e'
status: 'done'
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
- `eng/story117-builds-publication/verify.py`: passing historical publication replay; current-checkout snapshot mismatch remains recorded. Its receipt protects 42 files including the old packet; preserve them.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: local EventStore 3.124.0 versus published Builds 4.32.0's 3.123.0 and old packet's 3.121.0. Change proposal data only.
- Platform's `ARCHITECTURE-SPINE.md` path is in old sourceInputs. AD-9 adds canonical hosting, local bindings, mTLS, deny-default ACLs and declarative subscriptions; include these without claiming live enforcement.
- `references/Hexalith.Platform/docs/implementation/story-1-17-admission-preparation-2026-10-16.json`: historical handoff. Platform Stories 1.8/1.9, tool manifest, intake, owner releases/attestations and hosting remain prerequisites.
- Existing `spec-1-17-complete-pd10-v2-migration-and-closure.md` in this directory remains in progress with its frozen scope unchanged.

## Tasks & Acceptance

Bare task filenames resolve under `eng/story117-platform-qualification-current/`; the evidence path is repository-relative.

**Execution:**
- [x] `configuration.proposed.json` -- record current canonical revisions/actual source states, all 25 bindings and observed catalog selections. Distinguish published Builds from local catalog inputs; retain unresolved admission/operational fields.
- [x] `staging-checks.json` -- retain fourteen finite, owned, unexecuted checks; reconcile AD-9 requirements without granting execution authority.
- [x] `verify.py` -- check allowlisted sources, strict JSON/types, proposal controls and dependency consistency offline. Accept no arguments granting execution/approval; use no subprocess/network calls.
- [x] `test_verify.py` -- cover every matrix row and existing authority, check-removal and disclosure guards. Use controlled disposable source fixtures for mutation assertions; reset each subtest even on failure. Separately prove current-source binding and missing/changed-source refusal.
- [x] `README.md` -- document new commands, reviewed deltas, historical supersession limits and exact remaining owner prerequisites.
- [x] `_bmad-output/implementation-artifacts/evidence/story-1-17-current-qualification-2026-10-17.json` -- retain exact commands/results, source/proposal hashes and preservation results. Label historical refusals accurately.

**Acceptance Criteria:**
- Given current inputs, when the new diagnostic/tests run, then both pass with HOLD and zero staging execution.
- Given the historical publication receipt and captured Platform snapshot, when the unchanged publication verifier runs against a disposable reconstruction of that snapshot, then it passes all 4,888 captured Platform hashes, while all 42 protected workspace files and the 45 current-run preservation-baseline inputs remain unchanged. Record the current-checkout verifier failure separately as pre-existing snapshot drift; it is not a refresh acceptance gate.
- Given absent Platform acceptance or operational evidence, when the proposal is inspected, then it leaves those prerequisites unresolved and Story 1.17 open.

## Implementation Notes

Implemented the five-file current packet in `eng/story117-platform-qualification-current/`, the requested evidence receipt and additive LF attributes for byte-stable new artifacts. The historical packet, receipts, approvals, candidate manifests, tracker, dependencies and gitlinks remain unchanged.

All 25 actual source bindings match. Current clean dependency snapshots and local EventStore `3.124.0` catalog selection remain separate from published Builds `4.32.0`, its canonical source revision and observed `3.123.0` catalog selection. AD-9 requirements and outstanding Platform/module/operational prerequisites are recorded without claiming adoption or acceptance.

The parent independently ran the new diagnostic and all 39 tests successfully after review patches and checked the five proposal hashes, 25 source hashes and 45 preserved baseline inputs. All four matrix scenarios have passing coverage. HOLD, V1Only, unset T0, unconfirmed first-install scope and fourteen unexecuted checks remain enforced.

**Current-checkout observation:** The unchanged publication verifier against the current checkout exits 1 at `references/Hexalith.Platform/DaprComponents/accesscontrol.eventstore-admin.yaml`. Before this refresh, the Platform gitlink update left 45 of the historical receipt's 4,888 captured files different (including an absent path). All 42 protected workspace files match. A disposable historical replay using the supported `--workspace` option passes all 4,888 hashes after restoring exactly those 45 blobs from retained canonical Platform revision `a29daf6b1bb3471dded2196cf17064aa2b632554`; current checkout bytes remain untouched. This separately labelled historical replay does not make the current-checkout command pass; that failure remains an explicit observation.

On 2026-10-17 the user approved the recommended acceptance basis: historical publication replay plus unchanged protected files and current-run preservation checks, alongside the passing current-source diagnostic and safety tests. This accepts the bounded refresh only. Current Platform qualification remains separate work with its existing owner prerequisites unresolved. The original closure spec and Story 1.17 `backlog` remain unchanged. Implementation acceptance and all three parent review layers are complete. The implementation agent fixed all five shared-root groups; the parent inspected the patches and reran the focused verification successfully.

## Spec Change Log

- 2026-10-17: Completed the bounded refresh after all three review layers, five direct patch groups and final 39-test/preservation/replay verification. Preserved `tracker_policy: no-automatic-transition` and the frozen prohibition on staging/committing/pushing; no local commit or tracker transition is part of this completion.

- 2026-10-17: User approved the recommended historical-replay/preservation acceptance basis with "apply recommended". Clarified acceptance criterion 2 without changing frozen intent, command outcomes, historical receipts, tool/range acceptance, Platform HOLD or Story 1.17 backlog. Current Platform qualification remains separate owner work.

- 2026-10-17: Captured baseline and implementation results. Recorded the newly discovered pre-existing publication snapshot blocker without editing frozen intent, weakening the verifier or replacing historical evidence. Added LF attributes solely for the new byte-bound artifacts.

## Review Triage Log

- Acceptance gate resolved by the user-approved historical-replay/preservation basis. All three review layers returned; finding-floor arithmetic is informational, not a defect.

| Finding | Verdict | Evidence | Route |
|---------|---------|----------|-------|
| Blind B1: secret-name substring accepted | medium | Parent reproduced `human-receipt.sourceName="a"` passing. An owner could follow an undeclared secret name. | patch: exact reviewed secret mapping |
| Blind B2: secret key/consumer/target associations unchecked | medium | Parent reproduced wrong key, unrelated consumer and wrong target passing together. This can misdirect credential integration. | patch: exact reviewed secret mapping |
| Blind B3: unrelated allowlisted module source accepted | medium | Parent reproduced folders-server attributed to the Platform spine passing. That file does not define its module settings. | patch: exact module source |
| Blind B4: AD-9 requirements replaceable by contradictory text | medium | Parent reproduced disabled mTLS/allow-all and programmatic-only text passing. The proposal then contradicts its reviewed hosting/security requirements. | patch: bind reviewed requirement content |
| Blind B5: first-install branch rule replaceable | medium | Parent reproduced an always-first-install rule despite existing installation passing. It contradicts the retained migration boundary. | patch: bind reviewed requirement content |
| Blind B6: recovery coverage restrictions replaceable | medium | Parent reproduced unconditional outage coverage passing. This overstates the accepted RTO envelope. | patch: bind reviewed requirement content |
| Blind B7: tool acceptance prerequisite replaceable | medium | Parent reproduced the Story 1.9 requirement saying no accepted tool is required while verification passes. It misstates remaining prerequisites. | patch: bind reviewed requirement content |
| Blind B8: staging safeguards reduced to nonempty placeholders | medium | Parent reproduced isolation preconditions, procedure and evidence reduced to `["x"]` passing. The authored checks lose substantive qualification safeguards. | patch: bind reviewed requirement content |
| Blind B9: internal source symlink redirects beyond allowlist | medium | Parent reproduced an allowlisted source linked to an unallowlisted same-byte file passing. This violates the documented explicit source-read boundary. | patch: refuse source redirection |
| Blind B10: special-file input can block indefinitely | medium | Parent created an allowlisted FIFO; a bounded child validation timed out at one second. A malformed input can hang the diagnostic. | patch: require regular packet/source files |
| Edge E1: secret binding metadata unchecked | medium | Same demonstrated mapping defect as B1/B2; parent reproduced it independently. | patch: same secret-mapping group |
| Edge E2: module-to-source association unchecked | medium | Same demonstrated provenance defect as B3; parent reproduced it independently. | patch: same module-source group |
| Verification V1: stale canonical revision lacks regression coverage | medium | Pre-verified reviewer bypassed only the canonical comparison in memory: all 30 tests still passed and local Builds could be attributed to the historical published revision. | patch: add stale-revision mutations |

All findings are verified or pre-verified. They group into secret mappings, module sources, reviewed requirement content, source/file boundaries and canonical-revision test coverage. Each has a direct correction without a new public interface or operational authority. All five groups are now fixed by direct checks and passing regression tests. The parent inspected the patch diff, retained every original command result, refreshed current artifact hashes and reran the focused verification: 39 tests pass. No findings remain unresolved or deferred.

## Verification

Run Python 3 with PYTHONDONTWRITEBYTECODE=1:
- `python3 eng/story117-platform-qualification-current/verify.py`
- `python3 -m unittest discover -s eng/story117-platform-qualification-current -p 'test_*.py' -v`
- `python3 eng/story117-builds-publication/verify.py`
- `git diff --check`

No .NET build or Aspire lifecycle applies. Historical validation exits 1 on the Builds fingerprint; 13 tests report 21 failures and 8 errors. Preserve those results.

### Initial observed results (2026-10-17)

All Python commands used `PYTHONDONTWRITEBYTECODE=1`. The full command streams, hashes, snapshot differences and disposable replay procedure are in [the current evidence receipt](evidence/story-1-17-current-qualification-2026-10-17.json).

| Command | Result |
|---------|--------|
| `python3 eng/story117-platform-qualification-current/verify.py` | Exit 0; consistency only, HOLD and zero staging execution |
| `python3 -m unittest discover -s eng/story117-platform-qualification-current -p 'test_*.py' -v` | Exit 0; 30 tests passed |
| `python3 eng/story117-builds-publication/verify.py` | Exit 1; pre-existing captured Platform snapshot drift |
| Unchanged publication verifier with supported `--workspace` against disposable historical snapshot | Exit 0; historical replay only, 4,888 captured hashes match |
| `python3 eng/story117-platform-qualification/verify.py` | Exit 1; historical Builds fingerprint refusal preserved |
| `python3 -m unittest discover -s eng/story117-platform-qualification -p 'test_*.py' -v` | Exit 1; historical 13 tests, 21 failures and 8 errors preserved |
| `git diff --check` | Exit 0; existing spec CRLF normalization warning |

Matrix coverage ran and passed: current proposal (`test_controlled_proposal_passes_and_cli_reports_only_consistency`, `test_current_proposal_binds_all_25_real_sources`); source drift (`test_each_allowlisted_source_change_or_absence_is_refused`, `test_current_binding_refuses_missing_or_changed_sources_in_disposable_copy`); activation claims (`test_route_activation_rejected_in_both_documents_and_module`, `test_authority_and_clock_claims_rejected`, `test_every_check_refuses_results_or_execution_claims`); release/source mismatch (`test_published_release_cannot_be_substituted_with_local_source`, `test_release_platform_and_source_observations_cannot_gain_acceptance`).

### Final verification after review (2026-10-17)

- New diagnostic: exit 0, consistency only with HOLD, zero execution, V1Only and T0 unset.
- New suite: 39/39 pass. Added regressions cover exact secret mappings/source names, module-to-source associations, reviewed requirements/staging safeguards, source/ancestor symlink redirection, bounded refusal of FIFO/directory inputs, and stale canonical revisions for all five snapshots including published/local Builds substitution. Directory coverage remains available on hosts without POSIX FIFO support.
- Accepted historical publication replay: exit 0, all 4,888 Platform hashes and both signed package identities verified. The current-checkout command retains its expected exit 1 observation; no historical verifier, receipt or checkout bytes changed.
- Preservation: all 25 current source bindings, 42 historical protected files and 45 current-run baseline inputs match. Proposal JSON bytes remain unchanged by review fixes.
- Whitespace: `git diff --check` exits 0. Exact final command streams and refreshed five-file proposal hashes are in the current receipt's `reviewVerification` section.
- Current Platform qualification remains separate owner work: accepted tool/range and catalog binding, module intake/attestations, qualified release/hosting, and separately authorized operational evidence remain unresolved. Story 1.17 stays backlog/HOLD.
