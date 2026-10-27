# Story 1.17 — loader approval retained; root gate stopped execution

**Jerome's “yes” authorizes the original two-case loader experiment. Read-only preflight stopped before any SDK command because the root changed from `43bc73bdf2254008c2547bd3e48f1e8f098a8043` to `43423a9849c82a3a526590b0fa6e613768e7eb24`. No loader case, compiler, model build or runtime probe ran.**

The [approval record](story-1-17-cli-4-38-loader-authorization-2026-10-26.json) preserves the actual reply and exact original plan hash `83fec7a858d1e1a8e9a9e1f514803a7ccb5408f56b1ef15c7c5bad12e4c42e7f`. It does not invent authority to change a failed condition. The original plan explicitly says: **“Root revision drift, including a later proposal-only commit: stop for explicit revised tuple disposition; do not amend approval automatically.”** This is the specific reason execution is stopped, from the [approved plan's stopConditions](story-1-17-cli-4-38-runtime-resolution-plan-2026-10-26.json), rather than a new general permission requirement.

The gate is unnecessarily sensitive to a commit that only records its own already reviewed proposal. This follow-up makes that limitation concrete and proposes an exact replacement for that condition. Nothing in the original approved packet is edited.

## Verified transition and preserved inputs

`43423a9849c82a3a526590b0fa6e613768e7eb24` is a child of the approved root. It adds exactly the original packet's nine files: probe, runner, input manifest, static receipt/archive/member manifest, spec, decision report and plan. There are no other changed paths or gitlinks. The nine files' exact bytes are retained in the [preflight evidence](evidence/story-1-17-cli-4-38-loader-root-preflight-2026-10-26.json).

All thirteen repositories initially report clean working trees and no staged entries. All twelve direct revisions and stage-entry hashes match the approved inputs, including Parties `986a369ec26f6c5194593eb7bce12955eda54149`, Platform `9869094acc21f9054960a573f884c60ddf3235cd`, direct Builds `74932ad0247203797274aba8b7b6366a4bbf92b9` and EventStore `c5d1c280d8c1c1a207cd159d817f614e93c93130`.

Forty-five original proposal/supporting/preserved bindings match. All **503 SDK/compiler/reference/runtime file hashes**, the candidate package hash and model archive hash match by file reads only; no SDK identity command was executed. The prior static archive's **49 members** match their manifest. This reuses its source/metadata findings without redoing assembly inspection or historical build, capsule verifier, graph test or adoption work. File equality does not satisfy a new immediate SDK execution gate; that gate remains required later. The historical incomplete gate remains incomplete.

## One new decision: the root-recording rule

Approve only the root revision/index derivation in the [additive rebind plan](story-1-17-cli-4-38-loader-root-rebind-plan-2026-10-26.json). The original experiment's execution authority is already recorded. The new rule permits root `43423a9849c82a3a526590b0fa6e613768e7eb24`, or a descendant whose complete delta consists solely of additions of the eight explicitly named follow-up artifacts and the separately named rebind-approval record. Each added artifact must match the owner-bound bytes; existing tracked files, modes, gitlinks, direct checkouts and indices may not change. Renames, modifications, deletions, unrelated additions, staged changes and tracked working-tree edits fail.

This handles a subsequent commit recording **this exact reviewed follow-up packet** without requiring another revision-only decision. There is no directory wildcard or general “documentation-only” exemption. Each actual root revision and stage-entry hash is captured in full. An unrelated change still stops execution.

The [proposed adapter](evidence/story-1-17-cli-4-38-loader-root-rebind-proposal-2026-10-26/execute_after_rebind_authorization.py) is complete, unexecuted and Python-syntax checked. After separate rebind authority, it verifies this predicate and copies the original, hash-matched LoaderProbe and execution driver unchanged into a new private adapter directory. It derives a private copy of the original input manifest by changing exactly:

- `repositories["."].revision` to the actual root that passed the predicate;
- `repositories["."].indexEntriesSha256` to that root's actual `git ls-files --stage` hash.

It proves every other input value is equal, records both manifest hashes and both owner records, captures actual repository state, then invokes the original driver without changing its code. The original plan, spec, probe, driver, input manifest and historical receipts remain byte-identical. The original driver's authorization record in the adapter explicitly links the original execution approval and new root-rebind approval; it does not masquerade as the former record.

The plan includes a concrete current-root derivation and the exact later command. An outer `timeout --signal=TERM --kill-after=5s 600s` bounds the adapter and original driver together. The two cases, compiler, immediate SDK/global.json checks, full explicit environment, certificate behavior, read-only mounts, network/PID namespaces, seccomp rules, process observations, per-command deadlines and stop-on-failure behavior all remain as approved. No model rebuild, restore, replacement pin, alternative version or unsandboxed fallback is added.

This is an additive qualification adapter; no dependency, build-property or operational-source patch is established necessary or applied. The affected paths are only the eight named new records and proposed adapter. Its provenance is the verified proposal-recording commit plus the original plan and inputs. Risk: an incorrectly applied root exemption could hide source drift. The implementation therefore accepts only ancestry plus exact additions at named paths with owner-bound bytes, and verifies all twelve direct indices separately. Rollback is to abandon the adapter, retain the approval and failure evidence, and leave the original packet and accepted tool untouched.

## Execution, evidence and cleanup boundaries

The new approval would authorize deriving those two private input fields under the named predicate, then carrying out the already approved two-case experiment. It would not authorize changing the original packet in place, application entrypoints, ComposeRun invocation, run/debug, prerequisite probes, acceptance or any staging check.

The future approval must bind the new plan, adapter, decision receipt and every named root-recording artifact. There is no rebind approval now. The original “yes” is not reused as if it had approved this new condition.

The adapter will retain its canonical root revision/index, root-diff evidence, original/rebound manifest hashes, original approval, rebind approval, derived authorization, actual driver argv/cwd/exit and stdout/stderr hashes under `/tmp/story117-cli438-root-rebind-20261026-*`. The original driver will retain its normal `/tmp/story117-cli438-loader-20261026-*` outputs. Both directories must be included in the actual later execution archive/member manifest. No future result files are pre-created. On a gate failure, retain only what actually exists and report the specific stopping point.

After verified archival and owned-process checks, cleanup may remove only those two newly allocated private directories by their recorded paths after inventory. No automatic cleanup is performed. Historical qualification directories, shared caches, certificate stores and all checkouts remain outside cleanup scope.

All prior qualification limits survive, including the incomplete historical immediate SDK gate, certificate first-use uncertainty, uncaptured inherited environment, overwritten failure record, missing optional PDB and bounded process observations. SDK first-use prevention and real/private certificate-store metadata observations remain mandatory for the later execution. Nothing here claims that the sandbox or new adapter has run successfully.

Ordinary public CLI prerequisites and a genuinely command-produced mapping/ContentHash remain separate. Hosted graph fixtures remain fixture evidence. EventStore/Tenants/Memories dependency policy is unchanged and separate. Parent **in-progress**, tracker **backlog**, **V1Only**, unset **T0**, null **activation authority**, and **0/14 staging checks executed** remain unchanged. CLI4.35.2 remains historical accepted; CLI4.38.0 remains a candidate. Current tuple, package and hosted acceptance remain HOLD.
