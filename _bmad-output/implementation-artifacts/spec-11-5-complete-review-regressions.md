---
title: '11.5 Complete approved review regression coverage'
type: 'chore'
created: '2026-10-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
story_key: '11-5-consolidate-domain-and-provider-helper-duplication'
baseline_commit: '5b6b5dee22d21ba93f747483c2fa0fd12d72ae7c'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-11-5-consolidate-domain-and-provider-helper-duplication.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The completed 11.5 consolidation has ten approved but unchecked chunk-3 review patches. Missing regression vectors leave persisted identity and provider fingerprint compatibility insufficiently protected for subsequent adoption stories.

**Approach:** Complete those test-only patches. Compute one golden vector per legacy fingerprint method shape from original source revision `591194c7766c72d8bd51398d66f814a6a1fca922`; cover populated profile encoding and every provider target overload. Correct the Forgejo domain list, pin malformed-Unicode and validation boundaries, and restore the Abstractions dependency guard's coverage of the moved encoder. Preserve production behavior, all earlier review deferrals, dependency pins, contracts, and approval evidence. Record completion in the original spec and move only the resolved sprint story to review.

</frozen-after-approval>

## Implementation Notes

- Re-entry investigation found the original spec marked done, ten unchecked approved review patches, and the sprint key already in progress. The recorded patch decision supplies scope; no new product decision or irreversible action is required.
- The change is confined to core unit tests and story tracking. Separate single-type test files may hold the two large vector families to keep the existing compatibility class readable.
- Validate with the Debug source-reference build, focused classes and the full core test assembly, changed-file whitespace checks, and dependency-guard counterexamples. Capture the required Aspire baseline separately from unit results. Obtain expected hashes from a temporary standalone probe using original source files, never current implementations.
- Added `Helpers/LegacyFingerprintCompatibilityTests.cs` with 16 domain method shapes and a populated capability-profile vector, and `Helpers/ProviderTargetFingerprintCompatibilityTests.cs` with five overloads per provider. Original source probes executed at `591194c7766c72d8bd51398d66f814a6a1fca922`; the parent verified the snapshots byte-for-byte against Git and verified that the two legacy probe classes differ only by their type names. Probe sources and outputs remain under `/tmp/story-11-5-legacy-baseline-probe/` and `/tmp/story-11-5-provider-target-baseline-probe/`.
- Extended `HelperCompatibilityTests` with both complete authorization outcome tables, opaque stream boundaries, Unicode-digit and sibling-path negatives, malformed-Unicode replacement, both-null/actual-null comparisons, domain guards and null writer hashes. Existing discovery cases now preserve failure reasons.
- Corrected Forgejo domain vectors to the 19 live source purposes. Extended the Abstractions dependency guard to `Hashing`, with file-path diagnostics; each of the nine missing boundary terms was individually rejected through temporary negative controls, removed afterward.
- Pre-edit runtime baseline: `aspire ps --format Json --non-interactive` returned `[]`. `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exited 2 with `CS1704`: duplicate `Hexalith.Commons.UniqueIds` assemblies (source 3.126.1 versus package 2.30.3). `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json --non-interactive` confirmed no Folders AppHost was running. Log: `/tmp/story-11-5-review-aspire.log`. This reproduces the existing runtime blocker; source-reference unit verification remains independent.
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Debug -p:UseNuGetDeps=false -m:1` passed with 0 warnings and 0 errors. The built assembly passed 113 focused cases across the five changed/new classes. Changed-file `dotnet format whitespace --no-restore --verify-no-changes --include` passed; workspace loading reported a warning, without formatting failures. `git diff --check` passed.
- `dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll` passed: 2,357 total, 2,355 passed, 0 failures/errors/not-run, and the two existing optional Forgejo skips (operator live evidence and the unconfigured isolated Alpine TLS fixture). Logs: `/tmp/story-11-5-review-focused.log`, `/tmp/story-11-5-review-full.log`, `/tmp/story-11-5-review-format.log`, and `/tmp/story-11-5-review-boundary-counterexamples.log`.

## Review Triage Log

- One-shot route: Blind Hunter completed independently and also ran all 113 focused cases successfully. Edge Case Hunter, Verification Gap Reviewer and Acceptance Auditor were skipped for this test-only follow-up.
- R1 — low, patch: equal allowed/protected pattern counts could hide substituting one count for the other. Use unequal collection sizes and recompute the original-source vector.
- R2 — low, patch: equal normalized provider family, key and profile product could hide substituting fields. Give them distinct values in the existing fixtures, validate acceptance through original sources, and recompute their expected hashes.
- R3 — low, patch: retry interval 1.5 cannot distinguish midpoint-to-even from midpoint-away rounding. Change the existing interval to 2.5 and verify it through the original source; both original F0 inputs encode 2, so the expected rounding field remains stable.
- R4 — low, patch: lowercase alpha/zeta keys do not distinguish ordinal from linguistic ordering. Use uppercase Zeta with lowercase alpha in raw metadata, which both providers permit; recompute the affected original-source vectors. The profile sorts raw keys before normalizing them, so this also distinguishes its ordinal ordering.
- R5 — low, rejected: the single populated branch-policy vector does not pin separate null and empty protected-pattern representations. The code is unchanged; the approved decision requests one vector per method shape. Adding multiple branch fixtures and expected hashes expands that requirement beyond a direct correction. Signed/count byte encoding already has boundary coverage.
- R6 — low, rejected: the populated change-file vector does not pin absent optional values or add/remove branches. Those unchanged branches are outside the approved one-vector-per-method requirement; the selected change case covers every optional appended field. Additional scenario fixtures and expected hashes are further test expansion, beyond a direct correction.

## Final Verification

- After the four review corrections, the same Debug source-reference build again passed with 0 warnings and 0 errors. The final full assembly run again passed 2,357 tests total: 2,355 passed, 2 configured skips, 0 failures/errors/not-run. Changed-file whitespace verification and `git diff --check` passed. Final logs: `/tmp/story-11-5-review-final-build.log`, `/tmp/story-11-5-review-final-full.log`, and `/tmp/story-11-5-review-final-format.log`.
- All ten original review action items are checked complete. The original frozen intent, production files, dependencies, contracts, generated artifacts, submodule pins and existing deferrals remain unchanged. Sprint story 11.5 advances to review. No new deferrals arose from this follow-up review.
