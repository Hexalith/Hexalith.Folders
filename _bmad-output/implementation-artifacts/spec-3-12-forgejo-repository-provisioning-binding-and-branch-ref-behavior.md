---
title: 'Story 3.12: Forgejo repository provisioning, binding, and branch/ref behavior'
type: 'feature'
created: '2026-08-26'
status: 'in-review'
baseline_revision: '84243a4b1853471cd146dad062393fb70c170b73'
baseline_commit: '84243a4b1853471cd146dad062393fb70c170b73'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md'
  - 'docs/adrs/0003-provider-abstraction-and-capability-model.md'
warnings: [oversized]
deferred: []
---

<intent-contract>

## Intent

**Problem:** Forgejo create/bind succeeds only through recording fakes: the concrete HTTP methods throw, production DI hard-wires an unconfigured provider, opaque references bypass the shared target resolver, and durable admission is ignored. This cannot prove canonical repository identity, exact branch/ref behavior, safe replay, or real Forgejo compatibility.

**Approach:** Complete the Forgejo-private typed HTTP path behind the existing provider-neutral contracts, mirror the proven GitHub boundary ordering while preserving Forgejo-specific API/version behavior, harden pinned contract evidence, and provide a credential-gated live evidence runner for operator execution.

## Boundaries & Constraints

**Always:** Authorize and validate current tenant/evidence before admission; gate replay/conflict/expiry before target, secret, client, or provider access; resolve opaque profile/repository/policy references only through `IProviderRepositoryTargetResolver`; dispatch at most one mutation; use ordinal exact branch/ref and canonical numeric repository identity; keep credentials, raw owner/repository/ref/URL/body/exception data inside the adapter; bind each supported version to reviewed operation shapes; preserve cancellation phase and no-blind-retry semantics.

**Block If:** A required behavior cannot be represented without changing the public OpenAPI/SDK/parity contract, or authentic Forgejo contract evidence contradicts the shared target/result model.

