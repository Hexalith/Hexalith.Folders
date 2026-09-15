---
title: 'Publish OQ4 canonical provider compatibility catalog'
type: 'feature'
created: '2026-09-15'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '33f0e114bd0b2a2a21865a84043aa65cbb187bfe'
context:
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** OQ4 is the last open bounded decision and C12 the last `reference_pending` criterion, so provider-ready status and the Epic 12 durable sequence stay blocked. `docs/contract/provider-compatibility-catalog.md` is an unversioned, un-digested prose file whose only approval record covers the GitHub profile; the per-call timeout, retry-limit, and backoff-cap ceilings the PRD promises it publishes are absent; and the nightly drift gate reports a hardcoded `live-provider-drift` placeholder.

**Approach:** Publish a versioned canonical catalog that adds the missing call-ceiling, readiness-outcome, and instance-identity profile, bind Provider, Architecture, and PM approval to its SHA-256, add offline governance gates mirroring OQ2/OQ3, and close C12 on hermetic plus scheduled drift evidence with the narrowed evidence standard and the remaining credentialed-live debt written down.

## Boundaries & Constraints

**Always:** Publish ceilings that describe what the adapters actually enforce, and record every unenforced ceiling as a numbered gap. Keep every string `GitHubDependencyGuardTests` pins: the single exact line `- OQ4 status: approved`, the `- OQ4 approval record:` prefix line, the substring `The GitHub OQ4 profile in this catalog is approved`, and the full case-sensitive `requiredEvidence` list. LF-pin the new artifacts in `.gitattributes`. Bind version, digest, named signer, date, and reopen policy. Keep governance-criterion status separate from NFR-row status per the decoupling precedent.

**Never:** Claim a credentialed live provider run occurred, invent a GitHub live suite, or reinterpret hermetic evidence as live mutation evidence. Weaken or delete the existing OQ4 approval-claim guard. Flip NFR49 to `covered` or touch the `{C3,C4,C7,C12}` hard-pin in `NfrTraceabilityConformanceTests`. Claim Stories 3.3, 3.14, 12.1, or 12.3 complete, or change any provider adapter's runtime behavior.

## Approved Decisions

- Catalog version `1.0.0`. Publish the ceilings as observed from code: GitHub 5s mutation-send and 5s tree-observation budgets, Forgejo 30s REST response/body, HttpClient, and native-transport deadlines, shared 15-minute/5-check reconciliation window, shared 24-hour `Retry-After` clamp, and the 100-change/1 MiB/10 MiB input caps. Record as gaps that GitHub pins no adapter-level per-call REST timeout, that no retry-limit has a referent because no adapter retry loop exists, and that the 24-hour clamp bounds a passed-through value rather than a backoff algorithm.
- Approve with three authority records — Provider, Architecture, and PM — each signed `Administrator` on `2026-09-15` and bound to the catalog version and digest.
- Close C12 on hermetic-PR-gate plus scheduled containerized/fixture drift evidence, and state that narrowed standard verbatim in the C12 `result_summary` and the OQ4 `runtime_posture`, naming credentialed live provider runs as residual provider-ready debt. C7 is the precedent: approved while its summary names still-pending runtime evidence.
- GitHub's drift lane is a pinned-profile manifest plus the C12 fixture-to-failure-mode coverage matrix, not a swagger-snapshot diff, because GitHub REST is one dated API behind a pinned Octokit package. Replace the `live-provider-drift` placeholder with real per-provider status plus an explicit not-run row for credentialed evidence. Do not add any network call to `api.github.com`.
- Fix the broken relative link at `docs/operations/provider-integration-and-testing.md:99` to `../contract/provider-compatibility-catalog.md`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Approved package | Catalog digest matches evidence and every required authority has a fresh exact record | OQ4 governance gate passes | No protected value enters evidence |
| Digest drift | Catalog edited without updating the evidence digest | Gate fails closed naming the mismatch | Metadata-only diagnostic |
| Bad approval record | Stale, future-dated, generic-token signer, or an authority-record set that is not exactly the required three | Gate fails closed | Reports authority id only |
| Unpinned ceiling | A published ceiling has no recorded gap and no code referent | Content gate fails closed | Reports the ceiling id |
| Approval-claim drift | A new catalog line pairs `OQ4` with `approved`/`accepted` outside the allowlist | GitHub guard fails closed | Reports the line index only |

