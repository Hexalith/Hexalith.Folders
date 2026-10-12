---
title: 'Story 12.1: reconcile completed EventStore implementation lifecycle'
type: 'chore'
created: '2026-10-12'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/spec-12-1-eventstore-backed-folder-repository-and-replay.md'
  - '_bmad-output/implementation-artifacts/story-12-1-eventstore-6-6-decision-2026-10-11.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 12.1 is already implemented and its original spec is `done`, but its sprint lifecycle key remains `in-progress`. The cached Epic 12 context is older than its planning source. Repeating the completed implementation would not address this discrepancy.

**Approach:** Verify the current EventStore compatibility pin and the focused production process, replay, authorization, ACL-read, and Worker callback checks. Refresh the epic context and synchronize only Story 12.1's lifecycle key to the build workflow's review handoff. Reuse the recorded deployed evidence with its stated limitations. The existing `DEC-EXEC-12.1` decision explicitly anticipates scoped lifecycle updates while preserving user comments and historical records. Preserve the original completed spec, frozen approvals, manifest, other story keys, and code. This work makes no new deployment, live conflict, or operator-recovery claim.

</frozen-after-approval>

## Implementation Notes

- Investigation at Folders revision `fef2d6ef9587cd2bd0cb4409f3356e58b8de5edf` found all original execution tasks checked and the final review closed. Production binds actor-folded repositories, SDK projection dispatch, durable permission views, and authenticated callbacks. No unfinished Story 12.1 code task was identified.
- The current EventStore gitlink is `851984f2362ba0b00972da4d03dab8be7e5ef4cb`, matching the consumer compatibility supplement. The original 6.6 acceptance and approval records remain historical evidence.
- Regenerated `epic-12-context.md` from planning sources because the manifest was newer. Made its execution controls explicit: general hold removed, v2 exposure unauthorized, tracker regeneration unauthorized. The original completed spec and frozen authority are unchanged.
- Fresh verification: `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug -m:1 -v:q` and the corresponding Workers.Tests build both passed with zero warnings/errors. Direct xUnit v3 execution passed 59 Server tests across `FolderEventReplayTests`, `ServerEndpointRegistrationTests`, `ProductionProcessWireEventTests`, `EventStoreOrganizationEffectivePermissionsReadModelTests`, `VerifiedReceiptEventStoreAuthorizationValidatorTests`, and `FolderAclEndpointTests`; `WorkersOrganizationAclRouteTests` and `FoldersFolderPermissionEventProcessorTests` passed 3/3.
- These checks verify the current source; deployed restart, persisted state, revocation, denial, replica serialization, outage, and positive ACL GET evidence remains in the original spec. No new live ETag race, provider-binding probe, operator repair, or infrastructure-failure 503 response was observed. Previously deferred work remains separately owned.
- The initial lifecycle sync to `in-progress` made no change because the story and parent epic already held that status. Finalization will update only the exact Story 12.1 key to `review` and the tracker `last_updated` scalar, preserving every comment and all other lifecycle keys.
- Independent Blind Hunter review returned four documentation findings. Corrected three context omissions and recorded one spec-only reproducibility improvement in the deferred-work ledger, as the one-shot review routing requires. Finalization hands Story 12.1 to `review`; it does not change the completed original spec or assert release acceptance.

## Review Triage Log

- Finding 1 — **medium, patch:** The context called the historical `ead4949d30b37d3a976f5c9197789a91701f1a72` acceptance revision the pinned revision, while the gitlink and supplement use `851984f2362ba0b00972da4d03dab8be7e5ef4cb`. Distinguished both records and cited the supplement.
- Finding 2 — **medium, patch:** The regenerated dependency section omitted the prior A6b matrix prerequisite. Canonical Story 12.1 and its manifest retain A6b and accepted OQ1/OQ2/OQ4 prerequisites; restored them explicitly.
- Finding 3 — **low, patch:** The confidential-value summary omitted the explicit outbox exclusion. Canonical Story 12.5 names outbox, broker, bridge, and retry evidence; restored those egress surfaces in the context.
- Finding 4 — **low, defer:** Verification notes name the builds, selected classes, and passing counts but do not spell out the full Worker build and both direct xUnit commands. Copyable commands would improve reproduction. The fix only edits a spec, which the one-shot classification instructions route to the deferred-work ledger; it does not invalidate the observed 62 passing checks.
