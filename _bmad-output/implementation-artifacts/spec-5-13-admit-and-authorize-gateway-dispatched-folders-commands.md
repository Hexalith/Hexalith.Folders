---
title: '5.13 Admit and authorize Gateway-dispatched Folders commands'
type: 'feature'
created: '2026-11-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '4c7a1b3f70a8676b54df595e5c33edafbfb27d67'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-5-context.md'
  - '{project-root}/docs/contract/mcpcli-folders-operation-declarations.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Gateway-dispatched McpCli commands targeting Folders `/process` currently fail admission or produce broken idempotency intents: `/process` rejects canonical uppercase ULID `MessageId`/`CorrelationId` and mixed-case opaque identifiers, `FolderDomainProcessor` rejects all 13 decorated `Hexalith.Folders.Contracts.Commands.*` v2 JSON shapes (missing `requestSchemaVersion`, extra routing/`idempotencyKey` fields, C07/C08/C10/C13 structural deltas), and `Hexalith.Folders.EventStoreExtensions` intent adapters drop or fail on decorated v2 fields (`changes[]`, C04 flat policy fields, C08/C13 `subjectRef`/`permissionLevel`/`aclEntryId`).

**Approach:** Extend Folders' Gateway idempotency adapters (`Hexalith.Folders.EventStoreExtensions`) and `/process` admission/translation pipeline (`FoldersDomainServiceRequestHandler`, `FolderDomainProcessor`) to accept both existing REST v1 payloads and all 13 decorated McpCli v2 Command shapes with uppercase ULID envelope IDs, full payload/constraint validation, and identical deny-before-access PD10 v2 authorization and state-store end-state.

## Boundaries & Constraints

**Always:**
- Preserve all existing REST v1 `/process` and idempotency adapter behavior without regressions.
- Accept canonical uppercase ULIDs and valid opaque identifiers (`StreamNameSegment.IsValid`) in `/process` envelope `MessageId`, `CorrelationId`, `AggregateId`, `UserId`, and task context without silently lowercasing opaque or aggregate IDs; echo valid identifiers unchanged in `FolderProcessResultPayload` and `FolderCommandRejected`.
- Keep caller `IdempotencyKey` independent of generated `MessageId`; normalize equivalent v1 and decorated v2 intents to deterministic canonical intent descriptors without storing or logging raw file content bytes.
- Treat envelope `TenantId` and `UserId` strictly as comparison inputs against the verified `HumanAuthorityReceipt` and layered tenant/folder ACL authority; preserve deny-before-access safe denials (zero protected reads, zero state mutations on unauthorized/cross-tenant/revoked/forged-tenant commands).
- Translate all 13 decorated v2 Command payloads (C01–C13) into validated domain commands: supply domain-accepted `requestSchemaVersion` (`v1`) at root and nested `branchRefPolicy`, reject explicit JSON `null` where the v2 spine forbids null (only `FileMutationRequest.byteLength` is nullable), reject unmapped/unknown properties (including caller-supplied `effect` on C08/C13), enforce `CommitWorkspace.auditMetadataKeys` count (1..24) + uniqueness + closed enum set, reconcile `payload.TaskId == extensions["taskId"]` on C03/C09/C10/C11/C12, derive C08/C13 `grant`/`revoke` from wire type, resolve C07 `repositoryBindingId` and `credentialScopeClass`, and translate C10 `changes[]` (1..100 items, inline decoded bytes <= 262,144, batch total <= 10 MiB, SHA-256 verification, `IAuthoritativeContentStore` receipt materialization with metadata-only events).
- Follow single-type-per-file C# rules, XML docs, and zero warnings (`TreatWarningsAsErrors`).

