# Adversarial Spine Review — architecture.md amendment 2026-09-15

- **Reviewer lens:** adversarial ("construct two units one level down that each obey every AD to the letter yet still build incompatibly")
- **Target:** `_bmad-output/planning-artifacts/architecture.md` (working-tree amendment applying §5.2 of the approved `sprint-change-proposal-2026-09-15.md`)
- **Baseline compared:** `git diff -- _bmad-output/planning-artifacts/architecture.md` against `HEAD` (`1621358`)
- **Date:** 2026-09-15
- **Verdict:** **FAIL** — 3 critical, 6 high, 7 medium, 2 low. Three of the newly added decisions (S-6 token derivation, S-6 vs. the canonical serializing identity, the PD11 double-defined `(state, event)` pairs) cannot be implemented twice and arrive at the same system.

## Method

For each newly added or amended decision I constructed two concrete implementation units one level down —
two stories, two surfaces (REST / CLI / MCP / SDK / UI / worker), or two provider adapters — each reading only
this document, and looked for a pair that both satisfy the text and still produce incompatible artifacts.
Every finding below names the two units, quotes the clause each one obeys, names the incompatible artifact,
and proposes exact replacement or additional decision text. Claims about the current codebase were verified
against the working tree (file and line references are given).

---

## CRITICAL

### F1 — S-6 fixes no derivation for the correlation token; two writers legitimately emit different tokens, and an unkeyed token leaks the value and correlates across tenants

**Decision under attack:** S-6 (`architecture.md:622`), amended by PD8 / A5.

**What the document says.** "Per-tenant `confidential` override is applied at event-write time by substituting a
correlation token for the value… The token is a stable, non-reversible reference that correlates the same
confidential value across records for operator triage and audit joins; it carries no recoverable payload and no
key exists to restore one."

**Unit A — Epic 12 Story 12.1 event-write path.** Writes folder lifecycle events. Implements the override as
`token = Base64Url(SHA-256(value))[..22]`. Stable ✓, non-reversible (one-way function) ✓, correlates the same
value across records ✓, no key exists ✓.

**Unit B — Epic 12 Story 12.5 / Epic 10 Memories egress projection writer.** Emits `SearchIndexEntryChanged`
with the same branch name. Implements the override as `token = "ct1:" + Hex(SHA-256(tenantId + "|" + fieldName
+ "|" + value))`. Same four properties hold, literally.

**Incompatible artifact.** One confidential branch name now has two tokens in the durable record set. The
*only* stated purpose of the token — "operator triage and audit joins" — silently returns an empty join.
Nothing detects it: there is no gate, no fixture, and no parity-oracle column for the token.

This is not hypothetical multiplicity. The document itself records that four independent sensitive-value
filter implementations exist today and must be converged (Epic 13, HXF-SEC-005/006, `architecture.md:257`), and
`src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs` contains no hashing primitive at all, so every
writer must invent one.

**Two further defects in the same clause.**

1. *"Non-reversible" is false for this data domain.* The classified values are branch names, repository names,
   and paths (S-6 default tier). An unkeyed digest over `main`, `develop`, `release/2026.09`, or a customer's
   repository name is recovered by dictionary in milliseconds by any holder of a backup, export, or incident
   dump — the exact readers S-6's own rationale enumerates as the reason to stop durable cleartext. Unit A
   above is therefore a full re-creation of the disclosure path the amendment exists to close, while obeying
   every word of it.
2. *A tenant-independent derivation is a cross-tenant oracle.* Under Unit A, equal tokens in two tenants prove
   that two tenants use the same repository or branch name. That is cross-tenant information flow through a
   durable field, contradicting cross-cutting concern #1 (`architecture.md:103`) and the tenant-prefix
   invariant of concern #13.

**Contrast with A-9, which does this correctly.** A-9 (`architecture.md:641`) pins "a versioned HMAC-SHA-256
opaque-key digest and collision-verification tag" partitioned by managed tenant. S-6 pins none of: algorithm,
keyed vs. unkeyed, key custody, key scope, tenant partitioning, version tag, output length/encoding, collision
handling, or the rotation-versus-stability rule. S-6 is strictly weaker than A-9 on data that is more sensitive
than an idempotency key.

**Proposed tightening — replace the token sentence in S-6 with:**

> The token is produced by exactly one shared primitive, `Hexalith.Folders/Observability/ConfidentialCorrelationToken`,
> and never by a per-writer implementation. Derivation is `token = "ct" + <keyVersion> + ":" +
> Base64Url(HMAC-SHA-256(key = confidentialCorrelationKey(managedTenantId, keyVersion), message =
> canonicalFieldName + 0x1F + normalizedValue))[..27]`. The key is a per-managed-tenant secret held in the S-5
> credential tier, never in an event, projection, configuration file, or log; it is a correlation key, not an
> encryption key, and no operation recovers a value from a token. Tokens are stable within a key version and
> are guaranteed distinct across managed tenants; joins are scoped to one tenant and one key version, and a key
> rotation issues a new `keyVersion` rather than re-tokenizing history. `keyVersion` is durable on every record
> that carries a token. Every writer that can persist a classified field — aggregate event writer, projection
> writer, audit sanitizer, egress publisher, diagnostics builder — calls this primitive; a second derivation
> anywhere in the solution is a defect. The C9 gate asserts token equality across at least two different
> writers for one value, token inequality for the same value across two tenants, and absence of the cleartext
> in every durable channel.

---

### F2 — Tokenizing repository and ref breaks the canonical serializing identity and the Git write path; the two units diverge on what identity to lock and what ref to commit to

