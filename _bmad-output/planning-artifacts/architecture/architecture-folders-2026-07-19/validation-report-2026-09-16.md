# Architecture Validation Report — 2026-09-16

**Target:** `_bmad-output/planning-artifacts/architecture.md` (1857 lines · `status: complete` · `updated: 2026-09-15` · `reconciledTo: sprint-change-proposal-2026-09-15`)
**Intent:** Validate (standalone critique — **no edits were made to `architecture.md` or any project file**)
**Scope ratified by Jerome:** whole document, six lenses
**Run workspace:** `_bmad-output/planning-artifacts/architecture/architecture-folders-2026-07-19/`

---

## Gate verdict — **FAIL**

The document is *directionally sound and materially better than it was on 2026-09-15*, but it cannot yet be accepted as mechanism authority: the flagship PD10 denial-envelope correction names wire values that exist in no vocabulary, PD11's guard-discriminated lifecycle model landed in one paragraph while four other normative statements still contradict it, and the document's own `✅ Validation Results` section certifies coverage the body no longer supports.

| Lens | Verdict | Crit | High | Med | Low |
| --- | --- | ---: | ---: | ---: | ---: |
| Adversarial divergence *(configured floor)* | **FAIL** | 3 | 5 | 4 | 0 |
| Technology currency *(configured floor)* | **FAIL** | 0 | 6 | 6 | 3 |
| Rubric walker | **FAIL** | 3 | 9 | 7 | 2 |
| Security & authorization | **FAIL** | 1 | 8 | 8 | 3 |
| Code reality / brownfield | **FAIL** | 4 | 4 | 4 | 2 |
| Authority conformance | **PASS-WITH-FINDINGS** | 1 | 5 | 5 | 3 |
| **Total** | **FAIL** | **12** | **37** | **34** | **13** |

Mechanical pass (`lint_spine.py`): **clean** — all 9 hits are false positives (a quoted `"TBD"` inside a *resolved* C0 row; `{tenant}`/`{domain}`/`{env}` are intended pattern placeholders).

---

## The one-paragraph story

Two failure modes account for almost every critical finding, and they are opposites.

**The 2026-09-15 amendment is honest but incompletely propagated.** Three lenses independently verified the new *Current reality* table (`:240–243`) row by row and could not falsify it; the five routed `Open —` items, the planning-consistency invariant, and the per-row NFR ownership recital all check out. But each fix "landed in one paragraph and left its contradiction standing elsewhere in the same document" — which is the most dangerous shape for a spine, because a reviewer who checks the amended paragraph reasonably believes the item is closed.

**The unamended 2026-05 strata were never re-verified.** Every code-reality critical, and both "named technology that does not exist" findings, sit in material written in May and carried forward untouched through every amendment: the project tree, the requirements-to-structure mapping, the I-3/I-8/C10 enforcement rows, and the version block stamped *"verified on NuGet 2026-05-09"*.

---

## Convergence — where independent lenses agreed

Findings corroborated by lenses that did not see each other's work. These carry the most weight in the gate.

### CV-1 · S-7's denial envelope names a vocabulary that does not exist — **4 lenses**
`AUTH-1` (critical) · `SEC-3` (high) · `ADV-4` (high) · `RUB-5` (high) — amplified by `SEC-2`, `SEC-4`, `SEC-5`, `ADV-5`, `SEC-19`

`architecture.md:640` claims both envelopes are *"fully named so the C13 `cli_exit_code` / `mcp_failure_kind` columns stay derivable"*, then names category `authorization` / code `tenant_access_denied` for the 404 and category `availability` / code `authority_unavailable` / action `retry_after_backoff` for the 503.

- The approved OQ3 matrix (`docs/contract/authorization-matrix.md:145–146`) says 404 → category `tenant_access_denied`, code `resource_unavailable`, action `no_action`; 503 → category `read_model_unavailable`, code `projection_unavailable`, action `retry`.
- `prd.md:611,1156` and `sprint-change-proposal-2026-09-15.md:321–323` agree with the matrix.
- Neither `authorization`, `availability` nor `authority_unavailable` appears in the closed canonical vocabulary (`tests/fixtures/parity-contract.schema.json` `$defs/canonical_error_category`, nor `HexalithFoldersClient.g.cs:13324`).
- The architecture **contradicts itself**: its own CLI exit-code table at `:691` maps exit 66 to canonical category `tenant_access_denied`.
- Consequence: the architecture inverts the category/code pairing and invents two categories and one code, so CLI derives exit 1 / non-retryable while MCP derives retryable for the same authority outage — the exact divergence S-7 exists to close.

