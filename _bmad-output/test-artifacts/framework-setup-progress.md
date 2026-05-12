---
stepsCompleted: ['step-01-preflight', 'step-02-select-framework', 'step-03-scaffold-framework', 'step-04-docs-and-scripts', 'step-05-validate-and-summary']
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-05-12'
---

# Test Framework Setup Progress

## Step 01 - Preflight

### 2026-05-12 Preflight Refresh

- Configured `test_stack_type`: `auto`.
- Detected stack remains `backend` with a Blazor UI edge.
- Primary manifest remains `Hexalith.Folders.slnx`.
- Root package/browser indicators remain absent: no root `package.json`, `playwright.config.*`, `cypress.config.*`, or `cypress.json`.
- Existing Hexalith.Folders test projects are present and align with the intended .NET-first framework: xUnit v3, Shouldly, NSubstitute, Testcontainers, Microsoft.AspNetCore.Mvc.Testing, Aspire testing, YamlDotNet, and coverlet.
- Existing FrontComposer Playwright assets are submodule/sibling assets and do not apply to the Hexalith.Folders root.
- Preflight status: pass. Continue with framework selection using the existing xUnit-first setup as the baseline; avoid duplicating a browser framework unless UI smoke/accessibility coverage is explicitly introduced later.

### Stack Detection

- Configured `test_stack_type`: `auto`.
- Detected stack: `backend` with a Blazor UI edge.
- Primary manifest: `Hexalith.Folders.slnx`.
- Backend manifests found under `src/`, `tests/`, and `samples/` as .NET project files.
- No root `package.json`, root `playwright.config.*`, root `cypress.config.*`, or `cypress.json` found for Hexalith.Folders.
- Existing sibling/submodule frontend E2E assets were ignored for this workflow because they belong to `Hexalith.FrontComposer`, not the Hexalith.Folders root.

### Prerequisite Validation

- Backend project manifests are present.
- Existing test projects are present and aligned with the project context expectation: xUnit v3, Shouldly, NSubstitute, Testcontainers, and Aspire testing patterns.
- Existing .NET tests do not conflict with framework setup; they are the baseline to extend.
- No existing browser E2E framework exists in the Hexalith.Folders root.

### Context Gathered

- Root build uses .NET `net10.0`, nullable enabled, implicit usings enabled, `LangVersion=latest`, and warnings-as-errors.
- Package versions are centrally managed in `Directory.Packages.props`.
- Testing packages currently include `xunit.v3`, `Shouldly`, `NSubstitute`, `Testcontainers`, `Microsoft.AspNetCore.Mvc.Testing`, `Aspire.Hosting.Testing`, `YamlDotNet`, and `coverlet.collector`.
- Architecture positions Hexalith.Folders as an EventStore/Dapr/Aspire-backed module with REST, SDK, CLI, MCP, workers, and a read-only Blazor operations console.
- Highest-risk verification surfaces are tenant isolation, metadata-only audit/redaction, idempotency, workspace state transitions, cross-surface parity, provider capability contracts, Dapr policy behavior, and read-only UI accessibility.

### Preflight Decision

Proceed to framework selection. Risk calculation: default to a .NET-first test architecture with xUnit v3 and Aspire/Testcontainers as the blocking lane; add Playwright only for read-only operations-console smoke/accessibility checks when UI workflows become testable. Cypress is not favored because the repo is not a JavaScript frontend app and already follows .NET/Aspire conventions.

## Step 02 - Framework Selection

### 2026-05-12 Selection Refresh

### Selected Frameworks

- Primary blocking framework remains xUnit v3.
- Assertion style remains Shouldly.
- Test doubles remain NSubstitute for focused unit seams.
- Integration/runtime harness remains Microsoft.AspNetCore.Mvc.Testing, Aspire testing, and Testcontainers.
- Optional UI adjunct remains Playwright for read-only operations-console smoke/accessibility checks once stable UI routes and selectors exist.

### Rationale

- Detected stack is backend-first .NET; workflow rules map C#/.NET backend projects to xUnit by default.
- Current xUnit v3 documentation supports async tests, `Fact`/`Theory`, class fixtures, collection fixtures, and async fixture lifecycle patterns that match the repo's need for isolated unit tests plus shared integration resources.
- Microsoft Learn's ASP.NET Core integration testing guidance uses `WebApplicationFactory<TEntryPoint>` with xUnit fixtures and customizable test services, matching the existing package inventory and future server-boundary tests.
- Playwright's current configuration guidance supports retries, traces, screenshots, videos, and reporters for UI diagnostics, but those capabilities should be reserved for the read-only operations console after lower-level contracts stabilize.
- Cypress remains not selected: no root JavaScript app, no Cypress convention, and no risk reduction for the highest-priority tenant/isolation/idempotency/parity concerns.

### Risk Calculation

- P0/P1 coverage belongs in unit, integration, contract, and adapter tests: tenant isolation, metadata-only redaction, idempotency, workspace state transitions, cross-surface parity, and provider capability behavior.
- Browser automation is P2 until the operations console has stable route/selector contracts. Keep it thin to avoid flakiness and toolchain drift.

