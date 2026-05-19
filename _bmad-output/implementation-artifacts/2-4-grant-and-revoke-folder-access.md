# Story 2.4: Grant and revoke folder access

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tenant administrator,
I want to grant and revoke folder access for permitted principals,
so that access to folders can evolve without changing repository bindings.

## Terms

- Folder ACL override means folder-scoped permission state for one folder and one principal. It layers over the Story 2.2 organization ACL baseline but does not replace or rewrite organization ACL events.
- Permitted principal means a metadata-only `user`, `group`, `role`, or `delegated_service_agent` subject whose ID passes the same opaque identifier validation style used by Story 2.2. Principal kinds and IDs are validation inputs only; events, results, diagnostics, and tests must not embed user emails, group display names, role display names, delegated service claims, or principal token payloads.
- Folder access action means one closed lower-snake-case domain token from the MVP ACL vocabulary: `configure_provider_binding`, `prepare_workspace`, `lock_workspace`, `read_metadata`, `read_file_content`, `mutate_files`, `commit`, `query_status`, `query_audit`, and `view_operations_console`. `create_folder` remains organization-baseline scope and must not be granted as a folder override.
- Authoritative tenant context means the managed tenant ID supplied by the authenticated execution context or EventStore envelope. Tenant IDs in request bodies, routes, query strings, or client-controlled headers are validation inputs only.
- ACL administrator evidence means tenant-access evidence from Story 2.1 plus Story 2.2 organization ACL evidence proving the actor can manage folder access for the target tenant and folder scope. Story 2.4 records direct folder override grants and revokes only; it does not rewrite organization ACL baseline events, compute inherited/effective access, or decide folder-vs-organization precedence for Story 2.5.
- Effective ACL metadata means the replayable folder-level grant/revoke state needed by later authorization and Story 2.5 effective-permission inspection. This story may add internal projection helpers but does not add public effective-permission endpoints.
- C7 freshness budget means the revocation propagation/revalidation window defined by architecture concern C7. This story records revocation metadata and tests the domain/projection signals needed to honor that budget; lock revalidation implementation remains in later workspace-lock stories.
- Protected resource touch means constructing or loading folder streams, idempotency records, projections, diagnostics, audit resources, provider readiness, repositories, workspaces, files, or cache entries tied to a target folder/principal/action. Rejected tenant and ACL evidence must fail before any protected resource touch, even when the requested folder or principal would otherwise be valid.

## Acceptance Criteria

