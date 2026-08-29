---
title: 'Guard layered authorization accessor lifetime'
type: 'chore'
created: '2026-08-29'
status: 'done'
baseline_revision: '8a1deea1669f4b95234de8a1aa3e60af0c95a82f'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '{project-root}/_bmad-output/project-context.md'
warnings: []
deferred: []
---

<intent-contract>

## Intent

**Problem:** `SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence` proves request behavior but cannot independently detect an accidental singleton registration when the accessor is manually cleared. The server composition therefore lacks a direct regression guard for this security-relevant lifetime invariant.

**Approach:** Add a service-registration test against the public server composition root that requires the layered authorization accessor to have one scoped descriptor with the intended implementation type. Retain the existing sequential-request integration test as complementary behavior coverage.

## Boundaries & Constraints

**Always:** Inspect the descriptors produced by `AddFoldersServer`; assert the accessor registration is unique, maps `ILayeredFolderAuthorizationResultAccessor` to `ScopedLayeredFolderAuthorizationResultAccessor`, and uses `ServiceLifetime.Scoped`; use xUnit v3 and Shouldly; preserve the existing sequential-request test.

**Block If:** The public server composition root cannot be inspected without changing production registration behavior or introducing a new test dependency.

**Never:** Change the production accessor lifetime or implementation, weaken request-scope cleanup behavior, replace the sequential-request test, edit generated files or submodules, or edit `_bmad-output/implementation-artifacts/deferred-work.md`.

</intent-contract>

## Code Map

- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:37` -- read-only production registration: `TryAddScoped<ILayeredFolderAuthorizationResultAccessor, ScopedLayeredFolderAuthorizationResultAccessor>()` is the invariant under test.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs:65` -- public `AddFoldersServer` composition root; the regression test must exercise this outer registration surface rather than calling the inner extension directly.
- `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs` -- existing server composition/registration test home; add the descriptor-level lifetime guard here.
- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:172` -- existing `SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence` behavior test; keep unchanged and validate alongside the new structural guard.
- `tests/Hexalith.Folders.UI.Tests/UserContextAccessorRegistrationTests.cs:18` -- read-only local precedent for asserting `ServiceDescriptor.ImplementationType` and `Lifetime` with Shouldly.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs` -- add a focused test that registers the server into a fresh `WebApplicationBuilder.Services`, selects the sole descriptor for `ILayeredFolderAuthorizationResultAccessor`, and asserts its implementation type and scoped lifetime -- makes lifetime drift fail independently of cleanup behavior.

**Acceptance Criteria:**
- Given a fresh service collection, when `AddFoldersServer` registers server services, then exactly one descriptor exists for `ILayeredFolderAuthorizationResultAccessor`, its implementation type is `ScopedLayeredFolderAuthorizationResultAccessor`, and its lifetime is `ServiceLifetime.Scoped`.
- Given the accessor registration is changed to singleton or transient, duplicated, or mapped to another implementation, when the registration test runs, then it fails without relying on request execution or `EndScope` cleanup.
- Given the new structural guard is added, when the archive process-wiring coverage runs, then `SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence` remains present and passes unchanged.

## Spec Change Log

## Review Triage Log

### 2026-08-29 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 0
- defer: 0
- reject: 18: (high 0, medium 0, low 18)
- addressed_findings:
  - none

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- expected: all server tests pass, including the new lifetime registration guard.
- `dotnet test tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence"` -- expected: the retained sequential-request behavior test passes; if the local xUnit v3 runner rejects the project-level filter, build the project and invoke the built test assembly with its single-dash `-method` selector.

## Auto Run Result

Status: done

Summary: Added an outer-composition service-descriptor regression test that requires `ILayeredFolderAuthorizationResultAccessor` to remain uniquely registered as `ScopedLayeredFolderAuthorizationResultAccessor` with scoped lifetime. The existing sequential-request behavior test remains unchanged.

Files changed:
- `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs` -- added the scoped-lifetime registration guard.
- `_bmad-output/implementation-artifacts/spec-archive-accessor-lifetime-guard.md` -- recorded the implementation contract, review disposition, and verification evidence.

Review findings breakdown: 0 patches applied, 0 items deferred, 18 low-impact suggestions rejected as scope expansions, already-satisfied checks, or workflow-state observations.

Follow-up review recommendation: false. Patched findings: high 0, medium 0, low 0; score 0.

Verification performed:
- `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- passed 645, failed 0, skipped 0.
- `dotnet test tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence"` -- passed 1, failed 0, skipped 0.

Residual risks: none identified within the requested service-registration guard. Host-specific intentional service overrides remain governed by their owning composition tests.
