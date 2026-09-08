---
stepsCompleted:
  - step-01-init
  - step-02-discovery
  - step-02b-vision
  - step-02c-executive-summary
  - step-03-success
  - step-04-journeys
  - step-05-domain
  - step-06-innovation
  - step-07-project-type
  - step-01b-continue
  - step-08-scoping
  - step-09-functional
  - step-10-nonfunctional
  - step-11-polish
  - step-12-complete
  - step-v-validation-complete-2026-05-07
  - step-e-edit-applied-2026-05-07
inputDocuments:
  - "_bmad-output/planning-artifacts/product-brief-Hexalith.Folders.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-tenants-integration-for-folder-management-application-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-eventstore-domain-aggregates-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-forgejo-and-github-api-research-2026-05-05.md"
  - "_bmad-output/brainstorming/brainstorming-session-20260505-070846.md"
  - "_bmad-output/planning-artifacts/architecture.md"
  - "_bmad-output/project-context.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06-builds-package-version-centralization.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-081620.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-090742.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-190839.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-193110.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-content-materializer.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-dcp-lane-standup.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-governance-approval-freshness.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-honest-green-gate-baseline.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-idempotency-key-canonicalization.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-memories-facade-dapr-egress.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-nfr-traceability-decoupling.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-seed-backed-read-models.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-08-rest-negative-path-coverage.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-incremental-review.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md"
  - "_bmad-output/planning-artifacts/reconcile-july-2026-synthesis.md"
  - "_bmad-output/planning-artifacts/reconcile-july-2026-delta-review.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-19.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md"
  - "_bmad-output/planning-artifacts/reconcile-architecture-downstream-2026-07-19.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-04.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-04.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-24.md"
  - "_bmad-output/planning-artifacts/planning-story-manifest.yaml"
  - "_bmad-output/planning-artifacts/validation-report.md"
  - "_bmad-output/planning-artifacts/review-rubric.md"
  - "_bmad-output/planning-artifacts/review-adversarial-general.md"
  - "_bmad-output/planning-artifacts/review-post-finalization-drift.md"
documentCounts:
  productBriefs: 1
  research: 3
  brainstorming: 1
  projectDocs: 0
  projectContext: 1
  changeProposals: 22
  readinessReports: 4
  reconciliations: 3
  planningManifests: 1
  validationReports: 1
classification:
  projectType: api_backend
  domain: developer infrastructure / AI workspace storage
  complexity: high
  projectContext: brownfield living contract; greenfield at inception
workflowType: 'prd'
releaseMode: phased
title: 'Product Requirements Document — Hexalith.Folders'
status: final
created: '2026-05-05'
updated: '2026-09-08'
finalized: '2026-07-15'
completedAt: '2026-05-07'
lastEdited: '2026-09-08'
implementationReadiness: not-ready
implementationReadinessAssessedAt: '2026-08-04'
implementationReadinessSource: '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-04.md'
productMvpDecision: durable-repository-round-trip-required
productMvpDecisionRatifiedAt: '2026-07-14'
editHistory:
  - date: '2026-09-08'
    changes: 'Validation-driven update (validation-report 2026-09-08, grade Poor): made FR44 the single caller-visible error taxonomy and removed enumeration-leaking categories from the caller boundary; defined the locked, changes_staged, and dirty lifecycle boundaries, task identity, lock lease rules, file-mutation execution model, single-tenant remote binding, stale-remote commit outcome, and cleaned-up workspace behaviour; added the Actors and Protected-operation-families tables, glossary rows, folder-creation operation graph, and context-query scoping rule; normalized the repository-backed folder noun and role vocabulary; removed silently, where-policy-allows, and subjective acceptance qualifiers; added verifiable clauses to FR8, FR9, FR10, FR17, FR21, FR22, FR33, and FR51 (mirrored into epics.md); completed the C0-C13 exit-criteria table and marked not-yet-created evidence paths; refreshed provenance, readiness posture (2026-08-04), and post-MVP phasing; added the Assumptions and PM Decisions Index recording every inferred value and every decision (Epic 12/13 admission, MVP discard path, tracking conflicts, NFR-bullet lockstep edits) that remains with the PM. A same-day reviewer gate then aligned the taxonomy, permission model, completion rows, task identity, and retention statements with the Contract Spine, the approved C6 matrix, and the approved OQ8 design, and recorded the remaining Spine conflicts as PD10; a second gate pass then reconciled the dirty/stale/terminal-cleanup semantics, merged the safe-denial family with the Spine envelope, restricted file-body reads to the write grant, bootstrapped folder administration, and recorded the C6/C3 transition disagreements as PD11. Stable IDs, status final, and the 73 NFR bullets are unchanged.'
  - date: '2026-07-15'
    changes: 'Reconciled 19 July sprint-change inputs—including approved, applied, pending, proposed, deferred, and no-op sources—under the approved 2026-07-15 authority and delta adjudication; recorded not-ready implementation posture; corrected UJ2, FR56, and OQ8; preserved OQ1-OQ10 and the metadata-token FR58 boundary; retained unratified July 14 audit recommendations for separate PM approval.'
  - date: '2026-07-14'
    changes: 'Reconciled validation findings with approved governance and current product boundaries; clarified contract authority, MVP surfaces, tenant administration, repository binding, incident-mode console, state vocabulary, retention, query limits, idempotency, search semantics, and open release decisions.'
  - date: '2026-05-07'
    changes: 'Validation polish: 11 wording edits removing subjective adjective and CQRS pattern leakage. Edit workflow: added Journey 9 (Tenant Administrator folder access and lifecycle); added Journey Requirements Summary bullets; added FR-section objective cross-reference; added Deferred Quantitative Targets — Architecture Exit Criteria subsection (C1–C5).'
---

# Product Requirements Document — Hexalith.Folders

**Author:** Jerome
**Date:** 2026-05-05

## Executive Summary

Production AI agents increasingly need to work on real project files, but giving them direct filesystem paths, Git remotes, provider credentials, and cleanup responsibility creates tenant isolation, recovery, audit, and provider-coupling risks. Hexalith.Folders provides the workspace control plane for productionizing agentic file work.

Hexalith.Folders gives chatbot developers a tenant-scoped, task-oriented API for preparing workspaces, acquiring task-scoped locks, applying governed file changes, querying file context through tree, search, glob, metadata, and partial-read operations, persisting Git-backed work, and exposing interrupted tasks as inspectable, non-terminal state. It centralizes workspace lifecycle, file-operation audit metadata, GitHub and Forgejo provider handling, credential references, and operational state so each chatbot or automation layer does not need to rebuild fragile storage orchestration.

The product serves three primary stakeholder groups. Chatbot developers can persist task output without writing custom filesystem or Git orchestration. Operators can diagnose workspace state before and after agent execution, including stale locks, dirty worktrees, failed provider syncs, incomplete provisioning, and misconfigured workspaces. Tenant administrators can trust that folders, credentials, permissions, and task artifacts remain isolated and auditable across tenants.

### What Makes This Special

Hexalith.Folders is not a generic file manager, Git UI, chatbot interface, agent runtime, prompt orchestration layer, or execution sandbox. Its differentiated value is turning project storage into a task lifecycle API: prepare a workspace, claim exclusive work, apply bounded changes, persist results, expose relevant context, and leave every failure inspectable.

The core insight is that production AI agents need narrow, task-oriented workspace primitives rather than direct ownership of filesystem paths, credentials, Git remotes, and recovery workflows. The product wins when chatbot builders stop writing bespoke workspace orchestration and delegate file isolation, Git persistence, locking, auditability, context access, and failure visibility to one tenant-aware service.

Users choose Hexalith.Folders over improvised temp folders, direct Git CLI automation, shared provider tokens, unrestricted file traversal, and cleanup scripts when they need agentic file work to be observable, recoverable, auditable, and tenant-scoped.

## Project Classification

