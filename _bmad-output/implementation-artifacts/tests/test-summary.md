# Test Automation Summary — Story 6.3 (Operator disposition labels)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-05-28 · **Engineer:** QA automation (Jerome) — tests only, no code review/story validation
**Framework:** xUnit v3 + Shouldly + Microsoft.Playwright in `tests/Hexalith.Folders.UI.E2E.Tests`
**Generated tests:** browser-level Playwright coverage for the Story 6.3 state-label gallery.
**Validation status:** blocked by local SDK/WSL environment; see "Validation" below.

## Generated Tests

### API Tests
- [x] N/A — Story 6.3 adds UI renderers and a local enum-fed development gallery; it does not add API endpoints or SDK calls.

### E2E Tests
- [x] `tests/Hexalith.Folders.UI.E2E.Tests/StateLabels/StateLabelGalleryE2ETests.cs` — verifies `/dev/state-label-gallery` loads, exposes the page-root selector, renders one `technical-state-metadata` and one `operator-disposition-badge` per lifecycle row, exercises the `Ready` projection-lag branch, and remains read-only.
- [x] `tests/Hexalith.Folders.UI.E2E.Tests/Routes/ConsoleRoutes.cs` — adds the route constant required by the e2e route contract.

## Coverage
- API endpoints: N/A
- UI features: 1/1 Story 6.3 browser surface covered (`StateLabelGallery`)
- Happy path: gallery loads and exposes reusable status selectors.
- Critical negative cases: read-only/no mutation affordances; non-development test host does not advertise the dev gallery from Home.

## Validation

- [x] Existing framework detected: Playwright-on-.NET UI E2E lane.
- [x] Tests use standard project APIs: xUnit v3, Shouldly, Microsoft.Playwright locators.
- [x] Tests use stable `data-testid` / `data-fc-*` selectors; no CSS class selectors, sleeps, or order dependency.
- [ ] Tests run successfully — blocked locally.

Attempted commands:

```text
/mnt/c/Program Files/dotnet/dotnet.exe --version
```

Blocked by WSL interop before the SDK could start:

```text
WSL (2 - ) ERROR: UtilBindVsockAnyPort:307: socket failed 1
```

Fallback attempt with native Linux SDK from `/tmp`:

```text
DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet build /mnt/d/Hexalith.Folders/tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj --no-restore --verbosity minimal
```

Blocked before compilation because the existing restore assets were generated on Windows and include a missing Windows fallback package folder:

```text
MSB4018: The "ResolvePackageAssets" task failed unexpectedly.
NuGet.Packaging.Core.PackagingException: Unable to find fallback package folder 'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'.
```

## Checklist (`bmad-qa-generate-e2e-tests/checklist.md`)

- [x] API tests generated if applicable (N/A documented)
- [x] E2E tests generated for UI
- [x] Tests use standard test framework APIs
- [x] Tests cover happy path
- [x] Tests cover critical negative cases
- [ ] All generated tests run successfully (blocked by SDK/restore environment)
- [x] Tests use semantic/stable locators
- [x] Tests have clear descriptions
- [x] No hardcoded waits or sleeps
- [x] Tests are independent
- [x] Test summary created
- [x] Tests saved to appropriate directories
- [x] Summary includes coverage metrics

---

