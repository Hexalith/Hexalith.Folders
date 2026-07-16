---
baseline_commit: b9aa762
discovered_inputs:
  sprint_status: _bmad-output/implementation-artifacts/sprint-status.yaml
  epics: _bmad-output/planning-artifacts/epics.md
  prd: _bmad-output/planning-artifacts/prd.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  ux: _bmad-output/planning-artifacts/ux-design-specification.md
  project_context:
    - _bmad-output/project-context.md
    - Hexalith.Commons/_bmad-output/project-context.md
    - Hexalith.EventStore/_bmad-output/project-context.md
    - Hexalith.FrontComposer/_bmad-output/project-context.md
    - Hexalith.Memories/_bmad-output/project-context.md
    - Hexalith.Tenants/_bmad-output/project-context.md
  previous_story: _bmad-output/implementation-artifacts/7-12-wire-production-observability-and-alerts.md
  latest_technical_sources:
    - https://spec.openapis.org/oas/v3.1.0.html
    - https://redocly.com/docs/cli/
    - https://mermaid.js.org/syntax/stateDiagram.html
    - https://learn.microsoft.com/dotnet/standard/commandline/
    - https://github.com/modelcontextprotocol/csharp-sdk
    - https://modelcontextprotocol.io/specification
    - https://learn.microsoft.com/dotnet/core/tools/global-tools
    - https://learn.microsoft.com/aspnet/core/security/authentication/jwt
---

# Story 7.13: Publish API, SDK, CLI, and MCP consumer references

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a downstream consumer,
I want API, SDK, CLI, and MCP references published,
so that I can use the product without reading implementation code.

## Acceptance Criteria

> Epic 7.13 BDD from `_bmad-output/planning-artifacts/epics.md`:
> Given surfaces are implemented
> When consumer documentation is generated
> Then rendered OpenAPI reference, SDK quickstart, CLI reference, MCP tool/resource reference, examples, auth guidance, and lifecycle diagrams are published
> And examples compile or are otherwise validated by CI.

Decomposed acceptance criteria:

1. Publish a **rendered OpenAPI reference** derived from the single Contract Spine `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` (OpenAPI 3.1.0, title "Hexalith.Folders API", version `v1`). It must present the consumer-facing REST surface: the base server path `/api/v1`, all **9 operation tag groups** (`provider-readiness`, `folders`, `workspaces`, `files`, `commits`, `query-status`, `audit`, `ops-console`, `context-queries`), **all named operationIds** (the 47 canonical operations the SDK/CLI/MCP surfaces mirror, plus any ops-console example operations — derive the exact set by parsing the spine, do not hard-code a count), the single `oidcBearer` (`type: openIdConnect`) security scheme applied globally, the mutating header triple (`Idempotency-Key` required, `X-Correlation-Id` optional, `X-Hexalith-Task-Id` optional), the query rule that non-mutating operations must NOT accept `Idempotency-Key`, the `ValidateProviderReadiness` POST-as-query exception, and the canonical metadata-only Problem Details error shape (`category`, `code`, `message`, `correlationId`, `taskId`, `retryable`, `clientAction`, `details.visibility`). The reference must stay consistent with the existing `docs/contract/*-contract-groups.md` authoring notes and **cross-link** them rather than duplicate their normative text. No server-side Swagger/Redoc middleware exists today and none is to be added; render from the spine only.

2. Publish the **SDK quickstart**. `docs/sdk/quickstart.md` already exists (Story 5.1) and covers DI registration (`AddFoldersClient`), correlation/task-ID sourcing (`CorrelationAndTaskId`), the upload convenience helper (inline ≤ 262144 bytes vs streamed), idempotency-key guidance, and the opt-in AppHost sample run. Complete it for release: add cross-links to the new API/CLI/MCP/auth references and lifecycle diagrams, add a `HelperSchemaVersion` compatibility-gating note (`HexalithFoldersGeneratedArtifacts.HelperSchemaVersion`), and verify every code block uses only **real public types/members** from `Hexalith.Folders.Client` (`IClient`, `FoldersClientServiceCollectionExtensions`, `FoldersClientOptions`, `FileUploadDescriptor`, `FileStreamStagingEvidence`, `CorrelationAndTaskId`, `FileUpload`, `FoldersFileUploadExtensions`). Do not invent method or property names.

3. Publish an **SDK operation/API reference** for the typed client surface: enumerate the `IClient` operations grouped by the same tag groups as the OpenAPI reference, document the canonical **9-step golden lifecycle ordering** (`ConfigureProviderBinding → ValidateProviderReadiness → CreateRepositoryBackedFolder → PrepareWorkspace → LockWorkspace → UploadFile → CommitWorkspace → GetWorkspaceStatus → ListAuditTrail`), and record the generated idempotency-helper signature contract including the **parameter-order trap** (e.g. `PrepareWorkspaceRequest.ComputeIdempotencyHash(folderId, workspaceId, taskId)` — the old `(folderId, taskId, workspaceId)` order is no longer emitted and positional callers silently diverge). This MAY be a dedicated `docs/sdk/api-reference.md` or folded into the rendered OpenAPI reference (AC 1); pick one home and cross-link, do not split the same content across both.

4. Publish the **CLI reference** (`docs/sdk/cli-reference.md`) for the `folders` .NET tool (`PackAsTool=true`, `ToolCommandName=folders`; distributed as a `dotnet tool`, not a library). Document: the **7 top-level groups** (`provider`, `folder`, `workspace`, `file`, `commit`, `context`, `audit`) and every leaf subcommand; recursive global options (`--base-address`/`-b` with `HEXALITH_FOLDERS_BASE_ADDRESS`, `--token`/`-t`, `--correlation-id`, `--output`/`-o` constrained to `human`|`json`); the three-layer credential precedence (`HEXALITH_TOKEN` env → `~/.hexalith/credentials.json` per-tenant section → `--token`); the mutation-only options (`--task-id` required caller-provided, `--idempotency-key` xor `--allow-auto-key`) and that query commands reject `--idempotency-key` with exit 64; the `--request` body source (`inline JSON` | `@path` | `-` stdin); the **`commit create`** subcommand naming (named `create`, not `commit`, to avoid a System.CommandLine 2.0.8 token-table collision); and the full **exit-code table** `{0,64,65,66,67,68,69,70,71,72,73,74,75,1}` sourced verbatim from `src/Hexalith.Folders.Cli/FoldersExitCodes.cs` + `Errors/ErrorProjection.cs` (NOT the EventStore 0/1/2 scheme). All examples metadata-only with opaque synthetic identifiers.

