# Sprint Change Proposal — Story 4.19 prerequisites (2026-10-06)

**Status:** Accepted 2026-10-07 by Jerome (E1–E14; O1 not accepted) and applied. Decision note: `_bmad-output/implementation-artifacts/story-4-19-prerequisites-decision-2026-10-07.md`.
**Trigger:** Story 4.19 spec, frozen decisions 2 and 3 (`_bmad-output/implementation-artifacts/spec-4-19-prove-durable-workspace-prepare-and-lock-lifecycle.md`).
**Mode:** Batch. **Scope class:** Moderate (new backlog story plus dependency edges; no production code).

## 1. Issue summary

Story 4.19 is proof-only. Two things it must prove have no implementing owner, and the deployed lane it must run on is not yet a prerequisite.

**Orphaned requirements.** No story's Requirements line claims AR-CURRENT-15 (serializing lock identity) or AR-AUTHZ-04 (C7 `1.0.0` lock timing). Both appear only in the epics.md requirements inventory. Story 4.3 (done) and Story 2.4 (done) state the behavior in their ACs but never implemented it.

Verified code facts at `381a323`:

| Fact | Evidence |
| --- | --- |
| Lock instance ID and ownership proof hash tenant/folder/workspace/task, so collision is folder-scoped | `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs:446` (`DeriveWorkspaceLockId`), `:463` (`DeriveWorkspaceLockOwnershipProof`) |
| `FolderState` keeps `RepositoryBindingId` and `BranchRefPolicy` but no canonical repository identity | `src/Hexalith.Folders/Aggregates/Folder/FolderState.cs:34-39`; `CanonicalRepositoryId` exists only under `src/Hexalith.Folders/Providers/` (e.g. `Providers/Abstractions/ProviderRepositoryBindingResult.cs`) |
| Two folders on one repository/ref are separate streams, so cross-folder collision needs a separate tenant-scoped lock-identity record | `{managedTenantId}:folders:{folderId}` aggregate identity (AR-DOMAIN-02) |
| No renewal operation exists. Renewal appears only as description prose in both spines, and v2 is unrouted | `hexalith.folders.v1.yaml:8758`, `hexalith.folders.v2.yaml:20013`; lock operations are only `LockWorkspace`, `GetWorkspaceLock`, `ReleaseWorkspaceLock`, `GetLockDiagnostics`; manifest `v2_exposure_authorized: false`; `FoldersApiRoutingMode.V1Only` is the default hold |
| C7 itself defers the mechanism | `docs/exit-criteria/c7-lock-authorization-timing.md` § Deferred implementation: "does not implement a renewal endpoint, scheduler, authorization propagation, revocation handler…" |
| Nothing produces lock state `revoked` | `WorkspaceLockStatusQueryHandler.cs:128` derives only `expired` from the clock; no domain event yields `revoked` (only UI/generated enums name it) |
| `expired → stale` via `LockLeaseBecameStale` is already owned | Story 4.22 AC: "`LockLeaseBecameStale` is the only event that moves an expired lock to stale" |

**Lane gap.** `tests/Hexalith.Folders.AppHost.Tests/AspireFoldersAppHostFixture.cs` boots Development (in-memory). Story 11.15's AC names its lane as the honest evidence path for product Epics 4, 6, 10, and 12. Yet 4.18–4.21 do not depend on 11.15. Story 10.8 already does, which is the precedent.

## 2. Impact analysis

**Epic 4.** It can still complete as planned with one added story. 4.19 gains two prerequisites (4.23, 11.15), and 4.18, 4.20, and 4.21 gain one (11.15). No story is removed, renumbered, or reopened. 4.3 and 2.4 stay `done` as historical component evidence. The new story records that it owns their unimplemented runtime clauses.

**Other epics.** Epic 11 gains no new scope; 11.15 simply gains four dependents. Epic 12 is unchanged: 12.7 already names "lock identity" as a token consumer, and 12.4 already uses the 12.7 writer token.

**Execution order.**

