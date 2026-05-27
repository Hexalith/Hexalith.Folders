# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Folders.Server.Tests/WorkspaceCleanupStatusEndpointTests.cs` - Added Story 4.10 cleanup-status API coverage for folder ACL safe denial before read-model access.
- [x] `tests/Hexalith.Folders.Server.Tests/WorkspaceCleanupStatusEndpointTests.cs` - Added fail-closed HTTP coverage for available-but-scope-mismatched cleanup snapshots: wrong workspace, wrong task, and wrong correlation.

### Core Query Tests

- [x] `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceCleanupStatusQueryHandlerTests.cs` - Added cleanup projection `not_found` fail-closed coverage.
- [x] `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceCleanupStatusQueryHandlerTests.cs` - Expanded malformed/scope mismatch coverage for invalid cleanup reason, invalid freshness metadata, future timestamps, tenant/folder/workspace mismatches, evidence principal/tenant/action mismatches, and task/correlation incompatibility.

### E2E Tests

- [x] Browser UI E2E tests were assessed and not added because Story 4.10 is an API/status boundary with no in-scope UI workflow; project context keeps Playwright UI E2E deferred until Epic 6 supplies stable read-only console routes and selectors.
- [x] The generated API tests use ASP.NET Core TestServer and xUnit v3 as the hermetic E2E-style HTTP boundary for this story.

## Coverage

- API endpoints covered: 1/1 cleanup status route (`GetWorkspaceCleanupStatus`).
- Cleanup status states covered at HTTP boundary: 4/4 (`pending`, `succeeded`, `failed`, `status_only`).
- API critical error cases covered: idempotency-key rejection, unsupported freshness rejection, malformed identifiers, missing authentication context, folder ACL safe denial, safe not found, projection stale, projection unavailable, malformed/read-model unavailable, scope-mismatched snapshots, and leakage-corpus non-echo.
- Core critical error cases covered: authentication failure, tenant denial before read model, malformed identifiers before authorization/read model, missing projection, stale/unavailable/malformed read model outcomes, malformed snapshot shape, future timestamps, task/correlation mismatch, authorization watermark mismatch, scope mismatch, and evidence mismatch.
- UI features covered: not applicable for this story (0/0 in-scope UI workflows).
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory state, and recording/fixed fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.10.
- [x] Tests use standard project APIs and semantic HTTP/domain assertions.
- [x] Tests cover happy path and critical error cases.
- [x] Tests use semantic HTTP route/header/body assertions and no CSS/text/sleep-based UI locators.
- [x] Tests have clear descriptions.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceCleanupStatusQueryHandlerTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceCleanupStatusEndpointTests
git diff --check
awk '/[ \t]$/{print FILENAME ":" FNR ": trailing whitespace"; bad=1} END{exit bad}' src/Hexalith.Folders/Queries/Folders/WorkspaceCleanupStatusQueryHandler.cs tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceCleanupStatusQueryHandlerTests.cs tests/Hexalith.Folders.Server.Tests/WorkspaceCleanupStatusEndpointTests.cs _bmad-output/implementation-artifacts/tests/test-summary.md
```

## Validation Result

- Core test project build: passed, 0 warnings, 0 errors.
- Server test project build: passed, 0 warnings, 0 errors.
- Workspace cleanup status query handler tests: passed, 33 total, 0 failed.
- Workspace cleanup status endpoint tests: passed, 21 total, 0 failed.
- Existing workspace status, workspace lock, lifecycle status, authorization gate, and action catalog focused regressions: passed, 71 total, 0 failed.
- Contract/OpenAPI focused tests: passed, 12 total, 0 failed.
- Contract rules artifact tests: passed, 5 total, 0 failed.
- `git diff --check`: passed for tracked diffs; Git reported existing CRLF/LF normalization warnings on unrelated tracked files.
- Explicit trailing-whitespace check across generated/updated Story 4.10 files: passed.
- The focused validation used xUnit v3 in-process runners because this story already records VSTest socket creation as blocked in the sandbox.
