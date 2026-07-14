---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
assessmentStatus: NOT_READY
reportKind: rerun-before-approved-structural-correction
governingReadinessDecision: false
preservedTriggerReport: implementation-readiness-report-2026-07-14.md
includedDocuments:
  prd:
    - prd.md
  architecture:
    - architecture.md
  epics:
    - epics.md
  ux:
    - ux-design-specification.md
supportingDocuments:
  - prd-validation-report.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-14
**Project:** folders

## Document Inventory

### PRD Files Found

**Whole Documents:**

- `prd.md` (75,633 bytes, modified 2026-07-07 20:50)
- `prd-validation-report.md` (57,462 bytes, modified 2026-06-26 17:32; supporting validation report)

**Sharded Documents:** None found.

### Architecture Files Found

**Whole Documents:**

- `architecture.md` (181,678 bytes, modified 2026-07-07 21:03)

**Sharded Documents:** None found.

### Epics and Stories Files Found

**Whole Documents:**

- `epics.md` (170,016 bytes, modified 2026-07-07 21:05)

**Sharded Documents:** None found.

### UX Design Files Found

**Whole Documents:**

- `ux-design-specification.md` (55,660 bytes, modified 2026-07-07 09:25)

**Sharded Documents:** None found.

### Discovery Issues

- No whole-document versus sharded-document duplicates were found.
- No required document category is missing.
- The primary assessment inputs are `prd.md`, `architecture.md`, `epics.md`, and `ux-design-specification.md`.
- `prd-validation-report.md` is retained as supporting material rather than treated as the primary PRD.

## PRD Analysis

### Functional Requirements

#### Capability Contract Terms
FR1: Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology.
FR2: Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility.
FR3: Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries.

#### Authorization and Tenant Boundary
FR4: Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks.
FR5: Tenant administrators can grant folder access to users, groups, roles, and delegated service agents.
FR6: Authorized actors can inspect effective permissions for a folder or task context.
FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

#### Folder Lifecycle
FR11: Authorized actors can create logical folders within a tenant.
FR12: Authorized actors can inspect folder lifecycle and binding status.
FR13: Authorized actors can archive folders when policy allows.
FR14: The system can preserve audit and status evidence for archived folders.

#### Provider Readiness and Repository Binding
FR15: Platform engineers can configure supported Git provider bindings and credential references for a tenant.
FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
FR18: Authorized actors can create a repository-backed folder when readiness checks pass.
FR19: Authorized actors can bind a folder to an existing repository where supported.
FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.
FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
FR23: Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior.

#### Workspace and Lock Lifecycle
FR24: Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid.
FR25: Authorized actors can acquire a task-scoped workspace lock.
FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
FR27: The system can deny competing operations when lock ownership or workspace state makes the operation unsafe.
FR28: The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions.
FR29: Authorized actors can release a workspace lock when ownership and policy allow.
FR30: The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP.
FR31: Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable.

#### File Operations and Context Queries
FR32: Authorized actors can add, change, and remove files within a prepared and locked workspace.
FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
FR34: Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads.
FR35: The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses.
FR36: The operations console can remain read-only and excluded from file editing or file-content browsing capabilities.

#### Commit, Evidence, and Idempotency
FR37: Authorized actors can commit workspace changes for repository-backed folders.
FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits.
FR39: The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID.
FR40: The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence.
FR41: The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers.
FR42: The system can reject duplicate logical operations when retry identity or operation intent conflicts.

#### Error, Status, and Diagnostics Contract
FR43: The system can expose a canonical error taxonomy across supported surfaces.
FR44: The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure.
FR45: The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.
FR46: The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure.

#### Cross-Surface Contract
FR47: API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle.
FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

#### Audit and Operations Visibility
FR52: Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status.
FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
FR54: Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference.
FR55: The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses.
FR56: The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events.
FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

#### Authorized Search Facade
FR58: Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see — security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only — without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence.

**Total FRs: 58**

### Non-Functional Requirements

#### Security and Tenant Isolation
NFR1: Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, provider callback, and audit record.
NFR2: Cross-tenant access leaks are zero-tolerance defects. No object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
NFR3: Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, background jobs, provider callbacks, audit records, and context-query results.
NFR4: File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
NFR5: Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
NFR6: Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.
NFR7: Credential references must be validated and displayed only as non-secret identifiers or status indicators.
NFR8: Provider credentials and repository bindings must be tenant-scoped and must not be reused across tenants, even if repository URLs appear identical.
NFR9: Provider credentials must use the least privilege required for supported lifecycle operations and must be validated against required provider capabilities before use.
NFR10: Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.

#### Reliability, Idempotency, and Failure Visibility
NFR11: Every lifecycle step must expose terminal and non-terminal state, including `Pending`, `InProgress`, `Succeeded`, `Failed`, and `Cancelled` where cancellation is supported.
NFR12: Required observable lifecycle states include `ProviderReady`, `RepositoryBound`, `WorkspacePrepared`, `Locked`, `FilesChanged`, `CommitPending`, `Committed`, `CleanupPending`, and `Cleaned`.
NFR13: Repository-backed task lifecycle operations must leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.
NFR14: Idempotency keys are required for workspace preparation, lock acquisition, file mutation, commit, and cleanup request operations.
NFR15: A repeated call with the same idempotency key and equivalent payload must return the same logical result; the same key with a conflicting payload must return an idempotency conflict.
NFR16: Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.
NFR17: Lock acquisition must be deterministic, tenant-scoped, and limited to one active write lock per tenant/repository/workspace scope.
NFR18: Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.
NFR19: Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
NFR20: Failure visibility must expose state, cause category, retryability, and correlation ID without providing automated remediation in MVP.

