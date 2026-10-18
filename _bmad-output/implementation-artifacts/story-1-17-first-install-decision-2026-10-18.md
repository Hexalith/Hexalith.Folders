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
| No existing installation; no retained domain data requiring migration | Use the first-install sequence below after all common gates pass. |
| No existing installation; retained data exists | Require compatibility evidence or a named approved recovery procedure and usable post-lock recovery point before installation. Preserve all data. |
| Existing v1 installation or consumer is discovered | Use the original coexistence migration, with exact prior artifacts, real consumer inventory, traffic and periodic schedules. |
| Installation or retained-data disposition remains unknown | Keep execution on HOLD; do not convert inventory nonmatches into empty-data evidence. |

Administrator records target coverage, existing consumers and retained-data
disposition in the actual attempt record. A discovered installation within the
accepted fallback requires its migration checks, rather than another approval
of that already accepted branch. A materially different target or recovery risk
still requires a new decision under the owner policy.

## First-install sequence

1. Resolve the compatible EventStore release/catalog prerequisite and verify
   full Folders/Projects Release package builds. Bind exact published packages,
   images, declarations, configuration and the composed Platform release.
2. Complete Platform/module intake, hosted authentication and delegation,
   environment isolation, provider/profile qualification, recovery coverage,
   numeric stop thresholds, operators and UTC attempt slots. Production route
   selection remains part of the qualified attempt; this decision changes no
   runtime route configuration.
3. Qualify that exact release in isolated staging: critical flows, durable
   persisted end-state and restart/replay, authorization negatives, direct and
   delegated Projects reads, distinct denial/unavailability, attribution and
   the applicable workload-removal or retained-data recovery procedure.
4. Once the target/data and common entry gates pass, execute the authorized
   first installation through Platform's existing attempt mechanism. Record
   actual activation time, run the accepted production verification/smoke and
   observation policy, and retain outcomes bound to the installed artifacts.
   Unknown timing, thresholds or observation evidence blocks the dependent step.
5. On failure, close ingress and set the promotion stop; the qualified executor
   removes only candidate application workloads. Retain persistent objects,
   shared/environment infrastructure and diagnostics. With retained data, use
   the verified compatibility/recovery branch before reopening. Removal does
   not undo writes or external effects; never claim a nonexistent v1 restore.

First installation supplies no v1 coexistence or retirement evidence. The
original migration's T0, Projects-within-24-hours requirement, 24-hour ordinary
traffic plus all periodic calls, and T0+168-hour limit remain binding when an
existing v1 installation selects that branch. No clock is started or reset by
this planning decision. Story 1.17 closes only after the applicable branch's
real release/deployment, verification, observation and recovery evidence and
an authorized closure/tracker decision.

## Current execution status

**HOLD.** Retained-data disposition, compatible release/catalog, exact hosted
release, identity/delegation and operational entry evidence remain open.
`V1Only`, unset T0, activation authority and tracker `backlog` are unchanged.
Neither qualification packet has been rewritten or accepted by this decision.
No namespace, release, deployment or data mutation is performed by this plan.
