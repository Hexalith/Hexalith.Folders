---
title: 'Publish the PD10 v2 Authorization Contract Spine'
type: 'feature'
created: '2026-09-22'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/planning-story-manifest.yaml'
  - '{project-root}/_bmad-output/planning-artifacts/epics.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Consumers still lack a published, digest-bound PD10 v2 authorization spine. A candidate already exists, and treating it as Story 1.17 closure or as a routed release would skip the execution gates.

**Approach:** Leave the candidate, historical v1 spine, and accepted A6b, Section 9, and A8 records unchanged. Decision: stop. Story 1.17 stays backlog. This spec is not approved and does not start a slice, GENERATE review, or production publication.

## Boundaries & Constraints

**Always:** Keep `hexalith.folders.v1.yaml` byte-for-byte and preserve historical OQ3 evidence. Authorize before any protected lookup or side effect. Keep fourteen access states, every protected family once, and the 49-operation candidate. Unauthenticated is `401`; fresh negative authority is one byte-equivalent `404`; unusable authority is one retryable `503`. CLI `73`/`77` and MCP `concurrency_conflict` stay paired. Generated SDK and parity rows stay generator-owned. `sprint-status.yaml` stays orchestrator-owned.

**Never:** Map `/api/v2` from production `Program.cs`, publish a release, or close Story 1.17. Infer a new A6b or A8 approval, or flip `v2_exposure_authorized` or `ordinary_story_execution_authorized`. Hand-edit generated output or revive `tests/tools/pd10-v2-contract-generator/`. Start a slice, resume GENERATE review, or cut production over from this stopped spec.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Unauthenticated | No actor | No protected lookup | Canonical `401` |
| Fresh negative authority | Denied, hidden, absent, or out of scope | Zero protected reads | Byte-equivalent `404` |
| Unusable authority | Stale, unavailable, conflicting, or incomplete | Zero protected reads | Retryable `503` |
| Production host | Current `Program.cs` | Historical endpoints only | v2 seam stays unwired |
| External v1 consumer | Deployed consumer outside this repo | Stop | Escalate; no invented migration window |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` -- historical spine. Do not change.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v2.yaml` -- unrouted candidate. Touch only if the chosen unit already owns it.
- `scripts/generate-pd10-v2-contract.py`, `scripts/generate-pd10-v2-runtime-catalog.py`, `scripts/generate-pd10-v2-conformance-set.py` -- generators on HEAD. The deleted C# generator stays deleted.
- `src/Hexalith.Folders.Server/Program.cs` -- `MapFoldersServerEndpoints()` only. Do not call `UsePd10V2CandidateCompatibilitySeam`.
- `src/Hexalith.Folders.Server/Pd10V2CandidateCompatibilitySeam.cs` and `Authorization/Pd10ProtectedOperationExecutor.cs` -- test-only seam and `ExecuteAsync`. Do not promote into production routing.
- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj` -- SDK already generated from v2. Do not hand-edit it.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10V2CandidateContractTests.cs` -- v1 byte pin and non-routing guard.
- `docs/contract/authorization-matrix.md` -- `2.0.0` digest bound by accepted `DEC-A6B-OQ3`. Do not retarget it.
- `_bmad-output/planning-artifacts/planning-story-manifest.yaml` -- exposure and ordinary execution are false; Story `1.17` is backlog and held; GENERATE evidence is open; A6b, Section 9, and A8 are accepted. Do not edit.
- `_bmad-output/implementation-artifacts/spec-1-17-generate-pd10-v2-relock-milestone.md` -- in-review GENERATE spec, loop 12. Not Story 1.17 closure.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/implementation-artifacts/spec-1-17-publish-the-pd10-v2-authorization-contract-spine.md` -- leave this spec draft and unapproved -- the scope answer is stop.
- [ ] `src/Hexalith.Folders.Server/Program.cs` -- leave mapping historical-only -- exposure is not authorized.
- [ ] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/Pd10V2CandidateContractTests.cs` -- keep non-routing and v1 byte-stability green -- current exposure guard.

**Acceptance Criteria:**
- Given this spec is draft and the scope answer is stop, when the session ends, then no contract, server, SDK, CLI, MCP, UI, approval, or sprint-status file has changed for Story 1.17.
- Given the production host, when it starts from `Program.cs`, then it does not map `/api/v2`.
- Given the scope answer is stop, when this session ends, then Story 1.17 stays backlog and this spec stays unapproved.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Story 1.17 is slices `1.17-A` through `1.17-G`. A6b, Section 9, and A8 are accepted, but the story stays held and GENERATE evidence stays open. The in-review GENERATE spec is the only active 1.17 artifact. Do not reimplement Story 1.16's gates.

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --filter Pd10V2CandidateContractTests` -- expected: v1 byte-stability and production non-routing tests pass with no source edits.
