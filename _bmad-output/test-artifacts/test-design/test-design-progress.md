---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted: ['step-01-detect-mode', 'step-02-load-context', 'step-03-risk-and-testability', 'step-04-coverage-plan', 'step-05-generate-output']
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-05-19'
inputDocuments:
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/planning-artifacts/architecture.md'
  - '_bmad-output/planning-artifacts/epics.md (Epic 2 section)'
  - '_bmad-output/implementation-artifacts/sprint-status.yaml'
  - '_bmad-output/implementation-artifacts/2-5-inspect-effective-permissions.md'
  - '_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md'
  - '_bmad-output/project-context.md'
  - 'src/Hexalith.Folders/Authorization/* (28 files — existing TenantAccess + EffectivePermissions surface)'
  - 'tests/Hexalith.Folders.Tests/Authorization/* (8 test files)'
  - 'tests/Hexalith.Folders.Server.Tests/EffectivePermissionsEndpointTests.cs'
  - 'knowledge: risk-governance.md, probability-impact.md, test-levels-framework.md, test-priorities-matrix.md'
---

# Epic 2 Test Design — Progress

## Step 01 - Detect Mode

### 2026-05-19 Mode Detection

- **Mode resolved:** Epic-Level Mode.
- **Detection path:** File-based — `_bmad-output/implementation-artifacts/sprint-status.yaml` is present, which the workflow maps to Epic-Level Mode. Confirmed by user intent (Jerome explicitly chose "Epic 2 full surface").
- **Scope:** Epic 2 "Tenant-Scoped Folder Access and Lifecycle" in full: stories 2-1 through 2-9, including completed (`2-1`–`2-4`), in-review (`2-5`), and ready-for-dev (`2-6`–`2-9`).
- **Rationale:** Highest leverage at this point in the sprint. Stories 2-1–2-4 give us the *as-built* baseline to anchor coverage decisions; 2-5 is in review and tests are fresh in the repo; 2-6–2-9 are pre-implementation, so risk-based test design now will shape their ATDD/dev runs.
- **Prerequisites confirmed:**
  - Epic source: `_bmad-output/planning-artifacts/epics.md` and per-story specs in `_bmad-output/implementation-artifacts/2-*.md`.
  - Architecture context: `_bmad-output/planning-artifacts/architecture.md`, `project-context.md`, PRD.
  - Existing tests: `tests/Hexalith.Folders.Tests/Authorization/*`, `tests/Hexalith.Folders.Tests/Aggregates/*`, plus per-area test projects.

### Halt Conditions

No halt conditions tripped. All required inputs are available.

## Step 02 - Load Context

### 2026-05-19 Context Load

### Configuration Snapshot

- `test_stack_type`: auto → detected `backend` (re-confirmed from framework refresh earlier today).
- `tea_use_playwright_utils`: true (irrelevant for backend authorization surface).
- `tea_use_pactjs_utils`: false (contract testing flows through OpenAPI parity + provider corpus, not Pact.js).
- `tea_pact_mcp`: none.
- `tea_browser_automation`: auto → no browser exploration (backend stack).
- `test_artifacts`: `_bmad-output/test-artifacts`.

### Knowledge Fragments Loaded (Required for Epic-Level Mode)

- `risk-governance.md` — risk scoring matrix (probability × impact, 1–9), gate decision logic, traceability matrix patterns.
- `probability-impact.md` — probability/impact scales, action classification (DOCUMENT/MONITOR/MITIGATE/BLOCK), score → priority mapping, gate threshold table.
- `test-levels-framework.md` — unit/integration/E2E decision matrix, when to use each level, examples for service-to-service and middleware behavior.
- `test-priorities-matrix.md` — P0/P1/P2/P3 criteria with examples, risk-based adjustments, coverage targets per level/priority.

### Project Artifacts Loaded

- Epic 2 charter from `_bmad-output/planning-artifacts/epics.md` lines 730–849.
- Story 2-5 full spec (13 acceptance criteria, tasks, dev notes, regression traps, completed implementation list).
- Story 2-6 full spec (15 acceptance criteria, layered authorization order contract, denial mapping table).
- Stories 2-7/2-8/2-9 captured from the Epic 2 charter summary (full file load deferred to step 04 if needed for coverage mapping).
- Architecture authorization slice (`Phase 3` host wiring, `Phase 4` tenant integration, file map under `src/Hexalith.Folders/Authorization/`).
- Project context's critical don't-miss rules: zero cross-tenant leakage, metadata-only events/logs/traces, tenant-prefixed cache keys, idempotency, authorization-before-resource-touch ordering, mid-task revalidation, C6 transition matrix.
- PRD authorization-and-tenant-boundary requirements (FR4–FR10), security/tenant isolation NFRs (NFR1–NFR10), reliability/idempotency NFRs (NFR11–NFR20), observability NFRs (NFR47–NFR55).

