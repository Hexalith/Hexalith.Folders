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
context:
  - '_bmad-output/implementation-artifacts/proposal-12-2-canonical-workload-audience.md'
  - 'references/Hexalith.Platform/docs/security/workload-binding-enrollment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Folders source reads require exact signed tenant/domain bindings. Its ordinary Keycloak credentials cannot issue the SDK profile, so acquisition refuses before source transport.

**Approach:** Extend the existing Keycloak authority through a Platform-owned provider and explicit enrollment integration. Platform AD-6 establishes realm-contract ownership; it does not establish a new standalone issuer. Implement locally against the installed 26.6.4 server without changing dependency pins. Keep activation blocked until actual protected trust and grants are supplied and verified.

**Renegotiated (2026-11-13, Jerome, [sprint-change-proposal-2026-11-13](../planning-artifacts/sprint-change-proposal-2026-11-13.md)):** The strict acceptance fixture draws a new run identity for every run, so a pre-provisioned owner manifest can never match its callers or tenant. Inside that disposable fixture only, verified enrollment comes from the Platform run-owned authority (Platform Story 2.12). That authority is a disposable Keycloak 26.6.4 with the same provider, enrolled for exactly that run's cohort and tenant and verified by the unchanged consumer. Persistent, staging and production enrollment stays owner-provisioned (Platform Story 10.1). Updating the Folders Platform gitlink to the commit that delivers Story 2.12 is the one authorized dependency-pin change.

**Renegotiated canonical audience (2026-11-13, Jerome, “approve”, [bounded proposal](proposal-12-2-canonical-workload-audience.md)):** The unchanged-consumer requirement has one bounded exception: correct Platform's gateway grant/receiver audience selection to the native SDK's logical `eventstore` audience. Preserve each actual run-specific caller/client ID, physical Dapr target, callback audience, exact operation/binding set, grant count and every existing cryptographic, expiry and refusal check. Update the matching producer validator, component inventory, runbook and tests, and add real SDK issuance plus physical-audience refusal controls. This does not authorize accepting both audiences, adding grants, changing strict tests/budgets/retries, changing dependency pins, committing, pushing or deploying.

## Boundaries & Constraints

**Always:** Validate authenticated client, server-owned caller, exact audience, operation and complete binding set. Require one current, unambiguous, nonrevoked grant with identity, positive revision and bounded validity. Preserve independent RSA proofs, human authentication, SDK guards, source authorization and metadata-only grants. Preserve callback AllowUnbound grants, query-envelope digests and Tenants bootstrap/source permissions separately. Bound tokens to 120 seconds and grant/signer expiry. Qualify TLS and actual enrollment separately from synthetic component signers. Label run-owned enrollment evidence as run-owned; it never qualifies owner enrollment.

