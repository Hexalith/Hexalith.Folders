# Story 12.2 sidecar startup correction and selected-candidate qualification

**Task 36 selected-candidate consumer qualification passes. Whole Story 12.2 remains in progress.**
The unchanged strict authenticated real-provider case passes 1/1, with zero failures/skips, in
343.629 xUnit seconds. Existing HTTP/rebuild helpers pass 22/22. The only implementation change is
the fixture's `StartAsync` directory layout. Production/SDK binaries, admission, access policies,
assertions, deadlines and retry limits are unchanged.

## Startup diagnosis and correction

The two earlier sealed startup logs explicitly record Configuration CREATED for `accesscontrol`,
followed by a hangup restart. The exact Dapr 1.18.7 source identifies the sender: standalone config
loading does not seed the raw Configuration resource into the component store; a later watched-directory
event discovers the co-located YAML as a new resource, and the reconciler signals its own PID.
[The source diagnosis](sidecar-source-diagnosis.json) retains commit-specific primary-source URLs,
hashes, line references, native binary identity and both original log extracts. No external signal
sender is inferred.

`diagnose.py` executes two native daprd controls with byte-identical deny-by-default configuration.
An unrelated directory event causes CREATED/SIGHUP with configuration inside the watched directory.
Moving only configuration outside that directory avoids reload. Both keep HotReload enabled;
`colocated.json/log` and `separate.json/log` retain argv, outcomes and hashes.

The fixture now materializes configuration separately from components and ordinary proof resources.
Its provider credentials, private context and callback observation files remain outside the component
directory. The live observer records all nine sidecars using configuration outside watched resources.
The strict log records zero configuration-triggered SIGHUP reloads. The parent independently confirms
the diff changes only `StartAsync`; other fixture methods and the acceptance test are unchanged.

## Executed commands and results

Each command's JSON receipt contains exact argv, UTC start, environment selection, elapsed time and
exit code. Logs and xUnit XML are retained alongside it.

| Receipt | Result |
| --- | --- |
| `fixture-build` | Test-only Debug/source build, zero warnings/errors; reference-version mismatch discovered afterward. |
| `strict-live` | Retained diagnostic: 0 passed/1 failed/0 skipped, 4.023 s; fixture could not load the newer compile-time Aspire assembly identity from the preserved runtime. |
| `qualified-fixture-build` | Test-only build against exact selected references, zero warnings/errors. |
| `strict-selected-live` | Retained diagnostic: 0 passed/1 failed/0 skipped, 91.849 s; unchanged disposable-provider readiness deadline elapsed before Aspire started. Its cause remains unclassified. |
| `strict-observed-live` | **1 passed/0 failed/0 skipped, 343.629 s**, exit 0; complete unchanged strict authenticated provider acceptance. |
| `fixture-helpers` | **22 passed/0 failed/0 skipped**, exit 0. |

The successful command is:

```bash
HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false python3 _bmad-output/implementation-artifacts/evidence/story-12-2-sidecar-startup-20261022T105749Z/run.py strict-observed-live dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll -class Hexalith.Folders.AppHost.Tests.OperationalStatusDurabilityAcceptanceTests -showLiveOutput -result-xml /home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/evidence/story-12-2-sidecar-startup-20261022T105749Z/strict-observed-live.xml
```

The strict pass executes real Forgejo TLS readiness and target denial, durable provider binding,
clean-release task closure, all six persisted concerns, original-source preservation, source-time TTLs,
empty-checkpoint rebuild, interrupted generation recovery, two concurrent Workers, changed host PIDs,
equivalent replay, closed-binding mutation refusal, authorized scoped task reads and safe denied/corrupt/
unavailable-storage behavior. Exact pass markers, PIDs and latency samples remain in the log and
`qualification.json`. No admission-unavailable message occurs. This pass does not repair or classify
the historical SDK admission failure.

## Exact execution identity and reused evidence

The pre-change audit verifies all 584 prior runtime artifacts and the earlier packet seal. Seven of
15,098 source/config inputs differ, all external sibling EventStore edits. They remain preserved;
they are neither compiled nor qualified by this run. `repositories.json` retains canonical revisions
and working-tree observations without performing any Git mutation.

Only the fixture DLL/PDB were rebuilt, with `BuildProjectReferences=false`. The evidence-owned
`qualified-test-references.targets` compiles that fixture against the exact selected implementation
assemblies. Normal build copying displaced 14 runner dependencies; `select-and-freeze.py` restores
their verified prior copies. Every production/SDK artifact remains byte-identical to the prior sealed
selection. This is qualification wiring, not a new dependency pin or SDK runtime adoption.

The rebuilt fixture PDB matches its PE identity and 20 ordinary/five generated documents with zero
errors (`fixture-source-binding/summary.json`). The final execution freeze contains 15,098 source/config
inputs, 584 runtime artifacts and one manifest, all unchanged during execution. The observer captures
186 actual mapped module identities, all matching that freeze. The prior packet supplies the original
production PE/PDB/source binding; current external SDK edits are explicitly outside that binding.

Task 36 combines this new strict pass with the byte-bound
[inherited-owner packet](../story-12-2-inherited-owner-requalification-20261022T084922Z/handoff.md): its
passing 10,001-position authenticated recovery/inheritance lane, both 10,001/32,769-position production
handler/dispatcher component sizes, growth/repeated rebuild/conflict controls and explicitly bound SDK
process coverage. Both packet seals are checked again, including 2,434 inherited-owner files and 546
prior startup files. All 6,503 historical evidence hashes remain unchanged. Reused results are enumerated
separately in `qualification.json`; they are not newly executed coverage. No 32,769-position Folders
HTTP run is claimed. The 100-operation/one-MiB limits and original source identities remain unchanged.

## Remaining gates and cleanup

Current p95 is 7,792.582 ms for acknowledgement against 1,000 ms, 258.854 ms for Workers against
1,000 ms, and 79.891 ms for status reads against 500 ms. SM4 acknowledgement remains open. Current
external SDK source/broad-regression dispositions, Release/package/PD10 and independent whole-story
review/acceptance remain separate open gates. No whole-story completion or current-external-source
qualification is claimed.

`cleanup-final.json` and `cleanup-directories-corrected.json` prove no exact fixture process, fixture-named
container or owned directory remains. The unrelated Tenants AppHost stays at PID 67730. The first
supplementary directory probe over-selected the default user Dapr configuration's unrelated ancestor;
that failed read-only absence assertion is retained and corrected. No removal was attempted against it.
Observers stopped; admission snapshots export selected metadata with storage-key hashes, excluding
replay bodies, credentials and tenant-partition values (`metadata-export.json`).

`parent-audit.json` independently verifies the frozen hashes, 186 mapped identities, nine separated
configuration paths, strict/helper XML, path-only diff and exact cleanup. `final-preservation-audit.json`
verifies historical preservation. No product/SDK source, dependency pin, Git index, quota, retained
source event, public v2 surface or unrelated application changed. `SHA256SUMS` seals this packet.
