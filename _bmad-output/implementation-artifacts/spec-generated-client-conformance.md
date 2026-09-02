---
title: 'Harden generated-client conformance assertions'
type: 'refactor'
created: '2026-09-02'
status: 'done'
baseline_revision: '9026f97cd4fcefa46d0f4d54aecdbe0541c87ccc'
review_loop_iteration: 0
followup_review_recommended: false
context: []
warnings: []
deferred:
  - summary: >-
      Pre-existing Forgejo provider syntax errors block the normal Client.Tests project build.
    evidence: |-
      `dotnet build tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` fails before compiling the changed tests with CS1513 at ForgejoProvider.cs:808 and CS1519 at ForgejoProvider.cs:821. A source-isolated client conformance project builds and runs the changed tests successfully.
    location: >-
      src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808
    severity: high
---

<intent-contract>

## Intent

**Problem:** The archive and lifecycle generated-client conformance tests encode NSwag's current parameter ordering and identifier mangling, so a behavior-preserving generator upgrade can fail tests even when the required operation contract remains intact.

**Approach:** Assert each generated operation by stable method name, exact return type, and an order-independent multiset of required parameter types. Continue proving that every expected parameter is required while ignoring generated parameter names and positions.

## Boundaries & Constraints

**Always:** Preserve coverage of `ArchiveFolderAsync` returning `Task<AcceptedCommand>` with four string inputs, `ArchiveFolderRequest`, and `CancellationToken`; preserve coverage of `GetFolderLifecycleStatusAsync` returning `Task<FolderLifecycleStatus>` with two string inputs, nullable `ReadConsistencyClass`, and `CancellationToken`; require all parameters on the matched overloads to remain non-optional; retain the existing archive-reason and lifecycle serialization tests.

**Block If:** Either operation has no overload whose semantic parameter-type multiset matches the contract, or the only match changes return type or requiredness; those are contract changes rather than incidental NSwag output changes.

**Never:** Assert generated parameter names or positions; edit generated files, the OpenAPI Contract Spine, NSwag configuration/generation code, unrelated parity tests, or the deferred-work ledger; weaken parameter cardinality/type coverage to a presence-only check.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Current generated overload | NSwag emits the present names and order | Conformance passes with the exact return type and required parameter-type multiset | No error expected |
| Incidental generator reshaping | Parameter names are remangled or parameter order changes, but types, cardinality, requiredness, and return type are preserved | Conformance still passes | No error expected |
| Semantic contract drift | A parameter is missing, added, optional, or has a different type, or the return type changes | Conformance fails with the affected operation test | Test failure identifies contract drift |

</intent-contract>

## Code Map

- `tests/Hexalith.Folders.Client.Tests/ArchiveFolderClientConformanceTests.cs:11` -- archive operation reflection assertion currently selects by six-parameter count and compares the exact ordered NSwag names; keep the archive-reason serialization theory unchanged.
- `tests/Hexalith.Folders.Client.Tests/LifecycleStatusClientConformanceTests.cs:12` -- lifecycle operation reflection assertion currently selects by four-parameter count and compares exact mangled names; keep both DTO serialization tests unchanged.
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:65` -- read-only evidence for the lifecycle overload pair and its current return/parameter types; generated output must not be edited.
- `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:87` -- read-only evidence for the archive overload pair and its current return/parameter types; generated output must not be edited.
- `tests/Hexalith.Folders.Client.Tests/TransportParityConformanceTests.cs:287` -- read-only reuse precedent for identifying the fuller generated overload through `CancellationToken` semantics without positional assumptions.
- `tests/Hexalith.Folders.Client.Tests/GeneratedClientMethodConformance.cs` -- shared semantic-signature matcher that ignores names and order while enforcing return type, type multiplicity, cardinality, and requiredness.
- `tests/Hexalith.Folders.Client.Tests/GeneratedClientMethodConformanceTests.cs` -- matrix coverage proving remangled/reordered parameters pass and missing, added, optional, mistyped, or return-type drift fails.
- `tests/tools/run-contract-parity-ci-gates.ps1:63` -- the `sdk-transport-parity` gate already owns both target conformance classes; no gate wiring change is needed.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.Client.Tests/ArchiveFolderClientConformanceTests.cs` -- replace ordered parameter-name equality with semantic overload discovery by an order-independent parameter-type multiset, then assert the exact return type and that every matched parameter is required.
- [x] `tests/Hexalith.Folders.Client.Tests/LifecycleStatusClientConformanceTests.cs` -- apply the same semantic overload assertion for lifecycle status without depending on NSwag's header-name mangling or parameter order.
- [x] `tests/Hexalith.Folders.Client.Tests/GeneratedClientMethodConformance.cs` and `tests/Hexalith.Folders.Client.Tests/GeneratedClientMethodConformanceTests.cs` -- centralize semantic matching and cover every I/O matrix row with focused positive and negative tests.