**Decisions in collision:** S-6 (`:622`), cross-cutting concern #4 (`:106`), D-8 (`:609`), Epic 12 Story 12.4
(`:248`–`:252`), NFR79 (restart survival, `:259`).

**What the document says.** S-6: confidential values are "never made durable — not in an event, projection,
audit record, log, trace, diagnostic, or generated artifact", and "no key exists to restore" the value.
Concern #4: the single active mutation writer is serialized on "**managed tenant + canonical provider/repository
identity + normalized target ref**". Story 12.4 must perform "a real Git commit" after restart-survivable
state.

**Unit A — Story 12.4 commit executor.** After a process restart, it rebuilds workspace state from durable
events (12.1) and must push to the target ref. Under a `confidential` override, the durable events hold only
tokens, and no key restores the value, so the executor cannot name the branch. Unit A therefore reads the
cleartext from the live request and refuses to operate without one — which makes every request-less path
(reconciler, process manager, retry after restart) unable to complete a commit for exactly the tenants that
chose the strictest policy. NFR79 ("accepted mutations… must survive process restart") fails by construction
for those tenants, and nothing in the document warns the implementer.

**Unit B — lock manager.** Computes the concern #4 canonical serializing identity. Since it may not hold
durable cleartext either, it computes the identity over the tokens — deterministic and tenant-scoped, so a
defensible reading. Unit A, holding cleartext from the request, computes it over the normalized cleartext.

**Incompatible artifact.** One remote/ref now has two serializing identities. The single-active-writer
invariant — the mechanism that prevents two agent tasks from committing to the same branch concurrently —
silently admits two holders. This is the highest-consequence invariant in the document and the amendment
breaks it without mentioning it.

**Proposed tightening — add to S-6:**

> **Operational-value boundary.** A value that the system must itself use to produce an external effect or to
> compute the concern #4 canonical serializing identity — provider identity, repository identity, and
> normalized target ref — is not made durable in cleartext and is not replaced by a token either: it is held
> by reference in the S-5 credential/secret tier under a per-binding reference recorded on the repository
> binding, and is resolved at use time by the provider adapter and the lock manager. The canonical serializing
> identity is always computed over the resolved cleartext and stored only as its own tenant-scoped digest;
> it is never computed over a correlation token. Confidential classification governs what is *disclosed and
> persisted in the evidence plane*; it never governs what the system needs to execute, so no Epic 12 execution
> path depends on recovering a value from a token.

If that boundary is not acceptable, the alternative tightening is to state explicitly that `repository` and
`ref` are out of scope for the MVP `confidential` tier and that only paths and commit messages may be
tokenized — but the document must say one or the other, because today it says neither.

---

### F3 — PD11 creates three `(state, event)` pairs with two outcomes, while the stated implementation contract is a `(currentState, eventType)` switch and the C6 gate only requires "a defined outcome"

**Decisions under attack:** the PD11 transition rows (`:383`–`:396`), rule set (`:405`–`:413`), and the
unchanged implementation-enforcement bullets (`:419`–`:420`).

**The double-defined pairs introduced or left by this amendment:**

| Pair | Row 1 | Row 2 | Discriminator |
| --- | --- | --- | --- |
| `changes_staged` + `CommitFailed` | → `failed` (`:383`) | → `dirty` (`:384`) | "known non-retryable" vs "retryable, no confirmed remote effect" |
| `inaccessible` + `ProviderReadinessValidated` | → `dirty` (`:395`) | → `ready` (`:396`) | staged content remaining within the C3 window |
| `dirty` + `WorkspaceLocked` | → `changes_staged` (`:389`) | (undefined) | "by **the originating task**", "still holds staged changes" |

**Unit A — aggregate implementer.** Obeys `:419` verbatim: "implements this matrix as a switch expression over
`(currentState, eventType)` returning `DomainResult`". With only two inputs the first matching row wins, so
every `CommitFailed` reaches `failed` and every `ProviderReadinessValidated` reaches `ready`. That is a literal
implementation of the document that silently destroys staged work — precisely what PD11 rule 1 forbids in the
same section.

**Unit B — lifecycle-test author.** Obeys the table rows and asserts the `dirty` branches with a payload
qualifier.

**Incompatible artifact.** A and B cannot both pass. Worse, the CI gate does not arbitrate: `:420` requires
only that each `(state, event)` pair have "a defined outcome", which Unit A satisfies. The gate is blind to
the ambiguity the amendment introduced.

**The qualifier dimension exists in code but not in the document.**
`src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:47-51` already takes a third parameter,
`FolderWorkspaceDirtyResolution? dirtyResolution`, used for exactly one pair
(`UnknownProviderOutcome` + `ReconciliationCompletedDirty`). The document never mentions it, so Unit A and
Unit B will invent two different qualifier enums for the three new discriminators, and the C6 mapping artifact
`docs/exit-criteria/c6-transition-matrix-mapping.md` will express a third.

Note also that today's code implements `Dirty + OperatorDiscardRequested → Failed`,
`Failed + OperatorRetrySucceeded → Ready`, and `ReconciliationRequired + OperatorMarkedFailed → Failed` as
*allowed* transitions (`FolderStateTransitions.cs:100-121`), which PD11 rule 6 now declares defects. That part
is an honest to-do, but it means the document and the code disagree the moment this amendment lands, with no
named story to close it (see F7).

**Proposed tightening — replace `:419` and add rule 8:**

