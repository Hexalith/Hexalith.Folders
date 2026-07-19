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

### Discovery Issues

- No whole-versus-sharded duplicate formats were found.
- No required document type is missing.

## PRD Analysis

### Functional Requirements

- FR1: Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing.
- FR2: Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions.
- FR3: Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys.
- FR4: Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it.
- FR5: Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting verb scope is visible in effective permissions and auditable without exposing hidden principals.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.
- FR11: Authorized actors can create logical folders within a tenant.
- FR12: Authorized actors can inspect folder lifecycle and binding status.
- FR13: Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.
- FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.
- FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass.
- FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.
- FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
- FR23: Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready.
- FR24: Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect.
- FR25: Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata.
- FR28: Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.
- FR29: Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.
- FR30: Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.
- FR31: Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable.
- FR32: Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
- FR34: Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution.
- FR35: Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range/file content is never silently truncated, and unsupported excess returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.
- FR36: The operations console must remain read-only and excluded from file editing or file-content browsing capabilities.
- FR37: Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first enters `unknown_provider_outcome`; only exhausted/conflicting automatic evidence enters `reconciliation_required`.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission.
- FR39: The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification.
- FR40: The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation.
- FR41: Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as new intent.
- FR42: While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.
- FR43: Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility.
- FR44: The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.
- FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.
- FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.
- FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine.
- FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.
- FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.
- FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.
- FR56: Normal operation timelines come from projections; during projection degradation, the authorized incident view may expose bounded redacted event evidence with a persistent warning, last checkpoint, correlation ID, and time window, but no mutation or repair path.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.
- FR58: Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe.

**Total FRs: 58**

### Non-Functional Requirements

_The PRD presents these as unnumbered bullets. This assessment assigns NFR1–NFR73 in source order for traceability without changing their text._

