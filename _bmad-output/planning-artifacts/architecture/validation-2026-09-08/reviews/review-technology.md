# Technology reality review — 2026-09-08

Verdict: **Needs update before serving as the current implementation contract.** The selected ecosystem and major safety boundaries have concrete repository evidence, but several mandatory instructions contradict the current topology, generated wire contract, build modes, or validation machinery. The existing NOT READY production posture remains supported; this review does not establish production readiness.

Scope: the full 1,766-line `_bmad-output/planning-artifacts/architecture.md`, current source/configuration at root HEAD `9038e04`, and the explicitly retained legacy document format. The July 19 memlog requires update-in-place with stable D-/S-/A-/F-/I-/C- identifiers; there is no AD-format or migration penalty here. References below are repository-relative `file:line` locations.

Method: read-only inspection of repository instructions, build imports and pins, AppHost and host registrations, public contract, deployment policies, policy runner, and relevant implementation seams. One external lifecycle claim was checked against Microsoft documentation on September 8. No build, test suite, Aspire boot, live provider probe, or deployment was run. Source and test-runner inspection proves structure and configured behavior only, not that a lane passed.

## High findings

### TECH-01 — The documented event topic disconnects independently built producers and consumers

Classification: binding invariant drift. Disposition: **autofix in a subsequent Update**, adopting the implemented topic exception and keeping tenant authorization/partitioning explicit.

The communication rules prescribe `{tenantId}.{domain}.events`, and the integration flow specifically prescribes `{tenantId}.folders.events` for worker delivery (`architecture.md:822`, `:1514`, `:1539`). The actual Folders topology deliberately overrides the folders domain to the shared **`folders.events`** topic. The worker subscribes only to that fixed topic; production publishing and subscription scopes permit it, not arbitrary tenant-specific folder topics.

Evidence:

- `src/Hexalith.Folders.AppHost/Program.cs:34` documents and applies the override at line 40.
- `src/Hexalith.Folders.Workers/SemanticIndexing/FoldersSemanticIndexingDefaults.cs:16` sets `DomainEventsTopicName = "folders.events"`.
- `deploy/dapr/production/sidecar-config-bindings.yaml:16` explicitly states that without this override the worker never receives folder events; lines 24–25 set the production override.
- `deploy/dapr/production/pubsub.yaml:13`, `:19`, `:21` bind the allowed topic and publisher/subscriber scopes.

Two implementations following the architecture literally can therefore miss the deployed subscriber or fail the production topic policy. Record the fixed Folders-domain topic as an exception to the generic EventStore convention in the communication rules and lifecycle diagram. Managed tenant identity remains in the trusted envelope and storage/authorization boundary; the shared topic is not itself tenant isolation.

### TECH-02 — I-3 reports a live Dapr denial gate that the configured runner explicitly leaves unproved

Classification: actual verification capability gap plus overstated architectural evidence. Disposition: **discuss**, retain a release-blocking open item with a precise owner and proof condition; correct the present-tense validation claim in an Update.

I-3 says a CI job runs `daprd` in kind, asserts unauthorized invoke/pubsub triples return 403, and supplies exhaustive negative coverage (`architecture.md:641`). The test-pattern and handoff sections repeat live negative-test and pre-merge requirements (`:953`, `:1735`). The checked-in scheduled lane does not do this. It calls a static policy gate and always emits `reference_pending_story_7_8` for live kind/Dapr denial, while the overall report may still be `passed`.

Evidence:

- `.github/workflows/policy-conformance.yml:3` has scheduled/manual triggers; `:54` invokes the scheduled wrapper.
- `tests/tools/run-scheduled-policy-conformance-gates.ps1:100` records the live lane as reference-pending and defines the required future synthetic-app denial proof.
- The same runner at `:187` expects that reference-pending state, at `:247` emits it as a warning with exit code 0, and at `:249` emits overall `passed`.
- `tests/tools/run-dapr-policy-conformance-gates.ps1:54` likewise reports `live_dapr_kind_gate = 'reference_pending_story_7_8'`.

