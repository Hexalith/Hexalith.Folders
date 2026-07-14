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
documentCounts:
  productBriefs: 1
  research: 3
  brainstorming: 1
  projectDocs: 0
  projectContext: 0
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
updated: '2026-07-14'
finalized: '2026-07-14'
completedAt: '2026-05-07'
lastEdited: '2026-07-14'
editHistory:
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

Hexalith.Folders gives chatbot developers a tenant-scoped, task-oriented API for preparing workspaces, acquiring task-scoped locks, applying governed file changes, querying file context through tree, search, glob, metadata, and partial-read operations, persisting Git-backed work, and recovering from interrupted tasks. It centralizes workspace lifecycle, file-operation audit metadata, GitHub and Forgejo provider handling, credential references, and operational state so each chatbot or automation layer does not need to rebuild fragile storage orchestration.

The product serves three primary stakeholder groups. Chatbot developers can persist task output without writing custom filesystem or Git orchestration. Operators can diagnose workspace state before and after agent execution, including stale locks, dirty worktrees, failed provider syncs, incomplete provisioning, and misconfigured workspaces. Tenant administrators can trust that folders, credentials, permissions, and task artifacts remain isolated and auditable across tenants.

### What Makes This Special

Hexalith.Folders is not a generic file manager, Git UI, chatbot interface, agent runtime, prompt orchestration layer, or execution sandbox. Its differentiated value is turning project storage into a task lifecycle API: prepare a workspace, claim exclusive work, apply bounded changes, persist results, expose relevant context, and recover from failure.

The core insight is that production AI agents need narrow, task-oriented workspace primitives rather than direct ownership of filesystem paths, credentials, Git remotes, and recovery workflows. The product wins when chatbot builders stop writing bespoke workspace orchestration and delegate file isolation, Git persistence, locking, auditability, context access, and recovery to one tenant-aware service.

Users choose Hexalith.Folders over improvised temp folders, direct Git CLI automation, shared provider tokens, unrestricted file traversal, and cleanup scripts when they need agentic file work to be observable, recoverable, auditable, and tenant-scoped.

## Project Classification

**Project Type:** API/backend service module with supporting CLI and MCP surfaces.

**Domain:** Developer infrastructure and AI workspace storage.

**Complexity:** High. The complexity comes from multi-tenant authorization, event-sourced aggregates, GitHub and Forgejo provider support, local and Git-backed storage modes, workspace locking, metadata-only audit events, file context queries, and operational state projections.

**Project Context:** Greenfield at inception within a mature Hexalith ecosystem; now maintained as a brownfield living product contract. Current repository contracts and approved governance artifacts take precedence over historical delivery-status prose, while this PRD remains authoritative for product intent, release scope, actors, and user-visible outcomes.

## MVP Contract Summary

The MVP proves one job: an AI agent can safely prepare, modify, and commit files in a tenant-scoped repository workspace through one canonical contract.

The canonical lifecycle is provider readiness, repository-backed folder creation or binding, workspace preparation, task-scoped lock acquisition, governed file changes, commit, context query, status inspection, and metadata-only audit. Product intent and release scope are defined here. The OpenAPI 3.1 Contract Spine is the canonical machine-readable operation and schema contract; the generated SDK is the typed canonical client; REST is the required public runtime transport; CLI and MCP wrap the SDK. For every C13-supported surface cell, the four contract surfaces preserve the same states, authorization checks, idempotency rules, and error taxonomy; a diagnostic operation may be explicitly not applicable under the product-approved C13 rule. The console is a read-only diagnostic surface, not an independent command model.

Authority conflicts fail the release gate rather than being resolved at runtime. This PRD prevails for product intent, actors, scope, safety invariants, and user-visible outcomes; the Contract Spine prevails for operation names, wire schemas, and closed error fields. A conflict requires both artifacts to be reconciled and re-approved before shipping. The generated SDK, REST-emitted schema, CLI, MCP, parity oracle, and tests have no authority to override either source; any drift is a failing conformance defect.

The MVP is repository-backed first. It supports creating a provider-backed folder and binding a pre-created provider repository that passes readiness, access, duplicate-binding, and branch/ref-policy checks. Importing unmanaged local working trees or folders, migration assistance, local-first promotion, history rewriting, and repair of pre-existing dirty state are post-MVP.

The product has one non-negotiable content boundary: file contents, diffs, generated context payloads, provider tokens, credential values, secret material, and unauthorized resource existence must not appear in events, logs, traces, metrics, audit records, console views, provider diagnostics, or error responses.

## Success Criteria

### User Success

Chatbot developers succeed when they can complete the core repository-backed workflow without writing custom filesystem, Git, provider-credential, or cleanup orchestration: create or bind a provider-backed folder, prepare and lock a workspace, add/change/remove files, produce a provider-confirmed durable commit, and query final workspace and audit state.

Tenant-scoped operators succeed when they can inspect whether a workspace is usable before and after agent execution. The read-only console must expose provider readiness, workspace readiness, lock state, dirty/uncommitted state, last confirmed commit, failed operation state, and sync/provider status without direct filesystem inspection. MVP operators work only inside an explicitly authorized tenant and folder scope; global cross-tenant browsing and break-glass access are not MVP capabilities.

AI tool integrations succeed when the same core lifecycle is available through the Contract Spine, REST, generated SDK, CLI, and MCP with consistent authorization, idempotency, states, errors, and audit outcomes.

### Business Success

Three-month success means at least two independent Hexalith agent or chatbot integrations complete the canonical workflow through the public contract without direct filesystem paths, Git CLI orchestration, provider credentials, or Folders implementation knowledge.

Twelve-month success means at least 80% of newly approved Hexalith agentic file-work integrations use Hexalith.Folders as their workspace persistence layer rather than introducing bespoke temporary-folder, Git, token, or cleanup orchestration. This target is reviewed after the first 90 days of production evidence.

### Technical Success

The MVP must prove the complete repository workflow for both supported providers and all four contract surfaces: create or bind repository, prepare and lock, add/change/remove files, confirm the durable commit outcome, query context/status, and expose metadata-only audit.

The highest-priority technical measures are zero cross-tenant access leaks and deterministic completion evidence. Cross-tenant authorization failures must be caught before any file, workspace, credential, repository, lock, commit, provider, audit, or context resource is touched or inferred.

Provider readiness checks must prevent avoidable runtime failures by validating provider configuration, credential reference availability, required GitHub/Forgejo capabilities, repository provisioning readiness, and default branch policy before repository-backed folder creation.

The Contract Spine, REST, generated SDK, CLI, and MCP must expose the same core lifecycle and preserve consistent authorization, audit, error handling, idempotency, and state semantics.

### Measurable Outcomes

- **SM1 — Canonical lifecycle:** 100% of declared release-calibration scenarios pass for GitHub and Forgejo through REST, SDK, CLI, and MCP, including create and bind variants, failure paths, and durable commit confirmation.
- **SM2 — Isolation:** zero tolerated cross-tenant leaks across commands, queries, errors, events, logs, metrics, projections, caches, locks, working paths, provider access, audit, and context results.
- **SM3 — Task completion:** at least 95% of authorized canonical lifecycle runs in the 30-day release-calibration cohort reach the committed state; explicitly injected provider outages and policy denials are excluded from the numerator and denominator but must end in their expected safe state.
- **SM4 — Latency and freshness:** command acknowledgement is at most 1 second p95; bounded status/audit summaries are at most 500 ms p95; bounded context queries are at most 2 seconds p95 with a 2-second hard execution limit; commit-to-status visibility lag is at most 500 ms in the approved release-calibration path.
- **SM5 — Capacity:** the release gate sustains 4 concurrent tenants, 2 folders per tenant, 2 active workspaces per tenant, 2 concurrent agent tasks per tenant, and at least 1 lifecycle operation per second without cross-scope interference.
- **SM6 — Diagnostic completeness:** 100% of injected lifecycle failures expose state, safe cause category, retryability, client action, correlation ID, and metadata-only audit evidence; no accepted task is left without an inspectable state.
- **SM7 — Adoption:** within 3 months, at least two independent first-party integrations use the public lifecycle without bespoke workspace orchestration; within 12 months, at least 80% of newly approved Hexalith agentic file-work integrations use Hexalith.Folders.
- **SM8 — Agent context effectiveness:** 100% of approved context benchmark tasks retrieve the expected authorized tree/metadata/search/glob/range evidence within C4 limits and allow the agent to identify the intended edit target without unrestricted repository browsing.

