# Epic 6 Context: Read-Only Workspace Trust Console and Audit Review

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Give tenant-scoped operators, administrators, and audit reviewers a trustworthy read-only console for finding workspaces, proving tenant boundaries, and diagnosing lifecycle, readiness, locks, dirty work, provider outcomes, commits, failures, and audit history. Production trust requires populated durable projections that survive restart; seed-backed or unavailable views cannot establish completion.

## Stories

- Story 6.1: Audit and operation-timeline query endpoints
- Story 6.2: Scaffold FrontComposer-hosted read-only operations console
- Story 6.3: Render operator-disposition labels as primary visual
- Story 6.4: Implement sensitive-metadata redaction affordance
- Story 6.5: Author console diagnostic wireflow notes
- Story 6.6: Build folder and workspace diagnostic pages
- Story 6.7: Build provider readiness and support diagnostic pages
- Story 6.8: Build audit and operation-timeline diagnostic pages
- Story 6.9: Implement incident-mode last-resort read path
- Story 6.10: Enforce console performance and perceived-wait UX
- Story 6.11: Verify no-mutation enforcement and accessibility
- Story 6.12: Populate readiness, lock, dirty-state, and failed-operation projections
- Story 6.13: Populate provider-status, sync-status, and projection-freshness projections
- Story 6.14: Prove populated deployed-host diagnostic and transition-evidence journeys

## Requirements & Constraints

- Establish fresh tenant/folder authority before protected lookup, counting, checkpoint access, filtering, or shaping. Payload identifiers are input, never authority; scoped operators have no global browsing.
- Exclude file bodies, diffs, generated context, provider payloads, credentials, embedded credential URLs, secrets, and unauthorized existence from projections, errors, telemetry, and UI. Paths, repository names, branch names, and commit messages are tenant-sensitive. Confidential overrides retain only correlation references, with no cleartext recovery.
- Audit/timeline reads are paginated projections preserving safe actor/task/operation/correlation identity, timestamps, outcome, duration, transitions, and error category. Freshness and availability are independent; unavailable never means empty or current.
- Register production projections in the deployed Server; derive deterministic tenant/folder records from durable ordered events; replay from empty checkpoints and survive restart. Require populated authorized reads and denial, corruption/conflict, failure, timeout, replay-boundary, and redaction evidence. Seed-only, in-memory, fake-only, NoOp, and safe-empty evidence are insufficient.
- No mutation, repair, content browsing/editing, raw diff, credential reveal, or unrestricted filesystem access. Forms serve search/filter/sort/preferences only.
- Target WCAG 2.2 AA, keyboard access, visible focus, semantic tables/headings, non-color cues, responsive fallback, and 125%/150%/200% zoom. Primary page-load budgets: p95 <1.5s, p99 <3s; incident p95 <5s. Show skeletons at 400ms and cancellable loading at 2s.

## Technical Decisions

- Use Blazor Web App Interactive Server rendering through FrontComposerShell and Fluent UI V5, accessing projections through the generated client/read-only query service. Supply tenant-aware user context before enabling protected reads; no direct aggregate/provider reads.
- Reuse EventStore projection/query/storage infrastructure. Folders owns product semantics; platform actors, subscriptions, and pre-dispatch schema validation/upcasting stay platform-owned.
- Preserve six independent dimensions: workspace lifecycle, lock state, operator disposition, folder lifecycle, projection freshness/availability, and visibility/disclosure. Lock vocabulary is unlocked, locked, expired, stale, revoked. Dispositions are available, auto-recovering, degraded-but-serving, awaiting-human, terminal-until-intervention.
- Unknown_provider_outcome remains auto-recovering during bounded confirmation; exhausted/conflicting confirmation becomes reconciliation_required/awaiting-human. Committed means provider-confirmed remote persistence.
- Incident fallback requires the same actor's incident-admin permission and fresh tenant/folder authorization before observation. Denial emits exactly one safe audit record. Preserve bounded redacted metadata, checkpoint/time-window/correlation context, and a persistent degraded warning. Terminal denial and transient authority unavailability consume shared authorization envelopes and their approval state.

## UX & Interaction Patterns

Workspace search/state-first filters lead to scoped detail pages. Present identity, tenant/access context, trust summary, disposition, reason, and freshness before connected diagnosis, audit, provider, lock/task, durability, and indexing evidence. Provider views include safe capability/hardening signals.

Use status text, icons, accessible labels, and non-color cues; redaction uses a visible lock. Distinguish visible, redacted, withheld, unknown, and Missing disclosure from availability, denial, and stale data. Copy safe references only. Indexing unavailability never implies empty/complete indexing or reveals bodies, raw paths, snippets, or source URIs.

## Cross-Story Dependencies

- Reviewed wireflow notes and shared disposition/redaction components precede diagnostic pages.
- Epics 3–5 own shared readiness/lifecycle/parity/status semantics. Story 4.22 governs lifecycle; Stories 12.1–12.2 supply durable events/projection infrastructure. Their delivery is prerequisite; planning admission is not implementation evidence.
- Story 3.14 and Stories 12.4–12.5 supply provider/sync/checkpoint evidence. Story 4.18 owns transition evidence, Epic 10 indexing/search, Epic 13 hardening. Workstream 11 owns platform seams/verification lanes, not product projections.
- Deployed journeys require transition evidence, incident authorization, and all seven populated projections. Preserve Story 6.11's historical completion; production gaps belong to 6.12–6.14. Release gates consume performance/accessibility and runtime incident evidence.