5. Publish the **MCP tool/resource reference** (`docs/sdk/mcp-reference.md`) for the standalone `Hexalith.Folders.Mcp` stdio server (`IsPackable=false` executable sidecar, ModelContextProtocol 1.3.0, assembly attribute discovery — there is **no** `server-manifest.json`). Document: exactly **47 tools** (tool name = kebab-case of the canonical `operation_id`) grouped by their 8 tool-type files (Provider 4, Folder 11, Workspace 8, File 3, Context 5, Commit 5, Diagnostics 7, Audit 4); the **2 read-only resources** `folder-tree` (URI `folders://folder-tree/{folderId}/{workspaceId}/{taskId}`) and `audit-trail` (URI `folders://audit-trail/{folderId}`); which tools are mutating (caller-supplied `idempotencyKey`, no auto-key path) and which are task-scoped (`taskId` required); the result envelope (`correlationId` on every result; failures add `kind`, `code`, `retryable`, `clientAction`); and the **authoritative failure-kind catalog** = the 43 `outcome_mapping.mcp_failure_kind` values in `tests/fixtures/parity-contract.yaml` (each equal to its `CanonicalErrorCategory` name verbatim) **plus** the 2 pre-SDK kinds `usage_error` and `credential_missing`, with the `range_unsatisfiable → internal_error` drift note. Do NOT cite the abridged 13-row architecture summary (it misspells `unknown_provider_outcome`). Note stdio transport with logging on stderr only.

6. Publish **consumer auth guidance** (`docs/sdk/authentication.md`). Document the bearer-token `DelegatingHandler` pattern by **referencing** the existing handlers (`samples/Hexalith.Folders.Sample/BearerTokenHandler.cs`, `src/Hexalith.Folders.Cli/...`, `src/Hexalith.Folders.Mcp/Composition/BearerTokenHandler.cs`) — never duplicate token-resolution logic into docs. Document the frozen S-2 OIDC validation parameters (`ValidateIssuer/Audience/Lifetime/IssuerSigningKey=true`, `RequireSignedTokens=true`, `RequireExpirationTime=true`, `ClockSkew=30s`, JWKS auto-refresh 10 min / forced-refresh floor 1 min, JWT-only/no-introspection) by **citing** `docs/exit-criteria/s2-oidc-validation.md`, and the claim-provenance contract (`sub` = principal; `eventstore:tenant` = authoritative tenant after the EventStore claim transform; `eventstore:permission` = access gate; payload/header/query tenant values are comparison inputs only, never authority). Use **only `.invalid` placeholder issuers/audiences**; real issuer/audience values stay deployment configuration. Cross-link `docs/operations/production-identity-and-secrets.md` for production token acquisition, and document the CLI/MCP credential-sourcing precedence.

7. Publish **lifecycle diagrams** under `docs/diagrams/` (the architecture-mandated location: "Diagrams and renders in `docs/diagrams/`"). Author them as **Mermaid fenced code blocks** in markdown (rendered natively by GitHub; no doc-site generator exists and none is to be added). Publish at least three: (a) the workspace lifecycle + lock-state machine using the canonical C6 state vocabulary and **operator-disposition labels as primary labels** (`auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`) per F-4, with technical state names as secondary metadata; (b) the file-operation → commit flow; (c) the tenant/auth/ACL layered-authorization decision flow (JWT → claim transform → tenant-access freshness → folder ACL → EventStore validator → Dapr deny-by-default). Diagram states/events must trace to `docs/exit-criteria/c6-transition-matrix-mapping.md`; no operation may appear that is absent from the spine; metadata-only captions.

8. Make **examples compile or be CI-validated**. Reuse the two existing example-validation rails rather than inventing new ones: (a) add any new compilable C# snippet(s) as `.cs` files under `tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj` with matching `tests/fixtures/pattern-example-manifest.yaml` entries (`synthetic_data_only: true`, valid `<!-- hexalith-example: ... -->` marker, existing `source_path`) — this project is already built by `run-governance-completeness-gates.ps1` and the manifest is asserted by `GovernanceCompletenessGateTests.PatternExampleManifestIsOptInAndCompilableProjectIsInSolution`; and (b) ensure the hermetic SDK lifecycle example tests in `samples/Hexalith.Folders.Sample.Tests` actually execute in PR CI by adding that project to the baseline unit-test allow-list in `tests/tools/run-baseline-ci-gates.ps1` (today it is compiled by the solution build but not run as a test step). Every C# code block published in the docs must correspond to a compilable example or to real public types.

9. Add a **consumer-docs conformance test** at `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` (`namespace Hexalith.Folders.Contracts.Tests.Deployment`, `sealed partial class`, xUnit v3 + Shouldly + semantic YAML parsing). It must parse real artifacts and assert, with **exact-cardinality / inventory equality** (no vacuous passes): each required doc file exists; the rendered OpenAPI reference's operation + tag inventory equals the spine exactly (parse `hexalith.folders.v1.yaml`); the CLI reference exit-code rows equal the `FoldersExitCodes`/parity-oracle set; the MCP reference enumerates exactly **47 tools + 2 resources** and its failure-kind catalog equals the `parity-contract.yaml` `mcp_failure_kind` set + the 2 pre-SDK kinds; and every published doc/report is **metadata-only** (forbidden-token / absolute-path / non-`.invalid`-issuer scans). Negative controls must route through the **real parser/scanner** (not BCL tautologies — the Story 7.11 review finding): missing doc, wrong operation count, stale exit-code row, a leaked absolute path / token, and a malformed manifest entry must each fail the gate.

10. Add a focused **consumer-docs release-readiness gate** at `tests/tools/run-consumer-docs-gates.ps1` following the Epic 7 PowerShell posture (`#Requires -Version 7`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, repository-root resolution from the script path, `Push-Location`/`finally` `Pop-Location`, `$LASTEXITCODE` propagation, `utf8NoBOM` JSON, non-vacuous test-count guard, xUnit v3 in-process fallback for sandbox VSTest socket denial). It emits `_bmad-output/gates/consumer-docs/latest.json` (metadata-only, multi-category over the doc-surface set) with gate name, status, exit code, repository-relative inputs, per-surface category results, and `diagnostic_policy: 'metadata-only'`. **Wire it without broadening lanes:** add the static conformance step to `.github/workflows/contract-spine.yml` (the focused static-conformance lane that already hosts safety-invariant, governance-completeness, Dapr-policy, and production-observability gates) with `permissions: contents: read` only, and register `ConsumerDocsConformanceTests` in the `tests/tools/run-baseline-ci-gates.ps1` Contracts.Tests `--filter` so it is not inert in PR CI. Do NOT add a new top-level `ci.yml` lane, and do NOT add to `release-packages.yml`, `nightly-drift.yml`, or `policy-conformance.yml`. Keep `submodules: false` and root-level-only submodule init.

11. Keep everything **metadata-only and in scope**. Every doc, example, diagram, and gate report must contain only opaque synthetic identifiers (e.g. `folder_01HZY7Z6N7J4Q2X8Y9V0FLD001`), `.invalid` placeholder URLs, and bounded metadata — never secrets, bearer tokens, credential material, raw file contents, base64 file bytes, diffs, provider payloads, real issuer/audience values, production URLs, environment dumps, stack traces, tenant data, or local absolute paths. Stay strictly within the **consumer-reference** scope: do NOT publish operations-console / metadata-only audit documentation (Story 7.14), provider integration / canonical-error catalog documentation (Story 7.15), the NFR traceability bridge (Story 7.16), or ADRs / maintenance runbooks (Story 7.17). Do not hand-edit generated client files under `src/Hexalith.Folders.Client/Generated`, the OpenAPI Contract Spine, or the parity oracle to make a doc "fit" — fix the doc instead.

