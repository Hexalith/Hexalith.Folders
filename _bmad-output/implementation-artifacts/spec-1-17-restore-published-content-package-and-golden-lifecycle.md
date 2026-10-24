---
title: 'Restore Story 1.17 published content package and golden lifecycle'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
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

- [x] `Directory.Packages.props`, `references/Hexalith.Builds/Props/Directory.Packages.props` -- verify 3.127.0 signatures/metadata/interfaces; update both EventStore properties, preserving formatting.
- [x] `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs` -- repair request and content composition; assert persisted publication/bytes. Preserve all nine operations and negatives; add no nested helper types.
- [x] `_bmad-output/implementation-artifacts/evidence/story-1-17-content-package-2026-10-23.json` and adjacent `.artifacts.tar.gz` -- retain commands, revisions, package/runtime hashes, safe logs and preservation audit; distinguish probes from default builds.

**Acceptance Criteria:**

- Given adopted pins, when default audited Release/package solutions build, then both pass without override/source substitution.
- Given the refreshed host, when source/package content, routing and golden classes run, then all pass with state/content assertions intact.
- Given the package, when both server suites run, then no failures/new skips occur; distinguish the existing Projects skip.
- Given completion, when preservation is audited, then history/routing/tracker bytes match and PD10/operational work remains open.

## Implementation Notes

- 2026-10-24: Jerome approved and continued this exact prerequisite scope. Preserve the existing baseline and frozen intent. The no-Git-mutation and no-tracker-transition boundaries override generic workflow staging, commit and synchronization defaults.

- Implementation completed in the two owning catalogs and one integration fixture. AddFile now supplies valid hash/version/fence and uses the real authoritative store with persisted test transport; publication metadata and exact bytes are asserted. The positive context-read seed supplies the current handler's required fresh read_metadata grant. Its former candidate-only grant premise is no longer claimed; PD10 authorization-map reconciliation remains separate open work.
- Final post-format verification: both default audited Release/package CI solution builds pass; the focused content/routing/golden classes pass 115/115 in Debug/source and 115/115 in Release/package. Folders server passes 1216/1216; Projects passes 1033 with its one existing isolated-lane skip. Server assembly hashes remain unchanged after the fixture-only formatting correction. Parent independently reran the final package nine-step method with XML output: 1/1 passed.
- Parent acceptance audit independently verified all 229 archive members, 35 captured command logs, signed-package/runtime payload bindings and live Release binaries. The complete 12024-file root and 2035-file Builds baseline inventories permit only the approved pins, fixture and workflow bookkeeping; new paths are exactly the receipt and archive. Both owning diff checks pass. The first focused formatter failure was corrected and its original observation preserved; final formatter checks pass.
- Matrix audit: package graph behavior is covered by both default build commands plus 56 owner asset snapshots; persisted AddFile by GoldenLifecycleAllNineStepsDriveOverRealRestTransportToTransportTerminalClass and ActualBytesShouldTraverseTheGatewayAndOwningProcess; safe negatives by the latter's stale-fence/restricted-path assertions, the full GoldenLifecycleParityTests class and full Folders server suite. Every covering lane ran and passed; no matrix case relies on the existing Projects skip.
- Required pre-edit Aspire observation was missed. The explicitly post-edit start exits 2 on the previously recorded CS1704 duplicate Commons.UniqueIds references; describe confirms Folders is stopped and sanitized before/after ps observations preserve the unrelated AppHost. No host repair or operational acceptance is claimed. Evidence retains the missed prerequisite separately from passing focused checks. The receipt binds a historical implementation-spec snapshot and the exact approved frozen block, allowing subsequent status/checklist/notes updates.

## Spec Change Log

## Review Triage Log

All three review layers completed before triage. Verification-gap review returned no findings. Blind floor arithmetic is informational (9 requested); each of its nine findings and the edge finding is recorded separately below. The review diff excludes this mutable claims file, which was supplied separately only to the edge reviewer; the complete baseline diff remains retained for parent audit.

