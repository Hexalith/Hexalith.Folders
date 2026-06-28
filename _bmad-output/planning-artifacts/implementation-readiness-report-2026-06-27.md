---
project: Folders
date: 2026-06-27
workflow: bmad-check-implementation-readiness
documentInventory:
  prd:
    selected:
      - path: _bmad-output/planning-artifacts/prd.md
        type: whole
        sizeBytes: 74419
        modified: "2026-06-24 00:43"
    supporting:
      - path: _bmad-output/planning-artifacts/prd-validation-report.md
        type: validation-report
        sizeBytes: 57462
        modified: "2026-06-26 17:32"
  architecture:
    selected:
      - path: _bmad-output/planning-artifacts/architecture.md
        type: whole
        sizeBytes: 172999
        modified: "2026-06-26 17:34"
  epics:
    selected:
      - path: _bmad-output/planning-artifacts/epics.md
        type: whole
        sizeBytes: 144833
        modified: "2026-06-26 17:32"
  ux:
    selected:
      - path: _bmad-output/planning-artifacts/ux-design-specification.md
        type: whole
        sizeBytes: 55126
        modified: "2026-05-30 09:11"
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
status: complete
completedAt: 2026-06-27
---

# Implementation Readiness Assessment Report

**Date:** 2026-06-27
**Project:** Folders

## Step 1: Document Discovery

### PRD Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/prd.md` (74,419 bytes, modified 2026-06-24 00:43)
- `_bmad-output/planning-artifacts/prd-validation-report.md` (57,462 bytes, modified 2026-06-26 17:32)

**Sharded Documents:**
- None found

**Selected for assessment:**
- `_bmad-output/planning-artifacts/prd.md`

**Supporting evidence:**
- `_bmad-output/planning-artifacts/prd-validation-report.md`

### Architecture Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/architecture.md` (172,999 bytes, modified 2026-06-26 17:34)

**Sharded Documents:**
- None found

**Selected for assessment:**
- `_bmad-output/planning-artifacts/architecture.md`

### Epics & Stories Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/epics.md` (144,833 bytes, modified 2026-06-26 17:32)

**Sharded Documents:**
- None found

**Selected for assessment:**
- `_bmad-output/planning-artifacts/epics.md`

### UX Design Files Found

**Whole Documents:**
- `_bmad-output/planning-artifacts/ux-design-specification.md` (55,126 bytes, modified 2026-05-30 09:11)

**Sharded Documents:**
- None found

**Selected for assessment:**
- `_bmad-output/planning-artifacts/ux-design-specification.md`

### Discovery Issues

- No critical whole-vs-sharded duplicate conflicts found.
- No required document category is missing.
- `prd-validation-report.md` matched the PRD search pattern but was classified as supporting validation evidence, not the source PRD.

## Step 2: PRD Analysis

### Functional Requirements

FR1: Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology.

FR2: Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility.

FR3: Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries.

FR4: Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks.

FR5: Tenant administrators can grant folder access to users, groups, roles, and delegated service agents.

FR6: Authorized actors can inspect effective permissions for a folder or task context.

FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.

FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.

FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.

FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

FR11: Authorized actors can create logical folders within a tenant.

FR12: Authorized actors can inspect folder lifecycle and binding status.

FR13: Authorized actors can archive folders when policy allows.

FR14: The system can preserve audit and status evidence for archived folders.

FR15: Platform engineers can configure supported Git provider bindings and credential references for a tenant.

FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.

FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.

FR18: Authorized actors can create a repository-backed folder when readiness checks pass.

FR19: Authorized actors can bind a folder to an existing repository where supported.

FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.

FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.

FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.

FR23: Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior.

FR24: Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid.

FR25: Authorized actors can acquire a task-scoped workspace lock.

FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.

FR27: The system can deny competing operations when lock ownership or workspace state makes the operation unsafe.

FR28: The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions.

FR29: Authorized actors can release a workspace lock when ownership and policy allow.

FR30: The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP.

FR31: Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable.

FR32: Authorized actors can add, change, and remove files within a prepared and locked workspace.

FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.

FR34: Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads.

FR35: The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses.

FR36: The operations console can remain read-only and excluded from file editing or file-content browsing capabilities.

FR37: Authorized actors can commit workspace changes for repository-backed folders.

FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits.

FR39: The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID.

FR40: The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence.

FR41: The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers.

FR42: The system can reject duplicate logical operations when retry identity or operation intent conflicts.

FR43: The system can expose a canonical error taxonomy across supported surfaces.

FR44: The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure.

FR45: The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.

FR46: The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure.

FR47: API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle.

FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.

FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.

FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.

FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

FR52: Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status.

FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.

FR54: Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference.

FR55: The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses.

FR56: The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events.

FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

FR58: Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see, security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only, without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence.

Total FRs: 58

### Non-Functional Requirements

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

NFR51: Operations-console views must be read-model-based, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata.

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

Total NFRs: 70

### Additional Requirements

AR1: REST API is the canonical external contract and must be documented through an OpenAPI `v1` contract.

AR2: CLI, MCP server, SDK, and console are adapters or projections over the canonical contract and must preserve workspace states, authorization checks, idempotency rules, lock behavior, structured error categories, correlation metadata, and audit metadata.

AR3: Read-only operations console must consume projections only and must not expose mutation paths, credential material, file contents, hidden repair actions, or direct filesystem browsing.

AR4: MVP is repository-backed first. Local-only folders, local-first promotion, brownfield folder adoption, and repair-oriented cache rebuild workflows are excluded from MVP unless needed as internal mechanics for Git-backed workspace preparation.

AR5: Required command/query DTOs include `ValidateProviderReadiness`, `CreateFolder`, `BindRepository`, `PrepareWorkspace`, `LockWorkspace`, `ReleaseWorkspaceLock`, `AddFile`, `ChangeFile`, `RemoveFile`, `CommitWorkspace`, `GetWorkspaceStatus`, `ListFolderFiles`, `SearchFolderFiles`, `ReadFileRange`, and `GetAuditTrail`.

AR6: Mutating commands must support idempotency keys, correlation IDs, and expected version or conflict-detection semantics.

AR7: Projection DTOs must be read models, not direct event mirrors.

AR8: File content transport must be explicit before implementation. MVP must define whether content is accepted as inline text, base64 binary, stream/upload reference, or external content reference.

AR9: Metadata-only events may include path, content hash, size, media type, content reference ID, operation ID, provider reference, actor, timestamps, and commit ID, but not file contents.

AR10: Required error fields are `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details`.

AR11: Errors must be stable, machine-readable, and mapped consistently across REST API, CLI, MCP, and SDK.

AR12: Provider and workspace failures must distinguish known failure from unknown outcome. Repository creation, file mutation, or commit status that cannot be confirmed after timeout/interruption must expose reconciliation-required status instead of unsafe retry.

AR13: Workspace lock semantics must define owner, lease duration, renewal, expiry, release, stale lock behavior, reentrant behavior, and conflict behavior.

AR14: The MVP lock boundary is one active writer per tenant, folder, repository binding, and task workspace scope.

AR15: Governed file mutations and commits require a valid mutation lock. Context queries and read-only status inspection do not require the mutation lock, but still require tenant, folder ACL, and path-policy authorization.

AR16: Query/status responses must distinguish accepted command state from projected state when projection lag exists.

AR17: Documentation deliverables must include OpenAPI reference, getting started guide, auth/tenant/folder ACL guide, lifecycle and decision-flow diagrams, CLI/MCP/SDK references, provider integration guide, operations console/audit guide, and error catalog.

AR18: Contract and quality gates require complete adapter parity coverage for MVP workflow commands, ACL matrix coverage, provider contract suite pass for each supported provider, metadata-only artifact scanning, idempotency tests, tenant isolation tests, path security tests, read-model determinism, golden schema tests, provider failure tests, context-query security tests, and redaction tests.

AR19: MVP acceptance evidence requires one end-to-end parity scenario through REST, CLI, MCP, and SDK; GitHub and Forgejo automated provider contract tests; cross-tenant negative tests; failure-mode tests; adapter parity evidence; tenant isolation evidence; tenant-bound replay-safe provider callbacks/webhooks; and large change-set projection evidence.

AR20: Deferred quantitative targets must be resolved by architecture: C1 capacity is approved as 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, and 2 concurrent agent tasks per tenant; C2 freshness is approved as 500 ms commit-to-status-read lag; C3 retention durations remain TBD; C4 bounded MVP input limits remain TBD; C5 scalability quantifiers are approved as 4 tenant units, 2 folder units per tenant, 2 workspace units per tenant, 2 task units per tenant, and at least 1 lifecycle operation per second.

### PRD Completeness Assessment

The PRD is strong for product intent, user journeys, functional coverage, tenant isolation, metadata-only audit posture, cross-surface parity, provider readiness, workspace lifecycle, and quality-gate expectations.

Known readiness gaps remain where the PRD intentionally delegates detail to architecture: retention durations (C3), bounded query/input limits (C4), file content transport, large-file and binary policy, provider capability contract shape, projection compaction strategy, lock lease/expiry defaults, and file-operation batch atomicity. These are not necessarily PRD defects, but implementation readiness depends on architecture and epics closing them with explicit decisions and acceptance evidence.

