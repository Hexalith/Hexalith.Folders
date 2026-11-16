# Epic 11 Context: Domain-Focus Platform Alignment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Remove Folders-local copies of shared platform capabilities by adopting Commons, EventStore, FrontComposer, and Memories primitives while preserving supported contracts and behavior. This technical enabler does not add product scope or establish MVP completion. Durable source state and product projections remain with Epics 12, 4, 6, and 10.

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

Preserve tenant isolation, metadata-only evidence, contract parity, observability, accessibility, traceability, and maintainability. Authentication and the EventStore envelope supply authoritative tenant context; payload tenant IDs are comparison inputs. Authenticate before checking authority availability and permission; authorize before protected-resource lookup or observation. Denials cannot disclose existence. Reauthorize before asynchronous effects.

Preserve provider behavior, deterministic identities, failure categories, cancellation, and C9 redaction. Events, logs, traces, metrics, diagnostics, error responses, and evidence exclude bodies, diffs, credentials, and secrets. Provider correctness, search optimization, and reserved-tenant decisions retain separate owners.

Capture baseline builds, focused tests, formatting, inventories, governance pins, and blockers before edits. Preserve unrelated changes and evidence. Platform prerequisites need ownership, upstream references, compatible releases or full SHAs, availability, consumers, and verification. Inventory work authorizes no dependency changes or upstream implementation.

McpCli is the sole target Hexalith-owned CLI/MCP surface. Its heads dispatch decorated Folders Contracts through the EventStore Gateway and do not wrap the SDK or REST API. Proprietary Folders adapters and their shared core are frozen compatibility surfaces receiving safety and continuity fixes only. Their component/parity evidence cannot satisfy current-release CLI/MCP acceptance. Enrollment and retirement retain owner-approved inventory and replacement/withdrawal gates. Preserve C13 lifecycle, authorization, idempotency, status, error, and audit semantics.

Governance gates reject unsafe counterexamples, retain fresh named approvals and full-lane coverage, and consume generated inventories. Never weaken gates. Doubles provide component evidence only; provider, durable-state, and deployed-path acceptance require real evidence. External blockers remain explicit.

## Technical Decisions

Folders owns domain policy, aggregates, provider ports, authorization semantics, and product projections. Tenants owns tenant authority; EventStore owns durable events, admission, subscriptions, and domain-service mechanics; Commons owns cross-cutting primitives and service defaults; FrontComposer owns shell and identity helpers; Memories owns publication and search-client primitives. Domain writes use EventStore. Infrastructure dependencies in the domain require a documented exception.

Adopt compatible shared APIs before deleting local copies. Alignment closure removes local ServiceDefaults, duplicated platform helpers, subscription plumbing, and shell/auth/token mechanics where adopted APIs exist. Preserve publication identity, retryability, redaction, routing, and query contracts. Legacy bearer transport permits HTTPS and HTTP on exact case-insensitive localhost or literal IP loopback addresses, rejecting other destinations before token emission; McpCli independently owns its transport safety. No further legacy adapter-core development is planned.

Generate clients, fixtures, and parity inventories from canonical contracts rather than hand-editing generated artifacts. SDK modernization follows accepted v2 authorization, preserving ProblemDetails and idempotency regression vectors without a historical-v1 fallback. Record boundary decisions as ADRs; ADRs cannot create runtime evidence, rewrite lifecycle history, or transfer projection ownership.

## UX & Interaction Patterns

Keep the console read-only, metadata-only, and FrontComposer-hosted with shared identity/shell/OIDC/token and Fluent primitives. Preserve canonical accessible status vocabulary and distinct denial, redaction, inaccessible, unknown, stale, and failure states. Retain blocking WCAG 2.2 AA, responsive, and 125/150/200-percent zoom coverage. Add no mutation, content, credential-reveal, or repair paths.

## Cross-Story Dependencies

The planning manifest controls lifecycle, execution ranks, prerequisite edges, and execution authorization. Frozen build-spec status describes that bounded workflow result, not whole-story closure. Preserve DEC-A8-HOLD and v2 authorization prerequisites where recorded; completed legacy safety work does not release them. Story 11.6 closure still requires Story 1.17; Story 5.15 owns legacy adapter/shared-core retirement.

Story 11.2 gates platform consumption; cleanup follows adoption. Gateway doubles and provider/repository fakes retain separate stories. ADRs follow service-default deletion and cleanup. Final verification requires the DCP lane, exact pins/composition/commands/results, persisted-state/restart assertions, Debug/project-reference and Release/NuGet builds, generated v2 contracts, package boundaries, focused tests, full E2E/accessibility, governance, and consistent docs. Seam adoption cannot close product projections.
