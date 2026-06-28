---
project: Folders
date: 2026-06-27
workflow: bmad-correct-course
trigger: implementation-readiness-report-2026-06-27
mode: Batch
status: approved-and-applied
approvedAt: "2026-06-28"
approvalRecord: "User selected continue (`c`) on 2026-06-28."
verificationStatus: "Passed after adding Hexalith.PolymorphicSerializations as a root references/ submodule."
scope_classification: Moderate
---

# Sprint Change Proposal: Implementation Readiness Reconciliation

## 1. Issue Summary

The 2026-06-27 implementation readiness report marked the project `NEEDS WORK` and identified 15 actionable issues across requirements traceability, UX alignment, epic/workstream structure, story readiness, and release governance.

The report is valid as a readiness trigger, but later or more specific artifacts show that several reported blockers are no longer true blockers:

- `docs/exit-criteria/c3-retention.md` records C3 as approved by PM on 2026-06-22 and Legal on 2026-06-24.
- `docs/exit-criteria/c4-input-limits.md` records C4 as approved by PM on 2026-06-22.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` marks Epic 8, Epic 9, and Epic 10 as `done`.
- `_bmad-output/implementation-artifacts/8-6-record-c3-legal-signoff-and-apply-cascade.md` marks Story 8.6 as `done`.
- Epic 10 detailed story files `10-1` through `10-5` exist and are marked `done`; the seed-level text that remains in `epics.md` is stale planning text, not the current implementation contract.

The real issue is therefore not "start Epic 10" or "wait for C3/C4 approval." The real issue is artifact synchronization and release-governance routing:

1. `epics.md` still carries stale counts, stale Epic 10 seed/readiness language, and incomplete FR58 coverage mapping.
2. UX artifacts do not yet describe the FR58 authorized search and indexing-status experience, even though Story 10.5 implemented the facade and indexing-status projection.
3. C3/C4 are approved in governance artifacts but still appear as reference-pending rows in NFR traceability by deliberate contract-test precedent; that distinction is not clear enough for readiness readers.
4. Workstream 7, Epic 8, and Epic 9 need to remain classified as release governance, release closure, and architecture runway rather than product-capability epics.
5. Remaining Epic 9/10 work is release-readiness follow-up: live DCP-capable AppHost evidence, Server-side bridge-read wiring or explicit limitation, real mutation-to-index materialization, Memories read-facade egress-policy clarity, `.slnx` inventory drift, and naming drift.

## 2. Impact Analysis

### Epic Impact

- Epics 1-6 remain product-capability epics and do not need replanning.
- Workstream 7 remains release-governance work and should not be counted as a normal product epic.
- Epic 8 is release-acceptance closure. It is complete in sprint status, but it still owns open release-readiness action items.
- Epic 9 is architecture/platform runway. It is complete in sprint status, but live DCP boot evidence and inert resiliency handling remain action items.
- Epic 10 is now complete as a story execution track. It should no longer be described as seed-level or pending `create-story`. Its remaining work belongs in release-readiness action items, not new Epic 10 story planning unless the team deliberately opens a new epic or story.

### Story Impact

- Do not reopen Story 8.6. It is done; the remaining C3/C4 concern is documentation/traceability explanation.
- Do not run `bmad-create-story` for Epic 10 again. Stories 10.1-10.5 already exist and are done.
- Existing action items in `sprint-status.yaml` should be treated as the source of pending work:
  - DCP-capable AppHost lane and live `folders-index` evidence.
  - Server-side EventStore-backed semantic-indexing bridge read model or explicit release limitation.
  - Real mutation-to-materialized-curated-text-to-index path.
  - Memories read-facade egress-policy reconciliation.
  - `.slnx` canonical inventory drift.
  - `SemanticIndexing` vs `SearchIndexing` naming drift.

### Artifact Conflicts

- PRD already includes FR58, so no new FR is required.
- `epics.md` still needs FR58 coverage and count/classification corrections.
- `ux-design-specification.md` and `docs/ux/ops-console-wireflows.md` need an FR58 addendum without disrupting the stable UX-DR1 through UX-DR32 traceability set.
- `architecture.md` mostly contains the corrected Memories mechanism, but its "dormant until Epic 10" language should be revised to distinguish "implemented structurally" from "live evidence pending."
- `docs/exit-criteria/nfr-traceability.md` should explain why approved governance criteria can still have reference-pending traceability rows, and should stop implying that C3/C4 approval itself is pending.
- The readiness report should be treated as a historical trigger. This proposal is the corrective addendum rather than editing that report in place.

### Technical Impact

No production code change is required by this proposal. The technical work is governance/artifact synchronization and release-readiness routing.

Any later implementation work must preserve:

- The Memories search-index mechanism: `SearchIndexEntryChanged` / `SearchIndexEntryRemoved` CloudEvents over Dapr pub/sub, not `MemoriesClient.IngestAsync`.
- The shared `folders-index` isolation model: Memories ranks candidates; Folders authorizes, trims, hydrates, and redacts.
- The C3/C4 approved values and the deliberate `reference_pending_*` retention-class identifiers.
- The existing no-recursive-submodule policy.

## 3. Checklist Results

| Checklist item | Status | Notes |
| --- | --- | --- |
| 1.1 Triggering story identified | N/A | Trigger is the 2026-06-27 readiness report, not a single implementation story. |
| 1.2 Core problem defined | Done | Artifact synchronization and release-governance classification drift after Epic 8/9/10 close-out. |
| 1.3 Supporting evidence gathered | Done | Readiness report, sprint status, C3/C4 docs, Epic 10 story files, Epic 10 retro, UX wireflow docs. |
| 2.1 Current epic viability | Done | Product epics remain viable; non-product epics/workstreams need classification clarity. |
| 2.2 Epic-level changes | Action-needed | Patch `epics.md` counts, FR58 mapping, and Epic 10 current-state language. |
| 2.3 Future epic review | Done | No new numbered epic exists. Remaining work is release-readiness action-item closure. |
| 2.4 Obsolete/new epics | Done | No obsolete product epic; do not add an epic unless release owners request a new release-readiness epic. |
| 2.5 Priority/order changes | Done | Prioritize live evidence and release limitation clarity before claiming operational readiness. |
| 3.1 PRD conflicts | Done | PRD includes FR58; no PRD blocker found. |
| 3.2 Architecture conflicts | Action-needed | Revise stale "dormant until Epic 10" wording and clarify live-proof pending status. |
| 3.3 UX conflicts | Action-needed | Add FR58 authorized-search/indexing-status treatment to UX artifacts. |
| 3.4 Other artifacts | Action-needed | NFR traceability and sprint-status action items need explicit reconciliation language. |
| 4.1 Direct adjustment | Viable | Medium effort, low-to-medium risk. Best path. |
| 4.2 Rollback | Not viable | Completed stories should not be reverted to fix artifact wording. |
| 4.3 MVP review | Not viable | MVP scope does not need reduction; readiness evidence needs synchronization. |
| 4.4 Recommended path | Done | Direct adjustment plus release-governance handoff. |
| 5.1 Issue summary | Done | Captured above. |
| 5.2 Epic/artifact needs | Done | Captured below as detailed proposals. |
| 5.3 Path forward | Done | Direct adjustment. |
| 5.4 MVP impact/action plan | Done | No MVP scope change; release-readiness evidence remains pending. |
| 5.5 Handoff plan | Done | PO/Developer/Architect/UX/Tech Writer coordinated artifact patch. |
| 6.1 Checklist completion | Done | All applicable sections addressed. |
| 6.2 Proposal accuracy | Done | Based on current artifact state as of 2026-06-27. |
| 6.3 User approval | Action-needed | Awaiting approval. |
| 6.4 Sprint status update | Action-needed | Only after approval. |
| 6.5 Next steps | Action-needed | Awaiting approval. |

## 4. Recommended Approach

Use **Direct Adjustment**.

Do not reopen completed stories and do not roll back Epic 10. Patch planning, UX, architecture, NFR traceability, and sprint-status action-item language so the artifact set reflects the current truth:

- C3 and C4 are approved governance criteria.
- Story 8.6 is done.
- Epic 10 story execution is done.
- FR58 is implemented structurally and represented in PRD/architecture/OpenAPI/SDK/MCP/CLI/server/console-status artifacts.
- Live operational proof and production-readiness limitations remain open action items.

Effort estimate: Medium.

Risk level: Low-to-medium. The highest risk is accidentally weakening conformance tests or changing approved governance semantics while trying to clean up wording. The patch should be documentation/planning only unless a specific action item is deliberately pulled into implementation.

Timeline impact: 0.5 to 1 focused planning pass for artifact synchronization, plus separate release-readiness execution for the already-recorded action items.

## 5. Detailed Change Proposals

### Proposal A: Patch `epics.md` frontmatter counts and classification

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: frontmatter

OLD:

```yaml
epicCount: 8
storyCount: 93
```

NEW:

```yaml
epicCount: 10
storyCount: 102
productEpicCount: 6
nonProductWorkstreams:
  - id: workstream-7
    classification: release-governance
  - id: epic-8
    classification: release-closure
  - id: epic-9
    classification: architecture-platform-runway
