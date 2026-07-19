# Reconciliation — Sprint Change Proposal: Epic 10 Server Bridge-Read Deferral

**Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-190839.md`  
**Source date:** 2026-07-07 19:08:39 +02:00  
**Compared with:** `_bmad-output/planning-artifacts/prd.md` (final, updated 2026-07-14) and `_bmad-output/planning-artifacts/.memlog.md` (updated 2026-07-14)  
**Addendum:** None exists in the bound PRD workspace.  
**Reconciliation disposition:** **No new PRD edit. Already covered and partially superseded by FR58 plus OQ5.** Preserve the fail-safe unavailable behavior, but do not treat documentation of the limitation alone as sufficient for release acceptance.

## Source purpose and status

The source addresses an Epic 10 action item concerning the deployed Server query facade's missing EventStore-backed semantic-indexing bridge read model. It reports that the real `EventStoreSemanticIndexingBridgeStore` existed in the Workers project, while Server composition retained `UnavailableSemanticIndexingBridgeReadModel`. The reported runtime consequence was deliberately fail-safe:

- context search returned `Allowed` with zero items because candidates could not be hydrated and were dropped; and
- indexing status returned `ReadModelUnavailable` rather than a misleading healthy-empty state.

The proposal chose the **record-explicit-release-limitation + bind-wiring-to-Story-11.10** route. It proposed coordinated edits to architecture, epics, and sprint status, with no runtime or wire-contract change. The source frontmatter status is `proposed`; its approval section says Jerome's explicit approval and handoff confirmation were still pending. Its code evidence was recorded at historical HEAD `40cc5e1`. This reconciliation does not infer that the proposed planning edits or later wiring were applied.

The source classifies the change as moderate because several planning artifacts and one action-item status would change in lockstep, even though the implementation effort in this proposal was documentation-only.

## Product-level requirements and decisions relevant to the PRD

The proposal confirms several product decisions that matter to the PRD:

- FR58 remains an active product requirement rather than being removed or silently narrowed.
- Index/facade unavailability must be explicit and fail-safe; a degraded read model must not be presented as healthy.
- Search results that cannot be rehydrated and security-trimmed against current Folders authority must be dropped rather than leaked.
- Indexed search and indexing-status behavior must remain consistent across required public surfaces.
- An implementation limitation must be visible in release readiness, with ownership and a closure condition, rather than hidden behind a nominally successful empty response.

The later PRD goes further than the source's proposed limitation-only branch: it makes authorized non-empty metadata-token results and indexing status a release-blocking implementation-evidence obligation under OQ5.

## Implementation, architecture, and story detail that stays out of the PRD

The following belongs in architecture, story acceptance criteria, sprint status, implementation evidence, and this source document rather than normative PRD prose:

- The location and relocation of `EventStoreSemanticIndexingBridgeStore`.
- The Server-to-Workers project-reference constraint.
- DI registration in `AddFoldersContextSearchFacade` and replacement of `UnavailableSemanticIndexingBridgeReadModel`.
- The historical code comment and file-line references in `FoldersServerServiceCollectionExtensions.cs`.
- Sequencing Story 11.2 before Story 11.10 and assigning wiring to Story 11.10.
- The DCP-capable `aspire run` lane and its boot blocker.
- The separate `FailClosedSemanticIndexingContentMaterializer` population gap.
- Exact edits to `architecture.md`, `epics.md`, and `sprint-status.yaml`.
- The named implementation handoff to Amelia, Winston, Paige, Murat, and the story/action-item bookkeeping.
- The options analysis about wiring immediately, rolling back, or recording the limitation.

These mechanisms and delivery assignments explain how FR58 will be met; they should not be copied into the product contract.

## Already-covered PRD content

The current PRD already captures every product-level consequence with exact stable identifiers or named sections:

1. **MVP Feature Set** (`prd.md`, `### MVP Feature Set`): authorized metadata-token recall and indexing-status queries per **FR58** are explicitly in the MVP. This is the authoritative release-scope classification.
2. **Search Families** (`prd.md`, `#### Search Families`, row “Indexed metadata-token recall (FR58)”): defines asynchronous metadata-token sources, current-authority hydration, availability/freshness output, removal of stale/revoked/unauthorized/hidden hits, and the no-body/no-raw-path boundary.
3. **FR31 — Workspace and Lock Lifecycle:** authorized actors can inspect whether index status is current, delayed, failed, stale, or unavailable.
4. **FR44 — Error, Status, and Diagnostics Contract:** `read-model unavailable` is a required distinct error category.
5. **FR46 — Error, Status, and Diagnostics Contract:** index or read-model failures must expose resulting state, safe cause, retry eligibility, client action, correlation ID, and available metadata-only evidence.
6. **FR58 — Authorized Search Facade:** developers and AI agents can search authorized indexed metadata tokens and query indexing status through REST, SDK, CLI, and MCP; all hits are current-authority-trimmed and hydrated, and unavailability is explicit and fail-safe.
7. **Public Surfaces / C13:** every supported surface cell must pass behavioral parity, and approved Contract Spine/C13 snapshots govern release acceptance.
8. **OQ5 — Open Release Items:** the fail-safe but functionally empty FR58 search/status facade must be replaced with evidence for authorized non-empty results, indexing status, stale/unauthorized hit removal, and unavailable behavior; Search/Delivery owns the item, and it blocks FR58 implementation readiness until FR58 and both C13 operations round-trip through evidence in `docs/exit-criteria/fr58-search-evidence.md`.

