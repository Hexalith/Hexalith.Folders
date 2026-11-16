---
title: '1.17 Projects G-6 catalog acquisition'
type: 'bugfix'
created: '2026-11-16'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Correct Projects G-6 catalog acquisition in the Hexalith.CiCd owner repository before Conversations prepare-build. Recheck source revisions, bind selection to Projects' reviewed direct Platform gitlink, verify the root Builds declaration and origin, and acquire only the exact missing catalog commit without tags or recursion. Verify catalog bytes and preserve checked-out HEADs and every gitlink. Keep the mapper fail-closed and free of network acquisition.

Prove the missing-object failure and subsequent success in a disposable depth-one repository under run-with-temp. Apply through an existing supported owner mechanism and obtain fresh exact-source hosted CI/G-6 if that mechanism supports this correction; otherwise retain the concrete implementation, integration proposal, test evidence and precise owner blocker. Preserve dependency pins, nested-submodule state, publication/audit/A8 evidence, deployment/activation controls and Story 1.17 tracking.

</frozen-after-approval>

## Implementation Notes

- This is a small reversible owner implementation with no intent gap. Authorized delivery remains conditional on a supported mechanism; no irreversible step is needed for local implementation or qualification. Existing user diagnosis and CiCd migration work are preserved. The explicit no-tracking instruction overrides generic sprint synchronization and story completion; this spec records only the bounded acquisition work.
- Rechecked Projects remote main is 981ceef13a1abc88219925e73c51a23eb9ac50c2. Its direct Platform gitlink remains 829d68b4cb6bf4e27c9baba318555e2e7b3ebbaf even though Platform live main advanced. Select the catalog from that direct revision's Builds gitlink, never from live main or a nested checkout. Builds remains fdda17c0a4538b0f7c99f813c20ca24b49006530. CiCd advanced concurrently by an unrelated documentation commit; current owner base is d3f3ef864c08cd89e390246d1a5d1dedffc4a92b.
- Owner code map: add a self-contained `.github/actions/prepare-g6-catalog/` composite action and acquisition script in CiCd, behavioral tests in `tools/tests/test_prepare_g6_catalog.py`, and an owner integration proposal under `docs/`. Retain source/reproduction/owner observations in an additive Folders evidence packet. No edits to module workflows or Builds mapper.
- Delivery investigation: `tools/deliver.py:apply_module` requires an accepted delivered ref and applies the entire rendered perimeter. `templates/perimeter.json.j2` replaces workflows and sets a Builds pin. Projects is absent from `setup/` and has no accepted delivery ref. The generic CI template has no G-6 preparation extension. Projects adoption is separately planned as CiCd Story 18. Do not register/adopt it or broaden delivery to work around the missing narrow mechanism.
- Verification: focused synthetic Git behavior tests, a real exact-source depth-one GitHub acquisition probe, and unchanged source/control/gitlink checks. The real probe must fail the catalog read before invoking the owner helper, pass afterward, compare exact catalog bytes, and verify idempotence without a second fetch. No product build/test repeats substitute for hosted G-6.
- Implemented the owner composite action, explicit acquisition helper, behavioral suite and adjacent empty-dependency script lock, owner documentation and a complete unapplied Projects integration patch. Seventeen behavioral tests pass on Python 3.14 and the existing locked Python 3.12 runner command without TMPDIR. Tests exercise real named-remote refmap behavior, including a negative control that creates a branch when the protection is removed, plus timeout descendant termination and real process stderr redaction.
- The real-source proof acquires only the Platform-selected catalog from the approved root Builds GitHub origin. Its original read fails with exit 128; the same read succeeds afterward, reproducing exact catalog SHA-256 d098dab50977041a536508f6df7e934f95678a4a0abedd80b28e9bae19ee0637. HEADs, raw index bytes, entries, refs and gitlinks are preserved; a second invocation acquires nothing. Initial pre-review observations are retained with their original helper hashes, separate from the final proof.
- The independent review returned ten findings, all addressed below. No finding is deferred. An unrelated concurrent CiCd Story 12 spec edit is retained as an explicitly hash-bound observation; it is excluded only from task preservation equality and was never edited or staged by this task.
- Local implementation and qualification are complete. Supported owner delivery and fresh corrected exact-source hosted CI/G-6 remain blocked by the absent Projects registration/accepted ref and absent narrow source-preparation delivery mechanism. The existing full perimeter writer would replace workflow policy and enforce a Builds pin. No adoption, owner-contract expansion, direct module workflow edit, commit, push, dispatch or rerun is performed. This spec remains in-progress on that blocker; the parent Story 1.17 and sprint tracker are unchanged.
- Final preservation replay passes with owner source files and all root repository gitlinks/tracking unchanged. Concurrent CiCd commit 66e3a5a3a0e9bb01dd161c19349953594e01a22f adds unrelated Story 19 planning documents; its exact documented source/index advance is retained separately from this task's changes, alongside concurrent Story 12 spec work.

## Review Triage Log

- high, addressed — `remote.origin.vcs` could select a helper despite a canonical URL. Reject transport overrides; regression verifies failure before fetching.
- medium, addressed — inherited Git alternate-object environment could substitute another store. Isolate Git environment and system/global configuration; regression requires acquisition into the Builds store.
- medium, addressed — a symlinked references parent escaped the child-path check. Require canonical direct checkout paths before opening Git; regression covers the parent symlink.
- medium, addressed — named remote refmaps could update refs during exact-SHA fetch. Clear the refmap, disable automatic maintenance and compare refs; the named-origin negative control proves the fixture detects the unsafe behavior.
- medium, addressed — index entry hashes omitted index flags. Compare raw index bytes as well as entries; regression mutates assume-unchanged during acquisition and requires refusal.
- medium, addressed — mandatory TMPDIR broke the existing owner runner. TemporaryDirectory now uses its own reliable cleanup with optional TMPDIR; locked Python 3.12 execution without TMPDIR passes.
- medium, addressed — timed-out descendants could outlive preservation checks. Start a process group, terminate it and wait before returning; regression proves no delayed child write occurs.
- medium, addressed — the stderr test mocked away the boundary it claimed to verify. Exercise an actual failing subprocess through the CLI and reject secret sentinel leakage.
- medium, addressed — failed object inspection could trigger fetch on corruption. Require a clean batch-check result and literal missing marker before acquisition; corrupt-object regression verifies no fetch.
- medium, addressed — URL substitution bypassed configured named-remote refmap behavior. Substitute transport only at the subprocess boundary while retaining origin; both the protected fetch and an unsafe negative control execute real Git semantics.
