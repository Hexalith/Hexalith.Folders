---
title: 'Complete the next actionable deferred work items'
type: 'chore'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete ten actionable deferred items in ledger order. Verify each change, record its resolution in the ledger, and commit it separately. Record the basis for skipping entries. Run the full solution build and test suite before pushing to main.

</frozen-after-approval>

## Implementation Notes

The prior batches established ledger order. After the numbered entries, continue in the order of the attended workflow entries and use their source story and exact summary to identify them.

The completed attended entries now have numbered ledger records. Each record preserves the original summary, source spec and evidence.

The mutation message-ID coverage was identified after the first two bridge entries had landed. Its commit follows those entries.

| Item | Verification |
| --- | --- |
| DW-309 | Archive integration verifies the emitted event, event-free replay and marked transport denial. |
| DW-320 | Gateway parity tests exercise null event arrays and elements through `/process`. |
| DW-346 | Both project-context sections point to the declared root inventory and the README setup command. |
| DW-349 | The restored details match OQ8 retention, architecture I-8, the OpenAPI parity rules and provider readiness requirements. |
| DW-351 | HTTP 409 title cases verify idempotency, lock, binding, repository and reconciliation results. |
| DW-352 | Folder-scoped writes preserve the stored value and ETag for mismatched tenant and folder identities. |
| DW-353 | The missing-projection error uses a constant message. |
| DW-354 | Repeated submissions exercise the generated mutation inventory and preserve the key across distinct message IDs. |
| DW-355 | Bridge tests read persisted JSON with omitted, null and empty entry-key arrays. |
| DW-356 | CLI invocation rejects blank required identifiers before constructing an SDK client. |

### Entries skipped

- Earlier numbered entries retain the reasons recorded by the [first batch](spec-ten-deferred-items-2026-11-24.md#entries-skipped), [second batch](spec-next-ten-deferred-work-items-2026-11-24.md#entries-skipped) and [provider and query batch](spec-deferred-worker-provider-and-query-checks.md#entries-skipped). DW-150 and DW-151 would require edits to the retained contract documents. DW-267 still lacks a production caller of its bounded diagnostic policy.
- DW-290 and DW-317 are held by live FR58 acceptance and the governed archive contract revision, respectively. DW-291's accepted events and DW-292's production permission projection already shipped. DW-295 and DW-297 depend on the durable provisioning handoff.
- DW-319's canonical mapper runs in production. DW-329 and DW-344 are covered by the shared Server test lane. The reported DW-330 whitespace was fixed. DW-343's current replay tests pass.
- DW-333 and DW-339 record review recommendations. Their previously identified test and normalization gaps were already addressed by the later archive and lifecycle commits.
- DW-334 remains blocked by the live Memories enrollment path. The opted-in topology baseline reproduced the two sidecar endpoint skips. The previous corrected-discovery run reached the worker receipt and exposed the separate Memories proof failure. DW-335 needs activation in the central CI workflow, which does not invoke the prepared local hook.
- The Forgejo test doubles already implement disposal. GitHub replay fixtures use the current evidence contract. The old uppercase-SHA expectation conflicts with the provider's case-insensitive Git object-ID contract. The existing Octokit malformed HTTP-body test verifies the real parser's safe outcome.
- The Story 12.6 legacy groups still owned by durable admission include the provisioning caller, intent scope and full live expiry/restart proof. The v2 inventory and generator now validate expired-key categories and the required parity cells. The old dirty-tree notes describe prior review workspaces.
- The bridge index cannot prune tombstones while the status query and pending removal publication depend on them. Pruning needs a retention decision and a replay-safe egress design. The old CLI parity mismatch is the existing DW-281 entry.
- Search deduplication needs a cursor design that keeps file versions unique across pages while preserving the privacy rule for raw hits. A page-only implementation can hide valid results beyond a duplicate-filled page.

## Review Triage Log

- Fixed. Folder-scoped writes check the physical key, tenant and folder again on the write policy's reload. A deleted or replaced entry fails before it can be recreated or returned.
- Fixed. Folder reads and event application reject a same-folder row stored under the wrong file-version key.
- Fixed. File add and change validate the folder index before creating a version row. Archive, remove and listing fail closed on missing or null arrays.
- Fixed. The round-trip test serializes the updated nonempty index and reloads it through a new bridge instance.
- Fixed. Conflicting event evidence reports the sequence without the projection key. A same-sequence delivery regression exercises that path.
- Fixed. The test gateway clears captured events before each request.
- Out of scope. EventStore permits domain-defined rejection event discriminators. Adding a Folders event-type allowlist to the parity gateway would introduce a separate compatibility policy.
- Fixed. The Epic 3 guide identifies the retained SDK wrappers and the architecture's shared McpCli boundary.
- Deferred. The review's concerns about short raw pages and duplicate identities across cursors share one pagination issue. The batch took the CLI validation entry instead.
