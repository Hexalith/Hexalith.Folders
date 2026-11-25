---
title: 'Complete the next six eligible deferred Folders items'
type: 'bugfix'
created: '2026-11-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete six eligible deferred items in priority order, verify each, and record each resolution in a separate Conventional Commit. Continue past blocked or obsolete work. Build and test the full solution before pushing to main.

</frozen-after-approval>

## Implementation Notes

The priority rule and earlier dispositions are recorded in the preceding batches. Continue at the first open untagged entry after the previous batch. Ledger references use baseline `a3a255ba2a1aecc8508acaf219c563d4e0b7a7bc`.

- L3050. Check retained scope hints for specialized rejections in both FolderEventReplay folds and projection classification. Verify frozen version-one fixtures and unmapped generic command replay.
- L3056. Require the receipt verifier at the production Server pipeline boundary before mapping callbacks. Verify startup refuses missing or null registration.
- L3067. Drive a gap and ordered repair through the authenticated Workers callback for both permission actors. Check persisted identity, sequence, ETag and revoked grants.
- L3070. Use the command catalog for the folder classification and check the correct actor and both wrong actor kinds for every known command. Preserve the existing safe unknown-command classification.
- L3076. Drive permission catch-up and concurrent delivery with a controllable clock. Check timeout, stale evidence, caller cancellation, and successful predecessor catch-up at the existing budgets.
- L3079. Exhaust tenant-access conditional write retries. Verify persistence stays unchanged, the failure is observable, and the same revocation applies after the conflict clears.

### Skipped entry

L3053 requires a Contract Spine owner decision to change published v2 scope. The callback paths are EventStore workload routes and the public contract is approval-bound.

The previous priority checks still apply. DW-317 requires a governed OpenAPI and generated-client revision. DW-319 has production mapping callers. DW-329 and DW-344 are obsolete because CI invokes Server.Tests. DW-330 refers to a corrected whitespace failure. DW-343 refers to corrected repository replay fixtures.

## Review Triage Log

- Medium, fixed. Recompute the remaining catch-up time after obtaining the read task. A virtual-clock regression verifies that the task shares the original deadline.
- Medium, fixed. Worker permission delivery, native operational folding, and named operational projection check the same retained scope as Server replay.
- Medium, fixed. Rejection validation refuses duplicate JSON properties and ambiguous PascalCase and camelCase aliases.
- Low. The retained event-name contract admits the rejection outcomes it already knows. Narrower outcome-to-event pairing would change historical payload admission, and current producers already emit their specific pairs.
- Medium, fixed. The Worker gap case exhausts conditional writes on the same envelope that later succeeds.
- Medium, fixed. The gap test checks tenant, actor, the repaired position's ETag, and both recorded message identities and digests.
- Medium, fixed. Exhaustion now runs through the authenticated tenant callback and the durable adapter. The 500 response preserves the projection and releases the message claim for the exact retry. Later-sequence supersession remains the existing operational recovery case.
- Low, fixed. The baseline-revocation and folder conditional-write tests also use controllable retry clocks.

## Focused verification

The complete Workers project passed 200 tests. The focused Server replay, operational projection, and permission read-model classes passed 114 tests. Python script verification passed 76 tests and 89 subtests. The six ledger closures leave 209 entries open or without an explicit closure.
