---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/epics.md
  - _bmad-output/planning-artifacts/ux-design-specification.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-08-04
**Project:** folders

## Document Inventory

### PRD Files Found

**Whole Documents:**

- `prd.md` (123,784 bytes, modified 2026-07-19)
- `prd-validation-report.md` (57,462 bytes, modified 2026-06-26; excluded as a supporting validation artifact)

**Sharded Documents:** None.

### Architecture Files Found

**Whole Documents:**

- `architecture.md` (206,700 bytes, modified 2026-07-20)
- `reconcile-architecture.md` (7,060 bytes, modified 2026-07-14; excluded as a supporting reconciliation artifact)
- `reconcile-architecture-downstream-2026-07-19.md` (4,855 bytes, modified 2026-07-19; excluded as a supporting reconciliation artifact)

**Sharded Documents:** None.

### Epics and Stories Files Found

**Whole Documents:**

- `epics.md` (170,016 bytes, modified 2026-07-07)

**Sharded Documents:** None.

### UX Design Files Found

**Whole Documents:**

- `ux-design-specification.md` (55,660 bytes, modified 2026-07-07)

**Sharded Documents:** None.

### Discovery Resolution

No required document category is missing, and no whole-versus-sharded duplicate exists. The canonical assessment inputs confirmed by the user are `prd.md`, `architecture.md`, `epics.md`, and `ux-design-specification.md`.

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

FR11: Authorized actors with fresh tenant authority can create a logical folder within that tenant and receive its tenant-scoped managed identity and initial lifecycle state; denial creates no folder or provider side effect and uses the safe authorization/lifecycle result.

FR12: Authorized actors can inspect folder lifecycle and binding status with freshness and availability metadata; an unauthorized, hidden, stale, or unavailable state uses the canonical non-enumerating result rather than partial binding details.

FR13: Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.

FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.

FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.

FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.

FR18: Authorized actors can create a repository-backed folder when readiness checks pass and receive its canonical provider/repository binding plus inspectable folder/workspace state; failed readiness or authorization creates no repository or binding side effect and returns the canonical safe result.

FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.

FR20: Authorized tenant administrators can define or select the branch/ref policy used by repository-backed folder tasks; an accepted policy becomes part of readiness, binding, and the canonical serializing target, while invalid or unauthorized changes are rejected without changing the active binding.

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

FR35: Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range and file content are never silently truncated, and a request whose excess cannot be handled by supported truncation returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.

FR36: The operations console must remain read-only and excluded from file editing or file-content browsing capabilities.

FR37: Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting automatic evidence moves it to `reconciliation_required`.

FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission.

FR39: The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification.

FR40: The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation.

FR41: Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new intent.

FR42: While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

FR43: Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility.

FR44: The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.

FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.

FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.

FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine and every C13-required REST cell passing the shared authorization, idempotency, lifecycle, error, and audit scenarios.

FR48: CLI users can perform every C13-required CLI cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.

FR49: MCP clients can perform every C13-required MCP cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.

FR50: SDK consumers can perform every C13-required SDK cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.

FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.

FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.

FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.

FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.

FR56: Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only if, before any stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor holds incident-admin permission and fresh current tenant/folder authorization. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.

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

NFR16: When an external effect is unconfirmed, the workspace immediately enters `unknown_provider_outcome` and permits only bounded automatic read-only checks; exhausted or conflicting evidence moves the workspace to `reconciliation_required`, blocks retry, mutation, and takeover, and requires human escalation. These states never collapse into a generic failure.

NFR17: Idempotency keys are required for every mutating Contract Spine operation; non-mutating operations reject them.

NFR18: While the idempotency record is unexpired within its declared retention tier, a repeated call with the same key and equivalent payload must return the same logical result, and the same key with a conflicting payload must return an idempotency conflict. After expiry, either form of key reuse returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new request.

NFR19: Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.

NFR20: Lock acquisition is deterministic and limited to one active writer per managed tenant plus canonical provider/repository identity plus normalized target ref; aliases resolving to that identity collide.

NFR21: Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.

NFR22: Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.

NFR23: A successful committed state requires provider-confirmed durable update of the bound remote/ref. A timeout or unconfirmed remote result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks move it to `reconciliation_required`, and neither state permits blind retry.

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

NFR52: Every successful, denied, failed, retried, or duplicate operation—including lock, file, commit, provider-readiness, and status-transition operations—must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.

NFR53: Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.

NFR54: Paths, commit messages, repository names, and branch names are tenant-sensitive by default under C9; authorized tenant/scoped-operator views may display them, cross-tenant/external diagnostics redact them, and a tenant confidential override stores only the stable tenant-scoped correlation token at audit/projection write time. Confidential incident reconstruction links operations through that token and operation/correlation identity; it does not promise recovery of the original cleartext. Provider payloads, file bodies, secrets, and generated context remain forbidden.

NFR55: Operations-console views are projection-first, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata. During projection degradation, the bounded incident view may expose redacted event evidence only to an actor with incident-admin permission and normal tenant/folder access. The view must include a persistent warning, last checkpoint, correlation ID, and time window.

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