## Step 3: Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document contains a formal "FR Coverage Map" for FR1 through FR57. It also contains an Epic 10 entry for "New FR for an authorized context-query/RAG facade (Phase 2; to be added to PRD when scheduled)", but it does not number that work as FR58 even though the PRD now contains FR58.

- FR1: Epic 1
- FR2: Epic 1; closure validation in Epic 8
- FR3: Epic 1
- FR4: Epic 2
- FR5: Epic 2; closure validation in Epic 8
- FR6: Epic 2; closure validation in Epic 8
- FR7: Epic 3
- FR8: Epic 2
- FR9: Epic 2
- FR10: Epic 2
- FR11: Epic 2; closure validation in Epic 8
- FR12: Epic 2
- FR13: Epic 2
- FR14: Epic 2
- FR15: Epic 3; closure validation in Epic 8
- FR16: Epic 3
- FR17: Epic 3
- FR18: Epic 3
- FR19: Epic 3
- FR20: Epic 3
- FR21: Epic 3
- FR22: Epic 3
- FR23: Epic 3
- FR24: Epic 4
- FR25: Epic 4
- FR26: Epic 4; closure validation in Epic 8
- FR27: Epic 4
- FR28: Epic 4; closure validation in Epic 8
- FR29: Epic 4
- FR30: Epic 4
- FR31: Epic 4 and Epic 6
- FR32: Epic 4
- FR33: Epic 4
- FR34: Epic 4
- FR35: Epic 4
- FR36: Epic 6
- FR37: Epic 4
- FR38: Epic 4
- FR39: Epic 4; closure validation in Epic 8
- FR40: Epic 4
- FR41: Epic 4
- FR42: Epic 4
- FR43: Epic 1 and Epic 4
- FR44: Epic 4
- FR45: Epic 4 and Epic 6
- FR46: Epic 4 and Epic 6; closure validation in Epic 8
- FR47: Epic 1 and Epic 5
- FR48: Epic 5
- FR49: Epic 5
- FR50: Epic 1 and Epic 5
- FR51: Epic 1 and Epic 5
- FR52: Epic 6; closure validation in Epic 8
- FR53: Epic 6
- FR54: Epic 6
- FR55: Epic 4 and Epic 6
- FR56: Epic 6
- FR57: Epic 3 and Epic 6
- FR58: Not formally mapped by number. Semantically described by Epic 10 as an authorized context-query/RAG/search facade, but the epics document still labels it "New FR ... to be added to PRD when scheduled".

