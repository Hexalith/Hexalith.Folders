# Reconciliation — Sprint Change Proposal: REST Negative-Path Coverage Extension

**Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-08-rest-negative-path-coverage.md`  
**Source date:** 2026-07-08  
**Compared with:** `_bmad-output/planning-artifacts/prd.md` (final, updated 2026-07-14) and `_bmad-output/planning-artifacts/.memlog.md` (updated 2026-07-14)  
**Addendum:** None exists in the bound PRD workspace.  
**Reconciliation disposition:** **No PRD edit; test-hardening input under existing FRs and OQ3.** Confirm the canonical safe-denial status/envelope matrix before implementing the proposal's exact 403/404 assertions.

## Source purpose and status

The source proposes fast route-level tests for Story 8.1 REST operations whose production behavior was already implemented and reportedly covered by no-mock integration tests. The intended additions cover:

- an authorized `ListFolderAclEntries` 200 response shape;
- authenticated tenant-mismatch safe denials for ACL-list and workspace-retry-eligibility reads;
- unknown-workspace and unavailable-read-model responses for retry eligibility; and
- gateway not-found mapping for `UpdateFolderAclEntry`.

The proposal classifies the change as minor, test-only, and a direct adjustment with no production, PRD, architecture, OpenAPI, UX, or C13 operation change. Jerome approved all four change proposals in incremental review, but execution was explicitly deferred. No tests, tracking edits, or action-item closure should be inferred from this proposal alone.

The source contains a mechanical counting inconsistency: its four numbered proposal bundles define **six named test methods** (1 + 2 + 2 + 1), while validation predicts “~+5 tests” and the success criteria say “four new tests.” Implementation planning must use the six named behaviors or deliberately consolidate them and update the count.

## Product-level requirements and decisions relevant to the PRD

The source reinforces existing product outcomes:

- Tenant and folder authorization applies to ACL and workspace-status reads as well as mutations.
- Unauthorized and cross-tenant requests fail before protected resource details are exposed.
- Denial, not-found, and unavailable paths use stable, bounded, metadata-only error envelopes with no identifier echo.
- Authorized actors can inspect folder permissions/ACL outcomes and workspace retry eligibility.
- Read-model unavailability is distinguishable from a known absent resource and supplies safe retry/client-action semantics.
- REST behavior remains aligned with the canonical error contract and cross-surface semantics.

The exact HTTP status, `category`, `code`, DTO shape, header name, and `aclEntryId` derivation belong to the Contract Spine and adapter tests rather than the PRD.

## Implementation, architecture, and story detail that stays out of the PRD

The following should remain in the test suite and story/action-item records:

- `FolderAclEndpointTests.cs`, `WorkspaceLockEndpointTests.cs`, `BuildApp`, and named test doubles.
- `InMemoryFolderRepository.Seed`, stream names, internal visibility, clocks, and gateway exceptions.
- Exact routes, HTTP headers, response field names, `aclEntryId` derivation, and DTO values.
- The use of tenant-header mismatch as the denial trigger.
- `WorkspaceLockStatusQueryHandler`, `NotFoundSafe`, `Unavailable`, and route-mapper implementation details.
- Test-tier selection, file-list/change-log updates, sprint-status line numbers, and test commands.
- Historical Story 8.1 operation numbering and the source's hard-coded parity counts.

These details verify the product contract but should not be promoted into stable product requirements.

## Already-covered PRD content

The current PRD already contains the relevant capabilities and safety invariants:

1. **Authentication and Authorization Model:** effective permission is the intersection of active tenant authority and allow-only folder grants; absence, stale/conflicting evidence, and revocation fail closed. Cross-tenant access is denied before protected access and uses non-enumerating safe errors.
2. **Error Codes:** errors are stable and machine-readable; the Contract Spine owns closed error details; absent, cross-tenant, missing-binding, missing-policy, and equivalent protected-resource denials are caller-visibly indistinguishable before specific-scope authorization.
3. **FR5 — Authorization and Tenant Boundary:** tenant administrators can grant and revoke folder access with visible, auditable verb scope.
4. **FR6 — Authorization and Tenant Boundary:** authorized actors can inspect effective permissions for a folder or task context.
5. **FR8 — Authorization and Tenant Boundary:** every operation is evaluated against tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope.
6. **FR9 — Authorization and Tenant Boundary:** unauthorized and cross-tenant operations are denied before protected information is exposed.
7. **FR10 — Authorization and Tenant Boundary:** allowed and denied authorization evidence excludes unauthorized resource details.
8. **FR26 — Workspace and Lock Lifecycle:** authorized actors can inspect retry-eligibility metadata.
9. **FR43 — Error, Status, and Diagnostics Contract:** supported surfaces expose category, code, safe message, correlation ID, retryability, client action, and bounded details visibility.
10. **FR44 — Error, Status, and Diagnostics Contract:** authentication failure, tenant denial, folder-policy denial, read-model unavailability, and transient infrastructure failure are distinct taxonomy categories.
11. **FR46 — Error, Status, and Diagnostics Contract:** authorization/read-model failures expose safe resulting state, cause, retry eligibility, client action, correlation ID, and evidence.
12. **FR51 — Cross-Surface Contract:** authorization behavior and error categories remain equivalent across supported surfaces.
13. **NFR — Security and Tenant Isolation:** API responses and errors are part of mandatory tenant-isolation tests; unauthorized existence may not appear; denial shapes avoid enumeration.
14. **NFR — Verification Expectations:** security and tenant-isolation behavior require automated tests.
15. **Contract and Quality Gates / OQ3:** the canonical authorization matrix must cover every protected operation family across authorized, wrong-tenant, revoked, stale, and hidden-resource states. OQ3 remains the release-blocking owner of the exact denominator.

The memlog confirms zero-tolerance non-enumeration, scope-complete authorization evaluation, allow-only ACL composition, universal non-enumeration before specific-scope authorization, and deferral of OQ3 until the complete authorization matrix is approved.

## Genuine PRD gaps

**None.** The source describes missing fast tests for existing behavior, not a missing capability, user outcome, safety invariant, error category, or NFR.

The current PRD intentionally leaves exact wire mappings to the Contract Spine and the canonical authorization matrix. The unresolved work is to ensure the proposed route assertions match those authorities. That is already governed by **OQ3**, so no new PRD open item is needed.

## Conflicts and supersession

### Safe-denial semantic mismatch

The proposal says its authenticated 403 tests prove “401/403/404 caller-visible indistinguishability,” but the proposed assertions expose different HTTP statuses and categories:

- tenant mismatch: `403`, category `tenant_access_denied`, code `denied_safe`;
- unknown workspace/folder: `404`, category/code `not_found`; and
- unauthenticated behavior is separately `401`.

Those values are caller-visible and therefore cannot literally be indistinguishable. The current PRD specifically requires absent, cross-tenant, missing-binding, missing-policy, and equivalent protected-resource cases to be indistinguishable before specific-scope authorization. Authentication failure may remain distinct, but the authenticated cross-tenant-versus-absent mapping must be resolved through the current Contract Spine and **OQ3 authorization matrix** before these tests hard-code 403 versus 404.

If the Contract Spine currently mandates the proposed distinct responses, that is a PRD/Contract-Spine authority conflict and must be reconciled and re-approved rather than silently blessed by tests. If the cases are not equivalent because tenant-header mismatch is classified as an authority/input mismatch rather than a hidden-resource lookup, the authorization matrix must state that distinction explicitly.

### Release-readiness scope

The source describes the test gap as non-release-blocking. The tests themselves can remain a minor direct adjustment, but they touch the release-blocking authorization denominator in **OQ3**. Passing these representative routes cannot close OQ3, and their assertions must not contradict the eventual canonical matrix.

### C13 denominator supersession

The source's historical “REST stays 40/47 for Bucket A” count must not be imported. The current PRD says C13 is generated from the current Contract Spine, contains a later snapshot count, and—more importantly—the generated current inventory rather than any hard-coded count is binding. The correct non-change statement is simply that no operation or C13 row is added or removed.

### Internal proposal consistency

Execution remains deferred, so the tracking action must remain open until tests are actually implemented and pass. The implementation handoff should also reconcile the source's six named methods with its contradictory four/~five test counts.

## Recommended stable-ID edits or additions

- **FR5, FR6, FR8, FR9, FR10:** No edits.
- **FR26:** No edit.
- **FR43, FR44, FR46:** No edits.
- **FR51:** No edit.
- **OQ3:** Keep open and unchanged; use its canonical authorization matrix to resolve the 403/404 equivalence class before coding assertions.
- **New FR/NFR/OQ:** None.
- **Renumbering:** None.

After matrix/Contract-Spine confirmation, implement the route tests as verification evidence without changing stable product IDs.

## Qualitative ideas the FR structure might otherwise drop

The source preserves useful test-design principles:

- **Fast route tests complement end-to-end evidence.** They localize status/envelope regressions without replacing integration coverage.
- **Negative assertions matter.** A safe denial test must assert that requested tenant, folder, workspace, and subject identifiers are absent, not merely check the status code.
- **Known absence and infrastructure unavailability are different.** `404` and `503` paths require separate retry/client-action behavior.
- **Use established seams.** Existing authorization, repository, gateway, and throwing-read-model fixtures reduce accidental production-code changes.
- **“Indistinguishable” needs a defined equivalence class.** Tests should say which fields must match and which distinctions are intentionally permitted; contradictory visible statuses cannot substantiate a blanket indistinguishability claim.
- **Evidence counts must be exact.** Named scenarios, expected test count, and action-item completion should agree.

These are test and contract-governance details already preserved by the source; they do not justify a PRD addendum.

## Concise disposition

**No `prd.md` change; no addendum; no new open item.** Treat the proposal as route-level verification hardening. Keep OQ3 open, resolve the authenticated cross-tenant versus absent-resource status/envelope mapping against the canonical authorization matrix and Contract Spine, discard historical hard-coded C13 counts, and close the sprint action only after the six named behaviors (or an explicitly consolidated equivalent set) pass.
