# Adversarial-Divergence Review — Architecture Update 2026-09-17

Reviewer lens: configured `bmad-architecture` adversarial divergence.

Artifacts reviewed:

- `_bmad-output/planning-artifacts/architecture.md`
- `_bmad-output/planning-artifacts/reconcile-architecture-downstream-2026-09-17.md`
- `docs/contract/authorization-matrix.md`
- `docs/exit-criteria/c3-retention.md`
- `docs/exit-criteria/c6-transition-matrix-mapping.md`
- `docs/diagrams/workspace-lifecycle.md`
- `docs/deployment/supported-mvp-profile.md`
- `docs/runbooks/backup-restore.md`
- governing `_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-15.md`
- prior `_bmad-output/planning-artifacts/reconcile-architecture-downstream-2026-09-16.md`
- `_bmad-output/planning-artifacts/implementation-readiness.md`

## Gate verdict

**FAIL.** The revision closes most of the September 16 routed mechanism questions, but it still contains two
governing-authority contradictions, an execution dependency cycle plus a freeze/approval deadlock, and security
and recovery ambiguities that would force Delivery to invent behavior. Do not treat the architecture update as
downstream-ready until the critical findings are removed and the high findings are either fixed or explicitly
re-routed to named approval objects.

## Critical findings

### C1 — PD8 adds reversible payload recovery that the governing proposal explicitly prohibited

**Disposition:** discuss and correct the authority record before handoff; this is not a safe editorial autofix.

The September 15 proposal is explicit: S-6/C9 must provide token auditability **without reversible payload
recovery** (`sprint-change-proposal-2026-09-15.md:175-184`). The updated S-6 instead persists AES-256-GCM
ciphertext, defines an unsealing API, restores the encryption keys separately, and assigns that reversible
capability to Story 12.7 (`architecture.md:699,705`; reconciliation `:53-59`). Calling the bytes a “sealed
operational value” avoids durable *cleartext*, but it does not satisfy “without reversible payload recovery.”

This matters beyond wording: the new capability expands A5 from the approved token substitution into a new
recoverable confidential-data store, key-recovery contract, backup surface, authorization seam, and erasure
workflow. The current A5 row in the proposal approved only replacement with correlation tokens
(`sprint-change-proposal-2026-09-15.md:373`).

**Required resolution:** choose one of these two authority-consistent paths:

1. remove reversible storage/unsealing from PD8 and define how provider execution obtains a non-persisted value;
   or
2. record an explicit amendment to the September 15 authority that approves the sealed-value exception, its
   data classification, recovery purpose, retention/erasure rules, authorizers, and Legal-review test before
   Story 12.7 is admitted.

A pending C9 digest under A5 is not by itself evidence that the governing proposal was amended.

### C2 — The strict execution graph still contains a Story 12.6/OQ8 cycle

**Disposition:** autofix the architecture and reconciliation together.

The architecture declares that every unresolved prerequisite has a strictly lower rank and that later-rank
dependencies fail validation (`architecture.md:214`). It then says Story 12.6 at rank 11 **cannot close before
OQ8 evidence** (`:225`) while OQ8 at rank 40 **follows Story 12.6** (`:233`). The downstream rank table repeats
12.6 at rank 11 and OQ8 at rank 40 (`reconcile-architecture-downstream-2026-09-17.md:112,120`). The governing
proposal itself contained this circular wording; resolving the unsatisfiable rule was one of this update's
explicit jobs, so copying the contradiction is not sufficient.

**Required resolution:** split OQ8 into its already-approved design authority and its post-implementation
delivery-evidence decision. Make the design decision an accepted terminal prerequisite (rankless with its
approved digest), make Story 12.6 depend only on that design authority, and make the rank-40 evidence decision
depend on completed Story 12.6. Remove every “12.6 cannot close before rank-40 OQ8 evidence” statement and
require the regenerated manifest to reject a reverse `OQ8 -> 12.6 -> OQ8` edge.

### C3 — The freeze and the approval objects form an undocumented execution deadlock

**Disposition:** discuss with Product/Architecture/Delivery and make the allowed relock lane explicit.

The handoff says to retain the execution freeze until all proposal Section 9 checks pass, then calls Story 1.17
the first executable story (`architecture.md:1931-1935`). But Section 9 requires current approved digests and
generated-surface gates. The updated approval register makes A6b depend on Story 1.17 generated-v2 conformance,
A7 depend on Story 4.22 code/tests, A5 depend on Story 12.7 evidence, and A2/A2b depend on Story 13.7 evidence
(`architecture.md:251-260`). Those story outputs cannot exist if the hold forbids their execution, while the hold
cannot lift without those outputs.

