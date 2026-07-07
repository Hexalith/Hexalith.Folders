---
project_name: Hexalith.Folders
workflow: bmad-correct-course
source_change_file: fable_Folders_changes.md
date: 2026-07-07T08:16:20+02:00
mode: batch
status: approved
scope_classification: moderate
approved_at: 2026-07-07T08:25:25+02:00
approved_by: Jerome
---

# Sprint Change Proposal - Domain-Focus Platform Refactoring

## 1. Issue Summary

`fable_Folders_changes.md` identifies a post-MVP architectural correction for Hexalith.Folders. The product requirements and domain model remain valid: the module still delivers a tenant-scoped, repository-backed workspace control plane for agentic file work. The change is about implementation direction and backlog structure.

The triggering evidence is the 2026-07-07 domain-focus audit at HEAD `533806b`. It found that the core domain project deliberately avoids direct platform references to `Hexalith.Commons` and `Hexalith.EventStore`, while the current implementation re-creates platform capabilities locally. The audit confirms the following concrete issues:

- `src/Hexalith.Folders/Hexalith.Folders.csproj` references only `Hexalith.Folders.Contracts` plus `Dapr.Client`, `Octokit`, and Microsoft extension packages; it does not reference `Hexalith.Commons.*` or `Hexalith.EventStore.Client`.
- `src/Hexalith.Folders.ServiceDefaults` exists even though current Hexalith module instructions say domain modules should not ship their own `*.ServiceDefaults` project when shared platform defaults exist.
- Folders contains local copies or equivalents of platform behavior: TenantAccess projection/evaluator logic, telemetry sources, cursor codecs, read-model storage patterns, URL/secret/correlation helpers, Dapr client construction, Dapr subscription mapping, UI token/user context plumbing, and adapter bootstrap code.
- Several correctness issues are tied to the duplication: plaintext unsigned cursors, per-call Forgejo HTTP handler/client creation, weaker OpsConsole secret filtering, UI token relay failure after Blazor Server circuit startup, sync-over-async user context access, missing CLI/MCP HTTPS guard, and divergent reserved-tenant checks.
- The existing gate topology makes this refactor planning-sensitive: project inventories, route tables, package allow-lists, doc gates, workflow scripts, and test FQNs are heavily pinned.

This is not a PRD scope pivot. It is a technical course correction to reduce duplicated platform code, remove domain infra coupling, and schedule platform-first changes before in-repo consumption.

## 2. Impact Analysis

### Epic Impact

Existing Epics 1-10 are all marked `done` in `_bmad-output/implementation-artifacts/sprint-status.yaml`. Reopening those completed epics would blur release evidence and historical traceability. The recommended change is to add a new non-product epic:

`Epic 11: Domain-Focus Platform Refactoring And Governance Closure`

Epic 11 has no new functional requirement scope. It consumes existing PRD/NFR goals: tenant isolation, metadata-only audit, cross-surface parity, observability, operations-console accessibility, and platform reuse.

Impacted completed epics:

- Epic 2: TenantAccess local projection and authorization plumbing should collapse onto shared Commons/EventStore abstractions where available.
- Epic 3: provider adapters need transport/failure/secret helper dedup and likely adapter-infra relocation.
- Epic 4: cursors, read-model stores, telemetry, idempotency/correlation helpers, and provider correctness fixes affect lifecycle/query implementation.
- Epic 5: CLI/MCP adapter duplication and bearer-token transport guards affect cross-surface parity implementation.
- Epic 6: FrontComposer/Fluent UI conformance needs hardening below the shell.
- Epic 7: ServiceDefaults removal, probe path changes, observability wiring, and CI gate updates affect release-readiness evidence.
- Epic 8: route/envelope/server dedup must preserve 47/47 REST parity and honest-green baseline claims.
- Epic 9: AppHost/Aspire exception should be captured explicitly while deleting ServiceDefaults.
- Epic 10: Memories publisher/read facade should move toward shared Memories producer/read wrappers once available.

### Story Impact

No completed story should be edited in place as if it were still in flight. Instead, Epic 11 should create follow-up stories that supersede or amend implementation details while preserving history.

High-impact existing story areas:

- `2.1`, `2.6`, `2.9`: tenant-access projection/subscription/authz should adopt platform TenantAccess/subscription primitives.
- `3.2` through `3.5`: provider port remains Folders-owned, but generic adapter helpers, URL hygiene, secret detection, deterministic hashing, and transport creation move to shared modules or a Folders infra adapter boundary.
- `4.8`, `4.9`, `4.14`: query cursors, read-model stores, telemetry, context-search hydration, and metadata-only audit helper code need platform alignment.
- `5.2`, `5.3`, `5.6`: CLI/MCP plumbing and bearer-token handling need dedup and secure transport guards.
- `6.2` through `6.11`: UI needs FrontComposer Shell auth/user context/token relay and Fluent component conformance below the shell.
- `7.12`: ServiceDefaults/health/telemetry wiring needs replacement by `Hexalith.Commons.ServiceDefaults`.
- `8.1`, `8.2`: server route implementation should be simplified without changing wire behavior.
- `10.3`, `10.5`: Memories publish/search wrappers should be shared upstream where platform APIs exist.

### Artifact Conflicts

PRD conflict:

- No product conflict. The PRD's MVP objective remains intact.
- Add a technical governance note so future post-MVP work does not normalize local platform copies.

Epics conflict:

- `epics.md` currently reports `epicCount: 10`, `storyCount: 102`, and all epics complete.
- Add Epic 11 and increase story count to 115 if the 13 proposed stories are accepted.

Architecture conflict:

- `architecture.md` still describes `Hexalith.Folders.ServiceDefaults` as part of the intended structure.
- It also says `Hexalith.Folders` references Contracts only, while the current Hexalith domain-module rule requires reuse of shared platform modules for hosting, telemetry, read stores, cursor codecs, and shared boilerplate.
- Update the architecture to document the sanctioned AppHost/Aspire exception, delete ServiceDefaults, and permit domain references to narrow shared platform abstraction packages.

UX conflict:

- UX requirements are valid, but current implementation evidence indicates drift below the Shell: raw tables/inputs/buttons, undefined `fc-*` classes, local token relay/user-context code, and custom icon/svg components.
- UX docs should add an implementation correction note: FrontComposer/Fluent reuse is mandatory below the shell, not just at the layout boundary.

Sprint status conflict:

- `sprint-status.yaml` has all epics done and only action items open.
- Add Epic 11 as `backlog` after approval; do not rewrite completed status entries.

### Technical Impact

Expected impact:

- Large in-repo diff across domain, server, workers, UI, client, CLI/MCP, tests, docs, and governance gates.
- Cross-repo prerequisite work in `Hexalith.Commons`, `Hexalith.EventStore`, `Hexalith.FrontComposer`, and `Hexalith.Memories`.
- Possible project inventory changes: remove `Hexalith.Folders.ServiceDefaults`; optionally add `Hexalith.Folders.Providers` if transport adapters are split from the core domain.
- Wire-contract changes should be avoided. Phases A-F in the audit are intended to preserve REST/OpenAPI/SDK/CLI/MCP behavior.
- Full verification remains per-project tests, not solution-level `dotnet test`, plus targeted governance/contract/format gates.

## 3. Recommended Approach

Recommended path: direct adjustment through a new non-product Epic 11, with platform-first prerequisites and no rollback of completed MVP stories.

Alternative considered: rollback completed implementation to rebuild on the platform primitives. This is not recommended. The current domain model and completed evidence are valuable, and the main issue is duplicated technical plumbing. Rollback would destroy traceability without reducing cross-repo coordination.

Alternative considered: PRD MVP review. This is not required. The MVP scope remains the same. No product requirements need removal or reduction.

Effort estimate: high.

Risk level: medium-high, due to cross-repo dependencies and pinned governance gates.

Timeline impact: one new technical epic with 13 backlog stories. Several stories can run in parallel after the platform prerequisite slices land, but platform API work and submodule pins are gating dependencies for the domain adoption stories.

Recommended sequence:

1. Baseline and governance map.
2. Platform gap stories in Commons/EventStore/FrontComposer/Memories.
3. In-repo wire-preserving dedup and hygiene.
4. Domain adoption of platform primitives.
5. Server/Workers/UI/Client alignment.
6. Cleanup, docs, ADRs, and final verification.

## 4. Detailed Change Proposals

### PRD Changes

