---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-05-19'
epicNum: 2
epicTitle: 'Tenant-Scoped Folder Access And Lifecycle'
designLevel: 'Epic-Level'
---

# Test Design: Epic 2 — Tenant-Scoped Folder Access And Lifecycle

**Date:** 2026-05-19
**Author:** Jerome (with Murat, Master Test Architect)
**Status:** Draft

---

## Executive Summary

**Scope:** Epic-Level test design for Epic 2 — stories 2-1 through 2-9, covering completed (2-1 to 2-4), in-review (2-5), and ready-for-dev (2-6 to 2-9) work. Backend-first .NET 10 stack with xUnit v3 + Shouldly + NSubstitute + Testcontainers + Aspire test host. Browser E2E remains deferred (operations console is read-only MVP without stable selector contracts).

**Risk Summary:**

- Total risks identified: **28**
- High-priority risks (score ≥6): **14** (1 BLOCK, 13 MITIGATE)
- Critical categories: SEC (15), DATA (7), OPS (4), BUS (2), TECH (1), PERF (1)

**Coverage Summary:**

- P0 scenarios: ~25 (~25–40 hours)
- P1 scenarios: ~20 (~15–25 hours)
- P2 scenarios: ~6 (~4–8 hours)
- P3 scenarios: ~3 (~1.5–2 hours)
- **Total effort:** ~45–75 hours (~6–9 days for one developer-pair, faster if split across story teams)

**Top headline:** Story 2-6 (`enforce layered authorization with safe denials`) carries the only BLOCK-level risk (R-01, score 9) in the whole epic. The "first denial short-circuits all later layers and any protected resource touch" invariant **must** ship with its proving test, not after.

---

## Not in Scope

| Item | Reasoning | Mitigation |
|---|---|---|
| Browser E2E coverage | Operations console is read-only MVP; no stable route/selector contracts yet (Epic 6) | Smoke/accessibility coverage deferred to a dedicated Playwright effort once Epic 6 lands |
| Provider contract drift (GitHub / Forgejo) | Owned by Epic 3 tests; hermetic-PR-gate + nightly drift already in place | Existing `tests/contracts/forgejo/<version>/` fixture matrix + nightly oasdiff |
| CLI / MCP / SDK behavioral parity | Owned by Epic 5 (5-1 through 5-7) | Parity oracle outputs already feed `tests/fixtures/parity-contract.yaml`; Epic 2 only contributes authorization rows |
| Performance/capacity tests | NFR21–NFR36 owned by Epic 7 (7-7, 7-10) | Authorization decision latency is observed but not benchmarked here |
| Production Dapr policy deployment | Story 2-6 only scoped to the evidence seam | Deployment owned by Epic 7 (7-1) |
| Repair workflows / local-only folder mode / webhooks / brownfield adoption | Out of MVP per project-context constraints | N/A — explicitly excluded |

---

## Risk Assessment

