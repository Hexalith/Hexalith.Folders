# Reviewer Gate — Technology / Currency & Reality-Check Lens (Pass-2 Re-Gate)

- **Artifact under review:** `_bmad-output/planning-artifacts/architecture.md` (1907 lines, pass-2 state, uncommitted; `git diff` = +115 / −65 vs `de281e7`)
- **Prior reviews:** `reviews/review-technology-2026-09-16.md` (FAIL, 6 H / 6 M / 3 L) → `reviews/review-technology-update-2026-09-16.md` (FAIL, 3 H / 6 M / 3 L, TECH-U1…U12)
- **Repo state:** branch `main`, HEAD `de281e7`
- **Date:** 2026-09-16

**Nothing here is asserted from training data.** Every currency claim is a live `api.nuget.org` flat-container fetch or the live `dotnet/core` releases index on 2026-09-16; every repo claim is a `file:line`. The brief warned that pass 1's fixes introduced false claims, so **every pass-2 assertion was independently re-derived from the repo rather than accepted** — including the brief's own framing, one clause of which turned out to be wrong (see TECH-R1).

---

## Verdict

**FAIL — narrowly, and for a different reason than last time.**

The engineering content of the pass-2 corrections is **good, and checks out in every particular I could verify**. TECH-U1's ownership statement, TECH-U3's two constraints and TECH-U8's `Configuration`-driven default are each verified true against the files they name — no hand-waving, no invented paths, no versions that do not exist. The downstream `oasdiff` propagation (TECH-U6) was done, correctly, in all three published documents, and a repo-wide grep now finds `oasdiff` in **zero** build, test, tooling, or `docs/` artifacts.

Two things keep the gate red:

1. **Pass 2 introduced a new provably-false claim in the very paragraph it was fixing.** A-7 (`:696`) states that three published documents "*still name it and are owed the same correction*" — and then names the three documents that **were corrected in the same working tree**. The sentence is refuted by `git diff` of its own commit. Worse, it displaces the debt that *is* still open: `epics.md:327` plus three further `_bmad-output` artifacts still say `oasdiff`, and — contrary to the brief — that debt is **not** recorded in the reconciliation note.
2. **The constraint-recording rule is no longer self-contradictory, but it is still not satisfied by the document's own text — now in both directions.** It under-includes (`Redis 7.x`, `System.CommandLine 2.x`, `xUnit v3` are recorded and fit none of the four stated categories) and over-excludes (the `Microsoft.OpenApi` 2.x ceiling and the `MessagePack` security floor are "compatibility floor or ceiling" *by the rule's own definition* and appear nowhere). Most concretely: `Octokit 14.0.0` survives four times with **no label saying it is approval-bound**, so the sweep hazard I raised in TECH-U2 — a future pass deleting a pin that two conformance tests and a digest-bound approved catalog depend on — is **unmitigated**.

**No stale numeric pin crept back in.** The only version literal added by pass 2 is `5.0.0-rc` at `:1746`, which is correct and is squarely inside the new rule's "prerelease" category.

**The repo-vs-world conclusion is re-confirmed for the third time:** repository currency is genuinely good. Every pin re-fetched on 2026-09-16 is at, or within one patch of, the current published version, and the Aspire SDK-vs-Hosting pair — the axis that broke Epic 9 — is **currently aligned at 13.5.3 on both sides**. The gap has been doc-vs-repo from the start and still is.

---

## Part A — Closure status of the five findings pass 2 targeted

### TECH-U1 — Aspire version authority — **SUBSTANTIALLY CLOSED** ✅ (residual: TECH-R3, MEDIUM)

The new statement at `:1746` was checked clause by clause. **Every clause is true:**

