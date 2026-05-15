# Story 1.14: Wire Contract Spine drift and generated-client CI gates

Status: ready-for-dev

Created: 2026-05-13

## Story

As a maintainer,
I want contract drift and generated-client consistency gates wired into CI,
so that surface divergence fails before feature implementation can depend on it.

## Acceptance Criteria

1. Given the Contract Spine, generated SDK client, and C13 parity oracle exist or are explicitly reference-pending, when CI runs, then it executes server-vs-spine validation, symmetric drift detection, NSwag generated-client golden-file consistency, and parity-oracle schema validation as blocking gates.
2. Given a server-emitted OpenAPI artifact is available from the existing server/test surface, when the server-vs-spine gate runs, then it compares that artifact against `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` using deterministic normalization and fails on public contract drift unless the story records a bounded reference-pending blocker.
3. Given `tests/fixtures/previous-spine.yaml` is the symmetric drift baseline, when the drift gate runs, then removed operations, changed operation IDs, changed mutating/read classifications, changed required headers, changed canonical error categories, or changed parity metadata fail unless an approved deprecation entry exists in the baseline.
4. Given Story 1.12 owns NSwag client generation, when the generated-client gate runs, then CI re-runs the checked-in generation command and fails if `src/Hexalith.Folders.Client/Generated/` or generated idempotency-helper artifacts differ from the committed output.
5. Given Story 1.13 owns the C13 parity oracle, when the parity gate runs, then CI validates `tests/fixtures/parity-contract.yaml` against `tests/fixtures/parity-contract.schema.json` before any SDK, REST, CLI, or MCP parity tests consume it.
6. Given parity rows must cover the Contract Spine, when CI runs, then it verifies every current OpenAPI operation appears exactly once in the generated parity oracle and every row points to an existing operation ID.
7. Given generated artifacts must be deterministic, when CI re-runs spine normalization, SDK generation, and parity generation, then repeated runs on the same inputs are byte-stable after documented normalization and do not depend on timestamps, machine-local paths, runner paths, network state, or mutable environment data.
8. Given GitHub Actions is the architecture-selected CI/CD mechanism, when workflow files are added or changed, then they use the repository's checked-in `global.json`, central package management, root-level submodule policy, and explicit command steps that can also be run locally.
9. Given this story wires Contract Spine and generated-client gates only, when implementation is complete, then it does not add runtime REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git or filesystem side effects outside deterministic generation, CLI commands, MCP tools, UI pages, safety-invariant gates, exit-criteria gates, release publishing, live provider drift jobs, or nested-submodule initialization.
10. Given active Story 1.10 contract work may be dirty, when implementation starts, then the developer inspects the current OpenAPI, contract docs, Story 1.10 through Story 1.13 artifacts, and existing tests before assuming operation inventory, generated-client paths, parity row shape, or drift baseline contents.
11. Given CI diagnostics are visible outside the local developer machine, when a gate fails, then logs include only safe provenance such as operation IDs, schema pointers, repository-relative paths, normalized content hashes, and gate names; they must not include file contents, diffs, provider tokens, credential material, raw provider payloads, generated context payloads, local absolute paths, production URLs, tenant data, or unauthorized resource hints.
12. Given downstream implementation may find missing server OpenAPI emission, missing NSwag outputs, missing parity oracle output, or an uninitialized previous-spine baseline, when a prerequisite is absent, then this story records exact prerequisite drift and fails closed instead of inventing partial public API, SDK, or parity semantics.
13. Given `tests/fixtures/previous-spine.yaml` may still contain the synthetic placeholder `operations: []`, when the symmetric drift gate runs before an approved prior-spine baseline exists, then it reports `prerequisite-drift` for `tests/fixtures/previous-spine.yaml` and fails closed before treating the placeholder as an accepted no-operation baseline.
14. Given server-vs-spine validation must not compare the Contract Spine to itself, when the gate is implemented, then it uses a repository-local server-emitted OpenAPI artifact or explicitly configured generation command as the server source; if no offline server source exists without Aspire, Dapr, credentials, live providers, or nested submodules, it reports `prerequisite-drift` and fails closed.
15. Given generated outputs must be stable across developer machines and CI runners, when NSwag, spine normalization, or parity generation is invoked, then the implementation pins the command, working directory, tool/config path, version source, output paths, line-ending normalization, and timestamp/banner handling used for deterministic comparison.
16. Given maintainers need actionable CI failures, when any contract/generated-artifact gate fails, then it emits one of the bounded categories `contract-spine-drift`, `server-spine-mismatch`, `previous-spine-drift`, `generated-client-drift`, `parity-oracle-mismatch`, `generation-nondeterminism`, or `prerequisite-drift` with repository-relative path, operation/schema identifier when available, gate name, and remediation hint.

