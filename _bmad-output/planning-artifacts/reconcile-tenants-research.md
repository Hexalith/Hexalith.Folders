# Input Reconciliation — Hexalith.Tenants Integration Research

Input: `_bmad-output/planning-artifacts/research/technical-hexalith-tenants-integration-for-folder-management-application-research-2026-05-05.md`

Compared with: `_bmad-output/planning-artifacts/prd.md` (updated 2026-07-14)

Addendum: no `addendum.md` was present in the planning-artifacts workspace at reconciliation time.

Purpose: extract product-relevant gaps and contradictions without copying implementation detail into the PRD. This file does not change the PRD, research source, architecture, contracts, or implementation.

## Reconciliation verdict

The updated PRD preserves the research's central product direction: `Hexalith.Tenants` is the authority for tenant identity/lifecycle/membership; `Hexalith.Folders` owns folder policy, ACLs, provider bindings, workspaces, and folder audit; authorization is tenant-scoped and must be fresh before protected side effects; revocation fails closed; caller-supplied tenant identifiers are not trusted; and global cross-tenant browsing is excluded from MVP.

Four material gaps remain. Two are authorization semantics that belong in the PRD, one is an unresolved configuration-authority boundary, and one is a production security outcome whose mechanism belongs downstream.

## Meaningful gaps and contradictions

### 1. Disabled, unknown, missing, and stale tenant-authority states lack explicit product outcomes

Research evidence:

- A disabled tenant denies state-changing Folder commands.
- Unknown or missing tenant authority fails closed unless an authoritative lookup is deliberately allowed.
- Stale local tenant-access state denies high-risk operations or is refreshed from the authoritative tenant source.
- Member removal and role downgrade revoke the affected capabilities after propagation.

PRD coverage:

- **Architectural Boundaries** names `Hexalith.Tenants` as source of truth.
- **Authentication and Authorization Model** requires fresh authorization before every side effect and handles membership/ACL/delegated-authority revocation.
- FR8-FR10, FR24, FR37, the authorization matrix gate, CM1, and the Security NFR establish broad fail-closed intent.

Gap:

The PRD never states the caller-visible outcome for an inactive/disabled tenant or for missing, unknown, unavailable, or stale tenant-authority state. "Fresh authorization" is required but not defined against the tenant lifecycle source. As written, an implementation could satisfy the prose while treating an old cached active/member result as sufficient.

Product-level reconciliation:

- Require active, current tenant authority for protected Folder operations.
- Define disabled, unknown, missing, stale, and authority-unavailable states as fail-closed for mutation and protected reads, with one safe/non-enumerating error family and explicit retry/refresh semantics.
- State that tenant re-enable, member removal, and role downgrade change effective Folder permissions within the approved authorization-revalidation/revocation SLO.
- Bind these cases to the canonical authorization matrix and C7 evidence rather than selecting cache, query, event, or polling mechanics in the PRD.

### 2. Tenant configuration ownership is inconsistent with the research and under-specified in the PRD

Research evidence:

- `Hexalith.Tenants` owns tenant configuration as well as tenant lifecycle and membership.
- Folders may consume only Folder-owned tenant configuration, illustrated as `folders.*`, while unrelated tenant configuration is ignored.
- Local Folder state is a derived projection/cache, not a second tenant-configuration authority.

PRD coverage:

- **Architectural Boundaries** assigns tenant identity, lifecycle, and membership to Tenants, but omits tenant configuration from that ownership statement.
- The PRD assigns folder-specific policy and provider-binding references to Folders and requires tenant-scoped organization/provider configuration, without saying whether any tenant-level Folders configuration is sourced from Tenants.

Gap/contradiction:

The PRD leaves two plausible authorities for tenant-level Folder configuration. It also does not say whether `folders.*` tenant configuration is an MVP input, what product behavior it may control, or whether such events/fields must be ignored. That ambiguity can create duplicated configuration sources or silently divergent policy.

Product-level reconciliation:

- Decide explicitly whether tenant-level Folder configuration from `Hexalith.Tenants` is in MVP.
- If in scope, name the supported Folder-owned configuration classes and observable effects, retain Folder-specific policy ownership, and require unrelated or unknown tenant configuration to have no effect.
- If out of scope, state that Folders does not consume Tenants configuration in MVP and that provider bindings, folder ACLs, and Folder policy remain governed only by the canonical Folders contracts.
- Keep topic names, handlers, key prefixes, projection DTOs, and storage layout in architecture/contracts.

