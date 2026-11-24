---
title: 'Complete the next ten actionable deferred items'
type: 'chore'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Take the next ten actionable open items in the deferred list in ledger order. Implement each item, verify its behavior, record its resolution, and commit it separately. Record the reasons for skipped items and run the complete build and test suite before pushing the commits to main.

</frozen-after-approval>

## Implementation Notes

The deferred list has no overall priority field. The existing order determines selection. The archive and ledger already had local updates that compact completed entries. The item commits stage their own ledger changes and preserve the local compaction.

| Item | Implementation and verification |
| --- | --- |
| DW-28 | Reject blank archive decision fingerprints and document the event invariant. Assert the recorded fingerprint survives replay. |
| DW-80 | Keep the excluded-method warning with a bounded location. Verify an excluded path stays out of the oracle and command output. |
| DW-175 | Detect serialized GitHub PATs, PASETOs, and common Macaroon forms in decision artifacts. Check synthetic controls and redacted placeholders. |
| DW-197 | Verify JWT settings, claim provenance, and endpoint placeholders against one OIDC artifact snapshot. Retain the two negative controls. |
| DW-224 | Move the nullable parent presence rule to the SDK generator's schema-and-field registry. Preserve explicit-null and omitted hashes and the generated output. |
| DW-229 | Check declared NFC before hashing mutation path metadata and before request serialization. Exercise Add, Change, and Remove with composed and decomposed names. |
| DW-239 | Apply each safe-provenance sample's declared channel restrictions to artifact scans. Verify that the event channel permits the sample and provider diagnostics flag it. |
| DW-258 | Scan organization ACL grant, initialization, and denial results and concrete events against the shared corpus. Redact unsafe rejection evidence and verify no append on denial. |
| DW-274 | Validate access commands after tenant authorization and before ACL evaluation. Verify identical malformed-command results across ACL outcomes and zero repository access. |
| DW-289 | Fail contradictory explicit dependency-mode flags before MSBuild executes the requested target. Check both conflicting pairs, both valid pairs, and the single-flag forms. |

### Entries skipped

The [previous batch](spec-ten-deferred-items-2026-11-24.md#entries-skipped) records the reasons for earlier open entries through DW-171. The prerequisites and contract ownership still apply. DW-28 and DW-80 qualify in this batch because the public archive command accepts a blank override and the generator reads the excluded route from its input.

- DW-173. Story 1.1 acceptance owns the exact graph assertion and pins it independently of the build files.
- DW-179. Changing the discriminating fields requires a governed schema and compatibility revision. The historical v1 spine and the pending v2 bytes are fingerprinted.
- DW-184. The current placeholder is `oidc.invalid`. The SDK generator works from local artifacts.
- DW-225 and DW-227. The hashing API is synchronous and the approved parent field remains the only explicit-null presence case.
- DW-233. The metadata guard already covers all four Unix path roots, with a regression for path and checkout diagnostics.
- DW-235 and DW-236. The corpus version check is intentional. The scanner allows concurrent readers and the current CI job runs on Ubuntu.
- DW-240. Diagnostic equality includes the output channel, so deduplication preserves separate channel findings.
- DW-256. The size and retention policy for removed configuration keys remains undecided and the ledger explicitly keeps the item open.
- DW-259. The organization command path needs the production enrollment and authorization proof assigned to the durable owner stories.
- DW-260 and DW-263. The operation shape is still losslessly deduplicated and unsupported action input already fails closed.
- DW-266 and DW-267. Deployment-time allowlist validation belongs to policy deployment. The bounded diagnostic authorization seam has no provider, repository, file, or workspace read surface.
- DW-275 and DW-278. `IsAllowed` is derived from the tenant outcome, and the access validator always supplies a fingerprint on success. The cited inconsistent values cannot reach either branch.
- DW-280 and DW-286. Nullable generated properties would change public SDK signatures across the published package. The change needs a compatibility plan before regeneration.
- DW-281. Both spines now list CLI parity for indexing status, and the CLI ships and tests the subcommand. The cited discrepancy is obsolete.
- DW-282 and DW-284. The production server already registers the EventStore bridge reader. Story 10.8 owns the remaining live round trip.

## Review Triage Log

- Fixed. Regenerated the SDK helper, enforced NFC when decoding as well as encoding, and pinned the existing digest for composed metadata.
- Accepted. The dependency guard uses MSBuild's initial-target mechanism for restore and build. Targetless property introspection evaluates and prints properties without executing validation targets.
- Fixed. Organization ACL commands reject credential-shaped evidence before event creation. Rejections retain the batch operation index after redaction.
- Fixed during server verification. Event admission uses credential-shape checks so valid message IDs containing operation words still work. Rejection output retains the stricter metadata filter.
- Fixed. Excluded-only parity routes skip parameter resolution, and the diagnostic identifies the method without reading out the raw route.
- Fixed. The JWT scan keeps the previous matching behavior for tokens adjacent to other text.
- Fixed during the final suite. A timed-out HTTP probe now retries within the existing browser fixture startup budget.
