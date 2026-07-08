# Sprint Change Proposal — Stand up & run the DCP-capable AppHost lane (Epic 10 live evidence)

- **Date:** 2026-07-07
- **Author:** Amelia (dev) via `bmad-correct-course`, for Jérôme Piquot
- **Trigger (verbatim):** "Stand up and run the DCP-capable AppHost lane for Epic 10 live evidence: topology boot, folders-index auto-provisioning, SearchIndexEntryChanged, SearchIndexEntryRemoved, archived status filtering, and query-facade search/hydration."
- **Change scope classification:** **Moderate** (two real fixes applied + one newly-surfaced defect to triage; backlog/status reconciliation needed). Routes to Developer + PO.
- **Mode:** Batch (operational investigation was run first; findings presented together).

---

## ✅ Status update — 2026-07-08: the topology boots live under DCP (live-boot evidence achieved)

After the EventStore `3.43.0` packages were published, run #8 of the opt-in lane is **`Test Run Successful` — 2 passed, 2 skipped, 0 failed**:
- ✅ `FullFoldersTopologyBootsRunningAcrossProcesses` — **all 6 resources reached Running** (eventstore, tenants, folders, folders-workers, folders-ui, memories).
- ✅ `EventStorePublisherAndFolderWorkerSubscriberAreLive` — pub/sub participants live, folders gateway endpoint resolved.
- ⏭️ `SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` (AC9) + the folders.events probe — SKIP (Blocker D, below).

**These two passes are the Epic 9 AC6 live-boot + Story 10.3 D1#3 evidence.** Blockers A, B, C are resolved. Two follow-ups remain (see §3):

- **Blocker C — durable fix.** C was fixed by building the **Tenants server from source** (`-p:UseHexalithProjectReferences=true` → source DomainService `v3.43.0-16`). The published-package route (bump `Hexalith.EventStore.*` pins `3.42.0→3.43.0`) is **not viable as-is**: it breaks `Hexalith.Folders.Server` with `CS1704` because the pins are deliberately one minor *behind* the EventStore submodule source so source wins version-resolution; matching them ties the version → source-vs-package collision. Durable fix is a **platform decision**: let the Epic 11 refactor (removing EventStore refs from the Folders domain csproj) land first, then bump; or keep the EventStore source ahead of the published package; or build the Tenants server in source mode by default. The source-mode build here is a **local proof, not committed**.
- **Blocker D — AC9 round-trip (SKIP, not fail).** The harness resolves the Dapr sidecar HTTP endpoint on the **project** resource (`App.GetEndpoint("folders-workers","dapr-http")`), but it lives on a **separate sidecar resource** and isn't host-exposed by the default `DaprSidecarOptions` in `FoldersAspireModule.cs`. Running the seed→search→remove→archive assertions needs the sidecar HTTP endpoint **resolvable + host-reachable** — a small design story touching the AppHost topology + the harness.

---

## Section 1 — Issue Summary

Epic 9 and Epic 10 carried a standing residual: the live-boot half of the AppHost topology was **never verified**, blamed on an "environment-wide Aspire CLI 13.4.5 / DCP `--tls-cert-file` mismatch." Multiple acceptance items were parked as **BLOCKED-PENDING the DCP-capable lane**:

- **Epic 9 AC6** (stories 9.1 / 9.2 / 9.3) — live-boot half.
- **Story 10.3** D1 #3 — the Tier-3 cross-process harness `tests/Hexalith.Folders.AppHost.Tests` (built, never executed live).
- **Story 10.4 AC9** — the 6-service seed → search → remove → archive round-trip against the real `folders-index`.
- **Story 10.5 AC15** — live `folders-index` round-trip / query-facade sign-off.
- **sprint-status.yaml:280** — the literal action this proposal executes.

This change **stands the lane up**. The DCP lane is now **operational** — it composes, DCP orchestrates, the dashboard comes up, containers start, and all six services launch. Getting there required fixing **two real, latent defects** the live boot surfaced (neither of which any structural/conformance test could catch). A **third defect** (a service-startup crash) now blocks the final all-Running state and therefore the AC9 round-trip green; it is precisely characterised below for follow-up.

---

## Section 2 — Impact Analysis (what the live lane proved)

### Environment reality (corrects the Epic 9 framing)
The environment **is** DCP-capable today. The `aspire` CLI (13.4.5) is **not on the code path** the harness uses — `DistributedApplicationTestingBuilder` drives DCP straight from the NuGet packages — so the "CLI blocker" framing was a red herring for this lane.

