---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - "_bmad-output/planning-artifacts/prd.md"
  - "_bmad-output/planning-artifacts/architecture.md"
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
date: '2026-05-10'
status: 'complete'
completedAt: '2026-05-10'
epicCount: 7
storyCount: 60
---

# Hexalith.Folders - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.Folders, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

#### Capability Contract Terms

- FR1: Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology.
- FR2: Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility.
- FR3: Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries.

#### Authorization and Tenant Boundary

- FR4: Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks.
- FR5: Tenant administrators can grant folder access to users, groups, roles, and delegated service agents.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

#### Folder Lifecycle

- FR11: Authorized actors can create logical folders within a tenant.
- FR12: Authorized actors can inspect folder lifecycle and binding status.
- FR13: Authorized actors can archive folders when policy allows.
- FR14: The system can preserve audit and status evidence for archived folders.

#### Provider Readiness and Repository Binding

- FR15: Platform engineers can configure supported Git provider bindings and credential references for a tenant.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass.
- FR19: Authorized actors can bind a folder to an existing repository where supported.
- FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
- FR23: Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior.

#### Workspace and Lock Lifecycle

- FR24: Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid.
- FR25: Authorized actors can acquire a task-scoped workspace lock.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: The system can deny competing operations when lock ownership or workspace state makes the operation unsafe.
- FR28: The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions.
- FR29: Authorized actors can release a workspace lock when ownership and policy allow.
- FR30: The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP.
- FR31: Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable.

#### File Operations and Context Queries

- FR32: Authorized actors can add, change, and remove files within a prepared and locked workspace.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
- FR34: Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads.
- FR35: The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses.
- FR36: The operations console can remain read-only and excluded from file editing or file-content browsing capabilities.

#### Commit, Evidence, and Idempotency

- FR37: Authorized actors can commit workspace changes for repository-backed folders.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits.
- FR39: The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID.
- FR40: The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence.
- FR41: The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers.
- FR42: The system can reject duplicate logical operations when retry identity or operation intent conflicts.

#### Error, Status, and Diagnostics Contract

- FR43: The system can expose a canonical error taxonomy across supported surfaces.
- FR44: The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure.
- FR45: The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.
- FR46: The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure.

#### Cross-Surface Contract

- FR47: API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle.
- FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

#### Audit and Operations Visibility

- FR52: Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference.
- FR55: The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses.
- FR56: The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

### NonFunctional Requirements

#### Security and Tenant Isolation

- NFR1: Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, provider callback, and audit record.
- NFR2: Cross-tenant access leaks are zero-tolerance defects; no object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
- NFR3: Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, background jobs, provider callbacks, audit records, and context-query results.
- NFR4: File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
- NFR5: Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
- NFR6: Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.
- NFR7: Credential references must be validated and displayed only as non-secret identifiers or status indicators.
- NFR8: Provider credentials and repository bindings must be tenant-scoped and must not be reused across tenants, even if repository URLs appear identical.
- NFR9: Provider credentials must use the least privilege required for supported lifecycle operations and must be validated against required provider capabilities before use.
- NFR10: Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.

#### Reliability, Idempotency, and Failure Visibility

- NFR11: Every lifecycle step must expose terminal and non-terminal state, including `Pending`, `InProgress`, `Succeeded`, `Failed`, and `Cancelled` where cancellation is supported.
- NFR12: Required observable lifecycle states include `ProviderReady`, `RepositoryBound`, `WorkspacePrepared`, `Locked`, `FilesChanged`, `CommitPending`, `Committed`, `CleanupPending`, and `Cleaned`.
- NFR13: Repository-backed task lifecycle operations must leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.
- NFR14: Idempotency keys are required for workspace preparation, lock acquisition, file mutation, commit, and cleanup request operations.
- NFR15: A repeated call with the same idempotency key and equivalent payload must return the same logical result; the same key with a conflicting payload must return an idempotency conflict.
- NFR16: Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.
- NFR17: Lock acquisition must be deterministic, tenant-scoped, and limited to one active write lock per tenant/repository/workspace scope.
- NFR18: Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.
- NFR19: Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
- NFR20: Failure visibility must expose state, cause category, retryability, and correlation ID without providing automated remediation in MVP.

#### Performance and Query Bounds

- NFR21: Command submission must acknowledge accepted lifecycle commands within 1 second p95 before asynchronous provider or workspace work continues.
- NFR22: Status and audit summary queries must return within 500 ms p95 for bounded MVP inputs.
- NFR23: Context queries must return within 2 seconds p95 for bounded MVP inputs.
- NFR24: Provider and workspace operations may complete asynchronously when external Git provider latency or workspace size exceeds interactive response budgets; callers must receive operation identity and status visibility rather than blocking indefinitely.
- NFR25: Context queries must define and enforce maximum files, maximum bytes, maximum result count, maximum query duration, timeout behavior, truncation behavior, and included/excluded result audit visibility.
- NFR26: File tree, search, glob, metadata, and bounded range queries must protect the service from unbounded workspace scans.
- NFR27: Large file and binary handling limits must be explicit before MVP release; unsupported files must fail with stable policy errors rather than causing unbounded processing.
- NFR28: Provider calls must use explicit timeout budgets, retry limits, and backoff caps.
- NFR29: Provider calls must report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states rather than leaving callers waiting indefinitely.
- NFR30: Provider rate-limit responses must preserve retry hints where available and expose retry-after or classified retryability.

#### Scalability and Capacity

- NFR31: The system must support multiple tenants, folders, repositories, workspaces, and concurrent agent tasks without shared mutable state causing cross-tenant or cross-task interference.
- NFR32: Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.
- NFR33: Audit, timeline, and file-context projections must remain queryable as folder history grows.
- NFR34: Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.
- NFR35: MVP capacity targets must avoid assuming a single tenant, single repository, or single active workspace, while avoiding unsupported claims about massive scale before concrete load targets are defined.

#### Integration and Contract Compatibility

- NFR36: REST, CLI, MCP, and SDK surfaces must preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes; transport shape and UX may differ.
- NFR37: Public contracts must be versioned; breaking changes to lifecycle commands, queries, error categories, workspace states, provider capabilities, or audit fields require an explicit new versioned contract.
- NFR38: The product must support at least the active contract version and define a deprecation policy before removing any public lifecycle contract.
- NFR39: Shared or generated contract tests must validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
- NFR40: Generated SDKs, MCP tool definitions, CLI command schemas, and OpenAPI contracts must be derived from or validated against the same canonical lifecycle contract.
- NFR41: GitHub and Forgejo support must be validated through provider contract tests before either provider is marked ready.
- NFR42: Provider contract tests must cover only MVP-dependent lifecycle behavior: readiness, repository binding, branch/ref handling, file operations, commit, status, provider errors, and failure behavior.
- NFR43: Supported GitHub and Forgejo API versions or behavior assumptions must be pinned or recorded so provider compatibility drift is visible.
- NFR44: Provider capability differences must be reported explicitly instead of inferred by clients from failed operations.
- NFR45: Provider failures (timeout, rate limit, auth failure, repository missing/conflict, branch/ref conflict, unavailable, invalid path, commit rejected, unknown outcome) must map to stable product error categories.

#### Observability, Auditability, and Replay

- NFR46: Every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
- NFR47: Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
- NFR48: Allowed audit metadata must be explicitly classified; sensitive metadata (file paths, branch names, commit messages, repository names, provider error payloads) must be classified and protected through access control, hashing, truncation, or redaction.
- NFR49: Operations-console views must be read-model–based, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata.
- NFR50: Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.
- NFR51: Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.
- NFR52: The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.
- NFR53: Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.

#### Data Retention and Cleanup

- NFR54: Retention periods must be defined for audit metadata, workspace status, provider correlation IDs, projections, temporary working files, and cleanup records.
- NFR55: Tenant deletion must define which records are deleted, tombstoned, retained for audit, or anonymized.
- NFR56: Workspace cleanup visibility must state whether cleanup is automatic, best-effort, retryable, user-triggered, or status-only for MVP.
- NFR57: Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID.
- NFR58: No cleanup process may remove audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations.

#### Operations Console Accessibility

- NFR59: Read-only operations console flows must target WCAG 2.2 AA.
- NFR60: The console must support keyboard navigation for primary diagnostic workflows.
- NFR61: Status, failure, readiness, and lock indicators must not rely on color alone.
- NFR62: Console screens must provide visible focus states, semantic headings, readable table structure, and sufficient contrast.
- NFR63: Console text, controls, and tables must remain readable at common browser zoom levels used by operators.

#### Verification Expectations

- NFR64: Each NFR category must have at least one automated verification path or documented manual validation path before MVP release.
- NFR65: Security, tenant isolation, idempotency, provider contract, read-model determinism, and cross-surface contract compatibility NFRs must have automated tests.
- NFR66: Performance, accessibility, retention, backup/recovery, and operations-console usability NFRs must have release validation evidence before MVP acceptance.
- NFR67: Security verification must include dependency/package scanning, generated artifact review, and least-privilege provider credential validation.

### Additional Requirements

These come from the Architecture document and represent technical/infrastructure requirements that must be satisfied alongside FRs/NFRs.

#### Solution Scaffolding (Phase 0 — sibling-module starter pattern)

- AR-SCAFFOLD-01: No third-party `dotnet new` template fits; scaffold by mirroring Hexalith.Tenants project structure (Directory.Build.props, Directory.Packages.props with central package management pinning Hexalith.* 3.15.1, .slnx solution format) and Hexalith.EventStore.Admin.* surfaces (Cli/Mcp/UI conventions). Project layout must follow §"Recommended Project Layout" exactly, including 13 src projects, 11 test projects, 2 sample projects, .github/workflows/ files (ci, contract-tests, nightly-drift, policy-conformance, release).
- AR-SCAFFOLD-02: Initialize root configuration files (Directory.Build.props, Directory.Packages.props, global.json pinned to .NET 10 SDK, nuget.config, .editorconfig, .gitmodules, Hexalith.Folders.slnx).
- AR-SCAFFOLD-03: Submodule policy: root-level only; never `git submodule update --init --recursive` (per CLAUDE.md). Reference Hexalith.AI.Tools, Hexalith.EventStore, Hexalith.FrontComposer, Hexalith.Tenants as root submodules.
- AR-SCAFFOLD-04: Create placeholder normative fixture files: `tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/previous-spine.yaml`, `tests/fixtures/idempotency-encoding-corpus.json`. Create `tests/load/Hexalith.Folders.LoadTests.csproj` and `tests/tools/parity-oracle-generator/` placeholder. Create `docs/exit-criteria/_template.md` and `docs/adrs/0000-template.md`.

#### Pre-Spine Workshop (Phase 0.5 — exit criteria deliverables)

- AR-SPINE-01: Resolve C3 retention durations per data class (audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, cleanup records). Output: `docs/exit-criteria/c3-retention.md`. Phase-1-blocking.
- AR-SPINE-02: Resolve C4 bounded MVP input limits (max files / max bytes / max result count / max query duration per context query). Output: `docs/exit-criteria/c4-input-limits.md`. Phase-1-blocking; values land in OpenAPI `maxItems`/`maxLength`/`maxBytes`/`maxResultCount`.
- AR-SPINE-03: Enumerate the C6 Workspace State Transition Matrix (11 states × ~30 transitions × default-rejection rule × operator-disposition labels per F-4). Already enumerated in architecture §"Workspace State Transition Matrix"; must translate 1:1 into `Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`.
- AR-SPINE-04: Pin S-2 OIDC validation parameters per environment (issuer, audience, ClockSkew=30s, RequireSignedTokens, JWKS AutomaticRefreshInterval=10m / RefreshInterval=1m).
- AR-SPINE-05: Declare per-mutating-command `x-hexalith-idempotency-equivalence` field list (lexicographic order) and per-operation `x-hexalith-parity-dimensions` (with mutating ops MUST declare `idempotency_key_rule`; query ops MUST declare `read_consistency_class`).
- AR-SPINE-06: Finalize the §"Adapter Parity Contract" (idempotency-key sourcing, correlation-id default, credential sourcing, pre-SDK error mapping, CLI exit-code table, MCP failure-kind set) per adapter (SDK / CLI / MCP).
- AR-SPINE-07: Author `tests/fixtures/parity-contract.schema.json` (defines parity-oracle row shape).
- AR-SPINE-08: Author `tests/fixtures/idempotency-encoding-corpus.json` (NFC/NFD/NFKC/NFKD/zero-width-joiner/ULID-case variants).
- AR-SPINE-09: Initialize `tests/fixtures/previous-spine.yaml` (seed copy of v1 spine for symmetric drift detection).

#### Contract Spine (Phase 1 — C0)

- AR-SPINE-10: Author OpenAPI 3.1 Contract Spine at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` with extension vocabulary: `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier`, `x-hexalith-correlation`, `x-hexalith-lifecycle-states`, `x-hexalith-parity-dimensions`, `x-hexalith-audit-metadata-keys`, `x-hexalith-sensitive-metadata-tier`.
- AR-SPINE-11: Wire NSwag SDK generation pipeline in `Hexalith.Folders.Client.csproj` emitting `ComputeIdempotencyHash()` per command DTO using `x-hexalith-idempotency-equivalence` field list.
- AR-SPINE-12: Generate parity oracle `tests/fixtures/parity-contract.yaml` from Contract Spine with both transport-parity columns (`auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`) and behavioral-parity columns (`pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `cli_exit_code`, `mcp_failure_kind`).
- AR-SPINE-13: Wire all Phase-1 CI gates: BLOCKING server-vs-spine validation (per A-3), symmetric drift gate against `previous-spine.yaml`, per-class completeness assertion, `parity-contract.schema.json` validation, NSwag golden-file gate (`git diff --exit-code` on `Hexalith.Folders.Client/Generated/`).

#### Domain & Hosting

- AR-DOMAIN-01: Implement `OrganizationAggregate` + `OrganizationState` + `OrganizationStateApply` for FR15–FR23 (provider readiness, repository binding, ACL baseline). Aggregate identity `{managedTenantId}:organizations:{organizationId}`; never `system` tenant.
- AR-DOMAIN-02: Implement `FolderAggregate` + `FolderState` + `FolderStateApply` + `FolderStateTransitions.cs` (C6 matrix) for FR11–FR14, FR24–FR42. Aggregate identity `{managedTenantId}:folders:{folderId}`; opaque ULID; folder hierarchy projected, not identity.
- AR-DOMAIN-03: Aggregate handlers must be pure functions of (Command, State?, CommandEnvelope) returning `DomainResult.Success(events) | DomainResult.Rejection | DomainResult.NoOp`. FORBIDDEN inside handlers: Dapr calls, HTTP calls, file I/O, Git, secret access, DB queries, time-of-day reads (use envelope `Timestamp`), random (use causation ID).
- AR-DOMAIN-04: Wrap all domain commands and events in `CommandEnvelope` / `EventEnvelope` records; required envelope metadata: `tenantId`, `domain`, `aggregateId`, `messageId`, `correlationId`, `causationId`, `timestamp`, `userId`, `eventTypeName`.
- AR-DOMAIN-05: Stand up `Hexalith.Folders.Server` domain-service host with `AddEventStore()` / `UseEventStore()`, `MapPost("/process")`, `MapPost("/project")` endpoints; ASP.NET Core Minimal APIs for REST canonical transport; OpenAPI emitted by Microsoft.AspNetCore.OpenApi from controllers/handlers and validated against C0 in CI as BLOCKING gate.
- AR-DOMAIN-06: Stand up `Hexalith.Folders.Client` SDK with NSwag-generated typed methods and hand-written `UploadFileAsync(stream)` convenience wrapper that picks `PutFileInlineAsync` or `PutFileStreamAsync` based on stream length (D-9 bimodal).
- AR-DOMAIN-07: Snapshot strategy: conservative defaults from `SnapshotManager` (every 50 events) for `folders` domain (D-6).

#### Authorization & Tenant Integration

- AR-AUTHZ-01: Implement layered authorization: JWT validation → Hexalith.EventStore claim transform (`eventstore:tenant`, `eventstore:permission`) → local fail-closed-on-stale tenant-access projection → folder ACL → EventStore validators → production Dapr deny-by-default policies + mTLS.
- AR-AUTHZ-02: Wire `Hexalith.Folders.Client.Subscription.MapTenantEventSubscription` consuming `system.tenants.events` Dapr pub/sub; build local `FolderTenantAccessProjection` (Dapr state) for fail-closed authorization.
- AR-AUTHZ-03: Implement Tenants-availability degraded mode: read paths continue under bounded staleness; mutations require fresh authorization (synchronous Tenants query or rejection). Health check `TenantsAvailabilityCheck`.
- AR-AUTHZ-04: Implement mid-task authorization revalidation per C7 two-number lock contract (lease-renewal interval AND auth-revalidation interval, default + per-tenant tunable, tied to stated SLO "revoked tenant access takes effect within N seconds").
- AR-AUTHZ-05: Tenant context provenance middleware: authoritative tenant comes from request authentication context + EventStore envelope; tenant-in-payload is INPUT requiring validation, never authority. Tested as parity invariant on REST/CLI/MCP/SDK.

#### Provider Adapters

- AR-PROVIDER-01: Implement `IGitProvider` capability-discoverable port; capability-discovery model accommodates N providers (not hardcoded for 2). Provider port surfaces credential references and capability metadata; provider-specific permission scoping lives inside the adapter.
- AR-PROVIDER-02: Implement GitHub adapter (`Hexalith.Folders.Providers.GitHub`) using Octokit 14.0.0 with GitHub Apps fine-grained permissions; not surfaced beyond the provider port.
- AR-PROVIDER-03: Implement Forgejo adapter (`Hexalith.Folders.Providers.Forgejo`) as a typed HttpClient wrapper, fed by per-version `swagger.v1.json` snapshots in `tests/contracts/forgejo/<version>/`, with Forgejo scoped tokens.
- AR-PROVIDER-04: Maintain `tests/contracts/forgejo/supported-versions.json` test matrix (latest stable + latest LTS + n-1 minor + any pinned customer instance). Nightly oasdiff schema-diff job classifies additive (warn) vs breaking (fail).
- AR-PROVIDER-05: Distinguish known provider failure (timeout / 401 / 403 / 404 / 409 / 429 / 5xx / branch-protection / missing-or-deleted repository / stale clone / credential revocation / drift) from unknown outcome; unknown outcome enters `reconciliation_required` state — never silent retry that could duplicate repositories, file changes, or commits.
- AR-PROVIDER-06: Provider contract suite runs in two execution modes: hermetic-PR-gate (pinned fixtures, fast) AND live-nightly-drift (against real GitHub/Forgejo); fixture-to-failure-mode coverage matrix asserted in CI.

#### Workers / Reconciliation / Rate Limiting

- AR-WORKER-01: Implement process-manager workers in `Hexalith.Folders.Workers` reacting to events: `WorkspacePreparationWorkflow` (reacts to `FolderGitRepositoryBound`), `RepositoryProvisioningWorkflow` (reacts to `FolderGitRepositoryRequested`), `CommitWorkflow`, reconcilers for `unknown_provider_outcome`. Idempotent by causation/correlation ID.
- AR-WORKER-02: Implement working-copy storage at per-AppHost ephemeral filesystem under configurable root (`/var/lib/hexalith-folders/work/{tenantId}/{folderId}/{taskId}`); checkouts disposable, never authoritative; existence recorded as workspace-readiness state in EventStore.
- AR-WORKER-03: Implement provider rate-limit handling (I-8): per-provider token bucket scoped per-tenant for user-driven calls; per-provider global bucket for background reconciliation; backoff with jitter; reconciliation queue feeds C12 drift detection on sustained 429s. Chaos test in CI injects synthetic 429 storms.
- AR-WORKER-04: Implement Tenants event handlers in `Hexalith.Folders.Workers.Tenants.TenantEventHandlers`: `TenantDisabledHandler`, `UserRemovedFromTenantHandler`, `UserRoleChangedHandler`, `TenantConfigurationSetHandler` (processes `folders.*` keys only).

#### Idempotency & Caching

- AR-IDEMP-01: Required `Idempotency-Key` header on every mutating command (workspace prepare, lock, file mutation, commit, cleanup). Server canonicalizes via `x-hexalith-idempotency-equivalence` field list (lexicographic order) using NSwag-generated `ComputeIdempotencyHash()`. Replay = same key + equivalent payload → same result; same key + different payload → `409 Idempotency-Conflict`.
- AR-IDEMP-02: Two-tier idempotency record TTL (D-7): `mutation = 24h`; `commit = retention-period(C3)`. Backed by Dapr state.
- AR-IDEMP-03: Cache-key tenant prefix invariant (C10): every cache key (in-process MemoryCache, Dapr state, Redis distributed cache) MUST start with `{tenantId}:` prefix. CI lint check (Roslyn analyzer or grep-based) enforces as hard build-time gate. Helper `TenantPrefixedCacheKey.cs`.

#### Audit, Redaction, Observability

