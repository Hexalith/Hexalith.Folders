# Reviewer Gate — Code-Reality / Brownfield Lens

**Target:** `_bmad-output/planning-artifacts/architecture.md` (1857 lines, classic architecture doc)
**Repo HEAD:** `de281e7` (`main`, working tree clean at session start)
**Date:** 2026-09-16 · **Run:** VALIDATE (no edits applied to architecture.md or any project file)

## Verdict

**FAIL** — 4 critical findings.

The document's *new* material (PD8/PD10/PD11, the 2026-09-15 amendment) is honestly labelled: the Release Authority Overlay reality table at lines 238–243 and the Current Delivery Posture at lines 247–253 do the job they were added for, and the four lenses that ran on 2026-09-15 got that fix landed. The honesty problem has moved. It now lives in the **older, unamended strata** of the document — the "Complete Project Directory Structure", "Requirements to Structure Mapping", the I-3/I-8/C10 enforcement claims, and the "Architecture Validation Results" ✅ block — which were written in 2026-05 against a design and have never been re-verified against the code that actually shipped. Those sections assert shipped structure and shipped CI mechanisms in the present tense, carry no target-state label, and are contradicted by the tree.

Separately, and more seriously for a gate whose whole purpose is honesty: **the verified reality table itself contains three verifiable inaccuracies** (REAL-5, REAL-9, REAL-10), one of which understates the risk in the dangerous direction.

One structural fact dominates: `src/Hexalith.Folders.EventStore/` — a real, deployable, AppHost-composed project that *is* the `eventstore` Dapr app and hosts the A-9 idempotency-intent adapters — has **zero occurrences** in 1857 lines.

Scope note: the three known `ScaffoldContractTests` failures over `Hexalith.Folders.EventStore` project / `.slnx` / build-config drift are pre-existing and tracked; they are cited in REAL-1 only as corroboration that the drift is known to the codebase, not as a new defect.

---

## Findings

### REAL-1 — CRITICAL — An entire deployable project (`Hexalith.Folders.EventStore`) and the service-topology change it makes have no place in the document

`grep -n "Hexalith.Folders.EventStore" architecture.md` → **0 hits**.

The project is not a stub. `src/Hexalith.Folders.EventStore/` contains a full ASP.NET host plus **15 idempotency-intent adapters** — `CreateFolderIdempotencyIntentAdapter.cs`, `CommitWorkspaceIdempotencyIntentAdapter.cs`, `LockWorkspaceIdempotencyIntentAdapter.cs`, `MutateFilesIdempotencyIntentAdapter.cs`, … plus `FoldersCanonicalIntentBuilder.cs` and `FoldersIdempotencyIntentAdapterCatalog.cs`. This is precisely the A-9 mechanism the document specifies at line 665: *"a registered domain adapter supplies EventStore a versioned trusted descriptor derived from `x-hexalith-idempotency-equivalence`"*. The document specifies the mechanism and never says where it lives.

It is also a **service-topology change**. architecture.md line 1548:

> `- `eventstore` — sibling Hexalith.EventStore (existing)`

Code: `src/Hexalith.Folders.AppHost/Program.cs:22`

```csharp
IResourceBuilder<ProjectResource> eventStoreProject = builder.AddProject<Projects.Hexalith_Folders_EventStore>(FoldersAspireModule.EventStoreAppId);
```

The `eventstore` app id is now served by a **Folders-owned project**, not the sibling submodule. `src/Hexalith.Folders.EventStore/Program.cs:14-24` builds a `WebApplication` that calls `AddEventStoreServer(...)`, `AddEventStoreSignalR(...)` **and** `AddFoldersIdempotencyIntentAdapters()`. Line 554's *"EventStore (`AppId=eventstore`, gateway-only) … Composed via the platform Aspire helpers (`AddHexalithEventStore` …)"* no longer describes the AppHost.

Downstream consequences inside the document, all unlabelled:

| Doc claim | Line | Reality |
| --- | --- | --- |
| Tree lists `src/Hexalith.Folders/Idempotency/{IdempotencyKeyValidator,PayloadEquivalence,IdempotencyRecordStore}.cs` | 1293–1296 | `src/Hexalith.Folders/` has no `Idempotency/` directory at all (`ls src/Hexalith.Folders/` → Aggregates, Authorization, Observability, Projections, Providers, Queries). Only `IdempotencyRecordStore.cs` is labelled aspirational (line 251). |
| FR37–FR42 map to `src/Hexalith.Folders/Idempotency/` | 1571 | Same — directory absent; the real adapters are in `src/Hexalith.Folders.EventStore/`. |
| Concern #21 maps to `Idempotency/IdempotencyRecordStore.cs` (durable) | 1597 | Same. |
| *"Repo-root-to-leaf-file directory tree enumerates all **13 src projects**, **11 test/load projects**"* | 1724 | The tree enumerates **11** src projects (Contracts, Folders, Server, Client, Cli, Mcp, UI, Workers, Aspire, AppHost, Testing — lines 1189–1472). `src/` holds **13** directories and `Hexalith.Folders.slnx` declares **14** src projects. The two missing from the tree are exactly `Hexalith.Folders.EventStore` and `Hexalith.Folders.ServiceDefaults`; the 14th is `src/Hexalith.Folders.Client/Generation/Shared/Hexalith.Folders.Client.Generation.Shared.csproj`. Test projects: the tree lists 10 + `tests/load` = 11; `.slnx` declares **17** under `/tests/`. |

Why critical: line 1827 instructs implementers to *"Respect project structure and boundaries; component references must follow the dependency direction in §'Component Boundaries.'"* §Component Boundaries (1538–1544) has no row for this project, so an implementer adding the next intent adapter has no sanctioned home for it and the dependency direction for `Hexalith.Folders.EventStore → Hexalith.EventStore.Gateway/DomainService` (`src/Hexalith.Folders.EventStore/Hexalith.Folders.EventStore.csproj:14-15`) is undeclared. The three standing `ScaffoldContractTests` reds are the codebase noticing the same gap.

---

### REAL-2 — CRITICAL — I-3 asserts a Dapr policy-conformance mechanism (kind cluster + `daprd` + property-based generator + merge block) that does not exist anywhere, in the present tense, unlabelled

