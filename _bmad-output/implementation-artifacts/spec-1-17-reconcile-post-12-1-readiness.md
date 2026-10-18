---
title: 'Reconcile Story 1.17 readiness after Story 12.1'
type: 'chore'
created: '2026-10-18'
baseline_commit: '65f34e7c4b2bd41805aaa1c3b8d87ee3b65363f1'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 12.1 is complete, but host/authentication changes invalidate four qualification bindings and three of 229 candidate bindings. The diagnostic fails; its 39-test suite reports four failures and two errors.

**Approach:** Reconcile authentication requirements and regenerate local readiness evidence. Operational migration and Story 1.17 closure remain incomplete.

## Boundaries & Constraints

**Always:** Review semantics before fingerprints. Preserve historical receipts/approvals/v1, original frozen migration intent/baseline, seven-day coexistence, V1Only, unset T0, unconfirmed first-install scope, fourteen unexecuted checks and backlog tracker. Distinguish source observations, releases and operational acceptance.

**Never:** Change runtime behavior, dependencies/gitlinks, tool acceptance, migration scope or tracker; start another story; execute staging, publish, deploy, stage/commit/push; initialize nested submodules; rewrite historical evidence; or weaken checks. Record Platform's catalog mismatch without repairing it.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Reviewed current inputs | Reconciled source/configuration inventory | Offline consistency only; HOLD and zero execution | Bounded diagnostic |
| Changed or missing input | Source or candidate drift | Refuse consistency/conformance | Relative path, no source contents |
| Authentication inventory | Gateway, receipt and workload changes | Exact reviewed settings and defaults; enrollment unresolved | Reject contradictory values |
| Operational prerequisites absent | Missing acceptance or incoherent catalog tuple | Retain HOLD and unresolved owner work | No artifact substitution |

</frozen-after-approval>

## Code Map

Repository-relative aliases: `packet` = `eng/story117-platform-qualification-current/`; `planning` = `_bmad-output/planning-artifacts/`; `implementation` = `_bmad-output/implementation-artifacts/`; `server` = `src/Hexalith.Folders.Server/`.

- `packet/configuration.proposed.json`: four stale sources; outdated Folders/Platform/EventStore snapshots. Add `server/Authentication/FoldersGatewayHumanBearerHandler.cs` as input 26.
- `server/FoldersServerHostComposition.cs`: gateway client/base address, human bearer handler and Query/Metadata policies. `server/Authentication/HumanAuthorityReceiptVerifier.cs`: ExpectedWorkload defaults to eventstore.
- Bound EventStore `WorkloadAuthenticationOptions.cs`: WorkloadJwtBearer with JwtBearer fallback; `WorkloadAssertionIssuerOptions.cs`: EnableResourceBindingRequests defaults false, granting no enrollment.
- `scripts/generate-pd10-v2-conformance-set.py`: three changed server rows; 20 provenance bindings currently match. Update candidate provenance after regeneration.
- Platform Stories 1.8/1.9 remain in-progress/backlog; its Builds gitlink differs from direct Builds. Operational qualification remains absent.
- `implementation/evidence/story-12-1-durability-2026-10-18-complete.json`: local durability acceptance; no deployed qualification.

## Tasks & Acceptance

**Execution:**
- [x] `packet/configuration.proposed.json`, `packet/staging-checks.json` -- reconcile snapshots/settings/prerequisites; bind the bearer handler and record catalog incoherence. Keep credentials, enrollment, releases and activation unresolved.
- [x] `packet/verify.py`, `packet/test_verify.py` -- enforce reviewed inventory/checksums; retain safety regressions and add mutations for new settings/source. Preserve isolated fixtures and independent real-source checks.
- [x] `packet/README.md` -- document reviewed deltas and remaining prerequisites.
- [x] `planning/generated-v2-conformance-set-2026-09-17.yaml`, `planning/planning-story-manifest.yaml` -- regenerate twice; update only current candidate provenance. Preserve inventory scope, historical bindings and lifecycle fields.
- [x] `implementation/evidence/story-1-17-post-12-1-readiness-2026-10-18.json`, `implementation/story-1-17-current-candidate-technical-readiness-2026-09-24.md` -- append exact commands/results, canonical revisions, before/after hashes, preservation checks and owner prerequisites.

