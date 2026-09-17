# Rubric Walker Review — architecture.md, 2026-09-16 UPDATE pass

- **Reviewer lens:** Rubric Walker (good-architecture checklist + dimension sweep), BMad architecture Reviewer Gate — *Update* re-review
- **Subject:** `_bmad-output/planning-artifacts/architecture.md` (1902 lines, was 1857; classic format, decision IDs `D-`/`A-`/`S-`/`F-`/`I-`/`C-`)
- **Altitude:** INITIATIVE / whole-system. Level below: EPICS (`epics.md`). Driving spec: `prd.md` (FR1–FR58, NFR1–NFR84) + `ux-design-specification.md`
- **State reviewed:** working tree at `de281e7` + the uncommitted 2026-09-16 amendment (`architecture.md` +137/−52, `docs/exit-criteria/c6-transition-matrix-mapping.md` +25/−?)
- **Prior gate:** `review-rubric-2026-09-16.md` — verdict FAIL, 3 critical / 9 high / 7 medium / 2 low
- **Ratified scope judged against:** architecture.md + co-normative docs only; no production code; no `epics.md` edits; Tier-3 dimensions deliberately ROUTED as open items rather than decided
- **Mode:** read-only. This report is the only file written.

## Verdict: **FAIL** — materially improved, two criticals remain, both closable inside the ratified scope

This is the largest single improvement the document has had. Three of my prior findings are fully closed (RUB-1, RUB-7, RUB-9), one is essentially closed (RUB-15), the S-7 invented-vocabulary defect is replaced with a verbatim transcription from the approved matrix that I checked field by field, and four previously silent or near-silent dimensions now carry routed open items with named owners. The routing disposition itself is the right call under the ratified scope, and three of the four routed items are well-formed.

It still fails, for two reasons, neither of which is a rewrite:

1. **The routed items name owners but not ranks.** The document's own §"Release Authority Overlay" makes `execution_rank` the scheduling authority and says a prerequisite must sit at a strictly lower rank than its dependent. Four dimensions were converted from silent to open — and none of them was given a rank. Three of the four sit in front of the rank-10/rank-20 wave whose stories must assume their answers, and two of them (`:642` write concurrency, `:767` deployment envelope) each declare the other blocking. An open item with an owner and no rank cannot gate the work it protects, which is checklist item 3 failing on the very items this amendment created.
2. **The amendment fixed the validation section's over-claim and left the completeness checklist's over-ticks.** `:1804` still ticks *Requirements to structure mapping complete* and *Cross-cutting concerns mapped (22 concerns)* about 200 lines below the new banner at `:1604` that says in terms the mapping does **not** route three of this relock's mechanisms and does **not** map NFRs at all, and above a concerns table that holds 12 of 22 rows. That is the identical false-self-claim failure RUB-1 was, surviving in the one section a reader reaches for a completeness verdict.

Everything else is high or below.

## Checklist scorecard

| # | Checklist item | Prior | Now | One-line basis |
| --- | --- | --- | --- | --- |
| 1 | Fixes the real divergence points for the level below and misses none | PARTIAL | **PARTIAL (better)** | Degraded mode is now decided once and normatively (`:675`); PD8/PD10/PD11 ownership is disclosed per-correction (`:278–281`). But S-7's vocabulary still never reaches the CLI exit-code table or MCP kind set (**RU-H2**), and `withheld` still never reaches the UX contract (**RU-M3**). |
| 2 | Every rule is enforceable and actually prevents its stated divergence | FAIL | **PARTIAL** | The C6 gate is now correctly triple-keyed where it is stated as a gate (`:447`, `:1097`) — a real fix. But four other statements of the same rule stay pair-keyed (**RU-H3**), no gate spans the doc↔code edge and the amendment widened that delta (**RU-H6**), and C9's measurement method still hangs off a file S-6 says is not the write path (**RU-H4**). |
| 3 | Nothing deferred or left open could let two units diverge | PARTIAL | **FAIL** | Ten routed open items now, none carrying a rank, three of them in front of the wave obliged to assume their answers, two of them mutually blocking. The document says outright at `:642` that "two units reading this document today could reasonably pick different ones and both believe they complied" and then schedules Story 12.1 first anyway (**RU-C1**). |
| 4 | Named technology is verified-current | PASS | **PASS (stronger)** | Version pins are now deferred to `references/Hexalith.Builds/Props/Directory.Packages.props` with a stated owner-on-disagreement rule (`:1741`). I confirmed the file exists and that the one surviving in-document pin (Octokit `14.0.0`) matches it. This kills a recurring drift class rather than resetting its clock. Residual is rule-consistency only (**RU-L2**). |
| 5 | Ratifies rather than contradicts the brownfield codebase | PASS (with note) | **PASS (stronger)** | The *Current reality* table was corrected in the **strict** direction (`:240`): two of the five PD11 transitions are not merely missing, they are accepted onto the unguarded branch, so the guarded outcome is unreachable and the pair-keyed gate passes the build that destroys staged work. Verified: `LockLeaseBecameStale` and `stagedBy*` have zero occurrences in `src/`. The one new as-built contradiction is S-7's `visibility` sentence (**RU-H1**). |
| 6 | Covers the capabilities of the specs that drove it | FAIL | **PARTIAL** | `prd.md:651`'s **event payload** versioning requirement is no longer silent — it is a routed open item at `:644` naming the right four questions. NFR category count is now honestly 11 with the two admitted bands labelled uncovered. The FR capability-block count is still wrong (**RU-M4**). |
| 7 | **Every structural dimension the altitude owns is decided, deferred, or an open question** | FAIL | **PARTIAL** | Disaster recovery, event-payload schema evolution, write concurrency, and the deployment envelope all moved SILENT → ROUTED OPEN. Two dimensions remain effectively silent: the S-6 tenant-key store and rotation policy (**RU-H5**), and cost (**RU-L3**). Alerting/SLO ownership and the runbook inventory stay partial (**RU-M9**). |
| 8 | No new decision weakens or contradicts an inherited one | PARTIAL | **PASS** | RUB-7 closed. `:675` settles degraded mode once, names the approved matrix as the deciding authority, scopes concern #20's bounded-staleness allowance to read models that are *not* authorization evidence, and prices the availability consequence into OQ12/OQ13. This is the model answer for converting a contradiction into a decision. |

## Dimension sweep

Grep terms are given for every row marked SILENT or newly moved. All greps case-insensitive over the full 1902 lines.

