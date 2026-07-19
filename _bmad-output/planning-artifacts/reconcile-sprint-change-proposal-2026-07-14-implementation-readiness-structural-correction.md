---
source: sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md
source_date: 2026-07-14
source_status: approved
reconciled_against:
  - prd.md
  - .memlog.md
addendum_present: false
disposition: apply-readiness-metadata-and-delivery-posture-retain-current-fr58-scope
---

# Reconciliation — Implementation-Readiness Structural Correction

## Source purpose and status

This approved major-scope `bmad-correct-course` proposal responds to the 2026-07-14 implementation-readiness result of **NOT READY**. It preserves the durable repository-backed product direction while correcting delivery-status truth, product-versus-enabling portfolio classification, forward dependencies, story authority, production-projection ownership, FR58 delivery structure, and planning consistency.

Administrator explicitly approved the proposal on 2026-07-14. The workflow registered epic/story/status changes and handed remaining source-artifact edits to Product, Architecture, Delivery, and Test owners. The proposal changes planning and tracking only; it does not authorize code, infrastructure, deployment, submodule, or external-repository mutations.

The current PRD is also dated/finalized 2026-07-14 and contains many later reconciliations, including OQ5/OQ6 implementation blockers and a narrower, explicitly metadata-only FR58. Therefore this source must be applied selectively: its readiness truth and durable-MVP posture remain relevant, while its body-content interpretation of FR58 is superseded by the current PRD and memlog.

## PRD-relevant product decisions

1. **Document finality is not implementation readiness.** A complete/final PRD and 58/58 FR mapping do not imply that the product is production-ready or that the product MVP is accepted.
2. **The product MVP remains the durable repository-backed round trip.** Acceptance requires authorized durable lifecycle/file state, restart survival, provider-confirmed Git persistence, terminal state, authoritative retrieval/context, and rebuildable production projections; a control-plane-only increment is insufficient.
3. **Safe-empty, unavailable, seed-only, no-op, and fake-backed paths prove safe fallback, not completed capability.** They are valuable safety evidence but cannot support a product-done claim.
4. **Completed enabling work remains valid evidence.** Contract, adapter, authorization, governance, accessibility, topology, and remediation work should not be invalidated merely because production vertical slices remain incomplete.
5. **Production capability ownership belongs with the consuming product outcome.** Product projections and real round trips should not be hidden inside a later technical-refactoring workstream.
6. **Delivery evidence must be independently executable.** A story may not claim behavior that depends on a later story; production mutation and read-model stories require real-path, restart/replay, authorization, failure, audit, and sensitive-data evidence.
7. **Planning authority must be deterministic.** One authoritative story/AC source, stable historical IDs, explicit aliases, generated/validated counts, and cross-artifact status consistency are necessary to trust readiness conclusions.
8. **Implementation-readiness status must remain visible until re-assessed.** The approved 2026-07-14 result is not-ready and should not be erased by PRD finalization.

## Already covered in the current PRD

### Durable product-MVP direction

- **MVP Contract Summary** already defines the canonical repository-backed lifecycle and provider-confirmed durable commit outcome.
- **SM1** requires complete canonical lifecycle scenarios across GitHub and Forgejo and every required surface.
- **SM3** requires authorized lifecycle runs to reach `committed`, while **CM1** forbids counting local/unconfirmed/unauthorized outcomes as successful.
- **FR18–FR24** cover repository-backed creation/binding, provider readiness, and workspace preparation.
- **FR25–FR31** cover task lock, collision, state, cleanup, and inspectable lifecycle/projection status.
- **FR32–FR42** cover governed mutations, provider-confirmed durable commit, evidence, unknown outcomes, reconciliation, and idempotency.
- **FR45–FR46** require explicit state/failure evidence, and **FR52–FR56** require populated operator/audit outcomes rather than opaque completion claims.
- **Non-Functional Requirements → Reliability, Idempotency, and Failure Visibility** requires inspectable restart/failure/provider outcomes and provider-confirmed durable success.
- **Non-Functional Requirements → Observability, Auditability, and Replay** requires deterministic read-model rebuilds from ordered durable events.

### Readiness and production-evidence blockers

