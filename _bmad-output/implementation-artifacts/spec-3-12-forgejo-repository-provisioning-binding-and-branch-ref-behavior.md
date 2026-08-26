---
title: 'Story 3.12: Forgejo repository provisioning, binding, and branch/ref behavior'
type: 'feature'
created: '2026-08-26'
status: 'in-progress'
baseline_revision: '485fcf38181a1ea8f2b643940a8751b433e66f6f'
baseline_commit: '84243a4b1853471cd146dad062393fb70c170b73'
review_loop_iteration: 2
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
- `src/Hexalith.Folders/Providers/Abstractions/ProviderIdempotencyAdmission.cs`, GitHub/Forgejo admission validators, and repository result records -- extend provider-neutral replay evidence with the exact prior success disposition and canonical repository identity while retaining the legacy eleven-parameter constructor; validate or reject the new companions coherently in every GitHub and Forgejo consumer so source, binary, and behavior compatibility do not silently diverge. This remains outside OpenAPI, generated SDK, and parity wire surfaces.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoRepository{Creation,Binding}{Request,Result}.cs` -- carry short-lived `ProviderRepositoryResolvedTarget` and canonical identity; never carry opaque locator substitutes.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoApiFailureCondition.cs`, `ForgejoFailureMapper.cs` -- express cancellation-before-dispatch, default/ref/permission conflicts, ambiguity, and safe retry/remediation distinctly.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoHttpApiClientFactory.cs`, `IForgejoApiClient.cs`, and `FoldersServiceCollectionExtensions.cs:174-277` -- normalize injectable Forgejo composition, dispose per-operation credential-bearing clients while retaining pooled handlers, and preserve explicit custom Forgejo instances and factories without changing non-Forgejo provider precedence.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoReadinessMapper.cs` and readiness evidence records -- validate the `/user` identity document, distinguish authenticated platform support from target-scoped permission, advertise create/bind/ref as partial until the concrete target call proves permission, expose public/private creation limits, and keep Story 3.13 file/commit/status capabilities unavailable.
- `src/Hexalith.Folders/Providers/Abstractions/IProviderRepositoryTargetResolver.cs` and `ProviderRepositoryResolvedTarget.cs` -- reuse the opaque resolver seam, but validate every resolved owner, repository, visibility, default branch, selected branch, protection, permission, equivalence, and expected canonical-id field at the private Forgejo boundary.
- `tests/contracts/forgejo/`, `ForgejoSupportedVersionCatalog.cs`, `tests/tools/run-nightly-drift-gates.ps1`, and `docs/contract/provider-compatibility-catalog.md` -- retain the authentic full upstream artifact for each supported version, derive the used-operation projection deterministically, recompute source/projection/manifest hashes, and bind every runtime catalog field to the manifest.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` -- make Forgejo normalization idempotent across repeated readiness and Dapr registration, preserve supported singleton instance/factory customizations before adding aliases, reject incompatible lifetimes without capturing them, and preserve non-Forgejo precedence.
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs` -- consume an explicitly persisted Forgejo product version, reject distinct conflicting version aliases across policy dictionaries, and preserve missing authorized-base-URL evidence; when legacy metadata is absent or ambiguous, fail closed with operator revalidation guidance instead of guessing.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs` -- keep Story 3.14 orchestration unchanged while making any boundary failure reason provider-neutral or selected-provider correct.
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/` -- existing façade/drift guards plus new concrete recording-handler transport and DI composition coverage.
- `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoLiveEvidenceTests.cs` and `tests/tools/run-forgejo-provider-evidence-gates.ps1` -- new opt-in, credential-gated lane that resolves the production `IGitProvider` registration and drives the real Forgejo transport; the PowerShell wrapper performs no substitute vendor implementation and never enters hermetic PR gates.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- orchestrator-owned read-only boundary.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Folders/Providers/Abstractions/ProviderIdempotencyAdmission.cs`, GitHub/Forgejo admission validators, repository result records, and compatibility tests -- retain the legacy eleven-parameter constructor, carry and validate exact prior success disposition/canonical identity, make GitHub repository replay preserve or explicitly reject the new companions, reject them on unrelated file/commit admissions, and preserve zero-touch replay/conflict/expiry ordering without changing public wire contracts.
- [ ] `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs` and `ForgejoRepository{Creation,Binding}{Request,Result}.cs` / `ForgejoApiFailureCondition.cs` / `ForgejoFailureMapper.cs` -- enforce null-safe boundary/admission/target/credential/client ordering, safe resolver and credential exception mapping, canonical identity on every success path, exact category/reason/retry/remediation mapping, and phase-correct cancellation.
- [ ] `src/Hexalith.Folders/Providers/Forgejo/ForgejoHttpApiClient.cs` and `ForgejoHttpApiClientFactory.cs` / `IForgejoApiClient.cs` -- implement escaped and bounded create/bind/ref HTTP behavior; immediately preflight the exact live `/version` before create or bind access; send the exact configured default branch without implicit initialization; validate owner/name/id/visibility/default and selected branches/permissions/protection; cross-check every success against equivalence authorization and expected identity; authorize the one post-conflict observation; classify every readiness/observation response without collapsing it to conflict or unknown mutation; reject dot-segment protection names and every unsafe 3xx; and use a deterministic non-hanging disposal contract for credential-bearing clients while pooling handlers.
- [ ] `src/Hexalith.Folders/Providers/Forgejo/ForgejoReadinessMapper.cs` and readiness transport -- parse and validate bounded JSON identity evidence from `/user`, prove authentication before `/version`, map read-only transport failures as unavailable/readiness failures, advertise create/bind/ref as target-permission-dependent partial support with public/private visibility limits, and keep Story 3.13 file/commit/status operations unavailable.
- [ ] `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` -- normalize configured Forgejo dependencies idempotently across repeated readiness and Dapr calls, preserve supported singleton instance/factory customizations before aliases are installed, reject scoped/transient concrete providers, preserve non-Forgejo precedence, and keep the default target resolver fail closed.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs` and `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs` -- remove guessed Forgejo versions, reject distinct conflicting version aliases, preserve missing authorized-base-URL evidence for provider revalidation, and remove provider-specific GitHub failure labels while leaving Story 3.14 terminal orchestration out of scope.
- [ ] `tests/contracts/forgejo/**`, `src/Hexalith.Folders/Providers/Forgejo/ForgejoSupportedVersionCatalog.cs`, `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoManifestAndDriftTests.cs`, `tests/tools/run-nightly-drift-gates.ps1`, and `docs/contract/provider-compatibility-catalog.md` -- replace self-referential path/hash proof with retained upstream `16.0.3`/`15.0.7` artifacts, deterministic used-operation projections, recomputed hashes, complete catalog/manifest conformance including `/user`, and negative drift evidence.
- [ ] `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs`, new `ForgejoHttpApiClientTests.cs`, GitHub admission compatibility tests, provider DI/dependency tests, and focused aggregate/worker boundary tests -- cover every matrix and review row at both transport and public provider boundaries, including live-version mismatch before dispatch, both replay equivalence values for create/bind, every allowed failure tuple, success/target contradictions, conflict policy guards, `/user` failures, canonical-id propagation, public/private/internal visibility, throwing/null credential/target seams, discovery/create/bind reserved/null boundaries, readiness/create/bind disposal, missing/conflicting version and base-URL metadata, repeated/Dapr/custom DI registration, and sensitive sentinels.
- [ ] `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoLiveEvidenceTests.cs`, `tests/tools/run-forgejo-provider-evidence-gates.ps1`, and `docs/operations/provider-integration-and-testing.md` -- add a non-default external lane that deletes any stale report before execution, fails on skipped/zero tests, invokes the production-composed canonical `IGitProvider` over the real Forgejo transport, distinguishes live-provider observations from hermetic admission evidence, proves the isolation credential is valid before it is denied the protected target, and requires a newly written report with exact scenario set, per-scenario pass/evidence class, supported versions, diagnostic policy, schema, and elapsed duration.