Counter-metrics prevent a hollow win:

- **CM1 — Unsafe completion:** zero tasks may count as successful when authorization is stale, the lock is invalid, the remote commit is unconfirmed, or protected data is exposed.
- **CM2 — Recovery burden:** no more than 5% of accepted release-calibration tasks may end in dirty, unknown-provider-outcome, or reconciliation-required states; every such task must remain inspectable.
- **CM3 — Operator burden:** no more than 10% of accepted release-calibration tasks may require manual intervention, excluding deliberate failure injection.
- **CM4 — Surface drift:** zero current Contract Spine operations may lack the required C13 parity declaration, and no adapter may report a materially different authorization, state, error, or audit outcome for the same scenario.

The canonical release-calibration plan at `docs/exit-criteria/release-calibration-plan.md` must freeze each metric's population, exclusions, environment, scenario set, measurement method, evidence owner, and approval record before results are accepted. OQ10 tracks creation and approval of that plan.

## Product Scope

### MVP - Minimum Viable Product

The MVP includes the repository-backed task workflow: provider readiness, logical folder creation, provider-backed repository creation or binding, workspace preparation, locking, add/change/remove file operations, provider-confirmed durable commit, context/status queries, and metadata-only audit.

The MVP is repository-backed first. Binding a pre-created provider repository is supported when readiness, authorization, duplicate-binding, and branch/ref policy pass. Importing an unmanaged local working tree or folder, migration assistance, local-first promotion, history rewriting, and repair of pre-existing dirty state are excluded.

This is an intentional scope change from the Product Brief. The Product Brief included limited local storage and local-to-Git upgrade in MVP; the PRD narrows MVP to the repository-backed workflow so the first release can prove tenant isolation, provider readiness, task locking, file operations, commit, status, and audit before expanding storage modes.

The MVP includes a tenant-scoped, read-only operations console focused on trust signals: provider readiness, workspace readiness, lock state, dirty state, commit state, failed operation state, and provider/sync status. Normal views use projections. When projections are degraded, an explicitly authorized incident view may expose bounded metadata-only event evidence with a persistent degraded-state warning, the last projection checkpoint, correlation/time-window context, and C9 redaction; it remains read-only and never exposes content, credentials, diffs, or repair controls.

The MVP includes the Contract Spine, REST transport, generated SDK, CLI, and MCP for the core lifecycle, plus the diagnostic console. CLI and MCP must preserve the generated SDK and Contract Spine semantics rather than creating independent models.

The MVP must enforce tenant-scoped authorization and must prevent cross-tenant file, credential, workspace, and repository access.

### Growth Features (Post-MVP)

Post-MVP growth expands reliability and operational depth after the core workflow is proven. Candidate growth features include repair commands, deeper drift detection, richer provider contract tests, unmanaged local-folder/repository migration, large-file policy expansion, advanced provider capability recipes, broader operations workflows, and technical platform-alignment work that removes local copies of shared Hexalith platform capabilities without changing product semantics.

Known post-MVP pressure points include local-first folders that promote to Git-backed storage, auto-commit command mode, disposable working-copy repair, evented repair console workflows, drift-first operations views, multiple Git organizations per tenant, and module-managed local storage policy.

### Vision (Future)

The long-term vision is for Hexalith.Folders to become the default durable workspace substrate for Hexalith AI agents. In that future state, agentic file work is consistently tenant-scoped, observable, recoverable, auditable, provider-portable, and accessible through stable API, CLI, and MCP surfaces.

## User Journeys

### UJ1: Developer Proves Agentic File Work Is Production-Ready

Nadia is building a chatbot that must work on real project files for tenant customers. Her concern is not whether she can script Git; she already can. Her concern is whether she can let an AI agent touch project files without creating tenant leaks, unrecoverable partial changes, hidden dirty state, or provider-specific Git logic inside the chatbot.

She starts with a provider readiness check for the tenant organization. The system confirms that the provider binding exists, the credential reference resolves, repository creation is supported, the default branch policy is valid, and the tenant has permission to create the repository. Nadia then creates a Git-backed folder, prepares the workspace, and runs the core task flow through CLI or MCP: add files, change files, remove files, commit the change set, and query workspace status.

The value moment is not repository creation by itself. It is the clean final state: the workspace is `committed`, no hidden dirty state remains, the commit SHA is visible, the changed paths are traceable to tenant/folder/task metadata, and the chatbot did not need direct filesystem paths, provider credentials, or Git CLI orchestration.

This journey reveals requirements for provider readiness checks, repository-backed folder creation, workspace preparation, governed file operations, commit support, workspace status queries, CLI/MCP access, task/correlation metadata, and clean committed-state reporting.

### UJ2: Platform Engineer Establishes Tenant Provider Readiness

Ravi is a platform engineer responsible for making a tenant organization ready for Git-backed agent work. Before any agent can create a workspace, he must configure the provider binding, credential reference, repository naming policy, default branch policy, and minimum provider capabilities.

Ravi runs readiness validation and sees a clear state rather than a generic failure. If a credential reference is missing, a token lacks repository creation permission, a provider is unavailable, or a default branch policy is invalid, the system reports a stable machine-readable reason code and human-readable diagnosis. Secrets are never displayed. The readiness result tells Ravi whether the tenant is `ready`, `failed`, or blocked by configuration.

The value moment is confidence before runtime. Ravi can correct provider setup before an agent task fails halfway through repository creation or commit.

This journey reveals requirements for organization-level provider configuration, credential references, repository policy validation, provider capability checks, readiness status projection, actionable readiness reason codes, and secret-safe error handling.

### UJ3: Agent Task Is Interrupted and Leaves Inspectable State

Asha, an AI agent running Nadia's tenant-scoped task, starts work against an existing Git-backed folder. She prepares the workspace, acquires a task-scoped lock, applies several file changes, and then the task is interrupted before commit.

Without Hexalith.Folders, the team would be left with uncertain state: partial files, no reliable lock owner, no clear task correlation, and no trustworthy recovery signal. With Hexalith.Folders, the workspace enters an inspectable blocked, non-terminal state. It is visibly `locked`, `dirty`, and associated with the interrupted task. The changed file list is available as metadata, the last successful operation is visible, and the absence of a commit is explicit.

The MVP does not silently repair, discard, or commit the changes. It makes the state understandable and prevents another task from overwriting the workspace accidentally. Post-MVP repair commands may support retry commit, discard changes, rebuild cache, or release stale locks.

This journey reveals requirements for task-scoped locks, lock ownership metadata, dirty workspace detection, interrupted task status, operation timeline, changed-path metadata, safe blocked state, and post-MVP repair workflows.

### UJ4: Concurrent Agent Is Denied by Workspace Lock

Nadia starts Asha on a tenant folder, then a second agent named Borel targets the same effective repository/ref through another surface. Asha's task prepares the workspace and acquires the lock. Borel then tries to prepare, write, or commit against the same serializing identity through MCP or API.

The system rejects the second task with a deterministic lock response. The denial includes safe metadata: lock state, lock owner/task reference if authorized, lock age, and retry eligibility policy. It does not allow mixed writes, overlapping commits, or silent lock takeover.

The value moment is trust under pressure. Nadia and the operator can see that lock contention did not corrupt the workspace and did not produce a mixed commit.

This journey reveals requirements for exclusive workspace locking, consistent lock denial across API/CLI/MCP, lock status visibility, retry/idempotency policy, no-lost-update behavior, and structured lock error responses.

### UJ5: Tenant-Scoped Operator Diagnoses Provider or Credential Failure

Marcus maintains the Hexalith platform. A tenant reports that an agent task cannot complete. Marcus enters an explicitly authorized tenant and folder scope, then opens the read-only operations console. MVP does not permit global cross-tenant search or unscoped break-glass browsing.

