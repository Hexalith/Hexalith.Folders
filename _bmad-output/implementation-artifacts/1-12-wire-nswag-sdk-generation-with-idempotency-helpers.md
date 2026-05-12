# Story 1.12: Wire NSwag SDK generation with idempotency helpers

Status: ready-for-dev

Created: 2026-05-12

## Story

As an SDK consumer,
I want generated typed clients and idempotency-hash helpers from the Contract Spine,
so that .NET callers use the same operation shapes and retry identity semantics as REST.

## Acceptance Criteria

1. Given the Contract Spine exists at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, when the SDK generation target runs, then `Hexalith.Folders.Client` produces deterministic NSwag-generated C# clients and DTOs from that file only.
2. Given mutating operations declare `x-hexalith-idempotency-equivalence`, when client generation runs, then each generated mutating command DTO has a generated `ComputeIdempotencyHash()` helper based only on the declared semantic field list in declared lexicographic order.
3. Given non-mutating query operations must not accept idempotency keys, when SDK generation runs, then query DTOs and client methods do not expose idempotency-hash helpers or implicit idempotency-key behavior.
4. Given the SDK is the typed canonical client, when generated client methods are inspected, then they preserve Contract Spine operation IDs, route shapes, request/response schemas, Problem Details fields, `X-Correlation-Id`, `X-Hexalith-Task-Id`, and `Idempotency-Key` header behavior without adding SDK-only lifecycle semantics.
5. Given `Hexalith.Folders.Contracts` must remain behavior-free, when this story is complete, then generation wiring and helper implementation live in `Hexalith.Folders.Client` or its build-time generation assets, not in the Contracts project.
6. Given idempotency equivalence is tenant-scoped and parser-policy-sensitive, when helper tests run, then they cover null versus omitted values, field ordering, typed ULID casing, Unicode normalization where declared, single safe percent-decoding for path metadata where declared, and conflicting payload examples from `tests/fixtures/idempotency-encoding-corpus.json`.
7. Given file mutation helpers must remain metadata-only, when `AddFile`, `ChangeFile`, `RemoveFile`, `PutFileInline`, or `PutFileStream` helpers are generated, then hashes use declared metadata fields and content-hash references only; raw file contents, diffs, provider tokens, credential material, generated context payloads, raw provider payloads, and local file paths are never serialized into helper input or test output.
8. Given generated output must be reviewable and reproducible, when generation is rerun twice on the same Contract Spine, then `src/Hexalith.Folders.Client/Generated/` and any generated idempotency helper files are byte-stable except for explicitly approved tool banner text that is normalized or suppressed.
9. Given this story wires SDK generation only, when implementation is complete, then no server endpoints, EventStore commands, domain aggregate behavior, provider adapters, Git or filesystem side effects, CLI commands, MCP tools, UI pages, parity oracle result rows, runtime idempotency persistence, CI workflow gates, or nested-submodule initialization are added.
10. Given Story 1.13 owns final C13 parity rows and Story 1.14 owns CI gate wiring, when this story validates generation, then it may add focused local client-generation and helper tests but does not generate final `tests/fixtures/parity-contract.yaml` rows or modify CI workflows.
11. Given Story 1.6 through Story 1.11 may have active Contract Spine changes, when implementation starts, then the developer inspects the current OpenAPI file, extension vocabulary, and operation-group artifacts before editing generation assets; unresolved or missing operation metadata is recorded as prerequisite drift rather than silently guessed.
12. Given generated SDK output will be consumed by CLI and MCP later, when generated exception and response types are authored, then they preserve canonical Problem Details fields (`category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details`) in a typed exception or response shape that adapters can map without parsing English messages.

## Tasks / Subtasks

- [ ] Confirm prerequisites and freeze the generation source. (AC: 1, 4, 5, 9, 11)
  - [ ] Inspect `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` and `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`.
  - [ ] Inspect `docs/contract/idempotency-and-parity-rules.md`, `tests/fixtures/idempotency-encoding-corpus.json`, `tests/fixtures/previous-spine.yaml`, and `tests/fixtures/parity-contract.schema.json`.
  - [ ] Inspect current Story 1.6 through Story 1.11 artifacts before assuming operation IDs, request schemas, or `x-hexalith-*` metadata names.
  - [ ] Treat missing or malformed Contract Spine metadata as prerequisite drift. Do not invent equivalence fields, header rules, error categories, or operation names inside the generator.
  - [ ] Do not initialize or update nested submodules.