Total FRs formally mapped in epics: 57

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --------- | --------------- | ------------- | ------ |
| FR1 | Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology. | Epic 1 | Covered |
| FR2 | Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility. | Epic 1; Epic 8 closure validation | Covered |
| FR3 | Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries. | Epic 1 | Covered |
| FR4 | Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks. | Epic 2 | Covered |
| FR5 | Tenant administrators can grant folder access to users, groups, roles, and delegated service agents. | Epic 2; Epic 8 closure validation | Covered |
| FR6 | Authorized actors can inspect effective permissions for a folder or task context. | Epic 2; Epic 8 closure validation | Covered |
| FR7 | Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks. | Epic 3 | Covered |
| FR8 | The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope. | Epic 2 | Covered |
| FR9 | The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information. | Epic 2 | Covered |
| FR10 | The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details. | Epic 2 | Covered |
| FR11 | Authorized actors can create logical folders within a tenant. | Epic 2; Epic 8 closure validation | Covered |
| FR12 | Authorized actors can inspect folder lifecycle and binding status. | Epic 2 | Covered |
| FR13 | Authorized actors can archive folders when policy allows. | Epic 2 | Covered |
| FR14 | The system can preserve audit and status evidence for archived folders. | Epic 2 | Covered |
| FR15 | Platform engineers can configure supported Git provider bindings and credential references for a tenant. | Epic 3; Epic 8 closure validation | Covered |
| FR16 | Authorized actors can validate provider readiness before repository-backed folder creation or binding. | Epic 3 | Covered |
| FR17 | The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID. | Epic 3 | Covered |
| FR18 | Authorized actors can create a repository-backed folder when readiness checks pass. | Epic 3 | Covered |
| FR19 | Authorized actors can bind a folder to an existing repository where supported. | Epic 3 | Covered |
| FR20 | Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks. | Epic 3 | Covered |
| FR21 | The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets. | Epic 3 | Covered |
| FR22 | The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle. | Epic 3 | Covered |
| FR23 | Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior. | Epic 3 | Covered |
| FR24 | Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid. | Epic 4 | Covered |
| FR25 | Authorized actors can acquire a task-scoped workspace lock. | Epic 4 | Covered |
| FR26 | Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata. | Epic 4; Epic 8 closure validation | Covered |
| FR27 | The system can deny competing operations when lock ownership or workspace state makes the operation unsafe. | Epic 4 | Covered |
| FR28 | The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions. | Epic 4; Epic 8 closure validation | Covered |
| FR29 | Authorized actors can release a workspace lock when ownership and policy allow. | Epic 4 | Covered |
| FR30 | The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP. | Epic 4 | Covered |
| FR31 | Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable. | Epic 4 and Epic 6 | Covered |
| FR32 | Authorized actors can add, change, and remove files within a prepared and locked workspace. | Epic 4 | Covered |
| FR33 | The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy. | Epic 4 | Covered |
| FR34 | Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads. | Epic 4 | Covered |
| FR35 | The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses. | Epic 4 | Covered |
| FR36 | The operations console can remain read-only and excluded from file editing or file-content browsing capabilities. | Epic 6 | Covered |
| FR37 | Authorized actors can commit workspace changes for repository-backed folders. | Epic 4 | Covered |
| FR38 | Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits. | Epic 4 | Covered |
| FR39 | The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID. | Epic 4; Epic 8 closure validation | Covered |
| FR40 | The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence. | Epic 4 | Covered |
| FR41 | The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers. | Epic 4 | Covered |
| FR42 | The system can reject duplicate logical operations when retry identity or operation intent conflicts. | Epic 4 | Covered |
| FR43 | The system can expose a canonical error taxonomy across supported surfaces. | Epic 1 and Epic 4 | Covered |
| FR44 | The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure. | Epic 4 | Covered |
| FR45 | The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`. | Epic 4 and Epic 6 | Covered |
| FR46 | The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure. | Epic 4 and Epic 6; Epic 8 closure validation | Covered |
| FR47 | API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle. | Epic 1 and Epic 5 | Covered |
| FR48 | CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5 | Covered |
| FR49 | MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5 | Covered |
| FR50 | SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 1 and Epic 5 | Covered |
| FR51 | The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior. | Epic 1 and Epic 5 | Covered |
| FR52 | Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status. | Epic 6; Epic 8 closure validation | Covered |
| FR53 | Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations. | Epic 6 | Covered |
| FR54 | Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference. | Epic 6 | Covered |
| FR55 | The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses. | Epic 4 and Epic 6 | Covered |
| FR56 | The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events. | Epic 6 | Covered |
| FR57 | Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness. | Epic 3 and Epic 6 | Covered |
| FR58 | Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see, security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only, without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence. | Not formally mapped by number. Semantic seed exists in Epic 10, but the epics document calls it an unnumbered "New FR" and says it still needs to be added to PRD. | Missing formal mapping |

### Missing Requirements

#### Critical Missing Formal FR Coverage

FR58: Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see, security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only, without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence.

- Impact: The PRD has promoted the authorized Memories-backed search facade to a numbered requirement, but the epics document still treats it as an unnumbered future/new FR. That breaks formal traceability and could let security-trimmed search authorization, metadata-only redaction, or cross-surface support fall outside release or phase planning.
- Recommendation: Update the epics Requirements Inventory, FR Coverage Map, Epic 10 "FRs covered" line, and relevant Story 10.5 wording to explicitly map Epic 10 to FR58. If FR58 is intended for Phase 2 rather than MVP, mark that explicitly in PRD and epics so release readiness does not interpret it as MVP scope.

### Epics FRs Not In PRD

No numbered epic FRs appear outside the PRD FR1-FR58 set. The unnumbered Epic 10 "New FR" appears to be stale wording for PRD FR58, not a separate extra requirement.

### Coverage Statistics

- Total PRD FRs: 58
- FRs formally covered in epics by number: 57
- FRs with semantic but not formal coverage: 1
- FRs missing any epic evidence: 0
- Formal coverage percentage: 98.3%
- Semantic coverage percentage if Epic 10 is accepted as FR58 coverage: 100%

## Step 4: UX Alignment Assessment

### UX Document Status

Found:

- Primary UX design specification: `_bmad-output/planning-artifacts/ux-design-specification.md` (whole document, 787 lines).
- Supporting implementation UX bridge: `docs/ux/ops-console-wireflows.md` (887 lines), referenced by Epic 6 as the reviewed contract for Stories 6.6-6.10.

No sharded UX document was found under `_bmad-output/planning-artifacts/*ux*/index.md`.

### UX to PRD Alignment

The primary UX specification aligns strongly with the PRD's MVP console scope:

- PRD defines the read-only operations console as diagnostic and metadata-only; UX-DR11, UX-DR12, UX-DR23, and the wireflow scope preserve no mutation controls, no repair actions, no file editing, no raw diff display, no credential reveal, and no unrestricted file browsing.
- PRD requires provider readiness, workspace readiness, lock state, dirty state, commit state, failed operation state, and provider/sync status; UX-DR5, UX-DR8, UX-DR9, UX-DR18, UX-DR19, and the wireflow journey sections give these requirements a coherent workspace-trust information architecture.
- PRD requires tenant isolation evidence and safe denial; UX-DR4, UX-DR6, UX-DR20, UX-DR21, and the "Prove Tenant Isolation" journey keep tenant scope and authorization posture visible before evidence.
- PRD requires metadata-only audit and incident reconstruction; UX-DR8, UX-DR27, and the audit/timeline wireflow support task/correlation/operation evidence without file-content exposure.
- PRD accessibility NFRs require WCAG 2.2 AA, keyboard navigation, non-color-only status, focus, tables, contrast, and zoom readability; UX-DR14, UX-DR30, UX-DR31, and UX-DR32 cover those expectations.

UX details that go beyond the PRD are accepted as design elaboration rather than conflict: exact custom component names, chosen "Resource Detail Console" direction, breakpoint ranges, and the UX-DR1-UX-DR32 traceability set are adopted by epics and architecture as implementation guidance.

### UX to Architecture Alignment

Architecture supports the UX requirements directly:

- Architecture explicitly names `_bmad-output/planning-artifacts/ux-design-specification.md` as an input and says Epic 6 and FrontComposer UI stories should treat it as normative.
- Architecture decisions F-1 through F-3 select Blazor Server/Interactive Server, FrontComposer, SignalR, projection-backed reads, and Microsoft Fluent UI Blazor.
- F-4 through F-6 support operator-disposition labels, redaction lock-icon affordance, and ACL-checked incident-mode event-stream fallback.
- F-7 defines a console-specific performance budget: p95 page load under 1.5 seconds for primary diagnostic flows, p99 under 3 seconds, degraded-mode p95 up to 5 seconds, skeleton at 400 ms, and still-loading/cancel affordance at 2 seconds.
- Architecture project layout includes `Hexalith.Folders.UI`, read-only pages, redaction and disposition components, incident stream page, perceived-wait components, and a client facade over `Hexalith.Folders.Client`.
- Architecture NFR coverage explicitly binds console accessibility to Fluent UI, operator-disposition labels, redaction affordance, incident-mode guardrails, perceived-wait UX, and automated or release-validation accessibility evidence.
- The wireflow document maps UX-DR1 through UX-DR32 to stories and semantic owners, with UX-DR31 and UX-DR32 release-verified through Story 6.11 and Workstream 7.

### Alignment Issues

1. FR58 / Memories search-index UX is not yet reflected in the UX specification or wireflow document.

   The PRD now contains FR58 for authorized, security-trimmed search over Folders content indexed into Memories. Architecture and Epic 10 contain the technical direction for worker-side indexing, bridge projection, authorized read facade, and indexing status. The UX specification and `docs/ux/ops-console-wireflows.md` predate that requirement and do not define the user experience for indexed search results, indexing status, stale/skipped/failed index evidence, or how raw paths/snippets/source URIs are represented when FR58 forbids leaking them.

   Recommendation: add a UX refresh or addendum before Epic 10 / FR58 implementation. It should define the authorized search result view, indexing-status evidence, redaction/inaccessibility treatment, empty/denied states, and cross-surface search-facade behavior. If FR58 is Phase 2 and not MVP, mark that explicitly in PRD, epics, and UX traceability.

### Warnings

- The UX specification is dated 2026-05-11, while the PRD/architecture/epics now include later Memories and search-index topology changes from 2026-06-22 through 2026-06-24. The MVP console UX remains aligned, but FR58 needs a design pass.
- `UX-DR31` and `UX-DR32` are release-verified rather than built by Stories 6.2-6.10. That is planned and traceable, but release readiness depends on Story 6.11 / Workstream 7 evidence being current and not merely asserted.
- The wireflow document calls out deferred inputs such as C3 retention and C4 metadata-filter vocabulary. UI work must not expose filters, retention claims, or search/indexing controls beyond the server-supported policy and approved C3/C4 artifacts.

## Step 5: Epic Quality Review

### Review Scope

Validated `_bmad-output/planning-artifacts/epics.md` against create-epics-and-stories standards:

- Epics should deliver user value, not only technical milestones.
- Epic sequencing should be independent in order: Epic N can use previous outputs, not future outputs.
- Stories should be independently completable in sequence and should not require future stories.
- Acceptance criteria should be Given/When/Then, specific, testable, and complete enough to implement.
- Greenfield setup and starter/scaffold stories are allowed, but should be explicitly framed as consumer-enabling runway rather than product capability.

Structural scan:

- Frontmatter claims `epicCount: 8` and `storyCount: 93`.
- Current document structure contains Epic 1 through Epic 10 plus Release Readiness Workstream 7, and 102 `### Story` entries.

