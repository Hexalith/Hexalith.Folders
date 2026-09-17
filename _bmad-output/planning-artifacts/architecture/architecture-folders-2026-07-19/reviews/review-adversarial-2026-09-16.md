# Adversarial Spine Review — architecture.md (2026-09-16)

- **Reviewer lens:** adversarial — "construct two units one level down that each obey every AD to the letter yet still build incompatibly"
- **Target (read-only):** `_bmad-output/planning-artifacts/architecture.md` (1857 lines, `D-`/`A-`/`C-`/`F-`/`S-` decision IDs)
- **Units one level down:** epics/stories from `_bmad-output/planning-artifacts/epics.md` and the live inventory in `_bmad-output/implementation-artifacts/sprint-status.yaml`
- **Prior gate report:** `review-adversarial-2026-09-15.md` (3 critical / 6 high / 7 medium / 2 low) — verified item by item in §"Prior-run verification"
- **Date:** 2026-09-16
- **Verdict:** **FAIL** — 3 critical, 5 high, 4 medium.

## Method

For each attack I named two units that exist in `epics.md` / `sprint-status.yaml`, wrote the implementation each one
produces from the document text alone, and checked whether the two artifacts can coexist. Every claim about code,
fixtures, or sibling artifacts was verified against the working tree at the time of writing and the file/line is given.
Findings already raised on 2026-09-15 are not re-reported unless they are still open *and* the amendment changed their
shape; those are called out explicitly.

The headline result: **the PD11 guard-keying fix (prior F3) was applied in exactly one paragraph and contradicted in
four other normative places, including the artifact PD11 itself declares co-normative.** Two of the three new criticals
are direct consequences.

---

## CRITICAL

### ADV-1 — The `(state, event, guard)` keying exists in one paragraph; four other normative rules are still `(state, event)`-keyed, and the live CI gate arbitrates in favour of the implementation that destroys staged work

**Severity:** critical (silent loss of durable staged content; the gate is blind by construction)

**What the document says — the triple-keyed copy (:436–:437):**

> `FolderStateTransitions.cs` implements this matrix as a switch expression over **`(currentState, eventType, resolution)`** … PD11 makes **four pairs guard-discriminated**, so their outcome is a function of `(state, event, guard)` and never of `(state, event)` alone

> **The gate is keyed on `(state, event, guard)`, not `(state, event)`:** for a guard-discriminated pair every branch needs its own asserted outcome, because an implementation that handles one branch and silently takes the other path still satisfies "this pair has a defined outcome" while destroying staged work.

**What the same document says four other times — the pair-keyed copies:**

- `:358` — "Every `(currentState, event)` pair has a defined outcome. **Pairs not listed are rejected** with canonical error category `state_transition_invalid`"
- `:1065` (§Enforcement Guidelines → CI gates) — "**C6 transition matrix coverage gate** (every `(state, event)` cell asserted by at least one test; CI fails if a state or event is added without coverage)"
- `:1066` (§Enforcement Guidelines → PR review) — "new C6 state or event requires matrix update + transition tests in same PR" (no guard dimension)
- `docs/exit-criteria/c6-transition-matrix-mapping.md:14` and `:41` — "Every unlisted `(state, event)` pair rejects with canonical category `state_transition_invalid`" / "Default rejection | Reject every unlisted pair" — and its only implementation rule is "Implement every Architecture C6 listed **`(from, event) -> to`** row as a total switch expression or equivalent total mapping". The mapping document contains **zero** occurrences of "guard", "resolution", or "retryable" (verified).

`:420` declares that document co-normative: "This matrix, `docs/exit-criteria/c6-transition-matrix-mapping.md`, `FolderStateTransitions.cs`, and the lifecycle tests express **one** model; a divergence is a defect in the code or the test, never an alternative reading."

**Unit A — Epic 4 Story 4.19 (`4-19-prove-durable-workspace-prepare-and-lock-lifecycle`, `sprint-status.yaml:124`, `backlog`).**
Builds the aggregate from §Enforcement Guidelines and the C6 mapping artifact, both of which specify a total
`(from, event) -> to` mapping with an unlisted-pair default. First matching row wins:

```
(changes_staged, CommitFailed)              -> failed          // :398 is the first row
(inaccessible,  ProviderReadinessValidated) -> dirty           // :410 is the first row
```

Every retryable commit failure now reaches `failed` and staged changes are declared terminal. PD11 rule 1 (:422,
"Staged work is never silently lost") is violated by an implementation that obeys three of the four normative
statements. The gate that exists today —
`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs:82`
`EveryUnlistedStateEventPairShouldRejectWithoutChangingState` — is pair-keyed and passes.

**Unit B — Epic 4 Story 4.21 (`4-21-prove-real-commit-retry-conflict-and-unknown-outcome-reconci`, `sprint-status.yaml:126`).**
Builds the lifecycle tests from `:436`–`:437` and asserts both branches of each guarded pair, including
`(changes_staged, CommitFailed, retryable-no-confirmed-remote-effect) -> dirty`.

**Incompatible artifact.** A and B cannot both be green, and there is no arbitration rule: the document contains two
contradictory statements of the same gate's key, and the co-normative mapping artifact backs the losing one 2-to-1.
Blast radius is the worst in the document — Unit A silently discards uncommitted user work on every transient provider
5xx, and no gate, fixture, or oracle column detects it.

