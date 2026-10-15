# Container Images And Dapr App IDs

Hexalith.Folders publishes one .NET SDK container image per deployable service. Image repository names are deployment artifacts; Dapr app IDs are policy identities. Do not derive one from the other.

## Service Contracts

| Service | Project | Image repository | Dapr app ID | Production Dapr config |
| --- | --- | --- | --- | --- |
| Server | `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj` | `hexalith-folders-server` | `folders` | `hexalith-folders-production-accesscontrol-folders` |
| Workers | `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj` | `hexalith-folders-workers` | `folders-workers` | `hexalith-folders-production-accesscontrol-folders-workers` |
| UI | `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj` | `hexalith-folders-ui` | `folders-ui` | `hexalith-folders-production-accesscontrol-folders-ui` |

The server image repository intentionally differs from the Dapr app ID. `hexalith-folders-server` is the container image name used by deployment tooling, while `folders` remains the stable service identity used by Dapr access control.

## Container Metadata

Container metadata is centralized in `Directory.Build.targets` for projects that set `EnableContainer=true`. Service projects declare their stable `ContainerRepository` and `HexalithContainerServiceName`.

Expected non-secret labels:

- `org.opencontainers.image.source`
- `org.opencontainers.image.revision`
- `org.opencontainers.image.version`
- `org.opencontainers.image.title`
- `org.opencontainers.image.vendor`
- `org.opencontainers.image.licenses`
- `io.hexalith.project`
- `io.hexalith.service`

The revision label uses `SourceRevisionId`. CI should pass the commit value during publish. Local validation falls back to `local` or `NO_VCS` evidence.

## Local Archive Validation

The focused gate publishes Linux x64 SDK-container archives and writes a metadata-only report:

```powershell
tests/tools/run-container-image-gates.ps1
```

The script writes `_bmad-output/gates/container-images/latest.json` and archive files under `_bmad-output/gates/container-images/archives/`. It does not require a live registry push.

Equivalent direct publish commands:

```bash
dotnet publish src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj -c Release --os linux --arch x64 /t:PublishContainer -p:ContainerArchiveOutputPath=_bmad-output/gates/container-images/archives/hexalith-folders-server.tar.gz -p:ContainerImageTag=local-validation
dotnet publish src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj -c Release --os linux --arch x64 /t:PublishContainer -p:ContainerArchiveOutputPath=_bmad-output/gates/container-images/archives/hexalith-folders-workers.tar.gz -p:ContainerImageTag=local-validation
dotnet publish src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj -c Release --os linux --arch x64 /t:PublishContainer -p:ContainerArchiveOutputPath=_bmad-output/gates/container-images/archives/hexalith-folders-ui.tar.gz -p:ContainerImageTag=local-validation
```

## Promotion Flow

1. Validate service projects with `dotnet restore`, `dotnet build`, and the focused conformance tests.
2. Produce local SDK-container archives with the focused gate.
3. CI or release deployment tooling assigns registry ownership, release tags, and immutable digests outside the sanitized repository artifacts.
4. Staging and production manifests keep the same Dapr app IDs and production config names while selecting environment-owned image references.

## Required Production Environment Overrides

The EventStore actor host (`eventstore`) hosts the folder aggregate actors and publishes their domain events. By the D6 per-tenant convention it would publish folder events to `{tenantId}.folders.events`, but the `folders-workers` semantic-indexing subscriber listens on the single, tenant-agnostic `folders.events` topic. The production `hexalith-eventstore` deployment **must** therefore set this functional environment override (it is recorded on the `hexalith-eventstore` deployment fragment in `deploy/dapr/production/sidecar-config-bindings.yaml` and pinned by `ContainerImageConformanceTests`):

```text
EventStore__Publisher__TopicOverrides__folders = folders.events
```

Without it, folder events publish to a per-tenant topic the worker never subscribes, so semantic indexing silently never fires. The local AppHost wires the same override through `FoldersAspireModule.WithFoldersDomainEventTopicOverride`, keeping dev and production in lockstep. Cross-tenant isolation is preserved in the worker by the bridge projection's per-tenant keys, not by per-topic separation.

## Production Evidence

`deploy/containers/production/service-images.yaml` binds each service deployment to its stable image repository and Dapr app ID. `deploy/dapr/production/sidecar-config-bindings.yaml` proves sidecar injection with exact `dapr.io/app-id` and `dapr.io/config` annotations, and records the `eventstore` folder-events publish-topic override (`EventStore__Publisher__TopicOverrides__folders`).