phase2CapabilityEpics:
  - id: epic-10
    classification: phase2-authorized-search-index
countsReviewedAt: "2026-06-27"
```

Rationale: The document currently contains 102 `### Story` entries and sections through Epic 10 plus Workstream 7. Counts and classification need to stop misleading readiness tooling.

### Proposal B: Add FR58 to `epics.md` requirements and coverage language

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Requirements Inventory / Functional Requirements / Audit and Operations Visibility

OLD:

```markdown
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.
```

NEW:

```markdown
- FR57: Platform engineers can inspect provider support evidence for GitHub and Forgejo where it affects operational readiness.

#### Authorized Search Facade

- FR58: Developers and AI agents (via API, SDK, MCP, and CLI) can search the content that Folders has indexed into the Memories search index and receive only results they are authorized to see, security-trimmed to their tenant/folder/workspace, hydrated from the authoritative Folders read, and redacted to metadata-only, without Folders ever leaking another managed tenant's content, raw paths, snippets, source URIs, or hidden-resource existence.
```

Rationale: PRD and architecture already include FR58. `epics.md` must not remain at FR57 in the inventory.

### Proposal C: Replace stale Epic 10 seed-level status

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: Epic 10 preface

OLD:

```markdown
_Phase 2 - gated on Epic 9 + C4 (large-file guardrail) and C9 (path-exposure policy). Stories are backlog stubs pending `create-story`; ACs below are seed-level and will be detailed per story. Architecture inputs: `architecture.md` §130-156 (Memories integration track)._
```

