# Story 12.2 strict consumer continuation

**Not qualified. Task 36 and Story 12.2 remain in progress.** Two unchanged strict
real-provider commands failed during fixture initialization, before any authenticated
consumer command or acceptance assertion. This packet supplies current source/runtime
audits and an actionable startup handoff. It does not supersede the prior passing
capacity evidence or establish a new admission failure or repair.

| Current command receipt | Passed | Failed | Errors / skipped | xUnit seconds | Exit |
| --- | ---: | ---: | ---: | ---: | ---: |
| `strict-live.json` | 0 | 1 | 0 / 0 | 226.062 | 1 |
| `strict-retry-live.json` | 0 | 1 | 0 / 0 | 211.058 | 1 |

Both failures are `FolderDurabilityAcceptanceFixture.InitializeAsync` → `StartAsync`
line 880 → `DaprClient.CheckHealthAsync`: the existing 100-second HttpClient timeout
elapsed. No assertion, deadline, retry limit, admission setting or rate limit changed.
No product source, SDK source, dependency pin, qualified output or Git state was changed.
Only this packet was authored. The only build created the independent PDB audit tool
under an isolated temporary directory.

The exact invocation, working directory, start time, environment selection and elapsed
time are in each receipt; corresponding logs and xUnit XML retain the complete failures.
Both commands used the already-built Debug assembly:

```bash
HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true DOTNET_USE_POLLING_FILE_WATCHER=1 DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false python3 _bmad-output/implementation-artifacts/evidence/story-12-2-strict-continuation-20261022T100829Z/run.py strict-retry-live dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll -class Hexalith.Folders.AppHost.Tests.OperationalStatusDurabilityAcceptanceTests -showLiveOutput -result-xml /home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/evidence/story-12-2-strict-continuation-20261022T100829Z/strict-retry-live.xml
```

The initial read-only audit matches all 15,098 prior frozen source/configuration inputs,
303 PE/PDB files and 2,434 sealed packet files, with zero differences. The prior runtime
selector checks 81 identities and 13 dependency manifests successfully. The expensive
passing 10,001-position capacity lane and both required component sizes were reused
with that explicit provenance; they were not rerun or counted as new coverage.

The launch audit exposed two inventory omissions. Aspire launches the executable
`references/Hexalith.Tenants/src/Hexalith.Tenants`, whereas the old selector names its
`Hexalith.Tenants.Server` library directory. All 25 actual required launch artifacts
exist. The extended freeze contains 584 runtime artifacts, including the actual Tenants
graph. All 158 portable PDBs match their PE identities and 22,086 ordinary source-document
copies; 1,736 generated copies use the retained embedded-source policy. All 78 current
generated-disk differences remain recorded. The independent actual-process capture
contains 270 mapped DLL observations, including 66 Hexalith observations, with zero
hash/source-binding errors after correctly excluding third-party packages from the
portable-source requirement. The original overbroad diagnostic remains preserved.

The optional Memories launcher is absent. It was also absent in the prior passing
capacity and prior strict logs; it is not established as the cause of either current
failure, and was not repaired. The first launch-inventory diagnostic mistakenly checked
the Tenants library as an executable; the corrected inventory retains that distinction.

`sidecar-startup-handoff-expanded.json` retains nine exact sidecar launch argv/resource
paths per run, source line numbers, startup completion metrics, errors and terminal
failure text. All seven relevant sidecars receive SIGHUP during startup in each run.
The EventStore sequence is:

| Attempt | SIGHUP UTC | Subsequent initialization metric | Terminal native error |
| --- | --- | --- | --- |
| Initial | 10:10:10.743 | `dapr initialized. Status: Running. Init Elapsed 77838ms` | `Graceful shutdown timeout exceeded, forcing shutdown` |
| Retry | 10:16:32.741 | `dapr initialized. Status: Running. Init Elapsed 74430ms` | `Graceful shutdown timeout exceeded, forcing shutdown` |

The initial attempt additionally reports `Failed to register hosted actors: api error:
code = Internal desc = the state store is not configured to use the actor runtime.
Have you set the - name: actorStateStore value: "true" in your state store component file?`
The retry's component directory was present, owned by the fixture UID, mode 0700; both
statestore YAMLs contain `actorStateStore=true` and isolated Redis addresses. Component
hashes and ownership are retained without secrets. Both directories are gone after
cleanup. The signal sender and reason are unclassified. Neither prior accepted capacity
nor prior strict logs contain the same SIGHUP observation. No demonstrated configuration
mismatch justifies a consumer correction. Next investigation should identify the sender
and Dapr restart/actor initialization sequence using these exact launch arguments before
another strict run; it must preserve unrelated applications and production policy.

Historical admission diagnosis is separate. `retained-admission-first-failure.json` and
`retained-admission-timeline.json` are explicitly marked extracts from the prior sealed
strict log. That run reserved ConfigureBranchRefPolicy, routed it, then logged anonymous
internal HTTP429 failures and inability to persist the unknown admission outcome at
`IdempotencyAdmissionActor/MarkRecoveryAsync`. Current SDK source inspection identifies
the global chained limiter, internal partition selection and Pending return path, without
claiming the historical rejecting lease, request authentication or final durable state.
The EventStore owner must investigate this exact admitted/routed failure and safe recovery
under the existing limits; changing Folders retry/admission policy is not a substitute.
Neither new run reached this failure. Read-only metadata observers captured no matching
admission/status rows and stopped after the runs; no protected replay bodies or credentials
were exported by those observers.

`cleanup-final.json` proves no owned process or container remains for either fixture ID.
The unrelated Tenants AppHost remains at PID 67730. No quota or unrelated resource was
changed. Final frozen/historical audits and packet checksums are retained alongside
`qualification.json`. Strict consumer acceptance, the separately owned admission issue,
package/PD10, SM4 acknowledgement performance and independent whole-story review remain
open. No new full live pass, successful strict persisted end-state or Task 36 closure
is claimed.