- NFR1 [Security and Tenant Isolation]: Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, asynchronous provider side effect, and audit record. No incoming webhook ingestion exists in MVP.
- NFR2 [Security and Tenant Isolation]: Cross-tenant access leaks are zero-tolerance defects. No object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
- NFR3 [Security and Tenant Isolation]: Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, asynchronous work, audit records, index results, and context-query results.
- NFR4 [Security and Tenant Isolation]: File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
- NFR5 [Security and Tenant Isolation]: Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
- NFR6 [Security and Tenant Isolation]: Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.
- NFR7 [Security and Tenant Isolation]: Every mutation and asynchronous side effect must revalidate current tenant, folder, delegated-actor, binding, and credential authority before touching a protected resource; revocation fails closed and changes any held lock to revoked/inaccessible.
- NFR8 [Security and Tenant Isolation]: Paths, repository names, branch names, and commit messages are tenant-sensitive by default. Authorized tenant members and tenant-scoped operators with need-to-know may view them; cross-tenant/external diagnostics redact them. A tenant confidential override replaces cleartext at audit/projection write time with a stable tenant-scoped correlation token that preserves equality/linkage across authorized incident records but cannot reveal the original value. Redacted, hidden, unknown, missing, stale, and unavailable remain visibly distinct.
- NFR9 [Security and Tenant Isolation]: Credential references must be validated and displayed only as non-secret identifiers or status indicators.
- NFR10 [Security and Tenant Isolation]: Provider credentials and repository bindings must be tenant-scoped and must not be reused across tenants, even if repository URLs appear identical.
- NFR11 [Security and Tenant Isolation]: Provider credentials must use the least privilege required for supported lifecycle operations and must be validated against required provider capabilities before use.
- NFR12 [Security and Tenant Isolation]: Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.
- NFR13 [Reliability, Idempotency, and Failure Visibility]: Workspace lifecycle uses only the canonical lowercase wire states defined in the Glossary; lock state and generic operation-execution status are separate dimensions and must be labeled as such.
- NFR14 [Reliability, Idempotency, and Failure Visibility]: Every accepted operation exposes operation identity, workspace lifecycle, applicable lock state, projection freshness, and a terminal or inspectable non-terminal outcome.
- NFR15 [Reliability, Idempotency, and Failure Visibility]: Repository-backed task lifecycle operations must leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.
- NFR16 [Reliability, Idempotency, and Failure Visibility]: Unconfirmed external effects immediately enter `unknown_provider_outcome` and permit only bounded automatic read-only checks; exhausted or conflicting evidence enters `reconciliation_required`, blocks retry/mutation/takeover, and requires human escalation. These states never collapse into a generic failure.
- NFR17 [Reliability, Idempotency, and Failure Visibility]: Idempotency keys are required for every mutating Contract Spine operation; non-mutating operations reject them.
- NFR18 [Reliability, Idempotency, and Failure Visibility]: While the idempotency record is unexpired within its declared retention tier, a repeated call with the same key and equivalent payload must return the same logical result, and the same key with a conflicting payload must return an idempotency conflict. After expiry, either use returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new request.
- NFR19 [Reliability, Idempotency, and Failure Visibility]: Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.
- NFR20 [Reliability, Idempotency, and Failure Visibility]: Lock acquisition is deterministic and limited to one active writer per managed tenant plus canonical provider/repository identity plus normalized target ref; aliases resolving to that identity collide.
- NFR21 [Reliability, Idempotency, and Failure Visibility]: Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.
- NFR22 [Reliability, Idempotency, and Failure Visibility]: Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
- NFR23 [Reliability, Idempotency, and Failure Visibility]: A successful committed state requires provider-confirmed durable update of the bound remote/ref. A timeout or unconfirmed remote result first enters `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks enter `reconciliation_required`, and neither state permits blind retry.
- NFR24 [Reliability, Idempotency, and Failure Visibility]: Failure visibility must expose state, cause category, retryability, and correlation ID without providing automated remediation in MVP.
- NFR25 [Performance and Query Bounds]: Command submission must acknowledge accepted lifecycle commands within 1 second p95 before asynchronous provider or workspace work continues.
- NFR26 [Performance and Query Bounds]: Status and audit summary queries must return within 500 ms p95 for bounded MVP inputs.
- NFR27 [Performance and Query Bounds]: Context queries must return within 2 seconds p95 for bounded MVP inputs.
- NFR28 [Performance and Query Bounds]: Performance targets apply to bounded MVP inputs and control-plane responses. Targets must be validated against implementation benchmarks and recalibrated before release if provider or runtime constraints make the initial target misleading.
- NFR29 [Performance and Query Bounds]: Provider and workspace operations may complete asynchronously when external Git provider latency or workspace size exceeds interactive response budgets; callers must receive operation identity and status visibility rather than blocking indefinitely.
- NFR30 [Performance and Query Bounds]: Context queries accept at most 100 requested paths; return at most 2,000 tree entries or 500 search/glob results; allow at most 262,144 bytes for one bounded range and 1,048,576 serialized bytes for the aggregate response; and stop after 2 seconds of server execution. Excess input returns the stable input-limit result without partial execution. Supported result truncation occurs only after authorization/path filtering and sets one `isTruncated` flag; file content is never silently truncated.
- NFR31 [Performance and Query Bounds]: Query-limit audit evidence includes family, configured limit, actual count/bytes, elapsed time, truncation, safe category, and correlation ID, but excludes raw query text, file content, path lists, and unauthorized existence.
- NFR32 [Performance and Query Bounds]: File tree, search, glob, metadata, and bounded range queries must protect the service from unbounded workspace scans.
- NFR33 [Performance and Query Bounds]: Large file and binary handling limits must be explicit before MVP release; unsupported files must fail with stable policy errors rather than causing unbounded processing.
- NFR34 [Performance and Query Bounds]: Provider calls must use explicit timeout budgets, retry limits, and backoff caps.
- NFR35 [Performance and Query Bounds]: Provider calls must report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states rather than leaving callers waiting indefinitely.
- NFR36 [Performance and Query Bounds]: Provider rate-limit responses must preserve retry hints where available and expose retry-after or classified retryability.
- NFR37 [Scalability and Capacity]: The MVP release calibration must support 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent agent tasks per tenant, and at least 1 lifecycle operation per second without cross-tenant or cross-task interference.
- NFR38 [Scalability and Capacity]: Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.
- NFR39 [Scalability and Capacity]: Audit, timeline, and file-context projections must remain queryable as folder history grows.
- NFR40 [Scalability and Capacity]: Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.
- NFR41 [Scalability and Capacity]: Capacity claims beyond the approved C1/C5 release-calibration units require new evidence and are not implied by this PRD.
- NFR42 [Integration and Contract Compatibility]: REST, CLI, MCP, and SDK surfaces must preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes; transport shape and UX may differ.
- NFR43 [Integration and Contract Compatibility]: Public contracts must be versioned. Breaking changes to lifecycle commands, queries, error categories, workspace states, provider capabilities, or audit fields require an explicit new versioned contract.
- NFR44 [Integration and Contract Compatibility]: The product must support at least the active contract version and define a deprecation policy before removing any public lifecycle contract.
- NFR45 [Integration and Contract Compatibility]: Shared or generated contract tests must validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
- NFR46 [Integration and Contract Compatibility]: The OpenAPI 3.1 Contract Spine is the canonical operation/schema authority; the generated SDK is the typed canonical client; CLI and MCP wrap it; REST emitted schemas validate against the spine. Every current Contract Spine operation has exactly one C13 parity row.
- NFR47 [Integration and Contract Compatibility]: GitHub and Forgejo support must be validated through provider contract tests before either provider is marked ready.
- NFR48 [Integration and Contract Compatibility]: Provider contract tests must cover only MVP-dependent lifecycle behavior: readiness, repository binding, branch/ref handling, file operations, commit, status, provider errors, and failure behavior.
- NFR49 [Integration and Contract Compatibility]: Supported GitHub and Forgejo products, instance/API versions, accepted credential/authentication profiles, and behavior assumptions must be published and recorded so compatibility drift is visible; unknown compatibility cannot be marked ready.
- NFR50 [Integration and Contract Compatibility]: Provider capability differences must be reported explicitly instead of inferred by clients from failed operations.
- NFR51 [Integration and Contract Compatibility]: Provider failures such as timeout, rate limit, authentication failure, authorization failure, repository missing, repository conflict, branch/ref conflict, unavailable provider, invalid path, commit rejected, and unknown outcome must map to stable product error categories.
- NFR52 [Observability, Auditability, and Replay]: Every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
- NFR53 [Observability, Auditability, and Replay]: Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
- NFR54 [Observability, Auditability, and Replay]: Paths, commit messages, repository names, and branch names are tenant-sensitive by default under C9; authorized tenant/scoped-operator views may display them, cross-tenant/external diagnostics redact them, and a tenant confidential override stores only the stable tenant-scoped correlation token at audit/projection write time. Confidential incident reconstruction links operations through that token and operation/correlation identity; it does not promise recovery of the original cleartext. Provider payloads, file bodies, secrets, and generated context remain forbidden.
- NFR55 [Observability, Auditability, and Replay]: Operations-console views are projection-first, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata. During projection degradation, the bounded incident view may expose redacted event evidence only with incident-admin plus normal tenant/folder access, a persistent warning, last checkpoint, correlation ID, and time window.
- NFR56 [Observability, Auditability, and Replay]: Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.
- NFR57 [Observability, Auditability, and Replay]: Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.
- NFR58 [Observability, Auditability, and Replay]: The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.
- NFR59 [Observability, Auditability, and Replay]: Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.
- NFR60 [Data Retention and Cleanup]: C3 retention is binding: audit metadata and commit-idempotency records are retained 7 years; workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, and normalized auth-claim metadata are retained 400 days; read models are retained 400 days or until rebuilt, whichever is sooner; temporary working files are deleted 7 days after task-terminal closure and no active task; folder metadata and tombstones remain for the tenant lifetime plus 400 days after the approved deletion workflow, subject to legal hold.
- NFR61 [Data Retention and Cleanup]: Tenant deletion anonymizes user display aliases while preserving metadata-only audit correlation/category/timestamp/outcome evidence; task-local display labels are tombstoned, secrets/content are deleted, and retained identifiers remain bounded by C3.
- NFR62 [Data Retention and Cleanup]: Workspace cleanup is platform-owned and automatic only after task-terminal closure and no active task. Failed/inaccessible closure records final metadata-only evidence and operator disposition before the C3 seven-day observation window starts. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are excluded. Cleanup retries idempotently; MVP exposes pending/retrying/completed/failed status but no user-triggered cleanup or repair action.
- NFR63 [Data Retention and Cleanup]: Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID.
- NFR64 [Data Retention and Cleanup]: No cleanup process may remove audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations.
- NFR65 [Operations Console Accessibility]: Read-only operations console flows must target WCAG 2.2 AA.
- NFR66 [Operations Console Accessibility]: The console must support keyboard navigation for primary diagnostic workflows.
- NFR67 [Operations Console Accessibility]: Status, failure, readiness, and lock indicators must not rely on color alone.
- NFR68 [Operations Console Accessibility]: Console screens must provide visible focus states, semantic headings, readable table structure, and sufficient contrast.
- NFR69 [Operations Console Accessibility]: Console text, controls, and tables must remain readable at common browser zoom levels used by operators.
- NFR70 [Verification Expectations]: Each NFR category must have at least one automated verification path or documented manual validation path before MVP release.
- NFR71 [Verification Expectations]: Security, tenant isolation, idempotency, provider contract, read-model determinism, and cross-surface contract compatibility NFRs must have automated tests.
- NFR72 [Verification Expectations]: Performance, accessibility, retention, backup/recovery, and operations-console usability NFRs must have release validation evidence before MVP acceptance.
- NFR73 [Verification Expectations]: Security verification must include dependency/package scanning, generated artifact review, and least-privilege provider credential validation.

**Total NFRs: 73**

### Additional Requirements

- **Authority and drift:** The PRD governs product intent, actors, scope, safety invariants, and user-visible outcomes. The OpenAPI 3.1 Contract Spine governs operation names, wire schemas, and closed error fields. Authority conflicts fail the release gate and require reconciliation and re-approval; generated SDK, REST-emitted schema, CLI, MCP, parity oracle, and tests cannot override either source.
- **MVP boundary:** The release is repository-backed first and supports provider-backed creation plus binding of eligible pre-created repositories. Unmanaged local imports, migration assistance, local-first promotion, history rewriting, repair automation, archived-folder restore or deletion, incoming webhooks, first-class rename, and simultaneous multi-agent writes are excluded.
- **Surface contract:** REST, generated SDK, CLI, and MCP are required current-release surfaces. Every Contract Spine operation must have exactly one C13 parity row; an immutable version-and-digest snapshot of the spine and C13 inventory is required for release evidence.
- **Console boundary:** The operations console is read-only and projection-first. Its degraded incident view requires incident-admin permission plus normal tenant/folder authorization, remains bounded and metadata-only, and cannot expose mutation, repair, credentials, contents, diffs, unrestricted events, or filesystem browsing.
- **Platform ownership:** Hexalith.Tenants owns tenant identity, lifecycle, and membership. Hexalith.EventStore owns platform command/event/projection/query/domain-service mechanics. Shared technical capabilities belong in Hexalith.Commons, Hexalith.FrontComposer, and Hexalith.Memories. Hexalith.Folders owns folder policy and ACLs, binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.
- **Content boundary:** File bodies, diffs, generated context payloads, provider payloads or tokens, credential material, secrets, and unauthorized existence are forbidden from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console views.
- **Lifecycle and concurrency:** Workspace lifecycle, lock state, operation state, and operator disposition are distinct vocabularies. One active mutation writer is permitted per managed tenant plus canonical provider/repository identity plus normalized ref; aliases collide. Each task can produce at most one provider-confirmed durable commit.
- **Unknown outcomes:** Unconfirmed external effects enter `unknown_provider_outcome`; no more than five read-only evidence checks may run and the process must end within 15 minutes. Exhausted or conflicting evidence enters `reconciliation_required`. Blind retries, mutation, release, or takeover are prohibited in both states.
- **Idempotency:** Every mutation requires an idempotency key and every read rejects one. Equivalent unexpired replays return the same logical result; conflicting intent returns the canonical conflict; expired keys return `idempotency_key_expired` without execution. Mutation records use 24-hour retention except commit records, which use the C3 seven-year tier.
- **Fixed quantitative evidence:** C1/C5 capacity is 4 tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent tasks per tenant, and at least 1 lifecycle operation per second. C2 freshness is 500 ms. C3 defines seven-year, 400-day, and seven-day retention classes. C4 fixes context limits. C6, C9, and C13 are governed contracts. C7 timing remains pending.
- **Release calibration:** SM1–SM8 and CM1–CM4 require a frozen release-calibration plan defining populations, exclusions, environment, scenarios, measurement, evidence ownership, and approvals.
- **Open release blockers:** OQ1 C7 timing; OQ2 canonical file policy; OQ3 authorization matrix; OQ4 provider compatibility catalog; OQ5 non-empty FR58 search evidence; OQ6 projection-backed console evidence; OQ7 canonical lock identity alignment; OQ8 all-mutations idempotency alignment; OQ9 incident-access proof; and OQ10 release-calibration plan. Closure requires canonical evidence, named approvers, approval dates, and a governed version/digest.

### PRD Completeness Assessment

The PRD is comprehensive and internally structured: it defines actors and journeys, 58 functional requirements, 73 traceable non-functional statements, measurable outcomes, counter-metrics, explicit scope and non-goals, canonical state vocabularies, authority boundaries, quantitative limits, provider behavior, and release evidence expectations.

It is not yet implementation-ready on PRD evidence alone. Although its frontmatter marks it final, the PRD explicitly declares ten release-blocking open items. The most consequential specification gaps are the pending C7 lock/revalidation timings, canonical file-policy vocabulary, authorization-matrix denominator, and provider-compatibility catalog. The remaining six items require implementation and release evidence rather than new product scope. Epic coverage must therefore trace both FR/NFR requirements and OQ1–OQ10 closure work.

## Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document contains explicit mappings for FR1 through FR58. No epic-only FR identifiers were found.

- **Nominal identifier coverage:** 58 of 58 PRD FR identifiers
- **Semantic comparison basis:** current PRD text dated 2026-07-14 versus the epic mapping and story acceptance criteria last synchronized through 2026-07-07
- **Result:** 42 fully traced requirements and 16 partially traced requirements

### Coverage Matrix

| FR Number | Current PRD Requirement | Epic Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing. | Epic 1 — vocabulary in OpenAPI Contract Spine + `docs/contract-terms.md` | ✓ Covered |
| FR2 | Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions. | Epic 1 — lifecycle vocabulary via `x-hexalith-lifecycle-states` extension + diagrams | ✓ Covered |
| FR3 | Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys. | Epic 1 — command/query distinction in OpenAPI operation grouping + Server endpoint routing | ✓ Covered |
| FR4 | Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it. | Epic 2 — tenant administrator ACL configuration via `OrganizationAggregate` ACL baseline<br>**Gap:** The plan splits configuration across Epic 2 and a platform-engineer-led Story 3.1, but does not enforce tenant-administrator ownership of provider/binding policy or the rule that scoped operators may validate but not modify it. | ⚠ Partial |
| FR5 | Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting verb scope is visible in effective permissions and auditable without exposing hidden principals. | Epic 2 — folder access grant to users, groups, roles, and delegated service agents | ✓ Covered |
| FR6 | Authorized actors can inspect effective permissions for a folder or task context. | Epic 2 — effective-permissions inspection | ✓ Covered |
| FR7 | Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks. | Epic 3 — tenant readiness inspection (depends on provider configuration) | ✓ Covered |
| FR8 | The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope. | Epic 2 — layered authorization evaluation (foundation: JWT → claim transform → tenant projection → folder ACL → EventStore validators → Dapr policy) | ✓ Covered |
| FR9 | The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information. | Epic 2 — cross-tenant denial before any file/workspace/credential/repository/lock/commit/provider/audit access | ✓ Covered |
| FR10 | The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details. | Epic 2 — authorization evidence (allowed and denied) without unauthorized resource enumeration | ✓ Covered |
| FR11 | Authorized actors can create logical folders within a tenant. | Epic 2 — folder creation | ✓ Covered |
| FR12 | Authorized actors can inspect folder lifecycle and binding status. | Epic 2 — folder lifecycle and binding inspection | ✓ Covered |
| FR13 | Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive. | Epic 2 — folder archive<br>**Gap:** Story 2.8 uses an undefined eligibility predicate and omits the current no-active-task/lock and no staged/dirty/unknown/reconciliation preconditions, separately authorized governance exceptions, and provider-repository non-mutation rule. | ⚠ Partial |
| FR14 | Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden. | Epic 2 — audit and status evidence preservation for archived folders<br>**Gap:** The plan preserves audit/status under retention but does not trace per-field C3 expiry, omission, safe retention-expired markers, or the prohibition on extending shorter classes to seven years. | ⚠ Partial |
| FR15 | Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness. | Epic 3 — provider binding + credential reference configuration per tenant<br>**Gap:** Story 3.1 assigns configuration to a platform engineer and does not explicitly preserve tenant-administrator ownership of capability policy while limiting platform engineers to validation. | ⚠ Partial |
| FR16 | Authorized actors can validate provider readiness before repository-backed folder creation or binding. | Epic 3 — provider readiness validation before repository-backed creation/binding | ✓ Covered |
| FR17 | The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID. | Epic 3 — readiness diagnostics with safe reason codes, retryability, remediation category, provider reference, correlation ID | ✓ Covered |
| FR18 | Authorized actors can create a repository-backed folder when readiness checks pass. | Epic 3 — repository-backed folder creation when readiness passes | ✓ Covered |
| FR19 | Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence. | Epic 3 — folder binding to existing repository<br>**Gap:** Story 3.7 covers repository access and branch/ref compatibility but not duplicate-binding and canonical alias detection. | ⚠ Partial |
| FR20 | Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks. | Epic 3 — branch/ref policy selection | ✓ Covered |
| FR21 | The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets. | Epic 3 — provider/credential-reference/binding/branch/capability metadata exposure (no secrets) | ✓ Covered |
| FR22 | The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle. | Epic 3 — GitHub vs Forgejo capability differences exposed explicitly | ✓ Covered |
| FR23 | Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready. | Epic 3 — per-provider readiness evidence for canonical lifecycle (readiness, repo binding, branch/ref, file ops, commit, status, failure behavior) | ✓ Covered |
| FR24 | Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect. | Epic 4 — workspace preparation | ✓ Covered |
| FR25 | Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide. | Epic 4 — task-scoped workspace lock acquisition<br>**Gap:** Story 4.3 acquires a task lock but does not define the current managed-tenant + canonical provider/repository + normalized-ref serializing identity or require aliases to collide; the PRD tracks this as OQ7. | ⚠ Partial |
| FR26 | Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata. | Epic 4 — lock state, owner, task, age, expiry, retry-eligibility metadata inspection | ✓ Covered |
| FR27 | Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata. | Epic 4 — competing-operation denial under unsafe lock/state | ✓ Covered |
| FR28 | Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition. | Epic 4 — lock state transitions (active, expired, stale, abandoned, interrupted, released)<br>**Gap:** The coverage map still names active/abandoned/interrupted/released states, while the current PRD permits only unlocked/locked/expired/stale/revoked as lock-state wire terms. | ⚠ Partial |
| FR29 | Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely. | Epic 4 — workspace lock release<br>**Gap:** Story 4.4 covers valid release but not unexpired equivalent replay, expired-key rejection without execution, or revoked/non-owner safe denial. | ⚠ Partial |
| FR30 | Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP. | Epic 4 — workspace cleanup status visibility for completed/failed/interrupted/abandoned task lifecycles<br>**Gap:** Story 4.10 exposes cleanup status but no story owns automatic cleanup start conditions, seven-day deletion, ineligible states, idempotent retries, failed/inaccessible observation windows, or evidence preservation. | ⚠ Partial |
| FR31 | Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable. | Epic 4 and Epic 6 — lifecycle status currency produced by the task lifecycle and surfaced for operators | ✓ Covered |
| FR32 | Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit. | Epic 4 — file add/change/remove (PutFileInline ≤256KB + PutFileStream multipart) | ✓ Covered |
| FR33 | The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy. | Epic 4 — file-operation policy violation rejection (workspace boundary, path, branch/ref, lock, tenant, provider, folder) | ✓ Covered |
| FR34 | Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution. | Epic 4 — context queries via tree, metadata, search, glob, bounded range reads | ✓ Covered |
| FR35 | Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range/file content is never silently truncated, and unsupported excess returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence. | Epic 4 — context-query policy boundaries (paths, exclusions, binary handling, range/result limits, secret-safe responses)<br>**Gap:** Story 4.8 covers authorization and policy checks but not the complete current truncation contract, stable excess outcome, no-silent-truncation rule, bounded snippet fields, or forbidden query/path/content logging; OQ2 remains open. | ⚠ Partial |
| FR36 | The operations console must remain read-only and excluded from file editing or file-content browsing capabilities. | Epic 6 — read-only console scope (no file editing or content browsing in console) | ✓ Covered |
| FR37 | Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first enters `unknown_provider_outcome`; only exhausted/conflicting automatic evidence enters `reconciliation_required`. | Epic 4 — workspace commit for repository-backed folders<br>**Gap:** Story 4.12 covers commit and unknown outcome but not fresh authorization at the side effect, the no-more-than-five/read-only/15-minute evidence budget, or transition only after exhausted/conflicting evidence. | ⚠ Partial |
| FR38 | Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission. | Epic 4 — task/operation/correlation/actor/author/branch/commit-message/changed-path metadata attachment | ✓ Covered |
| FR39 | The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification. | Epic 4 — task and commit evidence exposure (provider, binding, branch, paths, status, commit ref, timestamps, IDs) | ✓ Covered |
| FR40 | The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation. | Epic 4 — failed/incomplete/duplicate/retried/conflicting operation reporting with stable status and audit evidence | ✓ Covered |
| FR41 | Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as new intent. | Epic 4 — idempotent lifecycle retries with stable task/operation/correlation IDs<br>**Gap:** Story 4.11 says mutating lifecycle commands and same/conflicting replay, but does not trace every mutating Contract Spine operation or expired-key behavior; the PRD tracks this as OQ8. | ⚠ Partial |
| FR42 | While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys. | Epic 4 — duplicate logical operation rejection on retry-identity or intent conflict<br>**Gap:** The plan covers conflicting unexpired intent but not expired-key rejection regardless of intent or universal rejection of idempotency keys on reads. | ⚠ Partial |
| FR43 | Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility. | Epic 1 and Epic 4 — canonical error taxonomy defined in the Contract Spine and realized by lifecycle behavior | ✓ Covered |
| FR44 | The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key. | Epic 4 — full error category set (validation/auth/tenant/folder ACL/credential/provider/capability/repository/branch/lock/workspace/path/commit/read-model/duplicate/transient)<br>**Gap:** The epic inventory and coverage description use the older error set and do not explicitly trace expired idempotency key, unknown provider outcome, reconciliation required, and the complete current client-action semantics. | ⚠ Partial |
| FR45 | The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status. | Epic 4 — canonical workspace/task states (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`) per C6 matrix | ✓ Covered |
| FR46 | After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence. | Epic 4 — final-state explanation + retry eligibility + operational evidence after any lifecycle failure | ✓ Covered |
| FR47 | API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine. | Epic 1 and Epic 5 — versioned REST contract authored first, then proven through cross-surface parity<br>**Gap:** Epic 8 still closes a 47-operation REST denominator, while the current PRD notes a 49-row generated C13 inventory and makes the live generated inventory authoritative. | ⚠ Partial |
| FR48 | CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5 — CLI canonical lifecycle parity | ✓ Covered |
| FR49 | MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 5 — MCP canonical lifecycle parity | ✓ Covered |
| FR50 | SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior. | Epic 1 and Epic 5 — SDK generated from the Contract Spine and proven through canonical lifecycle parity | ✓ Covered |
| FR51 | The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior. | Epic 1 and Epic 5 — cross-surface equivalence defined by the Contract Spine/parity oracle and validated across surfaces | ✓ Covered |
| FR52 | Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing. | Epic 6 — read-only ops console projection consumption (readiness, binding, workspace, lock, dirty, commit, failure, provider, credential-ref, sync) | ✓ Covered |
| FR53 | Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations. | Epic 6 — metadata-only audit trail inspection (success/denied/failed/retried/duplicate) | ✓ Covered |
| FR54 | Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources. | Epic 6 — incident reconstruction from immutable audit metadata | ✓ Covered |
| FR55 | File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown. | Epic 4 (write-side: redaction in events/projections/logs/traces/metrics) + Epic 6 (read-side: console rendering with classification + lock-icon affordance) | ✓ Covered |
| FR56 | Normal operation timelines come from projections; during projection degradation, the authorized incident view may expose bounded redacted event evidence with a persistent warning, last checkpoint, correlation ID, and time window, but no mutation or repair path. | Epic 6 — operation timelines for folder, workspace, file, lock, commit, provider, status, authorization events<br>**Gap:** Story 6.9 requires incident permission but not the current dual gate of incident-admin plus normal tenant/folder authorization, nor complete denial-audit coverage; the PRD tracks this as OQ9. | ⚠ Partial |
| FR57 | Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness. | Epic 6 — provider support evidence visibility for GitHub and Forgejo | ✓ Covered |
| FR58 | Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe. | Epic 10 — authorized Memories search-index query facade with Folders-side tenant/folder/workspace trimming, authoritative hydration, and metadata-only redaction | ✓ Covered |

