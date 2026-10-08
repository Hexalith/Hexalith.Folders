---
title: 'Establish Story 1.17 release-artifact identity'
type: 'chore'
created: '2026-10-08'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="explicit user instruction and accepted conditional migration scope">

## Intent

**Problem:** Story 1.17's first migration entry gate lacks an exact current-candidate release/artifact tuple. Released Client/Contracts 1.1.1 represents an earlier candidate even though Projects read-adapter checks pass against it.

**Approach:** Compare released and current source identities, prepare and validate the matching current Client/Contracts pair through the existing five-package release tooling, and build/test Projects against those exact local archives. Record full revisions, versions, hashes, commands, results, publication prerequisites, and Platform-owned deployment gaps. Preserve historical approvals and reconciled OQ4 evidence; keep V1Only active, T0 unset, and Story 1.17 open. Execute local preparation within existing authorization and prepare any dependent release proposal without inventing approval or treating local artifacts as publication/deployment.

</frozen-after-approval>

## Implementation Notes

- Investigation: the latest OQ4 addendum clears governance with full Contracts 347/347; current conformance inventory has 224 paths. Accepted Projects migration package and September 24 single-owner policy authorize conditional pair publication and bounded coexistence, with technical and operational gates still required.
- Reuse `tests/tools/run-release-package-gates.ps1` in DryRun mode, `scripts/pack-release-packages.py`, package/symbol/dependency validators, and isolated package consumers. Preserve the five-package inventory; archive pre-existing local outputs before tooling replaces them.
- Use local version `0.0.0-local.story117.02152bace0c7`, source revision `02152bace0c782b2b65bf9ccd31e4738a94eac17`; this is a preparation identity, not a selected public release version. No code/API or dependency catalog change is needed. Product source remains committed; new evidence/spec documentation is outside the conformance allowlist.
- Projects revision `caf3721427f0b4834415369c2b4a5f981725474f` remains clean. Override `HexalithFoldersVersion` only for the verification commands, use Release/package mode and a local source, inspect resolved assets and package hashes, then run existing lifecycle/permissions/metadata/denial/unavailability adapter tests. Restore the original package build state after retaining candidate evidence; do not alter the tracked released pin.
- Append the current readiness result and migration package clarification; retain structured metadata in `evidence/story-1-17-release-identity-2026-10-08.json`. Preserve historical text and all bound planning/OQ4/tracker/routing bytes. No tracker transition or Git recording operation is required or authorized.
- Review with the skill's independent blind-hunter layer, then verify preservation, current inventory/provenance, package hashes, and precise evidence boundaries. No new irreversible action or unresolved intent choice is part of this local preparation.
- The tooling rejected the original local prerelease identifier because its hash component started with a digit; corrected it to `rev02152bace0c7` without changing version policy. The complete dry run passed, as did full Contracts 347/347, Client 329/329, governance 23/23 + 7/7 + 3/3, and all twelve parity categories.
- Read-only release preflight finds current live main equal to the candidate, successful exact-source push CI 37842520868, and the existing publication variable enabled. The configured commit analyzer reports a minor release from the 29 commits since v1.1.1, proposing `1.2.0`; all five NuGet indexes lack that version. Prepare a second DryRun at this concrete public-version candidate and verify Projects against these exact local `1.2.0` bytes. This changed package-version input needs new package/consumer checks, not another owner signature. Preserve the preliminary local artifacts separately. Publication remains a dependent guarded operation, outside the requested preparation; never call these unsigned local archives published.
- Concrete `1.2.0` DryRun passes all package/symbol/closure and isolated-consumer checks. Projects restores the exact pair from mapped local source into an isolated cache, force-builds with zero warnings/errors and passes both read-adapter classes, 39/39. Asset SHA-512, archive SHA-256 and actual test runtime assemblies match the prepared pair. Retained its runnable candidate build and assets under `nupkgs/projects-consumer-1.2.0/`; restored the original 1.1.1 package restore/build state with zero warnings/errors. Tracked Projects files stay clean.
- Appended the exact identity/gap/proposal readiness addendum and migration-package update; created structured metadata for all ten candidate archives, released comparison, consumer outputs, commands and controls. The first gate still lacks current publication and Platform deployment identities; other operational gates and durable-host work remain pending. Existing conditional publication acceptance applies; the release workflow independently requires its protected production review. No new decision or release dispatch is claimed.
- Final technical audit before independent review verifies unchanged 224-path inventory, two identical generations, all 20 provenance bindings, all eleven root revisions and 35 protected file digests. Updated current-status text supersedes historical OQ4 failure reporting without rewriting any historical approval or prior addendum.
- Moved retained candidate consumer outputs and older package/report backups beneath the already ignored `nupkgs/artifacts/` directory; their final paths are `nupkgs/artifacts/projects-consumer-1.2.0/` and `nupkgs/artifacts/history/`. This preserves all local binaries without adding them to the source diff or changing ignore configuration. Structured metadata and readiness links use these final paths.
- Independent review identified ten evidence reproducibility gaps. Patched them with a full consumer source template, explicit retained file/log paths, a 382-file runnable bundle manifest, named offline validator versions/reconstruction, archived prerequisite binding, reproducible configured analyzer and source/payload audit scripts, and timestamped read-only GitHub observations. No package or candidate bytes changed. The retained 1.2.0 consumer reruns 39/39 after restoring Projects' normal 1.1.1 build state; final report conformance passes 9/9. The reusable offline audit passes and rejects a tampered package digest with exit 1.
- Reviewer's follow-up verifies all ten fixes, passing offline audit/analyzer reproduction and independent Projects 39/39 plus release conformance 9/9 reruns. No unresolved finding or deferred work remains. This bounded preparation spec is complete; parent Story 1.17 remains backlog/open, V1Only and unset T0 are preserved. No commit or push was made: the user's repository instructions require such recording operations to be explicitly required by the task.

## Review Triage Log

- Medium, patch: missing NuGet configuration reproduction. Added complete source-clearing/feed/mapping template and root/scratch resolution instructions; candidate archive and asset identity checks already matched.
- Medium, patch: original consumer paths no longer match candidate hashes after restoring released build state. Added explicit retained paths for every build/asset hash and audited all six.
- Medium, patch: original test executable now uses the restored pair. Added retained candidate executable command and reran it, 39/39.
- Low, patch: test log digests had no retained locations. Retained all five original test logs and identified their paths; added retained-bundle rerun evidence separately. The offline audit checks every digest.
- Medium, patch: selected consumer hashes did not identify the complete runnable bundle. Added ordinally sorted 382-file manifest and its digest; audit checks all files and rejects inventory/content drift.
- Medium, patch: unnamed offline validator environment. Recorded Python 3.13.13, relevant exact dependency versions, reconstruction and activation commands; no installation/dependency update was executed.
- Medium, patch: SkipRestoreBuild prerequisite relationship lacked its retained report/hash. Bound the archived same-run preliminary report, source/version and complete solution prerequisite explicitly.
- Medium, patch: analyzer version proposal lacked reproducible invocation/input identity. Added read-only configured analyzer script and recorded exact range/options, Node/package versions, release configuration and lockfile hashes; rerun still proposes minor/1.2.0.
- Medium, patch: source and signature-excluded archive comparisons were not reproducible. Retained released signed/GitHub archives and added offline audit for 223 released paths, 224 current paths, 28 differences and exact payload equality; positive and tampered-digest negative checks pass.
- Medium, patch: GitHub preflight observations lacked individual query timestamps/commands. Repeated the four read-only queries and retained start/observation times, response digests and bounded results. Main, exact CI, variables and required reviewer still match; no dispatch occurred.
