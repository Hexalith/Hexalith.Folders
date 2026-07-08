# Story 11.2: Land platform prerequisite APIs in shared modules

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform maintainer,
I want the shared primitives that Folders re-implements locally to be **created or confirmed** in the Commons, EventStore, FrontComposer, and Memories platform modules and pinned into Folders,
so that the later Epic 11 adoption stories (11.8–11.12) can **delete** Folders' local copies instead of moving duplication around.

This is the **platform-first (Phase B) prerequisite story** for Epic 11. It changes **zero product wire behavior** in Folders. Its Folders-repo footprint is limited to: (a) `references/**` submodule-pointer and/or `Directory.Packages.props` version bumps that make the new upstream APIs available, and (b) a machine-checkable **prerequisite API availability manifest** recorded as the story's evidence.

**Execution model — RATIFIED `Model B` (confirm + spec + pin) by `{user_name}`, 2026-07-08.** This story does **not** author the missing upstream APIs in-session. Its deliverables are: (1) **confirm** the already-present seams; (2) **specify** each missing seam exactly (the register in §Prerequisite API Register + the §Consuming-Side Contracts ARE that spec); (3) **open/track one per-repo platform story per gap** in the owning submodule repository; (4) **pin** (`chore(deps):`) whatever is already landable and record everything in the manifest. Each missing seam's implementation and the Folders adoption that consumes it are separate, later, gated work. See **§Execution model & scope boundary**. Two Step-4 ratifications remain open (AppHost/Aspire+ServiceDefaults ADR direction; `FolderStreamName` reserved-tenant semantics) — resolve in Task 0.

## Acceptance Criteria

**AC1 (epic-canonical).**
**Given** the audit platform gaps G1–G9
**When** upstream stories land and Folders pins the resulting submodule SHAs
**Then** Memories exposes index-event publish/read wrappers, EventStore exposes secret-store/auth/cursor/read-model/test seams, Commons exposes secret/hash/URL/correlation helpers and ServiceDefaults consumption, and FrontComposer exposes missing UI helpers
**And** every submodule bump uses a conventional `chore(deps):` commit message.

**AC2 — Confirm what already exists (no upstream work).** For every register row marked **CONFIRM** in §Prerequisite API Register, the story records the exact public type, namespace, project, and pinned SHA where it lives today. No re-implementation is authored for a CONFIRM row. (Concretely: the Memories event contracts `SearchIndexEntryChanged`/`SearchIndexEntryRemoved`; `Commons.Publication` primitives; `Commons.UniqueIds.UniqueIdHelper`; `Commons.TenantAccess`; `Commons.ServiceDefaults`; the EventStore cursor/read-model/admission/mapping/fake seams; and the FrontComposer Shell user-context/token-relay/OIDC/skeleton/empty-state primitives.)

**AC3 — Memories seams (G1).** At the pinned Memories SHA, Folders can consume: a **producer publish seam** (`IIndexEventPublisher` or equivalent + a Dapr CloudEvents publish helper) reachable from a project `Hexalith.Folders.Workers` already references (`Hexalith.Memories.Contracts`); a **public** curated event-type constant surface (the `internal CuratedSearchIndexEventTypes` is made public **and** reachable from Contracts/Client, not `Hexalith.Memories.EventStore`); and a **resilient search wrapper** in `Hexalith.Memories.Client.Rest` that Folders' Server can consume in place of the hand-rolled degradation logic. The wrappers preserve the exact producer/consumer shapes in §Consuming-Side Contracts.

**AC4 — EventStore seams (G6/G8/G9 + confirm cursor/read-model/test).** At the pinned EventStore SHA: `ISecretStoreClient` with `Found/Missing/Denied/Unavailable` outcomes exists in `Hexalith.EventStore.Client` (G8); the promoted auth handlers exist in a **consumable** library — `DaprAppIdHandler` in `Hexalith.EventStore.Client`, a new `InboundBearerForwardingHandler`, and JWT-hardening + `eventstore:*` claim accessors in `Hexalith.EventStore.DomainService` (or a shared auth package) (G6); `Eventually` async-poll exists in `Hexalith.EventStore.Testing` (G9). The cursor/read-model/admission/mapping/fake seams (`IQueryCursorCodec`, `IReadModelStore`, `ReadModelWritePolicy`, `AddEventStoreDataProtection`, `IDomainServiceAdmissionStage`, `MapEventStoreDomainEvents`, `FakeEventStoreGatewayClient`, `EventStoreDomainDiagnostics`) are **confirmed already present** (no new work) and recorded in the manifest.

