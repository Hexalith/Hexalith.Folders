# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs` - Added provider-readiness REST guardrails for sanitized/generated correlation IDs, pre-service validation failures, provider unavailable mapping, client-controlled tenant mismatch, and freshness response metadata.
- [x] Existing route registration, `Idempotency-Key` rejection, unsupported freshness rejection, safe denial, operator diagnostic response, and provider rate-limit tests remain covered.

### E2E Tests
- [x] `tests/Hexalith.Folders.Tests/Providers/Readiness/ProviderReadinessValidationServiceTests.cs` - Added offline provider-readiness workflow coverage for tenant-scoped evidence records, reserved `system` tenant denial, mismatched binding reconciliation, and secret-shaped correlation redaction.
- [x] `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderReadinessValidationServiceTests.cs` - Added boundary-safe Forgejo readiness coverage proving binding metadata feeds Forgejo target version/base URL evidence while stored attempts and diagnostics remain metadata-only.
- [x] No browser/UI E2E tests were added because story 3.5 exposes an API/query workflow and no UI route.

## Coverage

- Provider-readiness core focused suite: 30/30 passing.
- Forgejo provider-readiness focused suite: 1/1 passing.
- Provider-readiness server focused suite: 10/10 passing.
- Full core test project: 619/619 passing.
- Full server test project: 80/80 passing.
- Contract Spine gate: 80/80 contract tests and 16/16 client tests passing.
- Safety invariant gate: 10/10 contract safety tests passing after solution build.
- Full solution test run: 877 passed, 1 existing UI E2E placeholder skipped, 0 failed.

## Validation

- [x] API tests generated where applicable.
- [x] E2E-style offline workflow tests generated for the provider-readiness query path.
- [x] Tests use xUnit v3, Shouldly, slim `WebApplication` endpoint tests, and existing fake provider seams.
- [x] Tests cover happy path plus critical error cases: idempotency rejection, unsupported freshness, provider rate limit, provider unavailable, authorization denial, stale tenant evidence, tenant mismatch, reserved tenant, Forgejo binding metadata construction, reconciliation required, and unsafe correlation inputs.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and run offline without GitHub, Forgejo, provider credentials, live Tenants services, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics and validation commands.

## Commands Run

```text
dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore --filter "FullyQualifiedName~ProviderReadinessValidationServiceTests"
dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore --filter "FullyQualifiedName~ForgejoProviderReadinessValidationServiceTests"
dotnet test tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~ProviderReadinessEndpointTests"
dotnet build Hexalith.Folders.slnx --no-restore
dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore
dotnet test tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore
pwsh -NoLogo -NoProfile -File tests\tools\run-safety-invariant-gates.ps1
pwsh -NoLogo -NoProfile -File tests\tools\run-contract-spine-gates.ps1
dotnet test Hexalith.Folders.slnx --no-restore
```
