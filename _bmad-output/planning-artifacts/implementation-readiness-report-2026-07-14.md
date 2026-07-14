---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
status: complete
overallReadiness: NOT_READY
completedAt: '2026-07-14'
inputDocuments:
  prd:
    - prd.md
  architecture:
    - architecture.md
  epicsAndStories:
    - epics.md
  uxDesign:
    - ux-design-specification.md
excludedDocuments:
  - prd-validation-report.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-14
**Project:** folders

## Document Discovery

### Documents Selected for Assessment

- PRD: `prd.md` (75,633 bytes; modified 2026-07-07 20:50 +0200)
- Architecture: `architecture.md` (181,678 bytes; modified 2026-07-07 21:03 +0200)
- Epics and stories: `epics.md` (170,016 bytes; modified 2026-07-07 21:05 +0200)
- UX design: `ux-design-specification.md` (55,660 bytes; modified 2026-07-07 09:25 +0200)

### Discovery Notes

- No whole-versus-sharded duplicate document formats were found.
- No required document type was missing.
- `prd-validation-report.md` was classified as an auxiliary validation report and excluded as a primary assessment input.

## PRD Analysis

### Functional Requirements

- FR1: Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology.
- FR2: Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility.
- FR3: Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries.
- FR4: Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks.
- FR5: Tenant administrators can grant folder access to users, groups, roles, and delegated service agents.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.
- FR11: Authorized actors can create logical folders within a tenant.
- FR12: Authorized actors can inspect folder lifecycle and binding status.
- FR13: Authorized actors can archive folders when policy allows.
- FR14: The system can preserve audit and status evidence for archived folders.
- FR15: Platform engineers can configure supported Git provider bindings and credential references for a tenant.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass.
- FR19: Authorized actors can bind a folder to an existing repository where supported.
- FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
- FR23: Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior.
- FR24: Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid.
- FR25: Authorized actors can acquire a task-scoped workspace lock.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: The system can deny competing operations when lock ownership or workspace state makes the operation unsafe.
- FR28: The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions.
- FR29: Authorized actors can release a workspace lock when ownership and policy allow.
- FR30: The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP.
- FR31: Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable.
- FR32: Authorized actors can add, change, and remove files within a prepared and locked workspace.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
- FR34: Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads.
- FR35: The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses.
- FR36: The operations console can remain read-only and excluded from file editing or file-content browsing capabilities.
- FR37: Authorized actors can commit workspace changes for repository-backed folders.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits.
- FR39: The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID.
- FR40: The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence.
- FR41: The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers.
- FR42: The system can reject duplicate logical operations when retry identity or operation intent conflicts.
- FR43: The system can expose a canonical error taxonomy across supported surfaces.
- FR44: The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure.
- FR45: The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.
- FR46: The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure.
- FR47: API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle.
- FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.
- FR52: Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference.
- FR55: The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses.
- FR56: The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.
- FR58: Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see — security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only — without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence.

**Total FRs: 58**

### Non-Functional Requirements

**Category mapping:** NFR1–NFR10 Security and Tenant Isolation; NFR11–NFR20 Reliability, Idempotency, and Failure Visibility; NFR21–NFR31 Performance and Query Bounds; NFR32–NFR36 Scalability and Capacity; NFR37–NFR46 Integration and Contract Compatibility; NFR47–NFR55 Observability, Auditability, and Replay; NFR56–NFR61 Data Retention and Cleanup; NFR62–NFR66 Operations Console Accessibility; NFR67–NFR70 Verification Expectations.

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
NFR32: The system must support multiple tenants, folders, repositories, workspaces, and concurrent agent tasks without shared mutable state causing cross-tenant or cross-task interference.
NFR33: Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.
NFR34: Audit, timeline, and file-context projections must remain queryable as folder history grows.
NFR35: Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.
NFR36: MVP capacity targets must avoid assuming a single tenant, single repository, or single active workspace, while avoiding unsupported claims about massive scale before concrete load targets are defined.
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
NFR47: Every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
NFR48: Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
NFR49: Allowed audit metadata must be explicitly classified. File paths, commit messages, repository names, branch names, and provider error payloads must be treated as potentially sensitive metadata.
NFR50: Sensitive audit metadata such as file paths, branch names, commit messages, repository names, and provider diagnostic payloads must be classified and protected through access control, hashing, truncation, or redaction where appropriate.
NFR51: Operations-console views must be read-model–based, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata.
NFR52: Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.
NFR53: Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.
NFR54: The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.
NFR55: Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.
NFR56: Retention periods must be defined for audit metadata, workspace status, provider correlation IDs, projections, temporary working files, and cleanup records.
NFR57: Retention durations are policy decisions and must be defined before production release; the PRD requires explicit retention semantics but does not set final retention periods.
NFR58: Tenant deletion must define which records are deleted, tombstoned, retained for audit, or anonymized.
NFR59: Workspace cleanup visibility must state whether cleanup is automatic, best-effort, retryable, user-triggered, or status-only for MVP.
NFR60: Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID.
NFR61: No cleanup process may remove audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations.
NFR62: Read-only operations console flows must target WCAG 2.2 AA.
NFR63: The console must support keyboard navigation for primary diagnostic workflows.
NFR64: Status, failure, readiness, and lock indicators must not rely on color alone.
NFR65: Console screens must provide visible focus states, semantic headings, readable table structure, and sufficient contrast.
NFR66: Console text, controls, and tables must remain readable at common browser zoom levels used by operators.
NFR67: Each NFR category must have at least one automated verification path or documented manual validation path before MVP release.
NFR68: Security, tenant isolation, idempotency, provider contract, read-model determinism, and cross-surface contract compatibility NFRs must have automated tests.
NFR69: Performance, accessibility, retention, backup/recovery, and operations-console usability NFRs must have release validation evidence before MVP acceptance.
NFR70: Security verification must include dependency/package scanning, generated artifact review, and least-privilege provider credential validation.

