# Story 1.17 — current Builds source-host qualification

**The rebuilt Debug source host and matching Platform model satisfied both fixed EventStore 3.128 loader cases. Cold and host-first each ran once and exited 0 on .NET 10.0.13. This supplies binding evidence only; package, API, ordinary-workspace and hosted acceptance remain open.**

The [completed execution receipt](evidence/story-1-17-current-builds-host-2026-10-26-attempt-5.json) binds the [final input plan](../../eng/story117-current-builds-host/plan-attempt-5.json), actual outer commands/environments, compiler inputs, restored assets, generated dependency/runtime files, outputs, host traces, assembly events and preservation observations. All three Debug builds passed: AppHost, CLI and Platform Composition. No Release build or pack ran. The [release-owner handoff](evidence/story-1-17-current-builds-host-2026-10-26.handoff.json) retains machine-readable identities and remaining prerequisites.

## Exact source tuple and observed binding

Builds remains `74932ad0247203797274aba8b7b6366a4bbf92b9`; Platform remains `9869094acc21f9054960a573f884c60ddf3235cd`; EventStore remains `c5d1c280d8c1c1a207cd159d817f614e93c93130`. All thirteen complete revisions/indices and actual recording statuses remain in the receipt. Current Builds already selected EventStore 3.128.0; no dependency pin changed.

The sealed AppHost DLL is **100,352 bytes**, SHA-256 `5553a7deefe9fb8464b259d12e3d7b84980c722233ea2d7806391c30c44ff187`. The rebuilt Platform model is **358,912 bytes**, SHA-256 `76252a429c8707f2641472b95a05d998f97566d3eb91e12e558e8a9e0c7a087d`. Generated AppHost deps/runtimeconfig stayed unchanged through the cases. Host and model Tooling copies match exactly at SHA-256 `c97a79248415731cef6cfac4b8e82902e4554ffe8c928d4cce6c2488708af84b`.

Both cases observed these Default-context bindings; paths are relative to `/tmp/story117-current-builds-host-dkkg4fqa/sealed/`:

| Assembly and version | Actual path | SHA-256 | MVID |
| --- | --- | --- | --- |
| `Aspire.Hosting` 13.6.3.0 | `host/Aspire.Hosting.dll` | `86507650b7d1ecb6765f5cf46092327a53635bba1eef5e98c4207844ff31cd3b` | `4904eacd-5621-4e18-99a8-232ca027f88e` |
| `Hexalith.Builds.Tooling` 1.0.0.0 | `host/Hexalith.Builds.Tooling.dll` | `c97a79248415731cef6cfac4b8e82902e4554ffe8c928d4cce6c2488708af84b` | `4819a445-b9ce-4ebd-9c1a-31f131e4be98` |
| `Hexalith.Platform.Composition` 1.0.0.0 | `model/Hexalith.Platform.Composition.dll` | `76252a429c8707f2641472b95a05d998f97566d3eb91e12e558e8a9e0c7a087d` | `5fac6660-019c-4ddf-b1b3-9a514b8ca963` |
| `StackExchange.Redis` 3.0.0.0 | `host/StackExchange.Redis.dll` | `e14460fe50cd699b34c75b9a8b10cdb79c2ed68f4088879afa2e073b7afba502` | `cca8a025-8f2a-4628-aa78-3a9874ac6164` |
| `Hexalith.EventStore.ServiceDefaults` 3.128.0.0 | `model/Hexalith.EventStore.ServiceDefaults.dll` | `2186cac45fc92be37d832d9a71643e7a96c12fc183fd5c5d9923bed85c4ad0e7` | `ddf33779-8801-4507-b306-de186defbbe2` |
| `Hexalith.EventStore.Aspire` 3.128.0.0 | `host/Hexalith.EventStore.Aspire.dll` | `448004459386060a871661ffb2df223a0e48ac0cda06edd8427520ca02406bbb` | `9bce6e81-73f8-4978-b1bd-b9d3188ecfb3` |

