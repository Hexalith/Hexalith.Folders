---
stepsCompleted: ['step-01-preflight', 'step-02-select-framework', 'step-03-scaffold-framework', 'step-04-docs-and-scripts', 'step-05-validate-and-summary']
lastStep: 'step-05-validate-and-summary'
lastSaved: '2026-05-19'
---

# Test Framework Setup Progress

## Step 01 - Preflight

### 2026-05-19 Playwright Lane Preparation

- Scope change: Jerome selected `C 1+2` — Create mode with goals (1) reaffirm the xUnit v3 backend baseline and (2) **prepare the Playwright lane** for the read-only operations console without writing tests yet.
- Preflight finding: prior runs (2026-05-12 and 2026-05-19 AM) had already deferred Playwright. Re-checked and confirmed `Microsoft.Playwright` was missing from `Directory.Packages.props`, no `tests/Hexalith.Folders.UI.E2E.Tests` project existed, and no install script lived under `tests/`. The lane was genuinely absent, so preparation work has real value rather than churn.
- Sprint state still confirms the deferral rationale: Epic 6 (operations console) is entirely in `backlog`; story 6-2 (`scaffold-frontcomposer-hosted-read-only-operations-console`) has not started.
- Decision: prepare the lane — central package pin, test project scaffold with a Playwright fixture, install script, route/selector contract document, and runner-script wiring — but keep the only test method `[Fact(Skip = "...")]` until Epic 6 ships a real UI.

### 2026-05-19 Preflight Refresh

- Configured `test_stack_type`: `auto`.
- Detected stack remains `backend` with a Blazor Server UI edge; UI is server-rendered .NET, not a JS frontend.
- Primary manifest remains `Hexalith.Folders.slnx` (12 production projects + 12 test projects + 2 tools).
- No root `package.json`, no root `playwright.config.*`, no `cypress.config.*` or `cypress.json`. All `package.json` / `playwright.config.ts` matches live inside submodule trees (`Hexalith.FrontComposer`, `Hexalith.Tenants`, `Hexalith.EventStore`, `Hexalith.Memories`, `Hexalith.Commons`) and remain out of scope for Hexalith.Folders root.
- Testing package inventory in `Directory.Packages.props` unchanged in shape: `xunit.v3 3.2.2`, `xunit.v3.assert 3.2.2`, `xunit.runner.visualstudio 3.1.5`, `Shouldly 4.3.0`, `NSubstitute 5.3.0`, `Testcontainers 4.10.0`, `Microsoft.AspNetCore.Mvc.Testing 10.0.5`, `Microsoft.NET.Test.Sdk 18.5.1`, `Aspire.Hosting.Testing 13.2.1`, `YamlDotNet 17.1.0`, `coverlet.collector 10.0.1`.
- Solution growth since 2026-05-12: `tests/Hexalith.Folders.Tests` (core unit tests, Aggregates/Authorization/Projections subdirectories) was added alongside `tests/Hexalith.Folders.Testing.Tests`; tooling tests appear under `tests/tools/parity-oracle-generator` and `tests/tools/pattern-examples`.
- `src/Hexalith.Folders.Testing/` now exposes `FoldersTestingModule.cs` plus the existing `Factories/`, `Http/`, and `Polling/` helpers — shape consistent with prior refresh.
- In-flight work (story 2-5 Inspect Effective Permissions) is introducing a new `Authorization/` namespace under `src/Hexalith.Folders/` with effective-permissions read model, principal kinds, evidence sources, lifecycle states, and task scope types — a high-risk verification surface that will pull on fixture/factory and integration patterns established in the existing framework.
- Preflight status: pass. No conflicting E2E framework or alternate runner has been introduced; the .NET-first xUnit v3 baseline is still the right anchor.

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

### 2026-05-19 Playwright Lane Preparation

