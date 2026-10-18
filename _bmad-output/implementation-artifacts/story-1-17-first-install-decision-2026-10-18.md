# Story 1.17 conditional first-install decision

Decision maker: Jerome. Date: 2026-10-18. Decision: **accepted conditionally**.

After the migration and first-install options, tradeoffs and recommendation
were presented, Jerome instructed `apply recommended`. This accepts first
installation for the designated target once target/consumer coverage and
retained-data disposition establish that branch. It replaces the assumption
that a deployed Projects v1 consumer necessarily exists. It does not assert
that application data is absent or that production entry checks have passed.
The [single-owner policy](../../docs/governance/approval-policy.md) governs this
decision; another approval of the same conditional scope is unnecessary.

The [October 16 proposal](story-1-17-platform-rollout-proposal-2026-10-16.md)
supplies the prior target, common gates and recovery branches. That dated
proposal and its evidence remain unchanged. This decision supersedes its
pending scope-acceptance statement only. Its proposed dates are not appointments,
its artifacts are not current release qualification, and its source observations
are not current deployment evidence.

## Target and branch selection

The designated native context is `jpiquot@local`, kube-system UID
`b5860674-1d1e-4a3b-b413-e1bb3b9a4557`. The
[October 18 read-only inventory](evidence/story-1-17-first-install-target-2026-10-18.json)
confirms that UID and finds no Folders/Projects identifying references in 290
returned workload/routing objects or 59 PV/PVC objects. All four proposed
staging/production application/data namespaces are absent. These observations
cover this context and time only. Shared stores, streams, backups, opaque images
and external installations are not proven empty or absent.

| Verified state | Applicable procedure |
| --- | --- |
| No existing installation; no retained domain data | Use the first-install sequence below after all common gates pass. |
| No existing installation; retained data exists | Require a usable post-lock recovery point and either compatibility evidence or a named approved recovery procedure before installation. Preserve all data. |
| Existing v1 installation or consumer is discovered | Use the original coexistence migration, with exact prior artifacts, real consumer inventory, traffic and periodic schedules. |
| Installation or retained-data disposition remains unknown | Keep execution on HOLD; do not convert inventory nonmatches into empty-data evidence. |

Administrator records target coverage, existing consumers and retained-data
disposition in the actual attempt record. A discovered installation within the
accepted fallback requires its migration checks, rather than another approval
of that already accepted branch. A materially different target or recovery risk
still requires a new decision under the owner policy.

Refresh the target/consumer/data disposition after acquiring the environment
attempt lock and before the first workload-affecting mutation. Bind it to that
lock's epoch and actual attempt, not a reusable age allowance. The October 18
inventory is planning evidence only. A target mismatch, intervening deployment,
new consumer, changed data disposition or loss of lock invalidates selection;
stop and re-evaluate the applicable branch before proceeding.

## First-install sequence

1. Resolve the compatible EventStore release/catalog prerequisite and verify
   full Folders/Projects Release package builds. Bind exact published packages,
   images, declarations, configuration and the composed Platform release.
2. Complete Platform/module intake, hosted authentication and delegation,
   environment isolation, provider/profile qualification, recovery coverage,
   numeric stop thresholds, operators and UTC attempt slots. Plan `V2Only` for
   the verified first installation: qualify canonical non-enumerating external
   v1 404s and the allowed/authentication/denial/unavailable v2 outcomes for direct
   and delegated Projects flows in exact-release staging and production smoke.
   Keep current runtime `V1Only` until entry passes; the qualified executor then
   applies and restarts the planned mode. This is no current route change. If an
   existing v1 installation is found, use its `Coexistence` procedure instead.
3. Qualify that exact release in isolated staging: critical flows, durable
   persisted end-state and restart/replay, authorization negatives, direct and
   delegated Projects reads, distinct denial/unavailability, attribution and
   the applicable workload-removal or retained-data recovery procedure.
4. Once the target/data and common entry gates pass, execute the authorized
   first installation through Platform's existing attempt mechanism. Record
   actual activation time and use Platform's existing five-minute post-readiness
   verification/observation window with immutable staging-passed suites. Start
   smokes at window entry and use their fixed declared cadence. Cover the full
   window with availability observations no more than ten seconds apart (at
   least thirty observations, including required boundary coverage). Fail on
   sixty continuous seconds of required-service unavailability, the same smoke
   failing twice consecutively with its retry thirty seconds after the first
   failure, or missing readiness/latest passing checks at the deadline. A single
   restart or probe failure alone does not fail the attempt. Bind results and
   measured request counts to the installed artifacts. Absent ordinary traffic
   is recorded as a limited observation, never fabricated migration evidence;
   required smoke/critical-flow coverage still passes. Unknown thresholds,
   incomplete samples or missing suite results block completion. This reuses
   [Platform's Verification contract](../../references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md)
   rather than introducing another monitoring mechanism or migration window.
5. On failure, close ingress and set the promotion stop; the qualified executor
   fences candidate public/internal/delegated admission and candidate
   worker/queued/shared-EventStore execution under the attempt epoch. Drain or
   quiesce candidate effects and verify fencing before removing only candidate
   application workloads. Do not stop unrelated modules' shared infrastructure.
   Retain persistent objects, shared/environment infrastructure and diagnostics.
   With any retained data, use the verified compatibility/recovery branch before
   reopening. Uncertain in-flight effects stay stopped pending module-approved
   reconciliation. Removal does not undo writes or external effects; never
   claim a nonexistent v1 restore.

First installation supplies no v1 coexistence or retirement evidence. The
original migration's T0, Projects-within-24-hours requirement, 24-hour ordinary
traffic plus all periodic calls, and T0+168-hour limit remain binding when an
existing v1 installation selects that branch. No clock is started or reset by
this planning decision. Story 1.17 closes only after the applicable branch's
real release/deployment, verification, observation and recovery evidence and
an authorized closure/tracker decision. The five-minute first-install
verification/observation endpoint supplies no waiver of the original
existing-v1 branch's separate 24-hour traffic and periodic-call requirements.

## Current execution status

**HOLD.** Retained-data disposition, compatible release/catalog, exact hosted
release, identity/delegation and operational entry evidence remain open.
`V1Only`, unset T0, activation authority and tracker `backlog` are unchanged.
Neither qualification packet has been rewritten or accepted by this decision.
No namespace, release, deployment or data mutation is performed by this plan.

The [release-prerequisite observation](evidence/story-1-17-eventstore-release-prerequisite-2026-10-18.json)
at `2026-10-18T12:17:07Z` confirms NuGet still lists only EventStore `3.124.0`.
For remote source `f43b121c6c7539487b1f38b3c28508e3ab6264cc`, Commitlint
succeeded while CI and P1R qualification were running. The existing release
workflow requires all three proofs for live `main` and reviewer `jpiquot` at
its `production` gate. Re-resolve live source and proofs before dispatching;
do not repeat the preceding failed release preflight or invent a catalog pin.

The [application receipt](evidence/story-1-17-first-install-application-2026-10-18.json)
binds this amended plan, final current candidate/provenance and validation. The
earlier resume receipt remains evidence for its original bytes and dependency
tuple; these documentation checks do not renew service integration or full
package readiness for concurrently advanced dependencies.
