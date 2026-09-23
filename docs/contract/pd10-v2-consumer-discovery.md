# PD10 v2 consumer discovery evidence

Recorded: 2026-09-24 (Europe/Paris)

Status: no deployed external v1 consumer was identified by the checks below. This is bounded discovery evidence, not proof that no private runtime exists. It does not authorize production routing, A6b, Section 9, A8, a migration window, or Story 1.17 closure. This document is a bound candidate artifact; its changed bytes require the planned T11 reseal.

## T9 discovery commands

Each time is on 2026-09-24 in Europe/Paris. Exit code 1 from `git grep` means no match.

| Time | Command and working directory | Result |
| --- | --- | --- |
| 00:01:56 | `gh search code '"/api/v1/folders"' --owner Hexalith` (Folders lane) | Exit 0; matches only in `Hexalith/Hexalith.Folders` source, tests, and this prior evidence document. |
| 00:02:01 | `gh search code '"Hexalith.Folders.Client"' --owner Hexalith` (Folders lane) | Exit 0; `Hexalith.Projects` declares and references client `1.0.0`; `Hexalith.Builds` has a shared package-version entry. Other matches are in Folders. |
| 00:02:06 | `gh api repos/Hexalith/Hexalith.Folders/deployments` (Folders lane) | Exit 0; two `production` records, IDs `6546836679` and `6546802692`, both for SHA `823dce16bca7d1eb9c10c4d871bf581fc9dc61ef`. |
| 00:02:10 | `gh release list -R Hexalith/Hexalith.Folders` (Folders lane) | Exit 0; release `v1.0.0`, published 2026-09-19T23:23:32Z. |
| 00:02:15 | `curl -s 'https://azuresearch-usnc.nuget.org/query?q=packageid:Hexalith.Folders.Client&prerelease=true'` (Folders lane) | Exit 0; `totalHits: 1`, client `1.0.0`, 101 downloads at query time. Downloads do not identify a consumer. |
| 00:02:22 | `git grep -n "/api/v1/folders"` (`references/Hexalith.AI.Tools`) | Exit 1; no match. |
| 00:02:22 | `git grep -n "/api/v1/folders"` (`references/Hexalith.Builds`) | Exit 1; no match. |
| 00:02:22 | `git grep -n "/api/v1/folders"` (`references/Hexalith.Commons`) | Exit 1; no match. |
| 00:02:22 | `git grep -n "/api/v1/folders"` (`references/Hexalith.EventStore`) | Exit 1; no match. |
| 00:02:22 | `git grep -n "/api/v1/folders"` (`references/Hexalith.FrontComposer`) | Exit 1; no match. |
| 00:02:22 | `git grep -n "/api/v1/folders"` (`references/Hexalith.Memories`) | Exit 1; no match. |
| 00:02:23 | `git grep -n "/api/v1/folders"` (`references/Hexalith.PolymorphicSerializations`) | Exit 1; no match. |
| 00:02:23 | `git grep -n "/api/v1/folders"` (`references/Hexalith.Tenants`) | Exit 1; no match. |
| 00:02:27 | `git grep -n "/api/v1/folders"` (local `Hexalith.Projects` checkout) | Exit 0; matches in historical planning/implementation documents and three routes in `tests/Hexalith.Projects.E2E.Fixtures/Program.cs`. No match in `src/`. |

## Classification of outside hits and release evidence

- The Projects v1 literals are historical documentation or test-only fixture routes. The fixture project declares `IsPackable=false` and `IsPublishable=false`. `git grep -n 'api/v1/folders' c767d38d8ae76f9ad949965ac4870a842c699f9c -- src` returned exit 1 against the Projects release source. The local Projects checkout had two unpushed commits and an existing modified Builds submodule; the release-source check avoids treating that checkout as deployed source.
- Projects' released server source references `Hexalith.Folders.Client` `1.0.0` and calls generated folder operations. The published Folders client assembly contains three UTF-16LE `api/v2/folders` route markers and zero `api/v1/folders` route markers; the Folders release source also selects v2 for the examined operations. A package version of `1.0.0` does not imply a v1 route selection.
- The [Folders deployment statuses](https://api.github.com/repos/Hexalith/Hexalith.Folders/deployments/6546836679/statuses) and [release run](https://github.com/Hexalith/Hexalith.Folders/actions/runs/35475836727) identify package-release jobs with no runtime environment URL. The release workflow at that SHA has `publish-containers: false`. [Folders v1.0.0](https://github.com/Hexalith/Hexalith.Folders/releases/tag/v1.0.0) and the [NuGet client package](https://www.nuget.org/packages/Hexalith.Folders.Client/1.0.0) establish publication, not runtime consumption.
- The [Projects deployment statuses](https://api.github.com/repos/Hexalith/Hexalith.Projects/deployments/6547910236/statuses) point to a [package-release run](https://github.com/Hexalith/Hexalith.Projects/actions/runs/35482414271) with no runtime environment URL. Its workflow has `publish-containers: false`, and the [Projects v1.0.0 release](https://github.com/Hexalith/Hexalith.Projects/releases/tag/v1.0.0) contains packages but no server artifact. These records do not establish a running Projects server.

No examined external runtime source or configuration selects a Folders `/api/v1` route. Projects' own `/api/v1/projects` routes are unrelated. The checks cannot identify private deployments or callers. If a Projects server is deployed elsewhere, its exact deployed binary and Folders endpoint remain unverified; this discovery makes no compatibility claim about that runtime.

## Escalation rule

Classify and record every hit outside `Hexalith.Folders`. Historical documentation and test-only fixtures do not themselves establish a consumer. Stop and escalate if external runtime code or configuration selects a Folders `/api/v1` route, a deployed external v1 consumer is found, or a potential runtime hit cannot be classified. Package publication, downloads, and release-environment deployment records are exposure signals to record, not independent proof of runtime consumption. Never invent a migration window.
