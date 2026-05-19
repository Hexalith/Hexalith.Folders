# Story 2.3: Create folders within a tenant

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authorized actor,
I want to create logical folders inside my tenant,
so that repository-backed workspace tasks have a tenant-scoped logical home.

## Terms

- Authoritative tenant context means the managed tenant ID supplied by the authenticated execution context or EventStore envelope. Tenant IDs in request bodies, routes, query strings, or client-controlled headers are validation inputs only.
- Tenant and ACL gates mean the ordered authorization boundary of authoritative tenant context, Story 2.1 tenant-access evidence, and Story 2.2 `create_folder` ACL evidence.
- Safe metadata means stable metadata fields that are allowed in events, results, logs, diagnostics, audit, and projections: tenant ID, folder ID, lifecycle/result code, safe display name, optional safe description/tags/path label, correlation/task/idempotency IDs, safe actor/principal ID, and event version/sequence when available.
- Opaque folder ID means an immutable generated or validated identifier that is never derived from tenant name, display name, path label, repository name, provider name, stream name, or folder hierarchy.
- Minimal replay projection means the smallest tenant-scoped read model needed to prove creation event replay for existence, lifecycle state, and safe metadata. It is not a public folder listing/query feature.
- Durable folder-create key means any idempotency, duplicate-detection, cache, or operation key introduced for this command. It must be derived only after tenant and ACL gates pass and must include authoritative tenant ID plus opaque folder ID.
- Concurrent duplicate means a same-tenant same-folder creation race observed through expected-version, append-conflict, or already-created state evidence. It is handled as deterministic duplicate/idempotency evidence, not as a second creation event or an infrastructure exception leak.

## Acceptance Criteria

1. Given the `FolderAggregate` does not yet exist, when this story is implemented, then the aggregate, state, command, event, result, and rejection types are introduced under `src/Hexalith.Folders/Aggregates/Folder/` using EventStore-oriented naming and opaque folder identity.
2. Given authoritative managed tenant context, Story 2.1 tenant-access evidence, and Story 2.2 organization ACL permission for `create_folder` are all present in that order, when `CreateFolder` is accepted, then the folder stream is created in the `{managedTenantId}:folders:{folderId}` shape with an active logical lifecycle state and safe metadata needed for later repository binding.
3. Given tenant identity appears in command payloads, routes, query parameters, or client-controlled headers, when create-folder command context is built, then tenant authority comes only from authentication context or EventStore envelopes; payload tenant IDs are validation inputs only.
4. Given folder identity is supplied or generated, when the command is processed, then folder IDs are opaque immutable values and are never derived from display name, path, repository name, provider name, or tenant name.
5. Given folder metadata is supplied, when it is accepted, then display name, optional description, optional projected path label, and optional tags are validated as metadata-only values; invalid metadata rejects before durable/cache key construction, stream loading, duplicate lookup, append, projection update, diagnostics, audit lookup, or mutation; raw file contents, diffs, generated context payloads, provider credential material, repository URLs, branch names, and unauthorized resource identifiers are rejected or omitted.
6. Given tenant-access evidence is stale, unavailable, disabled, unknown, malformed, future-dated, replay-conflicting, tenant-mismatched, denied, or missing authoritative tenant context, when `CreateFolder` is evaluated, then it rejects before stream-name construction, durable/cache key construction, stream loading, duplicate lookup, event append, aggregate mutation, projection update, audit lookup, diagnostic lookup, provider readiness checks, or repository operations.
7. Given ACL evidence does not grant `create_folder`, is unavailable, or cannot be evaluated from stable Story 2.2 result evidence, when `CreateFolder` is evaluated, then it rejects with metadata-only `folder_acl_denied` or equivalent stable evidence before durable/cache key construction, stream loading, duplicate lookup, append, projection update, diagnostics, audit lookup, or mutation.
8. Given the same idempotency key and equivalent canonical create-folder payload are retried after tenant and ACL gates pass, when the command is processed, then the same logical result is returned without duplicating events and with durable idempotency keys scoped by authoritative tenant ID; given the same idempotency key and materially different payload are processed, then the command rejects as `idempotency_conflict`; given idempotency evidence is unavailable after authorization succeeds, then the command fails closed before append without disclosing folder existence; given tenant or ACL gates fail, then prior folder existence and idempotency records are not disclosed.
9. Given a folder already exists for the same opaque folder ID in the same tenant, when create is retried without matching idempotency equivalence or races another same-folder create, then the command returns deterministic duplicate/already-exists evidence without appending a second creation event; the same opaque folder ID in a different authoritative tenant is a different stream; display-name, projected path-label, and parent-folder uniqueness are deferred and must not be introduced as duplicate rules in this story.
10. Given a folder creation result is accepted, rejected, duplicate, unauthorized, idempotency-unavailable, or idempotency-conflicted, when callers or tests inspect it, then stable result codes such as `created`, `idempotent_replay`, `idempotency_conflict`, `idempotency_unavailable`, `duplicate_folder`, `append_conflict`, `invalid_folder_id`, `invalid_folder_metadata`, `tenant_evidence_missing`, `tenant_access_denied`, `folder_acl_denied`, `acl_evidence_unavailable`, and `validation_failed` plus safe metadata are available without parsing localized text, event type names, stack traces, or diagnostic strings.
11. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodules, when unit and smoke tests execute, then folder creation, validation, tenant evidence gates, ACL gates, idempotency behavior, concurrent duplicate handling, stream shape, metadata-only leakage boundaries, and projection replay are covered with in-memory fakes and spies that fail on forbidden side effects.
12. Given this story creates logical folders only, when implementation is complete, then it does not implement provider readiness, repository creation, repository binding, workspace preparation, locks, file mutation, commits, folder ACL grant/revoke, effective-permission query endpoints, archive behavior, CLI/MCP/UI commands, workers, production Dapr policy mapping, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Tasks / Subtasks

