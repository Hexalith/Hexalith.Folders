# Test Automation Summary

## Story 4.17 Lifecycle Capacity Harness

### Generated Tests

- [x] `tests/Hexalith.Folders.LoadTests.Tests/LifecycleCapacityHarnessTests.cs` - Covers direct prepare, lock, mutate, and commit driver execution through the production lifecycle services.
- [x] The harness tests verify safe synthetic identifiers, unique mutating idempotency keys, tenant-scoped recorder counts for overlapping identifiers, metadata-only reference-pending evidence shape, and fail-fast invalid profile/ordinal handling.
- [x] Review coverage verifies generated NBomber log files do not retain local absolute report-folder paths after the harness run.

### Validation

- [x] Focused load harness test project restore passed.
- [x] Focused load harness test project build passed with 0 warnings and 0 errors.
- [x] xUnit v3 in-process harness tests passed: 5 total, 0 failed.
- [x] Load harness self-check passed.
- [x] Quick NBomber lifecycle run passed: 3 full-lifecycle iterations, 0 failures, `thresholds: reference_pending`, 12 measured operations, and 12 idempotency keys.
- [x] Generated report/log redaction scan passed for `/mnt/`, `/home/`, `C:\`, and `Users` path markers.
- [x] `git diff --check` passed; Git reported line-ending warnings for existing CRLF-normalized files only.

### Commands Run

```text
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 PATH=/home/administrator/.dotnet:$PATH dotnet restore tests/Hexalith.Folders.LoadTests.Tests/Hexalith.Folders.LoadTests.Tests.csproj --tlp:off -v:minimal /nr:false
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 PATH=/home/administrator/.dotnet:$PATH dotnet build tests/Hexalith.Folders.LoadTests.Tests/Hexalith.Folders.LoadTests.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 PATH=/home/administrator/.dotnet:$PATH dotnet tests/Hexalith.Folders.LoadTests.Tests/bin/Debug/net10.0/Hexalith.Folders.LoadTests.Tests.dll
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 PATH=/home/administrator/.dotnet:$PATH dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj --no-build -- --self-check --profile quick --report-folder /tmp/hexalith-folders-load-self-check-final
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 PATH=/home/administrator/.dotnet:$PATH dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj --no-build -- --profile quick --report-folder /tmp/hexalith-folders-load-reports-final --run-id story-4-17-validation-final
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 NUGET_FALLBACK_PACKAGES= PATH=/home/administrator/.dotnet:$PATH dotnet test tests/Hexalith.Folders.LoadTests.Tests/Hexalith.Folders.LoadTests.Tests.csproj --no-build --tlp:off -v:minimal /nr:false /m:1
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 NUGET_FALLBACK_PACKAGES= PATH=/home/administrator/.dotnet:$PATH dotnet run --project tests/load/Hexalith.Folders.LoadTests.csproj --no-build -- --profile quick --report-folder artifacts/load-reports/quick --run-id story-4-17-review-sanitized
rg -n "/mnt/|/home/|C:\\|Users" artifacts/load-reports/quick tests/load/bin/Debug/net10.0/artifacts/load-reports/quick
git diff --check
```

## Generated Tests

### API Tests

- [x] No runtime API endpoint tests were added for Story 4.16 because AC7 explicitly excludes runtime endpoints, live providers, Dapr sidecars, UI pages, CLI/MCP commands, and broad integration harness work.
- [x] Service/API-boundary coverage was added through hermetic lifecycle services and query handlers using the existing xUnit v3 test framework.

### E2E Tests

- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleSecurityBoundaryTests.cs` - Covers overlapping tenant lifecycle streams, tenant-scoped idempotency ledgers, and wrong-tenant attempts that avoid protected tenant state mutation.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderWorkspaceFileMutationServiceTests.cs` - Covers path-security ordering, malformed idempotency boundary failures, tenant mismatch short-circuiting, path evidence denials, content/delete side-effect ordering, replay, and idempotency conflicts.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/WorkspacePathPolicyValidatorTests.cs` - Covers traversal, absolute paths, mixed separators, encoded dot/separator smuggling, reserved names, empty segments, trailing-space/dot ambiguity, controls, invisible characters, non-NFC input, and Unicode ambiguity without unsafe path echo.
- [x] `tests/Hexalith.Folders.Tests/Queries/FileContext/WorkspaceFileContextQueryHandlerTests.cs` - Covers unsafe context-query paths denying before sensitivity classification or context-source observation, with serialized results not echoing raw unsafe paths.
- [x] `tests/Hexalith.Folders.Tests/Observability/FolderAuditObservationTests.cs` - Covers audit/lifecycle redaction against `tests/fixtures/audit-leakage-corpus.json` and bounded metadata-only denial evidence.
- [x] `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleReplayFixture.cs` - Adds metadata-only overlapping tenant lifecycle fixtures for Story 4.16 security isolation scenarios.

## Coverage