**Acceptance Criteria:**
- Given any denied, stale, malformed, reserved-tenant, conflicting, expired, or replayed request, when create/bind begins, then precedence is deterministic and target/secret/client/provider record zero calls unless the exact prior safe terminal result is replayed.
- Given an equivalent replay of success, unknown, or known failure, when create/bind begins, then the exact prior disposition, equivalence flag, canonical repository identity, safe operation/reconciliation references, category, reason, retry, and remediation tuple is reproduced or rejected as malformed with zero downstream calls.
- Given the shared admission record is consumed by an already-compiled or GitHub path, when the new Forgejo replay companions are absent or present, then the legacy constructor remains bindable and each GitHub repository/file/commit validator preserves or rejects those fields coherently rather than silently changing replay behavior.
- Given a fresh authorized target, when the concrete Forgejo transport creates or validates a repository, then only one eligible mutation occurs; creation sends the exact default branch and public/private visibility; and exact owner, name, numeric identity, default/selected branch, permission, and protection evidence determine the provider-neutral result.
- Given the configured Forgejo version changed after readiness evidence was produced, when create or bind reaches the real transport, then one bounded live version preflight rejects the mismatch before any mutation or protected repository observation.
- Given a create conflict, when equivalent-existing observation is not expressly authorized, then no observation follows; when it is authorized, exactly one read-only observation preserves authentication, concealment, redirect, rate-limit, availability, malformed-body, timeout, and cancellation semantics and proves the complete intended identity/policy before equivalence.
- Given any provider error, throwing/null dependency, or ambiguous mutation boundary, when results and emitted evidence are inspected, then stable category/reason/retry/reconciliation semantics are preserved without raw target, credential, payload, URL, or exception leakage and no blind mutation retry occurs.
- Given supported Forgejo profiles, when hermetic contract and transport suites run, then retained upstream artifacts and deterministic used-operation projections prove every Story 3.12 request/response/branch surface, the runtime catalog matches the complete manifest, and unsupported, guessed, or drifted versions fail closed.
- Given readiness succeeds, when the capability profile is inspected, then bounded `/user` identity proves authentication, create/bind/ref support is explicitly partial pending target-scoped permission, public/private creation limits are explicit, and Story 3.13 file/commit/status operations remain unavailable.
- Given production services are composed, when Forgejo is resolved through `IGitProvider`, then the configured credential resolver, managed/disposed HTTP client factory, and fail-closed target resolver are used exactly once while supported custom registrations and non-Forgejo precedence remain intact.
- Given an approved isolated HTTPS Forgejo deployment and distinct scoped credential references, when the opt-in live evidence lane runs through the production-composed `IGitProvider`, then stale/partial/skipped reports cannot pass; positive, denial, conflict, replay, known-failure, timeout/unknown, cancellation, tenant-isolation, and boundary scenarios each produce fresh typed metadata-only evidence without destructive retry.