**Total NFRs: 70**

### Additional Requirements

- **Canonical product boundary:** The MVP proves one repository-backed, tenant-scoped task lifecycle: provider readiness, repository-backed folder creation or binding, workspace preparation, task-scoped lock, governed add/change/remove operations, commit, context query, status inspection, and metadata-only audit.
- **Canonical contract and adapters:** REST is the canonical external contract and must be documented as OpenAPI v1. CLI, MCP, SDK, and the console must preserve the same lifecycle semantics, authorization, idempotency, locks, errors, correlation metadata, and audit outcomes rather than introducing separate business logic.
- **Domain ownership:** Hexalith.Tenants remains authoritative for tenant identity, lifecycle, and membership; Hexalith.EventStore supplies platform command/event/projection/query mechanics; Hexalith.Folders owns folder policy, ACLs, provider binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.
- **Content boundary:** File contents and temporary working-copy material remain outside EventStore. File contents, diffs, generated context payloads, provider tokens, credential values, secrets, and unauthorized-resource existence must not enter events, logs, traces, metrics, audit records, console views, provider diagnostics, or error responses.
- **Required provider ports:** Readiness validation, repository creation or binding, workspace preparation, governed file-operation application, Git-backed commit, provider-status query, and cleanup/expiration support where needed.
- **Minimum public contracts:** `ValidateProviderReadiness`, `CreateFolder`, `BindRepository`, `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `AddFile`, `ChangeFile`, `RemoveFile`, `CommitWorkspace`, `GetWorkspaceStatus`, `ListFolderFiles`, `SearchFolderFiles`, `ReadFileRange`, and `GetAuditTrail`.
- **Mutation contract:** Mutating commands require idempotency keys, correlation IDs, and expected-version or equivalent conflict detection. Equivalent replay must not duplicate events, provider writes, file changes, repositories, or commits; conflicting replay must produce `idempotency_conflict`.
- **Unknown outcomes:** Repository creation, file mutation, or commit operations whose provider outcome cannot be confirmed must transition to reconciliation-required status; unsafe automatic retries are prohibited.
- **Error contract:** Required fields are `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`. Stable error mapping must be equivalent across REST, CLI, MCP, and SDK.
- **Workspace concurrency:** At most one active writer is allowed per tenant/folder/repository-binding/task-workspace scope. Mutation and commit require a valid lock; read-only context/status operations do not require the mutation lock but still require tenant, ACL, and path-policy authorization.
- **Projection semantics:** Command acceptance and projected state must be distinguishable when projection lag exists; operations-console data is projection-backed, read-only, and non-authoritative.
- **File/context policy:** Tree, search, glob, metadata, and partial-read operations require tenant and folder authorization, path canonicalization, traversal rejection, include/exclude enforcement, symlink policy, binary/large-file handling, byte/range/result/time bounds, and secret-safe shaping before ranking or snippets.
- **Documentation deliverables:** OpenAPI v1 reference, getting-started guide, auth/tenant/ACL guide, workspace and lock diagram, file-operation-to-commit diagram, authorization decision flow, CLI reference, MCP reference, SDK quickstart/reference, provider testing guide, operations/audit guide, and cross-surface error catalog.
- **Quality gates:** 100% adapter command/query coverage for MVP workflows, 100% ACL decision-matrix coverage, 100% provider contract-suite pass per supported provider, zero sensitive content in observable/generated channels, deterministic read-model rebuilds, idempotency proof, tenant-isolation proof, path-security coverage, golden schemas, provider failure coverage, context-query security coverage, and sentinel-based redaction tests.
- **Approved quantitative exit criteria:** C1 sets 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, and 2 concurrent agent tasks per tenant. C2 sets 500 ms commit-to-status-read freshness under hermetic measurement. C5 repeats those capacity units and requires at least 1 lifecycle operation per second.
- **Unresolved exit criteria:** C3 retention periods by data class and C4 bounded context-query/input limits remain TBD and must be concretely measured and approved before MVP release.
- **Architecture decisions still required by the PRD:** Git-backed workspace mechanics, file-content transport, binary/large-file policy, provider-capability contract shape, projection compaction, lock lease/expiry defaults, and file-operation batch atomicity.
- **Explicit MVP exclusions:** Repair automation, brownfield migration, rich operations workflows, deep drift remediation, local-only workspace product mode, nested repositories, simultaneous multi-agent writes, console file editing/browsing/diffs, broad provider framework expansion, secret storage, and a general-purpose policy engine.
- **FR58 staged constraint:** Authorized search is in scope, but its first increment is metadata-token recall. Full body-text materialization requires explicit C9 Security and Product approval.

### PRD Completeness Assessment

The PRD is structurally strong and unusually explicit: it defines 58 numbered FRs, 70 individually extractable NFR statements, nine user journeys, MVP boundaries and non-goals, canonical surface semantics, error and idempotency contracts, quality gates, and provider-specific acceptance evidence. Requirements are generally testable and traceable by capability area.

Implementation readiness is not yet fully established from the PRD alone. C3 retention durations and C4 query/input bounds remain open release criteria. Several architecture-owned decisions are also explicitly unresolved: content transport, large/binary handling, provider capability shape, projection compaction, lock timing defaults, batch atomicity, and workspace mechanics. FR58 further contains a staged delivery distinction between metadata-derived recall and C9-gated body-text search that must remain visible in epic coverage. Finally, the PRD frontmatter says it was last edited on 2026-05-07 while the body contains approvals and implementation-status references through July 2026; this provenance mismatch should be corrected so readers can identify the authoritative revision date.

## Epic Coverage Validation

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

No FR identifiers appear in the epic requirements inventory that are absent from the PRD.

### Traceability Observations

- The epic Requirements Inventory reproduces the PRD wording exactly for all 58 FRs.
- FR31, FR43, FR47, FR50, FR51, and FR55 are intentionally mapped across multiple product epics.
- FR57 is mapped to Epic 6 in the explicit FR Coverage Map, while the Epic List also claims FR57 under Epic 3. This is not a coverage gap, but the map should name both Epic 3 (provider readiness evidence production) and Epic 6 (operator-facing evidence visibility) to eliminate ambiguity.
- FR58 is explicitly mapped to Epic 10. The document also records release limitations and later closure dependencies for that capability; those affect implementation readiness and story quality, not the existence of an epic-level implementation path.
- Epic 8 provides release-acceptance closure for several already-mapped FRs, while Epics 9 and 11 state that they add no new product FR scope.

### Coverage Statistics

- Total PRD FRs: 58
- FRs reproduced in the epic requirements inventory: 58
- FRs covered in the explicit epic coverage map: 58
- Missing FRs: 0
- Epic-only FR identifiers: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

**Found:** `ux-design-specification.md` is a completed whole UX specification. It defines 32 stable UX requirements (UX-DR1 through UX-DR32), three primary diagnostic journeys, six domain-specific evidence components, responsive behavior, and an accessibility verification strategy.

### UX ↔ PRD Alignment

- **Aligned target users and jobs:** Both artifacts focus on chatbot/agent developers, platform operators, tenant administrators, and audit reviewers who must find a workspace, prove tenant scope, diagnose lifecycle/provider/lock/commit failures, and reconstruct metadata-only evidence.
- **Aligned read-only boundary:** UX-DR11, UX-DR12, and UX-DR23 preserve the PRD prohibition on mutations, repairs, file editing, file-content browsing, raw diffs, credential reveal, and unauthorized-resource confirmation.
- **Aligned operational truth:** UX-DR4–UX-DR10 and UX-DR18–UX-DR22 directly realize PRD FR31, FR36, FR45–FR46, and FR52–FR57 through trust summaries, tenant banners, metadata-only folder orientation, diagnostic timelines, safe denied/empty states, and visibly distinct redaction.
- **Aligned accessibility:** UX-DR14, UX-DR24, and UX-DR29–UX-DR32 elaborate PRD NFR62–NFR70 with keyboard access, focus management, non-color-only state cues, semantic structure, zoom resilience, responsive fallback, automated axe checks, screen-reader review, forced-colors checks, and keyboard-only walkthroughs.
- **Aligned search scope:** The FR58 UX addendum keeps Memories search as an authorized backend discovery capability without adding content previews or bypassing Folders authorization, tenant trimming, authoritative hydration, or metadata-only redaction.
- **Additive but non-conflicting UX detail:** The UX specification adds explicit responsive breakpoints, named evidence components, desktop/tablet/mobile fallback testing, long-identifier stress cases, and loading-state behavior. These details do not contradict the PRD and are carried into epics and architecture.

### UX ↔ Architecture Alignment

- **Supported foundation:** Architecture selects a Blazor Interactive Server operations console through Hexalith.FrontComposer Shell and Microsoft Fluent UI Blazor, matching UX-DR1 and the component strategy.
- **Supported component model:** Architecture explicitly permits the same six domain evidence components named by UX: Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree, Diagnostic Timeline, Trust Matrix, and Redaction/Inaccessibility State.
- **Supported security model:** Layered authorization, tenant-context provenance, C9 sensitive-metadata classification, metadata-only projections, visible redaction affordances, and authorization-before-query execution support UX-DR4–UX-DR12 and UX-DR20–UX-DR22.
- **Supported state and evidence model:** The C6 lifecycle matrix, disposition labels, canonical state vocabulary, freshness headers, audit projection, and incident-mode event view support status, failure, stale-data, audit, and degraded-mode interactions.
- **Supported performance and accessibility:** Architecture F-7 sets a separate console budget (1.5 seconds p95 primary page load, 3 seconds p99, 5 seconds p95 degraded mode) plus a 400 ms skeleton and 2 second cancel affordance. I-5 includes the automated axe/WCAG 2.2 AA gate required by UX-DR30–UX-DR32.
- **Supported responsive posture:** Architecture adopts the UX desktop-first model and requires tablet/mobile fallback without weakening tenant authorization or read-only behavior.

### Alignment Issues

1. **HIGH — Core deployed diagnostic views are unpopulated.** Architecture states that `IOpsConsoleDiagnosticsReadModel` (seven views) and `IWorkspaceTransitionEvidenceReadModel` are seed-backed dev/test seams only. In a deployed host they return safe-empty `NotFoundSafe` results because no EventStore-backed projections exist. This prevents live fulfillment of provider readiness, lock, dirty-state, failure, sync, projection-freshness, and transition-evidence UX despite safe failure behavior. Epic 11 Story 11.10 owns authoring and wiring these projections.
2. **HIGH — Architecture readiness conclusion is stale relative to its own limitations.** The final validation still says “READY WITH MINOR GAPS” and “Critical Gaps: None,” while earlier, newer sections document production-empty ops-console read models and a fail-safe unavailable FR58 bridge. The readiness conclusion must be reconciled with these release-impacting limitations.
3. **MEDIUM — C6 disposition vocabulary is internally inconsistent.** The state catalog maps `ready` to `available` (or `degraded-but-serving`), but F-4 defines the primary disposition vocabulary as only `auto-recovering`, `awaiting-human`, `terminal-until-intervention`, and `degraded-but-serving`. `available` must either become a defined canonical disposition or be replaced so generated/tested labels cannot drift.
4. **MEDIUM — FR58 is absent from the architecture’s Requirements-to-Structure Mapping and final coverage statement.** Those sections still claim FR1–FR57 coverage, even though newer architecture sections describe FR58, Epic 10, the Folders query facade, and its current unavailable bridge. Add an explicit FR58 structure mapping and refresh the validation statement.
5. **MEDIUM — Artifact provenance is stale.** UX frontmatter still reports completion on 2026-05-11 and architecture completion metadata remains May 2026, while both bodies contain June/July corrections, Story 8–11 references, and FR58 updates. Revision metadata should identify the current authoritative date and change source.

### Warnings

- Do not represent the operations console as production-ready for live diagnosis until Epic 11 Story 11.10 supplies populated EventStore-backed diagnostics and transition-evidence projections and verifies them through the deployed topology.
- Documentation-level alignment does not prove implementation conformance. Release evidence must exercise all 32 UX-DR requirements, including full UI E2E, axe/WCAG checks, keyboard walkthroughs, screen-reader checks, zoom/responsive coverage, long-identifier layouts, safe-empty/denied/redacted distinctions, and the no-mutation boundary.
- The FR58 UI boundary remains metadata-only. Any future body-text search or content preview requires explicit C9 Security and Product approval and corresponding UX/architecture updates.

## Epic Quality Review

### Review Scope and Evidence

All 11 epics/workstreams and all story headings in `epics.md` were reviewed against user-value focus, epic independence, within-epic sequencing, forward dependencies, story sizing, BDD acceptance criteria, starter/setup requirements, and FR traceability.

The document frontmatter reports `storyCount: 115`, but the document contains **116** `### Story` headings. FR traceability remains 58/58; the defects below concern delivery structure and implementation readiness.

