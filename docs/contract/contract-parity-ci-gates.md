# Contract Parity CI Gates

Story 7.5 defines the pull-request lane for public contract and cross-surface parity drift. The stable required status-check name is `contract-and-parity-gates`; branch protection is configured outside this repository, but this job name is intentionally stable so maintainers can require it.

The workflow is `.github/workflows/ci.yml`. It runs for `pull_request` and pushes to `main`, `next`, `alpha`, and `beta`. Checkout uses `submodules: false`; the workflow then initializes only the documented root-level build submodules:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

Nested recursive initialization is forbidden unless a maintainer explicitly requests nested submodule work.

## Local Command

Run the workflow-equivalent lane from the repository root:

```powershell
dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false
dotnet build Hexalith.Folders.slnx --no-restore -m:1
.\tests\tools\run-contract-parity-ci-gates.ps1
```

The script runs exact project and filter allow-lists. It does not publish packages, upload artifacts, install browsers, run live providers, call production endpoints, start service containers, or require secrets.

## Gate Categories

`tests/tools/run-contract-parity-ci-gates.ps1` exposes these failure categories:

- `server-vs-spine`: server OpenAPI source discovery and server-vs-spine comparison behavior.
- `previous-spine`: previous-spine baseline coverage and approved removal/deprecation checks.
- `generated-client`: NSwag input, generated client provenance, stale generated output detection, and isolated regeneration.
- `idempotency-helpers`: generated helper coverage, canonical hashing, metadata rejection, and idempotency parser policy.
- `parity-oracle-schema`: parity oracle schema, enum, required column, and outcome mapping validation.
- `parity-oracle-determinism`: operation-count drift, deterministic output, and fail-closed generator cases.
- `sdk-transport-parity`: SDK/REST transport parity from the oracle.
- `rest-sdk-golden-parity`: REST/SDK golden lifecycle parity.
- `cli-behavioral-parity`: CLI exit-code, pre-SDK sourcing, and behavioral parity.
- `mcp-behavioral-parity`: MCP failure-kind, tool mapping, pre-SDK, post-SDK, and sourcing parity.
- `mixed-surface-handoff`: cross-adapter behavior and mixed REST, SDK, CLI, and MCP handoff workflows.

### Wire-exercised parity (Stories 8.1–8.3)

The `rest-sdk-golden-parity` and `mixed-surface-handoff` lanes are **wire-exercised end-to-end** against an
in-process host, not asserted at oracle-metadata level: all 47 operations have REST server routes (Stories
8.1/8.2), the golden-lifecycle steps are driven over the real REST transport (and SDK/CLI/MCP equivalents),
and cross-surface error parity is exercised on the wire — `idempotency_conflict` surfaces as HTTP 409 / CLI
exit 68 / MCP `idempotency_conflict`, and ACL denials surface as the safe denial `not_found_to_caller` (404)
uniformly across surfaces (the canonical `folder_acl_denied` → 403 gateway-hop mapping is covered at the
route/adapter layers). The public **four-surface canonical-lifecycle parity claim is gated on these stories**
passing; it is asserted to consumers only on green.

## Test Inventory

The gate uses these exact projects and filters:

| Category | Project | Filter scope |
|---|---|---|
| `server-vs-spine` | `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj` | `ContractSpineCiGateTests.ServerVsSpine*` |
| `previous-spine` | `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj` | previous-spine tests from `ContractSpineCiGateTests` and `ParityOracleGeneratorTests` |
| `generated-client` | `tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj` | `ClientGenerationTests` for NSwag input, provenance, stale output, isolated regeneration, and build-time inputs |
| `idempotency-helpers` | `tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj` | `ClientGenerationTests` helper and canonical hash cases |
| `parity-oracle-schema` | `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj` | `ParityOracleGeneratorTests` schema, enum, idempotency, and outcome mapping cases |
| `parity-oracle-determinism` | `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj` | `ParityOracleGeneratorTests` deterministic output and fail-closed cases |
| `sdk-transport-parity` | `tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj` | `TransportParityConformanceTests`, `ArchiveFolderClientConformanceTests`, and `LifecycleStatusClientConformanceTests` |
| `rest-sdk-golden-parity` | `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj` | `EndToEnd.GoldenLifecycleParityTests` |
| `cli-behavioral-parity` | `tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj` | `ParityOracleConformanceTests` and `BehavioralParityTests` |
| `mcp-behavioral-parity` | `tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj` | `ParityOracleConformanceTests`, `PreSdkFailureTests`, `PostSdkMappingTests`, `SourcingTests`, and `FailureKindProjectionTests` |
| `mixed-surface-handoff` | `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj` | `AdapterParity.CrossAdapterBehavioralParityTests` and `MixedSurfaceHandoff.MixedSurfaceHandoffTests` |

## Report

The gate writes a metadata-only report to `_bmad-output/gates/contract-parity-ci/latest.json`. The report may contain the gate name, category names, repository-relative artifact paths, project paths, filter names, statuses, and exit codes. It must not contain absolute local paths, secrets, tokens, tenant data, provider payloads, raw file contents, diffs, generated client bodies, OpenAPI bodies, TestResults bodies, or environment dumps.

## Refresh Commands

Validation commands do not mutate tracked generated output. Refresh commands are separate and intentional:

```powershell
dotnet msbuild src\Hexalith.Folders.Client\Hexalith.Folders.Client.csproj /t:GenerateHexalithFoldersClient
dotnet msbuild src\Hexalith.Folders.Client\Hexalith.Folders.Client.csproj /t:GenerateHexalithFoldersIdempotencyHelpers
dotnet run --project tests\tools\parity-oracle-generator\Hexalith.Folders.ParityOracleGenerator.csproj -- --output tests\fixtures\parity-contract.yaml
```

`tests/fixtures/previous-spine.yaml` is refreshed only after an approved public baseline sweep:

```powershell
dotnet run --project tests\tools\parity-oracle-generator\Hexalith.Folders.ParityOracleGenerator.csproj -- --initialize-baseline
```

Do not hand-edit generated client files or parity rows. Change the OpenAPI Contract Spine or generator inputs and regenerate.

## Relationship To Existing Gates

`.github/workflows/contract-spine.yml` remains active during the Story 7.5 handoff. It still owns focused lanes that are outside this story, including:

- `run-safety-invariant-gates.ps1`
- `run-governance-completeness-gates.ps1`
- `run-dapr-policy-conformance-gates.ps1`
- `run-container-image-gates.ps1`

This transitional duplication is deliberate. Story 7.5 consolidates contract and parity checks into `ci.yml`; Stories 7.6 and 7.8 own moving or narrowing the safety, governance, Dapr policy, and scheduled drift coverage without weakening merge protection.
