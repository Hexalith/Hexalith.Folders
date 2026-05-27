---
baseline_commit: 73628985c0c906d3d5dd2e00273ff0ba70d12455
---

# Story 5.4: Consume parity oracle in CLI and MCP tests

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want CLI and MCP tests to consume behavioral-parity oracle columns,
so that adapter behavior cannot drift from the canonical contract.

## Acceptance Criteria

Source epic AC (epics.md#Story-5.4):

> **Given** `parity-contract.yaml` exists
> **When** CLI and MCP tests run
> **Then** behavioral-parity columns drive assertions for pre-SDK errors, key sourcing, correlation sourcing, exit codes, and failure kinds
> **And** shared conformance scenarios are reused across CLI and MCP where behavior should match
> **And** missing rows or unsupported categories fail tests.

Decomposed, testable acceptance criteria:

1. **Tests read the oracle directly — no second copy of the mapping.** CLI and MCP tests load `tests/fixtures/parity-contract.yaml` at test time and assert the adapter projections against the values **in that file**. The oracle row values are the source of truth; the test must not hard-code a parallel expected table that could drift from the oracle (the hand-maintained `[InlineData]` tables in the existing `ErrorProjectionTests`/`FailureKindProjectionTests` are an *independent restatement* kept as defense-in-depth — see AC #8 — but the new assertions in this story are **data-driven from the oracle file**). Do **not** regenerate, hand-edit, or fork the oracle; read the committed `tests/fixtures/parity-contract.yaml` in place.

2. **Post-SDK exit-code conformance (CLI), oracle-driven.** For **every** `outcome_mapping` entry across all 47 operation rows, a CLI test asserts `Hexalith.Folders.Cli.Errors.ErrorProjection.Project(<canonical_error_category>)` equals that row's `cli_exit_code`. The category→code projection must agree with the oracle for **every** category the oracle lists, with **no** category collapsed (project-context Critical Don't-Miss rule). The success marker (`behavioral_parity.cli_exit_code: 0` / `canonical_error_category: success`) maps to `FoldersExitCodes.Success` (0).

3. **Post-SDK failure-kind conformance (MCP), oracle-driven.** For **every** `outcome_mapping` entry across all 47 rows, an MCP test asserts `Hexalith.Folders.Mcp.Errors.FailureKindProjection.Project(<canonical_error_category>)` equals that row's `mcp_failure_kind` (kind == canonical category name verbatim). The success marker (`mcp_failure_kind: none`/`success`) is handled explicitly and is **not** a failure kind.

4. **Pre-SDK error-class conformance, oracle-driven.** The oracle `behavioral_parity` (and per-row `outcome_mapping[].pre_sdk_error_class`) sourcing columns drive the pre-SDK assertions on **both** adapters:
   - `idempotency_key_sourcing` distinguishes mutating (`caller_provided`) from non-mutating (`not_accepted`) operations. From that column the tests derive the mutating/query partition (instead of a hand-maintained name list) and assert: **MCP** mutating tools declare a required `idempotencyKey` input and query tools declare none; **CLI** mutating commands fail-closed with exit `64` when `--idempotency-key`/`--allow-auto-key` is absent and query commands reject `--idempotency-key` (exit `64`).
   - `task_id_sourcing` (`caller_provided` vs `not_task_scoped`) drives the "missing `taskId`/`--task-id` on a task-scoped operation → pre-SDK usage error" assertions.
   - `credential_sourcing` drives the "no token resolved → CLI exit `65` / MCP `credential_missing`, no HTTP call" assertions.
   - Every pre-SDK assertion proves **no HTTP call is made** (CLI: `ClientFactoryInvoked == false`; MCP: fake handler/`IClient` received zero calls).

5. **Correlation-sourcing conformance, oracle-driven.** Using the oracle `behavioral_parity.correlation_id_sourcing` / `transport_parity.correlation_field_path` columns, the tests assert: an explicit correlation ID is echoed **unchanged** to the wire `X-Correlation-Id` header (and into the result for MCP / to stderr for CLI); when omitted, a fresh 26-char ULID is generated, propagated to the wire, and surfaced. (Mechanics already exist from Stories 5.2/5.3; here the *expectation* is sourced from the oracle column, and the assertion fails if the oracle column changes from a caller-provided/echo contract.)

6. **Completeness & drift guards — "missing rows or unsupported categories fail tests."** The tests fail loudly on oracle/adapter drift:
   - **Row completeness:** assert the oracle contains exactly the expected operation set (47 rows, distinct `operation_id`s); a missing or duplicate row fails. (The Contract-Spine governance gate already proves oracle↔OpenAPI completeness; here re-assert row presence is non-empty and unique so the adapter tests can't silently run against a truncated oracle.)
   - **Category coverage both directions:** every distinct `canonical_error_category` appearing in any oracle `outcome_mapping` MUST be handled by the adapter projection (no fall-through to the catch-all); and every `CanonicalErrorCategory` enum member that the oracle does **not** list MUST be the documented exception (`range_unsatisfiable`, enum 43 → CLI `1` / MCP `internal_error`). An unmapped category that is **not** the documented exception fails the test (this is the "unsupported categories fail tests" guard).
   - **Enum vocabulary guard:** assert every `mcp_failure_kind` string in the oracle is a real `CanonicalErrorCategory` `EnumMember` value (or the `none`/`success` marker), and every `cli_exit_code` integer is in the canonical set `{0,1,64,65,66,67,68,69,70,71,72,73,74,75}`. A value outside the vocabulary fails.

7. **Shared conformance scenarios reused across CLI and MCP.** The oracle reader and the derived scenario/`TheoryData` providers live in a **single shared source** consumed by both `Hexalith.Folders.Cli.Tests` and `Hexalith.Folders.Mcp.Tests` (do not fork the loader or the row data into two divergent copies — project-context fixture/DRY rule). Each adapter test project supplies its own adapter-specific assertion (CLI asserts `cli_exit_code`; MCP asserts `mcp_failure_kind`) over the **same** shared oracle rows, so the two surfaces are provably driven by one contract. (A *single test exercising both adapters together* is Story 5.6, not this story — adapter projections are `internal` to their own assemblies and cannot be co-referenced here.)

8. **Independent restatements preserved as defense-in-depth.** The existing `ErrorProjectionTests` (hand-typed `[InlineData]` exit-code table) and `FailureKindProjectionTests` (enum-`EnumMember`-derived) remain — they cross-check the projection from a source **other** than the oracle, so an erroneous oracle edit is caught by them while an erroneous projection edit is caught by the new oracle-driven tests. Do not delete them. If a new oracle-driven test makes one strictly identical, keep the independent one and note the relationship in a comment rather than duplicating intent.

9. **Hermetic, additive, no production-code or contract change.** This is a **test-only** story. No change to `src/Hexalith.Folders.Cli`, `src/Hexalith.Folders.Mcp`, the SDK/`Client`, the Contract Spine, the oracle, the generator, or any generated artifact. The only production-tree edits permitted are the two test `.csproj` files (add the centrally-pinned `YamlDotNet` reference) and new test sources. Tests build and run with **no** provider credentials, **no** Dapr/Keycloak/Redis, **no** network, **no** nested submodule init. If the new tests reveal a real projection bug, surface it in Dev Notes — do **not** "fix" it by editing the oracle.

## Tasks / Subtasks

- [x] **Task 1 — Shared oracle reader + row model (consumed by both test projects)** (AC: #1, #6, #7)
  - [x] Create a shared, test-only source under `tests/shared/Parity/` (a NEW directory — not a new solution project, so `Hexalith.Folders.slnx` is unchanged): `ParityOracle.cs` (loader) and `ParityScenarios.cs` (xUnit `TheoryData` providers). Namespace e.g. `Hexalith.Folders.Parity.Testing`.
  - [x] Reuse the proven loading pattern verbatim from `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs`: `FindRepositoryRoot()` (walk up from `AppContext.BaseDirectory` to the directory containing `Hexalith.Folders.slnx`), then `Path.Combine(root, "tests", "fixtures", "parity-contract.yaml")`; parse with `YamlDotNet.RepresentationModel` (`YamlStream` → `Documents[0].RootNode` as `YamlSequenceNode` → `YamlMappingNode` rows); helper accessors `RequiredScalar` / `RequiredMapping` / `RequiredSequence`. Do not invent a new YAML parsing approach.
  - [x] Expose a typed row view, e.g. `ParityRow(string OperationId, string OperationFamily, string PreSdkErrorClass, string IdempotencyKeySourcing, string CorrelationIdSourcing, string TaskIdSourcing, string CredentialSourcing, int SuccessCliExitCode, string SuccessMcpFailureKind, IReadOnlyList<OutcomeMapping> OutcomeMappings)` and `OutcomeMapping(string CanonicalErrorCategory, int CliExitCode, string McpFailureKind, string PreSdkErrorClass)`.
  - [x] Provide `TheoryData` sources: (a) every `(operation_id, canonical_error_category, cli_exit_code, mcp_failure_kind, pre_sdk_error_class)` outcome tuple flattened across all rows; (b) the per-operation rows; (c) the deduplicated `(canonical_error_category → cli_exit_code)` and `(canonical_error_category → mcp_failure_kind)` maps with a consistency check that the same category never carries two different codes/kinds across rows (it must not — assert it).
  - [x] Link the shared source into **both** test projects via `<Compile Include="..\shared\Parity\ParityOracle.cs"><Link>Parity\ParityOracle.cs</Link></Compile>` (and `ParityScenarios.cs`) in `Hexalith.Folders.Cli.Tests.csproj` and `Hexalith.Folders.Mcp.Tests.csproj`. (Linked compile is csproj-local; no `.slnx` edit.) Add `<PackageReference Include="YamlDotNet" />` (no inline `Version` — central `Directory.Packages.props` pins `18.0.0`) to **both** test csproj.

- [x] **Task 2 — CLI oracle-driven conformance tests** (AC: #2, #4, #5, #6, #8)
  - [x] New `tests/Hexalith.Folders.Cli.Tests/ParityOracleConformanceTests.cs`: `[Theory]` over the flattened outcome tuples asserting `ErrorProjection.Project(ParseCategory(row.canonical_error_category)) == row.cli_exit_code` for every oracle entry; plus the success marker → `0`. Map the oracle's snake_case category string to the `CanonicalErrorCategory` enum via the SDK enum's `EnumMemberAttribute` (mirror `FailureKindProjectionTests.EnumMemberValue`), NOT `Enum.Parse` on PascalCase.
  - [x] Coverage/drift `[Fact]`s (AC #6): every oracle category projects to a non-fallback code (i.e. is explicitly handled); `range_unsatisfiable` (absent from oracle) → `1`; every projected exit code ∈ the canonical set; the dedup map has no category with conflicting codes.
  - [x] Pre-SDK sourcing tests driven by the oracle columns (AC #4): for a representative mutating command (e.g. `folder create-repo-backed`) the oracle's `idempotency_key_sourcing=caller_provided` ⇒ missing key → exit `64`, no call; `task_id_sourcing=caller_provided` ⇒ missing `--task-id` → exit `64`, no call; `credential_sourcing` ⇒ missing token → exit `65`, no call. For a representative query command (e.g. `folder status`) `idempotency_key_sourcing=not_accepted` ⇒ `--idempotency-key` rejected → exit `64`. Reuse `CliTestHarness` (assert `ClientFactoryInvoked == false`). NOTE: CLI command names are **not** kebab-case of `operation_id` (e.g. `create-repo-backed` ≠ `create-repository-backed-folder`), so the CLI pre-SDK sourcing tests use a small explicit `operation_id → CLI argv` map for the representative commands rather than deriving argv from the oracle; the *expectation* (which exit code / whether the key is accepted) is still read from the oracle column.
  - [x] Correlation sourcing (AC #5): explicit `--correlation-id` echoed unchanged to the wire `X-Correlation-Id` + stderr; omitted → fresh 26-char ULID on the wire + stderr. Gate the expectation on the oracle's `correlation_id_sourcing` column.
  - [x] Keep `ErrorProjectionTests.cs` as the independent restatement (AC #8); add a one-line comment cross-referencing the new oracle-driven test.

- [x] **Task 3 — MCP oracle-driven conformance tests** (AC: #3, #4, #5, #6, #7, #8)
  - [x] New `tests/Hexalith.Folders.Mcp.Tests/ParityOracleConformanceTests.cs`: `[Theory]` over the flattened outcome tuples asserting `FailureKindProjection.Project(ParseCategory(row.canonical_error_category)) == row.mcp_failure_kind` for every oracle entry; success marker handled explicitly.
  - [x] Coverage/drift `[Fact]`s (AC #6): every oracle `mcp_failure_kind` is a real `CanonicalErrorCategory` `EnumMember` (or `none`/`success`); every oracle category projects to its verbatim name (no collapse); `range_unsatisfiable` → `internal_error`.
  - [x] Pre-SDK sourcing driven by the oracle (AC #4): derive the mutating set from the oracle `idempotency_key_sourcing` column and assert (via reflection over `[McpServerTool]` methods, mirroring `SourcingTests.MutatingToolsDeclareIdempotencyKeyAndQueryToolsDoNot`) that the tool whose name == **kebab-case(`operation_id`)** declares a required `idempotencyKey` iff the oracle marks it `caller_provided`, and none iff `not_accepted`. For a representative mutating tool, assert missing `taskId`→`usage_error` (no call) where `task_id_sourcing=caller_provided`, missing idempotency key→`usage_error` (no call), and missing credential→`credential_missing` (no call) using `TestSupport.Pipeline`/`CapturingHandler` and asserting `Requests` is empty.
  - [x] Correlation sourcing (AC #5): explicit `correlationId` echoed unchanged in the result + wire header; omitted → fresh ULID echoed + on the wire — gated on the oracle column.
  - [x] Keep `FailureKindProjectionTests.cs` as the independent restatement (AC #8).

- [x] **Task 4 — Verify build + focused tests** (AC: #9)
  - [x] Build with the WSL-accessible Windows SDK (the WSL-native SDK fails the `global.json` `10.0.300` pin — see Dev Notes): `dotnet.exe restore Hexalith.Folders.slnx`; `dotnet.exe build Hexalith.Folders.slnx --no-restore` (0 warnings / 0 errors); then `dotnet.exe test tests/Hexalith.Folders.Cli.Tests` and `dotnet.exe test tests/Hexalith.Folders.Mcp.Tests`.
  - [x] Confirm: no edits under any `Generated/`; no edits to `src/Hexalith.Folders.Cli`/`src/Hexalith.Folders.Mcp` production code, the SDK, the Contract Spine, `tests/fixtures/parity-contract.yaml`, or the generator; no inline package `Version` attributes (only `YamlDotNet` added centrally-pinned to the two test csproj); no recursive submodule commands; `.slnx` unchanged.
  - [x] Sanity-check the new tests actually fail on injected drift (e.g. temporarily flip one expected value locally to confirm the assertion bites), then revert. Record the count of oracle-driven cases (≈ one per `outcome_mapping` entry across 47 rows) in the Dev Agent Record.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** Make the CLI and MCP test suites *consume* the committed parity oracle (`tests/fixtures/parity-contract.yaml`) so adapter projections and pre-SDK/sourcing behavior are proven against the contract's own columns, with drift (missing rows, unsupported categories, out-of-vocabulary values) failing the build. A single shared oracle reader + scenario provider, linked into both test projects. `YamlDotNet` added to the two test csproj.
- **OUT of scope (do NOT implement here):**
  - **Story 5.5** — golden lifecycle parity across REST and SDK.
  - **Story 5.6** — a single test exercising CLI **and** MCP together (cross-adapter equality). The adapter projections are `internal`; co-referencing both from one assembly is a 5.6 concern. Here each adapter project asserts its own column over shared rows.
  - **Story 5.7** — mixed-surface handoff.
  - Any change to CLI/MCP **production** code, the SDK/`Client`, the Contract Spine, the oracle file, the parity-oracle generator, or any generated artifact. This is test-only.
- **Negative scope note for the dev:** if you find yourself editing `tests/fixtures/parity-contract.yaml`, regenerating the oracle, adding a new error category/exit code, touching `ErrorProjection.cs`/`FailureKindProjection.cs`, or making a CLI/MCP source change to satisfy a test — stop. Either the test expectation is wrong, or you've found a real drift bug to report in Dev Notes (not silently paper over).

### Critical guardrail — the oracle is the source of truth, the projection is the thing under test

Stories 5.2 (CLI `ErrorProjection`) and 5.3 (MCP `FailureKindProjection`) **encoded** the oracle's `outcome_mapping` columns verbatim into `switch` maps and unit-tested them against an *independent* restatement (a hand-typed table / the SDK enum's `EnumMember`). Story 5.4 closes the loop: it reads the **actual oracle file** and proves the projection agrees with it, row by row. The win is bidirectional drift detection:
- an erroneous **projection** edit is caught by the new oracle-driven test;
- an erroneous **oracle** edit is caught by the retained independent restatement (AC #8);
- an oracle row that references a category the adapter doesn't handle, or an adapter that handles a category the oracle dropped, is caught by the coverage guards (AC #6).

### Oracle file shape (what you will read) — `tests/fixtures/parity-contract.yaml`

Top of file is comment provenance (`# generated_by:`, `# contract_spine_sha256:`, etc.), then a YAML **sequence** of **47** operation rows. Each row:

```yaml
- operation_id: 'AddFile'
  operation_family: 'mutating_command'        # enum: mutating_command | query_status | context_query | audit | operations_console_projection
  read_consistency_class: 'not_applicable'
  transport_parity:
    auth_outcome_class: 'folder_acl_denied'
    error_code_set: [ ... canonical categories ... ]
    idempotency_key_rule: 'required_with_operation_id'   # | 'required_for_mutating_command' | (queries: a not-required rule)
    audit_metadata_keys: [ ... ]
    correlation_field_path: 'headers.X-Correlation-Id'
    terminal_states: [ 'accepted' ]
  behavioral_parity:                            # <-- the SUCCESS row for this operation
    pre_sdk_error_class: 'none'
    idempotency_key_sourcing: 'caller_provided' # mutating; queries: 'not_accepted'
    correlation_id_sourcing: 'caller_provided'
    task_id_sourcing: 'caller_provided'         # task-scoped; else 'not_task_scoped'
    credential_sourcing: 'sdk_configuration'
    cli_exit_code: 0
    mcp_failure_kind: 'none'
  outcome_mapping:                              # <-- the PER-CATEGORY projections (the meat for AC #2/#3)
    - canonical_error_category: 'folder_acl_denied'
      cli_exit_code: 66
      mcp_failure_kind: 'folder_acl_denied'
      pre_sdk_error_class: 'none'
    - canonical_error_category: 'idempotency_conflict'
      cli_exit_code: 68
      mcp_failure_kind: 'idempotency_conflict'
      pre_sdk_error_class: 'none'
    # ... one entry per category in this operation's error_code_set ...
  adapter_expectations: [ 'cli', 'mcp', 'rest', 'sdk' ]
  ownership: { ... metadata ... }
```

Key facts:
- **`outcome_mapping` is the per-category table** you assert against for AC #2/#3 — `canonical_error_category → cli_exit_code` (CLI) and `→ mcp_failure_kind` (MCP). `pre_sdk_error_class` here is per-category (e.g. `authentication_failure` rows carry `pre_sdk_error_class: 'credential_missing'`).
- **`behavioral_parity` is the success row** (`cli_exit_code: 0`, `mcp_failure_kind: 'none'`) plus the operation-level **sourcing** columns used for AC #4/#5.
- `mcp_failure_kind == canonical_error_category` for every post-SDK row (one-to-one). `none` is the success marker, not a kind.
- The same category appears in many operations' `outcome_mapping`s; it must carry the **same** `cli_exit_code`/`mcp_failure_kind` everywhere — assert that invariant when building the dedup map (AC #6).
- `range_unsatisfiable` (SDK `CanonicalErrorCategory` enum member 43) is **deliberately absent** from the oracle — the documented drift exception: CLI `1`, MCP `internal_error`. Treat it as the only allowed "unmapped" category; any other unmapped category fails.

### Proven oracle-loading pattern (copy this, don't reinvent)

`tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs` already loads the oracle with `YamlDotNet.RepresentationModel`. Lift these private helpers into the shared `ParityOracle.cs`:

```csharp
private static string FindRepositoryRoot()
{
    string current = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(current))
    {
        if (File.Exists(Path.Combine(current, "Hexalith.Folders.slnx"))) return current;
        current = Directory.GetParent(current)?.FullName ?? string.Empty;
    }
    throw new InvalidOperationException("Could not locate repository root.");
}

private static YamlMappingNode[] LoadRows(string path)
{
    using StreamReader reader = File.OpenText(path);
    YamlStream yaml = new();
    yaml.Load(reader);
    return yaml.Documents[0].RootNode.ShouldBeOfType<YamlSequenceNode>()
        .Children.Select(r => r.ShouldBeOfType<YamlMappingNode>()).ToArray();
}

private static string RequiredScalar(YamlMappingNode m, string key)
{
    m.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? v).ShouldBeTrue(key);
    return v.ShouldBeOfType<YamlScalarNode>().Value ?? string.Empty;
}
// RequiredMapping / RequiredSequence likewise (see ParityOracleGeneratorTests lines 525-541).
```

`int.Parse(RequiredScalar(node, "cli_exit_code"))` for the integer column (the generator emits it unquoted, so it parses as a scalar string — same as `ParityOracleGeneratorTests` does at line ~83/93).

The oracle path resolves to `Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "parity-contract.yaml")` and is identical from either test assembly's `AppContext.BaseDirectory`.

### Snake_case oracle string → `CanonicalErrorCategory` enum

The oracle uses snake_case (`folder_acl_denied`), the SDK enum members are PascalCase (`Folder_acl_denied`) but carry the snake_case as an `[EnumMember(Value=...)]`. Resolve by matching the `EnumMemberAttribute.Value` (mirror `FailureKindProjectionTests.EnumMemberValue<TEnum>` at lines 74-79), e.g.:

```csharp
static CanonicalErrorCategory ParseCategory(string oracleValue) =>
    Enum.GetValues<CanonicalErrorCategory>().Single(c =>
        typeof(CanonicalErrorCategory).GetField(c.ToString())!
            .GetCustomAttribute<EnumMemberAttribute>()!.Value == oracleValue);
```

Do **not** `Enum.Parse` against the PascalCase name — the wire/oracle contract is the `EnumMember` string.

### Internals access — why the reader can't touch both projections

`Hexalith.Folders.Cli.Errors.ErrorProjection` / `FoldersExitCodes` and `Hexalith.Folders.Mcp.Errors.FailureKindProjection` are `internal`, exposed only to their **own** test project via `InternalsVisibleTo`. Therefore:
- The shared `tests/shared/Parity/*` source must reference **neither** projection — it only reads the oracle and yields raw strings/ints + `TheoryData`.
- The adapter-specific assertion (call `ErrorProjection.Project(...)` / `FailureKindProjection.Project(...)`) lives in each project's own `ParityOracleConformanceTests.cs`, which compiles inside the assembly that has internals access.
- A single test calling **both** projections would need a project referencing both `src` adapters — that is **Story 5.6**, out of scope here.

### Why NOT put the reader in `Hexalith.Folders.Testing`

`src/Hexalith.Folders.Testing` is `IsPackable=true` — it ships as a NuGet package. Adding `YamlDotNet` and test-fixture-reading code there would (a) leak a dependency into a published package and (b) put test-only, `tests/fixtures/`-coupled code in a shipped library. Keep the oracle reader test-only: a linked shared source under `tests/shared/Parity/`. (A dedicated `tests/tools`-style support **project** is the heavier alternative; it would require a `.slnx` edit and is unnecessary for a linked-source reader. Prefer the linked source unless linking proves unworkable, in which case duplicate the small reader per project and note it — but do **not** add the reader to the packable Testing library.)

### Reusable test harnesses (already built — wrap, don't rebuild)

- **CLI:** `tests/Hexalith.Folders.Cli.Tests/TestSupport/CliTestHarness.cs` — `RunAsync(params string[])`→exit code; `UseRealClient(HttpStatusCode, json)`→`CapturingHttpHandler` (with `.Header("X-Correlation-Id" | "Idempotency-Key" | ...)`); `ClientFactoryInvoked`; `Console.StdErr`/`StdOut`; `IdempotencyKeyGenerator`; `SetEnvironment`. The existing `BehavioralParityTests.cs` is the template for pre-SDK/correlation assertions — your oracle-driven tests parameterize the *expectations* from the oracle column.
- **MCP:** `tests/Hexalith.Folders.Mcp.Tests/TestSupport.cs` — `Pipeline(IClient, token=Token)`→`ToolPipeline`; `RealClient(CapturingHandler)` / `BearerClient(...)`; `CapturingHandler(status, body)` with `Requests` (`CapturedRequest.CorrelationId/TaskId/Authorization/IdempotencyKey/...`); `Kind(json)` / `CorrelationId(json)` / `Parse(json)`. Invoke tool methods **directly** (e.g. `FolderTools.CreateFolder(pipeline, idempotencyKey, taskId, correlationId, requestJson, ct)`) as `SourcingTests.cs` does — no JSON-RPC round-trip needed.
- MCP tool name == **kebab-case of `operation_id`** (confirmed: `create-folder`←`CreateFolder`, `create-repository-backed-folder`←`CreateRepositoryBackedFolder`, `add-file`←`AddFile`). So MCP per-operation oracle binding is deterministic via reflection over `[McpServerTool(Name=...)]`. CLI command names are **not** derivable from `operation_id` (e.g. `folder create-repo-backed`), so CLI per-operation pre-SDK tests use a small explicit representative `operation_id → argv` map; the oracle still owns the expected exit code / accept-reject.

### Adapter expectations & exit-code vocabulary (for the coverage guards)

- CLI canonical exit-code set (`FoldersExitCodes`): `{0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 1}`. `64`=usage (pre-SDK), `65`=credential family, `66`=access denied, `67`=lock, `68`=idempotency, `69`=validation/input, `70`=provider/repo, `71`=unknown provider outcome, `72`=reconciliation/freshness, `73`=not-found/auth-revoked, `74`=state-transition, `75`=redacted, `1`=internal/query_timeout/unmapped.
- MCP failure kind == canonical category verbatim for post-SDK; pre-SDK kinds `usage_error`/`credential_missing` (and `internal_error` catch-all) are layered by the pipeline, not the projection.
- `adapter_expectations` includes `cli` and `mcp` on every row — you may assert that as a sanity precondition (a row missing `cli`/`mcp` would mean the adapter shouldn't be tested against it), but all 47 current rows list both.

### Previous-story intelligence (lessons to carry in)

- **Story 5.3 (MCP, just merged):** `FailureKindProjection` encodes `kind == category name` for 43 post-SDK categories; `range_unsatisfiable`→`internal_error` (drift signal); pre-SDK `usage_error`/`credential_missing` are added by `ToolPipeline`, never by the projection. Tests are hermetic over a fake `HttpMessageHandler`/`IClient`; assert `Requests` empty for pre-SDK paths. `SourcingTests.MutatingToolsDeclareIdempotencyKeyAndQueryToolsDoNot` already reflects over the 47 tools — adapt its mutating-set source to the oracle column for AC #4.
- **Story 5.2 (CLI):** `ErrorProjection` resolved the same authoritative-set trap (architecture's 13-row summary is NON-authoritative; the oracle `outcome_mapping` column is). Reviewer verified the map row-by-row against all categories. `range_unsatisfiable`→`1`. Pre-SDK short-circuits before the client factory (`ClientFactoryInvoked == false`). Wire shape is camelCase. `BehavioralParityTests`/`ErrorProjectionTests` are the templates.
- **Authoritative-set trap (still live):** do NOT assert against architecture.md §"Adapter Parity Contract"'s abbreviated 13-element list (it even misspells `unknown_provider_outcome` as `provider_outcome_unknown`). Assert against `tests/fixtures/parity-contract.yaml` only.
- **Epic 1 retro / project-context:** prefer executable boundaries over prose; metadata-only everywhere (these tests read only category names/codes — no secrets/paths, fine); code review is a mandatory pipeline stage — budget for at least one reviewer patch.
- **One pre-existing environmental test** (`ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration`, whitespace-only) fails regardless of this story — it is not a 5.4 regression; ignore it.

### Git intelligence

- `baseline_commit`: `7362898` (`feat(story-5.3): Implement MCP tools, resources, and failure kinds`). Recent history: 5.1 (SDK convenience) → 5.2 (CLI) → 5.3 (MCP). The two adapter projections this story validates are freshly settled. Commit convention: `feat(story-5.4): <imperative summary>`.
- Do **not** touch submodules. The working tree may show gitlink drift in `Hexalith.Commons`/`Hexalith.EventStore` newer than the superproject records and one unrelated modified file under `_bmad-output/story-automator/` — leave all of it; do **not** stage it with the Story 5.4 commit.

### Testing requirements

[Source: project-context.md#Testing-Rules]

- Tests live in `tests/Hexalith.Folders.Cli.Tests/` and `tests/Hexalith.Folders.Mcp.Tests/`; shared reader under `tests/shared/Parity/`. xUnit v3 + Shouldly; NSubstitute only if a focused double is needed (the reader needs none). `TestContext.Current.CancellationToken` in async tests.
- **Hermetic:** read the committed oracle file from disk via `FindRepositoryRoot()`; pre-SDK assertions make no HTTP call; post-SDK assertions use the fake handler/`IClient`. No live server, Dapr, Keycloak, Redis, network, or nested submodule init.
- **Shared fixtures unforked:** do not copy `parity-contract.yaml` into the test projects; read it in place. Do not fork the loader into two divergent copies — one linked shared source (project-context: "do not fork parity ... corpora into per-test-project copies").
- Use `tests/fixtures/parity-contract.schema.json` only if you want to additionally assert vocabulary (optional; the governance gate already validates the oracle against the schema — AC #6 vocabulary guard can read the enum sets from the SDK enum + the canonical exit-code set instead of re-reading the schema).

### Project Structure Notes

- New: `tests/shared/Parity/ParityOracle.cs`, `tests/shared/Parity/ParityScenarios.cs` (linked into both test projects). New: `tests/Hexalith.Folders.Cli.Tests/ParityOracleConformanceTests.cs`, `tests/Hexalith.Folders.Mcp.Tests/ParityOracleConformanceTests.cs`. Modified: the two test `.csproj` (add `YamlDotNet` reference + `<Compile Include=".." Link=".." />` for the shared sources).
- File-scoped namespaces, `using` outside namespace, one primary public type per file, `Async` suffix on async methods, `ConfigureAwait(false)` on library-style awaits where nearby code does. `.editorconfig`: CRLF for `.cs`, 4-space indent, final newline, trimmed trailing whitespace.
- No inline package `Version` attributes (central `Directory.Packages.props`; `YamlDotNet 18.0.0`). `IsPackable=false` already set on both test projects. `Hexalith.Folders.slnx` is **unchanged** (linked compile is csproj-local; no new project).
- No conflicts with the unified structure: this mirrors how `Hexalith.Folders.Contracts.Tests` already reads the oracle, generalized into a shared linked reader so CLI and MCP share one loader.

### References

- [Source: epics.md#Epic-5 / #Story-5.4] — epic objective (cross-surface parity verified through generated oracle rows and shared conformance tests) and the verbatim Story 5.4 acceptance criteria.
- [Source: tests/fixtures/parity-contract.yaml] — the oracle under test: 47 operation rows; `behavioral_parity` (success + sourcing columns) and `outcome_mapping` (per-category `cli_exit_code`/`mcp_failure_kind`/`pre_sdk_error_class`). **The source of truth for every expected value in this story.**
- [Source: tests/fixtures/parity-contract.schema.json] — `behavioral_parity` required keys (`pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `task_id_sourcing`, `credential_sourcing`, `cli_exit_code`, `mcp_failure_kind`) and `outcome_mapping` shape; `$defs` enums for `canonical_error_category` / `mcp_failure_kind` / `cli_exit_code`.
- [Source: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs] — the proven YamlDotNet loading pattern (`FindRepositoryRoot`/`LoadRows`/`RequiredScalar`/`RequiredMapping`/`RequiredSequence`, lines ~468-557) to lift into the shared reader.
- [Source: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs] — existing oracle↔OpenAPI completeness gate (`ParityCompletenessComparesStructuredOpenApiOperationsToGeneratedRows`, missing/stale/duplicate negative controls); this story's row-completeness check is the adapter-test-side echo, not a replacement.
- [Source: src/Hexalith.Folders.Cli/Errors/ErrorProjection.cs + FoldersExitCodes.cs] — the CLI category→exit-code projection under test (already oracle-verbatim); exit-code vocabulary.
- [Source: src/Hexalith.Folders.Mcp/Errors/FailureKindProjection.cs] — the MCP category→failure-kind projection under test (kind == category name; `range_unsatisfiable`→`internal_error`).
- [Source: tests/Hexalith.Folders.Cli.Tests/{ErrorProjectionTests.cs, BehavioralParityTests.cs, TestSupport/CliTestHarness.cs}] — independent restatement to retain + harness to reuse.
- [Source: tests/Hexalith.Folders.Mcp.Tests/{FailureKindProjectionTests.cs, SourcingTests.cs, TestSupport.cs}] — independent restatement to retain + harness/`CapturingHandler` to reuse; `EnumMemberValue` helper and the mutating-tool reflection pattern.
- [Source: architecture.md#Adapter-Parity-Contract] — per-adapter behavioral dimensions and cross-adapter invariants. **The 13-row failure-kind summary there is NON-authoritative** (Authoritative-set trap); assert against the oracle file only.
- [Source: architecture.md#C13] — parity oracle columns; `*.Cli.Tests`/`*.Mcp.Tests` consume `parity-contract.yaml` behavioral-parity columns (this story).
- [Source: project-context.md] — CLI exit codes & MCP failure kinds map 1:1 to canonical categories (never collapse); generated/oracle artifacts are not hand-edited; shared fixtures unforked; central package management; hermetic-gate rules; submodule policy.

### Latest technical notes (pinned versions — do not bump in this story)

Centrally managed in `Directory.Packages.props` (repo config is authoritative): **YamlDotNet 18.0.0** (add to the two test csproj — same package the Contracts tests use; `YamlDotNet.RepresentationModel` for the AST read), xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0` (only if needed), Newtonsoft.Json `13.0.4` (MCP JSON inspection via `JObject`). Do not add other packages; do not regenerate the client or the oracle.

**Build/test in this environment:** the WSL-native .NET SDK fails the `global.json` `10.0.300` pin; build and test through the Windows SDK from WSL, e.g. `/mnt/c/Program\ Files/dotnet/dotnet.exe` (`dotnet.exe restore|build|test`). [Source: user-memory — ".NET Windows SDK in WSL"]

## Dev Agent Record

### Agent Model Used

claude-opus-4-7[1m] (Claude Opus 4.7, 1M context) — BMAD dev-story workflow

### Debug Log References

- Build/test via the Windows SDK from WSL (`/mnt/c/Program Files/dotnet/dotnet.exe`, 10.0.300) — the WSL-native SDK fails the `global.json` pin, per Dev Notes.
- Full solution: `dotnet.exe restore Hexalith.Folders.slnx` + `build --no-restore` → **0 Warning(s), 0 Error(s)**.
- Focused suites at baseline: `Hexalith.Folders.Cli.Tests` **691 passed / 0 failed**; `Hexalith.Folders.Mcp.Tests` **646 passed / 0 failed**.
- Drift-injection sanity check (then reverted via `git checkout`): flipping `Idempotency_conflict` in `ErrorProjection` (68→69) failed **15** CLI oracle-driven cases; flipping it in `FailureKindProjection` (`idempotency_conflict`→`validation_error`) failed **15** MCP cases — confirming the oracle-driven assertions bite. Both projection files restored to baseline (verified clean in `git status`).

### Completion Notes List

- **Oracle-driven case count:** the oracle carries **529** `outcome_mapping` entries across **47** operation rows. The CLI exit-code `[Theory]` and the MCP failure-kind `[Theory]` each iterate all 529 — so ~1,058 oracle-driven post-SDK assertions in total, plus the coverage/drift `[Fact]`s and pre-SDK/correlation sourcing facts.
- **Shared reader (Task 1, AC #1/#6/#7):** one linked source set under `tests/shared/Parity/` (`ParityOracle.cs` + `ParityScenarios.cs`, namespace `Hexalith.Folders.Parity.Testing`), compiled into **both** test projects via `<Compile Include><Link>` — no `.slnx` change, no forked loader, no copied fixture. The reader references neither adapter projection (both are `internal` to their own assemblies); it yields raw oracle strings/ints + `TheoryData` only. `YamlDotNet` added centrally-pinned (no inline `Version`) to both test csproj.
- **CLI (Task 2, AC #2/#4/#5/#6/#8):** per-row exit-code theory `ErrorProjection.Project(ParseCategory(category)) == cli_exit_code`; success marker → 0; coverage/drift facts (every oracle category projects to its deduped code within the canonical set; `range_unsatisfiable` absent → 1; every oracle exit code ∈ canonical set; dedup map has no conflicting codes; 47 distinct rows; every row lists `cli`+`mcp`); pre-SDK sourcing via `CliTestHarness` for `folder create-repo-backed` (missing key→64, missing `--task-id`→64, missing token→65) and `folder status` (rejects `--idempotency-key`→64) — all asserting `ClientFactoryInvoked == false`; correlation echo/ULID gated on the oracle column.
- **MCP (Task 3, AC #3/#4/#5/#6/#7/#8):** per-row failure-kind theory `FailureKindProjection.Project(ParseCategory(category)) == mcp_failure_kind` (+ kind == category-name invariant); success marker `none` handled explicitly and never a projected kind; coverage/drift facts (every oracle kind is a real `CanonicalErrorCategory` `EnumMember`; verbatim-name/no-collapse; `range_unsatisfiable`→`internal_error`); pre-SDK sourcing via reflection over `[McpServerTool]` methods asserting `idempotencyKey` presence ⟺ oracle `idempotency_key_sourcing=='caller_provided'` for all 47 tools (tool name == kebab-case of `operation_id`), plus representative `create-repository-backed-folder` missing-taskId/key/credential → `usage_error`/`credential_missing` with `Requests` empty; correlation echo/ULID gated on the oracle column.
- **Snake_case → enum** resolved via `EnumMemberAttribute.Value` (mirroring `FailureKindProjectionTests.EnumMemberValue`), never `Enum.Parse` on PascalCase.
- **AC #6 "unsupported categories fail tests" reconciliation:** the 4 `CanonicalErrorCategory` members absent from the oracle `outcome_mapping` are `Success`, `Client_configuration_error`, `Credential_missing` (oracle carries it only as a `pre_sdk_error_class`), and the documented drift exception `Range_unsatisfiable`. The guard fails loudly if any *other* enum member is absent from the oracle (e.g. a newly added category) — its purpose as a drift gate.
- **No real projection bug found:** both `ErrorProjection` and `FailureKindProjection` agree with `parity-contract.yaml` row-for-row, so no Dev-Notes drift report is required and the oracle/production code were not touched. This story is hermetic and test-only.
- **AC #8 defense-in-depth preserved:** `ErrorProjectionTests.cs` and `FailureKindProjectionTests.cs` retained (only a one-line `<remarks>` cross-reference added to each).

### File List

- `tests/shared/Parity/ParityOracle.cs` (new) — linked oracle reader + `ParityRow`/`OutcomeMapping` row model + dedup-map builders.
- `tests/shared/Parity/ParityScenarios.cs` (new) — xUnit `TheoryData` providers (CLI/MCP outcome tuples, operation ids, row lookup).
- `tests/Hexalith.Folders.Cli.Tests/ParityOracleConformanceTests.cs` (new) — CLI oracle-driven conformance.
- `tests/Hexalith.Folders.Mcp.Tests/ParityOracleConformanceTests.cs` (new) — MCP oracle-driven conformance.
- `tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj` (modified) — linked shared sources + `YamlDotNet` reference.
- `tests/Hexalith.Folders.Mcp.Tests/Hexalith.Folders.Mcp.Tests.csproj` (modified) — linked shared sources + `YamlDotNet` reference.
- `tests/Hexalith.Folders.Cli.Tests/ErrorProjectionTests.cs` (modified) — `<remarks>` cross-reference to the oracle-driven suite (AC #8).
- `tests/Hexalith.Folders.Mcp.Tests/FailureKindProjectionTests.cs` (modified) — `<remarks>` cross-reference to the oracle-driven suite (AC #8).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified) — story 5-4 status tracking (in-progress → review).

### Change Log

| Date | Change |
| --- | --- |
| 2026-05-28 | Story created — comprehensive context engineering for oracle-driven CLI/MCP parity conformance tests (shared YamlDotNet reader linked into both test projects; post-SDK exit-code/failure-kind + pre-SDK/sourcing assertions driven by `parity-contract.yaml`; drift/coverage guards). Status → ready-for-dev. |
| 2026-05-28 | Implemented all 4 tasks: shared `tests/shared/Parity` oracle reader + scenarios linked into both test projects (YamlDotNet added centrally-pinned); CLI + MCP `ParityOracleConformanceTests` driving post-SDK exit-code/failure-kind, pre-SDK/sourcing, correlation, and completeness/coverage/vocabulary drift guards from the 47-row / 529-entry oracle; retained independent restatements with cross-reference comments. Test-only, hermetic, no production/oracle/generated/`.slnx` change. Full build 0/0; CLI 691 pass, MCP 646 pass; drift injection confirmed assertions bite (15 CLI + 15 MCP) then reverted. Status → review. |
| 2026-05-28 | Adversarial code review (cycle 1) — independently rebuilt and ran both suites on the Windows SDK 10.0.300: CLI **691 pass / 0 fail**, MCP **646 pass / 0 fail**, build **0 warn / 0 err**. Verified all 9 ACs against source-of-truth (oracle = 47 rows / 43 distinct categories / 529 outcome entries; enum = 47 members 0–46). Confirmed the oracle theories are non-vacuous (CLI conformance class = 589 = 529 + 47 + 13; MCP = 542 = 529 + 13). Confirmed drift-bite claim arithmetic (idempotency_conflict in 14 outcome rows + 1 deduped-map fact = 15). No production/oracle/generator/`.slnx` change; File List matches git. **No Critical/High/Medium findings; 2 Low observations (informational, not fixed).** Status → done. |

## Senior Developer Review (AI)

**Reviewer:** Jerome — **Date:** 2026-05-28 — **Cycle:** 1 (autonomous, auto-fix) — **Outcome:** ✅ Approve

### What was independently verified (not taken on the dev's word)

- **Build:** `dotnet.exe build` of both test csproj on Windows SDK 10.0.300 → **0 warning / 0 error**.
- **Tests:** `Hexalith.Folders.Cli.Tests` **691 / 0**, `Hexalith.Folders.Mcp.Tests` **646 / 0** (matches the Dev Agent Record exactly).
- **Theories are real, not vacuous:** the new CLI conformance class runs **589** cases (= 529 flattened `outcome_mapping` exit-code theory + 47 adapter-declaration theory + 13 facts) and the MCP class **542** (= 529 failure-kind theory + 13 facts). The flattened theories therefore genuinely iterate every one of the 529 oracle outcome entries.
- **Source-of-truth cross-checks:** oracle = **47** rows, **43** distinct `canonical_error_category`, **529** `outcome_mapping` entries; `CanonicalErrorCategory` enum = **47** members (0–46) all carrying `[EnumMember]` (so `EnumMemberValue(Success)` cannot NRE); the 4 enum members absent from the oracle are exactly `Success`/`Client_configuration_error`/`Credential_missing`/`Range_unsatisfiable` — matching the test's accounted-for set, so the `EveryEnumMemberAbsentFromTheOracleIsExplicitlyAccountedFor` guard is correct, not coincidental.
- **Drift-bite claim reconciled:** `idempotency_conflict` appears in **14** outcome rows; flipping its projection fails those 14 theory cases **plus** the 1 deduped-map fact = the **15** the dev reported. Consistent.
- **Hermetic / test-only honored:** `git status` shows **no** changes under `src/`, the SDK, `tests/fixtures/` (oracle/schema), the generator, or `Hexalith.Folders.slnx`. `YamlDotNet` added with **no** inline `Version` (centrally pinned `18.0.0`). AC #8 restatements (`ErrorProjectionTests`, `FailureKindProjectionTests`) preserved — diff shows only an added `<remarks>` cross-reference, no deletion.
- **File List ↔ git reality:** matches. The extra git-modified files (`_bmad-output/.../test-summary.md`, `_bmad-output/story-automator/orchestration-*.md`) are excluded-from-review automation churn the story explicitly says to leave; not a discrepancy.

### AC coverage

All 9 acceptance criteria implemented and proven: #1 oracle read in place (no parallel table), #2/#3 per-entry exit-code/failure-kind theories + success markers, #4 pre-SDK idempotency/task/credential sourcing on **both** adapters with no-HTTP-call proof, #5 correlation echo/fresh-ULID gated on the oracle column, #6 row-count + bidirectional category-coverage + vocabulary guards, #7 single shared linked reader, #8 independent restatements retained, #9 hermetic test-only.

### Findings

- 🔴 Critical: none — 🟠 High: none — 🟡 Medium: none.
- 🟢 **Low (informational, not fixed):**
  1. `ParityOracle.Load()` (uncached re-read) is public but unreferenced by any test (`Rows` is used everywhere). Left in place deliberately — it is part of the shared reader's intended API surface that stories 5.5/5.6 will also consume; removing it now would be scope creep, not a fix.
  2. `KebabCase` in the MCP conformance test derives tool names by inserting `-` before every interior uppercase char. Correct for all 47 current `operation_id`s (the test self-validates via `rowByToolName.TryGetValue(...).ShouldBeTrue`), but latently fragile for a future operation containing an acronym/digit (e.g. `GetACL` → `get-a-c-l`). No current defect.

No HIGH/MEDIUM issues existed to auto-fix (fixed = 0); no action items created (action = 0).

_Reviewer: Jerome on 2026-05-28_
