# Story 2.6: Enforce layered authorization with safe denials

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a system component executing a folder operation,
I want layered authorization to run before resource access,
so that cross-tenant access is denied without enumeration or leakage.

## Terms

- Layered authorization means the ordered gate for every protected folder operation: authenticated principal/JWT evidence, EventStore claim transform evidence, Story 2.1 tenant-access projection, Story 2.4/2.5 folder ACL or effective-permission evidence, EventStore validator evidence, and Dapr deny-by-default policy evidence.
- Protected folder operation means any command or query that can observe or mutate folder, repository, credential-reference, provider, workspace, lock, file, commit, context-query, audit, read-model, projection, cache, idempotency, or diagnostic resources.
- Resource access means constructing or dereferencing protected stream names, read-model keys, cache keys, idempotency keys, workspace paths, provider handles, repository references, audit keys, diagnostics keys, projection queries, EventStore command envelopes, or Dapr service invocation targets.
- Safe denial means the canonical metadata-only denial result or Problem Details shape used for authentication, tenant, folder ACL, validator, Dapr policy, unavailable, stale, not-found-to-caller, and cross-tenant outcomes. It must not reveal unauthorized resource existence.
- Authorization evidence means stable metadata such as outcome code, layer, policy class, freshness, watermark, correlation ID, task ID, actor safe identifier, and timestamp. It must not include raw claims, access tokens, membership inventories, folder names, repository names, branch names, file paths, file contents, provider payloads, exception text, or other sensitive values.
- EventStore validators means the Folders-owned validation layer that decides whether the authenticated command/query envelope is allowed to reach an aggregate or projection. This story wires and tests the authorization-before-resource-access contract; aggregate business rules remain owned by their stories.
- Dapr policy evidence means an in-process/environment evidence seam proving the target service invocation is covered by deny-by-default app policy and mTLS posture. Production policy authoring and deployment remain release/ops work unless already present locally.

## Acceptance Criteria

