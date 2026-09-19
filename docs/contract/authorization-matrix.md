# Canonical Authorization Matrix

Status: `generated-candidate-awaiting-a6b` — PD10 v2 artifacts are generated; A6b, runtime, and incident-access evidence remain incomplete.

Matrix version: `2.0.0`

Historical approval: version `1.0.0`, approved `2026-09-14`; that digest is not current release authority.

Required A6b approval: Product + Architecture + Security against the final version `2.0.0` digest.

Canonical matrix artifact: `docs/contract/authorization-matrix.md`

Historical v1 governance evidence: `docs/contract/oq3-authorization-evidence.yaml`; it remains immutable and
does not approve this matrix. A6b must bind fresh approvals to this exact matrix digest and the generated v2
conformance-set digest after review.

Denominator scope: 14 canonical access states, 11 protected operation families, 49 observed v1 operation identities, and 8 FR8 scope dimensions. The v2 gate replaces the observed v1 route/status inventory with the generated v2 inventory while preserving operation identity uniqueness.

This contract-only document is the canonical OQ3 authorization denominator. It is the single inventory that the
authorization completeness gate counts against: every canonical actor, every canonical negative access state,
every protected operation family, every current Contract Spine operation, and every FR8 scope dimension appears
here exactly once. The PRD remains the product authority for actor names, family definitions, and FR text; the
Contract Spine remains the machine-readable surface contract and must reproduce this matrix without weakening it.

The matrix is the authorization denominator. The C13 parity classification field `operation_family` is a
transport classification with its own closed vocabulary (`mutating_command`, `query_status`, `context_query`,
`audit`, `operations_console_projection`) and is not repurposed here. The two vocabularies are deliberately
disjoint, and generated parity rows are never hand-edited to satisfy this matrix.

## PD10 Candidate Amendment (Governing Target)

The approved September 15 proposal supersedes the v1 digest and fixes these rules for the candidate that A6b
must reapprove:

- Stale, unavailable, conflicting, or incomplete authority evidence routes to `authority-unavailable-503`.
  Only fresh negative facts route to `safe-denial-404`.
- The corrected external surface is `/api/v2`; `/api/v1` remains historical evidence. The operation inventory
  below records the generated v2 paths; the generated candidate remains unrouted until A6b, Section 9, and A8.
- `GetTaskStatus` becomes `/api/v2/folders/{folderId}/tasks/{taskId}/status`.
  `GetReadinessDiagnostics` becomes
  `/api/v2/folders/{folderId}/ops-console/readiness-diagnostics`; `GetProjectionFreshness` becomes
  `/api/v2/folders/{folderId}/ops-console/projection-freshness`. Fresh tenant and folder authority is
  established before task or diagnostic-resource lookup.
- `ListFolderAclEntries` requires folder `administer`; `GetEffectivePermissions` permits self-inspection under
  folder read authority and may carry an optional task context; `ValidateProviderReadiness` requires tenant
  folder-create authority and the architecture S-9 endpoint policy.
- `visibility` is required on every v2 error. `redacted`, `withheld`, `unavailable`, and `absent` are distinct;
  PD8 owns `withheld`. Post-authentication 403 and caller-visible `not_found`,
  `cross_tenant_access_denied`, and `audit_access_denied` are absent from protected v2 responses.

Within Story 1.17's scope, relock-only milestone `1.17-GENERATE` must generate the v2 Spine, client, CLI/MCP
adapters and parity fixtures, status/error drift surface, `previous-spine.yaml`, C13 inventory, UI migration,
and consumer-discovery evidence from this candidate. Product + Architecture + Security then perform A6b over
the exact final matrix `2.0.0` digest. Section 9 conformance and A8 follow; the canonical Story 1.17 lifecycle
row does not close before those gates. No generated artifact or lifecycle status is changed by this document update.

## What This Matrix Does Not Claim

This is an approval-pending target decision, not runtime evidence. The historical v1 approval and this candidate do not claim any of the
following, and none of the dependent stories may be closed because this package is approved:

- Runtime authorization behavior, layering, or enforcement in a deployed environment.
- C7 lock-renewal, authorization-revalidation, or revocation-effect timing evidence.
- Story 12.1 durable events, repositories, or projections that a real authorization decision would read.
- OQ9 incident-access evidence for the incident-evidence family.
- Completed PD10 Contract Spine remediation. The observed v1 drift stays recorded below; Story 1.17 closes it
  in generated v2 artifacts and then obtains A6b approval.

## Canonical Access States

Fourteen canonical access states form the row dimension: the six canonical actors from the PRD Actors table and
all eight canonical negative states below, including absent resource and insufficient scope. The completeness
gate counts exactly those 14 rows; none is an uncounted outcome case.

### Canonical Actors