- [ ] Add deterministic NSwag generation wiring in the client project. (AC: 1, 4, 5, 8, 9)
  - [ ] Add NSwag package/tool references through central package management in `Directory.Packages.props`; do not add inline package versions in project files.
  - [ ] Add `Hexalith.Folders.Client` generation configuration, such as `nswag.json` plus an MSBuild target in `Hexalith.Folders.Client.csproj`, using the Contract Spine file as input.
  - [ ] Generate C# clients and DTOs under `src/Hexalith.Folders.Client/Generated/`; make the folder clearly generated and keep manual extension code outside it.
  - [ ] Use `HttpClient` injection and avoid generated clients owning the lifetime of shared `HttpClient`.
  - [ ] Preserve async-only APIs and `CancellationToken` support; do not generate sync methods.
  - [ ] Preserve operation IDs and route names from the spine; do not regroup operations in a way that makes adapter parity rows ambiguous.
  - [ ] Keep generated output deterministic: stable namespace, stable class names, stable collection types, stable nullable behavior, no timestamps, no machine-local paths, and no environment-dependent base URL.
- [ ] Generate idempotency helper code from Contract Spine extensions. (AC: 2, 3, 4, 6, 7, 12)
  - [ ] For every mutating operation with `x-hexalith-idempotency-equivalence`, generate a helper on the relevant request DTO or companion partial type named `ComputeIdempotencyHash()`.
  - [ ] Base helper input only on the declared field list. Field paths must be consumed in declared lexicographic order; fail generation when the list is missing, duplicated, not lexicographic, or points to a missing schema property.
  - [ ] Use a deterministic canonical representation before hashing. The representation must distinguish null from omitted values unless the schema declares an explicit default-equivalence rule.
  - [ ] Normalize only fields whose parser-policy classification allows normalization. Do not globally normalize opaque identifiers, branch names, provider references, commit metadata classifications, or path metadata.
  - [ ] For path metadata fields, apply exactly one safe percent-decode step only where Story 1.5 classified that field as eligible; reject double-decode ambiguity.
  - [ ] Use SHA-256 or another explicitly documented stable hash algorithm with UTF-8 canonical bytes and an algorithm label in the helper output or test fixture expectations.
  - [ ] Never include raw file content, raw diffs, provider tokens, credential material, raw provider payloads, generated context payloads, production URLs, local filesystem paths, or unauthorized resource hints in canonical helper input.
  - [ ] Do not implement runtime idempotency persistence, retry storage, EventStore command handling, or provider side effects.
- [ ] Add focused local tests for generation and helper behavior. (AC: 1, 2, 3, 6, 7, 8, 9, 10, 12)
  - [ ] Add or update tests under `tests/Hexalith.Folders.Client.Tests/` that run without Aspire, Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, network calls, production secrets, or initialized nested submodules.
  - [ ] Verify generation can run from a clean checkout using only repository files and central package versions.
  - [ ] Verify generated clients compile and expose expected operation-derived method or interface names for the currently authored Contract Spine operations.
  - [ ] Verify mutating DTO helpers exist only where the Contract Spine declares idempotency equivalence; query operations must not get helper methods or idempotency-key defaults.
  - [ ] Verify canonical helper results are stable across repeated invocations and independent of object property declaration order.
  - [ ] Verify fixture-driven parser-policy cases from `idempotency-encoding-corpus.json`: Unicode variants, zero-width joiner cases, ULID casing, duplicate JSON key rejection expectation, null versus omitted, percent encoding, whitespace, and malformed key examples where applicable.
  - [ ] Verify generated exception or response handling preserves RFC 9457 Problem Details plus Hexalith fields without requiring adapters to parse messages.
  - [ ] Verify negative scope: no server runtime, domain aggregate, provider adapter, CLI, MCP, UI, worker, parity oracle result row, CI workflow, or nested-submodule changes are introduced.
- [ ] Record generation decisions for downstream stories. (AC: 4, 8, 10, 12)
  - [ ] Add a concise note such as `docs/contract/sdk-generation-and-idempotency-helpers.md` if generation details, hash format, or deferred decisions need a stable human-readable home.
  - [ ] Record exact generated file locations, rerun command, deterministic-output expectations, and how Story 1.13 should consume operation IDs and helper metadata.
  - [ ] Record deferred owners for final parity-oracle rows, CI golden-file gate wiring, runtime idempotency persistence, SDK convenience `UploadFileAsync(stream)`, CLI and MCP wrappers, and release documentation.
