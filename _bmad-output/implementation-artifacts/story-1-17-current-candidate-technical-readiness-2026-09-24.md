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
