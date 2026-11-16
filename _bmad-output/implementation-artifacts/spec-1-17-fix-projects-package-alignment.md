---
title: '1.17 fix Projects package alignment'
type: 'bugfix'
created: '2026-11-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Fix the two Projects package blockers reported in the previous turn. Adopt the McpCli.Abstractions 3.0.0 dependency required by the unchanged signed Folders Client/Contracts 2.0.0 pair and align the release manifest with the selected EventStore 5.1.1 family. Qualify the result with current package-only product builds, individual tests, exact package validation and all five isolated API consumers; update the closure handoff and prepared Projects owner action with actual results.

Preserve production wire contracts, API behavior, strict graph/provenance/consumer gates, prior evidence and existing local fixture repair. Keep DEC-A8-HOLD and Story 11.6 complete, V1Only, T0 unset, activation authority null and tracking unchanged. No commit, push, publication, deployment, activation, audit refresh or nested submodule initialization is authorized.

</frozen-after-approval>

## Implementation Notes

- The explicit follow-up fix authorizes this dependency alignment; preserve existing dirty state rather than asking again. Small reversible footprint: Projects Directory.Packages.props and tools/release-packages.json, plus current owner action/handoff and additive evidence. No local intent gap, public API addition or irreversible action.
- dotnet-inspect public API diff for McpCli.Abstractions 2.0.1..3.0.0 returns no shape changes. Existing ProjectMcpCliDecorationTests assert exact four query decorations, routing/identifier properties, no command enrollment and unchanged serialization allowlists. Retain and run them on the new package.
- Align the central McpCli pin to 3.0.0 and all four packable manifest dependency closures to McpCli.Abstractions 3.0.0 and EventStore.Contracts 5.1.1. Do not re-add Conversations or Folders to packable Contracts, alter validators or suppress diagnostics.
- Preserve every prior sealed packet. New run-with-temp snapshots use the exact Projects-declared Builds 801f268cd1b37ecae41d4fb20d115c055b4c5793 tree from the existing permitted root Builds objects, not a newly initialized nested checkout. All source bytes, commands, SDK observations, logs and metadata are retained in the new alignment packet. Actual committed-source CI and immutable host production remain distinct owner actions.
- Skip parent sprint synchronization and Git writes under the user's explicit controls. Only this bounded package repair can finish.

- Actual final checks: full Release solution build zero warnings/errors; eight product projects 2837 passed, zero failed, one unchanged isolated-Dapr environment skip; 20 Python fixtures; five strict packages and five isolated API consumers; OpenAPI fingerprint 101 cases, FrontComposer inspect and exact McpCli decorations 2 cases. Server output DLL hashes equal the protected signed Folders 2.0.0 pair. No new production C# or validator/CI changes.
- Initial disposable snapshots omitted required source/model metadata and lacked a Git provenance lookup for consumers. Preserve their failures. Complete declared owner inputs and read-only existing source metadata enabled the separately recorded successful final checks; no validator was weakened or synthetic committed candidate created.
- Both product blockers are removed from the current owner action/handoff. Committed-source CI, immutable host lane/configuration inputs, Platform enrollment, distributed/runtime qualification and locked operational attempts remain pending. Controls, all prior sealed packets, thirteen HEADs/indices and 200 historical protected files remain preserved.

## Review Triage Log

1. medium — Evidence runners returned wrapper success despite failed child commands. Future runners now propagate any required subprocess failure; original executed versions and historical outcomes remain unchanged.
2. medium — The current-product reproduction still lacked Folders model metadata. Future runner now materializes the exact declared Folders source, as the successful Integration completion did; retain the original executed runner and six missing-input failures.
3. medium — The seven unit-assembly reproduction command was omitted. Added verify-product.py to the README recipe and scoped that future runner to those seven assemblies, avoiding duplicate broad checks.
4. medium — Dependency archive/file hash claims were not replayed. Verification now reconstructs each selected archive from the actual declared owner Git revision and checks archive digest plus complete regular-file hash inventory.
5. medium — Builds byte capture was missing. Added an explicitly post-run deterministic archive/file reconstruction and verification; future runners capture exact Builds archive/extracted file hashes during execution. No retrospective extraction observation is claimed.
6. low — Original inherited MSBuild environment was not separately captured. Recorded the explicitly later relevant-property observation (empty), narrowed original empty-list claims to command flags, and added relevant environment recording/conflict refusal to future runners. No retroactive effective-environment guarantee.
7. medium — API inspection lacked input/tool provenance. Added a fresh exact-DLL comparison and command receipt with tool version, DLL/package hashes, timestamps and output hash; original comparison remains historical.
8. medium — Package verification accepted partial/duplicate result/ID/dependency inventories. Require all eight expected unique command labels, all five unique package IDs and both actual aligned dependencies in each applicable nuspec.
9. low — Disposed archives cannot be rehashed or recompiled by packet replay. Clarified that archive/asset validation and consumer compilation are historical execution evidence; verify retained logs/nuspecs and reproduce a new cohort. No diagnostic archive becomes an immutable release.
10. medium — Fixture execution lacked a structured command/context receipt. A fresh focused run passes 20/20 and records command/cwd/timestamps/exit/environment plus exact fixture and manifest hashes.

- Deliverability follow-up: package-local ignore exceptions retain every initial packages/ metadata member despite the global packages pattern. No packet member is ignored. No review finding is deferred.
