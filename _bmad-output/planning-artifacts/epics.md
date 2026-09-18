---
stepsCompleted:
  - step-01-validate-prerequisites
inputDocuments:
  - "_bmad-output/planning-artifacts/prd.md"
  - "_bmad-output/planning-artifacts/architecture.md"
  - "_bmad-output/planning-artifacts/ux-design-specification.md"
---

# Hexalith.Folders - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Hexalith.Folders, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

#### Capability Contract Terms

- FR1: Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing.
- FR2: Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions.
- FR3: Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys.

#### Authorization and Tenant Boundary

- FR4: Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; tenant-scoped operators may validate but may not modify it.
- FR5: Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting operation-family scope is visible in effective permissions and auditable without exposing hidden principals.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope; the OQ3 authorization matrix records the evaluated operation family for every protected operation, and a scope dimension missing from a decision is a failing conformance defect.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information; the denial response for absent, cross-tenant, and hidden resources is equivalent in status, category, code, message, and detail keys apart from correlation and per-request instance identity, and isolation tests assert zero protected-resource reads before the denial through the test-harness access counters.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details; every allow and deny decision emits exactly one metadata-only audit record carrying actor, tenant, operation, operation family, result, and correlation ID.

#### Folder Lifecycle

- FR11: Authorized actors with fresh tenant authority can create a logical folder within that tenant and receive its tenant-scoped managed identity and initial lifecycle state; denial creates no folder or provider side effect and uses the safe authorization/lifecycle result.
- FR12: Authorized actors can inspect folder lifecycle and binding status with freshness and availability metadata; an unauthorized, hidden, stale, or unavailable state uses the canonical non-enumerating result rather than partial binding details.
- FR13: Tenant administrators can archive a folder only when it has no active task, no lock in state `locked`, and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.
- FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

#### Provider Readiness and Repository Binding

- FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID; every readiness failure maps to one FR44 category, and readiness never reports ready while any required capability is unsupported or unknown.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass and receive its canonical provider/repository binding plus inspectable folder/workspace state; failed readiness or authorization creates no repository or binding side effect and returns the canonical safe result.
- FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.
- FR20: Authorized tenant administrators can define or select the branch/ref policy used by repository-backed folder tasks; an accepted policy becomes part of readiness, binding, and the canonical serializing target, while invalid or unauthorized changes are rejected without changing the active binding.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets; redaction tests with sentinel secrets prove that no credential value, token, or unauthorized repository identity appears in any binding, readiness, or diagnostic response.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle; readiness and provider-support responses enumerate the C13-declared capability set per provider as supported, unsupported, or unknown, and a client never has to infer a difference from a failed operation.
- FR23: Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready.

#### Workspace and Lock Lifecycle

- FR24: Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect.
- FR25: Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata.
- FR28: Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.
- FR29: Authorized owners can release a workspace lock when no changes remain staged, presenting the task identity and lock-ownership proof; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.
- FR30: Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.
- FR31: Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable.

#### File Operations and Context Queries

- FR32: Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy; a rejected request applies no part of its change set, returns the applicable FR44 category, and leaves the workspace lifecycle and lock state unchanged.
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
- FR44: The error taxonomy is the single caller-visible enumeration of outcome families and must distinguish safe denial (the one caller-visible outcome for tenant authorization denied, absent, cross-tenant, hidden-resource, audit-scope, and unauthorized missing-binding or missing-policy cases, carried today by the Spine as category `tenant_access_denied` with code `resource_unavailable`), validation failure, authentication failure, invalid state transition, folder ACL denied, credential reference missing or invalid, provider readiness failed, provider permission insufficient, provider unavailable, provider rate limited, unsupported provider capability, repository conflict, duplicate binding, branch/ref conflict, workspace not ready, lock conflict, stale or interrupted lock, stale workspace, dirty workspace, folder archived, content unavailable, path validation failed, file operation failed, commit failed, unknown provider outcome, reconciliation required, duplicate operation, idempotency conflict, expired idempotency key, read-model unavailable, input limit exceeded, and transient infrastructure failure; the Contract Spine maps each family to one or more closed categories and codes, and that mapping is C13 evidence. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.
- FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.
- FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.

#### Cross-Surface Contract

- FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine and every C13-required REST cell passing the shared authorization, idempotency, lifecycle, error, and audit scenarios.
- FR48: CLI users can perform every C13-required CLI cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR49: MCP clients can perform every C13-required MCP cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR50: SDK consumers can perform every C13-required SDK cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior; the C13 parity oracle reports zero material deltas across REST, SDK, CLI, and MCP for every supported cell, and any delta fails the release gate.

#### Audit and Operations Visibility

- FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.
- FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.
- FR56: Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only if, before any stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor holds incident-admin permission and fresh current tenant/folder authorization. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

#### Authorized Search Facade

- FR58: Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe.

### NonFunctional Requirements

#### Security and Tenant Isolation

- NFR1: Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, asynchronous provider side effect, and audit record. No incoming webhook ingestion exists in MVP.
- NFR2: Cross-tenant access leaks are zero-tolerance defects. No object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
- NFR3: Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, asynchronous work, audit records, index results, and context-query results.
- NFR4: File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
- NFR5: Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
- NFR6: Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.
- NFR7: Every mutation and asynchronous side effect must revalidate current tenant, folder, delegated-actor, binding, and credential authority before touching a protected resource; revocation fails closed and changes any held lock to revoked/inaccessible.
- NFR8: Paths, repository names, branch names, and commit messages are tenant-sensitive by default. Authorized tenant members and tenant-scoped operators with need-to-know may view them; cross-tenant/external diagnostics redact them. A tenant confidential override replaces cleartext at event write, before persistence, with a stable tenant-scoped correlation token that preserves equality/linkage across authorized incident records but cannot reveal the original value; no cleartext confidential value is ever made durable. Redacted, hidden, unknown, missing, stale, and unavailable remain visibly distinct.
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
- NFR54: Paths, commit messages, repository names, and branch names are tenant-sensitive by default under C9; authorized tenant/scoped-operator views may display them, cross-tenant/external diagnostics redact them, and a tenant confidential override stores only the stable tenant-scoped correlation token, substituted at event write so cleartext never becomes durable. Confidential incident reconstruction links operations through that token and operation/correlation identity; it does not promise recovery of the original cleartext. Provider payloads, file bodies, secrets, and generated context remain forbidden.
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
- NFR74: Bearer credentials must be accepted only over HTTPS or an explicitly approved loopback development boundary.
- NFR75: Provider endpoints must deny private, loopback, link-local, metadata-service, and otherwise prohibited destinations unless an approved deployment policy explicitly allows them.
- NFR76: Protected endpoints and internal service boundaries must deny by default when authority is absent, stale, malformed, or unavailable.
- NFR77: Local CLI and MCP credential material must use owner-only storage and must never be emitted to logs, telemetry, diagnostics, or generated artifacts.
- NFR78: Repository and workspace content is untrusted input and must not control commands, paths, templates, or rendered active content without validation or neutralization.
- NFR79: Accepted mutations, their state transitions, and their required evidence must survive process restart.
- NFR80: Supported multi-replica deployments must converge on one authoritative state without seed-local or replica-local correctness assumptions.
- NFR81: Readiness must report actual dependency health and must not report ready from configuration or seed data alone.
- NFR82: Every release-significant metric and alert must have demonstrated emission, a named owner, and fault-path evidence.
- NFR83: Release evidence must be classified as automated, operational, approval-bound, or reference-pending, with a named owner for every non-automated item.
- NFR84: Release verification must cover edge-security behavior including safe denial, endpoint validation, credential handling, and untrusted-content boundaries.

### Additional Requirements

These come from the Architecture document and represent technical/infrastructure requirements that must be satisfied alongside FRs/NFRs.

#### Current Technical Authority Overlay (2026-09-17)

The following requirements are the current architecture-derived constraints for story design. They supersede older wording below wherever the two conflict.