| Claim at `:1746` | Verification |
| --- | --- |
| entry point is the repo-root `Directory.Packages.props` | `Directory.Packages.props:1-17` exists, `ManagePackageVersionsCentrally=true` at `:3` |
| it imports the shared catalog at `references/Hexalith.Builds/Props/Directory.Packages.props` | `:5` defines `Hexalith1BuildPackageProps` to exactly that path; `:11` imports it (guarded by `HexalithVersionsLoaded != true`), with `../` and `../../` fallbacks at `:12-13` |
| which defines the `Hexalith*Version` properties | Builds props `:6-13` — 8 properties, `HexalithEventStoreVersion 3.104.0` … `HexalithChatbotVersion 1.80.0` |
| and adds the Folders-specific pins on top | `:14-16` — `<PackageVersion Include="LibGit2Sharp" Version="0.32.0" />`, the sole local pin |
| the AppHost SDK is pinned as `Sdk="Aspire.AppHost.Sdk/<version>"` in `Hexalith.Folders.AppHost.csproj` | `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1` — `<Project Sdk="Aspire.AppHost.Sdk/13.5.3">` |
| because CPM structurally cannot version an MSBuild project-SDK resolver | matches the recorded rationale verbatim: `"NuGet CPM cannot provide an MSBuild project SDK resolver version."` |
| registered against catalog package `Aspire.Hosting` in `references/Hexalith.Builds/Tools/package-version-exceptions.json` | the Folders entry exists: `kind: apphost-sdk`, `owner: Hexalith.Folders`, `path` = that csproj, `id: Aspire.AppHost.Sdk`, `version: 13.5.3`, `alignmentRule: exact-catalog-package`, `catalogPackage: Aspire.Hosting`; file header records the 2026-07-18 approved decision |
| this SDK-versus-Hosting axis produced the Epic 9 DCP blocker | consistent with the recorded diagnosis (`reconcile-sprint-change-proposal-2026-07-07-dcp-lane-standup.md:42`, "the `--tls-cert-file` compatibility diagnosis and DCP version table") |

This is the strongest fix in the pass. It replaces a delegation that could not answer the question with one that can, and it names the structural reason. The pair is currently aligned: `Aspire.AppHost.Sdk/13.5.3` (csproj `:1`) vs `Aspire.Hosting 13.5.3` (Builds props `:113`), against a published latest of `13.5.4` for both.

Two things keep it short of full closure — both are placement, not substance, and are carried as **TECH-R3**:
- `:588` and `:762` (I-1) are **textually unchanged**: both still name `Aspire.Hosting.AppHost`, which appears in **no** `.csproj`, `.props`, `.json` or `.yml` in this repository (grep excluding `references/` returns nothing), and both still delegate its version to a props file that has no row for it.
- `:588` cites the **Builds catalog** as the authority while `:1746` cites the **repo-root** file as the entry point. Both are navigable, but they are different sentences pointing at different files, 1,158 lines apart, with no cross-reference.

### TECH-U2 — the "never version numbers" rule — **PARTIALLY CLOSED**, still open as **TECH-R2 (HIGH)**

The absolute is gone and a criterion replaced it. That is real progress: the rule at `:1746` now reads *"it records a version only when the version is the decision — a compatibility floor or ceiling, a native ABI, a prerelease being relied upon, or an approval-bound pin — and never merely to report what is current."*

**Is the new rule self-consistent with the document's actual text? No — and it now fails in both directions.** Full census of surviving version literals against the four categories:

| Site | Literal | Fits a stated category? |
| --- | --- | --- |
| `:1746` | `5.0.0-rc` (Fluent UI) | ✅ prerelease |
| `:605`, `:695`, `:1316`, `:1659` | `Octokit 14.0.0` ×4 | ✅ approval-bound **in fact** — but the document never says so (see below) |
| `:633` (D-2) | `Redis 7.x via Aspire` | ❌ no category; and **no referent** — `grep -rn "AddRedis\|redis:" src/ deploy/` returns nothing, no image tag, no pinned Redis version anywhere in the repo |
| `:634` (D-3) | `self-hosted Redis 7+` | ~ arguably a floor, unverifiable |
| `:693`, `:1389` | `System.CommandLine 2.x` | ❌ no category (the "major-line/spec identifier" category from the recommended wording was dropped) |
| `:81`, `:560`, `:590` | `xUnit v3` ×3 | ❌ no category (though it is literally part of the package id `xunit.v3`) |
| `:320`, `:788`, `:1845`, +6 | `OpenAPI 3.1` | ~ defensible as "the version is the decision"; not explicitly covered |
| `:1550` | `github/14.0.0/openapi-snapshot.json` | ❌ path node, and the path does not exist (TECH-R9) |

And the rule now **mandates records that are absent**:
- **`Microsoft.OpenApi` 2.x ceiling.** Builds props `:238-239` holds `2.12.0` with the comment *"Keep Microsoft.OpenApi on 2.x: Microsoft.AspNetCore.OpenApi 10.x is compiled against the 2.x API surface; v3 breaks runtime OpenAPI document generation."* Published latest is **3.10.2** (fetched 2026-09-16). This is a compatibility ceiling by the rule's own words; A-3 does not mention it.
- **`MessagePack` security floor.** `tests/load/Hexalith.Folders.LoadTests.csproj:9-11` pins `MessagePack` explicitly to override *"NBomber's vulnerable transitive MessagePack 2.5.192 (GHSA-hv8m-jj95-wg3x)"*, resolved to `3.1.8` (Builds props `:171`). A floor with a CVE behind it, recorded nowhere in the architecture.