### High-Priority Risks (Score ≥6)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner | Timeline |
|---|---|---|:-:|:-:|:-:|---|---|---|
| R-01 | SEC | **Layered authorization order leaks resource existence** — any layer touches a protected stream/key/path before an earlier denial short-circuits | 3 | 3 | **9** | Spy-based unit test that asserts no later layer is invoked and no protected key derived after first denial; mechanical layer ordering type, not caller discipline | Story 2-6 dev pair | With Story 2-6 PR (BLOCK gate) |
| R-02 | SEC | Effective-permissions cache reuse across principal/task/tenant/watermark axes | 2 | 3 | 6 | Per-request memoization keyed by tenant+actor+action+scope+watermark; cross-principal cache-isolation test | Story 2-5 / 2-6 | Story 2-6 PR |
| R-03 | SEC | Stale tenant-access projection allows mutation | 2 | 3 | 6 | Fail-closed-on-stale across all mutation paths; clock-fakeable integration test ages projection beyond freshness threshold | Story 2-1 already done; gap test | This sprint |
| R-04 | SEC | Revocation freshness gate bypassed (held locks, mid-task) | 2 | 3 | 6 | C7 revocation watermark gate on every "allowed" answer; clock-fakeable revoke-vs-grant tests; Story 2-9 worker handler propagation test | Story 2-4 + 2-5 + 2-9 dev pairs | Story 2-9 PR |
| R-05 | SEC | Tenant authority sourced from request payload | 2 | 3 | 6 | Treat route/query/header/body tenant IDs as comparison only; mismatch → safe denial before resource touch; explicit conflict test on `CreateFolder` | Story 2-3 done; gap test | This sprint |
| R-06 | SEC | Safe denial envelopes leak resource existence (401/403/404 derived from lookup) | 2 | 3 | 6 | Single shared denial-mapping policy; parameterized test over the mapping table; byte-identical response bodies across cross-tenant/same-tenant-unauthorized/not-found-to-caller | Story 2-6 dev pair | Story 2-6 PR |
| R-07 | SEC | Metadata leakage across emit channels (logs/traces/metrics/events/audit/diagnostics/Problem Details) | 2 | 3 | 6 | Sentinel corpus sweep enforced on every new channel; extend audit-leakage-corpus inventory for Stories 2-6/2-7/2-9 | Cross-cutting + each story dev pair | Continuous (per-story DoD) |
| R-08 | SEC | Worker handler idempotency on Tenants events | 2 | 3 | 6 | Causation/correlation-keyed handlers; replay-twice test asserts single projection update; TenantDisabled/UserRemoved within C7 budget | Story 2-9 dev pair | Story 2-9 PR |
| R-09 | SEC | Credential / token / URL leakage in folder lifecycle/binding response | 2 | 3 | 6 | Sentinel sweep on lifecycle response; embedded-credential URL filter | Story 2-7 dev pair | Story 2-7 PR |
| R-10 | SEC | Principal-class confusion in ACL evaluation | 2 | 3 | 6 | Principal-kind matrix theory test (user × group × role × delegated_service_agent); explicit class-handling logic | Story 2-2 done; gap test | This sprint |
| R-11 | DATA | Read-model determinism breaks under replay | 2 | 3 | 6 | Replay corpus across Stories 2-2/2-4/2-5/2-8 events → byte-identical projection state; non-determinism = automatic FAIL | Cross-cutting | Pre Story 2-6 merge |
| R-12 | DATA | Conflicting stale grant vs fresh revoke ordering | 2 | 3 | 6 | Ordered-row matrix test; revoke wins regardless of projection input order | Story 2-4 done; gap test | This sprint |
| R-13 | DATA | ACL state divergence between aggregate and projection | 2 | 3 | 6 | Shared replay corpus + projection-vs-aggregate assertion; closes alongside R-11 | Story 2-2 / cross-cutting | Pre Story 2-6 merge |
| R-14 | SEC | Audit-leakage corpus regression on new emit channels | 2 | 3 | 6 | Channel inventory + automated sweep; gate FAIL on missing sentinel for any new emit | Cross-cutting | Continuous |

### Medium-Priority Risks (Score 3–5)

| Risk ID | Category | Description | P | I | Score | Mitigation | Owner |
|---|---|---|:-:|:-:|:-:|---|---|
| R-15 | OPS | Layered authorization adapter drift across REST/SDK/CLI/MCP | 2 | 2 | 4 | One shared denial-mapping seam; parity oracle row sourced from Story 2-6 result shape | Story 2-6 + Epic 5 |
| R-16 | DATA | Canonical ordering / dedup of EffectivePermissions output | 2 | 2 | 4 | Shuffled-input + duplicate-row theory tests; stable serialization assertion | Story 2-5 |
| R-17 | OPS | `FreshnessMetadata` accuracy in EffectivePermissions responses | 2 | 2 | 4 | Assert watermark/observedAt/stale fields change with seeded read-model state | Story 2-5 |
| R-18 | OPS | Tenant-event subscription degradation invisible | 2 | 2 | 4 | Health check exposes subscription staleness/lag; alarmable signal | Story 2-1 done; gap test |
| R-19 | PERF | Timing-based existence leakage via response latency | 2 | 2 | 4 | Bounded timing buckets on denial responses; histogram-asserting test | Story 2-6 (P2 scenario) |
| R-20 | OPS | Dapr policy evidence missing in dev / production mismatch | 2 | 2 | 4 | Local fake evidence provider mirrors prod contract; integration parity test in nightly | Story 2-6 |
| R-21 | BUS | Folder hierarchy / name treated as identity | 2 | 2 | 4 | Rename/path-change smoke test; opaque ULID enforced | Story 2-3 done; gap test |
| R-22 | BUS | Archive transition not enforced across future commands | 2 | 2 | 4 | C6 matrix theory test over archived state × every command | Story 2-8 |
| R-23 | DATA | Action vocabulary drift across surfaces | 2 | 2 | 4 | Non-canonical input negative tests on action parser | Stories 2-2 / 2-5 / 2-6 |
| R-24 | SEC | ACL row metadata leakage in events/audit | 2 | 2 | 4 | Extend sentinel sweep to grant/revoke event payloads | Story 2-4 done; gap test |
| R-25 | DATA | Folder ULID collision | 1 | 3 | 3 | Deterministic ULID generator; cross-stream uniqueness smoke | Story 2-3 |

### Low-Priority Risks (Score 1–2)

| Risk ID | Category | Description | P | I | Score | Action |
|---|---|---|:-:|:-:|:-:|---|
| R-26 | TECH | Reserved `system` tenant misuse in managed folder streams | 1 | 3 | 3 | DOCUMENT (covered by aggregate-identity assertion smoke) |
| R-27 | SEC | Tenant prefix missing on cache/idempotency/durable keys (C10) | 1 | 3 | 3 | DOCUMENT — already enforced by `run-governance-completeness-gates.ps1` CI lint |
| R-28 | OPS | Selective Tenants event handling drifts (non-`folders.*` keys handled) | 1 | 2 | 2 | DOCUMENT (Story 2-9 selective-filter theory) |