1. Given Story 2.3 folder creation exists, when this story is implemented, then folder-level grant and revoke command, event, state-apply, result, and rejection types are added under `src/Hexalith.Folders/Aggregates/Folder/` without changing folder identity or repository binding state.
2. Given authoritative tenant context, allowed tenant evidence, existing folder state, and ACL administrator evidence are present in that order, when a grant command is accepted, then metadata-only folder ACL grant events are recorded for permitted principals and folder access actions.
3. Given authoritative tenant context, allowed tenant evidence, existing folder state, and ACL administrator evidence are present in that order, when a revoke command is accepted, then metadata-only folder ACL revoke events are recorded and projected so subsequent authorization evidence can deny revoked access within the C7 freshness budget. Revocation freshness evidence must include a monotonic event version/sequence or projection watermark; implementations must not fall back to a stale grant view when the revoke event is present but projection freshness cannot be proven.
4. Given tenant identity appears in command payloads, route values, query parameters, ordinary headers, forwarded headers, metadata bags, or any client-controlled envelope field, when grant or revoke command context is built, then tenant authority comes only from authentication context or EventStore envelopes; mismatched or competing tenant values reject with indistinguishable metadata-only denial evidence before folder stream-name construction, idempotency lookup, stream load, append, projection update, diagnostics, audit lookup, provider readiness, repository access, workspace access, or file access.
5. Given tenant-access evidence is stale, unavailable, disabled, unknown, malformed, future-dated, replay-conflicting, tenant-mismatched, denied, or missing authoritative tenant context, when grant or revoke is evaluated, then it rejects before stream-name construction, idempotency lookup, folder stream load, append, state mutation, projection update, diagnostics, audit lookup, provider readiness, repository access, workspace access, or file access.
6. Given ACL administrator evidence is denied, unavailable, malformed, stale, tenant-mismatched, folder-mismatched, lacks `query_status`/`query_audit` where needed for evidence, or lacks the explicit manage-access permission used by the implementation, when grant or revoke is evaluated, then it rejects with stable metadata-only evidence before idempotency lookup, folder stream load, append, state mutation, projection update, diagnostics, audit lookup, provider readiness, repository access, workspace access, or file access. The manage-access proof must come from the Story 2.2 organization ACL evidence model or an existing shared authorization vocabulary; this story must not create an unreviewed folder-level management action token as a shortcut. The rejection must not reveal whether the folder exists, whether the principal exists, whether access was already granted, or whether a revoke would be a no-op.
7. Given a grant or revoke command includes unsupported action names, localized labels, display names, aliases, mixed-case variants, provider-specific verbs, `create_folder`, duplicate conflicting entries, malformed principal kind or ID, missing folder ID, reserved `system` tenant, or invalid correlation/task/idempotency metadata, when validation runs, then the command rejects with stable result codes and no durable side effects.
8. Given exact duplicate ACL entries appear in one command, when entries are canonicalized, then exact duplicate grant tuples or exact duplicate revoke tuples collapse deterministically; same-tuple grant/revoke conflicts in the same command reject before idempotency lookup or stream access.
9. Given the same idempotency key and equivalent canonical grant or revoke payload are retried after tenant and ACL administrator gates pass, when the command is processed, then the same logical result is returned without duplicating events; given the same idempotency key and materially different payload are processed, then the command rejects as `idempotency_conflict` and appends nothing. Equivalence includes operation type, authoritative tenant ID, folder ID, principal kind, principal ID, strict action token, actor safe identifier, and any implementation-owned scope metadata; correlation ID, task ID, timestamps, transport ordering, display metadata, and diagnostic-only fields do not create a new semantic payload.
10. Given an already-present grant is granted again with a different idempotency key, when state is evaluated after all authorization gates pass, then the command returns deterministic `already_applied` no-op evidence without appending a duplicate grant event; given an absent grant is revoked after all authorization gates pass, then the command returns deterministic `missing_entry` no-op evidence without appending a revoke event or inventing prior access. Replaying a prior revoke is separate from revoking absent access and must preserve the prior revocation event metadata during deterministic replay. No-op evidence must be indistinguishable from denial evidence until authorization proves the caller may observe the folder ACL tuple.
11. Given a revoke races with a grant or another revoke for the same tenant, folder, principal, and action, when expected-version or append-conflict evidence is observed, then the command re-reads safe state after authorization, validation, and idempotency checks and returns stable applied/no-op/conflict evidence without appending duplicate or contradictory ACL events.
12. Given folder ACL events, results, logs, traces, metrics, projections, audit records, or test failure messages are produced, when metadata is inspected, then only tenant ID, folder ID, principal kind, principal ID, action, operation intent, result code, actor safe identifier, correlation/task/idempotency IDs, version/sequence/watermark, C7 freshness metadata, and timestamps are allowed; names, emails, group display names, provider tokens, credential material, repository names, branch names, file paths, file contents, diffs, generated context payloads, raw command bodies, raw auth headers, arbitrary tenant configuration, exception messages containing user input, and unauthorized resource existence are forbidden.
13. Given folder ACL events are replayed, when the projection or state helper derives effective folder override metadata, then it is deterministic by tenant, folder, principal kind, principal ID, action, operation intent, version/sequence, watermark, revocation timestamp, and revocation correlation/idempotency metadata; it does not depend on localized text, event class names, wall-clock reads inside aggregate logic, or provider/workspace state.
14. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules, when unit and smoke tests execute, then grant/revoke validation, tenant and ACL gates, idempotency, duplicate/no-op behavior, revocation projection, metadata leakage boundaries, and side-effect negative controls are covered with in-memory fakes and spies.
15. Given this story owns folder-level grant/revoke only, when implementation is complete, then it exposes no mutation path outside the domain command/aggregate surface introduced here and does not implement public effective-permission query endpoints, folder archive behavior, provider readiness, repository binding, workspace preparation, locks, file mutation, commits, context query, CLI/MCP/UI commands, workers, production Dapr policy mapping, repair workflows, local-only folder mode, webhooks, brownfield adoption, multi-organization-per-tenant behavior, operations-console mutation paths, or operations-console grant/revoke UI.

## Tasks / Subtasks

- [x] Extend the Folder aggregate ACL surface. (AC: 1, 2, 3, 10, 13)
  - [x] Add folder ACL command types such as `GrantFolderAccess` and `RevokeFolderAccess`, or local equivalents aligned with EventStore naming.
  - [x] Add metadata-only events such as `FolderAccessGranted` and `FolderAccessRevoked`, or local equivalents.
  - [x] Extend `FolderState` and event application so folder-level ACL override state replays deterministically from grant and revoke events.
  - [x] Keep folder stream names in the Story 2.3 `{managedTenantId}:folders:{folderId}` shape; do not derive IDs from display names, folder paths, repository names, provider names, or tenant names.
  - [x] Keep organization ACL baseline state separate from folder ACL overrides. Story 2.2 events are evidence inputs, not state to rewrite from folder commands.
- [x] Define folder ACL value objects and result evidence. (AC: 2, 3, 7, 8, 12)
  - [x] Define principal kinds `user`, `group`, `role`, and `delegated_service_agent` as strict domain tokens.
  - [x] Validate supported principal kind separately from opaque principal ID format; reject empty, whitespace, malformed, unsupported, localized, display-name, email-shaped where prohibited, case-variant, or embedded-claim principal values without leaking the raw value.
  - [x] Define the folder-level action vocabulary as strict lower-snake-case domain tokens and exclude `create_folder` from folder overrides.
  - [x] Add result/rejection codes for accepted, already_applied, missing_entry, duplicate_entry, conflicting_entry, unsupported_action, invalid_principal, invalid_folder, invalid_tenant, reserved_tenant, missing_authoritative_tenant, tenant_access_denied, stale_projection, unavailable_projection, unknown_tenant, disabled_tenant, malformed_evidence, tenant_mismatch, folder_acl_denied, acl_evidence_unavailable, folder_not_found, idempotency_conflict, idempotency_unavailable, append_conflict, and validation_failed.
  - [x] Ensure `folder_not_found`, `already_applied`, and `missing_entry` are observable only after tenant and ACL administrator gates pass; before those gates, return the same safe denial family used for unauthorized callers.
  - [x] Document command/result semantics in code or tests so accepted grants append exactly one grant event, accepted revokes append exactly one revoke event, already-applied grants append no event, missing-entry revokes append no event, idempotent replays append no event, conflicts append no event, and failed gates append no event.
  - [x] Ensure result evidence exposes stable codes and safe identifiers only; tests must not parse exception text, localized strings, diagnostic messages, or event type names.
  - [x] Reject unsupported action aliases rather than normalizing them into accepted permissions.
