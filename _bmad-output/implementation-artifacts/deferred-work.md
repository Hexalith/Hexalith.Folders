# Deferred Work

### DW-1: Author the production ops-console diagnostics projection

origin: migrated from legacy ledger ("Deferred from: bmad-correct-course seed-backed read-model decision (2026-07-07)"), 2026-08-24
location: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:104
reason: `IOpsConsoleDiagnosticsReadModel` deployed default is `InMemoryOpsConsoleDiagnosticsReadModel` (`src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:104`, `TryAddSingleton`) — seven views (readiness, lock, dirty-state, failed-operation, provider-status, sync-status, projection-freshness) — **seed-only, no projection anywhere.** `.Save(...)` is called only in tests; production returns safe `NotFoundSafe`. Deferred to **Epic 11 Story 11.10** (owner Amelia / Winston): author + register an EventStore-backed projection in a Server-referenceable project.
status: open

### DW-2: Author the production workspace-transition-evidence projection

origin: migrated from legacy ledger ("Deferred from: bmad-correct-course seed-backed read-model decision (2026-07-07)"), 2026-08-24
location: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:86
reason: `IWorkspaceTransitionEvidenceReadModel` deployed default is `InMemoryWorkspaceTransitionEvidenceReadModel` (`src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:86`, `TryAddSingleton`) — C6 lifecycle transition evidence (FR46) — **seed-only, no projection anywhere.** Deferred to Epic 11 Story 11.10 (owner Amelia / Winston): author and register an EventStore-backed projection in a Server-referenceable project.
status: open

### DW-3: Scope note: sibling read models already have deterministic projection logic

