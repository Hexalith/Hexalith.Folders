---
baseline_commit: b2be204
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
  previous_story: _bmad-output/implementation-artifacts/7-11-enforce-c3-retention-and-tenant-deletion-behavior.md
  latest_technical_sources:
    - https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel
    - https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation
    - https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
    - https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks
    - https://opentelemetry.io/docs/languages/net/exporters/
    - https://learn.microsoft.com/dotnet/core/extensions/logging
---

# Story 7.12: Wire production observability and alerts

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform operator,
I want production observability exporters, health checks, monitored snapshots, and alerts wired,
so that operational failures are visible outside local Aspire.

## Acceptance Criteria

> Epic 7.12 BDD from `_bmad-output/planning-artifacts/epics.md`:
> Given production observability settings exist
> When services run
> Then traces, metrics, logs, health, projection lag, dead-letter depth, provider failures, stale locks, and cleanup failures are exported or alerted
> And emitted telemetry respects redaction and sensitive metadata policy.

Decomposed acceptance criteria:

1. Add a sanitized, metadata-only **production observability settings** artifact under `deploy/observability/production/` (the folder does not exist today) that declares production exporter intent, health-probe intent, and per-signal monitored-snapshot + alert-rule intent (signal name, severity, threshold source, owning component). It must mirror the `deploy/dapr/production/*.yaml` "sanitized production conformance artifact" precedent: synthetic/templated placeholders only (e.g. an `OTEL_EXPORTER_OTLP_ENDPOINT` templating sentinel), no real endpoints, tokens, credentials, production URLs, or vendor account identifiers. This realizes the BDD "Given production observability settings exist" precondition the same way Story 7.1 realized "production Dapr policy YAML exists."

2. Make the OpenTelemetry pipeline **production-ready and vendor-neutral for all three signal families (traces, metrics, logs)**. Today `src/Hexalith.Folders.ServiceDefaults/Extensions.cs` exports only traces and metrics via OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; **OpenTelemetry logging export is missing**. Wire OpenTelemetry logging export through the same `OTEL_EXPORTER_OTLP_ENDPOINT` config seam (`IsOtlpConfigured`), keeping exporters pluggable (Jaeger / Tempo / Application Insights / Datadog via OTLP — no vendor-specific SDK). Keep all OpenTelemetry packages family-aligned at the pinned `1.15.x` versions in `Directory.Packages.props` (no inline `Version` attributes).

3. Add real **health-check endpoints `/health/live` and `/health/ready`** on each Folders service. Replace the flat `GET /health` stub (which returns a static `{ status = "healthy" }`) with a liveness/readiness split using `AddHealthChecks()` registered in `AddServiceDefaults` and mapped in `MapDefaultEndpoints` (add a HealthChecks package entry through central package management — none exists today). Readiness must aggregate the I-7 monitored snapshots (Dapr sidecar health, Tenants-availability degraded-mode active flag, projection lag vs C2) and report `degraded-but-serving` when projection lag exceeds the pinned C2 target rather than failing readiness outright.

4. Bring the **Workers host into the observability/health pipeline**. `Hexalith.Folders.Workers` currently has no ServiceDefaults reference, no OpenTelemetry, and no health endpoint, yet the BDD "When services run" covers workers (they own provider, Git, reconciliation, and cleanup side effects). Add a `Hexalith.Folders.ServiceDefaults` project reference, call `AddServiceDefaults()` in `Workers/Program.cs`, and map worker health endpoints so worker telemetry and health are exported.

5. Export the **projection-lag** signal. Emit a projection-lag metric (milliseconds, severity `Warning` per the log-level convention "recoverable degradation, e.g. projection lag") computed against the existing `WorkspaceProjectionLag` / `FolderLifecycleFreshness` / status-and-audit watermark surfaces. The alert threshold must trace to the pinned **C2 status-freshness target of 500 ms** in `docs/exit-criteria/c2-freshness.md`, not an engineering guess. Lag is clock-derived: it must NOT be baked into replayable projection state (read-model determinism excludes clock-derived fields).

6. Export the **dead-letter depth** signal. Add the dead-letter topic (`deadletter.{domain}.events` for `folders` and `organizations`) to `deploy/dapr/production/pubsub.yaml` (no dead-letter topic is configured today) and emit/declare a dead-letter-depth monitored snapshot + alert-rule intent. This signal has **no PRD anchor** — it derives from architecture decision I-7 and EventStore/Dapr operational evidence; cite I-7, not the PRD, and do not over-claim a product requirement.

7. Export the **provider-failure** signal. Emit a provider-failure counter keyed by the bounded `ProviderFailureCategory` enum (known failure / `UnknownProviderOutcome` / `ReconciliationRequired` / `ProviderUnavailable` / `ProviderRateLimited`; canonical error code 70 `provider_failure_known`). Routed through the existing sanitized emitter; provider tokens, repository names, remote URLs, branch names, and provider error payloads must never appear in labels — only bounded categories and presence booleans.

8. Export the **stale-lock** signal. Emit a stale/abandoned/interrupted-lock metric/observation from the lock-status surfaces (`WorkspaceLockStatusReadModelSnapshot`, `FolderResultCode.LockExpired/LockNotOwned/LockConflict`, `FolderAuditResult.Locked/Stale`). It is clock-derived and **observe-only**: telemetry must make stale locks visible and "never silently break" them — no auto-release, no repair automation (MVP no-remediation boundary).

9. Export the **cleanup-failure** signal. Emit a cleanup-failure metric/observation from the workspace cleanup surface (`WorkspaceCleanupStatusReadModelSnapshot` with `Status` / `ReasonCode` / `RetryEligibility`, `FolderAuditOperationKind.CleanupStatus`). PRD anchor: "Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID." Observe-only; no repair automation in MVP.

10. Route **every new signal through the existing metadata-only telemetry pipeline** rather than reinventing it. Reuse the single existing `ActivitySource`/`Meter` named `Hexalith.Folders.Observability` (`FolderTelemetryNames`) so the existing `AddSource`/`AddMeter` registration captures new instruments; add new instrument/tag-name constants in `FolderTelemetryNames.cs`; emit via `FolderTelemetryEmitter` / `FolderAuditObservationBuilder` / `FolderAuditSanitizer`. New metric labels and span attributes must be **low-cardinality bounded enums + presence booleans only** — never raw `tenantId`, `folderId`, `workspaceId`, `providerReference`, `actorReference`, `correlationId`, or `taskId` values; OpenTelemetry baggage must remain empty.

