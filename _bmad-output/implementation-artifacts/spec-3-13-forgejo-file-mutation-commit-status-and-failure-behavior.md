---
title: 'Story 3.13: Forgejo file mutation, commit, status, and failure behavior'
type: 'feature'
created: '2026-09-06'
status: in-progress
review_loop_iteration: 1
followup_review_recommended: false
context:
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/spec-3-11-github-file-mutation-commit-status-and-failure-behavior.md'
  - '_bmad-output/implementation-artifacts/spec-3-12-forgejo-repository-provisioning-binding-and-branch-ref-behavior.md'
  - 'docs/adrs/0003-provider-abstraction-and-capability-model.md'
warnings:
  - 'LibGit2Sharp does not expose byte callbacks for its internal repeated smart-HTTP advertisements or receive-pack status body, so the explicit response-byte ceiling is not fully enforceable through the selected API.'
  - 'A synchronous libgit2 call that stalls without callbacks can outlive the bounded caller response until native code unwinds, delaying cleanup.'
  - 'Readiness has no repository locator with which to verify per-repository smart-HTTP/object-format/receive-pack capabilities.'
deferred: []
baseline_revision: 2046b6db576a0a8fa1b5d56c9ee8b47a250fb62a
baseline_commit: 7e8bf3de7cb66e93b01ef1d279ed7d1a734c1675
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
- [x] Extend the existing internal operation-source and outcome seams so commit re-resolves the exact ordered staged changes and verifies their reconstructed tree identity; bind the request fingerprint to both the ordered staged changes and their reconstructed tree, do not add durable provider-private state, and do not change the public `IGitProvider` surface.
- [ ] Implement a Forgejo-private LibGit2Sharp `0.32.0` smart-HTTPS transport. Use exactly one bounded read-only upload-pack fetch of the exact expected head in each fresh stage and commit temporary repository, one receive-pack dispatch during commit, an exact expected-old/full-ref update, no force, no redirects, and REST-only bounded status observation.
- [ ] Isolate native Git execution in a unique private temporary bare repository; disable checkout, ambient configuration, hooks, submodules, LFS, SSH, dumb HTTP, and redirects; apply the existing file/count/content bounds plus bounded time, disk, transfer, native-output capture, and deterministic cleanup. Credential material is supplied only as an in-memory `Authorization: Bearer` custom header from the short-lived Forgejo credential lease and is never persisted or emitted.
- [ ] Treat only SHA-1 smart-HTTPS repositories on exact Forgejo versions `15.0.7` and `16.0.3` as supported. Expand retained REST evidence, add hermetic smart-HTTP receive-pack fixtures, verify the LibGit2Sharp native asset by executing the production Alpine target, and make readiness unavailable when HTTP Git, object format, native runtime, version, or required protocol capabilities are unsupported.
- [x] Amend ADR 0003 and the provider compatibility catalog to record the second Forgejo transport and its dependency, protocol, credential, sandbox, object-format, version, failure, and drift policy.
- [ ] Enforce the operation deadline as a wall-clock bound across gate acquisition, DNS/TLS, advertisements, native fetch/push, pack construction, and callbacks. Enforce response/transfer and temporary-disk ceilings while data is arriving, not only after native calls return, and always attempt cleanup from `finally` on every exception path. Avoid process-global LibGit2Sharp configuration mutation unless every in-process LibGit2Sharp consumer is protected by the same isolation boundary; create private temporary directories with current-user-only access on Windows and Unix.
- [x] Revalidate the durable operation reservation after acquiring the native-operation gate and immediately before provider access. Preserve `OperationCanceledException` semantics through source resolution, reservation, access, and revalidation; distinguish cancellation before receive-pack dispatch from ambiguity after dispatch.
- [x] Inspect and require SHA-1 plus the classic smart-HTTP receive-pack capabilities needed by this profile, including `report-status`, before mutation. Reject executable and symbolic-link source entries rather than silently converting or deleting them. Classify authentication, authorization, policy, stale-old, transport, and arbitrary remote rejection separately; only evidence of an expected-old mismatch may become `ProviderConflict`.
- [x] Make readiness evidence require the exact readiness scope, map every allow-listed status REST failure through the canonical failure mapper, keep known-failure reason/category validation symmetric (including response-limit failures), and include `/version` as well as the ref endpoint in status drift coverage.
- [x] Prove the transport with hermetic assertions over the received pack: the commit has exactly the expected parent and tree; add/change bytes are exact; removed paths are absent; no extra paths change. Add boundary tests that cross transfer and disk ceilings, a stale-old race after advertisement but before receive-pack POST, unexpected-exception cleanup tests, and an Alpine production-image smoke test that loads the native runtime and completes minimal TLS smart-HTTP fetch/push behavior.

