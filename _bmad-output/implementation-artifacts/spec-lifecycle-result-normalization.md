---
title: 'Normalize lifecycle result outcomes and freshness'
type: 'refactor'
created: '2026-09-02'
baseline_revision: '9876c40a347dd41c5dc49ad031693b429909bd19'
baseline_commit: '9876c40a347dd41c5dc49ad031693b429909bd19'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/2-7-inspect-folder-lifecycle-and-binding-status.md'
warnings: []
deferred:
  - summary: >-
      Non-available lifecycle read-model statuses can return a future observation time without compatibility validation.
    evidence: |-
      ValidateSnapshotCompatibility rejects future ObservedAt values only for an Available result with a snapshot. Stale, Unavailable, Malformed, and NotFound outer statuses return their freshness without the same temporal check; this behavior predates the normalization change and requires a separate contract decision.
    location: >-
      src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:109
    severity: medium
  - summary: >-
      Sibling query handlers still duplicate the unavailable-freshness idiom and the authorization-outcome string constants that this spec normalized for the lifecycle handler.
    evidence: |-
      `WorkspaceStatusQueryHandler`, `WorkspaceLockStatusQueryHandler`, `WorkspaceCleanupStatusQueryHandler`, `BranchRefPolicyQueryHandler`, and `TaskStatusQueryHandler` each declare their own `allowed`/`denied_safe` constants and repeat `Freshness with { Stale = true, ReasonCode = ... ?? ... }` over the same `FolderLifecycleFreshness` record, and they still return a `ProjectionWatermark` on unavailable results. The canonical helpers introduced here are `internal` to the same assembly, so those sites could adopt them, but this spec's intent is scoped to the lifecycle handler.
    location: >-
      src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQueryHandler.cs:96-105,220
    severity: medium
---

<intent-contract>

## Intent

**Problem:** Lifecycle query branches construct authorization outcomes and unavailable freshness independently, allowing outcome tokens, reason-code precedence, and projection-watermark exposure to drift. The binding-reference emptiness predicate also duplicates the shared value predicate.

**Approach:** Represent lifecycle authorization outcomes with one typed model and route unavailable freshness through canonical helpers. Preserve specific source reasons for generic status fallbacks, but let reasons determined by handler validation win; unavailable results must suppress projection watermarks.

## Boundaries & Constraints

**Always:** Preserve allowed lifecycle/binding values, result-code and HTTP mappings, authorization-before-observation, metadata-only diagnostics, and allowed-result freshness evidence. Use ordinal semantics and keep each C# type in its own file. Treat an explicitly determined handler reason as more specific than an inherited read-model reason; otherwise preserve a nonblank inherited reason before using a generic fallback. Mark unavailable freshness stale and set its projection watermark to null.

