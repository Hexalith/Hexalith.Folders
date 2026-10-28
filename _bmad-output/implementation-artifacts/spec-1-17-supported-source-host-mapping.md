---
title: '1.17 Support the Builds source-host dependency graph'
type: 'bugfix'
created: '2026-10-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '98222637c0e5df4dbb753a51845bb32a06282da9'
story_key: '1-17-publish-the-pd10-v2-authorization-contract-spine'
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/docs/governance/approval-policy.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The completed Debug source-host diagnostic binds EventStore 3.128 successfully using a private reference-replacement import. Ordinary generated source mapping excludes Builds' EventStoreHost and UiHost, leaving their Hexalith dependencies outside source reconciliation. This prevents that diagnostic from establishing a supported source-build path.

**Approach:** Include these source-tree hosts in source-mode reconciliation and preserve the AppHost dependency's Aspire metadata. Reuse the existing mapping engine to select Debug projects. Qualify this build boundary with regression tests; distribution selection and ordinary application execution remain later prerequisites.

## Boundaries & Constraints

**Always:** Work in owning repository `references/Hexalith.Builds`. Let tool-resolved mapping select roots/origins. Preserve package/generated-host isolation, central versions, validation, historical evidence, revisions/indices and pins. Parent stays in-progress; tracker backlog; V1Only; T0 unset; activation authority null; staging 0/14.

**Never:** Reuse consumed approvals, invoke application entrypoints/ComposeRun, start topology, publish/deploy, install tools, initialize references or stage/commit/push. Introduce no root probing, fabricated ordinary mapping/ContentHash, source-package fallback or distribution acceptance. Preserve unrelated dependency-policy decisions.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Mapped source hosts | Valid source mapping; AppHost, EventStoreHost, UiHost | Debug graph selects EventStore.Aspire/Gateway and FrontComposer.Shell projects from mapped roots | Source errors stop compilation |
| Aspire library | Converted EventStore.Aspire reference | IsAspireProjectResource remains false | Regression test detects unintended resource classification |
| Package hosts | Package mapping | Existing host isolation and central package selection remain effective | Existing catalog violations still refuse |
| Generated hosts | Project under run workspace hosts | Mapping exclusions remain effective in either mode | No accidental reconciliation |
| Invalid source | Missing/ambiguous project, escaped root or mixed package origin | Existing categorized refusal before dependent compilation | No dependency change or silent fallback |

</frozen-after-approval>

## Code Map

Paths are relative to `references/Hexalith.Builds` unless marked Folders; base `74932ad0247203797274aba8b7b6366a4bbf92b9`.

