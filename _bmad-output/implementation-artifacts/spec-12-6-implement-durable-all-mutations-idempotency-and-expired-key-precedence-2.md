---
title: 'Story 12.6: Complete durable all-mutations idempotency'
type: 'feature'
created: '2026-10-03'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '_bmad-output/implementation-artifacts/epic-12-context.md'
  - '_bmad-output/implementation-artifacts/12-6-implement-durable-all-mutations-idempotency-and-expired-key-precedence.md'
  - 'docs/exit-criteria/oq8-idempotency-design.md'
  - '_bmad-output/planning-artifacts/planning-story-manifest.yaml'
baseline_commit: ebefb2836d5debe6735b60d070d805cee55168c5
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore admission can disclose replay, conflict, or expiry before Folders' layered authorization. Canonical intent, C13 checks, durable Folders state, and production evidence remain incomplete.

**Approach:** Complete approved OQ8 behavior across Folders and its released EventStore dependency. Build independent contract and authorization work first; final integration waits for Story 12.1 and package authority.

## Boundaries & Constraints

**Always:** Authorize and validate before admission/disclosure; recheck replay authority. EventStore owns tenant/key admission, fencing, expiry, and tombstones. At `now >= expiresAt`, either intent returns identical metadata-only `idempotency_key_expired` without repeating domain, provider, file, Git, projection, audit, or task effects. Retain mutation results 24 hours, commit results seven calendar years, and tombstones for tenant lifetime plus 400 days. Generate C13 from the Spine. Keep OQ8 open until production evidence and approvals pass.

**Never:** Treat source-only evidence or fake state as production proof; add a Folders ledger; trust public extensions as descriptor authority; hand-edit generated outputs; update a pin without release authority; or close Story 12.1/3.10 here.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| New/live | Authorized key; same/different intent | One execution; replay/conflict | No prior-intent leak or repeat effect |
| Expired | Either intent at/after expiry | Same 409; CLI 76; MCP expired kind | Nonretryable; use new key |
| Denied | Revoked or wrong authority | Deny before key disposition; ledger unchanged | Metadata-only denial |
| Unavailable | Corrupt/legacy store or unknown outcome | Fail closed or reconcile read-only | Never execute as fresh |
| Read | Any generated read with a key | Reject before source/query access | `idempotency_key_not_allowed` |

</frozen-after-approval>

## Code Map

- `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml` — source-only handoff; `SubmitCommandHandler` admits before routing.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `FoldersDomainServiceRequestHandler.cs` — keyed submits, late authorization, scattered read guards.
- `src/Hexalith.Folders.EventStore/FoldersCanonicalIntentBuilder.cs`, `FoldersIdempotencyIntentAdapterCatalog.cs` — descriptor construction and 13 adapters for 14 mutations.
- `src/Hexalith.Folders/Aggregates/Folder/IFolderRepository.cs`, `src/Hexalith.Folders/Aggregates/Organization/IOrganizationAclRepository.cs` — process-local state; Story 12.1 owns replacement.
- `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `hexalith.folders.v2.yaml`, `tests/tools/parity-oracle-generator/Program.cs` — Spine and matrix gates.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/_bmad-output/implementation-artifacts/4-8-eventstore-oq8-platform-evidence.yaml`, `Directory.Packages.props`, `_bmad-output/planning-artifacts/planning-story-manifest.yaml` — gate runtime on accepted package release and Story 12.1.
- [ ] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `FoldersDomainServiceRequestHandler.cs`, `FolderAuditEndpointFilter.cs` — authorize before gateway admission, restrict direct gateway ingress, recheck execution, and avoid replay audit mutation; test denial/disposition.
- [ ] `src/Hexalith.Folders.EventStore/FoldersCanonicalIntentBuilder.cs`, `src/Hexalith.Folders.EventStore/MutateFilesIdempotencyIntentAdapter.cs`, `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`, `hexalith.folders.v2.yaml` — reject duplicate JSON/untrusted scope; align behavior fields across both Spines and adapters; test equivalence.
- [x] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `Pd10V2CandidateCompatibilitySeam.cs` — reject keys across generated reads before source access; test effective permissions.
- [x] `tests/tools/parity-oracle-generator/Program.cs`, `tests/fixtures/parity-contract.schema.json`, `docs/contract/idempotency-and-parity-rules.md` — require key, tier, errors, and five behavior cells; document all rows; regenerate SDK/C13.
- [ ] `src/Hexalith.Folders.Server/FolderRepositoryStartupAssertion.cs`, `FoldersServerServiceCollectionExtensions.cs`, `src/Hexalith.Folders/Aggregates/Organization/IOrganizationAclRepository.cs` — after Story 12.1, require durable Production state and retire fingerprint authority.
- [ ] `tests/Hexalith.Folders.IntegrationTests/`, `tests/Hexalith.Folders.Server.Tests/`, `docs/exit-criteria/oq8-idempotency-evidence.yaml` — prove generated matrix, restart, multi-host races, expiry, failures, leakage, and persisted state; bind dated approvals.

