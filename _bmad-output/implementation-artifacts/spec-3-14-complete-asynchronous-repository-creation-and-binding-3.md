---
title: 'Story 3.14: Complete asynchronous repository creation and binding'
type: 'feature'
created: '2026-10-17'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
    - '_bmad-output/implementation-artifacts/epic-3-context.md'
    - '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
    - 'docs/governance/approval-policy.md'
baseline_revision: 13f94c5a25e101428609456303b1e15c701bfe23
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Repository creation and binding lack durable asynchronous completion. Binding calls the provider during acceptance; creation lacks a subscriber, durable execution fence, and restart reconciler.

**Approach:** Consume Epic 12's durable substrate to dispatch both operations through one tenant-scoped process with distinct semantics, one eligible provider effect, and one canonical terminal task/binding result.

## Boundaries & Constraints

**Always:** Follow the manifest and current decision policy; complete prerequisite acceptance first. Reauthorize and durably fence before credential resolution or protected access. Persist sanitized progress, retry eligibility, phase, and evidence through EventStore. Use production adapters; reconcile unknown outcomes with at most five read-only checks within fifteen minutes.

**Never:** Absorb prerequisite ownership, repeat ambiguous mutations, persist confidential cleartext/provider output, substitute component proof for deployed durability, expose v2, or change historical specs/sprint tracking.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Create/bind | Authorized accepted request | Durable dispatch and terminal outcome | Known failure retains retry eligibility |
| Replay/denial | Duplicate, conflict, or invalid authority | Equivalent replay returns recorded result; others reject before protected access | Non-enumerating metadata |
| Ambiguity | Unconfirmed provider effect | Persist phase and bounded read-only checks across restart | Confirm outcome or require reconciliation after exhaustion/conflict |
| Recovery | Empty checkpoint and retained events | Reconstruct identical terminal state without another effect | Corrupt/unavailable evidence fails closed |

</frozen-after-approval>

## Open Questions

1. Which scope should this run pursue? **Prerequisites first:** complete 12.1 acceptance and separately owned Stories 12.2, 12.3/12.7, then 12.4 before returning here. **Keep pending:** leave 3.14 gated until prerequisites land elsewhere.

## Code Map

- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1518` -- reuse 12.1's repaired event propagation and actor-folded repository.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:113` -- registration exists without an invoking provisioning subscriber.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs:91` -- provider dispatch precedes fencing; reconciliation is absent.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` -- verify accepted prerequisites and applicable execution authorization before source changes; preserve ownership.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs` -- make binding acceptance request-only, preserving authorization and conflict checks.
- [ ] `src/Hexalith.Folders.Workers/RepositoryProvisioning/` -- compose both operations, prerequisite fences, persisted phases, and bounded reconciliation.
- [ ] `src/Hexalith.Folders/Providers/` -- add bounded read-only create/bind observations through the existing port and production adapters.
- [ ] `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` -- compose the production subscriber using the shared SDK and prerequisite task/projection pipeline.
- [ ] `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs` -- cover every matrix case, including concurrent delivery and restart without duplicate effects.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/` -- prove production-composed create/bind success, restart, denial, ambiguity, and persisted end state on the governed deployed lane.

**Acceptance Criteria:**
- Given an unmet prerequisite, when implementation is dispatched, then it remains gated and does not duplicate prerequisite ownership.
- Given authorized creation or binding, when production processing completes or replays, then one eligible effect yields one durable canonical outcome and current retry eligibility.
- Given ambiguity, when reconciliation resumes after restart, then its original operation, phase, elapsed budget, and check count survive without another mutation.
- Given retained events and an empty checkpoint, when deployed replay completes, then persisted state matches the original result without substitute evidence.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Intent gap: prerequisite scope. Irreversibles: none during planning. Footprint: acceptance, workers, projections, provider observation, and tests.

A6b/A8 and adapters 3.10/3.12 are accepted; the general hold is lifted. Story 3.14 remains held. Its prerequisites 12.1/12.2/12.4 include 12.3/12.7 transitively. Story 12.1 source verification passed 1,119 checks on October 15; deployed acceptance remains open. Other prerequisite evidence remains open, corroborated by source.

## Verification

- `git diff --check` -- documentation whitespace passes; inspect both updated documents and preserve tracker/source bytes.
- `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -nr:false` -- after implementation, focused assembly builds without warnings/errors.
- `dotnet tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests.dll -class Hexalith.Folders.Workers.Tests.RepositoryProvisioningProcessManagerTests` -- after implementation, component cases pass.

Require live-Dapr persisted-state proof. Runtime checks await implementation.