The console does not present a flat wall of infrastructure facts. It answers three questions first: what is broken, who or what is affected, and what can safely happen next. Marcus sees that the workspace is `failed`, the provider readiness check now fails because the credential reference no longer has required permissions, and the last commit attempt did not succeed. The console shows supporting evidence: tenant, folder, provider binding, credential reference identifier, failed operation type, last successful operation, lock/dirty state, and timestamp.

The console remains read-only in MVP. It does not reveal credential material, expose file contents, mutate workspace state, or hide repair actions behind undocumented controls. Marcus can diagnose and escalate or correct configuration through the proper provider/tenant administration path.

This journey reveals requirements for a read-only operations console, workspace trust surface, primary diagnosis, provider/credential failure states, failed operation projection, secret-safe status display, no mutation path, and clear escalation posture.

### UJ6: Tenant Administrator Proves Cross-Tenant Isolation

Elise administers a tenant organization that wants to enable agentic file work. Her concern is whether another tenant, agent, or integration surface can discover or touch her tenant's workspaces, provider bindings, credential references, commits, locks, or audit records.

She validates isolation with concrete evidence. A wrong-tenant request attempts to query workspace status, inspect provider configuration, prepare a workspace, acquire a lock, write a file, commit changes, and read audit metadata. Each attempt is denied before file, workspace, credential, repository, or commit access occurs. The denial shape is safe and does not leak whether unauthorized resources exist beyond what policy allows.

The value moment is proof. Elise can show that tenant isolation is enforced across API, CLI, MCP, and console surfaces, and that denial events are captured as metadata-only audit records.

This journey reveals requirements for tenant-scoped authorization, cross-surface authorization parity, safe error shapes, no cross-tenant enumeration, credential reference isolation, denial audit events, and zero tolerated cross-tenant access leaks.

### UJ7: MCP/CLI/API Consumer Sees Cross-Surface Parity

Theo, an automation developer, starts a workflow in the CLI to validate provider readiness and create a folder. Later, his AI tool continues through MCP to prepare the workspace, write files, and request a commit. Marcus inspects the same workspace through the console, while Theo's service integration queries status through the API.

Every surface reports the same truth. The workspace state, error categories, tenant authorization behavior, lock behavior, idempotency behavior, task/correlation metadata, and audit outcomes are consistent. If CLI says the workspace is `dirty`, MCP and API report `dirty`. If API returns a lock denial, MCP receives the same category of denial.

The value moment is operational confidence. Different entry points do not create different product semantics.

This journey reveals requirements for one canonical workflow contract, thin API/CLI/MCP adapters, shared status model, shared structured errors, shared authorization enforcement, shared audit metadata, and parity tests for MVP workflow commands.

### UJ8: Audit Reviewer Reconstructs What Happened Without File Contents

Priya, a security reviewer, investigates a customer incident after an agent task changed project files. She needs to answer who or what changed which paths, in which tenant and folder, under which task or correlation ID, with what outcome, and whether a durable Git commit was produced.

The audit view shows metadata only: tenant ID, folder ID, actor/client type, task/correlation ID, operation type, path metadata, lock lifecycle, status transitions, provider reference, commit SHA when available, timestamps, and denial events. It does not show file contents, provider tokens, credential material, or secrets.

The value moment is accountable traceability without data exposure. The reviewer can reconstruct the operational story and prove whether the agent task completed, failed, was denied, or left dirty state.

This journey reveals requirements for metadata-only audit events, audit projections, path-level change metadata, commit SHA capture, lock lifecycle audit, denial event capture, secret/file-content exclusion, and incident-support queries.

### UJ9: Tenant Administrator Manages Folder Access and Lifecycle

Elise, the tenant administrator from UJ6, returns to the day-to-day operation of her tenant. Her concern is no longer "can isolation be proven" but "can I run the tenant cleanly". She needs to grant folder access to users, groups, roles, and delegated service agents as her teams onboard new chatbots, retire old ones, and rotate ownership; she needs to inspect effective permissions for a folder so that an audit ask ("who can write here today?") has a concrete answer; and she needs to retire folders whose tasks are finished without erasing the audit evidence that future incident reviews depend on.

Elise grants folder access to a new automation team and verifies through the same surface that the grant took effect — the effective-permissions view shows the grant, the actor identity, and the verb scope. Months later, when a chatbot project is retired, Elise archives the folder. The folder enters a clearly archived lifecycle state, mutating commands are denied with a stable error, but the metadata-only audit trail, lock lifecycle history, last commit reference, and operation timeline remain queryable for the duration of the tenant's retention policy. No file contents, provider tokens, or credential material are revealed by the archived view; the same denial and isolation guarantees that protect active folders also protect archived ones.

The value moment is operational confidence. Elise can run her tenant — granting and revoking folder access, retiring folders, and answering audit questions — using the same product semantics, cross-surface parity, and metadata-only audit posture that the rest of the canonical workflow already enforces.

This journey reveals requirements for tenant-administrator folder access grant and revoke (FR5), effective-permissions inspection (FR6), folder lifecycle and archive (FR11–FR13), audit and status preservation for archived folders (FR14), denial of mutating operations on archived folders, retention-bound audit visibility, and cross-surface parity for tenant-administration commands and queries.

### Journey Requirements Summary

The journeys reveal these required capability areas:

- Canonical workspace lifecycle and separate lock-state vocabularies as defined in Workspace State and Concurrency.
- Provider readiness checks before repository-backed folder creation.
- Stable provider readiness reason codes for missing credentials, insufficient permissions, provider unavailability, unsupported capabilities, invalid branch policy, repository conflict, and rate limiting.
- Tenant-scoped organization/provider configuration and credential references.
- Git-backed folder/repository creation with narrow MVP provider behavior.
- Workspace preparation and task-scoped locking.
- Deterministic lock contention behavior across API, CLI, and MCP.
- Idempotency for every mutating Contract Spine operation; while the record is unexpired within its declared retention tier, equivalent replays preserve one logical result and conflicting intent is rejected. An expired key returns `idempotency_key_expired` and never executes automatically as a new request.
- Governed file add/change/remove operations with workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, and case-collision handling.
- Commit support for Git-backed folders with tenant, actor, task/correlation, changed-path, and commit SHA metadata.
- File context queries through tree, search, glob, metadata, and partial reads.
- Workspace status queries and trust projections.
- Read-only operations console for readiness, lock state, dirty state, failed operation, last commit, credential reference status, and provider/sync status.
- No mutation path, credential reveal, or file-content browsing in the MVP read-only console.
- Metadata-only audit events, including successful operations, failed operations, status transitions, lock lifecycle, commit references, and authorization denials.
- Cross-tenant access prevention before file, workspace, credential, repository, lock, commit, provider, or audit access.
- Safe error shapes that avoid unauthorized resource enumeration.
- Consistent API, CLI, and MCP command/query semantics.
- Tenant-administrator folder access grant and revoke for users, groups, roles, and delegated service agents, with effective-permissions inspection.
- Folder lifecycle including archive with retention-bound, metadata-only audit and status visibility for archived folders.
- Provider contract tests for GitHub and Forgejo readiness, repository creation, file/commit workflows, credential failures, permission failures, conflicts, rate limits, timeouts, and provider drift.
- Future repair workflows for interrupted tasks, stale locks, dirty workspaces, provider sync failures, and drift, explicitly outside the MVP read-only console.

## Innovation & Novel Patterns

### Detected Innovation Areas

Hexalith.Folders introduces two main innovation patterns within the Hexalith ecosystem.

The first is an AI-native task lifecycle workspace API. Instead of exposing folders as raw filesystem paths or treating Git as a caller-owned concern, the product models agentic file work as a managed task flow: prepare a workspace, acquire a lock, apply bounded file changes, commit Git-backed results, query context, inspect state, and recover from interruption.

The second is a canonical workspace control plane exposed through API, CLI, and MCP surfaces. These surfaces must not become separate product models. They share the same command/query semantics, authorization behavior, workspace states, error categories, idempotency rules, and audit metadata.

A supporting innovation is the workspace trust surface. Hexalith.Folders makes operational states such as `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible` first-class product signals. This lets developers, operators, tenant administrators, and AI tools reason about whether a workspace can be trusted before, during, and after agent execution.

