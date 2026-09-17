# Adversarial Spine Review (update pass) — architecture.md amendment of 2026-09-16

- **Reviewer lens:** adversarial — "construct two units one level down that each obey every stated decision to the letter yet still build incompatibly"
- **Target (read-only):** `_bmad-output/planning-artifacts/architecture.md` at 1902 lines (amended 2026-09-16, working tree), plus the co-normative `docs/exit-criteria/c6-transition-matrix-mapping.md` amended in the same change set
- **Prior gate report:** `reviews/review-adversarial-2026-09-16.md` (FAIL — 3 critical / 5 high / 4 medium), findings ADV-1…ADV-12 and the F1–F18 prior-run table
- **Units one level down:** epics/stories from `_bmad-output/planning-artifacts/epics.md` and the live inventory in `_bmad-output/implementation-artifacts/sprint-status.yaml`
- **Ratified scope of the run under review:** architecture.md + co-normative docs only; no production code, no `epics.md` edits
- **Date:** 2026-09-16
- **Verdict:** **FAIL** — 3 critical, 7 high, 5 medium, 2 low. Real progress (ADV-3 genuinely closed; ADV-4's vocabulary now exists; ADV-2's missing durable fields now declared), but the dominant failure shape from the previous pass **repeated**: the fix landed in one paragraph and the contradiction survives elsewhere — this time including in the *same table* the fix describes, and in the co-normative artifact the same commit edited.

## Method and scope discipline

For each attack I name two units that exist in `epics.md` / `sprint-status.yaml`, write the implementation each one produces
from the document text alone, and check whether the two artifacts can coexist. Every claim about code, fixtures, or sibling
artifacts was verified against the working tree and the file/line is given.

I judged dispositions against the ratified scope. A finding that can only be closed by shipping code or by authoring a story
is scored as *correctly dispositioned* when it is recorded as an owned open item — but I say so where the recording is
incomplete, mislocated, or names fewer artifacts than the fix actually touches. Three findings below are scored as *worse*
because the amendment introduced new normative text that creates a new incompatibility.

---

## CRITICAL

### ADVU-1 — The `(state, event, guard)` keying now exists in four places and is contradicted in five, including the transition table's own header and the C6 criterion's measurement method

**Severity:** critical (unchanged blast radius from ADV-1: silent loss of durable staged content, with a blind gate)
**Prior finding:** ADV-1 / F3 — **half-closed**

**What the amendment fixed (verified).** `:365` (matrix preamble), `:1097` (CI-gate bullet), `:1099` (PR-review rule), and
`c6-transition-matrix-mapping.md:14`/`:16`/`:55`/`:56`/`:59`/`:69` are now triple-keyed, and `:365` adds the default rule
ADV-1 asked for verbatim: an unenumerated guard branch is *rejected*, never routed to its sibling. The mapping document
gained a four-row **Guard Discriminators** table. That is four of the five copies the prior run named, plus the co-normative
artifact. Good work.

**What is still pair-keyed (verified by grep over the current file).**

- `:107` — cross-cutting concern **#4**, in the list the document itself introduces as *"These concerns recur across multiple
  components and must be designed once, not per-component"*: "**a total state-transition matrix where every (state, event)
  pair has a defined outcome** including reconciliation paths is part of the architecture, not just the state set".
- `:325` — the **C6 exit-criterion definition**: "Total workspace state-transition matrix (every (state, event) pair →
  outcome…)".
- `:352` — the **C6 measurement method** in the Exit Criteria Operations Plan, i.e. the rule that decides whether C6 is met:
  "aggregate test asserts **every (state, event) has a defined outcome**".
- `:385` — the header of the transition table itself: "**Valid transitions (every `(from, event) → (to, side effect)`
  declared)**".
- `:1874` — Implementation Handoff / AI Agent Guidelines: "unlisted **(state, event)** pairs MUST reject with
  `state_transition_invalid`".

**The self-refuting sentence.** `:365` conditions its new default rule on a marking that does not exist: *"For a pair **this
matrix marks guard-discriminated**, a guard branch that is not enumerated is rejected…"*. The table at `:387`–`:425` has three
columns — From → To, Triggering Event, Side Effect — and **no guard column and no guard marking**. Two of the four
guard-discriminated pairs (`dirty`+`WorkspaceLocked` at `:411`, `dirty`+`LockLeaseBecameStale` at `:412`) appear as a single
unremarkable row indistinguishable from the 20 unguarded rows. The only enumeration of the four pairs is a prose bullet at
`:445`, 60 lines below the table, and the mapping document's new table. An implementer building a table-driven transition map
from `:385` cannot determine which pairs the rule at `:365` applies to.

**Unit A — Epic 4 Story 4.19 (`4-19-prove-durable-workspace-prepare-and-lock-lifecycle`, `sprint-status.yaml:124`, `backlog`).**
Builds from the artifacts an implementer actually reads for acceptance: the table header at `:385`, the C6 criterion at
`:325`, and the measurement method at `:352` — all three pair-keyed, all three normative, and `:352` is the one that decides
whether the criterion passes. Produces a total `(from, event) → to` map, first matching row wins:

```
(changes_staged, CommitFailed)              -> failed   // :405 is the first CommitFailed row
(inaccessible,  ProviderReadinessValidated) -> dirty    // :417 is the first row
```

Every retryable commit failure now reaches `failed` and staged changes are declared terminal. The live gate —
`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs:82`
`EveryUnlistedStateEventPairShouldRejectWithoutChangingState` — enumerates `(state, event, null)` and passes.

**Unit B — Epic 4 Story 4.21 (`4-21-…`, `sprint-status.yaml:126`).** Builds from `:365` and `:447` and asserts both branches
of each guarded pair, including `(changes_staged, CommitFailed, retryable-no-confirmed-remote-effect) -> dirty`.

**Incompatible artifact.** Unchanged from ADV-1: A and B cannot both be green, A silently discards uncommitted user work on
every transient provider 5xx, and there is still no arbitration rule — the document now says both things, and the pair-keyed
statement sits in the criterion's own measurement method, which is the text a gate author reads.

**Tightening (one commit, in scope).** Restate `:107`, `:325`, `:352`, `:385`, and `:1874` in the triple-keyed form already
drafted at `:365`. Add a **Guard** column to the `:387` table (or a `†` marker with a legend) so "this matrix marks
guard-discriminated" becomes true; without it the new default rule has no trigger.

---

### ADVU-2 — The newly declared `stagedByTaskId` guard is stated with two *different predicates* in the two artifacts this commit edited, and the new clearing rule is unsatisfiable in MVP