architecture.md line 731 (I-3):

> **Validated by a `dapr-policy-conformance` CI job (in I-5 gate list)** running `daprd` in a kind cluster with the production policy YAML and executing a **negative test suite** that asserts unauthorized `(sourceAppId, targetAppId, operation)` triples receive `403` on every `invoke` and `pubsub` topic; a property-based generator over the triple space provides exhaustive negative coverage. **Block merge on policy YAML changes without corresponding negative test additions**

Repeated as a shipped test pattern at line 1044 and counted among the shipped defence-in-depth gates at line 1799 and the merge-blocking set at line 1826.

Code reality:

- `grep -rln "kind cluster\|kind create cluster\|daprd" .github tests/tools tests/Hexalith.Folders.IntegrationTests` → **0 files**.
- The real gate is a static-fixture xUnit suite. `tests/tools/run-dapr-policy-conformance-gates.ps1:24-32` enumerates its eight test methods, the last of which names the mechanism outright: `DaprPolicyConformanceTests.WorkflowAndScriptShouldWireOfflineDaprPolicyConformanceGate`. The rest assert YAML shape (`ProductionAccessControlPolicyShouldBeDenyByDefaultAndMatchFixtureProvenance`, `PolicyConformanceFixtureShouldCoverAllowedAndDeniedTriples`).
- It cannot block merge: `.github/workflows/policy-conformance.yml:3-6` triggers on `schedule` (`cron: '43 2 * * *'`) and `workflow_dispatch` only — there is no `pull_request` trigger. Its workflow-dispatch input is literally `policy_mode: static-plus-live-reference | static-only`.
- The tree's homes for it do not exist: `tests/Hexalith.Folders.IntegrationTests/DaprPolicyConformance/` (line 1496) and `tests/tools/policy-conformance/` (line 1517) are both absent; the tests live in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformance/`, and `tests/tools/` holds `forgejo-drift/`, `parity-oracle-generator/`, `pattern-examples/`.

Why critical: the document's own rationale column for I-3 reads *"production policy without negative tests is theater (the first time the policy ships should not be in production)"*. An implementer or a release reviewer reading I-3 will believe a live-sidecar negative suite blocks merges. It does not. This is the single largest gap between an asserted security control and its implementation in the document, and nothing labels it.

---

### REAL-3 — CRITICAL — I-8's per-tenant token buckets and the 429 chaos gate do not exist, and are counted as shipped

architecture.md line 736 (I-8):

> **Per-provider token bucket scoped per-tenant for user-driven calls; per-provider global bucket for background reconciliation; backoff with jitter; reconciliation queue feeds C12 drift detection on sustained 429s.** **Chaos test in CI injects synthetic 429 storms** to verify the reconciliation queue does not unbounded-grow and that C12 drift signal fires within SLO

Also asserted as shipped at line 1712 (*"provider rate-limit chaos test (I-8)"* under NFR coverage ✅), line 1726 (*"CI gate set expanded by 7 new gates"*), line 1799 (defence-in-depth gate list), and line 1065 (*"CI gates: … provider-rate-limit chaos test"*).

Code reality:

- `ls src/Hexalith.Folders.Workers/` → `FoldersWorkersModule.cs`, `Program.cs`, `Properties`, `RepositoryProvisioning/`, `SemanticIndexing/`, `Tenants/`. There is **no `RateLimiting/` directory**, no `PerTenantTokenBucket.cs`, no `GlobalReconciliationBucket.cs` (tree lines 1435–1437).
- `find src -iname "*TokenBucket*"` → 0 source hits. What exists is read-only *evidence* modelling: `src/Hexalith.Folders/Providers/Abstractions/ProviderRateLimitPosture.cs`, `src/Hexalith.Folders/Providers/GitHub/GitHubRateLimitEvidence.cs`, `src/Hexalith.Folders/Providers/Forgejo/ForgejoRateLimitEvidence.cs` — posture reporting, not enforcement.
- `tests/Hexalith.Folders.IntegrationTests/ProviderRateLimitChaos/` (tree line 1497) does not exist; `ls tests/Hexalith.Folders.IntegrationTests/` → `AdapterParity`, `ContextSearch`, `EndToEnd`, `MixedSurfaceHandoff`, `Parity`, plus loose test files.

The same omission runs through the whole Workers tree: `WorkspaceWorkflows/` (with `WorkspacePreparationWorkflow.cs`, `WorkspaceCleanupWorkflow.cs`) and `CommitWorkflows/` (`CommitWorkflow.cs`, `CommitReconciler.cs`) at lines 1424–1433 do not exist either. Only `WorkingCopyManager.cs` inside that block carries the aspirational label (line 251) — its siblings do not, so the label reads as scoped to that one file.

Why critical: I-8 is the sole tenant-fairness control against provider quota exhaustion (`"Prevents one tenant DOS'ing the provider for others"`). It is written as a decision that has been taken and enforced, it is counted in the NFR-coverage ✅ block, and none of it is built.

---

### REAL-4 — CRITICAL — C10 cache-key enforcement is real but lives nowhere the document says, and the artifact it pins does not exist

architecture.md line 349 (Exit Criteria Operations Plan, C10 row):

> | C10 | Architect | Architecture team | `.github/workflows/ci.yml` (lint job) + `Hexalith.Folders/Caching/TenantPrefixedCacheKey.cs` | Phase 1 entry | CI lint gate; Roslyn analyzer or grep-based |

