# Scheduled drift and policy conformance

Story 7.8 adds two continuous release-readiness workflows that stay separate from PR CI:

- `nightly-drift-gates` in `.github/workflows/nightly-drift.yml`
- `policy-conformance-gates` in `.github/workflows/policy-conformance.yml`

Both workflows run on the default branch by UTC schedule and by bounded `workflow_dispatch` manual dispatch. They use `actions/checkout@v6` with `submodules: false`, initialize only the root-level build submodules, use `actions/setup-dotnet@v5` with `global-json-file: global.json`, and keep `permissions: contents: read`.

## Schedules

`nightly-drift-gates` runs at `02:17 UTC` every day. Manual dispatch accepts `provider_profile` with `pinned-snapshots` or `latest-supported`.

`policy-conformance-gates` runs at `02:43 UTC` every day. Manual dispatch accepts `policy_mode` with `static-plus-live-reference` or `static-only`.

The schedules intentionally avoid the top of the hour. GitHub scheduled workflows run against the latest commit on the default branch.

## Local commands

Run the scheduled drift gate locally:

```powershell
pwsh ./tests/tools/run-nightly-drift-gates.ps1
```

Run the scheduled policy conformance gate locally:

```powershell
pwsh ./tests/tools/run-scheduled-policy-conformance-gates.ps1
```

The existing static Dapr policy gate remains usable from `contract-spine.yml` and local verification:

```powershell
pwsh ./tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild
```

## Report paths

Scheduled reports are metadata-only:

- `_bmad-output/gates/nightly-drift/latest.json`
- `_bmad-output/gates/nightly-drift/sanitized-forgejo-drift.json`
- `_bmad-output/gates/policy-conformance/latest.json`
- `_bmad-output/gates/dapr-policy-conformance/latest.json`

Reports may include gate names, trigger posture, category names, provider versions, version classes, synthetic policy case categories, repository-relative artifact paths, status, severity, and exit codes. They must not include raw upstream schemas, raw schema diffs, provider payloads, Dapr diagnostics, tokens, secrets, production URLs, local absolute paths, stack traces, environment dumps, tenant data, or unauthorized-resource hints.

## Failure categories

Nightly drift categories:

- `forgejo-manifest-integrity`: supported-version manifest shape, unique versions, owner/reviewer fields, and integrity hashes.
- `forgejo-snapshot-coverage`: pinned Forgejo OpenAPI snapshots exist and cover required provider operation paths.
- `forgejo-drift-classification`: classification fixtures stay metadata-only and breaking or unknown actual drift fails.
- `forgejo-sanitized-report`: sanitized report generation succeeds and raw schema diff retention is disabled.
- `live-provider-drift`: `reference_pending_story_7_8` until a credential-free live lane is implemented.

Additive provider drift is warning-class evidence and remains visible. Breaking provider drift, unknown/unclassified drift, missing snapshots, stale integrity hashes, missing sanitized reports, raw schema diff retention, and forbidden sentinel values fail the workflow.

Policy conformance categories:

- `static-policy-shape`: production Dapr access-control artifacts exist and the static gate passes.
- `fixture-provenance`: the policy fixture semantic hash and canonical production inputs stay aligned.
- `negative-triple-coverage`: denied cases cover unknown caller, unauthorized caller, wrong target, wrong operation, wrong verb, wrong namespace, and wrong trust domain.
- `mtls-and-sidecar-bindings`: Dapr system mTLS and sidecar configuration bindings stay present.
- `pubsub-topic-scopes`: production pub/sub topic scopes stay constrained.
- `live-kind-dapr-denial`: `reference_pending_story_7_8` until isolated synthetic apps prove denied service invocation returns `403`.

Unauthorized Dapr policy changes fail the workflow. Live kind or daprd unavailability is not reported as a pass; it remains reference-pending until the synthetic lane exists.

## Relationship to PR CI

The scheduled workflows are continuous release-readiness evidence. They do not replace `.github/workflows/ci.yml`, `contract-spine.yml`, or focused PR gates such as `baseline-build-and-unit-gates`, `contract-and-parity-gates`, `security-and-redaction-gates`, `capacity-smoke-gates`, `run-contract-spine-gates.ps1`, `run-safety-invariant-gates.ps1`, `run-governance-completeness-gates.ps1`, `run-container-image-gates.ps1`, or `run-dapr-policy-conformance-gates.ps1`.

`contract-spine.yml` intentionally keeps the static Dapr policy conformance gate available for PR coverage. Story 7.8 adds scheduled release-readiness evidence around that gate plus a live-kind handoff boundary.

## Ownership and escalation

`folders-provider-maintainers` own Forgejo drift categories and the future live-provider implementation boundary.

`platform-engineering` owns production Dapr policy conformance, the static Dapr policy gate, and the future live-kind synthetic denial lane.

Scheduled evidence feeds later release stories:

- Story 7.12 uses the category/status fields for production observability and alert routing.
- Story 7.15 uses provider drift evidence for provider documentation and support posture.
- Story 7.16 uses metadata-only scheduled evidence in NFR traceability.
- Story 7.17 uses the commands, ownership, and escalation fields in maintenance runbooks.

## Submodules

Use only the root-level submodule initialization command:

```text
git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants
```

Nested recursive initialization is forbidden unless explicitly requested for nested-submodule work.