**Block If:** The implementation requires changing the OpenAPI Contract Spine, generated clients, public endpoint shapes, or a decision about a new externally visible lifecycle token.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md`, generated client files, unrelated lifecycle vocabulary, authorization order, provider/repository/filesystem behavior, or sibling submodules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Allowed status | Compatible active or archived snapshot | Typed allowed outcome; lifecycle data and trustworthy watermark are preserved | No error expected |
| Safe denial | Authentication, authorization, or safe-not-found result | Typed denied-safe outcome with no protected data or projection watermark | Existing result code remains unchanged |
| Generic source failure | Stale/unavailable status with a specific source reason | Existing specific reason is retained; freshness is stale and watermark is null | Generic reason is only a fallback |
| Handler-detected failure | Mismatch, malformed state, or unknown state with a pre-existing source reason | Handler-determined reason wins; freshness is stale and watermark is null | Existing fail-closed result code remains unchanged |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs` -- `Success`, `SafeResult`, `Unavailable`, status switches, and `HasNoBindingReferences` are the duplicate construction sites; retain authorization gates and lifecycle mappings.
- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleFreshness.cs` -- canonical home for stale/unavailable construction and the watermark-suppression invariant.
- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryResult.cs` -- public packable compatibility surface; keep positional `AuthorizationOutcome` as `string` and preserve its exact tokens.
- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleAuthorizationOutcome.cs` and `FolderLifecycleAuthorizationOutcomeExtensions.cs` -- new internal typed model plus the single fail-closed mapping to `allowed` and `denied_safe`.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusProjectionTests.cs` and `FolderLifecycleFreshnessTests.cs` -- focused handler and helper assertions for tokens, precedence, watermarks, result codes, safe-not-found, invalid arguments, and malformed bindings.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusAuthorizationGateTests.cs` and `ThrowingLifecycleStatusReadModel.cs` -- no-touch denial and read-model-exception coverage proving typed denied-safe outcomes remain fail-closed.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs` -- read-only evidence: lifecycle HTTP responses switch on `Code` and do not serialize `AuthorizationOutcome`; no endpoint change is expected.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders/Queries/Folders/FolderLifecycleAuthorizationOutcome.cs` and `FolderLifecycleAuthorizationOutcomeExtensions.cs` -- introduce an internal typed outcome and one fail-closed enum-to-token mapping; `DeniedSafe = 0`, unknown enum values map to `denied_safe`, and canonical tokens stay exact.
- [x] `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryResult.cs` and `FolderLifecycleStatusQueryHandler.cs` -- preserve the public positional string property while constructing it only through the typed mapping; do not change its public signature or endpoint JSON.
- [x] `src/Hexalith.Folders/Queries/Folders/FolderLifecycleFreshness.cs` -- add internal canonical unavailable transformations with explicit fallback-versus-handler reason precedence and unconditional watermark suppression while preserving read consistency and observation time.
- [x] `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs` -- route fail-closed freshness through those transformations, make `safe_not_found` deterministic for both not-found branches, and implement binding-reference emptiness through `HasValue`.
- [x] `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusProjectionTests.cs`, `FolderLifecycleStatusAuthorizationGateTests.cs`, and `FolderLifecycleFreshnessTests.cs` -- cover canonical string tokens and typed mapping, both precedence paths (including projection-unavailable and archive-unsupported with distinct inherited reasons), allowed watermark retention, exact result codes and watermark removal for every changed result family including read-model not-found, helper invariants/argument guards, and both one-sided malformed-binding combinations.

**Acceptance Criteria:**
- Given a compatible allowed snapshot, when lifecycle status is computed, then its typed outcome is allowed and its original projection watermark remains available.
- Given an existing package consumer reads or constructs `FolderLifecycleStatusQueryResult`, when this refactor is applied, then `AuthorizationOutcome` remains a positional `string` and handler results retain the exact `allowed` and `denied_safe` tokens.
- Given any safe denial or fail-closed unavailable result, when the result is returned, then its typed outcome is denied-safe and its projection watermark is null.
- Given a generic stale/unavailable status with an existing reason, when freshness is normalized, then that reason wins over the generic fallback.
- Given a handler-detected mismatch or malformed/unknown state with an inherited reason, when freshness is normalized, then the handler reason wins and the existing result code is preserved.
- Given unbound binding metadata, when emptiness is evaluated, then both references use the shared `HasValue` predicate and existing valid/malformed behavior is unchanged.

## Spec Change Log

- 2026-09-02 — Review pass 1: The first derivation changed the public positional `AuthorizationOutcome` property from `string` to an enum, creating a packable source/binary break and losing the canonical lowercase token shape. The Code Map, tasks, and acceptance were amended to preserve the string API while using an internal enum and one fail-closed token mapper. This avoids the known-bad public API break. KEEP: the two explicit freshness-precedence paths, unconditional unavailable watermark suppression, `DeniedSafe = 0`, shared `HasValue` binding checks, and focused lifecycle matrix coverage must survive re-derivation.

## Review Triage Log

### 2026-09-02 — Review pass
- intent_gap: 0
- bad_spec: 1: (high 1, medium 0, low 0)
- patch: 6: (high 0, medium 3, low 3)
- defer: 1: (high 1, medium 0, low 0)
- reject: 6: (high 1, medium 3, low 2)
- addressed_findings:
  - `[high]` `[bad_spec]` Preserve the public positional string outcome and canonical lowercase tokens while moving branch construction behind an internal typed mapping; avoid a source/binary package break during re-derivation.

### 2026-09-02 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 0, medium 7, low 3)
- defer: 1: (high 0, medium 1, low 0)
- reject: 9: (high 0, medium 4, low 5)
- addressed_findings:
  - `[medium]` `[patch]` Restore the existing public `SafeUnavailable` argument behavior while retaining validation on the new internal reason transformations.
  - `[low]` `[patch]` Route final fail-closed watermark and stale normalization through the canonical `ToUnavailable` helper instead of duplicating record mutation in `SafeResult`.
  - `[low]` `[patch]` Make the compatibility test compare the positional parameter name with exact ordinal semantics.
  - `[low]` `[patch]` Assert that `AuthorizationOutcome` remains constructor parameter index 6 as well as type `string`.
  - `[medium]` `[patch]` Expand verification selection from `FolderLifecycleStatus*` to `FolderLifecycle*` so the new freshness and enum tests are included.
  - `[medium]` `[patch]` Make direct xUnit v3 execution the documented verification path and retain legacy `dotnet test` only as a recorded tooling diagnostic.
  - `[medium]` `[patch]` Exercise an Available snapshot whose embedded freshness is stale, covering the compatibility-validation branch and source-reason fallback.
  - `[medium]` `[patch]` Seed a handler mismatch with a distinct inherited reason and prove the handler-determined reason wins.
  - `[medium]` `[patch]` Cover missing-authentication and throwing-read-model `SafeResult` entry paths for exact denied-safe tokens, reasons, and watermark suppression.
  - `[medium]` `[patch]` Prove handler-level unavailable normalization preserves distinctive read consistency and observation time.

### 2026-09-02 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 1, low 2)
- defer: 1: (high 0, medium 1, low 0)
- reject: 21: (high 0, medium 6, low 15)
- addressed_findings:
  - `[medium]` `[patch]` Every branch-level fallback reason token (`projection_stale`, `projection_unavailable`, `lifecycle_stale`, `lifecycle_unavailable`) was unobserved: each test reaching those branches seeded a non-blank inherited reason, so the fallback arguments could be swapped between branches without failing anything. Added blank-inherited-reason theories for the read-model statuses, the lifecycle states, and the compatibility-validation stale branch; mutation-checked by swapping `lifecycle_stale` for `lifecycle_unavailable` (1 failure, restored).
  - `[low]` `[patch]` `HasNoBindingReferences` was rewritten onto the shared `HasValue` predicate, whose only distinguishing behavior is whitespace handling, yet no test used a whitespace-only binding reference. Added a theory proving whitespace-only repository/provider references count as absent and still yield the allowed `ready` result.
  - `[low]` `[patch]` Nothing pinned the closed member set behind the fail-closed `ToToken` mapping, so a third enum member would silently map to `denied_safe`. Added an `Enum.GetValues` arity assertion alongside the existing token and unknown-value assertions.

### 2026-09-02 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 3: (high 0, medium 1, low 2)
- defer: 0
- reject: 19: (high 0, medium 7, low 12)
- addressed_findings:
  - `[medium]` `[patch]` The three binding-branch reason tokens this change rerouted through `ToUnavailableForHandler` -- `binding_metadata_malformed`, `binding_state_unsupported`, and `archived_binding_state_unsupported` -- had no test at all, so the branches that newly override an inherited source reason and suppress the watermark were unobserved. Added `BindingBranchFailuresOverrideSourceReasonAndSuppressWatermark`, a six-row theory over the Active and Archived binding switches seeded with a distinct inherited reason; mutation-checked by swapping `binding_metadata_malformed` for `binding_state_unsupported` (4 failures, restored).
  - `[low]` `[patch]` `SuccessWithBinding` still compared the same two binding references with raw `string.IsNullOrWhiteSpace` while `HasNoBindingReferences` had been moved onto the shared `HasValue` predicate, leaving the duplication the intent names alive at the sibling site in the same file. Routed both checks through `HasValue`; the new theory's whitespace-only rows pin the unchanged malformed behavior.
  - `[low]` `[patch]` `PublicAuthorizationOutcomeShouldRemainAPositionalString` hard-pinned constructor parameter index 6 with nothing recording why the ordinal is load-bearing, so a future position change could not be judged a real break. Added a comment stating the positional-record source/binary compatibility contract it guards.

## Design Notes

Unavailable freshness has two intentional reason paths: a generic fallback preserves an already-specific source reason, while a handler-determined failure replaces it. Both paths set `Stale = true` and `ProjectionWatermark = null`; only allowed results retain a watermark.

## Verification

**Commands:**
- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore` -- expected: core project builds with warnings as errors.
- `tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests -class 'Hexalith.Folders.Tests.Queries.Folders.FolderLifecycle*'` -- direct xUnit v3 execution after a successful Release test-project build; expected: all focused lifecycle status and freshness tests execute and pass.
- `git diff --check` -- expected: no whitespace errors.
- `git diff --exit-code -- _bmad-output/implementation-artifacts/deferred-work.md` -- expected: no ledger changes.

