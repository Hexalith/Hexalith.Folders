---
title: 'Complete Story 1.17 PD10 v2 migration and closure'
type: 'feature'
created: '2026-09-25'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '37d348945b35620da6ba4e524a09bee202712460'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.17 has a tested, 222-artifact PD10 v2 candidate, but deployed Projects still uses v1. There is no released v2 package pair or migration evidence, so the story remains open.

**Approach:** Finish the accepted seven-day Projects coexistence plan: establish packages and consumer readiness, make route use observable, rehearse reversal, execute after entry checks, then prove retirement and close the story.

**Scope decision (2026-09-26):** Full migration includes publication, deployments, observation, v1 retirement, and gated closure.

## Boundaries & Constraints

**Always:** Preserve historical v1 and signed OQ3/A6b/A8 evidence. Keep `V1Only` until every entry check passes. Bind results to exact revisions and rerun changed-byte checks. Use metadata-only, version- and consumer-attributed telemetry. T0 is the earlier of v2 becoming routable or its first production request; deploy Projects within 24 hours, then observe 24 hours of ordinary traffic and all periodic calls before retiring v1 by T0+168 hours. Restore v1 on rollback triggers.

**Never:** Infer approval from historical digests; claim absent traffic from source search; expose v2 or retire v1 before its gate; extend the window silently; claim rollback undoes mutations; start another story; or edit `sprint-status.yaml` without its authorized transition.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Hold | Default host configuration | v1 works; v2 is unrouted | Invalid mode fails startup |
| Coexistence | All entry evidence accepted | v1 and v2 work; attributed counts and errors are visible | Failed smoke or threshold triggers rollback |
| Projects migration | Exact released v2 client and contract | Lifecycle, permissions, and metadata reads succeed; denial and unavailable remain distinct | No silent deserialization fallback |
| Retirement | Exit evidence complete before deadline | External v1 gets canonical 404; Projects v2 calls continue | Missing consumer or schedule evidence blocks retirement |
| Expiry or incident | Deadline or disclosure regression | Stop exception; restore verified v1 path | Record metadata-only outcome and seek new decision |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/FoldersServerHostComposition.cs`, `FoldersApiRouting.cs`, `Pd10V2CandidateCompatibilitySeam.cs` -- reuse the three-mode route and canonical denial.
- `tests/Hexalith.Folders.IntegrationTests/EndToEnd/FoldersApiRoutingModeTests.cs` -- composed-host routing coverage.
- `src/Hexalith.Folders.Client/`, `src/Hexalith.Folders.Contracts/`, `tools/release-packages.json`, `.github/workflows/release.yml` -- v2 pair and package lane; generated output remains generator-owned.
- `references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/` -- known calls and outcome mapping; Projects owns its edits.
- `docs/contract/pd10-v2-consumer-discovery.md`, `_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md` -- inventory and entry/exit rules; reconcile the stale reseal note with current readiness.
- `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml`, `_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md` -- candidate digests and technical evidence.
- `_bmad-output/planning-artifacts/planning-story-manifest.yaml` -- refresh only current top-level provenance after checking the accepted planning changes and regenerated candidate. Preserve historical approval/finalization bindings, execution flags, and story lifecycle values.

## Tasks & Acceptance

**Execution:**
- [x] `_bmad-output/planning-artifacts/planning-story-manifest.yaml`, `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` -- reconcile current provenance for the committed McpCli correction, accepted October 7 planning amendment, root-only submodule documentation correction, and changed ACL candidate inputs; independently check all twenty bindings and rerun affected conformance and authorization checks.
- [ ] `references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/`, `references/Hexalith.Projects/tests/Hexalith.Projects.Server.Tests/ProjectFolderDirectoryTests.cs`, `ProjectFileReferenceDirectoryTests.cs` -- inventory calls and schedule; adapt and test the exact v2 client, including 401/404/503 and metadata reads, in the Projects repository.
- [x] `src/Hexalith.Folders.Server/`, server and integration tests -- add bounded version/consumer request attribution, counts, errors, and trace correlation without resource identifiers; prove hold, coexistence, and retirement behavior.
- [ ] `tools/release-packages.json`, `.github/workflows/release.yml`, package tests -- verify Client/Contracts identity and hashes through the existing release lane; record the exact package pair and Projects build artifact.
- [ ] `docs/contract/pd10-v2-consumer-discovery.md`, `_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md`, `docs/runbooks/rollback.md` -- complete discovery, baseline, thresholds, reversal, mutation disposition, UTC schedule, owners, and deadline; correct superseded status.
- [x] `_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md` -- rerun A6b, Section 9, parity, focused and package-profile checks on final bytes; append exact evidence without rewriting history.
- [ ] `_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md`, `_bmad-output/planning-artifacts/planning-story-manifest.yaml`, `_bmad-output/implementation-artifacts/sprint-status.yaml` -- record gated deployment and observation evidence, verify v1 retirement, then obtain the closure decision and authorized tracker delta.

**Acceptance Criteria:**
- Given any failed entry check, when execution is evaluated, then `V1Only` stays active and T0 has not started.
- Given the released package pair, when Projects builds and exercises each inventoried call, then its typed outcomes match the v2 contract and the deployment artifact is identified by exact revision.
- Given the coexistence window, when monitoring runs, then each decision uses attributed traffic, baseline thresholds, and a recorded UTC clock.
- Given complete exit evidence, when v1 is retired, then Projects remains functional on v2 and no deployed v1 consumer remains.
- Given any missing exit evidence or rollback trigger, when the window ends, then the safe v1 path is restored and Story 1.17 remains open pending a new decision.

## Implementation Notes

- **2026-10-08 resumed implementation:** The current-only provenance repair follows the accepted conditional migration and the single-owner policy's changed-input recheck rule. Investigation found the four stale planning inputs reflect the committed September 27 McpCli correction, Jerome's accepted October 7 E1–E14 amendment, and the root-only `--checkout` documentation correction. The candidate inventory also misses three committed ACL file changes. Refreshing these current-input hashes records those changes without modifying historical decisions or granting exposure. This run resumes the already approved migration spec and preserves its frozen scope and original baseline. The spec's explicit tracker restriction takes precedence over generic workflow synchronization; Story 1.17 remains backlog until its transition is authorized.

- This `bmad-build` implementation step prohibits push and remote operations. Publication, deployment, observation, and retirement remain pending until they can run through an authorized execution path.
- Added bounded version/consumer/status metrics and trace tags through the registered Folders meter, composed-host coverage, source call inventory, rollback instructions, and a reproducible 223-artifact candidate manifest. Final-byte Debug routing tests passed 16/16; the Release CI solution build, twelve parity categories, and governance/completeness gate passed. The current Projects checkout remains clean and pinned to v1 packages.
- Projects v2 testing needs an exact released Client/Contracts pair. Its source-profile test build is also blocked by the absent `Hexalith.Conversations.Contracts` sibling project; nested Projects submodules were not initialized under repository policy. Production baselines, attributed traffic, rehearsal, UTC slot/owners, T0, observation, and retirement evidence are absent. The ordinary `Program.cs` production host also requires an EventStore-backed folder repository registration before it can boot; its current composition only registers the in-memory repository in Development or Staging.
- Matrix audit: hold, coexistence, and retired-route behavior ran in `FoldersApiRoutingModeTests` (16/16). Projects migration and expiry/incident rows have no executable end-to-end result while their prerequisites are absent. Story 1.17 and its tracker row remain open; no tracker transition was authorized.
- **2026-09-26 continuation:** The earlier clean Projects state above is superseded by four uncommitted migration-preparation edits. A diagnostic, unpublished Client/Contracts pair from Folders `d4cc07f07b007dcb4ac89f443e779dcdaff8a8f8` built Projects in Release package mode through a temporary central-package override and cached Conversations packages, without initializing nested submodules; focused adapter tests passed 34/34. The tracked Projects `1.0.0` pins still fail their default build with a pre-existing `CS1501` permissions overload mismatch. The current 223-artifact manifest reproduced byte for byte after the edits, and the governance and twelve-category parity gates passed. The production host remains unable to start without the durable EventStore-backed `IFolderRepository` owned by Story 12.1 and pending `EXT-ES-EVENT-EVOLUTION`. The [readiness addendum](story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-09-26-local-projects-package-profile-probe-and-production-host-seam) contains the exact digests and local test commands. The released pair, Projects commit/deployment artifact, production baseline, rehearsal, UTC schedule, observation, retirement, and tracker authorization remain open; `V1Only` remains in force.

- **2026-10-08 continuation:** The existing release lane published Client/Contracts `1.1.1` from `d5f49e96dfab10bf2839ec263a343a6a4c13b06f`. Projects now pins that released pair at committed revision `caf3721427f0b4834415369c2b4a5f981725474f`; its Release/package build and focused adapters pass 39/39, and the identical pre-commit source passed the full Server suite 832/832. Current Folders routing passes 18/18 and its Release CI solution build passes. The final 224-artifact conformance manifest reproduces in two isolated generations at SHA-256 `cae97c1f685cfa89cc3125243648ce462456670a3750aae66ea7b016037721d7`. The published pair is not an exact package of the current full candidate. Parity passes 11/12 categories with four pre-existing golden creation failures; package/NFR/A6b checks pass 40/41 with one pre-existing stable-Dapr override failure; the automated governance gate passes while a direct Section 9 audit finds four stale current provenance bindings. Exact commands, artifact hashes, and failures are appended to the [readiness record](story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-10-08-released-pair-and-projects-consumer-verification). Current A8 technical readiness is not established. Production repository, deployment artifacts, telemetry/census, periodic schedules, reversal, UTC owners/slots, observation, retirement, and closure remain gated. `V1Only` and the Story 1.17 tracker value remain unchanged.

- **2026-10-08 final-byte recheck:** The golden-creation and stable-Dapr failures in the paragraph above are superseded by the committed CI repair and a fresh check on Folders `f0e7154461ff21226851046494b3f2b2fd8a772e`. The candidate inventory was regenerated after `ClientGenerationTests.cs` changed; two generations match at raw SHA-256 `835eee602d0f2f1daf8e22d8aef1138076d296c89c3159ba641977c9981de2de`, and only that current provenance binding was refreshed. Acceptance verification found the checked-in inventory still hashed to `7c0e078ff30d4abace4b853c094a920263566c5bf5fa45d529e4392a0d4d17ad` and wrote the matching generation into it. An uncommitted tracker edit from `backlog` to `in-progress` was restored. Release CI build passes with zero warnings; parity passes 12/12; governance passes 22/22, 7/7, and 3/3; routing passes 18/18; package/NFR/A6b passes 41/41; Projects adapters pass 39/39 against cached Client/Contracts `1.1.1`. The four planning provenance mismatches remain, so current A8 technical readiness is still not established. The released pair is still not this candidate. Production repository, deployment artifacts, telemetry/census, periodic schedules, reversal, UTC owners/slots, observation, retirement, and closure remain gated. `V1Only` stays active and T0 has not started. Exact commands are in the [final-byte readiness addendum](story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-10-08-final-byte-recheck-and-hold).

- **2026-10-09 published-pair continuation:** Release run `37932766815` published Client/Contracts `1.2.1` from exact Folders `17b5a70d2b4b91bbbf73ea63c8a214a0ad426759`. The Projects-owned central pin was updated locally to `1.2.1`; a fresh isolated NuGet.org restore, zero-warning Release/package build, and 39/39 focused adapter tests pass. The [readiness addendum](story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-10-09-exact-121-publication-and-projects-package-check) records both archive identities and remaining entry gaps. Projects has no new commit, CI, image, or deployment identity, and production prerequisites remain unverified. Keep `V1Only`, T0 unset, and the tracker unchanged.

## Spec Change Log

- **2026-10-09 published-pair check:** Recorded exact `v1.2.1` release identity and signed NuGet package hashes, updated the Projects-owned central pin in its working tree, and verified the released pair with an isolated NuGet.org restore, Release builds, and 39/39 adapter tests. Production entry, deployment, observation, retirement, and closure remain open.
- **2026-10-08 current-input reconciliation:** Refreshed only five current provenance digests after checking the committed planning changes and resealing the 224-artifact candidate, including the supplied Platform evidence in discovery. Independent input audits pass 20/20 bindings, all candidate bytes, historical decisions, lifecycle rows, frozen specifications, and the graph. Release/package builds pass, parity passes 12/12, package/NFR/A6b passes 41/41, routing passes 18/18, final ACL reauthorization passes 4/4, and Projects adapters pass 39/39. Full Contracts, after an offline Python-prerequisite retry, passes 345/346; governance fails 21/22 on the separately committed OQ4 provider-catalog drift. Current A8 technical readiness remains unmet. Platform's October 4 census is dated single-cluster evidence with no Folders/Projects artifact/traffic/schedule tuple; its accepted MVP infrastructure envelope adds no stricter Folders enrollment gate. Exact evidence and the unchanged entry hold are in the [current provenance readiness addendum](story-1-17-current-candidate-technical-readiness-2026-09-24.md#addendum-2026-10-08-current-provenance-reconciliation-and-entry-hold). Historical approval/control bytes, `V1Only`, T0, and Story 1.17's tracker value remain unchanged; publication, deployment, observation, retirement, and closure remain incomplete.

## Review Triage Log

- **2026-10-08 implementation acceptance audit:** Reviewed the Projects committed migration delta and the Folders migration-evidence diff. Independently verified all 224 candidate hashes and preservation of historical v1, the authorization matrix, approval register, and tracker. Concurrent Builds/Tenants changes required a fresh dependency-bound Folders build and checks; their exact revisions and results are appended to the readiness record. Local adapter and routing evidence passes, but production migration/expiry matrix rows remain unexecuted and the entry gates fail. The implementation remains `in-progress`; the workflow's review/closure step cannot begin while these tasks and acceptance criteria are incomplete. The separate concurrent CI repair spec and other user changes were preserved.

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1` -- package-profile build passes.
- `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` -- all twelve categories pass.
- `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- current-input provenance passes.
- Run the affected Folders and Projects test projects individually, then reproduce the conformance manifest twice to exact bytes.
