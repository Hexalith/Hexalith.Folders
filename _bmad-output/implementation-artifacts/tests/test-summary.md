# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Folders.Server.Tests/MutationEnvelopeEndpointMatrixTests.cs` - Added a Story 4.11 mutation-envelope matrix for all mutating lifecycle REST routes.

### E2E Tests

- [x] The generated API tests use ASP.NET Core TestServer and xUnit v3 as the hermetic HTTP boundary for this backend story.
- [x] Browser UI E2E tests were assessed and not added because Story 4.11 covers REST, `/process`, service, and contract metadata propagation with no in-scope UI workflow.

## Coverage

- Mutating REST routes covered by the new matrix: 10/10.
- Header fault cases covered per route: 6/6 (`Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id` missing and malformed).
- Total new matrix assertions: 60 request-level rejection checks, each asserting `400 validation_error`, unsafe value non-echo for malformed headers, and zero gateway submissions.
- `/process` handler regression coverage validated: 13 existing tests for tenant evidence, malformed envelope values, missing task extension, authorization short-circuit, and processor dispatch behavior.
- UI features covered: not applicable for this story (0/0 in-scope UI workflows).
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory state, and recording/fixed fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.11.
- [x] Tests use standard project APIs.
- [x] Tests cover happy path-adjacent acceptance guardrails and critical error cases.
- [x] Tests use HTTP route/header/body assertions and no CSS/text/sleep-based UI locators.
- [x] Tests have clear descriptions.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore -nr:false -p:BuildInParallel=false -maxcpucount:1 --tlp:off -v:minimal
dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~MutationEnvelopeEndpointMatrixTests --tlp:off -v:minimal
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -class Hexalith.Folders.Server.Tests.MutationEnvelopeEndpointMatrixTests -parallel none -noLogo
tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -class Hexalith.Folders.Server.Tests.FoldersDomainServiceRequestHandlerTests -parallel none -noLogo
git diff --check
```

## Validation Result

- Server test project build: passed, 0 warnings, 0 errors.
- `dotnet test` through VSTest: blocked before discovery by sandbox socket permission (`System.Net.Sockets.SocketException (13): Permission denied`).
- Mutation envelope matrix via xUnit v3 in-process runner: passed, 10 total route cases, 0 failed.
- `/process` handler focused regression via xUnit v3 in-process runner: passed, 13 total, 0 failed.
- `git diff --check`: passed.
