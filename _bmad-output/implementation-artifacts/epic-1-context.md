# Epic 1 Context: Canonical Contract and Adapter Foundation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give API consumers and adapter implementers one versioned OpenAPI v2 Contract Spine governing REST, SDK, CLI, MCP, schemas, errors, and parity evidence. Deterministic generation and conformance gates keep implementations consistent. Candidate generation, story acceptance, and production exposure require distinct evidence and authority.

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

The spine owns operation and schema identities; product decisions own scope, actors, safety, and user-visible outcomes. Reconcile conflicts before release. Hexalith.McpCli is the target CLI/MCP surface and is required for MVP. The retained Folders adapters are frozen compatibility binaries, limited to safety and continuity work, including the v2 migration. Their evidence does not qualify McpCli replacement or retirement.

Authorize before protected lookup, counts, filtering, provider calls, content reads, audit access, or search egress. Authenticate, establish fresh tenant authority and folder ACL, derive resource/task scope, then enforce EventStore validation and Dapr policy. Raw locators and payload tenant values confer no authority. Unauthenticated requests receive 401; fresh negative authority receives one byte-equivalent non-enumerating 404; stale, unavailable, conflicting, or incomplete authority receives one non-disclosing retryable 503. Protected v2 excludes caller-visible 403, not_found, cross_tenant_access_denied, and audit_access_denied.

Errors preserve category, code, safe message, correlation, retryability, client action, visibility, and closed metadata-only details across surfaces. Lifecycle, lock state, operation disposition, freshness, and disclosure remain separate dimensions. Exclude content, diffs, generated context, credentials, raw provider payloads, and unauthorized existence from metadata, telemetry, diagnostics, errors, and console output.

Every operation declares mutation or read semantics. Mutations require idempotency; reads reject keys before source execution. Intent equivalence excludes transport and retry metadata. Replay rechecks authorization; changed intent conflicts without revealing earlier intent; expired keys never re-execute. File mutation never auto-commits. Context queries remain bounded and subject to current authorization and path policy.

## Technical Decisions

Use the repository-pinned .NET SDK, modern solution format, central package management, and warnings as errors. Development/Debug consumes project references; Release/CI consumes NuGet packages. Both modes must build. Reuse platform hosting, persistence, and subscription capabilities; initialize only root-declared submodules. Approval-bound provider/native pins require compatibility evidence before change.

OpenAPI 3.1 drives deterministic typed SDK generation and idempotency helpers from ordered equivalence fields. Reads have no key helper; the SDK never invents mutation keys. Golden regeneration must be reproducible and emitted REST schemas must match the spine. Frozen compatibility CLI/MCP adapters wrap the SDK. McpCli enrollment instead uses decorated Contracts through the EventStore Gateway and must prove domain-service authorization equivalent to REST before it counts toward MVP.

C13 derives its denominator from operations and proves transport and adapter behavior with zero material deltas. Exceptions require approved rationale; removals require deprecation evidence. Previous-spine fingerprints cover status and error vocabularies. Server, SDK, adapter/UI consumption, drift, previous-spine, and parity artifacts regenerate together. Authorization coverage includes fourteen canonical access states and every protected operation family, with explicit folder/task binding and zero protected reads before denial.

Consumer discovery examines deployed consumers and package feeds. A deployed external v1 consumer blocks exposure until an explicit migration decision is accepted. Historical v1 remains evidence rather than the supported production contract. Local technical checks cannot substitute for deployment qualification or operational acceptance.

The execution manifest records removal of the general planning hold while withholding ordinary execution, v2 exposure, and automatic tracker regeneration. Scoped authorization is not closure. Preserve historical exact-digest approvals. Jerome's single-owner policy governs new decisions; changed bytes require current technical rechecks, while material behavior, scope, exposure, risk, or rollback changes require a new decision.

## UX & Interaction Patterns

Preserve vocabulary consumed by the read-only, metadata-only operations console. Safe denial is terminal for the caller; authority unavailability invites retry. Both provide safe correlation without confirming resource existence. Disclosure remains distinct from availability; withheld, redacted, unavailable, and missing values must not collapse into one presentation.

## Cross-Story Dependencies

Scaffold and root policy precede normative fixtures, decisions, equivalence, operation groups, generation, parity, gates, and publication. Execution ranks and prerequisite edges govern delivery. Story 1.17 closes only when bounded implementation slices and current conformance/authorization evidence satisfy accepted scope. Downstream stories consume the accepted contract. McpCli foundation, enrollment, parity, and legacy retirement follow their own named prerequisites and owner decisions; neither legacy compatibility checks nor v2 candidate generation closes them.
