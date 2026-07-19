---
source: sprint-change-proposal-2026-07-07-081620.md
source_date: 2026-07-07T08:16:20+02:00
source_status: approved
reconciled_against:
  - prd.md
  - .memlog.md
addendum_present: false
disposition: already-covered-with-provenance-and-governance-follow-up
---

# Reconciliation — Sprint Change Proposal 2026-07-07 08:16:20

## Source purpose and status

This approved `bmad-correct-course` proposal responds to the 2026-07-07 domain-focus audit. It classifies the issue as a moderate, post-MVP technical course correction: retain the Hexalith.Folders product model and completed MVP history, but move platform-owned plumbing to Hexalith.Commons, Hexalith.EventStore, Hexalith.FrontComposer, and Hexalith.Memories. It proposes a new non-product Epic 11 with 13 stories, lockstep updates to architecture/UX/sprint artifacts, and two narrowly scoped PRD edits.

Approval was recorded on 2026-07-07 by Jerome. The proposal explicitly says this is not a product-scope pivot, does not require reopening completed Epics 1–10, and should preserve existing wire behavior.

The current PRD is newer (`updated` and `finalized` 2026-07-14). Its product body already contains both requested PRD changes. The remaining proposal content is primarily architecture, implementation sequencing, backlog, and governance-gate detail.

## Product-level requirements and decisions relevant to the PRD

1. **No product-scope pivot.** The tenant-scoped repository-backed workspace control-plane thesis, canonical lifecycle, supported providers, and user-visible outcomes remain unchanged.
2. **Post-MVP platform alignment is valid growth work.** Local copies of shared Hexalith platform capabilities should be removed without changing product semantics.
3. **Platform/domain ownership must remain explicit.** EventStore owns platform command/event/projection/query/cursor/read-model/domain-service mechanics; Commons, FrontComposer, and Memories own applicable shared helpers, shell behavior, and search integration; Folders retains folder policy, ACLs, provider ports, bindings, workspace state, file-operation facts, commit metadata, and operational projections.
4. **Wire and surface semantics must remain stable.** Refactoring may not silently alter the Contract Spine, REST, generated SDK, CLI, MCP, error, authorization, state, idempotency, or audit behavior.
5. **Existing product qualities remain the reason for the work.** Tenant isolation, metadata-only audit, cross-surface parity, operational visibility, accessibility, replay/determinism, and safe failure behavior remain binding outcomes rather than new FR scope.

## Already covered in the current PRD

### Direct requested PRD edits

- **Product Scope → Growth Features (Post-MVP)** already includes “technical platform-alignment work that removes local copies of shared Hexalith platform capabilities without changing product semantics.” This is substantively the proposal's requested addition.
- **API Backend Specific Requirements → Architectural Boundaries** already gives EventStore the platform-owned command/aggregate/event/projection/query/cursor/read-model/domain-service mechanics, identifies Commons/FrontComposer/Memories shared responsibilities, and reserves folder-specific policy, ACL, provider binding, state, provider ports, and operational projections to Folders. This is substantively the proposal's requested replacement.

### Stable product identifiers already carrying the proposal's outcomes

- **SM2** and **FR8–FR10** cover zero-tolerance tenant isolation, scoped authorization, safe denial, and authorization evidence.
- **CM4**, **FR3**, and **FR47–FR51** cover the canonical operation classification and REST/SDK/CLI/MCP equivalence that the refactor must preserve.
- **FR15–FR23** cover provider configuration, readiness, repository binding, safe diagnostics, provider capability differences, and provider support evidence while leaving provider transport mechanics outside the PRD.
- **FR31** and **FR43–FR46** cover inspectable state, stable error/status semantics, and failure visibility.
- **FR39–FR40** and **FR52–FR57** cover metadata-only task/commit evidence, audit reconstruction, read-only operational visibility, and the no-content/no-secret boundary.
- **FR41–FR42** cover all-mutations idempotency and read-key rejection, which the proposed refactor must preserve when adopting shared helpers.
- **FR58** defines the authorized Memories-backed metadata-token facade and its fail-safe security trimming; the proposal's Memories wrapper work is an implementation mechanism beneath this requirement.
- **C13** defines the generated current operation/parity inventory as the binding denominator, while **OQ5–OQ9** already track implementation/conformance closure for search, console projections, lock identity, all-mutations idempotency, and incident access.
- The exact NFR headings **Security and Tenant Isolation**, **Reliability, Idempotency, and Failure Visibility**, **Integration and Contract Compatibility**, **Observability, Auditability, and Replay**, **Operations Console Accessibility**, and **Verification Expectations** already cover the proposal's claimed NFR rationale. The PRD does not assign stable numeric IDs to individual NFR bullets.

### Memlog alignment

- The memlog records the PRD as a brownfield living contract and the canonical product thesis, repository-backed lifecycle, zero-tolerance tenant isolation, metadata-only boundary, provider readiness, cross-surface authority hierarchy, and generated C13 denominator.
- The memlog's change/decision history supports the proposal's “technical alignment, not product pivot” classification. No prior product decision must be reversed to accept this source.
- The approved proposal itself is not named in `inputDocuments` and has no source-specific memlog entry, so provenance is weaker than the body coverage suggests.

## Genuine PRD gaps

### 1. Source provenance is missing

The current PRD body contains the proposal's two requested edits, but its `inputDocuments` frontmatter does not list `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-081620.md`, and `editHistory` does not attribute those platform-alignment changes to the approved 2026-07-07 proposal.

Recommended correction:

- append the proposal path to `inputDocuments` without removing or reordering existing sources;
- add one `editHistory` entry for the current update noting that the approved July sprint-change proposals were reconciled;
- record one memlog change entry identifying the approved proposal as the source of the already-present post-MVP platform-alignment and ownership-boundary wording.

### 2. No new product requirement is warranted

The source explicitly says Epic 11 introduces no new product FR scope, and the relevant outcomes are already covered. Adding an FR for `ServiceDefaults`, package references, helper consolidation, HTTP client construction, Fluent component selection, generated-client serializer choice, or submodule sequencing would leak implementation into the product contract.

No stable-ID addition or renumbering is recommended.

## Conflicts and stale assumptions

### Contract inventory conflict

The proposal refers to preserving “47/47 REST parity.” The current PRD states that C13 is generated from the current Contract Spine, currently contains 49 rows, and must not use a hard-coded count as its binding denominator. The memlog also records C13 as the single generated denominator. Therefore `47/47` is stale implementation-era evidence and must not be copied into the PRD or used as a release invariant.

Disposition: preserve behavioral parity against the generated C13 snapshot, not a static route count.

### AppHost/Aspire exception conflict

The proposal asks architecture to record a sanctioned module-test exception for `Hexalith.Folders.AppHost` and `Hexalith.Folders.Aspire`. Current repository-level Hexalith instructions say domain modules must not ship their own `*.AppHost`, `*.Aspire`, or `*.ServiceDefaults` project. This is not a PRD conflict, and the PRD should not resolve it. Architecture/governance must either remove those projects or document an explicit currently authorized override outside the PRD.

Disposition: keep out of the PRD; route to architecture/governance resolution.

### Commit-message convention conflict

Story 11.2 requires every submodule bump to use `chore(deps):`. Current repository instructions prohibit the `chore` type and give `build(deps):` as the expected submodule-bump form. This is implementation-story guidance, not product scope.

Disposition: correct the story/governance artifact to `build(deps):`; do not mention it in the PRD.

### Historical baseline staleness

The proposal's audit baseline is HEAD `533806b` and a 2026-07-07 all-done Epics 1–10 snapshot. It remains useful historical evidence but is not current product truth. The PRD's 2026-07-14 authority hierarchy, state semantics, C13 inventory, and open-item structure supersede any conflicting baseline count or shorthand.

## Implementation, architecture, UX, and story detail that stays out of the PRD

The following belongs in `epics.md`, story files, architecture/ADR/UX artifacts, project context, or the approved proposal itself:

- Epic 11/story counts, backlog status, sequencing, owners, acceptance criteria, and the instruction not to reopen completed Epics 1–10.
- Removing `Hexalith.Folders.ServiceDefaults`, deciding the AppHost/Aspire disposition, optionally introducing a Providers project, and selecting package/project boundaries.
- Specific shared primitives and seams: TenantAccess projection/evaluator, cursor codecs, read-model stores, telemetry helpers, Dapr subscription mapping, `IDomainServiceAdmissionStage`, secret stores, hashing, URL/correlation helpers, and Memories publisher/read wrappers.
- Specific defect fixes: unsigned cursors, per-call Forgejo clients, secret-filter drift, token relay and Blazor circuit behavior, sync-over-async user context, HTTPS bearer guard, reserved-tenant semantics, and N+1/double serialization.
- Server envelope/route helper consolidation, CLI/MCP adapter-core consolidation, test-helper relocation, generated-client System.Text.Json migration, and package cleanup.
- Exact component prescriptions such as `FluentDataGrid`, accordion use, raw-control replacement, `fc-*` styling-hook cleanup, icon upstreaming, and Shell helper reuse. The PRD already carries the user-visible read-only/accessibility/safety outcomes; component mechanics belong in UX and architecture.
- Exact verification commands, test FQNs, package allow-lists, project inventories, route tables, workflow pins, submodule SHAs, and conventional-commit wording.

No `addendum.md` currently exists. These details are already preserved in the approved source and its downstream architecture/backlog artifacts, so the PRD should not ingest them. If an addendum is created during the wider reconciliation, it should contain only a concise architectural-decision pointer rather than duplicating all 13 stories.

## Recommended stable-ID edits/additions

- **FR additions:** none.
- **FR edits:** none required for this source.
- **Renumbering:** none.
- **Existing IDs to cite in downstream Epic 11 traceability:** SM2, CM4, FR3, FR8–FR10, FR15–FR23, FR31, FR39–FR42, FR43–FR58, C13, and OQ5–OQ9, plus the named NFR sections listed above.
- **Metadata-only edit:** add the proposal to `inputDocuments` and attribute reconciliation in `editHistory`; this preserves source provenance without inventing product scope.

## Qualitative ideas at risk of being lost

- This work is a **technical course correction, not a product pivot**.
- Preserve completed-story history; add follow-up work rather than rewriting proof of what shipped.
- Work should be **platform-first**: provide missing shared seams before deleting local equivalents, so duplication is removed rather than moved.
- Refactoring is intentionally **wire-preserving**; behavioral change requires a separately approved contract/product change.
- Folders must retain folder-specific policy and provider ports even while platform-owned mechanisms move upstream.
- UI reuse must apply below the shell as well as at the layout boundary, while the product-level read-only/content-safety boundary remains unchanged.
- Exceptions should be explicit and traceable through ADR/governance rather than silently normalized in code or the PRD.

## Concise disposition

**Already covered; metadata update only.** Keep the current PRD body and stable IDs. Add source provenance, do not import the technical epic or architecture mechanics, and flag the stale `47/47` count, AppHost/Aspire exception, and `chore(deps)` rule for their owning downstream artifacts.
