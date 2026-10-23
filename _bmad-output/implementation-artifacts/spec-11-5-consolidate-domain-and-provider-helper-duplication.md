---
title: '11.5 Consolidate domain and provider helper duplication'
type: 'refactor'
created: '2026-10-23'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
story_key: '11-5-consolidate-domain-and-provider-helper-duplication'
baseline_commit: '591194c7766c72d8bd51398d66f814a6a1fca922'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-11-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Duplicated domain/provider helpers make platform adoption harder to verify.

**Approach:** Extract internal Folders-local helpers. Preserve caller policies and fingerprint bytes.

## Boundaries & Constraints

**Always:** Preserve FR17, FR22–FR23, FR32–FR40 behavior, failure categories, metadata-only evidence, authorization order, cancellation, replay, call ceilings, and public signatures. Keep Organization authorization mapping separate. Preserve provider-specific domain prefixes, validation order, exceptions, hash inputs, ordering, and normalization. One type per file.

**Never:** Change dependencies, submodule pins, contracts, generated clients, persistence, search, transports, disposal behavior, or provider orchestration. Reserved-tenant decisions belong to 11.20; platform adoption to 11.8; provider features/correctness and test-double consolidation retain their existing owners. Preserve existing reserved-name differences.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tenant merge | Null/empty payload; existing key | Ordinal copy; overwrite only for non-null payload; source unchanged | Comparison input never becomes authority |
| Authorization | Known/unknown outcomes | Existing Folder enum; Organization keeps ACL → MissingPermission and Dapr → UnavailableProjection | Unknown Folder outcome → TenantAccessDenied |
| Stream segment | Blank, 256/257 chars, ULID, newline | Same shared grammar/opaque acceptance and limits | Preserve per-record codes and exceptions |
| Reserved tenant | system, SYSTEM, padded system | Folder raw ordinal; Organization trimmed case-insensitive | Preserve distinct rejection codes |
| Provider guards | Opaque values, fingerprint case/length, reference bounds, ancestor paths | Identical acceptance; exact path match is not ancestor conflict | Existing provider result categories |
| Hash encoding | Null/empty, NFC variants, ordering, scalar/count boundaries | Legacy UTF-8/LE32/null-as-empty stays separate from canonical NFC/BE32/presence markers | Same digest bytes, prefix checks, and exceptions |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders/Aggregates/Folder/`: ten services repeat `WithPayloadTenant` and `MapAuthorization`: `FolderCreationService.cs`, `FolderAccessMutationService.cs`, `RepositoryBackedFolderCreationService.cs`, `RepositoryBindingService.cs`, `BranchRefPolicyConfigurationService.cs`, `WorkspacePreparationService.cs`, `WorkspaceCommitService.cs`, `WorkspaceFileMutationService.cs`, `WorkspaceLockAcquisitionService.cs`, `WorkspaceLockReleaseService.cs`.
- `src/Hexalith.Folders/Aggregates/Organization/ConfigureProviderBindingService.cs`: same tenant merge; different authorization enum/policy.
- `src/Hexalith.Folders/Aggregates/Folder/FolderStreamName.cs` and `src/Hexalith.Folders/Aggregates/Organization/OrganizationStreamName.cs`: identical segment predicate; different surrounding policy.
- `src/Hexalith.Folders/Providers/{GitHub,Forgejo}/`: each `*Provider.cs` and `*Provider.Operations.cs` repeats opaque-value/fingerprint/reference checks and `HasAncestorConflict`. Keep replay, allowlists, repository IDs, retry normalization, and Git-path validation local.
- Same provider directories: `*CanonicalEvidenceWriter.cs`, `*ProviderSafeOperationEvidence.cs`, `*SafeTargetFingerprint.cs`. Preserve provider façades; writer wrappers may retain differing argument-exception construction.
- `src/Hexalith.Folders/Aggregates/Folder/FolderCommandValidator.cs` and `src/Hexalith.Folders/Providers/Abstractions/ProviderCapabilityProfileFactory.cs`: legacy `AppendField`/`AppendInt32` match provider fingerprint encoders. Leave other hashing schemes untouched.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.Tests/Helpers/HelperCompatibilityTests.cs` — pin original caller outputs, then cover matrix cases; keep GitHub vectors and add Forgejo vectors in `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoCanonicalEvidenceTests.cs`.
- [x] `src/Hexalith.Folders/Authorization/ClientTenantValues.cs` and `src/Hexalith.Folders/Aggregates/Folder/FolderAuthorizationResultMapper.cs` — extract and rewire the eleven tenant-merge and ten Folder-map callers listed above; delete their private copies.
- [x] `src/Hexalith.Folders/Aggregates/StreamNameSegment.cs` — share only segment validation; delegate both stream records without changing reserved checks or identifier exclusions.
- [x] `src/Hexalith.Folders/Providers/Shared/ProviderAdapterGuards.cs` — centralize the four guards and rewire both providers' main/operation partials.
- [x] `src/Hexalith.Folders/Providers/Shared/CanonicalEvidenceWriter.cs` and `ProviderEvidenceHash.cs` — share canonical encoding, SHA-256 calculation, and fixed-time comparison; retain provider domain validation and façade semantics.
- [x] `src/Hexalith.Folders/Hashing/LengthPrefixedHash.cs` — share legacy LE32/UTF-8 appending across the four named fingerprint/validator callers; preserve field order.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — update only the resolved 11.5 key as workflow milestones are reached; record validation in this spec.


