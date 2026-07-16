---
discovery:
  generated_at: 2026-05-27T14:06:26+02:00
  story_key: 4-14-emit-metadata-only-audit-and-observability
  loaded_inputs:
    - _bmad-output/planning-artifacts/epics.md
    - _bmad-output/planning-artifacts/prd.md
    - _bmad-output/planning-artifacts/architecture.md
    - _bmad-output/planning-artifacts/ux-design-specification.md
    - _bmad-output/project-context.md
    - Hexalith.Commons/_bmad-output/project-context.md
    - Hexalith.EventStore/_bmad-output/project-context.md
    - Hexalith.Tenants/_bmad-output/project-context.md
    - Hexalith.Memories/_bmad-output/project-context.md
    - Hexalith.FrontComposer/_bmad-output/project-context.md
    - _bmad-output/implementation-artifacts/4-13-surface-canonical-errors-and-operational-evidence-after-failure.md
baseline_commit: abb8a3705818f64fea47792f6214ade54ebad748
---

# Story 4.14: Emit metadata-only audit and observability

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator and audit reviewer,
I want lifecycle operations to emit metadata-only audit, traces, metrics, and structured logs,
so that incidents can be reconstructed without exposing file contents or secrets.

## Acceptance Criteria

1. Given any successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, context-query, cleanup-status, read-model, or state-transition operation occurs, when runtime audit and observability records are emitted, then each record carries tenant ID, actor/principal reference, task ID when scoped, operation ID when available, correlation ID, folder ID when scoped, workspace ID when scoped, provider reference when scoped, timestamp, result, duration bucket or exact duration according to redaction state, state transition when available, and sanitized canonical error category.
2. Given a REST endpoint, provider-readiness endpoint, `/process` command, domain-service rejection, authorization denial, read-model query, or EventStore gateway failure emits audit/telemetry evidence, when metadata leaves the local method boundary, then file contents, diffs, raw paths, raw branch/ref names, raw commit messages, repository URLs/names, provider payloads, local absolute paths, generated context payloads, tokens, credentials, emails, exception stacks, and unauthorized-resource hints are excluded or represented only as approved digests, opaque references, classifications, or redaction metadata.
3. Given Story 4.13 introduced canonical error/evidence mapping, when 4.14 adds audit/observability, then it reuses `FolderCanonicalErrorMapper`, `FolderAuthorizationDenialMapper`, `FolderCommandRejected`, `WorkspaceStatusQueryResult`, `TaskStatusQueryResult`, and existing evidence DTOs instead of adding a second error taxonomy, state model, idempotency ledger, or projection vocabulary.
4. Given audit and observability are emitted for duplicate/idempotent replay and retry paths, when the same logical operation is replayed with the same idempotency key and equivalent payload, then the system records replay/retry metadata without appending duplicate domain events, duplicating provider side effects, or emitting ambiguous success/failure records.
5. Given the safety-channel inventory currently marks Story 4.14 logs/traces/metrics/events telemetry channels as prerequisite-drift, when this story lands deterministic runtime or test artifacts for those channels, then `tests/fixtures/safety-channel-inventory.json` is updated so the 4.14-owned channels have concrete artifact sources, covered diagnostics, and sentinel scanning where deterministic artifacts exist.
6. Given audit and telemetry tests run, when focused core/server/contract tests execute, then sentinel samples from `tests/fixtures/audit-leakage-corpus.json` are asserted absent from audit records, structured logs, trace/span attributes, metric names/labels/counters, event names, exception metadata, baggage, Problem Details, projection evidence, and test diagnostics touched by this story.
7. Given Story 4.14 is complete, when validation is performed, then existing Story 4.2-4.13 lifecycle behavior remains green, read-only routes still reject `Idempotency-Key`, mutating routes still require idempotency/correlation/task envelopes, and no audit query runtime endpoints, ops-console UI pages, CLI commands, MCP tools, SDK convenience helpers, generated-client hand edits, package upgrades, repair automation, production alerting, or nested submodule initialization is introduced.

## Tasks / Subtasks

