# Reviewer Gate — Code-Reality / Brownfield Lens (UPDATE pass)

**Target:** `_bmad-output/planning-artifacts/architecture.md` (1902 lines; amended 2026-09-16, `+91/-46`, uncommitted in the working tree)
**Prior review:** `reviews/review-code-reality-2026-09-16.md` (FAIL — 4 critical / 4 high / 4 medium / 2 low)
**Repo HEAD:** `de281e7` (`main`) · **Date:** 2026-09-16 · **Run:** VALIDATE (no edits applied to architecture.md or any project file)

## Verdict

**FAIL** — 2 critical findings.

The amendment did the hard part and did it well. All four prior criticals are closed at their root: `Hexalith.Folders.EventStore` is now in the component boundaries, the Dapr app-id list and both project trees with facts I verified line by line; I-3 and I-8 carry honest `as-built` / `Target (not built)` splits; C10 is re-anchored on the gate that actually runs — and I confirmed that gate is genuinely merge-blocking, which the amendment did not claim but is true. The PD11 reality table now states the dangerous fact correctly: two of the five new transitions are *accepted onto the unguarded branch*, not rejected. That is exactly the direction of correction the prior review asked for.

The problem is what came with it. **The amendment introduced eight new factual claims about the codebase that are false**, two of them load-bearing enough to be critical: S-7's assertion that the PD10 wire vocabulary is already constrained by `tests/fixtures/parity-contract.schema.json` (7 of its 12 tokens are absent, and the schema has no `code`, `clientAction` or `visibility` axis at all), and Story 12.4's new "corrected anchor" claim that **no workspace-executor type exists** (`IWorkspaceCommitExecutor`, `UnavailableWorkspaceCommitExecutor` and `WorkspaceCommitService` all exist, and the Server registers the fail-closed one). A remediation that re-anchors a story on a falsehood has reproduced the failure mode the gate exists to catch, one layer down.

Two structural patterns amplify this. First, **the corrections were applied in some places and not others**: C10's lint is honestly retracted in five locations and still asserted as shipped in six; I-8's chaos gate is labelled `Target (not built)` in its own row and still counted as a shipped defence-in-depth gate 1,080 lines later. The document now contradicts itself, which for a reader who lands on the uncorrected line is worse than the uniform overstatement it replaced. Second, **the `NOT BUILT` markers were placed selectively inside blocks the amendment demonstrably verified** — `RateLimiting/` is marked, its equally-absent siblings `WorkspaceWorkflows/`, `CommitWorkflows/` and `SearchIndexing/` in the same block are not — which upgrades an unmarked entry from "unverified" to "verified and found present". The banner's blanket disclaimer does not cover entries the same edit inspected.

On the structural remedy proper: **the banners are the right instrument and are close to sufficient.** The tree banner correctly demotes the tree to target intent and names an as-built authority. Three things keep it from landing: the marker asymmetry above (UPD-10), the fact that `Hexalith.Folders.slnx` is named authoritative while a standing conformance test disputes its two newest entries (UPD-11), and the "13 src projects / 11 test-load projects" count at line 1769, which the amendment moved the tree under without updating and which now disagrees with the tree *and* with the authority the banner installs (UPD-15).

---

## Findings — new, introduced or left by this amendment

### UPD-1 — CRITICAL — S-7 claims the PD10 vocabulary is already pinned by `parity-contract.schema.json`. It is not — 7 of 12 tokens are absent and the schema has no axis for three of them

architecture.md line 668 (S-7, new text):

> Every token above is a member of the closed canonical vocabulary in `tests/fixtures/parity-contract.schema.json`; the C13 `cli_exit_code` / `mcp_failure_kind` columns are **derived from the matrix**, so CLI and MCP cannot disagree on whether an authority outage is retryable

Verified against `tests/fixtures/parity-contract.schema.json` (every `enum` in the file enumerated):

| S-7 token | Role S-7 assigns it | In the schema? |
| --- | --- | --- |
| `authentication_failure` | category | yes — but as a member of `$defs/canonical_error_category` |
| `authentication_required` | code | **no — 0 occurrences** |
| `tenant_access_denied` | category | yes — `canonical_error_category` |
| `resource_unavailable` | code | **no — 0 occurrences** |
| `read_model_unavailable` | category | yes — `canonical_error_category` |
| `projection_unavailable` | code | yes — but as a *category*, not a code |
| `check_credentials` | clientAction | **no — 0 occurrences** |
| `no_action` | clientAction | **no — 0 occurrences** |
| `retry` | clientAction | **no — 0 occurrences** |
| `redacted` | visibility | yes — but as a *category* |
| `metadata_only` | visibility | **no — 0 occurrences** |
| `withheld` | visibility | **no — 0 occurrences** |

The schema's only closed vocabularies are `$defs/canonical_error_category` (50 members), `$defs/mcp_failure_kind` (48), `$defs/cli_exit_code`, `$defs/adapter_name`, and six per-property enums (`operation_family`, `read_consistency_class`, `auth_outcome_class`, `idempotency_key_rule`, `pre_sdk_error_class`, the four `*_sourcing` sets). **There is no `code` vocabulary, no `clientAction` vocabulary, and no `visibility` vocabulary anywhere in the file.** The five tokens that do appear sit on a different axis from the one S-7 assigns them: S-7 uses `redacted` as a `visibility` value, `resource_unavailable` as a *code* under category `tenant_access_denied`, and `projection_unavailable` as a *code* under category `read_model_unavailable` — none of which the schema can express.

Why critical: the sentence exists to reassure the PD10 owner that a divergence is mechanically caught. It is not caught — the parity gate has no column to catch it in. Worse, the claim points the wrong way round: `canonical_error_category` still enumerates `cross_tenant_access_denied`, `audit_access_denied` and `not_found`, the three codes PD10 *removes*, so `parity-contract.schema.json` is an artifact the correction must **change**, and is currently one of the six regeneration targets the same section lists. Stating that it already backs the vocabulary removes it from that list by implication. (For contrast, the spine does carry some of these: `check_credentials` ×1 and `no_action` ×2 appear in `hexalith.folders.v1.yaml` clientAction enums, `retry` ×25; `authentication_required`, `resource_unavailable` and `withheld` are absent there too.)

---

### UPD-2 — CRITICAL — Story 12.4's "corrected anchor" asserts that no workspace-executor type exists. Three do, and the Server registers one

architecture.md line 264 (new):

> **Corrected anchor (2026-09-16):** there are no `NotImplementedException` workspace-executor methods — **no workspace-executor type exists**, and the single `NotImplementedException` in `src/` is the deliberate live-readiness seam in `OctokitGitHubApiClient.cs:60` …