**Never:** Embed an issuer in Folders, activate the SDK fixture, infer enrollment from an option, generate or trust a signer outside the Platform run-owned authority, reuse a run-owned enrollment for another run or present it as owner enrollment, borrow another workload's identity, broaden grants, fabricate source zero, alter strict tests/deadlines/retries, deploy, publish, change dependency pins other than the approved Platform gitlink update, commit or push.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Failure |
|---|---|---|---|
| Bound source | Own client; exact tenant/folders; eventstore/stream-read | Independently validated signed scope; RSA source proof still required | No transport on refusal |
| Invalid admission | Wrong client/caller/tenant/domain/audience/operation; missing/extra bindings; stale/revoked/ambiguous grant | Refuse issuance | No fallback |
| Existing traffic | Exact enrolled unbound callbacks/bootstrap/source; bound query digest | Preserve each separate permission | No implicit grant |
| Invalid assertion | Malformed, expired, altered signature/scope; cancellation | SDK refuses or propagates cancellation | No source request |
| Missing enrollment | Absent owner references (outside the fixture), or a missing or failed run-owned authority (inside it) | Remain disabled; identify missing inputs | No runtime qualification claim |
| Run-owned enrollment | Platform producer manifest for this run's exact cohort and tenant | Consumer with the approved audience-selection correction verifies TLS, signer and every probe; profile enabled before catalog capture; evidence labeled run-owned | Foreign-run or mismatched manifest refuses; no fallback |
| Canonical gateway audience | Actual enrolled run-specific caller; native SDK audience `eventstore`; exact bootstrap/source operation and bindings | SDK obtains an independently verified assertion; receiver expects exactly `eventstore`; physical target/caller identities stay run-specific | Physical-app-ID audience substitution and altered bindings refuse; no extra audience/grant |

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
- [x] Approved audience correction: Platform `src/Hexalith.Platform.Composition/PlatformWorkloadEnrollmentInventory.cs`, `PlatformWorkloadEnrollment.cs`, `infrastructure/keycloak-workload-binding/run_owned.py`, `run_owned_component.py` and `tests/Hexalith.Platform.Composition.Tests/PlatformRunOwnedWorkloadEnrollmentTests.cs`: implement the five-file proposed correction. Preserve exact verification and grants; replace the gateway audience rather than adding another.
- [x] Approved audience regressions: the same Platform test files and Folders `tests/Hexalith.Folders.AppHost.Tests/WorkloadEnrollmentComponentTests.cs`: exercise real SDK issuance for Tenants command-submit/unbound source and Folders exact bound source against the actual owned authority; require canonical audience, independent scope checks and failure before transport for a physical-app-ID audience. Preserve existing receipt/TLS/signer/binding controls and add an executable inventory rejection for physical-audience substitution. Platform `docs/security/workload-binding-enrollment.md` documents the logical gateway audience separately from caller/transport IDs.
- [x] Approved audience focused qualification: run relevant Python/provider and individual Debug/source Platform/Folders controls serially; bind exact source/outputs/results and release them without an active build/test/topology. Preserve earlier packets and all protected bodies. The parent then owns coherent runtime/Memories/PE/PDB/assets audit, actual loaded observations and the unchanged strict class. Current runtime results and any remaining failure must stay explicit; no whole-story completion is inferred from component passes.
- [ ] External prerequisite (not implemented here): Platform Story 2.12 delivers the run-owned workload-binding authority. Then update the Folders `references/Hexalith.Platform` gitlink to exactly that commit; no other pin changes.
- [x] Folders `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`: after the final cohort, replicas and `TenantId` are selected, start the Platform run-owned authority, then call the unchanged `PlatformWorkloadEnrollment.ConfigureAsync` before catalog capture. Dispose the authority with the fixture's other owned resources. `src/Hexalith.Folders.AppHost/Program.cs` keeps owner references as its only activation path. Add a focused control showing that a missing or failed authority leaves the profile disabled.
- [ ] `_bmad-output/implementation-artifacts/evidence/`: bind coherent Debug/source runtime, focused results, unchanged strict real-provider result, source/loaded identities and owned cleanup, labeled run-owned enrollment. Keep privacy, governance and performance gates separate. Component evidence and the retained earlier-source failure are recorded; run-owned enrolled Folders/Workers runtime and current strict acceptance evidence remain outstanding.

**Acceptance Criteria:**
- Given verified enrollment (run-owned inside the disposable strict fixture, owner-provisioned elsewhere), when the enrolled clients acquire assertions, then source, callback, query and Tenants traffic retain their exact permissions.
- Given the rebuilt audited runtime, when unchanged strict real-provider acceptance runs, then current source/Workers evidence and its original 403 control pass without skips or relaxed budgets.
- Given no verified enrollment (no owner references outside the fixture, or a missing or failed run-owned authority inside it), when local qualification finishes, then the profile stays disabled, the exact activation blockers are reported and story 12.2 stays incomplete.
- Given a run-owned enrollment, when its evidence is recorded, then it is labeled run-owned, claims no owner-enrollment qualification and is refused for any other run.

## Implementation Notes

