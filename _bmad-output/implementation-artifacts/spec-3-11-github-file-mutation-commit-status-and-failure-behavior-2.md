---
title: 'Story 3.11 follow-on: remaining GitHub adapter hygiene'
type: 'refactor'
created: '2026-09-06'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '781168db4d5020822b5923ac5c946a936638a8dc'
context:
  - '_bmad-output/project-context.md'
  - '_bmad-output/implementation-artifacts/epic-3-context.md'
  - '_bmad-output/implementation-artifacts/spec-3-11-github-file-mutation-commit-status-and-failure-behavior.md'
  - '_bmad-output/implementation-artifacts/spec-3-11-live-github-evidence-operator-action.md'
  - '_bmad-output/implementation-artifacts/deferred-work.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.11's GitHub mutation, commit, and status seam is already implemented and both existing specs are `done` (including the 2026-09-06 path-C live-archive waiver). A new build was requested anyway. Three open deferred items still cite Story 3.11 authority: DW-299 (four `ReplayOrReject` gates), DW-300 (`IsMalformedJsonException` comment, name-substring heuristic, missing `SerializationException` test), and DW-304 (digits-only SHA test constants that make `ToUpperInvariant()` a no-op).

**Approach:** Do not reopen the landed Git Data seam, the live-archive waiver, or orchestrator sprint-status. Close DW-299, DW-300, and DW-304 while preserving today's public admission, failure, and metadata-only behavior.

**Decision (2026-09-06):** Remaining work = hygiene bundle **DW-299 + DW-300 + DW-304**.

## Boundaries & Constraints

**Always:** Keep ordered Git Data staging, one non-force ref update, fail-closed source/outcome seams, and production `IGitProvider` → `GitHubProvider` selection unchanged. Keep results, logs, exceptions, and tests metadata-only. Put any new C# type in its own file. Leave `references/Hexalith.FrontComposer` and `references/Hexalith.Tenants` pointer dirt untouched. After DW-299, DW-300, and DW-304 land, mark those three DW entries resolved with evidence.