## Tasks / Subtasks

- [ ] Confirm CI gate prerequisites and current artifact ownership. (AC: 1, 8, 10, 12)
  - [ ] Inspect `.github/workflows/`; if it is still absent, create the first workflow file only for this story's contract/generated-artifact gates and do not add unrelated release or security workflows.
  - [ ] Inspect `global.json`, `Directory.Packages.props`, `Hexalith.Folders.slnx`, and `tests/README.md` for the current restore/build/test lane.
  - [ ] Inspect `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`.
  - [ ] Inspect Story 1.10 through Story 1.13 artifacts before assuming generated-client paths, parity-oracle commands, or operation inventory.
  - [ ] Inspect `tests/fixtures/previous-spine.yaml`, `tests/fixtures/parity-contract.schema.json`, and `tests/fixtures/parity-contract.yaml` if present.
  - [ ] Treat absent generated SDK output, absent parity oracle output, absent server OpenAPI emission, or placeholder-only previous-spine content as prerequisite drift unless the implemented gate can fail closed with a clear developer message.
  - [ ] Build a prerequisite matrix covering the current Contract Spine, server OpenAPI source, previous-spine baseline, Story 1.12 generated-client output and NSwag config/version, Story 1.13 parity oracle output and schema, workflow file, and local gate command entry point.
  - [ ] Do not initialize or update nested submodules. If submodules are needed for local validation, initialize only the root-level modules listed in `AGENTS.md`.
- [ ] Wire the GitHub Actions contract/generated-artifact workflow. (AC: 1, 7, 8, 9, 11)
  - [ ] Add or update `.github/workflows/ci.yml` or a focused `.github/workflows/contract-spine.yml` following existing Hexalith naming if present.
  - [ ] Use `actions/checkout` and `actions/setup-dotnet` with pinned major versions current at implementation time, and configure setup from the repository `global.json` rather than an implicit runner SDK.
  - [ ] Run from the repository root with explicit commands: `dotnet restore Hexalith.Folders.slnx`, `dotnet build Hexalith.Folders.slnx`, and focused contract/generated-artifact test commands.
  - [ ] Keep NuGet caching optional and deterministic. If caching is enabled through `setup-dotnet`, require lock-file support or document why caching is deferred; do not introduce cache keys that contain tenant data, absolute paths, or mutable timestamps.
  - [ ] Keep all workflow diagnostics repository-relative and metadata-only.
  - [ ] Do not add release publishing, package signing, live provider drift, Dapr policy conformance, redaction sentinel, cache-key tenant-prefix, C6 matrix, exit-criteria, or safety-invariant jobs in this story.
- [ ] Add or wire local gate commands that CI can call. (AC: 1, 2, 3, 4, 5, 6, 7, 12)
  - [ ] Prefer repository scripts or focused test entry points that developers can run locally before pushing.
  - [ ] For server-vs-spine validation, compare normalized server-emitted OpenAPI against the Contract Spine. If server emission does not exist yet, add a fail-closed placeholder test or script with an explicit prerequisite-drift diagnostic rather than mocking success.
  - [ ] For symmetric drift detection, compare current Contract Spine operations against `tests/fixtures/previous-spine.yaml` and fail removed or incompatible operations without approved deprecation evidence.
  - [ ] If `previous-spine.yaml` still has placeholder `operations: []`, fail closed as `prerequisite-drift` and do not treat the empty operation set as an approved public baseline.
  - [ ] For NSwag golden-file consistency, re-run the Story 1.12 generation command and fail when generated output has a diff. Use `git diff --exit-code` only against generated-client/helper paths owned by Story 1.12.
  - [ ] Pin deterministic generation inputs for NSwag and parity checks: command, working directory, tool/config path, version source, output paths, line endings, and timestamp/banner handling.
  - [ ] For parity schema validation, validate `tests/fixtures/parity-contract.yaml` against `tests/fixtures/parity-contract.schema.json` before parity tests consume rows.
  - [ ] For parity completeness, assert every current OpenAPI operation appears once and only once in `parity-contract.yaml`.
  - [ ] If any Story 1.12 or Story 1.13 command is not yet implemented, record the exact missing command/path and make the gate fail closed with a targeted message.
