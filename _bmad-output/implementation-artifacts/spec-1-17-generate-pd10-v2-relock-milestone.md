---
title: 'Generate the PD10 v2 relock candidate set'
type: 'feature'
created: '2026-09-19'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3f1056d998ac4688f36eb869c516812c1a4ddb71'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-ux-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Rank-4 milestone `1.17-GENERATE` has approval to generate, but the repository still exposes a v1-only Contract Spine and legacy authorization/error behavior that cannot serve as the digest-bound PD10 v2 candidate for A6b.

**Approach:** Generate the complete v2 contract, server, SDK, CLI, MCP, UI, previous-spine, C13, documentation, drift-gate, and focused-test candidate set in lockstep, then emit an exact-path/SHA-256 conformance manifest without exposing or approving it.

## Boundaries & Constraints

**Always:** Preserve v1 and historical OQ3 evidence; cover exactly fourteen canonical access states and all eleven protected families; authenticate and establish fresh authority before any protected lookup/read; prove folder scope and task binding; use canonical 401/404/503 envelopes with closed visibility/action/error/exit/failure vocabularies; keep generated files generator-owned; preserve all pre-existing worktree changes. Stop and escalate if a deployed external v1 consumer is discovered.

**Never:** Route or publish v2; close Story 1.17 or any ordinary story; infer or record A6b/A8; remove the execution hold; modify/regenerate `sprint-status.yaml`; generate OQ5–OQ13 runtime evidence; edit generated SDK/parity rows by hand; alter deployment/release exposure.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Unauthenticated | Any protected v2 operation | No protected lookup; canonical `401` | `authentication_failure` / `authentication_required` / `check_credentials` / `redacted` |
| Fresh negative authority | Wrong tenant, revoked, hidden, absent, disabled, unknown, or insufficient scope | Zero protected reads; byte-equivalent `404` | `tenant_access_denied` / `resource_unavailable` / `no_action` / `redacted` |
| Unusable authority | Stale, unavailable, conflicting, or incomplete evidence | Zero protected reads; retryable `503` | `read_model_unavailable` / `projection_unavailable` / `retry` / `redacted` |
| Scoped diagnostics/task | Folder-scoped diagnostic or task status | Fresh folder authority precedes lookup; task belongs to folder | Scope/binding failure collapses to canonical `404`; authority failure to canonical `503` |
| Adapter conflict | `concurrency_conflict` | CLI exit `77`; MCP kind `concurrency_conflict` | No legacy caller-visible outcome |
| Read-model outage | `read_model_unavailable` | CLI exit `73`; closed MCP mapping | Retry metadata retained without disclosure |

</frozen-after-approval>

## Code Map

