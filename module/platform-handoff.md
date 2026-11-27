# Domain enrollment Platform registration handoff — pending

Static declaration/adoption validity does not register executables. Complete this
record with the accountable module and Platform owners before ordinary CLI
run/debug. The ordinary runner exposes no caller `Projects` override; an internal
test override provides composition-only evidence. Keep real-owner, published
package and live acceptance pending until observed.

| Accountability and identity | Owner-recorded value |
| --- | --- |
| Status / outstanding registration work | **pending** — `PlatformServerBindings.ReferenceProjects` and `ReferenceImages` contain no `folders/domain` or `folders/workers` entries; Platform Epics 2-6 and Story `10-1-enroll-folders-into-staging` remain open; static adoption checks still report `tool-pin` (`6.9.0` vs Platform `6.6.2`), `dependency-selectors`, and `dapr-subscriptions` findings; smoke suite and recovery/fence hook executables remain pending. |
| Module owner / repository / reviewed revision | `@jpiquot` / `Hexalith.Folders` / `aef674d3` (8 CI/tool-pin commits past `v2.2.1` `bf60ba51`) |
| Platform owner / registration review reference | `@jpiquot` / pending (Platform Story `10-1-enroll-folders-into-staging`) |
| Direct Platform revision containing the implementation / tag / dirty state | pending — current direct Platform gitlink is `3848af49f9afc91b174b4c716751fe49cb15dbba` (`v2.0.0-eventstore.1-382-g3848af49`, clean), which does not yet register `folders/domain` or `folders/workers`. |
| Builds catalog gitlink / accepted tool pin | Builds gitlink `bf8a28b4bbcec619e8d9764d335f786f2bafb69e` (`v6.8.1`, pins `HexalithFoldersVersion=2.0.1` and omits `Hexalith.Folders.EventStoreExtensions`) / Folders `.config/dotnet-tools.json` pins `hexalith.builds.module.cli` `6.9.0` (`rollForward: false`) vs direct Platform pin `6.6.2` (`rollForward: false`) — reconciliation pending. |
| Implementation diff / composition-only regression evidence | pending — local Folders `module/hexalith.module-manifest.v2.json` validates via `hexalith-module validate`; `FoldersEventStoreExtensionTests` and `WorkersHostSecurityEnrollmentTests` qualify the extension loader and Workers HTTP boundary locally, not Platform composition. |
| Registration acceptance by module owner / Platform owner / UTC date | pending |

Record one row per enabled server; add rows as needed. Paths are relative to the
mapped owner. Record pending package support explicitly when an image/release has
not been qualified.

| Module/server / appId / resourceId | Platform project binding / implementation reference | Image binding / catalog version / released declaration provenance | Accountable owner / review |
| --- | --- | --- | --- |
| `folders/domain` / `folders` / `folders` | Pending in `PlatformServerBindings.ReferenceProjects`: `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj` | Pending in `PlatformServerBindings.ReferenceImages`: `EnableContainer=true`, `<ContainerRepository>hexalith-folders-server</ContainerRepository>`, no retained content-addressed OCI image yet. Gateway extension `Hexalith.Folders.EventStoreExtensions` (`extensionId: folders`, `extensionApiMajor: 1`) packs `build/hexalith.module-manifest.v2.json` (released at `v2.2.1` `bf60ba51`, not yet pinned in Builds catalog `v6.8.1`). | `@jpiquot` / pending |
| `folders/workers` / `folders-workers` / `folders-workers` | Pending in `PlatformServerBindings.ReferenceProjects`: `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` | Pending in `PlatformServerBindings.ReferenceImages`: `EnableContainer=true`, `<ContainerRepository>hexalith-folders-workers</ContainerRepository>`, no retained content-addressed OCI image yet. | `@jpiquot` / pending |
| `folders/ui` (`folders-ui`, not yet declared in `hexalith.module-manifest.v2.json`) | Not enrolled in this v2 manifest: `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj` requires confidential-client OIDC credential wiring and FrontComposer/Platform UI enrollment before adding a UI server entry. | `EnableContainer=true`, `<ContainerRepository>hexalith-folders-ui</ContainerRepository>`, no retained OCI image yet. | `@jpiquot` / pending |

