# Epic 10 Context: Authorized Folders Search And Index Lifecycle

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Developers and AI agents can publish, remove, reconcile, authorize, search, and hydrate tenant-scoped metadata tokens through the Memories search index while Folders remains the authority for identity, lifecycle, and access. The epic must deliver a non-empty deployed round trip that never exposes raw paths, file bodies, snippets, source URIs, credentials, or hidden-resource existence. Safe-empty and unavailable behavior remains required failure handling but is not completion evidence. Indexed recall is not live workspace body search; body-content indexing is a separately approved follow-on.

## Stories

- Story 10.1: Define the worker-side semantic-indexing port and Memories dependency
- Story 10.2: Build the Folders-owned indexing bridge projection
- Story 10.3: Author authorized asynchronous indexing on file-write and commit
- Story 10.4: Emit SearchIndexEntryRemoved on removal/archive and prove end-to-end routing
- Story 10.5: Expose an authorized Folders query facade over Memories
- Story 10.6: Replace the fail-closed content materializer with a metadata-derived materializer under C4/C9
- Story 10.7: EventStore-backed search bridge and deployed Server registration
- Story 10.8: Real produce/index/authorize/hydrate/redact/search round trip
- Story 10.9: Authorized body-content materialization — C9 gated

## Requirements & Constraints

- Expose authorized metadata-token search and indexing status with equivalent authorization, error, and audit outcomes through REST, generated SDK, CLI, and MCP. Results contain only opaque authorized identity, classification/status, freshness, availability, and approved metadata.
- This capability is indexed metadata-token recall, not live workspace context search. Live authorized tree, glob, range, and snippet queries stay a separate family and must not leak snippets or bodies into events, logs, traces, metrics, projections, audit, diagnostics, errors, or the shared index.
- Run authentication and current tenant, organization, folder, workspace, ACL, path, sensitivity, size, and type checks before index publication, search egress, candidate lookup, counting, filtering, suggestions, empty-state classification, hydration, or response shaping. Denials must not reveal whether a hidden resource or index entry exists. Cross-tenant visibility of index results is a zero-tolerance defect.
- Treat the index as stale and security-untrusted. Recover identity from each candidate, hydrate from current Folders authority, then drop wrong-tenant, unauthorized, revoked, stale, removed, archived, conflict/corrupt, or otherwise non-live entries before returning results.
- Materialize only bounded, sensitivity-policy-sanitized mutation tokens for the current release. Allowed evidence includes type/size classification, media type, folder/organization identity, and path-policy outcome. Raw paths, file bodies, snippets, source URIs, secrets, and credential data are forbidden in indexed text, attributes, responses, telemetry, events, projections, audit, diagnostics, and errors. Paths, repository names, branch names, and commit messages default to tenant-sensitive; redacted, hidden, unknown, missing, stale, and unavailable must remain distinct.
- Apply explicit size/type policy outcomes and keep publication replay-stable and idempotent. Duplicate delivery must not duplicate or corrupt index or bridge state. Empty-checkpoint replay must rebuild equivalent bridge state from the same ordered events.
- Removal, archive, and tombstone processing must prune live search results. Memories or pub/sub outages must never roll back durable file operations; they surface as honest retryable, stale, failed, unavailable, or reconciliation-required indexing state.
- Completion requires deployed evidence for a non-empty authorized mutation-to-search flow, exact-once visible live units under at-least-once delivery, restart and empty-checkpoint replay, removal/archive pruning, failure and timeout behavior, and tenant-isolation/redaction boundaries. Mocks, seeds, in-memory stores, NoOp behavior, unavailable defaults, or safe-empty results alone are insufficient.

## Technical Decisions

- Memories is a separate Dapr-enabled derived syntactic/BM25 index, never an authoritative Folders datastore. Experimental RAG ingestion is out of scope.
- Write path: `Hexalith.Folders.Workers` owns a narrow publication port and is the only write-side project that references `Hexalith.Memories.Contracts`. It publishes `SearchIndexEntryChanged` and `SearchIndexEntryRemoved` CloudEvents on `pubsub` / `memories-events`, source `hexalith-folders`, with stable IDs and idempotency keys routed to the shared `folders-index` tenant. Memories upserts by tenant plus aggregate identity. Workers use pub/sub only; they do not service-invoke Memories.
- A Folders-owned durable bridge maps file versions to index entries and records indexed, stale, skipped, failed, tombstoned, and reconciliation-required states. Its deployed implementation is EventStore-backed, deterministic under replay, restart-safe, Server-referenceable, and registered in the search facade instead of the fail-safe unavailable default.
- Read path: a Folders-owned facade. The Server may call Memories search over Dapr service invocation (`GET /api/search` on app-id `memories`) behind a Memories-free core port; it must never ingest. Other public-surface projects reach the facade through the generated Folders SDK and take no direct Memories dependency.
- All managed tenants share the physical `folders-index` Memories tenant. Server-side managed-tenant, folder, and status filters are defense in depth; authoritative Folders-side authorization, identity recovery, bridge hydration, and security trimming are the load-bearing isolation controls.
- Production Dapr communication is deny-by-default with mTLS. The Server receives only the required Memories search invoke permission; Workers retain declared pub/sub publication rights. Negative policy tests must cover forbidden app, method, and topic combinations.
- Indexing is asynchronous after authorized file-write and commit events. Body-content indexing remains unavailable until named Security and Product authorities approve source classes, redaction, retention, deletion, access, and egress policy.

## UX & Interaction Patterns

Preserve existing browse/search, indexing-status, removal, archive, freshness, and governance signals without adding content previews. Empty, denied, redacted, stale, unavailable, and failed states must be explicit and visually distinct, use canonical labels, and never rely on color alone. Search, filters, result selection, and status details must remain keyboard accessible and screen-reader meaningful; redaction must be visible rather than silent truncation.

## Cross-Story Dependencies

- Stories 10.1–10.4 establish the publication contract, bridge state, changed-entry flow, and removal flow. Story 10.6 supplies real metadata documents; Story 10.7 supplies the deployed durable bridge; Story 10.8 combines them with the authorized facade to prove completion.
- Epic 9 supplies the Memories AppHost topology and `hexalith-folders` to `folders-index` routing. Epic 12 supplies durable mutation events, authoritative file/state hydration, and recoverable at-least-once egress/reconciliation needed by the deployed round trip.
- Shared platform seams and a DCP-capable verification lane may come from the platform-refactoring workstream, but that workstream owns no search projection and is not a substitute for Epic 10 product evidence.
- Story 10.9 depends on explicit Security and Product approval and is not required for the current metadata-token round trip.
