# Rubric Walker Review — architecture.md, 2026-09-16 RE-GATE (pass 2)

- **Reviewer lens:** Rubric Walker (good-architecture checklist + full dimension sweep), BMad architecture Reviewer Gate — *re-gate* of the second fix pass
- **Subject:** `_bmad-output/planning-artifacts/architecture.md` (1907 lines; was 1902 at pass 1, 1857 at pass 0)
- **Altitude:** INITIATIVE / whole-system. Level below: EPICS (`epics.md`). Driving spec: `prd.md` (FR1–FR58, NFR1–NFR84) + `ux-design-specification.md`
- **State reviewed:** working tree at `de281e7` + the uncommitted 2026-09-16 amendment, **pass-2 state**
- **Prior gate:** `review-rubric-update-2026-09-16.md` — FAIL, 2 critical / 6 high / 9 medium / 3 low
- **Mode:** read-only. Every load-bearing pass-2 assertion in my area was verified against the repository rather than accepted; verification commands and results are inline. This report is the only file written.

## Verdict: **FAIL** — real closure on four of five named items, but the RU-C1 fix introduced a provably invalid schedule edge and the RU-H2 fix introduced a false provenance claim in the A6b-blocking row

Pass 2 is a genuine second improvement, and it is visibly self-critical: the memlog records the author catching their own pass-1 defects (the stale bridge limitation, the wrong sole-writer arbitration, the non-existent Guard column, a third hard-coded `49`). Three of those self-corrections I verified independently and they are **right** — the bridge registration, the 12.4 fail-closed anchor, and the three-column/41-edge CI pin all reproduce exactly against the repo. RU-C2 closes outright. RU-H1 and RU-H3 close in substance.

It still fails, for two reasons, both of which are defects *created by the pass-2 fixes*:

1. **RU-C1's fix inverts a dependency instead of scheduling it.** Write concurrency is now "before rank 10"; the deployment/replica envelope is "before rank 40" — and the deployment item states twice, in its own paragraph, that it *gates the write-concurrency decision*. A prerequisite at rank 40 for a dependent at rank 10 is the exact edge §"Release Authority Overlay" says manifest validation **rejects**. Pass 1 had two items mutually blocking with no ranks; pass 2 gave them ranks that make the cycle machine-checkably wrong in the one direction the rule forbids.
2. **RU-H2's fix replaces a false derivation claim with three false provenance claims**, inside S-7 — the row the document itself says gates the A6b reapproval. `parity-contract.schema.json` does not close "exactly two axes"; it closes **thirteen**, including `cli_exit_code` as a 15-value enum. And `parity-contract.yaml` does **not** lack rows for `authentication_failure` / `read_model_unavailable` / `projection_unavailable` — it carries all three, mapped to exits 65 / 72 / 72. The real defect (this document's own table defines 72 as `reconciliation_required`, "not retryable until cleared") is named, but the mechanism around it is wrong, and it points the remediation at the wrong work.

Everything else is high or below, and the high band is now dominated by carried findings rather than new ones.

---

## Checklist scorecard

| # | Checklist item | Pass 0 | Pass 1 | **Pass 2** | One-line basis |
| --- | --- | --- | --- | --- | --- |
| 1 | Fixes the real divergence points for the level below and misses none | PARTIAL | PARTIAL | **PARTIAL** | S-7's honest "owed, not achieved" reaches the reader; the CLI exit-code table (`:719–735`) and MCP kind set (`:737`) still do not move, and now disagree with the shipped oracle on exit 72 (**RG-C2**, **RG-H7**). `withheld` still reaches no UX or concern statement (**RG-M8**). |
| 2 | Every rule is enforceable and actually prevents its stated divergence | FAIL | PARTIAL | **PARTIAL (better)** | Triple keying now reaches 6 of 7 statements (`:326`, `:353`, `:366`, `:386`, `:448`, `:1102`, `:1879`); only concern #4 at `:107` is left (**RG-M1**). But I verified the one gate that reads the transition table keys edges on `(from → to : event)` — structurally blind to guards — so nothing enforces the triple anywhere (**RG-H6**). |
| 3 | Nothing deferred or left open could let two units diverge | PARTIAL | FAIL | **FAIL** | Ten routed open items; four now carry rank entry-conditions **stated only in prose in the decision tables**, none of which reaches the wave table that `:212` calls the only machine-checkable schedule (**RG-H1**) — and two of the four ranks are inverted against each other (**RG-C1**). |
| 4 | Named technology is verified-current | PASS | PASS (stronger) | **PASS** | The version-pin reform holds. Residual: five `Octokit 14.0.0` sites survive the document's own no-version-numbers rule (`:605`, `:695`, `:1316`, `:1550`, `:1659`); the pin is correct, so this is rule-consistency only (**RG-L2**). |
| 5 | Ratifies rather than contradicts the brownfield codebase | PASS (note) | PASS (stronger) | **PASS (stronger still)** | Pass 2's three biggest self-corrections all verify: `FoldersServerServiceCollectionExtensions.cs:131–134` does bind `EventStoreSemanticIndexingBridgeStore`; `:67` does register `UnavailableWorkspaceCommitExecutor` and `OctokitGitHubApiClient.cs:60` is the only `NotImplementedException` in `src/`; `.slnx` is 14 src / 17 test. The one new as-built contradiction is S-6's `withheld` non-collision claim (**RG-H2**). |
| 6 | Covers the capabilities of the specs that drove it | FAIL | PARTIAL | **PARTIAL** | Unchanged from pass 1: the two admitted NFR bands are honestly labelled uncovered; the FR-block count is still wrong (**RG-M5**). |
| 7 | **Every structural dimension the altitude owns is decided, deferred, or an open question** | FAIL | PARTIAL | **PARTIAL** | No dimension moved between pass 1 and pass 2. Key management / secret rotation is still the one undecided dimension with no routed item — `grep -c rotat` = **2**, both pre-existing (**RG-H3**). Cost is still silent; alerting/SLO ownership and the runbook inventory are still partial. |
| 8 | No new decision weakens or contradicts an inherited one | PARTIAL | PASS | **PARTIAL (regressed)** | Pass 2 correctly *reversed* its own pass-1 over-claim that the matrix settles degraded mode — I verified `authorization-matrix.md:76` routes `stale` to `safe-denial-404` while `:146` routes stale authority evidence to `authority-unavailable-503`, so the reversal is right. But `:666` still claims gaps `G1`–`G11` wholesale for PD10 and pass 2 *sharpened* the claim on `G4`, against the matrix's own owner column (**RG-H4**). |

---

## Dimension sweep

Every structural dimension this altitude owns, with an explicit disposition. Grep terms given for SILENT rows and for anything that moved.

