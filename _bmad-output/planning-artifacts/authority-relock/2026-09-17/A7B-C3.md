# A7b / C3 Decision Payload

Version: `1.0.0-candidate.1`

Destructive temporary-file cleanup starts only after the staged task is terminal and no task is active. At that point the cleanup workflow records `stagedCleanupStartedAt` and `stagedCleanupNotBefore = stagedCleanupStartedAt + P7D`. Lock expiry, lease staleness, task cancellation alone, an `inaccessible` transition, retry, restart, or repeated readiness failure neither starts nor shortens this clock.

The non-destructive recovery clock is separate. `stagedRecoveryStartedAt` and `stagedRecoveryDeadline` govern only whether restored readiness may return an inaccessible workspace with staged content to `dirty`; they never authorize deletion. If the originating task legitimately resumes before cleanup, the current cleanup eligibility epoch is cancelled, and a later terminal/no-active closure starts a fresh full P7D epoch.

Legal hold blocks deletion. On hold release, the cleanup worker re-evaluates current terminal/no-active status, the full `stagedCleanupNotBefore` boundary, and every current-state predicate before deleting. The lifecycle transition matrix never deletes content; cleanup is an EventStore-owned workflow with durable evidence. The seven-calendar-day duration remains unchanged from the earlier policy; this approval changes and relocks the trigger, separate clocks, resume behavior, and legal-hold recheck.