</frozen-after-approval>

## Code Map

- `docs/contract/provider-compatibility-catalog.md` -- canonical artifact to version and extend; lines 10, 11, 68 and the `requiredEvidence` substrings are test-load-bearing.
- `docs/contract/oq3-authorization-evidence.yaml` -- shape to mirror for the new `oq4-provider-compatibility-evidence.yaml`.
- `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` -- copy `EvaluateOq3Evidence` and its positive/negative pair to `oq4_*`; digest rule is `Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(...)))`, i.e. plain `sha256sum`; `ApprovalBackedCriteria` at lines 115-120 is `["C3","C4","C7"]`.
- `docs/exit-criteria/c0-c13-governance-evidence.yaml` -- C12 at lines 204-216; C7 at 142-171 is the approval-backed row model; `approval_policy.max_age_days: 365`.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs:49-118` -- OQ4 allowlist; extend, never relax.
- `src/Hexalith.Folders/Providers/` -- ceiling values to publish, read only: GitHub `GitHubProvider.Operations.cs:7-11` and `OctokitGitHubApiClient.cs:14-22,1941`; Forgejo `ForgejoProvider.Operations.cs:7-13`, `ForgejoHttpApiClient.Operations.cs:16`, `ForgejoHttpApiClientFactory.cs:20`, `ForgejoSmartHttpGitTransport.cs:24-27`.
- `tests/tools/run-nightly-drift-gates.ps1:40-44,448`, `.github/workflows/nightly-drift.yml`, `tests/contracts/forgejo/supported-versions.json` -- existing drift evidence and the placeholder category.
- `tests/tools/run-governance-completeness-gates.ps1:33-44`, `docs/contract/governance-and-completeness-ci-gates.md` -- extend the one offline command and its bounded input inventory.
- `_bmad-output/planning-artifacts/{prd.md,architecture.md,epics.md,planning-story-manifest.yaml,.memlog.md}` -- prd.md:612,720,1053; architecture.md:215,265,270; epics.md:258,597,2616,2626; manifest OQ4 entry at 510-524.

## Tasks & Acceptance

**Execution:**
- [x] `docs/contract/provider-compatibility-catalog.md`, `.gitattributes` -- publish version `1.0.0` with the call-ceiling, readiness-outcome, and product/instance-identity profile plus numbered gaps; LF-pin; preserve every guarded string.
- [x] `docs/contract/oq4-provider-compatibility-evidence.yaml`, `.gitattributes` -- new digest-bound package: catalog version/path/sha256, canonical surfaces, ceiling and gap inventory, three authority records, runtime posture, reopen policy.
- [x] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/ProviderCompatibilityCatalogContractTests.cs` -- new content gate proving every required profile section, provider, ceiling, and gap id is present exactly once, with fail-closed negative controls.
- [x] `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` -- add the OQ4 package and negative-control pair with `oq4_*` metadata-only diagnostics.
- [x] `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs` -- extend the OQ4 allowlist for the new catalog wording while keeping unauthorized approval claims failing closed.
- [x] `tests/tools/run-governance-completeness-gates.ps1`, `docs/contract/governance-and-completeness-ci-gates.md`, `docs/operations/provider-integration-and-testing.md` -- add OQ4 to the offline command inventory and operator guidance; fix the broken catalog link.
- [x] `tests/contracts/github/pinned-profile.json` -- new hermetic manifest pinning the Octokit package version, `X-GitHub-Api-Version`, product header, and the failure-mode coverage matrix mapping every catalog-claimed provider-neutral category to its proving fixture.
- [x] `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDriftConformanceTests.cs` -- new hermetic gate asserting the manifest matches the catalog and the pinned package, and that every matrix row has a real proving test; fail closed on an unmapped or orphaned category.
- [x] `tests/tools/run-nightly-drift-gates.ps1`, `.github/workflows/nightly-drift.yml` -- add the GitHub lane, replace the `live-provider-drift` placeholder with real per-provider status plus an explicit credentialed-evidence not-run row, and keep the report metadata-only.
- [x] `docs/exit-criteria/c0-c13-governance-evidence.yaml`, `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs` -- flip C12 to `approved` with an approval block and a `result_summary` stating the narrowed evidence standard and residual credentialed-live debt; add `"C12"` to `ApprovalBackedCriteria` and clear its placeholders in the same commit.
- [x] `_bmad-output/planning-artifacts/prd.md`, `architecture.md`, `epics.md`, `planning-story-manifest.yaml`, `.memlog.md` -- synchronize the OQ4 and C12 status without claiming dependent runtime or live-provider completion.

