# Story 11.1 — Refactor Baseline And Governance Pin Map

- **Story:** 11.1 Establish refactor baseline and governance pin map (chore; documentation/evidence only)
- **Generated:** 2026-07-07
- **Baseline HEAD:** `5ce25e47dee8f7e63e7dbe3bcfc3b2e5d777b0b4`
- **Audit seed reconciled against:** `fable_Folders_changes.md` @ `533806b`
- **Diagnostic policy:** metadata-only (no secrets, tokens, file contents, diffs, provider payloads, or local absolute paths; repo-relative paths only)
- **Scope guard:** no product-code, generated-client, parity-artifact, or `references/` submodule edits; no commit; no recursive/nested submodule init.

---

## 1. HEAD / branch / tree state

| Item | Value |
| --- | --- |
| HEAD | `5ce25e47dee8f7e63e7dbe3bcfc3b2e5d777b0b4` |
| Branch | `main` (tracking `origin/main`, not ahead/behind at fetch time) |
| Working tree | Clean except one pre-existing modification: `_bmad-output/implementation-artifacts/spec-11-1-establish-refactor-baseline-and-governance-pin-map.md` (the Story 11.1 SPEC, orchestrator-owned — **not** product code). No `src/**` or generated-artifact changes exist. |
| Untracked litter | None. Standalone release-package packing wrote `.nupkg`/`.snupkg` under `_bmad-output/gates/release-packages/packages/`, which is git-ignored (verified via `git check-ignore`). |

Command: `git rev-parse HEAD && git status --short --branch && git submodule status` — captured exactly (Section 3).

---

## 2. Audit drift vs seed `533806b`

The audit seed `fable_Folders_changes.md` was written at `533806b`. Current HEAD `5ce25e4` is **4 commits ahead**:

| Commit | Subject | Nature |
| --- | --- | --- |
| `d50b603` | Refactor code structure for improved readability and maintainability | pre-Epic-11 refactor; structural claims re-verified at HEAD (§2 note) |
| `8edc88c` | Update adapter name to "codex" and add Epic 11 context documentation | Epic 11 planning |
| `a63aba8` | Update status from blocked to ready-for-dev in Epic 11 context documentation | Epic 11 planning |
| `5ce25e4` | chore: update Hexalith.EventStore submodule reference and add baseline artifact for Epic 11 | Epic 11 planning + submodule bump |

**Submodule pointer drift `533806b..HEAD` (unrelated changes — NOT reverted, NOT hidden):**

| Submodule | `533806b` | HEAD `5ce25e4` |
| --- | --- | --- |
| `references/Hexalith.EventStore` | `a592bbd` | `c0fe028` |
| `references/Hexalith.Memories` | `c8ff40e` | `1b1db8d` |
| `references/Hexalith.Tenants` | `dde9c45` | `9c7aa5c` |

**Content-level drift notes (audit seed vs actual HEAD):**
- **Parity fixture path:** the spec/task reference `tests/fixtures/parity-contract.json` does not exist; the actual artifact is `tests/fixtures/parity-contract.yaml` (its `.json` sibling is the schema `tests/fixtures/parity-contract.schema.json`). Counts below are read from the `.yaml`.
- **`ScaffoldContractTests` `.slnx`-inventory:** GREEN at HEAD (`SolutionContainsOnlyCanonicalBuildableProjects`), matching the audit's own correction and reconciling the older project-memory note of a pre-existing red.
- The audit's line-numbers/LOC figures are treated as **historical**; the load-bearing structural claims were re-verified at HEAD (domain-csproj platform isolation, 50 `.slnx` projects, 49 REST operations, `ContractVersion = 0.0.0-scaffold`, 29 root-policy projects). All hold.

---

## 3. Submodule SHA map (root-declared, non-recursive)

`git submodule status` (no `--recursive`). `.gitmodules` declares **8** submodule paths under `references/`; all are clean (leading space = at recorded commit, no `+`/`-`/`U`). No nested submodules were initialized.