The memlog records the same settled decisions:

- FR58 is authorized metadata-token recall through Memories with current-authority hydration and a strict content boundary.
- FR58 was rewritten to include fail-safe indexing status.
- Live-workspace text search (FR34–FR35) and indexed metadata-token recall (FR58) are separate products.
- OQ5 is deferred to Search/Delivery only until authorized non-empty FR58 behavior and C13 evidence exist; it blocks FR58 implementation readiness.

## Genuine PRD gaps

**None remain.** The July 14 PRD and memlog already convert the source's discovered implementation limitation into the precise release blocker that was missing on July 7.

The PRD does not need to name Story 11.10, the EventStore bridge class, DI registration, or DCP lane. OQ5 appropriately defines the observable closure outcome and evidence instead of freezing a particular implementation mechanism.

## Conflicts and supersession

### Superseded release-readiness interpretation

The source recommends satisfying the Epic 10 action by recording an explicit release limitation and deferring actual wiring to Story 11.10. That remains a valid planning/traceability action, but it is **not sufficient under the current PRD**:

- current **OQ5** says every open release item must close before release acceptance;
- OQ5 requires authorized non-empty metadata-token results and indexing-status evidence, not merely an honest unavailable response; and
- the memlog explicitly says OQ5 blocks FR58 implementation readiness until FR58 and C13 evidence prove authorized non-empty behavior.

Therefore, `Allowed` with permanently zero items plus `ReadModelUnavailable` is a correct fail-safe degraded outcome, not an acceptable steady-state implementation of FR58 for release. Story 11.10 may still own the wiring, but it must close OQ5 before release acceptance unless an explicit later product-scope change removes FR58 from the release.

### Phase classification conflict

The source calls this “Phase-2 release-readiness evidence, not MVP scope.” The current PRD explicitly includes FR58 in **MVP Feature Set**. If “Phase 2” was only an internal delivery-track label, it has no product-scope effect; if it meant post-MVP, that interpretation is superseded by the current PRD.

### Non-conflicting elements

The following source decisions remain compatible:

- keep FR58 unchanged rather than weaken it;
- preserve explicit `ReadModelUnavailable` behavior and fail-safe result dropping;
- avoid OpenAPI/SDK/CLI/MCP schema churn when the existing contract already expresses the behavior;
- assign implementation mechanics to architecture/stories rather than the PRD; and
- make the limitation and owner visible until evidence closes it.

No memlog decision contradicts the safe-unavailable fallback itself. The supersession concerns whether that fallback alone can close release readiness.

## Recommended stable-ID edits or additions

- **FR31:** No edit.
- **FR44:** No edit.
- **FR46:** No edit.
- **FR58:** No edit; preserve both the authorized non-empty capability and explicit fail-safe unavailability behavior.
- **OQ5:** No edit; it is the stable-ID destination for this proposal's release-readiness consequence and already has owner, blocking condition, closure evidence, and approvers.
- **New FR/NFR/OQ:** None.
- **Renumbering:** None; preserve every existing FR, OQ, UJ, SM, and C identifier.

If the aggregate PRD update records reconciled inputs in metadata/history, this proposal may be listed there without changing normative content.

## Qualitative ideas the FR structure might otherwise drop

The source preserves useful implementation and governance qualities that should remain visible in downstream records:

- **Honest degradation:** explicit unavailable is better than a healthy-looking empty result.
- **Fail-safe hydration:** candidates without current authoritative Folders hydration are dropped, never guessed or leaked.
- **Limitation traceability:** a deferral needs a reason, owner, consuming story, closure evidence, and release consequence.
- **Seam-aware sequencing:** shared API pinning and Server/Workers boundary realignment should precede relocation to avoid disposable work.
- **Evidence realism:** wiring is not proven merely by compilation when the live DCP/AppHost lane and data-population path remain blocked.
- **Separate failure layers:** producer/materializer population, bridge-read hydration, and query-facade availability are distinct gaps and should not be collapsed into one vague “search unavailable” label.

These are valuable architecture and delivery principles, but the source proposal already retains their full rationale. Creating an addendum solely to duplicate the class names, story sequencing, or lane details is unnecessary.

## Concise disposition

**No new `prd.md` change; no addendum; retain OQ5 as the open item.** Treat the proposal as historical evidence for why OQ5 exists and as an implementation-planning input for the bridge wiring. The documentation-only limitation branch is superseded as a release-closure strategy: fail-safe unavailability remains required behavior, while authorized non-empty FR58 behavior and both C13 operation paths must be evidenced before release acceptance.