### Missing Requirements

No FR number is wholly absent from the epic coverage map. The following partially covered FRs are treated as uncovered for strict readiness because their implementation path omits current mandatory semantics.

#### Critical Partial Coverage

#### FR4

Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it.

- **Coverage gap:** The plan splits configuration across Epic 2 and a platform-engineer-led Story 3.1, but does not enforce tenant-administrator ownership of provider/binding policy or the rule that scoped operators may validate but not modify it.
- **Impact:** An ownership ambiguity at this boundary can permit unauthorized policy mutation or inconsistent authorization behavior.
- **Recommendation:** Add explicit current-PRD ownership and operator-read-only acceptance criteria to Stories 2.2, 2.6, and 3.1, backed by the OQ3 authorization matrix.

#### FR15

Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.

- **Coverage gap:** Story 3.1 assigns configuration to a platform engineer and does not explicitly preserve tenant-administrator ownership of capability policy while limiting platform engineers to validation.
- **Impact:** Provider configuration authority can diverge from the PRD authorization model.
- **Recommendation:** Reframe Story 3.1 around tenant-administrator mutation and platform-engineer validation, with explicit capability-policy coverage.

#### FR25

Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.

- **Coverage gap:** Story 4.3 acquires a task lock but does not define the current managed-tenant + canonical provider/repository + normalized-ref serializing identity or require aliases to collide; the PRD tracks this as OQ7.
- **Impact:** Alias bindings could obtain independent locks and create mixed writes or lost updates.
- **Recommendation:** Add an OQ7 closure story or expand Story 4.3/11.10 with canonical-identity and alias-collision conformance tests.