**Results (2026-09-02):**
- Diagnostic `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --filter FolderLifecycle` was blocked before execution by the known .NET 10 Microsoft.Testing.Platform/VSTest incompatibility, so it is not the verification path for this xUnit v3 project.
- The prescribed core build was blocked by baseline syntax errors in `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808` and `:821`; its SHA-256 matches `HEAD` and it was not modified by this spec.
- Isolated Release validation substituted only that baseline brace defect outside the worktree; the core project then built with zero warnings and zero errors.
- For isolated test validation, a transient Release test project uses `AssemblyName=Hexalith.Folders.Tests`, disables default compile items, links `FolderLifecycleFreshnessTests.cs`, `FolderLifecycleStatusProjectionTests.cs`, `FolderLifecycleStatusAuthorizationGateTests.cs`, `FolderLifecycleStatusNoFallbackTests.cs`, `FolderLifecycleStatusTestSupport.cs`, and `ThrowingLifecycleStatusReadModel.cs`, and references only `Hexalith.Folders.csproj`; its unfiltered xUnit executable therefore includes the freshness helper tests rather than relying on the narrower `FolderLifecycleStatus*` class filter.
- The normal Release test-project build remains blocked by the same baseline Forgejo syntax errors and unrelated existing UI/FrontComposer compilation failures; the transient focused project substitutes only the Forgejo brace defect outside the worktree and does not compile the unrelated UI project.
- Before this review-fix pass, that lifecycle-only Release test assembly built successfully and direct unfiltered xUnit v3 execution passed all 55 tests with zero failures, skips, errors, or tests not run. All four I/O matrix rows ran in this focused assembly.
- After the second-review patches, the isolated core Release build passed with zero warnings and zero errors, the transient lifecycle-only Release test project built with zero warnings and zero errors, and its direct unfiltered xUnit v3 executable passed all 59 tests with zero failures, skips, errors, or tests not run.
- `git diff --check` passed.
- The deferred-work ledger diff check passed.

