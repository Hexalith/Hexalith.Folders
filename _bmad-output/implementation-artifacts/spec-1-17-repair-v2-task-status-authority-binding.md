---
title: 'Repair Story 1.17 v2 task-status authority binding'
type: 'bugfix'
created: '2026-10-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '7cbb7938cdcb40c7ef509aa3869b197688ffe7cd'
story_key: '1-17-publish-the-pd10-v2-authorization-contract-spine'
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/docs/governance/approval-policy.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

The current v2 compatibility seam constructs a task-status read request without the folder and fresh authorization watermark required by the production EventStore read model. Consequently an authorized v2 task-status request receives authority-unavailable instead of its task result. The existing golden test's in-memory reader ignores these fields and masks the integration error.

Pass the folder and watermark established by the seam's current layered authorization into its task-binding lookup. Preserve authentication, authorization and envelope validation before lookup, exact task/folder binding, fresh-evidence checks, cancellation, and canonical 401/404/503 outcomes. Extend the real-seam golden test to verify the complete authorized lookup context, including fresh watermark, and keep its scope-mismatch, stale and closed-wire-shape assertions. Reuse existing fixtures and substitutes; add no public API or nested helper type. Validate the focused golden, routing and task-authorization classes in Debug/source and Release/package when available, recording exact blockers and narrower passing evidence separately.

This is a small runtime correction within the accepted Story 1.17 migration scope. Preserve the existing Builds edit and all concurrent user work. Do not change dependencies, gitlinks, historical manifests, approval records, routing defaults, rollout limits, T0, or the tracker. Do not stage, commit, push, publish or deploy. Do not refresh the pending candidate inventory or qualification packet merely to silence their existing drift: current context-read semantics and inventory coverage still require separate reconciliation, so parent closure and operational qualification remain open.

</frozen-after-approval>

## Implementation Notes

- Planning found no unresolved intent choice or irreversible action in this correction. Footprint: the compatibility seam, its existing golden integration test, this spec and an additive local evidence receipt. The accepted parent migration and current owner-decision policy permit technical corrections without a repeated owner decision; the user's `yes` authorizes proceeding while preserving the remaining Builds change. This spec's explicit no-Git-mutation and no-tracker-transition scope overrides generic workflow finalization defaults.
- Investigation: `Pd10V2CandidateCompatibilitySeam.VerifyTaskBindingAsync` omits `TaskStatusReadModelRequest.AuthorizedFolderId` and `.AuthorizationWatermark`; the same seam already captures current authority in `AuthorizedFolderItem` and `AuthorizedWatermarkItem`. `TaskStatusQueryHandler` demonstrates the production request initializer. `EventStoreOperationalStatusReadModel.GetAsync(TaskStatusReadModelRequest)` refuses either missing value before storage reads. Extend `CandidateTaskStatusRejectsBindingAndEvidenceScopeMismatchAndEmitsClosedWireShape` to observe the real seam's requests while preserving its existing response assertions.
- Pre-edit baseline: `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exited 2 with existing `CS1704` duplicate `Hexalith.Commons.UniqueIds` references. CLI log: `/home/administrator/.aspire/logs/cli_20261024T030241_2841e1b7.log`. No host repair is part of this change. Resource-state inspection follows before code edits.
- Read-only planning observations found 33 stale rows in the current 229-path pending inventory and eight stale source bindings in the current 31-source qualification proposal; these counts describe that captured source state. The historical planning inventory remains immutable. Canonical context-read WRITE authority versus historical read-token reauthorization is a separate unresolved semantic gap; refreshing hashes would not resolve it.
- The pre-edit resource inspection, `aspire describe --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json`, exited 0 and confirmed that the selected Folders AppHost was not running. No stop action was necessary.
- The new request-context assertions failed against the original seam because `AuthorizedFolderId` was null. Supplying both existing authority items corrected the request. The first source validation passed 115 integration cases and 49 task-authorization/persisted-status cases. The independent source Server test build is blocked by existing obsolete enum aliases in `FoldersCallbackBoundaryTests.cs:99-100` (`CS0618`); no warning suppression or unrelated repair was applied.
- Review's production-reader regression exposed a second mismatch on the same task-status path: the native persisted projection watermark is a numeric sequence, while the v2 response requires an opaque identifier. The seam now compares the original native freshness evidence first, then projects canonical positive numeric watermarks into deterministic SHA-256 presentation tokens scoped to tenant/folder/task, using the existing length-prefixed hashing helper. Valid opaque tokens and absent watermarks retain their prior representation; malformed numeric forms still fail closed. Stored task evidence is unchanged. This private adapter correction preserves the existing public schema and approved task-status behavior.
- Review coverage now rotates current authority, rejects stale results and six malformed numeric watermarks, and drives the actual production projection writer/reader over the existing persisted Dapr test transport. It asserts selected persisted task contents before and after reads, canonical missing-task denial, schema-valid output and deterministic repeated watermarks. The first production test reproduced the watermark failure. A temporary ambiguous target-typed constructor diagnostic and an incorrect category/code assertion were corrected in the test; canonical denial remains category `tenant_access_denied` and code `resource_unavailable` as specified by v2.
- Final validation passed: 116/116 integration cases in Debug/source and 116/116 in Release/package; 49/49 task-authorization and persisted-status cases in each mode; full packaged Server suite 1216/1216. The audited Release CI solution build passed with zero warnings/errors. Focused whitespace checks passed, with workspace-load warnings separately noted. The source Server build's existing `CS0618` blocker and the pre-edit Aspire `CS1704` blocker remain recorded separately; neither is treated as a passing gate.
- This bounded correction is complete. The additive [evidence receipt](evidence/story-1-17-task-status-authority-2026-10-24.json) and its adjacent portable archive retain final XML, command logs, initial failures, reviewed diff, runtime hashes and protected-input checks. Parent Story 1.17 remains open; this run grants no candidate approval, operational qualification, deployment, tracker transition or closure. No staging, commit or push is performed.

## Review Triage Log

- R1: low, a constant authorization watermark did not prove refresh between requests. Patched by reseeding authority and asserting the later lookup receives the new watermark while prior lookups retain their own value.
- R2: medium, stale task-result rejection had no directly covering golden scenario. Patched with an explicit stale-result 503/category assertion while preserving existing malformed-envelope and binding negatives.
- R3: medium, request assertions alone did not exercise production folder-scoped persistence. Patched with the real production writer/reader test and the native-to-v2 watermark projection it exposed. The reviewer inspected the final runtime/test diff and confirmed all three findings resolved with no further correctness or security findings; reviewer did not execute tests.