- [x] Create the Folder aggregate domain surface. (AC: 1, 2, 4, 9)
  - [x] Add `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`.
  - [x] Add `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`.
  - [x] Add `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs` or equivalent local event-application surface if sibling EventStore conventions use a separate apply type.
  - [x] Add opaque value objects or validated identifiers for managed tenant ID and folder ID where existing project types do not already provide them.
  - [x] Keep stream names in the `{managedTenantId}:folders:{folderId}` shape and reject empty segments, `:` characters, control characters, non-canonical casing when the project validator requires canonical casing, and the reserved `system` tenant for managed folder streams.
  - [x] Keep any idempotency, duplicate-detection, cache, or operation keys in the same authoritative tenant scope; tests must fail if a durable folder-create key omits tenant ID or uses request-supplied tenant ID as authority.
  - [x] Represent the initial lifecycle as active/logical-folder-created while keeping repository binding and workspace state unset or explicitly unbound for later stories.
- [x] Define create-folder commands, events, and result evidence. (AC: 2, 5, 8, 10)
  - [x] Add `CreateFolder` command and `FolderCreated` event, or equivalent names aligned with local EventStore naming.
  - [x] Add result/rejection codes for accepted, already_exists, duplicate_folder, invalid_folder_id, invalid_folder_metadata, reserved_tenant, missing_authoritative_tenant, tenant_access_denied, stale_projection, unavailable_projection, unknown_tenant, disabled_tenant, malformed_evidence, tenant_mismatch, replay_conflict, folder_acl_denied, acl_evidence_unavailable, idempotency_conflict, idempotency_unavailable, append_conflict, and state_transition_invalid.
  - [x] Keep accepted event payloads metadata-only: tenant ID, folder ID, safe display name, optional safe description/tags, lifecycle state, idempotency/correlation/task IDs, actor/principal safe identifier, version/sequence when available, and reason/result code.
  - [x] Do not copy raw command payloads, raw request headers, authentication tokens, provider payloads, repository names, branch names, file paths, file contents, diffs, generated context payloads, arbitrary tenant configuration values, or unauthorized resource identifiers into events, results, logs, traces, metrics, audit, projections, or test failure output.
  - [x] Ensure result evidence is structured around stable result codes and safe metadata; tests must not infer behavior from exception text, localized diagnostics, or event type names.
- [x] Add tenant and ACL pre-load authorization gates. (AC: 2, 3, 6, 7)
  - [x] Consume the Story 2.1 tenant-access authorizer/projection boundary before building the folder stream name.
  - [x] Consume Story 2.2 organization ACL baseline evidence for `create_folder`; if the full Story 2.2 implementation is still in flight, define a narrow interface boundary and tests that model allowed, denied, unavailable, malformed, and stale ACL evidence without duplicating ACL aggregate logic.
  - [x] Treat tenant IDs from route/body/query/header as comparison values only, never as authority that selects the aggregate stream.
  - [x] Ensure every non-allowed tenant or ACL result rejects before stream-name construction, durable/cache key construction, stream load, duplicate lookup, append, mutation, projection update, diagnostics lookup, audit lookup, provider readiness checks, or repository operations.
  - [x] Keep authorization evidence metadata-only and stable-code based. Do not expose membership inventories, role display names, group names, user emails, raw projection payloads, or whether unauthorized folders already exist.
- [x] Add idempotency and duplicate handling. (AC: 8, 9, 10)
  - [x] Reuse the project idempotency equivalence rules established in Epic 1 and the client helper semantics already generated in `src/Hexalith.Folders.Client/Generated/`.
  - [x] Canonicalize idempotency inputs using command type, operation intent, tenant, folder ID, safe folder metadata, actor/principal safe ID, and any explicit parent/organization reference if present.
  - [x] Treat raw JSON order, casing noise around non-token display metadata, omitted optional metadata, Unicode normalization, culture-specific casing, and exact duplicate tags according to a documented culture-invariant canonicalization rule before comparing payload equivalence.
  - [x] Return the same logical result for equivalent replay with the same idempotency key without appending duplicate `FolderCreated` events.
  - [x] Reject same-key materially different payloads as `idempotency_conflict`.
  - [x] Reject or fail closed with stable `idempotency_unavailable` evidence when an introduced idempotency boundary cannot prove equivalence after tenant and ACL gates pass; do not fall through to append.
  - [x] Return deterministic `already_exists` or equivalent evidence when a folder stream already contains a creation event for the same folder ID and the request is not an equivalent replay.
  - [x] Treat expected-version or append conflicts for the same tenant/folder stream as duplicate or idempotency evidence after re-reading safe state, never as a second creation event or as leaked infrastructure exception text.
- [x] Add folder-list projection and replay support only as needed for creation evidence. (AC: 2, 10, 11)
  - [x] Add minimal projection/event-apply coverage that can derive tenant-scoped folder existence, lifecycle state, and safe metadata from creation events.
  - [x] Derive projection tenant scope from the stream/envelope tenant evidence, not from mutable event payload tenant fields if both are present.
  - [x] Prove replay isolation for two tenants with matching folder names, path labels, and folder-ID-like input values; tenant scope must come from the stream/envelope metadata and cannot collide across tenants.
  - [x] Cover empty streams, duplicate creation events, malformed metadata-only events, and replay ordering deterministically without external services.
  - [x] Keep projection output metadata-only and tenant-scoped; do not add public listing/query endpoints unless the Contract Spine already exposes the shape and the implementation can stay within this story's logical-folder scope.
  - [x] Do not implement hierarchy moves, folder archive, repository binding, provider readiness, workspace state, file context, or effective-permission inspection in this projection.
