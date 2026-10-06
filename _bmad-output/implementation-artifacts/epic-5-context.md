# Epic 5 Context: Cross-Surface Workflow Parity

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Let REST, SDK, CLI, and MCP consumers run one tenant-scoped, repository-backed task lifecycle with equivalent operation identity, authorization, idempotency, errors, audit evidence, and terminal states. Shared generated conformance evidence must prove that work can move between surfaces without changing its meaning or losing task and correlation identity.

## Stories

- Story 5.1: Ship SDK convenience helpers, samples, and quickstart
- Story 5.2: CLI tenant, folder, provider-readiness, and binding commands
- Story 5.3: MCP tenant, folder, provider-readiness, and binding tools/resources
- Story 5.4: Consume parity oracle in CLI and MCP tests
- Story 5.5: Validate golden lifecycle parity across REST and SDK
- Story 5.6: Validate behavioral parity across CLI and MCP
- Story 5.7: Validate mixed-surface handoff scenario
- Story 5.8: CLI workspace preparation and lock lifecycle
- Story 5.9: CLI file, context, commit, status, error, and audit behavior
- Story 5.10: MCP workspace preparation and lock lifecycle
- Story 5.11: MCP file, context, commit, status, error, and audit behavior

## Requirements & Constraints

- Preserve the ordered lifecycle: provider readiness, repository creation/binding, preparation, task lock, governed file changes, one durable commit, bounded context, status, audit, and release/cleanup visibility. Workspace lifecycle and lock states are separate contracts; lock states are `unlocked`, `locked`, `expired`, `stale`, and `revoked`.
- Evaluate current tenant/folder authorization before protected access and replay. Protected absent, hidden, wrong-tenant, and unauthorized targets share one non-disclosing denial; unavailable authority has a distinct safe availability result.
- Require idempotency for every mutation; reads reject keys before source access. Equivalent live replay preserves one logical result, conflicting intent fails, and expired keys never execute again. Unknown provider outcomes permit bounded read-only confirmation, then reconciliation/escalation rather than blind mutation retry.
- Preserve canonical errors, operation IDs, audit metadata, correlation/task IDs, input bounds, retry eligibility, and recovery states across all supported surface cells. Any material parity delta or missing required oracle row fails conformance.
- Keep file bodies, diffs, generated context, credentials, provider payloads, secrets, local absolute paths, and unauthorized existence out of audit, diagnostics, errors, and telemetry. Authorized context responses remain governed by their content policy.
- Production completion requires deployed positive and negative lifecycle evidence. Fakes, mocks, seed-only, in-memory, unavailable, and safe-empty paths establish neither durable capability nor production closure.

## Technical Decisions

- `Hexalith.McpCli` owns the target Hexalith CLI/MCP surfaces. Folders adapter stories feed an owner-approved operation inventory, decorated module Contracts, Gateway enrollment, and parity evidence. Existing proprietary Folders adapters are migration sources; unsupported legacy operations require explicit replacement, withdrawal, or deferral decisions before retirement.
- The OpenAPI 3.1 Contract Spine owns wire schemas and operation names; product requirements own safety and user-visible semantics. Use generated v2 clients and oracle rows. Conflicting authorities require reconciliation before release; historical v1 is not the production target.
- The generated SDK is the canonical typed client. Shared behavior still requires explicit verification of credential sourcing, pre-SDK validation, key/correlation/task sourcing, CLI exits, and MCP failure projection.
- CLI mutations require an explicit key or explicit auto-key opt-in; generated keys remain available for retries. MCP requires a supplied mutation key and never generates one. Task-scoped calls require caller-supplied task identity. Preserve supplied correlation IDs; defaults have invocation/tool-call scope.
- Project errors through the generated oracle, preserving category, code, safe message, correlation, visibility, retryability, client action, and closed metadata-only details. Protected v2 operations expose no `not_found` classification.
- Reuse EventStore-owned durable admission, authorization, lifecycle, lock, and recovery behavior. Adapters must not introduce local lifecycle semantics or independent provider/filesystem ownership.

## UX & Interaction Patterns

Use canonical state and reason language in human and machine output, with actionable retry or escalation guidance and safe correlation references. Distinguish denial, unavailability, redaction, missing evidence, and unknown outcome. Acceptance is not terminal success; provider confirmation determines durable commit success. Show unconfirmed outcomes as recovering while confirmation runs, then as requiring reconciliation if evidence remains unresolved.

## Cross-Story Dependencies

SDK and shared oracle foundations support transport, behavioral, and mixed-surface verification. Workspace/lock surfaces depend on Story 4.19's deployed durable path; file/context/commit/status/audit surfaces depend on Stories 4.20, 4.21, and 12.6. Preserve completed component evidence without treating it as production closure. The planning manifest governs execution order. Its September 22 corrective A8 reapproval removes the general hold, while older PRD prose and story-level held fields remain divergent; resolve those records against current approvals and actual prerequisite evidence before execution.