- Selection unchanged for the blocking lane: xUnit v3 (3.2.2) remains primary, with Shouldly, NSubstitute, Testcontainers, `Microsoft.AspNetCore.Mvc.Testing`, `Aspire.Hosting.Testing`, `coverlet.collector`, `YamlDotNet` as supporting infrastructure.
- Adjunct selection finalized: **Microsoft.Playwright 1.59.0** (current stable per nuget.org as of 2026-05-19) is now the chosen browser automation library for the read-only operations console. Cypress remains rejected — no JavaScript frontend in the Hexalith.Folders root, no convention, no risk reduction.
- Rationale for `Microsoft.Playwright` over `Microsoft.Playwright.MSTest` / `Microsoft.Playwright.NUnit`: the project standardizes on xUnit v3 across every other test project. Introducing a parallel runner just for E2E would add cognitive load with no upside; xUnit fixtures (`IAsyncLifetime`, `ICollectionFixture`) cover the same `PageTest`-style auto-fixturing once the host fixture is in place.
- Lane is **deferred-active**: project compiles and is discovered, but the only test is `[Fact(Skip = "...")]` until Epic 6 story 6-2 ships an operations console host. This avoids writing flake-prone E2E tests against a moving target.

### 2026-05-19 Selection Refresh

### Selected Frameworks

- Primary blocking framework: xUnit v3 (3.2.2) — unchanged.
- Assertion style: Shouldly (4.3.0) — unchanged.
- Test doubles: NSubstitute (5.3.0) — unchanged.
- Integration/runtime harness: `Microsoft.AspNetCore.Mvc.Testing` (10.0.5), `Aspire.Hosting.Testing` (13.2.1), Testcontainers (4.10.0) — unchanged.
- Coverage: `coverlet.collector` (10.0.1) — unchanged.
- Fixture data: `YamlDotNet` (17.1.0) — unchanged.
- Optional UI adjunct: Playwright remains deferred until the read-only operations console has stable routes/selectors that justify browser smoke + accessibility coverage.

### Rationale

- Detected stack remains backend-first .NET, so the workflow selection rule maps to xUnit v3 by default. No alternate runner (NUnit, MSTest) has been introduced; the central package list shows only xUnit v3 packages.
- New `tests/Hexalith.Folders.Tests` project already uses the xUnit v3 baseline for Aggregates/Authorization/Projections coverage, confirming the framework choice in practice.
- The in-flight effective-permissions read model (story 2-5) is a high-value match for xUnit v3 facilities: `Theory` + `MemberData` for matrix-style permission evaluation (principal kind × lifecycle state × evidence source), `IClassFixture`/`ICollectionFixture` for shared seeded read-model snapshots, async `Fact` for query handlers that propagate `CancellationToken`.
- ASP.NET Core integration testing via `WebApplicationFactory<TEntryPoint>` continues to be the official, supported pattern on Microsoft Learn — directly applicable for `/api/v1/...`, `/process`, and `/project` boundary tests that the Server project exposes today.
- Aspire test-host patterns plus Testcontainers cover the Dapr/EventStore/projection lane without baking sidecar requirements into unit/scaffold tests, which preserves the project rule that builds must not require running Dapr, Keycloak, Redis, GitHub, or Forgejo.
- NSubstitute is preferred over Moq because the existing codebase and sibling modules already standardise on it; introducing a second mocking library would add cognitive load without risk reduction.
- Shouldly's diagnostics align with the operational console's redaction discipline: failure messages render expected/actual without leaking arbitrary object dumps.
- Cypress remains not selected: no root JavaScript app, no Cypress convention, no risk reduction for the highest-priority verification surfaces (tenant isolation, redaction, idempotency, parity, provider capability contracts, cache-key tenant prefix enforcement).

### Risk Calculation

- P0/P1 risks unchanged: tenant isolation, metadata-only telemetry/audit/redaction, idempotency, workspace state-machine correctness, cross-surface parity (REST/SDK/CLI/MCP), provider capability contracts, cache-key tenant prefix gating, mid-task authorization revocation, and the new effective-permissions evaluation order (tenant access → folder ACL → path policy → execution).
- Optimal level coverage:
  - **Unit (xUnit + Shouldly + NSubstitute):** aggregate decisions, validators, principal/evidence/lifecycle state mapping, permission evaluation logic, redaction rules.
  - **Integration (xUnit + WebApplicationFactory + Aspire.Hosting.Testing + Testcontainers):** REST endpoints, EventStore handlers, projection lifecycle, Dapr policy behaviour, idempotency stores, tenant-access projection freshness.
  - **Contract/parity (xUnit + YamlDotNet + parity-oracle-generator):** OpenAPI parity, CLI/MCP/SDK behavioural parity, redaction corpus, audit-leakage corpus.
  - **E2E (deferred Playwright):** read-only operations console smoke + accessibility once route/selector contracts are stable.
- Flakiness budget stays tight by keeping high-fidelity coverage at unit/integration levels and refusing to use browser tests to compensate for backend gaps.

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

