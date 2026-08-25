---
title: 'Story 3.11: GitHub file mutation, commit, status, and failure behavior'
type: 'feature'
created: '2026-08-24'
status: in-review
baseline_revision: '5c01c5870a099362582e02637654afc0286cf20b'
baseline_commit: '02ef9f87e61dd2057ed9a3760ca2a32f86888f5d'
review_loop_iteration: 3
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

**Acceptance Surface:** For Story 3.11, "real deployed GitHub composition" means that production service registration resolves the canonical `IGitProvider` to the concrete GitHub provider backed by Octokit for mutation, commit, and status behavior. Completion requires provider-component production-composition evidence plus hermetic evidence through the real adapter and transport; it does not require a successful outer workspace-task dispatch. The adapter validates and consumes caller-supplied authoritative authorization, lock, ref-policy, idempotency, target, content, and reconciliation-budget evidence but does not produce or persist those decisions. Stories 12.3, 12.4, 4.20, and 4.21 retain ownership of durable target/content resolution, lock and idempotency persistence, executor/process-manager composition, reconciliation scheduling, terminal task/projection state, and end-to-end deployed workspace proof.

## Boundaries & Constraints

**Always:** Authorize and validate fresh tenant, folder, delegated-task, binding, ref-policy, and canonical lock evidence before target, credential, content, or provider observation; resolve opaque references only into short-lived GitHub-private values; preserve caller mutation order; never move the branch while staging files; allow at most one non-force ref update for an explicit commit; return only provider-neutral safe fingerprints, canonical outcome categories, retry posture, and opaque reconciliation identity. Treat cancellation before dispatch as no effect, possible post-dispatch ambiguity as `unknown_provider_outcome`, and status as read-only/unavailable rather than an unknown mutation. Perform one read-only status observation per authorized provider call and validate the caller-supplied authoritative check number and time-window evidence; durable scheduling and persistence of the five-check/15-minute reconciliation lifecycle remain downstream. Keep all events, results, exceptions, diagnostics, and test failures free of tokens, raw owner/repository/ref/path/content/message values, diffs, URLs, and provider bodies.

**Block If:** A proven public Contract Spine gap requires new externally observable semantics not settled by the current authority, or implementation would require inventing the durable content/target store, worker lifecycle, or canonical lock decision owned by another story. OQ4 approval and credential-gated live evidence are operator follow-up actions, not blocking conditions for the agent-completable adapter slice.

