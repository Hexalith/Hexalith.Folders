# Sprint Change Proposal: Memories and FrontComposer Research Alignment

Date: 2026-05-11
Project: Hexalith.Folders
Mode: Incremental
Status: Approved and applied

## 1. Issue Summary

New technical research for `Hexalith.Memories` semantic indexing/RAG and `Hexalith.FrontComposer` UI integration was added after the PRD, architecture, epics, sprint plan, and early Epic 1 stories were created.

The architecture now includes these research reports as inputs:

- `_bmad-output/planning-artifacts/research/technical-hexalith-memories-semantic-indexing-rag-research-2026-05-11.md`
- `_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md`

The current PRD and epics already support the broad concepts of context queries, read-only operations visibility, tenant isolation, and metadata-only audit. The gap is that current story language does not yet explicitly preserve the new architecture boundaries:

- Memories must be treated as a derived semantic index, never an authoritative Folders datastore.
- Future semantic/RAG retrieval must run only after Folders authorization and policy checks.
- The operations console should be implemented as a FrontComposer-hosted Blazor Web App shell, not as an ad hoc Fluent UI console.

## 2. Impact Analysis

### Epic Impact

**Epic 1: Bootstrap Canonical Contract For Consumers And Adapters**

Epic 1 remains viable. Story 1.9 should include extension-safe vocabulary for future semantic/RAG context-query families while keeping Memories runtime integration out of contract authoring scope.

**Epic 4: Repository-Backed Workspace Task Lifecycle**

Epic 4 remains viable. Story 4.8 should clarify that semantic/RAG retrieval backends are invoked only after Folders authorization and policy checks. No Memories ingestion or retrieval implementation is promoted into MVP by this proposal.

**Epic 6: Read-Only Operations Console And Audit Review**

Epic 6 remains viable but needs direct wording updates. Story 6.2 should target a FrontComposer-hosted Blazor Web App console rendered through `FrontComposerShell`. Story 6.5 should require the UX/wireflow notes to capture FrontComposer shell, navigation, projection-view, tenant-context, and read-only command-suppression conventions.

### Story Impact

Affected stories:

- `1-9-author-file-mutation-and-context-query-contract-groups`
- Epic backlog Story 4.8 in `_bmad-output/planning-artifacts/epics.md`
- Epic backlog Story 6.2 in `_bmad-output/planning-artifacts/epics.md`
- Epic backlog Story 6.5 in `_bmad-output/planning-artifacts/epics.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`

No existing implementation work needs rollback.

### Artifact Conflicts

**PRD:** No immediate PRD edit is required. RAG should not become an MVP promise unless the PRD is explicitly updated.

**Architecture:** Already updated with the Memories and FrontComposer technical research. Downstream stories should consume the new "Additional Technical Research: Memories and FrontComposer" section as active guidance.

**UX:** No standalone UX spec exists. Epic 6 and `docs/ux/ops-console-wireflows.md` should carry the FrontComposer UI constraints.

**Sprint Status:** No status change is required unless new stories are added later. This proposal does not add, remove, or renumber stories.

## 3. Recommended Approach

Use **Direct Adjustment**.

Rationale:

- The architecture has already absorbed the research.
- Current implementation is still early enough to avoid churn.
- The PRD MVP scope remains intact.
- The changes are story-language and backlog-boundary corrections, not a fundamental product pivot.
- Rollback would add unnecessary risk.
- Promoting Memories/RAG into MVP would create new contract, worker, projection, query facade, test, and release-evidence obligations that are not needed for the current MVP.

Scope classification: **Minor**. The proposal can be implemented through targeted artifact edits and then handed to Developer agents through the normal story flow.

## 4. Detailed Change Proposals

### Proposal 1: Story 1.9 Context Query Contract Extension Point

Artifact: `_bmad-output/implementation-artifacts/1-9-author-file-mutation-and-context-query-contract-groups.md`

Section: Acceptance Criteria + Dev Notes

Change:

```markdown
1. Given workspace and lock contract groups from Story 1.8 exist or are explicitly reference-pending, when this story is complete, then `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` contains file mutation and context-query operation groups for add file, change file, remove file, file tree/listing, file metadata, search, glob, and bounded range-read operations, and includes extension-safe vocabulary for future semantic/RAG context-query families without implementing Memories integration in this story.
```

```markdown
4. Given context queries are controlled workspace operations, when tree, metadata, search, glob, bounded range-read, or future semantic/RAG contracts are authored, then authorization order is tenant access, folder ACL, path policy, sensitivity classification, query/input limits, then query execution; the contract does not allow search-first/filter-later or semantic-retrieval-first/filter-later behavior.
```

Add under Scope Boundaries:

```markdown
- Memories-backed semantic indexing and RAG retrieval are downstream integration work. This story may define reusable query-family vocabulary and safe extension points only; it must not add Memories package references, workers, indexing projections, semantic index schemas, retrieval runtime behavior, or RAG response assembly.
```

Add under Contract Group Requirements:

```markdown
- Future semantic/RAG context queries must follow the same context-query authorization order: tenant access, folder ACL, path policy, sensitivity classification, C4 bounds, then retrieval. Folders remains the policy enforcement point; derived indexes such as Hexalith.Memories are never authoritative for tenant, folder, file, or authorization truth.
```