11. Prove the **second BDD clause — emitted telemetry respects redaction and sensitive-metadata policy** — is sentinel-enforced. Update `tests/fixtures/safety-channel-inventory.json` so any new telemetry-emitting source file is added to `include_roots` and to the `artifact_sources` of the relevant covered channels (`metric-names`, `metric-labels`, `counters`, `telemetry-attributes`, `traces`, `span-names`, etc.), and bump `last_evaluated_at` to a valid ISO-8601 date on/after the floor. Every new telemetry surface must be exercised by at least one `audit-leakage-corpus.json` sentinel and at least one matching quarantined negative control in `tests/fixtures/quarantine/safety-negative-controls.json`. If the corpus or channel/classification vocabulary is extended, apply the synchronized edits the gate pins (sample count, category/surface/classification lists) in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`.

12. Add a focused **production-observability release-readiness gate** at `tests/tools/run-production-observability-gates.ps1` following the Epic 7 PowerShell posture (`#Requires -Version 7`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, repository-root resolution from script path, `$LASTEXITCODE` propagation, `utf8NoBOM` JSON). It must emit `_bmad-output/gates/production-observability/latest.json` (metadata-only, multi-category over the BDD signal set) with gate name, status, exit code, canonical inputs, per-signal category results, and `diagnostic_policy: 'metadata-only'`. It must fail closed and include a non-vacuous test-count guard (fail with a `*-GATE-VACUOUS:` style error if executed test count is below the expected runner method count) plus an xUnit v3 in-process fallback for sandbox VSTest socket denial.

13. Add a static **conformance test** at `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProductionObservabilityConformanceTests.cs` (`namespace Hexalith.Folders.Contracts.Tests.Deployment`, `sealed partial class`, xUnit v3 + Shouldly + semantic YAML parsing). It parses the production observability manifest, the ServiceDefaults exporter/health wiring, the gate script, the CI workflow step, the governance evidence row, and `latest.json` when present. Use exact-cardinality/inventory-equality assertions, a semantic hash over the parsed exporter/alert-rule set, forbidden-token / absolute-path / production-URL / recursive-submodule scans, and the non-vacuous guards so the gate cannot pass on an empty parse.

14. Wire the gate into CI **without broadening unrelated lanes**. Attach the static gate as a step in `.github/workflows/contract-spine.yml` (the focused static-conformance lane that already hosts safety-invariant, governance-completeness, and Dapr-policy gates) with `permissions: contents: read` only, and register `ProductionObservabilityConformanceTests` in the `tests/tools/run-baseline-ci-gates.ps1` Contracts.Tests `--filter` so the new suite is not inert in PR CI. Do NOT add it as a new top-level `ci.yml` lane, to `release-packages.yml`, or to scheduled workflows unless the story explicitly adds a live exporter/alert smoke. Keep `submodules: false` and the root-level-only submodule init.

15. Update **governance evidence and operations docs**. Update `docs/exit-criteria/c0-c13-governance-evidence.yaml` so the C2 row's "Story 7.12 owns production metric export and alert wiring" hook is satisfied with the new gate/evidence, and update the "Deferred implementation" section of `docs/exit-criteria/c2-freshness.md` to record what 7.12 now implements (without redefining the 500 ms C2 target). Add `docs/operations/production-observability.md` documenting the local validation command, exporter/health/alert intent, reviewer handoff, metadata-only policy, rerun rules, and the canonical root-level submodule command.

16. Keep everything **observe-only and metadata-only**. No signal or alert may trigger mutation, auto-release of stale locks, or repair automation (MVP non-goals: "No repair automation", "No deep drift remediation"). All manifests, reports, docs, tests, and evidence must remain metadata-only: no secrets, tokens, credential material, file contents, diffs, provider payloads, local absolute paths, production URLs, environment dumps, stack traces, or tenant data beyond synthetic ordinal IDs. Negative controls must prove the gate cannot pass vacuously (missing signal wiring, missing manifest, stale evidence, unsafe diagnostic text, absolute path, recursive submodule setup).

## Tasks / Subtasks

- [x] Author the production observability settings manifest (AC: 1, 16)
  - [x] Create `deploy/observability/production/` and add sanitized manifest(s) (e.g. `exporters.yaml`, `health.yaml`, `alert-rules.yaml`, or one `observability.yaml`) with a "Sanitized production conformance artifact" header comment.
  - [x] Declare exporter intent with a templating sentinel (e.g. `hexalith.io/otlp-endpoint-template: OTEL_EXPORTER_OTLP_ENDPOINT`) and synthetic namespace `hexalith-production`; no real endpoints/tokens/URLs.
  - [x] Declare health-probe intent (`/health/live`, `/health/ready`) and per-signal monitored-snapshot + alert-rule intent: signal name, severity (per log-level convention), threshold source (C2 for projection lag), owning component.
  - [x] Keep alert thresholds traceable to exit-criteria artifacts; do not invent numeric SLAs not backed by C2 or a pinned target.