**Tightening (one commit).** Propagate the triple keying to every copy: restate `:358` as "every
`(currentState, event, guard)` triple has a defined outcome; a triple not listed is rejected"; replace `:1065`'s
"every `(state, event)` cell" with "every `(state, event, guard)` triple, and for a guard-discriminated pair every
declared branch"; add the guard dimension to `:1066`; and amend
`docs/exit-criteria/c6-transition-matrix-mapping.md:14`/`:41` plus its implementation rule to the
`(from, event, guard) -> to` form in the same change set. Add an explicit default rule for guard branches: *"a declared
guard whose branch is not enumerated in the transition table is rejected with `state_transition_invalid`; a pair may not
be treated as guard-free merely because only one of its branches is listed."*

---

### ADV-2 — `dirty` + `WorkspaceLocked` declares a guard but only one branch, and `:436`'s "never from caller input" guard rule is unimplementable, so one unit bricks the workspace and the other lets any principal resume another task's staged work

**Severity:** critical (cross-principal takeover of staged mutations inside a tenant, committed under the wrong identity; or a permanently unrecoverable, never-cleaned workspace)

**The rules being obeyed:**

- `:404` — "`dirty` → `changes_staged` | `WorkspaceLocked` by **the originating task**, on a dirty workspace that still holds staged changes | New lock instance under the unchanged canonical serializing identity; staged changes resume without re-staging"
- `:436` — "`dirty` + `WorkspaceLocked` (the originating task *vs* any other principal)" is one of the four guard-discriminated pairs, and "**Every guard is evaluated server-side from durable state, never from caller input (S-8)**"
- `:358` — "**Pairs not listed are rejected**" (pair-keyed; this pair *is* listed)
- `:677` (Adapter Parity Contract) — "**TaskId sourcing** | Caller-provided via `X-Hexalith-Task-Id` header. SDK does not generate"

**Verified gap.** The document never declares a durable field holding the staging task's identity. Repo-wide search for
`stagedBy`, `StagedByTask`, `originatingTask`, `OriginatingTask` across `src/` returns **zero** hits. Concern #4 (`:106`)
positively forbids the obvious substitute: "folder/workspace/task IDs are lock metadata, **never** collision identity",
and `:376` says "Lock recovery creates a new lock instance". So the only available input for the guard is the
caller-supplied `X-Hexalith-Task-Id`.

**Unit A — Epic 4 Story 4.19 aggregate.** Reads `:436` literally: the guard must come from durable state, and no durable
state carries it, so the safe reading is to reject `WorkspaceLocked` on a staged `dirty` workspace unless the lock
request can be proven to originate from the staging task — which it never can. Result: a `dirty` workspace with staged
changes is permanently unlockable. Cross-check the consequences the document itself specifies: `:407` makes
`OperatorDiscardRequested` fail closed in MVP, `:418` makes `OperatorMarkedFailed` fail closed, and `:984` states
"`changes_staged`, `dirty`, `unknown_provider_outcome`, and `reconciliation_required` are **never cleanup-eligible**".
The workspace has no terminal state, no discard path, and no cleanup path — it is a permanent orphan holding temporary
working files forever.

**Unit B — Epic 5 workspace-lock REST endpoint (`epics.md` Epic 5 workspace group; `sprint-status.yaml:129` `epic-5: in-progress`).**
Reads `:358`: the pair is listed, so the default-rejection rule does not fire; and reads `:404`: the row applies when the
request's task matches the staging task. It compares the caller-supplied `X-Hexalith-Task-Id` (per `:677`) against the
task ID recorded on the last `FileMutated` event. Any principal holding folder write authority who names that task ID
resumes another principal's staged changes, then commits them — under the resumer's identity, with the original
principal's content.

**Incompatible artifact.** Two implementations of one declared transition: one that can never fire and one that fires for
the wrong principal. S-8 (`:641`) exists precisely to close "a raw locator submitted by the caller is input, never
authority" — and this is the one dimension S-8 cannot derive from an authorized parent, because the lock (the parent) is
gone. The 2026-09-15 run raised the shape of this as F11 (medium); PD11 promoted it to critical by making the pair
guard-discriminated and then asserting a server-side-only guard rule that no declared durable field can satisfy.

**Tightening.** Add to the matrix and to `:436`: *"At first mutation the aggregate durably records `stagedByTaskId` and
`stagedByPrincipal` and retains them for the staged-content retention window. `dirty` + `WorkspaceLocked` → `changes_staged`
requires all of: the request's task resolves — through the task binding, not through the caller's assertion — to
`stagedByTaskId`; that task is live and bound to the same folder; and the caller holds current folder write authority.
Every other `(dirty, WorkspaceLocked)` branch — a different task, an unresolvable task, or a clean `dirty` workspace — is
rejected with `state_transition_invalid`. If the originating task is terminal or unresolvable, the reconciler emits
`ReconciliationRequested` so the workspace reaches `reconciliation_required` rather than remaining unlockable."*

---

### ADV-3 — Cleanup has two mutually exclusive triggers in the same document; one unit deletes staged content on a workspace-state change, the other never deletes it at all

**Severity:** critical (silent deletion of durable staged content on one reading; unbounded retention breaching NFR60/NFR62 on the other)

**Trigger #1 — `:430` ("C3 is the cleanup authority (2026-09-15)"), which is the PD11/A7b authority:**

