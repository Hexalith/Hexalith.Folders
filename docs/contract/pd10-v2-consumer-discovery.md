# PD10 v2 consumer discovery evidence

Recorded: 2026-09-19

Status: no deployed external v1 consumer discovered; candidate generation may continue. This is discovery evidence only and does not authorize production routing, A6b, Section 9, or A8.

## Checks

| Check | Result |
| --- | --- |
| GitHub organization code search for `"/api/v1/folders"` | Matches occur only in `Hexalith/Hexalith.Folders` source/tests. No other Hexalith repository contains the v1 route. |
| GitHub organization code search for `"Hexalith.Folders.Client"` | `Hexalith/Hexalith.Projects` declares a centrally managed `1.0.0` package reference; no v1 route literal was found there. `Hexalith.Builds` carries only the shared package-version catalog. |
| GitHub deployments for `Hexalith/Hexalith.Folders` | The deployments API returned an empty array. |
| NuGet.org query `packageid:Hexalith.Folders.Client`, including prerelease | `totalHits: 0`; no client package is available for a deployed consumer to restore. |

`Hexalith.Projects` is recorded as a first-party declared consumer that must compile against the published v2-generated client after release. It is not evidence of an external deployed v1 client because the package is unpublished, its repository contains no v1 route selection, and the Folders repository has no recorded deployment.

## Escalation rule

Any later package-feed hit, deployment record, or repository outside `Hexalith.Folders` that selects `/api/v1` invalidates this evidence and blocks release until Product, Security, and Architecture approve a consumer-specific migration window.
