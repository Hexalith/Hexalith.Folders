---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
overallStatus: NOT READY
completedAt: 2026-07-14
assessor: Codex (BMad Implementation Readiness)
includedFiles:
  prd:
    - prd.md
  architecture:
    - architecture.md
  epics:
    - epics.md
  ux:
    - ux-design-specification.md
supplementaryFiles:
  - prd-validation-report.md
  - ux-design-directions.html
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-14
**Project:** folders

## Document Discovery

### PRD Files Found

**Whole Documents:**

- `prd.md` (75,633 bytes; modified 2026-07-07)
- `prd-validation-report.md` (57,462 bytes; modified 2026-06-26; supplementary validation evidence)

**Sharded Documents:** None.

**Selected for assessment:** `prd.md`

### Architecture Files Found

**Whole Documents:**

- `architecture.md` (181,678 bytes; modified 2026-07-07)

**Sharded Documents:** None.

**Selected for assessment:** `architecture.md`

### Epics and Stories Files Found

**Whole Documents:**

- `epics.md` (170,016 bytes; modified 2026-07-07)

**Sharded Documents:** None.

**Selected for assessment:** `epics.md`

### UX Design Files Found

**Whole Documents:**

- `ux-design-specification.md` (55,660 bytes; modified 2026-07-07)
- `ux-design-directions.html` (25,281 bytes; modified 2026-05-30; supplementary design directions)

**Sharded Documents:** None.

**Selected for assessment:** `ux-design-specification.md`

### Discovery Issues

- No required document category is missing.
- No whole-versus-sharded duplicate document sets were found.
- The primary assessment inputs were confirmed by the user.

## PRD Analysis

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

AR1: The MVP is repository-backed first. Local-only folders, local-first promotion to Git-backed storage, brownfield folder adoption, and repair-oriented cache rebuild workflows are post-MVP unless required as internal implementation mechanics for Git-backed workspace preparation.

AR2: REST is the canonical contract. CLI, MCP, SDK, and console surfaces must not introduce independent lifecycle semantics; they map to the canonical operations, states, authorization checks, idempotency rules, and error taxonomy.

AR3: File contents, diffs, generated context payloads, provider tokens, credential values, secret material, and unauthorized resource existence must not appear in events, logs, traces, metrics, audit records, console views, provider diagnostics, or error responses.

AR4: Hexalith.Tenants remains the source of truth for tenant identity, tenant lifecycle, and tenant membership. Hexalith.EventStore provides platform-owned command, aggregate, event, projection, query, cursor, read-model, and domain-service mechanics. Hexalith.Folders owns folder policy, ACLs, provider binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.

AR5: Required provider ports cover readiness validation, repository creation or binding, workspace preparation, governed file-operation application, Git-backed commit, provider status query, and cleanup or expiration support where needed.

AR6: The canonical REST API must be documented through an OpenAPI `v1` contract, with additive evolution preferred and explicit versioning for breaking changes.

AR7: Mutating commands must support idempotency keys, correlation IDs, and expected-version or conflict-detection semantics; projections must be read models rather than direct event mirrors.

AR8: File content transport must be explicitly selected from inline text, base64 binary, stream/upload reference, or external content reference. Metadata-only events may carry safe metadata but never file contents.

AR9: Cross-tenant authorization must complete before any file, workspace, credential, repository, lock, commit, provider, audit, or context access; payload tenant identifiers are comparison inputs, while authenticated/request context and EventStore envelope provide authority.

AR10: The read-only console consumes projections only and must not expose mutation, credential material, file contents, hidden repair actions, or direct filesystem browsing.

AR11: MVP quality gates require complete adapter parity coverage, complete ACL-matrix coverage, complete provider-contract pass per supported provider, zero sensitive-content leakage, idempotency proof, tenant-isolation proof, path-security coverage, deterministic read-model rebuilds, golden schema tests, provider-failure tests, context-query security tests, and sentinel-based redaction tests.

AR12: The architecture must resolve Git-backed workspace mechanics, file-content transport, large-file and binary policy, provider-capability contract shape, projection compaction, lock lease and expiry defaults, and file-operation batch atomicity before implementation stories are finalized.

AR13: C1 capacity is approved at four concurrent tenants, two folders per tenant, two active workspaces per tenant, and two concurrent agent tasks per tenant. C2 status freshness is approved at 500 ms commit-to-status-read lag. C5 scalability quantifiers are approved at the same tenant/folder/workspace/task units and at least one lifecycle operation per second.

AR14: C3 retention durations remain TBD by architecture review for audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, and cleanup records.

AR15: C4 bounded context-query limits remain TBD by architecture review for maximum files, response bytes, result count, and query duration.

AR16: FR58's content search is currently defined as authorized metadata-token recall through Epic 10 Story 10.6; full body-text materialization remains a C9-gated follow-up requiring Security and Product Management sign-off.