1. Given any protected folder operation is executed through the domain service or REST host, when authorization starts, then the implementation evaluates the layers in this order before resource access: authenticated principal/JWT evidence, EventStore claim transform evidence, Story 2.1 tenant projection, folder ACL/effective-permission evidence, EventStore validators, and Dapr deny-by-default policy evidence.
2. Given authoritative tenant context is missing, mismatched, client-supplied, malformed, reserved as `system`, or competing across route, query, body, ordinary headers, forwarded headers, generated client arguments, metadata bags, or EventStore envelope fields, when authorization runs, then it returns safe denial before constructing folder stream names, read-model keys, cache keys, idempotency records, diagnostics, audit keys, provider handles, repository references, workspace paths, lock keys, file paths, or Dapr invocation targets.
3. Given JWT/authentication evidence is missing, invalid, expired, wrong issuer, wrong audience, unsigned, stale against configured clock skew, or lacks required subject/client identity, when authorization runs, then it returns the existing safe `401` denial shape and no downstream layer is evaluated.
4. Given EventStore claim transform evidence is missing, malformed, tenant-mismatched, permission-missing, or principal-mismatched, when authorization runs, then it returns safe denial and does not query tenant projection, folder ACL, read models, EventStore streams, diagnostics, audit, provider state, repository state, workspace state, locks, files, or Dapr targets.
5. Given Story 2.1 tenant-access projection evidence is disabled, unknown, stale beyond mutation policy, unavailable, malformed, future-dated, replay-conflicting, tenant-mismatched, or denied, when a protected operation is evaluated, then authorization fails closed with stable metadata-only evidence and no protected resource lookup.
6. Given folder ACL or effective-permission evidence from Stories 2.4 and 2.5 is missing, unavailable, stale where freshness is required, tenant-mismatched, folder-mismatched, principal-mismatched, action-denied, revocation-freshness-unproven, or malformed, when authorization runs, then it returns safe denial before operation-specific resource access and does not reveal whether the folder, ACL entry, task, workspace, repository, provider binding, audit record, or file exists.
7. Given EventStore validators reject an operation after earlier layers pass, when authorization returns the rejection, then the response and audit evidence use canonical safe categories without leaking aggregate state, stream existence, expected versions, event type names, validator exception text, or internal command payloads.
8. Given Dapr deny-by-default policy evidence is missing, disabled, unavailable, mismatched for the target app ID, or not configured for the requested service invocation class, when authorization runs in an environment that requires Dapr policy evidence, then it fails closed before service invocation; when running offline unit tests, a deterministic fake evidence provider must prove the same decision contract without Dapr sidecars.
9. Given authorization allows an operation, when downstream code receives the authorization result, then it contains only the minimal safe authorization context needed to continue: authoritative tenant ID, actor safe identifier, permitted action token, folder ID or opaque operation scope already authorized, correlation/task IDs, freshness/watermark evidence, and policy layer evidence.
10. Given authorization denies an operation at any layer, when REST maps the result, then `401`, `403`, `404`, or `503` responses use the existing Contract Spine safe-denial or read-model-unavailable Problem Details shapes and remain externally indistinguishable for cross-tenant, unauthorized same-tenant, not-found-to-caller, stale, unavailable, and policy-denied protected resources except for allowed canonical status/category differences.
11. Given denied authorization produces audit, logs, traces, metrics, diagnostics, test output, Problem Details, generated client exceptions, or projection evidence, when metadata is inspected, then raw auth headers, JWTs, claim bags, provider tokens, credential material, repository names, branch names, file paths, file contents, diffs, generated context payloads, user emails, group names, role labels, membership inventories, tenant configuration payloads, raw request bodies, raw command/query bodies, unauthorized resource IDs, and exception text with sensitive values are absent.
12. Given a query operation permits bounded stale reads by policy, when the tenant projection or permission read model is stale but within the documented diagnostic/read policy, then the response carries freshness metadata and the allowed operation class; given a mutation or strict read cannot prove freshness, it fails closed rather than falling back to aggregate scans, projection repair, provider calls, repository calls, audit queries, filesystem reads, or permissive defaults.
13. Given CLI, MCP, SDK, and REST adapters later consume the same capability, when this story completes, then the authorization result codes, canonical denial categories, action tokens, correlation propagation, and metadata fields are implemented in shared domain/server seams rather than copied into adapter-specific logic.
14. Given tests run without provider credentials, tenant seed data, production secrets, running Dapr sidecars, Keycloak, Redis, GitHub, Forgejo, provider endpoints, initialized nested submodules, or live EventStore servers, when unit/server tests execute, then layer ordering, short-circuit behavior, safe denial mapping, Dapr policy evidence seam, EventStore validator rejection, stale/unavailable behavior, and metadata leakage boundaries are covered with in-memory fakes and spies.
15. Given this story owns layered authorization enforcement only, when implementation is complete, then it does not implement ACL grant/revoke mutation, effective-permissions query semantics beyond consuming their evidence, folder creation, folder archive, provider readiness, repository binding, workspace preparation, locks, file mutation, commits, context query execution, audit browsing, CLI/MCP/UI commands, workers, production Dapr policy deployment, repair workflows, local-only folder mode, webhooks, brownfield adoption, or multi-organization-per-tenant behavior.

## Tasks / Subtasks

- [ ] Add a shared layered authorization evaluator. (AC: 1, 9, 13)
  - [ ] Add a service such as `LayeredFolderAuthorizationService` under `src/Hexalith.Folders/Authorization/`, or a locally consistent equivalent.
  - [ ] Model layer outcomes with stable lower-snake-case codes for allowed, authentication_denied, claim_transform_denied, tenant_access_denied, tenant_projection_stale, tenant_projection_unavailable, folder_acl_denied, folder_acl_stale, folder_acl_unavailable, eventstore_validator_denied, dapr_policy_denied, authorization_evidence_malformed, and safe_not_found.
  - [ ] Preserve the required layer order mechanically rather than relying on caller discipline.
  - [ ] Return a minimal safe allowed context for downstream operations and a minimal safe denial context for transport mapping.
  - [ ] Include correlation ID and task ID propagation without making them authorization authority.
- [ ] Wire authoritative identity and claim transform gates. (AC: 2, 3, 4)
  - [ ] Add input context types that separate authoritative tenant/principal evidence from route/body/query/header/generated-client comparison values.
  - [ ] Reject mismatched tenant values before any protected key or stream name is constructed.
  - [ ] Integrate existing `TenantAccessAuthorizer` rather than creating a second tenant projection evaluator.
  - [ ] Add a narrow claim-transform evidence seam for EventStore `eventstore:tenant` and `eventstore:permission` data, with safe denial for malformed or absent evidence.
  - [ ] Keep raw claims, tokens, headers, and command bodies out of result/evidence objects.