**Never:** Use GitHub Contents API writes that implicitly commit each file; force-update a ref; blindly retry a mutation or commit after ambiguous dispatch; parse opaque references as GitHub locators; implement GitHub readiness, Forgejo execution, repository creation orchestration, server-side durable content storage, final workspace executor/process-manager wiring, UI/CLI/MCP behavior, or hand-edit generated clients. Never write or revert `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Ordered staging | Authorized exact target/ref plus validated add/change/remove set | Blobs/tree are prepared in requested order without ref movement or implicit commit | Policy/SHA/ref mismatch is a known canonical failure |
| Explicit commit | Known staged set and unchanged expected head | One commit is created and one non-force ref update is confirmed; canonical safe commit evidence is returned | Moved head/protection conflict does not overwrite |
| Equivalent replay | Same live idempotent provider intent | Same logical safe result; no duplicate blob/tree/commit/ref/audit effect | Different intent conflicts before provider access; expired key never executes |
| Ambiguous dispatch | Timeout, disconnect, cancellation, or malformed success after mutation may have applied | `unknown_provider_outcome` with opaque reconciliation evidence; no second mutation; one later read-only observation per authorized status call | Downstream durable orchestration may request at most five checks within 15 minutes; conflicting/exhausted evidence becomes `reconciliation_required` |
| Status read | Exact authorized operation/ref/commit evidence | Provider-neutral confirmed/not-applied/conflicting/unavailable status without mutation | Reject an idempotency key before source access; conceal hidden/not-found targets |
| Production composition | Production provider registration with the downstream durable operation resolver absent | The canonical provider port resolves exactly one concrete GitHub/Octokit adapter; mutation, commit, and status fail closed before credential or provider access when their authoritative source is unavailable | Real provider registration plus hermetic adapter/transport behavior completes this story's composition surface; it is not successful outer workspace execution evidence |

</intent-contract>

## Code Map

- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs:3` -- canonical provider port; add ordered mutation, explicit commit, and read-only status operations plus one-type-per-file safe models under this folder.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationCatalog.cs:3` and `ProviderFailureCategory.cs:3` -- reuse existing capability IDs and stable failure vocabulary; do not create public GitHub categories.
- `src/Hexalith.Folders/Providers/Abstractions/ProviderIdempotencyAdmission.cs:3` and new resolved-source/outcome-seam types -- carry every prior terminal disposition and exact opaque identity on replay; reserve an opaque operation reference before dispatch; validate its generation immediately before dispatch; and record staged-tree, created-commit, ref-update, or terminal no-dispatch evidence without exposing GitHub SHAs publicly.
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs:154` -- reuse boundary ordering: validate evidence, resolve target, acquire/dispose credential, invoke the private client, sanitize result.
- `src/Hexalith.Folders/Providers/GitHub/IGitHubApiClient.cs:3`, `OctokitGitHubApiClient.cs:7` -- add GitHub-private Git Data staging, commit/ref update, and status/reconciliation behavior; leave `GetReadinessAsync` to Story 3.3.
- `src/Hexalith.Folders/Providers/GitHub/GitHubSafeTargetFingerprint.cs:10`, `GitHubProviderSafeOperationEvidence.cs`, `GitHubApiFailureCondition.cs:3`, `GitHubFailureMapper.cs:7` -- define versioned, length-prefixed SHA-256 bindings for resolved target/path/content/tree/message/head/commit evidence, and extend operation-specific known/unknown mapping without raw exception/provider data.
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:174` -- register the concrete GitHub/Octokit provider consistently in default and Dapr credential compositions, prove it resolves exactly once through the canonical port, and keep its downstream operation resolver fail-closed; do not wire workspace executors.
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:62` -- read-only boundary evidence: unavailable content/delete/commit implementations remain until later durable stories.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs:12`, `OctokitGitHubApiClientTests.cs:12`, `GitHubDependencyGuardTests.cs:6` -- extend boundary ordering, exact wire behavior, failure matrix, no-retry, and leakage proof through the recording transport.
- `src/Hexalith.Folders.Testing/Providers/FakeGitProvider.cs:5` and local test provider fakes -- keep all `IGitProvider` implementations compiling with explicit unsupported defaults outside GitHub.
- `docs/contract/provider-compatibility-catalog.md:5` -- record implemented GitHub mutation/commit/status assumptions as pending OQ4 approval; never self-approve it.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Folders/Providers/Abstractions/*.cs` -- define metadata-safe request/result/status/reconciliation shapes and extend `IGitProvider`; require resolved target, path, content, staged-tree, commit-message, expected-head, intended-commit, check-window, and full-ref bindings; carry exact prior success, unknown, and known-terminal identities; centralize allow-listed reason/retry metadata; reject empty, duplicate, and ancestor/descendant-conflicting change sets; and expose a persistence-neutral reserve/validate/record protocol with `acquired`, `pending`, `replay_success`, `replay_unknown`, `replay_known_failure`, `conflict`, and `unavailable` dispositions.
- [ ] `src/Hexalith.Folders/Providers/GitHub/GitHubProvider*.cs` -- validate canonical ULID correlation IDs, bounded opaque references, and every caller-authoritative evidence binding before credential/provider access; reserve the outcome slot and revalidate its opaque reference plus generation immediately before mutation dispatch; finalize reservations when dispatch never begins; use a five-second internal cancellation token for post-dispatch recording; require created-commit evidence to be recorded before ref update; handle null/failed recording as safe unknown evidence tied to the surviving reservation identity; preserve every exact terminal replay; distinguish known ref-update failures from ambiguous post-commit failures; and validate, but do not durably consume, caller-authoritative check numbers `1..5` and an operation-bound 15-minute window against an injected clock.
- [ ] `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient*.cs` -- implement ordered Git Data staging and one explicit commit/non-force ref update with Octokit 14.0.0 and REST profile `2022-11-28`; use pooled HTTP infrastructure and required headers; escape dynamic segments; validate only touched paths against mode `100644`; verify the created tree and commit with returned-SHA read-backs; record the commit before ref update; confirm the exact full ref and intended commit with one post-update GET; handle recursive truncation through non-recursive touched-ancestor traversal capped at 64 requests, 256 entries per response, depth 32, a 7 MiB response, and five seconds; enforce at most 100 changes, 1 MiB per file, and 10 MiB aggregate content with overflow-safe addition before hashing/base64/dispatch; accept delta/date `Retry-After`; and apply non-retryable ambiguity only after mutation dispatch while retaining read-only unavailable retry posture for checks `1..4`.
- [ ] `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs` plus provider fakes -- preserve custom/non-GitHub `IGitProvider` registrations, normalize only GitHub registrations to one canonical singleton concrete adapter, and make single-service plus capability resolution deterministically select it despite type-, instance-, factory-, or shorter-lived GitHub pre-registration; register fail-closed reservation/source seams; restore no unrelated gitlinks; and keep unsupported non-GitHub behavior compile-safe without enabling the later production executor path.
- [ ] `tests/Hexalith.Folders.Tests/Providers/GitHub/*.cs` -- cover the I/O matrix plus successful provider commit/status, all terminal mutation and commit replays, concurrent acquired/pending reservation races, reservation invalidation/finalization, created-commit recording before ref update, post-dispatch recorder cancellation/null, empty/duplicate/ancestor-conflicting changes, overflow-safe limits, real resulting-tree add/change/delete semantics, untouched executable/symlink/submodule entries, recursive and fallback truncation, commit/ref/full-ref identity mismatches, exact status decision rows, allow-listed resolver/credential/public failure metadata, delta/date retry evidence, exact DI coexistence/selection/lifetime, and one successful production-resolved `IGitProvider` path through the real adapter and recording transport for mutation, commit, and status. Exercise `400/401/403/404/409/422/429/5xx`, primary/secondary rate limits, moved/deleted/diverged refs, pre/post-dispatch cancellation, malformed success, and disconnects separately for staging, commit creation, ref update, confirmation, and status phases.
- [ ] `docs/contract/provider-compatibility-catalog.md` -- document the implemented GitHub tree traversal, response-size, aggregate-content, retry, and OQ4-pending profile without self-approval.

