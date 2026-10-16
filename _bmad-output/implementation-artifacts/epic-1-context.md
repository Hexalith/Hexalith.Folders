# Epic 1 Context: Canonical Contract and Adapter Foundation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give consumers and adapter implementers one versioned OpenAPI v2 Contract Spine driving REST, SDK, CLI, MCP, schemas, errors, and parity evidence. Generation gates prevent drift. Candidate generation, story closure, and production exposure require separate acceptance.

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

The spine owns operation and schema identities; product decisions own actors, scope, safety, and user-visible outcomes. Conflicts require reconciliation before release. `Hexalith.McpCli` is the CLI/MCP target; proprietary module adapters are migration sources requiring inventory, approved replacement or withdrawal, and behavior evidence before retirement.

Authorize before protected lookup or effects: authenticate, establish available and fresh tenant authority, prove folder ACL/derived scope, then enforce EventStore validation and Dapr deny-by-default policy. Locators, task identifiers, and payload tenant values grant no authority. Unauthenticated requests use `401`; fresh negative authority uses one byte-equivalent, non-enumerating `404`; stale, unavailable, conflicting, or incomplete authority uses one non-disclosing, retryable `503`. Protected v2 excludes `403`, `not_found`, `cross_tenant_access_denied`, and `audit_access_denied`.

Errors preserve category, code, safe message, correlation, retryability, client action, and closed metadata-only details with visibility across surfaces. Keep lifecycle, lock state, operation disposition, freshness, and disclosure separate; preserve their canonical vocabularies.

Every operation is classified as mutation or read. Mutations require idempotency; reads reject keys before source execution. Equivalence excludes transport/retry metadata. Replay rechecks authorization; changed intent conflicts without exposing prior intent; expired keys never execute. File mutations never auto-commit.

Exclude content, diffs, generated context, secrets, credentials, raw provider payloads, and unauthorized existence from metadata, telemetry, errors, console output, and evidence. Context reads remain bounded and subject to authorization and path policy.

## Technical Decisions

Use .NET 10, `.slnx`, central packages, and warnings as errors. Default/Debug consumes sibling projects; Release/CI consumes published NuGet packages. Both modes must build. Reuse platform capabilities; initialize only root-declared submodules.

OpenAPI 3.1 drives NSwag SDK generation and canonical idempotency helpers. Helpers follow ordered equivalence fields; reads have no helper; the SDK never invents mutation keys. Golden regeneration must be diff-free, and emitted REST schemas must match the spine. Extensions cannot weaken safety.

C13 generates one row per operation, proving SDK/REST transport parity and CLI/MCP behavioral parity. Diagnostic exceptions need approved rationale; removals need deprecation evidence. Previous-spine fingerprints include status/error vocabulary. Counts are generated. `read_model_unavailable` maps to CLI exit `73`; `concurrency_conflict` maps to exit `77` and matching MCP kind.

EventStore owns durable idempotency admission after authorization and validation. Authorization coverage includes all fourteen canonical access states and every protected operation family once. Diagnostic/task queries carry explicit folder scope; task lookup proves its binding. Gates prove zero protected reads before denial.

Keep v2 server, SDK, adapter, UI, parity, drift, and previous-spine artifacts synchronized. v1 becomes historical after accepted migration; deployed external v1 consumers require an explicit migration decision. Preserve `V1Only` and unset `T0` until migration entry gates pass.

The manifest records hold removal but withholds ordinary execution, v2 exposure, and Story 1.17 closure; 1.17 has scoped execution. Preserve bound artifacts and historical approvals. Jerome's single-owner policy requires technical rechecks for changed bytes and a new decision for material behavior, risk, scope, exposure, or rollback changes. Passing checks never implies acceptance.

## UX & Interaction Patterns

The console remains read-only and metadata-only. Denial is terminal for the caller; authority unavailability invites retry. Both expose safe correlation without confirming resource existence. Disclosure states remain distinct from availability.

## Cross-Story Dependencies

Scaffold and root policy precede fixtures, normative decisions, equivalence, operation groups, generation, parity, gates, and publication. Story 1.17 closes only after its bounded slices and combined conformance/authorization checks satisfy the accepted decision scope; later epics consume the accepted contract. Manifest ranks and exact prerequisites determine execution order. Story 4.23 adds lock identity/timing and v2 renewal after 1.17; its accepted October amendment leaves the bound 1.17 candidate unchanged.
