---
title: 'Sprint Change Proposal: run-owned workload-binding authority for the Story 12.2 strict fixture'
date: '2026-11-13'
author: Claude (correct-course, batch mode)
decision_maker: Jerome
status: approved
approval: 'Jerome, 2026-11-13, "/bmad-correct-course apply recommended" after reviewing the option analysis (run-owned authority vs. fixed-identity owner enrollment)'
scope_classification: moderate
repositories:
  - Hexalith.Folders (this repository)
  - Hexalith.Platform (references/Hexalith.Platform, observed at 32643bde39b4da0e4538f8388e225727f82122f6)
---

# Sprint Change Proposal — run-owned workload-binding authority (2026-11-13)

## 1. Issue summary

**Trigger:** Story 12.2, child spec `spec-12-2-external-workload-authority.md` (in-progress, 4 of 5 tasks done). On 2026-11-13, `/bmad-build 12.2` found the owner enrollment references (`Folders:WorkloadEnrollment:ManifestPath`, `ManifestSha256`) still absent.

**Problem (technical limitation found on investigation):** the blocker can't be cleared the way the spec describes. The required owner enrollment can never match the strict acceptance fixture:

- `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs:48` draws a random `_runId` for every run. Every caller AppId (`folders-<runId>`, `folders-workers-<runId>`, `tenants-<runId>`, `eventstore-<runId>`, proof workers) and the tenant (`durability-<runId>`) include it.
- The Platform consumer `PlatformWorkloadEnrollment.ConfigureAsync` (called at fixture line 1145) refuses unless the manifest names exactly that run's cohort and tenant, with one current grant per required probe. The runbook also forbids reusing credentials across runs.
- So a manifest the owner enrolls ahead of time can never match a run, and the spec would leave 12.2 blocked indefinitely.

**Who covers what today:** Platform owns the mechanism: the Keycloak 26.6.4 provider, the runbook and the consumer (Platform `09946e76`). No Platform epic, story, sprint entry or change proposal covers performing the enrollment. The nearest stories (3.4 local realm, 4.19 staging realm, 6.11 production realm, 10.1 Folders into staging) have no workload-binding criteria.

**Two design constraints verified in code:**

1. The consumer runs its live TLS, metadata, JWKS and token probes before the app starts (fixture line 1145 vs 1157–1158). So the workload authority must be a separate Keycloak that is already running, not the fixture's `security` Keycloak resource, which is plain HTTP and not yet started at that point. The design already supports a separate workload issuer through `Authentication:WorkloadJwtBearer`.
2. The consumer requires HTTPS with default TLS validation and sets `RequireHttpsMetadata=true` on every receiver.

**Existing building blocks:** Platform `infrastructure/keycloak-workload-binding/verify_component.py` already builds the provider, starts a disposable HTTPS 26.6.4 container, imports clients and grants, writes a full Folders-cohort manifest and runs the consumer. It does this for canonical names and `tenant-a`, under a "component only" label. The fixture already generates every other trust root per run: Dapr mTLS certificates, RSA source and bootstrap proof keys, client secrets, and the realm import.

## 2. Impact analysis

### Checklist record

| Item | Status | Finding |
| --- | --- | --- |
| 1.1 Trigger story | [x] | Folders 12.2, child spec external workload authority |
| 1.2 Problem type | [x] | Technical limitation: the specified owner-enrollment path can't satisfy a run-scoped fixture |
| 1.3 Evidence | [x] | Fixture lines 48, 94, 1145, 1157; consumer `ValidateInventory` and `Https` checks; runbook "Exact inventory" and "run suffix" rules |
| 2.1 Current epic | [x] | Epic 12 can still complete; 12.2 gains an external prerequisite |
| 2.2 Epic-level changes | [x] | None in Folders. Platform Epic 2 gains Story 2.12 |
| 2.3 Remaining epics | [x] | Platform Epic 10 (10.1) gains the owner-enrollment proof. No other Folders epic affected |
| 2.4 Obsolete or new epics | [N/A] | None |
| 2.5 Resequencing | [x] | Platform 2.12 must land before 12.2's strict run. No Folders rank change (new node at rank 1 < 12.2 at rank 21) |
| 3.1 PRD | [N/A] | No PRD requirement names workload enrollment; MVP unchanged |
| 3.2 Architecture | [N/A] | Folders architecture doesn't mention workload enrollment. Platform AD-6 already says local and CI realms are generated; the producer uses startup import only, with no admin user or admin API, so AD-6's no-admin-automation rule holds. EventStore's contract governs production issuance and the embedded issuer, which stays forbidden |
| 3.3 UX | [N/A] | No UI impact |
| 3.4 Other artifacts | [!] | Platform runbook and consumer change inside Story 2.12 (not edited now). Folders manifest and sprint trackers updated below |
| 4.1 Direct adjustment | Viable | Medium effort, medium risk |
| 4.2 Rollback | Not viable | Nothing to revert; the provider and consumer stay as they are |
| 4.3 MVP review | Not viable | Not needed; MVP scope unchanged |