### Existing Test Coverage Snapshot

- `tests/Hexalith.Folders.Tests/Authorization/`:
  - `TenantAccessAuthorizerTests.cs` (Story 2.1 evidence — tenant access freshness, fail-closed behavior).
  - `EffectivePermissionsAuthorizationGateTests.cs` (Story 2.5 — authorization-before-observation ordering).
  - `EffectivePermissionsLayeringTests.cs` (Story 2.5 — deterministic ACL precedence).
  - `EffectivePermissionsRevocationFreshnessTests.cs` (Story 2.5 — C7 revocation freshness gate).
  - `EffectivePermissionsTaskScopeTests.cs` (Story 2.5 — task-context narrowing).
  - `EffectivePermissionsReadModelFreshnessTests.cs` (Story 2.5 — stale/unavailable read-model behavior).
  - `EffectivePermissionsMetadataLeakageTests.cs` (Story 2.5 — leakage sentinel coverage).
  - `EffectivePermissionsTestSupport.cs` (shared seeded state).
- `tests/Hexalith.Folders.Server.Tests/EffectivePermissionsEndpointTests.cs` (Story 2.5 — REST contract alignment).
- `tests/Hexalith.Folders.Tests/Aggregates/{Folder,Organization}/` exercises Stories 2.2 and 2.3 aggregate behavior.
- `tests/Hexalith.Folders.Tests/Projections/TenantAccess/` covers projection from Story 2.1.

### Production Surface Snapshot

- `src/Hexalith.Folders/Authorization/` has 28 files: `AuthorizationOrder.cs`, `TenantAccessAuthorizer.cs` + supporting types, the 21-file `EffectivePermissions*` surface, and the `InMemoryEffectivePermissionsReadModel.cs` in-memory adapter.
- Not yet present (story 2-6 / 2-7 scope): `FolderAclAuthorizer.cs`, `PathPolicyAuthorizer.cs`, `LayeredFolderAuthorizationService` (or equivalent), EventStore validator wrapper, Dapr policy evidence provider, folder-lifecycle inspection handler.

### Coverage Gaps Already Visible

- No tests yet for the layered authorization composition (Story 2-6 is ready-for-dev). The TenantAccessAuthorizer and EffectivePermissions* surfaces are tested in isolation; cross-layer ordering and denial-mapping symmetry are unproven.
- No tests yet for folder lifecycle/binding inspection (Story 2-7 is ready-for-dev). Archive interactions (Story 2-8) have no current test coverage in `tests/Hexalith.Folders.Tests/Aggregates/Folder/` beyond aggregate-shape smoke.
- Tenants event reaction (Story 2-9): the `Hexalith.Folders.Workers.Tests` project exists but I have not yet enumerated its content — gap to revisit during coverage planning.
- Cross-tenant leakage corpus (`tests/fixtures/audit-leakage-corpus.json`) is consumed by the existing leakage tests but the catalog of channels where it must apply expands with every story. Worth a TR (Trace) pass after this TD lands.

### Open Inputs Confirmed With User

None outstanding. Jerome confirmed Epic 2 full surface as the scope. No additional documents requested.

## Step 03 - Risk and Testability

### 2026-05-19 Risk Assessment

Mode is Epic-Level, so the system-level testability review section is skipped (no new ASRs derived). Risk inventory follows the probability × impact scoring from `probability-impact.md` and the risk-governance categorization. Each risk targets a real failure mode — not a feature — anchored to story acceptance criteria, NFRs, and project-context invariants.

### Risk Categories Used

- **SEC** — Security and tenant isolation (NFR1–NFR10).
- **DATA** — Data integrity, determinism, idempotency, replay (NFR11–NFR20, NFR47–NFR55).
- **TECH** — Architectural fragility, layering violations.
- **PERF** — Performance and latency-derived information leakage (NFR21–NFR31).
- **BUS** — Business logic correctness.
- **OPS** — Operability, observability, cross-surface parity.

### Scoring Scale (recap)

- Probability: 1 Unlikely / 2 Possible / 3 Likely.
- Impact: 1 Minor / 2 Degraded / 3 Critical.
- Score = P × I. Action: 1–3 DOCUMENT, 4–5 MONITOR, 6–8 MITIGATE, 9 BLOCK.

### Risk Register (Epic 2 surface)