- [x] Add tests and fixtures. (AC: 1-12)
  - [x] Add unit tests under `tests/Hexalith.Folders.Tests/Aggregates/Folder/` or an equivalent local path for aggregate creation, event application, duplicate create, invalid folder ID, invalid metadata, reserved `system` tenant, and stream-name shape.
  - [x] Add authorization gate tests for allowed tenant plus `create_folder`, stale/unavailable/disabled/unknown/malformed/future/replay-conflicting tenant evidence, tenant mismatch, missing authoritative tenant, denied ACL, unavailable ACL evidence, and malformed ACL evidence.
  - [x] Add idempotency tests for equivalent replay, same key plus changed folder metadata, same key plus changed folder ID, and already-existing folder without equivalent replay.
  - [x] Add idempotency and duplicate tests for tenant-prefixed durable keys, idempotency evidence unavailable after authorization, concurrent same-folder create races, and the same opaque folder ID safely existing in two different authoritative tenants.
  - [x] Add metadata leakage tests with sentinel values for credential material, provider tokens, repository names, branch names, file paths, file contents, diffs, generated context payloads, arbitrary tenant configuration values, user emails, group display names, and unauthorized resource names.
  - [x] Add sequencing tests named like `RejectsBeforeStreamNameWhenTenantMissing`, `RejectsBeforeLoadWhenTenantNotEvidenced`, and `RejectsBeforeAppendWhenCreateFolderAclMissing`, or equivalent local names that encode the forbidden side-effect boundary.
  - [x] Use in-memory spies for EventStore access, tenant evidence, ACL evidence, idempotency store, clock/time, validators, diagnostics sink, and audit sink so rejected paths can prove zero stream naming/loading/appending/projection/diagnostic/audit/provider/repository side effects.
  - [x] Extend `src/Hexalith.Folders.Testing/Factories/*` only with reusable folder creation builders that delegate to production validation rules.
  - [x] Add conformance tests in `tests/Hexalith.Folders.Testing.Tests` if new testing helpers are introduced.
  - [x] Use pure in-memory fakes only for EventStore seams, tenant evidence, ACL evidence, clock/time, validators, idempotency records, and diagnostics sinks. These tests must not use Dapr, EventStore server, databases, network calls, generated SDK/OpenAPI, CLI/MCP/UI/workers, provider adapters, or nested submodule initialization.

### Review Findings

_Code review of commit `9f14bb7` via `/bmad-code-review 2.3` (Blind Hunter + Edge Case Hunter + Acceptance Auditor) on 2026-05-19._

**Decisions resolved (2026-05-19):**

- [x] [Review][Decision] Idempotency fingerprint format — **chosen: SHA-256 hex digest of canonical bytes** (option a). Eliminates collisions, caps width at 64 chars, no metadata embedded in event payload. Patched as part of `FolderCommandValidator.Fingerprint` and `FolderCreated.IdempotencyFingerprint`. [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:91-107`]
- [x] [Review][Decision] `IFolderRepository` ledger-key shape — **chosen: collapse to `(FolderStreamName, idempotencyKey)` as the single addressable identity** (option a). Both lookup and append take `FolderStreamName`; the gate constructs the stream name before the idempotency check. [`src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`]
- [x] [Review][Decision] ACL `Allowed`-but-context-mismatch — **chosen: new `FolderResultCode.AclEvidenceMismatch`** (option a). Preserves tampering/replay/drift signal distinct from a genuine `FolderAclDenied`. [`src/Hexalith.Folders/Aggregates/Folder/FolderCreateTenantGate.cs:107-125`, `FolderResultCode.cs`]
- [x] [Review][Decision] `FolderStateApply` foreign-event guard — **chosen: pass expected `FolderStreamName` into `Apply`** (option a). Aggregate-level defense-in-depth on every event including the first; gate already constructs the stream name. [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:12-19`]

**Patches:**