#### FR28

Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.

- **Coverage gap:** The coverage map still names active/abandoned/interrupted/released states, while the current PRD permits only unlocked/locked/expired/stale/revoked as lock-state wire terms.
- **Impact:** Stale vocabulary can leak into schemas, adapters, projections, and UI labels.
- **Recommendation:** Synchronize Epic 4, Contract Spine metadata, C6 tests, and parity fixtures to the current five-term lock vocabulary.

#### FR29

Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.

- **Coverage gap:** Story 4.4 covers valid release but not unexpired equivalent replay, expired-key rejection without execution, or revoked/non-owner safe denial.
- **Impact:** Lock release can violate the current idempotency and authorization contract.
- **Recommendation:** Extend Story 4.4 and OQ8 closure evidence with replay, expiry, revoked, and non-owner release cases.

#### FR37

Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first enters `unknown_provider_outcome`; only exhausted/conflicting automatic evidence enters `reconciliation_required`.

- **Coverage gap:** Story 4.12 covers commit and unknown outcome but not fresh authorization at the side effect, the no-more-than-five/read-only/15-minute evidence budget, or transition only after exhausted/conflicting evidence.
- **Impact:** Commit reconciliation may retry unsafely, run unbounded, or act after authorization revocation.
- **Recommendation:** Expand Story 4.12 and provider/reconciler tests with the full current bounded evidence and fresh-authorization contract.

