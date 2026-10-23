# Epic 11 Context: Domain-Focus Platform Alignment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Remove Folders-local copies of shared Hexalith capabilities and consume Commons, EventStore, FrontComposer, and Memories primitives while preserving REST, SDK, CLI, MCP, worker, and UI behavior. This is technical alignment with verification and governance obligations, not new product scope or product-MVP completion. Durable source state and product projections remain owned by Epics 12, 4, 6, and 10.

## Stories

- Story 11.1: Establish refactor baseline and governance pin map
- Story 11.2: Inventory, assign, and pin platform prerequisites
- Story 11.3: Apply wire-preserving repository hygiene
- Story 11.4: Consolidate Server transport, envelope, and route helper duplication
- Story 11.5: Consolidate domain and provider helper duplication
- Story 11.6: Consolidate CLI/MCP adapter core and secure bearer transport
- Story 11.7: Consolidate deterministic time, context, and path test helpers
- Story 11.8: Adopt Commons/EventStore primitives in the Folders domain
- Story 11.9: Delete Hexalith.Folders.ServiceDefaults and consume Commons.ServiceDefaults
- Story 11.10: Adopt EventStore admission and subscription-mapping seams
- Story 11.11: Adopt FrontComposer user-context, token, OIDC, and shared-shell helpers
- Story 11.12: Modernize the generated client and shared idempotency/ULID helpers
- Story 11.13: Delete obsolete local code and synchronize planning/maintenance documents
- Story 11.14: Adopt Memories publication and search-client seams
- Story 11.15: Maintain the DCP-capable cross-repository verification lane
- Story 11.16: Replace brittle governance and CI pins with behavioral gates
- Story 11.17: Consolidate EventStore gateway doubles and rejection conformance
- Story 11.18: Consolidate provider and repository fakes in Folders.Testing
- Story 11.19: Adopt Fluent UI tables, controls, icons, loading, and layout primitives
- Story 11.20: Record AppHost, ServiceDefaults, query-handler, and tenant-semantic ADRs
- Story 11.21: Run final boundary, package, test, E2E, accessibility, and governance verification

## Requirements & Constraints

Preserve tenant isolation, metadata-only evidence, parity, observability, accessibility, traceability, and maintainability. Authoritative tenant context comes from authentication and the EventStore envelope; payload tenant identifiers are comparison inputs only. Authentication precedes authority availability and permission evaluation, and authorization precedes protected-resource lookup or observation. Denials must not reveal existence. Fresh authorization applies before asynchronous effects.

Preserve provider behavior, failure categories, deterministic identity, and contract equivalence. Events, logs, traces, metrics, diagnostics, and error responses exclude bodies, diffs, credentials, and secrets; tenant-sensitive metadata obeys C9 redaction. Provider features, correctness work, search optimization, and reserved-tenant decisions retain their separate owners.

Capture baseline builds, focused tests, formatting, package/route inventories, governance pins, and known blockers before substantive changes. Preserve unrelated changes and retained evidence. Shared-capability adoption requires an owning repository, upstream reference, compatible release or full SHA, availability status, consuming story, and verification evidence. Inventory work does not authorize dependency changes or upstream implementation.

Canonical REST and SDK contracts govern CLI/MCP behavior. Generated clients, fixtures, and parity inventories remain generated; changes move contracts, documentation, and tests together. Client modernization targets the approved v2 spine after Story 1.17 acceptance, preserving ProblemDetails and idempotency vectors without silently selecting historical v1.

Governance gates must reject unsafe counterexamples, retain named approvals and blocking full-lane coverage, and use generated inventories instead of stale counts. Do not narrow, skip, or fail open a gate. Test doubles provide deterministic unit evidence only and cannot satisfy real-provider, durable-state, or deployed-path acceptance. Blocked or degraded external lanes remain explicit blockers.

## Technical Decisions

Folders owns folder policy, aggregates, provider ports, authorization semantics, and product projections. Tenants supplies tenant authority; EventStore supplies durable events, admission, subscriptions, and shared domain-service mechanics; Commons supplies cross-cutting primitives and service defaults; FrontComposer supplies shell and identity helpers; Memories supplies publication and search-client primitives. Domain writes never bypass EventStore. Infrastructure dependencies in the domain require a documented boundary exception.

Platform-alignment closure removes the local ServiceDefaults project and obsolete shared helper copies where compatible shared APIs exist. Reuse shared admission and subscription mapping rather than local plumbing. Preserve publication identity, retryability, redaction, tenant routing, and search-client contracts. CLI/MCP bearer transport rejects non-HTTPS, non-loopback destinations before token emission.

Record AppHost composition, service-default adoption, query-handler conformance, and reserved-tenant semantics as explicit ADRs. ADRs cannot manufacture runtime evidence or rewrite lifecycle history. Cross-repository verification records exact pins, composition, commands, results, persisted-state assertions, restart boundaries, and sanitized diagnostics without mock fallback. Final closure includes Debug/project-reference and Release/NuGet builds, generated v2 contracts, package boundaries, focused tests, full E2E, accessibility, governance, and documentation consistency.

## UX & Interaction Patterns

Keep the operations console read-only, metadata-only, and FrontComposer-hosted. Reuse shared shell, user context, OIDC, token relay, and Fluent UI primitives. State labels and accessibility names share the canonical API/SDK/CLI/MCP vocabulary. Distinguish denied, redacted, inaccessible, unknown, stale, and failed states without relying on color alone. Preserve blocking WCAG 2.2 AA, responsive, and 125/150/200-percent zoom coverage; introduce no mutation, content, credential-reveal, or repair paths.

## Cross-Story Dependencies

Baseline and prerequisite inventories precede consolidation and adoption. Story 11.2 gates shared-platform adoption; local implementations remain until compatible pinned APIs and equivalent behavior are established. Stories 11.3–11.7 consolidate local code; Stories 11.17–11.18 separately own gateway doubles and provider/repository fakes. Story 11.16 owns brittle-gate replacement.

Cleanup follows the applicable adoption stories; Story 11.20 follows service-default deletion and cleanup. Final verification follows Stories 11.1–11.20. Admission/subscription adoption, Memories seam adoption, and the DCP verification lane do not transfer product-projection or durable-egress ownership. Epic 13 remains a separate security and operations workstream.
