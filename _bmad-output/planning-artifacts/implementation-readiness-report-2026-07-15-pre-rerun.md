---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
status: not-ready
assessedAt: 2026-07-15
assessor: Codex — BMAD Implementation Readiness workflow
inputDocuments:
  prd: _bmad-output/planning-artifacts/prd.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  epics: _bmad-output/planning-artifacts/epics.md
  ux: _bmad-output/planning-artifacts/ux-design-specification.md
supportingDocumentsExcluded:
  - _bmad-output/planning-artifacts/prd-validation-report.md
  - _bmad-output/planning-artifacts/reconcile-architecture.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-15
**Project:** folders

## Document Inventory

### Documents Included

- PRD: `prd.md` (117,645 bytes; modified 2026-07-14 23:44 +02:00)
- Architecture: `architecture.md` (181,678 bytes; modified 2026-07-07 21:03 +02:00)
- Epics and Stories: `epics.md` (170,016 bytes; modified 2026-07-07 21:05 +02:00)
- UX Design: `ux-design-specification.md` (55,660 bytes; modified 2026-07-07 09:25 +02:00)

### Documents Excluded

- `prd-validation-report.md` — supporting validation report, not the canonical PRD.
- `reconcile-architecture.md` — supporting reconciliation artifact, not the canonical architecture specification.

### Discovery Findings

- All required document types were found.
- No sharded document sets were found.
- No whole-versus-sharded duplicate conflicts were found.

## PRD Analysis

### Functional Requirements

FR1: Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing.

FR2: Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions.

FR3: Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys.

FR4: Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it.

FR5: Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting verb scope is visible in effective permissions and auditable without exposing hidden principals.

FR6: Authorized actors can inspect effective permissions for a folder or task context.

FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.

FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.

FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.

FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

FR11: Authorized actors can create logical folders within a tenant.

FR12: Authorized actors can inspect folder lifecycle and binding status.

FR13: Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.

FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.

FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.

FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.

FR18: Authorized actors can create a repository-backed folder when readiness checks pass.

FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.

FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.

FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.

FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.

FR23: Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready.

FR24: Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect.

FR25: Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.

FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.

FR27: Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata.

FR28: Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.

FR29: Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.

FR30: Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.

FR31: Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable.

FR32: Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit.

FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.

FR34: Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution.

FR35: Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range/file content is never silently truncated, and unsupported excess returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.

FR36: The operations console must remain read-only and excluded from file editing or file-content browsing capabilities.

FR37: Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first enters `unknown_provider_outcome`; only exhausted/conflicting automatic evidence enters `reconciliation_required`.

FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission.

FR39: The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification.

FR40: The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation.

FR41: Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as new intent.

FR42: While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

FR43: Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility.

FR44: The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.

FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.

FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.

FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine.

FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.

FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.

FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.

FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.

FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.

FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.

FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.

FR56: Normal operation timelines come from projections; during projection degradation, the authorized incident view may expose bounded redacted event evidence with a persistent warning, last checkpoint, correlation ID, and time window, but no mutation or repair path.

FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

FR58: Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe.

**Total FRs: 58**

### Non-Functional Requirements

#### Security and Tenant Isolation

NFR1: Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, asynchronous provider side effect, and audit record. No incoming webhook ingestion exists in MVP.

NFR2: Cross-tenant access leaks are zero-tolerance defects. No object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.

NFR3: Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, asynchronous work, audit records, index results, and context-query results.

NFR4: File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.

NFR5: Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.

NFR6: Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.

NFR7: Every mutation and asynchronous side effect must revalidate current tenant, folder, delegated-actor, binding, and credential authority before touching a protected resource; revocation fails closed and changes any held lock to revoked/inaccessible.

NFR8: Paths, repository names, branch names, and commit messages are tenant-sensitive by default. Authorized tenant members and tenant-scoped operators with need-to-know may view them; cross-tenant/external diagnostics redact them. A tenant confidential override replaces cleartext at audit/projection write time with a stable tenant-scoped correlation token that preserves equality/linkage across authorized incident records but cannot reveal the original value. Redacted, hidden, unknown, missing, stale, and unavailable remain visibly distinct.

NFR9: Credential references must be validated and displayed only as non-secret identifiers or status indicators.

NFR10: Provider credentials and repository bindings must be tenant-scoped and must not be reused across tenants, even if repository URLs appear identical.

NFR11: Provider credentials must use the least privilege required for supported lifecycle operations and must be validated against required provider capabilities before use.

NFR12: Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.

#### Reliability, Idempotency, and Failure Visibility

NFR13: Workspace lifecycle uses only the canonical lowercase wire states defined in the Glossary; lock state and generic operation-execution status are separate dimensions and must be labeled as such.

NFR14: Every accepted operation exposes operation identity, workspace lifecycle, applicable lock state, projection freshness, and a terminal or inspectable non-terminal outcome.

NFR15: Repository-backed task lifecycle operations must leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.

NFR16: Unconfirmed external effects immediately enter `unknown_provider_outcome` and permit only bounded automatic read-only checks; exhausted or conflicting evidence enters `reconciliation_required`, blocks retry/mutation/takeover, and requires human escalation. These states never collapse into a generic failure.

NFR17: Idempotency keys are required for every mutating Contract Spine operation; non-mutating operations reject them.

NFR18: While the idempotency record is unexpired within its declared retention tier, a repeated call with the same key and equivalent payload must return the same logical result, and the same key with a conflicting payload must return an idempotency conflict. After expiry, either use returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new request.

NFR19: Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.

NFR20: Lock acquisition is deterministic and limited to one active writer per managed tenant plus canonical provider/repository identity plus normalized target ref; aliases resolving to that identity collide.

NFR21: Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.

NFR22: Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.

