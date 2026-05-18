# Governance And Completeness CI Gates

The governance/completeness gate is the local and CI entry point for Story 1.16 checks. It validates exit-criteria evidence, idempotency corpus consumption, opt-in pattern examples, tenant-prefixed cache-key exceptions, and parity completeness without Aspire, Dapr sidecars, provider credentials, network calls, or nested submodule initialization.

## Local Command

```powershell
.\tests\tools\run-governance-completeness-gates.ps1
.\tests\tools\run-governance-completeness-gates.ps1 -SkipRestoreBuild
```

The command must be run from the repository root or from the script location. It writes a sanitized discovery report to `_bmad-output/gates/governance-completeness/latest.json`. The report includes gate names, repository-relative canonical inputs, report path, and diagnostic policy only.

## CI Job

The `contract-spine-gates` workflow invokes the same command after restore and build:

```powershell
.\tests\tools\run-governance-completeness-gates.ps1 -SkipRestoreBuild
```

Workflow YAML may orchestrate setup, but gate decisions live in checked-in tests and fixtures rather than workflow-only shell logic.

## Owned Inputs

- `docs/exit-criteria/c0-c13-governance-evidence.yaml`
- `tests/fixtures/idempotency-encoding-corpus.json`
- `tests/fixtures/idempotency-encoding-corpus.schema.json`
- `tests/fixtures/idempotency-encoding-corpus-consumption.yaml`
- `tests/fixtures/pattern-example-manifest.yaml`
- `tests/fixtures/cache-key-exceptions.yaml`
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/parity-contract.schema.json`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`

## Diagnostic Categories

- `prerequisite_drift`: a required input, owner, path, command, report, or source authority is missing or inconsistent.
- `exit_criteria_missing`: a C0-C13 row is absent.
- `exit_criteria_duplicate`: a criterion appears more than once.
- `exit_criteria_malformed`: required evidence metadata is missing or contains an invalid placeholder.
- `artifact_path_invalid`: an evidence path is absolute, escapes the repository, or points to an unreadable artifact.
- `idempotency_sample_unmapped`: a corpus sample lacks exactly one stable consumption map entry.
- `pattern_example_invalid`: a C# example is unmarked, stale, or not part of the compilable examples project.
- `cache_key_unscoped`: a tenant-data cache key candidate lacks tenant scope and no reviewed exception applies.
- `parity_completeness_mismatch`: OpenAPI operations and generated parity rows differ, duplicate, or omit required metadata.

Diagnostics may include gate names, rule IDs, criterion IDs, sample IDs, operation IDs, schema pointers, repository-relative paths, bounded categories, counts, and safe hashes. Diagnostics must not include raw payloads, file contents, diffs, provider tokens, credentials, tenant data, local absolute paths, production URLs, cache key values, provider responses, or unauthorized-resource hints.

## Contribution Checklist

When adding evidence, corpus cases, pattern examples, cache-key exceptions, or parity row shapes:

1. Add the repository-relative artifact and ownership metadata first.
2. Add or update the stable fixture row that names the owner, status, command, and evidence link.
3. Add positive and negative coverage in `GovernanceCompletenessGateTests`.
4. Keep reference-pending decisions bounded with owner, criterion ID, reason, and consuming story.
5. Regenerate parity rows only through `tests/tools/parity-oracle-generator`; never hand-edit generated rows.
6. Keep all examples synthetic and metadata-only.

## Story Ownership

Story 1.12 owns generated `ComputeIdempotencyHash()` helper behavior. Story 1.13 owns parity-oracle generation. Story 1.14 owns Contract Spine drift and generated-client consistency gates. Story 1.15 owns sentinel redaction and output-channel leakage checks. Story 1.16 only wires governance/completeness checks and cache-key diagnostic metadata needed by these gates.