- Active audience continuation: use current root `db40bda8a107fcb03405e074810be16c0d47c85f` and Platform `a7f901ae79ed5fd7ad2f04f0779b21108a4cdee9`, preserving all existing Folders changes from prior qualification. The approved patch is a starting implementation; extend its regressions as specified above. No renderer rerun, Git mutation, dependency update or competing parent build/runtime is needed. Private logging/audit helpers from prior phases may be reused only without overwriting their results. Local builds use Debug, `UseHexalithProjectReferences=true`, `UseNuGetDeps=false`, `HexalithEventStoreVersion=5.0.1`, `VersionSuffix=story12-2-qualification`; root-declared dependency paths must be supplied when a Platform source build otherwise selects nested references. Never initialize nested submodules.

- Platform owns the provider and shared enrollment consumer. Folders links the consumer into its existing AppHost; no issuer, grant writer or fixture-authority activation was added to Folders. Memories remains outside this enrollment.
- The consumer verifies protected manifest bytes against a separately configured approved SHA-256, complete exact traffic probes, normal TLS, pinned RSA SPKI and fresh independently verified assertions before enabling the dedicated SDK profile. The fixture applies this after selecting all actual caller identities and before catalog capture; its manual bootstrap/capacity assertions use the enrolled SDK configuration when active.
- Missing `Folders:WorkloadEnrollment:ManifestPath` and `Folders:WorkloadEnrollment:ManifestSha256`, absent approved TLS/signer/grants and the outstanding current strict acceptance keep this child and story 12.2 **in-progress**. Disposable component signers and credentials establish no owner enrollment.
- From 2026-11-13, the strict fixture's enrollment comes from the Platform run-owned authority (Platform Story 2.12), not from owner references. Until that story lands and the fixture consumes it, the profile stays disabled in the fixture. Owner references remain the only activation path outside the fixture.

## Spec Change Log

- 2026-11-13: Jerome approved the bounded canonical-audience proposal (“approve”). Added the frozen exception and canonical-audience matrix row, narrowed the run-owned row's unchanged-consumer wording to permit that exception, and added actionable source/regression/qualification tasks. The parent frozen block and strict bodies remain unchanged. Prior child frozen SHA-256: `baf299675fe11e5807e0792ad53feb8a8c3837bba5a29735b805ac410ff8b433`.

- 2026-11-11: Implemented the approved source prerequisite and recorded component qualification; frozen intent and all strict acceptance bodies, budgets and retries remain unchanged. Actual protected activation and current strict runtime acceptance are incomplete.
- 2026-11-13: Human renegotiation (Jerome, [sprint-change-proposal-2026-11-13](../planning-artifacts/sprint-change-proposal-2026-11-13.md)). The random per-run fixture identity makes any pre-provisioned owner manifest unmatchable. Frozen intent: added the *Renegotiated* paragraph, the run-owned evidence label (Always), the scoped signer and pin rules (Never), and the *Run-owned enrollment* matrix row; the *Missing enrollment* row was widened. Added the prerequisite and fixture-wiring tasks and AC4; reworded AC1, AC3 and the evidence task. Strict bodies, deadlines, retries and budgets are unchanged. Frozen block SHA-256 before: `69e8a7beff20c976a844e19fc6cd4646c543d7d083909849f849b75d71092840`; the after-value is in the proposal's Appendix.

## Review Triage Log

## Verification

