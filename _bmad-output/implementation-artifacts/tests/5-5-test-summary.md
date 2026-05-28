# Story 5.5 — QA Test Automation Summary

Workflow: `bmad-qa-generate-e2e-tests` (skill `.claude/skills/bmad-qa-generate-e2e-tests/SKILL.md`)
Story file: `_bmad-output/implementation-artifacts/5-5-validate-golden-lifecycle-parity-across-rest-and-sdk.md`
Baseline commit: `75e9782`
Date: 2026-05-28

## Test framework

xUnit v3 + Shouldly (project default). NSubstitute available but not required by this story. Build/test driven via the Windows .NET SDK from WSL (`/mnt/c/Program Files/dotnet/dotnet.exe`) per `global.json` `10.0.300` pin.

## Gaps discovered against Story 5.5 ACs

Story 5.5 was in `review` with all dev tasks marked complete. The qa-generate-e2e-tests pass identified two additive gaps where the existing test suite did not exercise an AC end-to-end:

1. **AC #7 per-step transport contract.** `GoldenLifecycleParityTests` exercises only two operations end-to-end (`ArchiveFolder`, `GetFolderLifecycleStatus`) plus one negative. The 9-step canonical flow's per-step transport contract (family → terminal-state class, family ↔ idempotency-rule partition, universal `correlation_field_path`, surface coverage) was asserted only indirectly via the broader SDK/REST conformance suites, not parametrically against the golden-lifecycle step list itself.
2. **AC #4 cross-surface task-id echo.** The existing cross-surface tests assert explicit `X-Correlation-Id` echo on both surfaces but do not assert that the caller-supplied `X-Hexalith-Task-Id` is similarly echoed (header on REST, `AcceptedCommand.TaskId` on SDK).

ULID-on-omission was reviewed and is already covered by the unit-level `CorrelationAndTaskIdTests` in `Hexalith.Folders.Client.Tests`; the propagation-to-wire path is the same as the explicit-correlation path, so no additional integration test was needed.

## Tests added (auto-applied gaps)

### `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`

| Member | Kind | Coverage |
| --- | --- | --- |
| `GoldenLifecycleStepRows` | TheoryData provider | One row per golden-lifecycle step expanded across SDK and REST variants (REST variant added when the step's `RestInspectionOperationId` differs from `SdkOperationId`). |
| `GoldenLifecycleStepCarriesOracleTransportContractForFamily(stepLabel, operationId)` | `[Theory]` | Asserts every step's oracle row honors: terminal-state class per `FamilyToTerminalState` (AC #6), idempotency-rule per family partition (AC #3), `correlation_field_path == headers.X-Correlation-Id` (AC #4), and both `sdk` + `rest` membership in `adapter_expectations` (AC #2/#8). |
| `CrossSurfaceMutatingStepEchoesExplicitTaskIdOnResponse` | `[Fact]` | Drives `ArchiveFolder` via REST + SDK against the same in-process host; asserts REST echoes `X-Hexalith-Task-Id` on the response header and SDK surfaces it on `AcceptedCommand.TaskId` body field (AC #4). |

Net additions: 9 parametric rows (one per `GoldenLifecycle.Steps` entry; current step list has no REST/SDK divergence, so each step contributes one row) + 1 fact = **+10 tests**.

## Coverage

- API tests (transport-parity conformance per row + endpoint enumeration): pre-existing in `Hexalith.Folders.Client.Tests/TransportParityConformanceTests.cs` (SDK) and `Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs` (REST).
- E2E tests (dual-surface against one in-process host): `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` — pre-existing 4 tests + 10 new = 14 tests.
- Oracle rows covered: 47 / 47 (oracle is the source of truth; coverage guards in `TransportParityConformanceTests` already pin this both directions).
- Implemented REST endpoints: 28 / 47 oracle rows (19 documented as known REST surface gap in `Server.Tests.TransportParityConformanceTests.KnownRestSurfaceGap`; per story negative-scope directive, drift is surfaced in tests rather than papered over).

## Test run

Windows .NET SDK 10.0.300 via WSL:

```text
dotnet.exe build Hexalith.Folders.slnx --nologo
  → 0 Warning(s) / 0 Error(s)

dotnet.exe test tests/Hexalith.Folders.IntegrationTests/...
  → Passed! 26 / 26 (was 16, +10 new)
dotnet.exe test tests/Hexalith.Folders.Server.Tests/... --filter TransportParityConformanceTests
  → Passed! 8 / 8
dotnet.exe test tests/Hexalith.Folders.Cli.Tests/...
  → Passed! 691 / 691 (regression check: shared reader extension intact)
dotnet.exe test tests/Hexalith.Folders.Mcp.Tests/...
  → Passed! 646 / 646 (regression check: shared reader extension intact)
```

## Checklist validation (`.claude/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- [x] API tests generated (pre-existing `TransportParityConformanceTests` on SDK and REST).
- [x] E2E tests generated (`GoldenLifecycleParityTests` — pre-existing + new per-step contract + task-id echo).
- [x] Tests use standard test framework APIs (xUnit v3 `[Fact]` / `[Theory]` + `MemberData`, Shouldly).
- [x] Tests cover happy path (mutating accepted/202, query projected/200, task-id echo).
- [x] Tests cover critical error cases (authentication failure, idempotency-key-on-query rejection, cross-surface RFC 9457 shape).
- [x] All generated tests run successfully.
- [x] Tests use proper locators (oracle row lookups by `operation_id`, semantic HTTP status / header / response-body field access).
- [x] Tests have clear descriptions (XML doc comments tie each assertion back to AC numbers).
- [x] No hardcoded waits or sleeps.
- [x] Tests are independent (each `TestHost.StartAsync` instance is hermetic per-test; no order dependency).

## Hermeticity (AC #10)

The new tests are additive and test-only:

- No edits under any `Generated/`.
- No edits to `src/Hexalith.Folders.Server`, `src/Hexalith.Folders.Client`, the SDK convenience helpers, the Contract Spine `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, the oracle `tests/fixtures/parity-contract.yaml`, or the generator.
- No new package references; no inline package `Version` attributes.
- `Hexalith.Folders.slnx` unchanged.
- Tests bind to `127.0.0.1:0` (in-process host), use in-memory repository/read-models, the `InProcessEventStoreGatewayClient` round-trip, and `MutableTenantAndClaimContext`. No Dapr/Keycloak/Redis sidecars, no provider credentials, no network, no nested submodule init.

## Next steps

- Re-run `_bmad-output/implementation-artifacts/5-5-validate-golden-lifecycle-parity-across-rest-and-sdk.md` review with the new tests in scope.
- When an audit-family `/api/v1` endpoint lands, the golden-lifecycle `audit_inspection` step's `RestInspectionOperationId` substitution can be removed; the parametric per-step theory will automatically pick up the new SDK/REST divergence row without further edits.
- The pre-existing `BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch` failure (recorded in the story Dev Notes as unrelated to 5.5) remains; not in scope for this story.