The sponsor approval authorizes routing, but the updated documents never define a hold-exempt authority-relock
lane or say which story tasks may run before `general_execution_hold: false`.

**Required resolution:** either (a) define a narrowly authorized relock lane under the still-active hold, listing
the exact Story 1.17/4.22/12.7/13.7 tasks allowed and proving they cannot ship, or (b) split approval of target
digests from later implementation evidence so Section 9 can release the hold before implementation begins.
Encode that distinction in the manifest as separate `decision_status`, `execution_authorization_status`, and
`delivery_evidence_status`; `decision_status` alone must not authorize implementation.

## High findings

### H1 — HMAC key rotation can split the canonical writer identity

**Disposition:** autofix S-6 and Story 12.7 acceptance before Delivery admission.

The single-writer rule requires every alias for one remote/ref to derive the same token
(`architecture.md:107`). S-6 makes that token HMAC-keyed and key-versioned (`:699`), while the operational
boundary requires active/previous key versions and rotation/re-sealing (`:705`). No rule says whether the token
is immutable per binding, re-tokenized atomically, or resolved through an alias set during rotation. Deriving
with a new tenant key yields a different lock identity for the same provider/repository/ref, so an old in-flight
task and a new task can bypass each other's lock and write concurrently. Historical event correlation also
fragments at the rotation boundary.

Specify one stable identity strategy and test the overlap window. Safe options include persisting one immutable
random correlation token per binding, deriving the lock key from a non-rotating identity root while rotating
only encryption keys, or transactionally maintaining old/new token aliases until all leases and retry windows
close. Story 12.7 must prove same-target collision before, during, and after rotation.

### H2 — The staged-content clock changes the approved seven-day retention semantics

**Disposition:** discuss under A7b and correct all C3/C6 copies together.

The proposal says cleanup starts only after terminal task closure with no active task and retains the approved
seven-day window (`sprint-change-proposal-2026-09-15.md:342-359`). The update starts
`stagedRetentionStartedAt` when content first becomes inaccessible, potentially days before terminal closure,
and allows cleanup as soon as both the already-expired deadline and later terminal/no-active predicate hold
(`architecture.md:471-473`; `docs/exit-criteria/c3-retention.md:15-20,29`). A task that stays inaccessible for
seven days can therefore lose its content immediately on terminal closure, receiving no seven-day retention
window after the approved cleanup trigger.

If the recovery deadline must begin at inaccessibility, name it a separate **non-destructive recovery deadline**
and add a distinct cleanup-retention start/deadline at terminal/no-active eligibility. Otherwise start the one
seven-day clock only when that eligibility predicate becomes true. The final A7b approval object must state
which clock Legal is approving; the current single-field name obscures the distinction.

### H3 — The recovery profile does not make the claimed RPO/RTO resilient to its named single-region failure

**Disposition:** autofix I-11, the deployment profile, runbook, and Story 13.7 acceptance.

The profile is explicitly single-region (`supported-mvp-profile.md:11-20`) but the backup rules only say that WAL,
daily recovery points, and the WORM deletion ledger are outside the PostgreSQL *recovery set*
(`:51-58`; `backup-restore.md:9-17`). They do not put continuous WAL, restore points, the deletion ledger, or
key escrow in a separate region/failure domain. A regional loss can therefore remove the database and every
artifact needed to meet RPO ≤5 minutes/RTO ≤4 hours. “Highly available” and “outside the recovery set” do not
establish regional disaster recovery.

Define the protected failure model and storage placement: at minimum cross-region/independent-account durable
WAL and recovery points, separately failed key custody, separately failed WORM ledger, restoration-region
capacity, and a regional-loss drill. If regional loss is intentionally out of scope, rename the promise from
disaster recovery and bound RPO/RTO to a narrower failure class.

### H4 — Approval ownership is not exact across the co-normative deployment artifacts

**Disposition:** autofix the approval tables and C6 labels.

The proposal requires Product on both OQ12 and OQ13; Security is required on OQ12 and on A2/A2b, but not as an
independent OQ13 approver (`sprint-change-proposal-2026-09-15.md:368-370`). The reconciliation correctly lists
those OQ approvers at `:146-148`, but its combined approval row later requires the five-role superset at `:166`.
The deployment profile then says OQ12/OQ13 close with only Operations + Architecture + Security + Test, omitting
Product entirely (`supported-mvp-profile.md:64-74`). Story 13.7 acceptance repeats that omission. Delivery has
no single exact approval object to follow.