**AC5 — Commons helpers (P6/P7/P8/P9 + G7).** At the pinned Commons **package version** (referenced through `references/Hexalith.Builds/Props/Directory.Packages.props`), Commons exposes: `SensitiveValueDetector` (P7), `DeterministicHashBuilder` (P8), `AuthorizedBaseUrl` (P9), a correlation `SanitizeOrCreate` primitive (P6), a bearer `DelegatingHandler` with an **https/loopback guard**, and a **canonical cursor paging envelope** with the two verbatim offset `PagedResult<T>` copies de-duplicated (G7). `Commons.ServiceDefaults` (G3), `UniqueIdHelper`, and `TenantAccess` are confirmed present.

**AC6 — FrontComposer helpers (G9).** At the pinned FrontComposer SHA: `FcFluentIcons` gains `LockClosed16` and `Clock16` (matching the Folders hand-authored vector paths); an `FcSafeCopy` component exists (matching Folders' `SafeCopyId.razor` clipboard/copy pattern **and preserving the 5-selector command-suppression guard**); an RFC-7807 ProblemDetails **extension** parser exists; and `HermeticTestAuthenticationHandler` lives (public) in `Hexalith.FrontComposer.Testing`.

**AC7 — G4/G5 are proposal-only.** `Commons.Cli` / `Commons.Mcp` shared scaffolding is delivered as a **written proposal/ADR** with harmonized credential precedence, **not** implementation. Folders' CLI/MCP consolidation (Story 11.6) stays an in-repo adapter-core lib; migration onto Commons.Cli/Mcp is explicitly deferred. No Folders CLI/MCP code changes in this story.

**AC8 — Correct, conventional pinning.** Each availability change reaches Folders through the correct mechanism (see §Consumption mechanism): a `references/<module>` **pin bump** for the sibling-source modules (EventStore, Memories, FrontComposer, Tenants); a **Commons package publish + `references/Hexalith.Builds` version/pin bump** for Commons (package-consumed). Every bump is a separate conventional **`chore(deps):`** commit. The intentional submodule-pointer drift recorded in Story 11.1 §10 is **not** reverted, and no nested/recursive submodule init is introduced.

**AC9 — Behavior-equivalent, gate-green, honest-green preserved.** After every pin bump: `dotnet restore Hexalith.Folders.slnx` + `dotnet build … -c Release --no-restore` are **0 warnings / 0 errors** (warnings-as-errors); the focused test lanes and `ScaffoldContractTests` are green; `dotnet format whitespace/analyzers --verify-no-changes` is clean on Folders-owned files; and no REST/OpenAPI/envelope/ProblemDetails/parity behavior changes (Folders does not yet *consume* the new APIs — that is 11.8–11.12). The honest-green gate baseline is untouched: `e2e-gates` + `accessibility-gates` stay present + blocking, the full-63 UI.E2E lane stays un-narrowed, the AD7 forbidden-substring set stays absent, and all CI-workflow conformance classes stay green (this story edits none of them).

**AC10 — Evidence manifest.** A committed evidence artifact records, per gap G1–G9 (and the P6–P9 Commons helpers), the disposition (CONFIRM / CREATE / DEDUPE / PROMOTE / MOVE / PROPOSAL), the exact upstream type + namespace + project, the module's new pinned SHA (or Commons package version + Builds SHA), and the verification result. Any seam that could not be landed is recorded as an explicit, owned blocker — never silently skipped.

## Tasks / Subtasks

> **Model B semantics (ratified):** for every **CREATE/PROMOTE/MOVE/DEDUPE** row below, this story's job is to **specify** it precisely (the register + §Consuming-Side Contracts are the spec) and **open a per-repo platform story** in the owning submodule — *not* to author it in this Folders dev session. **Confirm + pin** anything already landable; record the rest as owned per-repo dependencies in the manifest. The subtask text ("create X in project Y matching shape Z") is the spec each per-repo story implements. Re-verify each register row against the actual pinned source with a fresh grep before writing the spec — code drifts between the audit and now.
> Ordering: **CONFIRM** rows first (cheap, they shrink the remaining work), then write the **CREATE/PROMOTE/MOVE/DEDUPE** specs + open per-repo stories per module, then pin what's landable, then verify. Prefer **one `chore(deps):` bump per module** as its seams land, so a bad bump is bisectable.

- [ ] **Task 0 — Ratify the two remaining open decisions (AC1, blocks the specs they touch).**
  - [x] ~~Execution model~~ — **DECIDED: Model B (confirm + spec + pin; implementation split into per-repo platform stories)**, ratified by `{user_name}` 2026-07-08. No in-session upstream authoring.
  - [ ] Ratify Step-4 decision (a): the AppHost/Aspire **sanctioned-exception** direction + `Hexalith.Folders.ServiceDefaults` deletion (consumed by Story 11.9) — record intent (ADR lands in 11.13).
  - [ ] Ratify Step-4 decision (b): `FolderStreamName` vs `OrganizationStreamName` **reserved-tenant** semantics divergence (audit §9.5) — this is a *deliberate decision, not a silent merge*; it shapes Story 11.5. Record intent (ADR lands in 11.13).
- [ ] **Task 1 — CONFIRM the already-present seams (AC2, AC4, AC5, AC6).** Record exact type/namespace/project/SHA in the manifest for each CONFIRM row in the register. Author **no** code for these. (Memories event contracts + Commons.Publication + UniqueIdHelper + TenantAccess + Commons.ServiceDefaults + EventStore cursor/read-model/admission/mapping/fake + FrontComposer Shell primitives.)
- [ ] **Task 2 — Memories G1 seams (AC3).** In `references/Hexalith.Memories`:
  - [ ] Create the producer publish seam (`IIndexEventPublisher` + Dapr CloudEvents publish helper) in a Folders.Workers-reachable project (`Hexalith.Memories.Contracts` — the only Memories project Workers references — or `.Client.Rest`). Match the §Consuming-Side producer shape (composite AggregateId, `cloudevent.id/type/source` metadata, retryable classification). Optionally layer on the existing `Commons.Publication` primitives.
  - [ ] Make `CuratedSearchIndexEventTypes` **public** and reachable from Contracts/Client (it is `internal` in `Hexalith.Memories.EventStore` today) so consumers stop re-deriving via `nameof(...)`.
  - [ ] Create the **resilient search wrapper** in `Hexalith.Memories.Client.Rest` (timeout budget → degraded; catch→safe-degraded; in-band `Degraded`/`UnavailableAxes`; `ScoredResult.SourceUri` identity recovery) — the wrapper Folders + Tenants both clone. `MemoriesClient.SearchAsync` currently *throws*; keep it, add the wrapper alongside.
  - [ ] Commit inside the Memories repo; bump `references/Hexalith.Memories` in Folders via `chore(deps):`.
- [ ] **Task 3 — EventStore G6/G8/G9 seams (AC4).** In `references/Hexalith.EventStore`:
  - [ ] G8: `ISecretStoreClient` + `Found/Missing/Denied/Unavailable` outcome model in `Hexalith.EventStore.Client` (mirror Folders' `IProviderCredentialSecretStoreClient` shape; keep the RpcException `PermissionDenied` → `Denied`, other Dapr fault → `Unavailable(retryAfter)` classification).
  - [ ] G6: promote `DaprAppIdHandler` from `Admin.UI` into `Hexalith.EventStore.Client`; **create** `InboundBearerForwardingHandler` (does not exist anywhere today); promote JWT-hardening + `eventstore:*` claim accessors from the gateway/Admin.Server host into `Hexalith.EventStore.DomainService` (or a shared auth package). None are in a consumable library today.
  - [ ] G9: add `Eventually` async-poll (`UntilAsync<T>(probe, isReady, timeout, interval, ct)` shape) to `Hexalith.EventStore.Testing`.
  - [ ] Commit inside the EventStore repo; bump `references/Hexalith.EventStore` via `chore(deps):`. **Watch Blocker C:** EventStore is source-consumed at v3.43.0 while its Builds *package* pins are 3.42.0 — do not introduce a source/package API skew that reddens the DCP lane (see Dev Notes).
- [ ] **Task 4 — Commons P6/P7/P8/P9 + G7 (AC5).** In `references/Hexalith.Commons`:
  - [ ] Create `SensitiveValueDetector` (P7), `DeterministicHashBuilder` (P8), `AuthorizedBaseUrl` (P9), a correlation `SanitizeOrCreate` primitive (P6 — `HttpCorrelation.ResolveCorrelationId` exists but is not it), and a bearer `DelegatingHandler` with the **https/loopback guard** the Folders/Cli/Mcp copies lack.
  - [ ] G7: introduce the canonical **cursor** paging envelope (no cursor type exists — both `PagedResult<T>` are offset-based); de-duplicate the two verbatim `PagedResult<T>` copies (`Hexalith.Commons.Paging` vs `Hexalith.Commons.Http`).
  - [ ] Commit inside the Commons repo; **publish** the Commons package version; update the `Hexalith.Commons*` versions in `references/Hexalith.Builds/Props/Directory.Packages.props`; bump `references/Hexalith.Builds` via `chore(deps):`. (A `references/Hexalith.Commons` pin bump alone does **not** reach Folders — Commons is package-consumed.)
- [ ] **Task 5 — FrontComposer G9 (AC6).** In `references/Hexalith.FrontComposer`:
  - [ ] Add `LockClosed16` + `Clock16` to `FcFluentIcons` (`Hexalith.FrontComposer.Shell.Components.Icons`) matching Folders' `FoldersConsoleIcons` vector paths.
  - [ ] Create `FcSafeCopy` (Shell) matching `SafeCopyId.razor` — read-only monospace value + JS clipboard copy, `fc-safe-copy*` classes, `data-testid` — and **preserve the 5-selector command-suppression guard** the UI E2E gate enforces.
  - [ ] Create the RFC-7807 ProblemDetails **extension** parser (Shell); reuse `FrontComposer.Contracts.ProblemDetailsPayload` + `Commons.Http.BoundedProblemDetailsReader` as prior art.
  - [ ] Move `HermeticTestAuthenticationHandler` (currently `private sealed` in Folders `UI/CompositionRoot.cs`) into the existing `Hexalith.FrontComposer.Testing` project, promoted public.
  - [ ] Commit inside the FrontComposer repo; bump `references/Hexalith.FrontComposer` via `chore(deps):`.
- [ ] **Task 6 — G2 Aspire drift reconciliation (platform-internal; optional in this story's Folders scope).** Reconcile the 5 duplicated Aspire-Dapr files across `Commons.Aspire` vs `EventStore.Aspire` into one home (recommend `Commons.Aspire`); **relocate** `RepositoryProjectPaths.cs` (exists only in EventStore.Aspire, not a dup) and reconcile the divergent extension-file names. Folders consumes these only through AppHost/Aspire, so this does not gate a Folders story — land it if scope permits, else record as an owned platform follow-up.
- [ ] **Task 7 — G4/G5 proposal only (AC7).** Author the `Commons.Cli` / `Commons.Mcp` proposal/ADR (root-command builder, global options, output formatter, exit codes, config layering, **harmonized** credential precedence — Folders puts explicit `--token` lowest vs Memories highest; pick one). No implementation; no Folders CLI/MCP edits.
- [ ] **Task 8 — Pin, verify, and record evidence (AC8, AC9, AC10).**
  - [ ] After each `chore(deps):` bump: `dotnet restore Hexalith.Folders.slnx`; `dotnet build … -c Release --no-restore` (0W/0E); focused lanes + `ScaffoldContractTests`; `dotnet format whitespace/analyzers --verify-no-changes` (Folders-owned clean).
  - [ ] Confirm no `references/**` intentional pointer drift (Story 11.1 §10) was reverted; no recursive/nested submodule init introduced; the honest-green conformance classes are untouched and green.
  - [ ] Write the evidence manifest (§AC10) as `docs/exit-criteria/…` or `_bmad-output/implementation-artifacts/…` — per-gap disposition, upstream type/namespace/project, new pinned SHA (or Commons package version + Builds SHA), verification result, and any owned blockers.

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

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