**Acceptance Criteria:**
- Given the published catalog, when the content gate runs, then every provider profile section, published ceiling, and recorded gap is present exactly once and each ceiling either cites an enforcing constant or is recorded as a gap.
- Given any catalog, digest, version, authority, signer, date, ceiling, or gap drift, when governance gates run, then OQ4 fails closed with metadata-only diagnostics.
- Given the GitHub pinned-profile manifest, when the drift gate runs, then every provider-neutral failure category the catalog claims maps to exactly one proving fixture, and an unmapped category, an orphaned row, or a package/API-version mismatch fails closed without any network call.
- Given the nightly drift run, when its report is read, then no category reports a hardcoded placeholder status, each provider reports real hermetic status, and credentialed live evidence is reported as explicitly not run.
- Given C12 approval, when planning artifacts and evidence are inspected, then the narrowed evidence standard is stated in `result_summary` and `runtime_posture`, and the Forgejo credentialed live-evidence lane, the GitHub live-mutation waiver, and Stories 3.3/3.14/12.1/12.3 remain explicitly incomplete and receive no completion claim.

## Implementation Notes

Implemented 2026-09-15 on baseline `33f0e11`.

- Catalog version `1.0.0` adds five sections: catalog identity and OQ4 governance (three `- OQ4 authority approval:` records, reopen policy, C12 evidence standard), product/instance identity, call ceilings `CC1`-`CC12`, the nine-row readiness-outcome profile, recorded gaps `PG1`-`PG3`, and the GitHub hermetic drift lane with its ten claimed provider-neutral failure categories. Every guarded string is preserved, including the single exact `- OQ4 status: approved` line, the `- OQ4 approval record:` prefix line, the footer substring, and the full `requiredEvidence` list. LF-stable SHA-256 is `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a`.
- `GitHubDependencyGuardTests` now evaluates approval claims through two pure functions that return line indexes only (`EvaluateUnauthorizedOq4ApprovalClaims`, `EvaluateAllowlistedLinesWithoutApprovalWording`), with a synthetic negative-control test. The allowlist gained exactly one shape, `- OQ4 authority approval:`; every other OQ4 line pairing `approved`/`accepted` still fails closed.
- The GitHub drift lane is `tests/contracts/github/pinned-profile.json` plus `GitHubDriftConformanceTests`, which resolves each matrix row's proving fixture by reflection and verifies its `InlineData` actually pairs the claimed `GitHubApiFailureCondition` with the claimed `ProviderFailureCategory`. Unmapped, orphaned, duplicate, unknown-category, missing-fixture, unproven-condition, package, API-version, product-header, catalog-version, schema, and network-posture drift each fail closed. No network call is made or permitted.
- The nightly drift report now carries `providers`, a derived per-provider `provider_status` block, and an explicit `credentialed-live-provider-evidence: not_run` row. The `live-provider-drift` / `reference_pending_story_7_8` placeholder is gone from the nightly lane; the policy-conformance lane keeps its own unrelated `reference_pending_story_7_8` boundary.
- Pre-existing drift fixed in lockstep: the nightly Forgejo lane pinned `expected_test_count = 8` while `ForgejoManifestAndDriftTests` now contains 10 cases, so the gate could not pass at baseline. Raised to 10 in the script and in `ScheduledDriftAndPolicyWorkflowConformanceTests`.
- C12 flipped to `approved` with a three-authority approval block, cleared placeholders, and a `result_summary` that states the narrowed standard and names NFR49 as still reference-pending. `ApprovalBackedCriteria` gained `"C12"` in the same commit. NFR49 was not touched and the `{C3,C4,C7,C12}` hard-pin in `ReferencePendingRowsAreOwnedAndSurfaceKnownGaps` is unchanged.
- `NfrTraceabilityConformanceTests.GovernanceEvidenceReferencePendingCriteriaStaySurfaced` rested on a non-empty governance reference-pending set. C12 was the last such row, so the guard was replaced with a stronger one: every criterion status must be a known value, the governance-pending subset direction still holds, and `C3`/`C4`/`C7`/`C12` must stay surfaced as owned reference-pending NFR gaps despite criterion approval.


