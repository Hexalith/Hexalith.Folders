# Story 8.3: Wire-exercise cross-surface parity and gate the four-surface claim

Status: backlog

<!-- Created 2026-06-22 via bmad-correct-course (sprint-change-proposal-2026-06-22.md). Epic 8 closure story. -->
<!-- Depends on 8.1 + 8.2 (47/47 server routes) for the full golden-lifecycle wire run. -->

## Story

As a release stakeholder,
I want the golden-lifecycle and mixed-surface parity scenarios actually exercised over the wire across all four surfaces,
So that the "four-surface parity" guarantee is true end-to-end before it is asserted to consumers.

## Context

The 2026-06-22 readiness review (§5 Major #7) found Epic 5's strongest parity ACs were satisfied only at oracle-metadata / aggregate-ledger level, not wire-exercised:
- **5.5 AC7** golden lifecycle drove only 2 of 9 steps over the wire (7 asserted at oracle-metadata level).
- **5.7** cross-surface `idempotency_conflict` and `folder_acl_denied` were not fully four-surface-evidenced — an in-process gateway stub flattened `IsRejection`, so `folder_acl_denied` surfaced as **503 instead of 403** and `idempotency_conflict` was not surfaced at REST **409**.

## Acceptance Criteria

1. **Given** 47/47 server routes exist (Stories 8.1 + 8.2), **when** the golden-lifecycle parity scenario runs, **then** all 9 steps are driven over the real REST transport (and the SDK/CLI/MCP equivalents), not asserted at oracle-metadata level.
2. **Given** an ACL-denied operation, **when** invoked across REST/SDK/CLI/MCP, **then** it returns canonical `folder_acl_denied` mapped to HTTP **403** (not 503), with the matching CLI exit code and MCP failure kind from the parity oracle.
3. **Given** an idempotency conflict, **when** invoked across all four surfaces, **then** it surfaces canonical `idempotency_conflict` at HTTP **409**, with matching CLI/MCP behavior.
4. **Given** the in-process gateway stub that flattened `IsRejection`, **when** the mixed-surface handoff test runs, **then** rejection identity is preserved end-to-end (no stub flattening).
5. **Given** parity is genuinely wire-exercised at 47/47, **when** documentation/consumer references are updated, **then** the public "four-surface canonical-lifecycle parity" claim is asserted **only** after this story passes (the claim is gated on 8.1–8.3).

## References

- Verified parity result: workflow `verify-epic5-parity-gap` (2026-06-22).
- Existing parity tests: `tests/Hexalith.Folders.IntegrationTests/EndToEnd/GoldenLifecycleParityTests.cs`, `.../MixedSurfaceHandoff/MixedSurfaceHandoffTests.cs`.
- Parity oracle: `tests/fixtures/parity-contract.yaml`.
- Sprint Change Proposal: `../planning-artifacts/sprint-change-proposal-2026-06-22.md`.