### Risk Category Legend

- **TECH**: Technical/Architecture (flaws, integration, scalability).
- **SEC**: Security (access controls, auth, data exposure, tenant isolation).
- **PERF**: Performance (SLA violations, degradation, resource limits — including latency-based information leakage).
- **DATA**: Data Integrity (loss, corruption, inconsistency, determinism, replay).
- **BUS**: Business Impact (state-machine correctness, UX, logic errors).
- **OPS**: Operations (deployment, config, monitoring, cross-surface parity).

---

## Entry Criteria

- [x] Epic 2 PRD slice (FR4–FR14) and architecture mapping reviewed.
- [x] Story acceptance criteria available for 2-1 through 2-9.
- [x] Test framework refresh complete (xUnit v3 baseline confirmed 2026-05-19; see `framework-setup-progress.md`).
- [x] Authorization-related production scaffolding present (`src/Hexalith.Folders/Authorization/`, 28 files).
- [x] Story 2-5 implementation in `review` with 8 test files in place.
- [ ] Replay corpus location agreed (open question — see "Open Assumptions").
- [ ] Story 2-6 ATDD red tests authored before dev begins (recommended; see Follow-on Workflows).

## Exit Criteria

- [ ] All P0 tests passing (100%, no exceptions).
- [ ] All P1 tests passing (≥95%, waivers owner-named with expiry).
- [ ] R-01 BLOCK-gate test green and added to the safety-invariant gate inventory.
- [ ] Audit-leakage sentinel sweep passes across all new emit channels from Stories 2-6/2-7/2-9.
- [ ] Replay determinism corpus produces byte-identical projection state across two cold runs.
- [ ] Existing governance gate scripts remain green (`run-safety-invariant-gates.ps1`, `run-governance-completeness-gates.ps1`, `run-contract-spine-gates.ps1`).
- [ ] No score-6 risk has an unmitigated implementation path.

---

## Test Coverage Plan

### P0 (Critical) — Run on every PR

**Criteria:** Blocks tenant isolation, authorization correctness, or data-integrity invariants. High-risk (score ≥6) or supports the BLOCK gate. No workaround.

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
|---|---|---|:-:|---|---|
| Layer short-circuit proof (R-01 BLOCK gate) | Unit (spies) | R-01 | 1–2 | Story 2-6 dev pair | Single mandatory test plus per-layer parameterization |
| Canonical layer order enforced mechanically | Unit | R-01 | 1 | Story 2-6 | Type-level ordering, not caller discipline |
| Denial-mapping table parameterized (401/403/404/503 × every condition) | Unit + Contract | R-06, R-15 | 2–3 | Story 2-6 | Shared policy seam |
| Per-request memoization isolation across tenant/actor/action/scope/watermark | Unit | R-02, R-01 | 1–2 | Story 2-5 + 2-6 | Extends current Story 2-5 cache-isolation pattern |
| Authorization decision snapshot shape (safe metadata only) | Unit | R-07 | 1 | Story 2-6 | |
| Cross-tenant matching identifiers cannot reach foreign data | Unit | R-01, R-05 | 1 | Story 2-5 + 2-6 | Same folder ID + principal ID across tenants |
| Tenant-access projection fail-closed on stale evidence (mutation) | Unit + Integration | R-03 | 2 | Story 2-1 gap | Fakeable clock ages projection |
| Payload tenantId vs authenticated tenant conflict → safe denial pre-touch | Integration (Server.Tests) | R-05 | 1 | Story 2-3 gap | On `CreateFolder` and other mutations |
| `system` reserved tenant rejected in stream-name builder | Unit | R-26 (smoke for R-05) | 1 | Story 2-3 gap | |
| C7 revocation propagates within budget on held locks | Unit + Integration | R-04 | 2–3 | Story 2-4 + 2-5 + 2-9 | Clock-fakeable, mutation path + query path |
| Stale grant vs fresh revoke: revoke wins regardless of input order | Unit | R-12, R-04 | 1 | Story 2-4 gap | Ordered-row matrix |
| Sentinel sweep on ACL grant/revoke event payloads | Unit | R-07, R-24 | 1 | Story 2-4 gap | Extend corpus |
| Principal-kind matrix (user × group × role × delegated_service_agent) | Unit (Theory) | R-10 | 1 | Story 2-2 gap | |
| Effective-permissions safe denial envelopes byte-identical across denial categories | Unit + Contract | R-06 | 1–2 | Story 2-5 | Verify byte-identical assertion exists |
| Read-model unavailable / stale produces canonical codes; no aggregate fallback | Unit (spies) | R-06, R-11 | 1 | Story 2-5 | Already covered; gap-check breadth |
| Server contract alignment for `GET /api/v1/folders/{folderId}/effective-permissions` | Contract | R-06 | 1 | Story 2-5 | Already covered |
| Replay corpus → byte-identical projection state | Integration | R-11, R-12, R-13, R-22 | 1 | Cross-cutting | Highest leverage — closes four risks |
| Audit-leakage sentinel sweep across all new emit channels from 2-6/2-7/2-9 | Unit + Contract gate | R-07, R-14 | 3–4 | Cross-cutting | Channel-inventory yaml driving the sweep |
| Worker handler idempotency: replay tenant event twice → single projection update | Unit + Integration | R-08, R-11 | 1–2 | Story 2-9 | |
| TenantDisabled / UserRemovedFromTenant revocation effective within C7 | Integration | R-08, R-04 | 1–2 | Story 2-9 | Fakeable clock + lock fixture |
| C6 transition: archived folder rejects every workspace/lock/file/commit command | Unit (Theory) | R-22 | 1 | Story 2-8 | Matrix row × every command |
| Lifecycle/binding response sentinel sweep (provider tokens, embedded-credential URLs) | Unit + Contract | R-09, R-07 | 1 | Story 2-7 | |
| Unauthorized-reader vs cross-tenant identical safe-denial on folder lifecycle | Unit | R-06 | 1 | Story 2-7 | Inherits denial-mapping policy |
| EventStore validator rejection mapped to canonical category without leaking internals | Unit | R-07, R-15 | 1 | Story 2-6 | |