**Acceptance Criteria:**
- Given an authorized ordered change set and exact expected head, when Forgejo staging succeeds, then exactly one read-only smart-HTTPS fetch produced the verified staged tree identity and no commit, receive-pack, or ref movement occurred; temporary objects were removed.
- Given the recorded staged tree and its re-resolved ordered changes, when Forgejo commit succeeds, then the adapter created a fresh isolated temporary bare repository, performed exactly one bounded read-only fetch of the exact expected head, reconstructed the same tree, created one commit whose sole parent is that expected head, sent exactly one non-force receive-pack update carrying that expected head as `old-id`, confirmed the exact full ref at the intended commit, and removed the repository.
- Given a moved head or ambiguous write, when the operation completes, then it returns the canonical conflict or unknown outcome without blind retry.
- Given a credential or transport failure, when the operation completes, then no bearer value, URL, repository/ref/path/content/message value, temporary path, native output, or raw exception crosses the provider boundary, and cleanup/disposal still occurs exactly once.
- Given production composition and hermetic tests, when the canonical Forgejo `IGitProvider` path runs on the production Alpine target against supported Forgejo `15.0.7` and `16.0.3` profiles, then mutation/commit use LibGit2Sharp smart HTTPS, status uses the selected real REST reads, stale-old races have no effect, unsupported profiles fail closed, and only metadata-safe evidence is emitted.
- Given a native operation, when the wall-clock deadline, transfer ceiling, temporary-disk ceiling, cancellation, or an unexpected exception occurs at any phase, then the call terminates with the correct metadata-only category, does not begin a late provider access, and attempts private-repository cleanup exactly once.
- Given commit source resolution, when any ordered staged change differs while the resulting tree identity is unchanged, then commit validation rejects it before provider access.
- Given a supported-profile claim, when the production Alpine image is exercised, then its actual native library loads and a minimal TLS smart-HTTP exchange succeeds; inspecting only package manifests is insufficient evidence.

## Spec Change Log

- 2026-09-06: Human resolution selected LibGit2Sharp `0.32.0` smart HTTPS. The shared two-phase provider contract remains authoritative; ordered changes are rehydrated through the caller-owned durable operation-source seam, staging is read-only/local, and commit performs its own single bounded read-only fetch before one exact-old non-force receive-pack update under a no-redirect, pinned credential, sandbox, object-format, version, and compatibility policy.
- 2026-09-07: Three independent review layers exposed specification gaps in native-operation bounding and cleanup, exact ordered-change binding, protocol capability and object-mode validation, reservation/cancellation ordering, failure classification, readiness scope, and production-grade verification. The execution and acceptance sections now make those requirements explicit to avoid the known-bad state in which post-hoc checks, broad exception mapping, manifest-only Alpine evidence, or a parsed-but-uninspected receive-pack could appear compliant. KEEP the pinned LibGit2Sharp/libgit2 profile, the private transport boundary, caller-owned durable seams, exact old/new push semantics, hermetic TLS fixture foundation, production DI path, retained REST evidence, metadata-only evidence, and unchanged public `IGitProvider` surface.

## Review Triage Log