- **OQ5** blocks FR58 implementation readiness until the facade proves authorized non-empty metadata-token behavior and both C13 operations.
- **OQ6** blocks console implementation readiness until seed-only diagnostics are replaced by projection-backed positive, degraded, and replay evidence.
- **OQ7–OQ9** separately block lock-identity, all-mutations idempotency, and incident-access implementation readiness.
- **OQ10** blocks acceptance of success/counter-metric results until the release-calibration plan freezes populations, scenarios, methods, evidence owners, and approvals.
- The closing rule for OQ1–OQ10 requires canonical evidence, named approvers, approval dates, and evidence version/digest; passing tests or nominal delivery alone cannot close an item.

### Other requested corrections already reflected

- **C3** and **C4** already contain approved dates, authoritative evidence links, and full current values; no replacement is needed.
- **Workspace State and Concurrency** already uses the five operator-disposition concepts: available, auto-recovering/auto-reconciling, degraded-but-serving, awaiting-human, and terminal-until-intervention.
- **Project Classification** already calls the PRD a brownfield living product contract and distinguishes current repository/governance truth from historical delivery prose.
- The current FR set remains stable at **FR1–FR58**; the structural proposal does not require a new product requirement.

### Memlog alignment

- The memlog records provider-confirmed durable commit, mandatory unknown-provider-outcome before reconciliation-required, deterministic lock/idempotency behavior, automatic cleanup, production-safe evidence, and OQ5–OQ10 implementation blockers.
- It records the current FR58 decision as metadata-token recall only and explicitly excludes body-content recall pending separate Security and PM approval.
- It records the PRD as finalized, but does not record that implementation readiness changed from the approved 2026-07-14 **not-ready** result.

## Genuine PRD gaps

### 1. Implementation-readiness metadata is missing

The current frontmatter has `status: final`, which correctly communicates document lifecycle, but lacks the separately approved implementation-readiness fields. Add, without changing `status: final`:

```yaml
implementationReadiness: not-ready
implementationReadinessAssessedAt: '2026-07-14'
implementationReadinessSource: '_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md'
productMvpDecision: durable-repository-round-trip-required
productMvpDecisionRatifiedAt: '2026-07-14'
```

Do not replace the BMad workflow's `status: final` with the proposal's `status: complete`; document finality and implementation readiness are different axes.

### 2. Current delivery posture is not explicit enough

The PRD defines the durable product scope and lists release blockers, but it does not directly state that completed control-plane, contract, governance, accessibility, and fallback foundations do not constitute product-MVP completion.

Recommended concise addition near **MVP Contract Summary** or **Product Scope**:

> The current control-plane, contract, authorization, governance, accessibility, and fail-safe diagnostic foundations are valid completed increments, but they do not by themselves complete or release the product MVP. MVP acceptance still requires the durable repository-backed lifecycle and all Open Release Items to close with approved production evidence; safe-empty, unavailable, seed-only, no-op, or fake-backed paths prove safe fallback only.

This addition changes no FR and prevents `status: final` or completed engineering work from being mistaken for release acceptance.

### 3. Source provenance is missing

The approved structural-correction proposal and its triggering readiness report are absent from `inputDocuments` and the PRD edit history. Add both as reconciled inputs and log the selective disposition: readiness/durable-MVP truth retained; the source's FR58 body-content interpretation superseded by the later current PRD decision.

## Conflicts and supersession

### FR58 body-content interpretation is superseded

The proposal says FR58 is authorized Folders content search, that metadata-token recall cannot complete it, and that body-content search remains incomplete unless Story 10.9 is authorized or the PRD is rescoped. The current PRD and memlog made the later explicit scope decision:

- **FR58** is authorized metadata-token recall with security trimming, authoritative hydration, status, and no raw paths/bodies/snippets/source URIs.
- Cross-workspace indexed body content/snippets/recall require separate future Security and PM approval.
- **FR34–FR35**, not FR58, own bounded live-workspace text-body search/snippets.

Disposition: preserve the structural proposal as historical rationale, but do not restore body-content search to FR58 or mark current FR58 incomplete solely because bodies are excluded. Current OQ5 defines the actual FR58 completion gap.

### `status: complete` conflicts with current workflow semantics

The proposal asks for PRD `status: complete`; the active PRD workflow uses `status: final` to distinguish completed documents from in-progress drafts. The current PRD correctly uses `final`.

Disposition: retain `status: final` and add separate implementation-readiness metadata.

### “Architecture decisions are resolved” is broader than current approved state

The proposal's replacement posture says content transport, provider capability, workspace state, idempotency, lock, query bounds, and sensitive metadata are resolved. The current PRD later retains **OQ1–OQ4** for C7 timing, file-policy contract, authorization-matrix inventory, and provider-compatibility catalog. Their fail-closed outcomes are fixed, but their bounded parameters/evidence remain reference-pending.