## Spec Change Log

## Review Triage Log

Review round 1 (2026-09-15). The canonical catalog was NOT edited; its SHA-256 stays
`5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a`, so the three 2026-09-15 approvals remain
bound to the content the authorities signed. Every fix lands in gates, scripts, the manifest, or planning prose.

1. Ceilings were published but never resolved. New `ProviderCallCeilingConformanceTests` parses the `CC*` rows,
   resolves each cited `Type.Member` by reflection (`NonPublic | Static`) and compares it to the value the row
   publishes; `CC5`/`CC8` are declared prose-cited and pinned to their literal, with `CC5` probed live off the
   Forgejo factory's HttpClient. Mutation-verified: 15 -> 5 minutes on `GitHubProvider.ReconciliationWindow`
   reddens it.
2. The claimed category set was untethered from the mapper. `GitHubDriftConformanceTests` now drives every
   non-None `GitHubApiFailureCondition` through `ToProviderOperationFailure` and the readiness
   `ToProviderFailure`, asserts the resulting codomain equals the catalog's ten claimed categories, and
   asserts every condition is handled (`ExistingEquivalent` via the declared `KnownFailureMappings` success
   short-circuit).
3. Pinned test counts were substring-only. A new test counts real xUnit cases (`[Fact]` + `[InlineData]`) on
   both drift classes and compares them to the integers parsed from the script. The GitHub lane count moved
   3 -> 5 in lockstep.
4. `Get-ProviderHermeticStatus` was unverified and hardcoded its category lists. Categories are now derived
   from `$categories` by `github-*` / `forgejo-*` prefix, and `Assert-ProviderHermeticStatusDerivation` runs
   six synthetic cases (passed / failed / in_progress / not_started x2 / empty category set) on every gate run.
   Mutation-verified: a `return 'passed'` body fails the gate with `hermetic-status-derivation-drift`.
5. PRD line 612 restored the normative "Provider calls use a bounded retry/backoff policy" clause; `PG2` is now
   stated as the recorded non-conformance against it, and one sentence records that the `CC1`-`CC12` prose
   overlaps PD6's reserved relock scope without touching any of the 73 hash-pinned NFR bullets.
6. The `- OQ4 authority approval:` allowlist was a bare prefix. It is now an exact whole-line regex pinning the
   authority set, signer, date shape, and catalog version, and requiring the closing sentence to name the same
   authority; the footer allowance is a whole-line equality. The reviewer's bypass line and four more variants
   are checked-in failing cases.
7. LibGit2Sharp was published as governed identity but ungated. The manifest now carries
   `libGit2SharpPackageVersion` / `libGit2SharpPinPath`, the C# gate mirrors the Octokit
   `github_profile_package_mismatch` check against repo-root `Directory.Packages.props`, and the nightly script
   gained matching `catalog-native-package-version-drift` / `native-package-pin-drift` checks.
8. `provingAssembly` was declared but ignored. It is now read into the model, asserted equal to the resolving
   assembly's simple name, and resolution goes through that same assembly reference.
9. `Sections` gained the missing set-equality against `RequiredSections`.
10. New `.gitattributes` assertion in the OQ4 governance test: the catalog, the evidence YAML and the pinned
    profile must each stay `text eol=lf`, since those pins are what make the approval digest reproducible.
11. The dead `governancePending` loop is gone, replaced by an explicit assertion that no governance criterion
    is `reference_pending` (with a message telling a future author to restore the projection), and the
    `{C3,C4,C7,C12}` set is hoisted into one shared `OwnedReferencePendingCriteria` constant used by both tests.
12. `credentialed_live_provider_evidence` gained a `closing_condition` naming exactly what would retire the
    not-run status, asserted by the script conformance test and by a new report-shape test.