origin: migrated from legacy ledger ("Deferred from: bmad-correct-course seed-backed read-model decision (2026-07-07)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:233-289
reason: **Scope note (distinguishes these two from the sibling read models):** the six event-replay-projected read models — folder lifecycle, branch/ref policy, workspace lock, workspace status, cleanup status, task status — already have deterministic projection logic in `InMemoryFolderRepository` (`src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:233-289`) and are pinned by Story 4.15 (NFR52). Their remaining gap is production EventStore wiring only. The two read models above have **no** projection logic today, so Story 11.10 must **author** the projection as well as wire it.
status: open

### DW-4: NFR posture: NFR51 (read-model-based console) and NFR52 (empty-rebuild determinism) remain honestly covered

origin: migrated from legacy ledger ("Deferred from: bmad-correct-course seed-backed read-model decision (2026-07-07)"), 2026-08-24
location: docs/exit-criteria/nfr-traceability.md
reason: **NFR posture:** NFR51 (read-model-based console) and NFR52 (empty-rebuild determinism) remain honestly covered — the console is read-model-based and an unpopulated production read model returns deterministically empty. `docs/exit-criteria/nfr-traceability.md` rows left unchanged (fingerprinted governance rows). Optional gate-lockstep follow-up (owner Murat) if the deferral should be surfaced on the NFR51/NFR52 rows.
status: open

### DW-5: `FolderDomainProcessor:50-52` `command.CommandType` case-sensitivity

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: FolderDomainProcessor:50-52
reason: `FolderDomainProcessor:50-52` `command.CommandType` case-sensitivity — CommandType is canonical and emitted by the REST endpoint; case-mismatched arrival implies an upstream bug, not a wire concern. Deferred — pre-existing convention.
status: open

### DW-6: `LayeredAuthBackedFolderArchiveAclEvidenceProvider:23-39` trailing-whitespace tenant mismatch

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: LayeredAuthBackedFolderArchiveAclEvidenceProvider:23-39
reason: `LayeredAuthBackedFolderArchiveAclEvidenceProvider:23-39` trailing-whitespace tenant mismatch — depends on upstream tenant normalization invariants enforced by layered authorization. Deferred — out-of-scope normalization concern; revisit if a non-normalized identifier path emerges.
status: open

### DW-7: `FolderCommandRejected.Create` callable via reflection bypass

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: src/Hexalith.Folders.Server/FolderCommandRejected.cs:59-78
reason: `FolderCommandRejected.Create` callable via reflection bypass [`src/Hexalith.Folders.Server/FolderCommandRejected.cs:59-78`] — System.Text.Json does not deserialize records with a private constructor unless `[JsonConstructor]` is explicitly applied; no realistic attack vector today. Deferred — revisit if the rejection type joins any deserialization surface.
status: open

### DW-8: Concurrent identical-fingerprint clock skew at `InMemoryFolderRepository:56-91`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: InMemoryFolderRepository:56-91
reason: Concurrent identical-fingerprint clock skew at `InMemoryFolderRepository:56-91` — current fingerprint composition is deterministic-per-inputs and does not include clock material. Deferred — only becomes live if a future fingerprint dimension adds time-based material.
status: open

### DW-9: (Round-3 echo) `IDomainProcessor.ProcessAsync` lacks `CancellationToken`, so `FolderDomainProcessor` passes `CancellationToken.None` into evidence providers

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: IDomainProcessor.ProcessAsync
reason: **(Round-3 echo)** `IDomainProcessor.ProcessAsync` lacks `CancellationToken`, so `FolderDomainProcessor` passes `CancellationToken.None` into evidence providers. ADR 0001 explicitly accepts this tradeoff. Deferred — the round-4 catch-block patch (`when (ex is not OperationCanceledException)`) is the right scope-level mitigation; revisit when the EventStore framework's `IDomainProcessor` contract gains a CT.
status: open

### DW-10: (Round-3 echo) `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: InMemoryFolderRepository
reason: **(Round-3 echo)** `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary` — the lock is the real serialization primitive. Deferred — style cleanup; revisit when an EventStore-backed repository replaces the in-memory implementation as the production default.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-inmemory-repository-lock-cleanup
resolution-undo: 0f576fc559595d239f0a8032672b2a2033e87f3e1ec77133cdd65ef323dca046 2026-08-28 7374617475733a206f70656e

### DW-11: (Round-3 echo) `FolderCommandRejected` projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: FolderCommandRejected
reason: **(Round-3 echo)** `FolderCommandRejected` projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit. Deferred — design-level documentation work; introduce an explicit marker if a future projection consumes `IRejectionEvent`.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Server/FolderCommandRejected.cs:9-18 documents the rejection's wire/log/audit role and implements only IRejectionEvent; src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs:5-8 restricts projection payloads to IFolderEvent.

### DW-12: Add gateway 5xx Theory cases for 503/505/507/599 in `ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable
reason: Add gateway 5xx Theory cases for 503/505/507/599 in `ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable`. Deferred — the `>= 500 and < 600` catch-all production arm covers the behavior; this is regression-trap coverage.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-archive-endpoint-hardening
resolution-undo: 6f917a24a4b79a2f7714e8b3186622f47192753a30463ad014765f32d9daddf9 2026-08-28 7374617475733a206f70656e

### DW-13: Add a `GatewayCorrelationRegex` header-injection Theory in `ArchiveFolderEndpointTests` proving CR/LF / oversized / control-character bytes are rejected before being reflected into `X-Correlation-Id`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: GatewayCorrelationRegex
reason: Add a `GatewayCorrelationRegex` header-injection Theory in `ArchiveFolderEndpointTests` proving CR/LF / oversized / control-character bytes are rejected before being reflected into `X-Correlation-Id`. Deferred.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-archive-endpoint-hardening
resolution-undo: 6f917a24a4b79a2f7714e8b3186622f47192753a30463ad014765f32d9daddf9 2026-08-28 7374617475733a206f70656e

### DW-14: Add a cancel-mid-flight integration test to `ArchiveFolderProcessWiringTests` that exercises the in-processor cancellation/cleanup path (current `CancelledRequestShouldStopBeforeGatewayRoundTrip`…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveFolderProcessWiringTests
reason: Add a cancel-mid-flight integration test to `ArchiveFolderProcessWiringTests` that exercises the in-processor cancellation/cleanup path (current `CancelledRequestShouldStopBeforeGatewayRoundTrip` only verifies the HttpClient-level cancel before the request leaves the test). Deferred.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-15: Replace `InProcessEventStoreGatewayClient.ToGatewayException`'s ad-hoc `FolderResultCode → HTTP status` mapping with a shared mapping path used by the production EventStore gateway

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: InProcessEventStoreGatewayClient.ToGatewayException
reason: Replace `InProcessEventStoreGatewayClient.ToGatewayException`'s ad-hoc `FolderResultCode → HTTP status` mapping with a shared mapping path used by the production EventStore gateway. Deferred — current test mapping is consistent with the safe-denial REST contract but duplicates logic.
status: done 2026-08-29
resolution: resolved by sweep bundle dw-archive-gateway-canonical-mapping
resolution-undo: 131c1f336964dd52c5eba9e851a53d7590b7df8753625e01e11ab2eda25ad0ee 2026-08-29 7374617475733a206f70656e

### DW-16: Add `FolderCommandRejected` to `FolderArchiveMetadataLeakageTests` sentinel iteration so every `tests/fixtures/audit-leakage-corpus.json` value is asserted absent across the new rejection-event…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: tests/fixtures/audit-leakage-corpus.json
reason: Add `FolderCommandRejected` to `FolderArchiveMetadataLeakageTests` sentinel iteration so every `tests/fixtures/audit-leakage-corpus.json` value is asserted absent across the new rejection-event payload. Deferred — the production `FolderCommandRejected.Create` factory canonicalizes all identifiers at construction time which mitigates the leak vector, but corpus-driven coverage remains a regression trap.
status: done 2026-08-29
resolution: resolved by sweep bundle dw-archive-leakage-regression-coverage
resolution-undo: 97be6c1f7934e86d73a377d9abaf72fc20778f943083b4fcbdef5459ac0f70be 2026-08-29 7374617475733a206f70656e

### DW-17: Add `ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted` integration test covering REST → gateway → `/process` → gate same-key + equivalent-payload replay path

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted
reason: Add `ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted` integration test covering REST → gateway → `/process` → gate same-key + equivalent-payload replay path. Deferred — gate-unit and endpoint-unit replay coverage exists; the round-trip is unverified end-to-end.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-18: Add a foreign-tenant smuggling integration test (`ArchiveRequestShouldRejectWhenEnvelopeTenantDisagreesWithAuthenticatedTenant`)

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveRequestShouldRejectWhenEnvelopeTenantDisagreesWithAuthenticatedTenant
reason: Add a foreign-tenant smuggling integration test (`ArchiveRequestShouldRejectWhenEnvelopeTenantDisagreesWithAuthenticatedTenant`). Deferred — gate-unit coverage of `HasCompetingClientTenant` plus layered-auth tenant comparison at the request handler provide defense-in-depth.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-19: Add a `DenyingFolderArchivePolicyEvidenceProvider` test fake and an integration test exercising the AC8 policy-denied path end-to-end through `/process`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: FolderArchivePolicyOutcome.Denied
reason: Add a `DenyingFolderArchivePolicyEvidenceProvider` test fake and an integration test exercising the AC8 policy-denied path end-to-end through `/process`. Deferred to Epic 7 when the production policy provider lands; gate-unit coverage of `FolderArchivePolicyOutcome.Denied` exists.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-20: `IDomainProcessor.ProcessAsync` lacks `CancellationToken`, so `FolderDomainProcessor` passes `CancellationToken.None` into the ACL/policy evidence providers

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: IDomainProcessor.ProcessAsync
reason: `IDomainProcessor.ProcessAsync` lacks `CancellationToken`, so `FolderDomainProcessor` passes `CancellationToken.None` into the ACL/policy evidence providers. ADR 0001 explicitly accepts this tradeoff (providers are deterministic in-memory operations bounded by the layered-auth context). Deferred — revisit when the EventStore framework's `IDomainProcessor` contract gains a CT.
status: open

### DW-21: `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary`; the lock is the real serialization primitive and the dictionary's thread-safety adds nothing

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: InMemoryFolderRepository
reason: `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary`; the lock is the real serialization primitive and the dictionary's thread-safety adds nothing. Deferred — style cleanup; revisit when an EventStore-backed `IFolderRepository` replaces the in-memory implementation as the production default.
status: done 2026-08-28
resolution: resolved by sweep bundle dw-inmemory-repository-lock-cleanup
resolution-undo: 0f576fc559595d239f0a8032672b2a2033e87f3e1ec77133cdd65ef323dca046 2026-08-28 7374617475733a206f70656e

### DW-22: `SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence` does not detect a scenario where the scoped accessor is accidentally re-registered as singleton; a singleton accessor with…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence
reason: `SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence` does not detect a scenario where the scoped accessor is accidentally re-registered as singleton; a singleton accessor with manual clearing in `finally` would also pass. Deferred — architectural test-design concern; revisit when DI lifetime auditing tooling is in place.
status: done 2026-08-29
resolution: resolved by sweep bundle dw-archive-accessor-lifetime-guard
resolution-undo: 8074c733b44d14cabce95cbf923fe8eeda11ad3a80fa107663b65ae29345066c 2026-08-29 7374617475733a206f70656e

### DW-23: `FolderCommandRejected` is a new `IRejectionEvent` type; the projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: FolderCommandRejected
reason: `FolderCommandRejected` is a new `IRejectionEvent` type; the projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit. Deferred — document the contract or introduce an explicit marker if a future projection consumes `IRejectionEvent`.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Server/FolderCommandRejected.cs:9-18 and src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs:5-8 establish an explicit rejection-versus-domain-event boundary, resolving the projection ambiguity.

### DW-24: Validator-version fingerprint skew — cross-deploy `IsSafeEvidenceIdentifier` changes can invalidate prior idempotency replay

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: IsSafeEvidenceIdentifier
reason: Validator-version fingerprint skew — cross-deploy `IsSafeEvidenceIdentifier` changes can invalidate prior idempotency replay. Deferred — needs a versioned-validator regression test once a second validator version exists.
status: open

### DW-25: `FolderListProjection.Apply` throw-on-missing-create tears down multi-tenant rebuild while `FolderStateApply` is per-stream

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderListProjection.Apply
reason: `FolderListProjection.Apply` throw-on-missing-create tears down multi-tenant rebuild while `FolderStateApply` is per-stream — asymmetric blast radius. Deferred — revisit when projection-rebuild tooling and replay diagnostics are introduced (Epic 6/7).
status: done 2026-08-28
decision: 2026-08-28 Keep fail-fast rebuild — Treat missing-create ordering as repository corruption that must stop the rebuild and require operator intervention.
resolution: closed by human decision: Treat missing-create ordering as repository corruption that must stop the rebuild and require operator intervention.
decision: 2026-08-28 Keep fail-fast rebuild — Treat missing-create ordering as repository corruption that must stop the rebuild and require operator intervention.

### DW-26: `FolderArchiveAclEvidence` is an unsigned value object

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchiveAclEvidence
reason: `FolderArchiveAclEvidence` is an unsigned value object — defense relies on trustworthy upstream `Allowed(...)` callers. Deferred — architectural concern, addresses with evidence-signing or capability-token redesign beyond Story 2.8.
status: done 2026-08-25
resolution: closed by human decision: Document the trust boundary and treat evidence as an internal value inside the trusted server process.
decision: 2026-08-25 Accept in-process boundary — Document the trust boundary and treat evidence as an internal value inside the trusted server process.

### DW-27: `FolderState` six adjacent same-typed nullable-string parameters (silent-swap risk on positional construction)

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderState
reason: `FolderState` six adjacent same-typed nullable-string parameters (silent-swap risk on positional construction) — current callers use `with`-syntax. Deferred — revisit if a positional caller is introduced.
status: open

### DW-28: `FolderArchived.IdempotencyFingerprint` non-empty invariant undocumented at event level

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchived.IdempotencyFingerprint
reason: `FolderArchived.IdempotencyFingerprint` non-empty invariant undocumented at event level — not currently reachable from `FolderAggregate.Handle`. Deferred — promote to an event-level invariant if a non-aggregate writer appears.
status: open

### DW-29: `ArchiveFolder.PayloadTenantId` with malformed segment matching the authoritative tenant — narrow edge case. Deferred — revisit if payload-tenant smuggling becomes a verified threat.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: ArchiveFolder.PayloadTenantId
reason: `ArchiveFolder.PayloadTenantId` with malformed segment matching the authoritative tenant — narrow edge case. Deferred — revisit if payload-tenant smuggling becomes a verified threat.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:62-70 validates and rejects reserved or invalid authoritative tenants before archive handling, including when a malformed payload tenant equals the authority.

### DW-30: `ArchiveFolderClientConformanceTests` parameter-order assertion is brittle to NSwag generator changes

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: ArchiveFolderClientConformanceTests
reason: `ArchiveFolderClientConformanceTests` parameter-order assertion is brittle to NSwag generator changes — currently passing. Deferred — relax to a set-equality assertion when a generator upgrade breaks it.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-generated-client-conformance
resolution-undo: ef4b096a597db99515c9fb5fd152ab76f1be034dca947c7297c3c5207593bd6f 2026-09-02 7374617475733a206f70656e

### DW-31: `FolderArchiveMetadataLeakageTests` asserts the validator's input restriction indirectly via factory defaults

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchiveMetadataLeakageTests
reason: `FolderArchiveMetadataLeakageTests` asserts the validator's input restriction indirectly via factory defaults — gives confirmation, not coverage. Deferred — expand to a corpus-driven property test in a future hardening pass.
status: done 2026-08-29
resolution: resolved by sweep bundle dw-archive-leakage-regression-coverage
resolution-undo: 97be6c1f7934e86d73a377d9abaf72fc20778f943083b4fcbdef5459ac0f70be 2026-08-29 7374617475733a206f70656e

### DW-32: Untested branches: `FolderArchivePolicyOutcome.ScopeMismatch`, several `FolderArchiveAclOutcome` variants, `FolderAppendOutcome.FingerprintConflict` mapping

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchivePolicyOutcome.ScopeMismatch
reason: Untested branches: `FolderArchivePolicyOutcome.ScopeMismatch`, several `FolderArchiveAclOutcome` variants, `FolderAppendOutcome.FingerprintConflict` mapping — become live once the gate is wired into production. Deferred until that happens.
status: done 2026-08-29
resolution: resolved by sweep bundle dw-archive-gate-outcome-coverage
resolution-undo: 42f54c5f9b4c96c48c0c8c4827569fd192cf4549979c3bcd8a2d0f77e64b9233 2026-08-29 7374617475733a206f70656e

### DW-33: `FolderArchiveTenantGate(TimeProvider)` constructor never exercised by tests or production callers. Deferred — moot until the gate is wired.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: n/a
reason: `FolderArchiveTenantGate(TimeProvider)` constructor never exercised by tests or production callers. Deferred — moot until the gate is wired.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:45-46 registers TimeProvider.System and FolderArchiveTenantGate; tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:734-735,769-770 injects a fixed TimeProvider through the host and exercises the two-argument primary constructor.

### DW-34: No `CancellationToken` propagation through `FolderArchiveTenantGate.Handle` or `IFolderRepository` methods. Deferred — moot until the gate is wired; add async port at the same time.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchiveTenantGate.Handle
reason: No `CancellationToken` propagation through `FolderArchiveTenantGate.Handle` or `IFolderRepository` methods. Deferred — moot until the gate is wired; add async port at the same time.
status: open

### DW-35: `EffectivePermissionsActionCatalog` insertion order untested — cosmetic until a positional consumer appears. Deferred.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: EffectivePermissionsActionCatalog
reason: `EffectivePermissionsActionCatalog` insertion order untested — cosmetic until a positional consumer appears. Deferred.
status: done 2026-08-29
resolution: resolved by sweep bundle dw-effective-action-contracts
resolution-undo: 8c06abcaac18bf48049224e367f28b74323a66545b9d73cdbce66560c048d443 2026-08-29 7374617475733a206f70656e

### DW-36: `IFolderEvent` interface coupling — `FolderProjectionEnvelope.Event` was widened from `FolderCreated` to `IFolderEvent`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation (2026-05-20)"), 2026-08-24
location: FolderProjectionEnvelope.Event
reason: `IFolderEvent` interface coupling — `FolderProjectionEnvelope.Event` was widened from `FolderCreated` to `IFolderEvent`. Other consumers of the envelope (workers, snapshots, generated code, tests outside this diff) must remain consistent. Deferred — broader event-interface design beyond Story 2.8 scope.
status: done 2026-09-01
resolution: already resolved: src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs:5-8 accepts IFolderEvent, and tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs:173-181 verifies an archived event through the widened envelope.

### DW-37: Archive metadata fields not cleared on a hypothetical unarchive event

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation (2026-05-20)"), 2026-08-24
location: FolderState.LifecycleState
reason: Archive metadata fields not cleared on a hypothetical unarchive event — `FolderState.LifecycleState`, archive reason category, and projection archived-at fields would need a reset path if restore/unarchive is introduced. Deferred — Story 2.8 explicitly excludes restore/unarchive (AC14, regression traps); revisit when a restore/unarchive story is scoped.
status: open

### DW-38: AC10 "archive reason category" surfacing on `FolderLifecycleStatus`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation (2026-05-20)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7165-7185
reason: AC10 "archive reason category" surfacing on `FolderLifecycleStatus` — the `FolderLifecycleStatus` schema [`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7165-7185`] is `additionalProperties: false` and has no reason field. Adding the field requires a Contract Spine update via the contract-workflow story path per the Do-Not-Touch rule.
status: open
decision: 2026-08-25 Add archive reason — Add optional archiveReasonCode through the contract workflow and regenerate every consumer.

### DW-39: `FolderAuthorizationDenialMapper` emits non-canonical categories

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs:45-70
reason: `FolderAuthorizationDenialMapper` emits non-canonical categories (`not_found_to_caller`, `policy_denied`, `policy_evidence_unavailable`) [`src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs:45-70`] — deferred, pre-existing from Story 2.6 and shared by other endpoints; fixing requires a coordinated category-vocabulary update across operations and SDK callers.
status: open
decision: 2026-08-25 Promote existing tokens — Add the deployed tokens to the canonical vocabulary, oracle, documentation, and contract checks without changing the wire.

### DW-40: `FolderLifecycleStatus.lifecycleState` schema conflates lifecycle and binding tokens

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7076-7089
reason: `FolderLifecycleStatus.lifecycleState` schema conflates lifecycle and binding tokens [`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7076-7089`] — deferred, Contract Spine design; the implementation conforms to the spec as written. Adjusting requires a contract-workflow story.
status: open
decision: 2026-08-25 Separate dimensions — Add versioned lifecycle and binding fields with compatibility mapping for the existing combined field.

### DW-41: Wire the lifecycle-status read model in production

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:42
reason: `InMemoryFolderLifecycleStatusReadModel` registered as production default [`src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:42`] — deferred, intentional pattern matching `InMemoryFolderTenantAccessProjectionStore` and `InMemoryEffectivePermissionsReadModel`. Production deployment is expected to register the real implementation; revisit in Epic 7 production wiring. A later 2026-07-07 course correction superseded the stale Epic 7 ownership and moved production projection wiring to Epic 11 Story 11.10.
status: open

### DW-42: Singleton lifetime for query handler and read model risks captive dependency if any collaborator becomes scoped

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:42-43
reason: Singleton lifetime for query handler and read model risks captive dependency if any collaborator becomes scoped [`src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:42-43`] — deferred, all current dependencies are singletons; revisit when scoped seams are introduced.
status: open

### DW-43: `DiagnosticSentinels` on `FolderLifecycleStatusReadModelSnapshot` is never read by the handler

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusReadModelSnapshot.cs:12
reason: `DiagnosticSentinels` on `FolderLifecycleStatusReadModelSnapshot` is never read by the handler [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusReadModelSnapshot.cs:12`] — deferred, harmless dead state; may anchor future redaction-enforcement logic.
status: done 2026-09-01
resolution: already resolved: tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusMetadataLeakageTests.cs:53-75 now uses DiagnosticSentinels as an active redaction negative control and proves each sentinel is absent from the serialized response.

### DW-44: `HasNoBindingReferences` duplicates `HasValue` logic [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:321-326`] — deferred, cosmetic consolidation.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:321-326
reason: `HasNoBindingReferences` duplicates `HasValue` logic [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:321-326`] — deferred, cosmetic consolidation.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-45: `Save` is not on `IFolderLifecycleStatusReadModel` interface

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/InMemoryFolderLifecycleStatusReadModel.cs:9
reason: `Save` is not on `IFolderLifecycleStatusReadModel` interface — test-only seam [`src/Hexalith.Folders/Queries/Folders/InMemoryFolderLifecycleStatusReadModel.cs:9`] — deferred, intentional pattern; promote to `IFolderLifecycleStatusSeed` when a second backing store appears.
status: open

### DW-46: `FolderLifecycleProjectionState.Unknown` is handled by the switch's `_` arm, never matched by name

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:117-132
reason: `FolderLifecycleProjectionState.Unknown` is handled by the switch's `_` arm, never matched by name [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:117-132`] — deferred, cosmetic.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:154-156 explicitly returns FolderLifecycleProjectionState.Unknown for the relevant state.

### DW-47: Test files use `ConfigureAwait(true)` while production handler uses `ConfigureAwait(false)` [`tests/Hexalith.Folders.Tests/Queries/Folders/*.cs`] — deferred, style inconsistency.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Queries/Folders/*.cs
reason: Test files use `ConfigureAwait(true)` while production handler uses `ConfigureAwait(false)` [`tests/Hexalith.Folders.Tests/Queries/Folders/*.cs`] — deferred, style inconsistency.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-lifecycle-test-hygiene
resolution-undo: 9ffcf9ab21c47fbee1041561963d8a61a6e0c8d5a978d40e48b6584ea52dfbcb 2026-09-02 7374617475733a206f70656e

### DW-48: `ActorSafeIdentifier: "actor_present"` magic string [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:43`] — deferred, extract to a named constant.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:43
reason: `ActorSafeIdentifier: "actor_present"` magic string [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:43`] — deferred, extract to a named constant.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:16,56-61 extracts and uses ActorPresentIdentifier.

### DW-49: `AllowedOutcome` and `DeniedSafeOutcome` string constants in handler instead of an enum

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:12-13
reason: `AllowedOutcome` and `DeniedSafeOutcome` string constants in handler instead of an enum [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:12-13`] — deferred, parallel representation to `Code` invites drift.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-50: `ReasonCode` null-coalesce ordering is inconsistent across branches and can bury handler-determined reasons

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,126,189-194,279-287
reason: `ReasonCode` null-coalesce ordering is inconsistent across branches and can bury handler-determined reasons [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,126,189-194,279-287`] — deferred, refactor pass to consolidate.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-51: Snapshot freshness mutation idiom repeated and `ProjectionWatermark` preserved on `Unavailable` outcomes

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,189-194,279-287
reason: Snapshot freshness mutation idiom repeated and `ProjectionWatermark` preserved on `Unavailable` outcomes [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,189-194,279-287`] — deferred, refactor pass.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-52: `LifecycleStatusClientConformanceTests` asserts `methods.Single(m => ...)` and locks NSwag parameter mangling

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/LifecycleStatusClientConformanceTests.cs
reason: `LifecycleStatusClientConformanceTests` asserts `methods.Single(m => ...)` and locks NSwag parameter mangling [`tests/Hexalith.Folders.Client.Tests/LifecycleStatusClientConformanceTests.cs`] — deferred, brittle to generator upgrades.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-generated-client-conformance
resolution-undo: ef4b096a597db99515c9fb5fd152ab76f1be034dca947c7297c3c5207593bd6f 2026-09-02 7374617475733a206f70656e

### DW-53: `MapFoldersServerEndpointsShouldRegisterLifecycleStatusRoute` builds an app without `await using` disposal

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs
reason: `MapFoldersServerEndpointsShouldRegisterLifecycleStatusRoute` builds an app without `await using` disposal [`tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs`] — deferred, resource leak in test process.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-lifecycle-test-hygiene
resolution-undo: 9ffcf9ab21c47fbee1041561963d8a61a6e0c8d5a978d40e48b6584ea52dfbcb 2026-09-02 7374617475733a206f70656e

### DW-54: `FolderLifecycleStatusTestSupport` builds `EventStoreClaimTransformEvidence.Allowed(...)` with nullable tenant/principal parameters

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusTestSupport.cs
reason: `FolderLifecycleStatusTestSupport` builds `EventStoreClaimTransformEvidence.Allowed(...)` with nullable tenant/principal parameters [`tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusTestSupport.cs`] — deferred, opaque test scaffolding.
status: done 2026-09-02
resolution: resolved by sweep bundle dw-lifecycle-test-hygiene
resolution-undo: 9ffcf9ab21c47fbee1041561963d8a61a6e0c8d5a978d40e48b6584ea52dfbcb 2026-09-02 7374617475733a206f70656e

### DW-55: Lifecycle 200 response does not echo `taskId` body field even when `X-Hexalith-Task-Id` is read

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:107-119
reason: Lifecycle 200 response does not echo `taskId` body field even when `X-Hexalith-Task-Id` is read [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:107-119`] — deferred, requires contract update to declare `taskId` in `FolderLifecycleStatus`.
status: done 2026-08-25
resolution: closed by human decision: Document the response header as the canonical transport-level task correlation.
decision: 2026-08-25 Keep response header — Document the response header as the canonical transport-level task correlation.

### DW-56: AC #9 principal-source classes are not surfaced in the response

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-inspect-effective-permissions (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/EffectivePermissionsQueryResult.cs
reason: AC #9 principal-source classes are not surfaced in the response [`src/Hexalith.Folders/Authorization/EffectivePermissionsQueryResult.cs`] — deferred, requires Contract Spine extension via the contract workflow. `EffectivePermissionEvidenceSource` is computed internally; OpenAPI `EffectivePermissions` schema is `additionalProperties: false`. Project rule "Do Not Touch" forbids changing the Contract Spine outside a dedicated contract-workflow story.
status: open
decision: 2026-08-25 Add sanitized sources — Add a sanitized principalSources field through the contract workflow and regenerate clients.

### DW-57: AC #5/#6 action-token granularity is collapsed to `FolderPermissionLevel`

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-inspect-effective-permissions (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs
reason: AC #5/#6 action-token granularity is collapsed to `FolderPermissionLevel` (`read/write/administer`) [`src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs`] — deferred, same Contract Spine constraint as above. Per-action revoke precedence is still computed correctly inside `Compute` (revokes win over grants per `(principal, action)` tuple); the response shape does not expose the per-action distinction. Granular exposure requires a contract-workflow story.
status: open
decision: 2026-08-25 Add action grants — Add an additive granular action-grants field while preserving existing coarse levels, then regenerate clients.

### DW-58: `InMemoryEffectivePermissionsReadModel` key scope is `(managedTenantId, folderId)` only

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-inspect-effective-permissions (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/InMemoryEffectivePermissionsReadModel.cs:27
reason: `InMemoryEffectivePermissionsReadModel` key scope is `(managedTenantId, folderId)` only [`src/Hexalith.Folders/Authorization/InMemoryEffectivePermissionsReadModel.cs:27`] — deferred, testing-only seam; project context "Critical Don't-Miss" requires production cache keys scoped by authoritative tenant, folder, principal, task/workspace scope, revocation watermark, and read-consistency class. The handler post-filters evidence rows by principal so the response is correct today. Revisit when a durable production read model replaces the in-memory implementation.
status: open

### DW-59: `EffectivePermissionPrincipal` record equality is case-sensitive on `PrincipalId`

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-inspect-effective-permissions (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/EffectivePermissionPrincipal.cs
reason: `EffectivePermissionPrincipal` record equality is case-sensitive on `PrincipalId` [`src/Hexalith.Folders/Authorization/EffectivePermissionPrincipal.cs`] — deferred, convention; production auth pipeline is expected to canonicalize casing before the handler sees it. Add explicit `StringComparer.OrdinalIgnoreCase` or pipeline-side normalization when a real IDP integration requires it.
status: open
decision: 2026-08-28 Normalize per identity provider — Define provider-aware canonicalization at the authentication boundary, document it, and add equality and projection tests.
decision: 2026-08-28 Normalize per identity provider — Define provider-aware canonicalization at the authentication boundary, document it, and add equality and projection tests.

### DW-60: `EffectivePermissionsTaskScope.AllowedActions` is `IReadOnlySet<string>` without an enforced comparer

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-inspect-effective-permissions (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs
reason: `EffectivePermissionsTaskScope.AllowedActions` is `IReadOnlySet<string>` without an enforced comparer [`src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs`] — deferred, testing-only seam; production task-scope projection must construct the set with `StringComparer.Ordinal` to match the action catalog. Document the contract on the type when the production task-scope projection lands.
status: done 2026-08-29
resolution: resolved by sweep bundle dw-effective-action-contracts
resolution-undo: 8c06abcaac18bf48049224e367f28b74323a66545b9d73cdbce66560c048d443 2026-08-29 7374617475733a206f70656e

### DW-61: Async port for `FolderAccessTenantGate` [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs`]

origin: migrated from legacy ledger ("Deferred from: code review of 2-4-grant-and-revoke-folder-access (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs
reason: Async port for `FolderAccessTenantGate` [`src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs`] — deferred, pre-existing; `IFolderRepository` is synchronous from Story 2.3. Revisit when EventStore integration replaces the repository with an async port that propagates `CancellationToken`.
status: open

### DW-62: Idempotency key collision risk when new significant fields are added to `FolderAccessOperation`

origin: migrated from legacy ledger ("Deferred from: code review of 2-4-grant-and-revoke-folder-access (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:874-907
reason: Idempotency key collision risk when new significant fields are added to `FolderAccessOperation` [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:874-907`] — deferred, theoretical; current fingerprint covers all existing fields. Revisit when extending `FolderAccessOperation`.
status: open

### DW-63: Rename test `AlreadyGrantedAccessShouldReturnAlreadyAppliedWithoutDuplicateEvent` to reflect its `HasFolderAccess` short-circuit (not idempotency-fingerprint) coverage

origin: migrated from legacy ledger ("Deferred from: code review of 2-4-grant-and-revoke-folder-access (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessCommandValidationTests.cs:~1508
reason: Rename test `AlreadyGrantedAccessShouldReturnAlreadyAppliedWithoutDuplicateEvent` to reflect its `HasFolderAccess` short-circuit (not idempotency-fingerprint) coverage [`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderAccessCommandValidationTests.cs:~1508`] — deferred, cosmetic; test asserts the right outcome via the right code path, only the name is misleading.
status: open

### DW-64: `InvalidFolderMetadata` collapses length / control-char / forbidden-term failures into a single code

origin: migrated from legacy ledger ("Deferred from: code review of 2-3-create-folders-within-a-tenant (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:64-79
reason: `InvalidFolderMetadata` collapses length / control-char / forbidden-term failures into a single code [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:64-79`] — deferred, pre-existing coarse-grained pattern (Story 2.2 makes the same trade-off); splitting requires expanding the public code surface and updating consumer error-handling.
status: done 2026-08-25
resolution: closed by human decision: Document the stable, non-revealing coarse code as deliberate.
decision: 2026-08-25 Keep coarse code — Document the stable, non-revealing coarse code as deliberate.

### DW-65: `IdempotentReplay` outcome is returned via `FolderResult.Rejected(...)` even though it is a successful equivalence; `FolderResult` has no `IsAccepted` helper

origin: migrated from legacy ledger ("Deferred from: code review of 2-3-create-folders-within-a-tenant (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs
reason: `IdempotentReplay` outcome is returned via `FolderResult.Rejected(...)` even though it is a successful equivalence; `FolderResult` has no `IsAccepted` helper [`src/Hexalith.Folders/Aggregates/Folder/FolderResult.cs`] — deferred, behavior is correct, cosmetic API clarity only; revisit when CLI/MCP/SDK adapters need to dispatch on accepted-vs-rejected.
status: open
decision: 2026-08-25 Add acceptance semantics — Add an IsAccepted property or semantic factory and migrate internals while preserving result codes.

### DW-66: `FolderCreateAclEvidence.Action` is declared non-nullable but no compile-time guarantee prevents deserializer-produced `null`

origin: migrated from legacy ledger ("Deferred from: code review of 2-3-create-folders-within-a-tenant (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/FolderCreateAclEvidence.cs
reason: `FolderCreateAclEvidence.Action` is declared non-nullable but no compile-time guarantee prevents deserializer-produced `null` [`src/Hexalith.Folders/Aggregates/Folder/FolderCreateAclEvidence.cs`] — deferred, applies to every record in `Aggregates/Folder/`; revisit when contract-level deserialization hardening is introduced.
status: open

### DW-67: `Resolve-Path (Join-Path $toolsParent '..')` resolves symlink targets in `tests/tools/run-governance-completeness-gates.ps1:16`

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18)"), 2026-08-24
location: tests/tools/run-governance-completeness-gates.ps1:16
reason: `Resolve-Path (Join-Path $toolsParent '..')` resolves symlink targets in `tests/tools/run-governance-completeness-gates.ps1:16` — deferred, sibling scripts use the same pattern; revisit when a sibling tool persists absolute paths.
status: open

### DW-68: `CloneRow` round-trips YAML through serialize/parse and drops anchors, tags, and comments

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:991-1000
reason: `CloneRow` round-trips YAML through serialize/parse and drops anchors, tags, and comments [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:991-1000`] — deferred, only used by negative-control tests; revisit if production validators start caring about tag/anchor metadata.
status: open

### DW-69: `EvaluateExitCriteriaRows` can emit both `exit_criteria_duplicate` and `exit_criteria_malformed` for the same Cx when duplicates exist

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:742-790
reason: `EvaluateExitCriteriaRows` can emit both `exit_criteria_duplicate` and `exit_criteria_malformed` for the same Cx when duplicates exist [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:742-790`] — deferred, noise rather than correctness.
status: open

### DW-70: `FindRepositoryRoot` throws `InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: ...")`

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:1099-1115
reason: `FindRepositoryRoot` throws `InvalidOperationException("GOVERNANCE-PREREQUISITE-DRIFT: ...")` — exception terminology hints at a categorized exit but the throw is uncaught and surfaces as a Shouldly/xUnit crash [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:1099-1115`] — deferred, will be resolved by the decision on `prerequisite_drift` script semantics.
status: open
decision: 2026-08-25 Preserve drift status — Emit structured prerequisite_drift report and exit semantics with focused runner tests.

### DW-71: Encoding/BOM handling in `File.ReadAllText` calls inside `SafetyInvariantGateTests`

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-17)"), 2026-08-24
location: File.ReadAllText
reason: Encoding/BOM handling in `File.ReadAllText` calls inside `SafetyInvariantGateTests` — deferred, low likelihood any scanned file in the safety scope is non-UTF-8 today; revisit when scan inputs broaden beyond JSON/YAML/Markdown sources we author.
status: open

### DW-72: JSON duplicate-keys detection in corpus / inventory / quarantine fixtures

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-17)"), 2026-08-24
location: JsonDocument
reason: JSON duplicate-keys detection in corpus / inventory / quarantine fixtures — deferred; relying on `JsonDocument` default behavior is acceptable for fixtures we own and write by hand. Revisit if multiple authors begin merging the corpus concurrently.
status: open

### DW-73: `AssertMetadataOnly` blacklist missing Linux absolute-path roots

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-17)"), 2026-08-24
location: AssertMetadataOnly
reason: `AssertMetadataOnly` blacklist missing Linux absolute-path roots (`/var/`, `/tmp/`, `/home/`, `/Users/`) — deferred. The gate runs primarily on Windows today, and the inventory `structured_exclusions` does not yet target Linux runner paths. Revisit when CI matrix expands to Linux/macOS or when a contributor reports a Linux-specific leak class.
status: open

### DW-74: Isolated regeneration does not copy repo-root `nuget.config`

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:214-227
reason: Isolated regeneration does not copy repo-root `nuget.config` [`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:214-227`]. CI runner happens to use the same `nuget.org`-only sources today; revisit if a private feed is added.
status: open

### DW-75: Removal of `--no-build` from generator-invoking tests adds an incremental MSBuild check per test

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:404
reason: Removal of `--no-build` from generator-invoking tests adds an incremental MSBuild check per test [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:404`]. Accepted trade-off for the `obj/` lock race; `[Collection("ParityOracleGenerator")]` keeps it serial.
status: done 2026-08-24
resolution: already resolved: Commit 2ff62a7; tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:406 invokes dotnet run with --no-restore and --no-build.

### DW-76: Workflow lacks `concurrency:`, `timeout-minutes:`, and `workflow_dispatch:`

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: .github/workflows/contract-spine.yml
reason: Workflow lacks `concurrency:`, `timeout-minutes:`, and `workflow_dispatch:` [`.github/workflows/contract-spine.yml`]. Hygiene items not required by AC8; revisit when a shared workflow pattern emerges with Stories 1.15/1.16.
status: open

### DW-77: `fetch-depth: 1` (shallow checkout) [`.github/workflows/contract-spine.yml:23`]. No current gate uses git history; future drift gates that diff tags must override.

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: .github/workflows/contract-spine.yml:23
reason: `fetch-depth: 1` (shallow checkout) [`.github/workflows/contract-spine.yml:23`]. No current gate uses git history; future drift gates that diff tags must override.
status: open

### DW-78: Case-sensitivity / symlink edge cases in `ValidateRepositoryRelativeApprovalSource`

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs:783-801
reason: Case-sensitivity / symlink edge cases in `ValidateRepositoryRelativeApprovalSource` [`tests/tools/parity-oracle-generator/Program.cs:783-801`]. `OrdinalIgnoreCase` prefix check accepts case-mismatched paths on Linux; not exploitable today (no symlinks in `docs/`).
status: open

### DW-79: Per-file allow-list pattern in negative-scope test

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs:286-298
reason: Per-file allow-list pattern in negative-scope test [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs:286-298`]. Allows only `contract-spine.yml`; Stories 1.15/1.16 will add more entries one at a time. Consider an expected-set assertion when 1.15 lands.
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs:353-366 asserts the complete expected set of five workflows.

### DW-80: `SafeCommentText` insufficient leak guard for diagnostic stream

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs
reason: `SafeCommentText` insufficient leak guard for diagnostic stream [`tests/tools/parity-oracle-generator/Program.cs` SafeCommentText helper] — hypothetical; no concrete leak path identified. Re-evaluate if diagnostic content sources expand beyond bounded code-constructed strings.
status: open

### DW-81: `--initialize-baseline` silently overwrites without `--force`/backup

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs --initialize-baseline branch
reason: `--initialize-baseline` silently overwrites without `--force`/backup [`tests/tools/parity-oracle-generator/Program.cs --initialize-baseline branch`] — UX concern, not correctness. Failing-CI path of least resistance is to nuke the baseline; mitigated socially by sprint review.
status: open
decision: 2026-08-25 Require force — Add --force-baseline-overwrite or an atomic backup requirement, reject an existing target without it, and cover both paths with focused CLI tests.

### DW-82: `AuditKey` regex does not check duplicate-after-normalization

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs ReadAuditMetadataKeys
reason: `AuditKey` regex does not check duplicate-after-normalization [`tests/tools/parity-oracle-generator/Program.cs ReadAuditMetadataKeys`] — the lowercase requirement catches mixed-case duplicates today; reopen if the audit-key vocabulary loosens its case rule.
status: open

### DW-83: Argument parser lacks `--` end-of-options sentinel [`tests/tools/parity-oracle-generator/Program.cs GeneratorOptions.Parse`] — not exercised; current callers never pass values starting with `--`.

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs GeneratorOptions.Parse
reason: Argument parser lacks `--` end-of-options sentinel [`tests/tools/parity-oracle-generator/Program.cs GeneratorOptions.Parse`] — not exercised; current callers never pass values starting with `--`.
status: open

### DW-84: `NormalizeName` leading separator/digit silently produces invalid names

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs NormalizeName
reason: `NormalizeName` leading separator/digit silently produces invalid names [`tests/tools/parity-oracle-generator/Program.cs NormalizeName`] — current OpenAPI does not declare parameters with these shapes.
status: open

### DW-85: `read_consistency_class` enum mixes underscore (`not_applicable`) and hyphen (`eventually-consistent`) forms

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs ReadConsistencyClass
reason: `read_consistency_class` enum mixes underscore (`not_applicable`) and hyphen (`eventually-consistent`) forms [`tests/tools/parity-oracle-generator/Program.cs ReadConsistencyClass`, `tests/fixtures/parity-contract.schema.json`] — intentional schema choice that survived prior reviews; harmonize during a future schema-cleanup sweep.
status: done 2026-08-25
resolution: closed by human decision: Accept not_applicable as the established exceptional sentinel and retain the existing wire vocabulary.
decision: 2026-08-25 Keep vocabulary — Accept not_applicable as the established exceptional sentinel and retain the existing wire vocabulary.

### DW-86: `ReadConsistencyClass` extra-keys / scalar-form produces opaque "value not in enum" rather than `prerequisite_drift:`

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs ReadConsistencyClass
reason: `ReadConsistencyClass` extra-keys / scalar-form produces opaque "value not in enum" rather than `prerequisite_drift:` [`tests/tools/parity-oracle-generator/Program.cs ReadConsistencyClass`] — diagnostic surface improvement only.
status: open

### DW-87: Provenance hash is YAML-comment + normalized-text

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs:2996-2999, 3310-3311, 3379-3381
reason: Provenance hash is YAML-comment + normalized-text — not authenticated file digest (`tests/tools/parity-oracle-generator/Program.cs:2996-2999, 3310-3311, 3379-3381`). Downstream YAML parsers strip the comment; `SHA256(NormalizeLineEndings(text))` doesn't match on-disk file digest with BOM/line-ending differences. Re-evaluate when downstream consumers need verifiable provenance.
status: open
decision: 2026-08-25 Emit both — Retain normalized semantic hashes and add separately named raw-byte SHA-256 fields with focused BOM and line-ending tests.

### DW-88: Generator does not resolve OpenAPI `$ref` for operation request/response schemas

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs:3137-3146
reason: Generator does not resolve OpenAPI `$ref` for operation request/response schemas (`tests/tools/parity-oracle-generator/Program.cs:3137-3146`). Blocks AC 7 schema-drift detection but the simplified column set in this story does not need schema bodies. Reopen if/when schema-drift detection becomes scope.
status: open

### DW-89: `correlation_field_path` always emits `headers.X-Correlation-Id`

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs:3128
reason: `correlation_field_path` always emits `headers.X-Correlation-Id` (`tests/tools/parity-oracle-generator/Program.cs:3128`). Spec invites richer paths (`problem.correlationId`, `result.correlationId`, `metadata.correlationId`) but the canonical `x-hexalith-correlation.correlationHeader` declaration is sufficient today. Add when canonical sources change.
status: open

### DW-90: Test-helper `LoadOperationIds` only recognizes lowercase canonical HTTP verbs

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle (2026-05-17)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:283-290
reason: Test-helper `LoadOperationIds` only recognizes lowercase canonical HTTP verbs (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:283-290`). Divergence from generator's case-insensitivity would underestimate inventory; harmless until contract authors use uppercase verbs.
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:457-459 verifies that generated HTTP verbs are lowercased.

### DW-91: Control-character / BOM / surrogate handling asymmetric between `Escape` (value path) and `RejectControlCharacters` (operation-id + field-path)

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:125-162
reason: Control-character / BOM / surrogate handling asymmetric between `Escape` (value path) and `RejectControlCharacters` (operation-id + field-path) (`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:125-162` vs `171-200`). Team chose to reframe via corpus reclassification; HTTP request-parser boundary owns rejection (Epic 4 server input validation).
status: open

### DW-92: `Half`, `BigInteger`, `Int128`, `UInt128`, `Version`, `nint`, `nuint` fall through to `NormalizeJson`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:37-56
reason: `Half`, `BigInteger`, `Int128`, `UInt128`, `Version`, `nint`, `nuint` fall through to `NormalizeJson` (`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:37-56`). No current spine field has these types; Newtonsoft serialization is version-dependent.
status: open

### DW-93: Cross-type numeric zero encoding asymmetry (`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:230-260`)

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:230-260
reason: Cross-type numeric zero encoding asymmetry (`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:230-260`). Same logical zero produces different canonical bytes via `double`, `float`, `decimal`, integer. Spec does not require cross-type equivalence.
status: open

### DW-94: Static constructor failure mode is opaque (`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:303-317`). Trade-off: fail-fast at type init vs. fail-on-first-call.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:303-317
reason: Static constructor failure mode is opaque (`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:303-317`). Trade-off: fail-fast at type init vs. fail-on-first-call.
status: open
decision: 2026-08-25 Validate on call — Move drift validation into the file-mutation helper entry point and throw a direct, actionable InvalidOperationException.

### DW-95: `YamlNodeExtensions` mixed-visibility surface — public loader, internal extensions (`src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:158-167`).

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:158-167
reason: `YamlNodeExtensions` mixed-visibility surface — public loader, internal extensions (`src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:158-167`).
status: open

### DW-96: Test `GetRawText` doesn't distinguish JSON value types in corpus comparison helper (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs`). Pre-existing Round 3 deferral.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs
reason: Test `GetRawText` doesn't distinguish JSON value types in corpus comparison helper (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs`). Pre-existing Round 3 deferral.
status: open

### DW-97: Empty-parameter-name corner case slips through `EnsureParameter`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:148-154
reason: Empty-parameter-name corner case slips through `EnsureParameter` (`src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:148-154`). Fail-closed-at-compile is acceptable for impossible-from-current-spine input.
status: done 2026-08-24
resolution: already resolved: Commit b117e56; src/Hexalith.Folders.Client/Generation/Program.cs:198-213 rejects empty or mismatched normalized parameter names.

### DW-98: `NormalizeName` acronym handling produces non-obvious fail-closed diagnostics

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:98-128
reason: `NormalizeName` acronym handling produces non-obvious fail-closed diagnostics (`src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:98-128`). No current spine uses acronym-suffix parameters.
status: open

### DW-99: Subnormal `double`s from FMA-fused arithmetic may diverge across hardware classes

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:230-240
reason: Subnormal `double`s from FMA-fused arithmetic may diverge across hardware classes (`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:230-240`). No current arithmetic-derived idempotency field.
status: open

### DW-100: `Process.WaitForExit(10_000)` after `Kill(entireProcessTree)` return value ignored; Windows handle-release race on `Directory.Delete`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:241-256
reason: `Process.WaitForExit(10_000)` after `Kill(entireProcessTree)` return value ignored; Windows handle-release race on `Directory.Delete` (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:241-256`). Resolves together with the Round-3 deferred tempdir-cleanup race.
status: done 2026-08-24
resolution: already resolved: Commit b117e56; tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:308-331 terminates and drains the spawned process.

### DW-101: `LocateRepositoryRoot` fallback if `AppContext.BaseDirectory` is also empty (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:475-485`). Single-file-publish edge case.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:475-485
reason: `LocateRepositoryRoot` fallback if `AppContext.BaseDirectory` is also empty (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:475-485`). Single-file-publish edge case.
status: open

### DW-102: `EnumerableExtensions.WhereNotNull<T>` constrained to reference types only (`tests/Hexalith.Folders.Client.Tests/EnumerableExtensions.cs:5`).

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/EnumerableExtensions.cs:5
reason: `EnumerableExtensions.WhereNotNull<T>` constrained to reference types only (`tests/Hexalith.Folders.Client.Tests/EnumerableExtensions.cs:5`).
status: open

### DW-103: `ScaffoldContractTests` hardcoded expected-reference list (`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:114`). Pre-existing pattern.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:114
reason: `ScaffoldContractTests` hardcoded expected-reference list (`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:114`). Pre-existing pattern.
status: open

### DW-104: Generator csproj relies on MSBuild item-ordering to exclude `Shared/**/*.cs` (`src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:5-7`).

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:5-7
reason: Generator csproj relies on MSBuild item-ordering to exclude `Shared/**/*.cs` (`src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:5-7`).
status: done 2026-09-01
resolution: already resolved: Commit b117e56 and src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:7-16 explicitly remove Shared/**/*.cs from compilation and consume the shared code through a ProjectReference.

### DW-105: `ParsedProblemDetailsCache` is a nested `private sealed class` visible to Newtonsoft reflection (`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:104-118`).

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:104-118
reason: `ParsedProblemDetailsCache` is a nested `private sealed class` visible to Newtonsoft reflection (`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:104-118`).
status: open

### DW-106: `--repository-root` argument not validated for absolute-path-ness inside the generator (`src/Hexalith.Folders.Client/Generation/Program.cs:9-13`).

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:9-13
reason: `--repository-root` argument not validated for absolute-path-ness inside the generator (`src/Hexalith.Folders.Client/Generation/Program.cs:9-13`).
status: open

### DW-107: Generator test `HelperGenerationTargetRegeneratesWhenContractSpineChanges` writes mutated spine via `Encoding.UTF8` with BOM preamble

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:214-218
reason: Generator test `HelperGenerationTargetRegeneratesWhenContractSpineChanges` writes mutated spine via `Encoding.UTF8` with BOM preamble (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:214-218`).
status: done 2026-08-28
resolution: already resolved: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:295-296 uses Encoding.UTF8 under the .NET 10 SDK pinned by global.json; modern Encoding.UTF8 is BOM-less, so the alleged BOM preamble is not emitted.

### DW-108: Test corpus classification polarity is forward-fragile (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:315-322`). Flips when AC 6 normalization implemented.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:315-322
reason: Test corpus classification polarity is forward-fragile (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:315-322`). Flips when AC 6 normalization implemented.
status: open

### DW-109: `FileMutationRequestFileOperationKind` enum drift detection only checks ordinal collisions, not `EnumMember` wire-value drift

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:303-317
reason: `FileMutationRequestFileOperationKind` enum drift detection only checks ordinal collisions, not `EnumMember` wire-value drift (`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:303-317`).
status: open

### DW-110: `IdempotencyField.ToCanonicalLine()` bypasses `RejectControlCharacters`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:308-312
reason: `IdempotencyField.ToCanonicalLine()` bypasses `RejectControlCharacters` (`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:308-312`). Direct callers (e.g. Story 1.13 oracle) skip validation.
status: open

### DW-111: Generation subproject offline-build reliance (`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:46`)

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:46
reason: Generation subproject offline-build reliance (`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:46`) — `dotnet run --project Generation\Hexalith.Folders.Client.Generation.csproj` requires successful restore on every host build; fresh checkout without NuGet cache fails. Belongs to Story 1.14 (CI gates).
status: done 2026-08-24
resolution: already resolved: Commit 1efdb19; .github/workflows/contract-spine.yml:31-39 restores before running build and contract gates.

### DW-112: Typed `ProblemDetails` companion folder placement

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:73-93
reason: Typed `ProblemDetails` companion folder placement (`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:73-93`) — conceptually neither idempotency nor NSwag-generated; could move under `Idempotency/` or split into its own generated file. Non-blocking ownership concern.
status: open

### DW-113: Defensive validation for empty parameter `$ref` / empty `name` scalar (`src/Hexalith.Folders.Client/Generation/Program.cs:201-211`) — spine currently has neither; defensive only.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:201-211
reason: Defensive validation for empty parameter `$ref` / empty `name` scalar (`src/Hexalith.Folders.Client/Generation/Program.cs:201-211`) — spine currently has neither; defensive only.
status: open

### DW-114: Defensive validation for zero-document YAML (`src/Hexalith.Folders.Client/Generation/Program.cs:398-400`) — spine always has at least one document; `yaml.Documents[0]` is safe in practice.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:398-400
reason: Defensive validation for zero-document YAML (`src/Hexalith.Folders.Client/Generation/Program.cs:398-400`) — spine always has at least one document; `yaml.Documents[0]` is safe in practice.
status: done 2026-08-24
resolution: already resolved: Commit b117e56; src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:14-25 rejects YAML streams containing zero documents.

### DW-115: Defensive validation for bare-filename `outputPath`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:27
reason: Defensive validation for bare-filename `outputPath` (`src/Hexalith.Folders.Client/Generation/Program.cs:27`) — MSBuild target always provides absolute path; only triggers under direct CLI invocation with a bare filename.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Client/Generation/Program.cs:38 safely falls back to '.' when Path.GetDirectoryName(outputPath) is null.

### DW-116: Multi-level nested-path NRE risk (`src/Hexalith.Folders.Client/Generation/Program.cs:130`) — `Root?.A.B.C` is null-safe only on the first hop; revisit if spine adds 3+ level equivalence paths.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:130
reason: Multi-level nested-path NRE risk (`src/Hexalith.Folders.Client/Generation/Program.cs:130`) — `Root?.A.B.C` is null-safe only on the first hop; revisit if spine adds 3+ level equivalence paths.
status: open

### DW-117: W1: P-Schema-9 normalization incomplete across earlier-story digest/correlation patterns

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups follow-up (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: RepositoryBinding, CommitEvidence
reason: W1: P-Schema-9 normalization incomplete across earlier-story digest/correlation patterns. The new shared `PrefixedOpaqueIdentifier` schema (yaml:6847) claims coverage of `digest_`, `changeref_`, and `provref_` prefixes, but `RepositoryBinding.changedPathMetadataDigest` (yaml:8384), `CommitEvidence.digest` (yaml:8562), `CommitEvidence.providerCorrelationReference` (yaml:8567, 8600) still hand-roll their own patterns. Either re-point those sites to the shared schema or narrow the schema's advertised coverage. Cross-story sweep best owned alongside the still-deferred P-Sweep-1 closure. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: RepositoryBinding, CommitEvidence`)
status: open

### DW-118: W2: Cross-redaction invariant between record-level `redaction.visibility: redacted` and per-field `evidenceTimestamp.precision: redacted` (and similar paired fields on `actorReference`,…

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups follow-up (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditTrailEntryRedacted example
reason: W2: Cross-redaction invariant between record-level `redaction.visibility: redacted` and per-field `evidenceTimestamp.precision: redacted` (and similar paired fields on `actorReference`, `operationId`, future audience-conditional fields) is unenforced. A server could legitimately emit `record-redacted` with `evidenceTimestamp.precision: exact` and a real timestamp. Fix needs the same JSON-Schema-2020-12 `if/then` conditional design pattern P-Schema-8 used for trust/freshness; best bundled with the operator-audience hardening story that closes D4 (`AuditRecord` correlation-ID exposure). (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditTrailEntryRedacted example`)
status: done 2026-08-28
resolution: already resolved: Commit 5d13a83a; src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:9470-9490 forbids exact values under redacted timestamp states, with regression coverage at tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:547-587.

### DW-119: W3: `PrincipalMismatchSafeDenialProblem` example uses HTTP 404 + `category: not_found`

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups follow-up (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: PrincipalMismatchSafeDenialProblem
reason: W3: `PrincipalMismatchSafeDenialProblem` example uses HTTP 404 + `category: not_found`. Other tenant/principal-related safe-denial paths in the corpus map to `tenant_access_denied`. Speculative drift without a fuller cross-corpus check. Revisit alongside the audience-equivalence rework that defines canonical category mappings for principal-mismatch scenarios. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: PrincipalMismatchSafeDenialProblem`)
status: done 2026-08-25
resolution: closed by human decision: Accept not_found as the intentional existence-hiding principal-mismatch response.
decision: 2026-08-25 Keep safe 404 — Accept not_found as the intentional existence-hiding principal-mismatch response.

### DW-120: Historical Story 1.11 resolution summary

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: n/a
reason: Resolved 2026-05-15 by Story 1.11 continuation: P-Schema-1 through P-Schema-9, P-Test-1, P-Test-3, P-Sweep-1, and D5 are closed in the OpenAPI contract, contract notes, and focused contract tests. Historical entries remain below for traceability.
status: done 2026-08-24
resolution: already resolved: Commit 8d339b8; tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:548-650 covers the audit and operations-console contract group.

### DW-121: P-Schema-1: `DiagnosticBase` + `allOf` + `additionalProperties: false` JSON Schema gotcha

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, LockDiagnostics, DirtyStateDiagnostics, FailedOperationDiagnostics, ProviderStatusDiagnostics, SyncStatusDiagnostics, ProjectionFreshnessDiagnostics
reason: RESOLVED 2026-05-15: P-Schema-1: `DiagnosticBase` + `allOf` + `additionalProperties: false` JSON Schema gotcha. Strict JSON Schema 2020-12 validators reject subclass-added properties because each `allOf` member evaluates `additionalProperties` independently. Fix: replace base `additionalProperties: false` with `unevaluatedProperties: false` on the composed `allOf` schemas. Needs coordination across `DiagnosticBase` + 6 subclasses (`LockDiagnostics`, `DirtyStateDiagnostics`, `FailedOperationDiagnostics`, `ProviderStatusDiagnostics`, `SyncStatusDiagnostics`, `ProjectionFreshnessDiagnostics`) + 7 example payloads + the foundation tests that may assert specific additionalProperties behavior. Best owned by Story 1.12 (NSwag SDK generation) where strict-mode validation lands. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, LockDiagnostics, DirtyStateDiagnostics, FailedOperationDiagnostics, ProviderStatusDiagnostics, SyncStatusDiagnostics, ProjectionFreshnessDiagnostics`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-122: P-Schema-2: `AuditRecord` redaction shape leaks timing

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditRecordRedacted example
reason: RESOLVED 2026-05-15: P-Schema-2: `AuditRecord` redaction shape leaks timing (`evidenceTimestamp`) and actor identity (`actorReference`) on redacted records. Wrapping the leaky fields in audience-gated `RedactionMetadata` requires design choices (bucketed timestamps vs. sentinel replacement, optional vs. null) plus example regeneration and audit-leakage-corpus alignment. Best owned alongside the operator-audience hardening story. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditRecordRedacted example`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-123: P-Schema-3: `DiagnosticBase.audience` is a required body field that lets callers A/B-test their credentials and observe a `consumer` → `authorized_operator` flip

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads, AuditOpsConsoleContractGroupTests
reason: P-Schema-3: `DiagnosticBase.audience` is a required body field that lets callers A/B-test their credentials and observe a `consumer` → `authorized_operator` flip. Decision D9 chose "remove audience from body". Deferred because the change touches the base schema, every diagnostic subclass example, and the audience-checking tests. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads, AuditOpsConsoleContractGroupTests`)
status: done 2026-08-25
resolution: closed by human decision: Supersede historical D9 and accept the current explicit audience discriminator as the implemented v1 contract.
decision: 2026-08-25 Retain audience — Supersede historical D9 and accept the current explicit audience discriminator as the implemented v1 contract.

### DW-124: P-Schema-4: `DiagnosticBase.fieldClassifications` is optional with no `minItems`, allowing operator responses to omit the array entirely

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads
reason: RESOLVED 2026-05-15: P-Schema-4: `DiagnosticBase.fieldClassifications` is optional with no `minItems`, allowing operator responses to omit the array entirely. Requires audience-conditional schema or a split into consumer/operator variants plus example regeneration. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-125: P-Schema-5: `ReadinessDiagnostics` schema lacks provider/folder/workspace summary references called out by AC 5 and the operation-inventory row

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ReadinessDiagnostics, ReadinessDiagnostics example
reason: RESOLVED 2026-05-15: P-Schema-5: `ReadinessDiagnostics` schema lacks provider/folder/workspace summary references called out by AC 5 and the operation-inventory row. Add operator-audience-gated summary fields (`DiagnosticSafeIdentifier` + `RedactionMetadata`). (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ReadinessDiagnostics, ReadinessDiagnostics example`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-126: P-Schema-6: `LockDiagnostics.lockReference` and `ProviderStatusDiagnostics.providerBindingReference` field-presence is an audience oracle (present for operator, absent for consumer)

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: LockDiagnostics, ProviderStatusDiagnostics
reason: RESOLVED 2026-05-15: P-Schema-6: `LockDiagnostics.lockReference` and `ProviderStatusDiagnostics.providerBindingReference` field-presence is an audience oracle (present for operator, absent for consumer). Coordinated with audience-conditional schema work above. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: LockDiagnostics, ProviderStatusDiagnostics`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-127: P-Schema-7: `OperationTimelineEntry.workspaceId` leaks cross-workspace evidence to callers authorized for the folder but not the specific workspace

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperationTimelineEntry
reason: RESOLVED 2026-05-15: P-Schema-7: `OperationTimelineEntry.workspaceId` leaks cross-workspace evidence to callers authorized for the folder but not the specific workspace. Gate via audience-aware redaction. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperationTimelineEntry`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-128: P-Schema-8: No cross-field consistency invariant between `DiagnosticTrustEvidence.availability` and `FreshnessMetadata.stale`

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticTrustEvidence, FreshnessMetadata
reason: RESOLVED 2026-05-15: P-Schema-8: No cross-field consistency invariant between `DiagnosticTrustEvidence.availability` and `FreshnessMetadata.stale`. Schema accepts contradictory pairs. Fix needs JSON-Schema-2020-12 `if/then` conditionals or a refactor into a single state machine. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticTrustEvidence, FreshnessMetadata`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-129: P-Schema-9: Opaque-identifier patterns are inconsistent across siblings

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OpaqueIdentifier, ContentHashReference, ChangedPathEvidence, AuditRecord, ProviderStatusDiagnostics
reason: RESOLVED 2026-05-15: P-Schema-9: Opaque-identifier patterns are inconsistent across siblings (`actorref_` min 7, `digest_` min 9, `changeref_` min 6, `provref_` min 8). Factor a shared `PrefixedOpaqueIdentifier` schema and normalize. Cross-cutting; touches Stories 1.7-1.11 patterns. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OpaqueIdentifier, ContentHashReference, ChangedPathEvidence, AuditRecord, ProviderStatusDiagnostics`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-130: P-Vocab-1: `x-hexalith-group` extension to mechanically separate the four canonical group names

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml
reason: P-Vocab-1: `x-hexalith-group` extension to mechanically separate the four canonical group names (`AuditQueries`, `OperationTimelineQueries`, `OpsConsoleDiagnostics`, `ProjectionFreshness`) was deferred because the extension key would need registration in `extensions/hexalith-extension-vocabulary.yaml` and updates to `ContractSpineFoundation_DeclaresRequiredVocabularyOnly`. Best owned by the next vocabulary-extension story. (`src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineFoundationTests.cs`, all 11 Story 1.11 operations)
status: open

### DW-131: P-Vocab-2: `x-hexalith-reference-pending` extension for marking individual enum values or properties as reference-pending was attempted and rolled back because it is not in the approved vocabulary

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml
reason: P-Vocab-2: `x-hexalith-reference-pending` extension for marking individual enum values or properties as reference-pending was attempted and rolled back because it is not in the approved vocabulary. Reference-pending state is currently carried in `description:` strings; promoting to a dedicated extension would let validators enforce reference-pending discipline. Bundle with the vocabulary-extension follow-up. (`src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`)
status: open

### DW-132: P-Test-1: Cursor/filter tamper, principal-mismatch, invalid-sort, boundary-duplicate, empty-page negative-case tests absent (AC 19 / AC 22 / Tasks line 100)

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs
reason: RESOLVED 2026-05-15: P-Test-1: Cursor/filter tamper, principal-mismatch, invalid-sort, boundary-duplicate, empty-page negative-case tests absent (AC 19 / AC 22 / Tasks line 100). Adding explicit negative tests requires representative cursor/principal fixtures or a runtime test harness; best owned by Story 1.13/1.14 contract-test-harness work. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-133: P-Test-2: `audit_access_denied` requirement is enforced only on `AuditOperationIds`, not on ops-console diagnostic operations that also declare it in canonical-error-categories

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:AuditOpsConsoleQueries_OmitIdempotencyAndDeclareReadConsistencySafeDenial
reason: P-Test-2: `audit_access_denied` requirement is enforced only on `AuditOperationIds`, not on ops-console diagnostic operations that also declare it in canonical-error-categories. Per-op canonical-category audit needed before broadening. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:AuditOpsConsoleQueries_OmitIdempotencyAndDeclareReadConsistencySafeDenial`)
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:117-130 verifies the current operations-console category arrays, which intentionally omit audit_access_denied.

### DW-134: P-Test-3: Examples for `AuditTrailPage` / `OperationTimelinePage` cover only the single-result case

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: components.examples
reason: RESOLVED 2026-05-15: P-Test-3: Examples for `AuditTrailPage` / `OperationTimelinePage` cover only the single-result case. Add named synthetic examples for zero results, exactly-limit boundary, beyond-last cursor, empty-page continuation, multi-tenant denial parity, and operator-disposition spectrum (Tasks line 86). Several hundred lines of example YAML; defer alongside the audience-equivalence rework. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: components.examples`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-135: P-Sweep-1: `CanonicalErrorCategory` / `WorkspaceErrorCategory` enum widening

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: all Stories 1.7-1.10 operations
reason: RESOLVED 2026-05-15: P-Sweep-1: `CanonicalErrorCategory` / `WorkspaceErrorCategory` enum widening (`projection_stale`, `projection_unavailable`, `failed_operation`) not propagated to Stories 1.7-1.10 operations' `x-hexalith-canonical-error-categories` arrays. Cross-story sweep; best owned by Story 1.12 or 1.14. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: all Stories 1.7-1.10 operations`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-136: D1: Predev preflight result is `fail` but story advanced to `review`

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: _bmad-output/process-notes/predev-preflight-latest.json
reason: D1: Predev preflight result is `fail` but story advanced to `review`. `_bmad-output/process-notes/predev-preflight-latest.json` records `"result": "fail"` with seven dirty paths, and a new `predev-preflight-2026-05-14T100203Z.json` captures the same failure. Process/governance concern outside contract correctness; recommend a separate ticket to investigate the dirty paths. (`_bmad-output/process-notes/predev-preflight-latest.json`, `_bmad-output/process-notes/predev-preflight-2026-05-14T100203Z.json`)
status: open

### DW-137: D2: `tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs` was edited outside the spec's `Allowed Files And Forbidden Work` list (the helper sits under…

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs
reason: D2: `tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs` was edited outside the spec's `Allowed Files And Forbidden Work` list (the helper sits under `tests/Hexalith.Folders.Testing.Tests/`, not `tests/Hexalith.Folders.Contracts.Tests/` or `tests/tools/`). The edit is logically required for Story 1.11 to own the ops-console path family, so record as an explicit scope expansion in the story's Change Log rather than reverting. (`tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs`)
status: done 2026-08-24
resolution: already resolved: _bmad-output/implementation-artifacts/1-11-author-audit-and-ops-console-query-contract-groups.md:394 records and accepts the SpineContractAssertions scope expansion.

### DW-138: D3: Synthetic example timestamps use today's wall-clock date

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples
reason: D3: Synthetic example timestamps use today's wall-clock date (`2026-05-14T08:30:00Z` and seventeen similar values). Spec disallows real timestamps; bucketed (e.g. `0001-01-01T00:00:00Z`) or far-future placeholders would be cleaner. Cosmetic. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples`)
status: open

### DW-139: D4: No dedicated `x-hexalith-redaction` extension on new operations

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: extensions
reason: D4: No dedicated `x-hexalith-redaction` extension on new operations. AC 2 enumerates redaction behavior as a required per-operation declaration; Stories 1.6-1.10 operations also lack the dedicated extension, so this is a pre-existing convention gap. Standardize in a Story 1.6 foundation refactor. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: extensions`)
status: open

### DW-140: D5: `OperatorDispositionLabel` enum includes `auto_recovering` and `available`, but no synthetic example exercises either value

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples
reason: RESOLVED 2026-05-15: D5: `OperatorDispositionLabel` enum includes `auto_recovering` and `available`, but no synthetic example exercises either value. Cosmetic test gap; bundle into the boundary-scenario examples patch. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`)
status: done 2026-05-15
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-141: W1: Negative-test cases for duplicate `operationId`, missing required `x-hexalith-*` metadata, mutating-without-idempotency, and read-without-read-consistency are not exercised

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:1694
reason: W1: Negative-test cases for duplicate `operationId`, missing required `x-hexalith-*` metadata, mutating-without-idempotency, and read-without-read-consistency are not exercised. AC12 minimum-matrix gap; better owned by Story 1.14 (Contract Spine CI gates). (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:1694`)
status: done 2026-08-25
resolution: already resolved: tests/tools/parity-oracle-generator/Program.cs:162,208; tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:167; and GovernanceCompletenessGateTests.cs:538 now reject duplicate operation IDs and missing mutation/read metadata with negative controls.

### DW-142: W2: No assertion that `hexalith.folders.v1.yaml` parses as a valid OpenAPI 3.1 document

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:LoadYamlMapping
reason: W2: No assertion that `hexalith.folders.v1.yaml` parses as a valid OpenAPI 3.1 document — the test currently only loads the file via `YamlStream`. Pre-existing across all contract-group tests; better owned by Story 1.6 foundation tests. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:LoadYamlMapping`)
status: done 2026-08-25
resolution: already resolved: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:17,34 and .github/workflows/contract-spine.yml:31 restore NSwag and parse, generate, and build the OpenAPI document in the required CI path.

### DW-143: W3: `CommitWorkspace` does not declare a 429 response even though `provider_rate_limited` is in the canonical-error-category set

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: CommitWorkspace responses
reason: W3: `CommitWorkspace` does not declare a 429 response even though `provider_rate_limited` is in the canonical-error-category set. Cross-cutting consistency across all mutating operations; not unique to this story. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: CommitWorkspace responses`)
status: open
decision: 2026-08-25 Add explicit 429 — Add a shared ProviderRateLimited response to CommitWorkspace and regenerate or update SDK parity, adapter mappings, and tests.

### DW-144: W4: `OpaqueIdentifier` does not reject the new namespace prefixes `branchref_`, `digest_`, `provref_`, `authorref_`

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OpaqueIdentifier
reason: W4: `OpaqueIdentifier` does not reject the new namespace prefixes `branchref_`, `digest_`, `provref_`, `authorref_`. Cross-namespace collision is theoretical; global hardening better handled with the wider opaque-identifier vocabulary review. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OpaqueIdentifier`)
status: open
decision: 2026-08-28 Reserve typed prefixes — Exclude typed prefixes from OpaqueIdentifier, migrate legitimate sites to namespace schemas, regenerate clients, and add compatibility tests.
decision: 2026-08-28 Reserve typed prefixes — Exclude typed prefixes from OpaqueIdentifier, migrate legitimate sites to namespace schemas, regenerate clients, and add compatibility tests.

### DW-145: W5: Wire `OperatorDispositionLabel` (defined at yaml:5329) into the relevant status schemas as part of Story 6.3

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperatorDispositionLabel
reason: W5: Wire `OperatorDispositionLabel` (defined at yaml:5329) into the relevant status schemas as part of Story 6.3 — operations console rendering. AC6 names disposition labels but Story 1.10 has no consumer of them yet; deferring keeps the contract surface free of unused fields until 6.3 defines the actual rendering requirements. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperatorDispositionLabel`)
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:9634-9635 and :9773-9786 defines the disposition values; src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs:21-60 maps them for the UI.

### DW-146: W1: No `412 Precondition Failed` response for `ChangeFile` concurrency control

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-author-file-mutation-and-context-query-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ChangeFile
reason: W1: No `412 Precondition Failed` response for `ChangeFile` concurrency control — concurrency model and optimistic-concurrency headers (`If-Match`/`If-None-Match`) belong to Epic 4 runtime; Story 1.9 contract group declares only idempotency-conflict (409), not stale-content versioning. Revisit when Epic 4 implements ChangeFile semantics on prepared workspaces. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ChangeFile`)
status: done 2026-08-25
resolution: closed by human decision: Locking plus idempotency remain canonical; no independent content-version precondition is supported.
decision: 2026-08-25 Keep lock model — Locking plus idempotency remain canonical; no independent content-version precondition is supported.

### DW-147: P5: `FileSearchRequest.queryText` and `FileGlobRequest.globPattern` carry audit-exclusion semantics in prose only; schema-level `x-hexalith-audit-visibility` (or…

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-author-file-mutation-and-context-query-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
reason: P5: `FileSearchRequest.queryText` and `FileGlobRequest.globPattern` carry audit-exclusion semantics in prose only; schema-level `x-hexalith-audit-visibility` (or `x-hexalith-sensitive-metadata-tier`) tagging blocked on Story 1.6 vocabulary registration. Bundle with P9 vocabulary extension follow-up. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`)
status: open

### DW-148: P9: Rename non-standard JSON-Schema keywords `maxBytes` and `maxResultCount` to vocabulary-aligned `x-hexalith-max-bytes` / `x-hexalith-max-result-count`

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-author-file-mutation-and-context-query-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml
reason: P9: Rename non-standard JSON-Schema keywords `maxBytes` and `maxResultCount` to vocabulary-aligned `x-hexalith-max-bytes` / `x-hexalith-max-result-count`. Requires registering both keys in `hexalith-extension-vocabulary.yaml` (allowedLocations / valueSchema / foundationSchema / example) and updating the Story 1.6 `RequiredExtensions` allow-list test. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `src/Hexalith.Folders.Contracts/openapi/extensions/hexalith-extension-vocabulary.yaml`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineFoundationTests.cs`)
status: open

### DW-149: P13: `FileContextContractGroupTests.ResolveRefs` only verifies that `$ref` targets exist; examples are not validated against their target schemas

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-author-file-mutation-and-context-query-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs:ResolveRefs
reason: P13: `FileContextContractGroupTests.ResolveRefs` only verifies that `$ref` targets exist; examples are not validated against their target schemas. Adding examples-must-be-valid coverage requires a JSON-Schema validator (e.g., `JsonSchema.Net` or `NJsonSchema`) in the contract test project. Belongs to a wider contract-test-harness story alongside Story 1.13/1.14 gates. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/FileContextContractGroupTests.cs:ResolveRefs`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs`)
status: open

