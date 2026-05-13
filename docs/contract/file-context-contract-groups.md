# File Mutation And Context Query Contract Groups

Status: Story 1.9 contract-only authoring note.

This note records how the Story 1.9 Contract Spine additions must be reused by downstream stories. The OpenAPI document remains the source of truth; this file is only a small map for future implementation owners.

## Reuse Points

- Story 1.10 must reuse the file mutation `operationId`, task, workspace, path metadata, retry eligibility, unknown outcome, and reconciliation vocabulary when commit and workspace status contracts are authored.
- Story 1.11 must reuse the metadata-only audit keys and safe-denial behavior for audit timeline contracts.
- Epic 4 owns runtime file mutation behavior, prepared-workspace checks, held-lock enforcement, path policy execution, context-query execution, provider/Git/filesystem side effects, and reconciliation.
- Epic 5 owns SDK, CLI, and MCP behavioral parity over these operation groups.
- Story 6.6 must reuse the same metadata-only context-query and workspace diagnostic labels in the read-only operations console.

## Deferred Owners

- Runtime file mutation behavior: Epic 4.
- Path policy enforcement and authorization-before-observation execution: Epic 4.
- Context-query tree, metadata, search, glob, and range-read execution: Epic 4.
- Semantic indexing and RAG retrieval integration: downstream Memories integration work; this story reserves only extension-safe vocabulary and does not implement semantic indexing.
- Commit/status contracts: Story 1.10.
- Audit timeline contracts: Story 1.11.
- Operations-console projections: Story 6.6 and adjacent Epic 6 stories.
- Generated SDK helpers and NSwag wiring: Story 1.12.
- Final parity oracle rows and CI gates: Story 1.13 and later release-readiness stories.

## Reference-Pending Decisions

- C4 input limits are copied from `docs/exit-criteria/c4-input-limits.md` as proposed workshop values with PM approval still pending.
- Story 1.6 foundation vocabulary and Story 1.8 workspace/lock references are reused rather than duplicated.
- C6 state metadata is referenced from `docs/exit-criteria/c6-transition-matrix-mapping.md`; aggregate implementation is deferred.

## Negative Scope

This is contract-only work. It does not add REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git or filesystem side effects, generated SDK output, NSwag generation wiring, CLI commands, MCP tools, workers, UI pages, final parity rows, CI gates, repair automation, or nested-submodule initialization.
