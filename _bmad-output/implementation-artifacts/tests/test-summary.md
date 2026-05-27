# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Folders.Server.Tests/WorkspaceStatusEndpointTests.cs` - Tightened read-only evidence endpoint coverage so `Idempotency-Key` rejection proves both workspace-status and task-status read models are untouched.

### Query/Handler Tests

- [x] `tests/Hexalith.Folders.Tests/Queries/Folders/TaskStatusQueryHandlerTests.cs` - Added focused task-status query coverage for success evidence, unknown outcome and reconciliation no-blind-retry semantics, authentication/authorization short-circuiting, client-controlled tenant mismatch, malformed identifiers, read-model failure modes, and malformed snapshot fail-closed behavior.

### E2E Tests

- [x] Browser UI E2E tests were assessed and not added because Story 4.13 is a backend/API operational-evidence workflow and the repo explicitly defers UI E2E until Epic 6 story 6-2 provides stable read-only console routes and selectors.

## Coverage

- API evidence endpoints: 4/4 read-only evidence routes reject `Idempotency-Key` before read-model access.
- Task status query handler: 17 focused cases covering happy path, critical authorization denials, read-model unavailable/stale/malformed/not-found outcomes, unknown provider outcome, reconciliation required, and fail-closed malformed snapshots.
- Existing workspace status query handler regression set: 33 focused cases retained.
- Existing workspace/evidence server endpoint and canonical mapper regression set: 45 focused cases retained.
- UI features covered: not applicable for Story 4.13.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.13.
- [x] Tests use standard project APIs: xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory read models, and focused recording/counting fakes.
- [x] Tests cover happy path and critical error cases.
- [x] Tests use HTTP route/header/body assertions and query-handler result assertions; no CSS/text/sleep-based UI locators.
- [x] Tests have clear descriptions.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and require no live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_HOME=/tmp/dotnet-cli-home PATH=$HOME/.dotnet:$PATH dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --filter "FullyQualifiedName~TaskStatusQueryHandlerTests|FullyQualifiedName~WorkspaceStatusQueryHandlerTests"
DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_HOME=/tmp/dotnet-cli-home PATH=$HOME/.dotnet:$PATH dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~WorkspaceStatusEndpointTests|FullyQualifiedName~FolderCanonicalErrorMapperTests"
DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_HOME=/tmp/dotnet-cli-home MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 PATH=$HOME/.dotnet:$PATH dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false
DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_HOME=/tmp/dotnet-cli-home MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 PATH=$HOME/.dotnet:$PATH dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false
DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_HOME=/tmp/dotnet-cli-home PATH=$HOME/.dotnet:$PATH dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Queries.Folders.TaskStatusQueryHandlerTests -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceStatusQueryHandlerTests
DOTNET_ROOT=$HOME/.dotnet DOTNET_CLI_HOME=/tmp/dotnet-cli-home PATH=$HOME/.dotnet:$PATH dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.WorkspaceStatusEndpointTests -class Hexalith.Folders.Server.Tests.FolderCanonicalErrorMapperTests
git diff --check
```

## Validation Result

- Initial `dotnet test`: blocked before compilation because .NET first-time-use tried to write under read-only `/home/administrator`.
- `dotnet test` with `DOTNET_CLI_HOME=/tmp/dotnet-cli-home`: blocked by sandbox socket permissions in MSBuild/VSTest paths.
- Core focused build: passed, 0 warnings, 0 errors.
- Server focused build: passed, 0 warnings, 0 errors.
- Core focused xUnit v3 in-process run: passed, 50 total, 0 failed.
- Server focused xUnit v3 in-process run: passed, 45 total, 0 failed.
- `git diff --check`: passed.