### DW-150: P18: `FileTreeResult` example is reused as the response example for `ListFolderFiles`, `SearchFolderFiles`, and `GlobFolderFiles` even though the example hardcodes `limits.queryFamily: tree`

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-author-file-mutation-and-context-query-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples FileTreeResult, FileTreeResultTruncated
reason: P18: `FileTreeResult` example is reused as the response example for `ListFolderFiles`, `SearchFolderFiles`, and `GlobFolderFiles` even though the example hardcodes `limits.queryFamily: tree`. Per-operation variants would require splitting `FileTreeResult` into operation-specific result schemas, which conflicts with the current shared schema design. Revisit when search/glob diverge from tree semantics. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples FileTreeResult, FileTreeResultTruncated`)
status: open

### DW-151: P27: Mutating operations inline-define `name: X-Hexalith-Task-Id, required: true` rather than `$ref`-ing the shared `TaskId` parameter, because the shared `TaskId` is `required: false`

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-author-file-mutation-and-context-query-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: mutating operations
reason: P27: Mutating operations inline-define `name: X-Hexalith-Task-Id, required: true` rather than `$ref`-ing the shared `TaskId` parameter, because the shared `TaskId` is `required: false`. Switching requires either (a) flipping shared `TaskId` to required (regressing other operations that treat task id as optional) or (b) introducing a new `RequiredTaskId` shared parameter. Bundle with Story 1.6 vocabulary extension follow-up. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: mutating operations`, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: parameters.TaskId`)
status: open

### DW-152: `allOf:[$ref]+description` on `LockLeaseMetadata.holderRef` and `ReleaseWorkspaceLockRequest.lockOwnershipProof` works under OpenAPI 3.1 but is the 3.0 workaround style

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: LockLeaseMetadata, ReleaseWorkspaceLockRequest
reason: `allOf:[$ref]+description` on `LockLeaseMetadata.holderRef` and `ReleaseWorkspaceLockRequest.lockOwnershipProof` works under OpenAPI 3.1 but is the 3.0 workaround style — inconsistent with sibling refs that use the direct sibling description. Style nit; revisit during the Story 1.6 vocabulary consolidation. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: LockLeaseMetadata, ReleaseWorkspaceLockRequest`)
status: open