- AR-AUDIT-01: Implement `AuditProjection` (D-10) under `Hexalith.Folders.Server` projection endpoints, derived from event streams; rebuildable from events; retention per C3.
- AR-AUDIT-02: Sentinel redaction pipeline: every component that emits a log/trace/metric label/event/audit record/console payload/provider diagnostic/error response MUST run sentinel tests over `tests/fixtures/audit-leakage-corpus.json`. CI gate fails on any sentinel match.
- AR-AUDIT-03: Sensitive metadata classifier (S-6, C9): default tier paths + repo names + branch names + commit messages classified as `tenant-sensitive`; per-tenant override allows `confidential` (hashed at write time). Implementation in `Redaction/SensitiveMetadataClassifier.cs`.
- AR-AUDIT-04: Correlation propagation invariant: `X-Correlation-Id` and `X-Hexalith-Task-Id` headers carry across REST/SDK/CLI/MCP; CommandEnvelope correlation/causation IDs propagate through EventStore → projection → audit. Parity oracle (C13) asserts the chain end-to-end.
- AR-AUDIT-05: OpenTelemetry SDK exporting OTLP: traces (correlation/causation/task IDs as span attributes), metrics, logs (structured, redacted). Local: Aspire OTLP collector. Production: pluggable exporters (Jaeger / Tempo / Application Insights / Datadog).
- AR-AUDIT-06: Logging: structured logs only (Microsoft.Extensions.Logging structured templates); required fields `tenantId`, `correlationId`, `causationId`, `taskId`, `aggregateId`, `eventTypeName`. FORBIDDEN as log values: file contents, secrets, provider tokens, raw credential references, anything matching audit-leakage-corpus.json.
- AR-AUDIT-07: Health-check endpoints `/health/live`, `/health/ready` per Folders service; monitored snapshots: dead-letter topic depth, projection lag (status-freshness target C2), Dapr sidecar health, Tenants-availability degraded-mode active flag.

#### Adapter Surfaces (CLI, MCP, SDK)

- AR-CLI-01: Build `Hexalith.Folders.Cli` on System.CommandLine 2.x, wrapping `Hexalith.Folders.Client` SDK; commands mirror REST capability groups (`provider`, `folder`, `workspace`, `file`, `commit`, `context`, `audit`).
- AR-CLI-02: CLI adapter behavior per §"Adapter Parity Contract": `--idempotency-key <key>` flag (required for mutating) or `--allow-auto-key` opt-in; `--correlation-id <id>` override; `--task-id <id>` (required for task-scoped); credential precedence `HEXALITH_TOKEN` env → `~/.hexalith/credentials.json` → `--token` flag; canonical exit-code table (0/64/65/66/67/68/69/70/71/72/73/74/75/1).
- AR-MCP-01: Build `Hexalith.Folders.Mcp` on ModelContextProtocol 1.3.0 SDK, wrapping `Hexalith.Folders.Client`; one tool per canonical command/query (PrepareWorkspaceTool, LockWorkspaceTool, WriteFileTool, CommitWorkspaceTool, ReadFileTool, SearchFolderTool, GetWorkspaceStatusTool); resources for FolderTreeResource, AuditTrailResource.
- AR-MCP-02: MCP failure-kind mapping per §"Adapter Parity Contract": every failure result includes `kind ∈ {usage_error, credential_missing, tenant_access_denied, workspace_locked, idempotency_conflict, validation_error, provider_failure_known, provider_outcome_unknown, reconciliation_required, not_found, state_transition_invalid, redacted, internal_error}` plus `correlationId`, `code`, `retryable`, `clientAction`.
- AR-PARITY-01: All four surface test projects (`*.Sdk.Tests`, `*.Rest.Tests`, `*.Cli.Tests`, `*.Mcp.Tests`) consume the parity oracle as xUnit theory data (transport-parity columns in SDK+REST tests; behavioral-parity columns in CLI+MCP tests). CI fails on missing rows or schema-validation failures.

#### Read-Only Operations Console (Frontend)

- AR-UI-01: Build `Hexalith.Folders.UI` as Blazor Server with SignalR (F-1, F-2) consuming `Hexalith.Folders.Client` SDK; reads only from projection endpoints (no direct EventStore aggregate access).
- AR-UI-02: Use Microsoft Fluent UI Blazor (`Microsoft.FluentUI.AspNetCore.Components`) component library (F-3) to satisfy WCAG 2.2 AA targets.
- AR-UI-03: Operator-disposition labels are the primary visual (F-4): `auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`. Technical state names appear as secondary metadata. `DispositionLabelMapper.cs` sourced from C6 matrix.
- AR-UI-04: Redacted fields render with a visible lock-icon affordance (F-5) — "your tenant policy hides this; contact your administrator". Never silent truncation.
- AR-UI-05: Incident-mode last-resort read path at `/_admin/incident-stream` (F-6) — ACL-checked event-stream view available when projections are degraded; surfaces latest events for operators with `eventstore:permission=admin`. Three UX guardrails: (1) persistent red banner ("DEGRADED MODE — last projection checkpoint: HH:MM:SS UTC"); (2) operator-disposition labels rendered alongside raw event types; (3) one-click "copy correlationId + timestamp window" affordance.
- AR-UI-06: Operations console performance budget (F-7): p95 page-load < 1.5s primary diagnostic flows; p99 < 3s; degraded-mode flows up to 5s p95. Perceived-wait UX: visible skeleton state at 400ms; "still loading… [cancel]" affordance at 2s.
- AR-UI-07: No mutation paths, credential reveal, file-content browsing, file-editing UI, raw diff display, hidden repair actions, or unrestricted filesystem browsing in the MVP read-only console.

#### Infrastructure & Deployment

- AR-INFRA-01: `Hexalith.Folders.AppHost` composes EventStore (`AppId=eventstore`) + Tenants (`AppId=tenants`) + Folders.Server (`AppId=folders`) + Folders.Workers (`AppId=folders-workers`) + Folders.UI (`AppId=folders-ui`) + Keycloak. Aspire 13.3.0 + CommunityToolkit.Aspire.Hosting.Dapr 13.0.0.
- AR-INFRA-02: Dapr components: shared `statestore` (Redis 7.x via Aspire), `pubsub` (Redis Streams), `resiliency` policies, `accesscontrol.yaml` (local: defaultAction allow). Production: deny-by-default + mTLS, app IDs restricted (`folders` may invoke `eventstore` and `tenants`; not `system` admin; pubsub topics declared).
- AR-INFRA-03: Dapr policy conformance: CI job runs `daprd` in kind cluster with production policy YAML; property-based negative test asserts unauthorized `(sourceAppId, targetAppId, operation)` triples receive `403`. Block merge on policy YAML changes without corresponding negative test additions.
- AR-INFRA-04: Containerized production hosting: one image per service (`hexalith-folders-server`, `hexalith-folders-workers`, `hexalith-folders-ui`); Dapr sidecars deployed alongside; Kubernetes-friendly but not Kubernetes-required.
- AR-INFRA-05: GitHub Actions CI/CD pipeline gates: build, format, lint (including C10 cache-key tenant-prefix lint), unit tests, contract tests (hermetic), parity tests (C13), redaction sentinel tests (C6), nightly live-drift provider tests (C12), `dapr-policy-conformance` negative-test job, Forgejo schema-diff job, exit-criteria-presence gate, pattern-examples compile gate.
- AR-INFRA-06: Production OIDC: `Microsoft.AspNetCore.Authentication.JwtBearer` with frozen validation parameters (S-2). Compatible providers: Keycloak, Microsoft Entra ID, Auth0, or any OIDC-compliant provider.
- AR-INFRA-07: NuGet packages published on tagged release: `Hexalith.Folders.Contracts`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, `Hexalith.Folders.Testing`.

#### Naming, Format, and Communication Patterns

- AR-PATTERN-01: Follow C# / domain naming tables (PascalCase types/methods/properties, camelCase locals/parameters; `{Concept}Aggregate`, `{Concept}State`, `{Verb}{Concept}` commands, `{Concept}{Verbed}` events, `{Concept}Projection`).
- AR-PATTERN-02: JSON wire format: camelCase, ISO-8601-Z dates, string enums, NFC-normalized Unicode forward-slash workspace-root-relative paths, content referenced by `contentHash`+`byteLength`+`mediaType` (never inline in event payloads).
- AR-PATTERN-03: HTTP header set: `Authorization: Bearer <jwt>`, `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, `X-Hexalith-Retry-As: stream`, `X-Hexalith-Freshness`. Errors `application/problem+json` (RFC 9457).
- AR-PATTERN-04: REST endpoint naming: lowercase hyphen-delimited path segments; capability-group prefixes (provider-readiness, folders, workspaces, files, commits, audit, ops-console, context queries); URL-versioned `/api/v1/...`.
- AR-PATTERN-05: Pub/sub topics `{tenantId}.{domain}.events`; tenant subscription `system.tenants.events`; dead-letter `deadletter.{domain}.events`. Internal calls go through canonical command/query API (`POST /api/v1/commands`, `POST /api/v1/queries`), never direct aggregate HTTP.

#### Testing

- AR-TEST-01: Aggregate tests: Given prior events / state → When command → Then expected `DomainResult`. Use `Hexalith.EventStore.Testing` assertions.
- AR-TEST-02: Replay tests for every event family; tombstone tests for terminated aggregates (`ITerminatable` compliance); identity tests for tenant/domain/aggregate IDs.
- AR-TEST-03: Projection tests: ordered event lists build deterministic read models; duplicate delivery is idempotent. Read-model determinism gate (rebuild from empty produces equivalent state from same ordered event stream, excluding fields derived from external clocks).
- AR-TEST-04: Conformance tests for `Hexalith.Folders.Testing` fakes (delegate to production aggregate logic; mirrors `TenantConformanceTests`).
- AR-TEST-05: Parity tests (C13) generated from C0 Contract Spine; SDK/REST/CLI/MCP tests consume `parity-contract.yaml` as xUnit theory data.
- AR-TEST-06: Sentinel tests iterate `audit-leakage-corpus.json` on every output pipeline (logs, traces, metrics labels, events, audit records, console payloads, provider diagnostics, error responses).
- AR-TEST-07: Path security tests (traversal, absolute paths, mixed separators, encoded traversal, reserved names, Unicode normalization, symlinks, case sensitivity).
- AR-TEST-08: Idempotency encoding-equivalence tests iterate `idempotency-encoding-corpus.json` (NFC/NFD/NFKC/NFKD/zero-width-joiner/ULID-case variants).
- AR-TEST-09: Cross-tenant isolation negative tests covering API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, background jobs, provider callbacks, audit records, context-query results.
- AR-TEST-10: Capacity test harness in `tests/load/` (NBomber); scenarios cover workspace prepare → lock → mutate → commit at concurrency profiles per C1.
- AR-TEST-11: End-to-end parity scenario in `tests/Hexalith.Folders.IntegrationTests/EndToEnd/` runs the canonical task lifecycle through REST + CLI + MCP + SDK.

#### Documentation Deliverables

- AR-DOC-01: OpenAPI v1 reference (rendered to `docs/api/`) with schemas, auth requirements, idempotency keys, pagination/filtering conventions, correlation IDs, examples.
- AR-DOC-02: Getting started guide; authentication/tenant/folder-ACL guide; workspace lifecycle and lock state diagram; file-operation to commit flow diagram; tenant/auth/ACL decision flow diagram.
- AR-DOC-03: CLI reference; MCP tool/resource reference; SDK reference and quickstart; provider integration and provider contract testing guide; operations console and metadata-only audit guide.
- AR-DOC-04: Error catalog with REST status, CLI exit behavior, SDK error/result behavior, retryability, client action, audit/logging expectations.
- AR-DOC-05: Tenant-deletion runbook at `docs/runbooks/tenant-deletion.md` (authored Phase 4); ADR template at `docs/adrs/0000-template.md` (authored Phase 0); contract-terms reference at `docs/contract-terms.md`.

### UX Design Requirements

No standalone UX Design Specification document was produced for this MVP. UX/UI requirements for the read-only operations console are captured under **Additional Requirements → Read-Only Operations Console (Frontend)** (AR-UI-01 through AR-UI-07), driven by Architecture decisions F-1 through F-7. Story authoring should treat those AR items with the same rigor as UX-DRs from a UX spec.

### FR Coverage Map

- FR1: Epic 1 — vocabulary in OpenAPI Contract Spine + `docs/contract-terms.md`
- FR2: Epic 1 — lifecycle vocabulary via `x-hexalith-lifecycle-states` extension + diagrams
- FR3: Epic 1 — command/query distinction in OpenAPI operation grouping + Server endpoint routing
- FR4: Epic 2 — tenant administrator ACL configuration via `OrganizationAggregate` ACL baseline
- FR5: Epic 2 — folder access grant to users, groups, roles, and delegated service agents
- FR6: Epic 2 — effective-permissions inspection
- FR7: Epic 3 — tenant readiness inspection (depends on provider configuration)
- FR8: Epic 2 — layered authorization evaluation (foundation: JWT → claim transform → tenant projection → folder ACL → EventStore validators → Dapr policy)
- FR9: Epic 2 — cross-tenant denial before any file/workspace/credential/repository/lock/commit/provider/audit access
- FR10: Epic 2 — authorization evidence (allowed and denied) without unauthorized resource enumeration
- FR11: Epic 2 — folder creation
- FR12: Epic 2 — folder lifecycle and binding inspection
- FR13: Epic 2 — folder archive
- FR14: Epic 2 — audit and status evidence preservation for archived folders
- FR15: Epic 3 — provider binding + credential reference configuration per tenant
- FR16: Epic 3 — provider readiness validation before repository-backed creation/binding
- FR17: Epic 3 — readiness diagnostics with safe reason codes, retryability, remediation category, provider reference, correlation ID
- FR18: Epic 3 — repository-backed folder creation when readiness passes
- FR19: Epic 3 — folder binding to existing repository
- FR20: Epic 3 — branch/ref policy selection
- FR21: Epic 3 — provider/credential-reference/binding/branch/capability metadata exposure (no secrets)
- FR22: Epic 3 — GitHub vs Forgejo capability differences exposed explicitly
- FR23: Epic 3 — per-provider readiness evidence for canonical lifecycle (readiness, repo binding, branch/ref, file ops, commit, status, failure behavior)
- FR24: Epic 4 — workspace preparation
- FR25: Epic 4 — task-scoped workspace lock acquisition
- FR26: Epic 4 — lock state, owner, task, age, expiry, retry-eligibility metadata inspection
- FR27: Epic 4 — competing-operation denial under unsafe lock/state
- FR28: Epic 4 — lock state transitions (active, expired, stale, abandoned, interrupted, released)
- FR29: Epic 4 — workspace lock release
- FR30: Epic 4 — workspace cleanup status visibility for completed/failed/interrupted/abandoned task lifecycles
- FR31: Epic 4 — workspace/task/audit/provider status currency inspection
- FR32: Epic 4 — file add/change/remove (PutFileInline ≤256KB + PutFileStream multipart)
- FR33: Epic 4 — file-operation policy violation rejection (workspace boundary, path, branch/ref, lock, tenant, provider, folder)
- FR34: Epic 4 — context queries via tree, metadata, search, glob, bounded range reads
- FR35: Epic 4 — context-query policy boundaries (paths, exclusions, binary handling, range/result limits, secret-safe responses)
- FR36: Epic 6 — read-only console scope (no file editing or content browsing in console)
- FR37: Epic 4 — workspace commit for repository-backed folders
- FR38: Epic 4 — task/operation/correlation/actor/author/branch/commit-message/changed-path metadata attachment
- FR39: Epic 4 — task and commit evidence exposure (provider, binding, branch, paths, status, commit ref, timestamps, IDs)
- FR40: Epic 4 — failed/incomplete/duplicate/retried/conflicting operation reporting with stable status and audit evidence
- FR41: Epic 4 — idempotent lifecycle retries with stable task/operation/correlation IDs
- FR42: Epic 4 — duplicate logical operation rejection on retry-identity or intent conflict
- FR43: Epic 4 — canonical error taxonomy across surfaces (foundation in Epic 1; full set realized here)
- FR44: Epic 4 — full error category set (validation/auth/tenant/folder ACL/credential/provider/capability/repository/branch/lock/workspace/path/commit/read-model/duplicate/transient)
- FR45: Epic 4 — canonical workspace/task states (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`) per C6 matrix
- FR46: Epic 4 — final-state explanation + retry eligibility + operational evidence after any lifecycle failure
- FR47: Epic 1 (artifact authoring); endpoint completeness implemented across Epics 2–4
- FR48: Epic 5 — CLI canonical lifecycle parity
- FR49: Epic 5 — MCP canonical lifecycle parity
- FR50: Epic 5 — SDK canonical lifecycle parity
- FR51: Epic 5 — cross-surface equivalence via C13 parity oracle (transport + behavioral parity columns)
- FR52: Epic 6 — read-only ops console projection consumption (readiness, binding, workspace, lock, dirty, commit, failure, provider, credential-ref, sync)
- FR53: Epic 6 — metadata-only audit trail inspection (success/denied/failed/retried/duplicate)
- FR54: Epic 6 — incident reconstruction from immutable audit metadata
- FR55: Epic 4 (write-side: redaction in events/projections/logs/traces/metrics) + Epic 6 (read-side: console rendering with classification + lock-icon affordance)
- FR56: Epic 6 — operation timelines for folder, workspace, file, lock, commit, provider, status, authorization events
- FR57: Epic 6 — provider support evidence visibility for GitHub and Forgejo

## Epic List

### Epic 1: Foundation — Solution Scaffolding & Contract Spine

Platform engineers can scaffold the module against the Hexalith ecosystem and lock a canonical OpenAPI v1 Contract Spine that drives every surface (REST, SDK, CLI, MCP) — with CI gates (server-vs-spine, symmetric drift, NSwag golden-file, parity-contract schema, exit-criteria-presence, pattern-examples compile, sentinel corpus, encoding-equivalence) preventing drift before any feature lands.

**FRs covered:** FR1, FR2, FR3, FR47 (artifact authoring; endpoint completeness delivered by Epics 2–4)

### Epic 2: Tenant-Scoped Folder Management

Tenant administrators and authorized actors can create, inspect, and archive folders within a tenant with cross-tenant isolation enforced before any resource access, fail-closed-on-stale local tenant projection, mid-task auth revalidation, and metadata-only audit preserving evidence even for archived folders.

**FRs covered:** FR4, FR5, FR6, FR8, FR9, FR10, FR11, FR12, FR13, FR14

### Epic 3: Provider Readiness & Repository Binding

Platform engineers can configure GitHub and Forgejo provider bindings + credential references, validate provider readiness with safe diagnostics + reason codes, expose capability differences explicitly, and bind folders to repositories — gating all subsequent repository-backed task work behind a green readiness check.

**FRs covered:** FR7, FR15, FR16, FR17, FR18, FR19, FR20, FR21, FR22, FR23

### Epic 4: Repository-Backed Task Lifecycle (Workspace → Lock → Files → Commit)

Developers and AI agents can prepare a workspace, acquire a task-scoped lock, add/change/remove files, query file context (tree/search/glob/range), commit Git-backed changes idempotently, and inspect stable terminal or intermediate state when interrupted — proving the MVP's canonical job. Honors the C6 11-state transition matrix, two-tier idempotency TTLs, single-active-writer locks, working-copy ephemerality, and unknown-provider-outcome → reconciliation_required (no silent retry).

**FRs covered:** FR24, FR25, FR26, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34, FR35, FR37, FR38, FR39, FR40, FR41, FR42, FR43, FR44, FR45, FR46, FR55 (write-side redaction)

### Epic 5: Cross-Surface Adapters — CLI, MCP, SDK Parity

Operators using the CLI, AI tools using the MCP server, and developers using the SDK can perform the complete canonical task lifecycle with identical canonical-error categories, idempotency replay semantics, correlation/task-id propagation, audit metadata, authorization decisions, and terminal states — validated by xUnit theory data sourced from the C13 parity oracle (transport-parity columns in SDK+REST tests; behavioral-parity columns in CLI+MCP tests).

**FRs covered:** FR48, FR49, FR50, FR51

### Epic 6: Read-Only Operations Console & Audit Reviewer Flows

Operators and audit reviewers can diagnose workspace, lock, provider, commit, and failure state through a WCAG 2.2 AA Blazor Server console with operator-disposition labels (`auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`) as the primary visual; reconstruct incidents from metadata-only audit trails; render redacted fields with a visible lock-icon affordance (never silent truncation); and continue diagnosis through `/_admin/incident-stream` when projections are degraded — with persistent red banner, disposition labels alongside raw events, one-click correlationId+timestamp copy, 400ms skeleton state, and 2s "still loading… [cancel]" affordance.

**FRs covered:** FR36 (negative scope enforcement), FR52, FR53, FR54, FR55 (read-side rendering with classification), FR56, FR57

### Epic 7: Production Hardening & Release Readiness

Platform operators can deploy Hexalith.Folders to production with measured tenant isolation, accessibility, performance, retention, and incident-response readiness — final C1 capacity targets, C2 status-freshness target, C3 retention durations, and C5 scalability quantifiers pinned and validated through NBomber capacity tests; production Dapr deny-by-default + mTLS validated by `dapr-policy-conformance` negative tests; pluggable production OIDC; container images per service; full documentation deliverables; tenant-deletion runbook; backup/recovery validation.

