---
title: 'Story 12.2: external workload authority profile and enrollment'
type: 'feature'
created: '2026-11-11'
status: 'in-progress'
baseline_commit: '5107222fe6be005e800f3d8bac296b89f5310087'
approved: '2026-11-11'
route: 'dispatch'
review_loop_iteration: 0
parent_spec: 'spec-12-2-durable-projections-and-task-completion-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Folders source reads require exact signed tenant/domain bindings. Its ordinary Keycloak credentials cannot issue the SDK profile, so acquisition refuses before source transport.

**Approach:** Extend the existing Keycloak authority through a Platform-owned provider and explicit enrollment integration. Platform AD-6 establishes realm-contract ownership; it does not establish a new standalone issuer. Implement locally against the installed 26.6.4 server without changing dependency pins. Keep activation blocked until actual protected trust and grants are supplied and verified.

## Boundaries & Constraints

**Always:** Validate authenticated client, server-owned caller, exact audience, operation and complete binding set. Require one current, unambiguous, nonrevoked grant with identity, positive revision and bounded validity. Preserve independent RSA proofs, human authentication, SDK guards, source authorization and metadata-only grants. Preserve callback AllowUnbound grants, query-envelope digests and Tenants bootstrap/source permissions separately. Bound tokens to 120 seconds and grant/signer expiry. Qualify TLS and actual enrollment separately from synthetic component signers.