### Market Context & Competitive Landscape

The innovation is positioned primarily inside Hexalith. The comparison point is not a mature external product category; it is the internal pattern Hexalith.Folders is replacing: improvised temporary folders, direct Git CLI calls, scattered provider tokens, inconsistent cleanup logic, and chatbot-specific workspace orchestration.

Within Hexalith, Hexalith.Folders should become the common workspace persistence boundary for AI agents. Its value comes from standardizing tenant-scoped file work, Git-backed persistence, readiness checks, locking, status visibility, and audit metadata behind one product model.

### Validation Approach

The innovation is validated if the MVP proves that a chatbot or AI tool can complete the repository-backed task flow through the canonical contract: provider readiness, repository-backed folder creation, workspace preparation, task lock, add/change/remove files, commit, and status query.

The API, CLI, and MCP surfaces validate the same workflow behavior rather than three separate implementations. Validation confirms consistent workspace states, authorization checks, structured errors, idempotency behavior, and metadata-only audit across all surfaces.

The workspace trust model is validated if operators can understand key states without inspecting the filesystem or Git provider manually: whether the workspace is ready, locked, dirty, committed, failed, inaccessible, or blocked by provider readiness.

### Risk Mitigation

The main innovation risk is overbuilding the control plane before proving the core file workflow. If the AI-native task lifecycle model proves too heavy for the MVP, the fallback is a simpler Git-backed file API that supports repository creation, file add/change/remove operations, commit, and status query.

The second risk is surface divergence. API, CLI, and MCP could drift into different semantics. Mitigation is one canonical workflow contract with CLI and MCP as adapters over the same command/query model.

The third risk is provider abstraction leakage. GitHub and Forgejo support requires explicit capability checks and provider contract tests rather than assumed API compatibility.

The fourth risk is scope creep from the read-only operations console into a repair or Git administration console. MVP keeps the console diagnostic-only, with repair workflows deferred until the workspace state model is proven.

## API Backend Specific Requirements

### Project-Type Overview

Hexalith.Folders is an API/backend service module with REST API, CLI, MCP, SDK, projection, and read-only console surfaces. These surfaces must expose one canonical workspace workflow contract rather than separate product models.

The product is a tenant-scoped workspace control plane for agentic file work. It is not a content store, Git implementation, identity provider, generic Git provider management platform, prompt orchestration layer, execution sandbox, or repair console.

The MVP must support the repository-backed task flow: validate provider readiness, create or bind a Git-backed folder, prepare a workspace, acquire a task lock, add/change/remove files, commit Git-backed changes, query workspace and file context, inspect metadata-only audit, and expose diagnostic state through the read-only console.

### Architectural Boundaries

Hexalith.Tenants remains the source of truth for tenant identity, tenant lifecycle, and tenant membership. Hexalith.EventStore provides command, aggregate, event, projection, query, cursor, read-model, and domain-service mechanics where those mechanics are platform-owned. Hexalith.Commons, Hexalith.FrontComposer, and Hexalith.Memories provide shared boilerplate for cross-module helpers, UI shell behavior, and search-index integration where applicable. Hexalith.Folders owns folder-specific policy, folder ACLs, provider binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.

Hexalith.Folders owns intent, policy, state, and audit. Git providers own provider-specific repository and Git mechanics behind narrow provider ports. File contents and temporary working-copy material must remain outside EventStore; events, logs, projections, traces, and console responses must never contain file contents, provider tokens, credential material, or secrets.

Required provider ports include readiness validation, repository creation or binding, workspace preparation, governed file-operation application, Git-backed commit, provider status query, and cleanup/expiration support where needed.

### Public Surfaces

The OpenAPI 3.1 Contract Spine is the canonical machine-readable operation and schema contract. REST is the required public runtime transport and must emit a versioned contract that validates against that spine. The generated SDK is the typed canonical client; CLI and MCP wrap the SDK and preserve Contract Spine behavior.

REST, generated SDK, CLI, and MCP are required current-release contract surfaces for the core lifecycle. They must preserve the same workspace states, authorization checks, idempotency rules, lock behavior, structured error categories, correlation metadata, and audit outcomes. The console is query-only and is not part of the mutation-parity surface.

C13 is the single surface-coverage denominator. Every Contract Spine operation has one C13 row declaring required support for REST, SDK, CLI, and MCP. Core lifecycle and tenant-administration contract operations are required on all four surfaces. A diagnostic/read-model operation may be marked not applicable for CLI or MCP only through an explicit product-approved C13 declaration with rationale; no operation may be silently absent, and every supported cell must pass behavioral parity.

Release acceptance freezes an immutable, versioned snapshot containing the Contract Spine version and digest plus the matching C13 inventory version and digest. Any operation, schema, support-cell, or rationale change after approval invalidates prior parity evidence and requires a new reviewed snapshot.

The read-only operations console must normally consume projections. When projections are degraded, an authorized incident view may expose bounded event-stream evidence only to an actor holding the incident-admin permission and the normal tenant/folder access. The view must remain metadata-only and read-only, enforce C9 redaction, show a persistent degraded-state warning and last projection checkpoint, and expose correlation/time-window context. It must not expose mutation paths, credentials, file contents, diffs, repair controls, or unrestricted event/filesystem browsing.

### Endpoint Specifications

The API is organized around capability groups:

- Provider readiness: validate provider binding, credential reference availability, provider capabilities, branch policy, repository naming policy, and provisioning readiness.
- Folder/repository lifecycle: create Git-backed folder/repository, bind existing repository where supported, inspect folder/repository binding metadata, and reject duplicate or cross-tenant bindings.
- Workspace lifecycle: prepare workspace, inspect workspace state, acquire lock, inspect lock, release lock where policy allows, and surface stale or interrupted lock state.
- File operations: add, change, and remove files while enforcing workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding policy, and case-collision handling.
- Commit operations: commit Git-backed changes with tenant, actor, task/correlation, author, message, changed-path, and commit SHA metadata.
- Query/status operations: expose workspace state, provider readiness, folder status, file tree, search, glob, metadata, partial reads, dirty state, failed operations, projection status, and last commit.
- Audit operations: expose metadata-only audit records for operations, status transitions, lock lifecycle, commit references, and authorization denials.
- Operations console queries: expose read-only projections for readiness, lock state, dirty state, failed operation, credential reference status, and provider/sync status.

Context queries are controlled workspace operations, not unrestricted repository browsing. Tree, search, glob, metadata, and partial-read responses must enforce tenant, folder ACL, path policy, include/exclude rules, binary handling, byte/range limits, result limits, and secret-safe response rules before ranking, summarization, snippet generation, or response shaping. Denied context queries must produce metadata-only audit evidence with actor, tenant, folder, query type, policy reason, correlation ID, timestamp, and safe error category.

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
- `BindRepository`
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
- `GetAuditTrail`

Every mutating Contract Spine operation must support an idempotency key, correlation identity, and conflict-detection semantics. The rule applies to current and future mutations, including folder creation, provider/repository binding, branch/ref policy, ACL updates, archive, workspace preparation, lock acquire/release, file add/change/remove, and commit.

Projection DTOs must be defined as read models, not direct event mirrors.

A task may apply many add/change/remove mutations while holding one valid lock, then issue one commit for the task's known staged change set. File mutations do not auto-commit. A multi-file request is accepted or rejected as one command; if external execution later produces a partial or unconfirmed outcome, the workspace first enters `unknown_provider_outcome`, then resolves to a known result or `reconciliation_required`. Commit is denied until every requested mutation has a known, policy-valid outcome. MVP rename is an add-plus-remove pair within this same task and commit boundary.

Each task may produce at most one successful durable commit. While its commit-idempotency record remains unexpired within the C3 tier, an equivalent replay returns the same logical result; after expiry the old key returns `idempotency_key_expired` without execution. Any later non-replay commit or mutation for the task-terminal task is denied.

#### Task and Lock Completion Model

