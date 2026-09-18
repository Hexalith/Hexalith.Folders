### DW-10: (Round-3 echo) `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: InMemoryFolderRepository
reason: **(Round-3 echo)** `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary` — the lock is the real serialization primitive. Deferred — style cleanup; revisit when an EventStore-backed repository replaces the in-memory implementation as the production default.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-inmemory-repository-lock-cleanup
resolution-undo: 0f576fc559595d239f0a8032672b2a2033e87f3e1ec77133cdd65ef323dca046 2026-08-28 7374617475733a206f70656e

### DW-11: (Round-3 echo) `FolderCommandRejected` projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 4 (2026-05-20)"), 2026-08-24
location: FolderCommandRejected
reason: **(Round-3 echo)** `FolderCommandRejected` projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit. Deferred — design-level documentation work; introduce an explicit marker if a future projection consumes `IRejectionEvent`.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Server/FolderCommandRejected.cs:9-18 documents the rejection's wire/log/audit role and implements only IRejectionEvent; src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs:5-8 restricts projection payloads to IFolderEvent.

### DW-12: Add gateway 5xx Theory cases for 503/505/507/599 in `ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable
reason: Add gateway 5xx Theory cases for 503/505/507/599 in `ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable`. Deferred — the `>= 500 and < 600` catch-all production arm covers the behavior; this is regression-trap coverage.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-endpoint-hardening
resolution-undo: 6f917a24a4b79a2f7714e8b3186622f47192753a30463ad014765f32d9daddf9 2026-08-28 7374617475733a206f70656e

### DW-13: Add a `GatewayCorrelationRegex` header-injection Theory in `ArchiveFolderEndpointTests` proving CR/LF / oversized / control-character bytes are rejected before being reflected into `X-Correlation-Id`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: GatewayCorrelationRegex
reason: Add a `GatewayCorrelationRegex` header-injection Theory in `ArchiveFolderEndpointTests` proving CR/LF / oversized / control-character bytes are rejected before being reflected into `X-Correlation-Id`. Deferred.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-endpoint-hardening
resolution-undo: 6f917a24a4b79a2f7714e8b3186622f47192753a30463ad014765f32d9daddf9 2026-08-28 7374617475733a206f70656e

### DW-14: Add a cancel-mid-flight integration test to `ArchiveFolderProcessWiringTests` that exercises the in-processor cancellation/cleanup path (current `CancelledRequestShouldStopBeforeGatewayRoundTrip`…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveFolderProcessWiringTests
reason: Add a cancel-mid-flight integration test to `ArchiveFolderProcessWiringTests` that exercises the in-processor cancellation/cleanup path (current `CancelledRequestShouldStopBeforeGatewayRoundTrip` only verifies the HttpClient-level cancel before the request leaves the test). Deferred.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-15: Replace `InProcessEventStoreGatewayClient.ToGatewayException`'s ad-hoc `FolderResultCode → HTTP status` mapping with a shared mapping path used by the production EventStore gateway

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: InProcessEventStoreGatewayClient.ToGatewayException
reason: Replace `InProcessEventStoreGatewayClient.ToGatewayException`'s ad-hoc `FolderResultCode → HTTP status` mapping with a shared mapping path used by the production EventStore gateway. Deferred — current test mapping is consistent with the safe-denial REST contract but duplicates logic.
status: done 2026-08-29
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-gateway-canonical-mapping
resolution-undo: 131c1f336964dd52c5eba9e851a53d7590b7df8753625e01e11ab2eda25ad0ee 2026-08-29 7374617475733a206f70656e

### DW-16: Add `FolderCommandRejected` to `FolderArchiveMetadataLeakageTests` sentinel iteration so every `tests/fixtures/audit-leakage-corpus.json` value is asserted absent across the new rejection-event…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: tests/fixtures/audit-leakage-corpus.json
reason: Add `FolderCommandRejected` to `FolderArchiveMetadataLeakageTests` sentinel iteration so every `tests/fixtures/audit-leakage-corpus.json` value is asserted absent across the new rejection-event payload. Deferred — the production `FolderCommandRejected.Create` factory canonicalizes all identifiers at construction time which mitigates the leak vector, but corpus-driven coverage remains a regression trap.
status: done 2026-08-29
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-leakage-regression-coverage
resolution-undo: 97be6c1f7934e86d73a377d9abaf72fc20778f943083b4fcbdef5459ac0f70be 2026-08-29 7374617475733a206f70656e

### DW-17: Add `ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted` integration test covering REST → gateway → `/process` → gate same-key + equivalent-payload replay path

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted
reason: Add `ArchiveRequestShouldReturnIdempotentReplayWhenSameKeyEquivalentPayloadIsResubmitted` integration test covering REST → gateway → `/process` → gate same-key + equivalent-payload replay path. Deferred — gate-unit and endpoint-unit replay coverage exists; the round-trip is unverified end-to-end.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-18: Add a foreign-tenant smuggling integration test (`ArchiveRequestShouldRejectWhenEnvelopeTenantDisagreesWithAuthenticatedTenant`)

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: ArchiveRequestShouldRejectWhenEnvelopeTenantDisagreesWithAuthenticatedTenant
reason: Add a foreign-tenant smuggling integration test (`ArchiveRequestShouldRejectWhenEnvelopeTenantDisagreesWithAuthenticatedTenant`). Deferred — gate-unit coverage of `HasCompetingClientTenant` plus layered-auth tenant comparison at the request handler provide defense-in-depth.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-19: Add a `DenyingFolderArchivePolicyEvidenceProvider` test fake and an integration test exercising the AC8 policy-denied path end-to-end through `/process`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: FolderArchivePolicyOutcome.Denied
reason: Add a `DenyingFolderArchivePolicyEvidenceProvider` test fake and an integration test exercising the AC8 policy-denied path end-to-end through `/process`. Deferred to Epic 7 when the production policy provider lands; gate-unit coverage of `FolderArchivePolicyOutcome.Denied` exists.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-process-integration
resolution-undo: d36348236959b561c92d23156c47b3ced1f7bf75ebe08511194d0dafa28cce3e 2026-08-28 7374617475733a206f70656e

### DW-21: `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary`; the lock is the real serialization primitive and the dictionary's thread-safety adds nothing

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: InMemoryFolderRepository
reason: `InMemoryFolderRepository` mixes `lock (_gate)` with an internal `ConcurrentDictionary`; the lock is the real serialization primitive and the dictionary's thread-safety adds nothing. Deferred — style cleanup; revisit when an EventStore-backed `IFolderRepository` replaces the in-memory implementation as the production default.
status: done 2026-08-28
archived: 2026-09-18
resolution: resolved by sweep bundle dw-inmemory-repository-lock-cleanup
resolution-undo: 0f576fc559595d239f0a8032672b2a2033e87f3e1ec77133cdd65ef323dca046 2026-08-28 7374617475733a206f70656e

### DW-22: `SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence` does not detect a scenario where the scoped accessor is accidentally re-registered as singleton; a singleton accessor with…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence
reason: `SequentialRequestsShouldNotReusePriorLayeredAuthorizationEvidence` does not detect a scenario where the scoped accessor is accidentally re-registered as singleton; a singleton accessor with manual clearing in `finally` would also pass. Deferred — architectural test-design concern; revisit when DI lifetime auditing tooling is in place.
status: done 2026-08-29
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-accessor-lifetime-guard
resolution-undo: 8074c733b44d14cabce95cbf923fe8eeda11ad3a80fa107663b65ae29345066c 2026-08-29 7374617475733a206f70656e

### DW-23: `FolderCommandRejected` is a new `IRejectionEvent` type; the projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 3 (2026-05-20)"), 2026-08-24
location: FolderCommandRejected
reason: `FolderCommandRejected` is a new `IRejectionEvent` type; the projection/event-routing boundary between `IFolderEvent` (projection-bound) and `IRejectionEvent` (gateway-bound) is implicit. Deferred — document the contract or introduce an explicit marker if a future projection consumes `IRejectionEvent`.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Server/FolderCommandRejected.cs:9-18 and src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs:5-8 establish an explicit rejection-versus-domain-event boundary, resolving the projection ambiguity.

### DW-25: `FolderListProjection.Apply` throw-on-missing-create tears down multi-tenant rebuild while `FolderStateApply` is per-stream

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderListProjection.Apply
reason: `FolderListProjection.Apply` throw-on-missing-create tears down multi-tenant rebuild while `FolderStateApply` is per-stream — asymmetric blast radius. Deferred — revisit when projection-rebuild tooling and replay diagnostics are introduced (Epic 6/7).
status: done 2026-08-28
archived: 2026-09-18
decision: 2026-08-28 Keep fail-fast rebuild — Treat missing-create ordering as repository corruption that must stop the rebuild and require operator intervention.
resolution: closed by human decision: Treat missing-create ordering as repository corruption that must stop the rebuild and require operator intervention.
decision: 2026-08-28 Keep fail-fast rebuild — Treat missing-create ordering as repository corruption that must stop the rebuild and require operator intervention.

### DW-26: `FolderArchiveAclEvidence` is an unsigned value object

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchiveAclEvidence
reason: `FolderArchiveAclEvidence` is an unsigned value object — defense relies on trustworthy upstream `Allowed(...)` callers. Deferred — architectural concern, addresses with evidence-signing or capability-token redesign beyond Story 2.8.
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Document the trust boundary and treat evidence as an internal value inside the trusted server process.
decision: 2026-08-25 Accept in-process boundary — Document the trust boundary and treat evidence as an internal value inside the trusted server process.

### DW-29: `ArchiveFolder.PayloadTenantId` with malformed segment matching the authoritative tenant — narrow edge case. Deferred — revisit if payload-tenant smuggling becomes a verified threat.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: ArchiveFolder.PayloadTenantId
reason: `ArchiveFolder.PayloadTenantId` with malformed segment matching the authoritative tenant — narrow edge case. Deferred — revisit if payload-tenant smuggling becomes a verified threat.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:62-70 validates and rejects reserved or invalid authoritative tenants before archive handling, including when a malformed payload tenant equals the authority.

### DW-30: `ArchiveFolderClientConformanceTests` parameter-order assertion is brittle to NSwag generator changes

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: ArchiveFolderClientConformanceTests
reason: `ArchiveFolderClientConformanceTests` parameter-order assertion is brittle to NSwag generator changes — currently passing. Deferred — relax to a set-equality assertion when a generator upgrade breaks it.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-generated-client-conformance
resolution-undo: ef4b096a597db99515c9fb5fd152ab76f1be034dca947c7297c3c5207593bd6f 2026-09-02 7374617475733a206f70656e

### DW-31: `FolderArchiveMetadataLeakageTests` asserts the validator's input restriction indirectly via factory defaults

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchiveMetadataLeakageTests
reason: `FolderArchiveMetadataLeakageTests` asserts the validator's input restriction indirectly via factory defaults — gives confirmation, not coverage. Deferred — expand to a corpus-driven property test in a future hardening pass.
status: done 2026-08-29
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-leakage-regression-coverage
resolution-undo: 97be6c1f7934e86d73a377d9abaf72fc20778f943083b4fcbdef5459ac0f70be 2026-08-29 7374617475733a206f70656e

