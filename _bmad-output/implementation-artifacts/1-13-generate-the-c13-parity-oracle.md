# Story 1.13: Generate the C13 parity oracle

Status: ready-for-dev

Created: 2026-05-13

## Story

As a maintainer,
I want the C13 parity oracle generated from the Contract Spine,
so that cross-surface tests consume one source of truth for transport and behavioral parity.

## Acceptance Criteria

1. Given the Contract Spine declares parity metadata, when the parity-oracle generator runs, then `tests/fixtures/parity-contract.yaml` is generated from `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` rather than hand-authored operation rows.
2. Given generated rows are produced, when validation runs, then every row schema-validates against `tests/fixtures/parity-contract.schema.json` and validation fails on missing required transport or behavioral columns.
3. Given the Contract Spine contains mutating commands, when generation runs, then every mutating command has a parity row with `idempotency_key_rule` derived from OpenAPI idempotency metadata and generation fails closed if required mutating metadata is missing.
4. Given the Contract Spine contains non-mutating operations, when generation runs, then every query, status, audit, context-query, and operations-console projection row has `read_consistency_class` and `idempotency_key_rule: not_accepted_for_non_mutating_operation`.
5. Given an operation declares `x-hexalith-parity-dimensions`, `x-hexalith-canonical-error-categories`, `x-hexalith-audit-metadata-keys`, `x-hexalith-correlation`, `x-hexalith-authorization`, idempotency, lifecycle, or read-consistency metadata, when a parity row is generated, then transport columns (`auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`) are derived from those declarations or reported as prerequisite drift.
6. Given SDK, CLI, and MCP adapters need behavior beyond REST transport shape, when rows are generated, then behavioral columns (`pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `task_id_sourcing`, `credential_sourcing`, `cli_exit_code`, `mcp_failure_kind`) are populated from `docs/contract/idempotency-and-parity-rules.md` and the architecture Adapter Parity Contract.
7. Given operation removals can silently break test parity, when generation compares the current Contract Spine to `tests/fixtures/previous-spine.yaml`, then operation identity is `HTTP method + normalized path + operationId`, and added, removed, renamed, request/response schema, status-code, idempotency, read-consistency, and operation-family changes produce deterministic symmetric-drift diagnostics unless an approved deprecation entry exists.
8. Given generated rows are downstream test inputs, when the generator writes `tests/fixtures/parity-contract.yaml`, then row ordering is deterministic by operation ID, array values and diagnostics are sorted where the schema does not require source order, serialization uses stable UTF-8 and LF formatting, wall-clock timestamps are excluded or normalized, and rerunning generation twice produces byte-stable output.
9. Given lifecycle and negative/error cases are needed for downstream conformance tests, when generation runs, then it emits or references golden lifecycle fixtures plus negative/error contract cases for safe denial, idempotency conflict, validation, provider outcome, read-model unavailable, redaction, and state-transition-invalid scenarios.
10. Given parity evidence must remain metadata-only, when rows, diagnostics, examples, and tests are inspected, then diagnostics may name operation IDs, repository-relative source file paths, schema fields, status labels, and bounded category names, but they contain no file contents, diffs, provider tokens, credential material, raw provider payloads, generated context payloads, local filesystem paths, production URLs, tenant data, request/response headers, sampled API bodies, environment values, or unauthorized resource hints.
11. Given `tests/fixtures/parity-contract.schema.json` is currently a seeded row schema, when implementation needs schema changes for final C13 row shape, then schema updates stay backward-auditable, keep adapter and failure-kind enums bounded, and are covered by focused schema tests.
12. Given Story 1.14 owns CI wiring, when Story 1.13 completes, then it may add local generator commands and focused tests, but it does not modify GitHub Actions workflows or release gates.
13. Given Story 1.12 owns NSwag client generation and idempotency helpers, when Story 1.13 consumes operation identities or helper metadata, then it reuses generated operation IDs and helper provenance instead of reimplementing SDK hash construction policy.
14. Given some C3/C4/S-2/C6 values may still be reference-pending, when generation encounters an explicitly reference-pending value, then it carries a bounded `reference_pending` marker only when the source contract names the unresolved decision and the schema allows the marker; otherwise it fails with a clear prerequisite-drift diagnostic. The generator must not invent final policy values or allow unbounded pending markers.
15. Given active contract work may be in progress, when implementation starts, then the developer inspects the current OpenAPI file, contract notes, existing tests, and active Story 1.7 through Story 1.12 artifacts before assuming the operation inventory or metadata names.
16. Given a maintainer changes the Contract Spine, when the local parity oracle is regenerated, then the resulting artifact and diagnostics identify which REST, SDK, CLI, MCP, and UI adapter expectations are present, missing, stale, removed, or reference-pending from one local metadata-only artifact.
17. Given canonical inputs can disagree, when OpenAPI extensions, rule-table documentation, architecture adapter rules, generated SDK helper provenance, schemas, or previous-spine baselines conflict, then generation fails with bounded `prerequisite_drift` diagnostics instead of choosing an implicit winner or inventing fallback policy.

## Tasks / Subtasks

- [ ] Confirm current Contract Spine and fixture prerequisites. (AC: 1, 3, 4, 5, 11, 14, 15, 17)
  - [ ] Inspect `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`.
  - [ ] Inspect `docs/contract/idempotency-and-parity-rules.md`, `docs/contract/*contract-groups.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/previous-spine.yaml`, and `tests/tools/parity-oracle-generator/README.md`.
  - [ ] Inspect Story 1.7 through Story 1.12 artifacts for downstream ownership notes and reference-pending decisions before editing generator behavior.
  - [ ] Treat missing OpenAPI extensions, missing operation IDs, malformed parity metadata, unresolved `$ref`, unsupported schema shape, or inconsistent docs-vs-spine rows as prerequisite drift.
  - [ ] Build and document a source-authority matrix before deriving rows: OpenAPI operation metadata owns transport facts, rule tables and architecture own adapter semantics, schemas own allowed row shape, generated SDK helper provenance owns helper identity only, and `previous-spine.yaml` owns removal/deprecation comparison.
  - [ ] Fail with metadata-only `prerequisite_drift` when canonical sources disagree or when a required source is missing for a non-reference-pending field.
  - [ ] Do not initialize or update nested submodules.
- [ ] Implement the parity-oracle generator under the existing tool path. (AC: 1, 2, 5, 6, 8, 10, 11, 14, 17)
  - [ ] Replace the placeholder-only `tests/tools/parity-oracle-generator/README.md` ownership note with implementation documentation while preserving ownership and non-leakage rules.
  - [ ] Add generator source under `tests/tools/parity-oracle-generator/` using the repo's .NET conventions unless a smaller script is explicitly justified by existing tooling.
  - [ ] Read OpenAPI with structured YAML parsing; do not derive rows through ad hoc line matching.
  - [ ] Resolve local OpenAPI `$ref` pointers deterministically and fail on unresolved external references, ambiguous schema metadata, or conflicting canonical inputs.
  - [ ] Emit `tests/fixtures/parity-contract.yaml` deterministically from repository-relative inputs.
  - [ ] Include safe provenance such as generator version, Contract Spine content hash, schema hash, source file names, and generated timestamp policy only if it remains byte-stable or explicitly normalized.
  - [ ] Keep generated output metadata-only and synthetic where examples are needed.
- [ ] Derive transport parity columns from canonical sources. (AC: 3, 4, 5, 7, 9, 14)
  - [ ] Classify operation families from HTTP method, path, operation ID, and existing rule tables: mutating command, query/status, context query, audit, or operations-console projection.
  - [ ] Require `Idempotency-Key` and `x-hexalith-idempotency-equivalence` for mutating operations; require no idempotency key for non-mutating operations.
  - [ ] Derive `read_consistency_class` from `x-hexalith-read-consistency` for non-mutating operations.
  - [ ] Derive `error_code_set` from `x-hexalith-canonical-error-categories` and fail if an operation lacks a bounded category set.
  - [ ] Derive `audit_metadata_keys` from `x-hexalith-audit-metadata-keys` or documented operation-rule rows; do not include raw values.
  - [ ] Derive correlation field path from the canonical headers and Problem Details fields, preserving `X-Correlation-Id` and `X-Hexalith-Task-Id` semantics.
  - [ ] Derive terminal states from lifecycle metadata where available and from bounded lifecycle/status schemas only when the source is explicit.
  - [ ] Compare current operations with `tests/fixtures/previous-spine.yaml`; fail removed operations unless the baseline records an approved deprecation window.
- [ ] Derive behavioral parity columns without duplicating adapter semantics. (AC: 6, 9, 10, 13)
  - [ ] Consume the Adapter Parity Contract from `_bmad-output/planning-artifacts/architecture.md` and the stable rule tables in `docs/contract/idempotency-and-parity-rules.md`.
  - [ ] Map SDK pre-call failures separately from server-returned Problem Details so pre-SDK and post-SDK errors cannot both apply to one case.
  - [ ] Map CLI exit codes exactly to canonical categories: 0 success, 64 client configuration or usage, 65 credential missing, 66 tenant access denied, 67 workspace locked, 68 idempotency conflict, 69 validation error, 70 known provider failure, 71 unknown provider outcome, 72 reconciliation required, 73 not found, 74 state transition invalid, 75 redacted, 1 internal error.
  - [ ] Map MCP failure kinds one-to-one to canonical categories; do not collapse categories for convenience.
  - [ ] Preserve SDK caller/provider idempotency sourcing, CLI `--idempotency-key` or `--allow-auto-key`, MCP `idempotencyKey`, correlation sourcing, task ID sourcing, and credential sourcing.
  - [ ] Do not generate CLI commands, MCP tools, SDK client code, or adapter wrappers in this story.
- [ ] Add local validation and generator tests. (AC: 2, 3, 4, 7, 8, 9, 10, 11, 14, 17)
  - [ ] Add focused tests under the most appropriate existing test project, likely `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` or a small tool test project if needed.
  - [ ] Verify all current Contract Spine operations appear exactly once in generated parity rows.
  - [ ] Verify generated rows validate against `tests/fixtures/parity-contract.schema.json`.
  - [ ] Verify mutating operations fail when idempotency metadata is missing, malformed, duplicated, not lexicographically ordered where required, or inconsistent with HTTP method.
  - [ ] Verify non-mutating operations fail if they accept idempotency keys or lack read-consistency metadata.
  - [ ] Verify symmetric drift detection catches removed operations when `previous-spine.yaml` lacks an approved deprecation entry.
  - [ ] Verify deterministic output by running the generator twice and comparing normalized bytes.
  - [ ] Verify diagnostics and generated fixtures do not contain forbidden leak patterns such as raw content, diffs, provider tokens, credential material, local absolute paths, production URLs, tenant seed values, or unauthorized resource hints.
  - [ ] Verify reference-pending values are either schema-bounded or fail as prerequisite drift; do not allow silent defaults.
  - [ ] Verify source-authority conflicts fail deterministically, including OpenAPI-vs-rule-table mismatch, schema enum mismatch, missing helper provenance where required, and stale or placeholder previous-spine baselines.
  - [ ] Maintain an AC-to-test matrix that maps each acceptance criterion to a fixture input, expected generated row or diagnostic, negative case, and test file.
- [ ] Record downstream handoff and negative scope. (AC: 10, 12, 13, 15)
  - [ ] Document the generator command, input files, output file, deterministic-output policy, schema-validation command, and expected developer workflow.
  - [ ] Document that Story 1.14 owns CI workflow wiring, server-vs-spine validation, generated-client consistency gates, and release gates.
  - [ ] Document that Epic 5 owns consuming the oracle in SDK, REST, CLI, and MCP tests.
  - [ ] Document that Epic 4 owns runtime idempotency persistence, workspace lifecycle execution, provider side effects, and reconciliation.
  - [ ] Document any prerequisite drift discovered during implementation with exact source file and operation ID.
- [ ] Run verification. (AC: 1, 2, 8, 10, 12)
  - [ ] Run the focused generator and schema-validation tests.
  - [ ] Run the parity generator twice and verify generated output is byte-stable.
  - [ ] Run `dotnet test` for the affected test project.
  - [ ] Run `dotnet build Hexalith.Folders.slnx` if the current active contract work allows it; if blocked by unrelated in-progress Story 1.10 changes or prerequisite drift, record the exact blocker.

## Dev Notes

### Scope Boundaries

- This story creates the local C13 parity-oracle generator and generated fixture output.
- Allowed implementation areas are:

```text
tests/tools/parity-oracle-generator/
tests/fixtures/parity-contract.yaml
tests/fixtures/parity-contract.schema.json
tests/fixtures/previous-spine.yaml
tests/Hexalith.Folders.Contracts.Tests/OpenApi/
docs/contract/parity-oracle-generator.md
```

- Equivalent file names are acceptable if they preserve the same ownership boundaries.
- Do not add runtime REST handlers, EventStore commands, domain aggregates, provider adapters, Git or filesystem side effects, SDK generated clients, NSwag generation wiring, CLI commands, MCP tools, UI pages, CI workflows, release gates, repair automation, or nested-submodule initialization.
- `Hexalith.Folders.Contracts` remains behavior-free. Generator behavior belongs under test/tooling paths, not inside the contracts library.
- Story 1.14 owns CI gate wiring. Story 1.13 may provide commands and tests that CI can later call.

### Current Repository State To Inspect

- `tests/tools/parity-oracle-generator/README.md` is a placeholder from Story 1.3 and explicitly says no generator code or final oracle semantics exist yet.
- `tests/fixtures/parity-contract.schema.json` is a seeded JSON Schema for row shape. It already requires `operation_id`, `operation_family`, `read_consistency_class`, `transport_parity`, `behavioral_parity`, `adapter_expectations`, and `ownership`.
- `tests/fixtures/previous-spine.yaml` is a synthetic placeholder with `operations: []`; implementation must decide whether this remains a harmless first-baseline seed or must be replaced with a captured spine snapshot before symmetric drift becomes meaningful.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` currently contains operation groups from Stories 1.7 through 1.11 and active Story 1.10 review work may be dirty.
- `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml` defines `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-correlation`, `x-hexalith-authorization`, `x-hexalith-canonical-error-categories`, `x-hexalith-read-consistency`, `x-hexalith-lifecycle-states`, `x-hexalith-parity-dimensions`, `x-hexalith-audit-metadata-keys`, and sensitivity metadata vocabulary.
- `Directory.Packages.props` already centralizes `YamlDotNet` for tests. Keep any new package versions centralized.

### Current Operation Inventory Snapshot

The developer must regenerate this from the current OpenAPI file at implementation time. At story creation, the observed inventory includes these operation IDs:

```text
AddFile
ArchiveFolder
BindRepository
ChangeFile
CommitWorkspace
ConfigureBranchRefPolicy
ConfigureProviderBinding
CreateFolder
CreateRepositoryBackedFolder
GetCommitEvidence
GetEffectivePermissions
GetFolderFileMetadata
GetFolderLifecycleStatus
GetProviderBinding
GetProviderOutcome
GetProviderSupportEvidence
GetReconciliationStatus
GetRepositoryBinding
GetTaskStatus
GetWorkspaceLock
GetWorkspaceRetryEligibility
GetWorkspaceStatus
GetWorkspaceTransitionEvidence
GlobFolderFiles
ListFolderAclEntries
ListFolderFiles
LockWorkspace
PrepareWorkspace
ReadFileRange
ReleaseWorkspaceLock
RemoveFile
SearchFolderFiles
UpdateFolderAclEntry
ValidateProviderReadiness
```

Do not freeze this list in code. Tests should derive the current operation set from the OpenAPI file and compare it with generated rows.

### Generator Requirements

- Use structured YAML parsing. Existing contract tests use `YamlDotNet.RepresentationModel`; reusing that avoids a new parser and keeps OpenAPI traversal close to current tests.
- Resolve local JSON Pointer references such as `#/components/...`; reject unresolved or external references unless the story explicitly documents a safe bounded fallback.
- Generate one row per operation ID. Duplicate operation IDs are a hard failure.
- Keep row order deterministic by operation ID and keep deterministic serialization settings under source control.
- Apply the documented source-authority matrix consistently. The generator may combine canonical sources, but it must not use docs, generated SDK implementation bodies, or inferred naming conventions to override explicit OpenAPI extension metadata.
- Treat `tests/fixtures/previous-spine.yaml` with `operations: []` as a synthetic placeholder. Removal detection must fail closed unless implementation replaces it with a captured baseline or records an explicit, test-covered first-baseline initialization mode.
- Treat operation identity as `HTTP method + normalized path + operationId`; emit deterministic diagnostics for added, removed, renamed, request/response schema, status-code, idempotency, read-consistency, and operation-family drift.
- Keep generated `operation_id` values aligned with OpenAPI `operationId`; do not invent adapter-specific names.
- Keep adapter expectations bounded to `rest`, `sdk`, `cli`, `mcp`, and `ui` as allowed by the schema. UI should appear only for operations-console projection rows where explicitly documented.
- Mutating commands are POST, PUT, PATCH, or DELETE operations that declare mutating idempotency metadata. Do not classify `ValidateProviderReadiness` as mutating only because it is POST; its existing contract marks it as a non-mutating provider readiness validation.
- Query/status operations must not accept `Idempotency-Key`. They must declare read consistency.
- Output and diagnostics must be repository-relative. Avoid machine-local absolute paths, timestamps, environment data, and network-derived data.
- Behavioral parity is derived only from Contract Spine metadata, `docs/contract/idempotency-and-parity-rules.md`, `_bmad-output/planning-artifacts/architecture.md`, and Story 1.12 helper provenance artifacts. Do not inspect runtime handlers, providers, live endpoints, generated SDK implementation bodies, CLI commands, or MCP tools to infer behavior.
- The README or handoff documentation must state the local command, input files, output path, schema-validation command, deterministic-output policy, exit behavior, and what Story 1.14 should wire into CI later.

### Oracle Contract

- The generated oracle should expose a stable top-level shape for metadata/provenance, operation rows, diagnostics, drift entries, and fixture references when the schema permits those sections.
- Each row and diagnostic should carry repository-relative source pointers to the canonical inputs used for derivation, but no raw file contents or generated payload excerpts.
- Diagnostics must use bounded levels such as `error`, `warning`, and `reference_pending`; new levels require schema updates and focused schema tests.
- Drift entries must be sorted by operation identity and drift category, and must include only metadata-safe source pointers.
- `reference_pending` is allowed only for explicitly documented unresolved C3/C4/S-2/C6 values. It must identify the source decision and owning criterion without raw payloads, tenant data, credentials, or local machine paths.
- Missing `previous-spine.yaml`, invalid Contract Spine YAML, unresolved local references, duplicate operation IDs, and operationId mismatches are fail-closed generator errors.
- Positive and negative fixtures for this story are limited to generator, schema, drift, lifecycle/status, safe denial, idempotency conflict, validation, provider outcome, read-model unavailable, redaction, and state-transition-invalid metadata cases. Runtime adapter behavior remains out of scope.

### Transport Parity Rules

- `auth_outcome_class` must come from operation authorization and safe-denial metadata, not from English error messages.
- `error_code_set` must come from `x-hexalith-canonical-error-categories` and remain bounded by the schema enum.
- `idempotency_key_rule` must distinguish required mutating keys from non-mutating operations that reject idempotency keys.
- `audit_metadata_keys` must contain metadata key names only. Tenant-sensitive classifications may guide tests, but rows must not include raw metadata values.
- `correlation_field_path` should reference canonical locations such as `headers.X-Correlation-Id`, `headers.X-Hexalith-Task-Id`, `problem.correlationId`, `result.correlationId`, or `metadata.correlationId`.
- `terminal_states` should be explicit lifecycle/status labels only. Do not infer terminality from prose unless the source table or schema names the state.
- Removed operations require approved deprecation evidence in `previous-spine.yaml`; otherwise generation fails.

### Behavioral Parity Rules

- SDK and REST transport parity does not eliminate CLI and MCP behavioral parity. CLI and MCP wrap the SDK but still have distinct pre-SDK validation, credential sourcing, idempotency-key sourcing, correlation sourcing, task ID sourcing, exit-code mapping, and failure-kind mapping.
- CLI exit codes are canonical and must not be remapped for convenience.
- MCP failure kinds are one-to-one with canonical categories and must not collapse multiple categories into `usage_error` or `internal_error`.
- Missing credentials, invalid local configuration, malformed local command input, and missing task/idempotency fields are pre-SDK failures. Server-returned RFC 9457 Problem Details are post-SDK failures.
- For one scenario, a row must not require both a pre-SDK failure and a post-SDK failure. Tests should catch this contradiction.
- Correlation IDs are caller/provider/generated according to adapter rules; caller-supplied correlation IDs must echo unchanged across surfaces.
- Task IDs are required only for task-scoped operations. Do not invent task scope for folder lifecycle or provider-readiness operations.

### Previous Story Intelligence

- Story 1.3 reserved `tests/tools/parity-oracle-generator/`, `tests/fixtures/parity-contract.schema.json`, and `tests/fixtures/previous-spine.yaml` as synthetic placeholders only.
- Story 1.5 made `docs/contract/idempotency-and-parity-rules.md` authoritative for operation metadata, idempotency equivalence, non-mutating read consistency, and adapter behavioral parity. It also states that envelope-derived partitioning keys such as `tenant_id` never appear in client-controlled OpenAPI equivalence lists.
- Story 1.6 created the Contract Spine foundation and extension vocabulary. Reuse extension names; do not define a parallel vocabulary inside the generator.
- Stories 1.7 through 1.11 authored operation groups and contract notes. Their docs intentionally defer final parity rows to Story 1.13.
- Story 1.12 owns NSwag SDK generation and generated `ComputeIdempotencyHash()` helpers. C13 rows should consume operation IDs and helper provenance, but must not reimplement helper hash construction policy.
- Story 1.12 party-mode and advanced-elicitation traces reinforced deterministic generated output, stale-output detection, safe provenance, and leak-safe diagnostics. Apply the same discipline to parity-oracle output.

### Latest Technical Notes

- Current NSwag documentation for C# client generation confirms the generator can emit partial async clients with `HttpClient` injection, client interfaces, exception classes, and separate contract output. This matters here because Story 1.13 must consume generated operation identities from the SDK path without editing generated SDK output. [Source: Context7 `/ricosuter/nswag` query on 2026-05-13]
- Existing project tests already use `YamlDotNet.RepresentationModel` to parse OpenAPI YAML and resolve local refs, so a parity generator can reuse those patterns without introducing another YAML library.
- JSON Schema validation should target draft 2020-12 semantics because `tests/fixtures/parity-contract.schema.json` declares `"$schema": "https://json-schema.org/draft/2020-12/schema"`.

### Testing Guidance

- Keep tests offline. They must not require Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant seed data, production secrets, network calls, or initialized nested submodules.
- Prefer focused tests around generator input parsing, row derivation, schema validation, deterministic output, drift detection, and leak-safe diagnostics.
- Reuse current OpenAPI contract-test helper patterns where practical: load `YamlStream`, enumerate operations, resolve local refs, and assert exact bounded values.
- Add negative fixture cases for missing idempotency metadata, query operations accepting idempotency keys, missing read consistency, duplicate operation IDs, unsupported references, removed operations without deprecation, and forbidden diagnostic leaks.
- Add negative fixture cases for source-authority conflicts, stale generated SDK helper provenance, schema enum drift, and placeholder previous-spine baselines.
- Include one positive minimal contract fixture, one lifecycle/negative contract fixture set, one removed-operation baseline pair, and one deterministic byte-stability test.
- Verify the generator touches only the intended output paths and leaves unrelated files unchanged.
- Keep tests resilient to operation inventory growth by deriving current operations from OpenAPI. Only assert exact operation allow-lists where a contract group owns a fixed subset.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.13: Generate the C13 parity oracle`
- `_bmad-output/planning-artifacts/architecture.md#Architecture Exit Criteria - Targets to Resolve`
- `_bmad-output/planning-artifacts/architecture.md#Adapter Parity Contract`
- `_bmad-output/planning-artifacts/architecture.md#Enforcement Guidelines`
- `_bmad-output/project-context.md`
- `docs/contract/idempotency-and-parity-rules.md`
- `docs/contract/file-context-contract-groups.md`
- `docs/contract/commit-status-contract-groups.md`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`
- `tests/fixtures/parity-contract.schema.json`
- `tests/fixtures/previous-spine.yaml`
- `tests/tools/parity-oracle-generator/README.md`
- `_bmad-output/implementation-artifacts/1-12-wire-nswag-sdk-generation-with-idempotency-helpers.md`
- `AGENTS.md#Git Submodules`
- Context7 `/ricosuter/nswag` documentation query for C# client generation settings.

## Project Structure Notes

- Generator/tooling belongs under `tests/tools/parity-oracle-generator/`.
- Generated parity rows belong in `tests/fixtures/parity-contract.yaml`.
- Row schema belongs in `tests/fixtures/parity-contract.schema.json`.
- Focused tests should live in the contracts test project unless the implementation creates a tool project that needs its own test project.
- Human-readable handoff documentation may live under `docs/contract/parity-oracle-generator.md`.
- Do not place generator behavior in `src/Hexalith.Folders.Contracts`; contracts remain DTO/schema/metadata-only.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-15 | Applied advanced-elicitation hardening for source-authority conflicts, previous-spine baseline semantics, deterministic prerequisite-drift fixtures, and metadata-only derivation evidence. | Codex |
| 2026-05-14 | Applied party-mode review clarification pass for drift semantics, deterministic output, diagnostics, reference-pending bounds, maintainer workflow, and acceptance-test mapping. | Codex |
| 2026-05-13 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Party-Mode Review

- Date: 2026-05-14T01:10:20+02:00
- Selected story: `1-13-generate-the-c13-parity-oracle`
- Command/skill invocation used: `/bmad-party-mode 1-13-generate-the-c13-parity-oracle; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Drift detection needed an explicit operation identity and deterministic drift category contract.
  - Deterministic output needed stable encoding, newline, timestamp, ordering, and byte-comparison expectations.
  - `reference_pending` needed bounded pass/fail rules instead of becoming a parking lot for unresolved policy.
  - Diagnostics needed a precise metadata-only allowlist and leak-safety denylist.
  - The maintainer workflow and Story 1.14 handoff needed clearer acceptance language.
  - Acceptance tests needed an AC-to-test matrix and explicit fixture categories.
- Changes applied:
  - Tightened AC 7, AC 8, AC 10, and AC 14.
  - Added AC 16 for the maintainer-facing parity workflow.
  - Added generator requirements for operation identity, no behavioral execution, and handoff documentation.
  - Added an Oracle Contract section for output shape, diagnostics, drift, `reference_pending`, and fail-closed cases.
  - Added testing guidance for fixture categories, byte stability, unchanged unrelated files, and AC-to-test mapping.
- Findings deferred:
  - CI wiring, release gates, server-vs-spine validation, generated-client consistency gates, and workflow enforcement remain Story 1.14 scope.
  - Runtime parity enforcement, provider-backed behavior, SDK/CLI/MCP generation changes, adapter wrappers, UI/console presentation, and localization implementation remain future-story scope.
- Final recommendation: ready-for-dev after applied story clarification pass.

## Advanced Elicitation

- Date: 2026-05-15T17:04:33+02:00
- Selected story: `1-13-generate-the-c13-parity-oracle`
- Command/skill invocation used: `/bmad-advanced-elicitation 1-13-generate-the-c13-parity-oracle`
- Batch 1 methods: Red Team vs Blue Team; Failure Mode Analysis; Self-Consistency Validation; Comparative Analysis Matrix; Critique and Refine
- Reshuffled Batch 2 methods: First Principles Analysis; Pre-mortem Analysis; Security Audit Personas; Graph of Thoughts; Active Recall Testing
- Findings summary:
  - Red-team and failure-mode review found that the story still allowed implicit source precedence when OpenAPI metadata, documentation rule tables, architecture adapter rules, schemas, generated SDK helper provenance, or previous-spine baselines disagree.
  - Self-consistency and comparative review found that `previous-spine.yaml` was named as a synthetic placeholder but did not yet force an explicit first-baseline decision before removal-drift semantics become meaningful.
  - Security and graph-of-thought review found that generated diagnostics needed source pointers for auditability while preserving the metadata-only safety boundary.
  - Active-recall review found that test expectations should include conflict fixtures, schema enum drift, stale helper provenance, and placeholder baseline handling in addition to ordinary row derivation.
- Changes applied:
  - Added AC 17 requiring bounded `prerequisite_drift` diagnostics for canonical-source conflicts instead of implicit fallback policy.
  - Added task guidance for a source-authority matrix and deterministic conflict handling.
  - Clarified generator requirements for explicit source precedence and fail-closed placeholder previous-spine behavior.
  - Clarified oracle output evidence with repository-relative source pointers and no raw payload excerpts.
  - Expanded testing guidance for source-authority conflicts, stale helper provenance, schema enum drift, and previous-spine placeholder fixtures.
- Findings deferred:
  - Choosing the exact final parity row schema fields remains implementation scope under AC 11.
  - Replacing the synthetic previous-spine baseline with a captured baseline remains implementation scope, but the story now requires an explicit test-covered decision.
  - CI workflow enforcement remains Story 1.14 scope.
- Final recommendation: ready-for-dev after applied advanced-elicitation clarification pass.

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
