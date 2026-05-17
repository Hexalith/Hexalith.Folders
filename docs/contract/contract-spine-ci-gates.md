# Contract Spine CI Gates

Story 1.14 wires the blocking gates for the Contract Spine, generated SDK client, generated idempotency helpers, previous-spine baseline, and C13 parity oracle. The gates are offline and repository-local: they do not require Aspire, Dapr sidecars, provider credentials, live providers, tenant seed data, or nested submodule initialization.

## Local Command

Run the workflow-equivalent lane from the repository root:

```powershell
dotnet restore Hexalith.Folders.slnx
dotnet build Hexalith.Folders.slnx --no-restore
.\tests\tools\run-contract-spine-gates.ps1 -NoRestore
```

The script runs the focused OpenAPI gate tests and generated-client consistency tests. It is validation-mode only and must leave tracked generated output unchanged.

## CI Job

`.github/workflows/contract-spine.yml` defines the `contract-generated-artifact-gates` job. It uses `actions/checkout@v6` with `submodules: false`, `actions/setup-dotnet@v5` with `global-json-file: global.json`, then runs restore, build, and the local gate script from the repository root. NuGet caching is deferred until lock-file support is introduced.

The job does not publish packages, sign artifacts, run live provider drift checks, initialize nested submodules, upload raw diffs, upload generated client bodies, or emit release evidence.

## Gate Inputs

| Gate | Input | Current status | Validation behavior |
|---|---|---|---|
| Contract Spine parse and drift | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | Present | OpenAPI tests parse the spine, resolve references, and assert metadata required by downstream generation. |
| Server OpenAPI emission | `src/Hexalith.Folders.Server` repository-local OpenAPI output | Reference-pending | Source discovery reports `prerequisite-drift` until exactly one non-self offline server OpenAPI source exists. |
| Previous-spine baseline | `tests/fixtures/previous-spine.yaml` | Captured baseline | The baseline must cover every current operation identity; approved removals require metadata-only deprecation evidence. |
| Generated SDK client | `src/Hexalith.Folders.Client/Generated/` and `src/Hexalith.Folders.Client/nswag.json` | Present | Client tests verify NSwag configuration, generated provenance, and stale-output detection. |
| C13 parity oracle | `tests/fixtures/parity-contract.yaml` and `tests/fixtures/parity-contract.schema.json` | Present | Parity tests validate row schema, operation coverage, deterministic output, and generator fail-closed behavior. |

## Failure Categories

Gate diagnostics use bounded categories only:

- `contract-spine-drift`
- `server-spine-mismatch`
- `previous-spine-drift`
- `generated-client-drift`
- `parity-oracle-mismatch`
- `generation-nondeterminism`
- `prerequisite-drift`

Diagnostics may include gate names, repository-relative paths, operation IDs, schema pointers, normalized content hashes, and remediation hints. They must not include file contents, raw diffs, generated client bodies, provider payloads, tokens, credentials, production URLs, tenant data, unauthorized resource hints, generated context payloads, or local absolute paths.

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

Do not hand-edit generated client files or parity rows. When an operation is intentionally removed or renamed, record an approved deprecation entry in `previous-spine.yaml` with the prior operation ID, rationale, approval reference, effective date, and repository-relative approval source.

## Prerequisite Drift

`prerequisite-drift` means a gate found missing source authority rather than a behavioral mismatch. Use it to decide which owner needs to run next:

- Missing server OpenAPI emission belongs to the future server OpenAPI surface work; the current server scaffold has no offline emitted source.
- Missing or stale generated SDK output belongs to Story 1.12.
- Missing or stale parity oracle output belongs to Story 1.13.
- Missing previous-spine approval evidence belongs to the baseline approval owner.

Story 1.15 owns safety invariant gates. Story 1.16 owns exit-criteria and parity-completeness release gates. Those jobs are intentionally outside this workflow.