#### Performance and Query Bounds
NFR21: Command submission must acknowledge accepted lifecycle commands within 1 second p95 before asynchronous provider or workspace work continues.
NFR22: Status and audit summary queries must return within 500 ms p95 for bounded MVP inputs.
NFR23: Context queries must return within 2 seconds p95 for bounded MVP inputs.
NFR24: Performance targets apply to bounded MVP inputs and control-plane responses. Targets must be validated against implementation benchmarks and recalibrated before release if provider or runtime constraints make the initial target misleading.
NFR25: Provider and workspace operations may complete asynchronously when external Git provider latency or workspace size exceeds interactive response budgets; callers must receive operation identity and status visibility rather than blocking indefinitely.
NFR26: Context queries must define and enforce maximum files, maximum bytes, maximum result count, maximum query duration, timeout behavior, truncation behavior, and included/excluded result audit visibility.
NFR27: File tree, search, glob, metadata, and bounded range queries must protect the service from unbounded workspace scans.
NFR28: Large file and binary handling limits must be explicit before MVP release; unsupported files must fail with stable policy errors rather than causing unbounded processing.
NFR29: Provider calls must use explicit timeout budgets, retry limits, and backoff caps.
NFR30: Provider calls must report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states rather than leaving callers waiting indefinitely.
NFR31: Provider rate-limit responses must preserve retry hints where available and expose retry-after or classified retryability.

#### Scalability and Capacity
NFR32: The system must support multiple tenants, folders, repositories, workspaces, and concurrent agent tasks without shared mutable state causing cross-tenant or cross-task interference.
NFR33: Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.
NFR34: Audit, timeline, and file-context projections must remain queryable as folder history grows.
NFR35: Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.
NFR36: MVP capacity targets must avoid assuming a single tenant, single repository, or single active workspace, while avoiding unsupported claims about massive scale before concrete load targets are defined.

#### Integration and Contract Compatibility
NFR37: REST, CLI, MCP, and SDK surfaces must preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes; transport shape and UX may differ.
NFR38: Public contracts must be versioned. Breaking changes to lifecycle commands, queries, error categories, workspace states, provider capabilities, or audit fields require an explicit new versioned contract.
NFR39: The product must support at least the active contract version and define a deprecation policy before removing any public lifecycle contract.
NFR40: Shared or generated contract tests must validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
NFR41: Generated SDKs, MCP tool definitions, CLI command schemas, and OpenAPI contracts must be derived from or validated against the same canonical lifecycle contract.
NFR42: GitHub and Forgejo support must be validated through provider contract tests before either provider is marked ready.
NFR43: Provider contract tests must cover only MVP-dependent lifecycle behavior: readiness, repository binding, branch/ref handling, file operations, commit, status, provider errors, and failure behavior.
NFR44: Supported GitHub and Forgejo API versions or behavior assumptions must be pinned or recorded so provider compatibility drift is visible.
NFR45: Provider capability differences must be reported explicitly instead of inferred by clients from failed operations.
NFR46: Provider failures such as timeout, rate limit, authentication failure, authorization failure, repository missing, repository conflict, branch/ref conflict, unavailable provider, invalid path, commit rejected, and unknown outcome must map to stable product error categories.

#### Observability, Auditability, and Replay
NFR47: Every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
NFR48: Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
NFR49: Allowed audit metadata must be explicitly classified. File paths, commit messages, repository names, branch names, and provider error payloads must be treated as potentially sensitive metadata.
NFR50: Sensitive audit metadata such as file paths, branch names, commit messages, repository names, and provider diagnostic payloads must be classified and protected through access control, hashing, truncation, or redaction where appropriate.
NFR51: Operations-console views must be read-model–based, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata.
NFR52: Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.
NFR53: Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.
NFR54: The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.
NFR55: Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.

#### Data Retention and Cleanup
NFR56: Retention periods must be defined for audit metadata, workspace status, provider correlation IDs, projections, temporary working files, and cleanup records.
NFR57: Retention durations are policy decisions and must be defined before production release; the PRD requires explicit retention semantics but does not set final retention periods.
NFR58: Tenant deletion must define which records are deleted, tombstoned, retained for audit, or anonymized.
NFR59: Workspace cleanup visibility must state whether cleanup is automatic, best-effort, retryable, user-triggered, or status-only for MVP.
NFR60: Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID.
NFR61: No cleanup process may remove audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations.

#### Operations Console Accessibility
NFR62: Read-only operations console flows must target WCAG 2.2 AA.
NFR63: The console must support keyboard navigation for primary diagnostic workflows.
NFR64: Status, failure, readiness, and lock indicators must not rely on color alone.
NFR65: Console screens must provide visible focus states, semantic headings, readable table structure, and sufficient contrast.
NFR66: Console text, controls, and tables must remain readable at common browser zoom levels used by operators.

