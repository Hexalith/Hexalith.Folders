# Input Reconciliation — Forgejo and GitHub API Research

## Reconciliation Scope

- **Input:** `_bmad-output/planning-artifacts/research/technical-forgejo-and-github-api-research-2026-05-05.md`
- **Compared with:** `_bmad-output/planning-artifacts/prd.md` (updated 2026-07-14)
- **Addendum:** none present
- **Method:** extracted provider facts that change product scope, configuration, readiness, user-visible capability, error/status behavior, or release acceptance. Adapter libraries, HTTP mechanics, queue topology, storage layout, and deployment recipes are separated as implementation concerns.

## Overall Assessment

The PRD preserves the research's main product conclusion: GitHub and Forgejo are overlapping but non-interchangeable providers and must be supported through explicit capabilities, provider-specific behavior, readiness checks, stable error translation, and contract tests rather than base-URL substitution. It also carries forward provider throttling, retry hints, timeout/unknown-outcome handling, least privilege, credential references, version-drift evidence, and provider-specific readiness visibility.

Four product-level gaps remain. The research's webhook-first architecture, broad forge feature catalog, migration engine, CI runner controls, and raw-payload archive are not missing MVP requirements; they are either explicitly superseded by the PRD or implementation/out-of-scope concerns.

## Product-Level Gaps and Contradictions

### 1. The release does not define which GitHub and Forgejo products/versions are supported

**Research signal:** GitHub.com, Enterprise Cloud, and GitHub Enterprise Server are distinct API targets. Forgejo behavior is instance- and major-version-dependent. Contract testing must target concrete products and versions.

**Current PRD:** The release promises "GitHub and Forgejo," requires supported API versions or behavior assumptions to be pinned or recorded, and requires contract tests, but it never declares whether GitHub means GitHub.com only, includes Enterprise Cloud, or includes any GHES version; nor does it name the supported Forgejo major-version range.

**Assessment:** This is a product-scope and acceptance gap, not merely adapter detail. Without a target matrix, "both providers pass" has no stable denominator and customers cannot know whether their provider deployment is eligible.

**Disposition needed:** Publish a release support matrix that names the supported GitHub product(s), GHES versions if any, Forgejo major/minor policy, and the exact targets used for release contract evidence. Unsupported products/versions must produce a safe readiness result rather than fail later in the task lifecycle.

### 2. Provider binding/readiness lacks an explicit instance identity and compatibility contract

**Research signal:** A Forgejo integration must treat each instance as a separate target with its own base URL, version, configuration, resource limits, and health. GitHub bindings likewise need product target and API version. Capability discovery should be evaluated for the specific instance/version.

**Current PRD:** Provider bindings, provider references, capability metadata, and version-drift evidence exist, but the user-visible contract does not explicitly require a binding to expose a canonical provider-instance identity, detected product/version, supported/unsupported compatibility status, or the freshness of that detection.

**Assessment:** This is a product-level readiness ambiguity. A tenant administrator or operator cannot reliably distinguish "credential invalid," "provider unavailable," "instance unsupported," "instance upgraded," or "capability probe stale" from the current named outcomes alone.

**Disposition needed:** Make canonical provider instance/product identity, detected version, compatibility status, probe timestamp/freshness, and safe mismatch reason part of binding/readiness/status semantics. Keep sensitive base URLs tenant-scoped or redacted under C9 rather than omitting instance identity entirely.

### 3. Supported credential/authentication profiles are unspecified

**Research signal:** Provider auth models differ materially. GitHub application automation should prefer GitHub App installation tokens or explicitly bounded fine-grained credentials. Forgejo should prefer scoped header tokens; its delegated OAuth2 scope behavior cannot be assumed equivalent. Credential type also affects permissions and rate limits.

**Current PRD:** It requires organization/tenant credential references, least privilege, capability validation, status, and revocation handling, but does not state which provider credential types are accepted for MVP, who owns them, or which types are rejected as unsafe or unsupported.

**Assessment:** This is partly implemented in provider adapters, but the supported credential profile is a product contract because administrators must configure it and readiness must give deterministic, actionable results. A generic "credential reference valid" result is insufficient if the credential kind cannot safely satisfy the lifecycle.

