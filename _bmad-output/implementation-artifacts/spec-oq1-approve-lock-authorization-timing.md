---
title: 'Approve OQ1 lock and authorization timing'
type: 'feature'
created: '2026-09-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e7386ceab65beeb60019761d8253cc35d867720f'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** OQ1 and C7 remain release-blocking because the lock-renewal interval, authorization-revalidation interval, revocation-effect SLO, and expired-to-stale timing relationship have no approved numeric contract or digest-bound governance record.

**Approach:** Publish the canonical C7 timing decision, record fresh Architecture and Security approval, and harden the offline governance gates so values, approval identity, version, and digest cannot drift silently.

## Boundaries & Constraints

**Always:** Keep owner-only renewal under fresh authorization; fail closed on stale, unavailable, or revoked authority; make tenant overrides no weaker than the approved SLO; define expiry and stale boundaries precisely; preserve metadata-only diagnostics and named, dated, digest-bound approvals.

**Never:** Implement a renewal endpoint, scheduler, revocation propagation, lock takeover, automatic staged-work release, C6/PD11 changes, OQ7 identity work, or claim NFR7/NFR21 executable evidence. C7 governance approval and runtime evidence remain separate tracks.

## Approved Decisions

- Use the security-first profile: renew every 30 seconds, revalidate authorization every 15 seconds, make revocation effective within 60 seconds, and mark an expired lock stale after 60 seconds. Tenant overrides may only tighten these bounds; a caller-requested lease shorter than the renewal interval expires normally and is never silently extended.
- Record `Administrator` as the exact signer for both Architecture and Security, approved on 2026-09-12, following the repository's established OQ8 multi-authority convention.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Approved decision | Canonical artifact and matching version/digest with exact Architecture and Security records | C7 is `approved`; OQ1 is recorded closed | Offline governance gates pass |
| Drift or incomplete approval | Changed digest, missing value, signer, authority, or stale/future date | C7 approval is invalid | Gate fails with bounded metadata-only diagnostic |
| Runtime evidence absent | NFR7/NFR21 still reference pending | Decision remains approved without a false runtime claim | Traceability gate preserves the explicit implementation gap |

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/prd.md` -- authoritative OQ1 closure rule, fixed lock semantics, approvers, and digest/version requirement; record closure without changing adjacent policy.
- `_bmad-output/planning-artifacts/architecture.md` and `.memlog.md` -- align the C7 authority/status narrative and append the governed decision event.
- `docs/exit-criteria/c7-lock-authorization-timing.md` -- new canonical versioned decision artifact, following the C4 structure.
- `docs/exit-criteria/c0-c13-governance-evidence.yaml` -- replace the C7 placeholder with approved artifact metadata and structured approval records.
- `docs/contract/governance-and-completeness-ci-gates.md` and `docs/contract/workspace-lock-contract-groups.md` -- include C7 in approval-backed guidance and replace timing-deferred prose with the governed reference while retaining runtime deferral.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` -- replace only the stale source-document deferral note; do not reshape the API or generated client.
- `tests/Hexalith.Folders.Testing.Tests/ExitCriteriaDecisionArtifactTests.cs` -- validate the artifact's values, boundaries, provenance, and deferred-runtime honesty.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` -- pin C7 as approval-backed and validate exact version/digest/authorities with negative controls.
- `docs/exit-criteria/nfr-traceability.md` -- preserve NFR7/NFR21 as reference-pending executable evidence; do not mark them covered.

## Tasks & Acceptance

**Execution:**
- [x] Create the C7 artifact and synchronize the PRD, architecture, memlog, workspace-lock, OpenAPI description, governance documentation, and evidence manifest without broadening runtime scope.
- [x] Add focused decision-artifact and governance tests, including missing/mismatched digest, incomplete authority, invalid timing relation, and retained NFR-gap coverage.

**Acceptance Criteria:**
- Given the approved profile and signers, when OQ1 evidence is generated, then every timing value has units, boundary semantics, tenant-override limits, rationale, version, SHA-256 digest, exact approver/date records, and a reopen-on-change rule.
- Given any artifact or approval drift, when offline governance checks run, then C7 fails closed without exposing sensitive data.
- Given C7 is governance-approved, when NFR traceability runs, then NFR7/NFR21 remain explicitly reference-pending until runtime renewal and revocation evidence exists.