**Never:**
- Modify `references/` submodules, add Folders-specific McpCli host/adapter code, or activate `V2Only` routing.
- Silently lowercase or rewrite caller identifiers, weaken McpCli or EventStore Gateway validation rules, or emit file content bytes or secrets in events, errors, logs, or intent descriptors.
- Implement Story 5.14 Gateway query handlers or touch `planning-story-manifest.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Uppercase ULID envelope | Canonical 26-char uppercase ULID `MessageId`/`CorrelationId`, mixed-case opaque `AggregateId` | `/process` admits, executes, and echoes IDs without lowercasing | Reject invalid grammar with `400` / `ValidationFailed` |
| Decorated v2 commands (C01–C13) | Valid decorated JSON per Story 5.12 (omitted `requestSchemaVersion`, with `idempotencyKey`/`folderId`) | Translated and executed identically to REST; events persisted | Unmapped member or forbidden explicit `null` -> `ValidationFailed` / `MalformedJsonPayload` |
| Idempotency replay, conflict, expiry | Same `IdempotencyKey` with different `MessageId`: identical intent / mutated field / expired TTL | Replay returns original result without duplicate events; conflict rejected; expired never re-executes | `IdempotencyConflict` / `IdempotencyKeyExpired`; zero extra writes |
| Task reconciliation (C03, C09–C12) | Missing `taskId` or `payload.taskId != extensions["taskId"]` | Fails closed before state mutation | `400` / `ValidationFailed` |
| C03 duplicate `auditMetadataKeys` | `auditMetadataKeys` empty, >24, unknown, or contains duplicates | Fails closed before aggregate execution | `ValidationFailed` |
| C08/C13 caller-supplied `effect` | Raw JSON includes `"effect": "revoke"` on `GrantFolderAccess` | Rejected at `/process` even when bypassing McpCli schema | `MalformedJsonPayload` / `ValidationFailed` |
| C10 `changes[]` batch | Inline bytes <= 262,144 matching SHA-256; >262,144; hash mismatch; conflicting paths; remove with content | Valid batch materializes receipts in content store and appends metadata-only events; invalid fails closed | `ValidationFailed` / `PathPolicyDenied`; no bytes in events |
| Cross-tenant / revoked / forged tenant | Forged envelope `TenantId`/`UserId`, missing/invalid receipt, revoked ACL, or non-owned lock | Deny-before-access PD10 safe denial; zero protected reads and zero persisted state mutations | `401`/`403`/`404`/`503` safe denial or `LockNotOwned`/`LockExpired` |

## Decisions (owner, 2026-11-27)

- **D1 — Scoped authorization & PD10 inventory:** Authorized ahead of `11.10`/`12.6`/`1.17` closure. Add all new `.cs` paths to `scripts/pd10-v2-story-owned-paths.txt` and refresh `_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml` so `Pd10ConformanceSetTests` stays green. Keep `planning-story-manifest.yaml` and `V1Only` unchanged.
- **D2 — C05 `ConfigureProviderBinding` stream namespace:** Require `AggregateId == ProviderBindingActorId.Create(payload.ProviderBindingRef)` (and `AggregateId != TenantId`) at `/process` and in `ConfigureProviderBindingIdempotencyIntentAdapter` so REST and Gateway always share one canonical EventStore stream.
- **D3 — C08/C13 `subjectRef` format:** Accept both `{kind}:{principalId}` (`user:`, `group:`, `role:`, `delegated_service_agent:`) and bare opaque `subjectRef` (mapped to `user`) in `/process` and `FolderAccessIdempotencyIntent`, verifying `aclEntryId == DeriveAclEntryId(kindToken, principalId, permissionLevel)` with zero changes to `Hexalith.Folders.Contracts`.
- **D4 — `extensions["taskId"]` requirement & grammar:** Keep `extensions["taskId"]` required on all 13 Commands (matching Story 5.12 E6), relaxing its grammar to accept canonical uppercase ULIDs and opaque identifiers (`FolderCanonicalSegmentIdentifier.IsValid`) without lowercasing, and strictly reconciling `payload.TaskId == extensions["taskId"]` on C03, C09, C10, C11, and C12.
- **D5 — Scope:** Keep one cohesive spec covering all 13 commands, `/process` authorization, and `Hexalith.Folders.EventStoreExtensions` idempotency adapters.

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/FolderCanonicalSegmentIdentifier.cs:21-40` -- `IsValid` (allows opaque/ULID) vs `IsLowercaseSegment` (blocks uppercase); reuse `IsValid` for `/process` envelope IDs.
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:35-123,235-278` -- receipt verification, `TryValidateProcessEnvelope`, `ResolveOperationScope`, `IsAdmittedActor`, layered authorization; relax lowercase-only checks to `IsValid` and reconcile C05/task rules.
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:59-63,133-1468,1600-1602` & `FolderDomainProcessor.ContentBatch.cs:15-94` -- payload deserialization (`PayloadJsonOptions` `Disallow`), task checks on C09/C11/C12, C03 `auditMetadataKeys` validation, C10 `changes[]` translation with `IAuthoritativeContentStore`, result echo; add dedicated translator helper class (separate `.cs` file).
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs:101-132,173-174` -- `CanonicalIdentifierOrNull` currently drops non-lowercase IDs to `null`; switch to `IsValid`.
- `src/Hexalith.Folders.Contracts/Routing/Payloads/FoldersCommandPayloads.cs:15-148` -- existing domain payload records; keep v1 compatibility intact.
- `src/Hexalith.Folders.EventStoreExtensions/FoldersCanonicalIntentBuilder.cs:20-161,325-406`, `FolderAccessIdempotencyIntent.cs:10-42`, `MutateFilesIdempotencyIntentAdapter.cs:31-48`, `ConfigureBranchRefPolicyIdempotencyIntentAdapter.cs:31-46`, `ArchiveFolderIdempotencyIntentAdapter.cs`, `CreateFolderIdempotencyIntentAdapter.cs`, `ConfigureProviderBindingIdempotencyIntentAdapter.cs` -- normalize decorated v2 and v1 payloads into identical canonical intent descriptors; strip `idempotencyKey`/`folderId` noise and never include raw `contentBytes`.
- `tests/Hexalith.Folders.Server.Tests/FoldersDomainServiceRequestHandlerTests.cs`, `ProductionProcessWireEventTests.cs`, `FolderDomainProcessorRejectionTests.cs` -- reuse `FoldersProductionTestHost` for real HTTP `POST /process` + receipt + state-store end-state tests.
- `tests/Hexalith.Folders.EventStore.Tests/FoldersDeclaredAdmissionFixture.cs`, `FoldersDeclaredAdmissionTests.cs`, per-command adapter tests -- reuse real EventStore admission coordinator + `IIdempotencyIntentAdapterRegistry` for replay/conflict/expiry tests.
- `scripts/pd10-v2-story-owned-paths.txt` -- keep sorted and updated for every new `.cs` file under PD10 coverage roots.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs`, `FolderDomainProcessor.cs`, `FolderCommandRejected.cs` -- admit canonical uppercase ULIDs and opaque identifiers (`FolderCanonicalSegmentIdentifier.IsValid`) across `/process` envelope validation, scope resolution, and result/rejection echo without lowercasing; enforce task reconciliation on C03/C09/C10/C11/C12 and C05 actor rules per approved decisions -- fixes G3/V03/V07 envelope admission.
- [x] `src/Hexalith.Folders.Server/GatewayCommandPayloadTranslator.cs` (new) & `FolderDomainProcessor.cs` / `FolderDomainProcessor.ContentBatch.cs` -- translate all 13 decorated v2 Command JSON payloads (C01–C13) into validated domain payloads, enforcing omitted `requestSchemaVersion` -> `"v1"`, forbidden explicit nulls, unmapped-property rejection (incl. C08/C13 `effect`), C03 `auditMetadataKeys` count/uniqueness/enum, C07 `repositoryBindingId`/`credentialScopeClass`, C08/C13 `FolderAclContract` mapping, and C10 `changes[]` inline/stream validation + `IAuthoritativeContentStore` receipt materialization -- closes Story 5.12 `command-payload-translation` handoff.
- [x] `src/Hexalith.Folders.EventStoreExtensions/FoldersCanonicalIntentBuilder.cs` & affected `*IdempotencyIntentAdapter.cs` / `FolderAccessIdempotencyIntent.cs` -- support both v1 and decorated v2 payloads with canonical equivalence (C01/C04/C05/C06/C08/C10/C13), ensuring distinct `changes[]` batches never collide, `idempotencyKey`/`folderId` noise is excluded, and raw `contentBytes` are never embedded in intent descriptors -- guarantees A-9 replay/conflict/expiry parity.
- [x] `tests/Hexalith.Folders.EventStore.Tests/McpCliCommandAdmissionTests.cs` (new) & existing adapter tests -- test all 13 decorated v2 Commands through `FoldersDeclaredAdmissionFixture` proving caller `IdempotencyKey` independence from generated uppercase ULID `MessageId`, replay/conflict/expiry, C04/C08/C10/C13 equivalence, and v1/v2 intent parity -- verifies Gateway admission.
- [x] `tests/Hexalith.Folders.Server.Tests/McpCliProcessAdmissionAndAuthorizationTests.cs` (new) -- test all I/O matrix scenarios through `FoldersProductionTestHost` `POST /process` with signed `HumanAuthorityReceipt` and real actor-folded state store, proving uppercase ULID preservation, all 13 translated commands, deny-before-access safe denials (cross-tenant, unauthorized, revoked, forged tenant/actor, lock not owned/expired) with state-store end-state assertions confirming zero mutations -- verifies `/process` authorization and persistence.
- [x] `scripts/pd10-v2-story-owned-paths.txt`, `_bmad-output/implementation-artifacts/evidence/story-12-2-v2-source-requalification-pending-2026-11-12.yaml` -- register all new `.cs` paths sorted/unique and regenerate the pending conformance inventory via `scripts/generate-pd10-v2-conformance-set.py` (D1) -- keeps `Pd10ConformanceSetTests` green.

