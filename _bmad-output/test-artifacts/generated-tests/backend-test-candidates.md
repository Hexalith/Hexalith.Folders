# Backend Test Candidates

Generated: 2026-05-18

These backend candidates map Epic 2 acceptance criteria into executable xUnit test groups. They are intentionally parked as candidates because production domain and endpoint implementations are not present yet.

## Story 2.1 - Tenants Integration And Fail-Closed Projection

| Test Group | Priority | Candidate Tests |
|---|---:|---|
| `TenantAccessProjectionEventHandlingTests` | P0 | applies allowed Tenants event types idempotently; ignores unknown event types; rejects malformed envelopes; records replay conflict for duplicate `MessageId` with divergent metadata |
| `TenantAccessAuthorizerTests` | P0 | returns `allowed`, `denied`, `stale_projection`, `unavailable_projection`, `unknown_tenant`, `disabled_tenant`, `malformed_evidence`, `tenant_mismatch`, `missing_authoritative_tenant`, and `replay_conflict` |
| `TenantAccessSideEffectBoundaryTests` | P0 | proves mutation rejection happens before folder/workspace/credential/repository/lock/provider/cache/audit access |
| `DaprSubscriptionShapeTests` | P1 | verifies `/tenants/events`, `pubsub`, `system.tenants.events`, `UseCloudEvents()`, `MapSubscribeHandler()`, and stable topic metadata without live Dapr |
| `AppHostStableAppIdTests` | P1 | verifies `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui` app IDs structurally |

## Story 2.2 - Organization ACL Baseline

| Test Group | Priority | Candidate Tests |
|---|---:|---|
| `OrganizationAclCommandValidationTests` | P0 | rejects reserved `system` tenant; rejects malformed principals; rejects unsupported actions; rejects aliases/mixed-case/localized labels |
| `OrganizationAclTenantEvidenceGateTests` | P0 | rejects every non-allowed tenant evidence result before stream-name construction or stream loading |
| `OrganizationAclIdempotencyTests` | P0 | equivalent replay returns same logical result; same key plus materially different canonical payload returns `idempotency_conflict` |
| `OrganizationAclEffectivePermissionTests` | P1 | derives deterministic baseline permissions by tenant, organization, principal kind, principal ID, and action |
| `OrganizationAclMetadataLeakageTests` | P0 | scans results/events/diagnostics for forbidden sentinel values |

## Story 2.3 - Create Folders

| Test Group | Priority | Candidate Tests |
|---|---:|---|
| `FolderCreationCommandValidationTests` | P0 | validates opaque folder ID; rejects invalid metadata before durable key construction; keeps stream shape `{tenant}:folders:{folderId}` |
| `FolderCreationTenantEvidenceGateTests` | P0 | rejects stale/unavailable/disabled/unknown/malformed/future/replay-conflicting/mismatched tenant evidence before stream-name construction |
| `FolderCreationAclGateTests` | P0 | rejects missing `create_folder` permission before idempotency lookup, stream load, append, projection, diagnostics, or audit |
| `FolderCreationIdempotencyTests` | P0 | equivalent replay has no duplicate event; conflicting payload returns `idempotency_conflict`; unavailable idempotency fails closed |
| `FolderCreationProjectionReplayTests` | P1 | replays `FolderCreated` into tenant-scoped active lifecycle state using stream/envelope tenant authority |

## Story 2.4 - Grant And Revoke Folder Access

| Test Group | Priority | Candidate Tests |
|---|---:|---|
| `FolderAccessCommandValidationTests` | P0 | rejects invalid principal/action; rejects `create_folder` override; collapses exact duplicate entries; rejects grant/revoke conflict |
| `FolderAccessAdministratorAclGateTests` | P0 | requires ACL administrator evidence before observing folder stream or prior grants |
| `FolderAccessIdempotencyTests` | P0 | grant and revoke never share equivalence; retries are deterministic; idempotency store outage fails closed |
| `FolderAccessProjectionReplayTests` | P0 | revoke removes or denies action and carries C7 freshness evidence |
| `FolderAccessMetadataLeakageTests` | P0 | ensures no names, emails, repository/branch/path values, tokens, diffs, file contents, generated context, raw headers, or unauthorized identifiers leak |

## Story 2.5 - Effective Permissions

| Test Group | Priority | Candidate Tests |
|---|---:|---|
| `EffectivePermissionsAuthorizationGateTests` | P0 | tenant access is evaluated before folder, ACL, lifecycle, task/workspace, diagnostics, audit, provider, repository, lock, or file lookup |
| `EffectivePermissionsLayeringTests` | P0 | applies organization baseline, folder grants, folder revocations, and lifecycle/task narrowing deterministically |
| `EffectivePermissionsRevocationFreshnessTests` | P0 | revoked action is absent or denied and includes C7 revocation watermark/timestamp |
| `EffectivePermissionsReadModelFreshnessTests` | P0 | stale/unavailable permission projection fails closed instead of scanning aggregates or defaulting allowed |
| `EffectivePermissionsContractAlignmentTests` | P1 | response shape matches `EffectivePermissions` and safe denial components in the Contract Spine |

## Fixture Needs

- In-memory tenant evidence builder with all Story 2.1 outcome codes.
- In-memory ACL evidence builder for allowed/denied/stale/unavailable/malformed/folder-mismatch outcomes.
- Spy store that records whether stream names, loads, appends, projections, diagnostics, audit, provider, repository, workspace, lock, or file access were attempted.
- Sentinel leakage corpus helper for forbidden strings.
- Culture-invariant canonical payload builder for idempotency tests.
