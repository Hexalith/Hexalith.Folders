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

## Production Evidence

`deploy/containers/production/service-images.yaml` binds each service deployment to its stable image repository and Dapr app ID. `deploy/dapr/production/sidecar-config-bindings.yaml` proves sidecar injection with exact `dapr.io/app-id` and `dapr.io/config` annotations.

Do not add registry credentials, pull secrets, tenant data, provider payloads, endpoint inventories, file contents, or generated diffs to image labels, reports, manifests, logs, or docs examples.
