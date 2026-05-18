# API Test Candidates

Generated: 2026-05-18

These API candidates are intentionally non-executable until Epic 2 server endpoints are implemented. Promote them into `tests/Hexalith.Folders.Server.Tests` or a future API test project when the target handlers exist.

## Contract Spine Endpoints

| Operation | Priority | Candidate Tests |
|---|---:|---|
| `CreateFolder` | P0 | accepts valid tenant-scoped request; rejects missing idempotency key; rejects malformed idempotency key; returns safe denial for tenant mismatch; never exposes folder existence on unauthorized request |
| `GetFolderLifecycleStatus` | P1 | returns metadata-only lifecycle status for authorized actor; omits idempotency; reports freshness; returns safe 403/404 envelope without resource-specific details |
| `ListFolderAclEntries` | P1 | paginates ACL metadata only; omits idempotency; applies freshness metadata; denies before listing ACL entries when tenant evidence is not allowed |
| `UpdateFolderAclEntry` | P0 | accepts grant/revoke metadata; rejects unsupported action; rejects `create_folder` folder override; enforces idempotency equivalence; returns safe denial before ACL observation |
| `GetEffectivePermissions` | P0 | returns authorized effective permission layers; removes revoked actions; reports revocation freshness; fails closed on stale/unavailable permission read model |

## P0 API Assertions

1. Safe denial envelopes for `401`, `403`, and protected `404` contain only stable redaction fields and no resource-identifying evidence.
2. Mutating operations require `Idempotency-Key`, `X-Correlation-Id`, and `X-Hexalith-Task-Id` as declared by the Contract Spine.
3. Read-only operations reject or ignore idempotency input according to their Contract Spine metadata.
4. All responses and Problem Details omit provider tokens, credential material, repository names, branch names, file paths, file contents, diffs, generated context payloads, raw auth headers, and unauthorized resource identifiers.
5. Tenant authority is never accepted from request body, route, query, or client-controlled tenant headers.

## Provider Endpoint Map

| Consumer Endpoint | Provider File | Route | Validation Schema | Response Type | OpenAPI Spec |
|---|---|---|---|---|---|
| `POST /api/v1/folders` | TODO - endpoint not implemented yet | `POST /api/v1/folders` | `CreateFolderRequest` | `AcceptedCommand` | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` |
| `GET /api/v1/folders/{folderId}/lifecycle-status` | TODO - endpoint not implemented yet | `GET /api/v1/folders/{folderId}/lifecycle-status` | parameters | `FolderLifecycleStatus` | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` |
| `GET /api/v1/folders/{folderId}/acl` | TODO - endpoint not implemented yet | `GET /api/v1/folders/{folderId}/acl` | parameters | `FolderAclEntryList` | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` |
| `PUT /api/v1/folders/{folderId}/acl/{aclEntryId}` | TODO - endpoint not implemented yet | `PUT /api/v1/folders/{folderId}/acl/{aclEntryId}` | `UpdateFolderAclEntryRequest` | `AcceptedCommand` | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` |
| `GET /api/v1/folders/{folderId}/effective-permissions` | TODO - endpoint not implemented yet | `GET /api/v1/folders/{folderId}/effective-permissions` | parameters | `EffectivePermissions` | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` |

Provider scrutiny status: pending handler implementation. Use the Contract Spine as the only current source of truth; cross-reference handler code once endpoints exist.
