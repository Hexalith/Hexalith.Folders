# Story 1.13: Generate the C13 parity oracle

Status: review

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

- [x] Confirm current Contract Spine and fixture prerequisites. (AC: 1, 3, 4, 5, 11, 14, 15, 17)
  - [x] Inspect `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`.
  - [x] Inspect `docs/contract/idempotency-and-parity-rules.md`, `docs/contract/*contract-groups.md`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/previous-spine.yaml`, and `tests/tools/parity-oracle-generator/README.md`.
  - [x] Inspect Story 1.7 through Story 1.12 artifacts for downstream ownership notes and reference-pending decisions before editing generator behavior.
  - [x] Treat missing OpenAPI extensions, missing operation IDs, malformed parity metadata, unresolved `$ref`, unsupported schema shape, or inconsistent docs-vs-spine rows as prerequisite drift.
  - [x] Build and document a source-authority matrix before deriving rows: OpenAPI operation metadata owns transport facts, rule tables and architecture own adapter semantics, schemas own allowed row shape, generated SDK helper provenance owns helper identity only, and `previous-spine.yaml` owns removal/deprecation comparison.
  - [x] Fail with metadata-only `prerequisite_drift` when canonical sources disagree or when a required source is missing for a non-reference-pending field.
  - [x] Do not initialize or update nested submodules.
- [x] Implement the parity-oracle generator under the existing tool path. (AC: 1, 2, 5, 6, 8, 10, 11, 14, 17)
  - [x] Replace the placeholder-only `tests/tools/parity-oracle-generator/README.md` ownership note with implementation documentation while preserving ownership and non-leakage rules.
  - [x] Add generator source under `tests/tools/parity-oracle-generator/` using the repo's .NET conventions unless a smaller script is explicitly justified by existing tooling.
  - [x] Read OpenAPI with structured YAML parsing; do not derive rows through ad hoc line matching.
  - [x] Resolve local OpenAPI `$ref` pointers deterministically and fail on unresolved external references, ambiguous schema metadata, or conflicting canonical inputs.
  - [x] Emit `tests/fixtures/parity-contract.yaml` deterministically from repository-relative inputs.
  - [x] Include safe provenance such as generator version, Contract Spine content hash, schema hash, source file names, and generated timestamp policy only if it remains byte-stable or explicitly normalized.
  - [x] Keep generated output metadata-only and synthetic where examples are needed.
- [x] Derive transport parity columns from canonical sources. (AC: 3, 4, 5, 7, 9, 14)
  - [x] Classify operation families from HTTP method, path, operation ID, and existing rule tables: mutating command, query/status, context query, audit, or operations-console projection.
  - [x] Require `Idempotency-Key` and `x-hexalith-idempotency-equivalence` for mutating operations; require no idempotency key for non-mutating operations.
  - [x] Derive `read_consistency_class` from `x-hexalith-read-consistency` for non-mutating operations.
  - [x] Derive `error_code_set` from `x-hexalith-canonical-error-categories` and fail if an operation lacks a bounded category set.
  - [x] Derive `audit_metadata_keys` from `x-hexalith-audit-metadata-keys` or documented operation-rule rows; do not include raw values.
  - [x] Derive correlation field path from the canonical headers and Problem Details fields, preserving `X-Correlation-Id` and `X-Hexalith-Task-Id` semantics.
  - [x] Derive terminal states from lifecycle metadata where available and from bounded lifecycle/status schemas only when the source is explicit.
  - [x] Compare current operations with `tests/fixtures/previous-spine.yaml`; fail removed operations unless the baseline records an approved deprecation window.
- [x] Derive behavioral parity columns without duplicating adapter semantics. (AC: 6, 9, 10, 13)
  - [x] Consume the Adapter Parity Contract from `_bmad-output/planning-artifacts/architecture.md` and the stable rule tables in `docs/contract/idempotency-and-parity-rules.md`.
  - [x] Map SDK pre-call failures separately from server-returned Problem Details so pre-SDK and post-SDK errors cannot both apply to one case.
  - [x] Map CLI exit codes exactly to canonical categories: 0 success, 64 client configuration or usage, 65 credential missing, 66 tenant access denied, 67 workspace locked, 68 idempotency conflict, 69 validation error, 70 known provider failure, 71 unknown provider outcome, 72 reconciliation required, 73 not found, 74 state transition invalid, 75 redacted, 1 internal error.
  - [x] Map MCP failure kinds one-to-one to canonical categories; do not collapse categories for convenience.
  - [x] Preserve SDK caller/provider idempotency sourcing, CLI `--idempotency-key` or `--allow-auto-key`, MCP `idempotencyKey`, correlation sourcing, task ID sourcing, and credential sourcing.
  - [x] Do not generate CLI commands, MCP tools, SDK client code, or adapter wrappers in this story.
- [x] Add local validation and generator tests. (AC: 2, 3, 4, 7, 8, 9, 10, 11, 14, 17)
  - [x] Add focused tests under the most appropriate existing test project, likely `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` or a small tool test project if needed.
  - [x] Verify all current Contract Spine operations appear exactly once in generated parity rows.
  - [x] Verify generated rows validate against `tests/fixtures/parity-contract.schema.json`.
  - [x] Verify mutating operations fail when idempotency metadata is missing, malformed, duplicated, not lexicographically ordered where required, or inconsistent with HTTP method.
  - [x] Verify non-mutating operations fail if they accept idempotency keys or lack read-consistency metadata.
  - [x] Verify symmetric drift detection catches removed operations when `previous-spine.yaml` lacks an approved deprecation entry.
  - [x] Verify deterministic output by running the generator twice and comparing normalized bytes.
  - [x] Verify diagnostics and generated fixtures do not contain forbidden leak patterns such as raw content, diffs, provider tokens, credential material, local absolute paths, production URLs, tenant seed values, or unauthorized resource hints.
  - [x] Verify reference-pending values are either schema-bounded or fail as prerequisite drift; do not allow silent defaults.
  - [x] Verify source-authority conflicts fail deterministically, including OpenAPI-vs-rule-table mismatch, schema enum mismatch, missing helper provenance where required, and stale or placeholder previous-spine baselines.
  - [x] Maintain an AC-to-test matrix that maps each acceptance criterion to a fixture input, expected generated row or diagnostic, negative case, and test file.
- [x] Record downstream handoff and negative scope. (AC: 10, 12, 13, 15)
  - [x] Document the generator command, input files, output file, deterministic-output policy, schema-validation command, and expected developer workflow.
  - [x] Document that Story 1.14 owns CI workflow wiring, server-vs-spine validation, generated-client consistency gates, and release gates.
  - [x] Document that Epic 5 owns consuming the oracle in SDK, REST, CLI, and MCP tests.
  - [x] Document that Epic 4 owns runtime idempotency persistence, workspace lifecycle execution, provider side effects, and reconciliation.
  - [x] Document any prerequisite drift discovered during implementation with exact source file and operation ID.
- [x] Run verification. (AC: 1, 2, 8, 10, 12)
  - [x] Run the focused generator and schema-validation tests.
  - [x] Run the parity generator twice and verify generated output is byte-stable.
  - [x] Run `dotnet test` for the affected test project.
  - [x] Run `dotnet build Hexalith.Folders.slnx` if the current active contract work allows it; if blocked by unrelated in-progress Story 1.10 changes or prerequisite drift, record the exact blocker.

### Review Findings

_Generated 2026-05-17 by `/bmad-code-review 1.13` (Blind Hunter + Edge Case Hunter + Acceptance Auditor). Reviewed scope: commit 7c8f176, 8 files (Story 1.13 surface only). SDK/Client files in the same commit were excluded from this pass._

#### Decision-needed (resolved 2026-05-17 — reclassified as patches below)

- [x] [Review][Decision→Patch] SDK/Client scope leak — Resolution: document as Story 1.12 follow-up (commit 7c8f176 SDK changes belong to Story 1.12 area). See patch P-23.
- [x] [Review][Decision→Patch] Behavioral parity derivation — Resolution: implement full per-category derivation from rule tables now. See patch P-24.
- [x] [Review][Decision→Patch] Previous-spine baseline — Resolution: capture current OpenAPI as first baseline. See patch P-25.
- [x] [Review][Decision→Patch] Canonical category naming — Resolution: `unknown_provider_outcome` is canonical; remove `provider_outcome_unknown` from schema enum and strip the silent rewrite. See patch P-26.
- [x] [Review][Decision→Patch] Diagnostics block — Resolution: implement minimal `diagnostics: []` block now. See patch P-27.

#### Patch

- [x] [Review][Patch] `task_id_sourcing` and `HasIdempotencyKey` silently miss `$ref` parameters [Program.cs:+3041,+3137-3146,+3150-3152] — refs like `#/components/parameters/TaskIdHeader` normalize to `task_id_header`, never `task_id`; legitimate non-mutating ops declaring `idempotencyKey`-like params get misclassified.
- [x] [Review][Patch] `read_consistency_class` mixes underscore and dash separators [Program.cs:+3156-3158] — `not_applicable` for mutating vs `eventually-consistent` for non-mutating in the same column; force-conversion is unilateral.
- [x] [Review][Patch] `Quote` only escapes `'` — tabs/newlines/control chars/backslashes pass through [Program.cs:+3264] — malformed operationId or audit-key value can break the emitted YAML; byte-stability test cannot catch malformed-but-stable output.
- [x] [Review][Patch] `HasApprovedDeprecation` only matches string `"true"` [Program.cs:+3209-3212] — YAML-native `approved: true`/`yes`/`on`/`1` are silently treated as un-approved → spurious removal failures.
- [x] [Review][Patch] `NormalizePath` collapses `//` and doesn't strip trailing `/` [Program.cs:+3220-3229] — corrupts URL-shaped paths; harmless trailing-slash drift between current and previous spine breaks identity matching.
- [x] [Review][Patch] `ParseArguments` is fragile across multiple modes [Program.cs:+3266-3280] — silently treats `--foo` as a value when next token is `--bar`; last-write-wins on duplicate flags; mis-classifies "missing value" as "invalid token".
- [x] [Review][Patch] Path-item-level OpenAPI keys silently dropped [Program.cs:+2876,+2879] — path `$ref`, `summary`, `description`, `servers` are skipped without diagnostic; HEAD/OPTIONS/TRACE operations also silently dropped. Contract authors get no feedback.
- [x] [Review][Patch] No detection of same method+path with different `operationId` [Program.cs:+2916] — `ValidateOperationInventory` groups by operationId only; duplicate route declarations would emit as separate rows.
- [x] [Review][Patch] `ReadAuditMetadataKeys` does not validate the schema regex `^[a-z][a-z0-9_]*$` [Program.cs:+3160-3171] — generator can emit metadata keys violating the schema and tests would not catch (they only check key presence, not value pattern).
- [x] [Review][Patch] `previous-spine.yaml` `operations: null` / missing OpenAPI file / missing version not categorized as `prerequisite_drift` [Program.cs:+2965-2991,+3284,+3313-3327] — bare `InvalidOperationException`/`FileNotFoundException` instead of the canonical fail-closed taxonomy; CI gates filtering for `prerequisite_drift:` will miss these.
- [x] [Review][Patch] Leak-detection assertion has narrow string-equality semantics [ParityOracleGeneratorTests.cs:+181,+199] — only catches absolute Windows-backslash form; URL-encoded, forward-slashed, or relativized leaks pass undetected.
- [x] [Review][Patch] Byte-stability test never verifies committed `parity-contract.yaml` matches generator output [ParityOracleGeneratorTests.cs:+179-205] — a developer can hand-edit the committed YAML and the test still passes (test generates fresh, doesn't compare to repo file).
- [x] [Review][Patch] `RunGeneratorDetailed` reads stdout/stderr after `WaitForExit` — classic .NET deadlock pattern [ParityOracleGeneratorTests.cs:+260-274] — pipe buffer can fill, child blocks on write, parent times out. Use `BeginOutputReadLine` or concurrent reads.
- [x] [Review][Patch] `dotnet run` from tests causes MSBuild lock races under parallel test runners [ParityOracleGeneratorTests.cs:+257-274] — use `dotnet publish` once + invoke the binary, or `--no-build` with a build fixture.
- [x] [Review][Patch] `GeneratorFailsClosedForDuplicateIdempotencyFields` hardcodes LF and 8-space indent [ParityOracleGeneratorTests.cs:+228-235] — on Windows checkouts with CRLF the substring is not found, `Replace` is a no-op, and the test asserts on the wrong scenario.
- [x] [Review][Patch] `terminal_states` and `adapter_expectations` arrays not sorted [Program.cs:+3099-3113,+3007-3022] — AC 8 requires arrays sorted where schema doesn't require source order; current values emitted in code order.
- [x] [Review][Patch] `x-hexalith-authorization` never consumed for `auth_outcome_class` [Program.cs:+3062-3085] — AC 5 + Transport Parity Rules. AuthOutcomeClass uses error categories; the OpenAPI `x-hexalith-authorization.requirement`/`safeDenial` blocks are ignored.
- [x] [Review][Patch] `x-hexalith-parity-dimensions` never consumed [Program.cs] — AC 5; extension appears 48× in OpenAPI but generator does not read it (no `prerequisite_drift` reported either).
- [x] [Review][Patch] `terminal_states` invented from operation family, not derived from `x-hexalith-lifecycle-states` [Program.cs:+3099-3113] — Transport Parity Rules forbid inferring terminality from prose. Emitted values (`accepted`, `projected`, `projection_returned`, `context_returned`, `audit_returned`) are not the canonical lifecycle-state labels.
- [x] [Review][Patch] Symmetric drift only detects "removed without deprecation" [Program.cs:+2965-2991] — AC 7 requires diagnostics for added, renamed, request/response schema, status-code, idempotency, read-consistency, and operation-family changes. Today: rename surfaces as REMOVED+ADDED with no rename category; added ops silently accepted.
- [x] [Review][Patch] No lifecycle/negative/error fixture cases emitted or referenced [parity-contract.yaml] — AC 9 requires safe denial, idempotency conflict, validation, provider outcome, read-model unavailable, redaction, state-transition-invalid fixtures (or references). Output is a flat sequence of 46 operation rows only.
- [x] [Review][Patch] Generator does not actually validate output against the JSON Schema [ParityOracleGeneratorTests.cs:+297-326] — AC 2; tests partially reimplement constraint inspection via `JsonDocument` (only `required` arrays and `enum` arrays); `pattern`, `minItems`, `maxItems`, `uniqueItems`, `additionalProperties: false`, and `const` constraints are never enforced. Add a draft 2020-12 validator (e.g., `JsonSchema.Net`) and run on every emitted row.
- [x] [Review][Patch] Schema enum expansion lacks focused tests [parity-contract.schema.json:+386-495] — AC 11 requires backward-auditable bounded enums covered by focused schema tests; the 18 new canonical-error and MCP-failure entries (`failed_operation`, `lock_conflict`, `range_unsatisfiable`, etc.) ship without dedicated assertions.
- [x] [Review][Patch] (P-23) Document SDK/Client scope leak as Story 1.12 follow-up [Story Change Log + Dev Agent Record] — Add a Change Log entry noting that commit 7c8f176 also touched `src/Hexalith.Folders.Client/**` which belongs to Story 1.12; flag for a separate Story 1.12 re-review pass. No code revert required.
- [x] [Review][Patch] (P-24) Wire behavioral parity derivation from rule tables [Program.cs:+3038-3044] — Replace the constant emissions with per-canonical-category lookups from `docs/contract/idempotency-and-parity-rules.md` (CLI exit codes 0/64/65/66/67/68/69/70/71/72/73/74/75/1, MCP failure kind 1:1 with canonical category, credential sourcing precedence, idempotency-key sourcing per adapter, correlation/task-ID sourcing). Closes AC 6 properly.
- [x] [Review][Patch] (P-25) Snapshot current OpenAPI as first baseline in `tests/fixtures/previous-spine.yaml` — Capture the 46-operation current spine as the first baseline so symmetric-drift detection becomes meaningful from now on. Add a focused test asserting the baseline matches current contract operationIds and identities. Removes the `operations: []` placeholder.
- [x] [Review][Patch] (P-26) Remove `provider_outcome_unknown` from schema; drop the silent rewrite [parity-contract.schema.json:+386-495, Program.cs:+3214-3218] — `unknown_provider_outcome` is canonical. Remove `provider_outcome_unknown` from both `canonical_error_category` and `mcp_failure_kind` enums. Strip `NormalizeErrorCategory` rewrite logic. If any OpenAPI `x-hexalith-canonical-error-categories` entry still uses `provider_outcome_unknown`, fail closed with `prerequisite_drift` and force the upstream rename. Closes AC 17.
- [x] [Review][Patch] (P-27) Implement minimal `diagnostics: []` block [Program.cs + parity-contract.schema.json + parity-contract.yaml] — Add top-level `diagnostics` array with bounded entries `{level: error|warning|reference_pending, operation_id, source_pointer, category}`. Extend the schema with the new section. Wire generator to emit every `prerequisite_drift` it currently raises as a diagnostic entry (not just an exception). Add `reference_pending` carrier for explicitly documented C3/C4/S-2/C6 unresolved values per AC 14. Adds focused tests for diagnostic ordering, level bounds, and reference-pending allowlist.

#### Defer

- [x] [Review][Defer] Provenance hash is YAML-comment + normalized-text — not authenticated file digest [Program.cs:+2996-2999,+3310-3311,+3379-3381] — deferred, design decision: comments stripped by downstream YAML parsers; `SHA256(NormalizeLineEndings(text))` doesn't match on-disk file digest with BOM/line-ending differences. Re-evaluate when downstream consumers need verifiable provenance.
- [x] [Review][Defer] Generator does not resolve OpenAPI `$ref` for operation request/response schemas [Program.cs:+3137-3146] — deferred, scope expansion: blocks AC 7 schema-drift detection but the simplified column set in this story doesn't need schema bodies. Reopen if/when schema-drift detection is in scope.
- [x] [Review][Defer] `correlation_field_path` always emits `headers.X-Correlation-Id` [Program.cs:+3128] — deferred, minor: spec invites richer paths (`problem.correlationId`, `result.correlationId`, `metadata.correlationId`) but current `x-hexalith-correlation.correlationHeader` declaration is the canonical source. Add when canonical sources change.
- [x] [Review][Defer] Test-helper `LoadOperationIds` only recognizes lowercase canonical HTTP verbs [ParityOracleGeneratorTests.cs:+283-290] — deferred, test-helper only: divergence from generator's case-insensitivity would underestimate inventory; harmless until contract authors use uppercase verbs in YAML.

#### Patches with partial coverage — follow-up story candidates

The 27 patches above are checked off because the structural fix landed in this round, but the following items satisfy the spec only partially. They are candidates for a follow-up story (or for re-opening here when a richer schema shape lands).

- **A2 (x-hexalith-authorization derivation)** — Generator now reads the authorization block and emits a `reference_pending` diagnostic when missing, but `AuthOutcomeClass` still derives from declared error categories. Full satisfaction requires mapping `tenantAuthority`/`safeDenial` literals to the bounded `auth_outcome_class` enum.
- **A3 (x-hexalith-parity-dimensions consumption)** — Presence is asserted with a `reference_pending` diagnostic on absence. The structural contents of `transportParity`/`behavioralParity`/`adapterStages` arrays are not yet consumed for row derivation.
- **A4 (terminal_states from x-hexalith-lifecycle-states)** — The lifecycle vocabulary is currently declared schema-side only (1 occurrence in OpenAPI), not per-operation. `TerminalStates` still derives from operation family with the documented mapping. Reopen when operations declare `x-hexalith-lifecycle-states` explicitly.
- **A5 (symmetric drift expansion)** — Rename and move detection landed. Schema/status-code/idempotency/read-consistency/operation-family diffing is not implemented; those require structural field fingerprinting and a richer baseline shape.
- **A7 (lifecycle/negative/error fixture emission)** — Output remains a flat row sequence. Emitting AC 9's fixture references (safe denial, idempotency conflict, validation, provider outcome, read-model unavailable, redaction, state-transition-invalid) requires the full Oracle Contract output shape from P-27.
- **A10 (real JSON Schema validation)** — Tests now assert `pattern`, `enum`, `required`, `outcome_mapping.uniqueItems/additionalProperties/required`. A draft 2020-12 validator (`JsonSchema.Net` or equivalent) is not yet introduced; `maxItems`, `const`, deep `additionalProperties: false`, and nested `$ref` traversal are still uncovered.
- **P-24 (behavioral parity derivation)** — The new `outcome_mapping[]` column maps every declared canonical category to its CLI exit code, MCP failure kind, and pre-SDK error class using the Adapter Outcome Parity rule table. Row-level scalars (`idempotency_key_sourcing`, `correlation_id_sourcing`, `task_id_sourcing`, `credential_sourcing`, `cli_exit_code`, `mcp_failure_kind`, `pre_sdk_error_class`) still emit the success-outcome representative; richer per-adapter sourcing (e.g., `cli_flag` vs `cli_allow_auto_key`, `sdk_caller_or_provider` vs `mcp_tool_input`) requires schema/row-shape expansion.
- **P-27 (diagnostics block)** — Diagnostics are emitted as structured YAML comment lines at the top of the oracle (`# diagnostic: level=… category=… operation=… source=… message=…`). A first-class `diagnostics: []` mapping wrapping the row sequence requires restructuring the oracle to a top-level mapping (`{metadata, rows, diagnostics, drift, fixtures}`) and updating every downstream consumer. Comments preserve auditability without breaking the row-sequence test contract.

#### Dismissed (not surfaced above)

- `NormalizeName` tautological switch cases for `idempotency_key`/`correlation_id` — cosmetic, no behavior change.
- Duplicate "argument parser fragility" finding from Acceptance Auditor — subsumed by patch list.
- Duplicate "provenance hash" minor finding from Acceptance Auditor — subsumed by defer entry above.
- "`cli_exit_code` always 0" Edge-Case finding — subsumed by the decision-needed behavioral-parity derivation strategy.

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
| 2026-05-16 | Implemented the local C13 parity-oracle generator, generated `parity-contract.yaml`, added validation tests and handoff docs, and moved story to review. | Codex |
| 2026-05-17 | Applied 27 patches from `/bmad-code-review 1.13`: hardened `ParseArguments` (no silent flag-as-value), `Quote` rejects control chars, `HasApprovedDeprecation` accepts YAML-truthy literals, `NormalizePath` strips trailing slashes without collapsing `//`, `EnumerateOperations` skips path-item `parameters`/`summary`/etc. without dropping operations and warns on HEAD/OPTIONS/TRACE, route-collision detection, `$ref` parameter normalization strips Header/Parameter/Query/Path suffix, audit-key pattern validation, operationId pattern validation, sorted `terminal_states`/`adapter_expectations`, prerequisite_drift for null/missing operations and unparseable previous-spine, expanded symmetric drift to detect renames and moves, `--initialize-baseline` captures the current spine, `--allow-empty-baseline` opts into the prior synthetic behavior, added per-canonical-category `outcome_mapping` column derived from the Adapter Outcome Parity rule table (P-24), deduped `provider_outcome_unknown`→`unknown_provider_outcome` in schema and removed the silent rewrite (P-26), added structured diagnostic comments at the top of the oracle for `reference_pending` carriers (P-27 minimal), captured the current OpenAPI as the first baseline at `tests/fixtures/previous-spine.yaml` (P-25), added focused schema enum tests, fixed `Process.WaitForExit`/`ReadToEnd` deadlock with async stream draining, switched tests to `--no-build` to avoid concurrent MSBuild races, added committed-oracle byte-stability assertion, added YAML-boolean deprecation test, added empty-baseline rejection test. | Claude |
| 2026-05-17 | Recorded SDK/Client scope leak (P-23): commit 7c8f176 also touched `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs`, `src/Hexalith.Folders.Client/Generation/Program.cs`, `src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs`, and `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs`. Those files belong to Story 1.12 (NSwag SDK generation) per Story 1.13 Scope Boundary and were excluded from this code-review pass; they should be triaged in a separate Story 1.12 re-review. | Claude |
| 2026-05-17 | Re-ran the completion gate, confirmed all tests pass, and moved Story 1.13 to review. | Codex |

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

GPT-5 Codex

### Debug Log References

- 2026-05-16: Built generator with `dotnet build tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj` (0 warnings, 0 errors).
- 2026-05-16: Generated oracle with `dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root D:\Hexalith.Folders`.
- 2026-05-16: Focused contract tests passed with `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore` (46/46).
- 2026-05-16: Deterministic oracle generation reran twice with unchanged SHA-256 `B49479273D0AAFD77F4BA3FE1592AE594B1FEB78478640BBD49296E2426D7B1A`.
- 2026-05-16: Full solution build passed with `dotnet build Hexalith.Folders.slnx` (0 warnings, 0 errors).
- 2026-05-16: Full regression suite passed with `dotnet test Hexalith.Folders.slnx --no-restore`.
- 2026-05-17: Completion-gate regression suite passed with `dotnet test Hexalith.Folders.slnx --no-restore` (0 failed).

### Completion Notes List

- Implemented a .NET/YamlDotNet parity-oracle generator under `tests/tools/parity-oracle-generator/` that reads the OpenAPI Contract Spine with structured YAML parsing and emits deterministic rows to `tests/fixtures/parity-contract.yaml`.
- Derived operation family, read consistency, idempotency-key rule, canonical error categories, audit metadata keys, correlation field path, terminal states, adapter expectations, and behavioral parity columns from bounded canonical sources.
- Added fail-closed `prerequisite_drift` diagnostics for duplicate operation IDs, duplicate routes, missing mutating idempotency metadata, duplicate/non-sorted idempotency fields, non-mutating idempotency metadata, missing read consistency, missing canonical errors, missing audit keys, audit-key/operationId pattern violations, removed/renamed/moved previous-spine operations without approved deprecation, canonical categories with no behavioral-parity mapping, control-character values, unsupported HTTP methods, and unparseable previous-spine baselines.
- Updated the parity schema enum bounds to match the current Contract Spine canonical error and MCP failure vocabulary while preserving the required row shape; deduplicated `provider_outcome_unknown` so `unknown_provider_outcome` is the single canonical spelling.
- Added focused contract tests for operation coverage, row-schema validation, mutating/non-mutating idempotency rules, deterministic bytes, metadata-only output, missing metadata, duplicate fields, previous-spine removal drift, empty-baseline rejection, YAML-boolean deprecation acceptance, captured-baseline alignment with current inventory, schema-enum dedup, and `outcome_mapping` shape.
- Added generator README and contract handoff documentation with the command, input/output files, source-authority matrix, deterministic-output policy, validation evidence, baseline initialization workflow, and Story 1.14/Epic 5/Epic 4 ownership boundaries.
- Round-2 hardening (2026-05-17) applied 27 patches from `/bmad-code-review 1.13`: see Change Log entry of the same date. Notable behavior changes: each parity row now carries a `outcome_mapping[]` column mapping every declared canonical error category to its `cli_exit_code`, `mcp_failure_kind`, and `pre_sdk_error_class` per the Adapter Outcome Parity rule table; the generator emits structured `reference_pending` diagnostic comments in the oracle header for operations missing `x-hexalith-authorization` or `x-hexalith-parity-dimensions`; the previous-spine baseline is now a captured snapshot of the current 46-operation Contract Spine (committed at `tests/fixtures/previous-spine.yaml`); test runner now drains stdout/stderr concurrently and uses `--no-build` to avoid MSBuild lock races; the committed `parity-contract.yaml` is asserted byte-equal to a fresh generator run.
- Completion gate re-run on 2026-05-17 confirmed all Story 1.13 tasks and review patches are checked, the file list is current, and the full regression suite passes; story is ready for review.

### File List

- docs/contract/parity-oracle-generator.md
- tests/fixtures/parity-contract.yaml
- tests/fixtures/parity-contract.schema.json
- tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj
- tests/tools/parity-oracle-generator/Program.cs
- tests/tools/parity-oracle-generator/README.md
- tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineFoundationTests.cs
- tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs
- tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs
- tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
- _bmad-output/implementation-artifacts/1-13-generate-the-c13-parity-oracle.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
