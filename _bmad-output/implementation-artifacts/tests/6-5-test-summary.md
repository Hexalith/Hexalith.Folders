# Test Automation Summary — Story 6.5 (Author Console Diagnostic Wireflow Notes)

**Workflow:** `bmad-qa-generate-e2e-tests`
**Date:** 2026-05-28
**Story:** `_bmad-output/implementation-artifacts/6-5-author-console-diagnostic-wireflow-notes.md` (Status: review)
**Feature under test:** `docs/ux/ops-console-wireflows.md` (the single Story 6.5 deliverable)
**Framework:** xUnit v3 + Shouldly (the project's .NET-first test stack)

## Why these are content/structure tests, not browser E2E

Story 6.5 is a **documentation-authoring** story. It ships **no C#**, **no API endpoint**, and
**no new UI** — the diagnostic pages it describes are built later by Stories 6.6–6.10. The story's
own acceptance criteria are therefore *"content/structure checks against the authored markdown —
there is no compiled artifact"* (story AC preamble; AC #10). The "feature" being tested is the
**reviewed contract** itself: the document that gates Stories 6.6–6.10.

- **API tests:** N/A — Story 6.5 exposes no endpoint. (Audit/timeline endpoints are Story 6.1 and
  already have `Hexalith.Folders.Server.Tests` coverage.)
- **Browser E2E tests:** N/A — Story 6.5 builds no routes or components. The console shell smoke +
  state-label E2E lanes belong to Stories 6.2/6.3 (`Hexalith.Folders.UI.E2E.Tests`).
- **What was generated instead:** an executable **content/structure validation gate** that asserts
  every Story 6.5 acceptance criterion against the markdown deliverable, mirroring the existing
  artifact-gate pattern (`ExitCriteriaDecisionArtifactTests`, `FixtureContractTests`). This is the
  automated form of the AC-7 gate self-review and protects the reviewed contract from silent drift.

## Generated Tests

### Content/Structure validation gate

- [x] `tests/Hexalith.Folders.Testing.Tests/OpsConsoleWireflowNotesTests.cs` — 17 facts validating
  `docs/ux/ops-console-wireflows.md` against Story 6.5 AC #1–#10.

No sidecars, credentials, network, or build output required — the gate reads only the repository
tree (repo root located by walking ancestors to `Hexalith.Folders.slnx`).

## Acceptance-Criteria → Test Coverage

| Story AC | What the gate asserts | Test |
| --- | --- | --- |
| AC #1 skeleton | H1 title, the four required sections, all six Ownership-Metadata keys, `synthetic_data_only: true` | `Ac1_DeliverableExistsWithRequiredSkeleton` |
| AC #1/#9 scope | Scope section declares read-only / projection-backed / metadata-only / MVP / reviewed-contract | `Ac1And9_ScopeAndBoundaryDeclaresReadOnlyProjectionBackedMetadataOnlyMvp` |
| AC #1 gate | Downstream Gate names the deliverable path, all five blocked stories (6.6–6.10), and the review requirement | `Ac1_DownstreamGateBlocksStories66Through610UntilReviewedAgainstFourSources` |
| AC #1 references | References cite all four review-source paths (prd, architecture, ux-spec, FrontComposer research) | `Ac1_ReferencesCiteTheFourReviewSourcesWithPaths` |
| AC #2 hosting | §1 documents shell (`FrontComposerShell`, Interactive Server), nav (`BuildRoute`), `[ProjectionTemplate]`, `IQueryService`/`QueryResult<T>` (`Items`/`TotalCount`/`ETag`/`IsNotModified`), deferred `AddHexalithEventStore`, `Null`→`Folders` context (`Services.Replace`), `[Command]` suppression | `Ac2_FrontComposerHostingModelIsDocumented` |
| AC #3 nine views | Each of the nine per-view subsections (§3.1–§3.9) present and mapped to its owning story | `Ac3_NinePerViewStateSetsPresentEachMappedToItsOwningStory` |
| AC #4 terms | All twelve epic taxonomy terms defined in §2 | `Ac4_TaxonomyDefinesAllTwelveEpicTerms` |
| AC #4 vocabularies | 5 dispositions + 11 C6 states + 4 `FieldDisclosure` members reconciled; `ready`→`available` documented as conditional on `hasProjectionLagEvidence` | `Ac4_TaxonomyReconcilesTheFourSourceVocabularies` |
| AC #4 distinctions | `redacted`≠`unknown`≠`missing`, `denied`≠`inaccessible`, `stale`/`delayed`≠`unavailable` called out | `Ac4_TaxonomyCallsOutTheDeliberateDistinctions` |
| AC #5 UX-DR1–30 | §4 table contains exactly UX-DR1..UX-DR30 (no renumbering; 31/32 correctly excluded) | `Ac5_ConsoleExpectationsCoverUxDr1ThroughUxDr30` |
| AC #5 a11y cluster | §4.1 explicitly covers keyboard, focus restore, non-color-only, zoom 125/150/200%, responsive fallback, redaction-vs-missing-vs-unknown | `Ac5_AccessibilityClusterIsExplicitlyCovered` |
| AC #6 traceability | §5 table covers UX-DR1..UX-DR32 exactly once (32 unique rows); 31/32 flagged release-verified | `Ac6_TraceabilityMapCoversUxDr1ThroughUxDr32ExactlyOnce` |
| AC #6 cross-surface | Every cross-surface row names a non-empty upstream semantic owner; UX-DR31/32 owned by Story 6.11 + Workstream 7, release-verified | `Ac6_CrossSurfaceRowsNameUpstreamOwnerAndReleaseVerifiedRowsAreFlagged` |
| AC #7 journeys | §6 has three Mermaid journeys named "three critical journeys"; each answers all five trust questions | `Ac7_ThreeDiagnosticJourneysEachAnswerTheFiveTrustQuestions` |
| AC #8 pending inputs | §7 lists C2 freshness, C3 retention, C4 filter, `ProjectionAvailability`, French localization | `Ac8_PendingAndDeferredInputsAreEnumeratedAsNotResolvedHere` |
| AC #9/#10 safety | No secret/credential material or raw JWTs; read-only + no-credential-reveal boundary asserted | `Ac9And10_DocumentIsMetadataOnlyWithNoSecretsOrRealData` |
| AC #10 markdown | Code fences balanced (even); exactly three Mermaid blocks | `Ac10_MarkdownFencesAreBalancedWithExactlyThreeMermaidBlocks` |

## Coverage

- **Story 6.5 acceptance criteria:** 10 / 10 covered (AC #1–#10).
- **Generated tests:** 17 facts, all passing.
- **Negative/critical-error cases included:** secret/JWT denylist, exact UX-DR cardinality
  (30 in §4, 32 in §5, none renumbered), no-duplicate-ID enforcement, balanced fence count,
  non-empty upstream-owner for cross-surface rows.

## Run Results

```
dotnet test tests/Hexalith.Folders.Testing.Tests --filter FullyQualifiedName~OpsConsoleWireflowNotesTests
Passed!  - Failed: 0, Passed: 17, Skipped: 0, Total: 17
```

Build: 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).

### How to run

```powershell
# Windows SDK is required (the WSL SDK fails the global.json 10.0.302 pin).
dotnet build  tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj
dotnet test   tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj `
  --no-build --filter FullyQualifiedName~OpsConsoleWireflowNotesTests
```

## Auto-applied gaps

Per the "auto-apply all discovered gaps" instruction, while authoring the gate one coverage gap was
closed automatically (no doc gaps were found — the deliverable satisfied all AC #1–#10 on first run):

- Added `Ac6_CrossSurfaceRowsNameUpstreamOwnerAndReleaseVerifiedRowsAreFlagged` to enforce AC #6's
  requirement that **cross-surface rows name an upstream semantic owner** and that **UX-DR31/UX-DR32
  are flagged release-verified via Story 6.11 + Workstream 7** — beyond the basic 32-unique-IDs check.

## Pre-existing failures (NOT introduced by this work — out of scope)

Running the **full** `Hexalith.Folders.Testing.Tests` project surfaces 3 failures that are
**unrelated to Story 6.5** and predate this change. The only working-tree code change here is the
new `OpsConsoleWireflowNotesTests.cs`; none of the three failing tests read that file or
`docs/ux/`, so they fail identically with or without it:

- `ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects` — the `.slnx` lists test
  projects (e.g. `UI.E2E.Tests`, `UI.Tests`, `Mcp.Tests`, …) added during Epic 6 that the contract's
  canonical project list has not been updated for.
- `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` — same solution-inventory
  drift, evaluated over `.csproj` ProjectReferences.
- `FixtureContractTests.DeferredArtifactAreasCarryMachineCheckableOwnershipNotes` — a deferred-artifact
  ownership-note check over fixtures/`deferred-work.md`, untouched by this story.

These are solution-governance contracts that belong to a separate triage (likely a sprint-planning /
scaffold-inventory update). They are surfaced here rather than silently "fixed" because they encode a
real drift signal the team should resolve deliberately. Story 6.5's deliverable explicitly must
**not** edit `.slnx`/`.csproj`/fixtures, so adjusting those contracts is out of this workflow's scope.

## Validation against `checklist.md`

- [x] API tests — N/A (no endpoint in this doc-only story); documented above.
- [x] E2E tests — N/A (no UI built by 6.5); content/structure gate generated instead.
- [x] Tests use standard framework APIs (xUnit v3 + Shouldly).
- [x] Tests cover happy path (deliverable satisfies all AC #1–#10).
- [x] Tests cover critical "error" cases (secret leakage, ID over/under-count, unbalanced fences).
- [x] All generated tests run successfully (17/17).
- [x] Proper "locators" — semantic section anchors + GFM table parsing (no brittle line numbers).
- [x] Tests have clear descriptions (AC-named methods + explanatory assertion messages).
- [x] No hardcoded waits or sleeps.
- [x] Tests are independent (each re-reads the doc; no shared state or ordering).
- [x] Test summary created (this file).
- [x] Tests saved to the appropriate project (`tests/Hexalith.Folders.Testing.Tests`).
- [x] Summary includes coverage metrics.

## Next Steps

- Wire `OpsConsoleWireflowNotesTests` into CI as the executable AC-7 gate for the wireflow contract
  (it runs in the default `dotnet test Hexalith.Folders.slnx` lane already).
- As Stories 6.6–6.10 build the pages this contract describes, add their own browser E2E coverage in
  `Hexalith.Folders.UI.E2E.Tests` against the routes/selectors those stories ship.
- Separately triage the 3 pre-existing `ScaffoldContractTests`/`FixtureContractTests` failures
  (solution-inventory + deferred-ownership drift) — not part of Story 6.5.