| ID | Category | Risk | P | I | Score | Action | Anchor / NFR / AC |
|---|---|---|:-:|:-:|:-:|---|---|
| R-01 | SEC | **Layered authorization order leaks resource existence before short-circuiting denial** — any layer that reaches resource lookup (folder stream name, ACL projection key, audit subject) before an earlier denial wins is a P0 cross-tenant leak. | 3 | 3 | **9** | **BLOCK** | Story 2.6 AC1/AC2; NFR1/2/6; project-context "authorization-before-touch" |
| R-02 | SEC | **Effective-permissions cache reuse across principals/tasks/tenants** — memoized allow result reused for a different principal, task, or watermark would cross authorization boundaries. | 2 | 3 | 6 | MITIGATE | Story 2.5 advanced-elicitation finding; Story 2.6 AC9 (memoization scope) |
| R-03 | SEC | **Stale tenant-access projection allows mutation** — fail-closed-on-stale must hold for every mutation path; bounded stale reads only when policy explicitly allows. | 2 | 3 | 6 | MITIGATE | Story 2.1 ACs; NFR2/NFR16; AR-AUTHZ-03 |
| R-04 | SEC | **Revocation freshness gate bypassed** — folder revoke must take effect within C7 budget on held locks and pre-existing sessions; stale revoke evidence cannot back an "allowed" answer. | 2 | 3 | 6 | MITIGATE | Story 2.4 AC; Story 2.5 AC6; C7; AR-AUTHZ-04 |
| R-05 | SEC | **Tenant authority sourced from request payload** — payload tenantId trusted as authority instead of input; spoofing potential. | 2 | 3 | 6 | MITIGATE | Story 2.3 AC; Stories 2.5 AC3, 2.6 AC2; AR-AUTHZ-05 |
| R-06 | SEC | **Safe denial envelopes leak resource existence** — 401 vs 403 vs 404 derived from actual resource lookup (instead of policy + decision snapshot); same-tenant-unauthorized externally distinguishable from cross-tenant. | 2 | 3 | 6 | MITIGATE | Story 2.6 AC10, denial mapping table; Story 2.5 AC4 |
| R-07 | SEC | **Metadata leakage across emit channels** — names, emails, paths, tokens, secrets appearing in logs/traces/metrics labels/events/projections/audit/Problem Details/diagnostics. | 2 | 3 | 6 | MITIGATE | NFR4/5/55; AR-AUDIT-02; sentinel corpus enforcement |
| R-08 | SEC | **Worker handler idempotency on Tenants events** — replays cause duplicate ACL updates or phantom grants; user-removed event must revoke effectively. | 2 | 3 | 6 | MITIGATE | Story 2.9 ACs; NFR16; AR-WORKER-04 |
| R-09 | SEC | **Credential / token / URL leakage in folder status responses** — provider credentials, embedded-credential URLs in lifecycle/binding query. | 2 | 3 | 6 | MITIGATE | Story 2.7 AC; NFR4/8/9 |
| R-10 | SEC | **Principal-class confusion in ACL evaluation** — delegated service agents handled as users (or vice-versa); baseline grants inherit incorrectly. | 2 | 3 | 6 | MITIGATE | Story 2.2 AC; Story 2.5 AC5/AC9 (principal source classes) |
| R-11 | DATA | **Read-model determinism breaks under replay** — projections from empty must reproduce identical permission/ACL/lifecycle state; non-determinism affects audit reconstruction. | 2 | 3 | 6 | MITIGATE | NFR52; AR-AUDIT-01 |
| R-12 | DATA | **Conflicting stale grant vs fresh revoke ordering** — projection ordering causes a revoke to disappear behind a stale grant. | 2 | 3 | 6 | MITIGATE | Story 2.4 AC; Story 2.5 AC5/AC6 |
| R-13 | DATA | **ACL state divergence between aggregate and projection** — apply logic mismatch produces phantom grants/revokes. | 2 | 3 | 6 | MITIGATE | Stories 2.2/2.4 ACs; NFR52 |
| R-14 | SEC | **Audit-leakage corpus regression on new emit channels** — every new log/trace/metric/event/audit/error must iterate `tests/fixtures/audit-leakage-corpus.json`; new channels added by 2-6/2-7/2-9 risk being skipped. | 2 | 3 | 6 | MITIGATE | AR-AUDIT-02; project-context redaction rules |
| R-15 | OPS | **Layered authorization adapters drift across REST/SDK/CLI/MCP** — Story 2.6 prescribes a single denial-mapping policy and shared seams; CLI/MCP adapters may copy logic and drift. | 2 | 2 | 4 | MONITOR | Story 2.6 AC13; NFR37/NFR40/NFR51 |
| R-16 | DATA | **Canonical ordering / deduplication of effective-permissions output** — duplicate projection rows or unordered inputs produce distinct serialized responses; breaks parity testing. | 2 | 2 | 4 | MONITOR | Story 2.5 advanced-elicitation; AC5/AC9 canonical response term |
| R-17 | OPS | **Freshness metadata accuracy in EffectivePermissions responses** — `projectionWatermark` / `observedAt` / `stale` must reflect actual read-model state, not hardcoded fresh. | 2 | 2 | 4 | MONITOR | Story 2.5 AC6/AC11 (FreshnessMetadata) |
| R-18 | OPS | **Tenant event subscription degradation invisible** — Dapr pub/sub interruption ages local projection without an operational signal. | 2 | 2 | 4 | MONITOR | Story 2.1; NFR53/NFR54; AR-AUTHZ-03 |
| R-19 | PERF | **Timing-based existence leakage** — even with identical bodies, response timing for authorized vs unauthorized lookups reveals existence; bounded timing buckets required. | 2 | 2 | 4 | MONITOR | Story 2.6 AC11 (no unbucketed latency); NFR2 |
| R-20 | OPS | **Dapr policy evidence missing in dev / production mismatch** — local fake passes; deployed policy rejects (or vice versa). | 2 | 2 | 4 | MONITOR | Story 2.6 AC8; AR-AUTHZ-01 production layering |
| R-21 | BUS | **Hierarchy / name treated as folder identity** — rename or path change breaks folder behavior because identity drifted from opaque ULID. | 2 | 2 | 4 | MONITOR | Story 2.3; project-context "folder hierarchy projected, not identity" |
| R-22 | BUS | **Archive transition not enforced across future commands** — archived folder accepts workspace/lock/file commands; C6 transition rules drift. | 2 | 2 | 4 | MONITOR | Story 2.8 ACs; C6 transition matrix; NFR12 |
| R-23 | DATA | **Action vocabulary drift across surfaces** — non-canonical action tokens (camelCase, alias) reach the action catalog through CLI/MCP. | 2 | 2 | 4 | MONITOR | Stories 2.2/2.5/2.6; strict lower-snake-case action set |
| R-24 | SEC | **ACL row metadata leakage in events/audit** — display names, emails, role labels in events not just responses. | 2 | 2 | 4 | MONITOR | Stories 2.2/2.4/2.5 AC10/AC11; NFR4 |
| R-25 | DATA | **Folder identity (ULID) collision** — astronomically unlikely but worth deterministic generator + cross-stream uniqueness check. | 1 | 3 | 3 | DOCUMENT | Story 2.3; project-context opaque-identity rule |
| R-26 | TECH | **Reserved `system` tenant misuse** — managed folder streams must never carry `system` tenant. | 1 | 3 | 3 | DOCUMENT | project-context aggregate stream rule; AR-DOMAIN-01/02 |
| R-27 | SEC | **Tenant prefix missing on cache/idempotency/durable keys** — C10 cache-key invariant violation. | 1 | 3 | 3 | DOCUMENT | C10; AR-IDEMP-03; CI lint already in place (`run-governance-completeness-gates.ps1`) |
| R-28 | OPS | **Selective Tenants event handling drifts** — non-`folders.*` config keys handled instead of skipped. | 1 | 2 | 2 | DOCUMENT | Story 2.9 AC; AR-WORKER-04 |

