# Rubric Walker Review — architecture.md, 2026-09-16

- **Reviewer lens:** Rubric Walker (good-spine checklist + dimension sweep), BMad architecture Reviewer Gate — *Validate* run
- **Subject:** `_bmad-output/planning-artifacts/architecture.md` (1857 lines, classic architecture format; decision IDs `D-`/`A-`/`S-`/`F-`/`I-`/`C-`)
- **Altitude:** INITIATIVE / whole-system. Level below: EPICS (`epics.md`). Driving spec: `prd.md` (FR1–FR58, NFR1–NFR84) + `ux-design-specification.md`
- **State reviewed:** working tree at commit `de281e7` (the 2026-09-15 amendment is now committed; no uncommitted diff on architecture.md)
- **Prior gate:** `review-rubric-2026-09-15.md` — verdict FAIL. That review ran against the 1833-line uncommitted amendment; the file was edited again at 20:09 and several fixes landed. Fix status verified item by item in §"Prior-gate verification".
- **Mode:** read-only. No project file was modified; this report is the only file written.

## Verdict: **FAIL**

Three critical items: one coverage claim the document makes about itself is false, and two whole dimensions an initiative-altitude architecture owns are silent. Eight high findings sit on top of them. This is not a regression — the 2026-09-15 amendment got materially better between 20:05 and 20:09 (honesty labels, four explicit `Open —` routes, the guard dimension, a pinned S-6 primitive, the governance-vocabulary lockstep). The remaining failures are concentrated in two places: **the validation section still asserts completeness the body no longer supports**, and **the new PD8/PD10/PD11 vocabulary stops at the section that introduces it and never reaches the tables the level below actually builds from**.

## Checklist scorecard

| # | Checklist item (verbatim from the skill) | Verdict | One-line basis |
| --- | --- | --- | --- |
| 1 | Fixes the real divergence points for the level below and misses none | **PARTIAL** | The three it picks (denial envelope, lifecycle model, write-time confidentiality) are the right ones and are well-argued; but S-7's own vocabulary never reaches the CLI exit-code table or MCP kind set (RUB-5), `withheld` never reaches the UX constraints or F-5 (RUB-6), and event-schema evolution is unaddressed (RUB-3). |
| 2 | Every rule is enforceable and actually prevents its stated divergence | **FAIL** | C6's stated measurement method (three-way identical assertion) does not exist and the drift is already live in-tree (RUB-4); S-6's C9 gate points at the component S-6 says is *not* the tokenizer (RUB-8); the guard-keyed C6 gate is stated once and contradicted twice (RUB-4b). |
| 3 | Nothing under Deferred could let two units diverge | **PARTIAL** | Deferred set is mostly safe and well-fenced (webhooks → 404; body-content needs new requirement/story/rank at line 225). But the three "reserved post-MVP" operator ops are accepted in code today (honestly labelled), and the escape hatches at S-7/NFR75 still have no enumerated set, artifact, approver or gate (RUB-19). |
| 4 | Named tech is verified-current | **PASS** | .NET 10, Aspire 13.4.6, CommunityToolkit.Aspire.Hosting.Dapr 13.4.0-preview.1.260602-0230, Octokit 14.0.0, MCP SDK 1.3.0, System.CommandLine 2.x, NSwag, OpenAPI 3.1, RFC 9457, HMAC-SHA-256, Fluent UI Blazor — all current and internally consistent (lines 575, 729, 1698). |
| 5 | Ratifies rather than contradicts the brownfield codebase | **PASS (with note)** | This is the biggest improvement since 2026-09-15. Lines 236–245 now carry an explicit "PD8, PD10, PD11 are target state, not current behavior" paragraph plus a four-row *Current reality* table, and I verified every row of it against the code — all four are accurate. Note: two live divergences are missing from that table (RUB-15). |
| 6 | Every dimension the altitude owns is decided, deferred, or an open question | **FAIL** | Disaster recovery/backup/restore (RUB-2) and event/message schema evolution + data migration (RUB-3) are silent; write-concurrency control (RUB-12) and the deployment-profile/environment/scaling envelope (RUB-13) are effectively silent. |
| 7 | If a spec drove it, it covers that spec's capabilities | **FAIL** | PRD §API Versioning requires versioning of **event payloads** (prd.md:651); architecture covers only URL versioning (A-11). PRD NFR categories number 11; the architecture's own coverage section binds 9 (RUB-1). |
| 8 | If a parent spine is inherited, no new AD weakens or contradicts an inherited one | **PARTIAL** | No parent spine document exists (this is the top). Within the document, S-7/NFR76 (deny on stale authority) contradicts inherited cross-cutting concern #20 (serve reads under bounded staleness) with no reconciliation (RUB-7). |

## Dimension sweep

Grep terms are given for every row marked SILENT. All greps run case-insensitively over the full 1857 lines.