**Never:** Embed an issuer in Folders, activate the SDK fixture, infer enrollment from an option, generate a signer and declare it trusted, borrow another workload's identity, broaden grants, fabricate source zero, alter strict tests/deadlines/retries, deploy, publish, change dependency pins, commit or push.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure |
|---|---|---|---|
| Bound source | Own client; exact tenant/folders; eventstore/stream-read | Independently validated signed scope; RSA source proof still required | No transport on refusal |
| Invalid admission | Wrong client/caller/tenant/domain/audience/operation; missing/extra bindings; stale/revoked/ambiguous grant | Refuse issuance | No fallback |
| Existing traffic | Exact enrolled unbound callbacks/bootstrap/source; bound query digest | Preserve each separate permission | No implicit grant |
| Invalid assertion | Malformed, expired, altered signature/scope; cancellation | SDK refuses or propagates cancellation | No source request |
| Missing enrollment | Absent protected trust/grant references | Remain disabled; identify missing inputs | No runtime qualification claim |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Platform/_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`, AD-6: existing Keycloak; Platform realm contract; EventStore claim semantics.
- Platform `src/Hexalith.Platform.Identity/`: admission verification, not an OAuth issuer. Do not repurpose its signer or enrollment receipts.
- Platform `src/Hexalith.Platform.Composition/Declarations/eventstore.json`: existing `workload-issuer-client-secret` reference; no binding enrollment.
- EventStore `src/Hexalith.EventStore.ServiceDefaults/Authentication/WorkloadAuthorizationDetails.cs`, `WorkloadAuthorityGrant.cs`, `JwtWorkloadAssertionIssuer.ResourceBindings.cs`: existing protocol and independent validation. Fixture authority and host refusal stay intact.
- Folders `src/Hexalith.Folders.AppHost/Program.cs`, `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`: supported dedicated workload trust/client configuration; capture catalog only after cohort configuration.

## Tasks & Acceptance

**Execution:**
- [x] Platform `infrastructure/keycloak-workload-binding/pom.xml` and `src/main/java/com/hexalith/security/WorkloadBindingProtocolMapper.java`, `WorkloadBindingGrantPolicy.java`, `WorkloadBindingClientPolicyExecutor.java`: add provider and exact validation through supported Keycloak SPIs; retain Keycloak credential validation/signing. Reject oversized/duplicate fields; exclude protected values from errors/logs.
- [x] Platform `docs/security/workload-binding-enrollment.md`: document protected enrollment, validity/revocation, reload, independent TLS/signer verification, disable/cleanup and exact grant inventory. Distinguish source implementation from enrollment authority.
- [x] Folders AppHost/fixture above: consume supported provider enrollment and dedicated `Authentication:WorkloadJwtBearer`/`Authentication:WorkloadIssuer`; configure every affected actual receiver/caller before catalog capture. Enable the profile only with verified enrollment. Source integration and component controls are complete; actual protected activation remains blocked.
- [x] Platform provider `src/test/java/com/hexalith/security/WorkloadBindingGrantPolicyTest.java` and Folders `tests/Hexalith.Folders.Server.Tests/EnrolledWorkloadSourceBoundaryTests.cs`: qualify the matrix with actual issuer controls; preserve existing tests. Qualification is explicitly disposable-component-only, using the actual 26.6.4 credential/signing implementation.
- [ ] `_bmad-output/implementation-artifacts/evidence/`: bind coherent Debug/source runtime, focused results, unchanged strict real-provider result, source/loaded identities and owned cleanup. Keep privacy, governance and performance gates separate. Component evidence and the retained earlier-source failure are recorded; actual enrolled Folders/Workers runtime and current strict acceptance evidence remain outstanding.

**Acceptance Criteria:**
- Given approved protected trust and exact current grants, when actual enrolled clients acquire assertions, then source, callback, query and Tenants traffic retain their exact permissions.
- Given the rebuilt audited runtime, when unchanged strict real-provider acceptance runs, then current source/Workers evidence and its original 403 control pass without skips or relaxed budgets.
- Given absent owner configuration, when local qualification finishes, then report the exact activation blockers and leave story 12.2 incomplete.

## Implementation Notes

- Platform owns the provider and shared enrollment consumer. Folders links the consumer into its existing AppHost; no issuer, grant writer or fixture-authority activation was added to Folders. Memories remains outside this enrollment.
- The consumer verifies protected manifest bytes against a separately configured approved SHA-256, complete exact traffic probes, normal TLS, pinned RSA SPKI and fresh independently verified assertions before enabling the dedicated SDK profile. The fixture applies this after selecting all actual caller identities and before catalog capture; its manual bootstrap/capacity assertions use the enrolled SDK configuration when active.
- Missing `Folders:WorkloadEnrollment:ManifestPath` and `Folders:WorkloadEnrollment:ManifestSha256`, absent approved TLS/signer/grants and the outstanding current strict acceptance keep this child and story 12.2 **in-progress**. Disposable component signers and credentials establish no owner enrollment.

## Spec Change Log

- 2026-11-11: Implemented the approved source prerequisite and recorded component qualification; frozen intent and all strict acceptance bodies, budgets and retries remain unchanged. Actual protected activation and current strict runtime acceptance are incomplete.

## Review Triage Log

## Verification

- Build/test the provider against the installed Keycloak version; exercise real credential validation, signing, grant changes and TLS with explicit qualification labels.
- Restore/build affected .NET projects in Debug with source references; invoke focused xUnit assemblies directly.
- After verified enrollment and runtime audit, run unchanged `OperationalStatusDurabilityAcceptanceTests`; companion/capacity tests retain their original bodies and budgets.
- Current metadata-only evidence: [verification.json](evidence/story-12-2-external-workload-authority-20261111/verification.json). Maven: 23 passed. Debug/source owner Composition, AppHost and Server builds: zero warnings/errors. Direct xUnit: 11 actual-issuer SDK controls, 5 consumer live controls, 8 model/source configuration tests and 23 existing source tests passed without skips.
- The retained strict result in `story-12-2-current-qualification-20261111.json` is an **earlier-source failure**, with the original expected 403 canceled under its unchanged 75-second policy. Its exact artifact hash and unchanged strict body hash are bound in the new evidence. No new strict run or current Folders/Workers runtime qualification is claimed.
- The disposable component run observed zero remaining task-owned containers and temporary paths after cleanup, with private directories/files held at 0700/0600. Protected grants, credentials, tokens, keys and local absolute paths are excluded from retained evidence.
- [Parent gate audit](evidence/story-12-2-external-workload-authority-20261111/parent-audit.json): all five matrix rows have passing component coverage; frozen intent, source bindings, protected test bodies and source/output copies verify. A final whitespace correction was followed by an AppHost rebuild and successful reruns of the eight focused and sixteen component cases. Actual enrollment and current strict acceptance remain open at workflow step 03.
