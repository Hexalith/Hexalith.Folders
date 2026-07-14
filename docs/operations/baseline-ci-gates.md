# Baseline CI Gates

Story 7.4 defines the pull-request baseline lane for mechanical repository health. The stable required status-check name is `baseline-build-and-unit-gates`; branch protection is configured outside this repository, but this job name is intentionally stable so maintainers can require it.

The workflow is `.github/workflows/ci.yml`. It runs for `pull_request` and pushes to `main`, `next`, `alpha`, and `beta`. Checkout uses `submodules: false`; the workflow then initializes only the documented root-level build submodules and never initializes nested submodules recursively:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
```

## Gate Categories

`tests/tools/run-baseline-ci-gates.ps1` exposes these failure categories:

- `dependency-mode`: evaluates the UI test project in unqualified/default, explicit Debug, and explicit Release/package modes and blocks unless the global source/package properties and representative source-availability flags agree.
- `restore`: `dotnet restore Hexalith.Folders.slnx -p:NuGetAudit=false`
- `build`: `dotnet build Hexalith.Folders.slnx --no-restore`
- `format`: `dotnet format whitespace Hexalith.Folders.slnx --verify-no-changes --no-restore --include ./src/ ./tests/ ./samples/`
- `lint`: `dotnet format analyzers Hexalith.Folders.slnx --verify-no-changes --no-restore --severity warn --include ./src/ ./tests/ ./samples/`
- `unit-tests`: explicit hermetic unit-test project allow-list
- `package-mode-restore`: fresh explicit Release/package restore of `Hexalith.Folders.UI.Tests`.
- `package-mode-build`: explicit Release/package build of `Hexalith.Folders.UI.Tests` without reusing source-mode assets.
- `package-mode-test`: executes the UI tests against `Hexalith.FrontComposer.Testing` from its NuGet package, including `InMemoryStorageService` consumption.

The `build` gate needs the root-level submodule working trees present (the `Hexalith.Folders.Server`/`Workers`/`AppHost` host projects reference sibling submodule source). Those submodules are independent repositories with their own formatting standards (for example, CRLF line-endings), so the `format` and `lint` gates are deliberately scoped with `--include ./src/ ./tests/ ./samples/` to evaluate only this repository's own code. The exact `./src/` path form matters: a bare `--include src tests` matches no files and makes the gate pass vacuously.

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

Several projects use baseline-safe filters so the lane stays focused on mechanical health:

- `tests/Hexalith.Folders.Tests` excludes two stale provider-boundary guard methods that need provider-scope cleanup outside Story 7.4.
- `tests/Hexalith.Folders.Contracts.Tests` runs smoke, baseline CI, release package, retention/deletion, production observability, and consumer documentation conformance checks only. Broader contract, parity, security, and provider cleanup belongs to later Epic 7 consolidation stories.
- `tests/Hexalith.Folders.Client.Tests` excludes isolated regeneration tests that perform their own restore and can be affected by NuGet audit network access.
- `tests/Hexalith.Folders.Testing.Tests` excludes scaffold/deferred-artifact policy checks that track broader repository governance drift.
- `tests/Hexalith.Folders.Workers.Tests` excludes tenant subscription endpoint tests that bind local sockets; worker endpoint coverage belongs in a lane with socket-capable test hosts.
- `samples/Hexalith.Folders.Sample.Tests` runs hermetic SDK lifecycle example checks with a fake handler and no AppHost, Dapr, provider, or network dependency.

Excluded from this lane:

- `tests/Hexalith.Folders.IntegrationTests`
- `tests/Hexalith.Folders.UI.E2E.Tests`
- `tests/load/Hexalith.Folders.LoadTests`
- `tests/Hexalith.Folders.LoadTests.Tests`

Container publish, live Dapr policy, provider drift, Playwright browser, capacity, release artifact upload, and live registry gates are intentionally outside the baseline lane.

## Cache Inputs

`actions/setup-dotnet@v5` uses `global-json-file: global.json` and NuGet caching. Cache dependency paths are:

- `Directory.Packages.props`
- `global.json`
- `nuget.config`
- `**/*.csproj`

## Diagnostics

The gate writes a metadata-only report to `_bmad-output/gates/baseline-ci/latest.json`. The report may contain category names, relative project or script paths, statuses, and exit codes. It must not contain absolute local paths, secrets, tokens, tenant data, provider payloads, raw file contents, diffs, or environment dumps.