**Acceptance Criteria:**
- Given current authorized provider evidence and an exact resolved GitHub target, when the canonical provider stages and commits a validated ordered change set, then no file operation auto-commits and success is returned only after one non-force ref update is confirmed at the intended commit.
- Given an empty change set, repeated path, or ancestor/descendant path conflict, when staging is requested, then validation rejects before allocation, credential resolution, or GitHub access; caller order is otherwise preserved exactly.
- Given a base tree containing unrelated executable files, symlinks, submodules, or nested trees, when regular-file changes target other paths, then only the touched paths are policy-validated, the created tree is read back by its returned SHA using resulting-tree semantics, additions/changes match their blob SHAs, deletions are absent, and unrelated entries do not reject the operation.
- Given an authorized request and a private resolver result, when any target, path, content, tree, message, expected-head, intended-commit, tenant, folder, task, or binding value does not reproduce the request's versioned safe fingerprint, then the provider rejects before credential or GitHub access and emits no raw value.
- Given denial, stale/revoked evidence, wrong tenant, conflicting/expired intent, invalid lock/ref/path/type/size policy, or an unknown mutation, when any provider operation is requested, then it performs no forbidden credential/target/provider observation or second mutation and returns the stable provider-neutral safe outcome.
- Given equivalent durable admission, when a mutation is replayed, then the exact prior success operation reference, unknown reconciliation reference, or known-terminal failure and operation reference is returned with the same logical disposition and no source, credential, provider, or audit effect; `pending` never owns a second dispatch.
- Given a known GitHub response or an ambiguous post-dispatch result, when mapping and status reconciliation run, then known failures remain distinct, a created commit is recorded and acknowledged privately behind its opaque operation reference before ref update, rate limits or disconnects after commit creation never authorize another commit, and every public result survives serialization without sensitive material.
- Given status evidence bound to the operation, exact full ref, expected head, intended commit, check number, and window start, when the adapter observes the ref, then intended commit is `confirmed`, unchanged expected head is `not_applied`, a different/deleted/non-commit/wrong-ref result is `conflicting`, transport or authorized concealed-not-found evidence is `unavailable`, checks `1..4` make only `unavailable` or `not_applied` retryable within 15 minutes, and check 5 or an expired window becomes `reconciliation_required`; the provider validates one observation request while durable scheduling and concurrent budget consumption remain downstream-owned.
- Given a fresh mutation intent, when durable outcome reservation is unavailable, cancelled, invalidated, or fails generation revalidation immediately before dispatch, then the reservation is finalized safely and no GitHub effect occurs; once dispatch begins, caller cancellation cannot abort five-second private outcome recording, and a null/failed recorder result becomes metadata-safe unknown evidence tied to the surviving reservation identity rather than an escaping exception.
- Given staging has no ref-visible effect, when its final Git object request is ambiguous, then the result remains `unknown_provider_outcome`, no mutation is retried, and no branch-status claim is fabricated; durable quarantine or later Git-object reconciliation remains downstream-owned.
- Given production provider services are registered, when the canonical provider port is resolved, then exactly one concrete GitHub/Octokit adapter is selected and an absent downstream operation source fails closed before credential or provider access without masquerading as successful outer workspace execution.
- Given production provider services and hermetic authorized collaborators are registered, when mutation, commit, and status are invoked through the production-resolved canonical `IGitProvider`, then each operation crosses the concrete GitHub provider and real Octokit/raw transport once with the same behavior proven by the isolated layers.
- Given Story 3.11 focused tests and dependency guards run offline, when they inspect the production provider registration, canonical provider boundary, and concrete Octokit transport, then all required success/failure/replay/order/ref/header/leakage cases pass without credentials or network access.

