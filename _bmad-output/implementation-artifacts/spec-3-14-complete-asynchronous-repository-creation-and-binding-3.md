---
title: 'Story 3.14: Complete asynchronous repository creation and binding'
type: 'feature'
created: '2026-10-18'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
    - '_bmad-output/implementation-artifacts/epic-3-context.md'
    - '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
    - 'docs/governance/approval-policy.md'
baseline_revision: e8ab7aff5aff6ad1e2f2e837be30ebbf3e593bd2
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Repository creation and binding lack durable asynchronous completion. Binding calls the provider during acceptance; creation lacks a subscriber, durable execution fence, and restart reconciler.

**Approach:** Consume Epic 12's durable substrate to dispatch both operations through one tenant-scoped process with distinct semantics, one eligible provider effect, and one canonical terminal task/binding result.

**Decision (human, 2026-10-18):** Keep 3.14 held. This spec stays `draft` until DEC-EXEC-3.14 exists and Stories 12.1, 12.2, and 12.4 (with 12.3 and 12.7) are accepted. The next work is 12.2 or 12.3 in its own session. Keep the full spec unsplit despite exceeding the token guideline.

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

## Code Map

- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs:146-202` -- acceptance resolves the provider (:146) and calls `ValidateRepositoryBindingAsync` (:163) before appending (:202); make it request-only like `RepositoryBackedFolderCreationService.cs:136`.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs:46-166` -- an in-memory state check (:46-55) precedes `CreateRepositoryAsync` (:91) with no durable fence; an unknown result appends `ProviderOutcomeUnknown` (:148-166) and stops, without bounded checks.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:113-122` -- singleton registration only; no route maps `RepositoryBindingRequested` (existing routes :144, :179). Workers registers no `IFolderRepository`, and request-scoped `src/Hexalith.Folders.Server/ActorFoldedFolderRepository.cs:12` cannot serve it; the durable read/task path comes from 12.2/12.4.
- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs:17,65` -- `ValidateRepositoryBindingAsync` is the only read-only repository lookup (GET-only in both adapters). `GetOperationStatusAsync` is commit-shaped. Add a metadata-only creation/binding observation through the port and both adapters; never reuse the commit status kinds.
- Reuse from 12.1: `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1518` (event propagation), `FolderEventReplay.cs:16`, `FolderDomainProjectionHandler.cs:12`, and production boot `FoldersServerHostComposition.cs:77-87`.
- Placeholders that prerequisites must replace, not this story: `FoldersServerServiceCollectionExtensions.cs:92,95` (unavailable content store/commit executor) and `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:197` (unconfigured outcome store).
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs` -- 14 fake-driven cases; no subscriber, fence, or reconciliation coverage. `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceTests.cs:32` -- opt-in durability lane pattern to reuse.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` and the two earlier 3.14 specs -- never modify.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/planning-story-manifest.yaml` -- confirm DEC-EXEC-3.14 and accepted 12.1/12.2/12.4 before any source change; HALT otherwise.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs` -- request-only acceptance; keep authorization, canonical identity, duplicate, and conflict checks.
- [ ] `src/Hexalith.Folders/Providers/` -- bounded read-only creation/binding observation in GitHub and Forgejo adapters.
- [ ] `src/Hexalith.Folders.Workers/RepositoryProvisioning/` -- distinct create/bind handling, a durable fence before credential resolution, a persisted phase/check count/elapsed budget, and reconciliation leading to confirmation or `reconciliation_required`.
- [ ] `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs` -- production subscriber over the prerequisite durable read/task pipeline.
- [ ] `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs` -- every matrix row, plus concurrent delivery and restart without a duplicate effect.
- [ ] `tests/Hexalith.Folders.AppHost.Tests/` -- deployed create/bind success, restart, denial, ambiguity, and persisted end state.

**Acceptance Criteria:**
- Given an unmet prerequisite, when implementation is dispatched, then it remains gated and does not duplicate prerequisite ownership.
- Given authorized creation or binding, when production processing completes or replays, then one eligible effect yields one durable canonical outcome and current retry eligibility.
- Given ambiguity, when reconciliation resumes after restart, then its original operation, phase, elapsed budget, and check count survive without another mutation.
- Given retained events and an empty checkpoint, when deployed replay completes, then persisted state matches the original result without substitute evidence.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Refreshed 2026-10-18 at the baseline. The prerequisite-scope gap was resolved by the frozen decision to keep 3.14 held. Irreversibles: none while planning. Footprint: acceptance, workers, provider port and adapters, tests.

Story 12.1 landed, and the tracker marks it `done`; the manifest still records `backlog`/`authorized-scoped`/`open`, and that reconciliation needs Jerome's decision note outside this story. Only DEC-EXEC-1.17 and DEC-EXEC-12.1 exist. Story 12.6 is not on 3.14's path.

## Verification

**Commands:**
- `git diff --check` -- expected: no whitespace errors; tracker and historical spec bytes unchanged.
- `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -c Debug -p:UseHexalithProjectReferences=true -m:1 -nr:false` -- expected after implementation: 0 warnings, 0 errors.
- `dotnet tests/Hexalith.Folders.Workers.Tests/bin/Debug/net10.0/Hexalith.Folders.Workers.Tests.dll -class Hexalith.Folders.Workers.Tests.RepositoryProvisioningProcessManagerTests` -- expected after implementation: all cases pass.

**Manual checks (if no CLI):**
- A governed live-Dapr lane shows persisted create/bind end state and restart replay; component results alone do not satisfy it.
