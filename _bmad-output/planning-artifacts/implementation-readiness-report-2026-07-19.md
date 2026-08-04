---
stepsCompleted:
  - step-01-document-discovery
  - step-02-prd-analysis
  - step-03-epic-coverage-validation
  - step-04-ux-alignment
  - step-05-epic-quality-review
  - step-06-final-assessment
status: complete
overallReadiness: not-ready
assessedAt: '2026-07-19'
documentsIncluded:
  prd: '_bmad-output/planning-artifacts/prd.md'
  architecture: '_bmad-output/planning-artifacts/architecture.md'
  epics: '_bmad-output/planning-artifacts/epics.md'
  ux: '_bmad-output/planning-artifacts/ux-design-specification.md'
---

# Implementation Readiness Assessment Report

**Date:** 2026-07-19
**Project:** Hexalith.Folders

## Document Inventory

| Type | File | Size | Last Modified | Format |
|---|---|---|---|---|
| PRD | `prd.md` | 124 KB | 2026-07-19 17:35 | Whole |
| Architecture | `architecture.md` | 203 KB | 2026-07-19 23:37 | Whole |
| Epics & Stories | `epics.md` | 170 KB | 2026-07-07 21:05 | Whole |
| UX Design | `ux-design-specification.md` | 56 KB | 2026-07-07 09:25 | Whole |

**Supporting documents (context, not assessed as primary):**