**Results (2026-09-02, follow-up review pass):**
- The prescribed `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj` remains blocked in the worktree by the pre-existing `ForgejoProvider.cs` brace defect (ledger `DW-336`); the file is untouched by this spec and its SHA-256 still matches `HEAD`.
- Isolated Release validation again substituted only that baseline defect (a missing `}` closing `IsReplayEvidenceWellFormed` at line 808, plus removal of the stray `}` at line 821); the core project then built with zero warnings and zero errors. `ForgejoProvider.cs` was restored byte-for-byte afterwards (SHA-256 `744b8f6e90de751113782b6fb2b5166f2e4fc4fc3b1ce913dfec3ca45838e663`, matching `HEAD`).
- The transient lifecycle-only Release test project (`AssemblyName=Hexalith.Folders.Tests`, default compile items disabled, linking `FolderLifecycleFreshnessTests.cs`, `FolderLifecycleStatusProjectionTests.cs`, `FolderLifecycleStatusAuthorizationGateTests.cs`, `FolderLifecycleStatusNoFallbackTests.cs`, `FolderLifecycleStatusMetadataLeakageTests.cs`, `FolderLifecycleStatusTestSupport.cs`, and `ThrowingLifecycleStatusReadModel.cs`, referencing only `Hexalith.Folders.csproj`) built with zero warnings and zero errors; its direct unfiltered xUnit v3 executable passed all 78 tests with zero failures, skips, errors, or tests not run. The metadata-leakage class was added to this pass's link set so the metadata-only diagnostics guard ran alongside the changed assertions.
- Mutation check: swapping the `lifecycle_stale` fallback for `lifecycle_unavailable` in the handler produced exactly one failure, proving the new fallback coverage has teeth; the handler was restored and re-verified green.
- The transient project directory was deleted and `src/Hexalith.Folders` was left at `HEAD` content after verification.
- `git diff --check` reports no whitespace errors in the changed sources.
- The deferred-work ledger carries orchestrator-owned edits (DW-44/48/50/51 closed, DW-337 appended) made outside this spec's change set; per the run instructions those entries were neither modified nor reverted, so the spec's original `git diff --exit-code -- deferred-work.md` check no longer applies to this pass.

