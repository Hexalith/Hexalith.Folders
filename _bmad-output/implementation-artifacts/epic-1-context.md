# Epic 1 Context: Canonical Contract and Adapter Foundation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give consumers one OpenAPI v2 contract driving REST, SDK, CLI/MCP consumption, and parity. Shared rules and generation gates prevent drift. Generation does not authorize production exposure or closure.

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

The spine owns operation/schema identities; product decisions own actors, scope, and safety. `Hexalith.McpCli` is the CLI/MCP target. Proprietary Folders adapters are migration sources requiring inventory decisions and parity evidence before retirement; do not extend them with new capabilities.

Before protected lookup or effects, authorize in order: authentication, authority availability, fresh tenant authority, folder ACL/derived scope, EventStore validation, Dapr deny-by-default policy. Unauthenticated requests receive `401`; fresh negative authority receives one byte-equivalent non-enumerating `404`; stale/unavailable/conflicting/incomplete authority receives one non-disclosing retryable `503`. Protected v2 excludes `403`, `not_found`, `cross_tenant_access_denied`, and `audit_access_denied`. Locators and payload tenant identifiers never grant authority.

Errors require category, code, safe message, correlation, retryability, client action, and closed metadata-only details with visibility. Adapter projections must agree. Glossary/state casing is normative; lifecycle, lock state, disposition, freshness, and disclosure stay separate.

Every operation is mutation or read. Equivalence excludes transport/retry metadata. Replay rechecks authorization; different intent conflicts without prior-intent disclosure; expired keys never execute. Reads reject keys before source execution. Commit replay has longer retention.

Content, credentials, secrets, raw provider payloads/paths, and unauthorized existence stay out of persisted metadata, telemetry, errors, and evidence. Bounded context responses require authorization/path policy. Mutations do not auto-commit. Use .NET 10, `.slnx`, platform capabilities, and root-declared submodules.

## Technical Decisions

OpenAPI 3.1 drives generation. Extensions cannot weaken safety. NSwag generates the SDK and helpers from ordered equivalence fields; reads receive no helper and the SDK never invents a key. Golden regeneration is diff-free; emitted REST schemas match the spine.

C13 generates one row per operation: SDK/REST transport parity and CLI/MCP behavioral parity. Diagnostic exceptions need approved rationale; removals need deprecation evidence. Previous-spine fingerprints include status/error vocabulary. `read_model_unavailable` maps to CLI exit `73`; `concurrency_conflict` to exit `77` and matching MCP kind. Counts are generated.

EventStore owns idempotency admission after authentication, authorization, and validation. The v2 matrix covers fourteen access states and every protected operation family once. Diagnostics/task status carry folder scope; task lookup proves its binding. Gates prove zero protected reads before denial.

v1 is historical; accepted migration removes supported production routes/client targets. External v1 consumers block release pending a migration decision. Server/SDK/UI/parity/drift changes are validated together.

The general hold was removed after corrective A8; only Story 1.17 has scoped execution authorization. Closure, v2 exposure, and other ordinary-story execution remain unauthorized. Use its isolated branch/worktree lane and preserve bound main artifacts until combined closure checks pass. Historical digests remain provenance. Jerome's single-owner policy governs new decisions: changed bytes require technical rechecks; materially changed behavior, risk, scope, exposure, or rollback requires another decision. Never infer acceptance.

## UX & Interaction Patterns

The console is read-only and metadata-only. Denial never invites retry; authority unavailability does. Both show safe correlation without confirming existence. Visible/redacted/withheld/unknown/missing disclosure stays separate from availability. `unknown_provider_outcome` is auto-recovering during bounded checks, then may escalate to `reconciliation_required`/awaiting-human.

## Cross-Story Dependencies

Scaffold/policy precede fixtures, decisions, equivalence, operation groups, SDK/parity generation, gates, and publication. Story 1.17 closes only after its bounded slices pass contract, generation, drift, parity, and safe-authorization checks under the accepted decision scope.

Later epics consume the accepted contract. Story 4.23 owns lock identity/timing and adds v2-only renewal after 1.17 closes, with matching enrollment, authorization, idempotency, and parity. The October amendment leaves the bound candidate unchanged. Manifest ranks/prerequisites govern order; authorization stays separate from lifecycle tracking.
