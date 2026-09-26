# Epic 1 Context: Canonical Contract and Adapter Foundation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give API consumers and adapter implementers one versioned OpenAPI contract for REST, the generated .NET SDK, CLI, MCP, schemas, errors, and parity evidence. Shared operation names, lifecycle terms, authorization outcomes, and idempotency rules prevent downstream drift. The epic also establishes generation and safety gates. The current v2 candidate remains unapproved for production exposure.

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

The spine owns operation names, wire schemas, and closed error fields; product decisions own actors, scope, and safety outcomes. REST must match the spine. CLI and MCP wrap the generated .NET SDK. The console is read-only. Generated surfaces cannot change product safety rules.

Glossary names and lowercase state casing are normative. Lifecycle, lock state, and operator disposition stay separate. `unknown_provider_outcome` is not `reconciliation_required`, and unconfirmed work is not a durable commit. The serializing identity is managed tenant plus canonical provider/repository identity plus normalized target ref. A task id never grants authority by itself.

Every operation is a mutation or a read. Mutations declare canonical idempotency fields, key rule, retention tier, and target; reads reject keys before protected execution. Equivalence excludes correlation, tokens, clock, trace, delivery, and retry metadata. Replay rechecks current authorization; different intent conflicts without revealing prior intent, and expired keys never execute. Commit replay has a longer retention tier than other mutations.

Errors are problem details with category, code, message, correlation id, retryability, client action, and closed metadata-only details including visibility, mapped the same way across REST, SDK, CLI exits, and MCP failure kinds. Authorization finishes before any protected lookup or side effect: unauthenticated is 401; a fresh negative fact is one byte-equivalent non-enumerating 404; stale, unavailable, conflicting, or incomplete authority is one non-disclosing retryable 503. Caller-visible 403 and the legacy not-found, cross-tenant, and audit-denied codes are absent. Exact category and code strings come from the signed authorization matrix.

File contents, diffs, generated context, provider payloads, tokens, credentials, secrets, absolute paths, and unauthorized existence stay out of events, logs, projections, audit, diagnostics, errors, and generated evidence. Authorized context-read responses may contain bounded content after authorization and path policy. Visible, redacted, withheld, unknown, and missing stay distinct from read-model availability. File mutations do not auto-commit; move or rename is add plus remove under one task and one commit.

The target production contract is v2; v1 is historical. An external v1 consumer blocks release until its migration is approved. Earlier A6b, Section 9, and A8 decisions bind specific candidate bytes; changes require renewed approval. Story 1.17 has scoped execution authorization, while its closure, other ordinary-story execution, and v2 exposure remain unauthorized.

Build with .NET 10, `.slnx`, central package management, nullable references, implicit usings, and warnings as errors. Debug uses sibling project references; Release uses centrally versioned packages. Only root-declared submodules are required. Placeholders must not look like release evidence.

## Technical Decisions

The spine is OpenAPI 3.1 in the contracts project. Extensions cover idempotency, correlation, lifecycle, parity, audit metadata, and sensitive-metadata tier, and cannot weaken safety invariants. NSwag emits the client and a hash helper from the lexically ordered equivalence fields; reads get no idempotency helper and the SDK never auto-generates a key. Emitted OpenAPI must match the spine, and regeneration must be diff-free.

One generated parity row exists per operation. SDK and REST consume transport columns; CLI and MCP consume behavioral columns. A not-applicable diagnostic cell needs an explicit rationale, and hand-authored rows cannot override the spine. Removals need an approved deprecation record. Previous-spine fingerprints include status codes and error vocabulary. `read_model_unavailable` is CLI exit 73; `concurrency_conflict` is CLI exit 77 and the matching MCP kind.

Idempotency admission belongs to the EventStore seam after authentication, authorization, and canonical validation. Contract extensions cannot choose the protected key digest or retention tier. File transport supports bounded inline and multipart streaming forms under the approved file policy.

The v2 authorization inventory covers fourteen access states and every protected operation family once. Diagnostics and task status carry explicit folder scope; task lookup proves its folder binding. Caller-supplied locators and payload tenant ids do not grant authority. Leakage gates assert zero protected reads before safe denial.

## UX & Interaction Patterns

Preserve the console vocabulary without designing the console. Disposition labels are available, auto-recovering, degraded-but-serving, awaiting-human, and terminal-until-intervention. Denial must not invite a retry; authority unavailability must. Neither confirms resource existence. Disclosure is visible, redacted, withheld, unknown, or missing, separate from read-model unavailability. UI contract consumption stays read-only.

## Cross-Story Dependencies

Scaffold and root policy precede fixtures and pre-spine decisions, then equivalence rules, the shared spine, operation groups, SDK and parity generation, CI gates, and v2 publication. Story 1.17 closes only after its bounded slices and renewed contract, drift, parity, safe-denial, and adapter evidence pass. Candidate generation alone does not expose v2.

Later epics consume this contract and do not rename operations, reopen error fields, or hand-author parity rows. Workspace runtime, durable content, the console, and security hardening stay separate; hardening reuses these envelopes. Epic numbers are not execution order; the manifest rank graph and explicit execution authorization govern delivery.
