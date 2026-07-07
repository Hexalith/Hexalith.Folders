---
project_name: Hexalith.Folders
workflow: bmad-correct-course
source_change_trigger: "Generalize governance gate freshness and exact approval-record checks so approval-backed criteria cannot pass with stale reports or generic Legal-approved text."
date: 2026-07-07T19:49:32+02:00
mode: batch
status: applied
scope_classification: moderate
prepared_for: Jerome
head_at_analysis: 40cc5e1
decision: apply-now + record-as-Story-11.3-AC + mandatory-global-max-age freshness window
---

# Sprint Change Proposal — Generalize Governance-Gate Freshness + Exact Approval-Record Checks

- **Workflow:** BMAD Correct Course (Sprint Change Management)
- **Author:** Jerome (Developer)
- **Triggering issue (as stated):** *Generalize governance gate freshness and exact approval-record
  checks so approval-backed criteria cannot pass with stale reports or generic Legal-approved text.*
- **HEAD at analysis:** `40cc5e1` (Epic 11 baseline lineage; working tree otherwise clean of pins)
- **Disposition:** **APPROVED + APPLIED this session** (Jerome pre-authorized "apply now" and the
  mandatory global max-age window). Additive, fail-closed, no REST/OpenAPI/wire behavior change.
- **Scope classification:** **Moderate** — coordinated governance-artifact edits (evidence YAML +
  generic conformance test + gate doc) in lockstep; no product-behavior change.
- **Ownership:** Delivered ahead of, and recorded as a new acceptance criterion on, **Epic 11
  Story 11.3** ("Apply wire-preserving repo hygiene and fragile-gate fixes").

---

## Section 1 — Issue Summary

The exact-approval-record + freshness discipline exists in the repo today only as **bespoke,
hardcoded, C3-specific** logic:

- `tests/Hexalith.Folders.Contracts.Tests/Deployment/RetentionAndTenantDeletionConformanceTests.cs`
  literally string-matches the C3 signer/date: `approval record: PM approved (Jerome) 2026-06-22;
  Legal approved (Jérôme Piquot) 2026-06-24, Louveciennes` (and per-row `Legal approved (Jérôme
  Piquot) 2026-06-24, Louveciennes`).
- `tests/tools/run-retention-deletion-gates.ps1` carries a `stale-c3-legal-placeholder` guard.

But the **generic** governance gate — `GovernanceCompletenessGateTests.ExitCriteriaEvidenceMapsEvery
C0ThroughC13WithBoundedReferencePendingRows`, the one that iterates **every** criterion C0–C13 — only
enforced, for each approval-backed row:

- `status ∈ {approved, reference_pending}`
- `artifact_path` exists
- `verification_command` non-blank
- `result_summary` **non-blank** and metadata-only

**The gap.** For any *approval-backed* criterion — one whose `approved` status rests on a human
governance sign-off rather than a machine-validated gate (today **C3** retention and **C4** input
limits; any future one) — the generic gate would happily pass with:

1. **A generic "Legal-approved" phrase.** Nothing forced a structured, named, dated signer; a bare
   `result_summary: "Legal-approved"` satisfied `ShouldNotBeNullOrWhiteSpace()`.
2. **A stale approval.** Nothing checked that an approval date was present, well-formed, non-future,
   or non-expired. C3's only freshness enforcement was a hardcoded literal-date string match — itself
   a *fragile pin* (it hard-codes `2026-06-24`, it does not generalize to any other criterion, and it
   is exactly the kind of brittle text pin Story 11.3 exists to fix).

The repo already had all the *ingredients* for the fix — `tests/fixtures/cache-key-exceptions.yaml`
enforces `last_reviewed_on`/`expiry_date` freshness and `review_status: approved` generically — they
were simply never generalized to the governance exit-criteria rows.

**How discovered:** direct read of the generic `GovernanceCompletenessGateTests` evidence loop against
the C3-specific retention conformance test and the cache-key-exception freshness precedent.

---

## Section 2 — Impact Analysis

### Epic / Story Impact

