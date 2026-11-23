---
title: 'Keep SDK generation and its verification consistent across checkouts'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close five deferred issues in SDK generation and verify that the checked-in client and helper can still be reproduced from the Contract Spine.

- DW-74. Given an isolated regeneration checkout, when it restores packages, then it uses a byte-for-byte copy of the repository's NuGet configuration.
- DW-94. Given an undefined file-operation kind, when the caller computes the mutation hash, then the call raises a direct diagnostic naming the unsupported kind. Valid mutation and unrelated request helpers continue to hash normally.
- DW-95. Given the generator and SDK tests, when they consume the shared YAML loader and node extensions, then both use an internal API exposed through their existing friend-assembly declarations.
- DW-101. Given empty assembly and application base paths, when SDK tests locate the repository, then they start at the working directory and search its ancestors for the solution marker.
- DW-217. Given the SDK project, when MSBuild evaluates default items, then generator sources and resources stay outside the client build and the two explicit generator project items retain their non-packable metadata.

</frozen-after-approval>

## Implementation Notes

- Work started at Folders commit d1b830348cb6af931d309a4e45faf1e8638919a9 after synchronizing Folders and the 13 direct repositories with their remote main branches. The worktree uses the pinned revisions for verification.
- The client project excludes the generator tree from SDK defaults and reinstates the two explicit non-packable items. The isolated generation fixture also copies the repository's NuGet configuration and the extracted YAML extensions source.
- The mutation regression verifies the direct error from the existing dispatch.
- All 435 package-mode Release SDK tests and all 21 focused PD10 conformance tests passed, including isolated regeneration of the checked-in client and helper.
- All 74 CI tooling tests passed. The updated pending PD10 candidate inventory reproduced byte for byte.

## Review Triage Log

- Medium, fixed. The copied NuGet configuration is now explicit in restore and all three isolated generation calls. A restore graph probe showed that default config discovery also reads ancestor and user settings.
- Medium, fixed. Both incremental generation targets track the YAML parser and extensions through one input property. The conformance check verifies that both targets use that property.
- Low, verified by MSBuild evaluation. The real project exposes exactly two non-packable generator `None` items. A scratch project with generator C#, resource and data files confirmed the default exclusion covers each item type.
- Low, fixed. The invalid-kind regression now checks that the valid mutation and folder creation hashes stay stable after the failed call.
- Low, fixed. The working directory test now exercises the fallback through the same provider call used by normal repository discovery.
- Low, fixed. The internal YAML loader and extracted node extensions have XML summaries.
