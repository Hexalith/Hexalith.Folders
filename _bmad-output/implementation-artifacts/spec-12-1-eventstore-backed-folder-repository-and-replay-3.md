---
title: 'Story 12.1: verify completed implementation against current dependencies'
type: 'chore'
created: '2026-10-14'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/spec-12-1-eventstore-backed-folder-repository-and-replay.md'
  - '_bmad-output/implementation-artifacts/story-12-1-eventstore-consumer-compatibility-2026-10-14.yaml'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The renewed Story 12.1 build request follows a Platform/Tenants reference update. The original implementation and independent review are complete, and the sprint key already reads `review`; investigation found no unfinished implementation task.

**Approach:** Verify the completed implementation against the current dependencies with fresh Server and Workers builds and test execution. Record exact commands, source identity, results, and limitations here. Preserve the completed specs, historical compatibility and approval records, selected submodule revisions, and final sprint `review` handoff. This verification adds no deployed acceptance or exposure authorization.

</frozen-after-approval>

## Implementation Notes

- Investigation baseline: `27d5446960a08f4c8669de20d85ab9a9ba68507d`, clean `main`. EventStore remains `173ad7f4e56a72f44621c7d3d7be86a5ec19dc03`; Platform is `92a02924894f6f2f219801a90e5bf25716abd028`; Tenants is `91faf19d8a3e9f254596363330a9130a970f4875`.
- Independent investigation confirmed all original execution tasks and final review corrections complete. Production host composition binds actor-folded repositories and durable permission stores; startup rejects incorrect repositories. `/project` validates retained event identifiers before SDK dispatch. Authenticated Workers persist derived permission rows through the platform store.
- The latest Platform changes concern Kubernetes operations tooling and timesheets deployment documentation; Tenants changes concern UI/BFF flows, localization, tests, and nested gitlinks. No changed Client/Contracts source consumed by Folders was identified. Nested submodules remain untouched.
- Intent gaps: none. Irreversible actions: none. Footprint: this verification record only; no new runtime interface or code change.
- Fresh verification used .NET SDK `10.0.401`, Debug assets and source project references. Commands below ran from the project root, `/home/administrator/projects/hexalith/folders`:

  ```bash
  dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:q
  dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll
  dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -c Debug --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:q
  dotnet tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests.dll
  ```

- Both builds passed with zero warnings and errors. Complete Server execution passed 960/960 and complete Workers execution passed 95/95, with no skips or failures. Server output was redirected to `/tmp/folders-12-1-recheck-2026-10-14/server.log`; Workers build and execution output are in `workers-build.log` and `workers.log` in that directory. Server build success was captured in this session's command output. These temporary logs are local verification aids, not committed acceptance artifacts.
- The original spec's final independent review closure remains the implementation evidence. Its prior complete-suite and deployed observations are historical; this run repeats only Server/Workers source validation. It does not rerun the solution gate, other test suites, EventStore compatibility tests, Aspire startup, live persistence/restart, live conflict, operator repair, or provider provisioning. The previously recorded startup timeout and live-proof limitations remain applicable.
- Sprint synchronization to `in-progress` is a no-op: the exact key `12-1-eventstore-backed-folder-repository-retire-noop-and-implemen` is already at the later `review` status. Final synchronization to `review` likewise requires no tracker edit.

## Review Triage Log

- Finding 1 — **low, defer:** The commands intentionally reuse restored assets, and the notes already identify Debug/source-reference mode. A fresh-checkout restore prerequisite would improve reproduction. The one-shot workflow routes spec-only improvements to the deferred-work ledger; no build or test failed.
- Finding 2 — **low, defer:** The baseline revision and current pins are recorded, but explicit previous pins and comparison commands would make the scope decision easier to reproduce. The claim is inspectable through the baseline commit's parent; the one-shot workflow routes this spec-only evidence improvement to deferred work.
- Finding 3 — **false:** The record already preserves compact build/test results: both builds passed with zero warnings/errors, Server passed 960/960, and Workers passed 95/95 with no failures/skips. Exact commands, SDK and source identities remain inspectable without temporary logs. The notes explicitly describe those logs as local aids and make no durable acceptance-artifact claim.
