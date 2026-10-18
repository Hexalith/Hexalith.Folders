# Epic 3 Context: Provider Readiness and Repository Binding

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Tenant administrators configure Git provider bindings, credential references, and repository and ref policy for their tenant. Authorized actors create or bind repository-backed folders, and operators check readiness without gaining configuration authority or exposing secrets. The epic ends with durable asynchronous completion, which turns create/bind requests into terminal binding outcomes that can be inspected and that later workspace tasks can trust. Stories 3.1–3.13 are done. Story 3.14 is the only open story, and it is gated on the Epic 12 durable data plane.

## Stories

- Story 3.1: Configure provider binding and credential reference
- Story 3.2: Define IGitProvider port and capability model
- Story 3.3: GitHub capability discovery and safe readiness
- Story 3.4: Forgejo capability discovery, safe readiness, and contract-drift detection
- Story 3.5: Validate provider readiness with safe diagnostics
- Story 3.6: Request asynchronous creation of a repository-backed folder
- Story 3.7: Bind an existing repository to a folder
- Story 3.8: Define branch and ref policy
- Story 3.9: Inspect tenant and per-provider readiness evidence
- Story 3.10: GitHub repository provisioning, binding, and branch/ref behavior
- Story 3.11: GitHub file mutation, commit, status, and failure behavior
- Story 3.12: Forgejo repository provisioning, binding, and branch/ref behavior
- Story 3.13: Forgejo file mutation, commit, status, and failure behavior
- Story 3.14: Complete asynchronous repository creation and binding

## Requirements & Constraints

- Tenant administrators own provider bindings, opaque credential references, naming and default-ref policy, accepted credential profiles, and required-capability policy. Scoped operators may validate and diagnose but never modify. Token material, reversible confidential values, and URLs with embedded credentials are never persisted or returned.
- Check authentication first, then authority availability, then authority. Only after that may the system resolve credentials, look up a target, or observe the provider. Re-check authorization immediately before every asynchronous side effect. Wrong-tenant, stale, revoked, hidden, and absent targets all get the same non-enumerating safe result.
- Readiness reports ready only when every required capability, credential profile, and observed provider version is supported and current. Unsupported, unknown, stale, or unavailable evidence is never ready. Each failure maps to one canonical outcome family, with a safe reason, retryability, remediation category, provider reference, and correlation ID. Readiness separates a fresh incompatibility from transient unavailability.
- Create and bind are separate authorized flows on an existing logical folder. Binding checks repository access, canonical identity, aliases, duplicates, and exact ref compatibility. A matching repository counts as a completed create only if provider evidence ties it to the originating operation. Otherwise it needs the separate bind flow.
- At most one managed tenant may bind a given remote repository/ref. An attempt from another tenant gets the safe denial, never a duplicate-binding result. An archived folder's binding no longer occupies the duplicate index.
- Commands are acknowledged within 1 s p95, and the provider work continues asynchronously. Equivalent replay produces no duplicate repository or binding. Conflicting replay is rejected without revealing the prior intent. Expired keys never run as fresh work.
- When a provider effect cannot be confirmed, the operation enters `unknown_provider_outcome`. Read-only checks start immediately: at most five, all within 15 minutes, and the mutation is never repeated. If the checks are exhausted or the evidence conflicts, the operation moves to `reconciliation_required`, which blocks retry and calls for human escalation. Confirmed absence allows a fresh authorized attempt with a new key.
- Events, checkpoints, projections, audit, telemetry, and diagnostics carry approved metadata only. They never contain credentials, provider response bodies, raw repository/ref locators, endpoints, or the existence of unauthorized resources. Durable provider execution may keep only correlation tokens, credential references, and opaque provider handles.
- Story 3.14 counts as complete only with real deployed evidence: durable success, restart replay from an empty checkpoint, tenant isolation, denial, conflict, failure, timeout/unknown outcome, and boundary cases. In-memory, NoOp, seed-only, unavailable, safe-empty, and fake-only results never count. Registering an adapter and running it against a hermetic real transport proves the adapter only, not the lifecycle.

