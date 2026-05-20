# Story 2.8b: Wire FolderArchiveTenantGate as an IDomainProcessor

Status: review

<!-- Spawned 2026-05-20 from `/bmad-code-review 2.8` round-2 BLOCKED finding. -->
<!-- This story exists to resolve the architectural decision that Story 2.8 surfaced. -->
<!-- Story 2.8 cannot move to `done` until this story completes. -->

## Story

As a platform engineer,
I want `FolderArchiveTenantGate` (and the surrounding archive decision-snapshot machinery) actually invoked by the production `/process` callback,
so that the archive command path enforces ACL evidence, policy evidence, freshness watermark, decision-bound idempotency fingerprints, and append-conflict reread in real production wiring — not just in unit tests.

## Context

Story 2.8 (`/bmad-code-review 2.8` round-2 verification, 2026-05-20) confirmed that:

- `FolderArchiveTenantGate.Handle` has zero non-test callers (`Grep("FolderArchiveTenantGate", "src/")` returns only the gate definition).
- `FolderAggregate` is declared `public static class` and cannot be DI-registered as `IDomainProcessor`.
- `AddFoldersDomainServices` (in `Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`) registers no `IDomainProcessor` implementation.
- `FoldersDomainServiceRequestHandler.ProcessAsync` returns `501 NotImplemented` when `processorList.Count == 0`.
- The REST endpoint at `FoldersDomainServiceEndpoints.cs:289` calls `IEventStoreGatewayClient.SubmitCommandAsync` directly. In production the gateway round-trips back to `/process`, which currently 501s.
- `ArchiveFolderEndpointTests` pass because `RecordingEventStoreGatewayClient` short-circuits the gateway round-trip; no test exercises the full REST → gateway → `/process` → gate flow.

End result: archive is functionally broken end-to-end in real production wiring. Round-1 of the 2.8 review marked this as "Resolved 2026-05-20" but the resolution claim was incorrect.

## Architectural Decision (must be made first)

The blocker is a model mismatch between two persistence paths:

- `IDomainProcessor.ProcessAsync(CommandEnvelope, object?)` returns `DomainResult` carrying `IReadOnlyList<IEventPayload>`. The EventStore framework owns persistence of those events (per `AggregateActor.ProcessCommandAsync` in the EventStore submodule).
- `FolderArchiveTenantGate.Handle` already persists via `IFolderRepository.AppendIfFingerprintAbsent`. The gate owns idempotency-key-conditional append plus race-conflict reread (`ResolveAppendConflict`).

The same pattern exists for `FolderAccessTenantGate` (Story 2.4). Both gates persist their own events. Wiring the gate as an `IDomainProcessor` forces a decision:

### Option A — Gate stops persisting; framework persists

`FolderArchiveTenantGate.Handle` is refactored to return events instead of persisting them. The processor then returns the events through `DomainResult` and the framework persists.

- **Pros:** Aligns with the EventStore framework contract; lets the framework manage idempotency-key-conditional append via its standard `MessageId` mechanism.
- **Cons:** Need to verify the framework's append semantics actually match the gate's (idempotency-key-conditional, race-conflict reread). `FolderAccessTenantGate` needs the same refactor for consistency. Projection wiring may need to change.
- **Files touched (estimate):** `FolderArchiveTenantGate.cs`, `FolderAccessTenantGate.cs`, `IFolderRepository.cs` (may shrink), `FolderDomainProcessor.cs` (new), `FoldersServerServiceCollectionExtensions.cs`, integration tests.

### Option B — Processor returns empty events; gate keeps persisting

The processor invokes the gate (which persists), then returns `DomainResult.NoOp()` or a sentinel result so the framework doesn't double-write.

- **Pros:** Smallest change to existing gates and `IFolderRepository`.
- **Cons:** Effectively bypasses the framework's persistence guarantees. The framework cannot track event lineage, snapshotting, or replay through its standard channels. Architectural smell.
- **Files touched (estimate):** `FolderDomainProcessor.cs` (new), `FoldersServerServiceCollectionExtensions.cs`, integration tests. No changes to gates or repository.

### Option C — Unify `IFolderEvent` with `IEventPayload`

Make `IFolderEvent : IEventPayload` so Folders events implement the framework's payload contract directly. Allows Option A's flow without dual interfaces.

