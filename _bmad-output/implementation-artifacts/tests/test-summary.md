# Test Automation Summary

## Generated Tests

### API Tests
- [x] `tests/Hexalith.Folders.Server.Tests/RepositoryBackedFolderEndpointTests.cs` - Added bind-repository invalid branch/ref policy coverage for missing default ref, empty allowed refs, and invalid policy identifiers before gateway submission.
- [x] Existing bind-repository endpoint tests cover accepted submission, required headers, unsupported schema version, malformed JSON, reserved `system` tenant denial before body parsing, unknown field rejection, safe gateway failure mapping, and metadata-only problem responses.

### E2E Tests
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderRepositoryBindingGateTests.cs` - Added offline workflow guardrails proving archived folders and already-bound folders short-circuit before idempotency, readiness, provider binding reads, provider resolution, provider calls, or appends.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderRepositoryBindingGateTests.cs` - Added readiness failure mapping coverage for stale/unavailable projections, read-model unavailable, auth failures, unsupported capability, unknown outcome, reconciliation required, unavailable/rate-limited/transient provider states, permission failures, and repository conflicts.
- [x] Existing Story 3.7 tests cover aggregate accept/replay/conflict/missing/archived/in-progress/bound cases, provider GitHub/Forgejo existing-repository binding success/equivalent/failure mappings, projection replay, metadata-only evidence, and bind endpoint safe responses.
- [x] No browser/UI E2E tests were added because Story 3.7 is an API/domain/provider workflow and project context keeps UI E2E deferred until stable read-only console routes exist.

## Coverage

- API endpoint scenarios added: 3 bind branch/ref validation cases.
- Domain workflow scenarios added: 2 short-circuit ordering cases plus 13 readiness mapping cases.
- Story 3.7 relevant lanes now include API endpoint, aggregate/domain gate, provider adapter seam, and projection replay coverage.
- Live external dependencies required: 0. The generated tests are hermetic by design and use xUnit v3, Shouldly, TestServer, recording fakes, and fake providers.

## Validation

- [x] API tests generated where applicable.
- [x] E2E-style offline workflow tests generated for the bind-repository service gate.
- [x] Tests use standard project test APIs and existing fake/recording seams.
- [x] Tests cover happy path through existing coverage and critical error cases through the new readiness and validation cases.
- [x] Tests use no hardcoded waits or sleeps.
- [x] Tests are independent and do not require live GitHub, Forgejo, provider credentials, tenant seed data, Aspire, Dapr sidecars, Redis, Keycloak, Docker, network access, or nested submodule initialization.
- [x] Summary includes coverage metrics.
- [ ] Test execution completed: blocked by restore/build environment.

## Commands Run

```text
dotnet --info
DOTNET_CLI_HOME=/tmp/hexalith-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --filter "FullyQualifiedName~BindRepository|FullyQualifiedName~RepositoryBinding|FullyQualifiedName~GitHubProviderTests|FullyQualifiedName~ForgejoProviderTests"
DOTNET_CLI_HOME=/tmp/hexalith-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore --filter "FullyQualifiedName~RepositoryBackedFolderEndpointTests"
DOTNET_CLI_HOME=/tmp/hexalith-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --verbosity minimal
DOTNET_CLI_HOME=/tmp/hexalith-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 NUGET_PACKAGES=/tmp/hexalith-dotnet-home/.nuget/packages dotnet restore src/Hexalith.Folders/Hexalith.Folders.csproj --ignore-failed-sources --verbosity normal
```

## Validation Blocker

- The installed SDK is `10.0.300`.
- Initial test execution failed the .NET first-time-use path because `/home/administrator/.dotnet/10.0.300.toolpath.sentinel` is read-only.
- Re-running with writable `DOTNET_CLI_HOME` cleared that blocker.
- A direct build then failed because existing `obj/project.assets.json` files were restored on Windows and reference the unavailable fallback folder `C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages`.
- Attempted Linux restore is blocked by restricted network access to `https://api.nuget.org/v3/index.json`, producing `NU1801` and `NU1101` for packages such as `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`, and `Octokit`.