- Build/test the provider against the installed Keycloak version; exercise real credential validation, signing, grant changes and TLS with explicit qualification labels.
- Restore/build affected .NET projects in Debug with source references; invoke focused xUnit assemblies directly.
- After verified enrollment and runtime audit, run unchanged `OperationalStatusDurabilityAcceptanceTests`; companion/capacity tests retain their original bodies and budgets.
- Current metadata-only evidence: [verification.json](evidence/story-12-2-external-workload-authority-20261111/verification.json). Maven: 23 passed. Debug/source owner Composition, AppHost and Server builds: zero warnings/errors. Direct xUnit: 11 actual-issuer SDK controls, 5 consumer live controls, 8 model/source configuration tests and 23 existing source tests passed without skips.
- The retained strict result in `story-12-2-current-qualification-20261111.json` is an **earlier-source failure**, with the original expected 403 canceled under its unchanged 75-second policy. Its exact artifact hash and unchanged strict body hash are bound in the new evidence. No new strict run or current Folders/Workers runtime qualification is claimed.
- The disposable component run observed zero remaining task-owned containers and temporary paths after cleanup, with private directories/files held at 0700/0600. Protected grants, credentials, tokens, keys and local absolute paths are excluded from retained evidence.
- [Parent gate audit](evidence/story-12-2-external-workload-authority-20261111/parent-audit.json): all five matrix rows have passing component coverage; frozen intent, source bindings, protected test bodies and source/output copies verify. A final whitespace correction was followed by an AppHost rebuild and successful reruns of the eight focused and sixteen component cases. Actual enrollment and current strict acceptance remain open at workflow step 03.

### Selected latest run-owned qualification — 2026-11-13

The user selected **keep latest**. Platform `a7f901ae79ed5fd7ad2f04f0779b21108a4cdee9`
contains the producer and bridge; its Story 2.12 spec remains in-progress. Existing final-cohort
adoption, unchanged consumer invocation, failure controls and owned disposal are implemented.
The real Keycloak 26.6.4 consumer controls pass for eight resources/six workloads, including
receipt, TLS, signer, scope and foreign-context refusal controls. Both real fixture runs observe
verified run-owned enrollment and zero remaining owned resources; no owner enrollment is claimed.

After correcting Folders' stale .NET 10 catalog metadata path, 82 AppHost controls and the
99-assembly source/PDB/assets audit pass. All 24 actual SDK observations in the final strict run
match its audited producer. The unchanged strict class nevertheless fails 1/1, zero skips,
in 142.301 seconds during `ObserveOwnerBootstrapAsync`; its original deadline cancels before
the scenario body or original 403 control. Source, runtime and working-tree state do not drift
during that run. This is current failed acceptance evidence, not a completed runtime scenario.

A separate run-owned authority probe observes HTTP 200 for the enrolled physical gateway
audience and HTTP 400 for the native SDK's logical `eventstore` audience, with the same caller,
operation and bindings. The [bounded audience proposal](proposal-12-2-canonical-workload-audience.md)
would correct the Platform inventory and receiver audience while preserving exact grants and
cryptographic checks. It remains unapplied because frozen intent requires an unchanged consumer.
The [metadata-only runtime evidence](evidence/story-12-2-latest-runtime-20261113.json) binds all
commands, results, runtime identities and cleanup. External-prerequisite and full acceptance
evidence tasks remain open; the child and parent stay in-progress pending that decision and
successful unchanged strict qualification.

### Ordinary rebuild assertion freshness — 2026-11-14

The approved audience correction and all focused live SDK/consumer controls pass. Current strict
qualification now completes real bootstrap and the original provider-denial/preparation/lock/clean
release/terminal-read workflow, then fails named-projection discovery with HTTP 401. The receiver
records `assertion-expired`: the fixture caches its short-lived metadata assertion at bootstrap,
but refreshes only the capacity lane. This is a fixture credential-lifecycle defect.

- [x] `tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs`: reuse the existing
  renewal helper before ordinary named discovery and each new shared-rebuild action. Keep the
  same actual caller, scope and operation; preserve the 120-second token bound, fixed authority
  lease, strict bodies, original HTTP/action budgets, retry rules and immutable headers within
  each request retry. A 90-second preflight refresh window covers the existing 75-second action
  budget without increasing it. Refusal/expired-token negative controls remain unchanged.
- [x] Rebuild the affected Debug/source graph, run fixture/inventory controls, re-audit exact
  source/runtime/loaded identities and execute the unchanged strict class in a fresh owned run.
  Retain the 271.290-second failed attempt and all earlier evidence with their exact-source scope.