### PRD Completeness Assessment

The PRD is structurally strong and unusually explicit: it contains 58 numbered functional requirements, 70 extractable non-functional requirements, a canonical MVP workflow, surface boundaries, error categories, lifecycle states, quality gates, non-goals, and architecture exit criteria. Its requirement language is generally testable and traceable.

The PRD is not fully implementation-closed on its own. Retention targets (C3) and bounded context-query limits (C4) remain explicitly TBD, while the file-content transport model, large-file/binary policy, provider-capability contract, projection compaction, lock timing defaults, and file-operation batch atomicity are delegated to architecture. These are acceptable PRD deferrals only if architecture and epics resolve them with measurable acceptance evidence. FR58 also contains a staged semantic distinction between metadata-token recall and full body-text search that must remain explicit in downstream stories and release claims.

## Epic Coverage Validation

### Epic FR Coverage Extracted

- Epic 1 covers FR1, FR2, FR3, FR43, FR47, FR50, and FR51.
- Epic 2 covers FR4, FR5, FR6, FR8, FR9, FR10, FR11, FR12, FR13, and FR14.
- Epic 3 covers FR7, FR15, FR16, FR17, FR18, FR19, FR20, FR21, FR22, FR23, and FR57.
- Epic 4 covers FR24, FR25, FR26, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34, FR35, FR37, FR38, FR39, FR40, FR41, FR42, FR43, FR44, FR45, FR46, and FR55.
- Epic 5 covers FR47, FR48, FR49, FR50, and FR51.
- Epic 6 covers FR31, FR36, FR45, FR46, FR52, FR53, FR54, FR55, FR56, and FR57.
- Epic 10 covers FR58; Story 11.10 carries the supporting production bridge-read closure.

**Total distinct FRs in the epic coverage map: 58**

### Coverage Matrix

