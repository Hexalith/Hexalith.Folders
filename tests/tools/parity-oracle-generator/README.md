# Parity Oracle Generator

This tool generates `tests/fixtures/parity-contract.yaml` from the OpenAPI Contract Spine and validates prerequisite metadata before writing rows.

## Command

```text
dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root <repo-root>
```

Optional arguments:

```text
--contract <path-to-hexalith.folders.v1.yaml>
--schema <path-to-parity-contract.schema.json>
--previous-spine <path-to-previous-spine.yaml>
--output <path-to-parity-contract.yaml>
```

## Source Authority

- OpenAPI operation metadata owns transport facts, operation IDs, idempotency metadata, canonical error categories, read consistency, audit metadata keys, correlation headers, and authorization metadata.
- `docs/contract/idempotency-and-parity-rules.md` and the architecture Adapter Parity Contract own adapter semantics such as idempotency-key sourcing, correlation sourcing, task-ID sourcing, credential sourcing, CLI exit codes, and MCP failure kinds.
- `tests/fixtures/parity-contract.schema.json` owns the bounded row shape and allowed enum values.
- `tests/fixtures/previous-spine.yaml` owns symmetric removal/deprecation comparison. The current synthetic empty baseline is treated as first-baseline initialization; non-empty removed operations fail unless they carry approved deprecation metadata.
- Story 1.12 generated helper provenance is consumed only as safe operation/helper identity context; this generator does not reimplement SDK hash construction.

## Deterministic Output

Rows are sorted by `operation_id`, arrays are sorted when source order is not normative, output is UTF-8 without BOM and LF-only, and timestamps are not emitted. Safe provenance appears only as comments containing content hashes and repository-relative source names.

## Failure Behavior

The generator fails closed with `prerequisite_drift` diagnostics for duplicate operation IDs, missing mutating idempotency metadata, non-mutating idempotency metadata, missing read consistency, missing canonical error categories, missing audit metadata keys, unresolved previous-spine removal without approved deprecation, and malformed arguments.

## Handoff

Story 1.14 owns CI workflow wiring and release gates. Epic 5 owns SDK, REST, CLI, and MCP consumption tests. Runtime idempotency persistence, workspace lifecycle execution, provider side effects, and reconciliation remain Epic 4 scope.

## Ownership Metadata

- owner_workstream: Phase 1 Contract Spine and C13 parity-oracle generator stories.
- future_test_use: Generate and validate REST, SDK, CLI, and MCP parity rows from the Contract Spine and parity fixtures.
- known_omissions: CI wiring and downstream adapter consumption remain in later stories.
- mutation_rules: Regenerate `tests/fixtures/parity-contract.yaml` from repository inputs; do not hand-edit generated operation rows.
- non_policy_placeholder: true
- synthetic_data_only: true