> - `Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` implements this matrix as a switch
>   expression over `(currentState, eventType, qualifier)` returning `DomainResult`, where `qualifier` is the
>   closed union `FolderWorkspaceTransitionQualifier ∈ { None, DirtyResolution(CommitConfirmed|CommitRejected),
>   CommitFailureClass(NonRetryable|RetryableNoConfirmedRemoteEffect), StagedContent(Present|Absent),
>   LockRequester(OriginatingTask|OtherTask) }`. The qualifier is derived by the domain from the event payload,
>   never supplied by a caller.
>
> 8. **A pair is ambiguous until it declares its discriminator.** Any `(state, event)` pair with more than one
>    row in this table MUST name the qualifier that selects between them, and the C6 aggregate gate MUST assert
>    every `(state, event, qualifier)` triple rather than every `(state, event)` pair. A pair with two rows and
>    no declared qualifier fails CI. `dirty` + `WorkspaceLocked` by a task other than the originating task, and
>    `dirty` + `WorkspaceLocked` on a clean dirty workspace, are explicitly rejected with
>    `state_transition_invalid` (they are not "unlisted"; the rejection is a declared outcome).

---

## HIGH

### F4 — The surviving `changes_staged` + `LockLeaseExpired` row contradicts the new `dirty` disposition and PD11 rule 2

`:387` still reads "`changes_staged` → `dirty` | `LockLeaseExpired` (mutations applied, lock lost) | **Operator
intervention required**", while the amended state catalog (`:354`) says `dirty` is `degraded-but-serving`
"while the originating task can still resume", and rule 2 (`:408`) says the originating task *can* resume from
exactly this state.

**Unit A — UI `DispositionLabelMapper`.** `:421` requires it to be "sourced from this table"; the table's side
effect for this row says operator intervention, so it renders `awaiting-human`.
**Unit B — read-model / API implementer.** Reads the state catalog and emits `operatorDisposition:
degraded-but-serving`.

**Incompatible artifact.** The console's primary visual (F-4) and the wire `operatorDisposition` field disagree
for the same workspace, and the "six independent dimensions, never conflated" rule (`:361`) is violated by the
surface that was supposed to enforce it. The two `changes_staged → dirty` rows also carry contradictory side
effects for the same destination state, which is the shape of a defect, not a nuance.

**Tightening.** Replace `:387`'s side effect with "Lock orphaned with staged changes; the originating task may
resume via `dirty` → `changes_staged`; no silent retry", and add to the state catalog: "operator disposition
for `dirty` is emitted by the aggregate as a function of `(stagedContent, originatingTaskResumable)` and is
never re-derived per surface."

---

### F5 — The two S-7 envelopes are not fully named here: `resource_unavailable` has no category, the 503 has no category / code / `retryable` / `clientAction`, and `cli_exit_code` + `mcp_failure_kind` are therefore not derivable from this document

**Decision under attack:** S-7 (`:624`), A-8 (`:640`), against the C13 columns (`:310`) and the canonical
mappings (`:660`–`:686`).

S-7 says post-authorization denials return "one HTTP 404 `tenant_access_denied` / `resource_unavailable`
shape", and authority problems return "one non-disclosing HTTP 503 envelope carrying `details.visibility:
redacted`". That is the entire specification. Verified facts:

- In `tests/fixtures/parity-contract.yaml`, `resource_unavailable` is **not** a `canonical_error_category`;
  it exists only as a *code*. `tenant_access_denied` is a category (`cli_exit_code: 66`).
- The 503's category and code appear nowhere in `architecture.md`. They exist only in
  `docs/contract/authorization-matrix.md:146` (category `read_model_unavailable`, code
  `projection_unavailable`, `retryable: true`, client action `retry`) — an artifact this same amendment
  declares superseded and forbids using as release evidence (`:627`, `:50`).
- `projection_unavailable` is *also* a `canonical_error_category` in the oracle with `cli_exit_code: 72`, so
  the same token is a category in one place and a code in another.

**Unit A — CLI adapter.** Maps by canonical category: 404 → `tenant_access_denied` → exit 66; 503 → category
unknown from this document → falls through to `internal_error` → exit 1, `retryable` false.
**Unit B — MCP adapter.** Reads the slash in "`tenant_access_denied` / `resource_unavailable`" as two
alternative categories and emits `kind: "resource_unavailable"` — a kind outside the published enum — and for
the 503 emits `kind: "read_model_unavailable"` after finding the matrix.