| Access state | Canonical actor | Defining authority | Effective access rule |
| --- | --- | --- | --- |
| `tenant-administrator` | Tenant administrator | Hexalith.Tenants tenant authority | Tenant authority intersected with allow-only folder grants; A23 supplies the administer grant on every folder in the managed tenant. |
| `tenant-member` | Tenant member | Tenant membership plus folder grants | Tenant membership intersected with allow-only folder grants assigned directly or through active group and role membership. |
| `delegated-service-agent` | Delegated service agent | Delegating principal authority plus agent grant | Intersection of the delegating principal current effective access, the agent explicit folder grant, and the delegated operation-family and task scope; delegation never elevates. |
| `tenant-scoped-operator` | Tenant-scoped operator | Operator permission plus explicit tenant and folder authorization | Operator permission intersected with ordinary tenant and folder authorization; no cross-tenant or unscoped browsing. |
| `audit-reviewer` | Audit reviewer | Audit-read grant plus tenant and folder authorization | Audit-reviewer role intersected with ordinary tenant and folder authorization. |
| `incident-administrator` | Incident administrator | Incident-admin permission held by an operator plus fresh tenant and folder authorization | Incident-admin permission intersected with fresh tenant authority and a fresh folder read grant; read-only with no mutation or repair path. The read-only property describes incident access itself; an explicit folder write grant is ordinary tenant-member access evaluated separately and is never conferred by the incident-admin permission. |

An access state is defined by its authority and permission set. A principal who acquires another access state
defining permission is evaluated as that access state, which is why a cell reads `deny` rather than
`allow-explicit-grant` when the missing conjunct is another access state defining permission.

A principal may hold several defining permissions at once. Because effective access is the union of applicable
allow-only grants, such a principal is evaluated as the union of those access states' allows: an operator who
also holds the audit-reviewer role receives both `console-view` and `audit-read`. A `deny` in one access state
row never subtracts an `allow` the same principal holds through another access state.

### Canonical Negative Access States

Every negative access state applies to all 11 operation families and is evaluated before any protected-resource
observation. Fresh negative facts return the exact canonical 404 safe denial; stale or otherwise unusable
authority evidence returns the exact canonical 503 authority-unavailable outcome. Each emits exactly one bounded
metadata-only denial audit record.

| Negative access state | Condition | Evaluated at | Canonical outcome | Families covered |
| --- | --- | --- | --- | --- |
| `wrong-tenant` | The authoritative tenant context does not match the requested resource tenant. | tenant-access evaluation, before any protected-resource lookup | `safe-denial-404` | all-11 |
| `revoked` | Tenant membership, folder grant, delegated authority, provider binding, or credential permission was revoked. | tenant-access and folder-ACL evaluation, and again at every pre-side-effect revalidation | `safe-denial-404` | all-11 |
| `stale` | Identity, membership, delegation, or folder-ACL evidence is stale, conflicting, or incomplete. | authority-evidence evaluation, before any protected-resource lookup | `authority-unavailable-503` | all-11 |
| `disabled` | The managed tenant or the principal is disabled. | tenant-access evaluation, before any protected-resource lookup | `safe-denial-404` | all-11 |
| `unknown` | The managed tenant, principal, delegation, or target identity is unknown. | tenant-access and folder-ACL evaluation, before any protected-resource lookup | `safe-denial-404` | all-11 |
| `hidden-resource` | The caller holds no applicable allow on the target folder or resource. | folder-ACL evaluation, before any protected-resource lookup | `safe-denial-404` | all-11 |
| `absent-resource` | The requested resource does not exist for the authoritative tenant. | folder-ACL evaluation, before any protected-resource lookup | `safe-denial-404` | all-11 |
| `insufficient-scope` | The caller holds an allow on the folder but not the requested operation family. | family-grant evaluation, before any protected-resource lookup | `safe-denial-404` | all-11 |

## Protected Operation Families

All 11 product-owned families are retained. `incident-evidence` has no current public Contract Spine operation
and is retained with a zero operation count and an OQ9 downstream owner rather than being dropped.