- **Authority and contract precedence:** The PRD governs product intent, actors, scope, safety invariants, and user-visible outcomes. The OpenAPI 3.1 Contract Spine governs operation names, wire schemas, and closed error fields. Conflicts block release until both artifacts are reconciled and re-approved; generated surfaces and tests cannot override either authority.
- **MVP scope:** The release must prove the complete repository-backed lifecycle for GitHub and Forgejo through REST, generated SDK, CLI, and MCP, plus a tenant-scoped read-only diagnostic console. Local-only workspaces, repair automation, unmanaged-folder migration, simultaneous multi-agent writes, first-class rename, archived-folder restoration/deletion, provider-history rewriting, incoming provider webhooks, console content browsing/editing, and cross-workspace body-content indexing are excluded.
- **Architectural ownership:** Hexalith.Tenants owns tenant identity/lifecycle/membership; Hexalith.EventStore owns shared command, aggregate, event, projection, query, cursor, read-model, and domain-service mechanics; Folders owns folder policy, ACLs, provider-binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections. File content and temporary working-copy material remain outside EventStore.
- **Provider boundaries:** Provider ports must cover readiness, repository creation/binding, workspace preparation, governed mutation application, durable commit, status, and cleanup/expiration. GitHub and Forgejo compatibility must be capability-tested and published; unknown compatibility cannot report ready.
- **State and concurrency:** Workspace lifecycle and lock state are separate closed vocabularies. One canonical tenant/provider/repository/ref identity permits one active mutation writer. Each task produces at most one provider-confirmed durable commit. Unknown external effects permit bounded read-only evidence checks only; exhausted or conflicting evidence becomes reconciliation-required, with blind retry and takeover prohibited.
- **Idempotency:** Every mutation requires an idempotency key and canonical intent comparison; reads reject keys. Unexpired equivalent replays return one logical result, conflicts return the canonical conflict, and expired keys always return `idempotency_key_expired` without execution. Mutation records use 24 hours except commit records, which use the C3 audit-retention tier.
- **Search separation:** FR34–FR35 define bounded live-workspace text/body context search for a currently authorized workspace. FR58 defines metadata-token recall only, with current-authority hydration and no raw paths, bodies, snippets, source URIs, or hidden existence. The two families cannot substitute for one another.
- **Quantified exit criteria:** C1/C5 require 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent tasks per tenant, and at least 1 lifecycle operation per second. C2 requires 500 ms commit-to-status visibility. C3 defines 7-year audit/commit-idempotency retention, 400-day operational metadata retention, 400-day-or-rebuilt read models, seven-day terminal workspace cleanup, and tenant-lifetime-plus-400-day folder/tombstone retention. C4 fixes context bounds. C6 fixes lifecycle/lock mapping. C9 fixes sensitive-metadata handling. C13 uses the generated Contract Spine parity inventory as denominator. C7 timing remains pending.
- **Quality and documentation gates:** Release requires complete C13 rows, an approved authorization matrix, 100% provider contract pass rates, negative tenant-isolation evidence, all-mutation idempotency evidence, read-key rejection, deny-by-default internal boundaries, path-security tests, deterministic/idempotent projections, replay compatibility, golden schemas, provider-failure tests, context-query security tests, and sentinel redaction tests. Required documentation includes OpenAPI, getting started, auth/ACL, lifecycle/lock and command-flow diagrams, CLI/MCP/SDK references, provider guidance, console/audit guidance, and a cross-surface error catalog.
- **Open release item OQ1:** Approve C7 lock-renewal, authorization-revalidation, and revocation-effect timing evidence.
- **Open release item OQ2:** Publish and approve the canonical file-policy vocabulary and exact safe allow/reject behavior.
- **Open release item OQ3:** Publish and approve the actor/access-state by protected-operation authorization matrix.
- **Open release item OQ4:** Publish and prove the GitHub/Forgejo compatibility catalog and reconciliation-check policy.
- **Open release item OQ5:** Replace the fail-safe but empty FR58 facade with authorized non-empty metadata-token and indexing-status evidence.
- **Open release item OQ6:** Replace seed-only console/read-model diagnostics with projection-backed positive, degraded, and replay evidence.
- **Open release item OQ7:** Align architecture, contracts, transitions, and tests to the canonical tenant/provider/repository/ref lock identity and alias collisions.
- **Open release item OQ8:** Align all contract and retention evidence to all-mutation idempotency, read-key rejection, expired-key precedence, and consumed-key recognition.
- **Open release item OQ9:** Prove incident access requires both incident-admin permission and current tenant/folder authorization, including C9 redaction and denial audit.
- **Open release item OQ10:** Publish and approve the frozen release-calibration plan for SM1–SM8 and CM1–CM4.

### PRD Completeness Assessment

The PRD is unusually detailed and structurally complete as a product contract: it contains 58 explicitly numbered FRs, 73 discrete NFR statements across nine categories, quantified success and capacity targets, closed state and error vocabularies, explicit authority rules, provider and surface boundaries, test/quality gates, retention rules, and named evidence owners. The NFR identifiers in this report are normalization labels assigned in source order because the PRD bullets themselves are not numbered.

It is not complete as an implementation-ready release contract. Its own frontmatter and delivery posture state `implementationReadiness: not-ready`, and OQ1–OQ10 remain explicit release blockers. The most material unresolved specifications are C7 timing, the canonical file-policy contract, the authorization matrix, provider compatibility evidence, non-empty FR58 behavior, projection-backed console behavior, canonical lock-identity alignment, comprehensive idempotency alignment, incident-access proof, and the release-calibration plan. These are clear rather than hidden gaps, but implementation and release acceptance cannot be considered ready until their governed evidence and approvals exist.