**Acceptance Criteria:**
- Given current inputs, when diagnostic/mutation tests run, then all pass with HOLD and unchanged activation controls.
- Given regeneration, when independently audited twice, then bytes match the inventory and all 20 provenance bindings match.
- Given host/authentication changes, when local checks run, then retain results; failures block acceptance.
- Given missing operational acceptance, when reporting readiness, then acknowledge Story 12.1 completion and retain Story 1.17 backlog and migration scope.

## Implementation Notes

- 2026-10-18: Implemented the current packet reconciliation, new bearer-handler source binding, reviewed authentication requirements, regenerated candidate/provenance and additive readiness evidence. Parent independently checked all 229 candidate artifacts, all 20 current bindings, 26 source hashes, eight changed-file hashes, 99 critical preservation hashes and the unchanged approved frozen block. All 4,879 parent-protected files and eleven gitlinks match the captured baseline.
- Matrix audit: reviewed inputs are covered by `test_controlled_proposal_passes_and_cli_reports_only_consistency` and `test_current_proposal_binds_all_26_real_sources`; source drift by `test_each_allowlisted_source_change_or_absence_is_refused` and the independent disposable-copy check; authentication inventory by `test_post_12_1_settings_require_exact_defaults_and_unresolved_credentials` and `test_authentication_semantics_cannot_claim_fallback_delegation_or_enrollment`; operational prerequisites by `test_catalog_mismatch_stays_an_observation_without_acceptance` and release/substitution regressions. Parent reran all 44 tests successfully; no matrix row is untested or skipped.
- Parent acceptance audit found initial build commands disabled NuGet auditing without establishing a default-gate blocker. Requested five audit-enabled rebuilds and retained the original command records as diagnostic history; acceptance requires the audited results or an explicitly recorded blocker.
- Audit correction completed: five `NuGetAudit=true` builds pass with implicit restore and zero warnings/errors. Their five assembly hashes are unchanged; all 38 focused xUnit tests and 33 governance tests pass again. The receipt retains all initial commands and appends the audited acceptance results; no broad-gate blocker remains.
- Final parent verification after review fixes: the diagnostic and all 46 Python tests pass; three fresh Debug/source builds pass with NuGet auditing enabled and zero warnings/errors; five focused xUnit classes pass 43 tests, including gateway transport; governance passes 33 tests. Total: 122 passing tests, no failures or skips. Parent independently verified 31 current source bindings, 229 candidate artifacts, 20 provenance bindings, 67 portable archive members, all 49 retained command outputs, eight normalized Git identities and the extracted replay. Five recorded assembly hashes remain unchanged. Exact parent commands and raw output are retained inline in the current receipt.
- Completed this bounded readiness reconciliation. The approved frozen prohibition on staging/committing/pushing and tracker changes overrides the generic workflow completion defaults: no local commit or tracker transition is part of this completion. Story 1.17 remains backlog/HOLD with its original operational prerequisites unresolved.

## Spec Change Log

## Review Triage Log

All three review layers completed. Edge-case review returned no findings; verification-gap review found no gaps. The blind review's ten claims were individually checked against the source, tests and retained programs before grouping. Its finding-floor arithmetic is informational.