| Capability | State |
|---|---|
| Docker daemon | 29.4.3, reachable |
| Dapr runtime | 1.18.1 (placement / scheduler / redis / zipkin up) |
| DCP orchestration | 13.4.6 → dcp **0.24.3** present in NuGet cache |
| Aspire.Hosting(.Testing) | 13.4.6 |

### Blocker A — Tenants project-path composition (FIXED, committed)
`references/Hexalith.Tenants/…/TenantsServerProjectMetadata.ProjectPath` used the legacy 2-branch probe (`<root>/src/…` and `<root>/Hexalith.Tenants/src/…`) and **omitted the canonical `<root>/references/Hexalith.Tenants/src/…` layout** that Folders uses. Result: topology composition threw
`Project file '<root>/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj' was not found` at `Program.cs:42` (`AddHexalithTenantsServer`). EventStore and Memories were already references-aware via the shared `RepositoryProjectPaths.GetReferencedModuleProjectPath` helper; Tenants was the lone laggard.

- **Fix:** switch `TenantsServerProjectMetadata` to `GetReferencedModuleProjectPath("Hexalith.Tenants","src","Hexalith.Tenants","Hexalith.Tenants.csproj")` — mirrors EventStore/Memories.
- **Status:** **committed in the submodule as `d923393`** ("refactor(TenantsServerProjectMetadata): simplify project path resolution and enhance documentation"), parent pointer bumped `9c7aa5c → d923393` (uncommitted in parent). Verified: composition now proceeds past Tenants.
- **Blast radius:** platform bug affecting **every** consumer that hosts Tenants under `references/` — this fix belongs upstream in `Hexalith.Tenants`.

### Blocker B — DCP `--tls-cert-file` / AppHost SDK version drift (FIXED)
The real "Epic 9 environment-wide DCP mismatch," pinned to a **version-pin drift inside the Folders AppHost csproj**:

- `Aspire.AppHost.Sdk` was pinned **13.3.5**, which stages `aspire.hosting.orchestration.linux-x64/13.3.5 → dcp 0.23.6`. **dcp 0.23.6 does not support `--tls-cert-file`.**
- `Aspire.Hosting` (package, `Directory.Packages.props`) is **13.4.6**, which **emits** `dcp start-apiserver … --tls-cert-file …`.
- Direct-run evidence: `dcp … the program finished with an error {"ExitCode": 1, "error": "unknown flag: --tls-cert-file"}` → the apiserver dies → `KubernetesService.EnsureKubernetesAsync` then times out at its hardcoded 20 s connect window (the symptom seen from the harness).

| orchestration pkg | dcp version | supports `--tls-cert-file` |
|---|---|---|
| 13.3.5 | 0.23.6 | ❌ NO |
| 13.4.5 / **13.4.6** | 0.24.3 | ✅ YES |

- **Fix:** `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` → `Aspire.AppHost.Sdk/13.3.5` **→ `13.4.6`** (one line). Now stages dcp 0.24.3 which accepts the flag. **Folders-repo change, no submodule involved.** Status: applied in working tree (uncommitted).
- **Verified live (post-fix):** `start-apiserver … --tls-cert-file` accepted (no "unknown flag"); Aspire dashboard "Now listening on https://localhost:17217"; "Distributed application started"; DCP brought up `memories-vectors` (Redis Stack) + `memories-graphs` (FalkorDB) containers; ~10 DCP-managed processes (6 services + Dapr sidecars); `eventstore` reached **Running**.
- **Dead-ends ruled out (do not pursue):** (1) box CPU saturation (load ~15.6/16 from other sessions + two long-running `java` procs) slowed boot but was **not** the cause; (2) `DcpPublisher__*` timeout env vars **do not** widen the hardcoded 20 s `EnsureKubernetes` Polly timeout, and they carry mixed TimeSpan/int types (`ContainerRuntimeInitializationTimeout` is a TimeSpan; an integer value overflows `CancelAfter`).

### Blocker C — `tenants` service aborts on startup (NEW, open)
With A + B fixed, the topology boots far enough to launch every service, but the fixture times out (8 min) waiting on `tenants`:

```
Resource 'tenants' failed to reach [Running]
- Current State: Finished   Start: 17:26:28   Stop: 17:26:30   Exit Code: 134 (SIGABRT)
```

`eventstore` reached Running; **`tenants` aborts ~2 s after launch (exit 134 / SIGABRT)** and never reaches Running, so the fixture's `WaitForResourceAsync("tenants", Running)` waits out the full budget. This is a distinct, newly-surfaced defect — **the live lane's job is exactly to surface this.**