**Severity:** critical (one reading resurrects `changes_staged` on a workspace with nothing staged; the other re-creates the permanently-unlockable workspace the amendment says it removed)
**Prior finding:** ADV-2 / F11 — **half-closed, with a new split introduced by the fix**

**Progress, credited.** `:446` is new and answers the substance of ADV-2: `stagedByTaskId` and `stagedByPrincipal` are declared
durable on the workspace aggregate, set at the first `FileMutated`, surviving the lock instance, and the guard is explicitly
**not** the `X-Hexalith-Task-Id` header. `:446` also states honestly that `stagedBy*` has zero occurrences in `src/` today —
verified: `grep -rn "stagedBy" src/` returns nothing. That is exactly the right disposition for a code-blocked item.

**Split #1 — two predicates for one guard, in two co-normative artifacts amended in the same change set.**

- `architecture.md:411`: "`dirty` → `changes_staged` | `WorkspaceLocked` by **the originating task, on a dirty workspace that
  still holds staged changes**". Two conjuncts.
- `c6-transition-matrix-mapping.md:26` (new Guard Discriminators table): Guard = "**The originating task *vs* any other
  principal**"; Durable field read = "**`stagedByTaskId`** — never the `X-Hexalith-Task-Id` request header". One conjunct. The
  staged-content conjunct is absent, and this is the row an implementer reads *because the mapping table is the artifact that
  enumerates the guards*.

**Unit A — the Epic 5 workspace-lock REST endpoint (`epics.md` Epic 5 workspace group; `sprint-status.yaml:129` `epic-5:
in-progress`)**, built from the mapping table's guard row. Guard = `stagedByTaskId == resolved task`. Fires on any `dirty`
workspace.
**Unit B — Story 4.19's aggregate**, built from `:411`. Guard = task match **AND** staged content present.

**Incompatible artifact, with a concrete exploit path.** `:401` reaches `dirty` from `locked` via `LockLeaseExpired` with **no
mutations applied** — a *clean* `dirty`. Under Unit A that workspace transitions to `changes_staged` when the same task
re-locks, so the aggregate reports "one or more file mutations applied; commit pending" (`:375`) for a workspace that has
staged nothing, and a subsequent `CommitSucceeded` records a commit of the empty set. Under Unit B the same request is
rejected as `state_transition_invalid`. Worse, the two units disagree about the *branch count* of the pair — two branches
under A, three under B — so `:447`'s gate ("every guard branch needs its own asserted outcome") has two different denominators
and "CI fails if a guard branch is added without coverage" is again not computable. This is the same class of defect ADV-9
raised, reintroduced by the fix.

**Split #2 — the clearing rule cannot fire in MVP.** `:446`: `stagedBy*` "are cleared **only on commit, discard, or C3
cleanup**". Check each against the document:

- **commit** — `:404`, live.
- **discard** — `:414` `OperatorDiscardRequested` is "**reserved post-MVP; fails closed in MVP code**".
- **C3 cleanup** — `:1014`: "`changes_staged`, `dirty`, `unknown_provider_outcome`, and `reconciliation_required` are **never
  cleanup-eligible**".

So on a `dirty` workspace there is exactly **one** clearing path in MVP, and it is the one path a `dirty` workspace cannot
take. `:446` itself names the consequence — *"reject every re-acquire and the workspace is permanently unlockable with no
cleanup path"* — and then writes the clearing rule that produces it. The escape the prior run asked for (`ReconciliationRequested`
→ `reconciliation_required`, which does exist at `:413`) is **not** in the clearing list, so a workspace that exits via
`ReconciliationCompletedClean` (`:423`) returns to `ready` still carrying a stale `stagedByTaskId` — and the next
`LockLeaseExpired` puts it back in `dirty` where that stale id now satisfies Unit A's guard for a principal who no longer owns
anything.

**Tightening (in scope).** Make the mapping row carry the full predicate (`stagedByTaskId` match **and** staged content
present) or delete the staged-content conjunct from `:411` — one of the two, not both. Add `ReconciliationCompletedClean`,
`ReconciliationCompletedDirty`, and the `inaccessible` → `ready` branch to the clearing list, and state the disposition for a
`dirty` workspace whose originating task is terminal or unresolvable (the prior run proposed reconciler-emitted
`ReconciliationRequested`; any named answer will do).

---

### ADVU-3 — The new single-writer arbitration names as sole writer the component that structurally cannot produce `Indexed`, so the rule as written guarantees the empty-search failure it was added to prevent

**Severity:** critical (authorized context-search returns `Allowed` with zero items, with no failure signal — the exact
fail-safe-masquerading-as-capability mode `:253` exists to prevent)
**Prior finding:** ADV-8 — **WORSE.** Arbitration now exists and selects the wrong writer.

**The new rule (`:675`, added by this amendment):**

> **the projection is the sole writer of bridge state, and the reconciler is a repair path that may only re-publish what the
> projection already asserts** — it never writes bridge state directly, and never deletes an entry the projection still holds.
> A replay rebuilds the projection from events and must converge to the same bridge content it had before

**Verified against the tree.**

- `ISemanticIndexingBridgeWriter` (`src/Hexalith.Folders/Projections/SemanticIndexing/ISemanticIndexingBridgeWriter.cs`) has
  three methods: `ApplyFolderEventsAsync` (event-derived — the projection path) and
  `RecordIndexingResultAsync` / `RecordRemovalEvidenceAsync` (delivery-outcome path).
- The event-derived path produces `SemanticIndexingBridgeStatus.Stale`
  (`SemanticIndexingBridgeProjection.cs:202`) and `Tombstoned` (`:296`). It never produces `Indexed` or `Failed`.
- `Indexed` and `Failed` are set only through `RecordIndexingResultAsync`, called from
  `src/Hexalith.Folders.Workers/SemanticIndexing/SemanticIndexingProcessManager.cs:290` — i.e. by the egress/reconciler path
  the new rule forbids from writing.
- `:179` drops from hydration "any hit without a current entry or **not in a live indexed state**".

**Unit A — Story 10.7's projection owner** (story `done`, `sprint-status.yaml:203`, but re-derivable by any future maintainer
reading this rule). Obeys "sole writer" and "a replay must converge to the same bridge content": makes `ApplyFolderEventsAsync`
authoritative over the entry. A rebuild from an empty checkpoint therefore re-derives every entry as `Stale`.
**Unit B — Story 12.5's reconciler owner** (`12-5-at-least-once-memories-egress-and-reconciler`, `sprint-status.yaml:237`,
`backlog`). Obeys "never writes bridge state directly": stops calling `RecordIndexingResultAsync` and only re-publishes.

