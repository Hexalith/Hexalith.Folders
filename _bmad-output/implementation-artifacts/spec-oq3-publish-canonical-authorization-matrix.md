---
title: 'Publish OQ3 canonical authorization matrix'
type: 'feature'
created: '2026-09-14'
status: 'done'
route: 'dispatch'
baseline_commit: '16d3f37e04e02e7ccce11a280c1dd0c59e287c90'
review_loop_iteration: 0
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** OQ3 blocks authorization completeness because no approved denominator maps every canonical actor and access state to all 11 protected-operation families and all 49 current Contract Spine operations. Existing authorization strings, permission mappings, role handling, and denial shapes conflict with the current PRD in known places.

**Approach:** Publish a versioned canonical matrix and digest-bound Security/PM evidence, then add offline gates that prove complete actor, negative-case, scope-dimension, family, and operation coverage while recording rather than disguising downstream runtime and Contract Spine gaps.

## Boundaries & Constraints

**Always:** Preserve S-4 authorization order, authorization before observation, allow-only effective access, metadata-only allow/deny audit evidence, and the zero-enumeration invariant. Cover tenant, principal, delegated actor, provider, repository, folder, workspace, and task scope explicitly; map every current operation exactly once; retain all 11 families even though incident evidence has no current public operation; bind version, LF-stable SHA-256, signer, date, and reopen policy.

**Never:** Add or rename an endpoint; claim runtime authorization, C7 revocation timing, Story 12.1 projections, OQ9 incident access, or broad PD10 error cleanup complete; silently bless known runtime drift; expose protected identifiers or payloads in evidence; close dependent stories merely because the design package is approved.

## Approved Decisions

- Retain `read|write|administer` as the public folder-grant representation. `write` implies `read`; `administer` implies neither. Context reads require `write`; tenant administrators and folder creators receive `administer`; tenant-level families remain outside folder ACLs. Approve A5 task/delegation binding and A12/A17/A22/A23 as written in the PRD. Operation families are the evaluation, audit, and conformance vocabulary, not a new public grant shape.
- Keep authentication failure as 401. After authentication, use one exact 404 `tenant_access_denied/resource_unavailable` envelope for absent, hidden, wrong-tenant, revoked, disabled, unknown, or insufficient-scope states. Use one exact non-disclosing 503 only when trusted authority evidence is stale or unavailable, evaluated before any protected-resource lookup. Retire post-authentication 403 from the canonical design; OQ3 records current drift and leaves broad Contract Spine remediation to PD10.
- Record `Administrator` as the exact Security and PM approver on 2026-09-14 through two authority-specific records bound to the final matrix version and SHA-256 digest. Any content, version, digest, authority, signer, or date change reopens both approvals.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Authorized | Actor satisfies every conjunctive tenant, role, folder, delegation, and task condition | Family operation is allowed and emits one metadata-only audit decision | No protected data enters the matrix evidence |
| Delegated agent | Fresh delegator, agent grant, and delegated family/task scope | Effective access is their intersection and cannot elevate | Any missing/stale conjunct denies before observation |
| Elevated read | Audit, console, or incident family | Required role/permission and ordinary tenant/folder access both pass | Either missing conjunct uses the approved safe-denial class |
| Protected denial | Wrong tenant, revoked, disabled, unknown/missing, insufficient scope, or hidden/absent resource | Return the exact canonical 404 before protected reads or side effects | Emit one bounded metadata-only denial audit |
| Authority unavailable | Trusted tenant/membership/delegation evidence is stale or unavailable | Return the exact canonical 503 before any target-resource lookup | Preserve retry semantics without existence hints |
| Denominator drift | New/removed/duplicate operation, actor, family, case, or scope mapping | Offline gate fails closed | Report only bounded IDs, counts, paths, and safe hashes |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/prd.md` -- authority for actors, 11 families, FR8–FR10, assumptions A5/A12/A17/A22/A23, OQ3 closure, and PD10 boundary.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` -- authoritative 49-operation inventory and current free-form authorization metadata; preserve operation identities.
- `docs/contract/authorization-matrix.md` and `docs/contract/oq3-authorization-evidence.yaml` -- new canonical versioned denominator and approval/runtime-posture package; pin LF in `.gitattributes`.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs` -- new exact coverage, scope, actor/case, mapping, and negative-control checks.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` -- reuse OQ2 digest/approval/reopen evaluation with exact Security/PM records.
- `tests/tools/run-governance-completeness-gates.ps1` and `docs/contract/governance-and-completeness-ci-gates.md` -- extend the existing offline gate/report; do not create a separate workflow.
- `src/Hexalith.Folders/Authorization/EffectivePermissionsActionCatalog.cs` and related handlers/tests -- evidence of downstream mapping/actor gaps; inventory only, no runtime rewrite.
- `_bmad-output/planning-artifacts/{architecture.md,epics.md,planning-story-manifest.yaml,.memlog.md}` -- synchronize design-approved OQ3 status without claiming dependent runtime completion.