- [x] [Review][Patch] FolderListProjection trusts envelope without validating that `envelope.ManagedTenantId == envelope.Event.ManagedTenantId` — cross-tenant data primitive (FolderStateApply throws on mismatch; projection does not). [`src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs:14-27`]
- [x] [Review][Patch] `FolderCreateTenantGate.Map(TenantAccessOutcome)` and `EvaluateAcl` `_ => throw new InvalidOperationException(...)` arms escape the `FolderResult` contract for unmapped values (e.g., defensive `TenantAccessOutcome.Allowed` reached with `IsAllowed=false`, future enum extension). Return a stable rejection code (e.g., `MalformedEvidence` / `AclEvidenceUnavailable`) instead of throwing. [`src/Hexalith.Folders/Aggregates/Folder/FolderCreateTenantGate.cs:118-143`]
- [x] [Review][Patch] `FolderStateApply` `_ => state with { IdempotencyFingerprints = ... }` silently updates the ledger for unknown `IFolderEvent` subtypes — future events poison the idempotency map on cold replay. Throw on unknown event type or skip the ledger write. [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:27-44`]
- [x] [Review][Patch] `FolderCommandValidator.IsSafeMetadata` calls `ToLower(InvariantCulture)` without `Normalize(NormalizationForm.FormC)` first (the fingerprint path at `CanonicalMetadata` *does* normalize). Lookalikes (ZWSP `​`, NFD-decomposed combiners, Greek omicron) bypass the forbidden-term blocklist but still produce a normalized fingerprint. Normalize before the blocklist check. [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:136-151`]
- [x] [Review][Patch] `ValidateAndCanonicalizeTags` has no max tag count — `Tags = new string[1_000_000]` is accepted and feeds into both `Fingerprint` (`string.Join(",", tags)`) and `state.IdempotencyFingerprints` unboundedly. Cap tag count (e.g., 32). [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:109-131`]
- [x] [Review][Patch] When `repository.TryGetIdempotencyFingerprint` returns `Unavailable`, the gate rejects with `IdempotencyUnavailable` before loading state — a folder that *already exists* with a flaky idempotency ledger is reported as transient-unavailable instead of `DuplicateFolder`. Either load state first or fail-closed less aggressively. [`src/Hexalith.Folders/Aggregates/Folder/FolderCreateTenantGate.cs:62-78`]
- [x] [Review][Patch] `FolderResult.From(IFolderCommand, ...)` copies `command.OrganizationId / FolderId / ActorPrincipalId / CorrelationId / TaskId / IdempotencyKey` verbatim into result fields — validation-rejection path bypasses `SafePassthrough`. A malformed-but-not-null command (e.g., `MalformedEvidence` from an `ActorPrincipalId` with forbidden chars) echoes the malformed bytes back in the result. [`src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs:40-51`]
- [x] [Review][Patch] `FolderCommandValidationResult.Rejected(code)` returns `IdempotencyFingerprint = string.Empty` — `""` is a valid-looking comparison target; any caller that string-equals `priorFingerprint == validation.IdempotencyFingerprint` without checking `IsAccepted` first will spuriously match. Use `null` (and make the field nullable) or a guaranteed-non-fingerprint sentinel. [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidationResult.cs:12-13`]
- [x] [Review][Patch] `FolderStreamName.IsReservedSystemTenant` calls `.Trim()` before comparison; `IsValidSegment` does not. Inputs `" system "` and `" tenant-a "` follow different rejection paths (`ReservedTenant` vs `InvalidTenant`), differentially disclosing the reserved-name list. Apply consistent whitespace policy. [`src/Hexalith.Folders/Aggregates/Folder/FolderStreamName.cs:50-57`]
- [x] [Review][Patch] `FolderListProjection.Apply` orders by `envelope.Sequence` only — equal sequences resolve via LINQ's stable-sort coincidence rather than deterministic tiebreak. Add a secondary key (e.g., `IdempotencyKey` or content hash). [`src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs:14`]
- [x] [Review][Patch] `FolderListProjection.Key` is `$"{tenant}:folders:{folder}"` with no segment-validation on envelope inputs — a misrouted envelope with `ManagedTenantId="a:folders:b", FolderId="c"` collides with `(a, b:folders:c)`. Validate envelope identifiers against `FolderStreamName.IsValidSegment`. [`src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs:39-40`]
- [x] [Review][Patch] `RecordingFolderRepository` only counts stream construction when callers go through `repository.CreateStreamName(...)` — future production code that does `new FolderStreamName(...)` or `FolderStreamName.Create(...)` directly evades the spy, and all `StreamNamesConstructed.ShouldBe(0)` negative-controls pass falsely. Add an observable seam (e.g., make `FolderStreamName.Create` callable only via the repository, or assert a different invariant). [`tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs:34-40`]
- [x] [Review][Patch] `SafePassthrough` silently nulls forensic identifiers on the tenant-denial path — when the rejection is *caused by* a malformed identifier, the diagnostic loses every clue (`ActorPrincipalId`, `CorrelationId`, `TaskId` all become `null`). Either emit a safe diagnostic event before nulling or include a `RedactedFields` enumeration in the result. [`src/Hexalith.Folders/Aggregates/Folder/FolderCreateTenantGate.cs:127-128`]
- [x] [Review][Patch] `ValidateAndCanonicalizeTags` calls `IsSafePathLabel(tag)` (returns `true` for `null` because of `string.IsNullOrWhiteSpace`) then `tag.Trim()` — a `null` element NREs. Reject `null`/whitespace-only elements explicitly. [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:109-131`]
- [x] [Review][Patch] Whitespace-only tag strings pass `IsSafePathLabel` (which short-circuits on `IsNullOrWhiteSpace`) and end up as `""` after `Trim()`, then `Distinct`/`Order`/`string.Join(",", ...)` swallows them silently into the fingerprint. Reject explicitly. [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:117-125`]
- [x] [Review][Patch] `FolderStreamName.Create` throws `ArgumentException(..., nameof(managedTenantId))` even when `folderId` is the invalid argument. Branch on `code` and use the matching `paramName`. [`src/Hexalith.Folders/Aggregates/Folder/FolderStreamName.cs:9-17`]
- [x] [Review][Patch] `FolderStateApply` throw message embeds raw event identifiers (`folderEvent.ManagedTenantId`, `folderEvent.FolderId`) — if exceptions surface in logs/diagnostics, an attacker-controlled event payload becomes a log-injection vector. Use stable codes only. [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:16-18`]
- [x] [Review][Patch] `FolderStateApply` does not detect repeated identical events on replay — the same `FolderCreated` applied twice silently rebuilds state and rewrites the ledger entry. Skip if `(IdempotencyKey, IdempotencyFingerprint)` is already present. [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:21-44`]
- [x] [Review][Patch] `FolderListProjection`'s record primary constructor accepts any `IReadOnlyDictionary<string, FolderListItem>` — including a mutable `Dictionary`. The `Apply` path freezes via `ToFrozenDictionary`, but a hand-constructed projection bypasses the invariant. Make the constructor private and expose `Empty`/`Apply` only. [`src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs:5-7`]
- [x] [Review][Patch] `FolderCreateTenantGate` constructor takes `IFolderRepository` without `ArgumentNullException.ThrowIfNull(repository)` — null surfaces as `NullReferenceException` on a deep success path, inconsistent with the `Handle` parameter null-guarding convention. [`src/Hexalith.Folders/Aggregates/Folder/FolderCreateTenantGate.cs:5`]
- [x] [Review][Patch] `FolderListProjection.Apply` NREs on a `null` element inside the envelope enumerable (`envelopes.OrderBy(item => item.Sequence)`). Guard or reject. [`src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs:14`]
- [x] [Review][Patch] `RecordingFolderRepository.AppendIfFingerprintAbsent` writes `LastDurableKey` before checking the fingerprint — `FingerprintConflict`/`FingerprintMatched` paths still mutate the spy field, weakening the side-effect assertion semantics. Reorder so `LastDurableKey` reflects only successful append intent. [`tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs:60-69`]
- [x] [Review][Patch] `FolderCommandValidator.MaxIdentifierLength` (256) and `FolderStreamName.MaxSegmentLength` (256) duplicate the same constant in two places — silent drift hazard. Move to a single `internal const`. [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:9`, `src/Hexalith.Folders/Aggregates/Folder/FolderStreamName.cs:7`]
- [x] [Review][Patch] Two folder-creation factories diverge: `FolderCreationTestDataFactory` (`src/...Testing`) validates and throws on invalid input; `FolderCommandFactory` (in tests) doesn't validate. Consumers may select the wrong helper; one canonical helper or a documented split is needed. [`src/Hexalith.Folders.Testing/Factories/FolderCreationTestDataFactory.cs:42-50`, `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs:13-29`]
- [x] [Review][Patch] `FolderCreationIdempotencyTests.IdempotencyUnavailableShouldFailClosedAfterAuthorizationBeforeAppend` and `AppendConflictShouldReturnStableEvidenceWithoutSecondEvent` both assert `repository.EventsAppended.ShouldBe(0)` — those paths never increment `EventsAppended` even in a buggy production state. Strengthen the assertion (e.g., assert `result.Events.Count == 0` AND a positive side-effect counter remains baseline). [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationIdempotencyTests.cs` (the two tests)]
- [x] [Review][Patch] `FolderStateApply` copies `created.Tags.ToArray()` and the projection copies `envelope.Event.Tags.ToArray()` — neither re-canonicalizes. An event produced by a future buggy writer with non-canonical (mixed-case / duplicate / unsorted) tags is replayed verbatim. Re-canonicalize on apply. [`src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs:38`, `src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs:24`]
- [x] [Review][Patch] No test exercises the *aggregate-level* idempotency conflict branch — `FolderAggregate.Handle`'s `IdempotencyConflict` path (when state already carries a fingerprint for the same key) is unreachable from the current suite because all idempotency tests stop at the gate. Seed `FolderState.IdempotencyFingerprints` and call `FolderAggregate.Handle` directly. [`src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs:16-21`]
- [x] [Review][Patch] `FolderCreationMetadataLeakageTests.AcceptedEventShouldContainOnlySafeMetadata` uses display name `"Operations"`, which lowercases to itself — no test verifies the fingerprint omits/escapes a sentinel that changes shape under canonicalization. Add a sentinel whose canonical form is non-trivial and assert it does not appear unredacted in the serialized event. [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationMetadataLeakageTests.cs`]

**Deferred:**

- [x] [Review][Defer] `InvalidFolderMetadata` collapses length / control-char / forbidden-term failures into a single code — debuggability nit that requires expanding the public code surface; revisit when operator feedback warrants. [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:64-79`] — deferred, pre-existing pattern (Story 2.2 also coarse-grains validation codes)
- [x] [Review][Defer] `IdempotentReplay` outcome is returned via `FolderResult.Rejected(...)` even though it is a successful equivalence — `FolderResult` has no `IsAccepted` helper, so callers must dispatch on `Code`. Cosmetic API clarity; consider an `IsAccepted` property or a dedicated factory in a follow-up. [`src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`] — deferred, behavior-correct, API ergonomics only
- [x] [Review][Defer] `FolderCreateAclEvidence.Action` declared non-nullable but no compile-time guarantee against deserialization producing `null` — a defensive deserialization layer would be needed across the codebase, not just this record. [`src/Hexalith.Folders/Aggregates/Folder/FolderCreateAclEvidence.cs`] — deferred, applies to all records in `Aggregates/Folder/`