**Incompatible artifact.** With both units compliant, **nothing in the system can set `Indexed`**. `:179` then drops every
candidate and the authorized facade returns `Allowed` with zero items — a well-formed success. FR58's round trip flips to
empty and no gate fires, because an empty result is not an error. The prior run's failure required a *replay* to trigger; the
amended rule produces it on the steady-state path. The ADV-8 tightening asked for the opposite assignment (event-derived
fields to the projection, delivery-outcome fields to the reconciler, with the latter declared non-deterministic alongside the
external-clock carve-out in concern #9 at `:112`); the amendment assigned everything to the projection and left concern #9's
carve-out untouched.

**Tightening (in scope, doc-only).** Split the write surface in the rule, not the component: *"Event-derived fields (version
identity, tombstone/removal state, policy outcome) have exactly one writer — the projection — and are rebuilt on replay.
Delivery-outcome fields (`indexed`, `failed`, attempt counters, egress evidence) are owned by the Story 12.5 egress path,
are declared non-deterministic alongside the external-clock carve-out in concern #9, are never reset by a rebuild, and are
re-established by re-driving the egress reconciler after one."* Also record the as-built delta (the reconciler writes bridge
state today through `RecordIndexingResultAsync`) the same way this amendment recorded the Testcontainers, `oasdiff`, `kind`,
and `RateLimiting/` deltas.

---

## HIGH

### ADVU-4 — S-7's new 401 outcome is "evaluated before every other conjunct" while S-4's PD10 evaluation order puts authority-unavailability first; two units build opposite middleware pipelines and one produces the outcome S-7 forbids

**Severity:** high (an unauthenticated caller receives the retryable outage envelope during a Tenants outage — S-7 names this
as the thing the ordering exists to prevent)
**Prior finding:** ADV/F15 (validation-vs-authority order) — **WORSE.** The amendment added a third evaluation step without
reconciling the existing order statement.

- **S-7 (`:656`, new):** "**(1)** `authentication-failure-401` … **evaluated before every other conjunct, so an
  unauthenticated caller is never routed to the outage envelope.**"
- **S-4 (`:653`, unchanged, same PD10 date):** "**Evaluation order (PD10, 2026-09-15):** **authority-unavailability is
  evaluated first**, then authority, and only then any protected-resource lookup".

**Unit A — the Epic 5 / Story 13.2 server pipeline built from S-4.** Middleware order: tenant-projection availability check →
authority → lookup. During a Tenants outage every request, authenticated or not, short-circuits to the
`authority-unavailable-503`.
**Unit B — the same pipeline built from S-7.** Authenticate → 401 → then availability.