**Acceptance Criteria:**
- Given a Gateway-dispatched McpCli envelope with canonical uppercase ULID `MessageId`/`CorrelationId` and independent caller `IdempotencyKey`, when submitted through EventStore admission and Folders `/process`, then aggregate/opaque IDs are never lowercased, replay returns the original logical result without duplicate events, changed intent returns `IdempotencyConflict`, and expired keys never re-execute.
- Given every one of the 13 decorated Commands (C01–C13), when dispatched to `/process` with a verified `HumanAuthorityReceipt`, then authorization evaluates layered tenant/folder ACL, revocation, and task/workspace/lock ownership identically to REST, and unauthorized/cross-tenant/revoked/forged-tenant requests disclose no protected data and leave persisted state unchanged.

## Implementation Notes

- Implemented all six tasks across `Hexalith.Folders.Server`, `Hexalith.Folders`, `Hexalith.Folders.EventStoreExtensions`, `Hexalith.Folders.EventStore.Tests`, `Hexalith.Folders.Server.Tests`, `scripts/pd10-v2-story-owned-paths.txt`, and the pending PD10 conformance manifest (`story-12-2-v2-source-requalification-pending-2026-11-12.yaml`).
- Added `TranslatedFileMutationItem.cs` alongside `GatewayCommandPayloadTranslator.cs` to honor the single-type-per-file rule.
- Also relaxed `OrganizationProviderBindingCommandValidator` solely for `host_profile_ref` (`ProviderBindingProfileFingerprint.ProfileRefKey`) so uppercase-capable opaque `capabilityProfileRef` values validate cleanly while preserving strict policy validation for all other metadata keys.
- Verified Release/package CI solution build (0 warnings, 0 errors), `EventStore.Tests` (149/149), `Server.Tests` (1595/1595; 11 pre-existing environment-gated Not Run), `Contracts.Tests` McpCli (51/51) + `Pd10ConformanceSetTests` (21/21), and `OrganizationProviderBindingValidationTests` (36/36). All 8 I/O Matrix rows and all 12 triaged review patches verified directly against diff and tests; 4 pre-existing follow-ups logged in `deferred-work.md`.