- Sentinel redaction: lifecycle result/event/audit surfaces and forbidden corpus values are checked for metadata-only serialization.
- Path security: validator, mutation service, and context-query paths cover unsafe normalized paths, display names, Unicode ambiguity, syntactic denial before protected observation, and evidence-layer denial before side effects.
- Encoding/idempotency boundaries: lifecycle mutation rejects malformed idempotency input before stream construction, path evidence, idempotency lookup, content staging, delete ordering, or append; existing client/governance corpus tests remain authoritative for generated parser/hash behavior.
- Tenant isolation: overlapping tenant A/B identifiers exercise separate streams, ledgers, state snapshots, content-store requests, and wrong-tenant non-mutation.
- Critical error cases: authorization denial, tenant mismatch, path-policy denial, policy-evidence unavailable, lock/state rejections, idempotency replay/conflict/unavailable, content-store failure, delete-order failure, stale/replay/projection determinism, and metadata-only audit denial categories.
- UI features covered: not applicable for Story 4.16; no UI page or stable browser workflow is in scope.

## Validation

- [x] API/service-boundary tests generated where applicable.
- [x] E2E/domain workflow tests generated where applicable.
- [x] Tests use standard project APIs: xUnit v3, Shouldly, in-memory repositories/read models/projections, and xUnit v3 in-process execution.
- [x] Tests cover happy paths and critical error cases.
- [x] Tests use semantic domain/read-model assertions; no CSS selectors, brittle text selectors, hardcoded waits, sleeps, live providers, runtime endpoints, filesystem working copies, or nested submodule initialization.
- [x] Tests have clear descriptions.
- [x] Tests are independent and require no Aspire, Dapr sidecars, Redis, Keycloak, Docker, provider credentials, or network access for the Story 4.16-focused lane.
- [x] Summary includes coverage metrics.

## Commands Run

```text
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-build --tlp:off -v:minimal /nr:false --filter "FullyQualifiedName~FolderLifecycleSecurityBoundaryTests|FullyQualifiedName~FolderWorkspaceFileMutationServiceTests|FullyQualifiedName~WorkspacePathPolicyValidatorTests|FullyQualifiedName~FolderAuditObservationTests|FullyQualifiedName~WorkspaceFileContextQueryHandlerTests"
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Aggregates.Folder.FolderLifecycleSecurityBoundaryTests -class Hexalith.Folders.Tests.Aggregates.Folder.FolderWorkspaceFileMutationServiceTests -class Hexalith.Folders.Tests.Aggregates.Folder.WorkspacePathPolicyValidatorTests -class Hexalith.Folders.Tests.Observability.FolderAuditObservationTests -class Hexalith.Folders.Tests.Queries.FileContext.WorkspaceFileContextQueryHandlerTests
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll -class Hexalith.Folders.Tests.Aggregates.Folder.FolderLifecycleReplayDeterminismTests -class Hexalith.Folders.Tests.Queries.Folders.WorkspaceLifecycleProjectionDeterminismTests -class Hexalith.Folders.Tests.Authorization.TenantAccessAuthorizerTests -class Hexalith.Folders.Tests.Authorization.LayeredFolderAuthorizationServiceTests
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests -class Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet build tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj --no-restore --tlp:off -v:minimal /nr:false /m:1
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests.dll -class Hexalith.Folders.Client.Tests.ClientGenerationTests
DOTNET_ROOT=/home/administrator/.dotnet DOTNET_CLI_HOME=/tmp/sa-codex-home-fbbf34ce DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /home/administrator/.dotnet/dotnet tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests.dll -class Hexalith.Folders.Client.Tests.ClientGenerationTests -method- Hexalith.Folders.Client.Tests.ClientGenerationTests.HelperGenerationTargetRegeneratesWhenContractSpineChanges -method- Hexalith.Folders.Client.Tests.ClientGenerationTests.GeneratedClientAndHelpersMatchIsolatedRegeneration
git diff --check
git diff --no-index --check /dev/null tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderLifecycleSecurityBoundaryTests.cs
git diff --no-index --check /dev/null _bmad-output/implementation-artifacts/4-16-validate-lifecycle-security-boundaries.md
```

## Validation Result

- Focused Story 4.16 test build: passed, 0 warnings, 0 errors.
- VSTest `dotnet test`: blocked by sandbox socket permission (`System.Net.Sockets.SocketException (13): Permission denied`).
- Story 4.16 xUnit v3 in-process run: passed, 146 total, 0 failed.
- Adjacent replay/projection/authorization xUnit v3 in-process run: passed, 54 total, 0 failed.
- Contract safety/governance xUnit v3 in-process run: passed, 22 total, 0 failed.
- Client test build: passed, 0 warnings, 0 errors.
- Client generation full xUnit v3 in-process run: 17 total, 2 failed due environment-bound subprocess validation. One child process resolves SDK `10.0.108` from `/usr/lib/dotnet` instead of required SDK `10.0.302`; one isolated restore attempts blocked NuGet access to `https://api.nuget.org/v3/index.json`.
- Client generation corpus/parser subset excluding the two subprocess regeneration tests: passed, 15 total, 0 failed.
- `git diff --check`: passed.
- Explicit whitespace checks for untracked Story 4.16 files: passed.