### Epic-by-Epic Compliance

| Epic / Workstream | User-value epic | Independent from future work | Story sizing | AC quality | Result |
| --- | --- | --- | --- | --- | --- |
| Epic 1 — Bootstrap Canonical Contract | ❌ Primarily scaffold, contract authoring, fixtures, code generation, and CI gates | ✓ Sequenced internally | ❌ Several multi-system enabling stories | △ Mostly existence/gate criteria | **Violation: technical epic** |
| Epic 2 — Tenant-Scoped Folder Access | ✓ Clear tenant-administrator value | ❌ Story 2.8 required later Story 2.8b | △ Generally bounded | △ Important denial/idempotency cases deferred | **Violation: forward dependency** |
| Epic 3 — Provider Readiness | ✓ Clear provider-readiness and binding value | △ Story 3.6 requests provisioning but does not complete the user outcome | ❌ Stories 3.3 and 3.4 span whole provider lifecycles | △ Broad capability assertions | **Major issues** |
| Epic 4 — Workspace Task Lifecycle | ✓ Core user outcome | ❌ Relies on later Stories 4.11, 4.13, 4.14 and Epic 11 Story 11.10 | △ Several cross-cutting/test stories | △ Earlier stories omit later-owned behavior | **Critical sequencing defect** |
| Epic 5 — Cross-Surface Parity | ✓ Integration-consumer value | ✓ Depends on prior epics only | ❌ Stories 5.2 and 5.3 implement entire adapters | △ High-level rather than operation-specific | **Major sizing issue** |
| Epic 6 — Trust Console | ✓ Strong operator/auditor value | ❌ Production diagnostics depend on Epic 11 Story 11.10 | △ Mostly screen-sized; Story 6.5 is documentation-only | △ Many state/error cases spread across later verification | **Critical forward dependency** |
| Workstream 7 — Release Readiness | ❌ Release engineering/governance, not a product-value epic | ✓ Consumes prior work by design | △ Several broad gate/documentation stories | △ Evidence-oriented | **Violation: technical workstream in epic model** |
| Epic 8 — Release Acceptance Closure | ❌ Closure/remediation milestone | ✓ Consumes prior work by design | ❌ Route buckets and residual-baseline stories are batch remediation | △ Count-based completion criteria | **Violation: technical closure epic** |
| Epic 9 — AppHost Platform Alignment | ❌ Infrastructure alignment only; explicitly no new FR scope | ❌ Routing is dormant until Epic 10 | △ Three bounded infrastructure changes | △ Testable but technical | **Critical technical/forward dependency** |
| Epic 10 — Authorized Search Facade | ✓ FR58 user capability | ❌ Real indexing depends on later Story 10.6 and live facade on Epic 11 Story 11.10 | ❌ Story 10.6 combines behavior, governance, tests, sequencing, and follow-up policy | △ Contradictory dependency criteria | **Critical sequencing defect** |
| Epic 11 — Platform Refactoring | ❌ Technical refactoring/governance closure | △ Sequential internally, but requires upstream shared-module delivery | ❌ Stories 11.3, 11.7, 11.10, 11.11, and 11.13 are oversized | △ Large catch-all AC sets | **Critical technical/sizing defect** |

