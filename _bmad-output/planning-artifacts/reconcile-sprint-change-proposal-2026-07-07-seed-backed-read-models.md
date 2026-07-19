---
source: sprint-change-proposal-2026-07-07-seed-backed-read-models.md
source_date: 2026-07-07
source_status: approved
reconciled_against:
  - prd.md
  - .memlog.md
addendum_present: false
disposition: implementation-finding-retained-mvp-limitation-superseded-by-oq6
---

# Reconciliation — Seed-Backed Ops-Console Diagnostics & Transition-Evidence Read Models

## Source purpose and status

This approved `bmad-correct-course` proposal documents that the default operations-console diagnostics and workspace transition-evidence read models were seed-backed dev/test seams, unpopulated by production paths, and therefore safely empty in deployed hosts. It chose to record the limitation, correct “projection-backed” architecture wording, and re-home EventStore-backed projection/wiring work to Epic 11 Story 11.10 rather than implement live projections immediately.

Jerome approved the decision on 2026-07-07. The source provides an implementation handoff for architecture, epics, deferred-work, and sprint-status edits, but does not itself state that every downstream edit was applied and verified. Its most durable contribution is the evidence-backed distinction between “read-model-based” and “actually populated by a production projection.”

The current PRD is newer (finalized 2026-07-14) and explicitly makes replacement of seed-only console/read-model diagnostics an open release blocker under OQ6. That later product decision supersedes the proposal's posture that the safe-empty limitation can remain an accepted MVP condition with no timeline impact.

## PRD-relevant product decisions

1. **Read-model-based is not equivalent to projection-backed.** A query handler over an empty seed seam can be structurally safe while still failing the product outcome of showing live diagnostic state.
2. **Safe-empty behavior is the correct failure posture, not successful feature delivery.** Returning non-enumerating not-found is preferable to leaking data or inventing status, but it does not prove operator diagnostics or transition evidence work.
3. **Production diagnostic views require authoritative population.** Readiness, lifecycle, lock, dirty/failure, provider/sync, projection-freshness, timeline, and transition evidence must be backed by production event/read-model flows.
4. **Documentation must describe deployed truth.** Aspirational “projection-backed” language cannot stand while default production composition resolves an unpopulated seed seam.
5. **The no-content/no-secret and read-only boundaries remain unchanged.** Replacing seed seams with projections must preserve tenant scope, metadata-only output, C9 redaction, safe denial, and the console's no-mutation boundary.
6. **Projection completeness requires positive, degraded, and replay evidence.** Deterministic emptiness is not an adequate substitute for rebuilding meaningful views from ordered events.

## Already covered in the current PRD

### Exact identifiers and sections

- **SM6 — Diagnostic completeness** requires 100% of injected lifecycle failures to expose state, safe cause category, retryability, client action, correlation ID, and metadata-only audit evidence.
- **UJ5** requires a tenant-scoped operator to diagnose provider or credential failures from readiness and status evidence.
- **FR31** requires authorized actors to inspect workspace lifecycle, lock state, disposition, projection freshness/checkpoint, retryability, and task/audit/provider/index status.
- **FR45–FR46** require complete lifecycle/lock vocabulary and resulting state, cause, retryability, client action, correlation ID, and available metadata-only evidence after failure.
- **FR52** requires tenant-scoped operators to inspect read-only readiness, binding, lifecycle, lock, disposition, durable commit, failure, provider, credential-reference, and sync status.
- **FR53–FR56** require metadata-only audit reconstruction and projection-backed normal timelines, with only a bounded redacted event-evidence exception during projection degradation.
- **API Backend Specific Requirements → Public Surfaces** states that the console normally consumes projections and permits raw bounded event evidence only through the authorized degraded incident view.
- **Non-Functional Requirements → Observability, Auditability, and Replay** requires projection-first console views and deterministic rebuilding of meaningful status, audit, and timeline results from the same ordered event stream.
- **C2** makes status/audit visibility freshness measurable, which presumes production-populated views.
- **OQ6** explicitly requires seed-only console/read-model diagnostics to be replaced with projection-backed readiness, lifecycle, lock, failure, timeline, and transition evidence; positive, degraded, and replay scenarios must populate approved projections before console implementation readiness is accepted.

### Memlog alignment

- The memlog records the console as normally projection-based, with only the bounded incident-mode exception during degradation.
- It records OQ6 as blocking console implementation readiness until approved projections pass positive, degraded, and replay scenarios.
- The memlog's later OQ6 decision supersedes any inference that safe-empty seed seams satisfy the complete MVP console outcome.
- The source's fail-safe observation remains compatible with the memlog's zero-tolerance isolation and no-content boundary; safe emptiness is acceptable interim behavior while the required feature remains incomplete.

## Genuine PRD gaps

The source originally exposed a genuine product-versus-implementation gap, but the current PRD has already captured it precisely as **OQ6**. No additional FR or NFR is needed.

