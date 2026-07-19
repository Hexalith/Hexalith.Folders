---
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-incremental-review.md
source_date: 2026-07-14
source_status: approved-handoff-pending-source-edits
reconciled_against:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/.memlog.md
addendum: absent
disposition: targeted-prd-metadata-clarification
---

# Reconciliation — Implementation-Readiness Incremental Review

## Source purpose and status

This major correct-course proposal responds to a July 14 portfolio readiness result of **NOT READY**. It preserves the product direction and valid contract/control-plane work while correcting planning truth: product epics had forward dependencies on later refactoring, safe-empty or seed-only seams were being mistaken for delivered production capability, story authority and counts conflicted, and enabling/governance/refactoring work was mixed into product-completion reporting.

Administrator approved all eight incremental proposals and the hybrid path on 2026-07-14 at 20:15 +02:00. Approval authorized a cross-role implementation handoff, but the source explicitly says it modified only this proposal. PRD, architecture, UX, epics, manifest, context, traceability, and related planning changes remained to be applied. It authorized no application-code, infrastructure, deployment, package, submodule, or external-repository change.

## PRD-relevant product decisions

1. The MVP remains a **durable repository-backed round trip**. It is not reduced to a contract/control-plane demonstration.
2. Valid completed contract, adapter, authorization, governance, accessibility, and safe-fallback work is preserved, but that increment alone does not prove product-MVP completion.
3. Product acceptance requires production evidence for durable content/state, restart survival and deterministic rebuild, authoritative retrieval, a real provider-confirmed Git commit, terminal task state, populated production read models, and cross-surface behavior.
4. Safe-empty, unavailable, seed-only, fixture-only, screenshot-only, in-memory, or fake-only behavior can prove confidentiality, rendering, or fallback honesty; it cannot prove populated production capability.
5. A blocked mandatory outcome keeps the owning story/capability incomplete. It cannot be carried as an alternate passing branch unless an approved scope change removes the outcome.
6. FR58 requires a real non-empty produce/index/authorize/hydrate/redact/search round trip for its approved metadata-token scope. Body-content indexing/recall is not silently included and remains separately governed.
7. Implementation readiness was explicitly **not-ready** as of 2026-07-14. Document finality and product/readiness completion are different states.
8. Current delivery truth must not alter the durable product scope: portfolio classification, story ownership, dependency ordering, and production evidence are delivery-plan concerns.

## Already covered in the current PRD

- **MVP Contract Summary**, **Product Scope**, **MVP Strategy & Philosophy**, and **MVP Feature Set** already define the product as a complete repository-backed task lifecycle rather than a control-plane-only increment.
- **User Success**, **Technical Success**, and **SM1–SM6** already require a complete cross-surface workflow, provider-confirmed durability, deterministic completion/failure evidence, current status, bounded performance, and populated diagnostic outcomes.
- **FR24** requires valid preparation with no unauthorized side effect; **FR32–FR35** require real governed mutation and live-workspace context behavior.
- **FR37** makes provider-confirmed durable update of the bound remote/ref the only successful commit and rejects local-only completion. **FR39–FR46** require durable evidence, explicit incomplete/unknown/reconciliation states, and inspectable failures.
- **FR52–FR57** require real read-only operational/audit capability, immutable incident evidence, projection-backed normal timelines, and provider-support evidence.
- **FR58** now precisely defines the approved metadata-token round trip: mutation-metadata-derived tokens, current-authority trimming/hydration, stale/archived/revoked/unauthorized/hidden-hit removal, C9 metadata-only egress, and explicit fail-safe unavailability.
- The paragraph following **FR58** and the **Explicit MVP Non-Goals** now supersede the source's broader “content search” shorthand: indexed file bodies, body snippets, and body recall require separate Security/PM approval and a future product requirement.
- **Reliability, Idempotency, and Failure Visibility** and **Observability, Auditability, and Replay** require inspectable non-terminal outcomes, historical replay compatibility, deterministic empty-read-model rebuild, and duplicate-safe projections.
- **OQ5** explicitly blocks FR58 implementation readiness until authorized non-empty round-trip evidence exists. **OQ6** blocks console implementation readiness until approved projection-backed positive, degraded, and replay evidence exists.
- **OQ7–OQ9** separately preserve lock-identity, idempotency, and privileged incident-access gaps rather than letting safe structural behavior imply delivery completion.
- **C3** and **C4** are already corrected to approved status with canonical evidence. The stale source baseline that described them as TBD no longer applies.
- **Workspace State and Concurrency** already defines derived operator dispositions. It refines the proposal by distinguishing `unknown_provider_outcome` as `auto-reconciling` during bounded checks rather than collapsing it into `auto-recovering`.
- The memlog records the durable canonical MVP job, provider-confirmed commit semantics, metadata-only FR58 scope, distinction between live-workspace search and indexed recall, deterministic rebuild/parity evidence, and every OQ1–OQ10 owner/revisit condition.

## Genuine PRD gaps

### Explicit document-status versus implementation-readiness metadata

The current frontmatter says `status: final`, `updated: '2026-07-14'`, and `finalized: '2026-07-14'`, but it does not carry the approved readiness result or explicitly distinguish final PRD status from implementation readiness. The Open Release Items make non-acceptance inferable, yet they do not preserve the approved portfolio-level **NOT READY** assessment or its date.

Add non-normative frontmatter metadata, without changing any requirement ID:

```yaml
implementationReadiness: not-ready
implementationReadinessAssessedAt: '2026-07-14'
productMvpDecision: durable-repository-round-trip-required
productMvpDecisionRatifiedAt: '2026-07-14'
```