**This is the single highest-priority finding in the gate.** `:645` states *"Release generation and parity gates consume only the corrected, reapproved matrix"* — so if an implementer takes S-7's values as the correction, the regenerated surface will not match the artifact A6b must reapprove.

### CV-2 · PD11's guard model landed in one paragraph; four normative statements still contradict it — **5 lenses**
`ADV-1`, `ADV-2` (critical) · `RUB-4`, `REAL-5`, `REAL-7`, `AUTH-4` (high) · `ADV-9`, `ADV-10`, `ADV-11`, `AUTH-9`, `RUB-15`, `RUB-17` (medium)

`:436–437` keys the C6 gate on `(state, event, guard)` and explains precisely why a two-tuple gate is unsafe. But `:358`, `:1065`, `:1066` and the **co-normative** `docs/exit-criteria/c6-transition-matrix-mapping.md:14,41` are all still `(state, event)`-keyed — and that mapping document contains zero occurrences of "guard", "resolution" or "retryable". `:420` declares all of these "express **one** model".

The live gate `FolderStateTransitionsTests.cs:82` is pair-keyed and **passes the implementation that routes every `CommitFailed` to `failed`**, destroying staged work — the exact outcome `:437` warns about. Code reality sharpens it further: `FolderStateTransitions.cs:90-91,107-108` does not *reject* two of the five new transitions, it **accepts them onto the other branch**, so the reality table at `:240` understates the gap in the dangerous direction.

`ADV-2` is the sharpest instance: `dirty` + `WorkspaceLocked` is declared guard-discriminated with only one branch, under a rule that guards are "evaluated server-side from durable state, never from caller input" — while no declared durable field carries the staging task's identity (`stagedBy*` → 0 hits across `src/`). One reading makes the workspace permanently unlockable with no cleanup path; the other lets any principal naming the caller-supplied `X-Hexalith-Task-Id` (`:677`) resume and commit another principal's staged changes.

### CV-3 · The `✅` validation section certifies coverage that does not exist — **4 lenses**
`RUB-1` (critical) · `AUTH-5` (high) · `SEC-16`, `REAL-12`, `RUB-13`, `RUB-14`, `AUTH-11`, `AUTH-13` (medium/low)

`:58` correctly says **eleven** NFR categories. `:1708`, under a heading stamped *"Requirements Coverage Validation ✅"*, enumerates **nine** and asserts every category is bound to *"at least one CI gate or runbook"* — while all eleven NFR74–NFR84 rows in `docs/exit-criteria/nfr-traceability.md` carry `—` for automated gates. The two new bands are paraphrased once at `:272` and bound to no mechanism anywhere. This contradicts the document's own planning-consistency invariant at `:253`.

### CV-4 · Present-tense claims about CI mechanisms and technologies that do not exist — **3 lenses**
`REAL-2`, `REAL-3`, `REAL-4` (critical) · `TECH-5`, `TECH-6` (high) · `REAL-8`, `TECH-11`, `RUB-10`

All unlabelled, all in the 2026-05 strata, all counted among the shipped gates at `:1799`/`:1826`:

| Claim | Line | Reality |
| --- | --- | --- |
| I-3: `daprd` in a kind cluster + property-based negative generator, **blocks merge** | 731 | 0 hits for `kind`/`daprd` repo-wide; real gate is a static-fixture xUnit suite, and `policy-conformance.yml:3-6` is **schedule-only** so it cannot block anything |
| I-8: per-tenant token buckets + 429 chaos gate | 736 | No `RateLimiting/`, no `*TokenBucket*`; what exists is read-only posture *evidence*, not enforcement |
| C10: `Hexalith.Folders/Caching/TenantPrefixedCacheKey.cs` + a ci.yml lint job | 349 | Neither exists; the real gate is `GovernanceCompletenessGateTests:1383` over `tests/fixtures/cache-key-exceptions.yaml` — and `c0-c13-governance-evidence.yaml:188-195` already says so |
| Testcontainers in the *non-negotiable* stack | 80, 547, 577 | Zero usage in Folders |
| `oasdiff` as the C12 drift classifier + `tests/tools/oasdiff/` | 351, 755, 1043, 1516, 1714, 1799 | Exists nowhere; real lane is `run-nightly-drift-gates.ps1` + `tests/tools/forgejo-drift/` — already propagated into three `docs/` files |

