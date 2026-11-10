---
title: '5.12 Verify existing McpCli contract declarations'
type: 'chore'
created: '2026-11-10'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-5-12-declare-folders-operations-in-decorated-mcpcli-contracts.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The user invoked `bmad-build 5.12` after the declaration implementation was committed and its spec marked done. Confirm whether implementation work remains using the existing acceptance checks.

**Approach:** Verify the committed implementation against the completed spec, rerun the focused McpCli contract checks and the Contracts test project, and record the results. Preserve the approved operation eligibility, behavior-loss decisions, downstream admission/query ownership and PD10 reseal deferral. Keep the existing implementation and sprint review handoff when no regression is found.

</frozen-after-approval>

## Implementation Notes

Verified on 2026-11-10 against implementation revision `b6338623081310d2fa7e4db1783db71ebb891725`. The initial working tree was clean on `main`; no implementation regression was found. The only new file from this run is this verification record.

The completed implementation declares 13 Commands and 28 Queries. The focused tests compare all 48 inventory candidates with the owner's decisions, verify the exact eligible/decorated set, and cover routing, wire names, identifier round-trips, serialized fields, spine drift and the packed package closure. They do not establish live Gateway admission or query readiness, which remain Stories 5.13 and 5.14.

Verification commands and results:

- `dotnet build tests/Hexalith.Folders.Contracts.Tests --configuration Release -warnaserror`: exit 0, zero warnings and errors, no MCPCLI001. This rebuilds the referenced Contracts project.
- `tests/Hexalith.Folders.Contracts.Tests/bin/Release/net10.0/Hexalith.Folders.Contracts.Tests -namespace Hexalith.Folders.Contracts.Tests.McpCli`: exit 0, 30 passed, none failed, skipped or unrun. This includes a real Release pack and checks the DLL inside the package and its dependency closure.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests --configuration Release --no-build`: exit 2, 661 passed, 5 failed, 0 skipped out of 666. All failures match the completed spec's pre-existing PD10 failure set, listed below.
- `dotnet format whitespace src/Hexalith.Folders.Contracts --verify-no-changes`: exit 0.
- `git diff --check`: exit 0 for the existing tracked content; this excludes the new untracked record. The record is checked separately with `git diff --no-index --check /dev/null _bmad-output/implementation-artifacts/spec-5-12-verify-existing-mcpcli-contract-declarations.md`. The no-index check returned 1 (file differences), with no whitespace diagnostics; `git diff --cached --check` returned 0 and covered the complete staged record. A read-only check also confirmed the PD10 story-owned path list remains sorted and unique.

Session logs, retained under `/tmp` for local inspection and not required to reproduce these checks:

- Build: `/tmp/folders-5-12-reverify-build.log`
- Focused tests: `/tmp/folders-5-12-reverify-mcpcli.log`
- Full Contracts project: `/tmp/folders-5-12-reverify-contracts.log`
- Whitespace format: `/tmp/folders-5-12-reverify-format.log`

The five failing tests, all under `Hexalith.Folders.Contracts.Tests.OpenApi`, are:

1. `Pd10V2ContractTests.ConformanceSetBindsEveryCandidateArtifactWithoutChangingReleaseAuthority`
2. `Pd10ConformanceSetTests.GeneratorRejectsOutputThatAliasesARequiredCandidateInput`
3. `Pd10ConformanceSetTests.InventoryCoversEveryRuntimeAndTestCompileInputIncludingExtractedHelpers`
4. `Pd10ConformanceSetTests.ManifestBindsEveryListedCandidateArtifactAndOrderedSetDigest`
5. `Pd10ConformanceSetTests.GeneratorReproducesTheCommittedManifestByteForByte`

The candidate inventory omits `tests/Hexalith.Folders.Server.Tests/OwnerSourceCompositionAssertionIssuer.cs`, and the committed manifest has stale rows. Decision D4 in the completed spec leaves resealing with Stories 12.2/1.17. This run did not change the inventory, regenerate the manifest, or modify dependencies or submodules.

The sprint row `5-12-declare-folders-operations-in-decorated-mcpcli-contracts` remains `review`: this is the workflow's expected handoff for a done build spec. The status-sync rule does not regress a row already at the target status or later. No solution build or live topology run was repeated because no code or configuration changed; the completed implementation spec retains its earlier solution-build evidence.

## Review Triage Log

| Finding | Verdict | Evidence and resolution |
| --- | --- | --- |
| Tracked diff whitespace check excludes the untracked record | low, patch | Confirmed the record was untracked during the original check. Check it explicitly with `git diff --no-index --check` and check the staged diff before committing. |
| Verification commands omit session log paths | low, patch | Logs exist at the four paths above and match the recorded results. Added the paths, identifying their temporary, local scope. |
| Description calls an untracked file tracked content | low, patch | Confirmed the record was the sole untracked addition. Changed the wording to “only new file.” |

All three findings were resolved in the verification record. No new work was deferred. The original implementation review remains recorded in the completed Story 5.12 spec.
