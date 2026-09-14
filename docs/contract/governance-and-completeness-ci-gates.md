# Governance And Completeness CI Gates

The governance/completeness gate is the local and CI entry point for Story 1.16 checks. It validates exit-criteria evidence (including fresh, exact approval records for approval-backed criteria), idempotency corpus consumption, opt-in pattern examples, tenant-prefixed cache-key exceptions, and parity completeness without Aspire, Dapr sidecars, provider credentials, network calls, or nested submodule initialization.

## Local Command

```powershell
.\tests\tools\run-governance-completeness-gates.ps1
.\tests\tools\run-governance-completeness-gates.ps1 -SkipRestoreBuild
```

The command must be run from the repository root or from the script location. It writes a sanitized discovery report to `_bmad-output/gates/governance-completeness/latest.json`. The report includes gate names, repository-relative canonical inputs, report path, and diagnostic policy only.

## CI Job

The `contract-spine-gates` workflow invokes the same command after restore and build:

<!-- hexalith-example: documentation-only -->

```powershell
.\tests\tools\run-governance-completeness-gates.ps1 -SkipRestoreBuild
```

Workflow YAML may orchestrate setup, but gate decisions live in checked-in tests and fixtures rather than workflow-only shell logic.

## Owned Inputs

- `docs/exit-criteria/c0-c13-governance-evidence.yaml`
- `docs/exit-criteria/c7-lock-authorization-timing.md`
- `docs/contract/file-context-contract-groups.md`
- `docs/contract/oq2-file-policy-evidence.yaml`
- `docs/contract/authorization-matrix.md`
- `docs/contract/oq3-authorization-evidence.yaml`
- `tests/fixtures/idempotency-encoding-corpus.json`
- `tests/fixtures/idempotency-encoding-corpus.schema.json`
- `tests/fixtures/idempotency-encoding-corpus-consumption.yaml`
- `tests/fixtures/pattern-example-manifest.yaml`
- `tests/fixtures/cache-key-exceptions.yaml`
- `tests/fixtures/parity-contract.yaml`
- `tests/fixtures/parity-contract.schema.json`
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`

## Diagnostic Categories

- `prerequisite_drift`: a required input, owner, path, command, report, or source authority is missing or inconsistent.
- `exit_criteria_missing`: a C0-C13 row is absent.
- `exit_criteria_duplicate`: a criterion appears more than once.
- `exit_criteria_malformed`: required evidence metadata is missing or contains an invalid placeholder.
- `artifact_path_invalid`: an evidence path is absolute, escapes the repository, or points to an unreadable artifact.
- `approval_record_missing`: an approval-backed criterion (for example C3 or C4) is missing its structured `approval` block, its required authorities, or its records.
- `approval_authority_unsatisfied`: a required approval authority has no record, or more than one record, in the criterion's approval block.
- `approval_approver_generic`: an approval record names a generic approver (for example "Legal", "PM", or "signed") or repeats the authority name instead of an exact signer.
- `approval_date_invalid`: an approval record's `approved_on`, or a criterion's `review_by`, is missing or is not a valid `yyyy-MM-dd` date.
- `approval_date_future`: an approval record's `approved_on` is dated in the future.
- `approval_stale`: an approval record is older than the mandatory `approval_policy.max_age_days` window, or a per-criterion `review_by` date has already passed.
- `approval_evidence_version_mismatch`: an approval-backed artifact or approval record does not carry the governed evidence version.
- `approval_evidence_digest_missing`: an approval-backed artifact or approval record omits its required SHA-256 digest.
- `approval_evidence_digest_mismatch`: an approval-backed artifact or approval record's SHA-256 digest does not match the canonical artifact.
- `c7_timing_profile_invalid`: a C7 timing value is missing, differs from the approved profile, is not positive, or violates the authorization-revalidation-to-revocation-SLO relationship.
- `c7_approval_identity_mismatch`: a C7 approval record does not name the exact approved signer, reported without echoing the unexpected value.
- `c7_approval_date_mismatch`: a C7 approval record does not carry the exact approved date, reported without echoing the unexpected value.
- `oq2_evidence_missing`: the OQ2 manifest, canonical policy binding, approval block, or required runtime-posture declaration is absent.
- `oq2_evidence_mismatch`: OQ2 evidence identity, status, policy version/path/digest, approval date, exact reopen policy, canonical surface inventory, or runtime posture differs from the approved package.
- `oq2_approval_incomplete`: one of PM, Architecture, or Security has no single approval record.
- `oq2_approval_extra`: the OQ2 authority or record set contains an unexpected or duplicate entry.
- `oq2_approval_identity_mismatch`: an OQ2 approval record does not name Administrator, reported without echoing the unexpected value.
- `oq2_approval_date_mismatch`: an OQ2 approval record does not carry 2026-09-14, reported without echoing the unexpected value.
- `oq3_evidence_missing`: the OQ3 manifest, canonical matrix binding, denominator block, recorded gap inventory, approval block, or required runtime-posture declaration is absent.
- `oq3_evidence_mismatch`: OQ3 evidence identity, status, matrix version/path/digest, approval date, exact reopen policy, canonical surface inventory, denominator counts, recorded gap IDs, or runtime posture differs from the approved package.
- `oq3_approval_incomplete`: one of Security or PM has no single approval record.
- `oq3_approval_extra`: the OQ3 authority or record set contains an unexpected or duplicate entry.
- `oq3_approval_identity_mismatch`: an OQ3 approval record does not name Administrator, reported without echoing the unexpected value.
- `oq3_approval_date_mismatch`: an OQ3 approval record does not carry 2026-09-14, reported without echoing the unexpected value.
- `oq3_matrix_mismatch`: a matrix family, decision, negative access state, operation identity, family operation count, or gap evidence path differs from the approved denominator or its closed vocabulary.
- `oq3_operation_unmapped`: a current Contract Spine operation has no authorization-matrix row.
- `oq3_operation_unknown`: an authorization-matrix row names an operation the Contract Spine does not declare.
- `oq3_operation_duplicate`: an operation maps to more than one matrix row.
- `oq3_family_uncovered`: one of the 11 protected operation families has no requirement row.
- `oq3_actor_uncovered`: a canonical actor, a canonical negative case, or an actor-by-family decision row is missing.
- `oq3_scope_incomplete`: an operation row does not account for all eight FR8 scope dimensions, or omits a dimension the Contract Spine declares.
- `oq3_denial_shape_mismatch`: a negative access state does not route to the exact canonical 404 safe denial.
- `oq3_gap_unrecorded`: an observed runtime or Contract Spine deviation is not recorded in the matrix conformance-gap table.
- `idempotency_sample_unmapped`: a corpus sample lacks exactly one stable consumption map entry.
- `pattern_example_invalid`: a C# example is unmarked, stale, or not part of the compilable examples project.
- `cache_key_unscoped`: a tenant-data cache key candidate lacks tenant scope and no reviewed exception applies.
- `parity_completeness_mismatch`: OpenAPI operations and generated parity rows differ, duplicate, or omit required metadata.

Diagnostics may include gate names, rule IDs, criterion IDs, sample IDs, operation IDs, schema pointers, repository-relative paths, bounded categories, counts, and safe hashes. Diagnostics must not include raw payloads, file contents, diffs, provider tokens, credentials, tenant data, local absolute paths, production URLs, cache key values, provider responses, or unauthorized-resource hints.

## Approval Records

Approval-backed criteria (those whose `approved` status rests on a human governance sign-off rather than a machine-validated gate — today `C3` retention, `C4` input limits, and `C7` lock/authorization timing) must carry a structured `approval` block in `docs/exit-criteria/c0-c13-governance-evidence.yaml`, not just a free-text `result_summary`. Each block declares the `required_authorities` and one exact `records` entry per authority with a named `approver` and a `yyyy-MM-dd` `approved_on` date. `GovernanceCompletenessGateTests.ApprovalBackedCriteriaCarryFreshExactApprovalRecords` enforces the generic floor, while `C7DecisionPackageBindsProfileVersionDigestAndExactApprovals` applies C7's stricter bounded exact-value checks:

- Every required authority has exactly one record with a specific (non-generic, non-authority-name) approver and a valid, non-future `approved_on`.
- `approval_policy.max_age_days` is a mandatory global freshness window: an approval older than the window fails closed and forces a governance re-review. This time-based redden is intentional — refresh the sign-off (or widen the window by decision) to clear it.
- An optional per-criterion `review_by` date must be a valid date strictly in the future.

C7 additionally binds `evidence_version`, the SHA-256 digest of `docs/exit-criteria/c7-lock-authorization-timing.md`, and the four whole-second timing values to both Architecture and Security records. `GovernanceCompletenessGateTests.C7DecisionPackageBindsProfileVersionDigestAndExactApprovals` rejects value, version, digest, authority, signer, or date drift. A C7 artifact change reopens OQ1 until both authorities approve the new version and digest. This governance approval does not claim runtime coverage: NFR7 and NFR21 remain `reference-pending` until renewal and revocation behavior is executable and evidenced.

The bespoke C3 retention checks in `RetentionAndTenantDeletionConformanceTests` remain the stricter retention-specific gate; this generic floor covers every approval-backed criterion, including future ones.

OQ2 uses a separate versioned manifest because it governs the file-policy decision rather than one C0-C13
criterion row. `GovernanceCompletenessGateTests.Oq2FilePolicyPackageBindsVersionDigestApprovalsAndRuntimePosture`
binds `docs/contract/file-context-contract-groups.md` version `1.1.0` and its LF-stable SHA-256 digest to
exactly one PM, one Architecture, and one Security approval by Administrator dated 2026-09-14. Missing,
mismatched, stale, extra, or incomplete evidence fails closed with bounded metadata-only diagnostics. A policy
content/version/digest, required-authority, signer-identity, or approval-date change reopens all three approvals.
The manifest deliberately keeps Stories 12.1,
12.3, and 4.20 and FR32-FR35 runtime evidence incomplete; design approval is not runtime completion.

OQ3 uses the same separate-manifest shape because it governs the authorization denominator rather than one
C0-C13 criterion row. `GovernanceCompletenessGateTests.Oq3AuthorizationMatrixPackageBindsVersionDigestApprovalsAndRuntimePosture`
binds `docs/contract/authorization-matrix.md` version `1.0.0` and its LF-stable SHA-256 digest to exactly one
Security and one PM approval by Administrator dated 2026-09-14, and
`AuthorizationMatrixContractTests` enforces the denominator itself: 49 Contract Spine operations mapped exactly
once, all 11 protected operation families present, all 6 canonical actors and 6 canonical negative cases
covered, and all 8 FR8 scope dimensions explicit or explicitly not applicable on every operation. Missing,
mismatched, stale, extra, duplicate, or incomplete evidence fails closed with bounded metadata-only
diagnostics. A matrix content/version/digest, required-authority, signer-identity, or approval-date change
reopens both approvals. The manifest deliberately keeps Stories 12.1, 4.19, 4.20, 4.21, 6.14, and 10.8 and
FR8-FR10 runtime evidence incomplete, and the matrix records rather than hides the current Contract Spine and
runtime drift under gap IDs `G1` through `G11`, whose downstream owners are PD10, OQ9, Story 12.1, and Epic 13.

## Contribution Checklist

When adding evidence, corpus cases, pattern examples, cache-key exceptions, or parity row shapes:

1. Add the repository-relative artifact and ownership metadata first.
2. Add or update the stable fixture row that names the owner, status, command, and evidence link.
3. Add positive and negative coverage in `GovernanceCompletenessGateTests`.
4. Keep reference-pending decisions bounded with owner, criterion ID, reason, and consuming story.
5. Regenerate parity rows only through `tests/tools/parity-oracle-generator`; never hand-edit generated rows.
6. Keep all examples synthetic and metadata-only.

## Story Ownership

Story 1.12 owns generated `ComputeIdempotencyHash()` helper behavior. Story 1.13 owns parity-oracle generation. Story 1.14 owns Contract Spine drift and generated-client consistency gates. Story 1.15 owns sentinel redaction and output-channel leakage checks. Story 1.16 only wires governance/completeness checks and cache-key diagnostic metadata needed by these gates.