**Incompatible artifact.** The two orders differ observably for exactly the population S-7 calls out: unauthenticated callers
during an authority outage. Unit A hands an anonymous caller a retryable 503 that says the *authority evidence* is stale,
which is both an availability signal to an unauthenticated party and a free retry loop. Neither unit is non-compliant; the
document states both orders as PD10 decisions. A third order exists at `:985` (A-9: "authentication and authoritative …
authorization → canonical structural/semantic validation → trusted intent construction"), which is consistent with S-7 but
silent on availability, and a fourth at `:1030` for context queries.

**Tightening.** State one ordered list, once, and have S-4, S-7, A-9, and the context-query list all point at it:
authentication → authority-availability → authority → canonical validation → protected-resource lookup (or whatever order is
intended — the point is that one of them is authoritative and the other three are transcriptions).

---

### ADVU-5 — The 503 envelope's category maps to CLI exit **72**, which this document's own canonical table defines as `reconciliation_required`, "not retryable until cleared" — and half of S-7's newly named tokens are not in the vocabulary S-7 claims they are in

**Severity:** high (cross-surface behavioural divergence on `retryable`, plus a silent exit-code collision that makes an
authority outage indistinguishable from a workspace needing human reconciliation)
**Prior finding:** ADV-4 / F5 — **half-closed.** The categories are now real; the derivation the amendment claims is not.

**Progress, credited.** ADV-4's headline defect is fixed: `authorization`, `availability`, and `authority_unavailable` are
gone. `docs/contract/authorization-matrix.md:140`–`:146` now carries a **Canonical Outcomes** table and S-7 transcribes it
byte-accurately, with the right precedence rule ("on any disagreement the matrix wins and this row is the defect"). Verified:
`authentication_failure`, `tenant_access_denied`, and `read_model_unavailable` are all members of
`parity-contract.schema.json` `$defs/canonical_error_category` (50 members).

**What is still broken.**

1. **The exit-code collision.** `tests/fixtures/parity-contract.yaml:604`–`:606` maps
   `canonical_error_category: 'read_model_unavailable'` to `cli_exit_code: 72`, `mcp_failure_kind: 'read_model_unavailable'`.
   The architecture's canonical CLI table at `:722` assigns **72** to `reconciliation_required` — "*Workspace in
   `reconciliation_required` state; **not retryable until cleared***". S-7's 503 is `retryable: true`, client action `retry`.
2. **`authentication_failure` has no exit-code row at all** (`:713`–`:727`), so it falls to the catch-all `1`
   `internal_error`, "not retryable".
3. **The claim "Every token above is a member of the closed canonical vocabulary in
   `tests/fixtures/parity-contract.schema.json`" is false for five of them** — verified with `grep -c`:
   `resource_unavailable` 0, `authentication_required` 0, `check_credentials` 0, `no_action` 0, `retry` 0. The oracle has no
   `code` vocabulary and no `retryable` or `clientAction` column at all, so "the C13 `cli_exit_code` / `mcp_failure_kind`
   columns are **derived from the matrix**, so CLI and MCP cannot disagree on whether an authority outage is retryable" is
   unsatisfiable by construction: there is nothing in the oracle for retryability to be derived *into*.
4. **The one-to-one claim at `:732` is false.** `canonical_error_category` has 50 members, `mcp_failure_kind` has 49;
   `client_configuration_error`, `credential_reference_missing`, and `success` are categories with no kind, and `none` and
   `usage_error` are kinds with no category.

**Unit A — the Epic 5 CLI adapter story** (`Hexalith.Folders.Cli`, parity tests in `tests/Hexalith.Folders.Cli.Tests`).
Consumes the oracle as instructed. An authority outage exits **72**. Automation keyed on the architecture's published table
reads 72 as "workspace needs human reconciliation; do not retry".
**Unit B — the Epic 5 MCP adapter story.** Emits `kind: read_model_unavailable`, `retryable: true`. Automation retries.

**Incompatible artifact.** `:735`'s cross-adapter invariant — "the (canonical category, code, retryable flag, clientAction)
returned by SDK, REST, CLI (post-projection), and MCP (post-projection) are **identical**" — fails on every
authority-unavailable response, and the CLI surface additionally conflates two unrelated operational conditions behind one
exit code, which is precisely the "never collapse multiple categories" rule at `:732`.

**Tightening.** Either assign `read_model_unavailable` and `authentication_failure` their own exit codes in `:713`–`:727` and
in the oracle's `$defs/cli_exit_code` enum (currently a closed 15-value set), or state explicitly in S-7 that the CLI cannot
distinguish an authority outage and that this is accepted. Add `code`, `retryable`, and `client_action` columns to the C13
oracle and its schema, or delete the "derived from the matrix" sentence — it currently promises a mechanism that does not
exist. Publish `resource_unavailable` / `authentication_required` in whatever the code vocabulary turns out to be.

---

### ADVU-6 — `visibility` now has **three** closed vocabularies for one required wire field, and the third one is shipped code pinned by a reviewed contract the architecture does not reference

**Severity:** high (wire-enum divergence on a field A-8 requires on every error; the generated client cannot deserialise two
of the three)
**Prior finding:** ADV-5 / F6 — **untouched.**

- **S-7 (`:656`)** — "`visibility` … is enumerated — `metadata_only`, `redacted`, `withheld` — not free text".
- **S-6 (`:655`, unchanged)** — "Surfaces render a confidential field as a **withheld** state carrying its token, kept
  visibly distinct from **redacted**, **unavailable**, and **absent**."
- **Shipped, verified** — `src/Hexalith.Folders.UI/Services/FieldDisclosure.cs:23` declares the closed set `Visible`,
  `Redacted`, `Unknown`, `Missing`. `docs/ux/ops-console-wireflows.md:229` publishes it as the source of truth and its
  `mutation_rules` (`:29`–`:32`) state that downstream stories "do not redefine, re-number, or fork … the `FieldDisclosure`
  members", enforced by `tests/Hexalith.Folders.Testing.Tests/OpsConsoleWireflowNotesTests.cs`. `withheld` maps to none of the
  four. `grep -c "ops-console-wireflows" architecture.md` → **0**: the architecture never references the artifact that owns
  the shipped vocabulary.

**Unit A — the PD10 spine regeneration (still unowned; `:277`–`:283`).** Implements S-7's three-value enum. Every response
that reports an unavailable projection or an absent field fails enum deserialisation on SDK, CLI, MCP and UI.
**Unit B — an Epic 6 console story (`Hexalith.Folders.UI`, `:1583`).** Implements S-6's five render states because concern
#11 (`:114`) requires "no cleartext exists" to be distinguishable from "projection is down" from "field not present" — and
finds `FieldDisclosure` already closed at four, none of which is `withheld`, with a mutation rule forbidding it from adding
one.

**Incompatible artifact.** Three closed enums, one required wire field. Secondary divergence unchanged from ADV-5: `withheld`
has no canonical error category, no CLI exit code, and no MCP kind, while `:726` assigns exit 75 to `redacted` — so one
implementer collapses `withheld` into 75, destroying the distinction S-6 just created, and another mints a token outside the
published set.

**Tightening.** Declare the closed set **once**, in A-8, as the union that actually has to exist; add a `visibility_value_set`
column to the C13 oracle and its schema (currently zero occurrences of `visibility` in either); reference
`docs/ux/ops-console-wireflows.md` from the architecture and say in one sentence how `FieldDisclosure` maps onto the wire
enum; and either give `withheld` an exit code / kind or state that it is a field-level render state that never appears as an
error category.

---

### ADVU-7 — Degraded mode is "reconciled" 550 lines away from the rule it voids; concern #20's normative sentence is unchanged, three other routings still point at the voided mechanism, and the shipped `TenantAccessAuthorizer` implements it

**Severity:** high (a revoked tenant member keeps reading through the staleness window under one compliant unit; the other
returns 503 — and the as-built is the first one)
**Prior finding:** new for this pass, arising from amended text

**The new arbitration (`:673`).** "The approved `authorization-matrix.md` is normative and settles it: **trusted tenant,
membership, or delegation evidence that is stale or unavailable returns the 503 envelope — it is never served from a stale
projection.** Concern #20's bounded-staleness allowance survives only for read models that are *not* authorization evidence".

**What was not changed.**

- `:123` — concern **#20** itself, in the "designed once" list, still reads: "**local tenant-access projection allows read
  paths to continue under bounded staleness when Hexalith.Tenants is unavailable**; mutations require fresh authorization".
  The only edit was appending "(reconciled 2026-09-16 — see the note under the security decisions)" to the heading. The
  normative sentence survives verbatim, and the tenant-access projection *is* authorization evidence, so the note voids the
  concern's operative clause while leaving it in the concern list.
- `:763` — I-7 still monitors a "**Tenants-availability degraded-mode active flag**" as a health snapshot.
- `:786` — Phase 4 still delivers "**Tenants-availability degraded-mode wiring**".
- `:1639` — the concern-routing table still routes "#20 Tenants-availability degraded mode" to
  `Authorization/TenantAccessAuthorizer.cs`.
- **Shipped code, verified:** `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs:16-18` exposes
  `AuthorizeDiagnosticReadAsync(...)` → `AuthorizeAsync(context, allowBoundedStale: true, …)`, and
  `EffectivePermissionsFolderPermissionEvidenceProvider.cs:47` returns a snapshot when the read model is `Stale` and
  `AllowBoundedStale` is set, labelling the evidence `"bounded_stale"` (`:103`).

**Unit A — the Phase-4 tenant-integration path (shipped, and re-derivable from concern #20, I-7, Phase 4, and `:1639`).**
Serves diagnostic reads from a bounded-stale tenant-access projection.
**Unit B — an Epic 5 / Story 13.2 implementer reading `:673`.** Returns the 503 envelope.

**Incompatible artifact.** Same request, same outage: 200-with-data versus 503. S-8 has just re-scoped
`GetReadinessDiagnostics` and `GetProjectionFreshness` to folder scope, making them protected folder operations, so the
diagnostic-read carve-out that `AuthorizeDiagnosticReadAsync` was written for is now inside the 503's stated blast radius.
The asymmetry is the point: this amendment went to real trouble to annotate `Testcontainers`, `oasdiff`, `kind`/`daprd`,
`TenantPrefixedCacheKey.cs`, and `RateLimiting/` as "named in the 2026-05 draft, never built" — and then declared a security
rule that shipped code contradicts, with no as-built note anywhere.

**Tightening (doc-only, in scope).** Rewrite concern #20's own sentence so the concern list carries the decision; update I-7,
Phase 4, and the `:1639` routing; and record the as-built delta (`AuthorizeDiagnosticReadAsync` / `AllowBoundedStale`) as the
owned gap, naming the story that removes or re-scopes it.

---

### ADVU-8 — The D-9 413 retry-header collision is untouched *and* unrecorded, while the same pass rewrote four neighbouring rows

**Severity:** high (the SDK's large-file fallback never fires; a request-side header name is reused for a response with a
disjoint value set)
**Prior finding:** ADV-6 — **untouched, and not routed as an open item.**

Verified unchanged at HEAD+working-tree:

- `:639` (D-9): "the 256KB boundary enforced server-side via `413 Payload Too Large` plus a **`x-hexalith-retry-as: stream`**
  response header."
- `:914` (Format Patterns): "`X-Hexalith-Retry-Transport` | response | … **distinct from the request-side
  `X-Hexalith-Retry-As`** (retry-allocation `[caller, operator]`) — **names kept disjoint to avoid round-trip echo
  conflicts**"; `:915` reserves `X-Hexalith-Retry-As` for the request side.
- `epics.md:386` (acceptance authority, out of scope this run) encodes D-9's spelling.
- Shipped code uses the other name: `FoldersDomainServiceEndpoints.cs:3053` emits `X-Hexalith-Retry-Transport`;
  `Convenience/FoldersFileUploadExtensions.cs:171` reads it.

**Unit A — an Epic 5 file-mutation endpoint story** built from D-9 + AR-PATTERN-03 emits `X-Hexalith-Retry-As: stream`.
**Unit B — the D-9 SDK convenience helper** looks for `X-Hexalith-Retry-Transport`. The over-boundary upload returns a hard
413 and D-9's entire "unimodal DX" rationale fails silently; if A wins instead, one header name carries `[caller, operator]`
on the request and `stream` on the response — the exact echo conflict `:914` was written to prevent.

**Why this one counts against the pass rather than being dispositioned as an open item.** The architecture half of the fix is
a **one-token edit inside D-9**, fully within the ratified scope, in a decision table where four sibling rows (A-5, A-7, I-1,
I-8) were rewritten in this very pass. It was neither fixed nor recorded: `grep "^\*\*Open —"` returns nine items and none of
them mentions the header. The `epics.md:386` half is correctly out of scope and should be recorded as such.

---

### ADVU-9 — Three present-tense "deployed behaviour" claims about the search bridge are still false at HEAD, in a paragraph this amendment edited, and rank 30 still schedules a `done` story behind four `backlog` ones

**Severity:** high (two stories build mutually exclusive deployed contracts, each citing the architecture; the schedule table
the document calls machine-checkable is unsatisfiable)
**Prior finding:** ADV-7 — **untouched.** The amendment edited the same paragraph and left the code claim.

Verified at working tree:

- `:181` still asserts: "the deployed `Hexalith.Folders.Server` composition does **not** register an EventStore-backed bridge
  read model: `AddFoldersContextSearchFacade` leaves the fail-safe `UnavailableSemanticIndexingBridgeReadModel` default in
  place (`FoldersServerServiceCollectionExtensions.cs:84-86`)". The amendment rewrote the *end* of this same bullet (the 10.9
  framing) and left the code assertion.
- `:148`–`:151` repeats it: "the deployed Server facade runs on the fail-safe `Unavailable` bridge read model … until … Story
  10.7 … and … Story 10.8".
- **Reality:** `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:130-134` does
  `services.RemoveAll<ISemanticIndexingBridgeReadModel>()` and registers `EventStoreSemanticIndexingBridgeStore`. The
  `Unavailable` default is gone. `sprint-status.yaml:203-204`: `10-7…: done`, `10-8…: done`.
- `:221` (rank 30) still reads "10.8 follows 12.1–12.3, 12.5, completed 10.7, the Story 11.15 DCP lane, and the corrected
  authorization projection", while `sprint-status.yaml:233`, `:237`, `:223` show 12.1, 12.5 and 11.15 all `backlog`.

**Unit A — Epic 6 Story 6.14 (`sprint-status.yaml:157`)** builds deployed-host console journeys asserting
`ReadModelUnavailable` / safe-empty as the correct deployed indexing-status contract, per `:181`/`:190`.
**Unit B — Story 10.8 (done)** asserts "the first search/status response is non-empty and tenant-correct" (`epics.md:2323`).
Two deployed-behaviour contracts for one endpoint, both sourced from a declared authority; and a Story 12.5 implementer
reading `:181` has documentary licence to re-register the `Unavailable` default.

**Tightening (doc-only, in scope).** Past-tense the three passages with the landing evidence, or replace the inline
`file.cs:line` assertions with a pointer plus a conformance test. Apply the `:212` escape hatch explicitly to the rank-30 row
for 10.8, or re-rank it.

---

### ADVU-10 — Concern #5 classes `timeout` as a **known** failure while `:406` routes transient/network failures to `dirty`; the "shared classifier" owner names a component but not the classification

**Severity:** high (staged work terminal on one reading, preserved on the other — the same blast radius as ADVU-1, reachable
through a different door)
**Prior finding:** F10 — **untouched.** The amendment repeated the owner sentence into the mapping doc without adding the map.

- `:108` (concern #5): "distinguish **known failure (timeout / 401 / 403 / 404 / 409 / 429 / 5xx / branch-protection /
  missing-or-deleted repository / stale clone / credential revocation / drift)** from unknown outcome".
- `:405`: `changes_staged` → `failed` on `CommitFailed` "(**known non-retryable**: branch protection, validation refusal,
  permanent 4xx)".