### DW-32: Untested branches: `FolderArchivePolicyOutcome.ScopeMismatch`, several `FolderArchiveAclOutcome` variants, `FolderAppendOutcome.FingerprintConflict` mapping

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: FolderArchivePolicyOutcome.ScopeMismatch
reason: Untested branches: `FolderArchivePolicyOutcome.ScopeMismatch`, several `FolderArchiveAclOutcome` variants, `FolderAppendOutcome.FingerprintConflict` mapping — become live once the gate is wired into production. Deferred until that happens.
status: done 2026-08-29
archived: 2026-09-18
resolution: resolved by sweep bundle dw-archive-gate-outcome-coverage
resolution-undo: 42f54c5f9b4c96c48c0c8c4827569fd192cf4549979c3bcd8a2d0f77e64b9233 2026-08-29 7374617475733a206f70656e

### DW-33: `FolderArchiveTenantGate(TimeProvider)` constructor never exercised by tests or production callers. Deferred — moot until the gate is wired.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: n/a
reason: `FolderArchiveTenantGate(TimeProvider)` constructor never exercised by tests or production callers. Deferred — moot until the gate is wired.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:45-46 registers TimeProvider.System and FolderArchiveTenantGate; tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:734-735,769-770 injects a fixed TimeProvider through the host and exercises the two-argument primary constructor.

### DW-35: `EffectivePermissionsActionCatalog` insertion order untested — cosmetic until a positional consumer appears. Deferred.

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation round 2 (2026-05-20)"), 2026-08-24
location: EffectivePermissionsActionCatalog
reason: `EffectivePermissionsActionCatalog` insertion order untested — cosmetic until a positional consumer appears. Deferred.
status: done 2026-08-29
archived: 2026-09-18
resolution: resolved by sweep bundle dw-effective-action-contracts
resolution-undo: 8c06abcaac18bf48049224e367f28b74323a66545b9d73cdbce66560c048d443 2026-08-29 7374617475733a206f70656e

### DW-36: `IFolderEvent` interface coupling — `FolderProjectionEnvelope.Event` was widened from `FolderCreated` to `IFolderEvent`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8-archive-folders-with-audit-preservation (2026-05-20)"), 2026-08-24
location: FolderProjectionEnvelope.Event
reason: `IFolderEvent` interface coupling — `FolderProjectionEnvelope.Event` was widened from `FolderCreated` to `IFolderEvent`. Other consumers of the envelope (workers, snapshots, generated code, tests outside this diff) must remain consistent. Deferred — broader event-interface design beyond Story 2.8 scope.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Projections/FolderList/FolderProjectionEnvelope.cs:5-8 accepts IFolderEvent, and tests/Hexalith.Folders.Workers.Tests/SemanticIndexingProcessManagerTests.cs:173-181 verifies an archived event through the widened envelope.

### DW-43: `DiagnosticSentinels` on `FolderLifecycleStatusReadModelSnapshot` is never read by the handler

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusReadModelSnapshot.cs:12
reason: `DiagnosticSentinels` on `FolderLifecycleStatusReadModelSnapshot` is never read by the handler [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusReadModelSnapshot.cs:12`] — deferred, harmless dead state; may anchor future redaction-enforcement logic.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusMetadataLeakageTests.cs:53-75 now uses DiagnosticSentinels as an active redaction negative control and proves each sentinel is absent from the serialized response.

### DW-44: `HasNoBindingReferences` duplicates `HasValue` logic [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:321-326`] — deferred, cosmetic consolidation.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:321-326
reason: `HasNoBindingReferences` duplicates `HasValue` logic [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:321-326`] — deferred, cosmetic consolidation.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-46: `FolderLifecycleProjectionState.Unknown` is handled by the switch's `_` arm, never matched by name

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:117-132
reason: `FolderLifecycleProjectionState.Unknown` is handled by the switch's `_` arm, never matched by name [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:117-132`] — deferred, cosmetic.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:154-156 explicitly returns FolderLifecycleProjectionState.Unknown for the relevant state.

### DW-47: Test files use `ConfigureAwait(true)` while production handler uses `ConfigureAwait(false)` [`tests/Hexalith.Folders.Tests/Queries/Folders/*.cs`] — deferred, style inconsistency.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Queries/Folders/*.cs
reason: Test files use `ConfigureAwait(true)` while production handler uses `ConfigureAwait(false)` [`tests/Hexalith.Folders.Tests/Queries/Folders/*.cs`] — deferred, style inconsistency.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-lifecycle-test-hygiene
resolution-undo: 9ffcf9ab21c47fbee1041561963d8a61a6e0c8d5a978d40e48b6584ea52dfbcb 2026-09-02 7374617475733a206f70656e

### DW-48: `ActorSafeIdentifier: "actor_present"` magic string [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:43`] — deferred, extract to a named constant.

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:43
reason: `ActorSafeIdentifier: "actor_present"` magic string [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:43`] — deferred, extract to a named constant.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:16,56-61 extracts and uses ActorPresentIdentifier.

### DW-49: `AllowedOutcome` and `DeniedSafeOutcome` string constants in handler instead of an enum

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:12-13
reason: `AllowedOutcome` and `DeniedSafeOutcome` string constants in handler instead of an enum [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:12-13`] — deferred, parallel representation to `Code` invites drift.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-50: `ReasonCode` null-coalesce ordering is inconsistent across branches and can bury handler-determined reasons

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,126,189-194,279-287
reason: `ReasonCode` null-coalesce ordering is inconsistent across branches and can bury handler-determined reasons [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,126,189-194,279-287`] — deferred, refactor pass to consolidate.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-51: Snapshot freshness mutation idiom repeated and `ProjectionWatermark` preserved on `Unavailable` outcomes

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,189-194,279-287
reason: Snapshot freshness mutation idiom repeated and `ProjectionWatermark` preserved on `Unavailable` outcomes [`src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:93-99,189-194,279-287`] — deferred, refactor pass.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-lifecycle-result-normalization
resolution-undo: 20384003a3ab721aa676d555ac211048ed294c230b6d2536c139f25928f3d33e 2026-09-02 7374617475733a206f70656e

### DW-52: `LifecycleStatusClientConformanceTests` asserts `methods.Single(m => ...)` and locks NSwag parameter mangling

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/LifecycleStatusClientConformanceTests.cs
reason: `LifecycleStatusClientConformanceTests` asserts `methods.Single(m => ...)` and locks NSwag parameter mangling [`tests/Hexalith.Folders.Client.Tests/LifecycleStatusClientConformanceTests.cs`] — deferred, brittle to generator upgrades.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-generated-client-conformance
resolution-undo: ef4b096a597db99515c9fb5fd152ab76f1be034dca947c7297c3c5207593bd6f 2026-09-02 7374617475733a206f70656e

### DW-53: `MapFoldersServerEndpointsShouldRegisterLifecycleStatusRoute` builds an app without `await using` disposal

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs
reason: `MapFoldersServerEndpointsShouldRegisterLifecycleStatusRoute` builds an app without `await using` disposal [`tests/Hexalith.Folders.Server.Tests/FolderLifecycleStatusEndpointTests.cs`] — deferred, resource leak in test process.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-lifecycle-test-hygiene
resolution-undo: 9ffcf9ab21c47fbee1041561963d8a61a6e0c8d5a978d40e48b6584ea52dfbcb 2026-09-02 7374617475733a206f70656e

### DW-54: `FolderLifecycleStatusTestSupport` builds `EventStoreClaimTransformEvidence.Allowed(...)` with nullable tenant/principal parameters

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusTestSupport.cs
reason: `FolderLifecycleStatusTestSupport` builds `EventStoreClaimTransformEvidence.Allowed(...)` with nullable tenant/principal parameters [`tests/Hexalith.Folders.Tests/Queries/Folders/FolderLifecycleStatusTestSupport.cs`] — deferred, opaque test scaffolding.
status: done 2026-09-02
archived: 2026-09-18
resolution: resolved by sweep bundle dw-lifecycle-test-hygiene
resolution-undo: 9ffcf9ab21c47fbee1041561963d8a61a6e0c8d5a978d40e48b6584ea52dfbcb 2026-09-02 7374617475733a206f70656e

### DW-55: Lifecycle 200 response does not echo `taskId` body field even when `X-Hexalith-Task-Id` is read

origin: migrated from legacy ledger ("Deferred from: code review of 2-7-inspect-folder-lifecycle-and-binding-status (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:107-119
reason: Lifecycle 200 response does not echo `taskId` body field even when `X-Hexalith-Task-Id` is read [`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:107-119`] — deferred, requires contract update to declare `taskId` in `FolderLifecycleStatus`.
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Document the response header as the canonical transport-level task correlation.
decision: 2026-08-25 Keep response header — Document the response header as the canonical transport-level task correlation.

### DW-60: `EffectivePermissionsTaskScope.AllowedActions` is `IReadOnlySet<string>` without an enforced comparer

origin: migrated from legacy ledger ("Deferred from: code review of 2-5-inspect-effective-permissions (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs
reason: `EffectivePermissionsTaskScope.AllowedActions` is `IReadOnlySet<string>` without an enforced comparer [`src/Hexalith.Folders/Authorization/EffectivePermissionsTaskScope.cs`] — deferred, testing-only seam; production task-scope projection must construct the set with `StringComparer.Ordinal` to match the action catalog. Document the contract on the type when the production task-scope projection lands.
status: done 2026-08-29
archived: 2026-09-18
resolution: resolved by sweep bundle dw-effective-action-contracts
resolution-undo: 8c06abcaac18bf48049224e367f28b74323a66545b9d73cdbce66560c048d443 2026-08-29 7374617475733a206f70656e

### DW-64: `InvalidFolderMetadata` collapses length / control-char / forbidden-term failures into a single code

origin: migrated from legacy ledger ("Deferred from: code review of 2-3-create-folders-within-a-tenant (2026-05-19)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:64-79
reason: `InvalidFolderMetadata` collapses length / control-char / forbidden-term failures into a single code [`src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:64-79`] — deferred, pre-existing coarse-grained pattern (Story 2.2 makes the same trade-off); splitting requires expanding the public code surface and updating consumer error-handling.
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Document the stable, non-revealing coarse code as deliberate.
decision: 2026-08-25 Keep coarse code — Document the stable, non-revealing coarse code as deliberate.

### DW-75: Removal of `--no-build` from generator-invoking tests adds an incremental MSBuild check per test

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:404
reason: Removal of `--no-build` from generator-invoking tests adds an incremental MSBuild check per test [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:404`]. Accepted trade-off for the `obj/` lock race; `[Collection("ParityOracleGenerator")]` keeps it serial.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit 2ff62a7; tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:406 invokes dotnet run with --no-restore and --no-build.