**The concrete unmitigated hazard.** `Octokit 14.0.0` is load-bearing: `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs:46` asserts the literal `PackageVersion Include="Octokit" Version="14.0.0"` in the shared props, `:60` asserts the literal ``Octokit `14.0.0` `` in `docs/contract/provider-compatibility-catalog.md` — the **OQ4 catalog approved 2026-09-05 and digest-bound** (`:33`). Nothing in architecture.md tells a reader any of this. The next sweep, applying the new rule honestly, sees four bare `14.0.0`s "merely reporting what is current" and deletes them. That is the exact failure TECH-U2 was raised to prevent, and the criterion was supposed to prevent it by being **applied**, not merely stated.

### TECH-U3 — lost constraints — **CLOSED on substance** ✅ (residual: TECH-R4, MEDIUM)

Both constraints are recorded at `:1746`, and **both were independently verified true**:

1. **Fluent UI prerelease.** Doc: *"`Microsoft.FluentUI.AspNetCore.Components` is on a `5.0.0-rc` prerelease while the stable line is 4.x, and F-3's approved WCAG 2.2 AA claim rests on it."* Verified — Builds props `:226-227` pin `5.0.0-rc.5-26219.1`; live nuget flat-container (2026-09-16, 81 versions): **latest overall = `5.0.0-rc.5-26219.1`, latest stable = `4.14.4`**. Both halves of the claim are exactly right, including "the stable line is 4.x".
2. **libgit2 native ABI.** Doc: *"`LibGit2Sharp` carries a native `libgit2` ABI, which is why a dedicated Alpine/musl smoke lane exists (`tests/tools/run-forgejo-smart-http-alpine-smoke.ps1`) — a bump is a native-compatibility event, not a package bump."* Verified — the script exists (7,512 bytes) at exactly that path and is invoked by `.github/workflows/nightly-drift.yml:61`; `ForgejoSmartHttpGitTransport.cs:30` documents the *"pinned LibGit2Sharp 0.32.0 / libgit2 1.8.6 native profile"*; `ForgejoReadinessMapper.cs:42` publishes `["libgit2_version"] = "1.8.6"` as readiness evidence. `LibGit2Sharp 0.32.0` is also the current published version.

This is the finding pass 2 handled best — the reasoning ("a bump is a native-compatibility event") is the right frame, not just the right fact. Residuals are carried as TECH-R4.

### TECH-U6 — `oasdiff` propagation — **CLOSED downstream** ✅ / **REOPENED in-document as TECH-R1 (HIGH)**

**The three edits are correct and complete.** Verified by diff:

| File | Before | After | Assessment |
| --- | --- | --- | --- |
| `docs/adrs/0003…md:23` | "*nightly oasdiff drift classifies…*" | "*the nightly drift classifier (`tests/tools/forgejo-drift/`) classifies…*" | correct; path exists; in-place edit is defensible because the **decision** (nightly drift classification gates readiness) did not change, only the tool name — a supersession note would be over-ceremony here |
| `docs/runbooks/provider-drift.md:3` | "*detected by the nightly oasdiff lane*" | "*detected by the nightly drift lane (`tests/tools/run-nightly-drift-gates.ps1` driving `tests/tools/forgejo-drift/`)*" | correct, and the operator now gets the **runnable** entry point, which is the right call for an operator-facing runbook |
| `docs/runbooks/index.md:13` | "*additive, breaking, and unknown oasdiff drift*" | "*additive, breaking, and unknown provider schema drift*" | correct; a one-line index row does not need the tool path |

Repo-wide confirmation: `grep -rn "oasdiff"` over `*.md`/`*.cs`/`*.ps1`/`*.yml`/`*.json` outside `references/` now returns **zero hits in `docs/`, in any workflow, script, fixture or test**. A-7's surviving claim that `oasdiff` *"appears in no build, test, or tooling artifact"* is therefore **true as written**.

**But the same paragraph now carries a false claim** — see TECH-R1.

### TECH-U8 — dual-mode default — **CLOSED** ✅ (residual: TECH-R8, MEDIUM)