**Total P0:** ~25 atomic scenarios → ~25–40 hours.

### P1 (High) — Run on PR to main (same PR gate)

**Criteria:** Important features + medium risk (3–5) + common workflows + secondary mitigations for high-risk surfaces.

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
|---|---|---|:-:|---|---|
| Canonical output: shuffled rows + duplicate grants/revokes → byte-identical response | Unit | R-16 | 1 | Story 2-5 | Verify dedicated test |
| `FreshnessMetadata` reflects actual read-model watermark | Unit | R-17 | 1 | Story 2-5 | |
| Action token parser rejects non-canonical inputs | Unit | R-23 | 1 | Stories 2-2 / 2-5 / 2-6 | |
| Replay corpus → projection state == aggregate ACL state | Integration | R-13, R-11 | 1 | Story 2-2 + cross-cutting | Closes with R-11 corpus |
| Dapr policy evidence missing → fail closed (deterministic fake) | Unit | R-20 | 1 | Story 2-6 | |
| Cross-surface parity oracle row generated from layered-auth output | Contract | R-15 | 1 | Story 2-6 | Feeds `parity-contract.yaml` |
| Archive event payload sentinel sweep | Unit | R-07, R-24 | 1 | Story 2-8 | |
| Archived folder audit/status remains queryable under retention class C3 | Integration | R-11, R-22 | 1 | Story 2-8 | Replay or projection rebuild |
| Lifecycle freshness metadata accurate (active/archived/unbound/provider-bound) | Unit | R-17, R-22 | 1 | Story 2-7 | |
| Worker handler emits metadata-only events | Unit | R-07 | 1 | Story 2-9 | Sentinel sweep |
| Selective Tenants-event filter (`folders.*` only) | Unit (Theory) | R-28 | 1 | Story 2-9 | |
| Tenant-event subscription staleness / lag exposed via health check | Integration | R-18 | 1 | Story 2-1 gap | |
| Folder rename / path change leaves aggregate identity unchanged | Unit | R-21 | 1 | Story 2-3 gap | Smoke OK |
| Per-request authorization memoization scope hardening for Story 2-6 | Unit | R-02 (P0 carries main scope) | 1 | Story 2-6 | Additional axis: action token |
| Bounded stale diagnostic reads honor `FreshnessMetadata` + operation policy | Unit + Contract | R-06, R-17 | 1 | Story 2-6 | Diagnostic read class |
| Authorization snapshot correlation/task ID propagation without becoming authority | Unit | R-05, R-07 | 1 | Story 2-6 | |
| Sentinel sweep on layered-auth denial Problem Details | Unit + Contract | R-07, R-14 | 1 | Story 2-6 | Stops 401/403/404/503 from leaking |
| ACL projection rebuild from empty event stream matches live projection | Integration | R-11, R-13 | 1 | Cross-cutting | Closes alongside R-11 |
| Cache key tenant prefix lint includes Story 2-6/2-9 new keys | CI lint | R-27 (covered) | — | Existing gate | No new test work; confirm gate covers new keys |
| Smoke test for opaque ULID collision (statistical) | Unit | R-25 | 1 | Story 2-3 | |

**Total P1:** ~20 atomic scenarios → ~15–25 hours.