| FR Number | PRD Requirement | Epic and Story Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology. | Epic 1; Stories 1.6–1.11 | ✓ Covered |
| FR2 | Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility. | Epic 1; Stories 1.6–1.11 | ✓ Covered |
| FR3 | Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries. | Epic 1; Stories 1.7–1.11 | ✓ Covered |
| FR4 | Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks. | Epic 2; Story 2.2 | ✓ Covered |
| FR5 | Tenant administrators can grant folder access to users, groups, roles, and delegated service agents. | Epic 2; Story 2.4 | ✓ Covered |
| FR6 | Authorized actors can inspect effective permissions for a folder or task context. | Epic 2; Story 2.5 | ✓ Covered |
| FR7 | Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks. | Epic 3; Story 3.9 | ✓ Covered |
| FR8 | The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope. | Epic 2; Story 2.6 | ✓ Covered |
| FR9 | The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information. | Epic 2; Story 2.6 | ✓ Covered |
| FR10 | The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details. | Epic 2; Story 2.6 | ✓ Covered |
| FR11 | Authorized actors can create logical folders within a tenant. | Epic 2; Story 2.3 | ✓ Covered |
| FR12 | Authorized actors can inspect folder lifecycle and binding status. | Epic 2; Story 2.7 | ✓ Covered |
| FR13 | Authorized actors can archive folders when policy allows. | Epic 2; Stories 2.8 and 2.8b | ✓ Covered |
| FR14 | The system can preserve audit and status evidence for archived folders. | Epic 2; Stories 2.8 and 2.8b | ✓ Covered |
| FR15 | Platform engineers can configure supported Git provider bindings and credential references for a tenant. | Epic 3; Story 3.1 | ✓ Covered |
| FR16 | Authorized actors can validate provider readiness before repository-backed folder creation or binding. | Epic 3; Story 3.5 | ✓ Covered |
| FR17 | The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID. | Epic 3; Story 3.5 | ✓ Covered |
| FR18 | Authorized actors can create a repository-backed folder when readiness checks pass. | Epic 3; Story 3.6 | ✓ Covered |
| FR19 | Authorized actors can bind a folder to an existing repository where supported. | Epic 3; Story 3.7 | ✓ Covered |
| FR20 | Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks. | Epic 3; Story 3.8 | ✓ Covered |
| FR21 | The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets. | Epic 3; Stories 3.2 and 3.9 | ✓ Covered |
| FR22 | The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle. | Epic 3; Stories 3.2, 3.3, 3.4, and 3.9 | ✓ Covered |
| FR23 | Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior. | Epic 3; Stories 3.3, 3.4, and 3.9 | ✓ Covered |
| FR24 | Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid. | Epic 4; Story 4.2 | ✓ Covered |
| FR25 | Authorized actors can acquire a task-scoped workspace lock. | Epic 4; Story 4.3 | ✓ Covered |
| FR26 | Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata. | Epic 4; Stories 4.3 and 4.4 | ✓ Covered |
| FR27 | The system can deny competing operations when lock ownership or workspace state makes the operation unsafe. | Epic 4; Stories 4.3 and 4.4 | ✓ Covered |
| FR28 | The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions. | Epic 4; Stories 4.1 and 4.4 | ✓ Covered |
| FR29 | Authorized actors can release a workspace lock when ownership and policy allow. | Epic 4; Story 4.4 | ✓ Covered |
| FR30 | The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP. | Epic 4; Story 4.10 | ✓ Covered |
| FR31 | Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable. | Epics 4 and 6; Stories 4.9, 6.6, and 6.7 | ✓ Covered |
| FR32 | Authorized actors can add, change, and remove files within a prepared and locked workspace. | Epic 4; Stories 4.6 and 4.7 | ✓ Covered |
| FR33 | The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy. | Epic 4; Stories 4.5–4.7 | ✓ Covered |
| FR34 | Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads. | Epic 4; Story 4.8 | ✓ Covered |
| FR35 | The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses. | Epic 4; Story 4.8 | ✓ Covered |
| FR36 | The operations console can remain read-only and excluded from file editing or file-content browsing capabilities. | Epic 6; Stories 6.2 and 6.11 | ✓ Covered |
| FR37 | Authorized actors can commit workspace changes for repository-backed folders. | Epic 4; Story 4.12 | ✓ Covered |
| FR38 | Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits. | Epic 4; Stories 4.11 and 4.12 | ✓ Covered |
| FR39 | The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID. | Epic 4; Stories 4.9 and 4.12 | ✓ Covered |
| FR40 | The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence. | Epic 4; Stories 4.11 and 4.13 | ✓ Covered |
| FR41 | The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers. | Epic 4; Story 4.11 | ✓ Covered |
| FR42 | The system can reject duplicate logical operations when retry identity or operation intent conflicts. | Epic 4; Story 4.11 | ✓ Covered |
| FR43 | The system can expose a canonical error taxonomy across supported surfaces. | Epics 1 and 4; Stories 1.6, 1.10, and 4.13 | ✓ Covered |
| FR44 | The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure. | Epic 4; Story 4.13 | ✓ Covered |
| FR45 | The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`. | Epics 4 and 6; Stories 4.1, 4.9, 4.13, and 6.3 | ✓ Covered |
| FR46 | The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure. | Epics 4 and 6; Stories 4.13 and 6.6 | ✓ Covered |
| FR47 | API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle. | Epics 1 and 5; Stories 1.6–1.11 and 5.5 | ✓ Covered |
| FR48 | CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5; Stories 5.2, 5.4, 5.6, and 5.7 | ✓ Covered |
| FR49 | MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5; Stories 5.3, 5.4, 5.6, and 5.7 | ✓ Covered |
| FR50 | SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epics 1 and 5; Stories 1.12, 5.1, and 5.5 | ✓ Covered |
| FR51 | The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior. | Epics 1 and 5; Stories 1.13 and 5.4–5.7 | ✓ Covered |
| FR52 | Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status. | Epic 6; Stories 6.6 and 6.7 | ✓ Covered |
| FR53 | Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations. | Epic 6; Stories 6.1 and 6.8 | ✓ Covered |
| FR54 | Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference. | Epic 6; Stories 6.1, 6.8, and 6.9 | ✓ Covered |
| FR55 | The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses. | Epics 4 and 6; Stories 4.14, 4.16, 6.4, and 6.11 | ✓ Covered |
| FR56 | The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events. | Epic 6; Stories 6.1 and 6.8 | ✓ Covered |
| FR57 | Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness. | Epics 3 and 6; Stories 3.9 and 6.7 | ✓ Covered |
| FR58 | Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see — security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only — without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence. | Epic 10; Stories 10.1–10.6, with production bridge-read support in Story 11.10 | ✓ Covered |

### Missing Requirements

No PRD functional requirement is absent from the epic coverage map. No epic coverage-map FR identifier is absent from the PRD, and no coverage-map identifier is duplicated.

### Coverage Statistics

- Total PRD FRs: 58
- FRs covered in epics: 58
- Missing FRs: 0
- Extra FR identifiers in epic coverage map: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

**Found.** The primary UX input is `ux-design-specification.md`, a complete whole-document specification containing 32 stable requirements (`UX-DR1`–`UX-DR32`). The supplementary `ux-design-directions.html` contains the explored visual directions. No sharded UX document set exists.

### UX ↔ PRD Alignment

The UX specification is strongly aligned with the PRD:

- Both define a web/desktop-first, read-only operations console for operators, tenant administrators, and chatbot or agent developers.
- The three critical UX journeys directly support PRD journeys for workspace trust inspection, tenant-isolation proof, and failure diagnosis from metadata-only evidence.
- Both preserve the same non-negotiable boundary: no mutation controls, repair actions, file editing, raw diffs, credential reveal, unrestricted file browsing, or unauthorized-resource confirmation.
- Both require visible tenant context, workspace state, provider readiness, lock and dirty state, commit evidence, audit history, correlation/task identifiers, freshness, safe denial, and redaction distinctions.
- Both target WCAG 2.2 AA, keyboard navigation, visible focus, semantic structure, sufficient contrast, zoom resilience, and status communication that does not rely on color alone.
- The UX specification elaborates the PRD with global search and state-first filters, resource-detail navigation, six domain evidence components, responsive fallbacks, dialog behavior, loading behavior, and explicit accessibility test methods. These are design elaborations rather than conflicting product scope, and the epics inventory carries all 32 UX-DR identifiers.
- FR58 is treated consistently as authorized backend discovery. The UX addendum does not introduce content preview or bypass the metadata-only, Folders-authorized boundary.

### UX ↔ Architecture Alignment

The architecture supports most UX requirements explicitly:

- Frontend decisions F-1 through F-3 select Blazor Server/Interactive Server behavior, Hexalith.FrontComposer Shell, and Microsoft Fluent UI Blazor, matching UX-DR1 and the UX implementation strategy.
- The UI consumes projection/read-model endpoints through `Hexalith.Folders.Client`; it has no aggregate or provider mutation path.
- The architecture recognizes the same custom evidence patterns: Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree, Diagnostic Timeline, Trust Matrix, and Redaction/Inaccessibility State.
- Sensitive metadata classification and the lock-icon redaction affordance support the UX requirement to distinguish redacted data from unknown, missing, unavailable, failed, and denied data.
- Architecture F-7 sets measurable console budgets: primary diagnostic page-load below 1.5 seconds p95 and 3 seconds p99, degraded-mode flows up to 5 seconds p95, a skeleton at 400 ms, and cancellation affordance at 2 seconds.
- Desktop-first layout, tablet/mobile fallback, search/filter dimensions, canonical state vocabulary, keyboard behavior, WCAG 2.2 AA, and the automated axe accessibility gate are all represented in architecture and epics.

### Alignment Issues

#### High — Deployed diagnostic evidence cannot yet populate the core UX

The architecture states that `IOpsConsoleDiagnosticsReadModel` and `IWorkspaceTransitionEvidenceReadModel` are seed-backed dev/test seams with no deployed projection. In the deployed host they are empty, so readiness, lock, dirty-state, failed-operation, provider-status, sync-status, projection-freshness, and transition-evidence requests return safe-empty `NotFoundSafe` results. This is secure and honest, but it prevents the primary UX promise—finding a workspace and understanding its current trust state from evidence—from being fulfilled in production.

**Required resolution:** Complete Epic 11 Story 11.10 by authoring and registering EventStore-backed diagnostics and transition-evidence projections, then prove populated console reads on the DCP-capable lane. Until that closes, the console is architecturally safe but not operationally complete.

#### Medium — Primary operator-disposition vocabulary is absent from the UX specification

Architecture F-4 and the C6 matrix make `auto-recovering`, `awaiting-human`, `terminal-until-intervention`, and `degraded-but-serving` the primary visual labels, with technical states secondary. These labels do not appear in `ux-design-specification.md` or the PRD. The authoritative UX requirements instead emphasize canonical states such as ready, locked, dirty, committed, failed, inaccessible, delayed, redacted, and stale.

**Required resolution:** Validate the disposition vocabulary with UX/product ownership and update the stable UX-DR traceability set and component semantics, or keep canonical product states primary. The architecture, UX specification, Story 6.3, tests, and operator documentation must use one agreed hierarchy.

#### Medium — Incident-stream flow is missing from the UX specification

Architecture F-6 defines a privileged, ACL-checked `/_admin/incident-stream` experience with a degraded-mode banner, raw event metadata, disposition labels, redaction controls, and correlation/time-window copying. Epic 6 Story 6.9 implements it, but the authoritative UX specification does not describe this flow or its navigation, responsive behavior, accessibility, safe-empty state, or no-leak acceptance criteria.

**Required resolution:** Add the incident-mode flow to the UX specification or its normative wireflow companion, map it to stable UX requirement identifiers, and verify it under UX-DR11 and UX-DR30–32 before treating F-6 as UX-approved.

### Warnings

- UX documentation is present and comprehensive; there is no missing-UX warning.
- The architecture intentionally introduces richer operator behavior than the current UX artifact documents. Those additions need explicit UX traceability rather than being treated as implicitly approved.
- The fail-safe empty read models protect tenant isolation but must not be presented as implementation-complete evidence for the console journeys.

## Epic Quality Review

### Review Basis

The epic document declares 11 epics/workstreams and `storyCount: 115`, but contains 116 `### Story` headings. All 116 stories have an Acceptance Criteria section and at least one Given/When/Then sequence. The review therefore distinguishes mechanical BDD formatting from actual user value, independence, sizing, error completeness, and sequencing quality.