**Acceptance Criteria:**
- Given a generated `IClient` whose archive overload preserves the required type/cardinality/requiredness contract, when archive conformance runs, then it passes regardless of parameter names or positions and still rejects missing, added, optional, or mistyped parameters and a changed return type.
- Given a generated `IClient` whose lifecycle-status overload preserves the required type/cardinality/requiredness contract, when lifecycle conformance runs, then it passes regardless of parameter names or positions and still rejects missing, added, optional, or mistyped parameters and a changed return type.
- Given the existing archive-reason and lifecycle DTO serialization cases, when the two conformance classes run, then their wire-shape coverage remains green and unchanged.
- Given the completed implementation, when repository changes are inspected, then neither generated artifacts nor `_bmad-output/implementation-artifacts/deferred-work.md` is modified.

## Spec Change Log

## Review Triage Log

### 2026-09-02 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 3, low 2)
- defer: 1: (high 1, medium 0, low 0)
- reject: 13: (high 1, medium 9, low 3)
- addressed_findings:
  - `[medium]` `[patch]` Required semantic signatures now reject static methods that cannot be called through an `IClient` instance.
  - `[medium]` `[patch]` Required semantic signatures now reject methods with unresolved generic parameters.
  - `[medium]` `[patch]` A one-optional-parameter negative fixture now detects requiredness quantifier regressions.
  - `[low]` `[patch]` Lifecycle-shaped remangling and reordering now has direct positive matcher coverage.
  - `[low]` `[patch]` Every untracked reviewed file was checked explicitly with `git diff --no-index --check`; empty diagnostic output with the expected diff exit code confirmed no whitespace errors.

### 2026-09-02 — Review pass (follow-up)
- intent_gap: 0
- bad_spec: 0
- patch: 6: (high 0, medium 2, low 4)
- defer: 1: (high 1, medium 0, low 0)
- reject: 18: (high 1, medium 9, low 8)
- addressed_findings:
  - `[medium]` `[patch]` The required parameter-type multisets were duplicated across three sites, so the matcher matrix could keep validating a stale contract shape after an edit to either production test. Both contracts now live once on `GeneratedClientMethodConformance` and are consumed by all four call sites.
  - `[medium]` `[patch]` A drift failure reported only a fixed sentence, not what was generated. The production tests now assert candidate presence separately and render the observed overload signatures through `GeneratedClientMethodConformance.Describe`/`DescribeOverloads`; mutation-verified against an intentionally wrong expected multiset.
  - `[low]` `[patch]` `ShouldAllBe` collapsed eight drift fixtures into one opaque failure; converted to a `[Theory]` with one named case per fixture.
  - `[low]` `[patch]` No fixture covered multiplicity-only drift — same arity and same distinct parameter-type set, different counts — which is the single shape that only multiset counting rejects. Added `ArchiveWithRedistributedParameterMultiplicitiesAsync`.
  - `[low]` `[patch]` `GeneratedClientExposesArchiveFolderOperationWithRequiredHeaders` no longer verified header identity; renamed to `...WithRequiredSignature` and both classes now document that header identity stays covered by the oracle-driven `TransportParityConformanceTests`.
  - `[low]` `[patch]` Recorded in the matcher's `<remarks>` that reference-type nullability is not part of runtime `Type` identity (`string` and `string?` compare equal) while nullable value types are compared exactly.

