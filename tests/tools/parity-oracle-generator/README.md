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
--initialize-baseline       Snapshot the current OpenAPI as the previous-spine baseline and exit.
--allow-empty-baseline      Accept an empty `operations: []` baseline (downgrades drift to warning).
--help                      Show usage and exit.
```

### Initialize the baseline

Run once whenever the Contract Spine is intentionally rewritten:

```text
dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root <repo-root> --initialize-baseline
```

The command overwrites `tests/fixtures/previous-spine.yaml` with the captured operation inventory and exits without emitting the parity oracle. Commit the regenerated baseline alongside any intentional contract change so subsequent runs perform meaningful symmetric drift detection.

## Source Authority

- OpenAPI operation metadata owns transport facts, operation IDs, idempotency metadata, canonical error categories, read consistency, audit metadata keys, correlation headers, and authorization metadata.
- `docs/contract/idempotency-and-parity-rules.md` and the architecture Adapter Parity Contract own adapter semantics such as idempotency-key sourcing, correlation sourcing, task-ID sourcing, credential sourcing, CLI exit codes, and MCP failure kinds.
- `tests/fixtures/parity-contract.schema.json` owns the bounded row shape and allowed enum values.
- `tests/fixtures/previous-spine.yaml` owns symmetric removal/deprecation comparison. The baseline is a captured snapshot of the prior Contract Spine; empty `operations: []` is rejected unless `--allow-empty-baseline` is passed explicitly. Removed, renamed, or moved operations fail closed unless they carry an `approved: true` (or any YAML-truthy literal) deprecation entry.
- Story 1.12 generated helper provenance is consumed only as safe operation/helper identity context; this generator does not reimplement SDK hash construction.

## Deterministic Output

Rows are sorted by `operation_id`, arrays are sorted when source order is not normative, output is UTF-8 without BOM and LF-only, and timestamps are not emitted. Safe provenance appears only as comments containing content hashes and repository-relative source names.

## Failure Behavior

The generator fails closed with `prerequisite_drift` diagnostics for duplicate operation IDs, duplicate routes (same method+path with different operationIds), missing mutating idempotency metadata, non-mutating idempotency metadata, missing read consistency, missing canonical error categories, missing audit metadata keys, audit-key pattern violations (`^[a-z][a-z0-9_]*$`), operationId pattern violations (`^[A-Z][A-Za-z0-9]*$`), control-character values that cannot be safely YAML-encoded, unresolved local OpenAPI `$ref` or path-item `$ref` constructs, unsupported HTTP methods, unparseable previous-spine baselines, `operations: null`, removed/renamed/moved previous-spine operations without an approved deprecation, canonical-error categories with no behavioral-parity mapping, and malformed arguments. HEAD/OPTIONS/TRACE methods and missing `x-hexalith-authorization` or `x-hexalith-parity-dimensions` extensions surface as non-blocking warnings or `reference_pending` markers in the generated oracle's diagnostic header.

## Handoff

Story 1.14 owns CI workflow wiring and release gates. Epic 5 owns SDK, REST, CLI, and MCP consumption tests. Runtime idempotency persistence, workspace lifecycle execution, provider side effects, and reconciliation remain Epic 4 scope.

## Ownership Metadata

- owner_workstream: Phase 1 Contract Spine and C13 parity-oracle generator stories.
- future_test_use: Generate and validate REST, SDK, CLI, and MCP parity rows from the Contract Spine and parity fixtures.
- known_omissions: CI wiring and downstream adapter consumption remain in later stories.
- mutation_rules: Regenerate `tests/fixtures/parity-contract.yaml` from repository inputs; do not hand-edit generated operation rows.
- non_policy_placeholder: true
- synthetic_data_only: true