- AR-CURRENT-01: The first implementation story must scaffold from the Hexalith.Tenants structure plus the Hexalith.EventStore Admin CLI/MCP/UI patterns; no generic starter, duplicated platform boilerplate, legacy `.sln`, or nested-submodule initialization is permitted.
- AR-CURRENT-02: The target OpenAPI 3.1 Contract Spine is `v2`; historical `v1` remains evidence only. Story 1.17 owns the PD10 correction and lockstep regeneration of server, generated .NET SDK, CLI, MCP, UI, previous-spine fingerprints, and C13 parity artifacts. CLI and MCP wrap the SDK, while REST remains the parallel public transport.
- AR-CURRENT-03: Product, mechanism, acceptance, and lifecycle/dependency authority remain separate: `prd.md` owns scope and requirements, `architecture.md` owns mechanisms, `epics.md` owns story acceptance, and the regenerated `planning-story-manifest.yaml` owns lifecycle state, execution rank, and prerequisite edges.
- AR-CURRENT-04: `execution_rank`, not epic or story numbering, governs order. Every unresolved prerequisite must have a strictly lower rank, accepted terminal prerequisites may remain rankless only with evidence, and manifest validation must reject cycles, equal/later-rank dependencies, and unranked nonterminal prerequisites.
- AR-CURRENT-05: The general execution hold remains active. Only the allow-listed relock lane may change authority artifacts and generate the v2 candidate until Section 9 conformance and the A8 freeze decision pass; relock work may not expose v2, publish a production release, close ordinary story lifecycle state, or generate runtime release evidence.
- AR-CURRENT-06: Stories 1.17, 4.22, 12.7, and 13.7 plus external prerequisite nodes `EXT-ES-EVENT-EVOLUTION` and `EXT-ES-RECOVERY` are mandatory architecture-to-Delivery admissions. The EventStore prerequisites must publish versioned, digest-bound event-evolution and production-recovery capabilities before their Folders dependents execute.
- AR-CURRENT-07: Hexalith.EventStore owns durable command admission, aggregate single-writer execution, event persistence, event-schema upcasting, idempotency authority, and the event-write confidentiality boundary. Production must replace NoOp/in-memory authority and the `/project` 501 path with durable repository, replay, projection, and bootable-host behavior.
- AR-CURRENT-08: Locks, fencing tokens, idempotency admission records and expired tombstones, in-flight checkpoints, reconciliation tasks, authoritative mutation state, and required evidence must survive restart and converge across supported replicas. Working copies are disposable caches and never authoritative.
- AR-CURRENT-09: Epic 12 owns durable source events, authoritative file content/state, restart replay, task completion, durable Git orchestration, and recoverable at-least-once egress. Epic 4 owns lifecycle and transition evidence; Epic 6 owns seven production diagnostic projections and deployed console/incident journeys; Epic 10 owns the search bridge and live FR58 round trip; Workstream 11 owns platform seam adoption and cross-repository verification, not product projections.
- AR-CURRENT-10: Authorization order is authentication → authority availability → fresh tenant authority → folder ACL and derived scope → EventStore validator → Dapr deny-by-default policy, and it must complete before any protected lookup, count, filter, provider call, file/content read, audit access, or search egress.
- AR-CURRENT-11: PD10 requires three pre-lookup envelopes: unauthenticated `401`, one byte-equivalent fresh-negative safe-denial `404`, and one non-disclosing retryable authority-unavailable `503`. Protected v2 responses remove caller-visible `403`, `not_found`, `cross_tenant_access_denied`, and `audit_access_denied`; raw provider/repository/ref/task locators never confer authority.
- AR-CURRENT-12: PD8 requires irreversible tenant-scoped HMAC correlation-token substitution before persistence for confidential values, alias-safe key rotation, fail-before-admission when recoverable cleartext would be required, and a distinct `withheld` disclosure state. No durable facility may contain confidential cleartext or a reversible form.
- AR-CURRENT-13: PD11 requires a total `(state, event, guard)` lifecycle matrix with explicit rejection of every unlisted guard branch. It preserves staged work across retryable commit failure and authorization loss, permits only the originating task to resume staged dirty work, returns clean dirty work to ready at the C7 stale boundary, and reserves operator discard/retry-success/mark-failed operations as post-MVP failures.
- AR-CURRENT-14: Recovery and cleanup use separate durable clocks. A recovery deadline may block restoration but never authorize deletion; destructive working-file cleanup starts only after terminal task closure with no active task, receives a fresh P7D window, is cancelled by legitimate resume, and remains subject to legal hold.
- AR-CURRENT-15: The serializing lock identity is managed tenant plus canonical provider/repository identity plus normalized target ref. Folder, workspace, and task IDs are metadata; aliases collide; lock state, lifecycle, operation state, disposition, freshness, and disclosure are independent dimensions.
- AR-CURRENT-16: Every mutation uses the EventStore-owned durable admission contract; every read rejects an idempotency key before source execution. Equivalent replay, conflicting live intent, and expired-key precedence are distinct, and minimal consumed-key tombstone evidence outlives replay-result retention without preserving protected intent.
- AR-CURRENT-17: Unknown external effects enter `unknown_provider_outcome` and allow no more than five read-only evidence checks within 15 minutes; only exhausted or conflicting evidence enters `reconciliation_required`. Neither state permits blind retry, takeover, or cleanup.
- AR-CURRENT-18: Memories is a derived, security-untrusted shared index, never Folders authority. Workers publish metadata-only index changes; Server reads through Dapr service invocation only after authorization; every candidate is hydrated and trimmed against current durable Folders state. Body-content indexing and recall remain outside MVP.
- AR-CURRENT-19: The operations console is a read-only, metadata-only Blazor Web App using Interactive Server through `FrontComposerShell` and Fluent UI Blazor. Incident evidence requires incident-admin plus fresh tenant/folder authorization before observation, and UI state/disclosure behavior must use the canonical contract rather than locally invented labels.
- AR-CURRENT-20: The supported MVP production profile is single-region Kubernetes with Dapr sidecars, two-replica service floors, external highly available PostgreSQL v2 transactional actor state, a separate durable Redis Streams-compatible broker, deny-by-default mTLS policies, real dependency readiness, and cross-region backup/restore evidence meeting the recorded RPO/RTO.
- AR-CURRENT-21: Build versions are owned by `Directory.Packages.props`, the shared Hexalith.Builds catalog, and the AppHost SDK declaration. Debug/default builds use sibling project references and Release uses NuGet packages; both modes must remain buildable, while approval-bound provider/native pins require explicit compatibility evidence rather than routine sweeping.
- AR-CURRENT-22: Every positive production capability must prove deployed composition, durable population/replay where applicable, tenant isolation, positive and denial/conflict/failure/timeout-or-unknown/boundary behavior, and honest degraded/unavailable behavior. No completion claim may rely only on NoOp, in-memory, seed, unavailable, safe-empty, or fake evidence.
- AR-CURRENT-23: Architecture is not implementation-ready. C3, C6, C9, and the OQ3 authorization matrix are superseded pending exact-digest reapproval; NFR74–NFR84 have target mechanisms but no implementation evidence; Dapr policy conformance is not merge-blocking; provider rate-limit chaos coverage is absent; and the execution manifest still requires regeneration.