## Epic Coverage Validation

### Epic FR Coverage Extracted

The epics document contains one explicit coverage-map entry for each PRD identifier from FR1 through FR58. Coverage is claimed across Epics 1–6 and Epic 10, with Workstream 7 and Epics 8–9/11 providing cross-cutting release, platform, and refactoring support rather than new FR scope.

### Coverage Matrix

| FR Number | PRD Requirement | Epic Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing. | Epic 1 — vocabulary in OpenAPI Contract Spine + `docs/contract-terms.md` | ✓ Covered |
| FR2 | Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions. | Epic 1 — lifecycle vocabulary via `x-hexalith-lifecycle-states` extension + diagrams | ✓ Covered |
| FR3 | Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys. | Epic 1 — command/query distinction in OpenAPI operation grouping + Server endpoint routing | ✓ Covered |
| FR4 | Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it. | Epic 2 — tenant administrator ACL configuration via `OrganizationAggregate` ACL baseline | ✓ Covered |
| FR5 | Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting verb scope is visible in effective permissions and auditable without exposing hidden principals. | Epic 2 — folder access grant to users, groups, roles, and delegated service agents | ✓ Covered |
| FR6 | Authorized actors can inspect effective permissions for a folder or task context. | Epic 2 — effective-permissions inspection | ✓ Covered |
| FR7 | Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks. | Epic 3 — tenant readiness inspection (depends on provider configuration) | ✓ Covered |
| FR8 | The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope. | Epic 2 — layered authorization evaluation (foundation: JWT → claim transform → tenant projection → folder ACL → EventStore validators → Dapr policy) | ✓ Covered |
| FR9 | The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information. | Epic 2 — cross-tenant denial before any file/workspace/credential/repository/lock/commit/provider/audit access | ✓ Covered |
| FR10 | The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details. | Epic 2 — authorization evidence (allowed and denied) without unauthorized resource enumeration | ✓ Covered |
| FR11 | Authorized actors with fresh tenant authority can create a logical folder within that tenant and receive its tenant-scoped managed identity and initial lifecycle state; denial creates no folder or provider side effect and uses the safe authorization/lifecycle result. | Epic 2 — folder creation | ✓ Covered |
| FR12 | Authorized actors can inspect folder lifecycle and binding status with freshness and availability metadata; an unauthorized, hidden, stale, or unavailable state uses the canonical non-enumerating result rather than partial binding details. | Epic 2 — folder lifecycle and binding inspection | ✓ Covered |
| FR13 | Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive. | Epic 2 — folder archive | ✓ Covered |
| FR14 | Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden. | Epic 2 — audit and status evidence preservation for archived folders | ✓ Covered |
| FR15 | Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness. | Epic 3 — provider binding + credential reference configuration per tenant | ✓ Covered |
| FR16 | Authorized actors can validate provider readiness before repository-backed folder creation or binding. | Epic 3 — provider readiness validation before repository-backed creation/binding | ✓ Covered |
| FR17 | The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID. | Epic 3 — readiness diagnostics with safe reason codes, retryability, remediation category, provider reference, correlation ID | ✓ Covered |
| FR18 | Authorized actors can create a repository-backed folder when readiness checks pass and receive its canonical provider/repository binding plus inspectable folder/workspace state; failed readiness or authorization creates no repository or binding side effect and returns the canonical safe result. | Epic 3 — repository-backed folder creation when readiness passes | ✓ Covered |
| FR19 | Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence. | Epic 3 — folder binding to existing repository | ✓ Covered |
| FR20 | Authorized tenant administrators can define or select the branch/ref policy used by repository-backed folder tasks; an accepted policy becomes part of readiness, binding, and the canonical serializing target, while invalid or unauthorized changes are rejected without changing the active binding. | Epic 3 — branch/ref policy selection | ✓ Covered |
| FR21 | The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets. | Epic 3 — provider/credential-reference/binding/branch/capability metadata exposure (no secrets) | ✓ Covered |
| FR22 | The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle. | Epic 3 — GitHub vs Forgejo capability differences exposed explicitly | ✓ Covered |
| FR23 | Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready. | Epic 3 — per-provider readiness evidence for canonical lifecycle (readiness, repo binding, branch/ref, file ops, commit, status, failure behavior) | ✓ Covered |
| FR24 | Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect. | Epic 4 — workspace preparation | ✓ Covered |
| FR25 | Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide. | Epic 4 — task-scoped workspace lock acquisition | ✓ Covered |
| FR26 | Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata. | Epic 4 — lock state, owner, task, age, expiry, retry-eligibility metadata inspection | ✓ Covered |
| FR27 | Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata. | Epic 4 — competing-operation denial under unsafe lock/state | ✓ Covered |
| FR28 | Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition. | Epic 4 — lock state transitions (active, expired, stale, abandoned, interrupted, released) | ✓ Covered |
| FR29 | Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely. | Epic 4 — workspace lock release | ✓ Covered |
| FR30 | Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP. | Epic 4 — workspace cleanup status visibility for completed/failed/interrupted/abandoned task lifecycles | ✓ Covered |
| FR31 | Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable. | Epic 4 and Epic 6 — lifecycle status currency produced by the task lifecycle and surfaced for operators | ✓ Covered |
| FR32 | Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit. | Epic 4 — file add/change/remove (PutFileInline ≤256KB + PutFileStream multipart) | ✓ Covered |
| FR33 | The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy. | Epic 4 — file-operation policy violation rejection (workspace boundary, path, branch/ref, lock, tenant, provider, folder) | ✓ Covered |
| FR34 | Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution. | Epic 4 — context queries via tree, metadata, search, glob, bounded range reads | ✓ Covered |
| FR35 | Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range and file content are never silently truncated, and a request whose excess cannot be handled by supported truncation returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence. | Epic 4 — context-query policy boundaries (paths, exclusions, binary handling, range/result limits, secret-safe responses) | ✓ Covered |
| FR36 | The operations console must remain read-only and excluded from file editing or file-content browsing capabilities. | Epic 6 — read-only console scope (no file editing or content browsing in console) | ✓ Covered |
| FR37 | Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting automatic evidence moves it to `reconciliation_required`. | Epic 4 — workspace commit for repository-backed folders | ✓ Covered |
| FR38 | Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission. | Epic 4 — task/operation/correlation/actor/author/branch/commit-message/changed-path metadata attachment | ✓ Covered |
| FR39 | The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification. | Epic 4 — task and commit evidence exposure (provider, binding, branch, paths, status, commit ref, timestamps, IDs) | ✓ Covered |
| FR40 | The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation. | Epic 4 — failed/incomplete/duplicate/retried/conflicting operation reporting with stable status and audit evidence | ✓ Covered |
| FR41 | Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new intent. | Epic 4 — idempotent lifecycle retries with stable task/operation/correlation IDs | ✓ Covered |
| FR42 | While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys. | Epic 4 — duplicate logical operation rejection on retry-identity or intent conflict | ✓ Covered |
| FR43 | Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility. | Epic 1 and Epic 4 — canonical error taxonomy defined in the Contract Spine and realized by lifecycle behavior | ✓ Covered |
| FR44 | The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key. | Epic 4 — full error category set (validation/auth/tenant/folder ACL/credential/provider/capability/repository/branch/lock/workspace/path/commit/read-model/duplicate/transient) | ✓ Covered |
| FR45 | The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status. | Epic 4 — canonical workspace/task states (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`) per C6 matrix | ✓ Covered |
| FR46 | After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence. | Epic 4 — final-state explanation + retry eligibility + operational evidence after any lifecycle failure | ✓ Covered |
| FR47 | API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine and every C13-required REST cell passing the shared authorization, idempotency, lifecycle, error, and audit scenarios. | Epic 1 and Epic 5 — versioned REST contract authored first, then proven through cross-surface parity | ✓ Covered |
| FR48 | CLI users can perform every C13-required CLI cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios. | Epic 5 — CLI canonical lifecycle parity | ✓ Covered |
| FR49 | MCP clients can perform every C13-required MCP cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios. | Epic 5 — MCP canonical lifecycle parity | ✓ Covered |
| FR50 | SDK consumers can perform every C13-required SDK cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios. | Epic 1 and Epic 5 — SDK generated from the Contract Spine and proven through canonical lifecycle parity | ✓ Covered |
| FR51 | The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior. | Epic 1 and Epic 5 — cross-surface equivalence defined by the Contract Spine/parity oracle and validated across surfaces | ✓ Covered |
| FR52 | Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing. | Epic 6 — read-only ops console projection consumption (readiness, binding, workspace, lock, dirty, commit, failure, provider, credential-ref, sync) | ✓ Covered |
| FR53 | Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations. | Epic 6 — metadata-only audit trail inspection (success/denied/failed/retried/duplicate) | ✓ Covered |
| FR54 | Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources. | Epic 6 — incident reconstruction from immutable audit metadata | ✓ Covered |
| FR55 | File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown. | Epic 4 (write-side: redaction in events/projections/logs/traces/metrics) + Epic 6 (read-side: console rendering with classification + lock-icon affordance) | ✓ Covered |
| FR56 | Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only if, before any stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor holds incident-admin permission and fresh current tenant/folder authorization. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record. | Epic 6 — operation timelines for folder, workspace, file, lock, commit, provider, status, authorization events | ✓ Covered |
| FR57 | Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness. | Epic 6 — provider support evidence visibility for GitHub and Forgejo | ✓ Covered |
| FR58 | Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe. | Epic 10 — authorized Memories search-index query facade with Folders-side tenant/folder/workspace trimming, authoritative hydration, and metadata-only redaction | ✓ Covered |

### Missing Requirements

No PRD functional requirement is absent from the explicit epic coverage map. No epic coverage-map identifier falls outside the PRD's FR1–FR58 range.

This step verifies declared traceability only. It does not determine whether the mapped stories are sufficiently detailed, current, independently implementable, or sequenced correctly; those questions are reserved for the later epic/story quality review.

### Coverage Statistics

- Total PRD FRs: 58
- FRs covered in epics: 58
- Missing PRD FRs: 0
- Extra epic FR identifiers: 0
- Coverage percentage: 100%

## UX Alignment Assessment

### UX Document Status

**Found.** The selected whole UX document is `ux-design-specification.md`; it is marked complete and defines UX-DR1 through UX-DR32. The supporting `ux-design-directions.html` artifact also exists. The architecture lists the UX specification as an explicit input and contains a dedicated UX integration section.

Overall document alignment is **strong at the target-design level but incomplete for implementation readiness**. The UX and architecture agree on a FrontComposer/Fluent UI Blazor, web/desktop-first, read-only, metadata-only operations console. However, the UX specification predates the ratified July lifecycle and incident-access corrections, while the architecture explicitly records that the production data and read-model paths needed to populate the experience are not yet available.

### UX ↔ PRD Alignment

#### Aligned Areas

- Workspace discovery, tenant-scope confirmation, trust-state inspection, provider/readiness diagnosis, audit reconstruction, and failure diagnosis directly reflect the PRD operator, tenant-administrator, developer, and audit-review journeys.
- UX-DR11, UX-DR12, and UX-DR23 preserve the PRD's non-negotiable read-only boundary: no mutation, repair, file editing, raw diffs, credential reveal, unrestricted browsing, or unauthorized-resource confirmation.
- Workspace Trust Summary, Tenant Scope Banner, Metadata-Only Folder Tree/Table, Diagnostic Timeline, Trust Matrix, and Redaction/Inaccessibility State support FR31, FR36, and FR52–FR57.
- Accessibility and responsive requirements align with the PRD's WCAG 2.2 AA, keyboard, non-color-only, focus, semantic structure, contrast, and zoom requirements.
- The FR58 addendum correctly keeps indexed search metadata-only and preserves Folders authorization, current-authority trimming, removal/archive signals, and the prohibition on content previews.

#### Alignment Issues

1. **High — UX state vocabulary is stale and incomplete.** The UX specification repeatedly lists `ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`, `delayed`, `unknown`, `redacted`, `stale`, `missing`, `unavailable`, `denied`, and `archived`, but it does not define the full PRD lifecycle (`requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, `reconciliation_required`), the separate lock vocabulary (`unlocked`, `locked`, `expired`, `stale`, `revoked`), or the five operator dispositions. This can collapse materially different retry and escalation states in search results, summaries, timelines, and accessible labels.
2. **High — Degraded incident UX is underspecified.** The PRD requires both incident-admin permission and fresh tenant/folder authorization before any stream lookup, counting, checkpoint access, filtering, or shaping, plus C9 redaction, safe denial audit, persistent degraded warning, last checkpoint, correlation ID, and time window. The UX specification does not define this dual-authorization incident journey or its pre-observation denial states.
3. **Medium — “Global search” scope is ambiguous in the UX document.** UX-DR2/UX-DR28 and several flow sections use “global workspace search” and tenant filters. The PRD forbids global cross-tenant browsing. The architecture resolves this by defining global as only within the caller's already-authorized tenant/folder scope and requiring authorization before lookup, counting, suggestions, filtering, or empty-state classification; the UX specification should state the same rule directly.
4. **Medium — Component phasing conflicts with current MVP language.** The stable UX-DR set requires the Diagnostic Timeline and Trust Matrix, and PRD FR53–FR56 require audit/incident evidence in MVP, yet the UX component roadmap labels Diagnostic Timeline and Trust Matrix as “Phase 2 components.” The intended delivery phase must be clarified so required MVP evidence is not accidentally deferred.