| Node | Rank | Change |
| --- | --- | --- |
| 11.15 | 30 | Gains dependents 4.18, 4.19, 4.20, 4.21 (ranks 40–42, all strictly later) |
| **4.23 (new)** | **31** | Follows 1.17 (10), 4.22 (11), 12.1 (20), 12.2 (21), 12.6 (21), 12.7 (22), 11.15 (30) and accepted OQ1 (C7) |
| 4.19 | 41 | Gains 4.23 and 11.15 |

Critical-path effect: 4.18 now also waits for 11.15. 4.19 was already gated behind rank 40 (4.18), so 4.23 at rank 31 does not move it later.

**Artifact conflicts.**
- **PRD:** none. FR25–FR29, NFR7, and NFR21 already state the behavior, and PRD § lock semantics already says "only the owning task may renew". No requirement changes, and the hash-pinned NFR bullets are untouched.
- **Architecture:** no conflict. Line 1069 already says "the durable lock key, transition tests, and alias-collision parity evidence use this identity". **Action-needed (deferred to the 4.23 spec):** § EventStore write side (line 1669) lists only `folders` and `organizations` streams, so the tenant-scoped lock-identity record must be added there in the same change that introduces it. This pass does not edit architecture.md.
- **UX:** none. The console already maps `LockState.Revoked` (`ConsoleStatusText.cs:67`).
- **Governance and traceability:** `docs/exit-criteria/nfr-traceability.md` is untouched. NFR7 and NFR21 stay reference-pending until 4.23 and 4.19 evidence lands. C7 is unchanged, since 4.23 implements the approved values without changing them.
- **v2 candidate:** planning files are not in `generated-v2-conformance-set-2026-09-17.yaml`, so Story 1.17's bound bytes are unaffected.

## 3. Recommended approach — Direct adjustment

Add one story inside Epic 4 and add dependency edges. Rollback is not applicable: nothing completed is wrong, only incomplete. MVP review is not needed, because scope is unchanged and the work was always required. Only its owner was missing.

**Effort and risk.** The planning change itself is low effort and low risk. Story 4.23 is high effort: cross-aggregate serialization, a new v2 operation, and timing boundaries. That is why it is split into five slices like 4.22, 12.6, and 12.7.

### Decisions for Jerome

**D1 — Renewal needs a new contract operation: yes (recommended).** Add the v2-only mutation `RenewWorkspaceLock`. Neither existing path satisfies the PRD rule "only the owning task may renew the lease, under fresh authorization":
- Same-task re-acquire returns the existing instance unchanged (PRD A7, idempotent result), so it cannot move the C7 renewal anchor.
- Mutation-implied renewal (architecture C6 rows `locked → changes_staged`) leaves a task between mutations with no way to keep its lease.

*Rejected alternative:* make re-acquire extend the lease. That overloads `LockWorkspace` (the same key would return different leases), conflates acquisition and renewal in audit, and contradicts A7.

**v2 exposure gating:**
- The operation is added only to the v2 spine, after Story 1.17 closes, so it cannot perturb the A6b-bound candidate that 1.17 carries.
- It is regenerated in lockstep:
  - server, SDK, previous-spine fingerprints, C13 cells, and the parity oracle;
  - its OQ3 v2 authorization-matrix row;
  - its Story 12.6 admission descriptor;
  - the `Hexalith.McpCli` operation inventory (per the 2026-09-27 McpCli correction; the obsolete Folders CLI/MCP adapters are not extended).
- It inherits v2 exposure: unreachable while `v2_exposure_authorized` is false or the server runs `V1Only`.
- There is no v1 backport, unversioned route, or flag-gated exception.
- 4.23 stays open until renewal is proven on the deployed v2 route.
- The new matrix row and C13 cells are behavior changes, so they need your decision when the 4.23 spec is approved. Historical A6b approval remains provenance for its bytes.