| # | Finding (layer) | Verdict | Evidence | Route |
|---|---|---|---|---|
| 1 | Published ceilings CC1-CC12 are not bound to the constants they name (verification-gap, blind-hunter, edge-case) | high | `EvaluateCatalog` treats any non-`none` string as "cites a constant"; no cited constant is resolved and no published value is compared. Grepped `tests/` for `MaximumMutationRequestElapsed`, `MaximumTreeElapsed`, `OperationResponseTimeout`, `NativeOperationDeadline`, `OperationReconciliationWindow`, `MaximumAggregateContentBytes`: zero hits. The catalog's entire thesis ("published as observed in adapter code") is unenforced. | patch |
| 2 | Nightly expected-test counts are pinned only by substring match on the script text (verification-gap, blind-hunter, edge-case) | medium | `ScheduledDriftAndPolicyWorkflowConformanceTests` asserts `ShouldContain("expected_test_count = 10")` etc. against file text; the script is never executed. This exact failure already occurred: the baseline pinned 8 while `ForgejoManifestAndDriftTests` had grown to 10 — I confirmed the class has 10 cases and this diff touches no Forgejo test file. | patch |
| 3 | `Get-ProviderHermeticStatus` is verified only by its name appearing in the script; provider category lists are hardcoded (verification-gap, edge-case) | medium | Only assertion is `script.ShouldContain("Get-ProviderHermeticStatus")`. No PowerShell test harness exists in-repo. Replacing the body with `return 'passed'` keeps every test green, letting a failed lane publish `hermetic_status: passed` into C12's standing evidence. | patch |
| 4 | PRD deleted the normative clause "Provider calls use a bounded retry/backoff policy" (blind-hunter, edge-case, and my own diff read) | medium | `prd.md:612`. Gap `PG2` records that no adapter implements a retry loop — the honest response is to publish the requirement as unmet, not delete it. No test pins the sentence. PD6 (`prd.md:1104`) also reserves "add provider timeout/retry/backoff ceilings" for a lockstep relock that this edit bypassed unmentioned. | patch |
| 5 | OQ4 approval-claim allowlist broadened with a free-text prefix (blind-hunter) | medium | Reproduced: a line `- OQ4 authority approval: Provider, signer Administrator, dated 2026-09-15. Also the Forgejo live evidence lane is accepted.` is allowlisted and skipped entirely. Spec said "extend, never relax" and "Never weaken the existing OQ4 approval-claim guard". | patch |
| 6 | Nothing ties the catalog's claimed failure categories to `GitHubFailureMapper`'s codomain or to condition coverage (blind-hunter) | medium | The gate cross-checks only catalog claims vs manifest rows. A new mapper category, or a new `GitHubApiFailureCondition` mapped to an existing category, reddens nothing — that is precisely the upstream drift this lane exists to catch. | patch |
| 7 | Catalog under-claims the GitHub failure surface (`ProviderConfigurationMissing`, `ReconciliationRequired`) (blind-hunter) | false | `GitHubFailureMapper` produces exactly 10 distinct categories, identical to the 10 the catalog claims. Both named categories come from credential-resolver configuration and reconciliation exhaustion, not from any `GitHubApiFailureCondition` mapping, so they fall outside the API-drift surface the matrix scopes. The claim list is the mapper's codomain. | rejected |
| 8 | Forgejo's published LibGit2Sharp pin is ungated (blind-hunter) | medium | Catalog publishes "LibGit2Sharp `0.32.0`" as governed identity; only Octokit is checked. Confirmed pinned at repo-root `Directory.Packages.props:15`. A bump silently falsifies a digest-bound, three-authority-approved catalog. | patch |
| 9 | `provingAssembly` is a decorative manifest field (blind-hunter, edge-case) | low | Declared in the manifest, checked non-empty by the script, never read by `LoadManifest`/`EvaluatePinnedProfile`; resolution hardcodes `typeof(GitHubDriftConformanceTests).Assembly`. Cheap to make real. | patch |
| 10 | Catalog `## ` sections get missing/duplicate checks but no set-equality (edge-case) | low | `Ceilings`, `GapIds`, `ReadinessRows` all get `ShouldBe(..., ignoreOrder: true)`; `Sections` does not, so an unapproved new section passes the content gate. One-line fix that passes today. | patch |
| 11 | `.gitattributes` LF pins are unguarded (verification-gap, other) | low | The three `text eol=lf` pins are what make the SHA-256 binding stable, and no test asserts they exist. Failure mode is fail-closed, but surfaces as an unexplained digest mismatch on a Windows checkout rather than a named cause. | patch |
| 12 | `{C3,C4,C7,C12}` pin duplicated and one loop went vacuous (blind-hunter) | low | Confirmed: governance now declares 0 `reference_pending` criteria, so `foreach (criterion in governancePending)` is provably dead, and the four-criterion set is now literal in two tests. | patch |
| 13 | Removing `follow_up_boundary` left the residual debt with no closing condition (edge-case) | medium | The deleted block was the only machine-readable statement of when the not-run status may be retired; `residual_debt` replaced it with prose only. | patch |
| 14 | NFR34 stays `covered` while PG1/PG2 publish that no per-call REST timeout and no retry/backoff referent exist (edge-case) | medium | Confirmed: `nfr-traceability.md:80` NFR34 = `covered`, hash `17a95286cc6f`; `epics.md:184` reads "Provider calls must use explicit timeout budgets, retry limits, and backoff caps." The new catalog is its own counter-evidence. Pre-existing inaccuracy this change surfaced; the fix touches hash-pinned NFR rows that PD6 reserves for a lockstep relock. | defer |
| 15 | NFR49's residual credentialed-live debt points at a completed story (blind-hunter) | medium | Confirmed: `nfr-traceability.md:95` and `:181` name consuming story `7-8`, and `sprint-status.yaml:168` records `7-8-wire-scheduled-drift-and-policy-conformance-workflows: done`. Deleting C12's `open_policy_placeholders` removed the last structured owner. Row edit is hash-pinned (`897e345f0e61`), so reconciliation belongs to the PD6 relock. | defer |
| 16 | Catalog omits GitHub's `capability_profile_schema` value (blind-hunter) | low | Confirmed: `GitHubReadinessMapper.cs:67` emits `"v1"`, identical to Forgejo's, but the identity table prints it only for forgejo. Correcting it edits the catalog, which re-cuts the approval-bound SHA-256 and would re-stamp a human sign-off onto content the human did not approve. | defer |
| 17 | PD6 lockstep relock overlap unrecorded (blind-hunter) | medium | `prd.md:1104` reserves "add provider timeout/retry/backoff ceilings" for a lockstep change of `prd.md`, `epics.md` NFR1-NFR73 and the `nfr-traceability.md` hashes. Covered by patch #4 for the prose note; the hash-pinned half is deferred with #14. | patch + defer |
| 18 | Crash-instead-of-diagnostic on malformed manifest/catalog: overloaded proving fixture, missing JSON property, absent/duplicate catalog-version line, `[Fact]`/`MemberData` fixture (edge-case ×4) | low | All four fail closed — an exception reddens the gate exactly as a diagnostic would. The loss is message quality on inputs the gate itself owns, and each fix adds branches. | rejected |
| 19 | PowerShell lane is case-insensitive on duplicates and does not detect unmapped categories (edge-case ×2) | low | `Select-Object -Unique` is case-insensitive, but the C# lane runs in the same nightly job with ordinal comparison and `Enum.IsDefined`, so no false pass survives the run. Defense in depth already present. | rejected |
| 20 | Manifest `provider` / `driftLane` / `packagePinPath` have no fail-closed diagnostic category (blind-hunter) | low | True that they sit outside the `DriftDiagnostic` model, but all three are asserted in the happy-path test, so drift still reddens. Fix would add three categories plus doc entries and negative controls. | rejected |
| 21 | `credentialed-live-provider-evidence` status is a hardcoded literal (edge-case) | false | The acceptance criterion requires that credentialed live evidence be "reported as explicitly not run". A constant is the correct implementation of a constant claim; the defect removed was a placeholder standing in for *hermetic* status, which is now derived. | rejected |
| 22 | The no-network-call property is established by substring checks (verification-gap, other) | low | True — the test observes text, not behavior. The property holds today (the lane only reads files and reflects over attributes), and behavioural enforcement is not cheaply available here. | rejected |
| 23 | Catalog parsing is not section-scoped (blind-hunter, edge-case) | low | Regexes run over the whole file. `ReadinessRowPattern` would absorb any future backticked snake_case table row, but set-equality makes that fail closed and noisy rather than silent. Section set-equality is covered by patch #10. | rejected |