- [ ] Add focused tests for gate behavior. (AC: 2, 3, 4, 5, 6, 7, 11, 12)
  - [ ] Add tests under the most appropriate existing project, likely `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` for OpenAPI/parity drift gates and `tests/Hexalith.Folders.Client.Tests/` for generated-client golden-file checks.
  - [ ] Verify server-vs-spine comparison fails on operation ID drift, path/method drift, required header drift, Problem Details category drift, and extension metadata drift.
  - [ ] Verify symmetric drift fails for removed operations, renamed operation IDs, changed mutating/read classification, and missing deprecation evidence.
  - [ ] Verify generated-client golden-file detection fails on stale generated output and ignores only explicitly documented normalization artifacts.
  - [ ] Verify parity schema validation fails on missing required row fields, unknown adapter names, unknown canonical error categories, duplicate operations, and rows for non-existent operation IDs.
  - [ ] Verify placeholder baselines, missing server OpenAPI sources, malformed spines, stale generated clients, stale parity oracle rows, additive-only changes, breaking changes, reorder-only changes, metadata-only changes, and redacted-diagnostic cases.
  - [ ] Verify repeated gate execution is deterministic and does not rewrite files unless the developer intentionally runs a generation command.
  - [ ] Verify failure diagnostics do not include raw payloads, file contents, diffs, tokens, credentials, local absolute paths, production URLs, tenant seed values, or unauthorized resource hints.
- [ ] Document developer and CI usage. (AC: 1, 7, 8, 11, 12)
  - [ ] Add or update focused documentation, such as `docs/contract/contract-spine-ci-gates.md`, with local commands, CI job names, owned inputs, owned outputs, and expected failure categories.
  - [ ] Document the bounded failure categories: `contract-spine-drift`, `server-spine-mismatch`, `previous-spine-drift`, `generated-client-drift`, `parity-oracle-mismatch`, `generation-nondeterminism`, and `prerequisite-drift`.
  - [ ] Document one local gate command block that mirrors the GitHub Actions steps and states the offline prerequisites; it must not require Aspire, Dapr, credentials, live providers, or nested submodules.
  - [ ] Document how to refresh generated SDK output and parity oracle output without hand-editing generated files or parity rows.
  - [ ] Document how `previous-spine.yaml` is updated only when a new public baseline is approved, and how deprecation evidence is recorded.
  - [ ] Document prerequisite drift separately from test failure so developers know whether to run Story 1.12, Story 1.13, server OpenAPI work, or baseline approval work next.
  - [ ] Document that Stories 1.15 and 1.16 own safety invariant, exit-criteria, and parity-completeness release gates that are outside this story.
- [ ] Run verification. (AC: 1, 5, 7, 8, 9, 11)
  - [ ] Run the focused gate tests added or changed by this story.
  - [ ] Run the workflow-equivalent local commands from the repository root.
  - [ ] Run `dotnet build Hexalith.Folders.slnx` if active Story 1.10 work and prerequisite drift allow it; if blocked, record the exact blocker.
  - [ ] Confirm no nested submodules were initialized and no unrelated active Story 1.10 files were modified.
  - [ ] Confirm generated artifacts are either unchanged after validation or intentionally regenerated by their owner commands with deterministic diffs.

## Dev Notes

### Scope Boundaries

- This story wires CI and local gate entry points for Contract Spine drift, generated-client golden-file consistency, symmetric drift, and parity schema validation.
- Allowed implementation areas are:

```text
.github/workflows/
tests/Hexalith.Folders.Contracts.Tests/OpenApi/
tests/Hexalith.Folders.Client.Tests/
tests/tools/
docs/contract/contract-spine-ci-gates.md
tests/fixtures/previous-spine.yaml
tests/fixtures/parity-contract.yaml
```

- Equivalent file names are acceptable when they preserve the same ownership boundaries.
- `tests/fixtures/previous-spine.yaml` may be updated only as an approved baseline or deprecation-evidence artifact. Do not add partial public API semantics as a shortcut.
- `tests/fixtures/parity-contract.yaml` may be regenerated only through the Story 1.13 oracle command. Do not hand-author parity rows in this story.
- Generated SDK output may be regenerated only through the Story 1.12 generation command. Do not manually edit files under `src/Hexalith.Folders.Client/Generated/`.
- Do not implement server runtime behavior, EventStore commands, domain aggregates, provider adapters, CLI commands, MCP tools, UI pages, safety-invariant gates, exit-criteria gates, release publishing, live provider drift jobs, or repair automation.