Two of the three clauses verify. `grep -rn "NotImplementedException" src/ --include=*.cs` returns **exactly one hit**, `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs:60`, inside `GetReadinessAsync` (`:49-63`), whose own comment says the live probe "is intentionally deferred to the provider contract / live-nightly drift path (AC 12)" — a readiness probe, not a write ✓. Provider write operations are implemented ✓ (`OctokitGitHubApiClient.cs:283 StageFileChangesAsync`, `:520 CommitAsync`; `ForgejoSmartHttpGitTransport.cs:450 repository.Network.Push(...)`).

The middle clause is false:

- `src/Hexalith.Folders/Aggregates/Folder/IWorkspaceCommitExecutor.cs` — the port
- `src/Hexalith.Folders/Aggregates/Folder/UnavailableWorkspaceCommitExecutor.cs` — the fail-closed implementation, returning `WorkspaceCommitExecutionResult.KnownFailure(ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode())`
- `src/Hexalith.Folders/Aggregates/Folder/WorkspaceCommitService.cs` — the consumer
- `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs` — registers `UnavailableWorkspaceCommitExecutor` alongside `UnavailableWorkspacePathPolicyEvidenceProvider`, `UnavailableWorkspaceFileContentStore`, `UnavailableWorkspaceFileDeleteOperationStore`

Why critical: the amendment's stated purpose for this edit was to stop Story 12.4 pointing at a code shape that does not exist. It replaced one wrong anchor with another, and this one is more misleading, because the type it denies **is the exact seam 12.4 must replace**. A developer told "no workspace-executor type exists" will author a second seam beside `IWorkspaceCommitExecutor` rather than swapping its registration. The paragraph almost reaches the right statement in its next sentence ("the real gap this story closes is composition… into a Server composition that currently fails closed") — the honest anchor is: *the executor port exists and is bound to `Unavailable*` stubs that return `KnownFailure`; 12.4 replaces the binding, not the type.*

---

### UPD-3 — HIGH — "`NFR74` is credited to Epic 13 but no Epic 13 story owns it" is false, self-refuting, and contradicts the paragraph directly above it

architecture.md line 283 (new):

> **`NFR74` is credited to Epic 13 but no Epic 13 story owns it** (`nfr-traceability.md` assigns 74 and 76 to 13-2, 75 to 13-1, 77 to 13-3, 78 and 84 to 13-6, 81 to 13-4, 82 to 13-5 …)

- `docs/exit-criteria/nfr-traceability.md:120` — `| NFR74 | Edge Security & Deployment Hardening | 28d62bb85938 | reference-pending | `13-2` | — | … |`. The row assigns it.
- `_bmad-output/implementation-artifacts/sprint-status.yaml:243` — `13-2-fail-safe-fallback-authorization-policy-and-sidecar-only-app: backlog`. The story exists.
- architecture.md line 275, five lines earlier and unchanged: *"As recorded there: Epic 13 owns NFR74 and NFR76 (`13-2`)…"* — and that paragraph declares the traceability table authoritative over prose.

The claim's own parenthetical states the assignment it denies. Two adjacent paragraphs in the same section now say opposite things about the same row, and a reader has no way to tell which the amendment meant. Whatever the intended point was (possibly that `13-2`'s scope as written in `epics.md` does not visibly cover NFR74's HTTPS-or-loopback obligation), that is a different claim and needs to be made as one.

---

### UPD-4 — HIGH — "`oasdiff` … exists nowhere in the repository" is false, and the three places it survives were left unreconciled

architecture.md line 691 (A-7, new): *"the 2026-05 draft named `oasdiff` as the classifier, which was never adopted and **exists nowhere in the repository**."*

`git grep -ni oasdiff` (excluding `references/` and `_bmad-output/`) returns three tracked files, all of them downstream authorities:

- `docs/adrs/0003-provider-abstraction-and-capability-model.md:23` — *"`C12`: nightly oasdiff drift classifies provider schema changes as additive, breaking, or unknown, and an unsupported or failing provider version cannot report ready."* An **ADR**, i.e. a decision record, not prose.
- `docs/runbooks/index.md:13` — *"Operator response to additive, breaking, and unknown oasdiff drift."*
- `docs/runbooks/provider-drift.md:3` — *"the operator-facing response to provider schema drift detected by the nightly oasdiff lane."*

The sweep replaced `oasdiff` at eight sites inside architecture.md and then asserted total absence — which suppresses the lockstep obligation the sweep created. ADR-0003 is cited by C12 as a decision authority and the provider-drift runbook is what an on-call operator opens during a drift incident; both now describe a classifier that does not exist, and the architecture says there is nothing to fix. The as-built substitution itself verifies: `tests/tools/run-nightly-drift-gates.ps1` (29,671 bytes) and `tests/tools/forgejo-drift/` (`Write-SanitizedForgejoDriftReport.ps1`, `classification-fixtures.json`) both exist ✓.

Minor, same row: C12's Artifact Location dropped `.github/workflows/nightly-drift.yml`, which does exist and is where the lane is surfaced. Naming only the script loses the trigger.

---

### UPD-5 — HIGH — "Degraded mode is decided once, by the approved matrix" defers to an artifact that contradicts itself on exactly that condition, and the new S-7 text quietly drops the disputed state

architecture.md line 673 (new): *"The approved `authorization-matrix.md` is normative and settles it: **trusted tenant, membership, or delegation evidence that is stale or unavailable returns the 503 envelope — it is never served from a stale projection.**"*

The matrix says both things:

- `docs/contract/authorization-matrix.md:76`, §"Canonical Negative Access States" — `| stale | Identity, membership, delegation, or folder-ACL evidence is stale, conflicting, or incomplete. | authority-evidence evaluation, before any protected-resource lookup | `safe-denial-404` | all-11 |`. The section header above it states that every one of these states "returns the exact canonical 404 safe denial."
- `docs/contract/authorization-matrix.md:146`, §"Canonical Outcomes" — `authority-unavailable-503` … "Trusted tenant, membership, or delegation evidence is **stale** or unavailable."

So the artifact the amendment declares normative routes a stale-authority request to a 404 in one table and a 503 in the other. The new S-7 row then enumerates the `safe-denial-404` causes as *"every authenticated absent, hidden, wrong-tenant, revoked, disabled, unknown, or insufficient-scope cause"* — seven of the matrix's **eight** negative access states, with `stale` silently omitted. That omission is what makes the reconciliation paragraph read as clean.

This is a caller-visible status code for the single most likely partial-outage condition in the system, and the two branches differ in `retryable` (`false` vs `true`) and in whether the client retries at all. Declaring it settled by appeal to an artifact that does not settle it is the more dangerous of the two available errors: a reader who checks the citation finds a table that agrees, and stops.

