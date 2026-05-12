# Hexalith.Folders Test Framework

Hexalith.Folders uses a .NET-first test framework: xUnit v3 for execution, Shouldly for assertions, NSubstitute for focused unit-test doubles, Testcontainers/Aspire test host patterns for integration boundaries, and shared helpers in `src/Hexalith.Folders.Testing`.

## Setup

1. Install the .NET SDK version from `global.json`.
2. Initialize only root-level submodules when needed:

   ```powershell
   git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
   ```

3. Restore from the repository root:

   ```powershell
   dotnet restore Hexalith.Folders.slnx
   ```

Do not run recursive submodule initialization unless nested submodule work is explicitly requested.

## Running Tests

Run the full scaffold lane:

```powershell
dotnet test Hexalith.Folders.slnx
```

Run the shared testing infrastructure lane:

```powershell
dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj
```

Run with coverage collection:

```powershell
dotnet test Hexalith.Folders.slnx --collect:"XPlat Code Coverage"
```

Run through the helper script:

```powershell
.\tests\run-tests.ps1 -Mode All
.\tests\run-tests.ps1 -Mode Coverage
.\tests\run-tests.ps1 -Mode Integration
```

## Debugging Tests

Run a single project while iterating:

```powershell
dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj
```

Run one named test with detailed output:

```powershell
dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --filter FullyQualifiedName~EventuallyTests --logger "console;verbosity=detailed"
```

For integration failures, prefer inspecting the failing request, headers, correlation ID, idempotency key, and generated test artifacts before widening to the whole solution. Browser headed/debug mode is intentionally not part of the default lane; add Playwright only when read-only operations-console routes have stable selectors.

## Architecture

- `tests/Hexalith.Folders.*.Tests` mirrors the production project boundaries.
- `tests/fixtures` stores normative cross-project data such as parity, idempotency, and redaction corpora.
- `src/Hexalith.Folders.Testing/Factories` provides override-based data factories for tenant, folder, task, correlation, idempotency, and authorization contexts.
- `src/Hexalith.Folders.Testing/Http` centralizes test request headers for correlation, idempotency, and task context.
- `src/Hexalith.Folders.Testing/Polling` provides bounded polling for eventual consistency without hard waits.

## Best Practices

- Prefer unit tests for pure domain rules, state transitions, and input validation.
- Use integration tests for EventStore, Dapr, Aspire, provider, and REST boundary behavior.
- Keep browser tests thin and reserve Playwright for read-only operations-console smoke and accessibility coverage when stable UI routes exist.
- Use factories with explicit overrides instead of static fixture objects when creating scenario data.
- Keep assertions visible in test bodies; helpers should arrange, extract, poll, or normalize.
- Every test that creates external state must own cleanup or use a fixture that does.
- No test may require production secrets, provider credentials, tenant seed data, running sidecars, or nested submodule initialization unless clearly marked outside the blocking lane.

## CI Notes

The blocking lane should start with:

```powershell
dotnet restore Hexalith.Folders.slnx
dotnet build Hexalith.Folders.slnx
dotnet test Hexalith.Folders.slnx --collect:"XPlat Code Coverage"
```

Future CI gates should add focused jobs for the parity contract schema, C6 state-transition matrix coverage, redaction sentinel corpus, cache-key tenant prefix enforcement, and provider contract drift checks.

## Knowledge References

- BMAD TEA fragments: fixture architecture, data factories, test levels, test quality, Playwright configuration, API request, polling, logging, and contract testing.
- Microsoft Learn: ASP.NET Core integration testing guidance with xUnit and test host patterns.
- xUnit v3 docs: fixture support, async tests, and .NET runner integration.
- Playwright docs: trace/artifact/retry guidance for later UI coverage.