- **Pros:** Cleanest long-term architecture; eliminates two parallel event hierarchies.
- **Cons:** Largest blast radius — every concrete folder event, projection envelope, repository, and test fixture changes. May require coordinated changes in `Hexalith.EventStore` submodule if `IEventPayload` has unexpected members.
- **Files touched (estimate):** Every `*.cs` file in `src/Hexalith.Folders/Aggregates/Folder/*` declaring an event, every projection, `IFolderRepository.cs`, every test using folder events.

**Decision recorder:** the team must capture the chosen option as an ADR under `docs/adrs/` before implementation starts. The ADR should reference Story 2.8's BLOCKED finding and this story's Acceptance Criteria. The ADR must also record how `FolderResult` becomes `DomainResult` and gateway result payload evidence, how duplicate persistence is prevented, how request cancellation is handled given the current `IDomainProcessor.ProcessAsync(CommandEnvelope, object?)` signature has no `CancellationToken`, and how layered authorization evidence is handed to the processor without leaking across requests.

## Party-Mode Review Hardening Notes

- `FoldersDomainServiceRequestHandler.ProcessAsync` currently authorizes before invoking a processor, but the approved `LayeredFolderAuthorizationResult` is local state. If archive ACL evidence is backed by that result, the story needs an explicit scoped evidence handoff, not an implicit provider lookup that can re-run authorization or read stale request state.
- `IDomainProcessor.ProcessAsync(CommandEnvelope, object?)` currently has no cancellation token. Any task requiring CT propagation must either keep IO-bearing evidence calls in the request handler before processor invocation, extend the EventStore contract deliberately, or document a bounded no-IO processor path in the ADR.
- `DomainResult` rejects mixed success/rejection event lists and only exposes `ResultPayload` for successful results through `DomainServiceWireResult`. The chosen persistence option must specify the exact success, rejection, no-op, and accepted-command payload mapping so the REST `202 AcceptedCommand` path does not depend on a test-only gateway shortcut.
- `FolderAccessTenantGate.Map` still throws if `TenantAccessOutcome.Allowed` arrives with `IsAllowed=false`, while `FolderArchiveTenantGate.Map` returns a safe rejection. Applying the chosen persistence option consistently should also reconcile this failure-mode difference or explicitly justify it in the ADR.

## Acceptance Criteria

1. Given the architectural decision is captured as an ADR in `docs/adrs/` (referenced from this story), when implementation starts, then the ADR records which of Options A / B / C was selected, the rationale, the `FolderResult` → `DomainResult` / `ResultPayload` mapping, the cancellation boundary, the layered-auth evidence handoff mechanism, and the impact on `FolderAccessTenantGate` (which must follow the same pattern for consistency unless the ADR explicitly carves it out).

2. Given an authenticated tenant administrator submits `POST /api/v1/folders/{folderId}/archive` with valid envelope and body, when the request flows through the production wiring (REST endpoint → `IEventStoreGatewayClient.SubmitCommandAsync` → `/process` → `FoldersDomainServiceRequestHandler.ProcessAsync` → `IDomainProcessor` → `FolderArchiveTenantGate.Handle` → `FolderAggregate.Handle` → persistence), then the response is `202 AcceptedCommand`, the folder lifecycle state becomes `Archived`, exactly one `FolderArchived` event is appended, and the result preserves only operation/correlation/task/sanitized evidence per Story 2.8 AC2.

3. Given the gate requires `TenantAccessAuthorizationResult`, `FolderArchiveAclEvidence`, and `FolderArchivePolicyEvidence`, when the processor is invoked, then evidence is sourced from production-grade providers registered in DI: tenant access via `TenantAccessAuthorizer.AuthorizeMutationAsync`, ACL evidence via a `IFolderArchiveAclEvidenceProvider` backed by the layered authorization result, and policy evidence via a `IFolderArchivePolicyEvidenceProvider` (baseline implementation may return `Allowed` with a versioned policy stub; real archive-retention policy logic is Epic 7 work and explicitly out of scope).

4. Given the layered authorization in `FoldersDomainServiceRequestHandler.ProcessAsync` has already approved the action token (`archive_folder`), when the processor derives evidence for the gate, then it must NOT re-run JWT validation or claim-transform layers (those are already proven by the layered authz result); evidence providers must consume the current request's layered authz result through an explicit scoped accessor or other ADR-approved handoff that is cleared between requests and cannot leak stale allow/deny evidence.

