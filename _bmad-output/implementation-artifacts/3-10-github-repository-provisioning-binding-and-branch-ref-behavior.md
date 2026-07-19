---
baseline_commit: 5edccc3d3fed4f5fc239415e50268fb789a80c32
---

# Story 3.10: GitHub repository provisioning, binding, and branch/ref behavior

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want GitHub repository provisioning, existing-repository binding, and branch/ref behavior to execute through the canonical provider port,
so that a tenant folder can use GitHub without provider-specific leakage, duplicate bindings, or ambiguous retry behavior.

## Terms and Authority

- Story 3.10 is introduced by the approved 2026-07-14 implementation-readiness structural correction and amended by the 2026-07-15 authority reconciliation. The current `epics.md` ends Epic 3 at Story 3.9, so this dedicated story file becomes the authoritative Story 3.10 definition until the planning manifest and epics inventory are reconciled. Split stories do not inherit the historical `done` status of Story 3.3.
- An **opaque reference** such as `repositoryProfileRef`, `externalRepositoryRef`, or `branchRefPolicyRef` is an authorization-scoped identifier. It is never a GitHub owner, repository name, URL, branch, tag, ref, or other provider locator.
- A **resolved target** is the short-lived, internal result of resolving authorized opaque references and approved tenant naming/ref policy after authorization succeeds. Raw owner, repository, endpoint, default-branch, selected-ref, and protection values remain in memory only for the provider call and must not be stored in events, durable process state, public results, logs, traces, metrics, fingerprints, or exceptions.
- **Equivalent existing** means the canonical GitHub repository identity and the originating operation or separately authorized binding intent both prove that the existing provider state is the exact intended state. Name similarity, case folding, redirect/rename behavior, or a GitHub `already exists` response alone is insufficient.
- **Known failure** means GitHub supplied enough trustworthy evidence to select a stable canonical failure category. **Unknown provider outcome** means a mutation may have applied but its result is not proven; it requires reconciliation and must never trigger a blind second mutation.
- Story 3.10 owns the GitHub provider-side create, bind, canonical-identity, duplicate/alias, and branch/ref validation slice. Story 3.8 owns tenant-admin policy configuration, Story 3.11 owns GitHub file/commit/status behavior, and Story 3.14 owns runtime subscription, durable asynchronous orchestration, and the final folder binding transition.

## Acceptance Criteria