## Spec Change Log

- 2026-08-26: Review iteration 1 found that the first plan could return non-exact replay success, perform or collapse an unauthorized conflict observation, ignore default-branch and owner identity evidence, overclaim unproved Story 3.13 capabilities, leak dependency/lifetime failures across the boundary, prove only component or direct-vendor behavior instead of the production-composed canonical port, accept self-referential contract hashes, and guess a legacy Forgejo version. The specification now extends the internal replay evidence, defines the complete conflict/identity/capability/transport matrix, requires disposable pooled clients and safe dependency mapping, binds authentic retained upstream artifacts to deterministic projections and the runtime catalog, fails closed on missing version evidence, and makes the credential-gated lane execute the production `IGitProvider`. The known-bad state to avoid is any replay that loses canonical identity, any unapproved or misclassified post-conflict read, a created repository with the wrong default branch or owner, a readiness profile that claims later-story operations, a green test suite over only internal enums/fakes/direct vendor calls, or a manifest whose hashes attest only to copied literals. KEEP: preserve admission-before-target-before-secret ordering, opaque target resolution, canonical numeric identity, one-mutation/no-blind-retry behavior, bounded JSON and same-origin redirect protections, private version-specific Forgejo DTOs, managed named-HTTP composition, metadata-only diagnostics and live evidence, the reviewed `16.0.3` and `15.0.7` operation shapes, the Story 3.13/3.14 boundaries, unchanged public OpenAPI/SDK/parity contracts, and the orchestrator-owned sprint file untouched.
- 2026-08-26: Review iteration 2 found that the repaired plan still trusted stale caller version metadata at mutation time, changed a shared public record without legacy-constructor or GitHub-consumer compatibility, described an authentication probe as target permission, could discard custom Forgejo DI on repeated/Dapr normalization, accepted conflicting persisted version aliases, and allowed stale or partial live reports to masquerade as complete evidence. The specification now requires an exact live version preflight before create/bind access, legacy binary/source plus GitHub admission compatibility, authenticated-but-partial readiness semantics, idempotent lifetime-safe registration normalization, conflict-aware metadata extraction, and new-report/exact-scenario enforcement in the operator runner. The known-bad state to avoid is a mutation after an unsupported server upgrade, a `MissingMethodException` or contradictory GitHub replay caused by shared admission fields, a low-privilege token advertised as target-ready, repeated registration replacing a custom provider, arbitrary alias order selecting a version, or an old/skipped/partial JSON report passing the live gate. KEEP: retain every iteration-1 ordering, identity, transport, provenance, and scope boundary; retain full upstream `16.0.3`/`15.0.7` artifacts and deterministic projections; retain exact Forgejo replay tuples, authorized conflict observation, honest Story 3.13 exclusions, provider-neutral worker evidence, production-composed live execution, metadata-only outputs, and the untouched orchestrator sprint file.