**FRs covered:** None directly (no new FRs); validates NFR54–58 (retention/cleanup) and NFR64–67 (release-validation evidence) for the integrated system.

## Epic 1: Foundation — Solution Scaffolding & Contract Spine

Platform engineers can scaffold the module against the Hexalith ecosystem (Hexalith.Tenants baseline + Hexalith.EventStore.Admin.* surfaces) and lock a canonical OpenAPI v1 Contract Spine that drives every cross-surface generation (REST, SDK, CLI, MCP) — with CI gates preventing drift before any feature lands. The deliverables of this epic are an empty-but-coherent codebase, a frozen contract artifact, a parity oracle, and a CI pipeline that every subsequent epic builds on.

### Story 1.1: Scaffold solution skeleton mirroring Hexalith.Tenants

As a platform engineer,
I want a solution skeleton with all 13 src projects, 11 test projects, and sample projects laid out per the Hexalith.Tenants baseline,
So that every subsequent story has a stable, convention-compliant home to add code without convention drift.

**Acceptance Criteria:**

**Given** a fresh repository with the existing root submodules (Hexalith.AI.Tools, Hexalith.EventStore, Hexalith.FrontComposer, Hexalith.Tenants)
**When** the scaffolding script (or manual `dotnet new sln` + project creation) runs
**Then** `Hexalith.Folders.slnx` exists at the repository root in `.slnx` format
**And** `src/` contains 13 projects: `Hexalith.Folders.Contracts`, `Hexalith.Folders`, `Hexalith.Folders.Server`, `Hexalith.Folders.Client`, `Hexalith.Folders.Cli`, `Hexalith.Folders.Mcp`, `Hexalith.Folders.UI`, `Hexalith.Folders.Workers`, `Hexalith.Folders.Aspire`, `Hexalith.Folders.AppHost`, `Hexalith.Folders.ServiceDefaults`, `Hexalith.Folders.Testing` (plus any provider/host helpers required)
**And** `tests/` mirrors `src/` 1:1 with corresponding `*.Tests` projects, plus `Hexalith.Folders.IntegrationTests`
**And** `samples/` contains `Hexalith.Folders.Sample` and `Hexalith.Folders.Sample.Tests`
**And** every project file uses `<TargetFramework>net10.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<LangVersion>latest</LangVersion>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (mirroring Tenants `Directory.Build.props`)
**And** `dotnet build Hexalith.Folders.slnx` succeeds with zero warnings on a clean machine

**Given** a developer searches for project naming conventions
**When** they list project files
**Then** all projects follow the `Hexalith.Folders.{Surface}` naming pattern (per AR-PATTERN-01) and there are no `dotnet new aspire-starter` artifacts or non-Hexalith starter remnants

### Story 1.2: Establish root configuration and submodule policy

As a platform engineer,
I want root configuration files (Directory.Build.props, Directory.Packages.props, global.json, nuget.config, .editorconfig, .gitmodules) configured per Hexalith ecosystem conventions with central package management pinning Hexalith.* to 3.15.1,
So that every project inherits consistent compilation, package versions, and submodule policy without per-project drift.

**Acceptance Criteria:**

**Given** repository root configuration is missing or incomplete
**When** the configuration story is implemented
**Then** `Directory.Build.props` mirrors `Hexalith.Tenants/Directory.Build.props` with `<LangVersion>latest</LangVersion>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, file-scoped namespace enforcement, and editorconfig integration
**And** `Directory.Packages.props` enables central package management (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`) and pins `Hexalith.EventStore.*` and `Hexalith.Tenants.*` to `3.15.1`, `Aspire.Hosting.AppHost` to `13.3.0`, `CommunityToolkit.Aspire.Hosting.Dapr` to `13.0.0`, `Octokit` to `14.0.0`, `ModelContextProtocol` to `1.3.0`
**And** `global.json` pins the .NET 10 SDK
**And** `nuget.config` declares the official `nuget.org` source and (where applicable) Hexalith package feed
**And** `.editorconfig` enforces 4-space indent for `*.cs`, 2-space for `*.json`/`*.yaml`, file-scoped namespaces, and `using` ordering (System / Microsoft / Hexalith / third-party / project-local)

**Given** a developer runs submodule operations
**When** they reference Hexalith ecosystem dependencies
**Then** `.gitmodules` contains only root-level submodule entries (Hexalith.AI.Tools, Hexalith.EventStore, Hexalith.FrontComposer, Hexalith.Tenants)
**And** `git submodule update --init` (without `--recursive`) is the documented init command in `CLAUDE.md` and any setup scripts
**And** no nested submodule reference is present in `.gitmodules` (per CLAUDE.md submodule policy)

### Story 1.3: Seed normative test fixtures and placeholder artifacts

As a platform engineer,
I want the normative shared fixtures (`audit-leakage-corpus.json`, `parity-contract.schema.json`, `previous-spine.yaml`, `idempotency-encoding-corpus.json`) and exit-criteria/ADR template placeholders seeded under `tests/fixtures/` and `docs/`,
So that subsequent CI gates and pre-spine deliverables have stable artifact locations to write into without inventing paths.

**Acceptance Criteria:**

**Given** repository scaffolding is in place
**When** the fixtures story is implemented
**Then** `tests/fixtures/audit-leakage-corpus.json` exists with at least 10 sentinel patterns spanning categories: secret-shaped paths, branch names with token-shaped values, commit messages with email/credential patterns, file-content markers, and provider tokens
**And** `tests/fixtures/idempotency-encoding-corpus.json` exists with NFC, NFD, NFKC, NFKD, zero-width-joiner, and ULID-case variants for at least 5 representative idempotency-key inputs
**And** `tests/fixtures/parity-contract.schema.json` exists as a JSON Schema document defining the row shape with both transport-parity columns (`auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`) and behavioral-parity columns (`pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `cli_exit_code`, `mcp_failure_kind`)
**And** `tests/fixtures/previous-spine.yaml` exists as an empty seed file ready to receive the v1 Contract Spine snapshot for symmetric drift detection

**Given** subsequent stories need exit-criteria and ADR locations
**When** documentation scaffolding is checked
**Then** `docs/exit-criteria/_template.md` exists describing decision-owner, decision-authority, artifact-location, decision-deadline, and measurement-tool fields per §"Exit Criteria Operations Plan"
**And** `docs/adrs/0000-template.md` exists with the standard ADR template (Title, Status, Context, Decision, Consequences)
**And** `docs/contract-terms.md` exists as a stub for the FR1–FR3 vocabulary reference, listing the terms `folder`, `repository`, `workspace`, `task`, `lock`, `provider`, `context query`, `audit record`, `status record`
**And** `tests/load/Hexalith.Folders.LoadTests.csproj` and `tests/tools/parity-oracle-generator/` exist as scaffolded placeholders ready for Phase 9 capacity tests and Phase 1 oracle generation respectively

### Story 1.4: Author Phase 0.5 Pre-Spine Workshop deliverables (C3 retention, C4 input limits, S-2 OIDC, C6 transition matrix consolidation)

As a platform engineer working with the Tech Lead and PM,
I want C3 retention durations, C4 input limits, S-2 OIDC parameters, and the C6 workspace state transition matrix recorded under `docs/exit-criteria/` and `docs/architecture/` with concrete values,
So that the Contract Spine (Story 1.6) can encode `maxItems`/`maxLength`/`maxBytes`/`maxResultCount`, the D-7 commit-TTL, OIDC validation parameters, and lifecycle states without TBDs.

**Acceptance Criteria:**

**Given** the architecture defers C3, C4, S-2 issuer/audience pinning, and C6 to Phase 0.5
**When** the Pre-Spine Workshop deliverables are authored
**Then** `docs/exit-criteria/c3-retention.md` records concrete retention durations per data class (audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, cleanup records) with rationale, decision owner (Tech Lead), and decision authority (Legal + PM)
**And** `docs/exit-criteria/c4-input-limits.md` records concrete numeric values for maximum files per context query, maximum bytes per query response, maximum result count, and maximum query duration with rationale and measurement method
**And** `docs/architecture/workspace-state-transition-matrix.md` consolidates the C6 11-state matrix (`requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, `reconciliation_required`) with operator-disposition labels (`auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`) and every documented `(from, event) → (to, side effect)` transition

**Given** the deployment configuration template is part of the workshop output
**When** the OIDC parameters are pinned per environment
**Then** the deployment configuration template (e.g., `src/Hexalith.Folders.Server/appsettings.template.json` or equivalent) records `JwtBearerOptions` with `ClockSkew = 30s`, `RequireExpirationTime = true`, `RequireSignedTokens = true`, `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateLifetime = true`, `ValidateIssuerSigningKey = true`, JWKS `AutomaticRefreshInterval = 10m`, `RefreshInterval = 1m`, with placeholder issuer/audience pinned per environment (Development, Staging, Production)
**And** `docs/exit-criteria/c3-retention.md` and `docs/exit-criteria/c4-input-limits.md` are referenced from the architecture document update so that the Contract Spine authoring story can pull values without ambiguity

### Story 1.5: Define per-command idempotency-equivalence, parity-dimensions, and finalize Adapter Parity Contract

As a platform engineer,
I want every mutating command's `x-hexalith-idempotency-equivalence` field list (lexicographic order) declared, every operation's `x-hexalith-parity-dimensions` declared (with mutating ops declaring `idempotency_key_rule` and query ops declaring `read_consistency_class`), and the §"Adapter Parity Contract" finalized with concrete rules per adapter,
So that the Contract Spine + parity oracle generator can produce drift-resistant artifacts on first author.

**Acceptance Criteria:**

**Given** the architecture lists the mutating commands and queries
**When** the per-command annotations and Adapter Parity Contract are authored
**Then** every mutating command in the architecture command list (ValidateProviderReadiness, CreateFolder, BindRepository, PrepareWorkspace, LockWorkspace, ReleaseWorkspaceLock, AddFile, ChangeFile, RemoveFile, CommitWorkspace, plus archive/grant/revoke commands) has a documented `x-hexalith-idempotency-equivalence` field list in lexicographic order, recorded under `docs/architecture/idempotency-equivalence.md` (or the architecture document)
**And** every operation has a documented `x-hexalith-parity-dimensions` entry; mutating operations declare `idempotency_key_rule`; query operations declare `read_consistency_class` (one of `snapshot-per-task` / `read-your-writes` / `eventually-consistent`)

**Given** the Adapter Parity Contract is partially specified in the architecture
**When** the contract is finalized
**Then** the architecture §"Adapter Parity Contract" or a new `docs/architecture/adapter-parity-contract.md` records concrete rules per adapter (SDK / CLI / MCP) for: idempotency-key sourcing, correlation-id sourcing, task-id sourcing, credential sourcing, pre-SDK error class, post-SDK error projection, audit metadata keys, terminal states
**And** the canonical CLI exit-code mapping table (0/64/65/66/67/68/69/70/71/72/73/74/75/1) is published as a reference table
**And** the canonical MCP failure-kind set is published with one-to-one mapping to canonical error categories

### Story 1.6: Author OpenAPI v1 Contract Spine with extension vocabulary

As an API consumer (REST / SDK / CLI / MCP),
I want a single canonical OpenAPI 3.1 Contract Spine artifact at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` defining every capability group's operations, schemas, errors, idempotency annotations, and lifecycle states,
So that all four surfaces are generated from one source of truth and cross-surface drift is mechanically impossible.

**Acceptance Criteria:**

**Given** Pre-Spine Workshop deliverables are complete (Stories 1.4 and 1.5)
**When** the Contract Spine is authored
**Then** `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` exists with `openapi: 3.1.0` and the URL versioning prefix `/api/v1/`
**And** the spine declares operations grouped by the eight capability groups (provider-readiness, folders, workspaces, files, commits, audit, ops-console, context-queries)
**And** the file-content transport is bimodal: `PutFileInline` (≤256KB JSON) and `PutFileStream` (multipart `application/octet-stream`) per D-9
**And** the spine declares the extension vocabulary `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier`, `x-hexalith-correlation`, `x-hexalith-lifecycle-states`, `x-hexalith-parity-dimensions`, `x-hexalith-audit-metadata-keys`, `x-hexalith-sensitive-metadata-tier` with JSON Schema references in `src/Hexalith.Folders.Contracts/openapi/extensions/`
**And** every mutating operation declares `x-hexalith-idempotency-equivalence` (lexicographic field list) and `x-hexalith-idempotency-ttl-tier` (one of `mutation` / `commit`)
**And** every operation declares `x-hexalith-parity-dimensions` per Story 1.5

**Given** the C3 and C4 values from Story 1.4
**When** the spine encodes input limits and lifecycle states
**Then** every context-query operation declares `maxItems`, `maxLength`, `maxBytes`, `maxResultCount` from `docs/exit-criteria/c4-input-limits.md`
**And** the spine declares `x-hexalith-lifecycle-states` listing the C6 11 states with operator-disposition labels
**And** the error response schemas use RFC 9457 `application/problem+json` with the canonical error fields (`category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details`)
**And** every operation that returns workspace state references the C6 state catalog rather than free-form strings
**And** `tests/fixtures/previous-spine.yaml` is updated with a frozen copy of the v1 spine to seed the symmetric-drift gate

### Story 1.7: Wire NSwag SDK generation with per-command ComputeIdempotencyHash() helpers

As an SDK consumer,
I want `Hexalith.Folders.Client` generated from the Contract Spine via NSwag with a per-command `ComputeIdempotencyHash()` helper auto-generated using the `x-hexalith-idempotency-equivalence` field list,
So that consumers never reimplement payload canonicalization and idempotency replay semantics are identical across every caller.

**Acceptance Criteria:**

**Given** the Contract Spine exists at `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
**When** `dotnet build src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` runs
**Then** NSwag executes as a build target consuming the spine and emits typed client methods, request/response DTOs, and per-command `ComputeIdempotencyHash()` helpers into `src/Hexalith.Folders.Client/Generated/`
**And** the generated `ComputeIdempotencyHash()` for every mutating command DTO produces a deterministic SHA-256 hash over the lexicographically ordered fields declared in `x-hexalith-idempotency-equivalence`, with values canonicalized as NFC-normalized UTF-8
**And** the bimodal file transport produces two distinct typed methods `PutFileInlineAsync` and `PutFileStreamAsync` (no `oneOf` union types)

**Given** the generated SDK exists
**When** unit tests run
**Then** every command DTO has at least one generated `ComputeIdempotencyHash()` equivalence test that asserts (a) two payloads with the same equivalence-fields produce the same hash, (b) two payloads with differing non-equivalence fields produce the same hash, (c) two payloads with differing equivalence fields produce different hashes
**And** the `idempotency-encoding-corpus.json` corpus is iterated by an encoding-equivalence test asserting that NFC/NFD/NFKC/NFKD/zero-width-joiner/ULID-case variants of the same logical key produce the same hash
**And** generated files are excluded from manual edits (they live under `Generated/` with an `<auto-generated/>` marker)

### Story 1.8: Generate the C13 parity oracle and validate it against parity-contract.schema.json

As a parity-test consumer (SDK / REST / CLI / MCP test projects),
I want `tests/fixtures/parity-contract.yaml` generated from the Contract Spine by `tests/tools/parity-oracle-generator/`, validated against `tests/fixtures/parity-contract.schema.json`, and consumed as xUnit theory data,
So that adding a Contract Spine command without a parity row, omitting required parity dimensions, or removing an operation without deprecation entry mechanically fails the build.

**Acceptance Criteria:**

**Given** the Contract Spine, NSwag-generated SDK, and `parity-contract.schema.json` exist
**When** the parity-oracle-generator tool runs (as a CI step or build target)
**Then** `tests/fixtures/parity-contract.yaml` is produced with one row per Contract Spine operation, populating both transport-parity columns (`auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`) and behavioral-parity columns (`pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `cli_exit_code`, `mcp_failure_kind`)
**And** the generator runs the per-class completeness assertion: mutating operations missing `idempotency_key_rule` cause generation failure; query operations missing `read_consistency_class` cause generation failure

**Given** `parity-contract.yaml` is generated
**When** validation runs
**Then** the generator validates the output against `parity-contract.schema.json` before writing; schema-validation failures stop the build with a clear error pointing to the offending row
**And** the symmetric-drift gate compares the current spine to `tests/fixtures/previous-spine.yaml`; operations removed without a deprecation-window entry fail the build
**And** an integration test in `Hexalith.Folders.Contracts.Tests` asserts every Contract Spine operation has exactly one row in `parity-contract.yaml`

### Story 1.9: Wire CI gates (server-vs-spine, golden-file, sentinel, encoding, exit-criteria-presence, pattern-examples, cache-key prefix)

As a maintainer,
I want a comprehensive CI pipeline (`.github/workflows/ci.yml` + supporting workflow files) that enforces server-vs-spine validation, NSwag golden-file consistency, sentinel-corpus redaction, idempotency-encoding equivalence, exit-criteria presence, pattern-examples compilation, cache-key tenant-prefix lint, and parity oracle consumption,
So that drift, redaction leaks, missing exit-criteria artifacts, and stale documentation snippets fail the build mechanically and consistently across every PR.

**Acceptance Criteria:**

**Given** the Contract Spine, NSwag pipeline, and parity oracle exist
**When** a PR opens
**Then** `.github/workflows/ci.yml` runs the BLOCKING server-vs-spine validation gate that compares the OpenAPI emitted by `Microsoft.AspNetCore.OpenApi` from `Hexalith.Folders.Server` to `hexalith.folders.v1.yaml` and fails on any divergence
**And** the NSwag golden-file gate runs `dotnet build` on `Hexalith.Folders.Client` and then `git diff --exit-code src/Hexalith.Folders.Client/Generated/` failing if generated outputs were hand-edited
**And** the parity-contract schema-validation gate runs the parity-oracle generator and validates the output against `parity-contract.schema.json` before any test consumes it
**And** the symmetric-drift gate compares the current Contract Spine to `tests/fixtures/previous-spine.yaml` and fails on operation removal without a deprecation entry

**Given** the redaction and encoding fixtures exist
**When** the CI pipeline runs
**Then** the sentinel-corpus iteration job runs sentinel tests against every output pipeline that any project emits (logs, traces, metrics labels, events, audit records, console payloads, provider diagnostics, error responses) and fails on any sentinel match in `audit-leakage-corpus.json`
**And** the idempotency-encoding-equivalence gate iterates `idempotency-encoding-corpus.json` and asserts every variant of the same logical key produces the same hash from `ComputeIdempotencyHash()`
**And** the cache-key tenant-prefix lint (Roslyn analyzer or grep-based) enforces that every cache-key construction in the codebase begins with `{tenantId}:` prefix; violations fail the build
**And** the exit-criteria-presence gate fails the release pipeline when any C0–C13 row in §"Exit Criteria Operations Plan" lacks an artifact link
**And** the pattern-examples compile gate compiles every C# snippet in §"Pattern Examples" of the architecture document against pinned package versions and fails on compilation error

## Epic 2: Tenant-Scoped Folder Management

Tenant administrators and authorized actors can create, inspect, and archive folders within a tenant with cross-tenant isolation enforced before any resource access, fail-closed-on-stale local tenant projection, layered authorization (JWT → claim transform → tenant projection → folder ACL → EventStore validators → Dapr policy), and metadata-only audit preserving evidence even after a folder is archived.

### Story 2.1: Stand up domain service host with Tenants integration and fail-closed local projection

As a platform engineer,
I want `Hexalith.Folders.Server` running as a domain service host inside the Aspire AppHost, subscribing to `system.tenants.events`, building a local fail-closed `FolderTenantAccessProjection`, and exposing a `TenantsAvailabilityCheck` health endpoint, plus a `TenantContextProvenanceMiddleware` that rejects payload-tenant authority,
So that every subsequent folder operation can authorize against a tenant identity that is verifiable, available under bounded staleness, and never trusted from request payloads.

**Acceptance Criteria:**

**Given** the Aspire AppHost has EventStore (`AppId=eventstore`), Tenants (`AppId=tenants`), and Folders.Server (`AppId=folders`) registered
**When** the AppHost starts locally
**Then** `dotnet run --project src/Hexalith.Folders.AppHost` brings up EventStore + Tenants + Folders.Server + Keycloak + Dapr sidecars + Redis state store + Redis Streams pub/sub with Aspire dashboard at `https://localhost:17000`
**And** `Hexalith.Folders.Server` starts via `AddEventStore()` / `UseEventStore()`, exposes `MapPost("/process")` and `MapPost("/project")` endpoints, and registers itself as `AppId=folders` for Dapr service invocation
**And** `Hexalith.Folders.Client.Subscription.MapTenantEventSubscription` consumes `system.tenants.events` via Dapr pub/sub and writes to `FolderTenantAccessProjection` in Dapr state under tenant-prefixed keys (per C10)