### 2026-05-19 Playwright Lane Preparation

- Files created:
  - `tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj` — xUnit v3 test project with `Microsoft.Playwright`, `Microsoft.NET.Test.Sdk`, `Shouldly`, `xunit.v3`, `xunit.runner.visualstudio`, `coverlet.collector`, plus a project reference to `Hexalith.Folders.Testing`. `IsPackable=false` and `RootNamespace=Hexalith.Folders.UI.E2E.Tests`.
  - `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/PlaywrightFixture.cs` — `IAsyncLifetime` collection fixture owning `IPlaywright` + headless `IBrowser`. Missing-browser surfaces an actionable `InvalidOperationException` pointing at `install-playwright.ps1` rather than hanging or throwing an opaque `PlaywrightException`.
  - `tests/Hexalith.Folders.UI.E2E.Tests/Fixtures/PlaywrightCollection.cs` — xUnit `[CollectionDefinition("Playwright")]` so the fixture initializes once per test run.
  - `tests/Hexalith.Folders.UI.E2E.Tests/Smoke/OperationsConsolePlaceholderSmokeTests.cs` — one `[Fact(Skip = "Pending Epic 6 story 6-2 ...")]` method. Reports as Skipped in test runs so the lane is visible without burning CI cycles.
  - `tests/Hexalith.Folders.UI.E2E.Tests/README.md` — route + selector contract, when-to-enable rules, network/wait/accessibility/redaction discipline, project layout, and knowledge references. This file is the authoritative pre-Epic-6 contract for anyone adding a UI test.
  - `tests/install-playwright.ps1` — bootstrap script that builds the UI E2E project to materialize the Playwright runtime, then locates and invokes the generated `playwright.ps1 install` for Chromium. `-SkipBuild` flag for CI scenarios where the build is already current.
- Files updated:
  - `Directory.Packages.props` — added `<PackageVersion Include="Microsoft.Playwright" Version="1.59.0" />` under the `Testing` ItemGroup.
  - `Hexalith.Folders.slnx` — registered the new project under `/tests/` immediately before `Hexalith.Folders.UI.Tests`.
- Patterns applied:
  - Lazy fixture initialization with explicit, actionable error mapping (matches `confidence-gate.md` and `selector-resilience.md` guidance — fail loudly, fail informatively).
  - Network-first discipline documented in the project README rather than enforced by infrastructure that nothing yet calls. When the first real test arrives, the dev follows the contract or fails review.
  - `data-testid` is mandated as the only allowed primary selector strategy — pre-empting a class of flake before any test exists to demonstrate it.
  - Placeholder smoke test is explicitly named and skipped, not silently absent. Skipped tests show up in CI reports; "no tests" silently hides the deferral.
- Patterns explicitly **not** applied yet:
  - No host fixture (`OperationsConsoleHostFixture`) created — Epic 6 must define what "operations console host" means before we can wire `DistributedApplicationTestingBuilder` or `WebApplicationFactory<TEntryPoint>` for it. Creating an empty placeholder would just be dead code.
  - No accessibility scanner package selected. `Deque.AxeCore.Playwright` is the most likely choice but will be locked in when the first real page-level test lands.
  - No routes constants file. Story 6-2 must produce the route inventory; the test project consumes it then.

### 2026-05-19 Scaffold Refresh

### Execution Mode

- Requested mode: `auto` (`tea_execution_mode` from config).
- Resolved mode: `sequential`.
- Reason: no explicit user request for subagents or agent-team execution; main-context execution remains coherent for a refresh-style verification pass.

### Scaffold Status

- Existing .NET-first scaffold remains the correct framework shape; no new top-level files needed for backend coverage.
- Shared helpers under `src/Hexalith.Folders.Testing/` have grown organically with the project, all following the override-record pattern from the `data-factories.md` knowledge fragment:
  - `Factories/FoldersTestDataFactory.cs` (existing baseline).
  - `Factories/FolderCreationTestDataFactory.cs` (new — folder-creation envelope factory).
  - `Factories/OrganizationAclTestDataFactory.cs` (new — organization ACL factory for the 2-5 effective-permissions surface).
  - `Factories/TestAuthorizationContext.cs` + `TestAuthorizationContextOverrides.cs` (new — auth context with explicit override record).
  - `Factories/TestFolderContext.cs` + `TestFolderContextOverrides.cs` (new — folder context with explicit override record).
  - `Http/TestRequestHeaders.cs`.
  - `Polling/Eventually.cs`.
  - `FoldersTestingModule.cs` (module-wiring entry for the testing helpers).
