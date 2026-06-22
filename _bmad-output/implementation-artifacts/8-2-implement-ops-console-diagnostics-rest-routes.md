# Story 8.2: Implement the 7 ops-console diagnostics REST server routes (Bucket B)

Status: backlog

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->

## Story

As an operator,
I want the ops-console diagnostics operations to have working REST server routes,
So that the read-only operations console and any REST consumer can retrieve diagnostics evidence instead of hitting unimplemented endpoints.

## Context

Verified 2026-06-22 (adversarial parity workflow): the 7 diagnostics operations are present on **SDK + MCP only**. CLI absence is **intentional** (diagnostics is MCP-only by design — do NOT add CLI commands). The gap is the **REST server route** (and the console consumes REST). The spine declares all 7. This story closes **Bucket B**; with Story 8.1 it brings REST to **47/47**.

## Operations to implement (all FR52; spine line refs verified)

| operationId | Method / Path | Spine line |
|---|---|---|
| `GetReadinessDiagnostics` | GET `/api/v1/ops-console/readiness-diagnostics` | 4304 |
| `GetLockDiagnostics` | GET `.../ops-console/lock-diagnostics` | 4390 |
| `GetDirtyStateDiagnostics` | GET `.../ops-console/dirty-state-diagnostics` | 4483 |
| `GetFailedOperationDiagnostics` | GET `.../ops-console/failed-operation-diagnostics` | 4577 |
| `GetProviderStatusDiagnostics` | GET `.../ops-console/provider-status-diagnostics` | 4674 |
| `GetSyncStatusDiagnostics` | GET `.../ops-console/sync-status-diagnostics` | 4768 |
| `GetProjectionFreshness` | GET `/api/v1/ops-console/projection-freshness` | 4864 |

## Acceptance Criteria

1. **Given** the spine declares the 7 diagnostics operations and the SDK + MCP wrap them, **when** REST server routes are added under `Hexalith.Folders.Server`, **then** all 7 respond on REST with metadata-only, read-only, projection-backed responses matching the spine.
2. **Given** diagnostics are read-only ops-console reads, **when** invoked, **then** they enforce authorization-before-observation, fail closed on stale projection/tenant-access revocation, and never expose secrets, raw provider payloads, or unauthorized-resource existence.
3. **Given** CLI is intentionally diagnostics-free, **when** the routes land, **then** no CLI command is added (CLI stays 40/47 by design) and the parity oracle reflects the MCP-only family rule.
4. **Given** Story 8.1 closed Bucket A, **when** Bucket B lands, **then** REST coverage reaches **47/47** and the parity oracle + contract-spine drift gate pass.

## References

- Verified parity result: workflow `verify-epic5-parity-gap` (2026-06-22).
- MCP diagnostics: `src/Hexalith.Folders.Mcp/Tools/DiagnosticsTools.cs`.
- SDK: `src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs`.
- Sprint Change Proposal: `../planning-artifacts/sprint-change-proposal-2026-06-22.md`.