## Spec Change Log

- 2026-08-25: Human resolution selected layered ownership. Story 3.11 owns the production-registered provider/Octokit mutation, commit, and status seam; Stories 12.3, 12.4, 4.20, and 4.21 retain durable orchestration and end-to-end workspace ownership.
- 2026-08-25: Reset the execution checklist because the attempted implementation was reverted. The saved intent-gap patch remains evidence only and must not be restored; re-drive this story from scratch.
- 2026-08-25: Review found that the re-derived provider seam structurally validated private resolver output without binding it to admitted target/content/commit/status fingerprints, synthesized replay identities, lost private created-commit evidence on ambiguous ref updates, and left transport/status edge cases under-specified. The specification now requires versioned source bindings, exact replay identity, a persistence-neutral private outcome-recording seam, explicit staging-ambiguity semantics, pooled and escaped raw transport, complete tree/object validation, and bounded retry posture. The known-bad state to avoid is any provider call that can substitute private data, re-dispatch after a possible commit, fabricate branch status for staging, or return an opaque identity that no downstream resolver can resolve. KEEP: retain the provider-component ownership boundary, the canonical `IGitProvider` extension, production registration with fail-closed durable seams, ordered Git Data staging, one explicit non-force ref update, provider-neutral safe outcomes, stable known-failure mapping, and hermetic real-transport evidence.
- 2026-08-25: Review iteration 2 found that the created-tree response was incorrectly treated as an echo of the request, untouched valid Git modes were rejected, and outcome recording had an availability race because it lacked a pre-dispatch reservation. The specification now requires returned-SHA read-back with resulting-tree semantics, touched-path-only policy checks, bounded traversal for the documented 7 MB recursive limit, an aggregate content budget, and reserve-before-dispatch/record-after-dispatch semantics with an internal bounded token. The known-bad state to avoid is comparing a resulting tree with request deletion markers, rejecting a repository because an unrelated path is executable/symlink/submodule, or performing a GitHub mutation after only sampling recorder availability. KEEP: retain the versioned fixed-time evidence bindings, exact replay identity, ULID correlation safety, provider-neutral public results, pooled/escaped/header-complete raw transport, one non-force ref update, fail-closed production seams, and the focused/full test coverage expectation with newly established green evidence.
- 2026-08-25: Review iteration 3 found that the reserved operation lacked a complete concurrent state protocol, created-commit recovery still had a record-before-ref-update gap, status results lacked an exact decision table, and the bounds/fingerprint/verification contracts remained non-normative while the runtime implementation was absent from the reviewed diff. The specification now defines reservation dispositions and generation revalidation, exact terminal replay, pre-ref-update commit recording, operation-bound status decisions, canonical evidence encoding, explicit resource limits, phase-specific ambiguity and confirmation, GitHub-registration coexistence, the complete response matrix, and executable verification. The known-bad state to avoid is a second dispatch from a pending/known-failure replay, a moved ref without recoverable commit identity, caller-shaped status budget bypass, tree truncation accepted as deletion, ambiguous mutation replay, or spec-only completion. KEEP: preserve the provider-component ownership boundary, production-resolved canonical GitHub adapter, caller-authoritative/downstream-durable separation, fixed-time safe bindings, ordered Git Data staging, touched-path-only tree validation, one non-force ref update, fail-closed production seams, metadata-only outcomes, and hermetic real-transport coverage.