NEW:

```markdown
_Phase 2 - gated on Epic 9 + C4 (large-file guardrail) and C9 (path-exposure policy). As of 2026-06-27, implementation story files 10.1 through 10.5 exist under `_bmad-output/implementation-artifacts/` and are marked `done` in sprint status. The terse ACs below remain planning traceability only; current implementation contracts and verification notes live in the 10.x story files and `epic-10-retro-2026-06-27.md`. Remaining work is tracked as release-readiness action items, not as seed-level Epic 10 story readiness._
```

Rationale: The readiness report correctly flagged stale seed text, but the current implementation artifacts already contain detailed completed stories.

### Proposal D: Add UX FR58 addendum without renumbering UX-DR IDs

Artifact: `_bmad-output/planning-artifacts/ux-design-specification.md`

Section: after Stable UX Design Requirements

OLD:

```markdown
| UX-DR32 | Validate accessibility with automated checks, keyboard-only walkthroughs for the three critical journeys, screen reader review, forced-colors/high-contrast checks where supported, color-blindness review, and focus management checks. |
```

NEW:

```markdown
| UX-DR32 | Validate accessibility with automated checks, keyboard-only walkthroughs for the three critical journeys, screen reader review, forced-colors/high-contrast checks where supported, color-blindness review, and focus management checks. |

### FR58 Authorized Search And Indexing-Status Addendum

FR58 uses the existing UX-DR set rather than adding new UX-DR identifiers. The authorized search facade and indexing-status view must preserve UX-DR2, UX-DR7, UX-DR10, UX-DR11, UX-DR13, UX-DR20, UX-DR21, UX-DR22, UX-DR26, UX-DR27, UX-DR30, UX-DR31, and UX-DR32.

Authorized search results are not file-content previews. Results show metadata-only, authorized, hydrated Folders evidence: tenant/folder/workspace scope, opaque file-version handle, indexing status, sensitivity/redaction state, freshness, and safe correlation/task evidence. Raw Memories snippets, raw source URIs, raw paths, file contents, diffs, provider payloads, and hidden-resource existence are never displayed.

Indexing-status evidence uses the bridge vocabulary `indexed`, `stale`, `skipped`, `failed`, `tombstoned`, and `reconciliation_required`. Each status uses the same text-plus-icon-or-shape, accessible-label, freshness, redaction-vs-unknown, and non-color-only rules already defined for the operations console.
```

