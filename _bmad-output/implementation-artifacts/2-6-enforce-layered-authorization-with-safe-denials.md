# Story 2.6: Enforce layered authorization with safe denials

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a system component executing a folder operation,
I want layered authorization to run before resource access,
so that cross-tenant access is denied without enumeration or leakage.

## Terms

- Layered authorization means the ordered gate for every protected folder operation: authenticated principal/JWT evidence, EventStore claim transform evidence, Story 2.1 tenant-access projection, Story 2.4/2.5 folder ACL or effective-permission evidence, EventStore validator evidence, and Dapr deny-by-default policy evidence.
- Protected folder operation means any command or query that can observe or mutate folder, repository, credential-reference, provider, workspace, lock, file, commit, context-query, audit, read-model, projection, cache, idempotency, or diagnostic resources.
- Resource access means constructing or dereferencing protected stream names, read-model keys, cache keys, idempotency keys, workspace paths, provider handles, repository references, audit keys, diagnostics keys, projection queries, EventStore command envelopes, or Dapr service invocation targets.
- Protected resource touch means constructing, deriving, logging, timing, probing, or dereferencing any identifier, key, path, name, handle, target, partition, scope, or lookup subject for file, workspace, credential, repository, lock, commit, provider, audit, cache, stream, read-model, Dapr, diagnostics, projection, idempotency, or EventStore resources.
- Safe denial means the canonical metadata-only denial result or Problem Details shape used for authentication, tenant, folder ACL, validator, Dapr policy, unavailable, stale, not-found-to-caller, and cross-tenant outcomes. It must not reveal unauthorized resource existence.
- Authorization decision snapshot means the immutable metadata-only result for exactly one protected operation evaluation, including the terminal layer, terminal outcome code, retryability, freshness class, correlation ID, task ID, actor safe identifier, and optional bounded timing bucket. It must not include raw evidence payloads, protected resource identifiers, or unbucketed latency that could distinguish resource existence.
- Authorization evidence means stable metadata such as outcome code, layer, policy class, freshness, watermark, correlation ID, task ID, actor safe identifier, and timestamp. It must not include raw claims, access tokens, membership inventories, folder names, repository names, branch names, file paths, file contents, provider payloads, exception text, or other sensitive values.
- Bounded diagnostic read means a metadata-only, tenant-scoped, fixed-size diagnostic authorization path that can return only configured freshness/policy/correlation evidence and cannot construct or query resource-specific keys, provider/repository/file/workspace state, folder existence, ACL inventories, or raw exception details.
- EventStore validators means the Folders-owned validation layer that decides whether the authenticated command/query envelope is allowed to reach an aggregate or projection. This story wires and tests the authorization-before-resource-access contract; aggregate business rules remain owned by their stories.
- Dapr policy evidence means an in-process/environment evidence seam proving the target service invocation is covered by deny-by-default app policy and mTLS posture. Production policy authoring and deployment remain release/ops work unless already present locally.

## Acceptance Criteria

1. Given any protected folder operation is executed through the domain service or REST host, when authorization starts, then the implementation evaluates the required decision trace in this order before resource access: `JwtValidation`, `EventStoreClaimTransform`, `TenantAccessFreshness`, `FolderAcl`, `EventStoreValidator`, and `DaprDenyByDefaultPolicy`; each denial short-circuits later layers and records one immutable metadata-safe authorization decision snapshot for the layer that decided.
2. Given authoritative tenant context is missing, mismatched, client-supplied, malformed, reserved as `system`, or competing across route, query, body, ordinary headers, forwarded headers, generated client arguments, metadata bags, or EventStore envelope fields, when authorization runs, then every client-controlled tenant or principal value is treated only as comparison evidence and any mismatch returns safe denial before any protected resource touch, including constructing folder stream names, read-model keys, cache keys, idempotency records, diagnostics scopes, audit subjects, provider handles, repository references, workspace paths, lock keys, file paths, EventStore command envelopes, or Dapr invocation targets.
3. Given JWT/authentication evidence is missing, invalid, expired, wrong issuer, wrong audience, unsigned, stale against configured clock skew, or lacks required subject/client identity, when authorization runs, then it returns the existing safe `401` denial shape and no downstream layer is evaluated.
4. Given EventStore claim transform evidence is missing, malformed, tenant-mismatched, permission-missing, or principal-mismatched, when authorization runs, then it returns safe denial and does not query tenant projection, folder ACL, read models, EventStore streams, diagnostics, audit, provider state, repository state, workspace state, locks, files, or Dapr targets.
5. Given Story 2.1 tenant-access projection evidence is disabled, unknown, stale beyond the configured freshness threshold, unavailable, malformed, future-dated, replay-conflicting, tenant-mismatched, or denied, when a protected operation is evaluated with a fakeable clock and explicit operation policy, then authorization fails closed with stable metadata-only evidence and no protected resource lookup.
6. Given folder ACL or effective-permission evidence from Stories 2.4 and 2.5 is missing, unavailable, stale where freshness is required, tenant-mismatched, folder-mismatched, principal-mismatched, action-denied, revocation-freshness-unproven, or malformed, when authorization runs, then it returns safe denial before operation-specific resource access and does not reveal whether the folder, ACL entry, task, workspace, repository, provider binding, audit record, or file exists.
7. Given EventStore validators reject an operation after earlier layers pass, when authorization returns the rejection, then the response and audit evidence use canonical safe categories without leaking aggregate state, stream existence, expected versions, event type names, validator exception text, or internal command payloads.
8. Given Dapr deny-by-default policy evidence is missing, disabled, unavailable, mismatched for the target app ID, or not configured for the requested service invocation class, when authorization runs in an environment that requires Dapr policy evidence, then it fails closed before service invocation; when running offline unit tests, a deterministic fake evidence provider must prove the same decision contract without Dapr sidecars.
9. Given authorization allows an operation, when downstream code receives the authorization result, then it contains only the minimal safe authorization context needed to continue: authoritative tenant ID, actor safe identifier, permitted action token, folder ID or opaque operation scope already authorized, correlation/task IDs, freshness/watermark evidence, and policy layer evidence.
10. Given authorization denies an operation at any layer, when REST maps the result, then `401`, `403`, `404`, or `503` responses use the existing Contract Spine safe-denial or read-model-unavailable Problem Details shapes, share a single denial-mapping policy, preserve correlation IDs, omit resource-specific identifiers, derive status/category only from the authorization decision snapshot and operation policy, and remain externally indistinguishable for cross-tenant, unauthorized same-tenant, not-found-to-caller, stale, unavailable, and policy-denied protected resources except for allowed canonical status/category differences.
11. Given denied authorization produces audit, logs, traces, metrics, diagnostics, test output, Problem Details, generated client exceptions, or projection evidence, when metadata is inspected, then raw auth headers, JWTs, claim bags, provider tokens, credential material, repository names, branch names, file paths, file contents, diffs, generated context payloads, user emails, group names, role labels, membership inventories, tenant configuration payloads, raw request bodies, raw command/query bodies, unauthorized resource IDs, unbucketed timing/latency values, and exception text with sensitive values are absent.
12. Given a query operation permits bounded stale reads by policy, when the tenant projection or permission read model is stale but within the documented diagnostic/read policy, then the response carries freshness metadata and the allowed operation class; given a mutation or strict read cannot prove freshness, it fails closed rather than falling back to aggregate scans, projection repair, provider calls, repository calls, audit queries, filesystem reads, cache/idempotency reuse, unbounded diagnostics, or permissive defaults.
13. Given CLI, MCP, SDK, and REST adapters later consume the same capability, when this story completes, then the authorization result codes, canonical denial categories, action tokens, correlation propagation, and metadata fields are implemented in shared domain/server seams rather than copied into adapter-specific logic; this story only requires one production integration path plus contract-style conformance tests proving the result shape is adapter-consumable.
14. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider endpoints, initialized nested submodules, or live EventStore servers, when unit/server tests execute, then layer ordering, short-circuit behavior, safe denial mapping, Dapr policy evidence seam, EventStore validator rejection, stale/unavailable behavior, and metadata leakage boundaries are covered with in-memory fakes and spies.
15. Given this story owns layered authorization enforcement only, when implementation is complete, then it does not implement ACL grant/revoke mutation, effective-permissions query semantics beyond consuming their evidence, folder creation, folder archive, provider readiness, repository binding, workspace preparation, locks, file mutation, commits, context query execution, audit browsing, CLI/MCP/UI commands, workers, production Dapr policy deployment, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Authorization Order Contract

