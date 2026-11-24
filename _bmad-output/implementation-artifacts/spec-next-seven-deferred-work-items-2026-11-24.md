---
title: 'Complete the next seven deferred work items in priority order'
type: 'chore'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Implement the next seven items from deferred-work.md in priority order. Implement each fully with tests where applicable, mark it done in the ledger, and commit it separately with a message that references the item. Skip blocked or obsolete items with a recorded reason and take the next one until seven are complete. Then run the full build and test suite, fix any failures, push the commits directly to main, and report completed items, skipped items with reasons, and the number of deferred items still open.

</frozen-after-approval>

## Implementation Notes

### Priority rule

Most ledger entries have no priority field. Thirty-six numbered entries carry a `severity`. Selection order is therefore: open `high` entries, then `medium`, then untagged entries in ledger order, then `low`. Ledger order includes the unnumbered legacy bullets. Some sit between DW-350 and DW-356, and the rest follow DW-356. They have no status field. A completed bullet gets indented `status: done 2026-11-24` and `resolution:` lines, matching the bullets closed on 2026-10-23. Where the same defect appears twice, one change closes both entries and counts once.

### Completed items

| Ledger line (at start) | Item | Change |
| --- | --- | --- |
| L2459 | Missing DW-298 uppercase Git SHA tests | The restored tests showed the GitHub client accepted uppercase SHAs. The client now requires lowercase object ids for inputs, the expected head and every SHA read back, and compares SHAs ordinally. |
| L2463 | Octokit may wrap `SerializationException` | Settled as false with two real-deserializer tests on the status and binding reads. No production change. |
| L2580, L2614 | Stale DW-281 description | DW-281 carries an amendment that narrows the defect to the generator's hard-coded adapter list. |
| L2584, L2612 | Duplicate context-search hits | The handler emits one item per file version and keeps the first row that passes trimming and hydration. |
| L2613 | CLI reference omits `context index-search` | Added the row and a CLI test that compares documented rows with the registered leaf commands. |
| L2616 | `/process` canonical-identifier copies | Both copies use `FolderCanonicalSegmentIdentifier.IsLowercaseSegment`. The stricter `/process` rule is unchanged. |
| L2620 | Shared Server JSON options | The four identical response option sets use `FoldersServerJsonOptions.Response`. Request options stay per endpoint group because their strictness differs. |

While the final suite ran, another session merged the guarded OTLP export test (`b85ab9d0`, merge `d56e868f`) into `main`, rebased these commits onto it, and pushed them. That merge added `OtlpExporterClientMarker.cs` without updating the PD10 inventory, so the generator failed on `main`. A follow-up commit binds the marker and regenerates the manifest.

Each code commit regenerates the pending PD10 v2 hash manifest with `scripts/generate-pd10-v2-conformance-set.py`, so `Pd10ConformanceSetTests` passes at every commit. The new Server source and test files are added to `scripts/pd10-v2-story-owned-paths.txt`.

### Entries skipped

Severity-tagged entries:

- DW-329 and DW-344 (high and medium). The shared `domain-ci.yml` job lists `tests/Hexalith.Folders.Server.Tests` in `unit-test-projects`, and `after-unit-tests` runs the complete project. The gap no longer exists.
- DW-330 (high). `dotnet format whitespace Hexalith.Folders.CI.slnx --verify-no-changes --no-restore --include ./src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs` exits 0.
- DW-343 (medium). The fixtures now set `PriorCanonicalRepositoryId`, and the replay tests pass on the current build.
- DW-317 (medium). The approved change edits the OpenAPI spine. The v2 spine digest is pinned until a governed revision is approved.
- DW-319 (medium). `ForDomain` calls `StatusFor`, and `FolderDomainProcessor` calls `CategoryFor`. Neither method is test-only any more.

Untagged numbered entries:

- The reasons recorded in [the earlier batch](spec-deferred-validation-and-compatibility.md#entries-skipped) still apply to every open entry through DW-279.
- DW-280 and DW-286. Nullable limits change the v2 spine or public SDK signatures, and both need a governed revision.
- DW-281. Deriving the adapter list from `transportParity` would remove CLI and MCP expectations from 47 of 49 oracle rows that those adapters still implement and test. The coverage change needs a parity-contract decision. Its stale description is corrected (L2580).
- DW-282 and DW-284. The Server registers `EventStoreSemanticIndexingBridgeStore`. Story 10.8 owns the live round trip.
- DW-290, DW-334 and DW-335. Each needs a DCP-capable AppHost lane. The current opt-in suite fails during Tenants bootstrap, and workflow changes belong to Hexalith.CiCd.
- DW-291 and DW-292. Durable write-side events and the EventStore permission reader have since landed. Story 10.8 owns the remaining live proof.
- DW-295. The worker now requires a matching admission fingerprint. Story 12.6 owns the binding-service seam.
- DW-297. A replay event would add a value to the governed `WorkspaceTransitionAttempt.eventName` enum in the spine.

Unnumbered entries before L2620:

- L2451 and L2455. The Forgejo test doubles already dispose, and the replay fixtures carry `PriorCanonicalRepositoryId` with passing tests.
- L2467, L2471, L2475, L2479, L2483, L2500, L2504, L2512, L2516, L2520, L2524, L2528, L2532 and L2536. Story 12.6 is in progress and owns admission, intent adapters, the generated OQ8 matrix and the idempotency inventory.
- L2487. It describes a dirty tree from an earlier review.
- L2510. It questions historical Story 11.2 verification evidence. No product change follows from it.
- L2558. `GetFolderIndexingStatus` reports tombstoned versions, so pruning them from the folder index would change caller-visible output. That needs a retention decision.
- L2597. A live remove and archive round trip needs EventStore emission, ACL population and the DCP lane.
- L2600 and L2606. They describe working-tree state from September that no longer exists.
- L2630. Story 11.4's spec and sprint status both read `done`.
- L2634. `ForDomain` now serves `ProviderOutcomeProblem` and the content endpoints.

## Review Triage Log

- Medium, fixed. Duplicate rows of an emitted file version now count toward a full page, so later rows stay reachable through the cursor.
- Low, fixed. Deduplication is per page because the cursor is a raw source offset. The code comment and ledger resolution say so.
- Low, fixed. A file version rejected at hydration is not read again within the query. Rejection depends only on the version and the query scope.
- Medium, fixed. The operation-status read parses with System.Text.Json, not Octokit. Its test is renamed and documented, and a new test covers the existing-repository reconciliation read through Octokit.
- Low, rejected. The provider layer and Forgejo still compare SHAs case-insensitively. GitHub returns lowercase ids, invalid observed SHAs already map to conflicting evidence, and Forgejo canonical forms are tracked by a separate ledger entry.
- Low, fixed. Tests now cover an uppercase expected head on staging, an uppercase observed ref on status, and an uppercase SHA in the post-update ref confirmation.
- Low, fixed. The unused `TryReferenceSha` helper is removed.
- Medium, fixed. `FolderCommandRejected` kept a third copy of the grammar and comments that named removed methods. It now calls `IsLowercaseSegment`.
- Low, fixed. The `FolderCanonicalSegmentIdentifier` docs describe both methods and their callers accurately.
- Low, fixed. A request-handler test rejects an opaque task id that only the REST grammar accepts.
- Low, fixed. A stale serializer comment and a leading blank line are removed.
- Medium, fixed. The shared response options are made read-only at construction. The test checks this before first use, and the summary names the four endpoint groups.
- Low, rejected. The CLI reference test cannot check the operation column because commands expose no operation metadata. The fixed count in `ConsumerDocsConformanceTests` remains the independent check of the PD10-bound document.
- Low, fixed. The DW-281 amendment names the generator path and the `ui` rows. The original text stays because the ledger records corrections as amendments.
- Fixed. The pending fixup commit was squashed before the push.
- False. `tests/Hexalith.Folders.Cli.Tests` is not a PD10 coverage root, so the new CLI test is correctly absent from the manifest.

## Verification

- `dotnet build Hexalith.Folders.slnx -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false` succeeded with no warnings or errors.
- All 15 test projects passed when their built assemblies were run directly. The Server project ran through the Keycloak component verifier with `--child-timeout 1500` and passed all 1,493 tests. AppHost skipped 10 opt-in live cases, and the core project skipped the two opt-in Forgejo fixtures. The Python tooling tests passed.
- In the first full run, `OperationalStatusCapacityTests` failed once with 10,001 owners. It passed in isolation and in the final full run. The code under test was not changed by this batch.
- Each commit regenerates the pending PD10 manifest, and a fresh generation matches the committed manifest at every commit.
- `dotnet format whitespace` reports no changes.
- After the PD10 follow-up, the solution rebuilt with no warnings or errors. The complete Contracts project passed 775 tests and the complete Server project passed 1,493 at the new head. Relative to the earlier full run, only the two merged Server test files changed.