The probe verified the exact public static `void ComposeRun(IDistributedApplicationBuilder, CompositionRunPlan, string, string)` metadata, token `100663798`. It invoked no target member or constructor and did not invoke ComposeRun or an application entrypoint. Assembly loading can trigger implicit initialization, which is not excluded by these observations.

ServiceDefaults was an explicitly labelled optional demand. Redis was a labelled transitive demand: the model has no direct Redis AssemblyRef, and its absent runtime copy caused the probe to demand the sealed host Redis identity. No Redis member, connection, API behavior or real command/JIT demand order was exercised. Observed Tooling/model assembly versions are 1.0.0.0; these source binaries do not qualify a published CLI version.

The AppHost build retained four MSB3277 conflict warnings involving StackExchange.Redis, Microsoft.OpenApi, Microsoft.IdentityModel.Protocols.OpenIdConnect and Microsoft.IdentityModel.Tokens. CLI and Platform model builds reported zero warnings/errors. No warning was suppressed or repaired by this work; identity success does not establish conflicting-member compatibility.

## Preparation failures retained before the completed run

Each failed attempt stopped, retained its first result and used a new additive plan/claim only after the parent implementer directed a preparation correction within the approved conditional sequence. No historical approval was reused and no loader case ran before attempt 5.

| Attempt | Exact stop | Narrow correction before the next attempt |
| --- | --- | --- |
| [1](evidence/story-1-17-current-builds-host-2026-10-26.json) | Exit 2, `Duplicate offline package filename`, before any SDK use. Versionless cached dotnet-tool archive basenames collided. | Preserve package ID/version identities and reject conflicting bytes. The original plan and all five original harness files are retained in the [14-member supporting capsule](evidence/story-1-17-current-builds-host-2026-10-26.supporting.json). [All duplicate input paths/sizes/hashes](evidence/story-1-17-current-builds-host-2026-10-26.diagnosis.json) remain recorded. |
| [2](evidence/story-1-17-current-builds-host-2026-10-26-attempt-2.json) | SDK 10.0.402 passed; AppHost evaluation failed MSB4236. Aspire.AppHost.Sdk 13.6.3 bytes were present, but the incomplete hierarchical feed was undiscoverable. | Use supported flat canonical `ID.version.nupkg` names, with discovery/collision guards. The exact present SDK package is retained in a [diagnostic capsule](evidence/story-1-17-current-builds-host-2026-10-26-attempt-2.diagnostic-inputs.json). This was a feed-format defect, not a missing external package. |
| [3](evidence/story-1-17-current-builds-host-2026-10-26-attempt-3.json) | Host evaluations passed; first restore exited 1. Thirteen worker reports show `SocketException (13)` in named-pipe/out-of-process-node startup; fourteen crash files are [retained](evidence/story-1-17-current-builds-host-2026-10-26-attempt-3.diagnostic-inputs.json). | Enforce `-m:1`, `-nr:false`, `RestoreBuildInParallel=false`, `BuildInParallel=false`, retaining socket denial. Omit exactly twenty tracked historical `.artifacts/ui-test-obj` generated files from fresh snapshots while preserving original hashes. |
| [4](evidence/story-1-17-current-builds-host-2026-10-26-attempt-4.json) | Restore/source assets passed; AppHost build exited 1: NU1101 from catalog helper metadata acquisition, propagated as MSB3073/MSB4181. | Seed only the verified existing selected nuspec into the private cache; run GeneratePlatformVersionCatalog unchanged. No Hexalith nupkg/DLL entered the feed or compiled package graph. |

The selected cached EventStore.Aspire 3.128.0 nupkg hash is `522e68bda69b2881417db790fd7938a30e7fa1f0935e1c14e47f675a69c89bda`; its nuspec hash is `36d376bab8683eaf72702f4df328b3e4ae839c5b0e06892bb6ae6fd9ab5a06bc`. The nupkg nuspec member equals the cached nuspec bytes and declares repository commit `c5d1c280d8c1c1a207cd159d817f614e93c93130`, matching the bound source. This is package-declared metadata provenance, not proof of full payload/source equivalence. Only the 3,769-byte nuspec was seeded; the private Hexalith package-cache directory contained no nupkg or DLL. GeneratePlatformVersionCatalog validated the selected 3.128.0 / Dapr-hosting 13.6.1-beta.935 catalog/range normally.

