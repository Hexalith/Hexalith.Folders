---
title: '11.6 Consolidate CLI/MCP adapter core and secure bearer transport'
type: 'refactor'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_key: '11-6-consolidate-cli-mcp-adapter-core-and-secure-bearer-transport'
baseline_commit: 'd0891926ae5fd43850d39fe4eb017a248fae1834'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-11-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Existing CLI/MCP adapters duplicate plumbing and attach bearer tokens without validating destinations.

**Approach:** Share existing mechanics and enforce safe bearer transport. Preserve FR48–FR49, FR51 and NFR42 behavior except the required transport rejection.

**Decision:** On 2026-10-23 Jerome chose “do recommended” after the itemized proposal: execute bounded legacy safety/continuity preparation now. This authorizes the tasks below while preserving the delivery hold and prerequisite 1.17; it does not authorize McpCli enrollment, deployment or whole-story closure.

## Boundaries & Constraints

**Always:** Preserve credential policies, lazy reads, sourcing order, correlation, cancellation, projections and formatting. Legacy changes serve safety/continuity; McpCli remains the target. One type per file.

**Never:** Add operations, packages or public APIs; change dependencies, contracts, routing, persistence or submodules; harmonize credentials; deploy or retire adapters. Preserve approval evidence. Local component evidence cannot close 11.6 or remove prerequisite 1.17.

## I/O & Edge-Case Matrix

| Scenario | Expected behavior |
|---|---|
| HTTPS remote; HTTP localhost, 127/8 or ::1 | Allow; exact localhost match ignores case; use IPAddress.IsLoopback for addresses |
| Remote HTTP, lookalike host, other scheme, relative/missing URI | Reject before token resolution/attachment and inner send; no unauthenticated fallback |
| Safe base plus unsafe absolute request URI | Reject actual destination; never automatically follow redirects |
| Credentials | CLI: env → tenant JSON → flag, no trimming; MCP: env → inline → token file, trim; lower sources remain unread |
| Missing credentials, malformed body, missing task/key | Existing pre-SDK failure and zero HTTP calls |
| Nested content-bearing properties | Same case-insensitive removal, camelCase, indentation and null omission |
| Security rejection | Constant safe text; CLI configuration exit 64, MCP startup exit 1 or per-call usage_error; retain available correlation |

</frozen-after-approval>

## Code Map

Paths are repository-relative; `{Cli,Mcp}` expands to both projects.

- `src/Hexalith.Folders.{Cli,Mcp}/Infrastructure/MetadataOnlyJson.cs`: equivalent Newtonsoft filtering.
- CLI `Commands/CommandOptions.cs`, MCP `Tooling/RequestBody.cs`: shared deserialization; retain CLI file/stdin and Client OperationFreshness.
- CLI `Commands/CommandPipeline.cs`, MCP `Tooling/ToolPipeline.cs`: repeated sourcing and validation; retain surface orchestration, catch order and rendering.
- CLI `Credentials/CredentialResolver.cs`, MCP `Credentials/McpCredentialResolver.cs`: different intentional source policies.
- Both `Composition/BearerTokenHandler.cs`; CLI `Composition/FoldersClientFactory.cs`, MCP `Program.cs`: attachment/redirects; retain documented handler facades.

## Tasks & Acceptance

**Execution:**

- [x] `tests/Hexalith.Folders.{Cli,Mcp}.Tests/AdapterCoreCompatibilityTests.cs` — pin baseline serialization, parsing, sourcing and projection behavior before extraction.
- [x] `src/shared/AdapterCore/{MetadataOnlyJson,MetadataOnlyContractResolver,RequestBodyParser,CredentialSources,AdapterInvocation,MutationSourcing,QuerySourcing}.cs` — link internal sources through both adapter `.csproj` files; rewire mapped callers, delete replaced bodies, retain local policies.
- [x] `src/shared/AdapterCore/{BearerTransportPolicy,SafeBearerTokenHandler,UnsafeBearerTransportException}.cs` — validate HTTP(S) destinations before acquisition/attachment. Rewire existing handler facades and composition; disable automatic redirects; map rejection through existing configuration failures.
- [x] `tests/Hexalith.Folders.{Cli,Mcp}.Tests/BearerTransportTests.cs` — cover matrix boundaries, overrides, zero sends, diagnostics, cancellation and production redirect settings.
- [x] `tests/Hexalith.Folders.IntegrationTests/AdapterParity/CrossAdapterBehavioralParityTests.cs` — verify unchanged cross-surface projections after extraction.
- [x] `docs/sdk/{authentication,cli-reference,mcp-reference}.md` — document transport policy and legacy scope. Record evidence here; preserve sprint backlog while delivery remains held.