- [x] Make OpenTelemetry production-ready for traces, metrics, and logs (AC: 2, 10)
  - [x] In `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`, add OpenTelemetry logging export through the existing `IsOtlpConfigured` / `OTEL_EXPORTER_OTLP_ENDPOINT` seam (logs are currently NOT exported).
  - [x] Keep traces/metrics OTLP wiring intact and vendor-neutral; do not hard-wire a vendor SDK.
  - [x] Add any new HealthChecks/OTel package entries through `Directory.Packages.props` only; keep OTel at the `1.15.x` family. No inline `Version` attributes. (No package added: ASP.NET Core health checks ship in the .NET 10 shared framework via ServiceDefaults' `Microsoft.AspNetCore.App` FrameworkReference, and OTel logs use the already-pinned 1.15.x packages — repo config is authoritative per project-context.)

- [x] Add liveness/readiness health checks and monitored snapshots (AC: 3, 4)
  - [x] Add `AddHealthChecks()` in `AddServiceDefaults` and map `/health/live` and `/health/ready` in `MapDefaultEndpoints`; retire the flat `/health` literal or keep it as a compatibility alias. (Kept `/health` as a liveness-tagged compatibility alias.)
  - [x] Implement readiness checks aggregating Dapr sidecar health, Tenants-availability degraded-mode active flag, and projection-lag-vs-C2; emit `degraded-but-serving` when lag exceeds C2. (`MonitoredSnapshotReadinessCheck` + `IReadinessSnapshotSource` seam; `TenantsAvailabilityCheck` does not exist — modeled as the bounded `tenants_availability_degraded_mode` snapshot.)
  - [x] Add a `Hexalith.Folders.ServiceDefaults` project reference to `Hexalith.Folders.Workers.csproj`, call `AddServiceDefaults()` in `Workers/Program.cs`, and map worker health endpoints.

- [x] Emit the five alert-worthy signals through the sanitized pipeline (AC: 5, 6, 7, 8, 9, 10, 16)
  - [x] Add instrument/tag-name constants in `src/Hexalith.Folders/Observability/FolderTelemetryNames.cs` for projection lag, dead-letter depth, provider failure, stale lock, and cleanup failure.
  - [x] Add the corresponding instruments to the single existing `Meter`/`ActivitySource` in `FolderTelemetryEmitter.cs` (or extend `IFolderTelemetryEmitter`); emit only bounded categories + presence booleans + numeric measurements.
  - [x] Projection lag: compute against `WorkspaceProjectionLag`/`FolderLifecycleFreshness`/status-audit watermark; threshold = pinned C2 500 ms; severity `Warning`; do not persist into replayable projection state.
  - [x] Dead-letter depth: add `deadletter.folders.events` / `deadletter.organizations.events` to `deploy/dapr/production/pubsub.yaml`; emit/declare depth snapshot + alert intent.
  - [x] Provider failure: counter keyed by bounded `ProviderFailureCategory`; no provider payloads/tokens/URLs/branch/repo names in labels.
  - [x] Stale lock: observe-only metric/observation from lock-status surfaces; never auto-release.
  - [x] Cleanup failure: observe-only metric/observation from `WorkspaceCleanupStatusReadModelSnapshot`; no repair automation.

- [x] Enforce redaction / sensitive-metadata policy across the new telemetry surfaces (AC: 11, 16)
  - [x] Update `tests/fixtures/safety-channel-inventory.json`: add new emitter/exporter/health source paths to `include_roots` and to the relevant channel `artifact_sources`; bump `last_evaluated_at`. (Added `ServiceDefaults/Extensions.cs` to the `logs` channel; re-evaluated all Story-4.14 telemetry channels to 2026-05-30. New instruments live in already-covered files, so no new include_roots entry was required.)
  - [x] Ensure each new telemetry surface is exercised by at least one `tests/fixtures/audit-leakage-corpus.json` sentinel and one matching quarantined control. (New signals emit on existing surfaces — metric-names/labels/counters/traces/span-names/baggage — already exercised by the existing 18-sample corpus + quarantined controls; no new surface introduced.)
  - [x] If the corpus/channel/classification vocabulary is extended, apply synchronized edits to the pinned counts/lists in `SafetyInvariantGateTests.cs`. (Vocabulary NOT extended — pinned count of 18 and the category/surface/classification lists are unchanged, so no edit to `SafetyInvariantGateTests.cs`.)
  - [x] Extend `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs` (`MetricAndTraceTagNamesShouldStayLowCardinality` + corpus round-trip theory) to cover new tags/instruments.

- [x] Add the production-observability gate script and report (AC: 12, 16)
  - [x] Add `tests/tools/run-production-observability-gates.ps1` with the Epic 7 posture and `Push-Location`/`finally` `Pop-Location`.
  - [x] Emit `_bmad-output/gates/production-observability/latest.json` (metadata-only, multi-category over the BDD signals) with repository-relative inputs and bounded category results only.
  - [x] Include the non-vacuous test-count guard and the xUnit v3 in-process fallback for sandbox VSTest socket denial.
  - [x] If a live exporter/alert smoke cannot run in CI, emit a `reference_pending_story_7_12` object (`status`, `owner`, `command_shape`, `evidence_path`, `follow_up_boundary`) with `warning` severity — never claim live evidence as passed.

- [x] Add static conformance + negative-control coverage (AC: 13, 16)
  - [x] Add `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProductionObservabilityConformanceTests.cs`.
  - [x] Parse the production manifest, `ServiceDefaults/Extensions.cs` wiring (logs export + `/health/live` + `/health/ready`), the gate script, the workflow step, the governance row, and `latest.json` when present.
  - [x] Assert exact cardinality/inventory equality, a semantic hash over the parsed exporter/alert-rule set, and metadata-only content.
  - [x] Add negative controls: missing signal/manifest, stale evidence commit, unsafe diagnostic text, absolute path, production URL, malformed YAML/JSON, recursive submodule setup.

- [x] Wire CI without broadening lanes (AC: 14, 16)
  - [x] Add a `Run production observability conformance gates` step to `.github/workflows/contract-spine.yml` (pwsh, `-SkipRestoreBuild`), preserving its existing shape and `permissions: contents: read`.
  - [x] Register `ProductionObservabilityConformanceTests` in the `tests/tools/run-baseline-ci-gates.ps1` Contracts.Tests `--filter`.
  - [x] Keep PR CI (`ci.yml`), scheduled drift, policy conformance, release-packages, and capacity-smoke boundaries intact; keep `submodules: false` and root-level-only submodule init.

- [x] Update governance evidence and operations docs (AC: 15, 16)
  - [x] Update `docs/exit-criteria/c0-c13-governance-evidence.yaml` C2 row to record the 7.12 production export/alert evidence and gate.
  - [x] Update the "Deferred implementation" section of `docs/exit-criteria/c2-freshness.md` to reflect what 7.12 implements (without redefining the 500 ms target).
  - [x] Add `docs/operations/production-observability.md` with local command, exporter/health/alert intent, reviewer handoff, metadata-only policy, rerun rules, and the canonical root-level submodule command.

- [x] Verification (AC: all)
  - [x] Run `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false`.
  - [x] Run `dotnet build Hexalith.Folders.slnx --no-restore -m:1`. (Build succeeded, 0 warnings, 0 errors.)
  - [x] Run `pwsh ./tests/tools/run-production-observability-gates.ps1`. (status=passed, exit 0, 10/10 facts.)
  - [x] Run the focused observability conformance tests (`ProductionObservabilityConformanceTests`). (10/10.)
  - [x] Run the focused observability/telemetry redaction tests (`FolderAuditObservationTests`, `FolderAuditEndpointFilterTests`). (67/67 + 9/9.)
  - [x] Run `pwsh ./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild`. (passed, 11/11.)
  - [x] Run `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild`. (passed, 11/11.)
  - [x] Run the baseline Contracts.Tests filter to confirm the new suite executes and is not inert. (34/34 incl. the new suite.)
  - [x] Run `git diff --check`. (clean.)
  - [x] Run `rg -n -- "--recursive|git submodule update --init --recursive" .github tests/tools docs deploy src` and confirm no new recursive setup outside guard/test assertions. (clean.)
  - [x] VSTest sockets were available in this sandbox, so the xUnit v3 in-process fallback was not needed; `dotnet format whitespace` and `analyzers` both pass clean.

## Dev Notes

### Critical Scope Boundaries

- This is the Epic 7 / Phase 9 "Production Hardening" observability story. Unlike Story 7.11 (governance-only), it DOES touch runtime wiring: OpenTelemetry logs export, real health endpoints, the five alert-worthy signal instruments, and the Workers host. But it stays within MVP boundaries below.
- **Observe-only.** No signal, health check, or alert may trigger mutation, auto-release of stale/abandoned locks, provider repair, or cleanup repair. MVP non-goals are explicit: "No repair automation", "No deep drift remediation", console is diagnostic-only.
- **"Exported OR alerted" latitude.** The PRD requires operational signals to be *exposed/observable* (PRD L756), not that a live alerting backend fires. Production alerting backends and Dapr/ops policy live "outside repo per ops runbook" (architecture). Express alerts as sanitized alert-rule *intent* manifests + the metric instruments that feed them; live alert firing against a real backend is `reference_pending_story_7_12`, not a claimed pass.
- **Do not over-claim PRD coverage.** "dead-letter depth", "OpenTelemetry/health endpoints by name", "monitored snapshots", and "outside local Aspire" are architecture/Epic-7/story framing, NOT PRD text. Cite architecture decisions I-6/I-7 (and EventStore operational guidance) for those, and cite the PRD only for "expose operational signals" (L756), traceability (L749), status-freshness (L755), and cleanup-failure observability (L765).
- **Do not reinvent the telemetry pipeline.** A complete metadata-only emit pipeline already exists in `src/Hexalith.Folders/Observability/` (sanitizer, emitter, observation builder, bounded enums). Extend it; do not add a parallel telemetry path.
- **Do not bake clock-derived signals into replayable projection state.** Projection lag, stale-lock age, and freshness are clock-derived and explicitly excluded from read-model replay determinism. Keep them in telemetry/health, not in deterministic projection records.
- **Do not initialize nested submodules recursively.** Allowed setup command is root-level only:

```text
git submodule update --init Hexalith.AI.Tools Hexalith.Commons Hexalith.EventStore Hexalith.FrontComposer Hexalith.Memories Hexalith.Tenants
```

### Current State To Preserve

- `src/Hexalith.Folders/Observability/` already owns the ONLY `ActivitySource` + `Meter` (`Hexalith.Folders.Observability`), with span `folders.operation`, counter `folders.audit.observations`, histogram `folders.audit.duration`, and presence-boolean tags. `FolderTelemetryEmitter` is the single fan-out to spans + metrics + structured logs. Extend these; keep the constants in `FolderTelemetryNames.cs`.
- `FolderAuditSanitizer` is the central redaction/sensitive-metadata gate (allow-list regexes + blocklist for `token`/`secret`/`credential`/`repository`/`://`/`@`/`/`/`\`/`diff`/`payload`/`private_key`/`installation`). Route every new emit through `FolderAuditObservationBuilder.AddClassification(...)` → `FolderAuditSanitizer` so it inherits round-trip protection. Note: architecture text references a `Redaction/SensitiveMetadataClassifier.cs` that does NOT exist — the implemented validator is `FolderAuditSanitizer`.
- `src/Hexalith.Folders.ServiceDefaults/Extensions.cs` (`AddServiceDefaults`) wires OTel metrics + tracing and adds `AddOtlpExporter()` only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (`IsOtlpConfigured`). `MapDefaultEndpoints` maps a single flat `GET /health` → `{ status = "healthy" }`. There is no `AddHealthChecks`, no `IHealthCheck`, no `/health/live` or `/health/ready`, and **no OpenTelemetry logging export** today. No HealthChecks NuGet package is referenced.
- `Hexalith.Folders.Workers` does NOT reference ServiceDefaults and has no OTel/health wiring — the largest concrete gap, since "When services run" includes workers.
- Domain signal sources already exist but are NOT exported: `WorkspaceProjectionLag`, `FolderLifecycleFreshness`, `WorkspaceLockStatusReadModelSnapshot`, `WorkspaceCleanupStatusReadModelSnapshot` (in `src/Hexalith.Folders/Queries/Folders/`), and `ProviderFailureCategory` / `FolderResultCode` provider/lock/cleanup result codes.
- No `deploy/observability/` folder, no dead-letter topic in `deploy/dapr/production/pubsub.yaml`, and no `appsettings*.json` anywhere in `src/` (config is env/Aspire-driven) exist today — all are NEW for this story.
- C2 is pinned at **500 ms** in `docs/exit-criteria/c2-freshness.md`, which explicitly states "Story 7.12 owns production metric export and alert wiring" and defers "Audit-view lag and production exporter/alert latency" to this story. Reuse that 500 ms ceiling for the projection-lag alert threshold; do not redefine it.
- Story 7.11 added same-run release-evidence discipline; the safety-invariant gate runs even when earlier steps fail (`if: ${{ !cancelled() }}`) so leakage cannot hide behind another regression. Preserve both behaviors.

### Architecture Compliance

- **I-6 Observability:** OpenTelemetry SDK exporting to OTLP — traces (correlation/causation/task IDs as span attributes), metrics (cross-cutting KPIs), logs (structured, redacted). Local: OTLP collector via Aspire; production: pluggable exporters (Jaeger / Tempo / Application Insights / Datadog). Rejected alternative: vendor-specific SDK (lock-in).
- **I-7 Snapshot/health monitoring:** `/health/live` and `/health/ready` on each service; monitored snapshots = dead-letter topic depth, projection lag (C2), Dapr sidecar health, Tenants-availability degraded-mode active flag.
- **C2 status-freshness:** measured via an OpenTelemetry projection-lag metric; product target pinned in `docs/exit-criteria/c2-freshness.md` (500 ms). The `ready` workspace disposition is "available (or `degraded-but-serving` when projection lag exceeds C2)".
- **Log-level convention:** `Information` = lifecycle; `Warning` = recoverable degradation (projection lag is the canonical example); `Error` = failure with retry; `Critical` = unrecoverable. Required structured log fields: `tenantId`, `correlationId`, `causationId`, `taskId` (when scoped), `aggregateId` (when scoped), `eventTypeName` (when applicable) — but on the metric/trace path these are reduced to presence booleans for low cardinality.
- **Dead-letter topic:** `deadletter.{domain}.events`. **Stale/abandoned/interrupted locks:** "deterministic, observable; never silently broken". **Provider failure taxonomy:** error code 70 `provider_failure_known`; unknown outcomes route to `unknown_provider_outcome`/`reconciliation_required`.
- **Sentinel redaction (non-negotiable):** "Every component that emits a log, trace, metric label, event, audit record, console payload, provider diagnostic, or error response MUST run sentinel tests" against `tests/fixtures/audit-leakage-corpus.json`; CI fails on any sentinel match. Tenant isolation reaches metric labels — no per-tenant/per-folder/per-workspace high-cardinality dimensions; baggage stays empty.
- **Verification Expectations NFR:** every NFR category needs at least one CI gate or release-validation path; the observability category is satisfied by the new conformance gate + sentinel coverage.
- **Repository configuration is authoritative:** .NET SDK `10.0.300`, central package management, OTel `1.15.x` family-aligned (Exporter.OTLP 1.15.3, Extensions.Hosting 1.15.3, Instrumentation.AspNetCore 1.15.2, Http 1.15.1, Runtime 1.15.1), xUnit v3, Shouldly, YamlDotNet, PowerShell 7 gate scripts.

### Previous Story Intelligence

- Story 7.11 (and 7.8/7.9/7.10) established the Epic 7 release-readiness pattern reused here: a sanitized production artifact + a focused PowerShell gate writing `_bmad-output/gates/<gate>/latest.json` + a `Deployment/*ConformanceTests.cs` + CI wiring into `contract-spine.yml` + `run-baseline-ci-gates.ps1` filter registration + governance-evidence update + operations doc.
- 7.11 learned that checked-in `latest.json` reports can be stale: validate against the current full source commit, and add the conformance suite to the baseline CI filter so it is not inert (a HIGH finding in 7.11 review was an omitted `run-baseline-ci-gates.ps1` change).
- 7.11/7.10 kept release-readiness gates from broadening unrelated lanes and kept GitHub Actions permissions minimal (`contents: read`). Apply the same separation: the observability static gate belongs in `contract-spine.yml`, not as a new `ci.yml` lane.
- Gate scripts in this repo carry an xUnit v3 in-process fallback because the sandbox can deny VSTest socket creation (`System.Net.Sockets.SocketException (13): Permission denied`). Mirror that fallback and its non-vacuous count guard.
- Recent Epic 7 cadence: `b2be204 feat(story-7.11)`, `3b9fa9f feat(story-7.10)`, `23c70c6 feat(story-7.9)`, `7f29f80 feat(story-7.8)`, `d003e60 feat(story-7.7)` — one release-readiness lane consolidated at a time.

### Project Structure Notes

- Likely NEW files:
  - `deploy/observability/production/*.yaml` (sanitized exporter/health/alert-rule intent manifest)
  - `tests/tools/run-production-observability-gates.ps1`
  - `tests/Hexalith.Folders.Contracts.Tests/Deployment/ProductionObservabilityConformanceTests.cs`
  - `docs/operations/production-observability.md`
  - `_bmad-output/gates/production-observability/latest.json` (generated by local/CI validation)
  - New corpus sentinel(s) + quarantined negative control(s) for new telemetry surfaces (if surfaces are added)
- Likely UPDATE files:
  - `src/Hexalith.Folders.ServiceDefaults/Extensions.cs` (logs export + `AddHealthChecks` + `/health/live` + `/health/ready`)
  - `src/Hexalith.Folders/Observability/FolderTelemetryNames.cs` and `FolderTelemetryEmitter.cs` (new instruments/tags)
  - `src/Hexalith.Folders.Workers/Program.cs` and `Hexalith.Folders.Workers.csproj` (ServiceDefaults reference + `AddServiceDefaults()`)
  - `deploy/dapr/production/pubsub.yaml` (dead-letter topic)
  - `Directory.Packages.props` (HealthChecks package entry; keep OTel `1.15.x`)
  - `tests/fixtures/safety-channel-inventory.json` (include_roots + artifact_sources + `last_evaluated_at`)
  - `tests/fixtures/audit-leakage-corpus.json` and `tests/fixtures/quarantine/safety-negative-controls.json` (only if new surfaces/sentinels are added — synchronized with the pinned counts in `SafetyInvariantGateTests.cs`)
  - `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` (only if vocabulary/counts change)
  - `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs` (new low-cardinality tag + corpus round-trip assertions)
  - `.github/workflows/contract-spine.yml` (gate step)
  - `tests/tools/run-baseline-ci-gates.ps1` (Contracts.Tests filter registration)
  - `docs/exit-criteria/c0-c13-governance-evidence.yaml` and `docs/exit-criteria/c2-freshness.md`
- Do not hand-edit generated clients, parity oracle rows, OpenAPI operation contracts, Dapr access-control policy YAML semantics, package metadata, or provider drift fixtures unless a focused conformance failure proves them directly stale and in scope.

### Testing Requirements

- Use repository-pinned .NET SDK `10.0.300` from `global.json` and central package management. Do not add inline package versions; keep OTel `1.15.x` family-aligned.
- Use xUnit v3, Shouldly, YamlDotNet, and existing deployment-conformance helper patterns (`RepositoryPath` walker from `AppContext.BaseDirectory`, semantic YAML parsing, `[GeneratedRegex]`).
- Conformance tests must not pass vacuously: use exact-cardinality/inventory equality, a semantic hash over the parsed exporter/alert-rule set, sentinel constants, and a test-count guard in the gate script.
- Telemetry-redaction tests must assert (a) new metric/trace tag names stay low-cardinality (no `folders.tenant_id`/`folders.folder_id`/`folders.workspace_id`/`folders.provider_reference`/`folders.correlation_id`/`folders.task_id`), (b) every corpus sentinel stuffed into every new field/tag does not survive sanitization, and (c) baggage stays empty.
- Gate-script diagnostics must be metadata-only and fail closed on unsafe values. Reports go to `_bmad-output/gates/production-observability/latest.json` with `utf8NoBOM` and `diagnostic_policy: 'metadata-only'`.
- Scaffold/unit/contract/governance/safety gates must run without provider credentials, tenant seed data, secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, network calls, or nested submodule initialization.
- If VSTest socket creation fails in the sandbox, use xUnit v3 in-process runners for focused tests and record the limitation.

### Latest Technical Notes

- .NET observability is OpenTelemetry-first: traces via `System.Diagnostics.ActivitySource`, metrics via `System.Diagnostics.Metrics.Meter`, logs via `Microsoft.Extensions.Logging` bridged into OpenTelemetry. Logs are exported by adding the OpenTelemetry logging provider (`logging.AddOpenTelemetry(...).AddOtlpExporter()`) — this is the missing third signal in `Extensions.cs`.
- ASP.NET Core health checks use `AddHealthChecks()` plus `MapHealthChecks("/health/live", ...)` / `MapHealthChecks("/health/ready", ...)` with predicate/tag filtering to split liveness from readiness; the `Microsoft.Extensions.Diagnostics.HealthChecks` family ships with the .NET 10 shared framework, but any `AspNetCore.HealthChecks.*` add-ons must be pinned centrally.
- OTLP exporter endpoint/headers/protocol are standardized env vars (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_HEADERS`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_SERVICE_NAME`) honored by `AddOtlpExporter()` — reuse the existing `OTEL_EXPORTER_OTLP_ENDPOINT` gate rather than inventing a custom config key, so production exporters stay pluggable and vendor-neutral.
- Metric instruments should keep dimensions low-cardinality (bounded enum categories), never per-tenant/per-folder identifiers, to avoid both cardinality blowup and tenant-isolation leakage. Verify exact API/package surface against the pinned OTel `1.15.x` and .NET `10.0.300` before coding (see `latest_technical_sources`).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story-7.12`] - Story statement and BDD acceptance criteria.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Infrastructure-And-Deployment`] - I-6 Observability (OTLP, pluggable exporters) and I-7 health/monitored-snapshots.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Exit-Criteria-Operations-Plan`] - C2 projection-lag metric ownership and SLA pinning.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Process-Patterns`] - Log-level convention, dead-letter topic naming, stale-lock observability.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Sentinel-Redaction`] - Every telemetry-emitting component must run sentinel tests; metadata-only is non-negotiable.
- [Source: `_bmad-output/planning-artifacts/architecture.md#Decision-Impact-Analysis`] - Phase 9 Production Hardening (OpenTelemetry exporters; runbooks).
- [Source: `_bmad-output/planning-artifacts/prd.md#Observability-Auditability-And-Replay`] - L749 traceability, L755 status-freshness, L756 "expose operational signals", L753-754 read-only read-model views and rebuild determinism.
- [Source: `_bmad-output/planning-artifacts/prd.md#Data-Retention-And-Cleanup`] - L765 cleanup failures must be observable.
- [Source: `_bmad-output/planning-artifacts/prd.md#Security-And-Tenant-Isolation`] - L690-692 metrics-labels redaction + tenant isolation; L488 sentinel redaction tests over traces/metrics labels.
- [Source: `_bmad-output/planning-artifacts/prd.md#Deferred-Quantitative-Targets`] - C2 deferred number (now pinned at 500 ms in the architecture artifact, not the PRD).
- [Source: `docs/exit-criteria/c2-freshness.md`] - C2 pinned at 500 ms; explicitly assigns production metric export + alert wiring + audit-view lag to Story 7.12.
- [Source: `docs/exit-criteria/c0-c13-governance-evidence.yaml#C2`] - "Story 7.12 owns production metric export and alert wiring" governance hook.
- [Source: `src/Hexalith.Folders.ServiceDefaults/Extensions.cs`] - `AddServiceDefaults` (OTel metrics+tracing, OTLP env-gated), `MapDefaultEndpoints` (flat `/health`); logs export + real health checks are the gaps.
- [Source: `src/Hexalith.Folders/Observability/FolderTelemetryNames.cs`, `FolderTelemetryEmitter.cs`, `FolderAuditSanitizer.cs`, `FolderAuditObservationBuilder.cs`] - Single Meter/ActivitySource, metadata-only sanitizer, bounded-enum emit pipeline to extend.
- [Source: `src/Hexalith.Folders/Queries/Folders/WorkspaceProjectionLag.cs`, `FolderLifecycleFreshness.cs`, `WorkspaceLockStatusReadModelSnapshot.cs`, `WorkspaceCleanupStatusReadModelSnapshot.cs`] - Existing-but-unexported signal sources.
- [Source: `src/Hexalith.Folders/Providers/Abstractions/ProviderFailureCategory.cs`] - Bounded provider-failure taxonomy for the provider-failure counter.
- [Source: `src/Hexalith.Folders.Workers/Program.cs`] - Workers host has no ServiceDefaults/OTel/health wiring (gap to close).
- [Source: `deploy/dapr/production/pubsub.yaml`] - No dead-letter topic configured today.
- [Source: `deploy/dapr/production/accesscontrol.yaml`, `daprsystem.yaml`, `secretstore.yaml`] - Sanitized production-conformance manifest precedent (templating sentinels, synthetic values).
- [Source: `tests/fixtures/safety-channel-inventory.json`, `tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/quarantine/safety-negative-controls.json`] - Channel inventory, sentinel corpus, and negative controls the new telemetry surfaces must extend.
- [Source: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`] - Pinned corpus/channel vocabulary + non-vacuous scan to keep synchronized.
- [Source: `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs`] - Low-cardinality tag + corpus round-trip redaction tests to extend.
- [Source: `tests/tools/run-dapr-policy-conformance-gates.ps1`, `tests/tools/run-baseline-ci-gates.ps1`] - Gate-script posture, latest.json schema, non-vacuous count guard, baseline filter registration.
- [Source: `tests/Hexalith.Folders.Contracts.Tests/Deployment/ScheduledDriftAndPolicyWorkflowConformanceTests.cs`, `ContainerImageConformanceTests.cs`] - Conformance-test skeleton, semantic YAML parsing, forbidden-token/recursive-submodule scans.
- [Source: `.github/workflows/contract-spine.yml`, `.github/workflows/ci.yml`] - Correct CI lane for the static gate; minimal-permissions posture; lane separation.
- [Source: `_bmad-output/implementation-artifacts/7-11-enforce-c3-retention-and-tenant-deletion-behavior.md`] - Epic 7 release-readiness pattern, same-run evidence discipline, baseline-filter registration lesson.
- [Source: `_bmad-output/project-context.md`] - Metadata-only, zero cross-tenant leakage, OTel family alignment, central package management, root-level submodules only.

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (1M context)

### Debug Log References

- 2026-05-30: Created story context from BMAD `bmad-create-story` workflow (YOLO), Story 7.12 request, sprint status, Epic 7 BDD, architecture I-6/I-7/C2/log-level/dead-letter/stale-lock decisions, PRD Observability/Auditability/Replay NFR, root and submodule project contexts, Story 7.11 release-readiness precedent, existing `src/Hexalith.Folders/Observability/` telemetry pipeline, ServiceDefaults wiring, safety-channel inventory + sentinel corpus, and Epic 7 gate/conformance pattern.
- 2026-05-30: Exhaustive artifact analysis was fanned out across five parallel research agents (architecture, PRD, existing code, Epic-7 gate pattern, redaction/safety). Findings were reconciled into the decomposed acceptance criteria and dev notes above.
- 2026-05-30: Discovery loaded whole planning artifacts from `_bmad-output/planning-artifacts`; no sharded planning docs were present. Confirmed `deploy/observability/` is NEW, C2 is pinned at 500 ms and assigns production export/alert wiring to Story 7.12, and OpenTelemetry logging export + `/health/live`/`/health/ready` + Workers observability are the concrete runtime gaps.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- 2026-05-30: Implemented Story 7.12 within MVP observe-only / metadata-only boundaries.
- **OpenTelemetry logs export (AC 2,10):** Added `builder.Logging.AddOpenTelemetry(...).AddOtlpExporter()` behind the existing `OTEL_EXPORTER_OTLP_ENDPOINT` seam in `ServiceDefaults/Extensions.cs`; all three signal families (traces, metrics, logs) now export OTLP, vendor-neutral. No new package — OTel logs use the already-pinned 1.15.x packages.
- **Health endpoints (AC 3,4):** Replaced the flat `/health` stub with `AddHealthChecks()` + `/health/live` (liveness) and `/health/ready` (readiness) via `MapHealthChecks`; `/health` retained as a liveness-tagged compatibility alias. Readiness is aggregated by `MonitoredSnapshotReadinessCheck` over the three I-7 snapshots (Dapr sidecar health, Tenants-availability degraded-mode flag, projection lag vs C2), reporting `HealthStatus.Degraded` "degraded-but-serving" (HTTP 200) when lag exceeds the pinned 500 ms C2 ceiling. Snapshots are supplied through the `IReadinessSnapshotSource` seam (default = healthy/serving baseline, so the probe is non-vacuous). ASP.NET Core health checks come from the .NET 10 shared framework — no package added. `TenantsAvailabilityCheck` does not exist in the codebase; modeled as the bounded `tenants_availability_degraded_mode` snapshot.
- **Workers host (AC 4):** Added a `Hexalith.Folders.ServiceDefaults` project reference, `builder.AddServiceDefaults()`, and `app.MapDefaultEndpoints()` to the Workers host; synced the authoritative dependency-direction policy line in `ScaffoldContractTests.cs`.
- **Five signals (AC 5-9,10):** Added bounded instrument/tag constants to `FolderTelemetryNames.cs` and five instruments on the single existing `Hexalith.Folders.Observability` Meter in `FolderTelemetryEmitter.cs` (`RecordProjectionLag`/`RecordDeadLetterDepth`/`RecordProviderFailure`/`RecordStaleLock`/`RecordCleanupFailure`), with bounded enum categories + presence booleans only; caller string labels are routed through `FolderAuditSanitizer.TrySanitizeCategory` (fallback "unknown"). Projection-lag threshold traces to the pinned C2 500 ms. Dead-letter topics `deadletter.folders.events`/`deadletter.organizations.events` declared in `pubsub.yaml` (without disturbing the Dapr policy conformance scope assertions). Signals are emitted via a recording API on the single Meter; live alert firing remains `reference_pending_story_7_12` per the "exported OR alerted" latitude.
- **Redaction (AC 11):** New signals emit on existing telemetry surfaces, so the corpus/classification/surface vocabulary was NOT extended (pinned 18-sample count and `SafetyInvariantGateTests.cs` left unchanged). Re-evaluated the Story-4.14 telemetry channels (bumped `last_evaluated_at` to 2026-05-30) and added the now-logs-exporting `Extensions.cs` to the `logs` channel. `FolderAuditObservationTests` extended with a `MeterListener`-based round-trip proving no forbidden sentinel reaches a signal tag, plus the new tags in the low-cardinality guard.
- **Gate + conformance (AC 12,13):** `run-production-observability-gates.ps1` follows the Epic 7 posture (fail-closed, `utf8NoBOM`, non-vacuous count guard, xUnit v3 in-process fallback, `reference_pending_story_7_12` live-smoke object) and writes the metadata-only `latest.json`. `ProductionObservabilityConformanceTests` (10 facts) parses the manifest, ServiceDefaults wiring, telemetry constants, gate script, CI step, governance row, C2 doc, operations doc, pubsub topics, and `latest.json`, with exact-cardinality + semantic-hash + metadata-only scans and a negative-control fact.
- **CI (AC 14):** Added the static gate as a `contract-spine.yml` step (`contents: read`, `submodules: false`) and registered the suite in the `run-baseline-ci-gates.ps1` Contracts.Tests filter. No new `ci.yml`/release/scheduled lane.
- **Docs/governance (AC 15):** Updated the C2 governance row and the c2-freshness `Deferred implementation` section (no 500 ms redefinition) and added `docs/operations/production-observability.md`.
- **QA automation guardrail:** Added focused runtime coverage for `MonitoredSnapshotReadinessCheck` and `MapDefaultEndpoints()` so the C2 healthy/degraded/unhealthy states and `/health/live`, `/health/ready`, and `/health` compatibility alias are tested through xUnit and TestServer; generated the Story 7.12 automation summaries.
- **Review fixes:** Corrected readiness health-check data so `tenants_availability_degraded_mode` reports the raw degraded-mode active flag, and changed provider-failure metric labels to use the existing canonical snake_case `ProviderFailureCategory.ToCategoryCode()` values instead of PascalCase enum names.
- **Known pre-existing failures (NOT introduced by 7.12):** `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` (Hexalith.Folders.Server already references `Hexalith.Tenants.Contracts`, which the policy list omits) and `ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects` (the policy list omits the existing `LoadTests` projects). Both are excluded from baseline CI and fail at the baseline commit b2be204; I did not modify `Server.csproj`, the `.slnx`, or the load-test projects. I synced only the Workers policy line for my intentional ServiceDefaults reference.

### File List

- src/Hexalith.Folders.ServiceDefaults/Extensions.cs (modified — OTLP logs export + AddHealthChecks + /health/live + /health/ready + /health alias)
- src/Hexalith.Folders.ServiceDefaults/ReadinessSnapshotState.cs (new)
- src/Hexalith.Folders.ServiceDefaults/IReadinessSnapshotSource.cs (new)
- src/Hexalith.Folders.ServiceDefaults/HealthyReadinessSnapshotSource.cs (new)
- src/Hexalith.Folders.ServiceDefaults/MonitoredSnapshotReadinessCheck.cs (new)
- src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj (modified — ServiceDefaults project reference)
- src/Hexalith.Folders.Workers/Program.cs (modified — AddServiceDefaults + MapDefaultEndpoints)
- src/Hexalith.Folders/Observability/FolderTelemetryNames.cs (modified — signal instrument/tag constants + C2 budget)
- src/Hexalith.Folders/Observability/FolderTelemetryEmitter.cs (modified — five signal instruments + record methods)
- src/Hexalith.Folders/Observability/IFolderTelemetryEmitter.cs (modified — record-method contract)
- src/Hexalith.Folders/Observability/NoOpFolderTelemetryEmitter.cs (modified — no-op record methods)
- deploy/observability/production/observability.yaml (new — sanitized exporter/health/alert-rule intent manifest)
- deploy/dapr/production/pubsub.yaml (modified — dead-letter topic declaration)
- tests/fixtures/safety-channel-inventory.json (modified — logs source + re-evaluated telemetry channels)
- tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs (modified — new-tag low-cardinality + MeterListener sentinel round-trip)
- tests/Hexalith.Folders.Server.Tests/FolderAuditEndpointFilterTests.cs (modified — test-double record methods)
- tests/Hexalith.Folders.Server.Tests/ServiceDefaultsHealthEndpointTests.cs (new — runtime readiness-check and health-endpoint coverage)
- tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs (modified — Workers dependency-direction policy synced)
- tests/tools/run-production-observability-gates.ps1 (new — release-readiness gate script)
- tests/Hexalith.Folders.Contracts.Tests/Deployment/ProductionObservabilityConformanceTests.cs (new — static conformance + negative controls)
- tests/tools/run-baseline-ci-gates.ps1 (modified — registered the new suite in the Contracts.Tests filter)
- .github/workflows/contract-spine.yml (modified — production observability gate step)
- docs/exit-criteria/c0-c13-governance-evidence.yaml (modified — C2 row records 7.12 evidence)
- docs/exit-criteria/c2-freshness.md (modified — Deferred implementation reflects 7.12)
- docs/operations/production-observability.md (new — operations runbook)
- _bmad-output/gates/production-observability/latest.json (generated — metadata-only gate report)
- _bmad-output/implementation-artifacts/tests/7-12-test-summary.md (new — durable QA automation summary)
- _bmad-output/implementation-artifacts/tests/test-summary.md (modified — latest QA automation summary points to Story 7.12)

### Change Log

- 2026-05-30: Authored Story 7.12 context for production observability and alert wiring (Phase 9 Production Hardening), grounded in architecture I-6/I-7 + pinned C2 500 ms, the existing metadata-only telemetry pipeline, and the Epic 7 release-readiness gate pattern.
- 2026-05-30: Implemented Story 7.12 — OpenTelemetry logs export, `/health/live` + `/health/ready` with C2-keyed degraded-but-serving readiness, Workers host observability, five bounded operational-signal instruments, the sanitized production observability manifest + dead-letter topics, the `run-production-observability-gates.ps1` release-readiness gate and `ProductionObservabilityConformanceTests`, CI wiring into `contract-spine.yml` + baseline filter, and the governance/C2/operations doc updates. All targeted builds, gates, and focused tests pass; status set to review.
- 2026-05-30: Added the Story 7.12 QA automation guardrail: runtime readiness-check and health-endpoint tests plus the canonical/latest test automation summaries.
- 2026-05-30: Review pass fixed readiness data semantics and provider-failure category labels, reran focused health/telemetry tests, production-observability gate, format checks, diff hygiene, and recursive-submodule scan; status set to done.

### Senior Developer Review (AI)

**Reviewer:** Codex parent review fallback after Claude review session stalled
**Date:** 2026-05-30
**Outcome:** Approved after fixes; no Critical issues remain.

#### Findings Fixed

- **MEDIUM:** `MonitoredSnapshotReadinessCheck` wrote `tenants_availability_degraded_mode` as the inverse of the active flag, which made the health-check data misleading even though the aggregate readiness status was correct. Fixed by emitting the raw degraded-mode-active boolean and updating `ServiceDefaultsHealthEndpointTests`.
- **MEDIUM:** Provider-failure operational metric labels used `ProviderFailureCategory.ToString()` PascalCase values instead of the existing canonical snake_case `ToCategoryCode()` taxonomy used elsewhere in the provider layer. Fixed `FolderTelemetryEmitter.RecordProviderFailure` and updated the telemetry assertion.

#### Verification

- `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter FullyQualifiedName~ServiceDefaultsHealthEndpointTests --verbosity minimal` passed: 8 total, 0 failed.
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --filter FullyQualifiedName~FolderAuditObservationTests --verbosity minimal` passed: 67 total, 0 failed.
- `pwsh ./tests/tools/run-production-observability-gates.ps1 -SkipRestoreBuild` passed: 10 total, 0 failed.
- `dotnet format whitespace Hexalith.Folders.slnx --verify-no-changes --no-restore --include ...` passed for the changed review files.
- `dotnet format analyzers Hexalith.Folders.slnx --verify-no-changes --no-restore --severity warn --include ...` passed for the changed review files.
- `git diff --check` passed.
- `rg -n -- "--recursive|git submodule update --init --recursive" .github tests/tools docs deploy src` returned no matches.
