---
title: 'Reconcile the Folders canonical package cohort'
type: 'bugfix'
created: '2026-11-02'
baseline_commit: 'a1f752fd1755756ae69fe95322b14f3b2fb04ee8'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
tracker_policy: 'no-automatic-transition'
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-llm-instructions.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Strict Folders AppHost.Tests Release compilation selects EventStore 3.132.2 while the selected Tenants host requires 4.0.0 APIs. The catalog model also mixes McpCli 1.1.0 and 2.0.0 outputs. Two bootstrap assertions still expect superseded tenant authority.

**Approach:** Reconcile the authoritative Builds families to EventStore 4.0.0, McpCli 2.0.0, and Tenants 7.0.0 together. Qualify packages rebuilt from exact selected sources in an isolated local feed/cache, rebuild the runtime-selected owners, and reuse the upstream bootstrap corrections.

## Boundaries & Constraints

**Always:** Preserve central package authority, disabled package overrides, analyzers, warnings-as-errors, and the selected-owner build. Keep Platform HEAD/gitlinks, historical approvals, V1Only, unset T0, null activation authority, and tracker backlog unchanged. Its approval binds Platform baseline `3d344e850471b6a21fc4649df7ede9a0390689b1` and only the four revisions in mapping SHA-256 `46a4bef075af48791ec3f0bc4e4336de8597c36406ccad2f3ab5183e438b41cd`; current root siblings never replace that tuple. Separate local package qualification from published-package, release, and operational acceptance.

**Never:** Publish, deploy, activate, push, initialize nested submodules, weaken authorization to compile, reuse stale candidate packages as current-source evidence, rewrite historical evidence, or alter unrelated package families. Preserve tracked nuget.config; use temporary feed configuration.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Strict package lane | Exact local 4/2/7 cohort | Release builds with zero warnings/errors | Fail on divergent catalog or SDK identity |
| Selected owner | Current declared Tenants project | Model reads complete declarations from its rebuilt output | Refuse missing or incompatible output |
| Bootstrap | Canonical global-administrator authority | Canonical scope and permission; no tenant claim | Retain denial cases |
| Approved Platform tuple | Different current root siblings | Approved mapping and controls stay byte-identical | Report drift separately |

</frozen-after-approval>

## Code Map

