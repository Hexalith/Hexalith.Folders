# Story 1.17 current-candidate technical readiness

Evaluated 2026-09-24, with final digest observation at 2026-09-24T16:17:55Z. Source revision: `19c29e00eb6679bbf7a0d20a8b4dbf30182243cf` plus the uncommitted Story 1.17 candidate on `feat/story-1-17-v2-closure-prep`. **Result: current A6b technical validation and Section 9 checks pass; the current A8 technical condition is met. Projects v2 testing and production exposure remain gated.** This is a technical result under the [single-owner decision policy](../../docs/governance/approval-policy.md) and the [accepted Projects migration package](story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md), not a new historical role approval or an exposure record.

## Candidate identity and A6b result

| Input | Current exact evidence |
| --- | --- |
| Authorization matrix 2.0.0 | `docs/contract/authorization-matrix.md` SHA-256 `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420` |
| Generated manifest | `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` raw SHA-256 `e89ee58008e3ff4b34d08eac6e8d6a3d357d7df3ce7b1f13eadd53781e8e500c` |
| Candidate set | Declared SHA-256 `d2c30653d9292c4d61122620cff5a61ebc1f7de1e2c11a6daf64c11d540a33cd`; 206 listed artifacts; all 206 listed raw-file hashes matched current files |
| Historical v1 | OpenAPI SHA-256 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2` |

Two isolated invocations of `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root . --output <scratch>/a.yaml` (then `b.yaml`) produced identical 38,488-byte files, each byte-identical to the checked-in manifest. The manifest's matrix digest matches the current matrix. The focused Release contract classes passed: `Pd10ConformanceSetTests` 3/3, `Pd10V2CandidateContractTests` 5/5, and `AuthorizationMatrixContractTests` 7/7. The denominator and canonical response checks also passed in the full 342/342 Contracts suite. **A6b current-candidate technical result: pass.**

The preserved [2026-09-22 A6b approval](../planning-artifacts/authority-relock/2026-09-17/A6B-OQ3-PD10-CORRECTIVE-APPROVAL-2026-09-22.yaml) binds manifest `4c8f33f0cb00afac3a5541eab7b9d30ec15b832d6114316ff672338cc02f5be7`, candidate set `c5bea4292e918605ba9a4331012806fc0e3e3da818dea66ae68c080929bf2c7f`, and 196 artifacts. It is valid history for those bytes; the current technical pass does not relabel it as an approval of the 206-artifact candidate. The owner's accepted conditional plan requires rerunning changed-byte checks without a new role-by-role signature.

## Current-input Section 9 replay

The current planning manifest is SHA-256 `7ad9fa657b649ed095b9ca490fe01568b8efd6cefe61b3eab074f5d7580addc0`. Its 20 top-level provenance entries now match current files, including the Story 1.17 governance test source `bfdf6276b7457819b3836a9da90b36687e6c16e537ff5e3d244edbe4fdede810` and generated conformance manifest above. Its approval-register binding matches the current register SHA-256 `2551bdecb3b07a3920ff5f76c27fa52e5c95834fe0da6207a4227cd6a4b031ff`. The historical `post_a8_finalization` bindings and approval records were not rewritten.

| Check | Current result and evidence |
| --- | --- |
| S9-01 decisions | Pass under the 2026-09-24 policy: the register retains exactly eleven A1–A8 gate records. Each record's decision-payload hash matches its file, required and recorded authorities match, and each recorded approval binds its decision payload. The owner accepted the conditional Projects plan. Earlier exact-digest approvals remain tied to their original inputs. |
| S9-02 provenance | Pass: 20/20 current top-level manifest paths and the register binding match raw SHA-256 bytes. `sprint-status.yaml` remains `230889efd5d5e5cfa18b5bbd307e093a581f2434656eae85d6f12ff9573b7c26`. |
| S9-03 requirements | Pass: normalized inventories are exactly FR1–FR58 and NFR1–NFR84. |
| S9-04 NFR traceability | Pass: `NfrTraceabilityConformanceTests` 17/17. |
| S9-05 lifecycle | Pass: all 158 canonical stories resolve to one matching tracker key; 111 done, 44 backlog, 2 in progress, 1 review. Story 1.17 is backlog. |
| S9-06 frozen lifecycle | Pass: the two frozen Story 3.14 specifications match their 2026-09-22 recorded hashes; Story 3.14 remains backlog. |
| S9-07 A6b inventory | Pass on current inputs: two isolated generations equal the checked-in 206-artifact manifest and its current matrix binding. Historical 196-artifact approval remains historical. |
| S9-08 dependency graph | Pass: 72 ranked nodes, 248 unique edges, 23 accepted-prefixed edges resolving to accepted terminal references, zero unresolved edges, unranked nodes, or rank errors. Strictly lower prerequisite ranks preclude cycles. |
| S9-09 verification | Pass: full Contracts 342/342; focused conformance 3/3, v2 candidate 5/5, matrix 7/7, governance 22/22, NFR traceability 17/17; governance/completeness gate passed; parity gate passed all 12 categories. Debug source and Release package solution builds passed with zero warnings/errors. |
| S9-10 authority | Pass: the September manifest remains current execution authority; no current-authority claim for the August 4 snapshot was found in the manifest, register, Epic 1 context, or active Story 1.17 spec. |
| S9-11 hold condition | Pass for current technical inputs: S9-01 through S9-10 pass. The historical general hold removal remains recorded; this replay does not activate v2 or close Story 1.17. |

Commands and artifacts: `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseNuGetDeps=false -m:1`, then its `dotnet build --configuration Debug --no-restore -m:1` (56 projects); matching restore and `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1` (33 projects); force rebuilds of the Contracts, Client, CLI, MCP, and Integration Release test projects with `--no-incremental -warnaserror -m:1`; the full Release Contracts test executable; `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild`; and `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore`. Each exited 0. The final parity report is [latest.json](../gates/contract-parity-ci/latest.json), SHA-256 `53439288efd4dd7e2163926e6a9d60ebb1e929e1225e209525c24640105b17d3`; the governance report is [latest.json](../gates/governance-completeness/latest.json), SHA-256 `4a5630580f3aec35925bef605bfa2616dc16452024abc592ca2be21d074997a7`.

The first full Contracts invocation used an old Release assembly and failed five approval-age cases (337/342). A forced rebuild from the current source removed that stale-assembly failure; the full rerun passed 342/342. No historical approval content was changed to make the suite pass.

## A8 condition and remaining Projects entry gaps

**Current A8 technical condition: met**, based on the passing current-input Section 9 replay. The [2026-09-22 A8 approval and result](../planning-artifacts/relock-section9-result-2026-09-22-post-a8-corrective.yaml) remain valid only for their recorded 196-artifact state. The current single-owner policy permits the already accepted conditional plan to advance after its stated checks; it does not turn the historical approval into a current-candidate signature. The current manifest still says `v2_exposure_authorized: false`; production `Program.cs` remains unchanged, v2 is unrouted, and Story 1.17 is open.

Before Projects v2 testing and any production T0, the migration package still requires an exact released v2 Client/Contracts pair; a complete Projects Folders-call inventory and periodic schedule; a Projects commit and passing build/tests against that pair, including lifecycle, effective permissions, file metadata, denial, and authority-unavailable outcomes; and its deployment artifact. It also requires version- and consumer-attributed request telemetry, baseline/thresholds, discovery of all deployed v1 consumers, an exact-artifact preproduction rollback rehearsal, and a UTC activation slot, 168-hour deadline, operations owner, and retirement slot. Mutating v2 callers need a separate rollback/effect disposition. None of those external or production checks is claimed here. Keep v1 available and the migration hold in effect until the entry checks are evidenced; no seven-day clock has started.

Following the owner's instruction to keep a used consumer in `references/`, the Folders root now declares [Hexalith.Projects](../../references/Hexalith.Projects/Directory.Packages.props) as a submodule pinned to deployed revision `c767d38d8ae76f9ad949965ac4870a842c699f9c`. This gives the local evidence check a reproducible source revision; it is not a Projects v2 test result or a change to the PD10 candidate inventory.

## Addendum 2026-09-24: route-preparation reseal

Evaluated 2026-09-24, with final digest observation at 2026-09-24T21:20:40Z. Source revision: `2de4279ae2a440d872d1780d251c9e2e937e4d7a` plus the uncommitted plan B route preparation on `feat/story-1-17-v2-closure-prep`. That work adds the validated `Folders:ApiRouting:Mode` setting (`V1Only` default, `Coexistence`, `V2Only`) and the host composition that `Program.cs` now calls. It also adds guard tests and review regressions, and 16 story-owned paths, and refreshes the [consumer discovery](../../docs/contract/pd10-v2-consumer-discovery.md). **Result: the A6b technical checks pass on the new bytes. Section 9 does not fully pass: S9-02 finds one stale binding, so S9-11 and the current A8 technical condition are not met until that binding is refreshed.** The earlier sections above still describe the 206-artifact state and are not rewritten. No approval record, `sprint-status.yaml`, or planning manifest was edited. No routing mode was deployed or switched.

| Input | Current exact evidence |
| --- | --- |
| Authorization matrix 2.0.0 | Unchanged: SHA-256 `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420` |
| Generated manifest | Raw SHA-256 `06b13d721cca7407827a69e3cfe20d4654a8098ab7c3759a8e02e1f426edf852` (41,536 bytes); supersedes `e89ee58008e3ff4b34d08eac6e8d6a3d357d7df3ce7b1f13eadd53781e8e500c` |
| Candidate set | Declared SHA-256 `19cc81d1b469d46c5d99f0d3b3703297e4c98f321e98e87d848fdc85ba86a605`; 222 listed artifacts (206 plus 16 route-preparation and review-regression paths); every listed raw-file hash matches |
| Historical v1 | Unchanged: OpenAPI SHA-256 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2` |

**A6b on the new bytes: pass.** The checked-in manifest was regenerated. Two further isolated generations to scratch files were byte-identical to it and to each other. The manifest's matrix binding matches the current matrix, and it still declares `production_routed: false`. Focused Release contract classes passed: `Pd10ConformanceSetTests` 3/3, `Pd10V2CandidateContractTests` 5/5, `AuthorizationMatrixContractTests` 7/7, and `GovernanceCompletenessGateTests` 22/22. The full Release Contracts suite passed 342/342.

| Check | Result on the new bytes |
| --- | --- |
| S9-01 decisions | Pass, carried: the approval register still hashes to `2551bdecb3b07a3920ff5f76c27fa52e5c95834fe0da6207a4227cd6a4b031ff`, matching the manifest binding. No decision record changed. |
| S9-02 provenance | **Fail, 19/20.** The planning manifest (`7ad9fa657b649ed095b9ca490fe01568b8efd6cefe61b3eab074f5d7580addc0`, unchanged) still binds the generated conformance manifest to `e89ee580…`, but its bytes are now `06b13d72…`. The other 19 entries and the register binding match. `sprint-status.yaml` remains `230889efd5d5e5cfa18b5bbd307e093a581f2434656eae85d6f12ff9573b7c26`. |
| S9-03 requirements | Pass, carried: the PRD, epics, and NFR-traceability inputs match their provenance digests, so the FR1–FR58 and NFR1–NFR84 inventories are unchanged. |
| S9-04 NFR traceability | Pass: `NfrTraceabilityConformanceTests` 17/17. |
| S9-05 lifecycle | Pass, carried: `epics.md` and `sprint-status.yaml` bytes are unchanged; Story 1.17 is backlog. |
| S9-06 frozen lifecycle | Pass: both Story 3.14 specifications still hash to `d21aa8200d1b322dc2487eac26c274c63d2062739f015f3129501e8a9170d652` and `5eec57125efe867f8ae2ffbf916b233d20248e7888b9a994f8f9a07447a765ee`; Story 3.14 remains backlog. |
| S9-07 A6b inventory | Pass: the 222-artifact manifest reproduces byte for byte and binds the current matrix. |
| S9-08 dependency graph | Pass, carried: the planning manifest that holds the ranked graph is byte-identical. |
| S9-09 verification | Pass: `dotnet restore Hexalith.Folders.slnx -m:1` and `dotnet build Hexalith.Folders.slnx -m:1` passed, as did `dotnet build Hexalith.Folders.CI.slnx -c Release -m:1`, all with zero warnings and errors. Before the review regressions, full Contracts passed 342/342, Server 757/757, and Integration 725/725. After them, focused runs passed: `FoldersApiRoutingModeTests` 13/13, the golden secret-log test 1/1, the three edited Client tests 4/4, and `PageReadFreshnessContractTests` 17/17. The coordinator reruns the full suites. `pwsh tests/tools/run-contract-parity-ci-gates.ps1` passed all 12 categories, with report SHA-256 `53439288efd4dd7e2163926e6a9d60ebb1e929e1225e209525c24640105b17d3`. `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` passed, with report SHA-256 `4a5630580f3aec35925bef605bfa2616dc16452024abc592ca2be21d074997a7`. |
| S9-10 authority | Pass, carried: the manifest, register, and Epic 1 context are unchanged. The active Story 1.17 spec makes no current-authority claim for the August 4 snapshot. |
| S9-11 hold condition | **Not met**, because S9-02 fails. |