- `prd-validation-report.md` (2026-06-26)
- `product-brief-Hexalith.Folders.md`
- `reconcile-architecture-downstream-2026-07-19.md` (same-day downstream reconciliation companion to today's architecture update)
- `ux-design-directions.html` (design exploration artifact)
- Prior readiness reports: 2026-05-10 → 2026-07-15 (9 reports); this report supersedes `implementation-readiness-report-2026-07-15.md`

**Discovery findings:**

- No duplicate whole/sharded document conflicts — each primary document exists in exactly one whole-document form. The `architecture/architecture-folders-2026-07-19/` folder contains only a `.memlog.md` working file, not a sharded document.
- No missing required documents.
- Freshness note: `architecture.md` was regenerated 2026-07-19 (today), while `epics.md` and `ux-design-specification.md` date from 2026-07-07 — architecture↔epics alignment drift is a focus area for this assessment.

## PRD Analysis

**Source:** `prd.md` (status: final; updated 2026-07-15; `implementationReadiness: not-ready` as of 2026-07-15 assessment; MVP decision: durable-repository-round-trip-required, ratified 2026-07-14). Read in full (958 lines).

### Functional Requirements

**Capability Contract Terms**

- FR1: Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing.
- FR2: Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions.
- FR3: Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys.

**Authorization and Tenant Boundary**

- FR4: Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it.
- FR5: Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting verb scope is visible in effective permissions and auditable without exposing hidden principals.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

**Folder Lifecycle**

- FR11: Authorized actors with fresh tenant authority can create a logical folder within that tenant and receive its tenant-scoped managed identity and initial lifecycle state; denial creates no folder or provider side effect and uses the safe authorization/lifecycle result.
- FR12: Authorized actors can inspect folder lifecycle and binding status with freshness and availability metadata; an unauthorized, hidden, stale, or unavailable state uses the canonical non-enumerating result rather than partial binding details.
- FR13: Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.
- FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

**Provider Readiness and Repository Binding**

- FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass and receive its canonical provider/repository binding plus inspectable folder/workspace state; failed readiness or authorization creates no repository or binding side effect and returns the canonical safe result.
- FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.
- FR20: Authorized tenant administrators can define or select the branch/ref policy used by repository-backed folder tasks; an accepted policy becomes part of readiness, binding, and the canonical serializing target, while invalid or unauthorized changes are rejected without changing the active binding.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
- FR23: Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready.

**Workspace and Lock Lifecycle**

- FR24: Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect.
- FR25: Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata.
- FR28: Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.
- FR29: Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.
- FR30: Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.
- FR31: Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable.

**File Operations and Context Queries**

- FR32: Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
- FR34: Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution.
- FR35: Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range and file content are never silently truncated, and a request whose excess cannot be handled by supported truncation returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.
- FR36: The operations console must remain read-only and excluded from file editing or file-content browsing capabilities.

**Commit, Evidence, and Idempotency**

- FR37: Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting automatic evidence moves it to `reconciliation_required`.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission.
- FR39: The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification.
- FR40: The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation.
- FR41: Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new intent.
- FR42: While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

**Error, Status, and Diagnostics Contract**

- FR43: Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility.
- FR44: The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.
- FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.
- FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.

**Cross-Surface Contract**

- FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine and every C13-required REST cell passing the shared authorization, idempotency, lifecycle, error, and audit scenarios.
- FR48: CLI users can perform every C13-required CLI cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR49: MCP clients can perform every C13-required MCP cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR50: SDK consumers can perform every C13-required SDK cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

**Audit and Operations Visibility**

- FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.
- FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.
- FR56: Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only if, before any stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor holds incident-admin permission and fresh current tenant/folder authorization. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

**Authorized Search Facade**

- FR58: Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe. (Cross-workspace body-content indexing/recall is explicitly NOT part of FR58 or the current release; bounded live-workspace body search stays in FR34–FR35.)

**Total FRs: 58**

### Non-Functional Requirements

The PRD states NFRs as category bullets (no inline numbering). The repository's canonical numbering is `docs/exit-criteria/nfr-traceability.md` (NFR1–NFR70); the current PRD bullet counts do not map 1:1 to those rows in every category (e.g., Security: 12 PRD bullets vs canonical NFR1–NFR10; Reliability: 12 vs NFR11–NFR20; Performance: 12 vs NFR21–NFR31; Observability: 8 vs NFR47–NFR55; Retention: 5 vs NFR56–NFR61) — flagged for the coverage steps. Extraction below uses per-category sequence labels.

**Security and Tenant Isolation (SEC-1..12; canonical NFR1–NFR10)**

- SEC-1: Tenant isolation enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, asynchronous provider side effect, and audit record; no incoming webhook ingestion in MVP.
- SEC-2: Cross-tenant access leaks are zero-tolerance defects; no object from tenant A retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
- SEC-3: Tenant isolation tests cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, asynchronous work, audit records, index results, and context-query results.
- SEC-4: File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
- SEC-5: Secrets and sensitive payloads redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
- SEC-6: Authorization denials use safe error shapes that avoid unauthorized resource enumeration.
- SEC-7: Every mutation and asynchronous side effect revalidates current tenant, folder, delegated-actor, binding, and credential authority before touching a protected resource; revocation fails closed and changes any held lock to revoked/inaccessible.
- SEC-8: Paths, repository names, branch names, and commit messages are tenant-sensitive by default; confidential tenant override stores a stable tenant-scoped correlation token; redacted/hidden/unknown/missing/stale/unavailable remain visibly distinct.
- SEC-9: Credential references validated and displayed only as non-secret identifiers or status indicators.
- SEC-10: Provider credentials and repository bindings tenant-scoped, never reused across tenants even with identical repository URLs.
- SEC-11: Provider credentials use least privilege and are validated against required provider capabilities before use.
- SEC-12: Build, dependency, package, and generated SDK artifacts traceable to source and free of secrets or tenant data.

**Reliability, Idempotency, and Failure Visibility (REL-1..12; canonical NFR11–NFR20)**

- REL-1: Workspace lifecycle uses only canonical lowercase wire states; lock state and generic operation-execution status are separate, labeled dimensions.
- REL-2: Every accepted operation exposes operation identity, workspace lifecycle, applicable lock state, projection freshness, and a terminal or inspectable non-terminal outcome.
- REL-3: Repository-backed task lifecycle operations leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.
- REL-4: Unconfirmed external effects enter `unknown_provider_outcome` (bounded automatic read-only checks only); exhausted/conflicting evidence moves to `reconciliation_required` (blocks retry/mutation/takeover, requires human escalation); never collapsed into generic failure.
- REL-5: Idempotency keys required for every mutating Contract Spine operation; non-mutating operations reject them.
- REL-6: While unexpired, same key + equivalent payload returns the same logical result; same key + conflicting payload returns idempotency conflict; after expiry, either reuse returns `idempotency_key_expired`, requires state refresh, never auto-executes as new.
- REL-7: Idempotent lifecycle operations create no duplicate domain events, provider writes, file changes, repositories, or commits.
- REL-8: Lock acquisition deterministic; one active writer per managed tenant + canonical provider/repository identity + normalized target ref; aliases collide.
- REL-9: Lock behavior defines conflict response, lease duration, renewal, expiry, cleanup after failed commit, and whether commit releases the lock.
- REL-10: Lock contention, stale locks, abandoned locks, and interrupted tasks produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
- REL-11: Committed state requires provider-confirmed durable update; timeout/unconfirmed results follow unknown-provider-outcome → reconciliation-required flow, no blind retry.
- REL-12: Failure visibility exposes state, cause category, retryability, and correlation ID without automated remediation in MVP.

**Performance and Query Bounds (PERF-1..12; canonical NFR21–NFR31)**

- PERF-1: Command submission acknowledges accepted lifecycle commands within 1 second p95.
- PERF-2: Status and audit summary queries return within 500 ms p95 for bounded MVP inputs.
- PERF-3: Context queries return within 2 seconds p95 for bounded MVP inputs.
- PERF-4: Targets apply to bounded MVP inputs and control-plane responses; recalibrate before release if misleading.
- PERF-5: Provider/workspace operations may complete asynchronously with operation identity and status visibility rather than indefinite blocking.
- PERF-6: Context queries: ≤100 requested paths, ≤2,000 tree entries, ≤500 search/glob results, ≤262,144-byte bounded range, ≤1,048,576-byte aggregate response, 2-second server execution stop; excess input returns stable input-limit result without partial execution; truncation only after authorization/path filtering with one `isTruncated` flag; file content never silently truncated.
- PERF-7: Query-limit audit evidence includes family, configured limit, actual count/bytes, elapsed time, truncation, safe category, correlation ID; excludes raw query text, file content, path lists, unauthorized existence.
- PERF-8: Tree, search, glob, metadata, and bounded range queries protect the service from unbounded workspace scans.
- PERF-9: Large file and binary handling limits explicit before MVP release; unsupported files fail with stable policy errors.
- PERF-10: Provider calls use explicit timeout budgets, retry limits, and backoff caps.
- PERF-11: Provider calls report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states.
- PERF-12: Provider rate-limit responses preserve retry hints and expose retry-after or classified retryability.

**Scalability and Capacity (SCAL-1..5; canonical NFR32–NFR36)**

- SCAL-1: MVP release calibration supports 4 concurrent tenants, 2 folders/tenant, 2 active workspaces/tenant, 2 concurrent agent tasks/tenant, ≥1 lifecycle op/sec without cross-tenant or cross-task interference.
- SCAL-2: Folder and workspace operations scoped by tenant and folder boundaries, no single global operation bottleneck.
- SCAL-3: Audit, timeline, and file-context projections remain queryable as folder history grows.
- SCAL-4: Large batches of file operations remain traceable without making routine status, audit, or context queries unusable.
- SCAL-5: Capacity claims beyond approved C1/C5 release-calibration units require new evidence.

**Integration and Contract Compatibility (INT-1..10; canonical NFR37–NFR46)**

- INT-1: REST, CLI, MCP, and SDK preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes.
- INT-2: Public contracts versioned; breaking changes require an explicit new versioned contract.
- INT-3: Support at least the active contract version; define a deprecation policy before removing any public lifecycle contract.
- INT-4: Shared or generated contract tests validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
- INT-5: OpenAPI 3.1 Contract Spine is canonical; generated SDK is the typed canonical client; CLI/MCP wrap it; REST validates against the spine; every operation has exactly one C13 parity row.
- INT-6: GitHub and Forgejo validated through provider contract tests before either is marked ready.
- INT-7: Provider contract tests cover only MVP-dependent lifecycle behavior.
- INT-8: Supported provider products, instance/API versions, credential profiles, and behavior assumptions published and recorded; unknown compatibility cannot be marked ready.
- INT-9: Provider capability differences reported explicitly, not inferred from failed operations.
- INT-10: Provider failures map to stable product error categories.

**Observability, Auditability, and Replay (OBS-1..8; canonical NFR47–NFR55)**

- OBS-1: Every successful, denied, failed, retried, or duplicate operation traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category.
- OBS-2: Audit data metadata-only and sufficient to reconstruct what happened.
- OBS-3: Paths, commit messages, repository names, branch names tenant-sensitive under C9; confidential override stores stable correlation token at write time; provider payloads, file bodies, secrets, generated context forbidden.
- OBS-4: Operations-console views projection-first, read-only; bounded incident view only for incident-admin + tenant/folder access with persistent warning, last checkpoint, correlation ID, time window.
- OBS-5: Rebuilding read-model views from empty produces deterministic status, audit, and timeline results from the same ordered event stream.
- OBS-6: Lifecycle events appear in status/audit views within the defined status-freshness target (C2: 500 ms).
- OBS-7: Operational signals exposed for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, cleanup status.
- OBS-8: Backup/recovery expectations preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.

**Data Retention and Cleanup (RET-1..5; canonical NFR56–NFR61)**

- RET-1: C3 retention binding: audit + commit-idempotency 7 years; workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, auth-claim metadata 400 days; read models 400 days or until rebuilt; temp working files deleted 7 days after task-terminal closure; folder metadata/tombstones tenant lifetime + 400 days, subject to legal hold.
- RET-2: Tenant deletion anonymizes display aliases while preserving metadata-only audit correlation/category/timestamp/outcome evidence.
- RET-3: Workspace cleanup platform-owned, automatic only after task-terminal closure and no active task; dirty/unknown/reconciliation excluded; idempotent retries; no user-triggered cleanup/repair in MVP.
- RET-4: Cleanup failures observable through status, reason code, retryability, timestamp, correlation ID.
- RET-5: No cleanup process removes audit evidence required to reconstruct operations.

**Operations Console Accessibility (ACC-1..5; canonical NFR62–NFR66)**

- ACC-1: Read-only console flows target WCAG 2.2 AA.
- ACC-2: Keyboard navigation for primary diagnostic workflows.
- ACC-3: Status, failure, readiness, lock indicators not color-alone.
- ACC-4: Visible focus states, semantic headings, readable table structure, sufficient contrast.
- ACC-5: Console text, controls, tables readable at common operator browser zoom levels.

**Verification Expectations (VER-1..4; canonical NFR67–NFR70)**

- VER-1: Each NFR category has at least one automated verification path or documented manual validation path before MVP release.
- VER-2: Security, tenant isolation, idempotency, provider contract, read-model determinism, cross-surface contract NFRs have automated tests.
- VER-3: Performance, accessibility, retention, backup/recovery, operations-console usability NFRs have release validation evidence before MVP acceptance.
- VER-4: Security verification includes dependency/package scanning, generated artifact review, least-privilege provider credential validation.

**Total NFR statements extracted: 73 (canonical traceability rows: 70)**

### Additional Requirements

- **Success metrics SM1–SM8** (canonical lifecycle pass rate, zero isolation leaks, ≥95% task completion, latency/freshness targets, capacity, diagnostic completeness, adoption, agent context effectiveness) and **counter-metrics CM1–CM4** (unsafe completion, recovery burden ≤5%, operator burden ≤10%, surface drift zero), all gated by the release-calibration plan (`docs/exit-criteria/release-calibration-plan.md`, tracked by OQ10).
- **Exit criteria C1–C7, C9, C13**: C1/C2/C3/C4/C5/C6/C9 approved with canonical evidence docs; **C7 reference-pending (OQ1)**; C13 generated from the current Contract Spine (49 rows currently; generated inventory is the binding denominator).
- **Open Release Items OQ1–OQ10** — all must close before release acceptance: OQ1 C7 lock/auth timing; OQ2 canonical file-policy artifact; OQ3 authorization matrix denominator; OQ4 provider compatibility catalog; OQ5 FR58 non-empty evidence; OQ6 projection-backed console read models; OQ7 lock-identity alignment; OQ8 all-mutations idempotency + expired-key evidence; OQ9 incident access proof; OQ10 release-calibration plan.
- **Authority model:** PRD prevails for product intent/actors/scope/safety; Contract Spine prevails for operation names, wire schemas, closed error fields; conflicts fail the release gate; no downstream artifact may override either.
- **Task/lock completion model** and bounded reconciliation policy (≤5 read-only evidence checks within 15 minutes; no revoked lock reactivation; per-family reconciliation rules).
- **Two search families** with non-substitutable contracts: live workspace context search (FR34–FR35) vs indexed metadata-token recall (FR58).
- **Explicit MVP non-goals**: no repair automation, no migration wizard, no local-only workspace mode, no rename command, no webhook ingestion, no cross-workspace body indexing, no secret storage, no archived-folder restore/hard deletion, no console file browsing/diffs.
- **Current Delivery Posture:** PRD final as product contract, but implementation `not-ready` per 2026-07-15 assessment; release blocked until the durable repository-backed lifecycle is complete and every Open Release Item closes with approved production evidence; safe-empty/seed-only/unavailable/no-op/fake-backed evidence proves safety but not positive runtime capability.

### PRD Completeness Assessment

The PRD is exceptionally complete and internally disciplined: 58 numbered FRs traceable to 9 user journeys, category-complete NFRs, an explicit authority hierarchy, closed state vocabularies, approved numeric exit criteria with canonical evidence paths, and an explicit Open Release Items register (OQ1–OQ10) that prevents silent scope closure. Frontmatter honestly records `implementationReadiness: not-ready` (2026-07-15). Two watch items for later steps: (1) the PRD NFR bullets no longer map 1:1 to the canonical `nfr-traceability.md` rows (73 extracted vs 70 canonical) — the traceability artifact must remain reconciled; (2) the PRD was updated 2026-07-15 and the architecture regenerated 2026-07-19, while epics.md dates from 2026-07-07 — FR/NFR-to-epic coverage must be validated against the newest text, not the 07-07 snapshot.

## Epic Coverage Validation

**Source:** `epics.md` (last modified 2026-07-07; FR inventory refreshed 2026-05-11; epicCount 11, storyCount 115). Compared against the reconciled PRD (2026-07-15) and `sprint-status.yaml` (current implementation tracker).

### Epic FR Coverage Extracted

`epics.md` §"FR Coverage Map" maps every FR1–FR58 to an owning epic:

- Epic 1 (Contract bootstrap): FR1, FR2, FR3, FR43 (shared), FR47 (shared), FR50 (shared), FR51 (shared)
- Epic 2 (Tenant-scoped folder access/lifecycle): FR4–FR6, FR8–FR14
- Epic 3 (Provider readiness/repository binding): FR7, FR15–FR23
- Epic 4 (Workspace task lifecycle): FR24–FR35 (FR31 shared with Epic 6), FR37–FR46, FR55 (write-side)
- Epic 5 (Cross-surface parity): FR47–FR51
- Epic 6 (Read-only console/audit): FR36, FR31 (read-side), FR52–FR57, FR55 (read-side)
- Epic 10 (Search facade / semantic-indexing producer): FR58

**Total FRs mapped in epics: 58 of 58.**

### Coverage Matrix

Status legend: ✓ = mapped and text-aligned; ⚠ minor = mapped, epics FR text mildly stale vs 2026-07-15 PRD; ⚠⚠ MATERIAL = mapped, but the epics FR text/coverage predates a material PRD contract change (2026-07-14/15 reconciliations) and no epics-level story text carries the new semantics.

| FR | Requirement (short) | Epic Coverage | Status |
| --- | --- | --- | --- |
| FR1 | Glossary-consistent terminology + failing doc/schema checks | Epic 1 | ⚠ minor |
| FR2 | Ordered canonical lifecycle documented/demonstrated per surface | Epic 1 | ⚠ minor |
| FR3 | C13 mutation/read classification; all-mutations idempotency; reads reject keys | Epic 1 | ⚠⚠ MATERIAL — epics text lacks all-mutations rule + read-key rejection (OQ8) |
| FR4 | Tenant admins own tenant-level config incl. archive; operators validate-only | Epic 2 | ⚠⚠ MATERIAL — ownership/archive scope missing in epics text |
| FR5 | Grant AND revoke folder access; verb scope visible + auditable | Epic 2 | ⚠⚠ MATERIAL — revoke + effective-verb visibility missing |
| FR6 | Effective-permissions inspection | Epic 2 | ✓ |
| FR7 | Tenant readiness inspection | Epic 3 | ✓ |
| FR8 | Every operation evaluated against full scope chain | Epic 2 | ✓ |
| FR9 | Cross-tenant denial before resource exposure | Epic 2 | ✓ |
| FR10 | Authorization evidence without enumeration | Epic 2 | ✓ |
| FR11 | Folder creation w/ fresh authority; denial side-effect-free | Epic 2 | ⚠ minor |
| FR12 | Folder/binding inspection w/ freshness + non-enumerating result | Epic 2 | ⚠ minor |
| FR13 | Archive preconditions (no active task/lock/dirty/unknown/reconciliation); provider repo untouched | Epic 2 | ⚠⚠ MATERIAL — preconditions + governance ops missing |
| FR14 | Per-field C3 data-class retention in archived views | Epic 2 | ⚠⚠ MATERIAL — per-class expiry markers missing |
| FR15 | TENANT ADMINS configure provider bindings/credential refs; platform engineers validate | Epic 3 | ⚠⚠ MATERIAL — epics text assigns configuration to platform engineers (actor corrected in PRD UJ2, 2026-07-15) |
| FR16 | Readiness validation before create/bind | Epic 3 | ✓ |
| FR17 | Readiness diagnostics w/ safe reason/retryability/remediation | Epic 3 | ✓ |
| FR18 | Repo-backed folder creation; failure side-effect-free | Epic 3 | ⚠ minor |
| FR19 | Bind pre-created repo w/ duplicate/alias detection, non-enumeration | Epic 3 | ⚠ minor |
| FR20 | Branch/ref policy part of readiness/binding/serializing target | Epic 3 | ⚠ minor |
| FR21 | Provider/credential/binding metadata w/o secrets | Epic 3 | ✓ |
| FR22 | GitHub/Forgejo capability differences | Epic 3 | ✓ |
| FR23 | Provider product/instance/version/credential-profile evidence; unknown ≠ ready | Epic 3 | ⚠ minor |
| FR24 | Prepare workspace w/ fresh authorization; inspectable failure | Epic 4 | ⚠ minor |
| FR25 | Lock on canonical tenant+provider/repo+ref identity; alias collision | Epic 4 | ⚠⚠ MATERIAL — serializing-identity contract (OQ7) absent from epics text |
| FR26 | Lock metadata inspection | Epic 4 | ✓ |
| FR27 | Deterministic competing-mutation denial + single audit record | Epic 4 | ⚠ minor |
| FR28 | Lock states exactly unlocked/locked/expired/stale/revoked | Epic 4 | ⚠⚠ MATERIAL — epics lists active/expired/stale/abandoned/interrupted/released (obsolete vocabulary) |
| FR29 | Lock release w/ idempotency + expired-key semantics | Epic 4 | ⚠⚠ MATERIAL — expired-key precedence missing |
| FR30 | Platform-owned automatic cleanup, C3 7-day boundary, eligibility exclusions | Epic 4 | ⚠⚠ MATERIAL — epics has status-only visibility, not the approved cleanup contract |
| FR31 | Lifecycle/lock/disposition/freshness/index status inspection | Epic 4 + 6 | ⚠ minor |
| FR32 | Multi-mutation staging, no auto-commit, rename=add+remove | Epic 4 | ⚠ minor |
| FR33 | Policy-violating file operations rejected | Epic 4 | ✓ |
| FR34 | Context queries incl. text-body search w/ C4 numeric bounds | Epic 4 | ⚠⚠ MATERIAL — C4 limits + body-search scope absent |
| FR35 | AuthZ/path policy before shaping; C9-wrapped results; isTruncated; no raw-query logging | Epic 4 | ⚠⚠ MATERIAL |
| FR36 | Console read-only, no file editing/browsing | Epic 6 | ✓ |
| FR37 | Commit = provider-confirmed durable update; unknown_provider_outcome flow | Epic 4 | ⚠⚠ MATERIAL — durable-confirmation + unknown-outcome flow absent |
| FR38 | Metadata attachment within closed constraints + C9; secret-like rejection | Epic 4 | ⚠ minor |
| FR39 | Metadata-only task/commit evidence under C9 | Epic 4 | ⚠ minor |
| FR40 | Failure reporting w/ unknown-outcome wait vs reconciliation escalation | Epic 4 | ⚠⚠ MATERIAL |
| FR41 | All-mutations idempotency w/ retention tiers + idempotency_key_expired | Epic 4 | ⚠⚠ MATERIAL — OQ8 semantics absent |
| FR42 | Conflict on same-key different-intent; expired-key precedence; reads reject keys | Epic 4 | ⚠⚠ MATERIAL |
| FR43 | Error taxonomy w/ required fields incl. taskId + details.visibility | Epic 1 + 4 | ⚠ minor |
| FR44 | Error categories incl. idempotency conflict, expired key, unknown outcome, reconciliation | Epic 4 | ⚠⚠ MATERIAL — 4 mandatory categories missing from epics text |
| FR45 | Full 11-state lifecycle + separate lock vocabulary | Epic 4 | ⚠⚠ MATERIAL — epics lists only 6 states |
| FR46 | Post-failure state/cause/retry/client-action/correlation evidence | Epic 4 | ⚠ minor |
| FR47 | REST for every Contract Spine op; C13 REST cells pass shared scenarios | Epic 1 + 5 | ⚠ minor |
| FR48 | CLI C13 cells + shared scenarios | Epic 5 | ✓ |
| FR49 | MCP C13 cells + shared scenarios | Epic 5 | ✓ |
| FR50 | SDK C13 cells + shared scenarios | Epic 1 + 5 | ✓ |
| FR51 | Cross-surface equivalence | Epic 1 + 5 | ✓ |
| FR52 | Tenant-scoped operator status inspection, no global browsing | Epic 6 | ⚠ minor |
| FR53 | Metadata-only audit trails | Epic 6 | ✓ |
| FR54 | Incident reconstruction from immutable C9 metadata | Epic 6 | ⚠ minor |
| FR55 | Content/secret/existence exclusion everywhere; redaction visibly distinct | Epic 4 + 6 | ⚠⚠ MATERIAL — expanded channel list + redaction-distinct rule |
| FR56 | Projection timelines + dual-authorized bounded incident view (OQ9) | Epic 6 | ⚠⚠ MATERIAL — epics text is the old "operation timelines" FR; incident-mode gating absent |
| FR57 | Provider support evidence | Epic 6 | ✓ |
| FR58 | Metadata-token recall + indexing status; never body content/paths/snippets | Epic 10 | ⚠⚠ MATERIAL — epics text says "search the content that Folders has indexed"; PRD now excludes body content from FR58 |

### Missing Requirements

**No FR is absent from the epics coverage map** — presence coverage is 58/58. However, two gap classes block a clean pass:

**1. Structural coverage gap — ratified epics and stories missing from epics.md (CRITICAL)**

The ratified 2026-07-14 structural correction and 2026-07-15 authority registered the following in `sprint-status.yaml` and (as of 2026-07-19) in `architecture.md`, but `epics.md` — the canonical epic/story breakdown — was never updated (confirmed by `reconcile-architecture-downstream-2026-07-19.md` §1):

- **Epic 12 — Durable Persistence & Git Round-Trip** (Stories 12.1–12.6, one in progress): this epic IS the durable delivery path for the runtime substance of FR11–FR14, FR18–FR19, FR24–FR32, FR37–FR42, FR46 and the release-blocking MVP decision (`durable-repository-round-trip-required`). Without it, the epics.md coverage map claims delivery through Epics 2–4 alone, which the 2026-07-14 audit showed is control-plane-shell only.
- **Epic 13 — Security & Operational Hardening** (Stories 13.1–13.6): owns HXF-SEC-002..006, HXF-REL-002/005, HXF-OBS-001 defects.
- **Reopened closure stories 4.18–4.21, 6.12–6.14, 10.7–10.9** (all registered in sprint-status): durable transition-evidence projection, durable prepare/lock/mutation/commit proof, console projection population, and the FR58 completion chain (10.7 bridge → 10.8 real round trip = FR58 completion → 10.9 C9-gated body content).
- **Story 11.10 ownership relocation**: production read-model projection ownership moved from Epic 11 Story 11.10 to Epics 4/6/10; epics.md still names 11.10 as owner in three places (Epic 4, Epic 6, Epic 10 limitation notes).

- Impact: an implementer or story-generation workflow reading epics.md as canonical would (a) believe FR coverage is deliverable without Epic 12, (b) miss 13 registered stories, and (c) assign projection work to the wrong story.
- Recommendation: register Epic 12 and Epic 13 with their stories in epics.md, add stories 4.18–4.21 / 6.12–6.14 / 10.7–10.9 to their epics, and relocate the 11.10 ownership notes, per the pending-edit list in `reconcile-architecture-downstream-2026-07-19.md`.

**2. Semantic drift — epics FR inventory frozen at 2026-05-11 (HIGH)**

21 FRs (marked ⚠⚠ above: FR3, FR4, FR5, FR13, FR14, FR15, FR25, FR28, FR29, FR30, FR34, FR35, FR37, FR40, FR41, FR42, FR44, FR45, FR55, FR56, FR58) carry materially outdated requirement text in epics.md relative to the reconciled PRD. Highest-risk instances:

- FR15: epics assigns provider-binding configuration to platform engineers; the PRD (UJ2 correction) assigns it to tenant administrators with engineers validate-only.
- FR45/FR28: epics lists a 6-state workspace vocabulary and an obsolete lock vocabulary; the PRD defines 11 lifecycle states + 5 lock states — the C6 matrix and all state-machine stories depend on the current vocabulary.
- FR41/FR42/FR3/FR44: the all-mutations idempotency contract, expired-key precedence, and the 4 newer error categories (OQ8) are absent.
- FR25: the canonical serializing lock identity (OQ7) is absent.
- FR56: the epics FR is an entirely different requirement (operation timelines) than the PRD's projection-degradation incident view (OQ9).
- FR58: epics describes indexed content search; the PRD explicitly restricts FR58 to metadata-token recall.

- Impact: story context generated from epics.md would implement superseded contracts; the architecture (2026-07-19) and PRD (2026-07-15) agree with each other, so epics.md is the odd one out.
- Recommendation: refresh the epics.md Requirements Inventory and FR Coverage Map from the 2026-07-15 PRD in the same pass that registers Epics 12/13. Note `reconcile-architecture-downstream-2026-07-19.md` §5 already instructs regenerating pre-2026-07-19 story contexts that cite the old lock scope, idempotency subset, or state vocabularies.

**FRs in epics but not in PRD:** none (the epics inventory contains no extra FR numbers). Non-FR inventories (AR-*, UX-DR*) are supplementary and traced separately.

### Coverage Statistics

- Total PRD FRs: 58
- FRs mapped in epics: 58 (100% presence)
- FRs mapped with aligned text: 18 (31%)
- FRs mapped with minor stale text: 19 (33%)
- FRs mapped with MATERIAL semantic drift: 21 (36%)
- Ratified epics missing from epics.md: 2 (Epic 12, Epic 13) + 13 registered stories (4.18–4.21, 6.12–6.14, 10.7–10.9, 13.1–13.6 partially overlapping) + 1 ownership relocation (11.10)
- Effective coverage verdict: **presence complete, currency failed** — the epics document cannot serve as the canonical implementation source until reconciled.

## UX Alignment Assessment

### UX Document Status

**Found:** `ux-design-specification.md` (792 lines, last modified 2026-07-07; UX-DR1–UX-DR32 stable requirements table + FR58 UX alignment addendum). The UI in scope is the tenant-scoped read-only operations console — UX documentation is required and present.

### Alignment Confirmed (no action)

- **Design system**: FrontComposer Shell + Microsoft Fluent UI Blazor, no second component library (UX-DR1) — matches architecture's custom-components-only-for-domain-evidence rule and the repo's pinned Fluent UI 5.0 RC line.
- **Hosting terminology**: the spec already says "Blazor Web App host rendering FrontComposerShell" — the 2026-07-15 hosting correction is substantially reflected (only the explicit "Interactive Server" render-mode phrasing is missing; minor).
- **Read-only boundary** (UX-DR11/12/23): matches PRD FR36 and MVP non-goals exactly (no mutation, credential reveal, file browsing, raw diffs).
- **Redaction distinctness** (UX-DR10/22): matches PRD C9 rule that redacted/hidden/unknown/missing/stale/unavailable are visibly distinct.
- **Accessibility** (UX-DR30–32): matches NFR62–66 (WCAG 2.2 AA, keyboard, no color-alone, zoom); the honest-green UI-E2E + axe CI gates are delivered and conformance-pinned.
- **FR58 boundary**: the spec's FR58 addendum (no content preview surfaces, preserve authorization/trimming) matches the PRD's metadata-token-only contract.
- **Journeys**: the three UX journeys (find/inspect trust state, prove tenant isolation, diagnose from evidence) map cleanly to PRD UJ5, UJ6, and UJ8.

### Alignment Issues

1. **State vocabulary drift (HIGH — same root cause as the epics drift).** UX-DR15 enumerates `ready, locked, dirty, committed, failed, inaccessible, delayed, unknown, redacted, stale, missing, unavailable, denied, archived` — the pre-reconciliation display list. The spec nowhere mentions `requested`, `preparing`, `changes_staged`, `unknown_provider_outcome`, `reconciliation_required` (PRD's 11-state lifecycle), the separate 5-value lock-state dimension (`unlocked/locked/expired/stale/revoked`), or operator disposition. Architecture (2026-07-19) mandates **six independent display dimensions** that surfaces must never conflate; the UX spec still models one flat state list.
2. **Operator disposition absent (HIGH).** Architecture makes disposition labels (`available`, `auto-recovering`, `degraded-but-serving`, `awaiting-human`, `terminal-until-intervention`) the primary console visual, with `unknown_provider_outcome` rendered as automatic-reconciliation progress (last check, remaining budget, next check). No disposition concept exists anywhere in the UX spec.
3. **Incident-mode UX missing (HIGH — blocks FR56/OQ9 UX readiness).** PRD FR56 and the architecture's Incident-Evidence Authorization Gate (dual authorization: incident-admin permission + fresh tenant/folder authorization) require a designed degraded-projection incident view: persistent degraded banner, last projection checkpoint, correlation/time-window context, safe denial behavior. The UX spec has no incident-mode journey, component, or the expected UX-DR33 requirement row (table ends at UX-DR32).
4. **Architecture-pinned components not yet specified (MEDIUM).** Architecture now pins normative components the spec does not define: Authorized Search & Results (authorization + safe scope establishment must precede candidate lookup, counting, suggestions, and empty-state classification), Access Evidence, Provider Readiness Evidence, Incident-Evidence Authorization Gate. The spec's existing components (Trust Summary, Scope Banner, Metadata Tree, Timeline, Trust Matrix, Redaction State) remain valid but incomplete.

These are the exact pending edits already recorded in `reconcile-architecture-downstream-2026-07-19.md` §2 — the assessment confirms that list is accurate and nothing further is missing from it.

### Architecture Support Check

Architecture supports every UX requirement in the spec (Blazor Web App + FrontComposerShell + Fluent UI, projection-first reads, C9 redaction affordances, performance budgets F-7, incident path F-6). The gap direction is one-way: the **UX spec trails the architecture/PRD**, not the reverse. No UX requirement lacks architectural support.

### Warnings

- ⚠ The UX spec (2026-07-07) predates the 2026-07-14/15 reconciliations and today's architecture regeneration. Console stories implemented from it (notably reopened Stories 6.12–6.14 and the FR56 incident view) would build the wrong state model. Refresh the UX spec per reconcile §2 **before** dev work starts on Epic 6 closure stories.
- ⚠ UX-DR numbering: architecture/reconcile references "UX-DR33" (incident gate) which does not yet exist in the spec — add it during the refresh so cross-references resolve.

## Epic Quality Review

Reviewed against create-epics-and-stories standards: user-value framing, epic independence, story sizing/independence, AC quality, dependency hygiene. Scope: epics.md (Epics 1–11, 115 stories) plus the ratified-but-unregistered Epic 12/13 and split/closure stories tracked in `sprint-status.yaml`.

### What Passes

- **User-value epic framing:** Epics 1–6 are phrased as actor outcomes ("Tenant administrators … can create folders, manage access…"), each with FR coverage and explicit guardrails. Epic 1 (contract bootstrap) is justified as consumer-facing contract value per the approved AR-PROPOSAL-02 reframe.
- **Non-product workstreams honestly labeled:** Workstream 7, Epics 8/9/11 are explicitly excluded from product-MVP completion metrics with creation provenance (bmad-correct-course proposals) — the correct brownfield pattern, not disguised technical epics.
- **Story structure:** Stories use role/want/so-that plus Given/When/Then ACs; terse planning ACs are explicitly declared non-authoritative in favor of as-built story files (header note, 2026-06-22) — a sound two-tier model.
- **Forward-dependency repairs held:** Stories 4.3, 4.4, 4.11, 6.3, 6.4 (repaired via AR-PROPOSAL-04) contain no forward references; sampled ACs confirm.
- **Justified technical stories:** Story 2.8b (production `/process` wiring) carries an explicit code-review-finding rationale and ADR link.
- **Starter template rule satisfied:** Architecture specifies scaffold-by-mirroring (AR-SCAFFOLD-01); Story 1.1 is exactly that scaffold story with build-without-credentials ACs.
- **Entity-creation timing:** N/A in the relational sense (event-sourced); aggregates are introduced by the stories that need them (2.2 Organization, 4.1 Folder state machine) — compliant.
- **Sprint-status is fully reconciled** to the ratified 2026-07-14/15 authority: Epics 12/13, closure stories 4.18–4.21 / 6.12–6.14 / 10.7–10.9, and every ratified split (3.10–3.14, 5.8–5.11, 11.14–11.19, 13.1–13.6) are registered with correct statuses.

### 🔴 Critical Violations

1. **Canonical epic file lacks ~28 ratified stories and 2 epics.** `epics.md` — the authoring source for story creation — is missing Epic 12 (Durable Repository-Backed Round Trip, 12.1–12.6), Epic 13 (Security & Operational Hardening, 13.1–13.6), closure stories 4.18–4.21 / 6.12–6.14 / 10.7–10.9, and the ratified splits 3.11–3.14, 5.8–5.11, 11.14–11.19. Of the 15 durable-path/closure stories, only two have authored story files (10-8 ready-for-dev, 12-6 in-progress); the other 13 exist solely as sprint-status keys plus charter text in `sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md`. Any story-creation run against epics.md will either fail to find them or author them from stale text.
   - Remediation: apply proposal §5.5–§5.7 into epics.md verbatim (epic charters, story lists, the mandatory story-level acceptance scenario families, split scopes, and the 11.10→11.10/11.14/11.15 replacement).
2. **Undocumented cross-epic forward dependency (Epics 4/6/10 → Epic 12).** The ratified delivery sequence makes 12.1→12.2/12.3→12.4 prerequisites for Epic 4 production closure (4.19–4.21), Epic 6 populated diagnostics (6.12–6.14), and Epic 10 real search closure (10.7–10.8). This inverts epic numbering (lower-numbered epics depend on a higher-numbered one). The dependency spine is explicit in the proposal but **absent from epics.md**, so nothing in the canonical planning document prevents a dev picking 4.19 or 6.12 before the Epic 12 substrate exists.
   - Remediation: when registering Epic 12 in epics.md, carry the delivery-sequence graph and add "Given Epic 12 Story 12.x is done" preconditions to the closure stories (or an equivalent gating note), mirroring sprint-status sequencing.

### 🟠 Major Issues

1. **Open stories in epics.md carry superseded contracts.** Examples: Story 4.3's AC models the lock as a folder-state transition with no canonical serializing identity or alias collision (OQ7); Story 11.2 still reads "Land platform prerequisite APIs in shared modules" though the ratified narrowing is "Inventory, assign, and pin platform prerequisites" (Model B — no upstream authoring); Stories 5.2/5.3 and 3.3/3.4 retain pre-split scopes although their split children (5.8–5.11, 3.10–3.14) are already tracked, with 3.10 in progress. Risk: a regenerated story context inherits obsolete scope — exactly the failure `reconcile-architecture-downstream-2026-07-19.md` §5 warns about.
2. **13 registered stories have no acceptance criteria at any level of story granularity.** The structural-correction proposal defines mandatory scenario families (production-path success, denial, equivalent/conflicting replay, known failure, unknown outcome + reconciliation, restart-surviving state, terminal status, metadata-only audit, sensitive-data exclusion; read-model registration/population/rebuild/isolation/freshness/unavailable) — but these are charter-level. Until each story is authored (via create-story against a reconciled epics.md), the mandatory scenarios are not bound to specific stories and cannot gate dev work.

### 🟡 Minor Concerns

1. **Story 2.8b supersession not recorded:** the proposal makes 2.8 absorb 2.8b's wiring/evidence, leaving 2.8b a "superseded alias", but epics.md still presents 2.8b as an active story with its own ACs.
2. **Epic numbering vocabulary drift:** "Release Readiness Workstream 7" vs "Epic 7" naming is inconsistent across artifacts (sprint-status uses epic-7); harmless but worth normalizing during the reconcile pass.
3. **Planning-AC terseness:** several planning ACs (e.g., 1.4, 4.4) compress multiple scenario families into one Then/And pair; tolerated because as-built story files are authoritative, but new stories must not copy this compression given the mandatory scenario families above.

### Dependency Analysis Summary

- Within-epic sequencing of Epics 1–6 is clean and backward-only (verified by sampling and by the absence of forward-reference phrases in the story bodies).
- The only forward dependency is the ratified Epic 12 inversion (Critical #2) — managed in sprint-status, unmanaged in epics.md.
- The FR58 completion chain is now 10.6 → 10.7 → (12.5) → 10.8 (= FR58 completion) with 10.9 as the separately authorized C9-gated body-content follow-on; the current PRD's FR58 metadata-token rescope makes 10.9 non-blocking for release. epics.md must carry this chain when reconciled.

## Summary and Recommendations

### Overall Readiness Status

**NOT READY** — with a narrow, explicitly bounded allowance for in-flight work.

The planning authority chain is healthy at the top: the PRD (2026-07-15) and architecture (regenerated 2026-07-19) are mutually aligned, and `sprint-status.yaml` faithfully tracks the ratified 2026-07-14/15 structural correction. But the two artifacts this workflow exists to validate as implementation sources — `epics.md` and `ux-design-specification.md` — are frozen at 2026-07-07 and materially behind that authority. Driving new story creation or dev-context generation from them today would implement superseded contracts (old lock identity, old state vocabulary, idempotency subset, pre-split story scopes) and would miss the entire durable data plane (Epic 12) that the ratified MVP decision made release-blocking.

**Allowance:** stories with story files already authored from the ratified authority — 12-6 (in progress) and 10-8 (ready-for-dev) — may proceed; their contexts postdate the correction. Everything else waits on the reconcile pass.

This verdict is consistent with, and narrower than, the PRD's own recorded posture (`implementationReadiness: not-ready`, 2026-07-15): product release additionally remains blocked on Epic 12 delivery and closure of Open Release Items OQ1–OQ10.

### Critical Issues Requiring Immediate Action

1. **epics.md is stale against the ratified authority (CRITICAL).** Missing: Epic 12 (12.1–12.6) and Epic 13 (13.1–13.6) charters; closure stories 4.18–4.21, 6.12–6.14, 10.7–10.9; ratified splits 3.11–3.14, 5.8–5.11, 11.14–11.19; the 11.10 ownership relocation; the Epic 12 dependency spine; and current FR text for 21 of 58 FRs (36% material drift, incl. FR15 actor inversion, FR45/FR28 state vocabularies, FR41/FR42 idempotency, FR25 lock identity, FR56 incident view, FR58 metadata-token rescope).
2. **Undocumented forward dependency (CRITICAL).** Epics 4/6/10 closure stories depend on Epic 12 substrate (12.1→12.2/12.3→12.4). Ratified and sequenced in the proposal and sprint-status, but invisible in epics.md — nothing stops premature pickup of 4.19/6.12/etc.
3. **ux-design-specification.md is stale (HIGH).** Missing the six independent state/disposition display dimensions, the 5-value lock and 5-value disposition vocabularies, `unknown_provider_outcome`-as-auto-reconciliation rendering, the UX-DR33 Incident-Evidence Authorization Gate, and four architecture-pinned components. Blocks honest UX readiness for FR56/OQ9 and Epic 6 closure stories.
4. **13 registered stories have no acceptance criteria at story granularity (HIGH).** The mandatory scenario families exist only at charter level in the structural-correction proposal; they bind to stories only when authored from a reconciled epics.md.

### Recommended Next Steps

1. **Reconcile epics.md** (bmad-correct-course or an epics-refresh pass) applying `sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md` §5.5–§5.7 and `reconcile-architecture-downstream-2026-07-19.md` §1: register Epics 12/13 + all missing stories/splits, carry the delivery-sequence graph and mandatory acceptance scenario families, relocate 11.10 ownership, mark 2.8b superseded, and refresh the FR inventory + FR Coverage Map from the 2026-07-15 PRD.
2. **Refresh ux-design-specification.md** per reconcile §2 (dimensions, dispositions, UX-DR33, pinned components) before any Epic 6 closure story (6.12–6.14) or FR56 incident-view work is authored.
3. **Regenerate stale story contexts**: any story context generated before 2026-07-19 citing the old lock scope, idempotency subset, four-disposition list, "Blazor Server", or 11.10 projection ownership (reconcile §5).
4. **Proceed with the durable path in ratified order**: finish 12-6; author and execute 12.1 → 12.2/12.3 → 12.4 → 12.5 from the reconciled epics.md; only then open the Epic 4/6/10 closure stories.
5. **Land the Contract Spine / C13 edits in gate-lockstep** (reconcile §3): all-mutations `x-hexalith-idempotency-*` coverage, read-cell key rejection, `idempotency_key_expired` (CLI exit 76, MCP kind), canonical serializing-identity lock semantics, generated-inventory denominators (OQ7/OQ8).
6. **Track release acceptance separately** through OQ1–OQ10 closure with approved evidence; keep honoring the governance decoupling precedent and the `ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` hard-pin when touching NFR traceability rows (note: the PRD-bullet↔NFR-row mapping is no longer 1:1 — 73 extracted vs 70 canonical — verify during the next traceability touch).

### Final Note

This assessment identified **13 findings across 4 categories** (epic coverage: 2 gap classes spanning 21 FR drifts + 2 missing epics/~28 stories; UX alignment: 4 issues + 2 warnings; epic quality: 2 critical, 2 major, 3 minor). The single root cause behind nearly all of them is one unfinished propagation: the ratified 2026-07-14/15 authority was applied to the PRD, architecture, and sprint-status, but never to epics.md and the UX spec. One disciplined reconcile pass over those two documents resolves the planning-readiness portion; product release readiness then rests on Epic 12 delivery and OQ1–OQ10 evidence closure.

---

**Assessed:** 2026-07-19 · **Assessor:** Implementation Readiness workflow (bmad-check-implementation-readiness), run for Administrator · **Supersedes:** `implementation-readiness-report-2026-07-15.md`