NFR23: A successful committed state requires provider-confirmed durable update of the bound remote/ref. A timeout or unconfirmed remote result first enters `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks enter `reconciliation_required`, and neither state permits blind retry.

NFR24: Failure visibility must expose state, cause category, retryability, and correlation ID without providing automated remediation in MVP.

#### Performance and Query Bounds

NFR25: Command submission must acknowledge accepted lifecycle commands within 1 second p95 before asynchronous provider or workspace work continues.

NFR26: Status and audit summary queries must return within 500 ms p95 for bounded MVP inputs.

NFR27: Context queries must return within 2 seconds p95 for bounded MVP inputs.

NFR28: Performance targets apply to bounded MVP inputs and control-plane responses. Targets must be validated against implementation benchmarks and recalibrated before release if provider or runtime constraints make the initial target misleading.

NFR29: Provider and workspace operations may complete asynchronously when external Git provider latency or workspace size exceeds interactive response budgets; callers must receive operation identity and status visibility rather than blocking indefinitely.

NFR30: Context queries accept at most 100 requested paths; return at most 2,000 tree entries or 500 search/glob results; allow at most 262,144 bytes for one bounded range and 1,048,576 serialized bytes for the aggregate response; and stop after 2 seconds of server execution. Excess input returns the stable input-limit result without partial execution. Supported result truncation occurs only after authorization/path filtering and sets one `isTruncated` flag; file content is never silently truncated.

NFR31: Query-limit audit evidence includes family, configured limit, actual count/bytes, elapsed time, truncation, safe category, and correlation ID, but excludes raw query text, file content, path lists, and unauthorized existence.

NFR32: File tree, search, glob, metadata, and bounded range queries must protect the service from unbounded workspace scans.

NFR33: Large file and binary handling limits must be explicit before MVP release; unsupported files must fail with stable policy errors rather than causing unbounded processing.

NFR34: Provider calls must use explicit timeout budgets, retry limits, and backoff caps.

NFR35: Provider calls must report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states rather than leaving callers waiting indefinitely.

NFR36: Provider rate-limit responses must preserve retry hints where available and expose retry-after or classified retryability.

#### Scalability and Capacity

NFR37: The MVP release calibration must support 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent agent tasks per tenant, and at least 1 lifecycle operation per second without cross-tenant or cross-task interference.

NFR38: Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.

NFR39: Audit, timeline, and file-context projections must remain queryable as folder history grows.

NFR40: Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.

NFR41: Capacity claims beyond the approved C1/C5 release-calibration units require new evidence and are not implied by this PRD.

#### Integration and Contract Compatibility

NFR42: REST, CLI, MCP, and SDK surfaces must preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes; transport shape and UX may differ.

NFR43: Public contracts must be versioned. Breaking changes to lifecycle commands, queries, error categories, workspace states, provider capabilities, or audit fields require an explicit new versioned contract.

NFR44: The product must support at least the active contract version and define a deprecation policy before removing any public lifecycle contract.

NFR45: Shared or generated contract tests must validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.

NFR46: The OpenAPI 3.1 Contract Spine is the canonical operation/schema authority; the generated SDK is the typed canonical client; CLI and MCP wrap it; REST emitted schemas validate against the spine. Every current Contract Spine operation has exactly one C13 parity row.

NFR47: GitHub and Forgejo support must be validated through provider contract tests before either provider is marked ready.

NFR48: Provider contract tests must cover only MVP-dependent lifecycle behavior: readiness, repository binding, branch/ref handling, file operations, commit, status, provider errors, and failure behavior.

NFR49: Supported GitHub and Forgejo products, instance/API versions, accepted credential/authentication profiles, and behavior assumptions must be published and recorded so compatibility drift is visible; unknown compatibility cannot be marked ready.

NFR50: Provider capability differences must be reported explicitly instead of inferred by clients from failed operations.

NFR51: Provider failures such as timeout, rate limit, authentication failure, authorization failure, repository missing, repository conflict, branch/ref conflict, unavailable provider, invalid path, commit rejected, and unknown outcome must map to stable product error categories.

#### Observability, Auditability, and Replay

NFR52: Every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.

NFR53: Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.

NFR54: Paths, commit messages, repository names, and branch names are tenant-sensitive by default under C9; authorized tenant/scoped-operator views may display them, cross-tenant/external diagnostics redact them, and a tenant confidential override stores only the stable tenant-scoped correlation token at audit/projection write time. Confidential incident reconstruction links operations through that token and operation/correlation identity; it does not promise recovery of the original cleartext. Provider payloads, file bodies, secrets, and generated context remain forbidden.

NFR55: Operations-console views are projection-first, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata. During projection degradation, the bounded incident view may expose redacted event evidence only with incident-admin plus normal tenant/folder access, a persistent warning, last checkpoint, correlation ID, and time window.

NFR56: Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.

NFR57: Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.

NFR58: The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.

NFR59: Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.

#### Data Retention and Cleanup

NFR60: C3 retention is binding: audit metadata and commit-idempotency records are retained 7 years; workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, and normalized auth-claim metadata are retained 400 days; read models are retained 400 days or until rebuilt, whichever is sooner; temporary working files are deleted 7 days after task-terminal closure and no active task; folder metadata and tombstones remain for the tenant lifetime plus 400 days after the approved deletion workflow, subject to legal hold.

NFR61: Tenant deletion anonymizes user display aliases while preserving metadata-only audit correlation/category/timestamp/outcome evidence; task-local display labels are tombstoned, secrets/content are deleted, and retained identifiers remain bounded by C3.

NFR62: Workspace cleanup is platform-owned and automatic only after task-terminal closure and no active task. Failed/inaccessible closure records final metadata-only evidence and operator disposition before the C3 seven-day observation window starts. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are excluded. Cleanup retries idempotently; MVP exposes pending/retrying/completed/failed status but no user-triggered cleanup or repair action.

NFR63: Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID.

NFR64: No cleanup process may remove audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations.

#### Operations Console Accessibility

NFR65: Read-only operations console flows must target WCAG 2.2 AA.

NFR66: The console must support keyboard navigation for primary diagnostic workflows.

NFR67: Status, failure, readiness, and lock indicators must not rely on color alone.

NFR68: Console screens must provide visible focus states, semantic headings, readable table structure, and sufficient contrast.

NFR69: Console text, controls, and tables must remain readable at common browser zoom levels used by operators.

#### Verification Expectations

NFR70: Each NFR category must have at least one automated verification path or documented manual validation path before MVP release.

NFR71: Security, tenant isolation, idempotency, provider contract, read-model determinism, and cross-surface contract compatibility NFRs must have automated tests.

NFR72: Performance, accessibility, retention, backup/recovery, and operations-console usability NFRs must have release validation evidence before MVP acceptance.

NFR73: Security verification must include dependency/package scanning, generated artifact review, and least-privilege provider credential validation.

**Total NFRs: 73**

### Additional Requirements

#### Contract and Scope Constraints

- Product authority is split deliberately: this PRD governs product intent, actors, scope, safety invariants, and user-visible outcomes; the OpenAPI 3.1 Contract Spine governs operation names, wire schemas, and closed error fields. Conflicts block release until both artifacts are reconciled and re-approved.
- The generated SDK is the typed canonical client, REST is the required public runtime transport, and CLI and MCP wrap the SDK. The read-only console is diagnostic and does not define an independent command model.
- C13 is the surface-coverage denominator. Every current Contract Spine operation requires exactly one parity declaration, and the release snapshot freezes the Contract Spine and C13 inventory versions and digests.
- MVP is repository-backed first and supports GitHub and Forgejo. Unmanaged local-folder import, local-first promotion, repair commands, incoming provider webhooks, cross-workspace body-content indexing, first-class rename, simultaneous multi-agent writes, archived-folder restore, hard deletion, provider-repository deletion, and history rewriting are excluded.
- The console is read-only. It cannot mutate or repair state, reveal credentials, browse/edit file content, show raw diffs, or provide unrestricted filesystem/event access.

#### Domain and Integration Constraints

- Hexalith.Tenants remains authoritative for tenant identity, lifecycle, and membership. Hexalith.EventStore supplies platform-owned command, aggregate, event, projection, query, cursor, read-model, and domain-service mechanics.
- Hexalith.Folders owns folder-specific policy, ACLs, provider-binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.
- File bodies and temporary working-copy material remain outside EventStore. Events, projections, logs, traces, metrics, diagnostics, errors, audit, and console views remain metadata-only.
- Provider behavior must sit behind explicit ports and provider contract tests. Unknown or incompatible GitHub/Forgejo capability evidence cannot report readiness.
- Public API begins at `v1`; DTOs and events require schema/versioning tests and backward-compatible evolution or explicit versioning for breaking changes.

#### Approved Quantitative Constraints

- C1/C5 capacity: 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent agent tasks per tenant, and at least 1 lifecycle operation per second.
- C2 freshness: 500 ms commit-to-status visibility lag in the approved calibration path.
- C3 retention: 7 years for audit and commit-idempotency metadata; 400 days for workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, copied auth claims, and most read models; temporary working files are deleted seven days after task-terminal closure with no active task.
- C4 query bounds: 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and a two-second server execution limit.
- C6 fixes the canonical workspace lifecycle, separate lock states, and derived operator dispositions.
- C9 classifies paths, repository/branch names, and commit messages as tenant-sensitive by default and governs stable token replacement for confidential tenants.

#### Open Release Items

- OQ1: Approve C7 lock-renewal, authorization-revalidation, and revocation-effect timing.
- OQ2: Publish and approve the canonical file-policy vocabulary and exact behavior.
- OQ3: Publish and approve the actor/access-state × protected-operation authorization matrix.
- OQ4: Publish and approve the GitHub/Forgejo provider compatibility catalog.
- OQ5: Produce non-empty, security-trimmed FR58 search and indexing-status evidence.
- OQ6: Replace seed-only console diagnostics with projection-backed evidence.
- OQ7: Align architecture and evidence to the canonical tenant/provider/repository/ref lock identity and alias collision behavior.
- OQ8: Align architecture, Contract Spine, SDK, and C13 evidence to all-mutations idempotency and read-key rejection.
- OQ9: Prove incident access requires incident-admin plus current tenant/folder authorization with C9 redaction and denial audit.
- OQ10: Publish and approve the release-calibration plan for SM1–SM8 and CM1–CM4.

Each open item remains release-blocking until its canonical evidence exists, all accountable approvers record identity and date, and the governance record stores approved status plus evidence version/digest.

### PRD Completeness Assessment

The PRD is structurally comprehensive and unusually explicit: it defines 58 numbered functional requirements, 73 testable non-functional requirement statements, canonical state vocabularies, public-surface authority, quantitative bounds, retention classes, safety invariants, scope exclusions, and release evidence expectations. Requirements are generally specific enough for traceability and acceptance analysis.

Completeness is not equivalent to release readiness. The PRD intentionally leaves ten release-blocking decisions or evidence packages open. The most material specification gaps are the C7 lock/revalidation timings, canonical file-policy behavior, complete authorization matrix, and provider compatibility catalog; the remaining items primarily require implementation alignment and acceptance evidence. These open items must be traced against architecture and epics in later steps.

## Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document explicitly claims the following coverage:

- Epic 1: FR1, FR2, FR3, FR43, FR47, FR50, FR51
- Epic 2: FR4, FR5, FR6, FR8, FR9, FR10, FR11, FR12, FR13, FR14
- Epic 3: FR7, FR15, FR16, FR17, FR18, FR19, FR20, FR21, FR22, FR23, FR57
- Epic 4: FR24, FR25, FR26, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34, FR35, FR37, FR38, FR39, FR40, FR41, FR42, FR43, FR44, FR45, FR46, FR55
- Epic 5: FR47, FR48, FR49, FR50, FR51
- Epic 6: FR31, FR36, FR45, FR46, FR52, FR53, FR54, FR55, FR56, FR57
- Epic 10: FR58
- Workstream 7 and Epics 8, 9, and 11 claim cross-cutting validation, closure, infrastructure, or refactoring support rather than new product FR scope.

**Total distinct FR numbers claimed in epics: 58**

### Coverage Matrix

The requirement column uses concise labels; the complete authoritative PRD wording is preserved in the preceding PRD Analysis section. “Partial” means the FR number is claimed but one or more binding clauses from the current PRD are absent, stale, or contradicted by the story text.

| FR | PRD requirement | Epic/story coverage | Status |
| --- | --- | --- | --- |
| FR1 | Consistent glossary terms and casing, enforced by documentation/schema checks | Epic 1: Stories 1.6, 1.13; Workstream 7: Story 7.13 | ✓ Covered |
| FR2 | Ordered canonical lifecycle on every required surface, including failures and cleanup visibility | Epic 1: Story 1.6; Epic 5: Stories 5.5, 5.7; Workstream 7: Story 7.13 | ✓ Covered |
| FR3 | C13 mutation/read classification; all mutations idempotent; reads reject keys | Epic 1: Stories 1.5–1.11, 1.13 | ✓ Covered |
| FR4 | Tenant-admin ownership of all Folders configuration; operators validate only | Epic 2: Stories 2.2, 2.4, 2.8; Epic 3: Stories 3.1, 3.8 | ⚠ Partial — Story 3.1 assigns provider configuration to platform engineers |
| FR5 | Grant/revoke folder access for users, groups, roles, and delegated agents | Epic 2: Story 2.4 | ✓ Covered |
| FR6 | Inspect effective folder/task permissions | Epic 2: Story 2.5 | ✓ Covered |
| FR7 | Inspect tenant readiness for repository-backed tasks | Epic 3: Stories 3.5, 3.9 | ✓ Covered |
| FR8 | Evaluate tenant, principal, delegation, provider, repository, folder, workspace, and task scope | Epic 2: Story 2.6 | ✓ Covered |
| FR9 | Deny unauthorized/cross-tenant access before any protected observation or touch | Epic 2: Story 2.6; Epic 4: Story 4.16 | ✓ Covered |
| FR10 | Safe authorization evidence for allows and denials | Epic 2: Story 2.6; Epic 4: Story 4.14 | ✓ Covered |
| FR11 | Create tenant-scoped logical folders | Epic 2: Story 2.3 | ✓ Covered |
| FR12 | Inspect folder lifecycle and binding | Epic 2: Story 2.7 | ✓ Covered |
| FR13 | Archive only from eligible safe states; stable mutation denial; provider untouched | Epic 2: Story 2.8 | ⚠ Partial — explicit eligibility matrix, provider non-mutation, and governance exceptions are absent |
| FR14 | Retention-class-specific archived views with safe expiry markers | Epic 2: Story 2.8; Workstream 7: Story 7.11 | ⚠ Partial — retention is covered, but per-field expiry/marker behavior is not |
| FR15 | Tenant admins configure provider policy; platform engineers validate | Epic 3: Story 3.1 | ⚠ Partial — story actor contradicts the current PRD authority boundary |
| FR16 | Validate provider readiness before create/bind | Epic 3: Story 3.5 | ✓ Covered |
| FR17 | Safe readiness diagnosis, retryability, remediation, provider ref, correlation | Epic 3: Story 3.5 | ✓ Covered |
| FR18 | Create repository-backed folder after readiness | Epic 3: Story 3.6 | ✓ Covered |
| FR19 | Bind pre-created repository after access, duplicate/alias, and branch/ref checks | Epic 3: Story 3.7 | ⚠ Partial — duplicate/alias binding detection is not explicit |
| FR20 | Define/select branch/ref policy | Epic 3: Story 3.8 | ✓ Covered |
| FR21 | Expose safe provider/binding/ref/capability metadata | Epic 3: Stories 3.1, 3.9 | ✓ Covered |
| FR22 | Expose GitHub/Forgejo capability differences | Epic 3: Stories 3.2–3.4, 3.9 | ✓ Covered |
| FR23 | Inspect provider product/profile/version/capability readiness evidence | Epic 3: Stories 3.2–3.5, 3.9; Workstream 7: Story 7.15 | ✓ Covered |
| FR24 | Prepare only with readiness, binding, ref, fresh auth, and task validity | Epic 2: Story 2.6; Epic 4: Story 4.2 | ✓ Covered |
| FR25 | Lock canonical tenant/provider/repository/ref identity; aliases collide | Epic 4: Story 4.3 | ⚠ Partial — canonical serializing identity and alias collisions are not acceptance criteria |
| FR26 | Inspect permitted lock metadata | Epic 4: Stories 4.3, 4.4 | ✓ Covered |
| FR27 | Deterministic conflict denial, no side effects, one safe audit record | Epic 4: Stories 4.3, 4.14, 4.16 | ✓ Covered |
| FR28 | Only `unlocked/locked/expired/stale/revoked`, separate from lifecycle/disposition | Epic 4: Stories 4.1, 4.4 | ⚠ Partial — coverage map still uses stale `active/abandoned/interrupted/released` terms |
| FR29 | Safe owner release with replay, expiry, revoked, and non-owner semantics | Epic 4: Stories 4.4, 4.11 | ⚠ Partial — expired-key and revoked/non-owner behavior is not explicit |
| FR30 | Automatic seven-day cleanup only after eligible task-terminal closure | Epic 4: Story 4.10; Workstream 7: Story 7.11 | ⚠ Partial — eligibility exclusions, automatic ownership, and observation-window rules are incomplete |
| FR31 | Inspect lifecycle, lock, disposition, freshness/checkpoint, retryability, and subsystem status | Epic 4: Story 4.9; Epic 6: Stories 6.3, 6.6; Epic 10: Story 10.2 | ✓ Covered |
| FR32 | One/many add/change/remove mutations under fresh lock, no auto-commit; rename is add+remove | Epic 4: Stories 4.6, 4.7 | ⚠ Partial — multi-mutation atomicity, no-auto-commit, and rename rule are not explicit |
| FR33 | Reject file operations violating every boundary/policy dimension | Epic 4: Stories 4.5, 4.16 | ✓ Covered |
| FR34 | Tree/metadata/glob/range/body search with exact C4 bounds | Epic 1: Stories 1.4, 1.9; Epic 4: Story 4.8 | ✓ Covered |
| FR35 | Authorization-before-shaping, bounded live snippets, explicit truncation/limit/logging rules | Epic 1: Story 1.9; Epic 4: Stories 4.8, 4.14, 4.16 | ⚠ Partial — exact result and no-raw-query/path-list logging clauses are not explicit |
| FR36 | Console remains read-only without file-content browsing/editing | Epic 6: Stories 6.2, 6.11 | ✓ Covered |
| FR37 | Fresh-authorized provider-confirmed commit; unknown then bounded reconciliation | Epic 2: Story 2.6; Epic 4: Story 4.12 | ✓ Covered |
| FR38 | Closed metadata constraints and pre-emission secret/content rejection | Epic 4: Stories 4.12, 4.14 | ✓ Covered |
| FR39 | C9-classified metadata-only task/commit evidence | Epic 4: Stories 4.9, 4.12, 4.14 | ✓ Covered |
| FR40 | Stable failure/retry/duplicate evidence and unknown/reconciliation actions | Epic 4: Stories 4.13, 4.14 | ✓ Covered |
| FR41 | All mutations replay safely within tier; expired keys reject without execution | Epic 1: Story 1.5; Epic 4: Story 4.11 | ⚠ Partial — expired-key behavior and full mutation denominator are not explicit in story ACs |
| FR42 | Conflict while live; expired-key result after expiry; reads reject keys | Epic 1: Story 1.5; Epic 4: Story 4.11 | ⚠ Partial — expired-key behavior is absent from the story text |
| FR43 | Canonical closed error shape on every surface | Epic 1: Story 1.6; Epic 4: Story 4.13 | ✓ Covered |
| FR44 | Complete error categories including expired key, unknown outcome, reconciliation | Epic 1: Story 1.6; Epic 4: Stories 4.12, 4.13 | ⚠ Partial — stable `idempotency_key_expired` behavior is not planned explicitly |
| FR45 | Complete canonical lifecycle plus separate lock vocabulary | Epic 1: Story 1.6; Epic 4: Stories 4.1, 4.9; Epic 6: Story 6.3 | ✓ Covered — C6 story carries the full state model despite stale map prose |
| FR46 | Failure evidence for lifecycle, auth, index, and read-model failures | Epic 4: Story 4.13; Epic 6: Story 6.6; Epic 10: Stories 10.2, 10.5 | ⚠ Partial — index-failure evidence is not explicit in the relevant ACs |
| FR47 | REST serves every current Contract Spine operation and validates emitted schemas | Epic 1: Stories 1.6–1.14; Epic 5: Story 5.5; Epic 8: Stories 8.1–8.3; Epic 10: Story 10.5 | ✓ Covered — Epic 8's hard-coded 47 count is stale against the current 49-row inventory |
| FR48 | CLI canonical lifecycle parity | Epic 5: Stories 5.2, 5.6, 5.7 | ✓ Covered |
| FR49 | MCP canonical lifecycle parity | Epic 5: Stories 5.3, 5.6, 5.7 | ✓ Covered |
| FR50 | SDK canonical lifecycle parity | Epic 1: Story 1.12; Epic 5: Stories 5.1, 5.5, 5.7 | ✓ Covered |
| FR51 | Cross-surface authorization/error/ID/audit/state/provider equivalence | Epic 1: Story 1.13; Epic 5: Stories 5.4–5.7 | ✓ Covered |
| FR52 | Tenant-scoped read-only operational diagnostics without global browsing | Epic 6: Stories 6.6, 6.7 | ✓ Covered |
| FR53 | Metadata-only audit inspection for all outcome types | Epic 6: Stories 6.1, 6.8 | ✓ Covered |
| FR54 | Immutable C9-classified incident reconstruction without bodies/hidden resources | Epic 4: Story 4.14; Epic 6: Stories 6.1, 6.8 | ✓ Covered |
| FR55 | Exclude all protected content from every output; visible redaction distinction | Epic 1: Story 1.15; Epic 4: Stories 4.14, 4.16; Epic 6: Stories 6.4, 6.11 | ✓ Covered |
| FR56 | Projection-first timeline; dual-authorized bounded incident evidence with guardrails | Epic 6: Story 6.9 | ⚠ Partial — story requires incident permission but not incident-admin plus current tenant/folder access |
| FR57 | Inspect GitHub/Forgejo support evidence | Epic 3: Story 3.9; Epic 6: Story 6.7; Workstream 7: Story 7.15 | ✓ Covered |
| FR58 | Authorized metadata-token search/status facade with trimming and hydration | Epic 10: Stories 10.1–10.6; Epic 11: Story 11.10 | ✓ Covered — implementation/evidence remains explicitly deferred/open |

### Missing Requirements

No FR number is wholly absent from the epics document. The gaps below are missing binding clauses within claimed coverage and therefore still require backlog correction.

#### Critical Missing FR Clauses

**FR4:** Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it.

- Impact: Story 3.1 currently grants configuration authority to a platform engineer, contradicting the PRD’s tenant-authority boundary and risking privileged policy mutation.
- Recommendation: Correct Stories 3.1 and 3.8 plus Epic 2/3 guardrails so tenant administrators mutate policy and scoped platform engineers only validate or diagnose.

**FR15:** Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.

- Impact: The provider-administration actor is reversed in Story 3.1, leaving the current authority requirement without a conforming story.
- Recommendation: Rewrite Story 3.1 actor and ACs, then add negative acceptance rows proving scoped operators cannot silently modify tenant provider policy.

**FR25:** Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.

- Impact: A workspace-scoped lock can permit two aliases of the same provider repository/ref to mutate concurrently, violating the canonical serializing identity.
- Recommendation: Amend Story 4.3 with the exact identity and alias-collision ACs and link it to OQ7/C6 evidence before subsequent mutation stories are considered ready.

**FR28:** Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.

- Impact: The coverage map still treats `active`, `abandoned`, `interrupted`, and `released` as lock states, creating contract and adapter vocabulary drift.
- Recommendation: Correct the FR coverage map and Stories 4.1/4.4 so all wire, projection, console, and parity evidence uses only the five current lock states.

**FR41:** Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as new intent.

- Impact: Story 4.11 covers replay and conflict but does not require the expired-key outcome or prove the complete current mutation denominator.
- Recommendation: Update Stories 1.5 and 4.11 and OQ8 acceptance evidence with generated all-mutation coverage and explicit expired-key/no-execution tests.

**FR42:** While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

- Impact: Expired-key precedence and read-key rejection are not captured together in any story acceptance criteria.
- Recommendation: Add explicit rows to Stories 1.5 and 4.11 for live-equivalent, live-conflict, expired-equivalent, expired-conflict, and read-with-key behavior.

**FR44:** The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.

- Impact: The current error coverage text predates expired-key, unknown-outcome, and reconciliation semantics; adapters can remain “green” against an incomplete error denominator.
- Recommendation: Update Stories 1.6 and 4.13 plus parity-oracle/error-catalog ACs to include the current closed taxonomy and exact expired-key client action.

**FR56:** Normal operation timelines come from projections; during projection degradation, the authorized incident view may expose bounded redacted event evidence with a persistent warning, last checkpoint, correlation ID, and time window, but no mutation or repair path.

- Impact: Story 6.9 requires only incident permission; it does not enforce the PRD’s combined incident-admin and current tenant/folder authorization rule, tracked by OQ9.
- Recommendation: Amend Story 6.9 and Story 11.10 with dual-authorization, revoked, wrong-tenant, hidden-resource, C9 redaction, and denial-audit acceptance rows.

#### High-Priority Missing FR Clauses

**FR13:** Archive eligibility must exclude active tasks/locks and `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required`; later mutations are denied safely and archive never mutates or deletes the provider repository.

- Impact: Story 2.8 delegates eligibility to unspecified policy and omits the provider non-mutation guarantee.
- Recommendation: Add the explicit state-denial matrix and provider no-touch assertions to Stories 2.8/2.8b.

**FR14:** Archived views must apply each C3 data-class period independently and expose a safe retention-expired marker when a field expires.

- Impact: Retention enforcement exists, but the user-visible mixed-retention behavior is not planned.
- Recommendation: Extend Stories 2.8 and 7.11 with per-class expiry and safe-marker tests.

**FR19:** Existing-repository binding must include duplicate/alias detection in addition to readiness, access, and branch/ref policy.

- Impact: Duplicate aliases can bypass one-binding and lock-identity assumptions.
- Recommendation: Add canonical repository identity, duplicate binding, and alias collision ACs to Story 3.7.

**FR29:** Lock release requires explicit replay, expired-key, revoked-lock, and non-owner outcomes.

- Impact: Story 4.4 covers valid release and dirty-state rejection but leaves several deterministic safety outcomes unspecified.
- Recommendation: Extend Stories 4.4 and 4.11 with the full release decision table.

**FR30:** Cleanup must be platform-owned and automatic, begin only after task-terminal closure/no active task, observe the C3 seven-day boundary, and exclude dirty/unknown/reconciliation states.

- Impact: Story 4.10 describes visibility but not the complete eligibility and timing contract; “interrupted/abandoned” wording may authorize unsafe cleanup.
- Recommendation: Reconcile Story 4.10 with Story 7.11 and add an explicit cleanup eligibility/timing matrix.

**FR32:** File mutation planning must cover one-or-many mutation commands, fresh authorization, no auto-commit, and the add-plus-remove rename rule.

- Impact: Stories 4.6/4.7 cover individual write/delete operations but not the task-level atomic request and commit boundary.
- Recommendation: Add shared ACs to Stories 4.6/4.7 or a dedicated task-change-set story before commit work.

**FR35:** Context-query planning must explicitly cover authorization-before-shaping, bounded result identity/location/snippets, `isTruncated`, no silent content truncation, stable excess results, and no raw query/path-list logging.

- Impact: Generic policy coverage can miss caller-visible response and telemetry constraints.
- Recommendation: Expand Stories 1.9, 4.8, and 4.14 with the exact current response and logging rules from FR35/C4/C9.

**FR46:** Failure evidence must include authorization, index, and read-model failures as well as lifecycle/provider failures.

- Impact: Index-failure evidence is not explicit in Epic 10’s acceptance criteria, despite being part of the current caller-visible failure contract.
- Recommendation: Add index unavailable/stale/failed evidence rows to Stories 4.13, 10.2, 10.5, and 11.10.

### Coverage Statistics

- Total PRD FRs: 58
- Distinct FR numbers claimed by epics: 58
- Fully semantically aligned FRs: 42
- Partially covered FRs with material missing/stale clauses: 16
- Completely unmapped PRD FRs: 0
- FR numbers present in epics but absent from the PRD: 0
- Claimed numeric coverage: 100%
- Verified full semantic coverage: 72.4%

The numeric coverage map is complete, but it overstates readiness because it was written against older requirement wording. The 16 partial rows must be reconciled before the epics can be treated as a faithful implementation path for the finalized 2026-07-14 PRD.

## UX Alignment Assessment

### UX Document Status

**Found:** `ux-design-specification.md` is a complete, whole-document UX specification with 32 stable traceability requirements (`UX-DR1`–`UX-DR32`), three critical user flows, six domain-specific component definitions, responsive behavior, and accessibility verification guidance. No sharded or competing UX document was found.

### UX ↔ PRD Alignment

#### Strong Alignment

- The UX centers the same PRD outcome: find a workspace, prove its tenant boundary, and understand its trust state from safe evidence.
- The read-only console boundary is consistent throughout: no mutation, repair, file editing, raw diffs, credential reveal, unrestricted browsing, or unauthorized-resource confirmation.
- Metadata-only folder orientation, audit timelines, provider readiness, lock/dirty/commit/failure evidence, freshness, correlation/task identifiers, and safe denial states trace directly to PRD journeys UJ5, UJ6, UJ8, and UJ9 and FR31, FR36, and FR52–FR57.
- Redacted, denied, inaccessible, unknown, missing, stale, unavailable, and failed states are deliberately distinguishable; this aligns with C9 and the PRD’s requirement that redaction never appear as missing data or silent truncation.
- WCAG 2.2 AA, keyboard access, visible focus, semantic structure, non-color-only status, responsive fallback, zoom testing, screen-reader review, and forced-colors testing align with NFR65–NFR73.
- UX-DR1 correctly requires Hexalith.FrontComposer Shell plus Fluent UI Blazor and prohibits a separate design system.
- FR58 is treated as a backend metadata-token discovery capability without adding content-preview or authorization-bypass UI.

#### Alignment Issues

**UX-PRD-1 — Global search scope is ambiguous (Critical).** UX-DR2 and several narrative sections call for “global search” across tenant, folder, workspace, repository binding, task, correlation ID, provider, state, and time window. The current PRD permits only explicitly authorized tenant/folder scope and forbids global cross-tenant browsing or resource inference.

- Required correction: Define “global” as global only within the caller’s already-authorized tenant/folder search scope. Authorization and safe-scope establishment must precede candidate lookup, filtering, result counting, suggestions, and empty-state classification.

**UX-PRD-2 — Canonical state and disposition vocabulary is stale/incomplete (High).** UX-DR13 requires canonical vocabulary, but UX-DR15 lists a mixed set of workspace states, folder lifecycle states, freshness states, and visibility states. It omits `requested`, `preparing`, `changes_staged`, `unknown_provider_outcome`, and `reconciliation_required` as explicit workspace lifecycle terms.

- Required correction: Model and display separate dimensions for workspace lifecycle, lock state, operator disposition, folder lifecycle, projection freshness, and visibility/redaction. Use the finalized PRD’s exact lowercase wire vocabulary.

**UX-PRD-3 — Automatic reconciliation experience is underspecified (High).** The PRD requires `unknown_provider_outcome` to enter bounded automatic evidence checks immediately, with no more than five checks and a 15-minute ceiling before `reconciliation_required`. It also requires last check, next scheduled check or escalation condition, and safe reason metadata.

- Required correction: Add explicit UX requirements and flow states for auto-reconciling progress, evidence-check budget, next scheduled check, exhausted/conflicting evidence, transition to human escalation, and prohibited retry/takeover actions.

**UX-PRD-4 — Incident-mode dual authorization is absent (Critical).** The UX specification covers degraded/stale/unavailable views but does not explicitly require incident-admin permission plus current tenant/folder authorization before bounded event evidence is shown.

- Required correction: Add the FR56/OQ9 dual-authorization rule, revoked/wrong-tenant/hidden-resource states, C9 redaction, persistent degraded warning, last checkpoint, bounded time window, correlation copy, and metadata-only denial audit to the UX traceability set.

### UX ↔ Architecture Alignment

#### Architectural Support Present

- Architecture explicitly takes the UX specification as an input and selects a FrontComposer-hosted Blazor interactive-server console using Fluent UI components.
- It supports projection-first read models, a Folders/Tenants user-context bridge, metadata-only query DTOs, workspace/readiness/audit/status projections, and a separate `folders-ui` service.
- Architecture maps the six UX domain components: Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree, Diagnostic Timeline, Trust Matrix, and Redaction/Inaccessibility State.
- F-4 through F-7 support operator-facing disposition labels, redaction affordances, guarded degraded mode, console page-load budgets, skeleton loading at 400 ms, and cancel affordance at two seconds.
- C9, path-policy authorization order, sentinel leakage tests, Dapr deny-by-default policy, tenant-prefixed caches, and safe read models provide the security foundation required by the UX.
- The automated `accessibility-gates`/axe CI path, UI E2E coverage, zoom/responsive checks, and Fluent UI foundation support UX-DR30–UX-DR32.

#### Architectural Gaps and Contradictions

**UX-ARCH-1 — Production diagnostic evidence is unpopulated (Critical blocker).** Architecture states that `IOpsConsoleDiagnosticsReadModel` and `IWorkspaceTransitionEvidenceReadModel` are seed-backed test seams only. No deployed projection populates them, so readiness, lock, dirty-state, failure, provider, sync, projection-freshness, and transition-evidence reads return safe-empty `NotFoundSafe` results.

- Impact: The core UX promise—real workspace trust, diagnosis, and incident evidence—cannot be achieved in a deployed host despite the UI being structurally safe.
- Required correction: Complete Epic 11 Story 11.10 to author and register EventStore-backed projections, then prove populated positive, degraded, replay, authorization, and live DCP-capable scenarios. This is the architecture counterpart of PRD OQ6.

**UX-ARCH-2 — Incident authorization is weaker than the PRD (Critical blocker).** Architecture F-6 grants incident-stream access using `eventstore:permission=admin`; it does not require the same actor to retain current normal tenant/folder authorization. This conflicts with FR56 and OQ9.

- Impact: An incident administrator could observe protected tenant/folder evidence outside current scope.
- Required correction: Amend F-6, Story 6.9, and Story 11.10 to require both authorities before event observation and to audit safe denials.

**UX-ARCH-3 — Unknown-outcome disposition conflicts with the finalized PRD (High).** The C6 table labels `unknown_provider_outcome` as `awaiting-human`; the PRD defines it as auto-reconciling during the bounded automatic evidence-check window. Architecture also describes it inconsistently as immediately entering `reconciliation_required` in older sections.

- Impact: Console labels and client guidance can instruct premature human action or hide active automatic reconciliation.
- Required correction: Reconcile C6, process patterns, F-4 mappings, UX labels, API/SDK/CLI/MCP states, and transition tests to `auto-reconciling` during the bounded check budget, then `awaiting-human` only after transition to `reconciliation_required`.

**UX-ARCH-4 — Architecture structure does not fully project the UX component/page model (High).** The conceptual architecture names all six custom components and search-first flows, but the “complete” directory tree lists only disposition, technical-state, redaction, copy, skeleton, and loading components. It does not map Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree, Diagnostic Timeline, Trust Matrix, global authorized search/results, or access-evidence pages.

- Impact: Implementers can satisfy the architecture tree while omitting several normative UX-DR components and the primary discovery flow.
- Required correction: Update the architecture structure and requirements-to-structure mapping with the missing components, pages, query ports, projections, and authorization-before-search boundaries.

**UX-ARCH-5 — Hosting terminology is internally stale (Medium).** Architecture alternates between “Blazor Server” and the newer “Blazor Web App with Interactive Server rendering” used by Epic 6 and the project context.

- Required correction: Normalize the architecture to the current Blazor Web App/Interactive Server model and its exact FrontComposer/Fluent UI V5 integration seam.

**UX-ARCH-6 — UX implementation phases are ambiguous (Medium).** The UX roadmap puts Diagnostic Timeline, Trust Matrix, Provider Readiness Evidence, and Access Evidence in “Phase 2,” while the current MVP PRD and Epic 6 require those capabilities for release.

- Required correction: Clarify that UX phases are implementation sequencing within the MVP, not product-release deferrals, or move any truly deferred component out of MVP acceptance explicitly.

### Warnings

- The finalized PRD was updated on 2026-07-14 after the UX and architecture baselines. Both downstream documents contain stale terminology and must be reconciled to the current authority hierarchy before implementation acceptance.
- The architecture’s own “READY WITH MINOR GAPS” conclusion predates and conflicts with its later documented production read-model limitations and the PRD’s OQ6/OQ9 release blockers. The current assessment must use the later facts, not the older readiness conclusion.
- Safe-empty production behavior prevents leakage but does not satisfy a diagnostic product outcome. “Fail-safe” and “implemented UX capability” must not be treated as equivalent.

### UX Alignment Conclusion

The UX concept is coherent and well specified, and the architecture supports most foundational choices. Alignment is **not implementation-ready** until the production diagnostic projections and dual-authorized incident path are completed. State/disposition vocabulary, authorized search scope, automatic reconciliation UX, and the architecture’s concrete page/component mapping also require reconciliation with the finalized PRD.

## Epic Quality Review

### Review Baseline

- Declared metadata: 11 epics, 115 stories, 6 product epics.
- Actual headings: 11 numbered epics plus Release Readiness Workstream 7, containing **116 story headings**.
- BDD form: all 116 stories contain at least one Given/When/Then sequence.
- Scenario depth: 113 of 116 stories contain exactly one Given/When/Then sequence; Story 3.4 has two When/Then paths, while Stories 10.6 and 11.7 contain three full scenarios.
- The epics document declares its ACs “terse planning ACs” and points to separate implementation story files for authoritative as-built detail. That external detail may help execution, but it does not make this canonical planning artifact independently implementation-ready.

### Epic Compliance Matrix

| Epic/workstream | User-value focus | No future-epic dependency | Story sizing | AC completeness | Current FR traceability | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Epic 1 — Bootstrap Canonical Contract | ✗ Primarily scaffold, schemas, codegen, fixtures, and CI | ✓ | ✗ Several multi-capability stories | ⚠ Mostly one happy-path planning scenario | ⚠ Current PRD clauses have drifted | 🔴 Technical runway presented as a product epic |
| Epic 2 — Folder Access and Lifecycle | ✓ Tenant-admin and authorized-user outcomes | ✗ Story 2.8 required later Story 2.8b to become real | ⚠ Story 2.6 is broad | ⚠ Denial/state/idempotency matrices are sparse | ⚠ FR4/13/14 gaps | 🔴 Independence defect; otherwise user-centric |
| Epic 3 — Provider Readiness and Binding | ✓ Clear provider-readiness user outcome | ✓ | ✗ Story 3.4 combines adapter, lifecycle behavior, versions, drift, and contract tests | ⚠ Failure and authority rows incomplete | ⚠ FR15/19 gaps | 🟠 Valuable but not fully refined |
| Epic 4 — Workspace Task Lifecycle | ✓ Core product job | ✗ Production transition evidence deferred to Epic 11 Story 11.10 | ✗ Multiple epic-sized/risk-bundled stories | ⚠ Critical state, idempotency, and failure rows missing | ⚠ Eight partial FR rows touch this epic | 🔴 Cannot deliver its stated outcome independently |
| Epic 5 — Cross-Surface Parity | ✓ Users receive coherent behavior across surfaces | ✓ | ⚠ Golden/mixed-surface scenarios are broad | ⚠ Oracle helps, but planning ACs omit current expired-key rules | ✓/⚠ Mostly traceable | 🟠 Sound structure with stale contract details |
| Epic 6 — Trust Console and Audit | ✓ Strong operator/auditor value | ✗ Production diagnostics deferred to Epic 11 Story 11.10 | ✗ Wireflow, page bundle, and accessibility verification are oversized | ⚠ Incident authorization and safe-empty cases incomplete | ⚠ FR56 gap | 🔴 Console is safe but cannot deliver populated production diagnosis |
| Workstream 7 — Release Readiness | ✗ Intentionally technical governance/evidence | ✓ | ⚠ Some broad evidence stories | ⚠ Often one umbrella scenario | NFR evidence, no new FR | 🟠 Correct as a separate workstream; must not be treated as a product epic |
| Epic 8 — Release Acceptance Closure | ✗ Corrective route/test/governance closure | ✓ | ✗ Stories 8.1, 8.2, and 8.5 are multi-lane bundles | ⚠ Hard-coded 47-operation denominator is stale | No new FR | 🔴 Release-closure plan mislabeled as an epic |
| Epic 9 — AppHost/Memory Topology | ✗ Infrastructure alignment | ✗ Routing is explicitly dormant until future Epic 10 | ⚠ Technical slices are reasonably bounded | ⚠ Mostly structural assertions | No new FR | 🔴 Technical epic with an explicit future dependency |
| Epic 10 — Search-Index Capability | ✓ Eventual FR58 user capability | ✗ Deployed facade remains empty/unavailable until Epic 11 Story 11.10 | ✗ Early stories are horizontal technical layers; Story 10.6 is broad | ⚠ Search/status, failure, and release evidence incomplete | ✓ number mapped; evidence open | 🔴 User outcome is not independently deliverable |
| Epic 11 — Platform Refactoring | ✗ Refactoring, deduplication, package seams, and governance | ✓ sequentially, but relies on cross-repository prerequisites | ✗ Several stories are epic-sized and multi-repository | ⚠ Behavior preservation is broad rather than scenario-complete | No new FR | 🔴 Technical refactoring program mislabeled as a product epic |

### 🔴 Critical Violations

#### EQ-C1 — Essential behavior is deferred to a future epic

Epics 4, 6, and 10 explicitly declare that essential production behavior is owned by future Epic 11 Story 11.10:

- Epic 4 cannot provide populated workspace transition evidence for FR46.
- Epic 6 cannot provide populated readiness, lock, dirty-state, failure, provider, sync, or projection-freshness diagnostics.
- Epic 10 cannot hydrate deployed search results or provide a functioning indexing-status facade.

This violates the rule that Epic N must not require Epic N+1 to deliver its outcome. Safe-empty output prevents leakage, but it is not the user value promised by those epics.

**Recommendation:** Move the required Story 11.10 projection/composition work into the owning product epics before their completion points, or mark Epics 4/6/10 incomplete and resequence Story 11.10 as prerequisite product work. Do not close these epics on interface-only or seed-only behavior.

#### EQ-C2 — Story 2.8 was knowingly non-completable without later Story 2.8b

The document states that Story 2.8 “could not reach done” until Story 2.8b wired the archive command into the real `/process` path. Story 2.8’s original acceptance path therefore depended on a later story and allowed unit-green/production-broken completion.

**Recommendation:** Merge 2.8b’s production-wiring and no-mock acceptance evidence into Story 2.8’s definition, or reframe 2.8 as contract/domain-only and make the vertical production slice a correctly sequenced story before any archive capability is claimed.

#### EQ-C3 — Technical programs are modeled as product epics

- Epic 1 is a scaffold/contract/codegen/CI runway. Its title is consumer-framed, but no consumer can complete a product job from it alone.
- Epic 8 is release-acceptance corrective work with no new product FR.
- Epic 9 is platform topology alignment with no product FR and no active value until Epic 10.
- Epic 11 is a multi-repository refactoring/governance program with no new product FR.

Workstream 7 correctly acknowledges this distinction, but Epics 8, 9, and 11 do not follow it consistently.

**Recommendation:** Keep user-value epics as the product backlog. Move contract runway, release closure, infrastructure alignment, and refactoring into explicitly separate enablement/release/technical-debt plans with their own gates. Where technical work is indispensable, attach the smallest enabling slice to the first user story it unlocks.

#### EQ-C4 — Epic 9 explicitly requires future Epic 10

Epic 9’s routing is described as dormant until Epic 10’s producer. It therefore cannot provide a usable outcome or validate its own value independently.

**Recommendation:** Combine a thin end-to-end publish/index/query proof into Epic 9, or treat Epic 9 solely as a technical enablement workstream rather than a product epic.

### 🟠 Major Issues

#### EQ-M1 — Story sizing is frequently too large

High-confidence oversized or multi-concern stories include:

- Stories 1.4, 1.7, 1.9, 1.11, 1.14, and 1.16: bundle multiple decision families, capability groups, or independent CI gates.
- Story 2.6: spans JWT, tenant projection, ACL, EventStore validators, Dapr policy, safe errors, and denial audit.
- Story 3.4: combines a Forgejo adapter, full lifecycle behavior, version snapshots, drift classification, nightly checks, and readiness gating.
- Story 4.1: implements and verifies the entire 11-state × event transition matrix.
- Story 4.8: implements six context-query families plus every authorization, content, and semantic-backend boundary.
- Story 4.13: spans failures across readiness, binding, preparation, locks, files, context, commit, cleanup, read models, and authorization.
- Story 4.16: combines sentinel redaction, path security, encoding equivalence, cross-tenant isolation, parallel locks, and interruption behavior.
- Stories 5.5 and 5.7: exercise broad nine-step, multi-surface lifecycle suites.
- Stories 6.5, 6.6, and 6.11: respectively cover the full 32-requirement wireflow, multiple diagnostic pages, and all automated/manual accessibility/responsive validation.
- Stories 8.1 and 8.2: implement eight and seven routes respectively; Story 8.5 spans multiple unrelated test baselines and CI masks.
- Story 10.6: combines materializer behavior, C4/C9 policy, cross-story governance, integration tests, live-environment evidence, and a future-content decision.
- Stories 11.2, 11.3, 11.5, 11.7, 11.8, 11.10, 11.11, and 11.13: span multiple modules, repositories, behavior families, gates, documentation, or release evidence. Story 11.10 alone contains at least four separable deliverables: domain-service admission/mapping, Memories bridge composition, diagnostics projections, and transition-evidence projections.

**Recommendation:** Split by independently testable behavior or risk family. Each resulting story should produce one vertical or one narrowly bounded enabling outcome and should not require simultaneous changes across unrelated gates.

#### EQ-M2 — Planning acceptance criteria are too terse for implementation readiness

All stories use BDD syntax, but syntax is not completeness. The one-scenario pattern omits predictable negative and boundary behavior in high-risk stories. Examples:

- Story 2.3 lacks duplicate, cross-tenant, archived/invalid state, idempotency replay/conflict/expiry, and real gateway-path cases.
- Story 2.4 lacks unknown/hidden principal, stale membership, delegated-scope, revocation, and safe-enumeration cases.
- Story 3.5 does not enumerate missing credential, insufficient permission, unavailable provider, invalid ref policy, repository conflict, rate limit, or unknown compatibility outcomes.
- Story 4.4 omits non-owner, revoked, expired-key, replay, dirty, unknown-outcome, and reconciliation rows.
- Story 4.12 omits single-commit task terminality, known retryable/non-retryable failures, bounded evidence checks, authorization revocation, and remote-ref confirmation variants.
- Story 6.9 omits current tenant/folder authorization, wrong-tenant, revoked, hidden-resource, and denial-audit scenarios.
- Story 10.5 omits non-empty success, stale/archived/revoked-hit dropping, index unavailable, bridge unavailable, and all-surface behavior.

**Recommendation:** Bring the authoritative scenario matrices into the planning story definitions or link versioned, immutable acceptance artifacts directly. At minimum, every safety-critical story needs happy, denied, stale/revoked, replay/conflict/expiry, unknown/reconciliation, and no-side-effect rows applicable to its behavior.

#### EQ-M3 — Current traceability is numeric rather than semantic

The epics map says 58/58, yet 16 FRs are only partially aligned with the finalized PRD. A story cannot claim traceability merely because it lists the same FR number.

**Recommendation:** Regenerate the FR coverage map from the finalized PRD and add clause-level links or acceptance rows for the 16 partial requirements identified in Epic Coverage Validation.

#### EQ-M4 — Story and epic metadata is inconsistent

Frontmatter declares `storyCount: 115`, but the document contains 116 story headings because Story 2.8b is counted structurally but not reflected in metadata.

**Impact:** Sprint metrics, coverage checks, and automated planning consumers can use the wrong denominator.

**Recommendation:** Normalize Story 2.8b to the project’s standard identifier scheme and update `storyCount`, sprint status, indexes, and cross-references atomically.

#### EQ-M5 — Several stories are horizontal technical layers with no independently consumable value

Examples include Stories 1.3, 2.1, 2.2, 2.9, 3.2, 10.1, 10.2, and most of Epic 11. These may be legitimate engineering tasks, but they are not independently valuable user stories.

**Recommendation:** Either label them explicitly as enablers with a bounded technical acceptance purpose or fold them into the first vertical user story that consumes them. Do not count their completion as product-value completion.

#### EQ-M6 — Epic 11 has an external multi-repository prerequisite hidden inside a story

Story 11.2 requires new or confirmed APIs across Commons, EventStore, FrontComposer, and Memories before downstream Folders refactors can proceed. This is a cross-repository delivery program, not a normal same-repository story.

**Recommendation:** Replace Story 11.2 with explicit upstream dependency records per repository, required API/version/commit, owner, approval, release availability, and fallback. Start dependent Folders stories only after the exact prerequisites are consumable.

### 🟡 Minor Concerns

- The `2.8b` identifier breaks the otherwise numeric story scheme and complicates stable sorting and automation.
- Epic 10’s title is implementation-centric (“Worker-Side Semantic-Indexing Producer And Bridge Projection”) despite having a user-facing FR58 outcome. Rename it around authorized metadata discovery and indexing status.
- Several stories use terms that have since changed in the PRD: stale lock-state labels, 47-operation parity counts, old idempotency scope, and old unknown-outcome disposition.
- The epics document mixes planned, as-built, corrective, reopened, done, and deferred status prose. A canonical planning artifact should distinguish current backlog contract from historical commentary more cleanly.
- The note delegating authoritative ACs to implementation story files creates two acceptance sources. Define one precedence rule and ensure the planning artifact cannot claim completeness when the referenced story file is missing or stale.

### Dependency Analysis

| Scope | Dependency finding | Compliance |
| --- | --- | --- |
| Epic 1 | Internal sequence is backward-only; starter → contracts → generation/gates | ✓ |
| Epic 2 | Story 2.8 requires later 2.8b production wiring | ✗ Critical |
| Epic 3 | Provider port precedes adapters; creation/binding follow readiness | ✓ |
| Epic 4 | Transition-evidence production capability deferred to Epic 11.10 | ✗ Critical |
| Epic 5 | Depends only on earlier contract/lifecycle work | ✓ |
| Epic 6 | Story 6.5 correctly gates later 6.6–6.10, but production diagnostics depend on Epic 11.10 | ✗ Critical overall |
| Workstream 7 | Consumes prior evidence; no future dependency found | ✓ as workstream |
| Epic 8 | Stories 8.1/8.2 precede 8.3; no future dependency found | ✓ sequence, though technical closure |
| Epic 9 | Routing remains dormant until future Epic 10 | ✗ Critical |
| Epic 10 | Internal producer/bridge/facade order is backward-only, but deployed facade depends on Epic 11.10 | ✗ Critical overall |
| Epic 11 | Story 11.2 precedes downstream adoption; Story 10.6 precedes 11.10. No later-epic dependency, but external repositories are blocking dependencies | ⚠ External coordination required |

### Special Implementation Checks

#### Starter Template

Architecture correctly states that no generic starter fits and selects a sibling-module pattern: Hexalith.Tenants structure plus EventStore.Admin CLI/MCP/UI conventions. Stories 1.1 and 1.2 cover scaffold and root configuration, so the starter requirement is present.

Minor improvement: pin the exact reference layout/version or source commit used for scaffolding and enumerate the expected project inventory in the AC rather than saying only “expected layout.”

#### Greenfield/Brownfield Posture

- Initial scaffold, root configuration, fixtures, Contract Spine, generation, and early CI gates are present.
- Existing-system integration with EventStore, Tenants, FrontComposer, Memories, Dapr, Aspire, GitHub, and Forgejo is explicit.
- No unmanaged local-folder migration is planned, consistently with the current MVP non-goal.

#### Persistence and Entity Timing

No “create every database/table up front” violation was found. EventStore aggregates, Dapr-backed projections, idempotency records, and bridge read models are introduced around the capabilities that need them. The Contract Spine defines public schemas early, which is intentional contract-first design rather than premature database design.

### Epic Quality Conclusion

The six primary product epics contain a coherent user-value sequence, but the backlog as a whole does not meet implementation-readiness standards. Future-epic dependencies prevent three product epics from delivering their outcomes, several technical programs are mislabeled as product epics, at least a dozen stories are materially oversized, and most planning ACs lack the negative/boundary scenarios required by the finalized PRD. The backlog requires structural correction before it can be treated as an independent, sequenced implementation plan.

## Summary and Recommendations

### Overall Readiness Status

## NOT READY

The planning set is not ready to authorize a clean Phase 4 implementation start. The PRD is strong, but the UX, architecture, and epics were not fully reconciled after the PRD’s 2026-07-14 finalization. Numeric FR coverage is 100%, while verified full semantic coverage is only 72.4%. Essential production behavior for three product epics is explicitly deferred to a future refactoring epic, and the deployed trust-console/search evidence paths remain safely empty rather than functionally complete.

Proceeding without correction would make implementation teams choose among conflicting authorities for tenant administration, lock identity/state, idempotency expiry, incident authorization, cleanup/archive behavior, and operator disposition. That is precisely the ambiguity an implementation-readiness gate is intended to stop.

### Critical Issues Requiring Immediate Action

1. **Reconcile the finalized PRD into every downstream artifact.** Sixteen FRs are only partially represented in the epics. The highest-risk drift concerns tenant-admin authority (FR4/FR15), archive/retention (FR13/FR14), canonical lock identity and vocabulary (FR25/FR28/FR29), all-mutations idempotency and expired keys (FR41/FR42/FR44), and dual-authorized incident evidence (FR56).

2. **Remove future-epic dependencies from product outcomes.** Epics 4, 6, and 10 cannot deliver their stated behavior until Epic 11 Story 11.10. Story 2.8 also required later Story 2.8b. Re-sequence or relocate the missing work into the owning product epics before those epics can be called complete.

3. **Implement and verify production diagnostic/read-model projections.** `IOpsConsoleDiagnosticsReadModel`, `IWorkspaceTransitionEvidenceReadModel`, and the Server-side semantic-index bridge are unpopulated or unavailable in deployed composition. Complete the projection authoring/wiring and prove populated positive, degraded, replay, authorization, and live search/status cases. This closes the core of OQ5 and OQ6.

4. **Fix the incident-view security contract.** Architecture F-6 and Story 6.9 currently rely on incident/admin permission without explicitly requiring current tenant/folder authorization. Enforce both authorities before any event observation, retain C9 redaction, and add wrong-tenant, revoked, hidden-resource, degraded, and denial-audit tests. This closes OQ9.

5. **Resolve state, reconciliation, and lock contradictions.** `unknown_provider_outcome` must be auto-reconciling during the bounded five-check/15-minute evidence budget and only become awaiting-human after `reconciliation_required`. Lock state must use only the finalized five-value vocabulary, and locks must serialize the canonical managed-tenant/provider/repository/ref identity including aliases.

6. **Close the explicit open release decisions/evidence.** OQ1–OQ10 remain declared blockers in the PRD. OQ1–OQ4 establish timing, file-policy, authorization, and provider denominators; OQ5–OQ9 close current implementation/evidence gaps; OQ10 freezes release-calibration evidence. Approval identity/date and evidence digest are required, not only implementation or green tests.

7. **Correct backlog structure before execution.** Separate technical runway/release/refactoring programs from product epics, split oversized stories, remove forward dependencies, update the story count from 115 to 116 or normalize the corrective story, and replace terse umbrella ACs with scenario-complete acceptance matrices.

### Recommended Next Steps

1. **Run one authority-reconciliation pass** across `prd.md`, `architecture.md`, `ux-design-specification.md`, `epics.md`, the OpenAPI Contract Spine, C13 parity inventory, error catalog, C6 matrix, and project context. Use the finalized PRD and current 49-row generated inventory as the starting authority; eliminate stale 47-operation, old lock-state, old idempotency, and old incident-access prose.

2. **Patch the 16 partial FR rows** using the exact recommendations in Epic Coverage Validation. Regenerate the FR coverage map from requirement clauses rather than preserving numeric claims manually.

3. **Rebuild the delivery structure around vertical value:**
   - keep product outcomes in user-value epics;
   - move Epic 1, Workstream 7, and Epics 8/9/11 into explicit enablement, release, infrastructure, and refactoring plans;
   - move the necessary projection/composition slices from Story 11.10 into the product epics they unblock;
   - split multi-route, multi-repository, multi-risk, and multi-gate stories.

4. **Close the security/contract decisions before further dependent work:** approve C7 timing (OQ1), publish file-policy behavior (OQ2), publish the complete authorization matrix (OQ3), approve the provider compatibility catalog (OQ4), align canonical lock identity (OQ7), align all-mutations idempotency/read-key rejection (OQ8), and approve dual incident authorization (OQ9).

5. **Complete the production evidence paths:** author and register the diagnostics, transition-evidence, and semantic-index bridge projections; replace safe-empty defaults; exercise real gateway and DCP-capable paths; record non-empty FR58 search/indexing status and populated console evidence.

6. **Expand acceptance criteria for every safety-critical story.** Add applicable rows for happy path, wrong tenant, hidden resource, stale/revoked authority, non-owner lock, alias collision, replay, conflict, expired key, known failure, unknown outcome, bounded reconciliation, cleanup/archive ineligibility, no side effects, safe audit, and metadata-only outputs.

7. **Repair planning metadata and traceability automation:** normalize Story 2.8b, correct the 116-story denominator, synchronize sprint status and indexes, and make CI fail when the planning FR text, C13 inventory, or referenced story acceptance artifact drifts.

8. **Re-run Implementation Readiness** after the artifact corrections and blocker evidence are complete. Do not carry the current “architecture ready” or “58/58 covered” labels forward without revalidation.

### Assessment Totals

This assessment documented **51 issue records across four categories**, with related risks intentionally cross-referenced:

- 16 partially covered functional requirements
- 10 UX/PRD/architecture alignment issues
- 15 epic-quality issues (4 critical, 6 major, 5 minor)
- 10 explicit open release items

The most important finding is not the raw count. It is that several “complete” or “covered” labels describe safe scaffolds, seed-only seams, or numeric mappings rather than deployed user outcomes. Those labels must be corrected before implementation planning can be trusted.

### Final Note

The artifacts contain a strong product contract and unusually thoughtful safety constraints. The remaining work is reconciliation and delivery integrity: make every downstream artifact tell the same story, ensure each product epic delivers its outcome without future rescue work, and require real production-path evidence where the product promises diagnostic or search behavior.

Address the critical issues before proceeding to implementation. If the team deliberately proceeds with an accepted exception, record the exact scope, owner, expiry/revisit condition, and evidence impact rather than treating the exception as readiness.

**Assessment date:** 2026-07-15  
**Assessor:** Codex — BMAD Implementation Readiness workflow