## Tasks & Acceptance

**Execution:**
- [x] `docs/contract/authorization-matrix.md`, `docs/contract/oq3-authorization-evidence.yaml`, `.gitattributes` -- publish and LF-pin the complete versioned denominator, digest-bound approvals, runtime posture, conformance gaps, and downstream owners.
- [x] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/AuthorizationMatrixContractTests.cs`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` -- enforce exact operation/family/actor/case/scope and approval-package coverage with fail-closed negative controls.
- [x] `tests/tools/run-governance-completeness-gates.ps1`, `docs/contract/governance-and-completeness-ci-gates.md` -- add OQ3 to the existing offline command, bounded report inventory, and operator guidance.
- [x] `_bmad-output/planning-artifacts/prd.md`, `_bmad-output/planning-artifacts/architecture.md`, `_bmad-output/planning-artifacts/epics.md`, `_bmad-output/planning-artifacts/planning-story-manifest.yaml`, `_bmad-output/planning-artifacts/.memlog.md` -- synchronize OQ3 design approval without changing unrelated open items or claiming dependent runtime completion.

**Acceptance Criteria:**
- Given the 49-operation Spine and PRD actors/families, when matrix gates run, then every operation maps exactly once, every family and canonical actor/negative case is covered, and all eight FR8 scope dimensions are explicit or explicitly not applicable.
- Given any matrix, digest, version, authority, signer, date, operation, family, actor, case, or scope drift, when governance checks run, then OQ3 fails closed with metadata-only diagnostics.
- Given OQ3 design approval, when planning and evidence are inspected, then runtime/PD10/OQ9 gaps remain explicit and dependent stories receive no false completion claim.

## Implementation Notes