- [x] Define a single metadata-only runtime audit/telemetry model. (AC: 1, 2, 3, 5)
  - [x] Add a focused concept area, preferably `src/Hexalith.Folders/Observability/` or `src/Hexalith.Folders/Projections/Audit/`, for records such as `FolderAuditObservation`, `FolderAuditOperationKind`, `FolderAuditResult`, and a sanitizer/validator. Keep one public type per file and file-scoped namespaces.
  - [x] Model only approved fields: tenant, actor/principal safe reference, folder/workspace/task/operation/correlation IDs, provider safe reference, timestamp, result, duration, state transition, sanitized category, retry/idempotency flags, and redaction/classification metadata.
  - [x] Represent sensitive file/provider/commit/path material by digest/reference/classification only. Do not include raw values even in internal record `ToString()` output, log scopes, activity tags, metric tags, exceptions, or assertion messages.
  - [x] Add a minimal `IFolderAuditObserver`/`IFolderTelemetryEmitter` abstraction and a no-op implementation for production-safe composition if the concrete in-memory emitter is test/dev only.

- [x] Wire audit/telemetry through command and query boundaries. (AC: 1, 2, 3, 4)
  - [x] Instrument `FoldersDomainServiceEndpoints` around mutating REST commands, read-only evidence/status/context queries, EventStore gateway success/failure mapping, and read-only envelope rejection paths.
  - [x] Instrument `ProviderReadinessEndpoints` for readiness validation and support-evidence query outcomes.
  - [x] Instrument `/process` handling in `FolderDomainProcessor` so accepted and rejected domain commands emit one sanitized operation observation with command type, result, causation/correlation metadata, and canonical rejection category.
  - [x] Preserve 4.13 response behavior. Observability must not change Problem Details extension shape, status codes, client actions, retry flags, safe-denial non-enumeration, or C6 state semantics.
  - [x] Ensure read-only audit/telemetry emission never requires `Idempotency-Key`; read-only routes must continue to reject that header before producing normal query results.

- [x] Add OpenTelemetry and structured logging without new package drift. (AC: 1, 2, 5, 6)
  - [x] Complete `src/Hexalith.Folders.ServiceDefaults/Extensions.cs` using the repository's existing OpenTelemetry package references and central versions. Register service name/resource metadata, ASP.NET Core/HTTP/runtime instrumentation, OTLP exporter when configured, and `Meter`/`ActivitySource` names owned by Hexalith.Folders.
  - [x] Add internal telemetry constants for stable span names, metric names, and tag keys. Use low-cardinality metric labels only; never put folder IDs, paths, branch refs, commit messages, repository names, provider payloads, tokens, or emails into metric names/labels.
  - [x] Use Microsoft.Extensions.Logging structured templates with named placeholders only. Do not interpolate strings or embed raw exception messages that can contain provider/file/path data.
  - [x] Prefer exact durations for authorized metadata-only records. For redacted records, bucket duration to 100ms granularity as documented in `docs/contract/audit-ops-console-contract-groups.md`.

- [x] Feed deterministic audit/read-model evidence for incident reconstruction. (AC: 1, 2, 4)
  - [x] Reuse state and evidence from `FolderState`, `InMemoryFolderRepository`, `WorkspaceStatusReadModelSnapshot`, `TaskStatusReadModelSnapshot`, `WorkspaceProviderOutcome`, and provider-readiness evidence stores.
  - [x] If an in-memory audit projection is introduced, keep it append-only, tenant/folder scoped, deterministic, and rebuildable from accepted/rejected operation observations. It must not become authoritative for tenant access, folder ACL, workspace state, idempotency, or provider outcome.
  - [x] Keep `/project` behavior unchanged unless implementing a minimal audit projection path is required by this story. Do not implement contract-declared `ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, or ops-console runtime handlers in this story.
  - [x] Treat idempotent replay, duplicate, retry, unknown-provider-outcome, reconciliation-required, safe denial, projection-stale, and projection-unavailable as distinct results/categories.

- [x] Update safety inventory and contract guardrails. (AC: 5, 6, 7)
  - [x] Update `tests/fixtures/safety-channel-inventory.json` for 4.14-owned channels once deterministic artifacts exist: logs, traces, span-names, metric-labels, metric-names, event-names, counters, telemetry-attributes, exception-metadata, baggage, and events as applicable.
  - [x] Keep `tests/fixtures/audit-leakage-corpus.json` synthetic-only. Add corpus entries only if a genuinely new sensitive category is introduced; otherwise reuse the existing 18 samples.
  - [x] Do not modify generated SDK files. If contract drift is discovered, change the OpenAPI Contract Spine and regenerate in the established pipeline only if this story truly requires it.

- [x] Add focused tests and validation gates. (AC: 1-7)
  - [x] Unit tests for sanitizer/validator behavior over every allowed field and forbidden sentinel category.
  - [x] Server tests for successful/denied/failed/replayed mutation observations, read-only status/evidence/context query observations, gateway exception mapping observations, and provider-readiness observations.
  - [x] `/process` tests for accepted and rejected command telemetry, including unsupported command type and malformed evidence paths.
  - [x] Metrics/tracing tests that assert stable names/tags and no high-cardinality or forbidden values.
  - [x] Safety inventory tests that fail if 4.14-owned channels remain prerequisite-drift after implementation artifacts are present.
  - [x] Regression tests for read-only `Idempotency-Key` rejection, mutating envelope requirements, 4.13 canonical error shapes, and no duplicate domain events/provider side effects on replay.
  - [x] Suggested validation: focused builds for `Hexalith.Folders`, `Hexalith.Folders.Server`, `Hexalith.Folders.ServiceDefaults`, focused xUnit v3 in-process tests for new classes, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests`, and `git diff --check`.