### Risk Action Summary

- **1 BLOCK risk (score 9):** R-01 (layer ordering / short-circuit). Must have automated tests that prove no resource touch precedes the deciding denial, and gate-fail if regressed. Story 2.6 is the natural home; tests must land *with* implementation, not after.
- **13 MITIGATE risks (score 6):** R-02 through R-14. Each maps to mandatory test coverage at unit, integration, or contract level — see coverage plan in step 04. Owners: domain (R-02–R-14 SEC/DATA) sit with Murat + dev pair on each story; cross-cutting R-07/R-14 sit with the redaction sentinel CI gate.
- **10 MONITOR risks (score 4):** R-15 through R-24. Tests required but secondary priority; tolerate post-launch incremental coverage if a gate forces it.
- **4 DOCUMENT risks (score 1–3):** R-25 through R-28. Smoke + lint coverage sufficient; some already enforced by existing CI lint scripts.

### Top Three Highest-Leverage Risks (Where Test Effort Pays Most)

1. **R-01 — Layered authorization order short-circuit (BLOCK).** This is the foundational tenant-isolation invariant for Epic 2. A single test miss here unlocks every other risk on this list. Story 2.6 ATDD scaffolding is the right venue; the existing `EffectivePermissionsAuthorizationGateTests` proves the pattern is testable in-memory.
2. **R-04 — Revocation freshness gate (MITIGATE, 6).** The "stale grant beats fresh revoke" path is the most subtle authorization defect class because tests must drive *both* a fresh-revoke clock and a stale-grant projection at once. Existing `EffectivePermissionsRevocationFreshnessTests` already covers the query side; Stories 2.4 and 2.9 need parity tests for the mutation and event-driven paths.
3. **R-11 — Read-model determinism on replay (MITIGATE, 6).** Replay tests are the single best way to catch ordering- and uniqueness-related defects in projections. With ACL + permission + lifecycle projections all in scope, a replay corpus that covers Stories 2.2/2.4/2.5/2.8 events would close R-12/R-13/R-22 in one swing.