- Sample/coverage layout under `tests/Hexalith.Folders.Testing.Tests/`:
  - `Unit/FolderCreationTestDataFactoryTests.cs`, `FoldersTestDataFactoryTests.cs`, `OrganizationAclTestDataFactoryTests.cs`.
  - `Api/TestRequestHeadersTests.cs`.
  - `Integration/EventuallyTests.cs`.
- Core unit-test project `tests/Hexalith.Folders.Tests/` now mirrors the production layout:
  - `Aggregates/Folder/`, `Aggregates/Organization/`.
  - `Projections/TenantAccess/`.
  - `Authorization/EffectivePermissionsAuthorizationGateTests.cs`, `EffectivePermissionsLayeringTests.cs`, `EffectivePermissionsMetadataLeakageTests.cs`, `EffectivePermissionsReadModelFreshnessTests.cs`, `EffectivePermissionsRevocationFreshnessTests.cs`, `EffectivePermissionsTaskScopeTests.cs`, `EffectivePermissionsTestSupport.cs`, `TenantAccessAuthorizerTests.cs`.
  - `FoldersModuleSmokeTests.cs` for module-shape verification.
- Tool tests in `tests/tools/parity-oracle-generator` and `tests/tools/pattern-examples` extend the solution-test surface without inventing a new framework.
- Root scaffold artifacts all present and current: `.env.example`, `global.json`, `tests/README.md`, `tests/run-tests.ps1`.
- No new framework adapter added: `tea_use_playwright_utils = true` in config maps to the Node/UI runtime path; for this backend stack the Playwright-Utils package is not installed and is not needed. Treating the flag as inert for the .NET surface.
- `tea_use_pactjs_utils = false` in config — Pact.js scaffolding intentionally skipped. Contract testing for Hexalith.Folders runs through OpenAPI parity, provider contract corpus (`tests/fixtures/parity-contract.yaml`), and provider-pinned hermetic fixtures rather than a Node Pact harness.

### Scaffold Decision

- No new files created during this refresh. The existing scaffold absorbs the in-flight 2-5 effective-permissions work without changes to framework infrastructure.
- Follow-up suggestion (low risk, deferred): consider adding a `Core` mode to `tests/run-tests.ps1` that targets `tests/Hexalith.Folders.Tests/` directly. The current `All` mode is correct; this would just shorten the inner-loop command developers run against the core unit-test project. Not blocking.
- Browser-automation scaffold deliberately not added: operations console remains read-only MVP without stable route/selector contracts. Playwright would be a flakiness-debt risk without payoff.

### Pattern Application

- Factory pattern uses explicit override records (`Test*Overrides.cs`) so tests state only the fields that matter — matches `data-factories.md` guidance.
- Test-support classes (`EffectivePermissionsTestSupport.cs`) centralize seeded read-model state and helper builders, keeping individual test files focused on intent rather than wiring.
- Polling helper (`Eventually.cs`) keeps eventual-consistency assertions explicit (timeout + interval + cancellation) — aligns with `recurse.md` and `timing-debugging.md` knowledge fragments.
- Request header helper centralizes correlation/idempotency/task headers so parity tests across REST/SDK/CLI/MCP can stay symmetric.
- Aggregates/Projections/Authorization mirror the `src/Hexalith.Folders/` layout one-to-one, satisfying the project rule that test projects mirror `src/` and that concept areas stay together.

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

### 2026-05-19 Playwright Lane Preparation

- `tests/README.md` updates:
  - Added `UiE2E` to the helper-script invocation list.
  - Reworded the debugging note: removed the "add Playwright only when ..." admonition (it has now been done — lane is added, tests are deferred); replaced with "the UI E2E lane is wired but its only test is skipped pending Epic 6 story 6-2."
  - Expanded the "best practices" bullet on browser tests to point at `tests/Hexalith.Folders.UI.E2E.Tests/README.md` as the authoritative route/selector contract.
  - Added a new section **UI End-to-End Lane (deferred until Epic 6)** under the existing knowledge-reference section: bootstrap command (`pwsh .\tests\install-playwright.ps1`), local execution command, and contract pointer.
