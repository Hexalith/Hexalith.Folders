# Story 12.2 current consumer verification

The existing implementation passes all 87 operational projection regressions on current source,
including both 10,001/32,769-position histories. Current integrated acceptance remains incomplete:
the current SDK selective projection mapper requires a whole-host HTTP security enrollment that the
Folders host does not supply. No product source correction is included in this continuation.

The packet date follows the first command's UTC date, 2026-10-31. Execution occurred on 2026-11-01
in the workspace's Europe/Paris timezone. All four frontmatter context files and the complete parent
spec were read; `intent-inputs.json` records their pre-continuation hashes.

## Executed verification

Every command has a JSON receipt with exact argv, UTC times and exit status. Logs and xUnit XML
remain alongside the receipts. These rows represent separate runtime selections and are not added
together into a claimed full passing regression run.

| Selection / receipt | Result |
| --- | --- |
| Initial isolated `domain-build`, `server-build`, `workers-build` | Debug/source builds pass, zero warnings/errors; 30.52/18.97/23.41 seconds. |
| `operational-domain` | 87 passed, zero failures/skips, 94.616 seconds. |
| Initial full `server-tests` | 1,206 total: 1,129 passed, 77 failed, zero skips. |
| Initial full `workers-tests` | 161 total: 156 passed, five failed, zero skips. |
| Repo-local `server-local-version-build`, `workers-local-version-build` | Debug/source builds pass, zero warnings/errors, with explicit local `Version=1.0.0`. |
| `server-affected-local-version` | Previously failing methods: 154 cases, 116 passed/38 failed, zero skips; all repository-path failures resolved. |
| `workers-affected-local-version` | Five previously failing cases: one passed/four failed; repository-path failure resolved, assembly mismatch retained. |
| `workers-matched-version-build` | Debug/source build passes, zero warnings/errors, 15.56 seconds; explicit local `Version=3.128.4` matches the existing SDK catalog. |
| `workers-affected-matched-version` | Four affected cases pass, zero failures/skips, 0.808 seconds. |
| `production-discovery-minimal` | One production-composition case fails, zero skips, 0.419 seconds, with missing `EventStoreHttpEndpointSource`. |
| `registration-probe-final-build` | Diagnostic harness builds with zero warnings/errors. Its initial two-warning build remains retained separately. |
| `registration-probe-final-original` / `registration-probe-final-public-seam` | Existing composition reproduces the exception; public HTTP registration removes that exception but the SDK inventory reports 58 incompatible endpoint classifications. |

The operational lane executes the existing Folders handler and production dispatcher with the SDK
batch implementation. It covers inherited generations, original identity conflicts/duplicates,
growth/repeated rebuild, scratch retry/Abort, retention boundaries, clock registration, corruption
and source-derived task status. It is component evidence; no new real-provider, Dapr, HTTP capacity,
Release/package, PD10 or SDK broad-regression qualification is claimed.

## Isolated-output corrections

The initial `/tmp` artifacts directory prevented existing tests from finding repository fixtures by
walking upward from `AppContext.BaseDirectory`. Rebuilding under ignored `.artifacts/` resolves
those failures without changing the tests or product.

`Hexalith.Tenants.Contracts.csproj` and `Hexalith.Tenants.Client.csproj` pass
`AdditionalProperties="Version=$(HexalithEventStoreVersion)"` to EventStore project references.
That global property reaches the Commons source project as well. Other graph edges initially built
Commons at its default 1.0.0, leaving EventStore.Contracts referencing Commons 3.128.4 beside a 1.0.0
runtime. A global local `Version=1.0.0` does not defeat the explicit Tenants edge override. Matching
the local build version to the existing 3.128.4 SDK catalog yields matching 3.128.4 references and
runtime, then all four affected Workers cases pass. `matched-*-metadata.txt` retains actual assembly
metadata from dotnet-inspect. This changes local assembly identity only; no dependency catalog or
package version is edited. Commons' self-contained build does not use MinVer.

`source-graphs.json` records every selected Hexalith dependency as a project reference in all three
selections. No Hexalith package/source mixture is used to obtain the passing results. Original failed
receipts and both original artifacts directories remain preserved.

## Production mapper enrollment handoff

The narrowest existing-test reproduction is:

```bash
dotnet .artifacts/story-12-2-current-consumer-20261031/bin/Hexalith.Folders.Server.Tests/debug/Hexalith.Folders.Server.Tests.dll -method Hexalith.Folders.Server.Tests.FoldersServerHostCompositionTests.ProtectedProductionDiscoveryAdvertisesOnlyProcessorBackedNamedAndSharedRoutes
```

Current SDK source at the recorded EventStore HEAD enters
`UseEventStoreDomainServiceEndpointSecurity` unconditionally from
`EventStoreDomainServiceExtensions.MapEventStoreDomainProjectionEndpoints` (line 361), which enters
`UseEventStoreHttpHostSecurity` and requires its internal endpoint source (line 75).
Folders' bespoke production host maps this selective seam without enrolling that generic security
service. The failure happens during pipeline construction, before live acceptance can start.

The SDK supplies public `AddEventStoreHttpHostSecurity` and
`AddEventStoreDomainServiceSecurity` registration seams. `RegistrationSeamProbe.cs` uses the actual
production test builder and only the public HTTP registration, then invokes the unchanged production
pipeline and public endpoint inventory. Registration removes the missing-service exception but leaves
58 policy-classification violations across the existing root/health/subscription and v1 routes.
The full domain-service registration also enrolls this same HTTP boundary; it is not a selective-only
compatibility registration. No internal service is registered and no policy or guard is bypassed.

The next owning change must qualify either a compatible SDK selective enrollment contract for this
bespoke host, or an explicitly scoped consumer migration of its complete HTTP security inventory.
A one-line registration is not demonstrated as compatible. This continuation does not silently move
that broader host-security migration into Story 12.2 or edit separately owned SDK source.

## Identity, preservation and remaining gates

`repositories-before.json` records all 13 repository heads and initial working trees.
`sources-before.json` freezes 17,022 tracked source/config inputs across the root and its 12 initialized
root-declared submodules; no nested submodule was initialized or updated. `final-audit.json` confirms
zero source/config drift, zero drift across the 406 original and 403 corrected runtime/config
artifacts, and all 174 entries in the prior selected-candidate packet's seal unchanged. The probe
records loaded Server/DomainService/ServiceDefaults paths, assembly names and MVIDs; the runtime
inventories bind their exact PE/PDB hashes.

`cleanup.json` confirms all task runner/probe processes exited. The first broad process probe matched
its own Bash command, retained as a diagnostic; the follow-up checks the executable and exact argv.
No Aspire/Dapr/provider/container topology was started by this continuation.

The checked historical Task 36 selected-candidate result remains exact-source evidence. Whole Story
12.2 stays in progress. Current integrated-source host enrollment, SDK broad-regression disposition,
Release/package/PD10, SM4 acknowledgement and independent whole-story review remain open. Historical
latencies and failed attempts are retained without reinterpretation. No product/SDK source, frozen
intent, sprint status, dependency pin, retained event, public v2 surface or Git index is changed.