**Results (2026-09-02, second follow-up review pass):**
- `dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj -c Release` in the worktree still fails with the pre-existing `ForgejoProvider.cs(808,11): error CS1513` / `(821,5): error CS1519` pair (ledger `DW-336`); the file is untouched by this spec and its SHA-256 matched `HEAD` before and after verification.
- Isolated Release validation substituted only that baseline defect (added the `}` closing the block-bodied method at line 808, removed the stray `}` at line 821); the core project then built with zero warnings and zero errors. `ForgejoProvider.cs` was restored byte-for-byte afterwards (SHA-256 `744b8f6e90de751113782b6fb2b5166f2e4fc4fc3b1ce913dfec3ca45838e663`, matching `HEAD`).
- The transient lifecycle-only Release test project (`AssemblyName=Hexalith.Folders.Tests`, `OutputType=Exe` as xUnit v3 requires, default compile items disabled, linking `FolderLifecycleFreshnessTests.cs`, `FolderLifecycleStatusProjectionTests.cs`, `FolderLifecycleStatusAuthorizationGateTests.cs`, `FolderLifecycleStatusNoFallbackTests.cs`, `FolderLifecycleStatusMetadataLeakageTests.cs`, `FolderLifecycleStatusTestSupport.cs`, and `ThrowingLifecycleStatusReadModel.cs`, referencing only `Hexalith.Folders.csproj`) built with zero warnings and zero errors; its direct unfiltered xUnit v3 executable passed all 84 tests with zero failures, skips, errors, or tests not run (78 before this pass, plus the six new theory rows).
- Mutation check: swapping `binding_metadata_malformed` for `binding_state_unsupported` in `SuccessWithBinding` produced exactly four failures -- the four malformed-binding rows -- proving the new branch coverage has teeth; the handler was restored and re-verified green at 84/84.
- Read-only endpoint evidence re-confirmed for this pass: `FoldersDomainServiceEndpoints.ToFreshnessResponse` and the lifecycle `Results.Json` branch map only `ReadConsistency`, `ObservedAt`, `ProjectionWatermark`, and `Stale`, and freshness is serialized only on the `Allowed` branch (every other result code returns `SafeProblem`). No reason code and no denied-path watermark is externally visible, so the two tokens introduced by this spec do not trip the Block-If on externally visible lifecycle tokens.
- The transient project directory was deleted and `src/Hexalith.Folders` was left at `HEAD` content apart from this spec's own changes.
- `git diff --check` reports no whitespace errors in the changed sources.
- The deferred-work ledger carries orchestrator-owned edits made outside this spec's change set; per the run instructions those entries were neither modified nor reverted.

## Auto Run Result

Status: done

**Summary of implemented change**

Lifecycle query results are normalized behind two canonical seams: an internal `FolderLifecycleAuthorizationOutcome` enum with a single fail-closed `ToToken` mapping (`DeniedSafe = 0`, unknown values map to `denied_safe`) replaces the per-branch `allowed`/`denied_safe` string constants, and three internal `FolderLifecycleFreshness` transformations (`ToUnavailable`, `ToUnavailableWithFallback`, `ToUnavailableForHandler`) replace the hand-rolled `with { Stale = true, ReasonCode = ... ?? ... }` idiom. Every non-allowed result now suppresses its projection watermark and is marked stale; a nonblank inherited source reason survives a generic status fallback, while a handler-determined reason wins outright. The public positional `AuthorizationOutcome` stays a `string` at constructor index 6, so the packable surface and the endpoint JSON are unchanged. Binding-reference emptiness now routes through the shared `HasValue` predicate at both sites in the handler.