1. Given a tenant administrator has configured an approved GitHub binding, opaque credential reference, repository naming/default-ref/capability policy, current readiness evidence, and the caller has create or bind authority, when a Story 3.10 operation begins, then authentication, managed-tenant/organization/folder/provider-binding authorization, evidence scope/freshness, and input validation complete before target resolution, credential resolution, client construction, repository/ref lookup, GitHub observation, or audit side effects; every denied, stale, malformed, reserved-tenant, or tenant-mismatched path proves zero provider and secret-store calls.
2. Given authorization succeeds, when repository or branch/ref input is needed, then a focused internal resolver converts only authorized opaque references and approved policy into a typed GitHub target after authorization; `repositoryProfileRef`, `externalRepositoryRef`, and `branchRefPolicyRef` are never parsed or forwarded as owner/repository/ref values, raw target material is bounded to the provider call, and returned/persisted evidence contains only canonical provider-neutral identifiers and safe fingerprints.
3. Given an authorized creation request resolves to an approved organization, repository name, visibility, and default-ref policy and GitHub accepts the operation, when `IGitProvider.CreateRepositoryAsync` executes through the real Octokit-backed seam, then exactly one repository-creation mutation is sent with the pinned product/API profile, the returned GitHub identity is normalized to a canonical safe fingerprint/result, no provider DTO escapes `Providers/GitHub`, and provider success remains a non-terminal handoff for Story 3.14. The adapter must not implicitly create a README, license, `.gitignore`, initial commit, or alternate default branch unless the approved OQ4 profile explicitly requires that behavior.
4. Given an authorized existing-repository target, when `IGitProvider.ValidateRepositoryBindingAsync` executes, then the adapter verifies access, immutable/canonical GitHub repository identity, required permission posture, default branch, selected branch/ref, and configured policy compatibility before returning a canonical binding result; missing and access-hidden repositories remain externally indistinguishable, and raw owner/repository/ref or GitHub response data is not returned.
5. Given a repository already exists, redirects after rename, differs only by a provider alias/case rule, or is already bound, when provisioning or binding evaluates it, then canonical GitHub identity is used to distinguish equivalent replay/authorized alias from duplicate or conflicting intent; only proven equivalent state returns `EquivalentExisting`, while every unproven or differently bound target returns a stable safe conflict without exposing protected identity or attaching to it implicitly.
6. Given Story 3.8's resolved tenant-admin-owned branch/ref policy, when GitHub compatibility is evaluated, then the adapter checks the exact default and selected branch/ref, required capability, and protection posture using explicit branch/ref semantics; it distinguishes missing ref, unsupported operation, insufficient contents permission, insufficient administration permission, policy conflict, and temporarily unavailable evidence without treating a policy reference as a raw branch name or using a prefix match as an exact ref match.
7. Given an idempotency key is unexpired, when equivalent intent is replayed, then the same logical result is returned without a second GitHub mutation; conflicting intent returns the canonical idempotency conflict before provider access and without prior-intent disclosure; an expired key returns `idempotency_key_expired` without execution. A provider-side existing repository is accepted as replay success only when canonical identity and operation-ownership/binding-intent evidence prove equivalence.
8. Given GitHub proves invalid input, invalid credentials, insufficient permissions, concealed/not-found target, duplicate/validation conflict, branch/ref or protection conflict, primary rate limit, secondary/abuse rate limit, service unavailability, or another known condition, when the adapter maps the response, then it selects the existing stable `ProviderFailureCategory`, preserves only safe retry-after/remediation evidence, keeps authentication/authorization/concealment semantics distinct where the public boundary permits, and excludes provider response bodies, URLs, identities, and raw exception text from every output channel.
9. Given a mutating GitHub call is cancelled, times out, disconnects, or produces malformed/ambiguous evidence after dispatch may have occurred, when the adapter cannot prove application or non-application, then it returns `unknown_provider_outcome` with a safe operation identity and reconciliation requirement, does not automatically retry or issue a second create, and documents the Octokit cancellation limitation rather than claiming upstream cancellation. Cancellation before dispatch performs no mutation.
10. Given a provider operation can outlive a worker process, when Story 3.10 returns success, known failure, equivalent existing, conflict, or unknown outcome, then provider-neutral operation identity, result category, retryability, and sanitized reconciliation evidence can be represented in durable/restart-safe models without credentials, raw targets, Octokit objects, or provider payloads; restart does not cause a blind mutation. Story 3.14 remains responsible for subscribing, persisting the full process lifecycle, reconciliation scheduling, and publishing the final folder binding status.
11. Given any authorized success, denial, replay, conflict, known failure, or unknown outcome, when audit, support, readiness, or diagnostic evidence is emitted, then it is C9-safe and metadata-only: tenant, actor, task/operation/correlation, provider/binding safe references, capability/result category, timing, retryability, and sanitized reason may be present; token/credential values, owner/repository/ref labels, URLs, response bodies, file content, and hidden existence are absent. Story 3.9 remains a stored-evidence query and must not be changed to make live provider calls.
12. Given GitHub behavior assumptions are used, when this story is implemented, then the Octokit `14.0.0` and REST `2022-11-28` profiles, explicit `X-GitHub-Api-Version` header, accepted credential modes, permission/capability assumptions, creation defaults, alias semantics, and reconciliation policy are pinned and testable. The absent `docs/contract/provider-compatibility-catalog.md` and unresolved OQ4 remain release/provider-ready acceptance blockers; this story must not self-approve the catalog or advertise full GitHub readiness, and an API-version upgrade is a deliberate compatibility change rather than incidental work.
13. Given implementation exposes the canonical provider port, when a public contract gap is found, then the OpenAPI Contract Spine, current C13 inventory, parity and previous-spine fixtures, generated SDK, docs, and tests change together through the generation workflow; otherwise public contracts remain unchanged. Octokit types, GitHub endpoint shapes, raw target data, and provider-specific failure payloads never cross the core/public boundary.
14. Given Story 3.10 tests run in the PR gate, then focused hermetic tests cover authorized real-path creation/binding/ref success, denied no-touch behavior, secure target resolution, safe 404/concealment, duplicate and alias handling, equivalent/conflicting/expired replay, default/protected/missing refs, 400/401/403/404/409/422/429/5xx and both rate-limit postures, cancellation before dispatch, timeout/ambiguity after dispatch, restart-safe result evidence, no blind retry, explicit headers, and sensitive-sentinel exclusion. Tests exercise the concrete Octokit adapter through a fake `IConnection`/transport or equivalent network-free seam; optional live/nightly drift checks are credential-gated, non-secret-leaking, and non-mutating unless their environment is explicitly provisioned for mutation. Full provider-ready status also requires Story 3.11 and OQ4 acceptance.

## Tasks / Subtasks

