# Epic 3 Context: Provider Readiness And Repository Binding

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Platform engineers and authorized actors can configure Git provider bindings and opaque credential references, validate readiness with secret-safe diagnostics, create or bind repository-backed folders asynchronously, define branch/ref policy, and inspect per-provider capability evidence. This epic is the gate before workspace task work: agents must not start prepare/lock/mutate/commit until provider configuration, access, and ref policy are proven ready without leaking secrets or unauthorized repository existence.

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

- Tenant administrators own provider bindings, credential-reference IDs, repository naming/default-ref policy, and minimum capability policy. Scoped platform engineers may validate and diagnose readiness but must not mutate tenant policy.
- Readiness must run before create or bind. Diagnostics expose ready/failed state, safe reason code, retryability, remediation category, provider reference, and correlation ID — never tokens, credential values, raw endpoints, response bodies, or unauthorized resource existence.
- Create and bind are separate paths: create provisions a new provider repository for an existing logical folder; bind attaches a pre-created repository after access, duplicate/alias, and branch/ref checks. Failed readiness or authorization must leave no repository or binding side effect.
- Accepted branch/ref policy becomes part of readiness, binding, and the canonical serializing target (managed tenant + canonical provider/repository identity + normalized ref). Invalid or unauthorized policy changes must not alter the active binding.
- Capability metadata must surface supported operations, branch/ref behavior, file limits, credential mode, version/profile, retryability hints, and failure categories for GitHub and Forgejo differences required by the lifecycle. Unknown or incompatible evidence cannot report ready.
- Completion evidence for real provider behavior requires deployed composition and positive/negative/tenant-isolation/boundary paths. Fake-only, NoOp, seed-only, unavailable, or safe-empty results prove fail-safe shape, not product completion.
- File contents, diffs, secrets, and unauthorized existence must never appear in events, logs, traces, metrics, audit, diagnostics, or errors.

## Technical Decisions

- Model providers through a capability-discoverable `IGitProvider` port sized for N providers, not a hardcoded two-provider or base-URL-swap design. GitHub and Forgejo are not interchangeable APIs.
- GitHub adapter uses Octokit inside the adapter boundary. Forgejo uses a typed HttpClient wrapper with pinned per-version OpenAPI snapshots, a supported-versions manifest, and hermetic PR-gate plus live-nightly drift checks (additive warn vs breaking fail). Provider-specific permission scoping stays inside adapters.
- Port surfaces only credential references and capability metadata. Folders never stores secret material.
- Organization aggregate holds provider bindings, credential references, repository defaults, and related policy; Folder aggregate owns folder lifecycle and binding state. Provisioning, retries, and reconciliation belong in workers/process managers, not aggregate handlers.
- Mutating create/bind flows are idempotent: authorization and evidence freshness precede credential or target resolution; exactly one eligible mutation; equivalent replay must not duplicate; conflicting replay rejects; known failures are terminal as defined; unknown provider outcome enters bounded read-only reconciliation (at most five checks within 15 minutes) then `reconciliation_required` — never blind mutation retry.
- Async create/bind advances through the canonical lifecycle from `requested` with inspectable non-terminal then terminal folder/task/binding results and sanitized, restart-safe, provider-neutral evidence.
- Provider contract suite must cover fixture-to-failure-mode mapping for known categories (auth, not-found, conflict, rate limit, protection, drift, timeout) vs unknown outcome.

## UX & Interaction Patterns

- Readiness and binding semantics must stay explainable for later console consumption: ready, degraded, safe blockers, retryability, and secret-safe evidence — no UI-only states.
- Surfaces that show provider readiness should lead with what is broken, who is affected, and what can safely happen next, using stable reason categories, correlation IDs, and remediation posture without mutation or credential reveal.
- Repository binding and provider identity are first-class orientation cues alongside tenant and folder scope.

## Cross-Story Dependencies

- 3.1 → 3.5/3.9 (configuration before readiness evidence); 3.2 → 3.3/3.4 (port before adapters); 3.5 gates 3.6/3.7; 3.8 constrains bind and later prepare/commit refs.
- 3.6 accepts async create; 3.10/3.12 execute provider create/bind/ref; 3.14 completes durable terminal binding/lifecycle. 3.11/3.13 implement provider file/commit/status seams used by later workspace lifecycle work.
- Epic 4 depends on successful binding and ref policy for prepare/lock/mutate/commit. Epic 6 renders readiness/provider evidence but must not invent semantics. Epic 12 supplies durable substrate and real Git write path required for positive completion claims on 3.10–3.14.
