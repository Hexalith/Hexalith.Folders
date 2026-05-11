---
stepsCompleted:
  - 1
  - 2
  - 3
  - 4
  - 5
  - 6
  - 7
  - 8
inputDocuments:
  - "_bmad-output/planning-artifacts/prd.md"
  - "_bmad-output/planning-artifacts/prd-validation-report.md"
  - "_bmad-output/planning-artifacts/product-brief-Hexalith.Folders.md"
  - "_bmad-output/planning-artifacts/research/technical-forgejo-and-github-api-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-eventstore-domain-aggregates-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-tenants-integration-for-folder-management-application-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-memories-semantic-indexing-rag-research-2026-05-11.md"
  - "_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md"
documentCounts:
  prd: 1
  prdValidation: 1
  productBriefs: 1
  uxDesign: 0
  research: 5
  projectDocs: 0
  projectContext: 0
workflowType: 'architecture'
project_name: 'Hexalith.Folders'
user_name: 'Jerome'
date: '2026-05-07'
status: 'complete'
lastStep: 8
completedAt: '2026-05-11'
resumedAt: '2026-05-11'
completionRefreshedAt: '2026-05-11'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

The PRD defines 57 functional requirements across 11 capability blocks: Capability Contract Terms (FR1–FR3), Authorization & Tenant Boundary (FR4–FR10), Folder Lifecycle (FR11–FR14), Provider Readiness & Repository Binding (FR15–FR23), Workspace & Lock Lifecycle (FR24–FR31), File Operations & Context Queries (FR32–FR36), Commit/Evidence/Idempotency (FR37–FR42), Error/Status/Diagnostics (FR43–FR46), Cross-Surface Contract (FR47–FR51), and Audit & Operations Visibility (FR52–FR57). The capability density centers on three things architecture must encode consistently: (1) one canonical workspace task lifecycle (provider-readiness → folder/repository binding → workspace prepare → task lock → file add/change/remove → commit → query → audit), (2) one set of error/status semantics shared across REST, CLI, MCP, and SDK surfaces, and (3) one tenant authorization decision evaluated before any file/workspace/credential/repository/lock/commit/provider/audit access.

**Non-Functional Requirements:**

Nine NFR categories drive architecture: Security and Tenant Isolation (zero-tolerance cross-tenant leakage; metadata-only events/logs/traces; least-privilege provider credentials), Reliability/Idempotency/Failure Visibility (idempotency keys for prepare/lock/file mutation/commit/cleanup; deterministic single-active-writer locks; inspectable failure state, no silent repair), Performance and Query Bounds (1s p95 command-ack, 500ms p95 status/audit, 2s p95 context queries; bounded input enforcement), Scalability and Capacity (no single-tenant/single-repository/single-workspace assumptions), Integration and Contract Compatibility (versioned contracts, REST/CLI/MCP/SDK parity, provider contract tests for GitHub and Forgejo), Observability/Auditability/Replay (status-freshness target, read-model determinism, metadata-only audit with classified sensitive metadata), Data Retention and Cleanup (per-data-class retention, no audit erasure, tenant-deletion semantics), Operations Console Accessibility (WCAG 2.2 AA, keyboard navigation, no color-only indicators), and Verification Expectations (each NFR category has at least one automated or release-validation path).

**Scale & Complexity:**

- Primary domain: API/backend service module (developer infrastructure / AI workspace storage)
- Project context: Greenfield product built on top of mature Hexalith ecosystem modules (Hexalith.Tenants, Hexalith.EventStore, Hexalith.FrontComposer)
- Complexity level: **High**, driven by multi-tenant authorization, event-sourced aggregate mechanics, dual-provider portability (GitHub + Forgejo), workspace state machine with task-scoped locking and idempotency, four-surface canonical contract (REST/CLI/MCP/SDK), metadata-only audit with secret/content-exclusion guarantees, and read-only operations console.
- Estimated architectural components: ~12 core (canonical REST API, OpenAPI v1 contract, CLI adapter, MCP server adapter, SDK, read-only operations console, Folder domain service host, Organization aggregate, Folder aggregate, provider ports + GitHub/Forgejo adapters, projection/read-model layer, workspace/Git workers/process managers) plus shared concerns (tenant authorization service, idempotency/correlation infrastructure, audit metadata pipeline, observability).
- Surface topology to verify in architecture: **the natural shape is SDK as the typed canonical client, with CLI and MCP implemented as adapters over the SDK, and REST as a parallel transport with the same behavioral spec**. This collapses the *transport* parity surface from C(4,2)=6 pairs to one (SDK-vs-REST). **It does NOT collapse behavioral parity:** pre-SDK errors (credential sourcing, config validation), post-SDK error projection (exception → CLI exit code, exception → MCP tool failure), and side-channel parameter sourcing (Idempotency-Key default, CorrelationId default, TaskId default) remain per-adapter parity dimensions and require an explicit Adapter Parity Contract (see §"Adapter Parity Contract" under API & Communication Patterns). The PRD's "thin adapter" language is consistent with the transport collapse; architecture must make both the collapse and its limits explicit.
- Quantitative and structural targets to resolve: see **Architecture Exit Criteria** at the end of this section. The PRD-deferred targets (C1–C5) are joined by analysis-added targets (C0, C6–C13) raised through pre-mortem, first-principles, Socratic, red-team, and multi-persona review.

### Technical Constraints & Dependencies

**Ecosystem-imposed (non-negotiable):**

- .NET 10 / C# stack (matches Hexalith.Tenants and Hexalith.EventStore baselines)
- Hexalith.EventStore as command/query/event/projection mechanics (no parallel write-side framework)
- Hexalith.Tenants as the source of truth for tenant identity, lifecycle, and membership; consumed via Dapr pub/sub on `system.tenants.events` with a local fail-closed tenant-access projection
- Dapr sidecars for pub/sub, state store, service invocation, actors, access control, resiliency
- .NET Aspire AppHost for local topology orchestration
- Aggregate identity scheme `{tenant}:{domain}:{aggregateId}`; managed-tenant-scoped folders MUST NOT use the `system` tenant (reserved for tenant management); aggregate IDs are opaque immutable identifiers (GUID/ULID) — folder hierarchy and human-readable paths are projected metadata, not identity
- OpenTelemetry observability for traces/metrics/logs
- xUnit v3, Shouldly, NSubstitute, Testcontainers for testing
- **Single source-of-truth artifact for cross-surface generation** must be named and frozen as the Contract Spine (see C0); independent regeneration of any surface from a different source is forbidden

**Provider boundaries (explicit, capability-tested):**

- GitHub and Forgejo are NOT API-compatible base-URL swaps; capability differences must be modeled through provider ports and validated through provider contract tests before a provider is marked ready
- Provider port surfaces only credential references and capability metadata; provider-specific permission scoping (GitHub Apps fine-grained permissions, Forgejo scoped tokens) lives inside the adapter and is not flattened to a lowest common denominator
- Capability-discovery model must accommodate N providers, not be hardcoded for 2; adding a third provider must not require port redesign
- Provider version pinning + runtime capability-drift detection (see C12); each provider has a documented upgrade ritual
- Webhooks are at-least-once event notifications; receivers must validate signatures, persist delivery IDs, deduplicate, and reconcile via API rather than mutate state directly

**Hard product boundaries (forbidden content/behavior):**

- No file contents, diffs, generated context payloads, provider tokens, credential material, secrets, or unauthorized resource existence in events, logs, traces, metrics, projections, audit records, console responses, provider diagnostics, or error messages
- No mutation paths, credential reveal, file-content browsing, file-editing UI, raw diff display, hidden repair actions, or unrestricted filesystem browsing in the MVP read-only operations console
- No silent repair, discard, or commit when a workspace is interrupted; the MVP must leave inspectable terminal/intermediate state and prevent overwrites
- No secret material storage in Hexalith.Folders; only credential references may appear where authorized
- No webhook ingestion in MVP — explicit architectural posture, not absence-by-omission; when webhooks are introduced post-MVP, tenant-routing model must be designed first

### Cross-Cutting Concerns Identified

These concerns recur across multiple components and must be designed once, not per surface:

1. **Tenant isolation enforcement** — every command/query/event/projection/lock key/cache key/temporary path/provider callback/audit record MUST be tenant-scoped; cross-tenant access denied before any resource access; safe error shapes that prevent unauthorized resource enumeration.
2. **Layered authorization** — JWT validation → authoritative tenant claim → local tenant-access projection (with fail-closed-on-stale policy) → folder ACL → EventStore validators → production Dapr deny-by-default policies + mTLS.
3. **Idempotency and correlation** — required idempotency keys for workspace preparation, lock acquisition, file mutation, commit, and cleanup; correlation IDs propagated through every surface, event, projection, log, and audit record; replay returns the same logical result; conflicting payload returns idempotency conflict; payload-equivalence rule per command type (canonical hash, excluded fields) must be specified.
4. **Workspace state machine** — six canonical product-visible states (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`) plus lifecycle/failure states (`requested`, `preparing`, `changes_staged`, `unknown_provider_outcome`, `reconciliation_required`); single-active-writer lock per tenant/folder/workspace scope; deterministic conflict response across surfaces; **a total state-transition matrix where every (state, event) pair has a defined outcome including reconciliation paths** is part of the architecture, not just the state set; **each state carries a paired operator-disposition label** (`auto-recovering`, `awaiting-human`, `terminal-until-intervention`, `degraded-but-serving`) that surfaces as the primary visual in the operations console while the technical state name remains secondary metadata — this is a UX requirement driven by incident-response cognition, not engineering vocabulary preference.
5. **Provider failure taxonomy** — distinguish known failure (timeout / 401 / 403 / 404 / 409 / 429 / 5xx / branch-protection / missing-or-deleted repository / stale clone / credential revocation / drift) from unknown outcome; unknown outcome enters `reconciliation_required` rather than retrying in a way that could duplicate repositories, file changes, or commits; **provider contract suite runs in two execution modes**: hermetic-PR-gate (pinned fixtures, fast) and live-nightly-drift (against real GitHub/Forgejo, feeds reconciliation_required on detected drift); fixture-to-failure-mode coverage matrix asserted in CI.
6. **Metadata-only audit + redaction** — automated sanitizer tests with sentinel secrets and file-content markers across logs/traces/metrics labels/events/audit records/console views/provider diagnostics/error responses; **a normative `tests/fixtures/audit-leakage-corpus.json` is checked in** (paths containing secret-shaped strings, branch names with token-shaped values, commit messages with email/credential patterns, etc.); all sentinel tests iterate this corpus; new categories require corpus PR + reviewer sign-off to prevent per-developer drift.
7. **Path security** — workspace-root confinement, canonicalization, traversal rejection, symlink policy, binary/large-file policy, encoding/Unicode normalization, reserved-name handling, case-collision handling.
8. **Cross-surface contract parity** — one canonical workflow contract; SDK is the typed canonical client, CLI and MCP wrap the SDK, REST is the parallel transport with the same behavioral spec. **Parity dimensions catalog** (tested as invariants): authorization decisions, error categories, idempotency behavior, audit metadata fields, correlation propagation, lifecycle states. Latency is per-surface and is NOT a parity dimension. Parity verification is operationalized through C13 (parity oracle).
9. **Read-model determinism** — rebuilding views from an empty read model produces equivalent state from the same ordered event stream; **determinism scope explicitly excludes fields derived from external clocks** (stale-lock detection, freshness indicators); projection compaction strategy must preserve the determinism guarantee.
10. **Performance budgets** — 1s p95 command-ack, 500ms p95 status/audit summary, 2s p95 context query, bounded MVP input enforcement; provider/workspace operations may be asynchronous when external latency exceeds interactive budgets.
11. **Operations console boundary** — read-only, read-model–based, WCAG 2.2 AA, keyboard navigation, status indicators not relying on color alone, no mutation/repair paths in MVP; **separate console performance budget** from end-user budgets; **incident-mode last-resort read path** to authoritative event streams (still ACL-checked) for cases where projections themselves are the broken thing; **redacted fields are visually distinguished from unknown/missing fields** — a redacted value (per C9 sensitive-metadata policy) renders with a visible affordance such as a lock icon plus "your tenant policy hides this; contact your administrator", never as silent truncation, because silent redaction during an incident reads as a system bug and operators lose time chasing ghosts.
12. **Tenant context provenance invariant** — authoritative tenant comes from the request authentication context + EventStore envelope; NEVER from request payload. Tested as a parity invariant on all four surfaces (REST, CLI, MCP, SDK). A tenant identifier in any payload is treated as input requiring validation against context, not as authority.
13. **Cache-key tenant prefix invariant** — every cache key (in-process, Dapr state, Redis, distributed cache) MUST carry tenant prefix; build-time/CI lint check enforces it as a hard gate (see C10).
14. **Aggregate ID opacity** — aggregate IDs are opaque immutable identifiers (GUID/ULID); folder hierarchy and human-readable paths/names are projected metadata, never identity. Folder moves and renames preserve the ID and the event stream.
15. **Correlation propagation invariant** — correlation IDs and task IDs propagate end-to-end through every adapter chain (MCP → SDK → REST → EventStore → projection → audit); parity tests verify the chain across surfaces, not just per-surface.
16. **Mid-task authorization revocation** — held locks revalidate authorization on a defined freshness budget; revocation policy is part of the lock model, not a separate concern. A mid-task tenant-access revocation must visibly impact the held lock within the freshness budget. Concrete numbers fixed in C7.
17. **Sensitive metadata classification** — paths, branch names, repository names, and commit messages are *classified* metadata with per-tenant policy (hash/truncate/redact/expose); a default sensitivity tier is required; classification applies uniformly across audit, projections, and console responses; the redaction-vs-unknown UX rule in concern #11 is a downstream requirement of this classification.
18. **Context-query authorization order** — tenant + folder ACL + path policy MUST run before search/glob/partial-read execution; results respect include/exclude rules, binary/large-file policy, range/result limits; sentinel-secret tests cover search and glob results, not just file reads. Denied context queries produce metadata-only audit evidence with policy reason.
19. **Read consistency model** — explicit per-query-family choice: snapshot-per-task (tied to task ID, stable through lifecycle), read-your-writes (after a mutation by the same correlation), or eventually-consistent (read-only browsing). Documented per query family and exposed as a freshness header where relevant.
20. **Tenants-availability degraded mode** — local tenant-access projection allows read paths to continue under bounded staleness when Hexalith.Tenants is unavailable; mutations require fresh authorization (synchronous Tenants query or rejection). Degraded-mode SLOs documented; not a silent fallback.
21. **Operational state durability classification** — every operational state has an explicit decision: rebuilt from events on restart (stateless cache), acceptably lost on sidecar restart (in-memory ephemeral), or requires durable storage with recovery contract (locks, idempotency records, in-flight reconciliation tasks). Implementation choices must respect the classification.
22. **Architecture exit criteria** — quantitative and structural targets C0–C13 (below) MUST be set, validated, and recorded in the architecture document with measurement methods before MVP release.

### Additional Technical Research: Memories and FrontComposer

Two later technical research reports have been added as architecture inputs:

- `_bmad-output/planning-artifacts/research/technical-hexalith-memories-semantic-indexing-rag-research-2026-05-11.md`
- `_bmad-output/planning-artifacts/research/technical-frontcomposer-integration-for-hexalith-folders-ui-research-2026-05-11.md`

**Hexalith.Memories integration implications:**

- Treat `Hexalith.Memories` as a separate Dapr-enabled service and derived semantic index, not as an authoritative Folders datastore.
- Keep Folders events, projections, logs, traces, metrics, audit records, and error responses metadata-only. File content may be read by workers after authorization and sent to Memories for indexing; it must not be embedded in Folders events.
- Add a worker-side semantic-indexing port before referencing Memories directly. `Hexalith.Folders.Workers` is the only initial project that may depend on `Hexalith.Memories.Client.Rest` / `Hexalith.Memories.Contracts`; Contracts, core domain, CLI, MCP, UI, and Server must not take a direct Memories dependency.
- Use durable Folders events as indexing triggers and a Folders-owned bridge projection to track `file version -> Memories workflow/memory unit/status`. This projection answers whether a file version is indexed, stale, skipped, failed, tombstoned, or reconciliation-required.
- Authorize before indexing and before retrieval: tenant access, folder ACL, path policy, sensitivity classification, size/type limits, then Memories call. Search results must be security-trimmed and redacted by Folders policy before leaving the Folders API/SDK/MCP boundary.
- Use stable source URIs and idempotency keys for memory units, based on tenant/folder/file-version/content-hash metadata. Avoid raw path metadata unless C9 explicitly allows exposure.
- Start with asynchronous indexing after file-write/commit events; a Memories outage must not roll back a durable Folders file operation. It should surface as retryable indexing status and operational evidence.
- Large-file behavior remains coupled to C4. The Memories research identifies the current inline ingestion guardrail as a constraint; Folders should first expose explicit skipped/too-large status, then add chunked or reference-based ingestion only after limits are agreed.

**Hexalith.FrontComposer integration implications:**

- `Hexalith.Folders.UI` should become a Blazor Web App host rendering `FrontComposerShell` as the primary layout, using Interactive Server first.
- The MVP operations console remains read-only and projection-backed. Do not generate or route mutation command forms through FrontComposer during MVP.
- Add Folders-specific FrontComposer projection models for metadata-only views such as folder summaries, workspace status, provider readiness, tenant folder access, semantic-indexing status, and operation/audit timelines.
- Replace the fail-closed `IUserContextAccessor` with a Folders/Tenants auth bridge before enabling tenant-scoped FrontComposer queries.
- Defer `AddHexalithEventStore` until Folders Server implements the compatible command/query/projection-change endpoints, or provide a Folders-specific read-only `IQueryService` adapter backed by `Hexalith.Folders.Client`.
- Keep FrontComposer SourceTools generated output deterministic and unedited. If UI-only projection annotations would pollute the Contract Spine, place FrontComposer projection DTOs in a UI/domain companion assembly instead of `Hexalith.Folders.Contracts`.
- Aspire topology should keep `folders-ui` and `folders` as separate services with explicit references; UI must not require provider credentials, tenant seed data, Keycloak, Dapr sidecars, or nested submodule initialization for scaffold/build tests.

**Architecture impact summary:**

These reports do not overturn the core architecture. They add two optional integration tracks that must preserve the existing invariants:

- Memories extends context-query and RAG capability through worker-owned asynchronous indexing and an authorized Folders query facade.
- FrontComposer implements the read-only operations console shell and generated projection UI while Folders retains domain semantics, authorization, data access, and no-leakage policy.

### Architecture Exit Criteria — Targets to Resolve

| ID | Target | Source / Notes |
| --- | --- | --- |
| **C0** | **Contract Spine Decision** — name and version of the single source-of-truth artifact for cross-surface generation, including its extension vocabulary for idempotency keys, correlation, lifecycle states, parity-dimension annotations, and audit-metadata declarations. **Recommended:** OpenAPI 3.1 with extension vocabulary; SDK generated via NSwag or Kiota for .NET 10. This is a *blocking precondition* for C1–C13 because every cross-surface parity claim, generator pipeline, and CI guarantee depends on it. | Added by analysis (Winston) — promoted from "TBD" to blocking decision |
| C1 | Concurrent capacity targets (tenants, folders/tenant, active workspaces/tenant, concurrent agent tasks/tenant) | PRD §NFR Scalability — TBD |
| C2 | Status-freshness target (max acceptable lag between emitted lifecycle event and appearance in status/audit views) | PRD §NFR Observability — TBD; jointly decided with C8 |
| C3 | Retention durations per data class (audit metadata, workspace status, provider correlation IDs, read-model views, temporary working files, cleanup records) | PRD §NFR Data Retention — TBD |
| C4 | Bounded MVP input limits (max files / max bytes / max result count / max query duration per context query) | PRD §NFR Performance — TBD |
| C5 | Concrete scalability quantifiers replacing the word "multiple" in NFR Scalability | PRD §NFR Scalability — TBD |
| C6 | Total workspace state-transition matrix (every (state, event) pair → outcome, including reconciliation paths and terminal states) **plus the operator-disposition label paired to each state** (`auto-recovering` / `awaiting-human` / `terminal-until-intervention` / `degraded-but-serving`) | Added by analysis; UX-driven from Sally |
| C7 | **Two-number lock contract:** lease-renewal interval AND auth-revalidation interval, defaulted and tunable per tenant, tied to a stated SLO ("a revoked tenant access takes effect within N seconds") so the numbers trace to business value, not to engineering preference | Added by analysis; refined by Winston |
| C8 | Read consistency model per query family (snapshot-per-task / read-your-writes / eventually-consistent) | Added by analysis; jointly decided with C2 |
| C9 | Sensitive metadata classification policy + default sensitivity tier (paths, branch names, repo names, commit messages); classification feeds the console redaction-vs-unknown UX rule in cross-cutting concern #11 | Added by analysis; refined by Sally |
| C10 | Cache-key tenant-prefix lint enforcement (CI/build-time gate, naming convention, tooling) | Added by analysis |
| C11 | File-content transport model (inline JSON / base64 / streaming upload / external content reference) — **v1-OpenAPI-blocking**: must be decided before C0 artifact ships | PRD §"Architecture Decisions Needed Next"; analysis flagged severity |
| C12 | Provider version pinning + runtime capability-drift detection cadence + upgrade ritual; **provider contract suite runs in hermetic-PR-gate mode AND live-nightly-drift mode** with fixture-to-failure-mode coverage matrix asserted in CI | Added by analysis; refined by Murat |
| C13 | **Parity oracle artifact** — a `parity-contract.yaml` (or equivalent) generated from the C0 Contract Spine, declaring for each `(operation_id) →` a row with **transport-parity columns** (`auth_outcome_class`, `error_code_set`, `idempotency_key_rule`, `audit_metadata_keys`, `correlation_field_path`, `terminal_states`) **plus behavioral-parity columns** (`pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `cli_exit_code`, `mcp_failure_kind`) — see §"Adapter Parity Contract" for the per-adapter behavioral columns. **All four surface test projects consume the oracle as xUnit theory data:** `*.Sdk.Tests` + `*.Rest.Tests` for transport-parity columns; `*.Cli.Tests` + `*.Mcp.Tests` for behavioral-parity columns. The artifact is itself schema-validated against `tests/fixtures/parity-contract.schema.json` before tests consume it. **CI gates:** (a) Contract Spine adds a command without a parity-oracle row → fail; (b) Contract Spine *removes* an operation without a deprecation-window entry in `previous-spine.yaml` → fail (symmetric drift detection); (c) mutating command (POST/PUT/PATCH/DELETE) without `idempotency_key_rule` → fail (per-class completeness); (d) query operation without `read_consistency_class` → fail | Added by analysis; converged proposal from Amelia + Murat; behavioral-parity columns and symmetric-drift gate added in Step 7 elicitation |