### 2026-09-02 — Review pass (second follow-up)
- intent_gap: 0
- bad_spec: 0
- patch: 2: (high 0, medium 1, low 1)
- defer: 1: (high 1, medium 0, low 0)
- reject: 23: (high 2, medium 12, low 9)
- addressed_findings:
  - `[medium]` `[patch]` The lifecycle contract had no negative coverage at all, and the matcher's `<remarks>` documented an invariant no test pinned: nullable value types are compared exactly while reference-type nullability is erased. A matcher weakened to unwrap `Nullable<T>` passed the entire suite. Added `RequiredSignatureMatcherRejectsLifecycleContractDrift` (non-nullable `ReadConsistencyClass`, missing parameter, changed return type) plus the positive `RequiredSignatureMatcherIgnoresReferenceTypeNullabilityAnnotations`; mutation-verified — the `Nullable<T>`-unwrapping mutant now fails on the non-nullable fixture.
  - `[low]` `[patch]` `FormatType` sliced `Type.Name` at the arity backtick behind an `IsGenericType` guard only. A non-generic type nested in a generic one reports `IsGenericType == true` with no backtick, so the slice threw `ArgumentOutOfRangeException` from inside the failure-diagnostic path, replacing a conformance failure with an unrelated crash. Guarded on the backtick index and pinned by `SignatureDescriptionRendersNonGenericTypesNestedInGenericTypes` (`List<int>.Enumerator`); mutation-verified against the pre-patch expression.

## Design Notes