**D2 — 4.23 needs 4.22: yes.** Revocation must feed 4.22's authorization-loss branch so staged work is preserved, not released. The effective stale threshold feeds 4.22's `LockLeaseBecameStale`, and 4.23 adds no second stale path. Building either before 4.22 would mean coding against the superseded C6 model.

**D3 — Prerequisites beyond your brief** (12.1 and 12.7 were given):
- **1.17:** owns v2 spine regeneration (D1).
- **12.2:** lock projections become durable there, so `revoked` and renewal anchors are visible on inspect after restart.
- **12.6:** renewal is a new mutation and must use the EventStore admission contract (AR-CURRENT-16).
- **11.15:** renewal and revalidation are new positive capabilities that no other story proves deployed (AR-CURRENT-22). 4.19 proves collision, expiry/stale, and revocation end to end; 4.23 proves renewal and revalidation on the lane.
- **accepted:OQ1:** the C7 `1.0.0` decision.

This puts 4.23 at **rank 31** (rank 30 is taken by its prerequisite 11.15).

**D4 — Tracker digest.** Inserting one key changes `sprint-status.yaml` from SHA-256 `230889ef…`, which the A8 finalization records bind as historical provenance. Those records are not rewritten. A new `planning_amendments` entry in the manifest records the old and new digests. No regeneration occurs, and `sprint_status_regeneration_authorized` stays false.

## 4. Detailed change proposals

### E1 — epics.md: insert Story 4.23 after Story 4.22 (before `## Epic 5`)