- Canonical denominator published as `docs/contract/authorization-matrix.md` version `1.0.0`, LF-pinned in `.gitattributes`, SHA-256 `5ffabd71faea234d884b3c45506fd7c19db562b3353072c396aca206378c52d7`. `docs/contract/oq3-authorization-evidence.yaml` binds that version/digest to exactly one Security and one PM record for signer `Administrator` dated 2026-09-14, plus the denominator counts, the recorded gap inventory, the runtime posture, and the exact reopen policy.
- Denominator shape: 12 canonical access states (6 PRD actors plus 6 canonical negative cases, with `absent-resource` and `insufficient-scope` added to the denial-state routing table), 11 protected operation families, a 66-row actor-by-family decision table over the closed vocabulary `allow`, `allow-explicit-grant`, `allow-delegated-intersection`, `deny`, all 49 Contract Spine operations mapped exactly once, and all 8 FR8 scope dimensions declared applicable or explicitly not applicable per operation.
- Family assignment totals: provider-configuration 2, readiness-and-provider-evidence 2, folder-creation 1, incident-evidence 0, folder-administration 6, task-mutation 7, context-read 5, status-permission-and-lock-inspection 13, audit-read 4, console-view 7, index-search 2. `incident-evidence` is retained with a zero count and an OQ9 downstream owner rather than dropped.
- Access-state semantics recorded in the document: an access state is defined by its authority and permission set, so a cell reads `deny` when the missing conjunct is another access state's defining permission (tenant-administrator authority, operator permission, audit-reviewer role, incident-admin permission, delegation) and `allow-explicit-grant` when the missing conjunct is an ordinary folder grant or the tenant folder-create permission. A12/A17/A22/A23 are load-bearing: tenant administrators need an explicit `write` for task mutation and context read, and an explicit `read` for status, audit, console, and index families.
- Outcomes: exactly three canonical envelopes (401 `authentication_failure`/`authentication_required`, 404 `tenant_access_denied`/`resource_unavailable`, 503 `read_model_unavailable`/`projection_unavailable` with `redacted` visibility, evaluated before any protected-resource lookup). Post-authentication 403 is retired from the canonical design; the Spine's current 403 declaration is recorded as gap `G1` for PD10.
- Recorded conformance gaps `G1`-`G11` cover the two status-distinct Spine denial envelopes (403 on 49/49, 404 on 46/49), the enumeration-leaking categories on 24 of 49 operations (`not_found` 22, `cross_tenant_access_denied` 6, `audit_access_denied` 4), the missing exact authority-unavailable envelope (503 declared on 45/49), the deployed effective-permission action-catalog drift, the two tenant-scoped console operations inside a folder-scoped family, the absent incident-evidence operation surface, the unproven runtime/C7/Story 12.1 layer, the ACL-listing requirement-token mismatch, the missing task parameter on effective-permissions, the two further free-form requirement-token divergences that are not gate-mechanizable, and the provider/repository scope applicability derived from product rules rather than Spine path or parameter evidence.
- Gates: new `AuthorizationMatrixContractTests` (5 facts) parses the matrix tables and fails closed on unmapped, unknown, duplicate, uncovered, scope-incomplete, denial-shape, and unrecorded-gap drift with metadata-only diagnostics; it also pins the matrix family vocabulary disjoint from the C13 transport `operation_family` values. `GovernanceCompletenessGateTests` gains `EvaluateOq3Evidence` plus positive and negative approval-package facts mirroring the OQ2 pattern. Both classes run from the existing offline command; no separate workflow was added.
- Planning synchronization records OQ3 as design-approved only: PRD Open Release Items row, PD10 permission-representation sentence, family paragraph, edit history, architecture dependency spine, epics AR-CURRENT-08 / governing dependency / Epic 12 prerequisites / Story 12.1 Given, manifest `OQ3` row (`design-approved`, blocks list unchanged), and two `.memlog.md` entries. Stories 12.1, 4.19, 4.20, 4.21, 6.14, and 10.8 stay blocked and FR8-FR10 runtime evidence stays incomplete.

## Spec Change Log

## Review Triage Log