### CV-5 · "49 of 50 protected operations" reproduces from nothing — **4 lenses**
`AUTH-2` (high) · `REAL-9` (medium) · `ADV-12` (medium) · `SEC-18` (low)

`:241` says 50. Every source says 49 — `authorization-matrix.md:15,341`, `prd.md:1156`, three test pins, `parity-contract.yaml` — including `architecture.md:640` itself, four hundred lines later. Both surviving hard-coded denominators sit in the sentences that forbid hard-coded denominators.

### CV-6 · PD8's carve-out relocates durable cleartext rather than removing it — **2 lenses + prior-gate carry**
`SEC-1` (critical) · `RUB-8`, `RUB-6` (high) · adversarial prior-F2 "not closed"

`:639` guarantees *"Cleartext confidential values are never made durable"* and explains why read-time redaction is unacceptable: *"every later reader — replay, export, backup, incident dump — becomes a disclosure path the redaction rule never reaches"*. Then `:643` carves out "the operational fields the lock identity and provider executor consume, which stay cleartext inside the tenant's own execution boundary". All three named consumers (lock identity, Story 12.4 Git executor, NFR79 restart recovery) require the value to be **durable** — in the same shared Dapr state store (D-1/D-2/D-3), with no separate store, key, encryption requirement or export prohibition named. A tenant-prefixed key in a shared Redis is not an execution boundary; "never emitted" constrains egress and says nothing about snapshots, backups, replicas or operator access.

### CV-7 · The corrections have no owner — **4 lenses**
`RUB-9`, `AUTH-6` (high) · adversarial prior-F7 · `SEC-16` · code-reality note 4

`epics.md` contains **zero** occurrences of PD8, PD10 or PD11 (verified independently by two lenses). No story owns the PD10 spine correction — which `:276` states plainly and honestly — while Story 13.2 owns NFR76 and would build a second deny-by-default shape beside it. NFR74 is credited to Epic 13 but no Epic 13 story owns it.

---

## Critical findings (12)

| ID | Lens | Finding | Anchor |
| --- | --- | --- | --- |
| **AUTH-1** | Authority | S-7 names a denial vocabulary contradicting the approved OQ3 matrix, the PRD and the ratified proposal; the invented tokens are in no enum | `:640` |
| **ADV-1** | Adversarial | `(state,event,guard)` keying exists in one paragraph; 4 normative statements stay pair-keyed and the live gate passes the staged-work-destroying build | `:436`, `:358`, `:1065` |
| **ADV-2** | Adversarial | `dirty`+`WorkspaceLocked` declares a guard with one branch and a server-side-only rule no durable field satisfies — bricked workspace, or cross-principal takeover of staged work | `:404`, `:436`, `:677` |
| **ADV-3** | Adversarial | Cleanup has two mutually exclusive triggers (terminal *task* closure vs workspace *state*), so one unit deletes staged content at day 7 and the other never | `:430` vs `:983` |
| **SEC-1** | Security | The PD8 operational carve-out sanctions durable confidential cleartext in an unnamed, unencrypted, shared store — falsifying S-6's headline guarantee | `:643` vs `:639` |
| **REAL-1** | Code reality | `Hexalith.Folders.EventStore` — a real deployable project hosting the A-9 adapters and serving the `eventstore` app id — has **0 occurrences** in 1857 lines | `:1548`, `:1724` |
| **REAL-2** | Code reality | I-3's merge-blocking live-sidecar policy gate does not exist; the real workflow is schedule-only | `:731` |
| **REAL-3** | Code reality | I-8's per-tenant token buckets and 429 chaos gate do not exist and are counted as shipped | `:736` |
| **REAL-4** | Code reality | C10's pinned artifact and ci.yml lint job do not exist; the governance YAML already records the real gate | `:349` |
| **RUB-1** | Rubric | The NFR coverage claim is false — 9 of the document's own 11 categories, two bands bound to nothing, under a `✅` | `:1708` vs `:58` |
| **RUB-2** | Rubric | **Disaster recovery / backup / restore is a silent dimension** — `disaster`/`RTO`/`RPO` all 0 hits, against P7Y admission records a restore could resurrect | — |
| **RUB-3** | Rubric | **Event-payload schema evolution and data migration are silent** — `upcast`/`schema evolution`/`event version` all 0 hits, in an event-sourced system currently adding a lifecycle event | `prd.md:651` |

