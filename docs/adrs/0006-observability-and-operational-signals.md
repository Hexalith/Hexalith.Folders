# ADR 0006: OpenTelemetry observability and operational signals

Date: 2026-05-31

Decision identifiers: `I-6`, `I-7`, `C2`. Implementing story: Epic 7 story 7.12 (production observability and alerts). This is a retrospective ADR; it records a decision already implemented across Epics 1-7, it does not propose new design.

## Status

Accepted. OpenTelemetry wiring, the health endpoints, and the five operational signals were finalized in Story 7.12.

## Context

Operators need to see folder lifecycle health without exposing tenant data, and without coupling the platform to a single observability vendor. The signals that matter operationally - projection freshness, dead-letter backlog, provider failures, orphaned locks, and cleanup failures - must be emitted as metadata-only telemetry and surfaced through health endpoints.

Architecture decision `I-6` pins OpenTelemetry exporting to OTLP with vendor-neutral pluggable exporters and metadata-only telemetry. Decision `I-7` defines the health endpoints and the monitored snapshots. Decision `C2` sets the status-freshness target that the readiness path uses to decide degraded-but-serving.

## Decision

Observability is OpenTelemetry-first, metadata-only, and vendor-neutral, with a fixed operational-signal set.

- `I-6`: traces carry correlation, causation, and task IDs as span attributes; metrics and logs are structured and redacted; exporters are pluggable so no observability vendor is hardcoded.
- `I-7`: each service exposes `/health/live` and `/health/ready`. Readiness aggregates Dapr sidecar health, the Tenants degraded-mode flag, and projection lag against the `C2` ceiling; when lag exceeds the ceiling it reports `degraded-but-serving` rather than failing readiness.
- The five operational signals are `projection_lag`, `dead_letter_depth`, `provider_failure`, `stale_lock`, and `cleanup_failure`. Alert-rule intent is owned in-repo; live alert delivery is a separate operator concern (see the alerts runbook).

Telemetry never carries secrets, raw payloads, file contents, or tenant data beyond synthetic ordinal identifiers.

## Consequences

- Operators get a stable, vendor-neutral signal vocabulary and can route OTLP to any backend without code change.
- Readiness degrades gracefully under projection lag instead of flapping, because `C2` defines the threshold.
- The cost is that the signal set and telemetry shape are contracts: telemetry must stay metadata-only, and live alert-delivery tooling (NFR54) is intentionally out of scope here and tracked as reference-pending.

## Alternatives Considered

- A vendor-specific agent and dashboard were rejected because `I-6` requires vendor-neutral pluggable exporters so the platform is not locked to one backend.
- Failing readiness on any projection lag was rejected because it would take a serving system out of rotation; `C2` plus the `degraded-but-serving` state keeps it serving while signaling the lag.

## Verification

This decision is conformance-checked by `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The observability wiring is enforced by `tests/tools/run-production-observability-gates.ps1`. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants`.