### DW-153: `ResolveRefs` in the contract validator only verifies that JSON-pointer targets exist; it does not check that a `$ref` under `schema:` actually points to a schema component (or that a `$ref` under…

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:300-352
reason: `ResolveRefs` in the contract validator only verifies that JSON-pointer targets exist; it does not check that a `$ref` under `schema:` actually points to a schema component (or that a `$ref` under `parameters:` points to a parameter). Easy to enhance once a wider validator pass is undertaken. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:300-352`)
status: open

### DW-154: `EnumerateNamedFields` does not descend into inline example map keys when checking for forbidden token-shaped names

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:250-288
reason: `EnumerateNamedFields` does not descend into inline example map keys when checking for forbidden token-shaped names — only walks `properties:` keys and `name:` scalars. A property name accidentally introduced inline in an example would slip past. Revisit when example-introspection coverage is broadened. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:250-288`)
status: open

### DW-155: `EnumerateNamedFields` yields the same property names twice via the recursion path

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:252-288
reason: `EnumerateNamedFields` yields the same property names twice via the recursion path. Harmless but quadratic-ish on deep trees; revisit if test runtime grows. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:252-288`)
status: done 2026-08-28
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:366-404 yields each properties key only in its dedicated loop and recursively visits child values, eliminating the former duplicate-yield path.

### DW-156: `GetOptionalScalar` uses `ShouldBeOfType<YamlScalarNode>()` and throws an opaque Shouldly error on a malformed mapping/sequence value

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:386-391
reason: `GetOptionalScalar` uses `ShouldBeOfType<YamlScalarNode>()` and throws an opaque Shouldly error on a malformed mapping/sequence value. Defer until the validator is rewritten with structured diagnostics. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:386-391`)
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:502-506 now tests value is YamlScalarNode and returns null for non-scalars instead of throwing a Shouldly type assertion.

### DW-157: Synthetic IDs use `opaque_01HZY...` ULID-shaped values; these are correctly synthetic but visually indistinguishable from production ULIDs in logs/issue trackers

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: n/a
reason: Synthetic IDs use `opaque_01HZY...` ULID-shaped values; these are correctly synthetic but visually indistinguishable from production ULIDs in logs/issue trackers. Convention `opaque_example_workspace_001` would be clearer. Cosmetic — revisit during fixture/vocabulary sweep.
status: open

### DW-158: `WorkspaceTransitionEvidence.auditMetadata.additionalProperties: oneOf string|boolean` permits unbounded keys; a non-conformant server could flood audit metadata

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: WorkspaceTransitionEvidence
reason: `WorkspaceTransitionEvidence.auditMetadata.additionalProperties: oneOf string|boolean` permits unbounded keys; a non-conformant server could flood audit metadata. Schema-robustness enhancement; not Story 1.8 scope. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: WorkspaceTransitionEvidence`)
status: open
decision: 2026-08-28 Bound audit metadata — Add an approved maxProperties bound, update examples and tests, regenerate clients, and enforce the limit in server responses.
decision: 2026-08-28 Bound audit metadata — Add an approved maxProperties bound, update examples and tests, regenerate clients, and enforce the limit in server responses.

### DW-159: Read-consistency token form drift: story prose uses hyphenated `snapshot-per-task` / `read-your-writes` / `eventually-consistent`, OpenAPI `ReadConsistencyClass` enum uses underscore form

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: ReadConsistencyClass
reason: Read-consistency token form drift: story prose uses hyphenated `snapshot-per-task` / `read-your-writes` / `eventually-consistent`, OpenAPI `ReadConsistencyClass` enum uses underscore form. Enum is canonical; revisit prose during vocabulary documentation.
status: open

### DW-160: Contract test uses `FindRepositoryRoot()` keyed on `Hexalith.Folders.slnx` filename

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:402-418
reason: Contract test uses `FindRepositoryRoot()` keyed on `Hexalith.Folders.slnx` filename. Brittle if solution renamed or test run outside the working copy. Revisit when contract tests adopt embedded-resource pattern. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:402-418`)
status: open

### DW-161: `_bmad-output/process-notes/predev-preflight-2026-05-12T190331Z.json` ships with `result: fail` (11 dirty paths) inside the same diff being reviewed

origin: migrated from legacy ledger ("Deferred from: code review of 1-7-author-tenant-folder-provider-and-repository-binding-contract-groups (2026-05-13)"), 2026-08-24
location: _bmad-output/process-notes/predev-preflight-2026-05-12T190331Z.json
reason: `_bmad-output/process-notes/predev-preflight-2026-05-12T190331Z.json` ships with `result: fail` (11 dirty paths) inside the same diff being reviewed. Process artifact captured during dev; not a contract bug. Deferred as a process anomaly worth noting on the next dev-record housekeeping pass.
status: open

### DW-162: `CanonicalErrorCategory` retains `provider_failure_known` without any operation referencing it

origin: migrated from legacy ledger ("Deferred from: code review of 1-7-author-tenant-folder-provider-and-repository-binding-contract-groups (2026-05-13)"), 2026-08-24
location: CanonicalErrorCategory
reason: `CanonicalErrorCategory` retains `provider_failure_known` without any operation referencing it. Pre-existing enum value from Story 1.5/1.6 foundation; downstream stories may consume it. Deferred — revisit when the next consumer is introduced or when the bounded vocabulary is finalised.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2130 and :6062-6063 reference and exemplify provider_failure_known; it is no longer an unused category.

### DW-163: `PaginationMetadata` `pageCursor` is not bound to `filter` shape

