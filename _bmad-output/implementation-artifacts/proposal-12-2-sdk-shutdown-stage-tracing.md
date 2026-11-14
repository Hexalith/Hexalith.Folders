# Story 12.2 — bounded SDK shutdown investigation

Status: proposed; human decision pending. Neither companion patch is applied. This proposes diagnostic instrumentation and a bounded route to an evidence-confirmed repair; no shutdown cause or product repair has been established.

## Decision requested

Authorize EventStore-owned investigation of the intermittent process-stop failure blocking Story 12.2, starting with the default-off tracing patch below, and the smallest internal SDK coordination correction **only after causal evidence identifies it**. Include the Root-owned fixture forwarding patch needed to activate that diagnostic for the isolated EventStore resource. Keep all existing host-command budgets, resource restart order, retry behavior, authorization, source-proof requirements, exact lease/remote-call ownership and durable outcomes fixed.

The precise existing exclusion to supersede, only for this bounded investigation and evidence-confirmed shutdown correction, is: **“It does not authorize generic SDK shutdown/performance repair or policy changes.”** The spec's latest proposal-preparation continuation also states: “Further generic SDK source work remains outside the current approved scope.” Prior native authority read/bootstrap performance approvals do not authorize this stop investigation. General SDK performance redesign and policy changes remain excluded. No dependency/gitlink/pin update, publication, deployment, commit, push, public v2 exposure or weakened acceptance is included.

Approval would authorize applying and qualifying these diagnostics in their owning repositories, reproducing the existing failing stop with private receipts, preparing the smallest causally supported repair with a failing/passing regression, then qualifying that repair against the unchanged strict acceptance. A further coordination correction must identify its exact owner/files, demonstrate the awaited failure and preserve the fixed guarantees above before implementation. An ambiguous or unreproduced wait is not permission to guess a repair. A change to those guarantees requires a separate decision.

## Evidence and its limits

| Retained packet | Exact outcome | What it establishes |
| --- | --- | --- |
| [Final producer qualification](evidence/story-12-2-final-producer-qualification-20261114.json) | 0 passed / 1 failed; 411.136 assembly seconds, 335.046 body seconds; first EventStore stop after six-concern checks | The selected 106-file producer still has an ordinary strict stop failure. Shutdown signal/lifetime logging is observed; DCP fails about 11.999 seconds later. No dump or specific causal await was obtained. |
| [512 MiB RAM diagnostic](evidence/story-12-2-ram-stop-diagnostic-20261114.json) | 0 passed / 1 failed; partial 550985728-byte heap aborted and removed | The long stop reproduced, but the polled storage estimate failed. No usable heap or causal stack remains. RSS did not bound heap size. |
| [Corrected RAM sizing diagnostic](evidence/story-12-2-ram-stop-sizing-recheck-20261114.json) | 1 passed / 0 failed; 416.448 assembly seconds, 341.201 body seconds | All three stops ended before the eight-second trigger. No collection, heap or suspension occurred; the 2 GiB guard was unexercised. This explicitly diagnostic run is not promoted to production acceptance. |

All three packets retain matching producer identities, zero source/runtime drift and owned cleanup. Preserve them and every earlier failure/pass. The .NET 11 analyzer's incomplete async-stack support remains a limit. The isolated telemetry cancellation probe does not attribute the strict failure; the earlier unbounded-provider hypothesis is not a proven cause. Command/status SM4 over-budget findings, full-CI disk qualification and governance/whole-story gates remain open independently.

## Concrete unapplied changes

SDK base: `references/Hexalith.EventStore` at **51dd68b86a690ffe9e11c565dccec49b2d401a94**, clean before preparation. [SDK patch](proposal-12-2-sdk-shutdown-stage-tracing.patch) paths are relative to that repository. [Fixture patch](proposal-12-2-sdk-shutdown-stage-tracing.fixture.patch) is separate and relative to the Folders root at **b915d3ec5180f45c4c477cbeffb9b98df91082d0**, including its existing uncommitted changes. Exact touched-input hashes are recorded below; no repository configuration changes are proposed.