---

## High findings (37)

**Adversarial** — `ADV-4` S-7 tokens outside the canonical vocabulary so CLI and MCP disagree on retryability · `ADV-5` `visibility` declared closed twice at two different closures on a field A-8 requires everywhere · `ADV-6` D-9's 413 retry header collides with a reserved request-side name, and `epics.md` encodes the colliding spelling · `ADV-7` three "current deployed behaviour" anchors are false at HEAD and two rank-30 stories will encode the opposite · `ADV-8` the semantic-indexing bridge has two writers (10.7 projection, 12.5 reconciler) with no arbitration — a replay empties every search result.

**Technology** — `TECH-1` MCP SDK pinned `1.3.0` in four places; repo builds `2.2.0` · `TECH-2` Aspire pinned `13.4.6`; repo is on `13.5.3` — the same SDK-vs-Hosting mismatch class as the Epic 9 DCP blocker · `TECH-3` EventStore/Tenants "3.15.1" vs actual `3.104.0`/`5.7.0` · `TECH-4` the package-management decision is inverted — `:523` forbids exactly what `release-packages.yml:131-134` does · `TECH-5` Testcontainers mandated, unused · `TECH-6` `oasdiff` named in six places, absent from the repo.

**Rubric** — `RUB-4` the declared C6 three-way lockstep has no gate and the named gate does not read the document it claims to compare · `RUB-5` S-7's envelopes are not named anywhere the C13 columns are derived from · `RUB-6` `withheld` and the `confidential` tier never reach the UX contract or F-5 · `RUB-7` degraded mode is decided twice in opposite directions (S-7/NFR76 deny-on-stale vs concern #20 serve-under-bounded-staleness) · `RUB-8` S-6's tokenizer has no owner component, no key management, and its gate points at the file S-6 says is *not* the tokenizer · `RUB-9` PD8/PD11 code-landing work is unowned at every rank while rank-30 stories list it as prerequisite · `RUB-10` structure mapping presented as complete: no home for three new mechanisms, no NFR mapping, 12 of 22 concerns · `RUB-11` write-concurrency control on the aggregate stream undecided against an NFR requiring multi-replica convergence · `RUB-12` no deployment profile, environment inventory or replica/scaling decision.

**Security** — `SEC-2` the "single 404" is two codes chosen by an unspecified predicate, re-creating the oracle inside the envelope · `SEC-3` invented categories break the derivability claimed in the same sentence and would redden `AuthorizationMatrixContractTests:202-213` · `SEC-4` absent/malformed authority routed to a **retryable** 503, inverting NFR76 deny-by-default, dropping the 401 and creating pre-auth retry amplification · `SEC-5` the remediation is scoped to `category` while the live 404 still discriminates existence through `code`, the `type` URI, `retryReasonCode`, `layer` and `timingBucket` · `SEC-6` S-7 and S-8 are jointly unsatisfiable for task-derived scope · `SEC-7` S-8's "never caller-supplied" rule is broken by `ValidateProviderReadiness`, the known SSRF sink, and no destination-denial decision exists · `SEC-8` tokenizer truncation width unspecified; rotation silently destroys the evidence joins · `SEC-9` no deny-by-default authorization binding for the HTTP surface.

**Code reality** — `REAL-5` the reality table understates PD11 in the dangerous direction (two transitions accepted, not rejected) · `REAL-6` Story 12.4 is pointed at a `NotImplementedException` that is not in the Git write path — provider writes are implemented; the real gap is fail-closed Server composition · `REAL-7` `FolderWorkspaceDirtyResolution` discriminates a different pair than the four named · `REAL-8` five FR blocks and three concerns routed to paths that do not exist.

