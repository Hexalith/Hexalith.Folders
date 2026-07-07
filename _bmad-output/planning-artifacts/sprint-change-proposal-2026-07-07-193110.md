# Sprint Change Proposal — EventStore Gateway Test-Double Rejection-Propagation Guard

- **Date:** 2026-07-07
- **Author:** Amelia (Developer) via `bmad-correct-course`
- **Change mode:** Batch
- **Trigger:** Epic 8 (Story 8.3) retrospective action item, currently `open` and unscheduled in `sprint-status.yaml`
- **Scope classification:** Moderate (backlog reorganization — extend a story's ACs, re-home an action item, consolidate duplicated test doubles; no product/wire behavior change)
- **Recommended path:** Direct Adjustment — extend **Epic 11 Story 11.7** (Consolidate test helpers into `Hexalith.Folders.Testing`)

---

## Section 1 — Issue Summary

### Problem statement

There is **no automated guard** that prevents a contributor from re-introducing an in-process `IEventStoreGatewayClient` test double that **flattens domain rejections** — i.e., unconditionally returns an accepted `SubmitCommandResponse` without ever consulting `DomainServiceWireResult.IsRejection` or preserving the canonical `FolderResultCode` reason code. Such doubles manufacture false-green parity/acceptance evidence: an endpoint or parity test can be green while the real `REST → gateway → /process → IDomainProcessor → gate → persistence` path is broken or silently drops rejection identity.

### How it was discovered

This is the **Epic 8 / Story 8.3** discovery, recorded verbatim in `epic-8-retro-2026-06-27.md`:

> "Story 8.3 corrected a high-risk test double problem. Rejection-flattening gateway stubs had been manufacturing false-green parity. The shared rejection-propagating gateway made canonical 409, safe denial, and gateway-hop ACL rejection behavior visible."
>
> "Wire parity is not proven if the in-process gateway test double discards rejection identity."

Story 8.3 fixed the *parity* tests but the retrospective explicitly logged a **high-priority follow-up** to make the fix durable via a guard (owner Amelia / Murat). Completion signal:

> **"No flattening gateway copies exist; the shared propagating helper is the approved pattern."**

That action item is still `open` in `sprint-status.yaml` (Epic 8 block). The lineage traces back to the **Story 2.8 / 2.8b "green tests, broken production wiring" trap**, now a standing rule in `project-context.md:91`.

### Evidence (verified against HEAD source)

| Fact | Location |
| --- | --- |
| **9 copy-pasted flattening doubles** — `RecordingEventStoreGatewayClient`, each returns `new SubmitCommandResponse(...)` success unconditionally, never inspects a rejection | `tests/Hexalith.Folders.Server.Tests/`: `CreateFolderEndpointTests.cs:238`, `ArchiveFolderEndpointTests.cs:515`, `BranchRefPolicyEndpointTests.cs:493`, `ConfigureProviderBindingEndpointTests.cs:244`, `FolderAclEndpointTests.cs:257`, `WorkspaceLockEndpointTests.cs:1494`, `MutationEnvelopeEndpointMatrixTests.cs:349`, `RepositoryBackedFolderEndpointTests.cs:1452`, `TransportParityConformanceTests.cs:637` |
| **The one rejection-propagating double** — round-trips `/process`, reads back `DomainServiceWireResult`, and on `IsRejection` throws `EventStoreGatewayException` carrying HTTP status + the `FolderResultCode` name as `reasonCode` | `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs:41` (namespace `Hexalith.Folders.Parity.Testing`) — `<Compile Include>`-linked **only** into `tests/Hexalith.Folders.IntegrationTests` |
| **`src/Hexalith.Folders.Testing` contains no gateway double** — the propagating helper is not on the shared surface | — |
| **No automated guard** scans test-double source to require rejection propagation, forbid flattening copies, or assert canonical reason-code propagation | Confirmed absent |
| **`DomainServiceWireResult`** — `record(bool IsRejection, IReadOnlyList<DomainServiceWireEvent> Events, string? ResultPayload)`; the reason code lives **inside** the serialized rejection event payload as a `code` JSON property, not as a wire field | `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Results/DomainServiceWireResult.cs:14` |
| **Canonical reason codes** = `FolderResultCode` enum (57 members, JSON string-enum names on the wire: `IdempotencyConflict`, `DuplicateFolder`, `TenantAccessDenied`, `FolderAclDenied`, `StateTransitionInvalid`, …) | `src/Hexalith.Folders/Aggregates/Folder/FolderResultCode.cs:10` |
| **Existing guard idiom to reuse** — repo-root source/XML file-scan, walk up to `Hexalith.Folders.slnx`, allow-lists, no reflection | `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` (`RepositoryRoot()`:470); sibling patterns in `ForgejoDependencyGuardTests.cs`, `DaprPolicyConformanceTests.cs` |

### Important nuance — two legitimate non-propagating doubles must NOT be flagged

- `ThrowingEventStoreGatewayClient` (`ConfigureProviderBindingEndpointTests.cs:219`) throws `InvalidOperationException` to simulate an **infrastructure failure** (no reason code by design).
- `UnsupportedGatewayClient` (`WorkspaceStatusEndpointTests.cs:721`, `WorkspaceCleanupStatusEndpointTests.cs:496`) throws `NotSupportedException` on every method — a **read-only endpoint** double that must never call the gateway.

Both are correct negative-path/never-called doubles. The guard must allowlist them explicitly, not treat "does not propagate a rejection" as automatically illegal.

---

## Section 2 — Impact Analysis

### Epic impact

- **Epic 11 (in-progress)** — the natural and only affected epic. Story 11.7's charter is *"Consolidate test helpers into `Hexalith.Folders.Testing`"* and its current AC already lists *"gateway clients"* among the duplicated fakes to consolidate. This change **extends** Story 11.7; it does not add or resequence an epic. No other epic is affected.
- **Epic 8 (done)** — unchanged as a delivered epic; only its lingering retro **action item** is re-homed/scheduled (status `open → in-progress`, annotated as delivered by Story 11.7). No re-opening of Epic 8.

### Story impact

- **Story 11.7** — receives **two added acceptance criteria** (consolidation + guard). Still `backlog`; no status change from this proposal (it is sequenced within Epic 11's normal flow).
- No other Epic 11 story changes. Story 11.4 (Server transport consolidation) and Story 11.10 (Server/Workers EventStore SDK seams) touch adjacent Server code but neither owns the gateway *test doubles*; 11.7 is the correct owner.

### Artifact conflict analysis

| Artifact | Impact | Action |
| --- | --- | --- |
| **PRD** (`prd.md`) | None. No product FR change; supports existing NFRs for parity, maintainability, traceability. | No edit |
| **Architecture** (`architecture.md`) | Optional. A one-line note in the testing/verification narrative could name the guard, but doc sync is owned by Story 11.13. | Deferred to 11.13 |
| **UX** (`ux-design-specification.md`) | None. | No edit |
| **Epics** (`epics.md`) | Story 11.7 AC extension. | **Edit now** |
| **Sprint status** (`sprint-status.yaml`) | Re-home the Epic 8 action item; update `last_updated`. | **Edit now** |
| **Project context** (`project-context.md:91`) | The standing 2.8/2.8b rule should later name the canonical helper + guard. | **Implementation-time edit** (part of Story 11.7 dev) |

### Technical impact

- **Test infrastructure only** — no production `src/` behavior, no REST/OpenAPI/parity-oracle/CloudEvent/wire changes. Fully consistent with Epic 11's "no product behavior or public wire contract change" constraint.
- **Test-count discipline** — consolidating 9 identical copies into one shared double must keep the same assertions; per Story 11.7's own rule, "test counts only change when a deleted local implementation no longer needs local re-testing."
- **Offline-safe** — the guard is a source-text scan (walk to `.slnx`, glob `tests/**/*.cs`), needs no submodule build, no Dapr/Redis/provider credentials; it runs in the same hermetic lanes as `ScaffoldContractTests`.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment** (extend Story 11.7). Options 2 (rollback) and 3 (MVP review) are **not viable / not applicable**: nothing needs reverting, and the MVP is already delivered — this is governance-closure hardening.

**Rationale:** Story 11.7 is purpose-built for exactly this ("consolidate duplicated fakes … gateway clients … into `Hexalith.Folders.Testing`"). Homing the guard here means the shared propagating helper becomes the *approved pattern* as a side effect of the consolidation the story already intends, and the guard makes the completion signal ("no flattening copies exist") enforceable rather than aspirational. Effort **Low–Medium**; risk **Low** (test-only, offline, wire-preserving).

### Guard design (recommended contract)

An **allowlist source-scan guard** (robust; mirrors `ScaffoldContractTests`) rather than parsing method bodies (brittle):

1. **Consolidate** `InProcessRejectionPropagatingGatewayClient` onto the shared `Hexalith.Folders.Testing` surface as the **single canonical acceptance-path `IEventStoreGatewayClient` double**, and dedup the 9 `RecordingEventStoreGatewayClient` copies into **one** shared request-recording double, documented as *request-shape evidence only — never acceptance or parity evidence*.
2. **Guard** `EventStoreGatewayDoubleConformanceTests` in `tests/Hexalith.Folders.Testing.Tests`: scan `tests/**/*.cs` for type declarations implementing `IEventStoreGatewayClient`; **fail** if any is outside the approved allowlist —
   - the canonical rejection-propagating double,
   - the single shared recording double,
   - the named `ThrowingEventStoreGatewayClient` / `UnsupportedGatewayClient` negative-path doubles.
   A new ad-hoc double fails with a message pointing to the canonical helper. This is the "reject new flattening copies" mechanism.
3. **Positive behavioral test** proving the canonical double, when `/process` yields `DomainServiceWireResult.IsRejection = true` carrying a `FolderResultCode` (e.g. `FolderAclDenied`), throws `EventStoreGatewayException` with that canonical reason code — so the approved helper itself cannot silently regress to flattening.

**Noted alternatives (for the implementer, not blockers):**
- *Guard host* — `Hexalith.Folders.Testing.Tests` (recommended, co-located with `ScaffoldContractTests`) vs `Hexalith.Folders.Contracts.Tests` (if it must run inside the `submodules:false` contract-spine CI gate). Either works; the scan is source-text only.
- *Grandfather-only fallback* — if deduping all 9 copies risks churn against other in-flight Story 11.4/11.10 edits, ship the guard first with the 9 existing copies allowlisted and consolidate in a follow-up. The recommended path is full consolidation, since that is 11.7's charter and satisfies the retro completion signal literally.

---

## Section 4 — Detailed Change Proposals

### Change 4.1 — `epics.md`, Story 11.7 (applied on approval)

**File:** `_bmad-output/planning-artifacts/epics.md` (Story 11.7 Acceptance Criteria)

**OLD:**
```
**Acceptance Criteria:**

**Given** duplicated `FixedTimeProvider`, static tenant/claim context accessors, gateway clients, recording providers, repository helpers, and root walkers exist
**When** canonical helpers are added to `Hexalith.Folders.Testing`
**Then** test projects consume those helpers, platform fakes are adopted where available, and test counts only change when a deleted local implementation no longer needs local re-testing.
```

**NEW:**
```
**Acceptance Criteria:**

**Given** duplicated `FixedTimeProvider`, static tenant/claim context accessors, gateway clients, recording providers, repository helpers, and root walkers exist
**When** canonical helpers are added to `Hexalith.Folders.Testing`
**Then** test projects consume those helpers, platform fakes are adopted where available, and test counts only change when a deleted local implementation no longer needs local re-testing
**And** the rejection-propagating in-process gateway double (`InProcessRejectionPropagatingGatewayClient`, currently linked only into `Hexalith.Folders.IntegrationTests` from `tests/shared/Parity/`) becomes the single canonical acceptance-path `IEventStoreGatewayClient` double on the shared `Hexalith.Folders.Testing` surface, and the nine copy-pasted `RecordingEventStoreGatewayClient` flattening doubles in `Hexalith.Folders.Server.Tests` are consolidated into one shared request-recording double documented as request-shape evidence only, never acceptance or parity evidence.

**Given** the Epic 8 (Story 8.3) retrospective action item to guard against rejection-flattening gateway doubles, and the standing 2.8/2.8b "green tests, broken production wiring" rule in `project-context.md`
**When** the test-double consolidation lands
**Then** an automated conformance guard (e.g. `EventStoreGatewayDoubleConformanceTests` in `tests/Hexalith.Folders.Testing.Tests`, following the `ScaffoldContractTests` repo-root source-scan idiom) fails the build when any test source declares an `IEventStoreGatewayClient` double outside the approved allowlist — the canonical rejection-propagating double, the single shared recording double, and the named `ThrowingEventStoreGatewayClient` / `UnsupportedGatewayClient` negative-path doubles — so new ad-hoc flattening copies cannot be introduced
**And** a positive behavioral test proves the canonical double propagates a `DomainServiceWireResult` rejection as an `EventStoreGatewayException` carrying the canonical `FolderResultCode` reason code (e.g. `FolderAclDenied`) rather than flattening it to an accepted response.
```

**Rationale:** Makes the retro completion signal an enforceable Story 11.7 deliverable while preserving the story's existing consolidation intent; names concrete types so the dev/review agents have unambiguous targets.

### Change 4.2 — `sprint-status.yaml`, Epic 8 action item (applied on approval)

**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`

**OLD:**
```
  - epic: 8
    action: "Add or extend a guard that rejects new in-process EventStore gateway test doubles which do not propagate DomainServiceWireResult rejections with canonical reason codes."
    owner: "Amelia / Murat"
    priority: high
    status: open
```

**NEW:**
```
  - epic: 8
    # Re-homed 2026-07-07 via bmad-correct-course (sprint-change-proposal-2026-07-07-193110): scheduled into
    # Epic 11 Story 11.7 (test-helper consolidation) with two added acceptance criteria — (1) consolidate the
    # rejection-propagating InProcessRejectionPropagatingGatewayClient onto the shared Hexalith.Folders.Testing
    # surface as the single canonical acceptance-path double + dedup the nine RecordingEventStoreGatewayClient
    # flattening copies in Server.Tests into one documented request-recording double; (2) add the
    # EventStoreGatewayDoubleConformanceTests allowlist guard (ScaffoldContractTests source-scan idiom) that
    # rejects new ad-hoc IEventStoreGatewayClient doubles + a positive test proving canonical FolderResultCode
    # rejection propagation. Completion signal (no flattening copies exist; shared propagating helper is the
    # approved pattern) is delivered when Story 11.7 reaches done.
    action: "Add or extend a guard that rejects new in-process EventStore gateway test doubles which do not propagate DomainServiceWireResult rejections with canonical reason codes. RE-HOMED to Epic 11 Story 11.7 (two ACs added)."
    owner: "Amelia / Murat"
    priority: high
    status: in-progress
```

Plus update the header `last_updated` (lines 2 and 38) to:
```
# last_updated: 2026-07-07 (Epic 8 gateway-double guard action item re-homed to Epic 11 Story 11.7 via bmad-correct-course sprint-change-proposal-2026-07-07-193110; two ACs added to 11.7)
```

**Rationale:** Converts a stale `open` action item into scheduled, traceable work without pretending it is delivered.

### Change 4.3 — Implementation-time edits (executed when Story 11.7 is dev'd — NOT applied by this proposal)

1. **Promote** `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` onto the shared `Hexalith.Folders.Testing` surface (or a shared-compile-link consumable by both `IntegrationTests` and `Server.Tests`); keep the real `/process` round-trip + `EventStoreGatewayException(reasonCode = FolderResultCode)` propagation.
2. **Consolidate** the 9 `RecordingEventStoreGatewayClient` copies into one shared recording double in `Hexalith.Folders.Testing`, XML-documented as *request-shape evidence only — not acceptance/parity evidence*.
3. **Add** `EventStoreGatewayDoubleConformanceTests` (guard + positive behavioral test) per Section 3.
4. **Update** `project-context.md:91` to name the canonical propagating helper and the guard as the approved pattern, extending the existing 2.8/2.8b rule.

---

## Section 5 — Implementation Handoff

- **Scope classification:** **Moderate** → route to **Product Owner / Developer**.
- **Now (this proposal, on approval):** apply Change 4.1 (`epics.md`) and Change 4.2 (`sprint-status.yaml`).
- **Implementation (Story 11.7 execution):** **Amelia (Developer)** builds the consolidation + guard (Change 4.3); **Murat (Test Architect)** validates the guard's allowlist, the negative-path exceptions, and the positive canonical-reason-code propagation test.
- **Sequencing:** within Epic 11's normal flow. If Story 11.4/11.10 Server refactors are in flight, coordinate the `Server.Tests` double consolidation to avoid churn (grandfather-only fallback available per Section 3).

### Success criteria

1. Exactly one canonical rejection-propagating `IEventStoreGatewayClient` double exists on the shared `Hexalith.Folders.Testing` surface; no per-file propagating copies.
2. At most one shared recording double, documented as non-acceptance evidence; the 9 ad-hoc copies are gone.
3. `EventStoreGatewayDoubleConformanceTests` fails when a new unapproved/flattening double is added, and passes for the current approved allowlist (including `ThrowingEventStoreGatewayClient` / `UnsupportedGatewayClient`).
4. The positive test proves canonical `FolderResultCode` rejection propagation (not flattening).
5. Retro completion signal satisfied: **"No flattening gateway copies exist; the shared propagating helper is the approved pattern."**
6. No REST/OpenAPI/parity/wire behavior change; test counts change only per Story 11.7's rule.

---

## Appendix — Change Navigation Checklist results

| # | Item | Status | Note |
| --- | --- | --- | --- |
| 1.1 | Triggering story identified | Done | Story 8.3 / Epic 8 retro action item; lineage to 2.8/2.8b |
| 1.2 | Core problem defined | Done | Technical limitation — no guard against rejection-flattening gateway doubles |
| 1.3 | Evidence gathered | Done | 9 flattening copies + 1 mislocated propagating helper + no guard, all cited |
| 2.1 | Current epic still completable | Done | Yes — Epic 11 absorbs via Story 11.7 |
| 2.2 | Epic-level change | Done | Modify Story 11.7 scope (ACs); no new/removed epic |
| 2.3–2.5 | Other/ future epics, order, priority | N/A | No downstream epic impact or resequencing |
| 3.1 | PRD conflict | N/A | No FR change; supports existing NFRs |
| 3.2 | Architecture conflict | Action-needed (deferred) | Optional testing-narrative note owned by Story 11.13 |
| 3.3 | UX conflict | N/A | — |
| 3.4 | Other artifacts | Done | project-context.md rule update at 11.7 implementation time |
| 4.1 | Option 1 Direct Adjustment | Viable | Effort Low–Medium, Risk Low — **selected** |
| 4.2 | Option 2 Rollback | Not viable | Nothing to revert |
| 4.3 | Option 3 MVP review | Not viable | MVP delivered; governance-closure hardening |
| 4.4 | Path selected | Done | Option 1 — extend Story 11.7 |
| 5.1–5.5 | Proposal components | Done | Sections 1–5 above |
| 6.1–6.5 | Final review / handoff / status update | In progress | Pending user approval, then apply 4.1 & 4.2 |
