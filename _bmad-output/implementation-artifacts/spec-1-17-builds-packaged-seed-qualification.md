---
title: 'Story 1.17 Builds packaged seed qualification prerequisite'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="user-authorized reversible preparation; Story 1.17 remains open">

## Intent

Builds' G-4 package qualification must exercise configured and declarative projection subscriptions against the EventStore seed extracted from the actual distributable module-tool archive. Source-host tests alone cannot satisfy the gate. Preserve Platform's HTTP and receiver-metadata expectations and existing behavior. Retain exact archive/seed hashes and independently validate that proof before release publication.

Prepare and verify local diagnostic packages without publication, using an observed available version solely as an isolated diagnostic identity. Preserve clean-source, release-eligibility, catalog-pin, tool-range, fixture and provenance checks. Dirty or uncommitted preparation remains release-ineligible. Refresh release observations through read-only queries; invent no future versions, commits, tags, provenance or acceptance. Reuse passing evidence only for unchanged inputs.

First-install preparation and necessary reversible local changes in owning repositories are authorized. Preserve V1Only, unset T0, historical approvals and the Story 1.17 tracker. Do not claim staging qualification or story closure. Publication, deployment, Git staging/commit/push and production activation require separate authorization. Finish with verification, exact artifact hashes, the remaining blocker and concrete next commands/authorization.

</frozen-after-approval>

## Implementation Notes

- Baselines: Folders `9fe3fd38c0da74e804dd87c20d2ed59f93085eae`; Builds `728174f506d56204657e0c52e98a4453aef79f6a`; Platform `1711deba08a4e66b46acfff8d08e7f2ad7f74ba1`. Existing dirty preparation is explicitly authorized and preserved. No tracker synchronization or commit step applies because the user forbids it.
- Bounded tooling change; no production API or behavior change. Reuse `Tools/test-g4-tool-package-contracts.ps1`, shared qualification functions and publisher validation. Add a small separate test-host probe under `Tools/`, with the already cataloged MVC testing package and no source-host reference. Extract the seed from the archive and bind loaded assembly bytes to it. Remove only the asserted startup index writer hosted alias, as Platform does.
- Preserve configured discovery HTTP 200, exactly one projection topic with `configured-seed-events`, and matching receiver metadata; declarative discovery HTTP 404 and an existing receiver without topic metadata. Missing, stale, malformed or incomplete package proof must block inventory/publication.
- Extend publisher/self-test coverage and Tools documentation. Keep source fixture and catalog guards intact. Run focused contract/mutation checks and the existing G-4 diagnostic lane; record any exact broad blocker separately. Verify old published seed fails the new check.
- Retain Builds-owned qualification receipt/logs and Folders addendum/deferred-entry resolution. Hash protected inputs and all Platform files before/after. Prior admission evidence remains historical and unchanged.
- Context: current cached Epic 1 context is fresh; Story 1.16 continuity requires fail-closed evidence gates and exact prerequisite diagnostics. Story 1.17 remains `backlog` by explicit user direction.
- Implemented the separate MVC Testing probe and mandatory package gate. Publisher independently binds the proof to its exact archive/seed and validates typed HTTP/receiver results. Platform files, the catalog and prior seed/audit preparation were preserved.
- Full source validation builds successfully but stops at 31 existing synthetic acceptance-corpus failures (`HXE202`, stale EventStore 3.120.0 fixtures versus current catalog 3.122.0). The required-controls package run passes the new seed checks, then independently refuses the same P0 positive fixture and writes no complete inventory. No bound synthetic fixture or approval was rewritten to conceal this blocker.
- The diagnostic package lane succeeds with explicit skipped source/control modes and `releaseEligible=false`. Its exact retained archives were rechecked with the final reviewed probe/validator; unchanged packaged source allows retaining those archive bytes. The signed published 4.31.2 archive independently fails both modes. No release qualification is claimed.

## Review Triage Log

- High, patch: array-valued hashes/pubsub could pass PowerShell comparisons. Explicit scalar/string-or-null checks now reject them; publisher mutations refresh the outer hashes to exercise semantics.
- High, patch: array-wrapped proof/identity/check objects could pass member enumeration. `-NoEnumerate` and explicit object/array shape checks close this path; publisher wrapper controls added.
- Low, patch: killed probes could leave a separate extracted archive directory. Extraction now lives under the parent-owned scratch directory and is removed by the parent after timeout.
- Medium, patch: automatic HTTP redirects could hide the discovery endpoint's direct status. The test client now disables redirects.
- Low, patch: an evidence file created after preflight could be overwritten. The final write uses exclusive `FileMode.CreateNew`.
- Low, patch: missing/duplicate receivers or malformed JSON could prevent the second mode and structured failure retention. Structural assertion/JSON/key failures now produce a safe exception-type observation, both modes are attempted, and the process fails.

- Final review corrections are verified against the retained package; the artifact parser suite now passes 63 scenarios, including 24 scalar-array variants and four object-wrapper variants. The runtime type check deliberately uses `System.Management.Automation.PSCustomObject`: the `[pscustomobject]` accelerator maps to `PSObject` and can still match a wrapped root array. This spec is done only for bounded local implementation/diagnostic preparation; full G-4 release qualification and Story 1.17 remain held.