**Authority** — `AUTH-2` "49 of 50" · `AUTH-3` `:181` still frames Story 10.9 as the body-content follow-up that PD5/A4 abolished · `AUTH-4` `unknown_provider_outcome` disposition diverges across the three artifacts declared to "express one model" · `AUTH-5` nine-category coverage claim after the 84-row relock · `AUTH-6` Epic 13 credited with NFR74 but no story owns it.

---

## Medium and low (47)

34 medium and 13 low across the six reports — counts, hard-coded denominators, stale cross-references, the `visibility` wire position, escape hatches with no enumerated set, cost as an unowned dimension, undocumented `LibGit2Sharp`/Fluent-UI/Redis/JSON-serializer decisions, and editorial residue. Full detail per lens in the review files listed below.

---

## Cross-lens conflicts, reconciled

Honest disagreements between lenses, resolved on the evidence rather than averaged:

1. **"Named tech is verified-current"** — the rubric walker scored this **PASS**, the technology lens **FAIL**. The rubric checked the document's *internal* consistency (which is genuinely clean — every technology appears at the same version everywhere); the technology lens checked against `references/Hexalith.Builds/Props/Directory.Packages.props`. **The technology lens is correct**; the rubric's PASS on checklist item 4 should be read as superseded by `TECH-1`–`TECH-6`.
2. **Size of the canonical error vocabulary** — adversarial says 46 members, authority says 50 in `parity-contract.schema.json` `$defs/canonical_error_category` and 49 in the generated client. The authority lens reproduced its counts from the fixtures directly and is the better source. The disagreement does not affect the finding: all four lenses agree the S-7 tokens are absent from *every* candidate vocabulary.

---

## Prior-gate closure (2026-09-15 → 2026-09-16)

The fixes applied on 2026-09-15 were **real and verified**, but partial. Each lens tracked its own prior series (the numbering is per-lens and is not merged here):

- **Authority conformance** — the prior CRITICAL is **closed, verified by execution**: `Contracts.Tests` runs **314/314, 0 failed**, and the C6 lockstep (diagram + mapping doc + pinned counts 34→41, 23→24) landed in `de281e7`. Prior F2/F3/F4/F4b/F5/F8/F9 closed; F6, F10, F12 still open.
- **Rubric** — all three prior criticals **fixed**; this run's failure set is new, not a regression.
- **Adversarial** — of 18 prior findings: **3 fully closed** (F1 S-6 derivation, F4, F16 NFR ownership), **5 half-closed**, **10 not closed**. Two are worse than before: F5 was *restated with non-existent tokens* (→ `ADV-4`) and F11 was *promoted to critical* by PD11 making the pair guard-discriminated (→ `ADV-2`).
- **Security** — S-6 derivation, `visibility` enumeration, honesty labels and A-11 migration are genuinely fixed; the OQ2 schema supersession and the hard-coded denominator are not.
- **Code reality** — every 2026-09-15 addition carries its label and survives verification. All failures are in material written in 2026-05.
- **Technology** — the prior manifest finding is fixed-as-documented (`:206` now honestly states the regeneration is owed).

---

## What is strong — a fix pass must not undo it

1. **The honesty convention works and is accurate.** The *Current reality* table (`:240–243`) was verified row by row by two lenses; not one row could be falsified. The planning-consistency invariant (`:253`) and *"admission is not implementation evidence"* (`:278`) are exactly the rules that stop a control-plane shell being reported as a product.
2. **Five routed `Open —` items** (`:214`, `:276`, `:432`, `:643`, `:651`), four naming their owner. Converting a contradiction into a routed open question is the right disposition, and the document does it well.
3. **Every digest, count and ownership cell reproduces byte-exact** — OQ3 `5ffabd71…`, C12 `5799e090…`, C7 30/15/60/60, NFR 84/11, C6 41 edges / 24 events / 11 states, all eleven NFR74–84 owner cells, §7.1 edge-for-edge with zero dropped edges, §7.2 10/10 and §7.3 8/8 homed. The `reconciledTo` claim is substantially true.
4. **The guard dimension itself (`:436–437`) is good architecture** — it identifies the four guard-discriminated pairs, requires server-side evaluation, assigns retryable/non-retryable to one shared classifier so GitHub and Forgejo cannot disagree, and explains why a two-tuple gate is unsafe. It needs **propagating, not rewriting**.
5. **A-9 / D-7 idempotency** remains the strongest section and is the template S-6 and S-7 should be written against — S-6 now correctly cites it as precedent.
6. **The `{C3, C4, C7, C12}` hard-pin, the NFR row statuses, the 41/24 C6 pins and the governance YAML statuses are all handled correctly.** Do not touch them.
7. **Repo currency is genuinely good.** Octokit 14.0.0, LibGit2Sharp 0.32.0, NSwag 14.7.1, xunit.v3 4.0.1, Dapr 1.18.7, SDK 10.0.401 and the Forgejo support matrix are all at or within one release of current. The gap is doc-vs-repo, not repo-vs-world.