- `tests/run-tests.ps1` updates:
  - Added `UiE2E` to the `ValidateSet` and a matching `switch` case that runs `dotnet test` against `tests\Hexalith.Folders.UI.E2E.Tests\Hexalith.Folders.UI.E2E.Tests.csproj`.
- New script:
  - `tests/install-playwright.ps1` — see Step 03 entry.
- `tests/Hexalith.Folders.UI.E2E.Tests/README.md` is the single source of truth for the UI E2E contract; the root `tests/README.md` links to it rather than duplicating it.

### 2026-05-19 Documentation Refresh

### Documentation Checked

- `tests/README.md` remains the primary framework guide. Re-read against current code state and found two drift items, both addressed in this refresh:
  - The "Run through the helper script" snippet was missing the `Testing` mode that `tests/run-tests.ps1` already exposes. Fixed.
  - The CI/local gate section listed `run-safety-invariant-gates.ps1` and `run-governance-completeness-gates.ps1` but not `run-contract-spine-gates.ps1`, which has been added to `tests/tools/`. Fixed with a documented invocation matching the prior gate scripts' shape (`-SkipRestoreBuild`).
- Setup, architecture, best-practices, and CI sections still match the current scaffold and the project context's testing rules.

### Scripts Checked

- `tests/run-tests.ps1` modes intact: `All`, `Coverage`, `Integration`, `Testing`.
- `tests/tools/` now contains three governance gate scripts (safety invariant, governance/completeness, contract spine) plus the parity-oracle generator and pattern-examples projects.
- No `package.json` scripts present at root — Hexalith.Folders has no JS frontend test harness, so no Node script layer is required.

### Documentation Updates Made

- Added `Testing` mode line to the run-tests.ps1 snippet in `tests/README.md`.
- Added a `run-contract-spine-gates.ps1` invocation block beside the existing gate scripts in `tests/README.md`.

### Follow-Up Suggestions (Not Applied)

- `tests/run-tests.ps1` could gain a `Core` mode targeting `tests/Hexalith.Folders.Tests/` directly to shorten the inner-loop command for core domain/authorization tests. Surfacing this for Jerome's call rather than applying unilaterally; current `All` mode is still correct.

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

### 2026-05-19 Playwright Lane Preparation

#### Verification Commands

- `dotnet restore Hexalith.Folders.slnx` — passed. `Hexalith.Folders.UI.E2E.Tests` restored cleanly with `Microsoft.Playwright 1.59.0` and the existing testing package set. 38 of 39 projects were up-to-date for restore (single new project added).
- `dotnet build tests\Hexalith.Folders.UI.E2E.Tests\Hexalith.Folders.UI.E2E.Tests.csproj --no-restore` — passed: 0 warnings, 0 errors after the CA2007 fix below.
- `dotnet test tests\Hexalith.Folders.UI.E2E.Tests\Hexalith.Folders.UI.E2E.Tests.csproj --no-build` — passed: 1 test discovered, 1 skipped, 0 failed. The placeholder reports as `[SKIP]` with the Epic 6 deferral reason.

#### Build Issue Resolved

- Initial build failed with three `CA2007: Consider calling ConfigureAwait on the awaited task` errors in `PlaywrightFixture.cs` (`TreatWarningsAsErrors=true` promotes CA2007 to error). Resolved by adding `.ConfigureAwait(false)` to the three awaits in `InitializeAsync` and `DisposeAsync`. Chose the explicit-per-await fix over a `tests/Directory.Build.props` `NoWarn` entry because: (1) the cause was a fixture-specific code path, (2) the sibling-module suppression pattern in Hexalith.Tenants / Hexalith.EventStore tests would mask future cases for the rest of the test surface, and (3) explicit configuration in async library-style code is a defensible default even in test infrastructure.

#### Acceptance Criteria

- Microsoft.Playwright centralized: passed (`Directory.Packages.props` updated).
- New test project compiles under `TreatWarningsAsErrors=true`, `Nullable=enable`, `ImplicitUsings=enable`: passed.
- Solution shape preserved: passed (`Hexalith.Folders.slnx` includes the new project alongside `Hexalith.Folders.UI.Tests`).
- Placeholder smoke test discoverable and skipped (not silently absent, not running, not failing): passed.
- Bootstrap script idempotent and parameterized: passed (`-Browser`, `-SkipBuild`).
- Helper-script integration: passed (`run-tests.ps1 -Mode UiE2E`).
- Route/selector contract documented and authoritative: passed (`tests/Hexalith.Folders.UI.E2E.Tests/README.md`).
- Existing test projects unaffected: confirmed by spot build of `Hexalith.Folders.Testing.Tests` reporting "Build succeeded." with no regressions.
- No production code modified: confirmed.
- No tests written against unstable contracts: confirmed — only the deliberately-skipped placeholder exists.

