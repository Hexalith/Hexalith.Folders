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
  projectContext: greenfield
workflowType: 'prd'
releaseMode: phased
status: complete
completedAt: '2026-05-07'
lastEdited: '2026-05-07'
editHistory:
  - date: '2026-05-07'
    changes: 'Validation polish: 11 wording edits removing subjective adjective and CQRS pattern leakage. Edit workflow: added Journey 9 (Tenant Administrator folder access and lifecycle); added Journey Requirements Summary bullets; added FR-section objective cross-reference; added Deferred Quantitative Targets — Architecture Exit Criteria subsection (C1–C5).'
---

# Product Requirements Document - Hexalith.Folders

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

**Project Context:** Greenfield product informed by product brief, brainstorming, and technical research artifacts.

## MVP Contract Summary

The MVP proves one job: an AI agent can safely prepare, modify, and commit files in a tenant-scoped repository workspace through one canonical contract.

The canonical lifecycle is provider readiness, repository-backed folder creation or binding, workspace preparation, task-scoped lock acquisition, governed file changes, commit, context query, status inspection, and metadata-only audit. REST is the canonical contract. CLI, MCP, SDK, and console surfaces must not introduce independent lifecycle semantics; they map to the canonical operations, states, authorization checks, idempotency rules, and error taxonomy.

The MVP is repository-backed first. Local-only folders, local-first promotion to Git-backed storage, brownfield folder adoption, and repair-oriented cache rebuild workflows are post-MVP unless required as internal implementation mechanics for Git-backed workspace preparation.

The product has one non-negotiable content boundary: file contents, diffs, generated context payloads, provider tokens, credential values, secret material, and unauthorized resource existence must not appear in events, logs, traces, metrics, audit records, console views, provider diagnostics, or error responses.

## Success Criteria

### User Success

Chatbot developers succeed when they can complete the core repository-backed workflow without writing custom filesystem or Git orchestration: create a Git-backed folder/repository, add files, change files, remove files, commit the resulting change set, and query the final workspace state through the module.

Operators succeed when they can inspect whether a workspace is usable before and after agent execution. The read-only console must expose provider readiness, workspace readiness, lock state, dirty/uncommitted state, last commit, failed operation state, and sync/provider status without requiring direct filesystem inspection.

AI tool integrations succeed when the same core folder capabilities are available through API contracts, CLI commands, and an MCP server using consistent command/query semantics.

### Business Success

Three-month success means a developer can complete the canonical workflow — configure a provider, create a repository-backed folder, perform file changes, and commit those changes through CLI or MCP — without requiring knowledge of the internal storage, Git provider, or event-sourcing implementation.

Twelve-month success means adoption by Hexalith agent and chatbot workflows. The product is strategically successful when Hexalith.Folders becomes the default workspace persistence layer for agentic file work instead of each chatbot or automation component implementing its own temporary folders, Git CLI calls, token handling, and cleanup logic.

### Technical Success

The MVP must prove the complete repository workflow: create repository, add files, change files, remove files, commit changes, and expose the resulting workspace state.

The two highest-priority technical success measures are task completion rate and zero cross-tenant access leaks. Cross-tenant authorization failures must be caught before file, workspace, credential, or repository access occurs.

Provider readiness checks must prevent avoidable runtime failures by validating provider configuration, credential reference availability, required GitHub/Forgejo capabilities, repository provisioning readiness, and default branch policy before repository-backed folder creation.

The CLI, MCP server, and service/API surfaces must expose the same core capabilities and preserve consistent authorization, audit, error handling, and command/query semantics.

### Measurable Outcomes

- End-to-end MVP demo flow succeeds: create repository, add/change/remove files, commit, query workspace state.
- Cross-tenant access leaks: zero tolerated.
- Task completion rate is tracked for prepare, lock, file operations, commit, and release.
- Provider readiness check exists before Git-backed folder creation.
- Read-only operations console shows readiness, lock, dirty state, last commit, failed operation, and provider status.
- CLI supports the core repository and file workflow.
- MCP server supports the same core workflow for AI tool integration.
- File operations and Git commits are traceable to tenant, folder, task/correlation ID, and commit metadata.

## Product Scope

### MVP - Minimum Viable Product

The MVP includes the repository-backed task workflow: provider readiness check, repository-backed folder creation, add/change/remove file operations, commit changes, and query workspace status.

The MVP is repository-backed first. Local-only folders, local-first promotion, brownfield folder adoption, and repair-oriented cache rebuild workflows are excluded from MVP unless they are internal mechanics required to prepare a Git-backed workspace.

This is an intentional scope change from the Product Brief. The Product Brief included limited local storage and local-to-Git upgrade in MVP; the PRD narrows MVP to the repository-backed workflow so the first release can prove tenant isolation, provider readiness, task locking, file operations, commit, status, and audit before expanding storage modes.

The MVP includes a read-only operations console focused on trust signals: provider readiness, workspace readiness, lock state, dirty state, commit state, failed operation state, and provider/sync status.

The MVP includes a CLI and MCP server exposing the core folder, file, repository, commit, query, and status operations.

The MVP must enforce tenant-scoped authorization and must prevent cross-tenant file, credential, workspace, and repository access.

### Growth Features (Post-MVP)

Post-MVP growth should expand reliability and operational depth after the core workflow is proven. Candidate growth features include repair commands, deeper drift detection, richer provider contract tests, brownfield folder or repository adoption, large-file policy enforcement, advanced provider capability recipes, and broader operations workflows.

Known post-MVP pressure points include local-first folders that promote to Git-backed storage, auto-commit command mode, disposable working-copy repair, evented repair console workflows, drift-first operations views, multiple Git organizations per tenant, and module-managed local storage policy.

### Vision (Future)

The long-term vision is for Hexalith.Folders to become the default durable workspace substrate for Hexalith AI agents. In that future state, agentic file work is consistently tenant-scoped, observable, recoverable, auditable, provider-portable, and accessible through stable API, CLI, and MCP surfaces.

## User Journeys

### Journey 1: Developer Proves Agentic File Work Is Production-Ready