## Implementation Notes

- Published C7 evidence version `1.0.0` with SHA-256 `47da9d95b5d809a08b0c6097fc9ad97e8bd22e156ee1789f98870345c6be4403`, the approved 30/15/60/60-second profile, boundary rules, tightening-only tenant overrides, and digest-bound Architecture and Security approvals by `Administrator` dated 2026-09-12.
- Synchronized the PRD, architecture, memlog, governance manifest and guidance, workspace-lock contract guidance, OpenAPI description, and NFR traceability without claiming runtime renewal or revocation evidence; NFR7 and NFR21 remain reference-pending.
- Added positive and negative C7 governance coverage for exact values, version/digest integrity, authority completeness, timing validity, artifact drift, and the explicit runtime-evidence gap.
- Hardened the local PowerShell gates discovered during verification: the governance fallback now selects the current Debug runner, while the NFR wrapper recognizes the .NET 10 Microsoft.Testing.Platform fallback condition and preserves native failure exit codes across report generation.
- Review patches clarified the renewal/revocation anchors and effective-override rule, pinned LF digest bytes, synchronized stale planning/report inputs, converted exact C7 drift to bounded diagnostics, and added executable native-failure/no-echo coverage for the NFR wrapper.
- Verification passed: both focused test projects built with zero warnings/errors; decision-artifact tests passed 10/10; governance tests passed 16/16; workspace-lock contract tests passed 6/6; NFR traceability tests passed 17/17; the standalone native-failure control passed 1/1; both PowerShell gate wrappers passed via the repository's xUnit in-process fallback; `git diff --check` passed.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH-01 | medium | patch | `requestedLeaseSeconds` already determines `expiresAt` in `FolderAggregate`, so retaining “metadata only” in the changed OpenAPI description can mislead generated consumers about a behavior-affecting field. |
| BH-02 | medium | patch | C7 defines renewal from `lastSuccessfulRenewalAt` but never states the initial anchor; the acquisition event already supplies `acquiredAt`/`effectiveAt`, so the first due instant needs an explicit deterministic definition. |
| BH-03 | false | reject | `LockWorkspaceRequest.requestedLeaseSeconds` is required and the aggregate computes expiry from it, so OQ1 needs no default lease to make renewal scheduling coherent; intentionally short leases are already permitted. |
| BH-04 | medium | patch | `revocationEffectiveAt` has no explicit authority or clock-domain definition, leaving the 60-second SLO open to an observation-time interpretation that would weaken its end-to-end meaning. |
| BH-05 | false | reject | C7 fixes a contractual SLO but explicitly leaves propagation, scheduling, and production-path proof reference-pending in NFR7/NFR21; the governance gate does not claim the runtime budget is met. |
| BH-06 | medium | defer | Clock-skew/time-source policy was explicitly deferred before OQ1 and no renewal/revocation runtime is implemented; the consuming runtime story must select and test the authoritative clock before executable boundary evidence can close. |
| BH-07 | maybe-false | defer | No tenant timing-override representation or runtime exists yet. C7 already requires positive ceilings and `revalidation <= revocation SLO`; activation, fallback, supported lower bounds, and active-lock behavior must be settled when the tenant-policy consumer is designed. |
| BH-08 | false | reject | The PRD already defines revoked/inaccessible state, new-lock reacquisition after restored authority, and fail-closed behavior until fresh active authority is proven; C7 intentionally avoids duplicating the deferred runtime state machine. |
| BH-09 | false | reject | The frozen human decision explicitly names `Administrator` for both Architecture and Security, so replacing those records would contradict the approved intent and require editing the build spec. |
| BH-10 | medium | patch | Canonical drift does fail through raw assertions, but exact authority/signer/date/count mismatches are not expressed as bounded C7 diagnostics and unexpected approval values may be echoed by Shouldly. |
| BH-11 | medium | patch | `.gitattributes` leaves the digest-bound Markdown artifact at `text=auto` with unspecified EOL, so an unchanged Git blob can hash differently after a CRLF checkout. |
| BH-12 | medium | patch | The governance gate now hashes the C7 artifact but omits it from the report's `canonical_inputs`, making the emitted provenance inventory incomplete. |
| BH-13 | high | patch | The added NFR fallback path prints unrestricted native failure output; the observed .NET 10 failure already includes a host-absolute path, violating the declared metadata-only diagnostic policy. |
| BH-14 | false | reject | Both documented workflows build the default Debug configuration before `-SkipRestoreBuild`, and the corrected fallback executed the current 16-test net10.0 binary; Release-only reuse is not a demonstrated supported path. |
| BH-15 | medium | patch | `epics.md` still says C7 is an unspecified two-number `N` contract, while dependency-spine text still groups closed OQ1 with open OQ2–OQ4; these planning consumers now contradict the approved artifact. |
| BH-16 | false | reject | OQ2–OQ4 were split before the OQ1-only spec existed and are not derived from it; `source_spec: none` accurately records their freeform origin. |
| BH-17 | low | patch | The generated NFR7 gap label still says the authorization budget is missing even though the numeric budget is approved; only runtime revalidation/revocation evidence remains missing. |
| BH-18 | medium | defer | The `Hexalith.EventStore` gitlink moved concurrently after the OQ1 baseline and is unrelated to this story; preserving it and excluding it from OQ1 is required to avoid overwriting user work. |
| VG-01 | medium | patch | Pre-verified: neither the report nor an existing assertion includes the newly canonical C7 artifact, so a passed report can ship with incomplete provenance. |
| VG-02 | medium | patch | Pre-verified: source-substring checks do not execute an unmatched native failure, so regression to exiting with the report helper's later `git` status would escape all healthy-path verification. |
| VG-03 | high | patch | Pre-verified: raw `$testOutput` forwarding can expose stack traces and absolute paths despite the metadata-only conformance policy. |
| EC-01 | false | reject | C7's general expiry rule makes a lock expired at `now >= expiresAt`; therefore expiry already wins when expiry and renewal-due instants are equal, even though the renewal row could state that precedence more directly. |
| EC-02 | medium | patch | The short-lease paragraph hard-codes the global 30-second interval while tenant overrides can make the effective renewal interval shorter, creating conflicting instructions for a 20-second lease under a 10-second override. |
| EC-03 | maybe-false | defer | No tenant override activation path exists, so impact on already-held locks cannot be observed. The future policy consumer must define versioning or atomic re-evaluation before activation. |
| EC-04 | medium | patch | `EvaluateC7DecisionEvidence` checks required timing keys and values but not the exact key set, so an unapproved fifth behavior-affecting setting is accepted. |
| EC-05 | medium | patch | Git confirms the C7 Markdown path has no fixed `eol`; the SHA-256 check is therefore checkout-byte-dependent. |
| EC-06 | high | patch | The failing-test branch writes unbounded process output before classification, directly violating the metadata-only rule. |
| EC-07 | medium | patch | The new .NET 10 fallback can run on Windows, but its hard-coded extensionless executable path rejects the valid `.exe` runner there. |
| EC-08 | false | reject | The supported build and CI paths produce Debug output and the gate now selects the current Debug runner; a hypothetical Release-only `-SkipRestoreBuild` invocation does not establish a current defect. |
| EC-09 | medium | defer | The generic approval evaluator predates OQ1 and skips a present `review_by` with an empty scalar or non-scalar shape. C7 has no `review_by`; a governance-hardening follow-up should fail such malformed optional values closed. |
| EC-10 | false | reject | Exact fixed-value checks already reject every profile where 15-second revalidation exceeds the required 60-second SLO, so loss of the redundant relational branch cannot make an invalid canonical C7 profile pass. |
| EC-11 | medium | patch | Exact approver checks currently use raw Shouldly comparisons, which may render an unexpected manifest scalar even though C7 requires metadata-only gate failures. |

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --no-restore -m:1` -- expected: build succeeds.
- `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --no-restore -m:1` -- expected: build succeeds.
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- expected: C7 decision, digest, and approvals pass.
- `pwsh ./tests/tools/run-nfr-traceability-gates.ps1 -SkipRestoreBuild` -- expected: the explicit runtime gap remains valid.
- `git diff --check` -- expected: no whitespace errors.