**Dismissed (10):**

- Forbidden-metadata terms `"@"` and `"/"` reject legitimate display strings (e.g., `"Q3/2026 Reports"`, `"@Team-Foo Inbox"`) — intentional per project rule "when in doubt, choose the more restrictive option" (raised by Blind Hunter and Edge Case Hunter, confirmed compliant by Acceptance Auditor).
- `FolderStreamName.IsReservedSystemTenant` only reserves `"system"` — Story 2.3 scope; an expanded reserved list is a product decision, not a defect.
- `FolderResultCode.StateTransitionInvalid` and single-member `FolderLifecycleState` enum — vocabulary slots accepted per AC1's "rejection types are introduced" wording.
- `already_exists` collapsed into `DuplicateFolder` — AC10's list uses "such as", `DuplicateFolder` is sufficient.
- `FolderAggregate.Handle` does not re-validate `PayloadTenantId == ManagedTenantId` — by architecture the aggregate is internal; the gate is the public boundary that enforces this.
- Aggregate ordering of idempotency-check before duplicate-check — actually correct: rotated key on existing folder correctly returns `DuplicateFolder`; same key + matching fingerprint returns `IdempotentReplay`.
- Story 1.10/1.11 negative-scope guards narrowed to allow `Aggregates/Folder/` — documented in Completion Notes, intentional and necessary.
- Workflow files (story doc, sprint-status, preflight artifacts) appearing in the diff — informational, not subject to adversarial review.
- AC10's `tenant_evidence_missing` not present verbatim — the spec uses "such as"; the implementation provides a richer set (`MissingAuthoritativeTenant`, `UnavailableProjection`, `MalformedEvidence`, `StaleProjection`, `UnknownTenant`, `DisabledTenant`, `ReplayConflict`, `TenantMismatch`).
- `FolderStateApply` throws `InvalidOperationException` on cross-tenant guard (rather than returning a result code) — acceptable internal-invariant assertion; not a public surface.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.3 requires `CreateFolder` to create a folder aggregate with an opaque identifier and active lifecycle state; tenant scope comes from auth context, not request payload authority. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.3`]
- PRD FR11 requires authorized actors to create logical folders within a tenant, while FR9 and NFR2 require denial before exposing folder, repository, credential, lock, file, audit, provider, or context information across tenant boundaries. [Source: `_bmad-output/planning-artifacts/prd.md#Functional Requirements`]
- Architecture maps FR11-FR14 folder lifecycle work to `src/Hexalith.Folders/Aggregates/Folder/` and `src/Hexalith.Folders.Contracts/Folders/{Commands,Events,Queries}/`. This story should implement the domain creation surface first and avoid contract drift unless the existing Contract Spine already requires a change. [Source: `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`]
- Architecture and project context require aggregate IDs to be opaque immutable identifiers. Folder hierarchy, names, and paths are projected metadata, never aggregate identity. [Source: `_bmad-output/project-context.md#Critical Implementation Rules`]