| Submodule (`references/`) | SHA | Describe |
| --- | --- | --- |
| `Hexalith.AI.Tools` | `485d0aabd9af8fe02c78e68eedf9cc892c340560` | `heads/main` |
| `Hexalith.Builds` | `6fcd894c41bb83eaf78cd845a6ab4964af367154` | `v4.16.3-26-g6fcd894` |
| `Hexalith.Commons` | `275edc03e1fc4aa51d45340e21fc1592938eec87` | `v2.26.0-3-g275edc0` |
| `Hexalith.EventStore` | `c0fe028e5d9d25cf3f5d8f982c26c99f2449177b` | `v3.43.0-9-gc0fe028e` |
| `Hexalith.FrontComposer` | `f61c6a8a5bfba56811f298d1d9c58b779e25e389` | `v1.6.1-2-gf61c6a8a` |
| `Hexalith.Memories` | `1b1db8df2ac6f47ca250bf37009073050feaa770` | `v1.44.0-18-g1b1db8d` |
| `Hexalith.PolymorphicSerializations` | `89c8409785aad2b8bcfbbae079b52adf0ad14441` | `v1.16.1-1-g89c8409` |
| `Hexalith.Tenants` | `9c7aa5c7458cc7639ed7b4b4c6b5abee52a812a8` | `v2.3.0-5-g9c7aa5c` |

Note: `CLAUDE.md`'s default init list names **7** modules (omits `Hexalith.PolymorphicSerializations`), while `.gitmodules` declares **8**. `PolymorphicSerializations` is present and clean at HEAD; no action taken for this chore.

---

## 4. Solution / project / package inventory

- **`Hexalith.Folders.slnx`: 50 projects** = 19 submodule (`references/`) projects + **31 Folders-owned** (13 `src/`, 2 `samples/`, 14 `tests/`, 2 `tests/tools/`).
- **`src/` projects (13 in `.slnx`):** `Hexalith.Folders`, `.AppHost`, `.Aspire`, `.Cli`, `.Client`, `.Client.Generation.Shared` (nested under `Client/Generation/Shared`), `.Contracts`, `.Mcp`, `.Server`, `.ServiceDefaults`, `.Testing`, `.UI`, `.Workers`.
- **Package management:** central. Root `Directory.Packages.props` holds **0** inline `PackageVersion` entries; it imports the Builds central props (`references/Hexalith.Builds/Props/Directory.Packages.props`, **250** `PackageVersion` entries, via `$(Hexalith*BuildPackageProps)` MSBuild-property indirection) with `ManagePackageVersionsCentrally=true`. Package pins therefore live in the `Hexalith.Builds` submodule.
- **SDK pin:** `global.json` → `10.0.301`, `rollForward: latestPatch`. Local `dotnet --version` = `10.0.301`.

**Domain-isolation root cause (re-verified at HEAD — the driver of Epic 11):** `src/Hexalith.Folders/Hexalith.Folders.csproj` references only `Hexalith.Folders.Contracts` (ProjectReference) + `Dapr.Client` + `Octokit` + four `Microsoft.Extensions.*` packages. It references **neither `Hexalith.Commons.*` nor `Hexalith.EventStore.*`** — the isolation the audit identifies as the systemic cause. Still true at `5ce25e4`.

---

## 5. Route / parity operation counts (recorded, files NOT edited)

| Surface | File | Metric |
| --- | --- | --- |
| OpenAPI spine | `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml` | 10,268 lines; **46 path items**; **49 operations** (29 `get` + 16 `post` + 4 `put`) |
| Parity oracle | `tests/fixtures/parity-contract.yaml` | 4,692 lines; **49 operations**; families = 14 `mutating_command` + 17 `query_status` + 7 `context_query` + 7 `operations_console_projection` + 4 `audit`; `diagnostics_count` documented `0` (fixture header comment, not an enforced field) |

Operation count is pinned by `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs:64` → `ImplementedRestOperationCount = 49` (mirrored in `Client.Tests` **by value** via `ParityScenarios.ExpectedOperationCount = 49` at `tests/shared/Parity/ParityScenarios.cs:20` — same count, different constant). Both files above were read only; neither was modified.

---

## 6. Workflow pins