### Testability Notes Worth Flagging (No Mode-Switch Required)

- The `EffectivePermissionsTestSupport` pattern (shared seeded read-model state) is the right primitive for the rest of Epic 2 — extend it for Stories 2.6/2.7/2.9 rather than inventing per-story fakes.
- `IEffectivePermissionsReadModel` + `InMemoryEffectivePermissionsReadModel` is a good seam template — Stories 2.6 (Dapr policy evidence provider, EventStore validator wrapper) and 2.9 (Tenants event handler) should adopt the same interface-plus-in-memory pattern so unit tests never need sidecars.
- Bounded timing buckets (R-19) are testable but require a fakeable clock and a histogram-asserting helper. Worth adding to `src/Hexalith.Folders.Testing/` if Story 2.6 introduces them as a first-class evidence field.
- The audit-leakage corpus must list every emit channel; consider an "emit channel inventory" yaml or `[Trait]`-marked tests so new channels visibly require sentinel coverage.

## Step 04 - Coverage Plan

### 2026-05-19 Coverage Matrix

Test levels for this stack:

- **Unit** (xUnit v3 + Shouldly + NSubstitute, no I/O) — pure domain, validators, ordering logic, mappers, in-memory read-model adapters.
- **Integration** (`WebApplicationFactory<TEntryPoint>` + `Aspire.Hosting.Testing` + Testcontainers) — REST endpoints, projection lifecycle, Dapr policy seam, worker handlers.
- **Contract** (OpenAPI parity + sentinel corpus + parity-oracle outputs) — server-vs-spine alignment, response shape, denial-mapping symmetry, leakage corpus sweep.
- **E2E (browser)** — deferred. Operations console is read-only MVP without stable selector contracts; not in Epic 2 scope.

#### Coverage Mapping (Risk → Scenarios → Level → Priority)

##### Story 2.1 — Stand up domain service host with Tenants integration (`done`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Tenant-access projection fail-closed for stale/unavailable evidence on mutation | Unit + Integration | P0 | R-03 | Existing `TenantAccessAuthorizerTests` covers gate logic; **gap**: integration test with fakeable clock that ages projection past freshness threshold and asserts mutation rejection. |
| Subscription degradation surfaces operational signal (health check / status) | Integration | P2 | R-18 | Not yet covered; add health-check probe test. |

##### Story 2.2 — Organization aggregate ACL baseline (`done`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Principal-kind matrix: `user`, `group`, `role`, `delegated_service_agent` resolved without class confusion | Unit (`[Theory]`) | P0 | R-10 | **Gap**: explicit matrix test. Some coverage today through layering tests but principal-kind axis needs a dedicated theory. |
| Replay corpus → projection state matches aggregate ACL state | Integration | P1 | R-13, R-11 | **Gap**: shared replay corpus test missing. |
| Action token parser rejects non-canonical inputs (camelCase, alias, unknown) | Unit | P1 | R-23 | Likely covered indirectly; verify explicit negative tests exist. |
| ACL events redact display names / emails / role labels | Unit (sentinel sweep) | P0 | R-07, R-24 | **Gap**: extend leakage sentinels to ACL grant/revoke event payloads. |

##### Story 2.3 — Create folders within a tenant (`done`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Payload tenantId conflicting with authenticated tenant → safe denial before resource touch | Integration (Server.Tests) | P0 | R-05 | **Gap**: explicit conflict test on `CreateFolder`. |
| Stream-name builder rejects reserved `system` tenant | Unit | P0 | R-26 | **Gap**: explicit negative test on stream-name validator. |
| Folder rename/path change leaves aggregate identity unchanged | Unit | P2 | R-21 | Smoke-level OK. |
| Opaque ULID generator collision smoke (statistical) | Unit | P3 | R-25 | Document-only; existing entropy is sufficient. |

##### Story 2.4 — Grant and revoke folder access (`done`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| C7 revocation propagates within budget on held authorization decisions | Unit (fakeable clock) + Integration | P0 | R-04 | Partially covered by `EffectivePermissionsRevocationFreshnessTests`; **gap**: same proof on the mutation path (not just query). |
| Stale grant vs fresh revoke: revoke wins regardless of projection input order | Unit | P0 | R-12, R-04 | Partially covered; ensure ordered-row matrix test explicitly. |
| Grant/revoke event payloads pass sentinel-corpus sweep | Unit | P0 | R-07, R-24 | **Gap**: confirm sentinel sweep iterates these payloads. |