#### Verification Expectations
NFR67: Each NFR category must have at least one automated verification path or documented manual validation path before MVP release.
NFR68: Security, tenant isolation, idempotency, provider contract, read-model determinism, and cross-surface contract compatibility NFRs must have automated tests.
NFR69: Performance, accessibility, retention, backup/recovery, and operations-console usability NFRs must have release validation evidence before MVP acceptance.
NFR70: Security verification must include dependency/package scanning, generated artifact review, and least-privilege provider credential validation.

**Total NFRs: 70**

### Additional Requirements

- The MVP is repository-backed first. Local-only folders, local-first promotion, brownfield adoption, repair automation, nested repositories, simultaneous multi-agent writes, and operations-console mutation or file browsing are explicit non-goals.
- REST/OpenAPI v1 is the canonical public contract. CLI, MCP, SDK, and console surfaces must remain adapters or projections with equivalent authorization, operation identity, idempotency, errors, state transitions, and audit outcomes.
- Hexalith.Tenants remains authoritative for tenant identity, lifecycle, and membership; Hexalith.EventStore supplies platform-owned command, event, projection, query, cursor, read-model, and domain-service mechanics. Hexalith.Folders owns folder policy and ACLs, provider-binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.
- File contents and temporary working-copy material must remain outside EventStore. File contents, diffs, generated context payloads, provider tokens, credential values, secrets, and unauthorized-resource existence are prohibited from events, logs, traces, metrics, projections, audit, console output, diagnostics, and errors.
- Required public operations include provider-readiness validation; folder creation and repository binding; workspace preparation and locking; file add/change/remove; commit; workspace, file, and audit queries; and metadata-only operations-console projections.
- Mutating commands require idempotency and correlation identity plus version/conflict semantics. Unknown provider outcomes must enter a reconciliation-required state before any retry that could duplicate repositories, writes, or commits.
- GitHub and Forgejo require explicit capability reporting and provider contract evidence; clients must not infer capability differences from failed operations.
- Public contracts require versioning, compatibility testing, a deprecation policy, shared golden lifecycle scenarios, and generated or validated OpenAPI, SDK, CLI, and MCP contracts.
- Architecture must resolve the file-content transport model, large-file and binary policy, provider-capability contract, projection-compaction strategy, lock lease/expiry defaults, and file-operation batch atomicity before implementation stories are finalized.
- Quantitative exit criteria C1, C2, and C5 are recorded as approved. C3 retention durations and C4 bounded context-query limits remain explicitly TBD in the PRD.
- Documentation requirements include OpenAPI reference, getting started, authorization/ACL guidance, lifecycle diagrams, CLI/MCP/SDK references, provider integration guidance, audit/console guidance, and a cross-surface error catalog.
- Quality gates require complete adapter parity, ACL matrix and provider contract coverage; tenant-isolation, path-security, idempotency, read-model determinism, schema, provider-failure, context-query security, and sentinel-redaction evidence.

### PRD Completeness Assessment

The PRD is unusually comprehensive in scope definition, security boundaries, cross-surface behavior, failure semantics, and verification expectations. It contains 58 explicitly numbered functional requirements and 70 extractable non-functional requirements across nine quality categories.

The following clarity and readiness risks require downstream validation:

- The source NFRs are not assigned stable identifiers. The NFR1–NFR70 labels above are assessment-local identifiers, so later edits could silently shift traceability.
- C3 retention durations and C4 query bounds remain unresolved in the PRD. Their architecture resolution and story coverage must be verified.
- File-content transport, large-file/binary policy, lock defaults, projection compaction, and file-operation batch atomicity are deliberately deferred architecture decisions and therefore potential implementation blockers if still unresolved.
- FR58 mixes a product requirement with implementation status and references a C9 approval gate, while the PRD's quantitative exit-criteria table defines only C1–C5. The C9 authority and acceptance evidence need an authoritative definition.
- Several clauses remain conditional or qualitative—such as “where supported,” “when policy allows,” “where appropriate,” “common browser zoom levels,” and capacity support for “multiple” resources—and need concrete acceptance boundaries in architecture or stories.
- Retention, tenant deletion, backup/recovery, deprecation, and cleanup semantics are required but not fully quantified in the PRD itself.

## Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document claims coverage for all 58 PRD functional requirements. The owning epic mappings are reproduced in the matrix below.

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology. | Epic 1 — vocabulary in OpenAPI Contract Spine + `docs/contract-terms.md` | ✓ Covered |
| FR2 | Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility. | Epic 1 — lifecycle vocabulary via `x-hexalith-lifecycle-states` extension + diagrams | ✓ Covered |
| FR3 | Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries. | Epic 1 — command/query distinction in OpenAPI operation grouping + Server endpoint routing | ✓ Covered |
| FR4 | Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks. | Epic 2 — tenant administrator ACL configuration via `OrganizationAggregate` ACL baseline | ✓ Covered |
| FR5 | Tenant administrators can grant folder access to users, groups, roles, and delegated service agents. | Epic 2 — folder access grant to users, groups, roles, and delegated service agents | ✓ Covered |
| FR6 | Authorized actors can inspect effective permissions for a folder or task context. | Epic 2 — effective-permissions inspection | ✓ Covered |
| FR7 | Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks. | Epic 3 — tenant readiness inspection (depends on provider configuration) | ✓ Covered |
| FR8 | The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope. | Epic 2 — layered authorization evaluation (foundation: JWT → claim transform → tenant projection → folder ACL → EventStore validators → Dapr policy) | ✓ Covered |
| FR9 | The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information. | Epic 2 — cross-tenant denial before any file/workspace/credential/repository/lock/commit/provider/audit access | ✓ Covered |
| FR10 | The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details. | Epic 2 — authorization evidence (allowed and denied) without unauthorized resource enumeration | ✓ Covered |
| FR11 | Authorized actors can create logical folders within a tenant. | Epic 2 — folder creation | ✓ Covered |
| FR12 | Authorized actors can inspect folder lifecycle and binding status. | Epic 2 — folder lifecycle and binding inspection | ✓ Covered |
| FR13 | Authorized actors can archive folders when policy allows. | Epic 2 — folder archive | ✓ Covered |
| FR14 | The system can preserve audit and status evidence for archived folders. | Epic 2 — audit and status evidence preservation for archived folders | ✓ Covered |
| FR15 | Platform engineers can configure supported Git provider bindings and credential references for a tenant. | Epic 3 — provider binding + credential reference configuration per tenant | ✓ Covered |
| FR16 | Authorized actors can validate provider readiness before repository-backed folder creation or binding. | Epic 3 — provider readiness validation before repository-backed creation/binding | ✓ Covered |
| FR17 | The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID. | Epic 3 — readiness diagnostics with safe reason codes, retryability, remediation category, provider reference, correlation ID | ✓ Covered |
| FR18 | Authorized actors can create a repository-backed folder when readiness checks pass. | Epic 3 — repository-backed folder creation when readiness passes | ✓ Covered |
| FR19 | Authorized actors can bind a folder to an existing repository where supported. | Epic 3 — folder binding to existing repository | ✓ Covered |
| FR20 | Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks. | Epic 3 — branch/ref policy selection | ✓ Covered |
| FR21 | The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets. | Epic 3 — provider/credential-reference/binding/branch/capability metadata exposure (no secrets) | ✓ Covered |
| FR22 | The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle. | Epic 3 — GitHub vs Forgejo capability differences exposed explicitly | ✓ Covered |
| FR23 | Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior. | Epic 3 — per-provider readiness evidence for canonical lifecycle (readiness, repo binding, branch/ref, file ops, commit, status, failure behavior) | ✓ Covered |
| FR24 | Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid. | Epic 4 — workspace preparation | ✓ Covered |
| FR25 | Authorized actors can acquire a task-scoped workspace lock. | Epic 4 — task-scoped workspace lock acquisition | ✓ Covered |
| FR26 | Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata. | Epic 4 — lock state, owner, task, age, expiry, retry-eligibility metadata inspection | ✓ Covered |
| FR27 | The system can deny competing operations when lock ownership or workspace state makes the operation unsafe. | Epic 4 — competing-operation denial under unsafe lock/state | ✓ Covered |
| FR28 | The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions. | Epic 4 — lock state transitions (active, expired, stale, abandoned, interrupted, released) | ✓ Covered |
| FR29 | Authorized actors can release a workspace lock when ownership and policy allow. | Epic 4 — workspace lock release | ✓ Covered |
| FR30 | The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP. | Epic 4 — workspace cleanup status visibility for completed/failed/interrupted/abandoned task lifecycles | ✓ Covered |
| FR31 | Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable. | Epic 4 and Epic 6 — lifecycle status currency produced by the task lifecycle and surfaced for operators | ✓ Covered |
| FR32 | Authorized actors can add, change, and remove files within a prepared and locked workspace. | Epic 4 — file add/change/remove (PutFileInline ≤256KB + PutFileStream multipart) | ✓ Covered |
| FR33 | The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy. | Epic 4 — file-operation policy violation rejection (workspace boundary, path, branch/ref, lock, tenant, provider, folder) | ✓ Covered |
| FR34 | Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads. | Epic 4 — context queries via tree, metadata, search, glob, bounded range reads | ✓ Covered |
| FR35 | The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses. | Epic 4 — context-query policy boundaries (paths, exclusions, binary handling, range/result limits, secret-safe responses) | ✓ Covered |
| FR36 | The operations console can remain read-only and excluded from file editing or file-content browsing capabilities. | Epic 6 — read-only console scope (no file editing or content browsing in console) | ✓ Covered |
| FR37 | Authorized actors can commit workspace changes for repository-backed folders. | Epic 4 — workspace commit for repository-backed folders | ✓ Covered |
| FR38 | Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits. | Epic 4 — task/operation/correlation/actor/author/branch/commit-message/changed-path metadata attachment | ✓ Covered |
| FR39 | The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID. | Epic 4 — task and commit evidence exposure (provider, binding, branch, paths, status, commit ref, timestamps, IDs) | ✓ Covered |
| FR40 | The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence. | Epic 4 — failed/incomplete/duplicate/retried/conflicting operation reporting with stable status and audit evidence | ✓ Covered |
| FR41 | The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers. | Epic 4 — idempotent lifecycle retries with stable task/operation/correlation IDs | ✓ Covered |
| FR42 | The system can reject duplicate logical operations when retry identity or operation intent conflicts. | Epic 4 — duplicate logical operation rejection on retry-identity or intent conflict | ✓ Covered |
| FR43 | The system can expose a canonical error taxonomy across supported surfaces. | Epic 1 and Epic 4 — canonical error taxonomy defined in the Contract Spine and realized by lifecycle behavior | ✓ Covered |
| FR44 | The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure. | Epic 4 — full error category set (validation/auth/tenant/folder ACL/credential/provider/capability/repository/branch/lock/workspace/path/commit/read-model/duplicate/transient) | ✓ Covered |
| FR45 | The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`. | Epic 4 — canonical workspace/task states (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`) per C6 matrix | ✓ Covered |
| FR46 | The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure. | Epic 4 — final-state explanation + retry eligibility + operational evidence after any lifecycle failure | ✓ Covered |
| FR47 | API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle. | Epic 1 and Epic 5 — versioned REST contract authored first, then proven through cross-surface parity | ✓ Covered |
| FR48 | CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5 — CLI canonical lifecycle parity | ✓ Covered |
| FR49 | MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5 — MCP canonical lifecycle parity | ✓ Covered |
| FR50 | SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 1 and Epic 5 — SDK generated from the Contract Spine and proven through canonical lifecycle parity | ✓ Covered |
| FR51 | The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior. | Epic 1 and Epic 5 — cross-surface equivalence defined by the Contract Spine/parity oracle and validated across surfaces | ✓ Covered |
| FR52 | Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status. | Epic 6 — read-only ops console projection consumption (readiness, binding, workspace, lock, dirty, commit, failure, provider, credential-ref, sync) | ✓ Covered |
| FR53 | Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations. | Epic 6 — metadata-only audit trail inspection (success/denied/failed/retried/duplicate) | ✓ Covered |
| FR54 | Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference. | Epic 6 — incident reconstruction from immutable audit metadata | ✓ Covered |
| FR55 | The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses. | Epic 4 (write-side: redaction in events/projections/logs/traces/metrics) + Epic 6 (read-side: console rendering with classification + lock-icon affordance) | ✓ Covered |
| FR56 | The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events. | Epic 6 — operation timelines for folder, workspace, file, lock, commit, provider, status, authorization events | ✓ Covered |
| FR57 | Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness. | Epic 6 — provider support evidence visibility for GitHub and Forgejo | ✓ Covered |
| FR58 | Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see — security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only — without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence. | Epic 10 — authorized Memories search-index query facade with Folders-side tenant/folder/workspace trimming, authoritative hydration, and metadata-only redaction | ✓ Covered |