**Never:** Treat Forgejo as a GitHub base-URL swap; parse opaque references as locators; accept prefix/alias/name similarity as identity; downgrade an unsupported version to a nearby snapshot; retry an ambiguous POST; weaken HTTPS/same-origin rules; implement Story 3.13 file/commit/status or Story 3.14 orchestration; write or revert `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Create | Authorized fresh intent and resolved organization target | One `POST /api/v1/orgs/{org}/repos`, no implicit initialization, canonical numeric id returned | Pre-dispatch cancellation is known/no-call; ambiguous post-dispatch evidence is non-retryable unknown |
| Existing create target | Conflict response followed by one authorized identity observation | Equivalent only when expected canonical id and intent evidence match | Otherwise stable repository conflict; never a second POST |
| Bind/ref | Authorized resolved target | Exact repository id, visibility, default branch, selected branch, permissions, and required protection are verified | Hidden/missing remains concealed; unsupported ref kind and policy mismatch stay distinct |
| Durable admission | Replay, conflict, expiry, or malformed evidence | Exact safe terminal replay or canonical rejection with zero downstream calls | Prior unknown remains unknown with reconciliation evidence |
| Version/transport | Supported profile and bounded JSON response | Version-specific request/response shapes and safe retry metadata are enforced | Redirect, drift, malformed/oversized body, 4xx/429/5xx, and disconnect map without payload leakage |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:198-485` -- current create/bind façade; add admission validation/replay, shared target resolution, safe resolver mapping, canonical-id propagation, and phase-correct cancellation.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoHttpApiClient.cs:134-159` -- replace both `NotImplementedException` methods with bounded typed HTTP create, identity reconciliation, repository observation, and exact branch/protection validation.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoRepository{Creation,Binding}{Request,Result}.cs` -- carry short-lived `ProviderRepositoryResolvedTarget` and canonical identity; never carry opaque locator substitutes.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoApiFailureCondition.cs`, `ForgejoFailureMapper.cs` -- express cancellation-before-dispatch, default/ref/permission conflicts, ambiguity, and safe retry/remediation distinctly.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoHttpApiClientFactory.cs`, `FoldersServiceCollectionExtensions.cs:174-277` -- normalize injectable Forgejo composition and managed HTTP lifetime alongside GitHub without duplicate registrations.
- `src/Hexalith.Folders/Providers/Abstractions/IProviderRepositoryTargetResolver.cs`, `ProviderRepositoryResolvedTarget.cs`, provider request/result/admission records -- reuse unchanged unless authentic Forgejo evidence proves a provider-neutral gap.
- `tests/contracts/forgejo/`, `ForgejoSupportedVersionCatalog.cs`, `docs/contract/provider-compatibility-catalog.md` -- bind current `16.0.3` stable and `15.0.7` LTS operation shapes; retain retired fixtures only as non-supported evidence.
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/` -- existing façade/drift guards plus new concrete recording-handler transport and DI composition coverage.
- `tests/tools/run-forgejo-provider-evidence-gates.ps1` -- new opt-in, credential-gated, metadata-only live evidence runner; never enter hermetic PR gates.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- orchestrator-owned read-only boundary.

## Tasks & Acceptance

**Execution:**
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs` and `ForgejoRepository{Creation,Binding}{Request,Result}.cs` / `ForgejoApiFailureCondition.cs` / `ForgejoFailureMapper.cs` -- enforce boundary/admission/target/credential/client ordering, exact replay semantics, canonical identity, safe failures, and phase-correct cancellation.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoHttpApiClient.cs` and `ForgejoHttpApiClientFactory.cs` -- implement escaped, bounded create/bind/ref HTTP behavior with one-mutation and managed-lifetime guarantees.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` -- inject the configured Forgejo credential, API client, and target resolver through one canonical singleton registration.
- `tests/contracts/forgejo/**`, `src/Hexalith.Folders/Providers/Forgejo/ForgejoSupportedVersionCatalog.cs`, `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoManifestAndDriftTests.cs`, and `docs/contract/provider-compatibility-catalog.md` -- replace path-only proof with reviewed `16.0.3`/`15.0.7` operation-shape evidence and fail closed on unsupported/drifted profiles.
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs`, new `ForgejoHttpApiClientTests.cs`, provider DI/dependency tests, and `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs` -- cover every matrix row, exact request counts, restart-safe results, and sensitive sentinels.
- `tests/tools/run-forgejo-provider-evidence-gates.ps1` and `docs/operations/provider-integration-and-testing.md` -- add a non-default external evidence lane that emits metadata-only results for operator completion.

**Acceptance Criteria:**
- Given any denied, stale, malformed, reserved-tenant, conflicting, expired, or replayed request, when create/bind begins, then precedence is deterministic and target/secret/client/provider record zero calls unless the exact prior safe terminal result is replayed.
- Given a fresh authorized target, when the concrete Forgejo transport creates or validates a repository, then only one eligible mutation occurs and canonical identity plus exact default/selected branch, visibility, permission, and protection evidence determine the provider-neutral result.
- Given any provider error or ambiguous mutation boundary, when results and emitted evidence are inspected, then stable category/retry/reconciliation semantics are preserved without raw target, credential, payload, URL, or exception leakage and no blind mutation retry occurs.
- Given supported Forgejo profiles, when hermetic contract and transport suites run, then each accepted version proves the used request/response/branch surfaces and unsupported or drifted versions fail closed.
- Given production services are composed, when Forgejo is resolved through `IGitProvider`, then the configured credential resolver, managed HTTP client factory, and fail-closed target resolver are used exactly once.
- Given an approved isolated HTTPS Forgejo deployment and scoped credential references, when the opt-in live evidence lane runs, then positive, denial, conflict, replay, known-failure, timeout/unknown, cancellation, tenant-isolation, and boundary scenarios produce archived metadata-only evidence without destructive retry.

## Spec Change Log

## Review Triage Log

## Design Notes

Forgejo reuses the provider-neutral authorized target and result records, but its HTTP status, version, redirect, permission, and branch-protection evidence stays Forgejo-private. A create conflict permits one read-only identity check; that observation can prove equivalence or conflict but never authorizes a second mutation.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug -m:1` -- expected: zero warnings/errors.
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoProviderTests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoHttpApiClientTests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoManifestAndDriftTests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoDependencyGuardTests` -- expected: all focused tests pass.
- `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj --configuration Debug -m:1` and run its built assembly -- expected: provisioning handoff/restart tests pass.
- `pwsh ./tests/tools/run-provider-error-docs-gates.ps1 -SkipRestoreBuild` -- expected: metadata-only provider catalog gate passes.
- `dotnet build Hexalith.Folders.slnx --configuration Debug -m:1` -- expected: zero warnings/errors.
- `git diff --check` -- expected: clean.
