# Adversarial Spine Review (re-gate, pass 2) — architecture.md amendment of 2026-09-16

- **Reviewer lens:** adversarial — "construct two units one level down that each obey every stated decision to the letter yet still build incompatibly"
- **Target (read-only):** `_bmad-output/planning-artifacts/architecture.md` at 1907 lines (pass-2 working tree), plus the co-normative `docs/exit-criteria/c6-transition-matrix-mapping.md` (73 lines) and `docs/diagrams/workspace-lifecycle.md`, which `:428` declares co-normative with them
- **Prior gate report:** `reviews/review-adversarial-update-2026-09-16.md` (FAIL — 3 critical / 7 high / 5 medium / 2 low), findings ADVU-1…ADVU-17
- **Units one level down:** stories from `_bmad-output/implementation-artifacts/sprint-status.yaml` and `epics.md`
- **Ratified scope of the run under review:** architecture.md + co-normative docs only
- **Date:** 2026-09-16
- **Verdict:** **FAIL** — 3 critical, 8 high, 5 medium, 2 low. ADVU-3 is **genuinely closed** and is the best fix in either pass. ADVU-1 and ADVU-2 are **not closed**: both were re-landed as a new *encoding rule* rather than as a marking, and the encoding rule is falsified by the very table it governs — it misclassifies two of the four pairs it names, admits a fifth pair it does not name, and shares its one channel with three non-guard annotations. The pass-1 failure shape (fix the paragraph, miss the copies) has evolved into a pass-2 shape: **invent a rule, then do not check it against the data it is supposed to describe.**

## Method and scope discipline

For each attack I name two units that exist in `sprint-status.yaml`, write the implementation each produces from the document text
alone, and check whether the two artifacts can coexist. Every claim about code, fixtures, gates, or sibling artifacts was verified
against the working tree and the file/line is given. Three findings are scored *worse* because the pass-2 text created a new
incompatibility that did not exist in pass 1.

---

## CRITICAL

### RGT-1 — "A pair with one row has a single outcome" makes the two staged-work-preservation guards unguarded, in the same subsection that says they are guarded

**Severity:** critical (silent loss of durable staged content on a lock timer; blind gate)
**Prior finding:** ADVU-1 — **NOT closed. Worse in one respect:** pass 1 had no marking rule, so the four pairs were merely unmarked; pass 2 added a marking rule that positively *contradicts* the guarded status of two of them.

**What pass 2 fixed (verified, credited).** Triple keying now reaches `:326` (C6 exit criterion), `:353` (C6 measurement method),
`:386` (valid-transitions header), `:448` (aggregate-test bullet), `:1102` (CI-gate bullet), `:1103` (PR-review rule), `:1879`
(Implementation Handoff), and the whole of `c6-transition-matrix-mapping.md`. Seven of the eight copies the prior run named are
now triple-keyed. That is real work.

**The one copy left behind.** `:107` — cross-cutting concern **#4**, in the list the document itself introduces as *"These
concerns recur across multiple components and must be designed once, not per-component"* — still reads: "**a total
state-transition matrix where every (state, event) pair has a defined outcome** including reconciliation paths". Every other
statement moved; the one in the "designed once" register did not.

**The new rule, quoted in full (`:386`):**

> The table keeps its three-column shape because the C6 edge-count gate is pinned to that header; **the guard is carried inline
> in the Triggering Event cell, in bold**. A guard-discriminated pair therefore appears as **two rows with the same `from` and
> the same event**, distinguished only by the bolded guard — `changes_staged` + `CommitFailed`, `inaccessible` +
> `ProviderReadinessValidated`, `dirty` + `WorkspaceLocked`, and `dirty` + `LockLeaseBecameStale` are the four. **A pair with
> one row has a single outcome.** A guard branch that appears in no row is rejected and never routed to its sibling:

**Verified against the table it governs (`:389`–`:427`, parsed mechanically).** The table has 37 rows. Grouping by
`(from, event)` yields exactly **three** multi-row pairs:

| `(from, event)` | rows | in the named four? |
| --- | --- | --- |
| `changes_staged` + `CommitFailed` | `:406` → `failed`, `:407` → `dirty` | yes |
| `inaccessible` + `ProviderReadinessValidated` | `:418` → `dirty`, `:419` → `ready` | yes |
| `unknown_provider_outcome` + `ReconciliationCompletedDirty` | `:421` → `committed`, `:422` → `failed` | **no** |

`dirty` + `WorkspaceLocked` is **one row** (`:412`). `dirty` + `LockLeaseBecameStale` is **one row** (`:413`). So the detection
rule the amendment introduced — "a guard-discriminated pair appears as two rows" — is **false for two of the four pairs it names
in the same sentence**, and true for one pair it does not name. Precision 2/3, recall 2/4.

The two it gets wrong are precisely the two staged-work-preservation guards.

**Unit A — Story 4.18 (`4-18-eventstore-backed-workspace-transition-evidence-projection`, `sprint-status.yaml:123`, `backlog`).**
Builds a table-driven transition map by applying `:386`'s decoding rule to the `:389` table, exactly as written. For
`(dirty, LockLeaseBecameStale)` it finds one row, applies *"A pair with one row has a single outcome"*, and emits:

```
(dirty, LockLeaseBecameStale) -> ready      // unconditional; :413 is the only row
(dirty, WorkspaceLocked)      -> changes_staged  // unconditional; :412 is the only row
```