## Verification

**Results (2026-09-15, re-verified by the build workflow after the review patch round):**
- Catalog SHA-256 unchanged at `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a`; the patch round edited gates, scripts, the manifest and planning prose only, so the three 2026-09-15 approvals stay bound to the content those authorities signed.
- `Hexalith.Folders.Contracts.Tests` 314 passed / 0 failed; `Hexalith.Folders.Tests` 1961 passed / 0 failed / 1 pre-existing skip (`ForgejoSmartHttpGitTransportIntegrationTests.AlpineTlsProfileReceivesExactPackAndRejectsPostAdvertisementStaleOld`).
- `run-governance-completeness-gates.ps1 -SkipRestoreBuild` -> `passed`; `run-nightly-drift-gates.ps1 -SkipRestoreBuild -ProviderProfile pinned-snapshots` -> `passed`, 6 hermetic categories plus `credentialed-live-provider-evidence: not_run`, zero placeholder strings.
- Mutation check on the review's highest-severity finding: setting `GitHubProvider.ReconciliationWindow` to 5 minutes now fails `ProviderCallCeilingConformanceTests.EveryPublishedCallCeilingMatchesTheConstantTheCatalogCites`; the adapter was restored and re-verified green. Before the patch that mutation left every gate passing.
- `dotnet format whitespace` / `analyzers --verify-no-changes`: no touched file carries a finding. Pre-existing, untouched: `ProviderOperationSourceResolutionResult.cs`, `ForgejoSmartHttpGitTransportTests.cs`, and the `references/` submodules.
- Four findings deferred to `deferred-work.md` (NFR34 vs PG1/PG2, NFR49's completed consuming story `7-8`, GitHub's omitted `capability_profile_schema`, PD6 relock overlap) — each needs a human governance action, not a code patch.