### Previous Story Intelligence

- Story 2.1 defines the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and tenant-access authorizer outcomes that must gate this story before folder stream selection.
- Story 2.1 advanced elicitation hardened replay conflicts, projection-store unavailable semantics, configuration-removal tombstones, diagnostic leakage boundaries, and structural endpoint tests. Reuse those stable outcome codes and evidence fields instead of inventing a second tenant authority model.
- Story 2.2 defines the organization ACL baseline and the closed `create_folder` action vocabulary. This story consumes that evidence; it must not reimplement organization ACL grant/revoke semantics.
- Story 2.2 advanced elicitation hardened culture-invariant canonicalization, strict lower-snake-case action parsing, intra-command duplicate/conflict handling, structured result evidence, and side-effect negative controls. Apply the same discipline to create-folder idempotency and rejection paths.

### Existing Implementation State

- `src/Hexalith.Folders/FoldersModule.cs` currently exposes scaffold metadata only; no folder aggregate, creation command, or folder lifecycle state exists yet.
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs` already validates stream-name segments and exposes `FolderStreamName` as `{ManagedTenantId}:folders:{FolderId}`. Reuse or extend this pattern instead of creating a second stream naming convention.
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs` provides simple managed tenant and permission test context data. Extend it only if folder creation tests need reusable builders.
- `tests/Hexalith.Folders.Tests/FoldersModuleSmokeTests.cs` is scaffold-level; this story should add real aggregate and authorization-gate tests without requiring live Dapr, EventStore, Tenants, provider credentials, or initialized nested submodules.
- `src/Hexalith.Folders.Client/Generated/*` contains generated client/idempotency helpers. Do not hand-edit generated files.

### Required Architecture Patterns

- Keep `Hexalith.Folders.Contracts` behavior-free. Folder aggregate logic, command validation, idempotency equivalence, tenant/ACL gates, and EventStore command handling belong in `Hexalith.Folders` and tests, not in Contracts.
- Keep aggregates pure. The aggregate applies commands, validates invariants that do not require I/O, and produces events/results; Dapr, provider, filesystem, Git, UI, CLI, MCP, workers, diagnostics, and projection-store side effects stay outside aggregate state transitions.
- Authorization order for mutation paths is authoritative tenant context, Story 2.1 tenant evidence, Story 2.2 folder-create ACL evidence, then stream load/mutation. Do not construct stream names or load streams before fail-closed gates have passed.
- Idempotency and duplicate lookups happen only after tenant and ACL gates pass. Unauthorized, stale, unavailable, malformed, or denied evidence must not reveal whether a folder stream, idempotency record, or duplicate folder already exists.
- Metadata validation happens before durable folder-create key construction, stream loading, duplicate lookup, append, projection update, diagnostics, audit lookup, and aggregate mutation. Invalid metadata must not be allowed to create durable operation records.
- Expected-version, append-conflict, or same-stream race handling must resolve to stable duplicate/idempotency evidence after authorization and validation. It must not append a second creation event or expose EventStore exception details.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals, and async APIs where I/O boundaries are introduced.
- Use stable result codes, enum values, and metadata fields in tests. Do not assert localized or user-facing diagnostic text.
- Events, logs, traces, metrics, projections, audit records, and errors must remain metadata-only and must not include raw credential values, provider tokens, file contents, diffs, generated context payloads, repository names, branch names, raw paths, user emails, group display names, or unauthorized resource existence.
- Cache keys and durable operational keys must carry tenant scope. A missing tenant prefix is a correctness and security bug. If folder creation introduces an idempotency or duplicate-detection key, test the tenant prefix.
- Do not add repair workflows, local-only folder mode, webhooks, brownfield adoption, multi-organization-per-tenant, or operations-console mutation paths during this story.

### Files To Touch

- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Command*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Event*.cs`
- `src/Hexalith.Folders/Aggregates/Folder/*Result*.cs`
- `src/Hexalith.Folders/Authorization/*` only for narrow integration seams needed to consume Story 2.1 tenant-access and Story 2.2 ACL outcomes.
- `src/Hexalith.Folders/Idempotency/*` only if no existing reusable equivalence helper exists for domain command payloads.
- `src/Hexalith.Folders/Projections/FolderList/*` only for minimal tenant-scoped creation replay evidence.
- `src/Hexalith.Folders.Testing/Factories/*` only for reusable folder creation test data builders.
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/*Tests.cs`
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are added.

### Do Not Touch

- Contracts and SDK: Do not edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`; do not add or modify OpenAPI Contract Spine operations unless implementation discovers a blocking mismatch that is explicitly handled through the contract workflow.
- Storage/provider: Do not implement provider readiness, provider adapters, repository creation, repository binding, branch policy, local-only folder mode, webhooks, or brownfield adoption.
- Workspace/file behavior: Do not implement workspace preparation, locks, file mutation, commits, context query, file browsing, or filesystem paths.
- Permission/lifecycle management: Do not implement folder ACL grant/revoke, effective-permission query endpoints, archive behavior, repair workflows, or multi-organization-per-tenant behavior.
- UX/surface/worker behavior: Do not implement CLI, MCP, UI, workers, operations-console mutation paths, or production Dapr policy.
- Do not make display name, path, repository name, provider name, or tenant name part of folder identity.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Unit tests must run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, or nested submodule initialization.
- Use xUnit v3 and Shouldly for aggregate tests; use NSubstitute only where an actual seam needs substitution.
- Add focused tests named `FolderCreationCommandValidationTests`, `FolderCreationTenantEvidenceGateTests`, `FolderCreationAclGateTests`, `FolderCreationIdempotencyTests`, `FolderCreationMetadataLeakageTests`, `FolderCreationProjectionReplayTests`, and `FolderStreamShapeTests`, or equivalent locally consistent names.
- Include side-effect negative controls proving rejected tenant evidence, rejected ACL evidence, invalid metadata, duplicate conflict, and idempotency conflict do not construct stream names, load streams, append events, mutate aggregate state, query diagnostics, query audit resources, call provider readiness, or create repositories.
- Include side-effect negative controls proving idempotency store unavailability and same-stream append races do not bypass authorization, validation, tenant-scoped keys, or metadata-only result evidence.
- Include positive and negative metadata tests: allowed diagnostics may include tenant ID, folder ID, result code, lifecycle state, correlation/task/idempotency IDs, and safe actor/principal ID; forbidden diagnostics must not include raw auth tokens, provider payloads, command bodies, repository/branch/path values, diffs, file contents, generated context, stack traces with sensitive values, user emails, group names, arbitrary tenant configuration, or unauthorized resource identifiers.
- Include replay tests proving `FolderCreated` deterministically produces the same active logical lifecycle state and safe metadata projection from event history.

### Regression Traps

- Do not grant access from a payload tenant ID, route tenant ID, query tenant ID, or client-controlled header.
- Do not silently allow folder creation when Tenants projection freshness or ACL evidence cannot be proven.
- Do not derive folder IDs from display names, folder paths, repository names, provider names, or tenant names.
- Do not implement repository-backed provisioning in this story. Provider readiness and repository creation start in Epic 3.
- Do not create a folder content tree, file browser, context-query index, workspace directory, or local filesystem path as part of logical folder creation.
- Do not log or project user-friendly names, emails, provider identifiers, repository URLs, branches, raw paths, file contents, diffs, generated context payloads, secrets, or unauthorized resource existence.
- Do not create duplicate folder state through idempotency replay or through an already-existing stream.
- Do not construct idempotency, duplicate-detection, cache, or operation keys before authoritative tenant and ACL gates pass, and never derive those keys from request-supplied tenant authority.
- Do not add public list/get endpoints unless the existing Contract Spine already defines them and the implementation stays metadata-only and tenant-scoped.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.3`
- `_bmad-output/planning-artifacts/prd.md#FR11`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/architecture.md#Domain Layout`
- `_bmad-output/planning-artifacts/architecture.md#Requirements to Structure Mapping`
- `_bmad-output/project-context.md#Critical Implementation Rules`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`
- `src/Hexalith.Folders.Testing/Factories/TestAuthorizationContext.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-17 | Created story with folder aggregate creation, tenant/ACL pre-load gates, idempotency, metadata-only event, projection replay, and offline test guardrails. | Codex |
| 2026-05-17 | Applied party-mode review hardening for terms, gate ordering, idempotency disclosure, duplicate scope, projection replay isolation, and test spies. | Codex |
| 2026-05-17 | Applied advanced-elicitation hardening for durable key scoping, metadata validation ordering, idempotency unavailability, concurrent duplicates, and projection authority. | Codex |
| 2026-05-18 | Implemented folder aggregate creation, tenant/ACL pre-load gate, idempotency and duplicate handling, minimal folder-list replay projection, testing helper, and focused guardrail coverage. Story moved to review. | Codex |
| 2026-05-19 | Applied code-review patches: SHA-256 idempotency fingerprint, single-shape `(FolderStreamName, idempotencyKey)` ledger, `AclEvidenceMismatch` distinct from genuine deny, expected-stream-name guard in `FolderStateApply`, projection envelope/event tenant agreement check + tiebreaker, Unicode-aware metadata validator, max tag count, ledger-unavailable surfaces `DuplicateFolder`, stable fallback for unmapped tenant/ACL outcomes, plus matching test strengthening. All 302 solution tests pass. Story moved to done. | Claude |

## Party-Mode Review

- ISO date and time: 2026-05-17T10:56:49Z
- Selected story key: `2-3-create-folders-within-a-tenant`
- Command/skill invocation used: `/bmad-party-mode 2-3-create-folders-within-a-tenant; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), Paige (Technical Writer)
- Findings summary: Reviewers agreed the story was in the correct product scope but needed clarification before dev on authoritative tenant source, strict gate order before any stream/idempotency/diagnostic/audit side effects, ACL evidence dependency, idempotency disclosure boundaries, duplicate-folder meaning, safe metadata terms, projection replay isolation, and offline negative-control tests.
- Changes applied: Added a Terms section; tightened AC2, AC6, AC7, AC8, AC9, AC10, and AC11; added sequencing, projection replay, and spy-fixture subtasks; clarified idempotency lookup ordering; grouped exclusions by category; and recorded the trace separately from future advanced elicitation evidence.
- Findings deferred: Folder display-name uniqueness within a tenant; whether projected path label is cosmetic metadata or a future hierarchy locator; final replay projection shape beyond creation evidence; whether denied attempts emit audit records and the exact redacted field set; whether idempotency helper should stay folder-local or become reusable after another aggregate needs it.
- Final recommendation: `ready-for-dev`

## Advanced Elicitation

- ISO date and time: 2026-05-17T11:01:46Z
- Selected story key: `2-3-create-folders-within-a-tenant`
- Command/skill invocation used: `/bmad-advanced-elicitation 2-3-create-folders-within-a-tenant`
- Batch 1 method names: Red Team vs Blue Team; Security Audit Personas; Failure Mode Analysis; Self-Consistency Validation; Critique and Refine.
- Reshuffled Batch 2 method names: Pre-mortem Analysis; First Principles Analysis; Graph of Thoughts; Occam's Razor Application; Active Recall Testing.
- Findings summary:
  - The story had the right security shape, but advanced elicitation found remaining implementation traps around durable key construction happening before authorization, invalid metadata creating operational records, idempotency-store outages falling through to append, concurrent same-folder creates leaking EventStore failures, and projections trusting mutable event payload tenant fields.
  - The acceptance criteria and tasks needed explicit tenant-prefixed durable keys, culture-invariant idempotency canonicalization, same-stream race handling, idempotency-unavailable evidence, cross-tenant same-folder-ID isolation tests, and projection authority from stream/envelope metadata.
- Changes applied: Added durable folder-create key and concurrent duplicate terms; tightened AC5, AC8, AC9, AC10, and AC11; added tasks for tenant-scoped durable keys, metadata canonicalization, idempotency unavailability, append-conflict handling, and projection tenant authority; extended testing and regression guidance for side-effect negative controls and same-stream races.
- Findings deferred: Exact idempotency persistence implementation; whether append-conflict evidence is a distinct public result code or maps to existing duplicate evidence; final reusable idempotency helper shape across aggregates; any public projection/listing contract beyond minimal creation replay evidence.
- Final recommendation: ready-for-dev after applied advanced-elicitation hardening.

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-18: Red phase confirmed with `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` failing on missing `Hexalith.Folders.Aggregates.Folder` and `Hexalith.Folders.Projections.FolderList` types.
- 2026-05-18: Focused folder aggregate validation passed with `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` (123 tests).
- 2026-05-18: Testing-helper validation passed with `dotnet test tests\Hexalith.Folders.Testing.Tests\Hexalith.Folders.Testing.Tests.csproj --no-restore` (43 tests).
- 2026-05-18: Full regression initially surfaced Story 1.10/1.11 negative-scope guardrails still blocking the Story 2.3-owned `Aggregates/Folder` directory; narrowed those guards to keep all other forbidden categories intact.
- 2026-05-18: Full regression passed with `dotnet test Hexalith.Folders.slnx --no-restore`.

### Implementation Plan

- Mirrored the Story 2.2 aggregate pattern for a pure folder aggregate, stable result evidence, strict stream-name validation, and a repository seam that records stream/idempotency side effects only after authorization and metadata validation pass.
- Added a narrow folder-create ACL evidence boundary that consumes Story 2.2 `create_folder` evidence without reimplementing organization ACL grant/revoke semantics.
- Kept folder creation logical only: active lifecycle state, unbound repository state, safe metadata event payloads, idempotency fingerprinting, duplicate handling, and append-conflict evidence.
- Added a minimal tenant-scoped folder-list projection for replay evidence only, deriving tenant scope from the stream/envelope metadata instead of mutable event payload fields.

### Completion Notes List

- Story created by `/bmad-create-story 2-3-create-folders-within-a-tenant` equivalent workflow on 2026-05-17.
- Project context, Epic 2, PRD, architecture, Story 2.1, Story 2.2, testing factories, recent commits, and story-creation lessons were reviewed.
- Added `FolderAggregate`, `FolderState`, `FolderStateApply`, `CreateFolder`, `FolderCreated`, stable result codes, stream-name validation, safe metadata validation, and lifecycle/repository binding evidence under `src/Hexalith.Folders/Aggregates/Folder/`.
- Added `FolderCreateTenantGate`, `FolderCreateAclEvidence`, and `IFolderRepository` so tenant evidence and `create_folder` ACL evidence reject before durable key construction, stream naming, stream loading, appending, projection updates, diagnostics, audit, provider readiness, or repository work.
- Added folder idempotency fingerprinting, tenant-prefixed durable key tests, equivalent replay handling, idempotency conflict/unavailable evidence, duplicate-folder evidence, and append-conflict handling.
- Added minimal `FolderListProjection` replay support for creation evidence with envelope-derived tenant scope and cross-tenant isolation.
- Added focused unit tests for stream shape, aggregate command validation, tenant/ACL gates, idempotency, metadata leakage, replay projection, and reusable folder test-data factory conformance.
- Narrowed the older contract-story negative-scope guards to allow Story 2.3's `Aggregates/Folder` directory while preserving their remaining forbidden categories.
- Did not implement provider readiness, repository creation/binding, workspace preparation, locks, file mutation, commits, folder ACL grant/revoke, effective-permission query endpoints, archive behavior, CLI/MCP/UI/workers, production Dapr policy mapping, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

### File List

- `_bmad-output/implementation-artifacts/2-3-create-folders-within-a-tenant.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Aggregates/Folder/CreateFolder.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAppendOutcome.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidationResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCreateAclEvidence.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCreateAclOutcome.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCreated.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderCreateTenantGate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderIdempotencyLookupResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderLifecycleState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderRepositoryBindingState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateApply.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStreamName.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IFolderCommand.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IFolderEvent.cs`
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderListItem.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderListProjection.cs`
- `src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs`
- `src/Hexalith.Folders.Testing/Factories/FolderCreationTestDataFactory.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs`
- `tests/Hexalith.Folders.Testing.Tests/Unit/FolderCreationTestDataFactoryTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCommandFactory.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationAclGateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationCommandValidationTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationIdempotencyTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationMetadataLeakageTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationProjectionReplayTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderCreationTenantEvidenceGateTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStreamShapeTests.cs`
- `tests/Hexalith.Folders.Tests/Aggregates/Folder/RecordingFolderRepository.cs`