- [ ] Run verification. (AC: 1, 2, 6, 8, 9)
  - [ ] Run the focused client generation tests.
  - [ ] Run `dotnet build Hexalith.Folders.slnx` if the current scaffold and active Contract Spine changes allow it. If blocked by unrelated in-progress Story 1.7 work or prerequisite drift, record the exact blocker without expanding this story scope.

## Dev Notes

### Scope Boundaries

- This story wires `Hexalith.Folders.Client` SDK generation from the Contract Spine and generates idempotency hash helpers for mutating request DTOs.
- Allowed implementation areas are:

```text
Directory.Packages.props
src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj
src/Hexalith.Folders.Client/nswag.json
src/Hexalith.Folders.Client/Generated/
src/Hexalith.Folders.Client/Idempotency/
tests/Hexalith.Folders.Client.Tests/
docs/contract/sdk-generation-and-idempotency-helpers.md
```

- The implementation may choose equivalent file names, but it must keep generated files separate from hand-written extension code.
- Do not modify the Contract Spine to make generation easier unless the change is a separately justified prerequisite fix. This story consumes Story 1.6 through Story 1.11 operation metadata; it does not author new operation groups.
- Do not add runtime idempotency persistence, EventStore command handling, server endpoints, provider adapters, workers, CLI, MCP, UI, parity oracle rows, CI workflow gates, or release docs.
- `Hexalith.Folders.Contracts` remains behavior-free. It may be referenced by Client but must not receive generation behavior or hash algorithms.

### Current Repository State To Inspect

- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` currently references `Hexalith.Folders.Contracts` and has only a scaffold smoke surface.
- `src/Hexalith.Folders.Client/FoldersClientModule.cs` currently exposes the module name from `FoldersContractMetadata`.
- `tests/Hexalith.Folders.Client.Tests/ClientSmokeTests.cs` currently verifies the scaffold stays contract-centered.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` exists and already contains operation groups from active Contract Spine work.
- `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml` defines `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-correlation`, `x-hexalith-read-consistency`, `x-hexalith-parity-dimensions`, and related vocabulary.
- `tests/fixtures/idempotency-encoding-corpus.json`, `tests/fixtures/parity-contract.schema.json`, and `tests/fixtures/previous-spine.yaml` exist and are the correct fixture sources to consume.

### Generation Requirements

- Use OpenAPI 3.1 in `Hexalith.Folders.Contracts` as the single source of truth.
- Use NSwag for C# client generation in `Hexalith.Folders.Client`; centralize package versions in `Directory.Packages.props`.
- Generate under `src/Hexalith.Folders.Client/Generated/` and mark generated files as generated.
- Prefer `HttpClient` injection, async methods, `CancellationToken`, generated interfaces if stable, and no generated sync methods.
- Keep `UseBaseUrl` and base-address behavior deterministic and environment-independent. Runtime host selection belongs to `HttpClient` configuration, not generated code.
- Generated clients and DTOs must be partial or have companion partials so hand-written adapters can add behavior without editing generated files.
- Exception/response projection must preserve canonical Problem Details fields for CLI and MCP adapters.

### Idempotency Helper Requirements

- Helper generation is driven by operation-level `x-hexalith-idempotency-equivalence`.
- The field list is normative and must be consumed in declared lexicographic order.
- A mutating operation without equivalence metadata is a generation failure, not a prompt to hash the whole payload.
- A non-mutating operation with idempotency metadata is a generation failure.
- Helper output should be stable and documented, for example `sha256:<hex>` over a canonical UTF-8 representation.
- Tenant-scoped equivalence includes authoritative tenant identity where declared, but tenant authority still comes from auth context and EventStore envelopes at runtime.
- Parser-policy handling comes from Story 1.5:
  - null and omitted are non-equivalent unless schema default-equivalence is declared;
  - duplicate JSON keys are parser-rejected;
  - ULID casing can normalize only for fields explicitly typed as ULID;
  - Unicode normalization applies only to fields explicitly classified as normalization-insensitive;
  - percent-encoded path equivalence uses one safe decode only for eligible path metadata.
- Helpers must never hash raw file contents, diffs, secrets, provider tokens, credential material, raw provider payloads, generated context payloads, local paths, or unauthorized resource hints.