**Acceptance Criteria:**
- Given the identified duplicate families, when extraction completes, then callers delegate to one implementation per shared concern and the replaced bodies are removed.
- Given compatibility vectors and domain/provider suites, when verified, then hashes, failure categories, authorization ordering, replay, and metadata-only guarantees remain equivalent.
- Given provider-specific policies and reserved-name differences, when boundary regressions run, then their existing distinctions remain observable without contract, package, or feature changes.

## Implementation Notes

- Extracted the eleven ordinal tenant-merge callers into `ClientTenantValues` and the ten Folder authorization maps into `FolderAuthorizationResultMapper`. Organization authorization retains its original private map and distinct ACL/Dapr policy.
- Both stream records delegate only their segment grammar and 256-character limit to `StreamNameSegment`; reserved-tenant checks, identifier exclusions, result codes, and exception parameter attribution remain local and unchanged.
- Both providers' main and operation partials call `ProviderAdapterGuards` for opaque metadata, lowercase fingerprints, bounded references, and proper-ancestor conflicts. Replay, repository identity, retry normalization, allowlists, Git-path validation, and orchestration remain local.
- Provider writer and evidence façades retain their existing signatures and provider-specific domain checks. `CanonicalEvidenceWriter` owns NFC/UTF-8, presence markers, and BE32 canonical encoding; `ProviderEvidenceHash` owns SHA-256 and fixed-time decoded comparison. Writer disposal remains a no-op, and GitHub/Forgejo negative-count exception differences and `Create` argument-validation differences remain observable.
- `LengthPrefixedHash` owns the separate legacy UTF-8/LE32/null-as-empty encoding used by the four specified fingerprint callers. Field ordering and caller normalization are unchanged.
- Added compatibility tests and Forgejo canonical vectors; existing GitHub vectors are unchanged. Before extraction, 69 compatibility tests passed against the original caller implementations. Added scalar/count vectors were also verified against the original writer sources from baseline commit `591194c7766c72d8bd51398d66f814a6a1fca922` in a temporary standalone .NET probe. Their GitHub/Forgejo digests are `1f8c8e3a71c7b12b9160ad903d25bab3ccd6e6c54d6d586f1bc2abdaca2f5014` and `129719af6c4194e4a7c0f82703eb1715e3f0413710166c865ed7b78acd108fac`.
- No contracts, generated artifacts, package/dependency configuration, persistence, submodule pins, or reserved-tenant policy were changed. Pre-existing edits to `epic-11-context.md` and sprint tracking were preserved; the parent workflow owns the 11.5 lifecycle transition.

## Spec Change Log

## Review Triage Log

- Independent layers: Edge Case Hunter returned no findings; Verification Gap Reviewer found no gaps. Blind Hunter supplied ten suggestions, individually triaged below.
- B1 — low, patch: the disposal regression assertion checks only digest length, so ignored writes would pass. Replace it with the expected digest for the appended fields.
- B2 — low, patch: separate invalid-argument cases do not pin ordering when multiple arguments are invalid. Add combined invalid-domain/null-fields and invalid-domain/null-callback cases for both façades.
- B3 — low, patch: negative-count assertions omit parameter attribution and unchanged hash state. Add those direct assertions while preserving the existing provider-specific ActualValue expectations.
- B4 — low, rejected: creation/binding overloads lack additional fixed vectors, but their input sequences and ordering are unchanged, the shared byte encoder has pinned vectors, and existing provider suites cover these operations. Expanding request fixtures is beyond a direct assertion correction and does not address an observed regression.
- B5 — low, rejected: the new profile vector intentionally isolates shared encoding; existing ProviderCapabilityProfileTests cover operation ordering and values. The changed field-call sequence matches baseline exactly. A larger mixed-operation golden fixture would add scenario maintenance without an observed everyday defect.
- B6 — low, patch: the existing reordered-metadata fixture can directly verify Forgejo ordering as it does GitHub. Add that assertion without creating new fixtures.
- B7 — low, patch: the merge test checks result comparison but cannot represent simultaneous case-distinct source keys. Add an ordinal source and assert that only the exact payload key changes.
- B8 — low, patch: malformed-surrogate normalization exceptions are not directly pinned through the extracted writer façades. Add direct exception assertions for both providers.
- B9 — low, rejected: the Organization service's authorization calls and mapper are unchanged, and the distinct mapping is pinned directly. Existing tenant-gate tests cover denied side effects. Building another outer-service authorization fixture is beyond a direct correction; no changed call bypasses the mapper.
- B10 — low, rejected: the spec abbreviates focused/format argument lists; complete local logs exist under /tmp/story-11-5-*. The proposed correction edits this build's spec, which the review workflow explicitly excludes from fixes. This does not affect the executed build/test evidence.
- Patch completion: B1, B2, B3, B6, B7, and B8 were corrected in HelperCompatibilityTests only. The affected class passed all 32 tests. No production changes were required by review; the parent then repeated the source-reference build, full core suite, and changed-file format verification successfully.

