---
stepsCompleted:
  - step-01-validate-prerequisites
  - step-01-ux-requirements-refresh
  - step-02-design-epics
  - step-03-create-stories
  - step-04-final-validation
inputDocuments:
  - "_bmad-output/planning-artifacts/prd.md"
  - "_bmad-output/planning-artifacts/architecture.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-04.md"
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
date: '2026-05-10'
status: 'complete'
completedAt: '2026-05-10'
requirementsRefreshedAt: '2026-08-04'
requirementsAuthority: 'prd-2026-07-15-and-architecture-2026-07-19'
ratifiedInventoryThrough: 'sprint-change-proposal-2026-08-04'
epicStructureReviewedAt: '2026-08-04'
epicStructureAuthority: 'approved-major-planning-authority-recovery'
partyModeReviewedAt: '2026-05-11'
storiesReviewedAt: '2026-08-04'
finalValidationRefreshedAt: '2026-08-04'
authorityRecoveryCompletedAt: '2026-08-04'
implementationReadinessPatchedAt: '2026-05-12'
generatedCountsAuthority: '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
storyAuthorityManifest: '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
domainFocusRefactoringApprovedAt: "2026-07-07"
---

# Hexalith.Folders - Epic Breakdown

> **Note (2026-06-22):** Acceptance Criteria below are the terse *planning* ACs. As-built stories expanded these substantially (several to 11–22 ACs) through party-mode and code-review hardening. For the authoritative as-built ACs, see each story file under `_bmad-output/implementation-artifacts/`.

## Overview

This document provides the complete epic and story breakdown for Hexalith.Folders, decomposing the requirements from the PRD, Architecture, and approved sprint/readiness-correction proposals into implementable stories.

## Requirements Inventory

### Functional Requirements

#### Capability Contract Terms

- FR1: Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing.
- FR2: Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions.
- FR3: Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys.

#### Authorization and Tenant Boundary

- FR4: Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it.
- FR5: Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting verb scope is visible in effective permissions and auditable without exposing hidden principals.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

#### Folder Lifecycle

- FR11: Authorized actors with fresh tenant authority can create a logical folder within that tenant and receive its tenant-scoped managed identity and initial lifecycle state; denial creates no folder or provider side effect and uses the safe authorization/lifecycle result.
- FR12: Authorized actors can inspect folder lifecycle and binding status with freshness and availability metadata; an unauthorized, hidden, stale, or unavailable state uses the canonical non-enumerating result rather than partial binding details.
- FR13: Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.
- FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

#### Provider Readiness and Repository Binding

- FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass and receive its canonical provider/repository binding plus inspectable folder/workspace state; failed readiness or authorization creates no repository or binding side effect and returns the canonical safe result.
- FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.
- FR20: Authorized tenant administrators can define or select the branch/ref policy used by repository-backed folder tasks; an accepted policy becomes part of readiness, binding, and the canonical serializing target, while invalid or unauthorized changes are rejected without changing the active binding.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
- FR23: Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready.

#### Workspace and Lock Lifecycle

- FR24: Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect.
- FR25: Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata.
- FR28: Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.
- FR29: Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.
- FR30: Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.
- FR31: Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable.

#### File Operations and Context Queries

- FR32: Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
- FR34: Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution.
- FR35: Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range and file content are never silently truncated, and a request whose excess cannot be handled by supported truncation returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.
- FR36: The operations console must remain read-only and excluded from file editing or file-content browsing capabilities.

#### Commit, Evidence, and Idempotency

- FR37: Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting automatic evidence moves it to `reconciliation_required`.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission.
- FR39: The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification.
- FR40: The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation.
- FR41: Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new intent.
- FR42: While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

#### Error, Status, and Diagnostics Contract

- FR43: Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility.
- FR44: The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.
- FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.
- FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.

#### Cross-Surface Contract

- FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine and every C13-required REST cell passing the shared authorization, idempotency, lifecycle, error, and audit scenarios.
- FR48: CLI users can perform every C13-required CLI cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR49: MCP clients can perform every C13-required MCP cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR50: SDK consumers can perform every C13-required SDK cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

#### Audit and Operations Visibility

- FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.
- FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.
- FR56: Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only if, before any stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor holds incident-admin permission and fresh current tenant/folder authorization. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

#### Authorized Search Facade

- FR58: Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe.

### Non-Functional Requirements

#### Security and Tenant Isolation

- NFR1: Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, asynchronous provider side effect, and audit record. No incoming webhook ingestion exists in MVP.
- NFR2: Cross-tenant access leaks are zero-tolerance defects. No object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
- NFR3: Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, asynchronous work, audit records, index results, and context-query results.
- NFR4: File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
- NFR5: Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
- NFR6: Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.
- NFR7: Every mutation and asynchronous side effect must revalidate current tenant, folder, delegated-actor, binding, and credential authority before touching a protected resource; revocation fails closed and changes any held lock to revoked/inaccessible.
- NFR8: Paths, repository names, branch names, and commit messages are tenant-sensitive by default. Authorized tenant members and tenant-scoped operators with need-to-know may view them; cross-tenant/external diagnostics redact them. A tenant confidential override replaces cleartext at audit/projection write time with a stable tenant-scoped correlation token that preserves equality/linkage across authorized incident records but cannot reveal the original value. Redacted, hidden, unknown, missing, stale, and unavailable remain visibly distinct.
- NFR9: Credential references must be validated and displayed only as non-secret identifiers or status indicators.
- NFR10: Provider credentials and repository bindings must be tenant-scoped and must not be reused across tenants, even if repository URLs appear identical.
- NFR11: Provider credentials must use the least privilege required for supported lifecycle operations and must be validated against required provider capabilities before use.
- NFR12: Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.

#### Reliability, Idempotency, and Failure Visibility

- NFR13: Workspace lifecycle uses only the canonical lowercase wire states defined in the Glossary; lock state and generic operation-execution status are separate dimensions and must be labeled as such.
- NFR14: Every accepted operation exposes operation identity, workspace lifecycle, applicable lock state, projection freshness, and a terminal or inspectable non-terminal outcome.
- NFR15: Repository-backed task lifecycle operations must leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.
- NFR16: When an external effect is unconfirmed, the workspace immediately enters `unknown_provider_outcome` and permits only bounded automatic read-only checks; exhausted or conflicting evidence moves the workspace to `reconciliation_required`, blocks retry, mutation, and takeover, and requires human escalation. These states never collapse into a generic failure.
- NFR17: Idempotency keys are required for every mutating Contract Spine operation; non-mutating operations reject them.
- NFR18: While the idempotency record is unexpired within its declared retention tier, a repeated call with the same key and equivalent payload must return the same logical result, and the same key with a conflicting payload must return an idempotency conflict. After expiry, either form of key reuse returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new request.
- NFR19: Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.
- NFR20: Lock acquisition is deterministic and limited to one active writer per managed tenant plus canonical provider/repository identity plus normalized target ref; aliases resolving to that identity collide.
- NFR21: Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.
- NFR22: Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
- NFR23: A successful committed state requires provider-confirmed durable update of the bound remote/ref. A timeout or unconfirmed remote result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks move it to `reconciliation_required`, and neither state permits blind retry.
- NFR24: Failure visibility must expose state, cause category, retryability, and correlation ID without providing automated remediation in MVP.

#### Performance and Query Bounds

- NFR25: Command submission must acknowledge accepted lifecycle commands within 1 second p95 before asynchronous provider or workspace work continues.
- NFR26: Status and audit summary queries must return within 500 ms p95 for bounded MVP inputs.
- NFR27: Context queries must return within 2 seconds p95 for bounded MVP inputs.
- NFR28: Performance targets apply to bounded MVP inputs and control-plane responses. Targets must be validated against implementation benchmarks and recalibrated before release if provider or runtime constraints make the initial target misleading.
- NFR29: Provider and workspace operations may complete asynchronously when external Git provider latency or workspace size exceeds interactive response budgets; callers must receive operation identity and status visibility rather than blocking indefinitely.
- NFR30: Context queries accept at most 100 requested paths; return at most 2,000 tree entries or 500 search/glob results; allow at most 262,144 bytes for one bounded range and 1,048,576 serialized bytes for the aggregate response; and stop after 2 seconds of server execution. Excess input returns the stable input-limit result without partial execution. Supported result truncation occurs only after authorization/path filtering and sets one `isTruncated` flag; file content is never silently truncated.
- NFR31: Query-limit audit evidence includes family, configured limit, actual count/bytes, elapsed time, truncation, safe category, and correlation ID, but excludes raw query text, file content, path lists, and unauthorized existence.
- NFR32: File tree, search, glob, metadata, and bounded range queries must protect the service from unbounded workspace scans.
- NFR33: Large file and binary handling limits must be explicit before MVP release; unsupported files must fail with stable policy errors rather than causing unbounded processing.
- NFR34: Provider calls must use explicit timeout budgets, retry limits, and backoff caps.
- NFR35: Provider calls must report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states rather than leaving callers waiting indefinitely.
- NFR36: Provider rate-limit responses must preserve retry hints where available and expose retry-after or classified retryability.

#### Scalability and Capacity

- NFR37: The MVP release calibration must support 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent agent tasks per tenant, and at least 1 lifecycle operation per second without cross-tenant or cross-task interference.
- NFR38: Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.
- NFR39: Audit, timeline, and file-context projections must remain queryable as folder history grows.
- NFR40: Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.
- NFR41: Capacity claims beyond the approved C1/C5 release-calibration units require new evidence and are not implied by this PRD.

#### Integration and Contract Compatibility

- NFR42: REST, CLI, MCP, and SDK surfaces must preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes; transport shape and UX may differ.
- NFR43: Public contracts must be versioned. Breaking changes to lifecycle commands, queries, error categories, workspace states, provider capabilities, or audit fields require an explicit new versioned contract.
- NFR44: The product must support at least the active contract version and define a deprecation policy before removing any public lifecycle contract.
- NFR45: Shared or generated contract tests must validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
- NFR46: The OpenAPI 3.1 Contract Spine is the canonical operation/schema authority; the generated SDK is the typed canonical client; CLI and MCP wrap it; REST emitted schemas validate against the spine. Every current Contract Spine operation has exactly one C13 parity row.
- NFR47: GitHub and Forgejo support must be validated through provider contract tests before either provider is marked ready.
- NFR48: Provider contract tests must cover only MVP-dependent lifecycle behavior: readiness, repository binding, branch/ref handling, file operations, commit, status, provider errors, and failure behavior.
- NFR49: Supported GitHub and Forgejo products, instance/API versions, accepted credential/authentication profiles, and behavior assumptions must be published and recorded so compatibility drift is visible; unknown compatibility cannot be marked ready.
- NFR50: Provider capability differences must be reported explicitly instead of inferred by clients from failed operations.
- NFR51: Provider failures such as timeout, rate limit, authentication failure, authorization failure, repository missing, repository conflict, branch/ref conflict, unavailable provider, invalid path, commit rejected, and unknown outcome must map to stable product error categories.

#### Observability, Auditability, and Replay

- NFR52: Every successful, denied, failed, retried, or duplicate operation—including lock, file, commit, provider-readiness, and status-transition operations—must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
- NFR53: Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
- NFR54: Paths, commit messages, repository names, and branch names are tenant-sensitive by default under C9; authorized tenant/scoped-operator views may display them, cross-tenant/external diagnostics redact them, and a tenant confidential override stores only the stable tenant-scoped correlation token at audit/projection write time. Confidential incident reconstruction links operations through that token and operation/correlation identity; it does not promise recovery of the original cleartext. Provider payloads, file bodies, secrets, and generated context remain forbidden.
- NFR55: Operations-console views are projection-first, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata. During projection degradation, the bounded incident view may expose redacted event evidence only to an actor with incident-admin permission and normal tenant/folder access. The view must include a persistent warning, last checkpoint, correlation ID, and time window.
- NFR56: Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.
- NFR57: Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.
- NFR58: The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.
- NFR59: Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.

#### Data Retention and Cleanup

- NFR60: C3 retention is binding: audit metadata and commit-idempotency records are retained 7 years; workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, and normalized auth-claim metadata are retained 400 days; read models are retained 400 days or until rebuilt, whichever is sooner; temporary working files are deleted 7 days after task-terminal closure and no active task; folder metadata and tombstones remain for the tenant lifetime plus 400 days after the approved deletion workflow, subject to legal hold.
- NFR61: Tenant deletion anonymizes user display aliases while preserving metadata-only audit correlation/category/timestamp/outcome evidence; task-local display labels are tombstoned, secrets/content are deleted, and retained identifiers remain bounded by C3.
- NFR62: Workspace cleanup is platform-owned and automatic only after task-terminal closure and no active task. Failed/inaccessible closure records final metadata-only evidence and operator disposition before the C3 seven-day observation window starts. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are excluded. Cleanup retries idempotently; MVP exposes pending/retrying/completed/failed status but no user-triggered cleanup or repair action.
- NFR63: Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID.
- NFR64: No cleanup process may remove audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations.

#### Operations Console Accessibility

- NFR65: Read-only operations console flows must target WCAG 2.2 AA.
- NFR66: The console must support keyboard navigation for primary diagnostic workflows.
- NFR67: Status, failure, readiness, and lock indicators must not rely on color alone.
- NFR68: Console screens must provide visible focus states, semantic headings, readable table structure, and sufficient contrast.
- NFR69: Console text, controls, and tables must remain readable at common browser zoom levels used by operators.

#### Verification Expectations

- NFR70: Each NFR category must have at least one automated verification path or documented manual validation path before MVP release.
- NFR71: Security, tenant isolation, idempotency, provider contract, read-model determinism, and cross-surface contract compatibility NFRs must have automated tests.
- NFR72: Performance, accessibility, retention, backup/recovery, and operations-console usability NFRs must have release validation evidence before MVP acceptance.
- NFR73: Security verification must include dependency/package scanning, generated artifact review, and least-privilege provider credential validation.

### Additional Requirements

These come from the Architecture document and represent technical/infrastructure requirements that must be satisfied alongside FRs/NFRs.

#### Current Technical Authority Overlay (2026-08-04)

The following requirements are the current architecture-derived constraints for story design. They supersede older wording below wherever the two conflict.