**A8 technical condition on the new bytes: not met** until S9-02 passes. The only S9-02 gap is the planning manifest's `provenance` entry for `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml`. That entry needs `06b13d721cca7407827a69e3cfe20d4654a8098ab7c3759a8e02e1f426edf852` instead of `e89ee580…`. This task's scope does not include editing the execution-authority manifest, so that edit is left for an explicit owner-directed refresh. After the edit, the planning manifest's own digest changes, so rerun S9-02 and S9-11. The Projects entry gaps listed above remain open. `V1Only` is the default, so this route preparation keeps plan C, the hold, as the executable state and does not start the seven-day clock.

## Addendum 2026-09-25: provenance refresh and Section 9 replay

Evaluated 2026-09-25, with final digest observation at 2026-09-25T06:26:18Z. Source revision: `6f8abef01a9b755c6bf33ed00865ca4a39d66dcf` plus two owner-directed changes on `feat/story-1-17-v2-closure-prep`. **Result: every current-input Section 9 check, S9-01 through S9-11, passes on the resealed 222-artifact candidate, so the current A8 technical condition is met.** This is a technical result under the [single-owner decision policy](../../docs/governance/approval-policy.md). It is not a historical role approval, an A6b approval of the 222-artifact candidate, or an exposure record. No approval record and no `sprint-status.yaml` byte were edited. No routing mode was deployed or switched.

The two owner-directed changes:

1. Jerome, as owner, authorized refreshing one entry in the planning manifest: the top-level `provenance` entry for `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml`, from `e89ee58008e3ff4b34d08eac6e8d6a3d357d7df3ce7b1f13eadd53781e8e500c` to `06b13d721cca7407827a69e3cfe20d4654a8098ab7c3759a8e02e1f426edf852`. That one line is the only manifest change. The historical conformance bindings under `post_a8_finalization`, `corrective_a8_finalization`, and the `DEC-A6B-OQ3` node (`245c38f5…`, `4c8f33f0…`) were not rewritten. The planning manifest moves from `7ad9fa657b649ed095b9ca490fe01568b8efd6cefe61b3eab074f5d7580addc0` to `cbb6e8c771336f2af1c7546cb4e6d4a707713beecab43ab622cf4c4c66a046c0`. No other artifact binds the planning manifest's own digest as current input.
2. `references/Hexalith.Projects` was repinned from `3f12e4c329a8b46a8e69397635ba290388923c75` to the deployed revision `c767d38d8ae76f9ad949965ac4870a842c699f9c`, an ancestor of `3f12e4c`. That makes the pin match the revision the readiness and migration documents already cite. No nested submodule was initialized. Projects is not part of either Folders solution or the PD10 candidate inventory, so the repin does not change any Section 9 input.

| Input | Current exact evidence |
| --- | --- |
| Planning manifest | SHA-256 `cbb6e8c771336f2af1c7546cb4e6d4a707713beecab43ab622cf4c4c66a046c0` |
| Approval register | Unchanged: SHA-256 `2551bdecb3b07a3920ff5f76c27fa52e5c95834fe0da6207a4227cd6a4b031ff`, matching the manifest binding |
| Authorization matrix 2.0.0 | Unchanged: SHA-256 `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420` |
| Generated manifest | Unchanged: raw SHA-256 `06b13d721cca7407827a69e3cfe20d4654a8098ab7c3759a8e02e1f426edf852` (41,536 bytes) |
| Candidate set | Unchanged: declared SHA-256 `19cc81d1b469d46c5d99f0d3b3703297e4c98f321e98e87d848fdc85ba86a605`; 222 artifacts, all listed raw-file hashes match |
| Historical v1 | Unchanged: OpenAPI SHA-256 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2` |
| Tracker | Unchanged: `sprint-status.yaml` SHA-256 `230889efd5d5e5cfa18b5bbd307e093a581f2434656eae85d6f12ff9573b7c26` |

| Check | Result on the refreshed inputs |
| --- | --- |
| S9-01 decisions | Pass: the register holds exactly eleven A1–A8 gate records (A1, A2, A2b, A3, A4, A5, A6, A6b, A7, A7b, A8). Each decision-payload hash matches its file, required and recorded authorities match, every record is `approved`, and every approval binds its payload or preparation digest. No decision record changed. |
| S9-02 provenance | **Pass, 20/20.** Every top-level manifest provenance digest matches current raw bytes, including the refreshed conformance entry `06b13d72…`. The register binding matches. `sprint-status.yaml` remains `230889efd5d5e5cfa18b5bbd307e093a581f2434656eae85d6f12ff9573b7c26`. |
| S9-03 requirements | Pass: the manifest inventory is exactly FR1–FR58 and NFR1–NFR84. `epics.md` carries all 58 FR and 84 NFR identities, and `prd.md` carries all 58 FR identities. |
| S9-04 NFR traceability | Pass: `NfrTraceabilityConformanceTests` 17/17 (Release). |
| S9-05 lifecycle | Pass: all 158 canonical stories resolve to exactly one tracker key with an identical lifecycle value (111 done, 44 backlog, 2 in-progress, 1 review). Story 1.17 is backlog. |
| S9-06 frozen lifecycle | Pass: the two Story 3.14 specifications still hash to `d21aa8200d1b322dc2487eac26c274c63d2062739f015f3129501e8a9170d652` and `5eec57125efe867f8ae2ffbf916b233d20248e7888b9a994f8f9a07447a765ee`. Story 3.14 remains backlog. |
| S9-07 A6b inventory | Pass: two isolated `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root . --output <scratch>/s9-07-{a,b}.yaml` runs produced files byte-identical to each other and to the checked-in manifest (`06b13d72…`). The manifest binds the current matrix and v1 digests, lists 222 artifacts matching `artifact_count`, and declares `production_routed: false`. The historical 196-artifact A6b approval remains historical. |
| S9-08 dependency graph | Pass: 72 ranked nodes, 248 unique edges, and 23 `accepted:`-prefixed edges resolving to accepted terminal references. There are zero unresolved edges, unranked or duplicate-ranked nodes, and rank errors. Every prerequisite has a strictly lower rank, which precludes cycles. |
| S9-09 verification | Pass. Release package profile: `dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -m:1` and `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true --no-restore -warnaserror -m:1` completed with 0 warnings and 0 errors. The Contracts, Client, CLI, MCP, and Integration test projects were then force-rebuilt with `--no-incremental -warnaserror -m:1`. The full Release Contracts executable passed 342/342, and the focused classes passed as well: `NfrTraceabilityConformanceTests` 17/17, `GovernanceCompletenessGateTests` 22/22, `Pd10ConformanceSetTests` 3/3, `Pd10V2CandidateContractTests` 5/5, and `AuthorizationMatrixContractTests` 7/7. `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` passed with report SHA-256 `4a5630580f3aec35925bef605bfa2616dc16452024abc592ca2be21d074997a7`. `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` passed all 12 categories with report SHA-256 `53439288efd4dd7e2163926e6a9d60ebb1e929e1225e209525c24640105b17d3`. Both tracked reports were rewritten byte-identical. Debug source profile: `dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseNuGetDeps=false -m:1` and `dotnet build Hexalith.Folders.slnx --configuration Debug -p:UseNuGetDeps=false --no-restore -m:1` completed with 0 warnings and 0 errors. The full Debug suites passed Server 757/757 and Integration 727/727, which closes the full-suite rerun the previous addendum left to the coordinator. |
| S9-10 authority | Pass: the only August 4 references in the manifest are its own `supersedes_snapshot` block. The register, `epic-1-context.md`, and the active Story 1.17 spec make no current-authority claim for that snapshot. |
| S9-11 hold condition | **Pass:** S9-01 through S9-10 pass on current technical inputs. This replay does not activate v2, start the seven-day clock, or close Story 1.17. |

One first-attempt failure is recorded. A Debug build that restored with `-p:UseNuGetDeps=false` but built without it failed with 156 `CS0234`/`CS0246` errors in `references/Hexalith.Tenants`. The Tenants props default Debug builds to package mode, so its restore assets lacked the EventStore package. This was an invocation mismatch, not a source defect. The profile-matched build above passed. No source, package-version, or approval content was changed to make any check pass.

**Current A8 technical condition on the resealed 222-artifact candidate: met.** The [2026-09-22 A8 approval and result](../planning-artifacts/relock-section9-result-2026-09-22-post-a8-corrective.yaml) remain valid only for their recorded 196-artifact state. The manifest still says `v2_exposure_authorized: false`, `V1Only` remains the default routing mode, and Story 1.17 is open. The Projects entry gaps listed above remain open, and plan C (the hold) remains the executable state until they are evidenced. This refresh resolves the two deferred findings on the stale S9-02 binding and the `3f12e4c` Projects pin. The other deferred governance and submodule-pin findings from owner commit `2de4279` are untouched.

## Addendum 2026-09-26: local migration preparation

Source baseline is `37d348945b35620da6ba4e524a09bee202712460` plus uncommitted preparation. The bounded route meter, composed-host tests, consumer discovery, and rollback procedure changed. The route instruments use the existing `Hexalith.Folders.Observability` meter registered by Folders ServiceDefaults. The candidate allowlist now includes `FoldersApiRouteTelemetry.cs`, yielding 223 artifacts. Two isolated invocations of `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root . --output /tmp/story117-conformance-{a,b}.yaml` produced byte-identical files. The checked-in manifest SHA-256 is `229beef4aeca511461859c2e0ca36e5511bcd1546f4d2cbf5ad99a27a5d50b7d`; its declared candidate-set SHA-256 is `f44f59e7c0596fdf0f1785c9ac87fd3ddaa0c06faf323d58fd617ca8131a1cca`. Only the current top-level provenance binding for that manifest was refreshed; the planning manifest now hashes to `0e9a935f88641d7e97a397620674224f8f31ade195f0378b6b0137cce58c0a91`. Historical approval bindings were preserved.

`dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release -p:UseNuGetDeps=true -p:NuGetAudit=false -m:1 -v:quiet` and the matching `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true -p:NuGetAudit=false -warnaserror -m:1 --no-restore -v:quiet` passed with zero warnings and errors before the final meter correction. After the correction, the Debug Integration project, restored and built with `-p:UseNuGetDeps=false`, passed with zero warnings and errors; its `FoldersApiRoutingModeTests` executable passed 16/16, including hold, coexistence, retirement, and bounded metric tags. `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` passed all categories before the correction (report SHA-256 `53439288efd4dd7e2163926e6a9d60ebb1e929e1225e209525c24640105b17d3`). `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` passed before the correction (report SHA-256 `4a5630580f3aec35925bef605bfa2616dc16452024abc592ca2be21d074997a7`). Release build and broad gates still need a final-byte rerun. These are local technical checks, not release or production evidence.

The Projects checkout remains clean at `3a121eb678b000bf88816beff75b0230194c3f32`, using v1 generated packages. A proposed local v2 adapter edit was removed because no exact released v2 pair exists to build and test against. Its Debug source-profile restore passed, but `dotnet build tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true --no-restore -m:1 -v:quiet` failed: MSB9008 names the absent `/src/Hexalith.Conversations.Contracts/Hexalith.Conversations.Contracts.csproj`, followed by CS0234 in `ProjectConversationItem.cs`. Nested Projects submodules were not initialized. No exact released Folders v2 Client/Contracts pair or Projects package-profile build artifact exists in this evidence. Therefore Projects migration, delivery entry checks, T0, observation, v1 retirement, and Story 1.17 closure remain pending; keep `V1Only`.

**Final-byte verification, 2026-09-26:** A profile-matched Release restore followed by `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true -p:NuGetAudit=false --no-restore -warnaserror -m:1 -v:quiet` passed with zero warnings and errors. `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` passed all twelve categories, and `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` passed. An initial Release build using stale restore assets failed with four missing EventStore client/read-model type errors; the matching restore resolved that invocation error without source or dependency changes. These checks replace the pending final-byte rerun noted above. They do not provide Projects, production, or closure evidence.

## Addendum 2026-09-26: local Projects package-profile probe and production-host seam

Evaluated at 2026-09-26T07:26:02Z on Folders revision `d4cc07f07b007dcb4ac89f443e779dcdaff8a8f8`. The checked-in 223-artifact manifest reproduced byte for byte in two isolated generations: raw SHA-256 `229beef4aeca511461859c2e0ca36e5511bcd1546f4d2cbf5ad99a27a5d50b7d`, 41,712 bytes, with declared candidate-set SHA-256 `f44f59e7c0596fdf0f1785c9ac87fd3ddaa0c06faf323d58fd617ca8131a1cca`. The profile-matched Release CI solution build passed with zero warnings and errors. All twelve contract-parity categories and the governance/completeness gate passed; their tracked reports remained byte-identical at SHA-256 `53439288efd4dd7e2163926e6a9d60ebb1e929e1225e209525c24640105b17d3` and `4a5630580f3aec35925bef605bfa2616dc16452024abc592ca2be21d074997a7`.

A **local, unpublished** Client/Contracts pair was packed from that exact Folders revision into an isolated scratch feed with version `0.0.0-local.20260926.d4cc07f`. NuGet metadata names both package IDs and embeds the full revision; Client depends on Contracts at the matching version. Client `.nupkg` SHA-256 is `683ba635dbcb6aa8a506fb672b90e7d43b686a36c7c6b47e1589129c7c8721ab`; Contracts `.nupkg` SHA-256 is `28dee0a79f2932d5d40526208a4b0ae383daa72d7801063e9ad9c43dc73bf964`. This directly packed pair is diagnostic evidence, **not** the five-package release-lane result or a published v2 pair. Existing repository-owned `nupkgs/` artifacts were preserved; those older local packages carry version `0.0.0-local.20260920` and source revision `5174b82c9b69aa1a81d89837c4b14a25ebbd7915`.

The clean Projects base is `3a121eb678b000bf88816beff75b0230194c3f32`. A temporary central-package override and local-only NuGet source selected the diagnostic pair for the Projects Server test project in Release/package mode, leaving its tracked Client/Contracts `1.0.0` pins unchanged. Cached Conversations Client/Contracts `1.0.0` packages satisfied the dependency that blocks its Debug source profile when nested Conversations source is absent. The first package-profile build passed, but the original metadata tests failed 2/15: their v1 fixture omitted v2-required `limits`, so the generated client rejected HTTP 200 and Projects treated the wrapped status as `Denied`. A scratch deserialization probe with `limits` present parsed the expected path and stale flag.

Projects now has four **uncommitted, migration-preparation** file edits against that base: its metadata request uses schema `v2`, metadata fixtures use the v2 `limits` shape, and both adapters classify an unexpected successful API exception as `Unavailable` while retaining canonical `401`/`404` denial and `503` unavailability. The local diff SHA-256 is `9641e47ac7216cf211f0e550e6817edab6fc08e1fb941c53298d3a9ac3a41cd8`. With the diagnostic pair, the Release/package-mode test-project build passed with zero warnings and errors; `ProjectFolderDirectoryTests` passed 15/15 and `ProjectFileReferenceDirectoryTests` passed 19/19. These local tests do not satisfy the entry check for an **exact released** Client/Contracts pair, a Projects commit/deployment artifact, or production route use. The Projects v1 package pins and the Folders `V1Only` route remain unchanged.

A separate check using the tracked Projects `1.0.0` pins confirmed that its current base revision already fails a Release/package-mode build at `FoldersProjectFolderDirectory.cs:66` with `CS1501`: the pre-existing five-argument `GetEffectivePermissionsAsync` call has no overload in the pinned v1 client. The temporary v2 pair resolves that source/package mismatch for local testing; it does not make the tracked pin or deployment artifact ready.

Production host registration remains a separate prerequisite. `Program.cs` calls `AddFoldersServerHost`, which registers only `InMemoryFolderRepository` in Development or Staging; in Production no `IFolderRepository` exists and `FolderRepositoryStartupAssertion` stops startup. The `IFolderRepository` contract requires stream load, atomic append with a folder idempotency-fingerprint ledger, and matching-key lookup. [ADR 0001](../../docs/adrs/0001-folder-domain-processor-persistence.md) records that the current EventStore append path lacks those semantics. [Story 12.1](../planning-artifacts/epics.md) owns the durable repository, versioned replay, `DomainResult.NoOp()` retirement, and Production registration after the pending `EXT-ES-EVENT-EVOLUTION` capability. Substituting an in-memory or read-model store would not meet that contract. No production registration was added; the startup guard continues to fail closed. Production baseline, rehearsal, UTC owners/slot, deployment, observation, retirement, and closure evidence are still absent.

**Projects package-profile replay commands:** With `probe=/tmp/story117-local-packages-7qBzWLeu` and working directory `references/Hexalith.Projects`, the following commands exited 0. The scratch feed and override are local diagnostic inputs; neither is a tracked Projects pin or a published package source.

```bash
dotnet restore tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:DirectoryPackagesPropsPath="$probe/Directory.Packages.props" -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --configfile "$probe/nuget.config" -v:quiet
dotnet build tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj --configuration Release --no-restore -p:UseHexalithProjectReferences=false -p:DirectoryPackagesPropsPath="$probe/Directory.Packages.props" -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -warnaserror -m:1 -v:quiet
tests/Hexalith.Projects.Server.Tests/bin/Release/net10.0/Hexalith.Projects.Server.Tests -class Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests -class Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests
```

The build reported zero warnings and errors; the xUnit v3 executable reported total 34, failed 0, skipped 0. `tests/Hexalith.Projects.Server.Tests/obj/project.assets.json` resolves both Folders IDs to `0.0.0-local.20260926.d4cc07f`. `git diff --check` exited 0 in both repositories. The exact Projects diff SHA-256 remains `9641e47ac7216cf211f0e550e6817edab6fc08e1fb941c53298d3a9ac3a41cd8`.

**Tracked-pin blocker replay:** In the same Projects working directory, omitting the temporary central-package override selects tracked Folders `1.0.0` pins. `dotnet restore tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 --configfile /tmp/story117-local-packages-7qBzWLeu/nuget.config -v:quiet` exited 0. `dotnet build tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj --configuration Release --no-restore -p:UseHexalithProjectReferences=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -warnaserror -m:1 -v:quiet` exited 1 with one error, `CS1501` at `FoldersProjectFolderDirectory.cs:66`: no five-argument `GetEffectivePermissionsAsync` overload. Restoring with the diagnostic override again exited 0. This mismatch predates the four Projects edits and cannot be made release-ready by retaining a scratch-feed override.

## Addendum 2026-10-08: released pair and Projects consumer verification

Evaluated at 2026-10-08T12:36:52Z. Folders source is `c8e3d99468e27329d9e3977ec2d0b1542c7e9e60` plus this migration evidence update. Projects is now committed at `caf3721427f0b4834415369c2b4a5f981725474f` with a clean working tree. Its four migration files were committed externally during this implementation; the agent performed no staging, commit, push, publication or deployment. A fresh non-incremental Release/package rebuild and focused rerun bind the current Projects commit. **Result: Projects consumes a released v2 read client successfully; current-input Section 9 and migration entry remain blocked.** `V1Only` remains required, T0 is unset, and no tracker transition occurred.

### Released pair and build identity

[Release operations evidence](../../docs/operations/release-packages.md) records release run `37001706122`, publication of the five-package `1.1.1` set on 2026-10-02, and exact Folders source `d5f49e96dfab10bf2839ec263a343a6a4c13b06f`. Ordinary NuGet restore obtained these archives from `https://api.nuget.org/v3/index.json`; both nuspecs embed that full repository commit, and Client depends on Contracts `1.1.1`. No scratch package source or temporary version override is used by the tracked Projects pin.

