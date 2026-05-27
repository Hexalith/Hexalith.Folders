# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs` - Added Story 4.4 endpoint coverage for unsupported GET freshness rejection before read-model access, malformed GET route/correlation/task identifier rejection before read-model access, unauthenticated GET safe denial before read-model access, expired lock status response shape, and unauthenticated release rejection before gateway submission.
- [x] Existing Story 4.4 endpoint coverage in `WorkspaceLockEndpointTests.cs` covers route registration, authorized lock inspection shape, no `Idempotency-Key` on GET, accepted release request shape, route-authoritative `workspaceId`, required release headers, malformed JSON, unsupported schema version, invalid body, unknown-field rejection with redaction, release reason-code normalization, lock-not-owned, lock-expired, transition-invalid, reconciliation-required, and idempotency conflict mapping.
- [x] `tests/Hexalith.Folders.Tests/Queries/Folders/WorkspaceLockStatusProjectionTests.cs` - Added stale-projection lock status coverage.
- [x] Existing Story 4.4 aggregate, service, query, authorization, action-token, and contract tests cover accepted clean release, replay, idempotency conflict, schema validation, wrong workspace/lock/task/proof, archived and non-locked C6 states including committed, expired locks, authorization-first safe denial, idempotency lookup unavailable, append conflict reread, metadata-only responses, and Contract Spine parity.

### E2E Tests
- [x] No browser/UI E2E tests were added because Story 4.4 exposes API/domain lock inspection and release behavior, and the project context keeps Playwright UI E2E deferred until Epic 6 supplies stable read-only console routes and selectors.
- [x] The generated API tests use ASP.NET Core TestServer and in-process xUnit execution as the hermetic E2E-style boundary for this story.

## Coverage

- Story 4.4 API endpoints covered: `GET /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock` and `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/lock/release` (2/2 in-scope endpoints).
- New API scenarios added in this QA/review pass: 1 theory covering 4 malformed GET identifier cases, plus the previously generated focused endpoint cases.
- New aggregate scenarios added in this review pass: committed-state release rejection.
- New query/read-model scenarios added in this QA pass: 1 focused stale-projection case.
- Focused validation coverage: aggregate namespace, workspace-lock query/read-model class, workspace-lock endpoint class, action catalog, action-token mapper, and `WorkspaceLockContractGroupTests`.
- UI features covered: not applicable for this story (0/0 in-scope UI workflows).
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory state, and recording fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.4.
- [x] Tests use standard project APIs and semantic HTTP/query assertions.
- [x] Tests cover happy path and critical error cases.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceLockReleaseAggregateTests
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceLockStatusProjectionTests
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Authorization.WorkspaceLockActionCatalogTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.FolderCommandActionTokenMapperTests
tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.WorkspaceLockContractGroupTests
git diff --check
```

## Validation Result

- Builds: focused core, server, and contract test projects compiled successfully.
- Workspace lock release aggregate tests: passed, 22 total, 0 failed.
- Workspace lock query/read-model tests: passed, 6 total, 0 failed.
- Workspace lock action catalog tests: passed, 2 total, 0 failed.
- Workspace lock endpoint tests: passed, 26 total, 0 failed.
- Action-token mapper tests: passed, 7 total, 0 failed.
- Workspace lock contract group tests: passed, 6 total, 0 failed.
- `git diff --check`: passed.