- AR-CURRENT-01: Story 1.1 uses the exact sibling Hexalith structure: Hexalith.Tenants for the module baseline and the EventStore Admin CLI/MCP/UI patterns for adapters, while explicitly excluding generic starters, local platform-boilerplate duplication, and nested-submodule initialization.
- AR-CURRENT-02: The OpenAPI 3.1 Contract Spine is the operation/schema authority; NSwag generates the SDK, CLI and MCP wrap that SDK, REST-emitted schemas validate against the spine, and every current operation has exactly one generated C13 parity row.
- AR-CURRENT-03: Hexalith.EventStore owns durable command admission and event persistence. Production must replace the ADR-0001 NoOp repository and `/project` 501 with an EventStore-backed `IFolderRepository`, durable replay, and a bootable deployed host.
- AR-CURRENT-04: Operational state is classified explicitly. Locks, fencing tokens, idempotency admission/tombstones, in-flight checkpoints, and reconciliation tasks are durable and fail closed when unavailable; working copies are disposable caches, never authority.
- AR-CURRENT-05: Epic 12 owns durable source events, authoritative file content/state, restart replay, task completion, real Git persistence, and recoverable at-least-once egress. It does not own consuming product projections.
- AR-CURRENT-06: Epic 4 owns workspace transition evidence and durable prepare/lock/mutation/context/commit/reconciliation proof; Epic 6 owns seven populated diagnostic projections and deployed operator/incident journeys; Epic 10 owns the search bridge, deployed Server registration, current-authority hydration/redaction/pruning, and the non-empty FR58 round trip.
- AR-CURRENT-07: Story 11.10 owns EventStore admission and subscription-mapping seam adoption only. Story 11.14 owns Memories publication/search-client seams, and Story 11.15 owns the DCP-capable cross-repository verification lane. Workstream 11 owns no product projection.
- AR-CURRENT-08: The governing dependency sequence is OQ1–OQ4 → 12.1 → 12.2 plus 12.3 → 12.4 plus 12.5 → Epic 4/6/10 production closure → OQ5–OQ9 → OQ10 → implementation-readiness rerun.
- AR-CURRENT-09: Authorization order is JWT validation → EventStore claim transform → fresh tenant-access evidence → folder ACL → EventStore validator → Dapr deny-by-default policy. Authorization must precede any protected lookup, counting, filtering, provider call, file/content access, audit access, or search egress.
- AR-CURRENT-10: The serializing lock identity is managed tenant plus canonical provider/repository identity plus normalized target ref. Folder/workspace/task IDs are metadata, aliases collide, and lock state is distinct from lifecycle and disposition.
- AR-CURRENT-11: Every mutation follows the EventStore-owned durable admission contract; every read rejects an idempotency key before source execution. Live-equivalent replay, live-conflict, and expired-key precedence are distinct, and consumed-key evidence survives replay-result expiry without retaining protected prior intent.
- AR-CURRENT-12: Unknown external effects enter `unknown_provider_outcome` first and permit at most five read-only evidence checks within 15 minutes; only exhausted or conflicting evidence enters `reconciliation_required`. Neither state permits blind retry, takeover, or cleanup.
- AR-CURRENT-13: Memories is a derived shared search index, never Folders authority. Workers publish `SearchIndexEntryChanged`/`SearchIndexEntryRemoved`; the Server reads through Dapr service invocation; Folders authorizes before egress and hydrates/trims every candidate against current durable state.
- AR-CURRENT-14: The operations console is a read-only, metadata-only Blazor Web App using Interactive Server through `FrontComposerShell`; incident evidence requires incident-admin plus fresh tenant/folder authorization before observation.
- AR-CURRENT-15: Every positive production capability must prove deployed composition, restart-surviving durable population/replay where applicable, tenant isolation, positive and denial/conflict/failure/timeout-or-unknown/boundary behavior, and honest degraded/unavailable behavior. No completion claim may rely only on NoOp, in-memory, seed, unavailable, safe-empty, or fake evidence.

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

- AR-INFRA-01: `Hexalith.Folders.AppHost` composes the platform through the shared Aspire helpers — EventStore command gateway (`AppId=eventstore`, gateway-only via `AddHexalithEventStore(adminServer: null)`) + Tenants (`AppId=tenants`, `AddHexalithTenantsServer`) + Memories search-index server (`AppId=memories`, `AddHexalithMemoriesSearchIndexServer`; `memories-vectors` + `memories-graphs` containers) + Folders.Server (`AppId=folders`) + Folders.Workers (`AppId=folders-workers`) + Folders.UI (`AppId=folders-ui`) + Keycloak. Aspire 13.4.6 + CommunityToolkit.Aspire.Hosting.Dapr 13.4.0-preview.1.260602-0230. (See Epic 9.)
- AR-INFRA-02: Dapr components: shared `statestore` (Redis 7.x via Aspire), `pubsub` (Redis Streams), `resiliency` policies, `accesscontrol.yaml` (local: defaultAction allow), plus Memories components `memories-secretstore` + `memories-llm` (Epic 9). Production: deny-by-default + mTLS, app IDs restricted (`folders` may invoke `eventstore` and `tenants`; `folders`/`folders-workers` may invoke `memories`; not `system` admin; pubsub topics declared).
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
- AR-PROPOSAL-06: Preserve 58/58 FR coverage while renumbering affected stories and updating intra-document story references after the approved splits, including FR58 for the authorized Memories search-index facade.
- AR-PROPOSAL-07: Add an NFR traceability bridge: every PRD NFR bullet must map to an epic/story acceptance criterion, architecture exit criterion artifact, automated test gate, or documented release-validation evidence; release fails if any PRD NFR bullet remains unmapped.
- AR-PROPOSAL-08: Synchronize `_bmad-output/implementation-artifacts/sprint-status.yaml` after `epics.md` is revised, then rerun implementation readiness before sprint planning proceeds.

### UX Design Requirements

