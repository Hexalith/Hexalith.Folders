---
title: 'Complete ten deferred work items in ledger order'
type: 'chore'
created: '2026-11-24'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Complete the first ten actionable open items in deferred-work.md, in ledger order. Verify each change, close its entry, and commit it individually. Record why earlier open items were skipped. Run the full solution build and test suite before pushing the verified commits to main.

</frozen-after-approval>

## Implementation Notes

- The ledger has no explicit priority field, so selection follows its existing order. An unrelated archive update appeared during the work. Each commit stages only its own status and resolution lines and associated files.
- DW-9 adopts the SDK's async processor for archive evidence and checks cancellation before the synchronous gate. DW-45 splits in-memory seeding from the lifecycle reader. DW-63 identifies the different-key path in the existing grant test. DW-65 adds replay success semantics while preserving result codes.
- DW-83 supports the end-of-options marker and option-prefixed paths. DW-87 records the exact bytes used to generate the parity oracle alongside the normalized digests. DW-136 and DW-161 reconcile the historical preflight records. DW-167 covers internal and protected fields. DW-172 corrects the Story 1.2 file inventory.

### Entries skipped

- DW-1 and DW-2 depend on unfinished durable projection stories. Current owners are Stories 6.12–6.13 and 4.18, respectively. DW-3 is a superseded scope note. DW-4 refers to retired meanings of NFR51 and NFR52.
- DW-5 intentionally accepts only canonical command names. DW-6 has no non-normalized authoritative tenant path. DW-7's private-constructor event has no deserialization boundary. DW-20 duplicates DW-9.
- DW-24 has no second validator version. DW-27 has no positional production caller. DW-28 has no production writer outside the aggregate. DW-34's repository performs synchronous command-local work and EventStore owns the durable write. DW-37 has no restore or unarchive operation.
- DW-38, DW-40, DW-56, and DW-57 need approval of a Contract Spine revision. DW-39's historical category promotion conflicts with the current protected-operation matrix. DW-41 already uses the EventStore reader. DW-42 has no scoped collaborator.
- DW-58 describes the in-memory effective-permissions store, which the production host already replaces. DW-59 requires an identity provider with established case-folding rules. DW-61's production mutation already uses a cancellable service and the legacy gate has no async repository. DW-62 has no new fingerprint field. DW-66's evidence is constructed in-process and the validator rejects a null action.
- DW-76 belongs to CI/CD workflow ownership. DW-77 has no history-dependent gate. DW-80's diagnostics still have bounded sources. DW-88's C13 oracle does not consume operation schemas. DW-89 has no approved alternate correlation field. DW-91 already has the selected parser-boundary coverage. DW-92 has no matching spine type and changes could alter existing digests.
- DW-103 depends on an approved replacement for the independent scaffold reference inventory. DW-117, DW-130, DW-131, DW-138, DW-139, DW-144, DW-147, DW-148, DW-150, DW-151, DW-152, DW-157, and DW-158 require changes to governed contract or vocabulary artifacts. DW-143 already declares HTTP 429 in v2.
- DW-166 and DW-168 have no observed analyzer failure. DW-171 has no local deterministic PDB requirement.

## Review Triage Log

- Medium, fixed. Schema validation retains BOM-aware decoding, and the digest uses the same byte buffer. A UTF-16 schema regression verifies both parsing and the raw digest.
- Medium, fixed. Baseline initialization now hashes the same candidate and historical v1 bytes used to derive its inventory.
- Low, documented. The raw oracle headers identify the selected contract and schema. The previous spine remains a separate drift-comparison input.
- Medium, fixed. Both argument forms reject unknown names before the generator writes output.
- Medium, fixed. Generator tests invoke the built DLL directly so the temporary test directory cannot change SDK selection.
- Low, fixed. Generator help and its README describe the terminal marker and paths beginning with two dashes.
- Medium, documented. The archive test exercises cancellation of the `/process` callback. EventStore may continue an admitted command after the original HTTP caller disconnects.
- Low, accepted. The mid-flight test blocks the policy provider and verifies request cleanup, zero appended events, and a clean retry. The processor passes the same token to ACL evidence and final reauthorization.
- Low, documented. ADR 0001 records the original story decision. Current production uses its actor-folded repository for the command decision and returns events for the EventStore actor to persist.
- False. The in-memory repository deliberately requires a seed-capable reader when a reader is supplied so writes reach the instance used for queries. The deployed EventStore reader has its own writer and passes the composition test without exposing a seed capability.
- Medium, fixed. The processor, HTTP outcome handling, canonical category map, and rejection projection use the common success classification.
- Low, fixed. The Story 1.2 inventory includes `.gitmodules` and the test README identified by its review record.
- Low, verified. The naming rule is checked by IDE1006. A targeted Roslyn probe produces the expected warnings for all four accessibilities and passes after renaming the fields.
