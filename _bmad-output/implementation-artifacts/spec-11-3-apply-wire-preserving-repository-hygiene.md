---
title: '11.3 apply wire-preserving repository hygiene'
type: 'chore'
created: '2026-09-08'
status: 'done'
baseline_commit: 'f0dfa73de9937ad23457cbf5889354987eea2f27'
route: 'dispatch'
review_loop_iteration: 0
story_key: '11-3-apply-wire-preserving-repository-hygiene'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-11-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Tracked caches, leftover review/backup files, and stale maintainer text (E2E as a skipped Epic 6 placeholder; CLI/MCP versions behind Builds pins) will pollute later Epic 11 work.

**Approach:** Remove only that litter, copy current Builds pin numbers into the SDK refs, and correct the two test READMEs to the live 63-test blocking E2E lane. No wire, gate, submodule, or product change.

## Decisions

- Keep `fable_Folders_changes.md` and `_bmad-output/**`. Do not rewrite `docs/ux/**` or planning dumps.
- §10.2 pins and new gates stay in 11.16. Dead types stay later. Aspire is already `13.5.3`.
- E2E README: fix status/CI/layout from `docs/operations/e2e-ci-gates.md`; keep the selector contract; drop "do not create `Routes/`".
- SDK versions: copy Builds pins; lockstep the one conformance string; do not redesign the gate.
- Leave dirty 10.8 work and submodule gitlinks untouched except the `11-3-*` key.

## Boundaries & Constraints

**Always:** Touch only the listed hygiene set. Source versions from Builds `Directory.Packages.props`. Preserve evidence, generated clients, OpenAPI, parity fixtures, and blocking `e2e-gates` + `accessibility-gates`.

**Never:** Change REST/OpenAPI/parity, other lifecycle keys, CI semantics, or submodule SHAs. Do not revert dirty 10.8/submodule work, init nested submodules, or implement 11.4–11.7 / 11.16.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Caches | 22 tracked `*.csproj.lscache` | `git rm --cached`; keep `*.lscache` ignore | Local copies may return |
| Litter | `_tmp_review_1_11_followup.diff`; `_bmad/*.bak` | Delete; ignore `_tmp_*` and `_bmad/*.bak` | No repo-wide `*.diff` rule |
| E2E text | READMEs say skipped / not in CI | 63 tests; blocking `e2e-gates` + `accessibility-gates` | Keep valid contract sections |
| Pin text | CLI `2.0.8`; MCP `1.3.0` | CLI `2.0.11`; MCP `2.2.0`; lockstep one test | Do not invent versions |
| Other work | Evidence; dirty 10.8/submodules | Unchanged | Halt rather than revert |

</frozen-after-approval>

## Code Map

- 22 tracked `*.csproj.lscache` — untrack only (`git ls-files '*.lscache'`).
- `_tmp_review_1_11_followup.diff`, `_bmad/config.toml.bak`, `_bmad/config.user.toml.bak` — delete.
- `.gitignore` — `*.lscache` exists; add `_tmp_*` and `_bmad/*.bak`.
- `tests/Hexalith.Folders.UI.E2E.Tests/README.md`, `tests/README.md` — stale Epic-6 placeholder/CI/layout; `Routes/ConsoleRoutes.cs` exists.
- `docs/operations/e2e-ci-gates.md` — README source of truth; do not edit.
- `docs/sdk/cli-reference.md`, `docs/sdk/mcp-reference.md` — `2.0.8`/`1.3.0` → pins `2.0.11`/`2.2.0`.
- `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` — lockstep CLI version string only.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `11-3-*` to `in-progress` only.

## Tasks & Acceptance