```markdown
### Story 4.23: Enforce canonical lock identity and C7 lock timing

**Requirements:** FR25, FR27–FR28; NFR7, NFR21; AR-CURRENT-15, AR-AUTHZ-04

As a developer or AI agent holding a workspace lock,
I want writers serialized on the canonical repository target and my lease governed by the approved C7 timing,
So that aliased folders cannot write the same remote ref concurrently and no lock outlives its holder's authority.

**Acceptance Criteria:**

**Given** two folders, workspaces, or bindings in one managed tenant resolve to the same canonical provider/repository identity and normalized target ref, directly or through an alias
**When** a second task acquires a lock or mutates while the first lock instance is `locked`, `expired`, or `stale`
**Then** it receives the canonical lock-conflict result before any file, provider, repository, or commit effect, with one metadata-only audit record and no takeover
**And** the serializing identity is managed tenant plus canonical provider/repository identity plus normalized target-ref token; folder, workspace, and task IDs are lock metadata only, and an identical target in another tenant never collides.

**Given** folders on the same target are separate aggregate streams
**When** the serializing identity is enforced
**Then** one tenant-scoped durable lock-identity record, written only through EventStore, holds the active instance, owner task, lease, and fencing information across folders, restart, and supported replicas
**And** lock-instance IDs and ownership proofs may keep instance metadata, but collision is decided only by the serializing identity, never by a folder-scoped lock ID.

**Given** a repository binding
**When** its serializing identity is derived
**Then** the binding durably carries the canonical provider/repository identity through a versioned event payload under the Story 12.1 event-evolution rules, and confidential repository or ref values enter the identity only as the Story 12.7 canonical token
**And** a binding without a durable canonical identity fails closed with a canonical result instead of acquiring a lock under a weaker identity.

**Given** the approved C7 `1.0.0` profile (30-second renewal, 15-second revalidation, 60-second revocation effect, 60-second expired-to-stale) and any tenant override
**When** the effective profile is resolved
**Then** an override may only lower a value to a positive whole number of seconds, and a zero, negative, larger, or incomplete override never becomes active
**And** the effective expired-to-stale threshold is the value Story 4.22's `LockLeaseBecameStale` transition consumes; this story adds no other stale path.

**Given** the owning task holds a `locked` lease
**When** it calls the v2 `RenewWorkspaceLock` mutation with its task identity, ownership proof, and an idempotency key under fresh authorization
**Then** renewal is due at `now >= renewalAnchorAt + effective interval`, a successful renewal moves the anchor to its own `effectiveAt` and durably records the new expiry, and expiry takes precedence: no lease is renewed at or after `expiresAt`, and a lease shorter than the effective interval keeps its requested expiry
**And** a non-owner, wrong-tenant, locator-only, stale-authority, or revoked caller receives the canonical safe result with no lease change.

**Given** renewal is a new Contract Spine operation
**When** it is added
**Then** it exists only in the v2 spine, is added after Story 1.17 closes, and is regenerated in lockstep with the server, SDK, previous-spine fingerprints, C13 cells, parity oracle, OQ3 v2 authorization-matrix row, Story 12.6 admission descriptor, and the `Hexalith.McpCli` operation inventory
**And** it inherits v2 exposure gating: it is unreachable while v2 exposure is unauthorized, with no v1 route, unversioned route, or flag-gated exception.

**Given** a held lock
**When** 15 seconds have passed since the last successful authorization validation, or a renewal or mutation is requested
**Then** current tenant, folder ACL, delegated-actor, binding, and credential authority is revalidated, and stale, unavailable, or unknown authority fails closed for renewal and protected work
**And** after an authoritative revocation the instance is `revoked` no later than `revocationEffectiveAt + 60 seconds`, measured from the upstream timestamp rather than local receipt; later protected work is denied before any side effect, and the instance never reactivates, so recovery creates a new instance under the unchanged identity
**And** the revocation reaches Story 4.22's authorization-loss branch, so staged work is preserved rather than released or discarded.

**Given** Stories 4.3 and 2.4 are done but never produced the canonical identity or the `revoked` state
**When** their evidence is read
**Then** it remains historical component evidence and this story owns the runtime behavior
**And** Story 4.19 owns the end-to-end deployed prepare/lock lifecycle proof.

**Given** completion is evaluated
**When** evidence is attached
**Then** inclusive boundary tests cover the renewal-due, expiry, stale, revalidation, and revocation-SLO instants; alias collision, cross-tenant non-collision, multi-replica identity races, restart, and empty-checkpoint replay are proven against durable state; and renewal and revalidation are proven on the Story 11.15 lane
**And** audit, telemetry, and diagnostics stay metadata-only, with no ownership proof, raw repository or ref, or confidential cleartext, and NoOp, in-memory, seed, unavailable, safe-empty, mocked-gateway, or fake-only evidence cannot satisfy completion.

**Given** the execution manifest
**When** Story 4.23 is scheduled
**Then** it follows Stories 1.17, 4.22, 12.1, 12.2, 12.6, 12.7, and 11.15 plus accepted OQ1 (C7 `1.0.0`) and precedes Story 4.19
**And** every dependency uses a strictly lower rank unless it is accepted terminal with evidence.

**Execution model:** Story 4.23 owns AR-CURRENT-15 and AR-AUTHZ-04. Each slice is independently reviewable and testable; the parent closes only when every slice and the deployed renewal evidence agree.

**Single-session slices:**

- `4.23-A` — Carry the canonical provider/repository identity on bindings and derive the tenant-keyed serializing token.
- `4.23-B` — Enforce the durable lock-identity record, alias collision, and fencing across folders and replicas.
- `4.23-C` — Resolve the effective C7 profile and implement owner-only renewal through the v2 `RenewWorkspaceLock` operation.
- `4.23-D` — Implement 15-second revalidation and the revocation producer within the 60-second SLO.
- `4.23-E` — Prove boundary timing, collision, replay, multi-replica, metadata-only, and lane evidence.
```

### E2 — epics.md, Epic 4 header: new paragraph after the 2026-08-04 ownership note (line 1549)

```markdown
_**Course correction (2026-10-06):** Story 4.23 owns the canonical lock identity and C7 lock timing (AR-CURRENT-15, AR-AUTHZ-04) that Story 4.19 proves. Stories 4.18–4.21 record deployed evidence on the shared Story 11.15 lane rather than a private production-mode fixture._
```

### E3–E6 — epics.md, first `Given` of Stories 4.18–4.21

