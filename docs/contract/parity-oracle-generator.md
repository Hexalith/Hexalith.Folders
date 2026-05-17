# Parity Oracle Generator

Status: Story 1.13 implementation note.

## Local Command

```text
dotnet run --project tests/tools/parity-oracle-generator/Hexalith.Folders.ParityOracleGenerator.csproj -- --repository-root <repo-root>
```

The default output is:

```text
tests/fixtures/parity-contract.yaml
```

## Inputs

- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `tests/fixtures/parity-contract.schema.json`
- `tests/fixtures/previous-spine.yaml`
- `docs/contract/idempotency-and-parity-rules.md`
- `_bmad-output/planning-artifacts/architecture.md`
- Story 1.12 generated SDK helper provenance, for safe operation/helper identity only

## Source Authority Matrix

| Source | Owns |
|---|---|
| OpenAPI operation metadata | Operation IDs, HTTP method/path identity, idempotency metadata, canonical errors, read consistency, audit keys, correlation headers, authorization metadata |
| Idempotency and parity rules | Adapter semantics, sourcing rules, CLI and MCP behavioral parity expectations |
| Architecture Adapter Parity Contract | Behavioral parity dimensions and bounded adapter expectations |
| Parity row schema | Allowed row shape and enum bounds |
| Previous spine baseline | Symmetric removal/deprecation comparison |
| Story 1.12 helper provenance | Helper identity only; not hash construction policy |

Conflicts fail as `prerequisite-drift`; the generator does not invent fallback policy.

## Deterministic Output Policy

The generated YAML is a sequence of schema-valid rows sorted by `operation_id`. Output is UTF-8 without BOM, LF-only, timestamp-free, and metadata-only. Safe provenance is emitted as comments containing content hashes and source names, not local absolute paths.

## Validation

Focused tests in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs` verify operation coverage, row schema requirements and enums, mutating/non-mutating idempotency rules, deterministic bytes, metadata-only output, and fail-closed missing idempotency metadata.

## AC-To-Test Matrix

| Acceptance Criteria | Positive Evidence | Negative Evidence |
|---|---|---|
| AC1, AC3, AC4, AC5 | `GeneratedParityOracleContainsEveryCurrentOperationExactlyOnce`; `GeneratedParityRowsClassifyMutatingAndNonMutatingIdempotencyRules` | Missing mutating idempotency metadata fails as `prerequisite-drift` |
| AC2, AC11 | `GeneratedParityRowsValidateAgainstSeedSchemaEnumsAndRequiredColumns` | Schema enum mismatches fail row validation assertions |
| AC6, AC13 | Behavioral columns asserted in generated rows and sourced from bounded adapter rules | Pre-SDK and post-SDK categories remain separate row columns |
| AC7, AC17 | Previous-spine comparison runs during generation | Removed previous-spine operation without approved deprecation fails closed |
| AC8, AC10, AC14, AC16 | `GeneratorOutputIsByteStableAndMetadataOnly` | Forbidden leak strings and local absolute paths are rejected |
| AC12 | No CI workflow files are modified by this story | Story 1.14 ownership documented here and in the tool README |
| AC15 | Current OpenAPI, contract docs, previous-spine fixture, schema, and Story 1.12 provenance were inspected before implementation | Source conflicts and malformed metadata fail as `prerequisite-drift` |

## Downstream Ownership

Story 1.14 owns CI workflow wiring, generated-client consistency gates, server-vs-spine validation, and release gates. Epic 5 owns consuming the oracle in SDK, REST, CLI, and MCP tests. Epic 4 owns runtime idempotency persistence, workspace lifecycle execution, provider side effects, and reconciliation.
