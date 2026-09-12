---
review: architecture-good-spine-rubric
reviewed_at: 2026-09-08
artifact: _bmad-output/planning-artifacts/architecture.md
verdict: needs-reconciliation
severity_counts:
  critical: 0
  high: 2
  medium: 5
  low: 0
architecture_sha256: 34524ac272b93c948769fac55d5011866bd84335e0516b4b4fc598309d2070ac
prd_sha256: d2b682f7189bfec3502f9140050509dde0ca71d23c0a0737443c3d186f2ee913
---

# Architecture rubric review

The architecture fixes many real divergence points, but cannot currently serve as a consistent build contract against the September 8 PRD: two already-recorded authority conflicts remain, several newly explicit product assumptions lack architectural ownership, and historical implementation instructions contradict current code.

This is a static review of the full 1,766-line architecture and the current 1,103-line PRD, with targeted source, governance, and configuration inspection. No implementation, existing planning artifact, or existing review was changed. No runtime tests were run. Technology currency and adversarial boundary checks are separate reviewer lenses.

The July 19 architecture memlog explicitly preserves the classic D-/A-/C-/F-/I- document and stable IDs. Missing AD-n/Binds/Prevents/Rule fields are therefore not findings. The August 4 approved recovery proposal treats architecture as the technical target unless validation finds a direct contradiction (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-04.md:83`). The PRD now expressly requires reconciliation and re-approval of authority conflicts before release (`_bmad-output/planning-artifacts/prd.md:140`). Its A-series entries are working assumptions, not separately ratified architecture decisions.

## Rubric assessment

| Check | Assessment |
| --- | --- |
| Named paradigm and real divergence points | Substantial coverage: CQRS/event sourcing, pure aggregates, worker side effects, tenant-first authorization, one machine contract, repository/ref lock identity, and EventStore-owned idempotency admission are explicit. |
| Enforceable rules | Many rules are testable, but the canonical C6 transition matrix contradicts the current completion model, and the error contract still names a forbidden public outcome. R1/R2. |
| Deferred hazards | Post-MVP repair, local-only folders, webhooks, and additional providers are explicit. New cross-tenant binding and long-term audit assumptions require a decision before dependent implementation. R3/R4. |
| Brownfield agreement | Search bridge registration and dependency-reference instructions are stale. The report distinguishes those from an unproved deployed round trip. R6. |
| Product capability coverage | All FR groups have structural homes. That does not establish semantic coverage: C6 recovery, caller-visible errors, global binding ownership, and long-term audit retrieval remain unresolved. |
| Parent/inherited constraints | No parent spine was supplied. Existing legacy IDs and explicit July authority are retained as constraints rather than renumbered. |
| Operational/environmental envelope | Deployment style, sidecars, identity, production state/broker baseline, health, telemetry, rate limits, reconciliation, and release checks are present. This dimension is not silent. C7/C12 release-blocking language needs reconciliation. R7. |
| Current technology | Delegated to the technology reviewer; no independent claim of current compatibility is made here. |

## High findings

### R1 — C6 rejects the product's recovery transitions and can select the wrong terminal state

- **Classification:** Existing explicit blocker, PD11; not a newly discovered production failure.
- **Evidence:** `_bmad-output/planning-artifacts/architecture.md:299` makes every unlisted pair invalid; `:339` sends every known commit failure to `failed`; `:343` only allows `dirty` to enter reconciliation; `:347` restores inaccessible work directly to `ready`; `:1738` tells builders all those transitions are canonical. `_bmad-output/planning-artifacts/prd.md:483` allows originating-task reacquisition after staged lease expiry; `:484` requires restored authority to retain staged work and return to `dirty`; `:487` keeps known retryable commit failure dirty. `:708` and `:1103` explicitly record the disagreement as PD11. Actual code agrees with the old matrix: `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:90`, `:98`, `:106`, and `:122`.
- **Impact:** Independently built lifecycle, locking, cleanup, and console units can disagree about whether a task may resume and whether its content is eligible for terminal cleanup. A passing matrix-coverage test can preserve the disagreement because it verifies the historical rule.
- **Disposition:** **discuss** under PD11/PD2. Reconcile and re-approve the architecture matrix, C6 mapping, transition implementation, and C3 cleanup triggers together. Preserve the current no-discard default unless the product decision changes. Do not silently implement the PRD assumptions as already ratified changes.

### R2 — Architecture's canonical caller error mapping conflicts with the safe-denial rule

- **Classification:** Existing explicit blocker, PD10; architectural propagation of a recorded product/Spine conflict.
- **Evidence:** `_bmad-output/planning-artifacts/architecture.md:608` publishes CLI 73 / `not_found`; `:614` includes `not_found` in the MCP mapping and directs consumers to the full canonical enum; `:574` requires `details` but not the PRD's required closed `details.visibility`; the normative response example at `:800` has no visibility field. `_bmad-output/planning-artifacts/prd.md:590` forbids caller-visible `not_found`, `cross_tenant_access_denied`, and `audit_access_denied`, and requires a single indistinguishable denial status/shape per operation. `:586`, `:903`, and `:1102` establish the visibility/family mapping and PD10 work.
- **Impact:** An adapter implemented exactly from architecture may preserve a category the product now forbids, or omit the required visibility distinction. A generated parity oracle can prove agreement with a conflicting Spine without proving product conformance.
- **Disposition:** **discuss** through PD10, then update the architecture examples and adapter tables with the approved Spine/SDK/C13 regeneration. Distinguish internal audit classification from public result categories. Do not claim an observed runtime information leak solely from these declarations.

## Medium findings

### R3 — Global remote binding ownership is an unbound architectural seam

- **Classification:** Newly explicit PRD assumption needing architectural treatment; A8/A19 are not a ratified storage design.
- **Evidence:** `_bmad-output/planning-artifacts/prd.md:620` assumes that at most one managed tenant owns a remote repository/ref binding and that archive releases its duplicate-index occupancy; `:1072` and `:1083` name the decision owners. `_bmad-output/planning-artifacts/architecture.md:876` serializes writers on an identity that starts with the managed tenant; `:1465` and `:1466` identify aggregate/read-side ownership but do not assign a cross-tenant claim authority. The current binding service loads one tenant/folder stream before constructing its result (`src/Hexalith.Folders/Aggregates/Folder/RepositoryBindingService.cs:113`); its binding identifier also incorporates tenant/folder scope (`:84`). This targeted inspection does not establish absence of every possible registry elsewhere.
- **Impact:** Tenant-scoped locks alone cannot enforce the new cross-tenant uniqueness assumption. Separate binding and archive implementations could choose incompatible identity normalization, claim serialization, and release timing; two tenant partitions could each believe they own the same remote/ref.
- **Disposition:** **discuss** with PM/Security under A8/A19. If retained, bind the authoritative ownership scope, atomic claim/release protocol, archive/rebind ordering, and safe denial behavior in architecture before independent binding/archive work. If declined, revise the assumption and explicitly decide how same-remote cross-tenant writers are handled. Do not silently add a global registry or weaken tenant-prefixed isolation.

### R4 — Seven-year audit retrieval lacks a normal-reader ownership and compaction rule

- **Classification:** Newly explicit product assumption A14 / unresolved C3 interpretation, not proof that deployed records have been lost.
- **Evidence:** `_bmad-output/planning-artifacts/prd.md:311` promises normal authorized audit operations can still serve records after the 400-day read-model class; `:1078` assigns A14 to Architecture. `docs/exit-criteria/c3-retention.md:18` gives audit metadata seven years, while `:21` gives read-model views 400 days or until rebuilt. `_bmad-output/planning-artifacts/architecture.md:550` names a dedicated audit projection as the query store; `:632` defines only a degraded, incident-admin event-stream fallback. The current normal query handler reads only its injected audit read-model port (`src/Hexalith.Folders/Queries/Audit/AuditTrailQueryHandler.cs:73`).
- **Impact:** An audit-storage implementer can purge read-model rows after 400 days while an API implementer assumes all seven years remain available through that model. Requiring incident-admin access for old audit records would change the normal audit-reviewer contract. Rebuilding without a class-aware projection rule could also resurrect shorter-retained fields.
- **Disposition:** **discuss** under A14/C3/PD6. Choose an audit-class projection retention rule or an explicitly owned retained-record query path, including per-field expiry, authorization, pagination/freshness, and rebuild behavior. Keep the rule scoped to the approved retention periods; no legal conclusion is inferred.

### R5 — D-9 names the request retry-allocation header as the transport fallback response

- **Classification:** Direct internal contract inconsistency.
- **Evidence:** `_bmad-output/planning-artifacts/architecture.md:549` specifies `x-hexalith-retry-as: stream` on inline-upload 413. The same document at `:792` names the response `X-Hexalith-Retry-Transport`, explicitly distinct from the request-only `X-Hexalith-Retry-As` whose values are `caller`/`operator` (`:793`).
- **Impact:** SDK upload fallback built from D-9 and REST built from the header table disagree at the inline/stream boundary, and consumers can confuse a transport hint with retry authority.
- **Disposition:** **autofix** in a subsequent Update: correct D-9 to the approved `X-Hexalith-Retry-Transport` response name, confirmed in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:2089` and `:5368`. This Validate run does not edit it.

### R6 — Historical implementation and bootstrap instructions conflict with the current codebase

- **Classification:** Stale implementation claims, not new design defects or proof of release readiness.
- **Evidence:** `_bmad-output/planning-artifacts/architecture.md:181` says the Server cannot reference the bridge store and leaves the unavailable default registered. Current `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:131` adds the EventStore read-model store, and `:132` through `:134` replace the default with `EventStoreSemanticIndexingBridgeStore`, now located in core (`src/Hexalith.Folders/Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs:17`). Architecture `:445` says sibling dependencies must not be replaced with package references, whereas `Directory.Build.props:10` documents Debug/source versus Release/NuGet and `:18` selects package references for Release. Architecture `:1743` still leads handoff with initial solution scaffolding and workshop work.
- **Impact:** Builders may repeat finished bridge relocation or reconstruct existing scaffolding, and can reject the repository's deliberate CI/package lane as an architectural violation. These stale claims obscure the actual remaining runtime evidence gates.
- **Disposition:** **autofix** the brownfield seed/provenance in a subsequent Update from current tracked code and the approved delivery ledger. Preserve OQ5 and the NOT READY posture until non-empty deployed end-to-end evidence is accepted; registration alone is not FR58 completion. Preserve historical audit facts as dated history rather than current instructions.

### R7 — C7/C12 are simultaneously non-release-gating and release-blocking

- **Classification:** Existing release-authority conflict, not a newly discovered missing timing or provider-drift implementation.
- **Evidence:** `_bmad-output/planning-artifacts/architecture.md:270` calls C7 and C12 reference-pending and non-release-gating. The same architecture `:268` requires validation of each criterion before MVP release and `:1698` retains outstanding OQ1–OQ10. Current `_bmad-output/planning-artifacts/prd.md:1044` makes C7 timing acceptance blocking, `:1047` makes approved C12/provider evidence blocking, and `:1040` requires every OQ before release.
- **Impact:** Release automation or a reviewer following the status reconciliation paragraph can waive evidence that the product's current gate requires.
- **Disposition:** **autofix** the obsolete non-release-gating description during an Update, cross-referencing the current OQ/approval authority. Do not change criterion approval states, invent numeric timing values, or equate an approval record with positive runtime evidence.

## Explicit non-findings and retained blockers

- The absence of completed durable repository/Git, populated diagnostics, and accepted live search evidence remains an already-chartered production blocker. The architecture acknowledges those gaps at `:199`, `:203`, `:215`, and `:1698`; their existence is not reintroduced here as a newly discovered architecture defect.
- Repair/discard remains post-MVP by default. A permanently non-resumable dirty task is an accepted current limitation recorded by PRD PD2; reviewers must not silently authorize repair to remove it.
- FR58 is currently metadata-token recall. Architecture's body-content follow-up is explicitly C9-gated (`:143`, `:181`), so no new body-indexing scope is inferred. The label “Authorized content search” at `:1489` can be clarified as seed prose, but the explicit scoped rule already constrains it.
- The architecture includes operational topology and infrastructure choices. It does not fail the rubric for omitting the whole operational envelope, although individual rules need reconciliation.
- Legacy format, absence of the new spine filename, retained stable IDs, and literal identity placeholders in examples are not semantic architecture findings.

## Verification limits

Both full canonical documents were read. Targeted evidence includes C3/C6 governance, the approved August 4 recovery and August 24 NFR-relock proposals, the architecture memlog, repository baseline and build/configuration guidance, transition code, binding service, normal audit query, and Server search registration. Hashes above identify the reviewed working-tree contents. Static inspection establishes conflicting contracts and current registration/source facts; it does not establish successful deployment, persistence survival, a runtime leak, or a passed release gate.
