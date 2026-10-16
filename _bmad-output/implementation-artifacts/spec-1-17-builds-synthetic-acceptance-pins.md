---
title: 'Story 1.17 Builds synthetic acceptance pin repair'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized reversible preparation; preserve Story 1.17 tracker">

## Intent

Resolve the 31 existing HXE202 failures caused by stale synthetic G-4 P0 acceptance pins. Prepare the supported pins from the authoritative catalog and deterministically rebind dependent fixture hashes, preserving every negative control and exact expectation, explicitly synthetic records/sample approvals, and all real acceptance evidence.

Preserve actual-package configured/declarative subscription qualification, Platform assertions/behavior, v1 module-manifest compatibility, dependency selections, clean-source/release-eligibility/catalog/tool-range/provenance checks. Run relevant regressions and the full existing source/package workflow with required controls and no source skip. Retain local diagnostic archives, truthfully release-ineligible while dirty/uncommitted. Refresh GitHub/registry observations read-only; preserve concurrent upstream work.

First-install preparation and reversible local changes are authorized. Git writes, publication, deployment and production activation require separate authorization. Preserve V1Only, unset T0, historical approvals and Story 1.17 backlog/open. Update evidence, readiness and deferred status; report exact hashes, remaining blockers and actionable next commands without claiming staging qualification or story closure.

</frozen-after-approval>

## Implementation Notes

- Baselines and dirty work are retained from the previous qualification. No intent gap or irreversible action is needed. The change is bounded fixture maintenance plus a deterministic preparation helper and evidence updates. User instructions supersede workflow tracker/commit steps.
- Code map: `ModuleManifestLoader.ModuleManifestEventStoreVersion` and the v1 manifest schema intentionally retain 3.120.0. The package evidence helper/mock's 3.120.0 bindings enforce that compatibility and must remain. `G4P0AcceptanceValidator` separately uses all four `SupportedPlatformPins`, resolved from the embedded evaluated catalog. Its acceptance-only contract manifest explicitly is not runnable.
- `test/fixtures/evidence/acceptance`: 37 records and 15 bound run/cleanup documents need pin refresh. Preserve byte formatting/duplicate-key control. Rebind only digests that matched original artifact bytes; preserve deliberate invalid hashes. The Dapr-runtime pin-mismatch control retains 1.18.0. No `.expected.json`, sample approval, report, package or real evidence changes.
- New helper uses `Tools/write-platform-version-catalog.ps1`, computes all edits before writing, limits writes to the acceptance corpus, and supports a read-only consistency check. Verify determinism and unchanged expectations alongside existing validator tests.
- Reuse historical package/source results only where before/after input hashes match. New full qualification uses `-RequireControls -RetainPackageDirectory` without skips, with 4.31.2 solely as an observed diagnostic identity in its isolated feed.

- Implemented 52 JSON byte edits and an acceptance-only preparer, documented the legacy boundary, and added seven independent preparation controls. Existing 37 expectation files and sample approvals are byte/semantically preserved. Two independent preparations from captured HEAD bytes reproduce the current corpus and a second run is unchanged.
- Full G-4 source/package run passes with `-RequireControls` and no source skip, using `DOTNET_PROCESSOR_COUNT=2` to reduce concurrency. Source suites: Evidence 107/107; Module 811 pass, one existing checkout skip; Integration one pass, 12 existing live opt-in skips; zero failures. The initial two fake-tool launch assertion failures and passing focused 15-case rerun are retained separately. Actual-package subscription modes, source/package positive/36-negative parity and required controls pass.
- Official local diagnostic retains all four archives, 55 qualification artifacts and complete inventory; source/controls executed/passed, fixtures repository-modified, source dirty, releaseEligible=false. The actual pre-tag gate refuses it. No owning-repository Git write, dependency update, publication, deployment or tracker transition occurred.

## Review Triage Log

- Low, patched: Windows corpus keys used backslashes; normalized with `as_posix()`.
- Medium, patched: partial writes could appear stable with stale hashes; explicit binding validation now rejects ambiguous partial preparation before writing, with a regression.
- Medium, patched: isolated off-baseline stale pins could remain; every intended pin is explicitly checked against catalog values, preserving the named Dapr negative.
- Low, patched: JSON-escaped pins could evade byte replacement; explicit post-preparation pin checks reject unmatched refreshes, with a regression.
- Medium, patched: missing artifacts/control members could report consistency; reference, expectation-partner and tracked-membership checks now fail closed. README distinguishes preparation consistency from public-validator expectation tests.
- Medium, patched: no direct preparation coverage; seven regressions now cover regeneration/idempotence, preserved negative material, partial refreshes, stale pins, escaping and missing inputs. Reviewer independently reran all seven and reported no remaining introduced bug. No findings deferred.

- Medium, patched: primary handoff limits retained the earlier full-G-4 failure; current limits now distinguish complete technical success from release ineligibility.
- Medium, patched: latest prose incorrectly identified Platform's remote gitlink as Builds main; exact pin 86b9f351db87b00504cc98e125b0951ef2f4fa67 is now recorded separately with direct GitHub HTTP 422 and comparison 404. Local refs/dependencies remain unchanged.
- Medium, patched: selected source/package results still referenced historical failures; current selected results now bind the successful complete run and direct positive. Original failures remain explicitly historical.
- Low, patched: handoff document hashes were stale after evidence edits; recomputed for final readiness, ledger and spec bytes.
- Low, patched: resolved deferred entry retained present-tense failure evidence; wording now identifies the original blocker as historical and the current pass as resolved local preparation.
- Low, patched: copied initial-attempt log path omitted the corpus-repair prefix; copied command and registry body paths are rebased to retained files. Final evidence review verifies archives, all 55 inventory evidence members, bindings, counts/skips and actual pre-tag refusal; no findings deferred.

- Follow-up Git authorization: prepared source committed/pushed, reconciled with upstream through 5aceb5ab0683b5e8c494e82f865e138685180f36, and full clean-source G-4 plus successful exact-head push CI verified. Concurrent catalog EventStore 3.123.0/audit/fixture work was preserved. Current proof and verifier live in the Folders clean-source qualification handoff. The earlier dirty diagnostic remains historical/ineligible; no publication, Platform acceptance, deployment, activation, tracker change or story closure occurred.
