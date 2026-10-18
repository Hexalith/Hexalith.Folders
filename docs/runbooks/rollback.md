# Rollback Runbook

This runbook is the operator-facing procedure for reverting a release package or container image and verifying post-rollback health. It is metadata-only and uses synthetic examples only. It addresses a genuine operator gap that the existing operations docs do not yet cover.

## Purpose

Give operators a step-by-step revert procedure for a bad release - both the published package set and the per-service container images - and a concrete post-rollback health verification. It cross-links the release and image promotion sources rather than restating them.

## Preconditions

For Story 1.17, first apply the [October 18 target/data branch decision](../../_bmad-output/implementation-artifacts/story-1-17-first-install-decision-2026-10-18.md). The known-good-version and existing-v1 restoration procedures below apply when that baseline exists. First installation uses the failure procedure at the end of this runbook; never invent a prior release to satisfy these preconditions.

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

The [2026-10-08 final-byte recheck](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md#2026-10-08-final-byte-recheck) leaves every row in the table above without a measured value or named owner. Local gate results do not fill those fields. Keep `V1Only`, leave T0 unset, and do not treat a rollback procedure as a completed rehearsal.

The [current provenance reconciliation and Platform evidence](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md#2026-10-08-current-provenance-reconciliation-and-platform-evidence) resolves the four planning hashes but retains the hold: a separate OQ4 catalog mismatch fails governance, and the exact candidate release/deployment tuple is still absent. `Hexalith.Platform` owns production deployment evidence. Its retained October 4 single-cluster census supplies no Folders/Projects artifact tuple, measured traffic, periodic schedule, or migration owner. Delivery must still complete each record above, including the scheduled expiry action and its accountable operator, before T0. This runbook applies the accepted Platform MVP infrastructure envelope and adds no separate Folders infrastructure enrollment condition. Module behavior, authorization, durable-data safety, release, and the accepted seven-day rollback limits still apply.

The later [OQ4 reconciliation and release-identity preparation](../../_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-10-08-exact-release-artifact-preparation-and-entry-hold) supersede that OQ4 failure. A local, unpublished `1.2.0` package pair passed Projects adapter checks, but no exact published pair or production artifact was recorded. The prepared archives identify Folders `02152bace0c782b2b65bf9ccd31e4738a94eac17`; local HEAD later advanced to `503540aef1ebe42af2131cc3910aa52f96255acb` for release-identity evidence. Before a release dispatch, re-prove the package/source tuple and successful CI for the exact selected `main` revision. Neither the passing local audit nor the earlier proposal fills the execution record above. Keep `V1Only` and T0 unset until every entry field is measured and the reversal is rehearsed with the exact artifacts.

The [2026-10-09 committed-consumer recheck](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md#2026-10-09-committed-projects-consumer-recheck) supersedes the preceding unpublished-pair status for current package and Projects source readiness. Client and Contracts `1.2.1` are published and the clean Projects checkout at `eaf195b1c0a654127ca85d22069b6e1129911c86` builds and passes 39/39 focused adapter tests. The production artifact, durable Folders repository, deployed call schedule, attributed baseline and consumer census, exact-artifact rehearsal, thresholds, UTC slots, and named owners are still absent. This runbook is an unexecuted procedure until those records are supplied; keep `V1Only` and T0 unset.

The [2026-10-10 current-input recheck](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md#2026-10-10-current-input-and-consumer-recheck) supersedes only that local Projects revision: clean `1fd1650150920cef3b43df2de3468a607e2b2523` builds with Client/Contracts `1.2.1` and passes 39/39 focused adapters. The resealed Folders candidate passes local gates but is newer than the published pair. None of the measured execution-record fields in the table above has been supplied, and the production host still lacks its durable repository. The reversal remains unexercised; retain `V1Only` and T0 unset.

## 2026-10-14 execution-record status

The [current migration entry decision](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md#2026-10-14-current-source-recheck-and-entry-decision) retains the hold. The host now has actor-folded repository registration verified by local production-startup tests, superseding the earlier source-registration gap. Both default Release/package solution gates are blocked by the centrally selected EventStore `3.120.0` API surface; focused local startup/routing and Projects released-pair adapter tests do not establish a deployable final artifact tuple.

Every measured execution-record row above remains pending. No production artifact/configuration tuple, current attributed census/baseline, periodic-call schedule, numeric thresholds, exact-artifact rehearsal, UTC slot, deadline or named operator has been supplied. The reversal and scheduled expiry procedure remain unexecuted. Keep `V1Only`, T0 unset and the tracker unchanged. Do not use the passing source probe as production entry or retirement evidence; the accepted seven-day clock, mutation limits, incident ordering and expiry restoration requirements still apply.

## 2026-10-15 execution-record correction

The [current migration entry decision](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md#2026-10-15-committed-package-recheck-and-entry-decision) supersedes the preceding EventStore package-API blocker: the committed catalog selects `3.121.0`, Projects' Release/package build passes, and its 39 adapter checks pass. The exact required Folders build now fails on three `NU1903` audit errors; its permitted audit-disabled fallback passes but does not satisfy the default release gate. These local results establish no production artifact or completed reversal.

Every measured execution-record row remains pending. The accountable Platform deployment owner must supply the actual artifact/configuration tuple, attributed baseline/census, every periodic schedule, numeric thresholds, exact-artifact rehearsal, UTC slots and named operators before T0. Preserve `V1Only`, T0 unset, read-only caller scope, the original seven-day deadline and the incident/expiry restoration sequences above. A later successful local check does not start the window or authorize retirement.

## 2026-10-15 discovered ownership and production-subject qualification

The [read-only discovery result](../../_bmad-output/implementation-artifacts/evidence/story-1-17-operational-discovery-2026-10-15.json) found Platform code/workflow owner `@jpiquot`, recovery accountable operator `Administrator` and the recovery maintenance interval `2026-10-07T14:44:45Z` to exclusive `2026-10-31T23:00:00Z`. Reuse this evidence for its recorded recovery scope; a Folders migration appointment and actual activation/retirement/expiry slots still require their own recorded scope. The general 500 ms projection target does not fill the migration error/latency threshold fields.

The cited GitHub deployment statuses belong to package Release jobs, and the inspected native cluster has no Folders/Projects subject among 269 objects or 49 controller images. This corrects the earlier assumption that those statuses identify a known running v1 artifact to restore. Confirm the actual production subject and prior artifacts before attempting the reversal. One cluster's observation does not clear every consumer, and a failed monitoring-resource query does not prove monitoring is absent. The reversal remains unexecuted; preserve `V1Only`, T0 unset, the original deadline rules and the tracker.

## 2026-10-15 package-audit correction

The [SignalR fix result](../../_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-10-15-signalr-audit-build-fixed) supersedes the preceding default Folders build failure: the exact required Release/package command passes with auditing enabled and zero warnings/errors after the central Common `10.0.13` dependency fix. This resolves no production execution-record field. The actual production subject/prior artifacts, measured operational evidence and exact-artifact reversal remain pending; preserve `V1Only`, T0 unset and the original deadline/incident rules.

## 2026-10-18 current execution hold

The [resumed migration recheck](../../_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-10-18-resumed-migration-gates-and-package-handoff) supersedes the earlier passing package-build status: both current solution builds need owner-query APIs absent from released EventStore `3.124.0`. The current Projects inventory also includes verified delegated folder-read refreshes; the direct-adapter probe does not qualify that path. Rehearse the final composed path only after a compatible release/catalog and exact Folders/Projects artifacts pass their gates.

Local durable-host acceptance and Platform's recorded tool acceptance fill no measured execution-record field above. Establish the production target, retained data and existing consumers, or obtain the required owner decision for a material first-install scope change. Keep `V1Only`, T0 unset, the seven-day coexistence limit and incident/expiry ordering. No production rehearsal, migration, retirement, closure or tracker transition occurred.

## Story 1.17 accepted first-install failure procedure (2026-10-18)

Jerome accepted the conditional first-install recommendation. The [decision](../../_bmad-output/implementation-artifacts/story-1-17-first-install-decision-2026-10-18.md) supersedes earlier statements that the scope choice itself still needs approval. Target/consumer coverage, retained-data disposition, exact release/staging qualification and operational entry records remain required. The fresh scoped Kubernetes inventory is not empty-data evidence.

For a verified first installation with no retained domain data, close user ingress and set the promotion stop. Through the qualified executor and attempt epoch, fence candidate public/internal/delegated admission and candidate worker/queued/shared-EventStore execution; drain or quiesce candidate effects and verify fencing before removing only candidate application workloads. Do not stop unrelated modules' shared infrastructure. Retain all persistent data, environment-layer/shared infrastructure and diagnostics. Do not invoke a nonexistent v1 restore or claim removal reverses writes or external effects. Uncertain in-flight effects stay stopped pending module-approved reconciliation.

If retained data exists, require a usable post-lock recovery point and either compatibility evidence or the named approved recovery procedure before the attempt. Unknown data disposition blocks the attempt. Reopen only after the applicable recovery and smoke/verification gates pass. If an existing v1 installation is found, use the original exact-artifact migration procedure, T0/24-hour/168-hour limits and ordinary-versus-incident ordering above. This plan changes no live route, starts no clock and performs no removal or recovery.

Recheck target, consumers and data after acquiring the attempt lock and before mutation; any changed disposition or lost lock stops branch selection. For a qualified first installation, rehearse and apply `V2Only` with restart through the executor, proving external v1 canonical 404s and the required direct/delegated v2 outcomes. Use Platform's existing five-minute post-readiness verification/observation window and its ten-second availability samples, sixty-second continuous-unavailability and consecutive-smoke-failure rules, as detailed in the decision. Current runtime remains `V1Only` until entry passes; the existing-v1 migration's 24-/168-hour requirements are unchanged.