| Family | Scope | Authority conjunct | Governing conjuncts | FRs | Operations |
| --- | --- | --- | --- | --- | --- |
| `provider-configuration` | tenant | tenant-administrator authority | fresh tenant authority; tenant-administrator authority; provider binding owned by the authoritative managed tenant | FR4, FR15 | 2 |
| `readiness-and-provider-evidence` | tenant | folder-create permission or tenant-administrator authority or operator permission | fresh tenant authority; folder-create permission for readiness validation, or tenant-administrator authority or operator permission for provider support evidence | FR7, FR16, FR22, FR23, FR57 | 2 |
| `folder-creation` | tenant | tenant folder-create permission | fresh tenant authority; tenant folder-create permission | FR11 | 1 |
| `incident-evidence` | tenant | incident-admin permission | fresh tenant authority; incident-admin permission; fresh folder read grant evaluated before any stream lookup, event counting, checkpoint lookup, filtering, or shaping | FR56 | 0 |
| `folder-administration` | folder | folder administer grant | fresh tenant authority; folder administer grant; archive state permitting the requested change | FR5, FR13, FR18, FR19, FR20 | 6 |
| `task-mutation` | folder | folder write grant | fresh tenant authority; folder write grant; prepared workspace; lock held by the requesting task; task binding from A5; re-authorization immediately before the side effect | FR24, FR25, FR29, FR32, FR33, FR37 | 7 |
| `context-read` | folder | folder write grant | fresh tenant authority; folder write grant per A22; path policy; sensitivity classification; C4 bounds | FR34, FR35 | 5 |
| `status-permission-and-lock-inspection` | folder | folder read grant | fresh tenant authority; folder read grant; workspace, task, commit, or reconciliation scope binding where the operation carries one | FR6, FR12, FR26, FR30, FR31, FR46 | 13 |
| `audit-read` | folder | folder read grant and audit-reviewer role | fresh tenant authority; folder read grant; audit-reviewer role; diagnostic audience partition | FR53, FR54 | 4 |
| `console-view` | folder | folder read grant and operator permission | fresh tenant authority; folder read grant; operator permission; diagnostic audience partition; read-only projection access | FR52 | 7 |
| `index-search` | folder | folder read grant | fresh tenant authority; folder read grant; security trim to current tenant, folder, and workspace authority; authoritative hydration before egress | FR58 | 2 |

The operation counts total 49, which equals the current Contract Spine inventory.

## Grant Representation

The public folder-grant representation stays `read`, `write`, and `administer`. `write` implies `read`;
`administer` implies neither `read` nor `write`. Context reads require `write`. Tenant administrators and the
creator of a logical folder receive `administer`. Tenant-level families are granted by tenant authority or an
operator permission and have no folder ACL entry. Operation families are the evaluation, audit, and conformance
vocabulary; they are not a new public grant shape and this matrix does not introduce per-family wire grants.

Assumptions A5, A12, A17, A22, and A23 are approved as written in the PRD and are load-bearing in the decisions
below. MVP folder ACLs carry no explicit deny entries: absence of an applicable allow denies. Effective access
is the intersection of current active tenant authority and the union of applicable allow-only grants.

## Evaluation Order And Safe Outcomes

### Layer Order

The S-4 authorization layering is preserved and is not reordered by this matrix:

```text
jwt_validation -> eventstore_claim_transform -> tenant_access_projection_fail_closed_on_stale -> folder_acl -> eventstore_validators -> dapr_deny_by_default_policies_and_mtls
```

### Conjunct Order

Within that layering, every protected operation evaluates its conjuncts in this order and reaches observation
only after all of them pass:

```text
authentication -> authority_evidence_availability -> tenant_access -> principal_and_delegation_intersection -> folder_acl_allow -> family_grant -> resource_scope_binding -> freshness_revalidation -> observation
```

No implementation may look up, count, filter, shape, rank, or otherwise observe a protected resource before the
conjunct that protects it. Cross-tenant access is denied before file, workspace, credential, repository, lock,
commit, provider, audit, or index access. Authorization is revalidated immediately before each side effect, so
authorization only at request receipt is insufficient for asynchronous work.

### Canonical Outcomes

| Outcome | Status | Category | Code | Retryable | Client action | Details visibility | When |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `authentication-failure-401` | 401 | `authentication_failure` | `authentication_required` | false | `check_credentials` | `redacted` | The caller is not authenticated. Evaluated before every other conjunct. |
| `safe-denial-404` | 404 | `tenant_access_denied` | `resource_unavailable` | false | `no_action` | `redacted` | Any authenticated absent, hidden, wrong-tenant, revoked, disabled, unknown, or insufficient-scope state. One exact envelope for all causes. |
| `authority-unavailable-503` | 503 | `read_model_unavailable` | `projection_unavailable` | true | `retry` | `redacted` | Trusted tenant, membership, or delegation evidence is stale or unavailable. Evaluated before any protected-resource lookup. |

Post-authentication 403 is retired from the canonical design. Only correlation identity and the per-request
instance identifier may differ between two `safe-denial-404` responses; status, category, code, message, and
detail keys are identical across every cause. The `authority-unavailable-503` outcome preserves retry semantics
without existence hints and never reveals whether the target resource exists.

An authorized reader of an unbound or policy-less folder receives its explicit lifecycle and binding status
rather than the safe denial. A caller who holds at least one allow on a folder but lacks the requested
operation family is the `insufficient-scope` state and receives the same exact `safe-denial-404`; this matrix
declares no second post-authentication denial shape.

### Audit Evidence

Every allow and every deny emits exactly one metadata-only audit record carrying exactly these six fields:

```text
actor, tenant, operation, operation_family, result, correlation_id
```

No protected identifier, path, payload, credential, provider response, or existence hint enters allow or deny
audit evidence, and no evidence produced for this matrix contains protected data.

## Access State By Operation Family

The decision vocabulary is closed:

| Decision | Meaning |
| --- | --- |
| `allow` | The access state defining authority satisfies the family authority conjunct; the operation is allowed when the remaining governing conjuncts hold. |
| `allow-explicit-grant` | The defining authority does not satisfy the family grant; the operation is allowed only when this principal separately holds the required folder grant or tenant folder-create permission. |
| `allow-delegated-intersection` | Allowed only within the intersection of delegator effective access, the agent grant, and the delegated family and task scope; delegation never elevates. |
| `deny` | The family is unreachable for this access state; every attempt returns the canonical safe denial. |

| Access state | Operation family | Decision | Governing conjuncts | Outcome when a conjunct is missing |
| --- | --- | --- | --- | --- |
| `tenant-administrator` | `provider-configuration` | `allow` | Tenant-administrator authority satisfies the family authority conjunct for both configuration and binding inspection. | `safe-denial-404` |
| `tenant-administrator` | `readiness-and-provider-evidence` | `allow` | Tenant-administrator authority opens provider support evidence; readiness validation additionally requires the folder-create permission. | `safe-denial-404` |
| `tenant-administrator` | `folder-creation` | `allow-explicit-grant` | Tenant authority does not imply the tenant folder-create permission; the permission must be held explicitly. | `safe-denial-404` |
| `tenant-administrator` | `incident-evidence` | `deny` | The incident-admin permission defines the incident-administrator access state; a tenant administrator who receives it is evaluated as that access state. | `safe-denial-404` |
| `tenant-administrator` | `folder-administration` | `allow` | A23 gives tenant-administrator authority the administer grant on every folder in the managed tenant. | `safe-denial-404` |
| `tenant-administrator` | `task-mutation` | `allow-explicit-grant` | A12 and A17 deny task mutation to administer alone; an explicit folder write grant is required. | `safe-denial-404` |
| `tenant-administrator` | `context-read` | `allow-explicit-grant` | A22 reaches file bodies only through the folder write grant; administer implies neither read nor write. | `safe-denial-404` |
| `tenant-administrator` | `status-permission-and-lock-inspection` | `allow-explicit-grant` | A17 keeps administer distinct from read; an explicit folder read grant is required. | `safe-denial-404` |
| `tenant-administrator` | `audit-read` | `deny` | The audit-reviewer role defines the audit-reviewer access state; a tenant administrator who receives it is evaluated as that access state. | `safe-denial-404` |
| `tenant-administrator` | `console-view` | `deny` | The operator permission defines the tenant-scoped-operator access state; a tenant administrator who receives it is evaluated as that access state. | `safe-denial-404` |
| `tenant-administrator` | `index-search` | `allow-explicit-grant` | A17 keeps administer distinct from read; an explicit folder read grant is required. | `safe-denial-404` |
| `tenant-member` | `provider-configuration` | `deny` | Tenant membership never carries tenant-administrator authority; configuration changes require escalation. | `safe-denial-404` |
| `tenant-member` | `readiness-and-provider-evidence` | `allow-explicit-grant` | The folder-create permission opens readiness validation only; provider support evidence stays denied without tenant-administrator authority or the operator permission. | `safe-denial-404` |
| `tenant-member` | `folder-creation` | `allow-explicit-grant` | The tenant folder-create permission must be held explicitly. | `safe-denial-404` |
| `tenant-member` | `incident-evidence` | `deny` | MVP assigns the incident-admin permission only through the incident-administrator access state. | `safe-denial-404` |
| `tenant-member` | `folder-administration` | `allow-explicit-grant` | An explicit folder administer grant is required; A23 gives it to the creator of a logical folder. | `safe-denial-404` |
| `tenant-member` | `task-mutation` | `allow-explicit-grant` | An explicit folder write grant is required. | `safe-denial-404` |
| `tenant-member` | `context-read` | `allow-explicit-grant` | A22 requires the folder write grant for file bodies. | `safe-denial-404` |
| `tenant-member` | `status-permission-and-lock-inspection` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |
| `tenant-member` | `audit-read` | `deny` | The audit-reviewer role defines the audit-reviewer access state. | `safe-denial-404` |
| `tenant-member` | `console-view` | `deny` | The operator permission defines the tenant-scoped-operator access state. | `safe-denial-404` |
| `tenant-member` | `index-search` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |
| `delegated-service-agent` | `provider-configuration` | `deny` | Tenant-level authority is not delegable; delegation may never elevate beyond the delegating principal. | `safe-denial-404` |
| `delegated-service-agent` | `readiness-and-provider-evidence` | `deny` | Tenant-level readiness and provider-evidence permissions are not delegable in MVP. | `safe-denial-404` |
| `delegated-service-agent` | `folder-creation` | `deny` | The tenant folder-create permission is not delegable in MVP. | `safe-denial-404` |
| `delegated-service-agent` | `incident-evidence` | `deny` | The incident-admin permission is not delegable in MVP. | `safe-denial-404` |
| `delegated-service-agent` | `folder-administration` | `allow-delegated-intersection` | Delegator effective access intersected with the agent folder administer grant and the delegated family and task scope. | `safe-denial-404` |
| `delegated-service-agent` | `task-mutation` | `allow-delegated-intersection` | Delegator effective access intersected with the agent folder write grant, the delegated family and task scope, the prepared workspace, and the held lock. | `safe-denial-404` |
| `delegated-service-agent` | `context-read` | `allow-delegated-intersection` | Delegator effective access intersected with the agent folder write grant and the delegated family and task scope, then path policy and C4 bounds. | `safe-denial-404` |
| `delegated-service-agent` | `status-permission-and-lock-inspection` | `allow-delegated-intersection` | Delegator effective access intersected with the agent folder read grant and the delegated family and task scope. | `safe-denial-404` |
| `delegated-service-agent` | `audit-read` | `deny` | The audit-reviewer role is a role conjunct, not a folder grant, and is not delegable in MVP. | `safe-denial-404` |
| `delegated-service-agent` | `console-view` | `deny` | The operator permission is not delegable in MVP. | `safe-denial-404` |
| `delegated-service-agent` | `index-search` | `allow-delegated-intersection` | Delegator effective access intersected with the agent folder read grant and the delegated family and task scope, then security trim and hydration. | `safe-denial-404` |
| `tenant-scoped-operator` | `provider-configuration` | `deny` | Operators may not change or inspect tenant provider policy; the only operator path is escalation to a tenant administrator. | `safe-denial-404` |
| `tenant-scoped-operator` | `readiness-and-provider-evidence` | `allow` | The operator permission opens provider support evidence; readiness validation additionally requires the folder-create permission. | `safe-denial-404` |
| `tenant-scoped-operator` | `folder-creation` | `allow-explicit-grant` | The tenant folder-create permission must be held explicitly. | `safe-denial-404` |
| `tenant-scoped-operator` | `incident-evidence` | `deny` | An operator who receives the incident-admin permission is evaluated as the incident-administrator access state. | `safe-denial-404` |
| `tenant-scoped-operator` | `folder-administration` | `deny` | Folder ACL grants, archive decisions, and binding policy are tenant-administrator-owned configuration that operators may not change. | `safe-denial-404` |
| `tenant-scoped-operator` | `task-mutation` | `allow-explicit-grant` | An explicit folder write grant is required; the operator permission grants no mutation authority. | `safe-denial-404` |
| `tenant-scoped-operator` | `context-read` | `allow-explicit-grant` | A22 denies file bodies through folder read; an explicit folder write grant is required. | `safe-denial-404` |
| `tenant-scoped-operator` | `status-permission-and-lock-inspection` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |
| `tenant-scoped-operator` | `audit-read` | `deny` | The audit-reviewer role defines the audit-reviewer access state. | `safe-denial-404` |
| `tenant-scoped-operator` | `console-view` | `allow` | The operator permission satisfies the family role conjunct; an explicit folder read grant and the diagnostic audience partition still apply. | `safe-denial-404` |
| `tenant-scoped-operator` | `index-search` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |
| `audit-reviewer` | `provider-configuration` | `deny` | The audit-read grant never carries tenant-administrator authority. | `safe-denial-404` |
| `audit-reviewer` | `readiness-and-provider-evidence` | `allow-explicit-grant` | The folder-create permission opens readiness validation only; provider support evidence stays denied. | `safe-denial-404` |
| `audit-reviewer` | `folder-creation` | `allow-explicit-grant` | The tenant folder-create permission must be held explicitly. | `safe-denial-404` |
| `audit-reviewer` | `incident-evidence` | `deny` | MVP assigns the incident-admin permission only through the incident-administrator access state. | `safe-denial-404` |
| `audit-reviewer` | `folder-administration` | `allow-explicit-grant` | An explicit folder administer grant is required. | `safe-denial-404` |
| `audit-reviewer` | `task-mutation` | `allow-explicit-grant` | An explicit folder write grant is required. | `safe-denial-404` |
| `audit-reviewer` | `context-read` | `allow-explicit-grant` | A22 denies file bodies through folder read; an explicit folder write grant is required. | `safe-denial-404` |
| `audit-reviewer` | `status-permission-and-lock-inspection` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |
| `audit-reviewer` | `audit-read` | `allow` | The audit-reviewer role satisfies the family role conjunct; an explicit folder read grant and the diagnostic audience partition still apply. | `safe-denial-404` |
| `audit-reviewer` | `console-view` | `deny` | The operator permission defines the tenant-scoped-operator access state. | `safe-denial-404` |
| `audit-reviewer` | `index-search` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |
| `incident-administrator` | `provider-configuration` | `deny` | Operators holding the incident-admin permission may not change or inspect tenant provider policy. | `safe-denial-404` |
| `incident-administrator` | `readiness-and-provider-evidence` | `allow` | The underlying operator permission opens provider support evidence; readiness validation additionally requires the folder-create permission. | `safe-denial-404` |
| `incident-administrator` | `folder-creation` | `allow-explicit-grant` | The tenant folder-create permission must be held explicitly. | `safe-denial-404` |
| `incident-administrator` | `incident-evidence` | `allow` | The incident-admin permission satisfies the family authority conjunct; a fresh folder read grant and fresh tenant authorization are evaluated before any observation. | `safe-denial-404` |
| `incident-administrator` | `folder-administration` | `deny` | Folder ACL grants, archive decisions, and binding policy remain tenant-administrator-owned configuration. | `safe-denial-404` |
| `incident-administrator` | `task-mutation` | `allow-explicit-grant` | An explicit folder write grant is required; incident access grants no mutation or repair path. | `safe-denial-404` |
| `incident-administrator` | `context-read` | `allow-explicit-grant` | A22 denies file bodies through folder read; an explicit folder write grant is required. | `safe-denial-404` |
| `incident-administrator` | `status-permission-and-lock-inspection` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |
| `incident-administrator` | `audit-read` | `deny` | The audit-reviewer role defines the audit-reviewer access state. | `safe-denial-404` |
| `incident-administrator` | `console-view` | `allow` | The underlying operator permission satisfies the family role conjunct; an explicit folder read grant and the diagnostic audience partition still apply. | `safe-denial-404` |
| `incident-administrator` | `index-search` | `allow-explicit-grant` | An explicit folder read grant is required. | `safe-denial-404` |