- `docs/contract/authorization-matrix.md` -- finalize the existing `2.0.0-candidate.1` denominator as candidate version `2.0.0`; do not edit `oq3-authorization-evidence.yaml`.
- `src/Hexalith.Folders.Contracts/openapi/` -- retain v1 byte-stable; add `hexalith.folders.v2.yaml` and update extension references/tests to consume v2.
- `src/Hexalith.Folders.Server/{Program,FoldersDomainServiceEndpoints,ProviderReadinessEndpoints,OpsConsoleDiagnosticsEndpoints,AuditEndpoints}.cs` and authorization mappers -- candidate-only policy, ordering, scope, binding, and canonical envelopes; do not production-route v2.
- `src/Hexalith.Folders.Client/{Hexalith.Folders.Client.csproj,nswag.json,Generation/,Generated/}` -- switch the deterministic full generation pipeline to v2; never hand-edit `.g.cs`.
- `src/Hexalith.Folders.{Cli,Mcp,UI}/` -- consume generated v2 contracts and canonical projections; UI retains distinct denial/unavailable states using Fluent/FrontComposer patterns.
- `tests/tools/parity-oracle-generator/`, `tests/fixtures/{previous-spine.yaml,parity-contract.schema.json,parity-contract.yaml}` -- add status/error fingerprints and regenerate C13 deterministically; never hand-edit parity rows.
- `tests/Hexalith.Folders.*.Tests/` and `docs/{contract,sdk}/` -- focused conformance, zero-read, closed-vocabulary, consumer, and drift evidence.
- `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- deterministic inventory of every candidate artifact path and raw-byte SHA-256 digest for A6b review only.

## Tasks & Acceptance

**Execution:**
- [x] Contract/matrix files -- create the v2 surface, corrected scopes, exact envelopes, closed vocabularies, and historical-v1 fingerprints.
- [x] Server/auth files -- implement fallback/challenge behavior, fresh layered authorization, authorization-before-lookup, folder-scoped diagnostics, and task-folder binding without enabling v2 routes.
- [x] SDK/CLI/MCP/UI files -- regenerate the .NET client and migrate consumers/mappings in lockstep, including CLI 73/77 and MCP `concurrency_conflict`.
- [x] C13/gate/docs files -- extend generators and schema, regenerate parity/baseline outputs, document the v2 candidate, and add symmetric/server/generated/closed-vocabulary drift gates.
- [x] Conformance-set artifact -- inventory exact candidate paths and SHA-256 digests after all generated outputs are stable.

**Acceptance Criteria:**
- Given the v2 candidate set, when focused contract and adapter gates run, then each of 49 operation identities appears exactly once with all required scopes, status/error fingerprints, and parity cells, and forbidden protected outcomes are absent.
- Given every denial/authority-unavailable test double, when a protected endpoint is invoked, then authentication/authorization completes before resource, provider, content, audit, task, count, filter, or search access and protected read counters remain zero.
- Given candidate generation completes, when the conformance-set is verified, then every listed path exists and every SHA-256 matches, while v1, the hold, lifecycle tracking, approval records, and production exposure remain unchanged.

## Implementation Notes

- The locally complete candidate remains non-routed and has not been published or approved. `Program.cs`, supported deployment profiles, the historical v1 Spine, OQ3 evidence, execution hold, and sprint status remain unchanged.
- Independent audit corrected two generator omissions before the digest was sealed: `CliExitCode` now includes `77`, and the closed MCP failure vocabulary is derived from all 44 post-SDK operation categories plus the two pre-SDK kinds. The complete contract suite then exposed and fixed stale canonical-catalog and CI-lane assertions.
- The final conformance manifest inventories 75 candidate artifacts. Manifest SHA-256 is `649ecfffd95b54ce086777496d612af2793e6b8d254d4985f35b0cbc85ae90ad`; ordered candidate-set SHA-256 is `535c675159047c0ff325995d351dafaa99aac13a1a542ba07a150f9a26ae27ed`; authorization-matrix SHA-256 remains `1d60f21874e0c2e4e44ebc839786d8f65e76ea56c748afb26376e5996cf0d7ef`.
- The final matrix audit names all seven frozen fresh-negative access states in the runtime gate and asserts the exact ordered 14-state and 11-family vocabularies; the affected Server suite passes 728/728 and Contracts passes 322/322.
- Product, Architecture, and Security A6b review of those exact bytes remains the next human checkpoint. No approval is inferred by this implementation record.

## Spec Change Log

- 2026-09-19: Generated and verified the complete non-routed PD10 v2 candidate, closed the generated CLI/MCP vocabulary gaps found during independent audit, and sealed the final 75-artifact conformance digest for A6b review.

## Review Triage Log

## Design Notes

The relock candidate is a parallel v2 surface, not an in-place v1 migration. Candidate server endpoints may be assembled and tested through an isolated mapping seam, but `Program.cs` and supported deployment profiles must not select them until later governance gates authorize exposure.

## Verification

**Commands:**
- `dotnet msbuild src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj /t:GenerateHexalithFoldersIdempotencyHelpers /p:Configuration=Debug` -- generated SDK and helpers are deterministic from v2.
- `dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root . --contract src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml --output tests/fixtures/parity-contract.yaml` -- C13 and fingerprints regenerate without drift.
- Focused builds/direct xUnit v3 class runs for changed Contracts, Server, Client, CLI, MCP, and UI test classes -- all pass with zero warnings/errors.
- `pwsh ./tests/tools/run-contract-spine-gates.ps1 -NoRestore` and `pwsh ./tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` -- candidate contract/generation/parity gates pass.
