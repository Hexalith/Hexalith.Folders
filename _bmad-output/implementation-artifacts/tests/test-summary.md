# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs` - Added Story 4.6 stream transport coverage for minimum-boundary streamed change, invalid stream length/media type, missing stream descriptor, and body `workspaceId` rejection without unsafe echo.
- [x] Existing Story 4.6 endpoint coverage covers zero-byte inline content, inline 262144-byte boundary, over-bound inline 413 with `X-Hexalith-Retry-Transport: stream`, invalid base64/media type/length, missing inline content, route-operation mismatch, required headers, safe Problem Details, and gateway payload redaction.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs` - Added/strengthened service assertions proving path evidence denial, aggregate precheck rejection, equivalent replay, idempotency conflict, and content-store failure do not duplicate content staging or append work.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationAggregateTests.cs` - Added/strengthened aggregate assertions for metadata-only accepted events and invalid transport evidence rejection.

### E2E Tests
- [x] No browser UI E2E tests were added because Story 4.6 exposes API/runtime mutation intake only; the project context keeps Playwright UI E2E deferred until Epic 6 supplies stable read-only console routes and selectors.
- [x] The generated API tests use ASP.NET Core TestServer and in-process xUnit execution as the hermetic E2E-style boundary for this story.

## Coverage

- API endpoints covered: `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/add` and `PUT /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/change` for Story 4.6 add/change content transport, with existing remove regression retained.
- New API scenarios added in this QA pass: 4 focused endpoint tests/theories covering streamed change acceptance, invalid streamed transport, missing stream descriptor, and body workspace mismatch.
- New domain/service scenarios added in this QA pass: 1 aggregate invalid-transport-evidence theory and 1 service idempotency-conflict/no-content-staging test, plus strengthened no-content-staging assertions on existing denial/replay tests.
- Focused validation coverage: mutation endpoint class, action-token mapper, file-mutation aggregate, file-mutation service tests, and OpenAPI file-context contract group.
- UI features covered: not applicable for this story (0/0 in-scope UI workflows).
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory state, and recording fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.6.
- [x] Tests use standard project APIs and semantic HTTP/query assertions.
- [x] Tests cover happy path and critical error cases.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests
tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests
git diff --check
```

## Validation Result

- Core project build: passed, 0 warnings, 0 errors.
- Core test project build: passed, 0 warnings, 0 errors.
- Server test project build: passed, 0 warnings, 0 errors.
- Contracts test project build: passed, 0 warnings, 0 errors.
- File-mutation aggregate and service tests: passed, 31 total, 0 failed.
- Workspace lock/file mutation endpoint tests: passed, 54 total, 0 failed.
- Folder command action-token mapper tests: passed, 8 total, 0 failed.
- File context contract group tests: passed, 5 total, 0 failed.
- `git diff --check`: passed.
- `dotnet test` VSTest host for the server lane aborted before running tests because the sandbox denied the local VSTest socket. The project xUnit executable runner was used for focused execution instead.