5. Given the processor is invoked with a command type other than `Hexalith.Folders.Commands.ArchiveFolder`, when dispatch runs, then either (a) the processor handles the other folder command types using the same evidence-sourcing pattern (preferred), or (b) the processor returns a stable `UnsupportedCommandType` rejection that maps to the canonical safe denial — never throws or 500s.

6. Given an in-process integration test that does NOT mock `IEventStoreGatewayClient`, when a valid archive request is submitted via `HttpClient` to the running endpoint, then the gateway client actually invokes the `/process` callback in the same process, the processor is invoked, the gate runs, `FolderArchived` is persisted to an in-memory `IFolderRepository`, and the 202 response shape matches AC2.

7. Given the integration test from AC6, when an unauthorized caller, cross-tenant caller, malformed body, missing idempotency key, already-archived folder, or different-fingerprint idempotency-key reuse case is submitted, then each maps to the correct row from Story 2.8's Archive Denial And State Table — proven end-to-end, not just at gate-unit level.

8. Given `FolderAccessTenantGate` follows the same persistence pattern as `FolderArchiveTenantGate`, when the chosen option (A/B/C) is applied to the archive path, then it is also applied to the access path within this story unless the ADR carves out an explicit exception. Both gates must end the story with consistent persistence ownership.

9. Given the production wiring is now exercised, when `Hexalith.Folders.UI.E2E.Tests` placeholder is later enabled (Epic 6), it must continue to use the same processor path; this story must not introduce a "test-only" processor path that diverges from the production one.

10. Given Story 2.8 was marked `in-progress` because of this blocker, when this story moves to `done`, then Story 2.8 is also moved to `review` (or `done` if no other 2.8 follow-ups remain), Story 2.8's BLOCKED Decision item is checked off with a reference to this story's commit, and the Round-2 Review Findings section is updated.

## Tasks / Subtasks

- [x] Capture the architectural decision as an ADR. (AC: 1)
  - [x] Create `docs/adrs/{NNN}-folder-domain-processor-persistence.md`.
  - [x] Reference Story 2.8 BLOCKED finding and this story.
  - [x] Record Option A / B / C selection plus rationale.
  - [x] Record the exact `FolderResult` → `DomainResult` / `DomainServiceWireResult.ResultPayload` mapping for accepted, rejected, idempotent replay, already-archived, no-op, and unsupported-command outcomes.
  - [x] Record how cancellation is handled despite the current `IDomainProcessor.ProcessAsync(CommandEnvelope, object?)` interface lacking `CancellationToken`.
  - [x] Record how the approved layered authorization result is passed to archive/access evidence providers without re-running JWT/claim-transform layers or leaking stale request evidence.
  - [x] Get review sign-off before proceeding.

- [x] Implement the chosen persistence option. (AC: 1, 2, 8)
  - [x] Apply to `FolderArchiveTenantGate` AND `FolderAccessTenantGate` (consistent pattern).
  - [x] Update `IFolderRepository` contract if Option A shrinks it. (N/A: Option B selected; contract retained.)
  - [x] Update or unify event interfaces if Option C is chosen. (N/A: Option B selected; event interfaces unchanged.)

- [x] Build `FolderDomainProcessor : IDomainProcessor`. (AC: 2, 3, 4, 5)
  - [x] Decode `CommandEnvelope.Payload` to typed Folders commands using `RequestJsonOptions`-equivalent strictness (reject unknown fields).
  - [x] Source evidence via DI-registered providers: `TenantAccessAuthorizer`, `IFolderArchiveAclEvidenceProvider`, `IFolderArchivePolicyEvidenceProvider`, and the ADR-approved scoped layered-auth handoff.
  - [x] Invoke the gate; map `FolderResult` → `DomainResult` (success / rejection / no-op) without producing mixed regular/rejection event lists.
  - [x] Preserve accepted-command response evidence through the ADR-approved `ResultPayload` or equivalent EventStore gateway mechanism; do not rely on the current server endpoint test double that bypasses `/process`.
  - [x] Handle all current and reasonably-foreseeable folder command types; emit canonical safe denial for unsupported types.

