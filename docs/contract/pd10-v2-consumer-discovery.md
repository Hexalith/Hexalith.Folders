# PD10 v2 consumer discovery evidence

Recorded: 2026-09-24

Status: **v2 cutover held.** A deployed consumer outside the Folders repository uses the published v1 generated client. The owner accepted plan B, a bounded seven-day coexistence window for that consumer, but its entry checks have not passed. The branch now has a v2 route that can be switched on by configuration. No routing-mode change is deployed. This discovery does not authorize v2 exposure, v1 retirement, or Story 1.17 closure.

## Checks

| Check | Observed result |
| --- | --- |
| [Folders deployments](https://github.com/Hexalith/Hexalith.Folders/deployments) | GitHub deployment IDs `6546802692` and `6546836679` for `production` on 2026-09-19 both reached `success`; the later deployment selected commit `823dce16bca7d1eb9c10c4d871bf581fc9dc61ef`. |
| [Folders v1.0.0 release](https://github.com/Hexalith/Hexalith.Folders/releases/tag/v1.0.0) and [NuGet package index](https://api.nuget.org/v3-flatcontainer/hexalith.folders.client/index.json) | The release was published on 2026-09-19; NuGet lists `Hexalith.Folders.Client` version `1.0.0`. |
| [Projects deployments](https://github.com/Hexalith/Hexalith.Projects/deployments) | Deployment ID `6547910236` for `production` selected commit `c767d38d8ae76f9ad949965ac4870a842c699f9c` and reached `success` on 2026-09-20. |
| [Projects package pin](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/Directory.Packages.props) | The deployed commit pins `Hexalith.Folders.Client` and `Hexalith.Folders.Contracts` to `1.0.0`. |
| [Projects Folders client use](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/src/Hexalith.Projects.Server/Folders/FoldersProjectFolderDirectory.cs) and [registration](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/src/Hexalith.Projects.Server/ProjectsServerServiceCollectionExtensions.cs) | The deployed source uses `Hexalith.Folders.Client.Generated.IClient` for folder lifecycle and effective-permissions reads and registers it against the `folders` service. |
| GitHub organization code search for `"/api/v1/folders"` | Matches in `Hexalith.Projects` are historical documents, while the deployed source selects v1 indirectly through the pinned generated package. A missing route literal therefore does not clear the consumer. |

The 2026-09-19 observation of no package or deployment is superseded by the subsequent successful deployments and package publication. `Hexalith.Projects` is a first-party repository but an external deployed consumer of the Folders service. Its v1 client must be migrated through the owner-accepted, consumer-specific window under the [project decision policy](../governance/approval-policy.md) before any production v2 cutover. The branch candidate remains unexposed.

## Route preparation under plan B

The [accepted migration package](../../_bmad-output/implementation-artifacts/story-1-17-projects-v1-to-v2-migration-approval-package-2026-09-24.md) chooses plan B: bounded coexistence for the deployed Hexalith.Projects consumer. Plan C, continue the hold, stays the executable state until B's entry checks pass. On branch `feat/story-1-17-v2-closure-prep`, the server host reads one validated setting, `Folders:ApiRouting:Mode`:

| Mode | Routes | Plan B use |
| --- | --- | --- |
| `V1Only` (default when the setting is absent) | v1 only. The pipeline is the same as before this change, and `/api/v2` has no route. | The hold (plan C) and the rollback target. |
| `Coexistence` | v1, plus v2 through the PD10 candidate compatibility seam. | The seven-day window, after every entry check passes. |
| `V2Only` | v2 through the seam. Every external `/api/v1` request, in any letter case, gets the canonical non-enumerating 404 before authorization, lookup, or effect. The seam's own in-process v1 dispatch, marked by the seam and not by a client header, keeps working. | v1 retirement, after every exit check passes. |

The host reads the mode once at startup, so every mode change needs a restart or redeploy. That includes a rollback or an incident "disable v2 exposure" step, and the rollback rehearsal should time it. An empty or unknown value stops host startup. `FoldersApiRoutingModeTests` boots the real host composition in each mode and covers the startup rejection. These tests prove branch behavior only. No production host has been deployed with this change, no mode has been switched, no T0 has been recorded, and the seven-day clock has not started. The v1 retirement still needs every exit check in the migration package, including evidence that no other v1 consumer is deployed.
