# Epic 4 Context: Repository-Backed Workspace Task Lifecycle

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable developers and AI agents to prepare, lock, modify, query, commit, and diagnose tenant-scoped repository workspaces through a deterministic task lifecycle. Preserve staged work across interruption, expose trustworthy failure and recovery evidence, and prove durable behavior through the deployed production path.

## Stories

- Story 4.1: Implement Folder aggregate state machine with C6 transition matrix
- Story 4.2: Prepare workspace from a ready repository-backed folder
- Story 4.3: Acquire task-scoped workspace lock
- Story 4.4: Inspect lock state and release the workspace lock
- Story 4.5: Enforce workspace path policy before file mutations
- Story 4.6: Add and change files with inline and streamed content transport
- Story 4.7: Remove files with metadata-only events and provider-safe ordering
- Story 4.8: Query file context with policy boundaries
- Story 4.9: Inspect workspace and projection currency
- Story 4.10: Surface workspace cleanup status without repair automation
- Story 4.11: Propagate idempotency keys, correlation, and task IDs
- Story 4.12: Commit workspace changes with unknown-outcome reconciliation
- Story 4.13: Surface canonical errors and operational evidence after failure
- Story 4.14: Emit metadata-only audit and observability
- Story 4.15: Validate lifecycle replay and projection determinism
- Story 4.16: Validate lifecycle security boundaries
- Story 4.17: Seed lifecycle capacity test harness
- Story 4.18: EventStore-backed workspace transition-evidence projection
- Story 4.19: Prove durable workspace prepare and lock lifecycle
- Story 4.20: Prove durable file mutation and bounded-context lifecycle
- Story 4.21: Prove real commit, retry, conflict, and unknown-outcome reconciliation
- Story 4.22: Implement the PD11 Guard-Discriminated Lifecycle

## Requirements & Constraints

- Preparation requires readiness, binding, ref policy, fresh authorization, and task context. One task owns its workspace and produces at most one successful durable commit. Mutations stage work without auto-commit; commit requires known policy-valid outcomes and provider-confirmed durability.
- Authorize before protected lookup; enforce path policy before content execution. Fresh negative authority gets canonical safe denial; stale/unavailable/conflicting/incomplete authority gets canonical authority-unavailable. Task identifiers confer no authority.
- Failure evidence includes lifecycle/lock state, safe cause, retry eligibility, client action, task/operation/correlation identity, timestamps, and freshness/checkpoint. Events, projections, audit, and diagnostics exclude bodies, diffs, secrets, provider payloads, and unauthorized existence. C9 classifies paths, refs, repository names, and commit messages; confidential values become correlation tokens before persistence.
- Context bounds: 100 paths, 2,000 tree entries, 500 search/glob results, 262,144 bytes per range, 1,048,576 aggregate bytes, two seconds. Truncation is explicit and never silently affects content/ranges.
- Completion requires populated deployed projections, restart survival, empty-checkpoint replay, isolation, denial, conflict, failure, timeout/unknown-outcome, and boundary evidence. Seed, in-memory, unavailable, safe-empty, and fake-only evidence cannot prove it.

## Technical Decisions

- Consume Hexalith.EventStore commands, ordered events, projection/replay SDK seams; avoid local platform infrastructure. Checkouts are disposable caches; locks, idempotency, fencing, checkpoints, and reconciliation authority remain durable across replicas/restart.
- Writer identity is managed tenant + canonical provider/repository + normalized ref token; aliases collide. Folder/workspace/task IDs are metadata. Lock vocabulary: `unlocked`, `locked`, `expired`, `stale`, `revoked`.
- Total `(state, event, guard)` evaluation uses durable server-resolved evidence; unlisted branches reject with `state_transition_invalid`. Preserve staged work on retryable failure, lock loss, and revocation; only the originating authorized task resumes it under a new lock.
- `unknown_provider_outcome` is `auto-recovering`: at most five read-only checks within fifteen minutes precede unresolved escalation to `reconciliation_required`/`awaiting-human`. Blind retry and operator discard/retry-success/mark-failed remain unsupported.
- Recovery deadlines never authorize deletion. Cleanup requires terminal closure, no active task, a fresh seven-day window, no legal hold, and cancellation on legitimate resume. Dirty/unresolved work remains protected.
- Tenant-scoped EventStore idempotency follows authorization/validation: equivalent live intent replays, differing intent conflicts, expired keys stay consumed. Reads reject keys; replay revalidates authority.
- Ordered-event rebuilds are deterministic; external-clock freshness/stale-lock calculations are excluded from equivalence. Separate availability/freshness/classified evidence from operational authority.

## UX & Interaction Patterns

Views remain read-only and workspace-centered, showing scope, authorization, and freshness before diagnosis. Distinguish lifecycle, lock, disposition, availability, and disclosure. Label staged/confirmed durability and stale, unavailable, redacted, withheld, unknown, and missing evidence with accessible cues. Show bounded automatic confirmation before human escalation.

## Cross-Story Dependencies

Epic 3 supplies readiness/binding; Epic 12 supplies durability. Story 4.18 follows 12.1–12.2 and 4.22; 4.22 follows 1.17 v2 authorization and C6/C3 decisions. Durable prepare/lock proof consumes 12.3/12.6; mutation/context proof consumes 12.7 and approved file/authorization policy; commit proof consumes 12.4 and prior lifecycle proof. Epic 6 owns diagnostic projections/console journeys; 11.10 owns platform seam adoption without product projections. Preserve earlier component evidence.