`:536` was checked against the MSBuild precedence chain, not just read. `Directory.Build.props:15-21` evaluates in order:

```
15  UseNuGetDeps=='true'      → UseHexalithProjectReferences=false
16  UseNuGetDeps=='false'     → true
17  Configuration==''         → true
18  Configuration=='Release'  → false
19  Configuration=='Debug'    → true
20  (terminal fallback)       → false
```

Every clause of the corrected text holds:
- *"the selector is `Configuration`, not an explicit flag"* ✅ — lines 17-19 are the ones that fire in normal use.
- *"Debug (and no explicit configuration) → project references"* ✅ — `:19` and `:17`.
- *"Release → NuGet package references"* ✅ — `:18`.
- *"`-p:UseNuGetDeps=true` forces the package mode explicitly and is what `.github/workflows/release-packages.yml` passes"* ✅ — `release-packages.yml:131, 134, 180, 183`, all four carrying `-p:UseNuGetDeps=true` (and `-p:Configuration=Release` / `-c Release`, so both selectors agree there).
- *"a plain `dotnet build -c Release` already resolves to packages"* ✅ — `:18`, then `:20`.
- **`UseHexalithProjectReferences` is now named**, closing residual 2 of TECH-U8. It is the switch the props comment tells you to force (`-p:UseHexalithProjectReferences=true|false`) and the one `tests/tools/run-baseline-ci-gates.ps1:212-222` asserts on.

This correction is more accurate than the props file's own comment, which says only "Debug/Release" and omits the empty-`Configuration` case. Good work.

---

## Part B — Findings

### TECH-R1 — HIGH — A-7 says three documents "still name it and are owed the same correction"; the same working tree corrected all three

**Claim (`:696`, A-7):** *"…the 2026-05 draft named `oasdiff` as the classifier, which was never adopted and appears in **no build, test, or tooling artifact**. Three published documents still name it and are owed the same correction: `docs/adrs/0003-provider-abstraction-and-capability-model.md`, `docs/runbooks/provider-drift.md`, and `docs/runbooks/index.md`."*

**Evidence it is false:** all three files are `M` in `git status` and their diffs (Part A, TECH-U6) remove every `oasdiff` occurrence. `grep -rn "oasdiff" docs/` returns **zero**. The run's own records agree the work was done: `.memlog.md:88` — *"the 3 published docs fixed in lockstep (ADR-0003, provider-drift.md, runbooks/index.md)"*; `.memlog.md:91` — *"3 published docs de-oasdiff'd"*; `validation-report-2026-09-16.md:80` — *"already propagated into three `docs/` files"*; `reconcile-architecture-downstream-2026-09-16.md:20` — *"`oasdiff` corrected here and in three published docs"*. The architecture document is the **only** artifact in the run that says the debt is open.

**Why it matters beyond a stale sentence.** It is a decision record stating, in the present tense, that three named published documents disagree with it — when they agree. The next maintainer either re-edits already-correct files or concludes the document is not trustworthy on lockstep claims, which is the one thing this paragraph exists to assert. It is also the third instance in three reviews of the same shape: a claim corrected in one place while its copy goes stale — here inverted, with the *copy* corrected and the *claim* going stale.

**And the debt that is actually open is now hidden by it.** `oasdiff` still stands in four `_bmad-output` artifacts:
- `_bmad-output/planning-artifacts/epics.md:327` (`AR-PROVIDER-04`) — **co-normative with architecture.md**
- `_bmad-output/test-artifacts/test-design/test-design-epic-2.md:48`
- `_bmad-output/implementation-artifacts/3-4-implement-forgejo-provider-adapter-and-drift-detection.md:83` ("*the architecture-approved oasdiff classifier policy*")
- `_bmad-output/implementation-artifacts/7-17-publish-adr-set-and-maintenance-runbooks.md:141, 155`

**The brief's premise that `epics.md:327` "is recorded as owed in the reconciliation note" does not hold.** `reconcile-architecture-downstream-2026-09-16.md` §2 ("Owed lockstep — NOT applied") has two `epics.md` entries: **§2.1** (PD8/PD10/PD11 story admission) and **§2.6** (the `X-Hexalith-Retry-Transport` header-name collision). Neither mentions `oasdiff`, `AR-PROVIDER-04`, or the drift classifier; `grep -n "oasdiff\|AR-PROVIDER"` over the note returns nothing. The note's §5 ratified scope explicitly says **"no `epics.md` edits"**, so the edit was correctly not made — but the debt was never written down anywhere.