| # | Dimension | Prior | Now | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Paradigm & style | Decided | **Decided** | `:74–77`, `:596–600`, D-1 (`:635`) |
| 2 | Boundaries & dependency direction | Decided | **Decided** | §Architectural Boundaries `:1567`; Memories exception rules `:137`, `:176` |
| 3 | State mutation & persistence | Decided (unbuilt, labelled) | **Decided** | D-1/D-2/D-3 (`:635–637`); `:251` in-memory repo + NoOp ADR; Epic 12 `:256` |
| 4 | Shared-data ownership | Decided | **Decided** | §Data Boundaries; new bridge-writer arbitration at `:676` (10.7 vs 12.5) is a genuine addition |
| 5 | Identity & multi-tenancy | Decided | **Decided** | `:78`, concerns #1/#12/#13/#14 |
| 6 | Authentication & authorization | Decided, one contradiction | **Decided** | S-1…S-8 (`:650–657`); contradiction closed at `:675` |
| 7 | API/contract strategy & versioning | Decided | **Decided** | A-1/A-3/A-11, C0/C13 |
| 8 | **Event/message payload schema evolution & data migration** | **SILENT** | **ROUTED OPEN** | `:644` — names the version marker, where upcasting runs, the additive-vs-breaking rule, and the replay-reproducibility question. Owner: Architecture, with Epic 12. No rank → **RU-C1** |
| 9 | Cross-surface parity | Decided | **Decided** | §Adapter Parity Contract `:697–730`, C13 |
| 10 | Error & failure semantics | Partial | **Partial** | S-7 now transcribes the matrix verbatim (verified). The canonical tables underneath it did not move → **RU-H2** |
| 11 | Concurrency & locking (distributed lock) | Decided | **Decided** | Concern #4, §Locking, C7 |
| 12 | **Write-concurrency control on the aggregate stream** | **SILENT** | **ROUTED OPEN — inadequately** | `:642`. Grep `optimistic`=1 (only inside the open item), `expected version`=0, `ETag`=0. Names three candidate mechanisms and the losing-writer error, but no rank, in front of rank-10 Story 12.1/12.2 → **RU-C1** |
| 13 | Idempotency | Decided | **Decided** | A-9, D-7, §Idempotency — still the strongest section |
| 14 | Eventing & messaging topology | Decided | **Decided** | D-4/D-5, topics, I-9 |
| 15 | Read models & projections | Decided | **Decided** | `:183–195`, D-10, + the new sole-writer arbitration `:676` |
| 16 | Data retention & lifecycle | Decided, contradictory | **Decided, one contradiction left** | C3 authority paragraph `:439`; `:1014` cleanup bullet restated in C3 vocabulary (good); `:1016` "never cleanup-eligible" unreconciled → **RU-M5** |
| 17 | Search/indexing | Decided | **Decided** | §Query Facade, FR58 mapping |
| 18 | Frontend architecture | Decided, one hole | **Decided, same hole** | F-1…F-7; live-refresh still named three ways → **RU-M7**; `withheld` still absent → **RU-M3** |
| 19 | Observability & telemetry | Partial | **Partial** | I-6/I-7; alerting still only Epic-13 scope, grep `on-call`=0, no SLO ownership → **RU-M9** |
| 20 | Testing strategy & CI gates | Decided | **Decided** | `:1097` triple-keyed C6 gate is a real fix |
| 21 | Deployment, environments & configuration | Partial → SILENT in parts | **ROUTED OPEN** | `:767`. Grep `staging`=0 as an environment (both hits are `changes_staged`/"Staging a change set"), `autoscal`=0, `blue-green`=0, `canary`=0. Owner Architecture + Delivery, anchored to OQ12/OQ13 at rank 40 — but NFR80/NFR81 work sits at rank 10 → **RU-C1** |
| 22 | Infrastructure & provider strategy | Decided | **Decided** | D-3/D-5, I-1…I-4, A-6/A-7, C12/OQ4 |
| 23 | Operations & runbooks | Partial | **Partial** | The new DR item *requires* a restore runbook; §Minor Gaps still lists only `tenant-deletion.md`; no inventory, ownership, or gate → **RU-M9** |
| 24 | Migration & rollout of breaking changes | Open (acceptable) | **Open (acceptable)** | `:671` — v1-amendment-vs-v2, deprecation window, client rollout order. Still no owner named, unlike its siblings |
| 25 | Performance & capacity | Decided + open evidence | **Decided + open evidence** | C1/C2/C4/C5, F-7, OQ13 |
| 26 | Scaling & replica topology | **SILENT** | **ROUTED OPEN** | Folded into `:767`; per-app-ID replica and scaling rule explicitly listed as owed → **RU-C1** |
| 27 | **Disaster recovery / backup / restore** | **SILENT** | **ROUTED OPEN — adequate** | `:769`. Grep now: `RTO`=2, `RPO`=3, `disaster`=1, `backup`=5. Names RPO/RTO per store, backup mechanism + retention, restore runbook, the restore-vs-C3-deletion reconciliation, and restore-exercise cadence. One question short → **RU-M1** |
| 28 | Cost | **SILENT** | **SILENT** | grep `cost`=9, every hit still the word "cost" inside an *Alternatives considered* cell → **RU-L3** |
| 29 | **Key management & secret rotation (S-6 tenant key)** | Partial → gap | **Effectively SILENT, and not routed** | grep `rotat`=2: `:281` (an acknowledgement inside the unowned-story bullet) and `:651` (S-2 JWKS refresh). No store, no rotation policy, no rotation consequence — and alone among this amendment's undecided dimensions it got no `Open —` item → **RU-H5** |
| 30 | Accessibility | Decided | **Decided** | F-3, `accessibility-gates` axe job |
| 31 | Localization / i18n | N/A | **N/A** | grep `localization`/`i18n`=0; not a spec capability |

Net movement: **4 dimensions SILENT → ROUTED OPEN, 1 contradiction → DECIDED, 2 still silent (1 of them unrouted).**

---

# Findings

Severity for this pass: **critical** = a coverage claim the document makes about itself that is false, or an owned dimension left undecided in a way scheduled work must resolve before the decision is due · **high** = an unenforceable load-bearing rule or a real gap · **medium** = partial coverage or an internal contradiction · **low** = cosmetic.

---

## RU-C1 — CRITICAL — The four routed Tier-3 dimensions carry owners but no rank, and three of them sit in front of the wave obliged to assume their answers

**Checklist items:** 3 (primary), 7 · **Locations:** `:642`, `:644`, `:767`, `:769` against the wave table at `:221–227` and the rank rule at `:213`

The amendment's chosen disposition — route rather than decide — is exactly right under the ratified scope, and I want to be clear that I am not asking for the decisions. Three of the four items are well-formed: each names the actual questions, each names an owner, each explains what diverges if the question stays open. `:642` even states the divergence in the sharpest possible terms: *"Two units reading this document today could reasonably pick different ones and both believe they complied."*