The rest of the S-7 transcription is exact and verified against `authorization-matrix.md:144-146` — 401 / `authentication_failure` / `authentication_required` / `false` / `check_credentials` / `redacted`; 404 / `tenant_access_denied` / `resource_unavailable` / `false` / `no_action` / `redacted`; 503 / `read_model_unavailable` / `projection_unavailable` / `true` / `retry` / `redacted` ✓.

---

### UPD-6 — HIGH — The `NOT BUILT` markers are placed selectively inside blocks the amendment verified, which converts an unmarked sibling from "unverified" into "verified present"

The tree banner (line 1168) says: *"Entries carrying a `NOT BUILT` comment were verified absent at 2026-09-16; entries marked `AS-BUILT` were verified present. Unmarked entries have not been re-verified since 2026-05."* That contract is sound — but it is broken wherever the amendment marked one entry in a block and left its verified-absent neighbours bare.

`src/Hexalith.Folders.Workers/` actually contains: `FoldersWorkersModule.cs`, `Program.cs`, `Properties/`, `RepositoryProvisioning/`, `SemanticIndexing/`, `Tenants/`. The tree (lines 1461-1479):

| Tree entry | Line | Marked? | Exists? |
| --- | --- | --- | --- |
| `WorkspaceWorkflows/` + `WorkspacePreparationWorkflow.cs`, `WorkspaceCleanupWorkflow.cs`, `WorkingCopyManager.cs` | 1464-1467 | no | **no** |
| `RepositoryWorkflows/` + `RepositoryProvisioningWorkflow.cs`, `RepositoryReconciler.cs` | 1468-1470 | no | dir is `RepositoryProvisioning/` |
| `CommitWorkflows/` + `CommitWorkflow.cs`, `CommitReconciler.cs` | 1471-1473 | no | **no** |
| `SearchIndexing/` | 1474 | no | dir is `SemanticIndexing/` |
| `RateLimiting/` + `PerTenantTokenBucket.cs`, `GlobalReconciliationBucket.cs` | 1475-1477 | **`NOT BUILT` ×3** | no ✓ |

The same pattern elsewhere:

- `tests/Hexalith.Folders.IntegrationTests/ProviderRateLimitChaos/ # chaos test per I-8 + M5` (line 1537) and `DaprPolicyConformance/ # negative-test suite per I-3 + M4` (line 1536) — both unmarked, both absent (`ls tests/Hexalith.Folders.IntegrationTests/` → `AdapterParity`, `ContextSearch`, `EndToEnd`, `MixedSurfaceHandoff`, `Parity` + loose files). The amendment's own I-8 row says the chaos gate does not exist and its I-3 row says the live suite does not exist; the tree entries for both survive unlabelled.
- `tests/Hexalith.Folders.Tests/{Idempotency,Caching}/` (lines 1519-1520) — unmarked, absent (real: `Aggregates`, `Authorization`, `Observability`, `Projections`, `Providers`, `Queries`).
- `tests/tools/policy-conformance/ # property-based generator for I-3 negative tests` (line 1556) — unmarked, absent, and sitting **directly beneath** `forgejo-drift/ # nightly Forgejo schema-diff classifier (as-built)`, a line the same hunk rewrote. Adjacency now reads as endorsement.

Fix is cheap and mechanical: mark every entry the amendment verified, in the block where it verified it, or mark none and rely on the banner alone.

---

### UPD-7 — HIGH — `Hexalith.Folders.EventStore` was added to four places and omitted from the two routing tables that would send an implementer to it

Everything the amendment asserts about the project is **true and verified**:

| Claim | Evidence |
| --- | --- |
| deployable project, `ContainerRepository=eventstore` | `Hexalith.Folders.EventStore.csproj:2-7` — `Sdk="Microsoft.NET.Sdk.Web"`, `IsPublishable=true`, `EnableContainer=true`, `<ContainerRepository>eventstore</ContainerRepository>` ✓ |
| composes EventStore Gateway + DomainService | `csproj:14-17` — project refs under `HexalithEventStoreFromSource=='true'`, package refs otherwise ✓ |
| serves Dapr app id `eventstore` | `AppHost/Program.cs:22` `AddProject<Projects.Hexalith_Folders_EventStore>(FoldersAspireModule.EventStoreAppId)`; `FoldersAspireModule.cs:10` `EventStoreAppId = "eventstore"` ✓ |
| hosts the A-9 idempotency intent adapters | `Program.cs:24` `AddFoldersIdempotencyIntentAdapters()`; 13 `*IdempotencyIntentAdapter.cs` + `FoldersCanonicalIntentBuilder.cs` + `FoldersIdempotencyIntentAdapterCatalog.cs` ✓ |

Added at lines 491 (high-level tree), 1455-1460 (detailed tree), 1583 (component boundaries), 1589 (app-id list). But:

- §"Requirements to Structure Mapping" line 1614 still routes **FR37–FR42 Commit, Evidence, Idempotency** to `src/Hexalith.Folders/Idempotency/` — a directory that does not exist (`ls src/Hexalith.Folders/` → `Aggregates`, `Authorization`, `Observability`, `Projections`, `Providers`, `Queries`, `FoldersModule.cs`, `FoldersServiceCollectionExtensions.cs`).
- Cross-cutting concern **#21** (line 1639) still routes to `Idempotency/IdempotencyRecordStore.cs`.
- The amendment's own new boundary bullet (1583) says *"a change to the idempotency equivalence field list lands here"* — meaning `Hexalith.Folders.EventStore`. The document's two routing surfaces now point implementers at different projects for the same work, and the one an implementer is instructed to consult (line 1871: *"Respect project structure and boundaries"*) names a path that does not exist.
- Asymmetric tree update: `Hexalith.Folders.EventStore.Tests` was added to the high-level tree (line 503) but **not** to the detailed `tests/` tree (1514-1537), which still lists the same ten test projects it listed before.

---

### UPD-8 — MEDIUM — The "verified as-built" package-management correction names the wrong selector and inverts the default

architecture.md line 535 (new): *"consumed **two ways, selected by the `UseNuGetDeps` property** … by **default**, project references to the root-level sibling submodule source … under **`-p:UseNuGetDeps=true`**, package references … which is the mode `.github/workflows/release-packages.yml` restores and builds in."*

`Directory.Build.props:15-22`, in evaluation order:

```
UseHexalithProjectReferences ?= false   when UseNuGetDeps == 'true'
UseHexalithProjectReferences ?= true    when UseNuGetDeps == 'false'
UseHexalithProjectReferences ?= true    when Configuration == ''
UseHexalithProjectReferences ?= false   when Configuration == 'Release'
UseHexalithProjectReferences ?= true    when Configuration == 'Debug'
UseHexalithProjectReferences ?= false   (fallback)
UseNuGetDeps ?= false   when UseHexalithProjectReferences == 'true'
UseNuGetDeps ?= true    (fallback)
```

So:

1. **The selector is `Configuration`, with `UseHexalithProjectReferences` as the canonical forcing switch** — the file's own comment says so: *"Debug builds use project references to the `references/` submodule source; Release builds use the centrally pinned NuGet packages… Force a mode with `-p:UseHexalithProjectReferences=true|false`."* `UseNuGetDeps` is the derived inverse alias; the row promotes the alias to selector.
2. **"By default, project references" is true only for Debug or unspecified configuration.** Any plain `dotnet build -c Release` already resolves NuGet packages with nobody passing `-p:UseNuGetDeps=true`. That is the actual trap, and the row's warning ("a change that breaks the NuGet mode breaks the release lane silently, because the default lane will still be green") is the right risk attached to the wrong mechanism.
3. `release-packages.yml` restores and builds in **both** modes, not only the NuGet one: `:60,:63` default (no `Configuration`, therefore source mode), `:131,:134` and `:180,:183` explicit `-c Release -p:UseNuGetDeps=true`.
4. Minor: "the `Hexalith*Version` properties in `Directory.Packages.props`" — the repo-root `Directory.Packages.props` declares exactly one `PackageVersion` (`LibGit2Sharp 0.32.0`) and imports `references/Hexalith.Builds/Props/Directory.Packages.props`, which is where the `Hexalith*Version` properties live (`HexalithEventStoreVersion 3.104.0`, `HexalithTenantsVersion 5.7.0`, …). Two files share a basename and the row does not say which; the Coherence-Validation rewrite at line 1741 names the right one, so make them agree.

Verified in the same edit and correct: `Testcontainers` has **zero** occurrences in Folders source, csproj or props ✓ — its removal from the non-negotiable stack and both test-stack lists is sound.

---

### UPD-9 — MEDIUM — "one `*IdempotencyIntentAdapter` per mutating command" is not the as-built shape

Component boundaries, line 1583 (new, labelled `(**as-built**)`): *"the home of the **A-9 idempotency intent adapters** — one `*IdempotencyIntentAdapter` per mutating command."*

- **13** adapter files exist, not one per anything obvious. (A count of 15 is what you get by matching the string `IdempotencyIntentAdapter` against the directory listing — it picks up `FoldersIdempotencyIntentAdapterCatalog.cs` and `FoldersIdempotencyIntentAdapterServiceCollectionExtensions.cs`.)
- The Contract Spine declares **14** `mutating_command` operations (`tests/fixtures/parity-contract.yaml`, 49 operations total: 14 mutating, 17 query_status, 7 context_query, 7 operations_console_projection, 4 audit): `AddFile`, `ArchiveFolder`, `BindRepository`, `ChangeFile`, `CommitWorkspace`, `ConfigureBranchRefPolicy`, `ConfigureProviderBinding`, `CreateFolder`, `CreateRepositoryBackedFolder`, `LockWorkspace`, `PrepareWorkspace`, `ReleaseWorkspaceLock`, `RemoveFile`, `UpdateFolderAclEntry`.
- Adapters key on the **domain command type**, not the spine operation id: `MutateFilesIdempotencyIntentAdapter.CommandType => "Hexalith.Folders.Commands.MutateFiles"`, `OperationId => "mutate-files"`, doc comment *"Trusted canonical intent adapter for Add/Change/Remove file mutations"* — one adapter for three spine operations. `GrantFolderAccessIdempotencyIntentAdapter` / `RevokeFolderAccessIdempotencyIntentAdapter` (`"grant-folder-access"` / `"revoke-folder-access"`) have no matching spine operation id; the spine's is `UpdateFolderAclEntry`.

A-9 — the decision this bullet claims to implement — is written in Contract Spine operations (*"Required for every current/future mutating Contract Spine operation"*). The as-built mapping is spine-operation → domain command → adapter, and it is neither 1:1 nor id-preserving. Someone adding a spine mutating operation and looking for the adapter the boundary promises will mis-model it.

---

### UPD-10 — MEDIUM — "Neither `kind` nor `daprd` appears anywhere in the repository" is false, and the truth is a stronger citation than the absolute

I-3, line 759. `kind`/`daprd` appear in tracked Folders files:

- `_bmad-output/implementation-artifacts/7-1-deploy-production-dapr-deny-by-default-access-control.md:115` — *"Architecture requires a Dapr policy conformance CI job that runs `daprd` in a kind cluster… If the immediate PR gate cannot run kind/daprd reliably, create a deterministic static conformance gate now and document the live kind/daprd gate as a promotion/scheduled gate for Story 7.8. **Do not silently drop the live-gate requirement.**"* (also `:56`, `:166`)
- `tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:295` and `src/Hexalith.Folders.AppHost/DaprComponents/statestore.yaml:23` — incidental `daprd` references.

The absolute is both false and weaker than the fact: I-3's target is not unowned by oversight, it is a **recorded deferral with a named successor story and an explicit instruction not to lose it** — which is the citation an architect should be given, and which the architecture nowhere carries. Story 7.8's disposition against that deferral is the open question the I-3 row should be asking.