### DW-79: Per-file allow-list pattern in negative-scope test

origin: migrated from legacy ledger ("Deferred from: code review of 1-14-wire-contract-spine-drift-and-generated-client-ci-gates (2026-05-17)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs:286-298
reason: Per-file allow-list pattern in negative-scope test [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs:286-298`]. Allows only `contract-spine.yml`; Stories 1.15/1.16 will add more entries one at a time. Consider an expected-set assertion when 1.15 lands.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/TenantFolderProviderContractGroupTests.cs:353-366 asserts the complete expected set of five workflows.

### DW-85: `read_consistency_class` enum mixes underscore (`not_applicable`) and hyphen (`eventually-consistent`) forms

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle round 3 (2026-05-17)"), 2026-08-24
location: tests/tools/parity-oracle-generator/Program.cs ReadConsistencyClass
reason: `read_consistency_class` enum mixes underscore (`not_applicable`) and hyphen (`eventually-consistent`) forms [`tests/tools/parity-oracle-generator/Program.cs ReadConsistencyClass`, `tests/fixtures/parity-contract.schema.json`] — intentional schema choice that survived prior reviews; harmonize during a future schema-cleanup sweep.
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Accept not_applicable as the established exceptional sentinel and retain the existing wire vocabulary.
decision: 2026-08-25 Keep vocabulary — Accept not_applicable as the established exceptional sentinel and retain the existing wire vocabulary.

### DW-90: Test-helper `LoadOperationIds` only recognizes lowercase canonical HTTP verbs

origin: migrated from legacy ledger ("Deferred from: code review of 1-13-generate-the-c13-parity-oracle (2026-05-17)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:283-290
reason: Test-helper `LoadOperationIds` only recognizes lowercase canonical HTTP verbs (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:283-290`). Divergence from generator's case-insensitivity would underestimate inventory; harmless until contract authors use uppercase verbs.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:457-459 verifies that generated HTTP verbs are lowercased.

### DW-97: Empty-parameter-name corner case slips through `EnsureParameter`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:148-154
reason: Empty-parameter-name corner case slips through `EnsureParameter` (`src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:148-154`). Fail-closed-at-compile is acceptable for impossible-from-current-spine input.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit b117e56; src/Hexalith.Folders.Client/Generation/Program.cs:198-213 rejects empty or mismatched normalized parameter names.

### DW-100: `Process.WaitForExit(10_000)` after `Kill(entireProcessTree)` return value ignored; Windows handle-release race on `Directory.Delete`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:241-256
reason: `Process.WaitForExit(10_000)` after `Kill(entireProcessTree)` return value ignored; Windows handle-release race on `Directory.Delete` (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:241-256`). Resolves together with the Round-3 deferred tempdir-cleanup race.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit b117e56; tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:308-331 terminates and drains the spawned process.

### DW-104: Generator csproj relies on MSBuild item-ordering to exclude `Shared/**/*.cs` (`src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:5-7`).

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:5-7
reason: Generator csproj relies on MSBuild item-ordering to exclude `Shared/**/*.cs` (`src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:5-7`).
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: Commit b117e56 and src/Hexalith.Folders.Client/Generation/Hexalith.Folders.Client.Generation.csproj:7-16 explicitly remove Shared/**/*.cs from compilation and consume the shared code through a ProjectReference.

### DW-107: Generator test `HelperGenerationTargetRegeneratesWhenContractSpineChanges` writes mutated spine via `Encoding.UTF8` with BOM preamble

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:214-218
reason: Generator test `HelperGenerationTargetRegeneratesWhenContractSpineChanges` writes mutated spine via `Encoding.UTF8` with BOM preamble (`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:214-218`).
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:295-296 uses Encoding.UTF8 under the .NET 10 SDK pinned by global.json; modern Encoding.UTF8 is BOM-less, so the alleged BOM preamble is not emitted.

### DW-111: Generation subproject offline-build reliance (`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:46`)

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:46
reason: Generation subproject offline-build reliance (`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:46`) — `dotnet run --project Generation\Hexalith.Folders.Client.Generation.csproj` requires successful restore on every host build; fresh checkout without NuGet cache fails. Belongs to Story 1.14 (CI gates).
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit 1efdb19; .github/workflows/contract-spine.yml:31-39 restores before running build and contract gates.

### DW-114: Defensive validation for zero-document YAML (`src/Hexalith.Folders.Client/Generation/Program.cs:398-400`) — spine always has at least one document; `yaml.Documents[0]` is safe in practice.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:398-400
reason: Defensive validation for zero-document YAML (`src/Hexalith.Folders.Client/Generation/Program.cs:398-400`) — spine always has at least one document; `yaml.Documents[0]` is safe in practice.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit b117e56; src/Hexalith.Folders.Client/Generation/Shared/YamlContractLoader.cs:14-25 rejects YAML streams containing zero documents.

### DW-115: Defensive validation for bare-filename `outputPath`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 2 (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:27
reason: Defensive validation for bare-filename `outputPath` (`src/Hexalith.Folders.Client/Generation/Program.cs:27`) — MSBuild target always provides absolute path; only triggers under direct CLI invocation with a bare filename.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Client/Generation/Program.cs:38 safely falls back to '.' when Path.GetDirectoryName(outputPath) is null.

### DW-118: W2: Cross-redaction invariant between record-level `redaction.visibility: redacted` and per-field `evidenceTimestamp.precision: redacted` (and similar paired fields on `actorReference`,…

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups follow-up (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditTrailEntryRedacted example
reason: W2: Cross-redaction invariant between record-level `redaction.visibility: redacted` and per-field `evidenceTimestamp.precision: redacted` (and similar paired fields on `actorReference`, `operationId`, future audience-conditional fields) is unenforced. A server could legitimately emit `record-redacted` with `evidenceTimestamp.precision: exact` and a real timestamp. Fix needs the same JSON-Schema-2020-12 `if/then` conditional design pattern P-Schema-8 used for trust/freshness; best bundled with the operator-audience hardening story that closes D4 (`AuditRecord` correlation-ID exposure). (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditTrailEntryRedacted example`)
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: Commit 5d13a83a; src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:9470-9490 forbids exact values under redacted timestamp states, with regression coverage at tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:547-587.

### DW-119: W3: `PrincipalMismatchSafeDenialProblem` example uses HTTP 404 + `category: not_found`

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups follow-up (2026-05-15)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: PrincipalMismatchSafeDenialProblem
reason: W3: `PrincipalMismatchSafeDenialProblem` example uses HTTP 404 + `category: not_found`. Other tenant/principal-related safe-denial paths in the corpus map to `tenant_access_denied`. Speculative drift without a fuller cross-corpus check. Revisit alongside the audience-equivalence rework that defines canonical category mappings for principal-mismatch scenarios. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: PrincipalMismatchSafeDenialProblem`)
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Accept not_found as the intentional existence-hiding principal-mismatch response.
decision: 2026-08-25 Keep safe 404 — Accept not_found as the intentional existence-hiding principal-mismatch response.

### DW-120: Historical Story 1.11 resolution summary

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: n/a
reason: Resolved 2026-05-15 by Story 1.11 continuation: P-Schema-1 through P-Schema-9, P-Test-1, P-Test-3, P-Sweep-1, and D5 are closed in the OpenAPI contract, contract notes, and focused contract tests. Historical entries remain below for traceability.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit 8d339b8; tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:548-650 covers the audit and operations-console contract group.

### DW-121: P-Schema-1: `DiagnosticBase` + `allOf` + `additionalProperties: false` JSON Schema gotcha

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, LockDiagnostics, DirtyStateDiagnostics, FailedOperationDiagnostics, ProviderStatusDiagnostics, SyncStatusDiagnostics, ProjectionFreshnessDiagnostics
reason: RESOLVED 2026-05-15: P-Schema-1: `DiagnosticBase` + `allOf` + `additionalProperties: false` JSON Schema gotcha. Strict JSON Schema 2020-12 validators reject subclass-added properties because each `allOf` member evaluates `additionalProperties` independently. Fix: replace base `additionalProperties: false` with `unevaluatedProperties: false` on the composed `allOf` schemas. Needs coordination across `DiagnosticBase` + 6 subclasses (`LockDiagnostics`, `DirtyStateDiagnostics`, `FailedOperationDiagnostics`, `ProviderStatusDiagnostics`, `SyncStatusDiagnostics`, `ProjectionFreshnessDiagnostics`) + 7 example payloads + the foundation tests that may assert specific additionalProperties behavior. Best owned by Story 1.12 (NSwag SDK generation) where strict-mode validation lands. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, LockDiagnostics, DirtyStateDiagnostics, FailedOperationDiagnostics, ProviderStatusDiagnostics, SyncStatusDiagnostics, ProjectionFreshnessDiagnostics`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-122: P-Schema-2: `AuditRecord` redaction shape leaks timing

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditRecordRedacted example
reason: RESOLVED 2026-05-15: P-Schema-2: `AuditRecord` redaction shape leaks timing (`evidenceTimestamp`) and actor identity (`actorReference`) on redacted records. Wrapping the leaky fields in audience-gated `RedactionMetadata` requires design choices (bucketed timestamps vs. sentinel replacement, optional vs. null) plus example regeneration and audit-leakage-corpus alignment. Best owned alongside the operator-audience hardening story. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: AuditRecord, AuditRecordRedacted example`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-123: P-Schema-3: `DiagnosticBase.audience` is a required body field that lets callers A/B-test their credentials and observe a `consumer` → `authorized_operator` flip

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads, AuditOpsConsoleContractGroupTests
reason: P-Schema-3: `DiagnosticBase.audience` is a required body field that lets callers A/B-test their credentials and observe a `consumer` → `authorized_operator` flip. Decision D9 chose "remove audience from body". Deferred because the change touches the base schema, every diagnostic subclass example, and the audience-checking tests. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads, AuditOpsConsoleContractGroupTests`)
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Supersede historical D9 and accept the current explicit audience discriminator as the implemented v1 contract.
decision: 2026-08-25 Retain audience — Supersede historical D9 and accept the current explicit audience discriminator as the implemented v1 contract.