Compare parameter `Type` values as a multiset rather than a set so repeated string inputs remain counted. A matching candidate must also be an instance, closed, non-optional method with the exact return type; unrelated overload additions remain tolerated.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` -- expected: the focused test project and generated client build with warnings as errors.
- `tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests -noLogo -noColor -class Hexalith.Folders.Client.Tests.ArchiveFolderClientConformanceTests -class Hexalith.Folders.Client.Tests.LifecycleStatusClientConformanceTests -class Hexalith.Folders.Client.Tests.GeneratedClientMethodConformanceTests` -- expected: both target conformance classes and semantic matcher matrix coverage pass under the xUnit v3 in-process runner.
- `git diff --check && git status --short` -- expected: no whitespace errors; only the spec and generated-client conformance test support files are changed.
## Auto Run Result

Status: done

### Summary

Second follow-up review pass over the hardened archive/lifecycle generated-client conformance assertions. The semantic contract (instance, closed, exact return type, exact required parameter-type multiset, every parameter non-optional) is unchanged. This pass closed the two gaps the reviewers confirmed in the code the story authored: the lifecycle contract had no negative coverage and the matcher's documented nullable-type rule was untested, and the failure-diagnostic renderer could crash on a non-generic type nested in a generic one.

### Files Changed

- `tests/Hexalith.Folders.Client.Tests/GeneratedClientMethodConformance.cs` -- `FormatType` now locates the arity backtick before slicing, so a non-generic type nested in a generic type renders instead of throwing.
- `tests/Hexalith.Folders.Client.Tests/GeneratedClientMethodConformanceTests.cs` -- adds a lifecycle drift `[Theory]` (non-nullable `ReadConsistencyClass`, missing parameter, changed return type), the positive reference-type-nullability fixture, and the nested-in-generic rendering test.
- `tests/Hexalith.Folders.Client.Tests/ArchiveFolderClientConformanceTests.cs`, `tests/Hexalith.Folders.Client.Tests/LifecycleStatusClientConformanceTests.cs` -- unchanged this pass.

Generated artifacts, the OpenAPI Contract Spine, NSwag configuration, `TransportParityConformanceTests`, and `_bmad-output/implementation-artifacts/deferred-work.md` were not modified by this pass. The ledger's `DW-336` row and its working-tree state are orchestrator-owned bookkeeping and were left untouched.

### Review Findings

- Patches applied: 2 (high 0, medium 1, low 1).
- Items deferred: 1 -- the pre-existing `ForgejoProvider.cs` CS1513/CS1519 break, already carried in this spec's `deferred` frontmatter and already ingested by the orchestrator as `DW-336`; re-confirmed this pass (`git diff --stat main HEAD -- src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs` is empty; last touched by `9ac1e6e`) and not duplicated.
- Items rejected: 23 (high 2, medium 12, low 9). Chiefly: restoring parameter-name/position assertions or editing `TransportParityConformanceTests` (both forbidden by the intent's Never clause, and the intent auditor itself concluded the residual gate brittleness is a gap in the intent, not the implementation); the `.Single` -> `.Any` uniqueness relaxation and same-typed-parameter blindness (both are the ratified approach -- "four string inputs", extra overloads tolerated, since NSwag emits a token-less sibling for every operation); and several claims verified false in-tree -- `GeneratedClientMethodConformanceTests` does run in CI via the unfiltered `Hexalith.Folders.Client.Tests` entry in `run-baseline-ci-gates.ps1`, `ClientGenerationTests.cs:87` derives its expected parameter names from the spine rather than pinning NSwag mangling, the new files match the compact using-block style of their own pre-existing siblings, and `IClient` derives from no base interface.

Follow-up review recommendation: `false`; patched findings were high 0, medium 1, low 1, for score `3 x 1 + 1 x 1 = 4`, below the threshold of 5.

### Verification

- `Hexalith.Folders.Client.Tests` still cannot build in place: `ForgejoProvider.cs(808,11) CS1513` and `(821,5) CS1519`, reached through `Client.Tests -> Hexalith.Folders.Testing -> Hexalith.Folders`. Confirmed pre-existing and identical to `main`.
- Source-isolated harness (scratchpad project referencing the live `src/Hexalith.Folders.Client` and compiling the four conformance sources at their real repository paths, so the root `.editorconfig` applies): build succeeded, 0 warnings, 0 errors with `TreatWarningsAsErrors=true` under the repository's own analyzer configuration. Note: an earlier harness attempt that added `AnalysisMode=All` reported CA1068 on the deliberately reordered fixtures; `Hexalith.Folders` does not set `AnalysisMode`, so that was a harness artifact, not a repository build break.
- xUnit v3 in-process run of the isolated assembly: Total 26, Errors 0, Failed 0, Skipped 0, Not Run 0 (was 21 before the first follow-up, 26 now).
- Mutation check 1: reverting the `FormatType` guard makes `SignatureDescriptionRendersNonGenericTypesNestedInGenericTypes` fail with `System.ArgumentOutOfRangeException : length ('-1') must be a non-negative value`.
- Mutation check 2: weakening the matcher to unwrap `Nullable<T>` on both sides of the multiset comparison makes `RequiredSignatureMatcherRejectsLifecycleContractDrift(fixtureName: "LifecycleWithNonNullableConsistencyClassAsync")` fail. Both mutants reverted and re-verified green.
- `dotnet format whitespace --verify-no-changes` over each of the four changed sources and `dotnet format analyzers --verify-no-changes` over the harness project: all exit 0.
- `git status --short`: only the four test sources and this spec are modified; `src/Hexalith.Folders.Client/Generated/` is unchanged after NSwag re-ran during the build.

### Residual Risks

- All verification for this bundle remains source-isolated. No repository-owned lane can execute these tests until the pre-existing `ForgejoProvider.cs` syntax errors are fixed; until then generated-client contract drift surfaces as a build failure rather than a conformance failure. That break also reddens every CI job that builds `Hexalith.Folders.slnx`, so it is a branch-wide blocker, not a quirk of this lane.
- Matrix row 2 (generator reshaping still passes) is proven against synthesized fixtures, not against a real NSwag regeneration; the actual generated client is only observed in its current shape.
- Header *identity* is not pinned by these two classes by design; it depends on `TransportParityConformanceTests` and the parity oracle rows remaining in the same gate. That file identifies headers by NSwag's mangled parameter names, so a remangling generator upgrade would still redden `sdk-transport-parity` there -- out of scope for this story by the intent's Never clause.
- The matcher accepts any one conforming overload among same-named overloads. A generator that added a second, differently shaped overload alongside the conforming one would not be flagged here.