### P2 (Medium) — Run nightly or scheduled

**Criteria:** Secondary mitigations + edge cases that benefit from longer cycles.

| Requirement | Test Level | Risk Link | Test Count | Owner | Notes |
|---|---|---|:-:|---|---|
| Bounded timing buckets on denial responses | Unit | R-19 | 1–2 | Story 2-6 | Requires fakeable clock + histogram helper |
| Live integration: full Aspire host + Tenants worker + Folders revocation propagation | Integration (nightly) | R-04, R-08 | 1 | Story 2-9 | Real EventStore + Dapr test host |
| Drift smoke between local fake Dapr policy provider and production policy file | Integration (nightly) | R-20 | 1 | Story 2-6 | |
| Long-running replay corpus across all Epic 2 events (full epic) | Integration (nightly) | R-11 | 1 | Cross-cutting | Larger corpus than the PR-gate replay |
| Archive folder workflow under load (basic throughput smoke) | Integration (nightly) | R-22 | 1 | Story 2-8 | |
| Folder lifecycle / binding response stress sentinel sweep | Unit (nightly) | R-09, R-07 | 1 | Story 2-7 | Expanded payload corpus |

**Total P2:** ~6 atomic scenarios → ~4–8 hours.

### P3 (Low) — Run on-demand or weekly

**Criteria:** Documentation-grade or exploratory.

| Requirement | Test Level | Test Count | Owner | Notes |
|---|---|:-:|---|---|
| ULID generator statistical smoke (10k samples, no collision) | Unit | 1 | Story 2-3 | |
| Documentation/example test demonstrating canonical lifecycle for new contributors | Unit | 1 | Murat + cross-cutting | Useful as living spec |
| Exploratory chaos test on tenant-event subscription | Integration | 1 | Story 2-9 | Drop messages, replay, etc. |

**Total P3:** ~3 atomic scenarios → ~1.5–2 hours.

---

## Execution Order

### Smoke Tests (<2 min)

**Purpose:** Fast feedback, catch foundational regressions.

- [ ] `FoldersModuleSmokeTests` (already exists, ~1s)
- [ ] `TenantAccessAuthorizerTests` baseline path (~1s)
- [ ] `EffectivePermissionsEndpointTests` smoke path (~2s)
- [ ] Build + restore + `run-safety-invariant-gates.ps1 -SkipRestoreBuild`

### P0 Tests (<10 min)

**Purpose:** All BLOCK + MITIGATE risk coverage in-PR.

- [ ] Layer short-circuit BLOCK-gate (R-01) — must be green
- [ ] Effective-permissions full suite (`tests/Hexalith.Folders.Tests/Authorization/EffectivePermissions*Tests.cs`)
- [ ] TenantAccess suite + revocation freshness gap test
- [ ] Cross-tenant matching-identifier matrix
- [ ] Sentinel corpus sweep across all current emit channels
- [ ] Replay determinism corpus (in-PR-affordable subset)
- [ ] Aggregate transition coverage including archive matrix once Story 2-8 lands
- [ ] Worker handler idempotency once Story 2-9 lands

### P1 Tests (<25 min)

**Purpose:** Important feature coverage including projection rebuild, parity oracle row generation, freshness metadata accuracy.

### P2/P3 Tests (Nightly / Weekly)

**Purpose:** Aspire-host integration, larger replay corpora, chaos and load smoke. Run on a schedule, not in PR.

---

## Resource Estimates

### Test Development Effort

| Priority | Count | Hours/Test | Total Hours | Notes |
|---|:-:|:-:|---|---|
| P0 | ~25 | 1.0–1.6 | **25–40** | Spy-based seam tests, sentinel extensions, replay corpus seeding |
| P1 | ~20 | 0.75–1.25 | **15–25** | Standard unit + a handful of integration |
| P2 | ~6 | 0.6–1.3 | **4–8** | Nightly-ready integration scenarios |
| P3 | ~3 | 0.5–0.7 | **1.5–2** | Statistical smoke + exploratory |
| **Total** | **~54** | **–** | **~45–75 hours** | **~6–9 days for one dev-pair; faster if split across story teams** |

### Prerequisites

**Test Data:**

- Existing factories under `src/Hexalith.Folders.Testing/Factories/`: `FoldersTestDataFactory`, `FolderCreationTestDataFactory`, `OrganizationAclTestDataFactory`, `TestAuthorizationContext`/`Overrides`, `TestFolderContext`/`Overrides`. Extend with reusable effective-permission and layered-auth builders only where reused across tests.
- New replay-corpus fixture under `tests/fixtures/` — location open (see "Open Assumptions").
- Continued sentinel corpus: `tests/fixtures/audit-leakage-corpus.json` plus an "emit channel inventory" file driving the sweep across new channels.

**Tooling:**