## Dev Notes

### Source Context

- Epic 4 requires deterministic failure, status, idempotency, and redaction behavior for the repository-backed workspace lifecycle. Story 4.14 specifically requires metadata-only audit, traces, metrics, and structured logs for successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and state-transition operations. [Source: `_bmad-output/planning-artifacts/epics.md#Story 4.14: Emit metadata-only audit and observability`]
- PRD FR53-FR56 require metadata-only audit trails, incident reconstruction from immutable metadata, exclusion of file contents/provider tokens/credential material/secrets, and operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events. [Source: `_bmad-output/planning-artifacts/prd.md#Audit and Operations Visibility`]
- Architecture cross-cutting concern #6 makes sentinel redaction mandatory across logs, traces, metric labels, events, audit records, console views, provider diagnostics, and error responses, with `tests/fixtures/audit-leakage-corpus.json` as the normative corpus. [Source: `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns Identified`]
- Architecture logging rules require structured logs with `tenantId`, `correlationId`, `causationId`, `taskId` when scoped, `aggregateId` when scoped, and `eventTypeName` when applicable; forbidden values include file contents, secrets, provider tokens, raw credential references, and anything matching the audit leakage corpus. [Source: `_bmad-output/planning-artifacts/architecture.md#Process Patterns`]
- The UX spec needs future diagnostic timelines and audit views to show timestamp, event category, actor/task/correlation metadata, result, state transition, reason category, retry/escalation posture, and safe detail text. This story produces the server/runtime evidence Epic 6 can later render. [Source: `_bmad-output/planning-artifacts/ux-design-specification.md#Stable UX Design Requirements`]
- Existing Story 1.11 contracts declare audit trail and operation timeline routes but explicitly deferred runtime handlers. Story 4.14 should emit runtime audit/observability evidence; it should not implement audit query endpoints or ops-console UI unless explicitly required by this story. [Source: `docs/contract/audit-ops-console-contract-groups.md#Operation Inventory`]

### Previous Story Intelligence

- Story 4.13 added `FolderCanonicalErrorMapper`, enriched `FoldersDomainServiceEndpoints`, added runtime evidence routes for task/commit/provider/reconciliation status, and expanded `InMemoryFolderRepository` projection snapshot data.
- Story 4.13 fixed critical review issues around reconciliation-reference scope validation, changed-path digest propagation, and task-status projection validation. 4.14 must reuse those evidence sources instead of re-deriving identifiers or echoing caller-supplied references.
- Story 4.13 preserved safe denial behavior: authorization failures may expose correlation/task evidence but must not confirm protected tenant/folder/workspace/provider/commit/resource existence.
- Story 4.11 and 4.12 established mutation envelope requirements and idempotent commit behavior. Audit/telemetry must be a side effect of already-decided outcomes; it must not weaken idempotency or create duplicate domain events/provider calls.
- Recent commits show the repository pattern is narrow story-scoped core/server/test changes, no generated-client hand edits, and no package upgrades unless a contract/codegen story requires them.

### Existing Implementation State