## Review Triage Log

### 2026-08-25 — Review pass
- intent_gap: 1: (high 1, medium 0, low 0)
- bad_spec: 10: (high 9, medium 1, low 0)
- patch: 15: (high 7, medium 7, low 1)
- defer: 6: (high 4, medium 2, low 0)
- reject: 5: (high 0, medium 2, low 3)
- addressed_findings:
  - none

### 2026-08-25 — Review pass (iteration 1)
- intent_gap: 0
- bad_spec: 13: (high 10, medium 3, low 0)
- patch: 18: (high 10, medium 8, low 0)
- defer: 0
- reject: 5: (high 0, medium 3, low 2)
- addressed_findings:
  - `[high]` `[bad_spec]` Bound every private resolved target to the admitted tenant, organization, folder, task, repository binding, ref, and head evidence before credential or provider access.
  - `[high]` `[bad_spec]` Required canonical recomputation of path, content, and ordered change-set fingerprints so a resolver cannot substitute authorized mutation inputs.
  - `[high]` `[bad_spec]` Required canonical binding of the staged tree and commit message to the explicit commit intent.
  - `[high]` `[bad_spec]` Required canonical binding of status target, expected head, intended commit, and operation reference before any read-only observation.
  - `[high]` `[bad_spec]` Required canonical correlation identifiers and metadata-safe public results so token-shaped arbitrary text cannot be reflected as correlation evidence.
  - `[high]` `[bad_spec]` Required equivalent replay to carry the exact prior opaque operation identity and to reproduce success versus unknown disposition without synthesizing an unusable reference.
  - `[high]` `[bad_spec]` Clarified that ambiguous staging has no branch-visible status claim and cannot be redispatched; downstream owns quarantine or Git-object reconciliation.
  - `[high]` `[bad_spec]` Added a persistence-neutral private outcome-recording seam so an ambiguous ref update does not discard the created commit SHA behind an irreversible hash.
  - `[high]` `[bad_spec]` Added explicit status-read authorization and check-budget semantics so pre-fifth unavailable/not-applied observations remain safely retryable and the fifth check closes to reconciliation required.
  - `[high]` `[bad_spec]` Required returned Git object and commit identity validation instead of accepting any valid-shaped provider SHA.
  - `[medium]` `[bad_spec]` Required bounded provider/file/tree response limits with failure categories distinct from caller content-policy rejection.
  - `[medium]` `[bad_spec]` Required pooled/factory-managed raw GitHub HTTP transport rather than a fresh handler per operation.
  - `[medium]` `[bad_spec]` Required explicit provider-side caps for caller-authoritative change-count and file-size limits before allocation or base64 expansion.

### 2026-08-25 — Review pass (iteration 2)
- intent_gap: 0
- bad_spec: 6: (high 5, medium 1, low 0)
- patch: 21: (high 11, medium 10, low 0)
- defer: 0
- reject: 5: (high 0, medium 3, low 2)
- addressed_findings:
  - `[high]` `[bad_spec]` Replaced request-echo validation of `POST /git/trees` with returned-SHA read-back and resulting-tree assertions for add/change/delete behavior.
  - `[high]` `[bad_spec]` Limited regular-file mode policy to touched paths so unrelated executable blobs, symlinks, submodules, and nested trees remain valid repository state.
  - `[high]` `[bad_spec]` Replaced recorder availability sampling with a durable opaque-operation reservation that must succeed before any provider dispatch.
  - `[high]` `[bad_spec]` Required post-dispatch outcome recording to ignore caller cancellation under a bounded internal token and safely handle null or failed recorder responses.
  - `[high]` `[bad_spec]` Added an aggregate content-byte budget before hashing, base64 allocation, or provider dispatch.
  - `[medium]` `[bad_spec]` Aligned recursive tree handling with GitHub's documented 7 MB limit and required bounded non-recursive touched-path traversal when recursive evidence is truncated.

