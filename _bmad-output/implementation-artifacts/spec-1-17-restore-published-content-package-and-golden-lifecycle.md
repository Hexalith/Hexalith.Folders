---
title: 'Restore Story 1.17 published content package and golden lifecycle'
type: 'bugfix'
created: '2026-10-23'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4403ef17f76edf055d4a7b520abb340730df5592'
story_key: '1-17-publish-the-pd10-v2-authorization-contract-spine'
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/docs/governance/approval-policy.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Folders' package build fails with 43 errors because EventStore 3.126.1 lacks required content APIs. Published 3.127.0 restores both solution builds under an override. The REST golden lifecycle also fails in source and package modes because its AddFile fixture predates the content contract.

**Approach:** Adopt the published package and exercise the authoritative content pipeline in the golden fixture. This prerequisite leaves parent conformance and operational gates open.

## Boundaries & Constraints

**Always:** Limit catalog edits to EventStore properties. Separate audited Release/package and Debug/source validation. Preserve real REST, gateway `/process`, authorization, 202/correlation assertions and persisted publication/content checks. Bind evidence to exact packages and assemblies.

**Never:** Change product behavior, contracts, gitlinks, unrelated versions, assertions or timeouts; publish/deploy; mutate Git; initialize nested submodules; reseal conformance or amend approvals/tracking. Preserve user work, history, `V1Only`, unset T0, accepted rollout branches, original baseline and seven-day limit. Local checks establish no hosted acceptance or parent closure.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Package graph | EventStore 3.127.0 | Both solutions build without override | Missing API/mixed graph blocks acceptance |
| Golden AddFile | Valid hash/version/fence, principal, policy/store | 202 crosses `/process`; publication/bytes persist | Keep rejection assertions |
| Security negatives | Invalid authority/content/fence | Existing safe rejection | Never relax validation |

</frozen-after-approval>

## Code Map

- `Directory.Packages.props`: explicit override defeats a Builds-only update.
- `references/Hexalith.Builds/Props/Directory.Packages.props`: shared property consumed by Projects. Baselines: Builds `70ead608ac01f66dfcd80c13bb71152dac6c128e`; Projects `0247ccb999d788a6d92984362e5717955d58caa8`.
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`: invalid `hashref-a`, missing version/fence and obsolete host content stub.
- `tests/Hexalith.Folders.IntegrationTests/WorkspaceContentProcessWiringTests.cs`: reuse persisted RecordingDaprClient, non-confidential policy, principal, content authority and folded WorkspaceContentStateReader pattern; existing types/substitutes suffice.

## Tasks & Acceptance

**Execution:**

- [ ] `Directory.Packages.props`, `references/Hexalith.Builds/Props/Directory.Packages.props` -- verify 3.127.0 signatures/metadata/interfaces; update both EventStore properties, preserving formatting.
- [ ] `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` -- repair request and content composition; assert persisted publication/bytes. Preserve all nine operations and negatives; add no nested helper types.
- [ ] `_bmad-output/implementation-artifacts/evidence/story-1-17-content-package-2026-10-23.json` and adjacent `.artifacts.tar.gz` -- retain commands, revisions, package/runtime hashes, safe logs and preservation audit; distinguish probes from default builds.

**Acceptance Criteria:**

- Given adopted pins, when default audited Release/package solutions build, then both pass without override/source substitution.
- Given the refreshed host, when source/package content, routing and golden classes run, then all pass with state/content assertions intact.
- Given the package, when both server suites run, then no failures/new skips occur; distinguish the existing Projects skip.
- Given completion, when preservation is audited, then history/routing/tracker bytes match and PD10/operational work remains open.

## Implementation Notes

- 2026-10-24: Jerome approved and continued this exact prerequisite scope. Preserve the existing baseline and frozen intent. The no-Git-mutation and no-tracker-transition boundaries override generic workflow staging, commit and synchronization defaults.

## Spec Change Log

## Review Triage Log

## Design Notes

No intent gaps or irreversible actions. Footprint: two properties, one fixture, retained evidence. Catalog adoption affects other consumers; report tested scope. PD10 inventory's 32 stale rows and qualification packet's six stale bindings require separate semantic reconciliation.

## Verification

- `dotnet build <Hexalith.Folders.CI.slnx|Hexalith.Projects.CI.slnx> --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1` from each owner; retain auditing.
- Build IntegrationTests with `--configuration Debug -p:UseNuGetDeps=false -m:1`; invoke built assembly with `-class` for `WorkspaceContentProcessWiringTests`, `FoldersApiRoutingModeTests`, `GoldenLifecycleParityTests`. Repeat fresh Release/package checks.
- Run both server assemblies. Verify assets/runtime DLLs against signed archives and `git diff --check` in both owners.

Planning: both override builds pass; Folders server 1216 pass; Projects 1033 pass/one skip; focused integration 114 pass/one golden failure, reproduced in fresh source mode. Default Folders build remains blocked. No tracked pins/runtime changed.