Static YAML and negative-triple fixture checks are useful but cannot demonstrate runtime enforcement, mTLS identity, app-port bypass prevention, or actual pub/sub denial. The architecture must distinguish static-policy conformance from runtime security proof and must not treat the former's green status as satisfaction of I-3. This is not a claim that an exploit was observed.

### TECH-03 — D-9 prohibits the encoding that the authoritative public contract generates

Classification: binding wire-contract drift. Disposition: **discuss** the ratified contract authority; update D-9 to the settled contract without silently changing client wire behavior.

D-9 explicitly rejects base64 and tells clients to use the `x-hexalith-retry-as` response header for inline-to-stream fallback (`architecture.md:549`). The actual Contract Spine declares base64 content and the separate response header `X-Hexalith-Retry-Transport`. The architecture's later header table already uses that separate response header (`:792`), so even the document gives two incompatible implementations.

Evidence:

- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:8488` declares inline `contentEncoding: base64`; `:8490` specifies 262,144 decoded bytes and 349,528 encoded characters.
- Contract `:2087`–`:2089` specifies `X-Hexalith-Retry-Transport: stream` for HTTP 413.
- Contract `:5244` names `X-Hexalith-Retry-As` as the distinct request-side field; `:5368` explains why the two headers are disjoint.
- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:31`–`:38` generates the client directly from that contract.

A new client or test written against D-9 can send the wrong content encoding or inspect the wrong retry header. This is a public interoperability issue, not harmless starter-directory drift.

### TECH-04 — Package-only prohibition contradicts the implemented Release mode and current baseline

Classification: superseded baseline conflict with build-seed drift. Disposition: **autofix in a subsequent Update** for the architecture; separately preserve any existing CI implementation gap instead of rewriting pipeline behavior during validation.

The starter instructions say Hexalith dependencies are project references, are not pinned as packages, and must not be replaced with package references (`architecture.md:445`). D-1 and the implementation sequence repeat the unconditional source-reference posture. Current build configuration explicitly selects source references for Debug and NuGet packages for Release, and the required Hexalith baseline requires package/Release CI.

Evidence:

- `Directory.Build.props:9` describes the dual mode; `:15`–`:26` selects it and sets per-module source flags.
- `Directory.Packages.props:5`, `:11` import the central Hexalith.Builds pins.
- `references/Hexalith.Builds/Props/Directory.Packages.props:8` pins EventStore 3.103.0; `:11` pins Tenants 5.7.0.
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:20`–`:25` contains conditional project/package alternatives.
- `references/Hexalith.AI.Tools/hexalith-llm-instructions.md:242`–`:249` requires local Debug/project references and CI Release/package references.

Keep one explicit mode rule and point seed versions at repository configuration. Do not restore the old project-only instructions. There is also a distinct implementation observation: `.github/workflows/policy-conformance.yml:46` and `:49` invoke restore/build without Release, and `.github/workflows/ci.yml:69` and `:73` do likewise in the contract/parity lane. Merely fixing the prose would not prove all CI lanes conform to the current baseline.

## Medium findings

### TECH-05 — The search-bridge blocker is partly obsolete, while the underlying production data-plane blocker is still real

Classification: delivery-status drift, with a separately confirmed capability gap. Disposition: **autofix the factual status in a subsequent Update**; retain the durable end-to-end release requirement.

The architecture says Server leaves `UnavailableSemanticIndexingBridgeReadModel` registered and that the concrete EventStore store lives only in Workers (`architecture.md:181`; also `:147`–`:150`). Current Server explicitly replaces that fallback with `EventStoreSemanticIndexingBridgeStore`.

Evidence:

- `src/Hexalith.Folders.Server/FoldersServerModule.cs:74` invokes `AddFoldersContextSearchFacade`.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:129`–`:134` adds the EventStore read-model store, removes the fallback, and registers the real bridge for Server reads.
- `src/Hexalith.Folders.Server/Program.cs:24`–`:31` still registers the only folder repository only in Development/Staging and adds the startup assertion; `:59`–`:69` still rejects missing or in-memory Production repositories.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1257`–`:1276` still returns an empty-event result for accepted/created/replayed folder results; `:1340` makes the empty-event shape explicit.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:64`, `:67`, `:70` still register unavailable file-content, commit-executor, and context-source seams.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:84` and `:102` retain in-memory transition and diagnostics models.

Therefore update the completed registration fact without declaring FR58, the durable lifecycle, or Production boot complete. The blanket July statement that Git writes throw `NotImplementedException` (`architecture.md:201`) also needs a current mechanism: the default executor now returns a known unsupported-capability result (`src/Hexalith.Folders/Aggregates/Folder/UnavailableWorkspaceCommitExecutor.cs:10`). The capability remains unavailable, but the stated implementation detail is stale.

### TECH-06 — The explicit stack pins and compatibility assertion describe a different dependency set

Classification: build-seed drift. Disposition: **autofix in a subsequent Update**, replacing repeated historical pins with a dated configuration reference and recording preview use accurately.

The architecture binds Aspire 13.4.6, toolkit 13.4 preview, MCP 1.3.0, and claims EventStore/Tenants 3.15.1 compatibility (`architecture.md:571`, `:639`, `:1607`). Current source has moved substantially:

| Component | Current repository evidence |
| --- | --- |
| .NET SDK | 10.0.400, `global.json:3` |
| Aspire SDK | 13.5.3, `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1` |
| Dapr Aspire toolkit | 13.5.0-preview.1.260825-0345, central pins `:136` |
| Dapr .NET client/ASP.NET | 1.18.5, central pins `:139`–`:140` |
| MCP SDK | 2.2.0, central pins `:248`, `:250` |
| EventStore / Tenants | 3.103.0 / 5.7.0, central pins `:8`, `:11` |
| Fluent UI | 5.0.0-rc.5-26219.1, central pins `:226` |
| NSwag / Octokit / System.CommandLine | 14.7.1 / 14.0.0 / 2.0.11, central pins `:260`, `:264`, `:302` |

Here “central pins” means `references/Hexalith.Builds/Props/Directory.Packages.props`, imported by the root package file. These are checked-in pins, not a claim of latest upstream versions or successful restore/build compatibility. The .NET 10 baseline, Octokit 14 decision, NSwag generator existence, and System.CommandLine 2.x selection remain consistent. No dependency upgrade is warranted merely by this review.

### TECH-07 — The module-host exception and technical ownership need reconciliation with the current required baseline

Classification: superseded baseline conflict, not a demand to delete brownfield projects. Disposition: **discuss** and record the chosen authority/migration boundary in an Update.

The architecture explicitly calls the local AppHost a sanctioned module-test exception (`architecture.md:404`) and defers elimination of local ServiceDefaults until post-MVP (`:1728`). The current required Hexalith baseline categorically says a domain module must not ship its own AppHost, Aspire, or ServiceDefaults (`references/Hexalith.AI.Tools/hexalith-llm-instructions.md:131`–`:134`). Current code retains all three, and now also owns an EventStore executable absent from the architecture's prescribed project/deployment inventory.

Evidence:

- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1` and `:11` demonstrate the AppHost/Aspire projects.
- `src/Hexalith.Folders.ServiceDefaults/Hexalith.Folders.ServiceDefaults.csproj:3` still marks the local helper packable; Server imports it at `src/Hexalith.Folders.Server/Program.cs:6` and invokes it at `:12`.
- `src/Hexalith.Folders.AppHost/Program.cs:19`–`:24` hosts `Projects.Hexalith_Folders_EventStore` under the `eventstore` app ID.
- `src/Hexalith.Folders.EventStore/Program.cs:20`–`:24` composes the platform server plus Folders idempotency adapters.

The review cannot silently convert historical exception language into an exemption from newer instructions, or infer approval to remove working projects. State which host is a test composition root, which executable is deployed, where Folders trusted intent adapters are composed, and which technical ownership changes remain required. A future builder should not duplicate the gateway or omit its adapter registration based on the old topology inventory.