- [ ] Reconcile Story 3.10 authority, compatibility assumptions, and public-contract impact before implementation. (AC: 12, 13)
  - [ ] Record that the 2026-07-14 structural correction as amended by 2026-07-15 is the source for this split story; do not infer completion from historical Story 3.3.
  - [ ] Confirm the narrowed Story 3.3 production readiness path is operational before claiming Story 3.10 real-path acceptance. `OctokitGitHubApiClient.GetReadinessAsync` is also currently a stub, but must be fixed under Story 3.3 or an explicitly approved scope change rather than silently absorbed here.
  - [ ] Confirm the current Contract Spine can carry the provider-neutral creation/binding outcomes. If a public gap is proven, update the OpenAPI source, generation inputs/outputs, C13 inventory, parity/previous-spine fixtures, docs, examples, and tests as one change; do not hand-edit generated SDK files.
  - [ ] Create or update `docs/contract/provider-compatibility-catalog.md` only as evidence for human OQ4 acceptance. Pin the supported GitHub product/API profile, credential modes, permissions, creation defaults, branch/ref/protection capabilities, rate-limit behavior, alias rules, and reconciliation policy without declaring OQ4 approved.
  - [ ] Keep Octokit at the centrally pinned `14.0.0` and explicitly send `X-GitHub-Api-Version: 2022-11-28`; do not opportunistically adopt the newer REST profile.

- [ ] Add the minimum authorized target and policy-resolution seam. (AC: 1, 2, 11)
  - [ ] Introduce one-public-type-per-file provider-neutral request/result models and a focused resolver interface for creation targets, binding targets, and resolved branch/ref policy. Resolve opaque references only after the existing authorization/evidence gates succeed.
  - [ ] Extend `ProviderRepositoryCreationRequest`, `ProviderRepositoryBindingRequest`, or an adjacent internal model only enough to convey resolved authorized intent to the provider without making raw GitHub locators public or durable.
  - [ ] Keep raw organization, repository, endpoint, default-branch, selected-ref, and protection values in a short-lived in-memory object; never add them to `ProviderTargetEvidence`, safe fingerprints, aggregate events, read models, process state, telemetry, audit, exception messages, or public results.
  - [ ] Preserve credential lease disposal and ensure target resolution, credential resolution, client construction, and provider access all remain after authentication/tenant/action authorization and current evidence validation.

- [ ] Implement real GitHub repository creation through the existing provider seam. (AC: 3, 7, 8, 9, 10, 12)
  - [ ] Retain the supplied `GitHubClient` in `OctokitGitHubApiClient`; construct the exact organization/name/visibility request from the resolved target and approved policy, with no implicit initialization side effects unless OQ4 explicitly approves them.
  - [ ] Extend `GitHubRepositoryCreationRequest`/`Result` and `IGitHubApiClient` only as needed to express provider-internal target intent, canonical identity, equivalent-existing evidence, known failure, retry evidence, and unknown outcome without raw provider DTO leakage.
  - [ ] Reconcile `already exists` with a read-only canonical-identity check. Return equivalent existing only with operation/binding-intent proof; otherwise return a safe repository conflict.
  - [ ] Distinguish cancellation before dispatch from timeout/cancellation/disconnect after possible dispatch. Do not wrap a non-cancellable high-level Octokit call as though upstream cancellation were guaranteed, and never blindly retry ambiguous creation.
  - [ ] Preserve the provider-neutral result categories consumed by `RepositoryProvisioningProcessManager`; do not wire its missing runtime trigger or final aggregate transition in this story.

- [ ] Implement real GitHub binding, canonical identity, alias, and branch/ref validation. (AC: 4, 5, 6, 8, 11, 12)
  - [ ] Use repository lookup to obtain canonical immutable identity and safe permission/default-branch evidence. Treat redirects, renames, case variants, and aliases by canonical identity rather than input spelling.
  - [ ] Validate the exact selected branch/ref and default-ref policy. Keep branch existence/contents-read capability separate from branch-protection/administration-read capability, and never use matching-prefix results as proof of an exact ref.
  - [ ] Return only provider-neutral binding validity, canonical safe fingerprint, capability/policy status, and safe remediation/retry metadata. Conceal missing/private/inaccessible targets consistently.
  - [ ] Preserve Story 3.8 as the policy-configuration owner and Story 3.7 as the product binding-workflow owner; do not add an alternate GitHub-specific product port or endpoint.

- [ ] Complete canonical failure, replay, reconciliation, and restart-safe evidence behavior. (AC: 7-11)
  - [ ] Extend `GitHubFailureMapper`/condition models for the exact concrete-client evidence required to distinguish validation/conflict, credentials, permissions, concealment, branch policy, primary rate limit, secondary rate limit, unavailable, and unknown outcome while retaining existing canonical categories.
  - [ ] Preserve bounded retry-after metadata for proven retryable failures. A retry recommendation is never authorization to retry an ambiguous mutation.
  - [ ] Ensure replay/conflict/expiry checks occur before target, credential, client, or provider observation and do not reveal prior intent.
  - [ ] Ensure provider-neutral operation/reconciliation evidence can survive serialization/restart and contains no Octokit type, credential, raw target, or response payload; leave durable orchestration and terminal status transitions to Story 3.14.
  - [ ] Audit all success/failure/result `ToString`, exception, logging, metric, trace, and diagnostic paths with sentinel values for owner, repository, branch/ref, URL, token, and provider body leakage.

