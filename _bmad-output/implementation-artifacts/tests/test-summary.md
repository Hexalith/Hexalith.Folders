# Test Automation Summary

## Generated Tests

### API Tests

- [x] `tests/Hexalith.Folders.Server.Tests/FolderAuditEndpointFilterTests.cs` - Added replay-specific audit coverage proving a successful REST response with `idempotentReplay=true` emits `FolderAuditResult.Replayed`, `idempotent_replay`, and `IsIdempotentReplay=true` without marking the operation as a duplicate.

### E2E Tests

- [x] Browser UI E2E tests were assessed and not added because Story 4.14 is a backend/API observability workflow and the repository defers operations-console UI E2E until stable Epic 6 read-only console routes/selectors exist.

## Coverage

- Audit endpoint filter: 7 focused cases covering query failure metadata, stable endpoint operation-kind mapping, retry flag capture, `/process` accepted/rejected observations, and REST idempotent-replay metadata.
- Core observability model: 20 focused cases covering approved metadata, forbidden sentinel sanitization, redacted duration buckets, and low-cardinality telemetry tag names.
- Safety invariant gates: 11 focused cases covering 4.14 telemetry channel inventory, artifact source resolution, sentinel scans, and metadata-only diagnostics.
- UI features covered: not applicable for Story 4.14.

## Validation

- [x] API tests generated where applicable.
- [x] UI E2E tests assessed; not applicable for Story 4.14.
- [x] Tests use standard project APIs: xUnit v3, Shouldly, ASP.NET Core TestServer, and focused recording fakes.
- [x] Tests cover happy path and critical replay/error cases.
- [x] Tests use semantic HTTP route/header/body assertions; no CSS/text/sleep-based UI locators.
- [x] Tests have clear descriptions.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and require no live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.

## Commands Run

```text
DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet build /mnt/d/Hexalith.Folders/tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet /mnt/d/Hexalith.Folders/tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.FolderAuditEndpointFilterTests -parallel none -noLogo -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet build /mnt/d/Hexalith.Folders/tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet /mnt/d/Hexalith.Folders/tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Observability.FolderAuditObservationTests -parallel none -noLogo -noColor
DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet build /mnt/d/Hexalith.Folders/tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1
DOTNET_CLI_HOME=/tmp/dotnet-cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet /mnt/d/Hexalith.Folders/tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests -parallel none -noLogo -noColor
git diff --check -- src/Hexalith.Folders.Server/FolderAuditEndpointFilter.cs tests/Hexalith.Folders.Server.Tests/FolderAuditEndpointFilterTests.cs
```

## Validation Result

- Initial `dotnet test` from the repository root was blocked because `global.json` requests SDK `10.0.300` and this sandbox only has SDK `10.0.108`.
- `dotnet test` from `/tmp` could build with SDK `10.0.108`, but VSTest was blocked by sandbox socket permissions.
- Server focused build: passed, 0 warnings, 0 errors.
- Server focused xUnit v3 in-process run: passed, 7 total, 0 failed.
- Core focused build: passed, 0 warnings, 0 errors.
- Core focused xUnit v3 in-process run: passed, 20 total, 0 failed.
- Contracts focused build: passed, 0 warnings, 0 errors.
- Contracts focused xUnit v3 in-process run: passed, 11 total, 0 failed.
- `git diff --check`: passed.