## Tasks / Subtasks

- [x] Publish the rendered OpenAPI reference (AC: 1, 11)
  - [x] Choose the consumer home (`docs/sdk/api-reference.md` recommended; alternatively `docs/api/openapi-reference.md`) and add the metadata-only banner + cross-links to the `docs/contract/*-contract-groups.md` authoring notes.
  - [x] Render/author the surface from `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`: base path `/api/v1`, the 9 tag groups, the 48 operationIds, the `oidcBearer` global security scheme, the mutating header triple, the query no-`Idempotency-Key` rule, the `ValidateProviderReadiness` POST-as-query exception, and the canonical Problem Details shape.
  - [x] Do not add Swagger/Redoc/`MapOpenApi` server middleware; the spine is the only source. Use only `.invalid` placeholder server/issuer values.

- [x] Complete the SDK quickstart (AC: 2, 11)
  - [x] Extend `docs/sdk/quickstart.md` with cross-links to the new API/CLI/MCP/auth references and lifecycle diagrams.
  - [x] Add a `HelperSchemaVersion` compatibility-gating note and confirm the idempotency-key, correlation/task-ID, and inline-vs-streamed sections still match the current public surface.
  - [x] Verify every code block compiles against real `Hexalith.Folders.Client` public types (no invented members).

- [x] Publish the SDK operation/API reference (AC: 3, 11)
  - [x] Enumerate `IClient` operations by tag group; document the 9-step golden lifecycle ordering.
  - [x] Record the generated idempotency-helper signature contract and the `(folderId, workspaceId, taskId)` parameter-order trap; cross-link `docs/contract/sdk-generation-and-idempotency-helpers.md`.
  - [x] Keep this content in exactly one home (dedicated file or folded into AC 1); cross-link, never duplicate.

- [x] Publish the CLI reference (AC: 4, 11)
  - [x] Author `docs/sdk/cli-reference.md`: tool name `folders` (dotnet tool), the 7 groups and all leaf subcommands, recursive global options, the 3-layer credential precedence (+ credentials.json shape, no real token), mutation vs query options, `--request` body sources, and the `commit create` naming quirk.
  - [x] Render the exit-code table `{0,64,65,66,67,68,69,70,71,72,73,74,75,1}` verbatim from `FoldersExitCodes.cs` + `Errors/ErrorProjection.cs` with the canonical category mapping per code.
  - [x] Keep all examples metadata-only (synthetic IDs; no tokens, base addresses, file contents, or absolute paths).

- [x] Publish the MCP tool/resource reference (AC: 5, 11)
  - [x] Author `docs/sdk/mcp-reference.md`: the 47 tools grouped by their 8 tool-type files, the 2 resources with URI templates, mutating/task-scoped markers, the result envelope, and stdio/stderr transport facts.
  - [x] Document the failure-kind catalog from `tests/fixtures/parity-contract.yaml` `mcp_failure_kind` (43) + `usage_error` + `credential_missing`, plus the `range_unsatisfiable → internal_error` drift note. Do not cite the architecture 13-row summary.
  - [x] Describe assembly attribute discovery (no `server-manifest.json`); `Hexalith.Folders.Mcp` is a standalone Exe sidecar, not a NuGet library.

- [x] Publish consumer auth guidance (AC: 6, 11)
  - [x] Author `docs/sdk/authentication.md`: reference (do not duplicate) the existing `BearerTokenHandler` pattern; cite the frozen S-2 OIDC parameters from `docs/exit-criteria/s2-oidc-validation.md`; document the `sub`/`eventstore:tenant`/`eventstore:permission` claim-provenance contract.
  - [x] Use only `.invalid` placeholder issuers/audiences; cross-link `docs/operations/production-identity-and-secrets.md`; document CLI/MCP credential-sourcing precedence.

- [x] Publish lifecycle diagrams (AC: 7, 11)
  - [x] Add `docs/diagrams/` Mermaid diagrams: workspace lifecycle + lock state (operator-disposition labels primary, C6 states secondary), file-operation -> commit flow, tenant/auth/ACL layered-authorization decision flow.
  - [x] Trace states/events to `docs/exit-criteria/c6-transition-matrix-mapping.md`; include no operation absent from the spine; metadata-only captions.

- [x] Make examples compile / be CI-validated (AC: 8, 11)
  - [x] Add any new compilable C# snippet(s) to `tests/tools/pattern-examples/` + `tests/fixtures/pattern-example-manifest.yaml` (satisfying `GovernanceCompletenessGateTests` manifest assertions).
  - [x] Add `samples/Hexalith.Folders.Sample.Tests` to the baseline unit-test allow-list in `tests/tools/run-baseline-ci-gates.ps1` so the hermetic lifecycle example tests run in PR CI (keep the project hermetic — no AppHost/live services).
  - [x] Confirm every published C# code block maps to a compilable example or real public types.

- [x] Add the consumer-docs conformance test (AC: 9, 11)
  - [x] Add `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` parsing the docs, the spine, `parity-contract.yaml`, `FoldersExitCodes.cs`/`ErrorProjection.cs`, and the manifest.
  - [x] Assert exact inventory equality (operation/tag set vs spine; 47 tools + 2 resources; failure-kind set; exit-code rows) and metadata-only content; add `.invalid`-only issuer scan.
  - [x] Add negative controls that route through the real parser/scanner: missing doc, wrong operation/tool count, stale exit-code row, leaked absolute path/token, malformed manifest entry.

- [x] Add the gate script and wire CI without broadening lanes (AC: 10, 11)
  - [x] Add `tests/tools/run-consumer-docs-gates.ps1` with the Epic 7 posture; emit metadata-only `_bmad-output/gates/consumer-docs/latest.json` with the non-vacuous count guard and xUnit v3 in-process fallback.
  - [x] Add a `Run consumer docs conformance gates` step to `.github/workflows/contract-spine.yml` (`pwsh`, `-SkipRestoreBuild`, `permissions: contents: read`).
  - [x] Register `ConsumerDocsConformanceTests` in the `run-baseline-ci-gates.ps1` Contracts.Tests `--filter`. Do not add a new `ci.yml`/release/scheduled lane; keep `submodules: false`.
  - [x] Check whether `docs/exit-criteria/c0-c13-governance-evidence.yaml` or a docs index needs a pointer to the new references; no existing hook maps to consumer-reference publishing, so no fabricated governance row was added.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false`.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore -m:1` (0 warnings/errors).
  - [x] Run `pwsh ./tests/tools/run-consumer-docs-gates.ps1 -SkipRestoreBuild` (status=passed, exit 0, 11 conformance facts).
  - [x] Run the focused `ConsumerDocsConformanceTests`.
  - [x] Run `samples/Hexalith.Folders.Sample.Tests` and the `tests/tools/pattern-examples` build via `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild`.
  - [x] Run `pwsh ./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild` (docs/examples pass redaction sentinels).
  - [x] Run the baseline Contracts.Tests filter and full baseline unit allow-list to confirm the new suite executes and is not inert.
  - [x] Run `git diff --check` and recursive-submodule scan to confirm no recursive submodule setup was introduced.
  - [x] Run `dotnet format whitespace` + `analyzers` over `src`/`tests`/`samples` through the baseline gate and confirm clean.