What is missing is the one field that makes an open item safe at this altitude. `:215` makes `execution_rank` the scheduling authority and requires a prerequisite to sit at a strictly lower rank than its dependent. Every routed item was given an owner and none was given a rank:

| Open item | Route | Rank | The work that must assume its answer | That work's rank |
| --- | --- | --- | --- | --- |
| `:642` write concurrency | Architecture, with Epic 12 | none | Story 12.1 (EventStore-backed repository), 12.2 (`NFR80` multi-replica convergence) | **10** — the first wave |
| `:644` event-payload schema evolution | Architecture, with Epic 12 | none | 12.1/12.2 write the first durable payloads; the `LockLeaseBecameStale` addition is live now | **10**, and rank **0** |
| `:767` deployment/replica envelope | Architecture + Delivery, with OQ12/OQ13 | none | 12.2 (`NFR80`), 13.4 (`NFR81` true readiness) | **10** / Epic 13 **unranked** |
| `:769` disaster recovery | Architecture + Operations, with Epic 13 | none | 12.1 durable repository, 12.6 durable admission | **10** |

The two most acute are mutually blocking by the document's own words: `:767` says *"This also blocks the write-concurrency decision above, which only matters above one replica"*, and `:642` says the C6 matrix assumes a single serialized sequence. So the rank-10 wave — the one the release authority overlay puts first — is chartered to start on top of two undecided dimensions that each cite the other.

This is checklist item 3 failing, and it fails specifically on the items this amendment created. Note the contrast with the amendment's own good practice elsewhere: `:273` binds OQ12/OQ13 to rank 40 explicitly, and `:278–281` binds the three unowned PD corrections to a named route *and* to the rank-30 stories that list them as prerequisites. The four new items got the owner half and not the schedule half.

**Fix (in scope, cheap):** give each of the four a rank or a blocking edge in the §"Release Authority Overlay" wave table — most naturally a rank-0 row ("decide write concurrency and the replica envelope before rank 10 opens") for the two that gate Epic 12, and rank 40 alongside OQ12/OQ13 for DR. If a rank genuinely cannot be assigned yet, say which story is blocked until it is, so the manifest validator has an edge to point at. A one-line addition per item.

---

## RU-C2 — CRITICAL — The completeness checklist still ticks two boxes the amendment's own new banner says are false

**Checklist items:** 6, 2 · **Locations:** `:1804` and `:1812` (checklist) against `:1604` (the new banner) and `:1627–1639` (the concerns table)

The amendment added an honest banner to §"Requirements to Structure Mapping" — this is good, and it is exactly the treatment the 2026-09-15 pass gave PD8/PD10/PD11:

> `:1604`: *"**Target routing, not an as-built index (authored 2026-05, partially stale).** … It also does not yet route the three mechanisms this relock adds — the PD10 denial envelopes, the PD8 tokenizer, and the PD11 guard fields — nor does it map NFRs at all."*

Two hundred lines later the completeness checklist is untouched:

- `:1812`: `[x] Requirements to structure mapping complete`
- `:1804`: `[x] Cross-cutting concerns mapped (22 concerns; structure mapping table)` — the table carries rows for #1, #6, #11, #13, #14, #15, #16, #17, #18, #19, #20, #21. **Twelve.** Concerns #2, #4, #5, #7, #8, #9, #10, #12, #22 have no destination row; #7 path security still has no named component anywhere in the 400-line tree.

This is the same failure class as RUB-1, which this amendment closed well: a section that summarizes the document asserting completeness the body denies. It is arguably worse than RUB-1 was, because RUB-1's contradiction was between the validation section and *scattered* body text, whereas this one is between two sections of the same document where one was just edited to say the other is wrong. A reader who skips to §"Architecture Completeness Checklist" for a verdict — which is what that section is for — gets the 2026-05 answer.

**Fix:** either scope both ticks (`[x] Requirements to structure mapping complete for FR blocks; NFR bands and the three PD mechanisms are unrouted — see the banner`, and `[x] Cross-cutting concerns mapped (12 of 22 have dedicated components; the remainder are enforced by gates, not files)`) or unbox them. Two edits.

---

## RU-H1 — HIGH — S-7's one un-transcribed sentence reintroduces the invented-vocabulary defect the rest of the row just fixed

**Checklist items:** 2, 5 · **Location:** `:656` against `docs/contract/authorization-matrix.md` §"Canonical Outcomes", the spine, and the document's own `:242`

Credit first, because this is the biggest single fix in the amendment and I verified it field by field. S-7 now says the wire values are *"owned by `docs/contract/authorization-matrix.md` §'Canonical Outcomes' and transcribed here, never restated independently … on any disagreement the matrix wins and this row is the defect"*, and the three envelopes transcribe **exactly**:

| Outcome | architecture.md `:656` | authorization-matrix.md | Match |
| --- | --- | --- | --- |
| `authentication-failure-401` | 401 / `authentication_failure` / `authentication_required` / false / `check_credentials` / `redacted` | identical | ✔ |
| `safe-denial-404` | 404 / `tenant_access_denied` / `resource_unavailable` / false / `no_action` / `redacted` | identical | ✔ |
| `authority-unavailable-503` | 503 / `read_model_unavailable` / `projection_unavailable` / true / `retry` / `redacted` | identical | ✔ |

The invented `authorization`/`availability` categories and `retry_after_backoff` are gone; `authentication_failure`, `tenant_access_denied`, `read_model_unavailable`, and `projection_unavailable` are all real members of the 49-member `CanonicalErrorCategory` enum. The missing 401 is added. The envelope-identity rule now extends past `category` to code, message, type URI, detail keys, `retryReasonCode`, `layer`, and `timingBucket`. That is a clean close.

One sentence in the row is **not** transcribed and reintroduces the defect:

> *"`visibility` is a required field on every error and is enumerated — `metadata_only`, `redacted`, `withheld` — not free text, so two surfaces cannot invent different vocabularies."*

- `withheld` occurs **zero times** in `docs/contract/authorization-matrix.md`. The matrix's "Details visibility" column carries `redacted` for all three outcomes and declares no enum.
- In the spine, `visibility` is never an enumerated schema field. It appears only as example values inside `details` (`redacted`, `metadata_only`) at `hexalith.folders.v1.yaml:5786+`.
- The document's own *Current reality* table says so at `:242`: *"`visibility` exists only as the constant `details.visibility="metadata_only"`, not as a required top-level error field."*

So the row asserts as decided — in the present tense, inside the one cell that declares the matrix its owner — a wire shape that the owning artifact does not carry, the spine does not implement, and the document's own honesty table says does not exist. `withheld` additionally belongs to PD8/S-6, not PD10, so it arrives in the PD10 change set with no lockstep list (see **RU-M3**).

