---
title: 'Verify generated SDK artifacts and keep typed errors in their own source'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five deferred items in the Folders SDK and its generation checks.

- DW-102. Given a mix of absent and present request schemas, when SDK conformance enumerates the schemas, then it uses standard LINQ to select the present strings and retains the same distinct set.
- DW-105. Given concurrent reads of a typed API error, when readers access its projected Problem Details, then they observe the same cached result without a nested cache type on the exception.
- DW-112. Given a regenerated SDK, when a consumer reads typed error properties, then the existing API exception partial supplies them from handwritten serialization source.
- DW-226. Given an absolute checkout path, when artifact verification cannot read one of its inputs, then the diagnostic identifies the exception type and repository-relative path. Alternate spellings of a valid checkout continue to work.
- OQ2 generated-client verification. Given generated output with a changed principal client file, when a consumer verifies the checkout, then verification fails. The three-argument content check keeps its existing behavior.

</frozen-after-approval>

## Implementation Notes

- Folders started at `e99f87c81edc0faa890cb58a193790e968132004`. All 13 direct dependencies were pulled to their own `main` for the local workspace.
- The generation target obtains fresh NSwag output before computing the principal client hash. The verifier binds all four inputs without including filesystem exception messages in diagnostics.
- The API exception partial retains its namespace, operation context, and public properties. The cache publishes a single projection across readers.

- All 436 SDK tests, 38 PD10 contract tests and 74 CI tooling tests passed. The pending PD10 inventory reproduced byte for byte.
- The local Aspire baseline command timed out during the AppHost build after 120 seconds. `aspire describe` found no running AppHost.

## Review Triage Log

- Medium, fixed. A newer locally edited client could become the next verification baseline. The helper target now invokes raw generation before recording the hash. Isolated regeneration checks that a local edit is erased and both outputs return to their canonical content.
- Medium, deferred. A helper whose self-digest has a valid shape but the wrong value passes the inherited normalization rule. The checkout verifier can tighten this independently of the three-argument API.
- Low, fixed. The client source is prepared in memory. The generator renders the helper, validates the placeholder, and prepares the helper directory before it publishes either file.
- Low, fixed. Missing and malformed helper diagnostics now name the full regeneration target.
- Low, fixed. The path test covers a working checkout and a missing input through a directory symlink on Linux. The normalized-root cases cover both file and directory errors.
- Low, verified. The parallel test exercises the returned projection's reference identity, and the source confirms the exception declares no cache type. Thread scheduling is outside the contract.
- Low, fixed. The provenance documentation identifies the two output hashes and the calling SDK as the comparison source.
