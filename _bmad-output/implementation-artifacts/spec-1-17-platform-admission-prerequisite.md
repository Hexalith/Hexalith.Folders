---
title: 'Story 1.17 Platform admission prerequisite'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '9fe3fd38c0da74e804dd87c20d2ed59f93085eae'
context:
  - '/home/administrator/projects/hexalith/folders/AGENTS.md'
  - '/home/administrator/projects/hexalith/folders/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="user authorized necessary reversible local preparation in the invocation">

## Intent

Remove the next executable admission prerequisite exposed by the October 16 Platform bootstrap. Resolve the default file-entry workspace through the existing Builds mapping, repair the two Builds package-seed subscription failures, and prepare an honest release/intake handoff from observed identities.

## Boundaries & Constraints

Preserve existing changes, test expectations, release identity, clean-source, catalog-pin, tool-range and snapshot provenance checks. Use the owning repositories. Never load skills from references. Never initialize nested dependencies, invent a tag, accepted tool range, approval or artifact provenance, or stage, commit, push, publish, deploy or activate production. Production remains V1Only, T0 null, Story 1.17 backlog/open; historical approvals and the qualification packet stay unchanged. Existing local modifications are authorized inputs. This bounded prerequisite does not implement Platform's entire planned intake/publication system.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior |
|---|---|---|
| Default entry | No declaration or mapping override | Anchor existing WorkspaceRootResolver on Platform's identity record; owner declarations come from its single root mapping |
| Explicit declaration | Caller supplies declaration | Preserve existing resolver anchor and validation |
| Explicit mapping | Caller supplies changed mapping hash | LoadVerified rejects before any resources |
| Missing owner | Resolved mapping lacks EventStore | Refuse by owner identity before any resources |
| Configured seed | Programmatic subscriptions and configured pubsub | HTTP 200 and projection metadata bind configured-seed-events |
| Declarative seed | Dapr:DeclarativeSubscriptions=true | HTTP 404 for discovery and no programmatic projection topic metadata |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Platform/src/Hexalith.Platform.Composition/PlatformAppModel.cs`: Compose currently anchors absent nested owner declaration. Use `hexalith-platform.json` under builder.AppHostDirectory when DeclarationPath is absent. Preserve explicit declaration/mapping branches and all existing pre-resource validation.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Workspace/WorkspaceRootResolver.cs`: existing outermost workspace/direct-reference mapping. Do not add probing or change admission behavior. SourceMapping.LoadVerified retains content binding.
- `references/Hexalith.Platform/src/Hexalith.Platform.EventStoreHost/Program.cs`: established projection subscription behavior to reuse verbatim in Builds seed (Dapr ITopicMetadata/TopicAttribute and ProjectionChangeNotifierOptions).
- `references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.EventStoreHost/Program.cs`: unconditional MapControllers/MapSubscribeHandler causes recorded two failures.
- `references/Hexalith.Platform/tests/Hexalith.Platform.Composition.Tests/GatewaySeedSubscriptionTests.cs`: preserve all current assertions; four seed HTTP cases. WriterProtocolStartupTests supplies two adjacent checks.
- `references/Hexalith.Platform/tests/Hexalith.Platform.Composition.Tests/PlatformAppModelReviewTests.cs`: existing hash refusal; PlatformFourthReviewTests currently assumes initialized nested reference layout. Add isolated mapping tests without editing existing expectations or requiring nested initialization.
- `references/Hexalith.Platform/docs/platform-apphost-form.md`: update default anchor description only.
- Platform planning epics Stories 1.9/4.8/4.12: acceptance, intake and publication remain owner work. No implemented intake or publication workflow exists today.

## Tasks & Acceptance

- [x] Update Platform's default anchor and add focused isolated regression tests for default/explicit selection, mapping hash and missing-owner refusal. Avoid synthetic acceptance of the real workspace. Tests may use disposable fixtures; do not mutate real Git state.
- [x] Reuse Platform subscription behavior in Builds seed; retain package reference and current tests.
- [x] Update Platform host documentation; provide no new module CLI/topology/deployer.
- [x] Parent coordinator retains read-only remote observations, exact local hashes/patches, an inert intake proposal with unresolved acceptance fields and commands, fresh qualification evidence and append-only readiness addendum.

Given an umbrella workspace, when the default entry resolves dependencies, then it uses the established single mapping and never assumes nested owner source. Given an unaccepted release/tool/catalog tuple, when package admission runs, then existing refusal checks remain active. Given either subscription mode, when the unchanged HTTP regression suite runs, then all six seed/writer checks pass.

## Implementation Notes

