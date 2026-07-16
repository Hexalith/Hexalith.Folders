# Test Automation Summary — Story 5.2 (CLI commands with behavioral-parity rules)

**Workflow:** `bmad-qa-generate-e2e-tests` · **Date:** 2026-05-27 · **Engineer:** QA automation (Jerome)
**Framework:** xUnit v3 `3.2.2` + Shouldly `4.3.0` + NSubstitute `5.3.0` (project's existing stack)
**Test command:** `dotnet test tests/Hexalith.Folders.Cli.Tests` (Windows SDK 10.0.302 per `global.json`)
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