# Test Automation Summary — Story 5.4 (Consume parity oracle in CLI and MCP tests)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-05-28 · **Engineer:** QA automation (Jerome) — tests only, no code review/story validation
**Framework:** xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `5.3.0` + YamlDotNet `18.0.0` (project's existing stack; central package management)
**Test command:** `dotnet.exe test tests/Hexalith.Folders.{Cli,Mcp}.Tests` (Windows SDK 10.0.300 per `global.json`, run from WSL)
**Result:** ✅ CLI **691 passed**, MCP **646 passed** — 0 failed, 0 skipped. **Build:** both projects 0 warnings / 0 errors (warnings-as-errors).

> **No E2E/UI lane applies.** Story 5.4 is a **test-only** conformance story: it makes the CLI and MCP test
> suites *consume* the committed behavioral-parity oracle (`tests/fixtures/parity-contract.yaml`) so adapter
> projections + pre-SDK/sourcing behavior are proven against the contract's own columns, with drift failing the
> build. "E2E" here = oracle-driven behavioral conformance over a fake `IClient`/`HttpMessageHandler`. Hermetic:
> no live server, Dapr, Keycloak, Redis, network, credentials, or nested submodule init.

---

## Validation outcome

All 9 decomposed acceptance criteria are exercised by the implemented suite and the workflow checklist passes.
The implementation (4 tasks, shared linked oracle reader + CLI/MCP conformance suites + retained restatements)
was verified end-to-end: full builds 0/0, CLI 691/0, MCP 646/0.

### Coverage vs. acceptance criteria

| AC | Dimension | Status |
| --- | --- | --- |
| #1 | Tests read the oracle directly (no forked copy) | ✅ shared `tests/shared/Parity/ParityOracle.cs` read in place |
| #2 | CLI post-SDK exit-code conformance, every `outcome_mapping` entry | ✅ `[Theory]` over all 529 entries + success→0 |
| #3 | MCP post-SDK failure-kind conformance, every entry | ✅ `[Theory]` over all 529 + kind==category invariant + `none` marker |
| #4 | Pre-SDK sourcing (idempotency/task-id/credential), both adapters, no HTTP call | ✅ CLI repr. commands + MCP 47-tool reflection + repr. tool; **+ gap applied (see below)** |
| #5 | Correlation sourcing (echo + fresh ULID), oracle-gated | ✅ both adapters |
| #6 | Completeness & drift guards (rows, categories both directions, vocabulary) | ✅ 47 rows, 43 categories, canonical exit-code set, EnumMember vocabulary |
| #7 | Shared scenarios reused across CLI and MCP (one loader) | ✅ `tests/shared/Parity/*` linked into both projects |
| #8 | Independent restatements preserved | ✅ `ErrorProjectionTests`/`FailureKindProjectionTests` kept + cross-referenced |
| #9 | Hermetic, additive, test-only (only the 2 csproj + new tests) | ✅ verified via `git diff` — only YamlDotNet + linked compile added |

## Gap discovered & auto-applied

| Gap | AC | Action |
| --- | -- | ------ |
| `EveryToolDeclaresIdempotencyKeyIffItsOracleRowIsMutating` asserted only the **presence** of the `idempotencyKey` parameter, while AC #4 requires mutating MCP tools to declare a **required** idempotencyKey input. A future default value would make the key optional in the MCP schema (allowing an absent / MCP-generated key) yet still pass the old check. | #4 | Added `idempotencyKeyParameter.HasDefaultValue.ShouldBeFalse(...)` for mutating rows in `tests/Hexalith.Folders.Mcp.Tests/ParityOracleConformanceTests.cs`. Test-only (AC #9-compliant); passes against current code — all 14 mutating tools declare `string idempotencyKey` with no default. MCP suite re-run: 646/0. |

No other coverage gaps found. The CLI representative-command map vs. MCP per-tool oracle binding asymmetry is
AC-sanctioned (CLI command names are not kebab-case of `operation_id`); credential/correlation are shared-pipeline
behaviors adequately covered by representative tests.

## Checklist (`bmad-qa-generate-e2e-tests/checklist.md`)
- [x] API/behavioral-conformance tests generated; E2E/UI lane N/A (documented above)
- [x] Standard framework APIs (xUnit v3 / Shouldly / NSubstitute / YamlDotNet)
- [x] Happy path (success marker → 0 / `none`) + critical error cases (every category projection; pre-SDK `64`/`65`/`usage_error`/`credential_missing`)
- [x] All tests pass (CLI 691/691, MCP 646/646)
- [x] Semantic locators (operation ids, tool names via attributes, typed enum EnumMember values, wire headers)
- [x] Clear descriptions; no hardcoded waits/sleeps; tests independent; hermetic (read committed fixture, no network)
- [x] Summary created; tests under `tests/shared/Parity` + the two adapter test projects

## Next steps
- Run in CI via the existing focused gate lanes (no new gate script needed; both adapter test projects already participate).
- Story 5.5 — golden lifecycle parity across REST and SDK. Story 5.6 — a single test exercising CLI **and** MCP projections together (cross-adapter equality; deferred because the projections are `internal` to their own assemblies). Story 5.7 — mixed-surface handoff.

---
---

# (Previous run) Test Automation Summary — Story 5.3 (MCP tools, resources, failure kinds)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-05-27 · **Engineer:** QA automation (Jerome) — tests only, no code review/story validation
**Framework:** xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `5.3.0` (project's existing stack)
**Test command:** `dotnet test tests/Hexalith.Folders.Mcp.Tests` (Windows SDK 10.0.300 per `global.json`)
**Result:** ✅ **104 passed, 0 failed, 0 skipped** (baseline was 79; **+25** new test cases)
**Build:** `Hexalith.Folders.Mcp` — 0 warnings / 0 errors (warnings-as-errors)

> **No E2E/UI lane applies.** `Hexalith.Folders.Mcp` is a stdio JSON-RPC protocol adapter with no UI, so "E2E"
> here means tool/resource-over-`IClient` **behavioral-parity** tests that invoke tool methods directly
> against a fake `IClient` (NSubstitute) or the real generated client over a fake `HttpMessageHandler`. No
> live server, Dapr, Keycloak, Redis, network, or submodule init. Full JSON-RPC round-trip is out of scope per
> the story; oracle-driven consumption is Story 5.4.

---

## Coverage gaps found and auto-filled

The existing 79 tests were mapped to the 11 acceptance criteria; seven dimensions were under-tested. 25 tests
were added (7 new files + 2 extensions). No production defect was found — the implementation was already
behaviorally complete; these tests close coverage holes.

### New files
- `BearerTokenWireTests.cs` — **AC #7** credential→wire (was: resolver precedence only). Resolved token attaches
  as `Authorization: Bearer …`; caller idempotency key reaches the wire unchanged; missing token short-circuits
  (`credential_missing`) so **no request** reaches the handler. (3)
- `ResourceTests.cs` — **AC #3** the two read-only resources (was: discovery/count only). `audit-trail` wraps
  `ListAuditTrail` and echoes a fresh ULID; `folder-tree` is task-scoped (fail-closed `usage_error` without
  `taskId`, threads `taskId` to the wire when supplied); resource credential short-circuit; resources declare
  **no** `idempotencyKey` field. (5)
- `MetadataOnlyJsonTests.cs` — **AC #8** centralized serializer drops `contentBytes`/`inlineContent`/
  `streamDescriptor` at **any nesting depth** while keeping benign metadata; camelCase wire shape. (was:
  top-level `contentBytes` via one wire result only.) (2)
- `SuccessEnvelopeTests.cs` — **AC #8** `AcceptedCommand` surfaces `idempotentReplay`/`status`/`taskId`
  truthfully (replay not hidden); call correlation echoed at envelope top level. (1)
- `ToolMappingTests.cs` — **AC #2** every tool name is kebab-case and resolves 1:1 to an `IClient` operation;
  names unique; `add-file`/`change-file` kept distinct (never collapsed to `write-file`). (3)
- `DependencyDirectionTests.cs` — **AC #1** MCP assembly references `Hexalith.Folders.Client` but never
  Server/Workers/EventStore/Dapr (MCP → Client → Contracts). (2)
- `ToolInputsTests.cs` — **AC #5/query** `ParseFreshness` maps the 3 canonical tokens and unknown/blank → null;
  supplied freshness threads to `X-Hexalith-Freshness`, omitted sends no header. (8)

### Extended files
- `PreSdkFailureTests.cs` — **AC #10** `usage_error` carries the full canonical field set
  (`code=client_configuration_error`, `retryable=false`, `clientAction=revise_request`). (+1)
- `TestSupport.cs` (support) — added `BearerClient(...)` (production `BearerTokenHandler` in the HTTP pipeline)
  and capture of the `X-Hexalith-Freshness` / `Idempotency-Key` request headers.

---

## Coverage vs. acceptance criteria

| AC | Dimension | Status before | Status now |
| --- | --- | --- | --- |
| #1 | Thin adapter / dependency direction | none | **MCP → Client only** (new) |
| #2 | 47 tools, 1:1 kebab↔operation, add/change distinct | count only | + deterministic mapping (new) |
| #3 | Read-only resources, no new query semantics | discovery only | + behavioral invocation (new) |
| #4 | Idempotency-key sourcing | covered | covered |
| #5 | Correlation default + override echo (result + wire) | covered | covered |
| #5 | Query freshness → wire | none | + `ParseFreshness` + wire header (new) |
| #6 | Task-id fail-closed → `usage_error` | covered | covered |
| #7 | Credential precedence + missing→`credential_missing` | covered | covered |
| #7 | Bearer token reaches the wire | none | + `Authorization: Bearer` (new) |
| #8 | Metadata-only output | top-level only | + depth + replay truthfulness (new) |
| #9 | Pre/post-SDK mapping mutually exclusive | covered | covered |
| #10 | Full canonical fields on every failure kind | post-SDK/credential | + `usage_error` fields (new) |
| #11 | Hermetic build/test | covered | covered |

- Failure-kind projection: all 47 `CanonicalErrorCategory` members + all client actions (pre-existing theory).
- Tools: 47 registered; representative behavioral path per group exercised. Resources: 2/2 invoked.

---

## Checklist (./checklist.md)
- [x] API (tool-over-`IClient`) tests generated
- [x] E2E lane: N/A — protocol adapter, no UI (documented above)
- [x] Standard framework APIs (xUnit v3 / Shouldly / NSubstitute)
- [x] Happy path covered (success envelope, resource read, bearer attach, golden-path tools)
- [x] Critical error cases (`usage_error`, `credential_missing`, `internal_error`, `input_limit_exceeded`, post-SDK projection)
- [x] All tests pass (104/104)
- [x] Semantic "locators" (tool/operation names, typed SDK shapes, wire headers)
- [x] Clear descriptions; no hardcoded waits/sleeps; tests independent
- [x] Summary created; tests under `tests/Hexalith.Folders.Mcp.Tests`

## Next steps
- Story 5.4 wires `*.Mcp.Tests` to **consume** `tests/fixtures/parity-contract.yaml` as theory data — the
  encoded mapping and these behavioral tests give it a green target to assert against.
- Stories 5.5–5.7 run golden/behavioral/mixed-surface parity validation.

---
---

# (Previous run) Test Automation Summary — Story 5.2 (CLI commands with behavioral-parity rules)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-05-27 · **Engineer:** QA automation (Jerome)
**Framework:** xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `5.3.0` (project's existing stack)
**Test command:** `dotnet test tests/Hexalith.Folders.Cli.Tests` (Windows SDK 10.0.300 per `global.json`)
**Result:** ✅ **102 passed, 0 failed, 0 skipped** (baseline was 78; **+24** new test cases)
**Build:** `Hexalith.Folders.Cli` — 0 warnings / 0 errors (warnings-as-errors)

The CLI is the "API" under test; all tests are hermetic and drive `rootCommand.Parse(args).InvokeAsync()`
against a fake `IClient` (NSubstitute) or the real generated client over a fake `HttpMessageHandler`. No
server, Dapr, Keycloak, Redis, network, or submodule init.

---

## 🔴 Critical defect found and fixed

**The `commit` command group crashed at parse time for every invocation.** The group (named `commit`) declared
a subcommand also named `commit` (`commit commit` → `CommitWorkspaceAsync`). System.CommandLine 2.0.8's
tokenizer builds a name→token dictionary and threw `ArgumentException: An item with the same key has already
been added. Key: commit` — inside the **production** `CliApplication.RunAsync` parse path. So `folders commit`
+ *any* subcommand (`create`/`evidence`/`provider-outcome`/`reconciliation-status`/`task-status`) crashed the
process for real users. The existing 78 tests never exercised the `commit` group, so it shipped to `review`
undetected.

**Fix applied** (approved by user): renamed the colliding subcommand `commit commit` → **`commit create`** in
`src/Hexalith.Folders.Cli/Commands/Commit/CommitCommand.cs`. Usage is now `folders commit create …`.
Covered by `CommitGroupSubcommandParsesAndWrapsTheSdk` (regression) + the golden-path and reachability tests.

> **Follow-up for dev/architect:** the Command-to-SDK map in the story (`commit | commit → CommitWorkspaceAsync`)
> now reads `commit | create → CommitWorkspaceAsync`. Update the story §Command-to-SDK map and any user docs.

---

## Generated / extended tests

### New files
- `CommandSurfaceE2ETests.cs` — seven-group reachability theory (one reachable command per
  `provider/folder/workspace/file/commit/context/audit` group → exit 0 + SDK called); the **golden lifecycle
  path** end-to-end (configure binding → validate readiness → create repo-backed folder → prepare → lock →
  add file → **commit create** → context list → release → audit list), asserting each step succeeds, the
  overridden `--correlation-id` is propagated unchanged on every invocation, and the canonical SDK operations
  are each received once; `file change` upload routing; the `commit` group regression test.
- `CredentialSourcingE2ETests.cs` — token precedence resolved **through the whole CLI** (not just the resolver
  unit): `HEXALITH_TOKEN` env → credentials file → `--token` flag, plus env-wins-over-flag, each asserting the
  resolved token is the one attached to the client and a call is made.

### Extended files
- `BehavioralParityTests.cs` — non-absolute `--base-address` is a pre-SDK usage error (exit 64, no HTTP call)
  on both the mutation and query paths (previously uncovered `TryResolveBaseAddress` branch).
- `ExitCodeWiringTests.cs` — broadened the typed-problem→exit-code wiring theory (added 68/70/74/75 and the
  post-SDK `authentication_failure`→65); added JSON-mode problem rendering asserting the projected (typed-only)
  envelope on stderr with stdout empty.
- `MetadataOnlyOutputTests.cs` — the server's raw RFC 9457 `Response` body is **never** echoed in `human` or
  `json` mode (projected from typed `ProblemDetails` only), while the typed category is still surfaced.
- `TestData.cs` (support) — `ProblemException(rawResponse:)` overload + an `AcceptedCommand` instance factory.

---

## Coverage vs. acceptance criteria

| AC | Dimension | Status before | Status now |
| --- | --- | --- | --- |
| #2 | Seven canonical groups reachable & wired | folder/context/file only | **all 7 groups** + golden path E2E |
| #3 | Idempotency-key sourcing | covered | covered |
| #4 | Correlation default + override echo | single-call | + multi-step propagation across golden path |
| #5 | Task-ID fail-closed → 64 | covered | covered |
| #6 | Credential precedence | resolver **unit** only | + **end-to-end through the CLI** (all 3 layers) |
| #7 | Pre-SDK 64/65 + post-SDK projection | partial | + base-address→64; + raw-response no-leak |
| #8 | Canonical exit-code projection | full map (unit) | + broader pipeline **wiring** (10 categories) |
| #9 | Metadata-only output (both modes) | human + content drop | + JSON-mode problem shape + raw-body no-leak |
| #10 | File upload via SDK convenience | add only | + change + remove |
| #11 | Hermetic | yes | yes (fake IClient / fake handler) |

**Command-surface coverage:** 7/7 groups now have at least one reachability/E2E test (was 3/7).

---

## Checklist (./checklist.md)
- [x] API (CLI command-surface) tests generated
- [x] E2E tests generated (golden lifecycle path)
- [x] Standard framework APIs (xUnit v3 / Shouldly / NSubstitute)
- [x] Happy path covered (golden path + per-group reachability)
- [x] Critical error cases (pre-SDK 64/65, projection, raw-body leak)
- [x] All tests pass (102/102)
- [x] Semantic "locators" (command/option names, typed SDK shapes)
- [x] Clear descriptions; no hardcoded waits/sleeps; tests independent
- [x] Summary created; tests under `tests/Hexalith.Folders.Cli.Tests`

## Next steps
- Update the story's Command-to-SDK map + docs for `commit create` (see follow-up above).
- Story 5.4 will replace this story's hand-encoded exit-code map with oracle-driven theory data from
  `tests/fixtures/parity-contract.yaml` — these wiring tests give it a green target to assert against.