| # | Dimension | Status | Evidence |
| --- | --- | --- | --- |
| 1 | Paradigm & style (event-sourced CQRS, Dapr sidecars, actor admission) | **Decided** | 74–77, 572–576, D-1 (619), A-9 (665) |
| 2 | Boundaries & dependency direction | **Decided** | §Architectural Boundaries 1536–1544; Memories exception rules 137, 176 |
| 3 | State mutation & persistence | **Decided** (honestly labelled unbuilt) | D-1/D-2/D-3 (619–621); 251 records that the sole `IFolderRepository` is in-memory and ADR-0001 is NoOp; Epic 12 chartered 255–266 |
| 4 | Shared-data ownership | **Decided** | §Data Boundaries 1554–1559 |
| 5 | Identity & multi-tenancy | **Decided** | 78, 534–535, concerns #1/#12/#13/#14 (103, 114–116) |
| 6 | Authentication & authorization | **Decided**, one contradiction | S-1…S-8 (634–641); PD10 correction 645; contradiction with #20 → **RUB-7** |
| 7 | API/contract strategy & versioning (interface) | **Decided** | A-1/A-3/A-11 (657, 659, 667), C0/C13 (312, 325) |
| 8 | **Event/message payload schema evolution & data migration** | **SILENT** | grep: `upcast`=0, `schema evolution`=0, `event version`=0, `event schema`=0, `payload version`=0, `backfill`=0. Only hits for "migration" are 645/651 (wire contract). PRD:651 requires it. → **RUB-3** |
| 9 | Cross-surface parity | **Decided** | §Adapter Parity Contract 669–711, C13 (325) |
| 10 | Error & failure semantics | **Partial** | A-8 (664), exit-code table (686–702), MCP kinds (704), failure taxonomy (1007–1011) — but S-7's new vocabulary reaches none of them → **RUB-5** |
| 11 | Concurrency & locking (distributed lock) | **Decided** | Concern #4 (106), §Locking 964–972, C7 (319) |
| 12 | **Write-concurrency control on the aggregate stream** (expected-version / append conflict / retry) | **SILENT** | grep: `optimistic`=0, `ETag`=0, `expected version`=0, `version conflict`=0; the three `concurren` hits (313, 621, 1514) are capacity/load-profile, not control. NFR80 requires multi-replica convergence. → **RUB-12** |
| 13 | Idempotency | **Decided** (strongest section in the document) | A-9 (665), D-7 (625), §Idempotency 950–962 |
| 14 | Eventing & messaging topology | **Decided** | D-4/D-5 (622–623), topics 910–915, I-9 (737) |
| 15 | Read models & projections | **Decided**, limitation owned | 183–195, D-10 (628), determinism concern #9 (111) |
| 16 | Data retention & lifecycle | **Decided**, approval-pending | C3 (315), D-7 (625), §Cleanup 981–986 — internally contradictory → **RUB-16** |
| 17 | Search/indexing | **Decided** | §Query Facade 172–181, 133–153, FR58 mapping 1580 |
| 18 | Frontend architecture | **Decided**, one hole | F-1…F-7 (717–723); live-refresh mechanism undecided → **RUB-20**; `withheld` missing → **RUB-6** |
| 19 | Observability & telemetry | **Partial** | I-6/I-7 (734–735), logging 1026–1031. Alerting exists only as Epic-13 scope ("wire the five declared-only alert instruments", 270); no alert/SLO ownership decision. grep `on-call`=0, `dashboard`=1 (Aspire only) |
| 20 | Testing strategy & CI gates | **Decided** (dense) | 1033–1044, 1061–1066, I-5 (733) |
| 21 | Deployment, environments & configuration | **Partial → SILENT in parts** | I-2 (730), 1687–1692, per-env appsettings 1649. But grep `staging`=0 as an environment (sole hit 404 is `changes_staged`), no environment inventory, no config-validation gate, and `deployment profile` appears once (272) undefined though OQ12/OQ13 close on it → **RUB-13** |
| 22 | Infrastructure & provider strategy | **Decided** | D-3/D-5 (621, 623), I-1…I-4 (729–732), A-6/A-7 (662–663), C12/OQ4 (324, 266) |
| 23 | Operations & runbooks | **Partial** | `docs/runbooks/` declared (1163), tenant-deletion runbook listed as a *Minor Gap* (1739), incident path F-6 (722). No runbook inventory, no ownership, no gate; "production policies maintained outside repo per ops runbook" (1650) names no artifact |
| 24 | Migration & rollout of breaking changes | **Open question (acceptable)** | 651 explicitly records v1-amendment-vs-v2, deprecation window, client rollout order as open — this is the prior gate's CRITICAL-2 correctly converted to an open item. No owner named (cf. the four other `Open —` items which route explicitly) |
| 25 | Performance & capacity | **Decided** + open evidence | C1/C2/C4/C5 (313–320), F-7 (723), OQ13 (272) |
| 26 | Scaling & replica topology | **SILENT** | grep `replica`=6 — all are "across replicas" for the admission actor (105, 123, 665, 955) or NFR80 prose (272); `autoscal`=0, `blue-green`=0, `canary`=0. NFR80 demands multi-replica convergence with no topology decision → folded into **RUB-13** |
| 27 | **Disaster recovery / backup / restore** | **SILENT** | grep `disaster`=0, `RTO`=0, `RPO`=0, `backup`=2 — one is a Phase-9 bullet fragment "runbooks; backup/recovery validation" (759), the other is the `docs/runbooks/` tree comment. No decision, owner, target, or gate, against a P7Y commit-replay retention (625) and a no-audit-erasure NFR → **RUB-2** |
| 28 | Cost | **SILENT** | grep `cost`=9, every hit is the word "cost" inside an *Alternatives considered* cell (456, 624, 626–628, 635, 638, 719, 733). No cost dimension owned → **RUB-21** |
| 29 | Key management & secret rotation | **Partial → gap** | S-5 (638) credential references; Dapr secret store (1651); JWKS refresh (635). S-6 introduces a per-managed-tenant HMAC key with a key version but decides no store, no rotation policy, no re-tokenization behaviour → **RUB-8** |
| 30 | Accessibility | **Decided** | F-3 (719), 304, `accessibility-gates` axe job (733, 1717) |
| 31 | Localization / i18n | **N/A** | Not a spec capability — UX spec has no i18n requirement (grep over `ux-design-specification.md` returns only "visual language") |

---

# Findings

Severity: **critical** = a whole owned dimension silent, or a coverage claim the document makes that is false · **high** = an unenforceable load-bearing rule or a real gap · **medium** = partial coverage · **low** = cosmetic.

---

## RUB-1 — CRITICAL — The NFR coverage claim is false: 9 of the document's own 11 categories are bound, and the two new ones are bound to nothing

**Checklist items:** 7 (primary), 6 · **Locations:** line 1708 (+ bullets 1710–1718) against line 58

Line 58 (corrected in this revision, good): *"**Eleven** NFR categories drive architecture (nine original, plus Edge Security & Deployment Hardening and Durable Operation & Release Evidence admitted 2026-09-15 as NFR74–NFR84)."*

Line 1708, under a heading stamped **"Requirements Coverage Validation ✅"**:

> "**Non-Functional Requirements Coverage:** Every NFR category (security/tenant isolation, reliability/idempotency/failure visibility, performance/query bounds, scalability/capacity, integration/contract compatibility, observability/auditability/replay, data retention and cleanup, operations console accessibility, verification expectations) is bound to specific architectural decisions and at least one CI gate or runbook."

That parenthesis enumerates **nine**. The nine bullets that follow (1710–1718) enumerate the same nine. **Edge Security & Deployment Hardening (NFR74–NFR78) and Durable Operation & Release Evidence (NFR79–NFR84) appear nowhere in the coverage validation**, and no bullet binds any of the eleven requirements to an architectural decision or a gate.

Verified against the spec: `prd.md` has eleven `###` NFR subsections (`Security and Tenant Isolation` … `Verification Expectations`, `Edge Security and Deployment Hardening`, and the Durable Operation band). `docs/exit-criteria/nfr-traceability.md:150` carries the `Durable Operation & Release Evidence | NFR79–NFR84 | 6` category row. So the document is out of step with both the PRD and the traceability artifact it defers to — while stamping the section with a ✅.

This is not merely a stale sentence. NFR74–NFR84 are eleven mechanism-neutral obligations that the architecture *does* owe mechanism for — transport hardening, destination denial, deny-by-default, credential-at-rest, untrusted content, restart survival, multi-replica convergence, true readiness, demonstrated telemetry, evidence classification, edge-security verification. Several of them are exactly the dimensions this sweep finds silent (RUB-12 multi-replica write concurrency, RUB-13 deployment profile/readiness). The document paraphrases the requirements once at line 272 and then never decides a mechanism for any of them, and the validation section papers over it.