### Epic and story impact

- **Folders Story 12.2** stays in-progress. Its enrollment blocker changes from "owner input that can't match" to the deliverable Platform Story 2.12 plus Folders fixture wiring.
- **Platform Epic 2** gains Story 2.12 (backlog). Epic 2 is already in-progress.
- **Platform Story 10.1** gains the real owner-enrollment proof in staging, so that assurance isn't lost.

### Artifact conflicts resolved by this proposal

- Child spec frozen intent: "generate a signer and declare it trusted" and "change dependency pins" are now scoped (F1).
- The Platform runbook's "no generated grant is an activation path" and its Story 12.2 completion paragraph get scoped or replaced inside Story 2.12 (P1 criterion), not in this pass. The runbook describes implemented behavior, and the producer doesn't exist yet.

### Technical impact

- Platform: a C# test-support producer in `Hexalith.Platform.Composition`, next to the consumer. It shares the consumer's required-inventory rule so the two can't drift, and it ports the realm and grant building from `verify_component.py`.
- Folders: fixture wiring only. The AppHost (`Program.cs`) keeps owner references as its only activation path. Strict test bodies, deadlines, retries and budgets stay unchanged.
- One additional Folders Platform gitlink update, to the commit that delivers Story 2.12.

## 3. Recommended approach

**Direct adjustment (hybrid across two repositories):** a run-owned workload-binding authority, scoped to disposable test environments.

- **How it works:** after the fixture fixes its final cohort, replicas and `TenantId`, it starts a disposable HTTPS Keycloak 26.6.4 with the provider. It is configured only by a startup realm import and enrolled for exactly that run's inventory. The producer reads the signing key, writes an owner-only manifest and secret files in the run directory, and supplies `ManifestPath`/`ManifestSha256`. Then the **unchanged** consumer verifies everything and enables the profile before catalog capture. Teardown removes all of it.
- **Rationale:** it's the only option that works with run isolation and needs no manual action each run. It exercises the real provider, real Keycloak signing, real TLS validation and independent token checks. It matches how the fixture already handles every other trust root.
- **Rejected alternatives:**
  - Fixed-identity owner enrollment: monthly re-enrollment, can't run in CI, single person, weaker isolation, and no more real than a local key.
  - The SDK's nonconforming Development fixture authority: forbidden, and still forbidden.
  - Expanding Platform Story 3.4: unnecessary scope.
- **Trade-off accepted:** fixture evidence proves Folders works under the real workload-binding protocol. It does not prove that owner enrollment is correct; that proof moves to Platform Story 10.1 in staging.
- **Effort:** medium. Platform 2.12 is roughly one story (producer, inventory sharing, TLS precondition, component tests, runbook). The Folders fixture wiring is low.
- **Risk:** medium.
  - TLS trust must reach the test process and hosts at launch. Prove this first. The suggested default is the standard local developer HTTPS trust, with no bypass.
  - A second Keycloak adds startup time inside the fixture's own setup budget.
  - Because the producer and consumer share inventory logic, a shared bug would go unnoticed. The independent provider Maven tests partly mitigate this.
- **Timeline impact:** 12.2 closure now waits on Platform 2.12 instead of an unbounded owner action.
- **Open risk unchanged by this proposal:** the last strict run failed before reaching the enrollment-dependent steps. Its expected 403 request was cancelled under the unchanged 75-second policy, with no established cause. Enrollment may not make the run green by itself.