### 2026-08-25 — Review pass (iteration 3)
- intent_gap: 0
- bad_spec: 25: (high 15, medium 9, low 1)
- patch: 0
- defer: 0
- reject: 2: (high 0, medium 2, low 0)
- addressed_findings:
  - `[medium]` `[bad_spec]` Updated the Code Map so the operation seam owns reserve, generation validation, outcome recording, and terminal replay contracts.
  - `[high]` `[bad_spec]` Defined acquired, pending, replay-success, replay-unknown, replay-known-failure, conflict, and unavailable reservation dispositions so concurrent equivalent callers cannot both dispatch.
  - `[high]` `[bad_spec]` Required opaque reservation-reference and generation revalidation immediately before dispatch and safe finalization when dispatch never starts.
  - `[high]` `[bad_spec]` Required created-commit evidence to be durably recorded and acknowledged before the single non-force ref update.
  - `[high]` `[bad_spec]` Bound recorder failure and unknown public evidence to the surviving reservation identity so reconciliation references remain resolvable.
  - `[high]` `[bad_spec]` Extended exact replay to every known terminal failure so a rejected ref update cannot create another unreachable commit.
  - `[high]` `[bad_spec]` Bound status evidence to the operation, full ref, heads, check number, window start, and injected clock while preserving downstream ownership of durable scheduling.
  - `[high]` `[bad_spec]` Added exact confirmed, not-applied, conflicting, unavailable, retryable, and reconciliation-required status decisions, including deleted and diverged refs.
  - `[high]` `[bad_spec]` Defined canonical UTF-8/NFC, field-order, unsigned big-endian length, null, collection, and domain-version encoding with fixed vectors.
  - `[medium]` `[bad_spec]` Prevented public standalone hashes of predictable values by binding every exposed digest to high-entropy operation and authorization identities.
  - `[high]` `[bad_spec]` Rejected empty, duplicate, and ancestor/descendant-conflicting ordered change sets before any sensitive observation.
  - `[medium]` `[bad_spec]` Added numeric change, file, aggregate, response, depth, request-count, entry-count, and time limits with overflow-safe addition.
  - `[high]` `[bad_spec]` Scoped no-retry ambiguity to mutation phases and retained bounded read-only retry posture only for status checks 1 through 4.
  - `[high]` `[bad_spec]` Required deterministic commit request fields plus returned-SHA read-back of tree, parent, and safe message evidence.
  - `[high]` `[bad_spec]` Required exact full-ref and intended-commit confirmation with a post-update GET rather than trusting the PATCH response alone.
  - `[medium]` `[bad_spec]` Preserved custom and non-GitHub providers while normalizing only GitHub registrations to the canonical singleton.
  - `[medium]` `[bad_spec]` Restored the explicit HTTP, rate-limit, moved-ref, cancellation, malformed-response, and disconnect phase matrix to the test task.
  - `[medium]` `[bad_spec]` Added a complete unfiltered run of the owning test project after the focused adapter lane.
  - `[medium]` `[bad_spec]` Added Release solution restore before the serialized no-restore solution build.
  - `[medium]` `[bad_spec]` Added a path-specific assertion that story work did not mutate orchestrator-owned `sprint-status.yaml`.
  - `[low]` `[bad_spec]` Reworded historical KEEP text to require newly established green evidence instead of asserting unrecorded green results.
  - `[high]` `[bad_spec]` Routed the spec-only reviewed state back through implementation so runtime provider, transport, DI, and test evidence must exist before completion.
  - `[high]` `[bad_spec]` Required every truncated recursive or fallback tree response to fail closed rather than treating absent entries as confirmed deletion.
  - `[medium]` `[bad_spec]` Required bounded opaque operation and reconciliation references before any public result or source lookup.
  - `[high]` `[bad_spec]` Required status responses to reproduce the exact requested full ref as well as the intended commit before confirmation.

## Design Notes

GitHub Contents API is intentionally excluded because each write creates a commit. Use Git Data semantics so the adapter can prepare one ordered tree and preserve the product's explicit single-commit boundary; durable ownership of content and staged Git object references remains outside this story.

