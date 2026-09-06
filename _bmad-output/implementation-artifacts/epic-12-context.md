# Epic 12 Context: Durable Repository-Backed Round Trip

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Authorized developers and AI agents persist folder lifecycle and file content across process restart, read that content from one authoritative store, complete a provider-confirmed Git commit, observe terminal task and projection state, and recover asynchronous indexing delivery. This epic is the product data plane the control-plane shell was built around; adapter, authorization, and fail-safe foundations are not MVP completion. NoOp, unavailable, in-memory, seed-only, or fake-backed substitutions cannot prove it.

## Stories

- Story 12.1: EventStore-backed folder repository, retire NoOp, and implement projection replay
- Story 12.2: Durable projections and task-completion pipeline
- Story 12.3: Durable workspace file-content store and content-read source
- Story 12.4: Real Git commit executor and provider write path
- Story 12.5: At-least-once Memories egress and reconciler
- Story 12.6: Implement durable all-mutations idempotency and expired-key precedence

## Requirements & Constraints

- Persist folder and organization state through a real EventStore path so accepted lifecycle operations survive restart and Production boots with a real repository registration. `/project` must consume ordered events rather than refuse replay.
- Empty-checkpoint replay, host restart, append conflict, equivalent or conflicting idempotency, wrong-tenant or authorization denial, corrupt or unavailable store, timeout, and event-version boundaries must produce safe canonical behavior. Content never appears in events, audit, or telemetry.
- Lifecycle, lock, workspace, cleanup, task, and commit-status projections plus terminal task completion must persist across restart and duplicate delivery, with inspectable freshness, retry eligibility, and failure or recovery evidence. Transition-evidence, operations-console diagnostics, and search-bridge projections remain owned elsewhere.
- Store staged and committed file content durably outside EventStore. Context queries use this store, not a derived index. Verify server-side hashes and byte or media metadata; enforce task, lock, and version identity; make deleted content unavailable.
- File mutations require a prepared, freshly authorized, locked task workspace and do not auto-commit. Inline payloads are allowed only up to 256 KB; larger content must stream. Traversal, symlink, case, encoding, binary, oversize, and path-policy violations reject safely. While lifecycle is staged, dirty, unknown-provider, or reconciliation-required, live body and range reads are visible only to the originating lock-owning task; other authorized actors receive metadata-only status.
- A valid locked workspace may produce at most one successful provider-confirmed commit to the bound GitHub or Forgejo remote and ref. Unconfirmed provider results enter `unknown_provider_outcome` (at most five read-only checks within 15 minutes), then `reconciliation_required` if exhausted or conflicting—never a blind duplicate commit. Known retryable failure with no remote side effect stays dirty and locked; known non-retryable failure is failed or inaccessible with a revoked lock.
- Indexing outages must not roll back committed Folders truth. Deliver mutation and commit events at-least-once with ordered, recoverable egress and stable identity so duplicate delivery does not duplicate logical index units. Folders remains the hydration authority; Memories is a derived index.
- Every mutating Contract Spine operation uses durable tenant-scoped idempotency. Live equivalent intent returns one logical result; live different intent conflicts; every expired key returns `idempotency_key_expired` before protected work, regardless of intent; reads reject idempotency keys. Replay always revalidates current authorization. Mutation replay results last 24 hours; commit replay results last seven years; consumed-key tombstones last tenant lifetime plus 400 days and must not retain protected prior intent.
- Authorization and tenant isolation run before any folder, content, provider, commit, or index disclosure. Temporary working files delete seven days after task-terminal closure with no active task; dirty, unknown-provider, and reconciliation-required workspaces are not cleanup-eligible.
- Completion requires deployed restart, replay, denial, conflict, timeout, and retention evidence. Safe-empty and fail-closed paths remain mandatory outage behavior, not capability proof.

## Technical Decisions

- Hexalith.EventStore is the only write-side. Public REST reaches the EventStore gateway, then processor, authorization gate, and repository. Folder and Organization aggregates emit metadata-only events; snapshots may occur every 50 events. Provider, Git, and working-copy side effects belong in Workers process managers that subscribe to events and submit follow-up commands.
- Locks, idempotency admission (`reserved`, `pending`, `recoverable`, `unknown_provider_outcome`, `terminal`, `expired` plus a fencing token), in-flight checkpoints, and reconciliation tasks are durable and fail closed. Idempotency is not a rebuildable projection: replay must not create, erase, or resurrect consumed-key authority.
- Working copies under `/var/lib/hexalith-folders/work/{tenantId}/{folderId}/{taskId}` are disposable caches. Durable file content lives in the approved content store; Git checkouts rebuild from that store plus the provider.
- REST file transport is bimodal (`PutFileInline` JSON ≤256 KB, `PutFileStream` multipart). Base64 is rejected. The generated SDK exposes both operations plus a length-based `UploadFileAsync` helper.
- The Git write path composes production-registered GitHub and Forgejo mutation, commit, and status adapters and wires the provisioning process manager. It must not reimplement provider transports or ship fake or `NotImplementedException` executors. Exactly one eligible provider mutation occurs per accepted commit.
- Memories publication is commit-then-append through a durable outbox, checkpoint, or equivalent, using stable CloudEvent and idempotency identity toward `folders-index`. Epic 12 owns recoverable egress, not the search-bridge projection.
- Serializing identity is managed tenant plus canonical provider/repository identity plus normalized target ref. Authoritative tenant comes from authentication and the EventStore envelope, never from payload.
- Record lock and revocation timing, file-policy vocabulary, the authorization matrix, and the GitHub/Forgejo compatibility catalog before the durable boundary ships.

## UX & Interaction Patterns

The operations console stays read-only and metadata-only: no file contents, diffs, edits, or repair actions. After this epic, trust and status views must show real lifecycle, lock, commit reference, freshness, retry eligibility, and terminal or recovery state—not seed or placeholder emptiness. Canonical labels distinguish ready, locked, dirty, committed, failed, inaccessible, unknown-provider, and reconciliation-required, plus lock states unlocked, locked, expired, stale, and revoked; color is never the only cue. Redacted, missing, unknown, and unavailable remain visually distinct.

## Cross-Story Dependencies

- Record the lock-timing, file-policy, authorization-matrix, and provider-compatibility decisions, then Story 12.1. Stories 12.2 and 12.3 consume 12.1 streams. Story 12.4 needs 12.1–12.3 plus the production-registered provider adapters. Story 12.5 needs 12.1–12.3 and the configured Memories AppHost route. Story 12.6 needs 12.1 and EventStore admission/retention design.
- Epics 4, 6, and 10 own consuming projections (transition evidence, diagnostics, search bridge) and cannot close production proof until this substrate exists. Platform seam adoption does not own those projections.
- Epic 12 does not complete indexed search; it supplies durable source events, authoritative content and state, and recoverable egress for that work.