| ID | Verdict | Evidence | Route |
|---|---|---|---|
| VG-1 | high | The hermetic server parses the update command but never imports and inspects the received pack, while the only full add/change/remove proof is skipped as operator-only; exact tree bytes, removals, and parentage are therefore unproved. | bad_spec |
| VG-2 | medium | Tests use only tiny transfers and repositories and never cross either the 32 MiB transfer ceiling or 64 MiB temporary-disk ceiling, so boundary termination and cleanup are unproved. | bad_spec |
| VG-3 | high | The dependency test only searches the host `.deps.json` for a musl asset; no test starts the production Alpine image, loads libgit2 there, or exercises smart HTTP. | bad_spec |
| VG-4 | medium | `GetOperationStatusAsync` preserves only version-incompatible and conflicting failures; authentication, permission, rate-limit, and missing-ref results fall through to generic status unavailable/reconciliation. | bad_spec |
| BH-1 | false | The sprint tracker change is the required step-03 orchestrator synchronization and the implementation was explicitly forbidden from owning it; `in-progress` while the spec was under review was expected workflow state. | reject |
| BH-2 | high | Both transport cores delete their temporary repository only after their handled catch blocks; an unexpected exception bypasses deletion because cleanup is not in `finally`. | bad_spec |
| BH-3 | high | `EnsureWithinDiskLimit` runs only after fetch and tree/commit construction, so an inflated pack can consume unbounded disk before the check. | bad_spec |
| BH-4 | high | The 30-second test exists only in callbacks and after synchronous native calls; stalled DNS, TLS, advertisement, fetch, or push can hold the global semaphore indefinitely. | bad_spec |
| BH-5 | high | Transfer callbacks bound pack-byte progress but do not bound smart-HTTP advertisements or receive-pack status bodies consumed by libgit2. | bad_spec |
| BH-6 | high | The transport never validates advertised `report-status`; it can dispatch to a server outside the declared receive-pack capability profile. | bad_spec |
| BH-7 | medium | SHA-1 is assumed through 40-character request validation, but unsupported repository object format is reported as validation/ref failure rather than `ObjectFormatUnsupported`. | bad_spec |
| BH-8 | high | Source validation checks only that a target is a blob and then writes `Mode.NonExecutableFile`; executable files can be silently converted and symlink blobs can be modified or removed. | bad_spec |
| BH-9 | medium | Native authentication and permission failures are caught as generic `LibGit2SharpException` and reported as server unavailable before dispatch. | bad_spec |
| BH-10 | high | `OnPushStatusError` sets one rejection flag and every such rejection becomes `RefHeadConflict`, including branch policy and arbitrary server failures. | bad_spec |
| BH-11 | medium | `forgejo_response_limit_exceeded` is allow-listed for outward results but absent from `KnownFailureReasonCategory`, so replay/finalization coherence rejects its own recorded tuple. | bad_spec |
| BH-12 | medium | Source and reservation helpers catch all exceptions, including `OperationCanceledException`, and convert cancellation into unavailable, unconfigured, or conflict outcomes. | bad_spec |
| BH-13 | high | `AmbientGitConfigurationIsolation` mutates process-wide `GlobalSettings`; its class-local semaphore cannot protect unrelated LibGit2Sharp consumers in the same process. | bad_spec |
| BH-14 | medium | Windows uses plain `Directory.CreateDirectory`, which does not explicitly establish the required current-user-only ACL for credential-bearing temporary repositories. | bad_spec |
| BH-15 | high | The Alpine check is manifest-only and therefore cannot establish that the production musl native binary loads or works; independently confirms VG-3. | bad_spec |
| BH-16 | high | The stale test advances the fixture before receive advertisement and the fixture does not enforce the advertised old ID at receive time, so it does not prove a post-advertisement stale-old race has no effect. | bad_spec |
| BH-17 | medium | The status coverage matrix lists only the ref endpoint even though status performs version selection through `/version`, allowing drift in that dependency to escape the declared matrix. | bad_spec |
| BH-18 | high | Readiness validation accepts any canonical operation scope instead of exact readiness evidence, allowing authorization evidence captured for another operation to authorize readiness. | bad_spec |
| EH-1 | high | Independent tracing confirms exact readiness scope is weakened to membership in the whole canonical-operation catalog. | bad_spec |
| EH-2 | medium | Independent tracing confirms non-version/non-conflict status failures are flattened into generic unavailability or reconciliation. | bad_spec |
| EH-3 | high | Stage validates its reservation before waiting on `NativeOperationGate` and never revalidates after acquiring it, so a revoked reservation can still start provider access after a long wait. | bad_spec |
| EH-4 | high | Independent tracing confirms executable and symlink entries satisfy the blob check and can be replaced with regular-file mode or removed. | bad_spec |
| EH-5 | high | Independent tracing confirms every push-status error is mapped to expected-head conflict without inspecting the rejection. | bad_spec |
| EH-6 | medium | Independent tracing confirms status drift coverage omits its `/version` dependency. | bad_spec |
| EH-7 | high | Commit validates that ordered changes are structurally well formed but binds `SafeStagedChangeSetFingerprint` only to the tree SHA, so a different ordered list producing the same tree is accepted. | bad_spec |
| EH-8 | high | Independent tracing confirms no hard deadline can interrupt a synchronous native call that stops producing callbacks. | bad_spec |
| EH-9 | high | Independent tracing confirms temporary-disk usage is measured only after native fetch/build work completes. | bad_spec |
| EH-10 | high | Independent tracing confirms advertisement and receive-pack response bytes are outside the transfer-progress ceiling. | bad_spec |
| EH-11 | high | Independent tracing confirms unexpected exceptions skip temporary-repository deletion. | bad_spec |
| EH-12 | high | Independent tracing confirms the required receive-pack `report-status` capability is not inspected before mutation. | bad_spec |
| EH-13 | high | Independent tracing confirms the production Alpine claim is supported only by dependency-manifest text, not runtime execution. | bad_spec |