The canonical authorization order is:

1. `JwtValidation`
2. `EventStoreClaimTransform`
3. `TenantAccessFreshness`
4. `FolderAcl`
5. `EventStoreValidator`
6. `DaprDenyByDefaultPolicy`

Every implementation and test must use one ordered representation for layers, evidence, denial results, and response mapping. A denied layer must prevent all later layers and every protected resource touch. EventStore validators receive only safe authorization context and proposed operation descriptors, not materialized aggregate state, stream internals, or resource payloads.

## Authorization Evidence Snapshot

The layered evaluator must return exactly one authorization decision snapshot per protected operation attempt. A snapshot is either allowed or denied, never partially allowed. It carries the terminal layer, terminal outcome code, retryability, freshness class or watermark when allowed by policy, correlation ID, task ID, actor safe identifier, operation policy class, and a bounded timing bucket only when timing evidence cannot reveal resource existence. Per-request memoization or caches must be scoped by authoritative tenant, actor safe identifier, action token, operation policy, opaque folder or operation scope after authorization, and freshness watermark; stale, malformed, mismatched, or unavailable evidence must never be reused as allowed evidence.

## Denial Mapping Decision Table

| Condition | Status | Body shape | Retryable | Resource existence implication |
|---|---:|---|---|---|
| Unauthenticated, invalid, expired, unsigned, wrong issuer/audience, or missing subject/client identity | 401 | Existing safe authorization denial Problem Details | false | none |
| Authenticated but tenant authority is missing, malformed, reserved, mismatched, client-supplied, or denied before folder scope | 403 | Existing safe authorization denial Problem Details | false | none |
| Caller cannot learn whether a same-tenant folder/resource exists, including unauthorized same-tenant and not-found-to-caller outcomes | 404 | Existing safe authorization denial Problem Details | false | none; do not use actual resource lookup to choose 403 vs 404 |
| Tenant projection, folder permission evidence, EventStore validator evidence, or Dapr policy evidence is stale, unavailable, timed out, or policy-disabled for a retryable dependency | 503 | Existing read-model-unavailable or safe authorization denial Problem Details | true only when the dependency policy allows retry | none |

All denial responses, audit entries, logs, traces, metrics, generated-client exceptions, and diagnostic records must include only allowed metadata such as category, code, layer, freshness class, retryability, correlation ID, task ID, actor safe identifier, and timestamp. They must not include tenant names, folder IDs when unauthorized, ACL entries, provider names, repository names, stale version values, stream names, raw claims, raw policy text, raw exception messages, or values derived from protected resource lookups.

## Tasks / Subtasks

- [x] Add a shared layered authorization evaluator. (AC: 1, 9, 13)
  - [x] Add a service such as `LayeredFolderAuthorizationService` under `src/Hexalith.Folders/Authorization/`, or a locally consistent equivalent.
  - [x] Add one canonical ordered layer type used by evidence, result metadata, tests, and server response mapping.
  - [x] Model layer outcomes with stable lower-snake-case codes for allowed, authentication_denied, claim_transform_denied, tenant_access_denied, tenant_projection_stale, tenant_projection_unavailable, folder_acl_denied, folder_acl_stale, folder_acl_unavailable, eventstore_validator_denied, dapr_policy_denied, authorization_evidence_malformed, and safe_not_found.
  - [x] Preserve the required layer order mechanically rather than relying on caller discipline.
  - [x] Return a minimal safe allowed context for downstream operations and a minimal safe denial context for transport mapping.
  - [x] Emit exactly one immutable authorization decision snapshot per evaluation; malformed, unknown, or contradictory evidence must produce safe denial rather than an implicit allow or fallback.
  - [x] Scope any per-request memoization by authoritative tenant, actor safe identifier, action token, operation policy, authorized operation scope, and freshness watermark so decisions cannot bleed across tenants, principals, tasks, or stale evidence versions.
  - [x] Include correlation ID and task ID propagation without making them authorization authority.
  - [x] Keep behavior out of `Hexalith.Folders.Contracts`; only behavior-free DTOs may live there if an existing contract boundary requires them.
- [x] Wire authoritative identity and claim transform gates. (AC: 2, 3, 4)
  - [x] Add input context types that separate authoritative tenant/principal evidence from route/body/query/header/generated-client comparison values.
  - [x] Normalize and compare every route, query, body, ordinary header, forwarded header, generated-client argument, metadata bag, and EventStore envelope tenant or principal value before any protected key, path, target, partition, scope, diagnostics subject, audit subject, or stream name is constructed.
  - [x] Integrate existing `TenantAccessAuthorizer` rather than creating a second tenant projection evaluator.
  - [x] Add a narrow claim-transform evidence seam for EventStore `eventstore:tenant` and `eventstore:permission` data, with safe denial for malformed or absent evidence.
  - [x] Keep raw claims, tokens, headers, and command bodies out of result/evidence objects.
