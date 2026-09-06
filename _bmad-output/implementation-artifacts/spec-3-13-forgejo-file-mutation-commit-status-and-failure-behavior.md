---
title: 'Story 3.13: Forgejo file mutation, commit, status, and failure behavior'
type: 'feature'
created: '2026-09-06'
status: in-progress
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/spec-3-11-github-file-mutation-commit-status-and-failure-behavior.md'
  - '_bmad-output/implementation-artifacts/spec-3-12-forgejo-repository-provisioning-binding-and-branch-ref-behavior.md'
  - 'docs/adrs/0003-provider-abstraction-and-capability-model.md'
warnings: []
deferred: []
baseline_revision: 2046b6db576a0a8fa1b5d56c9ee8b47a250fb62a
---

<intent-contract>

## Intent

**Problem:** The Forgejo adapter advertises file-mutation, commit, and status capabilities but does not override the canonical `IGitProvider` operations, so every call returns `UnsupportedProviderCapability`. The supported Forgejo REST profiles cannot reproduce the authoritative two-phase operation semantics because `repoChangeFiles` commits immediately and carries no expected-old-head compare-and-swap field.

**Approach:** Preserve the shared `IGitProvider` stage/commit/status surface and implement Forgejo operations through Git smart HTTPS with centrally pinned `LibGit2Sharp` `0.32.0` and its bundled `libgit2` `1.8.6`. Staging performs a bounded read-only fetch and constructs the tree in an isolated temporary bare repository without a commit or remote write. Commit creates a fresh isolated temporary bare repository, performs its own single bounded read-only fetch of the exact expected head, re-resolves the ordered changes from the caller-owned durable staged-change source, reconstructs and verifies the same tree, creates one local commit whose sole parent is that expected head, and sends one non-force `git-receive-pack` update whose old object ID equals that expected head. REST remains the version-aware transport for readiness and bounded status observations; `repoChangeFiles` is never used.

## Boundaries & Constraints

**Always:** Preserve authorization-before-source-before-credential-before-provider ordering; exact terminal replay; one eligible mutation; expected-head concurrency protection; read-only bounded status reconciliation; deterministic disposal; supported-version proof; and metadata-only results, diagnostics, tests, and live reports. Keep durable source/outcome ownership outside the adapter. Extend the internal commit-resolved source, not the public provider port, so `ResolveCommitAsync` rehydrates the caller-ordered `ProviderResolvedFileChange` values associated with the opaque staged-change reference; no provider-private durable store is introduced. Before push, require the exact authorized full ref to be advertised at the expected SHA, require the reconstructed tree SHA to equal the recorded staged-tree SHA, and make the receive-pack update command carry that expected SHA as `old-id`; the advertisement is preflight evidence, not the compare-and-swap. Support smart HTTPS only, the classic receive-pack `old-id new-id ref` update model, and SHA-1 repositories on Forgejo `15.0.7` and `16.0.3`; any other object format, transport, version, missing HTTP-Git support, or incompatible capability fails closed.