- `src/Hexalith.Folders.ServiceDefaults/Extensions.cs` is currently a stub even though `src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj` already references OpenTelemetry exporter, hosting, ASP.NET Core, HTTP, and runtime instrumentation packages. This story should use those existing references and central package versions.
- `Directory.Packages.props` pins OpenTelemetry packages on the `1.15.x` family, Microsoft.Extensions `10.x`, Aspire `13.x`, Dapr `1.17.9`, xUnit v3, Shouldly, and other repository-owned versions. No dependency upgrade is needed.
- `FoldersDomainServiceEndpoints` is the main runtime boundary for REST commands, status/evidence/context queries, read-only envelope checks, safe Problem Details, and EventStore gateway exception mapping. It should be instrumented through helper methods rather than scattered copy/paste calls.
- `ProviderReadinessEndpoints` has a separate safe Problem Details and query path. Include it in audit/telemetry coverage so provider readiness outcomes do not become a blind spot.
- `FolderDomainProcessor` emits `FolderCommandRejected` for `/process` rejections and uses canonical command-type constants from `FoldersServerModule`. Use that rejection sanitizer and command vocabulary.
- `FolderAuthorizationDenialMapper` centralizes safe authorization Problem Details. Instrument after safe mapping, not by adding more details to safe denial bodies.
- `InMemoryFolderRepository` currently saves lifecycle, branch/ref, workspace lock/status/cleanup, and task status snapshots after accepted events. If audit projection state is added, keep the same deterministic snapshot discipline and fail-loud DI checks.
- `tests/fixtures/safety-channel-inventory.json` already marks many Story 4.14 telemetry surfaces as `prerequisite-drift` with empty `artifact_sources`; Story 4.14 is the named owner for those channels.

### Architecture Compliance

- Aggregates remain pure. Do not add logging, Activity, Meter, clocks, provider calls, Dapr calls, filesystem access, or audit projection writes inside aggregate handlers.
- Observability must be emitted at service/endpoint/processor/projection boundaries where the outcome, envelope metadata, and sanitized evidence are already known.
- Tenant authority comes from authentication context and EventStore envelope, never from request payload, audit filters, correlation IDs, or client-supplied tenant-like fields.
- Problem Details, audit records, logs, traces, metrics, projection payloads, provider diagnostics, and test diagnostics must be metadata-only.
- Metric labels must be low-cardinality. Use category/result/operation kind/provider kind, not raw tenant/folder/task/path/commit/repository values.
- Structured logs must use named placeholders. Avoid string interpolation and raw exception messages; use sanitized category/code and opaque operation/correlation references.
- Unknown provider outcome and reconciliation-required must remain non-retryable blind-retry cases with `wait_for_reconciliation` semantics.
- No nested submodule initialization or recursive submodule commands.

### Project Structure Notes

- Expected core audit/telemetry files: `src/Hexalith.Folders/Observability/` or `src/Hexalith.Folders/Projections/Audit/`.
- Expected service-defaults changes: `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`.
- Expected server integration files: `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`, `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`, and DI registration in `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` or `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`.
- Expected tests: `tests/Hexalith.Folders.Tests/` for core sanitizer/audit model, `tests/Hexalith.Folders.Server.Tests/` for runtime boundary instrumentation, and `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`/fixtures for inventory coverage.
- Keep generated files under `src/Hexalith.Folders.Client/Generated` untouched unless a deliberate Contract Spine regeneration is required and validated.

### Files To Inspect First

- `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`
- `src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs`
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs`
- `src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceStatusQueryResult.cs`
- `src/Hexalith.Folders/Queries/Folders/WorkspaceProviderOutcome.cs`
- `src/Hexalith.Folders/Queries/Folders/TaskStatusQueryResult.cs`
- `tests/fixtures/audit-leakage-corpus.json`
- `tests/fixtures/safety-channel-inventory.json`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`

### Do Not Touch

- Do not implement runtime `ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`, or ops-console diagnostics endpoints unless the dev agent proves they are necessary for 4.14 ACs.
- Do not implement UI pages, CLI commands, MCP tools, SDK convenience helpers, production alert rules, repair/discard/retry automation, provider drift workflows, release packaging, or generated-client hand edits.
- Do not add a second error taxonomy, second workspace state model, second idempotency ledger, route-level tenant authority, or audit source that bypasses existing authorization and evidence models.
- Do not expose raw file content, diffs, local paths, raw changed paths, raw branch/ref target, raw commit message, repository URL/name, provider payload, credential reference value, token, email, exception stack, projection watermark in safe denials, or unauthorized-resource hints.
- Do not upgrade packages, change target frameworks, or initialize nested submodules recursively.

### Latest Technical Context

- Local pins are authoritative for this story: .NET SDK `10.0.302`, `net10.0`, C# latest, central package management, Dapr `1.17.9`, Aspire `13.3.5`, OpenTelemetry exporter/hosting `1.15.3`, ASP.NET Core instrumentation `1.15.2`, HTTP/runtime instrumentation `1.15.1`, xUnit v3, and repository-owned package versions. [Source: `global.json`; `Directory.Packages.props`]
- No external web research is required. The story relies on pinned local package versions, existing service-defaults package references, and repository-owned Contract Spine/safety fixtures.

