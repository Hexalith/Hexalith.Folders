# Epic 1 Context: Canonical Contract and Adapter Foundation

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Establish one versioned OpenAPI 3.1 v2 Contract Spine as the machine-readable source for REST, the generated .NET SDK, CLI, MCP, shared schemas, errors, and parity evidence. This foundation matters because downstream lifecycle, authorization, diagnostic, and adapter work must expose the same terminology, behavior, and safety guarantees without surface-specific interpretations or drift.

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

The Contract Spine must describe every current operation with a stable operation identifier, bounded schemas, authentication and authorization family, correlation and task behavior, metadata classification, audit behavior, closed error vocabulary, and C13 parity dimensions. Every operation is classified as either a mutation or a read: mutations declare canonically ordered intent-equivalence fields and retention rules, while reads reject idempotency keys before protected-source execution. REST, SDK, CLI, and MCP must preserve equivalent authorization, lifecycle, lock, idempotency, error, correlation, audit, and terminal-state semantics; any unsupported diagnostic adapter cell needs an explicit approved rationale.

Protected operations must authorize before lookup, count, filtering, provider access, file or content reads, audit access, or search egress. The v2 authorization denominator covers all fourteen canonical access states and all protected operation families exactly once. Unauthenticated requests use `401`; fresh negative authority uses one non-enumerating, byte-equivalent `404`; stale, unavailable, incomplete, or conflicting authority uses one retryable, non-disclosing `503`. Caller-visible `403`, `not_found`, `cross_tenant_access_denied`, and `audit_access_denied` are forbidden. Folder-scoped diagnostics and task status must prove fresh folder authority and task-to-folder binding. Error envelopes expose only closed, metadata-safe fields, including visibility, correlation, retryability, and client action; `read_model_unavailable` maps to CLI exit `73`, while `concurrency_conflict` maps to CLI exit `77` and MCP failure kind `concurrency_conflict`.

The generated SDK, C13 inventory, CLI/MCP projections, UI consumption, server behavior, `previous-spine.yaml` fingerprints, and v2 OpenAPI must be regenerated in lockstep and remain deterministic. Symmetric drift, server-versus-spine, generated-output, schema-completeness, parity-completeness, idempotency-encoding, and sentinel-leakage gates must fail closed. Historical v1 artifacts remain evidence only; discovery of a deployed external v1 consumer requires escalation rather than an invented migration window.

## Technical Decisions

Use .NET 10, C# 14, the `.slnx` solution, central package management, nullable analysis, implicit usings, and warnings-as-errors. Local/default Debug builds use root sibling project references; Release or `UseNuGetDeps=true` uses centrally versioned NuGet packages. Only root-declared submodules are required.

Contracts contain no behavior. NSwag generates the typed client and idempotency helpers from `hexalith.folders.v2.yaml`; generated files are not hand-edited. CLI and MCP are thin adapters over the SDK, while REST is a parallel transport governed by the same behavioral specification. JSON uses camelCase, timestamps use ISO-8601 UTC, identifiers use ULIDs, errors use RFC 9457 problem details, and correlation plus task identity propagate end to end.

Authorization and canonical validation precede idempotency descriptor construction and durable admission. The serializing workspace identity is managed tenant plus canonical provider/repository identity plus normalized target ref; folder, workspace, and task identifiers are metadata, not collision identity. Workspace lifecycle, lock state, operation state, operator disposition, freshness, and disclosure are separate closed dimensions. Contract and adapter tests consume generated C13 data rather than hand-authored mappings.

## UX & Interaction Patterns

The operations console is read-only and consumes the same semantic state and error models as the other surfaces. Tenant, folder, repository, workspace, task, and authorization scope remain visible before detailed evidence. Denial and authority-unavailable states must be visually distinct so operators know whether retry can help, while status, wording, and detail shape within each envelope must not reveal resource existence or tenant relationships.

## Cross-Story Dependencies

Scaffolding, configuration, fixtures, workshop decisions, and parity rules precede Contract Spine authoring. Shared vocabulary precedes capability-group contracts; the completed Spine precedes SDK and C13 generation; generated artifacts precede drift, safety, and completeness gates. Product Epics 2–6 and 10–13 consume these contracts and vocabularies, while Hexalith.Tenants and Hexalith.EventStore supply the sibling authorization and admission patterns.

The PD10 v2 candidate is generated under the relock milestone without exposing v2 or closing Story 1.17. Exact-digest A6b approval, conformance, and the A8 freeze decision are required before Story 1.17 becomes consumable and before the general execution hold can be removed.
