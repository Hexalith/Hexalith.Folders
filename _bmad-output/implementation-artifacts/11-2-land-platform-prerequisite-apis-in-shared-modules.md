---
baseline_commit: c953461e0943415e4c2b4be258233008339486c7
---

# Story 11.2: Inventory, assign, and pin platform prerequisites

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform maintainer,
I want each required Commons, EventStore, FrontComposer, and Memories prerequisite assigned and pinned,
so that Folders consumes released shared capabilities without claiming ownership of upstream implementation.

This is the **platform-first (Phase B) prerequisite story** for Epic 11. It changes **zero product wire behavior** in Folders. Its Folders-repo footprint is limited to: (a) a machine-checkable **prerequisite API availability manifest**, (b) a written G4/G5 proposal, and (c) `references/**` gitlink and/or Builds `Directory.Packages.props` bumps **only when a missing seam is already landable**. This story does **not** implement upstream code, mutate pins without a landable API, or claim any product projection complete.

**Execution model — RATIFIED `Model B` (confirm + spec + pin) by Jerome, 2026-07-08.** Deliverables: (1) **confirm** already-present seams; (2) **specify** each missing seam exactly (YAML `required_behavior` + §Consuming-Side Contracts); (3) **open/track one per-repo platform story per gap**; (4) **pin** only what is already landable and record empty gitlink/Builds changes honestly. Upstream implementation and Folders adoption are later gated work (11.6–11.12, 11.14, 11.17, 11.19). Task 0 decisions (AppHost/Aspire+ServiceDefaults; reserved-tenant) are **ratified**; ADR authorship is Story **11.20**.

## Acceptance Criteria

**AC1 (epic-canonical).**
**Given** the audit platform gaps G1–G9
**When** the prerequisite inventory is reconciled
**Then** every capability has an owning repository, upstream issue/story reference, required release/version or SHA, availability status, consuming Folders story, and verification evidence
**And** this story records pin evidence only; it does not implement upstream code, mutate dependency pins without separate authorization, or claim any product projection complete.

