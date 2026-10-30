# Epic 4 Context: Repository-Backed Workspace Task Lifecycle

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Developers and AI agents can prepare, lock, modify, query, commit, and diagnose repository-backed workspaces with deterministic lifecycle and failure behavior. Success is a provider-confirmed commit or a truthful blocked state, without mixed writes, silent repair, or cross-tenant leakage. This epic owns the transitions and transition evidence. Seed, in-memory, unavailable, and fake-provider results are not completion.

## Stories

- Story 4.1: Folder aggregate state machine
- Story 4.2: Prepare workspace from a ready repository
- Story 4.3: Acquire task-scoped workspace lock
- Story 4.4: Inspect and release the workspace lock
- Story 4.5: Enforce workspace path policy
- Story 4.6: Add and change files with inline and streamed transport
- Story 4.7: Remove files with metadata-only events
- Story 4.8: Query file context with policy boundaries
- Story 4.9: Inspect workspace and projection currency
- Story 4.10: Surface cleanup status without repair
- Story 4.11: Propagate idempotency, correlation, and task IDs
- Story 4.12: Commit with unknown-outcome reconciliation
- Story 4.13: Surface canonical errors after failure
- Story 4.14: Emit metadata-only audit and observability
- Story 4.15: Validate lifecycle replay determinism
- Story 4.16: Validate lifecycle security boundaries
- Story 4.17: Seed lifecycle capacity harness
- Story 4.18: EventStore-backed transition-evidence projection
- Story 4.19: Prove durable prepare and lock
- Story 4.20: Prove durable mutation and bounded context
- Story 4.21: Prove commit, conflict, and reconciliation
- Story 4.22: Guard-discriminated lifecycle
- Story 4.23: Canonical lock identity and C7 lock timing

## Requirements & Constraints

Prepare only with valid readiness, binding, branch/ref policy, fresh authorization, and task context. Failure is inspectable and has no unauthorized side effect.

Lock identity is managed tenant + canonical provider/repository identity + normalized target ref. Aliases collide; other tenants do not. Folder, workspace, and task IDs are metadata. No canonical identity fails closed. Lock state is unlocked, locked, expired, stale, or revoked. Deny before any side effect, with one metadata-only audit record and no takeover. Release needs the originating task, ownership proof, and no staged changes. An unexpired equivalent retry is one result; an expired key does not run.

Renew at 30s, revalidate at 15s and before renewal or mutation, revoke within 60s of the upstream timestamp, and move expired to stale at the inclusive 60s boundary. Overrides may only tighten to a positive whole number of seconds. Renewal is owner-only on the versioned surface. A revoked instance never reactivates. Stale authority fails closed and keeps staged work.

No auto-commit. Move or rename is add plus remove, and only inside a prepared, freshly authorized, locked workspace. Reject a path escape without echoing the path. Use closed file policy ASCII/500/NFC.

After authorization, context caps are 100 paths, 2000 tree entries, 500 search/glob results, 262144 range bytes, 1048576 aggregate bytes, and 2 seconds. Truncation is explicit. Indexes are not authoritative.

Commit only on provider-confirmed durable remote/ref update. unknown_provider_outcome allows at most five read-only checks in 15 minutes; then reconciliation_required blocks retry, mutation, takeover, and cleanup.

States: requested, preparing, ready, locked, changes_staged, dirty, committed, failed, inaccessible, unknown_provider_outcome, reconciliation_required. Transitions are (state, event, guard); omissions reject as state_transition_invalid. Retryable unconfirmed commit failure stays dirty with staged work; non-retryable failure is failed. Restored readiness returns to dirty only when staged content is inside the recovery deadline, to ready only when clean, and otherwise waits for eligible cleanup. Only the server-resolved originating task resumes. Clean dirty returns to ready only on the stale-lease event. Operator discard, retry-success, and mark-failed reject. Pair-only behavior is historical, and reapproval is pending.

Cleanup starts after terminal closure and no active task, uses a fresh seven-day window, and cancels on resume. Recovery expiry never deletes. Keep dirty, unknown, reconciliation-required, legal-hold, and nonterminal work. No repair. Record the originating task, principal, and separate recovery and cleanup clocks.

Mutations require an idempotency key; reads reject one. Equivalent unexpired intent replays, conflict hides prior intent, and expiry does not run. Commit replay lasts seven years; other mutation replay lasts 24 hours. Carry correlation and task IDs, and authorize before admission.

Return final state, retry eligibility, correlation, a sanitized reason, and a client action. Denial is not retryable; unavailable authority is. Neither reveals existence.

No contents, diffs, tokens, credentials, secrets, or hidden existence in events, logs, traces, metrics, projections, audit, diagnostics, or errors. Redacted, withheld, unknown, missing, and unavailable stay distinct. Confidential values become non-reversible tokens at write.

## Technical Decisions

Lifecycle state stays on the folder aggregate. Preparation and retries run in workers or process managers. The working copy is disposable. Transition evidence rebuilds from ordered durable events, survives restart, and populates only the correct tenant. Deployed proof uses the shared verification lane. Inline JSON stops at 256 KB, then stream; no Base64. Both Git providers share one retryable classifier. Local success is not a commit. Stale-lease and lock renewal have no historical route.

## UX & Interaction Patterns

Keep lifecycle, lock, disposition, folder lifecycle, freshness, and visibility separate. Dispositions: available, auto-recovering, degraded-but-serving, awaiting-human, terminal-until-intervention. Requested, preparing, committed, and active unknown_provider_outcome are auto-recovering; ready is available unless freshness exceeds 500 ms; locked and changes_staged are degraded-but-serving; dirty is degraded while resumable or clean, otherwise awaiting-human; failed and inaccessible are terminal until intervention. reconciliation_required is awaiting-human and is not success or failure. The console is read-only: no retry, repair, or discard. Do not use color alone.

## Cross-Story Dependencies

Execution rank orders delivery. Durable events, content, restart replay, real Git, and mutation idempotency precede positive completion. This epic owns lifecycle rules and transition evidence. The trust console owns diagnostics. Parity must not fork the lifecycle. Metadata search is not file, workspace, or audit truth. The versioned contract precedes guarded transitions and lock renewal. Canonical lock identity and approved timing precede deployed prepare-and-lock proof. File-policy design is closed; runtime proof is not.