KEEP the successful layered surface: `IGitProvider` is the public provider-neutral seam; `GitHubProvider` is the authorization/sanitization boundary; the private GitHub client performs Git Data requests; production DI resolves exactly one GitHub provider while source and outcome persistence remain fail-closed; the Server workspace executor remains unavailable for later stories.

All safe bindings use one internal versioned, domain-separated, length-prefixed SHA-256 primitive. Its canonical input is UTF-8 over NFC-normalized text in the declared record-constructor field order; each field is encoded as a one-byte presence marker followed, when present, by a four-byte unsigned big-endian byte length and its bytes. Integers use fixed-width unsigned big-endian encoding, booleans use `0x00`/`0x01`, and collections encode a four-byte count followed by ordered elements without sorting. Every domain begins with the ASCII version tag and purpose (for example `hxf-github:v1:mutation-source`), and tests pin fixed vectors for null, empty, Unicode, ordered changes, and every source/outcome domain. The provider recomputes bindings from the private resolved value plus the relevant request identity and compares them with `CryptographicOperations.FixedTimeEquals`. No public digest binds only predictable owner, repository, ref, path, content, or message data: it also includes the admitted authorization fingerprint plus a caller-issued high-entropy ULID/opaque operation identity. A shape-valid 64-character value alone is never evidence. The resolved-source models carry only the minimum private value and opaque references needed for this comparison; `ToString()`, exceptions, logs, test names, and assertion messages remain metadata-only.

Equivalent replay reproduces the durable prior logical result. A prior success requires both its safe outcome fingerprint and original opaque operation reference and returns no reconciliation reference. A prior unknown outcome returns unknown with its original reconciliation reference and never becomes success. A prior known terminal failure returns the same allow-listed category, reason, retry posture, safe fingerprint, and original opaque operation reference; it never creates another Git object. Conflict and expired admission stop before source resolution. Opaque operation and reconciliation references are non-empty allow-listed identifiers of at most 128 ASCII characters and are never parsed as GitHub locators.

The downstream seam reserves an opaque operation reference durably before mutation dispatch and returns one of `acquired`, `pending`, `replay_success`, `replay_unknown`, `replay_known_failure`, `conflict`, or `unavailable`. Only `acquired` owns dispatch. Its opaque reference and generation are revalidated immediately before the first GitHub mutation; loss, mismatch, cancellation, or unavailability finalizes the reservation with a safe no-dispatch result. `pending` returns the existing identity without dispatch. This story defines the persistence-neutral protocol and fail-closed implementation but no durable store; production remains fail-closed when it is unavailable.

After dispatch, recording uses a five-second internal token rather than caller cancellation. Staging records its resulting private tree identity before public success. Commit creation records and receives acknowledgement for its private commit identity before the ref update begins. Null, exception, timeout, cancellation, generation mismatch, or negative acknowledgement produces safe unknown evidence tied to the already-reserved opaque identity and prevents ref movement when it occurs before ref dispatch. A proven ref rejection records the known terminal failure and unreachable commit; timeout, cancellation, malformed response, rate limit, or 5xx after ref dispatch records unknown/reconciliation-required and is never retryable as a second commit. A provider crash can leave `pending` with staged/commit evidence; replay returns that same identity and never dispatches, leaving durable recovery to downstream orchestration.

Staging writes immutable Git objects and does not move a ref. An ambiguous staging result therefore does not fabricate branch status; it returns unknown, forbids redispatch, and leaves durable quarantine or a future Git-object-specific reconciliation strategy to downstream ownership. Empty change sets, duplicate normalized paths, and ancestor/descendant path conflicts are rejected; otherwise the original order is preserved. The five-check/15-minute status sequence applies only to commit/ref outcomes with privately recoverable intended-commit evidence. The provider validates caller-authoritative check evidence bound to the operation reference, exact full ref, expected head, intended commit, check number `1..5`, and window start against an injected clock; it performs exactly one read-only observation and does not claim ownership of durable scheduling or atomic budget consumption.