**Incompatible artifact.** The C13 cross-adapter invariant ("same input → identical category, code, retryable,
clientAction", `:684`) fails. Operationally: a transient authority outage is `retryable: true / retry` on REST
and a non-retryable exit 1 on CLI, so automation stops retrying a recoverable outage — or, in the mirror case,
retries a permanent denial forever. The lens question "is the 503 retryable?" is unanswerable from this
document, and it is the one field that changes caller behaviour.

**Also unresolved:** whether `not_found`, `cross_tenant_access_denied`, and `audit_access_denied` are removed
from the canonical category enum or only from protected-operation *responses*. The oracle still maps all three
(exit 73 / 66 / 66 and matching kinds). One implementer deletes the enum members (a schema-breaking change for
MCP clients and a semantic reuse hazard for exit 73); another keeps them as unreachable members. Both obey
S-7.

**Proposed tightening — add an envelope table directly to S-7:**

> | Envelope | Status | Category | Code | `retryable` | `clientAction` | `details.visibility` | CLI exit | MCP kind |
> | --- | --- | --- | --- | --- | --- | --- | --- | --- |
> | Safe denial (all post-authorization denials) | 404 | `tenant_access_denied` | `resource_unavailable` | `false` | `no_action` | `redacted` | 66 | `tenant_access_denied` |
> | Authority unavailable | 503 | `read_model_unavailable` | `projection_unavailable` | `true` | `retry` | `redacted` | 72 | `read_model_unavailable` |
>
> Only `correlationId` and the per-request instance identifier may differ between two safe-denial responses.
> `not_found`, `cross_tenant_access_denied`, and `audit_access_denied` remain **reserved but never emitted**
> members of the canonical category vocabulary for the `previous-spine.yaml` deprecation window; their CLI exit
> codes and MCP kinds are reserved and must not be reassigned. Unprotected operations (health, readiness,
> version, discovery) are outside S-7 and declare their own responses; they must never accept or echo a
> resource identifier.

*(Also fixes the lens's unprotected-operation question: the document currently governs only the "49 protected"
operations and says nothing about the rest — see F14 for the count itself.)*

---

### F6 — `visibility` is required on every error but its value set is unenumerated, and S-6's new `withheld` state collides with the shipped definition of `redacted`

**Decisions under attack:** A-8 (`:640`, "`visibility` is a required field on every error"), S-7 (`:624`),
S-6 (`:622`, "render a confidential field as a **withheld** state carrying its token, kept visibly distinct
from **redacted**, **unavailable**, and **absent**").

Verified facts:

- The Contract Spine uses exactly four values today: `metadata_only`, `redacted`, `unavailable`, `absent`
  (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`). `withheld` appears nowhere in the spine,
  the client, or the oracle.
- `docs/contract/safety-invariant-ci-gates.md:59` defines `redacted` as "a value exists for an authorized
  audience but is deliberately **withheld**", and `src/Hexalith.Folders.UI/Services/ConsoleStatusText.cs:45`
  renders `redacted` as "The requested evidence is **withheld** by tenant policy." The two words are synonyms
  in the shipped system; S-6 now requires them to be visibly distinct states.
- `tests/fixtures/parity-contract.yaml` and `parity-contract.schema.json` contain **zero** occurrences of
  `visibility`. The oracle cannot arbitrate a field it does not model.

**Unit A — UI implementer (Epic 6).** Maps a confidential field to the existing `FieldDisclosure.Redacted` /
`RedactedField.razor`, which matches the shipped copy and the gate document. The token is never rendered.
**Unit B — contracts implementer.** Adds `withheld` as a fifth `visibility` value and emits it with the token.

**Incompatible artifact.** The API emits `withheld` + token; the console has no branch for it and either
renders the raw enum or falls back to "unknown" — which is the exact operator-confusion failure mode
cross-cutting concern #11 exists to prevent. Meanwhile the safety gate's definition of `redacted` now describes
two different states, so the gate's sentinel corpus no longer partitions them.

**Tightening.** In A-8, enumerate the closed set and name its owner: "`visibility ∈ { metadata_only, redacted,
withheld, unavailable, absent }`, generated from the Contract Spine and never surface-local; `withheld` means a
confidential value was replaced by an S-6 correlation token at write time and no cleartext exists anywhere,
distinct from `redacted`, which means a value exists for an authorized audience and is not being shown here."
Add a `visibility_value_set` column to the C13 oracle and its schema, and reconcile
`docs/contract/safety-invariant-ci-gates.md:59` and `ConsoleStatusText.cs` in the same commit.

---

### F7 — No unit owns the PD10 Contract Spine correction, and Epic 13 Story 13.2 will build a second, differently-shaped deny-by-default path

**Verified facts.** `_bmad-output/planning-artifacts/epics.md` contains **zero** occurrences of "PD8", "PD10",
or "PD11". `docs/exit-criteria/nfr-traceability.md` assigns NFR76 ("deny by default when authority is absent,
stale, malformed, or unavailable") to story `13-2` and NFR84 to `13-6`. `architecture.md:261` says the opposite:
"NFR76 and NFR84 exercise the S-7 safe-denial envelopes; the contract that produces them is owned here in
§'Authentication & Security', not by Epic 13." A document section cannot produce evidence, and the only wave
that mentions PD10 is rank 0 "Authority relock" (`:216`), described as approving and recording decisions.

**Unit A — Epic 13 Story 13.2** ("Fail-safe fallback authorization policy and sidecar-only app port",
`epics.md:2731`), which owns NFR76's row. It implements a fallback authorization policy that denies when
authority is unavailable, choosing its own status — today's fallback idiom is 401/403.
**Unit B — the (unowned) spine regeneration** required by `:627`, which implements the 503
`read_model_unavailable` envelope evaluated before lookup.

**Incompatible artifact.** Two deny-by-default paths for the same condition with different statuses, and the
NFR76 release evidence points at the one that is not the contract. Because the authority-unavailable envelope
must be evaluated *before* lookup on all protected operations, whichever unit runs first in the middleware
pipeline wins, and the outcome depends on registration order rather than on a decision.

**Tightening.** Create one ranked story that owns the PD10 correction end to end — OpenAPI spine, generated
client, CLI/MCP parity fixtures, `previous-spine.yaml` deprecation entries, C13 oracle inventory,
`docs/contract/authorization-matrix.md` regeneration and A6b re-approval, published docs, and the affected
tests — place it at a rank strictly below every rank-30 consumer, and split the NFR76/NFR84 rows into
`mechanism_story` (that story) and `evidence_story` (13-2 / 13-6). Add to S-7: "Epic 13 stories consume these
envelopes; they never define an alternative denial shape, and no surface may register a fallback authorization
result outside this table."

---

### F8 — The execution-wave table violates its own validation rule in at least six places, gives no rank to five of its own prerequisites, and points at manifest fields that do not exist

**Decision under attack:** §"Release Authority Overlay — 2026-09-15" (`:197`–`:231`).

**Stated rule (`:209`):** "a prerequisite must sit at a strictly lower rank than its dependent unless the
referenced item is already in a terminal accepted state… Manifest validation rejects a cycle and rejects any
dependency on an equal or later rank."

**Intra-rank violations in the table itself:**

| Rank | Dependent | Prerequisite at the *same* rank |
| --- | --- | --- |
| 10 | 12.2, 12.3, 12.6 | 12.1 |
| 30 | 4.21 | 4.19, 4.20 |
| 30 | 6.14 | 6.12, 6.13, 4.18–4.21 |

**Unranked prerequisites:** 3.11, 3.13, 10.6, 10.7, 11.15, and **all of Epic 13** (rank 40 depends on "the Epic
13 security and capacity evidence", and Epic 13 appears nowhere in the table). Under "`execution_rank` is the
sole scheduling authority", an unranked prerequisite makes the constraint undecidable, and the escape hatch
("already in a terminal accepted state") does not apply — 11.15 is the DCP lane, which is explicitly not
complete.

**Manifest reality check.** `_bmad-output/planning-artifacts/planning-story-manifest.yaml` is unmodified in the
working tree, carries `generated_on: '2026-08-04'`, and contains **zero** occurrences of `execution_rank`,
`story_lifecycle_status`, `OQ11`, `OQ12`, or `OQ13`. It uses `lifecycle_status`, not `story_lifecycle_status`.
The architecture describes it as "(regenerated 2026-09-15)" and as the owner of fields it does not have.

**Unit A — a delivery agent scheduling from `architecture.md`.** Starts 4.19, 4.20, and 4.21 concurrently
(same rank), and 12.1/12.2/12.3 concurrently.
**Unit B — a delivery agent scheduling from the manifest** (the declared sole authority). Reads `prerequisites`
and the August-4 `lifecycle_status` and produces the pre-correction ordering.

**Incompatible artifact.** Two schedules, both sourced from a declared authority; and the document's own
self-description is contradictory — `:203` says "This document does not assign story status and does not
schedule work", immediately followed by a table that schedules work.

**Tightening.** (a) Label the table "non-normative mirror of the manifest's `execution_rank`; on divergence the
manifest wins." (b) Give every named prerequisite a rank, including Epic 13 stories (they gate rank 40).
(c) Replace same-rank edges with sub-ranks (`10.0 → 12.1`, `10.1 → 12.2/12.3/12.6`; `30.0 → 4.18/4.19/4.20`,
`30.1 → 4.21`, `30.2 → 6.14`) or move the dependents to the next wave. (d) Add an explicit precondition: "this
overlay is inert until the manifest is regenerated with `execution_rank`, `story_lifecycle_status`, and
OQ11–OQ13; until then the 2026-08-04 manifest remains the lifecycle record and no wave is schedulable."

---

### F9 — C3's new cleanup trigger makes the staged-content retention window unreachable, and PD11 rule 6 removes every MVP path to terminal closure for an orphaned staged workspace

**Decisions in collision:** "C3 is the cleanup authority" (`:415`), transition rows `:395`/`:396`, rule 3
(`:409`), rule 6 (`:412`).

`:415`: "Temporary working-file cleanup starts **only after terminal task closure with no active task** — not
on lock expiry, lease staleness, or task cancellation alone."
`:396`/rule 3: `inaccessible` → `ready` fires "with no staged content remaining (clean, **or the C3 window
elapsed**)".

**Unit A — retention implementer (C3 / D-7, Epic 12).** Starts the seven-day clock only at terminal task
closure. For a workspace that went `changes_staged` → `inaccessible` (auth revoked, repo deleted), mutation and
commit are blocked, so the task never closes; the clock never starts.
**Unit B — lifecycle implementer.** Needs "the C3 window elapsed" to select the `inaccessible` → `ready`
branch. Under Unit A that predicate is permanently false, so `:396` is dead code and staged content is retained
indefinitely.

**Compounding:** rule 6 makes `dirty` + `OperatorDiscardRequested` and `reconciliation_required` +
`OperatorMarkedFailed` reject in MVP. An orphaned `dirty` workspace with staged changes whose originating task
is gone can therefore reach only `ready` or `committed` through reconciliation — both of which assert a factual
outcome an operator may be unable to assert — and has no legal terminal state. Its temporary working files are
never cleaned.

**Incompatible artifact.** Unbounded temporary-file retention for exactly the failure cases, contradicting C3
and NFR60–NFR64, plus a matrix branch that can never fire and a gate that will nevertheless demand coverage of
it.

**Tightening.** Define terminal task closure independently of workspace state: "a task reaches terminal closure
on `TaskCompleted`, `TaskFailed`, or `TaskAbandoned`; `TaskAbandoned` is emitted by the reconciler when the
originating task's lease and authorization have both lapsed, and it is the closure that starts the C3 clock for
an orphaned workspace. A workspace transition is never itself a deletion event, and the C3 clock for staged
content in `dirty` or `inaccessible` starts at the task-closure event, not at the workspace transition."
Then either restore an MVP operator-abandon path or state that abandonment is expressed as `TaskAbandoned` plus
`ReconciliationCompletedClean` with an audit reason.

---

## MEDIUM

### F10 — "Retryable / no confirmed remote effect" has no classification owner; the GitHub and Forgejo adapters will classify the same response differently, and the safe default is inverted

**Unit A — GitHub adapter (A-6, Octokit).** Classifies HTTP 429 and a socket timeout as "retryable, no
confirmed remote effect" → `changes_staged` → `dirty`, resumable, `degraded-but-serving`.
**Unit B — Forgejo adapter (A-7, hand-written typed client).** Classifies 429 as the canonical category
`provider_rate_limited` — a *known* failure per cross-cutting concern #5's taxonomy (`:107`, which lists
"timeout / 401 / 403 / 404 / 409 / 429 / 5xx" as known failures) → `failed`, `terminal-until-intervention`;
and classifies a timeout as `ProviderOutcomeUnknown` → `unknown_provider_outcome`.

Both obey. PD11 introduces a **third** bucket without re-partitioning concern #5's taxonomy, and never names
the owner of the classification.

**Incompatible artifact.** The same incident is terminal on one provider and resumable on the other; A-7's
required "response-equivalence tests against the GitHub adapter's port shape" and C13's `terminal_states`
column both go red with no arbitration rule in the document.

**Safety inversion.** "No confirmed remote effect" is a *safety* claim about the remote side. A commit request
that times out after the server accepted it *does* have a remote effect. Classifying a timeout as
retryable-no-effect directly contradicts concern #5's rule that an unconfirmed outcome enters
`unknown_provider_outcome` "rather than retrying in a way that could duplicate repositories, file changes, or
commits". Unit A is a duplicate-commit generator that obeys the amendment.

**Tightening.** Add a classification table to concern #5 (or bind it to the OQ4 catalog's readiness-outcome
profiles) mapping every taxonomy member to exactly one of `{ known_non_retryable, retryable_no_confirmed_remote_effect,
unknown_outcome }`, owned by the provider port and identical for both adapters, with the overriding rule:
"any response to a request that could have produced a remote mutation and did not return a confirmed outcome is
`unknown_outcome`; `retryable_no_confirmed_remote_effect` applies only where the provider contract proves the
request was rejected before any mutation (for example a pre-flight rate-limit rejection with no request body
accepted)."

---

### F11 — "The originating task" is not identifiable after the lock is lost

Rule 2 and `:389` hinge on "the originating task", but concern #4 (`:106`) states that "folder/workspace/task
IDs are lock metadata, never collision identity" and that lock recovery "creates a new lock instance". Nothing
declares that the aggregate durably retains the staging task's identity across `LockLeaseExpired`, for how
long, how the claim is authorized, or what happens when that task is itself terminal or revoked.

**Unit A.** Persists `StagedByTaskId` on the aggregate and rejects `WorkspaceLocked` from any other task on a
staged `dirty` workspace — permanently, so a workspace whose originating task is dead becomes unlockable and,
per F9, uncleanable.
**Unit B.** Treats "originating" as "the task named in the lock request that matches the last `FileMutated`
correlation". Since a caller supplies the task ID, naming the right one resumes another principal's staged
work — an authority-widening path that S-8 was written to close, on the one dimension S-8 cannot derive from a
parent (the lock is gone).

**Tightening.** "The aggregate durably records `stagedByTaskId` and `stagedByPrincipal` at first mutation and
retains them for the C3 staged-content window. `dirty` + `WorkspaceLocked` → `changes_staged` requires: the
request's task ID equals `stagedByTaskId`, the task is live and bound to the same folder, and the caller holds
current folder write authority; the task ID is a derived S-8 dimension resolved from the task binding, not a
caller assertion. If the originating task is terminal or unresolvable, the pair yields
`dirty` → `reconciliation_required`."

---

### F12 — `LockLeaseBecameStale` exists only in prose; the CI gate is driven by the code's vocabulary, and the event has no defined outcome for a staged `dirty` workspace

Rule 7 (`:413`) declares the event "new… added to the architecture event vocabulary" and asserts "an event in
the vocabulary with no asserted outcome fails CI by design". Verified: the gate iterates
`FolderStateTransitions.EventVocabulary` (`src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:20-44`),
which has 23 members and no `LockLeaseBecameStale`. A document-only event therefore never fails CI — the gate
runs in the opposite direction from the claim.

Second problem: `FolderWorkspaceLifecycleEvent` is a **wire** enum published through the generated client
(`src/Hexalith.Folders.Client/Generated/HexalithFoldersClient.g.cs:14645`, `LockLeaseExpired = 13`), so adding a
member is a Contract Spine change. PD10 enumerates its regeneration duties in detail (`:627`); PD11 names none,
so two units in the same amendment carry contradictory regeneration obligations.

Third problem: rule 7 says this is "the only event that moves a lock from `expired` to `stale`", but the only
row consuming it requires a **clean** `dirty` workspace. A *staged* `dirty` workspace therefore either never
receives it — leaving its lock pinned at `expired` past C7's 60-second threshold — or receives it and is
rejected with `state_transition_invalid`, i.e. the lock-state dimension is blocked by a workspace-lifecycle
rule, contradicting the "six independent dimensions… never conflated" rule at `:361`.

**Tightening.** Add the row `dirty`(staged) + `LockLeaseBecameStale` → `dirty` with the side effect "lock state
moves `expired` → `stale`; workspace lifecycle unchanged; staged content preserved"; state PD11's spine
obligation ("adding `LockLeaseBecameStale` to `FolderWorkspaceLifecycleEvent` regenerates the spine, the client
enum, and the C13 `terminal_states`/lifecycle columns in the same commit"); and change `:420` to "every event in
the **union** of this document's vocabulary and `FolderStateTransitions.EventVocabulary`; an event present in
one and absent from the other fails CI."

---

### F13 — S-8 prohibits caller-supplied scope but never specifies the derived-scope shape, so two owners emit incompatible scope structures

S-8 (`:625`) says provider, repository, ref, and task "are derived from an already-authorized folder, task, or
binding", and nothing more. Meanwhile `docs/contract/authorization-matrix.md` pins audit evidence to exactly six
fields — `actor, tenant, operation, operation_family, result, correlation_id` — containing **no** scope
dimension at all.

**Unit A — Epic 4 transition-evidence writer.** Emits `scope: { tenantId, folderId, provider, repository, ref }`
as a structured object.
**Unit B — Epic 6 diagnostics writer.** Emits `scope: "folder:{id}"` and relies on the matrix's six-field
record for everything else.

Both "derive from an already-authorized folder". **Incompatible artifact:** two scope shapes in one evidence
corpus; C13's `audit_metadata_keys` column cannot arbitrate because it compares *surfaces of one operation*, not
*owners of one concept*. And because the derived `repository` and `ref` dimensions are exactly the S-6
confidential candidates, Unit A writes cleartext scope into audit while Unit B (per F1) writes tokens.

**Tightening.** "The derived scope is a named Contract Spine type `AuthorizationScope { managedTenantId,
folderId, taskId?, providerRef?, repositoryRef?, normalizedRef? }`, generated, never hand-built, with a declared
S-6 sensitivity tier per dimension. Every audit, diagnostic, and denial record carries the same instance; the
canonical six-field audit record of the authorization matrix is extended with `scope` as a seventh field, or
the scope is carried in the transition-evidence record only — the document must state which, once."

---

### F14 — S-7 hard-codes "49 protected operations" in a document that twice forbids hard-coded surface denominators, and a test already pins the number

`:680` and `:1674` both state that "surface denominators (operation counts, parity-oracle cells, C13 inventory)
are always the current generated Contract Spine inventory — never hard-coded counts (2026-07-15)". S-7 opens
with "All **49** protected Contract Spine operations". `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs:148`
already pins `spine.Length.ShouldBe(49)`. PD10 itself changes the surface — the matrix's own gap list records
"the absent incident-evidence operation surface" and "the missing task parameter on effective-permissions".
(For reference, the spine declares 84 `operationId`s in total, so the 49 is a filtered denominator whose filter
is defined outside this document.)

**Unit A** adds the incident-evidence operation per the G-gap remediation → 50 protected operations; the pinned
test and S-7 both go stale. **Unit B** updates the test to 50 and leaves the prose at 49, making the
architecture the drifting artifact.

**Tightening.** Replace "All 49 protected Contract Spine operations" with "every protected operation in the
current generated Contract Spine inventory"; keep the numeric assertion only in the generated artifact and the
matrix, and state that the protected/unprotected partition is declared in the spine (see F5's unprotected-
operation clause).

---

### F15 — The malformed-versus-unauthorized evaluation order is unstated, so one surface's validator becomes the probe the other surface's ordering forbids

S-4's PD10 clause (`:620`) names three steps — authority-unavailability, then authority, then resource lookup —
and omits validation. The authorization matrix's conjunct chain also omits it. A-9 and concern #3 place
"canonical validation" after authorization for *mutating* operations only.

**Unit A — REST implementer.** Adds an OpenAPI/model-validation filter in the ASP.NET pipeline (the idiomatic
placement), so a malformed protected request returns 400 `validation_error` naming the failing field, before any
authority evaluation.
**Unit B — CLI/MCP implementer.** Validates tool input locally (per the Adapter Parity Contract), producing a
pre-SDK `client_configuration_error` / `usage_error` (exit 64) for the same input.

**Incompatible artifact.** The cross-adapter invariant at `:684` ("same input → identical category, code,
retryable, clientAction") fails on every malformed request. Security consequence: under Unit A an unauthorized
caller gets a free oracle over the request schema, enum members, and bounds of a resource family they hold no
authority over — the "learn it from the shape of the response" channel S-7's rationale is written to close.

**Tightening.** Add to S-7: "Evaluation order for protected operations is: (1) well-formedness sufficient to
derive the S-8 scope — failure returns the safe-denial envelope, never a validation error; (2)
authority-unavailability; (3) authority; (4) canonical/semantic validation; (5) resource lookup. No validation
detail, field name, bound, or enum member is returned to a caller who has not passed step (3). Surface-local
pre-flight validation is permitted only for inputs that are surface concerns (missing idempotency key, missing
credential) and must never pre-empt a server-side category."

---

### F16 — NFR79 and NFR80 have two believable owners, and the traceability row can hold only one

`:259` assigns Epic 13 "the release-hardening evidence for `NFR74`–`NFR84`"; `:261` then carves out NFR79/NFR80
mechanism to Epic 12. But `docs/exit-criteria/nfr-traceability.md` already assigns NFR79 → story `12-1` and
NFR80 → story `12-2`, with owners "Persistence / Delivery" and "Persistence / Platform". The row model has one
`stories` column and one owner, and `NfrTraceabilityConformanceTests` re-derives it from the PRD and epics; it
cannot express mechanism-versus-evidence.

**Unit A — Epic 12 Story 12.1.** Lands restart-survival tests and flips NFR79 to `covered`, citing `12-1` — the
story the row already names.
**Unit B — Epic 13.** Keeps NFR79 reference-pending until OQ12's deployment-profile evidence exists, per `:259`.

**Incompatible artifact.** A row that oscillates between `covered` and `reference-pending` with each epic's
commits, and a release-blocking gate that can be declared green by the epic the document explicitly excludes
from product-completion metrics — or by the epic that is not the evidence owner. The same defect applies to
NFR76/NFR84, whose "mechanism owner" is a *document section* (`:261`), which cannot produce evidence (see F7).

**Tightening.** Split the row model into `mechanism_story` and `evidence_story`, extend the governance/NFR
decoupling precedent paragraph with: "a row may not move to `covered` while its `evidence_story`'s owning open
question (OQ12 / OQ13) is unresolved, regardless of mechanism completion", and update
`NfrTraceabilityConformanceTests` in the same commit. Secondary note: all eleven new rows currently cite
`sprint-change-proposal-2026-09-15.md` as their exit-criteria artifact — a planning document as the evidence
pointer — which sits uneasily beside "Admission is not implementation evidence" (`:263`); they should cite the
gate or artifact that will carry the evidence, or `—`.

---

## LOW

### F17 — The projection-ownership table still credits Story 10.9 with "live search/status proof" after PD5 narrowed it

`:271` reads "Epic 10 | Search-bridge projection, Server registration, authorization, hydration, pruning, live
search/status proof (Stories 10.7–10.9)", while `:227` says "Story 10.9 is narrowed to the metadata-only safety
guard (PD5) and is **not** a forward capability dependency." An Epic 10 implementer reading the ownership table
builds body-content indexing proof under 10.9; one reading the overlay builds a guard that forbids it.
**Tightening:** amend `:271` to "(Stories 10.7–10.8; Story 10.9 is the metadata-only safety guard per PD5 and
carries no capability proof)".

### F18 — S-7's "MVP release reasons permit only approved values such as `caller_completed`" names no subject, no owner, and no closed set

The clause appears mid-decision with no antecedent: "release reasons" could be lock-release reasons, workspace-
release reasons, or release-readiness reasons; and "such as" is not an enumeration. Two implementers permit
`{caller_completed}` and `{caller_completed, task_completed, superseded}` respectively, and both claim the
values were "approved".
**Tightening:** name the field and close the set — "`ReleaseWorkspaceLock.reason ∈ { caller_completed }` in MVP;
every other value is reserved post-MVP and rejected with `validation_error`; the set is declared in the Contract
Spine and asserted by the C13 oracle."

---

## Summary table

| ID | Severity | One-line |
| --- | --- | --- |
| F1 | critical | S-6 fixes no token derivation — two writers emit different tokens, and an unkeyed digest over branch/repo names is dictionary-reversible and correlates across tenants |
| F2 | critical | Tokenizing repository/ref breaks the canonical serializing identity and leaves the Git commit executor with no recoverable target ref after restart |
| F3 | critical | PD11 double-defines three `(state, event)` pairs while the stated implementation is a two-input switch and the C6 gate only demands "a defined outcome" |
| F4 | high | The surviving `changes_staged` + `LockLeaseExpired` side effect ("operator intervention required") contradicts the new `dirty` disposition and rule 2 |
| F5 | high | The S-7 envelopes are underspecified here — `resource_unavailable` has no category, the 503 has no category/code/`retryable`, so `cli_exit_code` and `mcp_failure_kind` are not derivable |
| F6 | high | `visibility` is required on every error but unenumerated, and S-6's new `withheld` collides with the shipped definition of `redacted` |
| F7 | high | No story owns the PD10 spine correction; Epic 13 Story 13.2 owns NFR76 and will build a second deny-by-default shape |
| F8 | high | The wave table breaks its own strictly-lower-rank rule six times, leaves five prerequisites (incl. all of Epic 13) unranked, and names manifest fields that do not exist |
| F9 | high | C3's terminal-closure trigger makes the staged-content window unreachable and PD11 rule 6 leaves orphaned staged workspaces with no terminal state |
| F10 | medium | "Retryable / no confirmed remote effect" has no classifier — GitHub and Forgejo will split 429/timeout differently, and timeout-as-retryable duplicates commits |
| F11 | medium | "The originating task" is not durably identifiable after lock loss; one reading bricks the workspace, the other lets a caller claim another task's staged work |
| F12 | medium | `LockLeaseBecameStale` is prose-only, the CI gate runs off the code's vocabulary, it is a wire-enum change with no regeneration duty, and it has no outcome for a staged `dirty` workspace |
| F13 | medium | S-8 forbids caller-supplied scope but never specifies the derived-scope shape; two evidence owners will emit incompatible structures |
| F14 | medium | S-7 hard-codes "49 protected operations" in a document that twice forbids hard-coded denominators, and a test already pins 49 |
| F15 | medium | Validation-versus-authority order is unstated; the REST validator becomes a probe that S-7's own rationale forbids, and cross-surface parity fails on every malformed request |
| F16 | medium | NFR79/NFR80 have a mechanism owner and an evidence owner but one traceability row, so the row can be flipped green by either epic |
| F17 | low | The projection-ownership table still credits Story 10.9 with live search/status proof after PD5 narrowed it |
| F18 | low | "MVP release reasons permit only approved values such as `caller_completed`" names no field, no owner, and no closed set |

## Recommendation

Do not accept the amendment as mechanism authority until F1, F2, and F3 are closed with explicit decision text —
each is a case where two competent implementers reading only this document produce artifacts that cannot be
reconciled after the fact, and two of them (F2, F3) silently break invariants the document declares elsewhere
(single active mutation writer; staged work is never silently lost). F5, F6, F7, and F9 should be closed in the
same pass because they are the cross-surface contracts that the rank-30 stories will consume. F8 blocks
scheduling entirely: the manifest the overlay designates as the sole scheduling authority has not been
regenerated and does not contain `execution_rank`.