- `references/Hexalith.Builds/Props/Directory.Packages.props` owns all three family defaults; Tooling generates its platform catalog from that authority.
- `references/Hexalith.Tenants/Directory.Build.targets` enforces the canonical package boundary. Preserve it and `TenantsGlobalAdministratorAuthority`.
- `tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj` builds the selected Tenants owner. `FoldersDevelopmentCatalogCompositionTests.cs` verifies schema, commands, app IDs, and ingress without starting resources.
- `src/Hexalith.Folders.AppHost/FoldersDevelopmentCatalog.cs` loads selected artifacts. Runtime `TenantsServerProjectMetadata` resolves declared repository paths independently of MSBuild root overrides; build that same owner.
- Shared dependency-aware owner preparation already belongs to EventStore.Aspire. A loader defect is unproven; reconcile identities before considering a loader change.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/Bootstrap/` -- reuse canonical fixture corrections from `aea4b76a590be4b1122631d6e210332c24d3b8cb` for hosted-service, transport, and readiness tests. Preserve unrelated documentation, ledgers, and nested gitlinks.
- [x] `references/Hexalith.McpCli/{Directory.Build.props,src/,tests/}` -- selectively apply canonical-scope implementation/tests from `06cfdf25a352034b36041e9ed44da514b167ca5a`. Preserve existing approval records, enrollment vectors, release automation, and historical documents; a wholesale dependency advance would replace published enrollment approval.
- [x] `references/Hexalith.Builds/Props/Directory.Packages.props` -- migrate the three defaults together; extend `Tools/test-authoritative-package-catalog.ps1` to verify their family bindings and override prohibition.
- [x] `eng/package-cohort/README.md` -- record ordered local packing/restoration, isolated cache, producer revisions/diffs, package hashes, and temporary source mapping. Use EventStore `a9317e4b228adfbcdd6686a2f86171b464f34c06`; distinguish committed revisions from working changes.
- [x] `tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj` -- verify both existing selected-owner build paths use the coherent catalog and current runtime root; rebuild AppHost and tests, then run the catalog class.
- [x] `references/Hexalith.Tenants/tests/Hexalith.Tenants.Server.Tests/` -- rebuild and rerun both named failures, the complete hosted-service class, and adjacent authority/transport/readiness classes.
- [x] This spec -- record exact commands, counts, source/package identities, and remaining blockers; compare protected bytes before/after without rewriting prior receipts.

**Acceptance Criteria:**
- Given rebuilt local packages, when strict Release AppHost and AppHost.Tests builds run, then both pass with package-mode SDK dependencies and zero warnings/errors.
- Given the runtime-selected current owners, when the catalog class runs, then every case passes with complete inventory and existing ingress/schema assertions intact.
- Given rebuilt Tenants tests, when both prior failures and surrounding classes run, then canonical authority assertions pass without weakening negative cases.
- Given any result, when recorded, then Platform approvals, controls, tracker, and historical evidence are unchanged and local proof makes no publication claim.

## Implementation Notes

2026-11-02: The user approved this spec and continued implementation. The frozen intent is unchanged.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| BH1 | false | Producer pack invokes Build; the new family versions and fresh absolute NuGet reference paths invalidate prior compiler inputs. No stale-source result was demonstrated. Reject the claimed current failure. |
| BH2 | low | The reproduction lacks an executable source-identity preflight, so another checkout can produce different artifacts under the same labels. Patch the reproduction with revision/working-file checks. |
| BH3 | medium | Protected receipts omit their path-selection/hash algorithm and verifier; developers cannot independently reproduce the aggregate comparison. Retain a compact verifier and explicit exclusions/inputs. |
| BH4 | low | McpCli.Abstractions contains a shipped analyzer DLL outside the 17 main DLL comparisons. Add its archive/output hashes to package provenance. |
| BH5 | low | Consumer receipts hash dependencies but omit the executed Folders test and AppHost binaries. Bind those outputs to the existing commands. |
| BH6 | medium | Family loops validate surviving entries only; deleting Tenants.Server is not detected by the required-package list. Add the required cohort identities to that inventory. |
| BH7 | low | The new platform predicate duplicates the exact selected SDK predicate. Current grammars agree; use the shared predicate to keep future CatalogBuilder/QueriesController boundaries aligned. |
| BH8 | medium | The Core/CLI ledger omits the MCP suite, whose current cases use ordinary scopes. Add canonical describe/send/query/conflict/schema cases and run that suite. |
| BH9 | medium | New platform fixtures have no TenantProperty, leaving mapped-payload filling and conflict rejection unexercised for that scope. Add omitted/matching/conflicting cases. |
| BH10 | low | The CLI command case asserts response tenant and call count but not outbound scope/routing. Assert the captured command request. |
| BH11 | low | Public PlatformScope summaries omit rules enforced by CatalogBuilder/ResolveTenant. Add concise grammar, FixedTenant conflict, and precedence documentation. |
| VG1 | medium | Pre-verified gap: positive tests normalize the session but never mixed-case per-call tenants. Add MCP cases with matching lowercase session and absent session; assert canonical tenant. |
| EC1 | medium | The preserved 5.10.0 enrollment targets system; the baseline executor also forwards it unchanged, and the selected baseline EventStore a9317e4 already rejects public system. Defer this pre-existing operational/enrollment incompatibility; preserve approval and document the unresolved boundary. |

## Verification

- `dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -m:1 -nr:false -warnaserror` using the isolated restored cohort.
- Run the rebuilt xUnit assembly with `-class Hexalith.Folders.AppHost.Tests.FoldersDevelopmentCatalogCompositionTests`; run Tenants classes individually from rebuilt Debug/source output.
- Run Builds `Tools/test-authoritative-package-catalog.ps1` and `Tools/validate-platform-version-catalog.ps1`; compare protected snapshots and run `git diff --check` per changed owner.
- Baseline recheck reproduced CS0246 in TenantsGlobalAdministratorAuthority. The existing historical inventory verifier separately fails `Pending manifest bytes changed` before implementation; preserve/report that blocker rather than repinning it.

### Local qualification result — 2026-11-02

The frozen intent and frontmatter status are unchanged. Implementation and local
qualification are complete; this receipt grants no publication, release,
deployment, activation, or operational acceptance.

- Central Builds defaults now select EventStore 4.0.0, McpCli 2.0.0, and Tenants
  7.0.0 together. Family tests cover every central member and three real NU1013
  override-refusal probes. `CentralPackageVersionOverrideEnabled=false` remains.
- Tenants reuses only the hosted-service, transport, and readiness fixture changes
  from `aea4b76a590be4b1122631d6e210332c24d3b8cb`. Canonical scope/permission and
  absent tenant claims pass; denial cases remain intact.
- McpCli reuses the canonical scope source/tests from
  `06cfdf25a352034b36041e9ed44da514b167ca5a` and uses the 4.0.0 SDK/2.0.0 package
  identity, while retaining its approved Tenants.Contracts 5.10.0 enrollment pin,
  vectors, approvals, release automation, and historical documents.
- EventStore producer is exact clean `a9317e4b228adfbcdd6686a2f86171b464f34c06`.
  Builds starts at `13f503c`; McpCli starts at `829a98d`; Tenants starts at
  `93a5aa83`. These are committed starting revisions, not commits containing the
  new working changes. Full revisions, working paths (including both new McpCli
  test files), source patches, and patch SHA-256 values are retained in
  [sources.json](../../eng/package-cohort/qualification/sources.json).
- Fresh local packing produced 11 EventStore 4.0.0 packages,
  McpCli.Abstractions 2.0.0, and five Tenants 7.0.0 packages. All 17 package DLLs
  match selected producer outputs. Individual package/assembly hashes are in
  [packages.json](../../eng/package-cohort/qualification/packages.json).
- Release runtime manifests verify package-mode EventStore 4.0.0 SDK edges and
  coherent McpCli/Tenants identities for AppHost, AppHost.Tests, and the current
  selected Tenants owner. Both existing selected-owner build paths rebuilt
  `references/Hexalith.Tenants/src/Hexalith.Tenants/Hexalith.Tenants.csproj`.
  No loader or selected-owner project change was necessary.

The isolated run used `NUGET_PACKAGES=/tmp/folders-canonical-cohort-20261102/cache`
and `NUGET_HTTP_CACHE_PATH=/tmp/folders-canonical-cohort-20261102/http-cache`.
Its temporary source mapping is retained separately from tracked `nuget.config`.
The [ordered reproduction](../../eng/package-cohort/README.md) and
[command ledger](../../eng/package-cohort/qualification/commands.jsonl) retain
exact working directories, arguments, environment, exit codes, durations, and
adjacent logs, including diagnostic failures.

Both strict commands below passed with zero warnings/errors, using the isolated
cache and feed:

```bash
dotnet build src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:RestoreConfigFile=/tmp/folders-canonical-cohort-20261102/nuget.config -m:1 -nr:false -warnaserror
dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -p:RestoreConfigFile=/tmp/folders-canonical-cohort-20261102/nuget.config -m:1 -nr:false -warnaserror
dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Release/net10.0/Hexalith.Folders.AppHost.Tests.dll -class Hexalith.Folders.AppHost.Tests.FoldersDevelopmentCatalogCompositionTests -parallelMode none
```

The unchanged catalog class passed 1/1, retaining complete inventory and existing
app-ID, ingress, schema, and denial-policy assertions without starting resources.

From `references/Hexalith.Tenants`, the rebuild command passed with zero
warnings/errors:

```bash
dotnet build tests/Hexalith.Tenants.Server.Tests/Hexalith.Tenants.Server.Tests.csproj -c Debug --no-incremental -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:RestoreConfigFile=/tmp/folders-canonical-cohort-20261102/nuget.config -m:1 -nr:false -warnaserror
```

Direct assembly execution with `-method` passed each named prior failure:
`StartAsync_with_configured_userId_sends_BootstrapGlobalAdmin_command` and
`Development_symmetric_credential_is_a_short_lived_delegated_token_for_the_configured_administrator`.
The complete class and adjacent `-class` runs passed:

| Tenants class | Passed | Failed / skipped |
| --- | ---: | ---: |
| TenantBootstrapHostedServiceTests | 205 | 0 / 0 |
| TenantsGlobalAdministratorAuthorityTests | 25 | 0 / 0 |
| TenantBootstrapCommandTransportTests | 32 | 0 / 0 |
| TenantBootstrapReadinessBudgetTests | 8 | 0 / 0 |
| TenantBootstrapBoundCommandTests | 29 | 0 / 0 |
| TenantBootstrapReceiptTests | 6 | 0 / 0 |
| TenantBootstrapRedirectTests | 21 | 0 / 0 |
| BootstrapConfigurationTests | 2 | 0 / 0 |

This covers 328 distinct cases and the two separately repeated named cases.
McpCli Core passed 731 total (727 passed, four expected skips); CLI passed
487 total (485 passed, two Windows-only skips). The Core suite was repeated
sequentially after an earlier diagnostic run overlapped its adjacent CLI build.
The repeat is the final Core evidence. CLI also passed a sequential confirmation
(487 total, two Windows-only skips). Final Folders and Tenants build/test checks
and both McpCli confirmation runs completed sequentially.

Builds checks passed:

```bash
pwsh -NoLogo -NoProfile -File Tools/test-authoritative-package-catalog.ps1
pwsh -NoLogo -NoProfile -File Tools/validate-platform-version-catalog.ps1
```

The first standalone platform-catalog check ran before the fresh Aspire package
was restored into the isolated cache and returned NU1102. Its later qualified
run and normal Tooling builds passed at EventStore.Aspire/4.0.0. This ordering
failure remains in the ledger and is not acceptance evidence.

The [protected comparison](../../eng/package-cohort/qualification/protected-comparison.json)
passed for 64,374 tracked paths across all six inspected repositories. Every
repository HEAD and gitlink is unchanged. Platform approval baseline
`3d344e850471b6a21fc4649df7ede9a0390689b1`, mapping digest
`46a4bef075af48791ec3f0bc4e4336de8597c36406ccad2f3ab5183e438b41cd`, prior
receipts, V1Only, unset T0, null activation authority, tracker backlog, and tracked
NuGet configurations remain unchanged. Only the declared source/fixture/catalog
changes and the new qualification artifacts are excluded from protected hashes.
Per-owner `git diff --check` passed.

Remaining external blocker: the preserved historical inventory command
`python3 _bmad-output/implementation-artifacts/evidence/verify-story-1-17-current-inventory-2026-11-02.py`
still exits 1 with `ValueError: Pending manifest bytes changed`, as it did before
implementation. It was not repinned or rewritten. Published 4/2/7 availability,
release acceptance, and operational acceptance are not established by this local
qualification. No package was published and no resource was deployed/activated.

### Final parent verification and local commit capture

All review patch entries (BH2–BH11 and VG1) were resolved and covered by focused
checks, then a fresh sequential full qualification. EC1 is recorded as an
append-only pre-existing enrollment incompatibility; BH1 was rejected on its
recorded evidence. The final [result](../../eng/package-cohort/qualification/final/result.json)
records 45 passing commands, both strict Release builds at zero warnings/errors,
selected-owner model 1/1, both prior failures independently passing, 328 distinct
Tenants cases, Core 735 total (four existing skips), CLI 487 (two existing skips),
and MCP 72/72. Matrix coverage and protected-byte checks pass. All 17 package DLLs
and the shipped analyzer match their source outputs.

The workflow captured the reviewed owner snapshots in exact local commits,
recorded in [owner-commits.json](../../eng/package-cohort/qualification/owner-commits.json).
Those commits change only the qualified path sets and retain identical worktree
bytes. The verifier checks the exact commit diff and each qualified file hash;
root capture may advance only those three cohort gitlinks and the listed
qualification/append-only tracking artifacts. Platform retains its exact HEAD,
gitlinks, mapping, approvals, routing controls and backlog. No release or
operational qualification is inferred from these local commits.