| # | Dimension | Pass 1 | **Pass 2** | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Paradigm & style | Decided | **Decided** | `:74–82`, D-1 `:632` |
| 2 | Boundaries & dependency direction | Decided | **Decided** | §Architectural Boundaries `:1572–1605`; `Hexalith.Folders.EventStore` correctly added `:1588`/`:1594` |
| 3 | State mutation & persistence | Decided | **Decided** | D-1/D-2/D-3; Epic 12 `:257–268` |
| 4 | Shared-data ownership | Decided | **Decided (corrected)** | The bridge arbitration at `:676–680` was **rewritten** in pass 2 from a wrong sole-writer rule to a field split. I verified it: `EventStoreSemanticIndexingBridgeStore` implements both interfaces, Workers registers `ISemanticIndexingBridgeWriter` (`FoldersWorkersModule.cs:76`) and Server does not. The rewrite is correct. |
| 5 | Identity & multi-tenancy | Decided | **Decided** | `:79`, concerns #1/#12/#13/#14 |
| 6 | Authentication & authorization | Decided | **Decided, one contradiction re-opened honestly** | S-1…S-8; degraded mode correctly demoted from "reconciled" back to `Open —` at `:674` |
| 7 | API/contract strategy & versioning | Decided | **Decided** | A-1/A-3/A-11, C0/C13 |
| 8 | Event/message payload schema evolution | ROUTED OPEN, no rank | **ROUTED OPEN + prose rank** | `:645` — "rank-10 entry condition". Not in the wave table → **RG-H1** |
| 9 | Cross-surface parity | Decided | **Decided** | §Adapter Parity Contract `:702–744`, C13 |
| 10 | Error & failure semantics | Partial | **Partial, now with a false provenance paragraph** | S-7 `:657` honest on "owed"; three verifiable claims in that paragraph are false → **RG-C2** |
| 11 | Concurrency & locking (distributed lock) | Decided | **Decided** | Concern #4, §Locking, C7 |
| 12 | Write-concurrency on the aggregate stream | ROUTED OPEN, no rank | **ROUTED OPEN + prose rank, inverted** | `:643` "before rank 10"; gated by `:772` "before rank 40" → **RG-C1** |
| 13 | Idempotency | Decided | **Decided** | A-9, D-7 — still the strongest section |
| 14 | Eventing & messaging topology | Decided | **Decided** | D-4/D-5, I-9 |
| 15 | Read models & projections | Decided | **Decided** | `:185–197`, D-10, field split `:676` |
| 16 | Data retention & lifecycle | Decided, 1 contradiction | **Decided, same contradiction** | `:1021` "`dirty` … **never cleanup-eligible**" still unreconciled with PD11 rule 3's C3-window branch → **RG-M7** |
| 17 | Search/indexing | Decided | **Decided (as-built corrected)** | `:149–153`/`:183` limitation-closed rewrite — **verified true** |
| 18 | Frontend architecture | Decided, one hole | **Decided, same hole** | live-refresh still named four ways (`:751`, `:1655`, `:1687`, `:1689`) → **RG-M9**; `withheld` absent from `:310`/F-5 → **RG-M8** |
| 19 | Observability & telemetry | Partial | **Partial** | I-6/I-7; `grep -ci on-call` = **0**; no SLO ownership → **RG-M10** |
| 20 | Testing strategy & CI gates | Decided | **Decided, inventory stale** | `:1102` triple-keyed ✔, but still credits C10 lint / Dapr negative tests / rate-limit chaos as CI gates that other sections say do not exist → **RG-M3** |
| 21 | Deployment, environments & configuration | ROUTED OPEN, no rank | **ROUTED OPEN + prose rank, inverted** | `:772` → **RG-C1**, **RG-H1** |
| 22 | Infrastructure & provider strategy | Decided | **Decided** | D-3/D-5, I-1…I-4, C12/OQ4 |
| 23 | Operations & runbooks | Partial | **Partial** | DR item `:774` requires "the restore runbook"; §Minor Gaps still lists only `tenant-deletion.md`; no inventory, ownership, or gate → **RG-M10** |
| 24 | Migration & rollout of breaking changes | Open, no owner | **Open, still no owner and no rank** | `:672` — "an **open item, not a settled one**", but alone among the ten it names no route and no rank → **RG-M12** |
| 25 | Performance & capacity | Decided + open evidence | **Decided + open evidence** | C1/C2/C4/C5, F-7, OQ13 |
| 26 | Scaling & replica topology | ROUTED OPEN | **ROUTED OPEN** | folded into `:772` → **RG-C1** |
| 27 | Disaster recovery / backup / restore | ROUTED OPEN, adequate | **ROUTED OPEN + prose rank 40** | `:774`. Consumers are rank 10 (12.1 durable repository, 12.6 durable admission); the consumed-key resurrection direction still unnamed → **RG-M6** |
| 28 | Cost | SILENT | **SILENT** | `grep -ci cost` = **9**, every hit still the word inside an *Alternatives considered* cell → **RG-L4** |
| 29 | **Key management & secret rotation (S-6 tenant key)** | Effectively SILENT, unrouted | **Effectively SILENT, still unrouted** | `grep -n rotat` = `:282` (acknowledgement inside the unowned-story bullet) and `:652` (S-2 JWKS refresh). Ten `Open —` items exist and none is this one → **RG-H3** |
| 30 | Accessibility | Decided | **Decided** | F-3, `accessibility-gates` axe job |
| 31 | Localization / i18n | N/A | **N/A** | not a spec capability |

**Net movement pass 1 → pass 2: zero dimensions changed disposition.** Four gained a prose rank; one (shared-data ownership) had a wrong decision replaced with a right one. The dimension that was silent-and-unrouted at pass 1 is silent-and-unrouted at pass 2.

---

# Findings

Severity: **critical** = a claim the document makes about a load-bearing artifact or about its own schedule that is false or self-invalidating · **high** = an unenforceable load-bearing rule or a real gap · **medium** = partial coverage or an internal contradiction · **low** = cosmetic.

---

## RG-C1 — CRITICAL — The rank fix inverts the dependency it was meant to schedule: a rank-40 item is declared to gate a rank-10 item

**Checklist items:** 3 (primary), 7 · **Locations:** `:643` and `:772` against the rank rule at `:214` · **Prior ID:** RU-C1, fix applied and defective

This is the direct product of the RU-C1 remediation, so I want to be exact about what improved and what broke.

**What improved:** all four Tier-3 items now say when they must close, in their own words, and `:643` goes further than I asked — it explains *why* a prose entry-condition was chosen: *"An open item with no rank cannot gate anything under the §7.1 rule, so this one is stated as a rank-10 entry condition rather than a parallel question."* That is the right instinct.

**What broke:** the two ranks chosen contradict each other. Reading them together:

| Open item | Stated rank | What it says about the other |
| --- | --- | --- |
| `:643` write concurrency | *"must close **BEFORE rank 10**"* | assumes a replica answer exists |
| `:772` deployment / replica envelope | *"required **before rank 40**"* | *"it also **gates the write-concurrency decision above**, which only bites above one replica"* — and again three sentences later, *"This also **blocks** the write-concurrency decision above"* |

`:214` is unambiguous: *"a prerequisite must sit at a strictly lower rank than its dependent … Manifest validation rejects a cycle and **rejects any dependency on an equal or later rank**."* The deployment envelope is declared the prerequisite; it sits at 40; its dependent sits at 10. Under the document's own rule this edge is invalid, and it is invalid in the direction that produces the worst outcome: the rank-10 wave starts, writes the first durable events, and the concurrency mechanism is chosen *after* the replica count it depends on is finally decided at rank 40 — which is a migration, not a decision, by the document's own argument at `:645`.

Note this is strictly worse than the pass-1 state for review purposes and strictly better for the reader. At pass 1 the two items were mutually blocking with no ranks and the contradiction was latent. Pass 2 made it explicit and machine-checkable — and it fails the check.