#### Solution Scaffolding (Phase 0 — sibling-module starter pattern)

- AR-SCAFFOLD-01: No third-party `dotnet new` template fits; scaffold by mirroring the Hexalith.Tenants structure and Hexalith.EventStore Admin CLI/MCP/UI conventions. Use the `.slnx` solution and the architecture's recommended layout, but treat `Hexalith.Folders.slnx` as the authoritative project inventory rather than hard-coding project counts or package versions in planning artifacts.
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
- AR-AUTHZ-04: Implement the approved C7 four-value profile: 30-second lock renewal, 15-second authorization revalidation, 60-second revocation-effect SLO, and 60-second expired-to-stale threshold. Tenant overrides may only tighten these values.
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

- AR-UI-01: Build `Hexalith.Folders.UI` as a Blazor Web App using Interactive Server through `FrontComposerShell`, consuming `Hexalith.Folders.Client`; it reads only from projection endpoints and never accesses EventStore aggregates directly.
- AR-UI-02: Use Microsoft Fluent UI Blazor (`Microsoft.FluentUI.AspNetCore.Components`) component library (F-3) to satisfy WCAG 2.2 AA targets.
- AR-UI-03: Operator-disposition labels are the primary visual (F-4): `auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`. Technical state names appear as secondary metadata. `DispositionLabelMapper.cs` sourced from C6 matrix.
- AR-UI-04: Redacted fields render with a visible lock-icon affordance (F-5) — "your tenant policy hides this; contact your administrator". Never silent truncation.
- AR-UI-05: Incident-mode last-resort read path at `/_admin/incident-stream` (F-6) — ACL-checked event-stream view available when projections are degraded; surfaces latest events for operators with `eventstore:permission=admin`. Three UX guardrails: (1) persistent red banner ("DEGRADED MODE — last projection checkpoint: HH:MM:SS UTC"); (2) operator-disposition labels rendered alongside raw event types; (3) one-click "copy correlationId + timestamp window" affordance.
- AR-UI-06: Operations console performance budget (F-7): p95 page-load < 1.5s primary diagnostic flows; p99 < 3s; degraded-mode flows up to 5s p95. Perceived-wait UX: visible skeleton state at 400ms; "still loading… [cancel]" affordance at 2s.
- AR-UI-07: No mutation paths, credential reveal, file-content browsing, file-editing UI, raw diff display, hidden repair actions, or unrestricted filesystem browsing in the MVP read-only console.