#### FR41

Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as new intent.

- **Coverage gap:** Story 4.11 says mutating lifecycle commands and same/conflicting replay, but does not trace every mutating Contract Spine operation or expired-key behavior; the PRD tracks this as OQ8.
- **Impact:** Some mutations can escape idempotency or execute after key expiry.
- **Recommendation:** Add generated all-mutations coverage from C13 and explicit expired-key/no-execution cases to Story 1.5/4.11 or a dedicated OQ8 story.

#### FR42

While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

- **Coverage gap:** The plan covers conflicting unexpired intent but not expired-key rejection regardless of intent or universal rejection of idempotency keys on reads.
- **Impact:** Adapters and endpoints may treat expired keys or read keys inconsistently.
- **Recommendation:** Extend OQ8 acceptance evidence to every read and mutation row in the current C13 inventory.

#### FR47

API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine.

- **Coverage gap:** Epic 8 still closes a 47-operation REST denominator, while the current PRD notes a 49-row generated C13 inventory and makes the live generated inventory authoritative.
- **Impact:** A green 47/47 claim can leave current Contract Spine operations without REST implementation or parity evidence.
- **Recommendation:** Replace fixed 47-operation acceptance criteria with generated-current-inventory checks and add explicit rows/routes/tests for all current operations.

#### FR56

Normal operation timelines come from projections; during projection degradation, the authorized incident view may expose bounded redacted event evidence with a persistent warning, last checkpoint, correlation ID, and time window, but no mutation or repair path.

- **Coverage gap:** Story 6.9 requires incident permission but not the current dual gate of incident-admin plus normal tenant/folder authorization, nor complete denial-audit coverage; the PRD tracks this as OQ9.
- **Impact:** Incident mode can become a privileged bypass to tenant/folder isolation.
- **Recommendation:** Expand Story 6.9 or add an OQ9 closure story covering positive, revoked, wrong-tenant, hidden-resource, degraded, redaction, and denial-audit cases.

#### High-Priority Partial Coverage

#### FR13

Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.

- **Coverage gap:** Story 2.8 uses an undefined eligibility predicate and omits the current no-active-task/lock and no staged/dirty/unknown/reconciliation preconditions, separately authorized governance exceptions, and provider-repository non-mutation rule.
- **Impact:** Archive could race active work, hide unresolved outcomes, or accidentally imply provider-side deletion.
- **Recommendation:** Expand Story 2.8/2.8b or add a focused archive-policy story containing the complete FR13 transition and denial table.

#### FR14

Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

- **Coverage gap:** The plan preserves audit/status under retention but does not trace per-field C3 expiry, omission, safe retention-expired markers, or the prohibition on extending shorter classes to seven years.
- **Impact:** Archived views can over-retain sensitive metadata or become misleading after partial expiry.
- **Recommendation:** Add archived-view per-class expiry behavior to Epic 2 and Workstream 7 retention acceptance evidence.

#### FR19

Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.

- **Coverage gap:** Story 3.7 covers repository access and branch/ref compatibility but not duplicate-binding and canonical alias detection.
- **Impact:** The same repository/ref can be bound more than once, undermining lock identity and tenant-safe lifecycle behavior.
- **Recommendation:** Add duplicate/alias detection and non-enumerating rejection acceptance criteria to Story 3.7.

#### FR30

Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.

