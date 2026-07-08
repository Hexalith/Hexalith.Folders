# Sprint Change Proposal — Story 8.1 REST negative-path coverage extension

- **Date:** 2026-07-08
- **Author / Facilitator:** Jerome (via `bmad-correct-course`)
- **Trigger:** Sprint-status action item `_bmad-output/implementation-artifacts/sprint-status.yaml:385` (Epic 8; owner *Murat / Amelia*; priority *medium*; status *open*)
- **Scope classification:** **Minor** (test-only; Direct Adjustment)
- **Mode:** Incremental review · all four proposals approved as drafted · execution deferred (proposal only)

---

## Section 1 — Issue Summary

Story 8.1 ("Implement the 8 missing Bucket-A canonical REST server routes", status `done`) shipped all 8 routes with production code intact and each mutating leg proven end-to-end by no-mock `/process` integration tests. Its Senior Developer Review (`_bmad-output/implementation-artifacts/8-1-implement-bucket-a-missing-rest-server-routes.md:212–217`) recorded three **non-blocking MED follow-ups** — fast **route-level** `Server.Tests` that were never added because the legs were already covered end-to-end:

1. Authenticated-but-denied **403 `denied_safe`** for the read ops (to prove 401/403/404 caller-visible indistinguishability).
2. op7 `GetWorkspaceRetryEligibility` **404/503** legs + op3 `UpdateFolderAclEntry` route-level **404** (folder-not-found).
3. A `Server.Tests` route-level **200 happy-path** for `ListFolderAclEntries` (entry shape + `aclEntryId` round-trip; currently proven only end-to-end).

These were consolidated into the open action item at `sprint-status.yaml:385`.

**Core problem (categorized): test-coverage gap, not a defect.** No production behavior is wrong; the negative-path and happy-path legs execute correctly and are exercised by the integration lane. What is missing is fast, targeted route-level coverage in `Hexalith.Folders.Server.Tests` that pins the caller-visible envelope (status code, category/code, no-echo safe denial) for each leg. Discovered during the Story 8.1 review; surfaced again while triaging the Epic 8 action-item ledger.

**Evidence:**
- `8-1-…md:214` — `[AI-Review][MED]` authenticated 403 `denied_safe` for read ops (noted as needing "a denying-authorization double").
- `8-1-…md:215` — `[AI-Review][MED]` op3 `UpdateFolderAclEntry` route-level 404; op7 `GetWorkspaceRetryEligibility` 404/503 legs.
- `8-1-…md:216` — `[AI-Review][MED]` `Server.Tests` route-level 200 happy-path for `ListFolderAclEntries`.
- `sprint-status.yaml:385` — the tracking action item (status `open`).

---

## Section 2 — Impact Analysis

**Epic impact — none.** Epic 8 is `done` (`sprint-status.yaml:172`) with its retrospective closed. This change adds tests *within* the existing epic structure; no epic is modified, added, removed, resequenced, or reprioritized. No future epic is affected.