- [x] Add fail-closed tenant and ACL administrator gates. (AC: 4, 5, 6)
  - [x] Consume Story 2.1 tenant-access evidence before stream-name construction, idempotency lookup, stream load, append, projection update, diagnostics, audit lookup, provider readiness, repository access, workspace access, or file access.
  - [x] Consume Story 2.2 organization ACL baseline evidence before any folder stream access. If implementation needs a narrow seam, model allowed, denied, unavailable, stale, malformed, tenant-mismatched, folder-mismatched, and unsupported-action evidence without duplicating Story 2.2 aggregate logic.
  - [x] Add explicit negative coverage for tenant IDs supplied through command payload, route values, query parameters, ordinary headers, forwarded headers, metadata bags, and client-controlled envelope fields.
  - [x] Treat tenant IDs from route/body/query/header as comparison values only.
  - [x] Ensure non-allowed evidence returns metadata-only stable codes and does not reveal whether the target folder exists or whether a grant was already present.
  - [x] Add protected-resource-touch spies for every denied tenant and denied ACL evidence branch so tests prove stream-name, idempotency, stream-load, append, projection, diagnostics, audit, provider, repository, workspace, file, and cache seams were not invoked.
  - [x] Keep the aggregate pure; application/domain-service seams perform evidence checks and stream access.
- [x] Add idempotency, canonicalization, and concurrency behavior. (AC: 8, 9, 10, 11)
  - [x] Canonicalize command type, operation intent, tenant, folder ID, principal kind, principal ID, action, actor safe identifier, and optional scope metadata using culture-invariant rules before comparing idempotency payloads.
  - [x] Exclude correlation ID, task ID, timestamps, transport ordering, display-only metadata, and diagnostic-only fields from semantic idempotency equivalence while still preserving safe correlation/task IDs in result evidence.
  - [x] Treat raw JSON order, repeated exact entries, transport formatting, and casing noise around display-only metadata as non-semantic only when the relevant token validators allow it; strict ACL tokens remain case-sensitive lower-snake-case.
  - [x] Ensure grant and revoke operations for the same ACL tuple never share an idempotency equivalence class.
  - [x] Fail closed with `idempotency_unavailable` when an introduced idempotency boundary cannot prove equivalence after authorization succeeds; do not fall through to append.
  - [x] Resolve expected-version or append races by re-reading safe authorized state after tenant, ACL, validation, and idempotency gates and returning deterministic applied, no-op, or conflict evidence.
  - [x] Include tenant scope in every durable idempotency, duplicate-detection, cache, or operation key introduced by this story.
- [x] Add folder ACL projection or replay helper only as needed. (AC: 3, 13)
  - [x] Derive effective folder override metadata from stream/envelope tenant evidence plus event version/sequence, not from mutable payload tenant authority.
  - [x] Preserve revocation timestamp, event version/sequence/watermark, actor safe identifier, correlation ID, task ID, idempotency key reference, principal kind/ID, action, and operation intent for later C7 freshness enforcement.
  - [x] Treat stale or unavailable folder ACL projection freshness as deny/unknown evidence for later authorization consumers rather than falling back to the last known grant.
  - [x] Prove replay isolation for two tenants with matching folder IDs, principal IDs, and action tokens.
  - [x] Prove grant -> revoke -> grant and revoke -> grant -> revoke replay sequences produce deterministic final state and historical revocation metadata.
  - [x] Do not add public effective-permission query endpoints; Story 2.5 owns the public inspection surface.