**Disposition needed:** Define the accepted MVP credential profiles per supported provider target, their required ownership/permission posture, and stable readiness reasons for unsupported kind, excessive/insufficient scope, expired/revoked credential, or wrong installation/organization. Vault choice, token exchange code, and header construction remain architecture details.

### 4. Capability transparency has no stable availability or readiness rule

**Research signal:** For a specific provider instance/version, each capability should be classified as supported, partially supported, emulated, or unavailable so differences are visible before execution.

**Current PRD:** FR22 exposes provider differences, FR23 requires support evidence, and errors include unsupported capability, but the PRD does not define a capability-availability vocabulary or say whether partial/emulated behavior can satisfy MVP readiness and cross-surface parity.

**Assessment:** This is an accidental product ambiguity. Provider readiness could otherwise report "ready" even when an adapter substitutes materially different behavior, or different surfaces could interpret partial support differently.

**Disposition needed:** Define the user-visible capability status vocabulary and the readiness rule for every required MVP capability. For the canonical lifecycle, either require native/contract-equivalent support or explicitly define permitted emulation and its observable semantics. Optional/post-MVP capabilities must not make an otherwise valid MVP provider binding fail readiness.

## Intentional Supersessions and Out-of-Scope Research

### Incoming webhooks and raw provider payload archives

The research recommends webhook-first synchronization, raw delivery retention, deduplication, queueing, and API reconciliation. The PRD explicitly excludes incoming provider webhook ingestion from MVP and forbids provider payload snapshots from protected product evidence. This is a deliberate product/security boundary. A future webhook requirement would need the tenant-routing, retention, redaction, signature, replay, and authorization decisions named by the PRD; the research recommendation must not be imported wholesale.

### Broad forge functionality

The research covers issues, pull requests, releases, packages, Actions/workflows, workflow runners, federation, and cross-forge migration. Hexalith.Folders deliberately narrows the current product to repository-backed workspace readiness, binding, governed file operations, durable commit, context/status, and audit. Runner isolation, package APIs, issue/PR semantics, and workflow portability are not current Folders requirements.

### Migration and bulk synchronization

The research recommends migration waves, dry runs, object mapping, backfills, and reconciliation. The PRD deliberately defers brownfield adoption, unmanaged-local migration, provider portability tooling, and deeper drift remediation. These are growth inputs, not MVP gaps.

## Provider Implementation Mechanics — Route to Architecture

The following research recommendations support the PRD but should not become product requirements unless their effects are user-visible:

- Separate GitHub and Forgejo adapters behind narrow product ports.
- Provider-specific OpenAPI/SDK choices, HTTP clients, GraphQL optimization, pagination, conditional requests, redirects, and DTO mapping.
- Per-provider queues, backoff with jitter, circuit breakers, concurrency budgets, and dead-letter handling.
- Git transport versus forge-metadata API separation.
- Forgejo database/storage/reverse-proxy sizing and GitHub/Forgejo deployment mechanics.
- Internal normalized models, checkpoints, provider DTO archives, and reconciliation worker topology, subject to the PRD's stronger content/redaction boundary.
- CI runner isolation and Actions compatibility testing, unless Folders later takes responsibility for workflow execution.

## Preserved Product-Level Research Findings

- GitHub and Forgejo are never treated as interchangeable APIs or a base-URL swap.
- Required lifecycle capabilities are exposed behind provider ports and contract-tested before readiness.
- Provider differences and unsupported capabilities are user-visible rather than inferred from arbitrary failures.
- Provider authentication, authorization failure, rate limiting, timeout, branch/ref conflict, repository conflict, drift, and unknown outcome map to stable product results.
- Provider calls are bounded, throttled, retry-aware, and preserve retry hints where safe.
- Credential material stays outside product payloads; credential references are tenant-scoped, least-privilege, revocable, and auditable.
- Provider-confirmed remote durability, not local Git success, defines committed state.
- Supported provider API versions or behavior assumptions must be recorded and validated through provider contract evidence.