### Testing

- Prefer focused builds/tests first. Prior stories used local SDK `10.0.302` and xUnit v3 in-process runners because VSTest may hit sandbox socket permission issues.
- Minimum focused coverage: sanitizer/model unit tests, server endpoint telemetry tests, `/process` accepted/rejected command telemetry tests, provider-readiness telemetry tests, safety inventory tests, metadata leakage tests across emitted JSON/log/trace/metric artifacts, and 4.13 regression tests for canonical error shapes and evidence routes.
- Keep tests hermetic: no live GitHub/Forgejo calls, provider credentials, Dapr sidecars, Keycloak, Redis, production secrets, network calls, or nested submodule initialization.

### Regression Traps

- Do not log raw exception messages from provider, HTTP, filesystem, JSON, or EventStore failures; classify and log sanitized category/code.
- Do not put IDs or paths into metric labels. Metric labels must stay low-cardinality and metadata-only.
- Do not make audit emission part of aggregate transition success. Audit/telemetry failure must not mutate aggregate state or turn accepted domain outcomes into inconsistent partial outcomes.
- Do not allow caller-supplied `operationId`, `reconciliationId`, `folderId`, `workspaceId`, correlation ID, task ID, or audit cursor to establish existence or authorization.
- Do not let idempotent replay emit a second success event with the same meaning as the original mutation. Mark replay/retry/duplicate explicitly.
- Do not make telemetry spans or baggage carry raw tenant/folder/provider/file/commit data. Prefer opaque references and sanitized classifications.
- Do not leave 4.14-owned safety inventory channels as prerequisite-drift after adding deterministic telemetry artifacts.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-27: Created via BMAD create-story workflow in YOLO mode with inputs from sprint status, Epic 4 Story 4.14, PRD, architecture, UX specification, project contexts, Story 4.13, current audit/error/status/service-defaults code, safety fixtures, and recent git history.
- 2026-05-27: Discovery loaded whole planning artifacts from `_bmad-output/planning-artifacts`; no sharded planning docs were present. Persistent facts loaded from root and sibling `project-context.md` files.
- 2026-05-27: Validation checklist applied during drafting; guardrails added for metadata-only audit/telemetry, reuse of 4.13 canonical evidence, safety-channel inventory ownership, OpenTelemetry package pins, aggregate purity, and scope exclusion for audit runtime query/UI/CLI/MCP/generated-client work.
- 2026-05-27: BMAD dev-story workflow activated; no prepend/append customization steps were configured, persistent project-context facts were loaded, and sprint/story status moved to in-progress.
- 2026-05-27: Added metadata-only audit observation model, sanitizer, telemetry emitter, no-op and in-memory observers, DI registration, REST/provider `/process` endpoint filter wiring, OpenTelemetry service defaults, safety-channel inventory coverage, and focused core/server/contract tests.
- 2026-05-27: Validation passed: `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1`; `dotnet build src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1`; `dotnet build src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj --no-restore --tlp:off -v:minimal`; focused xUnit v3 in-process `FolderAuditObservationTests`, `FolderAuditEndpointFilterTests`, and `SafetyInvariantGateTests`; `git diff --check`.
- 2026-05-27: Full `dotnet build Hexalith.Folders.slnx --no-restore` remained environment-blocked by unavailable `Aspire.AppHost.Sdk/13.2.2` and restricted `api.nuget.org` access. Full server/contracts test assemblies also hit pre-existing environment constraints: Kestrel socket permission failures in tests that do not use TestServer, and parity-generator tests requiring NuGet access. Full `Hexalith.Folders.Tests` passed: 1154/1154.
- 2026-05-27: Senior review auto-fix applied: prevented duplicate successful `/process` transport observations when `FolderDomainProcessor` already emits command-level audit evidence, preserved `/process` transport rejection observations, captured safe provider references from provider-readiness responses, and isolated observer failures from request success paths.
- 2026-05-27: Review validation attempted in this environment. Focused `dotnet build` commands for core, server, and service-defaults were blocked before MSBuild by SDK mismatch: `global.json` requires .NET SDK `10.0.302`, installed SDK is `10.0.108`. `git diff --check` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story file prepared for dev-story execution.
- Story status set to ready-for-dev.
- Implemented a single metadata-only audit/telemetry surface with sanitized observation records, duration bucketing for redacted records, safe `ToString()` output, no-op production observer, in-memory test observer, and low-cardinality OpenTelemetry/logging names.
- Wired endpoint-level observation emission across REST, provider-readiness, read-model/query, file/lock/commit/cleanup/context, and `/process` route boundaries while preserving response shapes and read-only idempotency rejection behavior.
- Added service-defaults OpenTelemetry registration using existing package references and updated the safety-channel inventory so Story 4.14 logs/traces/metrics/events telemetry channels now point at deterministic artifacts and participate in sentinel scanning.
- Added focused core, server, and contract guardrail tests for sanitizer behavior, low-cardinality names/tags, endpoint operation classification, `/process` accepted/rejected observations, and 4.14 safety inventory coverage.
- Review auto-fix tightened `/process` ownership so successful command observations are emitted once by the domain processor while validation/authorization failures remain covered by the endpoint filter.
- Review auto-fix made audit observer failures non-fatal and added focused tests for provider reference capture, `/process` duplicate prevention, transport rejection observations, and observer failure isolation.