Nadia is building a chatbot that must work on real project files for tenant customers. Her concern is not whether she can script Git; she already can. Her concern is whether she can let an AI agent touch project files without creating tenant leaks, unrecoverable partial changes, hidden dirty state, or provider-specific Git logic inside the chatbot.

She starts with a provider readiness check for the tenant organization. The system confirms that the provider binding exists, the credential reference resolves, repository creation is supported, the default branch policy is valid, and the tenant has permission to create the repository. Nadia then creates a Git-backed folder, prepares the workspace, and runs the core task flow through CLI or MCP: add files, change files, remove files, commit the change set, and query workspace status.

The value moment is not repository creation by itself. It is the clean final state: the workspace is `committed`, no hidden dirty state remains, the commit SHA is visible, the changed paths are traceable to tenant/folder/task metadata, and the chatbot did not need direct filesystem paths, provider credentials, or Git CLI orchestration.

This journey reveals requirements for provider readiness checks, repository-backed folder creation, workspace preparation, governed file operations, commit support, workspace status queries, CLI/MCP access, task/correlation metadata, and clean committed-state reporting.

### Journey 2: Platform Engineer Establishes Tenant Provider Readiness

Ravi is a platform engineer responsible for making a tenant organization ready for Git-backed agent work. Before any agent can create a workspace, he must configure the provider binding, credential reference, repository naming policy, default branch policy, and minimum provider capabilities.

Ravi runs readiness validation and sees a clear state rather than a generic failure. If a credential reference is missing, a token lacks repository creation permission, a provider is unavailable, or a default branch policy is invalid, the system reports a stable machine-readable reason code and human-readable diagnosis. Secrets are never displayed. The readiness result tells Ravi whether the tenant is `ready`, `failed`, or blocked by configuration.

The value moment is confidence before runtime. Ravi can correct provider setup before an agent task fails halfway through repository creation or commit.

This journey reveals requirements for organization-level provider configuration, credential references, repository policy validation, provider capability checks, readiness status projection, actionable readiness reason codes, and secret-safe error handling.

### Journey 3: Agent Task Is Interrupted and Leaves Inspectable State

An AI agent starts a task against an existing Git-backed folder. It prepares the workspace, acquires a task-scoped lock, applies several file changes, and then the task is interrupted before commit.

Without Hexalith.Folders, the team would be left with uncertain state: partial files, no reliable lock owner, no clear task correlation, and no trustworthy recovery signal. With Hexalith.Folders, the workspace enters an inspectable MVP terminal state. It is visibly `locked`, `dirty`, and associated with the interrupted task. The changed file list is available as metadata, the last successful operation is visible, and the absence of a commit is explicit.

The MVP does not silently repair, discard, or commit the changes. It makes the state understandable and prevents another task from overwriting the workspace accidentally. Post-MVP repair commands may support retry commit, discard changes, rebuild cache, or release stale locks.

This journey reveals requirements for task-scoped locks, lock ownership metadata, dirty workspace detection, interrupted task status, operation timeline, changed-path metadata, safe blocked state, and post-MVP repair workflows.

### Journey 4: Concurrent Agent Is Denied by Workspace Lock

Two agent tasks target the same tenant folder. The first task prepares the workspace and acquires the lock. The second task arrives through a different surface, such as MCP or API, and tries to prepare, write, or commit against the same workspace.

The system rejects the second task with a deterministic lock response. The denial includes safe metadata: lock state, lock owner/task reference if authorized, lock age, and retry eligibility policy. It does not allow mixed writes, overlapping commits, or silent lock takeover.

The value moment is trust under pressure. Nadia and the operator can see that lock contention did not corrupt the workspace and did not produce a mixed commit.

This journey reveals requirements for exclusive workspace locking, consistent lock denial across API/CLI/MCP, lock status visibility, retry/idempotency policy, no-lost-update behavior, and structured lock error responses.

### Journey 5: Operator Diagnoses Provider or Credential Failure

Marcus maintains the Hexalith platform. A tenant reports that an agent task cannot complete. Marcus opens the read-only operations console and searches for the tenant and folder.

The console does not present a flat wall of infrastructure facts. It answers three questions first: what is broken, who or what is affected, and what can safely happen next. Marcus sees that the workspace is `failed`, the provider readiness check now fails because the credential reference no longer has required permissions, and the last commit attempt did not succeed. The console shows supporting evidence: tenant, folder, provider binding, credential reference identifier, failed operation type, last successful operation, lock/dirty state, and timestamp.

The console remains read-only in MVP. It does not reveal credential material, expose file contents, mutate workspace state, or hide repair actions behind undocumented controls. Marcus can diagnose and escalate or correct configuration through the proper provider/tenant administration path.

This journey reveals requirements for a read-only operations console, workspace trust surface, primary diagnosis, provider/credential failure states, failed operation projection, secret-safe status display, no mutation path, and clear escalation posture.

### Journey 6: Tenant Administrator Proves Cross-Tenant Isolation

Elise administers a tenant organization that wants to enable agentic file work. Her concern is whether another tenant, agent, or integration surface can discover or touch her tenant's workspaces, provider bindings, credential references, commits, locks, or audit records.

She validates isolation with concrete evidence. A wrong-tenant request attempts to query workspace status, inspect provider configuration, prepare a workspace, acquire a lock, write a file, commit changes, and read audit metadata. Each attempt is denied before file, workspace, credential, repository, or commit access occurs. The denial shape is safe and does not leak whether unauthorized resources exist beyond what policy allows.

The value moment is proof. Elise can show that tenant isolation is enforced across API, CLI, MCP, and console surfaces, and that denial events are captured as metadata-only audit records.

This journey reveals requirements for tenant-scoped authorization, cross-surface authorization parity, safe error shapes, no cross-tenant enumeration, credential reference isolation, denial audit events, and zero tolerated cross-tenant access leaks.

### Journey 7: MCP/CLI/API Consumer Sees Cross-Surface Parity

An automation developer starts a workflow in the CLI to validate provider readiness and create a folder. Later, an AI tool continues through MCP to prepare the workspace, write files, and request a commit. An operator inspects the same workspace through the console, while a service integration queries status through the API.

Every surface reports the same truth. The workspace state, error categories, tenant authorization behavior, lock behavior, idempotency behavior, task/correlation metadata, and audit outcomes are consistent. If CLI says the workspace is `dirty`, MCP and API report `dirty`. If API returns a lock denial, MCP receives the same category of denial.