**Given** the local tenant-access projection is operational
**When** Tenants availability is checked
**Then** `/health/ready` reports degraded when Tenants pub/sub is unavailable beyond the configured staleness budget
**And** `TenantsAvailabilityCheck` returns the projection's last-update timestamp and degraded-mode flag
**And** read paths continue under bounded staleness; mutations require fresh authorization (synchronous Tenants query or rejection) per cross-cutting concern #20

**Given** a request arrives at any REST endpoint
**When** `TenantContextProvenanceMiddleware` runs
**Then** the authoritative tenant is taken from the JWT `eventstore:tenant` claim transformed by Hexalith.EventStore — never from the request body or query parameters
**And** any tenant identifier present in a request payload is treated as input that must validate against the auth-context tenant; mismatches return `tenant_access_denied` with safe error shape (no resource enumeration)
**And** an integration test in `Hexalith.Folders.Server.Tests` asserts a request with mismatched payload tenant vs. JWT tenant is rejected with the same canonical error category that an unauthorized cross-tenant request would receive

### Story 2.2: Implement Organization aggregate ACL baseline

As a tenant administrator,
I want an `OrganizationAggregate` per managed tenant where the minimum tenant + folder access controls are configured (verbs, default ACL templates, role-to-verb maps),
So that every folder created in this tenant inherits a coherent ACL baseline before it has any folder-specific overrides.

**Acceptance Criteria:**

**Given** a managed tenant exists in `FolderTenantAccessProjection`
**When** `ConfigureTenantAccessControls` is submitted via the EventStore command API by an authorized tenant administrator
**Then** `OrganizationAggregate.Handle(ConfigureTenantAccessControls, OrganizationState?, CommandEnvelope)` is invoked as a pure function and returns `DomainResult.Success` containing `TenantAccessControlsConfigured` event with envelope metadata (`tenantId`, `domain=organizations`, `aggregateId`, `messageId`, `correlationId`, `causationId`, `timestamp`, `userId`, `eventTypeName`)
**And** aggregate identity is `{managedTenantId}:organizations:{organizationId}` and never `system:organizations:*`
**And** the resulting `OrganizationState` lists the configured verbs (create, configure provider binding, prepare workspace, lock workspace, read metadata, read file content where allowed, mutate files, commit, query status, query audit, view ops console)
**And** the aggregate handler is pure: no Dapr calls, no HTTP, no file I/O, no time-of-day reads (uses `envelope.Timestamp`), no random calls

**Given** the configuration is persisted
**When** the projection rebuilds from the event stream
**Then** rebuilding produces equivalent state from the same ordered event stream (read-model determinism)
**And** an aggregate test asserts that an unauthorized actor (lacking the tenant administrator role per `eventstore:permission`) submitting `ConfigureTenantAccessControls` is rejected with `tenant_access_denied`
**And** an Idempotency-Key with same payload returns the same result; same key with different payload returns `idempotency_conflict`

### Story 2.3: Create folders within a tenant

As an authorized actor (tenant administrator or developer with create permission),
I want to create logical folders within my tenant via `POST /api/v1/folders` (and the equivalent EventStore `CreateFolder` command),
So that subsequent provider-readiness checks, repository binding, and workspace work have a folder to attach to.

**Acceptance Criteria:**

**Given** the actor has `eventstore:permission=command:submit` and the tenant ACL grants `folder:create`
**When** `CreateFolder` is submitted with an `Idempotency-Key`, `X-Correlation-Id`, and folder name + optional description
**Then** `FolderAggregate.Handle(CreateFolder, FolderState?, CommandEnvelope)` returns `DomainResult.Success` with a `FolderCreated` event including the assigned opaque ULID `folderId`, the actor identity, and a `tenant-sensitive` classified folder name
**And** aggregate identity is `{managedTenantId}:folders:{folderId}` (never `system:folders:*`); folderId is opaque ULID per AR-PATTERN-01
**And** the REST endpoint `POST /api/v1/folders` returns `202 Accepted` with `folderId`, `correlationId`, and a status-poll URL; never blocks on EventStore persistence
**And** the response uses camelCase JSON, ISO-8601-Z timestamps, and `application/problem+json` for any error

**Given** the `FolderCreated` event is published
**When** the `FolderListProjection` handler processes it
**Then** the folder appears in `FolderListProjection` keyed by `{tenantId}:folder-list:{folderId}` (per C10 cache-key tenant-prefix invariant)
**And** an `AuditProjection` entry is emitted with metadata-only fields (tenant, folderId, actor, operationId, correlationId, timestamp, eventTypeName=`FolderCreated`); no file contents or secrets appear
**And** sentinel-redaction tests over `audit-leakage-corpus.json` find no leak in the folder name field (confirmed against the `tenant-sensitive` classification at the projection level)
**And** a cross-tenant negative test asserts that a request from tenant B cannot read or list a folder created by tenant A; the denial returns the same error category as a missing folder (`not_found` or `tenant_access_denied`) with safe error shape

### Story 2.4: Grant and revoke folder access for users, groups, roles, and delegated service agents

As a tenant administrator,
I want to grant and revoke folder-scoped access for users, groups, roles, and delegated service agents via `GrantFolderAccess` / `RevokeFolderAccess` commands,
So that I can onboard automation teams, retire chatbot projects, and rotate ownership without losing audit history.

**Acceptance Criteria:**

**Given** an existing folder owned by tenant T and an actor with `folder:configure-acl` permission on that folder
**When** `GrantFolderAccess` is submitted specifying `principal` (one of `user`, `group`, `role`, `service-agent`), `principalId`, and `verbs` (subset of the verb catalog)
**Then** `FolderAggregate.Handle(GrantFolderAccess, FolderState, CommandEnvelope)` returns `DomainResult.Success` with `FolderAccessGranted` event capturing `principal`, `principalId`, `verbs`, `actor`, `timestamp`, `correlationId`
**And** `RevokeFolderAccess` with the same `principal` + `principalId` produces `FolderAccessRevoked` with no remaining verbs for that principal
**And** the resulting state is reflected in `FolderAclProjection` keyed by `{tenantId}:folder-acl:{folderId}`
**And** revoking a non-existent grant returns `DomainResult.NoOp` (idempotent) rather than rejecting

**Given** a grant or revoke is replayed with the same `Idempotency-Key`
**When** the equivalent payload is submitted
**Then** the same logical result is returned; a same-key + different-payload submission returns `idempotency_conflict`
**And** an audit entry is written for both grant and revoke operations with metadata only (no file contents, no token material, principal identifier classified per S-6 sensitive-metadata classifier)
**And** a cross-tenant negative test asserts that tenant B cannot grant or revoke access on a folder belonging to tenant A; the denial happens before any folder lookup occurs

### Story 2.5: Inspect effective permissions for a folder or task context

As an authorized actor (tenant administrator or audit reviewer),
I want to query effective permissions for a folder or task context via `GET /api/v1/folders/{folderId}/effective-permissions`,
So that I can answer "who can write here today?" with concrete evidence drawn from the projected ACL.

**Acceptance Criteria:**

**Given** an existing folder with grants from Story 2.4 and an actor with `folder:read-acl` permission
**When** `GET /api/v1/folders/{folderId}/effective-permissions[?principal={id}]` is called
**Then** the response returns the resolved permission set: per principal, the verb list, grant source (direct, group, role), grant timestamp, and granting actor; metadata only (no token material, no credential references)
**And** the response is `eventually-consistent` per Story 1.5 read-consistency declaration, with `X-Hexalith-Freshness` header indicating the projection age
**And** the request is denied with `tenant_access_denied` if the actor lacks `folder:read-acl` or if the folder belongs to a different tenant; denial uses safe error shape (no resource enumeration)

**Given** the principal lookup spans group and role membership
**When** the projection resolves effective permissions
**Then** the resolution honors group membership and role assignments from the local `FolderTenantAccessProjection` and the folder's own `FolderAclProjection`
**And** an authorization-evidence audit entry is emitted with actor, query type, target folderId, correlationId, timestamp, and result classification (`allowed` or `denied`)

### Story 2.6: Enforce layered authorization with cross-tenant denial and safe error shapes

As any system component executing a folder operation,
I want layered authorization (JWT validation → Hexalith.EventStore claim transform → local fail-closed tenant-access projection → folder ACL check → EventStore validators) to run before any folder, repository, credential, lock, file, audit, provider, or context information is exposed,
So that cross-tenant access is denied before any resource is touched and denials never enumerate unauthorized resources.

**Acceptance Criteria:**

**Given** any request arrives at `Hexalith.Folders.Server` for a folder operation
**When** the authorization pipeline runs
**Then** `TenantAccessAuthorizer` first validates the JWT, then uses the `eventstore:tenant` claim, then consults `FolderTenantAccessProjection` (fail-closed when stale beyond budget), then checks `FolderAclAuthorizer` against `FolderAclProjection`, and only then dispatches to the aggregate
**And** every layer's decision is captured in an audit-evidence record (`actor`, `tenant`, `folderId`, `verb`, `decision`, `reason`, `correlationId`, `timestamp`)
**And** a cross-tenant request (correct JWT for tenant A, target folder owned by tenant B) is denied at the tenant layer; the response returns `tenant_access_denied` with safe error shape — no folder name, repository name, or existence indicator is leaked
**And** a tenant-correct but ACL-insufficient request is denied at the folder ACL layer with `folder_acl_denied` and the same safe-error shape

**Given** the local `FolderTenantAccessProjection` is stale beyond the freshness budget
**When** an authorization decision is required
**Then** for read paths, the projection is consulted under bounded staleness; the response includes `X-Hexalith-Freshness`
**And** for mutating paths, fail-closed behavior triggers a synchronous Tenants query or rejection with `tenant_access_unknown` (retryable per `clientAction=wait_and_retry`)
**And** an isolation-test suite covers API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, audit records, and context-query results — asserting no cross-tenant leakage in any output channel

### Story 2.7: Inspect folder lifecycle and binding status

As an authorized actor,
I want to inspect a folder's lifecycle state, binding metadata, and creation/archive timestamps via `GET /api/v1/folders/{folderId}` and list folders via `GET /api/v1/folders`,
So that operators and tenant administrators can answer "which folders exist, who owns them, and what is their current state" without needing direct EventStore access.

**Acceptance Criteria:**

**Given** an actor with `folder:read-metadata` permission and existing folders in the tenant
**When** `GET /api/v1/folders/{folderId}` is called
**Then** the response includes `folderId` (opaque ULID), `tenantId`, folder name (sensitivity-classified per S-6), description, lifecycle state (`active` or `archived` for MVP), creation actor + timestamp, last-update timestamp, and binding metadata if a repository is bound (handled in Epic 3) with provider name + binding identifier — never any credential material or repository URL with embedded credentials
**And** `GET /api/v1/folders` returns a paginated list of folders for the authenticated tenant; pagination follows the conventions declared in Story 1.6 Contract Spine
**And** the response sets `X-Correlation-Id` and `X-Hexalith-Freshness`; consistency model is `eventually-consistent` per the spine declaration
**And** a cross-tenant request returns `not_found`-shaped denial; existence of folders in the other tenant is not leaked

**Given** the actor lacks `folder:read-metadata`
**When** any folder query is attempted
**Then** the request is denied with `folder_acl_denied` using safe error shape; an audit entry captures the denial

### Story 2.8: Archive folders with retention-bound audit and status preservation

As a tenant administrator,
I want to archive a folder via `ArchiveFolder` so that mutating operations are denied with a stable error while metadata-only audit, lock lifecycle history, last commit reference, and operation timeline remain queryable for the tenant's retention policy duration,
So that I can retire chatbot projects without erasing the audit evidence future incident reviews depend on.

**Acceptance Criteria:**

**Given** an actor with `folder:archive` permission on an active folder
**When** `ArchiveFolder` is submitted with an `Idempotency-Key` and `X-Correlation-Id`
**Then** `FolderAggregate.Handle(ArchiveFolder, FolderState, CommandEnvelope)` returns `DomainResult.Success` with `FolderArchived` event including `actor`, `timestamp`, `correlationId`, and the archive lifecycle reason
**And** `FolderState` transitions to `archived`; the archive timestamp is preserved
**And** subsequent mutating commands (CreateFolder against the same id, GrantFolderAccess, ConfigureProviderBinding, PrepareWorkspace, LockWorkspace, AddFile, ChangeFile, RemoveFile, CommitWorkspace) are rejected with stable error category `folder_archived` using safe error shape

**Given** a folder is archived
**When** an authorized actor queries archived-folder metadata or audit
**Then** `GET /api/v1/folders/{folderId}` returns the folder with `lifecycleState=archived`, archive timestamp, last commit reference (if any), and lock lifecycle history; metadata-only — no file contents, no provider tokens, no credential material
**And** `GET /api/v1/folders/{folderId}/audit-trail` continues to return the operation timeline for the duration of the tenant's `c3-retention.md` retention period
**And** the same denial and isolation guarantees that protect active folders also protect archived ones (cross-tenant negative test confirms)
**And** when the C3 retention period expires for a given record class, the cleanup process removes the eligible records but does NOT remove audit evidence required to reconstruct completed/failed/denied/retried/duplicate/interrupted operations (per NFR58)

### Story 2.9: React to Tenants events through Worker handlers

As the system,
I want `Hexalith.Folders.Workers` to react to `system.tenants.events` via four handlers (`TenantDisabledHandler`, `UserRemovedFromTenantHandler`, `UserRoleChangedHandler`, `TenantConfigurationSetHandler`) that submit follow-up commands to update folder ACLs and processed `folders.*` configuration keys only,
So that tenant administrative changes (disabling a tenant, removing a user, role rotation, configuration updates) reflect in folder authorization within the C7 freshness budget without any Folders-internal admin path being needed.

**Acceptance Criteria:**

**Given** Hexalith.Tenants emits a `TenantDisabled` event
**When** `TenantDisabledHandler` processes it from `system.tenants.events`
**Then** the handler updates `FolderTenantAccessProjection` to mark the tenant disabled (no future authorization decisions for that tenant succeed) and submits a follow-up command to mark all affected folders inaccessible for new mutations (existing locks proceed to revalidation per Epic 4)
**And** the handler is idempotent by causation/correlation ID per AR-WORKER-01

**Given** Hexalith.Tenants emits `UserRemovedFromTenant` for principal P in tenant T
**When** `UserRemovedFromTenantHandler` processes it
**Then** the handler iterates folders in tenant T where P holds direct grants and submits `RevokeFolderAccess` for each; group/role grants are recomputed via the projection without per-folder commands
**And** an audit entry is emitted for each revoke with `causationId` linking to the Tenants event

**Given** Hexalith.Tenants emits `UserRoleChanged` for principal P in tenant T
**When** `UserRoleChangedHandler` processes it
**Then** the handler updates `FolderTenantAccessProjection` to reflect the new role-to-verb mapping; effective-permissions queries (Story 2.5) reflect the change within the C7 freshness budget

**Given** Hexalith.Tenants emits `TenantConfigurationSet` with keys including `folders.*` and other namespaces
**When** `TenantConfigurationSetHandler` processes it
**Then** only `folders.*` keys are processed; other namespaces are ignored
**And** processed keys update `FolderTenantAccessProjection` or trigger follow-up commands as appropriate
**And** all four handlers run sentinel-redaction tests asserting no Tenants-event payload field leaks into Folders logs/audit beyond classified metadata

## Epic 3: Provider Readiness & Repository Binding

Platform engineers can configure GitHub and Forgejo provider bindings + credential references on a tenant's `OrganizationAggregate`, validate provider readiness with stable safe-reason codes before any repository-backed folder is created, expose capability differences between providers explicitly, define branch/ref policy, and bind folders to repositories — preventing avoidable runtime failures by failing fast at provisioning time rather than mid-task.

### Story 3.1: Configure provider binding and credential reference for a tenant

As a platform engineer,
I want to configure a Git provider binding (GitHub or Forgejo) with a credential reference and repository naming policy on the tenant's `OrganizationAggregate` via `ConfigureProviderBinding`,
So that subsequent readiness checks and repository-backed folder operations have an authoritative provider configuration to evaluate against without storing or echoing token material.

**Acceptance Criteria:**

**Given** an authorized tenant administrator with `tenant:configure-provider` permission and an active `OrganizationAggregate` from Story 2.2
**When** `ConfigureProviderBinding` is submitted with `providerKind` (`github` or `forgejo`), `credentialReferenceId`, `repositoryNamingPolicy`, and `defaultBranchPolicy`
**Then** `OrganizationAggregate.Handle(ConfigureProviderBinding, OrganizationState, CommandEnvelope)` returns `DomainResult.Success` with `ProviderBindingConfigured` event including the binding identifier, providerKind, credentialReferenceId (reference only — never token material), naming policy, and default branch policy
**And** the credential reference resolves through Hexalith.Tenants or a Dapr secret store; the resolved token is loaded only at provider-call time inside the provider adapter and never written to any event, projection, log, trace, audit record, or error response
**And** `OrganizationState` reflects the active binding; subsequent `ConfigureProviderBinding` for the same providerKind updates the binding (idempotent if equivalent payload; rejects with `idempotency_conflict` on differing payload with same key)

**Given** the binding is persisted
**When** an authorized actor queries `GET /api/v1/organizations/{organizationId}/provider-bindings`
**Then** the response returns provider, binding identifier, credentialReferenceId, repository naming policy, default branch policy, last-update timestamp, and binding state — never any token, raw credential material, repository URL with embedded credentials, or unauthorized provider details (FR21)
**And** sensitive metadata classification per S-6 is applied: branch-policy values are `tenant-sensitive` by default; the response respects per-tenant overrides
**And** sentinel-redaction tests over `audit-leakage-corpus.json` find no leak in event payloads or query responses

### Story 3.2: Define IGitProvider port with N-provider capability model and capability-difference exposure

As an architect (and downstream provider adapter author),
I want a capability-discoverable `IGitProvider` port surfaced from `Hexalith.Folders/Providers/Abstractions/`, plus a query exposing the capability matrix per provider,
So that GitHub and Forgejo capability differences are reported explicitly rather than inferred by clients from failed operations, and adding a third provider does not require port redesign.

**Acceptance Criteria:**

**Given** the architecture's provider abstractions location
**When** the port is defined
**Then** `IGitProvider` declares the methods needed by the canonical lifecycle: `ValidateReadinessAsync`, `CreateOrBindRepositoryAsync`, `PrepareWorkspaceAsync`, `ApplyFileOperationsAsync`, `CommitAsync`, `GetProviderStatusAsync`, `CleanupAsync` — each taking a `ProviderRequest` carrying `tenantId`, `correlationId`, `taskId`, `bindingId`, and operation-specific payload, and returning a `ProviderResult` with `outcome` (`succeeded` / `known-failure` / `unknown-outcome`), categorized failure (per `ProviderFailureCategory`), and retryability
**And** `ProviderCapabilities` exposes a typed feature-flag set (e.g., `SupportsBranchProtection`, `SupportsForcePushPolicy`, `SupportsRepositoryCreate`, `SupportsLfs`, `SupportsScopedTokens`, `MaxFileSize`, `RateLimitWindow`) — extensible without breaking existing adapters
**And** `ProviderFailureCategory` enumerates `timeout`, `unauthorized`, `forbidden`, `not_found`, `conflict`, `rate_limited`, `server_error`, `branch_protection`, `repository_missing_or_deleted`, `stale_clone`, `credential_revoked`, `provider_unavailable`, `unknown_outcome`

**Given** `IGitProvider` is implemented by GitHub and Forgejo adapters (Stories 3.3 and 3.4)
**When** an authorized actor calls `GET /api/v1/provider-capabilities?providerKind={github|forgejo}` (or a comparison endpoint returning both)
**Then** the response returns the typed capability matrix per provider
**And** an explicit "capability difference" view is available enumerating dimensions where the providers diverge (e.g., GitHub Apps fine-grained permissions vs Forgejo scoped tokens; branch-protection model differences) per FR22
**And** `tests/Hexalith.Folders.Tests/Providers/ProviderPortConformance/` contains a conformance suite asserting both adapters implement every port method and report capabilities consistently

### Story 3.3: Implement GitHub provider adapter using Octokit

As a platform engineer,
I want `Hexalith.Folders.Providers.GitHub.GitHubProvider` implementing `IGitProvider` using Octokit 14.0.0 with GitHub Apps fine-grained permissions, a `GitHubFailureClassifier`, and per-tenant rate-limit token bucket scaffolding,
So that GitHub-backed folders can complete the canonical lifecycle with categorized failure reporting and unknown-outcome handling that never duplicates repositories, file changes, or commits.

**Acceptance Criteria:**

**Given** `IGitProvider` from Story 3.2 and the Octokit 14.0.0 dependency pinned in `Directory.Packages.props`
**When** `GitHubProvider` is implemented
**Then** every `IGitProvider` method delegates to Octokit calls inside `Hexalith.Folders.Providers.GitHub` and the Octokit reference is not surfaced beyond that namespace
**And** `GitHubReadinessChecker` validates: provider availability, credential reference resolves to a valid token, token has required permissions for the canonical lifecycle (repo creation, branch policy, file ops, commit), default branch policy is valid, repository naming policy is valid
**And** `GitHubFailureClassifier` maps Octokit exceptions and HTTP status to `ProviderFailureCategory` covering timeout, 401, 403, 404, 409, 429, 5xx, branch-protection, missing-or-deleted repository, stale clone, credential revocation
**And** unknown-outcome handling: when an Octokit call times out or returns ambiguous state, `GitHubProvider` returns `ProviderResult { outcome = unknown-outcome }` and never silently retries — escalating to `reconciliation_required` per AR-PROVIDER-05