| Finding | Verdict | Evidence | Route |
|---------|---------|----------|-------|
| B1: incomplete dedicated JWT inventory | medium | `JwtBearerAuthenticationContract.Validate` requires Audience/ValidAudiences and explicit asymmetric AllowedAlgorithms in authority mode; the new inventory lists neither and omits HTTPS metadata settings. Owners following it cannot supply the complete reviewed contract. | patch |
| B2: semantic implementation sources unbound | medium | SOURCES binds options declarations but omits the implementation of dedicated-section selection, JWT validation and resource-binding issuance. Those files can change without invalidating this packet. | patch |
| B3: catalog/status observations not reproducible from receipt | medium | Values are checksum-protected observations, not live Git checks. The receipt lacks explicit revision-bound tracker observations; its embedded Git commands rely on current HEAD. Retain the actual observation commands/results tied to Platform's recorded revision. | patch |
| B4: gateway transport class omitted from current evidence | low | Existing `FoldersGatewayTransportTests` covers default/custom targets and rejected direct submissions, but neither focused selection executes it despite the newly reconciled transport claim. Running the existing class is a direct evidence correction. | patch |
| B5: cross-operation staging negatives implicit | medium | The added staging procedure asks for four independent policy outcomes without requiring a matrix of wrong-operation/resource-binding denials. An operator could omit those isolation negatives. | patch |
| B6: preservation replay lacks baseline input | medium | The embedded preservation program reads temporary `baseline.json`; only its aggregate hash and selected entries are retained. Its complete 4,887-entry input cannot be reconstructed from the receipt. | patch; portable evidence group |
| B7: candidate audit uses mutable HEAD | medium | Historical comparisons explicitly use `git show HEAD:...`; after this change is committed, three-change assertions compare against the refreshed inventory. Use the recorded full implementation baseline. | patch |
| B8: output digests lack retained output | medium | Command records retain output hashes and summaries, while raw captured logs remain only in the temporary directory. Preserve the safe captured logs and bind artifact references for independent digest verification. | patch; portable evidence group |
| B9: preservation omits new paths | medium | The preservation program iterates only `base['files']`; it does not enumerate the complete final tracked/untracked inventory. A new runtime path is invisible to its success result. Require an exact inventory with explicit new evidence/spec exceptions. | patch |
| B10: worktree and Git representations conflated | medium | Parent reproduced readiness SHA-256 `6415ef1db179728cf1acdca47967c593cf38211a7290f6f010b0e27c30cfda9d` versus Git-normalized `c9a8f42fb164a7838649195d99a296f867be691ec0a1d244aa9ca0ec83e53719`; Git confirms that normalization. Label and retain the captured representation separately from normalized Git content. | patch |

All survivors have direct internal corrections, introduce no runtime/public surface, and require no change to frozen intent or owner authority. B6/B8 share the missing portable evidence retention; other findings remain separate causes. No intent gap, bad-spec loopback or deferred entry is required.

Resolution: B1/B2 now have complete unresolved JWT settings, secure defaults and five additional bound implementation sources (31 total). B3 has revision-bound Git observations. B4 has five passing gateway transport cases. B5 has an explicit 16-cell operation matrix and eight resource-binding negatives, still unexecuted. B6/B8 retain the portable baseline, corrected/original programs, raw outputs and byte representations in a 67-member archive. B7 uses the immutable full baseline and permits root HEAD to advance when recording this same change. B9 checks all tracked/untracked paths and passed the unexpected-source refusal probe. B10 separates captured-worktree hashes from verified normalized Git hashes/object identities. Parent inspected the patches and independently reran the complete focused verification and portable replay. All ten findings are fixed; none remain unresolved or deferred.

## Verification

- `PYTHONDONTWRITEBYTECODE=1 python3 eng/story117-platform-qualification-current/verify.py`
- `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s eng/story117-platform-qualification-current -p 'test_*.py' -v`
- Run the generator twice with disposable `--output` files; independently audit bytes and artifact/provenance hashes.
- Build affected test projects in Debug/source mode; invoke their xUnit assemblies directly with `-class` for `FoldersServerHostCompositionTests`, `HumanAuthorityReceiptVerifierTests`, `FoldersApiRoutingModeTests` and `Pd10ConformanceSetTests`.
- `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` after building required assemblies; record broad-gate blockers separately.
- `git diff --check`; verify preserved evidence, approvals, original frozen intent, gitlinks and tracker bytes.