| # | Finding | Verdict | Evidence | Route |
| --- | --- | --- | --- | --- |
| F1 | PD10 row still says `not_found` on 24 while new gap `G2` says 22 | medium | Parsed the Spine: `not_found` appears on 22 of 49 operations. This diff edits that exact PD10 sentence (`prd.md`) and leaves the stale figure, so the PRD and the new denominator disagree on the number the matrix was published to fix. | patch |
| F2 | PRD Actors table not synchronized with `absent-resource`/`insufficient-scope` | false | The matrix labels these "two additional protected denial states", explicitly distinct from the six canonical negative cases, and the evidence YAML records `negative_access_states: 6` matching `prd.md:534` exactly. Nothing claims they are PRD canonical negative cases. | rejected |
| F3 | Declared denominator is 12 access states while the gate enforces 14 distinct rows | medium | `oq3-authorization-evidence.yaml` pins `access_states: 12`/`negative_access_states: 6`; the negative table publishes 8 rows and `AuthorizationMatrixContractTests` requires all 8. For an artifact whose purpose is to be the counting authority, the published count disagrees with the enforced enumeration. | patch |
| F4 | No precedence rule when one principal holds two access-state-defining permissions | medium | `audit-reviewer x console-view` = `deny` and `tenant-scoped-operator x audit-read` = `deny`, so a principal holding both defining permissions is denied both families. The Grant Representation section already states the answer ("union of applicable allow-only grants"), which the actor-partition sentence contradicts. | patch |
| F5 | `readiness-and-provider-evidence` cells encode two answers in one decision token | false | The family Authority conjunct column is explicitly disjunctive and each decision row's Governing conjuncts column states the per-operation split ("readiness validation additionally requires the folder-create permission"; "provider support evidence stays denied"). The rows are unambiguous as written. | rejected |
| F6 | `CreateRepositoryBackedFolder` requires `administer` on a folder that does not yet exist | maybe-false | The Spine's own token for it is `tenant-context-existing-folder-access-and-provider-binding-use`, which supports the matrix's folder-administration binding. Settling it needs the operation's request schema and handler to establish whether it creates a new folder or binds an existing one. | defer |
| F7 | Recorded gap rows under-state the drift they claim to record (G8 token class, G5 replacement grant, G4 catalog items) | medium | Spine tokens diverge from the matrix family binding on `GetTaskStatus` (tenant + task scope, no folder ACL) and `ValidateProviderReadiness` (`provider-readiness-read` vs folder-create) with no gap row, while G8 records exactly this class for `ListFolderAclEntries`. G5 marks `folder` NA for the two tenant-scoped console operations without naming the grant that replaces it. `EffectivePermissionsActionCatalog.cs` maps `configure_provider_binding`->Administer and `provider_readiness_read`->Read, tenant-level families given folder levels, the same class G4 records for `create_folder`. | patch |
| F8 | `G5`/`G8`/`G9` routed to PD10 whose enumerated scope does not list them | low | True as stated, but the owner cell names the accountable people (Architecture with Contract and Delivery and Security), who are the natural owner of Spine corrections. Unlikely to mislead in everyday use, and the fix costs a digest and approval cascade for an ownership label. | rejected |
| F9 | Manifest introduces `design-approved` for OQ3 while OQ1/OQ2 remain `status: open` | medium | `planning-story-manifest.yaml:479,490` still read `open` although `prd.md`, `architecture.md` and `epics.md` all declare OQ1 closed 2026-09-12 and OQ2 closed 2026-09-14. The stale sibling rows are pre-existing and not caused by this change; OQ3's own row is accurate and keeps its `blocks` list intact. | defer |
| F10 | Approved digest hand-copied into three planning artifacts that no gate pins | medium | `a2393b4a...f412e` appears in `prd.md`, `.memlog.md` and `planning-story-manifest.yaml`, but is pinned in code only at `GovernanceCompletenessGateTests.cs:52`. A future matrix revision reddens the two contract-file checks and leaves the three planning copies silently stale. | patch |
| F11 | New `AssertMetadataOnly` fork dropped `cache-key-value` and the `ShouldBeFalse` message | medium | Compared both copies directly: the `GovernanceCompletenessGateTests` list carries `"cache-key-value"` and passes `value` to `ShouldBeFalse`; the new copy in `AuthorizationMatrixContractTests` has neither, so the new gate's metadata-only guarantee is weaker than its sibling's. | patch |
| F12 | `EvaluateOq3Evidence` hardcodes `new(2026, 9, 14)` instead of parsing `ApprovedOq3Date` | low | Confirmed at `GovernanceCompletenessGateTests.cs:1867`, while the generic `EvaluateApprovalRecords` path at `:1230` parses with `DateOnly.TryParseExact`. A re-approval that moves the constant leaves the staleness window pinned to the old date. Direct one-line correction. | patch |
| F13 | `oq3_operation_unknown` always reports the constant identifier `spine-inventory` | false | The constant is deliberate: the alternative echoes an unknown operation label taken from the document into the diagnostic, which is what the metadata-only rule forbids. The proposed fix weakens the invariant it claims to improve. | rejected |
| F14 | Countdown `for` loop emits N indistinguishable `family-vocabulary` diagnostics | low | Same non-echo rationale as F13; the loop is unusual style but behaviorally correct, and the fix is cosmetic. | rejected |
| F15 | `provider` and `repository` scope dimensions are never validated against anything | high | `EvaluateScopeDimensions` checks only the three always-applicable dimensions and the Spine-declared `folder`/`workspace`/`task`. Moving `provider` to not-applicable on `ConfigureProviderBinding` leaves all five facts green, and over-declaring a dimension the Spine does not carry is never caught either, directly short of the story's own AC that any scope drift fails closed. | patch |
| F16 | Path-item-level OpenAPI `parameters` are not read, so scope checks can fail open | low | `ResolveParameterNames` reads only the operation mapping. No path item declares `parameters` today, so the fail-open is latent, but a gate that fails open is the wrong direction and the extension is direct. | patch |
| F17 | An unresolved parameter `$ref` is silently dropped | medium | In `ResolveParameterNames`, a `$ref` whose target is missing from `components/parameters` falls through to `continue`, so a renamed component silently removes a scope conjunct from the gate rather than failing it. | patch |
| F18 | Malformed Spine YAML raises raw cast/range exceptions instead of bounded diagnostics | low | These fail loudly on a checked-in, schema-valid file with no demonstrated path to the malformed state, and the fix adds guards for unreachable conditions. | rejected |
| F19 | Gap evidence path with a leading slash escapes the repository on Windows | low | `IsRepositoryRelativePath` relies on `Path.IsPathFullyQualified`, which is false for `/foo` on Windows, and `Path.Combine` then discards the repository root. One-clause direct correction, no added complexity. | patch |
| F20 | Raw document text can be echoed in diagnostics past the vocabulary guard | low | The `:scope` loop in `EvaluateFamilies` and the `:decision` identifier in `EvaluateDecisions` interpolate cell text that has not been constrained to the canonical vocabulary, against the story's metadata-only diagnostics AC. | patch |
| F21 | `AllFamiliesToken` is the literal `all-11` rather than derived from the family count | low | Confirmed at `:14`. Adding a twelfth family leaves every negative state still accepted as covering all families while one is uncovered. One-line derivation. | patch |
| F22 | The OQ3 gate can be dropped from the only CI lane that runs it and the script assertion still passes | high | Pre-verified by the verification-gap layer with a demonstration; confirmed the asymmetry at `GovernanceCompletenessGateTests.cs:127-128`, where the sibling pins `FullyQualifiedName~...GovernanceCompletenessGateTests` but the new class is asserted as a bare substring that the `-class` fallback line alone satisfies. Deleting the `--filter` term stops the whole denominator gate running while governance reports passed. | patch |
| F23 | Matrix prose "`folder ACL denied` is returned" contradicts `insufficient-scope` -> 404 and "403 retired" | high | `authorization-matrix.md:76` routes exactly that condition ("holds an allow on the folder but not the requested operation family") to `safe-denial-404`, and `:143` retires post-authentication 403, while `:149` says a distinct `folder ACL denied` is returned for the same condition. An implementer following the prose emits a distinguishable envelope, defeating the zero-enumeration invariant the artifact exists to protect. | patch |
| F24 | Incident administrator is described as read-only yet its `task-mutation` row is `allow-explicit-grant` | low | Reconcilable on a careful read (incident access itself grants no mutation; an explicit folder write grant is ordinary member access), but the two sentences read as contradictory. Folded into the matrix edit already required by F23/F4. | patch |
| F25 | Actor, family and scope denominators are pinned to hardcoded arrays, never to the PRD tables the matrix says it counts | high | Pre-verified by the verification-gap layer: the operation axis is bound to its authority (`spine.Length.ShouldBe(49)` plus `oq3_operation_unmapped`), but adding, renaming or removing a PRD actor, negative case, family or FR8 dimension leaves every gate green on an unchanged matrix, so the release denominator can silently stop covering the PRD. | patch |
| F26 | `G1`-`G3` conformance counts are prose that no check recomputes | medium | Pre-verified by the verification-gap layer: `LoadSpineOperations` never reads `responses`, so no declared status code or error category is ever counted, and PD10 remediation in either direction leaves the recorded counts stale with the digest pin still green. The `G4` catalog half of this finding was dispositioned `defer` by that layer. | patch (G4 half deferred) |

## Design Notes

The matrix is the authorization denominator, while the existing C13 `operation_family` remains a transport classification. OQ3 must not repurpose that field or hand-edit generated parity rows. Incident evidence remains represented in the actor×family matrix with no current public operation and a downstream OQ9 owner.

## Verification

**Commands:**
- `sha256sum docs/contract/authorization-matrix.md` -- expected: equals every OQ3 evidence and approval digest.
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1` -- expected: succeeds without warnings.
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- expected: OQ3 matrix and approval categories pass and emit bounded evidence.
- `git diff --check` -- expected: no whitespace errors.