#### Infrastructure & Deployment

- AR-INFRA-01: `Hexalith.Folders.AppHost` composes the platform through shared Aspire helpers: the Folders-owned EventStore host (`AppId=eventstore`), Tenants (`AppId=tenants`), Memories search-index server (`AppId=memories`), Folders.Server (`AppId=folders`), Folders.Workers (`AppId=folders-workers`), Folders.UI (`AppId=folders-ui`), and Keycloak. Aspire and integration versions are owned by the build; the AppHost SDK and `Aspire.Hosting` compatibility pair must be validated together.
- AR-INFRA-02: Dapr components: shared `statestore` (Redis 7.x via Aspire), `pubsub` (Redis Streams), `resiliency` policies, `accesscontrol.yaml` (local: defaultAction allow), plus Memories components `memories-secretstore` + `memories-llm` (Epic 9). Production: deny-by-default + mTLS, app IDs restricted (`folders` may invoke `eventstore` and `tenants`; `folders`/`folders-workers` may invoke `memories`; not `system` admin; pubsub topics declared).
- AR-INFRA-03: Dapr policy conformance must become a merge-blocking CI job that runs `daprd` in a kind cluster with production policy YAML and property-based negative tests for unauthorized `(sourceAppId, targetAppId, operation)` triples. The current static-fixture workflow is schedule/manual-only and is not completion evidence for this target.
- AR-INFRA-04: Containerized production hosting uses one image per deployable service (`hexalith-folders-eventstore`, `hexalith-folders-server`, `hexalith-folders-workers`, `hexalith-folders-ui`) with Dapr sidecars in the supported single-region Kubernetes profile.
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
- UX-DR10: Implement a Redaction And Inaccessibility State component that distinguishes redacted, withheld, inaccessible, denied, unknown, missing, unavailable, stale, and failed data.
- UX-DR11: Preserve the MVP read-only boundary in every UI flow: no mutation controls, repair actions, file editing, raw diff display, credential reveal, unrestricted file browsing, or unauthorized resource confirmation.
- UX-DR12: Present folder metadata only as orientation and evidence; never make the console feel like a file manager or content browser.
- UX-DR13: Present six independent dimensions consistently across search results, trust summaries, tables, timelines, detail panels, empty states, denied states, and redaction states: workspace lifecycle, lock state, operator disposition, folder lifecycle, projection freshness/availability, and visibility/redaction. Workspace lifecycle uses exactly `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`; lock state uses exactly `unlocked`, `locked`, `expired`, `stale`, and `revoked`; disposition uses exactly `available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, and `terminal-until-intervention`.
- UX-DR14: Every status indicator must include readable text, icon or shape cue, semantic color, accessible label, and optional tooltip or detail link when meaning is not obvious; color must never be the only signal.
- UX-DR15: Visually and semantically distinguish every lifecycle, lock, disposition, freshness, visibility/redaction, and folder-lifecycle value. Show `unknown_provider_outcome` as automatic reconciliation in progress with safe reason, last check, remaining check/time budget, and next check; reserve `awaiting-human` for `reconciliation_required` and expose no retry or takeover control.
- UX-DR16: Use restrained Fluent UI-based visual foundations: neutral surfaces, high-contrast text, semantic status colors, compact typography, and an 8px spacing base suitable for dense operational work.
- UX-DR17: Use cards only for distinct repeated items, summary blocks, and focused panels; avoid nested cards and decorative section cards.
- UX-DR18: Structure workspace detail pages with predictable sections for overview, folder metadata, diagnosis, audit trail, provider readiness, lock/task history, access evidence, durability evidence, and indexing status.
- UX-DR19: Make current diagnosis and historical audit evidence connected from the workspace page rather than forcing users into disconnected pages for related evidence.
- UX-DR20: Provide safe empty states that distinguish no matches, insufficient filter scope, unavailable read model, and denied access without leaking unauthorized resource existence.
- UX-DR21: Provide denied states with safe reason category, allowed correlation ID evidence, and escalation posture without confirming unauthorized resource existence beyond policy.
- UX-DR22: Provide redacted states that are visibly different from withheld, missing, unknown, unavailable, failed, and denied data; redaction must not be silently hidden or represented as truncation.
- UX-DR23: Limit forms to search, filtering, sorting, and view preferences; forms must not submit domain mutations.
- UX-DR24: Use dialogs only for read-only detail expansion, safe identifier copy confirmation, filter configuration, and explanatory evidence; dialogs must trap focus, restore focus on close, and have accessible titles.
- UX-DR25: Preserve layout stability during loading states and label what is loading: search results, workspace summary, folder metadata, provider readiness, audit timeline, or access evidence.
- UX-DR26: Show stale or delayed data with freshness timestamps and read-model status; do not present stale evidence as current without labeling it.
- UX-DR27: Display safe identifiers such as task ID, operation ID, correlation ID, commit reference, credential reference identifier, and confidential-override correlation reference in monospace with safe copy affordances only.
- UX-DR28: Support desktop-first layouts with persistent navigation, global search, trust summaries, multi-column evidence panels, metadata tables, and side-by-side diagnosis or audit sections.
- UX-DR29: Provide tablet and mobile fallback layouts that stack evidence panels, collapse persistent navigation, preserve search and filters, prioritize tenant/workspace/state/risk signal, and do not break core lookup or high-level trust review.
- UX-DR30: Target WCAG 2.2 AA with keyboard access for search, filters, result selection, tabs, tables, tree expansion, detail panels, and dialogs; visible focus; semantic headings and landmarks; accessible names; sufficient contrast; zoom resilience; and screen-reader meaningful redaction/denial/status labels.
- UX-DR31: Test the UI at desktop, tablet, and mobile fallback widths, at 125%, 150%, and 200% browser zoom, and with dense identifiers and long paths in tables, timelines, metadata trees, and trust summaries.
- UX-DR32: Validate accessibility with automated checks, keyboard-only walkthroughs for the three critical journeys, screen reader review, forced-colors/high-contrast checks where supported, color-blindness review, and focus management checks.
- UX-DR33: Present a tenant-confidential override as a stored correlation reference only. The console must never display, reconstruct, or promise recovery of the cleartext value, and must not imply that any actor, including a tenant administrator, can reveal it.
- UX-DR34: Distinguish five field-disclosure outcomes with distinct text, accessible label, and non-color cue: visible, redacted, withheld, unknown, and missing. Architecture S-6 calls the fifth outcome `absent`; it is the same outcome as the shipped `Missing` member, and `Missing` is the normative render token here so no surface invents a sixth term. Withheld must be distinguishable from redacted because redaction is a policy decision that an authorized actor could reverse, whereas a withheld value has no durable cleartext to reveal. Availability of the read model is a separate axis and must not be collapsed into any disclosure outcome.
- UX-DR35: Present `unknown_provider_outcome` as automatically recovering for as long as bounded provider confirmation is still running, showing that confirmation is in progress and what its bound is. Use the canonical disposition `auto-recovering`; `auto-reconciling` is not a member of the disposition vocabulary and must not be rendered. Escalate the presentation to `reconciliation_required`, and to the `awaiting-human` disposition, only once those checks have ended without establishing the outcome.
- UX-DR36: Present protected-operation denial and authority unavailability as two distinct non-disclosing outcomes, matching the two envelopes in architecture S-7. A denial is terminal for the caller and must not invite a retry; an authority outage is transient and must invite one. Neither may reveal protected-resource existence, tenant relationship, or authorization reasoning, and both must expose an operator-safe correlation reference.
- UX-DR37: Provide a durability evidence section on workspace detail that distinguishes provider-confirmed durable state from locally staged or unconfirmed state, so an operator never reads staged work as persisted.
- UX-DR38: Provide an indexing status section on workspace detail that fails safe: when the read model is unavailable it must state that indexing status is unavailable rather than implying an empty or complete index, and it must never surface indexed body content, raw paths, snippets, or source URIs.
- UX-DR39: Provide a hardening evidence section on the provider view carrying the release-hardening signals owned by Epic 13, presented as metadata-only evidence with no configuration, repair, or credential-reveal affordance.

### FR Coverage Map

- FR1–FR3: Epic 1 — canonical vocabulary, lifecycle, and operation classification.
- FR4: Epics 2 and 3 — folder governance plus provider configuration.
- FR5–FR6: Epic 2 — grants, revocation, and effective permissions.
- FR7: Epic 3 — tenant provider-readiness inspection.
- FR8–FR10: Epic 2 — scoped authorization, safe denial, and audit evidence.
- FR11: Epics 2 and 12 — logical creation with durable identity.
- FR12: Epics 2 and 6 — lifecycle/binding inspection and presentation.
- FR13–FR14: Epic 2, with retention evidence in Workstream 7.
- FR15–FR23: Epic 3, with durable provider execution from Epic 12 where required.
- FR24: Epics 4 and 12 — validated preparation on durable state.
- FR25–FR28: Epic 4 — canonical lock identity, conflicts, and inspection.
- FR29: Epics 4 and 12 — safe release and durable idempotency.
- FR30: Epic 4, with retention validation in Workstream 7.
- FR31: Epics 4, 6, and 10 — lifecycle, diagnostic, and index status.
- FR32: Epics 4 and 12 — atomic mutations over authoritative content.
- FR33–FR35: Epic 4 — file policy and bounded context queries.
- FR36: Epic 6 — read-only console boundary.
- FR37: Epics 4 and 12 — provider-confirmed durable commits.
- FR38: Epic 4 — bounded, classified metadata.
- FR39: Epics 4, 6, and 12 — durable metadata-only evidence.
- FR40: Epic 4 — stable operation and reconciliation status.
- FR41–FR42: Epics 12 and 5 — durable idempotency plus surface conformance.
- FR43: Epics 1, 4, and 5 — canonical error contract and behavior.
- FR44: Epics 1, 4, 5, and 12 — complete outcome taxonomy.
- FR45: Epics 4 and 6 — lifecycle, lock, and disposition presentation.
- FR46: Epics 4, 6, and 10 — populated failure/status evidence.
- FR47: Epics 1 and 5 — REST contract and parity.
- FR48–FR49: Epic 5 — CLI and MCP parity.
- FR50–FR51: Epics 1 and 5 — generated SDK and cross-surface equivalence.
- FR52–FR56: Epic 6 — operational, audit, and incident evidence.
- FR57: Epics 3 and 6 — provider evidence production and presentation.
- FR58: Epic 10, enabled by Epic 9 topology and Epic 12 durability.

## Epic List

### Enabling Workstream 1: Canonical Contract and Adapter Foundation

API consumers and adapter implementers can rely on one versioned OpenAPI v2 Contract Spine driving REST, SDK, CLI, MCP, schemas, errors, and parity evidence.

**FRs covered:** FR1–FR3, FR43, FR47, FR50, FR51

**Classification:** Technical enabler; excluded from product-capability completion metrics.

### Product Epic 2: Tenant-Scoped Folder Access and Lifecycle

Tenant administrators and authorized actors can create folders, manage access, inspect permissions, archive folders, and receive safe authorization evidence without cross-tenant leakage.

**FRs covered:** FR4–FR6, FR8–FR14

**Classification:** Product.

### Product Epic 3: Provider Readiness and Repository Binding

Tenant administrators can configure Git providers and repository policy; authorized actors can create or bind repositories; operators can validate readiness without gaining mutation authority or exposing secrets.

**FRs covered:** FR4, FR7, FR15–FR23, FR57

**Classification:** Product.

### Product Epic 4: Repository-Backed Workspace Task Lifecycle

Developers and AI agents can prepare, lock, modify, query, commit, and diagnose repository-backed workspaces with deterministic lifecycle, policy, reconciliation, and failure behavior.

**FRs covered:** FR24–FR35, FR37–FR46, FR55

**Classification:** Product.

**Dependency:** Positive durable completion consumes Epic 12 substrate, while Epic 4 retains lifecycle and transition-evidence ownership. Execution rank, not epic number, orders delivery.

### Product Epic 5: Cross-Surface Workflow Parity

REST, SDK, CLI, and MCP users can perform the same lifecycle with equivalent authorization, idempotency, errors, status, audit, and mixed-surface handoff.

**FRs covered:** FR41–FR44, FR47–FR51

**Classification:** Product.

### Product Epic 6: Read-Only Workspace Trust Console and Audit Review

Operators, tenant administrators, and reviewers can find workspaces, prove tenant scope, and inspect readiness, lifecycle, locks, failures, durability, indexing, and audit evidence through a safe read-only console.

**FRs covered:** FR12, FR28, FR31, FR36, FR39, FR45–FR46, FR52–FR57

**UX requirements covered:** UX-DR1–UX-DR39

**Classification:** Product.

**Dependency:** Populated production views consume Epic 12 durable source evidence; Epic 6 retains ownership of diagnostic projections and deployed operator/incident journeys.

### Release Readiness Workstream 7: MVP Operational and Quality Evidence

Release stakeholders can verify security, isolation, provider compatibility, parity, retention, observability, performance, accessibility, documentation, and all 84 NFRs before acceptance.

**FRs covered:** Cross-cutting validation; no new product FR scope.

**Classification:** Release governance and quality closure; excluded from product-capability completion metrics.

### Release Remediation Workstream 8: MVP Acceptance Closure

Release stakeholders can close bounded conformance and evidence defects without misrepresenting partial control-plane work as product-MVP completion.

**FRs covered:** No new product FR scope; closes evidence gaps against existing FRs.

**Classification:** Release remediation; excluded from product-capability completion metrics.

### Enabling Workstream 9: AppHost and Search-Index Topology

Platform engineers can run the supported Folders topology with EventStore, Tenants, Memories, workers, UI, Dapr, and configured `hexalith-folders → folders-index` routing.

**FRs covered:** FR58 topology enablement; no independent product scope.

**Classification:** Technical enabler; excluded from product-capability completion metrics.

### Product Epic 10: Authorized Metadata Search and Index Lifecycle

Developers and AI agents can publish, remove, reconcile, query, and hydrate authorized metadata-token results while Folders remains authoritative and body-content indexing stays outside MVP.

**FRs covered:** FR31, FR46, FR58

**Classification:** Product.

**Dependency:** Epic 9 supplies topology enablement and Epic 12 supplies durable source state and recoverable egress; Epic 10 owns the deployed bridge, pruning, authorization, hydration, and non-empty FR58 round trip.

### Technical Workstream 11: Domain-Focus Platform Alignment

Maintainers can replace local platform duplication with supported Hexalith seams while preserving behavior and keeping product projections with their owning epics.

**FRs covered:** No new product FR scope; preserves existing FR/NFR conformance.

**Classification:** Technical enabler and refactoring; excluded from product-capability completion metrics.

### Product Epic 12: Durable Repository-Backed Round Trip

Authorized developers and agents can persist lifecycle and content across restart, retrieve authoritative state, complete a real Git commit, observe terminal state, and recover asynchronous delivery across replicas.

**FRs covered:** Durable substrate for FR2, FR11, FR18, FR24, FR29, FR32, FR37, FR39–FR46, FR58

**Classification:** Product.

**Boundary:** Owns durable source events, authoritative content/state, restart replay, task completion, real Git persistence, durable mutation idempotency, and recoverable egress—not the consuming Epic 4, 6, or 10 projections.

### Hardening Epic 13: Security and Operational Hardening

Security and operations stakeholders can prove SSRF defenses, fail-safe authorization, credential protection, truthful readiness, production durability, bounded requests, telemetry, backup, and recovery on the supported deployment profile.

**FRs covered:** No new product FR scope; release-blocking coverage for NFR74–NFR84 and related NFRs.

**Classification:** Security and operations hardening; release-blocking but excluded from product-capability completion metrics.

### Governing Portfolio Dependencies

Stable epic numbers are historical identities, not execution order. The regenerated manifest's strict `execution_rank` and prerequisite graph govern delivery. The general execution hold remains in force until the relock lane, required exact-digest approvals, Section 9 conformance, and A8 freeze decision complete. Epic 12 supplies durable substrate to Epics 4, 6, and 10; Epic 9 supplies search topology to Epic 10; Workstream 11 supplies platform seams without taking product-projection ownership; Workstreams 7 and 8 close release evidence only after their prerequisite product evidence exists.

<!-- Repeat for each epic in epics_list (N = 1, 2, 3...) -->

## Epic {{N}}: {{epic_title_N}}

{{epic_goal_N}}

<!-- Repeat for each story (M = 1, 2, 3...) within epic N -->

### Story {{N}}.{{M}}: {{story_title_N_M}}

As a {{user_type}},
I want {{capability}},
So that {{value_benefit}}.

**Acceptance Criteria:**

<!-- for each AC on this story -->

**Given** {{precondition}}
**When** {{action}}
**Then** {{expected_outcome}}
**And** {{additional_criteria}}

<!-- End story repeat -->
