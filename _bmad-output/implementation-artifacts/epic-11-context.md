# Epic 11 Context: Domain-Focus Platform Refactoring And Governance Closure

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 11 removes local copies of shared Hexalith platform capabilities from Folders while preserving existing REST, SDK, CLI, MCP, worker, and UI behavior. The epic is a technical alignment and governance-closure effort, not new product scope: Folders should retain folder-specific policy, aggregates, provider ports, projections, and audit semantics while consuming Commons, EventStore, FrontComposer, and Memories primitives for platform-owned mechanics.

## Stories

- Story 11.1: Establish refactor baseline and governance pin map
- Story 11.2: Land platform prerequisite APIs in shared modules
- Story 11.3: Apply wire-preserving repo hygiene and fragile-gate fixes
- Story 11.4: Consolidate Server transport, envelope, and route helper duplication
- Story 11.5: Consolidate domain/provider duplication and fix provider correctness defects
- Story 11.6: Consolidate CLI/MCP adapter core and secure bearer transport
- Story 11.7: Consolidate test helpers into Hexalith.Folders.Testing
- Story 11.8: Adopt Commons/EventStore primitives in the Folders domain
- Story 11.9: Delete Hexalith.Folders.ServiceDefaults and consume Commons.ServiceDefaults
- Story 11.10: Align Server and Workers with EventStore/Memories SDK seams
- Story 11.11: Harden FrontComposer and Fluent UI conformance below the shell
- Story 11.12: Modernize the generated client and shared idempotency/ULID helpers
- Story 11.13: Final cleanup, ADRs, documentation, and verification

## Requirements & Constraints

Epic 11 must not change product behavior or public wire contracts. Existing routes, OpenAPI schemas, response envelopes, ProblemDetails categories, status codes, parity-oracle expectations, CLI behavior, MCP failure behavior, SDK semantics, worker indexing behavior, and read-only console boundaries must remain equivalent unless a story explicitly updates the corresponding contract, fixture, documentation, and tests in lockstep.

The refactor supports existing NFRs for tenant isolation, metadata-only audit, cross-surface parity, observability, accessibility, traceability, and maintainability. Cross-tenant access must still be denied before file, workspace, credential, repository, lock, commit, provider, audit, or cache access. Events, logs, traces, metrics, projections, audit records, ProblemDetails, provider diagnostics, generated artifacts, and UI responses must remain metadata-only and must not expose file contents, diffs, provider tokens, credential material, secrets, or unauthorized resource existence.

Baseline evidence is required before substantive edits. Restore/build, focused test lanes, format checks, scaffold/contract/governance gates, package inventories, route tables, workflow pins, known blockers, and unrelated submodule pointer changes need to be captured so later simplification is judged against known behavior instead of optimistic assumptions.

Shared-platform adoption is gated by pinned upstream prerequisites. Commons, EventStore, FrontComposer, and Memories APIs must exist or be deliberately added before Folders deletes local behavior. Submodule bumps are explicit dependency changes and must use conventional `chore(deps):` commit messages.

Generated and contract-derived artifacts remain generated. Do not hand-edit generated client output or parity rows to mask drift; update the OpenAPI spine or generation inputs and regenerate. Newtonsoft removal from packable client surfaces is only acceptable when System.Text.Json generation preserves canonical ProblemDetails parsing and idempotency regression vectors.

## Technical Decisions

Folders is a bounded context over the Hexalith platform. Tenants remains authoritative for tenant facts; EventStore owns command, aggregate, event, projection, cursor, read-model, and domain-service mechanics where those mechanics are platform-owned; Commons owns shared helpers such as secret, hash, URL, correlation, and service-default primitives; FrontComposer owns shell/UI shared behavior; Memories owns search-index publication and query primitives. Folders owns folder ACLs, provider binding references, workspace state, file-operation facts, commit metadata, provider ports, operational projections, and folder-specific policy.

The target closure state has no local `Hexalith.Folders.ServiceDefaults`, no local copies of shared TenantAccess, cursor, read-model, telemetry, secret, hash, URL, correlation, or secret-store helpers where shared APIs exist, no hand-rolled Dapr subscription mapping where EventStore SDK mapping exists, and no UI shell/auth/token duplication where FrontComposer provides primitives.

Server consolidation should deduplicate transport helpers, envelope parsing, header/query readers, canonical ID validation, result mapping, and secret filtering without changing external REST behavior. Server and Workers should consume EventStore domain-service seams and Memories publication/search wrappers where available; local request handlers, route mapping, and indexing egress plumbing are removable only when REST parity and worker search-index behavior remain unchanged.

Domain and provider consolidation should centralize repeated Folders-local helper logic before moving platform-owned behavior out to shared modules. Provider adapters remain behind narrow ports and capability tests. Forgejo correctness must not be treated as a GitHub base-URL swap, and bearer-token transport must refuse non-HTTPS non-loopback endpoints before emitting credentials.

Test-helper consolidation belongs in `Hexalith.Folders.Testing` when helpers are shared across projects. Test counts should change only when deleted local implementations no longer need local re-testing, not because coverage was silently dropped. Verification remains focused on restore/build, unit, contract, parity, safety, governance, generated-output, and relevant UI/worker lanes.

ADRs are expected for final boundary decisions, including AppHost/Aspire exception handling, ServiceDefaults deletion, query-handler conformance, and reserved-tenant semantics.

## UX & Interaction Patterns

UI hardening must keep the operations console read-only, metadata-only, and FrontComposer-hosted. The UI should reuse FrontComposer Shell services and components for user context, token relay, OIDC/test helpers, loading states, safe copy, banners, icons, and shared shell behavior where such primitives exist.

Production pages should use Fluent UI Blazor primitives, including FluentDataGrid and Fluent input/button components, instead of raw interactive HTML controls, undefined `fc-*` hooks, or local component-library patterns. State labels, icons, tooltips, and accessibility labels should derive from canonical state vocabulary shared by API, SDK, CLI, and MCP surfaces.

Redacted, inaccessible, denied, unknown, missing, unavailable, stale, and failed states must remain visually and semantically distinct. Accessibility and incident-response UX require redaction to be distinguishable from absent or unknown data, and status must not rely on color alone.

## Cross-Story Dependencies

Story 11.2 unlocks later platform adoption stories by making or confirming the shared APIs that Folders will consume. Stories 11.8, 11.9, 11.10, 11.11, and 11.12 should not delete local implementations until their shared-module prerequisites are pinned and behavior-equivalent.

Stories 11.3 through 11.7 reduce local duplication and fragile gates before larger package-boundary changes. Story 11.13 depends on Stories 11.1-11.12 and closes the epic by deleting obsolete local code, synchronizing planning and project-context artifacts, recording ADRs, and running the final verification checklist or documenting blockers with evidence.