**Fix:** either move the `visibility` enum into the matrix as part of the A6b reapproval and cite it like the other three, or label the sentence target-state and attach it to PD8's change set. As written it is an unowned wire claim in a row whose whole value is that it has exactly one owner.

---

## RU-H2 — HIGH — S-7 says the CLI/MCP columns are "derived from the matrix"; the matrix has no such columns, and the document's own canonical tables still encode the retired vocabulary

**Checklist items:** 1, 2 · **Locations:** `:656` against `:716–730` (CLI exit-code table) and `:732` (MCP kinds) · **Prior ID:** RUB-5, unmoved and now with a broken derivation claim on top

S-7 ends: *"the C13 `cli_exit_code` / `mcp_failure_kind` columns are **derived from the matrix**, so CLI and MCP cannot disagree on whether an authority outage is retryable."*

`grep -n "cli_exit_code\|mcp_failure_kind\|exit code" docs/contract/authorization-matrix.md` returns **nothing**. The matrix carries no exit-code or failure-kind surface at all, so there is nothing to derive from. Meanwhile the two tables the CLI and MCP stories actually build from did not move:

| S-7 outcome | CLI exit code at `:716–730` | MCP kind at `:732` |
| --- | --- | --- |
| `authentication-failure-401` / `authentication_failure` | **no row** | **not in the set** |
| `safe-denial-404` / `tenant_access_denied` | 66 ✔ | present ✔ |
| `authority-unavailable-503` / `read_model_unavailable` | **no row** | **not in the set** |
| Retired `not_found` | **still row 73** | **still in the set** |

Two adapter stories reading `:716–730` today will ship `not_found`/exit 73 on a protected operation and will have nothing to map a 401 or a 503 authority envelope onto — so they will invent `internal_error`/exit 1, which is precisely the *"generic 500 on authority outage (indistinguishable from a bug, and retried blindly)"* alternative S-7's own rationale rejects. And `:732`'s closing rule — *"The `kind` set is identical to the canonical category set (one-to-one mapping)"* — is unsatisfiable while two of the three S-7 categories are absent from the set.

**Fix:** add exit-code and kind rows for `authentication_failure` and `read_model_unavailable`; strike `not_found`/73 with a deprecation marker in the PD10 change set; and either add the two columns to the matrix (so the derivation claim becomes true) or restate S-7 as "derived from the matrix's category, mapped by these tables."

---

## RU-H3 — HIGH — Triple keying reached three statements of the C6 rule and not the other four

**Checklist item:** 2 · **Prior ID:** RUB-4b, partially fixed

The propagation that landed is real and correct:

- `:365` matrix preamble — triple-keyed, with the default rule for an unenumerated guard branch spelled out ("rejected under that same rule — never silently routed to the sibling branch's outcome")
- `:447` aggregate-test gate — triple-keyed, with the reason a pair-keyed gate is unsafe
- `:1097` CI-gate inventory — triple-keyed, with **not every `(state, event)` cell** called out explicitly

Four statements of the same rule stay pair-keyed, including the two an implementer is most likely to read:

| Location | Text | Who reads it |
| --- | --- | --- |
| `:1874` **Implementation Handoff** | *"unlisted (state, event) pairs MUST reject with `state_transition_invalid`"* | every AI agent and every story author, by design — this is the handoff |
| `:352` **C6 Exit Criteria Operations Plan, measurement method** | *"aggregate test asserts every (state, event) has a defined outcome"* | whoever builds the C6 gate |
| `:325` **C6 exit-criteria row** | *"every (state, event) pair → outcome"* | the criterion definition itself |
| `:396` **Valid transitions table header** | *"every `(from, event) → (to, side effect)` declared"* | anyone reading the matrix table |
| `:107` concern #4 | *"every (state, event) pair has a defined outcome"* | the cross-cutting concern the matrix cites |

`:447` explains in the same document why the two-tuple version destroys staged work. The handoff section tells the implementer to build it anyway. A rule stated correctly in three places and incorrectly in four is not enforceable — the reader takes whichever they reached first, and `:1874` is the one the handoff points them at.

**Fix:** five sed-scale edits, same commit.

---

## RU-H4 — HIGH — The C9 artifact location, the structure-mapping row, and the tree annotation all still name the file the document says is not the tokenizer

**Checklist item:** 2 · **Locations:** `:355`, `:1636`, `:1334` against `:281` and S-6 at `:655` · **Prior ID:** RUB-8, half fixed

The amendment added a genuinely good honesty note at `:281`:

> *"**The tokenizer has no owning component in this architecture**: `FolderAuditSanitizer.cs` is explicitly *not* it (S-6), and no other type is named."*

Three pointers contradict it, unchanged:

- `:355` C9 Exit Criteria Operations Plan → *Artifact Location: this document §"S-6" + `Hexalith.Folders/Observability/FolderAuditSanitizer.cs`*, with the measurement method *"event-write token-substitution tests asserting no durable cleartext in any event, projection, audit record, log, trace, or export."* That test cannot be hung off a read/emit-path sanitizer.
- `:1636` structure mapping → `#17 Sensitive metadata classification | Observability/FolderAuditSanitizer.cs`
- `:1334` project tree → `FolderAuditSanitizer.cs  # C9 metadata-only sanitization per S-6`

So `:281` now says no component owns it while `:355` names the C9 artifact and `:1636` names the destination — and the destination is the one file S-6 rules out by name. The disclosure made the contradiction legible without resolving it. C9 is an approval-pending exit criterion whose reapproval will be measured against `:355`.

**Fix:** name the write-path component in the tree (e.g. `Hexalith.Folders/Security/ConfidentialValueTokenizer.cs`), repoint `:355` and `:1636` at it, and leave `FolderAuditSanitizer.cs` annotated as the read-path sanitizer it is. This is a naming decision, not an implementation — fully inside the ratified scope.

---

## RU-H5 — HIGH — The S-6 tenant key is the one undecided dimension this amendment did not route

**Checklist items:** 7, 2 · **Location:** `:655` (S-6) · **Grep:** `rotat` = 2 hits — `:281` and S-2's JWKS refresh at `:651`

S-6 introduces a **per-managed-tenant confidential-token key** with a carried key version, and argues at length why the keying is load-bearing twice (dictionary recovery on low-entropy values; a cross-tenant correlation oracle). It then decides nothing about the key: no store (Dapr secret store? Hexalith.Tenants? the shared state store the PD8 carve-out discussion at `:661` worries about?), no rotation policy, no statement of what rotation does to existing tokens.