Rationale: UX needs FR58 coverage, but the existing UX-DR1 through UX-DR32 set is explicitly stable and used by tests/review. Add an addendum instead of creating UX-DR33.

### Proposal E: Patch wireflow known omissions and add FR58 flow note

Artifact: `docs/ux/ops-console-wireflows.md`

Section: Ownership Metadata / known_omissions

OLD:

```markdown
- known_omissions: No screen-level visual mockups, no component-API signatures beyond names already
  shipped in Stories 6.2-6.4, no resolved values for the reference-pending platform inputs in the
  "Pending and Deferred Inputs" section (C2 status-freshness target, C3 retention durations, C4
  metadata-filter vocabulary, `ProjectionAvailability` redacted/unknown freeze, French
  localization). These remain owned elsewhere.
```

NEW:

```markdown
- known_omissions: No screen-level visual mockups and no component-API signatures beyond names already
  shipped in Stories 6.2-6.4. C3 retention and C4 input limits are approved in their exit-criteria
  artifacts; this wireflow does not duplicate their numeric policy. Remaining deferred inputs are C2
  status-freshness target, `ProjectionAvailability` redacted/unknown freeze, French localization, and
  live Epic 10 operational evidence for the DCP-capable AppHost round-trip.
```

Add section:

```markdown
## FR58 Authorized Search And Indexing-Status Flow

Authorized search is a metadata-only diagnostic workflow over the Folders query facade, not a file browser and not a Memories raw-result viewer.

flow: Search or filter authorized indexed content -> Folders authorizes and trims -> result list shows hydrated metadata-only evidence -> user opens indexing-status detail -> status explains indexed/stale/skipped/failed/tombstoned/reconciliation-required with freshness and redaction state.

The UI never displays raw Memories `ContentSnippet`, raw `SourceUri`, raw paths, raw file contents, diffs, provider payloads, or unauthorized resource existence.
```

Rationale: Wireflow docs predate FR58 and still imply C3/C4 are unresolved.

### Proposal F: Clarify C3/C4 NFR traceability status

Artifact: `docs/exit-criteria/nfr-traceability.md`

Section: Status semantics or Reference-pending release-blocking gaps

OLD:

```markdown
- `C4` - PM approval of context-query input bounds and large-file/payload limits. Owner: PM. Consuming story:
  `4-8`. Surfaced by NFR26 and NFR28.
- `C3` - Legal + PM approval of retention durations and tenant-deletion dispositions. Owner: Legal + PM.
  Consuming story: `7-11`. Surfaced by NFR57.
```

NEW:

```markdown
- `C4` - Governance approval exists in `docs/exit-criteria/c4-input-limits.md` (PM approved 2026-06-22).
  This traceability row remains reference-pending only where the conformance suite deliberately tracks
  downstream evidence/implementation closure separately from governance approval. Owner: PM / Contracts.
  Consuming story: `4-8`. Surfaced by NFR26 and NFR28.
- `C3` - Governance approval exists in `docs/exit-criteria/c3-retention.md` (PM approved 2026-06-22;
  Legal approved 2026-06-24). This traceability row remains reference-pending only where the conformance
  suite deliberately tracks downstream evidence/implementation closure separately from governance approval.
  Owner: Legal + PM / Contracts. Consuming story: `7-11`. Surfaced by NFR57.
```

Rationale: Story 8.6 records an intentional deviation: C3/C4 can be approved in governance while reference-pending rows remain for contract-spine guard behavior. The current prose reads as if approval itself is still missing.

### Proposal G: Update architecture live-state wording

Artifact: `_bmad-output/planning-artifacts/architecture.md`

Section: Memories integration implications

OLD:

```markdown
- The Epic 9 AppHost ships the `hexalith-folders -> folders-index` source->index routing on the standalone `memories` server in Phase 1 (Story 9.3), but it stays dormant: end-to-end ingestion and search are activated only when the Epic 10 worker-side producer emits `SearchIndexEntryChanged` events with source `hexalith-folders`.
```