##### Story 2.5 — Inspect effective permissions (`review`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Authorization-before-observation: every denied evidence path prevents folder/ACL/task/audit/projection lookup | Unit (spies) | P0 | R-01 (component-level), R-05 | Covered by `EffectivePermissionsAuthorizationGateTests`. |
| Cache/memoization scope: principal × tenant × folder × task × watermark — no result reuse across axes | Unit | P0 | R-02 | **Gap**: explicit cross-principal cache-isolation test (advanced-elicitation finding). |
| Safe denial envelopes externally indistinguishable across cross-tenant / unauthorized-same-tenant / not-found-to-caller / projection-unavailable | Unit + Contract | P0 | R-06 | Partial coverage; **verify** byte-identical response body assertions. |
| Canonical output: shuffled input rows + duplicate grants/revokes / duplicate memberships → identical serialized response | Unit | P1 | R-16 | Verify dedicated canonicalization theory exists. |
| `FreshnessMetadata` reflects actual read-model watermark, not a fixed value | Unit | P1 | R-17 | **Gap**: assert `projectionWatermark`/`observedAt` change with seeded read-model state. |
| Read-model unavailable / stale produces `read_model_unavailable` / `projection_stale` without aggregate fallback | Unit (spies) | P0 | R-06, R-11 | Covered by `EffectivePermissionsReadModelFreshnessTests`. |
| Server contract alignment: response shape matches OpenAPI `EffectivePermissions` + Problem Details for 401/403/404/503 | Contract | P0 | R-06 | Covered by `EffectivePermissionsEndpointTests`. |
| Cross-tenant matching folder/principal/task IDs cannot reach foreign tenant data | Unit | P0 | R-01, R-05 | Covered; verify breadth. |

##### Story 2.6 — Enforce layered authorization with safe denials (`ready-for-dev`) **[carries the BLOCK risk]**

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Layer short-circuit proof: spy on every later layer; first denial prevents subsequent layer invocation AND any protected key/path derivation | Unit (spies) | **P0 (BLOCK gate)** | **R-01** | **Must land with implementation.** This is the gate-blocker test. |
| Canonical layer order (JWT → ClaimTransform → TenantAccess → FolderAcl → EventStoreValidator → DaprPolicy) enforced mechanically | Unit | P0 | R-01 | Must be a single ordered type, not caller discipline. |
| Denial-mapping table parameterized test: every row in the mapping table → correct status + body shape + retryability | Unit + Contract | P0 | R-06, R-15 | Parameterized `[Theory]` over the table; same policy shared across REST/SDK/CLI/MCP. |
| Per-request memoization isolation: no result bleed across tenant/actor/action/scope/watermark axes | Unit | P0 | R-02, R-01 | Extends Story 2.5 cache-isolation pattern. |
| Authorization decision snapshot is immutable, one-per-evaluation, contains only safe metadata fields | Unit | P0 | R-07 | Snapshot shape test. |
| Dapr policy evidence missing → fail closed (with deterministic fake provider for unit tests) | Unit | P1 | R-20 | Adopt in-memory pattern like `InMemoryEffectivePermissionsReadModel`. |
| EventStore validator rejection mapped to canonical category without leaking stream/state internals | Unit | P0 | R-07, R-15 | Verify validator wrapper test. |
| Bounded timing buckets: all denial responses round into configured bucket regardless of which layer denied | Unit | P2 | R-19 | Requires fakeable clock + histogram helper. |
| Cross-surface parity oracle row generated from layered-auth output | Contract | P1 | R-15 | Feeds `tests/fixtures/parity-contract.yaml`. |

##### Story 2.7 — Inspect folder lifecycle and binding status (`ready-for-dev`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Lifecycle/binding response sentinel sweep — provider tokens, embedded-credential URLs, secret material absent | Unit + Contract | P0 | R-09, R-07 | Required. |
| Lifecycle freshness metadata accurate (active/archived/unbound/provider-bound) | Unit | P1 | R-17, R-22 | New `FreshnessMetadata` assertions. |
| Unauthorized reader sees identical safe-denial envelope as cross-tenant request | Unit | P0 | R-06 | Inherits denial-mapping policy from Story 2.6. |

##### Story 2.8 — Archive folders with audit preservation (`ready-for-dev`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| C6 transition matrix: archived folder rejects every workspace/lock/file/commit/binding-change command with `state_transition_invalid` | Unit (`[Theory]` over the matrix row) | P0 | R-22 | New matrix test against `FolderStateTransitions.cs`. |
| Archived folder's audit + status remain queryable under retention class C3 | Integration | P1 | R-11, R-22 | Requires replay or projection rebuild. |
| Archive event payload sentinel sweep | Unit | P1 | R-07, R-24 | Extend leakage corpus. |

