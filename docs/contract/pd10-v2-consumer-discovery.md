# PD10 v2 consumer discovery evidence

Recorded: 2026-09-24

Status: **v2 cutover blocked.** A deployed consumer outside the Folders repository uses the published v1 generated client. This discovery does not authorize a migration, v2 exposure, or Story 1.17 closure.

## Checks

| Check | Observed result |
| --- | --- |
| [Folders deployments](https://github.com/Hexalith/Hexalith.Folders/deployments) | GitHub deployment IDs `6546802692` and `6546836679` for `production` on 2026-09-19 both reached `success`; the later deployment selected commit `823dce16bca7d1eb9c10c4d871bf581fc9dc61ef`. |
| [Folders v1.0.0 release](https://github.com/Hexalith/Hexalith.Folders/releases/tag/v1.0.0) and [NuGet package index](https://api.nuget.org/v3-flatcontainer/hexalith.folders.client/index.json) | The release was published on 2026-09-19; NuGet lists `Hexalith.Folders.Client` version `1.0.0`. |
| [Projects deployments](https://github.com/Hexalith/Hexalith.Projects/deployments) | Deployment ID `6547910236` for `production` selected commit `c767d38d8ae76f9ad949965ac4870a842c699f9c` and reached `success` on 2026-09-20. |
| [Projects package pin](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/Directory.Packages.props) | The deployed commit pins `Hexalith.Folders.Client` and `Hexalith.Folders.Contracts` to `1.0.0`. |
| [Projects Folders client use](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/src/Hexalith.Projects.Server/Folders/FoldersProjectFolderDirectory.cs) and [registration](https://github.com/Hexalith/Hexalith.Projects/blob/c767d38d8ae76f9ad949965ac4870a842c699f9c/src/Hexalith.Projects.Server/ProjectsServerServiceCollectionExtensions.cs) | The deployed source uses `Hexalith.Folders.Client.Generated.IClient` for folder lifecycle and effective-permissions reads and registers it against the `folders` service. |
| GitHub organization code search for `"/api/v1/folders"` | Matches in `Hexalith.Projects` are historical documents, while the deployed source selects v1 indirectly through the pinned generated package. A missing route literal therefore does not clear the consumer. |

The 2026-09-19 observation of no package or deployment is superseded by the subsequent successful deployments and package publication. `Hexalith.Projects` is a first-party repository but an external deployed consumer of the Folders service. Its v1 client must be migrated through a consumer-specific window approved by Product, Security, and Architecture before any production v2 cutover. The branch candidate remains unexposed.