## Design Notes

The official Forgejo 15.0.7 and 16.0.3 Swagger surfaces provide read-only Git blob/tree/commit/ref operations and one multi-file `POST /repos/{owner}/{repo}/contents`. That write creates a commit immediately, while `ChangeFilesOptions` has no expected-head/old-commit compare-and-swap field. It remains inadmissible.

The selected design uses the existing caller-owned durable operation-source seam rather than a provider-private cache. Stage and commit each create a fresh isolated temporary bare repository and perform their own single bounded read-only fetch of the exact expected head before reconstructing the tree from that head and the ordered changes; commit rejects a tree mismatch before mutation. The receive-pack command's `old-id` is the expected-head compare-and-swap value, so a preliminary advertised-ref read never authorizes a push by itself. Fetch may use the smart-HTTP upload-pack behavior supported by bundled libgit2 `1.8.6`; push uses the classic receive-pack `old-id new-id ref` command and requires report-status. Redirects, protocol-v2-only behavior, SHA-256 repositories, and custom Forgejo Git extensions are outside this compatibility profile and fail closed.

### Review KEEP Instructions

Preserve the centrally pinned LibGit2Sharp `0.32.0` / libgit2 `1.8.6` compatibility profile, Forgejo-private transport boundary, caller-owned durable source/outcome seams, unchanged public `IGitProvider` surface, fresh-repository stage/commit model, exact LibGit2Sharp push-update old/new semantics, non-force single-ref update, metadata-only evidence, retained exact-version REST snapshots, canonical production DI path, and hermetic TLS smart-HTTP fixture. Preserve the prohibition on REST contents writes, external Git, checkout, provider-private durable state, raw output, and credential persistence. The re-derived implementation must keep these successful foundations while replacing post-hoc bounds, broad exception mapping, incomplete evidence binding, and manifest-only verification.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release -m:1 -p:MinVerVersionOverride=1.0.0 -p:NuGetAudit=false` -- expected: the pre-change project compiles with zero warnings/errors.
- `git diff --check` -- expected: no whitespace errors.

**Manual checks (if no CLI):**
- Confirm stage and commit each perform exactly one bounded read-only fetch in a fresh temporary bare repository, staging sends no receive-pack request, commit re-resolves changes and sends one update with the exact expected old SHA, redirects fail closed, a concurrent ref change produces no effect, credentials/native output stay private, and each temporary repository is removed on every terminal path.