**Never:** Make `StageFileChangesAsync` create a commit, invoke receive-pack, or move a ref; use `repoChangeFiles`; fake a tree identity; hide changes in an in-memory cache; parse opaque references as locators; accept a preflight ref read as compare-and-swap; force a push; invoke an external `git` executable; use SSH, dumb HTTP, redirects, ambient system/global Git configuration, a checkout, hooks, submodules, or LFS; blindly retry an ambiguous mutation; weaken the shared semantics only for Forgejo; invent durable orchestration owned by Epic 12; write or revert `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|---------------------------|----------------|
| Ordered staging | Authorized add/change/remove set and exact target/head | Fetch the exact head read-only, construct and verify the ordered tree in an isolated temporary bare repository, record its tree identity, then destroy the repository; no commit, receive-pack, or ref movement occurs | Policy, object-format, advertised-ref, limit, or evidence mismatch rejects before any remote mutation |
| Explicit commit | Recorded staged tree, re-resolved ordered changes, message, full ref, and unchanged expected head | In a fresh isolated temporary bare repository, perform one bounded read-only fetch of the exact expected head, reconstruct the exact tree, create one local commit with the expected head as its sole parent, then send one non-force receive-pack update with `old-id = expected head`; confirm the intended commit by one read-only ref observation and remove the repository | Advertisement mismatch or receive-pack stale-old rejection is a known conflict; ambiguity after receive-pack dispatch is unknown and never retried |
| Status | Exact operation/ref/expected/intended-commit evidence | Perform one version-aware REST ref observation and return confirmed/not-applied/conflicting/unavailable | Check 5, expiry, or conflicting evidence requires reconciliation |
| Transport isolation | Short-lived credential lease and authorized HTTPS target | Supply `Authorization: Bearer` only through LibGit2Sharp fetch/push custom headers; use a bounded private temporary bare repository with no checkout, ambient config, hooks, submodules, or LFS; remove it and dispose the lease deterministically | Redirect, TLS, credential, native-load, time, disk, response, cleanup, or unsupported-capability failure maps to an allow-listed metadata-only result without raw native/provider output |
| REST combined write | Forgejo `repoChangeFiles` request | Reject as inadmissible; the selected operation transport never calls it | It commits immediately and provides no expected-head CAS field |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs:21` -- authoritative separate stage, commit, and status surface; defaults currently mask the missing Forgejo implementation.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderCommitResolvedSource.cs:3` and `IProviderOperationSourceResolver.cs:3` -- extend the internal commit source with the exact caller-ordered changes rehydrated from the durable staged-change reference; keep durable ownership outside the provider and preserve existing GitHub behavior.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:6` -- readiness/create/bind boundary; lacks operation source/outcome dependencies and operation overrides.
- `src/Hexalith.Folders/Providers/Forgejo/IForgejoApiClient.cs:3` and `ForgejoHttpApiClient.cs:29` -- retain version/readiness/status REST behavior; add a Forgejo-private LibGit2Sharp smart-HTTPS transport for bounded object fetch, local tree/commit creation, and one receive-pack push.
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.Operations.cs:104` -- reusable provider-neutral ordering, reservation, replay, recording, and status decision reference.
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs:283` -- reference implementation uses Git Data create-tree/create-commit/ref-update operations absent from Forgejo REST.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoReadinessMapper.cs:26` and `ForgejoHttpApiClient.cs:82` -- capability evidence currently overclaims operations that resolve to interface defaults.
- `tests/contracts/forgejo/{15.0.7,16.0.3}/swagger.v1.json` -- extend the retained reviewed subsets with the exact REST reads used by status; smart-Git behavior is proven separately because it is not OpenAPI-described.
- `Directory.Packages.props`, `src/Hexalith.Folders/Hexalith.Folders.csproj`, `docs/adrs/0003-provider-abstraction-and-capability-model.md`, and `docs/contract/provider-compatibility-catalog.md` -- centrally pin LibGit2Sharp `0.32.0`, record bundled libgit2 `1.8.6`, and amend the Forgejo transport decision/profile without changing the GitHub contract.
- `tests/tools/run-forgejo-provider-evidence-gates.ps1:159` -- current operator lane probes raw create/bind HTTP, not production-composed mutation/commit/status.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` -- orchestrator-owned; never write or revert.

## Tasks & Acceptance

**Execution:**
- Extend the existing internal operation-source and outcome seams so commit re-resolves the exact ordered staged changes and verifies their reconstructed tree identity; do not add durable provider-private state or change the public `IGitProvider` surface.
- Implement a Forgejo-private LibGit2Sharp `0.32.0` smart-HTTPS transport. Use exactly one bounded read-only upload-pack fetch of the exact expected head in each fresh stage and commit temporary repository, one receive-pack dispatch during commit, an exact expected-old/full-ref update, no force, no redirects, and REST-only bounded status observation.
- Isolate native Git execution in a unique private temporary bare repository; disable checkout, ambient configuration, hooks, submodules, LFS, SSH, dumb HTTP, and redirects; apply the existing file/count/content bounds plus bounded time, disk, transfer, native-output capture, and deterministic cleanup. Credential material is supplied only as an in-memory `Authorization: Bearer` custom header from the short-lived Forgejo credential lease and is never persisted or emitted.
- Treat only SHA-1 smart-HTTPS repositories on exact Forgejo versions `15.0.7` and `16.0.3` as supported. Expand retained REST evidence, add hermetic smart-HTTP receive-pack fixtures, verify the LibGit2Sharp native asset on the production Alpine target, and make readiness unavailable when HTTP Git, object format, native runtime, version, or required protocol capabilities are unsupported.
- Amend ADR 0003 and the provider compatibility catalog to record the second Forgejo transport and its dependency, protocol, credential, sandbox, object-format, version, failure, and drift policy.