- [x] Add tests and fixtures. (AC: 1-15)
  - [x] Add unit tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/` for grant, revoke, duplicate grant, revoke missing grant, unsupported action, invalid principal, reserved tenant, folder-not-found evidence, and stream-name shape.
  - [x] Add authorization gate tests for every Story 2.1 non-allowed tenant evidence outcome and Story 2.2 ACL administrator evidence outcome.
  - [x] Add paired short-circuit tests where tenant evidence passes and ACL evidence fails, and where tenant evidence fails before ACL evidence is queried, to prove gate order and no audit/diagnostic side effects.
  - [x] Add side-effect negative-control tests named like `RejectsBeforeStreamNameWhenTenantMissing`, `RejectsBeforeLoadWhenAclEvidenceDenied`, and `RejectsBeforeAppendWhenPrincipalInvalid`, or local equivalents that encode the forbidden side-effect boundary.
  - [x] Add idempotency tests for equivalent replay, same key plus changed operation intent, same key plus changed principal/action/folder, already-present grant with a different key, missing revoke, idempotency store unavailable, and append-conflict race handling.
  - [x] Add idempotency and race tests proving same folder ID, principal ID, command/idempotency key, and action in different tenants do not dedupe, leak, or reuse ACL evidence.
  - [x] Add replay tests proving grants and revokes derive deterministic folder override state and revocation metadata, including cross-tenant same-folder-ID isolation and repeated replay of the same stream.
  - [x] Add table-driven duplicate/conflict matrix tests for duplicate grant, duplicate revoke, same-command grant/revoke conflict, stale expected version, and append conflict outcomes.
  - [x] Add leakage tests with sentinel values for credential material, provider tokens, repository names, branch names, file paths, file contents, diffs, generated context payloads, user emails, group display names, raw auth headers, arbitrary tenant configuration, and unauthorized resource names.
  - [x] Add leakage sentinel values in command metadata, tenant evidence, ACL evidence, principal token-like inputs, and denial reasons; assert denied results expose only allowed metadata fields.
  - [x] Add exception-path leakage tests for validation, idempotency, append-conflict, and projection-unavailable paths; test failures must not include raw command bodies, raw auth headers, emails, display names, provider/repository/file values, or arbitrary tenant configuration.
  - [x] Extend `src/Hexalith.Folders.Testing/Factories/*` only with reusable folder ACL builders that delegate to production validation rules.
  - [x] Add conformance tests in `tests/Hexalith.Folders.Testing.Tests` if new testing helpers are introduced.
  - [x] Use pure in-memory fakes and spies only for EventStore seams, tenant evidence, organization ACL evidence, idempotency records, validators, clock/time, diagnostics sinks, and audit sinks. Do not use Dapr, EventStore server, databases, network calls, generated SDK/OpenAPI, CLI/MCP/UI/workers, provider adapters, or nested submodule initialization.

### Review Findings

Findings from `/bmad-code-review 2.4` (Blind Hunter + Edge Case Hunter + Acceptance Auditor) over commit `0fa8c64`.

- [x] [Review][Decision] Grant vs Revoke partial-success asymmetry — `Grant` returns `Accepted` and emits only new events when some tuples already exist; `Revoke` returns `MissingEntry` only when zero events emit (whole-command). AC10 covers single-op semantics but multi-op partials are ambiguous. Choose: (a) keep asymmetry, (b) align both to per-op semantics with a partial-result code, or (c) reject whole-command on any-already-applied / any-missing.
- [x] [Review][Decision] `PrincipalKindToken` behavior on undefined enum during replay — currently throws `InvalidOperationException` and poisons the entire stream replay. Choose: (a) keep fail-loud (deny on stream corruption); (b) tolerate unknown values for forward-compat (e.g., emit opaque `"unknown:N"` token and skip override entry).
- [x] [Review][Decision] `AppendConflict` ambiguity when racing Revoke wins — `ResolveAppendConflict` re-reads state; if the racing event leaves no new work, original caller gets `AppendConflict` indistinguishably from a "real" conflict. Choose: (a) keep code as-is, (b) split into `AppendConflictRetryable` / `AppendConflictResolved`, or (c) document trade-off in code+tests.
- [x] [Review][Patch] Add revocation/grant timestamp to events + projection [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessGranted.cs`, `FolderAccessRevoked.cs`, `FolderAccessOverride.cs`, `Projections/FolderAccess/FolderAccessProjection.cs`] — story task list requires "Preserve revocation timestamp, event version/sequence/watermark..." but no timestamp field exists. C7 freshness can't be computed from sequence alone.
- [x] [Review][Patch] Preserve historical revocation metadata across grant→revoke→grant replay [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:96-120`] — `RecordAccessOverride` overwrites dict entry wholesale; prior revoke's `CorrelationId`/`TaskId`/`IdempotencyKey`/`AccessSequence` are lost. Spec task: "historical revocation metadata". Add a `RevocationHistory` list on `FolderAccessOverride` and assert it survives grant→revoke→grant.
- [x] [Review][Patch] `IsSafeEvidenceIdentifier` substring filter silently nulls valid tenant IDs [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:302-311`] — `Contains("auth")`, `Contains("display")`, `Contains("branch")`, `Contains("email")`, `Contains("/")` blocks legitimate IDs like `tenant-authority`, `acme-display-prod`, `tenant-branch-1`. Use anchored/whole-word matching, or scope the check to inputs that have not yet passed `FolderStreamName` validation.
- [x] [Review][Patch] `HasCompetingClientTenant` does not check `command.ManagedTenantId` [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs:47, 120-131`] — command's own `ManagedTenantId` is silently rebound via `WithManagedTenantId(tenantAccess.TenantId)` without verifying it matches authoritative tenant. Include `command.ManagedTenantId` in the competing-tenant check; add test `RejectsBeforeStreamNameWhenCommandManagedTenantIdDiffersFromAuthoritativeTenant`.
- [x] [Review][Patch] `EvaluateAcl` accepts Allowed evidence with null/whitespace `Action` only by accident [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs:141`] — `string.Equals(null, ManagementAction, Ordinal)` returns false → mismatch (correct outcome), but no explicit guard. Reject `string.IsNullOrWhiteSpace(aclEvidence.Action)` as `AclEvidenceUnavailable` before the Outcome switch.
- [x] [Review][Patch] Tenant-denied path echoes authorizer-supplied `tenantAccess.TenantId` [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs:16-27`] — on the pre-pass `!IsAllowed` branch, pass `null` for `managedTenantId` (caller was not authorized to know whether tenant exists). Add a leakage test asserting denied tenant identity does not appear in result.
- [x] [Review][Patch] `Math.Max(state.AccessSequence, event.AccessSequence)` allows out-of-order events to overwrite per-override sequence with smaller value [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:63, 78, 96-120`] — in `RecordAccessOverride`, replace existing entry only if `accessSequence > existing.AccessSequence`; otherwise drop the event or fail with a stable code. Add test for out-of-order replay.
- [x] [Review][Patch] Test tautology in `AppendConflictShouldRereadAndReturnAlreadyAppliedWhenConcurrentGrantWon` [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessIdempotencyTests.cs`, `RecordingFolderRepository.cs`] — `EventsAppended == 1` is satisfied by the test-harness's simulated append, not by gate behavior. Track concurrent appends in a separate counter and assert `EventsAppended == 0` after `AppendConflict`.
- [x] [Review][Patch] `Map(TenantAccessOutcome.Allowed)` returns `MalformedEvidence` [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs:158-172`] — reachable only when `Outcome == Allowed` but `IsAllowed == false` (contract violation upstream). Throw `InvalidOperationException` for this branch instead of coercing to a generic code.
- [x] [Review][Patch] `FolderResult.Accepted` and `Rejected` drop valid action echoes when `IsSupported` returns false [`src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs:52, 123`] — separates "structurally valid" from "semantically supported"; use a separate echo-safe predicate when surfacing the action in accepted/rejected payloads to avoid losing context on legitimate but borderline values.
- [x] [Review][Patch] Dead default branch in `FolderCommandValidator` operation-intent dispatch [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:~867`] — `_ => (FolderAccessOperationIntent)(-1)` plus `Enum.IsDefined` check is unreachable; only two `IFolderAccessCommand` implementers exist. Throw `InvalidOperationException` in `_ =>` or remove the redundant guard.
- [x] [Review][Patch] `IFolderAccessCommand.WithManagedTenantId` returns base `IFolderCommand`; gate runtime-casts back [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs:47`, `IFolderAccessCommand.cs`] — add typed `IFolderAccessCommand WithAuthoritativeTenant(string)` on the interface to remove the runtime cast.
- [x] [Review][Patch] `EmptyClientTenantIds` double-wraps an empty dictionary [`src/Hexalith.Folders/Aggregates/Folder/EmptyClientTenantIds.cs`] — replace with `FrozenDictionary<string, string?>.Empty`.
- [x] [Review][Patch] `FolderStateApply.RecordAccessOverride` calls `ToFrozenDictionary()` per event [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:96-120`] — O(N²) cost on long replay streams. Accumulate into a regular `Dictionary` during a replay batch and freeze once at the end, or relax the public state type to expose a non-frozen snapshot for intermediate transitions.
- [x] [Review][Patch] `FolderAccessAclEvidence` public constructor allows constructing `Allowed` evidence with an arbitrary `Action` [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessAclEvidence.cs`] — make ctor internal/private; expose `Allowed(...)` factory that hard-pins `Action = ManagementAction`.
- [x] [Review][Patch] `tupleIntents` mixed-intent check in `ValidateAndCanonicalizeAccessOperations` is unreachable [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:~242-247`] — `ValidateOperation` already rejects cross-intent ops via `requiredIntent`. Remove or annotate as defense-in-depth with an `Unreachable` test guard.
- [x] [Review][Patch] Asymmetric `Rejected` overloads in pre/post-tenant paths [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs:18-27, 36-44`] — standardize on the 11-arg overload with explicit `null` for principal/action to prevent drift if a future maintainer copies the pattern across branches.
- [x] [Review][Patch] AC7 vocabulary: `ConflictingEntry` missing from `FolderResultCode`; same-tuple grant/revoke in one command reuses `ReplayConflict` [`src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`, `FolderCommandValidator.cs:~246`] — add `ConflictingEntry` enum member and map same-command grant/revoke conflicts to it to match the spec's required token.
- [x] [Review][Patch] `EvaluateAcl` Allowed-with-mismatched-scope returns `AclEvidenceMismatch`, same as `FolderMismatch` [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs:142, 149`] — fold Allowed-mismatch into `AclEvidenceUnavailable` to fully equalize denial vs mismatch evidence per AC6's safe-denial guidance.
- [x] [Review][Patch] Add lock-in test for idempotent replay returning `AlreadyApplied` even after state-level revoke [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessIdempotencyTests.cs`] — documents the standard "same request → same response" trade-off; name e.g. `IdempotentReplayReportsAlreadyAppliedEvenIfStateLaterRevoked`.
- [x] [Review][Patch] Add concurrent-grant `AccessSequence` collision test [`tests/Hexalith.Folders.Tests/Aggregates/Folder/`] — drive two `FolderAggregate.Handle` calls against the same starting state; assert the production repository would reject the second (the in-memory `RecordingFolderRepository` does not enforce sequence uniqueness today).
- [x] [Review][Patch] Add cross-tenant projection mixing guard test [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessProjectionReplayTests.cs`] — assert `FolderAccessProjection.FromEvents("tenant-a", "folder-a", eventsWhereOneEventBelongsToTenantB)` throws; currently guarded only by `FolderState.Apply` exception with no explicit test.
- [x] [Review][Patch] Add doc + test asserting fingerprint depends on post-dedup canonical operations [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:817-839`] — locks in the invariant so future dedup changes break the test instead of silently breaking idempotency for already-recorded fingerprints.
- [x] [Review][Patch] Add `IsSupported` rejects-leading-trailing-whitespace test [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessCommandValidationTests.cs`] — verifies the intended exclusion of `" read_metadata"`-style inputs; current behavior is correct but unproven.
- [x] [Review][Defer] Async port for I/O boundaries [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs`] — deferred, pre-existing; `IFolderRepository` is synchronous (Story 2.3 decision). Revisit when EventStore integration story replaces the repository with an async port.
- [x] [Review][Defer] Idempotency key collision risk when future fields are added to `FolderAccessOperation` [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:874-907`] — deferred, theoretical until new significant fields exist; revisit when extending `FolderAccessOperation`.
- [x] [Review][Defer] Rename test `AlreadyGrantedAccessShouldReturnAlreadyAppliedWithoutDuplicateEvent` to reflect the actual short-circuit path [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessCommandValidationTests.cs:~1508`] — deferred, cosmetic. The test passes correctly via the `HasFolderAccess` short-circuit; rename next time the file is touched.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.4 foundation: a tenant administrator grants and revokes folder access for permitted principals so folder access can evolve without changing repository bindings; revoked access must be honored within the C7 freshness budget. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.4`]
- PRD FR5 requires tenant administrators to grant folder access to users, groups, roles, and delegated service agents; FR6 gives Story 2.5 the public effective-permissions inspection surface; FR9 and FR10 require safe denial before exposing resources and metadata-only authorization evidence. [Source: `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`]
- Architecture maps FR4-FR10 authorization work to `src/Hexalith.Folders/Authorization/`, tenant-access projections, and EventStore authorization boundaries; FR11-FR14 folder lifecycle work lives under `src/Hexalith.Folders/Aggregates/Folder/`. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`]
- Architecture concern C7 defines mid-task authorization revocation as a lock-model freshness requirement. This story must emit enough revocation evidence for later lock revalidation but must not implement workspace locks. [Source: `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns`]
- Project context requires zero cross-tenant leakage and metadata-only events, logs, traces, metrics, projections, audit records, console responses, provider diagnostics, and errors. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and tenant-access authorizer outcomes. Reuse its outcome vocabulary and evidence fields; do not create a second tenant authority model.
- Story 2.2 defines organization-level ACL baseline grants and revokes for `user`, `group`, `role`, and `delegated_service_agent` principals, strict lower-snake-case action tokens, metadata-only ACL events, culture-invariant canonicalization, and negative controls proving rejected paths do not access streams or diagnostics.
- Story 2.3 defines `FolderAggregate`, opaque folder identity, `{managedTenantId}:folders:{folderId}` stream names, logical active lifecycle state, tenant and ACL pre-load gates for create-folder, deterministic duplicate/idempotency behavior, and minimal folder replay evidence.
- Story 2.4 must build on Story 2.3 folder state and Story 2.2 ACL evidence. It must not fold folder ACL overrides back into organization baseline state or implement Story 2.5 public permission inspection.
- Story 2.4 must keep observable no-op outcomes behind successful authorization. Unauthorized callers get safe denial evidence, not `already_applied`, `missing_entry`, or `folder_not_found`, because those codes disclose folder/principal/ACL state.

### Existing Implementation State

- `src/Hexalith.Folders/FoldersModule.cs` currently exposes scaffold metadata only in the baseline repo; domain aggregate work may arrive from Stories 2.1-2.3 before this story is implemented.
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs` already validates stream-name segments and exposes `FolderStreamName` as `{ManagedTenantId}:folders:{FolderId}` plus `OrganizationStreamName` as `{ManagedTenantId}:organizations:{OrganizationId}`. Reuse or extend this pattern rather than creating a second stream naming convention.
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs` provides managed tenant and permission test data. Extend it only if folder ACL tests need reusable builders.
- `tests/Hexalith.Folders.Tests/FoldersModuleSmokeTests.cs` is scaffold-level; this story should add focused aggregate and authorization-gate tests without requiring external services.
- Generated SDK files under `src/Hexalith.Folders.Client/Generated/` are build outputs from the Contract Spine and must not be hand-edited.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Folder ACL aggregate logic, command validation, idempotency equivalence, tenant/ACL gates, and EventStore command handling belong in `Hexalith.Folders` and tests.
- Keep aggregates pure. The aggregate applies commands, validates in-memory invariants, and returns events/results. Authorizers, projections, Dapr, EventStore I/O, provider calls, filesystem, Git, UI, CLI, MCP, workers, diagnostics, and audit sinks stay outside aggregate state transitions.
- Authorization order for these mutations is authoritative tenant context, Story 2.1 tenant evidence, Story 2.2 ACL administrator evidence, input validation, idempotency equivalence, folder stream load, aggregate mutation, append, and projection/audit side effects.
- Unauthorized, stale, unavailable, malformed, tenant-mismatched, folder-mismatched, invalid, duplicate-conflicted, and idempotency-conflicted paths must not reveal folder existence, prior ACL state, idempotency records, stream names, provider/repository state, workspace state, or audit resource existence.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals, and async APIs where I/O boundaries are introduced.
- Use stable result codes and metadata fields in tests. Do not assert localized text, exception strings, event class names, or diagnostic text.
- Cache keys and durable operational keys must carry tenant scope. A missing tenant prefix is a correctness and security bug.
- Events, logs, traces, metrics, projections, audit records, and errors must remain metadata-only and must not include raw credential values, provider tokens, file contents, diffs, generated context payloads, repository names, branch names, raw paths, user emails, group display names, or unauthorized resource existence.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Access*Command*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Access*Event*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Access*Result*.cs`
- `src/Hexalith.Folders/Authorization/*` only for narrow seams needed to consume Story 2.1 tenant evidence and Story 2.2 ACL administrator evidence.
- `src/Hexalith.Folders/Idempotency/*` only if no existing reusable equivalence helper can support folder ACL commands.
- `src/Hexalith.Folders/Projections/FolderAccess/*` or `src/Hexalith.Folders/Projections/FolderList/*` only for internal replay/effective override metadata needed by this story.
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable folder ACL builders.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*Access*Tests.cs`
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are added.

### Do Not Touch

- Do not edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`.
- Do not add or modify OpenAPI Contract Spine operations unless implementation discovers a blocking mismatch that is explicitly handled through the contract workflow.
- Do not implement public effective-permission query endpoints; Story 2.5 owns that surface.
- Do not implement provider readiness, provider adapters, repository creation, repository binding, branch policy, local-only folder mode, webhooks, or brownfield adoption.
- Do not implement workspace preparation, locks, file mutation, commits, context query, file browsing, filesystem paths, CLI, MCP, UI, workers, production Dapr policy, repair workflows, archive behavior, or multi-organization-per-tenant behavior.
- Do not make display name, path, repository name, provider name, principal display name, group name, role display name, or tenant name part of folder ACL identity.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization.
- Use xUnit v3 and Shouldly for aggregate tests; use NSubstitute only where an actual seam needs substitution.
- Add focused tests named `FolderAccessCommandValidationTests`, `FolderAccessTenantEvidenceGateTests`, `FolderAccessAdministratorAclGateTests`, `FolderAccessIdempotencyTests`, `FolderAccessProjectionReplayTests`, `FolderAccessMetadataLeakageTests`, and `FolderAccessStreamShapeTests`, or equivalent locally consistent names.
- Include side-effect negative controls proving rejected tenant evidence, rejected ACL administrator evidence, invalid principal/action values, duplicate conflicts, idempotency conflicts, idempotency unavailability, and append conflicts do not construct stream names, load streams, append events, mutate aggregate state, query diagnostics, query audit resources, call provider readiness, call repositories, inspect workspaces, or touch files.
- Include positive and negative metadata tests: allowed diagnostics may include tenant ID, folder ID, principal kind, principal ID, action, operation intent, result code, actor safe ID, correlation/task/idempotency IDs, event version/sequence/watermark, C7 freshness metadata, and timestamps; forbidden diagnostics must not include raw auth tokens, provider payloads, command bodies, repository/branch/path values, diffs, file contents, generated context, stack traces with sensitive values, user emails, group names, display names, arbitrary tenant configuration, or unauthorized resource identifiers.
- Include replay tests proving `FolderAccessGranted` and `FolderAccessRevoked` deterministically produce the same folder override state and revocation evidence from ordered event history.

### Regression Traps

- Do not grant or revoke access from a payload tenant ID, route tenant ID, query tenant ID, or client-controlled header.
- Do not silently allow access mutations when Tenants projection freshness or ACL administrator evidence cannot be proven.
- Do not treat Story 2.2 organization baseline grants as folder overrides. They are baseline evidence and authorization inputs.
- Do not add `create_folder` as a folder-level action.
- Do not let revoked access remain ambiguous. The revoke event/projection must carry enough metadata for later C7 freshness enforcement and authorization denial evidence.
- Do not expose user names, group names, emails, repository names, branch names, provider identifiers, raw paths, file contents, diffs, generated context payloads, secrets, or unauthorized resource existence in events/results/diagnostics/tests.
- Do not build public effective-permission query, UI, CLI, MCP, or operations-console mutation behavior in this story.
- Do not implement workspace lock revocation behavior here; record the domain/projection evidence that later lock stories must consume.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.4`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/planning-artifacts/architecture.md#Integration Points`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-3-create-folders-within-a-tenant.md`
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-18 | Created story with folder ACL grant/revoke commands, fail-closed tenant and ACL administrator gates, idempotency, revocation freshness evidence, metadata-only projection guidance, and offline test guardrails. | Codex |
| 2026-05-18 | Applied party-mode review hardening for tenant ingress rejection, ACL layering boundaries, command/result semantics, idempotency equivalence, replay/race coverage, and revocation evidence. | Codex |
| 2026-05-18 | Applied advanced-elicitation hardening for protected-resource-touch boundaries, safe no-op observability, revocation freshness fallback behavior, management-permission proof, and exception-path leakage tests. | Codex |
| 2026-05-19 | Implemented folder ACL grant/revoke domain surface, fail-closed access gate, replay projection helper, idempotency/concurrency handling, and focused tests. | Codex |
| 2026-05-19 | Applied `/bmad-code-review 2.4` triage: resolved 3 decision items (D1 Revoke per-op symmetry, D2 PrincipalKindToken fail-loud, D3 AppendConflict semantics) and 25 patches (added OccurredAt timestamps via TimeProvider, RevocationHistory on overrides, anchored-term identifier safety, ManagedTenantId competing-tenant rejection, tenant-denied no-echo, stale-sequence guard, ConflictingEntry enum, ACL Allowed-mismatch fold, plus 5 new coverage tests). All 383 solution tests pass. | Claude Opus 4.7 |

## Party-Mode Review

- ISO date and time: 2026-05-18T12:02:42+02:00
- Selected story key: `2-4-grant-and-revoke-folder-access`
- Command/skill invocation used: `/bmad-party-mode 2-4-grant-and-revoke-folder-access; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - Tenant authority needed an explicit ingress matrix so route, query, payload, header, forwarded-header, metadata-bag, and client-envelope tenant values fail closed before stream naming or side effects.
  - Idempotency equivalence needed exact semantic key material and an explicit exclusion for correlation/task/timestamp/display/diagnostic fields.
  - ACL layering needed a clearer boundary between Story 2.2 organization ACL evidence, direct folder override state, and Story 2.5 effective-permission interpretation.
  - Grant/revoke result semantics needed deterministic event/no-event behavior for accepted, already-applied, missing-entry, replay, conflict, and failed-gate outcomes.
  - Revocation projection metadata and replay/race tests needed to prove C7 freshness inputs without implementing locks or public effective-permission queries.
  - Leakage and negative-control tests needed stronger sentinel coverage across command metadata, evidence payloads, principal token-like inputs, and denial reasons.
- Changes applied:
  - Clarified permitted-principal metadata boundaries and delegated-service-agent leakage constraints.
  - Clarified ACL administrator evidence and the non-goal of computing inherited/effective access in this story.
  - Tightened AC4, AC6, AC9, AC10, AC11, AC13, and AC15 for tenant ingress rejection, indistinguishable denials, idempotency equivalence, no-op event behavior, append-race rereads, revocation metadata, and domain-only mutation scope.
  - Added task coverage for principal validation, command/result semantics, tenant ingress negative tests, idempotency exclusions, authorized reread-after-conflict behavior, revocation metadata fields, replay sequences, gate short-circuiting, cross-tenant dedupe/race tests, duplicate/conflict matrix tests, and leakage sentinels.
- Findings deferred:
  - Folder-vs-organization permission precedence and inherited/effective access semantics remain Story 2.5 territory.
  - EventStore concurrency mechanism details remain implementation/architecture choices; this story requires deterministic reread outcomes, not a specific mechanism.
  - Shared action-token allowlist infrastructure is deferred unless an existing local pattern makes it low-cost.
  - API-surface tests for absence of public effective-permission endpoints are optional unless the in-process routing/contract surface makes them cheap and reliable.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- ISO date and time: 2026-05-18T20:45:00+02:00
- Selected story key: `2-4-grant-and-revoke-folder-access`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-4-grant-and-revoke-folder-access`
- Batch 1 method names:
  - Security Audit Personas
  - Failure Mode Analysis
  - Pre-mortem Analysis
  - Self-Consistency Validation
  - Critique and Refine
- Reshuffled Batch 2 method names:
  - Red Team vs Blue Team
  - Chaos Monkey Scenarios
  - First Principles Analysis
  - 5 Whys Deep Dive
  - Architecture Decision Records
- Findings summary:
  - The story already carried strong fail-closed boundaries, but elicitation found remaining implementation traps around observing no-op states before authorization, using a new manage-access token instead of proven organization ACL evidence, treating stale revoke projections as harmless, and letting exception text or test failures leak raw input.
  - The risk pass also found that protected-resource-touch assertions needed to cover cache/idempotency/projection/diagnostic/audit seams, not only stream load and append.
- Changes applied:
  - Added a protected-resource-touch term to make forbidden side effects explicit.
  - Tightened revocation freshness behavior so stale or unavailable folder ACL projection evidence cannot fall back to a stale grant.
  - Clarified that management permission proof must come from Story 2.2 evidence or an existing shared authorization vocabulary, not a new unreviewed folder action token.
  - Clarified that `folder_not_found`, `already_applied`, and `missing_entry` are observable only after authorization gates pass.
  - Added protected-resource-touch spy coverage and exception-path leakage tests.
- Findings deferred:
  - Exact management permission token naming remains an implementation choice constrained by Story 2.2 or existing shared authorization vocabulary.
  - Projection freshness storage mechanics remain an implementation choice as long as monotonic version/sequence or watermark evidence is preserved.
  - Story 2.5 still owns inherited/effective permission precedence and public inspection semantics.
- Final recommendation: ready-for-dev

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-19: `dotnet test tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore` passed with 203 tests.
- 2026-05-19: `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore` passed with 43 tests; a transient apphost copy retry warning appeared because another test process briefly held the executable.
- 2026-05-19: `dotnet test Hexalith.Folders.slnx --no-restore` passed across the solution.

### Completion Notes List

- Story created by `/bmad-create-story 2-4-grant-and-revoke-folder-access` equivalent workflow on 2026-05-18.
- Project context, Epic 2, PRD, architecture, Story 2.1, Story 2.2, Story 2.3, testing factories, recent commits, and story-creation lessons were reviewed.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added folder-level ACL grant/revoke commands, strict principal/action value objects, metadata-only grant/revoke events, and replayable folder override state without changing folder identity or repository binding state.
- Added `FolderAccessTenantGate` to enforce authoritative tenant evidence, Story 2.2-style ACL administrator evidence, client-controlled tenant mismatch rejection, validation, idempotency lookup, stream load, aggregate mutation, append, and safe append-conflict reread ordering.
- Added internal folder access projection replay helper preserving access sequence/watermark and revocation metadata for later C7 freshness enforcement without adding public effective-permission endpoints.
- Added focused tests for grant/revoke validation, duplicate/conflict canonicalization, tenant and ACL gates, idempotency replay/conflict/unavailable behavior, append-conflict reread, projection replay, tenant isolation, and metadata leakage sentinels.

### File List

- `_bmad-output/implementation-artifacts/2-4-grant-and-revoke-folder-access.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/EmptyClientTenantIds.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAclEvidence.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAclOutcome.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessAction.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessEntryKey.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessGranted.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessOperation.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessOperationIntent.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessOverride.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessPrincipalKind.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessRevoked.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidationResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/GrantFolderAccess.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IFolderAccessCommand.cs`
- `src/Hexalith.Folders/Aggregates/Folder/RevokeFolderAccess.cs`
- `src/Hexalith.Folders/Projections/FolderAccess/FolderAccessProjection.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessAdministratorAclGateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessCommandValidationTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessIdempotencyTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessMetadataLeakageTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessProjectionReplayTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessTenantEvidenceGateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs`