NEW:

```markdown
- The Epic 9 AppHost ships the `hexalith-folders -> folders-index` source->index routing on the standalone `memories` server in Phase 1 (Story 9.3). Epic 10 now implements the producer, removal/archive semantics, bridge projection, and authorized query facade. Release readiness still requires live DCP-capable AppHost evidence for topology boot, `folders-index` auto-provisioning, search-index publish/remove/archive behavior, and query-facade hydration.
```

Rationale: The old statement was correct before Epic 10, but it now reads as if Epic 10 has not happened.

### Proposal H: Add one sprint-status action item for artifact synchronization

Artifact: `_bmad-output/implementation-artifacts/sprint-status.yaml`

Section: `action_items`

NEW:

```yaml
  - epic: planning
    action: "Apply the 2026-06-27 correct-course artifact synchronization: epics.md counts/classification/FR58 coverage, UX FR58 addendum, wireflow FR58 note, C3/C4 NFR traceability clarification, and architecture live-state wording."
    owner: "Paige / Winston / Amelia"
    priority: high
    status: open
```

Rationale: Existing action items cover live evidence and technical follow-ups, but no item owns the readiness-report artifact synchronization itself.

## 6. Implementation Handoff

Scope classification: **Moderate**.

Route to:

- Paige / Tech Writer: apply documentation and traceability wording updates.
- Winston / Architect: approve architecture wording and governance/traceability distinction.
- Amelia / Developer: apply repo edits and run focused conformance tests.
- Murat / Test Architect: confirm no conformance guard is weakened, especially around C3/C4 reference-pending semantics.
- Jerome / PM: approve the release-governance classification and confirm no new product epic is required.

Recommended implementation order:

1. Patch `epics.md` counts, classifications, FR58 coverage, and Epic 10 current-state language.
2. Patch UX spec and wireflow FR58 addendum.
3. Patch `nfr-traceability.md` wording without changing status values until the conformance tests are reviewed.
4. Patch architecture live-state wording.
5. Add the sprint-status artifact-sync action item.
6. Run focused docs/gates:
   - `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --filter FullyQualifiedName~NfrTraceabilityConformanceTests`
   - `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --filter FullyQualifiedName~ScaffoldContractTests`
   - `pwsh ./tests/tools/run-nfr-traceability-gates.ps1`

If tests assert exact wording in the touched artifacts, update tests only to preserve the new truthful distinction. Do not remove the deliberate C3/C4 reference-pending guard unless the team explicitly decides to change that contract.

## 7. Success Criteria

- `epics.md` no longer claims `epicCount: 8`, `storyCount: 93`, or "Epic 10 stubs pending create-story" as current truth.
- FR58 appears consistently in PRD, architecture, epics coverage, UX addendum, and wireflow notes.
- C3/C4 are described as approved governance criteria, with any remaining reference-pending rows clearly framed as downstream evidence/guard posture rather than missing approval.
- Workstream 7, Epic 8, and Epic 9 are not treated as normal product capability epics in readiness/velocity reporting.
- Epic 10 remains closed; pending work is tracked as release-readiness action items.
- No code, contract, or governance guard is weakened by the documentation patch.

## 8. Approval Request

Review this draft proposal and choose:

- Continue: approve this proposal for implementation.
- Edit: request changes to the proposal before implementation.

## 9. Approval Record

Approved for implementation on 2026-06-28 when Jerome selected continue (`c`). The artifact
synchronization patch was applied to planning, UX, architecture, NFR traceability, sprint status, and
this proposal.

## 10. Verification Record

- `git diff --check` passed.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~NfrTraceabilityConformanceTests` passed: 16 tests.
- `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore --filter FullyQualifiedName~ScaffoldContractTests` passed: 10 tests.
- `references/Hexalith.PolymorphicSerializations` was added as a root-level `references/` submodule at `3f7ca70808300b29e0697a76c8198e67a57a6806`.
- The mistakenly initialized nested Memories checkout was deinitialized; `references/Hexalith.Memories/references/Hexalith.PolymorphicSerializations` is no longer an initialized working tree.
- `pwsh ./tests/tools/run-nfr-traceability-gates.ps1` passed with the root-level submodule layout. VSTest hit the local socket restriction and the script used its xUnit in-process fallback; 16 NFR traceability tests passed.