The final plan also records exactly two Commons instruction-link omissions; targets were never followed. Actual build-input symlinks continue to fail closed. No operational source/configuration, dependency pin, installed tool or isolation filter changed. The [final guard receipt](evidence/story-1-17-current-builds-host-2026-10-26-attempt-5.guards.json) retains **31/31 passing guards normally and 31/31 under Python optimization**, including the fixed two-case sequence, package fallback, drift/staleness, metadata-only input, serial restore, descriptor reuse, timeout and corruption checks.

## Evidence, verification and preservation

The completed archive contains **17,208** unique regular-file members and is **144,962,765 bytes**. It retains complete executed harness/plan bytes, source snapshots, compiler/reference captures, assets, dependency files, Debug outputs, exact probe observations and host traces. Private third-party feed/extraction/cache contents and certificate stores are omitted; their input bindings and the actual seeded nuspec are retained. No cleanup ran; all fresh attempt directories remain available.

Normal and optimized read-only verification passed. From Folders root:

```bash
python3 -I -B eng/story117-current-builds-host/verify.py \
  _bmad-output/implementation-artifacts/evidence \
  --receipt story-1-17-current-builds-host-2026-10-26-attempt-5.json \
  --receipt-sha256 c19f4a4df533d6aec2114686e3950955c32d3a786d43d577050943565da648d5
```

Add `-O` before the script for optimized verification. The verifier checks the fixed receipt, archive/manifest hashes, safe unique member paths, sizes and SHA-256 values, extracts nothing, invokes no subprocess/SDK and executes no archived script. Integrity verification supplies no renewed target execution authority.

All thirteen revisions, stage-entry hashes, raw index hashes, index flags and runtime-period working statuses matched before/after execution. Protected sources/records/SDK inputs rehashed unchanged, and real/private certificate-store metadata matched. Certificate prevention was set before the first SDK use; no store/key contents were inspected or cleaned. External mounts were read-only, network namespaces and socket denial remained enforced, and sampled owned groups showed no survivors. Sampling is bounded rather than whole-machine/file-access tracing. Immediate driver gates wrap outer commands; SDK calls inside the unchanged catalog helper inherited the same sandbox/environment and were not independently wrapped as driver gates. The additive final preservation record separately admits subsequent recording files.

## Release-owner handoff and retained acceptance limits

This proves the explicit binding demands for the sealed Debug source tuple under the recorded host dependency configuration. It does not qualify Release packaging/layout, signed CLI distribution, ordinary workspace mapping/ContentHash, API/Redis behavior or a hosted/full topology. The source-reference import is a private diagnostic, not ordinary SourceMappingMaterializer output. Release owners still need a chosen exact distribution tuple, genuine ordinary catalog/model/mapping evidence, package/native/satellite checks, direct owner closure/registration and the runtime/Docker/profile/secret/feed/state/port/cleanup prerequisites.

The prior signed CLI 4.38 cold and host-first failures demanding EventStore 3.128 remain unchanged: `FileLoadException / 0x80131040`, exit 10 each. Current source binding success does not repair or supersede those signed-package observations, historical procedural gaps, package-layout seams or the EventStore/Tenants/Memories dependency-policy conflict. CLI 4.35.2 remains the historical accepted tool; 4.38 remains a candidate.

Parent **in-progress**, tracker **backlog**, **V1Only**, unset **T0**, null **activation authority**, and **0/14 staging checks** remain intact. No ordinary mapping or ContentHash was fabricated. No staging, commit, push, tool installation, submodule initialization, publication, deployment, solution-level test or application entrypoint ran. The two loader cases are consumed once each; no runtime retry is authorized by this result.