### Selected Frameworks

- Primary blocking framework: xUnit v3.
- Assertion style: Shouldly.
- Test doubles: NSubstitute where pure unit seams require substitution.
- Integration/runtime harness: `Microsoft.AspNetCore.Mvc.Testing`, Aspire testing, and Testcontainers.
- Optional UI adjunct: Playwright for read-only operations-console smoke/accessibility checks once the UI has stable routes and selectors.

### Rationale

- Detected stack is backend-first .NET, so the workflow selection rule maps to xUnit by default.
- The repo already has centralized versions for `xunit.v3`, `Shouldly`, `NSubstitute`, `Testcontainers`, `Microsoft.AspNetCore.Mvc.Testing`, and `Aspire.Hosting.Testing`; no new primary test runner is needed.
- Microsoft Learn guidance for ASP.NET Core integration testing favors xUnit-compatible test hosts and separate unit/integration test projects, which matches the existing `tests/Hexalith.Folders.*.Tests` layout.
- xUnit v3 fixture support matches this repo's need for class/collection fixtures around in-memory EventStore/Testcontainers/Aspire resources without pushing state into global static setup.
- Playwright documentation supports traces/artifacts/retries for CI debugging, but those costs only pay off for browser-facing workflows. For Hexalith.Folders, browser tests should cover the read-only operations console only after API/service behavior is stable.
- Cypress is not selected because there is no root JavaScript frontend application, no existing Cypress convention, and Cypress would add a second toolchain without improving the highest-risk verification layers.

### Risk Calculation

- P0/P1 risks are tenant isolation, metadata leakage, idempotency, workspace state transitions, provider parity, and cross-surface error mapping. These belong in unit, integration, contract, and adapter tests before E2E.
- UI risk is real but narrower: the operations console is read-only in MVP. It needs smoke/accessibility coverage, not a broad browser-first framework.
- Flakiness risk increases if Dapr/Aspire/provider behavior is tested primarily through UI. Keep browser tests thin and let lower levels carry the load.

## Step 03 - Scaffold Framework

### 2026-05-12 Scaffold Refresh

### Execution Mode

- Requested mode: `auto`.
- Resolved mode: `sequential`.
- Reason: no explicit user request for subagents or agent-team execution in this run.

### Scaffold Status

- Existing .NET-first scaffold is present and remains the correct framework shape.
- Verified shared helpers under `src/Hexalith.Folders.Testing/`:
  - `Factories/FoldersTestDataFactory.cs`
  - `Http/TestRequestHeaders.cs`
  - `Polling/Eventually.cs`
- Verified sample/coverage directories under `tests/Hexalith.Folders.Testing.Tests/`:
  - `Unit/`
  - `Api/`
  - `Integration/`
- Verified root `.env.example` contains `TEST_ENV`, `BASE_URL`, and `API_URL`.
- Verified `global.json` pins the .NET SDK, so no additional backend version file is needed.
- Verified `tests/run-tests.ps1` exposes `All`, `Coverage`, `Integration`, and `Testing` modes.

### Scaffold Decision

- No new files created during this refresh. The current scaffold already satisfies the backend .NET framework setup requirements.
- No Playwright/Cypress scaffold added. Browser automation remains deferred until the operations console has stable UI contracts.

### Execution Mode

- Requested mode: `auto`.
- Resolved mode: `sequential`.
- Reason: no explicit user request for subagents or agent-team execution in this run.

### Scaffold Created

- Added shared test data factory records and helpers under `src/Hexalith.Folders.Testing/Factories/`.
- Added API request header helper under `src/Hexalith.Folders.Testing/Http/`.
- Added async polling helper under `src/Hexalith.Folders.Testing/Polling/`.
- Added root `.env.example` with `TEST_ENV`, `BASE_URL`, and `API_URL`.
- Added idiomatic xUnit sample coverage split across:
  - `tests/Hexalith.Folders.Testing.Tests/Unit/`
  - `tests/Hexalith.Folders.Testing.Tests/Api/`
  - `tests/Hexalith.Folders.Testing.Tests/Integration/`

### Pattern Application

- Factory pattern uses explicit override records so tests can state only the fields that matter.
- Helpers are framework-agnostic and pure where possible; xUnit remains at the test-project boundary.
- Polling uses explicit timeout and interval values with cancellation support, avoiding hard waits.
- API header helper centralizes correlation, idempotency, and task context for future parity tests.
- No Playwright/Cypress root scaffold was added because the detected stack is backend-first .NET and browser testing is only an adjunct for later UI smoke/accessibility coverage.

### Verification

- `dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj` passed: 14 tests.
- `dotnet build Hexalith.Folders.slnx` passed with 0 warnings and 0 errors.

## Step 04 - Documentation And Scripts

### 2026-05-12 Documentation Refresh

### Documentation Checked