| Owner / file | Observation or control |
| --- | --- |
| SDK `src/Hexalith.EventStore.ServiceDefaults/Diagnostics/ShutdownStage.cs` (new) | Closed set of ten stage names. |
| SDK `src/Hexalith.EventStore.ServiceDefaults/Diagnostics/ShutdownStageScope.cs` (new) | Internal, advisory, opt-in stage pairs through existing ILogger/LoggerMessage; no operation wrapper or new service. |
| SDK `src/Hexalith.EventStore.Server/Projections/ProjectionActivationWorker.cs` | `ProjectionActivationStop`: existing base StopAsync wait; `ProjectionActivationExecutionDrain`: existing ObserveAsync/WhenAll drain; `ProjectionActivationRemoteDrain`: exact existing lease DrainRemoteCallsAsync await. |
| SDK `src/Hexalith.EventStore.ServiceDefaults/BoundedTelemetryShutdownService.cs` | `TelemetryStop`: existing host wait; `TelemetryTracerCall`, `TelemetryMeterCall`, `TelemetryLoggerCall`: original provider calls inside the existing sequential Task.Run. |
| SDK `src/Hexalith.EventStore.Server/Diagnostics/AdminIndexShutdownTrace.cs` (new) | Internal fixed-stage bridge using existing assembly friendships; returns only its own internal type to the root SDK assembly. |
| SDK `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs` | `AdminIndexStop`, `AdminIndexCancelCallbacks` around existing CancelAsync, `AdminIndexRefreshDrain` around existing refresh-task WaitAsync. AddEventStore registers this hosted service. |
| SDK `tests/Hexalith.EventStore.Server.Tests/Telemetry/ShutdownStageScopeTests.cs` and `ShutdownStageTestLogger.cs` (new) | Default/invalid opt-in, Debug filtering, closed stage set, fixed payload, pairing, original exception/token, logger failure, internal bridge and original telemetry call order. Uses the existing nonparallel privacy collection and restores process environment in finally. |
| Root `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs` | Forward the test diagnostic opt-in only to the fixture's EventStore resource, with the three exact existing logger categories enabled. Existing observer behavior and strict test bodies are unchanged. |

The existing ServiceDefaults friendship to Server and Server friendship to the root SDK assembly suffice. The bridge's constructors/private storage may use the ServiceDefaults internal type, but its root-consumed signatures use only its own internal type, ILogger and CancellationToken. No new friendship, public type, endpoint, proxy, dummy sink, forced Activity, background task, timeout or restart policy is added.

## Activation, semantics and privacy

The SDK helper starts only if the EventStore **process** has the exact value `HEXALITH_EVENTSTORE_SHUTDOWN_TRACE=true` and its existing logger enables Debug. Missing, empty, false, uppercase or other values are off. Off returns null before touching the logger and allocates no observation scope. It still performs the small flag check at each candidate point; this is not a zero-cost claim.

For the unchanged strict fixture, launch a separately labeled diagnostic with **`FOLDERS_TASK36_SDK_SHUTDOWN_TRACE=true`** on the test process. The Root patch uses its existing WithEnvironment seam, before `FoldersDevelopmentCatalog.Compose`, to forward `HEXALITH_EVENTSTORE_SHUTDOWN_TRACE=true` only to the exact owned `eventstore` ProjectResource and enable Debug for `ProjectionActivationWorker`, `BoundedTelemetryShutdownService` and `AdminOperationalIndexHostedService`. Do not set the generic SDK switch globally. The test-specific switch is not consumed by other hosts. Without the exact test opt-in no resource annotation changes. ConfigureObservers, existing observer startup hooks/opt-outs, identity/security/catalog options, OTLP settings and the strict assertions stay unchanged.