| Outcome | Workspace lifecycle | Lock state | Permitted next action |
| --- | --- | --- | --- |
| Explicit release before any staged change | `ready` | `unlocked` | Task closes without a commit; later work requires a new task. |
| One or more known mutations applied | `changes_staged` or `dirty` | `locked` | The same freshly authorized task may continue mutations or issue its single commit. Release/takeover is denied while changes remain. |
| Provider-confirmed durable commit | `committed` | `unlocked` | Task is task-terminal; while the commit-idempotency record is unexpired, equivalent replay returns the same commit result. After expiry, the old key returns `idempotency_key_expired` without execution. |
| Known retryable commit failure with no remote side effect | `dirty` | `locked` | The same task may refresh state/authorization and retry with a new key; while its record is unexpired, replaying the old key returns the old logical result. After expiry, the old key returns `idempotency_key_expired` without execution. |
| Known non-retryable provider/authorization failure | `failed` or `inaccessible` | `revoked` | Mutations, commit, release, and takeover are denied; metadata-only status/audit remains available. |
| Unconfirmed external side effect | `unknown_provider_outcome` | `revoked` | Bounded automatic evidence checks run without repeating the mutation. Confirmed success/failure follows the corresponding known row; exhausted or conflicting evidence enters `reconciliation_required`. |
| Automatic evidence exhausted or conflicting | `reconciliation_required` | `revoked` | Only read-only evidence collection and human escalation are allowed. Mutations, blind retry, release, and takeover remain denied until authoritative evidence selects a known transition. |

Automatic evidence checking begins immediately after the unconfirmed result, performs no more than five read-only evidence checks, and ends within 15 minutes. If it cannot select a known transition within that budget, the state becomes `reconciliation_required`. Provider compatibility evidence may define a shorter bound but may not extend it without a new product decision.

MVP reconciliation determines outcome for every external side-effect family; it does not silently repeat a mutation, discard changes, rewrite history, or take over a task:

- repository creation/binding checks the provider's canonical repository identity, requested binding intent, and provider-side evidence tying a newly created repository to the originating operation. A matching repository completes a create result only when that ownership evidence matches; otherwise it may be accepted only through the separately authorized pre-created-repository binding flow. Confirmed absence/no side effect permits a fresh authorized attempt with a new key, and conflicting identity/evidence requires human escalation;
- file mutation checks each requested operation identity and intended content-hash/policy evidence; confirmed applied/not-applied outcomes become a known dirty change set, the same task may acquire a new lock instance under the same canonical serializing identity only after fresh authorization to complete known missing operations, and commit remains denied while any operation is unknown;
- commit checks the authoritative remote/ref for the intended commit evidence; confirmation transitions to committed/unlocked, confirmation of no remote update transitions to dirty and permits the same task to acquire a new lock instance under the same canonical serializing identity before retry, and ambiguous or conflicting remote history remains reconciliation-required.

No revoked lock instance is ever reactivated. Recovery creates a newly authorized lock instance for the originating task under the same canonical serializing identity only after the provider outcome is known; if authorization is no longer valid, the workspace remains inaccessible. No workspace may remain opaque: unknown-provider-outcome and reconciliation-required status expose the operation family, last evidence check, next scheduled check or human escalation condition, correlation identity, and safe reason without provider payloads or protected content.

For every mutation:

