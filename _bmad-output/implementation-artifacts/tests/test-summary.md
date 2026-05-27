# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs` - Added Story 3.9 support-evidence endpoint coverage for unsupported freshness rejection, missing provider-support read authority, and stale/unavailable/malformed read-model Problem Details mapping.
- [x] Existing Story 3.9 endpoint coverage in `ProviderReadinessEndpointTests.cs` covers route registration, authorized evidence list, safe empty list, idempotency header rejection, invalid cursor/limit rejection, unsafe correlation rejection, safe metadata-only payloads, and no read-model observation on pre-authorization validation failures.
- [x] Existing Story 3.9 core coverage in `tests/Hexalith.Folders.Tests/Providers/Readiness/ProviderSupportEvidenceQueryHandlerTests.cs` covers tenant scoping, GitHub/Forgejo distinguishability through safe profile references, deterministic row ordering, bounded pagination, empty lists, authorization no-touch behavior, stale/unavailable/malformed tenant evidence, stale/unavailable read-model states, stale records, future-dated records, unsafe diagnostics, and missing capability profile references.

### E2E Tests
- [x] No browser/UI E2E tests were added because Story 3.9 exposes an API/read-model workflow and the project context keeps the Playwright UI lane deferred until Epic 6 supplies stable read-only console routes and selectors.
- [x] The generated API tests use ASP.NET Core TestServer and seeded read models as the hermetic E2E-style boundary for this story.

## Coverage

- API endpoint covered for Story 3.9: `GET /api/v1/provider-readiness/support-evidence`.
- New API scenarios added in this QA pass: 5 focused endpoint cases.
- Story 3.9 support-evidence endpoint scenarios now covered in the focused server class: 11 support-evidence cases plus route registration.
- Story 3.9 core query/read-model scenarios covered in the focused core class: 15 cases.
- Live external dependencies required: 0. The tests use xUnit v3, Shouldly, ASP.NET Core TestServer, in-memory read models, and recording fakes.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable until Epic 6 UI routes/selectors exist.
- [x] Tests use standard project APIs and semantic HTTP assertions.
- [x] Tests cover happy path and critical error cases.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter FullyQualifiedName~ProviderReadinessEndpointTests
dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter FullyQualifiedName~ProviderReadinessEndpointTests -m:1 /nodeReuse:false
./tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -noLogo -class Hexalith.Folders.Server.Tests.ProviderReadinessEndpointTests
dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --filter FullyQualifiedName~ProviderSupportEvidenceQueryHandlerTests -m:1 /nodeReuse:false
./tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -noLogo -class Hexalith.Folders.Tests.Providers.Readiness.ProviderSupportEvidenceQueryHandlerTests
```

## Validation Result

- Build: focused server and core test projects compiled successfully through serialized MSBuild (`-m:1 /nodeReuse:false`).
- Server endpoint tests: passed under xUnit v3 in-process runner, 21 total, 0 failed.
- Core support-evidence query tests: passed under xUnit v3 in-process runner, 15 total, 0 failed.
- VSTest limitation: `dotnet test` execution is blocked in this sandbox after build by `System.Net.Sockets.SocketException (13): Permission denied` when VSTest tries to create its TCP listener. The in-process xUnit runner was used for actual execution.