Optionally add one short current-delivery note near the Project Context or Open Release Items: `status: final` means the PRD artifact is finalized; it does not claim implementation or release readiness. Completed control-plane/safe-fallback evidence is preserved but cannot close the production-evidence gates below.

No FR/NFR addition is needed. All durable product outcomes and current evidence blockers are already represented.

## Conflicts and supersession

### 1. The source's stale PRD baseline is superseded

The source describes `status: complete`, a 2026-05-07 last edit, C3/C4 as TBD, unresolved architecture-decision prose, and an ambiguous FR58. The current PRD was substantially reconciled and finalized later on July 14; its current C3/C4, FR58, scope, state, evidence, and OQ wording governs.

### 2. FR58 “content search” is narrowed by later approved decisions

The source calls metadata-token recall an enabling increment and proposes a C9-gated body-materialization story within Epic 10. The current PRD and memlog are stricter: FR58 itself is metadata-token recall; indexed body content is outside the current release and requires a **future product requirement** plus Security and PM approval. Do not import “authorized content search” or Story 10.9 body scope into FR58.

### 3. The proposed five-disposition shorthand was refined

The current PRD distinguishes `unknown_provider_outcome` as `auto-reconciling` during bounded automatic checks, while `reconciliation_required` is `awaiting-human`. This refinement preserves the later memlog decision that unknown outcome must precede reconciliation required. Do not overwrite it with a five-value mapping that collapses those states.

### 4. Epic ownership is not PRD traceability

The source maps product completion to Epics 2–6, 10, and 12 and assigns durable/search/projection ownership. The PRD should retain capability outcomes and evidence gates without naming epics as implementation mechanisms. Current FRs and OQs are the stable contract; epic classification belongs in the manifest, epics, sprint status, and traceability artifacts.

### 5. `status: final` is artifact status, not a reversal of NOT READY

The later BMad finalization event does not by itself supersede the approved readiness result. Until a newer readiness assessment says otherwise, the July 14 NOT READY result remains current. The proposed metadata clarification prevents the two status dimensions from being conflated.

## Implementation, architecture, UX, story, and planning detail that stays out of the PRD

- The story manifest schema, authority selection rules, duplicate/missing-source validation, story counts, aliases, and generated inventories are planning-system mechanics.
- Epic/workstream classifications, renamed epics, reopened statuses, Stories 2.8/2.8b, 3.10–3.14, 4.18–4.21, 5.8–5.11, 6.12–6.14, 10.7–10.9, Story 11.10 narrowing, and split-child status rules belong in epics/manifest/sprint status.
- The 12.1→12.4 dependency tree and exact ownership across Epics 4, 6, 9, 10, 11, 12, and 13 are architecture/backlog details.
- `UX-DR33`, `/_admin/incident-stream`, FrontComposer/Fluent component behavior, responsive/focus/screen-reader specifics, and rendering-versus-production-evidence wording belong in the UX specification, with the PRD retaining FR56, FR52–FR55, accessibility NFRs, and OQ9 outcomes.
- Deployed bridge registration, DCP lane, EventStore substrate, Memories topology, provider fakes, in-memory `Save(...)`, and safe-empty seam mechanics are implementation/verification details.
- Planning-consistency gate categories, deferred-work reconciliation, context updates, readiness-report preservation, role routing, and the implementation sequence are delivery governance.
- Historical counts of 58 FRs, 70 extracted NFRs, 116 story headings, and individual epic/story completion claims are snapshot evidence, not stable product requirements.

## Recommended stable-ID edits or additions

- **Add no FR, NFR, OQ, journey, metric, or exit-criterion ID; renumber nothing.**
- Add only the four approved readiness/MVP-decision frontmatter fields above, plus the optional one-paragraph status clarification.
- Retain **FR37** as the provider-confirmed durability boundary and **FR58** as metadata-token recall.
- Retain **OQ5–OQ9** as the stable implementation/readiness evidence gates; do not replace them with epic/story references.
- Retain the current operator-state/disposition mapping, including `auto-reconciling`, because it reflects the later unknown-outcome refinement.
- A future body-content recall capability must receive a new stable FR after Security/PM product approval; it must not be smuggled into FR58 through an implementation story.

## Qualitative ideas at risk

- Honest fallback is valuable safety evidence, but an empty or unavailable product journey is not a completed capability.
- A finished contract/control plane is a legitimate increment; calling it the completed product would erase the durable workspace job customers actually need.
- “Final document” and “ready implementation” are different claims and should remain visibly separate.
- Historical work should be preserved rather than rewritten, while new production-closure work must earn its own completion evidence.
- Mandatory blocked outcomes remain incomplete; alternate passing branches must not turn environmental or dependency blockers into false green.
- Production acceptance needs restart/replay, durable-state, real-provider, populated-read-model, and no-leak evidence together.
- Source-of-truth clarity is itself a readiness control: each story must have one authoritative source, but that mechanism stays outside the product contract.
- FR58's trust value is the complete authorized/hydrated/redacted round trip, not indexing topology or metadata production alone.

## Disposition

**Targeted PRD metadata clarification.** The current PRD already incorporates the approved durable-MVP scope, C3/C4 corrections, FR58 metadata-token boundary, operator-state refinement, production evidence requirements, and OQ5–OQ10 blockers. Preserve all stable requirement text. Add the approved `implementationReadiness`, assessment date, durable-MVP decision, and ratification date fields—and optionally one short status clarification—so `status: final` cannot be mistaken for implementation or release readiness.