### Current Repository State To Inspect

- `.github/workflows/` is currently absent. The first workflow created here should be focused and should not silently become the full release pipeline.
- `global.json` pins SDK `10.0.103` with `rollForward: latestPatch`.
- `Directory.Packages.props` uses central package management and already includes `YamlDotNet` for test tooling.
- `tests/README.md` defines the current blocking lane as restore, build, and test from `Hexalith.Folders.slnx`, with future CI gates called out for parity schema, C6 matrix, redaction sentinel, cache-key tenant prefix, and provider drift.
- `src/Hexalith.Folders.Client/` currently contains only scaffold files in the working tree unless Story 1.12 has already generated client output by implementation time.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` already contains focused contract-group tests and is the likely home for OpenAPI gate tests.
- `tests/Hexalith.Folders.Client.Tests/` currently contains client scaffold smoke tests and is the likely home for generated-client golden-file tests.
- `tests/fixtures/previous-spine.yaml` is currently a synthetic placeholder with `operations: []`.
- `tests/fixtures/parity-contract.schema.json` declares draft 2020-12 row validation and bounded adapter/error/failure-kind enums.
- Active Story 1.10 review work may leave dirty OpenAPI, contract doc, and contract-test files. This story must inspect current state but must not absorb Story 1.10 implementation scope.

### Gate Requirements

- Treat `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` as the canonical Contract Spine source of truth.
- Server-vs-spine comparison must be normalized and deterministic. Normalize formatting/order only where semantics are unchanged; do not hide operation, schema, required-header, Problem Details, or `x-hexalith-*` metadata drift.
- Symmetric drift must detect removed operations and incompatible changes relative to `previous-spine.yaml`. An approved deprecation entry is required before a removal can pass.
- NSwag golden-file validation must re-run generation and compare only generated-client/helper paths. It must not run broad `git diff --exit-code` across unrelated dirty development files.
- Parity schema validation must run before any parity row consumer tests. Completeness must compare the current OpenAPI operation set to generated rows, not to a hard-coded operation list.
- Gate commands must fail closed with actionable prerequisite-drift diagnostics when Story 1.12, Story 1.13, server OpenAPI emission, or previous-spine baseline work is missing.
- Diagnostics may include operation IDs, schema JSON pointers, extension names, normalized content hashes, gate names, and repository-relative paths only.
- Failure categories are bounded to `contract-spine-drift`, `server-spine-mismatch`, `previous-spine-drift`, `generated-client-drift`, `parity-oracle-mismatch`, `generation-nondeterminism`, and `prerequisite-drift`.
- The server-vs-spine gate must use a repository-local server OpenAPI source distinct from the checked-in Contract Spine. If no offline source exists, it must fail closed as `prerequisite-drift` rather than comparing the spine to itself.
- The placeholder `tests/fixtures/previous-spine.yaml` shape with `operations: []` is not an approved public baseline. Until a real prior-spine baseline is present, the symmetric drift gate must fail closed as `prerequisite-drift`.

### Previous Story Intelligence

- Story 1.3 reserved `tests/fixtures/previous-spine.yaml`, `tests/fixtures/parity-contract.schema.json`, and `tests/tools/parity-oracle-generator/` as synthetic placeholders.
- Story 1.5 made idempotency equivalence, parser-policy classifications, and adapter behavioral parity rules authoritative in `docs/contract/idempotency-and-parity-rules.md`.
- Story 1.6 created the Contract Spine foundation and extension vocabulary; this story must reuse extension names instead of redefining CI-only metadata.
- Stories 1.7 through 1.11 author operation groups. This story verifies those operations; it does not author new operation groups.
- Story 1.12 owns NSwag generation and idempotency helper generation. This story gates its output and must consume its command/paths instead of reimplementing SDK generation.
- Story 1.13 owns the C13 parity oracle. This story validates and gates the oracle output but must not hand-author rows or duplicate oracle derivation logic.
- Story 1.13 records that `tests/fixtures/parity-contract.schema.json` already requires `operation_id`, `operation_family`, `read_consistency_class`, `transport_parity`, `behavioral_parity`, `adapter_expectations`, and `ownership`.
- The latest commits show ongoing contract-group work and predev story creation. Expect active OpenAPI and contract-test churn; keep this story's staging and assertions scoped.

### Latest Technical Notes

- Current `actions/setup-dotnet` documentation shows v5 examples with `actions/checkout@v6`, supports reading SDK versions from `global-json-file`, and warns that implicit runner SDK selection can vary when a concrete `global.json` is not used. Source: https://github.com/actions/setup-dotnet
- `actions/setup-dotnet` NuGet caching is optional and off by default; when enabled, it expects NuGet lock files or an explicit `cache-dependency-path`. Defer caching rather than adding unstable cache behavior if lock files are not present. Source: https://github.com/actions/setup-dotnet
- GitHub Actions workflow syntax supports jobs composed of `uses` steps and `run` command steps; keep gate commands visible and locally reproducible. Source: https://docs.github.com/actions/learn-github-actions/workflow-syntax-for-github-actions
- Microsoft .NET CLI documentation states `dotnet build` and `dotnet test` perform implicit restore, but this repository's CI lane should keep explicit `dotnet restore Hexalith.Folders.slnx` first so package and lock-file failures are isolated. Sources: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build and https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test

### Testing Guidance

- Tests must run offline and must not require Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant seed data, production secrets, network calls, or initialized nested submodules.
- Prefer structured YAML parsing with `YamlDotNet.RepresentationModel`, matching existing OpenAPI tests.
- Keep test fixtures synthetic and metadata-only. Do not add real tenant names, repository URLs, branch names with sensitive values, file contents, diffs, raw provider responses, tokens, credentials, or local absolute paths.
- Good negative cases are removed operation without deprecation, operation row missing from parity oracle, duplicate parity row, stale generated-client file, server-vs-spine operation drift, unknown canonical error category, unknown MCP failure kind, missing read-consistency metadata for queries, and mutating operation without idempotency metadata.
- Local workflow-equivalent verification should be documented so CI failure can be reproduced without GitHub Actions.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.14: Wire Contract Spine drift and generated-client CI gates`
- `_bmad-output/planning-artifacts/architecture.md#Decision Impact Analysis`
- `_bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Handoff`
- `_bmad-output/project-context.md`
- `_bmad-output/implementation-artifacts/1-12-wire-nswag-sdk-generation-with-idempotency-helpers.md`
- `_bmad-output/implementation-artifacts/1-13-generate-the-c13-parity-oracle.md`
- `tests/README.md`
- `global.json`
- `Directory.Packages.props`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`
- `tests/fixtures/previous-spine.yaml`
- `tests/fixtures/parity-contract.schema.json`
- `AGENTS.md#Git Submodules`
- GitHub Actions workflow syntax: https://docs.github.com/actions/learn-github-actions/workflow-syntax-for-github-actions
- actions/setup-dotnet: https://github.com/actions/setup-dotnet
- .NET CLI build and test: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build and https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test