### Epic-Level Quality Summary

| Epic / Workstream | User Value | Independence | Story Quality | Verdict |
| --- | --- | --- | --- | --- |
| Epic 1: Bootstrap Canonical Contract For Consumers And Adapters | Strong for API consumers/adapters, though technical | Good; correct greenfield runway | Mostly clear; scaffold stories are technical but justified | Accept with technical-runway exception |
| Epic 2: Tenant-Scoped Folder Access And Lifecycle | Strong tenant/admin value | Good | Mostly clear; 2.8b is production-wiring remediation but valuable | Accept |
| Epic 3: Provider Readiness And Repository Binding | Strong platform/admin value | Good after Epic 1/2 | Story 3.4 is oversized | Accept with split recommendation |
| Epic 4: Repository-Backed Workspace Task Lifecycle | Strong developer/agent value | Good after Epic 1-3 | Several stories are broad, especially 4.8, 4.14, 4.16 | Accept with sizing fixes |
| Epic 5: Cross-Surface Workflow Parity | Strong consumer/integration value | Good after canonical contract/lifecycle | Clear verification focus | Accept |
| Epic 6: Read-Only Workspace Trust Console And Audit Review | Strong operator/audit value | Good after projection/status semantics | Good story flow; 6.11 is oversized but explicitly verification-focused | Accept with sizing/evidence caution |
| Release Readiness Workstream 7 | Release governance value, not product-user value | Depends on outputs from Epics 1-6 | Many stories are CI/release governance milestones | Keep as workstream, not product epic |
| Epic 8: MVP Release Acceptance Closure | Release stakeholder value, no new product FR | Depends on prior work and external Legal sign-off | Closure/remediation backlog, not product epic | Reclassify as release closure workstream |
| Epic 9: AppHost Platform Alignment And Memories Search-Index Topology | Platform engineering value, no product FR | Has dormant value until Epic 10 producer | Technical/infrastructure stories only | Reclassify as architecture/platform runway |
| Epic 10: Folders Worker-Side Semantic-Indexing Producer And Bridge Projection | Strong future user value through authorized search facade | Explicitly gated on Epic 9, C4, and C9 | Seed-level backlog stubs, not implementation-ready | Needs create-story pass before implementation |

