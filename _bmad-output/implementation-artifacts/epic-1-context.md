# Epic 1 Context: Canonical Contract and Adapter Foundation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give API consumers and adapter implementers one versioned OpenAPI v2 Contract Spine driving REST, SDK, CLI, MCP, schemas, errors, and parity evidence. Generation and conformance gates prevent drift before downstream features consume the contract. Candidate generation, story acceptance, and production exposure remain separate decisions.

## Stories

- Story 1.1: Scaffold the Consumer-Ready Folders Module
- Story 1.2: Establish Root Configuration and Submodule Policy
- Story 1.3: Seed Minimally Valid Normative Fixtures
- Story 1.4: Author Phase 0.5 Pre-Spine Workshop Deliverables
- Story 1.5: Finalize Idempotency Equivalence and Adapter Parity Rules
- Story 1.6: Author Contract Spine Foundation and Shared Extension Vocabulary
- Story 1.7: Author Tenant, Folder, Provider, and Repository-Binding Contract Groups
- Story 1.8: Author Workspace and Lock Contract Groups
- Story 1.9: Author File Mutation and Context Query Contract Groups
- Story 1.10: Author Commit and Workspace-Status Contract Groups
- Story 1.11: Author Audit and Ops-Console Query Contract Groups
- Story 1.12: Wire NSwag SDK Generation with Idempotency Helpers
- Story 1.13: Generate the C13 Parity Oracle
- Story 1.14: Wire Contract Spine Drift and Generated-Client CI Gates
- Story 1.15: Wire Safety-Invariant CI Gates
- Story 1.16: Wire Exit-Criteria and Parity-Completeness Gates
- Story 1.17: Publish the PD10 v2 Authorization Contract Spine

## Requirements & Constraints

The spine owns operation and schema identities; product decisions own scope, actors, safety, and user-visible outcomes. Reconcile conflicts before release. `Hexalith.McpCli` is the target CLI/MCP surface. Retiring proprietary module adapters requires an owner-approved operation inventory, accepted replacement or withdrawal, and behavior/parity evidence; historical adapter completion is compatibility evidence.

Authorize before protected lookup or effects: authenticate, establish available and fresh tenant authority, prove folder ACL and derived scope, then enforce EventStore validation and Dapr deny-by-default policy. Raw locators and payload tenant values confer no authority. Use `401` for unauthenticated requests, one byte-equivalent non-enumerating `404` for fresh negative authority, and one non-disclosing retryable `503` for stale, unavailable, conflicting, or incomplete authority. Protected v2 excludes `403`, `not_found`, `cross_tenant_access_denied`, and `audit_access_denied`.

Errors preserve category, code, safe message, correlation, retryability, client action, visibility, and closed metadata-only details across surfaces. Lifecycle, lock state, operation disposition, freshness, and disclosure remain separate dimensions with canonical vocabularies.

Classify every operation as mutation or read. Mutations require idempotency; reads reject keys before source execution. Equivalence excludes transport/retry metadata. Replay rechecks authorization; changed intent conflicts without exposing earlier intent; expired keys never execute. File mutation never auto-commits.

Exclude content, diffs, generated context, secrets, credentials, raw provider payloads, and unauthorized existence from metadata, telemetry, errors, console output, and evidence. Context queries remain bounded and subject to current authorization and path policy.

## Technical Decisions

Use .NET 10, `.slnx`, central package management, and warnings as errors. Development/Debug uses sibling project references; Release/CI uses published NuGet packages. Both modes must build. Reuse platform capabilities rather than duplicating hosting or subscription boilerplate; initialize only root-declared submodules. Approval-bound provider/native pins need compatibility evidence before change.

OpenAPI 3.1 drives NSwag SDK generation and idempotency helpers using the ordered equivalence fields. Reads have no helper, and the SDK never invents mutation keys. Golden regeneration must be diff-free; emitted REST schemas must match the spine. Extensions cannot weaken safety.

C13 generates its denominator from operations and proves SDK/REST transport parity plus per-adapter behavioral parity. Diagnostic exceptions require approved rationale; removals require deprecation evidence. Previous-spine fingerprints include status/error vocabulary. `read_model_unavailable` maps to CLI exit `73`; `concurrency_conflict` maps to CLI exit `77` and matching MCP kind.

EventStore owns durable idempotency admission after authorization and validation. Authorization coverage includes all fourteen canonical access states and every protected operation family once. Diagnostic/task queries carry explicit folder scope; task lookup proves that binding. Gates prove zero protected reads before denial.

Regenerate v2 server, SDK, adapter/UI consumption, parity, drift, and previous-spine artifacts together. Consumer discovery examines deployment inventories and package feeds; an external deployed v1 consumer blocks release pending an explicit migration decision. Preserve `V1Only` and unset `T0` until migration entry gates pass.

The manifest records hold removal while withholding ordinary execution and v2 exposure; Story 1.17 has scoped authorization and remains open. Preserve historical exact-digest approvals and bound artifacts. Jerome's current single-owner policy requires technical rechecks when bytes change and a new decision for material behavior, scope, risk, exposure, or rollback changes. Passing verification does not grant acceptance.

## UX & Interaction Patterns

Preserve the vocabulary consumed by the read-only, metadata-only operations console. Safe denial is terminal for the caller; authority unavailability invites retry. Both retain safe correlation without confirming resource existence. Disclosure states remain distinct from availability.

## Cross-Story Dependencies

Scaffold and root policy precede fixtures, normative decisions, equivalence, operation groups, generation, parity, gates, and publication. The manifest's strict execution ranks and exact prerequisite edges govern order. Story 1.17 closes only when its bounded slices and combined conformance/authorization evidence satisfy the accepted decision scope; downstream stories consume the accepted contract. Story 4.23 adds canonical lock identity/timing and v2 renewal after 1.17 closure; that amendment leaves the bound 1.17 candidate unchanged.
