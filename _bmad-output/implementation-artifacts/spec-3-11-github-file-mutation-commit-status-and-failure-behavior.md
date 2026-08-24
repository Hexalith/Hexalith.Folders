---
title: 'Story 3.11: GitHub file mutation, commit, status, and failure behavior'
type: 'feature'
created: '2026-08-24'
status: 'in-review'
baseline_revision: '02ef9f87e61dd2057ed9a3760ca2a32f86888f5d'
baseline_commit: '02ef9f87e61dd2057ed9a3760ca2a32f86888f5d'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** GitHub capability discovery claims file-mutation, commit, and status support, but the canonical provider port and Octokit adapter expose only readiness and repository create/bind behavior. The deployed workspace path therefore remains fail-closed and later durable orchestration has no real GitHub execution seam to consume.

**Approach:** Add provider-neutral ordered-change-set, explicit-commit, and read-only outcome/status operations to the capability-discoverable provider boundary, then implement the GitHub-private Octokit behavior with exact ref/commit evidence, stable failure mapping, and metadata-safe reconciliation results. Keep durable content/target resolution and worker/process-manager composition fail-closed for Stories 12.3/12.4.

## Boundaries & Constraints

**Always:** Authorize and validate fresh tenant, folder, delegated-task, binding, ref-policy, and canonical lock evidence before target, credential, content, or provider observation; resolve opaque references only into short-lived GitHub-private values; preserve caller mutation order; never move the branch while staging files; allow at most one non-force ref update for an explicit commit; return only provider-neutral safe fingerprints, canonical outcome categories, retry posture, and opaque reconciliation identity. Treat cancellation before dispatch as no effect, possible post-dispatch ambiguity as `unknown_provider_outcome`, and status as read-only/unavailable rather than an unknown mutation. Keep all events, results, exceptions, diagnostics, and test failures free of tokens, raw owner/repository/ref/path/content/message values, diffs, URLs, and provider bodies.

**Block If:** A proven public Contract Spine gap requires new externally observable semantics not settled by the current authority, or implementation would require inventing the durable content/target store, worker lifecycle, or canonical lock decision owned by another story. OQ4 approval and credential-gated live evidence are operator follow-up actions, not blocking conditions for the agent-completable adapter slice.