## 4. Detailed change proposals

### Folders

**F1 — Child spec `spec-12-2-external-workload-authority.md`, frozen intent (human renegotiation).**
- Add a dated *Renegotiated (2026-11-13)* paragraph after **Approach**. Inside the disposable strict fixture only, verified enrollment comes from the Platform run-owned authority (Platform Story 2.12). Persistent, staging and production enrollment stays owner-provisioned (Platform Story 10.1). The one authorized pin change is the Folders Platform gitlink update to the Story 2.12 commit.
- **Always** gains: label run-owned enrollment evidence as run-owned; it never qualifies owner enrollment.
- **Never**, OLD: `generate a signer and declare it trusted` … `change dependency pins`. NEW: `generate or trust a signer outside the Platform run-owned authority, reuse a run-owned enrollment for another run or present it as owner enrollment` … `change dependency pins other than the approved Platform gitlink update`.
- **I/O matrix:** the *Missing enrollment* row now covers both a missing owner reference and a missing or failed run-owned authority. A new *Run-owned enrollment* row is added.

**F2 — Child spec, non-frozen sections.**
- New task: the external prerequisite (Platform 2.12 plus the gitlink update).
- New task: fixture wiring in `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`, with a focused control that a missing authority leaves the profile disabled.
- The evidence task gains the run-owned label.
- AC1 and AC3 reworded; AC4 added (run-owned label, invalid for other runs).
- Implementation note and Spec Change Log updated.

**F3 — Parent spec `spec-12-2-durable-projections-and-task-completion-pipeline.md`.** Append a dated section, *Run-owned workload authority decision — 2026-11-13*, outside the frozen block. The parent frozen block (`497cba1a…`, the preserved `frozen497c` guard) is **intentionally unchanged**. The approval lives in the child's frozen intent.

**F4 — `planning-story-manifest.yaml`.**
- New pending external node `EXT-PLATFORM-RUN-OWNED-WORKLOAD-AUTHORITY` (rank 1, no prerequisites, `external-owner-controlled`), added to `execution_waves` rank 1 and to `exact_prerequisites['12.2']`.
- `planning_amendments` entry prepended.
- No story rows or counts change.

**F5 — `sprint-status.yaml`.** Update the `last_updated` header and field, and add a comment above `12-2-…` naming the new prerequisite. The story key stays `in-progress`. Your uncommitted Epic 3 comment line is preserved.

### Platform (uncommitted edits in `references/Hexalith.Platform`)

**P1 — `epics.md`: new Story 2.12, "Provision a run-owned workload-binding authority for disposable test environments".** Placed after Story 2.11. Its criteria cover:
- startup import only, with no admin user or admin API;
- inventory derived from the consumer's own rule, with validity bounded by the run;
- a manifest and secrets with owner-only permissions, passed to the unchanged consumer;
- default TLS validation with launch-time trust and a named precondition on failure;
- refusal for persistent, shared, staging and production targets and for foreign runs;
- cleanup, and evidence labeled `run-owned enrollment`;
- component tests;
- a runbook update that scopes the generated-trust rule and replaces the Folders 12.2 paragraph.

**P2 — `epics.md`.**
- Epic 2's additional-requirements line gains `AR-25 (run-owned workload-binding authority for disposable environments, Story 2.12)`.
- Story 10.1 gains a criterion: Administrator installs the provider on the shared Keycloak, and Folders' exact grants are enrolled in the staging realm per the runbook. The consumer verifies them, and bound source reads pass in staging. No run-owned enrollment counts as staging evidence.

**P3 — `sprint-status.yaml`.** Add `2-12-provision-a-run-owned-workload-binding-authority-for-dispos: backlog` after 2.11, with an origin comment. Epic 2 stays in-progress.

### Not changed (checked)

- Folders: PRD, epics.md, architecture.md and UX are unchanged. 12.2's epic criteria don't mention enrollment, and run-owned enrollment is real-provider proof, not "fake-only" proof.
- Platform: ARCHITECTURE-SPINE AD-6.
- EventStore: `docs/security/workload-binding-authority.md`, since the embedded issuer stays forbidden.
- The Platform consumer code, which is reused unchanged except for exposing its inventory rule inside Story 2.12.

