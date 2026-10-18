---
title: 'Apply the approved conditional first-install decision for Story 1.17'
type: 'chore'
created: '2026-10-18'
status: 'done'
route: 'oneshot'
baseline_commit: '14e70652d27dcfdf8e47638266dc97bfc11b9c64'
context:
  - '{project-root}/docs/governance/approval-policy.md'
---

<frozen-after-approval reason="Jerome instructed apply recommended on 2026-10-18">

## Intent

Apply Jerome's acceptance of the recommended conditional first-install rollout
to Story 1.17's current plan. Record one dated decision, amend the current
migration spec and current discovery/rollback summaries, and retain the original
seven-day migration procedure for any discovered existing v1 installation.
First installation requires verified target/consumer coverage and retained-data
disposition, compatible exact releases, staging, qualified hosted authority,
recovery, smoke checks and observation. Unknown retained data blocks execution;
absence of matching Kubernetes objects proves no more than the scoped query.

Retain historical approvals, the October 16 proposal, prior receipts, original
baseline and both qualification packets unchanged. Keep runtime `V1Only`, T0
unset and the tracker unchanged. Preserve the previous turn's local changes.
Refresh only affected current candidate/provenance identities. Read-only current
target and release discovery may proceed; publication depends on the existing
exact-revision CI and production-environment gates. No deployment, release-gate
bypass, data mutation, tracker change, Git commit or push is part of this bounded
plan application. A blocked package release remains an explicit prerequisite,
not an invented version or a local dependency override.

</frozen-after-approval>

## Implementation Notes

- The recommendation and its target/data condition were presented in the
  preceding response. Jerome replied `apply recommended`; the single-owner
  policy accepts that conditional scope without another approval round.
- The working tree contains only the previous turn's ten recorded local paths;
  their initial hashes are retained in the
  [application receipt](evidence/story-1-17-first-install-application-2026-10-18.json)
  under `initial_baseline.preexisting_changes`. Continue those edits
  without cleaning, staging or normalizing historical document bytes.
- Scope is a reversible documentation reconciliation with read-only discovery.
  No new runtime API, test framework or deployment mechanism is needed.
- Applied the conditional owner decision to the current rollout spec, discovery,
  migration package and rollback runbook; historical proposal/packets and
  previous receipts remain unchanged. The designated-target metadata inventory
  confirms its UID and no identifying workload/volume references, with shared
  data still unknown. Current candidate/provenance checks pass 229/20 with two
  identical generations. Focused document/conformance checks pass 13/13 and
  governance passes 23/23 + 7/7 + 3/3.
- Read-only NuGet/release discovery found no compatible published EventStore
  version. The existing exact-live-main CI/P1R release prerequisites are still
  running; no release dispatch or catalog change is justified by this snapshot.
- Concurrent `/pushall` recorded the in-progress work as commit
  `0ffb6e6ae0b8125fae2da57a701dc47033ff611b` during review preparation. This task
  did not stage, commit, push or update submodules. Review therefore targets the
  recorded diff plus any subsequent local completion notes, rather than an
  empty worktree. Concurrent dependency changes are preserved separately from
  this documentation-only acceptance.

## Review Triage Log

The initial worktree-only review found no diff after concurrent recording. The
completed blind review inspected commit `0ffb6e6ae0b8125fae2da57a701dc47033ff611b`
and local additions. Its ten findings are classified individually below.

| Finding | Verdict, evidence and disposition |
| --- | --- |
| 1. Missing post-amendment receipt | medium, patch: the earlier resume receipt is historical; retain it and write the separate application receipt with final decision/spec/discovery/candidate/provenance identities. |
| 2. Tested versus concurrently selected dependency tuple | medium, patch: record both tuples and explicitly limit these 13 document/conformance and 33 governance checks; no renewed service integration or full package result is claimed. |
| 3. Inventory matching rule missing | medium, patch: the target record now states complete-object `json.dumps(...).lower()` substring matching, retained identity fields and the limited meaning of nonmatches. |
| 4. Branch-selection freshness | medium, patch: recheck target/consumers/data under the actual attempt lock before mutation; bind the epoch and invalidate selection on intervening changes or lost lock. |
| 5. Historic temporary Projects probe cannot be replayed from its log bundle | low, limitation corrected: explicitly state that limitation in the current addendum. Reject adding a new probe-retention mechanism to this plan application; full direct/delegated package qualification remains required and the historical receipt is preserved. |
| 6. First-install route target and checks unspecified | medium, patch: plan `V2Only` only after common entry and exact-release staging, with external v1 canonical 404s and required direct/delegated v2 outcomes; runtime remains unchanged. |
| 7. Observation endpoint unspecified | medium, patch: reuse the existing Platform five-minute post-readiness contract, immutable suites, fixed smoke cadence, bounded availability samples, failure triggers and passing deadline conditions; absent ordinary traffic never fabricates migration evidence. |
| 8. Internal/background effects during failure | medium, patch: require candidate-scoped public/internal/delegated and queued/shared-host fencing plus quiescence before recovery/removal; retain unrelated shared infrastructure and reconcile uncertain effects. |
| 9. Overlapping data branches | medium, patch: the empty-data branch now means no retained domain data; every retained-data case requires compatibility/recovery and a usable post-lock recovery point. |
| 10. Unlinked initial preservation baseline | low, patch: retain the ten initial path hashes in the linked application receipt, distinguish intentional document amendments and preserve prior receipt/test/control hashes. |

The review follow-up confirmed the document fixes and identified one remaining
wording ambiguity in finding 9: a post-lock recovery point applies to every
retained-data branch. The decision, current spec, discovery and runbook now say
explicitly to require that point and either compatibility evidence or named
approved recovery. Final receipt verification is performed by the parent.