- [ ] Consume folder ACL/effective-permission evidence without reimplementing previous stories. (AC: 6, 12, 15)
  - [ ] Define an interface or adapter for folder-action authorization evidence that can be backed by Story 2.4 folder ACL projection and Story 2.5 effective-permission logic when those implementations land.
  - [ ] Require action tokens to use the existing strict lower-snake-case folder ACL vocabulary: `configure_provider_binding`, `prepare_workspace`, `lock_workspace`, `read_metadata`, `read_file_content`, `mutate_files`, `commit`, `query_status`, `query_audit`, and `view_operations_console`; `create_folder` remains organization-baseline scope.
  - [ ] Treat revocation-freshness-unproven as denial for mutations and strict reads.
  - [ ] Allow bounded stale diagnostic reads only when the operation policy explicitly permits them and freshness metadata is returned.
  - [ ] Prove denied folder ACL/effective-permission evidence short-circuits before folder, task, workspace, provider, repository, audit, lock, file, or context resources are observed.
- [ ] Add EventStore validator and Dapr policy evidence seams. (AC: 7, 8)
  - [ ] Add a testable EventStore validator wrapper that accepts the safe authorization context and returns allowed or safe denied evidence without exposing aggregate state or stream internals.
  - [ ] Add a Dapr policy evidence provider interface with local fake implementation for tests and configuration-backed production posture checks.
  - [ ] Fail closed when a protected service invocation class requires Dapr policy evidence and evidence is missing, mismatched, unavailable, or disabled.
  - [ ] Keep production policy authoring/deployment out of scope unless existing local files only need reference validation.
  - [ ] Avoid direct provider, repository, filesystem, or network calls in either evidence seam.
- [ ] Map safe denial to server responses. (AC: 10, 11, 13)
  - [ ] Add server response mapping for authorization results in `Hexalith.Folders.Server` without changing Contract Spine shapes.
  - [ ] Reuse existing `SafeAuthorizationDenial401`, `SafeAuthorizationDenial403`, `SafeAuthorizationDenial404`, and `ReadModelUnavailable` response semantics.
  - [ ] Ensure cross-tenant, not-found-to-caller, same-tenant unauthorized, stale, unavailable, EventStore validator denied, and Dapr policy denied paths do not disclose protected existence through body text, timing-sensitive branch behavior in tests, headers, diagnostic fields, or exception messages.
  - [ ] Keep generated SDK files under `src/Hexalith.Folders.Client/Generated/` untouched.
- [ ] Add tests and fixtures. (AC: 1-15)
  - [ ] Add unit tests such as `LayeredAuthorizationOrderTests`, `LayeredAuthorizationTenantIngressTests`, `LayeredAuthorizationClaimTransformTests`, `LayeredAuthorizationFolderAclTests`, `LayeredAuthorizationValidatorTests`, `LayeredAuthorizationDaprPolicyTests`, and `LayeredAuthorizationMetadataLeakageTests`.
  - [ ] Add server tests such as `SafeAuthorizationDenialMappingTests` proving Problem Details categories, status codes, correlation IDs, retryability/clientAction values, and response bodies match existing Contract Spine components.
  - [ ] Add side-effect spies proving each rejected layer prevents downstream layer evaluation and protected resource access.
  - [ ] Add matrix coverage for missing JWT, invalid JWT, tenant mismatch, requested tenant from each client-controlled ingress, claim-transform mismatch, each Story 2.1 tenant outcome, each folder ACL/effective-permission denial class, EventStore validator denial, Dapr policy unavailable, and allowed path.
  - [ ] Add stale/unavailable tests for mutation, strict read, and bounded diagnostic read policies.
  - [ ] Add same-identifier cross-tenant tests for folder IDs, task IDs, lock IDs, provider binding refs, repository binding refs, audit IDs, and cache/idempotency keys.
  - [ ] Add leakage sentinel tests with forbidden values in auth headers, claim bags, requested tenant values, principal metadata, folder ACL evidence, validator messages, Dapr policy evidence, exception messages, route values, query values, command payloads, and diagnostics sinks.
  - [ ] Extend `src/Hexalith.Folders.Testing/Factories/*` only with reusable authorization evidence builders that delegate to production validation where practical.
  - [ ] Add `tests/Hexalith.Folders.Testing.Tests` coverage if new testing helpers are introduced.

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
| 2026-05-18 | Created story with layered authorization order, safe denial mapping, Dapr/EventStore evidence seams, resource-access short-circuiting, and offline leakage tests. | Codex |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

### Completion Notes List

- Story created by `/bmad-create-story 2-6-enforce-layered-authorization-with-safe-denials` equivalent workflow on 2026-05-18.
- Project context, Epic 2, PRD, architecture, Contract Spine safe denial shapes, Stories 2.1-2.5, current tenant authorization code/tests, recent commits, and story-creation lessons were reviewed.
- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List