- xUnit v3 (`Fact`/`Theory`/`IClassFixture`/`ICollectionFixture`).
- NSubstitute for seam spies (layer-invocation observers, read-model fakes).
- `Microsoft.AspNetCore.Mvc.Testing` `WebApplicationFactory<TEntryPoint>` for contract/server tests.
- `Aspire.Hosting.Testing` + Testcontainers for nightly integration.
- `FakeTimeProvider` (System.Time.Testing) for clock-fakeable revocation and freshness tests.
- `Shouldly` assertions throughout.

**Environment:**

- All PR-gate tests run offline — no provider credentials, no sidecars, no live EventStore.
- Nightly tests bring up Aspire test host + Testcontainers locally; no live GitHub/Forgejo for Epic 2 scope (Epic 3 owns that).

---

## Quality Gate Criteria

### Pass/Fail Thresholds

- **P0 pass rate:** 100% (no exceptions). R-01 BLOCK-gate test must be green before any Story 2-6 PR merges.
- **P1 pass rate:** ≥95% (≤1 P1 carries an explicit owner-named WAIVED with expiry ≤ next sprint).
- **P2/P3 pass rate:** ≥90% (informational; nightly).
- **High-risk mitigations:** every score-6 risk has a named owner and at least one test scenario from this matrix landed before its story moves out of `review`/`ready-for-dev`.

### Coverage Targets