### Critical Violations

1. Non-product technical/release work is mixed into the epic list as if it were product epic scope.

   Evidence:
   - Release Readiness Workstream 7 explicitly has "no new product FR scope" and is release governance.
   - Epic 8 is a release-acceptance closure epic, includes residual test baseline and Legal sign-off, and explicitly says it is not a feature workstream.
   - Epic 9 has "No new product FR scope" and is infrastructure alignment / AppHost topology.

   Impact: If sprint planning treats these as normal product epics, velocity, dependency, and readiness signals become misleading. The create-epics standard rejects technical milestones as epics unless they are clearly separated as architecture runway or release governance.

   Recommendation: Keep Workstream 7, Epic 8, and Epic 9 outside the product epic sequence or relabel them as "Release Governance", "Release Closure", and "Architecture Runway". Preserve their stories, but do not count them as product capability epics or FR-bearing epic coverage.

2. Epic 10 is not implementation-ready even though PRD FR58 now exists.

   Evidence:
   - Epic 10 says it is Phase 2, gated on Epic 9 + C4 + C9, and "Stories are backlog stubs pending `create-story`".
   - Stories 10.1-10.5 use seed-level Given/When/Then criteria but do not have full user-story structure for 10.1-10.5.
   - Epic 10 "FRs covered" still says "New FR ... to be added to PRD when scheduled", while PRD already contains FR58.

   Impact: The authorized Memories-backed search facade has product value, but the planning artifact cannot yet support implementation or readiness claims for that capability.

   Recommendation: Run the story creation workflow for Epic 10 before implementation. Update Requirements Inventory, FR Coverage Map, Epic 10 summary, and Story 10.5 to explicitly map to FR58. Resolve C4 and C9 before scheduling stories that expose indexed search results.

### Major Issues

1. Stale frontmatter and count metadata.

   Evidence: frontmatter says `epicCount: 8` and `storyCount: 93`; the current document has 10 epic/workstream sections and 102 stories.

   Impact: Automated readiness checks, sprint planning, and reporting may consume stale metadata.

   Recommendation: Update frontmatter counts or remove them if the document is now append-only and counts are derived elsewhere.