The invocation authorizes reversible changes and continuing with dirty recorded preparation; no repeat approval is needed. Parent handles discovery and qualification evidence. Implementer handles the first three tasks and their narrow verification only. If a broader missing dependency appears, record it rather than weakening any gate. Baselines: Platform 1711deba08a4e66b46acfff8d08e7f2ad7f74ba1 plus bootstrap edits; Builds 728174f506d56204657e0c52e98a4453aef79f6a initially clean. Platform's committed Builds pin 5636b7f36e28e2731111b049f974b1fa951d1c36 is unavailable locally and GitHub returns 404. No published admitted Platform tuple is known.

- Parent discovered current Builds push CI and local audit validation fail on 19 stale catalog/audit entries. Prepare an audit refresh through the established generator with the preserved prior audit, keeping all package selections and declarations unchanged; validate before adopting. This is release preparation, not acceptance or publication.

## Verification

Build Platform composition tests in audited Debug with warnings as errors and serialized MSBuild. Run built xUnit v3 executable with single-dash class/method filters for new mapping tests, GatewaySeedSubscriptionTests and WriterProtocolStartupTests. Run Builds Workspace identity/catalog/mapping contract tests where feasible. Retain exact command/outcome and report any broader blocker separately. Never alter existing assertions to fit results.

- Parent completed the generated audit refresh and validation, retained all 7,144 historical entries, and captured the actual source/package refusals. A serialized Release AppHost build supplied the unchanged CLI-version test prerequisite; its replay passed, bringing final unique Builds guard outcomes to 242/242. No real repository Git writes or operational transitions occurred.

## Spec Change Log

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| BH1 late Builds selection | Medium: a real MSBuild CustomAfterMicrosoftCommonProps mapping overrides HexalithBuildsRoot after the cached normalized property, leaving catalog/tooling on a sibling checkout. This is exposed by the admission path and contradicts the single mapping invariant. | Patch: normalize after the import; add real-MSBuild regressions. |
| BH2 stale seed ledger entry | Medium: the prior deferred entry still reads as outstanding although both failures now pass. Future deferred-work processing can rediscover it. | Patch: append an explicit resolution reference without changing the historical failure. |
| BH3 Builds-owned regression | Medium: current Builds publication qualification does not execute Platform's seed HTTP assertions. This is a pre-existing cross-repository qualification ownership gap; no released or published correction is claimed. | Defer: require packaged-seed regression in Builds qualification before publishing/accepting the new tool. |
| BH4 packaged seed checks | Medium: source-project tests do not verify the future distributable seed. The receipt explicitly says current 4.31.2 lacks the fix and requires a new clean release plus exact-artifact qualification. | Defer: retain this publication/acceptance prerequisite. |
| BH5 full six-server fixture | Low: new tests prove the changed resolver selects direct owners before binding; ordinary declaration closure tests own complete graph binding. No defect in the unchanged graph composition was demonstrated. | Reject: additional complete topology fixture exceeds a direct correction for this low-impact coverage suggestion. |
| BH6 valid explicit-declaration fixture | Low: the branch is unchanged and the new test proves explicit-anchor precedence through its distinct declaration diagnostic; no precedence regression was demonstrated. | Reject: avoid duplicating broader unchanged declaration composition fixtures. |
| BH7 standalone default fixture | Low: existing MSBuild bootstrap fixtures cover standalone dependencies, while the changed default anchor uses the same outermost resolver whose standalone cases ran in the 242 Builds guards. No standalone defect demonstrated. | Reject: no additional fixture complexity for this low-impact suggestion. |
| BH8 global Git configuration | Medium: new disposable commits inherit commit signing/hooks, so a developer's normal Git settings can break the mapping suite. | Patch: isolate fixture process Git configuration; preserve real repository settings/hooks. |
| BH9 Windows fixture cleanup | Medium: Git object files can be read-only on Windows and Directory.Delete can mask results. The fixture owns this disposable tree. | Patch: normalize fixture file attributes before deletion. |
| BH10 ignored evidence retention | Medium: log and source-patch hashes alone cannot reconstruct ignored artifacts from a clean checkout. | Patch: retain a hash-bound evidence archive and member manifest in the root evidence directory; large public package bytes remain identified by URL/hash. |

Verification-gap reviewer: no gaps. Edge-case reviewer: no findings.

Review patches also retain diagnostic logs/source patches in a portable evidence bundle and reconcile the historical seed deferred entry. Future Builds-owned packaged-seed HTTP qualification remains explicitly deferred before publication/acceptance; local source tests do not replace it.

Final review patches passed: 12 real-MSBuild resolution cases, 10 pin-guard cases, all 11 mapping/seed/writer cases, audited Debug file-AppHost and composition test builds. The 242 Builds guards retain passing final outcomes on unchanged Builds code/test inputs. Scope completion does not close Story 1.17; its tracker, production mode and approvals remain unchanged. No commit was created because the user requires separate Git authorization.
