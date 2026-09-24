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
