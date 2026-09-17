# Reviewer Gate — Technology / Currency & Reality-Check Lens (Update Re-Review)

- **Artifact under review:** `_bmad-output/planning-artifacts/architecture.md` (1902 lines, amended 2026-09-16; `updated: '2026-09-16'`, `validationRemediation: 'validation-report-2026-09-16 …'`)
- **Prior review:** `reviews/review-technology-2026-09-16.md` — verdict **FAIL**, 6 HIGH / 6 MEDIUM / 3 LOW (TECH-1 … TECH-15)
- **Repo state:** branch `main`, HEAD `de281e7`; architecture.md is uncommitted (`git diff` = +91 / −46)
- **Date:** 2026-09-16
- **Scope:** (1) does the version-removal structural approach actually work; (2) the three claims the amendment makes about TECH-4/5/6; (3) wider technology currency, repo-vs-world.

**Nothing in this review is asserted from training data.** Every currency claim is backed by a live `api.nuget.org` flat-container fetch, the live `dotnet/core` releases index, or `aspire.dev` on 2026-09-16, or by a repo `file:line`. Web sources are listed at the end.

---

## Verdict

**FAIL — materially improved, but the structural fix has a hole on the one axis that has already broken this repo once.**

The amendment chose the right direction. Deleting copied version numbers and naming a single authority is strictly better than refreshing numbers that will rot again, and the reviewer note in the prior review recommended exactly that. Three of six HIGH findings are **fully closed and verified** (TECH-1 MCP, TECH-3 EventStore/Tenants, TECH-5 Testcontainers), and two more are substantially closed (TECH-4, TECH-6). The three specific claims I was asked to check — Testcontainers unused, `oasdiff` absent, the dual-mode package management — are **all true**, and the named replacement lane is real and is genuinely the classifier.

It still fails on three counts:

1. **The delegation target does not own the Aspire version.** `Aspire.Hosting.AppHost` — the package the document names at `:587` and `:757` — has **no row at all** in `references/Hexalith.Builds/Props/Directory.Packages.props`. The version that actually governs is `Aspire.AppHost.Sdk/13.5.3`, hard-coded in `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1`, and the Hexalith ecosystem already records this as a *CPM-incompatible exception* in a file the document never cites. So the document now points a reader at a file that cannot answer the question, on the exact SDK-vs-Hosting axis that produced the Epic 9/10 DCP boot blocker.
2. **The rule is falsified by its own document.** `:1741` states "*this document names technologies, never their version numbers*". `Octokit 14.0.0` survives five times, `Redis 7.x` twice, plus `System.CommandLine 2.x`, `xUnit v3`, `OpenAPI 3.1` and a `14.0.0/` path node. Several of those retentions are **correct** — but the document states no criterion separating a genuine compatibility constraint from a merely-current version, so the exceptions read as oversights rather than decisions.
3. **Genuine constraints were lost in the removal.** Fluent UI's shipped pin is a **release candidate** underwriting an approved WCAG 2.2 AA claim; `libgit2 1.8.6` is a **native ABI** profile with a dedicated Alpine/musl smoke lane. Neither is a "current version"; both are constraints, and the new rule now forbids recording either.

Six prior findings (TECH-9 … TECH-14) were not touched at all. Two of them — `Redis 7.x via Aspire` at `:632` and "production policies maintained outside repo" at `:1693` — are still stated present-tense and are still contradicted by files in this repository.

**The prior gate's headline conclusion is CONFIRMED, not refuted:** repository currency is genuinely good. Every pin re-checked on 2026-09-16 is at, or within one release of, the current published version. The gap remains doc-vs-repo, not repo-vs-world.

---

## Part A — Does the structural approach work?

### (a) Did every stale numeric pin actually go? — **Mostly, with surviving numbers**

| Site | Version text | Stale? | Verdict |
| --- | --- | --- | --- |
| `:591`, `:661`, `:1366`, `:1698` (old) — MCP `1.3.0` ×4 | removed | was stale | **gone** ✅ |
| `:575`, `:729`, `:1698` (old) — Aspire `13.4.6`, CT Dapr `13.4.0-preview.1.260602-0230` | removed | was stale | **gone** ✅ (but see TECH-U1) |
| `:1698` (old) — EventStore/Tenants `3.15.1 verified on NuGet 2026-05-09` | removed | was stale | **gone** ✅ |
| `:604`, `:690`, `:1311`, `:1545`, `:1654` — `Octokit 14.0.0` ×5 | **survives** | **not stale** (= repo pin = latest) | see **TECH-U2** |
| `:632` — `Redis 7.x via Aspire` | **survives** | **unverifiable** (no Redis version pinned anywhere in repo) | see **TECH-U9** |
| `:633` — `self-hosted Redis 7+` | **survives** | unverifiable | see **TECH-U9** |
| `:688`, `:1384` — `System.CommandLine 2.x` | survives | accurate (`2.0.12`) | acceptable |
| `:81`, `:589` — `xUnit v3` | survives | accurate (`xunit.v3 4.0.1`) | acceptable |
| `:319`, `:783`, `:1840` — `OpenAPI 3.1` | survives | accurate (spec version, not a package) | correct to keep |
| `:1545` — `github/14.0.0/openapi-snapshot.json` | **survives** | **path does not exist** | see **TECH-U11** |

No *stale* numeric pin against a package survives. That part of the mandate passes.

### (b) Does the cited authority genuinely own those versions? — **Partly. Two provable misses.**

**It works for the Hexalith siblings.** `references/Hexalith.Builds/Props/Directory.Packages.props:6-13` really does declare the `Hexalith*Version` property block:

```
HexalithCommonsVersion 2.30.0 · HexalithPolymorphicSerializationsVersion 1.19.2
HexalithEventStoreVersion 3.104.0 · HexalithFrontComposerVersion 4.4.0
HexalithMemoriesVersion 2.27.1 · HexalithTenantsVersion 5.7.0
HexalithPartiesVersion 1.1.1 · HexalithChatbotVersion 1.80.0
```

