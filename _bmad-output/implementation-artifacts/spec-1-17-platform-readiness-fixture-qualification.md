---
title: 'Qualify Platform infrastructure readiness from the Folders workspace'
type: 'bugfix'
created: '2026-10-17'
baseline_commit: '24fdf3de046fbd20866284ed0cf9caa27369f3de'
platform_baseline_commit: 'b54dc5f2e8fe070db9a0ca3a72ad495b380d2b5e'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
tracker_policy: 'no-automatic-transition'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The current Platform composition test project builds in explicit Debug mode, but all six existing infrastructure readiness cases fail before their protocol assertions. Their shared fixture assumes owner checkouts nested under Platform rather than the root-declared Folders workspace owners. This prevents current local Story 1.8 protocol evidence before Story 1.9 tool acceptance.

**Approach:** Change only the readiness fixture to resolve its source mapping with the existing Builds workspace resolver from Platform's identity anchor. Retain the six real model registration cases, fragmented PING/PONG versus ERR assertions, deadline and cleanup. Record exact before/after commands, source identities, preservation checks, prerequisite order and the next owner decision in a new Platform-owned evidence receipt. Reuse the existing Folders current-packet and Platform pin checks.

This bounded run preserves every historical packet, receipt, approval, replay, declaration snapshot and tracker. Keep Platform HOLD, V1Only, unset T0 and activation authority, unconfirmed first-install scope, fourteen unexecuted staging checks and Folders Story 1.17 backlog. Do not accept a tool/range, update dependencies or gitlinks, initialize nested submodules, execute staging, publish, deploy, stage, commit or push. Model/protocol tests supply local evidence only. Do not repair other suites or implement intake/publication in this run.

</frozen-after-approval>

## Implementation Notes

Investigation selected the earliest reproducible local verification blocker. Explicit `-c Debug --no-restore -m:1 -nodeReuse:false` builds the real Platform composition test project with zero warnings/errors; the historical namespace blocker does not reproduce. The existing six infrastructure cases fail on absent nested EventStore/Memories/Tenants declarations. Platform pin verification and its 11 tests pass; the Folders current diagnostic and 39 tests pass.

The owner implementation is `references/Hexalith.Platform/tests/Hexalith.Platform.Composition.Tests/PlatformAppModelReadinessTests.cs`. Preserve shared `Options()` and package-mode fixtures. The source resolver retains identity findings and initializes only missing direct references; inspect initialized current workspace owners before running it. No package admission or live host run is part of these tests. All retained logs and XML results will be copied into an additive Platform evidence directory. Story tracker synchronization and the skill's commit step are overridden by the explicit user boundaries.

The fixture now uses `WorkspaceRootResolver.ResolveAsync` from the existing Platform anchor, with all eleven root Git markers and HEADs prechecked against their recorded gitlinks. No checkout was initialized or moved. The real tracked Platform project builds with zero warnings/errors; all six existing protocol cases pass (previously six failed). Folders current consistency and all 39 safety tests, plus Platform pin consistency and all 11 pin tests, pass. No new tests or runtime changes were needed.

The additive receipt is `references/Hexalith.Platform/docs/implementation/story-1-17-readiness-qualification-2026-10-17.json`, with ten retained command/XML artifacts in the adjacent directory. Platform `.gitattributes` preserves their exact bytes. The receipt records full canonical source revisions, the local-only scope, actual catalog/tag observations and remaining prerequisites. Offline tag inspection finds EventStore 3.124.0's declaration differs from Platform's snapshot; the standard v2 declaration path is absent at the retained Tenants, Memories, Folders, Projects and FrontComposer catalog tags. These are local Git observations, not remote or artifact attestations. Published Builds 4.32.0 source predates the reviewed startup follow-up, so exact-artifact qualification and an explicit Platform tool/range/catalog decision remain required. No acceptance or authority was inferred.

Review follow-up keeps the source correction narrow: add a 30-second resolver cancellation bound and emit the actual resolved mapping through the existing xUnit output. The unchanged five-second protocol deadline remains separate. Rename new `.log` artifacts to `.txt` because Platform ignores logs; byte contents remain retained and Git visibility is checked. Extend the receipt with actual mapped declaration hashes/clean source observations, final executed DLL hashes, exact tag inspection paths/commands, audit output and an inline offline receipt-integrity command. The initial failure executable was not hashed before replacement; that limitation is explicit rather than reconstructed.

## Review Triage Log

One independent Blind Hunter layer completed; the oneshot workflow configures no other review layers. Its finding floor was nine. Each finding was inspected individually.

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| R1 ignored retained logs | Medium: `*.log` ignores the new artifacts, so ordinary packaging would omit them. | Patched: rename new files to `.txt`, update references and confirm none are ignored. |
| R2 resolver can initialize missing direct references | Low, rejected for this bounded qualification: all eleven existing Git markers and HEADs were prechecked and stayed at their gitlinks. Initialization of missing direct references is established source-resolver behavior; duplicating its workspace parser/initialization policy in this protocol fixture would expand this task. The source-mode fixture requires the initialized workspace recorded by the receipt. | No new resolver or framework; retain the explicit current-workspace precondition and audit. |
| R3 resolver lacks fixture deadline | Low: the newly called resolver otherwise inherits only the test cancellation. | Patched: linked 30-second bound before resolution, retaining the separate five-second probe bound. |
| R4 mapping not retained | Low: source heads alone do not show the actual returned mapping. | Patched: serialize mapping, content hash and owner paths into the existing xUnit result output. |
| R5 mapped declarations not hashed | Medium: a source HEAD does not establish working-tree declaration bytes. | Patched: actual EventStore/Tenants/Memories declarations and Builds catalog hashed with clean source-state observations. |
| R6 executed binaries not bound | Low: existing restore assets and retained source logs alone do not identify final executable bytes. | Patched: final test/model/tooling DLL hashes before and after execution; explicitly retain the limitation that before-run binaries were not captured. |
| R7 preservation audit not retained | Low: summary counts lack the supporting command stream. | Patched: additive read-only audit transcript and exact inline integrity command, without duplicating the prior preservation framework. |
| R8 tag observation paths/commands omitted | Low: the receipt described the standard declaration path only in prose. | Patched: repository, exact path and both Git commands per observation. |
| R9 new receipt integrity command omitted | Low: the prior-packet checks do not validate this new receipt. | Patched: record and run the exact inline offline hash/source/XML validation command and result. |

Final verification after review: the normal explicit Debug build again passes with zero warnings/errors; all six original readiness cases pass with no skips in the tracked test assembly. Their actual mapping is retained in XML/output, and the test/model/tooling DLL hashes match before and after execution. The receipt's recorded offline integrity command passes all fourteen retained artifacts, eight Platform source inputs, four mapped owner inputs, the executed binaries, unchanged indexes/heads and the 42/45/25 protected bindings. No retained artifact is ignored. Both repositories pass `git diff --check`. All eight actionable review corrections are verified; the source-resolver policy finding is explicitly rejected as low within this prechecked workspace. Nothing is deferred. This bounded local fixture qualification is complete; Platform operational qualification and Story 1.17 remain HOLD/backlog, and no Git recording or publication was performed.