### TECH-08 — D-3's Azure service option needs lifecycle qualification

Classification: external technology-lifecycle change. Disposition: **discuss/defer with a deployment-selection trigger**; do not automatically switch stores or provision a replacement.

D-3 lists Azure Cache for Redis as a production baseline without a retirement or migration qualifier (`architecture.md:543`). Microsoft's current FAQ says Basic/Standard/Premium retire September 30, 2028, while Enterprise/Enterprise Flash retire March 31, 2027, and recommends migration to Azure Managed Redis. It also identifies clustering-related compatibility considerations. See [Microsoft's Azure Cache for Redis retirement FAQ](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/retirement-faq), updated August 18, 2026, verified September 8.

Keep the Redis-compatible portability invariant, but require the concrete Azure SKU/lifecycle and Dapr state/pubsub compatibility to be revalidated at production binding. This does not invalidate self-hosted Redis or establish that Azure Managed Redis is already the approved replacement. D-3's capacity-trigger thresholds also remain described as deferred-until-C1 even though C1 is marked approved at `architecture.md:254`; its revisit condition has fired and needs disposition.

## Decisions with supporting reality checks

- **D-1 / durable-state posture:** an EventStore host exists, but accepted domain operations still produce empty-event results and the repository/Production boot boundary is unfinished. The architecture correctly retains a NOT READY posture; only specific stale mechanism claims should change.
- **D-2/D-4 / I-1:** Redis Dapr state/pubsub component composition is present in `src/Hexalith.Folders.AppHost/Program.cs:23`–`:31`; `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml:19` declares `state.redis`. This does not prove the stated Redis 7.x runtime image pin or live durability.
- **S-2/S-3:** JWT issuer/audience/lifetime/signing-key validation, 30-second clock skew, and ten-minute/one-minute metadata refresh intervals are implemented in `src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs:84`–`:110`. No live OIDC-provider compatibility claim is made.
- **A-1/A-2:** Contract-owned NSwag generation and a separate deterministic helper generator exist at `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:25`–`:56`. The prose's attribution of every helper to NSwag templates is imprecise: lines 40–46 invoke a dedicated .NET generation project.
- **A-4/A-5/A-6:** named CLI/MCP/GitHub libraries exist in central pins; MCP's explicit architecture version needs updating as above.
- **A-7/C12:** a dated provider manifest with source URLs and checksums exists at `tests/contracts/forgejo/supported-versions.json:1`; it currently selects 16.0.3 and 15.0.7 at lines 8 and 23. The v15/v14/v13 example tree is seed drift, not proof those old versions remain supported. Manifest provenance is supporting evidence, not a live provider compatibility test.
- **A-9/D-7:** the architecture references a concrete OQ8 design, and a dedicated Folders EventStore composition registers trusted intent adapters (`src/Hexalith.Folders.EventStore/Program.cs:22`). `docs/exit-criteria/oq8-idempotency-design.md:78` explicitly requires production-component, multi-host, restart evidence; `:82` retains Story 12.1 as the durable Folders prerequisite. Configuration evidence does not close OQ8.
- **F-1/F-2/F-3:** a separate ASP.NET Core UI host references both the generated Client and FrontComposer Shell (`src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj:1`, `:18`–`:20`). The library choice alone cannot establish WCAG conformance or runtime SignalR behavior.
- **I-2/I-4/I-6:** SDK container defaults, distinct image names, stable Dapr IDs, and OpenTelemetry dependencies are present (`Directory.Build.targets:11`–`:31`; `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj:9`–`:11`; production sidecar bindings; local ServiceDefaults package references `:10`–`:14`). Runtime health, recovery, provider quotas, and observability SLOs were not tested here.

Remaining cross-cutting rules and numerical performance/retention promises are design constraints, not claims proved by this technology review. Existing declarations and test files must not be promoted to passing operational evidence. The appropriate next artifact change is a targeted architecture Update retaining stable decision IDs, correcting concrete factual drift, and explicitly retaining the unresolved production and live-security evidence gates.