### UX ↔ Architecture Alignment

#### Supported Areas

- Architecture decisions F-1 through F-7 support the selected experience: Blazor Web App Interactive Server through `FrontComposerShell`, Fluent UI Blazor, canonical operator dispositions, visible redaction affordance, dual-authorized incident mode, and explicit page-load/perceived-wait budgets.
- The architecture maps every custom UX evidence component to the UI structure and constrains custom work to domain patterns not supplied by Fluent UI.
- UI boundaries are explicit: `Hexalith.Folders.UI` consumes the generated client/read-model endpoints, remains separate from the domain host, and has no direct aggregate or provider mutation access.
- Architecture accounts for responsive fallback, WCAG 2.2 AA, keyboard navigation, visible focus, semantic headings, screen-reader labels, non-color-only states, zoom resilience, skeleton loading at 400 ms, and a cancel affordance at 2 seconds.
- Architecture preserves the same tenant, state, error, audit, and redaction semantics used by REST, SDK, CLI, and MCP rather than introducing UI-only product truth.

#### Architectural Support Gaps

1. **Critical — The production experience has no authoritative data plane yet.** Architecture states that the sole folder repository is in-memory, EventStore persistence is a no-op, uploaded content is discarded, Git writes are unimplemented, task completion is absent, replay returns 501, and Production intentionally fails to boot. The UX cannot prove a real workspace, durable commit, final task state, or reconstructed history until the Epic 12 substrate exists.
2. **Critical — Required diagnostic projections are seed-only.** `IOpsConsoleDiagnosticsReadModel` and `IWorkspaceTransitionEvidenceReadModel` have no production projection logic and return safe-empty/not-found results outside tests. This preserves confidentiality but cannot populate readiness, lock, dirty, failure, provider/sync, freshness, transition, trust-summary, or timeline outcomes. Architecture assigns closure to Epic 4 Story 4.18 and Epic 6 Stories 6.12–6.14.
3. **High — FR58 search cannot populate UX evidence in deployment.** The Server currently uses an unavailable bridge read model, so context search returns zero hydrated items and indexing status is unavailable. Architecture assigns the non-empty authorized round trip to Epic 10 Stories 10.7–10.8.
4. **High — Planning artifacts disagree on projection ownership.** Architecture assigns product projection work to Epic 4 Story 4.18 and Epic 6 Stories 6.12–6.14 and removes it from Story 11.10, while the selected `epics.md` stops Epic 6 at Story 6.11 and still states that Story 11.10 owns these projections. This leaves the architecture-supported UX without a synchronized implementation path in the epic plan.