**Project Type:** API/backend service module with a generated .NET (C#) SDK and supporting CLI and MCP surfaces.

**Domain:** Developer infrastructure and AI workspace storage.

**Complexity:** High. The complexity comes from multi-tenant authorization, event-sourced aggregates, GitHub and Forgejo provider support, repository-backed storage, workspace locking, metadata-only audit events, file context queries, and operational state projections.

**Project Context:** Greenfield at inception within a mature Hexalith ecosystem; now maintained as a brownfield living product contract. Current repository contracts and approved governance artifacts take precedence over historical delivery-status prose, while this PRD remains authoritative for product intent, release scope, actors, and user-visible outcomes.

## MVP Contract Summary

The MVP proves one job: an AI agent can safely prepare, modify, and commit files in a tenant-scoped repository workspace through one canonical contract.

The canonical lifecycle is provider readiness, repository-backed folder creation or binding, workspace preparation, task-scoped lock acquisition, governed file changes, commit, context query, status inspection, and metadata-only audit. Product intent and release scope are defined here. The OpenAPI 3.1 Contract Spine is the canonical machine-readable operation and schema contract; the generated SDK is the typed canonical client; REST is the required public runtime transport; CLI and MCP wrap the SDK. For every C13-supported surface cell, the four contract surfaces preserve the same states, authorization checks, idempotency rules, and error taxonomy; a diagnostic operation may be explicitly not applicable under the product-approved C13 rule. The console is a read-only diagnostic surface, not an independent command model.

Authority conflicts fail the release gate rather than being resolved at runtime. This PRD prevails for product intent, actors, scope, safety invariants, and user-visible outcomes; the Contract Spine prevails for operation names, wire schemas, and closed error fields. A conflict requires both artifacts to be reconciled and re-approved before shipping. The generated SDK, REST-emitted schema, CLI, MCP, parity oracle, and tests have no authority to override either source; any drift is a failing conformance defect. The identifiers this PRD spells out are product-owned exceptions to Contract Spine authority and must appear verbatim in the Spine: the workspace lifecycle and lock-state values, the error fields `category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, and `details.visibility`, the code `idempotency_key_expired`, the flag `isTruncated`, and the task-identity header `X-Hexalith-Task-Id`. Every other field, code, operation name, and permission identifier is illustrative unless an FR names it.

The MVP is repository-backed first. It supports creating a repository-backed folder and binding a pre-created provider repository that passes readiness, access, duplicate-binding, and branch/ref-policy checks. Importing unmanaged local working trees or folders, migration assistance, local-first promotion, history rewriting, and repair of pre-existing dirty state are post-MVP.

The product has one non-negotiable content boundary: file contents, diffs, generated context payloads, provider tokens, credential values, secret material, and unauthorized resource existence must not appear in events, logs, traces, metrics, audit records, console views, provider diagnostics, or error responses.

### Current Delivery Posture

This PRD is final as a product contract; the implementation is not ready for product release as of the most recent implementation-readiness assessment (2026-08-04; the 2026-07-15 and 2026-07-19 assessments reached the same verdict). The approvals of 2026-07-20 and 2026-08-04 made the durable repository-backed round trip (Epic 12) and security/operations hardening (Epic 13) release-blocking in the governance record; this PRD has not yet admitted them to its release inventory, and the Assumptions and PM Decisions Index records that pending decision (PD1, PD3). Completed contract, adapter, authorization, governance, accessibility, topology, and fail-safe foundations remain valid increments, but they do not complete or release the product MVP. Release remains blocked until the durable repository-backed lifecycle is complete and every Open Release Item is closed with approved production evidence. Safe-empty, seed-only, unavailable, no-op, fake-backed, numerically mapped, structural, or documentation-only evidence may prove safety or contract shape but does not prove positive runtime capability.

## Success Criteria

### User Success

Chatbot developers succeed when they can complete the core repository-backed workflow without writing custom filesystem, Git, provider-credential, or cleanup orchestration: create or bind a repository-backed folder, prepare and lock a workspace, add/change/remove files, produce a provider-confirmed durable commit, and query final workspace and audit state.

Tenant-scoped operators succeed when they can inspect whether a workspace is usable before and after agent execution. The read-only console must expose provider readiness, workspace readiness, lock state, dirty/uncommitted state, last confirmed commit, failed operation state, and sync/provider status without direct filesystem inspection. MVP operators work only inside an explicitly authorized tenant and folder scope; global cross-tenant browsing and break-glass access are not MVP capabilities.

AI tool integrations succeed when the same core lifecycle is available through the Contract Spine, REST, generated SDK, CLI, and MCP with consistent authorization, idempotency, states, errors, and audit outcomes.

### Business Success

Three-month success means at least two independent Hexalith agent or chatbot integrations complete the canonical workflow through the public contract without direct filesystem paths, Git CLI orchestration, provider credentials, or Folders implementation knowledge.

Twelve-month success means at least 80% of newly approved Hexalith agentic file-work integrations use Hexalith.Folders as their workspace persistence layer rather than introducing bespoke temporary-folder, Git, token, or cleanup orchestration. The denominator is the set of integrations recorded as approved in the Hexalith integration register (to be created; OQ10) during the twelve months after product release, and an integration counts as using Hexalith.Folders when it holds no direct provider credential or filesystem path for agent file work. This target is reviewed after the first 90 days of production evidence. [ASSUMPTION A2: the integration register is the approval body and counting source; the two-integration and 80% thresholds are PM-set and have no governance record.]

### Technical Success

The MVP must prove the complete repository workflow for both supported providers and all four contract surfaces: create or bind a repository, prepare and lock, add/change/remove files, confirm the durable commit outcome, query context/status, and expose metadata-only audit.

The highest-priority technical measures are zero cross-tenant access leaks and deterministic completion evidence. Cross-tenant authorization failures must be caught before any file, workspace, credential, repository, lock, commit, provider, audit, or context resource is touched or inferred.

Provider readiness checks must prevent avoidable runtime failures by validating provider configuration, credential reference availability, required GitHub/Forgejo capabilities, repository provisioning readiness, and default branch policy before repository-backed folder creation.

The Contract Spine, REST, generated SDK, CLI, and MCP must expose the same core lifecycle and preserve consistent authorization, audit, error handling, idempotency, and state semantics.

### Measurable Outcomes

- **SM1 — Canonical lifecycle:** 100% of declared release-calibration scenarios pass for GitHub and Forgejo through REST, SDK, CLI, and MCP, including create and bind variants, failure paths, and durable commit confirmation.
- **SM2 — Isolation:** zero tolerated cross-tenant leaks across commands, queries, errors, events, logs, metrics, projections, caches, locks, working paths, provider access, audit, and context results.
- **SM3 — Task completion:** at least 95% [ASSUMPTION A1] of authorized canonical lifecycle runs in the 30-day release-calibration cohort reach the committed state; explicitly injected provider outages and policy denials are excluded from the numerator and denominator but must end in their expected safe state.
- **SM4 — Latency and freshness:** command acknowledgement is at most 1 second p95; bounded status/audit summaries are at most 500 ms p95; bounded context queries are at most 2 seconds p95 with a 2-second hard execution limit; commit-to-status visibility lag is at most 500 ms in the approved release-calibration path.
- **SM5 — Capacity:** the release gate sustains 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent agent tasks per tenant, and at least 1 lifecycle operation per second without cross-scope interference.
- **SM6 — Diagnostic completeness:** 100% of injected lifecycle failures expose state, safe cause category, retryability, client action, correlation ID, and metadata-only audit evidence; no accepted task is left without an inspectable state.
- **SM7 — Adoption:** within 3 months, at least two independent first-party integrations use the public lifecycle without bespoke workspace orchestration; within 12 months, at least 80% of integrations approved in the Hexalith integration register use Hexalith.Folders with no direct provider credential or filesystem path (Business Success defines the denominator). [ASSUMPTION A2]
- **SM8 — Agent context effectiveness:** 100% of approved context benchmark tasks retrieve the expected authorized tree/metadata/search/glob/range evidence within C4 limits and allow the agent to identify the intended edit target without unrestricted repository browsing.

Counter-metrics prevent a hollow win:

- **CM1 — Unsafe completion:** zero tasks may count as successful when authorization is stale, the lock is invalid, the remote commit is unconfirmed, or protected data is exposed.
- **CM2 — Recovery burden:** no more than 5% [ASSUMPTION A1] of accepted release-calibration tasks may end in dirty, unknown-provider-outcome, or reconciliation-required states; every such task must remain inspectable.
- **CM3 — Operator burden:** no more than 10% [ASSUMPTION A1] of accepted release-calibration tasks may require manual intervention, excluding deliberate failure injection.
- **CM4 — Surface drift:** zero current Contract Spine operations may lack the required C13 parity declaration, and no adapter may report a materially different authorization, state, error, or audit outcome for the same scenario.
- **CM5 — Hollow adoption:** zero integrations counted in SM7 may perform agent file writes outside Hexalith.Folders; an integration that reads context through Folders but writes files or commits through its own provider access does not count as adopted.

The canonical release-calibration plan at `docs/exit-criteria/release-calibration-plan.md` (to be created; OQ10) must freeze each metric's population, exclusions, environment, scenario set, measurement method, evidence owner, and approval record before results are accepted. OQ10 tracks creation and approval of that plan. The product-level population rules are fixed here and the plan may only add measurement mechanics: SM1, SM3, SM6, CM2, and CM3 count accepted tasks and scenarios in the approved release-calibration environment (C1/C5 units), exclude deliberately injected failures from numerators and denominators while requiring them to end in their expected safe state, and are smoke thresholds over a designed cohort rather than field statistics, except that non-resumable tasks (PD2) are always counted for CM2 and CM3; SM7 is the only adoption measure and its denominator is the integration register named in Business Success.

## Product Scope

### MVP - Minimum Viable Product

The MVP includes the repository-backed task workflow: provider readiness, logical folder creation, provider-backed repository creation or binding, workspace preparation, locking, add/change/remove file operations, provider-confirmed durable commit, context/status queries, and metadata-only audit.

The MVP is repository-backed first. Binding a pre-created provider repository is supported when readiness, authorization, duplicate-binding, and branch/ref policy pass. Importing an unmanaged local working tree or folder, migration assistance, local-first promotion, history rewriting, and repair of pre-existing dirty state are excluded.

This is an intentional scope change from the Product Brief. The Product Brief included limited local storage and local-to-Git upgrade in MVP; the PRD narrows MVP to the repository-backed workflow so the first release can prove tenant isolation, provider readiness, task locking, file operations, commit, status, and audit before expanding storage modes.

The MVP includes a tenant-scoped, read-only operations console focused on trust signals: provider readiness, workspace readiness, lock state, dirty state, commit state, failed operation state, and provider/sync status. Normal views use projections. When projections are degraded, an explicitly authorized incident view may expose bounded metadata-only event evidence with a persistent degraded-state warning, the last projection checkpoint, correlation/time-window context, and C9 redaction; it remains read-only and never exposes content, credentials, diffs, or repair controls. The alternative considered was leaving the console unavailable whenever projections degrade. It was rejected because projection degradation is exactly when an operator needs evidence; the accepted cost is a second bounded read path, the incident-admin permission model, and the OQ9 evidence obligation, and the fourth innovation risk (console scope creep) is contained by keeping that path read-only and evidence-only.

The MVP includes the Contract Spine, REST transport, generated SDK, CLI, and MCP for the core lifecycle, plus the diagnostic console. CLI and MCP must preserve the generated SDK and Contract Spine semantics rather than creating independent models.

The MVP must enforce tenant-scoped authorization and must prevent cross-tenant file, credential, workspace, and repository access.

### Growth Features (Post-MVP)

Post-MVP growth expands reliability and operational depth after the core workflow is proven. The single post-MVP inventory is the phased list in Post-MVP Features; this section does not carry a second list. Wire-preserving platform-alignment engineering that removes local copies of shared Hexalith platform capabilities (Epic 11, platform alignment) is not a product feature: it runs in parallel with MVP delivery as a technical enabler and is excluded from product-completion metrics.

### Vision (Future)

The long-term vision is the end state SM7 implies: every approved Hexalith agentic file-work integration uses Hexalith.Folders, and no first-party integration holds a provider credential, a filesystem path, or its own Git orchestration for agent file work. Tenant administrators then reason about agent file work through one lifecycle, one audit model, and one set of trust signals, whichever chatbot, provider, or surface produced it.

## User Journeys

### UJ1: Developer Proves Agentic File Work Is Production-Ready

Nadia is building a chatbot that must work on real project files for tenant customers. Her concern is not whether she can script Git; she already can. Her concern is whether she can let an AI agent touch project files without creating tenant leaks, unrecoverable partial changes, hidden dirty state, or provider-specific Git logic inside the chatbot.

She starts with a provider readiness check for the tenant organization. The system confirms that the provider binding exists, the credential reference resolves, repository creation is supported, the default branch policy is valid, and the tenant has permission to create the repository. Nadia then creates a repository-backed folder, prepares the workspace, and runs the core task flow through CLI or MCP: add files, change files, remove files, commit the change set, and query workspace status.

The value moment is not repository creation by itself. It is the clean final state: the workspace is `committed`, no hidden dirty state remains, the commit SHA is visible, the changed paths are traceable to tenant/folder/task metadata, and the chatbot did not need direct filesystem paths, provider credentials, or Git CLI orchestration.

This journey reveals requirements for provider readiness checks, repository-backed folder creation, workspace preparation, governed file operations, commit support, workspace status queries, CLI/MCP access, task/correlation metadata, and clean committed-state reporting.

### UJ2: Tenant Administrator Establishes Provider Readiness

*Revised 2026-07-15 (tenant-policy ownership) and 2026-09-08 (persona merged with UJ6/UJ9; operator boundary made absolute).*

Elise, the tenant administrator who also appears in UJ6 and UJ9, is responsible for making her tenant ready for repository-backed agent work. Before an agent can create or bind a repository-backed folder, she configures the provider binding, credential reference, repository naming and default-ref policy, and minimum capability policy for that tenant.

A platform engineer acting as a tenant-scoped operator may validate and diagnose the resulting readiness but may not change Elise's tenant policy. If a credential reference is missing, permissions are insufficient, the provider is unavailable, or repository/default-ref policy is invalid, readiness reports a stable safe reason, retryability, remediation category, and correlation ID without exposing secrets or hidden resources. An operator-only, wrong-tenant, stale, revoked, or otherwise unauthorized attempt fails before configuration or protected-state observation.

The value moment is controlled readiness before runtime: Elise owns tenant policy, while the platform engineer can prove whether the configured tenant is ready without acquiring tenant-policy mutation authority.

This journey reveals requirements for tenant-owned provider and credential-reference configuration, repository/default-ref and capability policy, scoped readiness validation, safe readiness diagnostics with reason, retryability, remediation category, and correlation ID, and fail-closed authority boundaries.

### UJ3: Agent Task Is Interrupted and Leaves Inspectable State

Asha, an AI agent running Nadia's tenant-scoped task, starts work against an existing repository-backed folder. She prepares the workspace, acquires a task-scoped lock, applies several file changes, and then the task is interrupted before commit.

Without Hexalith.Folders, the team would be left with uncertain state: partial files, no reliable lock owner, no clear task correlation, and no trustworthy recovery signal. With Hexalith.Folders, the workspace enters an inspectable non-terminal state. Its workspace lifecycle is `dirty` and it is associated with the interrupted task; its lock state is `locked` until the lease lapses and `expired` afterwards. The changed file list is available as metadata, the last successful operation is visible, and the absence of a commit is explicit.

The MVP does not repair, discard, or commit the changes. It makes the state understandable and prevents another task from overwriting the workspace accidentally. The only MVP exit is Asha's own task: presenting the same task identity under fresh authorization, it may re-acquire a new lock instance, finish its mutations, and issue its single commit. No MVP operation discards staged changes or releases a lock while changes remain; the one way staged work is lost in MVP is platform cleanup after a task-terminal `failed` or `inaccessible` closure, seven days after the closure (FR30). A task that can never resume from `dirty` therefore leaves that repository/ref serialized for the tenant until a post-MVP repair operation exists; the Assumptions and PM Decisions Index records this as PD2. Post-MVP repair commands may support retrying the commit, discarding changes, rebuilding the cache, or releasing stale locks.

This journey reveals requirements for task-scoped locks, lock ownership metadata, dirty workspace detection, interrupted task status, operation timeline, changed-path metadata, safe blocked state, and post-MVP repair workflows.

### UJ4: Concurrent Agent Is Denied by Workspace Lock

Nadia starts Asha on a tenant folder, then a second agent targets the same effective repository/ref through another surface. Asha's task prepares the workspace and acquires the lock. The second agent then tries to prepare, write, or commit against the same serializing identity through MCP or API.

The system rejects the second task with a deterministic lock response. The denial includes safe metadata: lock state, lock owner/task reference if authorized, lock age, and retry eligibility policy. It does not allow mixed writes, overlapping commits, or silent lock takeover.

The value moment is trust under pressure. Nadia and the operator can see that lock contention did not corrupt the workspace and did not produce a mixed commit.

This journey reveals requirements for exclusive workspace locking, consistent lock denial across API/CLI/MCP, lock status visibility, retry/idempotency policy, no-lost-update behavior, and structured lock error responses.

### UJ5: Tenant-Scoped Operator Diagnoses Provider or Credential Failure

Marcus maintains the Hexalith platform. A tenant reports that an agent task cannot complete. Marcus enters an explicitly authorized tenant and folder scope, then opens the read-only operations console. MVP does not permit global cross-tenant search or unscoped break-glass browsing.

The console does not present a flat wall of infrastructure facts. It answers three questions first: what is broken, who or what is affected, and what can safely happen next. Marcus sees that the workspace is `failed`, the provider readiness check now fails because the credential reference no longer has required permissions, and the last commit attempt did not succeed. The console shows supporting evidence: tenant, folder, provider binding, credential reference identifier, failed operation type, last successful operation, lock/dirty state, and timestamp.

The console remains read-only in MVP. It does not reveal credential material, expose file contents, mutate workspace state, or hide repair actions behind undocumented controls. Marcus can diagnose and escalate or correct configuration through the proper provider/tenant administration path.

This journey reveals requirements for a read-only operations console, workspace trust surface, primary diagnosis, provider/credential failure states, failed operation projection, secret-safe status display, no mutation path, and an explicit escalation posture.

### UJ6: Tenant Administrator Proves Cross-Tenant Isolation

Elise administers a tenant organization that wants to enable agentic file work. Her concern is whether another tenant, agent, or integration surface can discover or touch her tenant's workspaces, provider bindings, credential references, commits, locks, or audit records.

She validates isolation with concrete evidence. A wrong-tenant request attempts to query workspace status, inspect provider configuration, prepare a workspace, acquire a lock, write a file, commit changes, and read audit metadata. Each attempt is denied before file, workspace, credential, repository, or commit access occurs. The denial shape is safe and does not leak whether unauthorized resources exist beyond what policy allows.

The value moment is proof. Elise can show that tenant isolation is enforced across API, CLI, MCP, and console surfaces, and that denial events are captured as metadata-only audit records.

This journey reveals requirements for tenant-scoped authorization, cross-surface authorization parity, safe error shapes, no cross-tenant enumeration, credential reference isolation, denial audit events, and zero tolerated cross-tenant access leaks.

### UJ7: MCP/CLI/API Consumer Sees Cross-Surface Parity

Nadia, the developer from UJ1, starts a workflow in the CLI to validate provider readiness and create a folder. Later, her AI tool continues the same task through MCP, presenting the same task identity, to prepare the workspace, write files, and request a commit. Marcus inspects the same workspace through the console, while Nadia's service integration queries status through the API.

Every surface reports the same truth. The workspace state, error categories, tenant authorization behavior, lock behavior, idempotency behavior, task/correlation metadata, and audit outcomes are consistent. If CLI says the workspace is `dirty`, MCP and API report `dirty`. If API returns a lock denial, MCP receives the same category of denial.

The value moment is operational confidence. Different entry points do not create different product semantics.

This journey reveals requirements for one canonical workflow contract, thin API/CLI/MCP adapters, shared status model, shared structured errors, shared authorization enforcement, shared audit metadata, and parity tests for MVP workflow commands.

### UJ8: Audit Reviewer Reconstructs What Happened Without File Contents

Priya, a security reviewer, investigates a customer incident after an agent task changed project files. She needs to answer who or what changed which paths, in which tenant and folder, under which task or correlation ID, with what outcome, and whether a durable Git commit was produced.

The audit view shows metadata only: tenant ID, folder ID, actor/client type, task/correlation ID, operation type, path metadata, lock lifecycle, status transitions, provider reference, commit SHA when available, timestamps, and denial events. It does not show file contents, provider tokens, credential material, or secrets.

The value moment is accountable traceability without data exposure. The reviewer can reconstruct the operational story and prove whether the agent task completed, failed, was denied, or left dirty state.

This journey reveals requirements for metadata-only audit events, audit projections, path-level change metadata, commit SHA capture, lock lifecycle audit, denial event capture, secret/file-content exclusion, and incident-support queries.

### UJ9: Tenant Administrator Manages Folder Access and Lifecycle

*Added 2026-05-07.*

Elise, the tenant administrator from UJ6, returns to the day-to-day operation of her tenant. Her concern is no longer “Can isolation be proven?” but “Can I run the tenant cleanly?” She needs to grant folder access to users, groups, roles, and delegated service agents as her teams onboard new chatbots, retire old ones, and rotate ownership; inspect effective permissions for a folder so that an audit question (“Who can write here today?”) has a concrete answer; and retire folders whose tasks are finished without erasing the audit evidence that future incident reviews depend on.

Elise grants folder access to a new automation team and verifies through the same surface that the grant took effect — the effective-permissions view shows the grant, the actor identity, and the operation-family scope. Months later, when a chatbot project is retired, Elise archives the folder. The folder enters a clearly archived lifecycle state, mutating commands are denied with a stable error, but the metadata-only audit trail, lock lifecycle history, last commit reference, and operation timeline remain queryable for each field's C3 retention class (FR14). Audit records older than the 400-day read-model class are still served, for the seven-year audit class, from the retained audit records through the same authorized audit operations rather than reported as missing [ASSUMPTION A14]. No file contents, provider tokens, or credential material are revealed by the archived view; the same denial and isolation guarantees that protect active folders also protect archived ones.

The value moment is operational confidence. Elise can run her tenant — granting and revoking folder access, retiring folders, and answering audit questions — using the same product semantics, cross-surface parity, and metadata-only audit posture that the rest of the canonical workflow already enforces.

This journey reveals requirements for tenant-administrator folder access grant and revoke (FR5), effective-permissions inspection (FR6), folder lifecycle and archive (FR11–FR13), audit and status preservation for archived folders (FR14), denial of mutating operations on archived folders, C3-bound audit visibility, and cross-surface parity for tenant-administration commands and queries.

### Journey Requirements Summary

The journeys reveal these required capability areas:

- Canonical workspace lifecycle and separate lock-state vocabularies as defined in Workspace State and Concurrency.
- Provider readiness checks before repository-backed folder creation.
- Stable provider readiness reason codes drawn from the FR44 taxonomy and reported with the FR17 diagnostic fields.
- Tenant-scoped organization/provider configuration and credential references.
- Repository-backed folder creation or binding with narrow MVP provider behavior.
- Workspace preparation and task-scoped locking.
- Deterministic lock contention behavior across API, CLI, and MCP.
- Idempotency for every mutating Contract Spine operation; while the record is unexpired within its declared retention tier, equivalent replays preserve one logical result and conflicting intent is rejected. An expired key returns `idempotency_key_expired` and never executes automatically as a new request.
- Governed file add/change/remove operations with workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, and case-collision handling.
- Commit support for repository-backed folders with tenant, actor, task/correlation, changed-path, and commit reference metadata.
- File context queries through tree, search, glob, metadata, and partial reads.
- Workspace status queries and trust projections.
- Read-only operations console for readiness, lock state, dirty state, failed operation, last commit, credential reference status, and provider/sync status.
- No mutation path, credential reveal, or file-content browsing in the MVP read-only console.
- Metadata-only audit events, including successful operations, failed operations, status transitions, lock lifecycle, commit references, and authorization denials.
- Cross-tenant access prevention before file, workspace, credential, repository, lock, commit, provider, or audit access.
- Safe error shapes that avoid unauthorized resource enumeration.
- Consistent API, CLI, and MCP command/query semantics.
- Tenant-administrator folder access grant and revoke for users, groups, roles, and delegated service agents, with effective-permissions inspection.
- Folder lifecycle including archive with C3-bound, metadata-only audit and status visibility for archived folders.
- Provider contract tests for GitHub and Forgejo readiness, repository creation, file/commit workflows, credential failures, permission failures, conflicts, rate limits, timeouts, and provider drift.
- Future repair workflows for interrupted tasks, stale locks, dirty workspaces, provider sync failures, and drift, explicitly outside the MVP read-only console.

## Innovation & Novel Patterns

### Detected Innovation Areas

Hexalith.Folders introduces two main innovation patterns within the Hexalith ecosystem.

The first is an AI-native task lifecycle workspace API. Instead of exposing folders as raw filesystem paths or treating Git as a caller-owned concern, the product models agentic file work as a managed task flow: prepare a workspace, acquire a lock, apply bounded file changes, commit Git-backed results, query context, inspect state, and expose interruption as inspectable state.

The second is a canonical workspace control plane exposed through API, CLI, and MCP surfaces. These surfaces must not become separate product models. They share the same command/query semantics, authorization behavior, workspace states, error categories, idempotency rules, and audit metadata.

A supporting innovation is the workspace trust surface. Hexalith.Folders makes operational states such as `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible` first-class product signals. This lets developers, operators, tenant administrators, and AI tools reason about whether a workspace can be trusted before, during, and after agent execution.

### Market Context & Competitive Landscape

The innovation is positioned primarily inside Hexalith. The comparison point is not a mature external product category; it is the internal pattern Hexalith.Folders is replacing: improvised temporary folders, direct Git CLI calls, scattered provider tokens, inconsistent cleanup logic, and chatbot-specific workspace orchestration.

Within Hexalith, Hexalith.Folders should become the common workspace persistence boundary for AI agents. Its value comes from standardizing tenant-scoped file work, Git-backed persistence, readiness checks, locking, status visibility, and audit metadata behind one product model.

### Validation Approach

The innovation is validated if the MVP proves that a chatbot or AI tool can complete the repository-backed task flow through the canonical contract: provider readiness, repository-backed folder creation, workspace preparation, task lock, add/change/remove files, commit, and status query.

The API, CLI, and MCP surfaces validate the same workflow behavior rather than three separate implementations. Validation confirms consistent workspace states, authorization checks, structured errors, idempotency behavior, and metadata-only audit across all surfaces.

The workspace trust model is validated if operators can understand key states without inspecting the filesystem or Git provider manually: whether the workspace is ready, locked, dirty, committed, failed, or inaccessible, or whether provider readiness failed before a workspace existed.

### Risk Mitigation

The main innovation risk is overbuilding the control plane before proving the core file workflow. If evidence shows that the AI-native task lifecycle is too heavy, any reduction requires a new scope decision approved by PM and governance and cannot ship as this MVP. A revised scope must still preserve tenant isolation, readiness, locking, idempotency, failure visibility, metadata-only audit, durable provider confirmation, and required cross-surface semantics.

The second risk is surface divergence. API, CLI, and MCP could drift into different semantics. Mitigation is one canonical workflow contract with CLI and MCP as adapters over the same command/query model.

The third risk is provider abstraction leakage. GitHub and Forgejo support requires explicit capability checks and provider contract tests rather than assumed API compatibility.

The fourth risk is scope creep from the read-only operations console into a repair or Git administration console. MVP keeps the console diagnostic-only, with repair workflows deferred until the workspace state model is proven.

## API Backend Specific Requirements

### Project-Type Overview

Hexalith.Folders is an API/backend service module with REST API, CLI, MCP, SDK, projection, and read-only console surfaces. These surfaces must expose one canonical workspace workflow contract rather than separate product models.

The product is a tenant-scoped workspace control plane for agentic file work. It is not a content store, Git implementation, identity provider, generic Git provider management platform, prompt orchestration layer, execution sandbox, or repair console.

The MVP must support the repository-backed task flow: validate provider readiness, create or bind a repository-backed folder, prepare a workspace, acquire a task lock, add/change/remove files, commit Git-backed changes, query workspace and file context, inspect metadata-only audit, and expose diagnostic state through the read-only console.

### Architectural Boundaries

Hexalith.Tenants remains the source of truth for tenant identity, tenant lifecycle, and tenant membership. Hexalith.EventStore provides command, aggregate, event, projection, query, cursor, read-model, and domain-service mechanics where those mechanics are platform-owned. Hexalith.Commons, Hexalith.FrontComposer, and Hexalith.Memories provide shared boilerplate for cross-module helpers, UI shell behavior, and search-index integration where applicable. Hexalith.Folders owns folder-specific policy, folder ACLs, provider binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.

Hexalith.Folders owns intent, policy, state, and audit. Git providers own provider-specific repository and Git mechanics behind narrow provider ports. File contents and temporary working-copy material must remain outside EventStore; events, logs, projections, traces, and console responses must never contain file contents, provider tokens, credential material, or secrets.

Required provider ports include readiness validation, repository creation or binding, workspace preparation, governed file-operation application, Git-backed commit, provider status query, and cleanup/expiration support where needed.

### Public Surfaces

The OpenAPI 3.1 Contract Spine is the canonical machine-readable operation and schema contract. REST is the required public runtime transport and must emit a versioned contract that validates against that spine. The generated SDK is the typed canonical client; CLI and MCP wrap the SDK and preserve Contract Spine behavior.

REST, generated SDK, CLI, and MCP are required current-release contract surfaces for the core lifecycle. They must preserve the same workspace states, authorization checks, idempotency rules, lock behavior, structured error categories, correlation metadata, and audit outcomes. The console is query-only and is not part of the mutation-parity surface.

C13 is the single surface-coverage denominator. Every Contract Spine operation has one C13 row declaring required support for REST, SDK, CLI, and MCP. Core lifecycle and tenant-administration contract operations are required on all four surfaces. A diagnostic/read-model operation may be marked not applicable for CLI or MCP only through an explicit product-approved C13 declaration with rationale; no operation may be silently absent, and every supported cell must pass behavioral parity.

Release acceptance freezes an immutable, versioned snapshot containing the Contract Spine version and digest plus the matching C13 inventory version and digest. A breaking operation or schema change, or any support-cell or rationale change, after approval invalidates prior parity evidence for the affected cells and requires a new reviewed snapshot. An additive change also requires a new reviewed snapshot but reopens only the open release item whose evidence it changes, not every item [ASSUMPTION A11].

The read-only operations console must normally consume projections. When projections are degraded, an authorized incident view may expose bounded event-stream evidence only to an actor holding the incident-admin permission and the normal tenant/folder access. The view must remain metadata-only and read-only, enforce C9 redaction, show a persistent degraded-state warning and last projection checkpoint, and expose correlation/time-window context. It must not expose mutation paths, credentials, file contents, diffs, repair controls, or unrestricted event/filesystem browsing.

### Endpoint Specifications

The API is organized around capability groups:

- Provider readiness: validate provider binding, credential reference availability, provider capabilities, branch policy, repository naming policy, and provisioning readiness.
- Folder/repository lifecycle: create a logical folder, create a repository-backed folder, bind an existing pre-created repository, inspect folder/repository binding metadata, and reject duplicate bindings within the tenant while answering cross-tenant attempts with the safe denial.
- Workspace lifecycle: prepare workspace, inspect workspace state, acquire lock, inspect lock, release lock when no changes remain staged (Task and Lock Completion Model), and surface stale or interrupted lock state.
- File operations: add, change, and remove files while enforcing workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding policy, and case-collision handling.
- Commit operations: commit the task's staged change set to the bound repository/ref with tenant, actor, task/correlation, author, message, changed-path, and commit reference metadata.
- Query/status operations: expose workspace state, provider readiness, folder status, file tree, search, glob, metadata, partial reads, dirty state, failed operations, projection status, and last commit.
- Audit operations: expose metadata-only audit records for operations, status transitions, lock lifecycle, commit references, and authorization denials.
- Operations console queries: expose read-only projections for readiness, lock state, dirty state, failed operation, credential reference status, and provider/sync status.

Folder creation has one operation graph. `CreateFolder` creates the logical folder identity with no provider side effect; an unbound logical folder may exist but cannot prepare a workspace. `CreateRepositoryBackedFolder` creates the provider repository for an existing logical folder, and `BindRepository` binds a pre-created repository to it; both re-evaluate readiness inside the mutation before any provider side effect. `ValidateProviderReadiness` is an advisory read-only check whose result is not a persisted precondition, so readiness is never a separate mutation and it rejects idempotency keys like every read.

Context queries are controlled workspace operations, not unrestricted repository browsing. They name the folder and require an authorized workspace identity. A workspace whose lifecycle is `ready`, `locked`, or `committed` is readable by any actor holding the folder `read` grant, because its content equals the last confirmed state; a workspace in `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` is readable only by the lock-owning task. MVP has no server-prepared implicit read-only workspace: some task must have prepared the workspace. Tree, search, glob, metadata, and partial-read responses must enforce tenant, folder ACL, path policy, include/exclude rules, binary handling, byte/range limits, result limits, and secret-safe response rules before ranking, summarization, snippet generation, or response shaping. Denied context queries must produce metadata-only audit evidence with actor, tenant, folder, query type, policy reason, correlation ID, timestamp, and safe error category.

#### Search Families

The two search families have different contracts and may not substitute for each other:

| Search family | Source and searchable fields | Result contract | Content boundary |
| --- | --- | --- | --- |
| Live workspace context search (FR34–FR35) | Current authorized prepared workspace; canonical relative path/name metadata and supported text-file body content after tenant, folder, lock-independent read, path, include/exclude, encoding, binary, and size policy. | Up to 500 matches and 1,048,576 aggregate serialized bytes; authorized C9-wrapped relative path identity, line/byte location, match classification, and bounded live snippet; `isTruncated` when supported limits apply. | Snippets/content are returned only to the authorized caller and must never enter events, logs, traces, metrics, projections, audit, diagnostics, errors, or the shared index. Binary/unsupported content returns metadata or a stable policy result, never guessed text. |
| Indexed metadata-token recall (FR58) | Asynchronously indexed mutation metadata tokens only: type/size classification, media type, folder/organization identity, path-policy outcome, and other approved C9 metadata; no file body or raw path. | Opaque authorized identity, classification/status, and index freshness/availability after current-authority hydration; stale, archived, revoked, unauthorized, and hidden hits are dropped. | Never returns raw path, body, snippet, source URI, or hidden existence. It is not live workspace body search. |

### Command and Query Contract

The following names illustrate core lifecycle capabilities; they are not the coverage denominator. The current OpenAPI Contract Spine defines the complete operation and DTO inventory, and every Contract Spine operation must have exactly one C13 parity declaration:

- `ValidateProviderReadiness`
- `CreateFolder`
- `CreateRepositoryBackedFolder`
- `BindRepository`
- `ConfigureProviderBinding`
- `ConfigureBranchRefPolicy`
- `UpdateFolderAclEntry`
- `GetEffectivePermissions`
- `ArchiveFolder`
- `PrepareWorkspace`
- `LockWorkspace`
- `ReleaseWorkspaceLock`
- `AddFile`
- `ChangeFile`
- `RemoveFile`
- `CommitWorkspace`
- `GetWorkspaceStatus`
- `ListFolderFiles`
- `SearchFolderFiles`
- `ReadFileRange`
- `GetTaskStatus`
- `SearchFolderIndexedFiles`
- `GetFolderIndexingStatus`
- `ListAuditTrail`

Every mutating Contract Spine operation must support an idempotency key, correlation identity, and conflict-detection semantics. The rule applies to current and future mutations, including folder creation, provider/repository binding, branch/ref policy, ACL updates, archive, workspace preparation, lock acquire/release, file add/change/remove, and commit.

Status records are read models with freshness and availability semantics, never raw event mirrors (Glossary); read-model design itself belongs to architecture.

A task may apply many add/change/remove mutations while holding one valid lock, then issue one commit for the task's known staged change set. File mutations do not auto-commit. File mutations are accepted commands (the Spine answers with an operation identity, not a result); they apply to the platform-managed, tenant-scoped workspace working copy and have no Git-provider side effect, so their outcome becomes known without provider involvement and the caller reads it through the operation state exposed by the task and workspace status operations. Readiness validation, workspace preparation, repository creation or binding, and commit are the operations that reach the provider. A multi-file request is accepted or rejected as one command; if the platform cannot confirm the local result of an accepted batch (for example, an interruption mid-batch), the workspace becomes `dirty` and each affected operation is reported as failed or unknown in operation state so the caller can query and re-issue; `unknown_provider_outcome` is reserved for external side effects (create/bind and commit). A second mutation for the same path within one task is rejected with the state-transition-invalid outcome until the first has a known outcome [ASSUMPTION A6]. Commit is denied until every requested mutation has a known, policy-valid outcome. MVP rename is an add-plus-remove pair within this same task and commit boundary.

Each task may produce at most one successful durable commit. While its commit-idempotency record remains unexpired within the C3 tier, an equivalent replay returns the same logical result; after expiry the old key returns `idempotency_key_expired` without execution. Any later non-replay commit or mutation for the task-terminal task is denied.

**Task identity.** A task is identified by the caller-provided task identity (`X-Hexalith-Task-Id`), bound at workspace preparation to the authenticated principal, its delegation scope, the tenant, the folder, and the workspace. "The same task" means the same task identity presented, under fresh authorization, by the binding principal or by a delegated service agent whose delegation resolves to that binding principal, so in UJ7 Nadia's CLI session and her AI tool are the same task. Before preparation the header is correlation only: readiness and folder creation carry it but bind nothing, and one task identity binds to at most one workspace. The task identity is a correlation key, not a bearer credential: presenting it alone never grants lock ownership, and an unrelated principal presenting a known task identity is denied with the safe result. A workspace belongs to the task that prepared it for the workspace's lifetime; another task must prepare its own workspace, and its lock request on that workspace is a lock conflict. On task-scoped mutations the header must match the workspace's bound task; on tenant-administration operations and on reads by non-task actors it is correlation only and is never validated against a binding; a second `PrepareWorkspace` carrying an already-bound task identity is an idempotent replay when its intent is equivalent and an idempotency conflict otherwise. Processes that share one task identity are one task by definition: the delegating principal is responsible for serializing them, the platform serializes their mutations per path (A6), and the task still produces exactly one commit. Lock release and continuation additionally require the non-secret lock-ownership proof scoped to tenant, folder, workspace, task, and lock. The same task may continue from any surface (UJ7), and after process loss it resumes by presenting its task identity under fresh authorization and re-acquiring a new lock instance under the reconciliation rules below. Task identity participates in idempotency equivalence for task-scoped mutations. [ASSUMPTION A5: principal/delegation binding of the task identity at preparation and the one-workspace-per-task rule.]

#### Task and Lock Completion Model

| Outcome | Workspace lifecycle | Lock state | Permitted next action |
| --- | --- | --- | --- |
| Lock acquired, no mutation applied yet | `locked` | `locked` | The same task may apply mutations or release; other tasks receive the lock-conflict denial. |
| Explicit release before any staged change | `ready` | `unlocked` | Task closes without a commit; later work requires a new task. |
| Lease lapses before any staged change | `dirty` | `expired`, then `stale` after the C7 threshold | The approved C6 matrix orphans the workspace rather than returning it to `ready`; while `expired` the originating task may re-acquire a new lock instance and continue, or release, which returns the workspace to `ready`/`unlocked`; at `stale` the platform returns the clean workspace to `ready`/`unlocked` (A21); any other task is denied until then. |
| One or more known mutations applied under a valid lock | `changes_staged` | `locked` | The same freshly authorized task may continue mutations or issue its single commit. Release/takeover is denied while changes remain. |
| Lease lapses while changes remain | `dirty` | `expired`, then `stale` after the C7 threshold | Only the same task, under fresh authorization and the same task identity, may re-acquire a new lock instance and continue or commit; no other task may take over, and no MVP operation discards the changes. |
| Authority revoked while changes remain | `inaccessible` | `revoked` | Everything is denied while authority is absent. `inaccessible` is task-terminal, so platform cleanup retires the working files after the C3 seven-day window; if the originating task's authority is restored inside that window, fresh authorization lets it re-acquire a new lock instance and the workspace returns to `dirty` with its changes intact, and after cleanup it stays `inaccessible` with content retired [ASSUMPTION A18]. |
| Bound ref advanced since preparation, detected at commit | `dirty` | `locked` | Commit is denied with the branch/ref-conflict result and no remote side effect; MVP performs no automatic rebase or history rewrite, so the same task may only retry after the platform confirms the staged change set still applies cleanly, otherwise it remains `dirty` for human escalation [ASSUMPTION A9]. |
| Provider-confirmed durable commit | `committed` | `unlocked` | Task is task-terminal; while the commit-idempotency record is unexpired, equivalent replay returns the same commit result. After expiry, the old key returns `idempotency_key_expired` without execution. |
| Known retryable commit failure with no remote side effect | `dirty` | `locked` | The same task may refresh state/authorization and retry with a new key; while its record is unexpired, replaying the old key returns the old logical result. After expiry, the old key returns `idempotency_key_expired` without execution. |
| Known non-retryable provider failure (`failed`) or authorization failure (`inaccessible`) | `failed` or `inaccessible` | `revoked` | Mutations, commit, release, and takeover are denied; metadata-only status/audit remains available. |
| Unconfirmed external side effect | `unknown_provider_outcome` | `revoked` | Bounded automatic evidence checks run without repeating the mutation. Confirmed success/failure follows the corresponding known row; exhausted or conflicting evidence enters `reconciliation_required`. |
| Automatic evidence exhausted or conflicting | `reconciliation_required` | `revoked` | Only read-only evidence collection and human escalation are allowed. Mutations, blind retry, release, and takeover remain denied until authoritative evidence selects a known transition. |

Automatic evidence checking begins immediately after the unconfirmed result, performs no more than five read-only evidence checks, and ends within 15 minutes. If it cannot select a known transition within that budget, the state becomes `reconciliation_required`. Provider compatibility evidence may define a shorter bound but may not extend it without a new product decision.

MVP reconciliation determines outcome for every external side-effect family; it does not silently repeat a mutation, discard changes, rewrite history, or take over a task:

- repository creation/binding checks the provider's canonical repository identity, requested binding intent, and provider-side evidence tying a newly created repository to the originating operation. A matching repository completes a create result only when that ownership evidence matches; otherwise it may be accepted only through the separately authorized pre-created-repository binding flow. Confirmed absence/no side effect permits a fresh authorized attempt with a new key, and conflicting identity/evidence requires human escalation;
- file mutation checks each requested operation identity and its intended content-hash/policy evidence. Confirmed applied or not-applied outcomes become a known dirty change set. After fresh authorization, the same task may acquire a new lock instance under the same canonical serializing identity to complete known missing operations. Commit remains denied while any operation is unknown;
- commit checks the authoritative remote/ref for the intended commit evidence; confirmation transitions to committed/unlocked, confirmation of no remote update transitions to dirty and permits the same task to acquire a new lock instance under the same canonical serializing identity before retry, and ambiguous or conflicting remote history remains reconciliation-required.

No revoked lock instance is ever reactivated. Recovery creates a newly authorized lock instance for the originating task under the same canonical serializing identity only after the provider outcome is known; if authorization is no longer valid, the workspace remains inaccessible. `reconciliation_required` has no maximum residence time in MVP: it ends only when authoritative evidence selects a known transition (after which the originating task may resume under the completion model) or when a recorded human escalation leads to a post-MVP repair operation [ASSUMPTION A16; see PD2]. No workspace may remain opaque: the `unknown_provider_outcome` and `reconciliation_required` statuses expose the operation family, last evidence check, next scheduled check or human escalation condition, correlation identity, and safe reason without provider payloads or protected content.

For every mutation:

- equivalent intent means the same tenant, Contract Spine operation, canonical target identity, normalized payload and semantic options, policy version, and delegated task scope. Contract-defined defaults, path/ref normalization, map ordering, and set-like collection ordering are canonicalized before comparison; correlation IDs, authentication tokens, and transport retry metadata are excluded;
- while the idempotency record is unexpired within its declared retention tier, the same key plus equivalent tenant-scoped intent returns the same logical result without duplicate events, provider writes, file changes, repositories, commits, audits, or idempotency records;
- while the idempotency record is unexpired within its declared retention tier, the same key plus different intent returns the canonical idempotency-conflict result without revealing prior protected metadata;
- mutation replay records use the 24-hour tier and commit replay records the C3 seven-year tier, as approved in the OQ8 design (`docs/exit-criteria/oq8-idempotency-design.md`, 2026-07-19); after replay expiry the consumed-key evidence required by OQ8 is retained for the C3 consumed-key class (managed-tenant lifetime plus 400 days after an approved tenant-deletion request, with legal hold pausing the countdown), so an expired key stays recognizable for the life of the tenant; replay always revalidates current authorization before returning protected prior result data, so a revoked caller receives safe denial while the idempotency record remains intact;
- reuse of an expired key is rejected with the stable expired-key outcome and requires state refresh plus a new key; expiry never causes an old external mutation to be treated automatically as a new request;
- unconfirmed external outcomes first enter `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks enter `reconciliation_required`, and neither state permits blind retry;
- non-mutating operations reject idempotency keys and declare read consistency, safe denial, audit metadata, correlation behavior, and projection expectations.

### Authentication and Authorization Model

Authentication and tenant authorization rely on existing Hexalith.Tenants and Hexalith.EventStore patterns. Hexalith.Folders adds folder-specific ACL checks.

#### Actors

The canonical actor names below are the only ones used for authorization decisions, the OQ3 matrix, and the quality-gate matrix; the synonym column maps the wording used elsewhere in this document and in journeys.

| Canonical actor | Synonyms used in this PRD | Authority source | Typical journeys and FRs |
| --- | --- | --- | --- |
| Tenant administrator | tenant admin | Hexalith.Tenants tenant authority | UJ2, UJ6, UJ9; FR4, FR5, FR13, FR15, FR20 |
| Tenant member | developer, chatbot developer, automation developer, authorized actor | Tenant membership plus folder grants | UJ1, UJ7; FR11, FR18, FR24–FR35, FR37 |
| Delegated service agent | AI agent, agent task, delegated actor | Delegating principal's effective permission ∩ agent grant ∩ delegated operation-family/task scope | UJ3, UJ4; FR8, FR25, FR32, FR58 |
| Tenant-scoped operator | operator, platform operator, platform engineer (the job title of the person who usually holds this role) | Operator permission plus explicit tenant/folder authorization | UJ2, UJ5; FR7, FR15, FR16, FR22, FR23, FR52, FR57 |
| Audit reviewer | auditor, security reviewer | Audit-read grant plus tenant/folder authorization | UJ8; FR53, FR54 |
| Incident administrator | incident-admin permission holder | Incident-admin permission plus fresh tenant/folder authorization; a permission held by an operator, not a separate identity | FR56, OQ9 |
| Negative cases | wrong-tenant, revoked, stale, disabled, unknown, hidden-resource caller | None; every case fails closed with the safe denial | UJ6; FR9, FR10 |

"Authorized actor" in an FR means any canonical actor holding the permission that the FR's operation family requires; it is not a synonym for tenant member.

#### Protected operation families and permissions

Every protected operation belongs to exactly one operation family below; the OQ3 authorization matrix is actor × family. Tenant-level families are granted by tenant authority or an operator permission and have no folder ACL entry. Folder-level families are granted through the folder ACL, which the Contract Spine represents today as one `FolderPermissionLevel` per grant (`read`, `write`, `administer`); the family list and the actor rules are product-owned, the wire representation is the Spine's, and OQ3 decides whether the Spine keeps three levels or adopts per-family grants. FR5's operation-family scope is the family set derived from the granted level.

| Family | Scope | Protected operations | FRs | Spine grant today |
| --- | --- | --- | --- | --- |
| Provider configuration | Tenant | Configure provider binding, credential reference, naming/default-ref and capability policy | FR4, FR15 | Tenant administrator authority |
| Readiness and provider evidence | Tenant | Validate provider readiness (read-only and advisory, open to any tenant member holding the folder-create permission); inspect provider support evidence and capability differences | FR7, FR16, FR22, FR23, FR57 | Folder-create permission for readiness validation; tenant administrator authority or operator permission for provider evidence |
| Folder creation | Tenant | Create a logical folder | FR11 | Tenant folder-create permission |
| Incident evidence | Tenant | Degraded-mode event evidence (also requires folder `read`) | FR56 | Incident-admin permission |
| Folder administration | Folder | Grant and revoke access, archive, define branch/ref policy, create a repository-backed folder or bind a pre-created repository for an existing folder | FR5, FR13, FR18, FR19, FR20 | `administer` |
| Task mutation | Folder | Prepare workspace, acquire and release lock, add/change/remove files, commit | FR24, FR25, FR29, FR32, FR33, FR37 | `write` |
| Context read | Folder | Tree, metadata, glob, bounded range, live text search within path policy (the only family that returns file bodies) | FR34, FR35 | `write` |
| Status, permission, and lock inspection | Folder | Workspace, folder, task, cleanup, retry, and lock state including lock owner and task; effective permissions | FR6, FR12, FR26, FR30, FR31, FR46 | `read` |
| Audit read | Folder | Audit trail and incident reconstruction | FR53, FR54 | `read` plus the audit-reviewer role |
| Console view | Folder | Operations-console projections | FR52 | `read` plus the operator permission |
| Index search | Folder | Metadata-token recall and indexing status | FR58 | `read` |

Grant levels are distinct: `write` implies `read`, and `administer` implies neither `write` nor `read`, so tenant administrators hold the task-mutation and read families only through an explicit `write` or `read` grant and administering a folder never implies the ability to mutate or read its files [ASSUMPTION A12, A17]. File bodies are reachable only through the context-read family, which requires `write`; `read` grants metadata-level families only, so audit reviewers, operators, and incident administrators never obtain file bodies through folder `read` [ASSUMPTION A22]. Tenant-administrator authority implies `administer` on every folder in the tenant, and the creator of a logical folder receives `administer` on it at creation [ASSUMPTION A23]; a folder therefore always has at least one administrator who can grant `write` to the task principals. Lock-owner visibility is a read so that a task denied by a lock can see whose lock blocks it. The Spine's per-operation authorization requirement tokens are mapped to these families in the OQ3 matrix.

Tenant administrators own tenant-level Folders configuration: provider bindings, credential references, repository naming/default-ref policy, capability policy, folder ACL grants, and archive decisions. Tenant-scoped operators may validate readiness and diagnose within authorized scope but may not change tenant policy; the only operator path to a configuration change is escalation to a tenant administrator.

Effective permission is the intersection of current active tenant authority and the union of applicable allow-only folder grants assigned directly to the principal or through active group and role membership. MVP folder ACLs do not support explicit deny entries; absence of an applicable allow denies. A delegated service agent receives only the intersection of the delegating principal's current effective permissions, the agent's explicit folder grant, and the delegated operation-family/task scope, so delegation cannot elevate authority. Disabled, deleted, unknown, missing, stale, or revoked tenant authority overrides every folder grant and fails closed. Archive state and resource policy may further restrict an otherwise allowed permission. Conflicting, stale, or incomplete identity/membership/delegation evidence denies access.

Cross-tenant access must be denied before file, workspace, credential, repository, lock, commit, provider, or audit access. Denials must use safe error shapes that avoid unauthorized resource enumeration.

Authorization must be fresh when each mutation is about to perform a side effect; authorization only at request receipt is insufficient for asynchronous work. If tenant membership, folder ACL, delegated authority, provider binding, or credential permission is revoked, the held lock becomes revoked/inaccessible, the denial is audited, and subsequent work fails closed before touching files, repositories, providers, commits, or protected audit resources. Numeric lease-renewal and authorization-revalidation intervals remain governed by C7 and must be approved before release.

Authoritative tenant context comes from the authenticated request and EventStore envelope. Any payload tenant identifier is only a comparison input and must match that authority or be rejected.

Tenant administrators can inspect provider binding ownership and non-secret credential-reference status for their tenant without seeing credential material or unauthorized repository details.

MVP operator access is tenant- and folder-scoped. An operator must hold the applicable operator permission and normal tenant/folder authorization; there is no global cross-tenant search or unscoped break-glass browsing. Any future cross-tenant need-to-know workflow requires a separate Security and PM decision covering consent, duration, visible fields, and reviewable privileged-access audit.

### Data Schemas

The API uses JSON command/query DTOs and structured response models. DTOs are public contracts and require schema/versioning tests.

The Contract Spine must define the accepted bounded file-content transport and stable oversize/unsupported outcomes; SDK and adapter behavior must derive from it. Metadata-only events may include path, content hash, size, media type, content reference ID, operation ID, provider reference, actor, timestamps, and commit reference, but not file contents; path, repository, branch, and commit-message values are subject to C9 classification and the confidential-override replacement rule.

MVP file policy is fail-closed and cross-surface consistent. Absolute or traversal paths outside the workspace root, symlink/reparse traversal, reserved names, and case-collision aliases are rejected. Paths are compared using the approved canonical normalization without silently retargeting the caller's requested path. Binary or oversized content that lacks an approved bounded transport/policy is rejected with a stable policy result rather than truncated or partially applied. Exact encoding, binary, large-file, include/exclude precedence, and safe-denial rules must be published in the canonical file-policy artifact before release; C4 bounded-read limits are not a substitute for mutation/file-size policy.

### Error Codes

Errors must be stable, machine-readable, and mapped consistently across REST API, CLI, MCP, and SDK.

Required error fields:

- `category`
- `code`
- `message`
- `correlationId`
- `retryable`
- `clientAction`
- `details.visibility`

FR44 is the single product enumeration of required caller-visible outcome families; this section does not carry a second list. The Contract Spine's closed category enum realizes those families: it may split a family into finer categories and codes. The family-to-category mapping is published as C13 evidence so that every FR44 family has at least one Spine home and every Spine error category maps to exactly one family; a Spine category for an outcome this PRD does not name maps to the transient infrastructure failure family or to a new FR44 family added under a stable ID, and non-error members such as `success` and `redacted` are outside the mapping.

The safe-denial invariant is a product safety rule and prevails over the Spine. For a given operation, the response to a caller who is not authorized for the target (absent, cross-tenant, hidden-resource, audit-scope, and missing-binding or missing-policy cases alike) must be indistinguishable in status, category, code, message, and detail keys; only correlation identity and the per-request instance identifier may differ. An authorized reader of an unbound or policy-less folder receives its explicit lifecycle and binding status (FR12), not the safe denial. The Spine carries the safe denial today as category `tenant_access_denied` with code `resource_unavailable`; its current declaration of two status-distinct envelopes (403 and 404) on 46 of 49 operations violates the single-status rule and is listed in PD10. Enum members such as `cross_tenant_access_denied`, `audit_access_denied`, and `not_found` may appear in audit-side classification but never in a caller-visible response; where the current Spine declares them as caller-visible responses on an operation, that is a conformance defect against this invariant to be corrected in the Spine before release (PD10). A folder is hidden to a caller who holds no allow on it and receives the safe denial; `folder ACL denied` is returned only when the caller holds at least one allow on that folder but lacks the requested operation family.

Error responses indicate whether the client may retry, must refresh state, must request authorization, must change input, or must escalate provider/configuration failure.

Error details are a closed, bounded, metadata-only shape defined per error by the Contract Spine, not an arbitrary payload bag. They must never include secrets, tokens, file content, diffs, provider payloads, local absolute paths, or unauthorized existence. Authorization is evaluated before state-specific detail. The safe-denial response for absent, cross-tenant, missing-binding, missing-policy, and equivalent protected-resource cases must be indistinguishable at the caller-visible boundary for callers not authorized for the target. Redacted, unknown, missing, hidden, stale, and unavailable are distinct states; redacted data cannot also carry cleartext.

Non-enumerating equivalence applies until the caller is authorized for the specific tenant/provider-binding scope. After that authorization succeeds, readiness diagnostics may identify the caller's configured provider product, instance/profile, capability gap, credential-reference status, and safe remediation category. They still may not reveal an unconfigured provider identity, credential value, repository existence outside the authorized binding, or cross-tenant state.

Provider and workspace failure reporting must distinguish a known failure from an unknown outcome. If repository creation, file mutation, or commit status cannot be confirmed after a timeout or provider interruption, the system first exposes `unknown_provider_outcome` and performs only the bounded read-only evidence checks defined in Task and Lock Completion Model. Exhausted or conflicting evidence then exposes `reconciliation_required`; neither state permits retry that could duplicate repositories, file changes, or commits.

### Rate Limits, Throttling, and Idempotency

The MVP does not require fixed public rate-limit numbers. It applies throttling and correlation to provider readiness reads and provider API calls, and applies the all-mutations idempotency contract to repository creation/binding, configuration changes, ACL/archive changes, workspace/lock commands, file mutations, and commits. Readiness and other read-only operations reject idempotency keys.

Throttling policy enforcement dimensions are exactly tenant, folder, workspace, provider, and command type.

Provider calls use a bounded retry/backoff policy, provider-specific throttling, and a stable failure projection when rate limits, timeouts, permission failures, or unknown outcomes occur. The per-call timeout, retry-limit, and backoff-cap ceilings are published in the OQ4 provider compatibility catalog; until then no provider call may exceed the 15-minute evidence budget of the completion model.

### Workspace State and Concurrency

The canonical workspace lifecycle vocabulary is `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`. These lowercase wire terms are stable product states. Three of them are defined by the lock and the staged change set: lifecycle `locked` means a task holds a valid lock and no mutation has been applied yet; `changes_staged` means known mutations are applied under a valid lock with no interruption and no failed commit; `dirty` means the workspace has left the valid-lock path because the lock lapsed or was revoked, a commit failed, or reconciliation confirmed a partial set; it may or may not hold staged changes, and the changed-path metadata says which. A `dirty` workspace holding no staged changes returns to `ready`/`unlocked` when its lock becomes `stale`, so a task that locked, wrote nothing, and vanished does not block archive or cleanup [ASSUMPTION A21]. Lifecycle `locked` requires lock state `locked`; the pair lifecycle `locked` with lock state `unlocked` is illegal and is a conformance defect.

Lock state is a separate dimension: `unlocked`, `locked`, `expired`, `stale`, or `revoked`. Generic operation-execution labels such as pending, in-progress, succeeded, failed, and cancelled must be named as operation state and must not replace workspace lifecycle or lock state.

Operator disposition is derived rather than treated as another lifecycle: requested/preparing/committed are auto-recovering; unknown-provider-outcome is auto-reconciling during its bounded evidence-check budget; locked/changes-staged are degraded-but-serving; dirty/reconciliation-required are awaiting-human, which means no automatic transition will occur, not that the originating task is barred from acting under the completion model; failed/inaccessible are terminal-until-intervention (an operator label, not cleanup eligibility); ready is available unless freshness exceeds C2.

Lock semantics are product rules; only the numeric intervals are deferred to C7/OQ1. Each lock instance has one owner, the task that acquired it, and one lease. Only the owning task may renew the lease, and only under fresh authorization. A lease that lapses without renewal becomes `expired`; after the C7-approved threshold without owner activity it becomes `stale`. The originating task may re-acquire a new lock instance in either state: `stale` restricts nothing further for the owner and is an operator signal that still permits no takeover and no automatic release of a lock guarding staged changes in MVP (a clean `dirty` workspace returns to `ready` at `stale`, A21). Revocation of any authority makes the lock `revoked`. No second lock instance is ever created for the same task while its lock is valid: a same-task acquisition under fresh authorization returns the existing lock instance and its ownership proof (an idempotent result), so a restarted agent process recovers without waiting for its lease to lapse, while acquisition by any other task is denied with the lock-conflict result. Release accepts only the caller-completed reason in MVP; the other release reasons the Spine declares are reserved for post-MVP repair and PD2 [ASSUMPTION A20]. Provider-confirmed commit releases the lock; failed or unconfirmed commits follow the completion model. [ASSUMPTION A7: expired-versus-stale semantics, the no-takeover rule, and idempotent same-task acquisition.]

The MVP serializing identity combines the managed tenant, canonical provider/repository identity, and normalized target ref. All folder bindings, aliases, workspaces, and tasks that resolve to that identity are limited to one active mutation writer; genuinely different repository/ref identities may proceed independently. Governed mutations and commits require a valid, unrevoked lock and fresh authorization. Context reads of committed/ready content do not require the mutation lock but still require tenant, folder ACL, and path policy. While lifecycle is `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required`, live body search, range reads, and file content are visible only to the freshly authorized originating/lock-owning task; other authorized actors receive metadata-only status, and the incident view never exposes content. Commit must release the lock or transition the current task to a defined task-terminal or recovery state.

A remote repository/ref identity may be bound by at most one managed tenant, so tenant-scoped serialization is also global serialization for that remote [ASSUMPTION A8]. A binding attempt from another tenant receives the same non-enumerating safe denial as an absent repository (FR19), never a duplicate-binding result; `duplicate binding` is caller-visible only inside the tenant that already holds the binding. For a caller who already holds provider access to the repository the denial cannot hide the repository's existence; the invariant is that it discloses no Hexalith tenant, folder, or binding state. An archived folder's repository binding is retained as metadata but no longer occupies the remote identity in the duplicate index, so an authorized folder may bind that remote again [ASSUMPTION A19].

After platform-owned cleanup completes (FR30), the workspace lifecycle value is unchanged, cleanup status reports `completed`, and a context query against that workspace returns the stable content-unavailable policy result rather than empty content; no additional lifecycle state is introduced [ASSUMPTION A10].

Concurrent operations must either serialize deterministically or fail with stable conflict errors. Commit without a valid lock, mutation without fresh authorization, lock acquisition by a task other than the owner, and retry after an unknown provider outcome are denied without side effects and expose the applicable lock, inaccessible, unknown-provider-outcome, or reconciliation-required state.

Query/status responses must distinguish accepted command state from projected state when projection lag exists.

### API Versioning

The initial API is versioned as `v1`. Additive evolution is preferred. Breaking changes to command/query DTOs, event payloads, error categories, workspace states, provider capabilities, or SDK models require explicit versioning.

Event payload evolution uses schema versions and backward-compatible consumers.

### SDK Requirements

The generated typed SDK is required for the current release. Its first and only current-release language is .NET (C#), the Hexalith implementation ecosystem; additional SDK languages are Phase 3 candidates, not MVP scope [ASSUMPTION A15]. CLI and MCP consume this SDK and may not implement independent lifecycle behavior.

The initial generated SDK may be minimal only in packaging and convenience helpers. Its operation client and typed request/response/error models must be generated from the Contract Spine and cover every C13-required SDK cell. It must support authentication configuration, idempotency keys, correlation IDs, async operations where applicable, retry/idempotency helpers, and task-based examples for the core repository workflow.

The SDK must pass the same contract/parity suite as API, CLI, and MCP for MVP workflow commands.

### API Documentation

Documentation deliverables must include:

- OpenAPI `v1` reference with schemas, auth requirements, idempotency keys, pagination/filtering conventions, correlation IDs, and examples.
- Getting started guide.
- Authentication, tenant, and folder ACL guide.
- Workspace lifecycle and lock state diagram.
- File operation to commit flow diagram.
- Tenant/auth/ACL decision flow diagram.
- CLI reference.
- MCP tool/resource reference.
- SDK reference and quickstart.
- Provider integration and provider contract testing guide.
- Operations console and metadata-only audit guide.
- Error catalog with REST status, CLI exit behavior, SDK error/result behavior, retryability, client action, and audit/logging expectations.

### Contract and Quality Gates

The MVP must include these quality gates:

- Every current Contract Spine operation has exactly one C13 parity row; additions without a row and undeclared removals fail the gate. The current generated inventory, not a hard-coded count in this PRD, is the denominator.
- Every protected operation family is covered by the canonical authorization matrix for every canonical actor in the Actors table (tenant administrator, tenant member, delegated service agent, tenant-scoped operator, audit reviewer, incident administrator) and every negative case (wrong-tenant, revoked, stale, and hidden-resource). Security/Authorization owns this matrix, and release is blocked until its linked inventory is approved.
- Each supported provider must achieve a 100% pass rate on the provider contract suite before being marked ready.
- Zero event schemas, logs, traces, projections, console responses, or audit records containing file contents, provider tokens, credential material, or secrets.
- Idempotency tests cover every mutating Contract Spine operation and prove no duplicate events, provider writes, file changes, repositories, commits, audits, or idempotency records; every non-mutating operation rejects idempotency keys.
- Tenant isolation tests proving no cross-tenant read, write, lock, commit, provider, audit, or projection access.
- Tenant-authority tests cover enable/disable, deletion, downgrade, revocation, stale/missing/unknown authority, and duplicate/out-of-order authority updates; every mutation remains fail-closed until fresh active authority is proven.
- Internal service and event boundaries are deny-by-default: missing or invalid service identity, tenant scope, or policy evidence cannot invoke or project protected behavior.
- Path security tests for traversal, absolute paths, mixed separators, encoded traversal, reserved names, Unicode normalization, symlinks, and case sensitivity.
- Read-model determinism: rebuilding views from an empty read model must produce equivalent state from the same ordered event stream.
- Projection idempotency: duplicate event delivery cannot duplicate or corrupt status, audit, timeline, search-index, or authorization views.
- Historical replay compatibility: every supported event/schema version rebuilds current views or fails through an explicitly governed migration gate; release evidence must cover the retained history window.
- Golden schema tests for DTO versioning and error mapping.
- Provider failure tests for timeout, 401, 403, 404, 409, 429, 5xx, branch protection, missing repository, deleted repository, stale clone, credential revocation, and provider drift.
- Provider contract tests for GitHub and Forgejo must cover readiness, repository binding, branch/ref handling, file operations, commit behavior, credential-reference usage, retry/idempotency behavior, and unknown outcome handling before either provider is marked ready.
- Provider readiness evidence identifies product, instance, observed version/API profile, accepted credential profile, and required capabilities; unsupported or unknown compatibility is not ready.
- Context-query security tests must cover unauthorized tenant access, unauthorized folder access, excluded paths, binary files, large files, range limits, result limits, traversal attempts, symlinks, generated context payload redaction, and denial audit records.
- Redaction tests must inject sentinel secrets and file-content markers, then verify they do not appear in logs, traces, metrics labels, events, audit records, console views, provider diagnostics, error responses, or generated artifacts.

### Implementation Considerations

Implementation must avoid separate business-logic paths for API, CLI, MCP, and SDK. Shared application services or generated client contracts define canonical behavior; each surface adapts transport and presentation only.

Provider readiness must stay narrowly scoped to health, capability discovery, credential reference validation, repository policy validation, and workspace safety. It must not become a broad provider administration platform.

The read-only operations console remains projection-first and non-authoritative. Its bounded incident-mode exception is available only when projections are degraded and does not permit repair. Repair workflows remain post-MVP.

If delivery capacity tightens, an explicit scope-change decision is required; the current release minimum remains Contract Spine, REST, generated SDK, CLI, and MCP for the core lifecycle plus the read-only console. Security, idempotency, failure visibility, and cross-surface semantics are not optional cuts.

### Architecture Decisions Needed Next

The PRD defines product outcomes rather than implementation mechanisms. Architecture and the canonical contract artifacts own transport mechanics, provider adapter shape, projection compaction, and storage details. Before release they must also close the user-visible file-policy artifact, C7 lease/revalidation numbers, and the canonical authorization-matrix inventory named by this PRD.

### Deferred Quantitative Targets — Architecture Exit Criteria

Approved numeric targets and their canonical evidence are summarized below, one row per criterion in the governance record `docs/exit-criteria/c0-c13-governance-evidence.yaml`. Changes, including any recalibration of a performance target that implementation benchmarks show to be misleading, require documented approval in the governance record and a new open release item; the linked artifacts remain authoritative for measurement detail.

| ID | Target | PRD Source | Status |
| --- | --- | --- | --- |
| C0 | Contract Spine source of truth | MVP Contract Summary; Public Surfaces | Approved in the governance record. |
| C1 | Concurrent capacity targets: maximum concurrent tenants, folders per tenant, active workspaces per tenant, and concurrent agent tasks per tenant | NFR Scalability and Capacity (constraint that capacity targets must avoid assuming a single tenant, single repository, or single active workspace) | Approved 2026-05-30 (Story 7.10): 4 concurrent tenants, 2 folders/tenant, 2 active workspaces/tenant, 2 concurrent agent tasks/tenant — see `docs/exit-criteria/c1-capacity.md` |
| C2 | Status-freshness target: maximum acceptable lag between an emitted lifecycle event and its appearance in status/audit views under normal operation | NFR Observability, Auditability, and Replay | Approved 2026-05-30 (Story 7.10): 500 ms commit-to-status-read lag (hermetic) — see `docs/exit-criteria/c2-freshness.md`; production exporters/alerts wired by Story 7.12 |
| C3 | Retention by data class | NFR Data Retention and Cleanup | Approved by PM 2026-06-22 and Legal 2026-06-24 (the record's cleanup triggers "lock expiry" and "cancellation" predate this PRD's `dirty` and release rules and are reconciled under PD11): audit and commit-idempotency metadata 7 years; workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, and copied auth-claim metadata 400 days; read models 400 days or until rebuilt, whichever is sooner; temporary working files deleted 7 days after task-terminal closure and no active task; folder/tombstone metadata tenant lifetime plus 400 days after approved deletion workflow. See `docs/exit-criteria/c3-retention.md`. |
| C4 | Bounded MVP context-query inputs and responses | NFR Performance and Query Bounds | Approved 2026-06-22: 100 requested paths, 2,000 tree entries, 500 search/glob results, 262,144-byte bounded range, 1,048,576-byte aggregate response, and 2-second server execution limit. See `docs/exit-criteria/c4-input-limits.md`. |
| C5 | Concrete scalability quantifiers replacing the word "multiple" in the NFR scalability constraint, derived from C1 | NFR Scalability and Capacity | Approved 2026-05-30 (Story 7.10): 4 tenant units, 2 folder units/tenant, 2 workspace units/tenant, 2 task units/tenant, ≥1 lifecycle op/sec — see `docs/exit-criteria/c5-scalability-quantifiers.md` |
| C6 | Workspace lifecycle, lock-state mapping, and operator disposition | Workspace State and Concurrency | Approved 2026-05-11 for the state vocabularies only. The approved matrix (`docs/exit-criteria/c6-transition-matrix-mapping.md`, `architecture.md`, `FolderStateTransitions.cs`) does not carry the transitions this PRD's completion model requires and rejects them as invalid: originating-task resume out of `dirty`, retryable commit failure staying `dirty` (the matrix sends every commit failure to `failed`), revocation with staged changes to `inaccessible`, `inaccessible` back to `dirty` on restored authority, clean `dirty` to `ready` at `stale`; it also carries operator discard/retry/mark-failed events this PRD reserves for post-MVP and a disposition for `unknown_provider_outcome` that differs between the mapping and the architecture. PD11 owns the reconciliation; until it closes, C6 is approved for vocabulary and reference-pending for transitions. |
| C7 | Lock renewal, authorization revalidation, and revocation-effect SLO | Authentication/Authorization; Reliability | Reference-pending; tracked as OQ1. Product behavior already revalidates every mutation and fails closed. |
| C8 | Context-query authorization evidence | Endpoint Specifications; FR34–FR35 | Approved in the governance record. |
| C9 | Sensitive metadata classification | Security; Observability | Approved: paths, repository/branch names, and commit messages default tenant-sensitive; a confidential tenant override replaces cleartext with a stable tenant-scoped correlation token no later than audit/projection write time so incidents retain linkage without cleartext; because the incident view reads event evidence, it must apply the same replacement before display and OQ9 evidence must cover that path (whether replacement also occurs at event write is an architecture decision, PD8); redacted/hidden/unknown/missing/stale/unavailable are distinct. Evidence is governed by C9 safety fixtures. |
| C10 | Tenant-prefixed cache-key evidence | Security and Tenant Isolation | Approved in the governance record. |
| C11 | File transport boundary evidence | Data Schemas | Approved in the governance record. |
| C12 | Provider drift evidence | Integration and Contract Compatibility | Reference-pending; tracked as part of OQ4. |
| C13 | Complete operation/parity denominator | Integration and Contract Compatibility | Generated from the current Contract Spine; every operation requires exactly one parity row. The generated current inventory, not any count written here, is the binding denominator. |

Each target must be validated through its named release-calibration evidence. C7 lease-renewal and authorization-revalidation intervals remain reference-pending; the product outcome is already fail-closed on every mutation, but release requires approved numeric intervals and a revocation-effect SLO.

## Project Scoping & Phased Development

### MVP Strategy & Philosophy

**MVP Approach:** Platform MVP: repository-backed task lifecycle.

The MVP must prove one canonical job: an AI agent can safely prepare, modify, lock, commit, inspect, and audit tenant-scoped files in a real repository-backed workspace without leaking data, losing control, or hiding failure state.

The MVP is successful when the same repository-backed task lifecycle works through REST, CLI, MCP, and SDK surfaces with equivalent authorization, error semantics, audit outcomes, and observable state transitions.

**Canonical MVP Workflow:**

Tenant setup, provider readiness check, folder/repository binding, workspace preparation, task-scoped lock, file add/change/remove, commit, context query, and status/audit inspection.

Provider readiness is a gate, not a passive feature. If GitHub or Forgejo readiness fails, the workflow must stop with a stable readiness result carrying safe reason, retryability, remediation category, and correlation ID rather than continuing into partial mystery state.

**Resource Requirements:** Senior backend/domain, Dapr/Aspire/EventStore, Git provider, security/authorization, API/CLI/MCP/SDK contract, and test automation capability. Frontend scope stays narrow: read-only operational visibility.

### MVP Feature Set

**Must-Have Capabilities:**

- Tenant-scoped authorization and folder ACL enforcement.
- GitHub and Forgejo readiness gates and provider contract tests.
- Repository-backed folder creation/binding.
- Workspace preparation and task-scoped locking with defined contention, expiry, abandoned-lock, release, and cross-tenant behavior.
- Governed add/change/remove file operations.
- Commit workflow with task/correlation metadata and changed-path metadata.
- Workspace lifecycle, lock state, and operator disposition use the distinct vocabularies defined in Workspace State and Concurrency.
- Live-workspace context queries: tree, metadata, glob, bounded ranges, and bounded authorized text-body search/snippets per FR34–FR35.
- Metadata-only audit that records actor, tenant, provider, folder/repository, operation, target path, correlation ID, timestamps, result, commit reference, and error category while excluding file contents and secrets.
- OpenAPI Contract Spine as the machine contract; REST public transport, generated SDK, CLI, and MCP preserve core-lifecycle parity.
- Tenant-administrator access grant/revoke, effective-permission inspection, folder archive, stable denial of archived-folder mutations, and C3-bound lifecycle/audit visibility.
- Authorized metadata-token recall and indexing-status queries per FR58; cross-workspace body-content indexing/recall is not MVP, while bounded live-workspace text search remains required by FR34–FR35.
- Read-only operations console limited to readiness, locks, workflow/status, audit, provider health, dirty state, failed operation, and last commit visibility.
- Risk-based validation matrix for tenant isolation, provider contracts, adapter parity, locking, commit integrity, audit/status, and context query correctness.

**MVP Acceptance Evidence:**

- The full per-cell C13 conformance suite plus SM1 is the parity acceptance bar; a single end-to-end scenario through REST, CLI, MCP, and SDK is a smoke check and never substitutes for it.
- GitHub and Forgejo both pass automated provider contract tests.
- Cross-tenant negative tests cover tenant IDs, provider credentials, repository bindings, locks, audit visibility, and context queries.
- Failure-mode tests cover concurrent agents, stale locks, failed commits, provider unavailability, unauthorized access, and audit reconstruction.
- Adapter parity proves equivalent capability coverage, authorization behavior, error categories, operation IDs, audit entries, and status results.
- Tenant isolation tests prove a task can only access repositories, credentials, policies, locks, audit records, and context indexes belonging to its tenant; cross-tenant identifiers return non-disclosing failures.
- Every asynchronous provider side effect revalidates authorization and is tenant-scoped, idempotent, and safe under duplicate delivery; no incoming webhook ingestion exists in MVP.
- Tenant-administration evidence covers grant/revoke, effective permissions, archive, archived-mutation denial, retention-bound visibility, and cross-surface semantics.
- Large change-set projections preserve counts, operation types, failure attribution, and explicit limits without exposing unbounded per-file detail to routine user-facing surfaces.

### Explicit MVP Non-Goals

- No repair automation.
- No brownfield migration wizard.
- No rich operations workflow surface.
- No deep drift remediation.
- No local filesystem-only workspace mode as an MVP product capability.
- No nested repository orchestration.
- No multi-agent simultaneous write collaboration.
- No first-class move/rename command; MVP represents a rename as an authorized add plus remove under the same task, lock, and commit.
- No archived-folder restore, hard deletion, remote-repository deletion, or provider-history rewrite. Archive preserves the binding as metadata but never deletes or mutates the provider repository.
- No file editing or file-content browsing in the operations console.
- No raw file diffs or file-content display in the operations console.
- No broad provider framework beyond fully validating GitHub and Forgejo support.
- No incoming provider webhook ingestion; any future webhook surface requires an approved tenant-routing design.
- No cross-workspace body-content indexing or indexed body recall; FR58 is metadata-token recall until separate Security and PM approval. Bounded direct body search inside the currently authorized live workspace remains part of FR34–FR35.
- No secret material storage in Hexalith.Folders; only credential references may appear where authorized.
- No legal-hold placement/release workflow and no tenant-deletion workflow surface in MVP; the C3 rules apply, and the separately authorized governance operations named in FR13 are Phase 2 [ASSUMPTION A13].
- No policy engine beyond required tenant, provider, readiness, ACL, and workspace controls.

### Post-MVP Features

**Phase 2:** Repair commands (retry commit, discard changes, rebuild cache, release stale locks), brownfield adoption and unmanaged local-folder/repository migration, local-first folders that promote to repository-backed storage, auto-commit command mode, deeper drift detection and drift-first operations views, richer provider capability recipes and contract tests, large-file policy expansion, legal-hold and tenant-deletion workflow surfaces, multiple Git organizations per tenant, module-managed local storage policy, and broader operations workflows including evented repair console workflows.

**Phase 3:** Additional Git providers, migration/provider portability tooling, deeper AI context indexing, wider repair automation, and Hexalith-wide adoption as the default durable workspace substrate.

### Risk Mitigation Strategy

**Technical Risks:** Provider mismatch, wrong-repo mutation, stale locks, commit failure, event volume, path traversal, adapter drift, and cross-tenant leakage. Mitigate with contract tests, negative isolation tests, path security suites, idempotency tests, and parity scenarios.

**Market Risks:** Building a platform before proving the agent job. Mitigate with the canonical task lifecycle demo and operational evidence after completion or failure.

**Resource Risks:** If capacity tightens, preserve the Contract Spine, REST, generated SDK, CLI/MCP core-lifecycle parity, tenant isolation, readiness gate, lock/file/commit/status/audit workflow, and provider tests first. Any surface cut requires an explicit scope-change decision. Do not cut security, idempotency, or failure visibility.

## Functional Requirements

Functional Requirements are organized by capability area. Each block traces back to the User Journeys above and to the broader Hexalith.Folders objective stated in the Executive Summary: provide a tenant-scoped, auditable, recoverable folder lifecycle for agentic file work, accessible through API, CLI, MCP, and SDK with consistent semantics.

### Glossary and State Vocabulary

| Term | Product meaning |
| --- | --- |
| Logical folder | Tenant-scoped managed identity, policy, ACL, lifecycle, and audit boundary; it is not a filesystem path. |
| Repository-backed folder | A logical folder that holds a repository binding. This is the only noun for that concept; "Git-backed folder" and "provider-backed folder" are not used. |
| Provider binding | Tenant-level configuration of a Git provider: product, instance, credential reference, naming/default-ref policy, and capability policy. Owned by the tenant administrator (FR4, FR15). |
| Repository binding | Authorized association between one logical folder and a canonical provider/repository identity plus target ref policy; created through a provider binding. |
| Workspace | Disposable task working area prepared from a repository binding; never the durable source of truth. |
| Task | Caller-visible unit of agent work that correlates preparation, lock, mutations, commit, status, and audit. Identified by the caller-provided task identity bound to the principal and delegation scope at preparation (Command and Query Contract, Task identity). |
| Task-scoped lock | The single mutation lock a task holds on a workspace; "workspace lock" and "task-scoped mutation lock" refer to the same object. |
| Commit reference | The provider-returned identifier of a durable commit (the commit SHA for Git providers); "commit SHA" and "commit ID" refer to the same value. |
| Correlation ID | Caller-supplied or adapter-generated identifier that links every record of one request across surfaces, events, audit, and diagnostics. |
| Operations console | The read-only, projection-first diagnostic surface; "console", "read-only console", and "diagnostic console" refer to it. |
| Context query | Authorized, path-policy-filtered tree, metadata, search, glob, or bounded-range read; never unrestricted browsing. |
| Audit record | Immutable metadata-only evidence of allowed, denied, failed, retried, duplicate, or completed behavior. |
| Status record | Current read-model view with freshness and availability semantics; not a raw event mirror. |
| Contract Spine | Canonical machine-readable operation and schema contract from which surface parity is derived. |
| Tenant-scoped operator | Operator authorized for an explicit tenant/folder scope; MVP has no global cross-tenant browsing. |
| Tenant-sensitive metadata | Paths, repository names, branch names, and commit messages visible only to authorized tenant members or scoped operators; a tenant may elevate them to confidential. |
| Workspace lifecycle | One of `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, or `reconciliation_required`. |
| Unknown provider outcome | Immediate non-terminal state when an external create/bind, file mutation, or commit may have produced a side effect but confirmation is unavailable; only bounded automated evidence checks and read-only status are allowed. |
| Reconciliation required | Blocked state entered when automatic evidence checks are exhausted or evidence conflicts; no mutation/retry/takeover is allowed until authoritative provider/workspace evidence resolves the outcome or a human escalation is recorded. |
| Task-terminal | The current task has ended in `committed`, `failed`, `inaccessible`, or explicit no-change closure. `dirty`, `unknown_provider_outcome`, and `reconciliation_required` are not task-terminal. Failed/inaccessible closure first records final metadata-only evidence and operator disposition, then provides the same C3 seven-day observation window before cleanup. |
| Lock state | Separate dimension: `unlocked`, `locked`, `expired`, `stale`, or `revoked`. |
| Committed | Provider-confirmed durable update of the bound remote/ref with a returned commit reference; local-only success is insufficient. |

### Capability Contract Terms

- FR1: Public documentation, Contract Spine descriptions, generated SDK names, CLI/MCP help, and console labels use the Glossary terms consistently; documentation/schema checks fail on conflicting synonyms or state casing.
- FR2: Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one durable commit, context/status/audit, and cleanup visibility, including failure transitions.
- FR3: Every Contract Spine operation declares mutation or read-only classification in C13; mutations follow the all-mutations idempotency contract and reads reject idempotency keys.

### Authorization and Tenant Boundary

- FR4: Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; tenant-scoped operators may validate but may not modify it.
- FR5: Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting operation-family scope is visible in effective permissions and auditable without exposing hidden principals.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope; the OQ3 authorization matrix records the evaluated operation family for every protected operation, and a scope dimension missing from a decision is a failing conformance defect.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information; the denial response for absent, cross-tenant, and hidden resources is equivalent in status, category, code, message, and detail keys apart from correlation and per-request instance identity, and isolation tests assert zero protected-resource reads before the denial through the test-harness access counters.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details; every allow and deny decision emits exactly one metadata-only audit record carrying actor, tenant, operation, operation family, result, and correlation ID.

### Folder Lifecycle

- FR11: Authorized actors with fresh tenant authority can create a logical folder within that tenant and receive its tenant-scoped managed identity and initial lifecycle state; denial creates no folder or provider side effect and uses the safe authorization/lifecycle result.
- FR12: Authorized actors can inspect folder lifecycle and binding status with freshness and availability metadata; an unauthorized, hidden, stale, or unavailable state uses the canonical non-enumerating result rather than partial binding details.
- FR13: Tenant administrators can archive a folder only when it has no active task, no lock in state `locked`, and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.
- FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

### Provider Readiness and Repository Binding

- FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID; every readiness failure maps to one FR44 category, and readiness never reports ready while any required capability is unsupported or unknown.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass and receive its canonical provider/repository binding plus inspectable folder/workspace state; failed readiness or authorization creates no repository or binding side effect and returns the canonical safe result.
- FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.
- FR20: Authorized tenant administrators can define or select the branch/ref policy used by repository-backed folder tasks; an accepted policy becomes part of readiness, binding, and the canonical serializing target, while invalid or unauthorized changes are rejected without changing the active binding.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets; redaction tests with sentinel secrets prove that no credential value, token, or unauthorized repository identity appears in any binding, readiness, or diagnostic response.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle; readiness and provider-support responses enumerate the C13-declared capability set per provider as supported, unsupported, or unknown, and a client never has to infer a difference from a failed operation.
- FR23: Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready.

### Workspace and Lock Lifecycle

- FR24: Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect.
- FR25: Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata.
- FR28: Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.
- FR29: Authorized owners can release a workspace lock when no changes remain staged, presenting the task identity and lock-ownership proof; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.
- FR30: Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.
- FR31: Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable.

[NOTE FOR PM] The product rules for FR25–FR29 (task identity, lease ownership, expiry, stale, revocation, idempotent same-task acquisition, no takeover) are stated in Workspace State and Concurrency; OQ1 closes only the numeric intervals and OQ7 the lock-identity evidence. PD2 records the open decision on an MVP discard path.

### File Operations and Context Queries

- FR32: Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy; a rejected request applies no part of its change set, returns the applicable FR44 category, and leaves the workspace lifecycle and lock state unchanged.
- FR34: Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution.
- FR35: Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range and file content are never silently truncated, and a request whose excess cannot be handled by supported truncation returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.
- FR36: The operations console must remain read-only and excluded from file editing or file-content browsing capabilities.

[NOTE FOR PM] OQ2 closes the file-policy vocabulary and exact allow/reject behaviour for FR32–FR35; the fail-closed defaults in Data Schemas apply until it closes.

### Commit, Evidence, and Idempotency

- FR37: Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting automatic evidence moves it to `reconciliation_required`.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission.
- FR39: The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification.
- FR40: The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation.
- FR41: Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new intent.
- FR42: While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

### Error, Status, and Diagnostics Contract

- FR43: Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility.
- FR44: The error taxonomy is the single caller-visible enumeration of outcome families and must distinguish safe denial (the one caller-visible outcome for tenant authorization denied, absent, cross-tenant, hidden-resource, audit-scope, and unauthorized missing-binding or missing-policy cases, carried today by the Spine as category `tenant_access_denied` with code `resource_unavailable`), validation failure, authentication failure, invalid state transition, folder ACL denied, credential reference missing or invalid, provider readiness failed, provider permission insufficient, provider unavailable, provider rate limited, unsupported provider capability, repository conflict, duplicate binding, branch/ref conflict, workspace not ready, lock conflict, stale or interrupted lock, stale workspace, dirty workspace, folder archived, content unavailable, path validation failed, file operation failed, commit failed, unknown provider outcome, reconciliation required, duplicate operation, idempotency conflict, expired idempotency key, read-model unavailable, input limit exceeded, and transient infrastructure failure; the Contract Spine maps each family to one or more closed categories and codes, and that mapping is C13 evidence. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.
- FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.
- FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.

### Cross-Surface Contract

- FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine and every C13-required REST cell passing the shared authorization, idempotency, lifecycle, error, and audit scenarios.
- FR48: CLI users can perform every C13-required CLI cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR49: MCP clients can perform every C13-required MCP cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR50: SDK consumers can perform every C13-required SDK cell of the canonical repository-backed task lifecycle and pass the shared operation-identity, authorization, idempotency, status, error, and audit scenarios.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior; the C13 parity oracle reports zero material deltas across REST, SDK, CLI, and MCP for every supported cell, and any delta fails the release gate.

### Audit and Operations Visibility

- FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.
- FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.
*FR56 revised 2026-07-15 (dual authorization before observation).*

- FR56: Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only if, before any stream lookup, event counting, checkpoint lookup, filtering, or shaping, the same actor holds incident-admin permission and fresh current tenant/folder authorization. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

### Authorized Search Facade

*FR58 revised 2026-07-15 (metadata-token boundary).*

- FR58: Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe.

Cross-workspace body-content indexing, indexed body snippets, and indexed body recall are not part of FR58 or the current release. They require separate Security and PM approval and a future product requirement. Bounded direct text search and snippets inside the currently authorized live workspace remain required by FR34–FR35; the separate RAG ingestion capability is outside this PRD.

[NOTE FOR PM] FR58 stays in the MVP Must-Have list by the 2026-07-15 decision because it is the product's only cross-workspace recall path and the foundation of the ratified Memories-index integration (Epic 10); it is fail-safe, so an empty index costs nothing at runtime, and no user journey depends on it. Moving it to Phase 2 and removing OQ5 from the release gate is a PM option recorded as PD9.

## Non-Functional Requirements

### Security and Tenant Isolation

- Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, asynchronous provider side effect, and audit record. No incoming webhook ingestion exists in MVP.
- Cross-tenant access leaks are zero-tolerance defects. No object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
- Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, asynchronous work, audit records, index results, and context-query results.
- File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
- Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
- Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.
- Every mutation and asynchronous side effect must revalidate current tenant, folder, delegated-actor, binding, and credential authority before touching a protected resource; revocation fails closed and changes any held lock to revoked/inaccessible.
- Paths, repository names, branch names, and commit messages are tenant-sensitive by default. Authorized tenant members and tenant-scoped operators with need-to-know may view them; cross-tenant/external diagnostics redact them. A tenant confidential override replaces cleartext at audit/projection write time with a stable tenant-scoped correlation token that preserves equality/linkage across authorized incident records but cannot reveal the original value. Redacted, hidden, unknown, missing, stale, and unavailable remain visibly distinct.
- Credential references must be validated and displayed only as non-secret identifiers or status indicators.
- Provider credentials and repository bindings must be tenant-scoped and must not be reused across tenants, even if repository URLs appear identical.
- Provider credentials must use the least privilege required for supported lifecycle operations and must be validated against required provider capabilities before use.
- Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.

### Reliability, Idempotency, and Failure Visibility

- Workspace lifecycle uses only the canonical lowercase wire states defined in the Glossary; lock state and generic operation-execution status are separate dimensions and must be labeled as such.
- Every accepted operation exposes operation identity, workspace lifecycle, applicable lock state, projection freshness, and a terminal or inspectable non-terminal outcome.
- Repository-backed task lifecycle operations must leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.
- When an external effect is unconfirmed, the workspace immediately enters `unknown_provider_outcome` and permits only bounded automatic read-only checks; exhausted or conflicting evidence moves the workspace to `reconciliation_required`, blocks retry, mutation, and takeover, and requires human escalation. These states never collapse into a generic failure.
- Idempotency keys are required for every mutating Contract Spine operation; non-mutating operations reject them.
- While the idempotency record is unexpired within its declared retention tier, a repeated call with the same key and equivalent payload must return the same logical result, and the same key with a conflicting payload must return an idempotency conflict. After expiry, either form of key reuse returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new request.
- Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.
- Lock acquisition is deterministic and limited to one active writer per managed tenant plus canonical provider/repository identity plus normalized target ref; aliases resolving to that identity collide.
- Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.
- Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
- A successful committed state requires provider-confirmed durable update of the bound remote/ref. A timeout or unconfirmed remote result first moves the workspace to `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks move it to `reconciliation_required`, and neither state permits blind retry.
- Failure visibility must expose state, cause category, retryability, and correlation ID without providing automated remediation in MVP.

### Performance and Query Bounds

- Command submission must acknowledge accepted lifecycle commands within 1 second p95 before asynchronous provider or workspace work continues.
- Status and audit summary queries must return within 500 ms p95 for bounded MVP inputs.
- Context queries must return within 2 seconds p95 for bounded MVP inputs.
- Performance targets apply to bounded MVP inputs and control-plane responses. Targets must be validated against implementation benchmarks and recalibrated before release if provider or runtime constraints make the initial target misleading.
- Provider and workspace operations may complete asynchronously when external Git provider latency or workspace size exceeds interactive response budgets; callers must receive operation identity and status visibility rather than blocking indefinitely.
- Context queries accept at most 100 requested paths; return at most 2,000 tree entries or 500 search/glob results; allow at most 262,144 bytes for one bounded range and 1,048,576 serialized bytes for the aggregate response; and stop after 2 seconds of server execution. Excess input returns the stable input-limit result without partial execution. Supported result truncation occurs only after authorization/path filtering and sets one `isTruncated` flag; file content is never silently truncated.
- Query-limit audit evidence includes family, configured limit, actual count/bytes, elapsed time, truncation, safe category, and correlation ID, but excludes raw query text, file content, path lists, and unauthorized existence.
- File tree, search, glob, metadata, and bounded range queries must protect the service from unbounded workspace scans.
- Large file and binary handling limits must be explicit before MVP release; unsupported files must fail with stable policy errors rather than causing unbounded processing.
- Provider calls must use explicit timeout budgets, retry limits, and backoff caps.
- Provider calls must report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states rather than leaving callers waiting indefinitely.
- Provider rate-limit responses must preserve retry hints where available and expose retry-after or classified retryability.

### Scalability and Capacity

- The MVP release calibration must support 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent agent tasks per tenant, and at least 1 lifecycle operation per second without cross-tenant or cross-task interference.
- Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.
- Audit, timeline, and file-context projections must remain queryable as folder history grows.
- Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.
- Capacity claims beyond the approved C1/C5 release-calibration units require new evidence and are not implied by this PRD.

### Integration and Contract Compatibility

- REST, CLI, MCP, and SDK surfaces must preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes; transport shape and UX may differ.
- Public contracts must be versioned. Breaking changes to lifecycle commands, queries, error categories, workspace states, provider capabilities, or audit fields require an explicit new versioned contract.
- The product must support at least the active contract version and define a deprecation policy before removing any public lifecycle contract.
- Shared or generated contract tests must validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
- The OpenAPI 3.1 Contract Spine is the canonical operation/schema authority; the generated SDK is the typed canonical client; CLI and MCP wrap it; REST emitted schemas validate against the spine. Every current Contract Spine operation has exactly one C13 parity row.
- GitHub and Forgejo support must be validated through provider contract tests before either provider is marked ready.
- Provider contract tests must cover only MVP-dependent lifecycle behavior: readiness, repository binding, branch/ref handling, file operations, commit, status, provider errors, and failure behavior.
- Supported GitHub and Forgejo products, instance/API versions, accepted credential/authentication profiles, and behavior assumptions must be published and recorded so compatibility drift is visible; unknown compatibility cannot be marked ready.
- Provider capability differences must be reported explicitly instead of inferred by clients from failed operations.
- Provider failures such as timeout, rate limit, authentication failure, authorization failure, repository missing, repository conflict, branch/ref conflict, unavailable provider, invalid path, commit rejected, and unknown outcome must map to stable product error categories.

### Observability, Auditability, and Replay

- Every successful, denied, failed, retried, or duplicate operation—including lock, file, commit, provider-readiness, and status-transition operations—must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
- Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
- Paths, commit messages, repository names, and branch names are tenant-sensitive by default under C9; authorized tenant/scoped-operator views may display them, cross-tenant/external diagnostics redact them, and a tenant confidential override stores only the stable tenant-scoped correlation token at audit/projection write time. Confidential incident reconstruction links operations through that token and operation/correlation identity; it does not promise recovery of the original cleartext. Provider payloads, file bodies, secrets, and generated context remain forbidden.
- Operations-console views are projection-first, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata. During projection degradation, the bounded incident view may expose redacted event evidence only to an actor with incident-admin permission and normal tenant/folder access. The view must include a persistent warning, last checkpoint, correlation ID, and time window.
- Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.
- Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.
- The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.
- Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.

### Data Retention and Cleanup

- C3 retention is binding: audit metadata and commit-idempotency records are retained 7 years; workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, and normalized auth-claim metadata are retained 400 days; read models are retained 400 days or until rebuilt, whichever is sooner; temporary working files are deleted 7 days after task-terminal closure and no active task; folder metadata and tombstones remain for the tenant lifetime plus 400 days after the approved deletion workflow, subject to legal hold.
- Tenant deletion anonymizes user display aliases while preserving metadata-only audit correlation/category/timestamp/outcome evidence; task-local display labels are tombstoned, secrets/content are deleted, and retained identifiers remain bounded by C3.
- Workspace cleanup is platform-owned and automatic only after task-terminal closure and no active task. Failed/inaccessible closure records final metadata-only evidence and operator disposition before the C3 seven-day observation window starts. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are excluded. Cleanup retries idempotently; MVP exposes pending/retrying/completed/failed status but no user-triggered cleanup or repair action.
- Cleanup failures must be observable through status, reason code, retryability, timestamp, and correlation ID.
- No cleanup process may remove audit evidence required to reconstruct completed, failed, denied, retried, duplicate, or interrupted operations.

### Operations Console Accessibility

- Read-only operations console flows must target WCAG 2.2 AA.
- The console must support keyboard navigation for primary diagnostic workflows.
- Status, failure, readiness, and lock indicators must not rely on color alone.
- Console screens must provide visible focus states, semantic headings, readable table structure, and sufficient contrast.
- Console text, controls, and tables must remain readable at common browser zoom levels used by operators.

### Verification Expectations

- Each NFR category must have at least one automated verification path or documented manual validation path before MVP release.
- Security, tenant isolation, idempotency, provider contract, read-model determinism, and cross-surface contract compatibility NFRs must have automated tests.
- Performance, accessibility, retention, backup/recovery, and operations-console usability NFRs must have release validation evidence before MVP acceptance.
- Security verification must include dependency/package scanning, generated artifact review, and least-privilege provider credential validation.

## Open Release Items

OQ1–OQ4 close product parameters and inventories that this PRD bounds but does not fully specify (lock timing, file policy, the authorization matrix, and the supported-provider catalog); their closure may refine FR25–FR35 within the fail-closed invariants above but may not weaken those invariants or the approved scope. OQ5–OQ10 close implementation and release evidence. Every listed item, plus any item PD1 and PD3 add, must close before release acceptance. This inventory is incomplete until PD1 and PD3 resolve: Epic 12 (durable round trip) and Epic 13 (hardening) are release-blocking by the 2026-07-20 and 2026-08-04 approvals but have no row here yet; the candidate rows are the OQ11–OQ13 texts drafted in `reconcile-july-2026-synthesis.md` §8.

| ID | Decision/evidence still open | Delivery owner | Blocking consequence and revisit condition | Canonical evidence and accountable approvers |
| --- | --- | --- | --- | --- |
| OQ1 | Approve the C7 lock-renewal interval, authorization-revalidation interval, and revocation-effect SLO. | Architecture + Security | Blocks lock/revocation timing acceptance; close when C7 governance status changes from reference-pending to approved. | `docs/exit-criteria/c7-lock-authorization-timing.md` (to be created; OQ1) plus governance record; Architecture and Security approvers. |
| OQ2 | Publish the canonical file-policy vocabulary and exact allow/reject behavior for symlinks, Unicode/case collisions, encoding, binary/large files, include/exclude precedence, and safe-denial routing. | Architecture + Security + PM | Blocks final FR32–FR35 acceptance; close when the file-policy contract and cross-surface tests are approved. | `docs/contract/file-context-contract-groups.md`; PM, Architecture, and Security approvers. |
| OQ3 | Publish the canonical actor/access-state × protected-operation authorization matrix used as the release denominator. | Security/Authorization | Blocks the authorization completeness gate; close when the inventory covers every canonical actor and negative case in the Actors table and every family in the Protected operation families table, and records the chosen permission representation. | `docs/contract/authorization-matrix.md` (to be created; OQ3); Security and PM approvers. |
| OQ4 | Publish the supported-provider compatibility catalog: product/instance identity, observed versions/API profiles, accepted credential profiles, capability semantics, readiness outcomes, reconciliation check policy, per-call timeout, retry-limit, and backoff-cap ceilings, and the C12 provider live-drift evidence for GitHub and Forgejo. | Provider + Architecture | Blocks provider-ready status and provider contract acceptance; close when both providers pass against the catalog and C12 changes from reference-pending to approved. | `docs/contract/provider-compatibility-catalog.md`; Provider, Architecture, and PM approvers. |
| OQ5 | Replace the fail-safe but functionally empty FR58 search/status facade with evidence for authorized non-empty metadata-token results, indexing status, stale/unauthorized hit removal, and unavailable behavior. | Search/Delivery | Blocks FR58 implementation readiness; close when coverage and tests round-trip FR58 and both C13 operations. | `docs/exit-criteria/fr58-search-evidence.md` (to be created; OQ5); PM, Security, and Test approvers. |
| OQ6 | Replace seed-only console/read-model diagnostics with projection-backed readiness, lifecycle, lock, failure, timeline, and transition evidence. | Console + Projections/Delivery | Blocks console implementation readiness; close when positive, degraded, and replay scenarios populate approved projections. | `docs/exit-criteria/console-projection-evidence.md` (to be created; OQ6); PM, Operations/UX, and Test approvers. |
| OQ7 | Align architecture and contract evidence to the managed-tenant plus canonical provider/repository plus normalized-ref lock identity, including alias collisions. | Architecture + Locking/Delivery | Blocks FR25–FR29 implementation readiness; close when lock contracts, transitions, and tests use that identity. | `docs/contract/workspace-lock-contract-groups.md` and C6 evidence; Architecture and Security approvers. |
| OQ8 | Align architecture, Contract Spine, SDK, C13, storage/retention evidence, and tests to the all-mutations idempotency rule, read-key rejection, expired-key precedence, and minimal metadata-only consumed-key digest/tombstone evidence that keeps an expired key recognizable after replay-result expiry. | Architecture + Contract/Delivery | Blocks idempotency completeness; close when every mutating operation and read cell passes the rule and equivalent or conflicting reuse of an expired key deterministically returns `idempotency_key_expired` without re-execution or protected prior-intent disclosure. | `docs/contract/idempotency-and-parity-rules.md`, `docs/exit-criteria/oq8-idempotency-design.md`, and `docs/exit-criteria/oq8-idempotency-evidence.yaml` plus the versioned C13 snapshot and canonical consumed-key retention evidence; Architecture, Security, and Test approvers. |
| OQ9 | Prove incident access requires both incident-admin permission and current tenant/folder authorization, with C9 redaction (including replacement of confidential values in any event payload read in degraded mode) and denial audit. | Security + Console/Delivery | Blocks incident-view acceptance; close when positive, revoked, wrong-tenant, hidden-resource, degraded, and confidential-override tests pass. | `docs/exit-criteria/incident-access-evidence.md` (to be created; OQ9); Security and PM approvers. |
| OQ10 | Publish the release-calibration plan with frozen populations, exclusions, environments, scenarios, methods, evidence owners, and approval rules for SM1–SM8 and CM1–CM5. | PM + Test/Quality | Blocks use of metric results for release acceptance; close before calibration evidence is collected. | `docs/exit-criteria/release-calibration-plan.md` (to be created; OQ10); PM and Test/Quality approvers. |

An open item closes only when its canonical evidence exists, every accountable approver records their identity and approval date, and the governance record stores the approved status and the evidence version and digest. Delivery completion or a passing test alone cannot close an item, and any later evidence change reopens approval.

## Assumptions and PM Decisions Index

*Added 2026-09-08.* Every `[ASSUMPTION An]` tag in this document resolves here, and every product decision that the 2026-09-08 validation surfaced but that only the PM can make is listed as a PD item. An assumption stands until its named approver confirms or replaces it by the revisit date; a PD item stays open until the PRD decision log (`_bmad-output/planning-artifacts/.memlog.md`) records the decision.

### Assumptions

| ID | Assumption | Where it applies | Confirm or replace by |
| --- | --- | --- | --- |
| A1 | SM3 95%, CM2 5%, and CM3 10% are PM-set smoke thresholds over the designed release-calibration cohort; no governance record approves them. | Measurable Outcomes | PM, in the OQ10 release-calibration plan; revisit 2026-09-30. |
| A2 | The Hexalith integration register is the approval body and counting source for SM7; the two-integration and 80% thresholds are PM-set. | Business Success; SM7 | PM, in the OQ10 plan; revisit 2026-09-30. |
| A3 | Withdrawn 2026-09-08: the 24-hour mutation replay tier is recorded in the approved OQ8 design. | — | — |
| A4 | Withdrawn 2026-09-08: consumed-key evidence retention follows the approved C3 consumed-key class (tenant lifetime plus 400 days). | — | — |
| A5 | The caller-provided task identity is bound to the principal and delegation scope at workspace preparation, a delegated agent of the binding principal is the same task, one task identity binds to at most one workspace, and the identity is a correlation key, not a bearer credential. | Task identity; UJ7 | Security, in the OQ3 matrix and OQ7 evidence; revisit 2026-09-30. |
| A6 | File mutations are accepted commands applied to the local working copy with outcomes read through task/workspace status; only readiness, preparation, create/bind, and commit reach the provider; an unconfirmed local batch makes the workspace `dirty` rather than `unknown_provider_outcome`; a second mutation on one path is rejected until the first's outcome is known. | Command and Query Contract | Architecture, in the workspace and file contract groups; revisit 2026-09-30. |
| A7 | `expired` and `stale` both permit re-acquisition by the originating task; `stale` is an operator signal with no takeover and no automatic release; same-task acquisition while the lock is valid is idempotent. | Workspace State and Concurrency | Architecture and Security, in OQ1; revisit 2026-09-30. |
| A8 | A remote repository/ref identity may be bound by at most one managed tenant; cross-tenant attempts receive the safe denial, which hides Hexalith state but cannot hide a repository the caller already sees at the provider. | Workspace State and Concurrency; FR19 | PM and Security; revisit 2026-09-30. |
| A9 | A bound ref that advanced since preparation makes commit fail with the branch/ref-conflict result and no automatic rebase; the workspace stays `dirty`. | Task and Lock Completion Model | PM and Architecture; revisit 2026-09-30. |
| A10 | Cleanup leaves the lifecycle value unchanged and makes context queries return the content-unavailable policy result. | Workspace State and Concurrency; FR30 | Architecture. |
| A11 | Additive Contract Spine changes after a release snapshot reopen only the open release item whose evidence they change. | Public Surfaces | PM and Architecture; revisit 2026-09-30. |
| A12 | Tenant administrators hold the task-mutation and read families only through an explicit `write` or `read` grant; administration never implies file access. | Protected operation families and permissions | PM and Security, in OQ3; revisit 2026-09-30. |
| A13 | Legal-hold placement/release and tenant-deletion workflow surfaces are Phase 2; C3 rules apply in MVP. | Explicit MVP Non-Goals; FR13 | PM and Legal; revisit 2026-09-30. |
| A14 | Audit records older than the 400-day read-model class are served from retained audit records for the seven-year class. | UJ9; FR14 | Architecture, in the C3 evidence. |
| A15 | .NET (C#) is the only current-release SDK language. | SDK Requirements | PM with Architecture; revisit 2026-09-30. |
| A16 | `reconciliation_required` has no maximum residence time in MVP; it exits only through authoritative evidence or a recorded human escalation leading to a post-MVP repair operation. | Command and Query Contract | PM, together with PD2 (2026-09-30). |
| A17 | Folder grant levels are distinct: `write` implies `read`; `administer` implies neither. | Protected operation families and permissions | PM and Security, in OQ3; revisit 2026-09-30. |
| A18 | A workspace made `inaccessible` by revocation returns to `dirty` with its changes intact when the originating task's authority is restored inside the C3 seven-day window before cleanup. | Task and Lock Completion Model | Security, in OQ1; revisit 2026-09-30. |
| A19 | Archiving a folder frees its remote repository identity for a later authorized binding while retaining the binding as metadata. | Workspace State and Concurrency; FR13 | PM and Security; revisit 2026-09-30. |
| A20 | Lock release accepts only the caller-completed reason in MVP; the Spine's other release reasons are reserved for post-MVP repair. | Workspace State and Concurrency; FR29 | PM, together with PD2. |
| A21 | A `dirty` workspace holding no staged changes returns to `ready`/`unlocked` when its lock becomes `stale`. | Workspace State and Concurrency; Task and Lock Completion Model | PM and Architecture, in PD11; revisit 2026-09-30. |
| A22 | File-body context reads require the `write` grant; `read` grants metadata-level families only. | Protected operation families and permissions | PM and Security, in OQ3; revisit 2026-09-30. |
| A23 | Tenant-administrator authority implies `administer` on every folder; the creator of a logical folder receives `administer` on it. | Protected operation families and permissions; FR11 | PM and Security, in OQ3; revisit 2026-09-30. |

### PM decisions pending

| ID | Decision needed | Options on the table | Owner | Revisit condition |
| --- | --- | --- | --- | --- |
| PD1 | Admit the durable repository-backed round trip (Epic 12, the ratified MVP-completion gate) to this PRD's release inventory. | (a) Add it as OQ11 (text drafted in `reconcile-july-2026-synthesis.md` §8) with owner Persistence + Git/Delivery and approvers PM, Architecture, Security, and Test, and make OQ5, OQ6, and OQ7 depend on it; (b) record in the memlog that the Current Delivery Posture prose gate is sufficient. | PM | Before the next implementation-readiness assessment. |
| PD2 | Provide an MVP exit for a task that can never resume while changes remain staged or the workspace is `reconciliation_required`. | (a) Add a governed, audited discard/abandon mutation for the task owner or tenant administrator that returns the workspace to `ready`/`unlocked` (a scope addition to FR29/FR30 and the completion model; the approved C6 mapping already carries `OperatorDiscardRequested`, `OperatorRetrySucceeded`, and `OperatorMarkedFailed` events and the Spine already declares abandon/cancel release reasons, which favours this option); (b) keep the no-discard rule and accept that such a repository/ref stays serialized for the tenant until Phase 2 repair commands. Default in force since 2026-09-08: (b); a non-resumable task is a counted class for CM2 and CM3 and is never excluded as an injected failure. Under (b) the only loss of staged work is platform cleanup after a task-terminal `failed`/`inaccessible` closure. PD11 carries the C6 re-approval either option needs. | PM with Security | 2026-09-30, or before OQ1 and OQ7 close if earlier. |
| PD3 | Admit the Epic 13 security/operations hardening (Forgejo SSRF egress guard, fail-safe fallback authorization, sidecar-only app port, credential-file permissions, real readiness snapshot and health endpoints, alert instruments, rate limits/timeouts/body caps) to this PRD. | (a) Ratify the OQ12/OQ13 text drafted in `reconcile-july-2026-synthesis.md` §8 and add the corresponding NFR bullets through the lockstep relock in PD6; (b) state that Epic 13 gates release through the architecture authority outside product scope. | PM with Security | Before the next implementation-readiness assessment. |
| PD4 | Resolve the tracking conflict in which Stories 10.7 and 10.8 are `done` in `sprint-status.yaml` on hermetic or fail-closed evidence while this PRD's posture rule, OQ5, and the planning manifest say such evidence cannot close FR58 work; the same manifest-versus-sprint-status divergence covers the Epic 3 rows 3.10/3.12 and the Epic 11 rows 11.2–11.4, and the manifest has not been regenerated since 2026-08-04. | (a) Reopen 10.7 and 10.8 (or relabel them component increments) and keep OQ5 open; (b) ratify the narrowed done-bars in the manifest and memlog. No PRD change either way. | PM with Architecture | Immediately; the manifest and sprint status must agree before further Epic 10 work. |
| PD5 | Story 10.9 (body-content materialization) is `review` in sprint status (moved on 2026-09-08) with a spec whose frontmatter says `done` while its body says incomplete, for a capability this PRD lists as a non-goal pending Security and PM approval. | (a) Hold 10.9 at `backlog` until the C9 body-content approval is recorded, then add a new FR under a stable ID and amend Non-Goals and Search Families; (b) confirm 10.9 may only prove unavailability. No PRD change until approval. | PM with Security | When the C9 body-content approval is recorded or refused. |
| PD6 | Apply the validation findings that touch the 73 hash-pinned NFR bullets: rewrite the lock "must define" bullet into rules, remove the performance-target escape hatch, add provider timeout/retry/backoff ceilings, cite WCAG 2.2 success criteria 1.4.3, 1.4.4, 1.4.10, and 1.4.11, attach thresholds to the versioning, backup, and artifact-traceability bullets, and restate the read-model retention rule. | Requires one lockstep change of `prd.md`, `epics.md` NFR1–NFR73, and the `docs/exit-criteria/nfr-traceability.md` hashes, as in the 2026-08-24 relock. | PM with Architecture | At the next NFR traceability relock. |
| PD7 | Structural clean-up deferred by the 2026-07-15 decision to preserve repeated normative summaries: reclassify FR1/FR2 as documentation and gate deliverables, retire FR36 as a restated non-goal, and reduce Innovation & Novel Patterns and Market Context to one paragraph. | Defer to the next major PRD revision after OQ1–OQ10 close, as previously decided. | PM | Next major PRD revision. |
| PD8 | Fix the point at which a confidential-override value is replaced by its correlation token: at event write, or only at audit/projection write with the incident view applying replacement on read. | (a) Event write (no cleartext ever durable); (b) audit/projection write plus read-time replacement in the incident view, proven by OQ9. The C9 row states (b) as the latest permitted point. | Architecture with Security | Before OQ9 closes. |
| PD9 | Keep FR58 (metadata-token recall) in the MVP Must-Have list, or move it to Phase 2 and drop OQ5 from the release gate. | (a) Keep, with the rationale recorded under FR58; (b) move to Phase 2, retire OQ5, and re-scope Epic 10 to the live-workspace queries. Default in force since 2026-09-08: (a). | PM | Before OQ5 evidence collection starts. |
| PD10 | Correct the Contract Spine where it conflicts with product-owned invariants introduced or clarified on 2026-09-08: the leaking categories are declared as caller-visible responses on 24 of 49 operations (`not_found` on 24, `cross_tenant_access_denied` on six, `audit_access_denied` on four); the safe denial is declared as two status-distinct envelopes (403 and 404) on 46 of 49 operations and must collapse to one status per operation; `details.visibility` is not a required closed field; the FR44 family-to-category mapping does not exist; the release-reason enum is broader than MVP; and the permission representation (three levels versus per-family grants) awaits OQ3. | (a) Amend the Spine and regenerate the SDK, C13 inventory, and parity evidence before the release snapshot; (b) if any item is contested, escalate to PM for a product-rule change under a stable ID. | Architecture with Contract/Delivery and Security | Before the C13 release snapshot. |
| PD11 | Re-approve the C6 transition matrix (mapping, architecture matrix, and `FolderStateTransitions.cs`) in the direction of this PRD's completion model, or amend the completion model under stable IDs where the PM accepts the matrix's behaviour. Scope: originating-task resume out of `dirty`; retryable commit failure staying `dirty` versus `failed`; revocation with staged changes; restored authority; clean `dirty` to `ready` at `stale` (A21); the `unknown_provider_outcome` disposition (mapping `awaiting-human`, architecture `auto-recovering`, PRD auto-reconciling); the operator discard/retry/mark-failed events (with PD2); and the C3 record's lock-expiry and cancellation cleanup triggers. | (a) Amend and re-approve C6 and C3 to match this PRD; (b) PM accepts specific matrix behaviours and the completion model is amended. | PM with Architecture and Security | 2026-09-30, before OQ7 closes. |