### Epic-Level Compliance

| Epic / Workstream | User-value focus | Independence | Quality result |
| --- | --- | --- | --- |
| Epic 1 — Bootstrap Canonical Contract | Weak. It is primarily scaffold, contract authoring, code generation, fixtures, and CI gates, even though framed for consumers. | Stands alone technically, but does not deliver an executable user workflow. | 🔴 Technical milestone presented as a product epic; several oversized enabler stories. |
| Epic 2 — Tenant-Scoped Folder Access and Lifecycle | Strong tenant-administrator value. | Fails internal sequencing because folder operations precede the story that enforces layered authorization; archive needed a later remediation story for production wiring. | 🔴 User-value epic with critical forward sequencing defects. |
| Epic 3 — Provider Readiness and Repository Binding | Strong platform-engineer and authorized-actor value. | Uses only earlier foundations and has no later-epic dependency in its stated outcome. | 🟠 Broad technical adapter stories, especially Story 3.4, but epic direction is sound. |
| Epic 4 — Repository-Backed Workspace Task Lifecycle | Strong core product value. | Fails: deployed transition evidence is explicitly deferred to Epic 11 Story 11.10; idempotency/correlation and audit foundations are sequenced after operations that require them. | 🔴 Core epic is not independently implementation-complete. |
| Epic 5 — Cross-Surface Workflow Parity | Clear integration-consumer value. | Fails completion integrity: Epic 8 later records 15 missing REST routes and adds the real four-surface wire gate, so Epic 5 could not have delivered its declared outcome independently. | 🔴 Later closure epic proves the original parity epic was incomplete. |
| Epic 6 — Read-Only Workspace Trust Console | Strong operator and audit-reviewer value. | Fails: deployed diagnostics are empty until Epic 11 Story 11.10 authors and wires production projections. | 🔴 Safe shell and pages cannot deliver the stated trust-console outcome independently. |
| Workstream 7 — Release Readiness | Release/governance value, correctly labeled a non-product workstream. | Depends on evidence from Epics 1–6 by design. | 🟠 Appropriate as a release workstream, but its technical tasks should not be evaluated or reported as product user stories. |
| Epic 8 — MVP Release Acceptance Closure | No new product value; explicitly a closure epic. | Exists to repair incomplete routes, parity, accessibility evidence, and test baselines from earlier epics. | 🔴 Technical closure milestone and evidence that earlier epics were not independently complete. |
| Epic 9 — AppHost Platform Alignment | No direct user outcome; infrastructure alignment only. | Fails: its routing is explicitly dormant until future Epic 10 provides a producer. | 🔴 Technical epic with a direct forward dependency. |
| Epic 10 — Search-Index Producer and Bridge | Eventual FR58 user value through authorized search. | Fails: deployed search returns zero items/read-model-unavailable until future Epic 11 Story 11.10; Story 10.6 also names Story 11.10 in its own acceptance result. | 🔴 Product value cannot be realized independently of a later epic. |
| Epic 11 — Domain-Focus Platform Refactoring | No new product FR scope; refactoring/governance closure. | Internally sequenced, but contains multi-repository prerequisites and absorbs unfinished product projections from Epics 4, 6, and 10. | 🔴 Technical refactoring epic used to finish earlier product outcomes. |