### 🔴 Critical Violations

#### 1. Technical epics are modeled as product delivery

Epic 1, Workstream 7, Epic 8, Epic 9, and Epic 11 primarily deliver scaffolding, CI/governance, release closure, topology alignment, or refactoring. They may be necessary engineering work, but they do not satisfy the workflow’s epic test: a user can benefit from the epic as a standalone capability.

**Recommendation:** Move these into explicitly non-product enabling plans, release checklists, or engineering workstreams linked to the value epics they enable. Keep only externally meaningful capability increments in the product epic sequence. Preserve Story 1.1 as the required starter/scaffold setup task, but do not present the surrounding technical foundation as a user-value epic.

#### 2. Story 2.8 was not independently completable

The document explicitly states Story 2.8 “could not reach done until” later Story 2.8b wired the production `/process` path. This is a direct forward-story dependency and proves the original archive story was not a complete vertical slice.

**Recommendation:** Merge 2.8b into Story 2.8’s definition of done, or rewrite 2.8 as a contract/domain precursor that does not claim usable archive behavior. Apply the same no-mock production-path acceptance requirement to every mutation story when first authored.

#### 3. Epic 4 implements cross-cutting requirements after stories that already need them

- Story 4.2 claims idempotent workspace preparation, Story 4.6 claims retry-safe writes, and Story 4.7 claims idempotent removal, but idempotency/correlation propagation is not implemented until Story 4.11.
- Stories 4.2–4.12 require canonical failures and operational evidence, but Story 4.13 owns that behavior later.
- Story 4.8 already requires denial audit evidence and multiple prior stories claim auditable outcomes, but Story 4.14 implements audit/observability later.
- Epic 4’s FR46 transition-evidence read model remains empty in deployment until Epic 11 Story 11.10.