### Missing Requirements

No PRD functional requirements are missing from the epic coverage map.

### Epic-Only Requirements

No epic coverage-map FR identifiers exist outside the PRD inventory.

### Coverage Statistics

- Total PRD FRs: 58
- FRs represented in the epic coverage map: 58
- FRs covered in epics: 58
- Missing FRs: 0
- Epic-only FR identifiers: 0
- Coverage percentage: 100.0%

This result validates claimed FR-to-epic traceability only. It does not yet validate story quality, acceptance-criteria sufficiency, implementation status, or whether each epic's stories actually realize the mapped requirement.

## UX Alignment Assessment

### UX Document Status

**Found and complete:** `ux-design-specification.md` contains 32 stable, explicitly identified UX requirements (`UX-DR1`–`UX-DR32`), three primary journey flows, component strategy, responsive behavior, accessibility criteria, and validation expectations. The architecture names the UX specification as an explicit normative input, and `epics.md` reproduces the 32-requirement set and maps it to Epic 6 plus release evidence.

### UX ↔ PRD Alignment

The UX specification strongly reflects the PRD:

- The read-only operations-console scope aligns with FR36, FR52–FR57, Journeys 5, 6, and 8, and the operations-console accessibility NFRs.
- Workspace discovery, tenant scope, readiness, lock, dirty state, commit evidence, failures, audit, provider evidence, and freshness directly reflect PRD journeys and success measures.
- Metadata-only folder orientation preserves the PRD prohibition on file contents, raw diffs, credentials, secrets, and unauthorized-resource existence.
- Denied, redacted, inaccessible, unknown, unavailable, delayed, stale, failed, dirty, locked, ready, committed, and archived states are explicitly distinguished, supporting safe denial and incident reconstruction.
- API/SDK/CLI/MCP state and error vocabulary is treated as canonical rather than redefined by the UI.
- The FR58 addendum keeps search as authorized backend discovery and explicitly avoids content-preview or authorization-bypass UI.

UX-specific detail legitimately extends the PRD without changing product scope: named evidence components, desktop/tablet/mobile fallback behavior, exact responsive breakpoints, dialog focus behavior, layout-stability rules, zoom targets, forced-colors and color-blindness checks, and the 400 ms skeleton/2 s cancel affordance.

### UX ↔ Architecture Alignment

The architecture provides direct support for most UX requirements:

- FrontComposer Shell, Blazor Interactive Server/Blazor Server, SignalR, and Microsoft Fluent UI Blazor are the selected stack.
- The UI consumes authorized read models through the canonical client and has no domain mutation path.
- Architecture decisions F-4 through F-7 define disposition labels, explicit redaction affordances, degraded-mode guardrails, and measurable console performance/perceived-wait behavior.
- C6 provides the lifecycle state model; C9 provides sensitive-metadata classification; C2 and C8 provide freshness/read-consistency foundations.
- I-5 names an automated axe/WCAG 2.2 AA CI gate, while Epic 6 stories cover keyboard, responsive, zoom, screen-reader, focus, and no-mutation validation.
- Tenant authorization, path policy, metadata classification, and no-leakage gates support the UX's safe discovery, folder orientation, denial, and redaction states.

### Alignment Issues