**Correction:** delete the "*Three published documents still name it…*" sentence (the three are fixed; the ADR and both runbooks can simply be cited as *aligned* if provenance is wanted), and add an `epics.md` row to reconciliation-note §2 naming `AR-PROVIDER-04` at `:327` plus the three `_bmad-output` artifacts, owner Delivery.

### TECH-R2 — HIGH — the constraint rule is stated but not applied; `Octokit 14.0.0`'s approval-binding is still invisible, and two rule-mandated constraints are missing

Full argument and census in Part A / TECH-U2. In one line: the rule went from *self-falsifying absolute* to *unapplied criterion*, and the sweep hazard it was written to close is still open because `Octokit 14.0.0` appears four times with nothing marking it as a contract term.

**Correction (three small edits):**
1. At A-6 `:695`, mark the binding inline: *"Octokit `14.0.0` — **pinned as a contract term**, asserted by `GitHubDependencyGuardTests` and by the digest-bound OQ4 catalog (`docs/contract/provider-compatibility-catalog.md:33`); a bump requires a catalog version bump and re-approval."*
2. Extend the rule's category list with *"a major-line or spec identifier that is part of the technology's name (`OpenAPI 3.1`, `xUnit v3`, `System.CommandLine 2.x`)"* — the three survivors that fit nothing today.
3. Apply the rule to the two misses: add the `Microsoft.OpenApi` 2.x ceiling to A-3, and delete `7.x` from D-2 (see TECH-R7 — it is the one survivor with no referent at all).

### TECH-R3 — MEDIUM — TECH-U1 residual: `:588` and `:762` still name a package the repo does not use, and cite a different authority than `:1746`

`Aspire.Hosting.AppHost` is absent from every `.csproj`, `.props`, `.json` and `.yml` outside `references/`, and has no row in either props file. The AppHost's only third-party `PackageReference`s are `Aspire.Hosting.Redis` and `CommunityToolkit.Aspire.Hosting.Dapr` (`csproj:28-29`); the SDK attribute at `:1` supplies the rest. The package is still published (54 versions, latest `13.5.4`), so a reader cannot discover the problem by a 404 — they look in the cited props file, find nothing, and guess.

**Correction:** at `:588` and `:762`, replace `Aspire.Hosting.AppHost` with *"the `Aspire.AppHost.Sdk` project SDK + `Aspire.Hosting.Redis` + `CommunityToolkit.Aspire.Hosting.Dapr`"* and add *"(SDK version: see §Decision Compatibility — not CPM-ownable)"*.

### TECH-R4 — MEDIUM — TECH-U3 residual: the constraints live only in the recital, and the libgit2 record omits its approval binding

- **F-3 (`:752`) is textually unchanged.** It still asserts WCAG 2.2 AA conformance ("focus-visible, target sizes, dragging exemption confirmed") with no mention that the component library is a release candidate. The prerelease fact is recorded 994 lines later, in a paragraph a reader consulting the frontend decision table has no reason to open. The GA-upgrade trigger the record implies has no home in the row it constrains.
- **The libgit2 record understates the binding.** `:1746` calls it a native-ABI event. True, and it is also **approval-bound**: `docs/contract/provider-compatibility-catalog.md:33` records *"LibGit2Sharp `0.32.0` with bundled libgit2 `1.8.6`"* in the digest-bound OQ4 catalog approved 2026-09-05. Operationally that is the stronger constraint — a bump does not just need a native-compatibility check, it invalidates an approved digest and needs C12 re-approval.
- LibGit2Sharp still has **no decision row** (A-6/A-7 cover the provider API clients; D-8 covers checkout location).

### TECH-R5 — MEDIUM — TECH-U5 carried untouched: "live-nightly-drift mode" is still asserted at four sites; the tool declines to claim it

Unchanged and now at four sites: `:332` (C12 — *"hermetic-PR-gate mode AND live-nightly-drift mode"*), `:696` (A-7 — *"runs against each pinned upstream tag plus a weekly `HEAD` poll"*), `:792` (Phase 5), `:1102` (CI-gates bullet — *"contract tests (hermetic + nightly live-drift)"*).

As-built, re-verified: `run-nightly-drift-gates.ps1:7-8` — `[ValidateSet('pinned-snapshots','latest-supported')] $ProviderProfile = 'pinned-snapshots'`; neither profile fetches an upstream tag or polls `HEAD`. `nightly-drift.yml:56` passes `pinned-snapshots`. `run-nightly-drift-gates.ps1:682` deliberately records `credentialed-live-provider-evidence` as `not_run`/`informational`. C12 is an **approved exit criterion**; its measurement method should not over-claim above what its own tool reports.