### 🔴 Critical Violations

#### 1. Technical milestones are modeled as epics

Epics 1, 8, 9, and 11 are primarily scaffold/contract infrastructure, release closure, AppHost alignment, and platform refactoring. They do not independently deliver a product capability to an end user. Workstream 7 is at least labeled non-product, but the other technical epics remain mixed into the epic sequence.

**Remediation:** Keep technical enablers in a separate architecture/runway or release backlog, or attach the minimum necessary enabler stories to the user-value epic they unlock. Do not count them as completed product epics or use their completion to claim user-value readiness.

#### 2. Epics 4, 6, and 10 depend on future Story 11.10

- Epic 4 explicitly defers the production workspace transition-evidence projection to Story 11.10.
- Epic 6 explicitly defers all seven production diagnostics views to Story 11.10.
- Epic 10 explicitly leaves the deployed bridge read model unavailable until Story 11.10, causing context search to return zero items.

These are direct violations of the rule that Epic N cannot require Epic N+1 to deliver its stated outcome.

**Remediation:** Move each projection into the originating product epic and make it part of that epic's completion criteria, or formally reopen and re-sequence those epics ahead of dependent console/search claims. Story 11.10 may refactor already-working projections later, but it must not be the first production implementation.

#### 3. Epic 9 has an explicit forward dependency on Epic 10