**Given** the provider adapter is deployed
**When** the per-tenant rate-limit token bucket scaffolding from `Hexalith.Folders.Workers.RateLimiting.PerTenantTokenBucket` is consulted
**Then** user-driven calls per tenant are throttled per-tenant; bucket exhaustion returns `rate_limited` with the next-attempt hint
**And** the hermetic-PR-gate provider contract test suite at `tests/Hexalith.Folders.Tests/Providers/GitHub/` exercises pinned fixtures covering readiness, repository creation, branch handling, file operations, commit, every failure category, retry/idempotency behavior, and unknown-outcome handling
**And** sentinel-redaction tests assert no provider tokens, raw URLs with embedded credentials, or Octokit exception messages containing secrets leak into events, logs, traces, projections, audit records, or error responses

### Story 3.4: Implement Forgejo provider adapter with per-version snapshots and oasdiff drift detection

As a platform engineer,
I want `Hexalith.Folders.Providers.Forgejo.ForgejoProvider` implementing `IGitProvider` as a typed HttpClient wrapper fed by per-version `swagger.v1.json` snapshots, with a `supported-versions.json` matrix, a hermetic-PR-gate contract suite, and a nightly oasdiff drift-detection workflow,
So that Forgejo-backed folders complete the canonical lifecycle and per-instance OpenAPI variation is detected as drift before it causes runtime failures.

**Acceptance Criteria:**

**Given** the Forgejo per-instance OpenAPI behavior
**When** `ForgejoProvider` is implemented
**Then** the adapter is a hand-written typed HttpClient wrapper inside `Hexalith.Folders.Providers.Forgejo` with no generated client; capability methods use Forgejo scoped-token authentication
**And** `tests/contracts/forgejo/<version>/swagger.v1.json` snapshots are checked in for the test matrix declared in `tests/contracts/forgejo/supported-versions.json` (minimum: latest stable, latest LTS, n-1 minor, plus any pinned customer instance)
**And** `ForgejoFailureClassifier` maps HTTP status to the same `ProviderFailureCategory` set used by GitHub (Story 3.3) so cross-provider parity tests can assert equivalent error categorization

**Given** `.github/workflows/nightly-drift.yml` runs daily plus a weekly `HEAD` poll
**When** oasdiff compares the live Forgejo OpenAPI to each pinned snapshot
**Then** additive changes (new optional fields, new operations) emit a `warn` with a CI annotation
**And** breaking changes (removed fields, type narrowing, removed operations, security-scheme changes) `fail` the nightly job and surface a CI annotation pointing to the affected snapshot file
**And** a fixture-to-failure-mode coverage matrix is asserted in CI: every entry in `ProviderFailureCategory` has at least one fixture in `tests/contracts/forgejo/<version>/`
**And** response-equivalence tests compare GitHub adapter and Forgejo adapter port-shape outcomes for the same logical operation; divergence requires explicit acknowledgment in the architecture document via the FR22 capability-difference matrix from Story 3.2

### Story 3.5: Validate provider readiness with safe diagnostics and stable reason codes

As a platform engineer or tenant administrator,
I want to validate that a tenant's provider binding is ready for the canonical lifecycle via `ValidateProviderReadiness`, receiving a stable reason code, retryability hint, remediation category, and correlation ID — and seeing the result projected in `ProviderReadinessProjection`,
So that I can correct provider configuration before any agent task fails halfway through repository creation or commit.

**Acceptance Criteria:**

**Given** a tenant `OrganizationAggregate` with a configured provider binding from Story 3.1
**When** `ValidateProviderReadiness` is submitted with the binding identifier and an `Idempotency-Key`
**Then** `OrganizationAggregate.Handle(ValidateProviderReadiness, OrganizationState, CommandEnvelope)` invokes the configured provider's `ValidateReadinessAsync` (Octokit for GitHub or typed HttpClient for Forgejo) via a process-manager worker; the worker submits a follow-up `ProviderReadinessValidated` or `ProviderReadinessFailed` command using the original correlation/causation IDs
**And** the resulting event includes a stable reason code from the catalog: `missing_credential_reference`, `credential_invalid`, `insufficient_permissions`, `provider_unavailable`, `unsupported_capability`, `invalid_branch_policy`, `repository_naming_policy_invalid`, `repository_conflict`, `rate_limited`, `unknown_outcome`
**And** the response includes `retryable` (boolean), `clientAction` (one of `wait_and_retry`, `change_input`, `request_authorization`, `escalate`), `remediationCategory`, `providerReference`, `correlationId`, `timestamp` — never any credential material or token

**Given** the readiness event is projected
**When** `GET /api/v1/provider-readiness?bindingId={id}` is called
**Then** `ProviderReadinessProjection` returns the latest readiness state per binding (`ready`, `failed`, `unknown`, `revalidating`), reason code, retryability, last-validation timestamp, and last-failure timestamp
**And** an idempotency-replayed validation with the same key returns the same readiness result when provider evidence is unchanged; if provider evidence has changed, a fresh validation is performed and the new result replaces the old projection
**And** sentinel-redaction tests assert no credential material leaks into the readiness diagnostics, audit records, or error responses (FR17)

### Story 3.6: Create repository-backed folder and bind existing repository

As an authorized actor,
I want to either create a new Git-backed repository for an existing folder via `CreateRepositoryBackedFolder` (or extend `CreateFolder` from Story 2.3) or bind a folder to an existing repository via `BindRepository`, gated by a green provider-readiness check,
So that the folder transitions from `requested` to `preparing` to `ready` in the C6 workspace state machine and subsequent workspace tasks have a real Git target to operate on.

**Acceptance Criteria:**

**Given** an existing folder from Story 2.3, a configured provider binding from Story 3.1, and a `ProviderReadinessProjection` showing `ready` for that binding from Story 3.5
**When** `CreateRepositoryBackedFolder` is submitted (either as a one-shot command or as `CreateFolder` + provider arguments) with an `Idempotency-Key` and `X-Hexalith-Task-Id`
**Then** `FolderAggregate.Handle(CreateRepositoryBackedFolder, FolderState, CommandEnvelope)` returns `DomainResult.Success` with `FolderGitRepositoryRequested` event including the providerKind, bindingId, requested repository name, and default branch policy reference
**And** the folder transitions from `requested` to `preparing` per the C6 transition matrix; `RepositoryProvisioningWorkflow` in `Hexalith.Folders.Workers` reacts and calls the provider's `CreateOrBindRepositoryAsync` via the provider port
**And** on success, the worker submits `MarkRepositoryBound` which produces `RepositoryBound` event; folder transitions to `ready`
**And** on known failure, the worker submits `MarkRepositoryBindingFailed` with categorized reason; folder transitions to `failed`
**And** on unknown outcome, the worker submits `MarkProviderOutcomeUnknown`; folder transitions to `unknown_provider_outcome`; reconciler is scheduled per AR-PROVIDER-05 (no silent retry)

**Given** an authorized actor wants to bind to an existing repository
**When** `BindRepository` is submitted with the existing-repository identifier
**Then** equivalent state transitions happen via `BindRepositoryRequested` → provider's `CreateOrBindRepositoryAsync` (in bind mode) → `RepositoryBound` or failure
**And** the system rejects duplicate bindings (same provider + same repository identifier already bound) with `repository_conflict`
**And** the system rejects cross-tenant bindings (a folder in tenant A cannot bind a repository already bound by tenant B) with `tenant_access_denied` using safe error shape
**And** an Idempotency-Key replay with the same payload returns the same result; same key + different payload returns `idempotency_conflict`

### Story 3.7: Define branch/ref policy for repository-backed folder tasks

As an authorized actor (tenant administrator or platform engineer),
I want to define the branch/ref policy used by repository-backed folder tasks via `DefineBranchRefPolicy` (organization-scoped default + per-folder override),
So that workspace preparation, file operations, and commits in Epic 4 always target a deterministic, policy-compliant branch.

**Acceptance Criteria:**

**Given** an `OrganizationAggregate` with an active provider binding
**When** `DefineBranchRefPolicy` is submitted with `policyKind` (e.g., `protected-default-branch`, `feature-branch-per-task`, `main-only`), default branch name, allowed ref-name patterns, and force-push posture (always disallowed in MVP for protected branches)
**Then** `OrganizationAggregate.Handle(DefineBranchRefPolicy, OrganizationState, CommandEnvelope)` returns `DomainResult.Success` with `BranchRefPolicyConfigured` event
**And** subsequent `ValidateProviderReadiness` (Story 3.5) re-runs and incorporates the branch/ref policy into the readiness evaluation; an invalid policy produces `invalid_branch_policy` reason code
**And** a per-folder override can be applied via `DefineFolderBranchRefPolicy` on `FolderAggregate`; precedence is folder override > organization default

**Given** a branch/ref policy is configured
**When** any subsequent workspace-preparation or commit operation runs (Epic 4)
**Then** the policy is consulted to derive the target branch/ref; operations targeting branches outside the policy are rejected with `branch_ref_policy_denied`
**And** the policy is exposed in folder/binding metadata responses (Story 3.1) without revealing token material
**And** sentinel tests verify branch names treated as `tenant-sensitive` per S-6 are redacted in cross-tenant operator views

### Story 3.8: Inspect tenant readiness and per-provider readiness evidence

As a platform engineer or tenant administrator,
I want to inspect whether a tenant is ready to run repository-backed workspace tasks via `GET /api/v1/tenants/{tenantId}/readiness` (FR7) and `GET /api/v1/provider-readiness?providerKind={kind}` (FR23),
So that I get a single answer to "can my agents do real work today?" backed by readiness projection + provider contract test evidence.

**Acceptance Criteria:**

**Given** an actor with `tenant:read-readiness` permission on a tenant with at least one configured provider binding
**When** `GET /api/v1/tenants/{tenantId}/readiness` is called
**Then** the response aggregates `ProviderReadinessProjection` rows for every binding the tenant has configured and returns `tenantReadiness` (`ready` if all bindings ready; `partial` if some ready and some failed; `failed` if no bindings ready; `unknown` if any are revalidating)
**And** each per-binding entry includes providerKind, bindingId, lastValidationTimestamp, reason code if not ready, retryability, and remediation category
**And** the response sets `X-Hexalith-Freshness` and uses `eventually-consistent` semantics

**Given** the live-nightly-drift contract test suite from Story 3.4 produces evidence about provider behavior
**When** `GET /api/v1/provider-readiness?providerKind=github|forgejo` is called by a platform engineer with `platform:read-provider-evidence` permission
**Then** the response returns the latest contract-suite outcome for that provider: pass/fail per capability area (readiness, repository binding, branch/ref handling, file operations, commit, status, failure behavior), last-run timestamp, and any active drift annotations
**And** the response excludes any tenant-specific data; this is platform-level evidence about the provider itself
**And** failed contract entries include the safe failure category and the affected capability area without exposing internal stack traces or secrets

## Epic 4: Repository-Backed Task Lifecycle (Workspace → Lock → Files → Commit)

Developers and AI agents can complete the canonical repository-backed task lifecycle: prepare a workspace from a `ready` folder, acquire a task-scoped exclusive lock, add/change/remove files (with workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding/Unicode normalization, case-collision handling), query file context via tree/search/glob/bounded range reads, commit Git-backed changes idempotently, and inspect stable terminal or intermediate state on interruption. The lifecycle honors the C6 11-state transition matrix, two-tier idempotency TTLs (`mutation = 24h`; `commit = retention-period(C3)`), single-active-writer locks per tenant/folder/workspace scope, working-copy ephemerality (D-8), `unknown_provider_outcome → reconciliation_required` (no silent retry per AR-PROVIDER-05), the canonical error taxonomy (FR43–FR46), bounded MVP input limits (NFR25), and metadata-only audit + redaction (FR55 write-side).

### Story 4.1: Implement Folder aggregate state machine with C6 transition matrix

As a platform engineer,
I want `FolderStateTransitions.cs` implementing the C6 11-state transition matrix (every `(currentState, eventType)` → `DomainResult` outcome including reconciliation paths) and `FolderAggregate.Apply` methods for every event family, plus the C6 transition-matrix coverage CI gate,
So that the workspace state machine is canonical and any unlisted `(state, event)` pair rejects with `state_transition_invalid` rather than allowing undefined transitions to leak into projections and audit.

**Acceptance Criteria:**

**Given** the C6 enumerated matrix from `docs/architecture/workspace-state-transition-matrix.md` (Story 1.4)
**When** `FolderStateTransitions.cs` is implemented in `src/Hexalith.Folders/Aggregates/Folder/`
**Then** the file is a switch expression over `(currentState, eventType)` returning `DomainResult` (`Success(events)`, `Rejection(reason)`, or `NoOp`) covering all documented transitions: `(none) → requested`, `requested → preparing`, `requested → failed`, `requested → unknown_provider_outcome`, `preparing → ready`, `preparing → failed`, `preparing → unknown_provider_outcome`, `ready → locked`, `ready → inaccessible`, `ready → reconciliation_required`, `locked → changes_staged`, `locked → ready`, `locked → dirty`, `locked → inaccessible`, `changes_staged → changes_staged`, `changes_staged → committed`, `changes_staged → failed`, `changes_staged → unknown_provider_outcome`, `changes_staged → dirty`, `committed → ready`, `dirty → reconciliation_required`, `failed → reconciliation_required`, `failed → ready`, `inaccessible → ready`, `unknown_provider_outcome → ready`, `unknown_provider_outcome → committed`, `unknown_provider_outcome → failed`, `unknown_provider_outcome → reconciliation_required`, `reconciliation_required → ready`, `reconciliation_required → committed`, `reconciliation_required → failed`
**And** any `(state, event)` pair not enumerated returns `Rejection(state_transition_invalid)`; idempotency record per AR-IDEMP-01 captures the rejection inspectably
**And** `FolderState.Apply` methods exist for every event in the architecture's event vocabulary; replaying any ordered event stream produces equivalent state (read-model determinism, NFR50)

**Given** the matrix-coverage CI gate from Story 1.9
**When** the aggregate-test suite runs
**Then** for every state in the catalog and every event type, at least one xUnit test asserts the documented outcome (positive transition OR explicit rejection with `state_transition_invalid`)
**And** CI fails if a new state or event is added without corresponding test coverage
**And** `FolderAggregate` handlers are pure: no Dapr/HTTP/file/secret/DB calls; time read from `envelope.Timestamp`; randomness derived from `causationId` per AR-DOMAIN-03

### Story 4.2: Prepare workspace from a ready repository-backed folder

As an authorized actor with a task in flight,
I want to prepare a workspace via `PrepareWorkspace` against a folder in `ready` state with a bound repository,
So that the working copy is materialized inside the per-AppHost ephemeral root and the folder transitions to `preparing` then back to `ready` (or to `failed` / `unknown_provider_outcome` per C6) ready to receive a task lock.

**Acceptance Criteria:**

**Given** a folder in `ready` state with `RepositoryBound` event applied (from Story 3.6) and an actor with `workspace:prepare` permission
**When** `PrepareWorkspace` is submitted with `Idempotency-Key`, `X-Correlation-Id`, `X-Hexalith-Task-Id`, and the target branch/ref derived from the policy in Story 3.7
**Then** `FolderAggregate.Handle(PrepareWorkspace, FolderState, CommandEnvelope)` returns `DomainResult.Success` with `WorkspacePreparationRequested` event; folder transitions `ready → preparing`
**And** `WorkspacePreparationWorkflow` in `Hexalith.Folders.Workers` reacts: `WorkingCopyManager` materializes the working copy at `/var/lib/hexalith-folders/work/{tenantId}/{folderId}/{taskId}` (D-8); on success, the worker submits `MarkWorkspacePrepared` producing `WorkspacePrepared` event; folder transitions `preparing → ready`
**And** on known failure, worker submits `MarkWorkspacePreparationFailed`; folder transitions to `failed` with categorized reason
**And** on unknown outcome, worker submits `MarkProviderOutcomeUnknown`; folder transitions to `unknown_provider_outcome`; reconciler scheduled — never silent retry

**Given** an Idempotency-Key replay
**When** equivalent `PrepareWorkspace` is submitted with the same key
**Then** the same logical result is returned (existing workspace, not re-prepared); same key + different payload returns `idempotency_conflict`
**And** the working-copy directory is treated as an "acceptably lost on restart" cache per concern #21; restart rebuilds it from events + provider on next preparation
**And** REST returns `202 Accepted` with `correlationId` and a status-poll URL; command-acknowledge p95 < 1 second per NFR21
**And** sentinel-redaction tests assert no working-copy paths or repository URLs with embedded credentials leak into events, projections, logs, traces, audit records, or error responses

### Story 4.3: Acquire task-scoped workspace lock with deterministic single-active-writer enforcement

As an authorized actor,
I want to acquire a task-scoped workspace lock via `LockWorkspace` with deterministic conflict response when another task holds the lock, lease-renewal interval and auth-revalidation interval per C7,
So that mutating commands and commits run with a single active writer per tenant/folder/workspace scope and lock contention never produces mixed writes or overlapping commits.

**Acceptance Criteria:**

**Given** a folder in `ready` state with a prepared workspace, an actor with `workspace:lock` permission, and no current lock holder
**When** `LockWorkspace` is submitted with `Idempotency-Key`, `X-Hexalith-Task-Id`, requested lease seconds (within policy bounds), and `X-Correlation-Id`
**Then** `FolderAggregate.Handle(LockWorkspace, FolderState, CommandEnvelope)` returns `DomainResult.Success` with `WorkspaceLocked` event capturing `taskId`, `leaseUntilUtc = envelope.Timestamp + leaseSeconds`, `actor`, `correlationId`
**And** folder transitions `ready → locked`; lock state, owner, age, expiry, and retry eligibility are exposed via the workspace-status query (Story 4.7)
**And** auth-revalidation is scheduled per the C7 two-number contract; the lease-renewal interval and auth-revalidation interval default per architecture and are tunable per tenant

**Given** another task already holds the lock
**When** a competing `LockWorkspace` is submitted by a different task
**Then** the response returns `workspace_locked` (CLI exit 67; MCP failure-kind `workspace_locked`) with safe metadata: lock state, lock owner taskId (if authorized), lock age, retry-eligibility window
**And** the denial is deterministic across REST/CLI/MCP/SDK per the C13 parity oracle
**And** an Idempotency-Key replay of the original lock with equivalent payload returns the same lock result; lock is reentrant for the same taskId; conflicting attempts return deterministic lock-conflict error

**Given** a tenant-access revocation arrives mid-task per AR-AUTHZ-04
**When** auth-revalidation runs on the held lock within the freshness budget (C7)
**Then** the lock state visibly transitions to `inaccessible` per C6; subsequent mutating commands from the original task receive `tenant_access_denied`
**And** the revalidation behavior is asserted by an integration test simulating Tenants disabling the tenant mid-task

### Story 4.4: Inspect lock state and release the workspace lock

As an authorized actor,
I want to inspect lock metadata via `GET /api/v1/folders/{folderId}/workspace/lock` (FR26) and release the lock via `ReleaseWorkspaceLock` (FR29) when ownership and policy allow,
So that operators and tasks can answer "who owns this lock, when does it expire, can I acquire it?" and clean release returns the folder to `ready`.

**Acceptance Criteria:**

**Given** an actor with `workspace:read-status` on a folder
**When** `GET /api/v1/folders/{folderId}/workspace/lock` is called
**Then** the response returns lock state (`held`, `released`, `expired`, `stale`, `abandoned`, `interrupted`), owner taskId (only if the caller is the owner or has `workspace:read-lock-owner` privilege), lease-until timestamp, lock age, and retry-eligibility window — or a 404-shaped response if no lock exists, with safe error shape preventing existence enumeration
**And** the C6-defined lock states (`active`, `expired`, `stale`, `abandoned`, `interrupted`, `released`) are surfaced through observable transitions (FR28); each transition emits a metadata-only audit record

**Given** the lock holder requests release
**When** `ReleaseWorkspaceLock` is submitted with the matching `X-Hexalith-Task-Id` and an `Idempotency-Key`
**Then** if no mutations have been applied (no `FileMutated` events since lock), `FolderAggregate` produces `WorkspaceLockReleased`; folder transitions `locked → ready`
**And** if mutations have been applied (folder is in `changes_staged`), release is rejected with `state_transition_invalid` — the actor must commit (Story 4.10) or accept that lock-lease expiry will move the folder to `dirty`
**And** a non-holder attempting release receives `tenant_access_denied` with safe error shape; lock ownership is not leaked

### Story 4.5: Add, change, and remove files with workspace-root confinement and path policy enforcement