Same artifact named at line 322 (C10 exit-criterion row), line 1298 (tree), line 1589 (concern #13 structure mapping), and line 1024 (*"CI lint check enforces this as a hard build-time gate"*).

Code reality:

- `find src tests -iname "*TenantPrefixedCacheKey*" -o -iname "*CacheKey*"` → **0 source files**. `src/Hexalith.Folders/` has no `Caching/` directory.
- `.github/workflows/ci.yml` has six jobs — `baseline-build-and-unit-gates`, `contract-and-parity-gates`, `security-and-redaction-gates`, `capacity-smoke-gates`, `accessibility-gates`, `e2e-gates` — and **no lint job**; `grep -n "cache-key\|CacheKey\|cache_key" .github/workflows/ci.yml` → 0 hits.
- The enforcement that *does* exist is a repository-scanning xUnit gate: `tests/Hexalith.Folders.Contracts.Tests/OpenApi/GovernanceCompletenessGateTests.cs:1383` `CacheKeyExceptionManifestIsReviewedAndCurrentRepositoryHasNoTenantDataCacheKeysWithoutScope()` plus `:1432` `CacheKeyExceptionApprovalStateFailsClosedForExpiredOrUnknownStatus()`, driven by `tests/fixtures/cache-key-exceptions.yaml`.
- The authoritative governance record already says so and contradicts architecture.md — `docs/exit-criteria/c0-c13-governance-evidence.yaml:188-195`:
  ```yaml
    - criterion_id: C10
      title: Tenant-prefixed cache-key evidence
      status: approved
      artifact_path: tests/fixtures/cache-key-exceptions.yaml
      verification_command: .\tests\tools\run-governance-completeness-gates.ps1 -SkipRestoreBuild
  ```

Why critical rather than medium: line 354 states *"A criterion with no entry under 'Decision Authority' or 'Artifact Location' is **not yet ready to ship**; CI may add a `exit-criteria-presence` gate that fails the release pipeline when an artifact link is missing."* C10's artifact link points at a file that does not exist, and the document is the cited authority for where the gate lives. An implementer told to *"Tenant-prefix every cache key"* using `TenantPrefixedCacheKey.cs` (line 1054, 1110) will author a helper that duplicates nothing and is caught by no analyzer, and will not find the exception manifest that the real gate fails closed on.

---

### REAL-5 — HIGH — The verified reality table understates the PD11 lifecycle gap: two of the five added transitions are not rejected, they are *accepted onto the other branch*

architecture.md line 240:

> | **PD11** lifecycle | `FolderStateTransitions.cs` **rejects four of the five transitions this correction adds**, and a CI test asserts that rejection. …

The five transitions PD11 adds (per the C6 table, lines 399–411, and the memlog change record) and what `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:52-125` actually does:

| PD11-added row | Doc line | Code behaviour | Rejected? |
| --- | --- | --- | --- |
| `changes_staged` → `dirty` on retryable `CommitFailed` | 399 | `FolderStateTransitions.cs:90-91` — `(ChangesStaged, CommitFailed, null) => Failed`, unconditional, no guard | **No — accepted, routed to `failed`** |
| `changes_staged` → `inaccessible` on revocation | 400 | no arm → `_ => null` → `StateTransitionInvalid` (`:125-134`) | Yes |
| `dirty` → `changes_staged` on originating-task `WorkspaceLocked` | 404 | no arm → rejected | Yes |
| `dirty` → `ready` on `LockLeaseBecameStale` | 405 | event does not exist in `FolderWorkspaceLifecycleEvent.cs` (23 members, no stale member) | Vacuously |
| `inaccessible` → `dirty` on `ProviderReadinessValidated` within C3 window | 410 | `FolderStateTransitions.cs:107-108` — `(Inaccessible, ProviderReadinessValidated, null) => Ready`, unconditional | **No — accepted, routed to `ready`** |

So two of the five are silently taken down the *other* branch. That is exactly the hazard the document itself names 200 lines later, at line 437: *"an implementation that handles one branch and silently takes the other path still satisfies 'this pair has a defined outcome' while destroying staged work."* The reality table describes a fail-closed posture ("rejects", "a CI test asserts that rejection") where the code has a fail-*open* one for the two guard branches that protect staged work.

`EveryUnlistedStateEventPairShouldRejectWithoutChangingState` (`tests/Hexalith.Folders.Tests/Aggregates/Folder/FolderStateTransitionsTests.cs:82`) does assert rejection — but only for pairs with *no* arm, which is rows 2–3, not rows 1 and 5.

**Second, unlabelled divergence in the same section — the operator dispositions.** Two state-catalog rows are written in the present tense and contradicted by code, with no target-state marker and no row in the reality table:

- Line 369 — `dirty`: *"`degraded-but-serving` while the originating task can still resume or the workspace is clean; `awaiting-human` once staged changes are orphaned"*. Code: `FolderStateTransitions.cs:157` `Dirty => AwaitingHuman` — unconditional; mirrored at `src/Hexalith.Folders.UI/Services/DispositionLabelMapper.cs:33` `LifecycleState.Dirty => OperatorDispositionLabel.Awaiting_human`; pinned by `FolderStateTransitionsTests.cs:199 OperatorDispositionShouldMatchC6StateCatalog`. The only conditional arm in either file is `Ready` (`:152-154`, `DispositionLabelMapper.cs:28-30`). `grep -rn "PD11" src tests` → **0 hits**.
- Line 373 — `unknown_provider_outcome`: *"`auto-recovering` (automatic bounded reconciliation in progress; `awaiting-human` begins only at `reconciliation_required` — 2026-07-15)"*. Code: `FolderStateTransitions.cs:161` `UnknownProviderOutcome => AwaitingHuman`.

And line 438 asserts the drift is impossible:

> **Operator-disposition mapping** is sourced from this table; `Hexalith.Folders.UI/Services/DispositionLabelMapper.cs` is generated from it (or hand-written and tested against it) **so F-4 console labels cannot drift from architecture.**

It has drifted on two of eleven states, in both the domain and the UI copy, and the test pins the drifted values. The run memlog records both divergences as `(constraint)` — deliberate target state — which is a legitimate decision; the defect is that the document nowhere carries the label for them.

---

### REAL-6 — HIGH — "Current Delivery Posture" and Story 12.4's charter both point at a `NotImplementedException` that is not in the Git write path

architecture.md line 251:

> … uploaded file content is base64-decoded then discarded; **the provider Git write path throws `NotImplementedException`**; no component transitions a task to `completed`; `/project` replay is a hardcoded 501 …

and line 262:

> - **12.4** Real Git commit executor + durable provider-write orchestration (**replace the `NotImplementedException` workspace executor methods**; …)

Code reality — the provider write paths are **implemented**, and the workspace executors do **not** throw:

- GitHub: `src/Hexalith.Folders/Providers/GitHub/OctokitGitHubApiClient.cs:65-120` `CreateRepositoryAsync` calls real `_client.Repository.Create(...)`; `:283` `StageFileChangesAsync` and `:520+` `CommitAsync` perform real `git/commits` + ref compare-and-set HTTP.
- Forgejo: `src/Hexalith.Folders/Providers/Forgejo/ForgejoSmartHttpGitTransport.cs:450` performs a real LibGit2Sharp push — `repository.Network.Push(remote, commit.Id.Sha, request.Target.FullRef, options);`.
- The actual gap is **composition**, and it fails closed by *return value*, not by throwing. `src/Hexalith.Folders.Server/FoldersServerServiceCollectionExtensions.cs:63-67` registers `UnavailableWorkspacePathPolicyEvidenceProvider`, `UnavailableWorkspaceFileContentStore`, `UnavailableWorkspaceFileDeleteOperationStore`, `UnavailableWorkspaceCommitExecutor`. `src/Hexalith.Folders/Aggregates/Folder/UnavailableWorkspaceCommitExecutor.cs:7-11` returns `WorkspaceCommitExecutionResult.KnownFailure(ProviderFailureCategory.UnsupportedProviderCapability.ToCategoryCode())`. No adapter connects `IGitProvider` to `IWorkspaceCommitExecutor` / `IWorkspaceFileContentStore` in `src/`.
- The one live `NotImplementedException` on the GitHub path is a **readiness probe, not a write**: `OctokitGitHubApiClient.cs:49-63` `GetReadinessAsync` throws, reachable from `GitHubProvider.cs:138` in `DiscoverCapabilitiesAsync`. That is a genuine defect the document does not mention.

`/project` 501 checks out (`src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:93`, `FoldersDomainServiceRequestHandler.cs:95`), and the `InMemoryFolderRepository` / Production-boot-assertion description (line 251) is accurate (`src/Hexalith.Folders/Aggregates/Folder/InMemoryFolderRepository.cs:29`; `src/Hexalith.Folders.Server/Program.cs:24-29,59-70`).

Why high: Story 12.4's acceptance is written against a code shape that does not exist. A developer picking it up will search for `NotImplementedException` in the executors, find none, and either conclude the story is done or rewrite the wrong seam. The honest statement is "the Server composition binds every workspace mutation seam to `Unavailable*` fail-closed stubs; the provider adapters behind them are implemented but unwired."

---

### REAL-7 — HIGH — `FolderWorkspaceDirtyResolution` exists, but it discriminates a different pair than the four the document names, and the one real guard pair is absent from the guard list

architecture.md line 436:

> `Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs` implements this matrix as a switch expression over **`(currentState, eventType, resolution)`** returning `FolderWorkspaceTransitionResult`, where `resolution` is the `FolderWorkspaceDirtyResolution` discriminator (per Step 5 §"Process Patterns"). PD11 makes **four pairs guard-discriminated** … `changes_staged` + `CommitFailed` …, `inaccessible` + `ProviderReadinessValidated` …, `dirty` + `WorkspaceLocked` …, and `dirty` + `LockLeaseBecameStale` …

The 2026-09-15 gate fix got the *shape* right — both types exist:

- `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceTransitionResult.cs:3-9` — `sealed record FolderWorkspaceTransitionResult(bool IsAccepted, FolderWorkspaceLifecycleState? CurrentState, FolderWorkspaceLifecycleEvent AttemptedEvent, FolderWorkspaceLifecycleState? NextState, FolderResultCode Code, FolderOperatorDisposition? OperatorDisposition)`
- `src/Hexalith.Folders/Aggregates/Folder/FolderStateTransitions.cs:47-50` — `Transition(FolderWorkspaceLifecycleState? currentState, FolderWorkspaceLifecycleEvent attemptedEvent, FolderWorkspaceDirtyResolution? dirtyResolution = null)`

But the *semantics* are misattributed. `src/Hexalith.Folders/Aggregates/Folder/FolderWorkspaceDirtyResolution.cs:6-12` has exactly two members — `CommitConfirmed` (`"commit_confirmed"`) and `CommitRejected` (`"commit_rejected"`) — and the only arms that consume it are `FolderStateTransitions.cs:110` and `:112`:

```csharp
(UnknownProviderOutcome, ReconciliationCompletedDirty, FolderWorkspaceDirtyResolution.CommitConfirmed) => Committed,
(UnknownProviderOutcome, ReconciliationCompletedDirty, FolderWorkspaceDirtyResolution.CommitRejected)  => Failed,
```

So:

1. **None of the four PD11 pairs the document names is guard-discriminated in code**, and the type's name ("DirtyResolution") describes commit-outcome reconciliation, not dirty-workspace resolution. An implementer told `resolution` "is the `FolderWorkspaceDirtyResolution` discriminator" will try to extend a two-member commit-outcome enum to carry "originating task vs. any other principal" and "staged content vs. clean" — four orthogonal guards on one enum.
2. **The one pair that genuinely is guard-discriminated — `unknown_provider_outcome` + `ReconciliationCompletedDirty` — is missing from the C6 matrix's guard list at line 436**, and the matrix rows that cover it (lines 413–414) present the split as if driven by the event, not a guard. There is even a test pinning the guard requirement: `FolderStateTransitionsTests.cs:127 UnknownOutcomeDirtyReconciliationShouldRejectWithoutExplicitResolution`.
3. The cross-reference *"(per Step 5 §'Process Patterns')"* is dangling — §Process Patterns (lines 941–1044) never mentions `FolderWorkspaceDirtyResolution`.
4. Related asymmetry: the doc's `reconciliation_required` → `committed` row (line 417) is qualified *"(commit confirmed upstream)"*, but `FolderStateTransitions.cs:119-120` accepts `(ReconciliationRequired, ReconciliationCompletedDirty, null) => Committed` with **no** guard and has no `→ failed` counterpart — an unguarded "apply upstream truth" arm the matrix implies is conditional.

---

### REAL-8 — HIGH — "Requirements to Structure Mapping" routes five FR blocks and three cross-cutting concerns to paths that do not exist

The section is presented as complete and verified: line 1706 *"no FR block is unmapped"*, line 1785 checkbox *"[x] Requirements to structure mapping complete"*. Verified against the tree:

| Mapping | Line | Reality |
| --- | --- | --- |
| FR1–FR3 → `docs/contract-terms.md` | 1565 | Does not exist. The real contract vocabulary is 19 files under `docs/contract/` (`contract-spine-foundation.md`, `file-context-contract-groups.md`, `authorization-matrix.md`, …), a directory the document never names. |
| FR43–FR46 → `src/Hexalith.Folders/Projections/WorkspaceStatus/` | 1572 | `ls src/Hexalith.Folders/Projections/` → `FolderAccess`, `FolderList`, `SemanticIndexing`, `TenantAccess`. The real workspace-status read model is `src/Hexalith.Folders/Queries/Folders/IWorkspaceStatusReadModel.cs` + `InMemoryWorkspaceStatusReadModel.cs`. |
| FR53–FR57 → `src/Hexalith.Folders/Projections/Audit/` | 1579 | Absent; audit read models live under `src/Hexalith.Folders/Queries/Audit/`. |
| FR37–FR42 → `src/Hexalith.Folders/Idempotency/` | 1571 | Absent (see REAL-1). |
| Concern #13 → `Hexalith.Folders/Caching/TenantPrefixedCacheKey.cs` | 1589 | Absent (see REAL-4). |
| Concern #17 → `Observability/FolderAuditSanitizer.cs` | 1593 | **Exists** ✓ (`src/Hexalith.Folders/Observability/FolderAuditSanitizer.cs`). |
| Concern #18 → `Authorization/AuthorizationOrder.cs` | 1594 | **Exists** ✓. |
| FR58 → `src/Hexalith.Folders/Search/` + `Projections/Search/` | 1580, 1269, 1292 | Neither exists — **but both are labelled** "(Stories 10.7–10.9)" / "(Story 10.7)". Correctly marked; **not a finding**. Real code is at `src/Hexalith.Folders/Queries/ContextSearch/` + `Projections/SemanticIndexing/`. |

The codebase has clearly settled on a `Queries/{Area}/` + `Projections/{Area}/` split that the document's `Projections/{Concept}/`-for-everything layout never absorbed. That is a convention the codebase established and the document overrides without saying so (brief item 5).

---

### REAL-9 — MEDIUM — The reality table's 403 denominator (49 of 50) contradicts both the Contract Spine and the approved OQ3 matrix, and contradicts S-7 on the same page

architecture.md line 241:

> | **PD10** authorization spine | **HTTP 403 is live on 49 of 50 protected operations.** …

architecture.md line 640 (S-7), 400 lines later: *"All **49** protected Contract Spine operations evaluate authority before resource lookup"*.

Spine counts (`src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`): `grep -cE '^ +"403":'` → **49**; `"404":` → 46; `"503":` → 45.

`docs/contract/authorization-matrix.md:341` (OQ3 `1.0.0`, the approval-bound artifact):

> `G1` … The Contract Spine declares two status-distinct safe-denial envelopes after authentication: **403 on 49 of 49 operations** and 404 on 46 of 49.

and `:15` / `:102`: *"49 Contract Spine operations"* / *"The operation counts total 49, which equals the current Contract Spine inventory."*

So the denominator is 49, not 50, and "49 of 50" wrongly implies one protected operation is already free of 403. In a table whose function is to be the document's verified anchor, a number that disagrees with the artifact it summarises undercuts the anchor. (The prior adversarial review's F14 — *hard-coding* 49 at all, against the document's own generated-denominator rule at line 704 — remains open and is now compounded by a second, different hard-coded number.)

---

### REAL-10 — MEDIUM — The reality table's `visibility` row understates what the spine already declares

architecture.md line 241:

> … `visibility` exists only as the constant `details.visibility="metadata_only"`, not as a required top-level error field.

The second clause is correct — `visibility` is not top-level. The first is not. In `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`:

```yaml
7738:        details:
7739:          type: object
7740:          additionalProperties: false
7741:          required:
7742:            - visibility
7743:          properties:
7744:            visibility:
7745:              type: string
7746:              enum:
7747:                - redacted
7748:                - metadata_only
```

and per-problem narrowings including `enum: [redacted]` at `:7778`. It is already a **required, enumerated** property of `details` carrying both values, plus a required property on `RedactionMetadata` (`:10314`). The runtime constant the row describes is `src/Hexalith.Folders.Server/FolderAuthorizationDenialMapper.cs:36` (`["visibility"] = "metadata_only"`) and `src/Hexalith.Folders.Client/Serialization/Oq2ProblemProjection.cs:74,87`.

The accurate statement is narrower and more useful to the PD10 owner: *`visibility` is already a required enumerated member of `details` (`redacted` | `metadata_only`); PD10 needs it promoted to top level and extended with `withheld`.* The rest of the PD8/PD10 row is verified accurate — no `confidential` tier, no tokenizer (`rg -ni "tokeniz" src tests` → only `ScaffoldContractTests.TryTokenizeCommand`), no `withheld` render state (the real vocabulary is `src/Hexalith.Folders.UI/Services/FieldDisclosure.cs:23` `Visible, Redacted, Unknown, Missing` and `src/Hexalith.Folders.Contracts/Projections/Audit/RedactionVisibility.cs:5` `metadata_only, redacted`), and all three "removed" codes plus 403 are live and emitted (`AuditEndpoints.cs:459-462`; `StatusCodes.Status403Forbidden` × 37 across 10 files).

---

### REAL-11 — MEDIUM — Directory-tree drift beyond the critical items

All in §"Complete Project Directory Structure" (1136–1525), presented as as-built, checkbox-confirmed at line 1782 (*"[x] Complete directory structure defined"*):

| Tree entry | Line | Reality |
| --- | --- | --- |
| `docs/architecture/` | 1160 | Does not exist |
| `docs/api/` | 1161 | Does not exist |
| `docs/adrs/` containing only `0000-template.md` | 1165–1166 | 8 real ADRs + `index.md`, including the `0001-folder-domain-processor-persistence.md` and `0004-per-command-canonical-idempotency.md` the document itself cites at lines 251 and 259 |
| `docs/exit-criteria/` listing c1–c5 + `_template.md` | 1167–1173 | Also holds `c0-c13-governance-evidence.yaml`, `c6-transition-matrix-mapping.md`, `c7-lock-authorization-timing.md`, `nfr-traceability.md`, `oq8-idempotency-design.md`, `oq8-idempotency-evidence.yaml`, `s2-oidc-validation.md` — all of them cited elsewhere in the document as authorities |
| `docs/runbooks/` containing only `tenant-deletion.md` | 1163–1164 | 8 runbooks (`alerts`, `incident-mode`, `index`, `provider-drift`, `reconciliation`, `retention`, `rollback`, `tenant-deletion`) |
| `docs/contract/` (19 files) | — | Absent from the tree entirely, though cited as approval-bound authority at lines 266, 645, 647 |
| `tests/tools/oasdiff/`, `tests/tools/policy-conformance/` | 1516–1517 | Absent; real: `forgejo-drift/`, `pattern-examples/` (+ 25 `run-*-gates.ps1` scripts the tree omits) |
| `tests/contracts/forgejo/{v15.0,v14.0,v13.0}/` | 1501–1503 | Real: `11.0.14/`, `14.0.5/`, `15.0.2/`, `15.0.7/`, `16.0.3/` |
| `tests/contracts/github/14.0.0/openapi-snapshot.json` | 1504–1505 | Real: `tests/contracts/github/pinned-profile.json` |
| `src/Hexalith.Folders/Registration/HexalithFoldersDomainServiceExtensions.cs` | 1303–1304 | Real: `FoldersServiceCollectionExtensions.cs` + `FoldersModule.cs` at project root; no `Registration/` |
| Missing test projects | 1474–1494 | `Hexalith.Folders.EventStore.Tests`, `Hexalith.Folders.AppHost.Tests`, `Hexalith.Folders.UI.E2E.Tests`, `Hexalith.Folders.LoadTests.Tests`, `tests/shared/Parity`, `tests/tools/pattern-examples` (the last is the project the line-1065 "pattern-examples compile gate" runs on) |
| Missing root entries | 1137–1149 | `deploy/` (containers, dapr, nuget, observability), `Directory.Build.targets`, `aspire.config.json`, `commitlint.config.mjs`, `.env.example`, `.mcp.json`, `.agents/`, `.codex/` |
| `src/Hexalith.Folders.ServiceDefaults/` | — | Exists in `src/` and `.slnx`; omitted from the tree. Consistent with line 463 / line 1819 (*"Folders must have no local ServiceDefaults project"*), so the **intent** is labelled — but an as-built tree that silently omits a project that exists is still drift, and line 1724 counts it in "13 src projects" |
| `src/Hexalith.Folders.Client/Generation/Shared/` | — | A second `.csproj` nested inside the Client project folder (`Hexalith.Folders.Client.Generation.Shared.csproj`, `YamlContractLoader.cs`), absent from the tree and from the one-project-per-`src/`-folder convention the layout implies |

Correctly verified, for the record (**not** findings): `.github/workflows/` is exactly the five files listed (1153–1157); `accessibility-gates` and `e2e-gates` jobs exist in `ci.yml:150,184` as claimed at line 1717; all five `tests/fixtures/` normative files listed at 1507–1511 exist; `tests/load/Hexalith.Folders.LoadTests.csproj` + `Scenarios/` exist; `docs/exit-criteria/_template.md` and `docs/adrs/0000-template.md` exist; `src/Hexalith.Folders.Contracts/openapi/{hexalith.folders.v1.yaml,extensions/}` exist.

---

### REAL-12 — MEDIUM — Counts in "Architecture Validation Results" do not reconcile

| Claim | Line | Reality |
| --- | --- | --- |
| *"enumerates all 13 src projects, 11 test/load projects"* | 1724 | Tree enumerates 11 src / 11 test; `.slnx` declares 14 src / 17 test (see REAL-1) |
| *"Cross-cutting concerns mapped (22 concerns; structure mapping table)"* | 1764 | 22 concerns are defined (103–124); the structure-mapping table (1584–1597) maps **12** of them — #1, #6, #11, #13, #14, #15, #16, #17, #18, #19, #20, #21. Ten concerns (#2–#5, #7–#10, #12, #22) have no structural home |
| *"11-state, ~30-transition matrix"* | 1797 | The matrix now has 37 rows / **41** canonical positive edges — the number `ConsumerDocsConformanceTests` was re-pinned to on 2026-09-15 (34 → 41). "~30" is pre-amendment |
| *"58 functional requirements across **12** capability blocks"* | 54 | Eleven blocks are then named (Capability Contract Terms … Authorized Search Facade). Echoed as *"12 capability groups"* at line 1706 |
| *"8 capability groups per PRD"* | 1531 | The spine declares 8 `tags` ✓ — consistent, but sits beside the "12 capability blocks" figure without reconciliation |

---

### REAL-13 — LOW — "Minor Gaps" lists two artifacts as pending that shipped

Lines 1738–1739 list under *"Minor Gaps (Nice-to-Have)"*: *"ADR template authored Phase 0 (location declared at `docs/adrs/0000-template.md`)"* and *"Tenant-deletion runbook authored Phase 4 (location declared at `docs/runbooks/tenant-deletion.md`)"*. Both files exist. Harmless, but it is the same staleness signature as REAL-11/REAL-12 and worth clearing in the same pass.

---

### REAL-14 — LOW — Component Boundaries names provider adapters as projects; they are namespaces inside the core domain

Line 1544: *"**Provider adapters** (`Hexalith.Folders.Providers.{GitHub,Forgejo}`) implement `IGitProvider` port and **may not be referenced from outside the core domain**"*. Line 1611–1612 repeats the form. They are not projects — `src/Hexalith.Folders/Providers/GitHub/GitHubProvider.cs:3` declares `namespace Hexalith.Folders.Providers.GitHub;` inside `Hexalith.Folders.csproj`. The boundary is therefore a naming convention with no project-reference enforcement, while line 1702 claims *"Component boundaries … are mechanically enforceable via project references."* Cosmetic in the tree, but it is the one boundary in the list that cannot be mechanically enforced the way the document says all of them can.

---

## Present-tense capability claims probed

| # | Claim | architecture.md line | Labelled target-state? | Code evidence | Verdict |
| --- | --- | --- | --- | --- | --- |
| 1 | `src/Hexalith.Folders.EventStore/` exists as a project | — (0 occurrences) | n/a | `src/Hexalith.Folders.EventStore/` (15 intent adapters + `Program.cs`); `.slnx`; `AppHost/Program.cs:22` | **Omitted entirely (REAL-1)** |
| 2 | "`eventstore` — sibling Hexalith.EventStore (existing)" | 1548 | No | `AppHost/Program.cs:22` composes `Projects.Hexalith_Folders_EventStore` as `EventStoreAppId` | **Contradicted** |
| 3 | Tree "enumerates all 13 src projects, 11 test/load projects" | 1724 | No | Tree lists 11 src; `.slnx` has 14 src / 17 test | **False** |
| 4 | `dapr-policy-conformance` runs `daprd` in a kind cluster with a property-based negative generator, blocks merge | 731, 1044, 1799, 1826 | No | 0 hits for `kind cluster`/`daprd`; `run-dapr-policy-conformance-gates.ps1:24-32`; `policy-conformance.yml:3-6` is schedule-only | **Not built (REAL-2)** |
| 5 | Per-tenant + global provider token buckets; 429 chaos test in CI | 736, 1065, 1712, 1799 | No | No `RateLimiting/` in Workers; 0 `*TokenBucket*`; no `ProviderRateLimitChaos/` | **Not built (REAL-3)** |
| 6 | C10 lint job in `ci.yml` + `Hexalith.Folders/Caching/TenantPrefixedCacheKey.cs` | 322, 349, 1024, 1298, 1589 | No | No `Caching/`; no lint job in `ci.yml`; real gate = `GovernanceCompletenessGateTests.cs:1383` + `tests/fixtures/cache-key-exceptions.yaml` (per governance YAML `:188-195`) | **Wrong mechanism + missing artifact (REAL-4)** |
| 7 | `FolderStateTransitions.cs` rejects four of five PD11 transitions | 240 | Row *is* the label | `FolderStateTransitions.cs:90-91` → `Failed`; `:107-108` → `Ready`; two of five accepted | **Inaccurate, understates risk (REAL-5)** |
| 8 | `dirty` disposition is conditional | 369 | No | `FolderStateTransitions.cs:157`; `DispositionLabelMapper.cs:33` — unconditional `AwaitingHuman` | **Contradicted, unlabelled (REAL-5)** |
| 9 | `unknown_provider_outcome` disposition is `auto-recovering` | 373 | No | `FolderStateTransitions.cs:161` → `AwaitingHuman` | **Contradicted, unlabelled (REAL-5)** |
| 10 | "F-4 console labels cannot drift from architecture" | 438 | No | Drifted on 2 of 11 states in both copies; drift pinned by `FolderStateTransitionsTests.cs:199` | **False** |
| 11 | `LockLeaseBecameStale` fires at the C7 boundary | 405, 428, 436 | **Yes** (240, 428) | Absent from `FolderWorkspaceLifecycleEvent.cs` (23 members); docs-only (`docs/diagrams/workspace-lifecycle.md:72`) | Correctly labelled ✓ |
| 12 | Three operator events are accepted today and in the spine enum | 240, 427 | **Yes** | `FolderStateTransitions.cs:101,105,123`; spine `:8913 OperatorDiscardRequested` | Accurate ✓ |
| 13 | Switch over `(currentState, eventType, resolution)` returning `FolderWorkspaceTransitionResult` | 436 | Partly | `FolderStateTransitions.cs:47-50`; `FolderWorkspaceTransitionResult.cs:3-9` — both exist | Shape accurate ✓ |
| 14 | `FolderWorkspaceDirtyResolution` is the discriminator for the four PD11 guard pairs | 436 | "PD11 makes" | `FolderWorkspaceDirtyResolution.cs:6-12` = `{CommitConfirmed, CommitRejected}`; used only at `:110,:112` for `(UnknownProviderOutcome, ReconciliationCompletedDirty)` | **Misattributed (REAL-7)** |
| 15 | "Provider Git write path throws `NotImplementedException`" | 251, 262 | Section *is* the label | Write paths implemented (`OctokitGitHubApiClient.cs:65,283,520`; `ForgejoSmartHttpGitTransport.cs:450`); executors return `KnownFailure` (`UnavailableWorkspaceCommitExecutor.cs:7-11`) | **Inaccurate (REAL-6)** |
| 16 | Sole `IFolderRepository` is `InMemoryFolderRepository`; Production won't boot | 251 | **Yes** | `InMemoryFolderRepository.cs:29`; `Server/Program.cs:24-29,59-70` | Accurate ✓ |
| 17 | `/project` replay is a hardcoded 501 | 251 | **Yes** | `FoldersDomainServiceEndpoints.cs:93`; `FoldersDomainServiceRequestHandler.cs:95` | Accurate ✓ |
| 18 | ADR-0001 returns `DomainResult.NoOp()` | 251, 259 | **Yes** | `docs/adrs/0001-folder-domain-processor-persistence.md`; `FolderDomainProcessor.cs:1257-1276,1340-1343` | Accurate ✓ |
| 19 | Story 10.6 ships a metadata-derived materializer | 143 | n/a | `FoldersWorkersModule.cs:79` registers `MetadataDerivedSemanticIndexingContentMaterializer`; `.cs:32-35,49-58` | Accurate ✓ |
| 20 | Server facade leaves the `Unavailable` bridge default | 181 | **Yes** | `FoldersServerServiceCollectionExtensions.cs` + `UnavailableSemanticIndexingBridgeReadModel.cs` | Accurate ✓ (note: `EventStoreSemanticIndexingBridgeStore` *is* now registered on the Server at `:131-134`, so Story 10.7's premise may be partly overtaken — **unverified**, flagged for the 10.7 owner) |
| 21 | Ops-console + transition-evidence read models are seed-backed `InMemory` | 187–190 | **Yes** | `FoldersServiceCollectionExtensions.cs` `TryAddSingleton` defaults | Accurate ✓ |
| 22 | No `confidential` tier / tokenizer / `withheld` render state | 242 | **Yes** | `rg -ni "tokeniz"` → only `ScaffoldContractTests.TryTokenizeCommand`; `FieldDisclosure.cs:23`; `RedactionVisibility.cs:5` | Accurate ✓ |
| 23 | 403 live on "49 of 50" protected operations | 241 | Row *is* the label | Spine `"403":` × 49; `authorization-matrix.md:341` "49 of 49"; S-7 line 640 says 49 | **Wrong denominator (REAL-9)** |
| 24 | `not_found` / `cross_tenant_access_denied` / `audit_access_denied` live | 241 | **Yes** | `FolderCanonicalErrorMapper.cs:28,77`; `FailureKindProjection.cs:46`; `AuditEndpoints.cs:459-462` | Accurate ✓ |
| 25 | `visibility` exists "only as the constant `details.visibility="metadata_only"`" | 241 | Row *is* the label | Spine `:7741-7748` — `required: [visibility]`, `enum: [redacted, metadata_only]`; `:7778` `[redacted]`; `:10314` | **Understated (REAL-10)** |
| 26 | `planning-story-manifest.yaml` still `generated_on: '2026-08-04'`, zero `execution_rank` | 206, 243 | **Yes** | Confirmed by the 2026-09-15 technology lens F1; unchanged since | Accurate ✓ |
| 27 | Generated contract manifest carries a date | (probe) | n/a | No date in any generated artifact — `HexalithFoldersIdempotencyHelpers.g.cs:12-25` carries SHA-256 + `HelperSchemaVersion = "ac2e839a4fc729c2"` and explicitly notes the date literal was *replaced* by a signature-derived constant; the only dated manifest is `tests/contracts/forgejo/supported-versions.json:3` `"generatedAt": "2026-08-26"` | No stale date ✓ |
| 28 | `accessibility-gates` axe/WCAG job (Story 8.4) | 1717 | No | `.github/workflows/ci.yml:150` | Accurate ✓ |
| 29 | Five workflows: ci, contract-spine, nightly-drift, policy-conformance, release-packages | 1153–1157 | No | Exactly those five exist | Accurate ✓ |
| 30 | FR1–FR3 live in `docs/contract-terms.md` | 1565 | No | Does not exist; real vocabulary in `docs/contract/` (19 files, never named in the tree) | **Broken mapping (REAL-8)** |
| 31 | FR43–46 → `Projections/WorkspaceStatus/`; FR53–57 → `Projections/Audit/` | 1572, 1579 | No | `Projections/` = FolderAccess, FolderList, SemanticIndexing, TenantAccess; real models under `Queries/Folders/`, `Queries/Audit/` | **Broken mapping (REAL-8)** |
| 32 | FR37–42 → `src/Hexalith.Folders/Idempotency/` | 1571 | No | Directory absent; real adapters in `src/Hexalith.Folders.EventStore/` | **Broken mapping (REAL-8)** |
| 33 | FR58 → `src/Hexalith.Folders/Search/`, `Projections/Search/` | 1269, 1292, 1580 | **Yes** ("Stories 10.7–10.9") | Absent; real: `Queries/ContextSearch/`, `Projections/SemanticIndexing/` | Correctly labelled ✓ |
| 34 | `Client` references Contracts only; `UI` references Client only | 1541, 1543 | No | `Hexalith.Folders.Client.csproj:23` → Contracts only ✓; `Hexalith.Folders.UI.csproj:18-19` → Client + FrontComposer.Shell (conditional) | Substantially accurate ✓ |
| 35 | Provider adapters are `Hexalith.Folders.Providers.{GitHub,Forgejo}` projects, boundary mechanically enforceable | 1544, 1702 | No | Namespaces inside `Hexalith.Folders.csproj` (`GitHubProvider.cs:3`) | **Not enforceable as stated (REAL-14)** |
| 36 | 22 cross-cutting concerns mapped | 1764 | No | Mapping table covers 12 | **Overstated (REAL-12)** |
| 37 | "~30-transition matrix" | 1797 | No | 37 rows / 41 edges (test re-pinned 34 → 41 on 2026-09-15) | **Stale (REAL-12)** |

---

## Notes for the gate owner

1. **The honesty convention works; it just has not been applied outside the amendment.** Every 2026-09-15 addition carries its label and survives verification (rows 11, 12, 15–22, 24, 26 above). Every failure in this report is in material written in 2026-05 and never re-verified. The cheapest structural fix is to give §"Complete Project Directory Structure", §"Requirements to Structure Mapping", and the I-3/I-8/C10 enforcement rows the same treatment the amendment got: a one-line as-built-vs-target marker per entry, or a single banner stating that the tree is the target layout and naming the authoritative as-built inventory (`Hexalith.Folders.slnx`).

2. **Do not fix the reality table by loosening it.** REAL-5, REAL-9 and REAL-10 are all in the direction of *making the code sound closer to the design than it is*. The table's value is entirely in being exactly right; a table that is approximately right is worse than none, because it is the artifact three downstream owners will cite instead of reading the code.

3. **REAL-1 is the one that cannot wait.** It is not a documentation gap; it is an undeclared service-topology change plus an undeclared home for the A-9 mechanism, and the codebase is already flagging it through three standing `ScaffoldContractTests` reds. Whoever owns the next Epic 12 story will touch `src/Hexalith.Folders.EventStore/` and will have no architectural authority to cite.

4. **Carried forward, still open from the 2026-09-15 reports** (not re-argued here): adversarial F7 (no unit owns the PD10 spine correction), F8 (execution-wave rank rule unsatisfiable), F14 (hard-coded 49), F16 (NFR79/NFR80 ownership); rubric MEDIUM-2 (PD8/PD10/PD11 unowned at every rank). The document now records F7, F8 and the C3/PD11 window collision as explicit OPEN items (lines 214, 276, 432, 651), which is the right disposition — they are routed, not hidden.

5. **Flagged but unverified**, for the Story 10.7 owner: `EventStoreSemanticIndexingBridgeStore` is now registered in the Server composition (`FoldersServerServiceCollectionExtensions.cs:131-134`) as well as in Workers (`FoldersWorkersModule.cs:71-76`). Line 181 describes 10.7 as *relocating* that store into a Server-referenceable project and registering it. If that has already happened, 10.7's scope has shrunk and the paragraph is stale; I did not trace the registration order against the `AddFoldersContextSearchFacade` default to confirm which registration wins. Verify before scheduling 10.7.
