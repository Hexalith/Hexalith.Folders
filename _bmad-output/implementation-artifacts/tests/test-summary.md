# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs` - Existing repository-backed REST route tests were rerun and remain green for accepted submission, required headers, malformed JSON, unsupported schema, reserved tenant, idempotency conflict, and safe provider-unavailable mapping.
- [x] `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs` - Existing REST-to-process repository-backed request coverage was rerun and remains green for persisting the metadata-only request event and lifecycle binding projection.

### E2E Tests
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderRepositoryBackedCreationGateTests.cs` - Added offline workflow guardrails for readiness category mapping, in-progress repository-binding short-circuiting before readiness/idempotency observation, and equivalent replay before readiness observation.
- [x] `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs` - Added worker guardrails proving provider calls receive only safe provisioning context and unavailable folder binding state does not resolve providers or append outcomes.
- [x] `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs` - Expanded repository-creation failure mapping coverage across validation, auth, permission, hidden, conflict, rate limit, unavailable, malformed, timeout, and transport outcomes.
- [x] `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs` - Expanded repository-creation failure mapping coverage across validation, auth, permission, hidden, missing target, conflict, redirect, rate limit, unavailable, malformed, unsupported, reconciliation, timeout, cancellation, and transport outcomes.
- [x] No browser/UI E2E tests were added because Story 3.6 is an API/worker/provider workflow and project context keeps UI E2E deferred until stable console routes exist.

## Coverage

- Core repository-backed/provider focused suite: 116/116 passing.
- Repository provisioning worker focused suite: 7/7 passing.
- Repository-backed server endpoint focused suite: 10/10 passing.
- Repository-backed integration focused test: 1/1 passing.
- Full core test project: 679/679 passing.
- Full server test project: 91/91 passing.
- Full worker test project: 18/18 passing.
- Full integration test project: 12/12 passing.

## Validation

- [x] API tests generated/rerun where applicable.
- [x] E2E-style offline workflow tests generated for the repository-backed creation gate, provider adapter seams, and provisioning process manager.
- [x] Tests use xUnit v3, Shouldly, slim WebApplication endpoint tests, and existing fake provider/repository seams.
- [x] Tests cover happy path plus critical error cases: readiness failure categories, unsupported capability, unknown provider outcome, reconciliation required, rate limit, unavailable, permission/auth failures, repository conflicts, in-progress binding mutation, idempotent replay, provider request context, and state-unavailable worker paths.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and run offline without GitHub, Forgejo, provider credentials, live Tenants services, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics and validation commands.

## Commands Run

```text
dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --filter "FullyQualifiedName~FolderRepositoryBackedCreationGateTests|FullyQualifiedName~GitHubProviderTests|FullyQualifiedName~ForgejoProviderTests" --no-restore
dotnet test .\tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --filter FullyQualifiedName~RepositoryProvisioningProcessManagerTests --no-restore
dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --filter RepositoryBackedFolderEndpointTests --no-restore
dotnet test .\tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj --filter FullyQualifiedName~RepositoryBackedFolderRequestShouldRoundTripThroughProcessAndPersistRequestEvent --no-restore
dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore
dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore
dotnet test .\tests\Hexalith.Folders.Workers.Tests\Hexalith.Folders.Workers.Tests.csproj --no-restore
dotnet test .\tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj --no-restore
```