- **Coverage gap:** Story 4.10 exposes cleanup status but no story owns automatic cleanup start conditions, seven-day deletion, ineligible states, idempotent retries, failed/inaccessible observation windows, or evidence preservation.
- **Impact:** The product has visibility for cleanup without a complete implementation path for cleanup behavior.
- **Recommendation:** Add a worker-owned automatic-cleanup story and connect it to C3/Workstream 7 retention and failure evidence.

#### FR35

Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range/file content is never silently truncated, and unsupported excess returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.

- **Coverage gap:** Story 4.8 covers authorization and policy checks but not the complete current truncation contract, stable excess outcome, no-silent-truncation rule, bounded snippet fields, or forbidden query/path/content logging; OQ2 remains open.
- **Impact:** Context responses can diverge across surfaces or leak protected query/content metadata.
- **Recommendation:** Close OQ2 through a canonical file-policy story and update Stories 1.9 and 4.8 with exact response and audit behavior.

#### FR44

The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.

- **Coverage gap:** The epic inventory and coverage description use the older error set and do not explicitly trace expired idempotency key, unknown provider outcome, reconciliation required, and the complete current client-action semantics.
- **Impact:** Contract surfaces can omit or collapse required failure states.
- **Recommendation:** Synchronize Stories 1.6, 1.10, and 4.13 plus the parity oracle to the current FR44 taxonomy.

### Coverage Statistics

- **Total PRD FRs:** 58
- **FR identifiers present in the epic map:** 58
- **Nominal identifier coverage:** 100%
- **Fully covered current FR semantics:** 42
- **Partially covered current FR semantics:** 16
- **Strict semantic coverage:** 72.4%
- **FRs in epics but not in the PRD:** 0

The primary cause is planning drift: the PRD was finalized on 2026-07-14, while the epic document's current mappings and acceptance criteria retain multiple pre-update semantics. The epic file must be synchronized to the current PRD before its 58/58 coverage claim can be accepted.

## UX Alignment Assessment

### UX Document Status

**Found:** `ux-design-specification.md` is a complete 792-line UX specification containing 32 stable UX requirements, three critical diagnostic journeys, component strategy, responsive behavior, and accessibility validation expectations.

### UX ↔ PRD Alignment

The documents align strongly on the product's console scope and intended outcomes:

- Both define a tenant-scoped, read-only operations console rather than a file manager or repair surface.
- Both make workspace discovery, visible tenant scope, trust-state diagnosis, provider readiness, lock/dirty/commit evidence, and metadata-only audit the primary operator jobs.
- Both prohibit mutation controls, repair, credential reveal, file-content browsing, raw diffs, unrestricted filesystem access, and unauthorized-resource confirmation.
- Both distinguish redacted, denied, unknown, missing, unavailable, stale, and failed data and require non-color-only communication.
- Both require WCAG 2.2 AA, keyboard operation, focus visibility, readable tables, zoom resilience, responsive fallback, and safe copy affordances.
- The UX FR58 addendum correctly limits the UI to backend metadata-token discovery and status signals; it adds no content preview or authorization bypass.

Alignment gaps:

1. **Canonical state vocabulary drift — high.** UX-DR13 requires canonical terminology, but UX-DR15 and many examples enumerate only a subset of the current PRD states. They omit `requested`, `preparing`, `changes_staged`, `unknown_provider_outcome`, and `reconciliation_required`, and do not systematically separate the current lock states `unlocked`, `locked`, `expired`, `stale`, and `revoked` from lifecycle and operator disposition. Update the UX state inventory, components, journeys, filters, and accessibility labels from the current PRD/C6 vocabulary.
2. **Search scope ambiguity — high.** The UX repeatedly calls the entry point “global search” and permits tenant filters. The current PRD forbids global cross-tenant browsing and permits only explicitly authorized tenant/folder scope. Rename or qualify this as authorized-scope workspace search, and require authorization-before-observation and non-enumerating empty/denied behavior.
3. **Incident authorization gap — critical.** The current PRD requires both incident-admin permission and normal current tenant/folder authorization. The UX specifies an ACL-checked incident view but does not encode the dual gate or denial-audit behavior. Add positive, revoked, wrong-tenant, hidden-resource, and degraded incident-flow states aligned with OQ9.
4. **Current PRD failure flow not fully reflected — high.** UX failure journeys need to show `unknown_provider_outcome` as an automatic bounded evidence-check state and `reconciliation_required` only after checks are exhausted or conflicting, with different wait/query versus human-escalation posture.

### UX ↔ Architecture Alignment

Architecture provides direct support for most UX requirements:

- F-1 through F-3 select Blazor Server/Interactive Server, the generated SDK/projection read path, FrontComposer, and Fluent UI.
- F-4 and F-5 support operator-disposition-first status and visibly distinct redaction.
- F-6 provides the degraded incident view, persistent warning, checkpoint, disposition labels, correlation/time-window copy, and continued redaction.
- F-7 defines page-load budgets, a 400 ms skeleton threshold, and a 2-second cancel affordance.
- The architecture explicitly admits the six UX-specific domain components, authorized search/filter fields, desktop-first responsive fallback, and WCAG 2.2 AA constraints.
- CI includes the automated axe/WCAG gate, while epic evidence covers keyboard, screen-reader, forced-colors, zoom, and dense-identifier validation.

Architecture gaps and warnings:

1. **Production diagnostic projections are empty — critical blocker.** `IOpsConsoleDiagnosticsReadModel` and `IWorkspaceTransitionEvidenceReadModel` are seed-only dev/test seams with no deployed projection logic. Production queries return safe-empty `NotFoundSafe`, so the UX cannot deliver readiness, lock, dirty-state, failure, transition, or trust-summary journeys. Story 11.10 is the planned owner, and PRD OQ6 remains open until populated positive, degraded, and replay evidence exists.
2. **Incident authorization is underspecified — critical blocker.** Architecture F-6 names `eventstore:permission=admin` and generic ACL checking, but does not state the current PRD's mandatory incident-admin **plus** normal tenant/folder authorization conjunction. OQ9 must update architecture, UX, stories, and tests together.
3. **Provider-outcome sequencing drift — high.** An early architecture invariant still says unknown provider results enter `reconciliation_required`, while the current PRD requires immediate `unknown_provider_outcome`, bounded read-only evidence checks, then `reconciliation_required` only on exhaustion or conflict. Architecture and UX failure-state behavior must be synchronized.
4. **FrontComposer conformance dependency — warning.** The architecture supports FrontComposer, while Epic 11.11 still owns hardening below the shell. Readiness depends on preserving Fluent-only interactive controls, shared Shell helpers, governed layouts, and the full accessibility/E2E lanes.

### Warnings

- The UX specification was originally completed in May and does not record a synchronization date matching the PRD's 2026-07-14 finalization. Its stable IDs remain useful, but the state, search-scope, and incident-mode sections need a controlled refresh.
- The UX design itself is sufficiently detailed. The readiness problem is not missing UX documentation; it is incomplete synchronization and missing production projection support.

## Epic Quality Review

### Review Scope

The review covered all 11 numbered epics, the separately labeled Release Readiness Workstream 7, and all 115 story headings and planning acceptance criteria. The document itself warns that these are terse planning ACs and that implementation-artifact story files contain authoritative as-built ACs. That split is itself a readiness risk: the canonical planning backlog cannot be validated without consulting a second, evolving story corpus.

### Epic Compliance Summary