**Recommendation:** Establish idempotency/correlation, canonical error projection, and metadata-only audit infrastructure before the first lifecycle mutation. Each lifecycle story must then include its own accepted, denied, replay, conflict, known-failure, unknown-outcome, persistence-path, and audit assertions. Bring production transition-evidence projection into Epic 4 or remove the claim that Epic 4 independently delivers FR46.

#### 4. Epic 6 cannot deliver its core console outcome without future Epic 11

The epic explicitly states that all seven `IOpsConsoleDiagnosticsReadModel` views are empty in deployed hosts and that Epic 11 Story 11.10 must author and wire their EventStore-backed projections. Stories 6.6–6.10 therefore build screens over safe-empty data rather than a usable live diagnostic capability.

**Recommendation:** Move production projection authoring/wiring ahead of the diagnostic page stories and include populated deployed-host acceptance evidence in Epic 6. Safe-empty behavior is a security fallback, not completion of the user-value epic.

#### 5. Epic 9 is intentionally dormant until a future epic

Story 9.3 says the Folders→Memories routing is “dormant until Epic 10,” and the epic states end-to-end indexing is gated on the Epic 10 producer. This violates epic independence.

**Recommendation:** Treat Epic 9 as an enabling infrastructure task inside the FR58 delivery plan, or combine it with the first producer slice so the resulting epic produces an observable user capability.

