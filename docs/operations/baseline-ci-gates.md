# Baseline CI Gates

Story 7.4 defines the pull-request baseline lane for mechanical repository health. The stable Folders-specific status-check name is `folders-specialized-gates`; branch protection is configured outside this repository.

The workflow is `.github/workflows/ci.yml`. It runs for pull requests and pushes to `main`. Checkout uses `submodules: false`; the workflow then initializes only root-declared submodules and never initializes nested submodules recursively:

```text
git -c submodule.recurse=false submodule update --init
```

## Gate Categories

`tests/tools/run-baseline-ci-gates.ps1` exposes these failure categories:

- `dependency-mode`: verifies local default/Debug source mode plus explicit Release and `CI=true` package-reference modes.
- `restore`: `dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1`
- `build`: `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1`
- `format`: `dotnet format whitespace Hexalith.Folders.CI.slnx --verify-no-changes --no-restore --include ./src/ ./tests/ ./samples/`
- `lint`: `dotnet format analyzers Hexalith.Folders.CI.slnx --verify-no-changes --no-restore --severity warn --include ./src/ ./tests/ ./samples/`
- `unit-tests`: explicit hermetic unit-test project allow-list
- `package-mode-restore`: fresh explicit Release/package restore of `Hexalith.Folders.UI.Tests`.
- `package-mode-build`: explicit Release/package build of `Hexalith.Folders.UI.Tests` without reusing source-mode assets.
- `package-mode-test`: executes the UI tests against `Hexalith.FrontComposer.Testing` from its NuGet package, including `InMemoryStorageService` consumption.

CI and Release builds resolve Hexalith dependencies from centrally pinned NuGet packages. Local default/Debug development may still use root-declared source dependencies. Those submodules are independent repositories with their own formatting standards, so the `format` and `lint` gates are deliberately scoped with `--include ./src/ ./tests/ ./samples/` to evaluate only this repository's code.

## Unit Allow-List

The baseline lane runs these projects only:

- `tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj`
- `tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj`
- `tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj`
- `tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj`
- `tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj`
- `tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj`
- `tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj`
- `samples/Hexalith.Folders.Sample.Tests/Hexalith.Folders.Sample.Tests.csproj`

Microsoft.Testing.Platform runs full-project entries without a selector. The two deliberately selected entries execute the already-built xUnit v3 assemblies directly, never project-level `dotnet test --filter`:

- `Hexalith.Folders.Contracts.Tests.dll` uses repeated `-class` selectors for the deployment-governance allow-list.
- `Hexalith.Folders.Client.Tests.dll` uses two `-method-` exclusions for the out-of-process regeneration checks already owned by the contract/parity gate.
- Folders, CLI, MCP, Testing, UI, Workers, and sample tests run as complete projects.

Excluded from this lane:

- `tests/Hexalith.Folders.IntegrationTests`
- `tests/Hexalith.Folders.UI.E2E.Tests`
- `tests/load/Hexalith.Folders.LoadTests`
- `tests/Hexalith.Folders.LoadTests.Tests`

Capacity smoke/calibration, retention/deletion, NFR traceability, contract/parity, security, safety, governance, accessibility, and browser checks are separate blocking steps/jobs for the same CI commit. Package sealing does not trust checked-in gate reports.

## Cache Inputs

`actions/setup-dotnet@v6.0.0` uses `global-json-file: global.json` and NuGet caching. Cache dependency paths are:

- `Directory.Packages.props`
- `global.json`
- `nuget.config`
- `references/Hexalith.Builds/Props/Directory.Packages.props`
- `**/*.csproj`

## Diagnostics

The gate writes a metadata-only report to `_bmad-output/gates/baseline-ci/latest.json`. The report may contain category names, relative project or script paths, statuses, and exit codes. It must not contain absolute local paths, secrets, tokens, tenant data, provider payloads, raw file contents, diffs, or environment dumps.
