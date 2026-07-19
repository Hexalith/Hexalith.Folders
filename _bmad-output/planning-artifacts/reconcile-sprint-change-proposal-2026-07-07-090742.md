---
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-090742.md
source_date: 2026-07-07T09:07:42+02:00
source_status: approved
reconciled_against:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/.memlog.md
addendum: absent
disposition: no-prd-change
---

# Reconciliation — Sprint Change Proposal 2026-07-07 09:07:42

## Source purpose and status

This is Jerome's approved, moderate-scope implementation-readiness remediation proposal. It responds to a `NEEDS WORK` report caused by stale and inconsistent planning artifacts, not by a product pivot or new product scope. Its approved route is a direct documentation adjustment followed by a rerun of implementation readiness.

The proposal's final frontmatter and approval section record approval by Jerome at 2026-07-07 09:20 +02:00. The checklist lines that still say approval and handoff are pending are internally stale and are superseded by that final approval record.

## Product-level decisions and requirements relevant to the PRD

1. The six MVP product epics and the product's implemented behavior are not to be reopened or rolled back. The readiness problem is artifact consistency.
2. FR58 is an existing requirement in the current PRD inventory, not a future FR awaiting creation.
3. FR58's product value is a Folders-owned authorized search facade for developers and AI agents across API/REST, SDK, MCP, and CLI. Results must be tenant/folder/workspace trimmed, checked or hydrated against authoritative Folders state, metadata-only at egress, non-enumerating, and fail-safe when indexing or the facade is unavailable.
4. FR58's inclusion in product scope and the evidence needed to accept its implementation are separate questions. Remaining release evidence must not be described as a missing PRD requirement.
5. C4 query limits and C9 path/metadata handling are approved policy constraints, not reasons to treat FR58 as unscheduled scope.
6. Removal, archive, tombstone, authorization revocation, or stale state must not leave an active searchable result; service outages must surface as inspectable/retryable indexing state without rolling back a durable Folders file operation.

## Already covered in the current PRD

- **FR58 — Authorized Search Facade** already defines authorized metadata-token search and indexing-status queries through REST, SDK, CLI, and MCP; current-authority trimming and Folders-state hydration; dropping stale, archived, revoked, unauthorized, and hidden hits; C9-classified metadata-only results; prohibition of raw paths, bodies, snippets, source URIs, and hidden-resource existence; and explicit fail-safe unavailability.
- The **MVP Feature Set** explicitly includes authorized metadata-token recall and indexing-status queries per FR58. It therefore already makes FR58 part of the current MVP/release inventory without coupling the PRD to Epic 10 implementation structure.
- The paragraph immediately after FR58 and the **Explicit MVP Non-Goals** explicitly exclude cross-workspace body-content indexing, indexed body snippets, and indexed body recall from FR58/current release while preserving bounded live-workspace body search under **FR34–FR35**.
- **FR31** and **FR46** already make index/status freshness, failure, retry eligibility, and safe diagnostic evidence visible.
- **FR47–FR50** already establish the current REST, CLI, MCP, and SDK surfaces. **FR51** requires their behavioral equivalence.
- **FR55** already enforces the metadata-only boundary across events, logs, traces, projections, audit, diagnostics, errors, and console responses.
- **OQ5** is the current canonical readiness gate for FR58: authorized non-empty metadata-token results, index status, stale/unauthorized-hit removal, unavailable behavior, both C13 operations, and PM/Security/Test approval in `docs/exit-criteria/fr58-search-evidence.md`.
- The current memlog records the governing decision that FR58 is metadata-token recall only, with authoritative trimming/hydration and no raw paths, snippets, source URIs, bodies, or hidden-resource existence. It also records the separate-product boundary between FR34/FR35 live-workspace snippets and FR58 indexed recall.
- The current PRD already reconciles approved C4 and C9 constraints into **FR34–FR35**, **FR38–FR39**, **FR54–FR56**, the NFRs, and the Open Release Items.

## Genuine PRD gaps

None.

The proposal's only direct PRD edit was a scope note saying that FR58 is in the current inventory and is implemented through Epic 10. The first point is already expressed more clearly by the MVP Feature Set and FR58. The second point is now unsafe: current PRD **OQ5** and the memlog say FR58 implementation readiness remains blocked until the approved non-empty round-trip evidence exists. Adding the proposed note would duplicate current scope text, leak epic/implementation structure into the product contract, and overstate readiness.