### 3. Release evidence does not explicitly exercise tenant-authority propagation

Research evidence:

The proposed security/integration suite covers tenant disabled/enabled, user removal, role downgrade, duplicate tenant-fact delivery, stale/missing authority, wrong-tenant routes, cross-tenant folder-ID guessing, Folder-owned configuration filtering, and safe handling of the Tenants event/query enum representations.

PRD coverage:

- MVP evidence covers broad unauthorized access, cross-tenant identifiers, async authorization revalidation, and duplicate delivery.
- Quality gates cover cross-tenant isolation, idempotency, redaction, and read-model determinism.

Gap:

The current evidence can pass without proving the specific dependency on Tenants lifecycle and membership changes. It does not explicitly require tests showing that disable/re-enable, removal, or downgrade changes Folder access, or that missing/stale tenant authority fails closed. Generic duplicate-delivery evidence also does not prove that duplicate tenant-authority updates leave effective permissions unchanged.

Product-level reconciliation:

- Add the tenant lifecycle/membership scenarios above to MVP authorization acceptance evidence and the authorization-matrix denominator.
- Require an observable revocation-effect result, not a particular event-consumer design.
- Treat integer-event/string-query enum compatibility, unknown enum values, subscription delivery, deduplication storage, and ordering as contract/architecture test detail unless those representations are exposed by a Folders public contract.

### 4. The PRD lacks an explicit security outcome for internal service/event boundaries

Research evidence:

The development Tenants Dapr access policy is permissive and unsuitable for production. Production requires authenticated service-to-service communication and deny-by-default authorization; the research recommends Dapr mTLS and app-ID access control.

PRD coverage:

The Security NFR is strong on tenant/object authorization, secret leakage, provider credential isolation, and asynchronous revalidation, but it does not say that internal command, query, projection, or tenant-fact ingress endpoints must reject unauthorized service callers.

Gap:

Tenant checks alone do not define the trust boundary for internal service invocation or event delivery. A deployment could meet the application-level tenant rules while exposing an internal integration endpoint more broadly than intended.

Product-level reconciliation:

- Add a technology-neutral NFR that production service-to-service and event-ingress paths are mutually authenticated, explicitly authorized, least-privilege, and deny by default.
- Require release evidence that an unapproved service identity cannot invoke protected internal endpoints or inject tenant-authority facts.
- Keep Dapr mTLS, app-ID policies, component YAML, topic ACLs, and network policy in architecture/deployment artifacts.

## Research content already preserved; no PRD change needed

- Separate Tenants and Folders bounded contexts and sources of truth.
- Host-derived/authenticated tenant context rather than trusting payload tenant IDs.
- Fresh authorization before asynchronous protected side effects and fail-closed revocation.
- Folder-specific ACLs layered on tenant authorization.
- Managed-tenant scoping of locks and normal Folder work.
- No implicit global-admin/break-glass cross-tenant browsing in MVP; operators also require normal tenant/folder authority.
- Cross-tenant non-enumeration, secret-safe errors, projection freshness visibility, duplicate-safe mutations, and broad isolation testing.

## Technical mechanisms correctly kept downstream

The following are useful architecture, integration, and implementation inputs, but should not be copied into the PRD as product requirements unless they become externally observable contract commitments:

- the `system.tenants.events` topic, `/tenants/events` route, CloudEvents middleware, and `AddHexalithTenants`/subscription registration calls;
- exact command envelopes (`tenant = system`, `domain = tenants`) and aggregate-key strings such as `system:tenants:{tenantId}` or `{managedTenantId}:folders:{folderId}`;
- project/package selection for `Hexalith.Tenants.Contracts`, `.Client`, `.Aspire`, `.Testing`, and the temporary project-reference recommendation;
- Redis/Dapr state-store choice, projection key prefixes, deduplication-store shape, event provenance fields, partitioning, handler concurrency, bulkheads, retry policies, and dead-letter plumbing;
- AppHost resource composition, Keycloak bootstrap, Dapr YAML, explicit mTLS mechanism, and sidecar app IDs;
- concrete event handler class names and serialization bridging for Tenants integer event enums versus string query enums.

These mechanisms should be validated by architecture/contract/implementation artifacts against the product outcomes above.