- equivalent intent means the same tenant, Contract Spine operation, canonical target identity, normalized payload and semantic options, policy version, and delegated task scope. Contract-defined defaults, path/ref normalization, map ordering, and set-like collection ordering are canonicalized before comparison; correlation IDs, authentication tokens, and transport retry metadata are excluded;
- while the idempotency record is unexpired within its declared retention tier, the same key plus equivalent tenant-scoped intent returns the same logical result without duplicate events, provider writes, file changes, repositories, commits, audits, or idempotency records;
- while the idempotency record is unexpired within its declared retention tier, the same key plus different intent returns the canonical idempotency-conflict result without revealing prior protected metadata;
- mutation records use the approved 24-hour tier except commit records, which use the C3 audit-retention tier; replay always revalidates current authorization before returning protected prior result data, so a revoked caller receives safe denial while the idempotency record remains intact;
- reuse of an expired key is rejected with the stable expired-key outcome and requires state refresh plus a new key; expiry never causes an old external mutation to be treated automatically as a new request;
- unconfirmed external outcomes first enter `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks enter `reconciliation_required`, and neither state permits blind retry;
- non-mutating operations reject idempotency keys and declare read consistency, safe denial, audit metadata, correlation behavior, and projection expectations.

### Authentication and Authorization Model

Authentication and tenant authorization rely on existing Hexalith.Tenants and Hexalith.EventStore patterns. Hexalith.Folders adds folder-specific ACL checks.

Folder ACLs must define verbs for create, configure provider binding, prepare workspace, lock workspace, read metadata, read file content where allowed, mutate files, commit, query status, query audit, and view operations-console projections.

Tenant administrators own tenant-level Folders configuration: provider bindings, credential references, repository naming/default-ref policy, capability policy, folder ACL grants, and archive decisions. Platform operators may validate readiness and diagnose within authorized scope but may not silently change tenant policy.

Effective permission is the intersection of current active tenant authority and the union of applicable allow-only folder grants assigned directly to the principal or through active group and role membership. MVP folder ACLs do not support explicit deny entries; absence of an applicable allow denies. A delegated service agent receives only the intersection of the delegating principal's current effective permissions, the agent's explicit folder grant, and the delegated verb/task scope, so delegation cannot elevate authority. Disabled, deleted, unknown, missing, stale, or revoked tenant authority overrides every folder grant and fails closed. Archive state and resource policy may further restrict an otherwise allowed permission. Conflicting, stale, or incomplete identity/membership/delegation evidence denies access.

Cross-tenant access must be denied before file, workspace, credential, repository, lock, commit, provider, or audit access. Denials must use safe error shapes that avoid unauthorized resource enumeration.

Authorization must be fresh when each mutation is about to perform a side effect; authorization only at request receipt is insufficient for asynchronous work. If tenant membership, folder ACL, delegated authority, provider binding, or credential permission is revoked, the held lock becomes revoked/inaccessible, the denial is audited, and subsequent work fails closed before touching files, repositories, providers, commits, or protected audit resources. Numeric lease-renewal and authorization-revalidation intervals remain governed by C7 and must be approved before release.

Authoritative tenant context comes from the authenticated request and EventStore envelope. Any payload tenant identifier is only a comparison input and must match that authority or be rejected.

Tenant administrators can inspect provider binding ownership and non-secret credential-reference status for their tenant without seeing credential material or unauthorized repository details.

MVP operator access is tenant- and folder-scoped. An operator must hold the applicable operator permission and normal tenant/folder authorization; there is no global cross-tenant search or unscoped break-glass browsing. Any future cross-tenant need-to-know workflow requires a separate Security and PM decision covering consent, duration, visible fields, and reviewable privileged-access audit.

### Data Schemas

The API uses JSON command/query DTOs and structured response models. DTOs are public contracts and require schema/versioning tests.

The Contract Spine must define the accepted bounded file-content transport and stable oversize/unsupported outcomes; SDK and adapter behavior must derive from it. Metadata-only events may include path, content hash, size, media type, content reference ID, operation ID, provider reference, actor, timestamps, and commit ID, but not file contents.

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

Required error categories include authentication failure, tenant authorization denied, folder ACL denied, cross-tenant access denied, provider readiness failed, credential reference missing or invalid, provider permission insufficient, provider unavailable, provider rate limited, repository conflict, duplicate binding, workspace not ready, workspace locked, stale or interrupted lock, dirty workspace, path validation failed, file operation failed, commit failed, unknown provider outcome, reconciliation required, idempotency conflict, expired idempotency key, unsupported provider capability, read-model unavailable, and audit access denied.

Error responses indicate whether the client may retry, must refresh state, must request authorization, must change input, or must escalate provider/configuration failure.

Error details are a closed, bounded, metadata-only shape defined per error by the Contract Spine, not an arbitrary payload bag. They must never include secrets, tokens, file content, diffs, provider payloads, local absolute paths, or unauthorized existence. Authorization is evaluated before state-specific detail. The safe-denial response for absent, cross-tenant, missing-binding, missing-policy, and equivalent protected-resource cases must be indistinguishable at the caller-visible boundary. Redacted, unknown, missing, hidden, stale, and unavailable are distinct states; redacted data cannot also carry cleartext.

Non-enumerating equivalence applies until the caller is authorized for the specific tenant/provider-binding scope. After that authorization succeeds, readiness diagnostics may identify the caller's configured provider product, instance/profile, capability gap, credential-reference status, and safe remediation category. They still may not reveal an unconfigured provider identity, credential value, repository existence outside the authorized binding, or cross-tenant state.

Provider and workspace failures must distinguish known failure from unknown outcome. If repository creation, file mutation, or commit status cannot be confirmed after a timeout or provider interruption, the system first exposes `unknown_provider_outcome` and performs only the bounded read-only evidence checks defined above. Exhausted or conflicting evidence then exposes `reconciliation_required`; neither state permits retry that could duplicate repositories, file changes, or commits.

### Rate Limits, Throttling, and Idempotency

The MVP does not require fixed public rate-limit numbers. It applies throttling and correlation to provider readiness reads and provider API calls, and applies the all-mutations idempotency contract to repository creation/binding, configuration changes, ACL/archive changes, workspace/lock commands, file mutations, and commits. Readiness and other read-only operations reject idempotency keys.

Throttling policy must identify enforcement dimensions such as tenant, folder, workspace, provider, and command type.

Provider calls use bounded retry/backoff policy, provider-specific throttling, and clear failure projection when rate limits, timeouts, permission failures, or unknown outcomes occur.

### Workspace State and Concurrency

The canonical workspace lifecycle vocabulary is `requested`, `preparing`, `ready`, `locked`, `changes_staged`, `dirty`, `committed`, `failed`, `inaccessible`, `unknown_provider_outcome`, and `reconciliation_required`. These lowercase wire terms are stable product states.

Lock state is a separate dimension: `unlocked`, `locked`, `expired`, `stale`, or `revoked`. Generic operation-execution labels such as pending, in-progress, succeeded, failed, and cancelled must be named as operation state and must not replace workspace lifecycle or lock state.

Operator disposition is derived rather than treated as another lifecycle: requested/preparing/committed are auto-recovering; unknown-provider-outcome is auto-reconciling during its bounded evidence-check budget; locked/changes-staged are degraded-but-serving; dirty/reconciliation-required are awaiting-human; failed/inaccessible are terminal-until-intervention (an operator label, not cleanup eligibility); ready is available unless freshness exceeds C2.

Lock semantics must define owner, lease duration, renewal, expiry, release, stale lock behavior, reentrant behavior, and conflict behavior.

The MVP serializing identity is managed tenant plus canonical provider/repository identity plus normalized target ref. All folder bindings, aliases, workspaces, and tasks resolving to that identity collide on one active mutation writer; genuinely different repository/ref identities may proceed independently. Governed mutations and commits require a valid, unrevoked lock and fresh authorization. Context reads of committed/ready content do not require the mutation lock but still require tenant, folder ACL, and path policy. While lifecycle is `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required`, live body search, range reads, and file content are visible only to the freshly authorized originating/lock-owning task; other authorized actors receive metadata-only status, and the incident view never exposes content. Commit must release the lock or transition the current task to a defined task-terminal or recovery state.

Concurrent operations must either serialize deterministically or fail with stable conflict errors. Commit without a valid lock, mutation without fresh authorization, duplicate lock acquisition, and retry after an unknown provider outcome are denied without side effects and expose the applicable lock, inaccessible, unknown-provider-outcome, or reconciliation-required state.

Query/status responses must distinguish accepted command state from projected state when projection lag exists.

### API Versioning

The initial API is versioned as `v1`. Additive evolution is preferred. Breaking changes to command/query DTOs, event payloads, error categories, workspace states, provider capabilities, or SDK models require explicit versioning.

Event payload evolution uses schema versions and backward-compatible consumers.

### SDK Requirements

The generated typed SDK is required for the current release, with the first supported language aligned to the primary Hexalith implementation ecosystem. CLI and MCP consume this SDK and may not implement independent lifecycle behavior.

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
- Every protected operation family is covered by the canonical authorization matrix for tenant administrator, member, delegated agent, tenant-scoped operator, auditor, wrong-tenant, revoked, stale, and hidden-resource cases. Security/Authorization owns this matrix, and release is blocked until its linked inventory is approved.
- 100% provider contract suite pass for each supported provider before marking that provider ready.
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

Approved numeric targets and their canonical evidence are summarized below. Changes require documented approval in the governance record; the linked artifacts remain authoritative for measurement detail.

| ID | Target | PRD Source | Status |
| --- | --- | --- | --- |
| C1 | Concurrent capacity targets: maximum concurrent tenants, folders per tenant, active workspaces per tenant, and concurrent agent tasks per tenant | NFR Scalability and Capacity (constraint that capacity targets must avoid assuming a single tenant, single repository, or single active workspace) | Approved 2026-05-30 (Story 7.10): 4 concurrent tenants, 2 folders/tenant, 2 active workspaces/tenant, 2 concurrent agent tasks/tenant — see `docs/exit-criteria/c1-capacity.md` |
| C2 | Status-freshness target: maximum acceptable lag between an emitted lifecycle event and its appearance in status/audit views under normal operation | NFR Observability, Auditability, and Replay | Approved 2026-05-30 (Story 7.10): 500 ms commit-to-status-read lag (hermetic) — see `docs/exit-criteria/c2-freshness.md`; production exporters/alerts wired by Story 7.12 |
| C3 | Retention by data class | NFR Data Retention and Cleanup | Approved by PM 2026-06-22 and Legal 2026-06-24: audit and commit-idempotency metadata 7 years; workspace status, provider correlation IDs, cleanup records, diagnostics/rejections, and copied auth-claim metadata 400 days; read models 400 days or until rebuilt, whichever is sooner; temporary working files deleted 7 days after task-terminal closure and no active task; folder/tombstone metadata tenant lifetime plus 400 days after approved deletion workflow. See `docs/exit-criteria/c3-retention.md`. |
| C4 | Bounded MVP context-query inputs and responses | NFR Performance and Query Bounds | Approved 2026-06-22: 100 requested paths, 2,000 tree entries, 500 search/glob results, 262,144-byte bounded range, 1,048,576-byte aggregate response, and 2-second server execution limit. See `docs/exit-criteria/c4-input-limits.md`. |
| C5 | Concrete scalability quantifiers replacing the word "multiple" in the NFR scalability constraint, derived from C1 | NFR Scalability and Capacity | Approved 2026-05-30 (Story 7.10): 4 tenant units, 2 folder units/tenant, 2 workspace units/tenant, 2 task units/tenant, ≥1 lifecycle op/sec — see `docs/exit-criteria/c5-scalability-quantifiers.md` |
| C6 | Workspace lifecycle, lock-state mapping, and operator disposition | Workspace State and Concurrency | Approved: canonical lifecycle and distinct lock vocabulary are defined in this PRD and `docs/exit-criteria/c6-transition-matrix-mapping.md`. |
| C7 | Lock renewal, authorization revalidation, and revocation-effect SLO | Authentication/Authorization; Reliability | Reference-pending; tracked as OQ1. Product behavior already revalidates every mutation and fails closed. |
| C9 | Sensitive metadata classification | Security; Observability | Approved: paths, repository/branch names, and commit messages default tenant-sensitive; confidential tenant override stores only a stable tenant-scoped correlation token at write so incidents retain linkage without cleartext; redacted/hidden/unknown/missing/stale/unavailable are distinct. Evidence is governed by C9 safety fixtures. |
| C13 | Complete operation/parity denominator | Integration and Contract Compatibility | Generated from the current Contract Spine; every operation requires exactly one parity row. The current inventory contains 49 rows, but the generated current inventory—not this count—is the binding denominator. |

Each target must be validated through its named release-calibration evidence. C7 lease-renewal and authorization-revalidation intervals remain reference-pending; the product outcome is already fail-closed on every mutation, but release requires approved numeric intervals and a revocation-effect SLO.

## Project Scoping & Phased Development

### MVP Strategy & Philosophy

**MVP Approach:** Platform MVP: repository-backed task lifecycle.

The MVP must prove one canonical job: an AI agent can safely prepare, modify, lock, commit, inspect, and audit tenant-scoped files in a real repository-backed workspace without leaking data, losing control, or hiding failure state.

The MVP is successful when the same repository-backed task lifecycle works through REST, CLI, MCP, and SDK surfaces with equivalent authorization, error semantics, audit outcomes, and observable state transitions.

**Canonical MVP Workflow:**

Tenant setup, provider readiness check, folder/repository binding, workspace preparation, task-scoped lock, file add/change/remove, commit, context query, and status/audit inspection.

Provider readiness is a gate, not a passive feature. If GitHub or Forgejo readiness fails, the workflow must stop with actionable status rather than continuing into partial mystery state.

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

- At least one end-to-end parity scenario runs through REST, CLI, MCP, and SDK; this scenario supplements rather than replaces the full per-cell C13 conformance suite.
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
- No broad provider framework beyond proving GitHub and Forgejo well.
- No incoming provider webhook ingestion; any future webhook surface requires an approved tenant-routing design.
- No cross-workspace body-content indexing or indexed body recall; FR58 is metadata-token recall until separate Security and PM approval. Bounded direct body search inside the currently authorized live workspace remains part of FR34–FR35.
- No secret material storage in Hexalith.Folders; only credential references may appear where authorized.
- No policy engine beyond required tenant, provider, readiness, ACL, and workspace controls.

### Post-MVP Features

**Phase 2:** Repair commands, brownfield adoption, deeper drift detection, richer provider capability recipes, large-file policy expansion, and broader operations workflows.

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
| Repository binding | Authorized association between one logical folder and a canonical provider/repository identity plus target ref policy. |
| Workspace | Disposable task working area prepared from a repository binding; never the durable source of truth. |
| Task | Caller-visible unit of agent work that correlates preparation, lock, mutations, commit, status, and audit. |
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

- FR4: Tenant administrators own tenant-level Folders configuration for provider bindings, credential references, repository naming/default-ref and capability policy, folder ACLs, and archive decisions; scoped operators may validate but not silently modify it.
- FR5: Tenant administrators can grant and revoke folder access for users, groups, roles, and delegated service agents; the resulting verb scope is visible in effective permissions and auditable without exposing hidden principals.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

### Folder Lifecycle

- FR11: Authorized actors can create logical folders within a tenant.
- FR12: Authorized actors can inspect folder lifecycle and binding status.
- FR13: Authorized actors can archive a folder only when it has no active task or lock and no `changes_staged`, `dirty`, `unknown_provider_outcome`, or `reconciliation_required` workspace. Archive denies later repository, workspace, file, and commit mutations with a stable, non-enumerating lifecycle result; tenant administrators may still revoke access and administer legal-hold or retention metadata through separately authorized governance operations. The provider repository remains provider-owned and is neither deleted nor mutated by archive.
- FR14: Archived-folder views retain each metadata-only lifecycle, audit, lock, timeline, and last-commit field for that field's C3 data-class period. When one class expires before another, the view omits the expired field and exposes its safe retention-expired marker; it never extends a shorter class to match seven-year audit retention. File content, credentials, and unauthorized existence remain hidden.

### Provider Readiness and Repository Binding

- FR15: Tenant administrators can configure supported Git provider bindings, credential references, repository naming/default-ref policy, and required capability policy; platform engineers can validate the resulting readiness.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass.
- FR19: Authorized actors can bind a pre-created provider repository when readiness, repository access, duplicate/alias detection, and branch/ref policy pass; unsupported eligibility is rejected without revealing unauthorized repository existence.
- FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
- FR23: Platform engineers can inspect provider product, instance identity, observed version/API profile, accepted credential profile, and supported/unsupported/unknown capability status for the canonical lifecycle; unknown or incompatible evidence cannot report ready.

### Workspace and Lock Lifecycle

- FR24: Authorized actors can prepare a workspace only when provider readiness, repository binding, branch/ref policy, fresh authorization, and task context are valid; failure leaves an inspectable lifecycle state and no unauthorized side effect.
- FR25: Authorized actors can acquire a task-scoped mutation lock for the canonical tenant/provider/repository/ref identity; aliases resolving to the same identity must collide.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: Competing mutations against the same serializing identity are deterministically denied without file, provider, repository, or commit side effects; the denial emits one metadata-only audit record, and authorized callers receive safe conflict and retry-eligibility metadata.
- FR28: Lock state is exposed only as `unlocked`, `locked`, `expired`, `stale`, or `revoked`, separately from workspace lifecycle and operator disposition.
- FR29: Authorized owners can release a workspace lock when policy allows; while the idempotency record is unexpired, equivalent retries preserve one logical release result, while expired keys return `idempotency_key_expired` without execution and revoked or non-owner attempts fail safely.
- FR30: Platform-owned automatic cleanup begins only after task-terminal closure and no active task, retries safely without caller action, and deletes temporary working files at the C3 seven-day boundary. Dirty, unknown-provider-outcome, and reconciliation-required workspaces are not cleanup-eligible. Failed/inaccessible closure records final metadata-only evidence and operator disposition before starting the seven-day observation window. Authorized callers can inspect pending, retrying, completed, or failed cleanup with reason, retryability, timestamp, and correlation ID; cleanup failure escalates to operators but never deletes required audit evidence. User-triggered cleanup/repair is not MVP.
- FR31: Authorized actors can inspect workspace lifecycle, lock state, operator disposition, projection freshness/checkpoint, retryability, and whether task, audit, provider, or index status is current, delayed, failed, stale, or unavailable.

### File Operations and Context Queries

- FR32: Authorized actors can apply one or many add/change/remove mutations within a prepared, freshly authorized, locked task workspace without auto-commit; a first-class move/rename is not MVP and is represented by add plus remove under the same task and commit.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
- FR34: Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution.
- FR35: Live-workspace context queries enforce authorization and path policy before filtering or shaping; body-search results contain only authorized C9-wrapped relative identity, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`, range/file content is never silently truncated, and unsupported excess returns the stable input/response-limit result without logging raw queries, path lists, content, or hidden existence.
- FR36: The operations console must remain read-only and excluded from file editing or file-content browsing capabilities.