The value moment is operational confidence. Different entry points do not create different product semantics.

This journey reveals requirements for one canonical workflow contract, thin API/CLI/MCP adapters, shared status model, shared structured errors, shared authorization enforcement, shared audit metadata, and parity tests for MVP workflow commands.

### Journey 8: Audit Reviewer Reconstructs What Happened Without File Contents

A security or support reviewer investigates a customer incident after an agent task changed project files. The reviewer needs to answer who or what changed which paths, in which tenant and folder, under which task or correlation ID, with what outcome, and whether a Git commit was produced.

The audit view shows metadata only: tenant ID, folder ID, actor/client type, task/correlation ID, operation type, path metadata, lock lifecycle, status transitions, provider reference, commit SHA when available, timestamps, and denial events. It does not show file contents, provider tokens, credential material, or secrets.

The value moment is accountable traceability without data exposure. The reviewer can reconstruct the operational story and prove whether the agent task completed, failed, was denied, or left dirty state.

This journey reveals requirements for metadata-only audit events, audit projections, path-level change metadata, commit SHA capture, lock lifecycle audit, denial event capture, secret/file-content exclusion, and incident-support queries.

### Journey 9: Tenant Administrator Manages Folder Access and Lifecycle

Elise, the tenant administrator from Journey 6, returns to the day-to-day operation of her tenant. Her concern is no longer "can isolation be proven" but "can I run the tenant cleanly". She needs to grant folder access to users, groups, roles, and delegated service agents as her teams onboard new chatbots, retire old ones, and rotate ownership; she needs to inspect effective permissions for a folder so that an audit ask ("who can write here today?") has a concrete answer; and she needs to retire folders whose tasks are finished without erasing the audit evidence that future incident reviews depend on.

Elise grants folder access to a new automation team and verifies through the same surface that the grant took effect — the effective-permissions view shows the grant, the actor identity, and the verb scope. Months later, when a chatbot project is retired, Elise archives the folder. The folder enters a clearly archived lifecycle state, mutating commands are denied with a stable error, but the metadata-only audit trail, lock lifecycle history, last commit reference, and operation timeline remain queryable for the duration of the tenant's retention policy. No file contents, provider tokens, or credential material are revealed by the archived view; the same denial and isolation guarantees that protect active folders also protect archived ones.

The value moment is operational confidence. Elise can run her tenant — granting and revoking folder access, retiring folders, and answering audit questions — using the same product semantics, cross-surface parity, and metadata-only audit posture that the rest of the canonical workflow already enforces.

This journey reveals requirements for tenant-administrator folder access grant and revoke (FR5), effective-permissions inspection (FR6), folder lifecycle and archive (FR11–FR13), audit and status preservation for archived folders (FR14), denial of mutating operations on archived folders, retention-bound audit visibility, and cross-surface parity for tenant-administration commands and queries.

### Journey Requirements Summary

The journeys reveal these required capability areas:

- Canonical workspace states: `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.
- Provider readiness checks before repository-backed folder creation.
- Stable provider readiness reason codes for missing credentials, insufficient permissions, provider unavailability, unsupported capabilities, invalid branch policy, repository conflict, and rate limiting.
- Tenant-scoped organization/provider configuration and credential references.
- Git-backed folder/repository creation with narrow MVP provider behavior.
- Workspace preparation and task-scoped locking.
- Deterministic lock contention behavior across API, CLI, and MCP.
- Idempotent operation IDs for create, prepare, file operation, and commit retries.
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

The second is a canonical workspace control plane exposed through API, CLI, and MCP surfaces. These surfaces should not become separate product models. They should share the same command/query semantics, authorization behavior, workspace states, error categories, idempotency rules, and audit metadata.

A supporting innovation is the workspace trust surface. Hexalith.Folders makes operational states such as `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible` first-class product signals. This lets developers, operators, tenant administrators, and AI tools reason about whether a workspace can be trusted before, during, and after agent execution.

### Market Context & Competitive Landscape

The innovation is positioned primarily inside Hexalith. The comparison point is not a mature external product category; it is the internal pattern Hexalith.Folders is replacing: improvised temporary folders, direct Git CLI calls, scattered provider tokens, inconsistent cleanup logic, and chatbot-specific workspace orchestration.

Within Hexalith, Hexalith.Folders should become the common workspace persistence boundary for AI agents. Its value comes from standardizing tenant-scoped file work, Git-backed persistence, readiness checks, locking, status visibility, and audit metadata behind one product model.

### Validation Approach

The innovation is validated if the MVP proves that a chatbot or AI tool can complete the repository-backed task flow through the canonical contract: provider readiness, repository-backed folder creation, workspace preparation, task lock, add/change/remove files, commit, and status query.

The API, CLI, and MCP surfaces should validate the same workflow behavior rather than three separate implementations. Validation should confirm consistent workspace states, authorization checks, structured errors, idempotency behavior, and metadata-only audit across all surfaces.

The workspace trust model is validated if operators can understand key states without inspecting the filesystem or Git provider manually: whether the workspace is ready, locked, dirty, committed, failed, inaccessible, or blocked by provider readiness.

### Risk Mitigation

The main innovation risk is overbuilding the control plane before proving the core file workflow. If the AI-native task lifecycle model proves too heavy for the MVP, the fallback is a simpler Git-backed file API that supports repository creation, file add/change/remove operations, commit, and status query.

The second risk is surface divergence. API, CLI, and MCP could drift into different semantics. This should be mitigated by defining one canonical workflow contract and treating CLI and MCP as adapters over the same command/query model.

The third risk is provider abstraction leakage. GitHub and Forgejo should be supported through explicit capability checks and provider contract tests rather than assuming API compatibility.

The fourth risk is scope creep from the read-only operations console into a repair or Git administration console. MVP should keep the console diagnostic-only, with repair workflows deferred until the workspace state model is proven.

## API Backend Specific Requirements

### Project-Type Overview

Hexalith.Folders is an API/backend service module with REST API, CLI, MCP, SDK, projection, and read-only console surfaces. These surfaces must expose one canonical workspace workflow contract rather than separate product models.

The product is a tenant-scoped workspace control plane for agentic file work. It is not a content store, Git implementation, identity provider, generic Git provider management platform, prompt orchestration layer, execution sandbox, or repair console.

The MVP must support the repository-backed task flow: validate provider readiness, create or bind a Git-backed folder, prepare a workspace, acquire a task lock, add/change/remove files, commit Git-backed changes, query workspace and file context, inspect metadata-only audit, and expose diagnostic state through the read-only console.

### Architectural Boundaries

Hexalith.Tenants remains the source of truth for tenant identity, tenant lifecycle, and tenant membership. Hexalith.EventStore provides command, aggregate, event, and projection mechanics. Hexalith.Folders owns folder-specific policy, folder ACLs, provider binding references, workspace state, file-operation facts, commit metadata, and operational projections.

Hexalith.Folders owns intent, policy, state, and audit. Git providers own provider-specific repository and Git mechanics behind narrow provider ports. File contents and temporary working-copy material must remain outside EventStore; events, logs, projections, traces, and console responses must never contain file contents, provider tokens, credential material, or secrets.

Required provider ports include readiness validation, repository creation or binding, workspace preparation, governed file-operation application, Git-backed commit, provider status query, and cleanup/expiration support where needed.

### Public Surfaces

The REST API is the canonical external contract and must be documented through an OpenAPI `v1` contract.

The CLI, MCP server, SDK, and console are adapters or projections over the canonical contract. They must preserve the same workspace states, authorization checks, idempotency rules, lock behavior, structured error categories, correlation metadata, and audit metadata.

The read-only operations console must consume projections only. It must not expose mutation paths, credential material, file contents, hidden repair actions, or direct filesystem browsing.

### Endpoint Specifications

The API should be organized around capability groups:

- Provider readiness: validate provider binding, credential reference availability, provider capabilities, branch policy, repository naming policy, and provisioning readiness.
- Folder/repository lifecycle: create Git-backed folder/repository, bind existing repository where supported, inspect folder/repository binding metadata, and reject duplicate or cross-tenant bindings.
- Workspace lifecycle: prepare workspace, inspect workspace state, acquire lock, inspect lock, release lock where policy allows, and surface stale or interrupted lock state.
- File operations: add, change, and remove files while enforcing workspace-root confinement, path canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding policy, and case-collision handling.
- Commit operations: commit Git-backed changes with tenant, actor, task/correlation, author, message, changed-path, and commit SHA metadata.
- Query/status operations: expose workspace state, provider readiness, folder status, file tree, search, glob, metadata, partial reads, dirty state, failed operations, projection status, and last commit.
- Audit operations: expose metadata-only audit records for operations, status transitions, lock lifecycle, commit references, and authorization denials.
- Operations console queries: expose read-only projections for readiness, lock state, dirty state, failed operation, credential reference status, and provider/sync status.

Context queries are controlled workspace operations, not unrestricted repository browsing. Tree, search, glob, metadata, and partial-read responses must enforce tenant, folder ACL, path policy, include/exclude rules, binary handling, byte/range limits, result limits, and secret-safe response rules before ranking, summarization, snippet generation, or response shaping. Denied context queries must produce metadata-only audit evidence with actor, tenant, folder, query type, policy reason, correlation ID, timestamp, and safe error category.

### Command and Query Contract

The MVP contract must define DTOs for at least:

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

Mutating commands must support idempotency keys, correlation IDs, and expected version or conflict-detection semantics. Replayed commands with the same idempotency key must produce a stable result without duplicate domain events, duplicate provider writes, or duplicate commits.

Projection DTOs must be defined as read models, not direct event mirrors.

Operation-specific idempotency must be defined before implementation:

| Operation | Idempotency Requirement | Retry/Duplicate Outcome |
| --- | --- | --- |
| Provider readiness | Stable operation or correlation identity for readiness attempts | Duplicate checks return the same readiness result when provider evidence is unchanged. |
| Workspace preparation | Required idempotency key per task/workspace preparation attempt | Duplicate equivalent attempts return the same workspace preparation result; unknown provider outcomes require reconciliation before unsafe retry. |
| Lock acquisition | Required idempotency key per task lock request | Duplicate equivalent attempts return the same lock result; conflicting attempts return deterministic lock conflict. |
| File mutation or batch mutation | Required operation ID for every logical file change | Duplicate equivalent attempts do not duplicate file events, provider writes, or changed-path metadata. |
| Commit workspace | Required commit operation ID and task/correlation identity | Duplicate equivalent attempts do not create duplicate commits; unknown provider outcome moves to reconciliation-required status. |

### Authentication and Authorization Model

Authentication and tenant authorization should rely on existing Hexalith.Tenants and Hexalith.EventStore patterns. Hexalith.Folders adds folder-specific ACL checks.

Folder ACLs must define verbs for create, configure provider binding, prepare workspace, lock workspace, read metadata, read file content where allowed, mutate files, commit, query status, query audit, and view operations-console projections.

Cross-tenant access must be denied before file, workspace, credential, repository, lock, commit, provider, or audit access. Denials must use safe error shapes that avoid unauthorized resource enumeration.

Tenant IDs in payloads must be treated carefully. The authoritative tenant context should come from the authenticated/request context and EventStore envelope; any tenant identifier in payloads must be validated against that context or rejected.

Tenant administrators can inspect provider binding ownership and non-secret credential-reference status for their tenant without seeing credential material or unauthorized repository details.

### Data Schemas

The API should use JSON command/query DTOs and structured response models. DTOs are public contracts and must be covered by schema/versioning tests.

File content transport must be explicit. MVP support should define whether content is accepted as inline text, base64 binary, stream/upload reference, or external content reference. Metadata-only events may include path, content hash, size, media type, content reference ID, operation ID, provider reference, actor, timestamps, and commit ID, but not file contents.

### Error Codes

Errors must be stable, machine-readable, and mapped consistently across REST API, CLI, MCP, and SDK.

Required error fields:

- `category`
- `code`
- `message`
- `correlationId`
- `retryable`
- `clientAction`
- `details`

Required error categories include authentication failure, tenant authorization denied, folder ACL denied, cross-tenant access denied, provider readiness failed, credential reference missing or invalid, provider permission insufficient, provider unavailable, provider rate limited, repository conflict, duplicate binding, workspace not ready, workspace locked, stale or interrupted lock, dirty workspace, path validation failed, file operation failed, commit failed, unknown provider outcome, reconciliation required, idempotency conflict, unsupported provider capability, read-model unavailable, and audit access denied.

Error responses should indicate whether the client may retry, must refresh state, must request authorization, must change input, or must escalate provider/configuration failure.

Provider and workspace failures must distinguish known failure from unknown outcome. If repository creation, file mutation, or commit status cannot be confirmed after a timeout or provider interruption, the system must expose reconciliation-required status rather than retrying in a way that could duplicate repositories, file changes, or commits.

### Rate Limits, Throttling, and Idempotency

The MVP does not require fixed public rate-limit numbers, but it must include throttling and idempotency protections for repository creation, provider readiness checks, file operations, commits, and provider API calls.

Throttling policy must identify enforcement dimensions such as tenant, folder, workspace, provider, and command type.

Provider calls should use retry/backoff policy, provider-specific throttling, and clear failure projection when rate limits, timeouts, permission failures, or unknown outcomes occur.

### Workspace State and Concurrency

The MVP must define a workspace state model covering at least `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.