| Finding | Verdict | Evidence and route |
|---|---|---|
| B1: cached context drops explicit checked-in equality | low | The cache says reproducible but omits the former explicit zero-diff phrase; canonical generation/conformance checks remain intact. This can mislead a future session using the cache. Defer the agent-context correction. |
| B2: cache omits concrete CLI/MCP error mappings | low | Exact 73/77 and failure-kind mappings remain in canonical contracts/code/tests, but are absent from the regenerated context. Future cached-context sessions need those constraints. Defer the agent-context correction. |
| B3: cache omits evidence from content-exclusion wording | low | The cache's destination list lost evidence; normative metadata-only rules and this synthetic-only archive remain intact. Preserve explicit guidance for future evidence authors. Defer the agent-context correction. |
| B4: candidate-only context authority no longer exercised | medium | The happy-path seed now grants read_metadata, while the handler explicitly rechecks it before QueryAsync. The prior diagnostic reproduced denial with mutate_files alone; a negative request plus source-call assertion preserves this boundary. Patch within the fixture. |
| B5: content-authority substitute accepts incomplete identity/scope | medium | Its AuthorizeAsync checks only authentication, tenant and domain, unlike the registered production authority's current task/resource policy. Reusing the existing production authority removes this fidelity gap without a new API. Patch separately from publication matching. |
| B6: source publication substitute matches CommandId only | medium | GetPublicationOutcomeAsync ignores bindings that production WorkspaceContentAuthority compares: scope, task, fence, version, receipt and metadata. A mismatched source binding could be accepted by this double. Patch by using the existing production authority. |
| B7: idempotency count is not a general stream head | false | This private fixture's content folder is seeded by six distinct-key events; its only appended batch returns exactly one event (asserted). Count equals the represented head on this concrete path. No multi-event/shared-key call reaches this reader; the generalization is not a current defect. |
| B8: AddFile lacks explicit ChangesStaged assertion | low | The subsequent Commit step uses another folder, so publication/byte assertions alone do not prove this folder stays staged. Add the direct state assertion. Patch. |
| B9: no inline-specific metadata/body exclusion assertion | false | The current source-folded binding and framework batch event are closed metadata-only types with no body field; production is unchanged, and the executed content wiring case separately excludes bytes from metadata CAS writes. No actual metadata/body leak or missing requirement is established by this proposed additional assertion. |
| E1: mismatched receipt/scope/fence can be accepted | medium | Same verified CommandId-only predicate as B6; group only with B6. The production authority already compares these fields, so reuse it rather than duplicate a second matcher. Patch. |

Survivor groups: B1/B2/B3 share incomplete regeneration of the agent context and are deferred together under the workflow's agent-context rule. B4, B5, B6/E1 and B8 are separate patch entries. No intent gap or bad-spec loopback is needed: all fixes are private fixture assertions or removal of the new substitute, with existing production policy retained.


Resolution: B4 now has an explicit mutate-only 404/resource_unavailable/redacted denial with zero context-source calls before the positive REST/SDK reads. B5 and B6/E1 are resolved by deleting the substitute and using the production WorkspaceContentAuthority, including its complete publication comparison; the private test transport now supplies legitimate seeded actor snapshots, and empty streams use null state. B8 directly asserts ChangesStaged after AddFile. All four patch entries are closed. B1/B2/B3 remain in one appended context-only deferred entry; B7/B9 are rejected on their recorded evidence.

Final parent verification independently ran both default audited CI builds and all 115 integration cases in each dependency mode, with explicit passing XML cases and final formatting success. The unchanged Folders/Projects server assemblies preserve the earlier 1216 and 1033 passing results plus one existing skip. Final evidence binds 272 archive members and 50 command logs; parent independently verified all hashes, live package runtimes, both 115-case XML results, exact frozen intent, unchanged gitlinks and empty staged index. Source phase identity is explicitly separate from later AppHost Debug-output replacements; tested copies match the retained source-qualified payloads.

This bounded prerequisite spec is complete. Story 1.17's tracker and parent closure remain unchanged under the approved no-transition policy. The approved no-Git-mutation boundary also overrides the generic local-commit default, so no commit or push is performed. Pre-edit Aspire observation remains recorded as missed; its post-edit CS1704 blocker and PD10/operational gates remain unresolved.

## Design Notes

No intent gaps or irreversible actions. Footprint: two properties, one fixture, retained evidence. Catalog adoption affects other consumers; report tested scope. PD10 inventory's 32 stale rows and qualification packet's six stale bindings require separate semantic reconciliation.

## Verification

- `dotnet build <Hexalith.Folders.CI.slnx|Hexalith.Projects.CI.slnx> --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1` from each owner; retain auditing.
- Build IntegrationTests with `--configuration Debug -p:UseNuGetDeps=false -m:1`; invoke built assembly with `-class` for `WorkspaceContentProcessWiringTests`, `FoldersApiRoutingModeTests`, `GoldenLifecycleParityTests`. Repeat fresh Release/package checks.
- Run both server assemblies. Verify assets/runtime DLLs against signed archives and `git diff --check` in both owners.

Planning: both override builds pass; Folders server 1216 pass; Projects 1033 pass/one skip; focused integration 114 pass/one golden failure, reproduced in fresh source mode. Default Folders build remains blocked. No tracked pins/runtime changed.
