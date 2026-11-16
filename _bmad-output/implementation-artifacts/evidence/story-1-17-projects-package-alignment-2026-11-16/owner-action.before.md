# Story 1.17 Projects host artifact owner next action

Status: **HOLD — fixture repaired locally; product tuple and host production unresolved.** This is a prepared owner action, not an approval, release or deployment record. Projects source is `bf9f98969efa036fd60700c627e712b4375e654a` plus the uncommitted fixture diff. There is no new committed candidate identity or retained host artifact.

## Verified local state

The release fixture in `tests/tools/test_release_package_tools.py` now uses `Hexalith.EventStore.Contracts` from the authoritative `tools/release-packages.json`, asserts that it is external to the five-package inventory and checks normalization preserves its version when equal to the Projects candidate. The complete fixture suite passes **20/20**. Production contracts, package manifest, central versions, validators and CI workflows are unchanged.

Using Projects' declared Builds tree `801f268cd1b37ecae41d4fb20d115c055b4c5793`, local Release/package-mode tests passed: Contracts 207, Client 125, core 691, UI 277, Mcp 35 and Cli 22; **1,357 passed, zero skipped**. These diagnostic checks used private disposable source snapshots with the pinned global.json, warnings as errors and unchanged dependency audit policy. They are not exact committed-source CI, G-6 runtime qualification, Server/Integration acceptance or hosted evidence.

Two current product blockers remain:

| Blocker | Observed result | Accountable next action |
| --- | --- | --- |
| Host dependency tuple | Solution restore and focused Server.Tests build fail `NU1109`: signed Folders Client/Contracts 2.0.0 require `Hexalith.McpCli.Abstractions >= 3.0.0`, but Projects centrally selects 2.0.1 with transitive pinning enabled. | Projects maintainer with McpCli/package owner must select and compatibility-qualify a coherent package tuple retaining the published Folders pair. Review the major-version adoption and Projects Contracts decorations/consumers before changing the central pin and corresponding manifest entries together. No dependency override, downgrade suppression, dependency removal or Folders republishing is an acceptable substitute. |
| Release graph drift | All five disposable candidates packed, but strict manifest validation failed: `Hexalith.Projects` has `wrong_versions=['Hexalith.EventStore.Contracts']`. The current manifest records 5.1.0 while `Directory.Packages.props` selects EventStore 5.1.1. | Projects package owner with EventStore/Builds maintainer must reconcile the intended dependency graph and manifest against actual pack output, then pass strict validation and five real API consumer compilations. Do not change the validator to accept drift. |

The available umbrella Builds checkout is `65fd22f37b9fad64b595841923cdbe050563ab99`, distinct from Projects' declared pin above and CI's reusable execution pin `6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b`. `run-with-temp python3 ../Hexalith.Builds/check.py` from Projects refuses the absent separate nested Builds checkout before product checks. No nested submodule was initialized. Run the committed-input checker in the owner-supported clean checkout when landing is separately authorized; these local results do not bypass it.

## Exact missing owner inputs

1. **Projects release/deployment maintainer, coordinating with McpCli/EventStore/Builds:** resolve both package blockers above, then obtain passing current CI and package/consumer checks on the exact approved source. The fixture repair is uncommitted by instruction; after separate landing authorization, retain the full resulting Projects commit and all selected dependency/catalog/tool identities. The old failed run cannot qualify the new source.
2. **Projects release/deployment maintainer, with CiCd for any workflow change:** name the reviewed Server host-artifact production lane, immutable retention destination, target platform/RID, base image digest and exact candidate configuration/manifests. Existing `.github/workflows/release.yml` and `release.config.cjs` produce five NuGet libraries. `src/Hexalith.Projects.Server/Hexalith.Projects.Server.csproj` is publishable and non-packable; it declares neither `EnableContainer` nor `ContainerRepository`. No host-image production action was identified in the inspected Projects source/tools/docs/workflows. Do not dispatch the NuGet Release workflow to fill the host gap. Use the owner's approved .NET SDK container lane after those concrete inputs exist; no Dockerfile or replacement pipeline is proposed here.
3. **Projects owner and Platform maintainer `@jpiquot`, with identity/data owners:** complete the existing Platform enrollment procedure, `Hexalith.Platform/docs/domain-module-enrollment.md` section 5, and keep `module/platform-handoff.md` pending until reviewed. At inspected Platform `5691d313ab20bf7ad40243e40a80b6a58ed04650`, `PlatformServerBindings.ReferenceProjects` and `.ReferenceImages` lack `projects/domain`. Record the actual binding to `src/Hexalith.Projects.Server/Hexalith.Projects.Server.csproj`, accepted image/catalog provenance, server-scoped configuration/token adapters, exact tenant/domain/version/appId/method routing, workload/owner authorization and Dapr policies. Supply both fixed state components `statestore` and `projects-conversation-start-setup`; changing the configurable state option does not redirect them. Fill separate nonempty smoke surfaces and every required critical-flow E2E mapping. Record the reviewed direct Platform revision; a declaration alone does not register a host.