- `:406`: `changes_staged` → `dirty` on `CommitFailed` "(**retryable, no confirmed remote effect**: transient 5xx, throttling,
  network)".
- `:445` and `c6-transition-matrix-mapping.md:20` both now say the classification "is owned by the shared provider-outcome
  classifier, not re-decided per adapter" — which names *who decides*, not *what the decision is*.

**Unit A — the GitHub adapter's classifier contribution** reads concern #5 and classes a commit `timeout` as a known failure
→ `failed`, staged work terminal.
**Unit B — the Forgejo adapter's contribution** reads `:406` and classes the same timeout as network / no-confirmed-remote-
effect → `dirty`, staged work preserved. Both are inside the single shared classifier, so "the adapters must not disagree"
is satisfied by construction while the classifier itself has two compliant readings. A timeout is the canonical case of *no
confirmed remote effect*, and concern #5 is the only list in the document that enumerates failure classes.

**Tightening.** Publish the mapping table the owner sentence implies: provider signal → `{known non-retryable, retryable-no-
confirmed-remote-effect, unknown outcome}`, with `timeout` assigned explicitly, and reconcile concern #5's list against it.

---

## MEDIUM

### ADVU-11 — `:445` and `:446` prescribe two incompatible guard mechanisms, and the enum reading turns a server-side guard back into a caller-supplied parameter