**Never:** Restore `_bmad-output/implementation-artifacts/review-3-11-intent-gap.patch`. Un-waive the live GitHub archive or add a GitHub live runner. Write or revert `_bmad-output/implementation-artifacts/sprint-status.yaml`. Change Forgejo execution (including DW-350), Contents API writes, generated clients, or Stories 12.3/12.4/4.20/4.21/3.12/3.14 ownership. Change create/bind replay field rules (canonical repository id on success) while collapsing gates. Self-approve OQ4 or claim a live mutation run.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Shared admission | Fresh / equivalent replay / conflict / expired, including malformed companion fields | Same typed mutation, commit, create, and bind results as today; zero extra target, credential, or GitHub calls | Malformed admission still rejects before source access |
| Malformed JSON | `System.Runtime.Serialization.SerializationException` vs unrelated `System.Text.Json` exception from our code | Octokit deserialize failures stay provider-malformed; our own JSON exceptions are not mapped as provider malformed-response | Observation vs post-dispatch mutation mapping stays distinct |
| SHA vacuity | Constants used with `ToUpperInvariant()` in non-canonical SHA tests | Uppercasing changes the value; the negative assertion still rejects | Digits-only constants remain only where uppercasing is not the assertion |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.Operations.cs:1443` and `:1452` -- Story 3.11 mutation/commit `ReplayOrReject`; keep typed result factories; candidate to share classification with create/bind.
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs:691` and `:753` -- Story 3.10 create/bind `ReplayOrReject`; preserve `PriorCanonicalRepositoryId` on create/bind success; do not weaken 3.11 companion-field rules.
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.Operations.cs:1574` (`IsOperationAdmissionWellFormed`) and `GitHubProvider.cs:616` (`IsReplayEvidenceWellFormed`) -- already share fingerprint/opaque helpers; DW-299 wants one classification helper so the four disposition switches cannot drift.
- `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.Operations.cs:949` -- existing `(ProviderFailureCategory, string)? ValidateBoundary` shape DW-299 cites as the helper pattern.
- `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs:1986` -- `IsMalformedJsonException`; still matches `SerializationException` plus `Name.Contains("Json")`; comment/heuristic/test are DW-300. Call sites `:129`, `:847`, `:1616`, `:1754`.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.Operations.cs:14-15` -- `TreeSha` and `CommitSha` are digits-only again; DW-298's `...cc` fix is not present here.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs:1112` -- digits-only `PriorOutcomeFingerprint`; DW-304 sibling hazard.
- `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs` and `GitHubProviderTests.Operations.cs` -- existing admission/replay/zero-touch rows that must stay green after the shared helper lands.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- DW-299, DW-300, DW-304 are in scope to resolve; DW-350 and live-archive residual stay out of scope.
- Do not change: `IGitProvider` mutation/commit/status signatures, `OctokitGitHubApiClient` Git Data write path, `FoldersServiceCollectionExtensions` GitHub singleton selection, `sprint-status.yaml`, Forgejo provider, live evidence runner.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs` -- correct the Octokit type comment, stop treating arbitrary `Json` type-name substrings from our code as provider malformed-response, and keep BCL `SerializationException` as the Octokit deserialize match -- closes DW-300.
- [ ] `tests/Hexalith.Folders.Tests/Providers/GitHub/OctokitGitHubApiClientTests.Operations.cs` -- add a `SerializationException` classification row and give SHA constants used with `ToUpperInvariant()` at least one `a-f` hex letter -- proves DW-300 mapping and DW-304 vacuity.
- [ ] `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs` -- same hex-letter rule for `PriorOutcomeFingerprint` and any sibling digits-only SHA used with uppercasing -- prevents the DW-298 no-op from returning.
- [ ] `src/Hexalith.Folders/Providers/GitHub/` -- extract one shared admission-classification helper used by all four `ReplayOrReject` methods without changing public results; new type in its own file -- closes DW-299.
- [ ] `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubProviderTests.cs` plus `GitHubProviderTests.Operations.cs` -- keep every existing fresh/replay/conflict/expired/malformed admission row green with the same recorder call counts -- proves the helper is behavior-preserving.
- [ ] `_bmad-output/implementation-artifacts/deferred-work.md` -- mark DW-299, DW-300, and DW-304 resolved with the implementing revision -- keeps the ledger honest.
- [ ] Always: leave `sprint-status.yaml` and the two dirty submodule pointers untouched.

**Acceptance Criteria:**
- Given the hygiene bundle, when the focused GitHub adapter tests run, then every previously green admission, replay, transport, and SHA-negative row still passes and no new provider call appears on replay/conflict/expired.
- Given Octokit surfaces `System.Runtime.Serialization.SerializationException`, when observation mapping runs, then it uses the malformed-response path; an unrelated `System.Text.Json` exception from test/production code is not classified as provider malformed-response.
- Given a non-canonical SHA test uppercases a SHA constant, when the negative assertion runs, then the uppercased value differs from the constant and the operation still rejects without GitHub observation or ref movement.
- Given the implementing diff, when it is inspected, then `sprint-status.yaml`, Forgejo execution, Git Data write ordering, and the live-archive waiver are unchanged.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

DW-299 is a classification extract, not a behavior merge. Create/bind success still carries `PriorCanonicalRepositoryId`; mutation/commit success must not start requiring it. A shared helper should return a disposition/reason classification (the existing `ValidateBoundary` tuple shape is the pattern); each `ReplayOrReject` overload still builds its own result type.

DW-300 must name the BCL `System.Runtime.Serialization.SerializationException`. Octokit 14.0.0 does not ship `Octokit.SerializationException`.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -c Release -m:1 -p:UseHexalithProjectReferences=false -p:MinVerVersionOverride=1.0.0 -p:NuGetAudit=false` -- expected: 0 warnings, 0 errors.
- `./tests/Hexalith.Folders.Tests/bin/Release/net10.0/Hexalith.Folders.Tests -noLogo -noColor -class Hexalith.Folders.Tests.Providers.GitHub.GitHubProviderTests -class Hexalith.Folders.Tests.Providers.GitHub.OctokitGitHubApiClientTests -class Hexalith.Folders.Tests.Providers.GitHub.GitHubDependencyGuardTests` -- expected: focused GitHub suite passes.
- `git diff --exit-code -- _bmad-output/implementation-artifacts/sprint-status.yaml` -- expected: no story-authored change.
- `git diff --check` -- expected: no whitespace errors in this spec's files.

**Manual checks (if no CLI):**
- DW-299, DW-300, and DW-304 in `deferred-work.md` show `status: resolved` with the implementing revision; other DW entries remain unchanged.
- `git status` still shows only the pre-existing FrontComposer and Tenants pointer dirt plus this story's files.