Event **6210**, Debug, emits only fixed Stage and State strings, nonnegative saturated integer ElapsedMs, numeric ProcessId and process-local Sequence, and the observed token's CanBeCanceled/IsCancellationRequested booleans. It does not inspect bodies, tenant/domain/aggregate/task/activation IDs, lease contents, URLs, files, credentials, exceptions or ambient Activity/scopes/tags/baggage. The token is observed only: no registration, cancellation or token substitution. The helper creates no Activity or logging scope.

Each enabled call emits Started then at most one Returned or Unwound. Returned means only that the enclosed existing call returned; provider Shutdown may return false or be swallowed by the pre-existing handler, and BackgroundService.StopAsync may return while execution still drains. Unwound does not disclose or classify an exception. Sequence pairs one scope inside a verified process; it has no business meaning. Remote-drain starts may occur before cancellation so a call already draining when stop begins remains observable. There is no forced lease release, dropped drain, extra retry or inference of persistence/success.

Logger enablement/emission exceptions are swallowed within the new helper. Existing non-diagnostic warnings and their semantics are unchanged. Logging is synchronous and an enabled external provider can itself block or add enrichment; the patch cannot guarantee nonblocking delivery or strip provider-added scopes/correlation. Use only the existing private resource-log capture, restrict extracted evidence to Event 6210's allowlisted fields, and keep complete enriched logs private (0700 directories / 0600 files). There is no new exporter/sink. Logging may become unavailable during telemetry shutdown; a missing end event alone does not prove a hung await, and absence after the last observed boundary cannot locate later host disposal. Validate the resource PID/start identity and join with the unchanged lifecycle and process-stop receipts before attribution.

## Verification after approval

1. Apply only after exact revision/input checks. Build affected SDK projects and Server.Tests with normal warnings/analyzers. Run the new controls and existing activation/route-catalog ownership, source-guard, telemetry and admin-index stop regressions. Add deterministic held-await controls at the causally implicated production seam before claiming a repair; current helper tests alone do not prove every drain boundary.
2. Build the bounded Root fixture change and check absent/invalid/true resource forwarding, exact owned-resource selection and preserved observer annotations. Rebuild the complete selected Debug/source producer; audit ordinary/generated source, PE/PDB, project assets, SDK variants and actual loaded identities. Preserve the frozen blocks and all three protected strict body hashes.
3. Run the **same** strict `OperationalStatusDurabilityAcceptanceTests` diagnostic with the existing opt-in, deadlines, retries, restart order, assertions and complete SM4 sampling. Keep logs private; retain only stage/PID/timing/cancellation metadata. Retain a failure as a failure. A tracing pass supplies no cause or repair, and no diagnostic run is silently promoted to production acceptance. Continue a bounded investigation only while it produces concrete evidence.
4. Before a correction, show the actual waiting stage and its ownership chain, a private deterministic reproduction at that seam and the smallest owner/file proposal. Preserve ambiguous remote-call/lease ownership until actual completion. Once justified, require the regression to fail before and pass after the correction, rerun relevant SDK gates, regenerate/audit the producer and execute the unchanged strict acceptance with tracing off. Record all failures/flakiness and unchanged SM4 budgets; separately complete broad/full-CI/governance gates. Do not relabel one pass as proof of a shutdown repair or whole-story completion.

## Preparation checks

Read-only applicability and isolated-overlay results are recorded in the final preparation receipt below. No companion patch has been applied to either actual repository. No live topology or acceptance run is part of preparing this decision. The frozen intent and in-progress story/sprint status remain unchanged pending the human answer.


Final preparation result (2026-11-14): both applicability commands below exit **0** against the exact current inputs. The patches use zero context so blank context lines introduce no whitespace noise; require `--unidiff-zero` and the recorded revision/hashes. These commands **do not apply** either patch.