| Full configuration key / module/server scope | Local profile / structured binding / secret reference | Authentication/signing propagation / token adapter and implementation | Owner / evidence |
| --- | --- | --- | --- |
| `Folders:ApiRouting:Mode` (`folders/domain`) | Default `V1Only` (`FoldersApiRouting.ResolveMode`); invalid/empty mode fails startup. Production stays `V1Only` until Story 1.17 exposure gate passes. | Compatibility v1/v2 route control; Projects `FoldersV1ProjectReadDirectory` currently calls `/api/v1/folders/{folderId}/lifecycle-status` and `/effective-permissions` and must switch to v2 before `V2Only`. | `@jpiquot` / `FoldersApiRoutingModeTests` (local) |
| `APP_API_TOKEN` (`folders/domain`, `folders/workers`) | Declared secret `app-channel-token` (`configurationKey: APP_API_TOKEN`). | Sidecar receiver-channel token for `/dapr/subscribe`, `/tenants/events`, and `/folders/events`. Platform `PlatformLocalProfile` needs explicit token-adapter wiring for `folders/domain` and `folders/workers`. | `@jpiquot` / pending Platform adapter |
| `Authentication:WorkloadIssuer:ClientSecret`, `Authentication:WorkloadIssuer:ClientId`, `Authentication:WorkloadIssuer:Workload` (`folders/domain`, `folders/workers`) | Declared secret `workload-client-secret`; client ID and workload identity (`folders` / `folders-workers`) supplied by environment profile. | Outbound EventStore workload client-credentials assertion; requires matching Keycloak/OIDC confidential client enrollment. | `@jpiquot` / pending |
| `Authentication:HumanAuthorityReceipt:SigningKey` (`folders/domain`, shared with `eventstore/gateway`) | Environment secret (not committed). | Verified by `HumanAuthorityReceiptVerifier` on `/process` alongside EventStore workload assertion. | `@jpiquot` / pending |
| `Authentication:BoundStreamRead:PrivateKey` & `Authentication:NativeSourceScan:PrivateKey` (`folders/domain`, `folders/workers`) | Environment RSA private keys; corresponding public keys and allowed domain `folders` enrolled on `eventstore/gateway`. | Authoritative bound-stream metadata reads and native source scans for `folders` and `folders-workers`. | `@jpiquot` / pending |
| `Folders:Authentication:Authority`, `MetadataAddress`, `Audience`, `ValidIssuer`, `RequireHttpsMetadata` (with `Authentication:JwtBearer:*` fallback) (`folders/domain`) | Environment OIDC settings (`RequireHttpsMetadata=true` outside Development/Test). | Validated at startup by `FoldersOidcOptionsValidator` for human bearer requests. | `@jpiquot` / pending |
| `EventStore:CommandStatus:StateStoreName`, `EventStore:DataProtection:StateStoreName`, `EventStore:Projections:ReadModelStateStoreName` (`folders/domain`, `folders/workers`) | Declared Dapr `state` role (`authoritative-restore`). | Platform injects scoped state store name; `folders` and `folders-workers` share the same read-model state store (`keyPrefix=none`). | `@jpiquot` / pending |
| `EventStore:ProjectionChanges:PubSubName` (`folders/domain`), `Folders:Workers:PubSubName` (`folders/workers`), `Folders:Workers:MemoriesPubSubName` (`folders/workers`) | Declared Dapr `pubsub` roles `events` and `worker-inbound-events` (`rebuild-only`). | `Folders:Workers:PubSubName` binds isolated inbound worker broker (`folders-workers-pubsub` in conformance); `Folders:Workers:MemoriesPubSubName` binds outbound `memories-events` publication. | `@jpiquot` / `SemanticIndexingWorkerRegistrationTests` (local) |
| `EventStore:Publisher:TopicOverrides:folders` (`eventstore/gateway`) | Value `folders.events` (`deploy/dapr/production/sidecar-config-bindings.yaml`). | Redirects per-tenant `{tenantId}.folders.events` publishing to the single `folders.events` topic consumed by `folders-workers`. | `@jpiquot` / pending Platform gateway profile |
| `EventStore:Gateway:BaseAddress` (`folders/domain`), `Memories:BaseAddress` (`folders/domain`) | Defaults `http://eventstore` and Dapr sidecar invoke `{DAPR_HTTP_ENDPOINT}/v1.0/invoke/memories/method/`. | Outbound command submission and authorized Memories search facade (`GET /api/search`). | `@jpiquot` / pending |