Five workflows under `.github/workflows/`. **Submodule-init policy: `submodules: false` on every checkout step** (12 occurrences: `ci.yml` ×6, `release-packages.yml` ×3, `nightly-drift.yml` ×1, `policy-conformance.yml` ×1, `contract-spine.yml` ×1). Baseline CI therefore has **no PackageReference fallback** — cross-repo platform work must ship via submodule pins or published packages (known caveat).

| Workflow | Jobs | Gate scripts pinned (`tests/tools/run-*.ps1`) |
| --- | --- | --- |
| `ci.yml` | `baseline-build-and-unit-gates`, `contract-and-parity-gates`, `security-and-redaction-gates`, `capacity-smoke-gates`, `accessibility-gates`, `e2e-gates` | `run-baseline-ci-gates`, `run-contract-parity-ci-gates`, `run-security-redaction-ci-gates`, `run-capacity-smoke-ci-gates`, `run-accessibility-ci-gates -SkipBrowserInstall`, `run-e2e-ci-gates -SkipBrowserInstall` |
| `contract-spine.yml` | `contract-generated-artifact-gates` | `run-contract-spine-gates -NoRestore`, `run-safety-invariant-gates`, `run-governance-completeness-gates`, `run-dapr-policy-conformance-gates`, `run-production-observability-gates`, `run-consumer-docs-gates`, `run-operations-audit-docs-gates`, `run-provider-error-docs-gates`, `run-nfr-traceability-gates`, `run-adr-runbook-docs-gates` (all `-SkipRestoreBuild`) |
| `nightly-drift.yml` | `nightly-drift-gates` (schedule + dispatch) | `run-nightly-drift-gates -SkipRestoreBuild -ProviderProfile …` |
| `policy-conformance.yml` | `policy-conformance-gates` (schedule + dispatch) | `run-scheduled-policy-conformance-gates -SkipRestoreBuild -PolicyMode …` |
| `release-packages.yml` | `release-prerequisite-gates`, `release-package-conformance`, `publish-packages` | `run-contract-parity-ci-gates`, `run-security-redaction-ci-gates`, `run-capacity-smoke-ci-gates`, `run-capacity-calibration-gates`, `run-retention-deletion-gates`, `run-safety-invariant-gates`, `run-governance-completeness-gates`, `run-nfr-traceability-gates`, `run-release-package-gates` |

SDK pin flows from `global.json` (`10.0.301`).

---

## 7. Focused gate results (exact commands + outcomes)

Restore/build ran once; `-SkipRestoreBuild` used on gate scripts that accept it. Gate reports live at `_bmad-output/gates/<name>/latest.json`. Re-running the passing gates #4/#6/#7/#8/#9/#10 produced byte-identical reports (git-clean afterward) — those gates are deterministic. Gates #3, #11, and #12a did **not** reproduce clean standalone this session (see their rows); the deterministic claim is scoped to the passing gates only.

