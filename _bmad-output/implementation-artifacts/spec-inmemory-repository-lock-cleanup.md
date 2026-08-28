---
title: 'Unify in-memory folder repository synchronization'
type: 'refactor'
created: '2026-08-28'
status: 'done'
baseline_revision: '3d428595015379407a911cff85968a7b129aa65f'
review_loop_iteration: 0
followup_review_recommended: false
context: []
warnings: []
deferred:
  - summary: >-
      The pre-existing Int32 EventsAppended test affordance can wrap after more than Int32.MaxValue appended events.
    evidence: |-
      Before this change the int auto-property used unchecked +=, and the refactor preserves that behavior in the gate-protected backing field. This is not caused by the synchronization cleanup and is not observable through IFolderRepository, but an extreme-lifetime test repository could report a negative diagnostic count.
    location: >-
      src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:152
    severity: low
  - summary: >-
      A single per-folder `_lastObservedAt` watermark is shared by all six read-model snapshot writers, so one projection's newer timestamp can advance another projection's reported freshness.
    evidence: |-
      Every `Save*Snapshot` clamps against the same `LifecycleKey(managedTenantId, folderId)` entry while sourcing a different candidate time (`state.ArchivedAt`, `policy.ConfiguredAt`, `state.WorkspaceLifecycleUpdatedAt`, or the append's `observedAt`). The clamp is a max, so whichever writer runs with the newest candidate pins `ObservedAt` for every other projection of the same folder. This predates the change -- the same key and the same max semantics were previously expressed through `ConcurrentDictionary.AddOrUpdate` -- and this story only relocated the operation under the gate. It is invisible in the current suite because the new coverage constructs the repository with no read models and `BranchRefPolicyReadModelTests` registers only the branch-policy read model; registering a second read model would surface the interference.
    location: >-
      src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:255
    severity: medium
  - summary: >-
      The sibling `InMemoryOrganizationProviderBindingRepository` still mixes `ConcurrentDictionary` with `lock (_gate)` and publishes state before its idempotency ledger entry where readers can observe the gap.
    evidence: |-
      Its file comment states it "mirrors the folder repository policy", yet `Load` (line 25) and `TryGetIdempotencyFingerprint` (line 69) never take `_gate`, while `AppendIfFingerprintAbsent` writes `_states[...]` at line 61 and `_idempotencyFingerprints[ledgerKey]` at line 62. A concurrent reader can therefore see appended organization state whose fingerprint is not yet visible, and a caller retrying the same idempotent command would treat it as new work. The only tests touching the type run single-threaded, so nothing observes the window. This story's intent scopes sibling `ConcurrentDictionary` users out ("Never: modify submodules or unrelated `ConcurrentDictionary` users"), so it is recorded here rather than fixed.
    location: >-
      src/Hexalith.Folders/Aggregates/Organization/InMemoryOrganizationProviderBindingRepository.cs:61
    severity: medium
  - summary: >-
      The sibling `InMemoryOrganizationProviderBindingRepository` file comment claims it "mirrors the folder repository policy", a claim this change made false.
    evidence: |-
      Lines 6-7 of that file describe it as mirroring the folder repository's synchronization policy. After this story, `InMemoryFolderRepository` owns all mutable state under one gate with plain `Dictionary` stores, while the sibling still mixes `ConcurrentDictionary` with `lock (_gate)`, leaves `Load` and `TryGetIdempotencyFingerprint` un-gated, keeps `EventsAppended` as an un-gated auto-property, and calls its own public `Load` from inside the gate. The comment now points a reader at a policy the file does not implement. It cannot be corrected here: this story's intent scopes that file out ("Never: modify submodules or unrelated `ConcurrentDictionary` users"), which covers a comment edit in it as much as a code edit. Distinct from the synchronization gap already recorded for the same file: that entry is about the publication window, this one is about the documentation now misdescribing the type.
    location: >-
      src/Hexalith.Folders/Aggregates/Organization/InMemoryOrganizationProviderBindingRepository.cs:6
    severity: low
---

<intent-contract>

## Intent

**Problem:** `InMemoryFolderRepository` combines a repository-wide lock with three `ConcurrentDictionary` stores even though the lock is the actual write-serialization primitive. The split model obscures which operations are atomic and retains redundant per-collection synchronization.

**Approach:** Use the existing repository gate as the single synchronization model for state, idempotency fingerprints, observation timestamps, and append counters. Preserve the public repository contract and observable append, seed, load, and fingerprint behavior, with focused concurrency regression tests.

## Boundaries & Constraints

**Always:** Retain ordinal string-key comparison; keep state application, fingerprint lookup/outcomes, duplicate-seed guards, snapshot writes, and event-count semantics atomic under one gate; validate arguments before entering the critical section; keep tests deterministic and assert observable repository outcomes rather than implementation fields.

**Block If:** Preserving behavior requires changing `IFolderRepository`, event/fingerprint semantics, or synchronization inside injected read-model implementations.

**Never:** Edit the deferred-work ledger; modify submodules or unrelated `ConcurrentDictionary` users; add a second lock, lock-free publication scheme, or new persistence abstraction; weaken tenant-scoped stream keys, duplicate-seed failure, or idempotency conflict behavior.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Equivalent append race | Concurrent appends to one empty stream with the same key, fingerprint, and event | Exactly one append materializes the state and event; every loser reports `FingerprintMatched`; lookup returns the fingerprint | No exception or partial state |
| Conflicting append race | Concurrent appends to one empty stream with the same key and different fingerprints | Exactly one append wins; the loser reports `FingerprintConflict`; loaded state and stored fingerprint belong to the same winner | No overwrite or mixed state/ledger pair |
| Seed/read race | Readers call load and fingerprint lookup while a valid seed is published | Reads complete safely; once load exposes seeded state, a subsequent lookup exposes its fingerprint; final state and lookup are seeded | No collection race or state-before-ledger publication |
| Duplicate seed race | Concurrent seed attempts target one stream | One seed succeeds and one throws the existing `InvalidOperationException`; winning state and ledger remain intact | Fail loud without partial overwrite |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` -- `_states`, `_idempotencyFingerprints`, and `_lastObservedAt` are concurrent collections while `AppendIfFingerprintAbsent` and `Seed` already serialize through `_gate`; `Load`, `TryGetIdempotencyFingerprint`, and `ResetAppendCounters` currently bypass that gate. Replace only these redundant collection primitives and make all owned mutable state gate-protected; snapshot helpers are invoked inside the write critical section.
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs` -- read-only public contract anchor for stream load, atomic append, and fingerprint lookup; no signature or outcome changes.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/CoordinatedSeedEventList.cs` -- new test-only `IReadOnlyList<IFolderEvent>` that parks the seeding writer inside the repository gate on a named enumeration pass, so independent readers can be observed blocking on it; its `BlockOnEnumeration` constant is coupled to `Seed`'s enumeration count and is documented as such.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/InMemoryFolderRepositoryConcurrencyTests.cs` -- new focused coverage for equivalent/conflicting append races and seed/load/fingerprint publication, using existing `FolderCommandFactory`, `FolderAggregate`, and Shouldly/xUnit conventions.
- `/home/administrator/projects/hexalith/folders/.bmad-loop/runs/20260828-163508-c24b/bundles/inmemory-repository-lock-cleanup/intent.md` -- read-only bundle authority containing DW-10 and DW-21; do not update the ledger or bundle.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` -- replace repository-owned concurrent dictionaries with ordinal `Dictionary` instances, gate every access to owned mutable state, and use a gate-local monotonic timestamp update -- establish one coherent synchronization boundary without changing behavior.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/InMemoryFolderRepositoryConcurrencyTests.cs` -- exercise simultaneous append, seed, load, and fingerprint operations, including equivalent and conflicting fingerprints -- prove atomic publication and stable outcomes under contention.

**Acceptance Criteria:**
- Given concurrent equivalent appends for one stream and idempotency key, when all calls finish, then one call reports `Appended`, all others report `FingerprintMatched`, one event is counted, and load plus lookup expose the winning state and fingerprint.
- Given concurrent different-fingerprint appends for one stream and key, when both calls finish, then one reports `Appended`, one reports `FingerprintConflict`, and loaded state is consistent with the stored winning fingerprint.
- Given seed, load, and fingerprint lookup contend on one repository, when a load first exposes the seeded state and lookup follows, then the fingerprint is found, callers encounter no collection failures, and the final seeded state remains intact.
- Given concurrent duplicate seeds, when both attempts finish, then exactly one succeeds, exactly one retains the existing duplicate-seed `InvalidOperationException`, and no winning data is overwritten.
- Given the focused test project and Release solution build, when verification runs, then all tests pass with zero build warnings or errors.

## Spec Change Log

## Review Triage Log

### 2026-08-28 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 2, low 1)
- defer: 1: (high 0, medium 0, low 1)
- reject: 16: (high 0, medium 2, low 14)
- addressed_findings:
  - `[medium]` `[patch]` The seed/read test masked standalone fingerprint locking and relied on an opportunistic publication window; replaced it with bounded coordination that pauses Seed while it holds the repository gate and proves independent Load and fingerprint operations remain blocked until atomic publication completes.
  - `[medium]` `[patch]` The rewritten observation-time maximum had no backward-clock regression; extended the branch-policy read-model test to publish after a clock rollback and assert that observable freshness never decreases.
  - `[low]` `[patch]` Verification evidence did not fully reproduce the default restore blocker and workaround; recorded the exact NU1107 failure, compatible restore/build commands, direct xUnit runs, and unchanged Release solution build.

### 2026-08-28 — Review pass (follow-up)
- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 1, low 4)
- defer: 2: (high 0, medium 2, low 0)
- reject: 21: (high 0, medium 3, low 18)
- addressed_findings:
  - `[medium]` `[patch]` Every terminal `await` in the new concurrency tests was unbounded, so a regression that re-opened an un-gated read or deadlocked the gate would hang the CI lane instead of failing it; bounded `Task.WhenAll`, `seed`, `load`, and `lookup` with the existing `CoordinationTimeout`.
  - `[low]` `[patch]` The change's central invariant -- one gate owns `_states`, `_idempotencyFingerprints`, `_lastObservedAt`, and `_eventsAppended` -- was only inferable from an `UnderGate` name suffix and nothing enforced it; added a class-level `<remarks>` stating the invariant, the plain-`Dictionary` rationale, and the no-reentrant-callback rule, plus `Debug.Assert(Monitor.IsEntered(_gate))` in both `UnderGate` helpers.
  - `[low]` `[patch]` `UpdateObservedAtUnderGate` returned a clamped value that all six call sites store as `clamped`, so the name described neither the operation nor the result; renamed to `ClampObservedAtUnderGate(string folderKey, DateTimeOffset candidate)` and documented the monotonic contract at the declaration.
  - `[low]` `[patch]` The test coordination carried undocumented load-bearing internals: the bare `== 2` enumeration trigger silently depended on `Seed` enumerating exactly twice, and neither `TaskCreationOptions.LongRunning` nor the 250 ms observation window explained itself; named the trigger `BlockOnEnumeration` with a `<remarks>` recording the coupling, and commented both constants and the starter helpers.
  - `[low]` `[patch]` The Code Map omitted `CoordinatedSeedEventList.cs`, a new file this change introduces; added it with its enumeration-count coupling noted.