- [ ] Add focused hermetic tests around both the boundary and the concrete Octokit transport. (AC: 1-14)
  - [ ] Extend `GitHubProviderTests` for authorization/evidence/target/credential ordering, denied zero-touch behavior, target-resolution failure, credential disposal, canonical result mapping, idempotency, alias/duplicate behavior, safe evidence, and unknown-outcome no-retry behavior.
  - [ ] Add `OctokitGitHubApiClientTests` using a fake Octokit `IConnection`, HTTP transport, or equivalent deterministic seam to prove request method/path/body, product/accept/auth/API-version headers, organization/name construction, success parsing, canonical identity, exact branch/ref/protection behavior, and no real network access.
  - [ ] Cover 400, 401, ordinary 403, primary-limit 403/429, secondary-limit 403/429, 404 concealment, 409/422, 5xx, malformed response, cancellation before dispatch, timeout/disconnect after dispatch, equivalent-existing reconciliation, conflicting-existing, and sensitive sentinels.
  - [ ] Add restart/round-trip tests for provider-neutral operation/reconciliation evidence and prove no second mutation occurs after equivalent replay or ambiguous outcome.
  - [ ] Run the narrow GitHub provider/core/worker/contract tests first, then the relevant solution gates. Keep default PR validation offline; document any optional live drift evidence separately.

## Dev Notes

### Source Context and Scope

