---
title: '11.6 Verify current adapter safety and retain the delivery hold'
type: 'chore'
created: '2026-11-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
story_key: '11-6-consolidate-cli-mcp-adapter-core-and-secure-bearer-transport'
baseline_commit: 'fc302b2c81aa4e107c3ed58458f0b39d2ebe6d85'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-11-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-11-6-consolidate-cli-mcp-adapter-core-and-secure-bearer-transport.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The requested Story 11.6 build targets an already implemented and reviewed bounded legacy-safety refactor. Current planning explicitly schedules no further adapter-core development, while whole-story closure remains held behind Story 1.17 and DEC-A8-HOLD.

**Approach:** Verify the existing shared core, bearer transport, and adapter parity against the current Debug source-reference graph and record exact results and blockers. Refresh the stale Epic 11 context from current planning. Preserve the original completed spec, production code, dependencies, submodules, approval evidence, and backlog/held lifecycle entries. This verification does not enroll McpCli, deploy services, retire adapters, release holds, or close Story 11.6.

</frozen-after-approval>

## Implementation Notes

- Re-entry found every bounded implementation task checked complete in the original spec. The approved November 10 planning amendment records the landed shared core and HXF-SEC-001 guard and assigns eventual removal to Story 5.15.
- The planning manifest retains DEC-A8-HOLD and Story 1.17 prerequisites, execution authorization held, delivery evidence open, and lifecycle backlog. The November 16 Story 1.17 handoff still records an operational hold, missing hosted Projects identity, composed hosted qualification, and an explicit closure decision. Local test results cannot substitute for those gates.
- No intent gaps or irreversible operations are required for this verification. The footprint is the refreshed context, this continuation spec, and retained verification logs. Run existing tests only; no production changes or new tests are planned.
- Initial inspection observed unrelated Builds working-tree changes; a later inspection found the workspace clean and root HEAD advanced externally to the baseline recorded above. No Git mutation was performed by this workflow. Preserve concurrent work.
- Sprint synchronization was read and intentionally not applied: the original approved bounded scope and this continuation preserve the story backlog and execution hold. The original spec remains unchanged.
- Verification command: `run-with-temp bash _bmad-output/implementation-artifacts/evidence/story-11-6-reentry-2026-11-16/verify.sh`. All three individual Debug source-reference builds passed with zero warnings/errors. Full CLI: 808/808; full MCP: 756/756; CrossAdapterBehavioralParityTests: 587/587. Total: 2,151 passed, zero failures, errors, skips or not-run cases.
- The initial separate artifact-output run built successfully but failed fixture discovery because ParityOracle walks upward from AppContext.BaseDirectory to Hexalith.Folders.slnx. The final disposable workspace linked the unchanged source solution and canonical tests directory at its ancestor, then rebuilt and reran all lanes. Both attempts' logs and the exact final harness are retained beside receipt.json. This was a verification-layout correction; no test or production source changed.
- Receipt: `evidence/story-11-6-reentry-2026-11-16/receipt.json` records full source revisions, artifact hashes, commands, results and evidence limits. Disposable artifacts were removed by run-with-temp after completion. Source, tests, build configuration, original spec and tracking files were checked unchanged against HEAD. No application startup was required because this run changed no production code.

- Blind Hunter completed independently. Edge Case Hunter, Verification Gap Reviewer and Acceptance Auditor were skipped for this documentation/evidence-only continuation. Ten findings were corrected; none were deferred.
- After review corrections, the harness was rerun with a fresh `review-verified` output directory: all three builds again passed with zero warnings/errors and all 2,151 cases again passed without failures/skips/not-run cases. Targeted negative invocations returned exit 2 before builds for existing logs and failed fixture-link setup. `bash -n`, receipt/hash checks and changed-text whitespace checks passed.
- The `done` status completes this bounded verification only. Story 11.6 and Story 1.17 lifecycle gates remain unchanged. No commit or push was requested; user instructions prohibit unrelated Git mutations, so no index or history changes were made.

## Review Triage Log

- R1 — medium, patch: restored authorization before protected-resource observation as well as lookup in the regenerated context; current planning requires both.
- R2 — medium, patch: restored explicit content exclusions for events, logs, traces, metrics, diagnostics and error responses, preventing the summary from narrowing C9.
- R3 — low, patch: stated the concrete HTTPS or HTTP literal-loopback/localhost boundary instead of leaving "unsafe" undefined.
- R4 — medium, patch: the reusable harness could overwrite receipt-bound logs. It now refuses existing final outputs; an optional fresh output directory permits a new run. A real refusal invocation returned 2 before builds and preserved the saved logs.
- R5 — low, patch: fixture-link failures were not checked explicitly. Both links now fail immediately; a forced solution-link collision returned 2 before any build.
- R6 — low, patch: added a positive, complete zero-error/failure/skip/not-run summary check to every lane, including the filtered parity run. Both successful runs executed all 587 parity cases.
- R7 — low, patch: local evidence attributes preserve captured log, JSON and platform bytes across checkout while keeping scripts LF; receipt hashes were regenerated after these corrections.
- R8 — low, patch: recorded initial per-lane build/test arguments, exits and discovery summaries separately from the successful runs; retained the originally executed harness byte-for-byte.
- R9 — low, patch: recorded each dependency checkout's clean state after the final run and verified full revisions stayed stable. Root source/config/tracker paths match HEAD; the root observation and its timing limits remain explicit.
- R10 — low, patch: retained dotnet --info and OS/kernel/architecture/RID information. The receipt explicitly limits evidence to this Linux component run and claims no cross-platform or hosted qualification.