## 5. Implementation handoff

**Scope: moderate.** This is a backlog change across two repositories with a frozen-intent renegotiation. No replan is needed.

| Order | Owner | Work | Done when |
| --- | --- | --- | --- |
| 1 | Jerome | Review the uncommitted edits (especially the F1 frozen wording), then commit Platform and Folders | Both repositories committed |
| 2 | Platform developer (`/bmad-build 2.12` in Hexalith.Platform) | Implement Story 2.12. Prove launch-time TLS trust first | Story 2.12 done, component evidence recorded, commit pushed |
| 3 | Folders developer (`/bmad-build 12.2`) | Update the Platform gitlink to the 2.12 commit, wire the fixture (F2 tasks), run the unchanged strict class, and record the evidence labeled run-owned | Child spec tasks complete; strict result recorded, whether pass or fail |
| 4 | Jerome | Accept `EXT-PLATFORM-RUN-OWNED-WORKLOAD-AUTHORITY` with an exact commit and an acceptance record | Node `accepted` |
| Later | Platform (Story 10.1) | Owner enrollment proof in staging | 10.1 criterion met |

**Success criteria:**
- Folders' strict fixture enables the workload profile from a run-owned enrollment for its own random cohort and tenant, with zero manual steps.
- The consumer, strict bodies, deadlines and retries are unchanged.
- Outside the fixture, owner references remain the only activation path.
- No evidence claims owner enrollment.

## 6. Change record

Approved by Jerome on 2026-11-13 ("apply recommended") and applied the same day. All edits are uncommitted in both repositories. The checks that ran: all three changed YAML files parse; the new node's rank (1) is below 12.2's (21); 12.2's prerequisites list it once; the parent frozen block still hashes to `497cba1a…`. No build or test run was needed: no source, test, epics.md NFR bullet or OQ4 manifest field changed.

## Appendix — applied change hashes (SHA-256)

| File | Before | After |
| --- | --- | --- |
| `_bmad-output/implementation-artifacts/spec-12-2-external-workload-authority.md` | `6d2fc5b76059f7d2097289c4a98f2cda247c80e79561469e073f1c6a986d9415` | `63066c3305d99433bfb2d2657948187239d24a7570942364952ba5a6d183dac2` |
| … its frozen block | `69e8a7beff20c976a844e19fc6cd4646c543d7d083909849f849b75d71092840` | `baf299675fe11e5807e0792ad53feb8a8c3837bba5a29735b805ac410ff8b433` |
| `_bmad-output/implementation-artifacts/spec-12-2-durable-projections-and-task-completion-pipeline.md` | `4b12ddf16aa8603d197fe58dd1146e03bdb2d4a6264ab56587441ae5ae229c5e` | `5bbe94f91dcd0ef7c6d0499eeffc69cddfac694382c722bbc0d9545f1ed5a6b4` (frozen block unchanged: `497cba1a5e7c3670502f2c49e396ac77fbfa66a337ae199aafaeab6313226422`) |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | `e2c90705a2436a47cd9fad521871637b520c8a1e5f1f6b2db58ccc2e6f722967` | `7ff67766f11d7a7eb44e5d6c5f782c6151deb2f82de65f972af6b1faed076842` |
| `_bmad-output/planning-artifacts/planning-story-manifest.yaml` | `1d8c6be38d22fce84c3775afe4dfc3ad1d71ac49e465536542c9bf3c008bffd9` | `4fd23cd1252b356ab24048f52150aec6b991d5a129a13c2890e159f7c5c2636d` |
| `references/Hexalith.Platform/_bmad-output/planning-artifacts/epics.md` | `a1b5679dd7b7c02d94a5864c74d88f6b7bde010e38d8b514d6aef3d76b52acc7` | `0f25afd6f400330275d40c047e87b554c15b1b188ff09f1847b201b8cb7ff1e3` |
| `references/Hexalith.Platform/_bmad-output/implementation-artifacts/sprint-status.yaml` | `857a938b2f7930dbb6ea59348bf90d77f671af2e43463044e30a0fe9e2318912` | `e33bc5e4c5921077ae39adf207eeb783d3f7baceb4ee514677d5e3c48fb0aa43` |