**Files changed**

- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleAuthorizationOutcome.cs` -- new internal enum, `DeniedSafe = 0`.
- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleAuthorizationOutcomeExtensions.cs` -- new single fail-closed enum-to-token mapping.
- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleFreshness.cs` -- adds the three internal unavailable transformations with unconditional watermark suppression and explicit reason precedence.
- `src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs` -- routes every fail-closed branch through those transformations and the typed outcome, makes `safe_not_found` deterministic, and uses `HasValue` in both `HasNoBindingReferences` and `SuccessWithBinding`.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleFreshnessTests.cs` -- new helper-invariant, argument-guard, token, enum-arity, and positional-compatibility assertions.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusProjectionTests.cs` -- fail-closed matrices over read-model statuses, lifecycle states, and binding branches; blank-reason fallback theories; whitespace-only binding references.
- `tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusAuthorizationGateTests.cs` -- denied-safe token, watermark suppression, missing-authentication, and throwing-read-model coverage.
- `tests/Hexalith.Folders.Tests/Queries/Folders/ThrowingLifecycleStatusReadModel.cs` -- new fake proving the read-model exception path stays fail-closed.

**Review findings breakdown (this pass)**

- Patches applied: 3 (medium 1, low 2) -- missing binding-branch coverage, the residual raw `IsNullOrWhiteSpace` duplication in `SuccessWithBinding`, and an unexplained positional-compatibility pin.
- Items deferred: 0. The two standing deferrals (non-available statuses returning an unvalidated future `ObservedAt`; sibling handlers still duplicating the idiom and the outcome constants) already cover the pre-existing findings this pass resurfaced, so nothing new was added.
- Items rejected: 19. The substantive rejections: the forced `Stale = true` and `safe_not_found` on not-found branches are intent-directed (matrix rows 2 and 4) and not externally observable; the double `ToUnavailable()` between `SafeResult` and the branch helpers is defense in depth on a fail-closed invariant; the new `safe_not_found` and `read_model_status_unknown` tokens are not externally visible, since `FreshnessMetadataResponse` carries no reason code and freshness is serialized only on `Allowed`, so no Block-If is tripped and no endpoint, spine, or generated-client test is warranted; and the argument guards on the internal transformations were retained by an explicit earlier review decision.

**Follow-up review recommendation**

`true`. Patched findings this pass: high 0, medium 1, low 2. Score = 3x1 + 1x2 = 5, which meets the threshold of 5.

**Verification performed**

See the `## Verification` results block above. Core Release build zero warnings/zero errors under the isolated DW-336 substitution; transient lifecycle-only Release test assembly zero warnings/zero errors; 84/84 direct xUnit v3 tests passing with zero failures, skips, errors, or tests not run; mutation check produced exactly the four expected failures and was restored green; `git diff --check` clean on the changed sources; `ForgejoProvider.cs` restored byte-for-byte to its `HEAD` SHA-256.

**Residual risks**

- The changed assertions cannot run in the repository's normal build or CI path while `DW-336` leaves `src/Hexalith.Folders` uncompilable; the recorded pass is reproducible only through the transient focused project described above. Nothing in this change set can fix that -- the defect is in an untouched file owned by a separate ledger entry.
- The reason-precedence and watermark-suppression contract is observable only at the in-process `FolderLifecycleStatusQueryResult` surface. No REST consumer reads it today, so a future decision to serialize freshness on non-allowed results would surface these values externally without any contract test standing in the way.
- Five sibling handlers over the same public `FolderLifecycleFreshness` record still hand-roll the superseded idiom and still return a projection watermark on fail-closed results, so the record now advertises an invariant most of its users do not honor. This is the standing deferral, deliberately out of this spec's scope.
