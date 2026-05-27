# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs` - Added Story 4.7 remove-file endpoint coverage proving top-level add/change transport evidence fields (`mediaType`, `transportEvidenceKind`, `observedByteLength`) are rejected before EventStore gateway submission and are not echoed in Problem Details.
- [x] Existing Story 4.7 endpoint coverage covers valid remove payload submission, route-authoritative workspace payload, content hash/byte length/inline/stream descriptor rejection, safe Problem Details, gateway reason normalization, operation-route mismatch rejection, required headers, path policy denial, and no unsafe payload echo.
- [x] Existing Story 4.7 domain/service coverage covers metadata-only remove acceptance, delete-order staging after path evidence and idempotency lookup, equivalent replay before duplicate delete ordering, idempotency conflict before delete ordering, delete-order unavailable fail-closed behavior, idempotency unavailable behavior, and no delete ordering on authorization/workspace/task denials.
- [x] Existing Story 4.7 aggregate/contract coverage covers `metadataOnlyRemoval`, content-field absence, C6 `locked -> changes_staged`, validator rejection of add/change evidence on remove, metadata-only leakage checks, and RemoveFile contract/idempotency parity.

### E2E Tests

- [x] No browser UI E2E tests were added because Story 4.7 is an API/domain mutation workflow; project context keeps Playwright UI E2E deferred until Epic 6 supplies stable read-only console routes and selectors.
- [x] The generated API tests use ASP.NET Core TestServer and the xUnit executable runner as the hermetic E2E-style boundary for this story.

## Coverage

- API endpoints covered: `POST /api/v1/folders/{folderId}/workspaces/{workspaceId}/files/remove`, plus retained add/change regression rows in the shared file-mutation endpoint tests.
- New API scenarios added in this QA pass: 3 focused remove endpoint cases for forbidden top-level transport evidence fields.
- Domain/service scenarios covered: remove delete-order staging, replay/conflict short-circuiting, fail-closed delete ordering, path evidence denial, authorization and state denial behavior, and metadata-only accepted events.
- Contract scenarios covered: RemoveFile operation, metadata-only transport, idempotency equivalence fields, content field forbiddance, and generated-client drift guardrails.
- UI features covered: not applicable for this story (0/0 in-scope UI workflows).
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory state, and recording fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.7.
- [x] Tests use standard project APIs and semantic HTTP/domain assertions.
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
tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -parallel none -noLogo -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationAggregateTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.WorkspaceLockEndpointTests
tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -parallel none -noLogo -class Hexalith.Folders.Contracts.Tests.OpenApi.FileContextContractGroupTests
git diff --check
```

## Validation Result

- Core project build: passed, 0 warnings, 0 errors.
- Core test project build: passed, 0 warnings, 0 errors.
- Server test project build: passed, 0 warnings, 0 errors.
- Contracts test project build: passed, 0 warnings, 0 errors.
- File-mutation aggregate/service/path-policy tests: passed, 60 total, 0 failed.
- Workspace lock/file mutation endpoint tests: passed, 59 total, 0 failed.
- File context contract group tests: passed, 5 total, 0 failed.
- `git diff --check`: passed.
