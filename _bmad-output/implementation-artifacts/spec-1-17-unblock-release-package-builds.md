---
title: 'Unblock Story 1.17 Release/package prerequisites'
type: 'chore'
created: '2026-10-14'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized prerequisite scope">

## Intent

Folders and Projects Release/package builds fail because the shared EventStore 3.120.0 pin lacks the safe-denial, human-authority-receipt, and bound-owner-query APIs already consumed by current source. Verify that EventStore 3.121.0 publishes these APIs together from one exact source revision, update the owning Builds catalog, and rebuild both complete CI solutions with warnings as errors. Rerun affected authorization, receipt, Folders EventStore adapter, and Projects Folders adapter tests against fresh Release assemblies; retain commands, package identities, hashes, and results in Story 1.17 readiness evidence.

The user authorizes necessary EventStore release operations and central dependency-pin updates for this prerequisite only. Use the existing release workflow if publication is required. Preserve all test expectations and gates, V1Only, unset T0, historical approvals, and Story 1.17's tracker. Do not deploy, start a migration window, claim production entry or Story 1.17 closure, initialize nested submodules, or change consumer behavior. Existing publication makes this a small reversible catalog and evidence change.

</frozen-after-approval>

## Implementation Notes

- Existing release v3.121.0 resolves to `21b34c01ec919769cfba9cfe0d47e50886b57ded`; successful release workflow run `38523534652` published fourteen packages on 2026-10-14. Inspect the actual NuGet assemblies and package provenance before selecting the release.
- Own the pin in `references/Hexalith.Builds/Props/Directory.Packages.props`; Folders and Projects already import it. Preserve its BOM/CRLF format and all unrelated version selections.
- New evidence goes in `evidence/story-1-17-package-unblock-2026-10-14/` with an addendum to `story-1-17-current-candidate-technical-readiness-2026-09-24.md`. Prior records remain historical.
- Captured hashes of 29 historical/control files and 6,323 source/test/gate files before catalog changes. User's explicit tracker constraint overrides skill tracker synchronization; this prerequisite never advances the canonical story.
- Prior Story 1.16 continuity: reuse governance/completeness and twelve-category parity commands; retain fail-closed prerequisite results and metadata-only diagnostics.

- Completed both complete CI solution restores/builds in the authorized package mode with compiler warnings as errors. Folders: zero errors and three existing NU1903 audit warnings; Projects: zero warnings/errors with its retained audit environment pin.
- Release tests passed: Server authorization/receipt 133/133, domain authorization 150/150, EventStore adapters 58/58, Projects authorization/Folders adapters 61/61, routing/golden parity 114/114. Existing governance and all twelve parity categories pass. Runtime EventStore DLLs in all three inspected consumers match the published 3.121.0 packages; Projects Folders pair remains 1.2.1.
- Recorded exact commands, exit codes, canonical revisions, fourteen package archive and DLL identities, and preservation evidence in the readiness addendum and structured record. Preserved 29 historical/control files and 6,323 source/test/gate files. No production entry or tracker transition.
- Independent review led to stronger evidence: full baseline retention, versioned checker snapshots, collector command result, tool/environment identities, all unsigned package entries, expected runtime inventories, all nine executed assemblies, and explicit before/after report hashes. All new checks pass. No findings are deferred.
- Only the prerequisite spec is complete. The canonical Story 1.17 tracker remains `backlog`. Commits/pushes are unnecessary for the already-published prerequisite and were not performed; the Builds pin and Folders evidence remain reviewable working-tree changes.

## Review Triage Log

- Medium, patched: preservation digests initially lacked the full input inventory. Retained `baseline.json` and bound its SHA-256 in readiness evidence; all 6,323 per-file comparisons and 29 protected comparisons pass.
- Medium, patched: historical checker versions initially shared a mutable command path. Retained each version, associated snapshots/hashes with each command record, and explicitly marked snapshots reconstructed from recorded edits.
- Medium, patched: collector assertions initially had no command result. Recorded the collector invocation and exit-0 log covering runtime, inventory, and preservation assertions.
- Medium, patched: tool versions/environment were absent. Recorded actual inspector version and cached package hash, SDK/runtimes, Python, PowerShell, GitHub CLI, Git, and relevant environment, with observation timing explicit.
- Low, patched: DLL/XML comparison omitted other package entries. Final verification compares every unsigned entry and explicitly excludes NuGet signing data; all fourteen match.
- Medium, patched: runtime enumeration could pass without expected DLLs. Derive expected runtime assets from restored test graphs, require nonempty inventories and explicit required package sets, and assert every expected DLL exists.
- Medium, patched: initial binary fingerprints covered only three output directories. Retained all nine executed test assemblies plus their EventStore dependency hashes; every present EventStore dependency matches the released payload.
- Medium, patched: gate-report preservation claims lacked explicit retained hashes. Compare both default reports with the exact clean Folders revision; record equal before/after hashes.
- Low, patched: identifier substring scans alone do not establish public metadata identity. Clarify their limited role and retain the separate fully qualified inspector output and successful consumer compilations.
- Medium, patched: publication replay depended on a local tag and latest-eight run selection. The final verifier uses the recorded immutable source, exact workflow run ID, and retained manifest; the command exits 0.