| Artifact | Exact identity |
| --- | --- |
| `Hexalith.Folders.Client` `1.1.1` archive | SHA-256 `e778aad218dcaf670fe8fb40ff40a83d76f224b62d77d6a4d0612aed9eab5db4` |
| `Hexalith.Folders.Contracts` `1.1.1` archive | SHA-256 `cb85fb7d0e6013ccbe3058061c9e6a57fbdd1a95367f97692dc187a73fe0cf0c` |
| Released conformance manifest | Raw SHA-256 `264380b817fc19092f075052de66f97a71c72af849b53a8b6473840d8269e2e9`; declared candidate set `d36d35edd348e0893e313d408e2cc8f592429c0701643fb825972c0c10325031`; 223/223 artifact hashes match that release source. |
| Final current conformance manifest | Raw SHA-256 `cae97c1f685cfa89cc3125243648ce462456670a3750aae66ea7b016037721d7`; 41899 bytes; declared candidate set `52fe4ab7ff3a980e26076179a3327d3a006aa3594ad0131ca0fc973c253777b6`; 224 artifacts. Two isolated final generations match each other and the checked-in bytes. |
| Current planning manifest | SHA-256 `2da4289361708f5da319b1545cd747f9f79b5432a125504ccb670c3be0cac3e9`. Only the current top-level conformance provenance binding was refreshed; historical approval bindings were preserved. |
| Projects committed delta from `f8649509d798435af4021cec4440c425b80f6fd6` | Unified binary-capable diff SHA-256 `57b568ec71139bbdf9cb0af570fd0c4df6b0bac2ddc789d3d4d688dcb1582edb`. |
| Local Projects Server build output | `tests/Hexalith.Projects.Server.Tests/bin/Release/net10.0/Hexalith.Projects.Server.dll`, SHA-256 `10b9f57aea20bdc126a46eb45042e2d1c76cfcc33a6b81cf0a2b5187face69d8`; matching `.deps.json` SHA-256 `f1cb8d209f47932aa9afd38c99f3051972da8583835f5ec504c8ff833563555b`. This is a local build identity, not an immutable production deployment artifact. |
| Preserved authority evidence | Authorization matrix SHA-256 `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420`; historical v1 SHA-256 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2`; approval register SHA-256 `2551bdecb3b07a3920ff5f76c27fa52e5c95834fe0da6207a4227cd6a4b031ff`. |
| Tracker | SHA-256 `cc964658f61d7cc4bf7ca61478f2fb75720b60c6028708dc65d4cc1a0930e540`, unchanged by this work; Story 1.17 remains `backlog`. |

The released generated transport client is byte-identical to current `HexalithFoldersClient.g.cs`. However, the full candidates differ in 19 shared paths and one newly inventoried converter. The v2 spine adds current idempotency behavior/read-key declarations; its hash and the generated helper verification constants differ from the release. Passing Projects read tests does not establish an exact current-candidate package or transfer historical approval.

| Candidate path | Released source SHA-256 | Final current SHA-256 |
| --- | --- | --- |
| `docs/contract/idempotency-and-parity-rules.md` | `757fd323afd1b7c4fc9296486cb18d39bb4ae9cafe93740e0e926c22ef49b110` | `c2c8c95a7a32f95b81627ba70cbd6564a10f3fc371abfbbe18d5a8e6468987d2` |
| `docs/contract/pd10-v2-consumer-discovery.md` | `692b7629f04fe6955b6fa42e85dacf3406d11490c62e09a7d2d2b1ed4738a111` | `99314150294fee42c0a4b91b09c9f12be1b39aa0771f0a51d442ca5be3ed96f3` |
| `scripts/pd10-v2-story-owned-paths.txt` | `ef02e7ecea852194523d3d10f49cd1c079cce94170c0d46a39b18bcfe258ff37` | `a1ab5a8382284371e934c80510170fd71a273301884fb4612a8c2e0db39dd3d0` |
| `src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs` | `877c1af948440950c51ce6eb36e9bb3a99e2ba6ada36d5df9c19cbdd2c2171fa` | `68c7cfadcb46b2f684e9e60adcef0ff1d59f441d512f572adef456301b634e08` |
| `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml` | `586eb24b09d5dfec1d9946ef88e00cdb31684333c81b9d76f3862713ce4ab0c0` | `314681a3b1822b1d195c25d83e607014c4f4dc37d64ef8537a6f5144c2edae98` |
| `src/Hexalith.Folders.Server/Authorization/Pd10ProtectedOperationCatalog.cs` | `2f11e901e19f8f25d291c87b135dcb0099ef0699fce9527b7f86012d807d7c9b` | `57e66f40fb25b438f716253da0875c32cbee8009c1a3710fee1445411ed2f638` |
| `src/Hexalith.Folders.Server/Authorization/Pd10ProtectedOperationExecutor.cs` | `1e65762f0500793d867e44f777a911062f3491e5b744d5e8d4eea81de8970e68` | `fd348859d87783450a9daa1e79536a4e7014a9a011bc9fc2dd75af47934333ff` |
| `src/Hexalith.Folders.Server/FolderDomainProcessor.cs` | `e6f3d8b7cb746858a49b34f6b1a322055debd66ac12b93c7bf4086b3d303b017` | `0ae58ada591ec6e106fcaa36cbecf5e8fdfd339511a3173898d530cf15c06c27` |
| `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` | `3c5375156ccfbc68edf7801a3fa3dc47bd8fa01fda3b76e5fcd4e94d31b9ffd3` | `0c9e10c02a82366e42ac5e28ef4b487647958483338706d5ed440a72c59ce665` |
| `src/Hexalith.Folders.Server/FoldersRequestJsonElementConverter.cs` | `absent` | `d9aea2b77cbb1f885b54d05aac6f3e783c06ed2e818488c5ee054e633de47774` |
| `src/Hexalith.Folders.Server/FoldersServerHostComposition.cs` | `b6457b0b50969f9089af4c313e64c1ed39016929941bc82132c970a6047b433b` | `0084b46d580a519b99c7252c3389a7896dc236ffd7dc186210d046bcc9bf3768` |
| `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs` | `da81f25eb441206b9293230868be489da342a9d65a66886d98807264d58a071c` | `0c9c16ad3626fb99bbc53a5906322bb90f8c23d03a54395ff1553363e2c4cf36` |
| `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs` | `c06bf9aeb68ee71d11634cf0529913b5eb098824be51f088a41d1b191b8551a9` | `2e19be46d46c84ea1ad0c757f902f58f768f4092262f6f55182fc82f78dcaeb2` |
| `tests/Hexalith.Folders.IntegrationTests/Routing/FoldersApiRoutingModeTests.cs` | `885c1e5b6df16652df516b5918cfb841882003eb3d75a218c43ad16a16cba504` | `ca05d6cef42b0d91a544abca6cba904ec0ded502ce1a07935c61f043b19667fc` |
| `tests/Hexalith.Folders.Server.Tests/Pd10ProtectedOperationCatalogTests.cs` | `fb29e5d1ff38fdbd69622c2c0343a02a5772ba71dbe1e846658f989aef74be2d` | `37b02cfd9cebbc0a80e3fbffbb9f585beecad5892626b921eea192efe21c594f` |
| `tests/fixtures/parity-contract.schema.json` | `dc20eb8d1083ffb6cf682ac607a1ff4446fce92eddc92a12e8b26261dc1b8916` | `6bbcbc6473d1387d5e814a3e2f5ca0f96ee9a8da5ba7e0c3e21a11760c88a36b` |
| `tests/fixtures/parity-contract.yaml` | `7eb4a5a5f289dc017233c5266dc6934576ca7366c65f0eebb145d75e6488dd67` | `da43ffd266a851616824d0c84ab0394707c7c6225b1c3aa210d677974c54eae4` |
| `tests/fixtures/previous-spine.yaml` | `f482503e7ec33d3a791d3b2f52da1e4a2f8e49a0a420b23d54088fe12ab26ab3` | `d998ca75faaa8b093a305d527ccce97d5f95817eebae5e3137a42cbfa8d7b10b` |
| `tests/tools/parity-oracle-generator/Program.cs` | `f08f20410102155fb519e969f3b27981a763b5cbb8fcc0e2e176296fb5615973` | `a4e44cdbbbeb1d4eb60f5ec267db350dbae90e4fe6e973e6bcdd0407f5e0c851` |
| `tests/tools/run-contract-parity-ci-gates.ps1` | `82aef1ede821fd6d903f6f629c7073a51d0f48c4ed43344b0fb27466f1587175` | `8c913159f905909ec2a187d7c8eed68f2173058fc94a8d4bc723ee03eae84149` |

### Verification and current-input Section 9

Commands ran individually in their owning repositories, with NuGet audit enabled. The initial `-p:HexalithFoldersVersion=1.1.1` discovery probe passed 34/34 before the tracked pin and added cases. Final commands below use the committed pin without that override.

| Command or check | Result |
| --- | --- |
| Projects: `dotnet restore tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj -p:Configuration=Release -p:UseHexalithProjectReferences=false -m:1 -v:quiet` | Exit 0; assets resolve Client and Contracts to `1.1.1`. |
| Projects: `dotnet build tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj --configuration Release --no-restore --no-incremental -p:UseHexalithProjectReferences=false -warnaserror -m:1 -v:quiet` | Exit 0; zero warnings/errors on commit `caf3721427f0b4834415369c2b4a5f981725474f`. |
| Projects test executable: `-class Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests -class Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests` | Exit 0; 39/39, zero skips. V2 lifecycle/permissions/metadata routes, required task header, canonical typed `401`/`404`/`503`, malformed lifecycle/permissions responses, and missing v2 metadata limits are exercised. |
| Projects full Server test executable, without a filter | Exit 0; 832/832, zero skips, on the identical four-file working-tree bytes before their external commit. |
| Folders: `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1 -v:quiet` | Exit 0; zero warnings/errors. |
| Folders Integration executable: `-class Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests` | Exit 0; 18/18, zero skips. Hold, coexistence, retirement, authenticated attribution, and invalid modes are covered. |
| `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` | **Exit 1; 11/12 categories pass.** `rest-sdk-golden-parity` fails four repository-creation cases described below. Report SHA-256 `f8965cd8608311490d30a4ee2e9db0e6125465686a2ea1d9d8d231566faae9ef`. |
| `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` | Exit 0 after final reseal: governance 22/22, authorization matrix 7/7, conformance 3/3. Report SHA-256 `4a5630580f3aec35925bef605bfa2616dc16452024abc592ca2be21d074997a7`. This automated suite does not prove every Section 9 current provenance binding. |
| Contracts executable: A6b conformance, v2 candidate, matrix, Release package, and NFR classes | **Exit 1; 40/41 pass.** The sole failure is `ReleasePackageConformanceTests.StableReleaseShouldUseStableDaprIntegration` at line 230, `Sequence contains no matching element`. The test expects a root `PackageVersion Update="CommunityToolkit.Aspire.Hosting.Dapr"` override which the current root file does not contain. A6b/matrix checks and all 17 NFR cases pass. |
| Two final `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root . --output <scratch>/a.yaml` and `b.yaml` generations | Exit 0; byte-identical to each other and the checked-in manifest. |

Parity failure detail: `GoldenLifecycleParityTests.CandidateAuthorizesRepositoryBackedCreationOfAMissingFolder` expects `202`, and the three `CandidateRepositoryCreateAndBindPreserveDeclaredProviderOutcome` cases with `bindRepository=false` expect `422`, `409`, and `503`; all receive canonical `404` before downstream dispatch. The current protected catalog classifies repository-backed creation as folder administration with request-folder `manage_folder_access` authority, while those fixtures supply no existing folder or permission evidence. These failures predate the migration edits. No authorization condition, fixture expectation, or upstream dependency was weakened to make a gate pass.

The direct Section 9 replay verifies eleven historical decision-payload records and the register binding, FR1–FR58/NFR1–NFR84 identities, and 159 consistent story/tracker rows (111 done, 45 backlog, two in progress, one review). The ranked graph has 73 nodes, 262 unique edges and 24 accepted-prefixed edges, with no duplicate ranks/edges, unresolved prerequisites or forward-rank violations. **S9-02 fails: 16/20 current provenance bindings match.** The four pre-existing mismatches below are retained for the owning planning reconciliation, without changing signed authority or historical approvals. **S9-09 fails** on the parity and package checks above, so **S9-11 does not establish a passing current A8 technical condition**. The earlier passing September replays remain historical.

| Current provenance path | Bound SHA-256 | Observed SHA-256 |
| --- | --- | --- |
| `_bmad-output/planning-artifacts/prd.md` | `743e8f7a001f67a136d817154af0f25773fab21ef6e6bc5adf08c1e3694d731c` | `b08c5bb51a8d07b0a452837304486b69a1ce5c1f63ca4e7b2a360393b0c45608` |
| `_bmad-output/planning-artifacts/architecture.md` | `74ef242f6bfb77458818ab08a8acc25f547d6c891d29d36ab4954f7ca879973e` | `da13b2bd481b0c72355ad41ecf52b63f303711f4f5293ea89673cd05f916fce1` |
| `_bmad-output/planning-artifacts/epics.md` | `a863dc5a6f1b44986fa2dadb9c21f1657c02700b8506368b167d651274e9dbcc` | `d1ac3564c5f50b48f4d2978546703dab82c1b8288d70c203317bf45d23c70492` |
| `docs/exit-criteria/nfr-traceability.md` | `d490ef1178d74198982035b3b3adbaa4cc5a479f32b79711fc04b9789d7cd588` | `7e3065a226339a78875f0687f39711f31ac8d21a0160f6e8af5ffb061f97423c` |

### Entry decision and matrix audit

Keep the hold. The released pair differs from the current full candidate, no production Projects artifact/deployment or exact Folders production artifact is verified, and the ordinary Folders host still lacks the durable EventStore-backed `IFolderRepository` owned by Story 12.1. Production consumer discovery, periodic schedules, attributed baseline/thresholds, exact-artifact reversal, UTC slots/owners, T0, ordinary-traffic observation, and v1 retirement remain absent. No remote release, deployment, route change, retirement or closure was attempted.

The hold, coexistence, and retired-route matrix rows have executed local host coverage (18/18). The Projects migration row has released-client adapter coverage (39/39), not a production end-to-end execution. The expiry/incident row has a documented reversal procedure, not an exact-artifact operational rehearsal. Missing operational evidence therefore remains a gate; Story 1.17 stays open.

### Final dependency-bound verification on 2026-10-08

The initial Folders checks above did not capture submodule identities at each run. Builds and Tenants subsequently changed externally, so those initial results cannot establish the final dependency state. A fresh verification captured the following revisions at 2026-10-08T12:40:25Z and confirmed that every captured revision remained unchanged through the build, parity, routing, and focused contract checks. These results supersede the initial Folders checks for the final dependency state; no submodule was staged, committed, updated, or reverted by this verification.

| Repository | Exact revision |
| --- | --- |
| Folders | `c8e3d99468e27329d9e3977ec2d0b1542c7e9e60` plus the recorded migration evidence edits |
| Builds | `f717a87c26a8266bdde95d18f998ef2ab366d43a` |
| Tenants | `fcdcb4205a3f6e46f736cdd3e6f2b20ca2f241df` |
| EventStore | `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` |
| FrontComposer | `0e114214007c22f5cdbac21a6853cff4208340ee` |
| Memories | `3e18d0dcdceb387eff89862c382637da89ad7e47` |
| Commons | `116d26815eb81e35b3c161e1799e5ee12805fc0a` |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` |
| Projects | `caf3721427f0b4834415369c2b4a5f981725474f` |

