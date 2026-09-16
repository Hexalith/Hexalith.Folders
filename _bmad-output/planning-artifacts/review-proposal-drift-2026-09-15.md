---
review: proposal-drift
date: 2026-09-15
reviewer: adversarial drift review (Claude Opus 5)
target: _bmad-output/planning-artifacts/prd.md
change signal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-09-15.md (SHA-256 5d12ae4dd4e8f306b8dde5caf8f187d13e40c1fabc6d8e56c0757e24f2004104, verified)
verdict: CONDITIONAL — additive edits sound, replacement edits not propagated; not safe as unqualified product authority
findings: 13 (2 critical, 4 high, 4 medium, 3 low)
---

# Adversarial Drift Review — PRD update of 2026-09-15

## Verdict

**CONDITIONAL — a second pass is required before this PRD can be cited as product authority.**

The *additive* half of the approved change signal landed cleanly and honestly: Epics 12/13 admission,
OQ11–OQ13, the eleven NFR74–NFR84 requirements, and the 73→84 inventory relock are all applied, correctly
worded, correctly hedged, and carried in lockstep across `epics.md`, `docs/exit-criteria/nfr-traceability.md`,
`NfrTraceabilityConformanceTests`, and `run-nfr-traceability-gates.ps1`. NFR1–NFR73 are byte-identical and
unrenumbered. The execution freeze is never described as lifted. Sponsor-only approval is correctly
distinguished from role attestation in the decision table and its intro.

The *replacement* half did not land. §5.1 items 7, 8, 9, and 11 were applied to the decision log only. The
PRD's normative body — the C3/C6/C9 exit-criteria rows, NFR8, NFR54, NFR60, the Task and Lock Completion
Model, Workspace State and Concurrency, Error Codes, and the OQ3 row — still encodes the superseded model
that those decisions replaced. The result is a document whose decision log and whose requirements now say
opposite things about confidentiality, workspace recovery, and which approvals are current. The 2026-09-08
failure mode (PRD update contradicting the Spine, the C6 matrix, and the OQ8 design) is live again, in a
different place.

`architecture.md` — written by a parallel worker under §5.2 — did propagate PD8/PD10/PD11 into its normative
sections *and* recorded the approval supersessions, the "target state, not current behavior" caveat, and two
routed open questions. Where the two artifacts disagree below, architecture is the one that matches the
approved proposal, and the PRD is the one that drifted.

---

## A. Completeness against §5.1 (the eleven required `prd.md` edits)

| # | Required edit | Disposition |
| --- | --- | --- |
| 1 | Epics 12/13 in Current Delivery Posture; numbering is identity not order; reference manifest waves | **Applied** (see A-1; but see H-4 on the manifest reference) |
| 2 | Replace PD1 — admit Epic 12, add OQ11, OQ5/6/7 depend on it | **Applied** |
| 3 | Replace PD3 — admit Epic 13, add OQ12/OQ13 | **Applied** |
| 4 | Append eleven requirements as NFR74–NFR84 without renumbering NFR1–NFR73 | **Applied** |
| 5 | Replace PD4 with the §6 lifecycle table; status changes evidence-led/explicit/dated | **Applied** (minor compression, L-3) |
| 6 | Replace PD5 — metadata-only search, narrowed 10.9 safety-guard bar | **Applied** |
| 7 | Replace PD8 — event-write tokens; **remove projection-time-redaction wording** | **PARTIAL — the removal was not done (C-1)** |
| 8 | Replace PD10 — spine correction; gates consume only the corrected, reapproved matrix | **PARTIAL — "reapproved" dropped (M-4); body not aligned (C-2); OQ3 not marked superseded (H-1)** |
| 9 | Replace PD11 — §7.3 lifecycle rules; operator transitions reserved post-MVP | **PARTIAL — decision row correct, completion model contradicts it (C-2)** |
| 10 | NFR traceability declaration 73 → 84; record linked PD6 relock; nothing marked implemented | **Applied** (declaration is only in the PD rows and frontmatter, L-1) |
| 11 | Retain old PD text as superseded history with approver, date, proposal path, authority digest | **PARTIAL — options/owner/decide-by deleted (M-3); "resulting authority digest" and named approver missing (M-2)** |

### Item 1 — applied

