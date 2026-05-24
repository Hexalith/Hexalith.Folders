# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs` - Existing provider-readiness/support evidence Contract Spine tests were validated to prove story 3.2 did not introduce public API drift.
- [x] Runtime API endpoint tests were not added for story 3.2 because `IGitProvider` and the capability model are internal provider-port abstractions with no REST, SDK, CLI, MCP, or UI surface in this story.

### E2E Tests
- [x] `tests/Hexalith.Folders.Tests/Providers/Abstractions/ProviderCapabilityDiscoveryWorkflowTests.cs` - Added offline end-to-end provider capability discovery workflow tests through `ProviderCapabilityDiscoveryService`.
- [x] `ProviderCapabilityDiscoveryWorkflowTests.DiscoveryServiceShouldReturnRequiredMetadataOnlyCapabilityProfileShape` - Covers authorized discovery, required profile shape, normalized provider identity, safe target/version evidence, supported/partial/emulated operations, operation limits, credential mode requirements, failure mappings, rate-limit posture, fingerprint/version metadata, correlation ID, and call ordering.
- [x] `ProviderCapabilityDiscoveryWorkflowTests.DiscoveryServiceShouldReturnUnsupportedResultWhenAuthorizedProviderCannotBeResolved` - Covers authorized unsupported-provider discovery without live provider calls.
- [x] `ProviderCapabilityDiscoveryWorkflowTests.DiscoveryServiceShouldPropagateProviderRateLimitAsSafeRetryableFailure` - Covers a critical provider error path with retryability and retry-after metadata.
- [x] `ProviderCapabilityDiscoveryWorkflowTests.ProviderComparisonShouldExposeChangedFingerprintDimensionsForEquivalentAndChangedProfiles` - Covers profile comparison behavior for equivalent profiles and changed safe evidence.

## Coverage

- Story 3.2 provider abstraction focused suite: 32/32 passing.
- Core `Hexalith.Folders.Tests` project: 512/512 passing.
- Provider Contract Spine group: 5/5 passing.
- Full solution verification: 756 passed, 1 existing UI E2E placeholder skipped, 0 failed.
- UI browser E2E: not applicable for story 3.2; the feature is an internal provider port and the existing UI E2E placeholder remains intentionally skipped until stable operations-console routes/selectors exist.

## Validation

- [x] API tests generated/validated where applicable through existing Contract Spine tests.
- [x] E2E-style offline workflow tests generated for the implemented internal capability discovery feature.
- [x] Tests use xUnit v3 and Shouldly standard APIs.
- [x] Tests cover happy path plus unsupported-provider and rate-limited critical error cases.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and run offline without GitHub, Forgejo, provider credentials, live Tenants services, Aspire, Dapr sidecars, Redis, Keycloak, or nested submodule initialization.
- [x] Summary includes coverage metrics and validation commands.

## Commands Run

```text
dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore --filter FullyQualifiedName~Providers.Abstractions
dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore
dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~TenantFolderProviderContractGroupTests
dotnet test Hexalith.Folders.slnx --no-restore
```