and `Hexalith.EventStore.*` / `Hexalith.Tenants.*` `PackageVersion` items resolve to those properties (`:42`, `:82`). TECH-3 is properly closed by construction.

**It also works for most third-party packages**, because the repo-root `Directory.Packages.props:11-13` *imports* the Builds file, so a reader who resolves the bare name to the root file still reaches the right pins.

**It fails in two specific, checkable places:**

- **`Aspire.Hosting.AppHost` has no row in the cited file.** `grep "Aspire" references/Hexalith.Builds/Props/Directory.Packages.props` returns `Aspire.Hosting`, `Aspire.Hosting.Redis`, `Aspire.Hosting.Testing`, `Aspire.Hosting.Docker`, the Azure family (all `13.5.3`), Keycloak/Kubernetes (`13.5.3-preview.1.26425.3`), and `CommunityToolkit.Aspire.Hosting.Dapr 13.5.1-beta.752` — and **no `Aspire.Hosting.AppHost`**. → **TECH-U1 (HIGH)**.
- **`LibGit2Sharp` is pinned in a different file.** `Directory.Packages.props:15` **at the repo root** carries `<PackageVersion Include="LibGit2Sharp" Version="0.32.0" />` — it is the **only** package this repo pins locally. `:1741` names LibGit2Sharp in the technology set and then directs the reader to the Builds file, which has no LibGit2Sharp row. → **TECH-U4 (MEDIUM)**.

### (c) Is the citation specific enough to be actionable? — **Borderline.**

- `:587` and `:1741` give the full path `references/Hexalith.Builds/Props/Directory.Packages.props`. Good.
- `:689` (A-5) and `:757` (I-1) give only the bare filename `Directory.Packages.props`. **Two files in this repo have that name** (repo root and Builds), and `:481` / `:1180` describe the root one. Resolvable via the import chain, but ambiguous as written.
- `:1741` scopes the authority to "*and its `Hexalith*Version` properties*". Those eight properties cover only the Hexalith siblings. The same sentence names NSwag, Octokit, LibGit2Sharp, ModelContextProtocol, System.CommandLine, Microsoft.FluentUI and JwtBearer, whose versions are plain `PackageVersion` items, not `Hexalith*Version` properties. The qualifier narrows the authority below what the sentence needs it to cover.

→ **TECH-U4 (MEDIUM)**.

### (d) Did anything that SHOULD be pinned get lost? — **Yes, three things.**

This is the sharpest cost of the approach. Three items are **compatibility constraints**, not current versions, and the new rule has no slot for them:

1. **`Microsoft.FluentUI.AspNetCore.Components 5.0.0-rc.5-26219.1` is a prerelease.** F-3 `:747` asserts WCAG 2.2 AA conformance ("focus-visible, target sizes, dragging exemption confirmed") against an unnamed version. Live check: highest **stable** is `4.14.4`; highest overall is the RC the repo ships. An RC's ARIA output can still change before GA. The *fact of being on an RC* is a risk statement the architecture must carry; "look it up in the props file" does not communicate it. → **TECH-U3 (HIGH)**.
2. **`libgit2 1.8.6` is a native ABI, not a NuGet version.** `src/Hexalith.Folders/Providers/Forgejo/ForgejoSmartHttpGitTransport.cs:30` documents "*the pinned LibGit2Sharp 0.32.0 / libgit2 1.8.6 native profile loaded*", `ForgejoReadinessMapper.cs:42` publishes `["libgit2_version"] = "1.8.6"` as readiness evidence, and `.github/workflows/nightly-drift.yml:61` runs an Alpine/musl smoke lane (`run-forgejo-smart-http-alpine-smoke.ps1`) because of it. No props file records it. → **TECH-U3 (HIGH)**.
3. **The `Microsoft.OpenApi` 2.x ceiling** — this one *survived*, and is the best evidence the approach can work. `Builds props:238-239` carries the constraint as a comment: "*Keep Microsoft.OpenApi on 2.x: Microsoft.AspNetCore.OpenApi 10.x is compiled against the 2.x API surface; v3 breaks runtime OpenAPI document generation*", pinned at `2.12.0` while the published latest is `3.10.2`. A reader who follows `:1741` to that file finds the constraint. But architecture.md's A-3 never mentions it, so the constraint is discoverable only by accident. → noted under **TECH-U7 (LOW)**.

**Conclusion on the approach:** sound, and correct in principle — but it must be paired with an explicit *constraints-stay-here* carve-out, because the three cases above are not answerable by "read the props file".

---

## Part B — The three specific claims

### TECH-5 — Testcontainers: claim **VERIFIED TRUE**, finding **CLOSED** ✅

`:81` now reads: "*xUnit v3, Shouldly, NSubstitute for testing (**as-built**; `Testcontainers` was named here in the 2026-05 draft, has zero usage in Folders, and is not a constraint — provider and topology evidence runs through the Aspire test host and the hermetic fixture lanes instead)*". `:559` and `:589` likewise drop it.