#### Open Follow-Ups (Not Applied)

- When Epic 6 story 6-2 lands the operations console host: add `OperationsConsoleHostFixture` (likely backed by `DistributedApplicationTestingBuilder` from `Aspire.Hosting.Testing`), an accessibility scanner package, and the first real route smoke. Replace the placeholder test rather than adding new ones beside it.
- Consider wiring the UI E2E lane into a separate, non-blocking CI job when the first real test ships. Trace + screenshot + video on failure only; do not retry above the Playwright default to keep flake visible.
- If team wants `tests/Directory.Build.props` matching Hexalith.Tenants / Hexalith.EventStore convention (suppressing CA2007, xUnit1051 across all test projects), surface as a separate, opinionated change — out of scope here.

#### Next Workflows (Recommended)

- `TD` — Test Design on the in-flight effective-permissions surface (story 2-5 area) or the upcoming story 2-7 lifecycle/binding scope, where the risk score is meaningfully higher than UI scaffolding.
- `RV` — Review Tests on `tests/Hexalith.Folders.Tests/Authorization/*` before story 2-7 merges, to lock in isolation and DoD discipline on a high-risk area.
- `CI` — wire the existing focused governance gate scripts plus the UI E2E lane (non-blocking, deferred-active) into the pipeline shape.

### 2026-05-19 Validation Refresh

### Checklist Validation

Checklist applied with backend-stack lens (browser-framework-only items marked N/A; Pact lane N/A because `tea_use_pactjs_utils = false`).

- Preflight success: passed.
- Directory structure: passed for backend .NET layout. `tests/Hexalith.Folders.Tests/{Aggregates,Authorization,Projections}` mirrors `src/Hexalith.Folders/` one-to-one; `tests/fixtures/`, `tests/tools/`, and per-area test projects all in place. Browser-specific `tests/e2e/` and `tests/support/` items remain N/A — no UI smoke framework yet.
- Config correctness: passed. `.csproj` test projects and `Directory.Packages.props` pin xUnit v3 (3.2.2), Shouldly (4.3.0), NSubstitute (5.3.0), Testcontainers (4.10.0), `Microsoft.AspNetCore.Mvc.Testing` (10.0.5), `Microsoft.NET.Test.Sdk` (18.5.1), `Aspire.Hosting.Testing` (13.2.1), `xunit.runner.visualstudio` (3.1.5), `coverlet.collector` (10.0.1), `YamlDotNet` (17.1.0). No version drift in project files.
- Environment configuration: passed via `.env.example` (`TEST_ENV`, `BASE_URL`, `API_URL`) and `global.json` for SDK pin. `.nvmrc` remains N/A — no Node runtime in scope.
- Fixtures/factories/helpers: passed. `src/Hexalith.Folders.Testing/Factories/*` (7 factories with override records), `Http/TestRequestHeaders.cs`, `Polling/Eventually.cs`, and `FoldersTestingModule.cs` all present and exercised by `Hexalith.Folders.Testing.Tests`.
- Sample tests: passed. `tests/Hexalith.Folders.Testing.Tests/{Unit,Api,Integration}/` exercises every shipped helper; `tests/Hexalith.Folders.Tests/{Aggregates,Authorization,Projections,FoldersModuleSmokeTests}` exercises the core production project.
- Helper utilities: passed via the testing module + request header + polling helpers. Browser/network helpers remain N/A.
- Docs and scripts: passed. `tests/README.md` refreshed for `-Mode Testing` and the third governance gate script. `tests/run-tests.ps1` exposes `All`, `Coverage`, `Integration`, `Testing` modes.
- Build/test script updates: passed for .NET. `dotnet test` is the idiomatic command; `tests/run-tests.ps1` wraps it for the documented modes. No `package.json` scripts required.
- Configuration validation: build succeeds (0 warnings, 0 errors); no placeholder/TODO text introduced in this refresh.
- Test execution validation: sample tests run successfully (see Verification Commands below); xUnit reports cleanly.
- Directory structure validation: passed.
- File integrity validation: passed — no placeholder text, no hardcoded credentials, paths Windows-correct.
- Code quality: generated/updated content follows project file-scoped namespace, nullable, async, and naming conventions.
- Best practices compliance: factory override pattern, explicit polling intervals, no hardcoded waits, helpers framework-agnostic, test-support classes centralize seeded state.
- Knowledge base alignment: matches `data-factories.md`, `fixture-architecture.md`, `recurse.md` / `timing-debugging.md`, `test-quality.md`, `test-levels-framework.md`.
- Pact consumer CDC alignment section: N/A in full — `tea_use_pactjs_utils = false`. Contract testing for Hexalith.Folders flows through OpenAPI 3.1 parity, `tests/fixtures/parity-contract.yaml`, and provider-pinned hermetic fixtures.
- Security checks: passed. No credentials added to `tests/README.md`; `.env.example` retains placeholders only.
- Status file integration: `_bmad-output/test-artifacts/framework-setup-progress.md` updated with 2026-05-19 sections for every step.
- Knowledge base integration: relevant fragments from `resources/tea-index.csv` identified and applied (test-levels-framework, test-priorities-matrix, risk-governance, fixture-architecture, data-factories, recurse, timing-debugging, test-quality).
- Workflow dependencies: ready for downstream `ci`, `test-design`, `atdd`, `automate`, `nfr`, `trace`, and `review-tests` workflows.