```sh
git -C references/Hexalith.EventStore apply --check --unidiff-zero --whitespace=error-all /home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/proposal-12-2-sdk-shutdown-stage-tracing.patch
git apply --check --unidiff-zero --whitespace=error-all _bmad-output/implementation-artifacts/proposal-12-2-sdk-shutdown-stage-tracing.fixture.patch
```

An isolated private RAM source overlay builds **ServiceDefaults, Server, EventStore and Server.Tests: four exit-0 results**, with existing assets/dependencies, normal warnings/analyzers and no restore. The final eight proposed SDK source files exactly match the compiled overlay inputs. The new controls pass **11/11**, zero failures/errors/skips, **0.121 assembly seconds**, **0.666 wrapper seconds**. This compiles the actual root SDK consumer of the internal bridge, confirming accessibility without new friendship or public surface. It does not compile/execute the Root fixture patch or establish a live stop cause.

All build/test executions use bubblewrap: the actual workspace, SDK outputs and shared caches are read-only; only the private RAM directory and SDK source/output overlay are writable; networking is disabled. Direct single-node MSBuild uses `BuildProjectReferences=false`, `UseSharedCompilation=false`, `-nr:false`, Debug/source mode, EventStore version `5.0.1` and suffix `story12-2-qualification`; referenced producer files are copied/read unchanged. No analyzer suppression is added. An earlier isolated `dotnet build` CLI attempt stalled before a compiler result and was stopped after about **179.688 seconds**; its signal-associated exit 0 is **not** counted as a successful build. Direct MSBuild subsequently completes in **2.774 / 8.769 / 2.120 / 21.127 seconds**. No cause is assigned to that preparation-only stall.

The actual SDK remains clean at the same full head. All **4987 ordinary SDK C# files** and **7142 SDK DLL/PDB files** match their pre-preparation hashes; all **106 selected final-producer DLLs** match the retained qualification audit. The parent frozen SHA remains `497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422`, and all three protected strict body hashes match. The actual Root fixture matches its recorded input. No SDK patch, Root fixture patch, SDK output, shared cache, package/pin or public-surface change is made. No live topology/acceptance run occurs. The phase-owned 2593767553-byte RAM overlay is removed; four proposed DLL/PDB pairs and exact private build/test/audit receipts remain protected in `h12proposal-df79xuod`. The source/output ownership is released with no active owned build/test/topology.

| Existing touched input | SHA-256 before / still current |
| --- | --- |
| SDK `src/Hexalith.EventStore.Server/Projections/ProjectionActivationWorker.cs` | `94add172fc336d0a1e46e1931556234d1db7b5ccec82dba1f42bd9cd00dc8769` |
| SDK `src/Hexalith.EventStore.ServiceDefaults/BoundedTelemetryShutdownService.cs` | `a9175339c5e51f4d486fb6b7183849a29610ad5a467afe108756a51034effa0e` |
| SDK `src/Hexalith.EventStore/Indexes/AdminOperationalIndexHostedService.cs` | `0bfa0a66e3cf36c4baa225426576b65198cbb2354c76a59a17508a771a067fe5` |
| Root `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs` | `17d82532cad9f597200635b724a0901e128daa2c6de2f221aebd0a3d01aecbd4` |

proposal-12-2-sdk-shutdown-stage-tracing.patch: SHA-256 `c7ad6daddefce7c18de8a98999e696bd67e6b0ffa9b2d1e8da1db67bb70138ba`.

proposal-12-2-sdk-shutdown-stage-tracing.fixture.patch: SHA-256 `4097a3af7391e2e3c47241f95cffae781e0e747301ec16e9c13c4fe725384aff`.

[The metadata-only preparation receipt](evidence/story-12-2-sdk-shutdown-tracing-proposal-20261114.json) binds these artifacts, final controls, original-source/output audits, the unsuccessful CLI attempt and private cleanup. It is evidence for this proposed diagnostic patch only. The scope exclusion remains in force until the human decides.