---

## Recommended disposition

**Tier 1 — before the PD10 spine regeneration consumes S-7 (blocks A6b):**
`AUTH-1`/`SEC-3` restate S-7's envelopes verbatim from `authorization-matrix.md:144-147`, all three outcomes including 401, one code for the 404, no invented categories. `SEC-2`, `SEC-4`, `SEC-5` ride the same edit. This is the one finding that can propagate a wrong wire value into the artifact A6b must reapprove.

**Tier 2 — before any Epic 4 / Epic 12 lifecycle story starts:**
`ADV-1` propagate the triple keying to `:358`, `:1065`, `:1066` and the C6 mapping artifact in one change set, with an explicit default rule for unenumerated guard branches. `ADV-2` declare the durable `stagedByTaskId`/`stagedByPrincipal` fields the guard requires. `ADV-3` correct `:983` to the task-closure vocabulary. `REAL-5` correct the reality table in the *strict* direction.

**Tier 3 — decide, or declare open with an owner:**
`SEC-1` PD8 operational boundary (HMAC the lock key; re-derive the executor's ref at call time; name the control set for any surviving cleartext). `RUB-2` disaster recovery. `RUB-3` event-payload schema evolution. `RUB-11` write concurrency. `RUB-12` deployment envelope. `SEC-7`/`SEC-9` destination policy and deny-by-default binding.

**Tier 4 — mechanical sweep, no lockstep cost:**
All version pins (`TECH-1`–`TECH-4`, one version × N sites — and consider citing `Directory.Packages.props` as the owner rather than copying numbers forward), the two non-existent technologies (`TECH-5`, `TECH-6`), `REAL-1`'s missing project, the `✅` validation section (`RUB-1`, `AUTH-5`), the "49 of 50" denominators, and the remaining single-sentence authority edits.

**Structural recommendation (three lenses converged on it independently):** give §Complete Project Directory Structure, §Requirements to Structure Mapping and the I-3/I-8/C10 enforcement rows the same treatment the 2026-09-15 amendment received — a per-entry as-built-vs-target marker, or one banner declaring the tree the *target* layout and naming `Hexalith.Folders.slnx` as the authoritative as-built inventory. And stop embedding `file.cs:line` claims in the prose; replace with a pointer plus a conformance test, so the document cannot silently go stale against the tree again.

---

## Review artifacts

| Lens | Full review |
| --- | --- |
| Adversarial divergence | `reviews/review-adversarial-2026-09-16.md` (511 lines) |
| Technology currency | `reviews/review-technology-2026-09-16.md` (306 lines, 40-row verification table) |
| Rubric walker | `reviews/review-rubric-2026-09-16.md` (443 lines, 31-row dimension sweep) |
| Security & authorization | `reviews/review-security-2026-09-16.md` (478 lines, focus-area coverage table) |
| Code reality / brownfield | `reviews/review-code-reality-2026-09-16.md` (372 lines, 37-row claim probe) |
| Authority conformance | `reviews/review-authority-conformance-2026-09-16.md` (324 lines, full reproduction table) |

Run memlog: `.memlog.md` · Prior gate: `reviews/review-*-2026-09-15.md`

**No file under `src/`, `tests/`, `docs/` or `_bmad-output/planning-artifacts/architecture.md` was modified by this run.**