**Never:** Use GitHub Contents API writes that implicitly commit each file; force-update a ref; blindly retry a mutation or commit after ambiguous dispatch; parse opaque references as GitHub locators; implement GitHub readiness, Forgejo execution, repository creation orchestration, server-side durable content storage, final workspace executor/process-manager wiring, UI/CLI/MCP behavior, or hand-edit generated clients. Never write or revert `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Ordered staging | Authorized exact target/ref plus validated add/change/remove set | Blobs/tree are prepared in requested order without ref movement or implicit commit | Policy/SHA/ref mismatch is a known canonical failure |
| Explicit commit | Known staged set and unchanged expected head | One commit is created and one non-force ref update is confirmed; canonical safe commit evidence is returned | Moved head/protection conflict does not overwrite |
| Equivalent replay | Same live idempotent provider intent | Same logical safe result; no duplicate blob/tree/commit/ref/audit effect | Different intent conflicts before provider access; expired key never executes |
| Ambiguous dispatch | Timeout, disconnect, cancellation, or malformed success after mutation may have applied | `unknown_provider_outcome` with opaque reconciliation evidence; no second mutation | At most five later read-only checks within 15 minutes; conflicting/exhausted evidence becomes `reconciliation_required` |
| Status read | Exact authorized operation/ref/commit evidence | Provider-neutral confirmed/not-applied/conflicting/unavailable status without mutation | Reject an idempotency key before source access; conceal hidden/not-found targets |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs:3` -- canonical provider port; add ordered mutation, explicit commit, and read-only status operations plus one-type-per-file safe models under this folder.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs:3` and `ProviderFailureCategory.cs:3` -- reuse existing capability IDs and stable failure vocabulary; do not create public GitHub categories.
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs:154` -- reuse boundary ordering: validate evidence, resolve target, acquire/dispose credential, invoke the private client, sanitize result.
- `src/Hexalith.Folders/Providers/GitHub/IGitHubApiClient.cs:3`, `OctokitGitHubApiClient.cs:7` -- add GitHub-private Git Data staging, commit/ref update, and status/reconciliation behavior; leave `GetReadinessAsync` to Story 3.3.
- `src/Hexalith.Folders/Providers/GitHub/GitHubSafeTargetFingerprint.cs:10`, `GitHubApiFailureCondition.cs:3`, `GitHubFailureMapper.cs:7` -- extend safe evidence and operation-specific known/unknown mapping without raw exception/provider data.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:174` -- register any new fail-closed resolver and construct GitHub consistently in default and Dapr credential compositions; do not wire workspace executors.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:62` -- read-only boundary evidence: unavailable content/delete/commit implementations remain until later durable stories.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs:12`, `OctokitGitHubApiClientTests.cs:12`, `GitHubDependencyGuardTests.cs:6` -- extend boundary ordering, exact wire behavior, failure matrix, no-retry, and leakage proof through the recording transport.
- `src/Hexalith.Folders.Testing/Providers/FakeGitProvider.cs:5` and local test provider fakes -- keep all `IGitProvider` implementations compiling with explicit unsupported defaults outside GitHub.
- `docs/contract/provider-compatibility-catalog.md:5` -- record implemented GitHub mutation/commit/status assumptions as pending OQ4 approval; never self-approve it.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders/Providers/Abstractions/*.cs` -- define metadata-safe request/result/status/reconciliation shapes and extend `IGitProvider`; preserve the existing capability and failure contracts.
- [x] `src/Hexalith.Folders/Providers/GitHub/*.cs` -- implement authorized private resolution, ordered Git Data staging, explicit commit/non-force ref update, exact status checks, safe mapping, and cancellation/ambiguity rules with Octokit 14.0.0 and REST profile `2022-11-28`.
- [x] `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` plus provider fakes -- wire fail-closed dependencies and compile-safe unsupported non-GitHub behavior without enabling the later production executor path.
- [x] `tests/Hexalith.Folders.Tests/Providers/GitHub/*.cs` -- cover the I/O matrix, authorization-before-observation, exact methods/paths/headers/order/ref identity, replay/no-second-dispatch, 400/401/403/404/409/422/429/5xx, primary/secondary limits, moved head, cancellation boundaries, ambiguous responses, bounded status checks, and serialized sentinel exclusion.
- [x] `docs/contract/provider-compatibility-catalog.md` -- document the implemented profile and leave Provider/Architecture/PM approval visibly pending.

**Acceptance Criteria:**
- Given current authorized provider evidence and an exact resolved GitHub target, when the canonical provider stages and commits a validated ordered change set, then no file operation auto-commits and success is returned only after one non-force ref update is confirmed at the intended commit.
- Given denial, stale/revoked evidence, wrong tenant, conflicting/expired intent, invalid lock/ref/path/type/size policy, or an unknown mutation, when any provider operation is requested, then it performs no forbidden credential/target/provider observation or second mutation and returns the stable provider-neutral safe outcome.
- Given a known GitHub response or an ambiguous post-dispatch result, when mapping and status reconciliation run, then known failures remain distinct, unknown outcomes permit only bounded read-only evidence checks, and every result survives serialization without sensitive material.
- Given Story 3.11 focused tests and dependency guards run offline, when they inspect both the canonical provider boundary and concrete Octokit transport, then all required success/failure/replay/order/ref/header/leakage cases pass without credentials or network access.

## Spec Change Log

## Review Triage Log

## Design Notes

GitHub Contents API is intentionally excluded because each write creates a commit. Use Git Data semantics so the adapter can prepare one ordered tree and preserve the product's explicit single-commit boundary; durable ownership of content and staged Git object references remains outside this story.

## Verification

**Commands:**
- `dotnet restore tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -m:1 -p:NuGetAudit=false` -- expected: restore succeeds with repository pins.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -c Release --no-restore -m:1 -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `./tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests -noLogo -noColor -class Hexalith.Folders.Tests.Providers.GitHub.GitHubProviderTests -class Hexalith.Folders.Tests.Providers.GitHub.OctokitGitHubApiClientTests -class Hexalith.Folders.Tests.Providers.GitHub.GitHubDependencyGuardTests` -- expected: focused adapter suite passes.
- `dotnet build Hexalith.Folders.slnx --no-restore -m:1` -- expected: solution build passes.
- `git diff --check` -- expected: no whitespace errors; `sprint-status.yaml` remains untouched.