- **`src/Hexalith.Folders/Authorization/*` and new code from Stories 2-6/2-7/2-9:** ≥85% line + branch (matches project's existing `coverlet` discipline).
- **Security scenarios (SEC category):** 100% — every SEC risk has at least one P0 or P1 test.
- **Business logic (BUS category):** ≥70%.
- **Replay determinism:** byte-identical projection state across two cold runs.

### Non-Negotiable Requirements

- [ ] R-01 BLOCK-gate test green and inventoried in the safety-invariant gate.
- [ ] No score-6 SEC risk unmitigated.
- [ ] Sentinel corpus passes with zero leaks across the full channel inventory.
- [ ] `run-safety-invariant-gates.ps1`, `run-governance-completeness-gates.ps1`, `run-contract-spine-gates.ps1` all green.
- [ ] PR gate finishes inside 15 minutes.

---

## Mitigation Plans

### R-01: Layered authorization order leaks resource existence (Score: 9, BLOCK)

**Mitigation Strategy:** Implement one canonical ordered layer type in `src/Hexalith.Folders/Authorization/` consumed by the layered evaluator, evidence, result metadata, tests, and server response mapping. The evaluator drives layers in fixed order; later layers cannot run until earlier layers return `allowed`. Tests inject spies into every later layer and assert zero invocations after a denial, plus assert no protected key/path/stream-name has been derived for the denied subject.
**Owner:** Story 2-6 dev pair.
**Timeline:** Test merged with Story 2-6 implementation PR (BLOCK; story cannot reach `done` without it).
**Status:** Planned.
**Verification:** Unit tests with spies + sentinel sweep on denial responses; safety-invariant gate inventory entry; review against `EffectivePermissionsAuthorizationGateTests` pattern.

### R-02: Effective-permissions cache reuse across principals/tasks/tenants (Score: 6)

**Mitigation Strategy:** Per-request memoization keyed by `(tenantId, actorSafeId, actionToken, operationScope, freshnessWatermark)`. Tests prove that toggling any axis invalidates the cached "allowed" result. Apply same pattern in Story 2-6 authorization decision snapshot memoization.
**Owner:** Story 2-5 (existing) + Story 2-6 dev pair.
**Timeline:** Story 2-6 PR.
**Status:** Partially implemented (Story 2-5 advanced-elicitation finding).
**Verification:** Theory-driven test toggles each axis; asserts result differs.

### R-03: Stale tenant-access projection allows mutation (Score: 6)

**Mitigation Strategy:** All mutation entry points consume `TenantAccessAuthorizer` with freshness budget; fail-closed on stale; bounded stale reads only on explicit diagnostic operation classes. Use `FakeTimeProvider` to advance time past the threshold; assert mutation rejected with stable code.
**Owner:** Story 2-1 done; gap test owned by Murat + Story 2-3/2-4/2-6 dev pair.
**Timeline:** This sprint.
**Status:** Mostly implemented; gap test pending.
**Verification:** Integration test with fakeable clock; assertion that mutation aborts before EventStore append.

### R-04: Revocation freshness gate bypassed (Score: 6)

**Mitigation Strategy:** C7 revocation watermark required for every "allowed" answer; mid-task revalidation propagates via Story 2-9 worker handlers to held lock state. Clock-fakeable tests: grant at T0, revoke at T1, assert denied at T2 (within budget) and at T0.5 with stale evidence safe-denied.
**Owner:** Stories 2-4, 2-5, 2-9 dev pairs.
**Timeline:** Story 2-9 PR completes the propagation path.
**Status:** Story 2-5 covers query path; mutation path needs coverage.
**Verification:** Cross-story integration test runs the grant → revoke → mid-task-attempt flow against fakeable clock.

### R-06: Safe denial envelopes leak resource existence (Score: 6)

**Mitigation Strategy:** Single denial-mapping policy file consumed by REST/SDK/CLI/MCP transports. Parameterized test over the denial-mapping decision table asserts every (terminal layer, condition) → (status, body shape, retryability) tuple, byte-identical bodies across cross-tenant/same-tenant-unauthorized/not-found-to-caller/projection-unavailable.
**Owner:** Story 2-6 dev pair.
**Timeline:** Story 2-6 PR.
**Status:** Planned (Story 2-6 ready-for-dev).
**Verification:** Theory test over decision table + contract test against OpenAPI Problem Details.

### R-07, R-14: Metadata leakage + sentinel corpus regression (Score: 6 each)

**Mitigation Strategy:** Maintain an emit-channel inventory file driving the sentinel sweep. Every new channel introduced by Stories 2-6/2-7/2-9 must register or fail the gate. Corpus iterates `tests/fixtures/audit-leakage-corpus.json` across all registered channels.
**Owner:** Cross-cutting + each story's dev pair as part of DoD.
**Timeline:** Continuous through Stories 2-6 → 2-9.
**Status:** Enforced today via existing safety-invariant gate; channel inventory expansion needed.
**Verification:** `run-safety-invariant-gates.ps1` extended to enforce channel-inventory completeness.

### R-11: Read-model determinism breaks under replay (Score: 6)

**Mitigation Strategy:** Build a replay corpus from Stories 2-2/2-4/2-5/2-8 events covering grants, revokes, lifecycle transitions, and ACL precedence edge cases. Replay produces byte-identical projection state across two cold runs. Non-determinism = automatic FAIL. Closes R-12 and R-13 simultaneously when projections include both organization-ACL and folder-ACL state.
**Owner:** Cross-cutting (Murat seeds; story dev pairs contribute event sequences).
**Timeline:** Before Story 2-6 merges (so layered-auth tests have a stable projection contract).
**Status:** Planned.
**Verification:** Integration test runs the corpus twice; asserts hash-equivalent projection state and identical permission output.

---

## Assumptions and Dependencies

### Assumptions

1. The xUnit v3 / Shouldly / NSubstitute / Testcontainers / Aspire baseline confirmed in today's framework refresh remains the test architecture for Epic 2.
2. `tea_use_pactjs_utils = false` stays — Hexalith.Folders contract testing flows through OpenAPI parity + provider corpus, not a Node Pact harness.
3. `Hexalith.Folders.IntegrationTests` is the right home for the replay determinism corpus (nightly + larger subset for PR). Confirm in step 04 question.
4. Bounded timing buckets are scoped into Story 2-6 as a first-class evidence field (R-19 mitigation), or split into a hardening follow-up.
5. The existing `EffectivePermissionsAuthorizationGateTests` and `EffectivePermissionsRevocationFreshnessTests` are the templates Story 2-6 will mirror for layer short-circuit and revocation propagation tests.

### Dependencies

1. **Story 2-1 fail-closed tenant-access projection** — already done; provides the freshness primitive R-03 leans on.
2. **Story 2-4 revocation watermark in ACL projection** — already done; provides the input for R-04 cross-story coverage in Story 2-9.
3. **Story 2-5 effective-permissions query handler** — in `review`; provides the read-model freshness reporting pattern R-02 / R-17 mitigations extend.
4. **Architecture-defined `FolderAclAuthorizer.cs` and `PathPolicyAuthorizer.cs`** — not yet created; Story 2-6 / 2-7 land them.
5. **Sentinel emit-channel inventory** — needs to be authored (or extended from existing CI gate metadata) before R-07/R-14 mitigations can be enforced mechanically.

### Risks to Plan

- **Risk:** Story 2-6 lands without the R-01 BLOCK test, delaying gate adoption.
  - **Impact:** Critical authorization invariant becomes review-time discovery instead of test-time guarantee.
  - **Contingency:** Run the `AT` workflow on Story 2-6 first to author the failing test up-front (recommended in Follow-on Workflows).
- **Risk:** Replay corpus authoring slips because no story explicitly owns it.
  - **Impact:** R-11 / R-12 / R-13 / R-22 stay open.
  - **Contingency:** Treat the corpus as cross-cutting work in Story 2-6 PR; Murat seeds the first version and per-story dev pairs extend with their own event sequences.
- **Risk:** Bounded timing buckets (R-19) get deferred indefinitely.
  - **Impact:** Latency-based existence leakage is a known but undetected channel.
  - **Contingency:** Either scope into Story 2-6 (recommended) or open a hardening story now with a date-targeted owner.

---

## Follow-on Workflows (Manual)

- Run `AT` (ATDD) on Story 2-6 **before** development starts. This pre-authors the R-01 BLOCK-gate test as a red test, plus the canonical layer-order theory and denial-mapping table — the right "tests first" move for a P0 blocker.
- Run `AT` on Story 2-9 to pre-author worker-handler idempotency + C7 propagation tests; same logic as Story 2-6.
- Run `RV` (Review Tests) on the freshly-landed `tests/Hexalith.Folders.Tests/Authorization/*` while context is hot — catches isolation/flakiness gaps before they harden.
- Run `TA` (Test Automation) once Stories 2-6/2-7/2-9 implementations are in flight to generate the prioritized test files and DoD summaries.
- Run `TR` (Trace Coverage) after this TD lands to map each acceptance criterion to its test scenario (already done in narrative form in the coverage matrix; `TR` produces the machine-readable artifact).
- Run `CI` to wire the new R-01 BLOCK gate and the replay corpus into the blocking lane.

---

## Approval

**Test Design Approved By:**

- [ ] Product Manager: ___________________ Date: ___________________
- [ ] Tech Lead: ___________________ Date: ___________________
- [ ] QA Lead / Murat: ___________________ Date: 2026-05-19 (proposed)

**Comments:**

---

## Interworking & Regression

| Service/Component | Impact | Regression Scope |
|---|---|---|
| `Hexalith.Folders.Server` (REST `/process` and `/project` + `/api/v1/...`) | New Story 2-6 layered authorization service wraps every request | Re-run full `Hexalith.Folders.Server.Tests`; add layered-auth parameterized denial tests |
| `Hexalith.Folders` Authorization namespace | Story 2-6 adds layered evaluator; Story 2-7 adds lifecycle inspector; potentially `FolderAclAuthorizer` / `PathPolicyAuthorizer` land | Re-run full `Hexalith.Folders.Tests/Authorization/*` |
| `Hexalith.Folders.Workers` | Story 2-9 adds tenant event handlers (TenantDisabled, UserRemovedFromTenant, UserRoleChanged, TenantConfigurationSet) | Re-run `Hexalith.Folders.Workers.Tests`; add idempotency + propagation tests |
| `Hexalith.Folders.Client` (generated SDK) | Story 2-6 changes denial response shapes only if the contract spine moves | OpenAPI parity gate must stay green; no hand-edits to `Generated/*` |
| `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp` | Will consume Story 2-6 denial categories via SDK | Parity oracle row covers — Epic 5 will pick up CLI/MCP test breadth |
| `Hexalith.Folders.UI` (Blazor Server, read-only console) | Out of Epic 2 scope; no expected change | Smoke test still runs in `Hexalith.Folders.UI.Tests` |
| Tenant-access projection in `Hexalith.Folders.Projections/TenantAccess/` | Worker handlers update it | Re-run `tests/Hexalith.Folders.Tests/Projections/TenantAccess/` |
| `tests/fixtures/audit-leakage-corpus.json` | Sentinel corpus grows with new emit channels | Sentinel sweep must pass across full inventory |
| `tests/fixtures/parity-contract.yaml` | New layered-auth rows added | Parity oracle gate must stay green |
| Governance gate scripts | New tests register; no functional drift | All three scripts must stay green |

---

## Appendix

### Knowledge Base References

- `risk-governance.md` — risk classification framework, gate decision engine, traceability matrix.
- `probability-impact.md` — risk scoring methodology, threshold mapping (1–9 score → DOCUMENT / MONITOR / MITIGATE / BLOCK).
- `test-levels-framework.md` — unit vs integration vs E2E selection guidance.
- `test-priorities-matrix.md` — P0–P3 criteria, coverage targets by level/priority.

### Related Documents

- PRD: `_bmad-output/planning-artifacts/prd.md` (Authorization and Tenant Boundary, FR4–FR10; NFR1–NFR10 Security & Tenant Isolation).
- Epic: `_bmad-output/planning-artifacts/epics.md` (Epic 2 — Tenant-Scoped Folder Access And Lifecycle).
- Architecture: `_bmad-output/planning-artifacts/architecture.md` (Phase 4 Tenant Integration, AR-AUTHZ-01 through AR-AUTHZ-05; concern #18 Authorization order; C7 two-number lock contract).
- Story 2-5 spec: `_bmad-output/implementation-artifacts/2-5-inspect-effective-permissions.md`.
- Story 2-6 spec: `_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md`.
- Sprint status: `_bmad-output/implementation-artifacts/sprint-status.yaml`.
- Framework refresh: `_bmad-output/test-artifacts/framework-setup-progress.md` (2026-05-19 sections).
- Test design progress: `_bmad-output/test-artifacts/test-design/test-design-progress.md`.

---

**Generated by:** BMad TEA Agent — Test Architect Module (Murat).
**Workflow:** `bmad-testarch-test-design`.
**Version:** 4.0 (BMad v6).
