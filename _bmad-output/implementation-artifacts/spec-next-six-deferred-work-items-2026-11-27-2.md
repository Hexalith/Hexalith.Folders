---
title: 'Complete the next eligible Folders deferred work'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '5d0e10f5effb1cc96e24aa8ba737cd06f4f52245'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Continue the previous batch in the established deferred-work priority order. Assign each eligible item to a subagent, record skips, and commit the completed items separately with their ledger references. Build and test the full Folders solution before closing the entries and pushing to main.

</frozen-after-approval>

## Implementation Notes

Line references use the starting revision. The first three Folders implementation items are L3394, L3398 and L3406. L3402 requires production deployment limits and event volumes. L3410 through L3426 describe policy and behavior owned by the EventStore SDK. Folders uses the pinned EventStore packages in CI and production.

1. L3394 applies the existing per-value comparison rule to Audit, Domain, provider-readiness and operations-console reads. Authenticated claims supply identity, and mismatches use the existing safe result shapes.
2. L3398 uses the mutation comparison-value collector in the v2 candidate seam and retains the same declared sources and index order.
3. L3406 forces competing writers to interleave during a capacity test and checks an indeterminate result, successful redelivery and one persisted checkpoint advance.
4. DW-334 resolves the running Dapr sidecar endpoints and verifies the Workers' durable receipt for the exact synthetic envelope. The isolated AppHost start made it possible to revisit this earlier test item.
5. DW-333 completes the archive leakage follow-up. Event-shape and fingerprint assertion failures now omit event payloads and fingerprint values.
6. DW-339 completes the lifecycle normalization follow-up. The read-model warning records the exception type and suppresses the exception object.

### Skipped entries

- L3402 needs aggregate tenant growth and effective deployed Dapr and state-store limits. The existing synthetic capacity evidence does not supply those inputs.
- L3410 and L3414 need the EventStore owner to align gateway timeout translation and the command budget across both ends of the SDK contract.
- L3418, L3422 and L3426 need the EventStore owner to change the shared retry strategy, retry delay and registration. A Folders-only change would leave the policy unchanged for the other hosts named in these entries.

## Review Triage Log

- Medium, fixed. Task status now compares both identities before consulting tenant access, including when an earlier task validation would have returned.
- Medium, fixed. The Dapr delivery budget starts after the sidecars report application health. The seeded search waits for the routed Memories tenant to become active and uses the normal tenant-scoped bearer policy.
- Low, fixed. A cleanup failure after a failed receipt probe is recorded by exception type and preserves the primary test failure. Cleanup failure after a successful receipt verification still fails the test.
- Low, fixed. The collector test exercises malformed input through an in-memory collection and checks retention without formatting the value.

The live component fixture uses the Memories-owned test command store for synthetic tenant bootstrap and scoped test authentication for the index queries. The Workers receipt is persisted and read through Dapr, with the EventStore sidecar publishing to the real broker.

## Verification

- The full Release CI solution and the final Release AppHost test project built with zero warnings or errors.
- The nine operational-status capacity tests passed, including the 10,001- and 32,769-position cases. The focused follow-up runs passed all 98 lifecycle and 133 archive tests.
- All four live AppHost topology cases and all six explicit Keycloak AppHost cases passed. The Keycloak provider build passed 23 Maven tests.
- Python tooling verification passed 76 tests and 89 subtests.
- All 15 .NET projects passed complete runs, including 1,583 Server tests through Keycloak, 888 integration tests and 64 browser cases. The default run recorded the documented AppHost and Forgejo opt-in dispositions, with ten AppHost cases covered by the separate runs above.
- The six ledger closures leave 190 entries open or without a closure field.