**Fix (small, three options):** (a) pull the replica question out of `:772` into a rank-0 sub-decision ("how many replicas per app ID does the rank-10 wave assume?") and leave the full profile at rank 40; (b) move the whole deployment item to rank 0 alongside the authority relock, since nothing in it requires Epic 13 output to *decide*, only to *validate*; or (c) restate `:643` as conditional — "if the rank-10 wave runs single-replica by declaration, the concurrency decision defers to rank 40 with that declaration as its entry condition." Option (c) is the cheapest and is architecturally honest: it turns an inverted edge into a recorded assumption with an owner.

---

## RG-C2 — CRITICAL — S-7's replacement provenance paragraph is false on three verifiable points, inside the row the document says gates A6b

**Checklist items:** 1, 2 · **Location:** `:657` · **Prior ID:** RU-H2, fix applied and defective

Credit first: the over-claim I raised is **gone**. S-7 no longer says the exit-code columns are "derived from the matrix". It now says *"The exit-code/failure-kind derivation is **owed, not achieved**"*, and I verified its premise — `grep -n "cli_exit_code\|mcp_failure_kind\|exit code" docs/contract/authorization-matrix.md` returns **nothing**. The matrix genuinely carries no such columns. That half is closed correctly.

The replacement text asserts three things about the two artifacts it names. All three are wrong.

**(1) "`tests/fixtures/parity-contract.schema.json` closes exactly two axes — `canonical_error_category` and `mcp_failure_kind`."** I enumerated every `enum` in that schema. It closes **thirteen**:

| Closed axis | Members |
| --- | --- |
| `operation_family` | 5 |
| `read_consistency_class` | 4 |
| `transport_parity.auth_outcome_class` | 6 (incl. `folder_acl_denied`, `audit_access_denied`, `safe_not_found`) |
| `transport_parity.idempotency_key_rule` | 3 |
| `behavioral_parity.pre_sdk_error_class` | 5 |
| `behavioral_parity.idempotency_key_sourcing` | 6 |
| `behavioral_parity.correlation_id_sourcing` | 7 |
| `behavioral_parity.task_id_sourcing` | 4 |
| `behavioral_parity.credential_sourcing` | 7 |
| `outcome_mapping.items.pre_sdk_error_class` | 5 |
| `$defs/adapter_name` | 5 |
| `$defs/canonical_error_category` | 50 |
| **`$defs/cli_exit_code`** | **15** |
| `$defs/mcp_failure_kind` | 49 |

This is not a pedantic correction. The document uses the false claim to scope the remediation: *"The `code`, `clientAction`, and `visibility` axes have no closed vocabulary anywhere; giving them one is part of this correction."* But `cli_exit_code` **does** have a closed vocabulary, so introducing an exit code for `authentication_failure` or `read_model_unavailable` is a **schema change** — with the `parity-contract.schema.json` validation gate, the `*.Cli.Tests`/`*.Mcp.Tests` consumption gates, and `previous-spine.yaml` all downstream of it. The document tells the PD10 implementer that axis is unconstrained. It is the most constrained one.

**(2) "Today the oracle … [has] **no row** for `authentication_failure`, `read_model_unavailable`, or `projection_unavailable`."** The sentence refutes itself eleven words later (*"the shipped `parity-contract.yaml` maps them to exit 65 and 72"*), and the repo confirms the second half:

```
parity-contract.yaml:56–58   canonical_error_category: 'authentication_failure'   cli_exit_code: 65   mcp_failure_kind: 'authentication_failure'
parity-contract.yaml:596–599 canonical_error_category: 'projection_unavailable'   cli_exit_code: 72   mcp_failure_kind: 'projection_unavailable'
parity-contract.yaml:604–607 canonical_error_category: 'read_model_unavailable'   cli_exit_code: 72   mcp_failure_kind: 'read_model_unavailable'
```

All three have rows, in both columns. The `mcp_failure_kind` enum contains all three.

**(3) "Until those rows exist, CLI and MCP **can** disagree about an authority outage."** They cannot — both derive from the same oracle rows, which agree. The real defect is a different one and the document is one sentence away from stating it correctly: **the oracle and this document disagree.** The oracle maps `read_model_unavailable` and `projection_unavailable` to exit **72**; this document's canonical table at `:730` defines exit 72 as `reconciliation_required`, *"not retryable until cleared"*. So exit 72 carries three categories with **opposite retryability**, and a CLI user who scripts on exit codes cannot distinguish a retryable authority outage from a non-retryable reconciliation state. That is a live, shipped parity defect — strictly more serious than the "missing rows" the document reports, and invisible to anyone who acts on the text as written.

**Fix:** three sentence-level corrections. Say the schema closes thirteen axes and name `cli_exit_code` as the one that makes new exit codes a schema-and-gate change; say the oracle *has* the rows and that they collide with this document's table on 72; and restate the divergence as document-vs-oracle rather than CLI-vs-MCP. Then the surviving obligations — retire `not_found` → 73, and the `auth_outcome_class` existence oracle one surface below the envelope — are correctly scoped.

---

## RG-H1 — HIGH — The four rank entry-conditions never reach the wave table, which the document declares the only machine-checkable copy of the schedule

**Checklist items:** 3, 7 · **Locations:** `:643`, `:645`, `:772`, `:774` against `:212`, `:214`, `:218–225` · **Prior ID:** RU-C1, partially closed

`:212` is explicit about where the schedule lives: *"Until the manifest is regenerated with `execution_rank`, the wave table below is the **only machine-checkable copy of the schedule** … Nothing may cite this table as the schedule once the manifest carries the ranks."* And `:214` makes `execution_rank` *"the sole scheduling authority once it exists."*

The wave table has a column literally headed **"Work and entry conditions"**. I read all six rows. None of the four conditions is in it:

- rank 10 still reads *"Story 12.1 first; 12.2 and 12.3 follow 12.1; 12.6 may resume after 12.1 but cannot close before OQ8 evidence"* — no write concurrency, no schema evolution
- rank 40 still reads *"OQ5 follows the Story 10.8 live round trip … OQ12 and OQ13 follow the Epic 13 security and capacity evidence"* — no deployment profile, no DR
- rank 0 ("Authority relock") is where a "before rank 10" obligation would naturally sit, and lists none of them

So the conditions exist only as prose in §"Data Architecture" and §"Infrastructure & Deployment", 400 and 550 lines from the schedule. Two consequences, and the second is the one that matters:

1. Nothing a reader consults for *what gates rank 10* mentions them. A story author reads the wave table.
2. `planning-story-manifest.yaml` is due for regeneration (I confirmed: `generated_on: '2026-08-04'`, zero occurrences of `execution_rank`). Whoever regenerates it will encode the wave table, because that is what `:212` says the manifest supersedes. The four conditions will not survive the transfer, and at that point the wave table may no longer be cited at all.

**Is a stated entry-condition sufficient?** As an *architecture* statement — yes, materially. It converts "this dimension is open" into "this dimension is open and must close before this wave", which is an owned obligation a human can act on and is the single largest thing pass 2 added for me. As a *gate* — no. It is not a node in the rank graph, so the validator that "rejects any dependency on an equal or later rank" has no edge to reject; and it is not in the artifact the schedule authority will be generated from. The honest summary is that RU-C1 moved from **unschedulable** to **stated but unrouted**.

**Fix:** four cells. Append to the rank-10 row *"— entry conditions: the write-concurrency and event-payload-versioning decisions (§Data Architecture) are closed"*, and to rank 40 *"— entry conditions: the deployment profile and DR decisions (§Infrastructure & Deployment) are closed"*, then add one line to `:212` saying these conditions transfer to the manifest on regeneration. Resolve **RG-C1** in the same edit or the rank-10 cell will encode the inversion.