origin: migrated from legacy ledger ("Deferred from: code review of 1-7-author-tenant-folder-provider-and-repository-binding-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:PaginationMetadata, MetadataFilter
reason: `PaginationMetadata` `pageCursor` is not bound to `filter` shape — a cursor issued for one `filter` value can be reused with a different filter, leaking partial result counts across permission-visibility classes (timing oracle on hidden ACL entries). Pagination component is shared from Story 1.6; belongs to a cross-cutting pagination hardening story, not Story 1.7. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:PaginationMetadata, MetadataFilter`)
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Server/AuditEndpoints.cs:334-362 rejects every supplied filter with filter_not_yet_supported, so no filter-bound cursor can currently be issued or replayed under a different filter.

### DW-164: `previous-spine.yaml` not proven syntactically valid by a YAML library

origin: migrated from legacy ledger ("Deferred from: code review of 1-3-seed-minimally-valid-normative-fixtures (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:ParseTopLevelYamlScalarMap
reason: `previous-spine.yaml` not proven syntactically valid by a YAML library — `ParseTopLevelYamlScalarMap` checks top-level key presence only; a malformed YAML block (tab indent, duplicate key) would not be caught. Fix requires confirming a YAML library is centrally available; defer to whichever story first adds one. (`tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:ParseTopLevelYamlScalarMap`)
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:306-316 loads previous-spine.yaml through the YamlDotNet-backed LoadYamlMapping path.

### DW-165: `openapi` guard prefix too narrow — `ShouldNotContainKey("openapi")` catches only the exact key; `openapi_version` or `openapi:` nested under another key would bypass it

origin: migrated from legacy ledger ("Deferred from: code review of 1-3-seed-minimally-valid-normative-fixtures (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:NormativeFixturesAreParseableAndCarryOwnershipMetadata
reason: `openapi` guard prefix too narrow — `ShouldNotContainKey("openapi")` catches only the exact key; `openapi_version` or `openapi:` nested under another key would bypass it. Low risk: `source_marker` and `mutation_rules` already document the intent; revisit if the guard needs hardening. (`tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:NormativeFixturesAreParseableAndCarryOwnershipMetadata`)
status: open

### DW-166: `.editorconfig` `async_methods_should_end_with_async` rule may flag controller actions and Blazor lifecycle overrides at feature-implementation time

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: .editorconfig
reason: `.editorconfig` `async_methods_should_end_with_async` rule may flag controller actions and Blazor lifecycle overrides at feature-implementation time — deferred to the first feature story that trips it. (`.editorconfig:41-49`)
status: open

### DW-167: Private-field naming rule covers `private` accessibility only; `protected`/`internal` field naming silently allowed — deferred until those modifiers actually appear. (`.editorconfig:31-39`)

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: .editorconfig:31-39
reason: Private-field naming rule covers `private` accessibility only; `protected`/`internal` field naming silently allowed — deferred until those modifiers actually appear. (`.editorconfig:31-39`)
status: open

### DW-168: `CA1062`, `CA2007` severities set to `warning` combined with root `TreatWarningsAsErrors=true` could mass-fail builds when real code lands

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: .editorconfig:59-61
reason: `CA1062`, `CA2007` severities set to `warning` combined with root `TreatWarningsAsErrors=true` could mass-fail builds when real code lands. Builds pass today per Story 1.2 Dev Notes; revisit if a feature story trips it. (`.editorconfig:59-61`)
status: open

### DW-169: Submodule policy text is triplicated across `AGENTS.md`, `CLAUDE.md`, `README.md`

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: AGENTS.md
reason: Submodule policy text is triplicated across `AGENTS.md`, `CLAUDE.md`, `README.md`. Drift risk but intentional per spec for discoverability. Revisit when an automated single-source-of-truth pattern (e.g., generated includes) becomes available.
status: done 2026-08-28
decision: 2026-08-28 Close as intentional — Keep discoverable copies protected by byte-identity and policy tests.
resolution: closed by human decision: Keep discoverable copies protected by byte-identity and policy tests.
decision: 2026-08-28 Close as intentional — Keep discoverable copies protected by byte-identity and policy tests.

### DW-170: `nuget.config` uses `<clear/>` then only nuget.org — destructive to corporate-mirror users but matches AC2 "no private feed assumptions". Revisit if a private feed becomes legitimate later.

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: nuget.config
reason: `nuget.config` uses `<clear/>` then only nuget.org — destructive to corporate-mirror users but matches AC2 "no private feed assumptions". Revisit if a private feed becomes legitimate later.
status: done 2026-08-28
decision: 2026-08-28 Keep public-only policy — Retain deterministic nuget.org-only source mapping until a real private-feed requirement is approved.
resolution: closed by human decision: Retain deterministic nuget.org-only source mapping until a real private-feed requirement is approved.
decision: 2026-08-28 Keep public-only policy — Retain deterministic nuget.org-only source mapping until a real private-feed requirement is approved.

### DW-171: `Deterministic=true` paired with `ContinuousIntegrationBuild` gated to `'$(CI)' == 'true'` means local PDBs still carry absolute paths

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: ContinuousIntegrationBuild
reason: `Deterministic=true` paired with `ContinuousIntegrationBuild` gated to `'$(CI)' == 'true'` means local PDBs still carry absolute paths. Matches the gated intent; revisit if local-build determinism becomes a requirement.
status: open

### DW-172: Story 1.2 spec File List (lines 240-247) omits `.gitmodules` from the touched files, even though `.gitmodules` was modified. Record-keeping inconsistency; sweep on next dev-record housekeeping pass.

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: .gitmodules
reason: Story 1.2 spec File List (lines 240-247) omits `.gitmodules` from the touched files, even though `.gitmodules` was modified. Record-keeping inconsistency; sweep on next dev-record housekeeping pass.
status: open

### DW-173: `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` now locks down the entire 24-project dependency graph

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection
reason: `ScaffoldContractTests.ProjectReferencesFollowAllowedDependencyDirection` now locks down the entire 24-project dependency graph — properly Story 1.1's territory and brittle. Acceptable per Story 1.2's "solution/dependency smoke test" allowance; revisit ownership in a Story 1.1 review iteration.
status: open

### DW-174: `ProductionUrl` regex in `ExitCriteriaDecisionArtifactTests` would reject legitimate documentation citations such as `https://learn.microsoft.com/...` if any are added later

origin: migrated from legacy ledger ("Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:222-224
reason: `ProductionUrl` regex in `ExitCriteriaDecisionArtifactTests` would reject legitimate documentation citations such as `https://learn.microsoft.com/...` if any are added later. No current usage; revisit if a citation outside the `.invalid` TLD becomes necessary. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:222-224`)
status: open

### DW-175: Opaque / provider-token detection (PASETO, Macaroon, GitHub PATs as non-JWTs)

origin: migrated from legacy ledger ("Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)"), 2026-08-24
location: RawJwt
reason: Opaque / provider-token detection (PASETO, Macaroon, GitHub PATs as non-JWTs) — `RawJwt` only catches `eyJ`-prefixed JWTs. Tracked as part of the broader hygiene-scan vocabulary owned by story 1-6 follow-ups; not a story 1.4 concern.
status: open

### DW-176: `File.ReadAllText` in the doc-verification test makes no encoding assertion

origin: migrated from legacy ledger ("Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)"), 2026-08-24
location: File.ReadAllText
reason: `File.ReadAllText` in the doc-verification test makes no encoding assertion — BOM / UTF-16 edge cases would silently misread the artifact. Low risk: project standardizes on UTF-8. Revisit if an editor introduces non-UTF-8 content.
status: open

### DW-177: Windows case-insensitive filesystem path normalization in `ExitCriteriaDecisionArtifactTests` could hide an accidental rename to non-canonical casing (e.g., `docs/Exit-Criteria/C3-Retention.md`)

origin: migrated from legacy ledger ("Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)"), 2026-08-24
location: docs/Exit-Criteria/C3-Retention.md
reason: Windows case-insensitive filesystem path normalization in `ExitCriteriaDecisionArtifactTests` could hide an accidental rename to non-canonical casing (e.g., `docs/Exit-Criteria/C3-Retention.md`). Convention is enforced by PR diff review; revisit if a regression appears.
status: open

### DW-178: C6 transition-matrix mapping artifact maps every state to a single Story 4.1 consumer

origin: migrated from legacy ledger ("Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)"), 2026-08-24
location: docs/exit-criteria/c6-transition-matrix-mapping.md
reason: C6 transition-matrix mapping artifact maps every state to a single Story 4.1 consumer. If 4.1 splits, all rows will need re-pointing. Already captured in the artifact's `open questions` section. Defer to story 4.1 entry. (`docs/exit-criteria/c6-transition-matrix-mapping.md`)
status: done 2026-08-24
resolution: already resolved: _bmad-output/implementation-artifacts/sprint-status.yaml:114 records Story 4.1 done, while src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:1 contains the implemented C6 transition spine; the attribution is now accurate.

### DW-179: Error subtypes (`SafeAuthorizationDenial`, `ValidationFailure`, `IdempotencyConflict`, `ReconciliationRequired`) `allOf` `ProblemDetails` with no own discriminating properties…

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:292-307
reason: Error subtypes (`SafeAuthorizationDenial`, `ValidationFailure`, `IdempotencyConflict`, `ReconciliationRequired`) `allOf` `ProblemDetails` with no own discriminating properties (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:292-307`). Downstream stories 1.7-1.11 must specialize each with operation-relevant required fields.
status: open
decision: 2026-08-28 Specialize error schemas — Add subtype category and code constraints, update examples and gates, and regenerate SDK, CLI, and MCP consumers.
decision: 2026-08-28 Specialize error schemas — Add subtype category and code constraints, update examples and gates, and regenerate SDK, CLI, and MCP consumers.

### DW-180: `OperatorDispositionLabel` and `SensitiveMetadataTier` schemas defined but never referenced in this story

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:423-444
reason: `OperatorDispositionLabel` and `SensitiveMetadataTier` schemas defined but never referenced in this story (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:423-444`). Foundation vocabulary; downstream operation groups must `$ref` them when they emit operator-disposition or sensitivity-tagged data.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7469-7471 and :8006 reference SensitiveMetadataTier, while :9635 and :9786 reference OperatorDispositionLabel; neither component is orphaned.

### DW-181: `paths: {}` empty Paths Object may produce warnings under Spectral, openapi-typescript, or NSwag

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: n/a
reason: `paths: {}` empty Paths Object may produce warnings under Spectral, openapi-typescript, or NSwag. Owned by story 1.12 (NSwag SDK generation) and story 1.14 (drift gate); validate when those stories land.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:35-58 contains real paths, and tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineFoundationTests.cs:31-39 asserts the paths map is non-empty.

### DW-182: CLI exit code → CanonicalErrorCategory mapping table is not declared

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: CliExitCode
reason: CLI exit code → CanonicalErrorCategory mapping table is not declared. The 14-value `CliExitCode` enum exists but distinct categories like `response_limit_exceeded`, `query_timeout`, `redacted`, `client_configuration_error` have no exit-code assignment. Owned by story 1.13 (parity oracle).
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Cli.Tests/ErrorProjectionTests.cs:24-85 verifies canonical category-to-exit-code projection, with the public mapping also documented in docs/sdk/cli-reference.md:174.

### DW-183: No test asserts mutating-completeness fails when `idempotency_key_rule` or equivalence fields are missing (AC4's forward-looking statement). Owned by story 1.13/1.14 contract-completeness gate.

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: n/a
reason: No test asserts mutating-completeness fails when `idempotency_key_rule` or equivalence fields are missing (AC4's forward-looking statement). Owned by story 1.13/1.14 contract-completeness gate.
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:167-178 covers missing mutation metadata; tests/tools/parity-oracle-generator/Program.cs:206-230 rejects both missing mutating metadata and metadata on non-mutating operations.

### DW-184: `oidc.local.invalid` may hang on corporate DNS sinkholes that override RFC 2606. Affects only consumers that pre-fetch metadata at codegen time. Environmental edge case outside MVP scope.

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: oidc.local.invalid
reason: `oidc.local.invalid` may hang on corporate DNS sinkholes that override RFC 2606. Affects only consumers that pre-fetch metadata at codegen time. Environmental edge case outside MVP scope.
status: open

### DW-185: `Idempotency-Key` parameter is declared `required: true` globally as a reusable component

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: docs/contract/contract-spine-foundation.md:13
reason: `Idempotency-Key` parameter is declared `required: true` globally as a reusable component. Downstream authors must explicitly not `$ref` it on query operations. Foundation note in `docs/contract/contract-spine-foundation.md:13` already states this; deferred to per-operation author discipline + future contract-completeness gate.
status: done 2026-08-24
resolution: already resolved: tests/tools/parity-oracle-generator/Program.cs:206-230 enforces that mutating operations declare idempotency metadata and non-mutating operations do not, mechanizing the query discipline.

### DW-186: Simultaneous-cancellation TOCTOU in `Eventually.UntilAsync`: when both `timeoutSource.Token` and the caller's `cancellationToken` fire in the same quantum, the `when` filter evaluates `false` and…

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Testing/Polling/Eventually.cs
reason: Simultaneous-cancellation TOCTOU in `Eventually.UntilAsync`: when both `timeoutSource.Token` and the caller's `cancellationToken` fire in the same quantum, the `when` filter evaluates `false` and raw `OperationCanceledException` propagates instead of `TimeoutException`. Low probability for a testing utility; acceptable trade-off. (`src/Hexalith.Folders.Testing/Polling/Eventually.cs`)
status: open

### DW-187: `TaskId`, `CorrelationId`, `IdempotencyKey` not validated at `TestFolderContext` construction

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs
reason: `TaskId`, `CorrelationId`, `IdempotencyKey` not validated at `TestFolderContext` construction — intentional asymmetric design: stream-segment fields validated early; header fields validated at header-build time by `ValidateHeaderValue`. (`src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`)
status: open

### DW-188: `"diff --git"` in `CredentialMaterialMarkers` is overly broad; would false-positive on any fixture legitimately containing a diff snippet

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs
reason: `"diff --git"` in `CredentialMaterialMarkers` is overly broad; would false-positive on any fixture legitimately containing a diff snippet. Pre-existing marker moved from old `ShouldNotContain` calls; no current fixture affected. (`tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs`)
status: open

### DW-189: `TestFolderContext` changed from positional record to custom-constructor record

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs
reason: `TestFolderContext` changed from positional record to custom-constructor record — loses compiler-synthesised positional deconstruction and `System.Text.Json` deserialisation support without a custom converter. Testing helper; neither usage pattern expected. (`src/Hexalith.Folders.Testing/Factories/TestFolderContext.cs`)
status: open

### DW-190: `RecursiveSubmoduleViolationDetectionDoesNotTreatBroadNearbyWordingAsExemption` test outcome is correct, but the `proseLinesSeen == 1` early-break prevents line 2 from ever being evaluated

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
reason: `RecursiveSubmoduleViolationDetectionDoesNotTreatBroadNearbyWordingAsExemption` test outcome is correct, but the `proseLinesSeen == 1` early-break prevents line 2 from ever being evaluated — the test does not mechanically verify the claimed "broad wording is rejected" path. (`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`)
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:482-497 constructs the unsafe command directly and asserts the violation; the faulty early-break search is gone.

### DW-191: A probe's own `OperationCanceledException` (from an unrelated internal token) may be misattributed as a timeout when `timeoutSource.IsCancellationRequested` is simultaneously true

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Testing/Polling/Eventually.cs
reason: A probe's own `OperationCanceledException` (from an unrelated internal token) may be misattributed as a timeout when `timeoutSource.IsCancellationRequested` is simultaneously true. Very low probability for a test utility. (`src/Hexalith.Folders.Testing/Polling/Eventually.cs`)
status: open

### DW-192: `CollectPrecedingProseContext` early-break limits exemption window to the immediately preceding prose line

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
reason: `CollectPrecedingProseContext` early-break limits exemption window to the immediately preceding prose line — intentional per 2026-05-12 review finding ("immediate preceding prose must carry the warning"); a warning comment separated from the recursive command by one non-warning line will not be seen. (`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`)
status: open

### DW-193: `C6MappingArtifactMirrorsArchitectureVocabularyBidirectionally` checks backtick-wrapped event names in architecture.md

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold round 2 (2026-05-12)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs
reason: `C6MappingArtifactMirrorsArchitectureVocabularyBidirectionally` checks backtick-wrapped event names in architecture.md — tests pass because events appear backtick-wrapped in prose, but the canonical transition table uses bare cell names; design nuance, not a failure. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`)
status: open

### DW-194: Dynamic `last reviewed` date in row-date assertions changes failure semantics from hard-coded `"2026-05-11"` to front-matter-derived date

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold round 2 (2026-05-12)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs
reason: Dynamic `last reviewed` date in row-date assertions changes failure semantics from hard-coded `"2026-05-11"` to front-matter-derived date — intentional improvement; undocumented scope change. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`)
status: open

### DW-195: `"diff --git"` in `SecretSubstringDenylist` alongside credential patterns produces confusing diagnostic; intent is correct (no patch content in docs) but classification is misleading

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold round 2 (2026-05-12)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs
reason: `"diff --git"` in `SecretSubstringDenylist` alongside credential patterns produces confusing diagnostic; intent is correct (no patch content in docs) but classification is misleading. (`tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs`)
status: open

### DW-196: `RepositoryRoot` `MaxAncestors = 12` magic number; could throw `InvalidOperationException` on deeply nested CI paths. 12 is sufficient for known repo layouts; pre-existing design.

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold round 2 (2026-05-12)"), 2026-08-24
location: RepositoryRoot
reason: `RepositoryRoot` `MaxAncestors = 12` magic number; could throw `InvalidOperationException` on deeply nested CI paths. 12 is sufficient for known repo layouts; pre-existing design.
status: open

### DW-197: S2 OIDC test split into `S2OidcArtifactPinsFrozenJwtBearerSettings` and `S2OidcArtifactDocumentsAuthoritativeClaimProvenanceAndSyntheticPlaceholders`

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold round 2 (2026-05-12)"), 2026-08-24
location: S2OidcArtifactPinsFrozenJwtBearerSettings
reason: S2 OIDC test split into `S2OidcArtifactPinsFrozenJwtBearerSettings` and `S2OidcArtifactDocumentsAuthoritativeClaimProvenanceAndSyntheticPlaceholders` — full OIDC contract only visible across both tests; structural coupling concern, both pass.
status: open

### DW-198: `<InternalsVisibleTo>` entries in `src/Hexalith.Folders.*/*.csproj` point to test assemblies

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: src/Hexalith.Folders.*/*.csproj
reason: `<InternalsVisibleTo>` entries in `src/Hexalith.Folders.*/*.csproj` point to test assemblies (`Hexalith.Folders.*.Tests`) that didn't exist in commit `eb52d15`; they exist at HEAD as later commits added them. No action needed unless a test project is later removed.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj:6 and the other InternalsVisibleTo declarations all map to existing test projects represented by tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:184-202.

### DW-199: `Directory.Build.props:23-26` declares MSBuild properties `HexalithEventStoreRoot` and `HexalithTenantsRoot` that nothing currently consumes

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: Directory.Build.props:23-26
reason: `Directory.Build.props:23-26` declares MSBuild properties `HexalithEventStoreRoot` and `HexalithTenantsRoot` that nothing currently consumes. Likely placeholders for future-story consumption (e.g., per-project file lists, NuGet feed switching). Revisit when a downstream story imports them.
status: done 2026-08-24
resolution: already resolved: Directory.Build.props:23-27 derives the shared *FromSource switches, and consumer projects such as src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj use the corresponding source roots and conditions.

### DW-200: Predev preflight gate `result: "fail"` recorded in `predev-preflight-2026-05-10T200403Z.json` and latest pointer due to a dirty working tree (sprint-status + story 1-6 staged)

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: predev-preflight-2026-05-10T200403Z.json
reason: Predev preflight gate `result: "fail"` recorded in `predev-preflight-2026-05-10T200403Z.json` and latest pointer due to a dirty working tree (sprint-status + story 1-6 staged). Process concern outside the code-review scope — track via the preflight gate, not in this story.
status: done 2026-09-01
resolution: already resolved: _bmad-output/process-notes/predev-preflight-latest.json:4,66 identifies the later 2026-05-21 run and a different 19-path dirty set, so the original Story 1.6 latest-pointer condition is superseded.

### DW-201: `.gitmodules` declares 5 root submodules including `Hexalith.Memories`, but `Directory.Build.props` only detects `Hexalith.EventStore` and `Hexalith.Tenants`

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: .gitmodules
reason: `.gitmodules` declares 5 root submodules including `Hexalith.Memories`, but `Directory.Build.props` only detects `Hexalith.EventStore` and `Hexalith.Tenants`. Add a `HexalithMemoriesRoot` detector when a downstream story first references Memories.
status: done 2026-08-24
resolution: already resolved: Directory.Build.props:4-7 detects EventStore, Tenants, Memories, and FrontComposer roots; :25 specifically derives HexalithMemoriesFromSource from the detected Memories project.

### DW-202: No `Directory.Build.targets` adapted from `Hexalith.Tenants`. Acceptable deviation today; revisit when stories require SourceLink wiring or pack-time MSBuild logic.

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: Directory.Build.targets
reason: No `Directory.Build.targets` adapted from `Hexalith.Tenants`. Acceptable deviation today; revisit when stories require SourceLink wiring or pack-time MSBuild logic.
status: done 2026-08-24
resolution: already resolved: Directory.Build.targets:1-43 now supplies the root container/build targets that the deferred entry reported missing.

### DW-203: Do not promote Hexalith.Memories semantic indexing or RAG retrieval into MVP unless the PRD is explicitly updated

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: n/a
reason: Do not promote Hexalith.Memories semantic indexing or RAG retrieval into MVP unless the PRD is explicitly updated. Current approved course correction keeps Memories as an architecture-guided extension path.
status: done 2026-08-24
resolution: already resolved: _bmad-output/planning-artifacts/prd.md:656-672 authorizes metadata-token recall and indexing status in MVP, while :687-702 explicitly excludes indexed body recall; FR58 at :832-836 matches that boundary.

### DW-204: Add a worker-owned Memories semantic-indexing integration story

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: n/a
reason: When a downstream story first implements Memories integration, add a dedicated story or story split for worker-owned semantic indexing:; worker-side `IFolderSemanticIndexingClient` port,; optional `Hexalith.Memories.Client.Rest` / `Hexalith.Memories.Contracts` dependency only from `Hexalith.Folders.Workers`,; Folders-owned indexing bridge projection for `file version -> Memories workflow/memory unit/status`,; stable source URI/idempotency metadata,; explicit skipped/too-large/binary/excluded statuses,; authorized RAG query facade that applies tenant access, folder ACL, path policy, sensitivity classification, and C4 limits before calling Memories.
status: done 2026-08-24
resolution: already resolved: _bmad-output/implementation-artifacts/sprint-status.yaml:270 onward marks Stories 10.1-10.6 done, and src/Hexalith.Folders.Workers/SemanticIndexing contains the delivered worker implementation.

### DW-205: If Memories packages or project references are introduced, update root dependency detection with `HexalithMemoriesRoot` and keep submodule initialization root-level only.

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: HexalithMemoriesRoot
reason: If Memories packages or project references are introduced, update root dependency detection with `HexalithMemoriesRoot` and keep submodule initialization root-level only.
status: done 2026-08-24
resolution: already resolved: Directory.Build.props:6 and :25 define HexalithMemoriesRoot and HexalithMemoriesFromSource; src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:21 and integration tests consume them.

### DW-206: Operations-console stories may display semantic-indexing status only as metadata/projection state; they must not expose indexed content, snippets, raw Memories payloads, file browsing, or RAG…

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: n/a
reason: Operations-console stories may display semantic-indexing status only as metadata/projection state; they must not expose indexed content, snippets, raw Memories payloads, file browsing, or RAG response assembly in MVP.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2978-2979 and :8571-8572, together with _bmad-output/planning-artifacts/prd.md:698-702, codify the no-content/no-snippet indexing boundary implemented by the UI projection.

### DW-207: F1: C4 limits inclusive/exclusive ambiguity — `docs/contract/idempotency-and-parity-rules.md` cites byte limits in the Non-Mutating Read Consistency section (e.g., 1048576, 262144) without…

origin: migrated from legacy ledger ("Deferred from: code review of 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules (2026-05-13)"), 2026-08-24
location: docs/contract/idempotency-and-parity-rules.md
reason: F1: C4 limits inclusive/exclusive ambiguity — `docs/contract/idempotency-and-parity-rules.md` cites byte limits in the Non-Mutating Read Consistency section (e.g., 1048576, 262144) without specifying whether boundaries are inclusive. C4 input-limits artifact (Story 1.4) is the authority for precise boundary behavior; revisit if consumers diverge.
status: done 2026-08-24
resolution: already resolved: docs/exit-criteria/c4-input-limits.md:19-21 explicitly states result-count, range-byte, and response-budget limit inclusivity.

### DW-208: F2: Verification Coverage AC mapping unenforced — rows in `docs/contract/idempotency-and-parity-rules.md` "Verification Coverage" cite ACs by number but the mapping is doc-only; renaming a test or…

origin: migrated from legacy ledger ("Deferred from: code review of 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules (2026-05-13)"), 2026-08-24
location: docs/contract/idempotency-and-parity-rules.md
reason: F2: Verification Coverage AC mapping unenforced — rows in `docs/contract/idempotency-and-parity-rules.md` "Verification Coverage" cite ACs by number but the mapping is doc-only; renaming a test or modifying its scope leaves the AC mapping silently stale. Revisit if traceability tooling becomes available.
status: open

### DW-209: F3: `equivalence_classification` strings not enum-typed

origin: migrated from legacy ledger ("Deferred from: code review of 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules (2026-05-13)"), 2026-08-24
location: tests/fixtures/idempotency-encoding-corpus.json
reason: F3: `equivalence_classification` strings not enum-typed — long compound classification strings (~50-90 chars) in `tests/fixtures/idempotency-encoding-corpus.json` are used as identifiers without schema enum constraint; one typo silently breaks future hash-helper consumers. Tied to D7 (whether to add corpus schema); revisit when Story 1.12 helpers begin consuming the values.
status: done 2026-08-24
resolution: already resolved: tests/fixtures/idempotency-encoding-corpus.schema.json:41-121 requires equivalence_classification and constrains it with an enum; governance tests consume that typed field.

### DW-210: F4: `File.ReadAllText` BOM/encoding handling — `tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs` reads files without explicit encoding; a UTF-8-BOM commit could shift `IndexOf`…

origin: migrated from legacy ledger ("Deferred from: code review of 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs
reason: F4: `File.ReadAllText` BOM/encoding handling — `tests/Hexalith.Folders.Testing.Tests/ContractRulesArtifactTests.cs` reads files without explicit encoding; a UTF-8-BOM commit could shift `IndexOf` offsets. Project standardizes on UTF-8 without BOM; revisit if an editor introduces non-UTF-8 content.
status: open

### DW-211: PathTooLongException nuance in `VerifyCurrentDetailed` catch filter

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:70
reason: PathTooLongException nuance in `VerifyCurrentDetailed` catch filter [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:70`] — current catch list is sufficient for the inputs the helper receives.
status: done 2026-08-25
resolution: already resolved: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:61 catches IOException, which already includes PathTooLongException.

### DW-212: Duplicate switch logic in `Resolve*OperationKindWireValue` / `Resolve*OperationId`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:354-368
reason: Duplicate switch logic in `Resolve*OperationKindWireValue` / `Resolve*OperationId` [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:354-368`] — cosmetic refactor; two switches will drift only if a new enum value lands without updating both.
status: open

### DW-213: `EnsureDeclaredOrder` duplicate-after-sort message wording

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:1375-1394
reason: `EnsureDeclaredOrder` duplicate-after-sort message wording [`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:1375-1394`] — cosmetic; order check fires before duplicate check for differently-cased equal names.
status: open

### DW-214: `ResolveField` perf nit — rebuilds `operationParameters` per field [`src/Hexalith.Folders.Client/Generation/Program.cs:616-638`] — micro-optimization; field counts per operation are small.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:616-638
reason: `ResolveField` perf nit — rebuilds `operationParameters` per field [`src/Hexalith.Folders.Client/Generation/Program.cs:616-638`] — micro-optimization; field counts per operation are small.
status: open

### DW-215: `HelperGenerationTargetRegeneratesWhenContractSpineChanges` test name vs assertion strength

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1625-1651
reason: `HelperGenerationTargetRegeneratesWhenContractSpineChanges` test name vs assertion strength [`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1625-1651`] — hash-difference check is sufficient evidence of regeneration; comment-only YAML mutation is acceptable.
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:287-337 invokes the MSBuild generator against a mutated spine and asserts that the generated file changes.

### DW-216: `ChangedPathEvidence2` shim documentation [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:34-36`] — the shim is harmless and the NSwag duplicate-emission cause is known.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:34-36
reason: `ChangedPathEvidence2` shim documentation [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:34-36`] — the shim is harmless and the NSwag duplicate-emission cause is known.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Client/Compat/ChangedPathEvidenceShim.cs:3-16 isolates and documents the compatibility shim, including cause and removal conditions; generator commentary points to the dedicated shim.

### DW-217: MSBuild `<None Remove>` / `<None Include>` order-dependence in client csproj

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1193-1199
reason: MSBuild `<None Remove>` / `<None Include>` order-dependence in client csproj [`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1193-1199`] — works as written; revisit when the file layout changes.
status: open

### DW-218: `dotnet run --project Generation` runs implicit build per outer build [`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1220-1222`] — perf concern; ties into Story 1.14 CI gate review.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1220-1222
reason: `dotnet run --project Generation` runs implicit build per outer build [`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1220-1222`] — perf concern; ties into Story 1.14 CI gate review.
status: done 2026-08-28
resolution: already resolved: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:40-47 declares Inputs and Outputs for GenerateHexalithFoldersIdempotencyHelpers, so MSBuild skips the generator when its output is current.

### DW-219: `nswag.json` `newLineBehavior` claim about old location [`src/Hexalith.Folders.Client/nswag.json:1474-1486`] — the new location works; original-location ineffectiveness is folklore.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/nswag.json:1474-1486
reason: `nswag.json` `newLineBehavior` claim about old location [`src/Hexalith.Folders.Client/nswag.json:1474-1486`] — the new location works; original-location ineffectiveness is folklore.
status: done 2026-08-28
resolution: already resolved: src/Hexalith.Folders.Client/nswag.json:9-12 places newLineBehavior under openApiToCSharpClient, and tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:35-38 rejects the obsolete fromDocument placement.

### DW-220: `ComputeCorpusHash` uses `GetRawText` for non-string types [`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1779-1790`] — equivalence-only comparison so string-vs-string is acceptable.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1779-1790
reason: `ComputeCorpusHash` uses `GetRawText` for non-string types [`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1779-1790`] — equivalence-only comparison so string-vs-string is acceptable.
status: open

### DW-221: `LockWorkspaceRequest` missing `repository_binding_id` vs `PrepareWorkspaceRequest` includes it

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:374-392
reason: `LockWorkspaceRequest` missing `repository_binding_id` vs `PrepareWorkspaceRequest` includes it [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:374-392`] — pending spine verification; owned by Stories 1.7-1.11 if it is a spine bug.
status: done 2026-08-25
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:1490 defines LockWorkspace idempotency without repository_binding_id, and generated helper lines 428-440 match that canonical contract.

### DW-222: `Render` uses `AppendLine` + final `ReplaceLineEndings("\n")` [`src/Hexalith.Folders.Client/Generation/Program.cs:929`] — Round 2 P28 was marked done with this approach; functionally deterministic.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:929
reason: `Render` uses `AppendLine` + final `ReplaceLineEndings("\n")` [`src/Hexalith.Folders.Client/Generation/Program.cs:929`] — Round 2 P28 was marked done with this approach; functionally deterministic.
status: done 2026-09-01
resolution: already resolved: src/Hexalith.Folders.Client/Generation/Program.cs:534 normalizes rendered output with ReplaceLineEndings("\n"), providing deterministic line endings.

### DW-223: `ReadProperties` does not follow `allOf` / `$ref` composition

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:709-722
reason: `ReadProperties` does not follow `allOf` / `$ref` composition [`src/Hexalith.Folders.Client/Generation/Program.cs:709-722`] — no current spine schema requires it; revisit when composed schemas with idempotency fields arrive.
status: open

### DW-224: `parent_folder_id` `Specified` hardcode coupling [`src/Hexalith.Folders.Client/Generation/Program.cs:1020`] — only fires for `CreateFolderRequest` today.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:1020
reason: `parent_folder_id` `Specified` hardcode coupling [`src/Hexalith.Folders.Client/Generation/Program.cs:1020`] — only fires for `CreateFolderRequest` today.
status: open

### DW-225: `Compute` does not accept `CancellationToken` [`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:14-27`]

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:14-27
reason: `Compute` does not accept `CancellationToken` [`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs:14-27`] — hash work is in-process and short; revisit when invoked from request-scoped network paths.
status: open

### DW-226: `repositoryRoot` symlink / casing handling [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:50-73`]

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:50-73
reason: `repositoryRoot` symlink / casing handling [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:50-73`] — filesystem-boundary concern; build-time and CI consume canonicalized paths today.
status: open

### DW-227: General-case null-vs-omitted presence tracking [`src/Hexalith.Folders.Client/Generation/Program.cs:546`]

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:546
reason: General-case null-vs-omitted presence tracking [`src/Hexalith.Folders.Client/Generation/Program.cs:546`] — only `parent_folder_id` has a presence companion via `ParentFolderIdSpecified`. Revisit when a second nullable single-property idempotency field surfaces in the spine.
status: open

### DW-228: `oneOf` traversal without explicit OpenAPI `discriminator`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:215-221
reason: `oneOf` traversal without explicit OpenAPI `discriminator` [`src/Hexalith.Folders.Client/Generation/Program.cs:215-221`] — `FileMutationRequest` is the only schema needing this today and is handled via `SpecialFields.Registry` projection. Story 1.13 owns generic discriminator-driven `oneOf` resolution.
status: done 2026-08-28
decision: 2026-08-28 Keep special case — Document the two-axis const union as the reason for retaining SpecialFields until another hash-relevant union requires generalization.
resolution: closed by human decision: Document the two-axis const union as the reason for retaining SpecialFields until another hash-relevant union requires generalization.
decision: 2026-08-28 Keep special case — Document the two-axis const union as the reason for retaining SpecialFields until another hash-relevant union requires generalization.

### DW-229: NFC/NFD/NFKC normalization not implemented in hasher

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs
reason: NFC/NFD/NFKC normalization not implemented in hasher [`src/Hexalith.Folders.Client/Idempotency/HexalithIdempotencyHasher.cs`] — AC 6 reads "Unicode normalization where declared" and no current spine field declares normalization-eligibility. Revisit when a field declares it.
status: open
decision: 2026-08-28 Reject non-NFC — Validate explicitly NFC-declared path metadata before hashing and fail closed on decomposed input, preserving hashes for already-valid requests.
decision: 2026-08-28 Reject non-NFC — Validate explicitly NFC-declared path metadata before hashing and fail closed on decomposed input, preserving hashes for already-valid requests.

### DW-230: Tempdir cleanup catches that swallow `IOException`/`UnauthorizedAccessException`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1640-1653
reason: Tempdir cleanup catches that swallow `IOException`/`UnauthorizedAccessException` [`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1640-1653`] — overlaps with the WaitForExit-orphan patch shipped in the same round; resolving the orphan process also resolves the cleanup races.
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:308-331 kills and drains a timed-out generator process before temporary-directory cleanup, eliminating the recorded orphan-process cleanup race.

### DW-231: YamlDotNet duplicate-keys detection [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `LoadYamlMapping`]

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: YamlDotNet duplicate-keys detection [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `LoadYamlMapping`] — overlaps prior JSON-duplicate-keys defer; fixtures are gate-owned and deterministic.
status: open

### DW-232: File encoding fallback (UTF-16/Latin-1/no-BOM) [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `ScanText` via `File.ReadAllText`] — overlaps prior BOM defer.

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: File encoding fallback (UTF-16/Latin-1/no-BOM) [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `ScanText` via `File.ReadAllText`] — overlaps prior BOM defer.
status: open

### DW-233: AssertMetadataOnly Linux absolute-path roots expansion

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: AssertMetadataOnly Linux absolute-path roots expansion (`/home/`, `/Users/`, `/var/`, `/tmp/`) [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `AssertMetadataOnly`] — overlaps prior defer; primary CI is Windows.
status: open

### DW-234: `InventoryChannel.Clone()` use-after-dispose latent risk

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `InventoryChannel.Clone()` use-after-dispose latent risk [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `InventoryChannel`] — current code correct per .NET docs; revisit if accessor returns raw `JsonElement`.
status: done 2026-08-28
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs:506-514 returns channel.Clone() before the owning JsonDocument is disposed, eliminating the alleged JsonElement use-after-dispose risk.

### DW-235: `schema_version "1.0.0"` hard-coded coupling between corpus and test

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `schema_version "1.0.0"` hard-coded coupling between corpus and test [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `SentinelCorpusDeclaresAuthoritativeSyntheticVocabulary`] — intentional schema-version gating; lift only when schema iterates.
status: open

### DW-236: File-locking races on Windows runners under parallel xUnit

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: File-locking races on Windows runners under parallel xUnit [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `ScanText` via `File.ReadAllText`] — rare on current runner topology.
status: open

### DW-237: Case-collision normalization across OS in `EnumerateSourceFiles`

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: Case-collision normalization across OS in `EnumerateSourceFiles` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `EnumerateSourceFiles`] — cross-OS naming collision is rare in generated SDK and contract directories.
status: open

### DW-238: Workflow doc claim `checkout with submodules: false` not visibly enforced in this diff

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: .github/workflows/contract-spine.yml
reason: Workflow doc claim `checkout with submodules: false` not visibly enforced in this diff [`.github/workflows/contract-spine.yml`, `docs/contract/safety-invariant-ci-gates.md`] — existing checkout step not modified by this story; verify when 1.14 ownership consolidates.
status: done 2026-08-24
resolution: already resolved: .github/workflows/contract-spine.yml:20-24 explicitly configures actions/checkout with submodules: false.

### DW-239: `safe-provenance` classification global allowlist behavior

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/fixtures/audit-leakage-corpus.json
reason: `safe-provenance` classification global allowlist behavior [`tests/fixtures/audit-leakage-corpus.json`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `ScanText`] — only one safe-provenance sample (`safe-provenance-operation-id`) exists today; per-sample `allowed_in_channels` design can wait until a second safe-provenance entry is needed.
status: open

### DW-240: `HashSet<SafetyScanDiagnostic>` dedup may hide multi-route findings when a file is reached via multiple channels

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `HashSet<SafetyScanDiagnostic>` dedup may hide multi-route findings when a file is reached via multiple channels [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `ScanManifestCoveredArtifacts`] — `SafetyScanDiagnostic` is `sealed record` so dedup is structurally correct; revisit only if a redundant-route signal becomes needed.
status: open

### DW-241: `MissingChannelDiagnostics` uses a single canned `Remediation` string for all prerequisite-drift channels

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `MissingChannelDiagnostics` uses a single canned `Remediation` string for all prerequisite-drift channels [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `BuildMissingChannelDiagnostics`] — add a per-channel `remediation_hint` field in inventory; not gating.
status: open

### DW-242: `-SkipRestoreBuild` probes only the test assembly, not direct dependencies like `Hexalith.Folders.Contracts.dll`

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/tools/run-safety-invariant-gates.ps1
reason: `-SkipRestoreBuild` probes only the test assembly, not direct dependencies like `Hexalith.Folders.Contracts.dll` [`tests/tools/run-safety-invariant-gates.ps1`] — `dotnet test --no-build` raises a clear error on missing deps; double-guarding adds complexity.
status: open

### DW-243: `AssertContainsText` emits `SAFETY-PREREQUISITE-DRIFT` for both "required marker absent" and "context-query missing" cases

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `AssertContainsText` emits `SAFETY-PREREQUISITE-DRIFT` for both "required marker absent" and "context-query missing" cases [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `AssertContainsText`] — vocabulary refinement (e.g., dedicated `SAFETY-REQUIRED-MARKER-ABSENT`); current message is non-leaking.
status: open

### DW-244: `LoadYamlMapping` / `JsonDocument.Parse` calls lack bounded options (max depth, size cap) [multiple call sites in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`]

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `LoadYamlMapping` / `JsonDocument.Parse` calls lack bounded options (max depth, size cap) [multiple call sites in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs`] — defense-in-depth against malformed/oversized fixtures; current fixtures are reviewer-curated.
status: open

### DW-245: `BoundedDiagnosticException` does not validate `ruleId` against an allowed set nor assert `remediation` through `AssertMetadataOnly`

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `BoundedDiagnosticException` does not validate `ruleId` against an allowed set nor assert `remediation` through `AssertMetadataOnly` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `BoundedDiagnosticException`] — tighten if a third caller is added with non-constant inputs.
status: open

### DW-246: `AssertRepositoryRelativePath` does not reject UNC paths

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `AssertRepositoryRelativePath` does not reject UNC paths (`//server/share/...`) or extended-length prefixes (`\?\D:\...`) [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `AssertRepositoryRelativePath`] — no current callers can produce these path shapes; tighten when a new include_root source is introduced.
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs:655-663 rejects fully-qualified paths, leading slashes, and every backslash; those checks reject UNC and extended-length prefixes.

### DW-247: `SerializeYaml` constructs `StringWriter` without `CultureInfo.InvariantCulture`

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `SerializeYaml` constructs `StringWriter` without `CultureInfo.InvariantCulture` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `SerializeYaml`] — YamlDotNet writes strings literally for the fields the gate scans; revisit if numeric YAML enters the corpus.
status: open

### DW-248: `LoadOpenApiOperationIds` crashes on `$ref`-only method operations and on a method-mapping missing `operationId`

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18, Round 2)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs
reason: `LoadOpenApiOperationIds` crashes on `$ref`-only method operations and on a method-mapping missing `operationId` [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` `LoadOpenApiOperationIds`] — current OpenAPI spec has no `$ref` operations and every operation carries an `operationId`; overlaps Round 2 prerequisite-drift work (Decision #5).
status: open

### DW-249: `ParseRequiredBoolean` rejects YAML 1.1 numeric booleans

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18, Round 2)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs
reason: `ParseRequiredBoolean` rejects YAML 1.1 numeric booleans (`1` / `0`) [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` `ParseRequiredBoolean`] — corpus fixtures only use lowercase literals; tighten when an authored fixture introduces numeric truth values.
status: open

### DW-250: `Write-GovernanceReport` lacks `try/catch` around the JSON write

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18, Round 2)"), 2026-08-24
location: tests/tools/run-governance-completeness-gates.ps1
reason: `Write-GovernanceReport` lacks `try/catch` around the JSON write — a partial write leaves `latest.json` stale while the shell exit code still reflects the original failure [`tests/tools/run-governance-completeness-gates.ps1` `Write-GovernanceReport`] — rare race; harden when a CI consumer reports stale-report incidents.
status: open