#### 6. Epic 10 contains two forward-delivery gaps

- Stories 10.3–10.5 are described as done even though the default materializer prevents real index population until later Story 10.6.
- Story 10.4 expects a live searchable hit, which cannot be produced by the placeholder materializer it precedes.
- The deployed facade returns zero results / `ReadModelUnavailable` until Epic 11 Story 11.10 wires the bridge read model.
- Story 10.1 says no Server project may depend on Memories, while Story 10.5 and architecture later introduce a Server-only Memories dependency exception.

**Recommendation:** Re-sequence 10.6 before producer and routing proof; update 10.1’s dependency boundary before implementation; bring the bridge read model into the same FR58 epic; and define one end-to-end vertical slice that produces, indexes, authorizes, hydrates, redacts, and returns a real result before calling the epic complete.

#### 7. Epic 11 Story 11.10 is epic-sized

Story 11.10 combines domain-service admission refactoring, subscription mapping, Memories publication/search wrappers, bridge-store relocation, seven ops-console diagnostic projections, transition-evidence projection authoring, Server composition, REST/console parity, lifecycle determinism preservation, and live DCP evidence.

**Recommendation:** Split it into at least four independently verifiable stories: EventStore admission/mapping adoption; Memories publication/search seam adoption; bridge read-model relocation and facade wiring; ops-console/transition projection authoring and deployed evidence.

### 🟠 Major Issues

#### 1. Oversized adapter and provider stories

Stories 3.3, 3.4, 5.2, and 5.3 each cover a complete provider or adapter surface across readiness, repository, file, commit, status, failure, and parity behavior. Stories 8.1 and 8.2 batch eight and seven routes respectively. Stories 11.3, 11.7, 11.11, and 11.13 combine multiple unrelated change families and gates.