The product vocabulary must also account for lifecycle and failure states such as `requested`, `preparing`, `changes_staged`, `unknown_provider_outcome`, and `reconciliation_required`, even if final API enum names are refined during architecture.

Lock semantics must define owner, lease duration, renewal, expiry, release, stale lock behavior, reentrant behavior, and conflict behavior.

The MVP lock boundary is one active writer per tenant, folder, repository binding, and task workspace scope. Governed file mutations and commits require a valid mutation lock. Context queries and read-only status inspection do not require the mutation lock, but they still require tenant, folder ACL, and path-policy authorization. Commit must either release the lock or transition it to a defined terminal or recovery state.

Concurrent operations must either serialize deterministically or fail with stable conflict errors. Commit without a valid lock, mutation without authorization, duplicate lock acquisition, and retry after unknown provider outcome must have defined behavior.

Query/status responses must distinguish accepted command state from projected state when projection lag exists.

### API Versioning

The initial API should be versioned as `v1`. Additive evolution should be preferred. Breaking changes to command/query DTOs, event payloads, error categories, workspace states, provider capabilities, or SDK models require explicit versioning.

Event payload evolution should use schema versions and backward-compatible consumers.

### SDK Requirements

An SDK is required for MVP or near-MVP adoption, with the first supported language aligned to the primary Hexalith implementation ecosystem.

The initial SDK may be generated or minimal, but it must preserve the canonical API semantics. It must support authentication configuration, idempotency keys, correlation IDs, typed request/response models, typed errors or result categories, async operations where applicable, retry/idempotency helpers, and task-based examples for the core repository workflow.

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

- 100% command/query coverage in adapter parity tests for MVP workflow commands across API, CLI, MCP, and SDK.
- 100% ACL matrix coverage for command/query authorization decisions.
- 100% provider contract suite pass for each supported provider before marking that provider ready.
- Zero event schemas, logs, traces, projections, console responses, or audit records containing file contents, provider tokens, credential material, or secrets.
- Idempotency tests proving no duplicate domain events and no duplicate provider commits.
- Tenant isolation tests proving no cross-tenant read, write, lock, commit, provider, audit, or projection access.
- Path security tests for traversal, absolute paths, mixed separators, encoded traversal, reserved names, Unicode normalization, symlinks, and case sensitivity.
- Read-model determinism: rebuilding views from an empty read model must produce equivalent state from the same ordered event stream.
- Golden schema tests for DTO versioning and error mapping.
- Provider failure tests for timeout, 401, 403, 404, 409, 429, 5xx, branch protection, missing repository, deleted repository, stale clone, credential revocation, and provider drift.
- Provider contract tests for GitHub and Forgejo must cover readiness, repository binding, branch/ref handling, file operations, commit behavior, credential-reference usage, retry/idempotency behavior, and unknown outcome handling before either provider is marked ready.
- Context-query security tests must cover unauthorized tenant access, unauthorized folder access, excluded paths, binary files, large files, range limits, result limits, traversal attempts, symlinks, generated context payload redaction, and denial audit records.
- Redaction tests must inject sentinel secrets and file-content markers, then verify they do not appear in logs, traces, metrics labels, events, audit records, console views, provider diagnostics, error responses, or generated artifacts.

### Implementation Considerations

Implementation should avoid separate business logic paths for API, CLI, MCP, and SDK. Shared application services or generated client contracts should define canonical behavior, with each surface adapting transport and presentation only.

Provider readiness must stay narrowly scoped to health, capability discovery, credential reference validation, repository policy validation, and workspace safety. It must not become a broad provider administration platform.

The read-only operations console should remain read-model–based and non-authoritative. Repair workflows remain post-MVP.

If API/CLI/MCP/SDK scope threatens MVP delivery, the fallback is to keep the REST API and one SDK as canonical first, then implement CLI and MCP as thin adapters over that contract.

### Architecture Decisions Needed Next

The PRD defines product requirements rather than final architecture. Architecture must resolve Git-backed workspace implementation mechanics, file content transport model, large-file and binary policy, provider capability contract shape, projection compaction strategy, lock lease/expiry defaults, and file-operation batch atomicity before implementation stories are finalized.

### Deferred Quantitative Targets — Architecture Exit Criteria

The PRD intentionally defers the following numeric targets to the architecture review. Each target must be set, recorded, and validated before MVP release. Architecture may revise these targets only with documented rationale.

| ID | Target | PRD Source | Status |
| --- | --- | --- | --- |
| C1 | Concurrent capacity targets: maximum concurrent tenants, folders per tenant, active workspaces per tenant, and concurrent agent tasks per tenant | NFR Scalability and Capacity (constraint that capacity targets must avoid assuming a single tenant, single repository, or single active workspace) | TBD by architecture review |
| C2 | Status-freshness target: maximum acceptable lag between an emitted lifecycle event and its appearance in status/audit views under normal operation | NFR Observability, Auditability, and Replay | TBD by architecture review |
| C3 | Retention durations per data class: audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, and cleanup records | NFR Data Retention and Cleanup | TBD by architecture review |
| C4 | Bounded MVP input limits: maximum files per context query, maximum bytes per query response, maximum result count, and maximum query duration | NFR Performance and Query Bounds | TBD by architecture review |
| C5 | Concrete scalability quantifiers replacing the word "multiple" in the NFR scalability constraint, derived from C1 | NFR Scalability and Capacity | TBD by architecture review |

