# Epic 11 Context: Domain-Focus Platform Refactoring And Governance Closure

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Epic 11 is a technical enabling workstream, not new product scope. Maintainers remove Folders-local copies of shared Hexalith capabilities, consume Commons, EventStore, FrontComposer, and Memories primitives, delete the local ServiceDefaults project, and keep REST, SDK, CLI, MCP, worker, and UI behavior equivalent through lockstep governance and verification. Folders keeps folder-specific policy, aggregates, provider ports, projections, and audit semantics. This workstream owns no Epic 4, 6, or 10 product projection and does not count toward product-capability completion.

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

Preserve current tenant-isolation, metadata-only audit, parity, observability, accessibility, traceability, and maintainability conformance. Do not add product capabilities or claim product-MVP completion.

Wire contracts stay equivalent unless the matching contract, fixture, docs, and tests change together: routes, OpenAPI schemas, envelopes, ProblemDetails, status codes, parity-oracle expectations, CLI/MCP/SDK semantics, worker publication and search-client behavior, and the read-only console boundary.

Cross-tenant access is denied before any file, workspace, credential, repository, lock, commit, provider, audit, or cache observation. Events, logs, traces, metrics, projections, audit, ProblemDetails, provider diagnostics, generated artifacts, and UI responses stay metadata-only — no file contents, diffs, tokens, credential material, secrets, or unauthorized existence.

Capture restore/build, focused tests, format checks, scaffold/contract/governance evidence, package inventories, route tables, workflow pins, and known blockers before substantive edits. Do not revert or hide unrelated submodule pointer changes.

Shared-platform adoption is gated by a pin inventory. Each required Commons, EventStore, FrontComposer, and Memories capability needs an owning repository, upstream reference, required release or SHA, availability status, consuming Folders story, and verification evidence. Recording pins does not implement upstream code or change dependency pins without separate authorization.

Generated client and contract-derived artifacts stay generated. Do not hand-edit generated output or parity rows to hide drift. Newtonsoft may leave packable client surfaces only when System.Text.Json generation preserves canonical ProblemDetails parsing and idempotency hash regression vectors.

Test counts change only when deleted local implementations no longer need local re-testing. Shared helpers and fakes live in `Hexalith.Folders.Testing`. Gateway doubles and provider/repository fakes are not production evidence and cannot satisfy real-path product acceptance.

Governance and CI gates must keep rejecting unsafe behavioral counterexamples, use generated denominators where applicable, preserve named approvals and blocking full-lane coverage, and must not hard-code stale story or test counts. Do not remove, narrow, skip, or fail-open a gate.

A DCP or live-sidecar lane records exact pins, composition, commands, results, persisted-state assertions, restart boundaries, and sanitized diagnostics. Silent mock or unavailable-seam fallback is forbidden. A blocked or degraded lane is an honest blocker, not completion evidence for Epics 4, 6, 10, or 12.

Final verification records exact commands and results for boundaries, packages, generated contracts, focused tests, full E2E, WCAG 2.2 AA, governance, workflow-conformance, and docs. Failures and unavailable external lanes stay explicit. This enabling pass is not product-capability completion.

## Technical Decisions

Folders is a bounded context on the Hexalith platform. Tenants remains authoritative for tenant facts. EventStore owns platform command, aggregate, event, projection, cursor, read-model, admission, and subscription-mapping mechanics. Commons owns shared helpers such as secrets, hashing, URLs, correlation, and service defaults. FrontComposer owns shell, user-context, token, and OIDC helpers. Memories owns search-index publication and query-client primitives. Folders owns folder ACLs, provider-binding references, workspace/file/commit facts, provider ports, and folder-specific policy. Product projections stay with Epics 4, 6, and 10; durable source events, content, Git persistence, and recoverable egress stay with Epic 12.

Closure means no local `Hexalith.Folders.ServiceDefaults`; no local copies of shared TenantAccess, cursor, read-model, telemetry, secret, hash, URL, correlation, or secret-store helpers where shared APIs exist; no hand-rolled Dapr subscription mapping where EventStore mapping exists; and no UI shell, auth, or token duplication where FrontComposer provides the primitive.

Domain adoption moves those platform-owned helpers onto shared abstractions. `Dapr.Client` and `Octokit` leave the core domain package unless an ADR records the exception. Folders-specific readiness checks move into the owning host or are deleted with ServiceDefaults; probe paths update in code, docs, tests, and deploy manifests together.

Server consolidation shares transport helpers, envelope parsing, header/query readers, canonical-ID validation, result mapping, and one secret-filter detector without changing REST behavior. Server and Workers consume pinned EventStore admission (`IDomainServiceAdmissionStage` or the approved equivalent) and `MapEventStoreDomainEvents` or its pinned equivalent. Memories publication and search-client seams replace obsolete local wrappers only when publication identity, retryability, redaction, tenant routing, and query-client behavior stay contract-compatible.

CLI and MCP share adapter-core plumbing. Bearer tokens are not emitted to non-HTTPS non-loopback endpoints.

Provider adapters stay behind narrow ports. This workstream consolidates duplicated helper logic only; provider feature or correctness work, search optimization, and reserved-tenant decisions stay with their product or ADR owners.

ADRs record AppHost/Aspire composition, ServiceDefaults deletion and adoption, query-handler conformance, and reserved-tenant semantics. An ADR does not manufacture implementation evidence, rewrite lifecycle history, or transfer product-projection ownership.

## UX & Interaction Patterns

The operations console stays read-only, metadata-only, and FrontComposer-hosted, with `FrontComposerShell` as the Blazor Interactive Server layout. Reuse FrontComposer user-context, token relay, OIDC and test-auth helpers, loading, safe copy, banners, and icons where those primitives exist.

Production pages use Fluent UI Blazor, including `FluentDataGrid` and approved Fluent inputs and buttons. Do not introduce raw interactive HTML controls, undefined shell classes, or a second component library. State labels, icons, tooltips, and accessibility names come from the same canonical vocabulary used by API, SDK, CLI, and MCP.

Redacted, inaccessible, denied, unknown, missing, unavailable, stale, and failed states stay visually and semantically distinct. Status must not rely on color alone. Full E2E and WCAG 2.2 AA lanes stay blocking, including responsive layout and 125/150/200-percent zoom. Adoption must not add mutation, file-content, credential, or secret paths.

## Cross-Story Dependencies

Story 11.2 unlocks Stories 11.8, 11.9, 11.10, 11.11, 11.12, and 11.14. Those stories must not delete local implementations until the matching shared APIs are pinned and behavior-equivalent.

Stories 11.3 through 11.7 reduce local duplication after the baseline. Story 11.16 owns brittle-gate replacement. Stories 11.17 and 11.18 own EventStore gateway doubles and provider/repository fakes; Story 11.7 does not.

Story 11.10 owns EventStore admission and subscription-mapping seams only. Story 11.14 owns Memories publication and search-client seams. Story 11.15 owns the DCP-capable verification lane. None of these own transition-evidence, diagnostic, or search-bridge projections, and none may claim FR58 complete.

Story 11.11 adopts identity and shell helpers. Story 11.19 adopts Fluent visual primitives below the shell.

Story 11.13 deletes obsolete local code after the applicable adoption stories and synchronizes maintenance and planning references without rewriting completed evidence. Story 11.20 records the ADRs. Story 11.21 closes the workstream after Stories 11.1 through 11.20.

Epic 13 is a separate security and operations hardening workstream and must not duplicate this epic's platform-seam or adapter-consolidation work.