Section: `Product Scope > Growth Features (Post-MVP)`

OLD:

```markdown
Post-MVP growth should expand reliability and operational depth after the core workflow is proven. Candidate growth features include repair commands, deeper drift detection, richer provider contract tests, brownfield folder or repository adoption, large-file policy enforcement, advanced provider capability recipes, and broader operations workflows.
```

NEW:

```markdown
Post-MVP growth should expand reliability and operational depth after the core workflow is proven. Candidate growth features include repair commands, deeper drift detection, richer provider contract tests, brownfield folder or repository adoption, large-file policy enforcement, advanced provider capability recipes, broader operations workflows, and technical platform-alignment work that removes local copies of shared Hexalith platform capabilities without changing product semantics.
```

Rationale: The PRD should make clear that this refactor is post-MVP technical alignment, not new product scope.

Section: `API Backend Specific Requirements > Architectural Boundaries`

OLD:

```markdown
Hexalith.Tenants remains the source of truth for tenant identity, tenant lifecycle, and tenant membership. Hexalith.EventStore provides command, aggregate, event, and projection mechanics. Hexalith.Folders owns folder-specific policy, folder ACLs, provider binding references, workspace state, file-operation facts, commit metadata, and operational projections.
```

NEW:

```markdown
Hexalith.Tenants remains the source of truth for tenant identity, tenant lifecycle, and tenant membership. Hexalith.EventStore provides command, aggregate, event, projection, query, cursor, read-model, and domain-service mechanics where those mechanics are platform-owned. Hexalith.Commons, Hexalith.FrontComposer, and Hexalith.Memories provide shared boilerplate for cross-module helpers, UI shell behavior, and search-index integration where applicable. Hexalith.Folders owns folder-specific policy, folder ACLs, provider binding references, workspace state, file-operation facts, commit metadata, provider ports, and operational projections.
```

Rationale: Aligns the PRD boundary with the current Hexalith shared-platform rule.

### Epics Changes

Section: YAML front matter

OLD:

```yaml
epicCount: 10
storyCount: 102
nonProductWorkstreams:
  - workstream-7-release-governance
  - epic-8-release-closure
  - epic-9-architecture-platform-runway
phase2CapabilityEpics:
  - epic-10-worker-side-semantic-indexing
```

NEW:

```yaml
epicCount: 11
storyCount: 115
nonProductWorkstreams:
  - workstream-7-release-governance
  - epic-8-release-closure
  - epic-9-architecture-platform-runway
  - epic-11-domain-focus-platform-refactoring
phase2CapabilityEpics:
  - epic-10-worker-side-semantic-indexing
```

Rationale: Adds a non-product technical epic and preserves the existing feature/release classifications.

Section: `Epic List`

Add after Epic 10:

```markdown
### Epic 11: Domain-Focus Platform Refactoring And Governance Closure
Platform maintainers can remove local copies of shared Hexalith platform capabilities from Folders, consume the appropriate Commons/EventStore/FrontComposer/Memories primitives, delete the local ServiceDefaults project, and preserve all REST/SDK/CLI/MCP/UI behavior through lockstep governance and verification gates.
**FRs covered:** No new product FR scope. Supports existing PRD NFRs for tenant isolation, metadata-only audit, parity, observability, accessibility, traceability, and maintainability.
**Created:** 2026-07-07 via bmad-correct-course (`sprint-change-proposal-2026-07-07-081620.md`). Technical alignment epic driven by `fable_Folders_changes.md`.
```

Add new Epic 11 stories:

```markdown
### Story 11.1: Establish refactor baseline and governance pin map
As a maintainer,
I want the current build, test, package, route, and governance-pin baseline captured before refactoring,
So that every simplification can be verified against known behavior and pinned gates.

**Acceptance Criteria:**

**Given** HEAD `533806b` and the current sprint status
**When** baseline verification runs
**Then** restore/build, focused test lanes, format checks, ScaffoldContractTests, release/package inventories, route tables, workflow pins, and known DCP/AppHost blockers are recorded before edits
**And** unrelated submodule pointer changes are not reverted or hidden.

### Story 11.2: Land platform prerequisite APIs in shared modules
As a platform maintainer,
I want shared primitives created or confirmed in Commons, EventStore, FrontComposer, and Memories,
So that Folders can delete local copies instead of moving duplication around.

**Acceptance Criteria:**

**Given** the audit platform gaps G1-G9
**When** upstream stories land and Folders pins the resulting submodule SHAs
**Then** Memories exposes index-event publish/read wrappers, EventStore exposes secret-store/auth/cursor/read-model/test seams, Commons exposes secret/hash/URL/correlation helpers and ServiceDefaults consumption, and FrontComposer exposes missing UI helpers
**And** every submodule bump uses a conventional `chore(deps):` commit message.

### Story 11.3: Apply wire-preserving repo hygiene and fragile-gate fixes
As a maintainer,
I want low-risk hygiene and brittle test pins corrected first,
So that later refactors do not fail unrelated governance checks.

**Acceptance Criteria:**

**Given** tracked cache files, temporary diffs, stale E2E docs, and fragile text-based tests exist
**When** hygiene fixes are applied
**Then** tracked `.lscache` files and root litter are removed, stale READMEs are corrected, fragile auth/package/process pins are made behavioral or flexible, and Aspire version text is aligned to authoritative package pins
**And** no REST/OpenAPI behavior changes.

### Story 11.4: Consolidate Server transport, envelope, and route helper duplication
As a maintainer,
I want the hand-written REST surface deduplicated without changing wire contracts,
So that route implementation remains maintainable and parity gates stay green.

**Acceptance Criteria:**

**Given** repeated `SafeProblem`, header/query readers, canonical-id validators, and result mappers exist across Server endpoint files
**When** shared Server helpers and table-driven status mapping are introduced
**Then** existing routes, response envelopes, ProblemDetails categories, status codes, and parity oracle expectations remain unchanged
**And** the OpsConsole secret-filter drift is closed by one shared detector.

### Story 11.5: Consolidate domain/provider duplication and fix provider correctness defects
As a domain maintainer,
I want duplicated domain/provider helper logic centralized before platform adoption,
So that later package-boundary moves are smaller and safer.

**Acceptance Criteria:**

**Given** repeated payload tenant mapping, authorization mapping, provider adapter code, deterministic hashing, and stream-name checks exist
**When** shared Folders-local helpers are introduced
**Then** provider behavior, failure categories, metadata-only guarantees, and tests remain equivalent
**And** Forgejo transport uses `IHttpClientFactory`, ContextSearch avoids double serialization/N+1 where applicable, and reserved-tenant semantics are decided explicitly.

### Story 11.6: Consolidate CLI/MCP adapter core and secure bearer transport
As an adapter maintainer,
I want CLI and MCP shared plumbing deduplicated and bearer handlers hardened,
So that cross-surface parity remains consistent without copied code.

**Acceptance Criteria:**

**Given** CLI and MCP repeat JSON metadata, bearer handling, sourcing, parse, and pipeline logic
**When** shared adapter-core helpers are introduced
**Then** CLI/MCP behavior remains parity-oracle equivalent
**And** bearer-token handling rejects non-HTTPS non-loopback endpoints before token emission.

### Story 11.7: Consolidate test helpers into Hexalith.Folders.Testing
As a test maintainer,
I want duplicated fakes and fixed clocks moved into the testing library,
So that later refactors change production seams once and tests stay focused.

**Acceptance Criteria:**

**Given** duplicated `FixedTimeProvider`, static tenant/claim context accessors, gateway clients, recording providers, repository helpers, and root walkers exist
**When** canonical helpers are added to `Hexalith.Folders.Testing`
**Then** test projects consume those helpers, platform fakes are adopted where available, and test counts only change when a deleted local implementation no longer needs local re-testing.

### Story 11.8: Adopt Commons/EventStore primitives in the Folders domain
As a domain maintainer,
I want the domain library to consume shared platform primitives for platform-owned behavior,
So that Folders contains only folder-specific policy, aggregates, provider ports, and projections.

**Acceptance Criteria:**

**Given** platform prerequisites from Story 11.2 are pinned
**When** Folders adopts Commons/EventStore primitives
**Then** TenantAccess, telemetry, bounded metrics, cursor codecs, read-model stores, correlation sanitization, secret detection, deterministic hashing, authorized URL validation, and secret-store access move to shared abstractions
**And** `Dapr.Client` and `Octokit` are removed from the core domain package unless an explicit, documented package-boundary exception remains.

### Story 11.9: Delete Hexalith.Folders.ServiceDefaults and consume Commons.ServiceDefaults
As a hosting maintainer,
I want Folders to use shared service defaults,
So that local hosting, health probes, telemetry registration, and deployment docs match the platform.

**Acceptance Criteria:**

**Given** `Hexalith.Folders.ServiceDefaults` duplicates shared platform behavior
**When** the project is removed
**Then** Server/UI/Workers consume `Hexalith.Commons.ServiceDefaults`, Folders-specific readiness checks are moved into the owning host or deleted, probe paths are updated in code/docs/tests/deploy manifests, and all inventory gates are updated in lockstep.

### Story 11.10: Align Server and Workers with EventStore/Memories SDK seams
As a platform maintainer,
I want Server and Workers to consume the platform domain-service and publication seams,
So that Folders stops reimplementing request routing, subscription mapping, and Memories egress plumbing.

**Acceptance Criteria:**

**Given** EventStore and Memories expose the required seams
**When** Server/Workers are refactored
**Then** authorization moves into `IDomainServiceAdmissionStage` or equivalent, `FoldersDomainServiceRequestHandler` is deleted where safe, `MapEventStoreDomainEvents` replaces local mapping, and Memories publication/search wrappers are shared
**And** REST parity and worker semantic-indexing behavior remain unchanged.

### Story 11.11: Harden FrontComposer and Fluent UI conformance below the shell
As a UI maintainer,
I want the operations console to reuse Shell/Fluent primitives consistently,
So that Epic 6 satisfies FrontComposer governance in implementation, not only layout.

**Acceptance Criteria:**

**Given** current UI pages/components contain local user context, token relay, icons, raw tables/controls, undefined `fc-*` classes, and custom loading/copy/banner components
**When** UI conformance hardening lands
**Then** Shell user-context/token/OIDC/test helpers are used, icons/components are upstreamed or consumed from FrontComposer, tables use FluentDataGrid, raw interactive controls are removed, accordions are used for multi-section pages, and no mutation/file-content boundary is weakened.

### Story 11.12: Modernize the generated client and shared idempotency/ULID helpers
As a client maintainer,
I want the SDK generation pipeline aligned with the ecosystem's System.Text.Json and Commons helper direction,
So that packable client dependencies and idempotency behavior are stable.

**Acceptance Criteria:**

**Given** NSwag currently generates a Newtonsoft-based client and local idempotency/ULID helpers exist
**When** the client is regenerated on System.Text.Json
**Then** idempotency hash regression vectors pass, ProblemDetails parsing remains canonical, Commons helpers replace local ULID/hash logic where available, Newtonsoft leaves the packable surface, and generated files remain build-generated rather than hand-edited.

### Story 11.13: Final cleanup, ADRs, documentation, and verification
As a release reviewer,
I want the refactor closed with traceable docs and gates,
So that downstream agents can trust the new module boundary.

**Acceptance Criteria:**

**Given** Stories 11.1-11.12 are complete
**When** final cleanup runs
**Then** obsolete local code/tests are deleted or re-pointed, PRD/epics/architecture/UX/project-context are synchronized, ADRs record AppHost/Aspire exception, ServiceDefaults deletion, query-handler conformance decision, and reserved-tenant decision, and the final verification checklist from `fable_Folders_changes.md` is satisfied or explicitly blocked with evidence.
```