### Warnings

- The UX document's readiness patch date is 2026-05-12, while the PRD and architecture incorporate ratified July 2026 changes. Its stable requirements need a synchronization pass for lifecycle, lock, disposition, incident-access, search-scope, and projection-readiness semantics.
- The target UX architecture is coherent, but safe-empty, seed-only, unavailable, no-op, and fake-backed behavior must not be counted as delivery of the positive diagnostic experience.
- UX implementation should not begin its production-data-dependent pages until the durable source events and owning production projections have independently completable stories and acceptance evidence.

## Epic Quality Review

### Review Summary

The selected `epics.md` has a sound BDD-shaped story format and declares coverage for all 58 PRD functional requirements. Product-oriented Epics 2–6 generally describe recognizable user outcomes, and most within-epic story ordering builds on earlier rather than later stories.

It nevertheless **fails implementation-readiness quality**. The epic plan is an older 2026-07-07 authority while the architecture records ratified 2026-07-14/15/20 delivery changes. The current architecture explicitly says no independently completable production vertical slice exists, but the missing closure work has not been incorporated into the selected epic plan. Consequently, declared traceability and prior completion language overstate what the planned stories can deliver.

### Critical Violations

#### Q-C1 — Ratified implementation work is absent from the epic plan

The architecture charters work that `epics.md` does not contain:

- Epic 4 Story 4.18 for the production workspace transition-evidence projection.
- Epic 6 Stories 6.12–6.14 for seven production ops-console diagnostic projections and deployed-host journeys.
- Epic 10 Stories 10.7–10.9 for the Server-referenceable search bridge, the non-empty authorized FR58 round trip, and the separately gated body-content follow-up.
- Epic 11 Stories 11.14–11.15 for Memories platform seams and a DCP-capable cross-repository verification lane.
- Epic 12 Stories 12.1–12.5 for durable EventStore persistence/replay, production projections and task completion, durable file content, real Git persistence, and Memories egress.
- Epic 13 Stories 13.1–13.6 for ratified security and operational hardening.

This is not optional future scope: the architecture identifies Epic 12 as the missing product data plane and sets the overall posture to **NOT READY**. The selected epic plan therefore cannot be used as the complete implementation backlog.

#### Q-C2 — Multiple epics depend on unplanned future work

The architecture's required dependency spine is `Epic 12 durable substrate → Epic 4 transition evidence / Epic 6 diagnostics / Epic 10 search bridge`. In the selected plan, the dependent Epic 4, Epic 6, Epic 8, and Epic 10 stories appear before—and without—the substrate and projections they need. This violates independent epic completion and the prohibition on forward dependencies.

Examples:

- Epic 4 claims a repository-backed lifecycle while the only repository is in-memory, EventStore writes are no-op, uploaded content is discarded, Git writes are unimplemented, tasks never reach `completed`, and replay returns 501.
- Epic 6 claims a truthful operations console while its diagnostic and transition-evidence read models are seed-only and unpopulated in production.
- Story 8.2 claims projection-backed diagnostic routes although the owning production projections are not in the plan.
- Epic 10 claims authorized search while its deployed Server bridge remains unavailable and every candidate is safely dropped during hydration.

Safe-empty, no-op, unavailable, seed-only, or fake-backed behavior is valid confidentiality-preserving fallback behavior, but it is not positive capability completion.

#### Q-C3 — Epic 10's sequence retroactively supplies behavior to earlier stories

Stories 10.1–10.5 were framed as delivered while using a fail-closed/unavailable content path. Story 10.6 later replaces the content materializer with a metadata-derived implementation, and the architecture then requires missing Stories 10.7–10.8 plus Epic 12 to deliver the deployed authorized round trip. Earlier stories therefore cannot be independently completed as written; later stories change the meaning and operational truth of their acceptance.

#### Q-C4 — Several top-level work units are technical milestones rather than independently valuable epics

- Epic 1 is predominantly scaffold, contract generation, extension vocabulary, drift gates, and CI gates.
- Workstream 7 is release governance, evidence collection, runbooks, SLOs, and exit criteria.
- Epic 8 is release-acceptance closure and residual test-baseline work.
- Epic 9 is AppHost topology and platform alignment.
- Epic 11 is cross-repository refactoring, duplication removal, shared APIs, test-helper consolidation, and governance closure.

These are legitimate enabling or release activities, but they do not independently deliver user product outcomes in the manner expected of product epics. They should be explicitly classified and managed as enablers/quality work, or attached to the user-value slices they unblock, so product-completion metrics cannot count them as MVP outcomes.

### Major Issues

#### Q-M1 — Requirement inventories and mappings are stale despite 100% identifier coverage

The epic coverage map contains FR1–FR58, but several mapped summaries predate the current PRD semantics:

- FR4/FR15 ownership still implies platform-engineer configuration where the PRD assigns tenant administrators the configuration action and platform engineers only validation.
- FR28 uses older active/abandoned/interrupted/released vocabulary rather than the separate current lock-state model.
- FR30 does not capture the exact automatic cleanup and retention behavior.
- FR41/FR42 omit the full mutation/read-rejection and expired-key precedence rules.
- FR44 omits newer canonical error categories.
- FR45 reflects an older six-state model rather than the 11-state lifecycle plus separate lock states.
- FR56 does not state the dual-authorization, denial-before-observation incident constraints.
- FR58's older “indexed content” framing does not distinguish the current metadata-token increment from the real deployed round trip required for completion.

The epic NFR inventory is likewise older than the PRD's current 73-item normalized set. Numeric coverage is therefore complete, but semantic coverage is not reliable.

#### Q-M2 — Several stories are too broad for reliable independent implementation

Examples of bundled scope include:

- Story 1.4 combines retention, input limits, OIDC validation, and lifecycle-transition mapping.
- Story 3.4 combines a complete Forgejo adapter with drift detection.
- Story 4.8 combines all context-query families, security boundaries, limits, and semantic-backend boundaries.
- Story 4.16 combines sentinel, path, encoding, isolation, and parallel-execution security suites.
- Story 6.11 combines no-mutation enforcement with the full accessibility, responsive, zoom, keyboard, and screen-reader gate.
- Story 7.17 combines the ADR set and multiple maintenance runbooks.
- Story 8.5 combines unrelated residual failing suites.
- Story 10.6 combines materialization, C4/C9 policy enforcement, orchestration, rebasing, and end-to-end evidence.
- Stories 11.2, 11.3, 11.5, 11.7, 11.10, 11.11, and 11.13 span multiple repositories, concerns, or independently verifiable outcomes.

These stories should be split at independently testable behavior or repository-ownership boundaries.

#### Q-M3 — Acceptance criteria are BDD-shaped but often omit decisive behavior

All 116 story headings contain Given/When/Then criteria, which is a strength. However, some criteria are too terse or outdated to prove the current requirements:

- Story 2.3 omits idempotency, safe-denial, and real persistence expectations.
- Story 3.1 omits wrong-tenant, revoked, stale, and unauthorized credential-reference cases and uses stale actor ownership.
- Story 4.2 says unknown outcomes enter reconciliation rather than explicitly proving `unknown_provider_outcome` followed by bounded reconciliation checks.
- Story 4.8 does not pin C4 numeric limits and result semantics.
- Story 6.9 omits fresh tenant/folder authorization in addition to incident permission and does not prove denial before lookup, counting, checkpoint access, filtering, or shaping.
- Story 10.5 says results are authorized and security-trimmed without specifying current-authority hydration, candidate dropping, unavailable behavior, and the non-empty deployed path.

The file's note that authoritative as-built story files exist elsewhere does not cure readiness of this selected implementation artifact; implementers need the current negative, failure, boundary, and runtime-evidence criteria in the backlog they are asked to execute.

#### Q-M4 — The startup story does not identify the authoritative starter pattern precisely enough

The architecture selects sibling Hexalith modules and the EventStore Admin CLI/MCP/UI patterns rather than a generic external template. Story 1.1 refers only to an “approved project layout,” and Story 1.2 separately lists root configuration. The initial implementation story should name the exact reference patterns, dependency/configuration reproduction rules, and exclusions so the first commit is deterministic.

#### Q-M5 — Planning ownership contradicts the architecture

Epic 6 still says Story 11.10 owns the diagnostics and transition-evidence projections. The architecture explicitly removes product projections from Story 11.10 and assigns them to Stories 4.18 and 6.12–6.14. This creates competing implementation authority over the same behavior.

### Minor Concerns

- Frontmatter declares 115 stories, while the document contains 116 `### Story` headings; the inserted Story 2.8b appears not to have been reflected in the metadata count.
- Labels such as “Epic,” “Workstream,” “release-readiness,” “Phase 2,” and “MVP limitation” are used inconsistently, making product-scope and completion reporting easy to misread.
- Several acceptance criteria rely on references such as C3, C4, C6, C9, S-2, OQ items, or an “approved layout” without always giving implementers a direct artifact path or stable definition.

### Epic-Level Compliance Snapshot

| Work unit | User-value orientation | Independent completion | Story/AC readiness | Result |
|---|---|---|---|---|
| Epic 1 | Mostly technical enablement | Foundational but not a user slice | Mixed; broad workshop/gate stories | Major concerns |
| Epic 2 | Clear tenant-admin/developer value | Positive persistence depends on missing data plane | Current semantic gaps | Critical dependency |
| Epic 3 | Clear configuration/readiness value | Mostly sequenced on prior capabilities | Authority drift; oversized adapter story | Major concerns |
| Epic 4 | Clear agent/developer lifecycle value | Requires missing Epic 12 and Story 4.18 | Broad and stale failure criteria | Critical dependency |
| Epic 5 | Clear cross-surface user value | Depends on truthful Epic 4 behavior | Parity shape is useful | Blocked by dependency |
| Epic 6 | Clear operator/auditor value | Requires missing Stories 6.12–6.14 and Epic 12 | Incident/access gaps | Critical dependency |
| Workstream 7 | Release/governance enablement | Not an independent product slice | Evidence scope is extensive | Reclassify |
| Epic 8 | Release closure, not product value | Projection evidence is forward-dependent | Residual work is bundled | Critical dependency |
| Epic 9 | Platform topology enablement | Enables later integration | Technical milestone | Reclassify |
| Epic 10 | Clear search value | Requires missing Stories 10.7–10.9 and Epic 12 | Earlier acceptance is retroactive | Critical dependency |
| Epic 11 | Refactoring/governance enablement | Cross-repository prerequisite chain | Many oversized stories | Reclassify/split |

