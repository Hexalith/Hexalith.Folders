# Hexalith.Folders.AppHost.Tests

Tier-3 Aspire integration harness. It boots the full Folders `AppHost` topology through
`DistributedApplicationTestingBuilder` and proves the cross-process publish/subscribe wiring comes up together:

- `eventstore` publishes managed-tenant folder domain events to the single `folders.events` topic (Story 10.3 D1
  `EventStore:Publisher:TopicOverrides:folders` override).
- `folders-workers` subscribes `folders.events` on `/folders/events` (semantic indexing) and `memories-events`.
- `memories` subscribes `memories-events` (the curated search index).

Story 10.6 replaced the fail-closed content materializer with
`MetadataDerivedSemanticIndexingContentMaterializer`. Curated index text is the approved metadata-token
vocabulary (type class, fileVersionId, organizationId, folderId, size class).

## Running the harness

These tests require a **DCP-capable host**: Docker running plus a Dapr runtime (`dapr init`). They are **opt-in**
and skip everywhere else so the hermetic full-solution lane stays green without infrastructure:

```bash
HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet test tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj
```

Without the variable set, `AspireFoldersAppHostFixture` reports unavailable and diagnostic tests call
`SkipIfUnavailable()`. The wider environment currently has a known Aspire CLI/DCP boot mismatch (Epic 9 residual);
Story 11.15 owns the dedicated DCP-capable verification lane.

## Diagnostic tests versus Story 10.8 / FR58 acceptance

`EventStoreSidecarShouldPublishFolderEnvelopeToWorkerSubscriberTopic` and
`SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` are **component diagnostics**. The first hand-publishes a
`WorkspaceFileMutationAccepted` envelope. The second seeds `SearchIndexEntryChanged` and queries Memories
directly. Neither is FR58 completion evidence.

Story 10.8 acceptance is `Fr58PublicMutationSearchRoundTripTests`: an authenticated public
`AddWorkspaceFile` / `ChangeWorkspaceFile` call, then polling **only** public Folders search and indexing-status.
It must not seed the index, hand-publish domain envelopes, fake ACL/gateway, or query Memories for acceptance.
A governed run (`HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true`) that skips this scenario is a failure.

Live DCP/OQ5 stays incomplete while Task 0 prerequisites are missing: Stories 12.1–12.3 and 12.5, DW-292
effective-permissions population, a production EventStore authorization validator (DenyAll is not a pass),
Story 12.6 admission ≠ event emission (`PayloadNoOpDomainResult`), and Story 11.15. Facade and CLI work may
land independently.

## Story 10.4 AC9 — seeded index lifecycle diagnostic

`SeedRemoveAndArchiveRoundTripAgainstFoldersIndex` proves the live publish → route → index → search → remove path
against the real `folders-index` by **seeding** `SearchIndexEntryChanged` through the worker pub/sub component:

1. a syntactic `GET /api/search?tenantId=folders-index&axis=syntactic&query=…` returns exactly one hit whose
   `ScoredResult.SourceUri` echoes the published `cloudevent.id`;
2. after a `SearchIndexEntryRemoved`, the search returns zero hits (no stale entry);
3. after a `SearchIndexEntryChanged{folders.status=archived}`, the document remains and is filterable as archived
   (and no longer matches the active filter).

It runs **only** on a DCP-capable lane (it resolves the `folders-workers` Dapr sidecar to publish, and skips cleanly
if that endpoint is unavailable).

## Extending

`AspireFoldersAppHostFixture` is reusable. Do not promote diagnostic seeding or direct Memories queries to
Story 10.8 acceptance.

## Story 12.1 durable repository acceptance

`FolderDurabilityAcceptanceTests` uses its own `FolderDurabilityAcceptanceFixture`. Keep Keycloak enabled.
This lane requires the pinned .NET SDK, the installed Aspire/DCP and Dapr CLIs, Docker, working local
development certificates, standalone Dapr placement/scheduler, and the `redis:7-alpine` image (or permission
for Docker to pull it). Linux inotify watch capacity must be available for Dapr's component-directory
watchers. Build the current source graph before running the assembly:

```bash
dotnet restore Hexalith.Folders.slnx -p:Configuration=Debug -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -m:1 -nr:false -v:q
dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -m:1 -nr:false -p:UseSharedCompilation=false -v:q
HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll \
  -class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests -showLiveOutput
```

Run the class explicitly: the separate FR58 scenario still has downstream prerequisites. An opted-in
durability run requires one passing scenario, zero failures and **zero skips**. Initialization failures,
authorization failures and startup/dependency timeouts fail the lane. Without opt-in it skips normally.