## Review Triage Log

### 2026-08-26 — Review pass (iteration 1)
- intent_gap: 0
- bad_spec: 9: (high 8, medium 1, low 0)
- patch: 7: (high 3, medium 3, low 1)
- defer: 0
- reject: 5: (high 0, medium 3, low 2)
- addressed_findings:
  - `[high]` `[bad_spec]` Extended replay evidence and validation so success, unknown, and known-failure replays preserve exact canonical identity and coherent companion tuples before every downstream seam.
  - `[high]` `[bad_spec]` Defined authorization, complete identity/policy validation, and phase-specific response handling for the single allowed post-conflict observation.
  - `[high]` `[bad_spec]` Required creation to send and verify the exact default branch, owner, numeric identity, visibility, and branch policy rather than accepting a partial repository shape.
  - `[high]` `[bad_spec]` Limited readiness to credential-scoped Story 3.12 capabilities and explicit visibility constraints instead of claiming unimplemented file/commit/status support.
  - `[high]` `[bad_spec]` Closed resolver, credential, client, response-body, cancellation, and credential-bearing client lifetime boundaries with sanitized deterministic outcomes.
  - `[high]` `[bad_spec]` Moved external evidence from a direct PowerShell vendor implementation to a production-composed canonical `IGitProvider` live lane and anchored hermetic tests at the same public provider surface.
  - `[high]` `[bad_spec]` Replaced self-referential contract proof with retained upstream artifacts, deterministic operation projections, recomputed hashes, complete manifest/catalog conformance, and negative drift tests.
  - `[high]` `[bad_spec]` Removed guessed legacy Forgejo versions and required fail-closed operator revalidation when persisted version evidence is absent.
  - `[medium]` `[bad_spec]` Defined Forgejo-only registration normalization that preserves supported custom instances/factories and all non-Forgejo provider precedence.