### Architecture Changes

Section: `Recommended Project Layout`

OLD:

```markdown
│   ├── Hexalith.Folders.ServiceDefaults    <- shared service-defaults extensions (REFERENCE: Tenants.ServiceDefaults)
```

NEW:

```markdown
│   ├── Hexalith.Folders.AppHost            <- .NET Aspire AppHost used for local/test topology; sanctioned module-test exception
│   ├── Hexalith.Folders.Aspire             <- thin Folders-specific Aspire constants/helpers only; no duplicated platform Dapr topology
│   └── Hexalith.Folders.Testing            <- in-memory fakes + builders + conformance assertions
```

Rationale: Delete the local ServiceDefaults project and record the AppHost/Aspire exception explicitly.

Section: `Component Boundaries`

OLD:

```markdown
- **`Hexalith.Folders`** owns the domain model (aggregates + state + projections + provider adapters + authorization + idempotency + caching + redaction). References Contracts only.
```

NEW:

```markdown
- **`Hexalith.Folders`** owns the domain model (aggregates + state + folder-specific projections + provider ports + folder authorization policy + idempotency semantics). It may reference narrow shared platform packages for platform-owned behavior such as TenantAccess, query cursors, read-model stores, telemetry abstractions, unique IDs, redaction helpers, and secret-store abstractions. It must not host duplicated platform boilerplate or take infrastructure package dependencies directly unless an ADR records the exception.
```

