# Input Reconciliation — Product Brief: Hexalith.Folders

## Reconciliation Scope

- **Input:** `_bmad-output/planning-artifacts/product-brief-Hexalith.Folders.md`
- **Compared with:** `_bmad-output/planning-artifacts/prd.md` (updated 2026-07-14)
- **Addendum:** none present
- **Method:** extracted product intent, differentiators, MVP promises, success signals, actors, and technical-boundary implications; implementation detail was not copied into this report.

## Overall Assessment

The updated PRD preserves the brief's central promise: a tenant-scoped, task-oriented workspace boundary for AI agents, with governed file operations, GitHub/Forgejo persistence, exclusive task locking, metadata-only audit, cross-tenant isolation, context queries, cross-surface parity, and read-only operational visibility. It materially strengthens authorization freshness, idempotency, durable-commit evidence, failure states, query limits, and release gates.

Three meaningful brief-to-PRD gaps remain. Two historical differences—local storage and incoming webhooks—are intentional supersessions and should not be restored accidentally.

## Meaningful Gaps and Ambiguities

### 1. File-search semantics no longer make one coherent user promise

**Brief intent:** AI agents can search or read relevant file content, and query efficiency is a success signal: agents should use tree, metadata, search, glob, and partial reads instead of loading an entire workspace.

**Current PRD:** The executive summary, endpoint list, glossary, FR34, and MVP feature set still promise `SearchFolderFiles` / file `search`, with a 500-result bound. FR58 separately defines metadata-token recall whose results cannot expose raw paths, file bodies, snippets, or source URIs, and the PRD explicitly excludes full body-content search and recall from the release.

**Assessment:** This is an accidental contract ambiguity, not a clean supersession. It is unclear whether `SearchFolderFiles` searches filenames/paths, file metadata, mutation metadata, or file bodies; what an authorized hit returns; and how this operation differs from FR58. The brief's context-loading efficiency outcome is also no longer measured—only latency and response bounds are measured.

**Disposition needed:** Define the MVP search families and result semantics explicitly. If body search is deferred, state what `SearchFolderFiles` searches and returns, distinguish it from FR58 metadata recall, and retain a measurable outcome showing that bounded context queries let an agent retrieve useful context without full-workspace reads.

### 2. The brief's file-move capability disappeared

**Brief intent:** The task-oriented command surface includes moving files, alongside writing, inspecting, searching, committing, and releasing.

**Current PRD:** File mutations are consistently limited to add, change, and remove. Neither the illustrative operation list nor FR32 defines move/rename behavior.

**Assessment:** This is an accidental capability loss unless move was deliberately removed. Modeling a move as unrelated remove-plus-add changes atomicity, path-policy evaluation, changed-path evidence, failure recovery, and idempotency semantics, so consumers cannot safely infer it.

**Disposition needed:** Either add a governed `MoveFile`/rename capability with cross-surface, authorization, audit, path-policy, and idempotency semantics, or explicitly list move/rename as post-MVP and document add-plus-remove as unsupported for atomic-move expectations.

### 3. The differentiating "many changes, one task commit" invariant is not binding

**Brief intent:** The task transaction is a differentiator: one task acquires a lock, makes many file changes, then commits once with task and correlation metadata.

**Current PRD:** Journeys describe committing a change set, and commit metadata is well specified, but the functional contract does not say whether a task may create exactly one durable commit, multiple commits, or commit again after reaching `committed`. Idempotency prevents duplicate replay of equivalent requests but does not prevent multiple distinct commit requests or keys for one task.

**Assessment:** The qualitative idea survives in prose but is not enforceable as a product invariant. This leaves lock release, task completion, audit interpretation, and cross-surface parity underspecified at the point that originally distinguished the product from generic Git automation.

**Disposition needed:** Decide and state the task-to-commit cardinality. If MVP is one commit per task, make the first provider-confirmed commit terminal and define later commit attempts. If multiple commits are allowed, revise the task-transaction language and define lock/lifecycle/audit behavior for each commit.

## Intentional Supersessions — Do Not Treat as Missing Requirements

### Limited local storage and local-to-Git upgrade

The brief placed limited local storage and promotion to Git-backed storage in MVP. The PRD explicitly narrows MVP to repository-backed create/bind workflows and moves local-first promotion and unmanaged-local migration post-MVP. This is a documented scope decision, not accidental loss.

One minor cleanup remains: the Project Classification still cites both local and Git-backed storage modes as a source of current complexity. If that sentence is meant to describe MVP complexity, it should be aligned with the repository-backed-first scope; if it describes the full product horizon, it is acceptable.

### Incoming provider webhooks

The brief treated webhook differences as part of provider capability modeling and placed webhook handling in workers/process managers. The PRD explicitly excludes incoming webhook ingestion from MVP and requires a future tenant-routing approval before adding it. This is a deliberate security/scope decision, not a reconciliation gap.

### Aggregate and process-manager topology

The brief proposed two aggregates and assigned provisioning, preparation, synchronization, webhooks, and retries to workers/process managers. The PRD deliberately declares product outcomes and delegates mechanism and adapter shape to architecture and canonical contract artifacts. Omitting those topology choices from the PRD is appropriate, provided the architecture retains or consciously replaces them.

## Preserved Brief Intent

- Tenant-scoped AI-agent workspace boundary rather than a generic file manager, Git UI, chatbot UI, or execution sandbox.
- Stable task lifecycle with preparation, exclusive lock, governed mutations, durable Git commit, release/recovery state, and audit.
- GitHub and Forgejo treated through explicit capabilities and provider contract tests rather than assumed API compatibility.
- Organization/tenant-owned credential references, explicit folder authorization, and zero-tolerance cross-tenant isolation.
- Metadata-only events and audit evidence; file bodies and secrets remain outside EventStore and diagnostics.
- API/REST, generated SDK, CLI, and MCP share one contract; the operations console remains read-only and diagnostic.
- Operators can see readiness, lock, dirty/uncommitted, commit, failure, credential-reference, provider, and sync trust signals without opening files.
- Repair UI, end-user chatbot UI, file-editing UI, branch/review UI, and multi-remote mirroring remain outside MVP.
- Long-term direction remains a durable, provider-portable workspace substrate for Hexalith AI agents.
