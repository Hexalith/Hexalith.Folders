# Epic 10 Context: Authorized Metadata Search and Index Lifecycle

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Deliver authorized metadata-token recall and indexing status through Memories while Folders remains authoritative for identity, lifecycle, and access. Completion requires a non-empty deployed mutation-to-index-to-search round trip followed by removal/archive pruning, without sensitive-data or hidden-resource disclosure. Safe-empty and unavailable responses are required failure behavior, not successful product evidence. Body-content indexing and recall remain excluded from the current release.

## Stories

- Story 10.1: Define the Worker-Side Search-Index Publication Port
- Story 10.2: Build the Folders-owned indexing bridge projection
- Story 10.3: Author authorized asynchronous indexing on file-write and commit
- Story 10.4: Emit SearchIndexEntryRemoved on removal/archive and prove end-to-end routing
- Story 10.5: Expose an authorized Folders query facade over Memories
- Story 10.6: Replace the fail-closed content materializer with a metadata-derived materializer under C4/C9
- Story 10.7: EventStore-backed search bridge and deployed Server registration
- Story 10.8: Real produce/index/authorize/hydrate/redact/search round trip
- Story 10.9: Preserve Metadata-Only Indexing Until C9 Body-Content Approval

## Requirements & Constraints

- Serve equivalent authorized search and indexing-status behavior through REST, SDK, CLI, and MCP. Expose only approved metadata tokens, opaque authorized identity, freshness, and indexing/status evidence.
- Check fresh tenant/folder/workspace authority, ACL, path policy, sensitivity, and size/type limits before publication or search observation. Authorization precedes lookup, counting, suggestions, filters, empty-state classification, and response shaping. Unavailable/stale authority fails closed; denials cannot disclose existence.
- Treat candidates as untrusted and stale. Hydrate from current Folders authority and discard wrong-tenant, revoked, unauthorized, stale, removed, archived, conflict/corrupt, or non-live entries.
- Build bounded index text/attributes only from approved mutation metadata: type/size classification, media type, folder/organization identity, and path-policy outcome. Reject or remove bodies, snippets, raw paths, source URIs, diffs, prompts, provider payloads, and credentials before egress. Events, projections, audit, telemetry, diagnostics, errors, and console responses remain metadata-only under C9.
- Mechanically reject attempts to enable body materialization, embeddings, indexed body recall, or content-derived snippets with a stable policy/configuration error before content read or publication. Flags and tenant settings cannot bypass the guard. Future body scope needs a separately approved requirement, story, rank, dependency path, C9 policy, and release evidence.
- Keep indexed metadata recall separate from bounded direct text search/snippets in an authorized live workspace. Experimental RAG ingestion is outside this capability.
- Use replay-stable identities and idempotency: duplicate delivery cannot duplicate visible live units. Removal/archive/tombstones prune entries. Index/pub-sub outages never roll back durable file operations; expose retryable failure, stale/unavailable state, or reconciliation needs.
- Completion evidence must cover deployed non-empty results, isolation/redaction/policy negatives, removal/archive, failure/timeout, restart, and empty-checkpoint replay. Mocks, seeds, NoOp, in-memory, unavailable, or safe-empty behavior alone are insufficient.

## Technical Decisions

- Memories is a derived syntactic/BM25 index, never Folders authority. Workers publish `SearchIndexEntryChanged`/`SearchIndexEntryRemoved` CloudEvents through `pubsub` / `memories-events`, source `hexalith-folders`, routed to `folders-index`, with stable event IDs/idempotency keys. Never use RAG `IngestAsync`.
- Confine write-side Memories contracts dependencies to Workers. Server may use the typed search client behind a Memories-free core port. Contracts, core, SDK, CLI, MCP, UI, and Testing take no direct Memories dependency.
- Read through Dapr invocation of `memories` at `GET /api/search`; Workers retain pub/sub-only egress. Production uses deny-by-default access control and mTLS, with forbidden app/method/topic negative tests.
- Managed tenants share physical `folders-index`. Attribute filters are defense in depth; Folders authorization and authoritative hydration enforce isolation. Drop index snippets and never expose backend source URIs or memory-unit identifiers.
- Use an EventStore-backed version-to-entry/status bridge. Lifecycle projection and egress outcomes must preserve invalidation ordering so late acknowledgements cannot revive stale entries. Require deployed registration, durable recovery, and deterministic replay.

## UX & Interaction Patterns

Show read-only workspace indexing status with an as-of timestamp; distinguish indexed, not yet indexed, removed, and archived-but-retained. Label unavailable status/search explicitly rather than rendering no matches. Security-trim and hydrate before display; add no content previews or hidden-existence clues.

## Cross-Story Dependencies

- Stories 10.1–10.5 are component increments. Story 10.6 supplies metadata documents, 10.7 the deployed bridge, and 10.8 product completion evidence. Story 10.9 supplies the negative metadata-only guard, not a dormant positive capability or body-indexing forward dependency.
- Epic 9 supplies topology/routing. Epic 12 supplies durable mutation events, authoritative hydration, and recoverable at-least-once egress/reconciliation. Deployed verification also requires bounded context policy and a DCP-capable lane; unresolved prerequisites need lower execution ranks.
- Workstream 11 may supply shared platform seams and the DCP lane, but owns no search projection and cannot substitute for product evidence.