Rationale: Enables platform reuse without moving Folders-specific domain policy out of the domain.

Section: `Architecture Exit Criteria / Areas for Future Enhancement`

Add:

```markdown
**Domain-focus refactoring closure:** Before post-MVP platform alignment is considered complete, Folders must have no local ServiceDefaults project, no local copies of shared TenantAccess/cursor/read-model/telemetry/secret/correlation helpers where shared platform APIs exist, no hand-rolled Dapr subscription mapping where EventStore SDK mapping exists, and no UI shell/auth/token duplication where FrontComposer supplies a primitive.
```

Rationale: Makes the audit's final verification criteria part of architecture governance.

### UX Changes

Section: `Component Implementation Strategy`

OLD:

```markdown
Build custom components on top of Fluent UI primitives and FrontComposer conventions. Do not introduce a separate component library.
```

NEW:

```markdown
Build custom components on top of Fluent UI primitives and FrontComposer conventions. Do not introduce a separate component library. Reuse FrontComposer Shell services and components for user context, token relay, OIDC bootstrapping, loading states, safe copy, banners, and icons where a shared primitive exists. Use FluentDataGrid and Fluent input/button components for production pages; raw interactive HTML controls and undefined styling hooks are implementation debt, not acceptable MVP patterns.
```

Rationale: Converts the audit's UI drift findings into explicit UX implementation guidance.

### Sprint Status Changes

Section: `development_status`

Add after Epic 10:

```yaml
  # Epic 11: Domain-Focus Platform Refactoring And Governance Closure
  # Created 2026-07-07 via bmad-correct-course from fable_Folders_changes.md.
  # Non-product technical alignment epic: removes local platform copies, consumes shared
  # Commons/EventStore/FrontComposer/Memories primitives, deletes ServiceDefaults, and
  # preserves existing wire behavior through governance gates.
  epic-11: backlog
  11-1-establish-refactor-baseline-and-governance-pin-map: backlog
  11-2-land-platform-prerequisite-apis-in-shared-modules: backlog
  11-3-apply-wire-preserving-repo-hygiene-and-fragile-gate-fixes: backlog
  11-4-consolidate-server-transport-envelope-and-route-helper-duplication: backlog
  11-5-consolidate-domain-provider-duplication-and-fix-provider-correctness-defects: backlog
  11-6-consolidate-cli-mcp-adapter-core-and-secure-bearer-transport: backlog
  11-7-consolidate-test-helpers-into-hexalith-folders-testing: backlog
  11-8-adopt-commons-eventstore-primitives-in-the-folders-domain: backlog
  11-9-delete-service-defaults-and-consume-commons-service-defaults: backlog
  11-10-align-server-and-workers-with-eventstore-memories-sdk-seams: backlog
  11-11-harden-frontcomposer-and-fluent-ui-conformance-below-the-shell: backlog
  11-12-modernize-generated-client-and-shared-idempotency-ulid-helpers: backlog
  11-13-final-cleanup-adrs-documentation-and-verification: backlog
  epic-11-retrospective: optional
```

Rationale: Adds backlog entries without mutating historical done state.

## 5. Change Navigation Checklist Results