**Acceptance Criteria:**
- Given generated operations, when contract/runtime gates run, then mutations have trusted keyed behavior and reads reject keys before work.
- Given retry, conflict, expiry, or denied authority, when admission is evaluated, then approved public results preserve privacy and effects occur at most once.
- Given restart, concurrent hosts, compaction, or corrupt state, when production tests run, then persisted state proves one execution or safe denial.
- Given Story 12.1 and package authority, when final evidence is reviewed, then C13, OQ8 digest, and dated owner decisions support closure; otherwise Story 12.6 stays open.

## Implementation Notes

- Direct historical v1 REST mutations now pass `HistoricalMutationAuthorization` in `V1Only` and `Coexistence` before route handlers can call `IEventStoreGatewayClient.SubmitCommandAsync`. The 14 mutation routes resolve through `Pd10ProtectedOperationCatalog.TryResolveHistorical`; unrecognized mutating v1 routes fail closed. `CreateRepositoryBackedFolder` receives tenant authorization, bounded body-folder extraction, then folder-scoped authorization before admission, matching `RepositoryBackedFolderCreationService.CreateAsync` operation scope. V2 internal historical dispatch bypasses this duplicate check; `V2Only` still retires external v1 before lookup. The v2 `Pd10V2CandidateCompatibilitySeam.AuthorizeAsync` path now uses the extracted `folderId` for layered folder authorization before historical gateway dispatch. For an unknown-length body it first establishes tenant authority, buffers within the input limit, then reevaluates folder authority before the executor audits its final decision. Focused known- and unknown-length denial tests verify that the historical handler is not invoked.
- Folders canonical adapters now include a versioned SHA-256 option over declared domain-semantic payload fields in addition to the Spine field projection. This distinguishes behavior-affecting fields omitted from the historical field list, including release reason, explicit null versus omission, and repository branch-policy details, while identical file semantics with different transport operation, evidence, observed length, or retry metadata remain equivalent. The public one-entry folder ACL adapter rejects multi-entry batches rather than hashing only the first entry. `CreateRepositoryBackedFolder` now uses the v2 Spine key `branch_ref_policy.repository_binding_id`; its public-payload `credentialScopeClass` no longer selects trusted credential scope.
- A direct submit to EventStore outside Folders REST still reaches `references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Pipeline/SubmitCommandHandler.cs` admission at lines 74-124 before `RouteCommandAsync` at lines 287-293 invokes Folders `/process`. Folders' `FoldersDomainServiceRequestHandler.ProcessAsync` can reauthorize execution but cannot reorder that upstream disposition. Global pre-admission authorization requires the governed EventStore hook/package release: `4-8-eventstore-oq8-platform-evidence.yaml` says `handoffMode: source-only`, `releaseApproved: false`, `packageAuthority: false`, and `runtimePinAuthority: false`. Story 12.1 remains a prerequisite in `planning-story-manifest.yaml`; `FoldersServerHostComposition` still registers `InMemoryFolderRepository` for Development/Staging. No production restart, multi-host, or closure claim is made.
- The historical v1 Spine remains byte-stable at SHA-256 `3c3c668071cfaad3e6318626c03051039d00771a4d1afe29ab49df28f71ae4e2`. Adding OQ8 metadata to it fails `Pd10V2CandidateContractTests.HistoricalV1SpineRemainsByteStable`; field-list alignment there needs governed reapproval. No approval evidence or package pin was changed.
- Focused Debug Server and EventStore test assemblies build and pass. Integration test source for direct v1 denial was added, but its project build is blocked by `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -v:q`: CS1704 reports both packaged and source `Hexalith.Memories.Aspire`. A fresh restore stalls at `_GetAllRestoreProjectPathItems` and timed out after 35 seconds even with `--no-dependencies` and a local package source.