### Proposal 2: Story 4.8 Runtime Context Query Boundary

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 4.8: Query file context with policy boundaries`

Change:

```markdown
### Story 4.8: Query file context with policy boundaries

As a developer or AI agent,
I want file tree, metadata, search, glob, bounded range-read, and extension-safe semantic context-query behavior,
So that task context is useful without unbounded scans, stale derived-index authority, or secret exposure.

**Acceptance Criteria:**

**Given** the actor has context-query permission
**When** a context query runs
**Then** tenant access, folder ACL, path policy, sensitivity classification, binary/large-file policy, and range/result limits are enforced before execution
**And** denied queries produce metadata-only audit evidence
**And** any semantic/RAG retrieval backend, including Hexalith.Memories, is invoked only after Folders authorization and policy checks pass
**And** derived semantic indexes are never treated as authoritative for tenant access, folder ACL, file truth, workspace state, or audit truth.
```

### Proposal 3: Story 6.2 FrontComposer-Hosted Console

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 6.2: Scaffold Blazor Server console with Fluent UI`

Change:

```markdown
### Story 6.2: Scaffold FrontComposer-hosted read-only operations console

As an operator,
I want a read-only Blazor Web App console hosted by `Hexalith.Folders.UI` and rendered through `FrontComposerShell`,
So that I can diagnose workspace state through a governed, tenant-aware UI.

**Acceptance Criteria:**

**Given** projection query endpoints exist
**When** the console shell is implemented
**Then** `Hexalith.Folders.UI` is a Blazor Web App host using Interactive Server rendering, `FrontComposerShell` as the primary layout, Fluent UI through the FrontComposer/Shell pattern, OIDC auth, SDK or read-only query-service projection access, and no direct aggregate write paths
**And** a real Folders/Tenants `IUserContextAccessor` replaces the fail-closed FrontComposer default before tenant-scoped queries are enabled
**And** navigation supports tenant and folder diagnostic workflows
**And** no FrontComposer mutation command forms, file browsing, file editing, raw diff display, repair actions, credential reveal, or unrestricted filesystem browsing are exposed in MVP.
```

### Proposal 4: Story 6.5 FrontComposer Wireflow Notes

Artifact: `_bmad-output/planning-artifacts/epics.md`

Section: `Story 6.5: Author console diagnostic wireflow notes`

Change:

```markdown
**Given** PRD console requirements, architecture decisions F-1 through F-7, and the FrontComposer technical research exist
**When** console wireflow notes are authored
**Then** folder, workspace, provider, audit, incident-mode, redaction, loading, empty, and error states are described under `docs/ux/ops-console-wireflows.md`
**And** the notes identify FrontComposer shell layout, navigation, projection-view composition, tenant/user context expectations, read-only command-suppression behavior, and generated/custom projection boundaries
**And** the notes identify keyboard-navigation, focus, non-color-only status, zoom readability, and redaction-vs-missing expectations for Epic 6 stories
**And** Stories 6.6, 6.7, 6.8, 6.9, and 6.10 cannot begin implementation until `docs/ux/ops-console-wireflows.md` exists and has been reviewed against PRD console requirements, architecture decisions F-1 through F-7, and the FrontComposer technical research.
```

### Proposal 5: Deferred Memories/RAG Work

Artifact: `_bmad-output/implementation-artifacts/deferred-work.md`

Section: new dated section

Change:

```markdown
## Deferred from: correct-course Memories and FrontComposer research alignment (2026-05-11)

- Do not promote Hexalith.Memories semantic indexing or RAG retrieval into MVP unless the PRD is explicitly updated. Current approved course correction keeps Memories as an architecture-guided extension path.
- When a downstream story first implements Memories integration, add a dedicated story or story split for worker-owned semantic indexing:
  - worker-side `IFolderSemanticIndexingClient` port,
  - optional `Hexalith.Memories.Client.Rest` / `Hexalith.Memories.Contracts` dependency only from `Hexalith.Folders.Workers`,
  - Folders-owned indexing bridge projection for `file version -> Memories workflow/memory unit/status`,
  - stable source URI/idempotency metadata,
  - explicit skipped/too-large/binary/excluded statuses,
  - authorized RAG query facade that applies tenant access, folder ACL, path policy, sensitivity classification, and C4 limits before calling Memories.
- If Memories packages or project references are introduced, update root dependency detection with `HexalithMemoriesRoot` and keep submodule initialization root-level only.
- Operations-console stories may display semantic-indexing status only as metadata/projection state; they must not expose indexed content, snippets, raw Memories payloads, file browsing, or RAG response assembly in MVP.
```

## 5. Implementation Handoff

Change scope: **Minor**

Handoff recipients:

- Developer agent: apply the approved artifact edits.
- Product Owner / backlog maintainer: no story renumbering needed; verify sprint-status remains unchanged.
- Architect: revisit only if RAG is promoted to MVP or if FrontComposer requires endpoint contract changes beyond the approved story text.

Success criteria:

- Story 1.9 includes semantic/RAG-safe extension wording without adding Memories runtime scope.
- Story 4.8 enforces Folders authorization before any semantic/RAG retrieval backend.
- Story 6.2 describes FrontComposer-hosted operations console implementation.
- Story 6.5 requires FrontComposer wireflow guidance before diagnostic page implementation.
- Deferred work records that Memories/RAG is not MVP unless PRD is explicitly updated.
- No recursive submodule initialization guidance is introduced.