### DW-251: `ReadRootTargetFramework` regex matches the first uncommented `<TargetFramework>` and does not strip XML comments or handle conditioned elements

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18, Round 2)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs
reason: `ReadRootTargetFramework` regex matches the first uncommented `<TargetFramework>` and does not strip XML comments or handle conditioned elements [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` `ReadRootTargetFramework`] — current `Directory.Build.props` is clean; revisit when conditional TFM authoring lands.
status: open

### DW-252: `IsGeneratedOrBuildOutput` path-segment check is case-sensitive

origin: migrated from legacy ledger ("Deferred from: code review of 1-16-wire-exit-criteria-and-parity-completeness-gates (2026-05-18, Round 2)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs
reason: `IsGeneratedOrBuildOutput` path-segment check is case-sensitive (`/Generated/`, `/bin/`, `/obj/`, `/quarantine/`) [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` `IsGeneratedOrBuildOutput`] — switch to `OrdinalIgnoreCase` if a generator with non-standard casing is introduced.
status: open

### DW-253: `accesscontrol.yaml` ships `defaultAction: allow` for every Dapr app with no environment guard

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml
reason: `accesscontrol.yaml` ships `defaultAction: allow` for every Dapr app with no environment guard [`src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml`] — local-dev scaffold; production deny-by-default access control belongs to Story 7.1.
status: done 2026-08-24
resolution: already resolved: deploy/dapr/production/accesscontrol.yaml:11-18 defines production access control with defaultAction: deny and per-caller deny defaults.

### DW-254: JWT audience hard-coded to `hexalith-eventstore` across all four services, `SigningKey=""`, `RequireHttpsMetadata=false`

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.AppHost/Program.cs:51-53
reason: JWT audience hard-coded to `hexalith-eventstore` across all four services, `SigningKey=""`, `RequireHttpsMetadata=false` [`src/Hexalith.Folders.AppHost/Program.cs:51-53`] — local-dev AppHost composition; production OIDC + secret-store wiring belongs to Story 7.2. Audience-per-app-id correctness must be revisited there.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs:94-106 validates issuer/audience and :128-138 rejects blank audience or non-HTTPS metadata outside Development/Test; production secret-store deployment assets are present.

### DW-255: `Workers/Program.cs` is an empty host with no `IHostedService`, no Tenants subscription, no work

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.Workers/Program.cs
reason: `Workers/Program.cs` is an empty host with no `IHostedService`, no Tenants subscription, no work [`src/Hexalith.Folders.Workers/Program.cs`] — workers do nothing in Story 2.1; Story 2.9 ("react to Tenants events through worker handlers") owns the subscription pipeline.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Workers/Program.cs:5-17 now adds service defaults and tenant event workers, enables CloudEvents, and maps subscription, worker, and health endpoints.

### DW-256: `RemovedConfigurationKeys` set has no size cap or rate limit

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs:111
reason: `RemovedConfigurationKeys` set has no size cap or rate limit [`src/Hexalith.Folders/Projections/TenantAccess/FolderTenantAccessHandler.cs:111`] — no AC bounds it; revisit when the durable projection store choice (Story 2.x / 7.x) lands and retention strategy is decided.
status: open
decision: 2026-08-25 Keep open

### DW-257: Hard-coded `localhost:6379` Redis with no `AddRedis()` resource in AppHost

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.Aspire/FoldersAspireModule.cs
reason: Hard-coded `localhost:6379` Redis with no `AddRedis()` resource in AppHost [`src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`] — distributed deployment wiring belongs to Story 7.x; local dev still works because Dapr's Redis state-store default matches.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Aspire/FoldersAspireModule.cs no longer hard-codes localhost:6379; src/Hexalith.Folders.AppHost/Program.cs:51-60 supplies the shared EventStore topology resources.

### DW-258: `OrganizationAclMetadataLeakageTests` scans only the happy-path single-Grant result; multi-op `Initialize` results, rejection paths (including the pre-allow tenant-denied path that echoes raw…