Everything else in the I-3 rewrite is verified correct and worth recording, because it is the strongest correction in the amendment: `.github/workflows/policy-conformance.yml:3-6` is `schedule: cron '43 2 * * *'` + `workflow_dispatch` only, with no `pull_request` trigger ✓; `DaprPolicyConformanceTests` lives in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/` and is **excluded** from the merge-blocking lane — `run-baseline-ci-gates.ps1:45` runs `Contracts.Tests` under an explicit twelve-class allow-list filter that does not include it, and `run-contract-parity-ci-gates.ps1` filters to `ContractSpine*` / `ParityOracleGenerator*` classes only ✓. "Cannot block a merge" is exactly right.

---

### UPD-11 — MEDIUM — The C10, I-3 and I-8 corrections are contradicted by their own document, five to six lines apiece

The honest labels landed in the rows; the recitals that cite them did not move. C10:

| Still asserts the lint that does not exist | Line |
| --- | --- |
| C10 criterion statement — *"Cache-key tenant-prefix **lint** enforcement (CI/build-time gate, naming convention, tooling)"* | 329 |
| I-5 — *"pipeline gates: build, format, lint (**including C10 cache-key tenant-prefix lint**)"* | 761 |
| Enforcement tiers — *"**Build-time (lint):** Cache-key tenant-prefix lint (C10)"* | 1095 |
| Pattern Consistency — *"cache-key tenant-prefix (C10 **lint**)"* | 1743 |
| Defence-in-depth gate set — *"cache-key tenant-prefix **lint**"* | 1844 |
| Implementer instructions — *"Sentinel-redaction tests, **cache-key tenant-prefix lint**, … all must pass before any PR merges"* | 1871 |

I-3: line 1076 (Test Strategy) still reads *"**Dapr policy conformance:** Negative-test suite **in kind cluster** asserts unauthorized `(sourceAppId, targetAppId, operation)` triples receive `403`; **CI blocks merge** on policy YAML changes without negative test additions"* — the exact sentence line 759 retracts.

I-8: line 788 (*"Phase 6 … provider rate-limit handling with chaos-test gate"*), line 1097 (*"CI gates: … provider-rate-limit chaos test"*), line 1537 (tree), line 1582 (*"`Hexalith.Folders.Workers` owns process managers / reconcilers / **rate-limit buckets** / tenant-event handlers"*), line 1844 (defence-in-depth).

A reader who lands on 1844 or 1871 gets the pre-amendment claim with no signal that it was downgraded 1,000 lines earlier, and 1871 is in the implementer-instructions block. Half-applied honesty labelling is a distinctive failure mode: it makes the document's truthfulness position-dependent.

Verified positively, and worth stating in the C10 row because the amendment does not: the as-built gate **is merge-blocking**. `run-governance-completeness-gates.ps1` is invoked by `.github/workflows/contract-spine.yml:53`, which triggers on `pull_request` and `push` to `main`/`next`/`alpha`/`beta` (and by `release-packages.yml:98`). `GovernanceCompletenessGateTests.cs:1383` `CacheKeyExceptionManifestIsReviewedAndCurrentRepositoryHasNoTenantDataCacheKeysWithoutScope()` + `:1432` fail-closed approval-state test, driven by `tests/fixtures/cache-key-exceptions.yaml` (1,119 bytes, present) ✓. No `Caching/` directory, no `*TenantPrefixedCacheKey*` file anywhere ✓. `ci.yml` has six jobs — `baseline-build-and-unit-gates` (16), `contract-and-parity-gates` (45), `security-and-redaction-gates` (80), `capacity-smoke-gates` (115), `accessibility-gates` (150), `e2e-gates` (184) — and no lint job ✓ (the only `lint` token in the file is a step *name* at `:40`; `run-baseline-ci-gates.ps1:109` carries a `lint` category, which is `dotnet format analyzers`, not a cache-key check).

---

### UPD-12 — MEDIUM — `Hexalith.Folders.slnx` is installed as "the authoritative as-built project inventory" while a standing conformance test disputes its two newest entries

Tree banner, line 1168: *"**`Hexalith.Folders.slnx` is the authoritative as-built project inventory**… where this tree and the repository disagree, the repository wins."*

`.slnx` declares 52 projects, including `src/Hexalith.Folders.EventStore/Hexalith.Folders.EventStore.csproj` and `tests/Hexalith.Folders.EventStore.Tests/Hexalith.Folders.EventStore.Tests.csproj`. `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:45` `ExpectedSolutionProjects` **omits both**, and `:124 SolutionContainsOnlyCanonicalBuildableProjects()` asserts `solutionProjects.ShouldBe(ExpectedSolutionProjects…)` — an exact-set equality. The test is red at HEAD on precisely the two entries the amendment relies on.

This drift is pre-existing and tracked, not caused by the amendment — but the remedy is now built on it, and the banner says nothing. Either the banner should note that the `.slnx`/`ScaffoldContractTests` lockstep is open, or the lockstep should be closed first. As written, the document sends a reader to an inventory that a repo gate says is wrong, to resolve disagreements about the very project the amendment added.

---

### UPD-13 — MEDIUM — The Requirements-to-Structure banner names two broken rows and leaves four reading as valid routing

Banner, line 1604: *"Several rows point at paths that do not exist — the `Caching/TenantPrefixedCacheKey.cs` helper and the `ci.yml` lint job among them."* Still broken, still unnamed, still reading as as-built routing:

| Mapping | Line | Reality |
| --- | --- | --- |
| FR1–FR3 → `docs/contract-terms.md` | 1608 | Does not exist. Real vocabulary: 19 files under `docs/contract/` (`contract-spine-foundation.md`, `authorization-matrix.md`, …) — a directory the tree still never names, though the amendment's own S-7/PD10 material now cites it as the normative artifact |
| FR37–FR42 → `src/Hexalith.Folders/Idempotency/` | 1614 | Absent; the adapters are in `src/Hexalith.Folders.EventStore/` (see UPD-7) |
| FR43–FR46 → `src/Hexalith.Folders/Projections/WorkspaceStatus/` | 1615 | `Projections/` = `FolderAccess`, `FolderList`, `SemanticIndexing`, `TenantAccess`. Real model: `Queries/Folders/IWorkspaceStatusReadModel.cs` |
| FR53–FR57 → `src/Hexalith.Folders/Projections/Audit/` | 1622 | Absent; real: `Queries/Audit/` |

The codebase settled on `Queries/{Area}/` + `Projections/{Area}/` (`Queries/` = `Audit`, `ContextSearch`, `FileContext`, `FolderAccess`, `Folders`, `OpsConsole`, `ProviderReadiness`) and the document's `Projections/{Concept}/`-for-everything layout never absorbed it. Naming the four rows costs one sentence and converts a generic disclaimer into usable routing.

---

### UPD-14 — MEDIUM — "Structure Completeness" now disagrees with both the tree it describes and the authority the new banner installs

Line 1769, unchanged: *"Repo-root-to-leaf-file directory tree enumerates all **13 src projects**, **11 test/load projects**…"*

- Tree before the amendment: 11 src. After adding `Hexalith.Folders.EventStore`: **12**. Still omitted: `Hexalith.Folders.ServiceDefaults` (exists in `src/` and `.slnx`) and `src/Hexalith.Folders.Client/Generation/Shared/` (a second `.csproj` nested inside the Client folder).
- `.slnx` — the banner's authority — declares **14** src projects and **17** under `tests/` (14 test projects + `tests/load` + two `tests/tools` projects).
- The detailed tree still lists 10 test projects + `tests/load` = 11 (EventStore.Tests went into the other tree only, per UPD-7).

Three different numbers for the same inventory, in a document that just declared one of them authoritative.

---

### UPD-15 — LOW — The PD11 reality table's "a CI test asserts that rejection" over-claims on one of the three rows

Verified: `(ChangesStaged, CommitFailed, null) => Failed` at `FolderStateTransitions.cs:90-91` and `(Inaccessible, ProviderReadinessValidated, null) => Ready` at `:107-108`, both unconditional ✓ — the two-accepted-onto-the-unguarded-branch claim is exactly right, and the switch's only guarded arms remain `:110`/`:112` for `(UnknownProviderOutcome, ReconciliationCompletedDirty, CommitConfirmed|CommitRejected)`.

Of the three said to be rejected with a CI test asserting it:

- `changes_staged` → `inaccessible` on `AuthRevocationDetected` / `TenantRevoked` — no arm, and `FolderStateTransitionsTests.cs:82 EveryUnlistedStateEventPairShouldRejectWithoutChangingState` iterates the full `state × event` cross-product with `dirtyResolution: null` and asserts `StateTransitionInvalid` ✓
- `dirty` → `changes_staged` on `WorkspaceLocked` — same ✓
- `dirty` → `ready` on `LockLeaseBecameStale` — **the test cannot reach this pair.** It loops `Enum.GetValues<FolderWorkspaceLifecycleEvent>()`, and `LockLeaseBecameStale` is not a member (23 members; `LockLeaseExpired` at `:21` is the only lease event). Rejection is vacuous and nothing asserts it. The same table cell says "LockLeaseBecameStale does not exist", so the cell's two halves disagree.

Also verified in the new "Two divergences are open right now" paragraph (line 434), which is a genuine improvement: `FolderStateTransitions.cs:157` `Dirty => AwaitingHuman` ✓, pinned by `FolderStateTransitionsTests.cs:199 OperatorDispositionShouldMatchC6StateCatalog` ✓; `:161 UnknownProviderOutcome => AwaitingHuman` ✓; `docs/exit-criteria/c6-transition-matrix-mapping.md:44` and `docs/diagrams/workspace-lifecycle.md:26` both say `awaiting-human` for `unknown_provider_outcome` while architecture says `auto-recovering` ✓. One precision note: the paragraph's structure implies the `dirty` divergence is also doc-vs-doc, but the mapping document (`:40`) and the diagram (`:22`) carry the *same* conditional `dirty` wording as the architecture — on `dirty`, all three documents agree and only the code diverges. `stagedBy*` has **zero occurrences in `src/`** ✓, exactly as claimed.

---

### UPD-16 — LOW — The PD10 reality row forbids transcribing a denominator in the sentence that transcribes it twice, and S-7 still hard-codes it

Line 240 (new): *"403 is live on **every protected operation in the current generated Contract Spine inventory** (`authorization-matrix.md` G1: 403 on 49 of 49, 404 on 46 of 49 — the denominator is the generated inventory, never a number transcribed here)."*

The prior REAL-9 (49-of-50) is fixed, and the quote is exact — `docs/contract/authorization-matrix.md:341` reads *"403 on 49 of 49 operations and 404 on 46 of 49"* ✓, matching the spine (`grep -cE '^ +"403":'` → 49) and the parity oracle (49 `operation_id` rows) ✓. But the parenthetical forbids the thing the clause before it does, and S-7 three lines later still opens *"All **49** protected Contract Spine operations evaluate authority before resource lookup"* — the hard-coded count the adversarial lens raised as F14, against the document's own generated-denominator rule at line 704. Either derive it or say once, in one place, that 49 is a transcription with a named refresh trigger.

---

## Carried forward from the 2026-09-16 review — unchanged, still open

| Prior | Sev | Status at this HEAD |
| --- | --- | --- |
| REAL-7 — `FolderWorkspaceDirtyResolution` misattributed as the PD11 guard discriminator | HIGH | **Fully open.** Line 444's guard list is untouched; the type still has exactly two members (`FolderWorkspaceDirtyResolution.cs:6-12` — `CommitConfirmed`, `CommitRejected`), consumed only at `FolderStateTransitions.cs:110,112`; the one genuinely guard-discriminated pair `(unknown_provider_outcome, ReconciliationCompletedDirty)` is still missing from the guard list; the *"(per Step 5 §'Process Patterns')"* cross-reference is still dangling. `docs/exit-criteria/c6-transition-matrix-mapping.md:24-27` carries the identical four-pair list, so the two documents are consistently wrong together — which is the shape that survives review longest |
| REAL-10 — `visibility` understated | MEDIUM | Open, clause verbatim unchanged. Spine still declares `details.visibility` as `required` with `enum: [redacted, metadata_only]` (`hexalith.folders.v1.yaml:7741-7748`) |
| REAL-12 — counts do not reconcile | MEDIUM | Open: "22 concerns mapped" (1809) vs 12 mapped; "~30 transitions" (1797, 1842) vs 41 edges; "12 capability blocks" (55) naming eleven |
| REAL-13 — "Minor Gaps" lists shipped artifacts | LOW | Open; `docs/adrs/0000-template.md` and `docs/runbooks/tenant-deletion.md` both exist |
| REAL-14 — provider adapters described as projects | LOW | Open (1584), and line 1745 still claims all boundaries are "mechanically enforceable via project references" |
| REAL-11 residual — docs tree, forgejo/github contract dirs, `Registration/`, missing root entries and test projects | MEDIUM | **Accepted as covered by the new banner** for entries the amendment did not touch. The exceptions are itemised in UPD-6 |

---

## Prior-finding closure tally

| Prior finding | Severity | Disposition |
| --- | --- | --- |
| REAL-1 — `Hexalith.Folders.EventStore` absent from the document | CRITICAL | **Closed** (residual UPD-7) |
| REAL-2 — I-3 kind/daprd merge gate asserted as shipped | CRITICAL | **Closed** (residuals UPD-10, UPD-11) |
| REAL-3 — I-8 token buckets + 429 chaos gate asserted as shipped | CRITICAL | **Closed** (residual UPD-11) |
| REAL-4 — C10 pinned to a non-existent artifact | CRITICAL | **Closed** (residual UPD-11) |
| REAL-5 — reality table understated the PD11 gap | HIGH | **Closed** (residual UPD-15) |
| REAL-6 — 12.4 / "provider Git write path throws" | HIGH | Mis-citation closed; **reopened at higher severity as UPD-2** |
| REAL-7 — `FolderWorkspaceDirtyResolution` misattribution | HIGH | Open, untouched |
| REAL-8 — five FR blocks + three concerns routed to absent paths | HIGH | **Partially closed** (banner + 2 rows); residual UPD-13 |
| REAL-9 — 403 denominator 49 of 50 | MEDIUM | **Closed** (residual UPD-16) |
| REAL-10 — `visibility` understated | MEDIUM | Open, untouched |
| REAL-11 — directory-tree drift | MEDIUM | **Closed by banner**, except UPD-6 |
| REAL-12 — validation-results counts | MEDIUM | Open (and UPD-14 adds a new instance) |
| REAL-13 — "Minor Gaps" stale | LOW | Open |
| REAL-14 — provider adapters as projects | LOW | Open |

**8 of 14 closed** (4 critical, 2 high, 2 medium), 1 partially closed, 1 closed-and-reopened-worse, 4 open.

---

## New false claims this amendment introduced

Every item below is a statement about the codebase that the 2026-09-16 edit added and that is not true at HEAD `de281e7`:

1. **`tests/fixtures/parity-contract.schema.json` contains the PD10 vocabulary** (S-7, line 668). 7 of 12 tokens absent; no `code`, `clientAction` or `visibility` axis exists in the schema. — UPD-1
2. **"No workspace-executor type exists"** (Story 12.4, line 264). `IWorkspaceCommitExecutor`, `UnavailableWorkspaceCommitExecutor`, `WorkspaceCommitService` exist; the Server registers the fail-closed one. — UPD-2
3. **"`NFR74` is credited to Epic 13 but no Epic 13 story owns it"** (line 283). `nfr-traceability.md:120` assigns it to `13-2`; `sprint-status.yaml:243` carries that story. — UPD-3
4. **"`oasdiff` … exists nowhere in the repository"** (A-7, line 691). Present in `docs/adrs/0003-*.md:23`, `docs/runbooks/index.md:13`, `docs/runbooks/provider-drift.md:3`. — UPD-4
5. **"The approved `authorization-matrix.md` … settles it"** for stale authority (line 673). The matrix routes `stale` to `safe-denial-404` at `:76` and to `authority-unavailable-503` at `:146`; the new S-7 text drops `stale` from its eight-cause list. — UPD-5
6. **"Consumed two ways, selected by the `UseNuGetDeps` property … by default, project references"** (line 535). The selector is `Configuration` (`Release` → packages) with `UseHexalithProjectReferences` as the forcing switch; `release-packages.yml` builds in both modes. — UPD-8
7. **"One `*IdempotencyIntentAdapter` per mutating command"** (line 1583). 13 adapters vs 14 spine mutating operations; adapters key on domain command type, one covering `AddFile`/`ChangeFile`/`RemoveFile`. — UPD-9
8. **"Neither `kind` nor `daprd` appears anywhere in the repository"** (I-3, line 759). Both appear in `7-1-deploy-production-dapr-deny-by-default-access-control.md` (`:56,:115,:166`), which records the live gate as a deferral to Story 7.8. — UPD-10

Two further statements are true-but-over-claimed rather than false: *"a CI test asserts that rejection"* for `dirty`→`ready` on the non-existent `LockLeaseBecameStale` (UPD-15), and *"the denominator is … never a number transcribed here"* in a sentence transcribing 49 twice (UPD-16).

---

## Present-tense claims probed in this pass

| # | Claim (new in this amendment unless marked) | Line | Evidence | Verdict |
| --- | --- | --- | --- | --- |
| 1 | `src/Hexalith.Folders.EventStore` is a deployable project, `ContainerRepository=eventstore` | 1455, 1583 | `csproj:2-7` (`Sdk.Web`, `IsPublishable`, `EnableContainer`, `ContainerRepository`) | Accurate ✓ |
| 2 | It composes EventStore Gateway + DomainService | 1456, 1583 | `csproj:14-17` (project refs / package refs by `HexalithEventStoreFromSource`) | Accurate ✓ |
| 3 | It serves the `eventstore` Dapr app id | 1589 | `AppHost/Program.cs:22`; `FoldersAspireModule.cs:10` | Accurate ✓ |
| 4 | It hosts the A-9 idempotency intent adapters | 1583 | `Program.cs:24`; 13 adapters + builder + catalog | Accurate ✓ |
| 5 | "one adapter per mutating command" | 1583 | 13 adapters vs 14 spine mutating ops; keyed on domain command | **Inaccurate (UPD-9)** |
| 6 | Added to component boundaries, app-id list, both trees | 491, 503, 1455, 1583, 1589 | Present at all five; **FR37–42 + concern #21 still route to the old path**; `EventStore.Tests` added to one tree only | **Partial (UPD-7)** |
| 7 | `(changes_staged, CommitFailed)` → `failed`, unguarded | 240 | `FolderStateTransitions.cs:90-91` | Accurate ✓ |
| 8 | `(inaccessible, ProviderReadinessValidated)` → `ready`, unguarded | 240 | `:107-108` | Accurate ✓ |
| 9 | The other three are rejected with a CI test asserting it | 240 | `:82` covers two; the third's event is not in the enum | **Over-claimed (UPD-15)** |
| 10 | `stagedBy*` has zero occurrences in `src/` | 445 | `grep -rni stagedBy src/` → 0 | Accurate ✓ |
| 11 | `FolderStateTransitions.cs:157` maps `Dirty` → `AwaitingHuman`, test pins it | 434 | `:157`; `FolderStateTransitionsTests.cs:199` | Accurate ✓ |
| 12 | `unknown_provider_outcome` is `awaiting-human` in mapping doc + diagram | 434 | `c6-transition-matrix-mapping.md:44`; `workspace-lifecycle.md:26` | Accurate ✓ |
| 13 | C10 target artifacts do not exist | 356, 1332, 1627, 1630 | No `Caching/`, no `*TenantPrefixedCacheKey*`, no lint job in `ci.yml` | Accurate ✓ |
| 14 | Real C10 gate is `GovernanceCompletenessGateTests` over `cache-key-exceptions.yaml` | 356 | `:1383`, `:1432`; fixture present; **merge-blocking via `contract-spine.yml` (`pull_request`)** | Accurate ✓ (understated — say it blocks) |
| 15 | C10 still asserted as a lint in six other places | 329, 761, 1095, 1743, 1844, 1871 | verbatim | **Self-contradiction (UPD-11)** |
| 16 | I-3 live-sidecar merge gate does not exist | 759 | 0 CI/test code; `run-dapr-policy-conformance-gates.ps1` is static-fixture | Accurate ✓ |
| 17 | `policy-conformance.yml` is schedule-only, cannot block merge | 759 | `:3-6`; suite excluded from `run-baseline-ci-gates.ps1:45` allow-list and from `run-contract-parity-ci-gates.ps1` | Accurate ✓ |
| 18 | "Neither `kind` nor `daprd` appears anywhere in the repository" | 759 | 3+ tracked files, incl. the Story 7.1 deferral record | **False (UPD-10)** |
| 19 | I-3 kind-cluster merge gate still asserted at line 1076 | 1076 | verbatim | **Self-contradiction (UPD-11)** |
| 20 | I-8 rate limiting does not exist at all | 764 | no `RateLimiting/`; no `*TokenBucket*`; only `ProviderRateLimitPosture.cs`, `{GitHub,Forgejo}RateLimitEvidence.cs` | Accurate ✓ |
| 21 | I-8 still counted as shipped in five other places | 788, 1097, 1537, 1582, 1844 | verbatim | **Self-contradiction (UPD-11)** |
| 22 | Only `NotImplementedException` in `src/` is `OctokitGitHubApiClient.cs:60`, a readiness seam | 264 | exactly one hit; inside `GetReadinessAsync:49-63` | Accurate ✓ |
| 23 | Provider write operations are implemented | 264 | `OctokitGitHubApiClient.cs:283,:520`; `ForgejoSmartHttpGitTransport.cs:450` | Accurate ✓ |
| 24 | "No workspace-executor type exists" | 264 | `IWorkspaceCommitExecutor.cs`, `UnavailableWorkspaceCommitExecutor.cs`, `WorkspaceCommitService.cs` | **False (UPD-2)** |
| 25 | `Testcontainers` has zero usage in Folders | 79, 557, 587 | 0 in src/tests/props/csproj | Accurate ✓ |
| 26 | `run-nightly-drift-gates.ps1` + `tests/tools/forgejo-drift/` are the as-built lane | 691, 1329(C12), 1558 | both present | Accurate ✓ |
| 27 | `oasdiff` "exists nowhere in the repository" | 691 | ADR-0003 + 2 runbooks | **False (UPD-4)** |
| 28 | Package management is dual-mode via `UseNuGetDeps` | 535 | `Directory.Build.props:15-22` — selector is `Configuration` | **Inverted (UPD-8)** |
| 29 | `Hexalith*Version` properties own the pins | 535, 1741 | true of `references/Hexalith.Builds/Props/Directory.Packages.props`, not the repo-root file | Accurate w/ ambiguity |
| 30 | S-7 transcribes the matrix §"Canonical Outcomes" exactly | 668 | `authorization-matrix.md:144-146` | Accurate ✓ |
| 31 | S-7 tokens are in `parity-contract.schema.json` | 668 | 7 of 12 absent; three axes missing entirely | **False (UPD-1)** |
| 32 | G1 quote "403 on 49 of 49, 404 on 46 of 49" | 240 | `authorization-matrix.md:341` verbatim; spine `"403":` ×49; oracle 49 ops | Accurate ✓ |
| 33 | The matrix settles stale-authority degraded mode as 503 | 673 | matrix `:76` says 404, `:146` says 503 | **Contradicted artifact (UPD-5)** |
| 34 | `epics.md` has zero references to PD8/PD10/PD11 | 277 | `grep -c` → 0 | Accurate ✓ |
| 35 | All eleven `NFR74`–`NFR84` rows carry `—` for gates | 1752 | `nfr-traceability.md:120-130` | Accurate ✓ |
| 36 | "No Epic 13 story owns NFR74" | 283 | `nfr-traceability.md:120` → `13-2`; `sprint-status.yaml:243` | **False (UPD-3)** |
| 37 | Tree banner demotes the tree to target + names `.slnx` authoritative | 1168 | banner present; `.slnx` disputed by `ScaffoldContractTests:45,124` | **Sound instrument, undisclosed conflict (UPD-12)** |
| 38 | `NOT BUILT` markers cover what was verified | 1332, 1475-1477 | verified siblings left unmarked in the same blocks | **Selective (UPD-6)** |
| 39 | Requirements-mapping banner names the broken rows | 1604 | 2 named, 4 unnamed and still broken | **Partial (UPD-13)** |
| 40 | "enumerates all 13 src projects, 11 test/load projects" (unchanged) | 1769 | tree now 12 src / 11 test; `.slnx` 14 src / 17 test | **False, newly so (UPD-14)** |

---

## Notes for the gate owner

1. **UPD-1 and UPD-2 are the two that must not ship.** Both are in material a downstream owner will act on rather than read: the PD10 owner will skip re-deriving the parity schema because S-7 says it is already pinned, and the 12.4 implementer will author a second commit-executor seam because the story says none exists. Both are one-sentence fixes. The correct sentences are already almost present in the surrounding text.

2. **The half-applied corrections (UPD-11) are the cheapest large win and should land in the same change set as the row edits.** Six C10 sites, one I-3 site, five I-8 sites — each a phrase. Leaving them is worse than the pre-amendment state, because the document now reads as truthful in the rows a reviewer checks and untruthful in the recitals an implementer skims.

3. **Mark completely or mark not at all (UPD-6).** The `NOT BUILT` convention is good and the banner is a genuine improvement over the prior review's recommendation. But a marker inside a block is read as a whole-block verification pass, so `SearchIndexing/`, `WorkspaceWorkflows/`, `CommitWorkflows/`, `ProviderRateLimitChaos/`, `DaprPolicyConformance/`, `tests/tools/policy-conformance/` and `tests/Hexalith.Folders.Tests/{Idempotency,Caching}/` need the same treatment their neighbours got. All were verified absent in this pass.

4. **UPD-5 is the one to route rather than patch.** The architecture cannot reconcile concern #20 against S-4 by citing an artifact that contradicts itself; `authorization-matrix.md` needs `stale` resolved to one outcome in both of its tables, and that is an OQ3/A6b re-approval item, not a documentation edit. Until it is, the document should record the conflict as open rather than record it as settled — which is the convention the rest of this amendment applies well.

5. **Two facts this pass established that the document should adopt, since they strengthen it:** the as-built C10 gate *is* merge-blocking (`contract-spine.yml` triggers on `pull_request`), and the I-3 live gate is a *recorded deferral with a named successor story* (`7-1-…md:115`, "do not silently drop the live-gate requirement"), not an unowned omission. Both replace an absolute with a citation.

6. **Unchanged from the prior review and still true:** REAL-7 is the highest-value open item nobody has touched. `FolderWorkspaceDirtyResolution` is a two-member commit-outcome enum, the four PD11 pairs the document says it discriminates are not guard-discriminated at all, the one pair that is (`unknown_provider_outcome` + `ReconciliationCompletedDirty`, pinned by `FolderStateTransitionsTests.cs:127`) is missing from the guard list, and `c6-transition-matrix-mapping.md:24-27` repeats the same four-pair list — so the defect is now mirrored in the artifact C6 cites as its mapping. Whoever owns the PD11 story will implement against both.
