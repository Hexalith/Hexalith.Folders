# Rubric Walker Review — Folders architecture relock, 2026-09-17

- **Reviewer lens:** independent BMad good-spine rubric walker
- **Subject:** `_bmad-output/planning-artifacts/architecture.md` plus the co-normative authorization, C3, C6, lifecycle, deployment, recovery, and downstream-reconciliation artifacts changed for the 2026-09-17 relock
- **Governing inputs:** `sprint-change-proposal-2026-09-15.md`, `reconcile-architecture-downstream-2026-09-16.md`, and `implementation-readiness.md`
- **Repository state reviewed:** `16f63e6` plus the uncommitted relock as observed at approximately 2026-09-17 10:01 Europe/Paris
- **Mode:** read-only. This review file is the only artifact written by this reviewer.

## Verdict: **FAIL** — the mechanisms are substantially stronger, but three downstream blockers remain unsatisfied

The update closes most of the eleven routed mechanism gaps: stale authority now has one target outcome, the
five lifecycle guards are explicit, PD8 has a confidential operational-value boundary, aggregate concurrency
and event evolution are selected, SSRF and fallback authorization have concrete policies, and the deployment
and recovery envelope is no longer silent. Dapr actor serialization and ASP.NET Core fallback authorization are
current, appropriate platform primitives for the selected roles ([Dapr actors](https://docs.dapr.io/developing-applications/building-blocks/actors/actors-overview/),
[ASP.NET Core fallback authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/secure-data?view=aspnetcore-10.0)).

The gate still fails because the retention clock changes the governing September 15/PRD rule, the approval
objects and execution freeze form a circular dependency, and the purportedly strict rank graph still contains
an equal/later-rank dependency plus several non-machine-resolvable prerequisites. Those are not polish defects:
Delivery cannot produce the requested valid manifest without choosing policy or silently granting itself an
exception.

Finding count: **3 critical, 5 high, 4 medium, 2 low**.

## Good-spine checklist

| Checklist item | Result | Basis |
| --- | --- | --- |
| Fixes the real divergence points for the level below | **Partial** | All seven prioritized mechanism families now have target decisions, but C3 timing, approval sequencing, rank edges, token rotation, and error mapping still allow incompatible implementations. |
| Every rule is enforceable and prevents its stated divergence | **Partial** | Triple-keyed lifecycle and EventStore/Dapr write serialization are enforceable; approval/freeze, redirect credential handling, callback reachability, and token-key rollover are not. |
| Nothing deferred/open can let two units diverge | **Fail** | The role approvals are precisely named but placed on the wrong side of the freeze; token rollover and several graph edges still require invention. |
| Named technology is verified-current and fit | **Pass, with one qualification** | Dapr actors provide per-actor turn serialization and transactional actor state; ASP.NET Core fallback policy is the right omission-safe default. The document must not infer app-port isolation from either primitive. |
| Ratifies rather than contradicts brownfield reality | **Pass** | Target-versus-as-built distinctions are unusually candid; PD8/PD10/PD11 are not presented as implemented. |
| Covers the governing product/spec capabilities | **Fail** | The staged-content cleanup clock weakens the PRD's seven-days-after-terminal rule, and the change is misattributed to the September 15 proposal. |
| Every whole-system dimension is decided, deferred, or escalated | **Partial** | Deployment, scaling, backup, restore, SSRF, concurrency, and schema evolution are now decided. Operational ownership for the new WORM ledger and exact rank/approval nodes is incomplete. |
| Does not weaken or contradict inherited authority | **Fail** | The September 15 proposal remains named as governing authority, but the retention timing and freeze/approval sequencing do not preserve it. |

## Critical findings

### RR-C1 — The single staged-retention deadline permits cleanup earlier than the governing seven-day post-terminal window

**Locations:** `architecture.md` C3/lifecycle rules and guard register; `docs/exit-criteria/c3-retention.md:14-20,29`; PRD C3 at approximately `prd.md:726`; approved proposal §7.3.

The proposal and Product authority fix the destructive-cleanup trigger at **terminal task closure with no active
task** and retain the seven-day window. The PRD is explicit: temporary working files delete seven days after
task-terminal closure and no active task.

The relock instead sets `stagedRetentionStartedAt` when content first becomes inaccessible (or only later when
an already-dirty task closes), then permits cleanup when that deadline has elapsed and the task eventually
becomes terminal. If a task stays inaccessible for more than seven days and closes on day eight, the content is
immediately cleanup-eligible. It receives no seven-day post-terminal retention at all. That is a materially
different product/legal policy. `c3-retention.md:14` also says the September 15 authority added this separate
clock; §7.3 did not.

**Required fix:** keep two distinct clocks/predicates. A recovery deadline may start at first inaccessibility and
govern `inaccessible -> dirty`. Destructive cleanup must additionally use a cleanup-eligibility timestamp no
earlier than `terminalTaskClosedAt + P7D`, with no active task and legal-hold checks. If Product and Legal instead
intend the shorter policy, record it as a new explicit deviation requiring A7b approval; do not call it the
September 15 governing rule. Update Story 4.22 acceptance and the downstream reconciliation to name both
timestamps and their different purposes.

**Disposition:** discuss/authority correction; not safe to autofix as wording only.

### RR-C2 — The freeze-removal gate requires evidence from stories that the freeze forbids starting

**Locations:** `architecture.md:247-275`; rank rows `:220-234`; downstream reconciliation Story 1.17/4.22/12.7 acceptance and `:158-174`; approved proposal §§8-9; PRD implementation-readiness/freeze text.

The proposal keeps the general execution freeze until all required role approvals and Section 9 validation
pass. The relock then defines the approval objects as follows:

- A6b requires generated v2 conformance evidence produced by Story 1.17.
- A7 requires triple-keyed implementation and tests produced by Story 4.22.
- A5 requires Story 12.7 token/sealed-value implementation evidence.
- A2/A2b is coupled to Story 13.7 supported-profile and recovery evidence.

The downstream reconciliation simultaneously keeps `general_execution_hold: true` until Section 9 passes and
calls Story 1.17 the first executable story. No authority artifact grants a narrow recovery exception to start
1.17, 4.22, 12.7, or 13.7 while the hold is active. Therefore the evidence needed to remove the freeze cannot be
created without violating the freeze. The approved proposal's design attestations have been silently converted
into post-implementation acceptance attestations.

**Required fix:** separate each design/digest approval from delivery evidence. Obtain the A5/A6/A6b/A7/A7b
design attestations required by Section 9 against finalized candidate documents before freeze release, then let
the owning stories produce runtime evidence that blocks story/release closure but not story start. Alternatively,
if A8 is intended to authorize a narrow planning-recovery implementation lane under the hold, state that exact
exception, its allowed story IDs/files, and its exit conditions in the governing execution-control artifact and
manifest. Do not infer it from sponsor routing language.

**Disposition:** discuss/governance correction.

### RR-C3 — The “strict topological” schedule still contains an equal/later-rank dependency and non-exact edges

**Locations:** `architecture.md:214-234`; `reconcile-architecture-downstream-2026-09-17.md:84-125`; existing Story 11.13 acceptance in `epics.md`.

The validator rule itself is sound. The supplied graph does not yet satisfy it:

1. Story **11.13** is rank 5, but its acceptance precondition is that the applicable Workstream 11 adoption
   stories have landed. Those include rank-5 and rank-6 stories. That is an equal/later-rank prerequisite under
   the document's own rule.
2. Story 4.19 names a “reapproved C6 decision” and Story 4.20 a “reapproved OQ3” in addition to their owning
   stories. Those approvals are pending nodes with no rank. The validator is instructed to reject an unranked
   nonterminal prerequisite.
3. Story 6.14 depends on “OQ9's lower-rank decision prerequisites,” Stories 6.12/6.13 retain unnamed “owning
   event flows,” and OQ11 follows a “12.x/4.x/provider vertical slice.” Those phrases are not exact node IDs and
   cannot be validated as a graph.
4. Stories 4.19 and 4.20 require equivalent/conflicting replay or all-mutation idempotency in acceptance but the
   handoff does not add Story 12.6 as a prerequisite.

**Required fix:** emit an explicit prerequisite list for every nonterminal row. Move 11.13 after every adoption
story it synchronizes (rank 20 is available before 11.21), or narrow its acceptance so it has only lower-rank
inputs. Represent approvals as rank-0 decision nodes with evidence references, or make them acceptance evidence
of 1.17/4.22 and remove the duplicate raw prerequisites. Enumerate the OQ9 and OQ11 producers, the 6.12/6.13
event-flow owners, and add 12.6 wherever durable all-mutation replay is accepted. Re-run the graph validator
against the exact list rather than the prose ranges.

**Disposition:** autofixable once Delivery's node schema is chosen.

## High findings

### RR-H1 — D-11 assigns CLI exit 75 to `concurrency_conflict`, but the canonical table already assigns 75 to `redacted`

**Locations:** `architecture.md` D-11 near `:685`; canonical CLI/MCP tables `:756-776`; downstream reconciliation Story 12.1 at `:75` and Story 1.17 at `:34-39`.

D-11 selects a useful canonical 409 conflict, but its cross-surface mapping is not a decision yet. CLI exit 75
already means `redacted`, and `concurrency_conflict` is absent from the illustrated MCP kind set. Story 1.17 is
supposed to close the v2 error/exit/failure vocabulary at rank 4, while Story 12.1 introduces the collision at
rank 10; the handoff only tells 12.1 to add the error.

**Fix:** allocate one non-colliding exit code, add `concurrency_conflict` to the v2 error schema/C13/MCP closed
vocabularies in Story 1.17, and make Story 12.1 consume that mapping. The story cannot independently invent a
post-freeze error category after the vocabulary has been closed.

### RR-H2 — Key-versioned HMAC tokens are not a stable lock identity across token-key rotation

**Locations:** `architecture.md` S-6/PD8 operational boundary; downstream Story 12.7 acceptance `:55-59`.

The correlation token is declared both key-versioned and the canonical writer/lock identity. Rotating the
per-tenant HMAC key changes the token for unchanged cleartext. Existing locks, bindings, events, and new commands
can then name the same provider target with different tokens and bypass serialization or lose correlation.
Active/previous key language and re-sealing are defined for AES sealed values, not for token identity rollover.

**Fix:** decide the token-key lifecycle separately from encryption-key rotation. Specify how lookup compares
active and previous tokens, how durable identities are migrated or aliased atomically, when old token versions
retire, and how concurrent old/new writers remain serialized. Add rotation-under-held-lock, restart, replay, and
collision tests to Story 12.7.

### RR-H3 — Co-normative approval labels still imply approvals the release overlay says are pending

**Locations:** `architecture.md` S-7 and C0-C13 status-reconciliation prose; `docs/exit-criteria/c6-transition-matrix-mapping.md:18,34-60`.

The release overlay correctly says OQ3/C3/C6/C9 digests are superseded and approval-pending. Lower-level text
is less precise:

- S-7 says the outcome cells are “approved” even though the owning matrix is `2.0.0-candidate.1` and the old
  digest is historical only. Historical unchanged cells can be identified as such, but they are not current
  release authority.
- The changed `dirty` disposition is marked `approved` in the C6 state catalog while A7 explicitly approves
  the final transition/**disposition** digest.
- The guard table heading says approval-pending under A7b even though A7 governs guard semantics and A7b governs
  their retention integration.

**Fix:** normalize every changed row to `superseded-pending-reapproval`, distinguish historically unchanged
cells from current authority, and use A7 for guards/dispositions plus A7b only where the retention clock or legal
hold is part of the rule.

### RR-H4 — `AllowAnonymous` Dapr callbacks lack a concrete sidecar-only reachability rule in the architecture decision

**Locations:** S-10; I-3/I-10; supported profile `:46-49`; downstream Story 13.2 `:81`.

ASP.NET Core fallback authorization does not protect an endpoint carrying `AllowAnonymous`. Dapr sidecar mTLS
authenticates sidecar-to-sidecar traffic, not a direct caller that can reach the application callback port.
S-10 says callbacks “require” Dapr identity/component/topic controls but does not bind the application listener
to loopback/UDS/app-initiated stream, remove it from the Kubernetes Service, or state the network-policy rule that
makes direct reachability impossible. Story 13.2's existing title says sidecar-only app port, while the exact
handoff omits the port/listener acceptance.

**Fix:** choose the port-isolation mechanism and add a deployed negative test proving direct pod/service ingress
cannot reach the anonymous callback. Keep the endpoint-metadata allow-list test as a separate control.

### RR-H5 — Redirect validation does not say whether provider credentials may cross authorities

**Locations:** S-9; downstream Story 13.1 `:80`.

Re-resolving and pinning each redirect closes SSRF pivots, but it does not prevent credential exfiltration to an
allowed public redirect target. A hand-written redirect loop can accidentally preserve Authorization, cookies,
or provider-specific secret headers across host/port changes.

**Fix:** either reject cross-authority redirects for credential-bearing calls, or strip credentials and require
an independently authorized binding for the new authority. Also disable ambient proxies unless they are an
explicit trusted egress component covered by the same destination policy. Add redirect credential-sentinel tests.

## Medium findings

### RR-M1 — The WORM deletion ledger is architecturally useful but not fully assigned to Story 13.7

I-11 and the co-normative deployment/runbook documents now define a signed, five-minute-export, 400-day WORM
ledger outside the PostgreSQL recovery set. Story 13.7 acceptance names only ledger replay. It does not require
the producer, signing/integrity-chain key ownership, export-lag alert, retention enforcement, loss/corruption
behavior, or recovery of the control store itself. Add those acceptance points; otherwise the restore drill may
pass against a hand-seeded ledger rather than the mechanism I-11 depends on.

### RR-M2 — Reserved post-MVP lifecycle events are still rendered as valid transition edges

`OperatorDiscardRequested`, `OperatorRetrySucceeded`, and `OperatorMarkedFailed` are stated to reject in MVP and
to leave the published spine enum, yet they remain in the architecture event vocabulary and appear as transition
arrows in the lifecycle diagram. A parser or implementer can reasonably treat those arrows as valid outcomes.
Move them to a separate reserved/rejected-event inventory and remove valid-looking edges from the MVP diagram.

### RR-M3 — The authorization denominator says 12 access states while its tables enumerate 14

The matrix declares six actors plus six negative cases as the 12-state denominator, then adds `absent-resource`
and `insufficient-scope` in the same canonical-negative-state table. Clarify whether those are states counted by
the conformance gate or conditions outside the row dimension, and make the denominator/gate reflect that choice.

### RR-M4 — Story 12.7's ownership boundary depends on a platform capability with no upstream delivery gate

Folders is correctly forbidden from writing confidential domain values through raw Dapr/database APIs, but the
handoff assumes EventStore already exposes `ISealedOperationalValueStore`. If it does not, Story 12.7 cannot
implement its acceptance without changing the root-declared EventStore repository. Name the upstream capability
version/issue and accepted evidence as a prerequisite, or explicitly include the cross-repository platform change
in Story 12.7's authorized scope and rank.

## Low findings

- The architecture's “12 Critical + 5 Important + 6 Deferred” completeness count is a legacy summary and no
  longer matches the expanded D-11/D-12, S-9/S-10, and I-10/I-11 decision inventory. Remove the count or generate it.
- `architecture.md` frontmatter says `status: complete` while the document's own readiness assessment says NOT
  READY. Those can represent different concepts, but the frontmatter field should be named `documentStatus` or
  explained to prevent lifecycle consumers treating it as implementation readiness.

## Strong portions to preserve

- Stale/unavailable/conflicting/incomplete authority versus fresh negative facts is now a coherent target rule
  in both architecture and the candidate matrix.
- The five `(state,event,guard)` discriminators, default rejection, durable guard fields, and explicit
  target-versus-code divergence are precise enough for Story 4.22 once RR-C1 is corrected.
- D-11's sole-writer/no-blind-retry rule and D-12's immutable-version/upcaster rule close the Epic 12 mechanism
  gaps; only the cross-surface conflict mapping remains.
- The production profile, preproduction parity, replica floors, PostgreSQL authority, RPO/RTO, isolated restore,
  and deletion-ledger principle are materially complete architecture decisions.
- The downstream reconciliation is admirably explicit about reserved story IDs, unchanged lifecycle status, and
  the continuing freeze. It needs graph/approval corrections, not a rewrite.

## Re-review after corrections — 2026-09-17

### Verdict: **FAIL** — one approval-order blocker remains

The corrections resolve the prior retention, freeze-lane, CLI/MCP, token-rotation, approval-role, callback,
redirect, WORM-evidence, reserved-event, authorization-denominator, and EventStore-capability findings. The
two recovery/cleanup clocks are now distinct; the relock-only lane has an explicit artifact boundary; every
literal edge in the exact rank table points to a lower rank or accepted-terminal evidence; and the two external
EventStore nodes are explicitly fail-closed escalations requiring owner, release, and evidence fields before
acceptance.

#### Remaining blocker — A6b and Story 1.17 still form an impossible authority sequence

The governing September 15 proposal requires the v2 surfaces to be regenerated and **then** OQ3/A6b to be
reapproved against the resulting matrix digest. The current co-normative package still says the same in
`architecture.md` (A6b is a “closing approval”; regenerate the v2 surfaces “and then” reapprove OQ3) and in
`docs/contract/authorization-matrix.md` (Story 1.17 generates the v2 artifacts and then obtains A6b).

However, the exact edge table makes accepted `DEC-A6B-OQ3` a prerequisite for Story 1.17, while Story 1.17's
acceptance also includes A6b. The literal ranks pass a numeric lower-rank check only because A6b was placed at
rank 0; the execution plan remains unsatisfiable because the approval cannot both precede and follow the output
whose digest it approves. This also conflicts with the architecture's newer sentence that A5/A6/A6b/A7/A7b
approve candidate digests before evidence work.

**Required correction:** choose one governing sequence and express it identically in the approval table,
relock-only lane, Story 1.17 acceptance, and exact rank/edge table. To preserve the September 15 authority,
authorize v2 regeneration under A6 and the relock-only lane, make A6b an explicit post-generation approval node
over the resulting final matrix digest, and require accepted A6b before freeze release and downstream consumers;
remove the reverse `1.17 -> DEC-A6B-OQ3` prerequisite (or split generation and story closure into separately
ranked nodes). Re-run the strict-rank validator after that change.

## Final follow-up re-review — 2026-09-17

### Verdict: **PASS** — no remaining concrete architecture blocker

The A6b/Story 1.17 cycle is closed. The package now expresses one governing sequence across the architecture,
authorization matrix, and downstream reconciliation: pre-generation approvals → `RELOCK-PLANNING` →
`1.17-GENERATE` → post-generation `DEC-A6B-OQ3` → `RELOCK-SECTION9` → `DEC-A8-HOLD` → canonical Story 1.17
and all other ordinary work. The generation milestone cannot close the story or expose v2, A6b signs its exact
output, and Section 9 consumes rather than precedes that approval.

After expanding the universal A8 prerequisite, every unresolved prerequisite has a strictly lower rank; the
graph has no equal-rank, forward-rank, unranked-nonterminal, or approval/output cycle. All prior material rubric
findings remain resolved. The named role attestations and the two EventStore owner/release records remain
explicit fail-closed approvals/escalations, not undecided architecture.