- Epic 3's product outcome is provider configuration/readiness, repository-backed folder creation or binding, branch/ref policy, and safe capability evidence. PRD FR15 gives tenant administrators ownership of provider binding, credential reference, naming/default-ref, and capability policy. FR18-FR20 require authorized create/bind/ref behavior after readiness, access, duplicate/alias, and policy checks; FR21-FR23 require safe metadata and explicit provider differences/evidence. FR41-FR42 apply replay/conflict/expiry behavior to every mutation. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 3: Provider Readiness And Repository Binding`; `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`; `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- The approved structural correction narrows historical Story 3.3 to GitHub discovery/readiness and assigns GitHub creation/binding/ref behavior to 3.10, GitHub file/commit/status behavior to 3.11, Forgejo equivalents to 3.12/3.13, and terminal asynchronous completion to 3.14. Its mutation-story matrix requires real-path success, denial, equivalent/conflicting replay, known failure, unknown/reconciliation, restart state, terminal/retryability, metadata-only audit, and sensitive-data exclusion. [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md#5.11 Epic 3 — provider execution and asynchronous creation completion`; `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md#Acceptance requirements for mutation stories`]
- The 2026-07-15 proposal amends that correction with duplicate/alias binding behavior and tenant-admin policy authority. Its reconciliation report treats OQ1-OQ4 as bounded release blockers rather than a blanket work-start prohibition. OQ4 therefore constrains acceptance/provider-ready claims while implementation may proceed against explicitly pinned assumptions. [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md#4.2 Provider Configuration, Binding, and Readiness — Approved`; `_bmad-output/planning-artifacts/reconcile-implementation-readiness-report-2026-07-15.md`]

### Current Implementation State

- `GitHubProvider` already enforces the canonical provider boundary, credential lease, safe target fingerprinting, and failure wrapping around `IGitHubApiClient`. Preserve that architecture rather than bypassing it with direct Octokit use in aggregates, workers, or endpoints.
- `OctokitGitHubApiClient.GetReadinessAsync`, `CreateRepositoryAsync`, and `ValidateRepositoryBindingAsync` currently throw `NotImplementedException`; the constructor also fails to retain the supplied `GitHubClient`. Story 3.10 implements create and bind/ref only. Readiness remains the narrowed Story 3.3 responsibility and must not be silently absorbed.
- `ProviderRepositoryCreationRequest` and `GitHubRepositoryCreationRequest` do not carry an authorized organization, repository name, visibility, default branch, or resolved ref policy. `ProviderRepositoryBindingRequest` carries `ExternalRepositoryRef` and `BranchRefPolicyRef`, but both are opaque references. This is a required modeling fix: do not reinterpret those identifiers or smuggle raw names through `ProviderTargetEvidence`.
- `ConfigureProviderBindingService` currently records `OrganizationProviderBindingPolicy.Empty` for both naming and branch policy. The implementation must establish an authoritative configured-policy source or explicitly record the upstream prerequisite; it must not invent target values in the adapter.
- `GitHubSafeTargetFingerprint` intentionally rejects owner/repository/branch/ref labels, URLs, and similar raw target data. Keep this invariant. Fingerprints should derive from canonical safe identifiers/evidence, not from a reversible concatenation of GitHub names.
- `OctokitGitHubApiClientFactory` currently applies product and credentials but does not apply the `GitHubProviderConstants.RestApiVersion` header. Story 3.10 closes that gap for the calls it owns.
- `RepositoryProvisioningProcessManager` already calls `IGitProvider.CreateRepositoryAsync` and maps provider-neutral success/failure/unknown results, but its runtime trigger is missing. Do not wire the trigger or claim a terminal folder binding in this provider story; those are Story 3.14.
- Normal creation/binding flows require current readiness, while the production Octokit readiness method is still a Story 3.3 stub. Story 3.10 cannot claim an end-to-end production-path success until that prerequisite is corrected; keep the ownership gap visible rather than weakening or bypassing the gate.
- There are boundary tests using recording GitHub clients, but no concrete `OctokitGitHubApiClient` transport tests. Fake-only boundary evidence cannot prove request construction, headers, response/error classification, or mutation ambiguity in the production adapter.

### Required Architecture Patterns

- Keep provider adapters behind the single provider-neutral `IGitProvider` port. GitHub and Forgejo are separate adapters, not base-URL substitutions. Octokit stays confined to `src/Hexalith.Folders/Providers/GitHub/` and provider tests.
- Preserve authorization-before-observation and metadata-only diagnostics. Never place secrets, credential values, raw references, raw owner/repository/ref labels, endpoint URLs, response bodies, file content, diffs, or provider exceptions in events, results, logs, traces, metrics, audit, diagnostics, snapshots, or test output.
- Keep provider failures in the known-versus-unknown taxonomy. A timeout, disconnect, or cancellation after mutation dispatch is an ambiguous outcome unless reconciliation proves state; no blind retry is allowed.
- Use one public type per file, file-scoped namespaces, nullable-aware C#, ordinal/invariant comparisons, deterministic mappings, async APIs with `CancellationToken`, and centralized package versions. Do not add package versions directly to project files.
- Keep public contracts behavior-free and provider-neutral. Public changes start in the OpenAPI Contract Spine and flow through the repository generation/parity process.
- Keep PR tests hermetic. Provider contract tests may use an in-memory/fake transport; live provider drift belongs in separately gated workflows and must not require secrets for the default gate.

### Library and API Intelligence

- The repository currently pins .NET `net10.0`, SDK `10.0.302`, and Octokit `14.0.0`; repository configuration is authoritative over stale version notes in earlier story files. [Source: `Directory.Build.props`; `global.json`; `references/Hexalith.Builds/Props/Directory.Packages.props`]
- Octokit 14 exposes organization repository creation via `IRepositoriesClient.Create(string organizationLogin, NewRepository)`, repository lookup via `IRepositoriesClient.Get(string owner, string name)`, and exact branch lookup via `IRepositoryBranchesClient.Get(string owner, string name, string branch)`. `Repository` exposes canonical `Id`, `DefaultBranch`, and permission evidence; `Branch` exposes name, commit, and protection posture. Use `dotnet-inspect` again while implementing if a less common overload or exact return type is needed.
- Those high-level Octokit repository/branch methods do not accept `CancellationToken`. Code must not claim cancellation of an already dispatched request; use the lower-level `IConnection`/transport only when necessary and map ambiguous post-dispatch outcomes safely.
- GitHub repository creation requires a name and supports organization-scoped creation. `auto_init` defaults false; leave it false unless the accepted compatibility profile says otherwise. Exact branch/ref checks should use exact lookup semantics rather than matching-ref prefix semantics. [Source: https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28; https://docs.github.com/en/rest/git/refs?apiVersion=2022-11-28]
- GitHub currently supports REST `2022-11-28` and `2026-03-10`; the existing `2022-11-28` pin remains supported until 2028-03-10. Explicitly send the version header and treat any upgrade as compatibility work because the newer profile contains breaking changes. [Source: https://docs.github.com/en/rest/about-the-rest-api/api-versions?apiVersion=2026-03-10; https://github.blog/changelog/2026-03-12-rest-api-version-2026-03-10-is-now-available/]
- Fine-grained token permissions differ by operation: repository creation requires administration write, repository metadata lookup requires metadata read, branch/ref lookup requires contents read, and branch-protection inspection requires administration read. Primary and secondary rate limits can use 403 or 429 and different retry evidence. Pin and test these distinctions in the compatibility catalog. [Source: https://docs.github.com/en/rest/repos/repos?apiVersion=2026-03-10; https://docs.github.com/en/rest/branches/branches?apiVersion=2026-03-10; https://docs.github.com/en/rest/branches/branch-protection?apiVersion=2026-03-10; https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api]

### Previous Story and Git Intelligence

- Story 3.3 established `IGitProvider`, the internal `IGitHubApiClient` seam, credential leases, canonical failure mapping, Octokit confinement, and safe fingerprints. Its review deliberately kept missing live methods loud. Reuse those seams and do not treat the old `done` label as production evidence.
- Story 3.6 owns the authorized asynchronous creation request. It currently appends `RepositoryBindingRequested`; provider exceptions become metadata-only unknown outcomes and must not be retried automatically.
- Story 3.7 owns the product flow for an authorized existing-repository binding. Its review fixed the provider seam so the opaque external target reaches the provider after authorization; 3.10 must now resolve that reference securely and validate the real provider target.
- Story 3.8 owns safe branch/ref policy configuration/read models. Its policy reference is an opaque token, not a branch name, and its review separated branch/ref readiness from repository-creation capability.
- Story 3.9 owns deterministic metadata-only support evidence and must never probe a live provider while serving a read. Do not introduce provider calls into that endpoint or projection.
- Relevant implementation history is `a357545` (Story 3.3 GitHub adapter), `2d17011` (Story 3.6 creation process), `3956209` (Story 3.7 binding workflow), and `ea60698` (Story 3.8 branch/ref policy). The latest workspace commit at story creation is `5edccc3`; recent commits do not implement the live GitHub create/bind path.

### Likely Files to Update

- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryBindingRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryBindingResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/*ProviderRepositoryTarget*.cs` (new focused resolver/models as required)
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryCreationResult.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryBindingRequest.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryBindingResult.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubApiClientRequest.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubApiFailureCondition.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubFailureMapper.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubSafeTargetFingerprint.cs`
- `src/Hexalith.Folders/Providers/GitHub/IGitHubApiClient.cs`
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs`
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClientFactory.cs`
- `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBindingService.cs` and adjacent binding-policy models only if they become the authoritative target/policy source
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBackedFolderCreationService.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningContext.cs` only for safe provider-neutral handoff evidence
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs` only for safe provider-neutral handoff evidence, not runtime trigger wiring
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` only if a resolver registration is needed
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.cs` (new)
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs` only for provider-neutral handoff/restart evidence, not runtime trigger wiring
- `docs/contract/provider-compatibility-catalog.md` (currently absent; human acceptance remains required)
- `docs/operations/provider-integration-and-testing.md` to replace any live-support claim that is not backed by the completed production path
- Contract Spine/generated/parity/inventory artifacts only if Task 0 proves a public contract gap.

### Do Not Touch

- Do not implement or absorb `OctokitGitHubApiClient.GetReadinessAsync`; narrowed Story 3.3 owns live GitHub discovery/readiness closure.
- Do not implement file reads/writes/deletes, commits, commit status, content SHA behavior, or their GitHub failure matrix; Story 3.11 owns them.
- Do not implement Forgejo create/bind/ref or file/commit behavior; Stories 3.12 and 3.13 own them.
- Do not wire the provisioning worker subscription, invent automatic reconciliation scheduling, publish final folder-binding completion, or claim the end-to-end asynchronous workflow is complete; Story 3.14 owns those behaviors.
- Do not change Story 3.8's policy ownership, make scoped platform engineers policy administrators, or expose a GitHub-specific public endpoint/port.
- Do not initialize nested submodules, use recursive submodule operations, update dependencies, or modify unrelated user work.

### Testing

- Use xUnit v3 and Shouldly. Prefer explicit recording fakes and deterministic fake transport over broad mocks. Current shared pins are authoritative.
- Separate boundary tests (`GitHubProvider`) from concrete transport tests (`OctokitGitHubApiClient`) so authorization/credentials/evidence ordering and wire behavior both have proof.
- Hermetic request tests must assert method, path, headers, body defaults, exact owner/name/ref routing, response normalization, error/rate-limit mapping, and absence of a second mutation.
- Add sentinel scans across results, provider evidence, exceptions, logs/test sink, audit, metrics/traces where observable, and serialized restart evidence. Include realistic token, owner, repository, URL, branch/ref, and provider-body sentinels.
- Run focused tests for GitHub provider and provisioning handoff first. If public/provider-neutral models change, also run contract/parity/architecture guards and worker tests. Run `git diff --check` and the narrow build before broader solution checks.

### Project Structure Notes

- The implementation belongs in `src/Hexalith.Folders/Providers/Abstractions/` and `src/Hexalith.Folders/Providers/GitHub/`; product orchestration remains in aggregate/worker code that consumes the provider-neutral port.
- The secure target resolver is a new internal seam required by the mismatch between opaque public references and GitHub's raw owner/name/ref API. Keep it provider-neutral where policy resolution is shared, then translate to GitHub-private DTOs inside the adapter.
- No UI change is expected. UX evidence remains metadata-only, non-enumerating, and explicit about unknown/stale/unavailable states.
- At story creation time the tracked worktree was clean; this story file and sprint-status transition are the only intended changes.

### References

- `_bmad-output/planning-artifacts/epics.md#Epic 3: Provider Readiness And Repository Binding`
- `_bmad-output/planning-artifacts/prd.md#Provider Readiness and Repository Binding`
- `_bmad-output/planning-artifacts/prd.md#Security and Tenant Isolation`
- `_bmad-output/planning-artifacts/architecture.md#Authentication & Security`
- `_bmad-output/planning-artifacts/architecture.md#Implementation Patterns & Consistency Rules`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md#5.11 Epic 3 — provider execution and asynchronous creation completion`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md#4.2 Provider Configuration, Binding, and Readiness — Approved`
- `_bmad-output/planning-artifacts/reconcile-implementation-readiness-report-2026-07-15.md`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/3-3-implement-github-provider-adapter.md`
- `_bmad-output/implementation-artifacts/3-6-create-a-new-repository-backed-folder.md`
- `_bmad-output/implementation-artifacts/3-7-bind-an-existing-repository-to-a-folder.md`
- `_bmad-output/implementation-artifacts/3-8-define-branch-and-ref-policy.md`
- `_bmad-output/implementation-artifacts/3-9-inspect-tenant-and-per-provider-readiness-evidence.md`
- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs`
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClientFactory.cs`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs`
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs`
- `references/Hexalith.Builds/Props/Directory.Packages.props`
- https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28
- https://docs.github.com/en/rest/git/refs?apiVersion=2022-11-28
- https://docs.github.com/en/rest/about-the-rest-api/api-versions?apiVersion=2026-03-10
- https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- https://github.com/octokit/octokit.net/releases

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-07-19 | Created Story 3.10 with the approved GitHub provisioning/binding/ref split, secure target-resolution boundary, duplicate/alias equivalence, unknown-outcome safety, OQ4 acceptance gate, and hermetic concrete-transport verification plan. | Codex |
| 2026-07-19 | Implemented the GitHub create/bind transport slice, secure target resolver seam, compatibility catalog, canonical failure/reconciliation behavior, and hermetic boundary/transport/restart tests. Kept the story in progress because OQ8 expired-key authority and two pre-existing contract traceability failures prevent the completion gates from passing. | Codex |

## Dev Agent Record

### Implementation Plan

- Reconcile the split-story authority, Contract Spine impact, Octokit/API pins, readiness ownership, and OQ4 limits before touching runtime code.
- Add a fail-closed, post-authorization resolver seam that converts opaque repository/policy references into a short-lived provider target without adding raw locators to public or durable models.
- Implement organization repository creation and existing-repository reconciliation through the retained Octokit client, with canonical identity proof and no blind retry after ambiguous dispatch.
- Implement canonical binding, exact default/selected branch, permissions, branch protection, alias/duplicate, concealment, and unsupported-ref behavior through read-only Octokit calls.
- Extend canonical failure mapping, bounded retry evidence, restart-safe results, leakage guards, and deterministic fake-transport coverage before broader repository validation.

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-07-19: Resolved `bmad-create-story` customization; no activation prepend/append or completion steps were configured. Loaded `_bmad/bmm/config.yaml`, sprint status, all discovered planning inputs, project context, repository baseline, relevant tracked configuration, prior Epic 3 stories, current provider/worker source and tests, and Git history.
- 2026-07-19: Canonical `epics.md` has no Story 3.10 rows. Used the approved 2026-07-14 structural correction as amended by the 2026-07-15 authority reconciliation; recorded the missing planning manifest/epics synchronization without treating it as a story-creation blocker.
- 2026-07-19: Parallel requirements, architecture/code, and history/technical analyses were used because the skill requires independent analysis. Findings were reconciled into the acceptance criteria, scope boundaries, task plan, regression traps, and file guidance.
- 2026-07-19: `dotnet-inspect` verified the locally pinned Octokit 14 repository/branch APIs and the absence of high-level cancellation-token overloads. Official GitHub REST sources were checked for the current supported API profiles, creation defaults, permissions, ref behavior, and rate-limit evidence.
- 2026-07-19: Identified the critical model mismatch: creation lacks a GitHub target, while existing repository/policy references are deliberately opaque and `ProviderTargetEvidence` forbids raw target labels. Added a post-authorization, memory-only target/policy resolver as an explicit prerequisite.
- 2026-07-19: The provider compatibility catalog required by OQ4 is absent. Story 3.10 may implement against pinned assumptions but cannot self-approve OQ4 or claim full GitHub provider readiness.
- 2026-07-19: Red/green implementation added the internal authorized-target resolver seam, fail-closed production registration, repository-profile handoff, explicit REST-version header transport, real Octokit create/bind calls, canonical repository identity checks, exact branch/protection validation, rate-limit distinction, bounded retry evidence, and ambiguous-mutation classification.
- 2026-07-19: Concrete fake-transport tests cover request method/path/body/product/accept/auth/version headers, no implicit initialization, equivalent/conflicting existing repositories, canonical aliases, branch/default/protection/permission behavior, 400/401/ordinary 403/404/409/422/429/5xx responses, primary and secondary 403/429 limits, malformed responses, cancellation before dispatch, timeout/disconnect after dispatch, and sentinel exclusion.
- 2026-07-19: Leakage review found the synthesized record `ToString()` exposed raw resolved target values. A failing sentinel test reproduced it; `ProviderRepositoryResolvedTarget.ToString()` is now opaque and the test passes.
- 2026-07-19: Focused GitHub provider/transport tests passed 113/113; full `Hexalith.Folders.Tests` passed 1432/1432; full worker tests passed 80/80; focused provider docs/contract tests passed 20/20; `dotnet build Hexalith.Folders.slnx --no-restore` passed with 0 warnings and 0 errors; `git diff --check` passed.
- 2026-07-19: HALT blocker — AC7 and the replay/expiry subtasks require `idempotency_key_expired` before provider access, but the current authoritative folder ledger exposes only `Missing`, `Found`, and `Unavailable`; no retention tier, expiry timestamp, or consumed-key tombstone exists. OQ8 explicitly owns that unresolved cross-contract/storage decision, so this story cannot safely invent expiry behavior.
- 2026-07-19: HALT blocker — full `Hexalith.Folders.Contracts.Tests` ran 283 tests with 281 passing and two pre-existing traceability failures: `PrdAndEpicsNfrInventoriesAlignOneForOne` expects 70 PRD NFR bullets but finds 73, and `TraceabilityTableHasSeventyRowsMatchingPrdHashes` reports an NFR1 hash mismatch. The failing planning/traceability files are unchanged by this implementation.
- 2026-07-19: `dotnet format Hexalith.Folders.slnx --no-restore --verify-no-changes --verbosity minimal` is not a usable clean gate on the current baseline: it reports extensive pre-existing whitespace/end-of-line/naming diagnostics across root projects and root-declared submodules. Formatting was applied only to the owned changed files; the solution build remains warning-free.
- 2026-07-19: Resume audit at `6dd0b9d` confirmed the OQ8 blocker remains authoritative. The approved 2026-07-15 change fixes expired-key precedence and approves a metadata-only consumed-key tombstone concept, but the PRD still lists OQ8 open until canonical storage/retention evidence and all-mutation tests exist. `IFolderRepository` still exposes only `Missing`, `Found`, and `Unavailable`; adding production expiry now would be an unapproved cross-cutting scope expansion beyond Story 3.10.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story 3.10 file created at `_bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md`.
- Sprint status updated to mark `3-10-github-repository-provisioning-binding-and-branch-ref-behavior` as `ready-for-dev`.
- Validation checklist applied against the authority chain, complete planning inputs, current implementation, previous story learnings, Git history, locally pinned Octokit surface, official GitHub behavior, scope boundaries, and sensitive-data/retry regression traps.
- The story keeps GitHub provider execution in 3.10, policy configuration in 3.8, file/commit/status in 3.11, Forgejo behavior in 3.12/3.13, and terminal asynchronous completion in 3.14.
- Implemented the concrete Story 3.10 provider slice without absorbing Story 3.3 readiness, Story 3.8 policy ownership, Story 3.11 content operations, or Story 3.14 runtime subscription/final transition.
- Added restart/round-trip evidence proving equivalent-existing and ambiguous outcomes do not trigger a second provider mutation after process-manager reconstruction.
- Created the compatibility catalog as pending human evidence only; production target resolution remains intentionally fail-closed until the authoritative Story 3.8 policy source exists.
- Story completion is intentionally withheld: expired-key behavior cannot be implemented until OQ8 defines and exposes canonical retention/tombstone evidence, and the repository-wide contract regression gate is not green.

### File List

- `_bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `docs/contract/provider-compatibility-catalog.md`
- `docs/operations/provider-integration-and-testing.md`
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders/Providers/Abstractions/IProviderRepositoryTargetResolver.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryBindingTargetResolutionRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryCreationTargetResolutionRequest.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryRefKind.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryResolvedTarget.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryTargetResolutionResult.cs`
- `src/Hexalith.Folders/Providers/Abstractions/ProviderRepositoryVisibility.cs`
- `src/Hexalith.Folders/Providers/Abstractions/UnconfiguredProviderRepositoryTargetResolver.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubApiFailureCondition.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubApiVersionHttpClient.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubFailureMapper.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryBindingRequest.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryBindingResult.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryCreationRequest.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubRepositoryCreationResult.cs`
- `src/Hexalith.Folders/Providers/GitHub/GitHubSafeTargetFingerprint.cs`
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs`
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClientFactory.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/RecordedGitHubHttpRequest.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/RecordingGitHubCredentialResolver.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/RecordingGitHubHttpMessageHandler.cs`
- `tests/Hexalith.Folders.Tests/Providers/GitHub/RecordingProviderRepositoryTargetResolver.cs`
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs`