## Conflicts with current memlog and prior decisions

### 1. “Implemented through Epic 10” overstates current readiness

The proposal recommends saying FR58 “is implemented through Epic 10” and that only release-readiness evidence remains. The current memlog and **OQ5** distinguish completed story status from accepted product behavior: the facade is still described as fail-safe but functionally empty until authorized non-empty metadata-token results and both C13 operations are evidenced. Preserve the current OQ5 gate and do not import the implementation-complete claim into the PRD.

### 2. The proposal's older FR58 wording is broader than the approved current boundary

The proposed PRD text says consumers can “search the content that Folders has indexed,” which can imply body-content ingestion or recall even though it later says metadata-only egress. The current memlog explicitly supersedes that ambiguity: FR58 indexes and recalls metadata tokens only; indexed bodies, body snippets, and source URIs remain outside the current release and require a future requirement plus Security and PM approval. The current FR58 wording governs.

### 3. Evidence governance is more precise in the current PRD

The proposal refers generically to Epic 10 action-item evidence. The current PRD gives FR58 one canonical artifact, owners, approvers, required scenarios, and a close/reopen rule under **OQ5**. Do not weaken that gate to generic sprint action-item completion.

### 4. Approval metadata inside the source is inconsistent

The source's checklist says approval/handoff are pending, while its frontmatter and final section say approved. Treat the later explicit approval record as authoritative; do not propagate the pending markers.

## Implementation, architecture, story, and artifact-maintenance detail that stays out of the PRD

- Story 8.6's Legal signer, date, location, done state, retention-gate result, and stale `BLOCKED-PENDING-LEGAL` cleanup belong in implementation evidence and `epics.md`.
- AR-PROPOSAL-06/08 wording, 58/58 epic coverage text, product-versus-workstream section headings, and Epic 10's Phase 2 classification belong in `epics.md` and readiness reporting.
- The standard `As a / I want / So that` framing and `Acceptance Criteria` headings for Stories 10.1–10.5 are story-quality edits, not PRD content.
- The worker-only `Hexalith.Memories.Contracts` dependency, Dapr pub/sub topology, `SearchIndexEntryChanged` and `SearchIndexEntryRemoved` CloudEvents, source/topic names, stable event/idempotency keys, bridge-projection mechanics, BM25 proof, and precise per-project dependency exclusions are architecture or implementation details. Their product consequences are already represented by FR58, FR31, FR46, FR55, and OQ5.
- UX frontmatter path conversion from `D:/Hexalith.Folders/...` to repository-relative paths is artifact hygiene, not a product requirement.
- Optional architecture wording about Epic 9, C4, C9, and Epic 10 is architecture traceability, not PRD scope.
- Sprint action-item ownership, DCP-capable-lane evidence, rerunning readiness, and the suggested handoff are delivery governance.

## Recommended stable-ID edits or additions

- **Add no FR and renumber nothing.** Retain **FR58** exactly as currently written.
- **Do not append the proposal's “implemented through Epic 10” scope note.** Current scope is already explicit, and **OQ5** is the stable readiness mechanism.
- **Retain OQ5** with its existing ID, canonical evidence path, approvers, scenarios, and blocking consequence.
- If a future approved change introduces indexed body-content recall, create a new stable FR rather than broadening FR58 silently; the current PRD and memlog require separate Security and PM approval.

## Qualitative ideas at risk of being lost

- The correction is about keeping planning truth honest, not manufacturing a scope change to satisfy a readiness report.
- “Requirement exists” and “implementation is release-ready” must remain visibly separate states.
- The facade's trust proposition is stronger than generic search: authoritative re-checking, security trimming, non-enumeration, metadata-only disclosure, stale-hit removal, and explicit unavailable state are the value.
- Durable file work must not be rolled back by asynchronous indexing failure.
- Search-removal correctness matters as much as indexing success: removed, archived, revoked, or tombstoned material must not remain discoverable.
- Developer/AI-agent consumption and operator/integration-maintainer inspectability are both important, even though the PRD should state outcomes rather than story personas or transport mechanisms.

## Concise disposition

**No PRD change.** Accept this source as approved historical evidence that FR58 was already part of the inventory and that the July 7 remediation was artifact cleanup. Apply its remaining corrections only to epics, UX, architecture traceability, and readiness artifacts as appropriate. For the product contract, preserve the newer July 14 FR58 metadata-token boundary and OQ5 evidence gate recorded in the current PRD and memlog.