### Commit, Evidence, and Idempotency

- FR37: Authorized actors can commit a valid locked workspace only when fresh authorization holds; success requires provider-confirmed durable update of the bound remote/ref and returns the commit reference. An unconfirmed result first enters `unknown_provider_outcome`; only exhausted/conflicting automatic evidence enters `reconciliation_required`.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits only within the Contract Spine's closed length/character constraints and C9 classification. Suspected secrets or content-like payloads in metadata are rejected before provider, event, audit, or diagnostic emission.
- FR39: The system exposes metadata-only task and commit evidence including provider, repository binding, tenant-sensitive branch/ref and changed-path metadata, durable result status, commit reference, timestamps, task ID, operation ID, and correlation ID under C9 classification.
- FR40: The system reports failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence; `unknown_provider_outcome` instructs callers to wait/query during bounded automatic checks, while `reconciliation_required` blocks retry and instructs human escalation.
- FR41: Every mutating Contract Spine operation supports idempotent retry while its idempotency record is unexpired within the declared retention tier: equivalent tenant-scoped intent returns the same logical result and cannot duplicate events, provider writes, files, repositories, commits, audits, or idempotency records. After expiry, the old key returns `idempotency_key_expired`, requires state refresh, and never executes automatically as new intent.
- FR42: While an idempotency record is unexpired, reuse of its key with different intent returns the canonical idempotency-conflict result without revealing protected prior intent; an expired key returns `idempotency_key_expired` regardless of submitted intent, and non-mutating operations reject idempotency keys.

### Error, Status, and Diagnostics Contract

- FR43: Every supported surface exposes the Contract Spine error taxonomy with category, code, safe message, correlation ID, optional task ID, retryability, client action, and closed metadata-only details visibility.
- FR44: The error taxonomy must distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, idempotency conflict, expired idempotency key, unknown provider outcome, reconciliation required, and transient infrastructure failure. The stable expired-key result uses code `idempotency_key_expired`, is not retryable with the old key, and instructs the client to refresh state before submitting equivalent intent with a new key.
- FR45: The system exposes the complete canonical workspace lifecycle and the separate lock-state vocabulary defined in the Glossary, without substituting generic operation status.
- FR46: After preparation, lock, file, commit, provider, authorization, index, or read-model failure, authorized callers receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available metadata-only evidence.