## Operation Mapping

Every current Contract Spine operation maps to exactly one family. Operation identities are preserved exactly as
the Contract Spine declares them; no operation is added, renamed, or removed by this matrix. The scope columns
account for all eight FR8 scope dimensions on every row: a dimension is applicable when the authorization
decision for that operation must evaluate it, and is listed as not applicable when the operation carries no such
scope. A dimension that is silently absent from a decision is a failing conformance defect.

| Operation | Method | Path | Family | Applicable scope dimensions | Not-applicable scope dimensions |
| --- | --- | --- | --- | --- | --- |
| `CreateFolder` | POST | `/api/v2/folders` | `folder-creation` | `tenant` `principal` `delegated-actor` `task` | `provider` `repository` `folder` `workspace` |
| `GetFolderLifecycleStatus` | GET | `/api/v2/folders/{folderId}/lifecycle-status` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |
| `ArchiveFolder` | POST | `/api/v2/folders/{folderId}/archive` | `folder-administration` | `tenant` `principal` `delegated-actor` `folder` `task` | `provider` `repository` `workspace` |
| `ListFolderAclEntries` | GET | `/api/v2/folders/{folderId}/acl` | `folder-administration` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |
| `UpdateFolderAclEntry` | PUT | `/api/v2/folders/{folderId}/acl/{aclEntryId}` | `folder-administration` | `tenant` `principal` `delegated-actor` `folder` `task` | `provider` `repository` `workspace` |
| `GetEffectivePermissions` | GET | `/api/v2/folders/{folderId}/effective-permissions` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |
| `ConfigureProviderBinding` | PUT | `/api/v2/provider-bindings/{providerBindingRef}` | `provider-configuration` | `tenant` `principal` `delegated-actor` `provider` `task` | `repository` `folder` `workspace` |
| `GetProviderBinding` | GET | `/api/v2/provider-bindings/{providerBindingRef}` | `provider-configuration` | `tenant` `principal` `delegated-actor` `provider` | `repository` `folder` `workspace` `task` |
| `ValidateProviderReadiness` | POST | `/api/v2/provider-readiness/validations` | `readiness-and-provider-evidence` | `tenant` `principal` `delegated-actor` `provider` | `repository` `folder` `workspace` `task` |
| `GetProviderSupportEvidence` | GET | `/api/v2/provider-readiness/support-evidence` | `readiness-and-provider-evidence` | `tenant` `principal` `delegated-actor` `provider` | `repository` `folder` `workspace` `task` |
| `CreateRepositoryBackedFolder` | POST | `/api/v2/folders/repository-backed` | `folder-administration` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `task` | `workspace` |
| `BindRepository` | POST | `/api/v2/folders/{folderId}/repository-bindings` | `folder-administration` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `task` | `workspace` |
| `GetRepositoryBinding` | GET | `/api/v2/folders/{folderId}/repository-bindings/{repositoryBindingId}` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` | `workspace` `task` |
| `ConfigureBranchRefPolicy` | PUT | `/api/v2/folders/{folderId}/branch-ref-policy` | `folder-administration` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `task` | `workspace` |
| `GetBranchRefPolicy` | GET | `/api/v2/folders/{folderId}/branch-ref-policy` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` | `workspace` `task` |
| `PrepareWorkspace` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/preparation` | `task-mutation` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` `task` | none |
| `LockWorkspace` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/lock` | `task-mutation` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` `task` | none |
| `GetWorkspaceLock` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/lock` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` | `task` |
| `ReleaseWorkspaceLock` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/lock/release` | `task-mutation` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` `task` | none |
| `GetWorkspaceRetryEligibility` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/retry-eligibility` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `GetWorkspaceTransitionEvidence` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/transition-evidence` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `AddFile` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/files/add` | `task-mutation` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `ChangeFile` | PUT | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/files/change` | `task-mutation` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `RemoveFile` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/files/remove` | `task-mutation` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `ListFolderFiles` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/tree` | `context-read` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `GetFolderFileMetadata` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/metadata` | `context-read` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `SearchFolderFiles` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/search` | `context-read` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `SearchFolderIndexedFiles` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/index-search` | `index-search` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `GetFolderIndexingStatus` | GET | `/api/v2/folders/{folderId}/indexing-status` | `index-search` | `tenant` `principal` `delegated-actor` `folder` `task` | `provider` `repository` `workspace` |
| `GlobFolderFiles` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/glob` | `context-read` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `ReadFileRange` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/context/range-read` | `context-read` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `CommitWorkspace` | POST | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/commits` | `task-mutation` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` `task` | none |
| `GetWorkspaceStatus` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/status` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `folder` `workspace` | `provider` `repository` `task` |
| `GetWorkspaceCleanupStatus` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/cleanup/status` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `GetTaskStatus` | GET | `/api/v2/folders/{folderId}/tasks/{taskId}/status` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `folder` `workspace` `task` | `provider` `repository` |
| `GetCommitEvidence` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/evidence` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` | `task` |
| `GetProviderOutcome` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/commits/{operationId}/provider-outcome` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` | `task` |
| `GetReconciliationStatus` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/reconciliation/{reconciliationId}/status` | `status-permission-and-lock-inspection` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` | `task` |
| `ListAuditTrail` | GET | `/api/v2/folders/{folderId}/audit-trail` | `audit-read` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |
| `GetAuditRecord` | GET | `/api/v2/folders/{folderId}/audit-trail/{auditRecordId}` | `audit-read` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |
| `ListOperationTimeline` | GET | `/api/v2/folders/{folderId}/operation-timeline` | `audit-read` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |
| `GetOperationTimelineEntry` | GET | `/api/v2/folders/{folderId}/operation-timeline/{timelineEntryId}` | `audit-read` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |
| `GetReadinessDiagnostics` | GET | `/api/v2/folders/{folderId}/ops-console/readiness-diagnostics` | `console-view` | `tenant` `principal` `delegated-actor` `provider` `folder` | `repository` `workspace` `task` |
| `GetLockDiagnostics` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/lock-diagnostics` | `console-view` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` | `task` |
| `GetDirtyStateDiagnostics` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/dirty-state-diagnostics` | `console-view` | `tenant` `principal` `delegated-actor` `folder` `workspace` | `provider` `repository` `task` |
| `GetFailedOperationDiagnostics` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/failed-operation-diagnostics` | `console-view` | `tenant` `principal` `delegated-actor` `folder` `workspace` | `provider` `repository` `task` |
| `GetProviderStatusDiagnostics` | GET | `/api/v2/folders/{folderId}/ops-console/provider-status-diagnostics` | `console-view` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` | `workspace` `task` |
| `GetSyncStatusDiagnostics` | GET | `/api/v2/folders/{folderId}/workspaces/{workspaceId}/ops-console/sync-status-diagnostics` | `console-view` | `tenant` `principal` `delegated-actor` `provider` `repository` `folder` `workspace` | `task` |
| `GetProjectionFreshness` | GET | `/api/v2/folders/{folderId}/ops-console/projection-freshness` | `console-view` | `tenant` `principal` `delegated-actor` `folder` | `provider` `repository` `workspace` `task` |