Status uses an exact decision table. A response for another full ref, a non-commit object, a deleted ref, or a head other than the expected or intended commit is `conflicting` and non-retryable. The exact intended commit is `confirmed`; the unchanged expected head is `not_applied`; concealed not-found, timeout, rate-limit, 5xx, and malformed read evidence are `unavailable`. Only `not_applied` and `unavailable` on checks 1 through 4 before the 15-minute deadline are retryable. Check 5, an expired window, or conflicting evidence becomes `reconciliation_required` without another mutation.

Raw GitHub paths escape every dynamic segment. GitHub's [Git Trees API](https://docs.github.com/en/rest/git/trees) defines `sha: null` as a request-side deletion marker, returns a created tree object, and caps recursive reads at 100,000 entries/7 MB. The adapter therefore parses the returned root SHA, reads that tree back, and validates resulting state only for touched paths: add/change must be `blob` mode `100644` with the expected blob SHA, and remove must be absent. Untouched executable blobs, symlinks, submodules, and nested trees are allowed. A truncated recursive read falls back to non-recursive traversal of touched-path ancestors and fails closed if any fallback response is truncated, oversized, malformed, or over budget. The provider caps this traversal at 64 requests, 256 entries per response, depth 32, 7 MiB per response, and five elapsed seconds. Caller change sets are capped at 100 entries, 1 MiB of decoded content per file, and 10 MiB aggregate content using subtraction-before-addition overflow checks before hashing, base64 allocation, or dispatch.

Commit creation supplies an exact message, returned staged-tree SHA, and one expected parent; author and committer are omitted deliberately so GitHub supplies them. The returned commit SHA is read back and accepted only when its tree, sole parent, and safe message binding reproduce the request. The ref update always uses `force: false`; its response is insufficient for success, so one GET of the escaped exact full ref must report `object.type == "commit"` and the intended commit SHA. Mutation POST/PATCH disconnects, cancellation, rate limits, malformed success, and 5xx after dispatch are non-retryable ambiguity. Read-only verification/status failures follow the status retry table and never authorize mutation replay. Transport body limits and handler-construction failures map to non-retryable known capability/size or provider categories as appropriate, not caller content-policy failures. The raw client reuses factory-managed HTTP infrastructure, accepts both delta and date-form `Retry-After`, and sends bearer authorization, user-agent, GitHub media type, and API-version headers on every request.

Public reason codes are selected from operation-specific allowlists; character shape alone is not safety evidence. Resolver, credential, transport, and public failure factories replace unknown values with stable fallbacks, reject/clamp invalid retry intervals, and never reflect arbitrary lowercase text, URLs, tokens, provider bodies, or control characters.

Production registration removes or replaces only descriptors that represent the canonical GitHub provider, including type, instance, factory, and shorter-lived GitHub registrations. It preserves custom and non-GitHub providers in `IEnumerable<IGitProvider>`, registers one concrete singleton `GitHubProvider`, and binds the canonical direct/capability resolution path to that singleton without constructing arbitrary registrations during service setup.

## Verification

**Commands:**
- `dotnet restore tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -m:1 -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false` -- expected: restore succeeds with repository pins in the Release/NuGet dependency mode used by CI.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -c Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -p:MinVerVersionOverride=1.0.0` -- expected: zero warnings and errors.
- `./tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests -noLogo -noColor -class Hexalith.Folders.Tests.Providers.GitHub.GitHubProviderTests -class Hexalith.Folders.Tests.Providers.GitHub.OctokitGitHubApiClientTests -class Hexalith.Folders.Tests.Providers.GitHub.GitHubDependencyGuardTests` -- expected: focused adapter suite passes.
- `./tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests -noLogo -noColor` -- expected: the complete owning test project passes.
- `dotnet restore Hexalith.Folders.slnx -m:1 -p:Configuration=Release -p:UseHexalithProjectReferences=false -p:NuGetAudit=false` -- expected: every solution project has Release assets.
- `dotnet build Hexalith.Folders.slnx -c Release --no-restore -m:1 -p:UseHexalithProjectReferences=false -p:MinVerVersionOverride=1.0.0` -- expected: solution build passes with zero warnings and errors.
- `git diff --check 5c01c5870a099362582e02637654afc0286cf20b` -- expected: no whitespace errors in the complete reviewed change.
- `git diff --exit-code 5c01c5870a099362582e02637654afc0286cf20b -- _bmad-output/implementation-artifacts/sprint-status.yaml` -- expected: no story-authored change to the orchestrator-owned file; never write or revert it.