The remaining gap is implementation/evidence closure, not PRD wording:

- production projections must populate the approved console and transition-evidence views;
- positive, degraded, and replay scenarios must prove the views;
- OQ6 approval must record evidence identity/date/version/digest before release acceptance.

The approved proposal is absent from PRD `inputDocuments` and has no source-specific memlog reconciliation entry. If July sources are enumerated, add it as the origin of the seed-only finding while recording that the later PRD/OQ6 release posture supersedes its accepted-limitation framing.

## Conflicts and supersession

### Accepted MVP limitation is superseded by OQ6

The proposal concludes that seed-backed, safe-empty diagnostics can remain an explicitly owned MVP limitation and that the choice has no MVP timeline impact. The current PRD instead makes projection-backed console/transition evidence a release blocker under OQ6.

Disposition: retain the implementation finding and Story 11.10 ownership, but do not preserve “acceptable for MVP release” or “no timeline impact” as current product policy.

### “NFR51/NFR52 remain covered” is too permissive for current product outcomes

The proposal argues that read-model-based empty views satisfy the read-only property and that “empty in → empty out” satisfies determinism. The first is only a structural safety property; the second proves deterministic absence, not the required diagnostic projections. SM6, FR31, FR46, FR52–FR56, Observability/Replay, and OQ6 require meaningful positive output and replay evidence.

Disposition: safe-empty behavior is compliant fail-safe behavior for an incomplete implementation, but it does not close console implementation readiness or the positive/replay verification obligation.

### “Wire-preserving” does not forbid completing already-approved behavior

The proposal treats live projection population as a behavior change outside a wire-preserving epic. Populating an existing response model with authorized current data changes runtime results, but it does not necessarily change the public schema or product scope; it completes behavior already required by the PRD.

Disposition: protect wire schemas and safety invariants, but do not use wire preservation to defer OQ6-required implementation indefinitely.

### FR46 is only partially satisfied by taxonomy alone

The proposal says FR46 is satisfied through error taxonomy/explanation while transition evidence stays empty. Current FR46 requires resulting state and available metadata-only evidence, and the surrounding operator/audit requirements require useful populated evidence. Taxonomy-only output is necessary but not sufficient for full release acceptance.

## Implementation, architecture, and backlog detail that stays out of the PRD

- `IOpsConsoleDiagnosticsReadModel`, `IWorkspaceTransitionEvidenceReadModel`, their `InMemory*` implementations, `TryAddSingleton` registrations, and `Save(...)` call searches.
- The exact seven diagnostic views, source file/line locations, and the six other read models populated by `InMemoryFolderRepository`.
- Story 4.15's projection inventory and the conclusion that these two models have no existing projection logic to relocate.
- `FolderScopedDiagnosticsQueryHandler`, `TenantScopedDiagnosticsQueryHandler`, `WorkspaceTransitionEvidenceQueryHandler`, and their exact `NotFoundSafe`/`ReadModelUnavailable` mechanics.
- Whether EventStore-backed projections are implemented in Story 11.10, another delivery unit, Server, Workers, or platform SDK seams.
- DCP/AppHost lane blockers, owner assignments, architecture/deferred-work edits, sprint-status comments, and optional NFR-traceability fingerprint changes.
- Estimates such as “8+ projections,” effort/risk ratings, and the Memories-bridge precedent.

No `addendum.md` exists. The proposal and architecture/deferred-work artifacts should preserve this technical evidence. The PRD needs only the already-present product outcomes and OQ6 closure condition.

## Recommended stable-ID edits and additions

- **New FRs:** none.
- **FR edits:** none.
- **OQ edits:** none; OQ6 already captures the issue and required evidence.
- **Renumbering:** none.
- **Downstream traceability:** cite SM6, UJ5, FR31, FR45–FR46, FR52–FR56, Public Surfaces, Observability/Auditability/Replay, C2, and OQ6.
- **Metadata:** optionally list the approved source and log that its evidence is retained while its MVP-limitation posture is superseded by the 2026-07-14 OQ6 release blocker.

## Qualitative ideas at risk

- “Safe empty” is an honest, fail-closed interim state—not proof that an operator-facing feature works.
- Read-model abstraction and production projection population are separate claims and must not be conflated.
- Documentation must describe what deployed composition actually resolves, not only the intended architecture.
- Positive population, degraded behavior, freshness/checkpoint visibility, and replay reconstruction are all necessary evidence dimensions.
- Seed seams remain useful for tests, but production defaults need authoritative event-driven population.
- The wiring owner and blocker must remain explicit even if implementation moves between stories or platform seams.

## Concise disposition

**No new PRD edit; later PRD already supersedes the source's release posture.** Preserve the seed-only finding and downstream ownership, but treat safe-empty diagnostics as an incomplete fail-safe implementation. OQ6 now blocks console readiness until projection-backed positive, degraded, and replay evidence closes the gap.
