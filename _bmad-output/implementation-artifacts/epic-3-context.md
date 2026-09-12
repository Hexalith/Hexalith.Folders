# Epic 3 Context: Provider Readiness And Repository Binding

<!-- Generated from planning artifacts. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Platform engineers and authorized actors can configure Git providers, prove tenant-scoped readiness, create or bind repository-backed folders, establish predictable branch/ref behavior, and inspect GitHub and Forgejo capability evidence without exposing credentials, protected repository details, or provider-specific contracts. This gates later workspace tasks on safe, current configuration and turns external provider outcomes into deterministic, inspectable product behavior.

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

- Tenant administrators own provider bindings, opaque credential references, repository naming/default-ref policy, and required capability policy. Scoped platform engineers may validate and diagnose readiness but cannot silently change tenant policy.
- Authorization and evidence freshness must be checked before credential resolution, protected target observation, or provider mutation. Wrong-tenant, stale, revoked, and otherwise unauthorized requests fail closed without revealing whether a repository or binding exists.
- Readiness must validate binding configuration, credential-reference availability and least privilege, provider/version compatibility, required capabilities, repository provisioning support, and branch/ref policy. Results expose a safe reason code, retryability, remediation category, provider reference, correlation ID, and supported/unsupported/unknown evidence; unknown or incompatible evidence cannot report ready.
- Repository creation and existing-repository binding remain distinct operations. Binding requires access, canonical identity, duplicate/alias, and exact branch/ref compatibility checks. A failed readiness or authorization decision causes no repository or binding side effect.
- Repository creation and binding are asynchronous after command acceptance. Return operation identity promptly, preserve the correct non-terminal lifecycle while provider work runs, and durably record exactly one terminal task/binding result with current retry eligibility and sanitized evidence. Restart from an empty worker checkpoint must reconstruct the same outcome without another provider effect.
- Accepted branch/ref policy is part of readiness, binding, and the canonical serializing target. Invalid, incompatible, or unauthorized changes must not alter the active binding.
- Every mutation is idempotent and must produce at most one eligible provider effect. Equivalent replay preserves one logical result; conflicting replay returns the canonical conflict without disclosing prior intent; expired keys never execute automatically as new work.
- Provider failures map to stable product categories with retry guidance. An unconfirmed external effect enters `unknown_provider_outcome` and permits at most five automatic read-only evidence checks within 15 minutes; exhausted or conflicting evidence enters `reconciliation_required`. Blind mutation retry is forbidden.
- Events, restart-safe evidence, logs, traces, metrics, audits, diagnostics, and errors remain metadata-only. They must exclude file bodies, diffs, tokens, credential material, endpoints, provider response bodies, raw protected repository/ref locators, and unauthorized existence.
- Completion of a provider surface requires production registration plus real-adapter evidence for success, denial, conflict, known failure, timeout/ambiguity, cancellation, tenant isolation, and applicable boundaries. Fake-only, NoOp, seed-only, unavailable, or safe-empty behavior is not positive completion evidence.

## Technical Decisions

- Use a capability-discoverable `IGitProvider` port that supports N providers. GitHub and Forgejo are separate adapters with explicit capability differences; provider DTOs and authentication models stay behind the port.
- GitHub uses Octokit inside its adapter. Forgejo uses a version-aware typed HTTP adapter backed by pinned per-version OpenAPI snapshots and a supported-version manifest. Hermetic contract checks gate changes, while scheduled schema-drift checks classify compatible additions separately from breaking drift.
- Provider adapters consume caller-supplied authoritative authorization, lock, ref-policy, idempotency, target/content, and reconciliation-budget evidence. They execute provider mechanics and return canonical results; they do not persist domain state or orchestrate durable workflows.
- Keep aggregate handling pure. Tenant provider policy belongs to organization state, folder binding/lifecycle belongs to folder state, and external provisioning, retry, and reconciliation effects belong in workers or process managers.
- Accepted domain events are persisted and published before workers perform external Git work and submit follow-up commands. In-flight checkpoints, reconciliation tasks, idempotency admission state, and fencing tokens are durable and fail closed when unavailable or unreadable.
- Public behavior follows the versioned Contract Spine and shared RFC 9457 error contract. Provider status, error categories, correlation/task identity, idempotency outcomes, and capability semantics must remain equivalent across REST, SDK, CLI, and MCP.
- Canonical repository identity and exact normalized target-ref semantics determine equivalent, duplicate/alias, or conflicting outcomes. Provider-owned repository state is referenced, not copied as Folders authority.

## Cross-Story Dependencies

- Provider configuration and the provider-neutral port precede provider-specific discovery; readiness validation then gates repository creation and binding. Branch/ref policy constrains readiness, binding, later preparation, and commit targets.
- GitHub and Forgejo provisioning/binding adapters supply the provider behavior consumed by asynchronous creation completion. Their mutation/commit/status adapters establish the provider-private seams consumed by later durable workspace orchestration.
- Epic 1 supplies the canonical Contract Spine and parity rules, while Epic 2 supplies logical folders and tenant authorization. Epic 4 consumes ready bindings for workspace lifecycle, and Epic 6 presents provider evidence without redefining it.
- Epic 12 owns durable target/content state, executor/process-manager composition, reconciliation scheduling, provider-confirmed commit persistence, terminal task/projection state, and end-to-end workspace proof. It consumes rather than reimplements the provider adapters established here.