**Artifact conflicts — none.**
- **PRD:** no conflict — no FR/NFR change; MVP unaffected.
- **Architecture:** no conflict — no new component, pattern, or contract; the OpenAPI Contract Spine (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`) is unchanged and the **C13 parity oracle is untouched** (no new operations; REST stays 40/47 for Bucket A).
- **UX:** N/A — no UI surface.

**Technical impact — localized, test-only.**
- Two existing `Server.Tests` files change: `tests/Hexalith.Folders.Server.Tests/FolderAclEndpointTests.cs` and `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`.
- No production `src/` change. No new bespoke test doubles beyond patterns already established in the suite.
- All four legs reuse in-repo precedents and existing test seams (verified 2026-07-08):
  - **Authorized-caller authz wiring** — `GetRepositoryBindingEndpointTests.BuildApp` (TenantStore + `IEffectivePermissionsReadModel` + `AllowingEventStoreAuthorizationValidator` + `FixedUtcClock`).
  - **Folder-state seeding** — `InMemoryFolderRepository.Seed(streamName, IReadOnlyList<IFolderEvent>)` (`src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:166`), `InternalsVisibleTo("Hexalith.Folders.Server.Tests")`.
  - **Authenticated 403 via tenant-mismatch** — `EffectivePermissionsRouteShouldUseSafeDenialEnvelopeForTenantMismatch` (client `X-Hexalith-Tenant-Id` ≠ authoritative tenant → `TenantAccessDenied` → 403 `denied_safe`). **No new denying-authorization double is required** — this resolves the open concern the review flagged.
  - **503 via throwing read model** — `EffectivePermissionsRouteShouldEmit503ProblemDetailsForUnavailableReadModel` (`ThrowingReadModel`).
  - **Mutating-op 404 via faulting gateway** — `ArchiveFolderEndpointTests.cs:138–148,523` (`[InlineData(404,"not_found","not_found")]`; `EventStoreGatewayException` → `ToArchiveGatewayProblem`).

**Tracking impact:** Story 8.1 File List / Change Log gains the new test entries at implementation time; `sprint-status.yaml:385` action item closes on completion.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment.** Add four route-level tests inside the current Epic 8 structure.

- **Effort:** Low. **Risk:** Low. **Timeline impact:** none (no release-blocking dependency).
- **Option 2 (Rollback):** not viable — nothing to revert; the routes are correct and shipped.
- **Option 3 (MVP review):** N/A — MVP scope unaffected.

**Rationale:** the legs are already implemented and integration-proven; the only gap is fast route-level assertions. Every leg maps to an existing, verified precedent, so implementation is mechanical and self-contained, with no production-code or contract risk. Completing the caller-visible status-code matrix hardens the safe-denial invariant (401/403/404 indistinguishability) at the cheapest test tier and closes the last Story 8.1 review follow-ups.

**Dev-verify note (single low-risk unknown):** confirm the op7 retry-eligibility projection maps a missing workspace → `NotFoundSafe` (→404) and a throwing read model → `Unavailable` (→503) through the shared `WorkspaceLockStatusQueryHandler`; the sibling `GetWorkspaceLock` legs and the read-route `ToHttpResult` switch indicate it does.

---

## Section 4 — Detailed Change Proposals

> All additions are test-only. Test-method names are PascalCase per project testing standards. Assertions are metadata-only and must not echo requested folder/tenant/workspace/subject identifiers on any denial leg.

### ① `ListFolderAclEntries` route-level 200 shape
**File:** `tests/Hexalith.Folders.Server.Tests/FolderAclEndpointTests.cs`

- **Enrich `BuildApp`** to wire the authorized-caller authz chain (mirror `GetRepositoryBindingEndpointTests.BuildApp`): register `IFolderTenantAccessProjectionStore` (TenantStore with `user-a` as `Member` of `tenant-a`), `IEffectivePermissionsReadModel` (folder-a `read_metadata` evidence), `IEventStoreAuthorizationValidator → AllowingEventStoreAuthorizationValidator`, and `IUtcClock → FixedUtcClock`.
- **Seed folder state** through the existing seam:
  `InMemoryFolderRepository.Seed(repo.CreateStreamName("tenant-a","folder-a"), [FolderCreated…, FolderAccessGranted(principalKind: user, principalId: user-a, action: read_metadata)])`.

```
NEW  ListFolderAclEntriesShouldReturnContractShapedEntries
  GET /api/v1/folders/folder-a/acl   (authorized; X-Hexalith-Freshness: eventually_consistent)
  → 200 OK
     entry.aclEntryId    == FolderAclContract.DeriveAclEntryId("user","user-a","read")
     entry.subjectRef    == "user:user-a"
     entry.permissionLevel == "read"
     entry.effect        == "grant"
     freshness.readConsistency == "eventually_consistent"
     response header X-Hexalith-Freshness present == "eventually_consistent"
```
**Rationale:** pins the DD2 `aclEntryId` round-trip + entry shape at route level (previously proven only end-to-end).

### ② Authenticated 403 `denied_safe` (read ops)
**Files:** `FolderAclEndpointTests.cs` and `tests/Hexalith.Folders.Server.Tests/WorkspaceLockEndpointTests.cs`

Trigger: client tenant-header mismatch on an authenticated, otherwise-authorized caller (precedent `EffectivePermissionsRouteShouldUseSafeDenialEnvelopeForTenantMismatch`). Handler path: `TenantAccessDenied → AuthorizationDenied → 403 denied_safe` (`ListFolderAclEntriesQueryHandler.MapAuthorizationDenial`; read-route `ToHttpResult` `AuthorizationDenied/default → 403 denied_safe`).

```
NEW  ListFolderAclEntriesShouldUseSafeDenialForTenantMismatch          (FolderAclEndpointTests)
NEW  GetWorkspaceRetryEligibilityShouldUseSafeDenialForTenantMismatch  (WorkspaceLockEndpointTests)
  authorized caller (tenant-a / user-a) + X-Hexalith-Tenant-Id: tenant-secret-victim
  → 403 Forbidden
     category    == "tenant_access_denied"
     code        == "denied_safe"
     clientAction == "no_action"
     body ShouldNotContain the requested folder / tenant / workspace identifiers
```
**Rationale:** proves the authenticated-denied leg is caller-visibly indistinguishable from 401/404 for the two representative reads. No new denying-double required.

### ③ op7 `GetWorkspaceRetryEligibility` 404 & 503 read legs
**File:** `WorkspaceLockEndpointTests.cs`

```
NEW  GetWorkspaceRetryEligibilityShouldReturnSafeNotFoundForUnknownWorkspace
  seed IWorkspaceLockStatusReadModel with no matching workspace
  → 404 Not Found; category == "not_found"; body ShouldNotContain folder/workspace ids

NEW  GetWorkspaceRetryEligibilityShouldEmit503ForUnavailableReadModel
  throwing IWorkspaceLockStatusReadModel (mirror EffectivePermissions ThrowingReadModel)
  → 503 Service Unavailable; retryable safe envelope
```
**Rationale:** completes the read-op status-code matrix (200/401/403/404/503) for op7. **Dev-verify** the missing→`NotFoundSafe` and throw→`Unavailable` mapping (Section 3 note).

### ④ op3 `UpdateFolderAclEntry` route-level 404 (folder-not-found)
**File:** `FolderAclEndpointTests.cs`

Add a faulting gateway double raising `EventStoreGatewayException` carrying a not-found problem (HTTP 404) — precedent `ArchiveFolderEndpointTests.cs:138–148,523`. The mutating route's `catch (EventStoreGatewayException) → ToArchiveGatewayProblem` maps it to a safe 404.

```
NEW  UpdateFolderAclEntryShouldMapGatewayNotFoundToSafe404
  PUT /api/v1/folders/folder-a/acl/{aclEntryId}  with a faulting-404 gateway double
  → 404 Not Found; category/code == "not_found"
     body ShouldNotContain "folder-a" / "user-a"
```
**Rationale:** the single mutating-op leg in the MED bullet; asserts folder-not-found surfaces as a safe 404 at the route boundary.

### Validation (at implementation time)
- `dotnet build Hexalith.Folders.slnx` clean (warnings-as-errors).
- Run `Hexalith.Folders.Server.Tests` → expect ~+5 tests, all green.
- No regressions in `Hexalith.Folders.Tests`, `Hexalith.Folders.IntegrationTests`, `Hexalith.Folders.Contracts.Tests`.
- Contract Spine drift + C13 parity oracle unchanged (no new operations).

---

## Section 5 — Implementation Handoff

**Scope classification: Minor → Developer-agent direct implementation.**

| Role | Responsibility |
|---|---|
| **Developer (Amelia)** | Implement proposals ①–④ in the two `Server.Tests` files; enrich `FolderAclEndpointTests.BuildApp`; run the build + `Server.Tests`; update Story 8.1 File List + Change Log; flip `sprint-status.yaml:385` action item → `done`. |
| **Test Architect (Murat)** | Verify safe-denial no-echo assertions, the 401/403/404 indistinguishability claim, and the op7 read-model mapping (Section 3 dev-verify). |

**Success criteria:** four new tests green; full-solution build clean; no cross-lane regressions; parity oracle unchanged; action item closed; Story 8.1 tracking updated. Recommended execution vehicle: a focused `bmad-dev-story` (or `bmad-quick-dev`) run scoped to this proposal.

**Out of scope (intentionally):** the remaining Story 8.1 review follow-ups not in this action item — `[LOW]` Epic-5 `idempotency_key_not_accepted` reconciliation (separately tracked, `sprint-status.yaml:380` done) and `[LOW]` production EventStore-backed `IOrganizationProviderBindingRepository` conflict-arm. No PRD/architecture/spine edits.

---

## Approval

- Change trigger: **clear** (tracked action item + Story 8.1 review follow-ups).
- Impact analysis: **complete** (no epic/PRD/architecture/UX/spine conflict).
- Proposals ①–④: **approved as drafted** (Jerome, incremental review, 2026-07-08).
- Execution: **deferred** — proposal only; handed off to Developer agent.
