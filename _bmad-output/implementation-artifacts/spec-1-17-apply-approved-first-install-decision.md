---
title: 'Apply the approved conditional first-install decision for Story 1.17'
type: 'chore'
created: '2026-10-18'
status: 'in-progress'
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
  their initial hashes are retained in the task baseline. Continue those edits
  without cleaning, staging or normalizing historical document bytes.
- Scope is a reversible documentation reconciliation with read-only discovery.
  No new runtime API, test framework or deployment mechanism is needed.