- `tests/README.md` remains the primary framework guide with setup, running tests, architecture, best practices, CI notes, and knowledge references.
- Added a focused debugging section with single-project and single-test `dotnet test` examples.
- Kept the root-level-only submodule setup command and the explicit warning against recursive submodule initialization.

### Scripts Checked

- `tests/run-tests.ps1` remains the idiomatic local script for the .NET test framework.
- Modes available: `All`, `Coverage`, `Integration`, and `Testing`.
- No `package.json` scripts were added because Hexalith.Folders has no root Node frontend test harness.

### Documentation Added

- Created `tests/README.md` with setup instructions, test commands, architecture overview, best practices, CI notes, and knowledge references.
- Documented the root-level-only submodule setup command and explicitly kept recursive submodule initialization out of default setup.

### Script Added

- Created `tests/run-tests.ps1` with `All`, `Coverage`, `Integration`, and `Testing` modes.
- Kept the script on `dotnet` commands rather than adding a Node/package layer.

### Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1 -Mode Testing` passed: 14 tests.

## Step 05 - Validate And Summary

### 2026-05-12 Validation Refresh

### Checklist Validation

- Preflight success: passed.
- Directory structure: passed for backend .NET layout; browser-specific `tests/e2e` and `tests/support` remain not applicable until Playwright is intentionally introduced.
- Config correctness: passed; `.csproj` test projects and central package management define xUnit v3, Shouldly, NSubstitute, Testcontainers, Microsoft.AspNetCore.Mvc.Testing, Aspire testing, YamlDotNet, runner, and coverlet packages.
- Fixtures/factories/helpers: passed via `src/Hexalith.Folders.Testing/Factories`, `Http`, and `Polling`.
- Docs and scripts: passed via `tests/README.md` and `tests/run-tests.ps1`.
- Security check: passed for refreshed docs and framework artifacts; no credentials or recursive submodule setup instructions were introduced.

### Verification Commands

- `dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj` passed: 25 tests.
- `dotnet build Hexalith.Folders.slnx` passed: 0 warnings, 0 errors.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1 -Mode Testing` passed: 25 tests.
- `dotnet test Hexalith.Folders.slnx` passed across all current solution test projects.

### Completion Summary

- Framework selected: xUnit v3 as the primary blocking framework.
- Supporting tools: Shouldly, NSubstitute, Testcontainers, Microsoft.AspNetCore.Mvc.Testing, Aspire testing, YamlDotNet, and coverlet.
- UI adjunct: Playwright deferred for future read-only operations-console smoke/accessibility coverage.
- Artifacts refreshed:
  - `tests/README.md`
  - `_bmad-output/test-artifacts/framework-setup-progress.md`
- Knowledge fragments applied: test levels, test quality, fixture architecture, data factories, Playwright config guardrails, API request, polling, logging, and contract-testing guidance.
- Official docs cross-checks applied: xUnit v3 fixture/async guidance, Microsoft Learn ASP.NET Core integration testing with `WebApplicationFactory`, and Playwright test artifact/retry configuration guidance.

### Checklist Validation

- Preflight success: passed.
- Directory structure created: passed for backend .NET layout; browser-specific `tests/e2e` and `tests/support` checklist items are not applicable until Playwright is intentionally introduced for UI coverage.
- Config correctness: passed; existing `.csproj` and central package management already define xUnit v3, Shouldly, coverlet, and runner packages.
- Environment configuration: passed with `.env.example`.
- Fixtures and factories: passed via `src/Hexalith.Folders.Testing/Factories`, `Http`, and `Polling`.
- Sample tests: passed via `Unit`, `Api`, and `Integration` examples in `tests/Hexalith.Folders.Testing.Tests`.
- Docs and scripts: passed via `tests/README.md` and `tests/run-tests.ps1`.
- Security scan: passed for generated files; matches in existing fixture tests are sentinel assertions, not secrets.

### Completion Summary

- Framework selected: xUnit v3 as the primary blocking framework, with Playwright deferred to later read-only UI smoke/accessibility coverage.
- Artifacts created:
  - `.env.example`
  - `src/Hexalith.Folders.Testing/Factories/*`
  - `src/Hexalith.Folders.Testing/Http/TestRequestHeaders.cs`
  - `src/Hexalith.Folders.Testing/Polling/Eventually.cs`
  - `tests/Hexalith.Folders.Testing.Tests/{Unit,Api,Integration}/*`
  - `tests/README.md`
  - `tests/run-tests.ps1`
- Knowledge fragments applied: fixture architecture, data factories, test levels, test quality, Playwright config guardrails, API request, auth session, polling, logging, burn-in, network-error-monitor, and contract testing.
- Official docs cross-checks applied: Microsoft Learn ASP.NET Core integration testing, xUnit v3 fixture/testing overview, and Playwright trace/artifact/retry configuration guidance.

### Final Verification

- `dotnet test Hexalith.Folders.slnx` passed across the scaffold suite.
- `dotnet build Hexalith.Folders.slnx` passed with 0 warnings and 0 errors.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1 -Mode Testing` passed.