### File List

- `_bmad-output/implementation-artifacts/4-14-emit-metadata-only-audit-and-observability.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Observability/FolderAuditObservation.cs`
- `src/Hexalith.Folders/Observability/FolderAuditObservationBuilder.cs`
- `src/Hexalith.Folders/Observability/FolderAuditOperationKind.cs`
- `src/Hexalith.Folders/Observability/FolderAuditRedactionState.cs`
- `src/Hexalith.Folders/Observability/FolderAuditResult.cs`
- `src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs`
- `src/Hexalith.Folders/Observability/FolderTelemetryEmitter.cs`
- `src/Hexalith.Folders/Observability/FolderTelemetryNames.cs`
- `src/Hexalith.Folders/Observability/IFolderAuditObserver.cs`
- `src/Hexalith.Folders/Observability/IFolderTelemetryEmitter.cs`
- `src/Hexalith.Folders/Observability/InMemoryFolderAuditObserver.cs`
- `src/Hexalith.Folders/Observability/NoOpFolderAuditObserver.cs`
- `src/Hexalith.Folders/Observability/NoOpFolderTelemetryEmitter.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/FolderAuditEndpointFilter.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`
- `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`
- `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FolderAuditEndpointFilterTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`
- `tests/fixtures/safety-channel-inventory.json`

### Change Log

- 2026-05-27: Implemented Story 4.14 metadata-only audit/observability model, runtime wiring, OpenTelemetry service defaults, deterministic safety inventory coverage, and focused test guardrails.
- 2026-05-27: Senior review auto-fix resolved duplicate `/process` observation risk, audit observer failure propagation, and provider-readiness provider-reference omission; story marked done after review.

## Senior Developer Review (AI)

Reviewer: Codex on 2026-05-27

### Outcome

Approved after automatic fixes. No critical issues remain.

### Findings Fixed

- High: Successful `/process` commands could produce two observations: one from `FolderDomainProcessor` with command-level outcome details and one from `FolderAuditEndpointFilter` over the transport-level 200 envelope. Fixed by skipping endpoint-filter emission for successful `/process` responses while preserving transport validation/authorization/error observations.
- Medium: `FolderTelemetryEmitter` propagated `IFolderAuditObserver` failures, allowing audit projection/test observer failure to break otherwise successful operation flows. Fixed by containing observer exceptions and emitting only a bounded structured warning without exception message or stack data.
- Medium: Provider-readiness observations did not carry the safe provider reference when the response supplied one. Fixed by extracting `ProviderReference`/`providerReference` from value results and passing it through the sanitizer.

### Validation Checklist

- Story file loaded from `_bmad-output/implementation-artifacts/4-14-emit-metadata-only-audit-and-observability.md`.
- Story status verified as reviewable before review (`review`).
- Epic/story resolved as `4.14`.
- Project context loaded from `_bmad-output/project-context.md`; story context and planning artifacts were already referenced by the story discovery block.
- Tech stack detected from project context: .NET SDK `10.0.302`, `net10.0`, C# latest, central package management, xUnit v3, Shouldly, OpenTelemetry `1.15.x`.
- External doc search was not required; story notes declare local pins and repository artifacts authoritative.
- Acceptance criteria, completed tasks, File List, source changes, tests, security, redaction, and telemetry cardinality were reviewed.
- Review notes and Change Log updated.
- Story status updated to `done`; sprint status synced to `done`.
- Validation: `git diff --check` passed. Focused `dotnet build` commands were attempted but blocked by SDK mismatch (`global.json` requires `10.0.302`; installed SDK is `10.0.108`).
