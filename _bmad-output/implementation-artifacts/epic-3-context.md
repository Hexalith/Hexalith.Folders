# Epic 3 Context: Provider Readiness and Repository Binding

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Tenant administrators can establish tenant-scoped Git provider policy, authorized actors can create or bind repository-backed folders through GitHub or Forgejo, and operators can prove readiness without gaining configuration authority or exposing protected data. This prevents avoidable provider failures before workspace work begins and gives later task lifecycles a provider-neutral, deterministic repository binding.

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

- Tenant administrators own provider bindings, opaque credential references, repository naming/default-ref policy, and required capability policy. Scoped operators may validate and diagnose readiness but cannot change tenant policy.
- Check fresh authorization before credential resolution, protected-target observation, or provider mutation. Wrong-tenant, stale, revoked, and unauthorized requests fail closed without confirming repository or binding existence.
- Readiness validates configuration, credential-reference availability and least privilege, provider/version compatibility, required capabilities, provisioning support, and branch/ref policy. It exposes safe reason, retryability, remediation category, provider reference, correlation ID, and supported/unsupported/unknown evidence; unknown or incompatible evidence is never ready.
- Creation and existing-repository binding are distinct. Binding also checks access, canonical identity and aliases, duplicates, and exact branch/ref compatibility. Accepted policy becomes part of readiness, binding, and the serializing target; rejection leaves policy and binding unchanged and causes no provider side effect.
- Provider work is asynchronous after prompt acknowledgement. Persist one inspectable lifecycle and terminal task/binding result, and survive restart without duplicating effects. Every mutation is idempotent: equivalent replay returns the same logical result, conflicting replay is rejected without prior-intent disclosure, and expired keys never execute as fresh work.
- Map known provider failures to stable product categories and retry guidance. An unconfirmed effect enters `unknown_provider_outcome`; only bounded read-only checks—at most five in 15 minutes—may run before unresolved or conflicting evidence becomes `reconciliation_required`. Never retry a mutation blindly.
- Events, checkpoints, projections, audit, telemetry, diagnostics, and errors are metadata-only. Exclude bodies, diffs, tokens, credential values, provider responses, protected repository/ref locators, and unauthorized existence.
- Completion requires production registration and real-adapter evidence for success, denial, conflict, failure, timeout/ambiguity, cancellation, tenant isolation, and relevant boundaries. Fakes, NoOp adapters, seeds, unavailable responses, and safe-empty evidence are insufficient.

## Technical Decisions

- Use a capability-discoverable `IGitProvider` port that supports N providers. GitHub and Forgejo remain separate adapters; provider authentication, DTOs, and failure shapes stay behind the port.
- GitHub uses Octokit inside its adapter. Forgejo uses a version-aware typed HTTP adapter backed by pinned per-version OpenAPI snapshots and a supported-version manifest. Hermetic contract checks gate changes, while scheduled schema-drift checks classify compatible additions separately from breaking drift.
- Keep aggregates pure: provider policy belongs to organization state; binding and folder lifecycle belong to folder state; workers/process managers own external provisioning, retry, reconciliation, and follow-up commands. Provider adapters execute mechanics but do not persist domain state or orchestrate workflows.
- Persist durable admission, checkpoints, fencing, and sanitized outcomes around external work. Provider state remains authoritative at GitHub or Forgejo; Folders stores only provider-neutral state and evidence needed for replay and projections.
- Canonical repository identity plus normalized target ref determines equivalence, aliases, and conflicts. A remote repository/ref is bound by at most one managed tenant; cross-tenant attempts use the safe non-enumerating denial.
- Follow the versioned Contract Spine and RFC 9457 error contract. Capability semantics, outcomes, correlation/task identity, idempotency, authorization, and audit behavior remain equivalent across REST, SDK, CLI, and MCP.
- No provider webhook ingestion is part of the MVP. External Git work is driven by durable domain events and process managers rather than side effects in aggregate handlers.

## UX & Interaction Patterns

- Provider evidence is read-only and projection-driven. Lead with tenant scope, authorization posture, provider/binding identity, readiness, reason, and freshness, then show each required capability as supported, unsupported, or unknown.
- Show safe provider/API profile, credential profile, remediation posture, retryability, and correlation reference. Never reveal credentials, tokens, protected locators, or unauthorized existence.
- Keep failed, unavailable, stale, unknown, missing, redacted, withheld, and inaccessible states distinct. Use text, icons, and accessible labels, never color alone; unavailability is not an empty result.
- The MVP console offers search/filtering and safe copy for identifiers only. It provides no provider configuration, repository mutation, credential reveal, retry, repair, or reconciliation controls.

## Cross-Story Dependencies

- Provider configuration and the neutral port enable provider discovery; readiness and branch/ref policy then gate creation and binding. GitHub and Forgejo adapters supply the mechanics consumed by asynchronous completion and later workspace orchestration.
- Epic 1 supplies the Contract Spine and parity rules; Epic 2 supplies folders and authorization; Epic 4 consumes ready bindings; Epic 6 presents provider evidence without redefining it.
- Story 3.14 runs after accepted GitHub and Forgejo provisioning/binding behavior and Epic 12's durable admission, checkpoints, process-manager execution, reconciliation, and terminal projections. Epic 12 consumes rather than reimplements the adapters.
- Epic 13 supplies endpoint validation, credential handling, call-budget controls, and production hardening for readiness and later provider calls.