**Root cause — PINNED** (from DCP's per-resource stderr, `/tmp/aspire-dcp<sid>/<guid>_err`; the box's load was **not** involved):
```
Unhandled exception. System.InvalidOperationException: No keyed service for type
'Hexalith.EventStore.DomainService.EventStoreDomainDiagnostics' using key type 'System.String'
has been registered.
  at Program …/references/Hexalith.Tenants/src/Hexalith.Tenants/Program.cs:line 67
  at Hexalith.EventStore.DomainService.EventStoreDomainServiceExtensions.ValidateDomainQueryHandlerRoutes
  at …UseEventStoreDomainService  →  Tenants/Program.cs:line 173
```
Tenants `Program.cs:67` registers `TenantTelemetry` via `serviceProvider.GetRequiredKeyedService<EventStoreDomainDiagnostics>("tenants")`, whose comment says the keyed diagnostics is *"registered by `AddEventStoreDomainService`."* That keyed registration (`AddEventStoreDomainService` → `AddEventStoreDomainTelemetry(GetDiscoveredDomainNames(...))` → `AddKeyedSingleton<EventStoreDomainDiagnostics>(domain)`) **exists only in EventStore DomainService source `v3.43.0`** (the Folders-repo submodule `963402c5`), **but is absent from the published `Hexalith.EventStore.DomainService` NuGet package `3.42.0` that the Tenants server actually builds against.** The Tenants server (a cross-repo submodule; its own nested EventStore submodule is *not* initialized per repo policy, and it builds in NuGet mode — `UseHexalithProjectReferences=false`) resolves `Hexalith.EventStore.DomainService/3.42.0` (verified in its `project.assets.json`), so `AddEventStoreDomainService` never registers the keyed `"tenants"` diagnostics → the `TenantTelemetry` factory throws in the SDK's startup `ValidateDomainQueryHandlerRoutes`, unhandled → exit 134.

**Definitive root cause: a Tenants-host ↔ published-EventStore-package version skew.** The Tenants host code (`v2.3.0-9`) got ahead of the published `Hexalith.EventStore.DomainService` package (`3.42.0`); the required registration only exists in the *unpublished* EventStore source `v3.43.0`. Bumping the EventStore **submodule** (which the `aspire update` did) does **not** help, because the AppHost-launched Tenants server links the **package**, not that source. Not Folders code, not load, not the Aspire version. Deterministic on any host.

**Fix (platform decision, owner Jerome/platform), any one of:** (a) publish `Hexalith.EventStore.DomainService 3.43.0` and bump the Tenants server's package pin to it; (b) build the AppHost-launched Tenants server in source mode (`UseHexalithProjectReferences=true`) against the Folders-repo EventStore submodule `v3.43.0-16`; or (c) pin the Tenants submodule to a host version whose telemetry expectations match the published `3.42.0` SDK.

### Artifact / status impact
- `sprint-status.yaml:280` DCP-lane action → **partially satisfied** (lane stood up + operational; two blockers fixed) with a **new follow-up** (Blocker C). Epic 9 AC6 live-boot, Story 10.3 D1#3, 10.4 AC9, 10.5 AC15 remain **owed** until Blocker C clears and the round-trip runs green.
- **Concurrency caveat (important):** this working tree is being edited by **other concurrent sessions** (Epic 11 refactor). During this run the Tenants fix was committed by another session (`d923393`), and `references/Hexalith.Memories` + several `Hexalith.Folders.Server` files show unrelated in-flight changes. **No sprint-status/epics edits were made here** to avoid clobbering concurrent work.

---

## Section 3 — Recommended Approach

**Direct adjustment**, in three moves:

