# Story 8.1: Implement the 8 missing Bucket-A canonical REST server routes

Status: backlog

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->
<!-- Run the create-story refinement pass before dev to expand ACs against each operation's existing aggregate/query handler. -->

## Story

As an API consumer,
I want every canonical operation that the SDK, CLI, and MCP already wrap to have a working REST server route,
So that cross-surface parity is real and CLI/MCP calls do not hit unimplemented endpoints (latent 404).

## Context

Verified 2026-06-22 by an adversarial parity workflow (enumerating the OpenAPI spine, parity oracle, server routes, and CLI/MCP adapters; high-confidence, refutation-tested): canonical set = **47**; REST = **32/47**; SDK = 47/47; MCP = 47/47; CLI = 40/47 (7 diagnostics are MCP-only by design). **15 operations lack a server route** even though the spine declares them and the SDK/CLI/MCP wrap them — so CLI/MCP calls to these would 404 at runtime. This story closes **Bucket A** (8 non-diagnostics ops). Bucket B (7 diagnostics) is Story 8.2.

This corrects the readiness report, which claimed 28/47 (19 missing) and falsely listed the audit family + several status queries as missing (they are fully implemented).

## Operations to implement (spine line refs verified)

| operationId | Method / Path | FR | Spine line |
|---|---|---|---|
| `CreateFolder` | POST `/api/v1/folders` | FR11 | 40 |
| `ListFolderAclEntries` | GET `/api/v1/folders/{folderId}/acl` | FR5, FR6 | 290 |
| `UpdateFolderAclEntry` | PUT `/api/v1/folders/{folderId}/acl/{aclEntryId}` | FR5 | 359 |
| `ConfigureProviderBinding` | PUT `/api/v1/provider-bindings/{providerBindingRef}` | FR15 | 514 |
| `GetProviderBinding` | GET `/api/v1/provider-bindings/{providerBindingRef}` | FR15 | 599 |
| `GetRepositoryBinding` | GET `/api/v1/folders/{folderId}/repository-bindings/{repositoryBindingId}` | FR2, FR39 | 1027 |
| `GetWorkspaceRetryEligibility` | GET `.../workspaces/{workspaceId}/retry-eligibility` | FR26, FR46 | 1817 |
| `GetWorkspaceTransitionEvidence` | GET `.../workspaces/{workspaceId}/transition-evidence` | FR46, FR28 | 1914 |

## Acceptance Criteria

1. **Given** the OpenAPI Contract Spine already declares these 8 operations and the SDK/CLI/MCP already wrap them, **when** each server route is implemented under `Hexalith.Folders.Server` against the existing aggregates/query handlers (no spine change), **then** all 8 operations respond on REST with canonical envelopes, problem categories, and idempotency behavior matching the spine.
2. **Given** the mutating ops (`CreateFolder`, `UpdateFolderAclEntry`, `ConfigureProviderBinding`), **when** acceptance is tested, **then** an in-process integration test that does NOT mock `IEventStoreGatewayClient` proves the real REST → gateway → `/process` → processor → persistence path (per the standing rule in project-context Testing Rules).
3. **Given** the read ops, **when** invoked, **then** layered authorization, safe denial, and metadata-only responses hold (no secret/path/credential leakage).
4. **Given** the contract-spine drift gate and the C13 parity oracle, **when** the routes land, **then** both pass and REST coverage reaches **40/47** (Bucket A closed).
5. **Given** CLI/MCP already wrap these SDK methods, **when** the routes land, **then** a CLI/MCP integration smoke confirms the previously-latent 404s now succeed.

## References

- Verified parity result: workflow `verify-epic5-parity-gap` (2026-06-22).
- Spine: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`.
- Server routes: `src/Hexalith.Folders.Server/{FoldersDomainServiceEndpoints,AuditEndpoints,ProviderReadinessEndpoints}.cs`.
- SDK: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`.
- Sprint Change Proposal: `../planning-artifacts/sprint-change-proposal-2026-06-22.md`.