2. Story 3.4 combines too much provider work.

   Current scope: Forgejo adapter implementation, version snapshots, schema drift detection, readiness, repository creation/binding, branch/ref, file, commit, status, provider failures, unknown outcomes, and nightly drift checks.

   Impact: This is likely multi-story work and hard to review independently.

   Recommendation: Split into Forgejo adapter baseline, Forgejo lifecycle contract tests, and Forgejo schema/version drift gate.

3. Story 4.8 is too broad for one independently completable story.

   Current scope: file tree, metadata, search, glob, bounded range-read, semantic/RAG retrieval backend guardrails, authorization, policy, binary/large-file policy, range/result limits, audit evidence, and derived-index authority rules.

   Impact: It mixes core context-query surfaces with future indexed/semantic behavior and could accidentally pull Epic 10 scope forward.

   Recommendation: Split core context queries (tree/metadata/range), search/glob policy, and indexed-search/Memories guardrails. Keep Memories search facade under Epic 10 / FR58.

4. Story 4.14 centralizes too many observability channels.

   Current scope: audit, traces, metrics, structured logs, successful/denied/failed/retried/duplicate/lock/file/commit/provider-readiness/state-transition events, and no-leakage policy.

   Impact: The story is testable only through many pipelines and may be hard to complete without broad cross-cutting rework.

   Recommendation: Split audit event/projection metadata, log/trace/metric propagation, and sentinel redaction verification if the story has not already been implemented with stronger as-built ACs.

5. Story 4.16 bundles multiple security test families.

   Current scope: sentinel redaction, path security, encoding equivalence, cross-tenant isolation, parallel tenant/task scenarios, stale locks, interrupted lifecycle attempts, safe error shapes, and metadata-only audit.

   Impact: This reads like a quality gate suite, not one product story.

   Recommendation: Split by risk family or clearly classify as a verification workstream with separate test artifacts.

6. Story 6.11 is very large.

   Current scope: no-mutation enforcement, WCAG 2.2 AA, responsive checks, zoom checks, keyboard walkthroughs, screen reader review, forced colors/high contrast, color-blindness review, focus management, and manual release evidence.

   Impact: This is a release evidence package, not a normal implementation story. It can be acceptable if tracked as verification only, but it should not hide unfinished manual evidence behind a green code story.

   Recommendation: Keep automated checks and manual release-evidence artifacts separate, with explicit "recorded", "reference_pending", or "blocked" states.

7. Story 8.6 is externally blocked by Legal.

   Evidence: Story 8.6 is marked `BLOCKED-PENDING-LEGAL` and requires recorded external Legal sign-off.

   Impact: This cannot be completed by implementation work alone and should not be planned as a normal development story.

   Recommendation: Track as a release blocker/dependency outside developer sprint commitment until Legal evidence exists.

8. "Semantic-indexing" naming remains ambiguous.

   Evidence: Epic 10 notes that the name is retained for traceability but now denotes the syntactic/BM25 search index, not RAG embeddings, with a future rename follow-up.

   Impact: The term can mislead story authors into implementing or testing the wrong Memories API path, especially because the document repeatedly references semantic/RAG language.

   Recommendation: Rename Epic 10 and affected stories to "Search-Indexing" or "Authorized Search Index" before detailed story creation, or add explicit terminology warnings to every Epic 10 story.

### Minor Concerns

1. Several Story 1 items are technical setup stories.

   This is acceptable because the architecture specifies a greenfield scaffold/mirroring approach and Epic 1 is framed as consumer-buildable contract value. Still, keep them marked as technical runway so they are not mistaken for end-user product increments.

2. Planning ACs are intentionally terse and defer to implementation story files.

   The note at the top says as-built stories expanded ACs substantially and that implementation artifacts are authoritative for as-built ACs. That is workable, but readiness review should be clear which source is authoritative for future implementation: `epics.md` for planning intent, implementation artifact files for as-built story contracts.

3. Story 2.8b is a remediation story inserted into Epic 2.

   It is justified by a production-wiring defect and has strong acceptance evidence, but it is not a clean product-value slice. Keep it as remediation context rather than a reusable story pattern.

### Dependency Analysis