| ID | Story | OLD | NEW |
| --- | --- | --- | --- |
| E3 | 4.18 | `**Given** Stories 12.1–12.2 supply durable ordered events/projection substrate and Story 4.22 supplies the reapproved total guarded lifecycle model` | `**Given** Stories 12.1–12.2 supply durable ordered events/projection substrate, Story 4.22 supplies the reapproved total guarded lifecycle model, and Story 11.15 supplies the DCP-capable verification lane` |
| E4 | 4.19 | `**Given** Stories 4.18, 12.1–12.3, 12.6, and 4.22 provide transition evidence, durable state/content/idempotency, and the reapproved C6 guarded lifecycle` | `**Given** Stories 4.18, 12.1–12.3, 12.6, 4.22, and 4.23 provide transition evidence, durable state/content/idempotency, the reapproved C6 guarded lifecycle, and the canonical lock identity with C7 timing, and Story 11.15 supplies the DCP-capable verification lane` |
| E5 | 4.20 | `…the confidential boundary, and v2 authorization; OQ2 file-policy…` | `…the confidential boundary, and v2 authorization; Story 11.15 supplies the DCP-capable verification lane; OQ2 file-policy…` |
| E6 | 4.21 | `**Given** Story 12.4 plus Stories 4.19, 4.20, and 4.22 provide the real Git path, durable lifecycle proof, and governing guarded transition model` | `…governing guarded transition model, and Story 11.15 supplies the DCP-capable verification lane` |

### E7–E12 — planning-story-manifest.yaml (lockstep with E1–E6)

- **E7 `stories`.**
  - Insert row `'4.23'` after `'4.22'`:
    - `classification: product`
    - `authoritative_definition: reconciled-epics-anchor:…/epics.md#Story-4.23`
    - `prerequisites: [DEC-A8-HOLD, accepted:OQ1, '1.17', '4.22', '12.1', '12.2', '12.6', '12.7', '11.15']`
    - `requirement_clauses: [FR25, FR27, FR28, NFR7, NFR21, AR-CURRENT-15, AR-AUTHZ-04]`
    - `evidence_posture: open-real-production-evidence-required`
    - `story_lifecycle_status: backlog`, `execution_rank: 31`, `decision_status: not-applicable`
    - `execution_authorization_status: held`, `delivery_evidence_status: open`
  - Add `'11.15'` to the prerequisites of 4.18, 4.19, 4.20, and 4.21.
  - Add `'4.23'` to the prerequisites of 4.19.
- **E8 `exact_prerequisites`.** Mirror E7 exactly. Add a `'4.23'` entry after `'11.21'`, the last rank-31 entry.
- **E9 `execution_waves`.** Add `'4.23'` to rank 31.
- **E10 `requirement_inventory.functional.completion_owners`.** Add `'4.23'` under FR25, FR27, and FR28.
- **E11 `generated_counts` and rule V2-003:**

  | Count | Before → after |
  | --- | --- |
  | canonical story identities | 158 → 159 |
  | inventory rows | 159 → 160 |
  | product | 86 → 87 |
  | backlog (both lifecycle maps) | 44 → 45 |
  | reconciled-epics-anchor | 47 → 48 |
  | scheduled nonterminal | 47 → 48 |

  V2-003 text changes to "159 canonical story identities and 160 inventory rows". The dated 2026-09-21 `validation_results` block stays as history.
- **E12 `planning_amendments` (new block before `stories`).** One entry recording:
  - the date, Jerome as decision maker, the policy, and this proposal;
  - the decision note;
  - the changes;
  - the tracker digest before and after;
  - validation results;
  - an explicit list of what is unchanged: the hold, exposure, regeneration and execution flags, and every approval/provenance digest.

### E13 — sprint-status.yaml

Insert one line after `4-22-implement-the-pd11-guard-discriminated-lifecycle: backlog`:

```yaml
  4-23-enforce-canonical-lock-identity-and-c7-lock-timing: backlog
```

Nothing else changes: no header, journal, or epic status edit.