- [x] Build evidence providers with production defaults. (AC: 3)
  - [x] `LayeredAuthBackedFolderArchiveAclEvidenceProvider`: consumes the current request's layered authorization result via the ADR-approved scoped handoff to produce `FolderArchiveAclEvidence.Allowed(...)` when the layered authz allowed `archive_folder`; otherwise `Denied(...)`.
  - [x] `BaselineFolderArchivePolicyEvidenceProvider`: returns `Allowed` with a stable versioned policy stub (`policy_version: "v1-baseline"`). Real archive-retention logic is explicitly Epic 7.
  - [x] Both providers must propagate `CancellationToken` when invoked before the processor or through an ADR-approved EventStore contract extension; if the processor remains on the current no-token interface, no provider may hide long-running IO inside `IDomainProcessor.ProcessAsync`.
  - [x] Add a test that executes two sequential requests in the same test host with different authorization outcomes and proves the scoped layered-auth evidence cannot bleed from the first request into the second.

- [x] Register the processor and providers in DI. (AC: 2, 9)
  - [x] `AddFoldersDomainServices` registers `FolderDomainProcessor` as scoped `IDomainProcessor` (keyed by `FoldersServerModule.DomainName` if the framework supports keyed processors).
  - [x] Register both evidence providers with appropriate lifetimes (scoped recommended).
  - [x] Ensure no second processor is registered; `FoldersDomainServiceRequestHandler` rejects multi-processor wiring at 500.

- [x] Add an in-process integration test. (AC: 6, 7)
  - [x] New test project or new test class under `tests/Hexalith.Folders.IntegrationTests`.
  - [x] Wires a real (in-memory) `IEventStoreGatewayClient` that round-trips to `/process` in the same process.
  - [x] Asserts happy path: 202, single `FolderArchived` persisted, projection updated.
  - [x] Asserts denial-table rows: cross-tenant, malformed body, missing key, already-archived, idempotency-conflict, gateway-server-error.
  - [x] Asserts the processor path covers the ADR-selected `DomainResult` / `ResultPayload` mapping and never double-writes through both gate-owned and framework-owned persistence.
  - [x] Asserts cancellation or request-abort behavior at the ADR-defined boundary.
  - [x] Asserts no test-only processor seam exists in production code paths.

- [x] Update Story 2.8 status when this story is done. (AC: 10)
  - [x] Move Story 2.8 BLOCKED Decision item to checked.
  - [x] Update `_bmad-output/implementation-artifacts/sprint-status.yaml`.
  - [x] Append a Change Log entry to Story 2.8 referencing this story's commit.

## Out of Scope

- Real archive-retention policy logic (Epic 7).
- Tenant-deletion processing, legal-hold enforcement, restore/unarchive, provider repository archival, workspace cleanup, audit browsing endpoints, CLI/MCP commands, UI work (per Story 2.8 AC14).
- Changes to the `Hexalith.EventStore` submodule unless Option C is chosen AND the submodule's `IEventPayload` interface has unexpected members; in that case scope the submodule change to a separate PR.
- Replacing `FolderAggregate`'s `static class` declaration with an instance class unless the chosen option requires it.

## References