**Original implementation-round results (2026-09-15):**
- `sha256sum docs/contract/provider-compatibility-catalog.md` -> `5799e090a005addebb8361ba42f36a075ecafd60350837cdd5e29a1d6bad228a`, matching `catalog_sha256`, all three `approval.records[].evidence_sha256`, the C12 row digest, and the prd/memlog/manifest pins.
- `tests/Hexalith.Folders.Contracts.Tests` built assembly: 313 passed, 0 failed (baseline 308; +3 catalog content gate, +2 OQ4 governance package).
- `tests/Hexalith.Folders.Tests` built assembly: 1958 passed, 0 failed, 1 skipped (pre-existing Forgejo Alpine fixture skip).
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -> `passed`, with both OQ4 paths in `canonical_inputs`.
- `pwsh ./tests/tools/run-nightly-drift-gates.ps1 -SkipRestoreBuild -ProviderProfile pinned-snapshots` -> `passed`; GitHub categories present; no `reference_pending_story_7_8` anywhere in the report.
- `dotnet format whitespace --verify-no-changes` / `analyzers --verify-no-changes`: clean for every file this change touches. Pre-existing unrelated whitespace findings remain in `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs`, `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoSmartHttpGitTransportTests.cs`, and the `references/` submodules.
- Pre-existing and untouched: `ScaffoldContractTests` 3 `.slnx`-inventory failures in `tests/Hexalith.Folders.Testing.Tests` (reproduced with this change stashed).

**Commands:**
- `sha256sum docs/contract/provider-compatibility-catalog.md` -- expected: matches `catalog_sha256` and every `approval.records[].evidence_sha256`.
- `dotnet build <proj> -m:1 -p:NuGetAudit=false` then run the built assembly under `bin/Debug/net10.0/` for `tests/Hexalith.Folders.Contracts.Tests` and `tests/Hexalith.Folders.Tests` -- expected: 0 failed (Contracts baseline 308 passed at `33f0e11`). `dotnet test` is environment-blocked by the VSTest/MTP error, so always run the built assembly.
- `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` -- expected: gate status `passed` with OQ4 in `canonical_inputs`.
- `pwsh ./tests/tools/run-nightly-drift-gates.ps1 -SkipRestoreBuild -ProviderProfile pinned-snapshots` -- expected: `passed`, a GitHub category present, and no `reference_pending_story_7_8` status in the report.
- `dotnet format whitespace --verify-no-changes` and `dotnet format analyzers --verify-no-changes` -- expected: clean.