Disposition: keep the current **Architecture Decisions Needed Next**, OQ1–OQ4, and evidence ownership. Do not apply the proposal's blanket resolved wording.

### Epic 12/13 and story splits are delivery structure, not product requirements

The proposal's portfolio classification, Epic 12 durable round trip, Epic 13 hardening, story splits, and authority manifest can be valid planning corrections without being duplicated into PRD FRs or product scope. The PRD already defines the durable outcomes independently of epic numbering.

Disposition: keep those changes in epics/manifest/sprint/traceability artifacts; the PRD should mention the durable acceptance posture, not depend on a specific epic number.

### NFR51/NFR52 are not stable identifiers in the current PRD

The proposal uses numbered traceability labels from a downstream NFR artifact. The current PRD's NFR bullets are organized by named sections and do not carry NFR51/NFR52 stable IDs.

Disposition: cite **Operations Console Accessibility**, **Observability, Auditability, and Replay**, **Verification Expectations**, and OQ6 rather than introducing downstream NFR numbering into the PRD.

## Implementation, architecture, UX, and planning detail that stays out of the PRD

- `planning-story-manifest.yaml` schema, authoritative-story selection mechanics, alias records, generated counts, and planning-consistency test implementation.
- Portfolio labels and exact Epic 1–13 status table, product-completion metric membership, historical batch tags, and sprint-status transitions.
- Story 2.8/2.8b absorption; Stories 3.10–3.14, 4.18–4.21, 5.8–5.11, 6.12–6.14, 10.6–10.9, 11.14–11.21, 12.1–12.5, and 13.1–13.6 definitions/status.
- The exact delivery dependency graph and story-level production-path acceptance templates.
- Projection ownership by source directory, EventStore/Memories SDK seams, DCP lane, Server registrations, Git executor, content store, publisher/reconciler, and provider split mechanics.
- UX evidence-boundary prose, console screen decisions, and whether FR58 needs a new view.
- Specific planning-validation failures, file inventories, owner-role handoffs, implementation sequence, and non-overwriting readiness-report procedure.
- Story/epic counts and claims derived from the manifest.

No `addendum.md` exists. This technical and planning depth is already preserved in the approved proposal, architecture, epics, sprint status, and future manifest. An addendum would at most point to the major replan; it should not duplicate the full 13-epic/story matrix.

## Recommended stable-ID edits and additions

- **New FRs:** none.
- **FR edits:** none; retain current FR58 wording.
- **OQ edits:** none; OQ5–OQ10 already encode the relevant implementation/readiness gates.
- **Renumbering:** none.
- **PRD metadata:** add separate implementation-readiness and durable-MVP decision fields while retaining `status: final`.
- **PRD narrative:** add one concise Current Delivery Posture paragraph distinguishing completed foundations from product-MVP/release acceptance.
- **Downstream traceability:** cite SM1, SM3, SM6, CM1, FR18–FR46, FR52–FR58, Reliability/Idempotency/Failure Visibility, Observability/Auditability/Replay, Verification Expectations, and OQ5–OQ10.
- **Provenance:** add the approved proposal and triggering readiness report to `inputDocuments`/edit history and record the FR58 supersession explicitly in the memlog.

## Qualitative ideas at risk

- A complete requirements map is not an executable implementation plan.
- Completed control-plane and governance work remains valuable even when the product vertical slice is incomplete.
- “Safe empty” and “fail closed” are safety successes but product-capability failures when positive behavior is required.
- Product-completion metrics should exclude enabling, remediation, release-governance, refactoring, and hardening workstreams.
- Reopening a capability epic does not erase completed stories; it corrects the status of missing production closure.
- One authoritative story source and mechanical cross-artifact validation are prerequisites for trustworthy readiness.
- Production capability must be proven through real authorized, durable, restart/replay-capable paths without no-op or fake-backed substitution.
- Durable product outcomes should remain independent of whatever epic/story numbering the delivery plan uses.

## Concise disposition

**Apply a narrow PRD update.** Add implementation-readiness/durable-MVP metadata, a concise delivery-posture paragraph, and source provenance. Keep all existing stable IDs and `status: final`. Do not import the epic/story replan, and do not restore the proposal's body-content interpretation of FR58; the current metadata-token FR58 and OQ5 are the later authoritative decision.