That last one is not administrative. The whole value of the token is that *"the event-write path and the Memories egress must produce the **same** token for the same value, or evidence stops joining"* (`:655`). Rotating a tenant's key breaks that join across the rotation boundary for every historical event — and with the 7-year admission-record retention and the replay-backed audit projection (D-10), historical joins are the point. Whether that break is acceptable, whether old tokens are re-derived, or whether both key versions are carried and joined, is a decision two units will answer differently. S-6's own alternatives cell rejects reversible encryption partly on *"key management cost"* and then introduces a key with no management.

What makes this a finding rather than a note is the **inconsistent disposition**. This amendment gave six undecided dimensions a routed `Open —` item with a named owner. This one got a clause inside a bullet about an unowned story. It is the same shape of gap as `:659` (PD8's scope boundary), which sits ten lines later and *did* get an open item, a named route, and three enumerated control-set options.

**Fix:** one more `Open —` item under §"Authentication & Security" routed to Security + Architecture with A5/PD8, naming: the key store, the rotation policy and trigger, and the rotation consequence for historical token joins.

---

## RU-H6 — HIGH — The co-normative lockstep widened the doc↔code delta and added normative rules in a shape no gate can read

**Checklist items:** 2, 5 · **Locations:** `docs/exit-criteria/c6-transition-matrix-mapping.md:14–29`, `:446` · **Prior ID:** RUB-4a, honesty half closed, enforcement half open and now larger

The honesty half is genuinely closed, and well. `:440` now says:

> *"**Two divergences are open right now, and naming them is the point of the invariant (2026-09-16).** Claiming one model while two are live is worse than recording the gap…"*

— and then names both, including the `unknown_provider_outcome` disposition split between this document and the mapping doc/diagram, and `FolderStateTransitions.cs:157`'s unconditional `Dirty → AwaitingHuman`. That closes RUB-15's first half outright.

The enforcement half is worse than before. The lockstep added to the mapping document:

- a new **Guard Discriminators** table (four pairs, branch outcomes, the durable field each reads)
- a normative declaration that `stagedByTaskId` / `stagedByPrincipal` are the durable fields the guards read — which have **zero occurrences in `src/`**, confirmed
- three rewritten Mapping-Rules rows carrying `approval-pending under A7b` guard-branch status

None of it is readable by any gate. The run's own `.memlog.md` records the reason explicitly: the new table was *"Confirmed SAFE against the pinned parsers"* because `ParseC6EventVocabulary` is anchored to the "copied for drift checking" line and `C6StateCatalogRow` requires a provenance column the new table does not carry. Safe means invisible. Combined with the pre-existing state — `LockLeaseBecameStale` present in `architecture.md`, the mapping doc, and `docs/diagrams/workspace-lifecycle.md`, and absent from `FolderStateTransitions.cs` and the spine enum — a digest-bound exit-criteria artifact has now gained a second layer of normative content that no test spans, on top of a drift that is already committed and already green.

The document half-discloses this (`:436` says the existing gate will not flag the missing event), but `:352`'s C6 measurement method still claims the PD11 rules *"are asserted identically against this document, the C6 mapping artifact, and `FolderStateTransitions.cs`"* — an assertion that exists nowhere.

**Fix (in scope):** restate `:352` as a target with the gate named as owed, and record in `:440` that the mapping artifact and the diagram carry the PD11 vocabulary while the code and the spine do not, so the next reader cannot take green CI for agreement. Building the doc↔code gate itself is code work and correctly out of scope — but the document should say the gate does not exist rather than say the assertion does.

---

## RU-M1 — MEDIUM — The disaster-recovery item names the erasure direction of the restore hazard and not the resurrection direction

**Checklist items:** 3, 7 · **Location:** `:769`

Judged on its merits this is a good open item — it names RPO/RTO per store, the backup mechanism and its retention, the restore runbook, the reconciliation of restore against C3 and the deletion obligations, and the cadence at which restore is exercised. It correctly identifies the load-bearing fact that the projections are rebuildable from events (D-10), which makes the event store the single point of total loss.

It names one direction of the admission-record hazard: *"a restore from before a deletion can resurrect data whose deletion was a compliance obligation, including admission records under the P7Y retention regime."*

The other direction is the one that makes two rank-10 stories diverge, and it is not named. Concern #21 (`:123`) says idempotency admission is **not** a rebuildable projection — *"`/project` replay cannot create, erase, or resurrect consumed-key authority"* — and D-7 retains commit-replay results for P7Y precisely so a consumed key stays consumed. A restore that rolls admission records **back** re-opens consumed keys, and the next replay of a client retry executes the mutation a second time. Story 12.1 (durable repository) and Story 12.6 (durable admission) can reasonably assume opposite answers: restore-with-the-streams, or never-restore-admission-records-backwards.

**Fix:** one sentence in the same item — "and the rule for whether admission records are restored with the streams, restored forward-only, or held out of restore entirely, given that a rolled-back admission record re-opens a consumed key."

---

## RU-M2 — MEDIUM — The validation, gap-analysis, and readiness sections still describe the 2026-07-19 draft

**Checklist item:** 8 · **Prior ID:** RUB-14, mostly unmoved

| Location | Text | Contradicted by |
| --- | --- | --- |
| `:797` | `[x] **C3 commit-TTL retention period set** … PM-approved 2026-06-22; Legal-approved 2026-06-24` | `:232`, `:328`, `:439`: C3 is **superseded, approval-pending** under A7b — restated in three more places by this amendment, while the Phase-1-entry checkbox stayed ticked |
| `:799` | `[ ] **C6 Workspace State Transition Matrix enumerated**` (unchecked) | the matrix exists at `:363–448` and was amended twice |
| `:1773` | Gap Analysis, framed at 2026-07-14/15 | does not list PD10's breaking spine remediation, the four superseded approvals, OQ11–OQ13, the NFR74–NFR84 admission, or any of the ten open items |
| `:1834` | *"**Overall Status (updated 2026-09-12): NOT READY**"* | predates two amendments; frontmatter says `updated: '2026-09-16'` and `implementationReadiness: 'not-ready (2026-07-14/15)'` — three dates, none matching this revision |
| `:1842` | *"`FolderStateTransitions.cs` translates **1:1**; matrix-coverage CI gate prevents drift"* | both halves false by the document's own `:240` and `:440`; the gate is the thing that passed the destructive build |
| `:1842` / `:1797` | *"~30 transitions"* | the table carries **37** transition rows |
| `:1871` | must-pass gate list | still no PD11 triple-keyed conformance assertion, no C9 event-write token-substitution test, no S-7 denial-envelope gate — the three measurement methods `:352`/`:355` introduce |

Individually cosmetic; together they mean the last 130 lines of the document still answer a question the first 800 lines have re-answered twice.

---

## RU-M3 — MEDIUM — `withheld` and the `confidential` tier still reach no UX or concern statement, and PD8 still has no lockstep list

**Checklist item:** 1 · **Prior ID:** RUB-6, labelled but not propagated

`withheld` occurs at exactly four lines: `:243` (the honesty table saying it does not exist), `:281` (the unowned-story bullet), `:655` (S-6), `:656` (S-7's un-transcribed sentence — **RU-H1**). Every downstream statement of the console's state vocabulary still models the old two-way split:

- `:309` UX Design Integration Implications — *"Redacted, inaccessible, unknown, missing, unavailable, failed, delayed, dirty, locked, ready, and committed states must be visually and semantically distinct"* — no `withheld`. This is the list Epic 6/8 treat as normative.
- `:114` concern #11 — still *"contact your administrator"*, copy S-6 says is wrong for a withheld value, because no administrator can reveal it.
- `:120` concern #17 — still *"per-tenant policy (hash/truncate/redact/expose)"* and *"classification applies uniformly across audit, projections, and console responses"*: read-side framing, and "hash" rather than the keyed write-time substitution S-6 decides.
- `:749` F-5 — redaction affordance only.

And PD10 has an explicit regeneration list (`:665`: spine, client, parity fixtures, `previous-spine.yaml`, C13 inventory, docs, tests). PD8 has none, though `SensitiveMetadataTier` gaining a `confidential` member and the console enums gaining `withheld` are breaking wire and UI additions.

**Fix:** add `withheld` to `:309` and F-5 with its own copy distinct from the redaction copy; rewrite concern #17 to write-time substitution; give PD8 the same explicit lockstep list PD10 has.

---

## RU-M4 — MEDIUM — Counts, in a document that now forbids transcribed counts

**Checklist item:** 2 · **Prior ID:** RUB-13, one of four fixed

The one that was fixed is the right one and was fixed the right way — `:242` now reads *"HTTP 403 is live on **every protected operation in the current generated Contract Spine inventory** (`authorization-matrix.md` G1: 403 on 49 of 49, 404 on 46 of 49 — the denominator is the generated inventory, never a number transcribed here)."* That is the pattern.

Three transcribed counts survive, two of them in the same section as the rule:

| Claim | Verified |
| --- | --- |
| `:55` / `:1751`: *"58 functional requirements across **12** capability blocks"* / *"FR1–FR58 across **12** capability groups"* | `prd.md` §Functional Requirements has **11** `###` capability subsections (Glossary excluded); `:55`'s own enumeration lists **11** |
| `:732`: *"the full `CanonicalErrorCategory` enum (**43** post-SDK members)"* | the spine enum at `hexalith.folders.v1.yaml:11054` has **49** members. Unchanged across three gates now |
| `:656` / `:679`: *"All **49** protected Contract Spine operations"* / *"The **49** protected operations"* | directly against `:242`'s own rule, eight lines apart in one case |

`:732` closes with *"Surface denominators … are always the **current generated Contract Spine inventory** — never hard-coded counts (2026-07-15)"*, in the same sentence as the 43.

---

## RU-M5 — MEDIUM — Staged-content retention is still stated three ways; the open item covers the clock, not the eligibility rule

**Checklist items:** 3, 8 · **Locations:** `:413`, `:424–425`, `:433`, `:441`, `:1016` · **Prior ID:** RUB-16, partially addressed

The cleanup trigger was correctly restated in C3's vocabulary at `:1014` — *"`inaccessible` is a workspace state, not a task closure, and does not trigger cleanup"* — which is a real fix and resolves the trigger half.

The eligibility half is untouched and still contradicts PD11 rule 3:

- `:1016`: *"Temporary working files are deleted at the C3 seven-day boundary; `changes_staged`, `dirty`, `unknown_provider_outcome`, and `reconciliation_required` are **never cleanup-eligible**."*
- `:425` rule 3 / `:433`: recovery from `inaccessible` branches on whether *"staged content remains within the C3 window"* — a branch whose `→ ready` side requires the staged content to be **gone**.

If `dirty` is never cleanup-eligible, staged content never expires and the `inaccessible → ready` branch is unreachable. The open item at `:441` catches the unreachability from the *clock-start* direction and is a good catch; it does not mention `:1016`, so a reader fixing the clock can leave the eligibility rule in place and still have an unreachable branch.

**Fix:** fold `:1016` into the `:441` open item, or state cleanup eligibility once and cross-reference from the other two.

---

## RU-M6 — MEDIUM — Two contradictions inside the matrix and the PD10 gap claim, both carried

**Checklist item:** 8 · **Prior ID:** RUB-17, unmoved

**(a)** `:417`: `changes_staged → dirty` on `LockLeaseExpired` (mutations applied, lock lost), side effect *"**Operator intervention required**"*. PD11 rule 2 (`:424`) says the originating task resumes via `dirty` + `WorkspaceLocked` → `changes_staged`, and the `dirty` catalog row (`:375`) assigns `degraded-but-serving` *"while the originating task can still resume"*. An implementer reading the table row sets `awaiting-human` on the exact path PD11 rewrote to be self-recovering — and `:440` already records that `DispositionLabelMapper.cs` gets `AwaitingHuman` wrong today, so the table row reinforces the live defect.

**(b)** `:665`: *"The matrix already records this remediation as gaps `G1` through `G11` — including `G4`, which this correction must close alongside the rest."* `docs/contract/authorization-matrix.md` assigns **G6 → "OQ9 incident-access evidence"** and **G7 → "Story 12.1 durable persistence and C7 runtime evidence"** — neither is PD10 work. Claiming G1–G11 wholesale gives two gaps two owners each, and the matrix is the digest-bound artifact A6b reapproves.

---

## RU-M7 — MEDIUM — The console's live-freshness mechanism is still named three ways and decided none

**Checklist item:** 7 · **Locations:** `:746` (F-2), `:1650`, `:1682`, `:1684` against C2 · **Prior ID:** RUB-19, unmoved

- F-2: *"Blazor Web App, Interactive Server render mode (SignalR) through `FrontComposerShell` **for live status updates**"* — a render channel, not a change source
- `:1650`: *"UI → Server: … **reads only from projection endpoints**"* — pull
- `:1682`: *"UI SignalR notification → live status update"* — push, from a hub no decision creates and no app-ID/service-invocation rule permits (`folders-ui` has no declared subscription in I-3/I-4)
- `:1684`: *"202 Accepted + correlationId for **status polling**"* — poll

C2 pins a 500 ms status-freshness target and F-7 a p95 page-load budget, so the mechanism and cadence are budget-relevant. Note this interacts with the new `:675` degraded-mode decision: if protected reads now return a retryable 503 beyond the staleness bound, the console's refresh loop needs a stated backoff, and none of the four statements owns one.

---

## RU-M8 — MEDIUM — The MVP release-reason escape hatch is still a non-exhaustive enumeration inside a normative clause

**Checklist items:** 3, 2 · **Location:** `:656` · **Prior ID:** RUB-18, one of three carried

Two of the three escape hatches I flagged are now routed and I am withdrawing them: NFR75's *"unless an approved deployment policy allows them"* is covered by the new destination-policy open item at `:677`, and "supported deployment profile" by `:767`. Good.

The third is unchanged: *"MVP release reasons permit only approved values **such as** `caller_completed`; reserved post-MVP reasons are rejected."* "Such as" makes the allowed set non-exhaustive inside a clause that then says the complement is rejected, so "rejected" cannot be tested. "Release reason" is also undefined at first use. Enumerate the set or point at the closed vocabulary in `parity-contract.schema.json` the way the rest of S-7 now does.

---

## RU-M9 — MEDIUM — Alerting/SLO ownership and the runbook inventory stay partial, and the new DR item raises the stakes

**Checklist item:** 7 · **Locations:** I-6/I-7, `:271`, `:1784` · **Grep:** `on-call` = 0

Alerting exists only as Epic-13 scope (*"wire the five declared-only alert instruments"*, `:271`) and NFR82's mechanism-neutral obligation. No decision names who owns an alert, what an SLO is, or where a burn is routed. Runbooks: `docs/runbooks/` is declared in the tree, `tenant-deletion.md` is listed under *Minor Gaps* (`:1784`), and `:1685`'s *"production policies maintained outside repo per ops runbook"* names no artifact. The new DR open item at `:769` explicitly requires *"the restore runbook"* — so the runbook set is now load-bearing for a release-blocking dimension while having no inventory, no ownership, and no gate.

---

## RU-L1 — LOW — `C20` still does not exist, and now points away from the fix

`:653` (S-4): *"local tenant-access projection (fail-closed-on-stale per **C8 + C20**)"*. The exit criteria are C0–C13; the intended reference is cross-cutting concern #20. This is more confusing after the amendment, not less: the real target is now the degraded-mode reconciliation note at `:675`, which is exactly where a reader chasing `C20` should land and cannot.

## RU-L2 — LOW — Five Octokit version pins survive the amendment's own no-version-numbers rule

The version-pin reform is the right fix and I want it kept: `:1741` now says *"Versions are owned by `references/Hexalith.Builds/Props/Directory.Packages.props` … **this document names technologies, never their version numbers**"*, with a stated owner-on-disagreement rule and an explanation of why the 2026-05 copy was the only stale artifact. I confirmed the props file exists and that `Octokit 14.0.0` in it matches.

`Octokit 14.0.0` nonetheless still appears at `:604`, `:690`, `:1311`, `:1545`, `:1654`. The pin is **correct**, so this is a rule-consistency defect rather than staleness — but the rule is one violation away from being ignorable, and `:1545` (`14.0.0/openapi-snapshot.json`) is a Forgejo snapshot path that may be a false positive worth checking before editing.

## RU-L3 — LOW — Cost still unowned; frontmatter still carries three readiness dates

- `grep -ic cost` = 9, every hit the word "cost" inside an *Alternatives considered* cell. No cost envelope, no per-tenant cost model, no cost-of-retention statement against D-7's P7Y tier. Defensible at this altitude for a self-hosted container product; recorded so the sweep is complete.
- `updated: '2026-09-16'` (`:39`) vs `implementationReadiness: 'not-ready (2026-07-14/15)'` (`:42`) vs *"§Readiness (updated 2026-09-12)"* (`:1834`). Either advance them or state that readiness is deliberately frozen until the rank-50 rerun. The new `validationRemediation` key (`:41`) is a good addition and the right place to say so.

---

# Prior-finding closure tally

| Prior ID | Severity | Status | Evidence |
| --- | --- | --- | --- |
| RUB-1 — NFR coverage claim false, ✅ over-claims | CRITICAL | **CLOSED** | Heading is now *"Requirements Coverage Validation — nine of eleven NFR categories"*; `:1753` states *"Non-Functional Requirements Coverage — partial, and the gap is named"*; `:1755` names both admitted bands, cites the `—` rows in `nfr-traceability.md`, and applies the invariant *"admission is not coverage"*; the *Verification Expectations* bullet is scoped to *"the nine original categories"* and says NFR74–84 *"have none"*. It went further than I asked: three individually weak gates (I-3 schedule-only, I-8 not built, C10 met by a different gate) are now labelled inside the bullets. Residual is the FR-block count only → **RU-M4** |
| RUB-2 — disaster recovery silent | CRITICAL | **ROUTED — adequate** | `:769`, owner Architecture + Operations with Epic 13. Names RPO/RTO per store, backup mechanism + retention, restore runbook, the restore-vs-C3/deletion reconciliation, and restore-exercise cadence; correctly identifies the event store as the single point of total loss. Two residuals: the consumed-key restore direction → **RU-M1**; no rank → **RU-C1** |
| RUB-3 — event-payload schema evolution silent | CRITICAL | **ROUTED — adequate** | `:644`, owner Architecture with Epic 12. Names the version marker, where upcasting runs, the additive-vs-breaking compatibility rule, and the replay-reproducibility question; grounds it in the live `LockLeaseBecameStale` addition and the P7Y retention. `prd.md:651`'s event-payload requirement is now acknowledged rather than covered, which is the honest state. Residual: no rank → **RU-C1** |
| RUB-4 — C6 lockstep unenforceable, gate misnamed, drift live | HIGH | **PARTIAL** | Honesty closed (`:440` names both live divergences). Triple keying reached 3 of 7 statements → **RU-H3**. No doc↔code gate, and the lockstep added more unenforced normative content → **RU-H6** |
| RUB-5 — S-7 envelopes not named where the columns derive | HIGH | **PARTIAL — the important half closed** | The three envelopes now transcribe verbatim from the approved matrix (verified field by field); invented `authorization`/`availability`/`retry_after_backoff` gone; the missing 401 added; envelope identity extended past `category`. The CLI/MCP tables did not move and the derivation claim is broken → **RU-H2**; the un-transcribed `visibility` sentence is new → **RU-H1** |
| RUB-6 — `withheld`/`confidential` never reach UX, F-5, concerns | HIGH | **OPEN (now labelled)** | `:281` acknowledges it; `:309`, `:114`, `:120`, `:749` unchanged → **RU-M3** |
| RUB-7 — degraded mode decided twice in opposite directions | HIGH | **CLOSED** | `:675` settles it once: stale or unavailable authority evidence returns the 503 envelope and is never served from a stale projection; concern #20's allowance is scoped to read models that are not authorization evidence; the availability consequence is priced into OQ12/OQ13; concern #20 at `:122` carries the back-reference. Model answer |
| RUB-8 — tokenizer has no owner, no key management, gate mispointed | HIGH | **PARTIAL** | Homelessness now disclosed at `:281`. Three pointers still name the wrong file → **RU-H4**; key store/rotation still undecided **and unrouted** → **RU-H5** |
| RUB-9 — PD8/PD11 code-landing work unowned at every rank | HIGH | **CLOSED** | `:278` — *"None of the three PRD corrections has an owning story (open — route to PM + Delivery)"*, verified again 2026-09-16 with zero `epics.md` occurrences, followed by a per-correction bullet naming exactly what each needs. `epics.md` story admission is correctly recorded as owed under the ratified scope rather than applied |
| RUB-10 — structure mapping presented as complete and is not | HIGH | **PARTIAL — and the contradiction sharpened** | `:1604` banner is the right disclosure; the checklist ticks 200 lines later were not updated → **RU-C2** |
| RUB-11 — write concurrency undecided | HIGH | **ROUTED — inadequately** | `:642` is a well-written item (three candidate mechanisms, the losing-writer error, an explicit statement that two units would diverge) sitting in front of rank-10 Story 12.1 with no rank of its own → **RU-C1** |
| RUB-12 — deployment/environment/replica envelope thin | HIGH | **ROUTED — adequate** | `:767` names the supported profile, the environment list, the per-app-ID replica and scaling rule, and configuration precedence, anchored to OQ12/OQ13. Residual: NFR80/NFR81 work sits at rank 10 while the anchor is rank 40 → **RU-C1** |
| RUB-13 — wrong counts in the paragraph forbidding counts | MEDIUM | **PARTIAL (1 of 4)** | `:242` fixed exemplarily; `:55`/`:1751` (12), `:732` (43), `:656`/`:679` (49) unmoved → **RU-M4** |
| RUB-14 — validation/readiness sections assert a contradicted state | MEDIUM | **MOSTLY OPEN** | → **RU-M2** |
| RUB-15 — reality table incomplete; disposition not generatable | MEDIUM | **MOSTLY CLOSED** | Both disposition divergences are now named at `:440`, and the PD11 row was corrected in the **strict** direction (two transitions accepted onto the unguarded branch, so the guarded outcome is unreachable). The disposition function's signature is still undeclared, so `DispositionLabelMapper.cs` still has nothing generatable for the two conditional cells → folded into **RU-M2** |
| RUB-16 — staged-content retention stated three ways | MEDIUM | **PARTIAL** | Trigger half fixed at `:1014`; eligibility half unmoved → **RU-M5** |
| RUB-17 — two contradictions in the matrix / G-gap claim | MEDIUM | **OPEN** | → **RU-M6** |
| RUB-18 — escape hatches undefined | MEDIUM | **PARTIAL (2 of 3)** | NFR75 routed at `:677`; deployment profile routed at `:767`; release reasons unmoved → **RU-M8** |
| RUB-19 — console live-freshness named three ways | MEDIUM | **OPEN** | → **RU-M7** |
| RUB-20 — cost unowned | LOW | **OPEN** | → **RU-L3** |
| RUB-21 — residual editorial (`C20`, frontmatter, findability) | LOW | **PARTIAL** | Amendment findability materially improved — `validationRemediation` in frontmatter plus dated in-line labels ("reconciled 2026-09-16", "arbitration, 2026-09-16") let a reader find the delta. `C20` → **RU-L1**; frontmatter dates → **RU-L3** |

**Tally: 3 of 3 criticals disposed (1 closed outright, 2 routed adequately, and the write-concurrency route inadequate) · 9 highs → 3 closed, 6 partial · 7 mediums → 1 mostly closed, 3 partial, 3 open · 2 lows → 1 partial, 1 open.**

---

## What this revision does well (so the next fix pass does not undo it)

1. **The S-7 rewrite is the correct pattern for a contested contract.** Naming one owning artifact, transcribing from it verbatim, and adding *"on any disagreement the matrix wins and this row is the defect"* removes the whole class of drift my prior review found. I checked all three outcomes field by field against `authorization-matrix.md` and they match exactly. Every other contested vocabulary in this document should be restated this way.
2. **The version-pin reform kills a recurring drift class rather than resetting its clock.** Deferring to `Directory.Packages.props` with a stated owner-on-disagreement rule, and explaining that the in-document copy was the only stale artifact, is the durable fix. Do not re-inline pins.
3. **The reality table was corrected in the strict direction.** Discovering that two PD11 transitions are *accepted onto the unguarded branch* — making the guarded outcome unreachable rather than merely absent, and the pair-keyed gate a passing gate for a destructive build — is the kind of finding that only comes from checking one's own honesty table against the code. `:240` is now the most load-bearing paragraph in the document.
4. **`:675` is a model conversion of a contradiction into a decision.** It names the deciding authority, states the single rule, scopes the losing statement's surviving allowance, and *prices the consequence* into the open questions that must accept it. Reuse this shape.
5. **The durable-guard-field declaration at `:446`** is real architecture: it recognizes that "evaluated server-side from durable state" is unsatisfiable without named fields, declares `stagedByTaskId`/`stagedByPrincipal`, states their lifecycle, rules out the caller-supplied header explicitly, and enumerates both failure modes of omitting them. It needs a gate, not a rewrite.
6. **The single-writer arbitration for the semantic-indexing bridge (`:676`)** closes a divergence between Stories 10.7 and 12.5 that no prior review had found. Unprompted, correct, and exactly the kind of thing this altitude owns.
7. **The honesty-label discipline has spread from the 2026-09-15 amendment to the 2026-05 strata** — the structure-mapping banner, the Testcontainers correction, the I-3/I-8/C10 gate labels. The one place it did not reach is the completeness checklist, which is **RU-C2** and is two edits.

## Recommended close-out order (all inside the ratified scope)

1. **RU-C1** — add a rank or a blocking edge to each of the four routed Tier-3 items in the wave table. Four lines; unblocks the item-3 checklist failure.
2. **RU-C2** — scope or unbox the two completeness ticks. Two lines.
3. **RU-H1 / RU-H2** — the `visibility` sentence and the CLI/MCP tables; both are PD10 change-set work the document already scopes.
4. **RU-H3** — propagate triple keying to `:1874`, `:352`, `:325`, `:396`, `:107`.
5. **RU-H4 / RU-H5** — name the tokenizer component and repoint `:355`/`:1636`; add the key-management open item.
6. **RU-M2** — one sweep of the last 130 lines.
