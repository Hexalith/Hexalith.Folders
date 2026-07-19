---
source: sprint-change-proposal-2026-07-15.md
source_date: 2026-07-15
source_status: approved
authority: latest-approved-authority-and-delivery-proposal
reconciled_against:
  - prd.md
  - .memlog.md
addendum_present: false
disposition: apply-targeted-prd-authority-readiness-and-release-gate-corrections
---

# Reconciliation — Post-Readiness Authority and Delivery Reconciliation

## Source purpose and status

This is the latest approved authority/delivery proposal. It responds to the immutable 2026-07-15 implementation-readiness result of **NOT READY**, amends the approved 2026-07-14 structural correction, and requires full synchronization of product truth, planning authority, production evidence ownership, stable requirement clauses, release gates, and readiness validation.

Administrator approved all 12 incremental edits and the consolidated proposal on 2026-07-15 at 02:02:07+02:00. The proposal modified only itself: PRD, architecture, UX, epics, manifest, traceability, context, governance, and validation artifacts remain implementation handoff work. It authorizes no code, infrastructure, deployment, package, submodule, or external-repository changes.

The proposal preserves the durable repository-backed MVP and completed historical evidence. It rejects rollback and a control-plane-only MVP. Its central correction is that safe-empty, unavailable, seed-only, no-op, fake-backed, or numerically mapped behavior cannot be claimed as completed product capability without real authoritative production evidence.

## Product-level decisions that remain authoritative

1. **Tenant administrators own tenant policy.** They own provider bindings, credential references, repository naming/default-ref policy, capability policy, folder ACLs, and archive decisions. Scoped platform engineers validate and diagnose readiness but cannot silently mutate policy.
2. **Archive is a production-safe vertical slice.** Eligibility excludes active task/lock and staged, dirty, unknown, or reconciliation work; archive never mutates/deletes the provider repository; post-archive mutations deny safely; C3 field classes expire independently.
3. **Locking uses one canonical serializing identity.** Managed tenant + provider product/instance + canonical repository identity + normalized target ref determines collision. Lifecycle, lock state, operation state, and operator disposition remain distinct.
4. **Cleanup is automatic and platform-owned.** It begins only after task-terminal closure with no active task/lock, respects the C3 seven-day boundary, excludes non-terminal/uncertain work, and exposes separate cleanup projection status.
5. **A task owns a validated change set and one commit boundary.** Many mutations can share one lock; rename is add-plus-remove; no mutation auto-commits; partial/unconfirmed execution blocks commit and enters the mandatory unknown-outcome sequence.
6. **Every mutation shares one idempotency contract; every read rejects a key.** Expired-key precedence applies to equivalent and conflicting intent, and old keys must remain recognizable through approved minimal expiry evidence rather than becoming silently reusable.
7. **Incident evidence requires dual authorization before observation.** The actor needs incident-admin permission and fresh current tenant/folder authorization before stream lookup, count, checkpoint, filter, or shaping.
8. **Authorized discovery is scoped before candidate lookup.** “Global” never means cross-tenant/global-platform browsing; state dimensions remain separate; unknown outcomes auto-reconcile for no more than five read-only checks/15 minutes before human-required reconciliation.
9. **Safe fallback is not positive capability evidence.** Diagnostics, transition evidence, and search require populated, replayable, deployed, tenant-isolated, authoritative results.
10. **OQ1–OQ10 are explicit release gates.** Code or passing tests cannot close them without canonical evidence, named accountable approval, date, version, and digest.
11. **Planning truth is mechanically governed.** Stable historical IDs, one authoritative story/AC source, aliases, generated counts, no later-story completion dependencies, and product-versus-workstream classification are required for trustworthy readiness.
12. **FR58 remains metadata-token recall.** Stories may separately gate body-content materialization under C9, but current FR58 completion is the approved non-empty authorized metadata-token round trip defined by the current PRD/OQ5; body recall is not silently restored to MVP.

