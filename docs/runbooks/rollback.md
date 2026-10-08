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

3. Re-point each service deployment at the known-good image digest while keeping the stable Dapr app IDs unchanged: `eventstore`, `tenants`, `memories`, `folders`, `folders-workers`, and `folders-ui`. The production Dapr access-control config names stay the same; only the image reference changes.
4. Restart each service so its Dapr sidecar re-attaches under the unchanged app ID.

Post-rollback health verification:

5. Confirm `/health/live` returns healthy for each service.
6. Confirm `/health/ready` aggregates Dapr sidecar health, the Tenants degraded-mode flag, and projection lag; a `degraded-but-serving` readiness is acceptable while projections catch up.
7. Confirm the five operational signals (`projection_lag`, `dead_letter_depth`, `provider_failure`, `stale_lock`, `cleanup_failure`) return to baseline; investigate any that do not.

Backup/restore automation and its first drill remain `reference_pending` on `EXT-ES-RECOVERY` plus Story 13.7. Architecture I-11 fixes the regional-loss RPO/RTO, cross-region PITR, recovery-safety export/retention, isolated restore, and drill cadence in `./backup-restore.md`; this rollback runbook does not claim those mechanisms are implemented. A database recovery follows that runbook and is not an image rollback.

## Verification

Run the conformance gate `pwsh ./tests/tools/run-adr-runbook-docs-gates.ps1`, which emits metadata-only evidence to `_bmad-output/gates/adr-runbook-docs/latest.json`. The release and image wiring are validated by `pwsh ./tests/tools/run-release-package-gates.ps1` and the container-image gate. CI checkout keeps `submodules: false`; local setup initializes only root-level submodules with `git submodule update --init --checkout references/Hexalith.AI.Tools references/Hexalith.Builds references/Hexalith.Commons references/Hexalith.EventStore references/Hexalith.FrontComposer references/Hexalith.Memories references/Hexalith.PolymorphicSerializations references/Hexalith.Tenants`.

## Escalation and handoff

- If post-rollback `/health/ready` does not recover, escalate to the on-call operator and hand off the correlation ID and UTC window only.
- If a rollback appears to require folder-state repair, stop: that is out of scope for rollback and hands off to the reconciliation runbook (`./reconciliation.md`); never repair or retry silently.

## Story 1.17 route reversal before coexistence

Keep `Folders:ApiRouting:Mode=V1Only` until the accepted Projects migration package's entry checklist is recorded. Before setting `Coexistence`, Delivery must record the exact Folders and Projects artifact IDs, prior configuration, UTC activation slot, calculated T0 + 168-hour deadline, on-call and operations owners, pre-switch request/error/latency baseline, alert thresholds, and retirement slot. The route mode is read at startup, so rehearse each switch with a restart or redeploy in preproduction. The rehearsal uses the exact proposed artifacts and confirms v1 reads, v2 lifecycle/permission/metadata reads, restoration of the prior Projects v1 artifact, disabling v2 with `V1Only`, and recovered v1 reads. Record only counts, status, duration, trace IDs, UTC times, artifact IDs, and configuration IDs.

For an ordinary rollback during coexistence, retain the Folders v1 route, restore the prior Projects v1 artifact, verify all three inventoried read families through v1, then restart Folders with `V1Only`. For an authorization or disclosure incident, disable v2 immediately with `V1Only`, restore Projects v1, then verify those reads. Stop on failed smoke, missing attribution, unexplained v2 errors, an unplanned v1 consumer, breached agreed thresholds, or an infeasible T0 deadline. A rollback never undoes completed v2 writes; prohibit mutating v2 callers until their effect and reconciliation disposition is approved. At the 168-hour deadline without complete exit evidence, stop the exception, restore and verify the v1 path, and seek a new decision. Do not retire v1 or extend the window by changing a clock entry.

## Related evidence

- `../operations/release-packages.md` - the published package set and release-tag model.
- `../operations/container-images-and-dapr-app-ids.md` - the container image repositories and stable Dapr app IDs.
- `./backup-restore.md` - authoritative data-recovery procedure and drill evidence.

## Forbidden evidence

Rollback evidence is metadata-only. It must not include credentials, tokens, registry pull secrets, raw file contents, raw diffs, provider payload bodies, production endpoints, environment dumps, stack traces, host-absolute paths, or tenant data beyond synthetic ordinal identifiers.

## Story 1.17 execution record required before T0

The [2026-10-08 migration entry decision](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md#2026-10-08-readiness-correction-and-entry-decision) remains hold. Published package availability and a local Projects build do not complete this record. Delivery must supply measured values and named owners; none are inferred from source or historical deployment status.

| Record | Required evidence |
| --- | --- |
| Exact artifacts | Folders source/configuration and immutable deployment digest; matching Client/Contracts version, source and archive hashes; final Projects commit and deployment digest; verified prior v1 artifacts/configuration. |
| Baseline and thresholds | UTC baseline interval; counts, error rates and latency by `api.version`, `consumer` and status; evidence that Projects requests resolve to `projects`; agreed numeric alert thresholds, evaluation intervals and trace availability. |
| Schedule and owners | Operations and on-call owners; proposed route activation and retirement slots in UTC; expected T0 + 24-hour deployment limit and T0 + 168-hour exception deadline; every periodic call and next execution. Recalculate from actual T0 without resetting the clock. |
| Rehearsal | Exact-artifact preproduction v1/v2 lifecycle, permissions and metadata results; timed restart/redeploy and restoration of the prior Projects v1 artifact; verified `V1Only` configuration and recovered v1 reads. |
| Caller scope and effects | Verified Projects read-only operation inventory. No mutating v2 caller is admitted until completed-write reconciliation and ownership are explicitly resolved. Deployment reversal does not reverse writes. |
| Observation and exit | Actual Projects deployment/smoke UTC times; at least 24 hours of ordinary attributed v2 traffic and every periodic call; no unexplained errors or Projects v1 calls; complete deployed-consumer census; retirement and post-retirement smoke before expiry. |

If any entry field is absent, keep `V1Only` and leave T0 unset. During coexistence, use the measured thresholds above and record the UTC time of every decision. At expiry with any missing exit evidence, restore the verified prior Projects artifact and `V1Only` through the ordinary reversal sequence, verify the three v1 read families, record the outcome, and seek a new decision. An authorization or disclosure incident uses the immediate-disable sequence already specified above. The scheduled expiry action and owner must exist before activation; a blank record never authorizes extension.