### E14 — spec-4-19 Code Map (non-frozen; lines 51–52 only)

- Line 51:
  - The prerequisite list becomes "plus 4.18, 4.22, 4.23, 11.15, 12.1–12.3, and 12.6".
  - The last sentence becomes "Story 4.23 and the 11.15 edge were added by the 2026-10-06 correct-course (decisions 2–3; `sprint-change-proposal-2026-10-06.md`)."
- Line 52: "owned by the new story" becomes "owned by Story 4.23".
- The frozen block, status (`draft`), and Tasks are unchanged.

### O1 (optional, outside your listed scope) — epic-4-context.md

Add "Story 4.23: Enforce canonical lock identity and C7 lock timing" to the story list. Add one sentence to Cross-Story Dependencies: "4.23 follows 1.17, 4.22, 12.1–12.2, 12.6–12.7, and 11.15 and precedes 4.19; 4.18–4.21 run on the 11.15 lane." The file says "Edit freely". **Default: skip unless you approve it.**

## 5. Implementation handoff

- **Route:** Product Owner/Developer (moderate). Jerome decides; the agent applies E1–E14, plus O1 only if approved, and runs the checks.
- **Checks before reporting done:**
  - `git diff --check`;
  - manifest graph validation (unique IDs, counts, prerequisite existence, strictly lower rank, acyclic, wave/rank consistency);
  - `NfrTraceabilityConformanceTests`, which reads epics.md;
  - `GovernanceCompletenessGateTests`, which reads the manifest;
  - the `ScaffoldContractTests` inventory pins (they don't read these files, but stay green as a regression guard).
- **Success criteria:**
  - every edit is in lockstep across epics.md and the manifest;
  - the graph is acyclic with 73 ranked nodes;
  - the hash-pinned NFR bullets are byte-identical;
  - the 4.18/4.19 frozen blocks are byte-identical;
  - the single tracker key is added;
  - the decision note is recorded.
- **After this:**
  - `/bmad-build 4.19` stays queued until 4.18, 4.22, 4.23, 11.15, 12.1–12.3, and 12.6 land.
  - The next link in the chain is 12.1. It still waits on the acceptance record at `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/ext-es-event-evolution-v1.yaml` (absent on 2026-10-06) and on DEC-EXEC-12.1.
  - 4.23 needs its own spec (`bmad-build 4.23`) once its prerequisites approach. That spec must add the lock-identity stream to architecture.md § EventStore write side.

## Checklist record

| Item | Status | Note |
| --- | --- | --- |
| 1.1–1.3 Trigger, problem, evidence | [x] | 4.19 decisions 2–3; requirement owner missing (misunderstanding: 4.3/2.4 claimed it); code facts verified |
| 2.1–2.5 Epic impact | [x] | Epic 4 +1 story; 11.15 gains dependents; no epic added, removed, or resequenced; 4.23 at rank 31 |
| 3.1 PRD | [N/A] | No requirement change |
| 3.2 Architecture | [!] | Lock-identity stream must be added to § EventStore write side by the 4.23 spec |
| 3.3 UX | [N/A] | `revoked` already rendered |
| 3.4 Other artifacts | [x] | Tracker digest (D4); nfr-traceability untouched; v2 conformance set unaffected; epic-4-context optional (O1) |
| 4.1 Direct adjustment | Viable | Low planning effort and risk; 4.23 itself high effort |
| 4.2 Rollback | Not viable | Nothing completed is wrong |
| 4.3 MVP review | Not viable | Scope unchanged |
| 4.4 Selected path | [x] | Direct adjustment |
| 5.1–5.5 Proposal components | [x] | Sections 1–5 |
| 6.1–6.2 Review | [x] | Lockstep and rank checks planned |
| 6.3 Approval | [x] | Jerome, 2026-10-07: "yes" (E1–E14; O1 skipped) |
| 6.4 sprint-status | [x] | E13 applied: one key; SHA-256 `230889ef…` → `cc964658…` |
| 6.5 Handoff | [x] | Section 5 |