**Execution:**
- [x] 22 tracked `*.csproj.lscache` — `git rm --cached` every `git ls-files '*.lscache'` path.
- [x] `_tmp_review_1_11_followup.diff`, `_bmad/config.toml.bak`, `_bmad/config.user.toml.bak` — delete from index and tree.
- [x] `.gitignore` — add `_tmp_*` and `_bmad/*.bak`.
- [x] `tests/Hexalith.Folders.UI.E2E.Tests/README.md` — replace placeholder/CI/layout claims; keep contract; mention `Routes/ConsoleRoutes.cs`.
- [x] `tests/README.md` — replace the Epic-6 E2E sentences only.
- [x] `docs/sdk/cli-reference.md`, `docs/sdk/mcp-reference.md` — `2.0.8`→`2.0.11`; `1.3.0`→`2.2.0`.
- [x] `tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs` — lockstep the CLI version string.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` — flip `11-3-*` to `in-progress` only.

**Acceptance Criteria:**
- Given the 22 `*.lscache` files and three litter paths are tracked, when hygiene is applied, then `git ls-files '*.lscache' '_tmp*' '_bmad/*.bak'` is empty and the new ignore rules exist.
- Given the two test READMEs describe a skipped Epic 6 placeholder, when they are corrected, then they describe the live 63-test lane and the two blocking CI jobs.
- Given CLI/MCP docs name stale versions, when they are corrected, then versions match Builds pins and `ConsumerDocsConformanceTests` stays green with only that string change.
- Given evidence, workflows, OpenAPI, parity fixtures, and the dirty 10.8/submodule work exist, when this story completes, then they are unchanged except this spec and the `11-3-*` key.

### Review Findings (code review round 2 — 2026-09-12)

- [x] [Review][Patch] Align the three stale version comments in `src/` with the Builds pins (resolved 2026-09-12: Jerome chose to patch now, accepting the departure from the frozen Code Map) [src/Hexalith.Folders.Cli/Commands/Commit/CommitCommand.cs:26; src/Hexalith.Folders.Mcp/Resources/AuditTrailResource.cs:20; src/Hexalith.Folders.Mcp/Resources/FolderTreeResource.cs:21] — `src/Hexalith.Folders.Cli/Commands/Commit/CommitCommand.cs:26` still says "the System.CommandLine 2.0.8 token table" while `docs/sdk/cli-reference.md:127` now says 2.0.11; `src/Hexalith.Folders.Mcp/Resources/AuditTrailResource.cs:20` still says "Uses the ModelContextProtocol 1.3.0 attribute resource surface (verified against the pinned package)" and `src/Hexalith.Folders.Mcp/Resources/FolderTreeResource.cs:21` says 1.3.0, while the Builds pin is 2.2.0 and `docs/sdk/mcp-reference.md:5` now says 2.2.0. The AuditTrailResource parenthetical is now factually false. The frozen Code Map lists only `docs/sdk/**`, so extending the sweep into `src/` is a scope call, not a mechanical fix.
- [x] [Review][Patch] `_bmad/*.bak` misses the subdirectory where BMAD customization files actually live [.gitignore:438]
- [x] [Review][Patch] E2E README rewrite deleted still-valid contract rules beyond the authorized drop [tests/Hexalith.Folders.UI.E2E.Tests/README.md:22,95]
- [x] [Review][Patch] deferred-work entry breaks the ledger's path and attribution conventions [_bmad-output/implementation-artifacts/deferred-work.md:2867]
- [x] [Review][Patch] "CI-equivalent local gate" block is not CI-equivalent and contradicts itself [tests/Hexalith.Folders.UI.E2E.Tests/README.md:83]
- [x] [Review][Patch] Stale future-tense E2E claim survives in a rewritten file [tests/README.md:145]
- [x] [Review][Patch] `# last_updated:` journal comment skips this story's lifecycle transition [_bmad-output/implementation-artifacts/sprint-status.yaml:2]
- [x] [Review][Defer] Doc/pin and doc/count literals have no derivation or lockstep guard [tests/Hexalith.Folders.Contracts.Tests/Deployment/ConsumerDocsConformanceTests.cs:417] — deferred: Story 11.16 owns replacing brittle pins with behavioural gates; frozen intent here forbids redesigning the gate. `docs/sdk/mcp-reference.md`'s `2.2.0` is asserted nowhere; only `cli-reference.md:127` is pinned, not `:18`; the 63/23/28/8/4 counts are now hardcoded in three documents with no gate.
- [x] [Review][Defer] No conformance guard stops untracked litter from re-accreting [.gitignore:434] — deferred: `*.lscache` was already ignored at `.gitignore:434` and 22 files were tracked anyway (ignore rules do not apply to already-tracked paths), so the AC's one-shot `git ls-files` check is the mechanism already shown to fail. A tracked-inventory assertion in `ScaffoldContractTests` is new gate work owned by Story 11.16.
- [x] [Review][Defer] `project-context.md` still orders agents to keep skipped E2E placeholders [_bmad-output/project-context.md:99] — deferred: agent-context file, and the frozen Decisions explicitly keep that surface. Its bullet ("UI E2E is a deferred Playwright-on-.NET lane; keep skipped placeholders until Epic 6 story 6-2") now directly contradicts both rewritten READMEs.

#### Rejected

- `medium` — Three-way lifecycle inconsistency: spec frontmatter `status: 'done'`, sprint key `review`, and the Code Map / Execution task / Implementation Notes all say `in-progress` — the Notes further claim the key "already had `11-3-…: in-progress`" when the pre-image was `backlog`. Real and confirmed by all four layers, but every available fix edits this spec.
- `medium` — Verification lists `dotnet test … --filter FullyQualifiedName~ConsumerDocsConformanceTests`, which DW-341 blocks; the direct-runner fallback actually executed (22 passed / 0 failed) is recorded only in Implementation Notes. Fix edits this spec.
- `low` — Three Review Triage Log entries logged as open `low` are already fixed by this same diff (`tests/README.md:138` and `:80` rewritten, the `Fixtures/` tree now lists all seven files), and `## Spec Change Log` ships as a bare heading. Fix edits this spec.
- `false` — "The deferred-work entry gets no `DW-NNN` id." The `source_spec:` entry format carries no ids at all: lines 2796–2875 contain zero `DW-` entry ids, and the sibling 10.7/10.8/11.4 entries have none either. The entry follows its section's convention.
- `low` — `deferred-work.md` is edited although it appears in no Code Map entry, against AC4's "unchanged except this spec and the `11-3-*` key". The wording breach is real, but the entry is a required review-loop artifact; the only non-spec "fix" is deleting evidence.
- `low` — Doc version claims re-dated without re-proof (the `commit create` parser collision re-attributed to 2.0.11; MCP behavioural claims carried across a two-major jump). `tests/Hexalith.Folders.Cli.Tests/CommandSurfaceE2ETests.cs` exercises the `commit create` surface and the MCP resource tests exercise the attribute surface against the pinned packages; only the rationale prose is unproven.
- `false` — "`_tmp_*` should be anchored to `/_tmp_*`." Unanchored is the safer shape for a litter class, nothing in the tree matches either form, and the frozen I/O matrix names `_tmp_*` exactly.

## Implementation Notes

- `sprint-status.yaml` already had `11-3-apply-wire-preserving-repository-hygiene: in-progress`; no other lifecycle keys were touched.
- Spec `dotnet test --filter` is blocked by DW-341 (Microsoft.Testing.Platform rejects the VSTest target on .NET 10). Fallback: `dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --configuration Debug` then `./tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests -noLogo -noColor -class Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests` — 22 passed, 0 failed.
- Pre-existing dirty `references/Hexalith.Builds` and `references/Hexalith.FrontComposer` gitlinks were left untouched.

## Spec Change Log

## Review Triage Log

- `false` — Blind: patch moves Builds/FrontComposer SHAs. Those gitlinks were dirty before baseline (`071ef99`→`35c3d1e`, `9f09416`→`774e3d4`); this story never wrote `references/`. Last committed touch is `ee80dd0`.
- `false` — Blind: hygiene rewrote `epic-11-context.md`. Step-01 compiled it because planning artifacts were newer; the implementer did not edit it. Content is the current 21-story workstream.
- `false` — Blind: notes vs `backlog`→`in-progress` / stale `last_updated`. The YAML field is `2026-09-08`; the 2026-07-14 block is a comment. The status flip is the intended 11-3-only edit.
- `false` — Blind: Verification still lists blocked `dotnet test --filter`. Fix would edit this spec. Fallback is already in Implementation Notes (DW-341).
- `false` — Blind/edge: MCP `2.2.0` is not asserted in `ConsumerDocsConformanceTests`. Frozen intent locksteps the one CLI string and forbids redesigning the gate. `docs/sdk/mcp-reference.md:5` already has `2.2.0`.
- `low` — Blind: `tests/README.md:138` still points at “when-to-enable rules” after that E2E section was removed. Readers of the file this story edited hit a dead heading.
- `low` — Blind: `tests/README.md:80` still reserves the lane “when stable UI routes exist” in a file this story edited.
- `false` — Blind: `project-context.md` / `docs/ux/**` still have old E2E claims. Frozen intent keeps those surfaces.
- `low` — Blind: rewritten E2E `Fixtures/` tree lists only Playwright collection/fixture and omits the live host fixtures on disk.
- `false` — Blind: hardcoded 63/23/28/8/4 counts. Frozen intent copies those numbers from `docs/operations/e2e-ci-gates.md`.
- `false` — Blind: unrooted `_tmp_*` / `_bmad/*.bak`. Frozen matrix asked for those exact patterns; they cover the identified litter class.
- `false` — Blind: spec `type: chore` vs commitlint. Spec type is not a commit subject; fixing it edits this spec.
- `false` — Blind: empty change/triage logs while `in-review`. This step writes them.
- `false` — Blind: compiled context still says 11.3–11.7 reduce duplication. That band is the hygiene-then-dedup phase; 11.4 starts helper dedup.
- `false` — Blind: `planning-story-manifest.yaml` still `backlog`. Frozen intent does not rewrite planning dumps.
- `false` — Blind: `fable_Folders_changes.md` still describes the old READMEs. Frozen intent keeps that audit seed.
- `false` — Edge: CLI test would miss a leftover `2.0.8`. Both `docs/sdk/cli-reference.md` sites are `2.0.11`; adding `ShouldNotContain` redesigns the gate.
- `false` — Edge: FrontComposer SHA change needs a host boot check. This story did not edit that gitlink.
- `false` — Edge: gitlinks/epic-context violate the “unchanged except spec + 11-3 key” claim as implemented hygiene. Same pre-existing / step-01 facts as above.
- `medium` — VG: Builds gitlink moves EventStore `3.102.0`→`3.103.0` with no PR package-mode compile of `Hexalith.Folders.EventStore`. Pre-verified; filed as defer. Pre-existing dirty gitlink, not authored here.

## Verification

**Commands:**
- `git ls-files '*.lscache' '_tmp*' '_bmad/*.bak'` -- expected: empty.
- `rg -n "skipped placeholder|deferred until Epic 6|CommandLine 2.0.8|Protocol 1.3.0" tests/README.md tests/Hexalith.Folders.UI.E2E.Tests/README.md docs/sdk` -- expected: no matches.
- `dotnet test tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj --filter FullyQualifiedName~ConsumerDocsConformanceTests` -- expected: green.
- `git diff -- .github/workflows src/Hexalith.Folders.Contracts/openapi tests/fixtures references` -- expected: no edits.