## Dev Notes

### Critical Scope Boundaries

- This is the **first of the Epic 7 documentation-publishing cluster** (7.13–7.17). 7.13 owns ONLY the **consumer references**: rendered OpenAPI reference, SDK quickstart + operation reference, CLI reference, MCP tool/resource reference, consumer auth guidance, lifecycle diagrams, and CI-validated examples. It is documentation + CI-gate wiring; it does not change runtime behavior of the surfaces themselves.
- **Explicit out-of-scope (do not author here):** operations-console workflows and metadata-only audit-field documentation → **Story 7.14**; provider integration / contract-testing guide and the canonical-error catalog with retryability/retry-after/client-action → **Story 7.15**; the PRD-NFR-to-evidence traceability bridge (`docs/exit-criteria/nfr-traceability.md`) → **Story 7.16**; ADRs and maintenance runbooks → **Story 7.17**. If a needed cross-link points at one of those, link to the surface/source, don't write the other story's deliverable.
- **Documentation reflects, never redefines, the contracts.** The OpenAPI Contract Spine (C0), the parity oracle (C13, `tests/fixtures/parity-contract.yaml`), `FoldersExitCodes`, and the `CanonicalErrorCategory` enum are the authoritative sources. If an example or table conflicts with them, fix the doc — never the spine, the oracle, or generated clients.
- **No doc-site generator, no server OpenAPI middleware.** All published docs are plain markdown under `docs/`. Do not introduce DocFX/MkDocs/Sphinx (the only `docfx.json` in the tree is inside the `Hexalith.FrontComposer` submodule and is off-limits), and do not add Swagger UI / Redoc / `MapOpenApi` server middleware. Lifecycle diagrams are Mermaid fenced blocks; the rendered OpenAPI reference is derived from the spine YAML.
- **Metadata-only is non-negotiable** across every doc, example, diagram caption, and gate report (see project-context "Critical Don't-Miss Rules"). Opaque synthetic identifiers and `.invalid` placeholder URLs only.
- **Do not initialize nested submodules recursively.** Allowed setup command is root-level only:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

### Current State To Preserve