### Recommended Remediation

1. Synchronize `epics.md` to the ratified architecture: add Epic 12, Epic 13, and Stories 4.18, 6.12–6.14, 10.7–10.9, and 11.14–11.15; remove superseded ownership statements.
2. Rebuild the dependency order around a demonstrably durable vertical slice. Do not mark the dependent lifecycle, console, or search capabilities complete until their positive production paths pass without in-memory, no-op, unavailable, seed-only, or fake substitutes.
3. Reclassify technical/platform/release work so it cannot be counted as user-value MVP completion, while retaining it as explicit enabling and quality work.
4. Refresh the FR and NFR inventories from the current PRD and regenerate semantic traceability, including actor ownership, lifecycle/lock states, incident authorization, canonical errors, and FR58 completion boundaries.
5. Split broad stories at independently deployable/testable behavior and repository boundaries; make each acceptance set prove positive behavior, safe denial, failure behavior, boundary conditions, restart/durability where relevant, and production-runtime evidence.
6. Reconcile UX, architecture, epics, and sprint status to one dated implementation authority before sprint planning or story execution.

## Summary and Recommendations

### Overall Readiness Status

**NOT READY**

The project is not ready to proceed with the selected epic plan as the implementation authority. The PRD is detailed and all 58 FR identifiers are declared in the coverage map, but this does not establish executable completeness. The PRD itself retains ten release-blocking open items, the UX specification is out of date on important lifecycle and authorization behavior, and `epics.md` omits the durable product data plane and production projections that the architecture now identifies as mandatory.

The decisive blocker is structural: there is no independently completable production vertical slice. Current safe-empty, no-op, seed-only, unavailable, or fake-backed paths protect confidentiality but cannot prove durable file persistence, restart recovery, a real Git commit, terminal task state, populated operations evidence, or an authorized non-empty FR58 round trip.

### Critical Issues Requiring Immediate Action

1. **Establish one implementation authority.** Synchronize the PRD, architecture, UX specification, `epics.md`, and sprint status to the ratified July decisions. The current epic plan contradicts the newer architecture and must not govern implementation unchanged.
2. **Add and sequence the missing durable data plane.** Incorporate Epic 12 Stories 12.1–12.5 and prove durable EventStore persistence/replay, authoritative file content across restart, real Git persistence, task completion, production projections, and recoverable Memories egress without test-only substitutes.
3. **Add the missing production read-model and search work.** Incorporate Story 4.18, Stories 6.12–6.14, Stories 10.7–10.9, and the relevant Epic 11 seam/verification work. Reorder dependencies so product stories do not require later, absent work.
4. **Incorporate ratified security and operational hardening.** Add Epic 13 Stories 13.1–13.6 and keep them visible as release-blocking security/operations work without counting them as product-capability completion.
5. **Close OQ1–OQ10 with governed evidence.** The unresolved timing, file policy, authorization matrix, provider compatibility, non-empty search, production diagnostics, lock identity, idempotency, incident authorization, and calibration decisions prevent release acceptance.
6. **Refresh semantic traceability.** Regenerate the epic FR/NFR inventory from the current PRD, correcting actor ownership, lifecycle and lock vocabularies, cleanup, idempotency, error taxonomy, incident access, and FR58 completion semantics.

### Recommended Next Steps

1. Run a planning-artifact reconciliation session and publish one dated decision record identifying the current PRD, architecture, UX, epic, and sprint-status authority.
2. Update `epics.md` with the missing epics/stories, remove superseded Story 11.10 ownership, correct the story count, and explicitly classify product epics versus technical enablers and release workstreams.
3. Rebuild the dependency graph around the durable substrate and one production vertical slice; reopen any story whose only evidence is no-op, unavailable, seed-only, safe-empty, or fake-backed behavior.
4. Split the identified cross-concern and cross-repository stories into independently testable increments. Expand acceptance criteria to include positive runtime behavior, safe denial, boundary/failure behavior, durability/restart evidence, and production composition.
5. Update the UX specification for the 11-state lifecycle, separate lock states, five dispositions, dual-authorized incident access, tenant-bounded search, and MVP diagnostic component phasing.
6. Re-run implementation readiness only after the synchronized epic plan maps every current requirement to independently implementable stories and the ten open release items have named evidence/approval paths.

### Final Note

This assessment identified **30 documented issues across three categories**: 10 open PRD/release-contract items, 8 UX/architecture alignment or support gaps, and 12 epic/story quality findings. Several findings overlap around the same root cause—planning authority drift—but each describes a distinct decision, dependency, or implementation defect. Address the critical issues before implementation proceeds under this backlog; proceeding as-is would predictably produce control-plane evidence without a releasable product data plane.

**Assessment date:** 2026-08-04  
**Assessor:** Codex, applying the BMad Implementation Readiness workflow