- UX-DR1: Build the MVP UI as a web/desktop-first Blazor Web App using Interactive Server rendering through `FrontComposerShell` and Microsoft Fluent UI Blazor; do not introduce a separate component library or custom design system.
- UX-DR2: Make workspace discovery the primary entry point with state-first filters for tenant, folder, workspace ID, repository binding, task ID, correlation ID, provider, lifecycle state, failure category, and time window. “Global” search is global only inside the caller's already-authorized tenant/folder scope; authorization and safe scope establishment precede candidate lookup, counting, suggestions, filtering, and empty-state classification.
- UX-DR3: Use a resource-detail console structure where search results lead to a workspace detail page anchored by tenant scope, resource identity, authorization posture, and current trust state.
- UX-DR4: Keep tenant, folder, repository binding, workspace, provider, task, and authorization context visible before detailed evidence in workspace, folder, provider, access, and audit views.
- UX-DR5: Implement a Workspace Trust Summary component on every workspace detail page showing tenant, folder, workspace ID, repository binding, provider, task ID, correlation ID, current state, authorization posture, lock state, dirty state, commit reference, latest reason category, and freshness timestamp.
- UX-DR6: Implement a Tenant Scope Banner component showing safe tenant identifier, effective access state, principal or delegated actor summary, policy scope, and last authorization check.
- UX-DR7: Implement a Metadata-Only Folder Tree or table that shows permitted path metadata, type, policy-safe size metadata or size class, last known operation, changed-path status, accessibility state, and redaction marker without exposing file contents or raw diffs.
- UX-DR8: Implement a Diagnostic Timeline component for diagnosis and audit views showing timestamp, event category, actor/task/correlation metadata, result, state transition, reason category, retry or escalation posture, and safe detail text.
- UX-DR9: Implement a Trust Matrix component comparing tenant boundary, provider readiness, workspace lifecycle, lock state, folder metadata visibility, and audit traceability with state label, icon, reason summary, last updated time, and link to supporting evidence.
- UX-DR10: Implement a Redaction And Inaccessibility State component that distinguishes redacted, inaccessible, denied, unknown, missing, unavailable, stale, and failed data.
- UX-DR11: Preserve the MVP read-only boundary in every UI flow: no mutation controls, repair actions, file editing, raw diff display, credential reveal, unrestricted file browsing, or unauthorized resource confirmation.
- UX-DR12: Present folder metadata only as orientation and evidence; never make the console feel like a file manager or content browser.
- UX-DR13: Present six independent dimensions consistently across search results, trust summaries, tables, timelines, detail panels, empty states, denied states, and redaction states: workspace lifecycle, lock state, operator disposition, folder lifecycle, projection freshness/availability, and visibility/redaction. Workspace lifecycle uses exactly `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`; lock state uses exactly `unlocked`, `locked`, `expired`, `stale`, and `revoked`; disposition uses exactly `available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, and `terminal-until-intervention`.
- UX-DR14: Every status indicator must include readable text, icon or shape cue, semantic color, accessible label, and optional tooltip or detail link when meaning is not obvious; color must never be the only signal.
- UX-DR15: Visually and semantically distinguish every lifecycle, lock, disposition, freshness, visibility/redaction, and folder-lifecycle value. Show `unknown_provider_outcome` as automatic reconciliation in progress with safe reason, last check, remaining check/time budget, and next check; reserve `awaiting-human` for `reconciliation_required` and expose no retry or takeover control.
- UX-DR16: Use restrained Fluent UI-based visual foundations: neutral surfaces, high-contrast text, semantic status colors, compact typography, and an 8px spacing base suitable for dense operational work.
- UX-DR17: Use cards only for distinct repeated items, summary blocks, and focused panels; avoid nested cards and decorative section cards.
- UX-DR18: Structure workspace detail pages with predictable sections for overview, folder metadata, diagnosis, audit trail, provider readiness, lock/task history, and access evidence.
- UX-DR19: Make current diagnosis and historical audit evidence connected from the workspace page rather than forcing users into disconnected pages for related evidence.
- UX-DR20: Provide safe empty states that distinguish no matches, insufficient filter scope, unavailable read model, and denied access without leaking unauthorized resource existence.
- UX-DR21: Provide denied states with safe reason category, allowed correlation ID evidence, and escalation posture without confirming unauthorized resource existence beyond policy.
- UX-DR22: Provide redacted states that are visibly different from missing, unknown, unavailable, failed, and denied data; redaction must not be silently hidden or represented as truncation.
- UX-DR23: Limit forms to search, filtering, sorting, and view preferences; forms must not submit domain mutations.
- UX-DR24: Use dialogs only for read-only detail expansion, safe identifier copy confirmation, filter configuration, and explanatory evidence; dialogs must trap focus, restore focus on close, and have accessible titles.
- UX-DR25: Preserve layout stability during loading states and label what is loading: search results, workspace summary, folder metadata, provider readiness, audit timeline, or access evidence.
- UX-DR26: Show stale or delayed data with freshness timestamps and read-model status; do not present stale evidence as current without labeling it.
- UX-DR27: Display safe identifiers such as task ID, operation ID, correlation ID, commit reference, and credential reference identifier in monospace with safe copy affordances only.
- UX-DR28: Support desktop-first layouts with persistent navigation, global search, trust summaries, multi-column evidence panels, metadata tables, and side-by-side diagnosis or audit sections.
- UX-DR29: Provide tablet and mobile fallback layouts that stack evidence panels, collapse persistent navigation, preserve search and filters, prioritize tenant/workspace/state/risk signal, and do not break core lookup or high-level trust review.
- UX-DR30: Target WCAG 2.2 AA with keyboard access for search, filters, result selection, tabs, tables, tree expansion, detail panels, and dialogs; visible focus; semantic headings and landmarks; accessible names; sufficient contrast; zoom resilience; and screen-reader meaningful redaction/denial/status labels.
- UX-DR31: Test the UI at desktop, tablet, and mobile fallback widths, at 125%, 150%, and 200% browser zoom, and with dense identifiers and long paths in tables, timelines, metadata trees, and trust summaries.
- UX-DR32: Validate accessibility with automated checks, keyboard-only walkthroughs for the three critical journeys, screen reader review, forced-colors/high-contrast checks where supported, color-blindness review, and focus management checks.
- UX-DR33: Before incident evidence performs stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor must hold incident-admin permission and fresh current tenant/folder authorization. The gate exposes only bounded C9-redacted metadata with a persistent degraded warning, checkpoint, time window, and correlation context; every denial fails before observation, emits one safe audit record, and reveals no hidden-resource existence.

### FR Coverage Map

- FR1: Epic 1 — vocabulary in OpenAPI Contract Spine + `docs/contract-terms.md`
- FR2: Epic 1 — lifecycle vocabulary via `x-hexalith-lifecycle-states` extension + diagrams
- FR3: Epic 1 — command/query distinction in OpenAPI operation grouping + Server endpoint routing
- FR4: Epic 2 and Epic 3 — tenant administrators own ACL/archive and provider/binding policy respectively; scoped operators validate only
- FR5: Epic 2 — folder access grant to users, groups, roles, and delegated service agents
- FR6: Epic 2 — effective-permissions inspection
- FR7: Epic 3 — tenant readiness inspection (depends on provider configuration)
- FR8: Epic 2 — layered authorization evaluation (foundation: JWT → claim transform → tenant projection → folder ACL → EventStore validators → Dapr policy)
- FR9: Epic 2 — cross-tenant denial before any file/workspace/credential/repository/lock/commit/provider/audit access
- FR10: Epic 2 — authorization evidence (allowed and denied) without unauthorized resource enumeration
- FR11: Epic 2 and Epic 12 — authorized folder creation plus durable managed identity and replay
- FR12: Epic 2 and Epic 6 — lifecycle/binding inspection with freshness, availability, and safe non-enumeration
- FR13: Epic 2 — archive eligibility, provider no-touch, stable post-archive mutation denial, and separately authorized governance operations
- FR14: Epic 2 and Workstream 7 — independently expiring C3-class fields, retention-expired markers, and retained metadata-only evidence
- FR15: Epic 3 — tenant-administrator provider/binding/credential/default-ref/capability configuration with platform-engineer validation
- FR16: Epic 3 — provider readiness validation before repository-backed creation/binding
- FR17: Epic 3 — readiness diagnostics with safe reason codes, retryability, remediation category, provider reference, correlation ID
- FR18: Epic 3 and Epic 12 — asynchronous repository-backed creation with no-side-effect denial and durable completion
- FR19: Epic 3 — pre-created repository binding with readiness, access, duplicate/alias, and branch/ref validation
- FR20: Epic 3 — tenant-admin branch/ref policy that becomes part of readiness, binding, and the serializing target
- FR21: Epic 3 — provider/credential-reference/binding/branch/capability metadata exposure (no secrets)
- FR22: Epic 3 — GitHub vs Forgejo capability differences exposed explicitly
- FR23: Epic 3 — published provider product/instance/version/API/credential/capability evidence; unknown compatibility cannot report ready
- FR24: Epic 4 and Epic 12 — freshly authorized preparation backed by durable repository/content state
- FR25: Epic 4 — lock acquisition on managed tenant plus canonical provider/repository plus normalized-ref identity, including alias collision
- FR26: Epic 4 — lock state, owner, task, age, expiry, retry-eligibility metadata inspection
- FR27: Epic 4 — deterministic conflict denial with no protected side effect and one safe audit record
- FR28: Epic 4 and Epic 6 — exact `unlocked`, `locked`, `expired`, `stale`, `revoked` lock vocabulary, separate from lifecycle/disposition
- FR29: Epic 4 and Epic 12 — authorized release with live replay, expired-key precedence, and safe revoked/non-owner denial
- FR30: Epic 4 and Workstream 7 — automatic task-terminal-only C3 cleanup, observable retries/failures, and no user repair
- FR31: Epic 4, Epic 6, and Epic 10 — lifecycle/lock/disposition/freshness plus task/audit/provider/index currency
- FR32: Epic 4 and Epic 12 — atomic validated change sets over authoritative durable content without auto-commit
- FR33: Epic 4 — file-operation policy violation rejection (workspace boundary, path, branch/ref, lock, tenant, provider, folder)
- FR34: Epic 4 — live-workspace tree/metadata/glob/range/body search under exact C4 numeric bounds
- FR35: Epic 4 — pre-scan authorization/path policy, bounded snippets/truncation, stable limit errors, and telemetry exclusions
- FR36: Epic 6 — read-only console scope (no file editing or content browsing in console)
- FR37: Epic 4 and Epic 12 — fresh-authority commit with provider-confirmed durable remote/ref result and bounded reconciliation
- FR38: Epic 4 — closed C9-classified task/operation/actor/commit metadata with secret/content-like rejection before emission
- FR39: Epic 4 and Epic 6 — metadata-only durable task/commit evidence under current C9 policy
- FR40: Epic 4 — stable failure/duplicate/retry/conflict evidence and distinct automatic-versus-human reconciliation posture
- FR41: Epic 12 and Epic 5 — durable all-mutations replay/expiry substrate plus cross-surface conformance without duplicates
- FR42: Epic 12 and Epic 5 — live conflict, expired-key precedence for either intent, and read-key rejection before execution
- FR43: Epic 1, Epic 4, and Epic 5 — canonical closed error shape, product behavior, and surface parity
- FR44: Epic 1, Epic 4, Epic 5, and Epic 12 — complete taxonomy including expired key, unknown outcome, and reconciliation required
- FR45: Epic 4 and Epic 6 — all 11 lifecycle states, separate five-state lock vocabulary, and separate disposition presentation
- FR46: Epic 4, Epic 6, and Epic 10 — safe lifecycle/lock outcome, retry/client action, and populated metadata-only evidence after failure
- FR47: Epic 1 and Epic 5 — versioned REST contract authored first, then proven through cross-surface parity
- FR48: Epic 5 — CLI canonical lifecycle parity
- FR49: Epic 5 — MCP canonical lifecycle parity
- FR50: Epic 1 and Epic 5 — SDK generated from the Contract Spine and proven through canonical lifecycle parity
- FR51: Epic 1 and Epic 5 — cross-surface equivalence defined by the Contract Spine/parity oracle and validated across surfaces
- FR52: Epic 6 — tenant-scoped populated read-only projections for readiness, binding, lifecycle, lock, disposition, durable commit, failure, provider, credential-reference, and sync
- FR53: Epic 6 — metadata-only audit trail inspection (success/denied/failed/retried/duplicate)
- FR54: Epic 6 — authorized incident reconstruction from immutable C9-classified metadata without file bodies or hidden existence
- FR55: Epic 4 (write-side: redaction in events/projections/logs/traces/metrics) + Epic 6 (read-side: console rendering with classification + lock-icon affordance)
- FR56: Epic 6 — projection-first timelines plus incident-admin and fresh tenant/folder authorization before any degraded-mode observation
- FR57: Epic 3 produces provider evidence; Epic 6 presents it within authorized operator scope
- FR58: Epic 10 owns authorized non-empty metadata-token search/status completion; Epic 12 supplies durable source state/egress and Epic 9 supplies topology enablement

## Epic List

### Enabling Workstream 1: Canonical Contract and Adapter Foundation
API consumers, adapter implementers, and maintainers can rely on a scaffolded Hexalith.Folders module with one OpenAPI v1 Contract Spine driving REST, SDK, CLI, and MCP before product work begins.
**FRs covered:** FR1, FR2, FR3, FR43, FR47, FR50, FR51
**Classification:** technical enabler; excluded from product-capability completion metrics.
**Guardrails:** Epic 1 owns the canonical workspace state model, response envelope, error taxonomy, audit vocabulary, golden contract fixtures, negative/error contract cases, and schema compatibility gates that constrain Epics 2-6.

### Product Epic 2: Tenant-Scoped Folder Access and Lifecycle
Tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access.
**FRs covered:** FR4, FR5, FR6, FR8, FR9, FR10, FR11, FR12, FR13, FR14
**Classification:** product.

### Product Epic 3: Provider Readiness and Repository Binding
Tenant administrators configure provider, credential-reference, repository/default-ref, and capability policy; authorized actors create or bind repositories; scoped platform engineers validate readiness and provider evidence without gaining tenant-policy mutation authority or exposing secrets.
**FRs covered:** FR7, FR15, FR16, FR17, FR18, FR19, FR20, FR21, FR22, FR23, FR57
**Classification:** product.
**UX/evidence guardrails:** Provider readiness and repository binding stories must preserve explainable readiness states, degraded states, safe blockers, retryability, and secret-safe evidence that Epic 6 can render without inventing UI-only semantics.

### Product Epic 4: Repository-Backed Workspace Task Lifecycle
Developers and AI agents can prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and receive deterministic failure, status, idempotency, and redaction behavior through the canonical repository-backed task lifecycle.
**FRs covered:** FR24, FR25, FR26, FR27, FR28, FR29, FR30, FR31, FR32, FR33, FR34, FR35, FR37, FR38, FR39, FR40, FR41, FR42, FR43, FR44, FR45, FR46, FR55
**Classification:** product.
**Dependency:** Positive durable completion depends on the applicable Epic 12 substrate; Epic 4 retains ownership of transition evidence and product lifecycle proof.
**Risk guardrails:** Lifecycle stories must expose coherent user-facing status language and evidence for prepare, lock, file mutation, context query, commit, audit, lock contention, stale locks, interrupted commits, provider outage, and tenant isolation under parallel activity.

### Product Epic 5: Cross-Surface Workflow Parity
API, SDK, CLI, and MCP users can run the same canonical lifecycle with equivalent operation identity, errors, idempotency, audit behavior, authorization outcomes, terminal states, and mixed-surface handoff.
**FRs covered:** FR47, FR48, FR49, FR50, FR51
**Classification:** product.
**Verification guardrails:** Parity must be proven through shared conformance tests and the generated parity oracle, not surface-by-surface manual equivalence. REST, SDK, CLI, MCP, and console-facing evidence must use the same concepts, names, error categories, and audit model.

### Product Epic 6: Read-Only Workspace Trust Console and Audit Review
Operators, tenant administrators, and audit reviewers can find a workspace, prove its tenant boundary, inspect readiness, locks, dirty state, failures, commits, provider evidence, metadata-only folder visibility, timelines, and audit records through a FrontComposer/Fluent UI read-only console without mutation or file-content exposure.
**FRs covered:** FR31, FR36, FR45, FR46, FR52, FR53, FR54, FR55, FR56, FR57
**Classification:** product.
**Dependency:** Populated production completion depends on durable Epic 12 source events; Epic 6 retains ownership of all seven diagnostic projections and deployed operator/incident journeys.
**UX requirements covered:** UX-DR1–UX-DR33, with accessibility release evidence also owned by Workstreams 7/8.
**Console guardrails:** The primary job is inspect, verify, and escalate. Users can search, filter, navigate, inspect, and copy safe identifiers, but the console must consume shared query/status/audit/readiness APIs only and must not expose mutation endpoints, privileged backdoors, hidden administrative bypasses, or UI-only lifecycle semantics.

### Enabling, Release, Quality, and Hardening Workstreams

The following workstreams are excluded from product-capability completion metrics. Completed work remains truthful evidence within its bounded enabling, release, remediation, topology, refactoring, or hardening scope.

### Release Readiness Workstream 7: MVP Release Readiness And Operational Evidence
Release stakeholders can verify that the MVP satisfies security, tenant isolation, parity, provider compatibility, Dapr policy, retention, observability, capacity, accessibility, documentation, package-traceability, and NFR traceability evidence before production acceptance.
**FRs covered:** Cross-cutting validation for all FRs; no new product FR scope.
**Classification:** release governance and quality closure; excluded from product-capability completion metrics.
**Evidence guardrails:** Readiness evidence is collected continuously from Epics 1-6 and must cover REST, SDK, CLI, MCP, console, tenant isolation, audit completeness, performance baselines, accessibility, operational runbooks, and NFR traceability before MVP acceptance.

### Release Remediation Workstream 8: MVP Release Acceptance Closure
Release stakeholders retain the completed REST-route, parity, accessibility, retention-approval, and honest-green remediation evidence without treating those bounded batches as acceptance of the still-open durable product MVP.
**FRs covered:** No new product FR scope. Completes REST-surface delivery for FR2, FR5, FR6, FR11, FR15, FR26, FR28, FR39, FR46, FR52 (server routes for operations already present on SDK/CLI/MCP) plus cross-cutting release validation.
**Classification:** release remediation and quality closure; excluded from product-capability completion metrics.
**Created:** 2026-06-22 via bmad-correct-course (`sprint-change-proposal-2026-06-22.md`). Closure epic — not a feature workstream.

### Enabling Workstream 9: AppHost and Memories Search-Index Topology
Platform engineers can run the full Folders topology — EventStore (gateway-only), Tenants, and the Memories search-index server — composed purely through the shared platform Aspire helpers, with `hexalith-folders → folders-index` routing configured, removing the hand-rolled `FoldersAspireModule` Dapr wiring.
**FRs covered:** No new product FR scope (infrastructure alignment + additive Memories hosting).
**Classification:** completed FR58 topology enabler; excluded from product-capability completion metrics.
**Created:** 2026-06-22 via bmad-correct-course (`sprint-change-proposal-2026-06-22-apphost-memories-platform-alignment.md`).

### Product Epic 10: Authorized Folders Content Search and Index Lifecycle
Developers and AI agents can publish, remove, reconcile, authorize, query, and durably hydrate metadata-token index results through Memories while Folders remains authoritative and prevents cross-tenant or sensitive-data disclosure.
**FRs covered:** FR58.
**Classification:** product.
**Dependency:** Epic 9 supplies completed topology enablement; Epic 12 supplies durable source events/state and at-least-once egress; Epic 10 owns the deployed bridge, pruning, current-authority hydration/redaction, and non-empty FR58 round trip.
**Guardrail:** Metadata-derived indexing is an enabling increment, not FR58 completion. Story 10.9 body-content materialization remains a separately authorized C9-gated follow-on and is not required by the current metadata-token FR58 clause.

### Technical Enabling Workstream 11: Domain-Focus Platform Refactoring and Governance Closure
Maintainers replace local platform copies with approved shared Hexalith seams, preserve wire behavior, and close boundary/governance verification without taking ownership of product projections.
**FRs covered:** No new product FR scope; preserves conformance for the current FR/NFR inventory.
**Classification:** technical enabler/refactoring; excluded from product-capability completion metrics.
**Ownership:** Story 11.10 owns EventStore admission/subscription seams, Story 11.14 owns Memories publication/search-client seams, and Story 11.15 owns the DCP-capable cross-repository verification lane. Workstream 11 owns no Epic 4, 6, or 10 product projection.

### Product Epic 12: Durable Repository-Backed Round Trip
Authorized developers and AI agents can persist folder lifecycle and file content across process restart, retrieve authoritative content, complete a real Git commit, observe terminal task/projection state, and recover asynchronous indexing delivery without NoOp, unavailable, in-memory, or fake-backed substitutions.
**FRs covered:** Durable substrate for FR2, FR11, FR18, FR24, FR29, FR32, FR37, FR39–FR46, and FR58.
**Classification:** product.
**Boundary:** Owns durable source events, authoritative content/state, restart replay, task completion, real Git persistence, durable all-mutations idempotency, and recoverable egress—not the consuming Epic 4/6/10 projections.

### Hardening Epic 13: Security and Operational Hardening
Security and operations stakeholders can harden the surfaces that already claim to work through SSRF defenses, fail-safe authorization, protected credential files, truthful health/readiness, production Dapr state/resiliency, bounded requests, and converged sensitive-value filtering.
**FRs covered:** No new product FR scope; strengthens the security, reliability, observability, performance, and operational NFR evidence needed for release.
**Classification:** security/operations hardening; release-blocking but excluded from product-capability completion metrics.

### Governing Portfolio Dependency

`OQ1–OQ4 → 12.1 → (12.2 + 12.3) → (12.4 + 12.5) → Epic 4/6/10 production closure → OQ5–OQ9 → OQ10 → implementation-readiness rerun`.

Stable epic numbers are retained for historical traceability, so numeric order is not execution order. No product completion claim may be supported only by NoOp, in-memory, seed, unavailable, safe-empty, or fake evidence.

## Enabling Workstream 1: Bootstrap Canonical Contract For Consumers And Adapters

API consumers, adapter implementers, and maintainers can rely on a scaffolded Hexalith.Folders module with one OpenAPI v1 Contract Spine driving REST, SDK, CLI, and MCP before feature work begins.

This epic owns the canonical workspace state model, cross-surface response envelope, error taxonomy, audit vocabulary, golden contract fixtures, negative/error contract cases, and schema compatibility gates that constrain every later implementation epic.

### Story 1.1: Establish a consumer-buildable module scaffold

As a platform engineer and downstream consumer,
I want the Hexalith.Folders solution scaffold to build with the approved project layout,
So that consumers and later stories have a stable, convention-compliant module baseline.

**Acceptance Criteria:**

**Given** an empty Hexalith.Folders repository and the architecture finding that no generic public or `dotnet new aspire-starter` template satisfies Hexalith conventions
**When** the scaffold is created
**Then** `Hexalith.Folders.slnx` reproduces the approved sibling-module structure: Hexalith.Tenants project/layout, central package management, `Directory.Build.props`, naming, configuration, and dependency conventions, plus the Hexalith.EventStore Admin CLI/MCP/UI reference patterns for the adapter surfaces
**And** project references follow the architecture dependency direction and target .NET 10
**And** the scaffold explicitly excludes generic starter boilerplate, local copies of shared platform infrastructure, provider-specific implementation, production persistence/data tables, product projections, credentials, tenant data, and initialized nested submodules
**And** `dotnet build` succeeds for the consumer-buildable scaffold without requiring any excluded runtime capability.

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

_**Disposition:** Historical batch. Preserve its completed evidence and do not reactivate this umbrella; any new defect requires a narrow ratified follow-up._

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
**And** the canonical workspace state model, cross-surface response envelope, error taxonomy, and audit vocabulary are defined as shared contract primitives rather than per-surface conventions
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
**And** rows include both transport-parity and behavioral-parity columns
**And** golden lifecycle fixtures plus negative/error contract cases are generated or referenced so downstream conformance tests can prove compatibility without hand-authored surface assumptions.

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

## Product Epic 2: Tenant-Scoped Folder Access And Lifecycle

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
**And** audit and status evidence remain queryable under retention policy
**And** the production REST → `IEventStoreGatewayClient` → `/process` → `FolderDomainProcessor` → `FolderArchiveTenantGate` → persistence path enforces ACL, policy freshness, all-mutation idempotency, and append-conflict reread
**And** an in-process integration test that does NOT mock `IEventStoreGatewayClient` proves the happy path and every Archive Denial And State Table row end-to-end
**And** `FolderAccessTenantGate` follows the same persistence pattern for consistency.

**Superseded alias:** Story 2.8b, “Wire FolderArchiveTenantGate as an IDomainProcessor,” is absorbed into Story 2.8. Its completed production-wiring evidence and lifecycle record remain preserved; it is not a second canonical story.

### Story 2.9: React to Tenants events through Worker handlers

As a system component,
I want worker handlers to react to tenant lifecycle and membership events,
So that Folders authorization stays aligned with tenant administration.

**Acceptance Criteria:**

**Given** a relevant Tenants event is published
**When** the Folders worker receives it
**Then** local tenant-access projections and folder authorization metadata are updated idempotently
**And** only `folders.*` configuration keys are processed.

## Product Epic 3: Provider Readiness And Repository Binding

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

**Given** the provider port and a fake/test provider adapter exist
**When** capabilities are queried through the port
**Then** supported operations, branch/ref behavior, file limits, credential mode, version/capability metadata, retryability hints, and failure categories are exposed as metadata
**And** capability-query behavior is validated without depending on future GitHub or Forgejo adapter implementation
**And** the model is not hardcoded to exactly two providers.

### Story 3.3: GitHub capability discovery and safe readiness

As a platform engineer,
I want GitHub capability discovery and safe readiness through the canonical provider port,
So that support can be evaluated before any GitHub repository operation observes a protected target.

**Acceptance Criteria:**

**Given** a GitHub binding and credential reference exist
**When** readiness and capabilities are queried
**Then** the adapter returns safe support, version, capability, retryability, and failure metadata without repository mutation
**And** repository provisioning/binding/ref behavior is owned by Story 3.10 and file/commit/status behavior by Story 3.11.

### Story 3.4: Forgejo capability discovery, safe readiness, and contract-drift detection

As a platform engineer,
I want Forgejo capability discovery, safe readiness, and versioned contract-drift detection,
So that Forgejo support is verified before protected repository behavior is attempted.

**Acceptance Criteria:**

**Given** supported Forgejo versions are listed
**When** readiness and capability discovery run through the canonical provider port
**Then** supported API behavior and safe failure metadata are returned without repository mutation
**And** supported Forgejo version snapshots are pinned
**When** contract tests and nightly drift checks run
**Then** schema drift is classified as warning or failure according to policy
**And** readiness cannot report ready for an unsupported or failing provider version
**And** repository/ref behavior is owned by Story 3.12 and file/commit/status behavior by Story 3.13.

### Story 3.5: Validate provider readiness with safe diagnostics

As a platform engineer,
I want to validate provider readiness before repository-backed creation or binding,
So that configuration failures are caught before workspace tasks begin.

**Acceptance Criteria:**

**Given** a tenant has provider binding metadata
**When** readiness validation runs
**Then** the result includes ready/failed state, safe reason code, retryability, remediation category, provider reference, and correlation ID
**And** secrets and credential values are not included.

### Story 3.6: Request asynchronous creation of a repository-backed folder

As an authorized actor,
I want to request creation of a new provider repository for an existing logical folder after readiness passes,
So that a durable asynchronous process can complete the binding without implying synchronous completion.

**Acceptance Criteria:**

**Given** a logical folder exists and provider readiness is green
**When** `CreateRepositoryBackedFolder` is accepted
**Then** repository provisioning is requested idempotently and the folder remains in the correct non-terminal C6 state
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

### Story 3.10: GitHub repository provisioning, binding, and branch/ref behavior

As an authorized actor,
I want GitHub repository provisioning, existing-repository binding, and branch/ref behavior to execute through the canonical provider port,
So that a tenant folder can use GitHub without provider-specific leakage, duplicate bindings, or ambiguous retry behavior.

**Acceptance Criteria:**

**Given** a tenant administrator has configured an approved GitHub binding, opaque credential reference, repository policy, and current readiness evidence
**When** an authorized create or bind operation executes through the real Octokit-backed provider seam
**Then** authorization and evidence freshness are checked before credential or target resolution, exactly one eligible mutation occurs, and canonical identity plus exact branch/ref compatibility determine success, equivalent existing, or safe conflict
**And** equivalent replay, conflicting replay, known provider failures, timeout or ambiguous post-dispatch outcomes, and cancellation boundaries return canonical results without blind mutation retry
**And** durable handoff evidence is provider-neutral, restart-safe, metadata-only, and excludes credentials, raw repository/ref locators, URLs, response bodies, and hidden existence
**And** completion requires real deployed composition plus positive, denial, conflict, failure, timeout/unknown, tenant-isolation, and boundary evidence; fake-only or unavailable behavior is not completion.

### Story 3.11: GitHub file mutation, commit, status, and failure behavior

As an authorized workspace actor,
I want GitHub file mutation, commit, and status operations implemented through the canonical provider port,
So that repository-backed work has provider-correct behavior without leaking GitHub-specific contracts.

**Acceptance Criteria:**

**Given** an authorized task owns the canonical workspace lock and its paths, ref policy, and C4 limits are valid
**When** add, change, remove, commit, or status behavior executes against GitHub
**Then** the concrete adapter preserves operation ordering, exact ref and commit identity, canonical status/failure mapping, cancellation boundaries, and unknown-outcome reconciliation semantics
**And** equivalent/conflicting replay, denied and wrong-tenant access, known failure, timeout or ambiguous outcome, size/path boundary, and sensitive-data exclusion are proven without duplicate provider effects
**And** restart-safe evidence contains only approved metadata and completion requires the real deployed GitHub composition, not fake-only, NoOp, seed, or safe-empty evidence.

### Story 3.12: Forgejo repository provisioning, binding, and branch/ref behavior

As an authorized actor,
I want Forgejo repository provisioning, existing-repository binding, and branch/ref behavior to execute through the canonical provider port,
So that supported Forgejo installations provide the same product semantics with explicit capability differences.

**Acceptance Criteria:**

**Given** the configured Forgejo version, tenant binding, credential reference, policy, and readiness evidence are supported and current
**When** an authorized create or bind operation executes through the real Forgejo transport
**Then** authorization precedes protected observation, canonical repository identity and exact ref semantics decide the result, and one eligible mutation occurs without provider DTO leakage
**And** duplicate/alias conflict, equivalent/conflicting replay, version drift, denial, known failure, timeout/unknown outcome, and reconciliation behavior use stable provider-neutral categories
**And** restart-safe metadata excludes credentials, endpoints, raw repository/ref values, and response bodies, and completion requires deployed positive, negative, tenant-isolation, and boundary evidence rather than fake or unavailable evidence.

### Story 3.13: Forgejo file mutation, commit, status, and failure behavior

As an authorized workspace actor,
I want Forgejo file mutation, commit, and status operations implemented through the canonical provider port,
So that repository-backed work remains deterministic across supported Forgejo versions.

**Acceptance Criteria:**

**Given** a supported Forgejo version and an authorized lock-owning task
**When** add, change, remove, commit, or status behavior executes
**Then** the adapter enforces path/ref/C4 policy, preserves ordering and canonical metadata, and maps version-specific responses to the shared success, conflict, failure, and unknown-outcome model
**And** equivalent/conflicting replay, wrong-tenant denial, known failure, timeout/ambiguity, cancellation, file-size/type/path boundaries, and contract-drift behavior are proven without duplicate effects or secret/content leakage
**And** completion requires real deployed Forgejo composition and restart-safe evidence; mocks, fakes, NoOp, unavailable, or safe-empty results alone cannot satisfy it.

### Story 3.14: Complete asynchronous repository creation and binding

As an authorized actor,
I want a requested repository creation or binding to reach a durable terminal folder state,
So that I can observe whether asynchronous provider work completed, failed, or requires reconciliation.

**Acceptance Criteria:**

**Given** Story 3.6 has emitted an authorized request and the applicable Story 3.10 or 3.12 provider behavior is available
**When** the worker subscribes, executes, persists, restarts, retries, or reconciles the process
**Then** the process advances through the canonical C6 lifecycle and records one terminal task/binding result with current retry eligibility and sanitized evidence
**And** equivalent replay causes no duplicate repository or binding, conflicting replay is rejected, known failure is terminal as defined, and unknown provider outcome is checked automatically within the governed budget before `reconciliation_required`
**And** deployed durable success, restart replay from an empty checkpoint, tenant isolation, denial, conflict, failure, timeout/unknown, and boundary evidence are required; no in-memory, NoOp, seed-only, unavailable, safe-empty, or fake-only proof counts as completion.

## Product Epic 4: Repository-Backed Workspace Task Lifecycle

Developers and AI agents can prepare workspaces, acquire locks, mutate files safely, query bounded context, commit changes, and receive deterministic failure, status, idempotency, and redaction behavior through the canonical repository-backed task lifecycle.

_**Reconciled production-closure ownership (2026-08-04):** the workspace transition-evidence seam remains safely unavailable until Story 4.18 authors and deploys its EventStore-backed projection. Stories 4.19–4.21 own the durable prepare/lock, mutation/context, commit, conflict, and reconciliation proof. Story 11.10 owns EventStore admission/subscription seam adoption only and owns no product projection. Existing seed-backed and deterministic component evidence is preserved but is not production-completion evidence._

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
**And** `unknown_provider_outcome` is recorded before bounded automatic reconciliation checks begin
**And** `reconciliation_required` is used only after the governed automatic check/time budget is exhausted, never as a synonym for the initial ambiguous result.

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
**Then** tenant access, folder ACL, path policy, sensitivity classification, binary/large-file policy, cancellation, authoritative-content-source rules, and C4 limits are enforced before execution: at most 100 requested paths, 2,000 tree entries, 500 search/glob results, 262,144 bytes per bounded range, 1,048,576 serialized aggregate bytes, and 2 seconds of server execution
**And** denied queries produce metadata-only audit evidence
**And** any semantic/RAG retrieval backend, including Hexalith.Memories, is invoked only after Folders authorization and policy checks pass
**And** tree, metadata, range, glob, and search families return their canonical result/error and truncation semantics independently, and derived semantic indexes are never authoritative for tenant access, folder ACL, file truth, workspace state, or audit truth.

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

**Given** provider readiness, repository binding, workspace preparation, lock acquisition or release, file mutation, context query, commit, cleanup-status, read-model freshness, or authorization evaluation returns a failure
**When** the response is produced
**Then** it includes final state per C6, retry eligibility, retry-after hint when known, correlation ID, operation ID where available, task ID where applicable, sanitized reason category, client action, and metadata-only supporting details
**And** audit/projection consumers receive the required evidence fields without changing the canonical error shape.

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

_**Disposition:** Historical batch. Preserve its completed evidence and do not reactivate this umbrella; new durable mutation/context gaps belong to Story 4.20._

As a maintainer,
I want sentinel-redaction, path-security, encoding-equivalence, and cross-tenant isolation tests for the lifecycle,
So that secret safety, path safety, encoding stability, and tenant isolation are checked mechanically.

**Acceptance Criteria:**

**Given** lifecycle operations and fixtures exist
**When** security boundary tests run
**Then** sentinel, path, encoding, and cross-tenant negative cases fail on any leak or unsafe acceptance
**And** parallel tenant/task scenarios prove lock contention, stale-lock behavior, interrupted lifecycle attempts, and cross-tenant identifiers cannot leak or mutate another tenant's workspace
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

### Story 4.18: EventStore-backed workspace transition-evidence projection

As an authorized caller,
I want transition evidence populated from durable workspace events,
So that lifecycle decisions can be inspected after restart without a seed-only read model.

**Acceptance Criteria:**

**Given** Story 12.1 supplies durable ordered source events and Story 12.2 supplies the durable projection substrate
**When** the deployed Server registers the EventStore-backed transition-evidence projector and replays from an empty checkpoint
**Then** C6 transitions, task/operation identity, timestamps, retry eligibility, failure/reconciliation metadata, and freshness are rebuilt deterministically and survive host restart
**And** correct-tenant reads return populated metadata while wrong-tenant, unauthorized, stale, corrupt, and unavailable paths return safe canonical results without existence or sensitive-data leakage
**And** real deployed population, restart replay, tenant isolation, denial, conflict, failure, timeout/unknown, and boundary evidence are required; the in-memory/seed/unavailable default is retained only as honest degraded behavior and cannot prove completion.

### Story 4.19: Prove durable workspace prepare and lock lifecycle

As a developer or AI agent,
I want prepare and lock behavior proven through the durable production path,
So that workspace ownership survives restart and prevents colliding writers.

**Acceptance Criteria:**

**Given** Stories 12.1–12.3 provide durable folder, task, projection, and authoritative state/content foundations
**When** prepare, acquire, inspect, release, expiry, stale, and revocation paths execute through REST → gateway → processor → authorization gate → EventStore/projection
**Then** the exact lock vocabulary `unlocked`, `locked`, `expired`, `stale`, `revoked` and the canonical tenant + repository + normalized-ref serialization identity are preserved across restart and empty-checkpoint replay
**And** positive behavior plus wrong-tenant/unauthorized denial, alias collision, equivalent/conflicting replay, known failure, timeout/unknown outcome, expiry boundary, terminal status, retry eligibility, and metadata-only audit are proven without a mocked gateway
**And** NoOp, in-memory, seed-only, unavailable, safe-empty, or fake-only evidence cannot satisfy completion.

### Story 4.20: Prove durable file mutation and bounded-context lifecycle

As a developer or AI agent,
I want file mutations and bounded context queries proven against authoritative durable content,
So that safe repository work remains correct across restart and every context-query family.

**Acceptance Criteria:**

**Given** Stories 12.1–12.3 provide durable events, task state, and authoritative file content and the caller has current tenant/folder authorization
**When** add/change/remove and tree/metadata/range/glob/search behavior runs through the real deployed production path
**Then** mutation ordering, all-mutation idempotency, lock ownership, path policy, cancellation, content authority, task/projection state, and replay survive restart without duplicate effects
**And** C4 enforces 100 requested paths, 2,000 tree entries, 500 search/glob results, 262,144 bytes per range, 1,048,576 aggregate bytes, and 2 seconds; each query family proves canonical success, truncation, denial, limit, unavailable, and cancellation semantics independently
**And** wrong-tenant denial, conflict, known failure, timeout/unknown, boundary, metadata-only audit, and sensitive-content exclusion are attached, and no fake, NoOp, seed, unavailable, or safe-empty evidence alone counts as completion.

### Story 4.21: Prove real commit, retry, conflict, and unknown-outcome reconciliation

As a developer or AI agent,
I want commits and ambiguous outcomes proven through the real durable Git path,
So that a task reaches one trustworthy terminal or recovery state without duplicate commits.

**Acceptance Criteria:**

**Given** Stories 12.1–12.4 provide durable state/content and the real Git commit/provider-write path
**When** an authorized lock owner commits, retries an equivalent request, submits a conflicting request, encounters a known failure, or receives an ambiguous post-dispatch result
**Then** exactly one eligible commit occurs, durable commit evidence and terminal task/projection state survive restart, and `unknown_provider_outcome` runs bounded automatic reconciliation before any `reconciliation_required` state
**And** deployed success, denial, wrong-tenant access, replay/conflict, provider failure, timeout/unknown outcome, reconciliation-budget boundary, metadata-only audit, and sensitive-data exclusion are proven end to end
**And** mocks, NoOp executors, in-memory state, seed records, unavailable/safe-empty paths, or fake Git evidence alone cannot support completion.

## Product Epic 5: Cross-Surface Workflow Parity

API, SDK, CLI, and MCP users can run the same canonical lifecycle with equivalent operation identity, errors, idempotency, audit behavior, authorization outcomes, terminal states, and mixed-surface handoff.

Parity is verified through generated oracle rows and shared conformance tests reused across surfaces, not by manual comparison of independently implemented behavior.

### Story 5.1: Ship SDK convenience helpers, samples, and quickstart

As an SDK consumer,
I want ergonomic helpers, samples, and quickstart material,
So that I can use the canonical lifecycle without learning internal transport details.

**Acceptance Criteria:**

**Given** generated SDK methods exist
**When** helpers and samples are added
**Then** upload convenience, idempotency guidance, correlation/task ID handling, and a local AppHost sample are documented
**And** helpers do not introduce lifecycle semantics absent from the Contract Spine.

### Story 5.2: CLI tenant, folder, provider-readiness, and binding commands

As a CLI user,
I want commands for tenant, folder, provider readiness, repository binding, and branch/ref policy workflows,
So that command-line control-plane use behaves like SDK and REST use.

**Acceptance Criteria:**

**Given** the SDK client is available
**When** CLI control-plane commands are implemented
**Then** tenant, folder, provider-readiness, repository-binding, and branch/ref-policy commands wrap SDK behavior
**And** pre-SDK errors, idempotency-key sourcing, correlation sourcing, and exit codes follow the Adapter Parity Contract.

### Story 5.3: MCP tenant, folder, provider-readiness, and binding tools/resources

As an MCP client,
I want tools and resources for tenant, folder, provider readiness, repository binding, and branch/ref policy workflows,
So that AI tools can use the control-plane slice without direct filesystem or provider ownership.

**Acceptance Criteria:**

**Given** the SDK client is available
**When** MCP control-plane tools and resources are implemented
**Then** one tool per tenant, folder, readiness, binding, or policy command/query is available where appropriate
**And** failures map to the canonical MCP failure-kind set with correlation ID, code, retryability, and client action.

### Story 5.4: Consume parity oracle in CLI and MCP tests

As a maintainer,
I want CLI and MCP tests to consume behavioral-parity oracle columns,
So that adapter behavior cannot drift from the canonical contract.

**Acceptance Criteria:**

**Given** `parity-contract.yaml` exists
**When** CLI and MCP tests run
**Then** behavioral-parity columns drive assertions for pre-SDK errors, key sourcing, correlation sourcing, exit codes, and failure kinds
**And** shared conformance scenarios are reused across CLI and MCP where behavior should match
**And** missing rows or unsupported categories fail tests.

### Story 5.5: Validate golden lifecycle parity across REST and SDK

As a stakeholder validating one canonical workflow contract,
I want the golden lifecycle scenario executed through REST and SDK,
So that transport parity is proven before CLI and MCP adapter behavior is layered on.

**Acceptance Criteria:**

**Given** REST endpoints and SDK client are available
**When** the golden lifecycle scenario runs through both surfaces
**Then** operation identity, authorization, errors, idempotency, audit metadata, correlation, and terminal states match oracle expectations
**And** shared conformance fixtures cover the canonical flow of provider readiness, repository binding, prepare, lock, file change, commit, context query, status, and audit inspection
**And** any transport drift fails loudly.

### Story 5.6: Validate behavioral parity across CLI and MCP

As a stakeholder validating adapter behavior,
I want CLI and MCP behavior tested against the same canonical lifecycle rules,
So that adapter-specific UX does not change product semantics.

**Acceptance Criteria:**

**Given** CLI and MCP surfaces wrap the SDK
**When** behavioral parity tests run
**Then** credential sourcing, usage errors, idempotency-key sourcing, correlation defaults, CLI exit codes, and MCP failure kinds match the Adapter Parity Contract
**And** adapters preserve canonical names, state language, evidence fields, and error categories.

### Story 5.7: Validate mixed-surface handoff scenario

As an automation developer,
I want one task lifecycle to move between REST, SDK, CLI, and MCP using the same IDs,
So that real integrations can hand off work without losing state or auditability.

**Acceptance Criteria:**

**Given** all four surfaces are available
**When** provider readiness, create/bind, prepare, lock, write, query, commit, status, and release are split across surfaces
**Then** task ID, correlation ID, operation IDs, audit records, and terminal state remain coherent
**And** any surface-specific drift in idempotency replay or error category fails the scenario.

### Story 5.8: CLI workspace preparation and lock lifecycle

As a CLI user,
I want to prepare a workspace and inspect, acquire, and release its task-scoped lock,
So that scripted work uses the same durable lifecycle and lock semantics as REST and SDK.

**Acceptance Criteria:**

**Given** the Epic 4 durable prepare/lock path is deployed and the caller has current tenant/folder authorization
**When** CLI prepare and lock commands run
**Then** inputs, operation identity, exact lock states, C6 lifecycle, idempotency, canonical results/errors, exit codes, and metadata-only output match the generated parity oracle
**And** success, denial/wrong-tenant, equivalent/conflicting replay, lock collision/expiry, failure, timeout/unknown, and input boundaries are verified against real deployed composition
**And** fake, seed-only, unavailable, safe-empty, or in-memory behavior alone cannot prove CLI completion.

### Story 5.9: CLI file, context, commit, status, error, and audit behavior

As a CLI user,
I want to mutate files, query bounded context, commit, inspect status, and review safe audit evidence,
So that the complete CLI workflow preserves canonical behavior and security.

**Acceptance Criteria:**

**Given** Epic 4 and Epic 12 production paths are available
**When** CLI file/context/commit/status/audit commands execute
**Then** C4 bounds, lock and all-mutation idempotency rules, canonical errors, terminal/recovery states, retry eligibility, output shaping, and parity-oracle semantics are preserved without exposing content or secrets outside authorized response rules
**And** deployed success plus denial, conflict, provider failure, timeout/unknown reconciliation, cancellation/size/path boundaries, tenant isolation, and metadata-only audit are proven
**And** NoOp, mock-only, seed, unavailable, safe-empty, or fake evidence cannot satisfy completion.

### Story 5.10: MCP workspace preparation and lock lifecycle

As an MCP client,
I want tools and resources for workspace preparation and task-scoped lock lifecycle,
So that AI agents receive the same durable lifecycle semantics as other surfaces.

**Acceptance Criteria:**

**Given** the Epic 4 durable prepare/lock path is deployed and the actor is authorized
**When** MCP preparation and lock operations run
**Then** tool/resource schemas, operation identity, exact lock states, C6 lifecycle, idempotency, failure kind, retryability, client action, and eventual-acceptance wording match the parity oracle
**And** success, wrong-tenant/authorization denial, equivalent/conflicting replay, collision/expiry, known failure, timeout/unknown, and schema/input boundaries are proven through deployed composition
**And** fake, seed-only, unavailable, safe-empty, or in-memory behavior alone cannot prove completion.

### Story 5.11: MCP file, context, commit, status, error, and audit behavior

As an MCP client,
I want tools and resources for file mutation, bounded context, commit, status, errors, and safe audit evidence,
So that AI-agent workflows retain full cross-surface parity.

**Acceptance Criteria:**

**Given** Epic 4 and Epic 12 production paths are available
**When** MCP file/context/commit/status/audit operations execute
**Then** schemas, C4 bounds, lock and all-mutation idempotency rules, terminal/recovery states, failure kinds, retryability, client action, and metadata-only output match the generated parity oracle
**And** deployed success plus denial, conflict, provider failure, timeout/unknown reconciliation, cancellation/size/path boundaries, tenant isolation, and sensitive-data exclusion are proven
**And** NoOp, mock-only, seed, unavailable, safe-empty, or fake evidence cannot satisfy completion.

## Product Epic 6: Read-Only Workspace Trust Console And Audit Review

Operators, tenant administrators, and audit reviewers can find a workspace, prove its tenant boundary, inspect readiness, locks, dirty state, failures, commits, provider evidence, metadata-only folder visibility, timelines, and audit records through a FrontComposer/Fluent UI read-only console without mutation or file-content exposure.

This epic implements UX-DR1 through UX-DR30 directly; UX-DR31 and UX-DR32 are verified through Story 6.11 and release-evidenced through Workstream 7.
Epic 6 owns the console experience, while Epics 3-5 own the readiness, lifecycle, parity, status, and evidence semantics that make the console truthful. Console stories must consume those shared semantics rather than defining UI-only state names or hidden control paths.

_**Reconciled production-closure ownership (2026-08-04):** the seven deployed diagnostic views remain safely unavailable until Stories 6.12–6.13 author and register their EventStore-backed projections and Story 6.14 proves populated deployed journeys. Story 4.18 separately owns workspace transition evidence. Workstream 11 owns platform seam adoption and verification lanes only; it owns no product projection. Existing seed-backed views are honest degraded seams, not positive completion evidence._

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

**Given** PRD console requirements, architecture decisions F-1 through F-7, the FrontComposer technical research, and `_bmad-output/planning-artifacts/ux-design-specification.md` exist
**When** console wireflow notes are authored
**Then** folder, workspace, provider, audit, incident-mode, redaction, loading, empty, and error states are described under `docs/ux/ops-console-wireflows.md`
**And** the notes identify FrontComposer shell layout, navigation, projection-view composition, tenant/user context expectations, read-only command-suppression behavior, and generated/custom projection boundaries
**And** the notes identify UX-DR1 through UX-DR30 implementation expectations, including keyboard-navigation, focus, non-color-only status, zoom readability, responsive fallback, and redaction-vs-missing behavior for Epic 6 stories
**And** the notes map UX-DR1 through UX-DR32 to owning and supporting stories, marking console-only requirements separately from cross-surface readiness, lifecycle, parity, status, and evidence semantics
**And** the notes define the shared visible status taxonomy for readiness, locked, prepared, dirty, committed, audited, failed, stale, unavailable, inaccessible, redacted, and unknown states
**And** primary diagnostic flows answer what happened, who or what caused it, when it happened, from which surface it came, and whether the evidence can be trusted
**And** Stories 6.6, 6.7, 6.8, 6.9, and 6.10 cannot begin implementation until `docs/ux/ops-console-wireflows.md` exists and has been reviewed against PRD console requirements, architecture decisions F-1 through F-7, the UX design specification, and the FrontComposer technical research.

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
**Then** incident-admin permission and fresh tenant/folder authorization are verified before stream lookup, result counting, checkpoint access, filtering, empty-state classification, or response shaping
**And** denial performs no protected observation, emits exactly one safe metadata-only audit record, and leaks no stream, tenant, folder, count, checkpoint, or event existence
**And** an authorized view shows a persistent degraded-mode banner, bounded C9-redacted event metadata, disposition labels from Story 6.3, and correlation/time-window copy affordance
**And** redacted values render through the shared redaction component from Story 6.4 with no relaxed policy or file content.

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

_**Disposition:** Historical batch. Preserve its completed evidence and do not reactivate this umbrella; new projection/runtime gaps belong to Stories 6.12–6.14._

As a release reviewer,
I want the console verified as read-only and WCAG 2.2 AA conformant,
So that the MVP console satisfies its safety and accessibility promises.

**Acceptance Criteria:**

**Given** the console is feature complete
**When** verification runs
**Then** automated and manual checks confirm no mutation paths, credential reveal, file-content browsing, file editing, raw diff display, hidden repair action, or unrestricted filesystem browsing
**And** responsive checks cover desktop, tablet, and mobile fallback widths with dense identifiers and long paths in tables, timelines, metadata trees, and trust summaries
**And** browser zoom checks at 125%, 150%, and 200% confirm text, controls, tables, and key workflows remain readable and usable
**And** accessibility validation covers automated checks, keyboard-only walkthroughs for the three critical journeys, screen reader review for summary/folder/redaction/audit flows, forced-colors or high-contrast checks where supported, color-blindness review, focus management, semantic headings, readable tables, contrast, and non-color-only indicators against WCAG 2.2 AA expectations.

### Story 6.12: Populate readiness, lock, dirty-state, and failed-operation projections

As a tenant-scoped operator,
I want readiness, lock, dirty-state, and failed-operation views populated from durable events,
So that the console reports real production state after restart.

**Acceptance Criteria:**

**Given** Stories 12.1–12.2 provide durable ordered events and projection infrastructure
**When** the four EventStore-backed diagnostic projections are registered in the deployed Server and replay from an empty checkpoint
**Then** they populate deterministic tenant/folder-scoped records, survive restart, expose freshness/availability honestly, and never derive authority from seed data
**And** authorized populated reads plus wrong-tenant/unauthorized denial, conflict/corrupt event, failure, timeout/unavailable, empty-checkpoint, replay-boundary, and metadata-redaction evidence are attached
**And** in-memory, seed-only, NoOp, unavailable, safe-empty, or fake-only evidence cannot satisfy completion.

### Story 6.13: Populate provider-status, sync-status, and projection-freshness projections

As a tenant-scoped operator,
I want provider status, sync status, and projection freshness populated from durable evidence,
So that the console distinguishes current, stale, degraded, and unavailable production state.

**Acceptance Criteria:**

**Given** durable provider, synchronization, and projection-checkpoint events are available from Epic 12 and the owning product flows
**When** the three EventStore-backed projections register, consume, restart, and replay from an empty checkpoint
**Then** deterministic tenant/folder records expose safe provider result, sync/reconciliation state, checkpoint/freshness, and availability metadata without credentials, raw repository identities, or content
**And** authorized populated reads plus wrong-tenant/unauthorized denial, replay conflict/corruption, source failure, timeout/unavailable, empty-checkpoint, freshness-boundary, and redaction evidence are attached
**And** seed-only, in-memory, NoOp, unavailable, safe-empty, or fake-only evidence cannot satisfy completion.

### Story 6.14: Prove populated deployed-host diagnostic and transition-evidence journeys

As a release reviewer,
I want the deployed operations console exercised against populated diagnostic and transition-evidence records,
So that operator, audit, and incident journeys prove production truth rather than empty-safe scaffolding.

**Acceptance Criteria:**

**Given** Stories 4.18, 6.12, and 6.13 are deployed with durable data and UX-DR33 dual incident authorization
**When** readiness, lock, dirty, failure, provider, sync, freshness, transition timeline, audit, and incident journeys execute in the real host
**Then** all seven diagnostic views and transition evidence show populated tenant-correct records after restart, render the six independent state dimensions correctly, and preserve read-only WCAG 2.2 AA behavior
**And** success plus wrong-tenant/unauthorized denial, stale/degraded/unavailable, conflicting/corrupt evidence, timeout, empty and populated boundaries, no-mutation, redaction, and exactly-one denial-audit behavior are proven
**And** no completion claim may rely only on seed, in-memory, fake, unavailable, safe-empty, or component-only evidence.

## Release, Platform, Governance, Enabling, And Hardening Workstreams

The following release and enabling sections remain outside product-completion metrics. Product Epics 10 and 12 appear later because their dependencies were ratified after the original MVP sequence; they still count as user-value product epics.

## Release Readiness Workstream 7: MVP Release Readiness And Operational Evidence

Release stakeholders can verify that the MVP satisfies security, tenant isolation, parity, provider compatibility, Dapr policy, retention, observability, capacity, accessibility, documentation, package-traceability, and NFR traceability evidence before production acceptance.

This workstream is not a product FR-bearing epic. It is a release-readiness gate that continuously consumes evidence from Epics 1-6 and blocks MVP acceptance when required evidence is missing. Sprint planning must treat these items as release governance and hardening work, not as a peer product capability increment.

> **CI submodule-posture guardrail (pinned 2026-06-22):** The canonical rule is CI checkout `submodules: false` (no PackageReference fallback assumptions); local setup may show only the explicit root-level submodule command. This is authoritative over any per-story wording drift in Stories 7.4–7.8. Source: `project-context.md` Development Workflow Rules + root `CLAUDE.md`.

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
**And** shared conformance tests cover REST, SDK, CLI, MCP, and mixed-surface golden workflows
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
**And** evidence includes tenant-isolation/security gates, audit completeness, workspace status/context-query performance baselines, CLI/MCP smoke tests, console accessibility/responsive validation, and operational runbook proof
**And** missing NFR evidence fails the release-readiness review.

### Story 7.17: Publish ADR set and maintenance runbooks

_**Disposition:** Historical batch. Preserve its completed evidence and do not reactivate this umbrella; new ADR work belongs to its ratified narrow owner._

As a future maintainer or architect,
I want ADRs and lifecycle runbooks published,
So that design rationale and operational decisions survive handoff and release pressure.

**Acceptance Criteria:**

**Given** MVP release evidence is complete
**When** ADRs and runbooks are reviewed
**Then** ADRs cover major contract, provider, idempotency, security, observability, and deployment decisions
**And** runbooks cover tenant deletion, retention, alerts, rollback, provider drift, reconciliation, and incident-mode operations.

### Story 7.18: Restore shared test-host composition baseline

_Added 2026-05-31 via the bmad-correct-course Sprint Change Proposal (reopens Epic 7). Owns the systemic test-host DI-composition red surfaced during 2-8b verification — distinct from, and ~50× larger than, the epic-1 CLI-reds historical-reds item._

As a platform engineer,
I want every in-process test host that mounts the Folders server surface to compose the same auth-scheme and health-check primitives the production surface now requires,
So that the MVP test suite runs green at HEAD and the "conditionally release-ready" claim rests on an honestly-passing baseline rather than ≈352 silently-red tests from a single composition gap.

**Acceptance Criteria:**

**Given** `AddFoldersServer()` registers `FoldersAuthSchemeValidator` (needs `IAuthenticationSchemeProvider`) and `MapFoldersServerEndpoints()` maps health endpoints (need `HealthCheckService`)
**When** a shared test-host helper (`AddAuthentication()`+`AddHealthChecks()`) is applied across all affected hosts
**Then** `Hexalith.Folders.Server.Tests` reports Total 433 / Failed 0, and the `IntegrationTests` (Golden/MixedSurface) and `Folders.Tests` (Epic 3 provider-boundary) composition reds clear
**And** a central host-composition smoke test (`ValidateOnBuild`) guards the shared-surface DI contract against recurrence
**And** no production code behavior changes.

## Release Closure Workstream 8: MVP Release Acceptance Closure

Release stakeholders can accept the MVP once the bounded, non-planning release-acceptance conditions from the 2026-06-22 implementation-readiness review are closed: the canonical REST contract is fully served (47/47 operations), the operations console has an automated WCAG 2.2 AA gate, C3 retention has Legal sign-off, and the solution test baseline is honestly green.

_Created 2026-06-22 via bmad-correct-course (`sprint-change-proposal-2026-06-22.md`). Release-acceptance **closure** epic — not a feature workstream; no new product FR scope. Verified parity ground truth (adversarial workflow, 2026-06-22): REST 32/47, SDK 47/47, MCP 47/47, CLI 40/47 (7 diagnostics MCP-only by design); 15 operations declared by the spine and wrapped by SDK/CLI/MCP but missing a server route. Detailed as-built ACs live in the `8-*` story files under `implementation-artifacts/`. Story 8.5 was split 2026-06-23 (`sprint-change-proposal-2026-06-23-story-8-5-legal-blocker-split.md`): its dev scope (residual-reds honest-green baseline) stays in 8.5 (done). Story 8.6 recorded C3 Legal sign-off on 2026-06-24 (`Jérôme Piquot`, Louveciennes; PM Jerome 2026-06-22), applied the in-lockstep C3 retention cascade, and is done. The retention-deletion gate now reports `status=passed` and `policy_status=approved`._

### Story 8.1: Implement the 8 missing Bucket-A canonical REST server routes

_**Disposition:** Immutable historical remediation batch, excluded from the active implementation-ready backlog. Preserve completed evidence and lifecycle history._

As an API consumer,
I want every canonical operation the SDK/CLI/MCP already wrap to have a working REST server route,
So that cross-surface parity is real and CLI/MCP calls do not hit unimplemented endpoints (404).

**Acceptance Criteria:**

**Given** the spine declares and SDK/CLI/MCP wrap `CreateFolder`, `ListFolderAclEntries`, `UpdateFolderAclEntry`, `ConfigureProviderBinding`, `GetProviderBinding`, `GetRepositoryBinding`, `GetWorkspaceRetryEligibility`, `GetWorkspaceTransitionEvidence`
**When** each server route is implemented against existing aggregates/query handlers (no spine change)
**Then** all 8 respond on REST with canonical envelopes, problem categories, and idempotency behavior matching the spine, mutating ops proven by a no-mock `IEventStoreGatewayClient` integration test
**And** REST coverage reaches 40/47 (Bucket A closed).

### Story 8.2: Implement the 7 ops-console diagnostics REST server routes (Bucket B)

_**Disposition:** Immutable historical remediation batch, excluded from the active implementation-ready backlog. Preserve completed evidence and lifecycle history._

As an operator,
I want the ops-console diagnostics operations to have working REST server routes,
So that the read-only console and REST consumers can retrieve diagnostics evidence.

**Acceptance Criteria:**

**Given** the spine declares and SDK+MCP wrap the 7 diagnostics ops (`GetReadinessDiagnostics`, `GetProviderStatusDiagnostics`, `GetSyncStatusDiagnostics`, `GetLockDiagnostics`, `GetDirtyStateDiagnostics`, `GetFailedOperationDiagnostics`, `GetProjectionFreshness`)
**When** REST server routes are added (CLI stays diagnostics-free by design)
**Then** all 7 respond read-only, metadata-only, projection-backed, authorization-before-observation
**And** REST coverage reaches 47/47 and the parity oracle + contract-spine drift gate pass.

### Story 8.3: Wire-exercise cross-surface parity and gate the four-surface claim

As a release stakeholder,
I want golden-lifecycle and mixed-surface parity exercised over the wire across all four surfaces,
So that the four-surface parity guarantee is true before it is asserted to consumers.

**Acceptance Criteria:**

**Given** 47/47 server routes (8.1 + 8.2)
**When** the golden-lifecycle and mixed-surface scenarios run
**Then** all 9 lifecycle steps are driven over the real transport, `folder_acl_denied` returns HTTP 403 (not 503), and `idempotency_conflict` surfaces at HTTP 409, with matching CLI exit codes and MCP failure kinds
**And** the public "four-surface canonical-lifecycle parity" claim is asserted only after this story passes.

### Story 8.4: Stand up an automated axe/WCAG 2.2 AA CI gate for the operations console

As a release stakeholder,
I want an automated accessibility gate against the read-only console,
So that the PRD accessibility release-validation path (NFR-A11Y-1..5, NFR-VER-3) is enforced, not asserted.

**Acceptance Criteria:**

**Given** the read-only console routes from Story 6-2
**When** an axe-core / WCAG 2.2 AA gate is wired into CI (registered in the gate inventory, closing the I-5 absence)
**Then** it fails CI on AA violations across the three critical console journeys, covering keyboard nav, visible focus, semantic structure, contrast, and not-color-alone indicators, plus zoom (125/150/200%) and dense-identifier no-clipping (UX-DR31)
**And** its green run is the recorded accessibility release-validation evidence.

### Story 8.5: Drive the residual test baseline honestly green

_**Disposition:** Historical batch. Preserve its completed evidence, including the original dedicated-story title, and do not reactivate this umbrella; new defects require narrow ratified follow-ups._

_Split 2026-06-23 (bmad-correct-course, `sprint-change-proposal-2026-06-23-story-8-5-legal-blocker-split.md`): the original story's AC1 — C3 Legal sign-off — moved to **Story 8.6** and was completed after recorded Legal approval on 2026-06-24. Story 8.5 retains the residual-reds + honest-green-baseline scope and is **done**._

As a release stakeholder,
I want the residual non-composition reds resolved or explicitly accepted and the obsolete CI masks removed,
So that the MVP rests on an honestly-green solution test baseline that proves — not hides — its own passing tests.

**Acceptance Criteria:**

**Given** the residual non-composition reds from the 2026-06-22 readiness snapshot (`Testing.Tests` ×4 governance, `Contracts.Tests` ×4 epic-1 CLI negative-scope, Epic 3 provider-boundary guards, `UI.E2E` ×40 Playwright provisioning)
**When** each is triaged — verified-green-and-unmasked, or explicitly accepted with rationale — and the obsolete fail-open `--filter` masks in `run-baseline-ci-gates.ps1` are removed
**Then** the union of CI gate lanes is honestly green (zero unexplained reds AND zero obsolete fail-open masks), the full 63-test UI.E2E lane runs in a Chromium-provisioned CI job, and the result is recorded as release evidence.

### Story 8.6: Record C3 Legal sign-off and apply the in-lockstep C3 retention cascade

_Split 2026-06-23 (bmad-correct-course) from Story 8.5 AC1. Completed after recorded Legal sign-off on 2026-06-24 (`Jérôme Piquot`, Louveciennes; PM Jerome 2026-06-22). The in-lockstep C3 retention cascade is applied, C3 is approved in governance evidence, and the retention-deletion gate is non-blocking. See `_bmad-output/implementation-artifacts/8-6-record-c3-legal-signoff-and-apply-cascade.md`._

As a release stakeholder,
I want C3 Legal sign-off recorded and the full in-lockstep C3 retention cascade applied in one commit,
So that the MVP rests on a fully-approved governance posture and the release-blocking posture clears.

**Acceptance Criteria:**

**Given** PM approval recorded 2026-06-22 and recorded external Legal sign-off evidence in hand (never fabricated)
**When** the dev records the Legal approval and applies the documented in-lockstep cascade in one commit (governance YAML first, then the C3 doc approval-state cells, the gate-script literals, the four `RetentionAndTenantDeletionConformanceTests` assertions, and the narrating docs, while preserving the accepted NFR57 reference-pending deviation documented in the story file)
**Then** C3 flips to `approved` (`c0-c13-governance-evidence.yaml` + `c3-retention.md`; the `release_blocking_until_legal_approval` posture clears), `run-retention-deletion-gates.ps1` reports non-blocking, and the contract-spine lane stays green
**And** the per-class retention values, tenant-deletion dispositions, `reference_pending_*` class identifiers, and runtime `RetentionClassToken` markers are unchanged (no spine/generated-client/aggregate change), closing the former MVP-release blocker.

## Technical Enabling Workstream 9: AppHost Platform Alignment And Memories Search-Index Topology

Platform engineers can run the full Folders topology composed purely through the shared platform Aspire helpers — EventStore command gateway (gateway-only), Tenants, and the Memories search-index server — with `hexalith-folders → folders-index` routing configured, replacing the hand-rolled `FoldersAspireModule` Dapr wiring.

_Created 2026-06-22 via bmad-correct-course (`sprint-change-proposal-2026-06-22-apphost-memories-platform-alignment.md`). Infrastructure-alignment + additive Memories hosting epic; no new product FR scope. Decisions: EventStore **gateway-only** (`AddHexalithEventStore(adminServer: null)` — no admin server/UI); Memories depth **topology + AppHost routing config** (end-to-end indexing gated on Epic 10 producer). Behavior-preserving for `folders`/`folders-workers`/`folders-ui` sidecars._

### Story 9.1: Adopt the EventStore and Tenants platform Aspire helpers

As a platform engineer,
I want the Folders AppHost to compose EventStore and Tenants via the shared platform Aspire helpers,
So that Folders stops re-implementing shared Dapr topology and matches the canonical Tenants AppHost.

**Acceptance Criteria:**

**Given** `FoldersAspireModule.AddHexalithFolders` / `AddFoldersSharedDaprComponents` hand-roll the EventStore/Tenants sidecars and the shared `statestore`/`pubsub` components
**When** the AppHost is refactored to call `AddHexalithEventStore(eventStore, adminServer: null, …)` + `AddHexalithTenantsServer(eventStoreResources, …)`, the AppHost csproj references `Hexalith.EventStore.Aspire` + `Hexalith.Tenants.Aspire` (dropping the direct EventStore/Tenants runtime project refs), and the `statestore.yaml`/`pubsub.yaml`/`resiliency.yaml` Dapr component files are added under `DaprComponents/`
**Then** `folders`, `folders-workers`, and `folders-ui` keep identical app-IDs, sidecars, and references, and no hand-rolled EventStore/Tenants Dapr wiring remains
**And** `AspireTopologyTests` + `AppHostBootSmokeTests` are updated and green, and `aspire run` brings the topology up healthy.

### Story 9.2: Add the Memories search-index server to the AppHost topology

As a platform engineer,
I want the Memories search-index server hosted in the Folders topology,
So that the platform semantic search-index service runs alongside Folders locally and in release validation.

**Acceptance Criteria:**

**Given** Memories is registered in `.gitmodules` but unwired (no `HexalithMemoriesRoot`, no AppHost reference)
**When** `Directory.Build.props` resolves `HexalithMemoriesRoot`, the AppHost references `Hexalith.Memories.Aspire`, and `AddHexalithMemoriesSearchIndexServer(stateStore, pubSub, secretStorePath, llmPath, …)` is called reusing the shared state/pubsub, with `secretstore.memories.yaml` + `llm.memories.yaml` added under `DaprComponents/`
**Then** the topology adds `memories` (project + sidecar), `memories-vectors` (redis/redis-stack), `memories-graphs` (falkordb), `memories-secretstore`, and `memories-llm`, and the `memories` app-ID is registered
**And** access-control, structural topology tests, and container-image conformance are updated and green.

### Story 9.3: Apply Folders→Memories routing config and synchronize artifacts

As a platform engineer,
I want `hexalith-folders → folders-index` routing configured on the Memories resource and the architecture/context artifacts updated,
So that routing is in place (dormant until Epic 10) and the planning artifacts reflect the new topology.

**Acceptance Criteria:**

**Given** the Memories server is hosted (9.2)
**When** the AppHost sets `EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders=folders-index` and `AutoProvisionRoutedTenants=true` on the `memories` resource, and `architecture.md` (AppHost Composition, I-4/§756, I-3) + `project-context.md` (app-IDs + topology rule) are updated
**Then** the routing config is present and the `folders-index` tenant auto-provisions, and a Memories search-index handoff doc records that end-to-end ingestion/search is gated on the Epic 10 producer
**And** the updated artifacts are internally consistent (app-ID lists include `memories`).

## Product Epic 10: Authorized Folders Search And Index Lifecycle

Developers and AI agents can publish, remove, reconcile, authorize, search, and hydrate Folders-indexed metadata tokens through Memories while Folders remains authoritative and prevents cross-tenant or sensitive-data disclosure.

_Stories 10.1–10.5 are completed component increments, not FR58 completion evidence. Story 10.6 owns metadata-derived materialization under C4/C9, Story 10.7 owns the EventStore-backed bridge and deployed Server registration, Story 10.8 owns the non-empty authorized metadata-token round trip that completes current FR58, and Story 10.9 is a separate Security + Product-authorized body-content follow-on. Safe-empty and `Unavailable` behavior remains mandatory fail-safe behavior but cannot count as product completion._

_Epic 10 consumes Epic 12 durable source events, content/state authority, and at-least-once egress/reconciliation. Workstream 11 may supply shared seams and the DCP-capable lane, but owns no search projection and is not a hidden product-completion dependency._

**FRs covered:** FR58

_**Correction (2026-06-23, `sprint-change-proposal-2026-06-23-story-10-3-searchindexentrychanged-mechanism.md`):** The worker-side producer updates the Memories search index by **publishing `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvents** to `pubsub` / `memories-events` (source `hexalith-folders`, routed to `folders-index`) — the canonical mechanism proven by the live `hexalith-tenants → tenants-index` integration (`Hexalith.Tenants` `MemoriesSearchIndexEventPublisher`). It does **not** call `Hexalith.Memories.Client.Rest.IngestAsync`, which drives a separate RAG memory-ingestion subsystem (experimental `HXL001`; LLM embeddings → memory units) that the Epic 9 routing never ingests. Stories 10.1–10.4 are corrected accordingly; the in-review Story 10.3 `IngestAsync` egress is reworked to the event producer while the bridge projection, `/folders/events` subscription, orchestration, and authorization gating are preserved. The "Semantic-Indexing" naming is retained for traceability but denotes the syntactic/BM25 search index, not RAG embeddings (a full `semantic → search` rename is a tracked follow-up)._

