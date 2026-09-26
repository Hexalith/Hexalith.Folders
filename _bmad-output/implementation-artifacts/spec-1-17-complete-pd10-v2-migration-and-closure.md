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

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.Projects/src/Hexalith.Projects.Server/Folders/`, `references/Hexalith.Projects/tests/Hexalith.Projects.Server.Tests/ProjectFolderDirectoryTests.cs`, `ProjectFileReferenceDirectoryTests.cs` -- inventory calls and schedule; adapt and test the exact v2 client, including 401/404/503 and metadata reads, in the Projects repository.
- [x] `src/Hexalith.Folders.Server/`, server and integration tests -- add bounded version/consumer request attribution, counts, errors, and trace correlation without resource identifiers; prove hold, coexistence, and retirement behavior.
- [ ] `tools/release-packages.json`, `.github/workflows/release.yml`, package tests -- verify Client/Contracts identity and hashes through the existing release lane; record the exact package pair and Projects build artifact.
- [ ] `docs/contract/pd10-v2-consumer-discovery.md`, `_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md`, `docs/runbooks/rollback.md` -- complete discovery, baseline, thresholds, reversal, mutation disposition, UTC schedule, owners, and deadline; correct superseded status.
- [ ] `_bmad-output/implementation-artifacts/story-1-17-current-candidate-technical-readiness-2026-09-24.md` -- rerun A6b, Section 9, parity, focused and package-profile checks on final bytes; append exact evidence without rewriting history.
- [ ] `_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md`, `_bmad-output/planning-artifacts/planning-story-manifest.yaml`, `_bmad-output/implementation-artifacts/sprint-status.yaml` -- record gated deployment and observation evidence, verify v1 retirement, then obtain the closure decision and authorized tracker delta.

**Acceptance Criteria:**
- Given any failed entry check, when execution is evaluated, then `V1Only` stays active and T0 has not started.
- Given the released package pair, when Projects builds and exercises each inventoried call, then its typed outcomes match the v2 contract and the deployment artifact is identified by exact revision.
- Given the coexistence window, when monitoring runs, then each decision uses attributed traffic, baseline thresholds, and a recorded UTC clock.
- Given complete exit evidence, when v1 is retired, then Projects remains functional on v2 and no deployed v1 consumer remains.
- Given any missing exit evidence or rollback trigger, when the window ends, then the safe v1 path is restored and Story 1.17 remains open pending a new decision.

## Implementation Notes

- This `bmad-build` implementation step prohibits push and remote operations. Publication, deployment, observation, and retirement remain pending until they can run through an authorized execution path.
- Added bounded version/consumer/status metrics and trace tags through the registered Folders meter, composed-host coverage, source call inventory, rollback instructions, and a reproducible 223-artifact candidate manifest. Final-byte Debug routing tests passed 16/16; the Release CI solution build, twelve parity categories, and governance/completeness gate passed. The current Projects checkout remains clean and pinned to v1 packages.
- Projects v2 testing needs an exact released Client/Contracts pair. Its source-profile test build is also blocked by the absent `Hexalith.Conversations.Contracts` sibling project; nested Projects submodules were not initialized under repository policy. Production baselines, attributed traffic, rehearsal, UTC slot/owners, T0, observation, and retirement evidence are absent. The ordinary `Program.cs` production host also requires an EventStore-backed folder repository registration before it can boot; its current composition only registers the in-memory repository in Development or Staging.
- Matrix audit: hold, coexistence, and retired-route behavior ran in `FoldersApiRoutingModeTests` (16/16). Projects migration and expiry/incident rows have no executable end-to-end result while their prerequisites are absent. Story 1.17 and its tracker row remain open; no tracker transition was authorized.

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseNuGetDeps=true -warnaserror -m:1` -- package-profile build passes.
- `pwsh tests/tools/run-contract-parity-ci-gates.ps1 -NoRestore` -- all twelve categories pass.
- `pwsh tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- current-input provenance passes.
- Run the affected Folders and Projects test projects individually, then reproduce the conformance manifest twice to exact bytes.