`prd.md:159` now reads: *"…the sponsor approval of `sprint-change-proposal-2026-09-15.md` on 2026-09-15
admitted both to this PRD's release inventory under PD1 and PD3. Epic 12 is an MVP foundation rather than an
implicitly later epic; Epic 13 is the release-hardening epic that owns the NFR74-NFR84 evidence. Their release
questions are OQ11 for Epic 12 and OQ12/OQ13 for Epic 13. Epic and story numbers are stable identities, not
execution order…"* The sponsor-only qualifier and the freeze are both carried in the same paragraph. Correct.

### Item 4 — all eleven present, no renumbering, substance matches

Two new categories were appended after `### Verification Expectations`:

- `### Edge Security and Deployment Hardening` (`prd.md:1049–1057`) — 5 bullets = NFR74–NFR78.
- `### Durable Operation and Release Evidence` (`prd.md:1059–1066`) — 6 bullets = NFR79–NFR84.

Bullet count across the whole `## Non-Functional Requirements` section is now exactly **84**
(12+12+12+5+10+8+5+5+4+5+6). Category-by-category, NFR1–NFR73 are untouched: the `git diff` of `prd.md`
contains a single insertion hunk (`@@ -1043,0 +1049,21 @@`) in the NFR section, and the `git diff` of
`docs/exit-criteria/nfr-traceability.md` changes no NFR1–NFR73 row or hash. **No renumbering occurred.**

Substance check against the proposal's wording — all eleven match, with only mandated-modal normalization
("must") and two instances of "a named owner" where the proposal says "an owner":

| ID | PRD text | Matches proposal |
| --- | --- | --- |
| NFR74 | "Bearer credentials must be accepted only over HTTPS or an explicitly approved loopback development boundary." | yes |
| NFR75 | "Provider endpoints must deny private, loopback, link-local, metadata-service, and otherwise prohibited destinations unless an approved deployment policy explicitly allows them." | yes |
| NFR76 | "Protected endpoints and internal service boundaries must deny by default when authority is absent, stale, malformed, or unavailable." | yes |
| NFR77 | "Local CLI and MCP credential material must use owner-only storage and must never be emitted to logs, telemetry, diagnostics, or generated artifacts." | yes |
| NFR78 | "Repository and workspace content is untrusted input and must not control commands, paths, templates, or rendered active content without validation or neutralization." | yes |
| NFR79 | "Accepted mutations, their state transitions, and their required evidence must survive process restart." | yes |
| NFR80 | "Supported multi-replica deployments must converge on one authoritative state without seed-local or replica-local correctness assumptions." | yes |
| NFR81 | "Readiness must report actual dependency health and must not report ready from configuration or seed data alone." | yes |
| NFR82 | "Every release-significant metric and alert must have demonstrated emission, a named owner, and fault-path evidence." | yes |
| NFR83 | "Release evidence must be classified as automated, operational, approval-bound, or reference-pending, with a named owner for every non-automated item." | yes |
| NFR84 | "Release verification must cover edge-security behavior including safe denial, endpoint validation, credential handling, and untrusted-content boundaries." | yes |

`epics.md:242–252` mirrors all eleven byte-for-byte as `NFR74`–`NFR84`, and `nfr-traceability.md` carries
eleven new `reference-pending` rows with owners and release-blocking gap text. The lockstep is real.

Both category blocks carry the correct non-evidence caveat (`prd.md:1051`, `prd.md:1061`):
*"Admitted 2026-09-15 by sponsor approval of `sprint-change-proposal-2026-09-15.md` (A2b / linked PD6).
Admission records the requirement; it is not evidence that the requirement is met."* This satisfies item 10's
"do not mark any new NFR implemented merely because it has been admitted."

### Item 11 — the question text is verbatim, the rest of the row is not

Programmatic comparison of `git show HEAD:prd.md` against the working tree confirms the "Superseded question
retained as history" cell is **byte-identical** to the old "Decision needed" cell for all seven of PD1, PD3,
PD4, PD5, PD8, PD10, PD11. Good.

But the old table had five columns (question | options | owner | decide-by) and only the question survived.
See M-3.

---

## B. Overclaiming (and its reverse)

### Where the PRD reads as more closed than it is