- `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingMaterializer.cs`: `ConsumerProjectCondition` excludes both hosts in both modes; retain hash checks.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingReconciliationTask.cs`: reuse evaluated identity discovery and metadata cloning; no rewrite needed.
- `src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs`: launches a prebuilt AppHost; graph tests establish no ordinary startup.
- Folders `_bmad-output/implementation-artifacts/story-1-17-current-builds-host-results.md`: reuse dated binding evidence and limits without replay.

## Tasks & Acceptance

**Execution:**

- [x] `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingMaterializer.cs` — make source-tree EventStoreHost/UiHost exclusions package-mode only; preserve generated-host exclusions.
- [x] `src/hosts/Hexalith.Builds.Module.AppHost/Hexalith.Builds.Module.AppHost.csproj` — mark EventStore.Aspire with `IsAspireProjectResource="false"` so reconciliation retains library behavior.
- [x] `test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceMsBuildTests.cs` — replace both-mode exclusion expectations; test every matrix row with materialized imports, evaluated references and controlled fixtures. Verify real host declarations/metadata without private reference replacement.
- [x] `test/Hexalith.Builds.Module.Tests/Workspace/CatalogVersionTests.cs` — preserve package-host refusal coverage.
- [x] `README.md` — document source-host mapping behavior, package/generated exclusions and the prebuilt AppHost limitation.
- [x] Folders `_bmad-output/implementation-artifacts/story-1-17-supported-source-host-mapping-results.md` — record changes, commands/results, revisions and remaining prerequisites.

**Acceptance Criteria:**

- Given materialized source imports, when host references are reconciled and restored, then selected Hexalith dependencies have project origins under mapped roots and no duplicate package copies.
- Given package mode, when existing catalog and host-isolation tests run, then central versions and refusal behavior remain intact.
- Given successful tests, when results are recorded, then build evidence stays distinct from loader, ordinary-command, signed-package and hosted acceptance.

## Implementation Notes

- 2026-10-27: Jerome approved this spec with `approve and stop`. The stated source-host mapping scope is approved for development; implementation has not started. Resume from this spec in a fresh `bmad-build` session.

- 2026-10-27: Implemented in Builds at unchanged base `74932ad0247203797274aba8b7b6366a4bbf92b9`; source-host scope, Aspire metadata, tests and README are present. The original supported-source test-project build stopped before compilation on unchanged FrontComposer `AdditionalProperties` (HXW006). A separate focused runner passed 115/115 WorkspaceMsBuildTests assertions, but sampled child SDK certificate/serial controls were absent; CatalogVersionTests and WorkspaceAppHostTests remain unrun. Test tasks stay unchecked pending required verification. See [results](story-1-17-supported-source-host-mapping-results.md).
- 2026-10-27: Step 03 acceptance/matrix audit remains incomplete. Stopped dependent execution at prerequisite failure; no review/completion transition, tracker update, Git write, real AppHost or loader replay. Historical/protected bytes, all thirteen revisions and indices are unchanged.

## Spec Change Log

## Review Triage Log

## Verification

Recheck Git state before edits. Before SDK use, assert 10.0.402 from the owning root, retain executable/global.json hashes and explicit environment, set certificate-prevention flags and use private outputs/caches. Bound serial Debug commands; stop on drift/prerequisite failure. Run no consumed loader harness or real AppHost.

Build `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj` with supported source imports and `--configuration Debug -m:1`. Invoke its assembly with `-class` for `Hexalith.Builds.ModuleTool.Tests.Workspace.{WorkspaceMsBuildTests,CatalogVersionTests,WorkspaceAppHostTests}` separately; all must pass. Package fixtures establish no production Release qualification. Run `git diff --check` in both repositories; compare protected historical/tracker/pin bytes. Record exact blockers separately.

2026-10-27 current attempt: verification incomplete; exact commands, environment gap, broad-gate blocker and preservation evidence are recorded in the [results](story-1-17-supported-source-host-mapping-results.md) and [receipt](evidence/story-1-17-supported-source-host-mapping-2026-10-27.json). The frozen intent is unchanged.

2026-10-27 qualification follow-up: source forwarding validation and controlled SDK children are implemented. The reused independent runner passed 14 launcher, 132 workspace and 34 catalog tests (180 total, zero failed/skipped). Production resolution refused external checkout/gitlink drift before materialization; the original Debug graph and all original-assembly class gates, including WorkspaceAppHostTests, remain unrun. No mapping was substituted and no resolver replay or Git mutation ran. This slice remains incomplete; its frozen intent is unchanged. See [qualification results](story-1-17-supported-source-host-qualification-results.md) and [receipt](evidence/story-1-17-supported-source-host-qualification-2026-10-27.json).

## Qualification and review completion — 2026-10-28

The approved follow-up source-host qualification is complete after three independent review layers and correction of all retained defects. Its independent final original Debug graph build passed; original-assembly launcher/workspace/catalog/AppHost classes passed **202/202**, zero failed/skipped/not-run, including the mapped-source Platform assertion. All 29 restored graph assets contain zero Hexalith package libraries. This closes the two previously unchecked verification tasks and this local source-host mapping slice; the original baseline and frozen intent are unchanged. Dated failed attempts and their receipts remain unchanged.

See [reviewed final results](story-1-17-supported-source-host-qualification-final-2026-10-28.md) and [final receipt](evidence/story-1-17-supported-source-host-qualification-resume-2026-10-28.review-final.json). Source Debug compilation remains distinct from distribution, ordinary application startup, signed packages and hosted acceptance. Parent Story 1.17/tracker/authority/routing/staging values remain unchanged; no Git write or operational activation occurred.
