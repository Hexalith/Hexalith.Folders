---
source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-content-materializer.md
source_date: 2026-07-07
source_status: applied-planning-change
reconciled_against:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/.memlog.md
addendum: absent
disposition: no-prd-change
---

# Reconciliation — Real Content Materializer for Search-Index Population

## Source purpose and status

This moderate, incremental correct-course proposal was requested by Jerome to convert a deliberately deferred Epic 10 closure item into Story 10.6. Its immediate purpose was to replace the always-unavailable semantic-indexing materializer with a metadata-derived materializer so authorized folder-mutation evidence could populate the Memories search index without exposing file content. It also reserved any future body-content materialization for separate C9/Security/PM approval.

The document says its planning changes were already applied: Epic 10 was reopened, Story 10.6 and sprint tracking were added, the Story 11.1 baseline was annotated, and PRD/architecture wording was updated. Code implementation, test evidence, review, Server-side bridge wiring, and live DCP-lane proof remained outstanding. The source has no formal approval frontmatter, but it records Jerome's explicit decisions on hybrid strategy, story placement, and sequencing.

## PRD-relevant product decisions

1. FR58 remains in current product scope; the materializer gap does not justify removing, renumbering, or redefining it.
2. The current-release search/index behavior is metadata-derived only. Indexable text and attributes may be curated from authorized mutation metadata, but raw paths, file bodies, snippets, source URIs, and hidden-resource existence must not egress.
3. Indexing and search availability must be real and observable: authorized, policy-passing mutation metadata should be capable of producing a non-empty index entry and later an authorized search result rather than terminating in an always-unavailable placeholder.
4. Materialization or indexing failure must fail safely and remain inspectable; it must not weaken the durable file-operation boundary.
5. Indexed body-content recall is a different increment. It requires a new content-access path and separate Security and PM approval under C9; it is not silently included in FR58.
6. Current FR58 delivery is not release-ready merely because a story is planned or marked done. End-to-end behavior, stale/unauthorized-hit removal, status behavior, surface parity, and unavailable behavior still require canonical evidence.

## Already covered in the current PRD

- **FR58 — Authorized Search Facade** now says developers and AI agents can search authorized metadata tokens “derived from indexed mutation metadata” and query indexing status through REST, SDK, CLI, and MCP. This is the exact product consequence of the metadata-derived materializer decision.
- **FR58** already requires current-authority trimming and authoritative Folders hydration, drops stale/archived/revoked/unauthorized/hidden hits, limits results to C9-classified metadata and opaque authorized identity, and makes index/facade unavailability explicit and fail-safe.
- The paragraph following **FR58** explicitly excludes cross-workspace body-content indexing, indexed body snippets, and indexed body recall from FR58/current release; it requires separate Security and PM approval and a future product requirement.
- The **MVP Feature Set** explicitly includes authorized metadata-token recall and indexing-status queries per FR58 while excluding cross-workspace body-content recall.
- The **Explicit MVP Non-Goals** repeats the separation between FR58 metadata-token recall and bounded live-workspace body search under **FR34–FR35**.
- **FR31** and **FR46** already expose index/status currency, delay, failure, staleness, unavailability, lifecycle state, retry eligibility, client action, correlation, and safe evidence.
- **FR40–FR42** cover stable failure/retry/conflict behavior and idempotent mutation semantics at the product boundary; materializer-specific replay and CloudEvent mechanics remain implementation details.
- **FR47–FR51** cover REST/SDK/CLI/MCP availability and cross-surface equivalence.
- **FR55** excludes file contents, generated context, provider payloads, secrets, and unauthorized existence from events, logs, traces, projections, audit, diagnostics, errors, and console responses.
- **OQ5** is the canonical unfinished delivery gate: replace the fail-safe but functionally empty facade with evidence for authorized non-empty metadata-token results, index status, stale/unauthorized-hit removal, unavailable behavior, both C13 operations, and PM/Security/Test approval at `docs/exit-criteria/fr58-search-evidence.md`.
- The memlog records the same governing decisions: FR58 is metadata-token recall; body-content recall is outside the current release; FR34/FR35 live-workspace snippets are a separate product; and OQ5 blocks FR58 implementation readiness until approved evidence exists.

## Genuine PRD gaps

None.

The source's proposed two-increment scope clarification has already been absorbed and made more rigorous in the current PRD. The current text both names mutation-metadata-derived tokens and requires a future product requirement—not merely a technical follow-up—before indexed body content can enter scope. OQ5 also prevents the planning story from being mistaken for accepted delivery.