1. **PD8/PD10/PD11 are written in the present indicative, with no "target state" caveat.**
   `prd.md:1135` (PD10): *"OpenAPI, the generated client, CLI/MCP parity, the previous-spine comparison, the
   C13 inventory, docs, and tests **are regenerated**, and release generation and parity gates **consume**
   only the corrected matrix. OQ3 **is reapproved** against the resulting digest (A6b)."*
   `prd.md:1136` (PD11): *"the architecture matrix, C6 mapping, C3 policy, code, and tests **express** one
   model under §7.3…"*
   None of this is true today. `architecture.md` states it explicitly and shows the counter-evidence:
   *"**PD8, PD10, and PD11 are target state, not current behavior.** …none of the three is implemented
   today"* — HTTP 403 live on 49 of 50 protected operations; `not_found` / `cross_tenant_access_denied` /
   `audit_access_denied` still emitted by `AuditEndpoints.cs`; `FolderStateTransitions.cs` rejects four of the
   five transitions PD11 adds and a CI test asserts that rejection; no `confidential` tier, tokenizer, or
   `withheld` state exists. The PRD gives the reader no such signal. (**H-3**)

2. **OQ3 still presented as closed at a digest the correction invalidates** (`prd.md:1078`,
   *"**Closed 2026-09-14:** canonical authorization-matrix version `1.0.0` …"*, bound to SHA-256
   `5ffabd71…`). (**H-1**)

3. **A Legal sign-off is asserted for a cleanup trigger Legal never approved** (`prd.md:716`). (**H-2**)

4. **C9 still reads as an open architecture question** (`prd.md:722`) — the *opposite* overclaim, an
   under-claim, but the same defect class: the decision log and the governance row disagree. (**C-1**)