##### Story 2.9 — React to Tenants events through Worker handlers (`ready-for-dev`)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Worker handler idempotency: replay tenant event twice → single projection update + correlation/causation persisted | Unit + Integration | P0 | R-08, R-11 | Use `Hexalith.Folders.Workers.Tests`. |
| `TenantDisabled` and `UserRemovedFromTenant` propagate revocation effective within C7 budget on held locks | Integration | P0 | R-08, R-04 | Fakeable clock + lock fixture. |
| Selective filter: `folders.*` keys processed, non-`folders.*` keys silently skipped | Unit | P1 | R-28 | Theory over key prefixes. |
| Worker handler emits metadata-only events / no foreign-tenant data | Unit | P0 | R-07 | Sentinel sweep. |

##### Cross-Cutting (Epic 2 scope)

| Scenario | Level | Priority | Risk(s) | Status |
|---|---|---|---|---|
| Audit-leakage sentinel sweep across every new emit channel from 2-6/2-7/2-9 | Unit + Contract gate | P0 | R-14, R-07 | Extend `run-safety-invariant-gates.ps1` channel inventory. |
| Replay determinism corpus across Stories 2.2/2.4/2.5/2.8 events → identical projection | Integration | P0 | R-11, R-12, R-13, R-22 | Single corpus closes four risks; high leverage. |
| Cache-key tenant prefix lint (continued) | Build/CI lint | DOC | R-27 | Already enforced by `run-governance-completeness-gates.ps1`; no new test work. |
| Parity oracle ingests authorization output for REST/SDK/CLI/MCP rows | Contract | P1 | R-15 | Feeds existing parity-oracle generator. |

### Coverage Count Summary

- **P0:** ~25 atomic scenarios (≈55%). Most concentrated in Stories 2-5, 2-6, 2-9 and the cross-cutting replay/sentinel corpora.
- **P1:** ~20 atomic scenarios (≈30%).
- **P2:** ~6 atomic scenarios (≈9%).
- **P3:** ~3 atomic scenarios (≈6%).

### Execution Strategy (PR / Nightly / Weekly)

- **PR gate (target <15 min):** every unit test in `Hexalith.Folders.Tests`, `Hexalith.Folders.Server.Tests`, `Hexalith.Folders.Testing.Tests`, `Hexalith.Folders.Workers.Tests`. Lightweight integration tests that use in-memory fakes (no Testcontainers/Aspire bootstrapping) also run here. Sentinel sweep, OpenAPI parity, and the three governance gate scripts (`run-safety-invariant-gates.ps1`, `run-governance-completeness-gates.ps1`, `run-contract-spine-gates.ps1`) run as blocking gates.
- **Nightly:** `Hexalith.Folders.IntegrationTests` with Aspire test host + Testcontainers (Dapr policy evidence, EventStore projection lifecycle, Tenants worker handler integration). Replay determinism corpus runs here because the corpus grows large. Provider contract drift (already nightly).
- **Weekly:** Live provider drift against real GitHub/Forgejo (already weekly). Performance/capacity smoke against bounded MVP inputs — not Epic 2 scope but worth planning around.

### Resource Estimates (Ranges Only)

Backend-only stack, existing scaffolding leverage, mostly-unit work for authorization logic:

- **P0:** ~25–40 hours (≈25 scenarios × 1–1.5h, weighted toward Story 2.6 BLOCK gate test + replay corpus + sentinel extensions).
- **P1:** ~15–25 hours (≈20 scenarios × 0.75–1.25h).
- **P2:** ~4–8 hours.
- **P3:** ~1.5–2 hours.
- **Total:** ~45–75 hours. Spread over 2–3 sprint weeks if one developer-pair takes Stories 2-6 and the cross-cutting tests; faster if split across the Stories 2-6/2-7/2-8/2-9 dev pairs as part of each story's DoD.

### Quality Gates

Gate decision uses the existing PASS / CONCERNS / FAIL / WAIVED model anchored to risk-governance scoring:

- **P0 pass rate:** 100%. R-01 (BLOCK) test must be green before any Story 2-6 PR merges; any P0 regression fails the PR gate.
- **P1 pass rate:** ≥95%. Up to one P1 scenario may carry an explicit, owner-named WAIVED status with expiry ≤ next sprint.
- **Coverage thresholds (new authorization code):** ≥85% line + branch coverage on `src/Hexalith.Folders/Authorization/*` and any new code from Stories 2-6/2-7/2-9. Aligns with the project's existing `coverlet` discipline.
- **Sentinel corpus:** zero leaks across the channels that exist in the inventory. Adding a channel without sentinel coverage = automatic CONCERNS, gate FAIL on the next sprint if not closed.
- **Replay determinism:** corpus must produce byte-identical projection state across two cold runs. Non-determinism is automatic FAIL.
- **Existing gate scripts remain green:** `run-safety-invariant-gates.ps1`, `run-governance-completeness-gates.ps1`, `run-contract-spine-gates.ps1`.
- **High-risk mitigations:** every score-6 risk has a named owner and at least one test scenario from this matrix before the corresponding story moves out of `review`/`ready-for-dev`.