The loaded Builds central package catalog has SHA-256 `1e2621765f2632077f906e945bf4d74916fef5dec04ec34d630dfce545dfbc78`. The repeated Release solution build exited 0 with zero warnings/errors. `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` exited 1 with the same four creation failures and 11/12 passing categories. The final Integration executable routing command exited 0, 18/18. The final Contracts executable command exited 1, 40/41, solely on `StableReleaseShouldUseStableDaprIntegration` at line 230. Exact focused commands were:

```bash
tests/Hexalith.Folders.IntegrationTests/bin/Release/net10.0/Hexalith.Folders.IntegrationTests -class Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests
tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests -class Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests -class Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests -class Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests
```

Independent acceptance inspection again confirmed all 224 candidate artifact hashes, the manifest digest, and unchanged historical v1, authorization matrix, approval register, and tracker bytes. Current provenance still matches 16/20 bindings; the same four mismatches remain. The concurrently created `spec-fix-ci-and-verify-nuget-publication.md` covers the golden creation and stable-Dapr failures and was preserved. Passing local consumer checks, truthful failed-gate evidence, and the refreshed manifest do not authorize production exposure or Story 1.17 closure.

## Addendum 2026-10-08: final-byte recheck and hold

Evaluated at 2026-10-08T14:58:35Z. Folders HEAD is `f0e7154461ff21226851046494b3f2b2fd8a772e`. That commit includes the CI repair that corrected the golden-creation fixtures and the stable-Dapr assertion. `264b555` then changed `tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs`. An uncommitted tracker edit had moved Story 1.17 from `backlog` to `in-progress`; it was restored before this evidence was recorded. **Result: local technical gates that previously failed now pass on the resealed candidate, and the migration entry decision remains hold.** `V1Only` remains required, T0 is unset, and no authorized tracker transition occurred.

### Candidate and dependency identity

Two isolated generations of `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root . --output <scratch>/{c,d}.yaml` were byte-identical. Acceptance verification wrote that output over the checked-in inventory, which until then hashed to `7c0e078ff30d4abace4b853c094a920263566c5bf5fa45d529e4392a0d4d17ad` with candidate set `0394783dbf2d5d4ce0c422b08a589c49927efe14d2efd5fafa4c21d45d1f40b2`. The checked-in file now matches both generations. A later regeneration and `Pd10ConformanceSetTests` (3/3), `FoldersApiRoutingModeTests` (18/18), and the Projects folder plus file-reference adapter tests (39/39) passed on that file. Only the current top-level conformance provenance binding was refreshed, from `cae97c1f…` to the new manifest digest. Historical approval bindings were not rewritten. The four pre-existing planning-document mismatches remain.

