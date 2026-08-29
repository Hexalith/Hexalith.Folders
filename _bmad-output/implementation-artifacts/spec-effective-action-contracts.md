---
title: 'Enforce effective-action contracts'
type: 'bugfix'
created: '2026-08-29'
status: 'done'
baseline_revision: '7927e62a629a46a5c32c51df781f5df0c0d72b5c'
baseline_commit: '7927e62a629a46a5c32c51df781f5df0c0d72b5c'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/project-context.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** The effective-permission catalog's canonical action order has no regression test, and `EffectivePermissionsTaskScope` trusts the comparer of a caller-supplied set. A case-insensitive set can therefore make task-scope narrowing treat a non-canonical case variant as an allowed action.

**Approach:** Pin the complete existing catalog order through its comparison surface, and defensively normalize task-scope allowed actions to ordinal membership during construction. Cover both construction and query behavior with sets whose comparers are incompatible with the contract.

## Boundaries & Constraints

**Always:** Preserve the current lower-snake-case action vocabulary and exact catalog order; use `StringComparer.Ordinal` for action identity; keep task scope narrowing-only; reject a null allowed-actions collection at the public boundary; follow one-type-per-file and existing xUnit v3/Shouldly conventions.

**Block If:** The fix requires changing the OpenAPI Contract Spine, generated client surface, or canonical action vocabulary/order rather than enforcing the existing contract.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md` or any other deferred-work ledger; hand-edit generated clients; modify `references/` submodules; broaden task-scoped permissions; introduce culture-sensitive or case-insensitive action matching.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Catalog ordering | Complete catalog supplied in a different order | Sorting with `CompareActions` returns the exact pinned canonical sequence | No error expected |
| Exact task action | Incompatible-comparer set containing the exact canonical token | Construction preserves the token with ordinal membership and the query may retain that action | No error expected |
| Case variant | Case-insensitive set containing only an uppercase variant of a canonical token | Construction does not make the lowercase token a member; the query removes the action and returns safe denial | No error expected |
| Missing set | Null allowed-actions collection | Construction fails at the boundary | Throw `ArgumentNullException` |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs:5` -- `OrderedActions` is the canonical sequence; `CompareActions` at line 86 is the observable ordering surface to pin without expanding public API.
- `src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs:3` -- positional record currently stores the supplied `IReadOnlySet<string>` unchanged; normalize and document ordinal membership here while preserving constructor call shape.
- `src/Hexalith.Folders/Authorization/EffectivePermissionsQueryHandler.cs:184` -- `ApplyTaskScope` narrows at line 229 through `AllowedActions.Contains`; keep this query logic and make its membership semantics reliable at construction.
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsTaskScopeTests.cs:7` -- existing task-scope query fixtures always use `StringComparer.Ordinal`; add direct construction and incompatible-comparer query regressions.
- `tests/Hexalith.Folders.Tests/Authorization/ArchiveActionCatalogTests.cs:7` -- nearby tests prove individual action-to-level mappings but do not pin the complete order; keep them unchanged and add a focused catalog contract test.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- orchestrator-owned, read-only ledger; resolution must be demonstrated only through code/tests and this run spec.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs` -- defensively copy/freeze supplied actions with `StringComparer.Ordinal`, validate null, and document the membership contract while preserving the positional construction API.
- [x] `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsActionCatalogTests.cs` -- add a complete-order regression that sorts a deliberately reordered catalog through `CompareActions` and asserts the exact sequence.
- [x] `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsTaskScopeTests.cs` -- add construction and end-to-end query tests using `StringComparer.OrdinalIgnoreCase` inputs, covering exact-token retention and case-variant rejection.

**Acceptance Criteria:**
- Given every supported effective-permission action in non-canonical order, when ordered with `EffectivePermissionsActionCatalog.CompareActions`, then the result exactly matches the pinned current catalog sequence.
- Given an `EffectivePermissionsTaskScope` constructed from an ordinal-ignore-case set, when membership is queried after construction, then only exact ordinal action tokens match and later mutation of the input cannot change the scope.
- Given effective folder evidence for `read_metadata` and a task scope supplied with only `READ_METADATA` under an ordinal-ignore-case comparer, when the effective-permissions query narrows by task scope, then it returns `DeniedSafe` with no permissions.
- Given the same incompatible comparer contains the exact `read_metadata` token, when the query narrows by task scope, then it returns `Allowed` with `Read` permission.
- Given null allowed actions, when task scope construction is attempted, then `ArgumentNullException` is thrown.

## Spec Change Log

## Review Triage Log

### 2026-08-29 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 2: (high 0, medium 1, low 1)
- defer: 0
- reject: 15: (high 0, medium 2, low 13)
- addressed_findings:
  - `[medium]` `[patch]` Bound the catalog-order test to the production ordered sequence and verified that the sequence is unique, exact, and complete for all mapped actions through non-public test seams.
  - `[low]` `[patch]` Added incompatible-comparer and defensive-snapshot coverage for the custom `AllowedActions` init path exercised by record `with` assignment.

## Design Notes

Normalize at the task-scope boundary rather than compensating in `ApplyTaskScope`: every consumer then observes the same ordinal contract, and the query remains a simple intersection. Preserve the positional record signature so existing named construction and deconstruction call sites do not drift.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release --filter "FullyQualifiedName~EffectivePermissionsActionCatalogTests|FullyQualifiedName~EffectivePermissionsTaskScopeTests"` -- expected: focused contract tests pass.
- `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release` -- expected: the complete core unit-test project passes with warnings as errors.

## Auto Run Result

Status: done

### Summary

Pinned the production effective-permission action order and made task-scope allowed actions immutable snapshots with ordinal membership, independent of the supplied set comparer. Added construction, init-assignment, and query regressions for incompatible comparers.

### Files Changed

- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs` -- added non-public read-only catalog-order and mapping-completeness test seams.
- `src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs` -- freezes supplied actions with `StringComparer.Ordinal`, rejects null, and documents the contract.
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsActionCatalogTests.cs` -- pins the exact production sequence and proves complete, unique mapping coverage.
- `tests/Hexalith.Folders.Tests/Authorization/EffectivePermissionsTaskScopeTests.cs` -- covers construction, record init assignment, defensive snapshotting, null rejection, and incompatible-comparer query outcomes.
- `_bmad-output/implementation-artifacts/spec-effective-action-contracts.md` -- records the plan, review triage, verification, and completion result.

### Review Findings

- Patches applied: 2 (high 0, medium 1, low 1).
- Items deferred: 0.
- Items rejected: 15.
- Follow-up review recommendation: false; patch score `3 × 1 medium + 1 × 1 low = 4`.

### Verification Performed

- Focused Release tests: 13 passed, 0 failed, 0 skipped.
- Complete `Hexalith.Folders.Tests` Release project: 1,750 passed, 0 failed, 0 skipped.
- Matrix audit: all catalog ordering, exact-token, case-variant, null-input, defensive-copy, and init-assignment behaviors ran and passed.
- `git diff --check`: passed with no whitespace errors.
- Deferred-work ledger and `references/` submodules remained unchanged.

### Residual Risks

No known functional residuals. Distributed runtime validation was not required for this isolated in-memory authorization contract change; the relevant Release unit project and its full regression suite passed.
