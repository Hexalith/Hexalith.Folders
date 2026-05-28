---
baseline_commit: 75e9782
---

# Story 5.5: Validate golden lifecycle parity across REST and SDK

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder validating one canonical workflow contract,
I want the golden lifecycle scenario executed through REST and SDK,
so that transport parity is proven before CLI and MCP adapter behavior is layered on.

## Acceptance Criteria

Source epic AC (epics.md#Story-5.5):

> **Given** REST endpoints and SDK client are available
> **When** the golden lifecycle scenario runs through both surfaces
> **Then** operation identity, authorization, errors, idempotency, audit metadata, correlation, and terminal states match oracle expectations
> **And** shared conformance fixtures cover the canonical flow of provider readiness, repository binding, prepare, lock, file change, commit, context query, status, and audit inspection
> **And** any transport drift fails loudly.

Decomposed, testable acceptance criteria:

1. **The oracle's `transport_parity` columns are the source of truth — read in place, no second copy.** Tests load `tests/fixtures/parity-contract.yaml` and assert REST/SDK transport behavior against the values **in that file**. The shared reader is extended to expose the `transport_parity` block (`auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`) and the row-level `read_consistency_class`; no test hard-codes a parallel expected table that could drift from the oracle. Do **not** regenerate, hand-edit, or fork the oracle. This is the **transport-parity** mirror of Story 5.4 (which consumed the **behavioral** columns for CLI/MCP).

2. **Operation identity, oracle-driven, both surfaces.** For **every** oracle row whose `adapter_expectations` contains `sdk`, the generated `Hexalith.Folders.Client.IClient` exposes a method named `{operation_id}Async` (e.g. `CreateRepositoryBackedFolderAsync`, `AddFileAsync`, `GetWorkspaceStatusAsync`, `ListAuditTrailAsync`). For **every** oracle row whose `adapter_expectations` contains `rest`, the REST surface registers an endpoint identified by that `operation_id` (operationId/endpoint name). A missing or extra operation on either surface fails the test. (All 47 current rows list both `sdk` and `rest`.)

3. **Idempotency-key transport rule, oracle-driven, both surfaces.** Driven by `transport_parity.idempotency_key_rule`:
   - `required_with_operation_id` / `required_for_mutating_command` (mutating): the SDK method declares an `idempotency_Key` parameter; the REST endpoint **accepts** an `Idempotency-Key` header and replay of the same key + equivalent payload returns the same logical result (no duplicate side effect).
   - `not_accepted_for_non_mutating_operation` (query/context/audit/projection): the SDK method declares **no** idempotency parameter; the REST endpoint **rejects** an `Idempotency-Key` header with the canonical `idempotency_key_not_allowed` / `400` problem (per project-context: non-mutating operations must not accept `Idempotency-Key`).

4. **Correlation transport, oracle-driven, both surfaces.** Driven by `transport_parity.correlation_field_path` (`headers.X-Correlation-Id` for all rows): an explicit correlation id is echoed **unchanged** on the response `X-Correlation-Id` header for both REST (raw) and SDK invocations; when omitted, a fresh 26-char ULID is generated (SDK via `CorrelationAndTaskId.ResolveCorrelationId`), propagated to the wire, and surfaced. Task-scoped operations (`task_id_sourcing == caller_provided`) carry `X-Hexalith-Task-Id` on both surfaces.

5. **Error/authorization transport shape, oracle-driven.** For representative negative cases per operation family:
   - the emitted canonical error **category** is a member of that row's `transport_parity.error_code_set` (an out-of-set category fails);
   - an authorization denial maps to the row's `transport_parity.auth_outcome_class` (e.g. `folder_acl_denied`, `tenant_access_denied`), and where `auth_outcome_class == safe_not_found` the unauthorized and nonexistent cases are caller-indistinguishable;
   - the body is canonical RFC 9457 `application/problem+json` carrying `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details.visibility == metadata_only` (plus `taskId` when present);
   - REST (raw HttpClient) and SDK surface the **same** category/shape for the same provoked failure.

6. **Terminal transport state, oracle-driven.** Each operation's HTTP outcome matches its `transport_parity.terminal_states` **transport class**: `accepted` (mutating → `202`), `projected` (`query_status` → `200`), `context_returned` (`context_query` → `200`), `audit_returned` (`audit` → `200`), `projection_returned` (`operations_console_projection` → `200`). These are **transport**-terminal states, not domain lifecycle states — do not assert worker/provider-driven domain progression (that is Epic 4's lifecycle coverage), and do not require Dapr, providers, or network.