### Scope Dimension Rules

| Dimension | Applicable when |
| --- | --- |
| `tenant` | Always. Every decision evaluates the authoritative managed tenant from the authenticated request and the EventStore envelope. |
| `principal` | Always. Every decision evaluates the acting principal and its active group and role membership. |
| `delegated-actor` | Always. Every decision evaluates the delegation conjunct so that a delegated caller can never exceed the delegating principal. |
| `provider` | The decision must evaluate a provider binding tenant ownership, credential-reference scope, or capability policy, or the serializing lock identity that includes provider identity. Operations whose path or parameters name a provider carry Spine evidence; the remainder are derived from product rules and recorded in gap `G11`. |
| `repository` | The decision must evaluate a repository binding tenant and folder ownership or its normalized target ref. Operations whose path or parameters name a repository carry Spine evidence; the remainder are derived from product rules and recorded in gap `G11`. |
| `folder` | The operation declares a folder path parameter, carries a folder identity in its request body, or reaches its target through a task-to-workspace-to-folder binding. |
| `workspace` | The operation declares a workspace path parameter, or the target is reached through the A5 task-to-workspace binding. |
| `task` | The operation declares a task identity parameter or header. The task identity is a correlation key bound to the principal and delegation scope, never a bearer credential. |

## Runtime Posture