**Acceptance Criteria:**

- Given duplicated mechanics, when consolidation finishes, then both adapters consume one source implementation per extracted concern with unchanged compatibility vectors.
- Given existing oracle scenarios, when both adapter suites and cross-adapter parity run, then outputs and SDK requests remain equivalent.
- Given unsafe transport, when configuration or a request reaches the guard, then no token or request is emitted and diagnostics remain metadata-only.

## Implementation Notes

- The 2026-10-23 scope response authorizes implementation of the proposed bounded work. The approved frozen constraint overrides ordinary sprint synchronization: leave the 11.6 sprint key at backlog and its planning prerequisite intact.
- Pre-edit runtime baseline captured by the parent: `aspire ps --format Json --non-interactive` showed only an unrelated Tenants AppHost. `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exited 2: Server CS1704 from duplicate Commons.UniqueIds source 1.0.0 and package 2.30.3 assemblies (plus MSB3243). `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json --non-interactive` confirmed no Folders AppHost running. Log: `/tmp/story-11-6-aspire-start.log`. No dependency changes are authorized to repair this existing blocker; focused source-reference test builds remain available.
- Parent audit read the complete tracked/untracked diff against the preserved baseline and confirmed all six bounded tasks. Matrix coverage is provided by both BearerTransportTests classes (destination, absolute override, cancellation, redirect settings, diagnostic projections), both AdapterCoreCompatibilityTests classes (filtering, parsing, sourcing and credential normalization), and the existing full credential/pre-SDK/parity suites. The recorded direct assembly results contain no skipped or unrun cases. No index staging was performed; review uses a temporary unified diff.

## Spec Change Log

- 2026-10-23: Implemented the approved bounded legacy preparation. Both adapters link the same internal serializer, contract resolver, request parser, lazy credential-source selector, invocation sourcing/validation helpers, sourcing records, and bearer guard. Local handler/serializer facades, CLI file/stdin and auto-key behavior, MCP trimming and inline/file policy, catch order, projections, and rendering remain at their existing surfaces. No dependency, contract, route, persistence, submodule, approval evidence, or sprint-status changes were made.
- 2026-10-23: Configuration and actual-request guards accept HTTPS and literal HTTP loopback destinations, reject unsafe destinations with a constant metadata-only diagnostic, and disable primary-transport redirects. Added adapter compatibility/security tests and a cross-adapter serializer/parser assertion; documented the transport policy and retained legacy scope.

## Review Triage Log

- B1 — low, rejected: the eager pipeline credential checks remain observable with an injected invocation delegate, but preserve the required pre-SDK credential-missing behavior. Real command/tool delegates call fixed generated operations over the validated base address; they accept no absolute destination override. The actual-request handler acquires no token on rejection. Removing the preflight check would require new orchestration for a scenario outside ordinary adapter use, rather than correcting an emission or output defect.
- B2 — medium, patch: the production MCP HttpClientFactory logger runs outside the bearer handler and can log an unsafe request's host/path before rejection. The review's production-composition probe reproduced this. Remove the factory loggers at adapter registration and assert the actual registered pipeline's diagnostic output.
- B3 — medium, defer: the serializer filters reflected properties but not dictionary/JToken entries. Both original serializers have this limitation; extraction preserves it. Dynamic content representation safety needs separate investigation rather than changing the compatibility policy in this bounded refactor.
- B4 — medium, patch: new exact JSON expectations use literal LF while Newtonsoft follows the writer's platform newline. Replace those expected newlines with Environment.NewLine without changing output serialization.
- B5 — low, patch: redirect settings are covered, but the newly visible redirect response has no explicit SDK/adapter projection case. Add a 3xx response case proving the existing internal-error projection and one initial request; combine with the production redirect-setting assertions.
- B6 — medium, patch: MCP tests construct clients manually or exit before registration, so deleting Program's primary-handler configuration would escape them. Exercise a shared internal production registration seam from Program and the test.
- B7 — medium, patch: the executable startup regression test has no timeout or guaranteed child cleanup. A mistakenly accepted endpoint can keep a local test run alive. Bound the wait and kill a surviving child in finally.
- B8 — low, rejected: the CLI production-factory test currently passes against the repository's pinned generated client/runtime fields. Replacing its private-field inspection needs a new composition seam solely for a hypothetical later implementation change; no present transport failure was demonstrated. Keep the existing production wiring assertion.
- B9 — low, patch: the ratified Uri/IPAddress.IsLoopback rule already settles canonicalized and IPv4-mapped forms, so no new normalization decision is needed. Add direct allowed and adjacent-rejected rows to pin those cases.
- B10 — low, patch: CLI's per-request projection case throws the exception directly. Connect a generated SDK client and the real bearer handler to demonstrate rejection propagation and zero inner sends through CommandPipeline.
- E1 — medium, patch: the edge probe reproduced a permitted HTTP loopback request carrying the bearer to a remote HTTP_PROXY when NO_PROXY is empty. Correct primary transport to bypass proxies for permitted HTTP loopback destinations while retaining HTTPS proxy behavior; add a regression using a deterministic configured proxy.
- E2 — medium, patch: independently reproduced the same pre-guard HttpClientFactory log disclosure as B2; group with B2, retaining both rows.
- V1 — medium, patch: pre-verified MCP registration gap corroborates B6. A direct factory test cannot detect removal of Program's registration; group with B6, retaining both rows.
- Patch completion: B2/E2, B4, B5, B6/V1, B7, B9, B10 and E1 are fixed. The primary transport wraps the normal proxy to bypass only approved HTTP loopback destinations; HTTPS proxy choice, bypass and credentials delegate unchanged. Both production registrations remove factory URL loggers. Program and MCP tests share an internal registration; recording log-sink tests exercise actual registered clients, redirect cases exercise generated SDK projections, and subprocess waits have bounded cleanup. Portable expectations and canonicalized address rows are pinned. B3 is recorded in deferred-work.md; B1 and B8 retain the rejected verdicts above.

## Verification

Build each adapter test project with `dotnet build <project> --configuration Debug -p:UseNuGetDeps=false -m:1`; run built xUnit assemblies, integration parity and Testing.Tests ScaffoldContractTests. Check formatting and `git diff --check`. Capture Aspire baseline before code edits. Record blockers and PD10 inventory drift separately; never reseal pending approval evidence or weaken gates.

Planning baseline: both adapter builds passed with zero warnings/errors; CLI 753/753 and MCP 703/703 passed, no skips. No production changes or external effects performed.

### Bounded implementation evidence (2026-10-23)

- Before extraction, both Debug source-reference adapter test builds passed with zero warnings/errors. The new `AdapterCoreCompatibilityTests` ran against the unextracted code: CLI 9/9 and MCP 9/9 passed (no skips), covering nested content filtering/formatting, optional/required/malformed body parsing, credential lazy precedence and intentional whitespace difference, caller sourcing/cancellation, and projection vectors. CLI file/stdin compatibility was added afterward and also passes.
- Final builds: `dotnet build tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj --configuration Debug -p:UseNuGetDeps=false -m:1`, the corresponding MCP test-project build, and `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --configuration Debug -p:UseNuGetDeps=false -m:1` all passed with zero warnings/errors. Logs: `/tmp/story-11-6-{cli,mcp,integration}-build.log`.
- Direct built-assembly runs: `dotnet tests/Hexalith.Folders.Cli.Tests/bin/Debug/net10.0/Hexalith.Folders.Cli.Tests.dll` passed 790/790; the corresponding MCP assembly passed 738/738; `dotnet tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll -class '*CrossAdapterBehavioralParityTests'` passed 587/587. No skips or not-run cases. Logs: `/tmp/story-11-6-{cli,mcp,parity}-tests.log`.
- Security evidence includes HTTPS, exact case-insensitive localhost, multiple 127/8 addresses, ::1, remote HTTP, DNS lookalikes, localhost trailing dot, other schemes, relative/missing URI, unsafe absolute override of a safe base, no attachment/inner send on rejection, no handler credential acquisition on rejection/cancellation, safe CLI/MCP diagnostics and retained correlation, primary redirect settings (including the actual CLI factory pipeline), and the real MCP executable's startup exit 1.
- `dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --configuration Debug -p:UseNuGetDeps=false -m:1` passed with zero warnings/errors. `dotnet tests/Hexalith.Folders.Testing.Tests/bin/Debug/net10.0/Hexalith.Folders.Testing.Tests.dll -class '*ScaffoldContractTests'` returned 1: 14/16 passed. Existing untouched inventory/pin failures remain explicit: `RootBuildConfigurationOwnsTargetFrameworkAndPackageVersions` expects AppHost SDK 13.6.3 while the existing AppHost declares 13.6.1; `ProjectReferencesFollowAllowedDependencyDirection` omits the existing `Hexalith.Folders.Client` reference in Server.Tests. Log: `/tmp/story-11-6-scaffold-tests.log`. These gates were not weakened or resealed.
- The parent-captured Aspire startup duplicate-assembly blocker above remains outside this bounded scope. The focused test evidence does not replace deployed-path or provider evidence, does not resolve pending PD10 approval/inventory work, and does not authorize whole-story closure. Story 11.6 remains held at backlog in sprint tracking with prerequisite 1.17 intact; this spec remains in progress for workflow review and retained delivery holds.
- Formatting: `dotnet format whitespace <adapter-test-project> --no-restore --verify-no-changes --include <changed adapter/core/test paths>` returned 0 for CLI and MCP. The same check for the changed integration parity source returned 0 (`Formatted 0 of 41 files`); `git diff --check` returned 0. Logs: `/tmp/story-11-6-format-verify-{cli,mcp,parity}.log`.

### Final parent verification and scoped completion (2026-10-23)

- Rebuilt CLI.Tests, MCP.Tests, IntegrationTests and Testing.Tests individually with `--configuration Debug -p:UseNuGetDeps=false -m:1`: all four passed with zero warnings/errors. One initial parent invocation used the nonexistent `Integration.Tests` path; correcting it to the actual `IntegrationTests` path passed. Final logs: `/tmp/story-11-6-final-{Cli,Mcp,Integration,Testing}-build.log`.
- Direct final assembly runs passed CLI 808/808, MCP 756/756 and CrossAdapterBehavioralParityTests 587/587, with no skips or unrun cases. The Contracts.Tests Debug build passed zero warnings/errors; ConsumerDocsConformanceTests plus ProviderErrorDocsConformanceTests passed 42/42, including a repeat after documenting HTTP loopback proxy bypass. Logs: `/tmp/story-11-6-final-{cli,mcp,parity,consumer-docs}-tests.log`.
- Final scaffold run remains 14/16 with exactly the two baseline pin/reference failures described above. Git comparison confirmed the AppHost, Server.Tests project, scaffold tests, dependency policy, sprint tracker and planning manifest are unchanged from the preserved baseline. Its exact run was `dotnet tests/Hexalith.Folders.Testing.Tests/bin/Debug/net10.0/Hexalith.Folders.Testing.Tests.dll -class '*ScaffoldContractTests'` (exit 1); log: `/tmp/story-11-6-final-scaffold-tests.log`.
- Independently compiled the production transport sources into a temporary socket probe. With HTTP_PROXY set and NO_PROXY empty, the loopback target received the synthetic bearer and the configured proxy received zero requests. Command: `dotnet run --project /tmp/story-11-6-proxy-recheck/Probe.csproj --no-launch-profile` (exit 0); log: `/tmp/story-11-6-final-proxy-probe.log`. Deterministic unit tests separately prove ordinary HTTPS proxy and credential delegation.
- Final changed-file whitespace verification passed for both adapter projects and integration parity; `git diff --check` passed. Logs: `/tmp/story-11-6-final-format-{Cli,Mcp,Integration}.log`.
- Read-only PD10 comparison found 26 pre-existing versus 32 current mismatches in the unchanged 229-artifact A6b-pending inventory. Six additional stale paths are the CLI/MCP references, CommandOptions, CommandPipeline, ToolPipeline and the parity test source. New shared-core and registration files also require later inventory inclusion. No pending artifact, approval or source-inventory list was resealed.
- This spec's `done` status means the authorized bounded preparation and its review are complete. It does not close Story 11.6: its sprint key remains backlog, the planning hold and prerequisite 1.17 remain intact, and existing runtime/scaffold/requalification blockers remain explicit. The approved frozen scope overrides normal sprint synchronization.