**Acceptance Criteria:**
- Given an authorized ordered change set and exact expected head, when Forgejo staging succeeds, then exactly one read-only smart-HTTPS fetch produced the verified staged tree identity and no commit, receive-pack, or ref movement occurred; temporary objects were removed.
- Given the recorded staged tree and its re-resolved ordered changes, when Forgejo commit succeeds, then the adapter created a fresh isolated temporary bare repository, performed exactly one bounded read-only fetch of the exact expected head, reconstructed the same tree, created one commit whose sole parent is that expected head, sent exactly one non-force receive-pack update carrying that expected head as `old-id`, confirmed the exact full ref at the intended commit, and removed the repository.
- Given a moved head or ambiguous write, when the operation completes, then it returns the canonical conflict or unknown outcome without blind retry.
- Given a credential or transport failure, when the operation completes, then no bearer value, URL, repository/ref/path/content/message value, temporary path, native output, or raw exception crosses the provider boundary, and cleanup/disposal still occurs exactly once.
- Given production composition and hermetic tests, when the canonical Forgejo `IGitProvider` path runs on the production Alpine target against supported Forgejo `15.0.7` and `16.0.3` profiles, then mutation/commit use LibGit2Sharp smart HTTPS, status uses the selected real REST reads, stale-old races have no effect, unsupported profiles fail closed, and only metadata-safe evidence is emitted.

## Spec Change Log

- 2026-09-06: Human resolution selected LibGit2Sharp `0.32.0` smart HTTPS. The shared two-phase provider contract remains authoritative; ordered changes are rehydrated through the caller-owned durable operation-source seam, staging is read-only/local, and commit performs its own single bounded read-only fetch before one exact-old non-force receive-pack update under a no-redirect, pinned credential, sandbox, object-format, version, and compatibility policy.

## Review Triage Log

## Design Notes

The official Forgejo 15.0.7 and 16.0.3 Swagger surfaces provide read-only Git blob/tree/commit/ref operations and one multi-file `POST /repos/{owner}/{repo}/contents`. That write creates a commit immediately, while `ChangeFilesOptions` has no expected-head/old-commit compare-and-swap field. It remains inadmissible.

The selected design uses the existing caller-owned durable operation-source seam rather than a provider-private cache. Stage and commit each create a fresh isolated temporary bare repository and perform their own single bounded read-only fetch of the exact expected head before reconstructing the tree from that head and the ordered changes; commit rejects a tree mismatch before mutation. The receive-pack command's `old-id` is the expected-head compare-and-swap value, so a preliminary advertised-ref read never authorizes a push by itself. Fetch may use the smart-HTTP upload-pack behavior supported by bundled libgit2 `1.8.6`; push uses the classic receive-pack `old-id new-id ref` command and requires report-status. Redirects, protocol-v2-only behavior, SHA-256 repositories, and custom Forgejo Git extensions are outside this compatibility profile and fail closed.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release -m:1 -p:MinVerVersionOverride=1.0.0 -p:NuGetAudit=false` -- expected: the pre-change project compiles with zero warnings/errors.
- `git diff --check` -- expected: no whitespace errors.

**Manual checks (if no CLI):**
- Confirm stage and commit each perform exactly one bounded read-only fetch in a fresh temporary bare repository, staging sends no receive-pack request, commit re-resolves changes and sends one update with the exact expected old SHA, redirects fail closed, a concurrent ref change produces no effect, credentials/native output stay private, and each temporary repository is removed on every terminal path.