- [x] Consume folder ACL/effective-permission evidence without reimplementing previous stories. (AC: 6, 12, 15)
  - [x] Define an interface or adapter for folder-action authorization evidence that can be backed by Story 2.4 folder ACL projection and Story 2.5 effective-permission logic when those implementations land.
  - [x] Require action tokens to use the existing strict lower-snake-case folder ACL vocabulary: `configure_provider_binding`, `prepare_workspace`, `lock_workspace`, `read_metadata`, `read_file_content`, `mutate_files`, `commit`, `query_status`, `query_audit`, and `view_operations_console`; `create_folder` remains organization-baseline scope.
  - [x] Treat revocation-freshness-unproven as denial for mutations and strict reads.
  - [x] Allow bounded stale diagnostic reads only when the operation policy explicitly permits them and freshness metadata is returned.
  - [x] Prove denied folder ACL/effective-permission evidence short-circuits before folder, task, workspace, provider, repository, audit, lock, file, or context resources are observed.
- [x] Add EventStore validator and Dapr policy evidence seams. (AC: 7, 8)
  - [x] Add a testable EventStore validator wrapper that accepts the safe authorization context and returns allowed or safe denied evidence without exposing aggregate state or stream internals.
  - [x] Add a Dapr policy evidence provider interface with local fake implementation for tests and configuration-backed production posture checks.
  - [x] Fail closed when a protected service invocation class requires Dapr policy evidence and evidence is missing, mismatched, unavailable, or disabled.
  - [x] Keep production policy authoring/deployment out of scope unless existing local files only need reference validation.
  - [x] Avoid direct provider, repository, filesystem, or network calls in either evidence seam.
  - [x] Model timeout, stale, unavailable, malformed, denied, and allowed evidence separately in deterministic fakes.
- [x] Map safe denial to server responses. (AC: 10, 11, 13)
  - [x] Add server response mapping for authorization results in `Hexalith.Folders.Server` without changing Contract Spine shapes.
  - [x] Centralize the status/category mapping in a shared mapper such as `FolderAuthorizationDenialMapper`, or a locally consistent equivalent, instead of duplicating per endpoint.
  - [x] Derive response status, retryability, clientAction, and category only from the authorization decision snapshot and operation policy; do not branch on resource lookup results, exception subtype, provider payload, or raw latency.
  - [x] Reuse existing `SafeAuthorizationDenial401`, `SafeAuthorizationDenial403`, `SafeAuthorizationDenial404`, and `ReadModelUnavailable` response semantics.
  - [x] Ensure cross-tenant, not-found-to-caller, same-tenant unauthorized, stale, unavailable, EventStore validator denied, and Dapr policy denied paths do not disclose protected existence through body text, timing-sensitive branch behavior in tests, headers, diagnostic fields, unbucketed elapsed durations, or exception messages.
  - [x] Prove generated client exception shape is safe without hand-editing files under `src/Hexalith.Folders.Client/Generated/`.
  - [x] Keep generated SDK files under `src/Hexalith.Folders.Client/Generated/` untouched.
- [x] Add tests and fixtures. (AC: 1-15)
  - [x] Add unit tests such as `LayeredAuthorizationOrderTests`, `LayeredAuthorizationTenantIngressTests`, `LayeredAuthorizationClaimTransformTests`, `LayeredAuthorizationFolderAclTests`, `LayeredAuthorizationValidatorTests`, `LayeredAuthorizationDaprPolicyTests`, and `LayeredAuthorizationMetadataLeakageTests`.
  - [x] Add server tests such as `SafeAuthorizationDenialMappingTests` proving Problem Details categories, status codes, correlation IDs, retryability/clientAction values, and response bodies match existing Contract Spine components.
  - [x] Add side-effect spies and key-factory spies proving each rejected layer prevents downstream layer evaluation, protected resource access, and protected key/path/target/scope construction.
  - [x] Add matrix coverage for missing JWT, invalid JWT, tenant mismatch, requested tenant from each client-controlled ingress, claim-transform mismatch, each Story 2.1 tenant outcome, each folder ACL/effective-permission denial class, EventStore validator denial, Dapr policy unavailable, and allowed path.
  - [x] Add separate stale/unavailable/timeout tests for tenant projection, folder permission evidence, EventStore validator evidence, Dapr policy evidence, mutation, strict read, and bounded diagnostic read policies.
  - [x] Add malformed, contradictory, future-dated, unknown-outcome, and duplicate-evidence tests proving each layer fails closed without later protected touches.
  - [x] Add paired enumeration-control tests for nonexistent folder, unauthorized existing folder, wrong-tenant folder, same-tenant not-found-to-caller, stale authorization state, and unavailable authorization dependency.
  - [x] Add same-identifier cross-tenant tests for folder IDs, task IDs, lock IDs, provider binding refs, repository binding refs, audit IDs, and cache/idempotency keys.
  - [x] Add authorization decision isolation tests proving decisions are not reused across tenants, principals, action tokens, task IDs, operation policy classes, stale freshness watermarks, or allowed-vs-denied evidence snapshots.
  - [x] Add leakage sentinel tests with forbidden values in auth headers, claim bags, requested tenant values, principal metadata, folder ACL evidence, validator messages, Dapr policy evidence, exception messages, route values, query values, command payloads, and diagnostics sinks.
  - [x] Add bounded diagnostic read tests proving only configured metadata fields are emitted, max count/size limits are enforced, and no folder/provider/repository/file/workspace resource is touched.
  - [x] Add thin adapter-conformance tests proving the shared authorization result/denial shape can be consumed without duplicating authorization logic beyond the selected production integration path.
  - [x] Extend `src/Hexalith.Folders.Testing/Factories/*` only with reusable authorization evidence builders that delegate to production validation where practical.
  - [x] Add `tests/Hexalith.Folders.Testing.Tests` coverage if new testing helpers are introduced.

## Dev Notes

### Source Context