- No critical forward dependency pattern was found across Epics 1-6 after the documented readiness corrections. Explicit references to later Epic 6 UI evidence from Epic 3/4 are mostly "renderable evidence" guardrails, not implementation dependencies.
- Story 6.5 intentionally gates Stories 6.6-6.10 and is correctly ordered before them.
- Story 8.3 depends on Stories 8.1 and 8.2; this is backward within the same closure epic and acceptable.
- Epic 9 produces dormant Memories topology until Epic 10; this is acceptable only if Epic 9 is classified as platform runway, not a standalone product epic.
- Epic 10 is explicitly gated by previous platform work and unresolved policy inputs (C4/C9), so it must not be planned until those gates are satisfied.

### Database / Persistence Timing

No traditional database-table upfront violation was found. The plan follows EventStore aggregates, projections, Dapr state, fixtures, and provider/search-index infrastructure. Persistence work is generally introduced when its owning story needs it.

### Starter / Greenfield Checks

Architecture does not specify a third-party starter template; it specifies scaffolding by mirroring Hexalith.Tenants and EventStore.Admin surfaces. Epic 1 Story 1.1 and Story 1.2 satisfy the greenfield initial project setup and root configuration requirement.

### Overall Epic Quality Verdict

Epics 1-6 are broadly implementation-ready as a product capability plan, subject to the story sizing cautions above. Workstream 7, Epic 8, and Epic 9 should be treated as release governance / closure / architecture runway, not normal product epics. Epic 10 is not ready for implementation planning until FR58 traceability, UX treatment, C4/C9 policy gates, and detailed story creation are completed.

## Summary and Recommendations

### Overall Readiness Status

NEEDS WORK.

The artifact set is substantially complete and internally mature for the original MVP work through Epics 1-6. Required planning documents exist, the PRD has explicit FR/NFR coverage, architecture supports the UX and platform constraints, and the core epic/story sequence is mostly coherent.

It is not cleanly ready as a whole implementation plan because newer FR58 / Memories search-index scope is not formally synchronized across PRD, epics, UX, and detailed stories; release-governance and architecture-runway work is mixed into the epic list; and several stories are too broad or externally blocked for normal sprint execution.

### Critical Issues Requiring Immediate Action

1. FR58 must be synchronized across artifacts.

   Update `epics.md` Requirements Inventory, FR Coverage Map, Epic 10 summary, and Story 10.5 to explicitly map the authorized Memories-backed search facade to FR58. Add UX treatment for authorized search results, indexing-status evidence, redaction, and no-leakage behavior. Do not schedule FR58 implementation until this is done.

2. Epic 10 needs a full create-story pass before implementation.

   The current Epic 10 stories are seed-level backlog stubs and explicitly gated on Epic 9, C4, and C9. Create implementation-ready stories with user-story structure, scoped acceptance criteria, test evidence, and resolved policy inputs.

3. Reclassify non-product epics/workstreams.

   Workstream 7, Epic 8, and Epic 9 should not be counted as normal product capability epics. Keep them as release governance, release closure, and architecture/platform runway so readiness and velocity reporting are honest.

4. Resolve or clearly scope deferred release blockers.

   C3 retention / Legal sign-off and C4 bounded input/filter policy remain load-bearing for release and FR58/search behavior. Story 8.6 is externally blocked and must not be planned as developer-completable work until Legal evidence exists.

5. Split oversized stories before new implementation starts.

   At minimum, split or re-scope Stories 3.4, 4.8, 4.14, 4.16, and 6.11 if they are still pending in their planning form. They currently combine too many implementation and verification concerns for reliable independent completion.

### Recommended Next Steps

1. Patch planning metadata: update `epicCount`, `storyCount`, and FR58 coverage in `epics.md`.

2. Decide FR58 release posture: MVP, Phase 2, or future. Record the decision consistently in PRD, epics, architecture, UX, and sprint status.

3. Run `bmad-create-story` for Epic 10 after C4/C9 and UX addendum are ready.

4. Move Workstream 7 / Epic 8 / Epic 9 into a separate release-governance or architecture-runway view, or mark them explicitly as non-product epic exceptions in sprint planning.

5. Review pending oversized stories and split any not already handled by as-built implementation artifacts.

6. Keep `docs/ux/ops-console-wireflows.md` and the UX spec synchronized when adding indexing/search UI or changing C4 filter behavior.

7. Before release acceptance, verify Story 6.11 and Workstream 7 evidence is recorded as actual evidence, not inferred from component choice or narrative.

### Final Note

This assessment identified 15 actionable issues across five categories: requirements traceability, UX alignment, epic/workstream structure, story sizing/readiness, and release-governance/deferred decisions.

The strongest immediate implementation path is to proceed only with already-specified Epics 1-6 work that has current implementation story files, while treating FR58/Epic 10 and release acceptance work as not ready until the synchronization and gating issues above are closed.

Assessor: Codex using `bmad-check-implementation-readiness`
Assessment date: 2026-06-27