**Prior finding:** ADV-9 — **not closed, and compounded by the new `:446`.**

`:445` (unchanged): "implements this matrix as a switch expression over **`(currentState, eventType, resolution)`** … where
`resolution` is the `FolderWorkspaceDirtyResolution` discriminator". `:446` (new): the guards read durable aggregate fields
(`stagedByTaskId`, staged-content presence, C3-window membership).

**Verified.** `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:47-51` — `Transition(FolderWorkspaceLifecycleState?
currentState, FolderWorkspaceLifecycleEvent attemptedEvent, FolderWorkspaceDirtyResolution? dirtyResolution = null)` is a pure
static function with **no aggregate-state parameter**.
`FolderWorkspaceDirtyResolution.cs:8-12` has exactly two members (`CommitConfirmed`, `CommitRejected`), each with a
`JsonStringEnumMemberName` — it is a serialised wire type.

**Unit A — Story 4.19** keeps the pure signature and extends the serialised enum to carry the guard branches. This (a) changes
the wire surface with no declared regeneration duty — contrast `:437`, which declares that obligation for
`LockLeaseBecameStale` and nothing else — and (b) makes the guard a **parameter supplied by the caller of `Transition`**, so
the aggregate cannot itself verify it came from durable state, re-violating `:446`'s own "never from caller input (S-3, S-8)".
**Unit B — Story 4.21** changes the signature to take the workspace state, keeps the two-member enum, and introduces a
purpose-built internal guard union.

Two different branch denominators again, so `:447`'s "CI fails if a guard branch is added without coverage" is not computable.
**Tightening:** name the guard type (a domain-internal closed union, explicitly not a wire type), state that `Transition`
takes the workspace state, and say which `(state, event)` pairs accept which guard family.

---

### ADVU-12 — The `unknown_provider_outcome` disposition divergence is now *recorded*, but the recording names two artifacts where five carry it

**Prior finding:** ADV-10 / F4-sibling — **half-closed.** Recording is the right disposition for the code half; the doc half
was in scope and the artifact list is short.

`:429` (new) says the divergence is "**`awaiting-human` in the mapping document and the diagram**, and
`FolderStateTransitions.cs:157` still maps `Dirty` to `AwaitingHuman`". Verified — and three more sites carry it:

1. `docs/exit-criteria/c6-transition-matrix-mapping.md:44` — `unknown_provider_outcome` | `awaiting-human` | approved |
   review date **2026-05-11**. The same commit refreshed the `dirty` row (`:40`, dated 2026-09-15) and rewrote five other
   rows in this file, so this row was in scope and in hand.
2. `docs/diagrams/workspace-lifecycle.md:26` and `:41` (`state "awaiting-human · unknown_provider_outcome"`).
3. `src/.../FolderStateTransitions.cs:161` — `UnknownProviderOutcome => AwaitingHuman` (the same method as the `Dirty` case
   `:429` does name).
4. `tests/.../FolderStateTransitionsTests.cs` — the `OperatorDispositionShouldMatchC6StateCatalog` theory pins
   `UnknownProviderOutcome → AwaitingHuman` as `[InlineData]`; despite its name it reads no document.
5. `docs/ux/ops-console-wireflows.md:257` — pins `unknown_provider_outcome` to "the `awaiting-human` disposition badge
   (Warning) per §2.1/F-4 — **never** a neutral 'Unknown' with no badge", under the mutation_rules lockstep at `:29`–`:32`
   and the `OpsConsoleWireflowNotesTests` gate.