## Technical Decisions

- The `IGitProvider` port supports any number of providers and lets callers discover each provider's capabilities, which it exposes as supported/unsupported/unknown metadata. GitHub (Octokit, fine-grained permissions) and Forgejo (version-aware HTTP, scoped tokens) stay private to their own adapter projects, and only the core domain may reference those adapters. Forgejo support is pinned to per-version API snapshots, with a nightly drift classifier: additive changes warn, breaking changes fail.
- Aggregates stay pure (`Handle` and `Apply` with no I/O, clock, or randomness). Organization state owns provider policy, and folder state owns binding and lifecycle. Workers and process managers (`{Concept}Workflow` / `{Concept}Reconciler`) own provisioning, retries, reconciliation, and follow-up commands. Adapters run provider mechanics but do not persist or orchestrate domain decisions.
- Requests are admitted in this order: authorization → canonical validation → trusted intent → EventStore durable admission → provider work. Idempotency state moves `reserved` → `pending` → `terminal`, with `recoverable`, `unknown_provider_outcome`, and `expired` branches. A retry never receives a second fence, and resuming a recoverable operation requires a persisted checkpoint.
- The serializing identity is managed tenant + canonical provider/repository identity + normalized target ref. Aliases collide on it. GitHub and Forgejo remain authoritative for remote state.
- The lifecycle follows the accepted C6/PD11 model. Outcomes use the Contract Spine/RFC 9457 shape, with parity across REST, SDK, CLI, and MCP. MVP has no webhook ingestion, so webhook routes return 404. v2 stays unexposed and unrouted.

## UX & Interaction Patterns

- Readiness evidence is read-only and comes from projections. It leads with tenant scope, authorization posture, readiness, safe reason, and freshness. Each required capability shows as supported, unsupported, or unknown, along with safe remediation and a correlation reference.
- While bounded checks run, `unknown_provider_outcome` displays as `auto-recovering`, showing the last check, the next check, and the bound. It escalates to `reconciliation_required`/`awaiting-human` only after the checks are exhausted. The UI never renders `auto-reconciling`.
- Visible, redacted, withheld, unknown, and missing values each have distinct text, accessible labels, and cues that do not rely on color. Read-model availability is a separate axis. The console has no configuration, credential-reveal, mutation, retry, repair, or reconciliation controls.

## Cross-Story Dependencies

- The planning-story manifest is the execution authority, and the sprint tracker records lifecycle only. The manifest records `general_execution_hold: false`, `ordinary_story_execution_authorized: false`, and `v2_exposure_authorized: false`. Design approval never implies execution authorization.
- Story 3.14 (rank 31) is `backlog` / `held` / delivery `open`, with posture `open-real-production-evidence-required`. Its prerequisites are DEC-A8-HOLD (`accepted`), accepted 3.10 and 3.12 (both `done`/`completed`/`accepted`), and Stories 12.1, 12.2, and 12.4.
- Story 12.1 (rank 20) is `backlog` / `authorized-scoped` / delivery `open` in the manifest. The tracker marked it `done` on 2026-10-18 after the local authenticated durability acceptance passed. The manifest has not yet recorded that acceptance.
- Story 12.2 (rank 21) and Story 12.4 (rank 30) are `backlog` / `held` / `open`. Story 12.4 also needs Stories 12.3 (rank 21) and 12.7 (rank 22), both `backlog` / `held` / `open`, plus accepted 3.11 and 3.13. Story 3.14 consumes the provider adapters and the durable orchestration from 12.4; it does not reimplement them.
- Stories 6.13 and OQ11-EVIDENCE depend on 3.14. Epic 2 supplies folder authorization, Epic 4 consumes ready bindings, Epic 6 presents the evidence, and Epic 13 owns provider security hardening (SSRF guard, rate limiting).
- The epics definition governs Story 3.14. The 2026-09-12 (blocked), 2026-09-20 (ready-for-dev), and 2026-10-17 (draft, prerequisites-first) specs are workflow snapshots only and do not grant authorization.