- 1.1 Triggering story: [x] No single story triggered this; the trigger is the 2026-07-07 architecture audit after Epics 1-10 were completed.
- 1.2 Core problem: [x] Failed/aging implementation approach requiring a platform-aligned solution; local platform copies and infra coupling conflict with current Hexalith module boundaries.
- 1.3 Supporting evidence: [x] Evidence captured in `fable_Folders_changes.md` and verified locally for domain csproj refs, ServiceDefaults presence, telemetry, cursors, Dapr client construction, Octokit/Dapr package refs, and related test pins.
- 2.1 Current epic impact: [x] Existing Epics 1-10 stay historically done; add Epic 11.
- 2.2 Epic-level changes: [x] Add non-product Epic 11 with 13 stories.
- 2.3 Remaining epics: [x] No future feature epics remain; open action items under Epics 8-10 should remain but may be consumed by Epic 11 where relevant.
- 2.4 Obsolete/new epics: [x] No planned epic is obsolete; new Epic 11 is needed.
- 2.5 Priority/order: [x] Platform prerequisite stories before domain adoption; wire-preserving dedup before package-boundary moves.
- 3.1 PRD conflicts: [x] No product conflict; add platform-alignment guardrail.
- 3.2 Architecture conflicts: [x] Update ServiceDefaults, component boundaries, AppHost/Aspire exception, platform primitive adoption.
- 3.3 UX conflicts: [x] Update FrontComposer/Fluent implementation guidance.
- 3.4 Other artifacts: [x] Sprint status, implementation story files, docs, ADRs, CI gates, release-package inventories, ScaffoldContractTests, deploy manifests, and project context are impacted.
- 4.1 Direct Adjustment: [x] Viable; high effort, medium-high risk.
- 4.2 Potential Rollback: [N/A] Not recommended; no completed story rollback required.
- 4.3 PRD MVP Review: [N/A] Product MVP unchanged.
- 4.4 Recommended path: [x] Direct Adjustment through Epic 11 with platform-first prerequisites.
- 5.1 Issue summary: [x] Included.
- 5.2 Impact and artifact needs: [x] Included.
- 5.3 Recommended path and rationale: [x] Included.
- 5.4 PRD MVP impact/action plan: [x] MVP unchanged; new technical epic proposed.
- 5.5 Handoff plan: [x] Included below.
- 6.1 Checklist completion: [x] Complete except explicit user approval.
- 6.2 Proposal accuracy: [x] Drafted from loaded PRD, epics, architecture, UX, project context, sprint status, and change file.
- 6.3 User approval: [!] Pending.
- 6.4 Sprint-status update: [!] Pending approval; do not modify before approval.
- 6.5 Handoff confirmation: [!] Pending approval.

## 6. Implementation Handoff

Scope classification: Moderate. The product scope and MVP requirements do not change, but backlog organization, architecture artifacts, platform submodules, and many implementation surfaces need coordinated updates.

Recommended handoff:

- Architect: owns architecture updates, platform gap slicing, AppHost/Aspire exception ADR, ServiceDefaults deletion ADR, query-handler conformance decision, reserved-tenant decision.
- Product Owner / PM: approves adding Epic 11 and confirms it is non-product technical alignment rather than new feature scope.
- Developer agent: creates Epic 11 story files in sequence, starting with Story 11.1, and executes implementation after approval.
- Platform maintainers: land prerequisite APIs in Commons/EventStore/FrontComposer/Memories and provide submodule pins.
- Test Architect / QA: updates gate inventories and validates the final verification checklist.

Success criteria:

- Epic 11 exists in `epics.md` and `sprint-status.yaml` as backlog work.
- Architecture and UX artifacts reflect the updated platform-boundary rules.
- No existing completed epic/story status is rewritten.
- All platform prerequisite gaps are either landed and pinned or recorded as explicit blockers.
- Wire behavior remains stable until a separately approved contract-changing epic exists.
- Final verification shows no remaining local copies of platform-owned behavior where shared APIs exist, or an ADR records a deliberate exception.

## 7. Approval Status

This proposal is approved.

Requested decision:

- Approved 2026-07-07 by Jerome: add Epic 11 and update PRD/epics/architecture/UX/sprint status in lockstep.
- Revise: adjust story count, sequencing, or scope classification before artifact edits.
- Reject: keep the audit as a report only and do not alter sprint planning artifacts.

Final routing:

- Scope: Moderate.
- Routed to: Product Owner / Developer agents, with Architect, Platform maintainers, and Test Architect / QA participation per the handoff plan.
- Handoff deliverables: approved Sprint Change Proposal, backlog reorganization plan, updated PRD/epics/architecture/UX guidance, and `sprint-status.yaml` backlog entries for Epic 11.