1. **Production diagnostics are intentionally unpopulated pending Story 11.10.** The architecture states that `IOpsConsoleDiagnosticsReadModel` and `IWorkspaceTransitionEvidenceReadModel` are seed-backed test seams with no deployed projections. Production therefore returns safe-empty/`NotFoundSafe` results. This is secure, but it does not deliver the UX's core trust-summary, diagnostic, transition-evidence, and failure-investigation journeys. Story 11.10 is an implementation-readiness dependency for the promised console experience, not merely optional refactoring.
2. **The FR58 search facade is also fail-safe but non-functional in deployed Server composition pending Story 11.10.** Search returns zero hydrated items and indexing status reports unavailable until an EventStore-backed bridge read model is registered. The UX addendum remains safe, but the discovery capability cannot be considered delivered before that dependency closes and receives live evidence.
3. **The incident-mode event-stream exception conflicts with the PRD's blanket read-model rule.** PRD NFR51 requires operations-console views to be read-model-based, while architecture F-6 adds a direct ACL-checked event-stream view at `/_admin/incident-stream`. Its metadata-only and read-only guardrails reduce risk, but the exception needs explicit PRD/UX authorization or should be modeled as a governed read model rather than an undocumented exception to the product contract.
4. **The C6 disposition vocabulary is internally inconsistent for `ready`.** Architecture declares four allowed operator-disposition labels, but the state catalog maps `ready` to `available`, which is outside that set. Story 6.3 and the generated/tested mapper need one authoritative value before UI implementation or verification.
5. **Operator-disposition labels are architecture-added primary vocabulary.** The UX's stable requirements emphasize canonical technical states and do not explicitly identify the four disposition labels. The labels may be valuable, but their relationship to UX-DR13/UX-DR15 and cross-surface vocabulary should be made explicit so the UI does not appear to introduce a second state taxonomy.

### Warnings

- The architecture document contains older readiness prose claiming C3/C4 and other deferred items remain unresolved, while later status-reconciliation text says they are approved. Downstream UX acceptance should use the named governance artifacts and current story state, not stale narrative summaries.
- Responsive and accessibility requirements are well specified, but implementation readiness depends on the blocking full UI E2E and accessibility gates remaining present and unfiltered as required by Epics 8 and 11.
- `ux-design-directions.html` is referenced as a completed design artifact but was not part of the confirmed assessment inventory; the Markdown specification is sufficient for requirements alignment, but any visual-detail acceptance depending on the showcase should explicitly name it as supporting evidence.

## Epic Quality Review

### Structural Evidence

- The frontmatter declares `storyCount: 115`, but the document contains **116** `### Story` headings. The extra heading is likely the inserted `Story 2.8b`; the authoritative count and any sprint-status inventory need synchronization.
- All 116 story entries include an “As a / I want / So that” statement and at least one Given/When/Then block.
- The architecture specifies a sibling-module starter rather than a CLI template. Stories 1.1 and 1.2 correctly cover the initial scaffold, dependencies/configuration, and root-submodule policy.
- The greenfield baseline includes early solution setup, environment/root configuration, contract generation, and CI gates. No up-front relational-table or “create every entity first” violation was found; state and projections are generally introduced with the capabilities that need them.

### Per-Epic Compliance

| Epic / Workstream | User Value | Independent of Future Epics | Story Sizing | No Forward Dependencies | AC Quality | FR Traceability |
| --- | --- | --- | --- | --- | --- | --- |
| Epic 1 — Canonical Contract | ⚠️ Enabling consumer value, but heavily technical | ✓ | ⚠️ Several contract-group stories remain broad | ✓ | ⚠️ Mostly happy-path and artifact-existence criteria | ✓ |
| Epic 2 — Folder Access/Lifecycle | ✓ | ✓ | ✓ overall | ✓ | ⚠️ Planning ACs omit several negative/idempotency scenarios | ✓ |
| Epic 3 — Provider Readiness | ✓ | ✓ using earlier outputs | ❌ Stories 3.3 and 3.4 span entire provider lifecycles | ✓ | ⚠️ Provider failures are named generically rather than scenario-by-scenario | ✓ |
| Epic 4 — Workspace Lifecycle | ✓ | **No — depends on Epic 11 Story 11.10** | ❌ Stories 4.8 and 4.16 combine multiple independently testable capabilities | **No** | ⚠️ Broad failure and context-query criteria | ✓ |
| Epic 5 — Surface Parity | ✓ | ✓ using Epics 1–4 | ✓ overall | ✓ | ✓/⚠️ Testable, though some oracle expectations remain high-level | ✓ |
| Epic 6 — Operations Console | ✓ | **No — populated diagnostics depend on Epic 11 Story 11.10** | ⚠️ Story 6.11 is a large verification bundle | **No** | ⚠️ Safe-empty routes can pass while core journeys remain unpopulated | ✓ |
| Workstream 7 — Release Readiness | ❌ Technical governance, intentionally not a product epic | No; consumes all product work | ⚠️ Multiple cross-cutting gate bundles | N/A as a release workstream | ⚠️ Several “published” or “validated” outcomes lack exact artifact/gate assertions | NFR bridge only |
| Epic 8 — Release Closure | ❌ Release remediation, explicitly no new product value | No; depends on earlier epics | ⚠️ Story 8.5 is a multi-suite residual-red catch-all | ⚠️ Diagnostics closure remains incomplete without Story 11.10 | ⚠️ Allows accepted residuals while claiming an honestly green baseline | No new FR scope |
| Epic 9 — AppHost Alignment | ❌ Infrastructure-only | No standalone user outcome; routing is dormant until Epic 10 | ✓ within its technical scope | **No — value depends on Epic 10** | ✓/⚠️ Technically testable but not a product increment | No new FR scope |
| Epic 10 — Search Index/Facade | ✓ through FR58 | **No — deployed facade depends on Epic 11 Story 11.10** | ❌ Story 10.6 combines materialization, policy, regression, governance, and blocked live evidence | **No** | ❌ Core evidence may be carried forward instead of completed | ✓ FR58 |
| Epic 11 — Platform Refactoring | ❌ Technical refactoring/governance | Depends on prior epics and upstream shared-module changes | ❌ Multiple epic-sized stories, especially 11.2, 11.7, 11.8, 11.10, and 11.13 | ✓ internally sequenced, but externally coupled | ❌ Several ACs permit residuals or use vague completion language | Supporting NFRs only |