| Artifact | Exact identity |
| --- | --- |
| Final conformance manifest | Raw SHA-256 `835eee602d0f2f1daf8e22d8aef1138076d296c89c3159ba641977c9981de2de`; 41900 bytes; declared candidate set `8b0b70326ba83c95334a9046e0a55a5137e371f7e22ca7651f15bc642de4446f`; 224 artifacts. `production_routed` remains `false` and `story_closure_claimed` remains `false`. |
| Current planning manifest | SHA-256 `8edc35e93f66ae0e96df87ffbf83c9027785638bb60300eb9e8c80aa2d790b59`. Story 1.17 `story_lifecycle_status` remains `backlog`; `v2_exposure_authorized` remains `false`. |
| Preserved authority evidence | Authorization matrix SHA-256 `d5daa48323c3a117cb3e700b3ef3876ef29566bd6c4fa81a3edeec4d94c4d420`; historical v1 SHA-256 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2`; approval register SHA-256 `2551bdecb3b07a3920ff5f76c27fa52e5c95834fe0da6207a4227cd6a4b031ff`. |
| Tracker | SHA-256 `cc964658f61d7cc4bf7ca61478f2fb75720b60c6028708dc65d4cc1a0930e540`; Story 1.17 remains `backlog`. |
| Cached `Hexalith.Folders.Client` `1.1.1` | SHA-256 `e778aad218dcaf670fe8fb40ff40a83d76f224b62d77d6a4d0612aed9eab5db4`, matching the earlier release-lane record for source `d5f49e96dfab10bf2839ec263a343a6a4c13b06f`. |
| Cached `Hexalith.Folders.Contracts` `1.1.1` | SHA-256 `cb85fb7d0e6013ccbe3058061c9e6a57fbdd1a95367f97692dc187a73fe0cf0c`, matching that same record. |
| Local Projects Server build output | `Hexalith.Projects.Server.dll` SHA-256 `10b9f57aea20bdc126a46eb45042e2d1c76cfcc33a6b81cf0a2b5187face69d8`; `.deps.json` SHA-256 `f1cb8d209f47932aa9afd38c99f3051972da8583835f5ec504c8ff833563555b`. This remains a local build identity, not a production deployment artifact. |

The released `1.1.1` pair is still not an exact package of this candidate. No new publication was attempted.

| Repository | Exact revision through the checks below |
| --- | --- |
| Folders | `f0e7154461ff21226851046494b3f2b2fd8a772e` plus the recorded evidence and inventory edits |
| Builds | `50b0257001fe91f14bf16c7ea877d3e92bdfdf08` |
| Tenants | `5bfe0715591ecae8790472b29bb0e7d8986da4ae` |
| EventStore | `9542d3c9f48bf9ce1c57f2ef68904703eaba56cc` |
| FrontComposer | `0e114214007c22f5cdbac21a6853cff4208340ee` |
| Memories | `dbe4ce0a97c6a3a883af800f7873a99b79434d44` |
| Commons | `116d26815eb81e35b3c161e1799e5ee12805fc0a` |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` |
| Projects | `caf3721427f0b4834415369c2b4a5f981725474f` |

The loaded Builds central package catalog remains SHA-256 `1e2621765f2632077f906e945bf4d74916fef5dec04ec34d630dfce545dfbc78`. These submodule revisions were observed again after the final parity run and were unchanged. No submodule was staged, committed, updated, or reverted.

### Verification

| Command or check | Result |
| --- | --- |
| `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1` | Exit 0; 0 warnings, 0 errors. Restore reported all projects up to date. |
| `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` | Exit 0 on the final candidate bytes; 12/12 categories pass, including `rest-sdk-golden-parity` and `pd10-v2-conformance-set`. Report SHA-256 `03f22fd4ee8d0018b3ee1ea42b5f93fd55cfa81e518a0bbc67b9c1583e78ae43`. |
| `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` | Exit 0; governance 22/22, authorization matrix 7/7, conformance 3/3. Report SHA-256 `4a5630580f3aec35925bef605bfa2616dc16452024abc592ca2be21d074997a7`. This suite does not prove every Section 9 provenance binding. |
| Integration executable: `-class Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests` | Exit 0; 18/18. Hold, coexistence, retirement, attribution, and invalid modes remain covered. |
| Contracts executable: A6b conformance, v2 candidate, matrix, Release package, and NFR classes | Exit 0; 41/41, including `StableReleaseShouldUseStableDaprIntegration`. |
| Contracts executable: `-class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests` after the final reseal | Exit 0; 3/3, including byte-for-byte generator reproduction. |
| Projects Release/package build of `tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj` with `UseHexalithProjectReferences=false` and `--no-incremental` | Exit 0; 0 warnings, 0 errors. Assets resolve Client and Contracts to `1.1.1`. |
| Projects test executable: folder and file-reference directory classes | Exit 0; 39/39. |

Direct comparison of the 20 current provenance bindings matches 16/20. The same four mismatches remain:

| Current provenance path | Bound SHA-256 | Observed SHA-256 |
| --- | --- | --- |
| `_bmad-output/planning-artifacts/prd.md` | `743e8f7a001f67a136d817154af0f25773fab21ef6e6bc5adf08c1e3694d731c` | `b08c5bb51a8d07b0a452837304486b69a1ce5c1f63ca4e7b2a360393b0c45608` |
| `_bmad-output/planning-artifacts/architecture.md` | `74ef242f6bfb77458818ab08a8acc25f547d6c891d29d36ab4954f7ca879973e` | `da13b2bd481b0c72355ad41ecf52b63f303711f4f5293ea89673cd05f916fce1` |
| `_bmad-output/planning-artifacts/epics.md` | `a863dc5a6f1b44986fa2dadb9c21f1657c02700b8506368b167d651274e9dbcc` | `d1ac3564c5f50b48f4d2978546703dab82c1b8288d70c203317bf45d23c70492` |
| `docs/exit-criteria/nfr-traceability.md` | `d490ef1178d74198982035b3b3adbaa4cc5a479f32b79711fc04b9789d7cd588` | `7e3065a226339a78875f0687f39711f31ac8d21a0160f6e8af5ffb061f97423c` |

Those four bindings stay with the owning planning reconciliation. Because they still fail S9-02, this recheck does not establish a passing current A8 technical condition. The earlier September passing replays and the same-day 11/12 parity result remain historical.

### Entry decision

Keep the hold. Check 1 still fails on the four provenance mismatches, the difference between the released `1.1.1` pair and this candidate, and the absent production artifact tuple. Check 2 still lacks a Projects deployment artifact and a verified periodic schedule. Checks 3 through 5 still lack production attribution, a baseline, thresholds, a consumer census, an exact-artifact rehearsal, UTC slots, and named owners. The ordinary production host still does not register a durable EventStore-backed `IFolderRepository`; that seam stays with Story 12.1. No remote release, deployment, route change, retirement, or closure was attempted. Story 1.17 stays open.

## Addendum 2026-10-08: current provenance reconciliation and entry hold

This replay implements the resumed spec's current-only provenance task on Folders revision `039ec7a54a603b64c16e63e53e3640a99ccf5674` plus the recorded evidence edits. The four planning-document mismatches in the preceding addendum are resolved. Current A8 technical readiness is still **not established**: the renewed governance check finds a separate OQ4 catalog mismatch committed after the earlier green result. The technical result and production entry decision remain distinct. `V1Only` stays required and T0 stays unset.

### Checked changes and exact current identity

The current input repair follows Jerome's accepted conditional migration decision and the single-owner policy's changed-input recheck rule; the resumed spec records this bounded task. Commit `fb5511741623f672f51f9aa1192860ae52a0fe2b` records the approved McpCli correction to the PRD, architecture, and epics. Commit `12b3006819968bd52d595a12c9f163eac635478d` applies the [October 7 E1–E14 decision](story-4-19-prerequisites-decision-2026-10-07.md); its new story and dependency graph were checked without executing another story. Commit `ffd029517008b3c9ad5c094244ba8e1a563e9fac` changes the NFR traceability setup command to root-only `--checkout`. The current candidate inventory also needed the committed `RepositoryBindingService.cs`, `FinalAclReauthorizationTests.cs`, and `FinalAclRevokingPermissions.cs` changes. The discovery update adds the supplied Platform production evidence and its limits.

Only five top-level `provenance` digests in the planning manifest changed: the four planning inputs and the generated candidate manifest. All other manifest content is unchanged, including historical approvals/finalization bindings, the accepted October planning amendment, execution flags, graph, and lifecycle values. Historical v1, the authorization matrix, OQ3 evidence, approval register, frozen Story 3.14 specifications, and tracker bytes remain unchanged.

| Artifact | Exact current identity |
| --- | --- |
| Conformance manifest | Raw SHA-256 `8e41c87c1f863010fe95421490c1875deaf7bfcbfedeb5ffb88103b765f876f4`; 41,900 bytes; declared candidate set `6f430f554412b79336984bd6440f889830b09fd64129d7d92df1759b659a57f6`; 224 artifacts. Two isolated generations match each other and the checked-in file. Every artifact digest, byte count, and ordered-set binding was independently checked. |
| Current planning manifest | SHA-256 `8597687044d9d9ac5f3bf9aa3a4d2f67d5e3aa61fd7005ad2c27c3e6a39ca3dd`; 20/20 current bindings match. `v2_exposure_authorized` remains `false`; Story 1.17 remains `backlog`. |
| Preserved tracker | SHA-256 `cc964658f61d7cc4bf7ca61478f2fb75720b60c6028708dc65d4cc1a0930e540`. No tracker transition or regeneration occurred. |
| Cached Client/Contracts `1.1.1` | Client archive SHA-256 `e778aad218dcaf670fe8fb40ff40a83d76f224b62d77d6a4d0612aed9eab5db4`; Contracts `cb85fb7d0e6013ccbe3058061c9e6a57fbdd1a95367f97692dc187a73fe0cf0c`. Both archive identities embed release source `d5f49e96dfab10bf2839ec263a343a6a4c13b06f`; Client depends on Contracts `1.1.1`. These cache/archive checks do not claim a new publication or registry observation. The pair remains different from this candidate. |
| Projects local artifact | Revision `caf3721427f0b4834415369c2b4a5f981725474f`; Server DLL SHA-256 `10b9f57aea20bdc126a46eb45042e2d1c76cfcc33a6b81cf0a2b5187face69d8`; dependencies `f1cb8d209f47932aa9afd38c99f3051972da8583835f5ec504c8ff833563555b`. Assets resolve both Folders packages to `1.1.1`. This is a local build, not a production deployment artifact. |

The root repository and all root-declared submodule revisions were captured before verification and checked again after it. No dependency was updated by this replay. Build-relevant identities are:

| Repository | Exact revision |
| --- | --- |
| Folders | `039ec7a54a603b64c16e63e53e3640a99ccf5674` plus the recorded evidence edits |
| Builds | `58d9b546b4741a246121ab40fc3945703db2e19b` |
| EventStore | `07d1e23a6c5b06bbbb1fc8ddb5174cc3382d3d93` |
| Tenants | `032573384d3df4bc7a5bc0e69945ecca4e00967f` |
| FrontComposer | `0e114214007c22f5cdbac21a6853cff4208340ee` |
| Memories | `906bc07ad6a8e4912a7222d9d097da434148266a` |
| Commons | `b247ed116c6523f8c596ec0a933eff8973d11568` |
| PolymorphicSerializations | `98de6e013840ece9f0fa7c68ab7dcdf2bba3b375` |
| Projects | `caf3721427f0b4834415369c2b4a5f981725474f` |
| Platform | `f5a0d72f9b72e88008562147a7085da0607d55f7` |

The loaded Builds central package catalog has SHA-256 `66dafc002f544948ed192971265f4490b11f1c359004cbb486849d8568a3cb5c`.

### Verification and Section 9 replay

| Command or check | Result |
| --- | --- |
| `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1 -v:quiet` | Exit 0; zero warnings/errors. |
| Projects: `dotnet build tests/Hexalith.Projects.Server.Tests/Hexalith.Projects.Server.Tests.csproj --configuration Release -p:UseHexalithProjectReferences=false --no-incremental -warnaserror -m:1 -v:quiet` | Exit 0; zero warnings/errors with the tracked `1.1.1` pin and no package override. |
| `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` | Exit 0; 12/12 categories. Report SHA-256 `03f22fd4ee8d0018b3ee1ea42b5f93fd55cfa81e518a0bbc67b9c1583e78ae43`. |
| `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` | **Exit 1; governance 21/22.** OQ4 catalog binding fails as detailed below. The script correctly stops on that failure. Report SHA-256 `4bb24c098ca7e79350cd1b5e2eafe1ac4a3adbb47f9788528a3d7a0fedcf90c6`. |
| Release Contracts executable, full suite | Initial result 340/346: five generator tests lack Python `jsonschema`, plus the OQ4 failure. Repeating with an existing offline Python 3.13 environment supplies the prerequisite: **345/346**, solely OQ4 fails. No repository dependency or generator behavior changed. |
| Contracts executable: conformance, v2 candidate, matrix, Release package, and NFR classes | Exit 0; 41/41, zero skips, including all 17 NFR traceability cases. |
| Domain executable: `-class Hexalith.Folders.Tests.Aggregates.Folder.FinalAclReauthorizationTests` | Exit 0; 4/4. Binding ACL revocation stops provider dispatch or event append at the tested boundaries. |
| Integration executable: `-class Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests` | Exit 0; 18/18. Hold, coexistence, retirement, bounded attribution, and invalid modes are covered locally. |
| Projects executable: folder and file-reference directory classes | Exit 0; 39/39, zero skips, against released Client/Contracts `1.1.1`. |
| Current Section 9 input audit and two isolated conformance generations | Exit 0; all current inputs and preserved control/historical bytes pass their direct checks. |

