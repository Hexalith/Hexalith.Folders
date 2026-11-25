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

Complete six eligible deferred Folders items in the established priority order. Verify and close each item in a separate Conventional Commit. Record skipped items and their reasons. Run the full build and test suite, address failures, and push directly to main.

</frozen-after-approval>

## Implementation Notes

Ledger line numbers refer to baseline `92bfc44b885a74e47ce95af3c843436312e018c4`. Continue after the previous batch's stream-segment item.

1. L3183 pins the GitHub created-commit digest with the existing canonical vector table.
2. L3199 applies the legacy adapter's existing metadata-only field exclusions to emitted dictionary entries and JSON objects.
3. L3203 restores the three missing constraints in the compiled Epic 1 context.
4. L3287 rejects an erasure grant before a folder exists and checks the actor replay boundary with a zero-event state.
5. L3321 carries the five decorated query page ranges into their descriptions and checks the metadata consumed by the schema exporter.
6. L3334 checks the embedded helper hash at the checkout boundary while preserving the normalized content-check API.

### Skipped entries

The six open severity-tagged entries retain their previously recorded dispositions. DW-317 requires a governed OpenAPI revision. DW-319 has production callers. DW-329 and DW-344 are obsolete because CI runs Server.Tests. DW-330 refers to a corrected whitespace failure. DW-343 refers to corrected replay fixtures.

| Entry | Reason |
| --- | --- |
| L3161 | The approved parity oracle does not declare a rate-limit outcome for either operation. A 429 here needs a contract decision. |
| L3165 and L3172 | The active pending inventory already binds all four extracted helpers. The earlier hash set is retained as historical evidence and A6b remains pending. |
| L3177 | Production has no caller constructing either provider request. The two guard choices need a shared idempotency-key and operation-reference contract before the data plane wires them. |
| L3187 | The current CI workflow runs the core test project successfully on the starting revision. The historical EventStore namespace failure has been superseded. |
| L3194 | Persisted fingerprints need a version and compatibility decision before optional presence markers change the bytes. |
| L3207 | Measurement requires representative production namespace sizes and deployed dependency latency. |
| L3260 | The CI catalog model intentionally loads the compiled, pinned Tenants runtime output. Removing the source edge requires a package-backed runtime artifact for that test, or an equivalent topology qualification. |
| L3264 | Release or operational qualification still requires an owner-approved immutable Platform candidate. |
| L3270 and L3274 | The final 4/2/7 package-cohort qualification passed the selected-owner catalog and all 328 selected Tenants cases, including the named failures. |
| L3278 | Canonical legacy enrollment requires a new Tenants/McpCli owner decision. |
| L3284 | EventStore returns the stored terminal result for an exact committed command before invoking the folder processor. |
| L3290 | The content query still requires complete replay for the protected checks. An optimized path needs a qualified snapshot or native head check that preserves the authority fence. |
| L3293 | Existing checkpoints need an explicit schema/rebuild or migration policy before changing the integrity digest. |
| L3299, L3302, and L3306 | McpCli owns runtime enrollment, schema support, and its catalog acceptance. Folders has no consumable Core package for this gate. |
| L3312 and L3318 | The receiving implementation specs are still backlog. These handoffs belong to their governed acceptance criteria and the EventStore-owned extension rule. |
| L3324 | The CI timeout edit is reserved for the governed workflow reseal. |

## Review Triage Log

- Medium, fixed. Regenerated the checked-in SDK helper and rebound the pending source inventory. The recovery diagnostic now identifies the client project and tells the operator to remove the stale output before rerunning the incremental target.
- Medium, fixed. The metadata serializer covers the reachable SDK transition audit map, nested JSON objects, and raw JSON. It retains host formatting settings and the spelling of validated raw scalar values. The legacy contract's three reserved names apply to dynamic fields as well as reflected properties.
- Low, rejected. A typed member shadowed by JsonExtensionData can collapse in the token writer. The generated legacy operation DTOs have no extension-data property, and the reachable audit dictionary has unique keys.
- Medium, fixed. The erasure controller's permission provider returns a safe absence when the permissions row or lifecycle is missing. At the actor boundary the regression covers an empty source, a historically possible created snapshot with no organization, and a valid grant folded into the actor state. The actor check also protects against disagreement with the permission projection.
- Low, addressed. The local page-description test verifies the reflection metadata that the pinned SchemaDeriver copies into the schema. It keeps the separately deferred runtime-enrollment gate independent of a new core package dependency. The file-tree result description counts directories as entries.