### DW-124: P-Schema-4: `DiagnosticBase.fieldClassifications` is optional with no `minItems`, allowing operator responses to omit the array entirely

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads
reason: RESOLVED 2026-05-15: P-Schema-4: `DiagnosticBase.fieldClassifications` is optional with no `minItems`, allowing operator responses to omit the array entirely. Requires audience-conditional schema or a split into consumer/operator variants plus example regeneration. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticBase, 7 example payloads`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-125: P-Schema-5: `ReadinessDiagnostics` schema lacks provider/folder/workspace summary references called out by AC 5 and the operation-inventory row

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ReadinessDiagnostics, ReadinessDiagnostics example
reason: RESOLVED 2026-05-15: P-Schema-5: `ReadinessDiagnostics` schema lacks provider/folder/workspace summary references called out by AC 5 and the operation-inventory row. Add operator-audience-gated summary fields (`DiagnosticSafeIdentifier` + `RedactionMetadata`). (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ReadinessDiagnostics, ReadinessDiagnostics example`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-126: P-Schema-6: `LockDiagnostics.lockReference` and `ProviderStatusDiagnostics.providerBindingReference` field-presence is an audience oracle (present for operator, absent for consumer)

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: LockDiagnostics, ProviderStatusDiagnostics
reason: RESOLVED 2026-05-15: P-Schema-6: `LockDiagnostics.lockReference` and `ProviderStatusDiagnostics.providerBindingReference` field-presence is an audience oracle (present for operator, absent for consumer). Coordinated with audience-conditional schema work above. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: LockDiagnostics, ProviderStatusDiagnostics`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-127: P-Schema-7: `OperationTimelineEntry.workspaceId` leaks cross-workspace evidence to callers authorized for the folder but not the specific workspace

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperationTimelineEntry
reason: RESOLVED 2026-05-15: P-Schema-7: `OperationTimelineEntry.workspaceId` leaks cross-workspace evidence to callers authorized for the folder but not the specific workspace. Gate via audience-aware redaction. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperationTimelineEntry`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-128: P-Schema-8: No cross-field consistency invariant between `DiagnosticTrustEvidence.availability` and `FreshnessMetadata.stale`

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticTrustEvidence, FreshnessMetadata
reason: RESOLVED 2026-05-15: P-Schema-8: No cross-field consistency invariant between `DiagnosticTrustEvidence.availability` and `FreshnessMetadata.stale`. Schema accepts contradictory pairs. Fix needs JSON-Schema-2020-12 `if/then` conditionals or a refactor into a single state machine. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: DiagnosticTrustEvidence, FreshnessMetadata`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-129: P-Schema-9: Opaque-identifier patterns are inconsistent across siblings

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OpaqueIdentifier, ContentHashReference, ChangedPathEvidence, AuditRecord, ProviderStatusDiagnostics
reason: RESOLVED 2026-05-15: P-Schema-9: Opaque-identifier patterns are inconsistent across siblings (`actorref_` min 7, `digest_` min 9, `changeref_` min 6, `provref_` min 8). Factor a shared `PrefixedOpaqueIdentifier` schema and normalize. Cross-cutting; touches Stories 1.7-1.11 patterns. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OpaqueIdentifier, ContentHashReference, ChangedPathEvidence, AuditRecord, ProviderStatusDiagnostics`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-132: P-Test-1: Cursor/filter tamper, principal-mismatch, invalid-sort, boundary-duplicate, empty-page negative-case tests absent (AC 19 / AC 22 / Tasks line 100)

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs
reason: RESOLVED 2026-05-15: P-Test-1: Cursor/filter tamper, principal-mismatch, invalid-sort, boundary-duplicate, empty-page negative-case tests absent (AC 19 / AC 22 / Tasks line 100). Adding explicit negative tests requires representative cursor/principal fixtures or a runtime test harness; best owned by Story 1.13/1.14 contract-test-harness work. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-133: P-Test-2: `audit_access_denied` requirement is enforced only on `AuditOperationIds`, not on ops-console diagnostic operations that also declare it in canonical-error-categories

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:AuditOpsConsoleQueries_OmitIdempotencyAndDeclareReadConsistencySafeDenial
reason: P-Test-2: `audit_access_denied` requirement is enforced only on `AuditOperationIds`, not on ops-console diagnostic operations that also declare it in canonical-error-categories. Per-op canonical-category audit needed before broadening. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:AuditOpsConsoleQueries_OmitIdempotencyAndDeclareReadConsistencySafeDenial`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs:117-130 verifies the current operations-console category arrays, which intentionally omit audit_access_denied.

### DW-134: P-Test-3: Examples for `AuditTrailPage` / `OperationTimelinePage` cover only the single-result case

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: components.examples
reason: RESOLVED 2026-05-15: P-Test-3: Examples for `AuditTrailPage` / `OperationTimelinePage` cover only the single-result case. Add named synthetic examples for zero results, exactly-limit boundary, beyond-last cursor, empty-page continuation, multi-tenant denial parity, and operator-disposition spectrum (Tasks line 86). Several hundred lines of example YAML; defer alongside the audience-equivalence rework. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: components.examples`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-135: P-Sweep-1: `CanonicalErrorCategory` / `WorkspaceErrorCategory` enum widening

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: all Stories 1.7-1.10 operations
reason: RESOLVED 2026-05-15: P-Sweep-1: `CanonicalErrorCategory` / `WorkspaceErrorCategory` enum widening (`projection_stale`, `projection_unavailable`, `failed_operation`) not propagated to Stories 1.7-1.10 operations' `x-hexalith-canonical-error-categories` arrays. Cross-story sweep; best owned by Story 1.12 or 1.14. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: all Stories 1.7-1.10 operations`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-137: D2: `tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs` was edited outside the spec's `Allowed Files And Forbidden Work` list (the helper sits under…

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs
reason: D2: `tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs` was edited outside the spec's `Allowed Files And Forbidden Work` list (the helper sits under `tests/Hexalith.Folders.Testing.Tests/`, not `tests/Hexalith.Folders.Contracts.Tests/` or `tests/tools/`). The edit is logically required for Story 1.11 to own the ops-console path family, so record as an explicit scope expansion in the story's Change Log rather than reverting. (`tests/Hexalith.Folders.Testing.Tests/Helpers/SpineContractAssertions.cs`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/1-11-author-audit-and-ops-console-query-contract-groups.md:394 records and accepts the SpineContractAssertions scope expansion.

### DW-140: D5: `OperatorDispositionLabel` enum includes `auto_recovering` and `available`, but no synthetic example exercises either value

origin: migrated from legacy ledger ("Deferred from: code review of 1-11-author-audit-and-ops-console-query-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples
reason: RESOLVED 2026-05-15: D5: `OperatorDispositionLabel` enum includes `auto_recovering` and `available`, but no synthetic example exercises either value. Cosmetic test gap; bundle into the boundary-scenario examples patch. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: examples`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuditOpsConsoleContractGroupTests.cs`)
status: done 2026-05-15
archived: 2026-09-18
resolution: Closed by the Story 1.11 continuation in the OpenAPI contract, contract notes, and focused contract tests.

### DW-141: W1: Negative-test cases for duplicate `operationId`, missing required `x-hexalith-*` metadata, mutating-without-idempotency, and read-without-read-consistency are not exercised

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:1694
reason: W1: Negative-test cases for duplicate `operationId`, missing required `x-hexalith-*` metadata, mutating-without-idempotency, and read-without-read-consistency are not exercised. AC12 minimum-matrix gap; better owned by Story 1.14 (Contract Spine CI gates). (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:1694`)
status: done 2026-08-25
archived: 2026-09-18
resolution: already resolved: tests/tools/parity-oracle-generator/Program.cs:162,208; tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:167; and GovernanceCompletenessGateTests.cs:538 now reject duplicate operation IDs and missing mutation/read metadata with negative controls.

### DW-142: W2: No assertion that `hexalith.folders.v1.yaml` parses as a valid OpenAPI 3.1 document

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:LoadYamlMapping
reason: W2: No assertion that `hexalith.folders.v1.yaml` parses as a valid OpenAPI 3.1 document — the test currently only loads the file via `YamlStream`. Pre-existing across all contract-group tests; better owned by Story 1.6 foundation tests. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/CommitStatusContractGroupTests.cs:LoadYamlMapping`)
status: done 2026-08-25
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:17,34 and .github/workflows/contract-spine.yml:31 restore NSwag and parse, generate, and build the OpenAPI document in the required CI path.

### DW-145: W5: Wire `OperatorDispositionLabel` (defined at yaml:5329) into the relevant status schemas as part of Story 6.3

origin: migrated from legacy ledger ("Deferred from: code review of 1-10-author-commit-and-workspace-status-contract-groups (2026-05-14)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperatorDispositionLabel
reason: W5: Wire `OperatorDispositionLabel` (defined at yaml:5329) into the relevant status schemas as part of Story 6.3 — operations console rendering. AC6 names disposition labels but Story 1.10 has no consumer of them yet; deferring keeps the contract surface free of unused fields until 6.3 defines the actual rendering requirements. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: OperatorDispositionLabel`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:9634-9635 and :9773-9786 defines the disposition values; src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs:21-60 maps them for the UI.

### DW-146: W1: No `412 Precondition Failed` response for `ChangeFile` concurrency control

origin: migrated from legacy ledger ("Deferred from: code review of 1-9-author-file-mutation-and-context-query-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ChangeFile
reason: W1: No `412 Precondition Failed` response for `ChangeFile` concurrency control — concurrency model and optimistic-concurrency headers (`If-Match`/`If-None-Match`) belong to Epic 4 runtime; Story 1.9 contract group declares only idempotency-conflict (409), not stale-content versioning. Revisit when Epic 4 implements ChangeFile semantics on prepared workspaces. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml: ChangeFile`)
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Locking plus idempotency remain canonical; no independent content-version precondition is supported.
decision: 2026-08-25 Keep lock model — Locking plus idempotency remain canonical; no independent content-version precondition is supported.

### DW-155: `EnumerateNamedFields` yields the same property names twice via the recursion path

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:252-288
reason: `EnumerateNamedFields` yields the same property names twice via the recursion path. Harmless but quadratic-ish on deep trees; revisit if test runtime grows. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:252-288`)
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:366-404 yields each properties key only in its dedicated loop and recursively visits child values, eliminating the former duplicate-yield path.

### DW-156: `GetOptionalScalar` uses `ShouldBeOfType<YamlScalarNode>()` and throws an opaque Shouldly error on a malformed mapping/sequence value

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:386-391
reason: `GetOptionalScalar` uses `ShouldBeOfType<YamlScalarNode>()` and throws an opaque Shouldly error on a malformed mapping/sequence value. Defer until the validator is rewritten with structured diagnostics. (`tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:386-391`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/WorkspaceLockContractGroupTests.cs:502-506 now tests value is YamlScalarNode and returns null for non-scalars instead of throwing a Shouldly type assertion.

### DW-159: Read-consistency token form drift: story prose uses hyphenated `snapshot-per-task` / `read-your-writes` / `eventually-consistent`, OpenAPI `ReadConsistencyClass` enum uses underscore form

origin: migrated from legacy ledger ("Deferred from: code review of 1-8-author-workspace-and-lock-contract-groups (2026-05-13)"), 2026-08-24
location: ReadConsistencyClass
reason: Read-consistency token form drift: story prose uses hyphenated `snapshot-per-task` / `read-your-writes` / `eventually-consistent`, OpenAPI `ReadConsistencyClass` enum uses underscore form. Enum is canonical; revisit prose during vocabulary documentation.
status: done 2026-09-05
archived: 2026-09-18
resolution: already resolved: Commit 5a4fa0f; tests/tools/parity-oracle-generator/Program.cs:713-726 explicitly converts canonical underscore values to the hyphenated parity vocabulary, including not_applicable, while tests/fixtures/parity-contract.schema.json:50-56 pins the output vocabulary.

### DW-162: `CanonicalErrorCategory` retains `provider_failure_known` without any operation referencing it

origin: migrated from legacy ledger ("Deferred from: code review of 1-7-author-tenant-folder-provider-and-repository-binding-contract-groups (2026-05-13)"), 2026-08-24
location: CanonicalErrorCategory
reason: `CanonicalErrorCategory` retains `provider_failure_known` without any operation referencing it. Pre-existing enum value from Story 1.5/1.6 foundation; downstream stories may consume it. Deferred — revisit when the next consumer is introduced or when the bounded vocabulary is finalised.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2130 and :6062-6063 reference and exemplify provider_failure_known; it is no longer an unused category.

### DW-163: `PaginationMetadata` `pageCursor` is not bound to `filter` shape

origin: migrated from legacy ledger ("Deferred from: code review of 1-7-author-tenant-folder-provider-and-repository-binding-contract-groups (2026-05-13)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:PaginationMetadata, MetadataFilter
reason: `PaginationMetadata` `pageCursor` is not bound to `filter` shape — a cursor issued for one `filter` value can be reused with a different filter, leaking partial result counts across permission-visibility classes (timing oracle on hidden ACL entries). Pagination component is shared from Story 1.6; belongs to a cross-cutting pagination hardening story, not Story 1.7. (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:PaginationMetadata, MetadataFilter`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Server/AuditEndpoints.cs:334-362 rejects every supplied filter with filter_not_yet_supported, so no filter-bound cursor can currently be issued or replayed under a different filter.

### DW-164: `previous-spine.yaml` not proven syntactically valid by a YAML library

origin: migrated from legacy ledger ("Deferred from: code review of 1-3-seed-minimally-valid-normative-fixtures (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:ParseTopLevelYamlScalarMap
reason: `previous-spine.yaml` not proven syntactically valid by a YAML library — `ParseTopLevelYamlScalarMap` checks top-level key presence only; a malformed YAML block (tab indent, duplicate key) would not be caught. Fix requires confirming a YAML library is centrally available; defer to whichever story first adds one. (`tests/Hexalith.Folders.Testing.Tests/FixtureContractTests.cs:ParseTopLevelYamlScalarMap`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:306-316 loads previous-spine.yaml through the YamlDotNet-backed LoadYamlMapping path.

### DW-169: Submodule policy text is triplicated across `AGENTS.md`, `CLAUDE.md`, `README.md`

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: AGENTS.md
reason: Submodule policy text is triplicated across `AGENTS.md`, `CLAUDE.md`, `README.md`. Drift risk but intentional per spec for discoverability. Revisit when an automated single-source-of-truth pattern (e.g., generated includes) becomes available.
status: done 2026-08-28
archived: 2026-09-18
decision: 2026-08-28 Close as intentional — Keep discoverable copies protected by byte-identity and policy tests.
resolution: closed by human decision: Keep discoverable copies protected by byte-identity and policy tests.
decision: 2026-08-28 Close as intentional — Keep discoverable copies protected by byte-identity and policy tests.

### DW-170: `nuget.config` uses `<clear/>` then only nuget.org — destructive to corporate-mirror users but matches AC2 "no private feed assumptions". Revisit if a private feed becomes legitimate later.

origin: migrated from legacy ledger ("Deferred from: code review of 1-2-establish-root-configuration-and-submodule-policy (2026-05-12)"), 2026-08-24
location: nuget.config
reason: `nuget.config` uses `<clear/>` then only nuget.org — destructive to corporate-mirror users but matches AC2 "no private feed assumptions". Revisit if a private feed becomes legitimate later.
status: done 2026-08-28
archived: 2026-09-18
decision: 2026-08-28 Keep public-only policy — Retain deterministic nuget.org-only source mapping until a real private-feed requirement is approved.
resolution: closed by human decision: Retain deterministic nuget.org-only source mapping until a real private-feed requirement is approved.
decision: 2026-08-28 Keep public-only policy — Retain deterministic nuget.org-only source mapping until a real private-feed requirement is approved.

### DW-177: Windows case-insensitive filesystem path normalization in `ExitCriteriaDecisionArtifactTests` could hide an accidental rename to non-canonical casing (e.g., `docs/Exit-Criteria/C3-Retention.md`)

origin: migrated from legacy ledger ("Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)"), 2026-08-24
location: docs/Exit-Criteria/C3-Retention.md
reason: Windows case-insensitive filesystem path normalization in `ExitCriteriaDecisionArtifactTests` could hide an accidental rename to non-canonical casing (e.g., `docs/Exit-Criteria/C3-Retention.md`). Convention is enforced by PR diff review; revisit if a regression appears.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: .github/workflows/ci.yml:18,47,82,117,152,186 all select ubuntu-latest, so canonical path casing is exercised on case-sensitive CI.

### DW-178: C6 transition-matrix mapping artifact maps every state to a single Story 4.1 consumer

origin: migrated from legacy ledger ("Deferred from: code review of 1-4-author-phase-0-5-pre-spine-workshop-deliverables (2026-05-12)"), 2026-08-24
location: docs/exit-criteria/c6-transition-matrix-mapping.md
reason: C6 transition-matrix mapping artifact maps every state to a single Story 4.1 consumer. If 4.1 splits, all rows will need re-pointing. Already captured in the artifact's `open questions` section. Defer to story 4.1 entry. (`docs/exit-criteria/c6-transition-matrix-mapping.md`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/sprint-status.yaml:114 records Story 4.1 done, while src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:1 contains the implemented C6 transition spine; the attribution is now accurate.

### DW-180: `OperatorDispositionLabel` and `SensitiveMetadataTier` schemas defined but never referenced in this story

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:423-444
reason: `OperatorDispositionLabel` and `SensitiveMetadataTier` schemas defined but never referenced in this story (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:423-444`). Foundation vocabulary; downstream operation groups must `$ref` them when they emit operator-disposition or sensitivity-tagged data.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:7469-7471 and :8006 reference SensitiveMetadataTier, while :9635 and :9786 reference OperatorDispositionLabel; neither component is orphaned.

### DW-181: `paths: {}` empty Paths Object may produce warnings under Spectral, openapi-typescript, or NSwag

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: n/a
reason: `paths: {}` empty Paths Object may produce warnings under Spectral, openapi-typescript, or NSwag. Owned by story 1.12 (NSwag SDK generation) and story 1.14 (drift gate); validate when those stories land.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:35-58 contains real paths, and tests/Hexalith.Folders.Contracts.Tests/OpenApi/ContractSpineFoundationTests.cs:31-39 asserts the paths map is non-empty.

### DW-182: CLI exit code → CanonicalErrorCategory mapping table is not declared

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: CliExitCode
reason: CLI exit code → CanonicalErrorCategory mapping table is not declared. The 14-value `CliExitCode` enum exists but distinct categories like `response_limit_exceeded`, `query_timeout`, `redacted`, `client_configuration_error` have no exit-code assignment. Owned by story 1.13 (parity oracle).
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Cli.Tests/ErrorProjectionTests.cs:24-85 verifies canonical category-to-exit-code projection, with the public mapping also documented in docs/sdk/cli-reference.md:174.

### DW-183: No test asserts mutating-completeness fails when `idempotency_key_rule` or equivalence fields are missing (AC4's forward-looking statement). Owned by story 1.13/1.14 contract-completeness gate.

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: n/a
reason: No test asserts mutating-completeness fails when `idempotency_key_rule` or equivalence fields are missing (AC4's forward-looking statement). Owned by story 1.13/1.14 contract-completeness gate.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/ParityOracleGeneratorTests.cs:167-178 covers missing mutation metadata; tests/tools/parity-oracle-generator/Program.cs:206-230 rejects both missing mutating metadata and metadata on non-mutating operations.

### DW-185: `Idempotency-Key` parameter is declared `required: true` globally as a reusable component

origin: migrated from legacy ledger ("Deferred from: code review of 1-6-author-contract-spine-foundation-and-shared-extension-vocabulary (2026-05-12)"), 2026-08-24
location: docs/contract/contract-spine-foundation.md:13
reason: `Idempotency-Key` parameter is declared `required: true` globally as a reusable component. Downstream authors must explicitly not `$ref` it on query operations. Foundation note in `docs/contract/contract-spine-foundation.md:13` already states this; deferred to per-operation author discipline + future contract-completeness gate.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/tools/parity-oracle-generator/Program.cs:206-230 enforces that mutating operations declare idempotency metadata and non-mutating operations do not, mechanizing the query discipline.

### DW-190: `RecursiveSubmoduleViolationDetectionDoesNotTreatBroadNearbyWordingAsExemption` test outcome is correct, but the `proseLinesSeen == 1` early-break prevents line 2 from ever being evaluated

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold patch-set (2026-05-13)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
reason: `RecursiveSubmoduleViolationDetectionDoesNotTreatBroadNearbyWordingAsExemption` test outcome is correct, but the `proseLinesSeen == 1` early-break prevents line 2 from ever being evaluated — the test does not mechanically verify the claimed "broad wording is rejected" path. (`tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs`)
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:482-497 constructs the unsafe command directly and asserts the violation; the faulty early-break search is gone.

### DW-198: `<InternalsVisibleTo>` entries in `src/Hexalith.Folders.*/*.csproj` point to test assemblies

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: src/Hexalith.Folders.*/*.csproj
reason: `<InternalsVisibleTo>` entries in `src/Hexalith.Folders.*/*.csproj` point to test assemblies (`Hexalith.Folders.*.Tests`) that didn't exist in commit `eb52d15`; they exist at HEAD as later commits added them. No action needed unless a test project is later removed.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Contracts/Hexalith.Folders.Contracts.csproj:6 and the other InternalsVisibleTo declarations all map to existing test projects represented by tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:184-202.

### DW-199: `Directory.Build.props:23-26` declares MSBuild properties `HexalithEventStoreRoot` and `HexalithTenantsRoot` that nothing currently consumes

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: Directory.Build.props:23-26
reason: `Directory.Build.props:23-26` declares MSBuild properties `HexalithEventStoreRoot` and `HexalithTenantsRoot` that nothing currently consumes. Likely placeholders for future-story consumption (e.g., per-project file lists, NuGet feed switching). Revisit when a downstream story imports them.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Directory.Build.props:23-27 derives the shared *FromSource switches, and consumer projects such as src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj use the corresponding source roots and conditions.

### DW-200: Predev preflight gate `result: "fail"` recorded in `predev-preflight-2026-05-10T200403Z.json` and latest pointer due to a dirty working tree (sprint-status + story 1-6 staged)

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: predev-preflight-2026-05-10T200403Z.json
reason: Predev preflight gate `result: "fail"` recorded in `predev-preflight-2026-05-10T200403Z.json` and latest pointer due to a dirty working tree (sprint-status + story 1-6 staged). Process concern outside the code-review scope — track via the preflight gate, not in this story.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: _bmad-output/process-notes/predev-preflight-latest.json:4,66 identifies the later 2026-05-21 run and a different 19-path dirty set, so the original Story 1.6 latest-pointer condition is superseded.

### DW-201: `.gitmodules` declares 5 root submodules including `Hexalith.Memories`, but `Directory.Build.props` only detects `Hexalith.EventStore` and `Hexalith.Tenants`

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: .gitmodules
reason: `.gitmodules` declares 5 root submodules including `Hexalith.Memories`, but `Directory.Build.props` only detects `Hexalith.EventStore` and `Hexalith.Tenants`. Add a `HexalithMemoriesRoot` detector when a downstream story first references Memories.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Directory.Build.props:4-7 detects EventStore, Tenants, Memories, and FrontComposer roots; :25 specifically derives HexalithMemoriesFromSource from the detected Memories project.

### DW-202: No `Directory.Build.targets` adapted from `Hexalith.Tenants`. Acceptable deviation today; revisit when stories require SourceLink wiring or pack-time MSBuild logic.

origin: migrated from legacy ledger ("Deferred from: code review of 1-1-establish-a-consumer-buildable-module-scaffold (2026-05-11)"), 2026-08-24
location: Directory.Build.targets
reason: No `Directory.Build.targets` adapted from `Hexalith.Tenants`. Acceptable deviation today; revisit when stories require SourceLink wiring or pack-time MSBuild logic.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Directory.Build.targets:1-43 now supplies the root container/build targets that the deferred entry reported missing.

### DW-203: Do not promote Hexalith.Memories semantic indexing or RAG retrieval into MVP unless the PRD is explicitly updated

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: n/a
reason: Do not promote Hexalith.Memories semantic indexing or RAG retrieval into MVP unless the PRD is explicitly updated. Current approved course correction keeps Memories as an architecture-guided extension path.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: _bmad-output/planning-artifacts/prd.md:656-672 authorizes metadata-token recall and indexing status in MVP, while :687-702 explicitly excludes indexed body recall; FR58 at :832-836 matches that boundary.

### DW-204: Add a worker-owned Memories semantic-indexing integration story

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: n/a
reason: When a downstream story first implements Memories integration, add a dedicated story or story split for worker-owned semantic indexing:; worker-side `IFolderSemanticIndexingClient` port,; optional `Hexalith.Memories.Client.Rest` / `Hexalith.Memories.Contracts` dependency only from `Hexalith.Folders.Workers`,; Folders-owned indexing bridge projection for `file version -> Memories workflow/memory unit/status`,; stable source URI/idempotency metadata,; explicit skipped/too-large/binary/excluded statuses,; authorized RAG query facade that applies tenant access, folder ACL, path policy, sensitivity classification, and C4 limits before calling Memories.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/sprint-status.yaml:270 onward marks Stories 10.1-10.6 done, and src/Hexalith.Folders.Workers/SemanticIndexing contains the delivered worker implementation.

### DW-205: If Memories packages or project references are introduced, update root dependency detection with `HexalithMemoriesRoot` and keep submodule initialization root-level only.

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: HexalithMemoriesRoot
reason: If Memories packages or project references are introduced, update root dependency detection with `HexalithMemoriesRoot` and keep submodule initialization root-level only.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Directory.Build.props:6 and :25 define HexalithMemoriesRoot and HexalithMemoriesFromSource; src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:21 and integration tests consume them.

### DW-206: Operations-console stories may display semantic-indexing status only as metadata/projection state; they must not expose indexed content, snippets, raw Memories payloads, file browsing, or RAG…

origin: migrated from legacy ledger ("Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)"), 2026-08-24
location: n/a
reason: Operations-console stories may display semantic-indexing status only as metadata/projection state; they must not expose indexed content, snippets, raw Memories payloads, file browsing, or RAG response assembly in MVP.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2978-2979 and :8571-8572, together with _bmad-output/planning-artifacts/prd.md:698-702, codify the no-content/no-snippet indexing boundary implemented by the UI projection.

### DW-207: F1: C4 limits inclusive/exclusive ambiguity — `docs/contract/idempotency-and-parity-rules.md` cites byte limits in the Non-Mutating Read Consistency section (e.g., 1048576, 262144) without…

origin: migrated from legacy ledger ("Deferred from: code review of 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules (2026-05-13)"), 2026-08-24
location: docs/contract/idempotency-and-parity-rules.md
reason: F1: C4 limits inclusive/exclusive ambiguity — `docs/contract/idempotency-and-parity-rules.md` cites byte limits in the Non-Mutating Read Consistency section (e.g., 1048576, 262144) without specifying whether boundaries are inclusive. C4 input-limits artifact (Story 1.4) is the authority for precise boundary behavior; revisit if consumers diverge.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: docs/exit-criteria/c4-input-limits.md:19-21 explicitly states result-count, range-byte, and response-budget limit inclusivity.

### DW-209: F3: `equivalence_classification` strings not enum-typed

origin: migrated from legacy ledger ("Deferred from: code review of 1-5-finalize-idempotency-equivalence-and-adapter-parity-rules (2026-05-13)"), 2026-08-24
location: tests/fixtures/idempotency-encoding-corpus.json
reason: F3: `equivalence_classification` strings not enum-typed — long compound classification strings (~50-90 chars) in `tests/fixtures/idempotency-encoding-corpus.json` are used as identifiers without schema enum constraint; one typo silently breaks future hash-helper consumers. Tied to D7 (whether to add corpus schema); revisit when Story 1.12 helpers begin consuming the values.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/fixtures/idempotency-encoding-corpus.schema.json:41-121 requires equivalence_classification and constrains it with an enum; governance tests consume that typed field.

### DW-211: PathTooLongException nuance in `VerifyCurrentDetailed` catch filter

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:70
reason: PathTooLongException nuance in `VerifyCurrentDetailed` catch filter [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:70`] — current catch list is sufficient for the inputs the helper receives.
status: done 2026-08-25
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:61 catches IOException, which already includes PathTooLongException.

### DW-215: `HelperGenerationTargetRegeneratesWhenContractSpineChanges` test name vs assertion strength

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1625-1651
reason: `HelperGenerationTargetRegeneratesWhenContractSpineChanges` test name vs assertion strength [`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1625-1651`] — hash-difference check is sufficient evidence of regeneration; comment-only YAML mutation is acceptable.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:287-337 invokes the MSBuild generator against a mutated spine and asserts that the generated file changes.

### DW-216: `ChangedPathEvidence2` shim documentation [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:34-36`] — the shim is harmless and the NSwag duplicate-emission cause is known.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:34-36
reason: `ChangedPathEvidence2` shim documentation [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:34-36`] — the shim is harmless and the NSwag duplicate-emission cause is known.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Client/Compat/ChangedPathEvidenceShim.cs:3-16 isolates and documents the compatibility shim, including cause and removal conditions; generator commentary points to the dedicated shim.

### DW-218: `dotnet run --project Generation` runs implicit build per outer build [`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1220-1222`] — perf concern; ties into Story 1.14 CI gate review.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1220-1222
reason: `dotnet run --project Generation` runs implicit build per outer build [`src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:1220-1222`] — perf concern; ties into Story 1.14 CI gate review.
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:40-47 declares Inputs and Outputs for GenerateHexalithFoldersIdempotencyHelpers, so MSBuild skips the generator when its output is current.

### DW-219: `nswag.json` `newLineBehavior` claim about old location [`src/Hexalith.Folders.Client/nswag.json:1474-1486`] — the new location works; original-location ineffectiveness is folklore.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/nswag.json:1474-1486
reason: `nswag.json` `newLineBehavior` claim about old location [`src/Hexalith.Folders.Client/nswag.json:1474-1486`] — the new location works; original-location ineffectiveness is folklore.
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Client/nswag.json:9-12 places newLineBehavior under openApiToCSharpClient, and tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:35-38 rejects the obsolete fromDocument placement.

### DW-221: `LockWorkspaceRequest` missing `repository_binding_id` vs `PrepareWorkspaceRequest` includes it

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:374-392
reason: `LockWorkspaceRequest` missing `repository_binding_id` vs `PrepareWorkspaceRequest` includes it [`src/Hexalith.Folders.Client/Generated/HexalithFoldersIdempotencyHelpers.g.cs:374-392`] — pending spine verification; owned by Stories 1.7-1.11 if it is a spine bug.
status: done 2026-08-25
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:1490 defines LockWorkspace idempotency without repository_binding_id, and generated helper lines 428-440 match that canonical contract.

### DW-222: `Render` uses `AppendLine` + final `ReplaceLineEndings("\n")` [`src/Hexalith.Folders.Client/Generation/Program.cs:929`] — Round 2 P28 was marked done with this approach; functionally deterministic.

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers Round 3 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:929
reason: `Render` uses `AppendLine` + final `ReplaceLineEndings("\n")` [`src/Hexalith.Folders.Client/Generation/Program.cs:929`] — Round 2 P28 was marked done with this approach; functionally deterministic.
status: done 2026-09-01
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Client/Generation/Program.cs:534 normalizes rendered output with ReplaceLineEndings("\n"), providing deterministic line endings.

### DW-228: `oneOf` traversal without explicit OpenAPI `discriminator`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: src/Hexalith.Folders.Client/Generation/Program.cs:215-221
reason: `oneOf` traversal without explicit OpenAPI `discriminator` [`src/Hexalith.Folders.Client/Generation/Program.cs:215-221`] — `FileMutationRequest` is the only schema needing this today and is handled via `SpecialFields.Registry` projection. Story 1.13 owns generic discriminator-driven `oneOf` resolution.
status: done 2026-08-28
archived: 2026-09-18
decision: 2026-08-28 Keep special case — Document the two-axis const union as the reason for retaining SpecialFields until another hash-relevant union requires generalization.
resolution: closed by human decision: Document the two-axis const union as the reason for retaining SpecialFields until another hash-relevant union requires generalization.
decision: 2026-08-28 Keep special case — Document the two-axis const union as the reason for retaining SpecialFields until another hash-relevant union requires generalization.

### DW-230: Tempdir cleanup catches that swallow `IOException`/`UnauthorizedAccessException`

origin: migrated from legacy ledger ("Deferred from: code review of 1-12-wire-nswag-sdk-generation-with-idempotency-helpers round 4 (2026-05-16)"), 2026-08-24
location: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1640-1653
reason: Tempdir cleanup catches that swallow `IOException`/`UnauthorizedAccessException` [`tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:1640-1653`] — overlaps with the WaitForExit-orphan patch shipped in the same round; resolving the orphan process also resolves the cleanup races.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Client.Tests/ClientGenerationTests.cs:308-331 kills and drains a timed-out generator process before temporary-directory cleanup, eliminating the recorded orphan-process cleanup race.

### DW-231: YamlDotNet duplicate-keys detection [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `LoadYamlMapping`]

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: YamlDotNet duplicate-keys detection [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `LoadYamlMapping`] — overlaps prior JSON-duplicate-keys defer; fixtures are gate-owned and deterministic.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs:800-805 loads mappings through YamlStream/YamlMappingNode, whose pinned representation loader rejects duplicate mapping keys.

### DW-234: `InventoryChannel.Clone()` use-after-dispose latent risk

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `InventoryChannel.Clone()` use-after-dispose latent risk [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `InventoryChannel`] — current code correct per .NET docs; revisit if accessor returns raw `JsonElement`.
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs:506-514 returns channel.Clone() before the owning JsonDocument is disposed, eliminating the alleged JsonElement use-after-dispose risk.

### DW-238: Workflow doc claim `checkout with submodules: false` not visibly enforced in this diff

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates round 3 (2026-05-18)"), 2026-08-24
location: .github/workflows/contract-spine.yml
reason: Workflow doc claim `checkout with submodules: false` not visibly enforced in this diff [`.github/workflows/contract-spine.yml`, `docs/contract/safety-invariant-ci-gates.md`] — existing checkout step not modified by this story; verify when 1.14 ownership consolidates.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: .github/workflows/contract-spine.yml:20-24 explicitly configures actions/checkout with submodules: false.

### DW-246: `AssertRepositoryRelativePath` does not reject UNC paths

origin: migrated from legacy ledger ("Deferred from: code review of 1-15-wire-safety-invariant-ci-gates (2026-05-18, Round 4)"), 2026-08-24
location: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs
reason: `AssertRepositoryRelativePath` does not reject UNC paths (`//server/share/...`) or extended-length prefixes (`\?\D:\...`) [`tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs` `AssertRepositoryRelativePath`] — no current callers can produce these path shapes; tighten when a new include_root source is introduced.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/OpenApi/SafetyInvariantGateTests.cs:655-663 rejects fully-qualified paths, leading slashes, and every backslash; those checks reject UNC and extended-length prefixes.

### DW-253: `accesscontrol.yaml` ships `defaultAction: allow` for every Dapr app with no environment guard

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml
reason: `accesscontrol.yaml` ships `defaultAction: allow` for every Dapr app with no environment guard [`src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml`] — local-dev scaffold; production deny-by-default access control belongs to Story 7.1.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: deploy/dapr/production/accesscontrol.yaml:11-18 defines production access control with defaultAction: deny and per-caller deny defaults.

### DW-254: JWT audience hard-coded to `hexalith-eventstore` across all four services, `SigningKey=""`, `RequireHttpsMetadata=false`

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.AppHost/Program.cs:51-53
reason: JWT audience hard-coded to `hexalith-eventstore` across all four services, `SigningKey=""`, `RequireHttpsMetadata=false` [`src/Hexalith.Folders.AppHost/Program.cs:51-53`] — local-dev AppHost composition; production OIDC + secret-store wiring belongs to Story 7.2. Audience-per-app-id correctness must be revisited there.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Server/Authentication/FoldersAuthenticationServiceCollectionExtensions.cs:94-106 validates issuer/audience and :128-138 rejects blank audience or non-HTTPS metadata outside Development/Test; production secret-store deployment assets are present.

### DW-255: `Workers/Program.cs` is an empty host with no `IHostedService`, no Tenants subscription, no work

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.Workers/Program.cs
reason: `Workers/Program.cs` is an empty host with no `IHostedService`, no Tenants subscription, no work [`src/Hexalith.Folders.Workers/Program.cs`] — workers do nothing in Story 2.1; Story 2.9 ("react to Tenants events through worker handlers") owns the subscription pipeline.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Workers/Program.cs:5-17 now adds service defaults and tenant event workers, enables CloudEvents, and maps subscription, worker, and health endpoints.

### DW-257: Hard-coded `localhost:6379` Redis with no `AddRedis()` resource in AppHost

origin: migrated from legacy ledger ("Deferred from: code review of story-2.1 (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders.Aspire/FoldersAspireModule.cs
reason: Hard-coded `localhost:6379` Redis with no `AddRedis()` resource in AppHost [`src/Hexalith.Folders.Aspire/FoldersAspireModule.cs`] — distributed deployment wiring belongs to Story 7.x; local dev still works because Dapr's Redis state-store default matches.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Aspire/FoldersAspireModule.cs no longer hard-codes localhost:6379; src/Hexalith.Folders.AppHost/Program.cs:51-60 supplies the shared EventStore topology resources.

### DW-262: `OrganizationStreamName.IsValidSegment` uses `ToLowerInvariant` for canonical-casing check

origin: migrated from legacy ledger ("Deferred from: code review of 2-2-implement-organization-aggregate-acl-baseline (2026-05-18)"), 2026-08-24
location: src/Hexalith.Folders/Aggregates/Organization/OrganizationStreamName.cs:53
reason: `OrganizationStreamName.IsValidSegment` uses `ToLowerInvariant` for canonical-casing check [`src/Hexalith.Folders/Aggregates/Organization/OrganizationStreamName.cs:53`] — correct for ASCII inputs, but an explicit ASCII whitelist would be safer against Unicode lookalikes (Turkish dotted i, fullwidth letters). Subsumed by the segment-charset patch if the team picks the whitelist fix there.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Aggregates/Organization/OrganizationStreamName.cs:54-60 now validates canonical segments with the explicit ASCII regex ^[a-z0-9._-]+$.

### DW-268: Preflight result `fail` in `_bmad-output/process-notes/predev-preflight-2026-05-19T120131Z.json` (36 dirty paths)

origin: migrated from legacy ledger ("Deferred from: code review of story-2.6 (2026-05-19)"), 2026-08-24
location: _bmad-output/process-notes/predev-preflight-2026-05-19T120131Z.json
reason: Preflight result `fail` in `_bmad-output/process-notes/predev-preflight-2026-05-19T120131Z.json` (36 dirty paths) — captured pre-commit state of story 2.6's own files; clean post-commit at `ed657e5`. Re-run preflight before flipping story 2.6 to `done`.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit ed657e5 records the clean post-commit state that the historical preflight note said was required before Story 2.6 closure.

### DW-269: Restore the systemic test-host composition baseline

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: test hosts using AddFoldersServer/MapFoldersServerEndpoints
reason: **SYSTEMIC TEST-HOST RED — NOW OWNED BY STORY 7.18 (NOT a 2-8b defect):** Test hosts calling `AddFoldersServer()`+`MapFoldersServerEndpoints()` without `AddServiceDefaults()` fail DI validation because `FoldersAuthSchemeValidator` needs `IAuthenticationSchemeProvider` and `MapDefaultEndpoints` needs `HealthCheckService`. **Re-measured 2026-05-31 (xUnit v3 in-process runner): `Server.Tests` Total 433, Failed 339, Passed 94, Skipped 0** (single auth/health DI-validation cause, fail-closed at `WebApplicationBuilder.Build()`); plus IntegrationTests 11 (Epic 5 Golden/MixedSurface) and Folders.Tests 2 (Epic 3 provider-boundary guards), documented 2026-05-31. Introduced by later stories (`6e816ce` auth validator + ServiceDefaults health checks). Mechanical fix: add `AddAuthentication()`+`AddHealthChecks()` (or a shared helper) to each affected host — same fix applied to 2-8b's own host. **CORRECTION:** this is a *distinct, ~50× larger* blocker than the "4–6 epic-1 CLI negative-scope reds in `Contracts.Tests`" that the Epic 7 retro named as the historical-reds item — different root cause (test-host composition gap vs CLI-now-exists). Resolution owned by **Story 7.18** (`7-18-restore-test-host-composition-baseline.md`), which reopens Epic 7. See `planning-artifacts/sprint-change-proposal-2026-05-31-test-host-composition-baseline.md`.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: _bmad-output/implementation-artifacts/sprint-status.yaml:173-174 records Story 7.18 done with the restored Server.Tests composition baseline at 434 passed, 0 failed, 0 skipped.

### DW-270: P1: `InMemoryFolderRepository.EventsAppended`/`ResetAppendCounters` are not lock-guarded for reads

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: InMemoryFolderRepository.EventsAppended
reason: P1: `InMemoryFolderRepository.EventsAppended`/`ResetAppendCounters` are not lock-guarded for reads. Now `internal` (test-only via InternalsVisibleTo), single-threaded per test host. Already tracked rounds 3/4; revisit when an EventStore-backed repository replaces the in-memory default.
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: Commit f9cf754; src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:109-117 and :257-262 now guard both EventsAppended reads and ResetAppendCounters with _gate.

### DW-271: P4: No foreign-tenant-smuggling integration row exercising `HasCompetingClientTenant`/`TenantMismatch` end-to-end

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: HasCompetingClientTenant
reason: P4: No foreign-tenant-smuggling integration row exercising `HasCompetingClientTenant`/`TenantMismatch` end-to-end. Already deferred round 3 with documented defense-in-depth rationale (gate-unit coverage + layered-auth tenant comparison at the request handler).
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: Commit 1dcd53d; tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs:343 now exercises authenticated-tenant versus envelope-tenant mismatch through REST, gateway, and /process with zero append.

### DW-272: W1: `CancellationToken` cannot propagate into `IDomainProcessor.ProcessAsync`

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: IDomainProcessor.ProcessAsync
reason: W1: `CancellationToken` cannot propagate into `IDomainProcessor.ProcessAsync` — interface has no CT parameter; `FolderDomainProcessor` passes `CancellationToken.None` to all evidence providers. ADR 0001 explicitly accepts this; evidence providers are deterministic in-memory operations. Deferred — revisit when EventStore framework's `IDomainProcessor` gains a CT parameter.
status: done 2026-08-25
archived: 2026-09-18
resolution: closed by human decision: Keep cancellation at surrounding request and provider paths because evidence calls at this seam are bounded and deterministic.
decision: 2026-08-25 Retain ADR tradeoff — Keep cancellation at surrounding request and provider paths because evidence calls at this seam are bounded and deterministic.

### DW-276: W6: TOCTOU window between `TryGetIdempotencyFingerprint` and `AppendIfFingerprintAbsent` in both gates

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: TryGetIdempotencyFingerprint
reason: W6: TOCTOU window between `TryGetIdempotencyFingerprint` and `AppendIfFingerprintAbsent` in both gates — optimistic concurrency design; by-design for the current in-memory repository. Deferred until an EventStore-backed repository provides transaction-level idempotency.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs:121-139 uses atomic AppendIfFingerprintAbsent outcome handling, closing the recorded check-then-append correctness window.

### DW-277: W7: `FolderArchiveTenantGate` calls `BindArchiveDecisionFingerprint` before `EvaluatePolicy`; `validation.IdempotencyFingerprint!` null-forgiving dereference would NRE if a custom validator…

origin: migrated from legacy ledger ("Deferred from: code review of 2-8b-wire-folder-domain-processor (2026-05-31)"), 2026-08-24
location: validation.IdempotencyFingerprint!
reason: W7: `FolderArchiveTenantGate` calls `BindArchiveDecisionFingerprint` before `EvaluatePolicy`; `validation.IdempotencyFingerprint!` null-forgiving dereference would NRE if a custom validator returns `IsAccepted=true` with null fingerprint. Pre-existing; baseline policy provider always supplies valid fingerprint.
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs:65 validates the command, ACL, and policy before binding the archive decision fingerprint, and the gate has no custom-validator seam.

### DW-279: `hasMore`/`nextCursor` in `ContextSearchQueryHandler.cs:211-212` are derived from the index's raw `TotalCount` (before the Folders-side security trim + hydration), so a page whose remaining…

origin: migrated from legacy ledger ("Deferred from: code review of story-10.5 (2026-06-24)"), 2026-08-24
location: ContextSearchQueryHandler.cs:211-212
reason: `hasMore`/`nextCursor` in `ContextSearchQueryHandler.cs:211-212` are derived from the index's raw `TotalCount` (before the Folders-side security trim + hydration), so a page whose remaining matches are all foreign/stale rows still reports `hasMore=true` and emits a cursor that yields an empty next page. New code, but UX-only and acceptable under the no-cross-tenant-existence-disclosure rule (an aggregate boolean over the caller's own tenant scope). Optional fix: emit a cursor only when the source returned a full pre-trim page (`Hits.Count == limit`).
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:217-221 emits a continuation cursor only when the post-filter item count reaches the requested limit.

### DW-283: Direct Memories egress bypasses the Dapr invoke allow-rule

origin: migrated from legacy ledger ("Deferred from: code review of story-10.5 (2026-06-24)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:91; deploy/dapr/production/accesscontrol.yaml
reason: **(#5, decision: accept as documented)** The production `folders → memories GET /api/search` Dapr invoke allow-rule does not bind the facade's actual egress, because `AddMemoriesClient` is a direct base-address `HttpClient` (`Memories:BaseAddress` + bearer token), not a Dapr service-invoke client. Accepted by Jerome (2026-06-24) as already documented in `architecture.md#134`: the allow-rule + its conformance negative-controls are operative only if `Memories:BaseAddress` is configured as the sidecar invoke route; otherwise API-token control governs that egress. Revisit (pin `Memories__BaseAddress` to the sidecar route + add a conformance assertion) if/when the facade egress is moved onto the Dapr sidecar.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:98-113 defaults Memories egress to the Dapr service-invocation route, with direct absolute URLs retained only as an explicit override.

### DW-285: Dapr invoke allow-rule is conditional because the facade uses a direct Memories base-address client

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:91
reason: Dapr invoke allow-rule is conditional because the facade uses a direct Memories base-address client (`src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:91`, `deploy/dapr/production/accesscontrol.yaml`) — accepted in the story's 2026-06-24 review resolutions and documented in architecture; revisit when egress is pinned to sidecar invocation.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:98-113 now constructs the default Memories endpoint through /v1.0/invoke/memories/method/, binding it to Dapr access control.

### DW-287: Required test lane still has the pre-existing `.slnx` inventory red

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: _bmad-output/implementation-artifacts/10-5-expose-authorized-folders-query-facade-over-memories.md
reason: Required test lane still has the pre-existing `.slnx` inventory red (`_bmad-output/implementation-artifacts/10-5-expose-authorized-folders-query-facade-over-memories.md`) — pre-existing and documented in the story completion notes; do not treat as caused by the 10.5 facade chunk.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: Commit 9631820 updated solution inventory expectations; the current Hexalith.Folders.slnx project set and ScaffoldContractTests expected set both contain 50 projects.

### DW-288: Harden the canonical submodule command contract to parse and compare the exact nonrecursive command token set.

origin: migrated from legacy ledger ("Deferred from: code review of 10-5-expose-authorized-folders-query-facade-over-memories (2026-06-26)"), 2026-08-24
location: tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs
source_spec: _bmad-output/implementation-artifacts/spec-run-tests-and-fix-failures.md
reason: The pre-existing substring-based assertion in `ScaffoldContractTests.AssertCanonicalInitCommandPresent` can accept comments, recursive commands, or extra nested paths that merely contain the required substrings.
status: done 2026-08-24
archived: 2026-09-18
resolution: already resolved: tests/Hexalith.Folders.Contracts.Tests/ScaffoldContractTests.cs:528-558 verifies unsafe recursive/comment variants are rejected, backed by the canonical token parser at :755 onward.

### DW-294: Forgejo silently ignores the now-required provider idempotency admission.

origin: code review of spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior (2026-08-25)
location: src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs
source_spec: _bmad-output/implementation-artifacts/spec-3-10-github-repository-provisioning-binding-and-branch-ref-behavior.md
reason: Story 3.10 added `ProviderIdempotencyAdmission` to `ProviderRepositoryCreationRequest` and `ProviderRepositoryBindingRequest`, and `GitHubProvider` now gates on it. `ForgejoProvider` accepts the member and enforces nothing (`grep IdempotencyAdmission src/Hexalith.Folders/Providers/Forgejo/` returns no hits), so once a caller emits a real disposition a replayed or expired intent would execute live against Forgejo. This is not a regression — Forgejo enforced no idempotency before either — and Story 3.12 owns the Forgejo slice. No test pins the deliberate no-op today.
status: done 2026-08-28
archived: 2026-09-18
resolution: already resolved: Commit 485fcf38; src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:282-288 and :775-864 enforce creation and binding idempotency admission, with expiry and replay coverage at tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoProviderTests.cs:704-954.

### DW-318: The parity double's rejection reason code matches neither spelling the real EventStore gateway produces, so its "production fidelity" claim is unverified against the actual gateway hop.
origin: spec-deferred 7cb3e93d48f6
location: references/Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/DomainCommandRejectedExceptionHandler.cs:43
source_spec: `spec-archive-gateway-canonical-mapping.md`
severity: medium
reason: In production a Folders rejection reaches EventStoreGatewayException through DomainCommandRejectedExceptionHandler, which derives ProblemDetails reasonCode from DomainRejectionProblemCatalog.FromRejectionType(rejection.RejectionType) -- a kebab-case name derived from the rejection EVENT TYPE (folder-command-rejected), with a status drawn from a small set. The FolderResultCode never reaches the gateway exception in production at all. The double previously emitted the PascalCase enum name and now emits the snake_case canonical category; neither is the production wire spelling. Reconciling the two vocabularies would change the EventStore submodule or the caller-visible canonical error vocabulary, both of which this story's Block If and Never lists forbid.
status: done 2026-09-02
archived: 2026-09-18
resolution: closed by human decision: Treat the parity double as adapter-specific, remove its production-fidelity claim, and leave the production vocabulary unchanged.
decision: 2026-09-02 Document simulator — Treat the parity double as adapter-specific, remove its production-fidelity claim, and leave the production vocabulary unchanged.

### DW-336: Pre-existing Forgejo provider syntax errors block the normal Client.Tests project build.
origin: spec-deferred 14115ac2c839
location: src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808
source_spec: `spec-generated-client-conformance.md`
severity: high
reason: `dotnet build tests/Hexalith.Folders.Client.Tests/Hexalith.Folders.Client.Tests.csproj -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` fails before compiling the changed tests with CS1513 at ForgejoProvider.cs:808 and CS1519 at ForgejoProvider.cs:821. A source-isolated client conformance project builds and runs the changed tests successfully.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 3309644; src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808-823 now closes IsReplayEvidenceWellFormed correctly.

### DW-342: The unchanged Forgejo provider source blocks a clean rebuild of the core test assembly.
origin: spec-deferred 3f2fb6265e4e
location: src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808
source_spec: `spec-lifecycle-test-hygiene.md`
severity: high
reason: The baseline contains a brace error at ForgejoProvider.cs:808, so the in-scope test assembly was rebuilt only with that pre-existing source problem isolated out of tree. No production source was changed by this bundle.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 3309644; src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808-823 repairs the same duplicate syntax defect.

### DW-347: Reconcile EventStore nested FrontComposer/Memories gitlink objects with independently selected root gitlinks when EventStore advances.

origin: migrated from legacy ledger (""), 2026-09-05
location: references/Hexalith.EventStore/references/Hexalith.FrontComposer; references/Hexalith.EventStore/references/Hexalith.Memories
source_spec: `spec-update-submodules-and-hexalith-package-versions.md`
reason: EventStore recorded nested FrontComposer `20d62abd…` while the selected root gitlink was `d71790bb…`, and nested Memories `3a7a7025…` while the advanced root gitlink was `2f85536d…`; the Memories-only root bump correctly left those pre-existing EventStore nested pointers unchanged.
status: done 2026-09-05
archived: 2026-09-18
decision: 2026-09-05 Retain independent pins — Root repositories own compatible dependency selections independently; current nonrecursive development and the EventStore build are unaffected.
resolution: closed by human decision: Root repositories own compatible dependency selections independently; current nonrecursive development and the EventStore build are unaffected.
decision: 2026-09-05 Retain independent pins — Root repositories own compatible dependency selections independently; current nonrecursive development and the EventStore build are unaffected.

### DW-348: Restore Debug build of EventStore AggregateActor (`InspectPublicationRecoverySaveFailureAsync` missing on current EventStore tip).

origin: migrated from legacy ledger (""), 2026-09-05
location: references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Actors/AggregateActor.cs:1114,3212
source_spec: `spec-update-submodules-and-hexalith-package-versions.md`
reason: The unchanged EventStore gitlink fails `dotnet build` with CS0103 at AggregateActor.cs:1114 and :3212 because `InspectPublicationRecoverySaveFailureAsync` is missing; the failure pre-existed and was not caused by the Memories pointer advance.
status: done 2026-09-05
archived: 2026-09-18
resolution: already resolved: Commit 8532f2b advanced references/Hexalith.EventStore from broken 5583e207 to 4ae9cee1; dotnet build src/Hexalith.EventStore.Server/Hexalith.EventStore.Server.csproj --no-restore -m:1 -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 now succeeds with 0 warnings and 0 errors.

### DW-350: ForgejoProvider has an extra closing brace that blocks Release rebuild of Hexalith.Folders

origin: bmad-build review of Story 3.11 live-evidence path C waiver, 2026-09-06
location: src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:821
source_spec: `_bmad-output/implementation-artifacts/spec-3-11-live-github-evidence-operator-action.md`
reason: HEAD fails `dotnet build` of Hexalith.Folders with CS1513/CS1519: extra `}` after `HasNoPriorOutcomeFields`. Pre-existing on main (Story 3.12 lineage); blocks rebuilding GitHubDependencyGuardTests in this session. Outside path C waiver scope.
status: done 2026-09-06
archived: 2026-09-18
resolution: already resolved: Commit 3309644; src/Hexalith.Folders/Providers/Forgejo/ForgejoProvider.cs:808-823 now has the closing brace before HasNoPriorOutcomeFields.

