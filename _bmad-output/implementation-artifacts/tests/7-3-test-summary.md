# Test Automation Summary

**Workflow:** `bmad-qa-generate-e2e-tests`
**Story:** `_bmad-output/implementation-artifacts/7-3-build-container-images-with-stable-dapr-app-ids.md`
**Feature under test:** SDK container image metadata, stable Dapr app IDs, production service-image binding
evidence, local Aspire sidecar topology, and the focused container image gate.

This is a release-readiness packaging and static conformance story. There are no browser flows to automate; the
test surface is xUnit v3 + Shouldly static conformance, the Dapr policy PowerShell gate, and the SDK container
archive publish gate.

## Generated Tests

### API Tests

- [x] Not applicable - Story 7.3 publishes deployment/container evidence and CI gate behavior, not runtime API endpoints.

### E2E / Conformance Tests

- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ContainerImageConformanceTests.cs` - **5 facts**.
- [x] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` - **8 facts**.
- [x] `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs` - **4 facts**.

Coverage includes:

- [x] Server, Workers, and UI projects expose stable SDK container repositories and non-secret OCI metadata.
- [x] Production service-image evidence binds image repositories to exact Dapr app IDs and config names.
- [x] Local Aspire topology attaches the same Dapr app IDs used by production evidence, including `folders-ui`.
- [x] Container image gate script uses SDK `/t:PublishContainer`, archive output, audit-disabled restore for sandbox stability, and no live registry push.
- [x] Container gate report is metadata-only and records `status: passed` with service-level repository/tag/label evidence.
- [x] Secret-shaped and recursive-submodule negative scans are clean on Story 7.3 deployment/container artifacts.

## Validation

| Check | Command | Result |
| ----- | ------- | ------ |
| Restore | `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false` | passed |
| Build | `dotnet build Hexalith.Folders.slnx --no-restore -m:1` | passed, 0 warnings / 0 errors |
| Container/deployment tests | `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.ContainerImageConformanceTests` | 5/5 passed |
| Dapr policy tests | `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance` | 8/8 passed |
| Aspire topology tests | `dotnet tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll -noLogo -noColor -class Hexalith.Folders.IntegrationTests.AspireTopologyTests` | 4/4 passed |
| Dapr policy gate | `pwsh -NoLogo -NoProfile -File tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` | passed, 8/8 |
| Container image gate | `pwsh -NoLogo -NoProfile -File tests/tools/run-container-image-gates.ps1 -SkipRestoreBuild` | passed; server, workers, and UI archives generated locally |
| Format/analyzers | `dotnet format whitespace ...` and `dotnet format analyzers ...` on modified test files | passed |
| Diff hygiene | `git diff --check` | clean |
| Submodule policy scan | `rg -n "git submodule update --init --recursive|--recursive"` over Story 7.3 artifacts | clean |
| Secret-shaped scan | `rg -n "ghp_|github_pat_|client_secret|private_key|BEGIN .*PRIVATE KEY|password=|token=|https://...prod"` over Story 7.3 artifacts | clean |

## Files Changed

- `Directory.Build.targets`
- `deploy/containers/production/service-images.yaml`
- `docs/operations/container-images-and-dapr-app-ids.md`
- `src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`
- `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj`
- `src/Hexalith.Folders.UI/Hexalith.Folders.UI.csproj`
- `src/Hexalith.Folders.Workers/Hexalith.Folders.Workers.csproj`
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ContainerImageConformanceTests.cs`
- `tests/Hexalith.Folders.IntegrationTests/AspireTopologyTests.cs`
- `tests/tools/run-container-image-gates.ps1`
- `_bmad-output/gates/container-images/latest.json`

## Next Steps

- Run code review. No QA-only blocker remains.