## Already covered in the current PRD

### Authority and tenant configuration

- **FR4** already makes tenant administrators owners of bindings, credential references, repository/default-ref/capability policy, ACLs, and archive decisions while scoped operators validate only.
- **FR15** already assigns supported provider/credential/repository policy configuration to tenant administrators and validation to platform engineers.
- **Authentication and Authorization Model** repeats tenant-admin ownership and prevents platform operators from silently changing policy.

### Archive, locking, cleanup, and state

- **FR13–FR14** already contain the approved archive eligibility, provider no-touch rule, governance exceptions, C3 field-by-field expiry, and safe retention-expired behavior.
- **FR19** already requires duplicate/alias detection during repository binding.
- **FR25–FR29** already define canonical tenant/provider/repository/ref collision, exact lock vocabulary, deterministic conflict, and safe release/replay behavior.
- **Workspace State and Concurrency** already separates the 11 lifecycle states, five lock states, generic operation state, and the five operator dispositions; it maps `unknown_provider_outcome` to automatic reconciliation and `reconciliation_required` to awaiting-human.
- **FR30** already defines automatic cleanup eligibility, excluded states, C3 timing, evidence, retry, failure, and no user-triggered repair.
- **C6**, **C7**, and **OQ7** already carry state/lock mapping and remaining canonical-identity evidence closure.

### Change sets, live context, failure, and idempotency

- **Command and Query Contract** already defines multi-mutation tasks, one explicit commit, whole-command acceptance, add-plus-remove rename, and unknown/partial-outcome handling.
- **FR32–FR35** already define multi-file mutation, no auto-commit, add-plus-remove rename, exact C4 live context bounds, authorization/path-policy ordering, safe snippets, `isTruncated`, and telemetry exclusions.
- **FR37–FR42** already define provider-confirmed commit, unknown-before-reconciliation, all-mutations idempotency, authorization-aware replay, conflict, expired-key precedence, and read-key rejection.
- **FR43–FR46** already define stable errors, `idempotency_key_expired`, client action, lifecycle/error evidence, and inspectable failure.
- **Reliability, Idempotency, and Failure Visibility** already repeats the mandatory state and retry semantics.

### Incident evidence, discovery, and production closure

- **Public Surfaces** already states incident-admin plus normal tenant/folder access for degraded evidence.
- **FR52–FR55** already enforce tenant-scoped operations/audit, metadata-only reconstruction, and no protected content.
- **Authentication and Authorization Model** already forbids global cross-tenant browsing and unscoped break-glass access.
- **FR58** already defines authorized metadata-token recall with security trimming, authoritative hydration, hidden/stale removal, and fail-safe status; **FR34–FR35** separately own bounded live-workspace body search.
- **OQ5** already blocks FR58 on non-empty authorized production evidence; **OQ6** already blocks seed-only diagnostics/transition evidence on populated positive, degraded, and replay evidence.

### Release-gate structure

- **OQ1–OQ10** and their closing rule already match the proposal's evidence/approval/version/digest posture almost verbatim.
- **C3/C4/C6/C7/C9/C13** already capture current retention, query, state, timing, sensitivity, and generated-denominator authority.
- **MVP Contract Summary**, **SM1–SM8**, **CM1–CM4**, and **Verification Expectations** already prevent false completion based on partial or drifting evidence.

## Genuine product-level PRD gaps

### 1. UJ2 still assigns configuration to the wrong actor

**UJ2: Platform Engineer Establishes Tenant Provider Readiness** says Ravi, a platform engineer, configures provider bindings, credential references, repository naming/default-branch policy, and minimum capabilities. This contradicts FR4, FR15, Authentication and Authorization Model, and Proposal 4.1.

Recommended edit, preserving stable ID **UJ2**:

- retitle to **UJ2: Tenant Administrator Establishes Provider Readiness**;
- make the named protagonist a tenant administrator who configures bindings, credential references, naming/default-ref policy, and capability policy;
- retain a scoped platform engineer only as the actor who validates/diagnoses readiness without changing tenant policy;
- add wrong-tenant, operator-only, stale, revoked, and hidden-resource safe-denial context without turning the journey into an implementation test matrix.

No new journey or FR is needed.

### 2. FR56 does not itself state dual authorization explicitly enough

The **Public Surfaces** section and OQ9 are explicit, but **FR56** currently says only “the authorized incident view.” Clause-level extraction can miss the mandatory two-authority rule identified by the readiness assessment.

Recommended edit to **FR56**, without renumbering:

> FR56: Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only after the same actor holds incident-admin permission and fresh current tenant/folder authorization before stream lookup, event counting, checkpoint lookup, filtering, or shaping. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation/repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.

This makes FR56 independently complete and aligns it with OQ9.

### 3. OQ8 omits the approved expired-key persistence decision/evidence

FR41–FR42 require an expired key to remain recognizable and return `idempotency_key_expired`, but current **OQ8** mentions only all-mutations idempotency and read-key rejection. The latest proposal adds the necessary closure question: how minimal consumed-key digest/tombstone evidence persists after replay-result expiry so an old key cannot appear unused.

Recommended edit to **OQ8**, preserving its ID:

- add expired-key precedence and minimal metadata-only consumed-key persistence/retention to the decision/evidence description;
- require Contract Spine, SDK, C13, storage/retention evidence, and tests to agree;
- keep Architecture + Security + Test as accountable approvers.

The exact storage mechanism remains architecture-owned; the fixed product outcome is already in FR41–FR42.

### 4. Current delivery/readiness posture is absent from PRD metadata and narrative

The PRD is `status: final`, but the latest approved readiness authority is **not-ready** as of 2026-07-15. Add separate frontmatter fields while retaining `status: final`:

```yaml
implementationReadiness: not-ready
implementationReadinessAssessedAt: '2026-07-15'
implementationReadinessSource: '_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md'
productMvpDecision: durable-repository-round-trip-required
productMvpDecisionRatifiedAt: '2026-07-14'
```

Add a concise **Current Delivery Posture** paragraph near MVP Contract Summary:

> Completed contract, adapter, authorization, governance, accessibility, and fail-safe foundations remain valid increments, but they do not complete or release the product MVP. Release remains blocked until the durable repository-backed lifecycle and OQ1–OQ10 close with approved production evidence; safe-empty, seed-only, unavailable, no-op, fake-backed, or numerically mapped behavior is not positive capability evidence.

Avoid copying the time-bound 42/58 semantic-coverage count into the PRD; the immutable readiness report owns that snapshot.

### 5. Latest provenance is missing

Add the following to `inputDocuments`/edit history:

- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md`
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md`
- the amended 2026-07-14 structural-correction proposal and trigger report if not already added.

Record in the memlog that this latest proposal controls authority/delivery posture and amends the 2026-07-14 structural proposal.

## Supersession of earlier July sources

### 2026-07-14 structural correction

This proposal explicitly **amends** `sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md`:

- it keeps the not-ready/durable-MVP/planning-authority corrections;
- it supplies the 16 clause-level corrections and exact OQ1–OQ10 closure posture;
- it resolves the earlier FR58 ambiguity: current FR58 completion is the non-empty authorized metadata-token round trip, while body-content materialization remains separately C9-gated.

### 2026-07-07 seed-backed read-model proposal

The earlier decision to accept production safe-empty diagnostics/transition evidence as an owned MVP limitation is superseded. Safe-empty remains mandatory fail-safe behavior, but **OQ6** and product-owned projection stories block product completion until positive, degraded, replayable production evidence exists.

### 2026-07-07 domain-focus platform-refactoring proposal

The earlier assignment of product projection/search ownership to technical Story 11.10 is superseded. Product capabilities own their projections/evidence; Workstream 11 owns only EventStore/Memories seam adoption and DCP-capable verification. The proposal's static `47/47` route shorthand is also superseded by the generated current C13 denominator.

