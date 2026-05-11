---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - "_bmad-output/planning-artifacts/prd.md"
  - "_bmad-output/planning-artifacts/architecture.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-10.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-10-readiness-story-split.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-05-10-readiness-correction.md"
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
date: '2026-05-10'
status: 'complete'
completedAt: '2026-05-10'
epicCount: 7
storyCount: 86
---

# Hexalith.Folders - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.Folders, decomposing the requirements from the PRD, Architecture, and approved sprint/readiness-correction proposals into implementable stories.

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

#### Approved Readiness-Correction Requirements

- AR-PROPOSAL-01: Apply the approved backlog corrections from `sprint-change-proposal-2026-05-10.md`, `sprint-change-proposal-2026-05-10-readiness-story-split.md`, and `sprint-change-proposal-2026-05-10-readiness-correction.md` before sprint planning or implementation.
- AR-PROPOSAL-02: Reframe Epic 1 as consumer-facing contract value: a scaffolded module plus canonical OpenAPI v1 Contract Spine that prevents drift across REST, SDK, CLI, and MCP before downstream feature work depends on it.
- AR-PROPOSAL-03: Reframe Epic 7 as an MVP release-readiness gate for NFR validation and release evidence rather than a normal feature epic.
- AR-PROPOSAL-04: Remove forward-story acceptance dependencies from Stories 4.3, 4.11, 6.3, 6.4, and 4.4 so each story is independently completable in sequence.
- AR-PROPOSAL-05: Split combined or oversized stories into independently reviewable units: Contract Spine authoring, CI gate families, repository creation vs existing-repository binding, file mutation policy/write/delete flows, lifecycle validation risk families, cross-surface parity concerns, CI/CD vs release publishing, and documentation vs ADR/runbook deliverables.
- AR-PROPOSAL-06: Preserve 57/57 FR coverage while renumbering affected stories and updating intra-document story references after the approved splits.
- AR-PROPOSAL-07: Add an NFR traceability bridge: every PRD NFR bullet must map to an epic/story acceptance criterion, architecture exit criterion artifact, automated test gate, or documented release-validation evidence; release fails if any PRD NFR bullet remains unmapped.
- AR-PROPOSAL-08: Synchronize `D:\Hexalith.Folders\_bmad-output\implementation-artifacts\sprint-status.yaml` after `epics.md` is revised, then rerun implementation readiness before sprint planning proceeds.

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
- FR31: Epic 4 and Epic 6 — lifecycle status currency produced by the task lifecycle and surfaced for operators
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
- FR43: Epic 1 and Epic 4 — canonical error taxonomy defined in the Contract Spine and realized by lifecycle behavior
- FR44: Epic 4 — full error category set (validation/auth/tenant/folder ACL/credential/provider/capability/repository/branch/lock/workspace/path/commit/read-model/duplicate/transient)
- FR45: Epic 4 — canonical workspace/task states (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`) per C6 matrix
- FR46: Epic 4 — final-state explanation + retry eligibility + operational evidence after any lifecycle failure
- FR47: Epic 1 and Epic 5 — versioned REST contract authored first, then proven through cross-surface parity
- FR48: Epic 5 — CLI canonical lifecycle parity
- FR49: Epic 5 — MCP canonical lifecycle parity
- FR50: Epic 1 and Epic 5 — SDK generated from the Contract Spine and proven through canonical lifecycle parity
- FR51: Epic 1 and Epic 5 — cross-surface equivalence defined by the Contract Spine/parity oracle and validated across surfaces
- FR52: Epic 6 — read-only ops console projection consumption (readiness, binding, workspace, lock, dirty, commit, failure, provider, credential-ref, sync)
- FR53: Epic 6 — metadata-only audit trail inspection (success/denied/failed/retried/duplicate)
- FR54: Epic 6 — incident reconstruction from immutable audit metadata
- FR55: Epic 4 (write-side: redaction in events/projections/logs/traces/metrics) + Epic 6 (read-side: console rendering with classification + lock-icon affordance)
- FR56: Epic 6 — operation timelines for folder, workspace, file, lock, commit, provider, status, authorization events
- FR57: Epic 6 — provider support evidence visibility for GitHub and Forgejo

## Epic List

### Epic 1: Bootstrap Canonical Contract For Consumers And Adapters
API consumers, adapter implementers, and maintainers can rely on a scaffolded Hexalith.Folders module with one OpenAPI v1 Contract Spine driving REST, SDK, CLI, and MCP before feature work begins.
**FRs covered:** FR1, FR2, FR3, FR43, FR47, FR50, FR51

### Epic 2: Tenant-Scoped Folder Access And Lifecycle
Tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access.
**FRs covered:** FR4, FR5, FR6, FR8, FR9, FR10, FR11, FR12, FR13, FR14

### Epic 3: Provider Readiness And Repository Binding
Platform engineers and authorized actors can configure Git providers, validate readiness, create repository-backed folders, bind existing repositories, define branch/ref policy, and inspect provider capability evidence without exposing secrets.
**FRs covered:** FR7, FR15, FR16, FR17, FR18, FR19, FR20, FR21, FR22, FR23, FR57

### Epic 4: Repository-Backed Workspace Task Lifecycle
Developers and AI agents can prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and receive deterministic failure, status, idempotency, and redaction behavior through the canonical repository-backed task lifecycle.
**FRs covered:** FR24, FR25, FR26, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34, FR35, FR37, FR38, FR39, FR40, FR41, FR42, FR43, FR44, FR45, FR46, FR55

### Epic 5: Cross-Surface Workflow Parity
API, SDK, CLI, and MCP users can run the same canonical lifecycle with equivalent operation identity, errors, idempotency, audit behavior, authorization outcomes, terminal states, and mixed-surface handoff.
**FRs covered:** FR47, FR48, FR49, FR50, FR51

### Epic 6: Read-Only Operations Console And Audit Review
Operators and audit reviewers can inspect readiness, locks, dirty state, failures, commits, timelines, provider evidence, and metadata-only audit records through a read-only console without mutation or file-content exposure.
**FRs covered:** FR31, FR36, FR45, FR46, FR52, FR53, FR54, FR55, FR56, FR57

### Release Readiness Workstream 7: MVP Operational Acceptance And Evidence
Release reviewers, operators, and maintainers can accept the MVP for production only when safety, contract, deployment, observability, retention, documentation, package-traceability, and NFR evidence are complete.
**FRs covered:** Cross-cutting validation for all FRs; no new product FR scope.

## Epic 1: Bootstrap Canonical Contract For Consumers And Adapters

API consumers, adapter implementers, and maintainers can rely on a scaffolded Hexalith.Folders module with one OpenAPI v1 Contract Spine driving REST, SDK, CLI, and MCP before feature work begins.

### Story 1.1: Establish a consumer-buildable module scaffold

As a platform engineer and downstream consumer,
I want the Hexalith.Folders solution scaffold to build with the approved project layout,
So that consumers and later stories have a stable, convention-compliant module baseline.

**Acceptance Criteria:**

**Given** an empty Hexalith.Folders repository
**When** the scaffold is created
**Then** `Hexalith.Folders.slnx` contains the expected src, test, and sample projects
**And** project references follow the architecture dependency direction and target .NET 10
**And** `dotnet build` succeeds for the scaffold without requiring provider credentials, tenant data, or initialized nested submodules.

### Story 1.2: Establish root configuration and submodule policy

As a maintainer,
I want root repository configuration and root-level submodule policy established,
So that builds are reproducible and nested submodules are not initialized accidentally.

**Acceptance Criteria:**

**Given** the scaffolded repository
**When** root configuration is added
**Then** `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig`, `.gitmodules`, and `Hexalith.Folders.slnx` exist
**And** setup guidance forbids recursive nested-submodule initialization unless explicitly requested.

### Story 1.3: Seed minimally valid normative fixtures

As a maintainer,
I want normative fixtures and artifact templates to be minimally valid and owned by later gates,
So that contract, parity, redaction, encoding, and load tests have stable inputs rather than empty placeholders.

**Acceptance Criteria:**

**Given** the scaffolded repository
**When** fixture placeholders are seeded
**Then** the audit leakage corpus, parity schema, previous spine, and idempotency encoding corpus exist under `tests/fixtures` with minimal valid content
**And** fixture schemas or smoke validation prove the files are parseable and intentionally incomplete where applicable
**And** `tests/load`, `tests/tools/parity-oracle-generator`, `docs/exit-criteria/_template.md`, and `docs/adrs/0000-template.md` exist with ownership notes linking them to later CI or release-readiness stories.

### Story 1.4: Author Phase 0.5 Pre-Spine Workshop deliverables

As an architect and maintainer,
I want Contract Spine blocking decisions resolved,
So that the canonical contract starts with real retention, input-limit, state, and auth values.

**Acceptance Criteria:**

**Given** the architecture exit-criteria plan
**When** the Pre-Spine Workshop deliverables are authored
**Then** C3 retention and C4 input-limit artifacts exist under `docs/exit-criteria`
**And** S-2 OIDC validation parameters and the C6 transition-matrix implementation mapping are documented.

### Story 1.5: Finalize idempotency equivalence and adapter parity rules

As an adapter implementer,
I want idempotency equivalence and parity dimensions defined before endpoints are authored,
So that REST, SDK, CLI, and MCP cannot drift on operation identity or error handling.

**Acceptance Criteria:**

**Given** the approved commands and queries
**When** adapter-parity metadata is defined
**Then** every mutating command has an `x-hexalith-idempotency-equivalence` field list
**And** every operation declares required parity dimensions and read-consistency or idempotency rules.

### Story 1.6: Author Contract Spine foundation and shared extension vocabulary

As an API consumer and adapter implementer,
I want shared OpenAPI conventions and Hexalith extensions defined,
So that every capability group uses the same contract language.

**Acceptance Criteria:**

**Given** the Phase 0.5 decisions are complete
**When** the OpenAPI 3.1 Contract Spine foundation is authored
**Then** shared conventions for auth, idempotency, correlation, pagination, freshness, errors, lifecycle states, audit metadata, and sensitive metadata are present
**And** all `x-hexalith-*` extensions required by architecture C0 are declared.

### Story 1.7: Author tenant, folder, provider, and repository-binding contract groups

As an API consumer and adapter implementer,
I want tenant, folder, provider, and repository-binding operations represented in the Contract Spine,
So that access and provider readiness capabilities are canonical before implementation begins.

**Acceptance Criteria:**

**Given** the shared contract vocabulary exists
**When** these contract groups are authored
**Then** folder lifecycle, ACL, effective-permissions, provider-binding, provider-readiness, repository creation, repository binding, and branch/ref policy operations have schemas
**And** each operation declares its required metadata explicitly:
- all operations declare canonical error categories, authorization requirements, audit classification, correlation ID behavior, and parity dimensions
- mutating operations declare idempotency-key requirements and idempotency-equivalence fields
- read/query operations declare freshness, pagination/filtering, authorization-denial shape, and read-consistency expectations

### Story 1.8: Author workspace and lock contract groups

As an API consumer and adapter implementer,
I want workspace preparation and lock operations represented in the Contract Spine,
So that task lifecycle entry and concurrency behavior are canonical before implementation.

**Acceptance Criteria:**

**Given** the shared contract vocabulary exists
**When** workspace and lock contract groups are authored
**Then** prepare, lock, release, lock-inspection, state-transition, and retry-eligibility operations have schemas
**And** workspace and lock operations declare authorization requirements, idempotency-key requirements, idempotency-equivalence fields, canonical error categories, audit classification, correlation ID behavior, retry eligibility, lease/expiry semantics, and parity dimensions.

### Story 1.9: Author file mutation and context query contract groups

As an API consumer and adapter implementer,
I want file mutation and context query operations represented in the Contract Spine,
So that file changes and read-only context access preserve the same policy boundaries across surfaces.

**Acceptance Criteria:**

**Given** workspace and lock contract groups exist
**When** file and context contract groups are authored
**Then** file add/change/remove, tree, metadata, search, glob, and bounded range-read operations have schemas
**And** path, binary, range, result-limit, content-boundary, and secret-safe response rules are declared
**And** mutating file operations declare idempotency and audit metadata, while context queries declare freshness, pagination or result bounds, redaction behavior, authorization-denial shape, and parity dimensions.

### Story 1.10: Author commit and workspace-status contract groups

As an API consumer and adapter implementer,
I want commit and status operations represented in the Contract Spine,
So that clean committed states, failed states, and unknown provider outcomes are reported consistently.

**Acceptance Criteria:**

**Given** lifecycle command contract groups exist
**When** commit and status contract groups are authored
**Then** commit, commit evidence, workspace status, task status, provider outcome, and reconciliation status operations have schemas
**And** final state, retry eligibility, retry-after, correlation, canonical error metadata, audit evidence, provider unknown-outcome handling, reconciliation status, idempotency behavior for commit commands, and parity dimensions are declared.

### Story 1.11: Author audit and ops-console query contract groups

As an operator and audit reviewer,
I want audit and ops-console query operations represented in the Contract Spine,
So that diagnostics and incident reconstruction use metadata-only read models.

**Acceptance Criteria:**

**Given** lifecycle status contract groups exist
**When** audit and ops-console query contract groups are authored
**Then** audit trail, operation timeline, readiness, lock, dirty-state, failed-operation, provider-status, and sync-status queries have schemas
**And** schemas exclude file contents, diffs, provider tokens, credential material, secrets, and unauthorized resource existence
**And** audit and ops-console query schemas declare sensitive-metadata classification, redaction shape, authorization-denial shape, pagination/filtering, freshness, correlation ID behavior, and parity dimensions.

### Story 1.12: Wire NSwag SDK generation with idempotency helpers

As an SDK consumer,
I want generated typed clients and idempotency-hash helpers from the Contract Spine,
So that .NET callers use the same operation shapes and retry identity semantics as REST.

**Acceptance Criteria:**

**Given** the Contract Spine file exists
**When** SDK generation runs
**Then** NSwag emits reproducible generated clients and DTOs
**And** mutating command DTOs expose `ComputeIdempotencyHash()` based on declared equivalence fields.

### Story 1.13: Generate the C13 parity oracle

As a maintainer,
I want the C13 parity oracle generated from the Contract Spine,
So that cross-surface tests consume one source of truth for transport and behavioral parity.

**Acceptance Criteria:**

**Given** the Contract Spine declares parity metadata
**When** the parity-oracle generator runs
**Then** `tests/fixtures/parity-contract.yaml` is generated and schema-validated
**And** rows include both transport-parity and behavioral-parity columns.

### Story 1.14: Wire Contract Spine drift and generated-client CI gates

As a maintainer,
I want contract drift and generated-client consistency gates wired into CI,
So that surface divergence fails before feature implementation can depend on it.

**Acceptance Criteria:**

**Given** the Contract Spine and generated client exist
**When** CI runs
**Then** server-vs-spine validation, symmetric drift detection, NSwag golden-file consistency, and parity-oracle schema validation run
**And** any drift without an approved deprecation or regenerated artifact fails the build.

### Story 1.15: Wire safety invariant CI gates

As a maintainer,
I want safety invariant gates wired into CI,
So that implementation cannot leak secrets, file contents, or tenant data through generated or runtime artifacts.

**Acceptance Criteria:**

**Given** the sentinel corpus exists
**When** CI runs
**Then** sentinel-corpus redaction tests and forbidden-field scanning execute against configured output channels
**And** detected file content, token, credential, generated-context, or unauthorized-resource leakage fails the build.

### Story 1.16: Wire exit-criteria and parity completeness gates

As a maintainer,
I want governance and completeness gates wired into CI,
So that missing release evidence, unstable idempotency encoding, stale examples, or unsafe tenant cache keys block implementation.

**Acceptance Criteria:**

**Given** the fixture and exit-criteria artifacts exist
**When** CI runs
**Then** idempotency-encoding equivalence, pattern-example compilation, exit-criteria presence, tenant-prefixed cache-key lint, and parity completeness checks execute
**And** missing required evidence or metadata fails the build.

## Epic 2: Tenant-Scoped Folder Access And Lifecycle

Tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access.

### Story 2.1: Stand up domain service host with Tenants integration

As a platform engineer,
I want the Folders service hosted with Tenants integration and a fail-closed local tenant projection,
So that every folder operation has tenant identity and availability semantics before domain behavior is added.

**Acceptance Criteria:**

**Given** the scaffolded service host exists
**When** Tenants integration is wired
**Then** the service subscribes to `system.tenants.events` and builds `FolderTenantAccessProjection`
**And** stale or unavailable tenant data fails closed for mutations.

### Story 2.2: Implement Organization aggregate ACL baseline

As a tenant administrator,
I want organization-level folder access controls represented in the domain,
So that folder permissions can be granted consistently to users, groups, roles, and delegated service agents.

**Acceptance Criteria:**

**Given** an organization aggregate exists
**When** ACL baseline commands are processed
**Then** allowed principals and delegated actors are persisted as metadata-only events
**And** no credential material or unauthorized resource detail appears in events.

### Story 2.3: Create folders within a tenant

As an authorized actor,
I want to create logical folders inside my tenant,
So that repository-backed workspace tasks have a tenant-scoped logical home.

**Acceptance Criteria:**

**Given** the actor has create permission
**When** `CreateFolder` is accepted
**Then** a folder aggregate is created with an opaque identifier and active lifecycle state
**And** tenant scope comes from auth context, not request payload authority.

### Story 2.4: Grant and revoke folder access

As a tenant administrator,
I want to grant and revoke folder access for permitted principals,
So that access to folders can evolve without changing repository bindings.

**Acceptance Criteria:**

**Given** a folder exists and the administrator has ACL permission
**When** access is granted or revoked
**Then** effective ACL metadata changes are recorded and projected
**And** revoked access is honored within the C7 freshness budget.

### Story 2.5: Inspect effective permissions

As an authorized actor,
I want to inspect effective permissions for a folder or task context,
So that I can explain who can perform work before a task begins.

**Acceptance Criteria:**

**Given** a folder exists
**When** an authorized actor requests effective permissions
**Then** the response shows allowed actions and principal sources
**And** it omits unauthorized resource existence and secret material.

### Story 2.6: Enforce layered authorization with safe denials

As a system component executing a folder operation,
I want layered authorization to run before resource access,
So that cross-tenant access is denied without enumeration or leakage.

**Acceptance Criteria:**

**Given** a request targets a folder resource
**When** authorization runs
**Then** JWT, tenant projection, folder ACL, EventStore validators, and Dapr policy are evaluated before access
**And** denied requests return safe error shapes and metadata-only denial evidence.

### Story 2.7: Inspect folder lifecycle and binding status

As an authorized actor,
I want to inspect folder lifecycle and binding status,
So that I can tell whether a folder is active, archived, unbound, or repository-backed.

**Acceptance Criteria:**

**Given** the actor has folder read permission
**When** folder status is requested
**Then** lifecycle and binding metadata are returned
**And** provider credentials, tokens, and embedded credential URLs are never returned.

### Story 2.8: Archive folders with audit preservation

As a tenant administrator,
I want to archive folders when policy allows,
So that retired work is no longer active while audit and status evidence remain available.

**Acceptance Criteria:**

**Given** a folder is eligible for archive
**When** `ArchiveFolder` is accepted
**Then** lifecycle state becomes archived and future mutating task commands are rejected
**And** audit and status evidence remain queryable under retention policy.

### Story 2.9: React to Tenants events through Worker handlers

As a system component,
I want worker handlers to react to tenant lifecycle and membership events,
So that Folders authorization stays aligned with tenant administration.

**Acceptance Criteria:**

**Given** a relevant Tenants event is published
**When** the Folders worker receives it
**Then** local tenant-access projections and folder authorization metadata are updated idempotently
**And** only `folders.*` configuration keys are processed.

## Epic 3: Provider Readiness And Repository Binding

Platform engineers and authorized actors can configure Git providers, validate readiness, create repository-backed folders, bind existing repositories, define branch/ref policy, and inspect provider capability evidence without exposing secrets.

### Story 3.1: Configure provider binding and credential reference

As a platform engineer,
I want to configure a provider binding and credential reference for a tenant,
So that repository-backed folder creation can be gated by known provider configuration.

**Acceptance Criteria:**

**Given** the actor has provider configuration permission
**When** a binding is configured
**Then** provider kind, binding ID, credential reference ID, naming policy, and branch policy are recorded
**And** token material is never stored or returned.

### Story 3.2: Define IGitProvider port and capability model

As a provider adapter implementer,
I want an N-provider capability-discoverable Git provider port,
So that GitHub, Forgejo, and future providers can expose differences without changing product semantics.

**Acceptance Criteria:**

**Given** provider adapters are implemented behind a port
**When** capabilities are queried
**Then** supported operations, branch/ref behavior, file limits, credential mode, and failure categories are exposed as metadata
**And** the model is not hardcoded to exactly two providers.

### Story 3.3: Implement GitHub provider adapter

As a platform engineer,
I want a GitHub provider adapter using Octokit,
So that tenants can create and bind GitHub repositories through the canonical provider port.

**Acceptance Criteria:**

**Given** a GitHub binding and credential reference exist
**When** readiness, repository, branch/ref, file, commit, and status operations are called
**Then** the adapter returns canonical provider results and failure categories
**And** ambiguous provider outcomes return `unknown_provider_outcome` rather than silent retry.

### Story 3.4: Implement Forgejo provider adapter and drift detection

As a platform engineer,
I want a Forgejo provider adapter with version snapshots and schema drift detection,
So that Forgejo support is verified against pinned API behavior.

**Acceptance Criteria:**

**Given** supported Forgejo versions are listed
**When** contract tests and nightly drift checks run
**Then** schema drift is classified as warning or failure according to policy
**And** readiness cannot report ready for an unsupported or failing provider version.

### Story 3.5: Validate provider readiness with safe diagnostics

As a platform engineer,
I want to validate provider readiness before repository-backed creation or binding,
So that configuration failures are caught before workspace tasks begin.

**Acceptance Criteria:**

**Given** a tenant has provider binding metadata
**When** readiness validation runs
**Then** the result includes ready/failed state, safe reason code, retryability, remediation category, provider reference, and correlation ID
**And** secrets and credential values are not included.

### Story 3.6: Create a new repository-backed folder

As an authorized actor,
I want to create a new provider repository for an existing logical folder after readiness passes,
So that a tenant folder can become repository-backed through a controlled provisioning path.

**Acceptance Criteria:**

**Given** a logical folder exists and provider readiness is green
**When** `CreateRepositoryBackedFolder` is accepted
**Then** repository provisioning is requested idempotently and folder state moves toward ready according to C6
**And** repository creation failures use stable provider and repository error categories.

### Story 3.7: Bind an existing repository to a folder

As an authorized actor,
I want to bind an existing provider repository to an existing logical folder,
So that pre-created repositories can participate in the canonical lifecycle without sharing repository-creation failure paths.

**Acceptance Criteria:**

**Given** a logical folder exists and provider readiness is green
**When** `BindRepository` validates repository access and branch/ref compatibility
**Then** binding metadata is recorded and projected
**And** repository access failures do not expose unauthorized repository existence.

### Story 3.8: Define branch and ref policy

As an authorized actor,
I want to define or select branch/ref policy for repository-backed tasks,
So that workspace preparation and commits use predictable refs.

**Acceptance Criteria:**

**Given** a repository-backed folder exists
**When** branch/ref policy is configured
**Then** the selected policy is stored as metadata and validated against provider capabilities
**And** incompatible policies return stable branch/ref conflict errors.

### Story 3.9: Inspect tenant and per-provider readiness evidence

As a platform engineer,
I want to inspect tenant and provider readiness evidence,
So that I can diagnose provider setup before agents run workspace tasks.

**Acceptance Criteria:**

**Given** provider bindings and capability results exist
**When** readiness evidence is requested
**Then** provider support evidence for GitHub and Forgejo is returned as safe metadata
**And** credential material, tokens, and secret diagnostics are excluded.

## Epic 4: Repository-Backed Workspace Task Lifecycle

Developers and AI agents can prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and receive deterministic failure, status, idempotency, and redaction behavior through the canonical repository-backed task lifecycle.

### Story 4.1: Implement Folder aggregate state machine with C6 transition matrix

As a domain developer,
I want the Folder aggregate to implement the C6 transition matrix,
So that every lifecycle command produces a defined transition or explicit rejection.

**Acceptance Criteria:**

**Given** the C6 matrix is documented
**When** folder commands are handled
**Then** valid transitions emit metadata-only events and invalid transitions reject with `state_transition_invalid`
**And** aggregate tests cover every state/event pair.

### Story 4.2: Prepare workspace from a ready repository-backed folder

As a developer or AI agent,
I want to prepare a workspace from a ready repository-backed folder,
So that file work starts from a known provider and branch/ref state.

**Acceptance Criteria:**

**Given** provider readiness, repository binding, branch/ref policy, and task context are valid
**When** `PrepareWorkspace` is accepted
**Then** workspace preparation starts idempotently and exposes status visibility
**And** unknown provider outcome enters reconciliation rather than silent retry.

### Story 4.3: Acquire task-scoped workspace lock

As a developer or AI agent,
I want to acquire a task-scoped workspace lock,
So that concurrent work cannot create mixed writes or lost updates.

**Acceptance Criteria:**

**Given** a workspace is ready and no conflicting lock exists
**When** `AcquireWorkspaceLock` is accepted
**Then** folder state transitions `ready` to `locked`
**And** `FolderState` and emitted event metadata capture owner, age/expiry basis, and retry-eligibility metadata for later projections.

### Story 4.4: Inspect lock state and release the workspace lock

As an authorized actor,
I want to inspect and release a workspace lock when policy allows,
So that completed or abandoned task ownership is visible and controlled.

**Acceptance Criteria:**

**Given** a lock exists
**When** lock state is inspected or release is requested
**Then** permitted lock metadata is returned and valid release changes state according to C6
**And** if mutations have been applied, release is rejected because the state model requires commit before clean release or expiry to dirty.

### Story 4.5: Enforce workspace path policy before file mutations

As a developer or AI agent holding the workspace lock,
I want every file path normalized and validated before mutation,
So that no file operation can escape the workspace or create ambiguous provider-specific paths.

**Acceptance Criteria:**

**Given** a file mutation command is submitted
**When** path validation runs
**Then** traversal, absolute paths, mixed separators, reserved names, symlink escapes, Unicode ambiguity, and case collisions are rejected
**And** denials use `path_policy_denied` without unsafe path echoing.

### Story 4.6: Add and change files with inline and streamed content transport

As a developer or AI agent holding the workspace lock,
I want to add or change files through bounded inline and streamed transports,
So that writes are deterministic, retry-safe, and aligned with D-9.

**Acceptance Criteria:**

**Given** path policy passes and the caller owns the lock
**When** add or change is submitted through inline or multipart transport
**Then** size, binary, and media limits are enforced before provider writes
**And** events record content hash, byte length, media type, task, operation, and correlation metadata without file contents.

### Story 4.7: Remove files with metadata-only events and provider-safe ordering

As a developer or AI agent holding the workspace lock,
I want to remove files through the same policy pipeline as writes,
So that deletes are auditable, idempotent, and cannot bypass workspace or tenant boundaries.

**Acceptance Criteria:**

**Given** a delete request targets a permitted workspace-relative path
**When** `RemoveFile` is accepted
**Then** the provider-safe delete operation is ordered with the task changes
**And** emitted events remain metadata-only and idempotent.

### Story 4.8: Query file context with policy boundaries

As a developer or AI agent,
I want file tree, metadata, search, glob, bounded range-read, and extension-safe semantic context-query behavior,
So that task context is useful without unbounded scans, stale derived-index authority, or secret exposure.

**Acceptance Criteria:**

**Given** the actor has context-query permission
**When** a context query runs
**Then** tenant access, folder ACL, path policy, sensitivity classification, binary/large-file policy, and range/result limits are enforced before execution
**And** denied queries produce metadata-only audit evidence
**And** any semantic/RAG retrieval backend, including Hexalith.Memories, is invoked only after Folders authorization and policy checks pass
**And** derived semantic indexes are never treated as authoritative for tenant access, folder ACL, file truth, workspace state, or audit truth.

### Story 4.9: Inspect workspace and projection currency

As an authorized actor,
I want to inspect workspace, lock, dirty state, last commit, failed operation, and projection currency,
So that callers and operators have one trustworthy status answer.

**Acceptance Criteria:**

**Given** lifecycle events have been emitted
**When** workspace status is requested
**Then** canonical state, lock metadata, dirty evidence, last commit, last failure, and freshness metadata are returned
**And** stale or unavailable read-model state is classified explicitly.

### Story 4.10: Surface workspace cleanup status without repair automation

As an operator or developer,
I want cleanup status visible after completed, failed, interrupted, or abandoned tasks,
So that working-copy state is understandable without MVP repair controls.

**Acceptance Criteria:**

**Given** a task lifecycle has cleanup implications
**When** cleanup status is queried
**Then** pending, succeeded, failed, or status-only cleanup state is visible with reason, retryability, timestamp, and correlation ID
**And** no repair, discard, or hidden mutation action is exposed.

### Story 4.11: Propagate idempotency keys, correlation, and task IDs

As a caller,
I want mutating lifecycle commands to require idempotency and propagate correlation and task IDs,
So that retries never duplicate events, provider writes, file changes, repositories, or commits.

**Acceptance Criteria:**

**Given** a mutating lifecycle command is submitted
**When** idempotency validation runs
**Then** same key plus equivalent payload returns the same logical result and conflicting payload returns idempotency conflict
**And** correlation and task IDs propagate to events, projections, audit, logs, and traces.

### Story 4.12: Commit workspace changes with unknown-outcome reconciliation

As a developer or AI agent,
I want to commit workspace changes with task, actor, author, branch/ref, commit message, changed-path, operation, and correlation metadata,
So that repository-backed work reaches a clean committed state or an inspectable failure state.

**Acceptance Criteria:**

**Given** changes are staged and the caller owns the lock
**When** `CommitWorkspace` is accepted
**Then** successful commit records commit reference and transitions to `committed`
**And** ambiguous provider response transitions to `unknown_provider_outcome` and schedules reconciliation without silent retry.

### Story 4.13: Surface canonical errors and operational evidence after failure

As a caller using REST, SDK, CLI, or MCP,
I want failures reported through the canonical error taxonomy and workspace states,
So that final state, retry eligibility, and client action are explainable.

**Acceptance Criteria:**

**Given** a lifecycle command or status surface returns a failure available by this point
**When** the response is produced
**Then** it includes final state per C6, retry eligibility, retry-after hint when known, correlation ID, and categorized reason
**And** metadata needed by later audit/projection stories is included without changing the canonical error shape.

### Story 4.14: Emit metadata-only audit and observability

As an operator and audit reviewer,
I want lifecycle operations to emit metadata-only audit, traces, metrics, and structured logs,
So that incidents can be reconstructed without exposing file contents or secrets.

**Acceptance Criteria:**

**Given** any successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, or state-transition operation occurs
**When** audit and observability records are emitted
**Then** tenant, actor, task, operation, correlation, folder, provider, timestamp, result, duration, state transition, and sanitized error category are recorded
**And** file contents, diffs, tokens, credentials, and secrets are excluded.

### Story 4.15: Validate lifecycle replay and projection determinism

As a maintainer,
I want replay and projection determinism tests for the canonical lifecycle,
So that aggregate state and read models can be rebuilt consistently from durable events.

**Acceptance Criteria:**

**Given** canonical lifecycle event streams exist
**When** replay and projection tests run
**Then** aggregate state and read models rebuild to equivalent deterministic state
**And** nondeterministic freshness fields are explicitly excluded from determinism assertions.

### Story 4.16: Validate lifecycle security boundaries

As a maintainer,
I want sentinel-redaction, path-security, encoding-equivalence, and cross-tenant isolation tests for the lifecycle,
So that secret safety, path safety, encoding stability, and tenant isolation are checked mechanically.

**Acceptance Criteria:**

**Given** lifecycle operations and fixtures exist
**When** security boundary tests run
**Then** sentinel, path, encoding, and cross-tenant negative cases fail on any leak or unsafe acceptance
**And** denied operations produce safe error shapes and metadata-only audit evidence.

### Story 4.17: Seed lifecycle capacity test harness

As a maintainer,
I want the NBomber lifecycle capacity harness seeded with prepare, lock, mutate, and commit scenarios,
So that lifecycle scenarios capture capacity dimensions early and provide reusable evidence for release calibration.

**Acceptance Criteria:**

**Given** the lifecycle operations are available
**When** capacity harness scaffolding runs
**Then** parameterized scenarios exist without final production thresholds
**And** the harness records enough dimensions for tenant, folder, workspace, task, and operation concurrency calibration.

## Epic 5: Cross-Surface Workflow Parity

API, SDK, CLI, and MCP users can run the same canonical lifecycle with equivalent operation identity, errors, idempotency, audit behavior, authorization outcomes, terminal states, and mixed-surface handoff.

### Story 5.1: Ship SDK convenience helpers, samples, and quickstart

As an SDK consumer,
I want ergonomic helpers, samples, and quickstart material,
So that I can use the canonical lifecycle without learning internal transport details.

**Acceptance Criteria:**

**Given** generated SDK methods exist
**When** helpers and samples are added
**Then** upload convenience, idempotency guidance, correlation/task ID handling, and a local AppHost sample are documented
**And** helpers do not introduce lifecycle semantics absent from the Contract Spine.

### Story 5.2: Implement CLI commands with behavioral-parity rules

As a CLI user,
I want commands that mirror the canonical lifecycle,
So that terminal workflows behave like SDK and REST workflows.

**Acceptance Criteria:**

**Given** the SDK client is available
**When** CLI commands are implemented
**Then** provider, folder, workspace, file, commit, context, and audit commands wrap SDK behavior
**And** pre-SDK errors, idempotency-key sourcing, correlation sourcing, and exit codes follow the Adapter Parity Contract.

### Story 5.3: Implement MCP tools, resources, and failure kinds

As an MCP client,
I want tools and resources for the canonical lifecycle,
So that AI tools can work with folders without direct filesystem or provider ownership.

**Acceptance Criteria:**

**Given** the SDK client is available
**When** MCP tools and resources are implemented
**Then** one tool per canonical command/query is available where appropriate
**And** failures map to the canonical MCP failure-kind set with correlation ID, code, retryability, and client action.

### Story 5.4: Consume parity oracle in CLI and MCP tests

As a maintainer,
I want CLI and MCP tests to consume behavioral-parity oracle columns,
So that adapter behavior cannot drift from the canonical contract.

**Acceptance Criteria:**

**Given** `parity-contract.yaml` exists
**When** CLI and MCP tests run
**Then** behavioral-parity columns drive assertions for pre-SDK errors, key sourcing, correlation sourcing, exit codes, and failure kinds
**And** missing rows or unsupported categories fail tests.

### Story 5.5: Validate golden lifecycle parity across REST and SDK

As a stakeholder validating one canonical workflow contract,
I want the golden lifecycle scenario executed through REST and SDK,
So that transport parity is proven before CLI and MCP adapter behavior is layered on.

**Acceptance Criteria:**

**Given** REST endpoints and SDK client are available
**When** the golden lifecycle scenario runs through both surfaces
**Then** operation identity, authorization, errors, idempotency, audit metadata, correlation, and terminal states match oracle expectations
**And** any transport drift fails loudly.

### Story 5.6: Validate behavioral parity across CLI and MCP

As a stakeholder validating adapter behavior,
I want CLI and MCP behavior tested against the same canonical lifecycle rules,
So that adapter-specific UX does not change product semantics.

**Acceptance Criteria:**

**Given** CLI and MCP surfaces wrap the SDK
**When** behavioral parity tests run
**Then** credential sourcing, usage errors, idempotency-key sourcing, correlation defaults, CLI exit codes, and MCP failure kinds match the Adapter Parity Contract
**And** adapters preserve canonical error categories.

### Story 5.7: Validate mixed-surface handoff scenario

As an automation developer,
I want one task lifecycle to move between REST, SDK, CLI, and MCP using the same IDs,
So that real integrations can hand off work without losing state or auditability.

**Acceptance Criteria:**

**Given** all four surfaces are available
**When** provider readiness, create/bind, prepare, lock, write, query, commit, status, and release are split across surfaces
**Then** task ID, correlation ID, operation IDs, audit records, and terminal state remain coherent
**And** any surface-specific drift in idempotency replay or error category fails the scenario.

## Epic 6: Read-Only Operations Console And Audit Review

Operators and audit reviewers can inspect readiness, locks, dirty state, failures, commits, timelines, provider evidence, and metadata-only audit records through a read-only console without mutation or file-content exposure.

### Story 6.1: Audit and operation-timeline query endpoints

As an audit reviewer,
I want query endpoints for metadata-only audit and operation timelines,
So that incidents can be reconstructed without file contents or secrets.

**Acceptance Criteria:**

**Given** audit projection data exists
**When** audit or timeline queries run
**Then** records are paginated, filtered, tenant-scoped, and metadata-only
**And** sensitive metadata classification is applied consistently.

### Story 6.2: Scaffold FrontComposer-hosted read-only operations console

As an operator,
I want a read-only Blazor Web App console hosted by `Hexalith.Folders.UI` and rendered through `FrontComposerShell`,
So that I can diagnose workspace state through a governed, tenant-aware UI.

**Acceptance Criteria:**

**Given** projection query endpoints exist
**When** the console shell is implemented
**Then** `Hexalith.Folders.UI` is a Blazor Web App host using Interactive Server rendering, `FrontComposerShell` as the primary layout, Fluent UI through the FrontComposer/Shell pattern, OIDC auth, SDK or read-only query-service projection access, and no direct aggregate write paths
**And** a real Folders/Tenants `IUserContextAccessor` replaces the fail-closed FrontComposer default before tenant-scoped queries are enabled
**And** navigation supports tenant and folder diagnostic workflows
**And** no FrontComposer mutation command forms, file browsing, file editing, raw diff display, repair actions, credential reveal, or unrestricted filesystem browsing are exposed in MVP.

### Story 6.3: Render operator-disposition labels as primary visual

As an operator,
I want disposition labels to be the primary state visual with technical state secondary,
So that incident response uses human-actionable language.

**Acceptance Criteria:**

**Given** workspace state metadata is available
**When** status components render
**Then** `OperatorDispositionBadge` and technical-state metadata use the C6 mapping
**And** the badge and metadata components expose reusable parameters verified by this story's tests so diagnostic views can use the mapping without duplicating logic.

### Story 6.4: Implement sensitive-metadata redaction affordance

As an operator,
I want redacted metadata to render differently from unknown or missing data,
So that policy-hidden fields do not look like system defects.

**Acceptance Criteria:**

**Given** sensitive metadata is redacted by policy
**When** the UI renders the field
**Then** a visible lock-icon affordance and explanatory text are shown
**And** the redaction component exposes reusable rendering semantics verified by this story's tests so diagnostic views can distinguish redacted, unknown, and missing values consistently.

### Story 6.5: Author console diagnostic wireflow notes

As an operator and accessibility reviewer,
I want lightweight console wireflow notes for primary diagnostic workflows,
So that implementation of diagnostic pages follows reviewed information hierarchy, interaction states, and accessibility expectations.

**Acceptance Criteria:**

**Given** PRD console requirements, architecture decisions F-1 through F-7, and the FrontComposer technical research exist
**When** console wireflow notes are authored
**Then** folder, workspace, provider, audit, incident-mode, redaction, loading, empty, and error states are described under `docs/ux/ops-console-wireflows.md`
**And** the notes identify FrontComposer shell layout, navigation, projection-view composition, tenant/user context expectations, read-only command-suppression behavior, and generated/custom projection boundaries
**And** the notes identify keyboard-navigation, focus, non-color-only status, zoom readability, and redaction-vs-missing expectations for Epic 6 stories
**And** Stories 6.6, 6.7, 6.8, 6.9, and 6.10 cannot begin implementation until `docs/ux/ops-console-wireflows.md` exists and has been reviewed against PRD console requirements, architecture decisions F-1 through F-7, and the FrontComposer technical research.

### Story 6.6: Build folder and workspace diagnostic pages

As an operator,
I want folder and workspace diagnostic pages,
So that lifecycle, readiness, lock, dirty state, commit state, failure state, and cleanup status are inspectable.

**Acceptance Criteria:**

**Given** projection endpoints, reusable status components, and console wireflow notes exist
**When** folder and workspace diagnostic pages render
**Then** pages show authorized lifecycle, readiness, lock, dirty, commit, failure, cleanup, freshness, and correlation metadata
**And** no file editing, file browsing, raw diff, credential reveal, repair action, or mutation control is present.

### Story 6.7: Build provider readiness and support diagnostic pages

As an operator,
I want provider readiness and support diagnostic pages,
So that provider binding, credential-reference status, capability differences, and provider failure evidence are inspectable without secrets.

**Acceptance Criteria:**

**Given** projection endpoints, provider support evidence, and console wireflow notes exist
**When** provider diagnostic pages render
**Then** pages show authorized provider binding, credential-reference identifier/status, readiness reason, retryability, remediation category, capability, sync, and failure metadata
**And** provider tokens, credential values, embedded credential URLs, and unauthorized repository existence are never displayed.

### Story 6.8: Build audit and operation-timeline diagnostic pages

As an audit reviewer and operator,
I want audit and operation-timeline diagnostic pages,
So that incidents can be reconstructed from metadata-only evidence.

**Acceptance Criteria:**

**Given** audit projection endpoints and console wireflow notes exist
**When** audit and timeline pages render
**Then** records are paginated, filtered, tenant-scoped, and show actor, task, operation, correlation, folder, provider, timestamp, result, duration, state transition, and sanitized error category where authorized
**And** sensitive metadata classification and redaction affordances are applied consistently.

### Story 6.9: Implement incident-mode last-resort read path

As an operator,
I want an ACL-checked incident stream when projections are degraded,
So that diagnosis can continue while read models recover.

**Acceptance Criteria:**

**Given** projections are degraded and the actor has incident permission
**When** `/_admin/incident-stream` renders
**Then** it shows a persistent degraded-mode banner, raw event metadata, disposition labels from Story 6.3, and correlation/time-window copy affordance
**And** redacted values render through the shared redaction component from Story 6.4 with no relaxed policy.

### Story 6.10: Enforce console performance and perceived-wait UX

As an operator,
I want diagnostic pages to meet console performance budgets and show clear loading states,
So that the console remains useful during incidents.

**Acceptance Criteria:**

**Given** console pages call projection endpoints
**When** pages load
**Then** primary diagnostic flows meet p95 and p99 budgets or produce measured release evidence
**And** skeleton state appears at 400 ms and a cancel affordance appears at 2 seconds for in-flight requests.

### Story 6.11: Verify no-mutation enforcement and accessibility

As a release reviewer,
I want the console verified as read-only and WCAG 2.2 AA conformant,
So that the MVP console satisfies its safety and accessibility promises.

**Acceptance Criteria:**

**Given** the console is feature complete
**When** verification runs
**Then** automated and manual checks confirm no mutation paths, credential reveal, file-content browsing, file editing, raw diff display, hidden repair action, or unrestricted filesystem browsing
**And** keyboard navigation, focus states, semantic headings, readable tables, contrast, zoom readability, and non-color-only indicators meet WCAG 2.2 AA expectations.

## Release Readiness Workstream 7: MVP Operational Acceptance And Evidence

Release reviewers, operators, and maintainers can accept the MVP for production only when safety, contract, deployment, observability, retention, documentation, package-traceability, and NFR evidence are complete.

This workstream is not a product FR-bearing epic. It is a release-readiness gate that consumes evidence from Epics 1-6 and blocks MVP acceptance when required evidence is missing. Sprint planning must treat these items as release governance and hardening work, not as a peer product capability increment.

### Story 7.1: Deploy production Dapr deny-by-default access control

As a platform operator,
I want production Dapr access control to default deny with mTLS and negative-test conformance,
So that service invocation and pub/sub are constrained beyond local development.

**Acceptance Criteria:**

**Given** production Dapr policy YAML exists
**When** policy-conformance tests run
**Then** unauthorized source app, target app, and operation triples receive 403
**And** policy YAML changes require corresponding negative-test updates.

### Story 7.2: Configure production OIDC and secret store integration

As a platform operator,
I want pluggable production OIDC and Dapr secret-store integration configured,
So that authentication and credential references work without storing secret material in Folders.

**Acceptance Criteria:**

**Given** production identity and secret-store settings exist
**When** services start
**Then** JWT validation uses frozen S-2 parameters and secret access uses references only
**And** no provider token or credential value is stored in Folders state.

### Story 7.3: Build container images with stable Dapr app IDs

As a platform operator,
I want one container image per service with stable Dapr app IDs,
So that deployment policy applies consistently across environments.

**Acceptance Criteria:**

**Given** server, workers, and UI projects build
**When** container images are produced
**Then** image metadata and app IDs are stable for local, staging, and production
**And** deployment manifests attach sidecars and preserve access-control assumptions.

### Story 7.4: Consolidate baseline build and unit CI gates

As a maintainer,
I want baseline build, format, lint, and unit gates consolidated in PR CI,
So that every pull request proves the solution is mechanically healthy.

**Acceptance Criteria:**

**Given** feature implementation projects exist
**When** `.github/workflows/ci.yml` runs
**Then** restore, build, format, lint, and unit-test gates execute with stable caching and clear failure categories
**And** failures block merge.

### Story 7.5: Consolidate contract and parity CI gates

As a maintainer,
I want contract and parity gates consolidated in PR CI,
So that public surface drift is caught before merge.

**Acceptance Criteria:**

**Given** Contract Spine, generated client, and parity oracle artifacts exist
**When** `.github/workflows/ci.yml` runs
**Then** server-vs-spine validation, generated-client consistency, parity-oracle schema validation, and cross-surface parity checks execute
**And** failures block merge with actionable artifact names.

### Story 7.6: Consolidate security and redaction CI gates

As a maintainer and security reviewer,
I want sentinel, redaction, forbidden-field, and tenant cache-key gates consolidated in PR CI,
So that leaks of file contents, secrets, provider tokens, credential material, or tenant data block merge.

**Acceptance Criteria:**

**Given** security fixtures and redaction pipelines exist
**When** `.github/workflows/ci.yml` runs
**Then** sentinel-corpus, redaction, forbidden-field, and tenant-prefixed cache-key checks execute
**And** failures identify the emitting channel without exposing sensitive payloads.

### Story 7.7: Add capacity-smoke CI gate

As a maintainer,
I want a lightweight capacity-smoke gate in PR CI,
So that obvious lifecycle performance regressions are caught before release calibration.

**Acceptance Criteria:**

**Given** lifecycle capacity harness scenarios exist
**When** `.github/workflows/ci.yml` runs
**Then** smoke scenarios for prepare, lock, mutate, commit, and status paths execute with non-production thresholds
**And** failures block merge while final C1, C2, and C5 targets remain owned by release calibration.

### Story 7.8: Wire scheduled drift and policy-conformance workflows

As a maintainer,
I want scheduled drift and policy-conformance workflows separate from PR CI,
So that live provider drift and production policy regressions are caught continuously.

**Acceptance Criteria:**

**Given** provider contract and Dapr policy tests exist
**When** scheduled workflows run
**Then** nightly drift and policy-conformance results are reported with clear failure categories
**And** breaking provider drift or unauthorized policy changes fail the workflow.

### Story 7.9: Publish traceable NuGet release packages

As a downstream consumer,
I want versioned release packages published only after release gates pass,
So that consumers receive traceable and semver-versioned packages.

**Acceptance Criteria:**

**Given** a tagged release is created and gates pass
**When** release publishing runs
**Then** Contracts, Client, Aspire, and Testing packages are published to the configured feed
**And** package metadata traces back to source commit, contract version, and release evidence.

### Story 7.10: Calibrate capacity tests and pin C1/C2/C5 targets

As a release reviewer,
I want capacity and status-freshness targets calibrated with evidence,
So that scalability claims are measured rather than assumed.

**Acceptance Criteria:**

**Given** the lifecycle capacity harness exists
**When** calibration runs
**Then** C1, C2, and C5 artifacts record target numbers, hardware profile, methodology, results, and rationale
**And** release fails if required target evidence is missing.

### Story 7.11: Enforce C3 retention and tenant-deletion behavior

As an operator and compliance reviewer,
I want retention, cleanup observability, and tenant-deletion behavior enforced,
So that lifecycle evidence is retained or removed according to policy.

**Acceptance Criteria:**

**Given** C3 retention policy exists
**When** retention and deletion validation runs
**Then** audit metadata, workspace status, provider correlation IDs, projections, temporary files, and cleanup records follow policy
**And** tenant-deletion handling documents deleted, tombstoned, retained, and anonymized records.

### Story 7.12: Wire production observability and alerts

As a platform operator,
I want production observability exporters, health checks, monitored snapshots, and alerts wired,
So that operational failures are visible outside local Aspire.

**Acceptance Criteria:**

**Given** production observability settings exist
**When** services run
**Then** traces, metrics, logs, health, projection lag, dead-letter depth, provider failures, stale locks, and cleanup failures are exported or alerted
**And** emitted telemetry respects redaction and sensitive metadata policy.

### Story 7.13: Publish API, SDK, CLI, and MCP consumer references

As a downstream consumer,
I want API, SDK, CLI, and MCP references published,
So that I can use the product without reading implementation code.

**Acceptance Criteria:**

**Given** surfaces are implemented
**When** consumer documentation is generated
**Then** rendered OpenAPI reference, SDK quickstart, CLI reference, MCP tool/resource reference, examples, auth guidance, and lifecycle diagrams are published
**And** examples compile or are otherwise validated by CI.

### Story 7.14: Publish operations and audit documentation

As an operator or audit reviewer,
I want operations-console and metadata-only audit documentation published,
So that production diagnosis and incident reconstruction are repeatable.

**Acceptance Criteria:**

**Given** operations console and audit surfaces exist
**When** operations and audit documentation is published
**Then** console workflows, metadata-only audit fields, redaction behavior, incident-mode use, alerting handoff, and backup/recovery expectations are documented
**And** examples avoid file contents, provider tokens, credential material, secrets, and unauthorized resource details.

### Story 7.15: Publish provider and error documentation

As an operator and integration maintainer,
I want provider integration, retryability, and canonical error documentation published,
So that provider failures and client actions are diagnosable without reading implementation code.

**Acceptance Criteria:**

**Given** provider contracts and canonical error taxonomy exist
**When** provider and error documentation is published
**Then** provider integration/testing, supported versions, drift handling, error catalog, retryability, retry-after behavior, and client action guidance are documented
**And** GitHub and Forgejo capability differences are explicit.

### Story 7.16: Publish NFR traceability bridge

As a release reviewer,
I want every PRD NFR bullet mapped to implementation evidence,
So that MVP acceptance can prove non-functional coverage rather than rely on narrative claims.

**Acceptance Criteria:**

**Given** release gates, architecture exit criteria, and story evidence exist
**When** `docs/exit-criteria/nfr-traceability.md` is published
**Then** every PRD NFR bullet maps to story IDs, architecture exit criteria, automated gates, manual validation evidence, or release artifacts
**And** missing NFR evidence fails the release-readiness review.

### Story 7.17: Publish ADR set and maintenance runbooks

As a future maintainer or architect,
I want ADRs and lifecycle runbooks published,
So that design rationale and operational decisions survive handoff and release pressure.

**Acceptance Criteria:**

**Given** MVP release evidence is complete
**When** ADRs and runbooks are reviewed
**Then** ADRs cover major contract, provider, idempotency, security, observability, and deployment decisions
**And** runbooks cover tenant deletion, retention, alerts, rollback, provider drift, reconciliation, and incident-mode operations.