### 2026-08-28 — Review pass (second follow-up)
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 0, low 3)
- defer: 1: (high 0, medium 0, low 1)
- reject: 25: (high 0, medium 4, low 21)
- addressed_findings:
  - `[low]` `[patch]` Both `AppendIfFingerprintAbsent` calls in the new monotonic regression discarded their outcome. A snapshot is only re-published on an accepted append, so a future change that turned either into `FingerprintMatched`/`FingerprintConflict` would leave the test reading the previous snapshot and passing without exercising the clamp; both calls now assert `FolderAppendOutcome.Appended`, with the reason recorded inline.
  - `[low]` `[patch]` `CoordinatedSeedEventList.BlockOnEnumeration` documented its coupling to `Seed`'s enumeration count but nothing enforced it: fewer passes degrade to an unexplained coordination timeout, and an added pass before `Apply` would move the park past the state and ledger writes while the test still reported green. Exposed `EnumerationCount` and pinned it against `BlockOnEnumeration` after the seed completes, so a `Seed` refactor fails on a named expectation.
  - `[low]` `[patch]` The class `<remarks>` called all four gate-protected members "maps" even though `_eventsAppended` is an `int`; reworded so the three dictionaries and the plain counter field are described separately.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release` -- expected: the focused repository concurrency coverage and existing unit suite pass.
- `dotnet build Hexalith.Folders.slnx --configuration Release --no-restore` -- expected: zero warnings and zero errors.

**Results:**
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release` -- failed during restore with `NU1107`: `xunit.v3 4.0.0` requires `xunit.v3.common (= 4.0.0)`, while `xunit.v3.extensibility.core 3.2.2` requires `xunit.v3.common (= 3.2.2)`.
- `dotnet restore tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -p:Configuration=Release -p:CentralPackageTransitivePinningEnabled=false --force` -- passed and generated compatible Release assets for the focused test project.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release --no-restore -p:CentralPackageTransitivePinningEnabled=false -m:1` -- passed with zero warnings and zero errors.
- `dotnet tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Aggregates.Folder.InMemoryFolderRepositoryConcurrencyTests` -- passed 4 of 4 tests with no skips.
- `dotnet tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Queries.BranchRefPolicyReadModelTests` -- passed 2 of 2 tests with no skips.
- `dotnet tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests.dll` -- passed 1687 of 1687 tests with no skips.
- `dotnet restore Hexalith.Folders.slnx -p:Configuration=Release -p:CentralPackageTransitivePinningEnabled=false -m:1 --force` -- passed and generated compatible Release assets for all 54 solution projects.
- `dotnet build Hexalith.Folders.slnx --configuration Release --no-restore` -- passed unchanged with zero warnings and zero errors.

**Results — review-pass patch re-run (2026-08-28):**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release --no-restore -p:CentralPackageTransitivePinningEnabled=false -m:1` -- passed with zero warnings and zero errors.
- `dotnet tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests.dll` -- passed 1687 of 1687 tests with no skips.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug -p:CentralPackageTransitivePinningEnabled=false -m:1` then `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll` -- passed 1687 of 1687 tests with no skips. Run specifically because `Debug.Assert` is compiled out of Release: this is the configuration in which the two new gate assertions are live, and no assertion fired on any exercised path.
- `dotnet restore Hexalith.Folders.slnx -p:Configuration=Release -p:CentralPackageTransitivePinningEnabled=false -m:1 --force` then `dotnet build Hexalith.Folders.slnx --configuration Release --no-restore` -- passed with zero warnings and zero errors. The restore step remains mandatory: running the solution build against assets left by a focused-project restore fails with 28 pre-existing `CS0234` errors in `Hexalith.Folders.UI` (`Hexalith.FrontComposer.Shell`), unrelated to this change.

**Results — second review-pass patch re-run (2026-08-28):**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release --no-restore -p:CentralPackageTransitivePinningEnabled=false -m:1` -- passed with zero warnings and zero errors.
- `dotnet tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests.dll` -- passed 1687 of 1687 tests with no skips.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug -p:CentralPackageTransitivePinningEnabled=false -m:1` then `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll` -- passed 1687 of 1687 tests with no skips. Re-run because this is the configuration in which the two `Debug.Assert` gate guards are live; no assertion fired.
- `dotnet restore Hexalith.Folders.slnx -p:Configuration=Release -p:CentralPackageTransitivePinningEnabled=false -m:1 --force` then `dotnet build Hexalith.Folders.slnx --configuration Release --no-restore` -- passed with zero warnings and zero errors.

## Auto Run Result

Status: done

**Implemented change.** `InMemoryFolderRepository` uses its existing `_gate` as the single synchronization primitive for all state it owns. The three `ConcurrentDictionary` stores are plain ordinal `Dictionary` instances; `Load`, `TryGetIdempotencyFingerprint`, the `EventsAppended` test affordance, and `ResetAppendCounters` -- previously lock-free -- take the gate, and the append counter lives in a gate-protected `_eventsAppended` backing field. Six `_lastObservedAt.AddOrUpdate(...)` call sites collapsed into one gate-local monotonic clamp helper. `IFolderRepository`, event/fingerprint semantics, ordinal key comparison, tenant-scoped stream and ledger keys, duplicate-seed failure, and the match/conflict outcome table are unchanged. Four focused concurrency tests cover the intent's I/O matrix, and the branch-policy read-model test carries a backward-clock regression.

This second follow-up review pass changed no behavior. It closed two silent-degradation paths in the new test coverage (an unasserted append outcome, and the unenforced enumeration-count coupling in the seed coordination harness) and corrected one wording error in the class remarks.

**Files changed:**
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs` -- single-gate synchronization for state, fingerprints, observation watermarks, and the append counter; class-level invariant documentation, `Debug.Assert(Monitor.IsEntered(_gate))` guards, and the `ClampObservedAtUnderGate` helper. This pass corrected the remarks' "maps" wording.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/InMemoryFolderRepositoryConcurrencyTests.cs` -- equivalent-append, conflicting-append, seed/read publication, and duplicate-seed race coverage, all waits bounded. This pass added the enumeration-count pin to the seed/read test.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/CoordinatedSeedEventList.cs` -- test-only event list that parks the seeding writer inside the gate. This pass exposed `EnumerationCount` and made `BlockOnEnumeration` assertable.
- `tests/Hexalith.Folders.Tests/Queries/BranchRefPolicyReadModelTests.cs` -- backward-clock regression proving observable freshness never decreases. This pass pinned both appends to `FolderAppendOutcome.Appended`.