## Verification

- Before code edits, capture the required Aspire baseline using the orchestration skill; record exact blockers separately.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug` — source-reference build succeeds. Run the built xUnit assembly directly with `-class` for new helper suites, stream-shape suites, aggregate validation/idempotency/service suites, GitHubCanonicalEvidenceTests, GitHubProviderTests, ForgejoProviderTests, ForgejoProviderOperationTests, ProviderCallCeilingConformanceTests, and provider capability/dependency guards. Then run that test project fully; existing opt-in skips stay explicit.
- Run `dotnet format whitespace` against changed C# paths with `--verify-no-changes --include`; run `git diff --check`. Both must be clean.
- Inspect the diff: contract/generated artifacts, dependency pins, unrelated sprint keys, and reserved-tenant policy are unchanged.

### Execution evidence — 2026-10-23

- Required pre-edit Aspire baseline: `aspire ps --format Json --non-interactive` showed two unrelated AppHosts and no Folders AppHost. `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exited 2 during its build with `CS1704` (duplicate `Hexalith.Commons.UniqueIds`, source assembly 1.0.0 versus package 2.30.3), plus an OpenSSL certificate-trust message. `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json --non-interactive` confirmed that no Folders AppHost was running. This is an explicit runtime-baseline blocker; no application resource state could be inspected and no Folders AppHost required stopping.
- Initial unpinned test-project Debug build reproduced the same `CS1704` duplicate-assembly blocker. The environment-only correction is to propagate the existing source-reference mode explicitly with `-p:UseNuGetDeps=false`; no dependency or build-configuration files were edited.
- Final build: `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug -p:UseNuGetDeps=false -m:1` — passed, 0 warnings and 0 errors.
- Focused run: the built assembly was invoked directly with `-class` for `HelperCompatibilityTests`, every Folder/Organization aggregate suite, `GitHubCanonicalEvidenceTests`, `GitHubProviderTests`, `GitHubDependencyGuardTests`, `ForgejoCanonicalEvidenceTests`, `ForgejoProviderTests`, `ForgejoProviderOperationTests`, `ForgejoDependencyGuardTests`, every provider abstraction/capability suite, and `ProviderCallCeilingConformanceTests` — 1,381 tests passed, 0 failures, 0 skips.
- Full project: `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll` — 2,302 total, 2,300 passed, 0 failures/errors, 2 expected opt-in skips: `ForgejoLiveEvidenceTests.ProductionProviderEmitsMetadataOnlyEvidence` (operator lane) and `ForgejoSmartHttpGitTransportIntegrationTests.AlpineTlsProfileReceivesExactPackAndRejectsPostAdvertisementStaleOld` (isolated fixture environment not configured).
- `dotnet format whitespace tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --no-restore --verify-no-changes --include` with all 34 changed/new C# paths — passed; workspace loading emitted a warning without formatting failures. `git diff --check` — passed.
- Diff inspection confirmed that only the specified helper/caller/test families and this spec were edited by implementation. The existing user changes to context/sprint files remain preserved. Original Organization mapping and both reserved-tenant policies are unchanged.
- Parent acceptance audit: all six matrix rows have executed, passing coverage in HelperCompatibilityTests and the two provider canonical-evidence suites. All eleven tenant-merge and ten Folder authorization copies are removed. A baseline-to-current comparison confirms the exact field-call sequences in all four legacy hashing callers (131, 25, 43, and 49 calls respectively), and protected contracts, fixtures, generated clients, build/dependency files, and submodule pins are unchanged.
- Final post-review verification: `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug -p:UseNuGetDeps=false -m:1` passed with 0 warnings/errors. `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll` passed: 2,302 total, 2,300 passed, 0 failures/errors/not-run, and the same two optional Forgejo skips. Whitespace verification passed for all 34 changed/new C# files, and `git diff --check` passed. Local final logs are `/tmp/story-11-5-final-build.log`, `/tmp/story-11-5-final-full.log`, and `/tmp/story-11-5-final-format.log`; the complete format invocation is `/tmp/story-11-5-final-format-command.txt`.