Epic 9's routing is described as dormant until Epic 10 emits search-index events. Therefore Epic 9 delivers no standalone user value and cannot validate its intended topology outcome without later work.

**Remediation:** Merge the minimum producer and end-to-end routing proof into the same user-value search epic, leaving pure platform refactoring as a later technical task.

#### 4. Epic 5's completion is contradicted by Epic 8

Epic 5 claims complete REST/SDK/CLI/MCP lifecycle parity, but Epic 8 records that REST served only 32 of 47 operations and adds 15 missing server routes plus the first real four-surface wire gate. This means the parity epic's stories were not independently completable under their stated acceptance criteria.

**Remediation:** Treat Epic 5 as incomplete until Stories 8.1–8.3 evidence is folded into its acceptance boundary. Do not retain a separate closure epic for functionality that the original epic claimed.

#### 5. Authorization is implemented after operations that require it

Within Epic 2, Stories 2.3–2.5 create folders, mutate ACLs, and inspect permissions before Story 2.6 implements layered authorization and safe denial. Their Given clauses assume permission checks that the sequence has not yet delivered.

**Remediation:** Move Story 2.6 immediately after the tenant/ACL foundation and before any folder command/query story, or require each earlier story to implement and verify its complete authorization-before-observation path.

#### 6. Idempotency, correlation, and audit are implemented after mutating lifecycle stories

Within Epic 4, Stories 4.2–4.10 implement prepare, lock, file mutation, context, status, and cleanup before Story 4.11 provides mandatory idempotency/correlation propagation and before Story 4.14 provides the cross-cutting audit/observability pipeline. Earlier acceptance criteria already claim idempotent and auditable behavior, creating hidden forward dependencies.

**Remediation:** Move mandatory idempotency, correlation, metadata classification, and audit emission foundations before the first mutation story. Each mutation story must prove its own accepted, denied, replay, conflict, and metadata-only audit paths.

#### 7. Story 10.6 explicitly depends on future Story 11.10

Story 10.6's final Given/When/Then requires Story 11.10 to rebase on and preserve its behavior. A story cannot be completed by asserting what a future story will do.

**Remediation:** Limit Story 10.6 acceptance to behavior it delivers and verifies now. Put preservation and migration acceptance wholly in Story 11.10.

#### 8. Story 2.8 required Story 2.8b to become production-real

The document states that Story 2.8 could not reach done until the later 2.8b story wired the real `/process` path. This is direct evidence that Story 2.8's original acceptance boundary omitted production composition and allowed “green tests, broken production wiring.”

**Remediation:** Merge 2.8b's no-mock gateway and denial-table criteria into Story 2.8's canonical definition of done, and apply the same production-wiring criterion to every mutation-command story.

### 🟠 Major Issues

#### Oversized stories

The following stories bundle multiple independently reviewable outcomes and should be split:

- **1.1:** all source, test, and sample projects plus buildable dependency shape.
- **1.4:** C3, C4, S-2, and C6 decisions in one workshop-deliverable story.
- **1.7:** tenant, folder, provider, repository binding, branch/ref, ACL, and effective-permission contract groups.
- **1.14–1.16:** multiple independent CI gate families per story.
- **3.4:** Forgejo adapter behavior, version snapshot matrix, contract suite, and nightly drift detection.
- **4.16:** sentinel redaction, path security, encoding equivalence, cross-tenant isolation, parallel contention, stale locks, and interrupted lifecycles.
- **6.11:** no-mutation security, responsive layouts, zoom, automated accessibility, keyboard, screen reader, forced colors, color-blindness, and focus management.
- **7.13:** API, SDK, CLI, MCP references, examples, auth guidance, and diagrams.
- **7.17:** the full ADR set plus tenant deletion, retention, alert, rollback, drift, reconciliation, and incident runbooks.
- **8.1 and 8.2:** eight and seven distinct REST operations respectively under one generic acceptance boundary.
- **8.5:** several unrelated test projects and governance masks in one residual-baseline story.
- **10.4:** removal/archive/tombstone semantics plus live end-to-end routing proof.
- **10.6:** materializer behavior, sensitive-data corpus, C4/C9 gates, three test suites, DCP evidence, governance pinning, and future migration.
- **11.2:** prerequisite APIs across Commons, EventStore, FrontComposer, and Memories—multiple repositories and ownership boundaries.
- **11.3, 11.5, 11.7, 11.8, 11.10, 11.11, and 11.13:** each combines several subsystems, migrations, gates, documentation sets, or independent correctness outcomes.

**Remediation:** Split by independently demonstrable behavior, provider/surface, risk family, repository boundary, or projection. Each resulting story should have one primary outcome and a focused verification lane.

#### Acceptance criteria are mechanically formatted but frequently incomplete

Every story has Given/When/Then syntax, but many criteria contain only a broad happy path or delegate errors and security to later stories. Examples include:

- Story 2.3 omits duplicate, idempotency replay/conflict, ACL denial, wrong-tenant, and real persistence-path cases.
- Story 2.4 omits unauthorized grant/revoke, stale authorization, replay/conflict, and effective-permission projection failure cases.
- Story 3.6 omits duplicate provisioning, unknown provider outcome, append conflict, and reconciliation cases.
- Story 4.3 omits competing owner, replay, conflicting key, expiry, stale lock, and mid-task revocation acceptance rows.
- Story 4.6 omits partial provider failure, duplicate mutation, conflicting payload, content-policy error taxonomy, and unknown outcome.
- Stories 6.6–6.9 do not enumerate safe-empty, denied, redacted, stale, unavailable, loading, keyboard, and responsive states per page.
- Stories 8.1 and 8.2 use one generic criterion for many routes rather than route-level accepted/denied/not-found/idempotency/read-model rows.

The document itself says these are terse planning ACs and that authoritative as-built ACs live in implementation-artifact story files. That means `epics.md` alone is not a complete implementation contract even though it is the selected epics input for this readiness workflow.

**Remediation:** Either synchronize the complete acceptance rows back into `epics.md`, or make the individual story files part of the readiness inventory and assess them as the authoritative story specification set.

#### Fail-open completion language

- Story 11.10 permits live search and populated diagnostic proof to be replaced by “the residual is re-carried with evidence.”
- Story 11.13 permits the final verification checklist to be “satisfied or explicitly blocked with evidence.”
- Story 8.5 permits residual reds to be “explicitly accepted with rationale.”

Recording blockers is honest, but it is not equivalent to satisfying a story's acceptance criteria. These clauses allow technical stories to reach done while their outcomes remain unavailable.

**Remediation:** A blocked mandatory criterion must keep the story blocked or incomplete. Track accepted deviations separately with explicit product/release authority, expiry, and impact; do not encode them as alternate success branches.

#### Epic 10 scope classification is inconsistent

Frontmatter classifies Epic 10 as a Phase 2 capability epic, while the PRD and the epic narrative state FR58 is current scope and remaining Epic 10 work is release-readiness closure rather than a future addition.

**Remediation:** Decide whether FR58 is required for the current implementation/release boundary. Use one classification consistently across PRD, epics, sprint status, and release criteria.

### 🟡 Minor Concerns

- `storyCount: 115` is stale; the document contains 116 story headings.
- Story numbering uses `2.8b`, which complicates numeric ordering and automated inventory. Renumber it or formally support suffix identifiers in tooling.
- Historical correction notes, completion status, release limitations, and current story requirements are heavily interleaved. This improves provenance but obscures the normative plan; move history to change logs and keep the active contract concise.
- The architecture-selected sibling-module starter is represented correctly by Stories 1.1 and 1.2, and CI gates appear early in Epic 1.
- No relational database or complete entity schema is created upfront; state/read-model work is generally introduced near the capability that uses it. This aspect complies with incremental design guidance.

### Best-Practices Compliance Summary

- Epics delivering user value: **6 of 11 clearly do** (Epics 2–6 and 10); Workstream 7 is explicitly non-product; Epics 1, 8, 9, and 11 are technical milestones.
- Epic independence: **failed** due to forward dependencies into Epics 8, 10, and 11.
- Story independence: **failed** for authorization, idempotency/audit sequencing, Story 2.8/2.8b, and Story 10.6/11.10.
- Story sizing: **failed** for the listed multi-surface, multi-risk, and multi-repository stories.
- Acceptance-criteria format: **116 of 116 mechanically compliant** with Given/When/Then.
- Acceptance-criteria completeness: **failed** at planning-document level because negative/error/security rows are often deferred or only present in separate as-built story files.
- Starter-template alignment: **passed** for the architecture-selected sibling-module scaffold pattern.
- Database/entity timing: **passed**; no monolithic database-first story was found.
- FR traceability: **passed at 58/58**, but traceability does not establish story independence or production completeness.

### Required Structural Remediation

1. Re-sequence authorization and idempotency/audit foundations before the operations that require them.
2. Move first production implementations of transition evidence, ops diagnostics, and search bridge reads into their originating product epics; leave Epic 11 only the later refactor.
3. Fold Epic 8's missing route/parity closure back into Epic 5 and the originating feature epics.
4. Merge Epic 9's dormant topology with the minimum Epic 10 producer/round-trip needed for standalone value.
5. Split the oversized stories and replace fail-open alternate success clauses with blocking criteria.
6. Choose one authoritative story contract set for readiness assessment, synchronize it, and correct the story inventory count.

## Summary and Recommendations

### Overall Readiness Status

# NOT READY

