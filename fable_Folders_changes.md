# Hexalith.Folders — Domain-Focus Refactoring Analysis & Action Plan

- **Date:** 2026-07-07 · **HEAD:** `533806b` (main, clean tree)
- **Method:** five parallel architectural audits (domain core; Server/Workers/hosting; UI/Client/Cli/Mcp; tests & tooling; technical-module API catalog), cross-verified against the actual source of `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Tenants`, and `Hexalith.Memories` submodules.
- **Scope:** analysis and roadmap only — no code changes were made.

---

## 1. Executive summary

Hexalith.Folders' **domain modeling is sound**: pure `Handle(command, state) → Result` aggregates with `Apply` on immutable records, a well-formed layered authorization model, a genuinely domain-shaped provider port (`IGitProvider`), disciplined thin Cli/Mcp adapters, correct FrontComposer shell adoption at the layout level, exemplary contract-first client generation, and a remarkably clean test suite (zero skipped tests, near-zero re-testing of platform behavior).

The systemic problem is a **single architectural decision**: `src/Hexalith.Folders/Hexalith.Folders.csproj` references **neither `Hexalith.Commons` nor `Hexalith.EventStore`**. The domain library is deliberately isolated from the platform, and the Server hand-implements its entire REST surface instead of using the domain-service SDK routing. Almost every boundary violation below is a downstream consequence:

1. **Re-implemented platform capabilities inside Folders** — a line-for-line de-genericized copy of `Hexalith.Commons.TenantAccess` (~400+ LOC), self-declared `ActivitySource`/`Meter` telemetry sources, two hand-rolled *plaintext, unsigned* pagination cursors (the SDK's `IQueryCursorCodec` doc literally calls itself "the platform generalization of the per-domain cursor codecs domain modules previously hand-wrote"), in-memory-only production read models where `IReadModelStore`+`ReadModelWritePolicy` exist, a hand-rolled Aspire ServiceDefaults where `Hexalith.Commons.ServiceDefaults` is a packable library already loaded in this repo's `.slnx`, a hand-rolled ULID generator, and hand-rolled Dapr subscription mapping where `MapEventStoreDomainEvents` exists.
2. **~10,000 lines of removable duplication** — ~4,000 lines of mechanical, wire-contract-preserving dedup in the Server endpoint files; ~1,800 lines of GitHub↔Forgejo provider adapter copy-paste; byte-identical Cli↔Mcp plumbing; two copies of `FoldersTenantEventHandler`; ~110 duplicated test-helper declarations.
3. **Infra coupling in the domain project for almost nothing** — `Octokit` exists solely for a factory whose client is never invoked (all methods `NotImplementedException`); `Dapr.Client` exists solely for a 65-line secret-store client (and a DI line that mints its own `DaprClient` beside the SDK's).
4. **Several platform gaps must be filled first** — no Memories index-update publisher, no secret-store abstraction, no shared CLI/MCP scaffolding, no shared JWT/tenant-auth handlers (copied 4× across the ecosystem), and two *independently drifted* copies of the same Aspire Dapr helpers inside the platform itself (Commons.Aspire vs EventStore.Aspire). Per the boundary rule, these get built in the technical module first, then consumed.
5. **The refactor is gate-constrained, not code-constrained** — ~250 conformance/doc-gate tests in `Contracts.Tests` plus the `ScaffoldContractTests` inventory lock pin every project name, reference edge, route string, and doc phrase. The plan below sequences every move against those gates. (Correction to prior project memory: the `.slnx`-inventory test is **green at HEAD** — the drift was reconciled.)

**Correctness bugs found during audit** (report-only; each becomes a plan item): a per-call `SocketsHttpHandler`+`HttpClient` leak in the Forgejo transport factory; a UI bearer-token handler that silently sends unauthenticated requests mid-circuit; sync-over-async in the UI user-context accessor; a security-filter drift where OpsConsole's secret detector is a strict subset of ProviderReadiness's; a reserved-tenant check divergence between `FolderStreamName` and `OrganizationStreamName`; missing https/loopback guard on the Cli/Mcp bearer handlers; and `RepositoryProvisioningProcessManager` appearing DI-registered but never triggered.

Estimated net effect of the full plan: **-12,000 to -15,000 lines in Folders**, two infra package references removed from the domain, one project deleted (`ServiceDefaults`), zero wire-contract changes until the explicitly deferred epics.

---

## 2. Baseline status

| Item | State |
| --- | --- |
| Branch / HEAD | `main` @ `533806b`, clean working tree |
| Last full verified lane run (2026-06-24, Story 10.5/8.6) | Folders 1358 · Server 540 · Workers 62 · Contracts 275 · Cli 709 · Mcp 662 · Integration 631 — all green; AppHost 3 SKIP (Tier-3 DCP-gated) |
| `ScaffoldContractTests.SolutionContainsOnlyCanonicalBuildableProjects` | **GREEN at HEAD** — both hardcoded inventories diffed against `.slnx`/filesystem with zero drift (corrects prior memory of a pre-existing red) |
| Skipped tests | Zero `[Fact(Skip=…)]`, zero commented-out tests repo-wide |
| Known environment blocker | `aspire run` live boot / AppHost.Tests Tier-3 lane blocked by env-wide Aspire CLI/DCP `--tls-cert-file` mismatch (not a topology defect) |
| Format | `dotnet format whitespace`+`analyzers` clean (LF pinned in `.editorconfig`) |
| Known CI caveat | Baseline CI with `submodules:false` has no PackageReference fallback (CS0234) — cross-repo platform work must ship via submodule pins or published packages |

Action-plan step 1 re-establishes these numbers before any change (§13).

---

## 3. Current project / module inventory

### 3.1 Source projects (`src/`, 13 in `.slnx` + 1 codegen exe)

| Project | Files / LOC | Role | Verdict |
| --- | --- | --- | --- |
| `Hexalith.Folders` | 578 / ~29.9k | Domain: aggregates (Folder, Organization), authorization, observability, projections, providers, queries | **Keep** — add `Hexalith.Commons.*` + `Hexalith.EventStore.Client` refs; shed `Octokit`/`Dapr.Client`; large dedup |
| `Hexalith.Folders.Contracts` | 18 / ~0.2k | Wire DTOs + OpenAPI spine (`openapi/hexalith.folders.v1.yaml`, 10,268 lines) | **Keep** — absorb shared header-name constants |
| `Hexalith.Folders.Client` | 16 / ~16.6k | NSwag-generated typed client (14k generated) + convenience layer | **Keep** — regenerate on System.Text.Json; ULID via Commons |
| `Hexalith.Folders.Client/Generation` (+`.Shared`) | — | YamlDotNet codegen: idempotency helpers from OpenAPI extensions | **Keep** — extraction to platform deferred until a 2nd consumer; optionally add the exe csproj to `.slnx` |
| `Hexalith.Folders.Cli` | 27 / ~2.4k | System.CommandLine adapter over Client | **Keep** — dedupe with Mcp; adopt future platform CLI scaffolding |
| `Hexalith.Folders.Mcp` | 21 / ~1.7k | MCP server over Client | **Keep** — same |
| `Hexalith.Folders.Server` | 33 / ~12.3k | Hand-written REST surface + auth + ContextSearch facade | **Keep** — biggest simplification target (~4k mechanical + SDK adoption) |
| `Hexalith.Folders.Workers` | 26 / ~2.7k | Semantic-indexing egress, repository provisioning, tenant handlers | **Keep** — adopt SDK subscription mapper; egress primitive → Memories |
| `Hexalith.Folders.ServiceDefaults` | 5 / ~0.25k | Diverged stock Aspire template copy | **REMOVE** — replace with `Hexalith.Commons.ServiceDefaults`; fold the 4 Folders-specific readiness-snapshot files into Server |
| `Hexalith.Folders.UI` | 41 / ~4.3k | FrontComposer-hosted ops console | **Keep** — Shell-reuse fixes, Fluent V5 adoption below the shell |
| `Hexalith.Folders.Aspire` | 2 / ~0.22k | Folders topology constants + thin sidecar composition | **Keep** — canonical home of the Folders↔Memories contract constants |
| `Hexalith.Folders.AppHost` | 1 / 118 | Local 6-service topology | **Keep** — allowed as the manual/automated test host for the module stack |
| `Hexalith.Folders.Testing` | 16 / ~0.76k | Domain test doubles + validated factories + slim test host | **Keep** — absorb ~110 duplicated helpers from test projects; `Eventually` → platform |
| *(proposed, optional)* `Hexalith.Folders.Providers` | new | Infra transport adapters (Forgejo HTTP, future GitHub/Octokit, secret-store adapter until the platform seam lands) | **Add** in Phase D — or fold adapters into Server/Workers to avoid a new gate entry |

### 3.2 Samples & tests

| Project | Verdict |
| --- | --- |
| `samples/Hexalith.Folders.Sample` (+`.Tests`) | **Keep** — current, correct generated-client golden-flow demo; no drift |
| `tests/Hexalith.Folders.Tests` (672 tests) | Keep — domain workhorse, clean |
| `tests/Hexalith.Folders.Contracts.Tests` (277) | Keep — but recognize it as the **repo governance/doc-gate project** (only 1 test exercises src code); it is the lockstep-update workstream for every move |
| `tests/Hexalith.Folders.Testing.Tests` (60) | Keep — really "RepoGovernance.Tests" (Scaffold/Fixture/exit-criteria gates + 14 genuine harness tests); do **not** rename (pin cost) |
| `tests/Hexalith.Folders.Server.Tests` (348), `IntegrationTests` (73), `UI.Tests` (304), `UI.E2E.Tests` (63 cases), `Client.Tests` (81), `Cli.Tests` (55), `Mcp.Tests` (64), `Workers.Tests` (63) | Keep — behavior suites; helper dedup applies |
| `tests/Hexalith.Folders.AppHost.Tests` (4, runtime-skip) | Keep — Tier-3 opt-in DCP harness (`HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`) |
| `tests/load` (NBomber) + `LoadTests.Tests` | Keep — hermetic capacity harness (C1/C2/C5 evidence) |
| `tests/shared/Parity` (5 files, file-linked into 5 csproj) | Keep — consider promoting to a real `Hexalith.Folders.Parity.Testing` project during the refactor |
| `tests/tools/parity-oracle-generator`, `pattern-examples`, `forgejo-drift`, `tests/contracts/forgejo`, `tests/fixtures` | Keep — all live, all Folders-specific; extract the generic YAML/spine-diff core only if the ecosystem standardizes parity oracles |

### 3.3 Dependency-direction notes

- `Hexalith.Folders` (domain) → only Contracts + `Dapr.Client` + `Octokit` + `Microsoft.Extensions.*` — the isolation at the heart of this report.
- Server/Workers correctly consume `EventStore.DomainService`, `Tenants.Client`, `Memories.Contracts`; Server additionally `Memories.Client.Rest` (Story 10.5 Option B, encoded in `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection`).
- Folders references **zero `Hexalith.Commons.*` packages directly** anywhere, despite `Hexalith.Commons.ServiceDefaults` and `Hexalith.Commons.UniqueIds` already being loaded in this repo's `.slnx`.

---

## 4. Boundary violations — code that belongs in technical modules

### 4.1 Root cause

The domain library's platform isolation forces re-implementation. The fix direction (per the boundary rule in `hexalith-llm-instructions.md`: *"if a capability is boilerplate shared across modules, do not duplicate it — reuse the technical module, or add it to that technical module first"*) is to **add the platform references and delete the re-implementations**, not to keep polishing local copies.

### 4.2 In `src/Hexalith.Folders` (domain library)

| # | Location | Violation (evidence) | Target |
| --- | --- | --- | --- |
| P1 | `Authorization/TenantAccess*.cs` + `Projections/TenantAccess/*` (18 files, ~494 LOC) | `FolderTenantAccessHandler.HandleAsync` is a line-for-line de-genericized copy of `Hexalith.Commons.TenantAccess.TenantAccessProjectionHandler` (same retry loop, malformed check, replay-conflict, watermark drop, `Apply` switch); `TenantAccessOutcome`≈`TenantAccessDenialKind`, `TenantAccessAuthorizer`≈`TenantAccessEvaluator`. The Commons package currently has **zero consumers ecosystem-wide** — it was extracted for exactly this migration | **Hexalith.Commons.TenantAccess** — keep only the `folders.`-key filter, folder-action→requirement map, and `FoldersTenantAccessEventMapper` |
| P2 | `Observability/FolderTelemetryEmitter.cs:14-15` | Self-declares `static ActivitySource`/`Meter` — the exact "telemetry source" the domain-authoring rule forbids; platform owns `EventStoreDomainDiagnostics` wired by `AddEventStoreDomainTelemetry` | **Hexalith.EventStore.DomainService** (inject diagnostics); keep the Folders instrument names/semantics (`folders.projection.lag`, C2 500 ms budget) |
| P3 | `Observability/FolderTelemetryEmitter.cs:171-175` | Ad-hoc `BoundedCategory` low-cardinality tag sanitization duplicating `Commons.Diagnostics.BoundedTelemetryCounter`/`BoundedMetricDimension` | **Hexalith.Commons.Diagnostics** |
| P4 | `Queries/ContextSearch/ContextSearchQueryHandler.cs:337-354`; `Queries/ProviderReadiness/InMemoryProviderReadinessEvidenceStore.cs:281-289` | Two hand-rolled **plaintext, unsigned** cursors (`memories-search:<int>`, `cursor_<int>`) — tamper-non-evident offsets; explicit rule: "do not hand-roll a cursor codec" | **`IQueryCursorCodec`/`QueryCursorScope`** + `AddEventStoreDataProtection` (EventStore.Client) |
| P5 | `Queries/*/InMemory*ReadModel.cs` (~20 files) + `Aggregates/*/InMemory*Repository.cs` | Bespoke `ConcurrentDictionary` stores are the **only production wiring** — lifecycle/audit/task read models are non-persistent | **`IReadModelStore` + `ReadModelWritePolicy`** for production; keep the `I*ReadModel` domain interfaces and the in-memory impls as test doubles |
| P6 | `SafeCorrelationId` + `CanonicalIdentifierPattern` (5 identical copies across Queries) | Generic correlation-id sanitizer; also 5 sites minting fallback ids via `Guid.NewGuid():N` instead of ULIDs | **Hexalith.Commons.Http** beside `HttpCorrelation` — `CorrelationId.SanitizeOrCreate(...)` on `UniqueIdHelper` |
| P7 | Secret/PII regexes: `Providers/Abstractions/ProviderCapabilityProfileFactory.cs:329-366`, `Queries/ProviderReadiness/InMemoryProviderReadinessEvidenceStore.cs:309-319`, `ProviderReadinessValidationService.cs:805-811`, `Providers/*/*SafeTargetFingerprint.cs` | 3–4 identical `[GeneratedRegex]` triples for GitHub-token/JWT/PEM shapes — generic, security-sensitive, already drifting | **Hexalith.Commons** `SensitiveValueDetector`/`SecretScrubber` |
| P8 | `ProviderCapabilityProfileFactory.cs:210-327` (+2 dups) | Generic length-prefixed SHA-256 deterministic-hash builder | **Hexalith.Commons** `DeterministicHashBuilder` (field lists stay in Folders) |
| P9 | `Providers/Forgejo/ForgejoAuthorizedBaseUrl.cs` | Generic URL hygiene (HTTPS-only, userinfo/token-in-query rejection, same-origin redirect) — nothing Forgejo-specific but the name | **Hexalith.Commons** `AuthorizedBaseUrl`; keep the `missing_forgejo_*` reason mapping |
| P10 | `Projections/TenantAccess/{IUtcClock,SystemUtcClock,FixedUtcClock}.cs` | Generic `TimeProvider` wrapper; folds into Commons' `ITenantAccessClock` with P1 | folds into P1 |
| P11 | `FoldersServiceCollectionExtensions.cs:204` | Domain module mints its own `new DaprClientBuilder().Build()` beside the SDK's registration | resolve `DaprClient` from the host |

Also confirmed: **zero** domain types implement `IDomainQueryHandler`/`IDomainProjectionHandler` (20 query handlers, all projections use bespoke shapes). That conformance migration is a deliberate epic or a documented sanctioned exception — see §12/§13 Phase H.

### 4.3 In `src/Hexalith.Folders.Server`

| Location | Violation | Target |
| --- | --- | --- |
| `Program.cs` (76 lines) | Not the 2-line domain-service host; `MapEventStoreDomainService` never called anywhere in `src/` — the 37-route bespoke REST surface is why | Near-term: keep shape, move authz into the SDK's **`IDomainServiceAdmissionStage`** seam (Tenants already migrated); full route migration deferred (§12) |
| `FoldersDomainServiceRequestHandler.cs` (193) | Re-implements the SDK's `DomainServiceRequestRouter` steps 5–6 with weaker semantics; steps 1–4 are exactly the admission-stage seam; contains byte-identical `BuildUnsupportedCommandDenial`/`BuildMalformedScopeDenial` pair | **Delete** in favor of admission stage + SDK routing |
| `FolderDomainProcessor.cs` (1,464) | Legit `IDomainProcessor` but registered non-keyed so the SDK router can't find it — a parallel structure; matches the platform's documented **legacy** pattern (EventStore Counter sample labels it so); accepts `currentState` it never reads | Simplify now (~-750); convention `EventStoreAggregate<TState>` long-term |
| `Authentication/FoldersAuthenticationServiceCollectionExtensions.cs` (171), `FoldersAuthSchemeValidator.cs` (70) | Full JWT-bearer/OIDC hardening + boot re-assertion — generic; the platform-conformant Tenants host does this in 2 lines + config convention | **Shared platform auth package** (see gap G6); only the `Folders:Authentication` section name is domain-specific |
| `Authentication/HttpContextEventStoreClaimTransformEvidenceAccessor.cs` (39), `HttpContextTenantContextAccessor.cs` (32) + interfaces | Read the **platform-defined** `eventstore:tenant`/`eventstore:permission` claims (`EventStoreClaimsTransformation.cs:13-15`) — generic EventStore claim plumbing | **Hexalith.EventStore.DomainService/Client** |
| `ContextSearch/MemoriesFolderSearchSource.cs` (176) | Split: the resilient-search wrapper half (timeout budget, `IsMemoriesUnavailable` classification, malformed-payload guard) is generic Memories bridging that Tenants also re-implements (`TenantQueryGateway`) | Wrapper → **Hexalith.Memories.Client.Rest**; keep the `folders://…` SourceUri grammar + `FoldersSemanticIndexingAttributes` filter mapping in Folders |

### 4.4 In `src/Hexalith.Folders.Workers`

| Location | Violation | Target |
| --- | --- | --- |
| `SemanticIndexing/MemoriesSemanticIndexingPort.cs` (245) | The raw `DaprClient.PublishEventAsync` + `cloudevent.id/type/source` metadata + Dapr-exception→retryable classification is generic CloudEvents egress; Tenants' sample `MemoriesSearchIndexEventPublisher` is the same plumbing (gap G1 — no platform publisher exists) | Publish primitive → **Hexalith.Memories** (`IIndexEventPublisher` + public producer constants); keep the Folders mapping (composite `FileVersionAggregateId`, hybrid hard/soft delete, full re-send semantics, `folders.status`) |
| `FoldersWorkersModule.MapFoldersTenantEvents` (L112-142) | Inlined copy of the SDK's `MapEventStoreDomainEvents` (identical `MapPost + WithTopic`, narrower result switch) | Call the SDK mapper |
| `FoldersSemanticIndexingEventProcessor.cs` (109) | Re-implements the SDK's event-type registry (FullName/AQN/Name fan-out) + deserialize/dispatch; justified only by dispatching `IFolderEvent` into the process manager | SDK-extension candidate (open an EventStore issue); acceptable interim keep |

### 4.5 `src/Hexalith.Folders.ServiceDefaults` — remove the project

`Extensions.cs` (144) is a **diverged stock Aspire template copy**: hardcoded `Hexalith.Folders` source names, OTel logs added, no health-probe trace filtering, and **incompatible probe paths** (`/health/live`+`/health/ready`+`/health` vs the platform convention `/health`+`/alive`+`/ready` used by EventStore, Memories, Commons, and the Tenants host). `Hexalith.Commons.ServiceDefaults` is the packable, options-driven shared library (Tenants deleted its own ServiceDefaults for it — Epic B2 precedent). The 4 readiness-snapshot files (`MonitoredSnapshotReadinessCheck` etc.) are Folders-specific (I-7/C2 lag budget) **but no production code registers a non-default `IReadinessSnapshotSource`** — the check always evaluates the vacuous `HealthyReadinessSnapshotSource`. Verdict: **delete the project**; layer on `AddHexalithServiceDefaults` (with `ConfigureHealthChecks` hook); move the 4 readiness files into Server (or delete them if the seam never gets a live producer). Friction: probe-path change touches deploy manifests; 3 test suites textually pin these files (`ProductionObservabilityConformanceTests`, `ServiceDefaultsHealthEndpointTests`, `ScaffoldContractTests`) plus a release-manifest exclusion row — this is a story, not an inline edit.

*Note on the rule tension:* platform instructions say a domain module "must not ship its own `*.AppHost`, `*.Aspire`, or `*.ServiceDefaults`"; current project guidance allows Aspire/AppHost projects needed to test the module stack. Resolution adopted here (record as an ADR): **keep AppHost + Aspire** (thin, platform-delegating, Epic-9-conformant, and the ecosystem siblings ship them), **delete ServiceDefaults** (pure duplication of a platform library).

### 4.6 In `src/Hexalith.Folders.UI`

Shell adoption at the top is correct (`MainLayout.razor` is 3 lines wrapping `FrontComposerShell`; no legacy v4/FAST tokens anywhere; no theme redefinition). Below the shell:

| Location | Violation | Target |
| --- | --- | --- |
| `Services/FoldersUserContextAccessor.cs:15-42` | Re-implements `IUserContextAccessor` (hardcodes `tenant_id`); Shell ships `ClaimsPrincipalUserContextAccessor`/`ServerCircuitUserContextAccessor`. Latent bug: sync-over-async `GetAwaiter().GetResult()` on the circuit (`:35-38`) | Delete + reuse Shell accessor |
| `Infrastructure/BearerTokenDelegatingHandler.cs:15-47` | Forwards `access_token` from `IHttpContextAccessor`; Shell ships `FrontComposerAccessTokenProvider`/`FrontComposerTokenRelay`. Latent bug: no `HttpContext` after the first Blazor-Server request → silently unauthenticated mid-circuit (`:31`) | Delete + reuse Shell token relay |
| `CompositionRoot.cs:102-154` + `Configuration/FoldersAuthenticationOptions.cs` | Hand-rolled OIDC+cookie boot recipe; Shell has `FrontComposerAuthenticationOptions`/`AddHexalithFrontComposerServerSecurity` | Simplify → Shell; keep only Folders values |
| `CompositionRoot.cs:176-216` `HermeticTestAuthenticationHandler` | Env-gated auth stub — reusable harness helper | **Hexalith.FrontComposer.Testing** |
| `Components/Icons/FoldersConsoleIcons.cs:19-29` | 7 hand-traced SVG path strings; `FcFluentIcons` already exposes 5 equivalents (`CheckmarkCircle16`, `Warning16`, `InfoCircle16`, `QuestionCircle16`, `DismissCircle16`); the code comment's justification (forbidden icons PackageReference) is moot — Shell already references that package | Add `LockClosed16`+`Clock16` to **FcFluentIcons** upstream, then delete |
| `SkeletonState.razor`/`StillLoadingCancel.razor`, `ConsoleEmptyState.razor`, `SafeCopyId.razor:8-16`+`CorrelationCopyButton.razor:12-19`, `DegradedModeBanner.razor:11-18` | Raw HTML/`<button>`/clipboard JS where Shell owns `FcProjectionLoadingSkeleton`, `FcLifecycleWrapper`, `FcProjectionEmptyPlaceholder`, `IClipboardJSModule` and Fluent owns `FluentButton`/`FluentMessageBar` | Shell components (`FcSafeCopy` is a new-upstream candidate) |
| ~35 `fc-*` CSS classes referenced, **defined nowhere** (project ships zero `.css`; not in FrontComposer's bundle) | Components render unstyled; dead styling hooks (`fc-trust-matrix`, `fc-skeleton-*`, `fc-safe-copy*`, …) | Eliminated by adopting the Shell components above |
| Raw `<table>` in 7 production pages (`TrustMatrix.razor:9`, `AuditTrail:48`, `IndexingStatus:41`, `OperationTimeline:49`, `IncidentStream:62`, `ProviderSupport:43`); raw `<label>/<input>/<button>` in `Pages/Folders.razor:53-57`, `FolderDetail.razor:68-71`; `Workspace.razor:34-127` (7 sibling `<h2>`) and `Provider.razor:54-211` with zero `FluentAccordion` | Reuse-rule + UX "page sections" violations; the dev galleries prove `FluentDataGrid` works here | `FluentDataGrid`, `FluentTextField`+`FluentButton`, `FluentAccordion` |
| `Services/ConsoleErrorPresenter.cs` | RFC-7807 extension parsing is generic | Parser → Shell/Commons; Folders category vocabulary stays |

### 4.7 In Client / Cli / Mcp

| Location | Violation | Target |
| --- | --- | --- |
| `Client/nswag.json:59` `"jsonLibrary": "NewtonsoftJson"` | 746 Newtonsoft references, 0 STJ in the generated client; ecosystem convention is STJ (`EventStore.Client`, `Memories.Client.Rest`); `csproj:16` makes Newtonsoft a public transitive dep of a packable SDK | Regenerate on **SystemTextJson** (deliberate story — hasher + ProblemDetails parsing are Newtonsoft-based; hash is wire-stable `sha256:` canonical text → add regression vectors) |
| `Client/Convenience/CorrelationAndTaskId.cs:84-116` | Hand-implements Crockford-Base32 ULID | **Hexalith.Commons.UniqueIds** `UniqueIdHelper` |
| `Client/Idempotency/HexalithIdempotencyHasher.cs:12-319` | Canonical-JSON + `sha256:` keying driven by the ecosystem-branded `x-hexalith-idempotency-equivalence` extension — nothing Folders-specific | **Hexalith.Commons** (move once, in final STJ shape) |
| `Cli/Credentials/CredentialResolver.cs:34-55` | Ecosystem-scoped names (`~/.hexalith/credentials.json`, `HEXALITH_TOKEN`) implemented inside a domain module; precedence **contradicts Memories** (Folders puts explicit `--token` lowest, Memories highest) | Future **Hexalith.Commons.Cli** (gap G4); harmonize precedence there |
| `Cli/Composition/BearerTokenHandler.cs:15-34` vs `Mcp/Composition/BearerTokenHandler.cs:24-49` (+ a 3rd copy in Memories) | Same shape 3×; the Folders copies **lack** the https/loopback guard Memories has | **Hexalith.Commons** (with the guard) |

---

## 5. Platform gaps — abstractions to introduce in technical modules **first**

These block or shape the migrations above. Each is "build in the technical module, then consume" work coordinated across submodule repos.

| Gap | Evidence | Proposed API / home |
| --- | --- | --- |
| **G1 — Memories index-update publisher** | No producer-side helper exists anywhere in Memories; Folders (`MemoriesSemanticIndexingPort.cs`) and the Tenants sample each hand-roll `DaprClient.PublishEventAsync("pubsub","memories-events",…)`; event `type` re-derived via `nameof(...)` because `CuratedSearchIndexEventTypes` is `internal` | `IIndexEventPublisher` + public producer constants in **Hexalith.Memories.Contracts/Client**; optionally layered on `Commons.Publication` (`PublicationTransportMetadata`, `PublicationDeduplicationSet`) |
| **G2 — Drifted Aspire Dapr helpers inside the platform** | `Commons.Aspire` and `EventStore.Aspire` hold independently drifted copies of the same 5 files (`AspireDaprDomainModuleOptions` 78 vs 72 ln, etc.); `RepositoryProjectPaths.cs` copied EventStore→Memories; only Tenants.Aspire correctly references instead of copying | Pick one home (recommend **Commons.Aspire** as the generic layer), delete the copies, point EventStore/Memories at it |
| **G3 — ServiceDefaults copies** | `Memories.ServiceDefaults` and `Folders.ServiceDefaults` are hand-rolled template copies with zero references to `Commons.ServiceDefaults` | Both layer on **Hexalith.Commons.ServiceDefaults** (EventStore.ServiceDefaults already does) |
| **G4 — Shared CLI scaffolding** | Three disjoint CLI stacks (EventStore.Admin.Cli: System.CommandLine + GlobalOptionsBinding + IOutputFormatter; Memories.Cli: own CliServices; FrontComposer.Cli: bespoke dispatcher, no System.CommandLine); Folders is the 4th copy and its `GlobalOptionsBinding.cs:8-10` *admits* "Mirrors the Hexalith.EventStore.Admin.Cli binding shape" | **Hexalith.Commons.Cli** (root-command builder, global options, output formatter table/JSON/CSV, exit codes, config layering, credential resolution with harmonized precedence) |
| **G5 — Shared MCP bootstrap** | Three bootstraps; only FrontComposer.Mcp is a reusable library; the JWT/tenant-auth + ProblemDetails-challenge + Dapr-invocation stack in `Memories.Mcp/Authentication/` is exactly what Folders re-copied | **Hexalith.Commons.Mcp** (or promote the Memories auth stack) |
| **G6 — Shared JWT/multi-tenant auth handlers** | `DaprAppIdHandler` exists **4×** (Tenants.Api, EventStore.Admin.UI, 2 EventStore samples); `InboundBearerForwardingHandler` **2×**; Folders Server hand-rolls OIDC hardening; the only packaged auth is Aspire-side + FrontComposer's bridge | Promote handlers into **Hexalith.EventStore.Client**; JWT-hardening + `eventstore:*` claim accessors into **EventStore.DomainService** or a shared auth package |
| **G7 — Cursor-paging envelope fragmentation** | Three shapes: Commons offset `PagedResult<T>` (duplicated verbatim in `Commons` and `Commons.Http`), `Tenants.Contracts.PaginatedResult<T>(Items,Cursor,HasMore)`, EventStore `QueryPagingOptions`/`QueryResponseMetadata` | One canonical cursor envelope (Commons or EventStore.Contracts); dedupe the two Commons `PagedResult<T>` copies |
| **G8 — Secret-store abstraction** | Neither EventStore nor Commons has one; Folders' `DaprProviderCredentialSecretStoreClient` (Dapr `GetSecretAsync` + `RpcException{PermissionDenied}` classification) is generic | `ISecretStoreClient` (Found/Missing/Denied/Unavailable) in **Hexalith.EventStore.Client** |
| **G9 — Small upstream additions** | `Eventually` async-poll helper (fully generic, 16 consumers, no platform equivalent); `LockClosed16`/`Clock16` icons; `FcSafeCopy` component; RFC-7807 extension parser; resilient Memories search wrapper (Folders + Tenants both clone it) | `Eventually` → **EventStore.Testing**; icons/`FcSafeCopy`/parser → **FrontComposer.Shell**; search wrapper → **Memories.Client.Rest** |

---

## 6. Duplication findings

### 6.1 Folders re-implementing the platform (delete after adoption)

Covered in §4: TenantAccess cluster (P1/P10), telemetry sources + bounded tags (P2/P3), cursor codecs (P4), read-model stores (P5), ServiceDefaults (§4.5), subscription mapping (§4.4), SDK request routing (§4.3), ULID generator, Shell accessor/token-relay/skeleton/empty-state/clipboard/icons (§4.6).

### 6.2 Copy-paste inside Folders (dedupe in place — no wire change)

| Family | Copies | Locations | Consolidation |
| --- | --- | --- | --- |
| `WithPayloadTenant` | **12 byte-identical** | 11 folder command services + `Organization/ConfigureProviderBindingService.cs:131` (`FolderCreationService.cs:158` … `WorkspaceCommitService.cs:193`) | one internal helper |
| `MapAuthorization(string)` | 10 byte-identical | all folder command services (`FolderCreationService.cs:171` …) | one internal mapper (pattern proven by `AuditMapping.cs:7`) |
| `MapAuthorizationDenial` layered→result-code switch | ~12 | Queries/* handlers (`WorkspaceStatus:342`, `FolderLifecycleStatus:349`, `ContextSearch:370`, `FolderIndexingStatus:178`, `WorkspaceFileContext:365`, `FolderScopedDiagnostics:202`, …) | one `LayeredAuthorizationDenialMapper` |
| Read-model status 6-arm switch + identical `catch when (ex is not OperationCanceledException)` + LogWarning | 10× + 14× | Queries/* + Authorization | `ReadModelOutcome<T>` mapper + `TryReadAsync` wrapper |
| GitHub↔Forgejo provider adapters | ~1,800 of ~3,300 LOC | `DaprBacked*CredentialResolver` 100% identical, `*CredentialLease` 100%, `*CredentialModeValidator` 98%, `*ReadinessMapper`/`*FailureMapper` 88% (13-case switch repeated 3× *within* each file), `*Provider` 86%, `*SafeTargetFingerprint` 83% | shared `ProviderAdapterCore<TFailureCondition>` in Folders |
| Server transport-helper suite (`SafeProblem`, `MessageFor`, `ReadHeader/ReadQuery`, `ClientTenantIds`, `IsCanonicalIdentifier`, `IsSensitiveDiagnosticValue`) | 4 copies, **already rotted apart** | `FoldersDomainServiceEndpoints`, `ProviderReadinessEndpoints`, `AuditEndpoints`, `OpsConsoleDiagnosticsEndpoints` — canonical-id grammar `^[a-z0-9._-]+$`/128 vs `^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$`; OpsConsole's secret filter is a **strict subset** (silently weaker) | one shared transport-helper class (~-600–750 lines, closes the security drift) |
| `ToHttpResult`/`To*HttpResult` overloads + `SafeProblem` sites | 22 overloads, 230 call sites | `FoldersDomainServiceEndpoints.cs` (overloads at 2034/3982/4258/4440/4565/4640/4738/4852/4957/5074/5305/5396/5494…; `SafeProblem` at :5534) | generic envelope mapper + table-driven code→status map (~-2,000 lines) |
| 13 write handlers ~80% identical | 13 | `ArchiveFolderAsync` L1416 … `CommitWorkspaceAsync` L3127 (validate envelope → read JSON → schema → gateway submit 4-arm try/catch → correlation echo → 202) | generic `SubmitCommand<TPayload>` skeleton (~-1,000 lines) |
| Canonical-identifier regex | ≥5 | `FoldersDomainServiceRequestHandler:25`, `FolderDomainProcessor:46`, `FolderCommandRejected:30`, `FoldersDomainServiceEndpoints:56`, `AuditEndpoints:684` | one shared validator (Contracts or Commons) |
| `FoldersTenantEventHandler` | 2 near-identical | `Server/FoldersTenantEventHandler.cs` (159) vs `Workers/Tenants/TenantEventHandlers/FoldersTenantEventHandler.cs` (151) — differ by namespace, one enum value, a log string, one ctor | one shared class parameterized by projection writer |
| Twin rejection records | 2 | `WorkspaceTransitionInvalidRejected.cs` / `DuplicateWorkspaceLockRejected.cs` (48 each, byte-identical except code+name; 3rd/4th clones of `FolderCommandRejected`'s canonicalizer) | shared base/canonicalizer |
| Cli↔Mcp plumbing | byte-identical / ~80% | `MetadataOnlyJson.cs` (identical), `BearerTokenHandler`, `MutationSourcing`/`QuerySourcing` structs, `CommandPipeline.cs:31-240` vs `ToolPipeline.cs:37-188`, `ParseFreshness`/usage-exception/body-reader | one shared adapter-core lib in Folders now; migrate onto Commons.Cli/Mcp when G4/G5 land |
| `SafeCorrelationId`, `HasClientControlledMismatch`, `IsClaimTransformEvidenceValid` | 5 / 5 / 3+1 | Queries + Authorization | Commons (P6) / dedupe in Folders `Authorization` |
| `WorkspaceStatusQueryHandler` inline validators + `CanonicalErrorCategories`/`LifecycleStates` HashSets | 2 | duplicated verbatim in `TaskStatusQueryHandler` | extract `WorkspaceSnapshotValidator` |

### 6.3 Test-helper duplication (~110 declarations, zero behavior change)

| Helper | Copies | Consolidation |
| --- | --- | --- |
| `FixedTimeProvider` (nested private) | **16** (Folders.Tests ×10, IntegrationTests ×4+Server, Workers.Tests:362, `tests/load/Scenarios/LifecycleCapacityDriver.cs:402`) | `Hexalith.Folders.Testing` |
| `StaticTenantContextAccessor` / `StaticClaimTransformEvidenceAccessor` | 22 / 18 (Server.Tests) | Testing |
| `RecordingEventStoreGatewayClient` | 9 (Server.Tests) + `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` | adopt platform `Hexalith.EventStore.Testing.Fakes.FakeEventStoreGatewayClient` as base |
| `Recording{EventStoreAuthorizationValidator,DaprPolicyEvidenceProvider,FolderPermissionEvidenceProvider}` | 10 each (Folders.Tests) | Testing |
| `Allowing{FolderPermissionEvidenceProvider,DaprPolicyEvidenceProvider}` / `AllowingEventStoreAuthorizationValidator` | 9 / 7 / 7 | Testing; note production already ships `AllowingEventStoreAuthorizationValidator` (itself slated to move to Testing, §7) |
| `RecordingFolderRepository` | 2 (Folders.Tests + re-implemented `Workers.Tests/RepositoryProvisioningProcessManagerTests.cs:311`) | Testing |
| `RepositoryRoot()` slnx-walker / `ForbiddenSentinelValues()` | ~40+ / 5 | one canonical helper in Testing |
| `ConsoleStubFixtures` ↔ `ConsoleSweepFixtures` | ~300 lines verbatim (`UI.E2E.Tests/Fixtures/ConsoleStubFixtures.cs:22-25` self-documents the replication) | Testing |
| `MutableTenantAndClaimContext` / `TestHost` record | 4 / 3 (IntegrationTests) | project-local shared file |

---

## 7. Obsolete / dead / deprecated code

No `[Obsolete]` attributes and no HACK/FIXME markers exist anywhere in scope. Findings:

**Delete now (zero references):**
- `src/Hexalith.Folders/Observability/NoOpFolderTelemetryEmitter.cs` — zero refs in src *and* tests.
- `src/Hexalith.Folders.Testing/FoldersTestingModule.cs` — zero consumers.
- Server dead code: `CommitWorkspaceAcceptedResponse` + `RetryEligibilityResponse` (defined, never constructed), `AuditEndpoints.MaxEntriesPerPage` (0 refs), `AuditEndpoints.ValidateSingleEnvelope` (pass-through wrapper), the ProviderReadiness `workspace_preparation` capability arm (parsed but omitted from the accept-list → always rejected).
- `src/Hexalith.Folders.Client/FoldersClientModule.cs:5-8` — 8-line class whose sole consumer is one smoke test.
- `_tmp_review_1_11_followup.diff` (59 KB untracked repo-root litter).
- **22 git-tracked `*.csproj.lscache` files** (IDE caches; `.gitignore:433-434` already covers the pattern) — `git rm --cached` all 22.

**Delete after verification:**
- `Aggregates/Folder/FolderCreateTenantGate.cs`, `Organization/OrganizationAclTenantGate.cs`, `OrganizationProviderBindingTenantGate.cs` — no `src/` callers (superseded by the `*Service` classes the Server composes); referenced only by tests.
- `RepositoryProvisioningProcessManager` — DI-registered but **no subscription/handler invokes `HandleAsync`** in `src/`; confirm it is wired to a trigger or mark it dormant scaffolding.
- `Mcp/Configuration/FoldersMcpOptions.cs` — never bound (`Program.cs:24,40-41` reads raw config keys; `BaseAddress`/`Auth` unread).
- `FolderCanonicalErrorMapper.cs` (117) — 3 of 4 methods called only by tests while endpoints hardcode statuses inline (a drifting parallel table) → fold into the shared error mapper.

**Test-only types shipping in the production `src/` library (move to `Hexalith.Folders.Testing`):**
`Authorization/AllowingEventStoreAuthorizationValidator.cs`, `Observability/InMemoryFolderAuditObserver.cs`, `Projections/TenantAccess/FixedUtcClock.cs`, `Projections/FolderAccess/FolderAccessProjection.cs` (zero src consumers — wire it into a real read model or delete).

**Deliberate placeholders — keep (governed, test-pinned):**
`RetentionClassToken = "TODO(reference-pending)…"` (`AuditTrailQueryHandler.cs:18`, `OperationTimelineQueryHandler.cs:18`; pinned by `AuditQueryHandlerTests.cs:59` + `OperationsAuditDocsConformanceTests.cs:206`); `FoldersContractMetadata.ContractVersion = "0.0.0-scaffold"` (a deliberate publish blocker pinned by `ReleasePackageConformanceTests.cs:229`); `RedactionDisclosureMapper.cs:17` `TODO(reference-pending C5)`; the two Server TODOs (`Program.cs:26` Epic-7 in-memory repo gate; `AuditEndpoints.cs:351` C4 filter stub); the `FailClosed`/`Unavailable*` worker defaults; `Compat/ChangedPathEvidenceShim.cs` (correct NSwag workaround — refresh its stale line-number comment).

**Stale docs (actively misleading):** `tests/Hexalith.Folders.UI.E2E.Tests/README.md:3,7,84,93-97` and `tests/README.md:66,124` still claim the E2E lane "has only a skipped placeholder test pending Epic 6" — reality is 63 active tests in two blocking CI jobs. Update both. Optional deletions: `IntegrationSmokeTests.cs` (`true.ShouldBeTrue()`), `FoldersModuleSmokeTests.cs`.

---

## 8. Over-engineered / unnecessarily complex code

| Item | Problem | Proposal |
| --- | --- | --- |
| Bespoke ProblemDetails machinery (§6.2) | 22 overloads × 230 sites × 4 helper copies for one wire envelope; `FolderAuthorizationDenialMapper.cs` (95) maintains a second hand-copy of the same envelope | Single envelope mapper + table-driven status map |
| `FolderDomainProcessor` (1,464) | 13-way `if(CommandType==…)` dispatch + 12 near-identical `Process*Async`; parallel to the SDK router | ~-750 now; convention aggregate later |
| `FolderAuditEndpointFilter.cs:98-262` | Endpoint-name-substring `OperationKind` classification + reflection response probing | Route metadata + marker interface (~-80 lines; makes it liftable) |
| `WorkspaceStatusQueryHandler.cs` (693) | ~25 inline `Is*` snapshot validators; HashSets duplicated in `TaskStatusQueryHandler` | Extract `WorkspaceSnapshotValidator` |
| `ContextSearchQueryHandler.cs:229-230` | Serializes the whole response **twice** for a self-referential byte budget; also a per-hit `GetFileVersionByIdAsync` N+1 | Single-pass with fixed-width placeholder; batch hydrate |
| `IEventStoreAuthorizationValidator` + 3 records | Single production impl is `DenyAll`; only other impl is test-only | Flag, don't delete (intended EventStore-validation plug point) — record intent in an ADR |
| `FolderResult` 14 static factories (11 near-identical `Accepted`) | Mild | Optional collapse; low priority |
| `*CredentialLease : IAsyncDisposable` | Blanks an immutable interned string — no real scrubbing | Simplify with `ProviderAdapterCore` |
| Generated-code verification scaffolding | ~200 generated lines of SHA-256 artifact checks guarding a failure mode the `BeforeCompile` targets already close; "Round 4 review finding P10"-style archaeology comments | Trim during the STJ regeneration story |
| Not over-engineering (explicitly correct) | The many 3–15-line enum/record files (one-type-per-file rule); fail-closed `DenyAll`/`NoOp`/`Unavailable*` single-impl DI defaults; provider `internal I*ApiClient` seams | Keep |

---

## 9. Correctness findings surfaced by the audit (fix as part of the relevant phase)

1. **`ForgejoHttpApiClientFactory.cs:17-29`** — `new SocketsHttpHandler` + `new HttpClient` per `CreateAsync` call, never disposed, no timeout/resilience → handler/socket leak on every readiness probe. Fix with `IHttpClientFactory` regardless of the package move. *(Phase D)*
2. **`UI/Infrastructure/BearerTokenDelegatingHandler.cs:31`** — Blazor Server circuits have no `HttpContext` after the first request → silently unauthenticated API calls mid-circuit. *(Phase E — replaced by Shell token relay)*
3. **`UI/Services/FoldersUserContextAccessor.cs:35-38`** — sync-over-async on the circuit. *(Phase E)*
4. **`OpsConsoleDiagnosticsEndpoints` `IsSensitiveDiagnosticValue`** — strict subset of ProviderReadiness's copy → weaker secret filter on the diagnostics surface. *(Phase C helper consolidation closes it)*
5. **`FolderStreamName` vs `OrganizationStreamName`** — `IsReservedSystemTenant` diverges (Folder: case-sensitive/no-trim, self-documented as anti-disclosure; Organization: `Trim()`+`OrdinalIgnoreCase`). Needs a **deliberate decision**, not a silent merge — the Organization variant may be the wrong one. *(Phase C)*
6. **Cli/Mcp `BearerTokenHandler`** — missing the https/loopback guard the Memories copy has (token can leak over plaintext HTTP). *(Phase F)*
7. **`RepositoryProvisioningProcessManager`** — possibly dormant (no trigger found). *(Phase C verification)*
8. **Plaintext unsigned cursors** (P4) and **in-memory-only production read models** (P5) — architectural, covered in Phase D.
9. **`DaprComponents/accesscontrol.yaml`** uses `defaultAction: allow` for all app-ids — weaker than the references' local deny-by-default. *(Phase G hardening)*
10. **Aspire SDK skew** — AppHost pins `Aspire.AppHost.Sdk` 13.3.5 vs platform 13.4.6. *(Phase A hygiene)*
11. **Testing factories mint Guid-hex ids** (`FoldersTestingModule`'s `FoldersTestDataFactory.cs:9-14,29-30`) where production ids are ULIDs — masks ULID-format bugs; `TestFolderContext.cs:37-52` hand-rolls the stream-name format duplicating `FolderStreamName.cs:52`. *(Phase C)*
12. Confirmed clean: **no `Guid.TryParse`/`Guid.Parse` on any aggregate/message/correlation/causation identifier anywhere** (grep-verified).

---

## 10. Test impacts

### 10.1 The gate topology (what any move must update, in lockstep)

- **Master lock:** `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` — `ExpectedSolutionProjects` (50 entries incl. 19 submodule projects, L44-96), `ExpectedRootPolicyProjects` (29 entries, L11-42), `ProjectReferencesFollowAllowedDependencyDirection` (L152-189, full allowed-reference set per project), `ForbiddenReferencesAreNotIntroduced` (L192-273). **Any project add/move/rename/reference change updates this file in the same commit.** Never rename `Hexalith.Folders.slnx` — it is the repo-root sentinel for ~40 files' `RepositoryRoot()` walkers.
- **Inventory pins outside the master lock:** `ReleasePackageConformanceTests.cs:21-36` (packaged set: Contracts/Folders/Client/Aspire/Testing packed; Cli/ServiceDefaults excluded), `BaselineCiWorkflowConformanceTests.cs:27-38` (9-project baseline allow-list), `TenantFolderProviderContractGroupTests.cs:338-366` (asserts `src/Hexalith.Folders.UI/Pages` does **not** exist + exact 5-file workflow allow-list — fails on *additive* changes).
- **Doc gates:** ~250 tests in Contracts.Tests read disk artifacts (OpenAPI YAML pinned by ~13 files; `.github/workflows/*.yml`; `docs/**`; `deploy/**`; `tests/fixtures/**`) with **raw unnormalized `ShouldContain`** (only 2 of 30 files have normalization helpers). Before any doc reorg, roll the whitespace-normalization helper pattern (`ConsumerDocsConformanceTests.cs:855-859`) suite-wide.
- **Self-referential pins:** `AdrRunbookDocsConformanceTests.cs:36` / `NfrTraceabilityConformanceTests.cs:37` read their own sources; `SecurityRedactionCiWorkflowConformanceTests.cs:39-56` pins 15 sibling test-method FQNs; `ContractParityCiWorkflowConformanceTests.cs:137-166` pins 16 test-class FQNs across 5 projects. Renaming test classes/projects self-breaks gates.
- **Behavior pins that must move with their subject:** route table (`ServerEndpointRegistrationTests.cs:21-56`), `ImplementedRestOperationCount = 49` (`TransportParityConformanceTests.cs:64`), Dapr app-ids/topics/routing + platform image/port literals (`AspireTopologyTests.cs:22-31,209-215,414-420,469` — the `redis/redis-stack`/`falkordb` image and 3502/50002 port pins will break on any legit platform bump; loosen), adapter category counts + `InternalsVisibleTo` compile coupling (`CrossAdapterBehavioralParityTests.cs:159-160`), `IFolderEvent` 18-type reflection inventory (`FolderLifecycleReplayDeterminismTests.cs:76-106`), FrontComposer JS asset paths (`ShellCompositionTests.cs:54-57`).
- **E2E rename cost:** renaming/moving `UI.E2E.Tests` breaks **10 pin sites** (`tests/install-playwright.ps1:12,26`; `tests/tools/run-e2e-ci-gates.ps1:30,45,60-64` incl. the count pin "63"; `run-accessibility-ci-gates.ps1:28-29,44,53`; `tests/run-tests.ps1:28`; 3 Contracts.Tests files; `ScaffoldContractTests.cs:36,90,177`). Avoid renaming test projects.

### 10.2 Three genuinely fragile pins to fix early (they punish unrelated refactors)

1. `FoldersProductionAuthenticationTests.cs:127-137` — reads `Server/Program.cs` **as text** and asserts middleware ordering via `IndexOf` → replace with a behavioral assertion.
2. `GitHubDependencyGuardTests.cs:43-45` — asserts `references/Hexalith.Builds/Props/Directory.Packages.props` contains `Octokit Version="14.0.0"` **verbatim** → assert presence-of-pin, not the number (and it becomes moot when Octokit is dropped).
3. `ArchiveFolderProcessWiringTests.cs:720-726` — mirrors `DeriveCreateFolderId` logic.

### 10.3 Tests to update / delete per migration

- **Platform adoption (Phases D/E):** tests of the deleted local TenantAccess/cursor/read-model/ServiceDefaults code get deleted or re-pointed at the Folders-specific mappers that remain; `ServiceDefaultsHealthEndpointTests` + `ProductionObservabilityConformanceTests` + the release-manifest exclusion row update with the ServiceDefaults removal; probe-path change updates deploy manifests + their gates.
- **Near-zero platform re-testing exists today** — the only real overlap is the 9 hand-rolled `RecordingEventStoreGatewayClient` copies vs the platform's `FakeEventStoreGatewayClient` (adopt; ~10 files, no test-count change). UI's single shell seam test correctly asserts Folders wiring, not the shell — keep.
- **Helper consolidation** (§6.3) first: it shrinks every later diff.
- **AppHost.Tests** stays Tier-3/DCP-gated; Aspire resource renames break it silently until the lane runs — include it in every topology-touching story's checklist.

---

## 11. Classification — keep in Folders vs move

### Keep in Folders (domain-correct)
- Aggregates (`FolderAggregate`/`OrganizationAggregate`, pure `Handle`/`Apply`, idempotency-replay dedupe), all commands/events/value objects, `FolderResultCode` vocabulary, secret-detection command validators.
- The layered authorization model (`EffectivePermissions*`, `LayeredFolderAuthorization*`, `LayeredFolderOperationPolicy`, `DaprPolicyEvidence*` cluster) and the Server's `Authorization/` policy files (action-token mapper, archive ACL/evidence providers, scoped result accessor).
- `FolderList` + `SemanticIndexing` projections (bridge evidence/watermark/tombstone, hard-delete-wins rules) — correctly built on `IReadModelStore`/`ReadModelWritePolicy` in Workers.
- The provider **port** (`IGitProvider` + request records + `ProviderFailureCategory` taxonomy + `ProviderCapabilityDiscoveryService`) and `GitHubProvider`/`ForgejoProvider` as domain clients (transport adapters move; the port does not).
- Folders-specific egress mapping (composite `FileVersionAggregateId`, hybrid remove/archive, `folders://` URI grammar, `folders.status` attribute contract, `FoldersSemanticIndexingDefaults`).
- `Hexalith.Folders.Aspire` constants + `AppHost` topology; the `Program.cs` safety gates (in-memory-repo env gate, `FolderRepositoryStartupAssertion`, `ValidateOnBuild/ValidateScopes`).
- UI presentation vocabulary (all `Services/*` mappers + enums, `OperatorDispositionBadge`, `TechnicalStateMetadata`, `RedactedField`, `MainLayout`), Client convenience layer (`FileUpload*`, streaming rules), Cli/Mcp command/tool inventories + oracle-proven parity maps, `Hexalith.Folders.Testing` domain doubles + validated factories + slim test host.
- Governed placeholders (retention TODO tokens, `0.0.0-scaffold` gate) and fail-closed DI defaults.
- All test tooling (parity oracle generator, pattern examples, forgejo drift fixtures, load harness).

### Move to technical modules
- **Hexalith.Commons:** TenantAccess consumption (delete P1 copy), correlation sanitizer (P6), secret/PII detectors (P7), deterministic hash builder (P8), `AuthorizedBaseUrl` (P9), idempotency hasher (post-STJ), ULID usage via `UniqueIdHelper`, bearer handler with https guard, future `Commons.Cli`/`Commons.Mcp` scaffolding (G4/G5), ServiceDefaults consumption (G3).
- **Hexalith.EventStore:** telemetry sources (P2) + admission-stage authz (§4.3), cursor codecs (P4), read-model stores (P5), subscription mapping, `ISecretStoreClient` (G8), tenant/claim accessors + JWT hardening (G6), `Eventually` → EventStore.Testing, `FakeEventStoreGatewayClient` adoption.
- **Hexalith.FrontComposer:** user-context accessor, token relay, OIDC boot options, `HermeticTestAuthenticationHandler` → FrontComposer.Testing, icons (`LockClosed16`/`Clock16` upstream), skeleton/empty-state/safe-copy/banner components, RFC-7807 extension parser.
- **Hexalith.Memories:** `IIndexEventPublisher` + public producer constants (G1), resilient search wrapper (from `MemoriesFolderSearchSource`).

### Delete
`Hexalith.Folders.ServiceDefaults` (project), the Octokit stub + package reference, dead types/files listed in §7, the 22 `.lscache` files, `_tmp_review_1_11_followup.diff`.

### Deferred (explicit epics, not this refactor)
REST surface → `RestApi.Generators` (37 routes, envelope + header changes, ~33 pinned test files + `docs/sdk/api-reference.md`); `IDomainQueryHandler`/`IDomainProjectionHandler` conformance (or a documented sanctioned-exception ADR); Client `Generation` extraction to a platform client-generator (trigger: a 2nd module adopts `x-hexalith-idempotency-equivalence`).

---

## 12. Risks, migration order, compatibility

| Risk | Mitigation |
| --- | --- |
| **Gate lockstep breakage** — every csproj/route/doc change trips conformance tests | Sequencing rule (§10.1): ScaffoldContractTests lists → ReleasePackage/BaselineCi inventories → `run-*.ps1` + workflow FQN pins → `.slnx`, all in the same commit as the move |
| **Cross-repo coordination** — platform-first work lands in 4 submodule repos, each with its own review/release; baseline CI without submodules has no PackageReference fallback | One platform story per gap (G1–G9), landed and pinned via submodule bumps before the consuming Folders story starts; keep each Folders story consumable against a pinned submodule SHA |
| **Packable API breakage** — `Hexalith.Folders`, `Contracts`, `Client`, `Aspire`, `Testing` are packable; moving public types is a breaking package change | Publishing is still gated by `0.0.0-scaffold`, so breaks are cheap **now** — do the moves before first publish; note SemVer major if that gate lifts first |
| **Wire-contract stability** | Phases A–F change **zero** wire behavior (the ~4k Server dedup is envelope-preserving; STJ regeneration keeps the `sha256:` idempotency hash wire-stable — add regression vectors). Envelope changes live only in the deferred RestApi.Generators epic with a contract version bump |
| **Cursor opacity change** (P4) | Cursors are contractually opaque; switching to DP-sealed codecs invalidates in-flight cursors — acceptable pre-GA; document in release notes; land with `AddEventStoreDataProtection` so cursors survive rollouts thereafter |
| **Probe-path change** (`/health/live|ready` → `/health|/alive|/ready`) | Update deploy manifests + `deploy/**` gates + `ServiceDefaultsHealthEndpointTests` in one story |
| **CS1591 explosion** — wiring `GenerateDocumentationFile` repo-wide under `TreatWarningsAsErrors` surfaces hundreds of missing XML docs at once | Staged: enable per-project, backfill docs project-by-project (Providers' 96 files and Contracts' 18 types are the known bulk) |
| **Doc-gate prose fragility** | Roll the normalization-helper pattern across the 30 gate files before touching docs |
| **Newtonsoft→STJ regen churn** (14k generated lines) | Single deliberate story; generated diff reviewed via the checked-in `.g.cs`; Compat shim (`ChangedPathEvidenceShim`) revalidated |
| **DCP lane still blocked** | AppHost.Tests stays SKIP; live `aspire run` verification remains an environment action item, not a refactor gate |

**Migration order rationale:** platform-first (the boundary rule requires it, and it unblocks everything), then wire-preserving in-repo dedup (high ROI, zero contract risk, shrinks every later diff), then domain→platform adoption, then hosting/UI/client alignment, with the contract-changing work explicitly deferred. Phases B, C, E, F are largely parallelizable; D depends on B; G depends on D.

---

## 13. Action plan (ordered, practical)

### Step 1 — Establish baseline build/test status *(Phase A start)*
- `dotnet restore Hexalith.Folders.slnx && dotnet build Hexalith.Folders.slnx -c Release` (expect 0 warnings/errors).
- Run each test project individually (per repo rule; solution-level `dotnet test` is not used): Folders, Contracts, Server, Workers, Cli, Mcp, Client, UI, UI.E2E (via `tests/tools/run-e2e-ci-gates.ps1`), IntegrationTests, Testing.Tests, LoadTests.Tests; AppHost.Tests expected 3 SKIP.
- `dotnet format whitespace --verify-no-changes` + `dotnet format analyzers --verify-no-changes`.
- Record counts as the baseline table; confirm `ScaffoldContractTests` green (it is at `533806b`).

### Step 2 — Inventory projects and dependencies
- Done — §3 of this report is the inventory; §10.1 is the pin map. Commit this report; optionally add `Hexalith.Folders.Client.Generation.csproj` to the `.slnx` (with the ScaffoldContractTests lockstep edit) for IDE completeness.

### Step 3 — Identify duplicate/common code
- Done — §6. Use it as the checklist; re-verify each family with a fresh diff before editing (code moves since audit).

### Step 4 — Classify each candidate
- Done — §11. Ratify the two open decisions with the team: (a) keep-AppHost/Aspire + delete-ServiceDefaults ADR (§4.5); (b) `FolderStreamName` reserved-tenant semantics (§9.5).

### Step 5 — Update/create abstractions in technical modules *(Phase B — platform-first, one story per repo)*
1. **Memories:** `IIndexEventPublisher` + public producer constants; resilient search wrapper in `Client.Rest` (G1, §4.3).
2. **EventStore:** `ISecretStoreClient` (G8); promote `DaprAppIdHandler`/`InboundBearerForwardingHandler` + JWT-hardening + `eventstore:*` claim accessors (G6); `Eventually` → `EventStore.Testing` (G9); optional: keyed-processor/admission-stage docs for the Folders migration.
3. **Commons:** `SensitiveValueDetector`, `DeterministicHashBuilder`, `AuthorizedBaseUrl`, `CorrelationId.SanitizeOrCreate`; dedupe the two `PagedResult<T>` copies (G7 first slice); resolve the Commons.Aspire↔EventStore.Aspire drifted-copy split (G2); open the `Commons.Cli`/`Commons.Mcp` proposal with harmonized credential precedence (G4/G5 — proposal now, implementation can trail).
4. **FrontComposer:** `LockClosed16`+`Clock16` in `FcFluentIcons`; `FcSafeCopy`; RFC-7807 extension parser; `HermeticTestAuthenticationHandler` → `FrontComposer.Testing` (G9).
5. Bump the `references/` submodule pins in Folders as each lands (conventional `chore(deps):` commits).

### Step 6 — Migrate common code out of Folders *(Phases C–D)*
**Phase C — wire-preserving in-repo dedup (no platform dependency, start immediately in parallel with B):**
- Repo hygiene: `git rm --cached` the 22 `.lscache` files; delete `_tmp_review_1_11_followup.diff`; fix the two stale test READMEs; fix the 3 fragile pins (§10.2); align `Aspire.AppHost.Sdk` to 13.4.6.
- Server mechanical dedup (~4,000 lines): shared transport-helper class (closes the OpsConsole secret-filter drift), envelope mapper + status table, `SubmitCommand<TPayload>` skeleton, canonical-id validator, merge `FoldersTenantEventHandler`, twin rejection records, fold `FolderCanonicalErrorMapper`.
- Domain dedup: `WithPayloadTenant`/`MapAuthorization`/denial-map/read-model-outcome helpers; `ProviderAdapterCore<TFailureCondition>` (~-1,800); resolve the stream-name divergence per Step 4(b); extract `WorkspaceSnapshotValidator`; fix ContextSearch double-serialize + N+1.
- Cli↔Mcp: extract the byte-identical plumbing into one shared internal lib; add the https/loopback guard to the bearer handlers.
- Tests: consolidate the ~110 duplicated helpers into `Hexalith.Folders.Testing`; adopt `FakeEventStoreGatewayClient`; switch test-data factories to ULIDs; delegate `TestFolderContext` to `FolderStreamName.Create`; verify `RepositoryProvisioningProcessManager` wiring.

**Phase D — domain adopts the platform (after B lands):**
- Add `Hexalith.Commons.*` + `Hexalith.EventStore.Client` references to the domain csproj (ScaffoldContractTests dependency-direction lockstep).
- P1/P10 TenantAccess collapse onto Commons; P2/P3 telemetry onto `EventStoreDomainDiagnostics` + `BoundedTelemetry*`; P4 cursors onto `IQueryCursorCodec` + `AddEventStoreDataProtection`; P5 production read models onto `IReadModelStore`+`ReadModelWritePolicy` (in-memory impls demoted to test doubles); P6–P9 onto the new Commons APIs.
- Delete the Octokit stub + `Octokit` PackageReference (+ retire `GitHubDependencyGuardTests` pin); move the Dapr secret client behind `ISecretStoreClient`; stop `new DaprClientBuilder()`; move the Forgejo HTTP transport onto `IHttpClientFactory` (fixes the leak) inside the new `Hexalith.Folders.Providers` infra project (or Server/Workers if the team prefers no new gate entry).
- Evict the test-only types from `src/` into Testing; delete the dead types (§7).

### Step 7 — Simplify Folders to consume the technical modules *(Phases E–F)*
**Phase E — hosting & UI:**
- Server: move authorization into an `IDomainServiceAdmissionStage`; delete `FoldersDomainServiceRequestHandler`; simplify `FolderDomainProcessor` (~-750); adopt the shared auth package from G6; split `MemoriesFolderSearchSource` (wrapper → Memories, mapping stays).
- Workers: `MapFoldersTenantEvents` → SDK `MapEventStoreDomainEvents`; `MemoriesSemanticIndexingPort` → `IIndexEventPublisher` (mapping stays); file the SDK-extension issue for the event processor.
- **Delete `Hexalith.Folders.ServiceDefaults`** → `Hexalith.Commons.ServiceDefaults` (probe-path change + deploy manifests + the 3 pinning test suites + release-manifest row, one story); move the 4 readiness files into Server or delete.
- UI: Shell accessor/token relay/OIDC options adoption (fixes bugs §9.2/9.3); icons → `FcFluentIcons`; skeleton/empty-state/safe-copy/banner → Shell (kills the 35 undefined `fc-*` classes); tables → `FluentDataGrid`; inputs → `FluentTextField`/`FluentButton`; `FluentAccordion` on Workspace/Provider; one page-lifecycle base for the 7 duplicated page scaffolds.
- Harden `DaprComponents/accesscontrol.yaml` to deny-by-default.

**Phase F — client modernization:**
- NSwag `jsonLibrary` → `SystemTextJson`; rewrite hasher + ProblemDetails on `JsonNode`; hash regression vectors; then move the hasher to Commons; replace the hand-rolled ULID with `UniqueIdHelper`; drop Newtonsoft from the packable surface; trim generated verification scaffolding; delete `FoldersClientModule`, bind-or-delete `FoldersMcpOptions`; split one-type-per-file violations (`CorrelationAndTaskId`, `HexalithIdempotencyHasher`, `YamlContractLoader`, `ProviderReadinessModel`, `CommandPipeline`, `ToolPipeline`, `FoldersMcpOptions`).
- Repo-wide: wire `GenerateDocumentationFile` (staged per project) and backfill XML docs.

### Step 8 — Remove obsolete code and obsolete tests
- Rolled into Phases C–F above (§7 is the checklist); after each deletion, remove/re-point the tests that pinned the deleted code, honoring the §10.1 lockstep rule.

### Step 9 — Update documentation and samples
- Sample is current — verify it still compiles against the STJ client and update only if the generated surface shifted.
- Update `docs/sdk/`, ADRs (new: sanctioned-exception ADR for AppHost/Aspire + the query-handler-conformance decision), runbooks touched by probe paths, and the two stale test READMEs. Before any doc edit, land the normalization-helper sweep across the 30 doc-gate files.

### Step 10 — Focused tests per project, then restore/build the `.slnx`
- After each story: run that project's test suite + `Testing.Tests` (gates) + `Contracts.Tests` (gates); then `dotnet restore Hexalith.Folders.slnx && dotnet build -c Release` and `dotnet format … --verify-no-changes`.
- After each phase: the full per-project lane sweep from Step 1.

### Step 11 — Final verification checklist *(Phase G close-out)*
- [ ] `dotnet build Hexalith.Folders.slnx -c Release` — 0 warnings/errors (warnings-as-errors).
- [ ] All test projects green individually, counts ≥ baseline minus deliberately deleted tests (each deletion traced to a §7/§10.3 line item); AppHost.Tests 3 SKIP (or green if the DCP lane exists by then).
- [ ] `ScaffoldContractTests` green with the **new** inventories (ServiceDefaults removed; Providers added if created; domain references extended).
- [ ] `ReleasePackageConformanceTests`, `BaselineCiWorkflowConformanceTests`, dependency-direction and forbidden-reference gates green.
- [ ] `dotnet format whitespace` + `analyzers` verify-no-changes.
- [ ] `src/Hexalith.Folders.csproj` contains **no** `Octokit`/`Dapr.Client` PackageReference; references `Hexalith.Commons.*` and `Hexalith.EventStore.Client`.
- [ ] `Hexalith.Folders.ServiceDefaults` gone from disk, `.slnx`, gates, and deploy manifests; probe paths are `/health`+`/alive`+`/ready` everywhere.
- [ ] No hand-rolled cursor strings, `ActivitySource`/`Meter` declarations, tenant-access handler copies, or Dapr subscription `MapPost+WithTopic` blocks remain in Folders (grep sweep).
- [ ] Generated client is STJ-only; idempotency-hash regression vectors pass; Newtonsoft absent from the package graph.
- [ ] UI: zero undefined `fc-*` classes, zero raw `<table>`/`<button>`/`<input>` on production pages, zero hand-authored SVG paths.
- [ ] 22 `.lscache` files untracked; no repo-root litter; docs/READMEs current.
- [ ] E2E (63) + accessibility gates green; capacity smoke green.
- [ ] Every commit conventional (`npx commitlint --last --verbose` spot-check); submodule bumps use `chore(deps):`.
- [ ] ADRs recorded: AppHost/Aspire sanctioned exception; ServiceDefaults deletion; query-handler-conformance decision; stream-name reserved-tenant decision.

---

## Appendix — audit provenance

Five independent audits, each reading source directly (no findings accepted from file names alone): **A** domain core (`Hexalith.Folders`, `Contracts`, `Testing`); **B** hosting (`Server`, `Workers`, `ServiceDefaults`, `Aspire`, `AppHost`, samples); **C** front/clients (`UI`, `Client`+Generation, `Cli`, `Mcp`); **D** tests & tooling (all 16 test/tool projects, fixtures, gate scripts); **E** technical-module API catalog (Commons 12 packages, EventStore SDK/Client/Testing/Aspire/Generators, FrontComposer Shell/Testing/SourceTools/Mcp, Tenants Client/Contracts/Testing, Memories Contracts/Client.Rest/Aspire). Load-bearing cross-cutting claims (zero platform refs in the domain csproj, Commons.TenantAccess zero consumers, `.lscache` tracking, ScaffoldContractTests green, drifted Aspire helper copies) were verified by at least two audits or a direct check.