| Tenant | Domain | Version | appId | Method | Gateway settings / preservation and filtering evidence / owner |
| --- | --- | --- | --- | --- | --- |
| `<environment-managed-tenant>` | `folders` | `v1` (gateway domain registration; REST compatibility mode separate) | `folders` | `/process`, `/query`, `/project`, `/project/v2`, `/admin/operational-index-metadata` | Pending in Platform `PlatformGatewayConfiguration` / EventStore gateway `appsettings.json` (`EventStore:DomainServices:Registrations`); must survive selection when `folders` is enabled and be filtered out when unselected. |

| Dapr and acceptance evidence | Reference / owner / status |
| --- | --- |
| Capabilities / injected scoped components | Declared in `module/hexalith.module-manifest.v2.json` (`state`, `events`, `worker-inbound-events`, `eventStoreExtensions: Hexalith.Folders.EventStoreExtensions`). `hexalith-module validate --adoption` passes `workspace`, `declaration`, `references`, `gitlinks`, `nested-references`, `hosting-inventory`, `eventstore-image`, `eventstore-extensions`; fails `tool-pin` (`6.9.0` vs `6.6.2`), `dependency-selectors` (legacy `Directory.Build.props`/`Directory.Packages.props` and tracked evidence copies), and `dapr-subscriptions` (programmatic `MapSubscribeHandler`/`WithTopic` and literal component names in source/tests) / `@jpiquot` / **pending** |
| Declarative subscriptions / delivery routes / dead-letter policy | `folders/domain` declares `tenant-events-domain` (`tenants.events` -> `/tenants/events`, `deadLetter: none`). `folders/workers` currently retains programmatic `WithTopic` subscriptions for `tenants.events` (`deadletter.tenants.events`) and `folders.events` (`deadletter.folders.events`) because v2 topic inventory cannot name dotted physical dead-letter topics / `@jpiquot` / **pending** |
| Deny-by-default receiver ACL / approved callers and verbs / mTLS | Authoritative production conformance input at `deploy/dapr/production/accesscontrol.yaml` and `deploy/dapr/production/daprsystem.yaml`: `eventstore` -> `folders` (`POST /process`, `/project`; v2 manifest adds `/query`, `/project/v2`, `/admin/operational-index-metadata`); `folders` -> `memories` (`GET /api/search`); `folders-workers` denies all inbound service invoke / `@jpiquot` / **pending Platform policy generation & live mTLS proof** |
| Static/model policy evidence, qualified as synthetic where applicable | Local synthetic evidence only: `DaprPolicyConformanceTests`, `FoldersEventStoreExtensionTests`, `WorkersHostSecurityEnrollmentTests`, `SemanticIndexingWorkerRegistrationTests` / `@jpiquot` / **synthetic only; Platform composition pending** |
| Ordinary public CLI run/debug/down command and diagnostic references | pending (blocked until `PlatformServerBindings` registers `folders/domain` and `folders/workers` and adoption gaps are resolved) |
| Real-owner live readiness / policy acceptance | pending |
| Human SM-2 checklist / breakpoint and source-edit evidence / Story 3.11 | `module/sm-2-checklist.md` seeded / **pending** |
| Package/released-image acceptance, if requested | pending — Builds catalog `v6.8.1` still pins Folders `2.0.1` (missing `Hexalith.Folders.EventStoreExtensions`), and no retained content-addressed OCI images exist for `hexalith-folders-server`, `hexalith-folders-workers`, or `hexalith-folders-ui`. |
| Recovery / fence hooks & smoke suite mapping | `lifecycle.recoveryInventory` declares the 5 rebuild-only Folders projections; `lifecycle.criticalFlows` declares `folder-lifecycle` on `folders-domain`. `smokeSurfaces`, `recoveryHooks`, and `fenceHooks` remain empty `[]` until Platform registers Folders' check suite (`10-1`) and Story 13.7 / `EXT-ES-RECOVERY` implements executable recovery/fence tasks. |
| Historical Dapr timeout disposition / new live evidence | unresolved / pending |

- [ ] Both named owners reviewed the direct Platform revision containing every required binding and adapter.
- [ ] The reviewed gateway tuple survives selection and disappears when its appId is unselected.
- [ ] Required implementation and static/model policy evidence are attached before ordinary CLI commands.
- [ ] Ordinary CLI qualification and real-owner live/SM-2 acceptance are recorded separately; unobserved evidence remains pending.

Result: **pending**. Synthetic composition evidence confers no published release or
real-owner/live acceptance.
