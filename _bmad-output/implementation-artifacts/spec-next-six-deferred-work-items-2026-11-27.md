---
title: 'Complete the next six eligible deferred Folders items'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: 'aef674d37adcc3628d915b26f5d9b497fa930fdd'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Continue in deferred-work priority order. Implement and verify six eligible items, close each in a separate Conventional Commit, run the full build and test suite, and push the verified commits to main. Record skips and count remaining entries.

</frozen-after-approval>

## Implementation Notes

Ledger references use the starting revision. Continue from the previous batch's last completed item.

- L3350. Validate examples declared on candidate JSON Schema objects using each owning subschema and its reference scope.
- L3354. Inspect decoded YAML keys and values with the existing corpus matcher and verify escaped quarantined controls.
- L3358. Emit the canonical HTTP task header and assert its literal wire name.
- L3362. Reject non-ASCII header identifiers at HTTP conversion with fixture contexts still usable outside HTTP.
- L3366. Use native Microsoft.Testing.Platform filtering and TRX output in the specialized conformance scripts.
- L3378. Load the generated console CSS bundle and verify it delivers the Shell styles.

### Skipped entries

The prior batches record earlier blocked and obsolete entries. DW-1 and DW-2 still depend on production story prerequisites. DW-41 already has a durable reader.

- L3334. The workflow timeout belongs to CI/CD ownership and the governed reseal.
- L3370. The G4 action count is part of the approved authorization matrix.
- L3374. Serving assets in every E2E fixture changes the coverage bound to PD6. Revalidation and the NFR evidence reseal remain coupled.

## Review Triage Log

- Refreshed the pending candidate inventory for each bound change. The published contract inputs and prior evidence retain their recorded bytes.
- Covered component response names and header names beginning with `x-`. The schema verifier accepts the OpenAPI 3.1 and Draft 2020-12 dialects and rejects an unsupported dialect before generation.
- Made the YAML control use the quarantine inventory and assert each sentinel stays out of its diagnostic.
- Required passing results for the named conformance methods. Each invocation uses a private TRX directory, handles unreadable results through the failed report, and prints only bounded runner metadata.
- Documented that direct consumers of the public task-header constant need to rebuild.

## Verification

- The focused candidate and safety suites passed 79 tests. Header conversion passed four tests, and the interactive Chromium smoke test loaded the Shell CSS.
- The six conformance scripts verified their required test methods through Microsoft.Testing.Platform on Linux.
- The Release CI solution built all Folders projects. The Debug source build at the starting revision encountered duplicate source and package assemblies. The local Aspire start failed at the pinned Tenants SDK check.