7. **The golden lifecycle scenario runs end-to-end through BOTH surfaces.** A single shared, ordered conformance fixture defines the canonical flow — **provider readiness → repository binding → prepare → lock → file change → commit → context query → status → audit inspection** — with each step pinned to an oracle `operation_id`. The scenario is executed once via **raw `HttpClient`** against `/api/v1/...` (the REST surface) and once via the generated **SDK `IClient`** (the SDK surface), both against the **same** in-process host (`Hexalith.Folders.IntegrationTests`, `EndToEnd/` per FR51). Each step asserts the oracle transport row for that operation (terminal-state class per AC #6, correlation echo per AC #4, idempotency rule per AC #3), and the two surfaces are asserted to produce **equivalent** transport outcomes for the same logical step.

8. **Completeness & drift guards — "any transport drift fails loudly."** Mirror Story 5.4's coverage guards for the transport columns:
   - **Row completeness:** the oracle contains exactly the expected operation set (47 distinct `operation_id`s); a missing or duplicate row fails.
   - **Surface coverage both directions:** every `sdk`-expected row has a matching `{operation_id}Async` SDK method, and every `IClient` operation method maps back to an oracle row (no orphan on either side); likewise every `rest`-expected row has a registered endpoint and every registered `/api/v1` operation maps back to a row. An unmatched operation on either side fails.
   - **Vocabulary guards:** every `auth_outcome_class` is in the schema enum (`tenant_authorized`, `tenant_access_denied`, `folder_acl_denied`, `audit_access_denied`, `credential_missing`, `safe_not_found`); every `idempotency_key_rule` is in its enum; every `error_code_set` member is a real `CanonicalErrorCategory`; every `terminal_states` value is one of the five known transport-terminal classes. A value outside the vocabulary fails.

9. **Shared conformance source reused — no fork.** The extended oracle reader and the golden-lifecycle step definitions live in a **single shared source** under `tests/shared/Parity/` (extending the Story 5.4 reader), linked (not copied) into the consuming test projects. Do not fork the loader or the oracle row data. The transport-column conformance theories are consumed by `Hexalith.Folders.Client.Tests` (SDK) and `Hexalith.Folders.Server.Tests` (REST); the end-to-end dual-surface run lives in `Hexalith.Folders.IntegrationTests/EndToEnd/`.

10. **Hermetic, additive, test-only — no production/contract/generated change.** No change to `src/Hexalith.Folders.Server`, `src/Hexalith.Folders.Client` (incl. `Generated/`), the SDK convenience helpers, the Contract Spine, the oracle, the generator, or any generated artifact. The only production-tree edits permitted are the test `.csproj` files (link the shared sources + add the centrally-pinned `YamlDotNet` reference where missing) and new/extended test sources. Tests build and run with **no** provider credentials, **no** Dapr/Keycloak/Redis sidecars, **no** network, **no** nested submodule init. If a test reveals a real REST/SDK transport drift, surface it in Dev Notes — do **not** "fix" it by editing the oracle.

## Tasks / Subtasks

- [ ] **Task 1 — Extend the shared parity oracle reader with the `transport_parity` columns** (AC: #1, #8, #9)
  - [ ] In `tests/shared/Parity/ParityOracle.cs` (the Story 5.4 reader), parse the per-row `transport_parity` mapping and the row-level `read_consistency_class`. Add a `TransportParity` record `(string AuthOutcomeClass, IReadOnlyList<string> ErrorCodeSet, string IdempotencyKeyRule, IReadOnlyList<string> AuditMetadataKeys, string CorrelationFieldPath, IReadOnlyList<string> TerminalStates)` and extend `ParityRow` **additively** with `string ReadConsistencyClass` and `TransportParity Transport`. Reuse the existing `RequiredScalar`/`RequiredMapping`/`RequiredSequence` helpers — do **not** introduce a second YAML parsing approach.
  - [ ] **Additive only:** appending to the positional `ParityRow` record changes its constructor signature, so update the loader to populate the new members in the same file. The Story 5.4 `Cli.Tests`/`Mcp.Tests` consume `ParityRow` **by property name** (not by positional construction), so they keep compiling — re-run both suites in Task 5 as a regression check.
  - [ ] In `tests/shared/Parity/ParityScenarios.cs` add `TheoryData` providers for the transport dimension: (a) one entry per oracle row (`operation_id`, `operation_family`, `adapter_expectations`, the `TransportParity` row); (b) the deduplicated `idempotency_key_rule`/`terminal_states` partitions; and a consistency check that a given `operation_family` never carries two different `terminal_states` classes.
  - [ ] Define the **golden lifecycle step list** here (or a sibling `tests/shared/Parity/GoldenLifecycle.cs`): an ordered, immutable sequence of `(StepName, operation_id)` covering provider readiness → repository binding → prepare → lock → file change → commit → context query → status → audit inspection, each `operation_id` validated to exist in the oracle. This is the single shared conformance fixture consumed by both surfaces in Task 4.
  - [ ] Link the (extended) shared sources into `Hexalith.Folders.Client.Tests`, `Hexalith.Folders.Server.Tests`, and `Hexalith.Folders.IntegrationTests` via `<Compile Include="..\shared\Parity\ParityOracle.cs"><Link>Parity\ParityOracle.cs</Link></Compile>` (and `ParityScenarios.cs`, and the golden-lifecycle source) — mirror the existing block in `tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj`. Add `<PackageReference Include="YamlDotNet" />` (no inline `Version`; central `Directory.Packages.props` pins it) to any of those three csproj that does not already reference it. No `.slnx` edit (linked compile is csproj-local).

- [ ] **Task 2 — SDK (Client.Tests) transport-parity conformance** (AC: #2, #3, #4, #6, #8)
  - [ ] New `tests/Hexalith.Folders.Client.Tests/TransportParityConformanceTests.cs`. Reflect over the public `Hexalith.Folders.Client.IClient` interface. For every oracle row with `sdk` in `adapter_expectations`, assert a method named `{operation_id}Async` exists (operation identity, AC #2). Surface any missing/renamed method in Dev Notes — do **not** rename the generated client to satisfy the test.
  - [ ] Idempotency parity (AC #3): assert the `{operation_id}Async` method declares an `idempotency_Key` parameter **iff** the row's `idempotency_key_rule != not_accepted_for_non_mutating_operation`; query/context/audit methods declare none. (Cross-check against `operation_family`: `mutating_command` ⟺ has the param.)
  - [ ] Correlation/task/freshness parity (AC #4, #6): assert every SDK method declares an `x_Correlation_Id` parameter (correlation_field_path is `headers.X-Correlation-Id` for all rows); task-scoped rows (`task_id_sourcing == caller_provided`) declare `x_Hexalith_Task_Id`; query-family rows declare `x_Hexalith_Freshness` (read_consistency_class transport).
  - [ ] Coverage/drift `[Fact]`s (AC #8): the set of `{operation_id}Async` methods on `IClient` is exactly the set of `sdk`-expected oracle rows (no orphan method, no missing row); 47 distinct rows; every `idempotency_key_rule`/`auth_outcome_class`/`terminal_states` value is in its vocabulary.
  - [ ] Reuse `FoldersClientRegistrationTests.cs` / `ClientGenerationTests.cs` / `LifecycleStatusClientConformanceTests.cs` as the templates for reflecting the generated surface and constructing the client.

- [ ] **Task 3 — REST (Server.Tests) transport-parity conformance** (AC: #2, #3, #5, #6, #8)
  - [ ] New `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs`. Stand up the in-process host (reuse the `AddFoldersServer()` + `MapFoldersServerEndpoints()` harness already used by the Server.Tests endpoint suites). Enumerate the registered endpoints (mirror `ServerEndpointRegistrationTests.cs`) and assert every `rest`-expected oracle row maps to a registered `/api/v1` endpoint by its operation identity (endpoint name/operationId), and every registered `/api/v1` operation maps back to a row (AC #2, #8 both-directions).
  - [ ] Idempotency transport rule (AC #3): for a representative mutating endpoint, sending `Idempotency-Key` is accepted and replay (same key + equivalent body) yields the same logical result; for a representative query endpoint, sending `Idempotency-Key` is rejected with the canonical `idempotency_key_not_allowed`/`400` problem.
  - [ ] Error/authorization transport shape (AC #5): provoke representative negatives and assert the emitted canonical category ∈ the row's `error_code_set`; an authorization denial maps to the row's `auth_outcome_class`; safe-denial indistinguishability where `auth_outcome_class == safe_not_found`; the body is canonical RFC 9457 (`category`/`code`/`message`/`correlationId`/`retryable`/`clientAction`/`details.visibility == metadata_only`). Reuse `SafeAuthorizationDenialMappingTests.cs` / `FolderCanonicalErrorMapperTests.cs` patterns (and `SafeProblem()` in `FoldersDomainServiceEndpoints.cs` as the shape under test). To provoke denial in the in-process host, withhold tenant/ACL seeding (the existing denial tests show the approach); the `MutableTenantAndClaimContext` always allows, so denial must come from the unseeded authorization read models, not from the claim context.
  - [ ] Terminal transport state (AC #6): each operation-family endpoint returns its oracle terminal-state class (mutating `202` `accepted`; query `200` `projected`; context `200` `context_returned`; audit `200` `audit_returned`; projection `200` `projection_returned`). Seed the in-memory read models as the existing endpoint tests do so query/context/audit steps return `200` (transport terminal), without requiring worker/provider/domain progression.
  - [ ] Correlation echo (AC #4): explicit `X-Correlation-Id` echoed unchanged on the response; omitted → a 26-char ULID echoed.

- [ ] **Task 4 — Golden lifecycle scenario run through BOTH surfaces (IntegrationTests/EndToEnd)** (AC: #4, #5, #6, #7, #10)
  - [ ] New folder `tests/Hexalith.Folders.IntegrationTests/EndToEnd/` (per FR51). New `GoldenLifecycleParityTests.cs`. Extract/reuse the in-process host pattern from `ArchiveFolderProcessWiringTests.StartHostAsync()` (`WebApplication.CreateSlimBuilder` on `127.0.0.1:0`, in-memory repository/gateway/read-model/tenant stubs, `MutableTenantAndClaimContext`). Add a `ProjectReference` to `Hexalith.Folders.Client` (the SDK is `public` — no internals issue) so the same host can be driven by the SDK.
  - [ ] **REST run:** execute the shared golden-lifecycle step list (Task 1) once via raw `HttpClient` against `/api/v1/...` (mirror `CreateValidArchiveRequest`: set `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`). **SDK run:** execute the same step list once via the generated `IClient` pointed at the same host (`AddFoldersClient(o => o.BaseAddress = hostUri)` or `new Client(new HttpClient { BaseAddress = hostUri })`), passing the same correlation/task/idempotency values.
  - [ ] Per step, assert the oracle transport row: terminal-state class (AC #6), `X-Correlation-Id` echoed unchanged (AC #4), idempotency rule honored (mutating accepts + replay-equivalent; query rejects, AC #3). Seed tenant/permissions/folder and project read models (reuse the `SeedTenant`/`SeedPermissions`/`SeedFolder` helpers + in-memory read-model seeding) so query/context/audit steps reach their `200` transport-terminal class.
  - [ ] **Cross-surface equivalence:** for each logical step, assert REST and SDK produce equivalent transport outcomes (same terminal-state class, same echoed correlation contract, same idempotency behavior). Add at least one negative step per surface proving the canonical RFC 9457 category ∈ `error_code_set` and the metadata-only problem shape are identical across REST and SDK (AC #5).
  - [ ] **Audit metadata (AC #5, metadata-only):** the audit-trail/operation-timeline step asserts the response surfaces the oracle-declared `audit_metadata_keys` that the in-process projection populates, and contains **none** of the forbidden content (no secrets/tokens/raw file contents/diffs/provider payloads/absolute paths). Do not over-claim keys that only a worker-produced audit record would carry — assert the metadata-only invariant plus the keys the in-process projection emits, and note the boundary in Dev Notes.

- [ ] **Task 5 — Verify build + focused tests + drift sanity** (AC: #8, #10)
  - [ ] Build with the WSL-accessible Windows SDK (the WSL-native SDK fails the `global.json` `10.0.300` pin — see Dev Notes): `dotnet.exe restore Hexalith.Folders.slnx`; `dotnet.exe build Hexalith.Folders.slnx --no-restore` (0 warnings / 0 errors).
  - [ ] Run the touched suites: `dotnet.exe test tests/Hexalith.Folders.Client.Tests`, `tests/Hexalith.Folders.Server.Tests`, `tests/Hexalith.Folders.IntegrationTests`. **Regression check (additive reader):** also run `tests/Hexalith.Folders.Cli.Tests` and `tests/Hexalith.Folders.Mcp.Tests` to confirm the extended `ParityRow`/`ParityScenarios` did not break the Story 5.4 behavioral consumers.
  - [ ] **Known pre-existing failure:** `ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration` (whitespace-only) may fail regardless of this story — it is **not** a 5.5 regression (recorded in the Story 5.4 notes). Confirm it is the only pre-existing red and unrelated.
  - [ ] Sanity-check the new tests bite on injected drift (temporarily flip one oracle-derived expectation — e.g. a terminal-state class or an `idempotency_key_rule` partition — confirm the assertion fails, then revert). Record the count of oracle-driven transport cases in the Dev Agent Record.
  - [ ] Confirm: no edits under any `Generated/`; no edits to `src/Hexalith.Folders.Server`/`src/Hexalith.Folders.Client` production code, the SDK convenience helpers, the Contract Spine, `tests/fixtures/parity-contract.yaml`, or the generator; no inline package `Version` attributes (only `YamlDotNet` linked centrally-pinned where added); no recursive submodule commands; `.slnx` unchanged.

## Dev Notes

### Scope boundaries (read first)

- **In scope:** Make the **SDK** (`Hexalith.Folders.Client.Tests`) and **REST** (`Hexalith.Folders.Server.Tests`) test suites *consume* the parity oracle's **`transport_parity`** columns (operation identity, idempotency-key rule, correlation field path, error-code set, auth outcome class, audit metadata keys, terminal states) + `read_consistency_class`; and run the **golden canonical lifecycle through both surfaces** against one in-process host (`IntegrationTests/EndToEnd/`), asserting REST and SDK are transport-equivalent and oracle-conformant. Extend the Story 5.4 shared reader (`tests/shared/Parity/`) additively; link it into the three consuming test projects. This is the **transport** mirror of Story 5.4's **behavioral** consumption.
- **OUT of scope (do NOT implement here):**
  - **Story 5.6** — CLI **and** MCP behavioral parity (already partly seeded by 5.4's behavioral consumption). Do not add CLI/MCP behavioral assertions here.
  - **Story 5.7** — mixed-surface handoff (one task lifecycle split across REST/SDK/CLI/MCP). Here both runs execute the **whole** flow on **one** surface each, then are compared; no handoff between surfaces mid-flow.
  - **Domain lifecycle progression** — reaching worker/provider-driven domain-terminal states (e.g. `committed`, `released`) is Epic 4's coverage. Here `terminal_states` means the **transport**-terminal class (`accepted`/`projected`/`context_returned`/`audit_returned`/`projection_returned`) — reachable in-process without Dapr/providers/network.
  - Any change to Server/Client **production** code, the SDK convenience helpers, the Contract Spine, the oracle file, the generator, or any generated artifact. Test-only.
- **Negative scope note for the dev:** if you find yourself editing `tests/fixtures/parity-contract.yaml`, regenerating the oracle, editing the OpenAPI spine, the server endpoints, the generated client, or the convenience helpers to satisfy a test — stop. Either the test expectation is wrong, or you've found a real REST/SDK transport drift to **report in Dev Notes** (not silently paper over).

### The contract: oracle is truth, the two surfaces are the things under test

The SDK (`Hexalith.Folders.Client`, generated by NSwag) and the REST server (`Hexalith.Folders.Server`) are both generated/implemented from the **same** OpenAPI Contract Spine (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`); the oracle (`tests/fixtures/parity-contract.yaml`) is generated from that same spine. So "transport parity across REST and SDK" is: both surfaces expose the same operation identity, carry idempotency/correlation/task headers the same way, map errors to the same canonical categories with the same RFC 9457 shape, and reach the same transport-terminal states — exactly the `transport_parity` columns. Story 5.4 closed this loop for the **behavioral** columns on CLI/MCP; this story closes it for the **transport** columns on REST/SDK, and adds a live end-to-end run proving the server actually behaves per the oracle and the SDK drives it identically to raw REST.

### Oracle `transport_parity` shape (what you will read) — `tests/fixtures/parity-contract.yaml`

A YAML **sequence** of **47** operation rows (same file Story 5.4 reads). The block this story consumes:

```yaml
- operation_id: 'GetWorkspaceStatus'
  operation_family: 'query_status'                 # mutating_command | query_status | context_query | audit | operations_console_projection
  read_consistency_class: 'read-your-writes'       # queries declare this; mutating rows: 'not_applicable'
  transport_parity:
    auth_outcome_class: 'folder_acl_denied'         # enum: tenant_authorized | tenant_access_denied | folder_acl_denied | audit_access_denied | credential_missing | safe_not_found
    error_code_set: [ 'authentication_failure', 'folder_acl_denied', 'internal_error', 'not_found', 'projection_stale', ... ]
    idempotency_key_rule: 'not_accepted_for_non_mutating_operation'  # enum: required_for_mutating_command | required_with_operation_id | not_accepted_for_non_mutating_operation
    audit_metadata_keys: [ 'correlation_id', 'folder_id', 'projection_watermark', 'task_id', 'workspace_id', 'workspace_state', ... ]
    correlation_field_path: 'headers.X-Correlation-Id'
    terminal_states: [ 'projected' ]                # transport-terminal class
  behavioral_parity: { ... }                        # Story 5.4 territory — already consumed by Cli/Mcp
  outcome_mapping: [ ... ]                          # Story 5.4 territory
  adapter_expectations: [ 'cli', 'mcp', 'rest', 'sdk' ]
```

Key facts:
- **`idempotency_key_rule`** partitions mutating (`required_*`) vs non-mutating (`not_accepted_for_non_mutating_operation`). Use it (and `operation_family`) to derive the mutating/query split — do **not** hand-maintain a name list.
- **`terminal_states`** values observed across the oracle: `accepted` (mutating_command → `202`), `projected` (query_status → `200`), `context_returned` (context_query → `200`), `audit_returned` (audit → `200`), `projection_returned` (operations_console_projection → `200`). These are **transport**-terminal, not domain states.
- **`correlation_field_path`** is `headers.X-Correlation-Id` for every row.
- **`auth_outcome_class`** drives the denial-mapping assertion (AC #5). `safe_not_found` ⇒ unauthorized vs nonexistent must be caller-indistinguishable.
- **`error_code_set`** is the per-operation allow-list of canonical categories; an emitted category outside it is drift.
- `adapter_expectations` includes `rest` and `sdk` on all 47 current rows — assert that as a precondition for the surface coverage guard (AC #8).

### Operation identity mapping (oracle ↔ SDK ↔ REST) — confirmed

- **SDK:** generated method name == `{operation_id}Async`. Confirmed examples: `ValidateProviderReadinessAsync`, `CreateRepositoryBackedFolderAsync`, `BindRepositoryAsync`, `PrepareWorkspaceAsync`, `LockWorkspaceAsync`, `AddFileAsync`, `ChangeFileAsync`, `RemoveFileAsync`, `CommitWorkspaceAsync`, `ListFolderFilesAsync`, `GetWorkspaceStatusAsync`, `GetFolderLifecycleStatusAsync`, `ListAuditTrailAsync`, `ListOperationTimelineAsync`. Surface (`public partial interface IClient` + `public partial class Client : IClient`) in `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`. Reflect over `IClient` for identity/parameter assertions — both types are **public** (unlike the `internal` CLI/MCP projections in 5.4), so Client.Tests and IntegrationTests can both reference them.
- **REST:** operationId == oracle `operation_id`, mapped to `/api/v1/...` routes in `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` (+ `ProviderReadinessEndpoints.cs`). Representative routes: `POST /api/v1/provider-readiness/validations` (ValidateProviderReadiness), `POST /api/v1/folders/repository-backed` (CreateRepositoryBackedFolder), `POST /api/v1/folders/{folderId}/repository-bindings` (BindRepository), `POST .../preparation` (PrepareWorkspace), `POST .../lock` (LockWorkspace), `POST .../files/add` (AddFile), `PUT .../files/change` (ChangeFile), `POST .../commits` (CommitWorkspace), `GET .../context/tree` (ListFolderFiles), `GET .../status` (GetWorkspaceStatus), `GET .../lifecycle-status` (GetFolderLifecycleStatus), `GET .../audit-trail` (ListAuditTrail). Enumerate registered endpoints via `EndpointDataSource` (see `ServerEndpointRegistrationTests.cs`) and bind to operation identity through the endpoint name metadata the server sets.

### In-process host harness (reuse — do not rebuild)

`tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` already builds an in-process host: `StartHostAsync()` → `WebApplication.CreateSlimBuilder` bound to `http://127.0.0.1:0`, `builder.Services.AddFoldersServer()`, `app.MapFoldersServerEndpoints()`, `app.StartAsync()`, `hostUri = app.Urls.First()`. It replaces the gateway/repository/read-models/tenant-store with in-memory stubs (`InProcessEventStoreGatewayClient`, `InMemoryFolderRepository`, `InMemoryFolderLifecycleStatusReadModel`, `InMemoryFolderTenantAccessProjectionStore`, `InMemoryEffectivePermissionsReadModel`) and uses `MutableTenantAndClaimContext` (always returns `EventStoreClaimTransformEvidence.Allowed`). Seed helpers: `SeedTenant`, `SeedPermissions`, `SeedFolder`, `SeedArchivedFolder`. The host exposes both an `HttpClient { BaseAddress = hostUri }` (REST) and — by adding a `Hexalith.Folders.Client` project reference — a SDK `IClient` over the same `hostUri`. **No Dapr, no sidecars, no providers, no network.** Extract `StartHostAsync` into a small shared fixture if cleaner, but keep it in-process and hermetic.

- **Provoking denial:** `MutableTenantAndClaimContext` always allows, so authorization-denial assertions (AC #5) must come from **withholding** tenant/ACL/permission seeding so the layered authorization read models deny — `tests/Hexalith.Folders.Server.Tests/SafeAuthorizationDenialMappingTests.cs` shows the canonical denied-category mapping; reuse its approach.
- **Reaching `200` on queries:** seed the relevant in-memory read model (lifecycle/workspace/audit) so query/context/audit steps return their `200` transport-terminal class — the Server.Tests endpoint suites (`WorkspaceStatusEndpointTests.cs`, `FolderLifecycleStatusEndpointTests.cs`, `FileContextEndpointTests.cs`, `FolderAuditEndpointFilterTests.cs`) show the seeding per family.

### Wire transport details (confirmed)

- Headers: `X-Correlation-Id` (echoed unchanged when caller-provided; SDK generates a 26-char ULID via `src/Hexalith.Folders.Client/Convenience/CorrelationAndTaskId.cs` `ResolveCorrelationId` when omitted), `X-Hexalith-Task-Id` (caller-supplied, required on task-scoped ops, never SDK-generated), `Idempotency-Key` (caller-sourced; rejected on non-mutating ops), `X-Hexalith-Freshness` (read-consistency hint on queries).
- RFC 9457 problem shape: `SafeProblem()` in `FoldersDomainServiceEndpoints.cs` (and `ProviderReadinessEndpoints.cs`) emits `application/problem+json` with extensions `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details.visibility == "metadata_only"` (+ `taskId` when present, `details.finalState` for `unknown_provider_outcome`/`reconciliation_required`). This is the cross-surface error shape both REST and SDK must surface identically (AC #5).
- SDK construction: `AddFoldersClient(options => options.BaseAddress = hostUri)` (registers `AddHttpClient<IClient, ...>`) per `src/Hexalith.Folders.Client/FoldersClientServiceCollectionExtensions.cs`, or directly `new Client(new HttpClient { BaseAddress = hostUri })`.

### Why extend the existing shared reader (not a new one, not the packable Testing lib)

Story 5.4 created `tests/shared/Parity/ParityOracle.cs` + `ParityScenarios.cs` (namespace `Hexalith.Folders.Parity.Testing`), linked into Cli.Tests + Mcp.Tests, exposing **only** behavioral columns. Extend it **additively** with the transport columns and link the same file into Client.Tests/Server.Tests/IntegrationTests — one loader, no fork (project-context: "do not fork parity ... corpora into per-test-project copies"). Do **not** put the reader in `src/Hexalith.Folders.Testing` (it is `IsPackable=true` — adding `YamlDotNet` + fixture-reading code would leak a test dependency into a shipped package). `ParityRow` is `internal` and linked per-assembly; that is fine for all three consumers.

### Previous-story intelligence (lessons to carry in)

- **Story 5.4 (just merged, behavioral mirror):** built the shared `tests/shared/Parity/` reader (`FindRepositoryRoot()` → `Path.Combine(root, "tests", "fixtures", "parity-contract.yaml")`, `YamlDotNet.RepresentationModel`), linked into Cli/Mcp via `<Compile Include><Link>`; added `YamlDotNet` centrally-pinned. Snake_case oracle string → `CanonicalErrorCategory` via `EnumMemberAttribute.Value` (mirror `FailureKindProjectionTests.EnumMemberValue`), never `Enum.Parse` on PascalCase. Oracle = **47** rows / **43** distinct categories / **529** outcome entries. Coverage guards (row completeness, both-direction category coverage, vocabulary) are the template for AC #8 here. Reuse all of it.
- **Authoritative-set trap (still live):** do **not** assert against architecture.md §"Adapter Parity Contract"'s abbreviated 13-element list (it even misspells `unknown_provider_outcome` as `provider_outcome_unknown`). Assert against `tests/fixtures/parity-contract.yaml` only.
- **Epic 1 retro / project-context:** prefer executable boundaries over prose; metadata-only everywhere (these tests read category names/codes/headers and assert metadata-only problem bodies — no secrets/paths); code review is a mandatory pipeline stage — budget for at least one reviewer patch.
- **Known pre-existing red:** `ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration` (whitespace-only) fails regardless of this story — not a 5.5 regression.

### Git intelligence

- `baseline_commit`: `75e9782` (`feat(story-5.4): Consume parity oracle in CLI and MCP tests`). Recent history: 5.1 (SDK convenience) → 5.2 (CLI) → 5.3 (MCP) → 5.4 (oracle consumption, behavioral). Commit convention: `feat(story-5.5): <imperative summary>`.
- Do **not** touch submodules. The working tree may show gitlink drift in sibling submodules and one unrelated modified file under `_bmad-output/story-automator/` — leave all of it; do **not** stage it with the Story 5.5 commit.

### Testing requirements

[Source: project-context.md#Testing-Rules]

- Tests live in `tests/Hexalith.Folders.Client.Tests/` (SDK), `tests/Hexalith.Folders.Server.Tests/` (REST), and `tests/Hexalith.Folders.IntegrationTests/EndToEnd/` (dual-surface run); shared reader/scenarios under `tests/shared/Parity/`. xUnit v3 + Shouldly; NSubstitute only if a focused double is needed. `TestContext.Current.CancellationToken` in async tests.
- **Hermetic:** in-process host on `127.0.0.1:0`; read the committed oracle from disk via `FindRepositoryRoot()`; in-memory repository/gateway/read-models; no live server, Dapr, Keycloak, Redis, GitHub/Forgejo, network, or nested submodule init.
- **Shared fixtures unforked:** read `parity-contract.yaml` in place; one linked shared reader; one shared golden-lifecycle step list consumed by both surfaces.
- Integration tests own EventStore/REST-boundary behavior; the in-process host stubs EventStore/Dapr — keep it that way (no Testcontainers, no provider credentials).

### Project Structure Notes

- New: `tests/shared/Parity/` additions (transport columns in `ParityOracle.cs`/`ParityScenarios.cs`, golden-lifecycle step list). New: `tests/Hexalith.Folders.Client.Tests/TransportParityConformanceTests.cs`, `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs`, `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`. Modified: the three test `.csproj` (link shared sources + `YamlDotNet` where missing; IntegrationTests also adds a `Hexalith.Folders.Client` `ProjectReference`).
- File-scoped namespaces, `using` outside namespace, one primary public type per file, `Async` suffix on async methods, `ConfigureAwait(false)` on library-style awaits where nearby code does. `.editorconfig`: CRLF for `.cs`, 4-space indent, final newline, trimmed trailing whitespace.
- No inline package `Version` attributes (central `Directory.Packages.props`). `IsPackable=false` already set on test projects. `Hexalith.Folders.slnx` **unchanged** (linked compile is csproj-local; no new project).
- No conflicts with the unified structure: this generalizes the Story 5.4 shared reader to the transport columns and adds the architecture-mandated `*.Sdk.Tests`/`*.Rest.Tests` (= Client.Tests/Server.Tests) transport-parity consumption plus the FR51 `IntegrationTests/EndToEnd/` cross-surface run.

### References

- [Source: epics.md#Epic-5 / #Story-5.5] — epic objective (cross-surface parity verified through generated oracle rows and shared conformance tests) and the verbatim Story 5.5 acceptance criteria; sibling-story scope split (5.6 CLI/MCP behavioral, 5.7 mixed-surface handoff).
- [Source: tests/fixtures/parity-contract.yaml] — the oracle under test: 47 rows; **`transport_parity`** (`auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`) + `read_consistency_class`. **The source of truth for every expected transport value in this story.**
- [Source: tests/fixtures/parity-contract.schema.json] — `transport_parity` required keys and enums (`auth_outcome_class`, `idempotency_key_rule`); `terminal_states`/`audit_metadata_keys` array patterns — the vocabulary guard for AC #8.
- [Source: tests/shared/Parity/ParityOracle.cs + ParityScenarios.cs] — the Story 5.4 shared reader to **extend additively** with the transport columns; the `FindRepositoryRoot()`/`YamlDotNet.RepresentationModel` loading pattern and `RequiredScalar`/`RequiredMapping`/`RequiredSequence` helpers.
- [Source: tests/Hexalith.Folders.Cli.Tests/Hexalith.Folders.Cli.Tests.csproj] — the `<Compile Include="..\shared\Parity\..."><Link>` linking pattern + central `YamlDotNet` reference to replicate in the three consuming csproj.
- [Source: tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs] — `StartHostAsync()` in-process host harness, seed helpers, in-memory stubs to reuse for the dual-surface run (Task 4).
- [Source: src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs + ProviderReadinessEndpoints.cs] — REST routes (operation identity) and `SafeProblem()` RFC 9457 producer (error-shape under test).
- [Source: src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs] — `public IClient`/`Client`; `{operation_id}Async` method-per-operation surface (SDK identity/parameter assertions).
- [Source: src/Hexalith.Folders.Client/FoldersClientServiceCollectionExtensions.cs + Convenience/CorrelationAndTaskId.cs] — `AddFoldersClient(o => o.BaseAddress=...)` construction; correlation/task ULID sourcing.
- [Source: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml] — Contract Spine: operationId↔path identity that both surfaces and the oracle derive from.
- [Source: tests/Hexalith.Folders.Server.Tests/{ServerEndpointRegistrationTests.cs, SafeAuthorizationDenialMappingTests.cs, FolderCanonicalErrorMapperTests.cs, WorkspaceStatusEndpointTests.cs, FolderLifecycleStatusEndpointTests.cs, FileContextEndpointTests.cs, FolderAuditEndpointFilterTests.cs}] — endpoint enumeration, denial mapping, canonical-error mapping, and per-family read-model seeding patterns to reuse.
- [Source: tests/Hexalith.Folders.Client.Tests/{FoldersClientRegistrationTests.cs, ClientGenerationTests.cs, LifecycleStatusClientConformanceTests.cs}] — generated-surface reflection + client construction templates; the known pre-existing whitespace-only `ClientGenerationTests` red.
- [Source: _bmad-output/implementation-artifacts/5-4-consume-parity-oracle-in-cli-and-mcp-tests.md] — the behavioral mirror: shared reader, coverage guards, snake_case→enum, drift-injection sanity, authoritative-set trap, Windows-SDK-in-WSL build note.
- [Source: architecture.md#C13] — "All four surface test projects consume the oracle as xUnit theory data: `*.Sdk.Tests` + `*.Rest.Tests` for transport-parity columns" (= this story); §"Adapter Parity Contract" (the 13-row summary there is **NON-authoritative** — assert against the oracle file only); FR51 → `tests/Hexalith.Folders.IntegrationTests/EndToEnd/`.
- [Source: project-context.md] — SDK is the typed canonical client and REST is the parallel transport validated against the same OpenAPI spine; non-mutating ops must not accept `Idempotency-Key`; idempotency replay equivalence; metadata-only everywhere; safe-denial indistinguishability; generated/oracle artifacts not hand-edited; shared fixtures unforked; central package management; hermetic-gate + submodule policy.

### Latest technical notes (pinned versions — do not bump in this story)

Centrally managed in `Directory.Packages.props` (repo config is authoritative): **YamlDotNet** (link to the consuming test csproj where missing — same package Story 5.4 added; `YamlDotNet.RepresentationModel` for the AST read), xUnit v3 `3.2.2`, Shouldly `4.3.0`, NSubstitute `5.3.0` (only if needed). The in-process host uses `Microsoft.Extensions`/ASP.NET Core `10.x` already referenced by `Hexalith.Folders.Server`. Do not add other packages; do not regenerate the client, the server OpenAPI, or the oracle.

**Build/test in this environment:** the WSL-native .NET SDK fails the `global.json` `10.0.300` pin; build and test through the Windows SDK from WSL, e.g. `/mnt/c/Program\ Files/dotnet/dotnet.exe` (`dotnet.exe restore|build|test`). [Source: user-memory — ".NET Windows SDK in WSL"]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

### Change Log

| Date | Change |
| --- | --- |
| 2026-05-28 | Story created — comprehensive context engineering for transport-parity oracle consumption across REST (Server.Tests) and SDK (Client.Tests) plus a golden-lifecycle dual-surface run (IntegrationTests/EndToEnd). Extends the Story 5.4 shared `tests/shared/Parity/` reader additively with the `transport_parity` columns; in-process hermetic host; test-only, no production/contract/generated change. Status → ready-for-dev. |