Each target must be (a) set as a concrete number with a measurement method, (b) validated through implementation benchmarks before MVP release, and (c) recorded in the Architecture document and referenced from this PRD via update.

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
- Workspace states: `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.
- File context queries: tree, metadata, search, glob, and partial reads.
- Metadata-only audit that records actor, tenant, provider, folder/repository, operation, target path, correlation ID, timestamps, result, commit reference, and error category while excluding file contents and secrets.
- REST API as canonical contract, with CLI, MCP, and SDK parity for core MVP workflow.
- Read-only operations console limited to readiness, locks, workflow/status, audit, provider health, dirty state, failed operation, and last commit visibility.
- Risk-based validation matrix for tenant isolation, provider contracts, adapter parity, locking, commit integrity, audit/status, and context query correctness.

**MVP Acceptance Evidence:**

- One end-to-end parity scenario runs through REST, CLI, MCP, and SDK.
- GitHub and Forgejo both pass automated provider contract tests.
- Cross-tenant negative tests cover tenant IDs, provider credentials, repository bindings, locks, audit visibility, and context queries.
- Failure-mode tests cover concurrent agents, stale locks, failed commits, provider unavailability, unauthorized access, and audit reconstruction.
- Adapter parity proves equivalent capability coverage, authorization behavior, error categories, operation IDs, audit entries, and status results.
- Tenant isolation tests prove a task can only access repositories, credentials, policies, locks, audit records, and context indexes belonging to its tenant; cross-tenant identifiers return non-disclosing failures.
- Provider callbacks and webhooks are tenant-bound, replay-safe, and covered by duplicate-delivery tests.
- Large change-set projections preserve counts, operation types, failure attribution, and explicit limits without exposing unbounded per-file detail to routine user-facing surfaces.

### Explicit MVP Non-Goals

- No repair automation.
- No brownfield migration wizard.
- No rich operations workflow surface.
- No deep drift remediation.
- No local filesystem-only workspace mode as an MVP product capability.
- No nested repository orchestration.
- No multi-agent simultaneous write collaboration.
- No file editing or file-content browsing in the operations console.
- No raw file diffs or file-content display in the operations console.
- No broad provider framework beyond proving GitHub and Forgejo well.
- No secret material storage in Hexalith.Folders; only credential references may appear where authorized.
- No policy engine beyond required tenant, provider, readiness, ACL, and workspace controls.

### Post-MVP Features

**Phase 2:** Repair commands, brownfield adoption, deeper drift detection, richer provider capability recipes, large-file policy expansion, and broader operations workflows.

**Phase 3:** Additional Git providers, migration/provider portability tooling, deeper AI context indexing, wider repair automation, and Hexalith-wide adoption as the default durable workspace substrate.

### Risk Mitigation Strategy

**Technical Risks:** Provider mismatch, wrong-repo mutation, stale locks, commit failure, event volume, path traversal, adapter drift, and cross-tenant leakage. Mitigate with contract tests, negative isolation tests, path security suites, idempotency tests, and parity scenarios.

**Market Risks:** Building a platform before proving the agent job. Mitigate with the canonical task lifecycle demo and operational evidence after completion or failure.

**Resource Risks:** If capacity tightens, preserve the canonical REST contract, tenant isolation, readiness gate, lock/file/commit/status/audit workflow, and provider tests first. Do not cut security, idempotency, or failure visibility.

## Functional Requirements

Functional Requirements are organized by capability area. Each block traces back to the User Journeys above and to the broader Hexalith.Folders objective stated in the Executive Summary: provide a tenant-scoped, auditable, recoverable folder lifecycle for agentic file work, accessible through API, CLI, MCP, and SDK with consistent semantics.

### Capability Contract Terms

- FR1: Users can distinguish logical folders, repositories, workspaces, tasks, locks, providers, context queries, audit records, and status records through consistent product terminology.
- FR2: Users can understand the canonical task lifecycle from provider readiness through repository binding, workspace preparation, lock, file operations, commit, context query, status, audit, and cleanup visibility.
- FR3: Users can distinguish mutating lifecycle commands from read-only readiness, status, context, audit, and diagnostic queries.

### Authorization and Tenant Boundary

- FR4: Tenant administrators can configure the minimum tenant and folder access controls required for repository-backed workspace tasks.
- FR5: Tenant administrators can grant folder access to users, groups, roles, and delegated service agents.
- FR6: Authorized actors can inspect effective permissions for a folder or task context.
- FR7: Platform engineers and tenant administrators can inspect whether a tenant is ready to run repository-backed workspace tasks.
- FR8: The system can evaluate every operation against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
- FR9: The system can deny unauthorized or cross-tenant operations before exposing folder, repository, credential, lock, file, audit, provider, or context information.
- FR10: The system can produce authorization evidence for allowed and denied operations without exposing unauthorized resource details.

### Folder Lifecycle

- FR11: Authorized actors can create logical folders within a tenant.
- FR12: Authorized actors can inspect folder lifecycle and binding status.
- FR13: Authorized actors can archive folders when policy allows.
- FR14: The system can preserve audit and status evidence for archived folders.

### Provider Readiness and Repository Binding

- FR15: Platform engineers can configure supported Git provider bindings and credential references for a tenant.
- FR16: Authorized actors can validate provider readiness before repository-backed folder creation or binding.
- FR17: The system can report provider readiness diagnostics with safe reason, retryability, remediation category, provider reference, and correlation ID.
- FR18: Authorized actors can create a repository-backed folder when readiness checks pass.
- FR19: Authorized actors can bind a folder to an existing repository where supported.
- FR20: Authorized actors can define or select the branch/ref policy used by repository-backed folder tasks.
- FR21: The system can expose provider, credential-reference, repository-binding, branch/ref, and capability metadata without exposing secrets.
- FR22: The system can expose GitHub and Forgejo capability differences required to complete the canonical lifecycle.
- FR23: Platform engineers can inspect whether each supported provider is ready for the canonical lifecycle, including readiness, repository binding, branch/ref handling, file operations, commit, status, and failure behavior.

### Workspace and Lock Lifecycle

- FR24: Authorized actors can prepare a workspace when provider readiness, repository binding, branch/ref policy, and task context are valid.
- FR25: Authorized actors can acquire a task-scoped workspace lock.
- FR26: Authorized actors can inspect permitted lock state, owner, task, age, expiry, and retry eligibility metadata.
- FR27: The system can deny competing operations when lock ownership or workspace state makes the operation unsafe.
- FR28: The system can represent active, expired, stale, abandoned, interrupted, and released lock states through observable status transitions.
- FR29: Authorized actors can release a workspace lock when ownership and policy allow.
- FR30: The system can expose workspace cleanup status for completed, failed, interrupted, or abandoned task lifecycles without providing repair automation in MVP.
- FR31: Authorized actors can inspect whether workspace, task, audit, or provider status is current, delayed, failed, or unavailable.

### File Operations and Context Queries

- FR32: Authorized actors can add, change, and remove files within a prepared and locked workspace.
- FR33: The system can reject file operations that violate workspace boundary, path, branch/ref, lock, tenant, provider, or folder policy.
- FR34: Authorized actors can request policy-filtered task context through file tree, metadata, search, glob, and bounded range reads.
- FR35: The system can apply policy boundaries to context queries, including permitted paths, excluded paths, binary handling, range limits, and secret-safe responses.
- FR36: The operations console can remain read-only and excluded from file editing or file-content browsing capabilities.

### Commit, Evidence, and Idempotency

- FR37: Authorized actors can commit workspace changes for repository-backed folders.
- FR38: Authorized actors can attach task, operation, correlation, actor, author, branch/ref, commit message, and changed-path metadata to file operations and commits.
- FR39: The system can expose task and commit evidence including provider, repository binding, branch/ref, changed paths, result status, commit reference, timestamps, task ID, operation ID, and correlation ID.
- FR40: The system can report failed, incomplete, duplicate, retried, or conflicting operations with stable status and audit evidence.
- FR41: The system can support idempotent retries for lifecycle operations using stable task, operation, and correlation identifiers.
- FR42: The system can reject duplicate logical operations when retry identity or operation intent conflicts.

### Error, Status, and Diagnostics Contract

- FR43: The system can expose a canonical error taxonomy across supported surfaces.
- FR44: The error taxonomy can distinguish validation failure, authentication failure, tenant denial, folder policy denial, credential failure, provider unavailable, unsupported capability, repository conflict, branch/ref conflict, lock conflict, stale workspace, path policy denial, commit failure, read-model unavailable, duplicate operation, and transient infrastructure failure.
- FR45: The system can expose canonical workspace and task states including `ready`, `locked`, `dirty`, `committed`, `failed`, and `inaccessible`.
- FR46: The system can explain final state, retry eligibility, and operational evidence after workspace preparation, lock, file operation, commit, provider, or read-model failure.

### Cross-Surface Contract

- FR47: API consumers can use a versioned canonical REST contract for the complete repository-backed task lifecycle.
- FR48: CLI users can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR49: MCP clients can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR50: SDK consumers can perform the canonical repository-backed task lifecycle with equivalent operation identity, status, errors, and audit behavior.
- FR51: The system can expose cross-surface equivalence for authorization behavior, error categories, operation IDs, audit records, status transitions, and provider capability behavior.

### Audit and Operations Visibility

- FR52: Operators can inspect read-only readiness, binding, workspace, lock, dirty state, commit, failure, provider, credential-reference, and sync status.
- FR53: Operators and audit reviewers can inspect metadata-only audit trails for successful, denied, failed, retried, and duplicate operations.
- FR54: Audit reviewers can reconstruct incidents from immutable metadata covering actor, tenant, task, operation ID, correlation ID, provider, repository binding, folder, path metadata, result, timestamp, status, and commit reference.
- FR55: The system can exclude file contents, provider tokens, credential material, and secrets from events, logs, traces, projections, audit records, diagnostics, and console responses.
- FR56: The system can expose operation timelines for folder, workspace, file, lock, commit, provider, status, and authorization events.
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

## Non-Functional Requirements

### Security and Tenant Isolation

- Tenant isolation must be enforced on every command, query, event, read-model view, lock, repository binding, context query, cleanup view, provider callback, and audit record.
- Cross-tenant access leaks are zero-tolerance defects. No object from tenant A may be retrievable, inferable, lockable, committed, queried, audited, or visible from tenant B.
- Tenant isolation tests must cover API responses, errors, events, logs, metrics labels, projections, cache keys, lock keys, temporary paths, provider credentials, repository bindings, background jobs, provider callbacks, audit records, and context-query results.
- File contents, diffs, prompts, provider tokens, credential material, secrets, remote URLs with embedded credentials, generated context payloads, and unauthorized resource existence must not appear in events, logs, traces, metrics, projections, diagnostics, audit records, provider payload snapshots, exception messages, command arguments, or console responses.
- Secrets and sensitive payloads must be redacted at source, with automated sanitizer tests and forbidden-field scanning in CI.
- Authorization denials must use safe error shapes that avoid unauthorized resource enumeration.
- Credential references must be validated and displayed only as non-secret identifiers or status indicators.
- Provider credentials and repository bindings must be tenant-scoped and must not be reused across tenants, even if repository URLs appear identical.
- Provider credentials must use the least privilege required for supported lifecycle operations and must be validated against required provider capabilities before use.
- Build, dependency, package, and generated SDK artifacts must be traceable to source and must not include secrets or tenant data.

### Reliability, Idempotency, and Failure Visibility

- Every lifecycle step must expose terminal and non-terminal state, including `Pending`, `InProgress`, `Succeeded`, `Failed`, and `Cancelled` where cancellation is supported.
- Required observable lifecycle states include `ProviderReady`, `RepositoryBound`, `WorkspacePrepared`, `Locked`, `FilesChanged`, `CommitPending`, `Committed`, `CleanupPending`, and `Cleaned`.
- Repository-backed task lifecycle operations must leave an inspectable final or intermediate state after interruption, provider failure, commit failure, lock contention, read-model lag, or retry.
- Idempotency keys are required for workspace preparation, lock acquisition, file mutation, commit, and cleanup request operations.
- A repeated call with the same idempotency key and equivalent payload must return the same logical result; the same key with a conflicting payload must return an idempotency conflict.
- Idempotent lifecycle operations must not create duplicate domain events, duplicate provider writes, duplicate file changes, duplicate repositories, or duplicate commits.
- Lock acquisition must be deterministic, tenant-scoped, and limited to one active write lock per tenant/repository/workspace scope.
- Lock behavior must define conflict response, lease duration, renewal behavior, expiry behavior, cleanup after failed commit, and whether commit releases the lock.
- Lock contention, stale locks, abandoned locks, and interrupted tasks must produce deterministic status, retry eligibility, reason code, timestamp, and correlation ID.
- Failure visibility must expose state, cause category, retryability, and correlation ID without providing automated remediation in MVP.

### Performance and Query Bounds

- Command submission must acknowledge accepted lifecycle commands within 1 second p95 before asynchronous provider or workspace work continues.
- Status and audit summary queries must return within 500 ms p95 for bounded MVP inputs.
- Context queries must return within 2 seconds p95 for bounded MVP inputs.
- Performance targets apply to bounded MVP inputs and control-plane responses. Targets must be validated against implementation benchmarks and recalibrated before release if provider or runtime constraints make the initial target misleading.
- Provider and workspace operations may complete asynchronously when external Git provider latency or workspace size exceeds interactive response budgets; callers must receive operation identity and status visibility rather than blocking indefinitely.
- Context queries must define and enforce maximum files, maximum bytes, maximum result count, maximum query duration, timeout behavior, truncation behavior, and included/excluded result audit visibility.
- File tree, search, glob, metadata, and bounded range queries must protect the service from unbounded workspace scans.
- Large file and binary handling limits must be explicit before MVP release; unsupported files must fail with stable policy errors rather than causing unbounded processing.
- Provider calls must use explicit timeout budgets, retry limits, and backoff caps.
- Provider calls must report timeout, rate-limit, unavailable, partial-success, and unknown-outcome states rather than leaving callers waiting indefinitely.
- Provider rate-limit responses must preserve retry hints where available and expose retry-after or classified retryability.

### Scalability and Capacity

- The system must support multiple tenants, folders, repositories, workspaces, and concurrent agent tasks without shared mutable state causing cross-tenant or cross-task interference.
- Folder and workspace operations must be scoped by tenant and folder boundaries rather than relying on a single global operation bottleneck.
- Audit, timeline, and file-context projections must remain queryable as folder history grows.
- Large batches of file operations must remain traceable without making routine status, audit, or context queries unusable.
- MVP capacity targets must avoid assuming a single tenant, single repository, or single active workspace, while avoiding unsupported claims about massive scale before concrete load targets are defined.

### Integration and Contract Compatibility

- REST, CLI, MCP, and SDK surfaces must preserve equivalent operation identity, lifecycle semantics, authorization behavior, error categories, status transitions, and audit outcomes; transport shape and UX may differ.
- Public contracts must be versioned. Breaking changes to lifecycle commands, queries, error categories, workspace states, provider capabilities, or audit fields require an explicit new versioned contract.
- The product must support at least the active contract version and define a deprecation policy before removing any public lifecycle contract.
- Shared or generated contract tests must validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
- Generated SDKs, MCP tool definitions, CLI command schemas, and OpenAPI contracts must be derived from or validated against the same canonical lifecycle contract.
- GitHub and Forgejo support must be validated through provider contract tests before either provider is marked ready.
- Provider contract tests must cover only MVP-dependent lifecycle behavior: readiness, repository binding, branch/ref handling, file operations, commit, status, provider errors, and failure behavior.
- Supported GitHub and Forgejo API versions or behavior assumptions must be pinned or recorded so provider compatibility drift is visible.
- Provider capability differences must be reported explicitly instead of inferred by clients from failed operations.
- Provider failures such as timeout, rate limit, authentication failure, authorization failure, repository missing, repository conflict, branch/ref conflict, unavailable provider, invalid path, commit rejected, and unknown outcome must map to stable product error categories.

### Observability, Auditability, and Replay

- Every successful, denied, failed, retried, duplicate, lock, file, commit, provider-readiness, and status-transition operation must be traceable by tenant, actor, task ID, operation ID, correlation ID, folder, provider, repository binding, timestamp, result, duration, state transition, and sanitized error category where applicable.
- Audit data must be metadata-only and sufficient to reconstruct what happened without exposing file contents or secrets.
- Allowed audit metadata must be explicitly classified. File paths, commit messages, repository names, branch names, and provider error payloads must be treated as potentially sensitive metadata.
- Sensitive audit metadata such as file paths, branch names, commit messages, repository names, and provider diagnostic payloads must be classified and protected through access control, hashing, truncation, or redaction where appropriate.
- Operations-console views must be read-model–based, read-only, and limited to lifecycle, status, readiness, lock, failure, provider, and audit metadata.
- Rebuilding read-model views from an empty read model must produce deterministic status, audit, and timeline results from the same ordered event stream, excluding explicitly nondeterministic generated values.
- Lifecycle events must appear in status/audit views within a defined status-freshness target under normal operation.
- The system must expose operational signals for provider readiness failures, stale projections, lock conflicts, dirty workspaces, failed commits, inaccessible workspaces, retryability, and cleanup status.
- Backup or recovery expectations must preserve durable events or authoritative records needed to rebuild status, audit, and timeline projections.

### Data Retention and Cleanup

- Retention periods must be defined for audit metadata, workspace status, provider correlation IDs, projections, temporary working files, and cleanup records.
- Retention durations are policy decisions and must be defined before production release; the PRD requires explicit retention semantics but does not set final retention periods.
- Tenant deletion must define which records are deleted, tombstoned, retained for audit, or anonymized.
- Workspace cleanup visibility must state whether cleanup is automatic, best-effort, retryable, user-triggered, or status-only for MVP.
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