### Cross-Surface Contract

- FR47: API consumers can use the versioned REST transport for every current Contract Spine operation, with emitted schemas validated against the canonical OpenAPI 3.1 spine.
- FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

### Audit and Operations Visibility

- FR52: Tenant-scoped operators can inspect read-only readiness, binding, workspace lifecycle, lock state, disposition, durable commit, failure, provider, credential-reference, and sync status without global cross-tenant browsing.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Authorized audit reviewers can reconstruct incidents from immutable C9-classified metadata covering actor, tenant, task, operation/correlation identity, provider, binding, folder, result, timestamp, lifecycle/lock state, and durable commit reference without exposing file bodies or hidden resources.
- FR55: File contents, diffs, generated context, provider payloads/tokens, credential material, secrets, and unauthorized existence are excluded from events, logs, traces, metrics, projections, audit, diagnostics, errors, and console responses; redaction is visibly distinct from missing or unknown.
- FR56: Normal operation timelines come from projections; during projection degradation, the authorized incident view may expose bounded redacted event evidence with a persistent warning, last checkpoint, correlation ID, and time window, but no mutation or repair path.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

### Authorized Search Facade

- FR58: Developers and AI agents can search authorized metadata tokens derived from indexed mutation metadata and query indexing status through REST, SDK, CLI, and MCP. Before egress, every hit is security-trimmed to the current tenant/folder/workspace authority and hydrated against current Folders state; stale, archived, revoked, unauthorized, or hidden hits are dropped. Results expose only C9-classified metadata, opaque authorized identity, and indexing/status evidence—never raw paths, file bodies, snippets, source URIs, or hidden-resource existence. Index or facade unavailability is explicit and fail-safe.

Cross-workspace body-content indexing, indexed body snippets, and indexed body recall are not part of FR58 or the current release. They require separate Security and PM approval and a future product requirement. Bounded direct text search and snippets inside the currently authorized live workspace remain required by FR34–FR35; the separate RAG ingestion capability is outside this PRD.

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
- Unconfirmed external effects immediately enter `unknown_provider_outcome` and permit only bounded automatic read-only checks; exhausted or conflicting evidence enters `reconciliation_required`, blocks retry/mutation/takeover, and requires human escalation. These states never collapse into a generic failure.
- Idempotency keys are required for every mutating Contract Spine operation; non-mutating operations reject them.
- While the idempotency record is unexpired within its declared retention tier, a repeated call with the same key and equivalent payload must return the same logical result, and the same key with a conflicting payload must return an idempotency conflict. After expiry, either use returns `idempotency_key_expired`, requires state refresh, and never executes automatically as a new request.
- Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.
- Lock acquisition is deterministic and limited to one active writer per managed tenant plus canonical provider/repository identity plus normalized target ref; aliases resolving to that identity collide.
- Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.
- Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
- A successful committed state requires provider-confirmed durable update of the bound remote/ref. A timeout or unconfirmed remote result first enters `unknown_provider_outcome`; only exhausted or conflicting bounded evidence checks enter `reconciliation_required`, and neither state permits blind retry.
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

- Every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
- Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
- Paths, commit messages, repository names, and branch names are tenant-sensitive by default under C9; authorized tenant/scoped-operator views may display them, cross-tenant/external diagnostics redact them, and a tenant confidential override stores only the stable tenant-scoped correlation token at audit/projection write time. Confidential incident reconstruction links operations through that token and operation/correlation identity; it does not promise recovery of the original cleartext. Provider payloads, file bodies, secrets, and generated context remain forbidden.
- Operations-console views are projection-first, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata. During projection degradation, the bounded incident view may expose redacted event evidence only with incident-admin plus normal tenant/folder access, a persistent warning, last checkpoint, correlation ID, and time window.
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

These items do not reopen the approved scope or fail-closed invariants above. OQ1–OQ4 close bounded parameters and contract inventories within those invariants; OQ5–OQ10 close implementation and release evidence. Every item must close before release acceptance.

| ID | Decision/evidence still open | Delivery owner | Blocking consequence and revisit condition | Canonical evidence and accountable approvers |
| --- | --- | --- | --- | --- |
| OQ1 | Approve the C7 lock-renewal interval, authorization-revalidation interval, and revocation-effect SLO. | Architecture + Security | Blocks lock/revocation timing acceptance; close when C7 governance status changes from reference-pending to approved. | `docs/exit-criteria/c7-lock-authorization-timing.md` plus governance record; Architecture and Security approvers. |
| OQ2 | Publish the canonical file-policy vocabulary and exact allow/reject behavior for symlinks, Unicode/case collisions, encoding, binary/large files, include/exclude precedence, and safe-denial routing. | Architecture + Security + PM | Blocks final FR32–FR35 acceptance; close when the file-policy contract and cross-surface tests are approved. | `docs/contract/file-context-contract-groups.md`; PM, Architecture, and Security approvers. |
| OQ3 | Publish the canonical actor/access-state × protected-operation authorization matrix used as the release denominator. | Security/Authorization | Blocks the authorization completeness gate; close when the inventory covers every declared actor and protected operation family. | `docs/contract/authorization-matrix.md`; Security and PM approvers. |
| OQ4 | Publish the supported-provider compatibility catalog: product/instance identity, observed versions/API profiles, accepted credential profiles, capability semantics, readiness outcomes, and reconciliation check policy for GitHub and Forgejo. | Provider + Architecture | Blocks provider-ready status and provider contract acceptance; close when both providers pass against the catalog. | `docs/contract/provider-compatibility-catalog.md`; Provider, Architecture, and PM approvers. |
| OQ5 | Replace the fail-safe but functionally empty FR58 search/status facade with evidence for authorized non-empty metadata-token results, indexing status, stale/unauthorized hit removal, and unavailable behavior. | Search/Delivery | Blocks FR58 implementation readiness; close when coverage and tests round-trip FR58 and both C13 operations. | `docs/exit-criteria/fr58-search-evidence.md`; PM, Security, and Test approvers. |
| OQ6 | Replace seed-only console/read-model diagnostics with projection-backed readiness, lifecycle, lock, failure, timeline, and transition evidence. | Console + Projections/Delivery | Blocks console implementation readiness; close when positive, degraded, and replay scenarios populate approved projections. | `docs/exit-criteria/console-projection-evidence.md`; PM, Operations/UX, and Test approvers. |
| OQ7 | Align architecture and contract evidence to the managed-tenant plus canonical provider/repository plus normalized-ref lock identity, including alias collisions. | Architecture + Locking/Delivery | Blocks FR25–FR29 implementation readiness; close when lock contracts, transitions, and tests use that identity. | `docs/contract/workspace-lock-contract-groups.md` and C6 evidence; Architecture and Security approvers. |
| OQ8 | Align architecture, Contract Spine, SDK, and C13 evidence to the all-mutations idempotency rule and read-key rejection. | Architecture + Contract/Delivery | Blocks idempotency completeness; close when every mutating operation and every read cell passes the rule. | `docs/contract/idempotency-and-parity-rules.md` plus versioned C13 snapshot; Architecture, Security, and Test approvers. |
| OQ9 | Prove incident access requires both incident-admin permission and current tenant/folder authorization, with C9 redaction and denial audit. | Security + Console/Delivery | Blocks incident-view acceptance; close when positive, revoked, wrong-tenant, hidden-resource, and degraded tests pass. | `docs/exit-criteria/incident-access-evidence.md`; Security and PM approvers. |
| OQ10 | Publish the release-calibration plan with frozen populations, exclusions, environments, scenarios, methods, evidence owners, and approval rules for SM1–SM8 and CM1–CM4. | PM + Test/Quality | Blocks use of metric results for release acceptance; close before calibration evidence is collected. | `docs/exit-criteria/release-calibration-plan.md`; PM and Test/Quality approvers. |

An open item closes only when its canonical evidence exists, every accountable approver records identity and approval date, and the governance record stores approved status plus the evidence version/digest. Delivery completion or a passing test alone cannot close an item, and any later evidence change reopens approval.