A `dirty` workspace holding staged changes now returns to `ready` when its lease crosses the C7 expired-to-stale boundary.
The staged changes are gone, with no operator signal — the exact outcome `:446` declares forbidden ("**`(dirty-with-staged-changes,
LockLeaseBecameStale)` is rejected:** staged work is never discarded by a lock timer") and rule 4 at `:435` describes only for
the clean case.

**Unit B — Story 4.19 (`4-19-prove-durable-workspace-prepare-and-lock-lifecycle`, `sprint-status.yaml:124`, `backlog`).** Applies
`:366`'s default rule and `:446`'s explicit sentence: the staged branch is unenumerated, therefore rejected.

**Incompatible artifact.** Same event, same state: one unit discards durable user work, the other rejects. Both cite normative
sentences twenty lines apart in the same subsection. No gate arbitrates:
`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs:82`
`EveryUnlistedStateEventPairShouldRejectWithoutChangingState` enumerates `(state, event, null)` triples over the *current* enum,
and `LockLeaseBecameStale` has **zero occurrences in `src/` and `tests/`** (verified) — so the gate is green for both.

**Why the marking is a fudge, stated plainly.** The amendment's justification for not adding a Guard column is accurate as far as
it goes — `ConsumerDocsConformanceTests.ParseArchitectureC6Transitions` (`:819`) locates the table by
`IndexOf("| From → To | Triggering Event | Side Effect |")` and `:557` pins `matrixEdges.Count.ShouldBe(41)`. But the chosen
alternative cannot express the two pairs in question **at all**: their second branch is a *rejection*, and the row shape
`` `from` → `to` `` has no cell for "rejected". Adding such a row would also mint a phantom positive edge and redden the 41-edge
pin and the diagram-equality assertion. So the document adopted a marking convention that is structurally incapable of marking
the two pairs whose mismarking destroys data, and then asserted in the same sentence that all four are marked. That is the
definition of a fudge: the rule is stated, the data does not satisfy it, and nothing checks.

**Tightening (in scope, doc-only).**
1. Fix `:107`.
2. Delete the sentence "A pair with one row has a single outcome" — it is false for two of the four and is the sentence that
   licenses the data loss.
3. Mark the guarded pairs with a mechanism the row shape can carry: a `†` marker plus a legend, or a **Guard branches** list
   immediately under the table enumerating all eight branches (positive *and* rejected) for the four pairs. A rejected branch
   cannot be a table row; it has to be enumerated somewhere the reader of the table sees.
4. Sequence the Guard column with the gate change the document already orders (`:281`: PD11 "needs … the CI gate re-keyed with
   it"), in the owning story's lockstep commit — see RGT-7.

---

### RGT-2 — The detection rule admits a fifth, unnamed guard-discriminated pair, and its one channel is shared with three non-guard annotations; both readings produce a shipped defect

**Severity:** critical (a commit the provider **refused** is recorded as `committed` and replayed to the caller as success; and the guard-branch denominator is again not computable)
**Prior finding:** new for this pass — created entirely by the pass-2 encoding rule.

**Fact 1 — the fifth pair.** `unknown_provider_outcome` + `ReconciliationCompletedDirty` occupies two rows sharing `from` and
event (`:421` → `committed` "(commit confirmed upstream)", `:422` → `failed` "(commit refused upstream)"). By `:386`'s stated
detection rule this pair **is** guard-discriminated. By `:386`'s own enumeration ("…are the four"), by `:446`'s enumeration, by
the C6 mapping document's four-row **Guard Discriminators** table (`c6-…:23`–`:28`), and by that document's Verification-impact
clause ("the **four** PD11 guard discriminators"), it **is not**. Four artifacts say four; the table says five.

**Fact 2 — the bold channel carries non-guards.** Parsed mechanically, eight rows carry bold inside the Triggering Event cell:

| Row | Bolded content | Is it a guard? |
| --- | --- | --- |
| `:406` | `known non-retryable` | yes |
| `:407` | `retryable, no confirmed remote effect` | yes |
| `:412` | `the originating task` | yes — but only **one** of the two conjuncts (see RGT-3) |
| `:413` | `clean` | yes |
| `:415` | `reserved post-MVP; fails closed in MVP code` | **no — MVP status marker** |
| `:417` | `reserved post-MVP; fails closed in MVP code` | **no** |
| `:418` | `within the C3 retention window` | partial — "while staged content survives" is outside the bold |
| `:426` | `reserved post-MVP; fails closed in MVP code` | **no** |

And `:419` — `inaccessible` → `ready`, a *named* guard branch — carries **no bold at all**, while `:366`'s default rule says the
guard is "read from the bolded discriminator in the Triggering Event cell". Under that rule `:419` has no guard, so it is the
unconditional row for `(inaccessible, ProviderReadinessValidated)` and collides with `:418`.

**Unit A — Story 4.21 (`4-21-prove-real-commit-retry-conflict-and-unknown-outcome-reconci`, `sprint-status.yaml:126`).** Owns
commit retry and unknown-outcome reconciliation. Obeys the *enumerations* (`:386`, `:446`, `c6-…:23`–`:28`): four guarded pairs,
and `(unknown_provider_outcome, ReconciliationCompletedDirty)` is not one of them, so it "has a defined outcome" as a pair.
First matching row wins:

```
(unknown_provider_outcome, ReconciliationCompletedDirty) -> committed   // :421 is first
```

A commit the provider **refused** is recorded as `committed`, and `:421`'s own Side Effect column then guarantees the damage is
visible to the caller as success: "Idempotency replay returns success".

**Unit B — Story 4.18.** Obeys the *detection rule* (`:386`): two rows sharing `from` + event ⇒ guard-discriminated. Implements
five guarded pairs, ten branches.

**Incompatible artifact.** (a) Unit A records refused commits as successes; Unit B does not. (b) `:448`'s gate — "for a
guard-discriminated pair every branch needs its own asserted outcome … CI fails if a state, event, **or guard branch** is added
without test coverage" — has denominator 8 under A and 10 under B, so it is not computable. This is the identical defect ADVU-2
and ADVU-9 raised, reintroduced by the pass-2 fix through a different door.

**Third derivation, from the same rule.** A mechanical reader of `:366` ("the guard being read from the bolded discriminator in
the Triggering Event cell") applied to `:415` gets guard = `reserved post-MVP; fails closed in MVP code`, outcome = the From → To
column = `failed`. The guard is *satisfied* in MVP. So the rule as written derives `(dirty, OperatorDiscardRequested,
reserved-post-MVP) → failed` — the exact inverse of rule 6 at `:437` and of the row's own Side Effect column, which says
"MVP: rejected with `state_transition_invalid`". One table row, three mutually exclusive instructions. The same applies at `:417`
and `:426`.

**Tightening.** Either name the fifth pair and fix the four counts in two artifacts, or state that
`(unknown_provider_outcome, ReconciliationCompletedDirty)` is discriminated by *reconciliation evidence* and list it as the fifth.
Move the MVP-reservation markers out of the Triggering Event cell into the Side Effect cell so the bold channel carries guards and
only guards. Bold the whole guard on `:418` and give `:419` one.

---

### RGT-3 — The `stagedByTaskId` predicate is still split — one conjunct in `:446`, in `:412`'s bolded guard, and in the co-normative diagram; two conjuncts in `:447` and the mapping document

**Severity:** critical (a *clean* `dirty` workspace is resurrected into `changes_staged` with nothing staged, and the subsequent commit records an empty change set)
**Prior finding:** ADVU-2 — **half-closed.** The cross-*file* split is genuinely fixed. An intra-file split and a diagram split survive, and they are in the two carriers the amendment just designated as the guard channel.

**What pass 2 fixed (verified, credited).** `:447` is a strong piece of writing: one predicate, both conjuncts, "server-resolved,
not the `X-Hexalith-Task-Id` header", `stagedByPrincipal` demoted to "evidence, not a guard input — no transition reads it", the
honest note that `stagedBy*` has zero occurrences in `src/` (verified: `grep -rn "stagedBy" src/` returns nothing), and the
unsatisfiable clearing rule replaced by an explicit orphaned-workspace obligation. `c6-…:26` now carries the identical two-conjunct
predicate. That is exactly what ADVU-2 asked for, in both artifacts.

**The three carriers that still say one conjunct.**

1. **`architecture.md:446`** — the bullet *immediately above* `:447`, in the same list: "`dirty` + `WorkspaceLocked` (**the
   originating task *vs* any other principal**)". One conjunct, and keyed on *principal* — the dimension `:447` demotes to
   non-guard evidence one line later.
2. **`architecture.md:412`** — the table row. Full cell: ``WorkspaceLocked` by **the originating task**, on a dirty workspace
   that still holds staged changes`. The staged-content conjunct sits **outside the bold**. Under `:366`'s own parsing rule
   ("the guard being read from the bolded discriminator in the Triggering Event cell") the guard for this row is *the originating
   task*, full stop. The document's designated machine-readable guard channel carries the one-conjunct predicate.
3. **`docs/diagrams/workspace-lifecycle.md:97`** — declared co-normative by `:428`: "`dirty`+`WorkspaceLocked` (**the originating
   task only**)".

So the two-conjunct form lives in `:447` and `c6-…:26`; the one-conjunct form lives in `:446`, in `:412`'s bold, and in the
diagram. `:447`'s claim — "**The guard has exactly one predicate, and it is the same one in every artifact**" — is falsified by
the line directly above it.

**Unit A — Story 4.19's aggregate.** Reads the guard channel the amendment designated: `:412`'s bolded discriminator, confirmed by
`:446`'s parenthetical and by the co-normative diagram. Guard = `stagedByTaskId == resolved task`. Fires on **any** `dirty`
workspace.
**Unit B — Story 4.21 / Story 6.3's mapping consumer.** Reads `:447` and `c6-…:26`. Guard = task match **AND** staged content
present.

**Incompatible artifact, with the same concrete exploit path as ADVU-2.** `:402` reaches `dirty` from `locked` via
`LockLeaseExpired` with **no mutations applied** — a *clean* `dirty`. Under Unit A that workspace transitions to `changes_staged`
when the same task re-locks, so the aggregate reports "one or more file mutations applied; commit pending" (`:376`) for a
workspace that has staged nothing, and a following `CommitSucceeded` records a commit of the empty set. Under Unit B the same
request rejects with `state_transition_invalid`. Branch counts differ again (two vs three), so `:448`'s gate is not computable —
for the third time in this review.

**Why `:428`'s one-model rule does not arbitrate.** It reads: "This matrix, `docs/exit-criteria/c6-transition-matrix-mapping.md`,
`docs/diagrams/workspace-lifecycle.md`, `FolderStateTransitions.cs`, and the lifecycle tests express **one** model; a divergence
is **a defect in the code or the test**, never an alternative reading." The escape clause assigns the defect to code or test only.
When the divergence is document-versus-document — matrix prose vs matrix table vs diagram — the invariant names no arbiter at
all. `c6-…:29`'s tiebreaker ("a defect in whichever was edited last") is also inapplicable: all three were edited in the same
change set.

**Tightening (one commit).** Delete the parenthetical in `:446` or restate it as both conjuncts; extend `:412`'s bold to cover
the whole predicate; fix `workspace-lifecycle.md:97`; and extend `:428`'s one-model rule to say which document wins when two
documents diverge.

---

## HIGH

### RGT-4 — S-7's "the matrix wins" precedence rule is undecidable for `stale`, the one input the degraded-mode open item is about, while S-7 still transcribes only the 503 side as normative

**Prior finding:** ADVU-7 — **correctly re-dispositioned as an open item** (credited), with a residue that is a live divergence generator.

**Credited.** Downgrading the "reconciliation" to an open item was the right call and the finding is verified exactly as stated:
`docs/contract/authorization-matrix.md:76` routes the `stale` negative access state to `safe-denial-404`, and `:146` routes
"trusted tenant, membership, or delegation evidence is stale or unavailable" to `authority-unavailable-503`. The approved matrix
genuinely contradicts itself. Concern #20 was amended **in place** at `:123` and honestly marked CONTESTED. Both are model
dispositions.

**The residue.** S-7 (`:657`) still carries, unqualified, the precedence rule **"on any disagreement the matrix wins and this row
is the defect"** and then transcribes outcome (3) as normative for stale evidence. Applied to input `stale`, "the matrix wins"
returns two answers.

**Unit A — the Epic 5 / Story 13.2 server pipeline (`13-2-fail-safe-fallback-authorization-policy-and-sidecar-only-app`,
`sprint-status.yaml:243`).** Obeys S-7's precedence rule literally: goes to the matrix, finds the negative-access-state table,
reads `stale` → `safe-denial-404`. Emits 404, `tenant_access_denied`, `retryable: false`, `no_action`.
**Unit B — the PD10 spine regeneration.** Obeys S-7's transcription: 503, `read_model_unavailable`, `retryable: true`, `retry`.

**Incompatible artifact.** During a Tenants outage every folder looks **deleted** to Unit A's clients — a non-retryable 404 with
`no_action` is an instruction to stop asking and, for any client that mirrors server state, to drop the local copy. Unit B's
clients back off and retry. Both obeyed S-7.

**Tightening.** Add one clause to S-7: "for the `stale` input the matrix is self-contradictory (`:76` vs `:146`) and the
precedence rule does not apply; see the degraded-mode open item — no unit may implement stale routing until it closes." Then give
that open item a rank (RGT-6).

### RGT-5 — S-7's honesty note about the parity oracle is factually wrong and self-contradicting, so its own remediation trigger is already satisfied while the exit-code collision is live

**Prior finding:** ADVU-5 — **half-closed, with a new false statement.**

**Credited.** S-7 now records the exit-72 collision explicitly and states that "CLI and MCP **can** disagree about an authority
outage", and it drops the pass-1 claim that the columns are "derived from the matrix". It also correctly de-scopes vocabulary
ownership: "`parity-contract.schema.json` closes exactly two axes". All verified.

**The false statement.** S-7 says: "Today the oracle and this document's own canonical table have **no row** for
`authentication_failure`, `read_model_unavailable`, or `projection_unavailable`; the shipped `parity-contract.yaml` maps them to
exit **65** and **72**". The second half contradicts the first, and the first is false — verified:

- `tests/fixtures/parity-contract.yaml:56`–`:59` — `canonical_error_category: 'authentication_failure'`, `cli_exit_code: 65`, `mcp_failure_kind: 'authentication_failure'`
- `tests/fixtures/parity-contract.yaml:604`–`:606` — `canonical_error_category: 'read_model_unavailable'`, `cli_exit_code: 72`
- `tests/fixtures/parity-contract.yaml:596`–`:598` — `canonical_error_category: 'projection_unavailable'`, `cli_exit_code: 72`

All three rows exist. What does **not** exist is a row in *this document's* canonical CLI table (`:721`–`:735`). So S-7's stated
remediation trigger — "**Until those rows exist**, CLI and MCP can disagree" — is already satisfied, today, while the defect is
fully live.

**Unit A — the Epic 5 CLI adapter story.** Consumes the oracle as instructed ("Assert against the oracle file, not this prose",
`:737`), verifies that rows exist for all three categories, concludes S-7's condition is met, and ships. An authority outage
exits **72**, which `:730` defines as `reconciliation_required` — "*not retryable until cleared*".
**Unit B — the Epic 5 MCP adapter story.** Emits `kind: read_model_unavailable`, `retryable: true`, `clientAction: retry`.

**Incompatible artifact.** `:741`'s cross-adapter invariant fails on every authority-unavailable response, and exit 72 now carries
**four** categories — `reconciliation_required`, `read_model_unavailable`, `projection_unavailable`, `file_policy_unavailable`
(verified) — inside an oracle the same paragraph forbids from collapsing categories. Exit **65** likewise carries both
`credential_missing` (`:723`) and `authentication_failure` (oracle `:56`), so the CLI cannot distinguish "I have no credentials"
from "the server rejected my token".

**Tightening.** Replace the false sentence with what is true: the oracle rows exist and are *colliding*. Re-state the trigger as
"until `read_model_unavailable` and `authentication_failure` hold exit codes disjoint from `reconciliation_required` and
`credential_missing`". The `cli_exit_code` enum in the schema is a closed 15-value set `[0,1,64…76]`, so this needs two new values
or an explicit accepted-collision statement.

### RGT-6 — `:643`'s new rank rule demotes seven of the document's own ten open items to non-gating, including degraded mode and deny-by-default

**Prior finding:** new for this pass — created by pass-2 text.

`:643` (new, and correct in itself) states: "**An open item with no rank cannot gate anything under the §7.1 rule**, so this one
is stated as a rank-10 entry condition rather than a parallel question."

Verified: `grep "^\*\*Open — "` returns **ten** items (`:216`, `:442`, `:643`, `:645`, `:660`, `:674`, `:682`, `:684`, `:772`,
`:774`). Three carry an explicit rank entry condition (`:643`, `:645`, `:774`). **Seven do not** — including degraded mode
(`:674`) and the deny-by-default HTTP binding (`:684`), both of which the document elsewhere says must be settled before their
consumers build.

**Unit A — Story 13.2 (`sprint-status.yaml:243`, `backlog`).** Owns `NFR76` deny-by-default. `:684` warns: "Story 13.2 owns
`NFR76` deny-by-default, while the PD10 spine correction that defines the denial shape is unowned — if 13.2 lands first it will
build a second deny-by-default shape beside S-7." But `:684` has no rank, and `:643` says an unranked open item gates nothing, so
13.2 is entitled to proceed. It builds a fallback policy with its own denial shape.
**Unit B — the PD10 spine regeneration** (which `:278` records as having **no owning story**) later builds S-7's shape.

**Incompatible artifact.** Two deny-by-default shapes on one HTTP surface, and the document's own warning is the thing it made
non-binding. The same structure applies to `:674`: Phase 4 (`:791`) still delivers "Tenants-availability degraded-mode wiring",
I-7 (`:768`) still monitors a degraded-mode flag, and `:1644` still routes concern #20 to `TenantAccessAuthorizer.cs` — all
unqualified, all licensed by an unranked open item.

**Tightening.** Give each open item a rank or an explicit "gates nothing" label. The three that already have ranks show the right
pattern; the inconsistency is what creates the licence.

### RGT-7 — The guard marking is deferred to preserve a gate the same document orders replaced, and the channel it chose is the cell that gate parses as the event vocabulary

**Prior finding:** new for this pass.

**The circularity.** `:386` declines a Guard column "because the C6 edge-count gate is pinned to that header". Verified:
`ConsumerDocsConformanceTests.cs:819` locates the table by `IndexOf("| From → To | Triggering Event | Side Effect |")` and
`:557` pins 41 edges. But `:281` says PD11 "needs `FolderStateTransitions.cs` re-keyed to `(state, event, guard)`, **the CI gate
re-keyed with it**", `:448` says the gate is keyed on triples, and `:1102`/`:1103` restate it. The document therefore declines a
documentation change in order to protect a gate it simultaneously orders replaced, and the gate is in the same lockstep commit
the owning story must make anyway.

**The channel collision, verified in the parser.** `ArchitectureTransitionRow()` (`:1125`–`:1126`) captures the Triggering Event cell as
`[^|]+`, and `ParseArchitectureC6Transitions` (`:829`–`:836`) then harvests **every backticked token beginning with an uppercase
letter** from that cell and emits it as a transition event. So the amendment has designated, as the guard-carrying channel, the
exact cell the gate parses as the event vocabulary.

**Unit A — Story 4.19's documentation lockstep.** Obeys `:447` ("the durable fields the guards read are declared here") and
`:386` ("the guard is carried inline in the Triggering Event cell") and renders `:412`'s guard precisely, naming the field it
reads: ``WorkspaceLocked` by the task in **`StagedByTaskId`**, on a workspace that still holds staged changes`. This is the
maximally compliant rendering of both rules.
**Unit B — CI.** `ParseArchitectureC6Transitions` mints an event named `StagedByTaskId`, `matrixEdges.Count` becomes 42,
`WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix` fails on the count assertion, and
`WorkspaceLifecycleDiagramEventLabelsEqualC6EventVocabulary` (24-event pin, `:542`) fails next.

**Incompatible artifact.** The most precise possible compliance with the new encoding rule reddens two gates. The rule is
therefore only satisfiable while guards are written in imprecise prose — which is what produced RGT-3.

**Tightening.** Add the Guard column and re-key the parser in the same commit, as `:281` already requires; or, if the marking must
stay inline, state explicitly that a guard is written in **plain bold text with no backticks** and say why.

### RGT-8 — `:446`'s `FolderWorkspaceDirtyResolution` guard mechanism and `:447`'s "never from caller input" are now in direct contradiction

**Prior finding:** ADVU-11 — **untouched, and sharpened by the new `:447`.**

`:446` (unchanged): "implements this matrix as a switch expression over **`(currentState, eventType, resolution)`** … where
`resolution` is the `FolderWorkspaceDirtyResolution` discriminator".
`:447` (new): guards are read from durable aggregate fields and are "**never from caller input (S-3, S-8)**".

**Verified.** `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:47-51` —
`Transition(FolderWorkspaceLifecycleState? currentState, FolderWorkspaceLifecycleEvent attemptedEvent,
FolderWorkspaceDirtyResolution? dirtyResolution = null)` is a pure static function with **no aggregate-state parameter**.
`FolderWorkspaceDirtyResolution.cs` has exactly two members (`CommitConfirmed`, `CommitRejected`), each carrying
`[JsonStringEnumMemberName]` under a `JsonStringEnumConverter` — **it is a serialized wire type**.

**Unit A — Story 4.19** keeps the prescribed signature and extends `FolderWorkspaceDirtyResolution` to carry the new guard
branches. The guard is now a parameter the caller of `Transition` supplies, and it is on the wire: `:447`'s "never from caller
input" is violated by obeying `:446`, and the wire change has no declared regeneration duty (contrast `:438`, which declares one
for `LockLeaseBecameStale` and nothing else).
**Unit B — Story 4.21** changes the signature to take the workspace state and introduces a domain-internal guard union, leaving
the two-member wire enum alone.

**Incompatible artifact.** Two incompatible signatures for the aggregate's central function, two wire surfaces, and — a fourth
time — two guard-branch denominators for `:448`'s gate.

**Tightening.** Delete `resolution` from `:446` or name a domain-internal closed union explicitly marked "not a wire type", state
that `Transition` takes the workspace state, and say which pairs accept which guard family.

### RGT-9 — Hard-coded denominators: one of the three was fixed, two survive, and the surviving count is wrong

**Prior finding:** ADVU-13 / F14 — **partially fixed; the fix note overstates it.**

- **Fixed (credited):** S-7's "All 49 protected Contract Spine operations" is gone; `:657` now reads "Every protected operation in
  the current generated Contract Spine inventory … never a number transcribed here".
- **Survives:** `:684` — "**The 49 protected operations** are protected because each one opts in." This is the instance the
  prior run flagged as *added by pass 1*; it was not removed.
- **Survives and is wrong:** `:737` — "the full `CanonicalErrorCategory` enum (**43** post-SDK members)". Verified actual:
  **50** members in `parity-contract.schema.json` `$defs/canonical_error_category`, **46** distinct categories in
  `parity-contract.yaml`. The same sentence ends "never hard-coded counts (2026-07-15)".
- **Survives and is false:** `:737`'s "The `kind` set is identical to the canonical category set (one-to-one mapping)".
  Verified: `canonical_error_category` has 50 members, `mcp_failure_kind` has 49; `client_configuration_error`,
  `credential_reference_missing`, `success` are categories with no kind; `none`, `usage_error` are kinds with no category.
- `:243` transcribes "403 on 49 of 49, 404 on 46 of 49" in the same clause that says "never a number transcribed here".

`tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs:148` pins `spine.Length.ShouldBe(49)`, and
PD10 adds operations, so the first unit to add the incident-evidence operation reddens a conformance pin and leaves the
architecture the drifting artifact. **Tightening:** delete the numerals at `:684`, `:737`, and `:243`; correct or delete the
one-to-one claim.

### RGT-10 — The D-9 413 retry-header collision is untouched for a second consecutive pass and still unrouted

**Prior finding:** ADVU-8 — **untouched.**

Verified unchanged in the pass-2 tree:

- `:640` (D-9): "the 256KB boundary enforced server-side via `413 Payload Too Large` plus a **`x-hexalith-retry-as: stream`** response header."
- `:919`: "`X-Hexalith-Retry-Transport` | response | … distinct from the request-side `X-Hexalith-Retry-As`"; `:920` reserves `X-Hexalith-Retry-As` for the request side with values `[caller, operator]`.
- Shipped code uses the other name: `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3053` emits `X-Hexalith-Retry-Transport`; `src/Hexalith.Folders.Client/Convenience/FoldersFileUploadExtensions.cs:171` reads it; `FileUploadStreamingRequiredException.cs:10` documents it.

**Unit A — an Epic 5 file-mutation endpoint story** built from D-9 emits `x-hexalith-retry-as: stream`.
**Unit B — the D-9 SDK convenience helper** looks for `X-Hexalith-Retry-Transport`, finds nothing, and the "unimodal DX" fallback
never fires; the over-boundary upload surfaces as a hard 413. If A wins instead, one header name carries `[caller, operator]` on
the request and `stream` on the response — the exact echo conflict `:919` exists to prevent.

This is a **one-token edit inside D-9**, inside the ratified scope, in a decision table this pass rewrote elsewhere. It is
neither fixed nor in the ten-item open register.

### RGT-11 — Concern #5 still classes `timeout` as a *known* failure while `:407` routes network failures to `dirty`; the "shared classifier" sentence now appears in three places and still publishes no classification

**Prior finding:** ADVU-10 — **untouched; the owner sentence was propagated a third time instead.**

- `:108` (concern #5): "distinguish **known failure (timeout / 401 / 403 / 404 / 409 / 429 / 5xx / …)** from unknown outcome" — verified unchanged.
- `:406`: `changes_staged` → `failed` on `CommitFailed` "(**known non-retryable**…)".
- `:407`: `changes_staged` → `dirty` on `CommitFailed` "(**retryable, no confirmed remote effect**: transient 5xx, throttling, network)".
- `:446` and `c6-…:20` both now say the classification "is owned by the shared provider-outcome classifier … so the GitHub and Forgejo adapters cannot disagree" — naming *who decides*, never *what the decision is*.

**Unit A — the GitHub adapter's classifier contribution** reads concern #5 — the only list in either document that enumerates
failure classes — and classes a commit `timeout` as a known failure → `failed`, staged work terminal.
**Unit B — the Forgejo adapter's contribution** reads `:407` and classes the same timeout as network / no-confirmed-remote-effect
→ `dirty`, staged work preserved. Both sit inside the single shared classifier, so "the adapters must not disagree" is satisfied
by construction while the classifier itself has two compliant readings. A timeout is the canonical case of *no confirmed remote
effect*.

**Tightening.** Publish the mapping the owner sentence implies: provider signal → `{known non-retryable, retryable-no-confirmed-
remote-effect, unknown outcome}`, with `timeout` assigned explicitly, and reconcile concern #5's list against it.

---

## MEDIUM

### RGT-12 — `:447`'s orphaned-workspace obligation is assigned to an owner the same document says does not exist

**Prior finding:** ADVU-2 split #2 — the unsatisfiable *rule* is gone (credited); the *escape* is now an unaddressed obligation.

`:447`: "**a workspace whose originating task has terminally closed while staged changes remain is orphaned**, and the owning
story must provide the platform-owned path that clears it." Verified against the document: `:1021` still declares `dirty` "never
cleanup-eligible", `:415` keeps `OperatorDiscardRequested` "reserved post-MVP; fails closed in MVP code", and `:278` states —
"verified again 2026-09-16, zero occurrences" — that **none of PD8, PD10, or PD11 has an owning story** in `epics.md`.

**Unit A — Story 4.19** (which does exist, `sprint-status.yaml:124`) implements the guard and the two durable fields exactly as
`:447` specifies, and no clearing path, because the clearing path belongs to "the owning story".
**Unit B — the PD11 story** does not exist, so nothing supplies the path.

**Incompatible artifact.** The shipped system carries the guard without the escape, so the first orphaned workspace is
permanently unlockable — the outcome `:447` says the guard exists to remove. The same shape appears at `:680` ("the owning story
must state whether re-egress is triggered automatically on replay completion"), which at least routes to a story that exists
(12.5, `sprint-status.yaml:237`). **Tightening:** name the story, or state that 4.19 may not ship the guard until the clearing
path lands with it.

### RGT-13 — The `unknown_provider_outcome` disposition divergence is recorded at three of five sites, up from two

**Prior finding:** ADVU-12 — **improved, still under-scoped.**

`:430` now names the mapping document, the diagram, and `FolderStateTransitions.cs` (citing `:157`, the `Dirty` case). Two sites
still carry `awaiting-human` unnamed, both behind conformance gates that will redden when the owning story moves the other three:

1. `tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs:197` —
   `[InlineData(FolderWorkspaceLifecycleState.UnknownProviderOutcome, FolderOperatorDisposition.AwaitingHuman)]`.
2. `docs/ux/ops-console-wireflows.md:257` — pins `unknown_provider_outcome` to "disposition `awaiting-human` / Warning", under
   the `mutation_rules` lockstep and the `OpsConsoleWireflowNotesTests` gate.

`:430` also conflates two different divergences in one sentence — the `unknown_provider_outcome` disposition and the separate
`Dirty` → `AwaitingHuman` mapping — and never names the code site for the former (`FolderStateTransitions.cs:161`,
`UnknownProviderOutcome => AwaitingHuman`, verified). Note also that
`ConsumerDocsConformanceTests.WorkspaceLifecycleDiagramDispositionTableMatchesC6StateCatalog` reads the **mapping document**, not
architecture.md, as the C6 state catalog — so the architecture's `auto-recovering` is the one value no gate protects.

### RGT-14 — The table titled "Valid transitions" contains three transitions that must never occur, counted as 3 of the 41 positive edges and drawn in the co-normative diagram

`:415`, `:417`, and `:426` declare `dirty` → `failed`, `failed` → `ready`, and `reconciliation_required` → `failed` in the
From → To column while their Side Effect column says "MVP: rejected with `state_transition_invalid`; state unchanged". Verified:
`ParseArchitectureC6Transitions` counts them among the 41 "positive transition edges", and `docs/diagrams/workspace-lifecycle.md`
draws all three as real edges (`:74`, `:76`, `:85`) because
`WorkspaceLifecycleDiagramEdgesEqualArchitectureC6Matrix` requires exact set equality.

**Unit A** implements the From → To column (the transitions exist). **Unit B** implements the Side Effect column (they reject, per
rule 6 at `:437`). Both green under the edge gate. `:437` honestly records that the code accepts all three today and that the
owning story must flip them — but the table and the diagram will still *declare* them after that flip. **Tightening:** move the
three rows into a separate "Reserved post-MVP (must reject)" table and adjust the 41 pin and the diagram in the same commit.

### RGT-15 — The C6 mapping document now carries approval-pending PD11 content under a front matter that still says approved, last reviewed 2026-05-11

`c6-transition-matrix-mapping.md` front matter is unchanged: `status: approved architecture mapping plan`, `last reviewed:
2026-05-11`. The body added this pass carries "Guard Discriminators (PD11 — **approval-pending under A7b**)" and three decision
rows whose Approval state now reads "approved; guard … approval-pending under A7b" while their **Review date column still reads
2026-05-11**. A reader checking freshness at the document level sees an artifact approved four months before the content it
contains. `ExitCriteriaDecisionArtifactTests` checks provenance/approval/consumer/review-date columns for the C3 and C4
artifacts (`:200`+) but only asserts that the C6 artifact mentions `FolderStateTransitions.cs`, so no gate catches this.

### RGT-16 — ADVU-14 and F17 residues

- **ADVU-14 untouched.** `tests/…/FolderStateTransitionsTests.cs:140` `StateCatalogAndEventVocabularyShouldMatchC6MappingDocument`
  still reads no document — verified, it compares `FolderStateTransitions.StateCatalog` against hard-coded literals. The
  architecture still does not record that the gate does not do what its name says, and `:438` (which is otherwise a model
  disclosure) covers only the `LockLeaseBecameStale` case.
- **F17 improved to 3 of 4 sites.** `:183` is now an excellent, verified closure note and `:227` correctly narrows 10.9. But
  `:294`, `:1331`, and `:1628` still credit "**live search/status proof (Stories 10.7–10.9)**" — the framing `:227` retires.

---

## LOW

### RGT-17 — The ten open items are still unregistered prose, and are now inconsistent with each other

Ten `**Open —` paragraphs, none with an ID, owner field, or row in the `:218`–`:225` wave table that `:212` calls "the only
machine-checkable copy of the schedule". Three now carry rank entry conditions and seven do not, which is what makes RGT-6
exploitable. The document already has an ID scheme for exactly this (`OQ1`–`OQ13`).

### RGT-18 — F18 unchanged

`:657` still reads "MVP release reasons permit only approved values **such as** `caller_completed`" — no field named, no closed
set, an open-ended enumeration inside the row whose subject is closed vocabularies.

---

## Disposition of the three criticals this pass targeted

| Prior critical | Status | Evidence |
| --- | --- | --- |
| **ADVU-1** — pair-keyed residuals + a default rule referencing a non-existent marking | **NOT closed.** Triple keying propagated to 7 of 8 copies (`:107` missed). The marking now "exists" as a decoding rule that its own table falsifies for 2 of 4 named pairs, admits a 5th, and shares its channel with 3 non-guards. | RGT-1, RGT-2 |
| **ADVU-2** — split predicate + unsatisfiable clearing rule | **Half-closed.** Cross-file split genuinely fixed and the clearing rule genuinely replaced (both credited). One-conjunct form survives in `:446`, in `:412`'s bolded guard, and in the co-normative diagram; the replacement obligation has no owner. | RGT-3, RGT-12 |
| **ADVU-3** — single-writer bridge rule contradicting as-built | **CLOSED.** `:676`–`:681` is a correct field split, enforced by the interface, verified against `ISemanticIndexingBridgeWriter`, `SemanticIndexingBridgeProjection`, and `FoldersServerServiceCollectionExtensions` (Server registers no writer — confirmed in `:183`). The replay consequence is stated rather than assumed, and routed. This is the best fix in either pass. | — |

**Prior F-list deltas:** F14 partially fixed (`:657` cleaned; `:684` and `:737` survive — RGT-9). F15 **fixed**: S-4's evaluation
order at `:654` now reads authentication → authority-unavailability → authority → lookup, which agrees with S-7 (1) and with the
gate-pinned six-layer order in `docs/diagrams/auth-acl-decision-flow` (`AuthAclDecisionFlowEncodesFixedSixLayerDenyByDefaultOrder`).

## New divergences pass 2 created

1. **RGT-1 / RGT-2** — the "two rows / bolded guard / a pair with one row has a single outcome" encoding rule. Did not exist in
   pass 1. It is the single largest new defect in this review, and it licenses staged-work destruction on a lock timer and
   refused-commits-recorded-as-committed.
2. **RGT-6** — `:643`'s "an open item with no rank cannot gate anything" demotes seven of the document's own ten open items.
3. **RGT-7** — the guard channel chosen is the cell the C6 parser harvests as the event vocabulary, so precise compliance reddens
   two gates.
4. **RGT-5** — S-7's new honesty note makes a false factual claim about the parity oracle and states a remediation trigger that
   is already satisfied.

## What this pass did well

- **The bridge field split (`:676`–`:681`)** is the right shape: split by field, enforced by the interface, with the replay
  consequence stated as a real availability property and routed to 12.5 rather than assumed away.
- **`:183`** turns a stale limitation into a verified closure note with the composition evidence, and `:227` narrows 10.9 honestly.
- **Concern #20 amended in place** (`:123`), marked CONTESTED, with the shipped `AuthorizeDiagnosticReadAsync(allowBoundedStale:
  true)` named as the third answer. Downgrading the "reconciliation" to an open item because the *approved* artifact contradicts
  itself is the correct call and takes discipline to make.
- **`:447`** — declaring the durable fields, demoting `stagedByPrincipal`, ruling out the header, and recording zero occurrences
  in `src/` is exactly the right disposition for a code-blocked item.
- **`:216` and `:643`** — the rank rule and the write-concurrency entry condition are genuinely good structural finds.

## Recommendation

Do not accept as mechanism authority. Three doc-only edits inside this run's ratified scope close the two open criticals:

1. Delete "A pair with one row has a single outcome"; enumerate all eight guard branches (positive **and** rejected) under the
   table; name or exclude `(unknown_provider_outcome, ReconciliationCompletedDirty)`; move the three "reserved post-MVP" markers
   out of the Triggering Event cell; bold the full guard on `:418` and give `:419` one; fix `:107`.
2. Make `:446`, `:412`'s bold, and `workspace-lifecycle.md:97` carry the same two-conjunct predicate as `:447` and `c6-…:26`, and
   extend `:428`'s one-model rule to arbitrate document-versus-document divergence.
3. Rank the seven unranked open items, or label them explicitly non-gating.

The structural note from the last pass still applies and needs one addition. Pass 1's failure was *not searching for the other
copies of a token you changed*. Pass 2's failure is **stating a rule about an artifact without running the rule against the
artifact**. Both are mechanical to prevent: for every decision token changed, grep it across `architecture.md`,
`docs/exit-criteria/`, `docs/contract/`, `docs/diagrams/`, `docs/ux/`, `tests/fixtures/`, and `epics.md`; and for every rule
written *about* a table, list, or enum, enumerate that table and paste the result into the change note. RGT-1, RGT-2, RGT-3,
RGT-5, and RGT-9 would all have been caught by the second step alone.