**Recommendation:** Split by user-visible capability group or independently deployable behavior, with each story owning its route/SDK/adapter implementation, negative paths, authorization, idempotency where applicable, audit evidence, and focused tests.

#### 2. Story 3.6 stops at a provisioning request

The story promises creation of a new repository-backed folder, but its AC only proves that provisioning was requested and state “moves toward ready.” No completion, provider-side repository evidence, terminal failure, or reconciliation outcome is required.

**Recommendation:** Either rename it as a provisioning-request precursor or extend it to a complete asynchronous vertical slice with operation identity, terminal status, repository-binding evidence, idempotent replay, known failure, and unknown-outcome reconciliation.

#### 3. Planning ACs are not self-contained

The document warns that its ACs are terse and that authoritative expanded ACs live in separate implementation-artifact story files. That makes the selected `epics.md` artifact insufficient by itself for implementation readiness and introduces two competing story authorities.

**Recommendation:** Either synchronize authoritative ACs back into `epics.md`, or formally inventory the implementation-artifact story set as the canonical story source and validate it for duplicates/completeness. Do not rely on a pointer to unvalidated files.

#### 4. Negative and boundary criteria are systematically deferred

Representative stories with primarily happy-path or broad criteria include 2.3–2.5, 3.1, 3.5–3.9, 4.2–4.3, 4.6–4.10, 5.2–5.3, and 6.6–6.10. Key denial, stale-read, conflicting-idempotency, authorization-revocation, provider-known-failure, projection-unavailable, accessibility-state, and metadata-leak cases are often deferred to later validation stories.

**Recommendation:** Put the relevant failure and boundary behavior in the story that introduces the behavior. Later conformance stories may add broad regression evidence, but must not complete an earlier story’s missing definition of done.

#### 5. Cross-repository prerequisite work is bundled into Story 11.2

Story 11.2 requires APIs to land in Commons, EventStore, FrontComposer, and Memories plus Folders submodule pins. This is multi-repository coordination, not one independently completable Folders story.

**Recommendation:** Track one prerequisite story/issue per owning repository with explicit version/SHA exit criteria, then make the Folders adoption stories depend only on released/pinned prerequisites.

### 🟡 Minor Concerns

- `storyCount: 115` is stale; 116 story headings exist.
- Story identifier `2.8b` breaks the otherwise numeric sequence and commonly confuses sprint tooling, sorting, and traceability. Renumber it or merge it into 2.8.
- “Semantic-indexing” terminology remains in Epic 10 even though the document says the mechanism is syntactic/BM25 search. Retained historical naming increases implementation and stakeholder ambiguity.
- Several acceptance sections use long chains of `And` clauses or catch-all verification rather than one scenario per behavior. Split multi-scenario ACs into explicit Given/When/Then cases.
- Architecture-required project scaffolding is correctly present in Story 1.1, and root configuration/CI setup occurs early. No up-front relational database/table-creation violation was found; state/projection artifacts are generally introduced near their capability.

### Required Structural Remediation

1. Reclassify technical/release/refactoring epics as enabling workstreams outside the product epic sequence.
2. Remove every forward dependency by moving required infrastructure and projections into the first consuming vertical slice.
3. Re-sequence Epic 4 cross-cutting foundations before lifecycle behaviors.
4. Rebuild Epic 6 around populated production read models, not seed-only seams.
5. Rebuild FR58 as one independently demonstrable end-to-end capability across topology, producer, materializer, bridge, facade, authorization, and redaction.
6. Split oversized provider, adapter, route-bucket, and Epic 11 stories.
7. Establish one canonical story/AC source and correct the story count/identifier sequence.
8. Require each behavior story to own its negative paths, no-mock production-path evidence where mutating, and metadata-only audit assertions.

## Summary and Recommendations

### Overall Readiness Status

# NOT READY

The planning set has **complete FR enumeration and 100% epic-level FR mapping**, but it is not implementation-ready as a coherent, independently executable backlog. Several claimed capabilities are safe but non-functional in deployed hosts, multiple stories and epics depend on future work, technical milestones are modeled as product epics, and the authoritative acceptance-criteria source is ambiguous.

This status does not mean the underlying implementation has no value. It means the selected PRD, architecture, UX, and epic/story artifacts do not yet support a reliable “start the next story and finish it independently” implementation contract.

### Critical Issues Requiring Immediate Action