### Verification Commands

- `dotnet build Hexalith.Folders.slnx` — passed (0 warnings, 0 errors, ~16s).
- `dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-build` — passed: 43/43 tests (123 ms).
- `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-build` — passed: 246/246 tests (93 ms).
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-tests.ps1 -Mode Testing` — passed: 43/43 tests (102 ms). Helper script and freshly-documented mode line both validated.

### Completion Summary

- Framework re-confirmed: xUnit v3 (3.2.2) as the primary blocking framework.
- Supporting tools confirmed: Shouldly, NSubstitute, Testcontainers, `Microsoft.AspNetCore.Mvc.Testing`, `Aspire.Hosting.Testing`, `YamlDotNet`, and `coverlet.collector`.
- UI adjunct: Playwright deferred until the operations console has stable route/selector contracts.
- Artifacts refreshed this run:
  - `_bmad-output/test-artifacts/framework-setup-progress.md` — appended 2026-05-19 sections for all five steps and bumped frontmatter `lastStep` / `lastSaved`.
  - `tests/README.md` — added `Testing` mode line to the helper-script snippet; added an invocation block for `run-contract-spine-gates.ps1` beside the existing gate-script blocks.
- No source-test scaffolding files created or removed in this refresh; the existing scaffold already absorbs the in-flight 2-5 effective-permissions surface and the new core unit-test project.
- Knowledge fragments applied: fixture architecture, data factories, test levels, test priorities, risk governance, polling/recurse, timing debugging, test quality.
- Official docs cross-checks reaffirmed: Microsoft Learn ASP.NET Core integration testing with `WebApplicationFactory<TEntryPoint>`, xUnit v3 fixture/async guidance, Playwright trace/artifact configuration (deferred application).

### Open Follow-Ups (Not Applied)

- Optional: add a `Core` mode to `tests/run-tests.ps1` targeting `tests/Hexalith.Folders.Tests/` for faster inner-loop iteration on core domain/authorization tests. Not blocking; surfaced for Jerome's decision before applying.
- Optional: a fuller verification pass that runs `dotnet test Hexalith.Folders.slnx` (includes integration tests requiring Docker/Testcontainers and Aspire) is appropriate to wire into CI but was scoped out of this in-session refresh to keep the run deterministic without sidecars.

### Next Workflows (Recommended)

- `TD` — Test Design risk assessment, particularly to score the new effective-permissions surface (principal kinds × lifecycle states × evidence sources) before more code lands.
- `RV` — Review Tests on the freshly-landed `tests/Hexalith.Folders.Tests/Authorization/*` suite to lock in isolation, flakiness, and DoD quality.
- `CI` — wire the focused governance scripts (`run-safety-invariant-gates.ps1`, `run-governance-completeness-gates.ps1`, `run-contract-spine-gates.ps1`) into the blocking CI lane.
- `TR` — start a traceability matrix for story 2-5 requirements against the new `EffectivePermissions*Tests` files before merging.

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