### 2026-08-26 — Review pass (iteration 2)
- intent_gap: 0
- bad_spec: 7: (high 5, medium 2, low 0)
- patch: 12: (high 5, medium 6, low 1)
- defer: 0
- reject: 4: (high 0, medium 3, low 1)
- addressed_findings:
  - `[high]` `[bad_spec]` Required an exact live Forgejo version preflight before create mutation or protected bind observation so stale readiness metadata cannot authorize an unsupported upgraded server.
  - `[high]` `[bad_spec]` Preserved the shared admission record's legacy constructor and required every GitHub and Forgejo consumer to handle the new replay companions coherently.
  - `[high]` `[bad_spec]` Made Forgejo registration normalization idempotent and lifetime-safe across repeated readiness, Dapr, custom factory, and alias composition.
  - `[high]` `[bad_spec]` Required the live runner to reject stale, skipped, zero-test, partial-scenario, or weakly typed reports and to record versions and elapsed duration.
  - `[high]` `[bad_spec]` Expanded verification to exact replay dispositions, conflict policy guards, credential dependency failures, disposal, live-version drift, discovery boundaries, and success/target contradictions at public seams.
  - `[medium]` `[bad_spec]` Distinguished bounded credential authentication from target-scoped permission so readiness advertises partial Story 3.12 support instead of overclaiming.
  - `[medium]` `[bad_spec]` Rejected conflicting persisted version aliases and preserved missing authorized-base-URL evidence for deterministic operator revalidation.

## Design Notes

Forgejo reuses the provider-neutral authorized target and result records, but its HTTP status, version, redirect, permission, and branch-protection evidence stays Forgejo-private. A create conflict permits one read-only identity check; that observation can prove equivalence or conflict but never authorizes a second mutation.

For this story, deployed composition means production service registration resolves the canonical `IGitProvider` to `ForgejoProvider`, backed by the configured credential resolver, target resolver, and named HTTP factory, and the same composition can execute against the real Forgejo transport in the operator lane. Story 3.14 retains subscription, scheduling, reconciliation, and terminal folder-state ownership; Story 3.13 retains file mutation, commit, and status behavior.

Patch findings carried into re-derivation: reject null evidence without throwing; validate canonical identity on `ExistingEquivalent`; map every new failure at the public provider boundary; reject dot-segment protection names and all unsafe redirects; catch body-stream transport failures; use the selected provider rather than a hard-coded GitHub cancellation reason; and add direct assertions for binding identity, visibility, throwing resolvers, reserved tenant, and zero-call ordering.

Iteration-2 patch findings carried into re-derivation: validate the complete `/user` JSON shape and map read-only transport failures as unavailable; allow exact replay of every fresh-operation failure code; cross-check every successful client result against target equivalence authorization and expected identity; make credential-bearing client disposal non-hanging; include `/user` in contract-operation coverage; test both replay equivalence values and all conflict guards; prove throwing/null credential seams, discovery boundaries, readiness/binding disposal, missing base URL, and revalidation reason codes; and distinguish live-provider observations from hermetic boundary scenarios in the archived report.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug -m:1` -- expected: zero warnings/errors.
- `tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoProviderTests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoHttpApiClientTests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoManifestAndDriftTests -class Hexalith.Folders.Tests.Providers.Forgejo.ForgejoDependencyGuardTests` -- expected: all focused tests pass.
- `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj --configuration Debug -m:1` and run its built assembly -- expected: provisioning handoff/restart tests pass.
- `pwsh ./tests/tools/run-provider-error-docs-gates.ps1 -SkipRestoreBuild` -- expected: metadata-only provider catalog gate passes.
- `dotnet build Hexalith.Folders.slnx --configuration Debug -m:1` -- expected: zero warnings/errors.
- `git diff --check` -- expected: clean.
