# Reconciliation — Sprint Change Proposal: DCP-Capable AppHost Lane Stand-Up

**Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-dcp-lane-standup.md`  
**Source date:** 2026-07-07, with a 2026-07-08 status update  
**Compared with:** `_bmad-output/planning-artifacts/prd.md` (final, updated 2026-07-14) and `_bmad-output/planning-artifacts/.memlog.md` (updated 2026-07-14)  
**Addendum:** None exists in the bound PRD workspace.  
**Reconciliation disposition:** **PRD no-op; partial implementation/release evidence only.** Keep OQ5 open because topology boot passed but the live seed/search/remove/archive FR58 round trip was skipped.

## Source purpose and status

The source records an operational investigation and remediation of the DCP-capable AppHost lane needed for Epic 9/10 live evidence. Its target evidence was topology boot, `folders-index` provisioning, changed/removed index publication, archive filtering, and query-facade search/hydration.

The most recent status is the 2026-07-08 update at the top of the source, which supersedes stale “Blocker C open” wording later in the document:

- The topology booted live under DCP.
- The opt-in lane reported **2 passed, 2 skipped, 0 failed**.
- All six resources reached `Running`; the EventStore publisher and folder-worker subscriber were live.
- The source treats those passes as Epic 9 AC6 live-boot and Story 10.3 D1#3 evidence.
- The seed/remove/archive round trip and its event probe were **skipped**, not passed, because the Dapr sidecar HTTP endpoint was neither resolvable nor host-reachable from the harness.

Blockers A and B received concrete fixes. Blocker C was operationally bypassed/resolved for the successful lane run by building the Tenants server against source EventStore APIs, but the source calls that a local proof rather than a committed durable solution. A durable cross-repository EventStore/Tenants version strategy remains a platform decision. Blocker D remains the direct obstacle to live round-trip evidence.

The source records mixed persistence state: the Tenants path fix was committed in its submodule, the parent pointer and AppHost SDK bump were uncommitted at the time described, and the source-mode Tenants build was explicitly not committed. It also says planning/status edits were intentionally not made because concurrent sessions were changing the same workspace.

## Product-level requirements and decisions relevant to the PRD

The proposal reinforces these product-level outcomes:

- FR58 needs real authorized indexed-result and indexing-status evidence, not only compilation or structural conformance.
- Search results must be hydrated through current authoritative Folders state, and stale, removed, archived, unauthorized, or otherwise invalid results must not escape.
- Index/read-model unavailability must be explicit rather than misrepresented as healthy data.
- Release evidence must distinguish passed, failed, and skipped scenarios; a boot pass cannot stand in for an unexecuted behavioral round trip.
- Cross-surface and read-model behavior must be proven in a representative live topology before release acceptance.

The DCP/Aspire lane is one means of producing that evidence. It is not itself a user-visible capability or immutable product requirement.

## Implementation, architecture, and story detail that stays out of the PRD

The following belongs in architecture, platform dependency policy, defects/stories, the test harness, and release evidence rather than the PRD:

- DCP, Aspire CLI, Docker, Dapr, NuGet-cache, and orchestration component versions.
- The `--tls-cert-file` compatibility diagnosis and DCP version table.
- The exact `Aspire.AppHost.Sdk` and `Aspire.Hosting` version alignment.
- `TenantsServerProjectMetadata`, `RepositoryProjectPaths`, project-path layout, submodule commits, and parent pointer handling.
- `EventStoreDomainDiagnostics("tenants")`, source-versus-package resolution, `UseHexalithProjectReferences`, and the EventStore/Tenants package-skew options.
- Dapr sidecar resource endpoint exposure and `DaprSidecarOptions` topology changes.
- The opt-in environment-variable gate, harness timeout, run numbers, stderr paths, process exit code, and quiet-host advice.
- Story/epic acceptance-item bookkeeping and sprint-status reconciliation.
- The proposed build/MSBuild guard requiring AppHost SDK and Hosting package version alignment.
- Working-tree concurrency cautions and commit sequencing.

These details explain how delivery will satisfy product outcomes, but placing them in the PRD would couple the product contract to replaceable orchestration and dependency mechanisms.

## Already-covered PRD content

The current PRD already expresses the observable outcomes and release consequence:

1. **MVP Feature Set:** authorized metadata-token recall and indexing-status queries per **FR58** are explicitly required in MVP.
2. **Search Families — Indexed metadata-token recall (FR58):** asynchronously indexed metadata is current-authority hydrated; stale, archived, revoked, unauthorized, and hidden hits are dropped; freshness and availability are part of the result contract.
3. **FR13 — Folder Lifecycle:** archive is a governed lifecycle transition that denies later mutations and preserves provider ownership. The lane's archive-filtering scenario is downstream evidence of archive-aware indexed behavior, not a new archive rule.
4. **FR31 — Workspace and Lock Lifecycle:** authorized actors can inspect whether index status is current, delayed, failed, stale, or unavailable.
5. **FR44 — Error, Status, and Diagnostics Contract:** `read-model unavailable` is a distinct required error category.
6. **FR46 — Error, Status, and Diagnostics Contract:** index/read-model failures expose safe resulting state, retry eligibility, client action, correlation ID, and available evidence.
7. **FR58 — Authorized Search Facade:** authorized metadata-token recall and indexing status must work across REST, SDK, CLI, and MCP; hits are security-trimmed and hydrated; stale, archived, revoked, unauthorized, and hidden hits are dropped; unavailability is explicit and fail-safe.
8. **NFR — Integration and Contract Compatibility:** shared/generated contract tests validate golden scenarios across surfaces, and C13 governs supported cells.
9. **NFR — Verification Expectations:** automated evidence is required for read-model determinism and cross-surface compatibility, while every NFR category needs an automated or documented validation path.
10. **OQ5 — Open Release Items:** the fail-safe but functionally empty FR58 facade must produce evidence for authorized non-empty results, indexing status, stale/unauthorized hit removal, unavailable behavior, and both C13 operations before FR58 implementation readiness can close.

The memlog confirms FR58's Memories-backed, metadata-only, current-authority-hydrated contract and explicitly assigns OQ5 to Search/Delivery until authorized non-empty behavior plus C13 evidence exists.

## Genuine PRD gaps

**None.** The source exposes implementation and evidence gaps, not a missing product requirement:

- **Blocker D:** the harness cannot yet reach the sidecar endpoint needed to execute the round trip.
- **Durable Blocker C resolution:** the successful source-mode proof still needs a supported EventStore/Tenants dependency strategy.
- **OQ5 evidence:** no successful authorized non-empty, removal, archive-filtering, indexing-status, and cross-surface round trip is established by this source.

All three belong under existing OQ5 or downstream architecture/platform work. A new PRD open item for DCP, Aspire, or package skew would duplicate and over-specify the existing outcome-based gate.

## Conflicts and supersession

### Internal source supersession

The July 8 status update supersedes several older statements later in the same source:

- Blocker C is no longer the immediate reason the topology cannot reach all-six-Running; the updated run did reach all six `Running`.
- Epic 9 AC6 live boot and Story 10.3 D1#3 are reported achieved, contrary to the earlier artifact-impact paragraph that says they remain owed.
- Blocker C is not “the only thing” preventing the AC9 green; Blocker D now prevents the round-trip tests from running.
- The simple “publish 3.43.0 and bump pins” option is not viable as-is because equal package/source versions cause the reported `CS1704` collision; the durable platform choice remains unresolved.

The reconciliation must use the top status update as the source's current state while retaining the unresolved durable-dependency warning.

### Relationship to current PRD and memlog

No product-decision conflict exists. The source's live-boot success is compatible with the PRD, but it does not close **OQ5** because two behavioral scenarios were skipped. The current PRD also governs the payload boundary: the eventual round trip must prove FR58 metadata-token recall and status, not introduce cross-workspace body-content indexing.

The source's “0 failed” run must not be interpreted as complete product acceptance when the relevant AC9/FR58 tests were skipped. This is an evidence-status clarification, not a PRD wording change.

## Recommended stable-ID edits or additions

- **FR13:** No edit.
- **FR31:** No edit.
- **FR44:** No edit.
- **FR46:** No edit.
- **FR58:** No edit.
- **OQ5:** Keep open and unchanged; attach qualifying evidence only after the canonical artifact contains the complete round trip and approvals.
- **OQ6:** No edit; this source does not prove the console's positive, degraded, and replay projection scenarios.
- **New FR/NFR/OQ:** None.
- **Renumbering:** None.

The live-boot result may be referenced from downstream release evidence, but it is partial evidence rather than a stable requirement change.

## Qualitative ideas the FR structure might otherwise drop

The source captures important delivery principles that should remain in technical and evidence artifacts:

- **Live topology tests find integration defects that structural tests cannot.** Both path-layout and SDK/orchestrator drift survived earlier gates.
- **Skip is not pass.** Evidence reporting must preserve skipped behavioral coverage even when the overall test run is green.
- **Local proof is not a durable fix.** Source-mode success demonstrates causality but does not settle package and platform policy.
- **Version provenance matters.** Source and published-package versions can represent different API contracts even inside one workspace.
- **Regression guards should target the actual drift seam.** AppHost SDK and Hosting package compatibility needs a focused automated guard.
- **Environment myths should be tested.** The historical Aspire CLI explanation was not on the harness code path; direct evidence isolated the real version drift.
- **Concurrent-worktree discipline matters.** Deferring shared status edits avoided overwriting unrelated in-flight work.

These details are well preserved by the proposal itself. They belong in architecture, platform policy, test design, and release evidence rather than a new addendum entry.

## Concise disposition

**No `prd.md` change; no addendum; no new open item.** Treat the all-six-Running result as partial live-environment evidence. Keep OQ5 open until Blocker D is resolved and the canonical evidence proves authorized non-empty FR58 search, indexing status, removal, archive/stale/unauthorized filtering, unavailable behavior, and both C13 operations. Carry the durable EventStore/Tenants dependency strategy and SDK-version regression guard in downstream platform/architecture work.