Each exit criterion must be (a) set as a concrete value or rule with a measurement/enforcement method, (b) validated through implementation evidence before MVP release, and (c) recorded in the architecture document and referenced from the PRD via update where applicable.

### Exit Criteria Operations Plan

"Tracked exit criteria" without ownership and decision authority is a wishlist. Each criterion below has an assigned **decision owner**, **decision authority** (final sign-off), **artifact location** (where the recorded decision lives), **decision deadline** (the latest implementation phase that can start before the criterion is set), and **measurement tool/method**. **C3 and C4 are Phase-1-blocking** because they shape the Contract Spine itself (D-7 commit-TTL field; OpenAPI `maxItems`/`maxLength`/`maxBytes`/`maxResultCount`).

| ID | Decision Owner | Decision Authority | Artifact Location | Decision Deadline | Measurement Tool / Method |
| --- | --- | --- | --- | --- | --- |
| C0 | Architect | Architecture team | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | Phase 1 entry | OpenAPI 3.1 + extension vocabulary; A-3 BLOCKING server-vs-spine CI gate |
| C1 | Architect | PM | `docs/exit-criteria/c1-capacity.md` | Phase 9 entry | NBomber harness in `tests/load/`; target hardware profile pinned in artifact |
| C2 | Architect | PM | `docs/exit-criteria/c2-freshness.md` | Phase 4 exit (BLOCKS Phase 8 ops console UX) | OpenTelemetry projection-lag metric; product-target SLA pinned in artifact |
| C3 | Tech Lead | Legal + PM | `docs/exit-criteria/c3-retention.md` | **Phase 1 entry — BLOCKS Contract Spine (D-7 commit-TTL inherits)** | stakeholder workshop (legal + audit + PM); per-data-class table |
| C4 | Architect | PM | `docs/exit-criteria/c4-input-limits.md` | **Phase 1 entry — BLOCKS Contract Spine (`maxItems`/`maxLength`/`maxBytes`/`maxResultCount`)** | preliminary load probe + product judgment; values land in OpenAPI schema |
| C5 | Architect | PM | inherits C1 artifact | Phase 9 entry | inherits C1 |
| C6 | Architect | Architecture team | this document §"Workspace State Transition Matrix" | **Phase 1 entry — BLOCKS Phase 2 `FolderStateTransitions.cs`** | enumerated state × event matrix; aggregate test asserts every (state, event) has defined outcome |
| C7 | Architect | PM (SLO sign-off) | this document §"Authentication & Security" + §"Process Patterns" | Phase 4 exit | tunable defaults pinned per tenant; SLO statement in artifact |
| C8 | Architect | PM | this document §"API & Communication Patterns" + per-query-family freshness header | Phase 4 entry | per-query-family declaration table; freshness-header tests |
| C9 | Architect | Security + PM | this document §"S-6" + `Hexalith.Folders/Redaction/SensitiveMetadataClassifier.cs` | Phase 3 entry | classifier unit tests; sentinel corpus iteration |
| C10 | Architect | Architecture team | `.github/workflows/ci.yml` (lint job) + `Hexalith.Folders/Caching/TenantPrefixedCacheKey.cs` | Phase 1 entry | CI lint gate; Roslyn analyzer or grep-based |
| C11 | Architect | Architecture team | this document §"D-9" + Contract Spine (PutFileInline + PutFileStream operations) | **resolved (D-9 bimodal REST + UploadFileAsync convenience)** | OpenAPI operations + SDK convenience helper test |
| C12 | Architect | Architecture team | `tests/contracts/forgejo/supported-versions.json` + `.github/workflows/nightly-drift.yml` | Phase 5 entry | oasdiff classifier; fixture-to-failure-mode coverage matrix |
| C13 | Architect | Architecture team | `tests/fixtures/parity-contract.yaml` + `tests/fixtures/parity-contract.schema.json` | Phase 1 exit | generated from C0 spine; consumed as xUnit theory data; CI fails on missing rows |

A criterion with no entry under "Decision Authority" or "Artifact Location" is **not yet ready to ship**; CI may add a `exit-criteria-presence` gate that fails the release pipeline when an artifact link is missing.

### Workspace State Transition Matrix (C6 — Enumerated)

This subsection is the architecture deliverable referenced by C6 and by `Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs`. It enumerates the total state matrix per cross-cutting concern #4 + concern #21. Every `(currentState, event)` pair has a defined outcome. Pairs not listed are **rejected** with canonical error category `state_transition_invalid` (CLI exit 74; MCP failure kind `state_transition_invalid`); state is unchanged; an idempotency record per A-9 makes the rejection inspectable.

