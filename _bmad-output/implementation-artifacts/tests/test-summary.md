# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs` - Added Story 4.3 lock endpoint coverage for client-controlled tenant headers, invalid route identifiers, unknown-field rejection with redaction, and reserved-system safe denial before body parsing.
- [x] Existing Story 4.3 endpoint coverage in `RepositoryBackedFolderEndpointTests.cs` covers accepted request shape, route-authoritative workspace identity, required `Idempotency-Key`, required `X-Correlation-Id`, required `X-Hexalith-Task-Id`, malformed JSON, unsupported schema version, unsupported lock intent, invalid lease duration, lock conflict, workspace locked, and transition-invalid problem mapping.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceLockAcquisitionServiceTests.cs` - Added service-level coverage for repository idempotency lookup match and conflict paths before append.
- [x] Existing Story 4.3 aggregate/service tests cover accepted metadata-only lock acquisition, equivalent replay, idempotency conflict, malformed commands, wrong workspace ID, missing/unprepared/non-ready workspaces, archived folders, lock contention after append race, authorization-first safe denial, idempotency lookup unavailable, and metadata-only lock event fields.

### E2E Tests
- [x] No browser/UI E2E tests were added because Story 4.3 exposes an API/domain lock-acquisition workflow and the project context keeps the Playwright UI lane deferred until Epic 6 supplies stable read-only console routes and selectors.
- [x] The generated API tests use ASP.NET Core TestServer and in-process xUnit execution as the hermetic E2E-style boundary for this story.

## Coverage

- Story 4.3 API endpoint covered: `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` (1/1 in-scope endpoint).
- New API scenarios added in this QA pass: 5 focused endpoint cases.
- New service scenarios added in this QA pass: 2 focused idempotency lookup cases.
- Story 4.3 focused validation coverage: aggregate namespace, lock acquisition service, REST endpoint class, action-token mapper, endpoint registration, and `WorkspaceLockContractGroupTests`.
- UI features covered: not applicable for this story (0/0 in-scope UI workflows).
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory state, and recording fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.3.
- [x] Tests use standard project APIs and semantic HTTP assertions.
- [x] Tests cover happy path and critical error cases.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -namespace Hexalith.Folders.Tests.Aggregates.Folder
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.RepositoryBackedFolderEndpointTests
tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -method Hexalith.Folders.Server.Tests.ServerEndpointRegistrationTests.MapFoldersServerEndpointsShouldRegisterDomainServiceAndTenantSubscriptionRoutes
git diff --check
```

## Validation Result

- Builds: focused core, server, and contract test projects compiled successfully.
- Aggregate namespace tests: passed, 421 total, 0 failed.
- Server endpoint tests: passed, 67 total, 0 failed.
- Workspace lock contract group tests: passed, 6 total, 0 failed.
- Action-token mapper tests: passed, 6 total, 0 failed.
- Endpoint registration focused method: passed, 1 total, 0 failed.
- `git diff --check`: passed.