## Project Structure Notes

- Workflow files belong under `.github/workflows/`.
- Contract/OpenAPI gate tests should prefer `tests/Hexalith.Folders.Contracts.Tests/OpenApi/`.
- Generated-client gate tests should prefer `tests/Hexalith.Folders.Client.Tests/`.
- Gate helper scripts, if needed, belong under `tests/tools/` or another clearly test-owned tooling path.
- Human-readable gate documentation may live at `docs/contract/contract-spine-ci-gates.md`.
- Do not place CI gate implementation inside runtime projects unless it is a build target already owned by Story 1.12 or Story 1.13.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-13 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |
| 2026-05-15 | Applied party-mode review hardening for fail-closed prerequisites, deterministic gate inputs, failure taxonomy, and local/CI command parity. | Codex |

## Party-Mode Review

- Date: 2026-05-15T07:06:16Z
- Selected story key: 1-14-wire-contract-spine-drift-and-generated-client-ci-gates
- Command/skill invocation used: `/bmad-party-mode 1-14-wire-contract-spine-drift-and-generated-client-ci-gates; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: Review found that the story scope was sound, but implementation could pass with false confidence unless placeholder previous-spine behavior, server OpenAPI source selection, deterministic generation inputs, bounded failure categories, prerequisite drift handling, and local/CI command parity were made explicit.
- Changes applied: Added acceptance criteria and task guidance for `previous-spine.yaml` placeholder fail-closed behavior, non-self server-vs-spine comparison, deterministic NSwag/parity generation inputs, bounded failure taxonomy, prerequisite matrix coverage, redacted diagnostic expectations, and local gate documentation parity with GitHub Actions.
- Findings deferred: Detailed fixture taxonomy, exact command names, workflow job names, and diagnostic string examples remain implementation decisions within this story's existing scope; Story 1.15 safety invariant gates and Story 1.16 exit-criteria/release gates remain out of scope.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
