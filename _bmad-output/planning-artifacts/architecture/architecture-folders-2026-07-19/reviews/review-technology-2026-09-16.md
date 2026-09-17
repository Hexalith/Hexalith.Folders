# Reviewer Gate — Technology / Currency & Reality-Check Lens

- **Artifact under review:** `_bmad-output/planning-artifacts/architecture.md` (1857 lines, full document; decision IDs `D-`/`A-`/`C-`/`F-`/`S-`)
- **Scope:** full-document technology sweep (the 2026-09-15 predecessor review was scoped to that day's amendment diff only)
- **Repo state:** branch `main`, HEAD `1621358`, working tree as reported at session start
- **Date:** 2026-09-16
- **Reviewer lens:** every named library / framework / version pin / API / generator must be reality-checked against this repository, the live web, or the current starter — never asserted from memory.

## Verdict

**FAIL** — not because any single decision is unimplementable, but because the document's own summary claim is false. Line 1698 asserts *"All technology pins … are coherent and version-compatible."* Of the nine pins it enumerates, **four are wrong against the repository's actual pins** (Aspire, CommunityToolkit Dapr, ModelContextProtocol, Hexalith.EventStore/Tenants), one names a package-management model the repo inverted, and the sentence omits every third-party dependency added since the pins were written (LibGit2Sharp + native libgit2, Playwright, axe-core, bunit, Newtonsoft.Json). Two further technologies the document treats as settled mechanism — **Testcontainers** and **oasdiff** — do not appear anywhere in the Folders solution. A reader using this document as the mechanism authority would specify a build that does not match, and in two places could not build at all.

The document has no *internal* version inconsistency: where a version appears more than once (Aspire 13.4.6 ×3, ModelContextProtocol 1.3.0 ×4, Octokit 14.0.0 ×5) it is stated identically each time. The failure mode is uniformly **staleness against the repo**, not self-contradiction.

**Nothing in this review is asserted from training data.** Every currency claim below is backed by a live `api.nuget.org` flat-container fetch on 2026-09-16 or by a repo file:line. No web call failed; the only "unverified" rows in the verification table are the two marked as such (GitHub REST API behaviour, and the D-3/D-5 escalation options, which have no repo pin to check).

---

## Findings

### TECH-1 — HIGH — ModelContextProtocol C# SDK is pinned at `1.3.0` in four places; the repo builds against `2.2.0`

**Claims:**
- line 591: `7. **MCP server SDK** (ModelContextProtocol C# SDK 1.3.0)`
- line 661 (A-5): `| A-5 | MCP server SDK | **ModelContextProtocol C# SDK 1.3.0** in `Hexalith.Folders.Mcp` wrapping `Hexalith.Folders.Client` | Official SDK, active development, .NET 10 compatible |`
- line 1366: `│   │   ├── Program.cs                          # ModelContextProtocol 1.3.0 server bootstrap`
- line 1698: `… ModelContextProtocol 1.3.0 …`

**Evidence:**
- `references/Hexalith.Builds/Props/Directory.Packages.props:248` — `<PackageVersion Include="ModelContextProtocol" Version="2.2.0" />`; `:250` — `ModelContextProtocol.AspNetCore` `2.2.0`.
- `src/Hexalith.Folders.Mcp/Hexalith.Folders.Mcp.csproj:17` — `<PackageReference Include="ModelContextProtocol" />` (version resolved centrally, i.e. 2.2.0).
- Web, `https://api.nuget.org/v3-flatcontainer/modelcontextprotocol/index.json` (fetched 2026-09-16): version list ends `… 1.4.0, 1.4.1, 2.0.0-preview.1 … 2.0.0, 2.1.0, 2.2.0`. Highest stable = **2.2.0**.

**Why it matters:** `1.3.0` is two majors behind and three releases short of even the 1.x line. The 1.x→2.x boundary is where the C# SDK's hosting/tool-registration surface changed; an implementer specifying A-5 literally would write against an API the solution no longer has. This is the single largest gap between a recorded decision and the build.

**Correction:** set A-5, line 591, line 1366 and line 1698 to `ModelContextProtocol 2.2.0` and record the check date. If A-5's rationale ("active development") was the basis for the choice, it is still true — only the number is stale.

---

### TECH-2 — HIGH — Aspire is pinned at `13.4.6` / CommunityToolkit Dapr at `13.4.0-preview.1.260602-0230`; the repo is on the 13.5.x line

**Claims:**
- line 575: `- Local orchestration: .NET Aspire (`Aspire.Hosting.AppHost` 13.4.6; `CommunityToolkit.Aspire.Hosting.Dapr` 13.4.0-preview.1.260602-0230)`
- line 729 (I-1): `**.NET Aspire AppHost (`Aspire.Hosting.AppHost` 13.4.6 + `CommunityToolkit.Aspire.Hosting.Dapr` 13.4.0-preview.1.260602-0230)**`
- line 1698: `… Aspire 13.4.6; CommunityToolkit.Aspire.Hosting.Dapr 13.4.0-preview.1.260602-0230 …`

**Evidence:**
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1` — `<Project Sdk="Aspire.AppHost.Sdk/13.5.3">`. The AppHost carries **no** `Aspire.Hosting.AppHost` `PackageReference` at all (whole-file read; only `Aspire.Hosting.Redis` and `CommunityToolkit.Aspire.Hosting.Dapr`).
- `references/Hexalith.Builds/Props/Directory.Packages.props:113-121` — the entire `Aspire.Hosting.*` family pinned at `13.5.3` (`Aspire.Hosting`, `Aspire.Hosting.Redis`, `Aspire.Hosting.Testing`, `Aspire.Hosting.Docker`, …), with `Aspire.Hosting.Keycloak` / `Aspire.Hosting.Kubernetes` at `13.5.3-preview.1.26425.3`.
- `references/Hexalith.Builds/Props/Directory.Packages.props:136` — `<PackageVersion Include="CommunityToolkit.Aspire.Hosting.Dapr" Version="13.5.1-beta.752" />`.
- Web, `https://api.nuget.org/v3-flatcontainer/aspire.hosting.apphost/index.json`: latest = **13.5.4**; `13.4.6` exists but is five releases back.
- Web, `https://api.nuget.org/v3-flatcontainer/communitytoolkit.aspire.hosting.dapr/index.json`: highest overall = **13.5.1-beta.757**; the doc's `13.4.0-preview.1.260602-0230` is not in the recent window at all.
- Web (search, aspire.dev / learn.microsoft.com "Aspire SDK for distributed apps"): `Aspire.AppHost.Sdk` automatically includes `Aspire.Hosting.AppHost`, so an explicit package reference "is no longer needed" — the doc's *package-reference* framing is also the pre-13.x idiom, not how this AppHost is wired.

**Related memory note:** the Epic 9/10 DCP-boot blocker was root-caused to an AppHost SDK vs Hosting version mismatch (SDK 13.3.5 vs Hosting 13.4.6). Leaving a stale 13.4.6 in the mechanism authority re-creates exactly that class of mismatch for the next implementer.

**Correction:** replace both pins with `Aspire.AppHost.Sdk 13.5.3` (naming the SDK, not the package) and `CommunityToolkit.Aspire.Hosting.Dapr 13.5.1-beta.752`, and state that the Aspire hosting family version is owned by `references/Hexalith.Builds/Props/Directory.Packages.props`, not by this document.

---

### TECH-3 — HIGH — "Hexalith.EventStore/Tenants 3.15.1 verified on NuGet 2026-05-09" is stale for EventStore and wrong for Tenants

**Claim (line 1698):**
> "**Decision Compatibility:** All technology pins (.NET 10; Hexalith.EventStore/Tenants **3.15.1 verified on NuGet 2026-05-09**; …) are coherent and version-compatible."

**Evidence:**
- `references/Hexalith.Builds/Props/Directory.Packages.props:8` — `<HexalithEventStoreVersion …>3.104.0</HexalithEventStoreVersion>`; `:11` — `<HexalithTenantsVersion …>5.7.0</HexalithTenantsVersion>` (also `:6` Commons `2.30.0`, `:9` FrontComposer `4.4.0`, `:10` Memories `2.27.1`, `:7` PolymorphicSerializations `1.19.2`).
- Web, `https://api.nuget.org/v3-flatcontainer/hexalith.eventstore.contracts/index.json`: highest stable **3.105.0**; both `3.15.1` and `3.104.0` exist.
- Web, `https://api.nuget.org/v3-flatcontainer/hexalith.tenants.contracts/index.json`: highest stable **5.7.0**; `3.15.1` exists historically.

So Tenants is a **whole major line** past the recorded pin, and EventStore is ~89 minors past it. The "verified on NuGet 2026-05-09" stamp is honest about *when* it was checked — which is exactly why it should not still be the current statement four months later.

**Correction:** replace with the two real pins (`Hexalith.EventStore 3.104.0`, `Hexalith.Tenants 5.7.0`, verified 2026-09-16) or, better, delete the numbers from this document and point at `references/Hexalith.Builds/Props/Directory.Packages.props:6-11` as the single owner, so this line cannot drift again.

---

### TECH-4 — HIGH — The package-management decision is inverted: the repo now resolves Hexalith siblings as **NuGet PackageReferences** in Release, and the doc forbids exactly that

**Claims:**
- line 523: "`Hexalith.EventStore.*` and `Hexalith.Tenants.*` are consumed as **project references to the root-level sibling submodule source** … — **not pinned as NuGet packages (verified as-built; do not replace with package references)**."
- line 524: "`Hexalith.Tenants.Client` will use a project reference to the submodule project **until package availability is confirmed** (per Tenants research caveat)."

**Evidence:**
- `Directory.Build.props:9-20` implements an explicit dual mode, documented in its own comment: *"Debug builds use project references to the references/ submodule source; **Release builds use the centrally pinned NuGet packages in Directory.Packages.props**"*, switchable with `-p:UseHexalithProjectReferences=true|false`.
- Every consuming csproj carries the conditional pair, e.g. `src/Hexalith.Folders.Server/Hexalith.Folders.Server.csproj:22-28` — `<PackageReference Include="Hexalith.EventStore.Client" Condition="'$(HexalithEventStoreFromSource)' != 'true'" />`, `…Tenants.Client`, `…Tenants.Contracts`; same shape in `Workers`, `UI`, `AppHost`.
- `.github/workflows/release-packages.yml:131` / `:134` / `:180` / `:183` — the release lane restores and builds with `-p:Configuration=Release -p:UseNuGetDeps=true`, i.e. the package path is a **shipped, CI-exercised** path, not a hypothetical.
- The "until package availability is confirmed" caveat is moot: `Hexalith.Tenants.Contracts` 5.7.0 is published (web check above), and `Hexalith.Tenants.Client` is pinned centrally at `$(HexalithTenantsVersion)`.

**Why it matters:** "do not replace with package references" reads as a prohibition. An agent enforcing it would strip the Release path and break `release-packages.yml`. This is the one finding where following the document as written breaks a green CI lane.

**Correction:** rewrite line 523 to describe the dual-mode resolution (Debug = submodule project references via `HexalithEventStoreRoot`/`HexalithTenantsRoot`; Release/`UseNuGetDeps=true` = centrally pinned packages), cite `Directory.Build.props:9-20` and `release-packages.yml:131-134`, and delete line 524's obsolete caveat.

---

### TECH-5 — HIGH — Testcontainers is declared part of the mandated testing stack in three places and is used nowhere in this solution

**Claims:**
- line 80 (under **Ecosystem-imposed (non-negotiable)**): `- xUnit v3, Shouldly, NSubstitute, Testcontainers for testing`
- line 547: `- xUnit v3 + Shouldly + NSubstitute + Testcontainers + Aspire test host (sibling-module convention).`
- line 577: `- Testing: xUnit v3 + Shouldly + NSubstitute + Testcontainers + Aspire test host`

**Evidence:**
- `grep -rn "Testcontainers" --include=*.csproj --include=*.cs --include=*.props .` over the repo returns **zero hits outside `references/`**. The only matches are in the `references/Hexalith.Memories` submodule (`tests/Hexalith.Memories.IntegrationTests/…`), i.e. a sibling module's tests, not Folders.
- `references/Hexalith.Builds/Props/Directory.Packages.props:311` does pin `Testcontainers 4.15.0`, but no Folders project consumes it — central pinning is shared across all Hexalith repos and is not evidence of adoption.
- The actual integration/E2E substrate is `Aspire.Hosting.Testing` (13.5.3), plus `tests/Hexalith.Folders.IntegrationTests` and `tests/Hexalith.Folders.AppHost.Tests`.

**Why it matters:** line 80 sits in the *non-negotiable ecosystem constraints* list. It implies a container-per-dependency integration strategy that was never built, and it is the kind of claim that gets cited to justify work ("we already decided on Testcontainers") that nothing supports. It reads as inherited from sibling-module convention rather than checked.

**Correction:** remove Testcontainers from lines 80/547/577, or demote it to "available via the shared `Directory.Packages.props` pin; **not currently adopted by Folders** — the integration substrate is `Aspire.Hosting.Testing`." Replace it in the stack sentence with the technologies actually in use (see TECH-7).

---

### TECH-6 — HIGH — `oasdiff` is named as the provider-drift classifier in six places, including a C12 measurement method and a structure path; it exists nowhere in the repo

**Claims:**
- line 351 (C12 exit-criteria row, **measurement method**): `… | **oasdiff classifier**; fixture-to-failure-mode coverage matrix |`
- line 755 (Phase 5): "Forgejo adapter (typed HttpClient + per-version `swagger.v1.json` contract tests with **oasdiff** drift detection)"
- line 1043: "nightly **oasdiff** drift job classifies additive (warn) vs breaking (fail)"
- line 1516 (structure diagram): `│       ├── oasdiff/                            # config for nightly Forgejo schema-diff job`
- lines 1714, 1799 — same claim restated in the validation section.

**Evidence:**
- `ls tests/tools/oasdiff` → `No such file or directory`.
- Repo-wide `grep -rn -i "oasdiff"` excluding `references/`, `obj/`, `bin/` and `_bmad-output/` returns **only prose**: `docs/runbooks/index.md:13`, `docs/runbooks/provider-drift.md:3`, `docs/adrs/0003-provider-abstraction-and-capability-model.md:23` — all downstream documents inheriting this document's claim. No binary, no container image, no GitHub Action, no config file, no script invocation.
- The real lane: `.github/workflows/nightly-drift.yml:56` runs `./tests/tools/run-nightly-drift-gates.ps1 -SkipRestoreBuild -ProviderProfile 'pinned-snapshots'`, and the classifier assets live at `tests/tools/forgejo-drift/` (`Write-SanitizedForgejoDriftReport.ps1`, `classification-fixtures.json`).
- oasdiff itself is a live, maintained project — this is **not** a dead-technology finding. It simply was never adopted, and the document says it was.

**Why it matters:** C12 is an approved exit criterion (catalog `1.0.0` landed 2026-09-15). Its recorded *measurement method* names a tool that does not run. That makes the criterion unauditable as written, and the three downstream docs have already propagated the error.

**Correction:** replace "oasdiff" throughout with the in-repo classifier (`tests/tools/run-nightly-drift-gates.ps1` + `tests/tools/forgejo-drift/classification-fixtures.json`), delete the `oasdiff/` node from the structure diagram at line 1516, and note the three downstream documents that need the same correction in lockstep.

---

### TECH-7 — MEDIUM — The UI/E2E/accessibility stack the document relies on is never named, and four real projects are absent from the inventory

**Claim (line 1717):** "**enforced by the automated axe / WCAG 2.2 AA CI gate (`accessibility-gates`, Story 8.4) over the three console journeys**"
**Claim (lines 465-500 and 1134-1527):** the recommended layout and the "Complete Project Directory Structure" list `tests/` as nine projects ending at `Hexalith.Folders.IntegrationTests` (lines 493-494, 1493-1494).

**Evidence:**
- The gate line 1717 leans on is implemented by technologies the document never names: `tests/Hexalith.Folders.UI.E2E.Tests/Hexalith.Folders.UI.E2E.Tests.csproj:12` `Microsoft.Playwright` (pinned `1.62.0`, Builds props `:240`) and `:15` `Deque.AxeCore.Playwright` (pinned `4.13.0`, Builds props `:148`); component tests use `bunit 2.11.3` (Builds props `:317`). Grep for `playwright|axe-core|axecore` across architecture.md returns **no hits** — only the word "axe" inside line 1717's gate name.
- Real `tests/` inventory includes four projects the document does not list: `Hexalith.Folders.UI.E2E.Tests`, `Hexalith.Folders.AppHost.Tests`, `Hexalith.Folders.EventStore.Tests`, `Hexalith.Folders.LoadTests.Tests`.
- Real `src/` includes `Hexalith.Folders.EventStore` (referenced from `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:13` and composed at `AppHost/Program.cs:22` as `Projects.Hexalith_Folders_EventStore`). `grep -n "Hexalith.Folders.EventStore" architecture.md` returns **zero hits** — a first-class AppHost resource project is invisible to the mechanism authority.

**Correction:** add Playwright + Deque.AxeCore (+ bunit) to the testing-stack sentences at 547/577 and to the I-5 gate list at 733; add the four test projects and `src/Hexalith.Folders.EventStore` to the layout and structure sections.

---

### TECH-8 — MEDIUM — `LibGit2Sharp` (and its native `libgit2`) is a load-bearing, CI-pinned dependency with no recorded decision

**Claim (line 1698):** "**All technology pins** (…) are coherent and version-compatible" — the enumeration contains no Git library. A-6 (line 662) and A-7 (line 663) record the *provider API* clients (Octokit, typed HttpClient) and D-8 (line 626) records *where* checkouts live, but nothing records *how* Git itself is spoken.

**Evidence:**
- `Directory.Packages.props:15` (repo root) — `<PackageVersion Include="LibGit2Sharp" Version="0.32.0" />`. This is the **only** package the Folders repo pins locally rather than inheriting; that alone marks it as a Folders-specific decision.
- `src/Hexalith.Folders/Hexalith.Folders.csproj:17` — `<PackageReference Include="LibGit2Sharp" />`; consumed by `src/Hexalith.Folders/Providers/Forgejo/ForgejoSmartHttpGitTransport.cs` and `ForgejoHttpApiClient.cs`.
- `src/Hexalith.Folders/Providers/Forgejo/ForgejoSmartHttpGitTransport.cs:30` documents a **native** profile: *"the pinned LibGit2Sharp 0.32.0 / libgit2 1.8.6 native profile loaded"* — i.e. a native-binary dependency with an Alpine smoke lane (`.github/workflows/nightly-drift.yml:61` → `tests/tools/run-forgejo-smart-http-alpine-smoke.ps1`).
- Two conformance tests hard-pin it: `tests/Hexalith.Folders.Tests/Providers/Forgejo/ForgejoDependencyGuardTests.cs:98-99` and `tests/Hexalith.Folders.Tests/Providers/GitHub/GitHubDriftConformanceTests.cs:27,76-82`.
- Currency is **fine**: web, `https://api.nuget.org/v3-flatcontainer/libgit2sharp/index.json` — `0.32.0` is the latest release. The defect is the missing decision, not the version.

**Correction:** add a decision row (e.g. `A-12` or a `D-` row beside D-8) recording LibGit2Sharp `0.32.0` / libgit2 `1.8.6`, its native-runtime consequence (Alpine/musl smoke lane), the two CI pins that enforce it, and the alternatives rejected (shelling out to `git`, a managed smart-HTTP implementation). Add it to the line 1698 enumeration.

---

### TECH-9 — MEDIUM — F-3 leaves Fluent UI Blazor unversioned; the shipped pin is a **release candidate**, and the stable line is a major behind

**Claim (line 719, F-3):** "**Microsoft Fluent UI Blazor (`Microsoft.FluentUI.AspNetCore.Components`)** — provides accessible primitives, **satisfies WCAG 2.2 AA targets (focus-visible, target sizes, dragging exemption confirmed)**"

**Evidence:**
- `references/Hexalith.Builds/Props/Directory.Packages.props:226-227` — `Microsoft.FluentUI.AspNetCore.Components` and `.Icons` pinned at **`5.0.0-rc.5-26219.1`** (a prerelease).
- Consumed for real: `src/Hexalith.Folders.UI/Components/App.razor:7` loads `_content/Microsoft.FluentUI.AspNetCore.Components/...bundle.scp.css`; many components `@using Microsoft.FluentUI.AspNetCore.Components`.
- Web, `https://api.nuget.org/v3-flatcontainer/microsoft.fluentui.aspnetcore.components/index.json`: highest **stable** = `4.14.4`; highest overall = `5.0.0-rc.5-26219.1`. The repo is on the newest RC, i.e. ahead of stable and on a prerelease.
- `mcp__fluent-ui-blazor__get_version_info` (Fluent UI Blazor MCP server, 2026-09-16) states its documentation set targets component-library version **`5.0.0.26180`** and that a project "must reference the same version" for the docs to be accurate — confirming v5 is the live line but also that the RC's surface is still moving.

**Why it matters:** the accessibility guarantees in F-3 ("dragging exemption confirmed") are version-specific component behaviour. Attaching them to an unnamed version, when the shipped version is an RC whose API and ARIA output can still change before GA, means the WCAG 2.2 AA claim has no fixed referent — and the axe gate (line 1717) is the only thing actually holding it.

**Correction:** state the pin and its prerelease status in F-3: "`Microsoft.FluentUI.AspNetCore.Components 5.0.0-rc.5-26219.1` (**prerelease**; stable line is 4.14.x)", note that the WCAG claims are asserted against v5-rc and re-verified by the `accessibility-gates` axe job, and add a GA-upgrade trigger.

---

### TECH-10 — MEDIUM — D-2 "Redis 7.x via Aspire" describes a provisioning path the AppHost does not use, and no Redis version is pinned anywhere

**Claim (line 620, D-2):** `| D-2 | Dapr state-store backend (local) | **Redis 7.x via Aspire** | Mirrors Tenants AppHost; required for shared state across EventStore + Tenants + Folders sidecars |`

**Evidence:**
- `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml` — `type: state.redis`, `redisHost: "localhost:6379"`, with an in-file comment stating the reason: *"DAPR does not support the `{env:VAR|default}` template form, so an unset REDIS_HOST left the literal string as the hostname and daprd failed with 'no such host'. **`dapr init` provisions Redis at localhost:6379** with no password."* The local state store is therefore the `dapr init` sidecar Redis, **not** an Aspire-managed resource.
- `grep -rn "AddRedis" src/ --include=*.cs` (excluding `obj/`,`bin/`) returns **zero hits**. `Aspire.Hosting.Redis` is referenced by the AppHost csproj but never invoked for the state store; the only Aspire-managed Redis in the topology is the `memories-vectors` container created inside `AddHexalithMemoriesSearchIndexServer` (see the comment at `AppHost/Program.cs:65`).
- No "7.x" pin exists: no image tag, no `WithImageTag`, no compose file. Production (`deploy/dapr/production/pubsub.yaml:9`) declares `type: pubsub.redis` with the version owned by deployment tooling.

**Correction:** restate D-2 as "Redis provided by the local `dapr init` sidecar at `localhost:6379` (component: `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml`); Aspire manages only the Memories `memories-vectors` Redis container. **No Redis major version is pinned by this repository** — if 7.x is a real requirement it needs a pin and a check, not a prose assertion."

---

### TECH-11 — MEDIUM — Production Dapr policy is said to live "outside repo" / "in a separate ops repository"; it is checked in

**Claims:**
- line 1650: "Dapr components in `src/Hexalith.Folders.AppHost/DaprComponents/` for local; **production policies maintained outside repo per ops runbook**"
- line 1691: "**Production Dapr access-control YAML maintained in a separate ops repository**, validated by `dapr-policy-conformance` job before promotion (per I-3 + M4)"

**Evidence:**
- `deploy/dapr/production/` exists in this repository and contains `accesscontrol.yaml`, `daprsystem.yaml`, `pubsub.yaml`, `secretstore.yaml`, `sidecar-config-bindings.yaml`.
- `deploy/containers/production/service-images.yaml` binds each service to its `daprConfig` (`hexalith-folders-production-accesscontrol-folders`, `…-folders-workers`, `…-folders-ui`), i.e. the in-repo policy is the one referenced by the deployment binding artifact.
- The conformance lane is in-repo too: `tests/tools/run-dapr-policy-conformance-gates.ps1`, `.github/workflows/policy-conformance.yml`.

**Why it matters:** I-3's deny-by-default rule and the S-4/Query-Facade egress allow-rule are cited throughout as the operative network control. Telling a reader they live in another repository sends them looking in the wrong place for the file a security review must read.

**Correction:** point both lines at `deploy/dapr/production/` and `deploy/containers/production/service-images.yaml`, keeping whatever *registry/tag/overlay* ownership genuinely sits with deployment tooling (which `service-images.yaml:2` already states: "Deployment tooling owns registry selection, image tags, digests, pull secrets, and namespace overlays").

---

### TECH-12 — MEDIUM — No JSON serializer decision exists, and the generated canonical SDK uses Newtonsoft.Json rather than System.Text.Json

**Evidence:**
- `grep -i "newtonsoft" architecture.md` → **0 hits**; `grep -i "System.Text.Json" architecture.md` → **0 hits**. The document specifies JSON *conventions* (camelCase properties at line 824, ULID formats at 837-840, RFC 9457 problem shapes at A-8) but never names the serializer that implements them.
- `src/Hexalith.Folders.Client/nswag.json:59` — `"jsonLibrary": "NewtonsoftJson"`.
- `src/Hexalith.Folders.Client/Hexalith.Folders.Client.csproj:16` — `<PackageReference Include="Newtonsoft.Json" />`, pinned at `13.0.4` (`references/Hexalith.Builds/Props/Directory.Packages.props:263`).

**Why it matters:** the canonical typed SDK — the artifact A-2 makes authoritative for every non-REST surface — serializes with a different library from the ASP.NET Core 10 server default. That difference is exactly the kind of thing the parity dimensions catalog (concern #8) and the idempotency-hash helper (`ComputeIdempotencyHash()`, A-2/A-9) are sensitive to: property ordering, `null` handling, numeric and date formats, and Unicode escaping all differ between the two stacks, and the encoding-equivalence corpus at line 773 assumes one canonical encoding.

**Correction:** add a decision row recording `Newtonsoft.Json 13.0.4` as the generated-client serializer (per `nswag.json:59`), state whether the server side is System.Text.Json, and say explicitly which one defines canonical form for `ComputeIdempotencyHash()` and for the RFC 9457 wire shape.

---

### TECH-13 — LOW — GitHub contract-snapshot path in the structure diagram does not exist

**Claim (lines 1504-1505):**
```
│   │   └── github/                             # Octokit-version snapshots for regression
│   │       └── 14.0.0/openapi-snapshot.json
```
**Evidence:** `ls -R tests/contracts/github` → a single file, `pinned-profile.json`. There is no `14.0.0/` directory and no `openapi-snapshot.json`. (By contrast the Forgejo side is accurate: `tests/contracts/forgejo/{11.0.14,14.0.5,15.0.2,15.0.7,16.0.3}/` + `supported-versions.json`.)

**Correction:** replace the two lines with `└── pinned-profile.json` and describe what it actually pins.

---

### TECH-14 — LOW — Aspire dashboard URL is wrong

**Claim (line 1677):** "Aspire dashboard at `https://localhost:17000` exposes service health, logs, traces, metrics"
**Evidence:** `src/Hexalith.Folders.AppHost/Properties/launchSettings.json:8` — `"applicationUrl": "https://localhost:17217;http://localhost:15437"` (http profile at `:21` is `http://localhost:15437`).
**Correction:** use `https://localhost:17217` (or point at `launchSettings.json` rather than hard-coding a port).

---

### TECH-15 — LOW — `System.CommandLine 2.x` is correct today; flag 3.0 as a watch item

**Claim (line 660, A-4):** "**System.CommandLine 2.x** (Microsoft, supports .NET 10, hierarchical commands, JSON output)"
**Evidence:** `references/Hexalith.Builds/Props/Directory.Packages.props:302` — `2.0.12`; `src/Hexalith.Folders.Cli/Hexalith.Folders.Cli.csproj:18` consumes it. Web, `https://api.nuget.org/v3-flatcontainer/system.commandline/index.json`: `2.0.12` is the highest **stable**; `3.0.0-rc.1.26425.128` and eight 3.0.0 previews now exist.
**This is not a defect** — the doc is accurate. Recorded only so the next review does not re-derive it: a 3.0 GA will be a breaking API change for `Hexalith.Folders.Cli` and A-4 should carry an upgrade trigger.

---

## Verification table

| Technology | Doc says | Repo / web reality | Status |
| --- | --- | --- | --- |
| .NET / TFM | `.NET 10`, `net10.0` | `Directory.Build.props:31` `net10.0` | **repo-verified — OK** |
| .NET SDK | ".NET 10 SDK pin" (line 1148) | `global.json` `10.0.401`; web: 10.0.401 released 2026-09-08, current | **repo + web-verified — OK** |
| C# language | `LangVersion=latest`, nullable, implicit usings, warnings-as-errors | `Directory.Build.props:32-35` — all four present | **repo-verified — OK** |
| Solution format | `.slnx` | `Hexalith.Folders.slnx` | **repo-verified — OK** |
| Aspire AppHost | `Aspire.Hosting.AppHost 13.4.6` | `AppHost.csproj:1` `Aspire.AppHost.Sdk/13.5.3`; family 13.5.3; web latest 13.5.4 | **TECH-2 (HIGH)** |
| CommunityToolkit.Aspire.Hosting.Dapr | `13.4.0-preview.1.260602-0230` | Builds props `:136` `13.5.1-beta.752`; web latest `13.5.1-beta.757` | **TECH-2 (HIGH)** |
| Aspire.Hosting.Testing | "Aspire test host" | Builds props `:120` `13.5.3` | **repo-verified — OK** |
| Dapr SDK | "Dapr sidecars" (no version) | Builds props `:139-146` `Dapr.* 1.18.7`; web latest stable 1.18.7 | **repo + web-verified — OK** |
| Hexalith.EventStore | `3.15.1` (line 1698) | Builds props `:8` `3.104.0`; web latest `3.105.0` | **TECH-3 (HIGH)** |
| Hexalith.Tenants | `3.15.1` (line 1698) | Builds props `:11` `5.7.0`; web latest `5.7.0` | **TECH-3 (HIGH)** |
| Hexalith.Commons / Memories / FrontComposer / PolymorphicSerializations | unversioned | `2.30.0` / `2.27.1` / `4.4.0` / `1.19.2` (Builds props `:6-11`) | **repo-verified — OK (no doc claim)** |
| Hexalith sibling consumption model | "project references … not pinned as NuGet packages; do not replace" | `Directory.Build.props:9-20` dual mode; `release-packages.yml:131-134` `UseNuGetDeps=true` | **TECH-4 (HIGH)** |
| ModelContextProtocol C# SDK | `1.3.0` (×4) | Builds props `:248` `2.2.0`; web latest stable `2.2.0` | **TECH-1 (HIGH)** |
| Octokit | `14.0.0` (×5) | Builds props `:264` `14.0.0`; web latest `14.0.0`; pinned by `GitHubDependencyGuardTests.cs:46` | **repo + web-verified — OK** |
| LibGit2Sharp / libgit2 | *not mentioned* | root `Directory.Packages.props:15` `0.32.0`; `ForgejoSmartHttpGitTransport.cs:30` libgit2 `1.8.6`; web latest `0.32.0` | **TECH-8 (MEDIUM — undocumented)** |
| Forgejo (provider) | per-version `swagger.v1.json` keyed by semver | `tests/contracts/forgejo/supported-versions.json` → `16.0.3` latest-stable + `15.0.7` LTS; web: both released 2026-08-20, current | **repo + web-verified — OK** |
| oasdiff | drift classifier + `tests/tools/oasdiff/` | absent repo-wide; real lane `run-nightly-drift-gates.ps1` + `tests/tools/forgejo-drift/` | **TECH-6 (HIGH)** |
| NSwag | "NSwag" SDK generator | `NSwag.MSBuild` Builds props `:260` `14.7.1` (web latest `14.7.1`); `Client.csproj` `NSwagExe_Net100` target | **repo + web-verified — OK** |
| Newtonsoft.Json | *not mentioned* | `Client/nswag.json:59` `"jsonLibrary": "NewtonsoftJson"`; Builds props `:263` `13.0.4` | **TECH-12 (MEDIUM — undocumented)** |
| System.CommandLine | `2.x` | Builds props `:302` `2.0.12`; web: latest stable `2.0.12`, `3.0.0-rc.1` exists | **repo + web-verified — OK (TECH-15 watch)** |
| Microsoft.FluentUI.AspNetCore.Components | unversioned, WCAG claims | Builds props `:226` `5.0.0-rc.5-26219.1` (**prerelease**); web: stable line `4.14.4`; MCP server targets `5.0.0.26180` | **TECH-9 (MEDIUM)** |
| Blazor Web App / Interactive Server | F-1/F-2 | `src/Hexalith.Folders.UI` + `FrontComposer.Shell` reference | **repo-verified — OK** |
| bunit | "bUnit Blazor component tests" (line 1491) | Builds props `:317` `2.11.3` | **repo-verified — OK** |
| Microsoft.Playwright | *not mentioned* | `UI.E2E.Tests.csproj:12`; Builds props `:240` `1.62.0` | **TECH-7 (MEDIUM — undocumented)** |
| Deque.AxeCore.Playwright | "axe / WCAG gate" only by gate name | `UI.E2E.Tests.csproj:15`; Builds props `:147-148` `4.13.0` | **TECH-7 (MEDIUM — undocumented)** |
| xUnit | `xUnit v3` | Builds props `:319-321` `xunit.v3 4.0.1`; web latest stable `4.0.1` | **repo + web-verified — OK** |
| Shouldly | named | Builds props `:294` `4.3.0` | **repo-verified — OK** |
| NSubstitute | named | Builds props `:259` `6.2.0` | **repo-verified — OK** |
| Testcontainers | mandated stack (×3) | **zero usage** in Folders; only `references/Hexalith.Memories` | **TECH-5 (HIGH)** |
| Microsoft.NET.Test.Sdk / coverlet | *not mentioned* | Builds props `:237` `18.10.0`, `:138` `coverlet.collector 10.0.1` | **repo-verified — no doc claim** |
| Microsoft.AspNetCore.Authentication.JwtBearer | S-2 frozen params | Builds props `:174` `10.0.12`; `Server.csproj:14`; `Authentication/FoldersAuth*.cs` | **repo-verified — OK** |
| Microsoft.AspNetCore.OpenApi | A-3 server-emitted OpenAPI | Builds props `:190` `10.0.12` (+ deliberate `Microsoft.OpenApi` 2.x floor comment at `:238`) | **repo-verified — OK** |
| OpenAPI version | `3.1` (C0/A-1) | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml:1` `openapi: 3.1.0` | **repo-verified — OK** |
| RFC 9457 Problem Details | A-8 | `ProblemDetailsTypes.cs` present; RFC 9457 is the current Problem Details RFC (obsoletes 7807) | **repo-verified — OK** |
| OpenTelemetry | I-6 OTLP, unversioned | Builds props `:266-275` `1.18.0`; `ServiceDefaults.csproj:10-14`, `Extensions.cs` | **repo-verified — OK** |
| Keycloak (local IDP) | S-1, "optional Keycloak" | `Aspire.Hosting.Keycloak 13.5.3-preview.1.26425.3` flows via `AddHexalithEventStoreSecurity()` (`AppHost/Program.cs:17`; AppHost `deps.json`) | **repo-verified — OK** |
| Redis (Dapr state/pubsub) | "Redis 7.x via Aspire" | `DaprComponents/statestore.yaml` → `dapr init` Redis `localhost:6379`; no `AddRedis`; no version pin | **TECH-10 (MEDIUM)** |
| Production Dapr policy location | "separate ops repository" | `deploy/dapr/production/*.yaml` in-repo | **TECH-11 (MEDIUM)** |
| GitHub Actions | I-5 | `.github/workflows/{ci,contract-spine,nightly-drift,policy-conformance,release-packages}.yml`; `actions/checkout@v6`, `actions/setup-dotnet@v5` | **repo-verified — OK** |
| WCAG 2.2 AA | accessibility target | current W3C Recommendation; enforced by `accessibility-gates` axe job | **web-verified — OK** |
| GitHub REST API / GitHub Apps | A-6 capability claims | not independently exercised this run; covered indirectly by Octokit 14.0.0 being current | **unverified (indirect only)** |
| Azure Cache for Redis / Service Bus / RabbitMQ / PostgreSQL (D-3/D-5 escalation paths) | named as escalation options | no repo artifact exercises any of them; all are live products | **unverified (deferred options, no pin to check)** |

---

## Reviewer notes

1. **One root cause explains TECH-1 through TECH-3.** The version block at line 1698 was written once (stamped "verified on NuGet 2026-05-09") and has been carried forward through every amendment without re-checking, while `references/Hexalith.Builds/Props/Directory.Packages.props` moved underneath it. The durable fix is to stop copying version numbers into this document and instead cite the Builds props file as the owner, keeping only *choices* (which library) here and leaving *versions* (which release) to the pin file plus the existing dependency-guard tests.
2. **TECH-5 and TECH-6 are the same failure in a different register:** a technology name inherited from sibling-module convention or from the original research report, restated as a settled decision, never checked against `grep`. Both have now propagated — oasdiff into three `docs/` files, Testcontainers into the non-negotiable constraints list.
3. **The document is internally consistent on versions.** Every technology that appears more than once appears at the same version each time. There is no split-pin finding to report; the corrections above can therefore be applied mechanically (one version, N sites) without adjudicating between two competing numbers.
4. **Currency is genuinely good where the repo is the source of truth.** Octokit, LibGit2Sharp, NSwag, xunit.v3, Dapr, System.CommandLine, the .NET SDK and the Forgejo support matrix are all at or within one release of the current published version as of 2026-09-16 — this repository is well maintained. The gap is between the repository and the document, not between the repository and the world.
5. **Not re-reported from the 2026-09-15 gate:** F1 (manifest `execution_rank`) is visibly still open — line 206 now reads "regeneration owed (§5.5); still `generated_on: '2026-08-04'`", which is the honest restatement that review asked for, so it is **fixed as documented**. F8 (stray blank line splitting the S-table) — I did not re-verify the table rendering this run and take no position. F2–F7, F9 are contract/lifecycle findings outside this lens.
