# Production Observability and Alerts

Story 7.12 wires production observability for the Folders services: OpenTelemetry export of all three
signal families (traces, metrics, logs), real liveness/readiness health endpoints, and the five
alert-worthy operational signals — all metadata-only and observe-only.

## Local validation

Run the focused gate from the repository root:

```text
pwsh ./tests/tools/run-production-observability-gates.ps1
```

The gate restores and builds `Hexalith.Folders.slnx`, runs the
`ProductionObservabilityConformanceTests`, and writes a metadata-only report to
`_bmad-output/gates/production-observability/latest.json`. Pass `-SkipRestoreBuild` (alias `-NoRestore`)
when the shared restore/build lane already ran. If the sandbox denies VSTest socket creation, the gate
falls back to the xUnit v3 in-process runner and still enforces the non-vacuous test-count guard.

If submodule working trees are missing, initialize only the root-level modules:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
```

Do not initialize nested submodules.

## Exporter intent

- OpenTelemetry exports traces, metrics, and logs to OTLP, gated on the standard
  `OTEL_EXPORTER_OTLP_ENDPOINT` environment seam, so production backends (Jaeger, Tempo, Application
  Insights, Datadog) stay pluggable and vendor-neutral. No vendor-specific SDK is wired.
- The sanitized exporter, health-probe, and alert-rule intent lives in
  `deploy/observability/production/observability.yaml` with templated placeholders only — no real
  endpoints, tokens, credentials, production URLs, or vendor account identifiers.

## Health intent

- `/health/live` reports process liveness.
- `/health/ready` aggregates the I-7 monitored snapshots — Dapr sidecar health, the Tenants-availability
  degraded-mode active flag, and projection lag versus the pinned C2 target. When projection lag exceeds
  the 500 millisecond C2 ceiling, readiness reports `degraded-but-serving` (HTTP 200) instead of failing.
- Both the server and the workers host map these endpoints through `MapDefaultEndpoints`.

## Alert intent

| Signal | Severity | Threshold source | Owning component |
|---|---|---|---|
| projection_lag | warning | docs/exit-criteria/c2-freshness.md (500 ms) | folders-server |
| dead_letter_depth | warning | architecture I-7 | folders-workers |
| provider_failure | error | architecture I-7 | folders-workers |
| stale_lock | warning | architecture process patterns | folders-server |
| cleanup_failure | error | PRD cleanup observability | folders-workers |

Each signal is emitted through the single existing `Hexalith.Folders.Observability` Meter with bounded
labels and presence booleans only. Alert rules are expressed as intent; live alert firing against a real
backend is reference-pending (`reference_pending_story_7_12`) outside this repository per the ops runbook.

## Metadata-only policy

All telemetry, manifests, reports, docs, and gate diagnostics stay metadata-only: no secrets, tokens,
credentials, file contents, diffs, provider payloads, host-absolute paths, production URLs,
environment dumps, stack traces, or tenant data beyond synthetic ordinal identifiers. Metric and trace
labels carry bounded enum categories and presence booleans only — never raw tenant, folder, workspace,
provider, correlation, or task identifiers — and OpenTelemetry baggage stays empty.

## Reviewer handoff

A reviewer validating this story should: run the local validation command above; confirm
`_bmad-output/gates/production-observability/latest.json` reports `status: passed` with
`diagnostic_policy: metadata-only`; confirm the production manifest, ServiceDefaults wiring, and the
governance C2 row stay synchronized with `deploy/observability/production/observability.yaml`; and confirm
no live endpoints, tokens, or backend assertions were introduced. Production alert backends and dashboards are
owned by the operations runbook outside this repository.

## Rerun rules

Use these rerun rules to decide when to rerun the gate:

- Rerun the gate after any change to the observability manifest, the ServiceDefaults exporter/health
  wiring, the operational-signal instruments, the dead-letter topic declaration, or the governance C2 row.
- The static gate runs in the `contract-spine` CI lane and through the baseline CI Contracts.Tests filter;
  it must not be promoted to a new top-level `ci.yml` lane, to `release-packages.yml`, or to scheduled
  workflows unless a live exporter/alert smoke is explicitly added.
- CI checkout keeps `submodules: false`; the gate never performs network calls, provider credentials
  resolution, or nested submodule initialization.