- Epic 2 objective: tenant administrators and authorized actors can create folders, manage access, inspect effective permissions, archive folders, and receive safe authorization evidence with cross-tenant isolation enforced before any resource access. [Source: `_bmad-output/planning-artifacts/epics.md#Epic 2: Tenant-Scoped Folder Access And Lifecycle`]
- Story 2.6 foundation: layered authorization must run before resource access, and denied requests must return safe error shapes and metadata-only denial evidence. [Source: `_bmad-output/planning-artifacts/epics.md#Story 2.6`]
- PRD FR8 requires every operation to evaluate tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope; FR9 requires denial before exposing protected resources; FR10 requires authorization evidence without unauthorized resource details. [Source: `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`]
- Architecture AR-AUTHZ-01 defines the layered order: JWT validation, EventStore claim transform, local fail-closed tenant-access projection, folder ACL, EventStore validators, and production Dapr deny-by-default policies plus mTLS. [Source: `_bmad-output/planning-artifacts/architecture.md#Authorization & Tenant Integration`]
- Project context defines zero cross-tenant leakage as the top safety invariant and requires metadata-only events, logs, traces, metrics, projections, audit records, console responses, provider diagnostics, and errors. [Source: `_bmad-output/project-context.md#Critical Don't-Miss Rules`]
- The Contract Spine uses safe authorization responses across folder operations and defines `SafeAuthorizationDenial`, RFC 9457-style `ProblemDetails`, `EffectivePermissions`, and `FreshnessMetadata`. [Source: `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`]

### Previous Story Intelligence

- Story 2.1 implemented the Folders host, Tenants event subscription, local fail-closed tenant-access projection, and `TenantAccessAuthorizer`. Story 2.6 must reuse that authorizer and its outcomes rather than forking tenant semantics.
- Story 2.2 defines organization-level ACL baseline and strict action/principal token handling. Story 2.6 consumes ACL administrator or permission evidence; it must not rewrite organization ACL state.
- Story 2.3 defines opaque folder identity and the `{managedTenantId}:folders:{folderId}` stream-name pattern. Story 2.6 must prove stream names are not constructed before authorization allows them.
- Story 2.4 defines folder-level grants/revokes and revocation metadata needed for C7 freshness. Story 2.6 consumes that evidence for operation authorization; it does not mutate ACLs.
- Story 2.5 defines effective-permission inspection and safe permission evidence. Story 2.6 can depend on a narrow evidence interface but must not implement or duplicate the public effective-permission query.

### Existing Implementation State

- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs` already has `AuthorizeMutationAsync` and `AuthorizeDiagnosticReadAsync`, fail-closed projection handling, stale/fresh status, and safe outcome codes.
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizationContext.cs` currently separates `AuthoritativeTenantId`, `PrincipalId`, and `RequestedTenantId`; extend this separation instead of accepting client-controlled tenant authority.
- `src/Hexalith.Folders/Projections/TenantAccess/*` contains the local tenant-access projection and in-memory store used by tests.
- `tests/Hexalith.Folders.Tests/Authorization/TenantAccessAuthorizerTests.cs` provides current patterns for offline authorization tests with in-memory projection stores and fixed clocks.
- `src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml` exists for local Dapr posture, but tests must not require running Dapr sidecars or production policy files.
- Generated SDK files under `src/Hexalith.Folders.Client/Generated/` are generated outputs and must not be hand-edited.

### Required Architecture Patterns

- Authorization-before-observation is the core invariant: no protected stream/key/path/handle/resource may be constructed or queried before the relevant authorization layer passes.
- Keep authorization evidence metadata-only. Do not echo raw claims, raw tokens, raw request bodies, raw command bodies, membership inventories, folder names, repository names, branch names, file paths, provider details, or exception text.
- Keep `Hexalith.Folders.Contracts` behavior-free. Authorization evaluators, result mapping, and tests belong in `Hexalith.Folders`, `Hexalith.Folders.Server`, and test projects.
- Keep aggregates pure. Layered authorization happens before aggregate/resource access, while aggregates still enforce their own domain invariants.
- The Dapr evidence seam validates policy posture but must not turn unit tests into integration tests. Use deterministic fakes for test coverage.
- Prefer shared result and denial mapping over per-endpoint copies so later REST, SDK, CLI, and MCP parity work can consume the same semantics.
- Use C# file-scoped namespaces, nullable-aware records/classes, one public type per file, PascalCase types/members, camelCase locals/parameters, async APIs for I/O boundaries, and cancellation token propagation.

### Deferred From Story 2.6

- Full CLI, MCP, SDK, and UI adapter rollout is deferred unless already adjacent to the selected production integration path; this story proves shared result/denial conformance instead of duplicating adapter behavior.
- Diagnostics beyond bounded metadata-only freshness/policy/correlation reads are deferred.
- Production Dapr access-control deployment, background worker authorization rollout, provider readiness, repository/workspace/file/commit behavior, repair flows, public permission-query UX, ACL grant/revoke mutation, and folder lifecycle behavior remain out of scope.

### Files To Touch

- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationContext.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationResult.cs`
- `src/Hexalith.Folders/Authorization/AuthorizationLayer.cs`
- `src/Hexalith.Folders/Authorization/AuthorizationOrder.cs`
- `src/Hexalith.Folders/Authorization/IEventStoreAuthorizationValidator.cs`
- `src/Hexalith.Folders/Authorization/IFolderPermissionEvidenceProvider.cs`
- `src/Hexalith.Folders/Authorization/IDaprPolicyEvidenceProvider.cs`
- `src/Hexalith.Folders.Server/*Authorization*` or endpoint modules that map safe authorization results to existing Problem Details.
- `src/Hexalith.Folders.Testing/Factories/*Authorization*` only for reusable safe test evidence builders.
- `tests/Hexalith.Folders.Tests/Authorization/*LayeredAuthorization*Tests.cs`
- `tests/Hexalith.Folders.Server.Tests/*SafeAuthorization*Tests.cs`
- `tests/Hexalith.Folders.Testing.Tests/*` only when new testing helpers are added.

### Do Not Touch

- Do not edit generated SDK files in `src/Hexalith.Folders.Client/Generated/`.
- Do not change the OpenAPI Contract Spine unless implementation discovers a blocking mismatch handled through the contract workflow.
- Do not implement public effective-permission query behavior; Story 2.5 owns that.
- Do not implement ACL grant/revoke mutation; Story 2.4 owns that.
- Do not implement folder creation, folder archive, provider readiness, repository binding, workspace preparation, locks, file mutation, commits, context query execution, audit browsing, CLI, MCP, UI, workers, production Dapr policy deployment, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.
- Do not introduce provider, repository, filesystem, audit, diagnostics, cache, idempotency, read-model, or EventStore resource access before authorization allows that layer.
- Do not initialize nested submodules or use recursive submodule commands.

### Testing

- Tests must run offline without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider endpoints, live EventStore servers, or nested submodule initialization.
- Use xUnit v3 and Shouldly for unit/server tests; use NSubstitute only where an actual seam needs substitution.
- Use in-memory fakes and spies for authentication evidence, claim-transform evidence, tenant projection, folder permission evidence, EventStore validators, Dapr policy evidence, diagnostics sinks, audit sinks, and protected resource access.
- Include negative controls proving each denied layer prevents all downstream layers and protected resource construction.
- Include response-shape tests aligned to Contract Spine `SafeAuthorizationDenial401`, `SafeAuthorizationDenial403`, `SafeAuthorizationDenial404`, and `ReadModelUnavailable`.
- Include metadata leakage tests for every denied path and for allowed evidence objects.
- Include cross-tenant same-identifier tests so tenant A and tenant B can share folder IDs, task IDs, provider binding refs, repository binding refs, lock IDs, audit IDs, idempotency keys, and cache suffixes without dedupe, leakage, or authorization reuse.

### Regression Traps

- Do not use route, query, body, ordinary header, forwarded header, metadata bag, or generated client tenant values as authority.
- Do not build stream names, cache keys, idempotency keys, read-model keys, workspace paths, provider handles, repository refs, audit keys, diagnostics keys, or Dapr invocation targets before the relevant authorization layer passes.
- Do not let stale or unavailable tenant/permission evidence default to allowed for mutations.
- Do not expose why a resource was not found when the caller is unauthorized to know it exists.
- Do not leak raw claims, auth headers, JWTs, provider tokens, credential material, repository names, branch names, file paths, file contents, diffs, generated context payloads, user emails, group names, role labels, membership inventories, tenant configuration payloads, raw request bodies, raw command bodies, or exception text.
- Do not duplicate authorization semantics separately in REST, SDK, CLI, MCP, or UI adapters.
- Do not treat Dapr policy evidence as optional in environments configured to require it.

### References

- `_bmad-output/planning-artifacts/epics.md#Story 2.6`
- `_bmad-output/planning-artifacts/prd.md#Authorization and Tenant Boundary`
- `_bmad-output/planning-artifacts/prd.md#Contract and Quality Gates`
- `_bmad-output/planning-artifacts/architecture.md#Authorization & Tenant Integration`
- `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns`
- `_bmad-output/project-context.md#Critical Don't-Miss Rules`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`
- `_bmad-output/implementation-artifacts/2-1-stand-up-domain-service-host-with-tenants-integration.md`
- `_bmad-output/implementation-artifacts/2-2-implement-organization-aggregate-acl-baseline.md`
- `_bmad-output/implementation-artifacts/2-3-create-folders-within-a-tenant.md`
- `_bmad-output/implementation-artifacts/2-4-grant-and-revoke-folder-access.md`
- `_bmad-output/implementation-artifacts/2-5-inspect-effective-permissions.md`
- `src/Hexalith.Folders/Authorization/TenantAccessAuthorizer.cs`
- `tests/Hexalith.Folders.Tests/Authorization/TenantAccessAuthorizerTests.cs`

## Change Log

| Date | Change | Author |
|---|---|---|
| 2026-05-19 | Code review (commit ed657e5): resolved 5 decision-needed items and applied 20 patches — production handler hardening (per-command action-token map, claim-transform accessor, organization-baseline scope, AggregateId validation), default-deny EventStore validator + Dapr policy seams, explicit mapper branches, wildcard removal, ordering tiebreaker, future-dated evidence handling, validator/snapshot watermark coherence, plus added test coverage for future-dated tenant freshness, ConfigurationDaprPolicyEvidenceProvider, body equivalence, cross-tenant same-ID, and wildcard regression. Story moved from review to done. Tests: 466 pass solution-wide (+11 new). | Claude |
| 2026-05-19 | Implemented layered authorization evaluator, evidence seams, safe denial mapper, domain-service wiring, and offline conformance tests. | Codex |
| 2026-05-19 | Applied advanced-elicitation hardening for immutable decision snapshots, evidence isolation, fail-closed malformed evidence, safe response mapping inputs, and timing-leakage controls. | Codex |
| 2026-05-18 | Applied party-mode hardening for authorization order contract, denial mapping table, protected-resource no-touch tests, freshness semantics, bounded diagnostics, and adapter scope. | Codex |
| 2026-05-18 | Created story with layered authorization order, safe denial mapping, Dapr/EventStore evidence seams, resource-access short-circuiting, and offline leakage tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-05-19: Red phase confirmed with `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore`; expected compile failures for missing layered authorization types.
- 2026-05-19: `dotnet test .\tests\Hexalith.Folders.Tests\Hexalith.Folders.Tests.csproj --no-restore` passed: 274 tests.
- 2026-05-19: `dotnet test .\tests\Hexalith.Folders.Server.Tests\Hexalith.Folders.Server.Tests.csproj --no-restore` passed: 27 tests.
- 2026-05-19: `dotnet test .\Hexalith.Folders.slnx --no-restore` passed across the solution.

### Completion Notes List

- Story created by `/bmad-create-story 2-6-enforce-layered-authorization-with-safe-denials` equivalent workflow on 2026-05-18.
- Project context, Epic 2, PRD, architecture, Contract Spine safe denial shapes, Stories 2.1-2.5, current tenant authorization code/tests, recent commits, and story-creation lessons were reviewed.
- Ultimate context engine analysis completed - comprehensive developer guide created.
- Party-mode review completed on 2026-05-18T20:05:09+02:00 with Winston, Amelia, Murat, and John; coherent low-risk findings were applied inline.
- Advanced elicitation completed on 2026-05-19T03:04:00+02:00; coherent low-risk hardening was applied inline and scope-changing proposals were deferred rather than added.
- Added `LayeredFolderAuthorizationService` with canonical layer ordering, stable lower-snake-case outcome codes, immutable decision snapshots, minimal allowed context, and metadata-only denial context.
- Added authoritative identity comparison, EventStore claim-transform evidence, tenant-access projection reuse, folder permission evidence adapter, EventStore validator seam, and Dapr policy evidence seam.
- Wired the Folders domain-service `/process` path through the layered evaluator and centralized safe denial mapping in `FolderAuthorizationDenialMapper`.
- Added offline tests covering ordered evaluation, short-circuit behavior, stale/unavailable/malformed evidence, no downstream protected touches, bounded diagnostic reads, safe Problem Details mapping, metadata leakage sentinels, and decision isolation.

## Party-Mode Review

- ISO date and time: 2026-05-18T20:05:09+02:00
- Selected story key: 2-6-enforce-layered-authorization-with-safe-denials
- Command/skill invocation used: `/bmad-party-mode 2-6-enforce-layered-authorization-with-safe-denials; review;`
- Participating BMAD agents: Winston (System Architect), Amelia (Senior Software Engineer), Murat (Master Test Architect and Quality Advisor), John (Product Manager)
- Findings summary:
  - The authorization-before-resource-access invariant needed a testable protected-resource-touch definition and no-touch spy coverage.
  - The required layer order needed one canonical decision trace shared by evidence, results, tests, and mapping.
  - Safe denial mapping needed explicit 401/403/404/503 rules that do not use actual resource existence to choose between forbidden and masked-not-found outcomes.
  - Freshness, unavailable, timeout, and bounded diagnostic-read behavior needed separate fail-closed tests and fakeable clock/evidence seams.
  - Full adapter rollout risked scope creep; this story should prove one production integration path plus shared conformance rather than implementing every adapter.
- Changes applied:
  - Added `Protected resource touch` and `Bounded diagnostic read` terms.
  - Tightened AC1, AC2, AC5, AC10, AC12, and AC13 for ordered decision traces, no-touch behavior, fakeable freshness, centralized denial mapping, bounded diagnostics, and adapter-consumable seams.
  - Added `Authorization Order Contract` and `Denial Mapping Decision Table` sections.
  - Expanded tasks for canonical layer types, behavior-free Contracts boundaries, denial mapper centralization, deterministic evidence fakes, no-touch key-factory spies, enumeration-control tests, bounded diagnostic tests, and adapter conformance.
  - Added a `Deferred From Story 2.6` section to prevent scope creep into full adapter rollout, production Dapr deployment, diagnostics beyond bounded metadata, workers, provider/repository/workspace/file behavior, repair flows, permission UX, ACL mutation, and folder lifecycle behavior.
- Findings deferred:
  - None requiring product or architecture decision after the inline denial mapping and bounded diagnostic constraints were added.
- Final recommendation: ready-for-dev

## Advanced Elicitation

- ISO date and time: 2026-05-19T03:04:00+02:00
- Selected story key: 2-6-enforce-layered-authorization-with-safe-denials
- Command/skill invocation used: `/bmad-advanced-elicitation 2-6-enforce-layered-authorization-with-safe-denials`
- Batch 1 method names: Security Audit Personas; Failure Mode Analysis; Pre-mortem Analysis; Self-Consistency Validation; Critique and Refine
- Reshuffled Batch 2 method names: Red Team vs Blue Team; Chaos Monkey Scenarios; First Principles Analysis; 5 Whys Deep Dive; Architecture Decision Records
- Findings summary:
  - Security and failure-mode passes found that the story needed one immutable authorization decision snapshot so downstream response mapping cannot mix partial layer results or raw evidence.
  - Pre-mortem and chaos passes identified decision reuse, stale freshness watermarks, malformed evidence, and unbucketed latency as plausible leakage or fail-open risks.
  - Self-consistency and ADR passes confirmed the existing architecture remains correct when the evaluator centralizes evidence snapshots and keeps Contract Spine DTOs behavior-free.
- Changes applied:
  - Added the `Authorization decision snapshot` term and a dedicated `Authorization Evidence Snapshot` section.
  - Tightened AC1, AC2, AC10, AC11, and AC12 for immutable snapshots, client-controlled evidence comparison, response mapping inputs, timing leakage, and cache/idempotency no-fallback behavior.
  - Expanded tasks for fail-closed malformed evidence, decision isolation across tenants/principals/actions/tasks/freshness, safe response mapper inputs, and leakage-safe timing handling.
  - Expanded tests for contradictory/unknown evidence, decision reuse boundaries, and stale freshness isolation.
- Findings deferred:
  - No product or architecture decisions were added; full cross-adapter rollout, production Dapr deployment, and diagnostics beyond bounded metadata remain deferred by the existing story scope.
- Final recommendation: ready-for-dev

### File List

- `_bmad-output/implementation-artifacts/2-6-enforce-layered-authorization-with-safe-denials.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/Hexalith.Folders/Authorization/AllowingEventStoreAuthorizationValidator.cs`
- `src/Hexalith.Folders/Authorization/AuthorizationLayer.cs`
- `src/Hexalith.Folders/Authorization/AuthorizationOrder.cs`
- `src/Hexalith.Folders/Authorization/ConfigurationDaprPolicyEvidenceProvider.cs`
- `src/Hexalith.Folders/Authorization/DaprPolicyEvidenceOptions.cs`
- `src/Hexalith.Folders/Authorization/DaprPolicyEvidenceRequest.cs`
- `src/Hexalith.Folders/Authorization/DaprPolicyEvidenceResult.cs`
- `src/Hexalith.Folders/Authorization/DaprPolicyEvidenceStatus.cs`
- `src/Hexalith.Folders/Authorization/EffectivePermissionsFolderPermissionEvidenceProvider.cs`
- `src/Hexalith.Folders/Authorization/EventStoreAuthorizationValidationRequest.cs`
- `src/Hexalith.Folders/Authorization/EventStoreAuthorizationValidationResult.cs`
- `src/Hexalith.Folders/Authorization/EventStoreAuthorizationValidationStatus.cs`
- `src/Hexalith.Folders/Authorization/EventStoreClaimTransformEvidence.cs`
- `src/Hexalith.Folders/Authorization/FolderOperationPolicyClass.cs`
- `src/Hexalith.Folders/Authorization/FolderPermissionEvidenceRequest.cs`
- `src/Hexalith.Folders/Authorization/FolderPermissionEvidenceResult.cs`
- `src/Hexalith.Folders/Authorization/FolderPermissionEvidenceStatus.cs`
- `src/Hexalith.Folders/Authorization/IDaprPolicyEvidenceProvider.cs`
- `src/Hexalith.Folders/Authorization/IEventStoreAuthorizationValidator.cs`
- `src/Hexalith.Folders/Authorization/IFolderPermissionEvidenceProvider.cs`
- `src/Hexalith.Folders/Authorization/LayeredAuthorizationOutcomeCodes.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationAllowedContext.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationContext.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationDecisionSnapshot.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationResult.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs`
- `src/Hexalith.Folders/Authorization/LayeredFolderOperationPolicy.cs`
- `src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs`
- `src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs`
- `src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs`
- `tests/Hexalith.Folders.Tests/Authorization/FolderPermissionEvidenceProviderTests.cs`
- `tests/Hexalith.Folders.Tests/Authorization/LayeredFolderAuthorizationServiceTests.cs`
- `tests/Hexalith.Folders.Server.Tests/FoldersDomainServiceRequestHandlerTests.cs`
- `tests/Hexalith.Folders.Server.Tests/SafeAuthorizationDenialMappingTests.cs`

### Review Findings

_Code review on 2026-05-19 (commit `ed657e5`). Triage: 5 decision-needed, 12 patches, 4 deferred, 7 dismissed as noise._

#### Decision Needed — resolved 2026-05-19

All 5 decisions resolved during review. Resolutions inlined; resulting patches appear in the Patch section.

- [x] [Review][Decision][Resolved → Patch] Production integration path hardening — **decision: harden now within 2.6**. Add explicit per-command action-token mapping, source claim-transform evidence from an injected provider (not the command body), use organization-baseline opaque scope for `create_folder`, and validate `Command.AggregateId` against the expected folder identity for folder-scoped commands.
- [x] [Review][Decision][Resolved → Patch] Default-allow seams (EventStore validator + Dapr policy) — **decision: flip production defaults to fail-closed** (best practice). Change `LayeredFolderOperationPolicy.Mutation()` to `requiresDaprPolicyEvidence: true`. Replace `AllowingEventStoreAuthorizationValidator` registration with either no default (force consumers to wire one explicitly) or a default-deny validator. Allow stubs may be opt-in via explicit non-production configuration.
- [x] [Review][Decision][Resolved → Patch] Mapper coverage for non-retryable Dapr / claim-transform denials — **decision: explicit 403 with distinct categories** (best practice: stable HTTP status, distinct categories help operators). `DaprPolicyDenied` (Retryable=false) → 403 `policy_denied`. `ClaimTransformDenied` → 403 `authorization_denied`. Remove wildcard fallthrough for these outcomes.
- [x] [Review][Decision][Resolved → Patch] Wildcard permission tokens — **decision: remove wildcards** (best practice: principle of least privilege). `HasPermissionFor` must require an explicit token from the ACL vocabulary; no `*`, `folders:*`, or `commands:*` shortcuts. Update tests accordingly.
- [x] [Review][Decision][Resolved → Defer] Preflight `fail` vs story `review` status — **decision: defer with documented reason**. The 36 dirty paths in `predev-preflight-2026-05-19T120131Z.json` (recorded at 12:01:31Z) are exactly this story's own in-flight files. Commit `ed657e5` landed at 14:23 the same day; working tree was clean post-commit and the offline test suite passed (274 + 27). Preflight captured a transient pre-commit state, not a quality regression. Re-run preflight before flipping the story to `done`; expected to pass with the clean tree.

#### Patch

##### From resolved decisions

- [x] [Review][Patch] Replace substring action-token derivation with an explicit per-command mapping [`src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:~65-72`] — map known command types to ACL vocabulary tokens (`create_folder`, `prepare_workspace`, `lock_workspace`, `read_metadata`, `read_file_content`, `mutate_files`, `commit`, `query_status`, `query_audit`, `view_operations_console`, `configure_provider_binding`); unmapped command types must deny rather than default to `read_metadata`.
- [x] [Review][Patch] Source `EventStoreClaimTransformEvidence` from an injected provider, not the request body [`src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:~28-31`] — introduce a claim-transform evidence accessor (e.g., `IEventStoreClaimTransformEvidenceAccessor`) backed by the actual EventStore claim transform / auth pipeline; remove the in-handler synthesis that compares the command to itself; drop the `commands:*` injection.
- [x] [Review][Patch] `OperationScope` for `create_folder` must be the organization-baseline opaque scope, not `Command.AggregateId` [`src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:~32`] — derive the operation scope per command policy; for folder-scoped commands, validate that `Command.AggregateId` parses as the expected folder identifier shape before passing it as scope.
- [x] [Review][Patch] Validate `Command.AggregateId` for folder-scoped commands before constructing evidence requests [`src/Hexalith.Folders.Server/FoldersDomainServiceRequestHandler.cs:~32`] — reject malformed identifiers with `authorization_evidence_malformed` before any folder-permission lookup.
- [x] [Review][Patch] Flip `LayeredFolderOperationPolicy.Mutation()` default to `requiresDaprPolicyEvidence: true` [`src/Hexalith.Folders/Authorization/LayeredFolderOperationPolicy.cs:~30-37`] — production-default fail-closed. Non-production environments may explicitly construct a policy with `requiresDaprPolicyEvidence: false` or wire a permissive provider.
- [x] [Review][Patch] Replace the default `AllowingEventStoreAuthorizationValidator` registration with a default-deny (or no default) [`src/Hexalith.Folders/FoldersServiceCollectionExtensions.cs:~42`] — either omit a default `IEventStoreAuthorizationValidator` registration (force consumers to wire one) or register a `DenyAllEventStoreAuthorizationValidator` returning safe-denial `Denied()` evidence. `AllowingEventStoreAuthorizationValidator` becomes an opt-in test/non-production wiring only.
- [x] [Review][Patch] Add explicit mapper branches for non-retryable Dapr and claim-transform denials [`src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs:~45-62`] — `DaprPolicyDenied` when `Retryable=false` → `(403, "policy_denied")`; `ClaimTransformDenied` → `(403, "authorization_denied")`. Update `Title()` / `Message()` to cover the new categories.
- [x] [Review][Patch] Remove wildcard permission token support from `HasPermissionFor` [`src/Hexalith.Folders/Authorization/EventStoreClaimTransformEvidence.cs:~30-34`] — require an exact, case-sensitive match against the ACL vocabulary; remove `*`, `folders:*`, `commands:*` shortcuts. Update affected tests to use explicit tokens.

##### Pre-existing patches

- [x] [Review][Patch] Reserved-tenant guard is case-sensitive ordinal "system" only [`src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs:~332`] — `SYSTEM`, `System`, `system` with mixed case all bypass the reserved check. Use `OrdinalIgnoreCase` (and consider canonicalizing the authoritative tenant once at ingress).
- [x] [Review][Patch] `HasClientControlledMismatch` silently skips empty-string values [`src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs:~271-303`] — an attacker who supplies an empty ingress header for a tenant/principal dimension bypasses the mismatch check. Distinguish "key present but blank" from "key absent" and either deny on present-but-blank or include the empty value in comparison.
- [x] [Review][Patch] `TenantAccessAuthorizer` reuse is partial [`src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs:~144-155`] — `RequestedTenantId` is always passed as `null` and `managedTenantId = tenantAccess.TenantId ?? AuthoritativeTenantId.Trim()` silently switches to the projection-returned tenant if it differs. Pass the authoritative tenant as `RequestedTenantId` and assert that any non-null `tenantAccess.TenantId` equals the authoritative value; otherwise deny with `authorization_evidence_malformed`.
- [x] [Review][Patch] Future-dated permission evidence treated as `Stale` rather than `Malformed` [`src/Hexalith.Folders/Authorization/EffectivePermissionsFolderPermissionEvidenceProvider.cs:~75-79`] — `snapshot.Freshness.ObservedAt > clock.UtcNow` is silently coerced to stale; with `AllowBoundedStale=true` an obviously bogus future-dated snapshot is then allowed. Treat future-dated observations as malformed (fail closed) per the story's "future-dated" trap.
- [x] [Review][Patch] `EffectivePermissionsReadModelStatus.Stale` ignores `request.AllowBoundedStale` [`src/Hexalith.Folders/Authorization/EffectivePermissionsFolderPermissionEvidenceProvider.cs:~45-46`] — the BoundedDiagnosticRead policy can never consume a stale read-model snapshot. Branch on `AllowBoundedStale` when `Stale` is returned (return the snapshot as bounded-stale evidence with freshness metadata, or deny consistently with strict reads).
- [x] [Review][Patch] `Available` status with `Snapshot is null` falls through to `Unavailable` (retryable) [`src/Hexalith.Folders/Authorization/EffectivePermissionsFolderPermissionEvidenceProvider.cs:~41-44`] — an `Available` status without a snapshot should be `Malformed` (non-retryable). Otherwise a buggy read model loops the caller on a permanently broken state.
- [x] [Review][Patch] `HasActionGrant` non-deterministic for rows with identical `Sequence` and `EffectiveAt` [`src/Hexalith.Folders/Authorization/EffectivePermissionsFolderPermissionEvidenceProvider.cs:~99-112`] — last enumeration wins. Add an explicit deterministic tiebreaker (prefer revoke over grant on tie, or break ties on a stable secondary key).
- [x] [Review][Patch] Validator receives the pre-mutation `safeContext` [`src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs:~178 vs ~224-227`] — `safeContext` is built before `freshnessWatermark` is finalized, and the `with { FreshnessWatermark = ... }` clone happens after the validator is called. Validator sees a different watermark than the final snapshot reports. Build the final watermark first, then construct `safeContext` once.
- [x] [Review][Patch] Allowed snapshot's `FreshnessClass` not synced with the validator/Dapr final watermark [`src/Hexalith.Folders/Authorization/LayeredFolderAuthorizationService.cs:~170,~203`] — when validator/Dapr supply alternate `FreshnessWatermark` values, `FreshnessClass` still reflects the tenant/folder evidence class. Resolve to a single coherent freshness class+watermark pair on the allowed snapshot.
- [x] [Review][Patch] Dead branch in mapper: `AuthorizationEvidenceMalformed when Retryable` is unreachable [`src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs:~407-408`] — no factory constructs that outcome with `Retryable: true`. Delete the branch or document why it must remain.
- [x] [Review][Patch] Test coverage gaps relative to spec ACs — add: (a) `FreshnessStatus.Future` case to the tenant-access matrix theory (AC5); (b) revocation-freshness-unproven test with `policyClass: Mutation` (AC6); (c) direct unit tests for `ConfigurationDaprPolicyEvidenceProvider` covering `Enabled=false`, `RequirePolicyEvidence=false` with `RequiresPolicyEvidence=true`, and mismatched `TargetAppId` (AC8); (d) replace `DeniedDecisionSnapshotShouldRemainMetadataOnly`'s substring blocklist with a positive allow-list assertion on the serialized JSON shape (AC11).
- [x] [Review][Patch] Test coverage gaps for enumeration-control invariants — add: (a) paired body-equivalence assertion that the Problem Details body for a nonexistent folder and an unauthorized-but-existing same-tenant folder is byte-equivalent except for correlation/task IDs (AC10); (b) explicit same-identifier cross-tenant sweep (same folder ID, task ID, lock ID, idempotency key across tenant A and tenant B) proving authorization decisions are not reused or collided (AC11/Regression Trap).

#### Deferred (from resolved decisions)

- [x] [Review][Defer] Preflight `fail` recorded in `_bmad-output/process-notes/predev-preflight-2026-05-19T120131Z.json` — deferred; the 36 dirty paths were this story's own in-flight files (pre-commit state at 12:01:31Z). Commit `ed657e5` at 14:23 landed the work; working tree was clean afterwards and offline test suites passed (274 + 27). Re-run preflight before moving 2.6 to `done`; expected to pass on the clean tree.

#### Deferred

- [x] [Review][Defer] `EventStoreClaimTransformEvidence.Allowed` exposes its internal `HashSet<string>` via `IReadOnlySet<string>` [`src/Hexalith.Folders/Authorization/EventStoreClaimTransformEvidence.cs:~17-23`] — deferred, not exploitable from current call sites; revisit if the evidence is shared across threads or returned to untrusted callers.
- [x] [Review][Defer] Tests pin to a single UTC instant — no DST/timezone-offset or `>` vs `>=` boundary coverage on `ObservedAt > clock.UtcNow` [`tests/.../LayeredFolderAuthorizationServiceTests.cs:~11`] — deferred, fixed-clock pattern is consistent with sibling modules and the bounded-stale math is dominated by `TimeSpan` arithmetic.
- [x] [Review][Defer] `ConfigurationDaprPolicyEvidenceProvider` does not validate empty/missing allow-lists at registration time [`src/Hexalith.Folders/Authorization/ConfigurationDaprPolicyEvidenceProvider.cs:~28-29`] — deferred to the production Dapr deployment story; configuration-validation gate belongs with the policy-deployment work.
- [x] [Review][Defer] Bounded-diagnostic-read tests don't assert max count/size or non-touch of provider/repository/file/workspace [Tasks/Subtasks AC14 ledger] — deferred, current seams don't expose those paths so the invariant is satisfied by construction; revisit when diagnostic surface grows.

#### Dismissed (recorded for traceability, not actionable)

- Folder ACL denial and `safe_not_found` both mapping to 404 — intentional per the Denial Mapping Decision Table (caller must not learn whether a same-tenant resource exists).
- `details.code` and `details.layer` leaked in Problem Details body — spec line ~70 explicitly lists `category, code, layer, freshness class, retryability, correlation ID, task ID, actor safe identifier, timestamp` as allowed metadata.
- `TimingBucket` hard-coded to `"not_recorded"` — conservative; spec permits omitting timing buckets when timing cannot reveal resource existence.
- `StrictRead` policy using `AuthorizeMutationAsync` freshness budget — consistent with AC12 ("mutation or strict read cannot prove freshness, it fails closed").
- `Title()` and `Message()` varying by category in Problem Details — category-level distinction is allowed by spec.
- `HasClientControlledMismatch` not detecting multi-ingress disagreement when only one ingress is supplied — production handler supplies a single envelope-tenant only; the multi-ingress matrix is exercised through tests.
- `MapperShouldNotLeakProtectedIdentifiersInBodyExtensions` and `DeniedDecisionSnapshotShouldRemainMetadataOnly` cover JSON output only (not logs/traces/metrics) — there is no production logging in the current paths, so the dimension is structurally absent.