> Temporary working-file cleanup starts **only after terminal task closure with no active task** — not on lock expiry, lease staleness, or task cancellation alone … **The lifecycle matrix never triggers destructive cleanup on its own: a transition into `ready`, `failed`, or `inaccessible` is a state change, not a deletion event.**

**Trigger #2 — `:983` (§Process Patterns → Cleanup), not updated by the same pass:**

> Platform-owned and automatic only after task-terminal closure (**committed, failed, inaccessible**, or explicit no-change closure) with no active task or lock

`committed`, `failed`, and `inaccessible` are three of the eleven **workspace lifecycle states** in the `:360` state
catalog, not task outcomes. `:983` therefore makes cleanup eligibility a function of workspace state; `:430` explicitly
forbids exactly that.

**Unit A — Epic 12 Story 12.2 (`12-2-durable-projections-and-task-completion-pipeline`, `sprint-status.yaml:234`), which owns the task-completion pipeline, plus the Epic 4 cleanup-status story (`epics.md:1289` Story 4.10).**
Implements `:983`. A workspace that went `changes_staged` → `inaccessible` (`:400`, credentials revoked / repository
deleted — "Staged changes preserved intact") has reached one of the named closure states, so the seven-day clock starts
at that transition and the temporary working files are deleted at day 7 (`:984`, "Temporary working files are deleted at
the C3 seven-day boundary").

**Unit B — the C3/D-7 retention implementer reading `:430`.** The task is still alive (mutation and commit are blocked,
but nothing closed it), so terminal task closure never occurs and the clock never starts.

**Incompatible artifact.** Under Unit A, PD11 rule 3 (`:424`, "once readiness is restored, `inaccessible` → `dirty` while
staged content remains within the C3 window, otherwise `inaccessible` → `ready`") silently changes outcome: the operator
restores access on day 8 and gets `ready` with the staged work gone — a direct violation of PD11 rule 1 ("staged work is
never silently lost") produced by an implementation that obeys `:983` and `:984` to the letter. Under Unit B the same
workspace retains temporary working files indefinitely, breaching NFR60/NFR62 (`epics.md:220`, `:224`) which both key the
seven-day deletion to **task**-terminal closure.

Note this is *not* the item the document already flags. `:432` records the open question about whether the window is
*reachable*; it does not record that `:983` names a second, state-based trigger. The prior run's F9 covered the
reachability half only.

**Tightening.** Correct `:983` to the task-closure vocabulary — *"only after terminal task closure (`TaskCompleted`,
`TaskFailed`, or `TaskAbandoned`) with no active task or lock; workspace lifecycle states are never themselves closure
triggers"* — and define `TaskAbandoned` as the reconciler-emitted closure for a task whose lease and authorization have
both lapsed, so an orphaned staged workspace has exactly one clock and exactly one owner. Do it in the same commit as the
A7b relock so `docs/exit-criteria/c3-retention.md` carries one trigger.

---

## HIGH

### ADV-4 — S-7's "fully named" envelopes use category and code tokens that are not members of the canonical vocabulary the CLI exit table and the MCP `kind` set are keyed on, so the two adapters still disagree about whether an authority outage is retryable

**Severity:** high (the prior run's F5 was closed with tokens that do not exist; cross-surface behavioural divergence on the one field that changes caller behaviour)

**What S-7 now says (`:640`):**

> **Both envelopes are fully named so the C13 `cli_exit_code` / `mcp_failure_kind` columns stay derivable:** the 404 carries category `authorization`, code `tenant_access_denied` (resource-shaped denials use code `resource_unavailable` under the same category), `retryable: false`, client action `verify_tenant_context_and_authorization`; the 503 carries category `availability`, code `authority_unavailable`, `retryable: true`, client action `retry_after_backoff`

**Verified facts.** `tests/fixtures/parity-contract.yaml` declares **46** distinct `canonical_error_category` values.
`authorization` is not one of them. `availability` is not one of them. `authority_unavailable` does not appear anywhere in
the file. `tenant_access_denied` *is* a category (not a code), and the document's own CLI table maps it at `:691`
("66 | `tenant_access_denied` | Authorization failed at tenant or folder ACL boundary"). The exit-code table (`:686`–`:702`)
and the MCP kind set (`:704`, "The `kind` set is identical to the canonical category set (one-to-one mapping)") are both
keyed on **canonical category**.

**Unit A — Epic 5 CLI adapter story (`Hexalith.Folders.Cli`, parity tests in `tests/Hexalith.Folders.Cli.Tests`, `epics.md` FR48 row / `architecture.md:1574`).**
Maps the server category through `:686`. Category `availability` has no row, so the catch-all applies: exit 1,
`internal_error`, `retryable` false (`:702`). Automation stops retrying a recoverable authority outage.

**Unit B — Epic 5 MCP adapter story (`Hexalith.Folders.Mcp`, `architecture.md:1575`).**
Obeys `:704`'s instruction to "Assert against the oracle file, not this prose". `availability` is absent from the oracle,
so it emits the nearest published member, `read_model_unavailable` (`parity-contract.yaml:604`), `retryable: true`.

**Incompatible artifact.** The cross-adapter invariant at `:708` — "the (canonical category, code, retryable flag,
clientAction) returned by SDK, REST, CLI (post-projection), and MCP (post-projection) are **identical**" — fails on every
authority-unavailable response. The same incident is a permanent failure on CLI and a retryable outage on MCP.

**Tightening.** Pick one vocabulary and move it in lockstep. Either (a) restate S-7 in terms of existing canonical
categories (`tenant_access_denied` for the 404; `read_model_unavailable` for the 503, with code `projection_unavailable`
or a new code under that category), or (b) admit `authorization` and `availability` as canonical categories and land the
oracle rows, the `:686` exit-code rows, the MCP kind set, and the generated matrix in the same change set. Say which in
S-7, not in a downstream artifact.

---

### ADV-5 — The `visibility` value set is declared closed twice with two different closures, on a field that is required on every error

**Severity:** high (wire-enum divergence on a required field; the generated client cannot deserialise one of the two)

**S-7 (`:640`):** "`visibility` is an enumerated field — `metadata_only`, `redacted`, `withheld` — **not free text**, so two surfaces cannot invent different vocabularies."
**S-6 (`:639`):** "Surfaces render a confidential field as a **withheld** state carrying its token, kept visibly distinct from **redacted**, **unavailable**, and **absent**."
**A-8 (`:664`):** "`visibility` is a required field on every error (PD10, 2026-09-15)".

**Verified.** `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` publishes `metadata_only`, `redacted`,
`unavailable`, `absent`; `withheld` appears nowhere in the spine. `tests/fixtures/parity-contract.yaml` and
`parity-contract.schema.json` contain zero occurrences of `visibility`, so the C13 oracle cannot arbitrate.

**Unit A — the PD10 spine regeneration (unowned; `:276` records that no story owns it).**
Implements S-7's closed three-value enum. The generated client's `visibility` enum loses `unavailable` and `absent`;
every existing response that reports an unavailable projection or an absent field now fails enum deserialisation on
SDK, CLI, MCP and UI.

**Unit B — Epic 6 UI story (`Hexalith.Folders.UI`, `:1543`).**
Implements S-6's five render states, because the console must distinguish "no cleartext exists" (`withheld`) from
"projection is down" (`unavailable`) from "field not present" (`absent`) — exactly what cross-cutting concern #11
(`:113`) demands.

**Incompatible artifact.** Two closed enums for one required wire field. Secondary divergence in the same pair:
`withheld` has **no** canonical error category, CLI exit code, or MCP kind. `:700` assigns exit 75 to `redacted`; one
implementer routes a confidential field to exit 75 (collapsing the distinction S-6 just created), another mints a new
code outside the published set.

**Tightening.** State the closed set once, in A-8, as `{ metadata_only, redacted, withheld, unavailable, absent }`,
declare it generated from the Contract Spine, add a `visibility_value_set` column to the C13 oracle and its schema, and
either assign `withheld` its own exit code / MCP kind or state explicitly that it is a field-level render state that
never appears as an error category.

---

### ADV-6 — D-9 and the header table name the 413 retry header differently, and `epics.md` — the acceptance authority — encodes D-9's spelling, which the header table warns is the colliding one

**Severity:** high (the SDK's large-file fallback never fires; a request-side header name is reused for a response with a disjoint value set)

**D-9 (`:627`):** "the 256KB boundary enforced server-side via `413 Payload Too Large` plus a **`x-hexalith-retry-as: stream`** response header."
**Format Patterns (`:882`–`:883`):**

> `X-Hexalith-Retry-Transport` | response | conditional | Set to `stream` on `413` from inline file mutations (per D-9); transport-substitution hint, **distinct from the request-side `X-Hexalith-Retry-As`** (retry-allocation `[caller, operator]`) — **names kept disjoint to avoid round-trip echo conflicts**
> `X-Hexalith-Retry-As` | request | conditional | Retry-allocation hint `[caller, operator]` on file mutations

**Verified.** `epics.md:386` (AR-PATTERN-03, the acceptance-authority statement of the header set) reads
"`X-Hexalith-Retry-As: stream`" — D-9's spelling, the one the architecture's own table calls a collision. The shipped
implementation uses the other one: `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3053` emits
`X-Hexalith-Retry-Transport`, and `src/Hexalith.Folders.Client/Convenience/FoldersFileUploadExtensions.cs:171` reads
`X-Hexalith-Retry-Transport`.

**Unit A — an Epic 5 file-mutation endpoint story built from AR-PATTERN-03 + D-9.** Emits
`X-Hexalith-Retry-As: stream` on the 413.
**Unit B — the D-9 SDK convenience helper (`UploadFileAsync`, same decision row).** Looks for
`X-Hexalith-Retry-Transport`.

**Incompatible artifact.** An over-boundary inline upload returns 413 and the SDK never substitutes the streaming
transport — D-9's entire "unimodal DX for SDK consumers" rationale fails silently, and the caller sees a hard 413. If
Unit A wins instead, `X-Hexalith-Retry-As` carries `[caller, operator]` on the request and `stream` on the response, the
exact round-trip echo conflict `:883` was written to prevent.

**Tightening.** Correct D-9 at `:627` to `X-Hexalith-Retry-Transport: stream`, and correct `epics.md:386` in the same
commit so the acceptance authority and the mechanism authority agree with the spine.

---

### ADV-7 — Three "current deployed behaviour" anchors are false at HEAD, and two rank-30 stories will encode opposite deployed contracts from them

**Severity:** high (two stories build mutually exclusive deployed behaviour, each citing the architecture)

**What the document asserts as present-tense fact:**

- `:181` — "the deployed `Hexalith.Folders.Server` composition does **not** register an EventStore-backed bridge read model: `AddFoldersContextSearchFacade` leaves the fail-safe `UnavailableSemanticIndexingBridgeReadModel` default in place (`FoldersServerServiceCollectionExtensions.cs:84-86`) … **Deployed effect:** context-search returns `Allowed` with **zero items** … and indexing-status returns `ReadModelUnavailable`"
- `:148`–`:151` — the same limitation, "until the Server-side EventStore-backed read model is wired under Epic 10 Story 10.7 and the live round trip is proven under Story 10.8"
- `:221` (rank 30) — "10.8 follows 12.1–12.3, 12.5, completed 10.7, the Story 11.15 DCP lane, and the corrected authorization projection"

**Verified reality.** `sprint-status.yaml:203`–`:204`: `10-7-…: done`, `10-8-…: done`.
`src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:129-134` now does
`services.RemoveAll<ISemanticIndexingBridgeReadModel>()` and registers `EventStoreSemanticIndexingBridgeStore`. The
`Unavailable` default is gone. Meanwhile 12.1, 12.2, 12.5, and 11.15 are all `backlog`
(`sprint-status.yaml:233`, `:234`, `:223`), so the rank-30 prerequisite chain for an already-`done` story is unsatisfiable.

**Unit A — Epic 6 Story 6.14 (`6-14-prove-populated-deployed-host-diagnostic-and-transition-evid`, `sprint-status.yaml:157`).**
Reads `:181`/`:190` and builds deployed-host console journeys that assert `ReadModelUnavailable` / safe-empty as the
correct deployed indexing-status contract.
**Unit B — Story 10.8 (done).** Asserts "the first search/status response is non-empty and tenant-correct"
(`epics.md:2323`).

**Incompatible artifact.** Two deployed-behaviour contracts for one endpoint, both sourced from a declared authority —
and a fresh implementer of Story 12.5 reading `:181` has explicit documentary licence to re-register the `Unavailable`
default. The schedule half is equally unusable: the wave table is described at `:210` as "the only machine-checkable copy
of the schedule" while it schedules a completed story behind four backlog ones.

**Tightening.** Rewrite `:148`–`:151`, `:181`, and `:190` to past tense with the landing evidence, or replace the
present-tense code assertions with a pointer to the live registration so the document stops carrying a code claim it
cannot keep fresh. Reconcile the rank-30 row for 10.8 (or apply the `:212` escape hatch — "unless the referenced item is
already in a terminal accepted state" — explicitly to it).

---

### ADV-8 — The semantic-indexing bridge projection has two writers and a determinism requirement only one of them can meet; a replay silently empties every search result

**Severity:** high (durable read-model corruption on rebuild; FR58 round trip flips to empty with no failure signal)

**The rules being obeyed:**

- `:138` — "a Folders-owned bridge projection to track `file version -> Memories search-index entry/status`. This projection answers whether a file version is **indexed, stale, skipped, failed, tombstoned, or reconciliation-required**."
- `:111` (concern #9) — "rebuilding views from an empty read model produces equivalent state from the same ordered event stream; **determinism scope explicitly excludes fields derived from external clocks**" — external *clocks* only; external *delivery outcomes* are not carved out.
- `:263` — Story 12.5 owns "At-least-once Memories egress + reconciler (commit-then-append ordering)".
- `epics.md:2308`–`:2309` (Story 10.7 AC) — the bridge projection is "registered in `AddFoldersContextSearchFacade`, restarted, and **replayed from an empty checkpoint**" and "durably populates version/status/removal records".
- `:178` — the facade hydrates from the bridge, "dropping any hit without a current entry or **not in a live indexed state**".

**Verified.** `SemanticIndexingBridgeStatus` carries `Indexed`, `Stale`, `Skipped`, `Failed`, `Tombstoned`,
`ReconciliationRequired` (`src/Hexalith.Folders/Projections/SemanticIndexing/SemanticIndexingBridgeStatus.cs:9-16`).
`Indexed` and `Failed` are Memories *delivery* outcomes; no Folders domain event carries them.
`EventStoreSemanticIndexingBridgeStore` implements **both** `ISemanticIndexingBridgeReadModel` and
`ISemanticIndexingBridgeWriter` (`EventStoreSemanticIndexingBridgeStore.cs:17`), and the Workers host registers the
writer (`FoldersWorkersModule.cs:73-76`) which `SemanticIndexingProcessManager.cs:145` drives via
`RecordRemovalEvidenceAsync`.

**Unit A — Story 10.7's projection.** Rebuilds every entry from the ordered folder event stream, because its own AC
demands empty-checkpoint replay equivalence. Replay can only produce pre-delivery status values.
**Unit B — Story 12.5's egress reconciler** (and today's `SemanticIndexingProcessManager`). Writes `Indexed` / `Failed`
out of band on publish acknowledgement, because that is the only place the outcome exists.

**Incompatible artifact.** Both write the same tenant/folder/file-version key in the same store, with no declared
arbitration. After any projection rebuild — the operation NFR52 and Story 10.7's AC both require — Unit A resets every
entry out of `Indexed`, the facade's `:178` hydration drops every surviving hit, and authorized context-search returns
zero items while reporting `Allowed`. That is the exact fail-safe-masquerading-as-capability failure mode the
planning-consistency invariant at `:253` exists to prevent, and nothing detects it: the response is a well-formed empty
result.

**Tightening.** Name one writer and carve the egress fields out of determinism explicitly: *"The bridge projection has
exactly one writer. Event-derived fields (version identity, removal/tombstone state, policy outcome) are rebuilt on
replay; delivery-outcome fields (`indexed`, `failed`, attempt counters) are egress evidence owned by the Story 12.5
reconciler, are declared non-deterministic alongside the external-clock carve-out in concern #9, and are re-established
by re-driving the egress reconciler after a rebuild — never by the projection and never reset to a pre-delivery value
that would drop a live unit from hydration."*

---

## MEDIUM

### ADV-9 — `FolderWorkspaceDirtyResolution` cannot express the four guards `:436` assigns to it, and extending it is an undeclared wire change

`:436` names the third switch input concretely: "a switch expression over **`(currentState, eventType, resolution)`** …
where `resolution` is the `FolderWorkspaceDirtyResolution` discriminator (per Step 5 §'Process Patterns')".

**Verified.** `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceDirtyResolution.cs:8-12` declares exactly two
members, `CommitConfirmed` and `CommitRejected`, each with a `JsonStringEnumMemberName` — it is a serialised enum, and
`FolderStateTransitions.Transition(...)` takes it as `FolderWorkspaceDirtyResolution? dirtyResolution = null`
(`FolderStateTransitions.cs:47-51`). The four guards `:436` assigns to it are four orthogonal dimensions: commit-failure
class, staged-content presence, lock-requester identity, and clean-vs-staged.

**Unit A — Story 4.19.** Extends the flat enum to roughly eight members mixing the four dimensions. Being serialised,
this changes the wire surface with no declared regeneration duty — contrast `:649`, which declares the spine obligation
for `LockLeaseBecameStale` and for nothing else. The flat enum also admits meaningless triples such as
`(changes_staged, CommitFailed, OriginatingTask)` that have no defined outcome.
**Unit B — Story 4.21.** Introduces a separate closed union for the new guards and leaves the two-member enum alone.

**Incompatible artifact.** The `:437` gate ("every guard branch needs its own asserted outcome") has two different
denominators — a flat cross-product under A, a per-dimension branch set under B — so "CI fails if a guard branch is added
without test coverage" is not computable from the document.

**Tightening.** Name a purpose-built closed union (for example
`FolderWorkspaceTransitionGuard ∈ { None, DirtyResolution(CommitConfirmed|CommitRejected),
CommitFailureClass(NonRetryable|RetryableNoConfirmedRemoteEffect), StagedContent(Present|Absent),
LockRequester(OriginatingTask|OtherPrincipal) }`), state that it is domain-internal and not a wire type, and state which
`(state, event)` pairs accept which guard family.

---

### ADV-10 — The operator disposition for `unknown_provider_outcome` differs between the architecture table and the co-normative C6 mapping artifact, and the console is generated from the losing one

`:373` and PD11 rule 5 (`:426`): `unknown_provider_outcome` is **`auto-recovering`**; "Operator disposition becomes
`awaiting-human` at `reconciliation_required`, never before."
`docs/exit-criteria/c6-transition-matrix-mapping.md:30`: `unknown_provider_outcome` | **`awaiting-human`** | "approved" |
review date **2026-05-11** — the `dirty` row above it was refreshed to 2026-09-15, this one was not.

`:438` says `DispositionLabelMapper.cs` is "generated from" the architecture table; the mapping artifact says its rows are
consumed by "Story 6.3 disposition labels".

**Unit A — Epic 6 disposition-label story** generating from the mapping artifact → renders `awaiting-human`.
**Unit B — Epic 4 read model** per `:373` → emits `operatorDisposition: auto-recovering`.

**Incompatible artifact.** The console's primary visual (F-4, `:720`) and the wire field disagree for the same workspace,
violating the "six independent dimensions … never conflated" rule at `:376`. Worse operationally: `:376` requires the
`unknown_provider_outcome` view to show automatic reconciliation progress *and* to keep retry/takeover controls absent —
so Unit A tells an operator to intervene while offering nothing to act on, at 3 a.m., which is precisely the cognition
failure F-4 exists to prevent. Same shape as the prior run's F4, at a row the PD11 pass did not touch.

**Tightening.** Update `docs/exit-criteria/c6-transition-matrix-mapping.md:30` to `auto-recovering` with the 2026-09-15
review date, and state once that the disposition mapping has a single source (the `:362` catalog) which the mapping
artifact mirrors.

---

### ADV-11 — The `LockLeaseBecameStale` lockstep rule is already broken in the committed tree, and the gates form a doc-to-doc loop that stays green while doc-to-code drift persists

PD11 rule 7 (`:428`) requires "the enum, `FolderStateTransitions.cs`, the lifecycle tests,
`docs/exit-criteria/c6-transition-matrix-mapping.md`, and the published spine enum to move in the **same commit**".

**Verified at HEAD.** `docs/exit-criteria/c6-transition-matrix-mapping.md:36` lists `LockLeaseBecameStale` in the event
vocabulary and `docs/diagrams/workspace-lifecycle.md:72` draws `dirty --> ready : LockLeaseBecameStale` — both committed.
`src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceLifecycleEvent.cs` does **not** declare it, and
`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs:159` pins `EventVocabulary` to a
hard-coded 23-name list that does not contain it. Meanwhile
`tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs:539`
(`WorkspaceLifecycleDiagramEventLabelsEqualC6EventVocabulary`) compares the *diagram* to the *C6 document* — two
artifacts that were updated together — so it is green.

**Unit A — a story that adds the enum member** reddens the hard-coded 23-name pin at
`FolderStateTransitionsTests.cs:159` and must flip it.
**Unit B — a story that does not** leaves three documents and one diagram asserting an event the wire vocabulary does not
have, with every gate green.

**Incompatible artifact.** The "one model" invariant at `:420` is violated right now, and the only gate that spans the
doc/code boundary is a hand-maintained string list rather than a derivation. `:428` states "the gate will not flag the gap
for you" but does not name `StateCatalogAndEventVocabularyShouldMatchC6MappingDocument`, the hard-coded pin, or the
doc-to-doc conformance test that hides the drift.

**Tightening.** State in rule 7 that the vocabulary gate must be derived (`EventVocabulary` ↔ parsed C6 document ↔ spine
enum), not pinned to a literal list, and record that the documents have already moved ahead of the enum so the owning
story starts from the real delta.

---

### ADV-12 — Hard-coded surface denominators survive in the same sentences that forbid them, and both are now factually wrong

`:704`: "the authoritative `kind` vocabulary is the full `CanonicalErrorCategory` enum (**43** post-SDK members) as
published in `tests/fixtures/parity-contract.yaml` … **Surface denominators (operation counts, parity-oracle cells, C13
inventory) are always the current generated Contract Spine inventory — never hard-coded counts (2026-07-15).**"

**Verified.** `tests/fixtures/parity-contract.yaml` declares **46** distinct `canonical_error_category` values, not 43.
S-7 (`:640`) still opens "All **49** protected Contract Spine operations" — the prior run's F14, unchanged — while
`tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs:148` pins
`spine.Length.ShouldBe(49)` and PD10 itself adds operations.

**Unit A** asserts the prose denominators in a conformance test; **Unit B** generates them. Adding the incident-evidence
operation PD10 requires, or admitting `authority_unavailable` per ADV-4, makes A red and leaves the architecture the
drifting artifact. **Tightening:** delete both numerals and cite the generated inventory, consistently with the rule in
the same sentence.

---

## Prior-run verification (2026-09-15 findings)

| Prior | Status at 2026-09-16 | Evidence |
| --- | --- | --- |
| **F1** S-6 token derivation | **Closed.** `:639` now pins `HMAC-SHA-256` keyed per managed tenant, versioned classification tag ‖ canonical field identity ‖ NFC-normalized cleartext, one shared write-path implementation, and states both the dictionary-recovery and cross-tenant-oracle rationales. | `:639` |
| **F2** tokenized repository/ref vs the canonical serializing identity | **Not closed — converted to a named open item.** `:643` records the collision and an "assumption, pending confirmation" that tokenization applies to evidence surfaces only. No decision, no gate, no oracle column, no owning story (`epics.md` still has **zero** occurrences of PD8/PD10/PD11, verified). Stories 12.1 and 12.4 are the consumers and are `backlog`. | `:643`; `sprint-status.yaml:233-234` |
| **F3** double-defined `(state, event)` pairs | **Partially closed, and re-opened in four places.** See **ADV-1**, **ADV-2**, **ADV-9**. | `:358`, `:436`-`:437`, `:1065`-`:1066`, `c6-transition-matrix-mapping.md:14,41` |
| **F4** `changes_staged` + `LockLeaseExpired` disposition | **Closed for that row** (`:402` now reads "Operator intervention required" only in the side-effect column, and `:369` makes `dirty` disposition content-dependent). The same defect survives at a different row — see **ADV-10**. | `:369`, `:402` |
| **F5** S-7 envelopes underspecified | **Not closed — restated with non-existent tokens.** See **ADV-4**. | `:640` |
| **F6** `visibility` unenumerated / `withheld` collision | **Half closed.** The `redacted`-vs-`withheld` semantic collision is resolved in prose (`:639`). The value set is now declared twice with two different closures — see **ADV-5**. | `:639`, `:640` |
| **F7** no owner for the PD10 spine correction | **Not closed — recorded as open.** `:276` states it plainly: "No story owns the PD10 spine correction yet … which is how the second, divergent shape gets built." | `:276` |
| **F8** wave table breaks its own rank rule | **Not closed — recorded as open.** `:214` enumerates the six equal-rank violations and the unranked prerequisites and proposes a reading "that needs confirmation". ADV-7 adds that the table also schedules an already-`done` story behind four `backlog` ones. | `:214`, `:221` |
| **F9** C3 window unreachable | **Half closed.** `:432` records the reachability question for Legal/Product/Security. The second, sharper half — two different cleanup *triggers* in the same document — was not addressed; see **ADV-3**. | `:430`, `:432`, `:983` |
| **F10** no owner for retryable-vs-unknown classification | **Half closed.** `:436` now names an owner ("the shared provider-outcome classifier, not re-decided per adapter — the GitHub and Forgejo adapters must not disagree"). The mapping table is still absent and the safety inversion stands: concern #5 (`:107`) still lists `timeout` among **known** failures while `:399` invites a timeout to be classified "retryable, no confirmed remote effect". | `:107`, `:399`, `:436` |
| **F11** "the originating task" not identifiable | **Not closed — promoted.** See **ADV-2**. | `:404`, `:436`, `:677` |
| **F12** `LockLeaseBecameStale` prose-only | **Partially closed.** `:428` and `:649` now state the aggregate-gate and wire obligations. The lockstep rule is already broken in the committed tree and the gates form a doc-to-doc loop — see **ADV-11**. | `:428`, `:649` |
| **F13** derived-scope shape unspecified | **Not closed.** S-8 (`:641`) still specifies only that scope dimensions are "derived from an already-authorized folder, task, or binding"; no named Contract Spine type, no per-dimension sensitivity tier. | `:641` |
| **F14** hard-coded "49 protected operations" | **Not closed.** See **ADV-12**. | `:640`, `:704` |
| **F15** validation-vs-authority order | **Not closed.** S-4's PD10 clause (`:637`) still names only authority-unavailability → authority → resource lookup; canonical validation's position relative to authority for *protected* operations is still unstated outside the mutating-operation order at `:953`. | `:637`, `:953` |
| **F16** NFR79/NFR80 two owners, one row | **Closed in prose.** `:274` now assigns per-row ownership explicitly ("NFR79 belongs to Story `12-1` and NFR80 to Story `12-2` … NFR83 belongs to Story `7-16`") and defers to `nfr-traceability.md` as authoritative. The mechanism-vs-evidence split for NFR76/NFR84 is handled by the same paragraph's "Story 13.2 must consume that one shape rather than build a second deny-by-default behaviour beside it". | `:274` |
| **F17** 10.9 credited with live proof | **Not closed.** `:286` still reads "Search-bridge projection, Server registration, authorization, hydration, pruning, live search/status proof (**Stories 10.7–10.9**)" while `:225` narrows 10.9 to the metadata-only safety guard. | `:225`, `:286` |
| **F18** "release reasons … such as `caller_completed`" | **Not closed.** `:640` is unchanged: "MVP release reasons permit only approved values such as `caller_completed`; reserved post-MVP reasons are rejected" — no field named, no closed set. | `:640` |

---

## Summary table

| ID | Severity | One-line |
| --- | --- | --- |
| ADV-1 | critical | PD11's `(state, event, guard)` keying lives in one paragraph and is contradicted by `:358`, `:1065`, `:1066`, and the co-normative C6 mapping artifact; the live pair-keyed gate passes the implementation that silently destroys staged work |
| ADV-2 | critical | `dirty` + `WorkspaceLocked` declares a guard with one branch and a "never from caller input" rule no declared durable field can satisfy — one reading bricks the workspace forever, the other lets any principal resume and commit another task's staged changes |
| ADV-3 | critical | `:430` and `:983` give cleanup two mutually exclusive triggers (task closure vs workspace state), so one unit deletes staged content seven days after `inaccessible` and the other never deletes it at all |
| ADV-4 | high | S-7's "fully named" envelopes use categories `authorization`/`availability` and code `authority_unavailable` that are not in the 46-member canonical vocabulary the exit-code table and MCP kind set are keyed on — CLI says exit 1 non-retryable, MCP says retryable |
| ADV-5 | high | `visibility` is declared closed at three values in S-7 and at five in S-6 and the shipped spine, on a field A-8 requires on every error |
| ADV-6 | high | D-9 names the 413 header `x-hexalith-retry-as: stream` while `:882`-`:883` reserve that name for a request-side header and call the collision out by name; `epics.md:386` encodes D-9's spelling, the shipped code uses the other |
| ADV-7 | high | `:148`-`:151`, `:181`, `:190` still assert the `Unavailable` bridge default as deployed fact after Stories 10.7/10.8 landed, and rank 30 schedules a `done` story behind four `backlog` ones |
| ADV-8 | high | The semantic-indexing bridge has two writers (10.7 replay projection, 12.5 egress reconciler) with no arbitration, and a replay resets every `Indexed` entry so authorized search returns a well-formed empty result |
| ADV-9 | medium | `FolderWorkspaceDirtyResolution` has two members and cannot express the four orthogonal guards `:436` assigns to it; extending it is an undeclared wire change |
| ADV-10 | medium | `unknown_provider_outcome` is `auto-recovering` in the architecture and `awaiting-human` in the co-normative C6 mapping artifact that the console labels are generated from |
| ADV-11 | medium | `LockLeaseBecameStale` is in the C6 document and the diagram but not the enum; the only spanning gate is a hard-coded 23-name list and the live conformance test compares doc to doc |
| ADV-12 | medium | "43 post-SDK members" (actually 46) and "All 49 protected operations" survive in the sentences that forbid hard-coded denominators |

## Recommendation

Do not accept this document as mechanism authority until **ADV-1, ADV-2, and ADV-3** are closed with explicit decision
text. All three are cases where the *fix applied on 2026-09-15 landed in one paragraph and left its contradiction
standing elsewhere in the same document* — which is the most dangerous failure mode for a spine, because the reviewer
who checked the amended paragraph reasonably believes the item is closed. Each of the three lets a competent implementer
destroy or misappropriate durable user content while obeying the majority of the normative text.

**ADV-4, ADV-5, and ADV-6** should be closed in the same pass: they are the cross-surface contracts the Epic 5 adapter
stories and the unowned PD10 spine regeneration will consume, and two of them (ADV-4, ADV-5) are regressions introduced
by the previous round's fixes rather than pre-existing gaps.

**ADV-7 and ADV-11** are a different class: the document has begun carrying present-tense assertions about code that the
code has since moved past. Every such assertion is a divergence generator with a delay fuse. Consider replacing the
inline `file.cs:line` claims with a single pointer plus a conformance test, so the document cannot silently go stale
against the tree again.