## Spec Change Log

## Review Triage Log

- `false` (blind-hunter B1) — C05 `AggregateId == ProviderBindingActorId.Create(providerBindingRef)` is explicitly mandated by approved decision D2 so REST and Gateway share one EventStore stream; McpCli's `AggregateIdProperty` routing mismatch is a known documented gap (Story 5.12 E5 / Story 5.13 D2).
- `medium` -> `defer` (blind-hunter B2) — Pre-existing mismatch between Story 5.12 `ConfigureProviderBinding.ProviderFamilyRef` (16–128 chars) and pre-existing `OrganizationProviderBindingCommandValidator.SupportedProviderKinds` (`github`/`forgejo`/etc., 6–12 chars); not introduced by Story 5.13.
- `low` (blind-hunter B3, rejected) — Flattening 1-item `changes[]` intentionally preserves parity with single-file REST v1 (`BuildV1AndV2Pair("C10")`), omitting null `byteLength` normalizes omitted vs explicit null on remove, and `contentMediaType` has no pre-existing field on `FileMutationPayload`.
- `high` -> `patch` (blind-hunter B4) — Verified in `FolderDomainProcessor.ContentBatch.cs`: `ProcessDecoratedContentBatchAsync` uploads/verifies content before checking `item.ExpectedLockFence == initialState.WorkspaceLockFence`, omits `catch (UnauthorizedAccessException)` around `PrepareAsync`/`ListAsync` (misclassifying revocation as 503 `PolicyEvidenceUnavailable`), and passes `CancellationToken.None` instead of forwarding the request token.
- `medium` -> `patch` (blind-hunter B5) — Verified in `FoldersCanonicalIntentBuilder.cs:131-134` (`BuildNormalizedV2Guard` allows omitted `folderId` when `stripFolderId: true`), `FolderAccessIdempotencyIntent.cs:23-44` (treats payloads with `requestSchemaVersion` + no `operations` as v2), `ConfigureBranchRefPolicyIdempotencyIntentAdapter.cs:35-38` (skips guard on hybrid `branchRefPolicy` without `requestSchemaVersion`), and `MutateFilesIdempotencyIntentAdapter.cs:38-41` (accepts `changes` mixed with `requestSchemaVersion`/`items`). (`ReadTaskScope` allowing omitted `Extensions` is intentional for legacy adapter unit tests; `/process` enforces `extensions["taskId"]`.)
- `low` (blind-hunter B6, rejected) — Lowercase passthrough in `IsLowercaseSegment` and substring blocklists in `FolderCommandValidator.IsSafeEvidenceIdentifier` are pre-existing invariants preserved by Story 5.13 rather than defects caused by this diff.
- `medium` -> `defer` (blind-hunter B7) — Pre-existing `FolderDomainProcessor.CommandTypeClassification` and `FolderCommandRejected.NormalizeCommandTypeForRejection` omit C05/C06/C08/C13; neither method was altered by Story 5.13.
- `medium` -> `defer` (blind-hunter B8) — Pre-existing `FolderCommandValidator.MaxDisplayNameLength = 128` vs v2 Contracts 160-char `DisplayName` limit and unforwarded domain request fields (`ParentFolderId`/`MetadataClass`/`AuditMetadataKeys`); `FolderCanonicalSegmentIdentifier.IsValid` intentionally preserves legacy + opaque identifier compatibility per Story 5.12.
- `medium` -> `defer` (blind-hunter B9) — Pre-existing `FolderAclContract.DeriveAclEntryId` Base64Url encoding exceeds 128 chars for valid 80–128 char opaque `subjectRef` values; D3 explicitly binds existing `DeriveAclEntryId` without modifying `Hexalith.Folders.Contracts`.
- `medium` -> `patch` (blind-hunter B10) — Verified in `FoldersCanonicalIntentBuilder.cs:153-160`: `BuildNormalizedV2Guard` allows a smuggled root `repositoryBindingId` on C07 instead of throwing `JsonException`. (Omitted vs explicit null on v1 is pinned by `legacy-canonical-intents.json`.)
- `low` -> `patch` (blind-hunter B11) — `FolderCanonicalSegmentIdentifier.IsLowercaseSegment` XML doc (`:29-30`) still claims `/process` and rejection events enforce `IsLowercaseSegment`; direct doc update. (`CatalogPayloadTypes` is intentionally pinned to v1 payload types by existing catalog digest tests.)
- `medium` -> `patch` (blind-hunter B12) — Verified `Random.Shared.Next(10, 99)` in `McpCliProcessAdmissionAndAuthorizationTests.ExecuteRejectedAsync` creates non-deterministic birthday-collision risk, and `DenyBeforeAccessSafeDenialsPreventProtectedReadsAndStateMutations` lacks a valid-receipt tenant-access denial case (missing `PutFileStream` and multi-item rollback also covered under V1/V2).
- `low` (blind-hunter B13, rejected) — `tests/Hexalith.Folders.EventStore.Tests` is outside `scripts/pd10-v2-coverage-roots.txt` (false on PD10 omission), private nested test doubles inside a single top-level test class avoid unnecessary public/internal file sprawl, and JSON quote escaping in `legacy-canonical-intents.json` is cosmetic.
- `medium` -> `patch` (verification-gap V1, pre-verified) — Decorated v2 `MutateFiles` `PutFileStream` translation, `VerifyUploadAsync` receipt validation, and `MutateFilesIdempotencyIntentAdapter` `streamDescriptor.mediaType` parity have zero test coverage.
- `medium` -> `patch` (verification-gap V2, pre-verified) — Multi-item decorated `MutateFiles` `changes[]` batch execution and second-item failure atomic rollback in `ProcessDecoratedContentBatchAsync` have zero test coverage.
- `medium` -> `patch` (verification-gap V3, pre-verified) — Payload `folderId` vs envelope `AggregateId` mismatch guards in `FoldersCanonicalIntentBuilder`, `FolderAccessIdempotencyIntent`, `MutateFilesIdempotencyIntentAdapter`, and `GatewayCommandPayloadTranslator` have zero negative test coverage.
- `medium` -> `patch` (verification-gap V4, pre-verified) — `FolderCommandRejected.CanonicalIdentifierOrNull` `!FolderSensitiveDiagnosticDetector.IsSensitive` guard is masked by `"synthetic"` in all opaque-shaped corpus sentinels; add non-`"synthetic"` mixed-case credential cases (`ghp_...`, `glpat-...`, `Password...`).
- `medium` -> `patch` (verification-gap V5, pre-verified) — Legacy v1 `payload.TaskId != extensions["taskId"]` guards in `FolderDomainProcessor.cs:1112,1197,1280,1368` have zero test coverage.
- `high` -> `patch` (edge-case-hunter E1) — Verified in `FolderDomainProcessor.ContentBatch.cs:243-250`: missing `catch (UnauthorizedAccessException)` in second try block misclassifies mid-flight revocation during `PrepareAsync`/`ListAsync` as retryable `PolicyEvidenceUnavailable` (grouped with B4).
- `high` -> `patch` (edge-case-hunter E2) — Verified in `FolderDomainProcessor.ContentBatch.cs:80-129`: missing `item.ExpectedLockFence != initialState.WorkspaceLockFence` pre-check before `UploadAsync`/`VerifyUploadAsync` allows stale-fence content-store probes and orphan uploads (grouped with B4).
- `medium` -> `patch` (edge-case-hunter E3) — Verified in `FolderDomainProcessor.ContentBatch.cs:188-238`: `BatchItemKey` hashes `envelope.MessageId` before `ProcessSingleFileMutationAsync`, bypassing `FolderCommandValidator.IsSafeEvidenceIdentifier` on raw `envelope.MessageId` and `acceptedIdempotencyKey` before response echo/event persistence.
- `medium` -> `patch` (edge-case-hunter E4) — Verified in `GatewayCommandPayloadTranslator.cs:764-780`: `ownerFileId` only checks non-whitespace and length `<= 128`, missing `FolderFileIdentityState.IsCanonical` (and `WorkspaceFileMutationService` overwrites `OwnerFileId` on `add`, hiding malformed inputs).
- `medium` -> `patch` (edge-case-hunter E5) — Verified in `FolderAccessIdempotencyIntent.cs:23-44`: hybrid payload with `requestSchemaVersion` and no `operations` passes Gateway admission as v2 while `/process` dispatches as v1 and rejects (grouped with B5).
- `medium` -> `patch` (edge-case-hunter E6) — Verified in `MutateFilesIdempotencyIntentAdapter.cs:38-41`: hybrid payload mixing `changes` with `requestSchemaVersion` or `items` passes admission as decorated batch while `/process` routes to v1 (grouped with B5).

## Verification

**Commands:**
- `dotnet build Hexalith.Folders.CI.slnx --configuration Release -p:UseHexalithProjectReferences=false -warnaserror -m:1` -- expected: 0 warnings, 0 errors.
- `dotnet tests/Hexalith.Folders.EventStore.Tests/bin/Release/net11.0/Hexalith.Folders.EventStore.Tests.dll -noLogo -noColor` -- expected: all passed, 0 failed.
- `dotnet tests/Hexalith.Folders.Server.Tests/bin/Release/net11.0/Hexalith.Folders.Server.Tests.dll -noLogo -noColor` -- expected: all passed, 0 failed.
- `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Release/net11.0/Hexalith.Folders.Contracts.Tests.dll -noLogo -noColor -namespace Hexalith.Folders.Contracts.Tests.McpCli -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests` -- expected: all passed, 0 failed.
