# Contract Spine CI Gates

Story 1.14 wires the blocking gates for the Contract Spine, generated SDK client, generated idempotency helpers, previous-spine baseline, and C13 parity oracle. The gates are offline and repository-local: they do not require Aspire, Dapr sidecars, provider credentials, live providers, tenant seed data, or nested submodule initialization.

## Local Command

Run the workflow-equivalent lane from the repository root:

```powershell
dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1
dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1
.\tests\tools\run-contract-spine-gates.ps1 -NoRestore
dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli
```

The script runs the focused OpenAPI gate tests and generated-client consistency tests. The McpCli namespace selector runs separately in the workflow and in the command above, covering decorated-operation eligibility, routing, spine drift, identifiers and packed closure independently of the PD10-red OpenAPI set. It is validation-mode only and must leave tracked generated output unchanged.

## CI Job

`.github/workflows/contract-spine.yml` defines the `contract-generated-artifact-gates` job. It uses `actions/checkout@v7` with `submodules: false`, `actions/setup-dotnet@v6` with `global-json-file: global.json`, then runs restore, build, the local gate script, and a separate `Run McpCli contract gates` step from the repository root. The separate step uses `!cancelled()` so it still reports its own exit status after the OpenAPI gate fails; it rejects a selector that executes zero tests. NuGet caching is deferred until lock-file support is introduced.

The job does not publish packages, sign artifacts, run live provider drift checks, initialize nested submodules, upload raw diffs, upload generated client bodies, or emit release evidence.

## Gate Inputs

| Gate | Input | Current status | Validation behavior |
|---|---|---|---|
| Contract Spine parse and drift | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml` | Present | OpenAPI tests parse the spine, resolve references, and assert metadata required by downstream generation. |
| Server OpenAPI emission | `src/Hexalith.Folders.Server` repository-local OpenAPI output | Reference-pending | Source discovery reports `prerequisite-drift` until exactly one non-self offline server OpenAPI source exists. |
| Previous-spine baseline | `tests/fixtures/previous-spine.yaml` | Captured baseline | The baseline must cover every current operation identity; approved removals require metadata-only deprecation evidence. |
| Generated SDK client | `src/Hexalith.Folders.Client/Generated/` and `src/Hexalith.Folders.Client/nswag.json` | Present | Client tests verify NSwag configuration, generated provenance, and stale-output detection. |
| McpCli inventory and declarations | `references/Hexalith.McpCli/_bmad-output/implementation-artifacts/folders-migration-inventory-v1.md`, decorated Contracts and the owner decision record | Present; root McpCli submodule required | The independent McpCli selector pins exact eligibility, CLR/wire/spine/routing/exemption/freshness columns and the spine digest. |
| McpCli canonical payload options mirror | `references/Hexalith.McpCli/src/Hexalith.McpCli.Core/Serialization/McpCliJson.cs` | Present; read-only source | Compare `CreatePayload` and `CreateResolver` with the local test mirror and check canonical omission and unknown-member rejection. |
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
dotnet run --project tests\tools\parity-oracle-generator\Hexalith.Folders.ParityOracleGenerator.csproj -- --contract src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml --initialize-baseline --force-baseline-overwrite
```

Do not hand-edit generated client files or parity rows. When an operation is intentionally removed or renamed, record an approved deprecation entry in `previous-spine.yaml` with the prior operation ID, rationale, approval reference, effective date, and repository-relative approval source.

## Prerequisite Drift

`prerequisite-drift` means a gate found missing source authority rather than a behavioral mismatch. Use it to decide which owner needs to run next:

- Missing server OpenAPI emission belongs to the future server OpenAPI surface work; the current server scaffold has no offline emitted source.
- Missing or stale generated SDK output belongs to Story 1.12.
- Missing or stale parity oracle output belongs to Story 1.13.
- Missing previous-spine approval evidence belongs to the baseline approval owner.

Story 1.15 owns safety invariant gates. Story 1.16 owns exit-criteria and parity-completeness release gates. Those jobs are intentionally outside this workflow.