### 🔴 Critical Violations

1. **Forward-epic dependency: Epic 4 → Epic 11 Story 11.10.** Epic 4 claims FR46 operational transition evidence, but its own limitation states the production transition-evidence read model has no projection and is empty until Story 11.10. Epic 4 therefore does not stand alone as a completed lifecycle increment.
   - Recommendation: move the transition-evidence projection into Epic 4, or explicitly remove FR46 delivery from Epic 4 and keep the epic open until the projection is populated and verified.
2. **Forward-epic dependency: Epic 6 → Epic 11 Story 11.10.** The read-only console's seven diagnostic views are seed-backed only and empty in deployed composition. A safe-empty UI does not deliver the stated user outcome of diagnosing readiness, locks, dirty state, failures, provider status, sync status, and freshness.
   - Recommendation: make production diagnostic projections an Epic 6 prerequisite/story, then keep refactoring of their implementation seams in Epic 11 separate.
3. **Forward-epic dependency: Epic 10 → Epic 11 Story 11.10.** FR58's deployed query facade drops every candidate because the authoritative bridge read model is unavailable. Story 10.5 and Epic 10 cannot claim user-value completion while the feature returns zero hydrated results by construction.
   - Recommendation: move bridge-store relocation/registration and the live facade proof into Epic 10. Story 11.10 may later refactor the seam without owning initial delivery.
4. **Technical epics presented as epics despite no standalone product value.** Epics 8, 9, and 11 explicitly state that they add no product FR scope; Workstream 7 is correctly labeled a workstream but still appears in the same story hierarchy. Epic 9 is additionally dormant until Epic 10.
   - Recommendation: keep product epics user-outcome based; move release closure, platform alignment, governance, and refactoring into technical workstreams/enablers attached to the product increment they unblock.
5. **Story 11.10 is epic-sized.** It combines domain-service admission, event mapping, Memories publication/search wrappers, bridge-store relocation, three new EventStore-backed projection families, Server registration, REST parity, console behavior, worker behavior, and live DCP verification.
   - Recommendation: split it into independently verifiable stories for domain-service alignment, Memories publication wrapper adoption, search bridge wiring, ops-console diagnostic projections, transition-evidence projection, and live topology evidence.

### 🟠 Major Issues

1. **Provider stories are oversized.** Stories 3.3 and 3.4 each cover readiness, repository creation/binding, branch/ref behavior, file operations, commit, status, failure mapping, unknown outcomes, and—in Forgejo's case—version snapshots and nightly drift.
   - Recommendation: split by provider capability slice, with shared conformance fixtures proving each increment.
2. **Story 4.8 combines six query families and semantic-backend policy.** Tree, metadata, search, glob, bounded range reads, and semantic retrieval have distinct policies and failure modes.
   - Recommendation: split metadata/tree, pattern search/glob, bounded range read, and derived-index integration.
3. **Story 4.16 recombines risk families that should fail independently.** Sentinel redaction, path security, encoding equivalence, tenant isolation, lock concurrency, stale locks, interruption, and denial audit are separate test ownership areas.
   - Recommendation: create focused security-invariant stories/gates with one clear failure domain each.
4. **Story 10.6 is not independently completable under its own ACs.** It requires a future Story 11.10 to preserve its behavior and permits the live proof to remain blocked pending a DCP-capable lane.
   - Recommendation: keep Story 10.6 limited to the materializer and worker-boundary proof; create a separately owned live-evidence story and do not mark either complete while its required evidence is carried forward.
5. **Epic 11 contains multiple oversized consolidation stories.** Story 11.2 spans four upstream modules; 11.7 combines helper migration, double consolidation, source-scan governance, positive behavior, and CI preservation; 11.8 adopts many unrelated platform primitives; 11.13 is a final catch-all.
   - Recommendation: split by module boundary or platform primitive and give each story a narrow behavior-preservation gate.
6. **Planning ACs are not the stated authoritative ACs.** The document says as-built story files contain expanded authoritative criteria, while this readiness workflow inventories only `epics.md`. The terse planning criteria cannot substantiate current implementation readiness by themselves.
   - Recommendation: either synchronize authoritative ACs back into the planning artifact or include the implementation story files explicitly in the readiness inventory and traceability process.
