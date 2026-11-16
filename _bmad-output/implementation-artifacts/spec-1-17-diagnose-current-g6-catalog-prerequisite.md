---
title: '1.17 diagnose the current G-6 catalog prerequisite'
type: 'chore'
created: '2026-11-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resume Story 1.17 from its post-landing handoff. Inspect the existing exact-source CI, diagnose the earliest failed gate, and leave a concrete owner action supported by retained original evidence and a disposable reproduction. Current Projects product CI success must remain distinct from failed runtime qualification and missing immutable host artifacts.

Reuse completed publication, audit, A8, Story 11.6 and repair landing. Preserve historical packets, protected controls and tracking. Report any pipeline defect through its CiCd ownership boundary; do not rewrite delivered workflows, change dependency pins, publish, deploy, activate, initialize nested submodules or perform workspace Git mutations. Keep the parent story open until its operational gates pass.

</frozen-after-approval>

## Implementation Notes

- Investigation resolved the next action without an implementation choice: Projects CI 43881420098, source 981ceef13a1abc88219925e73c51a23eb9ac50c2, has a passing build-and-test job and failed G-6 source preparation. Artifact 14234531847 records HXW009 for the Platform-selected Builds catalog e2bbbd0893f586e4d447ef8358ff747ea3d34435. The initializer acquires depth-one root dependencies; the mapper reads the pinned catalog with git show. Full local history contains that ancestor.
- Small additive footprint: this spec and a compact evidence directory with original run/job/artifact observations, original diagnostic archive, a reproducible Git-object probe and readable owner action. No product API or runtime changes; no irreversible action or unresolved user-visible implementation choice in this diagnostic slice.
- Verification will rehash the downloaded archive against GitHub's digest, inspect exact source/job/attempt identities, reproduce missing-versus-acquired pinned objects in a private TMPDIR repository, and compare protected source/control/index state. Existing runtime/build tests are not repeated to fill a pipeline acquisition gap.
- The generic sprint-sync and commit steps yield to the user's explicit no-tracking-transition and no-Git-mutation boundaries recorded in the parent story and latest handoff. This bounded diagnosis can finish while Story 1.17 remains in-progress and its tracker stays backlog.
- Retained completed exact-source CI outcomes, the original GitHub-digest-verified diagnostic archive, source bindings and owner correction. The disposable depth-one object probe fails the missing catalog read with exit 128; exact catalog acquisition makes the same read pass with exit 0, preserving HEAD and reproducing the selected catalog hash. This is a Git-object prerequisite reproduction, not mapper or runtime qualification.
- The configured independent blind reviewer returned seven evidence-replay improvements. All are addressed below; no application code, workflow or dependency was changed. Historical post-landing packet-only replay passes. Archive acquisition was repeated during review to capture its retrieval record, yielding identical original bytes.
- Final optimized local replay, historical packet-only replay and fresh disposable probe comparison pass. A standalone packet copy passes without original repositories; an inconsistent retained result with a recalculated manifest is correctly rejected. All packet members are deliverable. This diagnostic slice is complete; the parent story and tracker remain unchanged on external CI/host/enrollment/operational gates. No commit or push was performed.

## Review Triage Log

- low — Historical replay previously required checkout-specific Git indices. Added an explicit packet-only mode; the default retains strict local preservation checks, and mode-specific output states its limits.
- medium — Independently valid source blobs did not establish their execution selection. Added Projects-to-Builds/Platform gitlink checks, caller/source identity and Git-blob checks, plus retained job-log initializer/checkout evidence. No test of workflow YAML was added.
- low — Reproduction did not compare its newly computed result with the retained result. Default probe now requires exact agreement; result capture is an explicit authoring-only option.
- medium — Any nonzero catalog read was insufficient to isolate missing objects. Added Git batch-check proof that the commit is missing and retained the failed read's stderr.
- medium — Replay checked fewer claims than the readable packet summarized. Added complete reported job outcomes, artifact-list completeness, diagnostic phase/category, clean mapper-build and empty-stderr checks, plus retained probe subject/outcome checks.
- medium — Revisions and indices do not detect arbitrary unstaged tracked changes. Added a separately dated clean tracked-worktree observation for all thirteen repositories and compare it during local replay. Its post-creation capture scope is explicit.
- low — Archive acquisition was missing from the query record. Re-downloaded the original archive, checked identical bytes and retained the canonical download endpoint, tool and retrieval time, alongside the bounded job-log acquisition. Expiring authenticated download URLs are omitted.
