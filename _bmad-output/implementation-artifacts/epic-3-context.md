# Epic 3 Context: Provider Readiness and Repository Binding

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Tenant administrators establish tenant-scoped Git provider policy; authorized actors create or bind repository-backed folders; operators inspect readiness without gaining configuration authority or exposing protected data. Durable asynchronous completion turns provider requests into inspectable binding outcomes that later workspace tasks can trust.

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

- Tenant administrators own provider bindings, opaque credential references, naming/default-ref policy, accepted credential profiles, and required capabilities. Scoped validation and diagnostic permissions confer no configuration authority.
- Recheck authorization immediately before asynchronous side effects, before credential resolution or protected observation. Wrong-tenant, stale, revoked, hidden, and absent-target requests use the non-enumerating safe result.
- Readiness checks configuration, credential-reference availability and permission, provider/version support, provisioning capability, and branch/ref policy. Unsupported, unknown, stale, or unavailable required evidence cannot be ready. Return safe reason, retryability, remediation, correlation identity, and capability evidence.
- Creation and existing-repository binding are separate authorized flows for an existing logical folder. Binding checks access, canonical identity and aliases, duplicates, and exact ref compatibility. A matching repository proves creation only with evidence tying it to the originating operation; otherwise it requires the separately authorized binding flow.
- Acknowledge commands within 1 second p95, then persist asynchronous lifecycle, checkpoints, and one terminal task/binding result. Equivalent replay cannot duplicate effects; conflicting replay is rejected safely; expired idempotency keys never execute as fresh work.
- Unconfirmed provider effects enter `unknown_provider_outcome`. Start bounded read-only reconciliation immediately: at most five checks within 15 minutes, then `reconciliation_required` for unresolved or conflicting evidence. Never blindly repeat a mutation; confirmed absence permits a fresh authorized attempt with a new key.
- Events, checkpoints, projections, audit, telemetry, errors, and diagnostics contain approved metadata only. Exclude content, diffs, credentials, provider response bodies, raw protected repository/ref locators, and unauthorized existence.
- Outer lifecycle completion requires deployed durable success, restart replay from an empty checkpoint, and negative, tenant-isolation, cancellation, and boundary evidence. Adapter registration and hermetic real-transport evidence establish adapter behavior only; fakes, seeds, NoOp, unavailable, and safe-empty results cannot establish lifecycle completion.

## Technical Decisions

- Use an N-provider capability-discoverable `IGitProvider` port. Keep GitHub authentication/Octokit and Forgejo version-aware HTTP contracts private to separate adapters. Forgejo support uses pinned version snapshots, a supported-version manifest, hermetic contract gates, and scheduled drift classification.
- Keep aggregates pure: organization state owns provider policy; folder state owns binding/lifecycle; durable workers/process managers own provisioning, retries, reconciliation, and follow-up commands. Adapters execute provider mechanics without persisting or orchestrating domain decisions.
- Persist provider-neutral admission, checkpoints, fencing, and sanitized outcomes. GitHub/Forgejo remain authoritative for remote state. Serialization uses managed tenant + canonical provider/repository identity + normalized target-ref token; aliases collide. A remote repository/ref is bound by at most one managed tenant; cross-tenant conflicts remain non-enumerating.
- Preserve Contract Spine/RFC 9457 outcomes and authorization, idempotency, correlation, task, and audit parity across supported surfaces. The v2 candidate is governed by Story 1.17; historical v1 evidence cannot authorize v2 exposure. No provider webhook ingestion belongs to MVP.

## UX & Interaction Patterns

- Read-only, projection-driven evidence leads with tenant scope, authorization posture, readiness, safe reason, and freshness. Show required capabilities as supported, unsupported, or unknown, with safe retry/remediation and correlation references.
- Distinguish unavailable/stale evidence from field disclosure: visible, redacted, withheld, unknown, and missing have separate text and accessible non-color cues. During bounded reconciliation show progress and remaining budget; human escalation begins at `reconciliation_required`.
- The console offers scoped search/filtering and safe identifier copying; it exposes no configuration, credential reveal, mutation, retry, repair, or reconciliation controls.

## Cross-Story Dependencies

- Configuration and provider-neutral contracts enable discovery; readiness/ref policy gate creation and binding. Epic 2 supplies folder authorization, Epic 4 consumes ready bindings, Epic 6 presents evidence, and Epic 13 supplies provider-security hardening.
- Story 3.14 has accepted-terminal adapter prerequisites 3.10 and 3.12, plus Stories 12.1, 12.2, and 12.4. Story 12.4 also depends on 12.3 and 12.7. Adapters are consumed, not reimplemented. Execution rank 31 must remain later than unresolved prerequisites.
- Current execution authority is the planning-story manifest; sprint status tracks lifecycle only. Story 3.14 remains `backlog`/`held` with open delivery evidence. The general hold was removed and A6b/A8 accepted, but ordinary-story execution is not generally authorized. The epics definition governs; the two frozen 3.14 specifications are historical workflow snapshots.
- Story 12.1 has completed source verification recorded on 2026-10-15, while its manifest acceptance remains open; this does not prove deployed durability. Story 1.17 remains open, v2 exposure unauthorized, and the accepted 2026-10-17 qualification refresh leaves Platform qualification outstanding.
- The accepted 2026-10-06 correction assigns canonical lock identity/timing to 4.23 and shared deployed proof to 11.15 for 4.18–4.21; it adds no direct 3.14 prerequisite and does not close its existing dependencies.
