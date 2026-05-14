# Commit And Workspace Status Contract Groups

Story 1.10 adds only Contract Spine declarations for commit acceptance, commit evidence, workspace status, task status, provider outcome, retry eligibility, retry-after metadata, and reconciliation status.

The canonical machine-readable source remains `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`. These notes record reuse requirements, deferred owners, reference-pending decisions, and negative scope only; they do not define runtime behavior.

## Operation Mapping

| Operation | Scope | Idempotency or freshness | Notes |
|---|---|---|---|
| `CommitWorkspace` | folder, workspace, task, operation, branch/ref target reference, changed-path metadata digest | `Idempotency-Key`; commit TTL tier; equivalence fields `author_metadata_reference`, `branch_ref_target`, `changed_path_metadata_digest`, `commit_message_classification`, `operation_id`, `task_id`, `workspace_id` | `tenant_id` remains envelope-derived per `docs/contract/idempotency-and-parity-rules.md`; the story drafting list included it as a partition key, not a client-controlled OpenAPI equivalence field. |
| `GetWorkspaceStatus` | folder and workspace status | `read_your_writes`; freshness and projection-lag metadata | Accepted command state and projected/read-model state are separate. |
| `GetTaskStatus` | task status | `eventually_consistent`; freshness metadata | Hidden, wrong-tenant, unknown, redacted, and projection-unavailable tasks do not reveal existence. |
| `GetCommitEvidence` | commit operation evidence | `eventually_consistent`; freshness and redaction metadata | Commit reference is classified or redacted; changed paths remain digest-only. |
| `GetProviderOutcome` | provider outcome metadata | `eventually_consistent`; retry eligibility and optional retry-after metadata | Unknown provider outcome never recommends blind commit retry. |
| `GetReconciliationStatus` | reconciliation status | `eventually_consistent`; final-state evidence and retry eligibility | Completed clean and completed dirty states remain distinct. |

## Reuse Requirements

- Story 1.11 must reuse the metadata-only audit keys, safe-denial behavior, provider outcome states, reconciliation states, and redaction semantics when audit and operations-console projection query groups are authored.
- Story 1.12 must reuse the `CommitWorkspace` operation identity, commit-tier idempotency metadata, equivalence field order, read-consistency annotations, and response schemas for generated SDK helpers.
- Story 1.13 must reuse the same operation allow-list and metadata fields when generating final parity oracle rows.
- Epic 4 owns runtime commit behavior, C6 state transitions, lock release side effects, idempotency persistence, unknown-outcome reconciliation, provider workers, and workspace cleanup.
- Epic 5 owns SDK, CLI, and MCP behavioral parity over these contracts.
- Story 6.6 must reuse the workspace status, provider outcome, retry eligibility, and reconciliation labels in the read-only operations console.

## Deferred Owners

- Runtime commit behavior: Epic 4.
- C6 state transitions and aggregate enforcement: Epic 4.
- Lock release side effects and held-lock enforcement: Epic 4.
- Idempotency persistence and commit-tier retention execution: Epic 4.
- Unknown-outcome reconciliation and provider workers: Epic 4.
- Workspace cleanup: Epic 4.
- Audit timeline contracts: Story 1.11.
- Operations-console projections: Story 6.6 and adjacent Epic 6 stories.
- Generated SDK helpers and NSwag wiring: Story 1.12.
- Final parity oracle rows and CI gates: Story 1.13 and later release-readiness stories.

## Reference-Pending Decisions

- Commit tier idempotency retention inherits C3 and remains `TODO(reference-pending)` until Legal and PM approval in `docs/exit-criteria/c3-retention.md`.
- C6 transition policy is consumed from `docs/exit-criteria/c6-transition-matrix-mapping.md`; this story declares states and outcomes but does not implement transitions.
- Story 1.5 names `provider_outcome_unknown` in prose, while the OpenAPI foundation froze the canonical category as `unknown_provider_outcome`; Story 1.10 preserves the frozen OpenAPI category.
- Story 1.8 workspace/lock and Story 1.9 file/context shapes are reused by reference. This story does not duplicate workspace preparation, lock management, file mutation, path policy, or context-query operations.

## Audit Metadata Boundaries

`CommitWorkspace` audit metadata uses `branch_ref_policy_ref` (the policy reference) rather than `branch_ref_target` (the raw target reference). This is deliberate: the policy reference is the less-sensitive view of branch/ref intent, and audit pipelines should not propagate the raw target identifier alongside the operation. The story spec phrase "branch/ref target metadata" in AC2 maps to the policy reference for audit purposes; the target reference itself appears only in the commit request body and the idempotency-equivalence list, where it is required to compute commit-identity equivalence.

## Authorization And Metadata Boundaries

All status and evidence reads follow authorization-before-observation. Tenant access, folder ACL, workspace/task scope, and redaction decisions happen before any status distinction is exposed.

Wrong-tenant, unauthorized, hidden, unknown, redacted, stale, and projection-unavailable cases use safe-denial or read-model-unavailable shapes that do not reveal folder, workspace, task, commit, provider outcome, or reconciliation-record existence.

Commit messages, branch names, repository names, changed paths, provider correlation IDs, author metadata, and commit references are tenant-sensitive metadata. The Contract Spine uses classifications, opaque references, digests, redaction metadata, and bounded strings rather than raw values.

`retryAfter`, retry eligibility, projection age, and freshness watermarks are advisory client metadata only. They are not scheduler behavior, provider retry orchestration, or a guarantee of eventual provider completion.

## Negative Scope

This is contract-only work. It does not add runtime REST handlers, EventStore commands, domain aggregate behavior, provider adapters, Git or filesystem side effects, generated SDK output, NSwag generation wiring, CLI commands, MCP tools, workers, UI pages, final parity rows, CI gates, repair automation, or nested-submodule initialization.