## Artifact receipt the owner lane must return

Return a retained content-addressed OCI image/archive and a provenance receipt binding all of these together:

- Full committed Projects source revision and exact dependency, SDK, build-tool/catalog and build recipe identities; passing current product/consumer CI references.
- Immutable registry image digest, archive digest/retention coordinate, target platform/RID and base image digest. No image name or mutable tag alone qualifies.
- The exact retained signed Folders pair below, both its archive and DLL identities, resolved NuGet graph and the matching actual DLLs in the built host/image. A package version or feed restore alone does not prove payload identity.
- Candidate module declaration, deployment manifests and configuration/profile digests, with protected secret references and identity/grant enrollment references. No secret values belong in this receipt. Missing configuration selection remains pending.

| Folders package 2.0.0 | Signed archive SHA-256 | `lib/net11.0` DLL SHA-256 |
| --- | --- | --- |
| Hexalith.Folders.Contracts | `6a55c386a561dee8785bea2c657a45a6464e067532615461635a0200e447ae1f` | `15967866af43db82ed4d6a237d5454c90bae6192df83be5cbadd25b2864a9098` |
| Hexalith.Folders.Client | `1af188709de4978bbc3484fdf0eccce5d3a0bf93db23521f567684fb500f6af8` | `b9b4a8eff8881d89741d8072c53ae76cf6999fef417ffaced7664dfaa11e4b65` |

Both packages identify Folders source `209dacbdb42fcde728d9e190781b69945bccc93b`. Their retained archive/payload bytes were matched to the supplied closure handoff; publication and signature verification are historical completed evidence, not repeated work here.

After tuple repair, rerun `run-with-temp python3 -m unittest tests/tools/test_release_package_tools.py -v`, the package-only Release solution build, individual test projects, and `tests/tools/run-package-dependency-gate.ps1` through the clean owner lane. That package gate must pass exact metadata, provenance, graph, assets and all five real API consumers. Run applicable generated-artifact and current G-6 checks against the explicit current policy `tools/qualification/g6-current-policy-2026-11-12-dapr-net1191.json`; clean-source runtime and real managed AppHost E2E remain pending until qualified owner prerequisites exist.

Actual served image/configuration, loaded DLLs, deployment receipts, target/consumer/data disposition, hosted grants/schedules and recovery outcomes belong to later qualified, locked staging/production attempts. Acquire each environment's lock/epoch before its mutations. Only the latest issued serialized full-pass E2E attempt qualifies; a newer queued/running/non-passing attempt blocks fallback. Keep mutating v2 callers blocked until accountable completed-write reconciliation/disposition is resolved.

DEC-A8-HOLD is satisfied; Story 11.6 implementation and verification are complete. Preserve `V1Only`, unset T0, null activation authority, all fourteen unexecuted staging checks and unchanged tracking. This preparation performs no commit, push, publication, deployment or activation.

Evidence is retained in the Folders workspace at `_bmad-output/implementation-artifacts/evidence/story-1-17-projects-ci-repair-2026-11-16/`: `product-results.json`, `package-results.json`, `folders-payload-binding.json`, exact logs and the fixture diff. This path is an umbrella-workspace coordinate, not a Projects-owned release artifact.