**State catalog (11 states; per concern #4):**

| State | Operator Disposition (F-4) | Description |
| --- | --- | --- |
| `requested` | `auto-recovering` | Repository binding submitted; provider activity not yet started |
| `preparing` | `auto-recovering` | Workspace preparation in flight (clone, materialize) |
| `ready` | available (or `degraded-but-serving` when projection lag exceeds C2) | Workspace usable; no lock; no uncommitted changes |
| `locked` | `degraded-but-serving` | Lock held by a task; no mutations yet |
| `changes_staged` | `degraded-but-serving` | Lock held; one or more file mutations applied; commit pending |
| `dirty` | `awaiting-human` | Uncommitted changes outside the active task (orphaned lock with staged mutations); operator-visible variant of `changes_staged` after lock loss |
| `committed` | `auto-recovering` | Commit succeeded; lock release and projection update in flight |
| `failed` | `terminal-until-intervention` | Known categorized failure; human or reconciler intervention required |
| `inaccessible` | `terminal-until-intervention` | Provider unreachable, repository deleted, or credentials revoked; state known |
| `unknown_provider_outcome` | `awaiting-human` | Provider call did not confirm; state genuinely unknown |
| `reconciliation_required` | `awaiting-human` | Reconciler inspecting upstream state; mutations blocked until resolved |

**Valid transitions (every `(from, event) → (to, side effect)` declared):**

| From → To | Triggering Event | Side Effect |
| --- | --- | --- |
| `(none)` → `requested` | `RepositoryBindingRequested` | Aggregate created; audit record |
| `requested` → `preparing` | `RepositoryBound` | Worker dispatches `WorkspacePrepareRequested` |
| `requested` → `failed` | `RepositoryBindingFailed` (known) | Audit; operator-disposition `terminal-until-intervention` |
| `requested` → `unknown_provider_outcome` | `ProviderOutcomeUnknown` | Reconciler scheduled (no retry per concern #5) |
| `preparing` → `ready` | `WorkspacePrepared` | `WorkspaceStatusProjection` updated |
| `preparing` → `failed` | `WorkspacePreparationFailed` (known) | Audit |
| `preparing` → `unknown_provider_outcome` | `ProviderOutcomeUnknown` | Reconciler scheduled |
| `ready` → `locked` | `WorkspaceLocked` | Lease started; auth-revalidation scheduled per C7 |
| `ready` → `inaccessible` | `AuthRevocationDetected` / `TenantRevoked` / `RepositoryDeletedAtProvider` | Lock impossible; audit |
| `ready` → `reconciliation_required` | `ReconciliationRequested` (operator-initiated) | Manual inspection path |
| `locked` → `changes_staged` | `FileMutated` (first mutation) | Mutation event persisted; lease renewed per C7 |
| `locked` → `ready` | `WorkspaceLockReleased` (clean release with no mutations) | Lease released |
| `locked` → `dirty` | `LockLeaseExpired` (no mutations applied yet) | Lock orphaned; recovery via release-or-reconciliation |
| `locked` → `inaccessible` | `AuthRevocationDetected` during lock-revalidation window | Lock revoked; audit; subsequent mutations rejected with `tenant_access_denied` |
| `changes_staged` → `changes_staged` | `FileMutated` (additional) | Mutation persisted; lease renewed |
| `changes_staged` → `committed` | `CommitSucceeded` | Commit recorded; lease release scheduled |
| `changes_staged` → `failed` | `CommitFailed` (known: branch-protection / 4xx / 5xx) | Audit; provider-specific rollback strategy |
| `changes_staged` → `unknown_provider_outcome` | `ProviderOutcomeUnknown` during commit | Reconciler scheduled (no retry) |
| `changes_staged` → `dirty` | `LockLeaseExpired` (mutations applied, lock lost) | Operator intervention required |
| `committed` → `ready` | `WorkspaceLockReleased` | Final terminal of clean task lifecycle |
| `dirty` → `reconciliation_required` | `ReconciliationRequested` | Reconciler engaged |
| `dirty` → `failed` | `OperatorDiscardRequested` (post-MVP — currently rejected per concern "no silent repair") | Operator-explicit terminal |
| `failed` → `reconciliation_required` | `ReconciliationRequested` | Operator-driven repair path |
| `failed` → `ready` | `OperatorRetrySucceeded` (after manual fix; audit retains failure record) | Failure cleared |
| `inaccessible` → `ready` | `ProviderReadinessValidated` (after revocation/credential restoration) | Authorization restored |
| `unknown_provider_outcome` → `ready` | `ReconciliationCompletedClean` | Provider state confirms no mutations applied |
| `unknown_provider_outcome` → `committed` | `ReconciliationCompletedDirty` (commit confirmed upstream) | Idempotency replay returns success |
| `unknown_provider_outcome` → `failed` | `ReconciliationCompletedDirty` (commit refused upstream) | Categorized failure recorded |
| `unknown_provider_outcome` → `reconciliation_required` | `ReconciliationEscalated` (automated reconciler cannot decide) | Manual operator decision required |
| `reconciliation_required` → `ready` | `ReconciliationCompletedClean` | All-clear after manual or automated review |
| `reconciliation_required` → `committed` | `ReconciliationCompletedDirty` (commit confirmed upstream) | Apply upstream truth |
| `reconciliation_required` → `failed` | `OperatorMarkedFailed` | Operator declares terminal failure |

**Implementation enforcement:**

- `Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` implements this matrix as a switch expression over `(currentState, eventType)` returning `DomainResult` (per Step 5 §"Process Patterns").
- **Aggregate test (CI gate):** for every state in the catalog and every event in the architecture event vocabulary, at least one test asserts the documented outcome (positive transition OR explicit rejection with `state_transition_invalid`). CI fails if a state or event is added without test coverage.
- **Operator-disposition mapping** is sourced from this table; `Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` is generated from it (or hand-written and tested against it) so F-4 console labels cannot drift from architecture.

## Starter Template Evaluation

### Primary Technology Domain

API/backend service module on .NET 10 / Hexalith ecosystem (EventStore + Tenants + Dapr + Aspire). The project is greenfield at the module level but brownfield at the ecosystem level — mandatory integration with Hexalith.Tenants and Hexalith.EventStore constrains the technical foundation.

### Starter Options Considered

No third-party generic starter (Vite, Next.js, T3, NestJS, `dotnet new aspire-starter`) fits this project. Hexalith ecosystem modules require central package management, `Directory.Build.props` inheritance, naming conventions, project-layout patterns, and event-sourced domain-service wiring that no public template provides. Generic starters would create immediate convention drift from sibling modules.

The honest finding: **the starter is a sibling-module template, not a CLI-generated boilerplate**. Both Hexalith.Tenants and Hexalith.EventStore are present as submodules in this workspace and provide proven scaffolding patterns:

| Option | Verdict |
| --- | --- |
| Mirror Hexalith.Tenants structure (baseline) + layer in EventStore.Admin.* surfaces (Cli/Mcp/UI) | **Selected.** Closest functional analogue: Tenants is an event-sourced consumer of platform services; EventStore.Admin.* already provides the CLI/MCP/UI conventions Folders needs. Two reference patterns to merge, but each is well-proven. |
| Mirror Hexalith.EventStore in full (including Admin, SignalR, Server.Host, Abstractions) | Fallback if the Tenants-baseline approach proves leaky during scaffolding. |
| `dotnet new aspire-starter` + manual Hexalith retrofit | Rejected — convention drift cost outweighs tooling familiarity. |
| Custom scaffold from PRD only | Rejected — reinvents proven sibling patterns. |

### Selected Starter: Hexalith.Tenants Project Structure (baseline) + Hexalith.EventStore.Admin.* Surfaces (Cli/Mcp/UI)

**Rationale for Selection:**

Hexalith.Tenants is the closest functional analogue: it is an event-sourced .NET 10 module that consumes EventStore, publishes via Dapr pub/sub, exposes a domain service, and ships AppHost + Aspire + ServiceDefaults + Testing helpers. Hexalith.EventStore.Admin.* (Cli, Mcp, UI) provides the proven adapter patterns for the CLI, MCP, and read-only operations console surfaces the PRD requires. Combining the two minimizes reinvention while staying faithful to ecosystem conventions.

### Recommended Project Layout

```
Hexalith.Folders/
├── Hexalith.Folders.slnx
├── Directory.Build.props          ← mirror Hexalith.Tenants
├── Directory.Packages.props       ← central package management; pin Hexalith.* 3.15.1
├── src/
│   ├── Hexalith.Folders.Contracts          ← commands, events, state DTOs, projection DTOs, query DTOs (REFERENCE: Tenants.Contracts pattern)
│   ├── Hexalith.Folders                    ← Folder + Organization aggregates, state Apply, projection handlers (REFERENCE: Tenants core project)
│   ├── Hexalith.Folders.Server             ← domain service host: /process, /project endpoints, REST canonical transport (REFERENCE: Tenants.Server)
│   ├── Hexalith.Folders.Client             ← typed canonical SDK + tenant subscription wiring (REFERENCE: Tenants.Client)
│   ├── Hexalith.Folders.Cli                ← CLI adapter wrapping the SDK (REFERENCE: EventStore.Admin.Cli)
│   ├── Hexalith.Folders.Mcp                ← MCP server wrapping the SDK (REFERENCE: EventStore.Admin.Mcp)
│   ├── Hexalith.Folders.UI                 ← read-only ops console (Blazor) (REFERENCE: EventStore.Admin.UI)
│   ├── Hexalith.Folders.Workers            ← Git/workspace process managers reacting to events (NEW; per EventStore aggregate research)
│   ├── Hexalith.Folders.Aspire             ← Aspire AppHost helper extensions (REFERENCE: Tenants.Aspire)
│   ├── Hexalith.Folders.AppHost            ← .NET Aspire AppHost (REFERENCE: Tenants.AppHost)
│   ├── Hexalith.Folders.ServiceDefaults    ← shared service-defaults extensions (REFERENCE: Tenants.ServiceDefaults)
│   └── Hexalith.Folders.Testing            ← in-memory fakes + builders + conformance assertions (REFERENCE: Tenants.Testing)
├── tests/
│   ├── Hexalith.Folders.Contracts.Tests
│   ├── Hexalith.Folders.Tests              ← aggregate unit + replay + projection tests
│   ├── Hexalith.Folders.Server.Tests
│   ├── Hexalith.Folders.Client.Tests
│   ├── Hexalith.Folders.Cli.Tests
│   ├── Hexalith.Folders.Mcp.Tests
│   ├── Hexalith.Folders.UI.Tests
│   ├── Hexalith.Folders.Workers.Tests
│   ├── Hexalith.Folders.Testing.Tests      ← conformance tests for the testing-fakes library itself
│   └── Hexalith.Folders.IntegrationTests   ← Aspire/Dapr E2E
└── samples/
    ├── Hexalith.Folders.Sample             ← end-to-end demo app
    └── Hexalith.Folders.Sample.Tests
```

**Initialization Approach:**

There is no single `dotnet new` command. Project scaffolding proceeds as a multi-project sequence and is itself the first implementation story (per the EventStore-aggregate research roadmap, Phase 0):

```bash
# From the Hexalith.Folders root
dotnet new sln -n Hexalith.Folders --format slnx

# Mirror Tenants Directory.Build.props and Directory.Packages.props (pin Hexalith.* 3.15.1)
# Then create projects in the order shown in the layout above:
#   src/* projects (in dependency order: Contracts → Hexalith.Folders → Server → Client → adapter projects)
#   tests/* mirroring src/ 1:1
#   samples/* per sibling-module convention
```

### Architectural Decisions Provided by This Starter Pattern

**Language & Runtime:**

- .NET 10 / C#, nullable enabled, implicit usings, `LangVersion=latest`, warnings as errors (mirrors Tenants `Directory.Build.props`).

**Package Management:**

- Central package management via `Directory.Packages.props`. Pin `Hexalith.EventStore.*` and `Hexalith.Tenants.*` to **3.15.1** (verified on NuGet 2026-05-09, latest published).
- `Hexalith.Tenants.Client` will use a project reference to the submodule project until package availability is confirmed (per Tenants research caveat).
- Aspire packages from `CommunityToolkit.Aspire.Hosting.Dapr`.

**Solution Format:**

- `.slnx` (sibling-module convention).

**Domain Layout:**

- Two aggregate roots in `src/Hexalith.Folders`: `OrganizationAggregate` (provider bindings, repo policy, credential refs, ACL baseline) and `FolderAggregate` (lifecycle, storage mode, repository binding, workspace readiness, ACL overrides, file-op metadata). Repository deferred per EventStore-aggregate research.
- Aggregate identity: `{managedTenantId}:folders:{folderId}` and `{managedTenantId}:organizations:{organizationId}`. Folders never use the `system` tenant.
- Aggregate IDs are opaque immutable identifiers (GUID/ULID); folder hierarchy is projected metadata, not identity (per Project Context Analysis cross-cutting concern #14).

**Surface Topology:**

- `Hexalith.Folders.Client` is the typed canonical SDK; `Hexalith.Folders.Cli` and `Hexalith.Folders.Mcp` wrap the SDK; `Hexalith.Folders.Server` exposes REST as the parallel transport. This realizes the SDK-as-canonical reframe from the Project Context Analysis (Scale & Complexity section).

**Build Tooling:**

- `dotnet build` / `dotnet test` via standard .NET SDK; CI gates inherit from sibling modules' workflows.

**Testing Framework:**

- xUnit v3 + Shouldly + NSubstitute + Testcontainers + Aspire test host (sibling-module convention).
- Test projects mirror `src/` 1:1.
- `Hexalith.Folders.Testing` provides in-memory fakes (mirroring `InMemoryTenantService` pattern) for fast aggregate tests.
- `Hexalith.Folders.Testing.Tests` validates that fakes delegate to production aggregate logic (mirroring `TenantConformanceTests`).

**AppHost Composition:**

- EventStore (`AppId=eventstore`) + Tenants (`AppId=tenants`) + Folders.Server (`AppId=folders`) + Folders.Workers + Folders.UI (`AppId=folders-ui`) + optional Keycloak.
- Shared Dapr `statestore` and `pubsub` components.
- Production Dapr access control is deny-by-default with mTLS.

**Submodule Policy:**

- Root-level submodules only. `git submodule update --init --recursive` is forbidden per `CLAUDE.md`.

**Note:** Project scaffolding using the layout above should be the first implementation story (Phase 0 in the implementation roadmap).

## Core Architectural Decisions

### Decision Priority Analysis

**Already Decided (from Project Context Analysis and Starter Template Evaluation):**

- Language/runtime: .NET 10 / C#
- Write-side framework: Hexalith.EventStore 3.15.1 (CQRS + event sourcing)
- Tenant identity source of truth: Hexalith.Tenants 3.15.1 (consumed via Dapr pub/sub `system.tenants.events` + local fail-closed projection)
- Sidecar runtime: Dapr (pub/sub, state store, service invocation, actors, access control, resiliency)
- Local orchestration: .NET Aspire (`Aspire.Hosting.AppHost` 13.3.0; `CommunityToolkit.Aspire.Hosting.Dapr` 13.0.0)
- Observability: OpenTelemetry (traces / metrics / logs)
- Testing: xUnit v3 + Shouldly + NSubstitute + Testcontainers + Aspire test host
- Project layout: Tenants-baseline + EventStore.Admin.* surfaces (per Step 3)
- Aggregate identity: `{managedTenantId}:{domain}:{aggregateId}`; folders never use `system` tenant; aggregate IDs are opaque immutable (GUID/ULID)
- Aggregates: `OrganizationAggregate` + `FolderAggregate` (Repository deferred per EventStore-aggregate research)
- Surface topology: SDK is canonical typed client; CLI and MCP wrap SDK; REST is parallel transport (per Project Context Analysis Scale & Complexity)

**Critical Decisions (Block Implementation) — recorded in this step:**

1. **C0 — Contract Spine artifact and generator chain** (single source-of-truth for cross-surface code-gen)
2. **C11 — File-content transport model** (v1-OpenAPI-blocking)
3. **Dapr state-store backend** (production)
4. **Dapr pub/sub broker** (production)
5. **Authentication / OIDC provider** (production; local already standardized on Keycloak via Tenants AppHost)
6. **CLI framework** (System.CommandLine vs. Spectre.Console)
7. **MCP server SDK** (ModelContextProtocol C# SDK 1.3.0)
8. **GitHub provider client** (Octokit 14.0.0)
9. **Forgejo provider client** (generated OpenAPI client vs. typed HttpClient wrapper)
10. **Read-only ops console hosting model** (Blazor Server vs. WebAssembly)
11. **Working-copy storage location** (where Git checkouts physically live)
12. **Operational-state durability binding** (locks, idempotency records — concrete Dapr backings)

**Important Decisions (Shape Architecture) — also recorded:**

13. Audit storage strategy (dedicated read model vs. EventStore-derived projection)
14. Idempotency record TTL policy
15. Snapshot strategy per aggregate
16. Provider rate-limit handling pattern
17. Background reconciliation pattern (process managers reacting to events)

**Deferred Decisions (Post-MVP — explicitly NOT decided here):**

- Local-only folder mode (PRD scope reduction)
- Repair workflows / repair console
- Multi-organization-per-tenant
- Brownfield repository adoption
- Webhook ingestion (architectural posture: NONE in MVP)
- Additional Git providers beyond GitHub + Forgejo

### Data Architecture

| # | Decision | Choice | Rationale | Alternatives considered |
|---|---|---|---|---|
| D-1 | Aggregate event persistence | **Hexalith.EventStore 3.15.1 with Dapr state store** (per ecosystem) | Mandated by ecosystem + PRD audit/replay/determinism requirements | None viable inside the ecosystem |
| D-2 | Dapr state-store backend (local) | **Redis 7.x via Aspire** | Mirrors Tenants AppHost; required for shared state across EventStore + Tenants + Folders sidecars | Postgres (heavier local setup) |
| D-3 | Dapr state-store backend (production) | **Redis-compatible (Azure Cache for Redis or self-hosted Redis 7+) for state and pub/sub baseline; PostgreSQL upgrade path documented** for higher-durability tenants. **Postgres-escalation trigger CRITERIA are recorded; trigger THRESHOLDS are deferred-until-C1** (cannot be set independently of capacity targets without producing a circular reference). Criteria: (a) per-tenant state-store size sustained beyond Redis-replication operational comfort (threshold pinned alongside C1), (b) tenant SLA mandates point-in-time-recovery / multi-region durability beyond Redis-replication guarantees (threshold-free; SLA-driven), (c) actor-reminder volume sustained beyond per-sidecar headroom (threshold pinned alongside C1). Provisional placeholders for early infrastructure planning (NOT recorded decisions): 50 GB and 5,000 reminders/sec/sidecar — to be confirmed or revised when C1 sets concurrent capacity targets | Redis carries the Tenants/EventStore production pattern; Postgres is the documented escalation per EventStore deployment guidance | Cosmos DB (Azure-coupling), Kafka for pub/sub (operational weight) |
| D-4 | Dapr pub/sub broker (local) | **Redis Streams via Aspire** | Mirrors Tenants AppHost | RabbitMQ |
| D-5 | Dapr pub/sub broker (production) | **Redis Streams baseline; RabbitMQ or Azure Service Bus as escalation when delivery durability or throughput requires it** | Carries local pattern; defers broker complexity until measured need | Kafka (operational weight for MVP scale) |
| D-6 | Snapshot strategy per aggregate | **Conservative defaults from `SnapshotManager` (every 50 events) for `folders`; lower per-tenant override available via tenant-domain settings** | Per EventStore-aggregate research: folder streams may grow under heavy file activity | No snapshots (replay cost), per-event snapshots (storage cost) |
| D-7 | Idempotency record TTL | **Two fixed tiers** documented in extension `x-hexalith-idempotency-ttl-tier`: `mutation = 24h` (workspace prepare, lock, file mutation, cleanup); `commit = retention-period(C3)` (commit operation IDs persist for the audit retention period to support reconciliation). **No free-form per-command knob** — limits the test matrix and prevents drift | Two tiers cover real reconciliation needs while keeping the test surface bounded; per-command TTLs would require per-command expiry tests, clock-skew tests, and reconciliation race tests against `I-9` workers that may resubmit at TTL boundary | Per-command free-form TTL (test explosion), single global TTL (commit reconciliation insufficient) |
| D-8 | Working-copy storage (Git checkout location) | **Per-AppHost ephemeral filesystem under a configurable root** (`/var/lib/hexalith-folders/work/{tenantId}/{folderId}/{taskId}`); checkouts are disposable and never authoritative; their existence is recorded as workspace-readiness state in EventStore, not in working-copy itself | Working copy is a cache, not state; per concern #21 (operational state durability classification) it is in the "acceptably lost on restart" tier with a "rebuild from events + provider" recovery contract | Persistent volumes (deferred until measured benefit), object storage (latency cost) |
| D-9 | File-content transport in API (C11 — v1-OpenAPI-blocking) | **REST contract is bimodal:** two distinct operations — `PutFileInline` (≤256KB, JSON body with content + metadata) and `PutFileStream` (multipart `application/octet-stream` with metadata part) — with the 256KB boundary enforced server-side via `413 Payload Too Large` plus a `x-hexalith-retry-as: stream` response header. **NSwag generates two typed SDK methods** (`PutFileInlineAsync`, `PutFileStreamAsync`). **`Hexalith.Folders.Client` adds a hand-written convenience method `UploadFileAsync(stream)`** that picks the right typed method based on stream length — bimodal contract for clean OpenAPI/parity tests, unimodal DX for SDK consumers. **Base64 explicitly rejected** (33% bandwidth waste, no integrity advantage over content-hash header on streamed transport) | Inline keeps small files simple and JSON-validatable; streaming keeps large files out of memory; bimodal REST gives crisp red-phase tests in both `*.Sdk.Tests` and `*.Rest.Tests`; convenience helper preserves single-call ergonomics for normal use | All-inline (memory cost), all-streaming (overhead for tiny config files), single REST operation with `oneOf` schema (NSwag union types break test ergonomics), external-content-reference (added storage tier complexity) |
| D-10 | Audit storage | **Dedicated audit projection under `Hexalith.Folders.Server` projection endpoints**, derived from event streams; rebuildable from events; retention policy per C3 | Per EventStore-aggregate research: projections, not aggregate streams, serve audit reads | Separate audit DB (operational cost), aggregate streams as audit (no compaction) |

### Authentication & Security

| # | Decision | Choice | Rationale | Alternatives considered |
|---|---|---|---|---|
| S-1 | Local IDP | **Keycloak** (mirrors Tenants AppHost) | Existing pattern in sibling AppHosts | Local-only PAT, OAuth2 mock |
| S-2 | Production OIDC provider | **Pluggable provider via `Microsoft.AspNetCore.Authentication.JwtBearer` with frozen validation parameters:** `ClockSkew = TimeSpan.FromSeconds(30)`, `RequireExpirationTime = true`, `RequireSignedTokens = true`, `ValidateIssuer = true` (issuer pinned per environment via configuration), `ValidateAudience = true` (audience pinned per environment via configuration), `ValidateLifetime = true`, `ValidateIssuerSigningKey = true`. **JWKS retrieval:** `OpenIdConnectConfigurationRetriever` with `AutomaticRefreshInterval = TimeSpan.FromMinutes(10)` and `RefreshInterval = TimeSpan.FromMinutes(1)` (forced-refresh minimum on signature-validation failure). **Token introspection:** JWT-only (no introspection round-trip). **Compatible providers:** Keycloak, Microsoft Entra ID, Auth0, or any OIDC-compliant provider exposing `/.well-known/openid-configuration`. **Non-pluggable items** (frozen by Hexalith.EventStore claim transformation): `sub` is principal; `eventstore:tenant` is authoritative tenant; `eventstore:permission` gates command/query access | Avoids vendor lock-in while removing implementer ambiguity; concrete `JwtBearerOptions` configuration eliminates Phase 3 stalls; pinned issuer/audience prevents token-confusion attacks; bounded JWKS cache prevents stale-key-after-rotation outages | Hard-bind to Keycloak (lock-in), hard-bind to Entra ID (Azure-coupling), introspection-only (latency cost, IdP coupling), under-specified pluggability (implementer ambiguity) |
| S-3 | JWT claim mapping | **`sub` claim = principal; tenant claim derived from Hexalith.EventStore claim transformation (`eventstore:tenant`); `eventstore:permission` claims for command/query gating; tenant-in-payload is INPUT not authority** (cross-cutting concern #12) | Sibling-module pattern; enforces tenant-context-provenance invariant | Trust payload tenant (insecure), per-route bespoke auth (drift) |
| S-4 | Authorization layering | **JWT validation → Hexalith.EventStore claim transform → local tenant-access projection (fail-closed-on-stale per C8 + C20) → folder ACL → EventStore validators → production Dapr deny-by-default policies + mTLS** | Per Project Context Analysis cross-cutting concern #2 | Single-layer (insufficient for tenant isolation) |
| S-5 | Provider credential storage | **Credential references only** (Hexalith.Tenants OR a Dapr secret store); never raw tokens in events, payloads, projections, logs, traces | PRD hard boundary; sentinel redaction tests enforce | Encrypted-in-events (key management cost) |
| S-6 | Sensitive metadata classification (C9) | **Default tier: paths + repo names + branch names + commit messages classified as `tenant-sensitive`** (visible to tenant members and operators-with-need-to-know; redacted in cross-tenant operator views and external diagnostics); per-tenant override allows `confidential` (hashed in audit/projection storage at write time) | Per cross-cutting concern #17 + Sally's redacted-vs-unknown UX rule | Default-public (leaks structural secrets), default-confidential (operator unable to diagnose) |

### API & Communication Patterns

| # | Decision | Choice | Rationale | Alternatives considered |
|---|---|---|---|---|
| A-1 | Contract Spine artifact (C0) | **OpenAPI 3.1 owned by `Hexalith.Folders.Contracts`**, with extension vocabulary: `x-hexalith-idempotency-key`, `x-hexalith-idempotency-equivalence`, `x-hexalith-idempotency-ttl-tier`, `x-hexalith-correlation`, `x-hexalith-lifecycle-states`, `x-hexalith-parity-dimensions`, `x-hexalith-audit-metadata-keys`, `x-hexalith-sensitive-metadata-tier` | Boring choice (Winston, confirmed strongly); well-tooled in .NET; survives the next maintainer | TypeSpec (immature .NET tooling), code-first (no canonical artifact) |
| A-2 | SDK generator | **NSwag** (mature in .NET ecosystem, Hexalith convention compatibility) — generated SDK lives in `Hexalith.Folders.Client`; NSwag templates emit a `ComputeIdempotencyHash()` helper per command DTO using the field list declared in `x-hexalith-idempotency-equivalence` (lexicographic order required) so consumers never reimplement canonicalization | Stable, supports OpenAPI 3.1 extensions for code-gen customization; per-command generated hash helper prevents Phase 5 provider-adapter drift | Kiota (newer, fewer .NET examples), hand-written (drift) |
| A-3 | REST canonical transport | **Hexalith.EventStore Command/Query API patterns + ASP.NET Core Minimal APIs in `Hexalith.Folders.Server`** for `/process` and `/project` endpoints; OpenAPI generated by Microsoft.AspNetCore.OpenApi from controllers/handlers, **validated against the C0 Contract Spine in CI as a BLOCKING gate** (not advisory) — server output drifting from the Contract Spine fails the build | Sibling-module convention; preserves "REST is one of two transports" framing; blocking validation prevents the most common drift failure mode (controllers quietly becoming the spec) | REST as source of truth (loses cross-surface generation), gRPC (PRD requires REST), advisory-only validation (drift) |
| A-4 | CLI framework | **System.CommandLine 2.x** (Microsoft, supports .NET 10, hierarchical commands, JSON output) — `Hexalith.Folders.Cli` wraps `Hexalith.Folders.Client` | Microsoft-supported, generates from same SDK contract; no parity drift | Spectre.Console (richer UI, but heavier dependency for CLI-as-adapter) |
| A-5 | MCP server SDK | **ModelContextProtocol C# SDK 1.3.0** in `Hexalith.Folders.Mcp` wrapping `Hexalith.Folders.Client` | Official SDK, active development, .NET 10 compatible | Custom MCP implementation (drift, maintenance) |
| A-6 | GitHub provider client | **Octokit 14.0.0 (.NET)** inside the GitHub adapter; not surfaced beyond the provider port | Most-mature .NET GitHub client; abstracts auth + retry; supports GitHub Apps | Direct HttpClient (reinvent), Refit (less feature-coverage) |
| A-7 | Forgejo provider client | **Typed HttpClient wrapper hand-written in `Hexalith.Folders` provider adapter, fed by per-version `swagger.v1.json` snapshots** stored at `tests/contracts/forgejo/<version>/swagger.v1.json` keyed by Forgejo semver. A `tests/contracts/forgejo/supported-versions.json` manifest declares the test matrix (minimum: latest stable + latest LTS + n-1 minor + any pinned customer instance). **Nightly schema-diff job using oasdiff** runs against each pinned upstream tag plus a weekly `HEAD` poll; classifier flags additive (warn) vs breaking (fail). Add response-equivalence tests against the GitHub adapter's port shape so C13 has an oracle for the Forgejo side | Forgejo OpenAPI is per-instance; generated clients risk version skew per Forgejo research; hand-written wrapper allows per-version pinning + drift detection (C12); structured snapshot directory + oasdiff makes drift MTTD measured in minutes, not days | Generic Gitea client (compatibility risk per research), generated client (version-skew risk), unstructured contract tests (silent drift) |
| A-8 | Error contract | **PRD §"Error Codes" required fields (`category`, `code`, `message`, `correlationId`, `retryable`, `clientAction`, `details`) serialized as RFC 9457 Problem Details** (`application/problem+json`); cross-surface parity asserted via C13 parity oracle | Standards-compliant, supports cross-surface mapping | Custom JSON shape (no standard tooling) |
| A-9 | Idempotency keys | **Per cross-cutting concern #3:** required for prepare / lock / file mutation / commit / cleanup; per-command payload-equivalence rule (canonical hash, fields documented in extension `x-hexalith-idempotency-equivalence` in **lexicographic order** as a normative requirement). **NSwag emits a `ComputeIdempotencyHash()` helper per command DTO** so consumers never reimplement canonicalization. **AC: one hash-equivalence test per command, generated, not hand-rolled.** Two TTL tiers per D-7 | Unambiguous, auditable, generated equivalence prevents adapter drift | Hash-entire-payload (timestamps cause false conflicts), hand-rolled per-consumer canonicalization (drift) |
| A-10 | Correlation propagation | **`X-Correlation-Id` and `X-Hexalith-Task-Id` headers carry across REST, SDK, CLI, MCP; CommandEnvelope correlation/causation IDs propagate through EventStore → projection → audit; parity oracle (C13) asserts the chain end-to-end** | Per cross-cutting concern #15 | Ad-hoc correlation (drift), no task-ID separation (loses task lineage) |
| A-11 | API versioning | **`v1` URL-versioned (`/api/v1/...`); breaking changes get a new major version; OpenAPI extension version in C0 Contract Spine** | PRD §API Versioning | Header-versioning (less discoverable) |

#### Adapter Parity Contract

The SDK-as-canonical reframe (per §"Project Context Analysis → Scale & Complexity") collapses *transport* parity to SDK-vs-REST. It does NOT collapse *behavioral* parity. The following per-adapter contract specifies the dimensions that wrapping the SDK does not unify on its own; each row is asserted via the C13 parity oracle's behavioral-parity columns and consumed by `*.Cli.Tests` and `*.Mcp.Tests`.

| Behavioral Dimension | SDK (`Hexalith.Folders.Client`) | CLI (`Hexalith.Folders.Cli`) | MCP (`Hexalith.Folders.Mcp`) |
| --- | --- | --- | --- |
| **Idempotency-Key sourcing** | Caller-provided via SDK method parameter, OR resolved from registered `IIdempotencyKeyProvider` (DI). NEVER auto-generated by SDK. | `--idempotency-key <key>` flag (required for mutating commands) OR `--allow-auto-key` opt-in flag (CLI generates ULID, prints to stderr for retry traceability). Mutating command without either flag → exit code 64 (`USAGE_ERROR`). | Tool-input field `idempotencyKey` (required for mutating tools per JSON Schema). Missing field → MCP failure `kind = "usage_error"`. MCP server NEVER auto-generates. |
| **CorrelationId sourcing** | Caller-provided via `X-Correlation-Id` header, OR resolved from registered `ICorrelationIdProvider` (DI). When neither set, SDK generates a fresh ULID per call (logged at `Information`). | Generated as fresh ULID per CLI invocation, propagated to all sub-calls within the invocation. Override via `--correlation-id <id>` for cross-tool tracing. | Tool-input field `correlationId` (optional). When omitted, MCP server generates fresh ULID per tool call. Always echoed in tool result for caller correlation. |
| **TaskId sourcing** | Caller-provided via `X-Hexalith-Task-Id` header. SDK does not generate; required for task-scoped operations (lock, file mutation, commit). | `--task-id <id>` flag (required for task-scoped commands). Validation occurs client-side; missing → exit code 64. | Tool-input field `taskId` (required for task-scoped tools per JSON Schema). |
| **Credential sourcing** | `IConfiguration` binding to `HexalithFolders:Authentication:Token` OR `IAccessTokenProvider` (DI). | Precedence: (1) `HEXALITH_TOKEN` env var, (2) `~/.hexalith/credentials.json` (per-tenant section), (3) `--token <jwt>` flag. Missing → exit code 65 (`CREDENTIAL_MISSING`). | MCP server config `auth.token` OR `auth.tokenFile` path. Missing → server startup error; tool calls return MCP failure `kind = "credential_missing"`. |
| **Pre-SDK error class** (canonical category, before any HTTP call) | `HexalithFoldersConfigurationException` (config invalid), `HexalithFoldersAuthenticationException` (token missing/expired locally) | Maps to canonical category `client_configuration_error` (exit 64) or `credential_missing` (exit 65) | Maps to canonical category `client_configuration_error` (MCP failure kind `usage_error`) or `credential_missing` (MCP failure kind `credential_missing`) |
| **Post-SDK error projection** (server returned RFC 9457 problem + canonical error category) | Throws `HexalithFoldersException(category, code, correlationId, retryable, clientAction, details)` — caller catches and decides | Maps to exit code per canonical exit-code table below; emits human or JSON output per `--output` flag | Maps to MCP tool failure with `kind` derived from canonical category; failure structure includes `correlationId`, `code`, `retryable`, `clientAction` |
| **Audit metadata keys** | identical to REST | identical to REST | identical to REST |
| **Terminal states** | identical to REST | identical to REST | identical to REST |

**CLI exit-code mapping (canonical, asserted by C13 oracle):**

| Exit Code | Canonical Category | Meaning |
| --- | --- | --- |
| 0 | `success` | Command succeeded |
| 64 | `client_configuration_error` | Usage / argument / config error (sysexits.h convention) |
| 65 | `credential_missing` | No valid credentials found |
| 66 | `tenant_access_denied` | Authorization failed at tenant or folder ACL boundary |
| 67 | `workspace_locked` | Lock contention, retry-eligible |
| 68 | `idempotency_conflict` | Same key, different payload — caller bug |
| 69 | `validation_error` | Server-side input validation failed |
| 70 | `provider_failure_known` | Provider returned categorized failure (timeout / 4xx / 5xx) |
| 71 | `provider_outcome_unknown` | Provider call did not confirm; reconciliation entered |
| 72 | `reconciliation_required` | Workspace in `reconciliation_required` state; not retryable until cleared |
| 73 | `not_found` | Resource not found within tenant scope |
| 74 | `state_transition_invalid` | Operation not valid in current workspace state (per C6 matrix) |
| 75 | `redacted` | Field redacted by tenant policy (informational; not a hard failure for diagnostic commands) |
| 1 | `internal_error` | Catch-all for unmapped server exceptions; correlation ID always emitted to stderr |

**MCP failure-kind mapping (canonical, asserted by C13 oracle):** every MCP tool failure result includes `kind ∈ {usage_error, credential_missing, tenant_access_denied, workspace_locked, idempotency_conflict, validation_error, provider_failure_known, provider_outcome_unknown, reconciliation_required, not_found, state_transition_invalid, redacted, internal_error}` plus `correlationId`, `code`, `retryable`, `clientAction`. The `kind` set is identical to the canonical category set (one-to-one mapping); never collapse multiple categories into a single `kind` for MCP convenience.

**Cross-adapter invariants (asserted by parity tests):**

- For the same input + same authoritative tenant + same correlation/task/idempotency triple, the (canonical category, code, retryable flag, clientAction) returned by SDK, REST, CLI (post-projection), and MCP (post-projection) are **identical**.
- Idempotency replay semantics are **identical** across adapters: same key + equivalent payload → same logical result; same key + different payload → `idempotency_conflict`.
- CorrelationId, when caller-supplied, is **echoed unchanged** through all four surfaces.
- Pre-SDK error classes (configuration, credential-missing) are **mutually exclusive** with post-SDK error classes; tests assert that no operation can return both.

### Frontend Architecture (Read-Only Operations Console)

| # | Decision | Choice | Rationale | Alternatives considered |
|---|---|---|---|---|
| F-1 | UI framework | **Blazor Server** in `Hexalith.Folders.UI` (matches `Hexalith.EventStore.Admin.UI` and Hexalith.FrontComposer pattern) | Sibling-module convention; .NET-native; no separate JS toolchain | Blazor WebAssembly (slower first-load for incident-mode), React/Vue SPA (separate build chain, drift risk) |
| F-2 | Hosting model | **Blazor Server with SignalR** for live status updates; consumes `Hexalith.Folders.Client` SDK; reads only from projections (no direct EventStore aggregate access from UI) | Real-time status indicators serve incident-response; per cross-cutting concern #11 the console is read-only and read-model-based | WebAssembly (offline capability not needed for ops console; SSR latency advantage matters more) |
| F-3 | Component library | **Microsoft Fluent UI Blazor (`Microsoft.FluentUI.AspNetCore.Components`)** — provides accessible primitives, satisfies WCAG 2.2 AA targets (focus-visible, target sizes, dragging exemption confirmed), matches Microsoft ecosystem | Accessibility-first; minimal custom CSS for incident-mode clarity | MudBlazor (fewer accessibility audits), bespoke CSS (cost) |
| F-4 | State / status visual model | **Per Sally's S1 — operator-disposition labels are the primary visual** (`auto-recovering`, `awaiting-human`, `terminal-until-intervention`, `degraded-but-serving`); technical state names appear as secondary metadata | UX-driven (incident cognition); per cross-cutting concern #4 + #11 | Technical-state-primary (cognitive overhead at 3 AM) |
| F-5 | Redaction affordance | **Per Sally's S2 — redacted fields render with a visible lock-icon affordance** ("your tenant policy hides this; contact your administrator"); never silent truncation | Per cross-cutting concern #11; prevents operators chasing ghosts | Silent redaction (debugging confusion) |
| F-6 | Incident-mode last-resort read path | **Separate ACL-checked event-stream view at `/_admin/incident-stream`**, available when projections are degraded; surfaces latest events for operators with `eventstore:permission=admin`. **Three UX guardrails before MVP ships:** (1) **persistent red banner** — *"DEGRADED MODE — events shown may be incomplete or out of order. Last projection checkpoint: HH:MM:SS UTC"*; (2) **operator-disposition labels (F-4) rendered alongside raw event types** so operators do not switch vocabularies mid-incident; (3) **one-click "copy correlationId + timestamp window" affordance** for handoff to other engineers. **Lock icons from F-5 still apply** — redaction rules do not relax just because projections are down | Per cross-cutting concern #11; sleep-deprived operators following a runbook link must not land on a raw event stream with zero scaffolding | No fallback (console useless when projections break), unguarded raw access (security risk), bare event view (cognitive overload during incident) |
| F-7 | Operations console performance budget | **p95 page-load < 1.5s for primary diagnostic flows; p99 < 3s; degraded-mode (incident) flows allowed up to 5s p95** — separate from end-user budgets in PRD §NFR Performance. **Perceived-wait UX requirements:** visible skeleton state at 400ms for any view that may take longer; "still loading… [cancel]" affordance at 2s for any in-flight request | Per cross-cutting concern #11; budget on its own does not solve the "is the console itself the new outage" perception under incident load | Same budget as end-user (insufficient under incident load), no budget (unmeasurable), budget-only-no-perceived-wait (operator clicks twice, sees spinner, doubts the console) |

### Infrastructure & Deployment

| # | Decision | Choice | Rationale | Alternatives considered |
|---|---|---|---|---|
| I-1 | Local orchestration | **.NET Aspire AppHost (`Aspire.Hosting.AppHost` 13.3.0 + `CommunityToolkit.Aspire.Hosting.Dapr` 13.0.0)** in `Hexalith.Folders.AppHost` | Sibling-module convention | Docker Compose (no .NET integration), pure CLI (no resource graph) |
| I-2 | Production hosting | **Container-based (Docker images per service); Kubernetes-friendly but not Kubernetes-required**; Dapr sidecars deployed alongside each Hexalith.Folders service container | Carries Dapr/Aspire portability; no immediate Kubernetes dependency for MVP | Kubernetes-required (premature for MVP), VM-based (no Dapr sidecar story) |
| I-3 | Dapr access control | **Local: development `accesscontrol.yaml` with `defaultAction: allow`** (mirrors Tenants AppHost). **Production: deny-by-default + mTLS**, app IDs restricted: `folders` may invoke `eventstore` and `tenants`; may NOT invoke `system` admin; may publish/subscribe `pubsub` only for declared topics. **Validated by a `dapr-policy-conformance` CI job (in I-5 gate list)** running `daprd` in a kind cluster with the production policy YAML and executing a **negative test suite** that asserts unauthorized `(sourceAppId, targetAppId, operation)` triples receive `403` on every `invoke` and `pubsub` topic; a property-based generator over the triple space provides exhaustive negative coverage. **Block merge on policy YAML changes without corresponding negative test additions** | PRD §NFR Security; carries Tenants production guidance; production policy without negative tests is theater (the first time the policy ships should not be in production) | Trust-by-default (insecure), policy-without-tests (silent regression on policy changes) |
| I-4 | Service identity | **Stable Dapr app IDs across environments**: `eventstore`, `tenants`, `folders`, `folders-ui`, `folders-workers` | Required for access-control YAML portability | Dynamic IDs (breaks policies) |
| I-5 | CI/CD | **GitHub Actions** (carries sibling-module convention; CLAUDE.md repo is on GitHub); pipeline gates: build, format, lint (including C10 cache-key tenant-prefix lint), unit tests, contract tests (hermetic), parity tests (C13), redaction sentinel tests (C6), nightly live-drift provider tests (C12), `dapr-policy-conformance` negative-test job (per I-3), Forgejo schema-diff job (per A-7) | Boring choice; matches existing Hexalith patterns | Forgejo Actions self-hosted (operational cost early), GitLab CI (different ecosystem) |
| I-6 | Observability | **OpenTelemetry SDK exporting to OTLP**: traces (correlation/causation/task IDs as span attributes), metrics (per cross-cutting concern KPIs), logs (structured, redacted). Local: OTLP collector via Aspire; production: pluggable exporters (Jaeger / Tempo / Application Insights / Datadog) | Sibling-module convention; vendor-neutral | Vendor-specific SDK (lock-in) |
| I-7 | Snapshot/health monitoring | **Health-check endpoints** on each Folders service (`/health/live`, `/health/ready`); monitored snapshots: dead-letter topic depth, projection lag (status-freshness target C2), Dapr sidecar health, Tenants-availability degraded-mode active flag | Per cross-cutting concern #20 + EventStore operational guidance | No health checks (silent failure) |
| I-8 | Provider rate-limit handling | **Per-provider token bucket scoped per-tenant for user-driven calls; per-provider global bucket for background reconciliation; backoff with jitter; reconciliation queue feeds C12 drift detection on sustained 429s.** **Chaos test in CI injects synthetic 429 storms** to verify the reconciliation queue does not unbounded-grow and that C12 drift signal fires within SLO | Prevents one tenant DOS'ing the provider for others; preserves provider quota; chaos test prevents silent unbounded-queue regressions | Single global bucket (cross-tenant DoS), no rate-limit handling (provider ban), no chaos test (silent regression) |
| I-9 | Background reconciliation | **Process-manager pattern in `Hexalith.Folders.Workers`**: workers subscribe to events (`FolderGitRepositoryRequested`, `WorkspacePreparationFailed`, `unknown_provider_outcome`), perform external work (provider calls, working-copy operations), submit follow-up commands; idempotent by causation/correlation ID | Per EventStore-aggregate research; keeps aggregates pure | In-aggregate side effects (impure, untestable) |

### Decision Impact Analysis

**Implementation Sequence (informs future Step 6 epic breakdown):**

1. **Phase 0 — Solution Scaffolding:** project skeleton per Step 3 layout; central package management pinning Hexalith.* 3.15.1; `Directory.Build.props` + `Directory.Packages.props` mirroring Tenants; root `.slnx`. Empty placeholder files for `tests/load/`, `tests/fixtures/idempotency-encoding-corpus.json`, `tests/fixtures/parity-contract.schema.json`, `docs/exit-criteria/_template.md`.
2. **Phase 0.5 — Pre-Spine Workshop (BLOCKS Phase 1 Contract Spine authoring):** resolve every input the spine needs before it can be authored. Outputs land in `docs/exit-criteria/` and in this architecture document.
   - **C3 retention durations per data class** (Tech Lead + Legal + PM; output: `docs/exit-criteria/c3-retention.md`); D-7 commit-TTL inherits this value.
   - **C4 bounded MVP input limits** (Architect + PM; output: `docs/exit-criteria/c4-input-limits.md`); `maxItems` / `maxLength` / `maxBytes` / `maxResultCount` constraints land in the spine.
   - **C6 Workspace State Transition Matrix** (Architect; output: this document §"Workspace State Transition Matrix"); `FolderStateTransitions.cs` translates 1:1.
   - **S-2 OIDC parameter set** (already specified above; verify per-environment issuer + audience values pinned in deployment configuration template).
   - **Per-command annotations:** `x-hexalith-idempotency-equivalence` (canonical field list per mutating command, lexicographic order) and `x-hexalith-parity-dimensions` (with completeness-by-class CI assertion: mutating ops MUST declare `idempotency_key_rule`; query ops MUST declare `read_consistency_class`).
   - **Adapter Parity Contract** per §"Adapter Parity Contract" (idempotency-key sourcing, correlation default, credential sourcing, pre-SDK error mapping, CLI exit codes, MCP failure kinds).
3. **Phase 1 — Contract Spine (C0):** OpenAPI 3.1 contract in `Hexalith.Folders.Contracts` with extension vocabulary; NSwag SDK generation pipeline including `ComputeIdempotencyHash()` per-command helper template; parity oracle skeleton (C13) generated from Contract Spine with both transport-parity and behavioral-parity columns; `tests/fixtures/parity-contract.schema.json` validation gate; symmetric drift gate against `tests/fixtures/previous-spine.yaml`; CI gate wiring including the BLOCKING server-vs-spine validation per A-3.
3. **Phase 2 — Aggregates:** `OrganizationAggregate` + `OrganizationState` + commands/events + aggregate tests (per EventStore research). Then `FolderAggregate` + `FolderState` + commands/events + aggregate tests. Aggregate identity validation, terminate-able compliance.
4. **Phase 3 — Domain Service Hosting:** `Hexalith.Folders.Server` with `AddEventStore()` / `UseEventStore()`, `/process` and `/project` endpoints, REST canonical transport from OpenAPI; Aspire AppHost composition (EventStore + Tenants + Folders.Server); local Keycloak; cross-tenant isolation tests.
5. **Phase 4 — Tenant Integration:** `Hexalith.Folders.Client` integrates `AddHexalithTenants()`, subscription endpoint, fail-closed local tenant-access projection; layered authorization service; Tenants-availability degraded-mode wiring.
6. **Phase 5 — Provider Adapters:** GitHub adapter (Octokit), Forgejo adapter (typed HttpClient + per-version `swagger.v1.json` contract tests with oasdiff drift detection); provider port; capability discovery; hermetic-PR-gate + live-nightly-drift modes (C12).
7. **Phase 6 — Workers:** Git/workspace process managers; reconciliation flows for `unknown_provider_outcome`; provider rate-limit handling with chaos-test gate.
8. **Phase 7 — Adapter Surfaces:** `Hexalith.Folders.Cli` (System.CommandLine), `Hexalith.Folders.Mcp` (ModelContextProtocol SDK); parity tests via C13 oracle.
9. **Phase 8 — Read-Only Ops Console:** `Hexalith.Folders.UI` (Blazor Server + Fluent UI); operator-disposition labels (F-4); redaction affordances (F-5); incident-mode last-resort read path with three UX guardrails (F-6); perceived-wait UX (F-7).
10. **Phase 9 — Production Hardening:** Dapr deny-by-default access control + mTLS with negative-test conformance job (I-3); OpenTelemetry exporters; runbooks; backup/recovery validation; full C0–C13 exit-criteria evidence.

**Spine Authoring Checklist (Phase 1 entry gate):** the Contract Spine cannot be authored until every item below is decided. Each item has an artifact owner. Phase 1 begins only after every box is checked.

- [ ] Capability groups enumerated (✓ inherited from PRD §"Endpoint Specifications" — provider-readiness, folders, workspaces, files, commits, audit, ops-console, context queries)
- [ ] Error taxonomy enumerated (✓ inherited from PRD §"Error Codes")
- [ ] **C3 commit-TTL retention period set** (Tech Lead + Legal + PM → `docs/exit-criteria/c3-retention.md`)
- [ ] **C4 input limits set** (Architect + PM → `docs/exit-criteria/c4-input-limits.md`); `maxItems`, `maxLength`, `maxBytes`, `maxResultCount` per operation
- [ ] **C6 Workspace State Transition Matrix enumerated** (Architect → this document §"Workspace State Transition Matrix")
- [ ] **S-2 OIDC issuer + audience** pinned per environment in deployment configuration template (Architect + Security)
- [ ] **Per-command `x-hexalith-idempotency-equivalence`** field lists declared in lexicographic order for every mutating command
- [ ] **Per-command `x-hexalith-parity-dimensions`** declared (with completeness-by-class CI assertion enabled)
- [ ] **Adapter Parity Contract** finalized (idempotency-key sourcing per adapter, correlation-id default per adapter, credential sourcing per adapter, pre-SDK error mapping, CLI exit-code table, MCP failure-kind set) — see §"Adapter Parity Contract"
- [ ] **`tests/fixtures/parity-contract.schema.json`** authored (defines the row shape so future dimension additions are validated)
- [ ] **`tests/fixtures/idempotency-encoding-corpus.json`** authored (NFC/NFD/NFKC/NFKD/zero-width-joiner/ULID-case variants)
- [ ] **`tests/fixtures/previous-spine.yaml`** initialized (seed copy of v1 spine for symmetric drift detection)

**Cross-Component Dependencies:**

- **C0 Contract Spine blocks all surface generation** (SDK, REST scaffolding, CLI commands, MCP tools, parity oracle). It is the highest-priority Phase-1 deliverable.
- **C11 file-content transport (D-9) blocks the Contract Spine ship** (the OpenAPI cannot be frozen without it). Decided in this step (bimodal REST + unimodal SDK convenience helper).
- **C13 parity oracle requires C0** and feeds CI gates from Phase 1 onward.
- **Tenants integration (Phase 4) blocks Folder-level authorization tests** (without local projection, fail-closed behavior cannot be validated).
- **Provider adapters (Phase 5) block Workers (Phase 6)** which block end-to-end repository workflows.
- **Operations Console (Phase 8) depends on stable projections** (Phases 2–4) and on operator-disposition state model (C6 transition matrix).
- **Production Hardening (Phase 9) depends on all C0–C13 criteria being measurable**, not just defined; the `dapr-policy-conformance` negative-test job (I-3) is itself a Phase-9 production-readiness gate.

## Implementation Patterns & Consistency Rules

### Pattern Categories Defined

**Critical Conflict Points Identified:** ~40 areas where implementers (AI agents or humans) could make divergent choices without explicit rules. Categories: naming (C# / aggregate / event / domain / endpoint), structure (file layout / test location / fixture location), format (JSON casing / errors / dates / paths / headers), communication (topics / envelopes / claims / webhook posture), and process (aggregate purity / idempotency / locking / authorization order / sentinel redaction).

Most rules cascade from decisions in §"Core Architectural Decisions"; this section turns them into explicit consistency invariants with examples.

### Naming Patterns

**C# code naming (sibling-module convention):**

| Element | Convention | Example | Anti-pattern |
|---|---|---|---|
| Class / record / interface | PascalCase | `FolderAggregate`, `IFolderRepository` | `folder_aggregate`, `folderAggregate` |
| Method | PascalCase | `ApplyFolderCreated` | `apply_folder_created`, `applyFolderCreated` |
| Property | PascalCase | `TenantId`, `LastEventSequenceNumber` | `tenantId`, `last_event_sequence_number` |
| Local variable / parameter | camelCase | `folderId`, `commandEnvelope` | `FolderId`, `folder_id` |
| Constant | PascalCase | `DefaultLockLeaseSeconds` | `DEFAULT_LOCK_LEASE_SECONDS` |
| File | matches contained type | `FolderAggregate.cs` | `folder-aggregate.cs`, `folder_aggregate.cs` |

**Domain-driven naming (per EventStore identity scheme):**

| Element | Rule | Example |
|---|---|---|
| Domain name (EventStore identity) | lowercase, plural | `folders`, `organizations` |
| Aggregate class | `{Concept}Aggregate` | `FolderAggregate`, `OrganizationAggregate` |
| State class | `{Concept}State` | `FolderState`, `OrganizationState` |
| Command (imperative verb + concept) | `{Verb}{Concept}` | `CreateFolder`, `WriteFile`, `LockWorkspace`, `BindRepository` |
| Event (past-tense fact) | `{Concept}{Verbed}` | `FolderCreated`, `FileWritten`, `WorkspaceLocked`, `RepositoryBound` |
| Rejection event | `{Concept}{Verbed}Rejected` | `FolderCreationRejected` |
| Projection class | `{Concept}Projection` | `FolderTenantAccessProjection`, `WorkspaceStatusProjection` |
| Worker / process manager | `{Concept}Workflow` or `{Concept}Reconciler` | `RepositoryProvisioningWorkflow`, `WorkspaceReconciler` |

**REST endpoint naming (per PRD §"Endpoint Specifications" capability groups):**

- Lowercase, hyphen-delimited path segments: `/api/v1/provider-readiness`, `/api/v1/folders/{folderId}/workspace/lock`
- Plural collection nouns: `/folders`, `/files`, `/audit-events`
- Path parameters as `{camelCase}` in OpenAPI: `{folderId}`, `{workspaceId}`
- Capability-group prefixes (provider-readiness, folders, workspaces, files, commits, audit, ops-console)

**Project naming (per Step 3 layout):**

- All projects: `Hexalith.Folders.{Surface}` (e.g., `Hexalith.Folders.Server`, `Hexalith.Folders.Cli`)
- Test projects: `Hexalith.Folders.{Surface}.Tests` for unit; `Hexalith.Folders.IntegrationTests` for cross-component
- Sample project: `Hexalith.Folders.Sample`

**Identifier formats:**

| ID | Format | Example |
|---|---|---|
| Aggregate ID (per concern #14) | opaque ULID (lowercase Crockford base32, 26 chars) | `01htr2k4q9z8x7v6w5n4m3p2j1` |
| Tenant ID | per Hexalith.Tenants convention (kebab-case slug) | `acme-corp` |
| Correlation ID | ULID | `01htr2k4q9z8x7v6w5n4m3p2j1` |
| Task ID | ULID | `01htr2k4q9z8x7v6w5n4m3p2j2` |
| Idempotency key | client-supplied opaque string ≤ 128 chars; server canonicalizes by `x-hexalith-idempotency-equivalence` | any UTF-8 string |
| Aggregate identity (EventStore) | `{tenantId}:{domain}:{aggregateId}` | `acme-corp:folders:01htr...` |

### Structure Patterns

**Project organization (sibling-module convention):**

- `src/{ProjectName}/{ConceptArea}/{Type}.cs` — concept area subfolders for aggregates, projections, providers, workers (e.g., `src/Hexalith.Folders/Aggregates/Folder/FolderAggregate.cs`)
- `tests/{ProjectName}.Tests/` mirrors `src/` 1:1
- `tests/{ProjectName}.Tests/Fixtures/` for shared test data; **`tests/fixtures/audit-leakage-corpus.json`** is the normative cross-project sentinel corpus (per cross-cutting concern #6)
- `tests/contracts/forgejo/<version>/swagger.v1.json` for per-version Forgejo OpenAPI snapshots; `tests/contracts/forgejo/supported-versions.json` for the test matrix manifest (per A-7)
- `samples/Hexalith.Folders.Sample/` for the end-to-end demo app

**File-content rules:**

- One public type per `.cs` file; nested private types allowed
- File-scoped namespace declaration (`namespace Hexalith.Folders.Aggregates;`), not block-scoped
- `using` declarations sorted: System / Microsoft / Hexalith / third-party / project-local

### Format Patterns

**JSON wire format:**

| Concern | Rule |
|---|---|
| Property casing | **camelCase** (matches Hexalith.EventStore convention: `tenantId`, `correlationId`, `commitSha`) |
| Date / time | **ISO 8601 with `Z` suffix** (UTC always); never local time, never Unix epoch |
| Booleans | JSON `true` / `false` literals; never `0`/`1`, `"yes"`/`"no"`, or `"true"`/`"false"` strings |
| Null handling | Explicit `null` for required-but-empty fields; omit only optional fields |
| Enum serialization | **String values matching the enum identifier** (`"ready"`, `"locked"`, `"committed"`); per Tenants research, integer values are tolerated on event payloads but never produced by Folders |
| File path values | Forward-slash separator, workspace-root-relative, no leading slash, NFC-normalized Unicode |
| Bytes / blobs | Never inline in event payloads; reference by `contentHash` + `byteLength` + `mediaType` |

**HTTP headers (consumed and emitted by REST + SDK + CLI + MCP):**

| Header | Direction | Required | Notes |
|---|---|---|---|
| `Authorization: Bearer <jwt>` | request | yes | OIDC-issued; `sub` claim is principal |
| `Idempotency-Key` | request | yes for mutating ops | per A-9; opaque ≤ 128 chars |
| `X-Correlation-Id` | request + response | yes | ULID; propagated end-to-end per concern #15 |
| `X-Hexalith-Task-Id` | request + response | yes for task-scoped ops | ULID; propagated alongside correlation |
| `X-Hexalith-Retry-As` | response | conditional | Set to `stream` on `413` from `PutFileInline` (per D-9) |
| `X-Hexalith-Freshness` | response | conditional | Reports staleness for snapshot-or-eventually-consistent reads (per C8) |
| Content negotiation | request | yes | `application/json` for inline; `multipart/form-data` for streaming uploads; `application/problem+json` always accepted for errors |

**Error response format (per A-8):**

```json
{
  "type": "https://hexalith.dev/errors/workspace-locked",
  "title": "Workspace is locked by another task",
  "status": 409,
  "category": "workspace_locked",
  "code": "WORKSPACE_LOCKED_BY_OTHER_TASK",
  "message": "Workspace is currently locked. Acquire lock or wait for retry-eligible window.",
  "correlationId": "01htr2k4q9z8x7v6w5n4m3p2j1",
  "retryable": true,
  "clientAction": "wait_and_retry",
  "details": {
    "lockOwnerTaskId": "01htr2k4q9z8x7v6w5n4m3p2j0",
    "lockAgeSeconds": 47,
    "retryEligibleAtUtc": "2026-05-09T14:32:11Z"
  }
}
```

### Communication Patterns

**EventStore identity & topics (per Hexalith.EventStore convention):**

- Aggregate identity: `{tenantId}:{domain}:{aggregateId}` (e.g., `acme-corp:folders:01htr...`); folders never use `system` tenant
- Pub/sub topic: `{tenantId}.{domain}.events` (e.g., `acme-corp.folders.events`, `acme-corp.organizations.events`)
- Tenant subscription topic (consumed): `system.tenants.events` (Hexalith.Tenants only emits on `system`)
- Dead-letter topic: `deadletter.{domain}.events`

**Command and event envelope (per EventStore contracts):**

- Always wrap domain payloads in `CommandEnvelope` / `EventEnvelope` records — never bare payloads
- Envelope metadata required: `tenantId`, `domain`, `aggregateId`, `messageId`, `correlationId`, `causationId`, `timestamp`, `userId`, `eventTypeName`
- Payload is byte-array of UTF-8 JSON; never raw object reference

**Service invocation:**

- Dapr app IDs only, never direct HTTP URLs to sibling services
- App IDs (per I-4): `eventstore`, `tenants`, `folders`, `folders-ui`, `folders-workers`
- Internal calls go through the canonical command/query API (`POST /api/v1/commands`, `POST /api/v1/queries`), never direct aggregate HTTP

**Authorization claims (per S-3, S-4):**

- Principal: JWT `sub` claim
- Tenant: `eventstore:tenant` claim (transformed by Hexalith.EventStore from JWT)
- Permissions: `eventstore:permission` claims (`command:submit`, `query:read`, `commands:*`, `queries:*`)
- Tenant ID in any payload: **input requiring validation**, never authority

**Webhook posture:**

- **NONE in MVP.** Routes that would receive webhooks return `404 Not Found` from the API surface. Architectural invariant per concern (no-webhook MVP posture).
- When introduced post-MVP, tenant-routing model must be designed first (per cross-cutting concern hard product boundaries).

### Process Patterns

**Aggregate purity (per EventStore convention):**

- Aggregate `Handle(Command, State?)` methods are **pure functions** of command + state + envelope metadata
- Return `DomainResult.Success(events)`, `DomainResult.Rejection(reason)`, or `DomainResult.NoOp`
- **FORBIDDEN inside aggregate handlers:** Dapr calls, HTTP calls, file I/O, Git operations, secret-store access, database queries, time-of-day reads (use envelope `timestamp` for determinism), random number generation (use causation ID for determinism)
- All side effects belong in workers / process managers (per I-9)

**Idempotency (per A-9):**

- Every mutating command MUST carry an `Idempotency-Key` header
- Client supplies the key; server canonicalizes using `x-hexalith-idempotency-equivalence` field list (lexicographic order)
- NSwag-generated `ComputeIdempotencyHash()` per command DTO is the single canonicalization implementation; consumers MUST NOT hand-roll equivalence
- Replay with same key + equivalent payload → same logical result
- Replay with same key + different payload → `409 Idempotency-Conflict`
- Idempotency record TTL: `mutation = 24h` / `commit = retention-period(C3)` (per D-7)

**Locking (per concerns #4, #16, C7):**

- Mutation requires a held lock; lock is checked AND auth-revalidated on every mutating call
- Auth revalidation freshness budget defined in C7 (two-number contract: lease-renewal + auth-revalidation intervals)
- Lock release on commit terminal state (or to defined recovery state per C6 transition matrix)
- Stale / abandoned / interrupted locks: deterministic, observable; never silently broken

**Authorization order (per concern #18):**

For any context-query (search / glob / partial-read / file metadata):

1. JWT validation
2. Tenant claim validation
3. Local tenant-access projection check (fail-closed-on-stale)
4. Folder ACL check
5. Path policy check (include / exclude / binary / large-file / range / result-limit)
6. **THEN** execute query
7. Result respects all of the above; sentinel-secret tests iterate `audit-leakage-corpus.json` over results

**Failure handling (per concern #5):**

- Distinguish **known failure** (categorized in PRD error taxonomy: timeout / 4xx / 5xx / branch-protection / etc.) from **unknown outcome** (provider call timed out, response not received, partial state)
- Known failures → return canonical error, retry per `clientAction`
- Unknown outcome → enter `reconciliation_required` state; never retry in a way that could duplicate repositories, file changes, or commits

**Sentinel redaction (per concern #6):**

- Every component that emits a log, trace, metric label, event, audit record, console payload, provider diagnostic, or error response MUST run sentinel tests
- Sentinel corpus: `tests/fixtures/audit-leakage-corpus.json` (normative, version-controlled)
- New sensitive-pattern category requires PR + reviewer sign-off on the corpus
- CI gate fails on any sentinel match

**Cache-key construction (per concern #13, C10):**

- Every cache key — in-process `MemoryCache`, Dapr state, Redis distributed cache — MUST start with `{tenantId}:` prefix
- CI lint check enforces this as a hard build-time gate
- No code may construct a cache key without a tenant scope

**Logging:**

- Structured logs only (no string-formatted message templates with embedded data); Microsoft.Extensions.Logging structured templates with named parameters
- Required structured fields: `tenantId`, `correlationId`, `causationId`, `taskId` (when scoped), `aggregateId` (when scoped), `eventTypeName` (when applicable)
- **Forbidden** as log values: file contents, secrets, provider tokens, raw credential references, anything matching `audit-leakage-corpus.json` patterns
- Log level convention: `Trace` for verbose internals; `Debug` for development; `Information` for lifecycle events; `Warning` for recoverable degradation (e.g., projection lag); `Error` for failure with retry; `Critical` for unrecoverable

**Test patterns (per EventStore convention):**

- **Aggregate tests:** Given prior events / state → When command → Then expected `DomainResult` (events, rejection, or no-op). Use `Hexalith.EventStore.Testing` assertions.
- **Replay tests:** Every event family used in production must replay into state without missing `Apply` paths
- **Tombstone tests:** Terminated aggregates reject subsequent commands (`ITerminatable` compliance)
- **Identity tests:** Tenant / domain / aggregate IDs validate against EventStore storage-key expectations
- **Projection tests:** Ordered event lists build deterministic read models; duplicate delivery is idempotent
- **Conformance tests:** `Hexalith.Folders.Testing` fakes delegate to production aggregate logic (mirrors `TenantConformanceTests`)
- **Parity tests (C13):** Generated from C0 Contract Spine via `parity-contract.yaml`; both `*.Sdk.Tests` and `*.Rest.Tests` consume as xUnit theory data; CI fails if Contract Spine adds a command without a parity row
- **Sentinel tests:** Iterate `tests/fixtures/audit-leakage-corpus.json`; CI fails on any match in audit / projection / log / error output
- **Forgejo contract tests:** Per `tests/contracts/forgejo/<version>/swagger.v1.json`; nightly oasdiff drift job classifies additive (warn) vs breaking (fail)
- **Dapr policy conformance:** Negative-test suite in kind cluster asserts unauthorized `(sourceAppId, targetAppId, operation)` triples receive `403`; CI blocks merge on policy YAML changes without negative test additions

### Enforcement Guidelines

**All implementers (AI agents or humans) MUST:**

1. Follow the C# / domain naming tables above without exception
2. Use camelCase JSON, ISO-8601-Z dates, and the documented header set
3. Wrap all commands and events in EventStore envelopes
4. Carry `Idempotency-Key` on every mutating command and use the generated `ComputeIdempotencyHash()` helper
5. Tenant-prefix every cache key
6. Keep aggregate handlers pure; put side effects in workers
7. Run sentinel-redaction tests against every signal pipeline they touch
8. Carry correlation + task IDs end-to-end through any adapter chain they author
9. Validate authorization in the documented order before executing context queries
10. Treat unknown provider outcomes as `reconciliation_required`, never retry blindly

**Pattern enforcement:**

- **Build-time (lint):** Cache-key tenant-prefix lint (C10); namespace structure check; one-public-type-per-file check
- **Build-time (codegen):** NSwag-generated `ComputeIdempotencyHash()` helpers; parity oracle generation with **per-class completeness assertion** (mutating commands MUST declare `idempotency_key_rule`; query operations MUST declare `read_consistency_class`; missing → generation fails); SDK generation; Contract-Spine validation against server-emitted OpenAPI (BLOCKING per A-3); **NSwag generated-output golden-file gate** (CI re-runs codegen and `git diff --exit-code` on `Hexalith.Folders.Client/Generated/`; PR fails if hand-edits drifted)
- **CI gates:** Unit tests (aggregate + replay + projection + parity + sentinel); contract tests (hermetic + nightly live-drift); Dapr-policy conformance negative tests; provider-rate-limit chaos test; **C6 transition matrix coverage gate** (every `(state, event)` cell asserted by at least one test; CI fails if a state or event is added without coverage); **`tests/fixtures/parity-contract.schema.json` validation gate** (validates `parity-contract.yaml` against schema before tests consume it); **`*.Cli.Tests` + `*.Mcp.Tests` parity oracle consumption** (behavioral-parity columns from C13 oracle: `pre_sdk_error_class`, `idempotency_key_sourcing`, `correlation_id_sourcing`, `cli_exit_code`, `mcp_failure_kind`); **Contract Spine symmetric drift gate** (operations removed without a deprecation entry in `tests/fixtures/previous-spine.yaml` → CI fail); **idempotency encoding-equivalence test** (iterates `tests/fixtures/idempotency-encoding-corpus.json`); **exit-criteria-presence gate** (release pipeline fails when an artifact link is missing for any C0–C13 row in §"Exit Criteria Operations Plan"); **pattern-examples compile gate** (compiles §"Pattern Examples" snippets against pinned package versions to catch API drift)
- **PR review:** New sensitive-pattern category in `audit-leakage-corpus.json` requires reviewer sign-off; new Forgejo `swagger.v1.json` snapshot requires reviewer sign-off; new Dapr policy YAML requires corresponding negative tests in same PR; new parity-oracle row shape column requires schema update in `parity-contract.schema.json` and reviewer sign-off; new C6 state or event requires matrix update + transition tests in same PR

**Pattern updates:**

- Patterns are versioned with the architecture document
- Breaking changes require an architecture-document revision and a migration plan for in-flight code
- Documented exceptions live in the architecture document under a clearly named "Pattern Exceptions" section, never inline in code as ad-hoc justification

### Pattern Examples

**Good:**

```csharp
// Aggregate handler (pure, returns DomainResult, uses envelope timestamp)
public DomainResult Handle(LockWorkspace command, FolderState? state, CommandEnvelope envelope)
{
    if (state is null || state.IsArchived)
        return DomainResult.Rejection("FOLDER_NOT_AVAILABLE", "Folder is archived or does not exist");

    if (state.LockOwner is { } currentOwner && currentOwner.TaskId != command.TaskId)
        return DomainResult.Rejection("WORKSPACE_LOCKED_BY_OTHER_TASK", "Lock held by other task");

    return DomainResult.Success(new[]
    {
        new WorkspaceLocked(
            FolderId: state.FolderId,
            TaskId: command.TaskId,
            LeaseUntilUtc: envelope.Timestamp.AddSeconds(command.LeaseSeconds),
            CorrelationId: envelope.CorrelationId)
    });
}
```

**Anti-patterns to avoid:**

```csharp
// BAD: side effect in aggregate
public DomainResult Handle(WriteFile command, FolderState state, CommandEnvelope envelope)
{
    File.WriteAllBytes(command.Path, command.Content);   // FORBIDDEN — aggregate must be pure
    return DomainResult.Success(new[] { new FileWritten(...) });
}

// BAD: cache key without tenant prefix
_cache.Set($"folder:{folderId}:status", status);   // FORBIDDEN — must be $"{tenantId}:folder:{folderId}:status"

// BAD: trusting payload tenant
public Task CreateFolder(CreateFolderRequest request)
{
    var tenantId = request.TenantId;   // FORBIDDEN — tenant from auth context, not payload
    ...
}

// BAD: hand-rolled idempotency hash
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command)));   // FORBIDDEN — use generated ComputeIdempotencyHash()

// BAD: silent retry on unknown outcome
catch (HttpRequestException) { return await provider.CreateRepositoryAsync(...); }   // FORBIDDEN — unknown outcome → reconciliation_required, no retry

// BAD: file content in event payload
events.Add(new FileWritten(Path: path, Content: bytes));   // FORBIDDEN — event holds metadata + hash + reference, never content

// BAD: silent redaction in console
return $"workspace {Truncate(repoName, 8)} is inaccessible";   // FORBIDDEN — must show lock-icon affordance + "your tenant policy hides this"
```

## Project Structure & Boundaries

### Complete Project Directory Structure

```
Hexalith.Folders/
├── README.md
├── LICENSE
├── CLAUDE.md                                   # exists; project-instruction file
├── AGENTS.md                                   # repo-wide agent instructions
├── .gitignore
├── .gitmodules                                 # root-level submodule references only (no recursive)
├── .editorconfig
├── Directory.Build.props                       # mirror Hexalith.Tenants
├── Directory.Packages.props                    # central package management; pins Hexalith.* 3.15.1
├── Hexalith.Folders.slnx
├── global.json                                 # .NET 10 SDK pin
├── nuget.config
│
├── .github/
│   └── workflows/
│       ├── ci.yml                              # build + format + lint + unit + parity + sentinel
│       ├── contract-tests.yml                  # hermetic provider contract tests (PR gate)
│       ├── nightly-drift.yml                   # live-nightly Forgejo + GitHub drift detection
│       ├── policy-conformance.yml              # dapr-policy-conformance negative tests
│       └── release.yml                         # NuGet publish on tag
│
├── docs/                                       # exists; contains static project docs
│   ├── architecture/                           # generated from architecture.md sections
│   ├── api/                                    # OpenAPI v1 reference (rendered)
│   ├── contract-terms.md                       # FR1–FR3 vocabulary reference
│   ├── runbooks/                               # ops runbooks (incident response, drift recovery)
│   │   └── tenant-deletion.md                  # tenant-deletion lifecycle (authored Phase 4)
│   ├── adrs/                                   # Architecture Decision Records (one per major decision)
│   │   └── 0000-template.md                    # ADR template (authored Phase 0)
│   ├── exit-criteria/                          # C1–C5 decision artifacts (per Exit Criteria Operations Plan)
│   │   ├── _template.md                        # stakeholder-decision template
│   │   ├── c1-capacity.md                      # owner: Architect; authority: PM; deadline: Phase 9 entry
│   │   ├── c2-freshness.md                     # owner: Architect; authority: PM; deadline: Phase 4 exit
│   │   ├── c3-retention.md                     # owner: Tech Lead; authority: Legal+PM; deadline: Phase 1 entry (BLOCKING)
│   │   ├── c4-input-limits.md                  # owner: Architect; authority: PM; deadline: Phase 1 entry (BLOCKING)
│   │   └── c5-scalability-quantifiers.md       # owner: Architect; authority: PM; deadline: Phase 9 entry
│   └── diagrams/                               # workspace lifecycle, lock state, auth flow diagrams
│
├── _bmad-output/                               # exists; planning artifacts (PRD, architecture, research)
├── _bmad/                                      # exists; BMAD installer
│
├── Hexalith.AI.Tools/                          # submodule — root-level only
├── Hexalith.EventStore/                        # submodule — root-level only
├── Hexalith.FrontComposer/                     # submodule — root-level only
├── Hexalith.Tenants/                           # submodule — root-level only
│
├── src/
│   ├── Hexalith.Folders.Contracts/             # C0 Contract Spine + DTOs + extensions
│   │   ├── Hexalith.Folders.Contracts.csproj
│   │   ├── openapi/
│   │   │   ├── hexalith.folders.v1.yaml        # OpenAPI 3.1 — Contract Spine SoT
│   │   │   └── extensions/                     # x-hexalith-* extension schemas
│   │   ├── Folders/
│   │   │   ├── Commands/                       # CreateFolder, RenameFolder, ArchiveFolder, etc.
│   │   │   ├── Events/                         # FolderCreated, FolderRenamed, FolderArchived, etc.
│   │   │   └── Queries/                        # GetFolder, ListFolders, GetFolderAuditTrail
│   │   ├── Organizations/
│   │   │   ├── Commands/                       # CreateOrganization, ConfigureGitProvider, etc.
│   │   │   ├── Events/                         # OrganizationCreated, GitProviderBound, etc.
│   │   │   └── Queries/
│   │   ├── Workspaces/
│   │   │   ├── Commands/                       # PrepareWorkspace, LockWorkspace, ReleaseWorkspaceLock
│   │   │   ├── Events/                         # WorkspacePrepared, WorkspaceLocked, WorkspaceReleased
│   │   │   └── Queries/                        # GetWorkspaceStatus
│   │   ├── Files/
│   │   │   ├── Commands/                       # AddFile, ChangeFile, RemoveFile (PutFileInline + PutFileStream)
│   │   │   ├── Events/                         # FileAdded, FileChanged, FileRemoved
│   │   │   └── Queries/                        # ListFolderFiles, SearchFolderFiles, ReadFileRange, GetFileMetadata
│   │   ├── Commits/
│   │   │   ├── Commands/                       # CommitWorkspace
│   │   │   └── Events/                         # WorkspaceCommitted, CommitFailed, ReconciliationRequired
│   │   ├── Providers/
│   │   │   ├── Commands/                       # ValidateProviderReadiness, BindRepository
│   │   │   ├── Events/                         # ProviderReadinessValidated, RepositoryBound
│   │   │   └── Queries/                        # GetProviderReadiness, GetProviderCapabilities
│   │   ├── Audit/
│   │   │   └── Queries/                        # GetAuditTrail
│   │   ├── Errors/
│   │   │   ├── ErrorCategories.cs              # canonical category enum
│   │   │   ├── ErrorCodes.cs                   # stable code constants
│   │   │   └── ProblemDetailsTypes.cs          # RFC 9457 type URI constants
│   │   ├── Identity/
│   │   │   ├── TenantId.cs                     # value type
│   │   │   ├── FolderId.cs                     # opaque ULID wrapper
│   │   │   ├── OrganizationId.cs
│   │   │   ├── WorkspaceId.cs
│   │   │   ├── TaskId.cs
│   │   │   └── CorrelationId.cs
│   │   └── Projections/                        # read-model DTOs
│   │       ├── WorkspaceStatusProjection.cs
│   │       ├── FolderListProjection.cs
│   │       ├── ProviderReadinessProjection.cs
│   │       └── AuditProjection.cs
│   │
│   ├── Hexalith.Folders/                       # core domain: aggregates, state, projections, providers, idempotency, authz
│   │   ├── Hexalith.Folders.csproj
│   │   ├── Aggregates/
│   │   │   ├── Folder/
│   │   │   │   ├── FolderAggregate.cs          # FR11–FR14, FR24–FR42 (folder + workspace + file ops)
│   │   │   │   ├── FolderState.cs
│   │   │   │   ├── FolderStateApply.cs         # all event Apply methods
│   │   │   │   └── FolderStateTransitions.cs   # C6 total transition matrix implementation
│   │   │   └── Organization/
│   │   │       ├── OrganizationAggregate.cs    # FR15–FR23 (provider readiness, repo binding, ACL baseline)
│   │   │       ├── OrganizationState.cs
│   │   │       └── OrganizationStateApply.cs
│   │   ├── Authorization/
│   │   │   ├── TenantAccessAuthorizer.cs       # FR4, FR8–FR10 (layered authz)
│   │   │   ├── FolderAclAuthorizer.cs          # FR5–FR6 (grant + effective permissions)
│   │   │   ├── PathPolicyAuthorizer.cs         # FR33, FR35 (path security + context-query policy)
│   │   │   └── AuthorizationOrder.cs           # canonical order enforcement (concern #18)
│   │   ├── Projections/
│   │   │   ├── TenantAccess/                   # FR4 (local fail-closed projection)
│   │   │   │   ├── FolderTenantAccessProjection.cs
│   │   │   │   └── FolderTenantAccessHandler.cs
│   │   │   ├── WorkspaceStatus/                # FR43–FR46, FR52 (status surface)
│   │   │   │   ├── WorkspaceStatusProjection.cs
│   │   │   │   └── WorkspaceStatusHandler.cs
│   │   │   ├── ProviderReadiness/              # FR17, FR23
│   │   │   │   ├── ProviderReadinessProjection.cs
│   │   │   │   └── ProviderReadinessHandler.cs
│   │   │   ├── FolderList/                     # FR12 (folder lifecycle visibility)
│   │   │   │   ├── FolderListProjection.cs
│   │   │   │   └── FolderListHandler.cs
│   │   │   └── Audit/                          # FR53–FR57 (metadata-only audit projection)
│   │   │       ├── AuditProjection.cs
│   │   │       └── AuditEventHandler.cs
│   │   ├── Providers/                          # provider port + adapters
│   │   │   ├── Abstractions/
│   │   │   │   ├── IGitProvider.cs             # capability-discoverable port (n-provider model)
│   │   │   │   ├── ProviderCapabilities.cs
│   │   │   │   ├── ProviderReadiness.cs
│   │   │   │   └── ProviderFailureCategory.cs  # taxonomy per concern #5
│   │   │   ├── GitHub/
│   │   │   │   ├── GitHubProvider.cs           # uses Octokit 14.0.0
│   │   │   │   ├── GitHubCapabilities.cs       # GitHub Apps fine-grained permissions
│   │   │   │   ├── GitHubReadinessChecker.cs
│   │   │   │   └── GitHubFailureClassifier.cs
│   │   │   └── Forgejo/
│   │   │       ├── ForgejoProvider.cs          # typed HttpClient wrapper per A-7
│   │   │       ├── ForgejoCapabilities.cs      # scoped tokens
│   │   │       ├── ForgejoReadinessChecker.cs
│   │   │       └── ForgejoFailureClassifier.cs
│   │   ├── Queries/
│   │   │   └── Context/                        # FR34–FR35 (tree, search, glob, partial reads)
│   │   │       ├── FolderTreeQueryHandler.cs
│   │   │       ├── FolderSearchQueryHandler.cs
│   │   │       ├── FolderGlobQueryHandler.cs
│   │   │       └── FileRangeQueryHandler.cs
│   │   ├── Idempotency/                        # FR41–FR42, A-9
│   │   │   ├── IdempotencyKeyValidator.cs
│   │   │   ├── PayloadEquivalence.cs
│   │   │   └── IdempotencyRecordStore.cs       # Dapr state with two-tier TTL (D-7)
│   │   ├── Caching/
│   │   │   └── TenantPrefixedCacheKey.cs       # C10 mandatory prefix helper
│   │   ├── Redaction/                          # concern #6 sentinel-redaction support
│   │   │   ├── SensitiveMetadataClassifier.cs  # C9 classification per S-6
│   │   │   └── RedactingFormatter.cs
│   │   └── Registration/
│   │       └── HexalithFoldersDomainServiceExtensions.cs   # AddEventStore() registration for OrganizationAggregate + FolderAggregate
│   │
│   ├── Hexalith.Folders.Server/                # canonical REST transport host
│   │   ├── Hexalith.Folders.Server.csproj
│   │   ├── Program.cs                          # AddEventStore(), UseEventStore(), MapPost("/process"), MapPost("/project")
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Endpoints/
│   │   │   ├── ProviderReadinessEndpoints.cs   # /api/v1/provider-readiness
│   │   │   ├── FolderEndpoints.cs              # /api/v1/folders/...
│   │   │   ├── WorkspaceEndpoints.cs           # /api/v1/folders/{id}/workspace/...
│   │   │   ├── FileEndpoints.cs                # PutFileInline + PutFileStream (D-9 bimodal)
│   │   │   ├── CommitEndpoints.cs              # /api/v1/folders/{id}/workspace/commits
│   │   │   ├── ContextQueryEndpoints.cs        # tree, search, glob, partial-reads
│   │   │   ├── AuditEndpoints.cs
│   │   │   └── OpsConsoleEndpoints.cs          # read-only projection queries
│   │   ├── Middleware/
│   │   │   ├── CorrelationPropagationMiddleware.cs    # concern #15
│   │   │   ├── TenantContextProvenanceMiddleware.cs   # concern #12 — auth context only
│   │   │   ├── IdempotencyKeyMiddleware.cs
│   │   │   └── ProblemDetailsErrorMiddleware.cs       # RFC 9457 mapping
│   │   ├── DomainServices/
│   │   │   ├── ProcessEndpoint.cs              # /process for DomainServiceRequest
│   │   │   └── ProjectEndpoint.cs              # /project for ProjectionRequest
│   │   └── HealthChecks/
│   │       ├── LivenessCheck.cs
│   │       ├── ReadinessCheck.cs
│   │       └── TenantsAvailabilityCheck.cs
│   │
│   ├── Hexalith.Folders.Client/                # canonical SDK
│   │   ├── Hexalith.Folders.Client.csproj
│   │   ├── Generated/                          # NSwag output — DO NOT edit by hand
│   │   │   ├── HexalithFoldersClient.cs
│   │   │   ├── DataContracts.cs
│   │   │   └── ComputeIdempotencyHash.cs       # generated per-command helpers
│   │   ├── Convenience/
│   │   │   └── UploadFileAsync.cs              # hand-written hybrid for D-9 (W4+A4 reconciliation)
│   │   ├── Subscription/
│   │   │   └── TenantEventSubscriptionEndpoints.cs    # MapTenantEventSubscription per Tenants research
│   │   ├── Registration/
│   │   │   └── HexalithFoldersServiceCollectionExtensions.cs   # AddHexalithFolders() + AddHexalithTenants()
│   │   └── Resilience/
│   │       └── DaprResiliencyOptions.cs        # Dapr resiliency policy bindings
│   │
│   ├── Hexalith.Folders.Cli/                   # FR48 — CLI adapter
│   │   ├── Hexalith.Folders.Cli.csproj
│   │   ├── Program.cs                          # System.CommandLine 2.x root
│   │   ├── Commands/
│   │   │   ├── ProviderCommands.cs             # readiness validate
│   │   │   ├── FolderCommands.cs               # create, list, archive, get
│   │   │   ├── WorkspaceCommands.cs            # prepare, lock, release, status
│   │   │   ├── FileCommands.cs                 # add, change, remove (auto-picks transport)
│   │   │   ├── CommitCommands.cs
│   │   │   ├── ContextCommands.cs              # tree, search, glob, read
│   │   │   └── AuditCommands.cs
│   │   ├── Output/
│   │   │   ├── JsonFormatter.cs
│   │   │   └── HumanFormatter.cs               # operator-disposition labels per F-4
│   │   └── ExitCodes.cs                        # mapped to canonical error categories
│   │
│   ├── Hexalith.Folders.Mcp/                   # FR49 — MCP server adapter
│   │   ├── Hexalith.Folders.Mcp.csproj
│   │   ├── Program.cs                          # ModelContextProtocol 1.3.0 server bootstrap
│   │   ├── Tools/
│   │   │   ├── PrepareWorkspaceTool.cs
│   │   │   ├── LockWorkspaceTool.cs
│   │   │   ├── WriteFileTool.cs                # auto-picks transport
│   │   │   ├── CommitWorkspaceTool.cs
│   │   │   ├── ReadFileTool.cs
│   │   │   ├── SearchFolderTool.cs
│   │   │   └── GetWorkspaceStatusTool.cs
│   │   ├── Resources/
│   │   │   ├── FolderTreeResource.cs
│   │   │   └── AuditTrailResource.cs
│   │   └── Manifest/
│   │       └── server-manifest.json            # MCP server descriptor
│   │
│   ├── Hexalith.Folders.UI/                    # FR52 — read-only ops console (Blazor Server + Fluent UI)
│   │   ├── Hexalith.Folders.UI.csproj
│   │   ├── Program.cs
│   │   ├── App.razor
│   │   ├── _Imports.razor
│   │   ├── wwwroot/
│   │   │   ├── css/
│   │   │   ├── js/
│   │   │   └── icons/                          # lock-icon for redaction affordance F-5
│   │   ├── Layout/
│   │   │   ├── MainLayout.razor
│   │   │   └── DegradedModeBanner.razor        # F-6 guardrail (1) — persistent red banner
│   │   ├── Pages/
│   │   │   ├── Index.razor                     # tenant + folder selector
│   │   │   ├── Folders.razor                   # folder list per tenant
│   │   │   ├── FolderDetail.razor              # workspace status, lock state, last commit
│   │   │   ├── ProviderHealth.razor            # provider readiness across tenants
│   │   │   ├── AuditTrail.razor                # incident reconstruction
│   │   │   └── _Admin/
│   │   │       └── IncidentStream.razor        # F-6 last-resort read path /_admin/incident-stream
│   │   ├── Components/
│   │   │   ├── OperatorDispositionBadge.razor  # F-4 primary-visual label
│   │   │   ├── TechnicalStateMetadata.razor    # F-4 secondary metadata
│   │   │   ├── RedactedField.razor             # F-5 lock-icon affordance
│   │   │   ├── CorrelationCopyButton.razor     # F-6 guardrail (3) — copy correlationId+timestamp
│   │   │   ├── SkeletonState.razor             # F-7 perceived-wait at 400ms
│   │   │   └── StillLoadingCancel.razor        # F-7 perceived-wait at 2s
│   │   └── Services/
│   │       ├── FoldersClientFacade.cs          # wraps Hexalith.Folders.Client SDK
│   │       └── DispositionLabelMapper.cs       # state → operator-disposition label
│   │
│   ├── Hexalith.Folders.Workers/               # process managers / reconcilers
│   │   ├── Hexalith.Folders.Workers.csproj
│   │   ├── Program.cs
│   │   ├── WorkspaceWorkflows/
│   │   │   ├── WorkspacePreparationWorkflow.cs # reacts to FolderGitRepositoryBound
│   │   │   ├── WorkspaceCleanupWorkflow.cs
│   │   │   └── WorkingCopyManager.cs           # D-8 ephemeral working-copy lifecycle
│   │   ├── RepositoryWorkflows/
│   │   │   ├── RepositoryProvisioningWorkflow.cs   # reacts to FolderGitRepositoryRequested
│   │   │   └── RepositoryReconciler.cs         # handles unknown_provider_outcome
│   │   ├── CommitWorkflows/
│   │   │   ├── CommitWorkflow.cs
│   │   │   └── CommitReconciler.cs
│   │   ├── RateLimiting/
│   │   │   ├── PerTenantTokenBucket.cs         # I-8 per-tenant for user-driven calls
│   │   │   └── GlobalReconciliationBucket.cs   # I-8 global for background work
│   │   └── Tenants/
│   │       └── TenantEventHandlers/            # Folder reactions to Tenants events
│   │           ├── TenantDisabledHandler.cs
│   │           ├── UserRemovedFromTenantHandler.cs
│   │           ├── UserRoleChangedHandler.cs
│   │           └── TenantConfigurationSetHandler.cs    # processes folders.* keys only
│   │
│   ├── Hexalith.Folders.Aspire/                # AppHost helper extensions
│   │   ├── Hexalith.Folders.Aspire.csproj
│   │   └── HexalithFoldersExtensions.cs        # AddHexalithFolders() Aspire extension
│   │
│   ├── Hexalith.Folders.AppHost/               # .NET Aspire AppHost
│   │   ├── Hexalith.Folders.AppHost.csproj
│   │   ├── Program.cs                          # composes EventStore + Tenants + Folders.Server + Workers + UI + Keycloak
│   │   ├── DaprComponents/
│   │   │   ├── statestore.yaml                 # Redis state store
│   │   │   ├── pubsub.yaml                     # Redis Streams pub/sub
│   │   │   ├── resiliency.yaml                 # retry/circuit-breaker policies
│   │   │   └── accesscontrol.yaml              # local dev: defaultAction allow
│   │   └── appsettings.json
│   │
│   ├── Hexalith.Folders.ServiceDefaults/       # shared service-defaults
│   │   ├── Hexalith.Folders.ServiceDefaults.csproj
│   │   └── ServiceDefaultsExtensions.cs        # OpenTelemetry, health checks, Dapr resilience
│   │
│   └── Hexalith.Folders.Testing/               # in-memory fakes + builders + conformance
│       ├── Hexalith.Folders.Testing.csproj
│       ├── Fakes/
│       │   ├── InMemoryFolderService.cs        # delegates to production aggregate logic
│       │   └── InMemoryProviderAdapter.cs      # for fast tests without GitHub/Forgejo
│       ├── Builders/
│       │   ├── FolderStateBuilder.cs
│       │   ├── CommandEnvelopeBuilder.cs
│       │   └── EventEnvelopeBuilder.cs
│       ├── Assertions/
│       │   └── FolderDomainAssertions.cs       # mirrors Hexalith.EventStore.Testing pattern
│       └── Compliance/
│           ├── FolderConformanceTests.cs       # reflection-discovered command/event coverage
│           └── ProviderPortConformanceTests.cs # provider port contract assertions
│
├── tests/
│   ├── Hexalith.Folders.Contracts.Tests/
│   ├── Hexalith.Folders.Tests/                 # aggregate + replay + projection unit tests
│   │   ├── Aggregates/
│   │   │   ├── Folder/
│   │   │   └── Organization/
│   │   ├── Authorization/
│   │   ├── Idempotency/
│   │   ├── Caching/
│   │   ├── Providers/
│   │   │   ├── GitHub/
│   │   │   └── Forgejo/
│   │   └── Projections/
│   ├── Hexalith.Folders.Server.Tests/          # endpoint + middleware tests
│   ├── Hexalith.Folders.Client.Tests/          # SDK + parity tests (consume parity-contract.yaml)
│   ├── Hexalith.Folders.Cli.Tests/             # CLI parity tests (consume parity-contract.yaml)
│   ├── Hexalith.Folders.Mcp.Tests/             # MCP parity tests (consume parity-contract.yaml)
│   ├── Hexalith.Folders.UI.Tests/              # bUnit Blazor component tests
│   ├── Hexalith.Folders.Workers.Tests/
│   ├── Hexalith.Folders.Testing.Tests/         # conformance tests for the testing-fakes library
│   ├── Hexalith.Folders.IntegrationTests/      # Aspire/Dapr E2E
│   │   ├── EndToEnd/
│   │   ├── DaprPolicyConformance/              # negative-test suite per I-3 + M4
│   │   └── ProviderRateLimitChaos/             # chaos test per I-8 + M5
│   ├── contracts/
│   │   ├── forgejo/
│   │   │   ├── supported-versions.json         # test matrix manifest per A-7
│   │   │   ├── v15.0/swagger.v1.json
│   │   │   ├── v14.0/swagger.v1.json
│   │   │   └── v13.0/swagger.v1.json
│   │   └── github/                             # Octokit-version snapshots for regression
│   │       └── 14.0.0/openapi-snapshot.json
│   ├── fixtures/
│   │   ├── audit-leakage-corpus.json           # normative sentinel corpus per concern #6 + A2
│   │   ├── parity-contract.yaml                # generated from C0 Contract Spine; consumed by SDK + REST + CLI + MCP tests
│   │   ├── parity-contract.schema.json         # JSON Schema for parity-contract.yaml row shape (transport + behavioral columns)
│   │   ├── previous-spine.yaml                 # baseline copy of last released Contract Spine; symmetric drift gate input
│   │   └── idempotency-encoding-corpus.json    # NFC/NFD/NFKC/NFKD/zero-width-joiner/ULID-case variants for ComputeIdempotencyHash tests
│   ├── load/                                   # capacity-test harness (NBomber); pinned target hardware profile per C1 artifact
│   │   ├── Hexalith.Folders.LoadTests.csproj
│   │   └── Scenarios/                          # workspace prepare → lock → mutate → commit at concurrency profiles
│   └── tools/
│       ├── oasdiff/                            # config for nightly Forgejo schema-diff job
│       ├── policy-conformance/                 # property-based generator for I-3 negative tests
│       └── parity-oracle-generator/            # generates parity-contract.yaml from Contract Spine; runs class-completeness assertion
│
└── samples/
    ├── Hexalith.Folders.Sample/                # end-to-end demo: chatbot creates Git-backed folder, edits files, commits
    │   ├── Hexalith.Folders.Sample.csproj
    │   └── Program.cs
    └── Hexalith.Folders.Sample.Tests/
```

### Architectural Boundaries

**API Boundaries:**

- **External canonical surface:** `/api/v1/...` REST endpoints in `Hexalith.Folders.Server.Endpoints.*` (8 capability groups per PRD §"Endpoint Specifications": provider-readiness, folders, workspaces, files, commits, audit, ops-console, context queries)
- **Internal command/query surface (per EventStore convention):** `/process` and `/project` endpoints invoked by EventStore via Dapr service invocation; never called by external clients
- **MCP tools surface:** `Hexalith.Folders.Mcp.Tools.*` exposing one tool per canonical command/query, all wrapping `Hexalith.Folders.Client`
- **CLI surface:** `Hexalith.Folders.Cli.Commands.*` mirroring REST capability groups, wrapping the SDK

**Component Boundaries:**

- **`Hexalith.Folders.Contracts`** owns the C0 Contract Spine (OpenAPI 3.1 + extension vocabulary) and all DTOs. NO behavior; no Hexalith.EventStore reference.
- **`Hexalith.Folders`** owns the domain model (aggregates + state + projections + provider adapters + authorization + idempotency + caching + redaction). References Contracts only.
- **`Hexalith.Folders.Server`** is the REST transport host + EventStore domain-service host. References core domain.
- **`Hexalith.Folders.Client`** is the canonical SDK + tenant-event subscription wiring. References Contracts only (consumed by Server, CLI, MCP, UI).
- **`Hexalith.Folders.Workers`** owns process managers / reconcilers / rate-limit buckets / tenant-event handlers. References core domain.
- **`Hexalith.Folders.UI`** is the read-only ops console. References Client only.
- **Provider adapters** (`Hexalith.Folders.Providers.{GitHub,Forgejo}`) implement `IGitProvider` port and may not be referenced from outside the core domain — capability-discoverable, swappable.

**Service Boundaries (Dapr app IDs):**

- `eventstore` — sibling Hexalith.EventStore (existing)
- `tenants` — sibling Hexalith.Tenants (existing)
- `folders` — `Hexalith.Folders.Server` (REST + EventStore domain-service host)
- `folders-ui` — `Hexalith.Folders.UI` (read-only console)
- `folders-workers` — `Hexalith.Folders.Workers` (process managers)

**Data Boundaries:**

- **EventStore (write side):** owns `folders` and `organizations` aggregate streams, snapshots, command status, event publication
- **Hexalith.Folders projections (read side):** owns workspace status, folder list, provider readiness, audit, tenant-access local projection — Dapr state-store backed
- **Working copy storage (D-8):** ephemeral filesystem; never authoritative; not in EventStore
- **Provider state:** owned by GitHub/Forgejo; never duplicated as Hexalith.Folders authoritative state — only referenced via provider URLs / IDs in events

### Requirements to Structure Mapping

| FR Block | Lives In |
|---|---|
| **FR1–FR3** Capability Contract Terms | `docs/contract-terms.md`; OpenAPI 3.1 in `src/Hexalith.Folders.Contracts/openapi/` |
| **FR4–FR10** Authorization & Tenant Boundary | `src/Hexalith.Folders/Authorization/`; `src/Hexalith.Folders/Projections/TenantAccess/`; `src/Hexalith.Folders.Client/Subscription/`; `src/Hexalith.Folders.Server/Middleware/TenantContextProvenanceMiddleware.cs` |
| **FR11–FR14** Folder Lifecycle | `src/Hexalith.Folders/Aggregates/Folder/`; `src/Hexalith.Folders.Contracts/Folders/{Commands,Events,Queries}/` |
| **FR15–FR23** Provider Readiness & Repository Binding | `src/Hexalith.Folders/Aggregates/Organization/`; `src/Hexalith.Folders/Providers/{Abstractions,GitHub,Forgejo}/`; `src/Hexalith.Folders.Contracts/{Organizations,Providers}/` |
| **FR24–FR31** Workspace & Lock Lifecycle | `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` (C6 matrix); `src/Hexalith.Folders.Contracts/Workspaces/`; `src/Hexalith.Folders.Workers/WorkspaceWorkflows/` |
| **FR32–FR36** File Operations & Context Queries | `src/Hexalith.Folders/Aggregates/Folder/` (file commands); `src/Hexalith.Folders/Queries/Context/`; `src/Hexalith.Folders/Authorization/PathPolicyAuthorizer.cs`; `src/Hexalith.Folders.Server/Endpoints/{File,ContextQuery}Endpoints.cs` |
| **FR37–FR42** Commit, Evidence, Idempotency | `src/Hexalith.Folders/Aggregates/Folder/` (commit command); `src/Hexalith.Folders.Workers/CommitWorkflows/`; `src/Hexalith.Folders/Idempotency/` |
| **FR43–FR46** Error, Status, Diagnostics | `src/Hexalith.Folders.Contracts/Errors/`; `src/Hexalith.Folders.Server/Middleware/ProblemDetailsErrorMiddleware.cs`; `src/Hexalith.Folders/Projections/WorkspaceStatus/` |
| **FR47** Versioned canonical REST | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; `src/Hexalith.Folders.Server/Endpoints/` |
| **FR48** CLI parity | `src/Hexalith.Folders.Cli/`; parity tests in `tests/Hexalith.Folders.Cli.Tests/` consuming `tests/fixtures/parity-contract.yaml` |
| **FR49** MCP parity | `src/Hexalith.Folders.Mcp/`; parity tests in `tests/Hexalith.Folders.Mcp.Tests/` |
| **FR50** SDK parity | `src/Hexalith.Folders.Client/`; parity tests in `tests/Hexalith.Folders.Client.Tests/` |
| **FR51** Cross-surface equivalence | `tests/fixtures/parity-contract.yaml` (C13 oracle, generated from Contract Spine); `tests/Hexalith.Folders.IntegrationTests/EndToEnd/` |
| **FR52** Read-only ops console | `src/Hexalith.Folders.UI/Pages/`, `Components/`, `Layout/`; `src/Hexalith.Folders.Server/Endpoints/OpsConsoleEndpoints.cs` |
| **FR53–FR57** Audit and Ops Visibility | `src/Hexalith.Folders/Projections/Audit/`; `src/Hexalith.Folders.Server/Endpoints/AuditEndpoints.cs`; `src/Hexalith.Folders/Redaction/` |

**Cross-Cutting Concerns:**

| Concern (from Step 2) | Lives In |
|---|---|
| #1 Tenant isolation enforcement | `Authorization/`, `Caching/TenantPrefixedCacheKey.cs`, `Server/Middleware/TenantContextProvenanceMiddleware.cs`, lint rules in `.github/workflows/ci.yml` |
| #6 Metadata-only audit + sentinel redaction | `Redaction/`, `tests/fixtures/audit-leakage-corpus.json`, sentinel-test runners across all `*.Tests/` |
| #11 Operations console boundary | `Hexalith.Folders.UI/Pages/_Admin/IncidentStream.razor`; `Components/{DegradedModeBanner,SkeletonState,StillLoadingCancel}.razor` |
| #13 Cache-key tenant prefix invariant | `Hexalith.Folders/Caching/TenantPrefixedCacheKey.cs` + CI lint job in `.github/workflows/ci.yml` |
| #14 Aggregate ID opacity | `Hexalith.Folders.Contracts/Identity/{Folder,Organization,Workspace}Id.cs` (opaque ULID wrappers) |
| #15 Correlation propagation invariant | `Server/Middleware/CorrelationPropagationMiddleware.cs`; parity tests in `tests/fixtures/parity-contract.yaml` |
| #16 Mid-task authorization revocation | `Authorization/TenantAccessAuthorizer.cs` (revalidation logic); `Workers/Tenants/TenantEventHandlers/` (revocation handlers) |
| #17 Sensitive metadata classification | `Redaction/SensitiveMetadataClassifier.cs` |
| #18 Context-query authorization order | `Authorization/AuthorizationOrder.cs`; enforced by `Queries/Context/*Handler.cs` |
| #19 Read consistency model | `Server/Endpoints/*Endpoints.cs` (per-query-family freshness header emission) |
| #20 Tenants-availability degraded mode | `Hexalith.Folders.Client/Subscription/`; `Server/HealthChecks/TenantsAvailabilityCheck.cs`; `Authorization/TenantAccessAuthorizer.cs` |
| #21 Operational state durability classification | `Idempotency/IdempotencyRecordStore.cs` (durable); `Workers/WorkspaceWorkflows/WorkingCopyManager.cs` (acceptably-lost); inline cache (rebuild-from-events) |

### Integration Points

**Internal communication:**

- **External REST → EventStore command API:** clients submit `POST /api/v1/commands` envelopes; EventStore validates auth, persists, and invokes Folders' `/process` endpoint via Dapr service invocation
- **EventStore → Folders domain service:** Dapr service invocation, app ID `folders`, method `process` (for commands) and `project` (for projections)
- **Folders → Workers:** event-driven via Dapr pub/sub topic `{tenantId}.folders.events`; Workers subscribe and submit follow-up commands via EventStore command API
- **Tenants → Folders:** Folders subscribes to `system.tenants.events` via Dapr pub/sub; updates local fail-closed tenant-access projection
- **UI → Server:** Blazor Server SignalR connection consumes `Hexalith.Folders.Client` SDK; reads only from projection endpoints

**External integrations:**

- **GitHub:** via Octokit 14.0.0 inside `Hexalith.Folders.Providers.GitHub.GitHubProvider`
- **Forgejo:** via typed HttpClient wrapper inside `Hexalith.Folders.Providers.Forgejo.ForgejoProvider`; per-version `swagger.v1.json` snapshots in `tests/contracts/forgejo/`
- **OIDC IDP:** Keycloak local; pluggable production provider — JWT validation in `Hexalith.Folders.Server` middleware
- **Observability:** OpenTelemetry SDK exports OTLP to Aspire-managed collector locally; pluggable production exporters (Jaeger / Tempo / App Insights / Datadog)

**Data flow (canonical task lifecycle):**

```
Client (CLI / MCP / SDK / direct REST)
  │
  ├─► POST /api/v1/commands (EventStore Command API)
  │     │
  │     └─► EventStore validates auth + envelope
  │           │
  │           └─► Dapr service invocation → Folders.Server /process
  │                 │
  │                 └─► OrganizationAggregate or FolderAggregate.Handle()
  │                       returns DomainResult.Success(events)
  │                 │
  │                 └─► EventStore persists events + publishes to {tenant}.folders.events
  │                       │
  │                       ├─► Workers subscribe → perform external Git work → submit follow-up commands
  │                       │
  │                       ├─► Projections subscribe → update read models
  │                       │     ├─► WorkspaceStatusProjection
  │                       │     ├─► AuditProjection
  │                       │     └─► FolderListProjection
  │                       │
  │                       └─► UI SignalR notification → live status update
  │
  └─◄ 202 Accepted + correlationId for status polling
```

### File Organization Patterns

**Configuration:**

- Solution-level: `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nuget.config`, `.editorconfig` (all at repo root)
- Per-project `appsettings.{env}.json` for runtime config; environment overrides via `appsettings.{Environment}.json`
- Dapr components in `src/Hexalith.Folders.AppHost/DaprComponents/` for local; production policies maintained outside repo per ops runbook
- Secrets: never in source; references via Dapr secret store

**Source organization:**

- Concept-area folders (per Step 5 patterns): `Aggregates/{Concept}/`, `Projections/{Concept}/`, `Providers/{ProviderName}/`, `Workers/{ConceptWorkflows}/`
- One public type per file; nested private types allowed
- File-scoped namespaces

**Test organization:**

- Test projects mirror `src/` 1:1
- Sub-organization within tests follows the source structure (`tests/Hexalith.Folders.Tests/Aggregates/Folder/`)
- Cross-component tests in `tests/Hexalith.Folders.IntegrationTests/`
- Shared fixtures in `tests/fixtures/` (normative); per-project fixtures in `tests/{Project}.Tests/Fixtures/`

**Asset organization:**

- UI static assets in `src/Hexalith.Folders.UI/wwwroot/`
- Diagrams and renders in `docs/diagrams/`
- ADRs (one file per major decision) in `docs/adrs/`

### Development Workflow Integration

**Development server structure:**

- `dotnet run --project src/Hexalith.Folders.AppHost` starts the full Aspire topology (EventStore + Tenants + Folders.Server + Folders.Workers + Folders.UI + Keycloak + Dapr sidecars + Redis)
- Aspire dashboard at `https://localhost:17000` exposes service health, logs, traces, metrics
- Hot-reload supported on all `dotnet`-hosted projects

**Build process structure:**

- `dotnet build Hexalith.Folders.slnx` builds all projects with central package versions from `Directory.Packages.props`
- NSwag SDK generation runs as a build target in `Hexalith.Folders.Client.csproj` (input: `Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`)
- Parity oracle generation runs as a build target producing `tests/fixtures/parity-contract.yaml`
- Contract Spine validation against server-emitted OpenAPI runs as a CI gate (BLOCKING per A-3)

**Deployment structure:**

- One container image per service: `hexalith-folders-server`, `hexalith-folders-workers`, `hexalith-folders-ui`
- Each image deploys with a Dapr sidecar container; app IDs preserved across environments per I-4
- Production Dapr access-control YAML maintained in a separate ops repository, validated by `dapr-policy-conformance` job before promotion (per I-3 + M4)
- NuGet packages for `Hexalith.Folders.Contracts`, `Hexalith.Folders.Client`, `Hexalith.Folders.Aspire`, `Hexalith.Folders.Testing` published on tagged release

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:** All technology pins (.NET 10; Hexalith.EventStore/Tenants 3.15.1 verified on NuGet 2026-05-09; Aspire 13.3.0; CommunityToolkit.Aspire.Hosting.Dapr 13.0.0; NSwag; Octokit 14.0.0; ModelContextProtocol 1.3.0; System.CommandLine 2.x; Microsoft.FluentUI.AspNetCore.Components; Microsoft.AspNetCore.Authentication.JwtBearer per S-2) are coherent and version-compatible. The SDK-as-canonical reframe collapses cross-surface *transport* parity from C(4,2)=6 pairs to one (SDK-vs-REST); the *behavioral* parity dimensions (pre-SDK errors, post-SDK error projection, side-channel parameter sourcing) remain per-adapter and are pinned by the new §"Adapter Parity Contract." D-9 bimodal file transport (PutFileInline ≤256KB, PutFileStream multipart, UploadFileAsync convenience helper) resolves C11 without breaking OpenAPI generation or parity tests; D-7's two-tier idempotency TTL (mutation=24h, commit=C3) bounds the test matrix without sacrificing reconciliation correctness.

**Pattern Consistency:** Naming (PascalCase types, camelCase JSON/parameters, ULID identifiers, kebab-case tenant IDs), domain conventions (`{Concept}Aggregate`, `{Concept}State`, `{Verb}{Concept}` commands, `{Concept}{Verbed}` events, `{Concept}Projection`), JSON wire format (camelCase, ISO-8601-Z, string enums, no inline content), and HTTP header set are uniform across REST, SDK, CLI, MCP. Aggregate purity (I-9) + worker process-manager pattern + sentinel-redaction (corpus + tests) + cache-key tenant-prefix (C10 lint) + new C6 transition-matrix coverage gate + new symmetric-drift gate form an internally consistent enforcement web at build-time, codegen, CI-gate, and PR-review tiers.

**Structure Alignment:** Project layout mirrors Hexalith.Tenants baseline; surface adapters mirror Hexalith.EventStore.Admin.{Cli,Mcp,UI}; tests/ mirrors src/ 1:1; concept-area subfolders match Step 5 patterns; normative shared fixtures (`tests/fixtures/audit-leakage-corpus.json`, `tests/fixtures/parity-contract.yaml`, `tests/fixtures/parity-contract.schema.json`, `tests/fixtures/previous-spine.yaml`, `tests/fixtures/idempotency-encoding-corpus.json`, `tests/contracts/forgejo/<version>/swagger.v1.json`) are placed where every test project can consume them. Component boundaries (Contracts has no behavior; Client references Contracts only; UI references Client only; provider adapters never referenced from outside core) are mechanically enforceable via project references. New `tests/load/` and `docs/exit-criteria/` directories close the previously implicit gaps in capacity testing and exit-criteria ownership.

### Requirements Coverage Validation ✅

**Functional Requirements Coverage:** Every FR block (FR1–FR57 across 11 capability groups) maps to a concrete file/folder destination via §"Requirements to Structure Mapping." See Step 6 for the full mapping table; no FR block is unmapped.

**Non-Functional Requirements Coverage:** Every NFR category (security/tenant isolation, reliability/idempotency/failure visibility, performance/query bounds, scalability/capacity, integration/contract compatibility, observability/auditability/replay, data retention and cleanup, operations console accessibility, verification expectations) is bound to specific architectural decisions and at least one CI gate or runbook. The post-elicitation additions reinforce coverage:

- **Security & Tenant Isolation:** S-2 OIDC parameters frozen (clock skew, JWKS TTL, audience/issuer pinning); S-6 sensitive-metadata classification; layered authorization (S-3/S-4); cache-key tenant-prefix lint (C10); Dapr deny-by-default with negative-test conformance (I-3).
- **Reliability/Idempotency:** A-9 generated `ComputeIdempotencyHash()`; D-7 two-tier TTL; new encoding-equivalence corpus eliminates Unicode-normalization edge cases; reconciliation workflows (Phase 6); C7 two-number lock contract.
- **Performance:** PRD budgets preserved; F-7 separate ops-console budget with 400ms-skeleton + 2s-cancel UX; provider rate-limit chaos test (I-8).
- **Scalability:** D-3 production state-store with criteria recorded and thresholds deferred-until-C1 (circular reference resolved); per-tenant token buckets (I-8); C1/C5 are exit criteria with operational ownership now documented.
- **Integration & Contract Compatibility:** C0 OpenAPI 3.1 Contract Spine + extension vocabulary; A-3 BLOCKING server-vs-spine validation; A-7 Forgejo per-version snapshots + nightly oasdiff; C13 parity oracle with transport AND behavioral columns; new symmetric drift gate; new per-class completeness assertion; NSwag golden-file gate.
- **Observability/Auditability/Replay:** I-6 OpenTelemetry OTLP; A-10 correlation + task ID propagation end-to-end; EventStore as audit substrate; D-10 audit projection rebuildable from events; sentinel redaction with normative corpus.
- **Data Retention:** D-7 ties commit-TTL to retention period (C3); C3 promoted to Phase-1-blocking with ownership and authority assigned.
- **Console Accessibility:** Microsoft Fluent UI Blazor (WCAG 2.2 AA); F-4 operator-disposition labels (now sourced from C6 enumerated matrix); F-5 redaction lock-icon affordance; F-6 incident-mode UX guardrails; F-7 perceived-wait UX.
- **Verification Expectations:** every NFR has at least one CI gate, lint, codegen rule, or release-validation evidence path; new exit-criteria-presence gate prevents shipping without C0–C13 artifact links.

### Implementation Readiness Validation ✅

**Decision Completeness:** 12 Critical + 5 Important + 6 Deferred decisions documented with rationale and alternatives. S-2 expanded to specify validation library + parameters. D-3 circular reference resolved.

**Structure Completeness:** Repo-root-to-leaf-file directory tree enumerates all 13 src projects, 11 test/load projects, samples projects, `.github/workflows/`, expanded `docs/` (with `exit-criteria/`, `runbooks/tenant-deletion.md`, `adrs/0000-template.md`), expanded `tests/fixtures/`, `tests/load/`, and `tests/tools/parity-oracle-generator/`.

**Pattern Completeness:** ~40 conflict-point categories addressed; new §"Adapter Parity Contract" specifies behavioral-parity dimensions previously asserted-away by the SDK-canonical reframe; new §"Workspace State Transition Matrix" enumerates C6 (deliverable, not just requirement); CI gate set expanded by 7 new gates.

### Gap Analysis Results

**Critical Gaps:** None. All previously identified blockers (Phase-1 spine pre-conditions, Phase-2 transition matrix, Phase-7 behavioral parity) have been resolved via Step 7 elicitation edits.

**Important Gaps (Tracked Exit Criteria — owned and scheduled):**

The architecture defers five PRD-quantitative targets (C1, C2, C3, C4, C5) to MVP-release validation. Each now has assigned owner, decision authority, artifact location, decision deadline (per phase), and measurement tool/method per §"Exit Criteria Operations Plan." C3 and C4 are Phase-1-blocking (their values shape the Contract Spine itself); the Phase 0.5 Pre-Spine Workshop is the resolution mechanism.

**Minor Gaps (Nice-to-Have):**

- ADR template authored Phase 0 (location declared at `docs/adrs/0000-template.md`).
- Tenant-deletion runbook authored Phase 4 (location declared at `docs/runbooks/tenant-deletion.md`).
- The C13 parity oracle's behavioral-parity columns now mandate `*.Cli.Tests` and `*.Mcp.Tests` consumption; coverage assertion presumes the test projects implement the contract — verification evidence lands during Phase 7.

### Validation Issues Addressed

Step 7 elicitation surfaced eight architectural concerns; each was addressed by upstream document edits before this validation result was finalized:

1. **C13 oracle symmetric drift gap** → Edit K added removal-detection gate against `tests/fixtures/previous-spine.yaml`.
2. **Parity-dimension completeness gap** → Edit K + Edit I added per-class completeness assertion (mutating ops MUST declare `idempotency_key_rule`; query ops MUST declare `read_consistency_class`).
3. **Encoding-equivalence gap** → Edit H added `tests/fixtures/idempotency-encoding-corpus.json` with NFC/NFD/NFKC/NFKD/zero-width-joiner/ULID-case variants; Edit I added the consumption gate.
4. **NSwag generated-output tampering gap** → Edit I added golden-file CI gate.
5. **Parity oracle self-schema gap** → Edit H added `tests/fixtures/parity-contract.schema.json`; Edit I added the validation gate.
6. **Exit-criteria ownership/authority/deadline gap** → Edit B added the Exit Criteria Operations Plan; Edit C added Phase 0.5 with C3/C4 as Phase-1-blocking; Edit I added the exit-criteria-presence gate.
7. **C6 transition matrix referenced-but-not-enumerated** → Edit E enumerated the 11-state, ~30-transition matrix with operator-disposition labels and the default-rejection rule; Edit I added the matrix-coverage CI gate.
8. **SDK-as-canonical behavioral-parity sleight-of-hand** → Edit G reworded the reframe to specify "transport collapse, not behavioral collapse"; Edit F added the §"Adapter Parity Contract" with per-adapter rules and full CLI exit-code + MCP failure-kind tables; Edit K extended the C13 row shape with behavioral-parity columns; Edit I added `*.Cli.Tests` + `*.Mcp.Tests` consumption gates.

Additional addressed items: S-2 OIDC underspecification (Edit D); D-3 Postgres-escalation circularity (Edit J); pattern-examples drift risk (Edit I compile gate).

### Architecture Completeness Checklist

**Requirements Analysis**

- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped (22 concerns; structure mapping table)

**Architectural Decisions**

- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed (PRD budgets + F-7 console budget + provider-rate-limit chaos)

**Implementation Patterns**

- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented (aggregate purity, idempotency, locking, authorization order, failure handling, sentinel redaction, cache key, logging, tests)

**Project Structure**

- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY WITH MINOR GAPS — all 16 checklist items pass; no Critical Gaps remain. The "minor" qualifier reflects the five Phase-deferred quantitative exit criteria (C1, C2, C3, C4, C5) and three artifact-deferred deliverables (ADR template, tenant-deletion runbook, behavioral-parity test evidence). C3 and C4 must resolve during Phase 0.5 Pre-Spine Workshop before Phase 1 Contract Spine authoring can begin; C1, C2, C5 resolve at their declared phase deadlines per §"Exit Criteria Operations Plan."

**Confidence Level:** Medium-high — high confidence in decision coherence, structure alignment, pattern enforcement, and CI-gate coverage. Confidence is one notch below "high" because (a) C3 and C4 must be set by stakeholders outside the architecture team, with possible scope or schedule pressure on the Phase 0.5 workshop, (b) the C6 enumerated transition matrix is large and aggregate-test coverage will surface refinements as `FolderStateTransitions.cs` is implemented, and (c) the behavioral-parity contract for CLI/MCP adapters is specified but not yet exercised through real test runs.

**Key Strengths:**

- One canonical Contract Spine (C0 OpenAPI 3.1) drives every surface (REST, SDK, CLI, MCP, parity oracle); the new symmetric drift gate, per-class completeness assertion, and NSwag golden-file gate prevent the most common cross-surface drift failure modes.
- Parity reframe is now precisely scoped: transport parity collapses to SDK-vs-REST; behavioral parity is pinned by §"Adapter Parity Contract" with full CLI exit-code and MCP failure-kind tables, and consumed by `*.Cli.Tests` + `*.Mcp.Tests` via the C13 oracle's behavioral-parity columns.
- C6 Workspace State Transition Matrix is enumerated as an architectural deliverable (11 states × disposition labels × ~30 transitions × default-rejection rule); `FolderStateTransitions.cs` translates 1:1; matrix-coverage CI gate prevents drift.
- D-9 bimodal file transport gives crisp red-phase contract tests AND ergonomic SDK DX without compromise.
- Defense-in-depth CI gate set (sentinel-redaction corpus, cache-key tenant-prefix lint, Dapr-policy negative-test conformance, Forgejo per-version contract snapshots with oasdiff, provider-rate-limit chaos test, encoding-equivalence corpus, symmetric drift gate, per-class completeness assertion, NSwag golden-file, parity-contract.schema.json validation, C6 matrix coverage, exit-criteria-presence, pattern-examples compile gate, behavioral-parity oracle consumption) covers the highest-risk concerns at multiple layers.
- Operations console UX (F-4 disposition labels, F-5 redaction affordance, F-6 incident-mode guardrails, F-7 perceived-wait UX) treats the console as a real product surface; disposition labels now source from the enumerated C6 matrix.
- Exit Criteria Operations Plan replaces "tracked" with "owned + authorized + dated + measured." Exit-criteria-presence CI gate prevents shipping with TBDs.
- Pre-Spine Workshop (Phase 0.5) and Spine Authoring Checklist make the Phase-1 entry conditions explicit and verifiable.

**Areas for Future Enhancement:**

- Local-only folder mode (post-MVP scope reduction).
- Repair workflows / repair console (post-MVP — MVP enforces inspectable terminal state, no silent repair).
- Multi-organization-per-tenant (post-MVP).
- Brownfield repository adoption (post-MVP).
- Webhook ingestion (post-MVP — explicit no-webhook posture in MVP).
- Additional Git providers beyond GitHub + Forgejo (capability-discovery model is n-provider-ready).
- Behavioral-parity test evidence accumulates during Phase 7; observed gaps may surface refinements to the Adapter Parity Contract.
- C1 capacity-test harness in `tests/load/` is scaffolded; targets and per-tenant load profiles refined Phase 9.
- C6 matrix may discover edge cases during aggregate implementation; matrix updates require corresponding aggregate-test additions in the same PR.
- Snapshot strategy refinement (D-6) once production event volumes are observed.
- Production retention policy (C3) evolution as legal/compliance landscape changes; the artifact at `docs/exit-criteria/c3-retention.md` should be reviewed annually.
- Pluggable production exporters (Jaeger / Tempo / App Insights / Datadog) selected per deployment; per-environment exporter binding remains an operations concern.

### Implementation Handoff

**AI Agent Guidelines:**

- Follow all architectural decisions exactly as documented; deviations require an architecture-document revision.
- Use implementation patterns consistently across all components; the enforcement guidelines list is non-negotiable. Sentinel-redaction tests, cache-key tenant-prefix lint, parity oracle (transport AND behavioral), Dapr policy conformance, C6 matrix coverage — all must pass before any PR merges.
- Respect project structure and boundaries; component references must follow the dependency direction in §"Component Boundaries."
- Refer to this document for all architectural questions; raise gaps as architecture-document amendments rather than coding around them.
- For workspace state changes, consult §"Workspace State Transition Matrix" — every transition is canonical; unlisted (state, event) pairs MUST reject with `state_transition_invalid`.
- For adapter behavior (CLI/MCP), consult §"Adapter Parity Contract" — pre-SDK errors, post-SDK error projection, and parameter sourcing are NOT inferable from "wrap the SDK."

**First Implementation Priority:**

Phase 0 — Solution Scaffolding, then Phase 0.5 Pre-Spine Workshop. From the repo root:

```bash
dotnet new sln -n Hexalith.Folders --format slnx
# Mirror Hexalith.Tenants Directory.Build.props and Directory.Packages.props (pin Hexalith.* 3.15.1)
# Create projects in dependency order:
#   src/Hexalith.Folders.Contracts → src/Hexalith.Folders → src/Hexalith.Folders.Server →
#   src/Hexalith.Folders.Client → src/Hexalith.Folders.{Cli,Mcp,UI,Workers,Aspire,AppHost,ServiceDefaults,Testing}
# Mirror tests/ 1:1 with src/.
# Create samples/Hexalith.Folders.Sample{,.Tests}.
# Create placeholder files:
#   tests/fixtures/{audit-leakage-corpus.json, parity-contract.schema.json, previous-spine.yaml, idempotency-encoding-corpus.json}
#   tests/load/Hexalith.Folders.LoadTests.csproj
#   tests/tools/parity-oracle-generator/
#   docs/exit-criteria/_template.md
#   docs/adrs/0000-template.md
# Create .github/workflows/{ci,contract-tests,nightly-drift,policy-conformance,release}.yml from sibling-module patterns.
```

Then Phase 0.5 Pre-Spine Workshop: complete every item in the §"Spine Authoring Checklist" before Phase 1 begins. Specifically: resolve C3 retention durations (Tech Lead + Legal + PM); resolve C4 input limits (Architect + PM); enumerate per-command `x-hexalith-idempotency-equivalence` and `x-hexalith-parity-dimensions`; finalize §"Adapter Parity Contract" if not already; pin S-2 issuer + audience per environment; seed `tests/fixtures/previous-spine.yaml`.

Then Phase 1 — Contract Spine (C0): create `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` with the extension vocabulary and the C3/C4 values; wire NSwag SDK generation in `Hexalith.Folders.Client.csproj`; generate the parity oracle with both transport-parity and behavioral-parity columns; wire all Phase-1 CI gates including BLOCKING server-vs-spine validation, symmetric drift detection, per-class completeness assertion, parity-contract.schema.json validation, NSwag golden-file gate.

From there, follow Phases 2–9 per §"Decision Impact Analysis → Implementation Sequence."
