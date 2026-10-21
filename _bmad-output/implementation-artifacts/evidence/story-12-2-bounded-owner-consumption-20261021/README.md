# Task 36 frozen-candidate implementation and incomplete qualification

**Task 36 remains unchecked. No complete authenticated live case passed. The current checkout is not qualified.**

The implementation replaces the obsolete expected-refusal diagnostic with positive original-owner
component cases at10,001 and32,769 positions, using the existing production Folders handler and SDK
named/shared dispatcher. Test transport uses256-event `AccumulatePart` pages, `AccumulateComplete`,
and the SDK durable phase when resuming a sealed operation. Qualification admission is40,000;
100-operation/1,048,576-byte recoverable batch limits remain unchanged. No Folders persistence protocol
or production-source change was introduced.

The additional synthetic HTTP case defines checks for10,001 positions, a second original owner,
sealed staging across process replacement, inheritance, growth/rebuild, historical duplicates and
exact `Failed/delivery_identity_conflict` outcomes. These are intended complete-case assertions.
Its partial executions do not establish a passing HTTP case. An authenticated tiny separate owner
primes the epoch; no cold shared-Begin bootstrap claim is made.

## Executed coverage

| Evidence | Result and boundary |
|---|---|
| [Component summary](component-summary.json), [build7 receipts](component-build7-results.json), [earlier unchanged suites](components-final/components.json) | 4,249 passes,0 failures,2 unchanged optional Domain skips. Fresh build7 Domain2,188 and AppHost helper22 passes; Server1,056, Workers161, docs28 and integration794 outputs remain byte-identical to their passing runs. |
| [Final Domain XML](final-domain-build7.xml) | Both complete original-owner sizes passed through actual Folders dispatcher/handler and supported bounded protocol DTOs.98 historical task views, further growth/rebuild, inherited generations and identity conflicts are checked. In-memory `Task.WhenAll` alone is not claimed as a controlled overlapping CAS race. |
| [Explicit SDK reuse](sdk-reused-evidence.json) | Previously executed74/74 capability cases and six Dapr/process scenarios cover both buffered/streamed sizes, controlled concurrency and true interrupted staging/partial-owner handoff. They are not new Folders execution. The SDK broad gate remains `qualified:false`. |
| [Capacity attempt5](capacity-attempt-5-diagnosis.json) | Partial failure:10,001-position accumulation268.718s, Stage499.334s, all four main hosts replaced, incorrect sealed-phase part replay rejected, authenticated Abort496.642s Completed. Build7 corrects that helper mistake. No complete large HTTP pass or32,769 Folders HTTP execution is claimed. |
| [Strict real-provider attempt2](real-provider-attempt-2-diagnosis.json) | Partial failure after passing corrected staged-session recovery, six-concern rebuild/integrity, persisted retention and ordinary-write substeps. A later canonical eventstore Stop timed out. Proof-a80102/proof-b80063 are launcher IDs; actual proof managed-host identities were not obtained. |

All exact live commands, environment pins, XML, logs and exit codes remain in
`capacity-live-attempt-{1..5}-result.json` and `real-provider-live-attempt-{1..3}-result.json`.
Capacity failures also retain watcher exhaustion, missing initial epoch, observer readiness mistakes
and their corrections. Strict attempt1 hit internal idempotency-actor HTTP429 before clean release;
attempt3 never entered the test because Dapr sidecars failed watcher creation with ENOSPC.
Assertions, real-provider deadlines/retry limits and limiter settings were not weakened.

## Frozen graph and external drift

The full Debug/source solution build passed, followed by final narrow Domain/AppHost build7 with
zero warnings/errors and passing format/analyzer checks. [Source freeze](source-freeze.json) binds
12,025 source/config inputs and546 PE/PDB files. [Exact final bytes](final-build7-byte-archive.json)
preserve554 PE/PDB and authored-harness/document path bindings. Earlier graph/attempt bytes remain separate.

[Parent build7 source binding](parent-source-binding-build7/summary.json) checked271 PDBs,
37,784 ordinary and3,273 generated document copies, with457 archived generated sources and zero
binding errors.18 generated disk/embedded metadata-copy differences across two AssemblyInfo families
are preserved, rather than reported as zero drift. [Runtime identity audit](parent-runtime-binding-audit.json)
matches562 mapped-module observations from failed/partial runs to archived PE/PDB hashes and MVIDs;
this is identity evidence, not an acceptance pass.

Concurrent external `/pushall` activity changed root/SDK HEAD/index and dependency gitlinks, including
substantive SDK source changes. [Observed heads](head-observation-at-handoff.json) and
[external Git state](parent-external-git-state.json) record that activity. This implementation agent
issued no staging, commit, push, branch or dependency-update operations and made no SDK production-source edits.
[Final observed drift](after-execution-audit.json) records172 external frozen-input differences plus
one authored README correction; all546 PE/PDB files remain unchanged. Current compiled-source drift
affects38 paths/352 PDB document copies. Exact original bytes are preserved by
[strict3 recovery](parent-preservation-external-recovery-strict3.json) and
[additional recovery](parent-external-drift-final-additional-recovery.json); the additional Contracts
test input has no PDB binding in this consumer graph. The original build7 README is separately archived.
This preserves frozen executed-source binding and does not qualify the changed checkout.

[Prebuild SDK audit](sdk-prebuild-audit.json) and [pre-external build7 audit](sdk-after-build7-audit.json)
matched all3,654 original SDK sources,86 SDK PE/PDB files and108 packet artifacts at those times.
They must not be interpreted as an unchanged-current-SDK claim after the external commits.

## Blockers and preservation

[Quiet observation](live-continuation-decision.json) shows available inotify watches fell548→35
against1,048,576 while unrelated processes occupied the shared quota. Task sidecars actually failed
watcher creation. No quota or unrelated process was changed; user authorization is pending separately.
[Cleanup](all-attempts-cleanup.json) found no surviving exact task-owned processes or containers.

Redis diagnostics measure point-in-time retained hash sizes/counts, not every physical write or
canonical batch. SDK instrumented run-wide bounds remain explicitly separate. Synthetic capacity
inputs are not real retained actor histories; the strict provider lane uses those real sources.

[Historical preservation](parent-preservation-historical.json) passes all25,017 prior files.
[Archived-source filtering](parent-preservation-archived-source-filtering.json) verifies byte-preserving
Git filtering under this packet's `* -text` rule, including CRLF generated/original sources.
[Parent result verification](parent-task36-verification.json) independently checks the complete frozen
input/binary/archive inventories, XML totals and unchanged frozen intent/baseline with Task36 unchecked.
The [final source diff receipt](parent-task36-final-diff.json) and
[exact source patch](parent-task36-source.patch) retain the reviewed change set.

[Qualification status](qualification.json) is false. Remaining gates are complete strict real-provider
and added10,001 HTTP passes against the frozen candidate; qualification of any changed integrated
source; SDK broad regressions; package/PD10; SM4; and independent scoped/whole-story review.