The same sanitized sidecar bindings require one Kubernetes app-channel secret per callback host. The `folders` deployment uses `hexalith-folders-server-app-token`; `folders-workers` uses `hexalith-folders-workers-app-token`; `tenants` uses `hexalith-tenants-app-token`. Each sidecar reads its secret through `dapr.io/app-token-secret`, and the matching application container reads the `token` key as `APP_API_TOKEN`. Provision all three secrets through the deployment secret manager before rollout; the manifest contains references only.

EventStore and Folders Server also read the same `signing-key` value from the `hexalith-human-authority-receipt-signing-key` secret as `Authentication__HumanAuthorityReceipt__SigningKey`. Provision this shared secret through the deployment secret manager; keep its material outside source. It must be Base64 encoding of at least 32 random decoded bytes. Rotate it with EventStore and Folders Server quiesced together: allow in-flight receipts to drain, install the same replacement in both hosts, then resume traffic after both reload. The codec supports one signing key, so a rolling mixed-key window would reject mutations.

The EventStore fragment also requires these reference-only bindings:

| Configuration | Production source |
| --- | --- |
| `Authentication__WorkloadIssuer__ClientId`, `Authentication__WorkloadIssuer__ClientSecret` | `hexalith-eventstore-workload-client`: `client-id`, `client-secret`; a confidential service-account client authorized for the Folders/Tenants audiences and required operations |
| `EventStore__IdempotencyAdmission__Enabled`, `DigestKeySource` | `true`, `DaprSecret`; configuration-backed key material is refused in Production |
| `EventStore__IdempotencyAdmission__ActiveDigestKeyVersion` | `hexalith-eventstore-idempotency-keys`: `active-version` |
| `EventStore__IdempotencyAdmission__DigestKeySecretStoreName`, `DigestKeySecretName` | `kubernetes`, `hexalith-eventstore-idempotency-keys`; only EventStore's sidecar can read this allow-listed key ring |
| `EventStore__IdempotencyAdmission__DigestKeySecretGeneration` | The same key-ring secret's `generation` value |

Provision the key-ring secret with `generation`, `active-version`, and one Base64 key value of at least 32 decoded bytes under each configured active or reader version. The Kubernetes workload must have permission to read that exact secret. During rotation, also configure `EventStore__IdempotencyAdmission__ReaderDigestKeyVersions__0` (and subsequent indexes) for retained versions. Keep old key values while live records, tombstones, aliases, migration inventory or legal-hold references still use them; follow the platform's additive key rotation protocol in `references/Hexalith.EventStore/docs/guides/configuration-reference.md`. Never print secret maps or key material in rollout evidence.

The externally supplied Production `statestore` component must use the same physical backend for Workers and Server and set its `keyPrefix` metadata to `none` (`keyPrefix: none` behavior), matching `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml`. These hosts use different Dapr app IDs but share the same SDK read-model keys. Default app-ID prefixing would put a Workers write and Server read in different backend keys and leave authorization unavailable. Configure and validate this requirement before rollout; this repository pins the configuration contract and local reference, not a deployed backend result.

Workers uses `Folders__Workers__PubSubName=folders-workers-pubsub` for inbound subscriptions. Deploy `pubsub.workers.yaml` with the same broker as `pubsub.yaml`, plus `resiliency.yaml`, before restarting the Workers sidecar. The Workers-only Redis component sets `concurrency: 1` per component instance, retries failures ten times, and publishes exhausted deliveries to the callbacks' declared dead-letter topics. This limits simultaneous callbacks in one instance; it does not guarantee global ordering across multiple replicas or after restart and reclaimed deliveries. A sequence gap fails closed and requires retry or the existing ordered repair path. Monitor and replay those dead letters through the [dead-letter monitoring and recovery procedure](folder-permission-projection-recovery.md#dead-letter-monitoring-and-recovery). Outside Development, Workers refuses startup if `Folders__Workers__PubSubName` is missing, blank, or names the shared `pubsub` component. Workers continues publishing Memories events through the shared `pubsub` component. This deliberately trades per-instance throughput for reduced delivery concurrency. No live broker restart or global ordering proof is claimed.

Do not add registry credentials, pull secrets, tenant data, provider payloads, endpoint inventories, file contents, or generated diffs to image labels, reports, manifests, logs, or docs examples.