**Review findings breakdown:** 3 patches applied (all low); 1 item deferred (low: the sibling `InMemoryOrganizationProviderBindingRepository`'s "mirrors the folder repository policy" comment, which this change made false and which the intent's Never clause forbids editing here); 25 items rejected (4 medium, 21 low); 0 intent gaps; 0 spec defects.

**Follow-up review recommendation:** `false`. Patched findings this pass: high 0, medium 0, low 3; score = (3 x 0) + (1 x 3) = 3, which is below 5. This pass surfaced no defect in the change itself -- only hardening of its own regression pins.

**Verification performed:** see the Verification section above. Release focused-suite build 0 warnings / 0 errors and 1687 of 1687 tests passing; Debug build and run of the same suite (the configuration in which the `Debug.Assert` gate guards are live) 1687 of 1687 passing with no assertion fired; Release solution build 0 warnings / 0 errors after the documented `CentralPackageTransitivePinningEnabled=false --force` restore.

**Residual risks:**
- The declared `dotnet test ... --configuration Release` command still fails at restore with the pre-existing `NU1107` conflict (`xunit.v3` 4.0.0 vs `xunit.v3.extensibility.core` 3.2.2). All green evidence comes from the documented workaround restore plus direct `dotnet <test dll>` invocation, not the project's default `dotnet test` path. Nothing in this change touches package versions.
- The two gate assertions are `Debug.Assert`, so they are compiled out of Release. The baseline CI lane builds and runs this suite in Debug, so they are on the normal verification path, but an un-gated access introduced and only ever exercised in Release would go undetected.
- Because `Load` and `TryGetIdempotencyFingerprint` now share the write gate, a caller-supplied event enumerator or an injected read model that blocks inside the critical section blocks readers of every stream. This is the intent's chosen model rather than a defect, and `CoordinatedSeedEventList` exercises it deliberately; real callers pass materialized lists.
- Three of the four new concurrency tests characterize write-path behavior that was already serialized before this change. They are correct and useful as regression pins, but only the seed/read test covers the read paths this change actually moved.
- The seed/read test's `loadCompletedBeforeRelease` / `lookupCompletedBeforeRelease` assertions are negative timing observations over a 250 ms window. A reader descheduled between signalling that it started and reaching the gate satisfies them without proving anything, so on a heavily loaded machine the test can pass vacuously. It cannot produce a false failure, and the post-release assertions still pin the published state/fingerprint pair.