- **OpenAPI spine:** `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` is OpenAPI 3.1.0, ~370 KB, with **9 tag groups** and the named operationIds (47 canonical operations mirrored by the SDK/CLI/MCP surfaces, plus any ops-console example operations — parse the spine for the exact set); the single security scheme is `oidcBearer` (`type: openIdConnect`, `openIdConnectUrl: https://oidc.invalid/...` placeholder) applied globally; mutating ops use `Idempotency-Key`/`X-Correlation-Id`/`X-Hexalith-Task-Id`; queries must not accept `Idempotency-Key`; `ValidateProviderReadiness` is a non-mutating POST-as-query. Extension vocabulary lives in `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`. The **server has no OpenAPI-serving middleware** (no Swashbuckle/NSwag server, no `MapOpenApi`); NSwag is used only at build time in the Client project to generate the typed client. `docs/contract/contract-spine-ci-gates.md` notes server OpenAPI emission is still "reference-pending" — so the rendered reference must derive from the spine file, not from a running server.
- **SDK:** `docs/sdk/quickstart.md` exists (Story 5.1) and is largely complete. The generated client (`src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`, `IClient`) and generated idempotency helpers (`HexalithFoldersIdempotencyHelpers.g.cs`, `HelperSchemaVersion`) are **never hand-edited**. Convenience helpers live under `src/Hexalith.Folders.Client/Convenience/`. Inline transport boundary is **262144 bytes**; over-boundary throws `FileUploadStreamingRequiredException` (message must stay metadata-only — never disclose the byte limit). Helper parameter order follows the spine path declaration `(folderId, workspaceId, taskId)`.
- **CLI:** `src/Hexalith.Folders.Cli` (System.CommandLine 2.0.8) is a thin adapter over `IClient`. Tool name `folders`; 7 groups; recursive global options; 3-layer credential precedence; mutation vs query option split; `--request` accepts inline JSON / `@path` / `-`; the `commit` group's mutating verb is **`create`** (not `commit`) due to a 2.0.8 token collision; exit codes are the sysexits-style set `{0,64,65,66,67,68,69,70,71,72,73,74,75,1}` projected by `Errors/ErrorProjection.cs` and verified row-by-row against the parity oracle. `--output json` is filtered metadata-only (`contentBytes`/`inlineContent`/`streamDescriptor` stripped at any depth). No CLI reference markdown exists yet.
- **MCP:** `src/Hexalith.Folders.Mcp` (ModelContextProtocol 1.3.0) is a stdio Exe sidecar (`IsPackable=false`) wrapping `IClient`. **47 tools** discovered via `[McpServerTool]` assembly scanning (`RegistrationTests` pins the count), tool name = kebab-case of `operation_id`, grouped across 8 tool-type files; **2 resources** (`folder-tree`, `audit-trail`) via `[McpServerResource]` with URI templates. Failure kinds: 43 post-SDK (each = `CanonicalErrorCategory` name) in `Errors/FailureKindProjection.cs` + 2 pre-SDK (`usage_error`, `credential_missing`); `range_unsatisfiable` is intentionally absent from the oracle and maps to `internal_error`. Logging is **stderr-only** (stdout is the JSON-RPC channel). No MCP reference doc exists yet; there is **no** `server-manifest.json` (assembly discovery is the actual mechanism, despite the architecture source-tree sketch).
- **Examples / CI rails (reuse, don't reinvent):** `tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj` + `tests/fixtures/pattern-example-manifest.yaml` are already built by `run-governance-completeness-gates.ps1` and asserted by `GovernanceCompletenessGateTests.PatternExampleManifestIsOptInAndCompilableProjectIsInSolution` (target framework must match `global.json` `net10.0`; every entry needs `synthetic_data_only: true`, a valid `<!-- hexalith-example: ... -->` marker, and an existing `source_path`). `samples/Hexalith.Folders.Sample` + `samples/Hexalith.Folders.Sample.Tests` are in `Hexalith.Folders.slnx` and the format/lint gate already scopes `./samples/`, but `Sample.Tests` is **NOT** in the baseline CI unit-test allow-list (it is compiled, not run) — closing that gap satisfies "examples … validated by CI". `Sample.Tests` is hermetic (RecordingHandler, no live network/Dapr); keep it that way.
- **Auth sources:** `docs/exit-criteria/s2-oidc-validation.md` holds the frozen S-2 OIDC parameters and `.invalid` placeholder issuers/audiences; `docs/operations/production-identity-and-secrets.md` enumerates the five `Folders:Authentication` config keys (`Authority`, `MetadataAddress`, `ValidIssuer`, `Audience`, `RequireHttpsMetadata`). Claim provenance: `sub`=principal, `eventstore:tenant`=authoritative tenant, `eventstore:permission`=access gate.
- **Epic 7 release-readiness pattern (7.8–7.12):** sanitized/metadata-only artifact + focused PowerShell gate writing `_bmad-output/gates/<gate>/latest.json` + a `Deployment/*ConformanceTests.cs` + CI wiring into `contract-spine.yml` + `run-baseline-ci-gates.ps1` Contracts.Tests filter registration. The Contracts.Tests filter currently includes `ContractsSmokeTests`, `BaselineCiWorkflowConformanceTests`, `ReleasePackageConformanceTests`, `RetentionAndTenantDeletionConformanceTests`, `ProductionObservabilityConformanceTests` — append `ConsumerDocsConformanceTests`.

### Architecture Compliance

- **Diagram location:** architecture pins "Diagrams and renders in `docs/diagrams/`" — use it. OpenAPI 3.1 source stays in `src/Hexalith.Folders.Contracts/openapi/`. There is no architecture mandate for a `docs/api/` directory, so consolidating consumer references under `docs/sdk/` (alongside the existing quickstart) is acceptable and keeps the consumer hub in one place.
- **Adapter Parity Contract:** REST, SDK, CLI, and MCP are parallel transports over one canonical contract. The CLI exit-code map and MCP failure-kind map are 1:1 with canonical categories from the parity oracle; docs must mirror the oracle, never collapse or rename categories. The architecture's abridged inline summaries are illustrative — the authoritative vocabularies are `tests/fixtures/parity-contract.yaml` and the `CanonicalErrorCategory` enum.
- **Operator-disposition vocabulary (F-4):** UI and docs must use the operator-disposition labels (`auto-recovering`/`awaiting-human`/`terminal-until-intervention`/`degraded-but-serving`) as the primary lifecycle vocabulary; technical state names are secondary. Lifecycle diagrams must follow this.
- **C6 transition matrix:** the workspace lifecycle/lock diagrams must trace to `docs/exit-criteria/c6-transition-matrix-mapping.md`; every state/event pair must be a real transition or an explicit invalid-rejection — no invented states.
- **Authorization layering (S-3 / cross-cutting):** the auth/ACL decision diagram must show the contractual order — JWT validation → EventStore claim transform → tenant-access projection freshness → folder ACL → EventStore validator → Dapr deny-by-default — and the rule that authoritative tenant/principal come from authenticated context, not payload.
- **Sentinel redaction (non-negotiable):** docs examples, gate reports, and diagram captions are output channels subject to the metadata-only invariant; they must pass the safety-invariant sentinels (`tests/fixtures/audit-leakage-corpus.json`) — no secrets, tokens, file contents, diffs, provider payloads, absolute paths, or real issuer/audience values.
- **Verification Expectations NFR:** every NFR category needs at least one CI gate or release-validation path; the documentation/consumer-reference category is satisfied here by the new consumer-docs conformance gate + the example-compilation rails. Do not leave the "examples compile or are otherwise validated by CI" clause to a manual claim.
- **Repository configuration is authoritative:** .NET SDK `10.0.302`, central package management (no inline `Version`), xUnit v3 + Shouldly + YamlDotNet, PowerShell 7 gate scripts. The `pattern-examples` project's target framework must equal `global.json`.

### Previous Story Intelligence

- **7.12 / 7.11 / 7.10 / 7.9 / 7.8** established the Epic 7 release-readiness gate pattern reused here verbatim (focused PowerShell gate + `latest.json` + `Deployment/*ConformanceTests.cs` + `contract-spine.yml` step + baseline-filter registration). Mirror the gate-script posture from `tests/tools/run-production-observability-gates.ps1` and the conformance-test skeleton from `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProductionObservabilityConformanceTests.cs`.
- **7.11 HIGH review finding:** the conformance suite was omitted from the `run-baseline-ci-gates.ps1` Contracts.Tests `--filter`, leaving it inert in PR CI. Do not repeat — register `ConsumerDocsConformanceTests` in that filter.
- **7.11 review finding (negative controls):** conformance negative controls must route through the real parser/metadata scanner and exercise staleness in both directions — BCL tautologies are invalid. Apply to every `ConsumerDocsConformanceTests` negative control.
- **Sandbox VSTest socket denial** (`SocketException (13): Permission denied`) recurs; the gate script must carry the xUnit v3 in-process fallback and a non-vacuous test-count guard, as in `run-production-observability-gates.ps1` / `run-security-redaction-ci-gates.ps1` / `run-governance-completeness-gates.ps1`.
- **Lane discipline (7.9–7.12):** static-conformance gates belong in `contract-spine.yml` with `permissions: contents: read`; PR CI lanes (`ci.yml`), release publishing (`release-packages.yml`), and scheduled workflows stay untouched unless the story explicitly targets them. 7.13 does not.
- **Source-of-truth traps to avoid (from 5.2 / 5.3):** use `commit create` (not `commit commit`); use `unknown_provider_outcome` (the architecture summary misspells it as `provider_outcome_unknown`); the MCP failure-kind set is the full oracle column, not the architecture 13-row excerpt; the MCP server uses assembly discovery, not a `server-manifest.json`.
- **Recent Epic 7 cadence:** `b9aa762 feat(story-7.12)`, `b2be204 feat(story-7.11)`, `3b9fa9f feat(story-7.10)`, `23c70c6 feat(story-7.9)`, `7f29f80 feat(story-7.8)` — one consolidated release-readiness lane per story. Commit convention: `feat(story-7.13): Publish API, SDK, CLI, and MCP consumer references`.

### Project Structure Notes

- **Likely NEW files:**
  - `docs/sdk/api-reference.md` — rendered/derived OpenAPI 3.1 consumer reference (or `docs/api/openapi-reference.md` if `docs/api/` is preferred)
  - `docs/sdk/cli-reference.md` — CLI reference
  - `docs/sdk/mcp-reference.md` — MCP tool/resource + failure-kind reference
  - `docs/sdk/authentication.md` — consumer auth guidance
  - `docs/diagrams/workspace-lifecycle.md`, `docs/diagrams/file-commit-flow.md`, `docs/diagrams/auth-acl-decision-flow.md` — Mermaid lifecycle diagrams
  - `tests/tools/run-consumer-docs-gates.ps1` — focused gate script
  - `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` — static conformance suite
  - one or more `tests/tools/pattern-examples/*.cs` compilable snippet files (only if new compilable C# examples are introduced)
  - `_bmad-output/gates/consumer-docs/latest.json` — generated by local/CI validation
- **Likely UPDATE files:**
  - `docs/sdk/quickstart.md` — cross-links + `HelperSchemaVersion` note + surface re-verification
  - `tests/fixtures/pattern-example-manifest.yaml` — new compilable-csharp entries (synchronized with `GovernanceCompletenessGateTests`)
  - `tests/tools/run-baseline-ci-gates.ps1` — add `samples/Hexalith.Folders.Sample.Tests` to the unit-test allow-list **and** register `ConsumerDocsConformanceTests` in the Contracts.Tests filter
  - `.github/workflows/contract-spine.yml` — `Run consumer docs conformance gates` step
  - `docs/exit-criteria/c0-c13-governance-evidence.yaml` — only if an existing hook maps to consumer-reference publishing (verify first; do not fabricate approval status)
- **Do not touch:** generated client files under `src/Hexalith.Folders.Client/Generated`, the OpenAPI Contract Spine, `tests/fixtures/parity-contract.yaml`, the `Hexalith.FrontComposer` submodule (incl. its `docfx.json`), provider drift fixtures, Dapr policy YAML, or package metadata — unless a focused conformance failure proves one directly stale and in scope.
- **Directory decision:** prefer consolidating the four consumer reference docs under `docs/sdk/` (the existing consumer home) and the diagrams under the architecture-mandated `docs/diagrams/`. If you instead create `docs/api/`, keep it consistent and reflect the chosen paths in `ConsumerDocsConformanceTests` and the gate script. Do not scatter `docs/cli/` + `docs/mcp/` + `docs/api/` as separate one-file directories.

### Testing Requirements

- Use the repo-pinned .NET SDK `10.0.302` (`global.json`) and central package management; no inline package versions. New conformance tests use xUnit v3 + Shouldly + YamlDotNet and the existing `Deployment/*ConformanceTests.cs` helper patterns (`RepositoryPath` walker from `AppContext.BaseDirectory`, semantic YAML parsing, `[GeneratedRegex]`).
- `ConsumerDocsConformanceTests` must not pass vacuously: assert exact inventory equality (operation/tag set parsed from the spine; exactly 47 MCP tools + 2 resources; the failure-kind set; the exit-code table sourced from `FoldersExitCodes`/`ErrorProjection`) and include a test-count guard in the gate script. Negative controls must run through the real parser/scanner and fail on: missing doc, wrong count, stale exit-code/failure-kind row, leaked absolute path / token / non-`.invalid` issuer, and a malformed manifest entry.
- Example validation: new compilable snippets compile via the `pattern-examples` project (governance gate), and `samples/Hexalith.Folders.Sample.Tests` runs hermetically in baseline CI. Keep sample tests free of AppHost/Dapr/Keycloak/Redis/network and credentials; live execution stays opt-in behind `FOLDERS_BASE_ADDRESS`.
- Gate-script diagnostics are metadata-only and fail closed on unsafe values; the report goes to `_bmad-output/gates/consumer-docs/latest.json` with `utf8NoBOM` and `diagnostic_policy: 'metadata-only'`.
- Scaffold/unit/contract/governance/safety gates must run without provider credentials, tenant seed data, secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, network calls, or nested submodule initialization. If VSTest sockets are denied in the sandbox, use the xUnit v3 in-process fallback and record the limitation.
- After implementing, the new docs/examples must pass `run-safety-invariant-gates.ps1` (redaction sentinels over the new output channels) and `dotnet format whitespace`/`analyzers` must be clean.

### Latest Technical Notes

- **OpenAPI 3.1 rendering without a server:** the spine is the single source. A hermetic, drift-proof approach is a hand-authored/derived **markdown** reference whose operation + tag inventory is asserted equal to the parsed spine by the conformance test (so the doc cannot silently drift). If a richer HTML render is later wanted, a Redocly/Redoc CLI step against the YAML is the conventional tool — but it adds an external/network tool dependency that conflicts with the hermetic PR-gate posture, so keep it out of the blocking gate.
- **Mermaid** state diagrams (`stateDiagram-v2`) render natively on GitHub from fenced ```mermaid blocks — no build step, no external asset. Use operator-disposition labels as the primary node labels.
- **System.CommandLine 2.0.8:** option/command names are the contract for the CLI reference; extract them from source, not from running the binary (no help-text-extraction infrastructure exists). The `commit create` collision workaround is intentional.
- **ModelContextProtocol 1.3.0 (.NET):** tools/resources are discovered from assembly attributes (`WithToolsFromAssembly`/`WithResourcesFromAssembly`); the reference should describe discovery, not a manifest file. stdio transport ⇒ stdout is JSON-RPC, logs go to stderr.
- **dotnet tool packaging:** the CLI ships as a tool (`PackAsTool=true`, `ToolCommandName=folders`); the MCP ships as a standalone executable sidecar (`IsPackable=false`). State each distribution shape correctly. Verify exact public surfaces against the pinned packages before writing examples (see `latest_technical_sources`).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.13`] - Story statement and BDD acceptance criteria (lines 1627–1638); adjacent 7.14–7.17 scope boundaries.
- [Source: `_bmad-output/planning-artifacts/prd.md#API-Documentation`] - Consumer documentation deliverable list (OpenAPI reference, auth/ACL guide, lifecycle diagrams, CLI/MCP/SDK references); provider/error catalog and ops/audit docs deferred to 7.14/7.15.
- [Source: `_bmad-output/planning-artifacts/architecture.md`] - `docs/diagrams/` diagram location (~L1454); Adapter Parity Contract (CLI exit-code / MCP failure-kind mapping); F-4 operator-disposition vocabulary; S-2/S-3 auth; OpenAPI 3.1 in contracts.
- [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`] - Contract Spine: 9 tag groups, 48 operationIds, `oidcBearer` scheme, header rules, Problem Details shape (rendered-reference source of truth).
- [Source: `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`] - `x-hexalith-*` extension vocabulary referenced by the reference.
- [Source: `tests/fixtures/parity-contract.yaml`] - Authoritative `cli_exit_code` and `mcp_failure_kind` mappings per operation; the failure-kind and exit-code reference tables must equal this oracle.
- [Source: `src/Hexalith.Folders.Cli/FoldersExitCodes.cs`, `src/Hexalith.Folders.Cli/Errors/ErrorProjection.cs`] - Canonical exit-code constants and category→exit-code projection for the CLI reference.
- [Source: `src/Hexalith.Folders.Cli/CliApplication.cs`, `Commands/*`, `GlobalOptionsBinding.cs`, `Credentials/CredentialResolver.cs`] - CLI command tree, global options, credential precedence, `commit create` naming.
- [Source: `src/Hexalith.Folders.Mcp/Tools/*.cs`, `Resources/FolderTreeResource.cs`, `Resources/AuditTrailResource.cs`, `Errors/FailureKindProjection.cs`, `Program.cs`] - 47 tools, 2 resources, failure-kind catalog, stdio/stderr transport, assembly discovery for the MCP reference.
- [Source: `docs/sdk/quickstart.md`] - Existing SDK quickstart to complete; metadata-only example style and synthetic-identifier convention.
- [Source: `docs/contract/sdk-generation-and-idempotency-helpers.md`, `docs/contract/idempotency-and-parity-rules.md`] - Hash format, `HelperSchemaVersion`, helper parameter-order trap, replay/conflict semantics to cross-link.
- [Source: `docs/contract/tenant-folder-provider-repository-contract-groups.md`, `workspace-lock-contract-groups.md`, `file-context-contract-groups.md`, `commit-status-contract-groups.md`, `audit-ops-console-contract-groups.md`, `contract-spine-foundation.md`] - Operation-group authoring notes the rendered reference must stay consistent with and cross-link.
- [Source: `docs/exit-criteria/s2-oidc-validation.md`] - Frozen S-2 OIDC parameters and `.invalid` placeholder issuers/audiences for the auth guide.
- [Source: `docs/operations/production-identity-and-secrets.md`] - `Folders:Authentication` config keys; production token-acquisition cross-link.
- [Source: `docs/exit-criteria/c6-transition-matrix-mapping.md`] - Canonical C6 states/events for lifecycle diagrams.
- [Source: `samples/Hexalith.Folders.Sample/FolderLifecycleSample.cs`, `samples/Hexalith.Folders.Sample.Tests/FolderLifecycleSampleTests.cs`] - Canonical 9-step lifecycle and the hermetic example tests to run in CI.
- [Source: `tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj`, `tests/fixtures/pattern-example-manifest.yaml`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs`] - Compilable-examples rail and the manifest assertions to satisfy.
- [Source: `tests/tools/run-baseline-ci-gates.ps1`] - Unit-test allow-list (add Sample.Tests) and Contracts.Tests filter (register `ConsumerDocsConformanceTests`).
- [Source: `tests/tools/run-production-observability-gates.ps1`, `tests/tools/run-governance-completeness-gates.ps1`, `tests/tools/run-security-redaction-ci-gates.ps1`] - Gate-script posture, `latest.json` schema, non-vacuous count guard, xUnit v3 in-process fallback, metadata-only assertions.
- [Source: `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProductionObservabilityConformanceTests.cs`] - Conformance-test skeleton (semantic parsing, exact cardinality, forbidden-token/absolute-path/recursive-submodule scans, negative controls).
- [Source: `.github/workflows/contract-spine.yml`, `.github/workflows/ci.yml`] - Correct static-conformance lane and minimal-permissions posture; lane separation (no new `ci.yml`/release/scheduled lane).
- [Source: `_bmad-output/implementation-artifacts/7-12-wire-production-observability-and-alerts.md`, `7-11-...md`, `7-9-...md`, `5-1-...md`, `5-2-...md`, `5-3-...md`] - Epic 7 gate pattern + baseline-filter lesson + negative-control discipline; SDK/CLI/MCP surface authority and traps.
- [Source: `_bmad-output/project-context.md`] - Metadata-only, zero cross-tenant leakage, central package management, generated-artifact rules, root-level submodules only, docs under `docs/`.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (1M context) with Codex parent recovery for final baseline documentation/report synchronization.

### Debug Log References

- 2026-05-31: Created story context via the BMAD `bmad-create-story` workflow (YOLO) for Story 7.13. Inputs: sprint status, Epic 7 BDD (`epics.md` Story 7.13 + 7.14–7.17 boundary), architecture (`docs/diagrams/` location, Adapter Parity Contract, F-4 operator-disposition, S-2/S-3 auth), PRD API-documentation deliverables, root + submodule project contexts, and Story 7.12/7.11/5.1/5.2/5.3 intelligence.
- 2026-05-31: Exhaustive artifact analysis fanned out across seven parallel research agents (REST/OpenAPI, SDK, CLI, MCP, examples-CI/publishing, architecture/epic/exit-criteria, previous-story conventions). Findings reconciled into the decomposed ACs and dev notes above. Confirmed: no server OpenAPI middleware and no doc-site generator (render from the spine; Mermaid diagrams); 9 tag groups / 48 operationIds; 47 MCP tools + 2 resources; CLI `commit create` quirk and the `{0,64,65,...,75,1}` exit-code table; authoritative CLI/MCP vocabularies live in `tests/fixtures/parity-contract.yaml`; `docs/sdk/quickstart.md` already exists (extend, don't rewrite); examples-validation rails are `tests/tools/pattern-examples` (built by the governance gate) and `samples/Hexalith.Folders.Sample.Tests` (compiled but not yet run in baseline CI).
- 2026-05-31: Implemented the consumer reference set from source-of-truth inventories: spine-derived API/SDK reference, CLI reference from command/exit-code sources, MCP reference from tool/resource/failure-kind sources, auth guide from S-2 and bearer-handler sources, and Mermaid lifecycle diagrams under `docs/diagrams/`.
- 2026-05-31: Added `ConsumerDocsConformanceTests` and `run-consumer-docs-gates.ps1`; fixed the absolute-path scanner so `https:/` URL schemes are not mistaken for Windows drive paths while real local paths still fail the metadata-only gate.
- 2026-05-31: Recovery fix after focused baseline filter failure: adding `samples/Hexalith.Folders.Sample.Tests` to the baseline allow-list required synchronizing the committed `_bmad-output/gates/baseline-ci/latest.json` and `docs/operations/baseline-ci-gates.md`. Regenerated the baseline report through the gate script.
- 2026-05-31: Code-review workflow fallback completed locally after the Claude review session hit the known workflow-script parse blocker. Verified all review fixes and release-readiness gates before syncing story and sprint status to done.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Published the consumer API/SDK, CLI, MCP, auth, and lifecycle-diagram references under `docs/sdk/` and `docs/diagrams/`, keeping all examples metadata-only and cross-linked from the quickstart.
- Added a compile-checked golden lifecycle pattern example and registered it in the pattern-example manifest; added hermetic sample tests to the baseline CI unit-test allow-list.
- Added a focused consumer-docs gate and exact-inventory conformance suite with negative controls over missing docs, stale counts, stale exit rows, unsafe metadata, and malformed manifest entries.
- **QA automation guardrail:** Expanded `ConsumerDocsConformanceTests` from 11 to 22 facts to assert deeper source-backed API security/header/error-shape details, SDK lifecycle/idempotency-helper traps, CLI/MCP/auth guidance, and lifecycle diagram bodies/edges against C6 and the spine; generated the Story 7.13 automation summaries.
- **Review fixes:** Corrected POST-as-query wording so the four context-query POSTs are not hidden behind the `ValidateProviderReadiness` exception, clarified `taskId` as optional `ProblemDetails` evidence rather than a required spine property, aligned the auth audience placeholder with S-2 (`api://hexalith-folders-production.invalid`), rebuilt the workspace lifecycle diagram from the architecture C6 transition matrix, and raised the consumer-docs gate anti-vacuity floor to all 22 facts.
- Wired the consumer-docs gate into `contract-spine.yml` and registered the conformance suite in the baseline Contracts.Tests filter without broadening lanes.
- Verified no governance-evidence or docs-index hook currently maps to consumer-reference publishing, so no fabricated governance row was added.
- Verification passed: restore, full solution build, consumer-docs gate, governance-completeness gate, safety-invariant gate, baseline gate allow-list, diff whitespace check, and recursive-submodule scan.
- Final review verification passed: Contracts.Tests build, 22/22 consumer-docs conformance facts, consumer-docs gate, pattern-examples build, baseline CI workflow conformance, baseline CI gate, safety-invariant gate, governance-completeness gate, `git diff --check`, and recursive-submodule scan.

### File List

- .github/workflows/contract-spine.yml (modified - consumer docs conformance gate step)
- _bmad-output/gates/baseline-ci/latest.json (modified - regenerated baseline report with Sample.Tests allow-list entry)
- _bmad-output/gates/consumer-docs/latest.json (new - metadata-only consumer docs gate report)
- docs/operations/baseline-ci-gates.md (modified - Sample.Tests allow-list and focused Contracts.Tests filter documented)
- docs/sdk/quickstart.md (modified - consumer reference links and `HelperSchemaVersion` guidance)
- docs/sdk/api-reference.md (new - spine-derived API/SDK reference)
- docs/sdk/cli-reference.md (new - CLI command/options/exit-code reference)
- docs/sdk/mcp-reference.md (new - MCP tools/resources/failure-kind reference)
- docs/sdk/authentication.md (new - consumer auth and claim-provenance guidance)
- docs/diagrams/workspace-lifecycle.md (new - C6/operator-disposition lifecycle diagram)
- docs/diagrams/file-commit-flow.md (new - file operation to commit flow)
- docs/diagrams/auth-acl-decision-flow.md (new - layered authorization decision flow)
- tests/Hexalith.Folders.Contracts.Tests/Deployment/BaselineCiWorkflowConformanceTests.cs (modified - Sample.Tests baseline allow-list assertion)
- tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs (new - static conformance and negative controls)
- tests/fixtures/pattern-example-manifest.yaml (modified - golden lifecycle pattern example entry)
- tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj (modified - Client project reference)
- tests/tools/pattern-examples/ConsumerGoldenLifecycleExample.cs (new - compile-checked golden lifecycle ordering example)
- tests/tools/run-baseline-ci-gates.ps1 (modified - Sample.Tests allow-list and ConsumerDocs filter registration)
- tests/tools/run-consumer-docs-gates.ps1 (new - focused consumer docs release-readiness gate)
- _bmad-output/implementation-artifacts/tests/7-13-test-summary.md (new - durable QA automation summary)
- _bmad-output/implementation-artifacts/tests/test-summary.md (modified - latest QA automation summary points to Story 7.13)

### Change Log

| Date       | Version | Description                                   | Author |
| ---------- | ------- | --------------------------------------------- | ------ |
| 2026-05-31 | 0.1     | Initial story context created (ready-for-dev) | BMAD create-story |
| 2026-05-31 | 1.0     | Published consumer references, diagrams, examples validation, conformance gate, CI wiring, and verification evidence; status set to review | BMAD dev-story |
| 2026-05-31 | 1.1     | Expanded consumer-docs conformance guardrail from 11 to 21 facts and wrote QA automation summaries | BMAD qa-automate |
| 2026-05-31 | 1.2     | Fixed review findings in API/auth docs, C6 lifecycle diagram edges, and consumer-docs gate anti-vacuity floor; conformance now covers 22 facts | BMAD code-review |
| 2026-05-31 | 1.3     | Completed review fallback, recorded senior review evidence, and synced story status to done | BMAD code-review |

## Senior Developer Review (AI)

Reviewer: Codex parent review fallback (after Claude review workflow parse failure)
Date: 2026-05-31
Outcome: Approved after fixes. No Critical issues remain.

### Findings Fixed

- **High:** API reference described only `ValidateProviderReadiness` as a POST-as-query operation, which hid the context-query POSTs. Fixed `docs/sdk/api-reference.md` to list all read-only POST-as-query operations.
- **Medium:** API reference presented `taskId` as part of the required Problem Details spine shape. Fixed `docs/sdk/api-reference.md` to clarify `taskId` is optional metadata-only task evidence.
- **Medium:** Auth guide audience placeholder drifted from the S-2 placeholder. Fixed `docs/sdk/authentication.md` to use `api://hexalith-folders-production.invalid`.
- **High:** Workspace lifecycle diagram edges did not fully match the architecture C6 transition matrix. Rebuilt `docs/diagrams/workspace-lifecycle.md` from the matrix and added an edge-level conformance fact.
- **Medium:** The consumer-docs gate anti-vacuity floor lagged behind the expanded test suite. Fixed `tests/tools/run-consumer-docs-gates.ps1` to require all 22 consumer-docs facts.

### Review Verification

- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1`
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests' --logger 'console;verbosity=minimal'` (22 passed)
- `pwsh ./tests/tools/run-consumer-docs-gates.ps1 -SkipRestoreBuild` (22 passed)
- `dotnet build tests/tools/pattern-examples/Hexalith.Folders.PatternExamples.csproj -m:1 -p:NuGetAudit=false`
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-build --filter 'FullyQualifiedName~Hexalith.Folders.Contracts.Tests.Deployment.BaselineCiWorkflowConformanceTests' --logger 'console;verbosity=minimal'` (7 passed)
- `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild`
- `pwsh ./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild`
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild`
- `git diff --check`
- Recursive-submodule scan over `.github`, `tests/tools`, `docs`, and this story file

## Story Creation Clarifications

These decisions were made with sensible defaults during context creation (YOLO, no user input). The dev agent should confirm or adjust during implementation:

1. **Consumer-reference doc location** — defaulted to consolidating `api-reference`, `cli-reference`, `mcp-reference`, and `authentication` under `docs/sdk/` (the existing consumer home), with diagrams under the architecture-mandated `docs/diagrams/`. `docs/api/` is the alternative for the OpenAPI reference; keep the choice consistent across the docs, the conformance test, and the gate.
2. **Rendered OpenAPI reference form** — defaulted to a spine-derived markdown reference whose inventory is asserted against the parsed spine (hermetic, drift-proof). A Redoc/Redocly static-HTML render is acceptable but must stay out of the blocking PR gate (external/network tool).
3. **Examples-validation wiring** — defaulted to reusing the `pattern-examples` project (governance gate) for new compilable snippets and adding `samples/Hexalith.Folders.Sample.Tests` to the baseline unit allow-list, rather than adding a 5th `ci.yml` job. The static `ConsumerDocsConformanceTests` follows the Epic 7 `contract-spine.yml` pattern.
4. **Release-gate prerequisite** — left consumer-docs OUT of the `release-packages.yml` evidence set (the AC does not require it). Promote it to a release prerequisite only if release governance later demands documentation completeness before publishing.
5. **Governance evidence row** — `c0-c13-governance-evidence.yaml` is the C0–C13 set and has no documentation criterion today; defaulted to NOT forcing a row. Add/update one only if an existing hook maps to consumer-reference publishing; do not fabricate approval status.