### Open Questions for Jerome

- **Replay corpus scope** — happy to seed it from existing Story 2.2/2.4/2.5 test fixtures, but a yes/no on whether `tests/fixtures/event-replay-corpus.json` is the preferred location vs. `tests/fixtures/parity-contract.yaml`-style scheme.
- **Bounded timing buckets** — should I scope these as a first-class evidence field in Story 2-6, or defer to a separate hardening story?
- **Story 2-6 ATDD** — would you like the BLOCK-gate test (`R-01`) pre-authored via the `AT` workflow before Story 2-6 development starts? That's the standard red-tests-first move for a P0 blocker.

## Step 05 - Generate Output

### 2026-05-19 Generation Completed

### Execution Mode

- Requested: `auto` (config `tea_execution_mode`).
- Resolved: `sequential` (no subagent/agent-team request from Jerome; single-document Epic-Level output runs as single worker by default per the workflow).

### Output Path

- `_bmad-output/test-artifacts/test-design/test-design-epic-2.md` — final Epic 2 test design document populated from `test-design-template.md`.

### Population Summary

- Filled every template section: Executive Summary, Not in Scope, Risk Assessment (high/medium/low), Entry/Exit Criteria, Test Coverage Plan (P0/P1/P2/P3 with risk-link columns), Execution Order, Resource Estimates, Quality Gate Criteria, Mitigation Plans (top 7 score-6+ risks), Assumptions/Dependencies, Follow-on Workflows, Approval, Interworking & Regression, Appendix.
- Optional "Project Team" section omitted (no roles/names yet assigned beyond Jerome + Murat).
- Risk register: 28 risks (1 BLOCK + 13 MITIGATE + 10 MONITOR + 4 DOCUMENT).
- Coverage matrix: ~54 atomic scenarios mapped to risks, level, owner.
- Resource estimate: ~45–75 hours total (~6–9 days dev-pair).

### Validation Against Checklist

Workflow checklist applied:

- [x] Risks tagged with category, P, I, score; high risks have mitigation, owner, timeline.
- [x] Coverage matrix has no duplicate level/scenario overlap (P0 unit tests do not redundantly cover what integration carries, etc.).
- [x] P0–P3 priorities map to risk scores per `test-priorities-matrix.md`.
- [x] Execution strategy is PR/Nightly/Weekly with no test re-listing.
- [x] Resource estimates use ranges (no false precision).
- [x] Quality gates set: P0 100%, P1 ≥95%, sentinel zero leaks, replay determinism FAIL-on-non-deterministic.
- [x] CLI / browser sessions: N/A (backend-only design; no Playwright CLI used).
- [x] Temp artifacts stored under `{test_artifacts}/test-design/` (the workflow's designated location).
- [x] No system-level mode artifacts generated (Epic-Level mode is one document; no handoff document required).

### Polish Pass

- Consolidated duplicate language between the progress file and final document by keeping the progress file as the working log and the final document as the authoritative deliverable.
- Confirmed terminology consistency (R-01 always referenced as BLOCK gate / score 9; FreshnessMetadata fields capitalized consistently; story numbering uses dotted form 2.1–2.9 in epic refs and dashed form 2-1 → 2-9 in story-file refs to match existing artifact naming).
- No orphaned references (every risk in the register appears in the coverage matrix and vice versa).

### Completion Report

- **Mode used:** Epic-Level Create, sequential execution.
- **Output:** `_bmad-output/test-artifacts/test-design/test-design-epic-2.md`.
- **Key risks:** 1 BLOCK (R-01 — layered authorization layer short-circuit, owned by Story 2-6 dev pair). 13 MITIGATE risks centered on cache reuse (R-02), revocation freshness (R-04), tenant authority sourcing (R-05), denial envelope leakage (R-06), metadata leakage (R-07), worker idempotency (R-08), credential leakage (R-09), principal-class confusion (R-10), replay determinism (R-11), ACL state divergence (R-12/R-13), and channel inventory completeness (R-14).
- **Gate thresholds:** P0 100%, P1 ≥95%, ≥85% line+branch coverage on new authorization code, sentinel sweep zero leaks, replay determinism FAIL-on-non-deterministic.
- **Open assumptions awaiting Jerome's confirmation:**
  1. Replay corpus file location (`tests/fixtures/event-replay-corpus.json` vs alternative).
  2. Whether bounded timing buckets land in Story 2-6 or a hardening follow-up.
  3. Whether the R-01 BLOCK-gate test gets pre-authored via the `AT` workflow before Story 2-6 development begins (recommended).