The fixture owns a random Redis container bound to a random loopback port, temporary Dapr component
copies, unique app IDs for every sidecar, a standalone actor namespace, and the AppHost it builds.
Dapr mDNS discovery uses app IDs independently of the actor namespace. The fixture rewrites copied
component scopes, ACL identities and resiliency app targets; it maps gateway domain registrations and
receiving JWT audiences to the generated IDs. Matching real workload audience scopes are registered
only in the disposable Keycloak realm. Other HTTP dependencies use model-discovered endpoints. It updates
component references and waits so every domain sidecar shares only that private store/broker. It snapshots
and restores process environment settings, serializes its collection, and removes only its own topology,
Redis container/anonymous volume and temporary files. It never flushes localhost:6379 or stops another
AppHost. Restart/failure controls target only resources from that model. The store remains alive during
host restarts and is deleted after the scenario, including failed runs.

Host replacement uses Aspire's canonical `stop` and `start` resource commands, waits for exit and
health separately, and requires each process ID to change. The disposable project resources set
`OTEL_EXPORTER_OTLP_TIMEOUT`, `OTEL_EXPORTER_OTLP_TRACES_TIMEOUT`,
`OTEL_EXPORTER_OTLP_METRICS_TIMEOUT` and `OTEL_EXPORTER_OTLP_LOGS_TIMEOUT` to `1000` milliseconds.
An earlier run's shutdown stack showed OTLP retry/export disposal outlasting DCP's process-stop
deadline when the optional telemetry receiver was unavailable. These fixture settings bound that
dependency; production exporter defaults and the durable store remain unchanged. A failed stop
command fails acceptance and cannot count as a completed restart.

The driver submits commands at least one second apart and polls status/derived reads at 500 ms, so
its internal actor fan-out stays under the existing consumer limiter. The limiter is unchanged.
Each ACL grant/revocation is a separate single-operation authenticated command.

Bootstrap authenticates against real Keycloak, invokes `BootstrapGlobalAdmin`, `CreateTenant` and
`AddUserToTenant`, and updates the disposable identity's tenant claims through Keycloak administration.
It registers the existing `tenants` and `permissions` mapper inputs as administrator-only managed profile
attributes, preserving the rest of the profile, and checks the resulting real JWT claim bindings. This uses
the [Keycloak user-profile Admin REST API](https://www.keycloak.org/docs-api/26.6.4/rest-api/).
The signed EventStore workload caller and the sidecar caller both use the generated EventStore app ID;
receiver allow-lists and `Authentication:HumanAuthorityReceipt:ExpectedWorkload` bind that exact ID.
The production receipt setting defaults to `eventstore`; blank/whitespace identities fail closed.
Receipt signatures, human bindings and generated authenticated app-channel tokens remain enforced.
Claims alone do not supply folder authority: accepted organization baseline/grant commands must populate
the durable authorization rows through authenticated Workers. Public v1 Folders creates enter EventStore
with its human-receipt checks. The public host relays the validated human bearer through its SDK gateway
client to `EventStore:Gateway:BaseAddress`, defaulting to Aspire discovery at `http://eventstore`.
Only catalogued public v1 mutation preflight uses human tenant/subject/action claims; `/process` still
requires the gateway-issued signed receipt. The test uses the existing deterministic v1 create actor identity to inspect
the matching retained stream; the public accepted response contains no folder identifier.

Assertions inspect SDK read models and authenticated retained stream bytes, compare hashes across
equivalent retries, conflicts and host restarts, and project complete prefixes from sequence zero using
a real operation-scoped workload token. Unknown events and invalid payload versions are submitted only
as replay inputs; retained events are never modified. Corruption injection changes only a task-owned
derived permission row through the SDK with ETags and restores it in `finally`. Dependency failure pauses
only the fixture's Redis and always resumes it. No authority is seeded or hand-published.

Workers allow one second for a concurrently delivered predecessor or conditional-write conflict to
complete, then reread and validate the row. Unsupported events, corrupt identities and replay conflicts
remain failures; an unresolved gap never advances the projection. This handles callback concurrency
without performing dead-letter or operator recovery.

The scenario prints compact `PASS` milestones and retained SHA-256 values. Retain sanitized outcomes,
expanded commands, source revision and EventStore gitlink/HEAD in
`_bmad-output/implementation-artifacts/evidence/story-12-1-durability-2026-10-17.json`. Never copy tokens,
passwords, environment dumps, dashboard login URLs or protected response/event bodies into evidence.
An unobserved milestone stays open. Passing source tests or topology health alone cannot close delivery
acceptance. Supported-replica serialization and EventStore actor fault-injection checks remain the
accepted bounded conflict proof; this lane does not claim a live backend ETag race, broker repair or
downstream Stories 12.2–12.7 acceptance.

The 2026-10-17 full matrix remains open. Earlier isolated candidates observed real tenant and organization
authority, persisted folder creation, a positive ACL read and equivalent/conflicting intent. The final
candidate failed initialization with zero skips because shared-host inotify watches were exhausted;
its six Dapr sidecars could not create their directory watchers. Complete host replacement, replay and
the remaining negative matrix have no passing live result. See the tracked evidence for exact commands,
separate candidate outcomes and the concrete host prerequisite. Do not stop unrelated processes or
change host limits as part of this lane.