- 2026-10-03 resumed at `a20127cbcbea27dfc7eb5e8b2a543af54aaf0b45`: prevented endpoint audit writes for replay, conflict, expired, and unavailable admission results. `FolderTelemetryEmitter` preserves replay telemetry while excluding replay observations from audit observers; the Server regression asserts that existing audit contents remain unchanged. The central read-key guard now delegates to the existing Audit/Timeline, ProviderReadiness, OpsConsole, or Domain problem factory, preserving endpoint evidence and safe correlation without calling the handler or audit emitter.
- Trusted file/workspace/commit task scope must be a nonempty JSON string and match any envelope task scope. Create/bind repository adapters derive the fixed `provider_binding` credential scope from server policy, reject incompatible payload scope, and ignore caller extension scope. These focused fixes do not complete cross-Spine equivalence alignment or the direct EventStore pre-admission authorization gate.
- Final integration remains blocked by Story 12.1 (`backlog`, execution held) and EventStore platform handoff (`releaseApproved: false`, `packageAuthority: false`, `runtimePinAuthority: false`). The historical v1 Spine and all approval/pin records are unchanged. The production matrix and final Build review remain incomplete; Story 12.6 and OQ8 remain open.

## Spec Change Log

## Review Triage Log

## Verification

**Observed results (2026-10-03):**
- `pwsh tests/tools/run-contract-parity-ci-gates.ps1` passed all 12 categories after regenerating `_bmad-output/planning-artifacts/generated-v2-conformance-set-2026-09-17.yaml` with `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root .`.
- `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll -class Hexalith.Folders.Server.Tests.Pd10V2ReadKeyPrecedenceTests` passed 1/1; its generated inventory loop asserted canonical 400 and no handler invocation for all 35 reads, including effective permissions.
- Focused Server authorization and EventStore adapter assemblies passed 18/18 each in the implementation run; `git diff --check` passed. The full-host integration project remains build-blocked as detailed above.
- Matrix audit remains open: generated read-key rejection ran, while production-path new/live, expired, denied, and unavailable cases lack the durable package, Story 12.1 repository, and persisted state-store evidence required by the frozen matrix.

**Resumed-run verification (2026-10-03):**

- Server Debug source build: zero warnings/errors; complete Server assembly: **768/768 passed**, no skips. This includes both replay-observer cases, consumed-key endpoint dispositions, all generated read-key guards, and the five repaired endpoint-evidence/correlation regressions. Commands: `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -v:q`; `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll`.
- EventStore adapter Debug source build: zero warnings/errors; complete adapter assembly: **30/30 passed**, no skips. Commands: `dotnet build tests/Hexalith.Folders.EventStore.Tests/Hexalith.Folders.EventStore.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -v:q`; `dotnet tests/Hexalith.Folders.EventStore.Tests/bin/Debug/net10.0/Hexalith.Folders.EventStore.Tests.dll`.
- Broad core lane blocker: `dotnet build tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj -c Debug --no-restore -m:1 -p:UseHexalithProjectReferences=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -v:q` failed with CS1704 because packaged/source `Hexalith.FrontComposer.Shell` assemblies collide. The audit-content regression was verified in the buildable Server lane; the broad gate was not weakened.
- Aspire baseline: `aspire start --non-interactive` timed out after 120 seconds during restore; scoped describe/cleanup confirmed no Folders AppHost running. Live production verification remains unavailable.
- Refreshed derived v2 conformance hashes with `python3 scripts/generate-pd10-v2-conformance-set.py --repository-root .` (already current; no generated diff). Contracts Debug source build passed with zero warnings/errors; `dotnet tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net10.0/Hexalith.Folders.Contracts.Tests.dll -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests -class Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests` passed **15/15**, no skips. Generated metadata still declares approval pending and production exposure disabled. `git diff --check` passed; Git reported only its existing CRLF normalization warning for this spec.

**Commands:**
- `dotnet build Hexalith.Folders.slnx -c Debug` — source-reference build succeeds after the dependency gates.
- `dotnet build Hexalith.Folders.slnx -c Release` — package-only build succeeds against approved pin.
- `pwsh tests/tools/run-contract-parity-ci-gates.ps1` — generated contract and C13 gates pass.
- Build/invoke focused xUnit v3 Server, EventStore adapter, Contracts, and Integration assemblies; inspect live-sidecar persisted state.
- `git diff --check` in each owning repository — no whitespace errors.