### Previous Story Intelligence

- Story 1.1 establishes scaffold and dependency direction.
- Story 1.2 establishes root configuration and the no-recursive-submodule policy.
- Story 1.3 seeds fixtures, including `idempotency-encoding-corpus.json`, `parity-contract.schema.json`, and `previous-spine.yaml`.
- Story 1.4 owns C3/C4/C6/S-2 evidence. Story 1.12 consumes approved values where the Contract Spine already declares them and must preserve unresolved values as reference-pending.
- Story 1.5 finalizes idempotency equivalence, parser-policy classifications, adapter parity dimensions, and explicitly names Story 1.12 as owner for generated helpers.
- Story 1.6 owns OpenAPI foundation and shared extension vocabulary. Reuse it; do not redefine extension shapes in client code.
- Stories 1.7 through 1.11 own operation groups. Generated method names, DTOs, and helpers must follow those operations instead of using a separate SDK inventory.
- Story 1.11 reinforces the current pattern: explicit allowed file sets, safe-denial semantics, metadata-only outputs, and negative-scope tests prevent cross-story bleed.

### Latest Technical Notes

- Current NSwag guidance supports C# client generation through `CSharpClientGenerator` and configuration-driven `openApiToCSharpClient` outputs.
- Relevant NSwag settings include generated C# namespace, class name, `InjectHttpClient`, `DisposeHttpClient`, `GenerateClientInterfaces`, `GenerateExceptionClasses`, `GenerateContractsOutput`, and disabling sync methods.
- NSwag-generated C# clients are partial and expose request/response hooks such as `PrepareRequest` and `ProcessResponse`; use partials or companion code rather than editing generated files.
- The SDK should use generated code for transport shape and separate generated or hand-written companion code for Hexalith-specific idempotency helpers when NSwag templates alone are not enough.

### Testing Guidance

- Tests must run offline with normal `dotnet test` behavior and without Aspire, Dapr, Keycloak, Redis, GitHub, Forgejo, provider credentials, tenant data, production secrets, network calls, or initialized nested submodules.
- Use the existing `Hexalith.Folders.Client.Tests` project unless a focused generator test project is clearly needed.
- Good tests for this story are deterministic-output checks, generated-code compile checks, helper existence checks, helper non-existence for queries, fixture-driven hash cases, and negative-scope checks.
- Avoid broad runtime integration tests. Runtime idempotency persistence and lifecycle behavior belong to Epic 4; CLI and MCP behavioral parity belong to Epic 5.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 1.12: Wire NSwag SDK generation with idempotency helpers`
- `_bmad-output/planning-artifacts/epics.md#Contract Spine (Phase 1 - C0)`
- `_bmad-output/planning-artifacts/architecture.md#API & Communication Patterns`
- `_bmad-output/planning-artifacts/architecture.md#Adapter Parity Contract`
- `_bmad-output/planning-artifacts/architecture.md#Build process structure`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Handoff`
- `_bmad-output/planning-artifacts/prd.md#Cross-Surface Contract`
- `docs/contract/idempotency-and-parity-rules.md`
- `tests/fixtures/idempotency-encoding-corpus.json`
- `tests/fixtures/parity-contract.schema.json`
- `tests/fixtures/previous-spine.yaml`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`
- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj`
- `tests/Hexalith.Folders.Client.Tests/ClientSmokeTests.cs`
- `_bmad-output/project-context.md`
- `AGENTS.md#Git Submodules`
- NSwag docs via Context7 `/ricosuter/nswag`: CSharpClientGenerator, OpenAPI client generation, and configuration-driven C# client generation.

## Project Structure Notes

- SDK generation belongs in `Hexalith.Folders.Client`; the Contract Spine remains in `Hexalith.Folders.Contracts`.
- Generated output should live under `src/Hexalith.Folders.Client/Generated/`.
- Hand-written SDK extension points should live outside `Generated/`, for example under `src/Hexalith.Folders.Client/Idempotency/`.
- Tests should live under `tests/Hexalith.Folders.Client.Tests/` and may consume shared fixtures from `tests/fixtures/`.
- Parity oracle output belongs to Story 1.13; CI golden-file wiring belongs to Story 1.14.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-12 | Created ready-for-dev story through `bmad-create-story` workflow. | Codex |

## Dev Agent Record

### Agent Model Used

TBD by dev-story agent

### Debug Log References

### Completion Notes List

### File List
