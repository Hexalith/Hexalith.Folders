---
title: 'Complete the next seven eligible deferred Folders items'
type: 'chore'
created: '2026-11-25'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete seven eligible deferred Folders items in priority order. Verify each change with applicable tests, close its ledger entry, and create a separate Conventional Commit referencing the item. Record blocked and obsolete entries and continue in order. Run the full local build and test suite, fix failures, and push the verified commits to `main`.

</frozen-after-approval>

## Implementation Notes

Continue the existing ledger priority order and resume the untagged scan after the previous batch. Line references use baseline `fafdb951c9ca76d8c5f28385f54c83f1d6dc343d`.

1. Inspect the actual AppHost environment callbacks for receipt sharing, digest admission, workload credentials, and per-sidecar token bindings.
2. Verify the durable permission callback behavior for a known neutral event, its redelivery, and invalid deliveries.
3. Verify mutation catch-up through the folder evidence provider while only the organization baseline trails its head.
4. Fold folder replay with one mutable fingerprint accumulator and publish only the final immutable map. Compare the retained end state and bound allocations.
5. Wire Claude to the tracked portable relay and verify invocation from a different working directory.
6. Add the original Builds package override and source-mode limitation to the historical Story 12.1 verification record.
7. Pin the reserved actor scope check for `ArchiveFolder` in the production `/process` request handler before permission lookup and replay dispatch.

### Entries skipped

The prior batch records the six remaining explicit high or medium entries and their dispositions. The untagged scan resumed after L2945.

| Entry | Reason |
| --- | --- |
| L2949 | The production reader now delegates to the EventStore `INativeSourceMetadataReader` SDK. The raw Dapr actor call described here has been removed. |
| L2953 | The entry already records the production DI regression and its passing verification. |
| L2971 | Platform now resolves its catalog version from the qualified declaration options. |
| L2974 | The remaining hard-coded Builds AppHost path is in Platform's published validation runtime. |
| L2977 | The later ledger resolution documents both passing subscription cases on the corrected source host. |
| L2982 | The published Builds artifact has passing configured and declarative seed checks. Platform acceptance still needs owner intake. |
| L3000 | The supported delegated owner-read route requires an enrolled consuming workload, a registered proof key, and a matching route catalog. That enrollment belongs to the consuming owner. |
| L3022 | Commit `0ffb6e6a` corrected the upload-artifact pin mismatch. Further blocking-job investigation belongs to CI ownership. |

The shared AppHost test closes both L2948 and L3048 as one item. The ledger contains 215 entries without a closure field or explicitly marked open. This includes the capacity-test follow-up and entries skipped in this and earlier batches.

## Review Triage Log

- Medium, fixed. The reserved-actor test supplies a recognized SDK snapshot wrapper containing invalid event data. If snapshot parsing ran first, the handler would return 503 before the scope denial.
- Medium, settled. The reserved actor cannot dispatch to the folder fold. Valid actor identities with genuinely corrupt retained state still fail through the existing EventStore infrastructure path.
- Medium, fixed. The Workers test now redelivers both the latest and an older sequence and checks the unchanged permission ETag.
- Medium, fixed. Both invalid Workers deliveries emit `folder_permission_failed`, tying the retry response to the intended stage.
- Low, fixed. The ledger records the remaining retry policy for invalid deliveries and points to the dead-letter sequence-gap entry.
- Low. The AppHost model test verifies secret references and sharing. Aspire's persistence wrapper is internal. The live boot verifies configuration startup, while restart persistence is outside this batch's model test.
- Low. A keyed single-event fold may allocate one extra immutable state record. The batch fold publishes that record once, and operational compaction bounds the discarded fingerprint map to one entry per event.
- Medium, fixed. The duplicate AppHost entry is closed with the original.
- Low, fixed at delivery. The verification section records the local results. The push is checked against the remote branch after the commits are complete.

## Verification

- Restored `Hexalith.Folders.slnx` in Debug with `UseHexalithProjectReferences=true` and `UseNuGetDeps=false`. The full solution build with the same properties, `--no-restore`, `-m:1` and `-nr:false` passed twice with zero warnings and errors.
- The 2,500-policy-update replay regression measured 861,606,616 allocated bytes before the change. Both replay paths pass the 64 MiB ceiling after the change, with matching fingerprints and event-derived state. The complete replay class passed all 31 tests.
- The complete Workers project passed 194 tests on the final test source. The final request-handler class passed all 23 tests.
- The isolated Aspire AppHost reached healthy state and stopped cleanly. The opted-in cross-process boot test and all six explicit Keycloak component cases passed.
- All five configured Claude hooks relayed their event from another working directory to a disposable project path containing spaces. Normal interactive `Stop` stayed silent. BMAD Loop's project validator recognizes the relay and reports separate existing plugin-permission and sweep-manifest gaps.
- Ran all 15 solution test projects individually. The complete Server project passed 1,532 tests through the Keycloak verifier. Contracts passed 775 tests and browser E2E passed 64. The default profile skipped ten opt-in AppHost cases and two opt-in Forgejo cases. The six AppHost cases marked explicit ran in the separate component lanes above.
- The full core run had one failure in the 10,001-position concurrent capacity case. The one serialized rerun passed all nine capacity cases. The ledger records the intermittent failure.
- `python3 -m pytest -q scripts/tests` passed 76 tests and 89 subtests. The whitespace verification, pending conformance-manifest check and quick lifecycle load run passed.