**AC2 — Confirm what already exists (no upstream work).** For every register row marked **CONFIRM**, the manifest records the exact public type, namespace, project, pinned SHA, and consuming Folders story. No re-implementation is authored for a CONFIRM row. A CONFIRM that is not Folders-consumable through Builds `PackageVersion` rows is recorded as an owned packaging blocker, not as a silent skip. (Memories event contracts; `Commons.Publication`; `UniqueIdHelper`; `Commons.TenantAccess`; `Commons.ServiceDefaults`; EventStore cursor/read-model/admission/mapping/fake/diagnostics; FrontComposer Shell user-context/token-relay/OIDC/skeleton/empty-state/**banner**.)

**AC3 — Memories seams (G1).** The manifest specifies, and tracks to an owned Memories issue, a **producer publish seam** (`IIndexEventPublisher` or equivalent **plus** a Dapr CloudEvents publish helper in a project `Hexalith.Folders.Workers` already references — `Hexalith.Memories.Contracts`, not `.Client.Rest`); a **public** curated event-type constant surface reachable from Contracts/Client; and a **resilient search wrapper** in `Hexalith.Memories.Client.Rest`. Binding shapes are §Consuming-Side Contracts (composite AggregateId, `cloudevent.id/type/source`, `pubsub`/`memories-events`, 2s linked timeout → `Timeout`, `SearchRequest` taxonomy). This story does **not** require those types to exist at the current pin.

**AC4 — EventStore seams (G6/G8/G9 + confirm cursor/read-model/test).** The manifest specifies owned EventStore issues for: classified `ISecretStoreClient` (`Found/Missing/Denied/Unavailable`, `secretStoreName`/`credentialReferenceId`, empty dict → Missing, `PermissionDenied` → Denied, other Dapr fault → `Unavailable(30s)`); consumable auth registration via **`AddEventStoreDaprServiceInvocation`** (not a host-local `DaprAppIdHandler`, which EventStore AD-18 forbids) plus **promote** `InboundBearerForwardingHandler` from Sample.Api into `Hexalith.EventStore.Client`, plus JWT-hardening/`eventstore:*` accessors in DomainService; and `Eventually.UntilAsync<T>(Func<CancellationToken,Task<T>>, Predicate<T>, TimeSpan timeout, TimeSpan interval, CancellationToken)` with timeout/interval `> TimeSpan.Zero` and timeout → `TimeoutException` distinct from caller cancel. Cursor/read-model/admission/mapping/fake/diagnostics seams are **confirmed** at the pin. Missing CREATE/PROMOTE seams are owned blockers, not claimed present.

**AC5 — Commons helpers (P6/P7/P8/P9 + G7).** The manifest specifies owned Commons issues (plus package publish + Builds pin as the consumption mechanism) for `SensitiveValueDetector`, `DeterministicHashBuilder`, `AuthorizedBaseUrl`, correlation `SanitizeOrCreate`, a bearer `DelegatingHandler` that **fail-closed rejects** non-HTTPS non-loopback targets **before** token emission (loopback = `IPAddress.IsLoopback` or hostname `localhost` ordinal-ignore-case), and a canonical cursor paging envelope with offset `PagedResult<T>` de-duplication. `Commons.ServiceDefaults` and `UniqueIdHelper` are confirmed **and** Builds-consumable. `TenantAccess` / `Publication` source CONFIRM is not Folders-consumable until Builds `PackageVersion` rows exist.

**AC6 — FrontComposer helpers (G9).** The manifest specifies an owned FrontComposer issue for: `FcFluentIcons.LockClosed16`/`Clock16` matching Folders **hand-authored vector paths** (Fluent icon **package** stays off the Folders graph per Story 6.4 AC#7); `FcSafeCopy` matching `SafeCopyId.razor` **without** the five mutation selectors `form`, `fluentinputform`, `fluentdialog`, `[data-fc-command]`, `[data-fc-mutation]`; an RFC-7807 extension parser; and public `HermeticTestAuthenticationHandler` in `Hexalith.FrontComposer.Testing` (scheme `HermeticTest`, bearer `hermetic-test-token` → `tenant_id=tenant-a` / `NameIdentifier=user-a`, Development/Test-only, production boot reject). This story does **not** require those types at the current pin.

**AC7 — G4/G5 are proposal-only.** `Commons.Cli` / `Commons.Mcp` shared scaffolding is a **written proposal** with harmonized credential precedence, **not** implementation. Folders Story 11.6 stays in-repo; Commons adoption is deferred. No Folders CLI/MCP code changes in this story.

**AC8 — Honest pinning.** Availability changes reach Folders only through the correct mechanism (sibling-source gitlink vs Commons package + Builds pin). A bump is a separate conventional commit (`build(deps):` per current commitlint; never invent a pin). If nothing is landable, record `no_dependency_change` and **do not** check bump subtasks as done. Do not revert Story 11.1 §10 pointer drift; no nested/recursive submodule init.

**AC9 — Behavior-equivalent, gate-green, honest-green preserved.** Restore/build `Hexalith.Folders.slnx` Release is 0W/0E; focused lanes and `ScaffoldContractTests` stay green; Folders-owned `dotnet format` verify is clean; no REST/OpenAPI/envelope/ProblemDetails/parity behavior changes (Folders does not consume the new APIs here — that is 11.6–11.12, 11.14, 11.17, 11.19). Honest-green baseline untouched. When no pin bump occurs, these checks still prove the baseline; they do not prove new APIs landed.

**AC10 — Evidence manifest.** A committed YAML artifact records, per gap G1–G9 and P6–P9: disposition, exact upstream type/namespace/project, SHA or Commons/Builds pin, **consuming Folders story**, verification result, and any owned blocker. Never silently skip a seam.

## Tasks / Subtasks

> **Model B semantics (ratified):** for every **CREATE/PROMOTE/MOVE/DEDUPE** row below, this story's job is to **specify** it precisely (YAML `required_behavior` + §Consuming-Side Contracts) and **open a per-repo platform story** in the owning submodule — *not* to author it in this Folders session. **Confirm + pin** anything already landable; record the rest as owned blockers. Subtask text is the spec each per-repo story implements.

- [x] **Task 0 — Ratify the two remaining open decisions (AC1).**
  - [x] ~~Execution model~~ — **DECIDED: Model B**, ratified by Jerome 2026-07-08. No in-session upstream authoring.
  - [x] Ratify Step-4 decision (a): keep AppHost/Aspire as the sanctioned local/test exception; delete `Hexalith.Folders.ServiceDefaults` in Story 11.9. **ADR owner: Story 11.20** (epic split superseded the 2026-07-14 11.13 assignment; decision content unchanged).
  - [x] Ratify Step-4 decision (b): strict ordinal/no-trim `system` reserved-tenant. **ADR owner: Story 11.20.** Implementation stays with owning product/ADR stories — **not** Story 11.5 consolidation (`epics.md` 11.5 AC).
- [x] **Task 1 — CONFIRM the already-present seams (AC2).** Record exact type/namespace/project/SHA/consuming story. Author **no** code. Include FrontComposer banner. Record Builds `PackageVersion` holes for Publication/TenantAccess as owned blockers.
- [x] **Task 2 — Specify Memories G1 seams (AC3).** Binding spec only; implementation is Memories issue 28.
  - [x] Specify the producer publish seam (`IIndexEventPublisher` + Dapr CloudEvents helper) in `Hexalith.Memories.Contracts` (Workers-reachable). Match §Consuming-Side producer shape. Do **not** place the helper in `.Client.Rest`.
  - [x] Specify `CuratedSearchIndexEventTypes` public and reachable from Contracts/Client.
  - [x] Specify the resilient search wrapper in `Hexalith.Memories.Client.Rest`: 2s linked-CTS → `Timeout`; `SearchRequest(TenantId, Axis, Query, MaxResults, Offset, AttributeFilters)`; `MemoriesRemoteException`/`HttpRequestException`/`InvalidOperationException`/non-caller cancel → `Unavailable`; rethrow caller cancel; in-band `Degraded`/`UnavailableAxes`; `SourceUri` identity. Keep throwing `SearchAsync`.
  - [ ] Commit inside Memories and bump `references/Hexalith.Memories` — **not done.** No landable pin; recorded as owned blocker.
- [x] **Task 3 — Specify EventStore G6/G8/G9 (AC4).** Binding spec only; issues 283/284/285.
  - [x] G8: `ISecretStoreClient` in Client — `GetSecretAsync(secretStoreName, credentialReferenceId, metadata, ct)`; empty dict → Missing; `PermissionDenied` → Denied; other Dapr fault → `Unavailable(30s)`; caller cancel propagates.
  - [x] G6: public registration **`AddEventStoreDaprServiceInvocation`** (AD-18; do not recreate host-local `DaprAppIdHandler`); **promote** `InboundBearerForwardingHandler` from Sample.Api into Client; JWT-hardening + `eventstore:*` accessors into DomainService.
  - [x] G9: `Eventually.UntilAsync<T>(Func<CancellationToken, Task<T>> probe, Predicate<T> isReady, TimeSpan timeout, TimeSpan interval, CancellationToken ct = default)` in Testing; timeout/interval `> TimeSpan.Zero`; probe exceptions propagate; timeout → `TimeoutException` distinct from caller cancel.
  - [ ] Commit inside EventStore and bump `references/Hexalith.EventStore` — **not done.** No landable pin; recorded as owned blocker.
- [x] **Task 4 — Specify Commons P6/P7/P8/P9 + G7 (AC5).** Binding spec only; issues 19–23. Consumption requires package publish + Builds pin.
  - [x] Specify `SensitiveValueDetector`, `DeterministicHashBuilder`, `AuthorizedBaseUrl`, `SanitizeOrCreate`, and bearer handler **fail-closed reject** of non-HTTPS non-loopback (`IPAddress.IsLoopback` or hostname `localhost`) **before** token emission; blank token omitted.
  - [x] G7: canonical cursor envelope; de-duplicate offset `PagedResult<T>`.
  - [ ] Publish Commons + Builds `PackageVersion` bump — **not done.** No landable pin; recorded as owned blocker.
- [x] **Task 5 — Specify FrontComposer G9 (AC6).** Binding spec only; issue 60.
  - [x] Specify `LockClosed16` + `Clock16` as Folders vector-path equivalents (not Fluent icon package).
  - [x] Specify `FcSafeCopy` with `fc-safe-copy*` / `data-testid="safe-copy"` and **none** of `form`, `fluentinputform`, `fluentdialog`, `[data-fc-command]`, `[data-fc-mutation]`.
  - [x] Specify RFC-7807 extension parser (bounded, no raw-body leakage).
  - [x] Specify public `HermeticTestAuthenticationHandler` (scheme `HermeticTest`, token `hermetic-test-token`, claims `tenant_id=tenant-a` / `NameIdentifier=user-a`, Development/Test only, production boot reject).
  - [ ] Commit inside FrontComposer and bump `references/Hexalith.FrontComposer` — **not done.** No landable pin; recorded as owned blocker.
- [x] **Task 6 — G2 Aspire drift (optional).** Record owned follow-up: Commons issue 24, EventStore 286, Memories 29. Does not gate a Folders product story.
- [x] **Task 7 — G4/G5 proposal only (AC7).** Written proposal; Commons issues 25 and 26. No Folders CLI/MCP edits.
- [x] **Task 8 — Pin, verify, and record evidence (AC8, AC9, AC10).**
  - [x] No honest `build(deps):` bump available; recorded `pin_outcome.status: no_dependency_change`.
  - [x] Baseline Release restore/build 0W/0E; focused lanes + Scaffold 11/11; Folders-owned format verify; honest-green untouched; no nested submodules.
  - [x] Evidence manifest with consuming Folders story per capability, owned blockers, and BoundedTelemetry naming note.

### Review Findings

Source: `bmad-code-review` 2026-09-06, four layers (blind-hunter, edge-case-hunter, verification-gap, acceptance-auditor) over `8188d97` (Story 11.2 Model B evidence). Verification-gap found no runtime-test gaps in this docs-only diff.

**Decisions resolved 2026-09-06 (Jerome: apply recommendations):** dedicated ACs rewritten to inventory-and-assign; ADR owner 11.20 / reserved-tenant not 11.5; credential stdin/secret-agent + inline JSON placed in the numbered list; P6 loopback = `IPAddress.IsLoopback` or hostname `localhost`; CLI 429 maps to `UnavailableOrReconciliationRequired` (no new `RateLimited` outcome).

- [x] [Review][Decision] Canonical acceptance set for closing Story 11.2 — **RESOLVED: rewrite dedicated ACs** to inventory-and-assign. AC3–AC6 no longer require APIs at the current pin.
- [x] [Review][Decision] ADR and reserved-tenant owners — **RESOLVED: align to `epics.md`.** ADR owner Story 11.20; reserved-tenant implementation stays with product/ADR stories, not Story 11.5.
- [x] [Review][Decision] Optional credential sources — **RESOLVED:** stdin/secret-agent after `--token-file`; opt-in inline JSON after secure file and before workload identity.
- [x] [Review][Decision] P6 loopback host set — **RESOLVED:** `IPAddress.IsLoopback` or hostname `localhost` (ordinal ignore-case).
- [x] [Review][Decision] CLI HTTP 429 / RateLimited — **RESOLVED:** no new `RateLimited` outcome; map 429 to `UnavailableOrReconciliationRequired`.

- [x] [Review][Patch] G1 YAML is not a Workers-reachable drop-in spec [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:282-331`]
- [x] [Review][Patch] G9 required_behavior contradicts AC6 / Story 6.4 vector-path and five-selector contracts [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:476-497`]
- [x] [Review][Patch] Commons.Publication and Commons.TenantAccess are marked verified but have no Builds PackageVersion rows [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:72-118`]
- [x] [Review][Patch] EventStore G8/G9 required_behavior omits Folders consuming-side details [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:355-385`]
- [x] [Review][Patch] G6 names a vague “or equivalent” seam instead of `AddEventStoreDaprServiceInvocation` [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:333-353`]
- [x] [Review][Patch] Availability rows omit the consuming Folders story [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:55`]
- [x] [Review][Patch] Pin-bump subtasks are checked complete despite zero dependency commits [`_bmad-output/implementation-artifacts/11-2-land-platform-prerequisite-apis-in-shared-modules.md:61`]
- [x] [Review][Patch] P6 bearer guard says attach-only rather than fail-closed reject before token emission [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:402`]
- [x] [Review][Patch] Credential Denied/Unavailable must stop the first-nonblank chain [`_bmad-output/implementation-artifacts/11-2-commons-cli-mcp-proposal.md:75`]
- [x] [Review][Patch] CLI/MCP proposal leaves Conflict/NotFound/Degraded, Sysexits defaults, cancel/exception, and bootstrap exits unmapped [`_bmad-output/implementation-artifacts/11-2-commons-cli-mcp-proposal.md:104`]
- [x] [Review][Patch] `--token-file` / process I/O secret rules are not MUST constraints [`_bmad-output/implementation-artifacts/11-2-commons-cli-mcp-proposal.md:14`]
- [x] [Review][Patch] FrontComposer Shell CONFIRM omits `FcExpandedRowHiddenBanner` present at the pinned SHA [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:245`]
- [x] [Review][Patch] File List claims `sprint-status.yaml`, which is not in `8188d97` [`_bmad-output/implementation-artifacts/11-2-land-platform-prerequisite-apis-in-shared-modules.md:239`]
- [x] [Review][Patch] Manifest omits the BoundedTelemetry naming note the register assigns to Story 11.8 [`_bmad-output/implementation-artifacts/11-2-land-platform-prerequisite-apis-in-shared-modules.md:117`]

- [x] [Review][Defer] “Python YAML/schema assertions” have no schema or script in the File List [`_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml:647`] — deferred: maybe-false, would be medium; settle from the 2026-07-14 Task 1 command/script (if none existed, `verification_runs` overclaims)

#### Rejected

- `false` Stale Prerequisite API Register vs YAML — fix would edit the spec under review; YAML is the AC10 evidence.
- `false` Unsubstituted `{user_name}` / leftover “Step-4 remain open” prose — spec-edit; Task 0 YAML decisions already record ratification.
- `false` Task 3 still says create `DaprAppIdHandler` from Admin.UI / `InboundBearerForwardingHandler` from nothing — spec-edit; YAML already records internal `DaprServiceInvocationHandler` and Sample.Api-only `InboundBearerForwardingHandler`.
- `false` G4/G5 artifact is a proposal not an ADR — AC7 allows a written proposal/ADR; Status: Proposed matches.
- `false` Dev Notes still cite EventStore 3.42.0/3.43.0, Commons 2.27.0, AppHost 3 SKIP, Scaffold 10/10 — spec-edit; YAML `verification_runs` already records 4 skips and Scaffold 11/11.
- `false` Release restore used `-p:NuGetAudit=false` — documented fallback; AC9 does not forbid it.
- `false` Search wrapper catch-all would swallow caller cancel — YAML `G1-resilient-search-wrapper.required_behavior` already requires caller cancellation propagation.
- `false` Secret-store blank Found values must be Missing — Folders `DaprProviderCredentialSecretStoreClient` maps only empty dictionaries to Missing; YAML matches that contract.
- `false` G6 must ship a public `DaprAppIdHandler` type — EventStore pin forbids host-local `DaprAppIdHandler` (AD-18); YAML’s equivalent registration seam is the correct re-audit.
- `low` Non-interactive stdout should default `--output` to json — table already allows adapters to pin; adding a new default is extra behavior on a proposal-only artifact.

## Dev Notes

### Why this story exists (root cause)
`src/Hexalith.Folders/Hexalith.Folders.csproj` references **only** `Hexalith.Folders.Contracts` + `Dapr.Client` + `Octokit` + four `Microsoft.Extensions.*` packages — **neither `Hexalith.Commons.*` nor `Hexalith.EventStore.*`**. That isolation forced Folders to re-implement platform behavior locally (TenantAccess, cursor codecs, read-model stores, secret client, telemetry, correlation/secret/hash helpers, a Dapr index publisher, UI shell/auth/icons). Epic 11 deletes those copies — but only **after** the shared modules expose the primitives. This story lands/confirms those primitives and pins them. [Source: `fable_Folders_changes.md` §4.1, §11; `11-1-…-governance-pin-map.md` §4]

### Consumption mechanism — the load-bearing detail (AC8)
Two different pathways; using the wrong one means the new API never reaches Folders:

| Module | How Folders consumes it | To make a new upstream API available |
| --- | --- | --- |
| **EventStore, Memories, FrontComposer, Tenants** | **Project reference to sibling submodule source**, located by `Hexalith{X}Root` props in `Directory.Build.props` (e.g. `Hexalith.Folders.UI.csproj` → `$(HexalithFrontComposerRoot)\src\…Shell`). | **Bump `references/<module>` pin** (`chore(deps):`). Source is picked up directly — no package publish needed. |
| **Commons** | **NuGet packages** — versions in `references/Hexalith.Builds/Props/Directory.Packages.props` (`Hexalith.Commons* = 2.27.0`). There is **no `HexalithCommonsRoot`**; editing `references/Hexalith.Commons` source has **no effect** on the Folders build. | **Publish the Commons package**, update the `Hexalith.Commons*` version in the Builds central props, and **bump `references/Hexalith.Builds`** (`chore(deps):`). |

Central-package fallback pins exist for `Hexalith.EventStore*` (3.42.0), `Hexalith.Memories*`, `Hexalith.Commons*` (2.27.0) etc. in the Builds props (70 `Hexalith.*` `PackageVersion` entries). **Blocker C caveat:** EventStore is source-consumed at v3.43.0-16 while its Builds package pin is 3.42.0 — a real source/package skew (the DCP-lane `EventStore.DomainService` keyed-diagnostics issue). When adding EventStore G6/G8/G9 APIs, reach Folders through the **source pin** (the projects Folders references consume EventStore source) and do not widen the 3.42.0-package vs v3.43.0-source API gap. [Source: `Directory.Build.props`; `references/Hexalith.Builds/Props/Directory.Packages.props`; memory `dcp-lane-standup`]

CI checks out with `submodules: false` (12 occurrences) and has **no PackageReference fallback** for the sibling-source modules. This story adds **no new consumption** in Folders, so CI is unaffected. The caveat is owned by the *consuming* stories (11.8–11.12), which must ensure the adopted APIs ship in a way CI can resolve (published packages, or the CI submodule policy is revisited). Record but do not solve here. [Source: `11-1-…-pin-map.md` §6]

### Prerequisite API Register (the heart of the story — verified at current pins)
Pins verified: Commons `20048e9` (v2.27.0, **package-consumed**), EventStore `963402c5` (v3.43.0-16, source), FrontComposer `f61c6a8a` (v1.6.1-2, source), Memories `deb9dd7` (v1.44.0-19, source). Re-verify each row with a fresh grep before editing.

| Gap | API / seam | Verdict @ pin | Current home | Action → target home |
| --- | --- | --- | --- | --- |
| **G1** | `IIndexEventPublisher` + Dapr publish helper | **MISSING** | — (only a Tenants *sample* clone) | **CREATE** → `Memories.Contracts` (Workers-reachable) or `.Client.Rest` |
| G1 | Producer event-type constants `CuratedSearchIndexEventTypes` | **PARTIAL** — `internal`, wrong project | `Hexalith.Memories.EventStore` | **PUBLIC + RELOCATE** → `Memories.Contracts`/`.Client` |
| G1 | `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` records | **EXISTS/PUBLIC** | `Hexalith.Memories.Contracts.V1` | **CONFIRM** |
| G1/G9 | Resilient Memories search wrapper | **MISSING** (only throwing `MemoriesClient.SearchAsync`) | — | **CREATE** → `Memories.Client.Rest` |
| G1 opt | `PublicationTransportMetadata` / `PublicationDeduplicationSet` / composer | **EXISTS/PUBLIC** (0 consumers) | `Hexalith.Commons.Publication` | **CONFIRM** (optional layering) |
| **G6** | `DaprAppIdHandler` | EXISTS wrong-home | `EventStore.Admin.UI` | **PROMOTE** → `EventStore.Client` |
| G6 | `InboundBearerForwardingHandler` | **MISSING** | — | **CREATE** → `EventStore.Client` |
| G6 | JWT-hardening + `eventstore:*` claim accessors | EXISTS gateway/host-only | `Hexalith.EventStore`/`Admin.Server` | **PROMOTE** → `EventStore.DomainService`/shared-auth |
| **G8** | `ISecretStoreClient` (Found/Missing/Denied/Unavailable) | **MISSING** | — | **CREATE** → `EventStore.Client` |
| **G9** | `Eventually` async-poll | **MISSING** | — | **CREATE** → `EventStore.Testing` |
| — | `IQueryCursorCodec`, `IReadModelStore`, `ReadModelWritePolicy`, `AddEventStoreDataProtection`, `IDomainServiceAdmissionStage`, `MapEventStoreDomainEvents`, `FakeEventStoreGatewayClient`, `EventStoreDomainDiagnostics` | **EXISTS** | EventStore.Client/DomainService/Testing | **CONFIRM** (prereqs for 11.10) |
| — | `BoundedTelemetry` (named) | **MISSING (as named)** | — | telemetry via `AddEventStoreDomainTelemetry`/`EventStoreDomainDiagnostics` — record naming note; Story 11.8 (P3) target |
| **P7** | `SensitiveValueDetector` | **MISSING** | — | **CREATE** → `Commons` |
| **P8** | `DeterministicHashBuilder` | **MISSING** | — | **CREATE** → `Commons` |
| **P9** | `AuthorizedBaseUrl` | **MISSING** | — | **CREATE** → `Commons` |
| **P6** | correlation `SanitizeOrCreate` | **MISSING** (`HttpCorrelation.ResolveCorrelationId` ≠ this) | `Commons.Http` | **CREATE** → `Commons`/`Commons.Http` |
| — | bearer `DelegatingHandler` + https/loopback guard | **MISSING** | — | **CREATE** → `Commons.Http` |
| **G7** | canonical **cursor** paging envelope | **MISSING** (only offset shapes) | — | **CREATE** → `Commons` (or `EventStore.Contracts`) |
| G7 | two verbatim `PagedResult<T>` copies | EXISTS ×2 (offset) | `Commons.Paging` + `Commons.Http` | **DEDUPE** |
| **G3** | `Commons.ServiceDefaults` (`HexalithServiceDefaults`) | **EXISTS** | `Commons.ServiceDefaults` | **CONFIRM** (11.9 consumes) |
| P1 | `TenantAccess` primitives | **EXISTS** (0 consumers) | `Commons.TenantAccess` | **CONFIRM** |
| — | `UniqueIdHelper` (ULID) | **EXISTS** | `Commons.UniqueIds` | **CONFIRM** |
| **G2** | Aspire-Dapr drift (5 files + `RepositoryProjectPaths.cs`) | drifted copies | `Commons.Aspire` / `EventStore.Aspire` | **RECONCILE** → `Commons.Aspire` (platform-internal; optional) |
| **G9** | `FcFluentIcons.LockClosed16` / `Clock16` | **MISSING** (`FcFluentIcons` exists) | `FrontComposer.Shell` | **ADD** |
| G9 | `FcSafeCopy` | **MISSING** | — | **CREATE** → `FrontComposer.Shell` |
| G9 | RFC-7807 extension **parser** | **MISSING** (payload record + `Commons.Http` reader = prior art) | — | **CREATE** → `FrontComposer.Shell` |
| G9 | `HermeticTestAuthenticationHandler` | **MISSING in FC** (dest project exists) | Folders `UI/CompositionRoot.cs` (`private`) | **MOVE + promote** → `FrontComposer.Testing` |
| — | FC Shell user-context / token-relay / OIDC / skeleton / empty-state / banner | **EXISTS** | `FrontComposer.Shell` | **CONFIRM** (11.11 consumes) |
| **G4/G5** | `Commons.Cli` / `Commons.Mcp` scaffolding | n/a | — | **PROPOSAL/ADR only** (impl trails; 11.6 stays in-repo) |

### Consuming-Side Contracts (exact shapes the upstream must satisfy)
The dev agent must make the upstream API match these so the later deletion stories are drop-in. All are `internal` in Folders today.
- **Producer** — `src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs` (`internal sealed : ISemanticIndexingPort`): composite `AggregateId = "{managedTenantId}/{organizationId}/{folderId}/{fileVersionId}"`; `BuildMetadata(id,type)` → `{cloudevent.id, cloudevent.type=nameof(...), cloudevent.source}`; `DaprClient.PublishEventAsync(PubSubName, EventsTopicName, entry, metadata, ct)`; retryable classification (caller-cancel rethrow; other cancel → retryable; `DaprException` → retryable). Constants in `FoldersSemanticIndexingDefaults` (`CloudEventsSource="hexalith-folders"`, `PubSubName="pubsub"`, `EventsTopicName="memories-events"`, `StatusAttributeKey/StatusActive/StatusArchived`). The `pubsub`+`memories-events` pair is Memories-owned; `cloudevent.source` is producer-specific. **Keep the Folders mapping (composite id, hybrid hard/soft delete, `folders.status`) in Folders — move only the publish primitive.**
- **Search consumer** — `src/Hexalith.Folders.Server/ContextSearch/MemoriesFolderSearchSource.cs` (`internal sealed : IFolderSearchSource`): 2s linked-CTS timeout → `Timeout`; `SearchRequest(TenantId, Axis, Query, MaxResults, Offset, AttributeFilters)`; `IsMemoriesUnavailable` switch (`MemoriesRemoteException`/`HttpRequestException`/`InvalidOperationException`/non-caller cancel) → `Unavailable`; in-band `Degraded || UnavailableAxes.Contains(axis)`; identity from `ScoredResult.SourceUri` only. **Wrapper → Memories; the folder security-trim + `folders.*` mapping stays in Folders.**
- **Secret client** — `src/Hexalith.Folders/Providers/…`: `IProviderCredentialSecretStoreClient.GetSecretAsync(secretStoreName, credentialReferenceId, metadata, ct)` → `ProviderCredentialSecretLookupResult` with `{ Found, Missing, Denied, Unavailable }` + `RetryAfter`; `DaprProviderCredentialSecretStoreClient` classifies `RpcException{PermissionDenied}` → `Denied`, other `DaprApiException` → `Unavailable(30s)`, empty dict → `Missing`.
- **Eventually** — `src/Hexalith.Folders.Testing/Polling/Eventually.cs`: `public static Task<T> UntilAsync<T>(Func<CancellationToken,Task<T>> probe, Predicate<T> isReady, TimeSpan timeout, TimeSpan interval, CancellationToken ct=default)`; linked-CTS, `Task.Delay(interval)`, `TimeoutException` on timeout (distinct from caller cancel).
- **UI copy targets** — `FoldersConsoleIcons.LockClosed16()/Clock16()` vector paths (Fluent icon package is deliberately off the reference graph per Story 6.4 AC#7 — the upstream `FcFluentIcons` members must supply equivalent paths); `SafeCopyId.razor` + `CorrelationCopyButton.razor` (`fc-safe-copy*`, `data-testid="safe-copy"`, must not trip the 5-selector command-suppression guard). No RFC-7807 parser exists in `Folders.UI` (parsers live in Client/Cli/Mcp) — the FrontComposer addition has no UI-local copy to delete.
[Source: three prerequisite-audit passes over `references/**` @ current pins + `src/Hexalith.Folders.*`, 2026-07-08]

### What must NOT change (wire preservation + lockstep)
- **Zero product wire change.** Folders does not consume the new APIs in this story (that's 11.8–11.12). No `src/` behavior, `.slnx`, workflow, conformance-class, OpenAPI, parity-fixture, or docs-gate edits. The only Folders-repo changes are `references/**` gitlinks (+ Builds `Directory.Packages.props` for Commons) and the evidence manifest.
- **Do not touch the honest-green baseline.** This story edits no CI/gate files; keep it that way. (`E2eCiWorkflowConformanceTests`, `AccessibilityCiWorkflowConformanceTests`, `HonestGreenGateBaselineConformanceTests`, the full-63 lane, AD7 set — all untouched.) [Source: `11-1-…-pin-map.md` §9–§10]
- **Do not revert** the intentional EventStore/Memories/Tenants submodule pointer drift; **no** `git submodule update --init --recursive`; initialize only root-declared `references/` modules. [Source: `CLAUDE.md`; `project-context.md` §Development Workflow]
- **Submodule edits are authorized by this story.** `CLAUDE.md` treats sibling modules as read-only *unless the task explicitly asks to modify a submodule*. This story **is** that explicit intent — but upstream changes are **separate commits inside each submodule repo** (GitHub `Hexalith/Hexalith.{Memories,EventStore,Commons,FrontComposer}`), and the Folders repo records only the resulting **pin**. Do not stage submodule content changes as Folders-repo changes.
- **Generated artifacts stay generated** (NSwag client, `parity-contract.yaml`) — not in scope here.

### Testing standards for this story
- Upstream new types get **upstream** unit tests in their own module's test project (each submodule ships its own suite; keep them green there before bumping the pin).
- Folders-side verification is **integration-by-build**: after each pin bump, restore/build `Hexalith.Folders.slnx` at 0W/0E, run the focused lanes + `ScaffoldContractTests` (10/10) + `dotnet format … --verify-no-changes`. Per repo rule, run **test projects individually** (no solution-level `dotnet test`). `AppHost.Tests` expected **3 SKIP** (Tier-3/DCP-gated). [Source: `project-context.md` §Testing; `fable_…` §13 Step 1/10]
- No new Folders test project or gate row is added by 11.2 (test-helper consolidation + `FakeEventStoreGatewayClient` adoption is **Story 11.7**, not here; the `Eventually` **consumption** switch is 11.7 too — this story only *lands* `Eventually` upstream).

### Cross-story sequencing (do not front-run)
- **11.2 blocks 11.8–11.12** (adoption/deletion). Land + pin before those start; keep each consumable against a pinned SHA. [Source: `fable_…` §12, §11.1 §10 "Platform-first"]
- **Not in 11.2:** Folders in-repo dedup (11.3–11.7), domain adoption/deletion (11.8), ServiceDefaults deletion (11.9), Server/Workers SDK-seam adoption + the Memories bridge-read-model wiring (11.10), UI-below-shell (11.11), STJ client regen (11.12), ADRs/close-out (11.13). The G3/`Commons.ServiceDefaults` confirm here **enables** 11.9; the EventStore cursor/read-model confirm **enables** 11.10; the FC Shell confirm **enables** 11.11.
- **Story 10.6** (reopened Epic 10, metadata-derived materializer) lands **before** 11.10 and rewrites the same Workers indexing code the Memories publish seam touches — the G1 publisher must not re-freeze the fail-closed placeholder; keep the Folders mapping intact. [Source: `11-1-…-pin-map.md` §12; `epics.md` Epic 10 note]

### Project Structure Notes
- Module layouts (for placing new upstream types): **Commons** libraries under `references/Hexalith.Commons/src/libraries/` (`.`, `.Http`, `.Publication`, `.ServiceDefaults`, `.TenantAccess`, `.UniqueIds`, `.Aspire`, …). **EventStore** libraries under `references/Hexalith.EventStore/src/` (`.Client`, `.Contracts`, `.DomainService`, `.Testing`, `.Aspire`, …; `RestApi.Generators` is the only analyzer project). **FrontComposer** under `references/Hexalith.FrontComposer/src/` (`.Shell`, `.Testing`, `.Contracts`, …). **Memories** under `references/Hexalith.Memories/src/` (`.Contracts`, `.Contracts.V1`, `.Client.Rest`, `.EventStore`, …).
- Follow each module's own conventions (one-primary-type-per-file, file-scoped namespaces, nullable, warnings-as-errors, central packages) — they mirror the Folders `project-context.md` rules. Each submodule has its own `.editorconfig`/build props; honor them.
- No new Folders project, no `.slnx` change, no `ScaffoldContractTests` inventory change in this story (those land with the *consuming* stories).

### References
- [Source: `_bmad-output/planning-artifacts/epics.md#Story 11.2` (L2017–2028) and Epic 11 cross-story ACs (11.8 L2114, 11.9, 11.10, 11.11)]
- [Source: `fable_Folders_changes.md` §5 (G1–G9 table), §11 (classification: keep vs move), §12 (risks/migration order), §13 Step 5 (platform-first action plan, one story per repo; G4/G5 "proposal now, implementation trails")]
- [Source: `_bmad-output/implementation-artifacts/11-1-establish-refactor-baseline-and-governance-pin-map.md` §4 (domain isolation), §6 (workflow/`submodules:false`), §9 (governance pin map), §10 (platform-first handoff constraint)]
- [Source: `_bmad-output/planning-artifacts/architecture.md` L404 (sibling-source project-reference consumption), L1645 (domain-focus refactoring closure criterion), §"Query Facade (Story 10.5)", §"Ops Console & Transition-Evidence Read Models"]
- [Source: `_bmad-output/project-context.md` §Framework/Testing/Workflow rules; `CLAUDE.md` §Git Submodules]
- [Source: `Directory.Build.props` (`Hexalith{X}Root` props — no `HexalithCommonsRoot`); `references/Hexalith.Builds/Props/Directory.Packages.props` (70 `Hexalith.*` `PackageVersion` pins; EventStore 3.42.0, Commons 2.27.0)]
- [Source: prerequisite-audit passes over `references/{Hexalith.Memories,Hexalith.EventStore,Hexalith.Commons,Hexalith.FrontComposer}` @ pins `deb9dd7`/`963402c5`/`20048e9`/`f61c6a8a`, 2026-07-08]

## Execution model & scope boundary (open decision — ratify in Task 0)

The AC's "**When upstream stories land** and Folders pins the resulting submodule SHAs" is deliberately agnostic about **who** authors the upstream APIs. Two viable models, and the choice reshapes Tasks 2–7:

- **Model A — Umbrella (author-across-repos):** this dev session authors every MISSING/PROMOTE/MOVE seam directly in the four `references/**` submodule working trees (separate commits/PRs per repo, each with its own review + green suite + — for Commons — a package publish), then bumps the Folders pins. One story closes the whole platform-first phase.
- **Model B — Confirm-and-pin (split upstream):** this story confirms what already exists, writes the exact upstream specifications (this register + the consuming-side contracts), pins only what is already landable, and defers each missing seam's implementation to a per-repo platform story (audit §12: "one platform story per gap … landed and pinned before the consuming Folders story starts"). The Folders-repo deliverable is the manifest + pins.

The audit leans **B** in wording (per-gap upstream stories) but Model A is operationally common when one maintainer drives all repos. The register, consuming-side contracts, mechanism, and verification are identical either way — only the "who authors / how many stories" differs.

**RATIFIED: Model B**, by `{user_name}` on 2026-07-08. This story therefore confirms + specifies + pins-what's-landable, and opens **one per-repo platform story per gap** (in `Hexalith/Hexalith.{Memories,EventStore,Commons,FrontComposer}`) for each missing seam; it authors **no** upstream implementation in-session. The register and §Consuming-Side Contracts are the binding spec those per-repo stories implement. The two Step-4 ratifications (AppHost/Aspire+ServiceDefaults ADR direction; `FolderStreamName` reserved-tenant semantics) remain open — resolve in Task 0.

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-14 | Implemented ratified Model B: confirmed 30 public API paths, specified and assigned every missing G1–G9/P6–P9 seam through 15 upstream issues, authored the Commons.Cli/Mcp proposal, recorded the no-bump pin outcome, and completed build/gate/regression evidence. Story moved to review. |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-14: Administrator ratified Step-4 decision (a): retain AppHost/Aspire as the sanctioned local/test exception and delete `Hexalith.Folders.ServiceDefaults` in Story 11.9; the ADR remains assigned to Story 11.13.
- 2026-07-14: Administrator ratified Step-4 decision (b): standardize on the strict ordinal/no-trim `system` reserved-tenant rule; Story 11.5 owns the code/test lockstep and Story 11.13 owns the ADR.
- 2026-07-14: Task 1 plan/verification — resolve parent-pinned SHAs with `git ls-tree HEAD`; query each exact commit with `git grep`; record public type, namespace, project, source path, SHA, and package/pin notes in a YAML manifest; validate every recorded path with `git cat-file -e`.
- 2026-07-14: The first broad `dotnet restore` + Release `--no-restore` build reused Debug/source-mode assets and failed with missing package namespaces. Per the repository fallback ladder, `dotnet restore Hexalith.Folders.slnx --property Configuration=Release -m:1 -p:NuGetAudit=false` followed by the serialized Release build passed with 0 warnings/0 errors.
- 2026-07-14: Full per-project regression sweep passed 5,255 tests; four DCP-gated AppHost tests skipped as configured; the UI E2E assembly ran 63/63 with no skips.
- 2026-07-14: Task 2 Model B plan/verification — prove the three G1 seams absent at the parent-pinned Memories SHA, specify their exact producer/search behavior, search the owning tracker for duplicates, open one owned Memories platform story, validate its live state, and avoid claiming the unrelated Story 10.6 Memories checkout as a pin.
- 2026-07-14: Task 2 regression sweep repeated after recording the owned dependency: 5,255 passed, four configured AppHost skips, zero failures, UI E2E 63/63.
- 2026-07-14: Task 3 Model B plan/verification — audit G6/G8/G9 at the exact EventStore pin; distinguish the newer internal `DaprServiceInvocationHandler` from the requested consumable auth surface; create one owning issue per gap; validate all live issue states and manifest ownership; preserve the current source/package graph without a speculative pin.
- 2026-07-14: Task 3 regression sweep passed 5,255 tests, four configured AppHost skips, zero failures, and UI E2E 63/63.
- 2026-07-14: Task 4 Model B plan/verification — audit P6/P7/P8/P9/G7 at the exact Commons pin; tie each contract to the corresponding Folders duplication; open one Commons issue per gap; require package publication plus Builds central-version delivery rather than treating the Commons source checkout as consumable.
- 2026-07-14: Task 4 regression sweep passed 5,255 tests, four configured AppHost skips, zero failures, and UI E2E 63/63; all five Commons issues remained OPEN and the manifest ownership assertions passed.
- 2026-07-14: Task 5 Model B plan/verification — apply the repository UX rules; audit FrontComposer G9 at the exact parent pin; distinguish the existing private EventStore ProblemDetails parser from a reusable public parser; specify all four Shell/Testing seams in one owning FrontComposer issue; preserve the five-selector no-mutation contract.
- 2026-07-14: Task 5 regression sweep passed 5,255 tests, four configured AppHost skips, zero failures, and UI E2E 63/63; FrontComposer issue 60 remained OPEN and the manifest ownership assertion passed.
- 2026-07-14: Task 6 Model B plan/verification — normalize and diff the six generic Aspire/Dapr source pairs at the exact Commons/EventStore pins; re-audit RepositoryProjectPaths and record that Memories now owns a narrower deliberate copy; assign canonicalization to Commons and copy-removal migrations to EventStore and Memories without changing Folders topology.
- 2026-07-14: Task 6 regression sweep passed 5,255 tests, four configured AppHost skips, zero failures, and UI E2E 63/63; all three G2 owner issues remained OPEN and the manifest ownership assertion passed.
- 2026-07-14: Task 7 plan/verification — audit Folders, Memories, and EventStore adapter mechanics; choose explicit invocation intent first for credential resolution; keep exit-code numerics behind named compatibility policies; write one proposal with distinct Commons.Cli and Commons.Mcp boundaries; open one Commons issue per gap.
- 2026-07-14: Task 7 proposal/schema checks passed; Commons issues 25 and 26 remained OPEN; the regression sweep passed 5,255 tests, four configured AppHost skips, zero failures, and UI E2E 63/63.
- 2026-07-14: Task 8 pin audit found no honest dependency bump: all confirmed seams already exist at the parent pins, while every missing seam remains absent even in the newer unpinned FrontComposer/Memories working checkouts and is owned by an OPEN upstream issue. Parent gitlinks and Builds versions remain unchanged.
- 2026-07-14: Final serialized Release restore/build passed with 0 warnings and 0 errors. Focused contract-spine, safety-invariant, governance-completeness, and ScaffoldContractTests lanes passed; the Scaffold class ran 11/11. Scoped Folders-owned whitespace and analyzer verification passed.
- 2026-07-14: Final manifest audit covered G1–G9 and P6–P9, resolved all 30 confirmed SHA:path entries, validated 15 owned dependency rows and 15 OPEN tracker issues, found no nested submodule markers, and confirmed `.gitmodules` plus honest-green workflow/gate sources were untouched.
- 2026-07-14: Final full per-project Release regression passed 5,255 tests, four configured DCP AppHost skips, zero failures, and UI E2E 63/63.

### Completion Notes List

- Task 1 confirmed 13 platform availability groups comprising 30 exact public API paths at the current parent-pinned SHAs. No upstream or Folders product code was authored. The manifest explicitly records that Commons.Publication and Commons.TenantAccess are packable but still lack central `PackageVersion` rows in the pinned Builds props.
- Task 2 specified the missing Memories publisher, public event-type constants, and resilient search wrapper in [Hexalith.Memories issue 28](https://github.com/Hexalith/Hexalith.Memories/issues/28). Under ratified Model B, each checkbox records specification/tracking completion; the manifest retains the unlanded implementation and absent pin bump as an explicit owned blocker.
- Task 3 specified and assigned EventStore G6 auth seams ([issue 283](https://github.com/Hexalith/Hexalith.EventStore/issues/283)), G8 classified secret lookup ([issue 284](https://github.com/Hexalith/Hexalith.EventStore/issues/284)), and G9 async polling ([issue 285](https://github.com/Hexalith/Hexalith.EventStore/issues/285)). No EventStore submodule content or pointer was changed; the manifest records all three as owned blockers pending upstream release.
- Task 4 specified Commons P6 safe HTTP helpers ([issue 19](https://github.com/Hexalith/Hexalith.Commons/issues/19)), P7 sensitive-value detection ([issue 20](https://github.com/Hexalith/Hexalith.Commons/issues/20)), P8 deterministic hashing ([issue 21](https://github.com/Hexalith/Hexalith.Commons/issues/21)), P9 authorized base URLs ([issue 22](https://github.com/Hexalith/Hexalith.Commons/issues/22)), and G7 cursor/offset paging consolidation ([issue 23](https://github.com/Hexalith/Hexalith.Commons/issues/23)). Under Model B these checkboxes record exact specification and ownership; no Commons package or Builds pin is claimed before those upstream releases exist.
- Task 5 specified the FrontComposer G9 icon, safe-copy, bounded ProblemDetails parser, and hermetic-auth surfaces in [issue 60](https://github.com/Hexalith/Hexalith.FrontComposer/issues/60). The UX instructions shaped the safe-copy contract toward Fluent UI v5 reuse while preserving Folders' compatibility hooks and five-selector no-mutation proof. No FrontComposer pointer changed before the upstream release exists.
- Task 6 recorded optional G2 as an owned platform follow-up: Commons canonicalization in [issue 24](https://github.com/Hexalith/Hexalith.Commons/issues/24), EventStore migration/copy removal in [issue 286](https://github.com/Hexalith/Hexalith.EventStore/issues/286), and Memories project-path copy removal in [issue 29](https://github.com/Hexalith/Hexalith.Memories/issues/29). Exact-pin inspection corrected the stale register premise: Memories now also carries a narrower `RepositoryProjectPaths` copy.
- Task 7 delivered the written [Commons.Cli / Commons.Mcp proposal](./11-2-commons-cli-mcp-proposal.md), tracked by Commons [issue 25](https://github.com/Hexalith/Hexalith.Commons/issues/25) and [issue 26](https://github.com/Hexalith/Hexalith.Commons/issues/26). It selects explicit token input over ambient sources, keeps inline plaintext config tokens disabled by default, and uses explicit compatibility policies for numeric exits. Folders Story 11.6 remains an in-repo consolidation; no CLI/MCP source changed here.
- Task 8 completed without a `chore(deps):` commit because no missing prerequisite is landable at a released/pinned revision. The manifest explicitly records empty gitlink/Builds changes rather than presenting the unrelated ahead FrontComposer, Memories, or Tenants working checkouts as Story 11.2 pins. All acceptance evidence is ready for review; downstream Stories 11.8–11.12 remain gated by the owned upstream issues.

### File List

- `_bmad-output/implementation-artifacts/11-2-land-platform-prerequisite-apis-in-shared-modules.md`
- `_bmad-output/implementation-artifacts/11-2-platform-prerequisite-api-availability.yaml`
- `_bmad-output/implementation-artifacts/11-2-commons-cli-mcp-proposal.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