The final evidence wording correction identifies Jerome's existing decision and policy as approval authority, with the resumed spec recording the bounded task. The twelve-category parity replay completed before that documentation correction. The affected candidate inventory was then regenerated twice and its current binding refreshed; `Pd10ConformanceSetTests` passed 3/3 on the final bytes. The final independent input/control audit at `2026-10-08T17:27:29Z` passed without revision drift. No code, package, or historical approval changed during that correction.

Focused executable commands were:

```bash
tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests -class Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests -class Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests -class Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests
tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests -noLogo -noColor -class Hexalith.Folders.Tests.Aggregates.Folder.FinalAclReauthorizationTests
tests/Hexalith.Folders.IntegrationTests/bin/Release/net10.0/Hexalith.Folders.IntegrationTests -noLogo -noColor -class Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests
# Run from references/Hexalith.Projects:
tests/Hexalith.Projects.Server.Tests/bin/Release/net10.0/Hexalith.Projects.Server.Tests -noLogo -noColor -class Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests -class Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests
```

The full-suite prerequisite retry prefixed `PATH` with the existing `/home/administrator/.cache/uv/archive-v0/SMU-_9vAZ4i-6HZv/bin` environment before invoking the full Release Contracts executable without a class filter. Both offline `uv run --with jsonschema --with PyYAML` probes, including one selecting `/usr/bin/python3`, could not resolve cached native dependencies; the existing environment avoided downloads and supplied the required validator.

**OQ4 failure:** `GovernanceCompletenessGateTests.Oq4ProviderCompatibilityPackageBindsVersionDigestApprovalsAndRuntimePosture` expects catalog SHA-256 `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a`, but `docs/contract/provider-compatibility-catalog.md` now hashes to `4a9d360e446ddbc622a6bc977a5a7357bd0dbc99d30d6d39d3c36efe0dde1474`. Commit `6b7274a` changed the live-evidence paragraph; the historical OQ4 evidence and test still bind the previous catalog. This replay preserves both the new provider-owner documentation and the historical approvals. The mismatch requires owning reconciliation; refreshing the planning provenance does not approve it or make the governance gate pass.

| Section 9 check | Current result |
| --- | --- |
| S9-01 decisions | Pass: all eleven historical A1–A8 decision-payload hashes, authority inventories, and approval payload bindings match. No approval is transferred to this candidate. |
| S9-02 provenance | Pass: 20/20 current bindings and the register binding match; tracker unchanged. |
| S9-03 requirements | Pass: FR1–FR58 and NFR1–NFR84 inventories remain complete; FR identities in PRD/epics and NFR identities in epics match. The NFR suite binds the PRD's unnumbered bullets. |
| S9-04 NFR traceability | Pass: 17/17. |
| S9-05 lifecycle | Pass: 159 canonical story rows each match one tracker key; 111 done, 45 backlog, two in progress, one review. Story 1.17 remains backlog. |
| S9-06 frozen lifecycle | Pass: both frozen Story 3.14 specification hashes match their recorded values; its lifecycle remains backlog. |
| S9-07 candidate inventory | Pass: 224 raw digests and byte counts, the ordered-set digest, matrix/v1 bindings, and two isolated generations match. Exposure and closure flags remain false. |
| S9-08 graph | Pass: 73 ranked nodes, 262 unique edges, 24 accepted-terminal edges; no unresolved, equal-rank, forward-rank, or duplicate dependencies. Strictly lower prerequisite ranks exclude cycles. |
| S9-09 verification | **Fail:** builds, parity, focused authorization/package/NFR and consumer checks pass; the full Contracts and governance gates fail only the OQ4 catalog binding after the Python prerequisite retry. |
| S9-10 authority | Pass: the September planning manifest remains current execution authority; the August snapshot is cited only as superseded provenance. |
| S9-11 technical condition | **Not met:** S9-09 fails. No execution or exposure flag changed. |

### Platform evidence and entry decision

