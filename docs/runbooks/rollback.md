# Rollback Runbook

This runbook is the operator-facing procedure for reverting a release package or container image and verifying post-rollback health. It is metadata-only and uses synthetic examples only. It addresses a genuine operator gap that the existing operations docs do not yet cover.

## Purpose

Give operators a step-by-step revert procedure for a bad release - both the published package set and the per-service container images - and a concrete post-rollback health verification. It cross-links the release and image promotion sources rather than restating them.

## Preconditions

- The acting principal is authorized to operate the deployment environment; tenant authority comes from authenticated context, never from a query parameter.
- The target known-good version is identified by its immutable release tag and digest (recorded outside the sanitized repository artifacts).
- A rollback is a deployment-config change only; it must not mutate folder state, locks, idempotency records, or provider repositories.

## Procedure

Release-package revert:

1. Identify the last known-good package version (the release tag without its leading `v`).
2. Re-point the consuming environment at the known-good package set: `Hexalith.Folders.Contracts`, `Hexalith.Folders`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.Testing`. Published packages are immutable; rollback selects an earlier version, it never overwrites one.

Container-image revert:

3. Re-point each service deployment at the known-good image digest while keeping the stable Dapr app IDs unchanged: `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui`. The production Dapr access-control config names stay the same; only the image reference changes.
4. Restart each service so its Dapr sidecar re-attaches under the unchanged app ID.

Post-rollback health verification:

5. Confirm `/health/live` returns healthy for each service.
6. Confirm `/health/ready` aggregates Dapr sidecar health, the Tenants degraded-mode flag, and projection lag; a `degraded-but-serving` readiness is acceptable while projections catch up.
7. Confirm the five operational signals (`projection_lag`, `dead_letter_depth`, `provider_failure`, `stale_lock`, `cleanup_failure`) return to baseline; investigate any that do not.

Backup/restore and recovery-drill tooling is `reference_pending` (NFR55, owner: Operations Runbook, consuming story `7-17`). MVP ships no backup automation; durable events, audit metadata, and commit idempotency records are authoritative, and projections are rebuildable from the event streams. This runbook documents the revert process, it does not claim point-in-time restore tooling exists.

## Verification

Run the conformance gate `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The release and image wiring are validated by `pwsh ./tests/tools/run-release-package-gates.ps1` and the container-image gate. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.Tenants`.

## Escalation and handoff

- If post-rollback `/health/ready` does not recover, escalate to the on-call operator and hand off the correlation ID and UTC window only.
- If a rollback appears to require folder-state repair, stop: that is out of scope for rollback and hands off to the reconciliation runbook (`./reconciliation.md`); never repair or retry silently.

## Related evidence

- `../operations/release-packages.md` - the published package set and release-tag model.
- `../operations/container-images-and-dapr-app-ids.md` - the container image repositories and stable Dapr app IDs.

## Forbidden evidence

Rollback evidence is metadata-only. It must not include credentials, tokens, registry pull secrets, raw file contents, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, host-absolute paths, or tenant data beyond synthetic ordinal identifiers.