**Evidence:** `grep -rni "testcontainers"` across `*.cs`, `*.csproj`, `*.props`, `*.json`, `*.ps1`, `*.yml` returns **zero hits outside `references/`**. All matches are `references/Hexalith.Memories/tests/…` (a sibling module's fixtures) and Builds tooling catalogs. `Builds props:311` still pins `Testcontainers 4.15.0` (= current per NuGet) for the shared catalog, but no Folders project consumes it. The three remaining Folders mentions (`docs/exit-criteria/c1-capacity.md:31`, `c2-freshness.md:30`, `docs/operations/capacity-calibration.md:40`) all name it in the *negative* ("does not require … Testcontainers"), so they are consistent with the correction and need no lockstep edit.

The correction is accurate, honestly labelled, and keeps the provenance ("named here in the 2026-05 draft") rather than silently deleting.

### TECH-6 — oasdiff: claim **VERIFIED TRUE**, replacement **VERIFIED REAL**, but **lockstep NOT done** ⚠️

**Removed correctly.** `oasdiff` now appears in architecture.md **once**, at `:691`, as an explicit negative: "*the 2026-05 draft named `oasdiff` as the classifier, which was never adopted and exists nowhere in the repository*". The `tests/tools/oasdiff/` node is gone from the structure diagram (`:1556` now reads `forgejo-drift/ # nightly Forgejo schema-diff classifier (as-built)`), and the C12 rows at `:331`/`:358` now name `tests/contracts/forgejo/supported-versions.json` + `run-nightly-drift-gates.ps1` + `tests/tools/forgejo-drift/` as the measurement method. `ls tests/tools/oasdiff` → still `No such file or directory`.

**The replacement is real and is genuinely the classifier.** Verified:
- `tests/tools/run-nightly-drift-gates.ps1` exists (29,671 bytes), invoked by `.github/workflows/nightly-drift.yml:56` on `cron: '17 2 * * *'`.
- `tests/tools/forgejo-drift/` contains `classification-fixtures.json` + `Write-SanitizedForgejoDriftReport.ps1`.
- The classification logic is real: `run-nightly-drift-gates.ps1:358 Assert-DriftClassificationFixtures`, iterating `additive-field, removed-field, type-change, enum-new-string-value, unknown-operation` (`:372`) and enforcing `unknown-unclassified` ⇒ `failure` (`:379-381`) and `breaking-incompatible` ⇒ `failure` (`:383-385`). The fixture file maps `additive-field → additive-compatible/warning`, `removed-field → breaking-incompatible/failure`, etc. — exactly the additive-warn / breaking-fail taxonomy the doc describes.

**Two residuals:**

- **Downstream lockstep was not done and was not noted.** The prior review explicitly asked for it. Still asserting oasdiff: `docs/adrs/0003-provider-abstraction-and-capability-model.md:23` (a **published ADR**, i.e. a decision record), `docs/runbooks/provider-drift.md:3`, `docs/runbooks/index.md:13`, plus a fourth site the prior review missed — `_bmad-output/planning-artifacts/epics.md:327` (`AR-PROVIDER-04`). → **TECH-U6 (MEDIUM)**.
- **The classifier's *input* is still overstated.** `:691` says the job "*runs against each pinned upstream tag plus a weekly `HEAD` poll*" and `:331` says the suite runs in "*hermetic-PR-gate mode AND live-nightly-drift mode*". The as-built lane runs `-ProviderProfile 'pinned-snapshots'` (`ValidateSet` at `run-nightly-drift-gates.ps1:7-8` offers only `pinned-snapshots` / `latest-supported` — no live poll), and the script deliberately records `Add-Result -Category 'credentialed-live-provider-evidence' -Status 'not_run' -Severity 'informational'` (`:682`) with the comment "*Credentialed live provider evidence is reported as explicitly not run, never as a hardcoded placeholder status standing in for real hermetic drift coverage.*" The amendment fixed the classifier's **name** but left its **input** asserted at a fidelity the tool itself declines to claim. → **TECH-U5 (MEDIUM)**.

### TECH-4 — dual-mode package management: claim **VERIFIED REAL**, description **not quite accurate** ⚠️

`:535` now reads: "*consumed **two ways, selected by the `UseNuGetDeps` property** … by **default**, project references to the root-level sibling submodule source, located by the `HexalithEventStoreRoot`/`HexalithTenantsRoot` properties in `Directory.Build.props`; under **`-p:UseNuGetDeps=true`**, package references pinned by the `Hexalith*Version` properties in `Directory.Packages.props`, which is the mode `.github/workflows/release-packages.yml` restores and builds in. Both modes must stay buildable; a change that breaks the NuGet mode breaks the release lane silently, because the default lane will still be green.*"

**Both modes are real and CI-exercised — verified:**
- `Directory.Build.props:15-27` implements the switch and the `Hexalith*FromSource` flags; every consuming csproj carries the conditional pair (e.g. `AppHost.csproj:20-25`).
- `.github/workflows/release-packages.yml:131, 134, 180, 183` — `dotnet restore/build … -p:UseNuGetDeps=true`. Confirmed verbatim.
- `tests/tools/run-baseline-ci-gates.ps1:212-222` asserts **both** modes explicitly (`Assert-DependencyMode -Label 'default'` / `'debug'` expecting `UseHexalithProjectReferences=true, UseNuGetDeps=false`; `-Label 'release-package'` with `-p:Configuration=Release -p:UseNuGetDeps=true` expecting the inverse). The "both modes must stay buildable" warning is therefore backed by a real gate — a good addition.
- `tests/tools/run-release-package-gates.ps1:317-336` does the same for the release lane.

The prohibition that would have broken `release-packages.yml` is gone. **TECH-4's HIGH severity is discharged.** Three residuals keep it at MEDIUM:

1. **"by default, project references" is wrong for `-c Release`.** `Directory.Build.props:18` — `<UseHexalithProjectReferences Condition="… and '$(Configuration)' == 'Release'">false</UseHexalithProjectReferences>` — and the terminal fallback at `:20` is also `false`. A plain `dotnet build -c Release` with **no** `UseNuGetDeps` flag resolves **NuGet packages**. The props file's own comment states the real rule: "*Debug builds use project references to the references/ submodule source; Release builds use the centrally pinned NuGet packages*". The document's `UseNuGetDeps`-only framing omits the Configuration-driven default.
2. **`UseHexalithProjectReferences` is never named**, although it is the primary switch the props comment tells you to use (`-p:UseHexalithProjectReferences=true|false`), and it is what the CI gate asserts on.
3. **`:481` and `:1180` were not updated in lockstep** and now contradict `:535`. Both still say the root `Directory.Packages.props` is "*central package management for third-party packages (Hexalith.\* siblings consumed via project reference, **not pinned here**)*" — the exact single-mode claim `:535` corrects.

Minor: the dual mode also covers `Hexalith.Memories.*` and `Hexalith.FrontComposer.*` (`Directory.Build.props:6-7, 25-27`; `AppHost.csproj:24-25`), which `:535` does not mention.

→ **TECH-U8 (MEDIUM)**.

---

## Part C — New and reopened findings

### TECH-U1 — HIGH — The Aspire version authority is provably false, on the axis that already caused a boot blocker

**Claims:**
- `:587` — "*Local orchestration: .NET Aspire (`Aspire.Hosting.AppHost` + `CommunityToolkit.Aspire.Hosting.Dapr`) — **versions are owned by `references/Hexalith.Builds/Props/Directory.Packages.props`, never restated here***"
- `:757` (I-1) — "*…`Aspire.Hosting.AppHost` + `CommunityToolkit.Aspire.Hosting.Dapr`… ; versions owned by `Directory.Packages.props`*"

**Evidence:**
- The cited file has **no `Aspire.Hosting.AppHost` row.** It pins `Aspire.Hosting 13.5.3` (`:113`), `Aspire.Hosting.Redis 13.5.3` (`:119`), `Aspire.Hosting.Testing 13.5.3` (`:120`), Docker/Azure/Cosmos at `13.5.3`, Keycloak/Kubernetes at `13.5.3-preview.1.26425.3`, and `CommunityToolkit.Aspire.Hosting.Dapr 13.5.1-beta.752` (`:136`). The named package is absent.
- The AppHost **does not reference that package.** `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` (whole file, 31 lines) declares `<Project Sdk="Aspire.AppHost.Sdk/13.5.3">` at `:1` and carries exactly two third-party `PackageReference`s: `Aspire.Hosting.Redis` and `CommunityToolkit.Aspire.Hosting.Dapr` (`:28-29`).
- **The governing version is structurally un-ownable by CPM**, and the ecosystem already says so: `references/Hexalith.Builds/Tools/package-version-exceptions.json` records `Hexalith.Folders`' AppHost SDK explicitly — `"kind": "apphost-sdk"`, `"path": "src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj"`, `"id": "Aspire.AppHost.Sdk"`, `"version": "13.5.3"`, `"rationale": "NuGet CPM cannot provide an MSBuild project SDK resolver version."`, `"alignmentRule": "exact-catalog-package"`, `"catalogPackage": "Aspire.Hosting"`. Its header records an approved architecture decision (2026-07-18, Hexalith.ChatBot Story 1.1e). architecture.md cites none of this.
- Web ([aspire.dev](https://aspire.dev/whats-new/aspire-13/), fetched 2026-09-16): "*The SDK automatically includes `Aspire.Hosting.AppHost`, so an explicit package reference is no longer needed*" — the doc's package-reference framing is the pre-13 idiom.

**Why it matters:** `Aspire.Hosting.AppHost` is still published (54 versions, latest `13.5.4`), so a reader can't discover the problem by the package 404-ing. They go to the props file, find no row, and either give up or substitute `Aspire.Hosting 13.5.3` — which happens to match today, **by coincidence**, because the exceptions inventory keeps the SDK aligned to the `Aspire.Hosting` catalog entry. The moment those diverge, the document's answer is silently wrong. This is precisely the SDK-vs-Hosting mismatch class that root-caused the Epic 9/10 DCP `--tls-cert-file` boot blocker (SDK 13.3.5 vs Hosting 13.4.6). The old text at least named a wrong number a reader could check; the new text names an authority that has nothing to check.

**Correction:** state that Aspire's AppHost version is pinned by the `Aspire.AppHost.Sdk` attribute in `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1` because CPM cannot own an MSBuild SDK resolver version; that it MUST stay equal to the `Aspire.Hosting` catalog entry in `Builds props:113`; and that the alignment is registered in `references/Hexalith.Builds/Tools/package-version-exceptions.json`. Keep the *Hosting family* delegation as written. Drop `Aspire.Hosting.AppHost` as the named package.

---

### TECH-U2 — HIGH — "this document names technologies, never their version numbers" is falsified by the document itself, with no stated exception criterion

**Claim (`:1741`):** "***Versions are owned by `references/Hexalith.Builds/Props/Directory.Packages.props` and its `Hexalith*Version` properties — this document names technologies, never their version numbers.***"

**Counterexamples in the same document:** `Octokit 14.0.0` at `:604`, `:690` (A-6), `:1311`, `:1545`, `:1654`; `Redis 7.x` at `:632`, `Redis 7+` at `:633`; `System.CommandLine 2.x` at `:688`, `:1384`; `xUnit v3` at `:81`, `:589`; `OpenAPI 3.1` at `:319`, `:783`, `:1840`; `14.0.0/` as a path node at `:1545`.

**The retentions are not equally defensible, and that is the point:**

- **Octokit `14.0.0` is a genuine, approval-bound constraint and SHOULD stay.** `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDependencyGuardTests.cs:41-46` asserts `packagesProps.ShouldContain("PackageVersion Include=\"Octokit\" Version=\"14.0.0\"", Case.Sensitive)`; `:60` asserts the literal string ``Octokit `14.0.0` `` is present in `docs/contract/provider-compatibility-catalog.md`, which is the **OQ4 catalog approved 2026-09-05 and digest-bound** (per the C12 close-out). `GitHubDriftConformanceTests.cs:47, 75-81, 302-303` compares the provider manifest's `OctokitPackageVersion` against the pinned props value and fails on mismatch (`:136` exercises `"13.0.0"` as the negative). Octokit's version is a contract term, not a current version.
- **`Redis 7.x` is the opposite** — no Redis version is pinned anywhere in this repo (TECH-U9), so it is a number with no referent, and it survived a sweep whose stated purpose was removing exactly that.
- **`System.CommandLine 2.x`, `xUnit v3`, `OpenAPI 3.1`** are major-line/spec identifiers, i.e. genuinely part of the technology's *name*. Fine to keep — but the rule as written forbids them.

**Why it matters:** an absolute rule with five unexplained exceptions is not a rule; it is an invitation for the next sweep to delete the Octokit pin (breaking two conformance tests and desynchronising an approval-bound catalog) or to keep the Redis one (which means nothing). The value of the structural fix depends entirely on the criterion, and the criterion is missing.

**Correction:** replace the absolute with the criterion. Something like: "*This document records a version only when the version is itself a constraint — a contract term pinned by a test or approval (Octokit `14.0.0`, OQ4 catalog, `GitHubDependencyGuardTests`), a major-line/spec identifier (`OpenAPI 3.1`, `xUnit v3`, `System.CommandLine 2.x`), a prerelease/stability status, or a native ABI. Current-version numbers are never copied here; they are owned by `references/Hexalith.Builds/Props/Directory.Packages.props`.*" Then apply it: keep Octokit, delete `Redis 7.x`.

---

### TECH-U3 — HIGH — Two genuine compatibility constraints were lost in the removal and are now un-recordable

Carries **TECH-9** and **TECH-8**; the removal converted both from "stale/missing" to "structurally excluded".

**1. Fluent UI Blazor is on a release candidate, and F-3 rests on it.**
- `:747` (F-3) is unchanged: "***Microsoft Fluent UI Blazor (`Microsoft.FluentUI.AspNetCore.Components`)** — provides accessible primitives, satisfies WCAG 2.2 AA targets (focus-visible, target sizes, dragging exemption confirmed)*".
- `Builds props:226-227` — `Microsoft.FluentUI.AspNetCore.Components` and `.Icons` at **`5.0.0-rc.5-26219.1`**.
- Web (nuget.org, 2026-09-16): highest **stable** `4.14.4`; highest overall = the RC the repo ships. The repo is ahead of stable, on a prerelease.
- The accessibility guarantees in F-3 are version-specific component behaviour. WCAG 2.2 AA is an approved architectural claim enforced by the `accessibility-gates` axe job (`:1762`). Anchoring it to a moving RC, with no statement that it *is* an RC, means the claim has no fixed referent and no GA-upgrade trigger.

**2. `libgit2 1.8.6` is a native ABI with its own CI lane.**
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoSmartHttpGitTransport.cs:30` — "*the pinned LibGit2Sharp 0.32.0 / libgit2 1.8.6 native profile loaded*".
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoReadinessMapper.cs:42` — `["libgit2_version"] = "1.8.6"` is published as **readiness evidence**, i.e. it crosses the provider port into observable behaviour.
- `.github/workflows/nightly-drift.yml:61` runs `tests/tools/run-forgejo-smart-http-alpine-smoke.ps1` — an Alpine/musl smoke lane that exists *because* of the native dependency.
- LibGit2Sharp still has **no decision row** (A-6/A-7 cover the provider API clients; D-8 covers checkout location). It is named only in the `:1741` recital — and, per TECH-U4, that recital points at the wrong file for it.

**Correction:** carve both out of the no-versions rule. F-3: "*`Microsoft.FluentUI.AspNetCore.Components` — the repository currently ships the **v5 release-candidate line** (exact pin in `Directory.Packages.props`); the WCAG 2.2 AA claims are asserted against v5-rc and re-verified by the `accessibility-gates` axe job. Re-validate on v5 GA.*" Add a decision row for LibGit2Sharp recording the **native `libgit2` ABI** and its Alpine/musl smoke consequence, plus the two conformance pins, and note that the NuGet pin lives in the **repo-root** `Directory.Packages.props:15`.

---

### TECH-U4 — MEDIUM — The authority citation is mis-scoped and ambiguous

Three distinct defects in the delegation itself:

1. **Wrong file for LibGit2Sharp.** `Directory.Packages.props:15` **at the repo root** is the sole pin. `:1741` names LibGit2Sharp and then names the Builds file. A reader following the instruction finds nothing.
2. **Scope too narrow.** "*and its `Hexalith*Version` properties*" describes eight properties covering the Hexalith siblings only. NSwag (`Builds props:260`), Octokit (`:264`), ModelContextProtocol (`:248`), System.CommandLine (`:302`), Microsoft.FluentUI (`:226`) and JwtBearer (`:174`) are plain `PackageVersion` items. The sentence names them and then points at a mechanism that does not cover them.
3. **Ambiguous filename.** `:689` (A-5) and `:757` (I-1) cite bare `Directory.Packages.props`. Two files carry that name. It resolves correctly only because root `:11-13` imports Builds — a mechanism the document never explains.

**Correction:** cite `references/Hexalith.Builds/Props/Directory.Packages.props` in full everywhere; say "*`Hexalith*Version` properties for the sibling modules and `PackageVersion` items for third-party packages*"; and add the one exception: "*except `LibGit2Sharp`, pinned locally at repo-root `Directory.Packages.props:15`, and the Aspire AppHost SDK (see I-1).*"

---

### TECH-U5 — MEDIUM — "live-nightly-drift mode" is still asserted; the as-built lane explicitly records live provider evidence as not run

`:331` (C12) — "*provider contract suite runs in hermetic-PR-gate mode **AND live-nightly-drift mode***"; `:691` (A-7) — "*Nightly schema-diff job runs against each pinned upstream tag plus a weekly `HEAD` poll*"; `:787` (Phase 5) — "*hermetic-PR-gate + live-nightly-drift modes (C12)*".

**Evidence:** `run-nightly-drift-gates.ps1:7-8` — `[ValidateSet('pinned-snapshots', 'latest-supported')]`, default `pinned-snapshots`; `nightly-drift.yml:56` passes `'pinned-snapshots'`. Neither profile fetches an upstream tag or polls `HEAD`. `:682` — `Add-Result -Category 'credentialed-live-provider-evidence' -Status 'not_run' -Severity 'informational' -ExitCode 0`, preceded by the comment "*Credentialed live provider evidence is reported as explicitly not run, never as a hardcoded placeholder status standing in for real hermetic drift coverage.*"

The tool is more honest than the architecture document. The amendment gave A-7 an "**as-built**" label for the classifier's identity while leaving the classifier's input asserted at a fidelity the script deliberately disclaims. Since C12 is an approved exit criterion, its measurement method should not over-claim.

**Correction:** label the live mode as target-not-built, mirroring the honesty pattern already applied to I-3 at `:759`: "*as-built: hermetic pinned-snapshot replay (`-ProviderProfile 'pinned-snapshots'`); credentialed live-provider evidence is explicitly `not_run` (`run-nightly-drift-gates.ps1:682`). Target: live upstream-tag diff + weekly `HEAD` poll.*"

---

### TECH-U6 — MEDIUM — The oasdiff correction was not propagated; a published ADR still asserts it

Still naming oasdiff as the live mechanism:
- `docs/adrs/0003-provider-abstraction-and-capability-model.md:23` — "*`C12`: nightly **oasdiff** drift classifies provider schema changes as additive, breaking, or unknown*". An ADR is a decision record; leaving it wrong means the corrected architecture and the published ADR now disagree.
- `docs/runbooks/provider-drift.md:3` — "*detected by the nightly **oasdiff** lane*". This is the **operator-facing** runbook, so an operator responding to a drift alert is sent to a tool that does not exist.
- `docs/runbooks/index.md:13` — same claim in the runbook index.
- `_bmad-output/planning-artifacts/epics.md:327` (`AR-PROVIDER-04`) — "*Nightly **oasdiff** schema-diff job classifies additive (warn) vs breaking (fail)*". **Not flagged by the prior review**; epics.md is co-normative with architecture.md, so this is a fourth live site.

The prior review asked for exactly this lockstep, and the amendment neither did it nor recorded it as owed.

**Correction:** apply the same replacement at all four sites, or record the debt explicitly. `docs/adrs/0003` may need a superseding note rather than an in-place edit, per ADR convention.

---

### TECH-U7 — MEDIUM — Undocumented-technology findings carried forward untouched

Three prior MEDIUMs, verified unchanged at HEAD:

- **TECH-7 residual — the UI/E2E/accessibility stack is still unnamed.** `grep -c "Playwright" architecture.md` → **0**. `Deque.AxeCore` → 0. The `accessibility-gates` claim at `:1762` rests on `tests/Hexalith.Folders.UI.E2E.Tests/…csproj:12` `Microsoft.Playwright` (`Builds props:240`, `1.62.0`) and `:15` `Deque.AxeCore.Playwright` (`:148`, `4.13.0`). *Partial credit:* the amendment **did** close the REAL-1 half — `Hexalith.Folders.EventStore` is now recorded at `:491`, `:1455-1456`, `:1583`, `:1589` and `Hexalith.Folders.EventStore.Tests` at `:503`. Still absent from both trees: `Hexalith.Folders.UI.E2E.Tests`, `Hexalith.Folders.AppHost.Tests`, `Hexalith.Folders.LoadTests.Tests`.
- **TECH-12 unchanged — no serializer decision.** `grep -ci "newtonsoft"` → **0**; `grep -ci "system.text.json"` → **0**. The canonical SDK still generates with `src/Hexalith.Folders.Client/nswag.json:59` `"jsonLibrary": "NewtonsoftJson"` (`Newtonsoft.Json 13.0.4`, `Builds props:263`), a different serializer from the ASP.NET Core 10 server default — material to `ComputeIdempotencyHash()` (A-2/A-9) and the encoding-equivalence corpus.
- **`Microsoft.OpenApi` 2.x ceiling undocumented** (see Part A(d) item 3): a real constraint, pinned at `2.12.0` against a published latest of `3.10.2`, recorded only as a comment in the props file, absent from A-3.

---

### TECH-U8 — MEDIUM — TECH-4 residuals (default mode, missing switch name, lockstep)

See Part B. Summary: `:535`'s "by default, project references" is wrong for `dotnet build -c Release` without `UseNuGetDeps` (`Directory.Build.props:18, 20`); `UseHexalithProjectReferences` — the switch the props file and the CI gate actually use — is never named; `:481` and `:1180` still carry the superseded "*not pinned here*" claim.

---

### TECH-U9 — MEDIUM — TECH-10 and TECH-11 carried forward verbatim; both still contradicted by files in this repo

Neither line changed. Both are stated present-tense and unlabelled, which is the same failure mode the amendment corrected elsewhere.

- **`:632` (D-2)** — "*`Redis 7.x via Aspire`*". `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml` is `type: state.redis` at `localhost:6379`, provisioned by `dapr init`, not by Aspire; `grep -rn "AddRedis" src/ --include=*.cs` → zero hits; no image tag, no `WithImageTag`, no compose file, so "7.x" has no referent. It is also a surviving version number under the new rule (TECH-U2).
- **`:1693`** — "*production policies maintained outside repo per ops runbook*" (and the matching claim in the validation section). `deploy/dapr/production/` is **in this repository** with `accesscontrol.yaml`, `daprsystem.yaml`, `pubsub.yaml`, `secretstore.yaml`, `sidecar-config-bindings.yaml`; `deploy/containers/production/service-images.yaml` binds each service to those policies; the conformance lane (`tests/tools/run-dapr-policy-conformance-gates.ps1`, `.github/workflows/policy-conformance.yml`) is in-repo too. A security reviewer following I-3 is sent to the wrong repository.

---

### TECH-U10 — LOW — Aspire product naming is one major behind

The document says "**.NET Aspire**" at `:78`, `:493`, `:587`, `:757`, `:1489`. Web ([aspire.dev](https://aspire.dev/whats-new/aspire-13/), fetched 2026-09-16): "*The product is now called **Aspire**, dropping its previous `.NET` prefix*", alongside the 9.x → 13.0 version jump that decoupled Aspire from .NET versioning. Cosmetic, but this lens exists to catch names carried from a 2026-05 draft — and the repo is on 13.5.3, well past the rename.

---

### TECH-U11 — LOW — Structure-diagram paths still misdescribe the repo (TECH-13, plus three new instances)

- `:1544-1545` — `github/ └── 14.0.0/openapi-snapshot.json`. `ls -R tests/contracts/github` → a single file, `pinned-profile.json`. **Unchanged from TECH-13.**
- `:1541-1543` — `forgejo/ v15.0/ v14.0/ v13.0/`. Actual: `11.0.14`, `14.0.5`, `15.0.2`, `15.0.7`, `16.0.3` + `supported-versions.json`. **New instance** (the prior review called the Forgejo side accurate on the basis of `supported-versions.json`; the diagram's directory names are in fact wrong).
- `:1557` — `tools/ ├── policy-conformance/`. No such directory; the real artifact is `tests/tools/run-dapr-policy-conformance-gates.ps1`. `tests/tools/pattern-examples/` exists and is absent from the diagram. **New instance.**
- `:1552-1554` — `tests/load/` with `Hexalith.Folders.LoadTests.csproj` + `Scenarios/` is **accurate**, and NBomber (`:1552`) is real (`tests/load/Hexalith.Folders.LoadTests.csproj:9`, pinned `6.6.0` = current stable, with a documented MessagePack CVE override at `:10-11`).

---

### TECH-U12 — LOW — Aspire dashboard URL still wrong (TECH-14, unchanged)

`:1720` — "*Aspire dashboard at `https://localhost:17000`*". `src/Hexalith.Folders.AppHost/Properties/launchSettings.json:8` — `"applicationUrl": "https://localhost:17217;http://localhost:15437"`. Point at `launchSettings.json` rather than hard-coding a port.

---

## Currency verification table — re-checked 2026-09-16

Live `api.nuget.org` flat-container fetches and the `dotnet/core` releases index. **Confirms the prior gate's conclusion:** repo currency is good; the gap is doc-vs-repo.

| Technology | Repo pin | Latest published (2026-09-16) | Currency | Doc status |
| --- | --- | --- | --- | --- |
| .NET SDK | `global.json` `10.0.401` (local `dotnet --version` = 10.0.401) | 10.0 channel: runtime `10.0.12`, SDK `10.0.401`, released 2026-09-08, `active` | **current** | OK |
| .NET TFM | `Directory.Build.props:31` `net10.0` | .NET 11 is `11.0.0-rc.1` / `go-live` | **correct to stay on 10** | OK |
| Aspire hosting family | Builds props `:113-121` `13.5.3` | `13.5.4` (1 patch back) | good | **TECH-U1** (wrong package named; wrong authority) |
| Aspire AppHost SDK | `AppHost.csproj:1` `Aspire.AppHost.Sdk/13.5.3` | `13.5.4` | good | **TECH-U1** (not in cited authority) |
| CommunityToolkit.Aspire.Hosting.Dapr | Builds props `:136` `13.5.1-beta.752` | `13.5.1-beta.757` | good (beta line) | OK (version delegated) |
| Hexalith.EventStore | Builds props `:8` `3.104.0` | `3.105.0` (1 minor back) | good | **TECH-3 closed** ✅ |
| Hexalith.Tenants | Builds props `:11` `5.7.0` | `5.7.0` | **exact** | **TECH-3 closed** ✅ |
| ModelContextProtocol | Builds props `:248, :250` `2.2.0` | `2.2.0` | **exact** | **TECH-1 closed** ✅ |
| Octokit | Builds props `:264` `14.0.0` | `14.0.0` | **exact** | retained ×5 — **TECH-U2** |
| LibGit2Sharp | **root** `Directory.Packages.props:15` `0.32.0` | `0.32.0` | **exact** | **TECH-U3 / TECH-U4** |
| libgit2 (native) | `ForgejoSmartHttpGitTransport.cs:30` `1.8.6` | native ABI, not a NuGet version | n/a | **TECH-U3** (unrecordable under new rule) |
| NSwag.MSBuild | Builds props `:260` `14.7.1` | `14.7.1` | **exact** | OK |
| xunit.v3 | Builds props `:319-321` `4.0.1` | `4.0.1` | **exact** | OK |
| Dapr.* | Builds props `:139-146` `1.18.7` | `1.18.7` stable (`1.19.0-preview.2` exists) | **exact** | OK |
| System.CommandLine | Builds props `:302` `2.0.12` | `2.0.12` stable; `3.0.0-rc.1.26425.128` exists | current | OK — **TECH-15 watch still stands** |
| Microsoft.FluentUI.AspNetCore.Components | Builds props `:226-227` `5.0.0-rc.5-26219.1` | stable `4.14.4`; overall = that RC | **prerelease** | **TECH-U3** |
| Microsoft.Playwright | Builds props `:240` `1.62.0` | `1.62.0` | **exact** | **TECH-U7** (unnamed) |
| Deque.AxeCore.Playwright | Builds props `:147-148` `4.13.0` | `4.13.0` | **exact** | **TECH-U7** (unnamed) |
| bunit | Builds props `:317` `2.11.3` | `2.11.3` | **exact** | named at `:1531` |
| Newtonsoft.Json | Builds props `:263` `13.0.4` | `13.0.4` stable | **exact** | **TECH-U7** (no decision) |
| Microsoft.OpenApi | Builds props `:239` `2.12.0` | `3.10.2` | **deliberately held** (documented 2.x ceiling) | **TECH-U7** (constraint not in doc) |
| NBomber | Builds props `:253` `6.6.0` | `6.6.0` stable | **exact** | named at `:1552` ✅ |
| Testcontainers | Builds props `:311` `4.15.0` (shared catalog) | `4.15.0` | n/a — **unused in Folders** | **TECH-5 closed** ✅ |
| oasdiff | not present | live upstream project; never adopted here | n/a | **TECH-6 closed in arch** ✅ / **TECH-U6** downstream |
| Forgejo support matrix | `tests/contracts/forgejo/` `11.0.14, 14.0.5, 15.0.2, 15.0.7, 16.0.3` | matrix manifest present | current | diagram wrong — **TECH-U11** |

---

## Prior-finding closure tally

| Prior | Severity | Disposition |
| --- | --- | --- |
| TECH-1 — MCP `1.3.0` ×4 | HIGH | **CLOSED** ✅ numbers removed; A-5 delegates; repo `2.2.0` = latest |
| TECH-2 — Aspire / CT Dapr pins | HIGH | **REOPENED as TECH-U1 (HIGH)** — stale numbers gone, but the authority does not own the AppHost SDK |
| TECH-3 — EventStore/Tenants `3.15.1` | HIGH | **CLOSED** ✅ `Hexalith*Version` properties verified at Builds props `:6-13` |
| TECH-4 — inverted package management | HIGH | **DOWNGRADED → TECH-U8 (MEDIUM)** — prohibition gone, dual mode real + CI-exercised both ways; default-mode description inaccurate, `:481`/`:1180` not in lockstep |
| TECH-5 — Testcontainers | HIGH | **CLOSED** ✅ zero usage confirmed; honestly annotated |
| TECH-6 — oasdiff | HIGH | **DOWNGRADED → TECH-U6 (MEDIUM)** — arch fixed, replacement lane verified real; 4 downstream sites (incl. ADR 0003) still assert it |
| TECH-7 — UI/E2E stack + missing projects | MEDIUM | **PARTIAL → TECH-U7** — `Hexalith.Folders.EventStore` (+`.Tests`) added ✅; Playwright/AxeCore and 3 test projects still absent |
| TECH-8 — LibGit2Sharp / libgit2 | MEDIUM | **PARTIAL → TECH-U3 / TECH-U4** — now named at `:1741`, but no decision row, no native-ABI record, wrong authority file |
| TECH-9 — Fluent UI unversioned / RC | MEDIUM | **NOT ADDRESSED → TECH-U3 (HIGH)** — now structurally unrecordable |
| TECH-10 — `Redis 7.x via Aspire` | MEDIUM | **NOT ADDRESSED → TECH-U9** — `:632` verbatim |
| TECH-11 — prod Dapr policy "outside repo" | MEDIUM | **NOT ADDRESSED → TECH-U9** — `:1693` verbatim |
| TECH-12 — no serializer decision | MEDIUM | **NOT ADDRESSED → TECH-U7** — 0 hits for both serializers |
| TECH-13 — `github/14.0.0/…` path | LOW | **NOT ADDRESSED → TECH-U11** (+3 new instances) |
| TECH-14 — dashboard `:17000` | LOW | **NOT ADDRESSED → TECH-U12** — real port `17217` |
| TECH-15 — System.CommandLine 3.0 watch | LOW (not a defect) | **still accurate**; `3.0.0-rc.1` exists; A-4 should carry an upgrade trigger |

**Totals — prior:** 6 HIGH / 6 MEDIUM / 3 LOW (15).
**Closed outright:** 3 HIGH (TECH-1, TECH-3, TECH-5).
**Downgraded:** 2 HIGH → MEDIUM (TECH-4, TECH-6).
**Reopened/escalated:** TECH-2 → TECH-U1 (HIGH); TECH-9 → TECH-U3 (MEDIUM → HIGH).
**Untouched:** 6 (TECH-9…TECH-14).

**This review:** 3 HIGH / 6 MEDIUM / 3 LOW (12).

---

## Reviewer notes

1. **Keep the approach; fix the two holes.** Deleting copied versions was the right call and it demonstrably worked for the Hexalith siblings, where the `Hexalith*Version` property block is a real, single, checkable owner. The approach needs exactly two amendments: (i) a named exception for what CPM structurally cannot own (the Aspire AppHost SDK resolver version), and (ii) a stated criterion for which versions are *constraints* and therefore stay in the architecture (Octokit's approval-bound pin, Fluent UI's prerelease status, libgit2's native ABI, Microsoft.OpenApi's 2.x ceiling).
2. **The honesty labelling is the amendment's best feature and should be applied wider.** The "**as-built**" / "*the 2026-05 draft named X, which was never adopted*" pattern at `:81`, `:358`, `:491`, `:691` preserves provenance instead of quietly rewriting history — it makes the next reviewer's job much easier. The six untouched findings (`:632`, `:1693`, Newtonsoft, Playwright, `:1545`, `:1720`) are precisely the ones that did not get it.
3. **The remediation was scoped to the validation report's Tier 4 sweep, and it shows.** Per `.memlog.md:82`, Tier 4 covered version pins, oasdiff/Testcontainers, TECH-4, REAL-1 and the coverage section. TECH-9/10/11/12/13/14 were not in that tier and were not deferred with an owner either — they simply fell between the report's tiers and this lens's findings. Recommend routing them explicitly rather than leaving them to rediscovery.
4. **Lockstep remains the recurring failure.** Both `:535`-vs-`:481`/`:1180` and architecture-vs-`docs/adrs/0003` are the same shape: a claim corrected in one place while its copies stay stale. The document already knows this pattern — `:437` warns about exactly it for the C6 enum. The same discipline should apply to technology claims.
5. **Not re-reported from other lenses:** the S-7 vocabulary convergence (CV-1), PD11 triple-keying (CV-2), the requirements-coverage rewrite (CV-3, visibly applied) and PD8 (CV-6) are outside this lens. I take no position on them.

---

## Web sources

- [Aspire 13 — What's new](https://aspire.dev/whats-new/aspire-13/) — product rename to "Aspire"; `Aspire.AppHost.Sdk` supersedes the explicit `Aspire.Hosting.AppHost` package reference
- [Aspire SDK for distributed apps](https://aspire.dev/get-started/aspire-sdk/)
- [Aspire 13.5 — What's new](https://aspire.dev/whats-new/aspire-13-5/)
- [dotnet/core releases-index.json](https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json) — .NET 10.0.12 / SDK 10.0.401 (2026-09-08, `active`); .NET 11.0.0-rc.1 (`go-live`)
- `https://api.nuget.org/v3-flatcontainer/{package}/index.json` — flat-container version lists for all 21 packages in the currency table, fetched 2026-09-16