As a developer or AI agent holding the workspace lock,
I want to add, change, and remove files via `AddFile` / `ChangeFile` / `RemoveFile` (REST `PutFileInline` ≤256KB or `PutFileStream` multipart per D-9), with workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding/Unicode normalization, and case-collision handling enforced before any provider write,
So that file operations are deterministic, secure, and bounded — never escaping the workspace, never duplicating events on retry, and never leaking file contents into events or audit.

**Acceptance Criteria:**

**Given** a folder in `locked` or `changes_staged` state with a held lock and an actor whose taskId matches the lock owner
**When** `AddFile` is submitted with workspace-root-relative path, content (inline JSON for ≤256KB or multipart octet-stream for larger), `Idempotency-Key`, `X-Hexalith-Task-Id`, and `X-Correlation-Id`
**Then** `FolderAggregate.Handle(AddFile, FolderState, CommandEnvelope)` validates the path: workspace-root confinement (no escape via `../`, absolute paths, or mixed separators); canonical NFC-normalized Unicode form; traversal rejection (per cross-cutting concern #7); symlink policy (rejected by default); binary/large-file policy per architecture decisions; reserved-name handling; case-collision detection
**And** path-validation failure returns `path_policy_denied` (CLI exit 69; MCP failure-kind `validation_error`) with safe error shape — never echoing the offending path to error responses if it could leak intent
**And** on success, the aggregate emits `FileMutated` event with metadata only: path (sensitivity-classified), `contentHash`, `byteLength`, `mediaType`, `operationId`, `taskId`, `correlationId`, `actor`, `timestamp` — never file contents
**And** folder transitions `locked → changes_staged` (first mutation) or `changes_staged → changes_staged` (subsequent); lease is renewed per C7

**Given** an actor without the lock attempts file mutation
**When** any `AddFile` / `ChangeFile` / `RemoveFile` is submitted
**Then** the request is rejected with `workspace_locked` if held by another task, or `state_transition_invalid` if folder state forbids mutation (e.g., `archived`, `failed`, `inaccessible`)
**And** an Idempotency-Key replay of the same logical mutation returns the same result; same key + different payload returns `idempotency_conflict`
**And** path-security tests covering traversal (`../`, `..\\`, encoded `%2e%2e`), absolute paths, mixed separators, encoded traversal, reserved names (Windows reserved like `CON`, `PRN`, `NUL`; Unix special), Unicode normalization (NFC vs NFD), symlinks, and case-sensitivity collisions all return `path_policy_denied` and never reach the provider

**Given** the file-content transport is bimodal (D-9)
**When** the inline endpoint receives a payload >256KB
**Then** the server returns `413 Payload Too Large` with `X-Hexalith-Retry-As: stream` header; the SDK's `UploadFileAsync` convenience helper auto-retries via `PutFileStreamAsync`
**And** sentinel-redaction tests confirm file contents never appear in events, projections, logs, traces, metrics labels, audit records, or error responses; the corpus iterates over content-hash and byte-length references only
**And** changes are persisted to the working copy by `WorkspaceWorkflows.WorkingCopyManager` only after the aggregate event is persisted; the working copy never holds authoritative state

### Story 4.6: Query file context (tree, metadata, search, glob, bounded range reads) with policy boundaries

As a developer or AI agent (whether or not holding the lock),
I want to request file context via tree, metadata, search, glob, and bounded range reads with policy boundaries (permitted paths, excluded paths, binary handling, range/result limits, secret-safe responses) and tenant + folder ACL + path policy authorization running before any query execution,
So that AI agents can ground responses in real file context without unbounded workspace scans, secret leakage, or cross-tenant exposure.

**Acceptance Criteria:**

**Given** an actor with `folder:read-context` permission and a workspace in any non-`requested`/`preparing`/`inaccessible` state
**When** `GET /api/v1/folders/{folderId}/tree` / `/files/{path}/metadata` / `/search` / `/glob` / `/files/{path}/range?offset={o}&length={l}` is called with the bounded MVP input limits from C4
**Then** `AuthorizationOrder` runs per cross-cutting concern #18: JWT validation → tenant claim validation → local tenant-access projection check (fail-closed-on-stale) → folder ACL check → path policy check (include/exclude/binary/large-file/range/result-limit) — and only THEN executes the query
**And** the response respects the C4 limits encoded in the OpenAPI `maxItems`, `maxLength`, `maxBytes`, `maxResultCount`; truncated responses set `truncated: true` and `truncationReason`
**And** the response is `eventually-consistent` per Story 1.5 read-consistency; `X-Hexalith-Freshness` header reports projection age; query p95 < 2 seconds for bounded MVP inputs (NFR23)

**Given** sensitive content patterns
**When** the result-shaping pipeline runs
**Then** sentinel-secret tests over `audit-leakage-corpus.json` iterate the search and glob results and the partial-read responses; CI fails on any sentinel match
**And** binary files are handled per policy (excluded by default; opted-in via explicit query parameter only when ACL permits)
**And** denied context queries (insufficient ACL, excluded path, range exceeded) produce metadata-only audit evidence with `actor`, `tenant`, `folderId`, `query type`, `policy reason`, `correlationId`, `timestamp`, and safe error category — never echoing the disallowed path content
**And** a cross-tenant negative test confirms tenant B cannot enumerate or read files in tenant A's folder; denial happens before any query executes

### Story 4.7: Inspect workspace, lock, dirty state, last commit, failed operation, and projection currency

As any authorized actor (developer, operator, audit reviewer),
I want to inspect workspace status via `GET /api/v1/folders/{folderId}/workspace/status` returning canonical state (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`, plus lifecycle states per C6) plus lock metadata, dirty-state evidence, last commit reference, last failed operation, and projection currency (FR31 / FR45 / FR52),
So that callers and operators always have a single trustworthy answer to "what is this workspace doing right now?"

**Acceptance Criteria:**

**Given** an actor with `workspace:read-status` permission
**When** `GET /api/v1/folders/{folderId}/workspace/status` is called
**Then** the response returns the current canonical workspace state from the C6 catalog (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`) with operator-disposition label per F-4 (`auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`)
**And** the response includes lock metadata (state, owner if authorized, age, expiry, retry eligibility), dirty-state indicator + changed-path metadata count if applicable, last commit reference (commit SHA + timestamp + author identity classified per S-6), last failed operation (category + reason + retryability), and projection currency (`X-Hexalith-Freshness`, projection lag if degraded)
**And** status query p95 < 500 ms for bounded MVP inputs per NFR22

**Given** projections are stale beyond the C2 freshness target
**When** the status query is executed
**Then** the response sets `X-Hexalith-Freshness` to a value indicating staleness; operator-disposition label may be `degraded-but-serving`
**And** `accepted-but-not-yet-projected` commands are distinguished from projected state; the response indicates whether a recently-submitted command's effect has been projected
**And** sentinel-redaction tests confirm no provider tokens, file contents, or secrets leak into the status response

### Story 4.8: Surface workspace cleanup status without repair automation

As an operator,
I want to inspect workspace cleanup status (FR30) for completed, failed, interrupted, or abandoned task lifecycles via `GET /api/v1/folders/{folderId}/workspace/cleanup`,
So that I have visibility into whether a workspace's working copy has been cleaned, is pending cleanup, or has cleanup-failure evidence — without expecting MVP repair automation.

**Acceptance Criteria:**

**Given** a folder where a task has completed, failed, been interrupted, or abandoned
**When** `GET /api/v1/folders/{folderId}/workspace/cleanup` is called by an operator with `workspace:read-cleanup` permission
**Then** the response returns the cleanup state per task: `cleaned`, `cleanup_pending`, `cleanup_failed`, `cleanup_skipped`, or `not_applicable`
**And** for `cleanup_failed`, the response includes a stable reason code, retryability, last-attempt timestamp, and correlation ID — never any working-copy filesystem paths beyond classified metadata
**And** cleanup failures appear as operational signals per NFR52; observable through status, reason code, retryability, timestamp, and correlation ID

**Given** the MVP non-goal "no repair automation"
**When** an operator inspects cleanup state
**Then** no mutation path is offered to retry cleanup, discard, rebuild, or release stale locks; the response is read-only diagnostic
**And** subsequent post-MVP repair workflows are explicitly out of scope for this story; an architecture note in the response documentation cross-references the post-MVP roadmap
**And** the cleanup workflow `WorkspaceCleanupWorkflow` in `Hexalith.Folders.Workers` runs deterministically on completion/failure events; its outcome populates this projection but does not retry on its own

### Story 4.9: Propagate idempotency keys, correlation, task IDs end-to-end with two-tier TTL and replay semantics

As any caller,
I want every mutating command on the canonical lifecycle (PrepareWorkspace, LockWorkspace, ReleaseWorkspaceLock, AddFile, ChangeFile, RemoveFile, CommitWorkspace) to require an `Idempotency-Key`, propagate `X-Correlation-Id` and `X-Hexalith-Task-Id` end-to-end (REST → SDK → EventStore envelope → projection → audit), and respect two-tier idempotency TTL (`mutation = 24h`; `commit = retention-period(C3)`) with deterministic replay semantics,
So that retries on transient failures, network glitches, or unknown provider outcomes never duplicate domain events, provider writes, file changes, repositories, or commits.

**Acceptance Criteria:**

**Given** any mutating command on the canonical lifecycle
**When** the command is submitted without an `Idempotency-Key`
**Then** the request is rejected at the middleware layer with `validation_error` (CLI exit 69; MCP failure-kind `validation_error`); no aggregate handler is invoked
**And** when submitted with an `Idempotency-Key` (≤128 chars, opaque), the server canonicalizes the payload using the NSwag-generated `ComputeIdempotencyHash()` from Story 1.7 over the lexicographically ordered fields declared in `x-hexalith-idempotency-equivalence`
**And** the canonicalized hash + key combination is stored in `IdempotencyRecordStore` (Dapr state) with TTL per D-7: `mutation = 24h` for prepare/lock/file/cleanup, `commit = retention-period(C3)` for commit operations

**Given** an Idempotency-Key replay
**When** the same key is submitted with equivalent payload (same hash)
**Then** the original logical result is returned without re-invoking the aggregate handler or the provider
**And** when the same key is submitted with a different payload (different hash), the response is `idempotency_conflict` (CLI exit 68; MCP failure-kind `idempotency_conflict`)
**And** correlation and task IDs propagate via headers (`X-Correlation-Id`, `X-Hexalith-Task-Id`) and through the EventStore envelope's `correlationId` and `causationId`; an integration test asserts the chain end-to-end through REST → SDK → EventStore → projection → audit projection

**Given** the encoding-equivalence corpus from Story 1.3
**When** the idempotency hash is computed for variant inputs
**Then** NFC/NFD/NFKC/NFKD/zero-width-joiner/ULID-case variants of the same logical key all produce the same hash; the encoding-equivalence CI gate from Story 1.9 enforces this
**And** an isolation test confirms that the same Idempotency-Key submitted by tenant A and tenant B produce independent records (cache key is `{tenantId}:idempotency:{key}` per C10 cache-key tenant-prefix invariant)

### Story 4.10: Commit workspace changes with task/correlation metadata and unknown-outcome reconciliation

As an authorized actor with the workspace lock and `changes_staged`,
I want to commit workspace changes via `CommitWorkspace` with task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata attached, exposing the resulting commit SHA + timestamps and entering `committed` state on success — entering `unknown_provider_outcome` on ambiguous provider response (no silent retry) and `failed` on categorized failure,
So that the canonical task lifecycle terminates in observable state with full evidence for audit reconstruction.

**Acceptance Criteria:**

**Given** a folder in `changes_staged` with the lock held by the calling task
**When** `CommitWorkspace` is submitted with `Idempotency-Key`, `X-Hexalith-Task-Id`, `X-Correlation-Id`, branch/ref (validated against policy from Story 3.7), commit message, and author identity
**Then** `FolderAggregate.Handle(CommitWorkspace, FolderState, CommandEnvelope)` returns `DomainResult.Success` with `CommitRequested` event including all metadata; folder remains in `changes_staged` while the commit is in flight
**And** `CommitWorkflow` in `Hexalith.Folders.Workers` reacts; the worker calls the provider's `CommitAsync` via the provider port (Octokit for GitHub, typed HttpClient for Forgejo); on success, the worker submits `MarkCommitSucceeded` producing `CommitSucceeded` event with commit SHA, provider reference, changed-path metadata; folder transitions `changes_staged → committed`
**And** the lease release is scheduled; folder eventually transitions `committed → ready` via `WorkspaceLockReleased` per C6

**Given** a known provider failure (timeout / 4xx / 5xx / branch-protection / etc.)
**When** the worker classifies the failure
**Then** `MarkCommitFailed` is submitted; folder transitions `changes_staged → failed` with categorized reason; audit record captures the failure category, retryability, and provider reference
**And** on unknown outcome (provider call did not confirm), `MarkProviderOutcomeUnknown` is submitted; folder transitions `changes_staged → unknown_provider_outcome`; reconciler is scheduled per AR-PROVIDER-05 — never silent retry

**Given** an idempotent replay of the same commit
**When** equivalent `CommitWorkspace` is submitted with the same key
**Then** if the original commit succeeded, the same commit SHA is returned
**And** if the original commit is in `unknown_provider_outcome`, the reconciler runs `RepositoryReconciler.GetProviderStatusAsync` to determine whether the commit landed upstream; on confirmation, the folder transitions to `committed` (replay returns success) or `failed` (replay returns categorized failure)
**And** the commit-TTL idempotency record persists for `retention-period(C3)` per D-7 so reconciliation queries against historical commits remain valid

### Story 4.11: Surface canonical error taxonomy, workspace states, and operational evidence after any failure

As any caller (REST / SDK / CLI / MCP),
I want a canonical error taxonomy across the lifecycle (validation, authentication, tenant denial, folder ACL denial, credential, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, transient infrastructure) with the canonical workspace/task states (`ready` / `locked` / `dirty` / `committed` / `failed` / `inaccessible`) per FR45,
So that final state, retry eligibility, and operational evidence are explainable after any workspace preparation, lock, file operation, commit, provider, or read-model failure (FR46).

**Acceptance Criteria:**

**Given** the canonical error catalog from the architecture (S-2 through provider failure taxonomy)
**When** any lifecycle command produces a failure
**Then** the response uses RFC 9457 `application/problem+json` with required fields: `category` (one of the canonical categories per FR44), `code` (stable string constant), `message`, `correlationId`, `retryable` (boolean), `clientAction` (one of `wait_and_retry`, `change_input`, `request_authorization`, `escalate`), `details`
**And** the canonical category set covers FR44: `validation_error`, `authentication_failure`, `tenant_access_denied`, `folder_acl_denied`, `cross_tenant_access_denied`, `credential_failure`, `provider_unavailable`, `unsupported_capability`, `repository_conflict`, `branch_ref_conflict`, `workspace_locked`, `stale_workspace`, `path_policy_denied`, `commit_failure`, `read_model_unavailable`, `idempotency_conflict`, `transient_infrastructure_failure`, `provider_outcome_unknown`, `reconciliation_required`, `state_transition_invalid`, `not_found`, `redacted`

**Given** a failure occurred during workspace preparation, lock, file operation, commit, provider, or read-model access
**When** the actor inspects status (Story 4.7) or audit (Story 4.12)
**Then** the response explains the final state per the C6 matrix, retry eligibility (boolean), retry-after hint when known, the causation chain (event sequence that led to the state), correlation ID, and the categorized reason
**And** `unknown_provider_outcome` is distinguished from `failed`: the former is "we don't know if it landed" with a reconciliation path; the latter is "we know it did not land" with a reason code
**And** an integration test asserts the same logical failure produces identical canonical category, code, retryable flag, and clientAction across REST / SDK responses (CLI/MCP parity is asserted in Epic 5)

### Story 4.12: Emit metadata-only audit and integrate OpenTelemetry observability with structured logging

As an operator or audit reviewer,
I want every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation to emit a metadata-only audit record with `tenantId`, `actor`, `taskId`, `operationId`, `correlationId`, `folderId`, `provider`, `repositoryBindingId`, `timestamp`, `result`, `duration`, `state transition`, sanitized error category — and OpenTelemetry traces (correlation/causation/task IDs as span attributes) + structured logs (Microsoft.Extensions.Logging templates) per AR-AUDIT-04..06,
So that incident reconstruction works from metadata alone without any file contents or secrets leaking into events, logs, traces, metrics, projections, audit records, diagnostics, or console responses (FR55 write-side).

**Acceptance Criteria:**

**Given** any aggregate or worker emits an event or makes a provider call
**When** the audit pipeline runs
**Then** an entry is written to `AuditProjection` (D-10) under `Hexalith.Folders.Server` projection endpoints with the metadata-only field set above; sentinel-redaction tests over `audit-leakage-corpus.json` find no leak
**And** sensitive metadata (paths, branch names, commit messages, repository names) is classified per S-6 / C9 (default `tenant-sensitive`); per-tenant override allows `confidential` (hashed at write time)
**And** audit records cover allowed and denied operations alike (FR53); denied operations include the policy reason without enumerating disallowed resources

**Given** OpenTelemetry SDK exports OTLP per AR-AUDIT-05
**When** any lifecycle operation runs
**Then** spans capture `tenantId`, `correlationId`, `causationId`, `taskId` as attributes; metrics include per-tenant operation counters and projection-lag gauges; structured logs use named parameters with `tenantId`, `correlationId`, `causationId`, `taskId`, `aggregateId`, `eventTypeName`
**And** sentinel tests confirm no log value matches a forbidden pattern (file contents, secrets, provider tokens, raw credential references)
**And** the redaction-formatter applies the S-6 classifier to all log/trace/audit outputs; classified values render as the configured form (visible / hashed / truncated / redacted)

### Story 4.13: Validate canonical lifecycle through replay, projection, sentinel, path-security, encoding, isolation, and capacity tests

As a maintainer,
I want the canonical lifecycle covered by replay tests, projection determinism tests, sentinel-redaction tests, path-security tests, encoding-equivalence tests, cross-tenant isolation tests, and a Phase-9-calibratable capacity test harness in `tests/load/`,
So that the lifecycle's invariants — purity, replay determinism, secret-safety, path security, encoding stability, isolation, and bounded capacity — are mechanically asserted on every PR.

**Acceptance Criteria:**

**Given** the FolderAggregate, projections, and worker pipeline exist
**When** the test suite runs in CI
**Then** replay tests (AR-TEST-02) cover every event family in the architecture event vocabulary; replaying any ordered event stream into an empty `FolderState` produces equivalent state; tombstone tests confirm archived/terminated folders reject subsequent commands
**And** projection tests (AR-TEST-03) confirm `WorkspaceStatusProjection`, `FolderListProjection`, `FolderAclProjection`, `ProviderReadinessProjection`, and `AuditProjection` are deterministic from the same ordered event stream and idempotent on duplicate event delivery
**And** sentinel tests (AR-TEST-06) iterate `audit-leakage-corpus.json` over every output pipeline (events, projections, logs, traces, metrics labels, audit records, console payloads, provider diagnostics, error responses); CI fails on any match
**And** path-security tests (AR-TEST-07) cover traversal, absolute paths, mixed separators, encoded traversal, reserved names, Unicode normalization, symlinks, and case sensitivity; all produce `path_policy_denied` and never reach the provider
**And** encoding-equivalence tests (AR-TEST-08) iterate `idempotency-encoding-corpus.json`; all variants produce the same hash

**Given** capacity targets are deferred to Phase 9 (C1)
**When** `tests/load/Hexalith.Folders.LoadTests.csproj` is set up
**Then** NBomber scenarios cover workspace prepare → lock → mutate → commit at multiple concurrency profiles; the harness is parameterized so Phase 9 calibration only needs to set numeric targets per `docs/exit-criteria/c1-capacity.md`
**And** cross-tenant isolation tests (AR-TEST-09) cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, background jobs, provider callbacks, audit records, and context-query results — asserting zero cross-tenant leak in any output channel
**And** a provider rate-limit chaos test injects synthetic 429 storms (AR-WORKER-03 / I-8); CI fails if the reconciliation queue grows unboundedly or if C12 drift signal does not fire within SLO

## Epic 5: Cross-Surface Adapters — CLI, MCP, SDK Parity

The SDK-as-canonical reframe collapses *transport* parity to SDK-vs-REST; *behavioral* parity (pre-SDK errors, post-SDK error projection, side-channel parameter sourcing) remains per-adapter. This epic implements `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp` as adapters wrapping `Hexalith.Folders.Client`, ships SDK convenience helpers, and proves cross-surface equivalence through the C13 parity oracle and an end-to-end parity scenario.

### Story 5.1: Ship SDK convenience helpers, samples, and quickstart

As an SDK consumer (developer integrating Hexalith.Folders into a chatbot or automation),
I want `Hexalith.Folders.Client` to provide a hand-written `UploadFileAsync(stream)` convenience helper that picks `PutFileInlineAsync` or `PutFileStreamAsync` based on stream length, plus retry/idempotency helpers, typed errors, async patterns, and a working sample at `samples/Hexalith.Folders.Sample/`,
So that single-call ergonomics for the bimodal D-9 file transport are preserved without forcing every consumer to handle the size-threshold + 413-retry logic.

**Acceptance Criteria:**

**Given** the NSwag-generated SDK from Story 1.7 with typed `PutFileInlineAsync` and `PutFileStreamAsync` methods
**When** `Hexalith.Folders.Client.Convenience.UploadFileAsync(Stream content, UploadFileOptions options, CancellationToken)` is called
**Then** the helper inspects `content.Length` (when seekable) or buffers up to 256KB to determine inline-vs-stream eligibility; ≤256KB calls `PutFileInlineAsync` with the buffered bytes; >256KB calls `PutFileStreamAsync` with the streamed content
**And** on `413 Payload Too Large` from `PutFileInlineAsync` (e.g., when stream length is unknown and the buffer exceeds the threshold), the helper auto-retries via `PutFileStreamAsync` using the `X-Hexalith-Retry-As: stream` response header signal
**And** correlation and task IDs are propagated through both code paths; idempotency-key generation is NEVER automatic — the caller must supply one or register an `IIdempotencyKeyProvider` per the Adapter Parity Contract

**Given** developers want to integrate
**When** `samples/Hexalith.Folders.Sample/` is built
**Then** the sample demonstrates the canonical task lifecycle end-to-end: configure provider readiness → create folder → bind repository → prepare workspace → lock → write files via `UploadFileAsync` → query context → commit → inspect status — using the SDK against a local Aspire AppHost
**And** typed exceptions (`HexalithFoldersException` with canonical `category`, `code`, `correlationId`, `retryable`, `clientAction`, `details` per A-8) are demonstrated in the sample's catch blocks
**And** an SDK quickstart at `docs/sdk/quickstart.md` walks a new consumer from package install to first successful commit, referencing the sample

### Story 5.2: Implement CLI commands with full behavioral-parity rules

As an operator,
I want `Hexalith.Folders.Cli` built on System.CommandLine 2.x wrapping `Hexalith.Folders.Client`, with hierarchical commands mirroring REST capability groups, JSON + human output formatters (the human formatter showing operator-disposition labels per F-4), credential precedence per the Adapter Parity Contract, and the canonical exit-code mapping (0/64/65/66/67/68/69/70/71/72/73/74/75/1),
So that operators can drive the canonical lifecycle from the terminal with deterministic exit codes for shell pipelines and semantically equivalent behavior to REST/SDK.

**Acceptance Criteria:**

**Given** the SDK from Stories 1.7 and 5.1 and the canonical exit-code mapping from Story 1.5
**When** `Hexalith.Folders.Cli.Program.Main` runs
**Then** the CLI exposes hierarchical commands rooted at `hexalith-folders` with subcommands per capability group: `provider readiness validate`, `folder create|list|get|archive`, `folder access grant|revoke`, `workspace prepare|lock|release|status|cleanup`, `file add|change|remove`, `commit create`, `context tree|search|glob|read`, `audit query`
**And** every mutating command requires `--idempotency-key <key>` OR explicit `--allow-auto-key` opt-in (CLI generates a ULID and prints it to stderr for retry traceability); missing → exit code 64 (`USAGE_ERROR`)
**And** every task-scoped command requires `--task-id <id>`; missing → exit code 64
**And** `--correlation-id <id>` overrides the auto-generated per-invocation correlation id; sub-calls within the invocation propagate the same id

**Given** credentials must be resolved per the Adapter Parity Contract precedence
**When** the CLI authenticates
**Then** credential precedence is `HEXALITH_TOKEN` env var → `~/.hexalith/credentials.json` (per-tenant section) → `--token <jwt>` flag; missing → exit code 65 (`CREDENTIAL_MISSING`)
**And** the CLI's `--output` flag selects `json` (machine-readable, stable shape) or `human` (compact rendering with operator-disposition labels)
**And** post-SDK errors map to exit codes per the canonical table (Story 1.5): `0=success`, `64=client_configuration_error`, `65=credential_missing`, `66=tenant_access_denied`, `67=workspace_locked`, `68=idempotency_conflict`, `69=validation_error`, `70=provider_failure_known`, `71=provider_outcome_unknown`, `72=reconciliation_required`, `73=not_found`, `74=state_transition_invalid`, `75=redacted`, `1=internal_error`
**And** `correlationId` is always emitted to stderr alongside any non-zero exit so operators can correlate with audit records

### Story 5.3: Implement MCP server tools, resources, and failure-kind set

As an AI tool integration,
I want `Hexalith.Folders.Mcp` built on ModelContextProtocol C# SDK 1.3.0 wrapping `Hexalith.Folders.Client`, exposing one tool per canonical command (PrepareWorkspaceTool, LockWorkspaceTool, WriteFileTool, CommitWorkspaceTool, ReadFileTool, SearchFolderTool, GetWorkspaceStatusTool) and resources (FolderTreeResource, AuditTrailResource), with the canonical MCP failure-kind set per the Adapter Parity Contract,
So that AI agents discover and invoke the canonical lifecycle through MCP semantics with identical authorization, error-category, and idempotency behavior to REST/CLI/SDK.

**Acceptance Criteria:**

**Given** the SDK from Stories 1.7 and 5.1 and the MCP failure-kind catalog from Story 1.5
**When** `Hexalith.Folders.Mcp.Program.Main` runs
**Then** the server publishes a manifest at `Hexalith.Folders.Mcp/Manifest/server-manifest.json` declaring every tool with JSON Schema input fields including `idempotencyKey` (required for mutating tools), `correlationId` (optional; server generates ULID if omitted, always echoed in tool result), `taskId` (required for task-scoped tools)
**And** every tool wraps a single `Hexalith.Folders.Client` SDK method; the MCP layer adds no business logic
**And** MCP server config supports `auth.token` or `auth.tokenFile` per the Adapter Parity Contract; missing → server-startup error or per-tool MCP failure `kind = "credential_missing"`

**Given** any tool invocation fails
**When** the MCP layer projects the failure
**Then** the result includes `kind ∈ {usage_error, credential_missing, tenant_access_denied, workspace_locked, idempotency_conflict, validation_error, provider_failure_known, provider_outcome_unknown, reconciliation_required, not_found, state_transition_invalid, redacted, internal_error}` plus `correlationId`, `code`, `retryable`, `clientAction`
**And** the `kind` set is one-to-one with canonical error categories — never collapse multiple categories into a single `kind` for MCP convenience
**And** `WriteFileTool` auto-picks the bimodal transport equivalent to the SDK convenience helper (Story 5.1) without exposing the size threshold to MCP callers
**And** `FolderTreeResource` and `AuditTrailResource` honor the C8 read-consistency declarations and emit `X-Hexalith-Freshness` equivalents in the resource metadata

### Story 5.4: Consume parity oracle in *.Cli.Tests and *.Mcp.Tests for behavioral-parity columns

As a maintainer,
I want `tests/Hexalith.Folders.Cli.Tests/` and `tests/Hexalith.Folders.Mcp.Tests/` to consume `tests/fixtures/parity-contract.yaml` (Story 1.8) as xUnit theory data, asserting the behavioral-parity columns (`pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `cli_exit_code`, `mcp_failure_kind`) per operation,
So that adding a new operation to the Contract Spine without populating the per-adapter behavioral parity row mechanically fails the build (per AR-PARITY-01).

**Acceptance Criteria:**

**Given** the parity oracle and schema from Story 1.8
**When** `tests/Hexalith.Folders.Cli.Tests/ParityOracleTests.cs` runs
**Then** an xUnit theory consumes `parity-contract.yaml` row-by-row; for each row, an isolated test invokes the corresponding CLI command and asserts: pre-SDK error class matches `pre_sdk_error_class`; idempotency-key sourcing follows `idempotency_key_sourcing`; correlation-id sourcing follows `correlation_id_sourcing`; CLI exit code on the documented failure path matches `cli_exit_code`
**And** an equivalent `tests/Hexalith.Folders.Mcp.Tests/ParityOracleTests.cs` asserts MCP failure-kind matches `mcp_failure_kind` and the same behavioral dimensions

**Given** the schema-validation gate from Story 1.9
**When** the parity-oracle generator emits a row missing a required behavioral column
**Then** generation fails before tests consume the oracle; CI surfaces the offending row
**And** Contract Spine adding an operation without a parity-oracle row → CI fails per Story 1.8/1.9 gates
**And** Contract Spine removing an operation without a deprecation entry in `previous-spine.yaml` → CI fails (symmetric drift gate from Story 1.9)

### Story 5.5: End-to-end cross-surface parity scenario and cross-adapter invariants

As a stakeholder validating the MVP "one canonical workflow contract" claim,
I want `tests/Hexalith.Folders.IntegrationTests/EndToEnd/CrossSurfaceParityScenario.cs` running the canonical task lifecycle (provider readiness → create folder → bind repo → prepare → lock → write files → query context → commit → inspect status → release) through REST, CLI, MCP, and SDK in a single test run, asserting cross-adapter invariants,
So that the four-surface promise is proven by a single integrated test that fails loudly if any surface drifts in error category, idempotency replay, correlation propagation, or terminal state.

**Acceptance Criteria:**

**Given** the Aspire AppHost from Story 2.1 with EventStore + Tenants + Folders.Server + Workers + Keycloak running
**When** the end-to-end scenario test runs
**Then** the canonical lifecycle executes once through REST (HttpClient), once through SDK (`Hexalith.Folders.Client`), once through CLI (process invocation), and once through MCP (tool invocation); each path uses the same tenant, the same folder/binding, and equivalent operation identity
**And** for the same logical operation across surfaces, the canonical category, code, retryable flag, and clientAction returned are **identical** (transport-parity invariant)
**And** idempotency replay semantics are identical: same key + equivalent payload returns the same logical result; same key + different payload returns `idempotency_conflict` with consistent error category
**And** correlation-id, when caller-supplied, is **echoed unchanged** through all four surfaces

**Given** the cross-adapter invariants from the architecture's §"Adapter Parity Contract"
**When** invariant assertions run
**Then** pre-SDK error classes (configuration, credential-missing) and post-SDK error classes are **mutually exclusive** — an assertion confirms no operation can return both
**And** idempotency-key sourcing differs per adapter as documented (SDK = caller/DI never auto; CLI = flag or `--allow-auto-key`; MCP = required tool-input field) — but the **resulting record key + canonical hash** is identical across surfaces for the same logical payload
**And** the audit projection (Story 4.12) shows one logical operation per (tenant, taskId, operationId, correlationId) regardless of the surface that submitted it; an assertion confirms no double-emission per surface
**And** the test runs as part of the CI parity-tests workflow and is also runnable locally via a single `dotnet test` invocation

## Epic 6: Read-Only Operations Console & Audit Reviewer Flows

A WCAG 2.2 AA Blazor Server + Microsoft Fluent UI read-only console reading exclusively from projections (no direct EventStore aggregate access). Operator-disposition labels (`auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`) are the primary visual; technical state names appear as secondary metadata. Sensitive metadata is rendered with a visible lock-icon affordance — never silent truncation. The incident-mode last-resort read path at `/_admin/incident-stream` is available with three UX guardrails when projections degrade. Performance budget separates console flows from end-user budgets and includes perceived-wait UX (skeleton at 400ms, cancel at 2s).

### Story 6.1: Audit and operation-timeline query endpoints

As an audit reviewer or operator,
I want server-side query endpoints `GET /api/v1/audit-events` (with filters: tenant, folderId, taskId, correlationId, time range, operation type, result) and `GET /api/v1/folders/{folderId}/timeline` returning operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events,
So that I can reconstruct incidents from metadata alone (FR53, FR54) and trace the causation chain that led to a specific outcome (FR56) without ever needing direct EventStore aggregate access.

**Acceptance Criteria:**

**Given** the `AuditProjection` from Story 4.12 is populated
**When** `GET /api/v1/audit-events` is called with filter combinations and the actor has `audit:read` permission for the requested tenant
**Then** the response returns paginated metadata-only audit records with `tenantId`, `actor`, `taskId`, `operationId`, `correlationId`, `folderId`, `provider`, `repositoryBindingId`, `timestamp`, `result`, `duration`, `eventTypeName`, `stateTransition`, `errorCategory` — never any file contents, secrets, or sensitive content beyond what S-6 classification permits for the requesting actor
**And** the response is `eventually-consistent` per Story 1.5; `X-Hexalith-Freshness` reports projection age
**And** denied + retried + duplicate operations all appear in the result set with their classification (FR53)

**Given** an actor with `folder:read-timeline` permission and a folder with a populated event history
**When** `GET /api/v1/folders/{folderId}/timeline?operationTypes=workspace,file,lock,commit,provider,authorization&since=&until=` is called
**Then** the response returns chronologically ordered operation events with the causation chain (each event links to its `causationId` so the chain is reconstructible)
**And** the same metadata-only fields apply; sensitive paths/branch names/commit messages render per S-6 classification with redaction tier honored
**And** sentinel-redaction tests over `audit-leakage-corpus.json` find no leak in any audit or timeline response
**And** a cross-tenant negative test confirms an actor in tenant B cannot retrieve audit or timeline data for tenant A; denial uses safe error shape

### Story 6.2: Scaffold Blazor Server console with Fluent UI, OIDC auth, and tenant/folder navigation

As a platform engineer,
I want `Hexalith.Folders.UI` running as a Blazor Server application with SignalR live updates, Microsoft Fluent UI Blazor (`Microsoft.FluentUI.AspNetCore.Components`) component library, OIDC authentication against the same identity provider as the backend, and a top-level tenant + folder navigation shell,
So that operators and audit reviewers have a single accessible web surface that consumes only projection endpoints (never direct EventStore aggregate access) and respects the same authorization stack as the API.

**Acceptance Criteria:**

**Given** the Aspire AppHost from Story 2.1 with Folders.Server + Keycloak (or production OIDC provider)
**When** `Hexalith.Folders.UI` starts as `AppId=folders-ui`
**Then** the application authenticates via OIDC using the same `JwtBearer` parameters from Story 1.4 (S-2); login is via the configured provider; sign-out works correctly
**And** the application consumes `Hexalith.Folders.Client` SDK only — no direct EventStore command/query API references; no aggregate-write paths; no service invocation outside projection endpoints (FR36 / AR-UI-07)
**And** Microsoft Fluent UI Blazor components are used throughout; the layout uses semantic HTML headings, visible focus states, and readable table structures (foundations for NFR59–63)

**Given** an authenticated operator with multi-tenant access lands on the home page
**When** they navigate
**Then** the top-level shell shows a tenant selector populated from the actor's tenant claims; once a tenant is selected, a folder selector lists folders the actor has `folder:read-metadata` for
**And** SignalR delivers live status updates to currently-rendered folder/workspace views; updates trigger debounced re-renders (no flicker)
**And** the navigation passes a keyboard-only test: every primary route is reachable via Tab/Shift-Tab/Enter/Esc

### Story 6.3: Render operator-disposition labels as the primary visual with technical state as secondary metadata

As an operator on-call at 3 AM,
I want every workspace/folder/provider state in the console to render its operator-disposition label (`auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`) as the primary visual badge with a distinct shape + glyph (not color alone), and the technical state name (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`, `requested`, `preparing`, `changes_staged`, `unknown_provider_outcome`, `reconciliation_required`) as secondary metadata,
So that I can read "what is broken and what can safely happen next" before I have to translate engineering vocabulary into incident-response cognition.

**Acceptance Criteria:**

**Given** the C6 transition matrix from Story 1.4 declares operator-disposition labels per state
**When** `Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` is built
**Then** the mapper is sourced from (or hand-written and tested against) the C6 enumerated matrix; a unit test asserts that every state in the catalog maps to the documented label and no state is unmapped
**And** the matrix-coverage CI gate from Story 1.9 fails if a new state is added in the architecture without updating `DispositionLabelMapper`

**Given** any view displays workspace/folder/provider state
**When** the disposition badge component renders
**Then** `Hexalith.Folders.UI/Components/OperatorDispositionBadge.razor` renders the label with a distinct shape + glyph + accessible-name attribute; color is supplementary, not primary (NFR61)
**And** `Hexalith.Folders.UI/Components/TechnicalStateMetadata.razor` renders the technical state name in a smaller, secondary position with sufficient contrast (NFR62)
**And** the disposition label appears alongside raw event types in incident-stream views (Story 6.6) so operators do not switch vocabularies mid-incident
**And** a screen-reader announces both label and technical state in a logical order (label first, technical state as secondary)

### Story 6.4: Implement sensitive-metadata redaction affordance with lock-icon — never silent truncation

As an operator diagnosing an incident,
I want any field that has been redacted by the S-6 sensitive-metadata classifier to render with a visible lock icon and explanatory text "Your tenant policy hides this; contact your administrator" — distinguished from unknown or missing fields (FR55 read-side, F-5),
So that I do not waste incident time chasing ghosts when a value was hidden by policy rather than missing in the data.

**Acceptance Criteria:**

**Given** an audit, timeline, or status response contains a redacted field per the per-tenant `confidential` classification override (S-6)
**When** the console renders that field
**Then** `Hexalith.Folders.UI/Components/RedactedField.razor` displays a lock icon + "Your tenant policy hides this; contact your administrator" with an `aria-label` that screen-readers announce as "redacted by tenant policy"
**And** an unknown/missing field renders distinctly (e.g., `—` placeholder + "no data" text) with a different `aria-label` so operators can distinguish "policy hides" from "data absent"
**And** the lock icon affordance also applies to incident-stream views (Story 6.6); redaction rules do not relax under degraded mode

**Given** the architecture's S-6 classifier
**When** the console fetches audit/timeline/status responses
**Then** classified fields arrive pre-redacted from the server (where applicable per the per-tenant policy); the console does not perform redaction client-side and does not have access to the unredacted value
**And** an integration test confirms that a tenant with default `tenant-sensitive` classification sees the value; a tenant with `confidential` override sees the redaction affordance; cross-tenant operator views always see redaction unless the operator has explicit cross-tenant audit permission

### Story 6.5: Build folder detail, workspace status, provider health, and audit trail pages

As an operator,
I want `Folders.razor`, `FolderDetail.razor`, `ProviderHealth.razor`, `AuditTrail.razor`, and `ProviderSupportEvidence.razor` (FR57) pages exposing the read-only diagnostic surface for a tenant — workspace state, lock metadata, dirty state, last commit, last failed operation, provider readiness, audit trail, provider support evidence,
So that I have one consolidated console view answering "what is the state of this tenant's workspaces today" without needing direct API or database access.

**Acceptance Criteria:**

**Given** an authenticated operator with `folder:read-metadata` for the selected tenant
**When** `/folders` (Folders.razor) renders
**Then** the page lists folders with operator-disposition badge (Story 6.3), folder name (S-6 classified), creation timestamp, and bound-repository indicator; pagination follows server conventions
**And** clicking a folder opens `/folders/{folderId}` (FolderDetail.razor) showing canonical workspace state + disposition label, lock metadata + owner taskId (when authorized) + lease expiry + retry-eligibility window, dirty-state indicator + changed-path count, last commit reference (commit SHA + timestamp + author per S-6 classification), last failed operation (category + reason + retryability), and projection currency
**And** the page subscribes to SignalR updates for the selected folder so state changes appear live without manual refresh

**Given** a platform engineer with `platform:read-provider-evidence` permission
**When** `/provider-health` (ProviderHealth.razor) renders
**Then** the page shows per-tenant provider-readiness aggregation from Story 3.8 with disposition badges, last-validation timestamps, and reason codes
**And** `/provider-support-evidence` (ProviderSupportEvidence.razor) shows the latest contract-suite outcomes per provider (GitHub / Forgejo) with pass/fail per capability area (readiness, repo binding, branch handling, file ops, commit, status, failure behavior) and any active drift annotations from the nightly oasdiff job (FR57)

**Given** an audit reviewer
**When** `/folders/{folderId}/audit` (AuditTrail.razor) renders
**Then** the page consumes `GET /api/v1/folders/{folderId}/timeline` and `GET /api/v1/audit-events?folderId=...` with filterable views (operation type, result, time range, taskId, correlationId)
**And** every record renders metadata-only with disposition labels and the redaction affordance from Story 6.4 where applicable
**And** the page provides an export-as-CSV affordance that excludes any field a screen render would have redacted (no policy bypass via export)

### Story 6.6: Implement incident-mode last-resort read path at /_admin/incident-stream with three UX guardrails

As an operator following a runbook link during an incident where projections are degraded,
I want `/_admin/incident-stream` (`IncidentStream.razor`) — an ACL-checked event-stream view available when projections are degraded — with three UX guardrails: a persistent red "DEGRADED MODE" banner showing the last projection checkpoint, operator-disposition labels rendered alongside raw event types, and a one-click "copy correlationId + timestamp window" affordance,
So that sleep-deprived operators do not land on a raw event stream with zero scaffolding (per F-6).

**Acceptance Criteria:**

**Given** an operator with `eventstore:permission=admin` (per F-6 ACL gate)
**When** they navigate to `/_admin/incident-stream`
**Then** the page authorizes via the same layered authorization stack from Story 2.6; non-admin operators are denied with `tenant_access_denied` using safe error shape
**And** the page consumes the latest events directly from the EventStore aggregate stream (still ACL-checked, never bypassing tenant boundaries) when projections are degraded; the data path is the documented last-resort
**And** `Hexalith.Folders.UI/Layout/DegradedModeBanner.razor` renders a persistent red banner across the top: "DEGRADED MODE — events shown may be incomplete or out of order. Last projection checkpoint: HH:MM:SS UTC" — banner is dismissible per session but re-asserts on every navigation

**Given** the page renders raw event types
**When** each event row is shown
**Then** the operator-disposition label (Story 6.3) is rendered alongside the raw `eventTypeName` so operators do not switch vocabularies mid-incident
**And** redacted fields still render with the lock-icon affordance from Story 6.4 — policy redaction does not relax in degraded mode
**And** `Hexalith.Folders.UI/Components/CorrelationCopyButton.razor` provides a one-click "copy correlationId + timestamp window" affordance per row, copying a JSON object to the clipboard for handoff to other engineers

### Story 6.7: Enforce ops-console performance budget and perceived-wait UX

As an operator under incident load,
I want the ops console to honor a separate performance budget — p95 page-load < 1.5s primary diagnostic flows, p99 < 3s, degraded-mode flows up to 5s p95 — with perceived-wait UX: visible skeleton state at 400ms and "still loading… [cancel]" affordance at 2s for any in-flight request (per F-7),
So that the console itself never becomes the new outage perception during real incidents.

**Acceptance Criteria:**

**Given** a primary diagnostic page (Folders, FolderDetail, AuditTrail, ProviderHealth)
**When** the page loads
**Then** for p95 of requests against bounded MVP inputs, page-load latency is < 1.5s; p99 < 3s
**And** any in-flight request that has not returned within 400ms shows a `Hexalith.Folders.UI/Components/SkeletonState.razor` placeholder so the page does not look frozen
**And** any in-flight request that has not returned within 2s shows `Hexalith.Folders.UI/Components/StillLoadingCancel.razor` with a visible "Still loading… [Cancel]" affordance; the cancel action aborts the underlying HTTP request and returns the operator to the previous state without a partial render

**Given** the degraded-mode incident-stream view (Story 6.6)
**When** the page loads
**Then** the degraded-mode performance budget allows up to 5s p95 (acknowledging the cost of last-resort read paths)
**And** the same skeleton + cancel affordances apply
**And** a release-validation evidence path is documented in `docs/exit-criteria/c2-freshness.md` showing how console p95 is measured (browser-based instrumentation + server-side OpenTelemetry trace span correlation)

### Story 6.8: Verify no-mutation enforcement and WCAG 2.2 AA accessibility

As a stakeholder validating MVP non-goals,
I want a release-validation pass on the ops console proving no mutation paths, no credential reveal, no file-content browsing, no file-editing UI, no raw diff display, no hidden repair actions, no unrestricted filesystem browsing — and full WCAG 2.2 AA conformance for primary diagnostic workflows (NFR59–63),
So that the MVP non-goal "read-only operations console" is mechanically enforced and operators with disabilities have first-class access to incident response.

**Acceptance Criteria:**

**Given** the architectural invariant from AR-UI-07
**When** an automated test suite scans the console's Razor components and routes
**Then** a test confirms no Razor route maps to a `POST` / `PUT` / `PATCH` / `DELETE` SDK method or EventStore command API path (negative scope enforcement of FR36)
**And** a test confirms no UI component renders raw credential material, raw token strings, file content beyond classified metadata + content hash, or raw diffs
**And** the console's HttpClient handler is configured to call only `GET` projection endpoints (`/api/v1/...` projection routes); a test attempts to invoke a mutating endpoint via the UI's HttpClient and asserts rejection at the configuration layer

**Given** a WCAG 2.2 AA accessibility audit
**When** primary diagnostic workflows are tested with axe-core (or equivalent automated tool) plus manual screen-reader verification
**Then** every primary workflow (folder list → folder detail → workspace status → audit trail → incident-stream) passes automated axe-core checks
**And** a keyboard-only walkthrough covers every primary diagnostic flow without mouse use; visible focus states present at every focusable element (NFR62)
**And** color contrast meets WCAG 2.2 AA across all status, failure, readiness, and lock indicators; status indicators use shape + glyph + label, never color alone (NFR61)
**And** the console remains readable at 200% browser zoom; tables reflow or scroll appropriately at common operator zoom levels (NFR63)
**And** a release-validation report records the audit results and lands in `docs/exit-criteria/` per the verification expectations (NFR66)

## Epic 7: Production Hardening & Release Readiness

This epic does NOT add user-facing features. It validates the integrated system against production invariants that cannot be proven from local-dev alone: deny-by-default Dapr access control with mTLS, pluggable OIDC, container deployment, capacity targets at scale, retention enforcement, observability exporters, and the full documentation surface — all while pinning the deferred quantitative exit criteria (C1, C2, C3, C5) with measurement evidence.

### Story 7.1: Deploy production Dapr deny-by-default access control with mTLS and negative-test conformance

As a platform operator,
I want the production Dapr access-control YAML to be deny-by-default + mTLS, with app IDs restricted (`folders` may invoke `eventstore` and `tenants`; not `system` admin; pubsub topics declared) and validated by the `dapr-policy-conformance` CI job running `daprd` in a kind cluster with a property-based negative test asserting unauthorized `(sourceAppId, targetAppId, operation)` triples receive `403`,
So that production tenant isolation rests on tested-not-theoretical Dapr policy.

**Acceptance Criteria:**

**Given** the architecture's I-3 production policy specification
**When** the production `accesscontrol.yaml` and mTLS configuration are authored
**Then** `defaultAction: deny` is set; explicit allow rules grant `folders` invoke permission to `eventstore` and `tenants` only (never `system` admin); pubsub topic permissions are declared per topic (`{tenantId}.folders.events`, `system.tenants.events`, `deadletter.folders.events`) — never wildcards
**And** mTLS is enabled across all Dapr sidecar communication; certificate rotation procedure is documented in `docs/runbooks/`
**And** the production policy YAML lives outside the application repo (per ops convention) but a hermetic copy used by tests lives at `tests/policies/dapr-production.accesscontrol.yaml`

**Given** `.github/workflows/policy-conformance.yml`
**When** a PR modifies the policy YAML or its negative tests
**Then** the conformance job spins up `daprd` in a kind cluster with the policy YAML and runs a property-based negative test that generates unauthorized `(sourceAppId, targetAppId, operation)` triples (covering invoke and pubsub) and asserts every triple receives `403`
**And** the job blocks merge on policy YAML changes without corresponding negative test additions in the same PR (CI gate from Story 1.9)
**And** the test surface includes both invoke calls AND pubsub topic publish/subscribe attempts; both must be denied for unauthorized triples

### Story 7.2: Configure pluggable production OIDC and Dapr secret store integration

As a platform operator,
I want the production deployment to use a pluggable OIDC provider (Keycloak, Microsoft Entra ID, Auth0, or any OIDC-compliant provider) configured per the S-2 frozen `JwtBearerOptions` (clock skew 30s, JWKS auto-refresh 10m / refresh 1m, validate issuer + audience + lifetime + signing key), with provider credentials stored only as references in a Dapr secret store,
So that production authentication is vendor-neutral, secrets never appear in events/logs/traces/projections, and credential rotation does not require code changes.

**Acceptance Criteria:**

**Given** the deployment configuration template from Story 1.4
**When** a target environment (Staging or Production) is provisioned
**Then** the OIDC issuer and audience values for that environment are pinned in the deployment configuration; `Hexalith.Folders.Server` reads them via `IConfiguration` binding and applies the S-2 frozen `JwtBearerOptions`
**And** integration with Microsoft Entra ID, Auth0, and self-hosted Keycloak is documented in `docs/auth/oidc-providers.md`; switching between providers requires only configuration changes, not code changes
**And** an integration test against a containerized Keycloak (in CI) and against a containerized Entra ID emulator (where available) validates the same canonical authorization decisions

**Given** provider credential references must never resolve to raw tokens in any persisted artifact
**When** Dapr secret-store integration is configured
**Then** all provider credential references resolve through a Dapr secret-store component (Azure Key Vault / HashiCorp Vault / Kubernetes Secrets / per environment); the resolved token is loaded only at provider-call time inside the provider adapter
**And** sentinel-redaction tests run against staging-like deployment artifacts (logs/traces/metrics/audit) to confirm no token material leaks
**And** secret rotation procedure is documented in `docs/runbooks/secret-rotation.md` with explicit steps for both OIDC signing keys and provider credential references

### Story 7.3: Build container images per service with stable Dapr app IDs and Kubernetes-friendly deployment

As a platform operator,
I want one container image per service (`hexalith-folders-server`, `hexalith-folders-workers`, `hexalith-folders-ui`) with a Dapr sidecar deployed alongside each, stable app IDs preserved across environments (`folders`, `folders-workers`, `folders-ui` per I-4), and Kubernetes-friendly (but not Kubernetes-required) deployment manifests,
So that production deployment can target Kubernetes, container apps, or VM-with-Docker without changing the application or Dapr policy artifacts.

**Acceptance Criteria:**

**Given** the architecture's I-2 + I-4 specifications
**When** Dockerfiles are authored
**Then** each service image is multi-stage built (build → runtime), uses the official `mcr.microsoft.com/dotnet/aspnet:10.0` base image, and runs as a non-root user
**And** images are tagged with semver + git SHA on tagged release; image scanning runs in CI; CVE-failing images block release
**And** Kubernetes deployment manifests (or equivalent ARM/Bicep templates) are provided under `deploy/kubernetes/` declaring each service's deployment + Dapr sidecar annotation + stable app ID + health-check probes (`/health/live`, `/health/ready`)

**Given** the application starts in a containerized environment
**When** stable app IDs are validated
**Then** `eventstore`, `tenants`, `folders`, `folders-ui`, and `folders-workers` resolve consistently across local Aspire, Staging, and Production environments — the Dapr access-control policy YAML from Story 7.1 portably applies
**And** a smoke test in Staging exercises the canonical task lifecycle (Story 5.5 cross-surface scenario) end-to-end against the containerized deployment
**And** rollback procedure is documented; rolling back any single service does not break canonical lifecycle invariants

### Story 7.4: Wire full CI/CD pipeline and publish NuGet release packages

As a maintainer,
I want `.github/workflows/ci.yml`, `contract-tests.yml`, `nightly-drift.yml`, `policy-conformance.yml`, and `release.yml` covering every CI gate from Stories 1.9, 3.4, 5.4, 7.1 — and `release.yml` publishing `Hexalith.Folders.Contracts`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.Testing` to NuGet on tagged release,
So that the full pipeline (build, format, lint, unit, contract, parity, sentinel, drift, policy-conformance, capacity, release) runs on every PR and tagged release without manual intervention.

**Acceptance Criteria:**

**Given** all CI gate stories from Epics 1–6 are implemented
**When** `.github/workflows/ci.yml` runs on PR
**Then** the pipeline executes (in dependency order with parallelism where possible): build → format → lint (including C10 cache-key tenant-prefix) → unit tests → contract tests (hermetic) → parity tests (C13 transport + behavioral columns) → redaction sentinel tests → encoding-equivalence corpus → C6 transition matrix coverage → exit-criteria-presence → pattern-examples compile gate → NSwag golden-file gate → server-vs-spine BLOCKING validation → symmetric drift gate
**And** `nightly-drift.yml` runs daily covering the live-Forgejo + live-GitHub provider drift detection (Story 3.4) plus oasdiff schema-diff classification (additive=warn, breaking=fail)
**And** `policy-conformance.yml` runs on PRs that touch policy YAML or negative tests (Story 7.1)

**Given** a tagged release is created
**When** `release.yml` runs
**Then** the workflow builds + tests + packages `Hexalith.Folders.Contracts`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.Testing` as NuGet packages with semver-versioned identifiers
**And** packages are published to nuget.org (or the configured Hexalith feed) only after every gate passes
**And** generated SDK artifacts are traceable to source (NFR10): each NuGet package embeds the source-link metadata and the contract-spine commit SHA used for generation
**And** an automated dependency/package security scan runs as part of release; scan failures block publish

### Story 7.5: Calibrate capacity tests, pin C1/C2/C5 numerical targets

As an architect (with PM sign-off),
I want `tests/load/Hexalith.Folders.LoadTests.csproj` calibrated against the production hardware profile pinned in `docs/exit-criteria/c1-capacity.md`, NBomber scenarios covering workspace prepare → lock → mutate → commit at concurrent-tenant + concurrent-task profiles, and the C1 (capacity), C2 (status-freshness), and C5 (scalability quantifiers) numerical values pinned and recorded,
So that the deferred PRD quantitative targets graduate from TBD to measured-and-pinned before MVP release.

**Acceptance Criteria:**

**Given** the Phase 9 exit-criteria operations plan
**When** capacity calibration runs
**Then** `docs/exit-criteria/c1-capacity.md` records concrete numbers for: maximum concurrent tenants, folders per tenant, active workspaces per tenant, concurrent agent tasks per tenant — with measurement method, target hardware profile pinned in the artifact, and rationale
**And** `tests/load/` NBomber scenarios are parameterized against the pinned hardware profile; CI nightly capacity job runs at calibrated load against a Staging-like environment and asserts the canonical lifecycle's p95/p99 latency budgets hold (per NFR21–23)
**And** `docs/exit-criteria/c2-freshness.md` records the maximum acceptable lag between an emitted lifecycle event and its appearance in status/audit views under normal operation; the value is validated by an OpenTelemetry projection-lag metric in Staging
**And** `docs/exit-criteria/c5-scalability-quantifiers.md` replaces the word "multiple" in NFR Scalability with concrete numeric quantifiers derived from C1

**Given** the exit-criteria-presence gate from Story 1.9
**When** the release pipeline runs
**Then** the gate confirms every C-row in §"Exit Criteria Operations Plan" has an artifact link with a non-empty body; release fails if any artifact is still TBD or empty
**And** the release blocker is removable only by populating the artifact, never by editing the gate to skip the row

### Story 7.6: Enforce C3 retention, cleanup observability, and tenant-deletion runbook

As a platform operator (with Legal + PM sign-off on retention policy),
I want C3 retention durations enforced (audit metadata, workspace status, provider correlation IDs, projections, temporary working files, cleanup records), workspace cleanup observable through status (FR30 / NFR57), and a documented tenant-deletion runbook (which records deleted, tombstoned, retained for audit, anonymized — per NFR55 / AR-DOC-05),
So that retention obligations are met without erasing audit evidence, cleanup failures are diagnosable, and tenant deletion runs predictably under legal/compliance pressure.

**Acceptance Criteria:**

**Given** `docs/exit-criteria/c3-retention.md` from Story 1.4
**When** retention enforcement is implemented
**Then** scheduled cleanup workflows in `Hexalith.Folders.Workers` honor the per-data-class retention durations; expired records (audit metadata, workspace status, provider correlation IDs, projections, temporary working files, cleanup records) are removed at the configured cadence
**And** cleanup never removes audit evidence required to reconstruct completed/failed/denied/retried/duplicate/interrupted operations (NFR58); a test confirms that an audit record protected by the "evidence required" rule survives even after its data-class retention expires
**And** cleanup failures emit observable signals: status, reason code, retryability, timestamp, correlation ID (NFR57); operators can see them via the ops console (Story 6.5)

**Given** the tenant-deletion lifecycle
**When** `docs/runbooks/tenant-deletion.md` is authored (per AR-DOC-05)
**Then** the runbook documents which records are deleted, tombstoned, retained for audit, and anonymized — including operational handling for in-flight tasks, held locks, working copies, projections, and Dapr state
**And** the runbook's operational steps are validated by a Staging dry-run that creates a synthetic tenant, exercises the canonical lifecycle, runs tenant deletion, and confirms tombstone + retention behavior matches the runbook
**And** the deletion process emits sentinel-redaction-clean audit records throughout; an isolation test confirms no tenant data resurfaces in any other tenant's projection or audit query post-deletion

### Story 7.7: Wire production observability — pluggable OpenTelemetry exporters, health checks, monitored snapshots, alerts

As a platform operator,
I want production observability built on OpenTelemetry SDK with pluggable exporters (Jaeger / Tempo / Application Insights / Datadog per environment), health-check endpoints (`/health/live`, `/health/ready`), and monitored snapshots (dead-letter topic depth, projection lag, Dapr sidecar health, Tenants-availability degraded-mode flag),
So that incidents are detected and diagnosed before customer escalation, and the per-environment exporter choice does not require code changes.

**Acceptance Criteria:**

**Given** the architecture's I-6 + I-7 specifications
**When** production observability is configured
**Then** `Hexalith.Folders.ServiceDefaults.ServiceDefaultsExtensions` configures the OpenTelemetry SDK with OTLP export by default; per-environment overrides bind via `IConfiguration` to switch to Jaeger / Tempo / Application Insights / Datadog without code changes
**And** traces capture `tenantId`, `correlationId`, `causationId`, `taskId`, `aggregateId`, `eventTypeName` as span attributes; metrics include per-tenant operation counters, projection-lag gauges, dead-letter topic depth, and Dapr sidecar health
**And** sentinel-redaction tests run on the production-shape exporter output (or a staging equivalent) confirming no forbidden patterns leak

**Given** monitored snapshots from I-7
**When** alert rules are configured
**Then** alert rules cover: dead-letter topic depth > threshold, projection lag > C2 freshness target, Dapr sidecar health degraded, Tenants-availability degraded-mode active, provider rate-limit chaos drift signal, hermetic-PR-gate or live-nightly-drift contract test failure
**And** alert routing is documented per environment in `docs/runbooks/alerts.md`; runbook entries link each alert to a remediation playbook
**And** synthetic monitoring exercises the canonical task lifecycle in Production every N minutes (configured per environment) and alerts on regression

### Story 7.8: Publish full documentation deliverables and ADRs

As a downstream consumer (developer, operator, auditor, or new team member),
I want the full documentation deliverable set published per AR-DOC-01..05: rendered OpenAPI v1 reference, getting-started guide, auth/tenant/folder-ACL guide, workspace lifecycle + lock state diagrams, file-operation-to-commit flow diagram, tenant/auth/ACL decision flow diagram, CLI reference, MCP tool/resource reference, SDK reference and quickstart, provider integration + provider contract testing guide, operations console + metadata-only audit guide, error catalog, contract-terms reference, ADRs for major decisions, and the tenant-deletion runbook,
So that consumers and future maintainers can answer their own questions without code archaeology and the architecture's design rationale survives team turnover.

**Acceptance Criteria:**

**Given** AR-DOC-01..05 enumerates the documentation surface
**When** the documentation site is published
**Then** `docs/api/` contains the rendered OpenAPI v1 reference (Redoc / Swagger UI / equivalent) generated from `hexalith.folders.v1.yaml` with schemas, auth requirements, idempotency keys, pagination/filtering conventions, correlation IDs, and per-operation examples
**And** `docs/getting-started.md` walks a new consumer from package install to first successful commit using the SDK + CLI + MCP — referencing `samples/Hexalith.Folders.Sample/` from Story 5.1
**And** `docs/auth/tenant-and-folder-acl.md` documents the layered authorization stack from Story 2.6 with the decision flow diagram
**And** `docs/diagrams/` contains the workspace lifecycle + lock state diagram (sourced from C6 matrix), the file-operation-to-commit flow diagram, and the tenant/auth/ACL decision flow diagram
**And** `docs/cli/reference.md`, `docs/mcp/reference.md`, `docs/sdk/reference.md` enumerate every command/tool/method with examples
**And** `docs/providers/integration.md` + `docs/providers/contract-testing.md` document the IGitProvider port and the provider contract testing approach (hermetic-PR-gate + live-nightly-drift modes)
**And** `docs/operations/console.md` + `docs/operations/audit.md` cover the read-only console workflows and metadata-only audit semantics
**And** `docs/errors/catalog.md` enumerates every canonical error category with REST status, CLI exit behavior, SDK error/result behavior, retryability, client action, and audit/logging expectations

**Given** ADRs preserve design rationale
**When** the ADR set is authored
**Then** at minimum the following ADRs land in `docs/adrs/`: ADR-0001 Hexalith.Tenants baseline + EventStore.Admin.* surfaces (Step 3 starter selection); ADR-0002 SDK-as-canonical reframe + Adapter Parity Contract; ADR-0003 OpenAPI 3.1 Contract Spine (C0); ADR-0004 Bimodal file transport (D-9); ADR-0005 Two-tier idempotency TTL (D-7); ADR-0006 Working-copy ephemerality (D-8); ADR-0007 Pluggable production OIDC (S-2); ADR-0008 Provider port N-provider capability model
**And** every ADR follows the `docs/adrs/0000-template.md` structure (Title, Status, Context, Decision, Consequences) and references the architecture document section it implements
**And** `docs/contract-terms.md` (Story 1.3 stub) is fully populated with FR1–FR3 vocabulary references