| Aspect | Status |
| --- | --- |
| Matrix design decision | generated 2.0.0 candidate; A6b approval pending |
| Runtime authorization enforcement | incomplete |
| Contract Spine conformance | generated candidate; Section 9 pending |
| Incident-evidence operation surface | absent |

This matrix resolves the PD10 architecture decision but does not close A6b or implementation. It does not complete Story 12.1, Story 4.19, Story 4.20,
Story 4.21, Story 6.14, or Story 10.8, and it does not satisfy FR8, FR9, or FR10 runtime evidence.

## Recorded Conformance Gaps

Observed deviations are recorded rather than disguised. Each gap names a bounded description, a downstream
owner, and a repository-relative evidence path.

| Gap | Surface | Description | Downstream owner | Evidence path | Operations |
| --- | --- | --- | --- | --- | --- |
| `G1` | contract-spine | The Contract Spine declares two status-distinct safe-denial envelopes after authentication: 403 on 49 of 49 operations and 404 on 46 of 49. The canonical design retires post-authentication 403 and keeps one exact 404. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | none |
| `G2` | contract-spine | Enumeration-leaking categories remain declared as caller-visible responses on 24 of 49 operations: not_found on 22, cross_tenant_access_denied on 6, and audit_access_denied on 4. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | none |
| `G3` | contract-spine | No exact authority-unavailable envelope is declared. 45 of 49 operations declare a 503 at all, and the shared read-model example carries a metadata_only visibility rather than the non-disclosing redacted visibility this matrix requires. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | none |
| `G4` | runtime | The deployed effective-permission action catalog maps 22 actions onto three folder levels with recorded drift: its file-content read and context-search actions map to read while this matrix requires write per A22; its repository-bind and repository-backed-folder-create actions map to write while folder administration requires administer; its folder-create action maps to administer, its provider-binding-configure action maps to administer, and its provider-readiness-read action maps to read although folder creation, provider configuration, and readiness and provider evidence are tenant-level families with no folder ACL entry; and the catalog carries no audit-reviewer role, operator permission, incident-admin permission, or incident-evidence action. | Story 12.1 and Epic 13 runtime authorization work | `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs` | none |
| `G5` | contract-spine | The console-view family is folder-scoped in the PRD, but two console operations are tenant-scoped in the Contract Spine and carry no folder path parameter, so the folder scope dimension is recorded as not applicable for them. Both replace the folder read conjunct with the requirement the Spine already declares for them: tenant access plus operator diagnostic scope plus the diagnostic audience partition, with no folder grant. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | GetReadinessDiagnostics GetProjectionFreshness |
| `G6` | product-inventory | The incident-evidence family retains its actor rows but has no current public Contract Spine operation, so its operation count is zero and its runtime evidence is not claimed. | OQ9 incident-access evidence | `_bmad-output/planning-artifacts/prd.md` | none |
| `G7` | runtime | Runtime authorization layering, C7 revocation timing, and the durable projections the decision path reads are not proven. This matrix is a design denominator and claims no executable authorization behavior. | Story 12.1 durable persistence and C7 runtime evidence | `_bmad-output/planning-artifacts/epics.md` | none |
| `G8` | contract-spine | ListFolderAclEntries binds to the folder administer grant in this matrix because listing ACL entries exposes grant structure, while the Contract Spine declares a separate folder-acl-read-permission requirement token. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | ListFolderAclEntries |
| `G9` | contract-spine | FR6 names a folder or task context for effective permissions, but GetEffectivePermissions declares no task parameter, so the task scope dimension is recorded as not applicable for it. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | GetEffectivePermissions |
| `G10` | contract-spine | The Contract Spine per-operation authorization requirement tokens are free-form prose, so comparing them against this matrix is not gate-mechanizable and the divergences are recorded here instead: GetTaskStatus declares tenant access and task scope with no folder ACL while this matrix binds a folder read grant, and ValidateProviderReadiness declares a provider-readiness-read token while this matrix family requires the folder-create permission. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | GetTaskStatus ValidateProviderReadiness |
| `G11` | contract-spine | Provider and repository scope applicability for these operations is derived from product rules rather than from Contract Spine path or parameter evidence: provider binding use, the serializing lock identity of managed tenant plus canonical provider and repository identity plus normalized target ref, and provider commit, outcome, and reconciliation evidence. The Spine declares no provider or repository reference in their paths or parameters, so the binding cannot be checked mechanically against the surface contract. | PD10 (Architecture with Contract and Delivery and Security) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | BindRepository CommitWorkspace ConfigureBranchRefPolicy CreateRepositoryBackedFolder GetBranchRefPolicy GetCommitEvidence GetLockDiagnostics GetProviderOutcome GetProviderStatusDiagnostics GetReadinessDiagnostics GetReconciliationStatus GetRepositoryBinding GetSyncStatusDiagnostics GetWorkspaceLock LockWorkspace PrepareWorkspace ReleaseWorkspaceLock |

## Reopen Policy

The candidate becomes current release authority only when Product, Architecture, and Security approve the exact
final `2.0.0` SHA-256 digest and the approval evidence manifest records all three identities and date. Any later
change to canonical matrix content, version, digest, required authority set, approver identity, or approval date
reopens the same three approvals. The version `1.0.0` approval remains historical evidence only.