The planning set is comprehensive but not safe to treat as implementation-ready. It has complete document-category coverage, 58/58 functional-requirement traceability, 70 extracted non-functional requirements, a detailed architecture, a complete UX specification, and mechanically valid Given/When/Then criteria for all 116 story headings. Those strengths establish scope coverage; they do not establish executable sequencing or honest completion boundaries.

Implementation readiness fails because core product outcomes are deferred into later technical closure/refactoring epics. Epic 4 transition evidence, Epic 6 production diagnostics, and Epic 10 authorized search require Epic 11 Story 11.10. Epic 5 parity required a later Epic 8 to add 15 missing REST routes and the real wire-level parity gate. Epic 9 is dormant until Epic 10. Within product epics, authorization and mandatory idempotency/audit foundations are sequenced after operations that depend on them. Several acceptance criteria also permit blocked residuals to be re-carried as an alternate success outcome.

### Critical Issues Requiring Immediate Action

1. **Remove forward product dependencies into Story 11.10.** Transition evidence, ops-console diagnostics, and the search bridge read model must be production-capable inside the originating product epics before those epics can be complete.
2. **Repair the parity completion boundary.** Fold Epic 8 Stories 8.1–8.3 into Epic 5 and the originating capability epics; do not claim cross-surface parity before all canonical routes exist and the four-surface wire scenario passes.
3. **Re-sequence security and reliability foundations.** Move layered authorization before folder commands/queries, and move idempotency, correlation, metadata classification, and audit emission before the first lifecycle mutation.
4. **Remove technical milestones from the product-epic sequence.** Epics 1, 8, 9, and 11 belong in architecture runway, release governance, or refactoring backlogs unless reframed around a standalone user outcome.
5. **Eliminate explicit forward story dependencies.** Story 10.6 must verify only its own delivered behavior; Story 11.10 alone owns later preservation/migration proof. Merge Story 2.8b's production-path criteria into Story 2.8.
6. **Close UX architecture gaps.** Complete populated production diagnostic projections, validate the operator-disposition vocabulary with UX/product ownership, and add the privileged incident-stream flow to the normative UX requirements or wireflows.
7. **Replace fail-open acceptance clauses.** “Residual re-carried with evidence” and “satisfied or explicitly blocked” must keep a mandatory story incomplete; they cannot be alternate passing outcomes.

### Recommended Next Steps

1. **Synchronize the authoritative artifacts.** Update the PRD's C3/C4 status to reflect approved architecture/governance decisions, resolve Epic 10's current-scope-versus-Phase-2 classification, correct `storyCount` from 115 to 116, and decide whether `epics.md` or the implementation-artifact story files are the authoritative acceptance contract.
2. **Redraw the epic dependency graph.** Keep Epics 2–6 and 10 as user-value capabilities, relocate technical workstreams, and move each first production projection/route into the capability that promises it. Ensure Epic N requires only Epics 1 through N−1.
3. **Reorder within-epic foundations.** Place Story 2.6 before Stories 2.3–2.5. Place the applicable parts of Stories 4.11 and 4.14 before Story 4.2, or make idempotency, correlation, authorization, and audit explicit acceptance rows of every mutation story.
4. **Split oversized stories.** Prioritize Stories 8.1, 8.2, 10.6, 11.2, 11.3, 11.7, 11.10, 11.11, and 11.13, then split the remaining multi-risk and multi-surface stories listed in the quality review.
5. **Strengthen acceptance criteria.** Add accepted, denied, wrong-tenant, not-found-safe, replay, conflict, provider-known-failure, unknown-outcome, stale/unavailable-read-model, metadata-only audit, and real production-wiring rows where relevant. A mutation story must include the no-mock EventStore gateway path required by project context.
6. **Make blocked evidence honest.** Mandatory DCP/live proof may remain an external blocker, but the owning story and epic must remain blocked until proof exists or an explicitly authorized scope change removes the requirement.
7. **Rerun implementation readiness** after the PRD, UX, architecture, epics, and authoritative story files are synchronized and the dependency/acceptance changes are complete.

### Findings Summary

- Required planning document categories found: 4 of 4
- PRD functional requirements: 58
- PRD non-functional requirements: 70
- FRs mapped to epics: 58 of 58 (100%)
- Story headings assessed: 116
- Stories with mechanical Given/When/Then criteria: 116 of 116
- Substantive issue groups: 15 across artifact alignment, UX/architecture alignment, epic structure/dependencies, and story quality/acceptance
- Additional minor defects: stale story count, nonstandard `2.8b` identifier, and excessive historical material in the normative epic plan

### Final Note

This assessment does not recommend beginning a new implementation phase or claiming implementation readiness from the current planning structure. The product scope and traceability are strong, but the plan must first make user-value epics independently completable, move production behavior out of later closure/refactoring epics, and enforce mandatory acceptance outcomes without fail-open alternatives.

**Assessment date:** 2026-07-14  
**Assessor:** Codex — BMad Implementation Readiness workflow