## Conflicts and supersession

### 1. “Real content materializer” is superseded as product language

The title and trigger speak of “real folder mutation evidence” and “real content,” while the selected increment actually materializes only curated mutation metadata. The current PRD's metadata-token language is authoritative because it avoids implying that file bodies enter the index.

### 2. A C9-gated follow-up is not yet approved product scope

The source records authorized real-content materialization as an explicit C9-gated follow-up. The current PRD and memlog strengthen this boundary: indexed bodies, snippets, and recall require separate Security and PM approval **and a future product requirement**. Treat the source's follow-up as a deferred idea, not as committed FR58 scope or an implementation task that may proceed after a technical gate alone.

### 3. Story completion cannot supersede OQ5

The source's success criteria stop at worker/port and Tier-3 evidence, with live deployment and the Server-side bridge still outstanding. The current PRD correctly retains **OQ5** as the FR58 readiness gate. Story 10.6 completion, Epic 10 status, or a seeded worker-port round-trip cannot by themselves close OQ5.

### 4. Source line references and applied-state claims are historical

The source's PRD line number and its “all APPLIED” artifact assertions describe the July 7 state. The PRD was substantially reconciled on July 14. Current stable IDs and current artifact contents govern; do not use old line numbers or reapply the source's PRD wording mechanically.

## Implementation, architecture, story, and delivery detail that stays out of the PRD

- `ISemanticIndexingContentMaterializer`, `FailClosedSemanticIndexingContentMaterializer`, `MemoriesSemanticIndexingPort`, `FoldersWorkersModule`, `CuratedText`, `CuratedAttributes`, and `ContentBytes` are implementation seams.
- Reopening Epic 10, Story 10.6 placement and acceptance criteria, Epic 11 Story 11.10 code ownership, Story 11.1 pin-map annotation, and the 10.6-before-11.10 sequencing guardrail are epic/story governance.
- Dapr/CloudEvent publication, UTF-8 descriptor bytes, event IDs, idempotency keys, the `folders-index` target, and keeping fail-closed as a fallback are architecture or code decisions.
- Named worker test classes, the sensitive-path corpus, Tier-3 opt-in harness, DCP-capable `aspire run` lane, six-service proof, and fresh code review are verification/delivery details.
- Sprint-status transitions, action-item ownership, and workflow routing to `bmad-create-story`, `bmad-dev-story`, and `bmad-code-review` remain outside the product contract.
- Server-side EventStore-backed bridge-read-model wiring is an architecture/delivery dependency represented in the PRD only by the observable FR58/OQ5 outcomes.

## Recommended stable-ID edits or additions

- **Add no FR and renumber nothing.** Retain **FR58** exactly as currently written.
- **Retain OQ5** under its existing ID and do not close it from Story 10.6 or worker-port evidence alone.
- Preserve **FR31**, **FR46**, **FR47–FR51**, and **FR55** as the surrounding status, parity, failure-visibility, and metadata-boundary consequences; do not add materializer-specific FRs.
- If Security and PM later approve indexed file-body recall, create a new stable FR and reconcile scope, journeys, success evidence, C9 policy, NFRs, and surface contracts. Do not broaden FR58 silently.

## Qualitative ideas at risk

- “Fail closed” is a safety baseline, not a usable product outcome; readiness must prove authorized non-empty behavior without relaxing the boundary.
- The metadata-derived increment is intentionally useful but narrow: it enables discoverability from mutation evidence while keeping file bodies out of events and indexes.
- Search value and search safety must be proven together—non-empty results, current authorization, stale-hit suppression, redaction, unavailability, and surface parity form one trust contract.
- Asynchronous indexing must never roll back or corrupt a durable Folders mutation.
- A refactoring story must preserve the newly approved behavior rather than freezing an obsolete placeholder simply because that placeholder was part of a baseline.
- Live deployment evidence matters; worker-level proof is valuable but not equivalent to the complete Folders facade experience.

## Disposition

**No PRD change.** Treat this source as implementation/planning provenance for the metadata-derived FR58 increment. Its product decisions are already represented more precisely by current **FR58**, the MVP scope/non-goal text, the memlog's metadata-token boundary, and **OQ5**. Preserve the current PRD; route remaining source detail to epics, architecture, implementation, testing, and release-evidence work.