**Unit A — the Epic 6 disposition-label story**, generating from the mapping artifact (`:57` routes its rows to "Story 6.3
`OperatorDispositionBadge` mapping") → renders `awaiting-human`.
**Unit B — the Epic 4 read model** per `:380` → emits `operatorDisposition: auto-recovering`. `:383` requires the
`unknown_provider_outcome` view to show automatic reconciliation progress *and* keep retry/takeover controls absent, so Unit
A tells a 3 a.m. operator to intervene while offering nothing to act on — the exact cognition failure F-4 exists to prevent.

An implementer handed a two-artifact list will fix two and leave three, including two with conformance gates that will then
redden. **Tightening:** name all five, or state once that the `:369` catalog is the single source and every other artifact
mirrors it.

---

### ADVU-13 — Hard-coded denominators got worse: the amendment removed one, re-asserted the prohibition, and added a new one

**Prior finding:** ADV-12 / F14 — **WORSE.**

- **Removed (credited):** `:242` (the *Current reality* PD10 row) now reads "403 is live on **every protected operation in the
  current generated Contract Spine inventory** … the denominator is the generated inventory, **never a number transcribed
  here**", and the technology-pin list at `:1741` was correctly de-versioned.
- **Survives:** S-7 `:656` still opens "**All 49 protected Contract Spine operations**". `:732` still says "the full
  `CanonicalErrorCategory` enum (**43** post-SDK members)" — verified actual: **46** distinct values in
  `parity-contract.yaml`'s `error_code_set`, **50** in the schema `$defs` — in the same sentence that ends "Surface
  denominators … are always the current generated Contract Spine inventory — never hard-coded counts (2026-07-15)".
- **Added by this amendment:** `:679` (new open item) — "**The 49 protected operations** are protected because each one opts
  in".

So the document now states the prohibition twice and violates it three times, once in text written the same day.
`tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs:148` pins `spine.Length.ShouldBe(49)`,
and PD10 itself adds operations — so the first unit to add the incident-evidence operation reddens a conformance pin and
leaves the architecture the drifting artifact. **Tightening:** delete the three numerals.

---

### ADVU-14 — The amendment added a third artifact to the "one model" invariant without adding any gate that spans the doc/code boundary, and the gate that claims to span it reads nothing

**Prior finding:** ADV-11 / F12 — **half-closed.**

`:427` now declares `docs/diagrams/workspace-lifecycle.md` co-normative alongside the matrix, the C6 mapping document,
`FolderStateTransitions.cs`, and the lifecycle tests. Credit where due: the diagram *was* updated in this pass — `:93`–`:98`
now names all four guard-discriminated pairs and states that "the diagram cannot show the guard, so the edge labels alone do
not determine the outcome".

But the gate situation is unchanged and the amendment's own text overstates it:

- `tests/.../FolderStateTransitionsTests.cs:140` `StateCatalogAndEventVocabularyShouldMatchC6MappingDocument` — **does not
  read `c6-transition-matrix-mapping.md` at all.** It compares `FolderStateTransitions.EventVocabulary` against a hard-coded
  23-name literal list (`:159`–`:184`) and the state catalog against a hard-coded 11-name list. The name asserts a doc
  conformance the body does not perform.
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` compares the *diagram* to the *C6
  document* — two artifacts updated together — so it stays green.
- `LockLeaseBecameStale` remains in the C6 document (`:51`) and the diagram (`:72`) and has **zero occurrences in `src/` and
  zero in `tests/`** (verified). Three documents and a diagram assert an event the wire vocabulary does not have, with every
  gate green.

`:437` states honestly that "the gate will not flag the gap for you", which is the right disposition for a code-blocked item.
What is missing is the doc-side half the prior run asked for: a statement that the vocabulary gate **must be derived**
(`EventVocabulary` ↔ parsed C6 document ↔ spine enum) rather than pinned to a literal, and a note that
`StateCatalogAndEventVocabularyShouldMatchC6MappingDocument` does not do what its name says.

---

### ADVU-15 — The cleanup trigger is genuinely unified, but restated using the same two overloaded tokens it was fixing, and "terminal task closure" still names no event

**Prior finding:** ADV-3 / F9 — **CLOSED on its main axis.** This is the one critical the pass actually closed, and it closed
cleanly: `:1013` now reads "only after **terminal task closure with no active task** — the single trigger fixed by C3 … **`inaccessible`
is a workspace state, not a task closure, and does not trigger cleanup**; neither does lock expiry, lease staleness, or task
cancellation alone". `:441` records the reachability residue as an owned open item routed to Legal + Product + Security with
A7b. Both dispositions are correct.

**The residue.** The corrected sentence continues: "Terminal task closure means the task reached **committed, failed**, or
explicit no-change closure." `committed` and `failed` are also two of the eleven workspace lifecycle states at `:377`/`:378`
— the exact ambiguity the sentence was rewritten to remove, de-conflicted for `inaccessible` only. And the document still
enumerates no task-closure event: the C6 event vocabulary (`c6-…:51`, 24 events) contains none, and there is no
`TaskCompleted` / `TaskFailed` / `TaskAbandoned` anywhere in the architecture.

**Unit A — Story 12.2 (`12-2-durable-projections-and-task-completion-pipeline`, `sprint-status.yaml:234`)**, which owns the
task-completion pipeline, reads "the task reached committed, failed" as the workspace-state tokens it already has an enum for
and keys the clock off `FolderWorkspaceLifecycleState.Failed`.
**Unit B — the C3/D-7 retention implementer** waits for a task-scoped closure signal that has no name, no event, and no
emitter, and the clock never starts.

**Tightening.** Name the task-closure events (or the task state machine) and use tokens disjoint from the workspace state
names. The prior run's `TaskAbandoned` proposal — the reconciler-emitted closure for a task whose lease and authorization have
both lapsed — also remains unaddressed and is the one that makes ADVU-2's orphaned `stagedByTaskId` recoverable.

---

## LOW

### ADVU-16 — The six new routed `Open —` items are unregistered prose: no IDs, no owner field, no rank, and not in the only machine-checkable copy of the schedule

The six new items (destination policy `:677`, deny-by-default HTTP binding `:679`, write concurrency `:642`, event-payload
schema evolution `:644`, deployment profile `:767`, disaster recovery `:769`) are well-written, correctly scoped, and name
real holes — the write-concurrency and schema-evolution ones in particular are findings a reviewer would have raised. Routing
them rather than deciding them is the correct disposition under the ratified scope.

The recording is thin, though. The document already has an ID scheme for exactly this (`OQ1`–`OQ13`, referenced by rank in
the wave table at `:216`–`:224` which `:210` calls "the only machine-checkable copy of the schedule"). None of the six got an
ID, an owner field, or a rank; three route to "Architecture, with Epic 12 / 13" while `epics.md` — which this run correctly
could not edit — carries no reference to any of them. `grep "^\*\*Open —"` is the only index. An open item that exists only as
a bolded paragraph is discoverable by a reader of the whole document and by nothing else. Give each an ID and a row in the
rank table (or a short register section) so the manifest can eventually encode them.

### ADVU-17 — Two prior findings were fixed in one location while their duplicates survive in three others

- **F17** (10.9 credited with live proof). The amendment rewrote the 10.9 framing at `:181` and added the clarifying note at
  `:226`. But `:293` still reads "**Epic 10** | Search-bridge projection, Server registration, authorization, hydration,
  pruning, **live search/status proof (Stories 10.7–10.9)**", and `:1326` and `:1623` repeat the `10.7–10.9` credit. One of
  four sites fixed.
- **F18** ("release reasons … such as `caller_completed`"). Unchanged in S-7 at `:656`: "MVP release reasons permit only
  approved values **such as** `caller_completed`". Still no field named, still no closed set, still an open-ended
  enumeration inside a row whose whole point is closed vocabularies.

---

## Prior-finding closure table (F1–F18)

| Prior | Status after the 2026-09-16 amendment | Evidence |
| --- | --- | --- |
| **F1** S-6 token derivation | **Closed** (unchanged since the last pass). | `:655` |
| **F2** tokenized ref vs canonical serializing identity | **Recorded open — recording improved.** `:659` unchanged, but `:661`–`:663` are new and much stronger: they state that the carve-out *relocates* durable cleartext rather than removing it, name the missing controls (store, key, encryption-at-rest, export/backup/replica exclusion, operator access), and propose three concrete options with a stated preference. Correct disposition. | `:659`–`:663` |
| **F3** double-defined `(state, event)` pairs | **Half-closed** — 4 copies fixed, 5 survive. → **ADVU-1** | `:107`, `:325`, `:352`, `:385`, `:1874` |
| **F4** disposition on a guarded row | **Closed at its row**; the sibling row is now *recorded* but under-scoped. → **ADVU-12** | `:429`, `c6-…:44` |
| **F5** S-7 envelopes underspecified | **Half-closed.** Categories are now real and matrix-owned; codes, exit codes, and the claimed derivation are not. → **ADVU-5** | `:656`, `:722`, `parity-contract.yaml:604-606` |
| **F6** `visibility` / `withheld` | **Untouched**, and now demonstrably three-way. → **ADVU-6** | `:655`, `:656`, `FieldDisclosure.cs:23` |
| **F7** no owner for the PD10 spine correction | **Recorded open — recording substantially improved.** `:277`–`:283` now covers all three PDs, names the PD8 tokenizer's missing home, and states the PD11 gate is a *live* not latent gap. Correct disposition. | `:277`–`:283` |
| **F8** wave table breaks its own rank rule | **Recorded open**, unchanged. → residue in **ADVU-9** | `:215`, `:221` |
| **F9** C3 window / cleanup trigger | **CLOSED** on the trigger axis; reachability correctly recorded. Residue only. → **ADVU-15** | `:1013`, `:441` |
| **F10** retryable-vs-unknown classification | **Untouched.** Owner named twice, map still absent, `timeout` inversion stands. → **ADVU-10** | `:108`, `:406`, `:445` |
| **F11** "the originating task" not identifiable | **Half-closed.** Durable fields declared (real fix); predicate split across the two amended artifacts and the clearing rule is unsatisfiable. → **ADVU-2** | `:411`, `:446`, `c6-…:26` |
| **F12** `LockLeaseBecameStale` prose-only | **Half-closed.** Diagram updated and declared co-normative; no spanning gate added, and the gate that claims to span reads nothing. → **ADVU-14** | `:427`, `:437`, `FolderStateTransitionsTests.cs:140` |
| **F13** derived-scope shape unspecified | **Untouched**, partially compensated: `:677` names the one operation S-8 structurally cannot cover and routes it. Still no named Contract Spine type and no per-dimension sensitivity tier. | `:658`, `:677` |
| **F14** hard-coded denominators | **WORSE** — one removed, prohibition re-asserted, a third instance added. → **ADVU-13** | `:242`, `:656`, `:679`, `:732` |
| **F15** validation-vs-authority order | **WORSE** — a third step (401) added to S-7 without reconciling S-4's order. → **ADVU-4** | `:653`, `:656`, `:985`, `:1030` |
| **F16** NFR79/NFR80 two owners | **Closed**, and strengthened: `:283` adds the NFR74 band-vs-row gap explicitly. | `:274`, `:283` |
| **F17** 10.9 credited with live proof | **Untouched at 3 of 4 sites.** → **ADVU-17** | `:293`, `:1326`, `:1623` |
| **F18** open-ended release reasons | **Untouched.** → **ADVU-17** | `:656` |

**Tally:** closed **3** (F1, F9, F16) · half-closed **5** (F3, F4, F5, F11, F12) · correctly recorded as open **3** (F2, F7, F8) ·
untouched **5** (F6, F10, F13, F17, F18) · **worse 2** (F14, F15).

---

## What this amendment did well

It is worth separating the signal from the finding count, because several of these are model fixes:

- **`:429` and the *Current reality* table.** Writing "**Two divergences are open right now, and naming them is the point of
  the invariant**" directly under a "one model" claim is the correct way to hold an invariant you cannot yet satisfy. The
  PD11 row's upgrade from "rejects four of five" to "rejects three of five, and **two are worse than missing — they are
  accepted onto the unguarded branch**, so the guarded outcome is *unreachable*" is a sharper and verifiably correct
  statement than what it replaced.
- **The as-built annotation sweep.** `Testcontainers`, `oasdiff`, `kind`/`daprd`, `TenantPrefixedCacheKey.cs`, the `ci.yml`
  lint job, `RateLimiting/`, the `UseNuGetDeps` dual consumption mode, `Hexalith.Folders.EventStore`, and the version-pin
  de-listing all removed real divergence generators. The directory-tree preamble ("the repository wins and this tree is the
  defect"; unmarked entries "must not be cited as evidence that a path exists") converts a stale inventory into a safe one.
- **Requirements Coverage Validation losing its ✅.** Downgrading "Every NFR category is bound to a gate" to "nine of eleven,
  and the gap is named", and labelling I-3 as schedule-only / I-8 as not built inside the coverage bullets that credited
  them, is the kind of correction that costs credibility to make and buys it back.
- **The six routed open items** are genuinely good finds, particularly write concurrency (which correctly observes that the
  C6 matrix presumes a serialized apply sequence that multi-replica writes do not provide) and event-payload schema evolution
  (correctly noting the document is adding an event to a published vocabulary under a P7Y retention obligation).
- **ADV-3 closed properly**, in the right vocabulary, in the same pass as the C3 authority note.

## Recommendation

Do not accept as mechanism authority. **ADVU-1, ADVU-2, and ADVU-3 are all closable by document edits inside this run's
ratified scope** and should be closed before the next gate:

1. Propagate the triple keying to `:107`, `:325`, `:352`, `:385`, `:1874`, and add the guard marking to the transition table
   that `:365` already assumes exists.
2. Make `c6-…:26` and `:411` state one predicate, and extend `:446`'s clearing list so `stagedByTaskId` is clearable on a
   workspace that never commits.
3. Re-cut `:675` along the event-derived / delivery-outcome seam instead of along the component seam, and carve the
   delivery-outcome fields out of concern #9's determinism scope.

**ADVU-4 through ADVU-9** are the cross-surface contracts the Epic 5 adapter stories and the unowned PD10 regeneration will
consume; four of them (ADVU-4, ADVU-5's exit-72 collision, ADVU-7's rule-vs-shipped-code conflict, ADVU-13) are regressions or
new contradictions introduced by this pass rather than pre-existing gaps, and regressions from a remediation pass are the most
expensive kind — the reviewer who checked the amended paragraph reasonably believes the item is closed.

One structural note for the next pass. The dominant failure shape is now well established across two runs: **a decision gets
restated in the paragraph the reviewer pointed at, and the same rule's other copies are not searched for.** The mechanical
counter is cheap — for every decision token you change, `grep` the token across `architecture.md`, `docs/exit-criteria/`,
`docs/contract/`, `docs/diagrams/`, `docs/ux/`, and `epics.md` before closing the item, and list the hit count in the change
note. Every finding in ADVU-1, ADVU-6, ADVU-12, ADVU-13, and ADVU-17 would have been caught by that one step.