7. **The plan mixes requirements, implementation status, correction history, and current backlog.** “Done” notes, dated corrections, release limitations, accepted deviations, and future stories coexist in the same epic plan, making sequence and completion claims hard to audit.
   - Recommendation: keep current requirements and dependencies in the epic plan; move historical implementation narrative to change records and maintain one machine-readable current-status source.

### 🟡 Minor Concerns

- Several ACs use non-binary language: “where supported,” “where appropriate,” “where applicable,” “or equivalent,” “major decisions,” and “or otherwise validated.” Replace each with a finite decision table or named evidence artifact.
- Story 11.10 allows live gaps to be “re-carried with evidence,” and Story 11.13 allows verification to be “satisfied or explicitly blocked.” These are useful escalation postures but invalid completion criteria; a blocked requirement must keep the story incomplete.
- Story 8.5 allows residual reds to be “explicitly accepted with rationale” while also claiming an honestly green baseline. Define whether accepted failures are excluded tests, waived release criteria, or actual green results.
- C6 maps `ready` to `available` even though the declared disposition vocabulary contains only four different labels; this can make Story 6.3's mapper acceptance ambiguous.
- The FR coverage is complete at epic level, but most mappings do not identify an owning story. Story-level FR ownership would expose the forward dependencies earlier.

### Overall Epic Quality Result

The six original product epics are generally structured around recognizable user outcomes, and all 58 FRs are claimed. However, the current plan is **not compliant with strict implementation-readiness standards** because essential behavior from Epics 4, 6, and 10 is deferred to Epic 11, several technical workstreams are modeled as epics, and multiple stories are too large or permit required evidence to remain blocked. The document should be corrected before treating its epic/story completion claims as a reliable implementation boundary.

## Summary and Recommendations

### Overall Readiness Status

**NOT READY**

The artifacts claim complete FR coverage, contain a substantial architecture, and specify the UX in unusual depth. Those strengths do not overcome the sequencing defect: production behavior required for Epic 4 transition evidence, Epic 6 diagnostics, and Epic 10/FR58 search is absent until Epic 11 Story 11.10. The current plan therefore allows earlier epics and release-closure work to appear complete while their user outcomes are safe but functionally empty.

### Critical Issues Requiring Immediate Action

1. **Restore ownership to the product epics.** Production transition evidence belongs in Epic 4, populated diagnostics belong in Epic 6, and the authoritative FR58 bridge/facade belongs in Epic 10. Do not defer initial delivery of those behaviors to a later refactoring epic.
2. **Split Story 11.10 before execution.** Its domain-service, Memories, search, diagnostics, transition-projection, parity, and live-evidence responsibilities are separate stories with different completion gates.
3. **Remove forward dependencies from completion claims.** Epics 4, 6, 8, 9, and 10 must not rely on later epics to become functional. A fail-safe empty result is valid failure behavior, not proof that the intended user capability is delivered.
4. **Resolve the direct event-stream exception.** Architecture F-6 conflicts with the PRD requirement that operations-console views are read-model based. Either authorize and constrain the exception in the PRD/UX contract or replace it with a governed projection/read-model design.
5. **Stop allowing required evidence to be carried forward as completion.** “Re-carried with evidence,” “satisfied or explicitly blocked,” and similar clauses must keep a story open rather than satisfy its acceptance criteria.

### Recommended Next Steps

1. Create focused delivery stories in Epics 4, 6, and 10 for the three production read-model gaps, including EventStore-backed projection logic, Server registration, authorization-before-observation, populated happy-path evidence, safe denial, replay/determinism, and live DCP-capable verification.
2. Recast Workstream 7 and Epics 8, 9, and 11 as technical/release workstreams or attach their enablers to the user-valued epic they unblock. Keep product-epic completion metrics limited to independently usable outcomes.
3. Split oversized stories 3.3, 3.4, 4.8, 4.16, 6.11, 8.5, 10.6, 11.2, 11.7, 11.8, 11.10, and 11.13 into narrow increments with one behavior and one evidence family each.
4. Replace conditional/vague AC language with named values, finite decision tables, exact artifacts, and binary gates. A blocked external lane must produce a blocked story state, not a passed criterion.
5. Reconcile the C6 `ready` disposition (`available` is outside the declared four-label vocabulary) and explicitly connect the disposition model to UX-DR13/UX-DR15 and cross-surface technical states.
6. Decide the F-6 incident-mode product posture and synchronize PRD, UX, architecture, epics, route authorization, redaction, accessibility, and audit evidence in one change.
7. Synchronize the declared story count (115) with the 116 headings, then update `epics.md`, sprint status, and any story inventory/gates atomically.
8. Make authoritative acceptance criteria auditable from the readiness inputs: either synchronize the expanded implementation-story ACs into the planning plan or include all referenced implementation story files in the next readiness inventory.
9. Reconcile stale status prose with authoritative governance artifacts, and add stable source identifiers for every PRD NFR so traceability does not depend on assessment-local numbering.
10. Rerun implementation readiness only after the corrected sequencing, ownership, and story inventory are saved.

### Final Note

This assessment documented **31 issue or warning entries across three categories**: six PRD clarity/readiness risks, eight UX/architecture alignment issues or warnings, and seventeen epic/story quality findings. Five epic-quality findings are critical. Address the critical sequencing and production-read-model gaps before relying on these artifacts for implementation or release acceptance.

**Assessment date:** 2026-07-14  
**Assessor:** Codex — BMAD Implementation Readiness workflow