---

## RG-H2 — HIGH — S-6's `withheld` non-collision claim is falsified by the exact file it cites, and S-7's "zero occurrences anywhere" is false

**Checklist items:** 5, 1 · **Locations:** `:656` (S-6), `:657` (S-7) against `src/Hexalith.Folders.UI/Services/ConsoleStatusText.cs:45` and `docs/contract/safety-invariant-ci-gates.md:59`

The RU-H1 fix is good and I am closing that finding — S-7 now separates *"the six cells per outcome above are transcribed from the matrix and are approved"* from the proposed `visibility` promotion and `withheld`, and says plainly that the proposals *"must not be cited as current contract."* That is the right shape.

Two claims attached to it do not survive checking.

**(a) S-6 `:656`:** *"`withheld` is a **new** render state: it does not collide with the shipped meaning of `redacted` in `docs/.../safety-invariant-ci-gates.md` and `ConsoleStatusText.cs`, which stays 'a value exists and is suppressed for this viewer'."*

The shipped operator-facing string for `redacted`, in the file S-6 names, **is the word `withheld`**:

```
ConsoleStatusText.cs:45   ["redacted"] = "The requested evidence is withheld by tenant policy."
safety-invariant-ci-gates.md:59   `redacted`: a value exists for an authorized audience but is deliberately withheld.
docs/ux/ops-console-wireflows.md:256   redacted | Hidden by tenant policy; value exists but is withheld
```

So the collision S-6 rules out is the one that already exists — not in the enum, but in the operator's vocabulary, which is the layer F-4/F-5 and concern #11 say matters ("silent redaction during an incident reads as a system bug and operators lose time chasing ghosts"). Shipping a `withheld` state that means *"no cleartext exists to show anyone"* alongside copy that already glosses `redacted` as "withheld by tenant policy" reproduces the exact confusion PD8 exists to prevent, and it does so in the three artifacts a console story would copy from.

**(b) S-7 `:657`:** *"`withheld` has **zero occurrences anywhere**, as this document's own reality table records."* False as written — nine occurrences across `src/` and `docs/` (the three above plus `MetadataOnlyFolderTree.razor:39/46/47/49`, `TrustDimensionState.cs:28`, `TenantAccessState.cs:25`, `MetadataOnlyFolderTreeTests.cs:78`). The *intended* claim — zero occurrences as an enum member, render state, or wire token — is true and is what `:244` actually says. But in a row whose entire authority rests on being byte-checkable against a named artifact, an unqualified "anywhere" that a one-line grep refutes is the same defect class as the one this sentence replaced.

**Fix:** qualify the S-7 claim to "zero occurrences as an enum member, wire value, or render state"; and rewrite the S-6 clause to name the collision honestly — *"`redacted`'s shipped operator copy already uses the word 'withheld' (`ConsoleStatusText.cs:45`, `safety-invariant-ci-gates.md:59`, `ops-console-wireflows.md:256`), so introducing `withheld` as a distinct state obliges those three strings to change in the same change set"* — and add that to PD8's (still missing) lockstep list.

---

## RG-H3 — HIGH — Key management and rotation for the S-6 tenant key is still the one undecided dimension with no routed open item

**Checklist items:** 7, 2 · **Location:** `:656` · **Prior ID:** RU-H5, unmoved

