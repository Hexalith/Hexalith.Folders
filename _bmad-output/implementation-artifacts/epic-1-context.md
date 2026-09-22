# Epic 1 Context: Canonical Contract and Adapter Foundation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give API consumers and adapter implementers one versioned OpenAPI contract for REST, the generated .NET SDK, CLI, MCP, schemas, errors, and parity evidence, so later work reuses one set of operation names, lifecycle terms, authorization outcomes, and idempotency rules. This epic scaffolds the module, authors that spine, generates the client and parity inventory, and gates drift and leakage. The corrected authorization contract is consumable only after its digest-bound approvals.

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

The spine owns operation names, wire schemas, and closed error fields. Product intent owns actors, scope, and safety outcomes. REST must emit a matching schema. The generated .NET SDK is the only current-release typed client; CLI and MCP wrap it. The console is read-only and outside mutation parity. Generated surfaces cannot override either source.

Glossary names and lowercase state casing are normative. Lifecycle, lock state, and operator disposition stay separate. `unknown_provider_outcome` is not `reconciliation_required`, and unconfirmed work is not a durable commit. The serializing identity is managed tenant plus canonical provider/repository identity plus normalized target ref. A task id never grants authority by itself.

Every operation is a mutation or a read. Mutations declare an ordered equivalence-field list, key rule, retention tier, and canonical target. Reads reject an idempotency key before protected execution. Equivalence uses tenant, operation, canonical target, normalized payload and options, policy version, and delegated task scope, and excludes correlation, tokens, clock, trace, delivery, and retry metadata. Unexpired equivalent intent replays one result; a different intent conflicts without revealing prior intent; an expired key never executes. Non-commit replay is 24 hours and commit replay uses the long tier, and replay rechecks current authorization.

Errors are problem details with category, code, message, correlation id, retryability, client action, and closed metadata-only details including visibility, mapped the same way across REST, SDK, CLI exits, and MCP failure kinds. Authorization finishes before any protected lookup or side effect: unauthenticated is 401; a fresh negative fact is one byte-equivalent non-enumerating 404; stale, unavailable, conflicting, or incomplete authority is one non-disclosing retryable 503. Caller-visible 403 and the legacy not-found, cross-tenant, and audit-denied codes are absent. Exact category and code strings come from the signed authorization matrix.

File contents, diffs, generated context, provider payloads, tokens, credentials, secrets, absolute paths, and unauthorized existence stay out of every output channel. Visible, redacted, withheld, unknown, and missing stay distinct from read-model availability. Context queries authorize and apply path policy before filtering or content access, with explicit path, entry, result, byte, aggregate, and execution-time bounds. Add, change, and remove are atomic and do not auto-commit; move or rename is add plus remove under one task and one commit.

Supported production exposure is version 2. Version 1 is historical, with no dual-write or in-place exception. A discovered external v1 consumer blocks release until a consumer-specific migration is approved. A post-approval change invalidates that approval, and the execution hold still blocks ordinary closure and exposure.

Build with .NET 10, `.slnx`, central package management, nullable references, implicit usings, and warnings as errors. Debug uses sibling project references; Release or an explicit NuGet-deps switch uses centrally versioned packages. Only root-declared submodules are required. Placeholders must not look like release evidence, and the contract is repository-backed.

## Technical Decisions

The spine is OpenAPI 3.1 in the contracts project. Extensions cover idempotency, correlation, lifecycle, parity, audit metadata, and sensitive-metadata tier, and cannot weaken safety invariants. NSwag emits the client and a hash helper from the lexically ordered equivalence fields; reads get no idempotency helper and the SDK never auto-generates a key. Emitted OpenAPI must match the spine, and regeneration must be diff-free.

One generated parity row exists per operation. SDK and REST consume transport columns; CLI and MCP consume behavioral columns. A not-applicable diagnostic cell needs an explicit rationale, and hand-authored rows cannot override the spine. Removals need an approved deprecation record. Previous-spine fingerprints include status codes and error vocabulary. `read_model_unavailable` is CLI exit 73; `concurrency_conflict` is CLI exit 77 and the matching MCP kind.

Admission belongs to the event-store seam, partitioned by managed tenant plus a protected key digest, and only after authentication, authorization, and canonical validation. Extensions cannot select the digest or tier. File content is inline JSON at or under 256KB, or a multipart stream, with an oversize response that says to retry as a stream. One SDK convenience chooses by length. Base64 content is rejected.

The v2 denominator covers fourteen access states and every protected operation family once. Readiness diagnostics, projection freshness, and task status take explicit folder scope, and task lookup must prove that membership. Caller-supplied locators and payload tenant ids are not authority. Approved input limits may shape schemas. The staged-working-file retention trigger and workspace transition map are superseded and approval-pending. Leakage gates use a sentinel corpus and assert no protected read before an equivalent safe denial.

## UX & Interaction Patterns

Preserve the console vocabulary without designing the console. Disposition labels are available, auto-recovering, degraded-but-serving, awaiting-human, and terminal-until-intervention. Denial must not invite a retry; authority unavailability must. Neither confirms resource existence. Disclosure is visible, redacted, withheld, unknown, or missing, separate from read-model unavailability. UI contract consumption stays read-only.

## Cross-Story Dependencies

Scaffold and root policy precede fixtures and the pre-spine decision record, then equivalence rules, the shared spine, operation groups, SDK and parity generation, the CI gates, and v2 publication. Publication is a governed aggregate: the parent closes only after every slice, the signed matrix digest, conformance, and the freeze decision. Relock-lane generation does not close the story or expose version 2.

Later epics consume this contract and do not rename operations, reopen error fields, or hand-author parity rows. Workspace runtime, durable content, the console, and security hardening stay separate; hardening reuses these envelopes. Epic numbers are not execution order. The manifest rank graph governs delivery, and the execution hold remains until relock approvals and the freeze decision.