1. **Keep both fixes.** Blocker A is already committed upstream in the submodule (`d923393`) — good. **Commit the AppHost SDK bump** (`13.4.6`) in the Folders repo; it is the substantive unblock and is verified.
2. **Open a focused defect for Blocker C** (`tenants` exit 134 on AppHost boot) — **root cause pinned**: align the **EventStore ↔ Tenants submodule versions** so `AddEventStoreDomainService` registers the keyed `EventStoreDomainDiagnostics("tenants")` that Tenants `Program.cs:67` requires (bump the EventStore submodule to the version that ships the keyed-diagnostics registration, or align Tenants to the EventStore SDK's current diagnostics API — a platform decision). Then land the AC9 green (the DCP lane itself is proven, so this is the last gate).
3. **Harden against regression:** add a build/test guard asserting `Aspire.AppHost.Sdk` version == `Aspire.Hosting` package version (this exact drift is what cost Epic 9 its live-boot half).

Effort: fixes are done/one-line; Blocker C triage is bounded (needs a quiet host + dashboard). Risk: low for the fixes; Blocker C is the only thing between here and the AC9 green.

---

## Section 4 — Detailed Change Proposals

### 4.1 `references/Hexalith.Tenants/src/Hexalith.Tenants.Aspire/TenantsServerProjectMetadata.cs` — DONE (submodule `d923393`)
Replace the 2-branch `GetProjectPath` probe with `RepositoryProjectPaths.GetReferencedModuleProjectPath("Hexalith.Tenants","src","Hexalith.Tenants","Hexalith.Tenants.csproj")` (references-aware; mirrors EventStore/Memories). Rationale: resolves the `references/` submodule layout Folders uses. **Upstream to `Hexalith.Tenants` main** — platform-wide fix.

### 4.2 `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` — APPLIED (uncommitted)
```
- <Project Sdk="Aspire.AppHost.Sdk/13.3.5">
+ <Project Sdk="Aspire.AppHost.Sdk/13.4.6">
```
Rationale: align the SDK-staged DCP (→ 0.24.3) with the `Aspire.Hosting` 13.4.6 package so `dcp start-apiserver --tls-cert-file` is accepted. This is the actual resolution of the Epic 9 "DCP `--tls-cert-file`" residual.

### 4.3 NEW defect — `tenants` domain-service aborts (exit 134) on AppHost boot — ROOT CAUSE PINNED
On a DCP-capable host the lane boots the topology but `tenants` exits 134 ~2 s post-launch (eventstore OK). Cause: the Tenants host (`v2.3.0-9`, `Program.cs:67`) resolves the keyed `EventStoreDomainDiagnostics("tenants")`, but the running server links the published **`Hexalith.EventStore.DomainService` package `3.42.0`** (verified in `project.assets.json`; the nested EventStore submodule is not initialized, NuGet mode), whose `AddEventStoreDomainService` does **not** register that keyed diagnostics — the registration exists only in EventStore **source `v3.43.0`** (Folders-repo submodule `963402c5`, unpublished). Bumping the EventStore submodule does not help because the Tenants server links the package, not the source. **Fix (platform, owner Jerome), any one:** (a) publish `Hexalith.EventStore.DomainService 3.43.0` + bump the Tenants package pin; (b) source-mode build (`UseHexalithProjectReferences=true`) the AppHost-launched Tenants server against the EventStore submodule `v3.43.0-16`; (c) align the Tenants host version to the published `3.42.0` SDK. Acceptance: the lane reaches all-6-Running and `SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` passes (SearchIndexEntryChanged seed → search hit, SearchIndexEntryRemoved → 0, archived-status filter). **Independent of Folders code.**

### 4.4 Regression guard (recommended)
Add a lightweight test/MSBuild check asserting `Aspire.AppHost.Sdk` == `Aspire.Hosting` version so SDK/package drift can never silently re-block the live lane.

---

## Section 5 — Implementation Handoff

- **Developer:** commit 4.2 (AppHost SDK bump) in Folders; confirm `references/Hexalith.Tenants` pointer bump to `d923393` is committed in the parent **separately from** the concurrent Epic 11 changes; add 4.4.
- **PO / Jerome:** upstream `d923393` to `Hexalith.Tenants` main; schedule the Blocker-C re-run on a quiet DCP host; reconcile sprint-status once Blocker C clears (deferred here due to concurrent edits).
- **Success criteria:** on a quiet DCP host, the opt-in lane boots all 6 resources to Running and `SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` passes — closing Epic 9 AC6 live-boot, 10.3 D1#3, 10.4 AC9, 10.5 AC15.

### Verification log (this session)
- Build: `Hexalith.Folders.AppHost.Tests` (+ cross-repo servers) 0 warn / 0 err, both fixes compile.
- run#1 → Blocker A (composition, `Program.cs:42`). run#2/#5 → Blocker B symptom (`EnsureKubernetes` 20 s). direct-run → Blocker B root cause (`unknown flag: --tls-cert-file`, ExitCode 1). run#6 (post-fixes) → apiserver + dashboard + containers + 6 services up; eventstore Running; `tenants` exit 134 (Blocker C).
- Hermetic behaviour unchanged: without the opt-in env var the harness skips (env-var gate untouched), so normal CI stays green.
