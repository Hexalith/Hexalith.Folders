# Epic 3 Context: Provider Readiness And Repository Binding

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable tenant administrators to establish provider, credential-reference, repository/default-ref, and capability policy; let authorized actors create or bind GitHub and Forgejo repositories; and give scoped platform engineers safe readiness evidence without tenant-policy mutation authority, secret exposure, provider-specific leakage, or ambiguous external effects. This epic establishes the provider-neutral repository boundary required by the durable workspace lifecycle.

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

- Tenant administrators own provider bindings, opaque credential references, repository naming/default-ref policy, and required capability policy. Scoped platform engineers may validate and diagnose readiness but may not silently mutate tenant policy.
- Authorize against current tenant, folder, delegated-actor, binding, credential, and operation scope before resolving credentials, observing protected provider state, or dispatching a side effect. Wrong-tenant and hidden-resource cases must fail without revealing existence.
- Readiness must validate provider support and version/profile, credential-reference availability, required permissions/capabilities, repository provisioning or binding support, and branch/ref compatibility. Results expose a safe reason, retryability, remediation category, provider reference, and correlation identity, never secret material.
- The provider capability contract must support an open-ended provider set and report operation support, ref behavior, file limits, credential mode, version/capability evidence, retry hints, and stable failure categories explicitly. GitHub and Forgejo differences must not leak into product contracts or be reduced to a lowest-common-denominator model.
- Repository creation and existing-repository binding are asynchronous, idempotent operations. Canonical provider/repository identity plus normalized target ref must distinguish success, equivalent replay, duplicate or alias binding, conflict, known failure, and ambiguous outcome without duplicate provider effects.
- File mutation, commit, and status behavior must preserve requested ordering, exact ref and commit identity, task-lock ownership, path/size/type policy, cancellation boundaries, and provider-neutral success/failure mapping. A successful commit requires provider-confirmed durable remote/ref state.
- Unknown external outcomes enter `unknown_provider_outcome`; perform at most five automatic read-only evidence checks within 15 minutes, never blind mutation retry. Exhausted or conflicting evidence enters `reconciliation_required` and permits only read-only evidence collection and human escalation.
- Events, logs, traces, metrics, projections, audit, diagnostics, errors, and restart evidence must exclude credentials, tokens, file contents, diffs, provider response bodies, raw URLs/locators, and unauthorized existence. Repository, ref, path, and commit-message metadata follows tenant-sensitive classification and redaction policy.
- Completion requires real deployed GitHub and Forgejo evidence across positive, denial, conflict, replay, failure, timeout/unknown, cancellation, tenant-isolation, restart, and boundary scenarios. Fakes, mocks, NoOp adapters, seeds, unavailable defaults, or safe-empty results alone are not completion evidence.

## Technical Decisions

- Provider bindings and policy belong to the organization aggregate; folder lifecycle and the canonical repository binding belong to the folder aggregate. Both persist through Hexalith.EventStore. Aggregate handlers remain pure; workers/process managers perform provider calls, durable handoff, retries, and reconciliation.
- All adapters implement one capability-discoverable `IGitProvider` port. GitHub uses Octokit 14.0.0 inside its adapter. Forgejo uses a typed HTTP client with per-version OpenAPI snapshots, a supported-version manifest, hermetic contract tests, response-equivalence tests, and nightly schema-drift classification; incompatible or unknown evidence cannot report ready.
- Every mutation uses durable EventStore-owned idempotency admission after authorization and canonical validation. Equivalent live intent replays one logical result; conflicting live intent returns a safe conflict; expired keys never execute as new work. Non-commit replay results use a 24-hour tier and commit results use the seven-year tier.
- The serializing identity is managed tenant plus canonical provider/repository identity plus normalized target ref. Aliases collide; folder, workspace, and task identifiers are metadata rather than lock identity. Revoked lock instances never reactivate.
- Provider rate limiting uses per-tenant, per-provider buckets for user-driven calls and per-provider global buckets for reconciliation, with bounded timeout, retry/backoff, retry-hint preservation, and explicit rate-limit/unavailable/unknown-outcome states.
- REST schemas derive from the OpenAPI 3.1 Contract Spine; the generated SDK is the typed client and CLI/MCP wrap it. Provider readiness, repository behavior, operation identity, authorization, idempotency, lifecycle, error, and audit semantics must remain equivalent across required surfaces.

## UX & Interaction Patterns

Provider-readiness evidence is read-only and metadata-only. Show tenant scope and authorization posture before provider, binding, credential-reference status, capability state, retryability, remediation category, correlation ID, and freshness. Ready, failed, denied, stale, unavailable, unknown, redacted, and not-configured states must be visibly and semantically distinct, with text and icons rather than color alone; redaction must never look like missing data or reveal credential values.

## Cross-Story Dependencies

- Stories 3.1–3.2 establish configuration and the provider-neutral port used by readiness, diagnostics, repository, and file/commit stories. Stories 3.3–3.5 gate create and bind work; Story 3.8 supplies the ref policy used by binding, locking, mutation, and commit behavior.
- Story 3.14 consumes the request from Story 3.6 and the applicable real provider behavior from Story 3.10 or 3.12 to reach a durable terminal binding result. Stories 3.11 and 3.13 depend on the canonical task lock and file-policy semantics owned by Epic 4.
- Epic 1 supplies the Contract Spine, error taxonomy, lifecycle vocabulary, and parity fixtures. Epic 2 supplies logical folders, tenant/folder authorization, and safe denial behavior. Epic 4 consumes the binding and provider operations for workspace tasks; Epic 5 proves cross-surface equivalence; Epic 6 renders the readiness evidence produced here without inventing UI-only states.