5. **Framing.** The heading `### PM decisions resolved` and the index sentence at `prd.md:1094`
   (*"PD1, PD3, PD4, PD5, PD8, PD10, and PD11 are recorded under **PM decisions resolved** below; PD2, PD6,
   PD7, and PD9 remain open"*) read on a skim as seven closed decisions. The section intro and every row do
   hedge correctly. (**L-2**)

### Where the PRD is correctly hedged (credit)

- `prd.md:159`: *"That admission is sponsor-approved only; the Product, Architecture, Security, Operations,
  Test, Delivery, and Legal attestations named in the proposal remain pending, and the general execution
  freeze stays in force until its freeze-removal gate passes."*
- `prd.md:1126`: *"…it does **not** substitute the sponsor for the Product, Architecture, Security,
  Operations, Test, Delivery, or Legal roles, whose attestations remain required inputs to the proposal's
  freeze-removal gate."*
- Every resolved row's approvers cell ends in **attestation pending**; PD11's adds *"the C3 cleanup-trigger
  reapproval (A7b) additionally requires Legal — **pending**"*.
- Both NFR category blocks carry the admission-is-not-evidence note.
- The freeze is **nowhere** described as lifted or liftable.

### Where hedging costs binding direction

The PRD's *normative* surface — the sections an implementer or contract author actually reads — was not
touched by items 7, 8, or 9. The approved product direction for confidentiality, the authorization spine, and
lifecycle recovery now exists only inside a decision-log table at the very end of the document, while the
Completion Model, Workspace State and Concurrency, Error Codes, and the C3/C6/C9 rows continue to state the
superseded model as binding product requirement. A reader using this PRD as product authority reads the old
model. That is the reverse failure the brief asks about, and it is the single largest defect in this update.
(**C-1**, **C-2**)

---

## C. Contradiction with approved records

### C-1 (CRITICAL) — PD8's required deletion was not performed; the PRD's own NFR inventory now contradicts the approved decision

§5.1 item 7: *"Replace PD8 with the event-write decision… **Remove wording that implies durable cleartext may
be made safe later by projection-time redaction.**"*

The PD8 row was added. The wording was not removed. It survives in three normative places, two of which are
hash-pinned rows of the 84-row release denominator:

- `prd.md:722` — C9 exit-criteria row: *"a confidential tenant override replaces cleartext with a stable
  tenant-scoped correlation token **no later than audit/projection write time** … **(whether replacement also
  occurs at event write is an architecture decision, PD8)**"*. This row still describes PD8 as *open*, and
  still names projection write as the governing point.
- `prd.md:958` — **NFR8**: *"A tenant confidential override replaces cleartext **at audit/projection write
  time** with a stable tenant-scoped correlation token…"*
- `prd.md:1019` — **NFR54**: *"a tenant confidential override **stores only the stable tenant-scoped
  correlation token at audit/projection write time**."*

Against:

- `prd.md:1134` — the PRD's own new PD8 row: *"Option (a), event write: a tenant-confidential override is
  replaced by a stable correlation token **before persistence**. Cleartext confidential values are **never**
  made durable and therefore **cannot be made safe later by projection-time redaction**."*
- `architecture.md` S-6: *"**Per-tenant `confidential` override is applied at event-write time by substituting
  a correlation token for the value (PD8 / A5, 2026-09-15).** Cleartext confidential values are never made
  durable — not in an event, projection, audit record, log, trace, diagnostic, or generated artifact — so
  confidentiality never depends on read-time redaction."* Architecture names the write-cleartext-and-redact-
  on-read option explicitly as *"the superseded release-blocking contradiction."*
- `architecture.md` Release Authority Overlay: *"C9 sensitive-metadata classification … **Superseded —
  approval-pending.** S-6 now writes a confidential override as a correlation token at event-write time;
  reapprove per A5/PD8."*

Impact. NFR8 and NFR54 are rows in the traceability table that the release gate counts against. The PRD's
binding requirement inventory therefore now *requires* the persistence model PD8 abolished, while the PRD's
decision log records the opposite as approved. Any implementer, Spine author, or gate reading NFR8/NFR54 will
build durable cleartext. §9 of the proposal requires "C3, C6, C9, authorization-spine, generated-surface, and
retention gates pass against their approved digests" — a C9 relock cannot be signed against a PRD that states
both models.

Mitigating note, not an excuse: NFR8 and NFR54 fall inside the hash-pinned NFR1–NFR73 block that PD6 governs,
so correcting them requires a PD6-style lockstep relock across `prd.md`, `epics.md`,
`nfr-traceability.md`, and the conformance test — exactly the relock this run performed for NFR74–NFR84. The
C9 row at `prd.md:722` is **not** hash-pinned and could have been corrected freely; leaving it, and leaving it
still calling PD8 an open architecture decision, is unambiguous drift.

**Required fix.** Correct `prd.md:722` in this change set (drop "no later than audit/projection write time",
drop the "(whether … is an architecture decision, PD8)" clause, mark C9 superseded/approval-pending per A5).
Schedule NFR8 and NFR54 into the next PD6 relock and say so in the PD6 row, or fold them into this one.

### C-2 (CRITICAL) — the Completion Model contradicts the PRD's own PD11 row and the approved §7.3

§7.3 rule 4: *"after readiness is restored, `inaccessible + ProviderReadinessValidated -> dirty` when staged
content remains within the C3 window, **otherwise it transitions to `ready`**."*

`prd.md:1136` (PD11 row) reproduces this correctly: *"restored readiness returns `inaccessible` to `dirty`
when staged content remains inside the C3 window **and otherwise to `ready`**."*

`prd.md:495` (Task and Lock Completion Model, unchanged) says the opposite: *"…if the originating task's
authority is restored inside that window, fresh authorization lets it re-acquire a new lock instance and the
workspace returns to `dirty` with its changes intact, and **after cleanup it stays `inaccessible` with content
retired** [ASSUMPTION A18]."* `prd.md:1117` (A18) likewise carries only the `dirty` branch.

`architecture.md:411` matches the proposal, not the PRD: *"| `inaccessible` → `ready` | `ProviderReadinessValidated`
with no staged content remaining (clean, or the C3 window elapsed) | Authorization restored; workspace clean |"*,
restated at `architecture.md:424`: *"Recovery is content-dependent: once readiness is restored, `inaccessible`
→ `dirty` while staged content remains within the C3 window, otherwise `inaccessible` → `ready`."*

Second instance, same defect class. `prd.md:625`: *"unknown-provider-outcome is **auto-reconciling** during its
bounded evidence-check budget."* §7.3 and the PD11 row both say **automatically recovering**, and
`architecture.md:376` fixes the operator-disposition vocabulary as *"exclusively `available`, `auto-recovering`,
`degraded-but-serving`, `awaiting-human`, `terminal-until-intervention`"* — `auto-reconciling` is not a member.
`architecture.md:373` and `:426` both use `auto-recovering` for this state. Operator disposition is a
console-visible product vocabulary the PRD owns; inventing a sixth label here puts the PRD out of step with the
closed set it is supposed to define.

Impact. Story 4.19 (C6/OQ7 authority) and the `FolderStateTransitions.cs` relock are supposed to build "one
model" from these three sources. They currently encode two. Both discrepancies are in PRD-owned territory
(workspace lifecycle values, operator disposition labels), so the PRD is the artifact that must move.

`architecture.md` additionally flags a routed open question the PRD does not carry: *"**Open — the new trigger
can make the staged-content window unreachable (route to Legal + Product + Security with A7b).** PD11 rule 3
recovers `inaccessible` → `dirty` only 'while staged content remains within the C3 window', but PD11's own
cleanup trigger starts that window at *terminal task closure with no active task*"* — and `inaccessible` is
task-terminal in this PRD. The PRD should either record this as an open item or resolve it.

### H-1 (HIGH) — OQ3 is still "Closed" at a digest the approved correction invalidates

`prd.md:1078`: *"| OQ3 | **Closed 2026-09-14:** canonical authorization-matrix version `1.0.0` … | Security/
Authorization | Closed as a governed design decision; reopens on matrix content, version, digest, authority,
signer, or date drift until Security and PM reapprove. …| `docs/contract/authorization-matrix.md` … bound to
SHA-256 `5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7`. |"*

The reopening condition is written as a *future conditional*. It is no longer conditional: A6 was
sponsor-approved, so the matrix *will* change, and A6b requires reapproval. `architecture.md` states this as
current fact: *"OQ3 canonical authorization matrix `1.0.0`, SHA-256 `5ffabd71…` | Security + PM, 2026-09-14 |
**Superseded — approval-pending.** §'Authorization Spine Correction (PD10)' changes the matrix; reapprove
against the new digest per A6b before release."* and *"the superseded `1.0.0` digest must not be reused as
release evidence."*

Verified: `docs/contract/authorization-matrix.md` is still at version `1.0.0`, "Approved on: `2026-09-14`", and
still records the PD10 remediation as unresolved gaps. §9's gate requires "OQ1–OQ4 approval digests are current
and OQ3 has been reapproved after PD10". The PRD, read alone, says OQ3 is closed and current.

**Required fix.** Mark the OQ3 row superseded / reapproval-pending per A6b, as architecture does.

### H-2 (HIGH) — the C3 row asserts a Legal approval for a trigger Legal has not approved

`prd.md:716`: *"| C3 | … | **Approved by PM 2026-06-22 and Legal 2026-06-24** (the record's cleanup triggers
'lock expiry' and 'cancellation' predate this PRD's `dirty` and release rules and are reconciled under PD11):
… **temporary working files deleted 7 days after task-terminal closure and no active task**; …"*

The row folds the *new* trigger into a sentence whose subject is the *old* approval. `docs/exit-criteria/
c3-retention.md` — the approved authority — still records the old trigger in both tables: *"Temporary working
files | 7 days after terminal workspace state | **Workspace cleanup worker after commit, failure,
cancellation, or lock expiry**"*, with approval state "PM approved 2026-06-22; Legal approved (Jérôme Piquot)
2026-06-24, Louveciennes". §3 of the proposal is explicit: *"C3 requires renewed Legal approval because the
cleanup trigger changes from elapsed lock time to terminal task closure with no active task."* A7b is pending.
`architecture.md` records it correctly: *"C3 retention | PM 2026-06-22, Legal 2026-06-24 | **Superseded —
approval-pending.** The cleanup trigger moves from elapsed lock time to terminal task closure with no active
task; Legal, Product, and Security reapprove per A7b."*

NFR60 (`prd.md:1028`) compounds it: *"**C3 retention is binding:** … temporary working files are deleted 7
days after task-terminal closure and no active task…"* — stated as binding policy while the change to that
policy is awaiting Legal.

This is the one finding with genuine legal/compliance exposure: the PRD attributes a named Legal signature
(Jérôme Piquot, 2026-06-24, Louveciennes, per `c3-retention.md`) to a destructive-cleanup rule that signature
does not cover. The wording predates this update, but this update made PD11 approved and therefore made the
claim load-bearing, and §5.1 item 9 put the C3 trigger inside the PRD's scope.

**Required fix.** Mark the C3 row superseded / A7b-pending and separate the Legal-approved durations from the
sponsor-approved-only trigger. NFR60's correction goes in the same PD6 relock as NFR8/NFR54.

### H-3 (HIGH) — no "target state, not current behavior" statement anywhere in the PRD

Covered under B.1. The PD8/PD10/PD11 rows read as accomplished fact. The two new NFR blocks got exactly the
right caveat; the decision table needs the same one. Without it, "the Contract Spine is amended" and "code and
tests express one model" become quotable as implementation evidence — the precise honesty rule this PRD
enforces elsewhere (`prd.md:159`: *"Safe-empty, seed-only, unavailable, no-op, fake-backed, numerically
mapped, structural, or documentation-only evidence may prove safety or contract shape but does not prove
positive runtime capability."*).

### H-4 (HIGH) — the PRD delegates scheduling to a field that does not exist, and states a rule architecture has flagged as unsatisfiable

`prd.md:159`: *"the scheduling authority is the `execution_rank` wave model in `planning-story-manifest.yaml`,
under which Epics 4, 6, and 10 depend on Epic 12 foundations and **no item may depend on an equal or later
rank**."*

Verified: `planning-story-manifest.yaml` is still `generated_on: '2026-08-04'` with **zero** occurrences of
`execution_rank`, `execution_waves`, `story_lifecycle_status`, or OQ11–OQ13. §5.5 (regeneration) is
unstarted — the PRD run's own memlog records this.

`architecture.md` handles the same delegation two ways the PRD does not: it annotates the authority row
*"`planning-story-manifest.yaml` — **regeneration owed (§5.5); still `generated_on: '2026-08-04'`**"*, it
publishes an **interim** wave table explicitly labelled transitional authority the manifest supersedes, and it
routes the rule itself: *"**Open — the rank rule as written is not satisfiable by the wave table it governs
(route to Delivery + PM).** The approved §7.1 table places 12.2, 12.3, and 12.6 at rank 10 *behind* 12.1 at
rank 10; 4.21 behind 4.19–4.20 at rank 30; and 6.14 behind 6.12–6.13 at rank 30 — six equal-rank prerequisites
that the strictly-lower-rank rule rejects. It also leaves 3.11, 3.13, 10.6, 10.7, and 11.15 unranked while rank
20–30 entries depend on them, and leaves **all of Epic 13 unranked** while rank 40 depends on Epic 13
evidence."*

So the PRD currently points product authority at an empty field, and restates as settled product direction a
rule the mechanism authority has formally routed as unsatisfiable. Either is survivable alone; together they
mean the PRD gives no usable schedule at all while claiming to have delegated one.

**Required fix.** Add the same "regeneration owed" qualifier and either cite architecture's interim table or
record the rank-rule question as open.

### M-1 (MEDIUM) — PD8's rendering clause has no home in the normative text, and `withheld` is missing from a PRD-owned closed field

PD8 (`prd.md:1134`) ends *"Rendering must still distinguish **withheld**, redacted, unavailable, and absent."*
Nothing in the PRD body says so:

- `prd.md:605`: *"Redacted, unknown, missing, hidden, stale, and unavailable are distinct states"* — no
  `withheld`.
- `prd.md:722` (C9): *"redacted/hidden/unknown/missing/stale/unavailable are distinct"* — no `withheld`.

`architecture.md` S-6 makes `withheld` a **new** render state with a precise meaning distinct from the shipped
`redacted` (*"`withheld` means 'no cleartext value exists to show anyone, here is its correlation token'"*),
and S-7 puts it in a closed enum: *"`visibility` is an enumerated field — `metadata_only`, `redacted`,
`withheld` — not free text."*

This matters because `details.visibility` is one of the identifiers `prd.md:145` declares *"product-owned
exceptions to Contract Spine authority [that] must appear verbatim in the Spine."* The PRD owns that field's
vocabulary; architecture's enum currently has no product authority behind its third member.

### M-2 (MEDIUM) — item 11's "resulting authority digest" and named approver are missing

The Record column carries the **proposal's** SHA-256 (`5d12ae4dd4e8f306b8dde5caf8f187d13e40c1fabc6d8e56c0757e24f2004104`
— independently recomputed and **correct**), the A-id, the timestamp `2026-09-15 16:08:21+02:00`, the response
`continue`, and the proposal path. Item 11 asked for four fields: *"approver, date, proposal path, and
resulting authority digest."*

- **resulting authority digest** — absent. There is no digest of the resulting PRD/authority anywhere. The
  proposal digest is a different thing and cannot substitute: §9 requires "PRD, architecture, UX specification,
  epics, manifest, and sprint tracker carry the new approved provenance," and §10.1 requires the rerun to
  "consume the newly approved digests of PRD, architecture, UX, epics, manifest, and sprint status." Nothing
  produced one.
- **approver** — the rows and the intro say only "sponsor" / "Sponsor approval". **Jerome is never named**
  anywhere in the decisions section (`grep` for "Jerome" in `prd.md` returns only the frontmatter edit history
  and `**Author:** Jerome` at line 116). §8's "Sponsor approval record" names him. The "Approvers and
  attestation status" column lists the *pending* roles, not the approver who signed.

### M-3 (MEDIUM) — superseded history is half-retained; "Option (a)" is now a dangling reference

Old rows: `ID | Decision needed | Options | Owner | Decide by`. New rows keep only the question. The **Options**
cell — which recorded the alternative that was *rejected* — is deleted for all seven. Examples now lost from
the decision log:

- PD8: *"(a) Event write (no cleartext ever durable); (b) audit/projection write plus read-time replacement in
  the incident view, proven by OQ9. **The C9 row states (b) as the latest permitted point.**"* — note this old
  text is precisely the pointer to C-1 above; deleting it removed the breadcrumb to the row that still needs
  fixing.
- PD11: *"(a) Amend and re-approve C6 and C3 to match this PRD; (b) PM accepts specific matrix behaviours and
  the completion model is amended."*
- PD4: *"(a) Reopen 10.7 and 10.8 (or relabel them component increments) and keep OQ5 open; (b) ratify the
  narrowed done-bars…"*

Consequence: PD8, PD10, and PD11 all now open with *"Option (a)"* referring to an option list that no longer
exists in the document. Each row is self-describing enough to be readable, but the reference is dangling and
the rejected branch is unrecoverable from the PRD. Item 11 said *"retain the old PD text as superseded
history"* and *"Do not delete the historical questions"* — the questions survived; the rest of the text did
not.

### M-4 (MEDIUM) — PD10 drops "reapproved" from the gate-consumption rule

Item 8: *"state that release generation and parity gates consume only the **corrected, reapproved** matrix."*
`prd.md:1135` says *"release generation and parity gates consume only the **corrected** matrix."*
`architecture.md` keeps both words: *"Release generation and parity gates consume **only** the corrected,
reapproved matrix; the superseded `1.0.0` digest must not be reused as release evidence."*

On its own this is a one-word omission. Combined with H-1 (OQ3 still shown Closed at `5ffabd71…`), it removes
the only textual barrier to a corrected-but-unreapproved matrix being consumed as release evidence.

### L-1 (LOW) — the 73→84 declaration lives only in the decision rows and frontmatter

Item 10 asked to "update the NFR traceability declaration from 73 to 84." In `prd.md` the number 84 appears
only in the PD3 row (*"PRD/epics/traceability parity was relocked at 84"*), the PD6 row, and the frontmatter
edit-history entry. The `## Non-Functional Requirements` section itself still carries no inventory-count
statement, so there is nothing in the requirements section for a reader or a gate to read. The substantive
relock **is** correct and complete in `nfr-traceability.md` ("84 bullets across eleven categories", "`NFR1`
through `NFR84`", eleven-category rollup), `epics.md`, `NfrTraceabilityConformanceTests` (17/17 green per the
memlog), and `run-nfr-traceability-gates.ps1`. Locate-ability defect only.

### L-2 (LOW) — section title and index sentence read as "closed"

`### PM decisions resolved` + `prd.md:1094` *"PD1, PD3, PD4, PD5, PD8, PD10, and PD11 are recorded under **PM
decisions resolved** below; PD2, PD6, PD7, and PD9 remain open."* Every row and the section intro hedge
correctly, but the index sentence is what a skimmer reads. Suggest `### PM decisions sponsor-approved
(attestation pending)`.

Related: the index preamble's own rule is *"a PD item stays open until the PRD decision log
(`.memlog.md`) records the decision."* `.memlog.md` now records them, so by the PRD's own stated rule these
items are closed — which is not what §8 intends. The rule should be amended to require role attestation, not
just a memlog entry.

### L-3 (LOW) — PD4 compresses §6 and drops the 10.7 honesty clause

PD4 carries all eight canonical results correctly but not the per-row "Evidence treatment" column. The one
that matters is §6's 10.7 note: *"It proves bridge relocation, registration, and restart behavior, **not
FR58/OQ5**"* (§5.4 repeats it: *"explicitly state that it does not close FR58 or OQ5"*). Without it, "Story
10.7 stays `done`" in a product-authority document is the exact reading the posture rule at `prd.md:159`
exists to prevent. Impact is limited because the OQ5 row still holds the FR58 gate independently.

---

## Summary table

| ID | Severity | Finding | Location |
| --- | --- | --- | --- |
| C-1 | **Critical** | PD8's required removal of projection-time-redaction wording not performed; C9 row still calls PD8 open; NFR8/NFR54 state the abolished model as binding requirement | `prd.md:722`, `:958`, `:1019` |
| C-2 | **Critical** | Completion model contradicts the PRD's own PD11 row, §7.3, and `architecture.md:411` on `inaccessible`→`ready`; `auto-reconciling` is not in the closed disposition vocabulary | `prd.md:495`, `:625`, `:1117` |
| H-1 | High | OQ3 still "Closed 2026-09-14" at the superseded `5ffabd71…` digest; A6b reapproval not recorded as pending | `prd.md:1078` |
| H-2 | High | C3 row attributes the 2026-06-24 Legal signature to the new terminal-closure cleanup trigger; A7b pending; NFR60 calls it "binding" | `prd.md:716`, `:1028` |
| H-3 | High | PD8/PD10/PD11 written as accomplished fact; no "target state, not current behavior" caveat | `prd.md:1134`–`:1136` |
| H-4 | High | Scheduling delegated to `execution_rank`, which does not exist in the manifest; rank rule restated as settled though architecture routed it as unsatisfiable | `prd.md:159` |
| M-1 | Medium | `withheld` absent from the PRD's distinct-state lists and from the product-owned `details.visibility` vocabulary | `prd.md:605`, `:722` |
| M-2 | Medium | Item 11's "resulting authority digest" missing; approver recorded only as "sponsor", Jerome never named | `prd.md:1128`–`:1136` |
| M-3 | Medium | Options/owner/decide-by columns deleted from superseded PD rows; "Option (a)" now dangling | `prd.md:1128`–`:1136` |
| M-4 | Medium | PD10 drops "reapproved" from the gate-consumption rule | `prd.md:1135` |
| L-1 | Low | 73→84 declaration present only in PD rows and frontmatter, not in the NFR section | `prd.md` §NFR |
| L-2 | Low | "PM decisions resolved" heading and index sentence read as closed; index's own open/closed rule needs amending | `prd.md:1094`, `:1124` |
| L-3 | Low | PD4 drops §6's "10.7 does not close FR58 or OQ5" evidence clause | `prd.md:1132` |

## Recommended disposition

1. **Same change set (no new approval needed — these are corrections toward the already-approved text):**
   C-1's `prd.md:722`, C-2 (`:495`, `:625`, `:1117`), H-1, H-2's C3 row framing, H-3, H-4, M-1, M-4, L-2, L-3.
2. **Fold into the PD6 relock, and say so in the PD6 row:** NFR8, NFR54, NFR60 — the three hash-pinned bullets
   that carry the superseded confidentiality and retention wording. They need the same four-artifact lockstep
   this run executed for NFR74–NFR84.
3. **Route to the owners:** M-2's resulting-authority digest belongs to the Delivery/manifest pass (§5.5) that
   also produces the provenance §9 requires; architecture's two routed open questions (PD8 operational-path
   scope boundary; the rank rule) need PM/Security/Delivery answers the PRD can then cite.
4. **Do not** treat `architecture.md` as the drifted artifact. On all five cross-checks (PD8/C9, PD11 recovery,
   OQ3 supersession, C3 supersession, manifest readiness) it matches the approved proposal and the PRD does
   not.