| Epic/workstream | User-value focus | Independent of future epics | Story sizing | AC quality | Current FR traceability |
| --- | --- | --- | --- | --- | --- |
| Epic 1 — Bootstrap Canonical Contract | ⚠ Mixed consumer value and technical scaffold/gates | ✓ | ⚠ Several bundled contract/gate stories | ⚠ Mostly happy-path artifact checks | ⚠ Current-PRD drift |
| Epic 2 — Tenant Access and Lifecycle | ✓ | ✓ | ✓ Mostly | ⚠ Archive semantics incomplete | ⚠ FR4/13/14 drift |
| Epic 3 — Provider Readiness and Binding | ✓ | ✓ Uses prior work only | ⚠ Forgejo adapter/drift story oversized | ⚠ Creation stops at request; ownership/alias gaps | ⚠ FR15/19 drift |
| Epic 4 — Workspace Task Lifecycle | ✓ | ❌ Transition-evidence production path deferred to Epic 11 | ❌ Several capability families bundled | ⚠ Multiple incomplete/vague outcomes | ⚠ Eight current semantic gaps |
| Epic 5 — Cross-Surface Parity | ✓ Consumer trust outcome | ✓ Uses prior epics | ⚠ “One tool per operation” is oversized | ⚠ “where appropriate” is undefined | ⚠ Fixed operation denominator drift |
| Epic 6 — Read-Only Console | ✓ | ❌ Production diagnostics deferred to Epic 11 | ⚠ Accessibility and wireflow stories oversized | ⚠ Incident dual-auth missing | ⚠ FR56/OQ6/OQ9 gaps |
| Workstream 7 — Release Readiness | ✗ Technical governance work, correctly labeled non-product | ✓ Consumes prior evidence | ⚠ Broad documentation/gate stories | ⚠ Several evidence-only outcomes | NFR/release evidence, not FR value |
| Epic 8 — Release Acceptance Closure | ✗ Technical closure milestone | ✓ Uses prior work | ❌ 8-route and 7-route batches | ⚠ Fixed 47-operation assumptions | ⚠ Current C13 denominator drift |
| Epic 9 — AppHost Alignment | ✗ Technical infrastructure milestone | ✓ | ⚠ Multi-topology changes | ⚠ “healthy” and “green” under-specified | No product FR scope |
| Epic 10 — Search-Index Capability | ✓ | ❌ Non-empty deployed facade deferred to Epic 11 | ⚠ Multi-system materializer/egress story | ❌ Live proof may be carried as blocker | ✓ FR58 path exists but is incomplete |
| Epic 11 — Platform Refactoring | ✗ Technical refactoring milestone | ⚠ Depends on external shared-module delivery | ❌ Multiple epic-sized stories | ❌ Closure permits residual blockers | No new product FR scope |

### 🔴 Critical Violations

#### 1. Technical epics are represented as peer epics

Epics 8, 9, and 11 are technical milestones with no standalone user outcome: release-route closure, AppHost topology alignment, and platform refactoring. Epic 1 also mixes consumer-facing contract value with repository scaffolding and CI machinery. Workstream 7 is correctly labeled non-product, but the numbered technical epics still inflate epic completion and obscure which user capabilities are independently releasable.

**Recommendation:** Keep the six product epics and Epic 10 capability track as product value slices. Represent release closure, platform alignment, and refactoring as enabling workstreams or technical enablers explicitly attached to the product story whose acceptance they unlock; exclude them from product-epic completion metrics.

#### 2. Epic 6 and Epic 10 require future Epic 11 to function

The document explicitly states that Epic 6's seven diagnostic views and Epic 4's transition evidence are empty in deployed production until Story 11.10 authors and wires their projections. Epic 10's deployed search facade likewise returns zero items/`ReadModelUnavailable` until Story 11.10. These are forbidden forward dependencies: completed product epics cannot deliver their stated user value without a later refactoring epic.

**Recommendation:** Move the Story 11.10 projection/read-model work into the owning Epic 4, Epic 6, and Epic 10 stories, or move those epics back to incomplete. Story 11.10 may later refactor a working implementation, but it must not be the first production implementation.

#### 3. Closure stories explicitly permit incompletion

- Story 10.6 carries the live end-to-end proof as a DCP blocker.
- Story 11.10 allows live search and populated diagnostic evidence to be “re-carried with evidence.”
- Story 11.13 allows the final checklist to be “satisfied or explicitly blocked.”

These clauses make a story completable without satisfying its user outcome. Evidence of a blocker is useful status reporting, not acceptance.

**Recommendation:** Remove carry-forward alternatives from acceptance criteria. A blocked external lane should leave the story blocked; split hermetic implementation from live acceptance if independent delivery is genuinely valuable.

#### 4. Core lifecycle stories stop before delivering their stated outcomes

- Story 3.6 promises repository creation but accepts only a provisioning request and a state that “moves toward ready.”
- Story 4.2 promises a prepared workspace but accepts that preparation merely “starts.”
- Story 4.10 exposes cleanup status but no story owns the automatic cleanup behavior required by current FR30.

These are orchestration fragments, not independently valuable stories.

**Recommendation:** Add independently testable completion stories for provider-confirmed repository provisioning, workspace-ready/failure outcomes, and automatic cleanup eligibility/execution/evidence. Alternatively rewrite the current story titles and user outcomes honestly as request-acceptance increments and add the missing completion stories immediately after them.

#### 5. Planning acceptance criteria are not the authoritative story contract

The epics document directs implementers to implementation-artifact files for authoritative as-built ACs. This makes the planning backlog non-self-contained and allows requirements, status, and acceptance to drift across two sources.

**Recommendation:** Select one canonical story contract. Either synchronize the complete current ACs into `epics.md` or make it an index that embeds immutable links/digests and derives its coverage map automatically from the authoritative story files.

### 🟠 Major Issues

#### Oversized stories

The following stories combine multiple independently testable capabilities or cross-repository changes and should be split:

- 1.4 (C3, C4, S-2, and C6 decisions)
- 1.7, 1.9, and 1.11 (multiple large Contract Spine groups)
- 3.4 (full Forgejo adapter plus version matrix and live drift)
- 4.8 (tree, metadata, glob, range, text search, policy, audit, and optional semantic backend)
- 4.13 and 4.16 (all failure families and multiple security gate families)
- 5.3 (tools/resources for the entire current operation inventory)
- 6.11 (all responsive, automated, keyboard, screen-reader, forced-colors, zoom, and no-mutation verification)
- 7.13 and 7.17 (all consumer references; all ADRs/runbooks)
- 8.1 and 8.2 (eight and seven routes respectively)
- 10.6 (materializer, C4/C9 policy, publication, three test suites, governance rebase, and DCP evidence)
- 11.2, 11.3, 11.7, 11.8, 11.10, 11.11, and 11.13 (multi-module or system-wide refactoring and verification bundles)

#### Vague or non-verifiable acceptance language

Examples include “expected projects,” “minimally valid,” “intentionally incomplete,” “eligible for archive,” “moves toward ready,” “starts idempotently,” “where appropriate,” “status-only,” “stable caching,” “healthy,” and “meet budgets or produce measured release evidence.” These permit materially different interpretations and often omit the required negative paths.

