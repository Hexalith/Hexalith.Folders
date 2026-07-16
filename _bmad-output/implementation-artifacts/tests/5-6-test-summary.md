# Story 5.6 — QA Test Automation Summary

Workflow: `bmad-qa-generate-e2e-tests` (skill `.claude/skills/bmad-qa-generate-e2e-tests/SKILL.md`)
Story file: `_bmad-output/implementation-artifacts/5-6-validate-behavioral-parity-across-cli-and-mcp.md`
Baseline commit: `7665fbd`
Date: 2026-05-28

## Test framework

xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `5.3.0` + Newtonsoft.Json `13.0.4` (project's existing stack; central package management). Build/test driven via the Windows .NET SDK from WSL (`/mnt/c/Program Files/dotnet/dotnet.exe`) per the `global.json` `10.0.300` pin.

## Story state on entry

Story 5.6 was in `review` with all dev tasks completed and `tests/Hexalith.Folders.IntegrationTests/AdapterParity/CrossAdapterBehavioralParityTests.cs` carrying **553 cross-adapter tests** (529 oracle projection-equivalence rows + reconciliation facts + pre-SDK end-to-end + correlation defaults + post-SDK end-to-end + canonical-name preservation + drift/vocabulary/mutual-exclusion guards). Baseline `dotnet.exe test` ran 553/553 green.

## Gaps discovered against Story 5.6 ACs

QA scan found four cross-adapter parity claims that the existing ACs prove only transitively (both adapters wrap the SDK, so SDK transport-parity implies adapter-pair parity) but never directly assert at the cross-adapter site:

1. **Wire-header symmetry beyond `X-Correlation-Id` (AC #4, #6).** `ExplicitCorrelationIsEchoedByteForByteThroughBothAdapters` proves `X-Correlation-Id` byte-equal across CLI/MCP wire requests. The full canonical mutating-call header set is `(Idempotency-Key, X-Hexalith-Task-Id, X-Correlation-Id)` — the other two were only asserted per-adapter, never cross-adapter.
2. **HTTP method + URI absolute path symmetry (Adapter Parity Contract: "same shape on the wire").** Never directly cross-asserted.
3. **`usage_error` MCP envelope completeness (AC #4 + Adapter Parity Contract canonical Problem-shape).** The `credential_missing` test asserts all 5 canonical pre-SDK fields (`kind/code/retryable/clientAction/correlationId`); the `usage_error` tests checked only `kind + correlationId` — asymmetric coverage on a symmetric pipeline.
4. **Idempotent-replay surfacing symmetry (architecture invariant: "identical idempotency replay across CLI/MCP/SDK/REST").** Not exercised — no test drove `idempotentReplay:true` on a `202 Accepted` through both adapters and asserted truthy surfacing on each.

## Gap tests added (auto-applied)

All four added as new `[Fact]`s in the existing class `tests/Hexalith.Folders.IntegrationTests/AdapterParity/CrossAdapterBehavioralParityTests.cs` (no new file; preserves the story's single-cross-adapter-class architecture):

| # | Test | What it asserts |
|---|---|---|
| 1 | `WireHeadersForMutatingCallEchoCallerInputsByteForByteAcrossBothAdapters` | For the same caller-supplied `(idempotencyKey, taskId, correlationId)` driven through CLI + MCP, the wire `Idempotency-Key`, `X-Hexalith-Task-Id`, and `X-Correlation-Id` headers each (a) equal the caller value and (b) are byte-for-byte equal across the two adapters' captured requests. |
| 2 | `HttpMethodAndPathSymmetryForSameOperationAcrossBothAdapters` | The CLI-observed `HttpRequestMessage.Method` and `RequestUri.AbsolutePath` byte-equal the MCP-observed values for the same operation (`CreateRepositoryBackedFolder`). |
| 3 | `UsageErrorMcpEnvelopeCarriesCanonicalPreSdkProblemFields` | The MCP `usage_error` pre-SDK envelope carries `kind == "usage_error"`, non-empty `code`, `retryable == false`, non-empty `clientAction`, and the caller-supplied `correlationId` echoed — symmetric with the `credential_missing` envelope assertion. |
| 4 | `IdempotentReplaySuccessIsSurfacedConsistentlyOnBothAdapters` | A `202 Accepted` carrying `idempotentReplay:true` surfaces as `"idempotentReplay": true` in CLI `--output json` stdout AND as `result.idempotentReplay == true` in the MCP success envelope. |

All tests are hermetic: CLI driven via `CliTestHarness` over `CapturingHttpHandler`, MCP driven via `ToolPipeline` over `TestSupport.CapturingHandler` / `Substitute.For<IClient>()`. No live server, Dapr, Keycloak, Redis, network, or nested submodule init.

## Results

| Suite | Before | After | Δ |
|---|---|---|---|
| `IntegrationTests` (filter `CrossAdapterBehavioralParityTests`) | 553 / 553 ✅ | **557 / 557 ✅** | **+4** |
| `IntegrationTests` (full) | 580 / 580 ✅ | **584 / 584 ✅** | +4 |
| `Hexalith.Folders.Cli.Tests` | 691 / 691 ✅ | 691 / 691 ✅ | 0 |
| `Hexalith.Folders.Mcp.Tests` | 646 / 646 ✅ | 646 / 646 ✅ | 0 |

0 warnings / 0 errors on build (warnings-as-errors). No regressions in adjacent suites.

## Files touched

- `tests/Hexalith.Folders.IntegrationTests/AdapterParity/CrossAdapterBehavioralParityTests.cs` — added 4 `[Fact]` methods at the end of the class under a "QA gap coverage" section, before the helper methods. No structural change to the rest of the file.

No production-source edits. No new csproj entries, no new packages, no `Hexalith.Folders.slnx` edit, no recursive submodule commands.

## Validation against checklist (`.claude/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- [x] API tests generated (N/A — story is a cross-adapter parity test story; no new API endpoints)
- [x] E2E tests generated (4 cross-adapter behavioral parity tests)
- [x] Tests use standard test framework APIs (xUnit v3, Shouldly, NSubstitute, JObject)
- [x] Tests cover happy path (wire-header echo, HTTP shape, idempotent-replay)
- [x] Tests cover critical error cases (`usage_error` envelope completeness)
- [x] All generated tests run successfully (4/4 green)
- [x] Tests use proper assertions (semantic Shouldly methods; no string-matching shortcuts where typed inspection is available)
- [x] Tests have clear descriptions (descriptive method names + inline rationale comments)
- [x] No hardcoded waits or sleeps
- [x] Tests are independent (each constructs a fresh `CliTestHarness` + `CapturingHandler` / substitute)
- [x] Test summary created
- [x] Tests saved to appropriate directory (`tests/Hexalith.Folders.IntegrationTests/AdapterParity/`)
- [x] Summary includes coverage metrics

## Notes for the reviewer

- The dev-recorded 🟡 casing observation (CLI emits `Folder_acl_denied` PascalCase via `Category.ToString()`; MCP emits canonical snake_case via `[EnumMember]`) is outside scope here (a CLI source edit). The canonical snake_case still surfaces on CLI via the server-supplied `problem.Code` echoed on stderr, so the AC #9 "category appears verbatim" assertion holds. The new tests do not regress that observation.
- The unrelated `Hexalith.Folders.Testing.Tests.ProjectReferencesFollowAllowedDependencyDirection` baseline red (since Story 5.5) is unchanged and out of scope.