### TECH-R6 — MEDIUM — TECH-U7 carried untouched: UI/E2E stack, serializer, OpenAPI ceiling

Re-verified at the pass-2 state: `grep -c "Playwright"` → **0**; `AxeCore` → **0**; `newtonsoft` → **0**; `system.text.json` → **0**. The `accessibility-gates` WCAG claim still rests on two packages the document never names (`Microsoft.Playwright 1.62.0`, `Deque.AxeCore.Playwright 4.13.0`, both current). The canonical SDK still generates with `src/Hexalith.Folders.Client/nswag.json:59` `"jsonLibrary": "NewtonsoftJson"` — a different serializer from the ASP.NET Core 10 server default, and material to `ComputeIdempotencyHash()` (A-2/A-9) and the encoding-equivalence corpus. `Hexalith.Folders.UI.E2E.Tests`, `Hexalith.Folders.AppHost.Tests` and `Hexalith.Folders.LoadTests.Tests` remain absent from both project trees. The `Microsoft.OpenApi` ceiling is now also a **rule violation** (TECH-R2), not merely an omission.

### TECH-R7 — MEDIUM — TECH-U9 carried verbatim: both lines still contradicted by files in this repository

- **`:633` (D-2) — "Redis 7.x via Aspire."** Re-verified: `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml` is `type: state.redis` at `localhost:6379`, provisioned by `dapr init`; `grep -rn "AddRedis\|redis:" src/ deploy/` returns **zero**. No image tag, no `WithImageTag`, no compose file. `7.x` has no referent anywhere in the repository, which makes it the single clearest casualty of a sweep whose stated purpose was removing exactly that — and the clearest rule violation under TECH-R2.
- **`:1698` — "production policies maintained outside repo per ops runbook."** Re-verified false: `deploy/dapr/production/` contains `accesscontrol.yaml`, `daprsystem.yaml`, `pubsub.yaml`, `secretstore.yaml`, `sidecar-config-bindings.yaml`; the conformance lane (`tests/tools/run-dapr-policy-conformance-gates.ps1`, `.github/workflows/policy-conformance.yml`) is in-repo too. A security reviewer following I-3 is sent to the wrong repository.

### TECH-R8 — MEDIUM — TECH-U8 residual: the two lockstep sites were not updated, and two more siblings share the switch

- **`:482`** still reads *"central package management for third-party packages (Hexalith.\* siblings consumed **via project reference, not pinned here**)"* and **`:1185`** *"(Hexalith.\* siblings **via project reference**)"*. Both are the single-mode claim `:536` corrects. `:482` is additionally misleading now that `:1746` names the same file as the version **entry point**: it *is* where `LibGit2Sharp` is pinned, and it is the import site for every `Hexalith*Version`.
- **`:536` names only `Hexalith.EventStore.*` and `Hexalith.Tenants.*`.** `Hexalith.Memories.*` and `Hexalith.FrontComposer.*` ride the identical switch (`Directory.Build.props:6-7, 24-27`; `AppHost.csproj:24-25`), so a reader inventorying "what breaks the release lane" undercounts by two.

### TECH-R9 — LOW — TECH-U11 carried untouched: four structure-diagram paths still misdescribe the repo

- `:1550` — `github/ └── 14.0.0/openapi-snapshot.json`. Actual: `tests/contracts/github/` holds a single file, `pinned-profile.json`.
- `:1546-1548` — `forgejo/ v15.0/ v14.0/ v13.0/`. Actual: `11.0.14`, `14.0.5`, `15.0.2`, `15.0.7`, `16.0.3` + `supported-versions.json`. Three of the three named directories do not exist, and the smart-HTTPS profile the ADR pins (`15.0.7`, `16.0.3`) is invisible in the diagram.
- `:1562` — `tools/ ├── policy-conformance/`. No such directory; the real artifact is `tests/tools/run-dapr-policy-conformance-gates.ps1`.
- `tests/tools/pattern-examples/` exists and is absent from the diagram (the document discusses pattern-examples drift at `:1805`).

### TECH-R10 — LOW — TECH-U12 carried untouched: Aspire dashboard URL