`grep -n "rotat" architecture.md` returns exactly **two** hits, both pre-existing: `:282` (an acknowledgement inside the unowned-story bullet — *"needs a tokenizer component, its per-tenant key management and rotation policy"*) and `:652` (S-2's JWKS refresh). Pass 2 touched S-6 twice — it added the "qualified, not absolute" headline caveat and the Dapr-state-store scope in C9's measurement method — and did not add the item.

The gap is unchanged and so is the argument. S-6 introduces a **per-managed-tenant confidential-token key** with a carried key version, and decides nothing about it: no store, no rotation policy, no statement of what rotation does to existing tokens. That last is load-bearing, not administrative: the whole value of the token is that *"the event-write path and the Memories egress must produce the **same** token for the same value, or evidence stops joining"*, and with D-7's P7Y admission retention plus the replay-backed audit projection, historical joins are the point. Rotating a key breaks the join across the boundary for every historical event. Whether that break is acceptable, whether tokens are re-derived, or whether both key versions are carried and joined, is a decision two units will answer differently.

What still makes this a finding rather than a note is the **inconsistent disposition**: pass 1 gave six undecided dimensions a routed `Open —` item, pass 2 gave four of them rank conditions, and this one — the same shape of gap as `:660` (PD8's scope boundary), ten lines away, which got an item, a route, and three enumerated control-set options — got a clause in a bullet about an unowned story. Ten `Open —` items exist in this document. None is this.

**Fix:** one more `Open —` under §"Authentication & Security", routed to Security + Architecture with A5/PD8, naming the key store, the rotation policy and trigger, and the rotation consequence for historical token joins. Give it the rank-10 condition too, since 12.1 writes the first durable events.

---

## RG-H4 — HIGH — The `G1`–`G11` wholesale claim double-owns three gaps against the matrix's own owner column, and pass 2 sharpened it

**Checklist items:** 8, 1 · **Locations:** `:666` and `:268` against `docs/contract/authorization-matrix.md:341–351` · **Prior ID:** RU-M6(b), escalated

`:666`: *"The matrix already records this remediation as gaps `G1` through `G11` — **including `G4`, which this correction must close alongside the rest rather than leave to a later pass**."* `:268` repeats the wholesale claim.

I read the matrix's gap table. It carries an explicit owner column, and three of the eleven are not PD10 work:

| Gap | Matrix owner column | Claimed by `:666` |
| --- | --- | --- |
| `G4` | *"Story 12.1 and Epic 13 runtime authorization work"* (a runtime `EffectivePermissionsActionCatalog.cs` drift) | yes, explicitly |
| `G6` | *"OQ9 incident-access evidence"* (product-inventory, no spine operation exists) | yes, by "G1 through G11" |
| `G7` | *"Story 12.1 durable persistence and C7 runtime evidence"* | yes, by "G1 through G11" |

So the document assigns to the PD10 change set three gaps the matrix assigns elsewhere — and `G6` is a *product-inventory* gap with no Contract Spine operation at all, which a spine correction structurally cannot close. This is not a transcription slip; pass 2 reached into the list and named `G4` specifically.

Why it matters more than an ownership tidy-up: S-7 establishes, correctly and as the fix for my prior finding, that *"on any disagreement the matrix wins and this row is the defect."* The matrix is the digest-bound artifact A6b reapproves. A statement in this document that contradicts the matrix's own owner column is, by the document's own rule, the defect — and it will be read by whoever scopes the PD10 story, who will either over-scope into runtime authorization work or find `G4`/`G7` already claimed by Story 12.1 and stall.

**Fix:** `"gaps G1–G3, G5, and G8–G11"`, with one sentence noting that `G4`, `G6`, and `G7` are recorded in the same table under different owners and are not closed by this correction.

---

## RG-H5 — HIGH — The C9 artifact location, the structure-mapping row, and the tree annotation still name the file S-6 says is not the tokenizer

**Checklist item:** 2 · **Locations:** `:356`, `:1641`, `:1316`-region tree against `:282` and S-6 `:656` · **Prior ID:** RU-H4, unmoved

Pass 2 **edited `:356`** — it added the Dapr state store to the C9 measurement method (*"asserting no durable cleartext in the **Dapr state store** (explicitly in scope — the PD8 carve-out's only candidate home, so a gate that omits it certifies green over the exact hole)"*), which is a genuinely sharp addition and closes a real hole in the gate's scope. But it edited the sentence and left the artifact pointer in the same cell untouched:

- `:356` *Artifact Location:* `this document §"S-6" + Hexalith.Folders/Observability/FolderAuditSanitizer.cs`
- `:1641` structure mapping: `#17 Sensitive metadata classification | Observability/FolderAuditSanitizer.cs`
- project tree: `FolderAuditSanitizer.cs  # C9 metadata-only sanitization per S-6`

Against `:282`: *"**The tokenizer has no owning component in this architecture**: `FolderAuditSanitizer.cs` is explicitly *not* it (S-6), and no other type is named."* And against S-6 itself: *"`FolderAuditSanitizer.cs` is a read/emit-path sanitizer and is **not** the tokenizer."*

So the document now says, in three places, that the C9 artifact is a file it says twice is not the thing C9 measures — and C9 is an approval-pending exit criterion whose reapproval will be measured against `:356`. The pass-2 edit made the cell *more* load-bearing without repointing it.

**Fix:** name the write-path component (e.g. `Hexalith.Folders/Security/ConfidentialValueTokenizer.cs`), repoint `:356` and `:1641` at it, and leave `FolderAuditSanitizer.cs` annotated as the read-path sanitizer it is. A naming decision, not an implementation — inside the ratified scope.

---

## RG-H6 — HIGH — Triple keying is now stated correctly almost everywhere and enforced nowhere; the one gate that reads the transition table is structurally pair-keyed

**Checklist item:** 2 · **Locations:** `:386`, `:448`, `:353` against `tests/.../ConsumerDocsConformanceTests.cs:557`, `:815–838`, `:1126` · **Prior ID:** RU-H3 propagation half closed; RU-H6 unmoved and now sharper

**The pass-2 decision not to add a Guard column is correct, and I verified its stated reason.** `ParseArchitectureC6Transitions` locates the table by the literal string `"| From → To | Triggering Event | Side Effect |"`, so a fourth column breaks the lookup; and `ConsumerDocsConformanceTests.cs:557` asserts `matrixEdges.Count.ShouldBe(41)`. Both pins are real. Carrying the guard inline in bold was the right call.

The consequence, which the document does not state, is that **the triple-keying propagation has no enforcement at all over this table**. The gate builds each edge as:

```csharp
edges.Add($"{from}->{to}:{eventName}");
```

— a `(from, to, event)` key with no guard term. It cannot see a guard, cannot count guard branches, and would silently collapse two guard branches that share a destination state into one edge. So the rule that `:366`, `:386`, `:448`, `:1102`, and `:1879` all now state correctly is enforced by exactly nothing, and `:353`'s claim that *"the PD11 rules are asserted identically against this document, the C6 mapping artifact, and `FolderStateTransitions.cs`"* describes an assertion that exists in none of the three directions. Pass 1 added unreadable normative content to the mapping document (the Guard Discriminators table, safe against the pinned parsers because invisible to them); pass 2 added more correct prose to this one. Both increase the volume of normative content that no gate spans.

`stagedByTaskId` / `stagedByPrincipal` remain **zero occurrences in `src/`** — I re-confirmed — which pass 2 states honestly at `:447`, so that is disclosure working. The gap is that disclosure is now doing the whole job.

**Fix (in scope):** restate `:353` as a target with the gate named as owed — *"no gate asserts this today; the C6 edge gate keys on `(from, to, event)` and is structurally blind to guards"* — so the next reader cannot take a green `ConsumerDocsConformanceTests` run for agreement. Building the guard-aware gate is code work and correctly out of scope; saying it does not exist is not.

---

## RG-H7 — HIGH — The CLI exit-code table and MCP kind set did not move, and now provably disagree with the shipped oracle

**Checklist items:** 1, 2 · **Locations:** `:719–735`, `:737` · **Prior ID:** RU-H2's second half, unmoved

Separate from **RG-C2**'s provenance errors, the underlying gap is unchanged and now measurable:

| S-7 outcome | This document `:719–735` | Shipped `parity-contract.yaml` | Agree? |
| --- | --- | --- | --- |
| `authentication_failure` | **no row** | exit 65, kind `authentication_failure` | no |
| `tenant_access_denied` | exit 66 | exit 66 | ✔ |
| `read_model_unavailable` | **no row**; exit 72 is `reconciliation_required`, *"not retryable until cleared"* | exit 72, kind `read_model_unavailable`, retryable | **contradiction** |
| `projection_unavailable` | **no row**; same 72 collision | exit 72 | **contradiction** |
| retired `not_found` | **still row 73** | still on 66 rows | both stale |

`:737` also still says *"the full `CanonicalErrorCategory` enum (**43** post-SDK members)"*. I counted the spine enum at `hexalith.folders.v1.yaml`: **49**. The schema's `canonical_error_category` carries **50**. Neither is 43, and the same sentence ends *"never hard-coded counts"*.

And `:737`'s closing rule — *"The `kind` set is identical to the canonical category set (one-to-one mapping)"* — is unsatisfiable while the prose set omits three categories the schema's `mcp_failure_kind` enum (49 members) actually contains.

**Fix:** add the three rows with a distinct exit code for the retryable authority outage (72 is taken and means the opposite), mark `not_found`/73 deprecated in the PD10 change set, and replace `43` with a pointer to the generated enum rather than a new number.

---

## RG-M1 — MEDIUM — Concern #4 is the last pair-keyed statement of the C6 rule

**Checklist item:** 2 · **Location:** `:107` · **Prior ID:** RU-H3, 6 of 7 closed

Propagation landed at `:326` (C6 criterion), `:353` (measurement method), `:366` (matrix preamble), `:386` (valid-transitions header), `:448` (aggregate-test gate), `:1102` (CI-gate inventory), and `:1879` (Implementation Handoff — the one I flagged as most-read, now triple-keyed *and* carrying the no-fall-through rule). That is a clean, complete propagation of the five I named plus the two already done.

One survivor: `:107`, cross-cutting concern #4 — *"**a total state-transition matrix where every (state, event) pair has a defined outcome including reconciliation paths**"*. It matters slightly more than its length suggests because `:366` cites concern #4 as the matrix's source of authority (*"per cross-cutting concern #4 + concern #21"*), so the rule's own cited origin is the pair-keyed version. One sed-scale edit.

---

## RG-M2 — MEDIUM — `:386` says a guard-discriminated pair "appears as two rows"; two of the four named pairs have one row

**Checklist item:** 8 · **Location:** `:386` against `:412`, `:413`

`:386`: *"A guard-discriminated pair therefore appears as **two rows** with the same `from` and the same event, distinguished only by the bolded guard — `changes_staged` + `CommitFailed`, `inaccessible` + `ProviderReadinessValidated`, `dirty` + `WorkspaceLocked`, and `dirty` + `LockLeaseBecameStale` **are the four**."*

In the table 25 lines below: `changes_staged` + `CommitFailed` has two rows (`:406`, `:407`) ✔; `inaccessible` + `ProviderReadinessValidated` has two (`:418`, `:419`) ✔; `dirty` + `WorkspaceLocked` has **one** (`:412`); `dirty` + `LockLeaseBecameStale` has **one** (`:413`).

The semantics are fine — the default rule rejects the unenumerated branch, and `:446` says so explicitly for `(dirty-with-staged-changes, LockLeaseBecameStale)`. But the paragraph tells a reader to identify guard-discriminated pairs *by counting rows*, and applying that test to the table marks two of the four as not guard-discriminated. Since this paragraph is the anchor the C6 gate's header lookup sits on, it is the paragraph an implementer reads first.

**Fix:** *"…appears as two rows where both branches have an outcome, and as one row where the unenumerated branch is rejected under the default rule — `dirty` + `WorkspaceLocked` and `dirty` + `LockLeaseBecameStale` are of the second kind."*

---

## RG-M3 — MEDIUM — The CI-gate inventory still credits three gates the document elsewhere says do not exist

**Checklist item:** 2 · **Location:** `:1102` against `:764` (I-3), `:769` (I-8), `:357` (C10)

The strata treatment reached the I-3/I-8/C10 decision rows, the Requirements Coverage bullets, the Key Strengths bullet, and the Implementation Handoff. It did not reach the gate inventory itself, which still lists as live CI gates: *"Cache-key tenant-prefix lint (C10)"* (never built — the as-built gate is `GovernanceCompletenessGateTests`), *"Dapr-policy conformance negative tests"* (schedule-only, cannot block a merge), and *"provider-rate-limit chaos test"* (does not exist).

The same list is also missing all three measurement methods this amendment introduced: the `:353` three-artifact PD11 assertion, the `:356` C9 event-write token-substitution test, and any S-7 denial-envelope gate. So the one section that answers "what must pass before merge" over-counts by three and under-counts by three.

---

## RG-M4 — MEDIUM — The validation and readiness tail still describes the 2026-07-19 draft

**Checklist item:** 8 · **Prior ID:** RU-M2, unmoved except for one row

| Location | Text | Contradicted by |
| --- | --- | --- |
| `:802` | `[x] **C3 commit-TTL retention period set** … PM-approved 2026-06-22; Legal-approved 2026-06-24` | `:234`, `:323`, `:440`: C3 is **superseded, approval-pending** under A7b |
| `:804` | `[ ] **C6 Workspace State Transition Matrix enumerated**` (unchecked) | the matrix at `:364–448`, amended three times |
| `:784` / `:1847` | *"`FolderStateTransitions.cs` translates **1:1**; matrix-coverage CI gate prevents drift"* | both halves false by `:242` and `:430`; the gate is the thing that passes the destructive build |
| `:1847` | *"~30 transitions"* | 37 table rows / **41** gate-counted edges |
| `:1772` | *"12 Critical + 5 Important + 6 Deferred decisions"* | unverifiable against the current decision set; the same paragraph is headed `✅` |
| `:1839` | *"**Overall Status (updated 2026-09-12): NOT READY**"* | predates three amendments; frontmatter says `updated: '2026-09-16'` and `implementationReadiness: 'not-ready (2026-07-14/15)'` — three dates |
| `:1780` | Gap Analysis framed at 2026-07-14/15 | lists none of the ten open items, the four superseded approvals, OQ11–OQ13, or the NFR74–84 admission |

**One row did move and deserves credit:** `:1774` now says the tree is the **target** layout, names `.slnx` as authoritative, and records *"14 src / 17 test at 2026-09-16, and `ScaffoldContractTests` is currently red on its two newest entries, a pre-existing drift unrelated to this document."* I verified 14 src / 17 test in `Hexalith.Folders.slnx`. That is the treatment the rest of these rows need.

---

## RG-M5 — MEDIUM — Three transcribed counts survive in a document that forbids transcribed counts

**Checklist item:** 2 · **Prior ID:** RU-M4, 1 of 3 closed

Pass 2 removed one of the two `49 protected operations` sites (S-7 now reads *"Every protected operation in the current generated Contract Spine inventory"*, which is the correct pattern). Remaining:

| Claim | Verified |
| --- | --- |
| `:684` *"The **49** protected operations are protected because each one opts in"* | the only surviving hard-coded `49`; directly against `:243`'s and `:737`'s own rule |
| `:737` *"the full `CanonicalErrorCategory` enum (**43** post-SDK members)"* | spine enum = **49**; schema `canonical_error_category` = **50**. Unchanged across four gates |
| `:55` / `:1754` *"58 functional requirements across **12** capability blocks"* | `:55`'s own enumeration lists **11** |

---

## RG-M6 — MEDIUM — The DR item still names only the erasure direction of the restore hazard, and its rank-40 placement puts it behind its rank-10 consumers

**Checklist items:** 3, 7 · **Location:** `:774` · **Prior ID:** RU-M1, unmoved and now compounded

The item still names *"a restore from before a deletion can resurrect data whose deletion was a compliance obligation, including admission records under the P7Y retention regime."* The opposite direction — the one that makes two rank-10 stories diverge — is still unnamed: concern #21 (`:124`) says idempotency admission is **not** a rebuildable projection (*"`/project` replay cannot create, erase, or resurrect consumed-key authority"*), and D-7 retains commit-replay results for P7Y so a consumed key stays consumed. A restore that rolls admission records **back** re-opens consumed keys, and the next client retry executes the mutation a second time. Story 12.1 and Story 12.6 can reasonably assume opposite answers.

Compounded by the rank: `:774` puts DR before rank 40, while 12.1 and 12.6 are rank 10 and are exactly the stories that must know whether admission records restore with the streams, restore forward-only, or are held out of restore. The item's own hedge (*"the restore-versus-deletion rule in particular must be settled before any production data exists to restore"*) is a good instinct with no rank attached.

**Fix:** one sentence naming the consumed-key direction, and split the restore-versus-admission rule out as a rank-10 condition while the RPO/RTO/mechanism work stays at 40.

---

## RG-M7 — MEDIUM — Staged-content cleanup eligibility still contradicts PD11 rule 3, and the open item covers the clock only

**Checklist items:** 3, 8 · **Locations:** `:1021`, `:434`, `:442`, `:447` · **Prior ID:** RU-M5, unmoved

`:1021`: *"Temporary working files are deleted at the C3 seven-day boundary; `changes_staged`, `dirty`, `unknown_provider_outcome`, and `reconciliation_required` are **never cleanup-eligible**."* PD11 rule 3 (`:434`) branches `inaccessible` recovery on whether *"staged content remains within the C3 window"* — a branch whose `→ ready` side requires the staged content to be **gone**. If `dirty` is never cleanup-eligible, it never goes, and `inaccessible → ready` is unreachable.

The `:442` open item catches the unreachability from the *clock-start* direction and is a good catch. It does not mention `:1021`, so a reader fixing the clock leaves the eligibility rule in place and still has an unreachable branch. Pass 2 also added a *third* statement in the same family at `:447` — *"Because MVP has no user-triggered discard and `dirty` is never cleanup-eligible, a workspace whose originating task has terminally closed while staged changes remain is **orphaned**"* — which is an excellent catch, correctly names the obligation, and makes the three-way split one statement wider.

**Fix:** fold `:1021` into the `:442` item, or state cleanup eligibility once and cross-reference from `:434` and `:447`.

---

## RG-M8 — MEDIUM — `withheld` and the `confidential` tier still reach no UX or concern statement, and PD8 still has no lockstep list

**Checklist item:** 1 · **Prior ID:** RU-M3, unmoved

Every downstream statement of the console's state vocabulary still models the old split:

- `:310` UX Design Integration Implications — *"Redacted, inaccessible, unknown, missing, unavailable, failed, delayed, dirty, locked, ready, and committed states must be visually and semantically distinct"* — no `withheld`. This is the list Epic 6/8 treat as normative.
- `:114` concern #11 — still *"contact your administrator"*, copy S-6 says is wrong for a withheld value, because no administrator can reveal it.
- `:120` concern #17 — still *"per-tenant policy (hash/truncate/redact/expose)"*, read-side framing with "hash" rather than the keyed write-time substitution S-6 decides.
- `:754` F-5 — redaction affordance only.

And PD10 has an explicit regeneration list (`:666`). PD8 still has none, though `SensitiveMetadataTier` gaining `confidential` and the console vocabulary gaining `withheld` are breaking wire and UI additions — plus, per **RG-H2**, three shipped copy strings that must change with them.

---

## RG-M9 — MEDIUM — The console's live-freshness mechanism is still named four ways and decided none

**Checklist item:** 7 · **Locations:** `:751` (F-2), `:1655`, `:1687`, `:1689` · **Prior ID:** RU-M7, unmoved

F-2 *"Interactive Server render mode (SignalR) … for live status updates"* (a render channel); `:1655` *"reads only from projection endpoints"* (pull); `:1687` *"UI SignalR notification → live status update"* (push, from a hub no decision creates and no I-3/I-4 rule permits for `folders-ui`); `:1689` *"202 Accepted + correlationId for status polling"* (poll). C2 pins a 500 ms freshness target and F-7 a p95 page-load budget, so the mechanism is budget-relevant. This now also interacts with `:674`: if stale authority evidence may return a retryable 503, the console's refresh loop needs a stated backoff, and none of the four statements owns one.

---

## RG-M10 — MEDIUM — Alerting/SLO ownership and the runbook inventory stay partial while DR makes runbooks release-blocking

**Checklist item:** 7 · **Locations:** I-6/I-7, `:272`, `:1789` · **Grep:** `on-call` = **0**

Alerting exists only as Epic-13 scope (*"wire the five declared-only alert instruments"*) and NFR82's mechanism-neutral obligation. No decision names who owns an alert, what an SLO is, or where a burn is routed. Runbooks: `docs/runbooks/` is in the tree, `tenant-deletion.md` is listed under *Minor Gaps*, and `:1689`-region prose names no artifact. The new DR item explicitly requires *"the restore runbook"* — so the runbook set is load-bearing for a release-blocking dimension with no inventory, no ownership, and no gate.

---

## RG-M11 — MEDIUM — A-8 states `visibility` as a required field flatly while S-7 says it is proposed and must not be cited as current contract

**Checklist item:** 8 · **Locations:** `:697` (A-8) against `:657` (S-7)

A-8: *"**`visibility` is a required field on every error (PD10, 2026-09-15);**"* — stated as a decision row, in the present tense. S-7, in the sentence written to fix exactly this: *"PD10's promotion of `visibility` to a **required top-level field on every error** … [is] *proposed*, not yet in the matrix, the spine, or any enum … must not be cited as current contract."*

`:238`'s blanket "PD8, PD10, and PD11 are target state" arguably covers A-8, but S-7's caveat is sharper than that blanket (it distinguishes *approved-target* from *proposed-not-yet-in-the-matrix*) and A-8 is the row a contract implementer reads. One clause.

---

## RG-M12 — MEDIUM — Two escape hatches remain: the release-reason enumeration and the unowned A-11 rollout item

**Checklist items:** 3, 2 · **Locations:** `:657`, `:672` · **Prior ID:** RU-M8 + dimension 24

`:657` still reads *"MVP release reasons permit only approved values **such as** `caller_completed`; reserved post-MVP reasons are rejected."* "Such as" makes the allowed set non-exhaustive inside a clause that then says the complement is rejected, so "rejected" cannot be tested. "Release reason" is also undefined at first use. Enumerate it or point at a closed vocabulary the way the rest of S-7 now does.

`:672` (migration and rollout of the breaking PD10 change) correctly says it is *"an **open item**, not a settled one"* and names the four questions — but alone among the ten open items it names **no route and no rank**. It is the only one whose owner a reader cannot find.

---

## RG-M13 — MEDIUM — Two matrix rows still contradict PD11

**Checklist item:** 8 · **Locations:** `:410`, `:377` · **Prior ID:** RU-M6(a), unmoved

`:410`: `changes_staged → dirty` on `LockLeaseExpired` (mutations applied, lock lost), side effect *"**Operator intervention required**"*. PD11 rule 2 (`:433`) says the originating task resumes via `dirty` + `WorkspaceLocked` → `changes_staged`, and the `dirty` catalog row (`:377`) assigns `degraded-but-serving` *"while the originating task can still resume"*. An implementer reading `:410` sets `awaiting-human` on the exact path PD11 rewrote to be self-recovering — and `:430` already records that `DispositionLabelMapper.cs` gets `AwaitingHuman` wrong today, so the row reinforces the live defect.

---

## RG-L1 — LOW — `C20` still does not exist, and still points away from the fix

`:654` (S-4): *"local tenant-access projection (fail-closed-on-stale per **C8 + C20**)"*. The exit criteria are C0–C13; the intended reference is cross-cutting concern #20. The real target is now the `:674` open item, which is exactly where a reader chasing `C20` should land and cannot.

## RG-L2 — LOW — Five `Octokit 14.0.0` sites survive the amendment's own no-version-numbers rule

`:1746` states the rule: *"this document names technologies, never their version numbers"*, with a stated owner-on-disagreement rule. Pins nonetheless survive at `:605`, `:695`, `:1316`, `:1550`, `:1659`. The pin is **correct** (confirmed against `references/Hexalith.Builds/Props/Directory.Packages.props`), so this is rule-consistency rather than staleness — but `:1550` (`14.0.0/openapi-snapshot.json`) is a Forgejo snapshot path and a likely false positive worth checking before editing.

## RG-L3 — LOW — `:772` states the same dependency twice in one paragraph

*"it also gates the write-concurrency decision above, which only bites above one replica"* and, three sentences later, *"This also blocks the write-concurrency decision above, which only matters above one replica."* Introduced by the pass-2 rank insertion. Delete one — and resolve **RG-C1** while there, since both copies encode the inverted edge.

## RG-L4 — LOW — Cost still unowned; frontmatter still carries three readiness dates

`grep -ci cost` = **9**, every hit the word inside an *Alternatives considered* cell. No cost envelope, no per-tenant cost model, no cost-of-retention statement against D-7's P7Y tier. Defensible at this altitude for a self-hosted container product; recorded so the sweep is complete. Separately: `updated: '2026-09-16'` (`:39`) vs `implementationReadiness: 'not-ready (2026-07-14/15)'` (`:42`) vs *"§Readiness (updated 2026-09-12)"* (`:1839`). The `validationRemediation` key (`:41`) is a good addition and the right place to say readiness is deliberately frozen until the rank-50 rerun.

---

# Prior-finding closure tally

| Prior ID | Severity | Status after pass 2 | Evidence |
| --- | --- | --- | --- |
| **RU-C1** — four routed items carry owners but no rank | CRITICAL | **PARTIALLY CLOSED → new CRITICAL** | All four now carry an explicit rank entry-condition in their own text, with `:643` articulating why. But none reaches the wave table that `:212` calls the only machine-checkable schedule (**RG-H1**), and the two ranks chosen invert the dependency the items declare between themselves (**RG-C1**). Obligation is now *stated and owned*; it is still not *schedulable*. |
| **RU-C2** — completeness checklist ticks two boxes the banner denies | CRITICAL | **CLOSED** | `:1814` is now `[~] Cross-cutting concerns mapped — **12 of 22** concerns carry a structure-mapping row; the remainder are decided in the concern list but unrouted`, and `:1835` is `[~] Requirements to structure mapping — **target routing, not complete**` with the three unrouted mechanisms named. I counted the concerns table: rows for #1, #6, #11, #13, #14, #15, #16, #17, #18, #19, #20, #21 = **12 of 22**. The text is exact. Clean close. |
| **RU-H1** — `withheld` / top-level `visibility` reintroduce invented wire vocabulary | HIGH | **CLOSED in substance; two residual false claims** | S-7 now splits approved-vs-proposed, marks both as A6b material, and says they *"must not be cited as current contract"* — the defect is fixed. Residuals: "zero occurrences anywhere" is false, and S-6's non-collision claim is falsified by its own cited file → **RG-H2**. |
| **RU-H2** — "derived from the matrix" when the matrix has no such columns | HIGH | **CLOSED on the over-claim; NEW CRITICAL on the replacement** | *"owed, not achieved"* is right, and I confirmed the matrix carries no such columns. But the replacement paragraph is false on the schema's closed-axis count, false on the oracle's missing rows, and mis-attributes the divergence to CLI-vs-MCP when it is document-vs-oracle → **RG-C2**. The underlying table gap is unmoved → **RG-H7**. |
| **RU-H3** — triple keying reached 3 of 7 statements | HIGH | **CLOSED (6 of 7), enforcement gap sharpened** | All five named targets landed: `:326`, `:353`, `:386`, `:448`, `:1879`, plus `:366` and `:1102`. Only concern #4 at `:107` survives → **RG-M1**. The Guard-column decision is correct and I verified both pins (`ConsumerDocsConformanceTests.cs:557` = 41 edges; the three-column header string is the table locator). The consequence — the gate keys on `(from, to, event)` and is blind to guards — is now the live issue → **RG-H6**. |
| RU-H4 — C9 pointers name the file S-6 rules out | HIGH | **OPEN** | `:356` edited (Dapr state store added) without repointing → **RG-H5** |
| RU-H5 — S-6 tenant key unrouted | HIGH | **OPEN** | `grep rotat` = 2, both pre-existing → **RG-H3** |
| RU-H6 — lockstep widened the doc↔code delta | HIGH | **OPEN, sharpened** | `:353`'s three-artifact assertion exists nowhere; the edge gate cannot see guards → **RG-H6** |
| RU-M1 — DR names one direction of the restore hazard | MEDIUM | **OPEN, compounded by rank 40** | → **RG-M6** |
| RU-M2 — validation/readiness tail stale | MEDIUM | **PARTIAL (1 of 7)** | `:1774` structure-completeness row fixed exemplarily → **RG-M4** |
| RU-M3 — `withheld`/`confidential` reach no UX statement | MEDIUM | **OPEN** | → **RG-M8** |
| RU-M4 — transcribed counts | MEDIUM | **PARTIAL (1 of 3)** | one `49` removed from S-7 → **RG-M5** |
| RU-M5 — staged-content eligibility | MEDIUM | **OPEN, third statement added** | → **RG-M7** |
| RU-M6 — matrix contradiction + G-gap claim | MEDIUM | **(a) OPEN → RG-M13; (b) ESCALATED** | `:666` now explicitly claims `G4`, against the matrix's owner column → **RG-H4** |
| RU-M7 — console live-freshness named three ways | MEDIUM | **OPEN (four ways)** | → **RG-M9** |
| RU-M8 — release-reason escape hatch | MEDIUM | **OPEN** | → **RG-M12** |
| RU-M9 — alerting/SLO + runbook inventory | MEDIUM | **OPEN** | → **RG-M10** |
| RU-L1 — `C20` | LOW | **OPEN** | → **RG-L1** |
| RU-L2 — Octokit pins | LOW | **OPEN** | → **RG-L2** |
| RU-L3 — cost + frontmatter dates | LOW | **OPEN** | → **RG-L4** |

**Tally: 2 criticals → 1 closed, 1 partially closed with a new critical · 6 highs → 3 closed or substantially closed, 3 open (1 escalated from medium) · 9 mediums → 1 partial, 8 open · 3 lows → all open.**
**This pass: 2 critical / 7 high / 13 medium / 4 low.** Both criticals and two of the highs (**RG-H1**, **RG-H2**) are defects in pass-2's own new text.

---

## What pass 2 got right (so a third pass does not undo it)

1. **The four self-caught pass-1 defects are all genuinely fixed, and I verified three independently.** `FoldersServerServiceCollectionExtensions.cs:131–134` really does `AddEventStoreReadModelStore()` + `RemoveAll` + bind `EventStoreSemanticIndexingBridgeStore`, with no writer on the Server; `:67` really does register `UnavailableWorkspaceCommitExecutor` and `OctokitGitHubApiClient.cs:60` really is the only `NotImplementedException` in `src/`; the C6 header and 41-edge pins really are in `ConsumerDocsConformanceTests`. Catching a stale as-built claim in the *head* of a sentence you edited the *tail* of is the hardest version of this work.
2. **The bridge-arbitration rewrite is the model correction.** Pass 1 declared a sole writer; pass 2 discovered that rule would mean no entry ever reaches `Indexed`, and replaced it with a field split enforced by interface registration. Reversing your own decision on as-built evidence, and stating the replay consequence rather than assuming it away, is exactly right.
3. **Demoting the degraded-mode "reconciliation" back to an open item is a rare and correct move.** I verified the underlying finding: `authorization-matrix.md:76` routes `stale` to `safe-denial-404` while `:146` routes stale authority evidence to `authority-unavailable-503`. The matrix cannot arbitrate, and saying so beats the tidier answer.
4. **`:356`'s C9 scope addition is real architecture.** Naming the Dapr state store explicitly *because* it is the PD8 carve-out's only candidate home — *"a gate that omits it certifies green over the exact hole"* — is the kind of reasoning that makes a measurement method testable rather than decorative.
5. **`:447`'s orphaned-workspace obligation.** Recognising that MVP has no discard and `dirty` is never cleanup-eligible, so a terminally-closed task with staged changes leaves an unescapable state, and naming that as an obligation on the owning story rather than assuming it away — unprompted and correct.
6. **The `[~]` treatment of the completeness checklist.** Two honest partial ticks with the real numbers in them is better than either a lie or an empty box, and the counts reproduce exactly.

## Recommended close-out order (all inside the ratified scope)

1. **RG-C1** — resolve the rank inversion (cheapest: make `:643` conditional on a rank-0 single-replica declaration). One sentence, and it unblocks the item-3 failure.
2. **RG-C2** — three sentence corrections in S-7: thirteen closed axes not two, `cli_exit_code` among them; the oracle *has* the rows; the divergence is document-vs-oracle on exit 72.
3. **RG-H1** — put the four entry conditions in the wave table's "Work and entry conditions" column and say they transfer to the manifest.
4. **RG-H2 / RG-H4** — qualify "zero occurrences", name the `redacted`/"withheld" copy collision in PD8's lockstep list, and narrow `G1–G11` to the eight PD10 actually owns.
5. **RG-H3 / RG-H5** — add the key-management open item; repoint `:356`/`:1641` at a named tokenizer component.
6. **RG-H6** — restate `:353` as owed, naming that the edge gate is structurally guard-blind.
7. **RG-M4** — one sweep of the last 130 lines, using `:1774` as the template.