**Fix:** either add two bullets that bind each band to concrete decisions and gates (and correct the parenthesis to eleven), or state explicitly that NFR74–NFR84 mechanism is deferred to Epic 13 and *withdraw the ✅*. The parenthesis and the bullet list must both move.

---

## RUB-2 — CRITICAL — Disaster recovery / backup / restore is a silent dimension

**Checklist item:** 6 · **Grep terms tried:** `disaster` (0), `RTO` (0), `RPO` (0), `backup` (2), `restor` (3 — all "readiness restored"/"staged changes restored"), `point-in-time` (1, inside D-3's Postgres-escalation criterion), `recovery` (present, but always *workspace* recovery)

The only trace of the dimension in 1857 lines is a fragment inside a phase bullet:

> line 759: "10. **Phase 9 — Production Hardening:** Dapr deny-by-default access control + mTLS …; OpenTelemetry exporters; runbooks; **backup/recovery validation**; full C0–C13 exit-criteria evidence."

There is no decision, no owner, no target, no artifact, and no gate. Specifically undecided: what is backed up (Dapr state store holding the event streams? snapshots? the read models? the working-copy filesystem?), restore procedure and its authorization, recovery point/time objectives, whether restore replays projections from the restored streams or rebuilds them, how idempotency admission records (which D-7 says are **not** a rebuildable projection — line 123: "`/project` replay cannot create, erase, or resurrect consumed-key authority") survive a restore, and what a restore does to the fencing tokens that serialize writers.

That last point makes the silence load-bearing rather than merely absent: D-7 retains commit replay results for **P7Y** (line 625) and concern #21 classifies idempotency admission as durable-and-fail-closed, so a restore that rolls those records back re-opens consumed keys — the exact "resurrection" failure D-7 rejects TTL-deletion to prevent. Two Epic-12 stories (12.1 durable repository, 12.6 durable admission) can reasonably make opposite assumptions here.

**Fix:** add a `DR-1`-style decision under §Infrastructure & Deployment covering backup scope per data class (event streams, snapshots, read models, admission records, working copies), RPO/RTO targets, restore authorization and procedure artifact, projection-rebuild-vs-restore policy, and the admission-record restore rule. Or declare it an explicit open question with an owner and a rank, as the document does well elsewhere (214, 276, 432, 643).

---

## RUB-3 — CRITICAL — Event/message payload schema evolution and data migration are silent, in an event-sourced system with 7-year replay retention

**Checklist items:** 6 (primary), 7 · **Grep terms tried:** `upcast` (0), `schema evolution` (0), `event version` (0), `event schema` (0), `payload version` (0), `schema version` (0), `backfill` (0), `migration` (2 — both about the *wire* contract at 645/651)

The architecture decides API versioning (A-11, line 667: "`v1` URL-versioned … breaking changes get a new major version") and stops there. The spec asks for more:

> `prd.md:651`: "Breaking changes to command/query DTOs, **event payloads**, error categories, workspace states, provider capabilities, or SDK models require explicit versioning."

Nothing in the architecture says how a persisted event payload evolves. The closest rules are a replay-coverage test (line 1036: "Every event family used in production must replay into state without missing `Apply` paths") and the envelope's `eventTypeName` (920) — neither of which is a versioning or upcasting scheme. The document does not decide whether historic events are upcast on read, whether `eventTypeName` carries a version suffix, whether an added required field is a breaking event change, or who owns the compatibility window.

This is not hypothetical at this altitude — the document is *currently in the middle of one*: PD11 adds a new lifecycle event (`LockLeaseBecameStale`, line 428) and line 649 correctly notes the wire obligation ("`FolderWorkspaceLifecycleEvent` is a published OpenAPI enum … adding the event touches the spine, the client, `previous-spine.yaml`, and the C13 inventory"). That is an *ad hoc* handling of one instance of a dimension that has no rule. Combined with D-7's P7Y commit-record retention and the replay-determinism guarantee (concern #9), code in year two must read events written in year one.

**Fix:** decide the event-payload evolution rule (additive-only + optional fields, or an upcaster pipeline keyed on `eventTypeName` version, or a stream-rewrite migration with a named tool), name where upcasters live in the structure tree, and add the gate ("replay tests run against a pinned corpus of historic payload shapes"). Also decide projection/read-model migration on schema change, which today only exists as "rebuild from events".

---

## RUB-4 — HIGH — The C6 three-way lockstep the document declares has no gate, the gate it names does not do what its name says, and the divergence is already live in the tree

**Checklist item:** 2 · **Locations:** lines 345 (C6 measurement method), 420 (one-model rule), 437 (guard-keyed gate), 1065 and 1829 (two restatements of the *old* gate)

**(a) The stated measurement method does not exist, and two of the three artifacts have already moved without the third.**

Line 420: *"This matrix, `docs/exit-criteria/c6-transition-matrix-mapping.md`, `Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`, and the lifecycle tests express **one** model; a divergence is a defect in the code or the test, never an alternative reading."*
Line 345 (Exit Criteria Operations Plan, C6 measurement method): *"the PD11 rules are asserted identically against this document, the C6 mapping artifact, and `FolderStateTransitions.cs`."*

Verified in the tree at `de281e7`:

| Artifact | `LockLeaseBecameStale` present? |
| --- | --- |
| `architecture.md` (405, 428, 436, 649) | yes |
| `docs/exit-criteria/c6-transition-matrix-mapping.md:36` (24-event vocabulary line) | yes |
| `docs/diagrams/workspace-lifecycle.md` | yes |
| `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` + the `FolderWorkspaceLifecycleEvent` enum | **no** (23 events) |
| `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | **no** |

Both documents moved; the code did not; **and every gate is green**, because no gate spans the doc↔code edge:

- `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs:140` is named `StateCatalogAndEventVocabularyShouldMatchC6MappingDocument` — it **never reads the mapping document**. It compares the code enum against a 23-string literal inside the test (lines 158–184). It passes precisely because the document changed and the code did not.
- `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs:439–456` does compare architecture.md ↔ the mapping doc, but only with `ShouldContain` over its own 23-event literal `C6Events` (109–134) — a presence check that structurally cannot detect an **added** event.
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs:779–807` does parse the mapping document's vocabulary and disposition rows — and compares them to the **diagram** (`WorkspaceLifecycleDiagramEventLabelsEqualC6EventVocabulary`, 539–541), i.e. doc↔doc.

So the C6 "one model" rule is enforced doc↔doc and pinned-literal↔code, with nothing closing the loop. The document half-discloses this at line 428 ("**The existing gate does not catch its absence** … the gate will not flag the gap for you") — which is honest about the aggregate gate but does not retract the C6 row's claim at line 345 that the three artifacts *are* asserted identically, and does not mention that the drift has already been committed.

**(b) The guard-keyed gate is stated once and contradicted twice.**

Line 437 (correct, and a genuine improvement over the prior revision): *"**The gate is keyed on `(state, event, guard)`, not `(state, event)`** … CI fails if a state, event, or guard branch is added without test coverage."*
Line 1065 (the CI-gate inventory implementers actually read): *"**C6 transition matrix coverage gate** (every `(state, event)` cell asserted by at least one test; CI fails if a state or event is added without coverage)."*
Line 1829 (Implementation Handoff): *"unlisted (state, event) pairs MUST reject with `state_transition_invalid`."*

Line 437 itself explains why the two-tuple version is unsafe ("an implementation that handles one branch and silently takes the other path still satisfies 'this pair has a defined outcome' while destroying staged work"). A story author reading §Enforcement Guidelines or §Implementation Handoff builds the unsafe gate.

**Fix:** (1) make one gate compare the code enum + transition table to the mapping document *by parsing it*, and delete the misleading test name or make it live up to it; (2) propagate the guard triple to lines 345, 1065 and 1829 in the same edit; (3) state in §"Current reality" that the mapping artifact and the diagram already carry the PD11 vocabulary while the code does not, so the next reader does not treat green CI as agreement.

---

## RUB-5 — HIGH — S-7 claims its envelopes are "fully named so the C13 columns stay derivable"; they are not named anywhere the columns are derived from

**Checklist items:** 1, 2 · **Locations:** line 640 (S-7) against lines 686–702 (CLI exit codes), 704 (MCP kinds), 664 (A-8)

S-7 says: *"**Both envelopes are fully named so the C13 `cli_exit_code` / `mcp_failure_kind` columns stay derivable:** the 404 carries category `authorization`, code `tenant_access_denied` …; the 503 carries category `availability`, code `authority_unavailable`, `retryable: true`, client action `retry_after_backoff` …"*

Checked against the rest of the document and the spine:

| Claim | Reality |
| --- | --- |
| category `availability` | Not a member of `CanonicalErrorCategory`. The spine enum (`hexalith.folders.v1.yaml:11054`) has **49 members**; `availability` and `authorization` are not among them (the closest are `tenant_access_denied`, `folder_acl_denied`, `read_model_unavailable`, `projection_unavailable`). |
| `cli_exit_code` derivable | The canonical exit-code table (686–702) has no row for `authority_unavailable`, none for `resource_unavailable`, and still carries **`73 | not_found | Resource not found within tenant scope`** — a code S-7 removes from protected operations. |
| `mcp_failure_kind` derivable | The kind set at 704 still lists `not_found`, has no `authority_unavailable`, and states the rule *"The `kind` set is identical to the canonical category set (one-to-one mapping)"* — which cannot hold for a category that is not in the set. |
| `clientAction: retry_after_backoff` | The spine's `clientAction` enum is closed: `retry`, `revise_request`, `check_credentials`, `wait_for_reconciliation`, `contact_operator`, `no_action`, `refresh_state_then_submit_with_new_key`. `retry_after_backoff` is not a member. |
| `visibility` enum `metadata_only | redacted | withheld` (640) | The spine's `details.visibility` enum is `redacted | metadata_only` (no `withheld`). |

Line 645 correctly scopes PD10 as a breaking spine change with a regeneration list, so the *spine* side is owned. What is missing is that **the document's own canonical tables — the two artifacts that the CLI and MCP stories build from directly — were not updated and still encode the retired vocabulary**. Two adapter stories reading 686–704 will ship `not_found`/exit 73 and have nothing to map a 503 authority envelope onto; they will invent `internal_error`/exit 1, which is exactly the "retried blindly" failure S-7's rationale rejects.

**Fix:** add exit-code and MCP-kind rows for the two envelopes (and decide their canonical category names against the real enum), strike `not_found`/73 and the other two retired codes with a deprecation marker, and align `clientAction`/`visibility` with the closed spine enums or record that those enums are part of the PD10 change set.

---

## RUB-6 — HIGH — `withheld` and the `confidential` tier are introduced in S-6 and never reach the UX contract, F-5, or the cross-cutting concerns the UI stories read

**Checklist item:** 1 · **Locations:** line 639 (S-6) against 113 (#11), 119 (#17), 302 (UX constraints), 721 (F-5), 1593 (structure mapping)

S-6 makes `withheld` a **new render state** with a stated non-collision rule against `redacted` ("a value exists and is suppressed for this viewer" vs "no cleartext value exists to show anyone, here is its correlation token"). Grep confirms `withheld` occurs at only three lines: 639 (S-6), 640 (S-7's visibility enum), 242 (the honesty table saying it does not exist). Every downstream statement of the console's state vocabulary still models the old two-way split:

- line 113 (concern #11): *"**redacted fields are visually distinguished from unknown/missing fields** … renders with a visible affordance such as a lock icon plus 'your tenant policy hides this; contact your administrator'"* — S-6 says that copy is wrong for a withheld value, because no administrator can reveal it.
- line 119 (concern #17): still *"per-tenant policy (hash/truncate/redact/expose)"* and *"classification applies uniformly across audit, projections, and console responses"* — read-side framing, and "hash" rather than write-time token substitution. This is the concern the structure mapping points at.
- line 302 (UX Design Integration Implications, the section Epic 6/8 treat as normative): *"Redacted, inaccessible, unknown, missing, unavailable, failed, delayed, dirty, locked, ready, and committed states must be visually and semantically distinct."* No `withheld`.
- line 721 (F-5): redaction affordance only.
- line 1593: concern #17 → `Observability/FolderAuditSanitizer.cs`, the very component S-6 says is *not* the tokenizer.

Same for the data side: `confidential` appears only at 234/242/639/643. `SensitiveMetadataTier` gaining a `confidential` member and the console enums gaining `withheld` are breaking wire/UI additions that appear in no change set — PD10 has a regeneration list (645), PD8 has none.

**Fix:** add `withheld` to line 302's state list and to F-5 (with its own copy, distinct from the redaction copy), rewrite concern #17 to write-time substitution, and give PD8 the same explicit lockstep list PD10 has (spine enums, SDK, `ConsoleStatusText.cs`, `docs/operations/audit-and-redaction.md`, the tier enum).

---

## RUB-7 — HIGH — Degraded mode is decided twice, in opposite directions

**Checklist items:** 6, 8 · **Locations:** line 122 (concern #20) and 637 (S-4) against 640 (S-7) and 272 (NFR76)

- Concern #20: *"local tenant-access projection allows **read paths to continue under bounded staleness** when Hexalith.Tenants is unavailable; mutations require fresh authorization … Degraded-mode SLOs documented; not a silent fallback."*
- S-7: *"Authority that is absent, **stale**, malformed, or unavailable returns one non-disclosing HTTP 503 envelope."*
- NFR76 (paraphrased at 272): *"deny-by-default at protected endpoints and internal service boundaries when authority is absent, **stale**, malformed, or unavailable."*

Both are normative, neither cites the other, and they prescribe opposite behaviour for the same condition on the same protected read operations. The document even carries both framings inside one cell: S-4 (637) says *"local tenant-access projection (fail-closed-on-stale …)"* and then appends the PD10 evaluation order. "Fail-closed-on-stale" and "reads continue under bounded staleness" cannot both be the rule unless "stale" means two different things (bounded-but-fresh-enough vs past-the-bound) — which is plausibly the intended reconciliation, and is exactly what is not written down.

This is a live divergence for the level below: Story 13.2 (NFR76 deny-by-default) and any Tenants-degraded-mode story will implement opposite behaviour, and both will cite this document.

**Fix:** define the staleness bound once (it is C8/C7-adjacent), and state the rule as one sentence: reads served within bound X, 503 authority envelope beyond X, mutations always fresh. Update #20, S-4, and S-7 to the same sentence.

---

## RUB-8 — HIGH — S-6's tokenizer has a pinned primitive but no owner component, no key-management decision, and a gate pointed at the wrong file

**Checklist item:** 2 · **Locations:** line 639 (S-6), 348 (C9 ops-plan row), 1593 (structure mapping), structure tree 1136–1526

The prior gate's HIGH-1 is **half fixed**: S-6 now pins the primitive properly — `HMAC-SHA-256(key = per-managed-tenant confidential-token key, message = versioned classification tag ‖ canonical field identity ‖ NFC-normalized cleartext)`, fixed width, key version carried, one shared write-path implementation, and a good rationale for why keying is load-bearing twice (dictionary recovery; cross-tenant correlation oracle). That is a real improvement.

What did not land:

1. **No component owns it.** Grep over the structure tree (1136–1526) for `token`/`confid`/`withheld` returns `ForgejoCapabilities.cs # scoped tokens` and `PerTenantTokenBucket.cs` (rate limiting) — nothing on the write path. Line 1593 still maps concern #17 to `Observability/FolderAuditSanitizer.cs`, and the tree annotates that file `# C9 metadata-only sanitization per S-6` (1136+165) — while S-6 states in terms: *"`FolderAuditSanitizer.cs` is a read/emit-path sanitizer and is **not** the tokenizer."* The document contradicts its own structure mapping.
2. **The C9 measurement method points at the same wrong file.** Line 348: *"Artifact Location: this document §"S-6" + `Hexalith.Folders/Observability/FolderAuditSanitizer.cs`"*. The stated test — *"event-write token-substitution tests asserting no durable cleartext in any event, projection, audit record, log, trace, or export"* — cannot be hung off a read-path sanitizer.
3. **Key management is undecided.** A per-managed-tenant HMAC key needs a store (Dapr secret store? Hexalith.Tenants?), a rotation policy, and a stated consequence of rotation (correlation breaks across the boundary — acceptable or not?). Grep `rotat` returns only S-2's JWKS refresh (635). S-6's own rationale rejects "reversible encryption" partly on "key management cost", then introduces a key with no management decision.

**Fix:** name the write-path component in the tree (e.g. `Hexalith.Folders/Observability/ConfidentialValueTokenizer.cs`), repoint line 348 and line 1593 at it, and decide key store + rotation + rotation-consequence in the S-6 cell.

---

## RUB-9 — HIGH — PD8 and PD11 code-landing work is unowned at every rank, while rank-30 stories list them as prerequisites

**Checklist items:** 2, 3 · **Locations:** lines 218 (rank 0), 221 (rank 30), 236–245 (target-state table), 276 (the PD10-only disclosure)

The prior gate's MEDIUM-2 is **one-third fixed**. Line 276 now says plainly: *"**No story owns the PD10 spine correction yet (open — route to PM + Delivery).**"* Excellent. PD8 and PD11 get no equivalent.

The wave table still reads rank 0 = *"Authority relock: Approve the 2026-09-15 proposal; record the OQ1–OQ4 decisions; **apply PD8, PD10, and PD11**; reapprove the changed C3, C6, C9, and OQ3 digests"* — every other item in that wave is an approval action, yet "apply PD11" means a coordinated change across `FolderStateTransitions.cs`, its 5 pinning test classes, `DispositionLabelMapper.cs`, the spine enum and the SDK (the document itself spells this out at 427–428), and "apply PD8" means building a tokenizer that does not exist (242).

Rank 30 then makes them prerequisites: *"4.18 follows 12.1–12.2 **and PD11**; … 4.20 follows 12.1–12.3, OQ2/OQ3, PD8, **and PD10**; 4.21 follows 12.4 plus 4.19–4.20 **and PD11**"*. A prerequisite with no owning story and no rank cannot gate anything; the manifest validator described at 212 has nothing to point its edge at.

Note this interacts with RUB-4: because the *documents* can be "applied" at rank 0 while the code cannot, the half-application already in the tree (mapping doc + diagram carrying `LockLeaseBecameStale`) is the predictable result.

**Fix:** extend line 276's disclosure to PD8 and PD11 by name, or charter the three implementation stories with ranks below 30. Either way, say explicitly whether rank-0 "apply" means document-level only.

---

## RUB-10 — HIGH — The structure mapping is presented as complete and is not: no home for three new mechanisms, no NFR mapping at all, 12 of 22 concerns mapped

**Checklist items:** 1, 6 · **Locations:** §Requirements to Structure Mapping 1561–1597, §Architecture Completeness Checklist 1764 and 1785

The mapping table is the level-below's file-destination contract, and three claims about it are overstated:

1. **Three new mechanisms have no destination.** The S-6 write-time tokenizer (RUB-8), the S-7 authority-availability pre-check that must run *before* resource lookup on ~49 operations, and S-8's derived-scope resolution appear in no row of the FR table, no row of the concerns table, and nowhere in the 390-line structure tree. Stories will place them wherever, and the S-7 pre-check in particular is a middleware-vs-handler decision with security consequences.
2. **NFR74–NFR84 appear in no mapping** — consistent with RUB-1.
3. **12 of 22 concerns are mapped.** The concerns table (1584–1597) has rows for #1, #6, #11, #13, #14, #15, #16, #17, #18, #19, #20, #21 — twelve. Line 1764 checks *"[x] Cross-cutting concerns mapped (**22 concerns**; structure mapping table)"*, and line 1785 checks *"[x] Requirements to structure mapping complete"*. Concerns #2 (layered authorization), #4 (workspace state machine), #5 (provider failure taxonomy), #7 (path security), #8 (parity), #9 (read-model determinism), #10 (performance budgets), #12 (tenant provenance), #22 (exit criteria) have no destination row. Several of those are individually load-bearing — #7 path security has no named component anywhere in the tree.

**Fix:** add the three missing component homes, add rows for the ten unmapped concerns (or scope the checklist claim to "the concerns with dedicated components"), and either map the NFR bands or point the row explicitly at `docs/exit-criteria/nfr-traceability.md`.

---

## RUB-11 — HIGH — Write-concurrency control on the aggregate stream is undecided, against an NFR that requires multi-replica convergence

**Checklist item:** 6 · **Grep terms tried:** `optimistic` (0), `ETag` (0), `expected version` (0), `version conflict` (0), `append conflict` (0), `concurren` (3 — 313 capacity targets, 621 Postgres criteria, 1514 load-test scenario names)

The document decides two adjacent things extremely well — the distributed single-active-writer lock on `managed tenant + canonical provider/repository identity + normalized target ref` (106, 964–972), and the EventStore-owned idempotency admission actor with fencing tokens serialized per `(managed tenant, key digest)` (665, 955). Neither is concurrency control on the aggregate stream itself. Nothing says what happens when two commands for the same `{tenant}:folders:{id}` reach `/process` concurrently: optimistic append with an expected version and a retry, actor-serialized single-threaded handling, or last-write-wins.

It is plausibly an inherited Hexalith.EventStore property — but the document never says so, and NFR80 ("multi-replica convergence on one authoritative state with **no seed-local or replica-local correctness assumption**", 272) puts the burden squarely at this altitude. Story 12.1 (EventStore-backed `IFolderRepository`) and Story 12.2 (durable projections + task completion) each have to assume an answer.

**Fix:** one row in §Data Architecture stating the concurrency model (even if the decision is "inherited from Hexalith.EventStore's `AppendAsync` expected-version contract — see <link>"), the retry/conflict-surface behaviour, and which canonical error category a losing write returns.

---

## RUB-12 — HIGH — The operational/environmental envelope is thin where the release gates depend on it: no defined deployment profile, no environment inventory, no replica/scaling decision

**Checklist item:** 6 · **Locations:** 272 (OQ12/OQ13), I-1…I-9 (729–737), 1687–1692 · **Grep terms tried:** `deployment profile` (1, at 272), `supported deployment` (1, same line), `staging` (0 as an environment), `autoscal` (0), `blue-green` (0), `canary` (0), `replica` (6, all "across replicas" or NFR80 prose)

Three connected gaps, all of which the prior gate raised as MEDIUM-1 and none of which moved:

1. **"Supported deployment profile" is undefined and load-bearing.** OQ12 and OQ13 both close on *"one supported deployment profile"* (272). The term appears exactly once in the document. §Infrastructure & Deployment decides hosting style (I-2: container-based, Kubernetes-friendly-not-required) but never defines an enumerable profile. Two stories can close OQ12 and OQ13 against different profiles and both be "right".
2. **No environment inventory.** The document knows "local" and "production" and nothing between; there is no dev/test/staging set, no promotion path, no configuration-schema or config-validation gate (per-project `appsettings.{env}.json` at 1649 is the whole configuration story), and production Dapr policy lives in an unnamed "separate ops repository" (1691).
3. **No replica or scaling decision.** NFR80 demands multi-replica convergence; NFR81 demands readiness that reports real dependency health. The architecture decides `/health/live` + `/health/ready` (I-7) but never how many replicas of `folders`, `folders-workers`, `folders-ui` run, whether workers are singleton or competing consumers (the process-manager pattern at I-9 does not say), or how scaling interacts with the per-tenant token buckets (I-8, which are per-process by construction).

Item 3 is the one that can produce divergent code: a competing-consumer worker design and a singleton-worker design differ in every reconciliation story.

**Fix:** define one `Supported Deployment Profile` block under §Infrastructure & Deployment (replica counts or a range, worker consumption model, statestore/broker instances, ingress/TLS termination, secret store binding), name the environment set, and name the artifact + approver + gate for NFR75's "approved deployment policy" escape hatch.

---

## RUB-13 — MEDIUM — Counts asserted in the document are wrong, in the paragraph that forbids hard-coded counts

**Checklist item:** 2 · **Locations:** 640, 704, 241, 54, 1706

| Claim | Verified value |
| --- | --- |
| line 640: "All **49** protected Contract Spine operations" | The spine has **50** unique `operationId`s (`grep -o 'operationId: …' | sort -u | wc -l` = 50). Line 241 says "403 is live on **49 of 50** protected operations" — so the document states two different protected-operation denominators eight lines apart. |
| line 704: "the full `CanonicalErrorCategory` enum (**43** post-SDK members)" | The spine enum has **49** members (`hexalith.folders.v1.yaml:11054`). Unchanged since the prior gate flagged it. |
| line 54 / 1706: "12 capability blocks" / "FR1–FR58 across **12** capability groups" | `prd.md` has **11** FR `###` subsections, and line 54's own enumeration lists 11. (The FR *coverage* claim itself is sound — the mapping table rows tile FR1–FR58 contiguously with no gap.) |

Line 704 ends with: *"Surface denominators (operation counts, parity-oracle cells, C13 inventory) are always the **current generated Contract Spine inventory** — never hard-coded counts (2026-07-15)."* Three of the four hard-coded counts in the document sit within 500 lines of that sentence.

**Fix:** state protected-operation counts as "all protected operations (currently N per the generated inventory)" or drop the numeral; correct 43→49 or replace with "the full enum as published in `parity-contract.yaml`"; correct 12→11.

---

## RUB-14 — MEDIUM — The validation and readiness sections still assert a state the body contradicts

**Checklist item:** 8 (internal consistency) · Consolidates prior HIGH-4 rows f, g, h, i, m — all still open

| Location | Text | Contradicted by |
| --- | --- | --- |
| 765 | `[x] **C3 commit-TTL retention period set** … PM-approved 2026-06-22; Legal-approved 2026-06-24` | 232, 315, 430: C3 is **superseded, approval-pending** under A7b |
| 767 | `[ ] **C6 Workspace State Transition Matrix enumerated**` (unchecked) | The matrix exists at 356–438 and was just amended |
| 1730 | Gap Analysis, framed at 2026-07-14/15 | Does not list the PD10 breaking spine remediation, the four superseded approvals, OQ11–OQ13, or the NFR74–NFR84 admission |
| 1734 | "The architecture defers five PRD-quantitative targets (C1–C5) to MVP-release validation" | C3 is not a deferred quantitative target now; it is a reopened approval |
| 1789 | "**Overall Status (updated 2026-09-12): NOT READY**" + blocker list | Predates the amendment; frontmatter says `implementationReadiness: 'not-ready (2026-07-14/15)'` and `updated: '2026-09-15'` — three dates, none matching this revision |
| 1797 | "`FolderStateTransitions.cs` translates **1:1**; matrix-coverage CI gate prevents drift" | Both halves false: PD11 exists because the code diverges (240), and the gate cannot see guarded branches (437) |
| 1797 | "11 states × disposition labels × **~30 transitions**" | The table now carries 38 transition rows (382–418) |
| 1826 | Implementation Handoff must-pass gate list | Unchanged: no PD11 three-way conformance assertion, no C9 event-write token-substitution test, no S-7/S-8 denial-envelope gate — the three measurement methods this revision introduces at 345/348 |
| 1785 | `[x] Requirements to structure mapping complete` | See RUB-10 |

Individually cosmetic; together they mean the last section of the document — the one a reader reaches for a completeness verdict — describes the 2026-07-19 draft.

---

## RUB-15 — MEDIUM — The "Current reality" honesty table is accurate but incomplete: two live disposition divergences are missing, and the conditional disposition still cannot be generated

**Checklist items:** 5, 2 · **Locations:** 236–245 (honesty table), 366 and 369 (conditional dispositions), 373, 438 (generation rule)

Credit first: I verified all four rows of the *Current reality* table (240–243) against the code and they are **accurate** — `FolderStateTransitions.cs` does reject the added transitions, the three operator events are accepted, `LockLeaseBecameStale` exists nowhere in `src/`, and `planning-story-manifest.yaml` is still `generated_on: '2026-08-04'`. This is the single biggest improvement in this revision.

Two divergences of the same kind are not in the table:

- line 373 states `unknown_provider_outcome` has disposition `auto-recovering`. `FolderStateTransitionsTests.cs:197` pins `UnknownProviderOutcome → AwaitingHuman` as an `[InlineData]` row, and the mapper implements that.
- line 369 states `dirty` is `degraded-but-serving` *or* `awaiting-human` conditionally. `FolderStateTransitionsTests.cs:193` pins `Dirty → AwaitingHuman` flat.

And the generation rule at 438 (*"`DispositionLabelMapper.cs` is generated from it (or hand-written and tested against it)"*) still cannot be satisfied for the two conditional cells: `ready`'s condition takes one declared input (projection lag vs C2) but `dirty`'s takes two that the table never names (has-staged-changes, resumable-originating-task). This was MEDIUM-3 in the prior gate and did not move.

**Fix:** add the two disposition rows to the honesty table, and declare the disposition function's signature explicitly — `(state, projectionLag, hasStagedChanges, resumableTask) → disposition` — so the mapper and its parity test have something to be generated from.

---

## RUB-16 — MEDIUM — Staged-content retention is stated three incompatible ways; the new open item covers the clock but not the contradiction

**Checklist items:** 3, 8 · **Locations:** 400, 410–411, 424, 432, 984

- line 400: `changes_staged → inaccessible` — *"Staged changes preserved intact"* (no window).
- lines 410–411 / rule 3 (424): recovery from `inaccessible` branches on whether *"staged content survives within the C3 retention window"*, i.e. staged content expires.
- line 984 (§Cleanup process pattern, 2026-07-15): *"`changes_staged`, `dirty`, `unknown_provider_outcome`, and `reconciliation_required` are **never cleanup-eligible**."*

The new `Open —` item at 432 is a genuinely good catch (*"the new trigger can make the staged-content window unreachable … the two rules need one clock between them"*, routed to Legal + Product + Security with A7b) and it covers the *start* of the clock. It does not reconcile line 984's "never cleanup-eligible" with a branch whose whole purpose is that the content is gone.

**Fix:** fold line 984 into the same open item, or state which states are cleanup-eligible under which clock in one place and cross-reference from the other two.

---

## RUB-17 — MEDIUM — Two surviving contradictions inside the transition table itself

**Checklist item:** 8 · **Locations:** 402 vs 369/423; 645 vs `docs/contract/authorization-matrix.md`

**(a)** Line 402: `changes_staged → dirty` on `LockLeaseExpired`, side effect *"**Operator intervention required**"*. PD11 rule 2 (423) and the `dirty` catalog row (369) say the originating task resumes and that `dirty` alone never implies human intervention. Prior gate 4c; unmoved. An implementer reading the table row sets `awaiting-human` on the exact path PD11 rewrote.

**(b)** Line 645: *"The matrix already records this remediation as gaps `G1` through `G11` — including `G4`, which this correction must close alongside the rest."* `G4` being re-owned by PD10 is a deliberate, welcome fix. But `docs/contract/authorization-matrix.md` assigns **`G6` → "OQ9 incident-access evidence"** (line 346) and **`G7` → "Story 12.1 durable persistence and C7 runtime evidence"** (347) — neither is PD10 work, and `G4`'s own recorded owner there is still "Story 12.1 and Epic 13 runtime authorization work" (344). Claiming G1–G11 wholesale leaves three gaps with two owners each, and the matrix is the digest-bound artifact.

**Fix:** state the PD10-owned subset explicitly (G1, G2, G3, G5, G8, G9, G10, G11 + the deliberately re-owned G4), and note that G6/G7 stay with their recorded owners — or update the matrix in the same lockstep set.

---

## RUB-18 — MEDIUM — Escape hatches still have no enumerated set, artifact, approver or gate

**Checklist items:** 3, 2 · **Locations:** 640 (S-7), 272 (NFR75), 272 (OQ12/OQ13) — prior gate MEDIUM-1, unmoved

- *"MVP release reasons permit only approved values **such as** `caller_completed`; reserved post-MVP reasons are rejected"* (640) — a non-exhaustive enumeration inside a normative clause, and the reserved set is never listed, so "rejected" cannot be tested. "Release reason" is also undefined at first use.
- NFR75's *"unless an **approved deployment policy** allows them"* (272) — the escape hatch on an SSRF control (HXF-SEC-002) names no policy artifact, no approver, and no gate.
- OQ12/OQ13's "supported deployment profile" — see RUB-12.

---

## RUB-19 — MEDIUM — The console's live-freshness mechanism is named three ways and decided none

**Checklist item:** 6 · **Locations:** 718 (F-2), 1607, 1639, 1641, against C2 (314)

- F-2: *"Blazor Web App, Interactive Server render mode (SignalR) **for live status updates**"* — Interactive Server gives a render channel, not a change source.
- 1607: *"UI → Server: … consumes `Hexalith.Folders.Client` SDK; **reads only from projection endpoints**"* — pull.
- 1639: *"UI SignalR notification → live status update"* inside the data-flow diagram — push, from a hub that no decision creates and that no app-ID/service-invocation rule permits (`folders-ui` has no declared subscription in I-3/I-4).
- 1641: *"202 Accepted + correlationId for **status polling**"* — poll.

C2 pins a 500 ms status-freshness target and F-7 pins a p95 page-load budget, so the refresh mechanism and cadence are budget-relevant, not cosmetic.

**Fix:** decide one (projection-endpoint polling at cadence N, or a server-side hub fed by the pub/sub subscription, or Dapr subscription in the UI host) and correct the other two statements.

---

## RUB-20 — LOW — Cost is not an owned dimension anywhere

**Checklist item:** 6 · **Grep:** `cost` (9 hits, every one the word "cost" inside an *Alternatives considered* cell: 456, 624, 626, 627, 628, 635, 638, 719, 733)

Defensible at this altitude for a self-hosted container product, and the D-3 Redis-vs-Postgres escalation criteria carry the only cost-shaped reasoning. Recording it so the sweep is complete: no cost envelope, no per-tenant cost model, no cost-of-retention statement against D-7's P7Y tier.

---

## RUB-21 — LOW — Residual editorial

- **`C20` does not exist.** Line 637 (S-4): *"local tenant-access projection (fail-closed-on-stale per **C8 + C20**)"*. The exit criteria are C0–C13; the intended reference is cross-cutting concern #20. A reader chasing C20 finds nothing.
- **Frontmatter date convention.** `implementationReadiness: 'not-ready (2026-07-14/15)'` (41) vs `updated: '2026-09-15'` (39) vs "§Readiness (updated 2026-09-12)" (1789). Prior gate LOW-3; unmoved. Either advance them or state that readiness is deliberately frozen until the rank-50 rerun.
- **Line 208 vs the wave table.** *"This document does not assign story status and does not schedule work"* sits four lines above a rank-0-to-50 schedule. Line 210's "transitional authority … the manifest supersedes on regeneration" resolves it, but the two sentences should not be read in the other order.
- **Amendment findability.** The 2026-09-15 material still lives in five separated places (197–245, 247–290, 308–354, 356–438, 637–651) with nothing near the top telling a reader that S-6 was rewritten or that S-7/S-8 exist. Prior gate LOW-2.

---

# Prior-gate verification (`review-rubric-2026-09-15.md`, verdict FAIL)

| Prior ID | Status | Evidence |
| --- | --- | --- |
| CRITICAL-1 — three new sections describe systems that do not exist, with no honesty label | **FIXED** | 236–245: "PD8, PD10, and PD11 are target state, not current behavior" + a four-row *Current reality* table; 427 ("**This is target state**") and 428 for PD11. All four rows verified accurate against the code. Residual: two disposition divergences missing → **RUB-15** |
| CRITICAL-2 — migration/rollout silent, and the named gate does not cover it | **FIXED** | 651 records the rollout dimension as an explicit open item (v1-amendment vs v2, deprecation window, client order); 647 replaces the false gate claim with the truth ("**The C13 symmetric-drift gate will not catch this** … the real tripwire is `AuthorizationMatrixContractTests` … the drift fixture must gain a status-code and error-vocabulary surface") |
| CRITICAL-3 — schedule authority does not exist in the claimed form | **FIXED** | 206 ("regeneration owed … still `generated_on: '2026-08-04'`"), 210 (interim/transitional authority, superseded on regeneration), 214 (the rank rule is unsatisfiable — open), 243 (manifest has zero `execution_rank`) |
| HIGH-1 — S-6 states an intention with no mechanism | **PARTIAL** | Primitive now pinned (639). Component home, C9 artifact location and key management still wrong/absent → **RUB-8** |
| HIGH-2 — C6 totality gate cannot see the guards | **PARTIAL** | 436–437 declare the guard dimension, the four guard-discriminated pairs and the `(state, event, guard)` gate, and add the missing `(dirty-with-staged-changes, LockLeaseBecameStale)` rejection. Not propagated to 345, 1065, 1829; no doc↔code gate exists → **RUB-4** |
| HIGH-3 — governance change breaks a CI gate with no lockstep rule | **FIXED** | 329 now spells out the `superseded-pending-reapproval` vocabulary extension, both affected tests, the one-commit lockstep, and "until that lands, this document is the record of supersession" |
| HIGH-4 — surviving contradictions (a–n) | **MOSTLY OPEN** | Fixed: 4k (line 58 now "Eleven"), 4b partially (241 adds "49 of 50"), 4n partially (432). Open: 4a → **RUB-5**; 4b → **RUB-13**; 4c → **RUB-17a**; 4d → **RUB-6**; 4e → **RUB-7**; 4f/4g/4h/4i/4m → **RUB-14**; 4j → **RUB-1**; 4l → **RUB-10**; 4n → **RUB-16** |
| HIGH-5 — divergence points missed (1–5) | **PARTIAL** | Fixed: G4 now explicitly in scope (645); non-canonical denial categories now named (647). Open: G6/G7 double ownership → **RUB-17b**; S-6 enum extensions → **RUB-6**; `docs/operations/audit-and-redaction.md` lockstep still absent → **RUB-6** |
| MEDIUM-1 — escape hatches undefined | **OPEN** | → **RUB-18**, **RUB-12** |
| MEDIUM-2 — PD8/PD10/PD11 unowned at every rank | **PARTIAL** | PD10 disclosed at 276; PD8/PD11 not → **RUB-9** |
| MEDIUM-3 — `dirty` disposition not generatable | **OPEN** | → **RUB-15** |
| MEDIUM-4 — stale canonical-category count | **OPEN** | Still "43" at 704; actual 49 → **RUB-13** |
| LOW-1 — S-7/S-8 not rendered as table rows | **FIXED** | 640–641 are now proper rows under the §Authentication & Security header; the S-4 run-on sentence is also repaired |
| LOW-2 — amendment scattered | **OPEN** | → **RUB-21** |
| LOW-3 — frontmatter readiness stamp | **OPEN** | → **RUB-21** |

---

## What this document does well (so a fix pass does not undo it)

1. **The honesty convention is now applied to new normative material, and it is accurate.** The *Current reality* table (240–243) is the right mechanism and I could not falsify a single row of it. The planning-consistency invariant (253) and "admission is not implementation evidence" (278) are the kind of rules that stop a control-plane shell being reported as a product.
2. **Five `Open —` items with explicit routes** (214 Delivery+PM, 276 PM+Delivery, 432 Legal+Product+Security, 643 Security+Architecture, 651 rollout). Converting a contradiction into a routed open question is exactly what the checklist asks for; four of the five name their route.
3. **Claims about sibling artifacts check out.** "`epics.md` carries no reference to PD8, PD10, or PD11" (276) — verified, grep count 0. The per-row NFR74–NFR84 ownership recital (274) — verified against `docs/exit-criteria/nfr-traceability.md:125–130` (NFR79→`12-1`, NFR80→`12-2`, NFR83→`7-16`, NFR84→`13-6`). Deferring per-row authority to the traceability table rather than the prose is the right call.
4. **The guard dimension (436–437)** is a genuinely good piece of architecture: it identifies the four guard-discriminated pairs, requires server-side evaluation from durable state (S-8), assigns the retryable/non-retryable classification to one shared classifier so GitHub and Forgejo cannot disagree, and explains why a two-tuple gate is unsafe. It needs propagating, not rewriting.
5. **A-9 / D-7 idempotency** remains the strongest section in the document and is the template the weaker mechanisms (S-6, S-7) should be written against — and S-6 now explicitly cites it as the precedent.