### Approved audience and current strict result — 2026-11-14

The audience proposal is applied in the selected Platform working tree. The earlier pending
approval notes above describe prior phases and are superseded by the explicit approved frozen
exception. The gateway uses exactly `eventstore`; physical caller/target IDs, callback audiences,
operations, bindings and grant counts remain unchanged. Current source is bound in the
[focused evidence](evidence/story-12-2-canonical-audience-focused-20261113.json) and
[current runtime evidence](evidence/story-12-2-canonical-audience-runtime-20261114.json).

Final focused qualification passes 23 provider, 44 Python, 36 Platform inventory/refusal,
one Platform live SDK, one bridge-cancellation, one Folders live SDK, 11 existing actual-issuer
SDK, five live-consumer, ten enrollment-model and 23 existing source-boundary cases. These
live results use the actual run-owned authority and include canonical-audience issuance,
physical-audience substitution refusal and independent scope/source-proof validation.
The ordinary Platform test-project build still has 52 CA2007 and five CA1062 errors in
unrelated files; the focused build retains analyzers, production references, package
registrations, helpers and generated entry points without suppressions.

The full Folders Debug/source solution and Memories runtime builds pass without warnings or
errors. The fixture refresh passes 82 controls; pending-inventory controls pass 21/21 and the
unchanged SDK source privacy scanner passes 286/286. All 99 selected assemblies pass the final
source/PDB/assets audit, with one SDK variant. The final strict run's 116 observed SDK loads
match its saved pre-run producer; source, heads, working-tree status and runtime bytes are
stable during each run. The mutable pending inventory binds 1,615 inputs and retains
`pending-a6b`, `candidate-awaiting-a6b`, disabled production exposure and no closure claim.

The unchanged strict class was executed in three fresh owned runs:

- 271.290 seconds: one failure, zero errors/skips. Real bootstrap, the original provider 403
  control and prepare/lock/clean-release/terminal-ready assertions pass, then discovery rejects
  the cached expired assertion. The renewal correction above addresses this fixture defect.
- 291.099 seconds: one failure, two framework errors, zero skips. Public-status polling cancels
  and console writes report `No space left on device`; sole causality is not established.
  Only an owned 64 MiB write/fsync probe was created and removed; no unrelated resources,
  caches, images or volumes were deleted.
- 474.271 seconds: one failure, zero errors/skips and no storage error. Bootstrap and the
  original 403 control pass. Passing sequential assertions verify retained-source/binding
  exactness, source-time TTLs, all six concerns' integrity and shared empty-checkpoint rebuild,
  sealed interrupted-generation recovery across changed writer PIDs, resumed promotion and
  replay, and two concurrent Workers' overlapping-write convergence. The first complete
  four-host restart succeeds. The second restart fails when DCP's supported EventStore stop
  command reports `timed out waiting for process to stop`; the false command result is still
  rejected. Later restart/fault controls and complete SM4 qualification are not reached.

Every run's observed owned descendants, containers and temporary directories are absent after
cleanup. These are run-owned results; no persistent owner-enrollment qualification is claimed.
Read-only SDK inspection confirms the logger provider is registered. An isolated synthetic SDK
host stops and disposes successfully, but does not reproduce the full EventStore/DCP host or
identify the asynchronous shutdown cause. No SDK shutdown source or telemetry policy was changed.
The parent scope explicitly excludes “generic SDK shutdown/performance repair or policy changes.”

The approved child frozen SHA-256 remains
`a31c07d7cb62a688a0ab1e0999a790800f5f9213588663215763230b5adecb03`; parent frozen497c and
all three protected acceptance bodies remain unchanged. No further dependency update, commit,
push or deployment was performed. Current strict acceptance, the external prerequisite disposition,
full SDK/Release-package qualification, PD10/A6b governance, complete SM4 and independent
whole-story review remain open. This child, parent and sprint story remain in-progress at build
workflow step 03.