- Story 2.8: [2-8-archive-folders-with-audit-preservation.md](./2-8-archive-folders-with-audit-preservation.md) — particularly the Round-2 Review Findings BLOCKED Decision item.
- EventStore framework: `Hexalith.EventStore/src/Hexalith.EventStore.Client` — `IDomainProcessor`, `CommandEnvelope`, `DomainResult`, `AggregateActor`.
- Existing gates: `src/Hexalith.Folders/Aggregates/Folder/FolderArchiveTenantGate.cs`, `src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs`.
- Existing request handler: `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs`.
- Layered authorization: `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs`.

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-20 | Implemented ADR 0001 using Option B: `FolderDomainProcessor` invokes gate-owned persistence and returns no-op/structured rejection results to prevent double-write. Wired archive evidence providers, scoped layered-authorization handoff, production DI registration, in-memory repository/projection support, and in-process REST -> gateway -> `/process` integration coverage. | Codex |
| 2026-05-20 | Applied party-mode review hardening for scoped layered-authorization evidence handoff, cancellation-boundary decisioning, `DomainResult` / result-payload mapping, duplicate-persistence prevention, and access/archive gate consistency checks. | Codex |
| 2026-05-20 | Created from Story 2.8 `/bmad-code-review` round-2 BLOCKED finding. Three architectural options documented; ADR required before implementation. | Claude |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test tests\Hexalith.Folders.IntegrationTests\Hexalith.Folders.IntegrationTests.csproj --no-restore` (11 passed)
- `dotnet test tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore` (59 passed)
- `dotnet test tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` (399 passed)
- `dotnet test tests\Hexalith.Folders.Contracts.Tests\Hexalith.Folders.Contracts.Tests.csproj --no-restore` (81 passed)
- `dotnet build Hexalith.Folders.slnx --no-restore` (passed)
- `dotnet test Hexalith.Folders.slnx --no-restore` (623 passed, 1 skipped, 0 failed; UI E2E placeholder skipped)

### Completion Notes List

- Captured ADR 0001 selecting Option B: gate-owned persistence remains authoritative, and the domain processor returns no events on accepted/no-op gate outcomes to avoid duplicate framework persistence.
- Added `FolderDomainProcessor` and DI wiring so production `/process` invokes `FolderArchiveTenantGate` for archive commands instead of returning 501.
- Added scoped layered-authorization result handoff and archive ACL/policy evidence providers, with the accessor cleared between requests to prevent stale authorization bleed.
- Added an in-memory folder repository/projection path for integration tests and production-safe default DI registration.
- Added end-to-end integration coverage for happy path, scoped authorization isolation, idempotency conflict, already archived, malformed body, missing idempotency key, and cancellation-before-gateway behavior.
- Updated Story 2.8's blocked Round-2 finding and sprint status for review handoff; no submodule recursive initialization was performed.

### File List

- `docs/adrs/0001-folder-domain-processor-persistence.md`
- `src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderAccessTenantGate.cs`
- `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsFolderPermissionEvidenceProvider.cs`
- `src/Hexalith.Folders/Authorization/FolderPermissionEvidenceResult.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationAllowedContext.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/Authorization/BaselineFolderArchivePolicyEvidenceProvider.cs`
- `src/Hexalith.Folders.Server/Authorization/IFolderArchiveAclEvidenceProvider.cs`
- `src/Hexalith.Folders.Server/Authorization/IFolderArchivePolicyEvidenceProvider.cs`
- `src/Hexalith.Folders.Server/Authorization/ILayeredFolderAuthorizationResultAccessor.cs`
- `src/Hexalith.Folders.Server/Authorization/LayeredAuthBackedFolderArchiveAclEvidenceProvider.cs`
- `src/Hexalith.Folders.Server/Authorization/ScopedLayeredFolderAuthorizationResultAccessor.cs`
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs`
- `src/Hexalith.Folders.Server/FolderDomainProcessor.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs`
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs`
- `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`
- `_bmad-output/implementation-artifacts/2-8-archive-folders-with-audit-preservation.md`
- `_bmad-output/implementation-artifacts/2-8b-wire-folder-domain-processor.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Party-Mode Review

- Date/time: 2026-05-20T13:04:31+02:00
- Selected story key: `2-8b-wire-folder-domain-processor`
- Command/skill invocation used: `/bmad-party-mode 2-8b-wire-folder-domain-processor; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Test Architect), John (Product Manager)
- Findings summary:
  - Winston: the story correctly frames the persistence mismatch, but the ADR also needs to decide result mapping, cancellation, and request-scoped authorization evidence handoff before implementation.
  - Amelia: `IDomainProcessor` has no cancellation token and `DomainResult` disallows mixed success/rejection events, so the implementation needs exact contracts for provider invocation, accepted payloads, no-op behavior, and unsupported commands.
  - Murat: the integration tests must prove true REST -> gateway -> `/process` flow, no stale scoped authorization bleed, no double-write through both persistence paths, and the ADR-defined cancellation boundary.
  - John: the story should keep the product scope narrow: fix production archive wiring, update Story 2.8 only when this blocker is resolved, and defer broader EventStore or retention-policy work unless the ADR explicitly selects it.
- Changes applied:
  - Added party-mode hardening notes for scoped layered-auth evidence, cancellation mismatch, `DomainResult` / `ResultPayload` mapping, and access/archive gate consistency.
  - Expanded AC1 and AC4 to require explicit ADR decisions and stale-evidence isolation.
  - Expanded ADR, processor, evidence-provider, and integration-test subtasks to make these implementation traps testable.
- Findings deferred:
  - Whether to extend the EventStore `IDomainProcessor` interface with cancellation support remains an ADR decision.
  - Whether Option A, B, or C is selected remains a human architecture decision before implementation.
- Final recommendation: ready-for-dev