The user identified `Hexalith.Platform` as the production deployment owner. Its dated [census](../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261004t085143z-census/inventory.json), captured `2026-10-04T08:52:47Z`, has SHA-256 `401d310ae750916b766486db309901c7577f8c878c76a85f827eec50a107bfaf` and inventories 2,596 resources across 23 namespaces in one cluster. It contains no Folders/Projects workload, service, ingress, or image. That dated, single-cluster observation is not a current/global absence claim and supplies no production artifact tuple or route-attributed traffic. The [native collection procedure](../../references/Hexalith.Platform/eng/cluster-management/QUALIFICATION.md#repeatable-collection-and-validation) documents fresh collection with explicit compatible tools and custody inputs. No live collection occurred here.

The [Platform source-precedence decision](../../references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md#source-precedence-and-module-integration) and [Folders reconciliation](../../references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/reviews/reconcile-folders.md) accept the common Platform MVP infrastructure envelope and prohibit a separate stricter Folders infrastructure enrollment condition. Module behavior, authorization, durable-data safety, and release gates remain required. This replay adds no infrastructure condition.

**Entry decision: hold.** The stale current planning hashes are resolved. Check 1 still lacks a passing OQ4/governance result and an exact tested release/production artifact tuple; cached `1.1.1` is a different candidate. Check 2 has a committed, locally tested Projects read adapter, but no identified production deployment artifact or verified periodic schedule. Checks 3–5 still lack attributed production traffic, a baseline, numeric thresholds, complete consumer discovery, exact-artifact reversal, UTC slots, and named owners. The ordinary Folders host's durable repository registration remains Story 12.1 work, with no verified production registration supplied here. Local routing and adapter tests do not constitute production migration, observation, retirement, or an expiry/incident rehearsal. No remote publication, deployment, activation, retirement, closure decision, or tracker delta occurred.

## Addendum 2026-10-08: OQ4 current evidence reconciliation

Checked on Folders revision `e189762595018a463f9df3eb2aee4e7fa4b815af` plus the uncommitted reconciliation, with final verification after review corrections complete at `2026-10-08T19:16:39Z`. **The OQ4 mismatch is resolved: governance and the complete Contracts suite pass on the current evidence. Migration entry remains on hold.** The [reconciliation spec](spec-1-17-reconcile-oq4-provider-compatibility-evidence.md) records this bounded work; the [current approval policy](../../docs/governance/approval-policy.md) governs its authority.

### Policy and preservation

Commit `6b7274ac06f1bdcb03bc6b10071384406bfd7386` changed exactly one catalog paragraph. The corrected Forgejo lane separates live version/create/conflict/bind/denial/isolation observations from production admission results, deletes stale reports, and archives evidence-class metadata. It changes no accepted compatibility behavior, governed provider, ceiling, gap, consumer scope, production exposure, rollback plan, or C12 evidence standard. The policy's changed-input rule therefore permits a technical refresh without another owner decision. No new acceptance is invented, and no deployment or credentialed run is claimed.

The original OQ4 manifest bytes are preserved as an exact prefix, including catalog digest `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a`, September 15 date, required authorities, all three approval records, runtime limits, and historical reopen wording. An appended `current_evidence` block separately binds catalog digest `4a9d360e446ddbc622a6bc977a5a7357bd0dbc99d30d6d39d3c36efe0dde1474`, the correction revision, refresh date, and policy digest. The entire catalog, including its corrected live-evidence paragraph, and the historical C12 fixture remain byte-identical to the starting revision. Operations and gate documentation now explain the current policy and historical/current binding distinction.

The governance gate pins both catalog identities independently and checks the current policy bytes. Missing or tampered current evidence, an attempted transfer of the current digest into a historical approval, or an unreviewed catalog/manifest pair fails closed. Catalog approval still grants no provider-ready or dependent-story completion; credentialed live-provider evidence remains `not-run`.

### Exact current bindings and verification

| Artifact | Current SHA-256 |
| --- | --- |
| OQ4 evidence manifest | `409c72e7d87286f6aa91c8158cf5f35d7c34b86e5dbef93aa2820fe9b8322ce7` |
| Governance test source | `8b879a5932491056574775a5be3ec37dad7fe1c56ab62d9c298e260c556fa222` |
| Generated conformance manifest | `157c3025375c91ac65b800bcc1b3ba5285cb847bfd99ef8dedee2d1d14c19346` |
| Candidate set, 224 artifacts | `b0961226f98d3e4c5941bbb8db33abde36f8a243b907614fa09f24ff8b656679` |
| Current planning manifest | `7f585098d643c8d409d56148ad8d8875e0eed3cf6ecb5ac8c64b5d7d5019eaf2` |
| Governance gate report | `5f18ede6f3d772c51a6c69baed5770e9d43e4d159f3a721c0f81069cb827ccbb` |

Only three current digests in the planning manifest changed: its OQ4 evidence reference and the top-level provenance entries for the governance test source and generated conformance manifest. All other parsed content is identical, including every historical approval/finalization binding, execution flag, graph, lifecycle value, and accepted decision. Both isolated generations reproduce the final 224-artifact inventory; the allowlist is unchanged and its only changed artifacts are the governance test source and gate report writer. All 20 current provenance bindings match.

| Command or check | Result |
| --- | --- |
| `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` before repair | Exit 1; governance 21/22. Reproduces the historical/current catalog digest mismatch. |
| `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1 -v:quiet` | Exit 0; zero warnings/errors. |
| `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` after repair | Exit 0; governance 23/23, authorization matrix 7/7, provider catalog 3/3. |
| `PATH=/home/administrator/.cache/uv/archive-v0/SMU-_9vAZ4i-6HZv/bin:$PATH tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests -noLogo -noColor` | Exit 0; **347/347**, zero errors/skips/not-run, including current OQ4 negative controls, conformance reproduction, candidate, authorization, package, and NFR contract checks. Uses the previously identified offline Python validator; no dependency update. |
| `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root /home/administrator/projects/hexalith/folders --output /tmp/oq4-conformance-first.yaml` and the same command with `/tmp/oq4-conformance-second.yaml` | Both exit 0; byte-identical outputs matching the checked-in manifest; 224 unchanged paths. |
| Independent input and preservation audit | Pass: 20/20 current hashes, current OQ4 manifest binding, original OQ4 prefix, catalog/C12/register/v1/matrix/OQ3/tracker hashes, and all unchanged planning control/history fields. |

Independent review identified an unpinned policy digest input, a missing policy entry in the gate report, acceptance of extra non-scalar YAML keys, and three constraints lost during epic-context regeneration. All six findings were corrected: the policy is LF-pinned and reported as a canonical input, complex keys fail closed with negative controls, and task-binding, bounded-reconciliation, and Story 4.23 dependency requirements are preserved. The focused build, governance command, full 347-test Contracts suite, two generations, and preservation audit all passed again on the final bytes. No findings were deferred.

This supersedes the preceding addendum's OQ4 failures in S9-09 and the complete Contracts suite. The previous Section 9 preservation results remain applicable to their unchanged inputs; this replay refreshes the affected technical evidence and supplies no new production observations. Previous CI/parity/routing/ACL/Projects results are retained as dated evidence; those broader lanes were not rerun by this bounded reconciliation.

### Entry decision

**Hold: keep `V1Only` active, T0 unset, and Story 1.17 `backlog`.** Check 1's OQ4/governance blocker is cleared, but an exact tested released-candidate/production artifact tuple is still absent; cached Client/Contracts `1.1.1` represents a different candidate. Check 2 still lacks the Projects production deployment artifact and verified periodic schedule. Checks 3–5 still require production attribution, baseline/thresholds, consumer census, exact-artifact reversal, UTC slots, and named owners. The ordinary Folders host's durable repository registration remains with Story 12.1. No publication, deployment, route activation, live provider run, T0, retirement, closure, tracker transition, or Git commit occurred.

No new owner decision is needed for this correction. Any future change to provider support or behavior, accepted risk, consumer/production scope, or rollback must be presented to Jerome as an exact proposal under the current policy before its dependent action. Supplying missing operational evidence within the accepted migration conditions does not itself require another approval round.

## Addendum 2026-10-08: exact release-artifact preparation and entry hold

Verified on committed Folders `02152bace0c782b2b65bf9ccd31e4738a94eac17` (source tree `29bac1fc91a5fd0e6345a1d1c71e9e86a4b50cc3`) and clean Projects `caf3721427f0b4834415369c2b4a5f981725474f`. **A matching local Client/Contracts `1.2.0` candidate and Projects consumer build now pass. That candidate is unpublished and has no production deployment tuple; migration entry remains on hold.** The [structured identity record](evidence/story-1-17-release-identity-2026-10-08.json) records all eleven dependency revisions, package and symbol hashes, source differences, commands, test counts, gate report identities, and preserved evidence. Product source matches the committed revision; the new evidence/spec documentation and release report are outside the candidate allowlist.

### Required identity and released comparison

The entry identity must connect the exact Folders source and current conformance/matrix digests to the published Client/Contracts versions and NuGet archive hashes, successful exact-source CI/release run, and the actual Folders server image/configuration. It must also connect the Projects source, resolved package hashes and passing consumer checks to its deployment artifact. Platform must provide the immutable image digests, deployment IDs/status, active configuration and route evidence for both workloads. Package version alone, a successful deployment API status alone, or matching generated transport code cannot supply that chain. API v2/candidate `2.0.0` and NuGet package `1.2.0` are separate version domains.

The unchanged current manifest has raw SHA-256 `157c3025375c91ac65b800bcc1b3ba5285cb847bfd99ef8dedee2d1d14c19346`, candidate-set SHA-256 `b0961226f98d3e4c5941bbb8db33abde36f8a243b907614fa09f24ff8b656679`, and 224 artifacts. All 224 raw hashes match, two isolated generations reproduce it, and all 20 current planning bindings match. OQ4's reconciled current manifest remains `409c72e7d87286f6aa91c8158cf5f35d7c34b86e5dbef93aa2820fe9b8322ce7`; historical approvals, finalization fields, tracker, matrix and v1 bytes remain unchanged.

Fresh read-only NuGet downloads returned HTTP 200 for both `1.1.1` archives. Both identify release source `d5f49e96dfab10bf2839ec263a343a6a4c13b06f`, confirmed by the immutable remote `v1.1.1` tag and [GitHub Release](https://github.com/Hexalith/Hexalith.Folders/releases/tag/v1.1.1). NuGet archive SHA-256 is Client `e778aad218dcaf670fe8fb40ff40a83d76f224b62d77d6a4d0612aed9eab5db4`, Contracts `cb85fb7d0e6013ccbe3058061c9e6a57fbdd1a95367f97692dc187a73fe0cf0c`. Their extracted payloads equal the GitHub assets entry for entry after excluding NuGet's added `.signature.p7s`; signed-feed and unsigned GitHub archive hashes are recorded separately. This compares payloads, without claiming a fresh cryptographic trust-chain validation.

The released inventory has 223/223 matching hashes, manifest `264380b817fc19092f075052de66f97a71c72af849b53a8b6473840d8269e2e9`, candidate set `d36d35edd348e0893e313d408e2cc8f592429c0701643fb825972c0c10325031`. Against today's 224-path inventory, 27 shared paths differ and one is new; all differences are listed in the structured record. Generated transport SHA-256 stays `fd42741ccb5c493fb37fbca02fb1660e35937f989335c1c1c96e572f9c5ac082`. The v2 spine changes from `586eb24b09d5dfec1d9946ef88e00cdb31684333c81b9d76f3862713ce4ab0c0` to `314681a3b1822b1d195c25d83e607014c4f4dc37d64ef8537a6f5144c2edae98`, and helpers from `877c1af948440950c51ce6eb36e9bb3a99e2ba6ada36d5df9c19cbdd2c2171fa` to `68c7cfadcb46b2f684e9e60adcef0ff1d59f441d512f572adef456301b634e08`. Released `1.1.1` therefore remains a different candidate.

### Prepared packages and consumer evidence

The configured semantic-release commit analyzer reports a minor release across 29 commits since `v1.1.1`, proposing **`1.2.0`**. All five public NuGet indexes lacked `1.2.0` at `2026-10-08T21:07:52Z`; that observation is neither a reservation nor publication. The existing release tool prepared all five mandated package/symbol pairs at this version, without changing the inventory. Each archive identifies Folders `02152bace0c782b2b65bf9ccd31e4738a94eac17`.

| Local unsigned archive | SHA-256 |
| --- | --- |
| `nupkgs/Hexalith.Folders.Client.1.2.0.nupkg` | `f2af8952faf43c1da25a1bc84ae13e2fb4828dfac24b31cad30f946736ecd8f4` |
| `nupkgs/Hexalith.Folders.Contracts.1.2.0.nupkg` | `9c34b6dc33e256778becdb93f5429bef2c5b4f99f2700e7fdd1a74e57f6baaac` |
| `nupkgs/Hexalith.Folders.Client.1.2.0.snupkg` | `5560fd7709abdf42f32e3cb667495e3c9f3016a9a555a2a985aa0bd7919cffc3` |
| `nupkgs/Hexalith.Folders.Contracts.1.2.0.snupkg` | `cb7264d15cad47988c8177219ff54835d48711f7ea20a05dc39391d5b7d624b7` |

Client's internal dependency selects Contracts `1.2.0`. The gate checks same-version closure across all five packages. Both packaged assemblies equal their current Release build outputs. Projects was restored with `HexalithFoldersVersion=1.2.0`, package mode, an isolated cache, and source mapping that restricts `Hexalith.Folders*` to this local feed. Its asset-file SHA-512 values, cached archive bytes and test runtime DLLs all match the prepared pair, excluding a silent fallback to published `1.1.1`.

The Projects Server DLL SHA-256 is `59bb74d9ab847bf0fe68ac0696c689933c314d07b15b7df1e86c9f2d0a963ee8`; its server `.deps.json` is `90eff804728762161bcda1d92c5b638bc5af99efa63c22dfef0f5e644ca12cd7`. Candidate assets and runnable test outputs are retained under `nupkgs/artifacts/projects-consumer-1.2.0/`. These are local build identities, with no image, deployment ID, live-health or preproduction rehearsal claim. Projects' tracked `1.1.1` pin was not edited; its original package restore/build state was restored after retention. Previous local outputs are preserved under `nupkgs/artifacts/history/`.

| Verification | Result |
| --- | --- |
| Full same-run `run-release-package-gates.ps1` with corrected local version, then `-Version 1.2.0 -SourceRevisionId 02152bace0c782b2b65bf9ccd31e4738a94eac17 -SkipRestoreBuild` | Exit 0: full Release/package solution prerequisite, per-package restore/build, exact five package/symbol pairs, metadata/archive/dependency validation and two isolated package consumers. The 1.2.0 report SHA-256 is `69aa102ed2e41e33c97f355463a8d76c5a36d8ecdac1b0a7b043333d5f197e0b`; `DryRun`, publication `skipped-dry-run`. Its legacy `pushed_package_ids` field enumerates inventory and does not establish a push. |
| Full current Release Contracts and Client executables | Exit 0: Contracts **347/347**, Client **329/329**, zero errors/failures/skips/not-run. Their tested library DLLs equal the packaged library bytes; the tests do not consume a remote release. |
| `pwsh -NoProfile -File tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` | Exit 0: governance 23/23, matrix 7/7, catalog 3/3; report remains `5f18ede6f3d772c51a6c69baed5770e9d43e4d159f3a721c0f81069cb827ccbb`. |
| `pwsh -NoProfile -File tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` | Exit 0: **12/12** categories; report `03f22fd4ee8d0018b3ee1ea42b5f93fd55cfa81e518a0bbc67b9c1583e78ae43`. |
| Projects `Server.Tests` restore and forced Release/package build with command-only `HexalithFoldersVersion=1.2.0`, followed by both directory test classes | Exit 0; zero build warnings/errors; **39/39** tests. Covers lifecycle, effective permissions, file metadata, canonical denial and authority-unavailable outcomes using mocked HTTP Tier-2 adapters. No live Folders/provider, production traffic, periodic schedule or deployment is exercised. |
| Original Projects `1.1.1` restore/build and preservation audit | Exit 0; zero warnings/errors; clean tracked Projects source; all eleven root dependency revisions and 35 protected file digests unchanged. |

The first local command rejected `0.0.0-local.story117.02152bace0c7` with exit 1, `version-policy/invalid-semver`, before building or packing. Prefixing the hash component with `rev` passed the existing validator. That preliminary local version and the subsequent concrete `1.2.0` dry run are distinct preparations; only the latter is the public-version proposal. Exact commands and results, with transient scratch paths represented by placeholders, are in the structured record. No version rule or test expectation was weakened.

Independent review found ten reproducibility gaps in the initial record; all were patched without changing package bytes or candidate inputs. The structured record now contains the complete NuGet configuration template, explicit retained paths for the six consumer build identities and every test log, the complete [382-file consumer bundle manifest](evidence/story-1-17-projects-consumer-files-2026-10-08.json), and the archived same-run solution prerequisite report/digest. It identifies the offline validator as Python `3.13.13`, records its relevant exact package versions and reconstruction commands, and timestamps each read-only GitHub preflight query. The public feed absence observation remains separately dated; recheck it before publication.

Run `python3 _bmad-output/implementation-artifacts/evidence/verify-story-1-17-release-identity.py` from Folders to repeat the offline source, archive/payload, log, bundle, current-binding and preservation audit. The [audit script](evidence/verify-story-1-17-release-identity.py) passes and rejects a mismatched prepared-archive digest with exit 1. Run `node _bmad-output/implementation-artifacts/evidence/analyze-story-1-17-release.mjs` to reproduce the proposed version using the recorded configuration, commit range, Node/package versions and lockfile. Neither script publishes or inspects live deployments. Raw local artifacts/logs are retained under the ignored `nupkgs/artifacts/` tree; source-controlled evidence contains their metadata and hashes.

To exercise the retained `1.2.0` consumer after Projects' normal build state has returned to `1.1.1`, run `nupkgs/artifacts/projects-consumer-1.2.0/tests/Hexalith.Projects.Server.Tests -noLogo -noColor -class Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests -class Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests` from Folders. This retained-bundle rerun passes **39/39**. The original Projects build paths describe the earlier candidate run; the record's `retained_path` fields identify the files matching those hashes now. Final focused release conformance also passes **9/9** against the `1.2.0` report input. All ten review findings are resolved, with none deferred.

### Concrete publication proposal and authority

The proposed dependent operation is the ordinary guarded `release.yml` dispatch from exact current `main` `02152bace0c782b2b65bf9ccd31e4738a94eac17`, producing semantic-release `1.2.0`/`v1.2.0` for **Contracts, Folders, Client, Aspire and Testing**, with all ten package/symbol assets. The read-only source check found live main at that revision and successful completed push CI [37842520868](https://github.com/Hexalith/Hexalith.Folders/actions/runs/37842520868). Workflow action identity and `builds-execution-sha` both remain `3639c8d9340fc81d6f8e0a90566a97e56d5d8446`; the package catalog comes from root Builds `58d9b546b4741a246121ab40fc3945703db2e19b`, separately recorded. The repository publication variable is exactly `true`, `NUGET_USER=jpiquot`, and the `production` environment has one required reviewer. No workflow was dispatched and no credential exchange occurred.

The [accepted migration package](story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md) already authorizes conditional pair publication; the existing release policy requires its complete five-package inventory. This preparation changes no consumer/production scope, window or rollback plan, so the [single-owner policy](../../docs/governance/approval-policy.md) does not require another acceptance merely for changed hashes. The protected-environment reviewer must authorize the actual publication run under `release.yml`; that execution control is distinct from asking Jerome to repeat the migration decision. This request's preparation is complete without initiating that external operation.

Before dispatch/publish, recheck exact live main, exact-source green push CI, version calculation and absence for every package, active registered trusted-publishing policy/creator membership, shared action identity, and current candidate/gates. Repack and verify in that workflow. Local unsigned archive hashes are not promised CI or NuGet hashes: packaging and NuGet signing can change archive bytes. After publication, download and hash all ten GitHub assets and the five NuGet archives, compare signed-feed payloads with the prepared release assets, rebuild/test Projects against the exact published pair, and record its committed package pin and deployment artifact. If main advances, refresh this source/version tuple and affected checks before the dependent operation. Never reuse occupied versions or move `v1.1.0`/`v1.1.1`. A partial five-package publication is an immutable release incident requiring the existing corrective-new-version procedure.

The dependent Projects change is concrete: in its owning repository, update `Directory.Packages.props`'s central `HexalithFoldersVersion` from `1.1.1` to the verified published `1.2.0`, retaining its existing read adapters. Restore from NuGet.org, verify both downloaded archive hashes and resolved runtime libraries, force the Release/package build, and rerun the two adapter classes before recording the new Projects commit/CI and giving its image identity to Platform. No prospective commit, image digest or deployment ID is invented. If publication chooses another source/version, refresh this proposal and checks first.

A new Jerome decision is needed only if that operation materially expands behavior, risk, consumer or production scope, rollback, or the seven-day cap. Examples are admitting mutating v2 callers, adding a consumer to the exception, extending the deadline, or changing the approved package inventory. Present the exact changed scope and risk before such a dependent action; no new acceptance is asserted here.

### Platform-owned production gaps and entry decision

Platform remains the deployment evidence owner. Its retained October 4 single-cluster census and common MVP infrastructure envelope remain the dated evidence already described above; no fresh production collection occurred and no stricter Folders infrastructure enrollment condition is added.

| Entry requirement | Required production evidence / responsibility |
| --- | --- |
| Exact Folders and Projects deployment tuple | Platform supplies immutable images, full source revisions, package/lock identities, deployment IDs/status, route configuration and live read smoke evidence. Published 1.2.0 package evidence and a committed Projects pin/build remain publication/consumer steps, not Platform deployment proof. |
| Durable Folders host | Story 12.1 owns durable `IFolderRepository` implementation; Platform must demonstrate the actual deployed registration and durable behavior before activation. No in-memory substitute or completed Story 12.1 claim is supplied. |
| Inventory, attribution and baseline | Platform coordinates the deployed Projects call inventory and every periodic schedule with the consumer maintainer; supplies version/consumer-attributed request/status/error/latency evidence, safe traces, pre-switch baseline, numeric thresholds and a current complete v1-consumer census. Three source read families and local adapter tests do not supply this evidence. |
| Preproduction reversal | Platform executes ordinary, incident and expiry reversal with the exact images/configuration and prior artifacts; records owner, commands, timing, v1 recovery and v2-disable results. Retain read-only v2 caller scope until any mutation disposition is separately accepted. |
| Window and accountable owners | Platform supplies UTC activation/retirement slots, operations/on-call owners, rollback thresholds and schedule-fit proof: Projects by T0+24h, at least 24h ordinary observation and every periodic call, retirement by T0+168h. No actual slots or named operator were supplied. |

**Entry decision: hold.** The local package/consumer identity gap is narrowed, and the reconciled OQ4 technical pass is retained. The first gate's published-current-candidate and production tuple remains incomplete; check 2 also lacks the published-pair Projects commit/deployment and periodic schedule, and checks 3–5 remain operationally pending. Keep `V1Only` active, T0 unset, and Story 1.17 `backlog`/open. No publication, deployment, routing switch, live provider run, ordinary-traffic observation, retirement, closure or tracker transition occurred.

## Addendum 2026-10-08: retained identity audit after evidence commit

Local Folders HEAD advanced to `503540aef1ebe42af2131cc3910aa52f96255acb` for release-identity evidence; Projects remains at `caf3721427f0b4834415369c2b4a5f981725474f`. The committed change touches evidence, the release gate report, and a separate spec outside the 224-path candidate inventory. The candidate manifest still hashes to `157c3025375c91ac65b800bcc1b3ba5285cb847bfd99ef8dedee2d1d14c19346`.

`python _bmad-output/implementation-artifacts/evidence/verify-story-1-17-release-identity.py` exited 0 at 2026-10-08T23:46Z: 223 released hashes, 224 candidate hashes, 28 changed/new paths, ten retained archives, logs, consumer bundle, current bindings, and protected evidence matched the record. This rechecks the retained preparation; it does not rebuild the local `1.2.0` archives, prove a release from the later commit, or inspect a live deployment. The proposed `release.yml` dispatch bound to `02152bace0c782b2b65bf9ccd31e4738a94eac17` is stale if `503540aef1ebe42af2131cc3910aa52f96255acb` is selected as the current main source. Recheck the selected full revision, successful exact-source push CI, version and destination, package identity, and consumer results before publication.

After the rollback runbook's current-status note was added, `pwsh -NoProfile -File tests/tools/run-adr-runbook-docs-gates.ps1 -SkipRestoreBuild -Configuration Release` exited 0 with **10/10** ADR/runbook conformance tests, zero errors, failures, skips, or unrun tests. Its metadata-only latest report records source commit `503540aef1ebe42af2131cc3910aa52f96255acb`. The candidate-bound consumer-discovery document was left byte-identical to the sealed inventory.

**Entry decision: hold.** The exact-current-candidate published pair, Projects production artifact, Platform deployment/traffic/schedule evidence, durable Folders registration, reversal rehearsal, UTC owners/slots, observation, and retirement remain absent. Keep `V1Only`, T0 unset, and Story 1.17 open with its tracker row unchanged.

## Addendum 2026-10-09: exact-current-main release preparation

The selected and fetched `main` tip is Folders `503540aef1ebe42af2131cc3910aa52f96255acb` (tree `ae3356f17b4b89df4351cacd9ddadac9df0ce8a9`). Live GitHub `main` matched it before dispatch. [Push CI 37861047296](https://github.com/Hexalith/Hexalith.Folders/actions/runs/37861047296) completed successfully for that full SHA, `event=push`, branch `main`; its build, specialized, e2e and accessibility jobs passed. The four pre-existing working-tree edits were outside the 224-path candidate inventory. Two new isolated generations match the checked-in manifest byte for byte: raw SHA-256 `157c3025375c91ac65b800bcc1b3ba5285cb847bfd99ef8dedee2d1d14c19346`, candidate-set SHA-256 `b0961226f98d3e4c5941bbb8db33abde36f8a243b907614fa09f24ff8b656679`, 224 artifacts. The retained identity audit passed 223 released and 224 current hashes; it still labels the older `02152ba` archives as historical preparation.

The current commit's configured analyzer proposed `1.2.0` (`minor`, 30 commits since `v1.1.1`). A fresh read-only preflight found that version absent from all five NuGet indexes, with no `v1.2.0` tag or GitHub Release. The full dry-run `pwsh -NoProfile -File tests/tools/run-release-package-gates.ps1 -Version 1.2.0 -SourceRevisionId 503540aef1ebe42af2131cc3910aa52f96255acb` exited 0: zero-warning Release/package solution build; five package/symbol pairs; metadata, archive, dependency and two package-only consumer checks. Its report is SHA-256 `57a6741500830d7edf0f42918934d41ac9f796830b8da22cec90ff098805ab8b`, with `mode=DryRun`, `publish=skipped-dry-run`. The old `02152ba` archives were preserved separately before this rebuild.

| Current-main local archive | SHA-256 |
| --- | --- |
| `Hexalith.Folders.Client.1.2.0.nupkg` | `b778fd5c8afebcaab92d112e118fc78307f1e41635a3040858ba2a095dbff19b` |
| `Hexalith.Folders.Contracts.1.2.0.nupkg` | `e7e1f240027a0955d739d1bd55998067726d94bd854a9ecdfda1407586af73b4` |
| `Hexalith.Folders.Client.1.2.0.snupkg` | `c2408c6ce7e66d38af5fc516f7d9d9fb96131da93f128f2dd6780cfb54a6d4ad` |
| `Hexalith.Folders.Contracts.1.2.0.snupkg` | `d846a20c316a649aedd3908dd381bcb07af44d01d1b7a4fbc8168b633cbaa491` |

Each archive's repository metadata identifies the full `503540a` revision; Client depends on Contracts `1.2.0`. Clean Projects `caf3721427f0b4834415369c2b4a5f981725474f` restored and built `Server.Tests` in Release package mode with command-only `HexalithFoldersVersion=1.2.0` and an isolated cache mapped to these local archives. The test and Server asset-file SHA-512 values, cache archive bytes and test runtime DLLs match this exact pair. Its two folder-directory classes passed **39/39**, zero errors, failures, skips or unrun tests; the Projects Server DLL SHA-256 is `c88b0a2fd01d84a1c0cd457f8384e8280cb7cc4a579823641fd022d5b547b85b`. These are local mocked-adapter checks, not a deployed Projects artifact or live traffic result.

The existing guarded [Release run 37902452459](https://github.com/Hexalith/Hexalith.Folders/actions/runs/37902452459) was dispatched from `main` for the exact selected SHA. Its `Verify exact green main source` job passed; the `release` job is waiting for the required `production` environment reviewer. No publication is claimed from the local archives or the waiting run. Keep `V1Only` active, T0 unset, and the tracker unchanged while all production entry evidence remains gated.

## Addendum 2026-10-09: retained release-identity replay

The later `503540aef1ebe42af2131cc3910aa52f96255acb` dry run replaced the mutable `nupkgs/` archives and `_bmad-output/gates/release-packages/latest.json`. The original offline verifier therefore failed at the first prepared archive despite the `02152bace0c782b2b65bf9ccd31e4738a94eac17` archives and matching release report remaining under `nupkgs/artifacts/history/story117-02152ba-before-current-build/`. The verifier now accepts that retained copy only when its SHA-256 equals the original structured record. Its revised script SHA-256 is `d07b1d3abcd75fd2a65038a9478ad991b8a0c823bb6c0a63a6b684d5265d0203`; the record's earlier script hash remains historical evidence of the original run.

`python3 _bmad-output/implementation-artifacts/evidence/verify-story-1-17-release-identity.py` now exits 0 and checks 223 released source hashes, 224 current candidate hashes, 28 changed/new paths, ten archives, retained logs/consumer payloads, current bindings and protected evidence. A temporary-record negative control with a changed prepared-archive digest exits 1 with `Prepared archive differs or is missing`. The built routing suite passes 18/18 and the retained Projects `1.2.0` adapter suite passes 39/39. These local checks establish no publication, production entry, T0, observation, retirement or tracker transition. Keep `V1Only`.

## Addendum 2026-10-09: exact `1.2.1` publication and Projects package check

[Release run 37932766815](https://github.com/Hexalith/Hexalith.Folders/actions/runs/37932766815) completed successfully at `2026-10-09T12:54:48Z`. Its exact-source check and [release job 113827343614](https://github.com/Hexalith/Hexalith.Folders/actions/runs/37932766815/job/113827343614) used Folders `17b5a70d2b4b91bbbf73ea63c8a214a0ad426759`; the job reported `RELEASE-PACKAGES status=passed`, pushed the five-package inventory, and published [GitHub release `v1.2.1`](https://github.com/Hexalith/Hexalith.Folders/releases/tag/v1.2.1). The tag resolves to that full source revision. Both Client and Contracts `1.2.1` are present in the NuGet.org flat-container indexes. The preceding [run 37902452459](https://github.com/Hexalith/Hexalith.Folders/actions/runs/37902452459) also completed successfully and published `v1.2.0` from earlier Folders `503540aef1ebe42af2131cc3910aa52f96255acb` at `2026-10-09T08:25Z`; it is historical relative to this exact-source pair. The checked-in 224-path candidate manifest remains SHA-256 `157c3025375c91ac65b800bcc1b3ba5285cb847bfd99ef8dedee2d1d14c19346`; the intervening source commits changed evidence and documentation outside that candidate inventory. The retained identity verifier still passes, but its older archive record is historical preparation, not this publication check.

| Published `1.2.1` package | GitHub release archive SHA-256 | NuGet.org archive SHA-256 |
| --- | --- | --- |
| `Hexalith.Folders.Client` | `99c5996dbdcdd58759b8436a0ee3496616eefc071c29c8d40c0c3a77bf0d7391` | `bc1e621d28a990e2a45bc3869177fb9eb6127673137369d0b35d735350e7f253` |
| `Hexalith.Folders.Contracts` | `325b4509ce924b92d31fb76ba40b21d1ee1eb1e90057ab53a19f6cf01f9ebb3d` | `9acb9e0058fe9f6bdfafe91e531e38f85fda71a96c944fa7c32168ffdbfceaa4` |

The signed NuGet archives have different whole-archive hashes from GitHub's release assets; each contains the same library DLL bytes as its GitHub counterpart. Both NuGet packages embed repository commit `17b5a70d2b4b91bbbf73ea63c8a214a0ad426759`, and Client declares an exact Contracts `1.2.1` dependency.

In the owning Projects repository, the central `HexalithFoldersVersion` pin was changed from `1.1.1` to `1.2.1` in the working tree at prior HEAD `caf3721427f0b4834415369c2b4a5f981725474f`. A fresh isolated-cache restore from `nuget.config` selected the two signed NuGet.org archives above; the `Server.Tests` Release/package build and the `Hexalith.Projects.CI.slnx` Release build completed with zero warnings and errors. Direct execution of `ProjectFolderDirectoryTests` and `ProjectFileReferenceDirectoryTests` passed **39/39**, with no skipped or unrun tests. The built Projects Server DLL SHA-256 is `879c92875325753ed5a8264592a377238bd868214231c9991bd6bc0e8c7b74c8`. This is a local package-profile check: the Projects pin is not committed, and there is no new Projects push CI, immutable deployment artifact, or live call evidence from this run.

**Entry decision: hold.** Package publication and the local released-pair consumer check are established. The Folders production image/configuration and durable repository registration, Projects committed/deployed artifact and complete call schedule, attributed production baseline and consumer census, exact-artifact reversal rehearsal, UTC slots, thresholds, and accountable operators remain unverified. Keep `V1Only`, leave T0 unset, and do not mark Story 1.17 closed or change its tracker value.