`:1725` — *"Aspire dashboard at `https://localhost:17000`"*. `src/Hexalith.Folders.AppHost/Properties/launchSettings.json:8` — `"applicationUrl": "https://localhost:17217;http://localhost:15437"`. Point at `launchSettings.json` rather than hard-coding a port.

### TECH-R11 — LOW — TECH-U10 carried untouched: Aspire product naming

*".NET Aspire"* at 5 sites (`:78`, `:494`, `:588`, `:762`, `:1494`). Per [aspire.dev](https://aspire.dev/whats-new/aspire-13/), the product dropped its `.NET` prefix at 13.0, the same release that decoupled Aspire from .NET versioning. The repo is on 13.5.3, well past the rename. Cosmetic, but this lens exists to catch names carried from a 2026-05 draft.

---

## Currency verification — re-fetched 2026-09-16 (live)

| Technology | Repo pin | Latest published | Currency |
| --- | --- | --- | --- |
| .NET SDK | `global.json` `10.0.401` (local `dotnet --version` = `10.0.401`, `rollForward: latestPatch`) | 10.0 channel `active`: runtime `10.0.12`, SDK `10.0.401`, released 2026-09-08 | **exact** |
| .NET TFM | `Directory.Build.props:31` `net10.0` | .NET 11 is `11.0.0-rc.1` / `go-live` | **correct to stay on 10** |
| Aspire hosting family | Builds props `:113-121` `13.5.3` | `13.5.4` | 1 patch back |
| Aspire AppHost SDK | `AppHost.csproj:1` `13.5.3` | `13.5.4` | 1 patch back — **pair aligned with Hosting** ✅ |
| Aspire.Hosting.AppHost | **not referenced** | `13.5.4` (54 versions) | n/a — **TECH-R3** |
| CommunityToolkit.Aspire.Hosting.Dapr | `:136` `13.5.1-beta.752` | `13.5.1-beta.757` | good (beta line) |
| Hexalith.EventStore / Tenants | `3.104.0` / `5.7.0` | `3.105.0` / `5.7.0` | good / exact |
| Microsoft.FluentUI.AspNetCore.Components | `:226-227` `5.0.0-rc.5-26219.1` | stable `4.14.4`; overall = that RC | **prerelease — now recorded** ✅ |
| LibGit2Sharp | root props `:15` `0.32.0` | `0.32.0` | **exact — now recorded** ✅ |
| Octokit | `:264` `14.0.0` | `14.0.0` | exact — **TECH-R2** |
| ModelContextProtocol | `:248, :250` `2.2.0` | `2.2.0` | exact |
| System.CommandLine | `:302` `2.0.12` | `2.0.12` stable; `3.0.0-rc.1.26425.128` exists | current — upgrade watch stands |
| Microsoft.Playwright | `:240` `1.62.0` | `1.62.0` | exact — **TECH-R6** (unnamed) |
| Deque.AxeCore.Playwright | `:148` `4.13.0` | `4.13.0` | exact — **TECH-R6** (unnamed) |
| Microsoft.OpenApi | `:239` `2.12.0` | `3.10.2` | **deliberately held** — **TECH-R2/R6** |
| MessagePack | `:171` `3.1.8` (CVE override) | — | security floor — **TECH-R2** |
| NBomber | `:253` `6.6.0` | `6.6.0` | exact |
| Newtonsoft.Json | `:263` `13.0.4` | `13.0.4` | exact — **TECH-R6** (no decision) |
| Testcontainers | catalog `4.15.0`, **unused in Folders** | `4.15.0` | **TECH-5 stays closed** ✅ |
| oasdiff | absent from repo **and now from `docs/`** | live upstream project, never adopted | **TECH-U6 closed downstream** ✅ |

**No stale numeric pin crept back in.** The single version literal pass 2 added (`5.0.0-rc`) is correct and rule-compliant.

---

## Closure tally

| Prior (update review) | Severity | Disposition at pass 2 |
| --- | --- | --- |
| TECH-U1 — Aspire authority provably false | HIGH | **SUBSTANTIALLY CLOSED** ✅ — new ownership statement verified true clause by clause; residual **TECH-R3 (MEDIUM)** |
| TECH-U2 — rule falsified by its own document | HIGH | **PARTIALLY CLOSED → TECH-R2 (HIGH)** — absolute replaced by a criterion, but unapplied and falsified both ways |
| TECH-U3 — two constraints lost | HIGH | **CLOSED** ✅ — both recorded, both independently verified; residual **TECH-R4 (MEDIUM)** |
| TECH-U4 — authority mis-scoped / ambiguous | MEDIUM | **CLOSED** ✅ — the root-file reframe makes the bare `Directory.Packages.props` citations at `:695`/`:762` resolve correctly, and root `:15` genuinely owns LibGit2Sharp |
| TECH-U5 — "live-nightly-drift" over-claim | MEDIUM | **NOT ADDRESSED → TECH-R5** (now 4 sites) |
| TECH-U6 — oasdiff not propagated | MEDIUM | **CLOSED downstream** ✅ / **REOPENED as TECH-R1 (HIGH)** — doc now asserts an already-paid debt and hides the unpaid one |
| TECH-U7 — undocumented technologies | MEDIUM | **NOT ADDRESSED → TECH-R6** |
| TECH-U8 — dual-mode default / switch / lockstep | MEDIUM | **CLOSED on the mechanism** ✅ — `Configuration`-driven default and `UseHexalithProjectReferences` both verified; residual **TECH-R8 (MEDIUM)** |
| TECH-U9 — Redis 7.x / policies-outside-repo | MEDIUM | **NOT ADDRESSED → TECH-R7** (verbatim) |
| TECH-U10 — Aspire naming | LOW | **NOT ADDRESSED → TECH-R11** |
| TECH-U11 — structure-diagram paths | LOW | **NOT ADDRESSED → TECH-R9** |
| TECH-U12 — dashboard port | LOW | **NOT ADDRESSED → TECH-R10** |

**Prior:** 3 HIGH / 6 MEDIUM / 3 LOW (12). **Closed outright:** 2 HIGH (U1 substantially, U3) + 2 MEDIUM (U4, U6-downstream, U8-mechanism). **New:** 1 HIGH (TECH-R1). **This review:** **2 HIGH / 6 MEDIUM / 3 LOW (11).**

---

## Reviewer notes

1. **The substance is right; the packaging is what failed.** Everything pass 2 asserted about the build system is true, and I checked it the hard way — MSBuild condition precedence, the exceptions-file schema, the flat-container version lists. That is a real improvement in the document's reliability. Both remaining HIGHs are *sentences about the document's own state*, not engineering errors: one claims a debt that was paid in the same tree, the other states a rule and then does not apply it. Both are short edits.
2. **The recurring failure is unchanged in shape but has inverted.** For three reviews running, the defect has been "a claim corrected in one place while its copy goes stale." TECH-R1 is the same shape with the polarity flipped — the copies were fixed and the claim went stale. A remediation pass that edits N files should re-read its own description of those N files last, not first.
3. **`:1746` is doing too much work.** It is now a single paragraph carrying version ownership, the CPM exception, the recording rule, two named constraints, the parity-collapse argument and the D-7/D-9 rationale. The Fluent UI prerelease belongs at F-3, the Octokit binding at A-6, the OpenAPI ceiling at A-3, the AppHost SDK at I-1 — with `:1746` keeping only the rule. Constraints are found where the decision is read, not in a recital.
4. **Honesty labelling remains the best feature and is still under-applied.** The `**as-built**` / "*the 2026-05 draft named X, which was never adopted*" pattern now appears at `:81`, `:358`, `:491`, `:696`, and I-3/I-8's "Target (not built)" rows are exemplary. The findings that survive — `:633`, `:1698`, `:332`'s live-drift mode, the structure diagram — are precisely the ones that still lack it.
5. **Not re-reported from other lenses:** S-7 vocabulary convergence, PD11 triple-keying, the requirements-coverage rewrite, PD8. Out of this lens's scope; I take no position.

---

## Web sources (fetched 2026-09-16)

- `https://api.nuget.org/v3-flatcontainer/{package}/index.json` — live flat-container version lists for `microsoft.fluentui.aspnetcore.components` (81 versions), `aspire.hosting`, `aspire.apphost.sdk`, `aspire.hosting.apphost`, `octokit`, `modelcontextprotocol`, `system.commandline`, `microsoft.playwright`, `libgit2sharp`, `microsoft.openapi`, `communitytoolkit.aspire.hosting.dapr`, `nbomber`
- `https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json` — .NET 10.0.12 / SDK 10.0.401 (2026-09-08, `active`); .NET 11.0.0-rc.1 / SDK 11.0.100-rc.1.26425.128 (`go-live`)
- [Aspire 13 — What's new](https://aspire.dev/whats-new/aspire-13/) — product rename to "Aspire"; `Aspire.AppHost.Sdk` supersedes the explicit `Aspire.Hosting.AppHost` package reference