- **Epic 11** (in-progress) — **Story 11.3** ("Apply wire-preserving repo hygiene and fragile-gate
  fixes") gains one explicit acceptance criterion capturing the generalization. No new/removed/renamed
  epic or story; no renumbering. The AC is delivered ahead by this correct-course and is already green.
- **Epics 1–10** — no impact. No FR/NFR scope change.

### Artifact Conflicts

| Artifact | Change |
| --- | --- |
| `docs/exit-criteria/c0-c13-governance-evidence.yaml` | Add a top-level `approval_policy` block (`max_age_days`, `generic_approver_tokens`) and an additive `approval` block to the two approval-backed rows (C3, C4). |
| `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` | Add generic freshness + exact-approval enforcement (positive over the real YAML + fail-closed negative controls), a pinned `ApprovalBackedCriteria` set, and doc-lockstep assertions. |
| `docs/contract/governance-and-completeness-ci-gates.md` | Document the six new diagnostic categories and an "Approval Records" section (kept in lockstep with the test's `ShouldContain` assertions). |
| `_bmad-output/planning-artifacts/epics.md` | Story 11.3 AC (above). |
| `run-governance-completeness-gates.ps1` | **No change** — the new facts run under the script's existing `FullyQualifiedName~…GovernanceCompletenessGateTests` filter. |
| OpenAPI / SDK / Client / CLI / MCP / UI / fixtures / `.slnx` / route + package inventories | **No change.** No wire-contract, parity-oracle, generated-artifact, or pinned-inventory impact. |

### Non-impact (verified, not assumed)

- `RetentionAndTenantDeletionConformanceTests.GovernanceEvidenceShouldPointC3AtRetentionDeletionGate`
  reads only C3's existing scalars + empty `open_policy_placeholders`; the additive `approval` block
  does not touch those reads. **Green (confirmed).**
- `ExitCriteriaEvidenceMapsEveryC0ThroughC13…` reads only the `criteria` sequence and per-row scalars;
  it asserts no exact key-set, so the sibling `approval_policy` top-level key and per-row `approval`
  key are safe. **Green (confirmed).**
- `NfrTraceabilityConformanceTests.ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` pins C3/C4/C7/C12
  as reference-pending **NFR-traceability** rows in a *separate* doc (`nfr-traceability.md`), not the
  governance evidence YAML. **Untouched; green (confirmed).**
- The bespoke C3 retention gate remains the stricter retention-specific check; this adds a **generic
  floor** across every approval-backed criterion, present and future.

### Technical Impact

- No product/wire behavior change. The change strengthens (adds fail-closed checks to) a CI-contract
  gate and enriches an evidence artifact; it is squarely "fragile-gate hardening."
- **Intentional time-based property:** the mandatory global `max_age_days: 365` window means C4
  (2026-06-22) and C3 (2026-06-24) will redden around 2027-06-22/24 unless the sign-offs are refreshed
  or the window is widened by decision. This is the requested "cannot pass with stale reports"
  behavior — an annual governance re-review prompt, not a defect (documented in code + the gate doc).

---

## Section 3 — Recommended Approach

**Direct Adjustment** — generalize the freshness + exact-approval discipline that already exists for
cache-key exceptions (and bespoke for C3) into a reusable, schema-driven check over the governance
exit-criteria, and record it as a Story 11.3 AC. Do not roll back; do not create a new story; do not
touch pinned `.slnx`/route/package inventories.

- **Effort:** Low–Moderate (one evidence YAML, one conformance test, one gate doc; in lockstep).
- **Risk:** Low. Additive and fail-closed; no wire/product change; no pinned-inventory change. The
  only residual is the intended annual max-age redden, which is the explicitly-requested behavior.
- **Timeline:** Applied this session; readiness/gates green immediately.

**Alternatives considered:**
- *Freshness = present + valid + non-future + non-generic only (no global max-age):* rejected per
  Jerome's decision — would not enforce "cannot pass with **stale** reports" over time.
- *Provenance-commit staleness only (report `source_commit` match):* not selected as the primary
  model; the release-package gate already covers `source_commit` staleness for the retention report.
- *Leave C3's bespoke literal-date pin as the only enforcement:* rejected — it does not generalize and
  is itself a fragile text pin (the Story 11.3 anti-pattern).

---

## Section 4 — Detailed Change Proposals (Batch — APPLIED)

### Edit A — `c0-c13-governance-evidence.yaml`: add the `approval_policy` block

Inserted between `diagnostic_policy` and `criteria`:

```yaml
approval_policy:
  # Mandatory global freshness window: an approval whose newest required-authority sign-off is older
  # than this many days fails closed and forces a governance re-review (intentional time-based redden).
  max_age_days: 365
  # Approver values too generic to be an exact approval record. A record whose approver (trimmed,
  # case-insensitive) is one of these — or equal to its own authority name — fails closed.
  generic_approver_tokens:
    - approved
    - approve
    - legal
    - legal-approved
    - legally approved
    - pm
    - pm-approved
    - pending
    - pending-review
    - tbd
    - todo
    - placeholder
    - signed
    - sign-off
    - signoff
    - none
    - 'n/a'
    - unknown
    - yes
```

### Edit B — `c0-c13-governance-evidence.yaml`: add `approval` blocks to C3 and C4

C3 (after `open_policy_placeholders: []`):

```yaml
    approval:
      required_authorities:
        - PM
        - Legal
      records:
        - authority: PM
          approver: Jerome
          approved_on: '2026-06-22'
        - authority: Legal
          approver: Jérôme Piquot
          approved_on: '2026-06-24'
          location: Louveciennes
```

C4 (after `open_policy_placeholders: []`):

```yaml
    approval:
      required_authorities:
        - PM
      records:
        - authority: PM
          approver: Jerome
          approved_on: '2026-06-22'
```

### Edit C — `GovernanceCompletenessGateTests.cs`: generic enforcement

- Pinned `ApprovalBackedCriteria = ["C3", "C4"]` (extend in lockstep with future sign-offs).
- `[Fact] ApprovalBackedCriteriaCarryFreshExactApprovalRecords` — over the real YAML: every pinned
  criterion is `approved` and carries a well-formed `approval` block; every row with an `approval`
  block satisfies, per required authority, exactly one record with a **specific** approver (rejecting
  `generic_approver_tokens` and authority-name-as-approver) and a valid, **non-future**, **non-stale**
  (`<= max_age_days`) `approved_on`; an optional per-criterion `review_by` must be a future date.
- `[Fact] ApprovalRecordNegativeControlsFailClosedWithBoundedDiagnostics` — fail-closed synthetic
  controls for each mode: missing block, generic approver, future date, malformed date, unsatisfied
  authority, stale approval, and an expired `review_by`; a fully-valid fresh record yields no
  diagnostics; all diagnostics asserted metadata-only.
- Reusable `EvaluateApprovalRecords(row, policy, today)` evaluator (mirrors the existing
  `EvaluateExitCriteriaRows`/`EvaluateSampleConsumption` shape) emitting bounded `GateDiagnostic`s.
- Six new bounded diagnostic categories: `approval_record_missing`, `approval_authority_unsatisfied`,
  `approval_approver_generic`, `approval_date_invalid`, `approval_date_future`, `approval_stale`.
- Doc-lockstep: `WorkflowAndScriptExposeOneOfflineGovernanceCompletenessCommand` now also asserts the
  gate doc lists the six new categories.

### Edit D — `governance-and-completeness-ci-gates.md`: document the model

- Extended the intro to name "fresh, exact approval records for approval-backed criteria".
- Added the six new diagnostic-category bullets.
- Added an "## Approval Records" section describing `approval_policy`, the per-authority record shape,
  the mandatory `max_age_days` window, and the optional `review_by`; notes the bespoke C3 retention
  gate remains the stricter retention-specific check.

### Edit E — `epics.md` Story 11.3: add the acceptance criterion

Added an `**And**` clause binding the generalized governance-gate freshness/exact-approval enforcement
to Story 11.3, noting it was delivered ahead via this proposal.

---

## Section 5 — Change Navigation Checklist Results

- 1.1 Triggering item: [x] Trigger is a direct correct-course directive (governance-gate hardening).
- 1.2 Core problem: [x] Category: **fragile/insufficient governance gate discovered during review** —
  the generic gate could pass approval-backed criteria on stale/generic approval text.
- 1.3 Supporting evidence: [x] Code trace: generic `GovernanceCompletenessGateTests` evidence loop vs.
  bespoke `RetentionAndTenantDeletionConformanceTests` C3 literals vs. `cache-key-exceptions.yaml`
  freshness precedent.
- 2.1 Current-epic impact: [x] Epic 11 Story 11.3 gains one AC; no reopen of Epics 1–10.
- 2.2 Epic-level changes: [x] One AC; no new/removed/redefined epic.
- 2.3 Remaining epics: [x] Story 11.3 (fragile-gate fixes) already owns this charter.
- 2.4 Obsolete/new epics: [N/A] None.
- 2.5 Priority/order: [x] No resequencing; additive gate hardening, safe ahead of the refactor stories.
- 3.1 PRD conflicts: [N/A] No FR/NFR scope change.
- 3.2 Architecture conflicts: [N/A] No architecture element changed.
- 3.3 UX conflicts: [N/A] None.
- 3.4 Other artifacts: [!] Evidence YAML + generic conformance test + gate doc (Edits A–D); epics.md
  Story 11.3 AC (Edit E). No OpenAPI/SDK/fixtures/pinned-inventory.
- 4.1 Direct Adjustment: [x] Viable. Effort Low–Moderate, Risk Low.
- 4.2 Rollback: [N/A] Nothing to revert.
- 4.3 PRD MVP review: [N/A] No scope reduction.
- 4.4 Recommended path: [x] Direct Adjustment — generalize + record as Story 11.3 AC.
- 5.x Proposal completeness: [x] Issue, impact, path (+ alternatives), detailed edits, handoff — all
  included.
- 6.1 Checklist completion: [x] Complete.
- 6.2 Proposal accuracy: [x] Drafted + verified from HEAD `40cc5e1` code evidence and live gate runs.
- 6.3 User approval: [x] Pre-authorized ("apply now" + "mandatory global max-age window").
- 6.4 Sprint-status update: [x] No story/epic **state** change (Story 11.3 stays `backlog`; the AC is
  pre-satisfied and will read green when 11.3 runs).
- 6.5 Handoff confirmation: [x] Below.

---

## Section 6 — Verification Evidence (this session)

- `dotnet build tests/Hexalith.Folders.Contracts.Tests` → **0 warnings, 0 errors** (warnings-as-errors).
- `dotnet test` (Governance + Retention + NFR classes) → **37 / 37 passed**.
- `dotnet test` full `Hexalith.Folders.Contracts.Tests` lane → **279 / 279 passed** (+2 new facts, no
  regression).
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` → **13 / 13** governance
  facts passed (11 original + 2 new); `_bmad-output/gates/governance-completeness/latest.json`
  `status: passed`.
- `dotnet format whitespace --verify-no-changes` → **clean**; changed files are LF-only.

---

## Section 7 — Implementation Handoff

**Scope classification: Moderate** — coordinated governance-artifact edits, no product-behavior change.
Applied this session; no further code work required to close the trigger.

- **Developer (Amelia) / Test Architect (Murat):** on the next Story 11.3 dev-story run, treat the new
  AC as already-delivered; re-confirm the governance gate stays green (it will re-run under the same
  filter) and that the annual max-age redden is understood as an intentional re-review prompt.
- **Architect (Winston) / PM (John / Jerome):** confirm C3 and C4 are the correct and complete
  approval-backed set today, and that `max_age_days: 365` is the intended re-review cadence.
- **Future maintainers:** when a new criterion becomes approval-backed, add its `approval` block to
  the evidence YAML **and** its id to `ApprovalBackedCriteria` in the test, in lockstep.

### Success criteria (all met at close)

1. Approval-backed criteria carry structured, named, dated `approval` records — ✅ (C3, C4).
2. Generic gate rejects generic approver text, missing/future/malformed/stale dates, unsatisfied
   authorities, and dropped approval blocks — ✅ (positive + negative facts green).
3. Mandatory global `max_age_days` freshness window enforced generically — ✅.
4. Gate doc documents the new categories + model in lockstep with the test — ✅.
5. No REST/OpenAPI/SDK/CLI/MCP/UI/worker/aggregate or pinned-inventory change; format clean — ✅.
6. Recorded as a Story 11.3 acceptance criterion — ✅.
