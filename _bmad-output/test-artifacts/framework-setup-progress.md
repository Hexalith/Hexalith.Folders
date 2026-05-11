---
stepsCompleted: ['step-01-preflight', 'step-02-select-framework', 'step-03-scaffold-framework', 'step-04-docs-and-scripts', 'step-05-validate-and-summary']
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-05-11'
---

# Test Framework Setup Progress

## Step 01 - Preflight

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

### Documentation Added

- Created `tests/README.md` with setup instructions, test commands, architecture overview, best practices, CI notes, and knowledge references.
- Documented the root-level-only submodule setup command and explicitly kept recursive submodule initialization out of default setup.

### Script Added

- Created `tests/run-tests.ps1` with `All`, `Coverage`, `Integration`, and `Testing` modes.
- Kept the script on `dotnet` commands rather than adding a Node/package layer.

### Verification

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1 -Mode Testing` passed: 14 tests.

## Step 05 - Validate And Summary

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
