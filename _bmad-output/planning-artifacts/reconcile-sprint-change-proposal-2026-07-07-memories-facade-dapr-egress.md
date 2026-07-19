# Reconciliation — Sprint Change Proposal: Memories Read-Facade Egress Policy

**Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-memories-facade-dapr-egress.md`  
**Source date:** 2026-07-07 19:28 CEST  
**Compared with:** `_bmad-output/planning-artifacts/prd.md` (final, updated 2026-07-14) and `_bmad-output/planning-artifacts/.memlog.md` (updated 2026-07-14)  
**Addendum:** None exists in the bound PRD workspace.  
**Reconciliation disposition:** **No PRD edit. Existing internal-boundary, FR58, failure, and OQ5 requirements govern.** Retain the source as architecture/security implementation evidence, with a downstream production-override guard still needing proof.

## Source purpose and status

The source corrects a mismatch between the claimed security architecture and the actual Story 10.5 Memories read path. Architecture and Dapr policy artifacts claimed production deny-by-default service invocation plus mTLS for `folders → memories GET /api/search`, while the live typed client used a direct `HttpClient` base address and therefore bypassed the Dapr policy path.

The chosen branch was to make the claimed enforcement real: compose the Memories base address as the local Dapr sidecar service-invocation path, preserve an explicit absolute direct URL override for hermetic/non-Dapr use, update architecture and the Story 10.5 completion record, add configuration-resolution tests, and close the deferred-work ledger entry.

The source says all five edit proposals were approved in incremental review and classifies the work as minor. It does not contain a post-implementation verification result showing that all edits, tests, architecture correction, and ledger update were actually applied; its handoff remains phrased as future action. The decision is approved, but completion should not be inferred from this source alone.

The source explicitly carries live through-sidecar proof as pending. Its local bar is build, hermetic endpoint-composition tests, and Dapr-policy conformance tests.

## Product-level requirements and decisions relevant to the PRD

The source reinforces these product requirements and security decisions:

- Internal service calls that contribute to protected Folders behavior must be deny-by-default; a policy artifact that the runtime path bypasses is not effective enforcement.
- Network/service-identity controls are a coarse service-to-service envelope and do not replace Folders' tenant, folder, workspace, and current-authority checks.
- FR58 results remain security-trimmed and hydrated against authoritative Folders state before egress.
- Memories or sidecar failure must degrade explicitly and safely to unavailable behavior rather than leak results or return an uncontrolled server error.
- Security claims, deployed routing, policy fixtures, conformance tests, and architecture text must describe the same operative path.

The choice of Dapr service invocation and the exact URL composition are architecture mechanisms used to satisfy those outcomes, not new product capabilities.

## Implementation, architecture, and story detail that stays out of the PRD

The following should remain in architecture, code, story history, configuration policy, and tests:

- `DAPR_HTTP_ENDPOINT`, `DAPR_HTTP_PORT`, port 3500, app-id `memories`, and `/v1.0/invoke/memories/method/` path composition.
- `AddFoldersContextSearchFacade`, `MemoriesClientOptions.Endpoint`, `MemoriesFolderSearchSource`, and `HttpMessageHandler` test seams.
- The specific `folders → memories GET /api/search` Dapr allow rule and deployment file paths.
- Dapr mTLS implementation, AppHost sidecar injection, bearer-token configuration, and direct-HTTP precedent in Tenants.
- Architecture labels I-3/S-4 and exact edits to architecture/story/sprint-status documents.
- The `InvalidOperationException` catch comment and connection-refused behavior.
- Test class names, endpoint-composition cases, `dotnet` commands, and format checks.
- Developer/architect handoff assignments and the deferred-work ledger line numbers.

The PRD should require fail-closed, authenticated, tenant-safe internal behavior without freezing Dapr's configuration grammar or class layout.

## Already-covered PRD content

The current PRD already states the product and release outcomes with exact identifiers or named sections:

1. **Architectural Boundaries:** Hexalith.Memories provides shared search-index integration while Hexalith.Folders owns Folders policy, state, audit, and operational projections. This preserves Folders authorization as the load-bearing product control.
2. **Contract and Quality Gates — internal boundaries:** “Internal service and event boundaries are deny-by-default: missing or invalid service identity, tenant scope, or policy evidence cannot invoke or project protected behavior.” This is the direct product-level requirement exposed by the source.
3. **Architecture Decisions Needed Next:** the PRD defines product outcomes rather than implementation mechanisms; architecture owns transport mechanics. Dapr routing belongs there.
4. **FR31 — Workspace and Lock Lifecycle:** index status distinguishes current, delayed, failed, stale, and unavailable.
5. **FR44 — Error, Status, and Diagnostics Contract:** `read-model unavailable` is a required distinct error category.
6. **FR46 — Error, Status, and Diagnostics Contract:** index/read-model failure exposes safe state, retryability, client action, correlation ID, and available evidence.
7. **FR55 — Audit and Operations Visibility:** file content, provider payloads/tokens, credentials, secrets, and unauthorized existence are excluded from diagnostics and responses.
8. **FR58 — Authorized Search Facade:** metadata-token results are security-trimmed and hydrated against current Folders authority across REST, SDK, CLI, and MCP; unavailable behavior is explicit and fail-safe.
9. **NFR — Security and Tenant Isolation:** tenant isolation applies to every query, asynchronous side effect, read model, and index result; unauthorized existence and sensitive payloads may not escape.
10. **OQ5 — Open Release Items:** the fail-safe but functionally empty FR58 path still needs authorized non-empty results, indexing status, stale/unauthorized-hit removal, unavailable behavior, and both C13 operation paths before implementation readiness closes.

The memlog independently records deny-by-default internal boundaries, FR58's Memories-backed current-authority hydration, the separation from live-workspace body search, and OQ5's release-blocking evidence condition.

## Genuine PRD gaps

**No missing PRD requirement.** The source reveals a prior implementation/conformance defect against an existing deny-by-default gate, not an omitted product outcome.

One residual implementation-evidence gap should be carried downstream: the proposed explicit absolute `Memories:BaseAddress` override always wins and deliberately bypasses Dapr. The source describes it as hermetic-test/non-Dapr behavior, but the proposal does not show a production-profile guard preventing accidental use. To satisfy the existing internal-boundary gate, delivery evidence must prove one of:

- production configuration cannot set or honor the direct override;
- the production deployment gate rejects it; or
- any allowed production direct route supplies equivalent deny-by-default service identity, transport protection, and tenant-safe authorization.

This does not require a new FR or OQ. It is a negative configuration/control case under the existing internal-boundary quality gate and security verification.

## Conflicts and supersession

### No product-scope conflict

The source says FR58 behavior and PRD scope are unchanged. The current PRD agrees. Routing through Dapr changes the enforcement mechanism, not the authorized result, status, error, or cross-surface contract.

### Local verification is not release closure

The source's build, hermetic registration tests, and policy-conformance suite are useful implementation gates, but the current **OQ5** requires complete behavioral evidence for authorized non-empty FR58 results and both C13 operation paths. Closing the sprint ledger item or proving that a URL is composed through Dapr does not close OQ5.

### Production override caveat

The source presents Dapr deny-by-default plus mTLS as unconditionally operative in production while retaining a direct absolute URL override that wins over sidecar composition. Those claims are compatible only if deployment/configuration policy prevents the override in production or protects it equivalently. Without that negative control, the implementation would still permit the bypass condition the proposal was created to remove and would conflict with the current PRD's deny-by-default internal-boundary gate.

### Graceful-degradation interpretation

Changing the fail-safe trigger from “no base address” to connection/refusal or malformed endpoint is compatible with FR31, FR44, FR46, and FR58 as long as callers receive the stable unavailable outcome rather than a raw 500 or a misleading healthy-empty state.

## Recommended stable-ID edits or additions

- **FR31:** No edit.
- **FR44:** No edit.
- **FR46:** No edit.
- **FR55:** No edit.
- **FR58:** No edit.
- **OQ5:** Keep open and unchanged until behavioral evidence closes it.
- **New FR/NFR/OQ:** None.
- **Renumbering:** None.

Add the production direct-override negative control to downstream security/conformance evidence, not to a new product requirement.

## Qualitative ideas the FR structure might otherwise drop

The source carries several important security and governance principles:

- **A dormant policy is not enforcement.** Conformance tests must exercise the route the deployed client actually uses.
- **Layered controls have different jobs.** Dapr supplies coarse app-to-app identity and transport protection; Folders remains responsible for per-tenant authorization and security trimming.
- **Architecture claims must be operationally true.** Conditional caveats hidden in one section cannot coexist with unconditional security claims elsewhere.
- **Graceful degradation is part of security.** Sidecar or Memories failure should become a stable unavailable state, not a bypass or uncontrolled exception.
- **Test escape hatches need environment boundaries.** A direct URL useful for hermetic tests must not become a silent production bypass.
- **Corrections should preserve audit history.** Updating a completed story with a dated correction is preferable to rewriting the record as if the original mismatch never existed.

The proposal itself preserves the implementation rationale and rejected weakening alternative, so a PRD addendum is not needed.

## Concise disposition

**No `prd.md` change; no addendum; no new open item.** Treat the route-through-sidecar decision as implementation of the existing deny-by-default internal-boundary gate. Keep OQ5 open for full FR58/C13 behavior evidence, and require a production-profile negative control for the absolute direct-URL override before claiming the Dapr enforcement is unconditionally operative.
