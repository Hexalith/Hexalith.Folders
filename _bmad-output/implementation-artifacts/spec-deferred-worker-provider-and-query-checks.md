---
title: 'Complete ten deferred worker, provider, and query items'
type: 'chore'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete ten actionable items in deferred ledger order. For each item, verify the affected behavior, record the resolution, and commit the change separately. Record why earlier entries were skipped. Run the full build and test suite before pushing to main.

</frozen-after-approval>

## Implementation Notes

The ledger has no separate priority field. Existing entry order supplies priority. The previous two batch specs record earlier decisions and prerequisites.

| Item | Implementation |
| --- | --- |
| DW-293 | Compare the real metadata-derived upsert with its archive re-send, including document identity, text, and attributes. |
| DW-296 | Return a validation result for provider validation failures and verify it alongside idempotency conflict, expiry, and ordinary repository conflict. |
| DW-301 | Register the logger required by folder telemetry and exercise observability and provider readiness with a bare container. |
| DW-302 | Observe the GitHub archived and disabled flags on the existing repository lookup. Reject inactive repositories before branch or protection requests. |
| DW-303 | Apply a per-request Octokit deadline. The 30-second default can be set from 1 to 120 seconds. |
| DW-322 | Verify the archive surface map against the declared corpus and scannable channel inventory. |
| DW-328 | Feed corpus values into an accepted aggregate command and verify input participation in the resulting fingerprint and absence from forbidden channels. |
| DW-337 | Use clock-based safe freshness for future lifecycle observations across all statuses. |
| DW-338 | Reuse the shared authorization and freshness rules across the five sibling queries and suppress unavailable projection watermarks. |
| DW-345 | Dispose the test host when endpoint mapping fails during lifecycle test setup. |

### Entries skipped

Earlier entries through DW-289 retain the documented dispositions in [the first batch](spec-ten-deferred-items-2026-11-24.md#entries-skipped) and [the second batch](spec-next-ten-deferred-work-items-2026-11-24.md#entries-skipped).

- DW-290 and DW-291. The public FR58 round trip remains held by its live-path prerequisites. Admission and the diagnostic broker probe cannot establish durable mutation publication and indexing.
- DW-292. The Server and Workers hosts now replace the in-memory permission reader with the EventStore reader. The worker processes folder and organization permission events. FR58 still owns the live round trip.
- DW-295. The worker rejects missing, non-executable, and mismatched admission before provider access. The command path's durable admission handoff belongs to Story 12.6.
- DW-297. Durable equivalent-replay evidence requires the Story 12.6 handoff and the approved additive event to advance together.
- DW-309, DW-320, DW-333, and DW-339. These request follow-up reviews of past bundles and identify no implementation change.
- DW-317. Its approved implementation requires coordinated OpenAPI and generated consumer updates. The accepted v1 and pending v2 archive contracts are bound to exact artifact revisions.
- DW-319. The Server now uses the canonical table for persisted provider outcomes, eligible gateway rejections, and content errors.
- DW-329 and DW-344. The shared CI lane lists the Server test project, and the complete-project verifier runs the assembly in the Platform issuer fixture.
- DW-330. The current source passes the scoped `dotnet format whitespace ... --verify-no-changes` check for the reported file.
- DW-334. The original opt-in probe skipped on sidecar discovery. With corrected discovery, the live worker persisted the expected folder-event receipt. The other probe failed because Memories could not provision `folders-index` without exact command/principal proof. The complete item needs the upstream enrollment path.
- DW-335. The recurring opt-in runtime lane requires a DCP-capable runner and belongs to the CI workflow owner.
- DW-343. All three historical replay failures pass in the current GitHub provider suite.

## Review Triage Log

- Fixed. A blank reason on an available stale lock or branch-policy snapshot now selects the literal fallback. The lock test covers empty and whitespace reasons.
- Kept the existing reason precedence for sibling compatibility failures. The earlier normalization spec governs the lifecycle handler and lists the sibling behavior separately. DW-338 aligns shared tokens and unavailable watermarks.
- Fixed. The lifecycle matrix independently tests future outer freshness and future snapshot freshness for an available result.
- Fixed. The aggregate sweep verifies fingerprint input participation. The REST-to-process integration covers retained corpus metadata and inspects the actual archive event, HTTP response, and lifecycle snapshot. The remaining map entries represent the declared test fixtures.
- Fixed. Inventory-only controls and channels are explicitly checked alongside corpus channels outside the archive sweep.
- Fixed. The GitHub repository lookup requires both activity flags and reconciliation also rejects inactive existing repositories. Transport tests cover omitted flags and equivalent creation conflicts.
- Fixed. Caller cancellation is classified separately from a transport timeout and stops subsequent branch or protection observations. The transport timeout covers delayed headers and delayed response bodies.
- Fixed. The GitHub deadline is validated during host startup.
- The exact OQ4 catalog bytes are governed. The README documents the current behavior and the OQ4 catalog can be refreshed with its next approval revision.
- Full-suite verification found stale hashes in the pending PD10 inventory. The generated inventory binds the updated source and all five new compile inputs while preserving its pending approval state.
