---
title: 'Story 3.14: Complete asynchronous repository creation and binding'
type: 'feature'
created: '2026-09-20'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
    - '_bmad-output/implementation-artifacts/epic-3-context.md'
    - '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
    - '_bmad-output/planning-artifacts/epics.md'
    - 'references/Hexalith.AI.Tools/hexalith-state-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Accepted repository creation and binding cannot reach a durable asynchronous terminal result. Folder events become an eventless response, binding performs provider I/O synchronously, and no production subscriber, durable fence, restart-safe reconciler, or real-Dapr proof completes the flow.

**Approach:** After the execution hold and prerequisite graph clear, consume Epic 12's durable substrate to dispatch both requests through one tenant-scoped fenced process. Persist sanitized progress and bounded reconciliation evidence, then append one canonical terminal outcome without duplicate provider effects.

## Boundaries & Constraints

**Always:** Treat `planning-story-manifest.yaml` as execution authority. Wait for A6b/OQ3, A8, external EventStore event evolution, and Stories 12.1, 12.2, 12.3, 12.7, and 12.4. Re-authorize and durably fence before provider access; keep operations distinct; use production `IGitProvider`; allow at most five read-only checks in fifteen minutes; preserve operation/phase across restart; use EventStore SDK persistence only.

**Never:** Duplicate Epic 12; perform provider work on acceptance; retry an ambiguous mutation; accept substitute evidence; persist credentials, cleartext confidential values, provider bodies, protected locators, content, diffs, or exceptions; modify `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Success | Authorized create or bind | One asynchronous effect and terminal result | Persist sanitized evidence |
| Replay/denial | Duplicate, conflict, or invalid authority | Replay result or reject before protected access | Non-enumerating rejection |
| Failure | Conclusive or ambiguous provider result | Terminal failure or bounded reads only | Confirm outcome or `reconciliation_required` |
| Restart | Empty checkpoint, durable state | Resume phase without another effect | Fail closed on invalid state |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/planning-story-manifest.yaml:2889` -- 3.14 authority; implementation is held.
- `_bmad-output/planning-artifacts/planning-authority-relock-approval-register.yaml:334` -- pending A6b/OQ3 and A8 (line 411).
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs:1257` -- eventless accepted result; Story 12.1 owns retirement.
- `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs:121` -- synchronous provider binding.
- `src/Hexalith.Folders.Workers/RepositoryProvisioning/RepositoryProvisioningProcessManager.cs:25` -- creation-only, post-effect-fenced scaffold.
- `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs:87` -- manager registration without an invoking subscriber.
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:12` -- restart-volatile; never production persistence.
- `src/Hexalith.Folders/Providers/Abstractions/IGitProvider.cs:65` -- lacks create/bind observation; keep any addition narrow.
- `src/Hexalith.Folders/Providers/{GitHub,Forgejo}/` -- implemented mechanics to compose, not reimplement.
- `tests/Hexalith.Folders.Workers.Tests/RepositoryProvisioningProcessManagerTests.cs:14` -- fake component evidence only.

## Tasks & Acceptance

**Execution:**
- [ ] `_bmad-output/planning-artifacts/` -- require every gate to be accepted terminal before source changes.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs` -- make accepted binding request-only.
- [ ] `src/Hexalith.Folders.Workers/RepositoryProvisioning/` -- dispatch both operations with durable fencing and bounded reconciliation.
- [ ] `src/Hexalith.Folders/Providers/Abstractions/` and adapters -- add narrow read-only creation/binding observation.
- [ ] `src/Hexalith.Folders/Aggregates/Folder/` and projections -- retain sanitized outcome, budget, phase, and terminal task state.
- [ ] `src/Hexalith.Folders.Workers/FoldersWorkersModule.cs`, `deploy/dapr/production/`, and AppHost -- compose the production handler, stores, credentials, resiliency, and dead-letter path.
- [ ] `tests/Hexalith.Folders.Workers.Tests/` and `tests/Hexalith.Folders.AppHost.Tests/` -- cover the matrix and real-Dapr persisted end state.

**Acceptance Criteria:**
- Given any authority or prerequisite gate is incomplete, when 3.14 is dispatched, then implementation does not begin and no prerequisite ownership is duplicated.
- Given an authorized creation or binding request, when the production worker executes or replays it, then exactly one eligible provider effect and one durable canonical terminal result occur.
- Given an ambiguous provider outcome, when automatic reconciliation runs, then no mutation is retried, at most five reads occur within fifteen minutes, and the result becomes confirmed or `reconciliation_required`.
- Given an empty checkpoint after restart, when retained events replay, then the same result is reconstructed without another provider effect and no substitute path can satisfy completion.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Story 3.14 integrates prerequisite capabilities. Story 12.4 transitively requires 12.1, 12.2, 12.3, and 12.7; the older spec omitted 12.7. Stories 3.10–3.13 remain provider-mechanics authority.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Workers.Tests/Hexalith.Folders.Workers.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=true -p:MinVerVersionOverride=1.0.0 -p:NuGetAudit=false` -- expected: focused worker tests build without warnings or errors.
- `tests/Hexalith.Folders.Workers.Tests/bin/Release/net10.0/Hexalith.Folders.Workers.Tests -noLogo -noColor -class Hexalith.Folders.Workers.Tests.RepositoryProvisioningProcessManagerTests` -- expected: create/bind fencing, replay, denial, failure, and reconciliation tests pass.
- `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true tests/Hexalith.Folders.AppHost.Tests/bin/Release/net10.0/Hexalith.Folders.AppHost.Tests -noLogo -noColor -class Hexalith.Folders.AppHost.Tests.Story314RepositoryProvisioningE2ETests` -- expected: production-composed durable success, empty-checkpoint restart, tenant denial, ambiguity, and state-store end-state assertions pass.
- `git diff --check` -- expected: no whitespace errors.