C6 has the same problem at smaller scale: the guard heading says all PD11 guards are pending under A7b, although
A7b is the Legal retention integration and A7 owns the transition/disposition model
(`c6-transition-matrix-mapping.md:3-8,18`). Its changed `dirty` disposition is labelled `approved` at `:41`
while the final transition/disposition digest is explicitly awaiting A7.

Keep separate rows for A2, A2b, OQ12, OQ13, A7, and A7b; do not collapse them into approval-role supersets.
Mark every changed disposition/guard row pending under the exact applicable gate.

### H5 — The authorization denominator is internally inconsistent before A6b

**Disposition:** autofix the candidate matrix and its gate expectations.

The matrix declares a denominator of 12 access states and says every canonical negative state appears exactly
once (`authorization-matrix.md:16-22`). It then defines six actors plus six canonical negative states **and two
additional protected denial states**, all in the “Canonical Negative Access States” table (`:68-72,94-110`).
That is either 12 canonical states plus two non-state cases or 14 access states; the current text uses both
models. A completeness gate cannot have an exact expected count until that distinction is machine-readable.

Choose one denominator and name the two extra rows as either states or outcome cases. Update the count, schema,
and A6b conformance test together. Also ensure final `2.0.0` replaces the observed-v1 route inventory rather
than merely approving the prose overlay in `2.0.0-candidate.1`.

## Medium findings

### M1 — Event-version adoption has no legacy unversioned-event rule

D-12 requires every persisted event to carry a positive integer `schemaVersion` and forbids stream rewrites
(`architecture.md:686`). It does not say how an already-retained event with no version is interpreted. Add the
explicit legacy rule (`missing => version 1` only for a closed set of pre-adoption event types, otherwise fail
closed) and require fixtures for it; never let every missing/invalid version silently become v1.

### M2 — The lifecycle diagram draws post-MVP reserved operations as valid transitions

`workspace-lifecycle.md` draws `OperatorDiscardRequested`, `OperatorRetrySucceeded`, and
`OperatorMarkedFailed` edges, then explains below that MVP rejects them. A diagram consumer or generator sees
valid edges before reading the caveat. Render reserved operations outside the active MVP state diagram or use
a visibly separate dashed “reserved / rejected in MVP” legend so the co-normative view cannot be mistaken for
an implementable transition set.

## Positive observations

- The stale-authority 404/503 contradiction is resolved consistently in the target matrix: stale/conflicting/
  incomplete evidence maps to 503, while fresh negative facts map to the non-disclosing 404.
- S-9 addresses DNS rebinding, redirect revalidation, original-host TLS, all-address validation, and private
  endpoint exceptions instead of stopping at URI-string validation.
- D-11 and D-12 name a single aggregate writer and one EventStore-owned upcasting seam rather than allowing
  per-adapter concurrency or per-projection schema policies.
- The update is candid that PD8/PD10/PD11, the deployment profile, and restore automation are target state, not
  implemented capability; no production code or lifecycle status was silently changed in the reviewed set.

## Gate closure conditions

The adversarial lens can pass after:

1. the PD8 reversible-storage authority conflict is removed or explicitly re-approved as an amendment;
2. the OQ8/12.6 cycle and the freeze/approval execution deadlock are eliminated in architecture and exact
   manifest instructions;
3. token rotation preserves one canonical writer identity;
4. C3 has distinct, unambiguous recovery and destructive-cleanup clocks approved under A7b;
5. the DR failure model/storage domains and exact A2/A2b/OQ12/OQ13 approvers are synchronized; and
6. the A6b denominator and C6 approval labels become exact and gateable.

## Re-review after corrections — 2026-09-17

**Verdict: PASS.** The corrected package closes every prior adversarial finding in the requested scope. PD8 is
now token-only and non-reversible; rotation aliases retain one immutable canonical writer identity; OQ8 design
authority and post-implementation evidence are separate acyclic nodes; the relock-only execution lane removes
the freeze/approval deadlock; recovery and destructive-cleanup clocks are distinct; regional-loss recovery
includes independent backup, key-custody, and hold/disposition replay controls; approval objects and approvers
remain separate; the authorization denominator is exactly 14 states; legacy unversioned events use a closed
v1 registry and otherwise fail closed; and reserved lifecycle identifiers are negative fixtures, not active
edges.

**Remaining concrete blockers from this adversarial re-review: none.** The named A1–A8 role attestations,
Delivery regeneration/manifest work, `EXT-ES-EVENT-EVOLUTION`, `EXT-ES-RECOVERY`, implementation, and release
evidence remain explicitly pending work rather than falsely claimed approvals or implemented capability. The
architecture records owners, dependency edges, and hold behavior for those items without presenting them as
closed.