origin: migrated from legacy ledger ("Deferred from: code review of 2-2-implement-organization-aggregate-acl-baseline (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Aggregates/Organization/OrganizationAclMetadataLeakageTests.cs
reason: `OrganizationAclMetadataLeakageTests` scans only the happy-path single-Grant result; multi-op `Initialize` results, rejection paths (including the pre-allow tenant-denied path that echoes raw `OrganizationId`/`CorrelationId`), and event-only JSON serialization are not exercised against the leakage sentinel corpus [`tests/Hexalith.Folders.Tests/Aggregates/Organization/OrganizationAclMetadataLeakageTests.cs`] — leakage coverage one fact deep, deferrable as a test hardening pass.
status: open

### DW-259: `OrganizationAclTenantGate` has no DI/composition root wiring yet

origin: migrated from legacy ledger ("Deferred from: code review of 2-2-implement-organization-aggregate-acl-baseline (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Organization/OrganizationAclTenantGate.cs
reason: `OrganizationAclTenantGate` has no DI/composition root wiring yet [`src/Hexalith.Folders/Aggregates/Organization/OrganizationAclTenantGate.cs`] — the gate is unreachable from any handler/controller/actor in the diff. Spec scope is "domain only"; production wiring belongs to a later Epic 2 worker/handler story. Tracking note so a downstream story does not bypass the gate.
status: open
decision: 2026-08-25 Wire in Story 12.1 — Add the gate to the real organization command path with DI registration and no-touch tenant-denial integration evidence.

### DW-260: Validator silently overwrites identical-tuple operations via `unique[$"{intent}|{tupleKey}"] = operation`

origin: migrated from legacy ledger ("Deferred from: code review of 2-2-implement-organization-aggregate-acl-baseline (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Organization/OrganizationAclCommandValidator.cs:53
reason: Validator silently overwrites identical-tuple operations via `unique[$"{intent}|{tupleKey}"] = operation` [`src/Hexalith.Folders/Aggregates/Organization/OrganizationAclCommandValidator.cs:53`] — today `OrganizationAclOperation` has only `(PrincipalKind, PrincipalId, Action, Intent)`, so the overwrite is loss-less. If new fields are added later (metadata, expiry, justification) the overwrite becomes a silent bug; add an equality guard before overwrite when the operation shape grows.
status: open

### DW-261: Future-dated evidence has no inline test row in `OrganizationAclTenantEvidenceGateTests.RejectedTenantEvidenceShouldPreventAllStreamSideEffects`

origin: migrated from legacy ledger ("Deferred from: code review of 2-2-implement-organization-aggregate-acl-baseline (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Aggregates/Organization/OrganizationAclTenantEvidenceGateTests.cs:11-37
reason: Future-dated evidence has no inline test row in `OrganizationAclTenantEvidenceGateTests.RejectedTenantEvidenceShouldPreventAllStreamSideEffects` [`tests/Hexalith.Folders.Tests/Aggregates/Organization/OrganizationAclTenantEvidenceGateTests.cs:11-37`] — covered transitively because Story 2.1's authorizer routes future timestamps through `TenantAccessOutcome.MalformedEvidence`, which is in the theory. Adding a direct integration row pinned to a future `LastEventTimestamp` would belt-and-brace this assumption.
status: open

### DW-262: `OrganizationStreamName.IsValidSegment` uses `ToLowerInvariant` for canonical-casing check

origin: migrated from legacy ledger ("Deferred from: code review of 2-2-implement-organization-aggregate-acl-baseline (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Organization/OrganizationStreamName.cs:53
reason: `OrganizationStreamName.IsValidSegment` uses `ToLowerInvariant` for canonical-casing check [`src/Hexalith.Folders/Aggregates/Organization/OrganizationStreamName.cs:53`] — correct for ASCII inputs, but an explicit ASCII whitelist would be safer against Unicode lookalikes (Turkish dotted i, fullwidth letters). Subsumed by the segment-charset patch if the team picks the whitelist fix there.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders/Aggregates/Organization/OrganizationStreamName.cs:54-60 now validates canonical segments with the explicit ASCII regex ^[a-z0-9._-]+$.

### DW-263: `OrganizationAclAction.IsSupported` lets ZWSP-suffixed actions past `IsNullOrWhiteSpace` and `value.Trim()` equality, then rejects at the `HashSet` lookup

origin: migrated from legacy ledger ("Deferred from: code review of 2-2-implement-organization-aggregate-acl-baseline (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs:20-23
reason: `OrganizationAclAction.IsSupported` lets ZWSP-suffixed actions past `IsNullOrWhiteSpace` and `value.Trim()` equality, then rejects at the `HashSet` lookup [`src/Hexalith.Folders/Aggregates/Organization/OrganizationAclAction.cs:20-23`] — fails closed (returns `UnsupportedAction`), so no correctness gap, but a regex guard would surface the malformed input as a more specific signal. Cosmetic.
status: open

### DW-264: `EventStoreClaimTransformEvidence.Allowed` exposes its internal `HashSet<string>` via `IReadOnlySet<string>`

origin: migrated from legacy ledger ("Deferred from: code review of story-2.6 (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/EventStoreClaimTransformEvidence.cs:~17-23
reason: `EventStoreClaimTransformEvidence.Allowed` exposes its internal `HashSet<string>` via `IReadOnlySet<string>` (`src/Hexalith.Folders/Authorization/EventStoreClaimTransformEvidence.cs:~17-23`) — not exploitable from current call sites; revisit if the evidence is shared across threads or returned to untrusted callers.
status: open

### DW-265: Authorization unit tests pin to a single UTC instant

origin: migrated from legacy ledger ("Deferred from: code review of story-2.6 (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Authorization/LayeredFolderAuthorizationServiceTests.cs:~11
reason: Authorization unit tests pin to a single UTC instant — no DST/timezone-offset or `>` vs `>=` boundary coverage on `ObservedAt > clock.UtcNow` (`tests/Hexalith.Folders.Tests/Authorization/LayeredFolderAuthorizationServiceTests.cs:~11`) — fixed-clock pattern is consistent with sibling modules.
status: open

### DW-266: `ConfigurationDaprPolicyEvidenceProvider` does not validate empty/missing allow-lists at registration time

origin: migrated from legacy ledger ("Deferred from: code review of story-2.6 (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/ConfigurationDaprPolicyEvidenceProvider.cs:~28-29
reason: `ConfigurationDaprPolicyEvidenceProvider` does not validate empty/missing allow-lists at registration time (`src/Hexalith.Folders/Authorization/ConfigurationDaprPolicyEvidenceProvider.cs:~28-29`) — deferred to the production Dapr deployment story; configuration validation belongs with policy-deployment work.
status: open

### DW-267: Bounded-diagnostic-read tests don't assert max count/size or non-touch of provider/repository/file/workspace

origin: migrated from legacy ledger ("Deferred from: code review of story-2.6 (2026-05-19)"), 2026-08-24
location: n/a
reason: Bounded-diagnostic-read tests don't assert max count/size or non-touch of provider/repository/file/workspace — current seams don't expose those paths, so the invariant is satisfied by construction; revisit when diagnostic surface grows.
status: open

### DW-268: Preflight result `fail` in `_bmad-output/process-notes/predev-preflight-2026-05-19T120131Z.json` (36 dirty paths)

origin: migrated from legacy ledger ("Deferred from: code review of story-2.6 (2026-05-19)"), 2026-08-24
location: _bmad-output/process-notes/predev-preflight-2026-05-19T120131Z.json
reason: Preflight result `fail` in `_bmad-output/process-notes/predev-preflight-2026-05-19T120131Z.json` (36 dirty paths) — captured pre-commit state of story 2.6's own files; clean post-commit at `ed657e5`. Re-run preflight before flipping story 2.6 to `done`.
status: done 2026-08-24
resolution: already resolved: Commit ed657e5 records the clean post-commit state that the historical preflight note said was required before Story 2.6 closure.

### DW-269: Restore the systemic test-host composition baseline

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: test hosts using AddFoldersServer/MapFoldersServerEndpoints
reason: **SYSTEMIC TEST-HOST RED — NOW OWNED BY STORY 7.18 (NOT a 2-8b defect):** Test hosts calling `AddFoldersServer()`+`MapFoldersServerEndpoints()` without `AddServiceDefaults()` fail DI validation because `FoldersAuthSchemeValidator` needs `IAuthenticationSchemeProvider` and `MapDefaultEndpoints` needs `HealthCheckService`. **Re-measured 2026-05-31 (xUnit v3 in-process runner): `Server.Tests` Total 433, Failed 339, Passed 94, Skipped 0** (single auth/health DI-validation cause, fail-closed at `WebApplicationBuilder.Build()`); plus IntegrationTests 11 (Epic 5 Golden/MixedSurface) and Folders.Tests 2 (Epic 3 provider-boundary guards), documented 2026-05-31. Introduced by later stories (`6e816ce` auth validator + ServiceDefaults health checks). Mechanical fix: add `AddAuthentication()`+`AddHealthChecks()` (or a shared helper) to each affected host — same fix applied to 2-8b's own host. **CORRECTION:** this is a *distinct, ~50× larger* blocker than the "4–6 epic-1 CLI negative-scope reds in `Contracts.Tests`" that the Epic 7 retro named as the historical-reds item — different root cause (test-host composition gap vs CLI-now-exists). Resolution owned by **Story 7.18** (`7-18-restore-test-host-composition-baseline.md`), which reopens Epic 7. See `planning-artifacts/sprint-change-proposal-2026-05-31-test-host-composition-baseline.md`.
status: done 2026-08-24
resolution: already resolved: _bmad-output/implementation-artifacts/sprint-status.yaml:173-174 records Story 7.18 done with the restored Server.Tests composition baseline at 434 passed, 0 failed, 0 skipped.

### DW-270: P1: `InMemoryFolderRepository.EventsAppended`/`ResetAppendCounters` are not lock-guarded for reads

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: InMemoryFolderRepository.EventsAppended
reason: P1: `InMemoryFolderRepository.EventsAppended`/`ResetAppendCounters` are not lock-guarded for reads. Now `internal` (test-only via InternalsVisibleTo), single-threaded per test host. Already tracked rounds 3/4; revisit when an EventStore-backed repository replaces the in-memory default.
status: done 2026-08-28
resolution: already resolved: Commit f9cf754; src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:109-117 and :257-262 now guard both EventsAppended reads and ResetAppendCounters with _gate.

### DW-271: P4: No foreign-tenant-smuggling integration row exercising `HasCompetingClientTenant`/`TenantMismatch` end-to-end

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: HasCompetingClientTenant
reason: P4: No foreign-tenant-smuggling integration row exercising `HasCompetingClientTenant`/`TenantMismatch` end-to-end. Already deferred round 3 with documented defense-in-depth rationale (gate-unit coverage + layered-auth tenant comparison at the request handler).
status: done 2026-08-28
resolution: already resolved: Commit 1dcd53d; tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:343 now exercises authenticated-tenant versus envelope-tenant mismatch through REST, gateway, and /process with zero append.

### DW-272: W1: `CancellationToken` cannot propagate into `IDomainProcessor.ProcessAsync`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: IDomainProcessor.ProcessAsync
reason: W1: `CancellationToken` cannot propagate into `IDomainProcessor.ProcessAsync` — interface has no CT parameter; `FolderDomainProcessor` passes `CancellationToken.None` to all evidence providers. ADR 0001 explicitly accepts this; evidence providers are deterministic in-memory operations. Deferred — revisit when EventStore framework's `IDomainProcessor` gains a CT parameter.
status: done 2026-08-25
resolution: closed by human decision: Keep cancellation at surrounding request and provider paths because evidence calls at this seam are bounded and deterministic.
decision: 2026-08-25 Retain ADR tradeoff — Keep cancellation at surrounding request and provider paths because evidence calls at this seam are bounded and deterministic.

### DW-273: W3: `FolderAccessTenantGate.HasCompetingClientTenant` does not guard whitespace-only keys in `ClientControlledTenantIds`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: FolderAccessTenantGate.HasCompetingClientTenant
reason: W3: `FolderAccessTenantGate.HasCompetingClientTenant` does not guard whitespace-only keys in `ClientControlledTenantIds` — a whitespace key bypasses the mismatch check entirely while the archive gate explicitly rejects whitespace keys. Pre-existing; not introduced by this change.
status: open

### DW-274: W4: `FolderAccessTenantGate` evaluates ACL evidence before schema validation (inverse of archive gate order)

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: FolderAccessTenantGate
reason: W4: `FolderAccessTenantGate` evaluates ACL evidence before schema validation (inverse of archive gate order) — creates a differential side-channel on malformed access commands. Pre-existing; separate story scope.
status: open

### DW-275: W5: `FolderArchiveTenantGate.Map(TenantAccessOutcome.Allowed)` returns `MalformedEvidence` without an OTel trace tag; `FolderAccessTenantGate` stamps `Activity.Current?.SetTag(...)` for the same…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: FolderArchiveTenantGate.Map(TenantAccessOutcome.Allowed
reason: W5: `FolderArchiveTenantGate.Map(TenantAccessOutcome.Allowed)` returns `MalformedEvidence` without an OTel trace tag; `FolderAccessTenantGate` stamps `Activity.Current?.SetTag(...)` for the same branch. Minor observability gap; pre-existing in archive gate.
status: open

### DW-276: W6: TOCTOU window between `TryGetIdempotencyFingerprint` and `AppendIfFingerprintAbsent` in both gates

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: TryGetIdempotencyFingerprint
reason: W6: TOCTOU window between `TryGetIdempotencyFingerprint` and `AppendIfFingerprintAbsent` in both gates — optimistic concurrency design; by-design for the current in-memory repository. Deferred until an EventStore-backed repository provides transaction-level idempotency.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs:121-139 uses atomic AppendIfFingerprintAbsent outcome handling, closing the recorded check-then-append correctness window.

### DW-277: W7: `FolderArchiveTenantGate` calls `BindArchiveDecisionFingerprint` before `EvaluatePolicy`; `validation.IdempotencyFingerprint!` null-forgiving dereference would NRE if a custom validator…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: validation.IdempotencyFingerprint!
reason: W7: `FolderArchiveTenantGate` calls `BindArchiveDecisionFingerprint` before `EvaluatePolicy`; `validation.IdempotencyFingerprint!` null-forgiving dereference would NRE if a custom validator returns `IsAccepted=true` with null fingerprint. Pre-existing; baseline policy provider always supplies valid fingerprint.
status: done 2026-08-28
resolution: already resolved: src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs:65 validates the command, ACL, and policy before binding the archive decision fingerprint, and the gate has no custom-validator seam.

### DW-278: W8: `FolderAccessTenantGate.cs:110` null-forgiving `validation.IdempotencyFingerprint!` after `IsAccepted` guard

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: FolderAccessTenantGate.cs:110
reason: W8: `FolderAccessTenantGate.cs:110` null-forgiving `validation.IdempotencyFingerprint!` after `IsAccepted` guard — static factory methods never produce null; raw constructor bypass is the only path. Pre-existing.
status: open

### DW-279: `hasMore`/`nextCursor` in `ContextSearchQueryHandler.cs:211-212` are derived from the index's raw `TotalCount` (before the Folders-side security trim + hydration), so a page whose remaining…

origin: migrated from legacy ledger ("Deferred from: code review of story-10.5 (2026-06-24)"), 2026-08-24
location: ContextSearchQueryHandler.cs:211-212
reason: `hasMore`/`nextCursor` in `ContextSearchQueryHandler.cs:211-212` are derived from the index's raw `TotalCount` (before the Folders-side security trim + hydration), so a page whose remaining matches are all foreign/stale rows still reports `hasMore=true` and emits a cursor that yields an empty next page. New code, but UX-only and acceptable under the no-cross-tenant-existence-disclosure rule (an aggregate boolean over the caller's own tenant scope). Optional fix: emit a cursor only when the source returned a full pre-trim page (`Hits.Count == limit`).
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:217-221 emits a continuation cursor only when the post-filter item count reaches the requested limit.

### DW-280: SDK `ContextIndexSearchRequest.Limit` is generated as a non-nullable `int` (NSwag), so an SDK caller who omits `limit` serializes `limit:0`, which the server's `<= 0` guard rejects

origin: migrated from legacy ledger ("Deferred from: code review of story-10.5 (2026-06-24)"), 2026-08-24
location: limit:0
reason: SDK `ContextIndexSearchRequest.Limit` is generated as a non-nullable `int` (NSwag), so an SDK caller who omits `limit` serializes `limit:0`, which the server's `<= 0` guard rejects — contradicting the OpenAPI "defaults to the maximum when omitted". **Pre-existing systemic NSwag pattern**, identical to `FileSearchRequest.Limit`/`WorkspaceFileContextQueryHandler`; 10.5 faithfully mirrors the convention. Spine-wide fix: emit nullable `int?` for optional non-required numerics (preferred, keeps `minimum:1`), or relax the server guard to treat absent-or-zero as default — applied uniformly across the spine, not in 10.5.
status: open
decision: 2026-08-25 Generate nullable limits — Make non-required numeric request properties nullable spine-wide, regenerate clients, and preserve minimum validation for supplied values.

### DW-281: `GetFolderIndexingStatus`'s generated `parity-contract.yaml` row lists `adapter_expectations: [cli, mcp, rest, sdk]` while the op's `transportParity` is `[rest, sdk, mcp]` and the CLI deliberately…

origin: migrated from legacy ledger ("Deferred from: code review of story-10.5 (2026-06-24)"), 2026-08-24
location: parity-oracle-generator/Program.cs
reason: `GetFolderIndexingStatus`'s generated `parity-contract.yaml` row lists `adapter_expectations: [cli, mcp, rest, sdk]` while the op's `transportParity` is `[rest, sdk, mcp]` and the CLI deliberately ships no `indexing-status` subcommand. **Pre-existing generator behavior**: `parity-oracle-generator/Program.cs` `AdapterExpectations()` hardcodes `[rest,sdk,cli,mcp]` for every row (same artifact on the pre-existing rest/sdk-only `GetFolderLifecycleStatus`); non-load-bearing, no test fails. Fix = derive the adapter set from `transportParity` and regenerate spine-wide; do NOT hand-edit the generated row (mutation_rules forbid it).
status: open

### DW-282: Server context-search facade lacks an EventStore-backed bridge read model

origin: migrated from legacy ledger ("Deferred from: code review of story-10.5 (2026-06-24)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:78
reason: **(#2, decision: accept deferral)** The deployed Server facade serves zero items: `AddFoldersContextSearchFacade` registers the live `MemoriesFolderSearchSource` but no Server-side EventStore-backed `ISemanticIndexingBridgeReadModel`, so `AddFoldersContextSearchQueries` leaves the fail-safe `UnavailableSemanticIndexingBridgeReadModel` (empty list) in place and every Memories hit is dropped in hydration. Accepted by Jerome (2026-06-24) as the documented AC15 DCP-lane deferral; AC9-live carved out of acceptance. **Tracked follow-up**: register `EventStoreSemanticIndexingBridgeStore` in `AddFoldersContextSearchFacade` (mirror `FoldersWorkersModule`) and verify the live round-trip once a DCP-capable `aspire run` lane + a populated `folders-index` (from Story 10.4) exist. The honest-status companion fix (indexing-status → `ReadModelUnavailable` instead of false "empty/Current") was applied in this review.
status: open

### DW-283: Direct Memories egress bypasses the Dapr invoke allow-rule

origin: migrated from legacy ledger ("Deferred from: code review of story-10.5 (2026-06-24)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:91; deploy/dapr/production/accesscontrol.yaml
reason: **(#5, decision: accept as documented)** The production `folders → memories GET /api/search` Dapr invoke allow-rule does not bind the facade's actual egress, because `AddMemoriesClient` is a direct base-address `HttpClient` (`Memories:BaseAddress` + bearer token), not a Dapr service-invoke client. Accepted by Jerome (2026-06-24) as already documented in `architecture.md#134`: the allow-rule + its conformance negative-controls are operative only if `Memories:BaseAddress` is configured as the sidecar invoke route; otherwise API-token control governs that egress. Revisit (pin `Memories__BaseAddress` to the sidecar route + add a conformance assertion) if/when the facade egress is moved onto the Dapr sidecar.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:98-113 defaults Memories egress to the Dapr service-invocation route, with direct absolute URLs retained only as an explicit override.

### DW-284: Live Server facade still depends on the unavailable bridge read model

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:78
reason: Live Server facade still depends on the unavailable bridge read model (`src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:78`) — accepted in the story's 2026-06-24 review resolutions as the AC15 DCP-lane/live-round-trip follow-up.
status: open

### DW-285: Dapr invoke allow-rule is conditional because the facade uses a direct Memories base-address client

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:91
reason: Dapr invoke allow-rule is conditional because the facade uses a direct Memories base-address client (`src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:91`, `deploy/dapr/production/accesscontrol.yaml`) — accepted in the story's 2026-06-24 review resolutions and documented in architecture; revisit when egress is pinned to sidecar invocation.
status: done 2026-08-24
resolution: already resolved: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:98-113 now constructs the default Memories endpoint through /v1.0/invoke/memories/method/, binding it to Dapr access control.

### DW-286: Generated `ContextIndexSearchRequest.Limit` is non-nullable despite optional OpenAPI semantics

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:11383
reason: Generated `ContextIndexSearchRequest.Limit` is non-nullable despite optional OpenAPI semantics (`src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:11383`) — pre-existing systemic NSwag optional-numeric pattern already recorded for 10.5; fix spine-wide rather than hand-edit generated code.
status: open

### DW-287: Required test lane still has the pre-existing `.slnx` inventory red

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: _bmad-output/implementation-artifacts/10-5-expose-authorized-folders-query-facade-over-memories.md
reason: Required test lane still has the pre-existing `.slnx` inventory red (`_bmad-output/implementation-artifacts/10-5-expose-authorized-folders-query-facade-over-memories.md`) — pre-existing and documented in the story completion notes; do not treat as caused by the 10.5 facade chunk.
status: done 2026-08-24
resolution: already resolved: Commit 9631820 updated solution inventory expectations; the current Hexalith.Folders.slnx project set and ScaffoldContractTests expected set both contain 50 projects.

### DW-288: Harden the canonical submodule command contract to parse and compare the exact nonrecursive command token set.

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
source_spec: _bmad-output/implementation-artifacts/spec-run-tests-and-fix-failures.md
reason: The pre-existing substring-based assertion in `ScaffoldContractTests.AssertCanonicalInitCommandPresent` can accept comments, recursive commands, or extra nested paths that merely contain the required substrings.
status: done 2026-08-24
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/ScaffoldContractTests.cs:528-558 verifies unsafe recursive/comment variants are rejected, backed by the canonical token parser at :755 onward.

### DW-289: Reject contradictory explicit Hexalith dependency-mode switches during MSBuild evaluation.

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: Directory.Build.props
source_spec: _bmad-output/implementation-artifacts/spec-run-tests-and-fix-failures.md
reason: The pre-existing configuration permits `UseHexalithProjectReferences=true` and `UseNuGetDeps=true` simultaneously, allowing different dependency projects to select inconsistent graphs.
status: open

### DW-290: Missing Tier-3 real-materializer proof (AC10).

origin: migrated from legacy ledger ("Deferred from: code review of 10-6-replace-fail-closed-content-materializer-with-metadata-derived (2026-07-14)"), 2026-08-24
location: tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:79-160
reason: **Missing Tier-3 real-materializer proof (AC10).** Deferred by Administrator: "Defer AC10 and keep Story 10.6 in progress until its prerequisites land." The existing opt-in mutation smoke publishes an envelope without asserting an indexed outcome, while the index round-trip seeds `SearchIndexEntryChanged` directly and bypasses the materializer (`tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:79-160`).
status: open

### DW-291: EventStore write side emits no accepted folder-mutation events for the worker subscription.

origin: migrated from legacy ledger ("Deferred from: code review of 10-6-replace-fail-closed-content-materializer-with-metadata-derived (2026-07-14)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs:201-205; src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1257-1276,1340
reason: **EventStore write side emits no accepted folder-mutation events for the worker subscription.** `WorkspaceFileMutationService` appends aggregate events to `IFolderRepository` (`src/Hexalith.Folders/Aggregates/Folder/WorkspaceFileMutationService.cs:201-205`), but `FolderDomainProcessor.ToDomainResult` maps every accepted result to `PayloadNoOpDomainResult`, whose EventStore event list is empty (`src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1257-1276,1340`). Consequently EventStore cannot publish `WorkspaceFileMutationAccepted` to `folders.events`; Story 10.6's materializer is unreachable from genuine accepted mutations until the durable write-side/data-plane work lands.
status: open

### DW-292: Production effective-permissions read model is never populated, so the indexing policy gate denies delivered mutations.

origin: migrated from legacy ledger ("Deferred from: code review of 10-6-replace-fail-closed-content-materializer-with-metadata-derived (2026-07-14)"), 2026-08-24
location: src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:247
reason: **Production effective-permissions read model is never populated, so the indexing policy gate denies delivered mutations.** `AddFoldersLayeredAuthorization` registers a fresh `InMemoryEffectivePermissionsReadModel` (`src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:247`), while production has no caller of its `Save` method. `FailClosedSemanticIndexingPolicyEvaluator` therefore receives `NotFoundSafe` from the permission provider and returns `folder_acl_denied` before invoking the content materializer. This is pre-existing authorization-projection/data-plane work, not introduced by Story 10.6.
status: open
decision: 2026-08-25 Create prerequisite story — Assign a durable projection owner before Story 10.8 and implement EventStore-backed population, replay, Server registration, restart, tenant-isolation, and failure evidence.

### DW-293: No test verifies that the new complete upsert attribute set keeps the archive soft-delete re-send off its legacy identity-reconstruction branch.

origin: migrated from legacy ledger ("Deferred from: code review of 10-6-replace-fail-closed-content-materializer-with-metadata-derived (2026-07-14)"), 2026-08-24
location: src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs:201-215
source_spec: _bmad-output/implementation-artifacts/10-6-replace-fail-closed-content-materializer-with-metadata-derived.md
reason: The story's Dev Notes assert that emitting all five identity keys means `BuildArchivedAttributes` (`src/Hexalith.Folders.Workers/SemanticIndexing/MemoriesSemanticIndexingPort.cs:201-215`) clones the preserved `IndexedAttributes` and only flips `folders.status` to `archived`, rather than taking its identity-reconstruction fallback. That claim is prose only — no test in this story drives a real metadata-derived upsert followed by an archive re-send. The archive egress is out of scope #3 for Story 10.6, so the interaction is surfaced here rather than fixed.
status: open

### DW-294: Forgejo silently ignores the now-required provider idempotency admission.

origin: code review of spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs
source_spec: _bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: Story 3.10 added `ProviderIdempotencyAdmission` to `ProviderRepositoryCreationRequest` and `ProviderRepositoryBindingRequest`, and `GitHubProvider` now gates on it. `ForgejoProvider` accepts the member and enforces nothing (`grep IdempotencyAdmission src/Hexalith.Folders/Providers/Forgejo/` returns no hits), so once a caller emits a real disposition a replayed or expired intent would execute live against Forgejo. This is not a regression — Forgejo enforced no idempotency before either — and Story 3.12 owns the Forgejo slice. No test pins the deliberate no-op today.
status: done 2026-08-28
resolution: already resolved: Commit 485fcf38; src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:282-288 and :775-864 enforce creation and binding idempotency admission, with expiry and replay coverage at tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs:704-954.

### DW-295: The repository provisioning admission seam fails open and is never checked against the requesting intent.

origin: code review of spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs:79-84
source_spec: _bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: `RepositoryProvisioningContext.IdempotencyAdmission` is optional and defaults to `Fresh`, so a future construction site that forgets to populate it degrades the new gate to "execute as new work" with no test failing. Separately, `ContextMatchesRequested` validates tenant, organization, and binding ref but never checks that `context.IdempotencyAdmission.IntentFingerprint` equals `requested.IdempotencyFingerprint`, so once Story 12.6 supplies real admissions a mismatched one would be honoured. Both become live hazards only when 12.6 wires the durable producer; the optional default is deliberate for now because the folder ledger still owns dedup.
amended (2026-08-25, code review of story 3.10): the worker is only half the gap. `RepositoryBindingService` (`src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs:190-195`) hard-codes `ProviderIdempotencyDisposition.Fresh` with **no injection seam at all** — not an optional context field that could be populated, but a literal. It is the only production caller that reaches the binding gate today. Consequence: `idempotency_key_expired`, the behaviour that HALTed Story 3.10 twice, is unreachable on every production path, so AC7 is proven by boundary unit tests only. Story 12.6 must add the seam here as well as populate the worker's context.
status: open

### DW-296: Idempotency conflict and expiry from the provider map to a repository conflict at the API boundary.

origin: code review of spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs (MapProvider)
source_spec: _bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: `RepositoryBindingService.MapProvider` folds `ProviderFailureCategory.ProviderConflict` into `FolderResultCode.RepositoryConflict`, so a real `idempotency_conflict` / `idempotency_key_expired` from the new gate would surface as a repository conflict even though `FolderResultCode.IdempotencyConflict` and the canonical `idempotency_conflict` string already exist. Likewise `ProviderValidationFailed` falls into the default arm and reports `ProviderReadinessFailed`. Unreachable today because the only production caller hard-codes `Fresh`; must be resolved with Story 12.6.
status: open

### DW-297: An equivalent-replay bind is indistinguishable from a fresh bind in the event stream.

origin: code review of spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs (MapProviderResult); src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs (BuildOutcomeEvents)
source_spec: _bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: Both outcome mappers branch on `IsSuccess` only and discard `EquivalentExisting`, so an `existing_equivalent` replay appends a plain `RepositoryBound`. Nothing durable records that no provider work happened, which weakens later reconciliation and audit evidence. Pre-existing shape, but Story 3.10 makes replay reachable through the provider gate.
status: open
decision: 2026-08-28 Add replay outcome event — Introduce an additive replay outcome event with bounded prior evidence and update projections, audit mapping, serialization, and replay tests.
decision: 2026-08-28 Add replay outcome event — Introduce an additive replay outcome event with bounded prior evidence and update projections, audit mapping, serialization, and replay tests.

### DW-298: Four Story 3.11 test failures were unmasked by the Story 3.10 compile repair and have no owner.

origin: code review of spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs; tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.cs
source_spec: _bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: `tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.cs:623` carried a `CS8122` that made the whole test project unbuildable, so no Folders test had run since Story 3.11 landed at `a69dd84`. Repairing it exposed four genuine pre-existing failures: `GitHubDependencyGuardTests.ProviderReadinessCompositionResolvesGitHubAndForgejoExactlyOnce` (unresolvable `ILogger<FolderTelemetryEmitter>`), `OctokitGitHubApiClientTests.MutationStatusTransportFailuresUseOneReadAndNoMutation(malformed)`, `OctokitGitHubApiClientTests.ExplicitCommitRejectsMalformedCreatedCommitBeforeRefMovement(uppercase-sha)`, and `OctokitGitHubApiClientTests.MutationStatusRejectsEqualOrNonCanonicalExpectedShasWithoutObservation`. Attribution proven: reverting every Story 3.10 change and keeping only the one-line repair reproduces the same four failures. They belong to the Story 3.11 slice, whose spec reads `in-review` while `sprint-status.yaml:105` still reads `backlog`.
diagnosis (2026-08-25): three are test defects, one is a production defect.
  1. `ProviderReadinessCompositionResolvesGitHubAndForgejoExactlyOnce` — TEST. Builds a bare `ServiceCollection` with `ValidateOnBuild = true` but never calls `AddLogging()`, while `AddFoldersProviderReadiness` -> `AddFoldersObservability` has registered `FolderTelemetryEmitter(IEnumerable<IFolderAuditObserver>, ILogger<FolderTelemetryEmitter>)` since Story 4.14. Production hosts supply logging, so only this container fails. Fix: add `services.AddLogging();` to the test.
  2. `ExplicitCommitRejectsMalformedCreatedCommitBeforeRefMovement(uppercase-sha)` — TEST. The scenario builds its SHA as `CommitSha.ToUpperInvariant()`, but `CommitSha` is `"3333...3333"` (digits only), so uppercasing is a no-op and the response carries a perfectly canonical SHA. The commit therefore validates and the ref update correctly proceeds, giving 3 requests instead of the asserted 2. Production `TryGitSha` does reject `A-F`. Fix: use a constant containing hex letters (e.g. `"cccc..."`) so `ToUpperInvariant()` actually produces a non-canonical SHA.
  3. `MutationStatusRejectsEqualOrNonCanonicalExpectedShasWithoutObservation` — TEST. Same digits-only `ToUpperInvariant()` no-op as (2); it is the `uppercase` assertion that fails, not `equal`. Same fix.
  4. `MutationStatusTransportFailuresUseOneReadAndNoMutation(malformed)` — PRODUCTION. `IsMalformedJsonException` (`src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs:907`) detects malformed bodies by testing whether the exception type NAME contains `"Json"`. Octokit 14.0.0 throws `Octokit.SerializationException` from its bundled SimpleJson deserializer, which does not match, so the mapper falls through to `UnexpectedTransportFailure` instead of `MalformedResponse`. Confirmed by inspecting the pinned `Octokit.dll`, which declares `SerializationException` and no `*JsonException`. Observation paths misclassify; mutation paths reach `AmbiguousMutationResponse` either way, so mutation safety is unaffected. Fix: match `SerializationException` as well as the name heuristic.
resolution (2026-08-25): all four fixed at the user's explicit request, outside Story 3.10's original scope.
  - `GitHubDependencyGuardTests` now calls `services.AddLogging()` before the ValidateOnBuild container is built.
  - `OctokitGitHubApiClientTests.CommitSha` changed to `"33333333333333333333333333333333333333cc"` so the two non-canonical-SHA scenarios actually produce an uppercase SHA; no production change was needed for (2) or (3).
  - `IsMalformedJsonException` now matches `System.Runtime.Serialization.SerializationException` in addition to the type-name heuristic. Diagnosis confirmed empirically: the malformed case goes green with that single change.
  - `Hexalith.Folders.Tests` is 1535/1535 for the first time since Story 3.11 landed at `a69dd84`. Story 3.11's review should still confirm it accepts these edits to its own slice.
status: resolved

### DW-299: Four near-identical ReplayOrReject gates can drift apart.

origin: code review of spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs; src/Hexalith.Folders/Providers/GitHub/GitHubProvider.Mutations.cs
source_spec: _bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: There are now four admission gates differing only in result type — two for creation/binding and two for change-set/commit. They already drifted once: the Story 3.10 pair initially shipped without the malformed-admission boundary checks its Story 3.11 sibling performs (fixed in review). A single helper returning `(ProviderFailureCategory, string)?`, the shape `ValidateBoundary` in `Mutations.cs` already uses, would make divergence impossible. Refactoring the Story 3.11 gates is outside Story 3.10's authority.
amended (2026-08-25, code review of story 3.10): the drift was not merely a risk — it had already produced a contradiction. Story 3.10 validated `PriorReconciliationReference` with `IsSafeFingerprint` (exactly 64 lowercase hex) while Story 3.11 validated the same field with `IsSafeOpaqueReference` (<=128 chars, `-_.:` allowed, ULID-shaped), so one durable admission from Story 12.6 could not have satisfied both gates. In the other direction the Story 3.10 gate was the weaker one: it required neither `PriorOutcomeDisposition` nor `PriorOperationReference`, and never allow-listed the prior reason or remediation codes.
resolution (2026-08-25): closed on the Story 3.10 side. `IsReplayEvidenceWellFormed` in `GitHubProvider.cs` now applies the same per-disposition rules as `IsOperationAdmissionWellFormed`, and both call the shared `IsSafeOpaqueReference` / `IsSafeFingerprint` / `SafeRetryAfter` / `AllowedOperationRemediationCodes` members of the same partial class. Reason codes are checked against the Story 3.11 allow-list first, with a repository-scoped set for the create/bind additions. The remaining item is the structural one: four gates still exist and a single shared helper would make future divergence impossible. That refactor still needs Story 3.11's authority.
status: open

### DW-300: `IsMalformedJsonException` cites a nonexistent Octokit type, keeps a name-substring heuristic, and ships untested.

origin: code review of 3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs:907
source_spec: _bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: Follow-up to DW-298's production fix. Three residual problems. (1) The explanatory comment asserts that "Octokit 14.0.0 surfaces an unparseable response body as `Octokit.SerializationException`", but that type does not exist in the pinned package — verified against `~/.nuget/packages/octokit/14.0.0/lib/netstandard2.0/`, whose `Octokit.xml` contains zero occurrences of `SerializationException` while the DLL string heap carries the BCL `System.Runtime.Serialization.SerializationException`. The code is correct; the load-bearing rationale a future maintainer would trust points at a type that isn't there. (2) The guard remains half a type-name substring test (`Name.Contains("Json")`), which will also swallow unrelated `System.Text.Json` exceptions thrown by our own code and report a programming error as a provider malformed-response. (3) Commit `19f40f2` changed production classification with no test exercising the new `SerializationException` branch. This is the Story 3.11 failure matrix, which Story 3.10's Dev Notes place under "Do Not Touch".
status: open

### DW-301: `AddLogging()` in the dependency guard test masks a real composition gap.

origin: code review of 3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs
source_spec: _bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: DW-298 diagnosed `ProviderReadinessCompositionResolvesGitHubAndForgejoExactlyOnce` as a test defect and fixed it by adding `services.AddLogging()` to the bare `ValidateOnBuild` container. That is a defensible reading, but it converts a genuine signal — `AddFoldersProviderReadiness()` is not self-contained, because `AddFoldersObservability` registers `FolderTelemetryEmitter(IEnumerable<IFolderAuditObserver>, ILogger<FolderTelemetryEmitter>)` — into a green test by mutating the test rather than the extension. Nothing now asserts what the composition root must supply, and any host calling the extension without logging registered still fails at resolution. Either the extension should call `AddLogging()` itself, or a test should pin the prerequisite explicitly.
status: open
decision: 2026-08-28 Register logging — Call AddLogging from readiness or observability composition and restore a bare-container ValidateOnBuild regression test.
decision: 2026-08-28 Register logging — Call AddLogging from readiness or observability composition and restore a bare-container ValidateOnBuild regression test.

### DW-302: An archived or disabled GitHub repository binds successfully.

origin: code review of 3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs:160-200
source_spec: _bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: `ValidateRepositoryBindingAsync` checks access, canonical identity, permission posture, default branch, selected ref, and protection posture, but never inspects `Repository.Archived` or the disabled flag. A binding to an archived repository validates cleanly and then fails on every subsequent write. AC4 enumerates the checks it requires and does not name archival, so this is a gap in the acceptance criteria rather than a deviation from it — it needs a product decision before it becomes a required check.
status: open
decision: 2026-08-28 Reject inactive repositories — Reject archived and disabled repositories before branch observation, map a bounded outcome, and add no-write binding tests.
decision: 2026-08-28 Reject inactive repositories — Reject archived and disabled repositories before branch observation, map a bounded outcome, and add no-write binding tests.

### DW-303: No bounded per-request deadline on the GitHub transport.

origin: code review of 3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClientFactory.cs
source_spec: _bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: `GitHubApiVersionHttpClient.SetRequestTimeout` delegates to the inner client, but the factory never calls it, so no story-owned deadline is established and Octokit's default applies. Combined with Octokit 14's high-level repository/branch methods accepting no `CancellationToken`, a hung GitHub response blocks the calling worker for that default. The `TimeoutDuringMutation` / `TimeoutDuringObservation` conditions were added specifically to model timeouts, so the taxonomy is ready but the bound is not chosen. Picking the value is a policy decision, not a mechanical fix.
status: open
decision: 2026-08-28 Configurable default — Add validated timeout options with a 30-second default and safe bounds, apply them in the factory, and test overrides.
decision: 2026-08-28 Configurable default — Add validated timeout options with a 30-second default and safe bounds, apply them in the factory, and test overrides.

### DW-304: Sibling digits-only SHA constants carry the `ToUpperInvariant()` vacuity hazard DW-298 fixed once.

origin: code review of 3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs
source_spec: _bmad-output/implementation-artifacts/3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: DW-298 changed `OctokitGitHubApiClientTests.CommitSha` from digits-only to `"...cc"` because the non-canonical-SHA scenarios uppercase it, and `ToUpperInvariant()` on a digits-only string is a no-op that silently vacates the assertion. The sibling constants `StagedTreeSha = "2222..."` and `PriorOutcomeFingerprint = "1111..."` are still digits-only, and no helper or assertion (e.g. `value.ToUpperInvariant().ShouldNotBe(value)`) prevents a future test from reintroducing the same vacuity. Story 3.11's slice.
status: open

### DW-305: ArchiveFolderProcessWiringTests is not selected by a blocking CI lane.
origin: spec-deferred 22f7160f19de
location: tests/tools/run-contract-parity-ci-gates.ps1
source_spec: `spec-archive-process-integration.md`
severity: medium
reason: The integration class was already excluded before this bundle: the blocking parity script selects GoldenLifecycleParityTests, CrossAdapterBehavioralParityTests, and MixedSurfaceHandoffTests, while the baseline allow-list omits the IntegrationTests project. The four new archive-process rows therefore run locally but inherit the pre-existing CI selection gap.
status: open

### DW-306: The shared gateway double's new `/process` result-payload propagation is asserted by no gate-selected test.
origin: spec-deferred 2143e3ee7943
location: tests/Hexalith.Folders.IntegrationTests/MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs
source_spec: `spec-archive-process-integration.md`
severity: medium
reason: `InProcessRejectionPropagatingGatewayClient` now forwards `ResultPayload`, which is the sole input to the REST `idempotentReplay` field. The only assertion on it lives in `ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted`, in the class no blocking lane selects. `MixedSurfaceHandoffTests` is gate-selected and drives the same double through a four-surface replay, but asserts 202 / no conflict / no second append without ever reading `idempotentReplay`. Reverting the propagation would leave every gate green while every replay silently reported `idempotentReplay: false`. Closing this means adding an assertion to `MixedSurfaceHandoffTests`, which is outside this intent's "add four archive tests" approach.
status: open

### DW-307: Only the `Denied` archive-policy outcome has an integration row; the retryable outcomes have none at the `/process` boundary.
origin: spec-deferred 3563da68a6d4
location: tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs
source_spec: `spec-archive-process-integration.md`
severity: medium
reason: `FolderArchivePolicyOutcome` also carries `ScopeMismatch`, `Unavailable`, `Malformed`, and `Stale`, and `EvaluatePolicy` additionally returns `PolicyEvidenceMalformed` when an *Allowed* evidence's tenant/organization/folder do not match the command or its `PolicyVersion` is blank -- the load-bearing anti-forgery check. Those codes are asserted only in `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveAuthorizationGateTests.cs`, never across REST -> gateway -> `/process`. `Unavailable`/`Stale` map to a retryable 503, a materially different caller contract than the non-retryable 403 this bundle pins, so a regression that collapsed one into the other would keep every row green. Adding those rows is outside this intent's four-scenario matrix.
status: open

### DW-308: `HasScopedMetadataProperty` rejects any property name containing `policy`, which production's own richer safe-denial body carries.
origin: spec-deferred 2aadb3b7b785
location: tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs
source_spec: `spec-archive-process-integration.md`
severity: medium
reason: `FolderAuthorizationDenialMapper.ToHttpResult` emits `details.policyClass` (alongside `reasonCategory`, `layer`, `freshnessClass`, `timingBucket`) on every layered-authorization denial. The helper passes today only because the gateway double flattens the `/process` 403 into an `EventStoreGatewayException`, so the caller sees `ToArchiveGatewayProblem`'s leaner `SafeProblem` body instead. Any future change that propagates the richer -- and still metadata-only -- denial body to the caller would red these rows on a body that discloses nothing. Closing this means matching exact field names, or exempting the known-safe classifier fields, rather than substring-matching property names.
status: open

### DW-309: Follow-up review still recommended for dw-archive-process-integration after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-archive-process-integration.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260828-001531-d5cf; this entry preserves the lingering recommendation for a deliberate later review.
status: open

### DW-310: The pre-existing Int32 EventsAppended test affordance can wrap after more than Int32.MaxValue appended events.
origin: spec-deferred eacd388efd79
location: src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:152
source_spec: `spec-inmemory-repository-lock-cleanup.md`
severity: low
reason: Before this change the int auto-property used unchecked +=, and the refactor preserves that behavior in the gate-protected backing field. This is not caused by the synchronization cleanup and is not observable through IFolderRepository, but an extreme-lifetime test repository could report a negative diagnostic count.
status: open

### DW-311: A single per-folder `_lastObservedAt` watermark is shared by all six read-model snapshot writers, so one projection's newer timestamp can advance another projection's reported freshness.
origin: spec-deferred 94fcd8bf7ae7
location: src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:255
source_spec: `spec-inmemory-repository-lock-cleanup.md`
severity: medium
reason: Every `Save*Snapshot` clamps against the same `LifecycleKey(managedTenantId, folderId)` entry while sourcing a different candidate time (`state.ArchivedAt`, `policy.ConfiguredAt`, `state.WorkspaceLifecycleUpdatedAt`, or the append's `observedAt`). The clamp is a max, so whichever writer runs with the newest candidate pins `ObservedAt` for every other projection of the same folder. This predates the change -- the same key and the same max semantics were previously expressed through `ConcurrentDictionary.AddOrUpdate` -- and this story only relocated the operation under the gate. It is invisible in the current suite because the new coverage constructs the repository with no read models and `BranchRefPolicyReadModelTests` registers only the branch-policy read model; registering a second read model would surface the interference.
status: open

### DW-312: The sibling `InMemoryOrganizationProviderBindingRepository` still mixes `ConcurrentDictionary` with `lock (_gate)` and publishes state before its idempotency ledger entry where readers can observe the
origin: spec-deferred 49c81b636fb6
location: src/Hexalith.Folders/Aggregates/Organization/InMemoryOrganizationProviderBindingRepository.cs:61
source_spec: `spec-inmemory-repository-lock-cleanup.md`
severity: medium
reason: Its file comment states it "mirrors the folder repository policy", yet `Load` (line 25) and `TryGetIdempotencyFingerprint` (line 69) never take `_gate`, while `AppendIfFingerprintAbsent` writes `_states[...]` at line 61 and `_idempotencyFingerprints[ledgerKey]` at line 62. A concurrent reader can therefore see appended organization state whose fingerprint is not yet visible, and a caller retrying the same idempotent command would treat it as new work. The only tests touching the type run single-threaded, so nothing observes the window. This story's intent scopes sibling `ConcurrentDictionary` users out ("Never: modify submodules or unrelated `ConcurrentDictionary` users"), so it is recorded here rather than fixed.
status: open

### DW-313: The sibling `InMemoryOrganizationProviderBindingRepository` file comment claims it "mirrors the folder repository policy", a claim this change made false.
origin: spec-deferred c9d27f6533b9
location: src/Hexalith.Folders/Aggregates/Organization/InMemoryOrganizationProviderBindingRepository.cs:6
source_spec: `spec-inmemory-repository-lock-cleanup.md`
severity: low
reason: Lines 6-7 of that file describe it as mirroring the folder repository's synchronization policy. After this story, `InMemoryFolderRepository` owns all mutable state under one gate with plain `Dictionary` stores, while the sibling still mixes `ConcurrentDictionary` with `lock (_gate)`, leaves `Load` and `TryGetIdempotencyFingerprint` un-gated, keeps `EventsAppended` as an un-gated auto-property, and calls its own public `Load` from inside the gate. The comment now points a reader at a policy the file does not implement. It cannot be corrected here: this story's intent scopes that file out ("Never: modify submodules or unrelated `ConcurrentDictionary` users"), which covers a comment edit in it as much as a code edit. Distinct from the synchronization gap already recorded for the same file: that entry is about the publication window, this one is about the documentation now misdescribing the type.
status: open

### DW-314: FolderCommandRejected.Create accepts noncanonical numeric and whitespace-padded FolderResultCode strings because its whitelist uses permissive Enum.TryParse semantics.
origin: spec-deferred ad076c3a4bd1
location: src/Hexalith.Folders.Server/FolderCommandRejected.cs:97
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: medium
reason: The production factory calls Enum.TryParse<FolderResultCode>(code, out _) without an exact ordinal round-trip or Enum.IsDefined check, despite its strict-whitelist comment. The parity gateway now defends its own wire boundary, but changing the production event factory is outside DW-15.
status: open

### DW-315: Rejection conversion assumes DomainServiceWireResult contains exactly one event and throws when a rejected result contains zero or multiple events.
origin: spec-deferred 3d92cf7432ed
location: tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs:134
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: medium
reason: ToGatewayException still selects result.Events.Single(), while the wire result contract represents Events as a collection. Defining multi-event rejection semantics is broader than replacing the duplicated canonical mapping requested by DW-15.
status: open
decision: 2026-09-02 Fail closed — Validate exactly one rejection event and convert zero or multiple events into a bounded MalformedEvidence gateway failure, with focused tests.

### DW-316: The Hexalith.Folders.IntegrationTests project has no unfiltered CI lane, so most of its classes -- including this story's gateway-boundary suite and the pre-existing ArchiveFolderProcessWiringTests --
origin: spec-deferred 0135b9f76686
location: tests/tools/run-contract-parity-ci-gates.ps1:71-94
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: medium
reason: tests/tools/run-contract-parity-ci-gates.ps1 reaches this project only through two `--filter` expressions naming GoldenLifecycleParityTests, CrossAdapterBehavioralParityTests, and MixedSurfaceHandoffTests; run-baseline-ci-gates.ps1 enumerates nine test projects and does not list this one. Reverting the canonical mapping fails 33 assertions locally while every CI lane stays green. Registering classes changes a CI contract that ContractParityCiWorkflowConformanceTests pins, which is broader than DW-15.
status: open

### DW-317: Roughly twenty result codes now reach a different caller-visible REST status through ToArchiveGatewayProblem, and no test drives the REST leg for any of them.
origin: spec-deferred 7434a398b12a
location: src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3909
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: medium
reason: FolderCanonicalErrorMapper emits categories that SafeGatewayReasonCode does not whitelist (state_transition_invalid, validation_error, not_found, policy_denied, already_archived, authentication_failure, repository_conflict), so those rejections fall through to the status-only default arm; StateTransitionInvalid reaches 422 at the gateway exception but still renders 403 denied_safe at REST. This story's acceptance criteria and I/O matrix are all stated at the gateway exception boundary, so cross-surface REST coverage is outside its scope. The four codes existing suites actually drive are unchanged end-to-end (IntegrationTests 667/667 green).
status: open

### DW-318: The parity double's rejection reason code matches neither spelling the real EventStore gateway produces, so its "production fidelity" claim is unverified against the actual gateway hop.
origin: spec-deferred 7cb3e93d48f6
location: references/Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/DomainCommandRejectedExceptionHandler.cs:43
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: medium
reason: In production a Folders rejection reaches EventStoreGatewayException through DomainCommandRejectedExceptionHandler, which derives ProblemDetails reasonCode from DomainRejectionProblemCatalog.FromRejectionType(rejection.RejectionType) -- a kebab-case name derived from the rejection EVENT TYPE (folder-command-rejected), with a status drawn from a small set. The FolderResultCode never reaches the gateway exception in production at all. The double previously emitted the PascalCase enum name and now emits the snake_case canonical category; neither is the production wire spelling. Reconciling the two vocabularies would change the EventStore submodule or the caller-visible canonical error vocabulary, both of which this story's Block If and Never lists forbid.
status: done 2026-09-02
resolution: closed by human decision: Treat the parity double as adapter-specific, remove its production-fidelity claim, and leave the production vocabulary unchanged.
decision: 2026-09-02 Document simulator — Treat the parity double as adapter-specific, remove its production-fidelity claim, and leave the production vocabulary unchanged.

### DW-319: FolderCanonicalErrorMapper.CategoryFor and StatusFor have no production caller, so the table this story adopts as canonical is documentation rather than the mapping that runs.
origin: spec-deferred bc225eef2edc
location: src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs:9
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: medium
reason: grep over src/ finds exactly one call into the mapper -- FoldersDomainServiceEndpoints.cs:5733 calls ClientActionFor. CategoryFor and StatusFor are called only by their own unit test and, after this change, by the parity double. FoldersDomainServiceEndpoints never references FolderResultCode; its caller-visible surface is keyed on SafeGatewayReasonCode instead. Wiring the mapper into the endpoint path would change caller-visible canonical error vocabulary, which this story's Never list forbids.
status: open
decision: 2026-09-02 Relocate test mapping — Move CategoryFor and StatusFor to explicit parity or test support while retaining ClientActionFor as the only production mapping.

### DW-320: Follow-up review still recommended for dw-archive-gateway-canonical-mapping after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260828-230804-913d; this entry preserves the lingering recommendation for a deliberate later review.
status: open

### DW-321: Corpus loading (`RepositoryRoot()` + parse of `tests/fixtures/audit-leakage-corpus.json`) is copy-pasted across at least nine test classes instead of one shared labelled reader.
origin: spec-deferred a56b1f5a32a1
location: tests/ (nine call sites); candidate home: src/Hexalith.Folders.Testing or tests/shared
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: medium
reason: The same discovery-and-parse block appears in FolderArchiveMetadataLeakageTests, FolderWorkspaceFileMutationAggregateTests, FolderWorkspaceCommitAggregateTests, WorkspaceLifecycleProjectionDeterminismTests, FolderAuditObservationTests, AuditEndpointsTests, AuditEndpointsSentinelTests, WorkspaceStatusEndpointTests and MemoriesFolderSearchSourceTests. Each copy is free to drift in labelling and in blank-value filtering, so "every sentinel is covered" cannot be checked centrally. Pre-existing; this story adds another consumer.
status: open

### DW-322: The archive surface map uses an invented channel vocabulary that never meets the declared channel inventory, so archive-path channel coverage cannot be measured by any gate.
origin: spec-deferred 20ac1ac18ee0
location: tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: medium
reason: FolderArchiveMetadataLeakageTests keys its surfaces `event`, `audit-record`, `projection`, `problem-details`, `log-template`, `trace-tags`, `generated-client-exception`, while tests/fixtures/audit-leakage-corpus.json (`forbidden_output_surfaces`) and tests/fixtures/safety-channel-inventory.json (`channels`) declare `events`, `audit-records`, `projections`, `problem-details-examples`, `logs`, `traces`, `generated-sdk` and ~18 more. Only 8 of 25 declared channels are swept on the archive path and nothing pins the shortfall. Pre-existing shape of the original [Fact]; not introduced by this story.
status: open

### DW-323: The per-sample `forbidden_output_surfaces` scoping in the corpus is ignored; every sentinel is asserted against every surface, encoding a stricter contract than the fixture's own policy.
origin: spec-deferred 7c040f72a129
location: tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: low
reason: `correlation-metadata` deliberately omits `events`/`projections` from its forbidden list (with the note that production policies decide where correlation may remain visible), and `safe-provenance-operation-id` forbids only `provider-diagnostics`. The tests also ignore `participates_in` and `classification`. The suite passes only because the archive surfaces are built from safe defaults. Pre-existing.
status: open

### DW-324: `AcceptedArchiveEventShouldCarryOnlyMetadataEvidence` hand-joins 8 of the 10 `FolderArchived` members, so a newly added property escapes that assertion silently.
origin: spec-deferred dde60fc80c0f
location: tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: low
reason: The [Fact] builds its subject with an explicit '|'-join that omits `IdempotencyFingerprint` and `OccurredAt`, unlike the corpus theories which serialize the whole record. Pre-existing and untouched by this story.
status: open

### DW-325: A trailing bare LF defeats every canonical-identifier gate in the repository, because .NET's `$` matches before a final newline unless `RegexOptions.Multiline` is set.
origin: spec-deferred 97e78371a3e9
location: src/Hexalith.Folders.Server/FolderCommandRejected.cs:30 and src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:952
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: high
reason: Verified: `^[a-z0-9._-]+$` accepts "safe-identifier\n" (it rejects "safe-identifier\r\n" and "safe\nidentifier"). So `FolderCommandRejected.CanonicalIdentifierOrNull("safe-identifier\n")` returns the value with the newline intact, and `FolderCommandValidator.IsSafeEvidenceIdentifier` / `FolderStreamName.IsValidSegment` accept a trailing newline too. That is a live log-injection vector on exactly the surfaces these canonical gates exist to protect. Pre-existing and out of scope here: the Intent's Block-If forbids changing production canonicalization. Fix shape: anchor with `\A...\z` instead of `^...$`.
status: open

### DW-326: Sibling corpus sweeps still assert leakage with value-printing `ShouldNotContain`, the exact idiom these two files ban, and they are the sites that drive sentinels through production.
origin: spec-deferred 3be07b46afe5
location: tests/Hexalith.Folders.Server.Tests/MemoriesFolderSearchSourceTests.cs:84
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: medium
reason: `MemoriesFolderSearchSourceTests.ShouldDropEveryLeakageCorpusSnippetFromResults:84` and `WorkspaceStatusEndpointTests:271` use bare `ShouldNotContain(sentinel)`, which renders both the sentinel and the actual payload into the assertion-messages channel that audit-leakage-corpus.json declares forbidden. They also use a raw substring scan, so a future corpus sample containing any character `JavaScriptEncoder.Default` escapes would serialize as `\uXXXX` and read as clean. Today's corpus is entirely plain ASCII, so the gap is latent rather than live. `AuditEndpointsSentinelTests:76` already truncates for this reason; the repo is inconsistent.
status: open

### DW-327: Two semantically incompatible leakage detectors now exist in the repository, and nothing pins which one is authoritative.
origin: spec-deferred 45155f26ae3f
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: medium
reason: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` scans `OrdinalIgnoreCase` with a token-boundary rule, driven by each sample's declared `forbidden_output_surfaces` plus safety-channel-inventory.json. The detector added by this story is `Ordinal` with no token boundary but adds a JSON-decoded walk -- weaker against case mutation, stronger against `\uXXXX` escaping. Neither subsumes the other and no test records the divergence.
status: open

### DW-328: The accepted-archive-surface sweep is structurally incapable of failing; its 18 corpus rows scan constant payloads that no sentinel can ever reach.
origin: spec-deferred c062d48a285d
location: tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: medium
reason: `AcceptedArchiveSurfaces()` builds all eight channel strings from `FolderCommandFactory.Archive()` safe defaults, and every corpus sentinel is rejected by `IsSafeEvidenceIdentifier`, so no corpus value can reach an accepted archive surface. Demonstrated: replacing all eight surface values with `string.Empty` still passes 18/18. Pre-existing (the original [Fact] had the same property) and intent-mandated -- the Intent's matrix row 3 and its "preserve existing accepted-surface checks" clause specify exactly this shape, so it was not in scope to change. Real coverage on this path would need a hostile-but-accepted caller value threaded into the archive command.
status: open

### DW-329: The entire `tests/Hexalith.Folders.Server.Tests` project runs in no CI gate lane, so this story's rejection-event regression trap gates nothing in CI.
origin: spec-deferred da328fd3ef5d
location: tests/tools/run-baseline-ci-gates.ps1 and tests/Hexalith.Folders.Server.Tests
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: high
reason: Verified: the project appears in no `$unitTestProjects` list across `tests/tools/*.ps1` and in no `.github/workflows/*.yml`; its only cross-project references are the `ScaffoldContractTests` assertions that it exists. 644 tests -- including the sibling corpus sweeps in `AuditEndpointsSentinelTests`, `WorkspaceStatusEndpointTests` and `MemoriesFolderSearchSourceTests` -- therefore gate nothing. Demonstrated: the mandated `SafeIdentifierRegex` widening turns 13 rows red locally while CI stays green. Pre-existing and far wider than this story, which merely inherited the project as its home (`ScaffoldContractTests` forbids the alternative). Attempted in this pass and reverted: enrolment needs a four-file lockstep -- `run-baseline-ci-gates.ps1`, `BaselineCiWorkflowConformanceTests._baselineUnitProjects`, `docs/operations/baseline-ci-gates.md` and the generated `_bmad-output/gates/baseline-ci/latest.json` (pinned by `BaselineGateReportShouldStayMetadataOnlyWhenPresent`) -- and an honest rege
status: open

### DW-330: The blocking baseline CI lane is red on `main` today: `dotnet format whitespace --verify-no-changes` fails with 13 errors on one untouched production file.
origin: spec-deferred ce17c1f3fb7c
location: src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs:22
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: high
reason: Reproduced on a clean tree at this story's baseline: `dotnet format whitespace Hexalith.Folders.slnx --verify-no-changes --no-restore --include ./src/ ./tests/ ./samples/` reports 13 `error WHITESPACE: Fix whitespace formatting. Insert '\s\s\s\s'` on src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs (lines 22-25 among others), a collection-expression indentation shape. The file is unmodified by this story and last changed by commit be36435 (2026-08-26, on main). The `format` gate runs before `unit-tests` and exits on failure, so every unit lane behind it is unreachable. Out of scope here: the fix edits a production source file, which this story's AC8 forbids.
status: open

### DW-331: Two of the three rejection events emitted by `/process` have zero test coverage anywhere, and the argument mapping that feeds all three is untested.
origin: spec-deferred 13fa8eedbbe5
location: src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1292-1330
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: medium
reason: `grep -rn "DuplicateWorkspaceLockRejected\|WorkspaceTransitionInvalidRejected" tests/` returns nothing, as does `grep -rn "IRejectionEvent" tests/`. Both records re-invoke `FolderCommandRejected.CanonicalIdentifierOrNull` / `NormalizeCommandTypeForRejection` inline (src/Hexalith.Folders.Server/DuplicateWorkspaceLockRejected.cs:22-46 and WorkspaceTransitionInvalidRejected.cs:22-46) and are emitted from FolderDomainProcessor.CreateRejectionEvent:1301,1325. Separately, that method sources its arguments from `result?.ActorPrincipalId`, `envelope.CorrelationId`, `envelope.MessageId` and `TryReadCanonicalExtension(...)`, none of which any test drives -- so a regression that echoed an envelope-supplied raw actor instead of the aggregate-nulled one leaves every new row green. Pre-existing; this story's rows call the factory directly, as the Intent's matrix specifies.
status: open

### DW-332: The leakage detector and its fixture readers are now duplicated verbatim across the story's two test files, so a hardening applied to one copy silently does not apply to the other.
origin: spec-deferred bf02dc3614f3
location: tests/Hexalith.Folders.Server.Tests/FolderCommandRejectedLeakageTests.cs and tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderArchiveMetadataLeakageTests.cs
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: medium
reason: `ContainsSentinel`, `JsonElementContainsSentinel`, `EscapeEveryCharacter`, `RequireWellFormedJson`, `SentinelById`, both fixture loaders, `RepositoryRoot` and both records (~150 lines) exist twice, as do three detector self-tests. Introduced by this story rather than pre-existing, but not fixable within it: the spec pins `Hexalith.Folders.Tests.csproj` to baseline, so the sanctioned `tests/shared` + `<Compile Include>` route is closed, and the alternative -- a shared helper in `src/Hexalith.Folders.Testing` -- is a public API addition to a packable library. Belongs with the nine-call-site corpus-reader consolidation already on the ledger.
status: open

### DW-333: Follow-up review still recommended for dw-archive-leakage-regression-coverage after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-archive-leakage-regression-coverage.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260828-230804-913d; this entry preserves the lingering recommendation for a deliberate later review.
status: open

### DW-334: Make opted-in Aspire/Dapr behavioral probes fail closed and observe subscriber receipt instead of passing with skipped endpoint checks.

origin: migrated from legacy ledger ("Deferred from: code review of 3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)"), 2026-09-01
location: tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:91,149,279
reason: Both Debug/source and Release/package AppHost runs booted all six services, but `CreateDaprClient` converts unresolved Dapr sidecar HTTP endpoints into skipped tests and the publish probe does not assert that the worker received the event. Make the opted-in behavioral probes fail closed and observe subscriber receipt.
status: open

### DW-335: Add the opted-in AppHost runtime suite to a regular automated DCP-capable CI lane.

origin: migrated from legacy ledger ("Deferred from: code review of 3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)"), 2026-09-01
location: .github/workflows/ci.yml:40-43; .github/workflows/release-packages.yml:136-148
reason: The existing baseline and release workflows build the topology but do not set `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION` or execute `Hexalith.Folders.AppHost.Tests`, so later startup regressions are not continuously gated. Add the opted-in suite to a regular automated DCP-capable CI lane.
status: open

### DW-336: Pre-existing Forgejo provider syntax errors block the normal Client.Tests project build.
origin: spec-deferred 14115ac2c839
location: src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808
source_spec: `spec-generated-client-conformance.md`
severity: high
reason: `dotnet build tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` fails before compiling the changed tests with CS1513 at ForgejoProvider.cs:808 and CS1519 at ForgejoProvider.cs:821. A source-isolated client conformance project builds and runs the changed tests successfully.
status: open

### DW-337: Non-available lifecycle read-model statuses can return a future observation time without compatibility validation.
origin: spec-deferred 16522b0d63aa
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:109
source_spec: `spec-lifecycle-result-normalization.md`
severity: medium
reason: ValidateSnapshotCompatibility rejects future ObservedAt values only for an Available result with a snapshot. Stale, Unavailable, Malformed, and NotFound outer statuses return their freshness without the same temporal check; this behavior predates the normalization change and requires a separate contract decision.
status: open

### DW-338: Sibling query handlers still duplicate the unavailable-freshness idiom and the authorization-outcome string constants that this spec normalized for the lifecycle handler.
origin: spec-deferred d6a12601aedf
location: src/Hexalith.Folders/Queries/Folders/WorkspaceLockStatusQueryHandler.cs:96-105,220
source_spec: `spec-lifecycle-result-normalization.md`
severity: medium
reason: `WorkspaceStatusQueryHandler`, `WorkspaceLockStatusQueryHandler`, `WorkspaceCleanupStatusQueryHandler`, `BranchRefPolicyQueryHandler`, and `TaskStatusQueryHandler` each declare their own `allowed`/`denied_safe` constants and repeat `Freshness with { Stale = true, ReasonCode = ... ?? ... }` over the same `FolderLifecycleFreshness` record, and they still return a `ProjectionWatermark` on unavailable results. The canonical helpers introduced here are `internal` to the same assembly, so those sites could adopt them, but this spec's intent is scoped to the lifecycle handler.
status: open

### DW-339: Follow-up review still recommended for dw-lifecycle-result-normalization after the damping cap was spent
origin: review-budget-followup
location: n/a
source_spec: `spec-lifecycle-result-normalization.md`
severity: low
reason: The follow-up-review damping cap (limits.max_followup_reviews = 1) was spent with the story finalized (status: done, verify green) while the review pass still recommended an independent follow-up. The work was committed by bmad-loop run 20260901-122019-16be; this entry preserves the lingering recommendation for a deliberate later review.
status: open

### DW-340: Four local lifecycle-area query fixtures still synthesize allowed claim evidence from nullable authority values.
origin: spec-deferred f11951ef4e1b
location: tests/Hexalith.Folders.Tests/Queries/Folders
source_spec: `spec-lifecycle-test-hygiene.md`
severity: medium
reason: This predates the bundle and is outside DW-54's named FolderLifecycleStatusTestSupport surface. TaskStatusQueryHandlerTests.cs, WorkspaceCleanupStatusQueryHandlerTests.cs, WorkspaceLockStatusProjectionTests.cs, and WorkspaceStatusQueryHandlerTests.cs each pass nullable tenant/principal values to EventStoreClaimTransformEvidence.Allowed.
status: open

### DW-341: Project-level dotnet test is incompatible with the pinned Microsoft.Testing.Platform invocation on .NET 10.
origin: spec-deferred a2da41ed4f8d
location: tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj and tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj
source_spec: `spec-lifecycle-test-hygiene.md`
severity: medium
reason: Both focused project commands fail before test execution because Microsoft.Testing.Platform 2.3.3 rejects the VSTest target under the .NET 10 SDK; direct xUnit v3 assembly execution remains green for the in-scope lanes.
status: open

### DW-342: The unchanged Forgejo provider source blocks a clean rebuild of the core test assembly.
origin: spec-deferred 3f2fb6265e4e
location: src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808
source_spec: `spec-lifecycle-test-hygiene.md`
severity: high
reason: The baseline contains a brace error at ForgejoProvider.cs:808, so the in-scope test assembly was rebuilt only with that pre-existing source problem isolated out of tree. No production source was changed by this bundle.
status: open

### DW-343: Three pre-existing GitHub provider tests fail in the broad core direct-runner lane.
origin: spec-deferred 88457e107f6a
location: tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs
source_spec: `spec-lifecycle-test-hygiene.md`
severity: medium
reason: The 1,658-test partial core run failed FreshRepositoryCreationCarryingPriorEvidenceStillExecutesInsteadOfReplaying, ReplaysEquivalentRepositoryCreationWithoutProviderAccess, and ReplaysEquivalentRepositoryBindingWithoutProviderAccess; the lifecycle namespace remained green.
status: open

### DW-344: The `Hexalith.Folders.Server.Tests` assembly is executed by no CI workflow and no gate script.
origin: spec-deferred 993cd4b24598
location: tests/tools/run-baseline-ci-gates.ps1 and .github/workflows/ci.yml
source_spec: `spec-lifecycle-test-hygiene.md`
severity: medium
reason: `Hexalith.Folders.Server.Tests` appears only in `Hexalith.Folders.slnx`; it is matched by no file under `.github/workflows/` and by no script under `tests/tools/`. The solution build compiles it, but its 645 tests -- including the lifecycle endpoint test this bundle repaired -- never execute in an automated lane. The lifecycle-status route itself remains covered by the integration contract-parity lane.
status: open

### DW-345: The `BuildApp` helper leaks its built `WebApplication` if endpoint mapping throws before the value is returned.
origin: spec-deferred a5d9369ad96d
location: tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs:258
source_spec: `spec-lifecycle-test-hygiene.md`
severity: low
reason: All seven callers bind the returned application with `await using`, but `BuildApp` itself calls `builder.Build()` and then `app.MapFoldersServerEndpoints()` before returning, so a mapping failure escapes with the built host undisposed. This is the same leak class the bundle fixed at line 34 and predates the bundle.
status: open

- source_spec: `/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/spec-update-submodules-and-hexalith-package-versions.md`
  summary: Align project-context default submodule init list with the eight-root inventory (add PolymorphicSerializations).
  evidence: Technology Stack lists eight roots including PolymorphicSerializations, but Development Workflow Rules omit `references/Hexalith.PolymorphicSerializations` from the default init list; pre-existing doc drift untouched by this Memories gitlink bump.

- source_spec: `/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/spec-update-submodules-and-hexalith-package-versions.md`
  summary: Reconcile EventStore nested FrontComposer/Memories gitlink objects with independently selected root gitlinks when EventStore advances.
  evidence: EventStore nested FrontComposer is `20d62abd…` vs root `d71790bb…`, nested Memories `3a7a7025…` vs advanced root `2f85536d…`; Memories-only root bump correctly left EventStore nested pointers alone.

- source_spec: `/home/administrator/projects/hexalith/folders/_bmad-output/implementation-artifacts/spec-update-submodules-and-hexalith-package-versions.md`
  summary: Restore Debug build of EventStore AggregateActor (`InspectPublicationRecoverySaveFailureAsync` missing on current EventStore tip).
  evidence: Unchanged EventStore gitlink fails `dotnet build` with CS0103 at AggregateActor.cs:1114 and :3212; not caused by the Memories pointer advance.