**Recommendation:** Replace each phrase with an enumerated artifact/operation set, exact terminal state, measurable threshold, stable error code, and explicit negative cases.

#### Story 10.1 contradicts Story 10.5

Story 10.1 says no Server project may depend on Memories; Story 10.5 and architecture Option B require `Hexalith.Folders.Server` to reference the Memories REST client and contracts. Later prose creates an exception, but the earlier story AC remains absolute.

**Recommendation:** Update Story 10.1 to state the final two-project dependency rule: Workers uses Memories contracts for publication; Server alone may use the approved search client/contracts behind the Folders-owned facade; all other projects stay Memories-free.

#### Error and boundary coverage is inconsistent

Most planning stories have one happy-path Given/When/Then block and rely on cross-cutting later test stories for denial, replay, expiry, concurrency, stale authority, provider ambiguity, and retention cases. This weakens independent completeness and lets implementation proceed before its feature's failure contract is defined.

**Recommendation:** Each functional story must include its own success, validation, authorization, idempotency/replay, concurrency/provider failure, and metadata-safety cases where applicable; cross-cutting gates should verify systemic invariants, not substitute for story acceptance.

### 🟡 Minor Concerns

- Story numbering uses `2.8b`, which is understandable history but weakens machine sorting and dependency tooling; renumber or add a stable immutable story ID.
- Several AC blocks are BDD-shaped but contain long chained “And” clauses spanning distinct tests. Split them into named scenarios.
- Epic/status prose embeds volatile counts such as 47 operations, 63 UI E2E tests, and specific historical totals. Derive these from inventories where possible so planning does not become stale when the contract changes.
- The architecture specifies no third-party starter template and Story 1.1/1.2 appropriately scaffold from Hexalith patterns; however, the exact starter-source version or commit is not pinned in the story.

### Special Checks

- **Starter approach:** Pass with warning. Architecture explicitly rejects a third-party template, and Stories 1.1–1.2 create the initial solution/configuration. Pin the exact sibling-pattern baseline.
- **Greenfield/brownfield fit:** Pass. The original scaffold is represented, while current release-closure and refactoring work acknowledge the repository's brownfield state.
- **Database/entity timing:** Pass. No up-front relational schema epic creates unused tables. Event-sourced aggregates and read models are introduced near their capabilities, although the diagnostic projections are incorrectly deferred.
- **Dependency direction:** Fails because of the Epic 6/Epic 10 → Epic 11 forward dependencies and Story 11.2 external platform prerequisites.
- **Traceability:** Fails strict readiness: identifiers are 58/58, but only 42/58 current FR semantics are fully traced.

## Summary and Recommendations

### Overall Readiness Status

**NOT READY**

The artifacts should not be used to authorize a new implementation phase or a release-readiness claim in their current form. The source set is complete and detailed, but it is not synchronized: the PRD was finalized on 2026-07-14, while the UX and epic/story mappings retain earlier semantics. More importantly, several product outcomes are knowingly non-functional in deployed composition and deferred to a later technical refactoring epic.

### Critical Issues Requiring Immediate Action

1. **Current requirements are not fully traced.** All 58 FR identifiers appear in the epic map, but 16 have only partial current-semantic coverage. Strict coverage is 42/58 (72.4%), not the claimed 58/58.
2. **Ten PRD release items remain open.** OQ1–OQ4 leave key specification/governance contracts unresolved; OQ5–OQ10 leave implementation and release evidence incomplete.
3. **Production UX and FR58 paths are intentionally empty.** Ops-console diagnostics, workspace transition evidence, and the search-facade bridge read model return safe-empty/unavailable results until Story 11.10.
4. **Forbidden forward dependencies exist.** Epic 4, Epic 6, and Epic 10 require future Epic 11 implementation to deliver their stated user outcomes.
5. **The incident path lacks the complete security contract.** UX, architecture, and Story 6.9 do not consistently require incident-admin permission plus current normal tenant/folder authorization and denial audit, as required by FR56/OQ9.
6. **Lifecycle and idempotency semantics drift across artifacts.** Lock vocabulary, unknown-provider-outcome sequencing, all-mutations idempotency, expired-key behavior, cleanup rules, and the current generated C13 denominator are inconsistent.
7. **Several stories cannot be accepted honestly.** Story 10.6, Story 11.10, and Story 11.13 allow blocked evidence or incomplete verification to be carried forward while still closing the story.
8. **Core lifecycle outcomes lack complete stories.** Repository provisioning, workspace preparation, and automatic cleanup are represented by request-start or status-only fragments rather than end-to-end independently valuable outcomes.

### Recommended Next Steps

1. **Freeze and synchronize the planning baseline.** Update PRD, architecture, UX, epics, story contracts, project context, Contract Spine, C13 inventory, and governance fixtures in one controlled change. Replace stale lock/state vocabulary, provider-outcome sequencing, fixed operation counts, “global” search wording, and incident-mode authorization.
2. **Close OQ1–OQ4 before accepting further implementation scope.** Approve C7 timing, publish the canonical file-policy contract, publish the actor/access-state × protected-operation authorization matrix, and publish the GitHub/Forgejo compatibility catalog. Record named approvers, dates, versions, and digests.
3. **Restore epic independence.** Move the first production implementations of the diagnostics projection, transition-evidence projection, and search bridge read model into their owning Epic 6, Epic 4, and Epic 10 stories. Keep Epic 11 limited to behavior-preserving refactoring of already working paths.
4. **Create the missing outcome stories.** Add provider-confirmed repository provisioning, terminal workspace preparation, and platform-owned automatic cleanup stories with success, failure, authorization, idempotency, retention, and operational-evidence criteria.
5. **Correct the 16 partial FR mappings.** Update the coverage map from generated current requirements, and add or expand stories for FR4, FR13, FR14, FR15, FR19, FR25, FR28, FR29, FR30, FR35, FR37, FR41, FR42, FR44, FR47, and FR56.
6. **Split oversized technical and capability bundles.** Prioritize Stories 3.4, 4.8, 4.13, 4.16, 5.3, 6.11, 8.1, 8.2, 10.6, 11.2, 11.3, 11.7, 11.8, 11.10, 11.11, and 11.13. Each replacement story should end in one independently verifiable outcome.
7. **Make acceptance binary and testable.** Remove “moves toward,” “starts,” “where appropriate,” “status-only,” “or produce evidence,” “re-carried,” and “satisfied or explicitly blocked.” Add exact terminal states, error codes, negative paths, measurable thresholds, and artifact inventories.
8. **Close OQ5–OQ10 with evidence.** Prove non-empty FR58 metadata-token recall, populated console projections, canonical lock identity, all-mutations/read-key idempotency, dual-gated incident access, and the frozen release-calibration plan.
9. **Re-run implementation readiness after correction.** Require 58/58 semantic FR coverage, no product-epic dependency on a later technical epic, zero closure ACs that accept residual blockers, and approved canonical evidence for all ten open items.

### Final Note

This assessment documents **37 findings across three categories**: requirements/epic traceability, UX/architecture alignment, and epic/story quality. Several findings deliberately overlap because the same underlying drift appears in multiple artifacts. The recommended correction is a coordinated planning-baseline synchronization, not isolated wording edits.

**Assessment date:** 2026-07-15  
**Assessor:** Codex — BMAD Implementation Readiness workflow
