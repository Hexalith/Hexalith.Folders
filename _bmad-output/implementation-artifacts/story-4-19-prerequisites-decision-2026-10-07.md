# Decision note: Story 4.19 prerequisites (2026-10-07)

- **Decision maker:** Jerome, under the [project decision policy](../../docs/governance/approval-policy.md).
- **Proposal:** [Sprint change proposal 2026-10-06](../planning-artifacts/sprint-change-proposal-2026-10-06.md). It carries out decisions 2 and 3 of the [Story 4.19 spec](spec-4-19-prove-durable-workspace-prepare-and-lock-lifecycle.md).
- **Response:** "yes", given 2026-10-07 to the itemized proposal.

## Accepted scope

- **Edits E1–E14.** Optional item O1 (`epic-4-context.md`) was not accepted and was not applied.
- **New Story 4.23, "Enforce canonical lock identity and C7 lock timing":**
  - owns AR-CURRENT-15 and AR-AUTHZ-04;
  - is in backlog at rank 31;
  - is a prerequisite of Story 4.19;
  - depends on Stories 1.17, 4.22, 12.1, 12.2, 12.6, 12.7, and 11.15, and on accepted OQ1 (C7 `1.0.0`).
- **Story 11.15** is now a prerequisite of Stories 4.18, 4.19, 4.20, and 4.21.
- **D1, renewal.** Renewal becomes a v2-only mutation, `RenewWorkspaceLock`.
  - It is added after Story 1.17 closes and regenerated in lockstep, including the McpCli inventory.
  - It inherits v2 exposure gating, with no v1 backport.
  - Its OQ3 v2 matrix row and C13 cells need Jerome's decision when the 4.23 spec is approved.
- **D2, D3 and D4** are accepted as written in the proposal.
- **Tracker.** One key was added to the tracker without regeneration: SHA-256 changed from `230889ef…` to `cc964658…`. The A8 records keep the old digest as history.
- **Unchanged:** the execution hold, v2 exposure, sprint-status regeneration, and ordinary story execution flags; every approval and provenance digest; the hash-pinned NFR bullets; and the frozen blocks of the 4.18 and 4.19 specs.

## Checks run after the edits

| Check | Result |
| --- | --- |
| `git diff --check` | clean |
| Manifest graph (V2-002–V2-009 rules) | pass: 159 stories (160 rows with the alias), 73 ranked nodes, 262 edges, acyclic, every unresolved prerequisite strictly lower in rank |
| epics.md NFR section, 4.19 frozen block | byte-identical to `HEAD` (`bb69d36`) |
| `NfrTraceabilityConformanceTests` | 17/17 |
| `GovernanceCompletenessGateTests` | 22/22 |
| `ScaffoldContractTests` | 16/16 |

## Outstanding

- Story 4.19 stays `draft` and queued until 4.18, 4.22, 4.23, 11.15, 12.1–12.3, and 12.6 land with acceptance records and runtime evidence.
- Story 12.1 is next in the chain. It waits on `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml`, which is still absent on 2026-10-07, and on DEC-EXEC-12.1.
- The Story 4.23 spec must add the lock-identity stream to architecture.md § EventStore write side.
- Nothing is committed yet.
