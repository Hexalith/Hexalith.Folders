---
title: '1.17 repair Projects release fixture and prepare host artifact'
type: 'bugfix'
created: '2026-11-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Repair the Projects release fixture failure in CI run 43838261389 using the authoritative current package manifest and dependency boundaries. Reconcile the supplied closure handoff first, run the focused fixture suite and applicable current product checks, and prepare the existing Projects/Platform owner lane's concrete next action for an immutable Projects host bound to exact source, signed Folders Client/Contracts 2.0.0 payloads and candidate configuration. Record actual results and exact external inputs/owners in the closure handoff.

Preserve production contracts, gate strength, approval evidence, completed DEC-A8-HOLD and Story 11.6 implementation/verification. Preserve V1Only, null T0, null activation authority and all tracking. Do not commit, push, publish, deploy, activate, refresh the audit or repeat NuGet publication.

</frozen-after-approval>

## Implementation Notes

- Reconciled Projects clean main at bf9f98969efa036fd60700c627e712b4375e654a and existing Folders preparation changes. The user explicitly authorized continuing that local work; preserve every pre-existing change.
- Small reversible local footprint: one Projects fixture, additive evidence/owner preparation and the supplied closure handoff. No local intent gap or irreversible action. Artifact production remains an explicitly reported owner deliverable, not an inferred authorization.
- Reuse tools/release-packages.json and scripts/_nuget.py unchanged. Contracts currently declares EventStore.Contracts, ByteAether.Ulid, Commons.UniqueIds and McpCli.Abstractions. Conversations belongs to the Server's integration dependencies; do not re-add it to Contracts to satisfy a stale fixture.
- Fix test_external_dependency_may_equal_candidate_version to read EventStore.Contracts from the manifest, assert it is external, normalize candidates and preserve the exact external version. Existing exact graph/internal range, metadata, provenance, unsafe archive, API consumer and AD-16 negatives remain active.
- Verification uses run-with-temp for disposable workspaces and retained logs outside TMPDIR. Run the complete focused fixture suite, applicable package-only product/build/consumer gates and focused product fallbacks if blocked. Report committed-input checker limits without initializing nested submodules or weakening audits.
- Parent Story 1.17 remains HOLD; only this bounded repair/preparation can finish. No sprint/tracker sync or Git write is authorized.

- Reproduced the exact KeyError, repaired only the fixture (five insertions/two deletions), and passed all 20 fixtures. Six individual Release/package-mode product assemblies passed 1357 tests with zero skips. Current solution restore and focused Server build remain blocked by NU1109; all five disposable packages pack but strict manifest rejects the EventStore.Contracts version. No dependency/catalog/contract/workflow or audit-policy change was made.
- Added the owning Projects host-artifact next-action runbook and updated the supplied closure handoff with actual results, exact package blockers/owners, immutable image/configuration receipt requirements and Platform registration gaps. Retained the original handoff bytes and every old evidence packet member. No source commit or host artifact is claimed.
- Independent blind-hunter review reported ten evidence-tooling findings. Nine are simple local evidence corrections; one is disproved by the existing HEAD/index invariant. Package-boundary replay retains actual nuspecs, source hashes and SDK observation. Parent controls/tracking and all 200 protected paths remain unchanged.

## Review Triage Log

1. medium — Unsealed verify.py could report success. Require SHA256SUMS and the complete recursive member set before any successful verification.
2. medium — verify-product.py could overwrite original logs/results on reproduction. Require a new --output directory and refuse an existing directory; original evidence remains untouched.
3. medium — The original ad hoc package runner was not retained as an executable procedure. Added verify-packages.py, an explicit command and an independent fresh execution; retain the original limitation honestly.
4. medium — The separate package run lacked its own source hashes. Fresh package-replay/input-manifest.json records every selected copied byte, and verification compares it with the earlier product inputs and exact source/Builds tuple.
5. medium — The rejected candidate's actual version could not be inspected after scratch cleanup. Fresh replay retains all five normalized nuspecs and structured dependency metadata; strict rejection remains a failure.
6. medium — SDK 11.0.100 was stated without an in-run SDK observation. Runbook now cites the pinned global.json; package replay records --info, and the separate post-check observation is labeled as later evidence. Original product flags/configuration remain retained.
7. medium — An empty or altered payload-binding list could self-validate. Require exactly Client and Contracts, all protected handoff fields and the actual retained archive/DLL hashes.
8. false — Newly tracked selected files could evade workspace verification. Existing thirteen HEAD/index hash checks reject index or committed inventory changes; untracked files are intentionally excluded by the diagnostic snapshot procedure. No extra inventory guard is needed.
9. low — Default verification intentionally requires the original unstaged diff, limiting later historical reuse. Added --packet-only to verify the complete retained seal after authorized landing without claiming live tuple equivalence.
10. medium — Duplicated source and result identities were not cross-checked. Verification now compares original/replay source/Builds/fixture identities, handoff fixture/product totals and retained command outcomes.