### 2026-07-07 gateway-double proposal

The rejection-propagation guard remains valid, but the latest structural split moves gateway-double/rejection conformance out of the broad Story 11.7 helper bucket into focused technical work (Story 11.17 in the amended planning model). This ownership change stays outside the PRD.

### 2026-07-07 honest-green gate proposal

Its blocking/full-lane/no-narrowing accessibility and UI E2E principle remains valid downstream governance. Exact job names, class FQNs, forbidden strings, and the historical 63-test count do not become product requirements.

### 2026-07-07 idempotency-code canonicalization proposal

Its exact read-key rejection correction remains valid. The latest proposal broadens the governing authority to every mutation/read cell, expired-key precedence, and durable minimal expiry evidence under OQ8. Exact error literals remain Contract Spine/error-catalog data rather than new PRD IDs.

## Implementation, architecture, UX, and planning detail that stays out of the PRD

- Epic/workstream 1–13 classification, exact story inventory/status, Story 2.8b alias mechanics, split story numbers, and product-completion metric membership.
- `planning-story-manifest.yaml`, authority selection, generated counts, validation implementation, and the 115-versus-116 historical discrepancy.
- Projection source directories, Server registrations, DCP lanes, EventStore/Memories seam assignments, and exact delivery dependency graph.
- Architecture F-1/F-2 terminology, concrete component tree, query-port names, FrontComposer/Fluent component mapping, and UX phase labels.
- Exact story acceptance matrices, role handoffs, 3–5 day/2–4 sprint estimates, and readiness workflow implementation.
- The immutable 42/58 semantic-coverage snapshot and issue counts from the trigger report.

No `addendum.md` exists. Preserve these details in the latest proposal, architecture, UX, epics, manifest, sprint status, traceability, and readiness reports rather than inflating the PRD.

## Stable-ID and section recommendations

- **UJ2:** edit actor/authority wording; do not renumber.
- **FR56:** edit to state dual authorization and pre-observation denial explicitly; do not renumber.
- **OQ8:** expand closure evidence to include expired-key precedence and minimal consumed-key persistence/retention; do not renumber.
- **FR1–FR55, FR57–FR58:** no edits required from this source.
- **OQ1–OQ7, OQ9–OQ10:** already covered; no renumbering or new items.
- **Frontmatter:** add latest not-ready assessment metadata and durable-MVP decision; retain `status: final`.
- **MVP Contract Summary/Product Scope:** add concise Current Delivery Posture; do not add epic numbers or readiness snapshot counts.
- **Provenance/edit history/memlog:** add the latest proposal/report and record its amendment/supersession decisions.

## Qualitative ideas at risk

- Product truth is clause-level, not satisfied by an FR number appearing in an epic table.
- “Safe empty” proves confidentiality and honest failure, not a functioning positive capability.
- Completed historical work remains valuable when product epics reopen for production closure.
- Tenant-policy mutation and platform readiness diagnosis are distinct authorities.
- Dual incident authorization must happen before any observation, including counting and checkpoint lookup.
- An expired idempotency key cannot become reusable merely because replay payload retention ended.
- Product projections belong to the product capability they complete, not a later technical cleanup workstream.
- Generated inventories and immutable readiness reports are safer than hand-maintained counts and overwritten evidence.
- The current FR58 is metadata-token recall; separately authorized body-content work must not silently change that boundary.
- Finalized product requirements and not-ready implementation status can coexist and should be represented separately.

## Concise disposition

**Apply targeted PRD corrections.** Update UJ2, FR56, OQ8, implementation-readiness/durable-MVP metadata, Current Delivery Posture, provenance, edit history, and memlog. Preserve all stable IDs and `status: final`. The rest of the latest proposal governs downstream planning/architecture/UX synchronization and supersedes earlier July ownership/status assumptions as described above.
