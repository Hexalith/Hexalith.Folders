---
title: 'Story 12.1: revalidate the completed replay implementation'
type: 'chore'
created: '2026-10-15'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - '_bmad-output/implementation-artifacts/spec-12-1-eventstore-backed-folder-repository-and-replay.md'
  - '_bmad-output/implementation-artifacts/story-12-1-eventstore-consumer-compatibility-2026-10-15.yaml'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The renewed Story 12.1 build request targets an implementation already completed and reviewed at the current source revision. The cached Epic 12 context still cites an older EventStore pin and superseded startup limitation, obscuring the current evidence.

**Approach:** Refresh the context from current planning authority and the latest compatibility record. Rebuild and execute the complete Server and Workers test projects using Debug source references, then record reproducible commands, results and limitations. Preserve existing source, completed specs, historical acceptance and approvals, dependency versions and submodule revisions. Preserve the user's pre-existing sprint-status edit, including Story 12.1's `in-progress` value. This continuation establishes source verification; it adds no deployed durability, rollout or v2-exposure acceptance.

</frozen-after-approval>

## Implementation Notes

- Intent gaps: none. Irreversible actions: none. Footprint: this verification record and `epic-12-context.md`; no runtime interface or source change is planned.
- Investigation baseline: Folders `a8a31efbc3400fbc55934f600023333744d0fff2`, branch `main`, .NET SDK `10.0.401`. `git ls-tree HEAD references/Hexalith.EventStore` returns `6394d0500e6e277b8975cb397c9f0389f9a16929`, matching the 2026-10-15 consumer supplement. That supplement explicitly preserves the historical 6.6 acceptance and prior 2026-10-14 pin `173ad7f4e56a72f44621c7d3d7be86a5ec19dc03`.
- Read-only independent investigation found no unfinished implementation task. All original execution tasks and final review corrections are complete. Production host composition binds actor-folded repositories and durable permission stores; startup rejects incorrect repositories. SDK projection dispatch and authenticated Worker routes are implemented and covered by existing replay, process-wire and persisted-row tests.
- Refreshed the stale Epic 12 context from epics, PRD, architecture, UX, the current planning manifest and the 2026-10-15 supplement. It now names the current pin, distinguishes passing startup health from live durability acceptance, and retains the separately failing upstream asynchronous-dispatch test and production-recovery prerequisite.
- The existing sprint-status diff deliberately changes Story 12.1 from `review` to `in-progress`. The user approved continuing while preserving existing edits. Initial synchronization to `in-progress` is already satisfied; final tracker synchronization is excluded by the preserved-edit instruction above. All comments, other story keys and original completed specs remain unchanged.
- Verification runs from `/home/administrator/projects/hexalith/folders`. Reuse existing restored Debug/source assets; on a fresh checkout or after switching dependency mode, first run `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -m:1 -nr:false -v:q`. No package or submodule update is required.
- Planned verification commands:

  ```bash
  dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -m:1 -nr:false -p:UseSharedCompilation=false -v:q
  dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll
  dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -m:1 -nr:false -p:UseSharedCompilation=false -v:q
  dotnet tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests.dll
  ```

- The original spec records historical 4443 passing Folders checks, two optional Forgejo skips and a successful 22-resource no-build Aspire startup. Those results are prior evidence, not rerun claims. This continuation does not rerun other suites, the solution build, EventStore platform checks, Aspire, live mutation/restart, ETag races, broker recovery/drain or operator repair. The upstream asynchronous-dispatch mock failure remains a separate recorded limitation.
- Local command logs and pre-existing-file byte snapshots are retained under `/tmp/folders-12-1-current-wm2hrdr8/`; compact observed results will be recorded below so verification does not depend on temporary logs.

- Observed verification: both planned builds passed with zero warnings/errors. Complete Server execution passed 979/979 and Workers execution passed 140/140: 1119 passing tests, zero failures, errors or skips. Every command above exited 0. The four local logs are `server-build.log`, `server-tests.log`, `workers-build.log` and `workers-tests.log`; `checks.json` records the exact argument arrays and exit codes. `git diff --check -- _bmad-output/implementation-artifacts/epic-12-context.md` and explicit CRLF/trailing-whitespace checks for both changed documents pass.
- Before independent review, byte/hash comparisons confirmed all 14990 pre-existing modified/untracked files, the three completed Story 12.1 specs, sprint tracking and all eleven root-declared gitlinks are unchanged. No source, dependency version or submodule change was needed. This record and the refreshed epic context are the only changes made so far.

- Independent Blind Hunter review completed against both documents. Four context regressions were confirmed against canonical PRD/epic/architecture requirements and corrected: metadata-only events/audit/telemetry, staged-versus-commit behavior and single-mutation eligibility, durable terminal retention timing and persisted time floor, and admission partition/descriptor invariants. One low-impact spec reproducibility improvement is recorded in the deferred-work ledger. No runtime source or test result changed; the completed Server/Workers verification remains applicable.

- Final documentation checks pass after review corrections. All pre-existing file hashes, the exact original tracked diff and the three completed specs remain unchanged; the deferred-work ledger retains its original bytes as an exact prefix. Root gitlinks and sprint tracking are preserved. The workflow commit includes only this verification record, the epic context and the appended deferred-work entry.

## Review Triage Log

- Finding 1 — **medium, patch:** The refreshed content-store paragraph omitted the explicit exclusion of bodies from events, audit and telemetry. Canonical Story 12.3 requires it; restored that sentence in the epic context.
- Finding 2 — **medium, patch:** The shortened provider paragraph could require a commit for staging and omitted single-mutation eligibility. PRD FR32 permits staging without auto-commit and Story 12.4 requires one eligible provider mutation; restored both distinctions.
- Finding 3 — **medium, patch:** The retention summary omitted the durable terminal-finalization start and persisted monotonic time floor. Architecture D-7 and the detailed idempotency rules require both; restored them.
- Finding 4 — **medium, patch:** The admission summary omitted managed-tenant/protected-key partitioning, descriptor versioning and canonical validation. Architecture A-9 requires each; restored these invariants in the same admission paragraph.
- Finding 5 — **low, defer:** The environment note omits the pre-existing imported Builds package-property edit from EventStore `3.120.0` to `3.121.0`. It is preserved and the executed commands explicitly use source projects, so no failing consumer or package-mode claim results. Recorded the exact configuration and source-mode limitation in a new deferred-work entry; the one-shot workflow routes spec-only reproduction improvements there.
