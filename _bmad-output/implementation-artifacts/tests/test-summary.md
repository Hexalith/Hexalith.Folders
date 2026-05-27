# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Folders.Server.Tests/FileContextEndpointTests.cs` - Added Story 4.8 metadata endpoint coverage proving metadata-only response shape and route-authoritative source request forwarding.
- [x] `tests/Hexalith.Folders.Server.Tests/FileContextEndpointTests.cs` - Added Story 4.8 search and glob endpoint coverage proving body `limit`, `cursor`, query text/pattern, and requested paths are forwarded through TestServer without returning file bytes.
- [x] `tests/Hexalith.Folders.Server.Tests/FileContextEndpointTests.cs` - Added source-denial API coverage for timeout, response-limit, redacted, binary-disallowed, large-file-disallowed, and unavailable outcomes with safe Problem Details and no requested path/content echo.
- [x] `tests/Hexalith.Folders.Server.Tests/FileContextEndpointTests.cs` - Added review guardrails for metadata/search/glob request-shape rejection and canonical `range_unsatisfiable` Problem Details.

### E2E Tests

- [x] No browser UI E2E tests were added because Story 4.8 is an API/query boundary with no in-scope UI workflow; project context keeps Playwright UI E2E deferred until Epic 6 supplies stable read-only console routes and selectors.
- [x] The generated API tests use ASP.NET Core TestServer and xUnit v3 as the hermetic E2E-style boundary for this story.

### Query-Service Tests

- [x] `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextQueryHandlerTests.cs` - Added safe source-status mapping coverage for unavailable, stale, timeout, input-limit, response-limit, redacted, binary-disallowed, large-file-disallowed, and unsatisfiable-range outcomes.
- [x] `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextQueryHandlerTests.cs` - Added response-budget denial coverage proving over-limit search/glob results return metadata-only `response_limit_exceeded`.
- [x] `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextQueryHandlerTests.cs` - Added review guardrails proving metadata requires paths, search/glob require the contract `limit`, and range-read source results cannot exceed the requested byte window.

## Coverage

- API endpoints covered: 5/5 context routes (`ListFolderFiles`, `GetFolderFileMetadata`, `SearchFolderFiles`, `GlobFolderFiles`, `ReadFileRange`).
- API critical error cases covered: idempotency-key rejection, authentication-safe denial, source timeout, source response limit, source redaction, binary/large-file disallowance, source unavailable, and leakage-corpus non-echo.
- Query-service operations covered: 5/5 kinds (tree, metadata, search, glob, range).
- Query-service denial categories covered: tenant denial, path validation, sensitivity redaction, input limit, response limit, timeout, unavailable read model/source, stale source, binary/large-file policy, and range unsatisfiable/reversed ranges.
- UI features covered: not applicable for this story (0/0 in-scope UI workflows).
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory state, and recording fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.8.
- [x] Tests use standard project APIs and semantic HTTP/domain assertions.
- [x] Tests cover happy path and critical error cases.
- [x] Tests use semantic HTTP route/body assertions and no CSS/text/sleep-based UI locators.
- [x] Tests have clear descriptions.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.FileContext.WorkspaceFileContextQueryHandlerTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.FileContextEndpointTests
dotnet build src/Hexalith.Folders/Hexalith.Folders.csproj --no-restore --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests
git diff --check
```

## Validation Result

- Core project build: passed, 0 warnings, 0 errors.
- Core test project build: passed, 0 warnings, 0 errors.
- Server test project build: passed, 0 warnings, 0 errors.
- Contracts test project build: passed, 0 warnings, 0 errors.
- File context query handler tests: passed, 25 total, 0 failed.
- File context endpoint tests: passed, 19 total, 0 failed.
- File context contract group tests: passed, 5 total, 0 failed.
- `git diff --check`: passed.