1. **Production diagnostic evidence is missing.** Epic 4 transition evidence and Epic 6’s seven diagnostic views remain seed-backed and empty in deployed hosts until Epic 11 Story 11.10.
2. **Forward dependencies invalidate story completion claims.** Story 2.8 required 2.8b; Epic 4 behavior depends on later 4.11/4.13/4.14; Epic 9 is dormant until Epic 10; Epic 10 depends on 10.6 and Epic 11.10.
3. **The product epic sequence contains technical milestones.** Epic 1, Workstream 7, Epic 8, Epic 9, and Epic 11 are scaffolding, governance, release closure, infrastructure alignment, or refactoring rather than standalone user-value increments.
4. **FR58 is mapped but not delivered as one coherent vertical slice.** Real population follows later materializer work, and the deployed query facade remains unavailable until a future bridge-read-model change.
5. **Story authority is ambiguous.** `epics.md` calls its ACs terse and points to separate implementation-artifact story files as authoritative, but those files were not part of the confirmed canonical input set. The frontmatter count is also stale (115 declared versus 116 story headings).
6. **Artifact conclusions contradict newer content.** PRD revision metadata and C3/C4 text are stale; architecture still concludes “READY WITH MINOR GAPS / no Critical Gaps” despite newer production limitations; UX and architecture revision dates do not identify June/July amendments.

### Recommended Next Steps

1. **Freeze one authoritative planning baseline.** Choose the canonical PRD, architecture, UX, and story/AC sources; update frontmatter revision dates, input-document lists, story counts, and change provenance.
2. **Reconcile requirement and architecture truth.** Update the PRD with approved C3/C4 outcomes and resolved architecture decisions; add FR58 to the architecture requirements-to-structure map; make the FR57 coverage map name both producing and presenting epics; refresh the architecture readiness conclusion.
3. **Move production read models into their consuming capability epics.** Split Epic 11 Story 11.10 and move transition-evidence projection delivery to Epic 4, ops-console diagnostic projections to Epic 6, and the search bridge/facade read model to Epic 10.
4. **Re-sequence Epic 4 foundations.** Implement idempotency/correlation, canonical errors, and metadata-only audit before the first lifecycle behavior that claims them. Make each mutation story prove the real REST → gateway → processor → gate → persistence path without mocking the gateway.
5. **Rebuild FR58 as an end-to-end slice.** Sequence topology, materializer, producer, removal/archive behavior, bridge projection, authorized facade, authoritative hydration, metadata-only redaction, and live result evidence before declaring the capability complete.
6. **Reclassify technical work.** Move scaffold, CI/governance, release closure, AppHost alignment, and platform refactoring into enabling plans or release workstreams linked to product epics rather than counting them as user-value epics.
7. **Split oversized stories.** Start with 3.3, 3.4, 5.2, 5.3, 8.1, 8.2, 11.2, 11.3, 11.7, 11.10, 11.11, and 11.13. Each resulting story should deliver one independently testable behavior and own its negative paths.
8. **Make acceptance criteria self-contained.** Synchronize full authoritative ACs into the canonical story source. Include authorization denial, stale/unavailable reads, equivalent/conflicting replay, known/unknown provider outcomes, metadata leakage, audit evidence, accessibility states, and terminal status in the story that introduces each behavior.
9. **Resolve UX state vocabulary.** Correct the C6 `ready → available` disposition mismatch or formally add `available` to the canonical disposition set, then align mapper tests and UX copy.
10. **Rerun implementation readiness** after artifact correction and backlog restructuring. Do not use the current 100% FR mapping as a substitute for this gate.

### Finding Summary

This assessment tracks **27 issues across four broad categories**:

- Requirements, traceability, and artifact drift: 6 findings.
- UX-to-architecture alignment: 5 findings.
- Epic/story structure: 7 critical violations.
- Story quality and document hygiene: 5 major issues and 4 minor concerns.

### Assessment Metadata

- Assessment date: 2026-07-14
- Assessor: Codex, acting as Product Manager and requirements-traceability reviewer
- Canonical inputs: `prd.md`, `architecture.md`, `epics.md`, `ux-design-specification.md`
- Readiness decision: `NOT READY`

### Final Note

Address the critical issues before using this backlog as the implementation contract. The strongest foundation is already present: requirements are comprehensive, the UX intent is clear, and every FR has an epic mapping. The remaining work is structural—make the mapped capabilities independently deliverable, make deployed evidence real, and make the artifacts agree about what is authoritative and complete.