| # | Command (repo root) | Status | Evidence / report path or blocker |
| --- | --- | --- | --- |
| 1 | `git rev-parse HEAD && git status --short --branch && git submodule status` | PASS | Sections 1–3 |
| 2 | `dotnet restore Hexalith.Folders.slnx` | PASS | exit 0 |
| 3 | `dotnet build Hexalith.Folders.slnx --configuration Release --no-restore` | PASS | **0 Warning(s), 0 Error(s)** on clean retry. First attempt exited 1 on a transient `MSB3883 IOException` (`obj/Release/net10.0/ref/Hexalith.Folders.dll` "used by another process") — consistent with a concurrent build-host file-lock (external cause **inferred, not captured**); the clean retry was not re-verified a second time in-session. Not a code defect. |
| 4 | `pwsh ./tests/tools/run-baseline-ci-gates.ps1 -SkipRestoreBuild` | PASS | `status=passed`; categories restore/build/format/lint/unit-tests all passed; 9 unit-test projects passed (Folders, Contracts, Client, Cli, Mcp, Testing, UI, Workers, Sample .Tests). Report: `_bmad-output/gates/baseline-ci/latest.json`. (`-SkipRestoreBuild` used since build already ran; script accepts the switch.) |
| 5 | `dotnet test tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj --filter FullyQualifiedName~ScaffoldContractTests` | PASS | **10 passed, 0 failed, 0 skipped** (incl. `SolutionContainsOnlyCanonicalBuildableProjects`, `ProjectReferencesFollowAllowedDependencyDirection`, `ForbiddenReferencesAreNotIntroduced`) |
| 6 | `pwsh ./tests/tools/run-contract-parity-ci-gates.ps1 -SkipRestoreBuild` | PASS | `status=passed`. Report: `_bmad-output/gates/contract-parity-ci/latest.json` |
| 7 | `pwsh ./tests/tools/run-security-redaction-ci-gates.ps1 -SkipRestoreBuild` | PASS | `status=passed`. Report: `_bmad-output/gates/security-redaction-ci/latest.json` |
| 8 | `pwsh ./tests/tools/run-safety-invariant-gates.ps1 -SkipRestoreBuild` | PASS | `status=passed`. Report: `_bmad-output/gates/safety-invariants/latest.json` |
| 9 | `pwsh ./tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild` | PASS | `status=passed`. Report: `_bmad-output/gates/governance-completeness/latest.json` |
| 10 | `pwsh ./tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild` | PASS | `status=passed`. Report: `_bmad-output/gates/dapr-policy-conformance/latest.json` |
| 11 | `pwsh ./tests/tools/run-release-package-gates.ps1 -Version 0.0.0-local.1 -SourceRevisionId 5ce25e47…` | BLOCKED | exit 1. **All 5 packable projects packed OK** (Contracts, Folders, Client, Aspire, Testing → `.nupkg`+`.snupkg`). Gate then fails the release-evidence freshness cross-check: `RELEASE-PACKAGES-FAILED: category=release-evidence reason=stale-capacity-calibration-evidence`, because `_bmad-output/gates/capacity-calibration/latest.json` has `source_commit=3b9fa9f…` ≠ HEAD. In CI, `release-packages.yml` runs `run-capacity-calibration-gates.ps1` in the same job **before** this gate, so the evidence is fresh; a standalone invocation does not regenerate it. Local-invocation ordering limitation, **not** a packable-surface defect. The report `_bmad-output/gates/release-packages/latest.json` was restored to its committed (`passed`, CI-representative) state so the committed baseline is not misrepresented; the standalone result is recorded here honestly. |
| 12a | `dotnet format whitespace --verify-no-changes` | BLOCKED (references-only) | exit 2. **0 Folders-owned files flagged.** All flagged files are under `references/**` submodule roots (`Hexalith.EventStore`, `Hexalith.FrontComposer`, `Hexalith.Memories`, `Hexalith.Tenants`) — CRLF end-of-line (`ENDOFLINE`) findings surfaced only because the command runs at the `.slnx` root, which includes the 19 submodule projects. `references/` is out of scope and must not be edited. The scoped baseline-ci `format` category (Folders `src`/`tests`/`samples` only, gate #4) passes. |
| 12b | `dotnet format analyzers --verify-no-changes --severity warn` | PASS | exit 0 |

---

## 8. Known blockers (recorded, not hidden by broad skips)

1. **DCP / AppHost live-boot.** `tests/Hexalith.Folders.AppHost.Tests` is Tier-3 opt-in via env `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION`; `SkipIfUnavailable()` skips cleanly (3 SKIP by default). The env-wide Aspire CLI/DCP `--tls-cert-file` mismatch blocks a real `aspire run` 6-service round-trip (Epic 10 AC9 live proof). Tracked as open retro action items (Epics 9/10). Not a topology defect; recorded as a standing environment blocker.
2. **`run-release-package-gates.ps1` standalone freshness** (gate #11). `stale-capacity-calibration-evidence` — needs `capacity-calibration/latest.json` regenerated at HEAD (CI does this in-job). Recorded, not masked; packages themselves pack clean.
3. **Solution-root `dotnet format whitespace`** (gate #12a). Fails only on `references/**` submodule EOL; zero Folders-owned files; `references/` is off-limits for this chore.
4. **Reference-pending NFR rows (release-blocking, owned & surfaced).** In `docs/exit-criteria/nfr-traceability.md`: `NFR18`→C7, `NFR26`/`NFR28`→C4, `NFR44`→C12, `NFR54`/`NFR55`→Story 7.17 observability, `NFR57`→C3, `NFR69`→release-validation. `tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs:188-204` (`ReferencePendingRowsAreOwnedAndSurfaceKnownGaps`) **hard-pins C3/C4/C7/C12** as required surfaced gaps — flipping any of them reddens the contract-spine lane. These are deliberate, owned gaps, not skips.

---

## 9. Governance pin map

Files that must move **in lockstep** with the code they pin during later Epic 11 stories. Any project add/rename/reference/route/doc change must update the paired pin file(s) in the **same commit**, or the corresponding gate reddens.

| Surface | Pinned files | Current evidence (HEAD) | Later-story impact |
| --- | --- | --- | --- |
| Solution/project inventory + dependency direction (master lock) | `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs` (`ExpectedRootPolicyProjects` L11, `ExpectedSolutionProjects` L44, `ProjectReferencesFollowAllowedDependencyDirection` L152, `ForbiddenReferencesAreNotIntroduced` L192); `Hexalith.Folders.slnx` | 29 root-policy + 50 solution projects; ScaffoldContractTests 10/10 green | 11.5/11.8 (add domain refs to `Hexalith.Commons.*`/`EventStore.Client`), 11.9 (delete `ServiceDefaults`), any project add/rename. Never rename `Hexalith.Folders.slnx` (repo-root sentinel for ~40 `RepositoryRoot()` walkers). |
| Packaged/release set | `tests/Hexalith.Folders.Contracts.Tests/Deployment/ReleasePackageConformanceTests.cs` (`ContractVersion`/`0.0.0-scaffold` pin L229-230; excluded/packable manifest); `deploy/**` release manifest | 5 in the release/publish set (Contracts/Folders/Client/Aspire/Testing); Cli + ServiceDefaults set `IsPackable=true` but are excluded from the release manifest (7 src csproj carry `IsPackable=true`); `ContractVersion = 0.0.0-scaffold` publish blocker | 11.9 (ServiceDefaults removal → drop its exclusion row), 11.12 (Client STJ regen keeps it packable) |
| Baseline-CI project allow-list | `tests/Hexalith.Folders.Contracts.Tests/Deployment/BaselineCiWorkflowConformanceTests.cs`; `.github/workflows/ci.yml` | 9-project baseline unit lane; `submodules: false` | 11.3 (fragile-gate fixes), 11.7 (test-helper moves must not change the 9-project lane silently) |
| REST route table + parity | `tests/Hexalith.Folders.Server.Tests/ServerEndpointRegistrationTests.cs`; `tests/Hexalith.Folders.Server.Tests/TransportParityConformanceTests.cs` (`ImplementedRestOperationCount = 49`; `Client.Tests` mirrors the value via `ParityScenarios.ExpectedOperationCount`); `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; `tests/fixtures/parity-contract.yaml` | 46 paths / 49 ops (OpenAPI) = 49 parity ops | 11.4 (Server transport/envelope dedup must stay wire-preserving — no count change); REST surface migration explicitly deferred beyond Epic 11 |
| Workflow / gate wiring | `.github/workflows/{ci,contract-spine,nightly-drift,policy-conformance,release-packages}.yml`; `ContractParityCiWorkflowConformanceTests.cs`, `SecurityRedactionCiWorkflowConformanceTests.cs` (pin sibling test-class/method FQNs) | 5 workflows; `submodules: false` ×12; 10 gate scripts in contract-spine | 11.3/11.7 — renaming any pinned test class/project self-breaks these FQN gates; move in lockstep |
| NFR traceability | `docs/exit-criteria/nfr-traceability.md`; `tests/Hexalith.Folders.Contracts.Tests/Deployment/NfrTraceabilityConformanceTests.cs` (C3/C4/C7/C12 hard-pin L202) | 8 reference-pending rows; C3/C4/C7/C12 required-surfaced | 11.13 close-out — do not silently flip reference-pending rows; they gate the contract-spine lane |
| Octokit dependency pin | `tests/Hexalith.Folders.*`/`GitHubDependencyGuardTests.cs` (asserts `Octokit Version="14.0.0"` verbatim in `references/Hexalith.Builds/Props/Directory.Packages.props`) | Domain csproj carries `Octokit` (stub-only) | 11.5/11.8 — becomes moot when Octokit is dropped; retire/relax this pin in lockstep |
| E2E lane | `tests/tools/run-e2e-ci-gates.ps1` (count "63"), `run-accessibility-ci-gates.ps1`, `install-playwright.ps1`, `ScaffoldContractTests.cs` L36/90/177 (10 pin sites total) | UI.E2E.Tests 63 cases in two blocking CI jobs | Avoid renaming/moving `UI.E2E.Tests` (11.7/11.11) — 10 pin sites break silently |
| AppHost/Aspire topology | `tests/…/AspireTopologyTests.cs` (Dapr app-ids/topics/routing, image/port literals); `tests/Hexalith.Folders.AppHost.Tests/**` (Tier-3, DCP-gated) | Topology pins; AppHost.Tests 3 SKIP | 11.9/11.10 topology-touching stories must include the (silent until DCP lane runs) AppHost.Tests checklist |

---

## 10. Next-story handoff constraints

- **Lockstep sequencing (audit §10.1/§12):** for any move, update `ScaffoldContractTests` inventories → `ReleasePackage`/`BaselineCi` inventories → `run-*.ps1` + workflow FQN pins → `.slnx`, **all in the same commit**. Never rename `Hexalith.Folders.slnx`.
- **Platform-first (11.2 before 11.8-11.12):** do not delete local implementations (TenantAccess, cursor codecs, read-model stores, ServiceDefaults, telemetry sources, secret client) until the shared-module prerequisite APIs are pinned via `chore(deps):` submodule bumps and behavior-equivalent.
- **Wire preservation:** Epic 11 changes zero REST/OpenAPI/envelope/ProblemDetails/parity behavior unless a story updates contract + fixture + docs + tests together; the `0.0.0-scaffold` publish blocker keeps package-surface moves cheap for now.
- **Submodule pointer drift** (EventStore/Memories/Tenants vs `533806b`) is intentional and must **not** be reverted; no recursive/nested init.
- **Generated artifacts stay generated:** do not hand-edit the NSwag client or `parity-contract.yaml`; regenerate from the spine.
- **Two standing environment blockers** (DCP/AppHost live-boot; release-package capacity-calibration freshness) are env/ordering limitations, not refactor gates — carry them forward as recorded, not as reasons to broaden skips.

---

## 11. Current sprint status (captured, NOT flipped)

From `_bmad-output/implementation-artifacts/sprint-status.yaml`:

- `epic-11: backlog`
- `11-1-establish-refactor-baseline-and-governance-pin-map: backlog`

---

## 12. Post-baseline annotation — pending cross-epic delta (2026-07-07, correct-course)

> Appended after baseline capture; §§1–11 above are the unchanged HEAD evidence snapshot.

**Story 10.6 (reopened Epic 10) intentionally changes worker semantic-indexing behavior.** This baseline pinned the *fail-closed* content materializer (`src/Hexalith.Folders.Workers/SemanticIndexing/FailClosedSemanticIndexingContentMaterializer.cs`, always `Unavailable`) as HEAD evidence. Story 10.6 replaces it with a **metadata-derived materializer** so authorized mutations populate the Memories search index under C4/C9 (FR58).

- **Sequencing:** Story 10.6 must land **before Story 11.10** (Server/Workers EventStore/Memories SDK alignment), which rewrites the same Workers indexing code. 11.10 must then rebase on and **preserve the new metadata-derived behavior**, not re-freeze the placeholder.
- **Scope:** internal worker behavior only — no REST/OpenAPI/envelope/parity change; the §10 wire-preservation invariant is unaffected.
- Ref: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-content-materializer.md`.

Per Story 11.1 constraints these remain `backlog` — the story is not review-approved. This artifact does **not** change sprint status.