### Story 10.1: Define the worker-side semantic-indexing port and Memories dependency

As a worker maintainer,
I want a worker-owned search-index publication port with a narrow Memories contracts dependency,
So that Folders can publish search-index events without leaking Memories dependencies into unrelated projects.

**Acceptance Criteria:**

**Given** the architecture restricts the Memories dependency to `Hexalith.Folders.Workers`
**When** a worker-side search-index publication port is defined and `Hexalith.Folders.Workers` takes a `Hexalith.Memories.Contracts` reference (the `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvent contracts) + Dapr pub/sub — NOT `Hexalith.Memories.Client.Rest`, whose `IngestAsync` drives the separate RAG memory-ingestion subsystem, not the search index
**Then** no other project (Contracts, core, CLI, MCP, UI, Server) depends on Memories.

### Story 10.2: Build the Folders-owned indexing bridge projection

As an operator and integration maintainer,
I want Folders to own the bridge projection between file versions and Memories search-index state,
So that indexing status remains auditable, tenant-scoped, and authoritative from the Folders side.

**Acceptance Criteria:**

**Given** durable Folders events as indexing triggers
**When** a bridge projection tracks `file version → Memories search-index entry/status`
**Then** it answers indexed / stale / skipped / failed / tombstoned / reconciliation-required per file version.

### Story 10.3: Author authorized asynchronous indexing on file-write and commit

As a developer or AI-agent consumer,
I want authorized file-write and commit events to publish curated search-index updates asynchronously,
So that search discovery can be updated without weakening Folders authorization or rolling back durable file operations.

**Acceptance Criteria:**

**Given** a file-write/commit event
**When**, after authorization (tenant → ACL → path policy → sensitivity → size/type limits), the worker publishes one curated `SearchIndexEntryChanged` CloudEvent per indexed unit (source `hexalith-folders`, pub/sub `pubsub` / topic `memories-events`, stable CloudEvent id and idempotency key)
**Then** a Memories/pub-sub outage surfaces as retryable indexing status and never rolls back a durable Folders file operation.

### Story 10.4: Emit SearchIndexEntryRemoved on removal/archive and prove end-to-end routing

As an operator and search-integration maintainer,
I want removed, archived, and tombstoned units to update the Memories search index correctly,
So that authorized search never returns stale live results for content Folders has removed from the active surface.

**Acceptance Criteria:**

**Given** Story 10.3 publishes `SearchIndexEntryChanged` on file-write/commit into `folders-index`
**When** the worker emits `SearchIndexEntryRemoved` CloudEvents (source `hexalith-folders`) for removed/archived/tombstoned units and the `folders-index` round-trip is exercised live against the Epic 9 routing
**Then** removed units leave no stale searchable entry, a syntactic/BM25 query returns exactly one hit per live indexed unit, and routing is proven live end-to-end.

### Story 10.5: Expose an authorized Folders query facade over Memories

As a developer or AI-agent consumer,
I want to search indexed Folders content through a Folders-owned authorized query facade,
So that results are security-trimmed, hydrated from Folders authority, and redacted to metadata-only before leaving API, SDK, MCP, or CLI surfaces.

**Acceptance Criteria:**

**Given** indexed content in Memories
**When** a Folders query facade serves search-index results
**Then** current tenant/folder authorization runs before candidate lookup, result counting, suggestions, filters, empty-state classification, or response shaping
**And** candidates are hydrated from current Folders authority, stale/removed/archived/unauthorized candidates are dropped, and remaining metadata-token results are C9-redacted before leaving API, SDK, CLI, or MCP
**And** backend unavailability returns an honest safe result without treating safe-empty behavior as successful search completion.

### Story 10.6: Replace the fail-closed content materializer with a metadata-derived materializer under C4/C9

As a developer or AI-agent consumer,
I want authorized folder mutations to produce real curated search-index text from metadata evidence,
So that the Memories search index is actually populated on live mutations without leaking raw content, paths, or snippets.

**Acceptance Criteria:**

**Given** the worker default `ISemanticIndexingContentMaterializer` is the fail-closed placeholder that always returns `Unavailable("content_materializer_unavailable")`
**When** a metadata-derived materializer is implemented and registered in `FoldersWorkersModule` in its place (fail-closed retained as an explicit fallback)
**Then** an authorized, policy-passing mutation yields `Available` curated text/attributes and the worker publishes a real `SearchIndexEntryChanged` into `folders-index` instead of dead-ending at materialization.

**Given** C9 classifies paths/repo/branch/commit as tenant-sensitive and forbids raw path/content in the CloudEvent `Text`/`Attributes` unless explicitly allowed
**When** the materializer builds `CuratedText`/`CuratedAttributes` from mutation metadata evidence (type/size classification, media type, folder/org identity, path-policy outcome)
**Then** the published `Text`/`Attributes` contain no raw file path, no file body, no snippet, and no source URI, asserted against a sensitive-path corpus; and C4 size/type gates (`content_too_large`/`content_type_unsupported`) plus idempotent/replay-stable CloudEvent ids remain green.

**Given** the live `aspire run` round trip remains blocked pending a DCP-capable lane
**When** Story 10.6 is assessed
**Then** real mutation → curated metadata text → publication is proven at the worker/port boundary, the deployed bridge and full round trip remain explicitly owned by Stories 10.7–10.8, and body-content materialization remains the separately authorized Story 10.9 rather than being silently included.

### Story 10.7: EventStore-backed search bridge and deployed Server registration

As an authorized search consumer,
I want the Folders search bridge populated from durable events and registered in the deployed Server,
So that search/status uses real current authority instead of the fail-safe unavailable default.

**Acceptance Criteria:**

**Given** Stories 12.1–12.3 provide durable source events and authoritative file/state hydration and Story 10.6 provides C9-safe metadata-token documents
**When** the EventStore-backed bridge projection is placed in a Server-referenceable project, registered in `AddFoldersContextSearchFacade`, restarted, and replayed from an empty checkpoint
**Then** it durably populates version/status/removal records, replaces the deployed `UnavailableSemanticIndexingBridgeReadModel` default, preserves current-authority hydration, and exposes honest freshness/availability
**And** authorization precedes candidate observation; wrong-tenant/unauthorized, stale, removed, archived, conflict/corrupt, timeout/unavailable, and replay-boundary cases are safely dropped or classified with metadata-only evidence
**And** deployed populated and restart evidence is required; NoOp, in-memory, seed, fake, unavailable, or safe-empty behavior alone cannot satisfy completion.

### Story 10.8: Real produce/index/authorize/hydrate/redact/search round trip

As a developer or AI-agent consumer,
I want a non-empty authorized metadata-token search and status round trip through the deployed topology,
So that current FR58 is proven without exposing raw paths, bodies, snippets, or source URIs.

**Acceptance Criteria:**

**Given** Stories 10.6–10.7 and 12.1–12.5 provide durable mutation events, C9-safe metadata documents, deployed bridge state, and recoverable at-least-once Memories egress
**When** a real authorized mutation is produced, indexed, queried, hydrated from current Folders authority, redacted, returned, then removed or archived and queried again on the DCP-capable deployed lane
**Then** the first search/status response is non-empty and tenant-correct, each live unit appears exactly once, and the later response prunes the stale unit without raw path, body, snippet, source URI, credential, or hidden-existence leakage
**And** authorization runs before lookup/count/filter/suggestion/empty classification, and denial, wrong-tenant, duplicate/conflict, Memories failure, timeout/unknown egress, stale candidate, removal/archive, size/result boundary, restart, and empty-checkpoint replay evidence is attached
**And** safe-empty, unavailable, NoOp, in-memory, seed, mock, or fake-only evidence cannot support FR58 completion.

### Story 10.9: Authorized body-content materialization — C9 gated

As an authorized search consumer,
I want approved file-body text materialized only under an explicit C9 policy,
So that future body-content recall can be introduced without weakening the current metadata-only authority.

**Acceptance Criteria:**

**Given** named Security and Product authorities have approved the C9 body-content scope, source classes, redaction, retention, deletion, access, and egress policy
**When** authorized current file content is materialized and published
**Then** authorization and policy precede content access, only approved bounded text crosses the egress boundary, deletion/archive prunes it, and Folders remains authoritative for current access and content
**And** wrong-tenant/unauthorized denial, binary/oversize/secret/path boundary, duplicate/conflict, provider/Memories failure, timeout/unknown, restart/replay, retention/deletion, and sensitive-data tests are attached to real deployed evidence
**And** absent approvals the feature remains unavailable and is not a blocker for current metadata-token FR58; unavailable, fake, seed, safe-empty, or in-memory behavior does not prove body-content capability.

## Technical Enabling Workstream 11: Domain-Focus Platform Refactoring And Governance Closure

Platform maintainers can remove local copies of shared Hexalith platform capabilities from Folders, consume the appropriate Commons/EventStore/FrontComposer/Memories primitives, delete the local ServiceDefaults project, and preserve all REST/SDK/CLI/MCP/UI behavior through lockstep governance and verification gates.

**FRs covered:** No new product FR scope. Supports existing PRD NFRs for tenant isolation, metadata-only audit, parity, observability, accessibility, traceability, and maintainability.

_Created 2026-07-07 via bmad-correct-course (`sprint-change-proposal-2026-07-07-081620.md`). Technical alignment epic driven by `fable_Folders_changes.md`._

### Story 11.1: Establish refactor baseline and governance pin map

As a maintainer,
I want the current build, test, package, route, and governance-pin baseline captured before refactoring,
So that every simplification can be verified against known behavior and pinned gates.

**Acceptance Criteria:**

**Given** HEAD `533806b` and the current sprint status
**When** baseline verification runs
**Then** restore/build, focused test lanes, format checks, ScaffoldContractTests, release/package inventories, route tables, workflow pins, and known DCP/AppHost blockers are recorded before edits
**And** unrelated submodule pointer changes are not reverted or hidden.

### Story 11.2: Inventory, assign, and pin platform prerequisites

As a platform maintainer,
I want each required Commons, EventStore, FrontComposer, and Memories prerequisite assigned and pinned,
So that Folders consumes released shared capabilities without claiming ownership of upstream implementation.

**Acceptance Criteria:**

**Given** the audit platform gaps G1-G9
**When** the prerequisite inventory is reconciled
**Then** every capability has an owning repository, upstream issue/story reference, required release/version or SHA, availability status, consuming Folders story, and verification evidence
**And** this story records pin evidence only; it does not implement upstream code, mutate dependency pins without separate authorization, or claim any product projection complete.

### Story 11.3: Apply wire-preserving repository hygiene

As a maintainer,
I want obsolete repository litter and stale maintenance text removed without wire changes,
So that later refactors start from a clean, reviewable baseline.

**Acceptance Criteria:**

**Given** tracked cache files, temporary diffs, root litter, and stale maintenance text exist
**When** hygiene fixes are applied
**Then** only identified hygiene artifacts are removed or corrected, authoritative package/version text remains sourced from repository pins, and tracked evidence is preserved
**And** REST/OpenAPI behavior, lifecycle status, CI/governance semantics, submodule pins, and product projections do not change; brittle gates are owned by Story 11.16.

### Story 11.4: Consolidate Server transport, envelope, and route helper duplication

As a maintainer,
I want the hand-written REST surface deduplicated without changing wire contracts,
So that route implementation remains maintainable and parity gates stay green.

**Acceptance Criteria:**

**Given** repeated `SafeProblem`, header/query readers, canonical-id validators, and result mappers exist across Server endpoint files
**When** shared Server helpers and table-driven status mapping are introduced
**Then** existing routes, response envelopes, ProblemDetails categories, status codes, and parity oracle expectations remain unchanged
**And** the OpsConsole secret-filter drift is closed by one shared detector.

### Story 11.5: Consolidate domain and provider helper duplication

As a domain maintainer,
I want duplicated domain/provider helper logic centralized before platform adoption,
So that later package-boundary moves are smaller and safer.

**Acceptance Criteria:**

**Given** repeated payload tenant mapping, authorization mapping, provider adapter code, deterministic hashing, and stream-name checks exist
**When** shared Folders-local helpers are introduced
**Then** provider behavior, failure categories, metadata-only guarantees, and tests remain equivalent
**And** provider feature/correctness work, search optimization, and reserved-tenant decisions remain with their owning product or ADR stories rather than expanding this consolidation.

### Story 11.6: Consolidate CLI/MCP adapter core and secure bearer transport

As an adapter maintainer,
I want CLI and MCP shared plumbing deduplicated and bearer handlers hardened,
So that cross-surface parity remains consistent without copied code.

**Acceptance Criteria:**

**Given** CLI and MCP repeat JSON metadata, bearer handling, sourcing, parse, and pipeline logic
**When** shared adapter-core helpers are introduced
**Then** CLI/MCP behavior remains parity-oracle equivalent
**And** bearer-token handling rejects non-HTTPS non-loopback endpoints before token emission.

### Story 11.7: Consolidate deterministic time, context, and path test helpers

As a test maintainer,
I want duplicated deterministic clocks, tenant/claim contexts, and repository-path helpers moved into the testing library,
So that later refactors change production seams once and tests stay focused.

**Acceptance Criteria:**

**Given** duplicated `FixedTimeProvider`, tenant/claim context accessors, canonical path fixtures, and repository-root walkers exist
**When** canonical helpers are added to `Hexalith.Folders.Testing`
**Then** test projects consume one deterministic helper per concern, behavior and test intent remain unchanged, and moves preserve the relevant conformance references
**And** EventStore gateway doubles are owned by Story 11.17 and provider/repository fakes by Story 11.18.

### Story 11.8: Adopt Commons/EventStore primitives in the Folders domain

As a domain maintainer,
I want the domain library to consume shared platform primitives for platform-owned behavior,
So that Folders contains only folder-specific policy, aggregates, provider ports, and projections.

**Acceptance Criteria:**

**Given** platform prerequisites from Story 11.2 are pinned
**When** Folders adopts Commons/EventStore primitives
**Then** TenantAccess, telemetry, bounded metrics, cursor codecs, read-model stores, correlation sanitization, secret detection, deterministic hashing, authorized URL validation, and secret-store access move to shared abstractions
**And** `Dapr.Client` and `Octokit` are removed from the core domain package unless an explicit, documented package-boundary exception remains.

### Story 11.9: Delete Hexalith.Folders.ServiceDefaults and consume Commons.ServiceDefaults

As a hosting maintainer,
I want Folders to use shared service defaults,
So that local hosting, health probes, telemetry registration, and deployment docs match the platform.

**Acceptance Criteria:**

**Given** `Hexalith.Folders.ServiceDefaults` duplicates shared platform behavior
**When** the project is removed
**Then** Server/UI/Workers consume `Hexalith.Commons.ServiceDefaults`, Folders-specific readiness checks are moved into the owning host or deleted, probe paths are updated in code/docs/tests/deploy manifests, and all inventory gates are updated in lockstep.

### Story 11.10: Adopt EventStore admission and subscription-mapping seams

As a platform maintainer,
I want Server and Workers to consume the platform EventStore admission and subscription-mapping seams,
So that Folders stops reimplementing domain-service request admission and event-subscription mapping.

**Acceptance Criteria:**

**Given** EventStore exposes the pinned admission and subscription-mapping seams from Story 11.2
**When** Server/Workers are refactored
**Then** authorization uses `IDomainServiceAdmissionStage` or the approved equivalent, obsolete local admission routing is deleted where safe, and `MapEventStoreDomainEvents` or its pinned equivalent replaces local subscription mapping
**And** REST parity, authorization ordering, lifecycle determinism, and worker behavior remain unchanged
**And** this story owns no transition-evidence, diagnostic, search-bridge, publication, search-client, or other product projection; those belong to Epics 4, 6, 10, 12 and Story 11.14 as declared.

### Story 11.11: Adopt FrontComposer user-context, token, OIDC, and shared-shell helpers

As a UI maintainer,
I want the operations console to reuse FrontComposer identity and shell helpers,
So that user context, token relay, OIDC, and shell composition follow the shared platform contract.

**Acceptance Criteria:**

**Given** local user-context, token-relay, OIDC, test-auth, or shell helpers duplicate FrontComposer
**When** pinned shared helpers are adopted
**Then** tenant/folder authorization context and token boundaries remain equivalent, `FrontComposerShell` remains the Blazor Interactive Server layout, and no mutation, file-content, or secret boundary is weakened
**And** Fluent tables, controls, icons, loading, and layout primitives remain owned by Story 11.19.

### Story 11.12: Modernize the generated client and shared idempotency/ULID helpers

As a client maintainer,
I want the SDK generation pipeline aligned with the ecosystem's System.Text.Json and Commons helper direction,
So that packable client dependencies and idempotency behavior are stable.

**Acceptance Criteria:**

**Given** NSwag currently generates a Newtonsoft-based client and local idempotency/ULID helpers exist
**When** the client is regenerated on System.Text.Json
**Then** idempotency hash regression vectors pass, ProblemDetails parsing remains canonical, Commons helpers replace local ULID/hash logic where available, Newtonsoft leaves the packable surface, and generated files remain build-generated rather than hand-edited.

### Story 11.13: Delete obsolete local code and synchronize planning/maintenance documents

As a maintainer,
I want obsolete local implementations deleted and affected planning/maintenance documents synchronized,
So that the adopted module boundary has one maintained source per concern.

**Acceptance Criteria:**

**Given** the applicable Workstream 11 adoption stories have landed
**When** cleanup executes
**Then** superseded local code/tests are deleted or re-pointed and affected maintenance/planning references are synchronized without rewriting completed evidence or lifecycle history
**And** ADR decisions remain owned by Story 11.20 and final boundary/gate verification by Story 11.21.

### Story 11.14: Adopt Memories publication and search-client seams

As a platform maintainer,
I want Folders to consume pinned Memories publication and search-client seams,
So that local egress/client plumbing can be removed without transferring product ownership.

**Acceptance Criteria:**

**Given** Story 11.2 records available compatible Memories seams
**When** Workers and Server adopt them
**Then** existing publication identity, retryability, redaction, tenant routing, and query-client behavior remain contract-compatible and obsolete local wrappers are removed
**And** Epic 10 retains search-bridge/hydration/product ownership and Epic 12 retains durable egress/reconciliation ownership; this story cannot claim either projection or FR58 complete.

### Story 11.15: Maintain the DCP-capable cross-repository verification lane

As a platform verifier,
I want one maintained DCP-capable lane across the root-declared platform repositories,
So that durable sidecar and cross-repository behavior can be proven against compatible pins.

**Acceptance Criteria:**

**Given** a story declares DCP/live-sidecar evidence and its owning repositories, versions, configuration, and data components are known
**When** the lane runs
**Then** it records exact pins, composition, commands, results, persisted-state assertions, restart boundaries, and sanitized diagnostics without silently falling back to mocks or unavailable seams
**And** a blocked or degraded lane is reported honestly and cannot serve as positive completion evidence for product Epics 4, 6, 10, or 12.

### Story 11.16: Replace brittle governance and CI pins with behavioral gates

As a release maintainer,
I want governance and CI checks to verify behavior rather than fragile text or fixed counts,
So that legitimate refactors do not weaken or accidentally bypass release controls.

**Acceptance Criteria:**

**Given** approval, route, package, E2E, accessibility, no-filter, forbidden-substring, and generated-inventory rules exist
**When** their conformance gates are refactored
**Then** each rejects its unsafe behavioral counterexample, consumes generated denominators where applicable, preserves fresh named approvals and blocking full-lane coverage, and does not hard-code stale story/test counts
**And** the previous governed behavior remains green or an exact regression is reported; no gate is removed, narrowed, skipped, or made fail-open.

### Story 11.17: Consolidate EventStore gateway doubles and rejection conformance

As a test maintainer,
I want one rejection-propagating acceptance-path gateway double and one clearly limited recording double,
So that tests cannot turn rejected production behavior into false acceptance evidence.

**Acceptance Criteria:**

**Given** duplicate or flattening `IEventStoreGatewayClient` doubles exist
**When** they are consolidated into `Hexalith.Folders.Testing`
**Then** the canonical acceptance double propagates `DomainServiceWireResult` rejection as the canonical gateway exception/result, the recording double is documented as request-shape evidence only, and named negative doubles remain explicit
**And** an automated allowlist/conformance guard rejects new ad-hoc gateway doubles and behavioral tests prove both accepted and rejected paths without claiming a double is deployed production evidence.

### Story 11.18: Consolidate provider and repository fakes in Folders.Testing

As a test maintainer,
I want reusable provider and repository fakes centralized with explicit evidence limits,
So that unit tests remain deterministic without being mistaken for production persistence or provider proof.

**Acceptance Criteria:**

**Given** duplicated recording providers, in-memory repositories, and provider-result fixtures exist
**When** canonical fakes are introduced
**Then** equivalent replay, conflict, known failure, timeout/unknown, cancellation, and sensitive-data assertions remain configurable and deterministic across test projects
**And** each fake is named/documented as non-production evidence, production registration guards remain intact, and no product story may satisfy its real-path acceptance floor from these fakes alone.

### Story 11.19: Adopt Fluent UI tables, controls, icons, loading, and layout primitives

As a UI maintainer,
I want the operations console to use shared Fluent UI and FrontComposer visual primitives,
So that the read-only experience is accessible and consistent below the shell.

**Acceptance Criteria:**

**Given** local tables, controls, icons, loading/copy/banner components, or undefined shell classes duplicate shared primitives
**When** they are replaced
**Then** tables use `FluentDataGrid`, interactive elements use approved Fluent components, multi-section pages use accessible composition, loading/redaction/status behavior remains equivalent, and no mutation or file-content path appears
**And** the full E2E and WCAG 2.2 AA lanes remain blocking and un-narrowed across responsive and 125/150/200-percent zoom checks.

### Story 11.20: Record AppHost, ServiceDefaults, query-handler, and tenant-semantic ADRs

As an architecture reviewer,
I want the four platform-boundary decisions recorded in focused ADRs,
So that future maintainers understand the approved exceptions and ownership choices.

**Acceptance Criteria:**

**Given** implementation evidence and named decision authorities exist
**When** the ADRs are authored or amended
**Then** AppHost/Aspire composition, ServiceDefaults deletion/adoption, query-handler conformance, and reserved-tenant semantics each record context, decision, alternatives, consequences, owner, approval, and affected contracts
**And** an ADR does not retroactively manufacture implementation evidence, change lifecycle status, or transfer product-projection ownership from Epics 4, 6, 10, or 12.

### Story 11.21: Run final boundary, package, test, E2E, accessibility, and governance verification

As a release reviewer,
I want Workstream 11 closed by one traceable verification pass,
So that the refactored platform boundary is demonstrably equivalent and honestly governed.

**Acceptance Criteria:**

**Given** the applicable Workstream 11 stories and ADRs are complete
**When** final verification runs
**Then** project/reference boundaries, package inventories, generated contracts, focused tests, full E2E, WCAG 2.2 AA, governance approvals, workflow-conformance gates, and documentation consistency are recorded with exact commands and results
**And** failures or unavailable external lanes remain explicit blockers, no lifecycle history is rewritten, and this enabling verification is not counted as product-capability completion.

## Product Epic 12: Durable Repository-Backed Round Trip

Authorized developers and AI agents can persist folder lifecycle and file content across process restart, retrieve authoritative content, complete a real Git commit, observe terminal task and projection state, and recover asynchronous indexing delivery without NoOp, unavailable, in-memory, seed-only, or fake-backed substitutions.

**FRs covered:** FR1–FR3, FR9–FR14, FR18–FR21, FR24–FR46, FR58.

**Prerequisite decisions:** OQ1–OQ4 where they govern timing, file policy, authorization, provider compatibility, and reconciliation. Product projection ownership remains with Epics 4, 6, and 10; Epic 12 owns their durable source events, authoritative state/content, task completion, Git persistence, and egress substrate.

### Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay

As an authorized repository-backed folder user,
I want folder and organization state persisted through EventStore and replayed into projections,
So that accepted lifecycle operations survive process restart and Production can boot without a NoOp repository.

**Acceptance Criteria:**

**Given** OQ1–OQ4 prerequisites affecting the durable boundary are recorded
**When** the real REST → EventStore gateway → processor → authorization gate → repository path accepts folder or organization behavior
**Then** metadata-only events append durably, `IFolderRepository` and required organization state rebuild from ordered streams, `/project` consumes events rather than returning 501, the ADR-0001 `DomainResult.NoOp()` path is retired, and Production boots with a real registration
**And** empty-checkpoint replay, host restart, append conflict/reread, equivalent/conflicting idempotency, wrong-tenant/authorization denial, corrupt/unavailable store, timeout, event-version boundary, and sensitive-data exclusion are proven
**And** NoOp, in-memory, seed-only, unavailable, safe-empty, or fake-only evidence cannot satisfy completion.

### Story 12.2: Durable projections and task-completion pipeline

As an authorized task actor,
I want lifecycle/task projections and terminal task completion persisted durably,
So that accepted work reaches trustworthy status after restart.

**Acceptance Criteria:**

**Given** Story 12.1 supplies durable ordered source events
**When** lifecycle, lock, workspace, cleanup, task, and commit-status projections consume new events or replay from an empty checkpoint
**Then** their deterministic state, checkpoints, freshness, terminal task result, retry eligibility, and failure/recovery evidence persist across restart and duplicate delivery
**And** tenant isolation, authorization denial, event duplication/order conflict, corrupt/unavailable state, timeout, empty stream/checkpoint, and retention boundaries produce safe canonical behavior with metadata-only evidence
**And** transition-evidence, seven diagnostics, and search-bridge projections remain owned by Epics 4, 6, and 10; in-memory, seed, unavailable, NoOp, safe-empty, or fake-only proof cannot complete this story.

### Story 12.3: Durable workspace file-content store and content-read source

As an authorized workspace actor,
I want staged and committed file content stored durably and read from one authoritative source,
So that mutations, context queries, and commits operate on verified content after restart.

**Acceptance Criteria:**

**Given** Story 12.1 supplies durable folder streams and current authorization/path policy passes
**When** bounded inline or streamed add/change/remove content is staged, retrieved, restarted, or replayed
**Then** content and metadata persist in the approved store, server-side hashes and byte/media metadata are verified, task/lock/version identity is enforced, deleted content is unavailable, and context reads use this authority rather than a derived index
**And** wrong-tenant/authorization denial, traversal/symlink/case boundary, binary/oversize/encoding limits, conflicting replay, corrupt/missing content, timeout/cancellation, restart, and retention/deletion evidence are attached without content in events/audit/telemetry
**And** discarded, memory-only, seed, unavailable, safe-empty, NoOp, or fake content cannot satisfy completion.

### Story 12.4: Real Git commit executor and provider write path

As an authorized lock-owning task actor,
I want staged durable changes applied and committed to the bound remote/ref,
So that repository-backed work produces a provider-confirmed durable commit.

**Acceptance Criteria:**

**Given** Stories 12.1–12.3 provide durable state/content and the selected provider binding/ref policy is current
**When** the real GitHub or Forgejo provider write path stages changes and commits
**Then** `NotImplementedException`/fake executors are replaced, exactly one eligible provider mutation occurs, provider-confirmed commit identity is persisted, the task/projections reach the correct clean terminal state, and the provisioning process manager is wired where required
**And** denial/wrong-tenant, lock/ref/path conflict, equivalent/conflicting replay, known provider failure, timeout/cancellation or unknown post-dispatch outcome, restart, and content/metadata boundary evidence prove no blind duplicate commit
**And** mocks, fake Git, NoOp, in-memory, seed, unavailable, or safe-empty evidence cannot satisfy completion.

### Story 12.5: At-least-once Memories egress and reconciler

As an authorized search/indexing consumer,
I want durable mutation/commit events delivered to Memories with recoverable at-least-once semantics,
So that indexing outages never roll back file truth and missed delivery can reconcile safely.

**Acceptance Criteria:**

**Given** Stories 12.1–12.3 provide durable source events/content-state metadata and the Epic 9 route is configured
**When** commit-then-append publication, duplicate delivery, outage, restart, or reconciliation occurs
**Then** an outbox/checkpoint or equivalent durable mechanism preserves ordered egress intent, stable CloudEvent/idempotency identity prevents duplicate logical index units, failures expose retry/reconciliation status, and committed Folders truth is never rolled back
**And** tenant routing, authorization/policy outcome, removal/archive, duplicate/conflict, Memories failure, timeout/unknown acknowledgement, poison/boundary, empty-checkpoint replay, restart, and metadata-only C9 exclusion are proven
**And** in-memory queues, fire-and-forget, seed, NoOp, unavailable, safe-empty, or fake-only evidence cannot satisfy completion.

### Story 12.6: Implement durable all-mutations idempotency and expired-key precedence

As an authorized Contract Spine caller,
I want every mutation to use durable tenant-scoped idempotency with unambiguous expiry precedence,
So that retries cannot duplicate work and an expired key can never silently execute as new intent.

**Acceptance Criteria:**

**Given** Story 12.1 supplies durable Folder/Organization state and the approved EventStore admission/retention design is available
**When** any generated Contract Spine mutation receives a new, live-equivalent, live-different, expired-equivalent, or expired-different key—or any read receives an idempotency key
**Then** exactly one eligible intent executes; live equivalent returns the same logical result, live different returns canonical conflict, every expired case returns `idempotency_key_expired` before protected work regardless of intent, and reads return `idempotency_key_not_allowed`
**And** durable consumed-key evidence survives result compaction, host restart, clock boundaries/rollback, concurrency races, and state drift without retaining or revealing protected prior intent
**And** every mutation/read cell is generated from the current C13 denominator and proves authorization-before-disclosure, tenant isolation, conflict/expiry precedence, unavailable/corrupt state, timeout/crash windows, no duplicate provider/event/file/commit/audit effects, and metadata-only leakage exclusion
**And** no in-memory, fake, seed, unavailable, NoOp, safe-empty, source-text-only, or component-only evidence can close OQ8 or this story.

## Security And Operational Hardening Epic 13

Release stakeholders can close ratified security and operational-truth defects on capabilities that already claim to work. Epic 13 is release-blocking hardening, excluded from product-completion metrics, and does not duplicate Workstream 11.

### Story 13.1: Forgejo SSRF egress guard for private and metadata IPs

As a security operator,
I want Forgejo readiness egress blocked from private, loopback, link-local, and cloud-metadata destinations,
So that tenant-controlled base URLs cannot turn the service into an SSRF proxy.

**Acceptance Criteria:**

**Given** an authorized Forgejo readiness request contains or resolves an endpoint
**When** DNS resolution and connection establishment occur through the production HTTP transport
**Then** scheme/host/port policy and `ConnectCallback`-level IP checks reject loopback, RFC1918/private, link-local, multicast, unspecified, rebinding, redirect, and provider-metadata destinations before credentials or HTTP bytes are sent
**And** allowed public endpoints, denial, DNS failure, timeout, IPv4/IPv6 and redirect boundaries, tenant isolation, safe audit, and sensitive-value exclusion are proven in deployed transport tests.

### Story 13.2: Fail-safe fallback authorization policy and sidecar-only app port

As a security operator,
I want fallback authorization to deny safely and the application port reachable only through the Dapr sidecar boundary,
So that missing policy or network bypass cannot expose Folders operations.

**Acceptance Criteria:**

**Given** route authorization metadata is absent/malformed or a caller attempts direct app-port access
**When** the deployed Server evaluates policy and network exposure
**Then** fallback policy denies, only the approved sidecar path can reach the app port, and authenticated/authorized sidecar traffic retains canonical behavior
**And** missing policy, wrong app ID, direct network, wrong tenant, timeout/sidecar failure, startup misconfiguration, and port-boundary evidence are attached with one safe denial audit and no hidden-resource leak.

### Story 13.3: Credential file 0600 permissions

As a security operator,
I want credential-bearing files created with owner-only permissions,
So that local or mounted credentials cannot be read by unrelated principals.

**Acceptance Criteria:**

**Given** CLI, MCP, development, test, or deployment tooling creates an approved credential file
**When** the file is first written, replaced, restored, or checked at startup
**Then** Unix mode is `0600` before secret content becomes observable, unsafe existing permissions fail closed or are repaired only under explicit policy, and logs/errors never expose the value
**And** positive, denied owner/group/world access, symlink/race, replacement, non-Unix behavior, timeout/I/O failure, and secret-sentinel evidence are attached.

### Story 13.4: Real readiness snapshot source and UI health endpoints

As an operator,
I want readiness and UI health to reflect real deployed dependencies,
So that orchestration never reports healthy from a seed or placeholder source.

**Acceptance Criteria:**

**Given** Server, Workers, UI, EventStore, provider, Memories, state-store, and sidecar dependencies have defined readiness semantics
**When** the production snapshot source and UI health endpoints are queried
**Then** current dependency state, freshness, safe reason, and degraded/unavailable posture are reported without secrets, and startup/readiness gates do not substitute an in-memory or constant-success source
**And** healthy, degraded, failed, timeout, stale, restart, wrong-tenant protected detail, and probe-boundary evidence are proven against deployed composition; fake-only or safe-success evidence cannot close the story.

### Story 13.5: Wire alert instruments and production Dapr state store and resiliency

As an operations engineer,
I want declared alerts and production Dapr persistence/resiliency components actually wired,
So that failures are observable and stateful services use governed retry/timeout behavior.

**Acceptance Criteria:**

**Given** five alert instruments and production `statestore`/Resiliency requirements are declared
**When** the deployed topology starts and representative success/failure conditions execute
**Then** each instrument emits bounded tenant-safe signals with documented thresholds/routing, the production state store is durable and correctly scoped, and resiliency policies apply only approved retries, timeouts, and circuit behavior
**And** alert-fire/recovery, store restart, conflict, outage, timeout, retry exhaustion, tenant isolation, configuration-boundary, and sensitive-label evidence are attached without using in-memory or fake components as production proof.

### Story 13.6: Rate limiting, timeouts, body caps, and sensitive-value filter convergence

As a security and reliability operator,
I want one enforced request-resource policy and one sensitive-value detector across hosts,
So that abusive input is bounded and redaction cannot drift between surfaces.

**Acceptance Criteria:**

**Given** REST, worker callbacks, UI backend calls, CLI/MCP transport, logs, audit, diagnostics, and errors process untrusted input or metadata
**When** production rate limits, operation timeouts, request/body caps, and the converged sensitive-value filter run
**Then** excess work is rejected with stable safe results before expensive/provider/content effects, cancellation propagates within policy, and all output channels redact the same sentinel corpus
**And** below/at/above-limit, burst/concurrency, slow-body, cancellation/timeout, wrong-tenant denial, filter false-positive/negative, encoding/normalization, and deployed-host evidence are attached without weakening C4 or canonical error semantics.
