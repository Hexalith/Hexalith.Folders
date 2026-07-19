# Reconciliation — Sprint Change Proposal and Production-Readiness Audit (2026-07-14)

**Source:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14.md`  
**Source date:** 2026-07-14  
**Audit snapshot:** HEAD `4ed58e1`, with 60 pre-existing uncommitted files reported by the source  
**Compared with:** `_bmad-output/planning-artifacts/prd.md` (final, updated 2026-07-14) and `_bmad-output/planning-artifacts/.memlog.md` (updated 2026-07-14)  
**Addendum:** None exists in the bound PRD workspace.  
**Reconciliation disposition:** **Apply targeted PRD readiness/evidence edits and add release-blocking open items; preserve the existing product MVP and stable FR numbering.** Do not copy the audit's implementation inventory into the PRD.

## Source purpose and status

The source is a major, read-only production-readiness audit and correct-course proposal. It reports a high-confidence **no-go** for production LLM/agent use and a conditional go for continued development. Its central finding is that the contract/control plane is mature while the production data plane is absent or stubbed: accepted state is in memory, file bytes are discarded or unavailable, provider write/commit paths are unimplemented, tasks do not complete, projections are not durable, semantic search is inert, and Production composition cannot boot with a durable repository.

The proposal also reports critical/high edge-security, operational-truth, CI, verification-quality, and agent-protocol findings. It recommends correcting release claims, fixing independent Phase-0 defects, and chartering a durable persistence and real-Git round-trip epic before treating the product MVP as complete.

The source is explicit that it changed no code, PRD, architecture, epics, or sprint status. Its final routing/application remained pending. However, its later “Unresolved decisions” section records two narrower decisions as ratified by Jerome on 2026-07-14:

- **Option B — Product MVP:** keep the real data-plane product goal and build it rather than redefining the shipped product as contract-only.
- **Durable repository ownership:** create Epic 12 for the durable `IFolderRepository`/data plane rather than expanding Story 11.10.

The source's final “awaiting approval” wording therefore means artifact application and broader Epic 13/Phase-0 routing were still pending, not that Option B and Epic 12 remained undecided. Findings are an audit snapshot, not proof of today's code state; this reconciliation uses them as the supplied change signal without independently re-auditing implementation.

## Product-level requirements and decisions relevant to the PRD

The source establishes or reinforces these product decisions:

- The product MVP remains a real repository-backed lifecycle, not a contract-only control-plane release.
- A successful mutation/commit path must preserve exact content, produce a provider-confirmed durable Git effect, expose an actionable status/task outcome, and survive restart through authoritative replay.
- Contract, SDK, CLI, MCP, fake-backed E2E, prose-conformance, and in-memory tests cannot substitute for production-composition evidence of the data plane.
- Production readiness is no-go until the durable lifecycle works through real seams; `prd.md status: final` describes artifact maturity, not implementation acceptance.
- Critical edge controls—token transport, provider-egress SSRF defense, fail-safe authentication/authorization defaults, and safe credential handling—are release safety concerns, not optional polish.
- Readiness, alerts, and operational status must reflect real dependency/projection state rather than constant or declared-only health.
- Accepted tasks must never disappear after restart or poll forever without an inspectable governed outcome.
- Search/index removal and reconciliation must not acknowledge loss-prone work as complete; deletion and revocation effects need durable retry/convergence evidence.
- Verification evidence must distinguish real behavior, fakes, mocks, structural checks, and documentation assertions.

The source also raises agent-fitness ideas—MCP safety annotations, typed bodies, structured output, provenance/version pins, and untrusted-content marking. These are valid concerns but were not among the two explicitly ratified scope decisions.

## Implementation, architecture, epic, and audit detail that stays out of the PRD

The following belongs in the audit, architecture, ADRs, epics/stories, code, and release-evidence artifacts rather than the PRD narrative:

- Concrete class names such as `InMemoryFolderRepository`, `UnavailableWorkspaceCommitExecutor`, and `HealthyReadinessSnapshotSource`.
- ADR-0001 `NoOp`, `/project` routing, DI registrations, `ConcurrentDictionary`, and source/package composition details.
- Exact endpoint and file-line evidence, `NotImplementedException` sites, process behavior, and provider-client factories.
- Epic 12/13 story decomposition, Story 10.6/11.10 sequencing, and effort estimates.
- Workflow names, CI run IDs, submodule-init command locations, `deps.json` races, dirty-tree contents, and local command logs.
- Dapr component manifests, AppHost/ServiceDefaults placement, health-route names, and specific alert instrument names.
- Specific retry/outbox/reconciler implementation, state-store selection, `IHttpClientFactory`, and batching algorithms.
- Exact MCP SDK attributes and request-body implementation classes.
- The full HXF finding catalog, capability matrix, trust-boundary diagram, and file-by-file plan.

These details explain why the product requirements are not met and how delivery may meet them; copying them into the PRD would make the product contract an implementation audit log.

## Already-covered PRD content

The July 14 PRD already incorporates much of the audit's product correction:

1. **Project Classification — Project Context:** the PRD is a brownfield living product contract, and product intent is separated from current repository/governance artifacts.
2. **MVP Contract Summary / MVP Strategy & Philosophy:** the MVP is explicitly repository-backed and must work in a **real repository-backed workspace**.
3. **User Success, Technical Success, SM1, and UJ1:** success requires add/change/remove plus a provider-confirmed durable commit, context/status, and audit across the required surfaces and both providers.
4. **FR2:** every required surface documents and demonstrates the ordered lifecycle through one durable commit, context/status/audit, and failure transitions.
5. **FR18–FR19:** repository-backed folder creation and eligible pre-created repository binding are required.
6. **FR24:** workspace preparation requires valid provider readiness, binding, policy, authorization, and task context.
7. **FR32:** authorized actors can apply one or many file mutations in a governed task workspace.
8. **FR34–FR35:** authorized live-workspace tree, metadata, glob, range, and bounded body-search behavior is required.
9. **FR37:** success requires a provider-confirmed durable update of the bound remote/ref; local-only success cannot satisfy it.
10. **FR39–FR40:** task/commit evidence and stable incomplete/failure/reconciliation behavior are required.
11. **FR43–FR46:** failures expose stable, safe, actionable states and errors.
12. **FR47–FR51 / C13:** REST, SDK, CLI, and MCP must match the Contract Spine and approved support-cell behavior; actual adapter drift is a conformance defect.
13. **FR58 / OQ5:** the fail-safe but functionally empty indexed-search/status facade is explicitly recognized as an implementation-readiness blocker.
14. **OQ6:** seed-only console/read-model diagnostics are explicitly recognized as a blocker until projection-backed positive, degraded, and replay evidence exists.
15. **NFR — Security and Tenant Isolation:** zero cross-tenant leakage, deny-by-default internal boundaries, secret exclusion, and automated security/tenant tests are required.
16. **NFR — Reliability, Idempotency, and Failure Visibility:** accepted work remains inspectable; successful commit is provider-confirmed and durable; duplicate effects are forbidden.
17. **NFR — Observability, Auditability, and Replay:** deterministic replay, operational signals, and durable records sufficient to rebuild views are required.
18. **OQ3, OQ4, OQ8, and OQ10:** authorization completeness, provider compatibility, idempotency alignment, and release-calibration governance are already open blockers.

The memlog confirms the decisive Option-B product direction even though it does not name the audit: the canonical MVP job includes real file mutations and commit; persistence and failure visibility are the reason local-first/brownfield scope was deferred; successful commit means provider-confirmed durable remote update; and tests/adapters cannot override the PRD/Contract Spine authority hierarchy.

## Genuine PRD gaps

The audit reveals gaps in release governance and several product-level NFRs even though the core functional intent is already present.

### 1. Current implementation-readiness posture is not explicit

The PRD is marked `status: final` and lists OQ1–OQ10, but it does not say that the 2026-07-14 implementation audit found the current runtime **not product-shippable**. Because prior epics used “MVP release readiness/acceptance” language, readers can still confuse a final contract with a completed product.

Recommended PRD addition: a concise **Current Implementation Readiness** note stating that the PRD is final as a product contract, the audited implementation is contract/control-plane complete but data-plane incomplete, and production release is no-go until all release blockers—including the durable data-plane gate below—close.

### 2. MVP acceptance evidence does not require authoritative durability

The current acceptance list requires parity and E2E scenarios but does not require exact-byte persistence/read-back, restart survival, authoritative replay, real Git/provider effects, task completion, or production composition without fakes.

Recommended acceptance-evidence additions:

- exact content upload/change → durable storage → authorized read-back with verified hash/version;
- provider-confirmed Git commit visible in a real supported provider repository;
- accepted operation/task state survives process restart and rebuilds from authoritative history;
- multiple replicas observe consistent authorization, idempotency, lifecycle, and projections;
- evidence uses production composition and real seams; mocks, fakes, source-text/prose gates, and in-memory stores may supplement but cannot satisfy runtime acceptance.

### 3. Reliability NFRs do not explicitly require restart/multi-replica survival

Deterministic replay and durable commit are present, but accepted mutations, task status, tenant-authority projections, idempotency, and read models are not explicitly required to survive process restart and remain consistent across replicas. Add mechanism-neutral reliability bullets for those outcomes.

### 4. Edge-security NFRs are incomplete

Add mechanism-neutral safety requirements for:

- bearer credentials never sent over plaintext non-loopback transport;
- caller-configurable provider endpoints blocked from loopback, private, link-local, metadata, and other prohibited destinations unless explicitly governed;
- all protected endpoints default to authenticated/deny-by-default behavior even when a handler omits local authorization code;
- locally stored CLI/MCP credentials use owner-only access and reject/warn on unsafe permissions; and
- retrieved repository content is explicitly marked untrusted data and can never acquire instruction authority merely because it came through Folders.

### 5. Operational truth is under-specified

Add NFRs requiring readiness to derive from actual required dependency and projection health, and requiring every declared release-critical metric/alert to have a production emission path and injected-fault evidence. A constant healthy snapshot or declaration-only instrument cannot satisfy readiness.

### 6. Index/reconciliation convergence is incomplete

OQ5 covers non-empty results, stale/unauthorized filtering, and unavailable behavior, but not durable retry after a failed index upsert/removal. Extend OQ5 or its evidence so a deletion/archive/revocation effect converges after broker/Memories outage and cannot remain silently searchable.

### 7. Agent protocol/provenance improvements need a separate PM decision

MCP safety annotations/typed schemas, content provenance/version pins, reference-to-path resolution, and untrusted-content response marking are valuable product concerns. Only untrusted-content safety should be added immediately as an NFR. The other API/tool expansions should be carried as explicit product decisions rather than inferred from the audit.

## Conflicts and supersession

### Ratified MVP option

The source initially presents Option A (control-plane product MVP) and Option B (real product MVP). Its later decision record ratifies **Option B**, and the current PRD already reflects it. Option A is superseded as a product-scope choice.

The phrase “current MVP is reframed as a control-plane MVP” can only describe the bounded implementation milestone represented by completed Epics 7/8. It must not weaken the PRD's product MVP, which still requires the durable repository-backed lifecycle. Use “control-plane milestone” or “contract milestone” for the implementation status to avoid two competing MVP definitions.

### Missing lifecycle API recommendations

The source recommends list/version/restore/move APIs as Phase-3/product work. The current PRD explicitly makes first-class move/rename and archived-folder restore non-goals for MVP; rename is add plus remove, and provider-history rewrite is excluded. Those audit recommendations do not override the ratified MVP scope and should remain post-MVP unless PM explicitly changes it. List-folders, version-history, and read-at-version likewise require separate product approval.

### Reconciliation mutation versus read-only MVP

The source proposes an operator API or worker that emits reconciliation-resolution events. The current PRD keeps the console read-only, forbids repair automation, and sends `reconciliation_required` to human escalation. A separate resolution-recording command may be compatible without repairing content, but it is a product-scope change and is not among the explicitly ratified decisions. Do not add it silently.

### Rate-limiting and AppHost decisions remain open

The source itself leaves rate-limit ownership and AppHost retention unresolved. Do not resolve those mechanisms in the PRD. The PRD may require bounded abuse protection and observable availability while architecture owns the placement.

### Audit snapshot limitations

The source's code/CI/build claims are tied to its HEAD, dirty tree, and audit limitations. They justify release blockers and evidence requirements but should not be copied as timeless PRD facts. Closure must be based on canonical evidence, approvers, version/digest, and current code—not the audit narrative alone.

## Recommended stable-ID edits or additions

Preserve all existing IDs and never renumber.

### Existing FR edits

- **FR2:** clarify that “demonstrates” means production-composition behavior using authoritative durable stores and a real supported provider, including restart/replay evidence; fake/in-memory demonstrations do not satisfy release acceptance.
- **FR32:** clarify that accepted add/change operations preserve the supplied bytes and verified hash in governed workspace state until durable commit or an explicit inspectable failure; acknowledgment may not discard content.
- **FR34–FR35:** clarify that authorized range/body reads return authoritative staged/committed content and include the approved provenance/version metadata; test-only sources do not satisfy the capability.
- **FR37:** no semantic change needed; it already requires provider-confirmed durable remote/ref update.
- **FR40:** no semantic change needed; it already requires stable evidence for incomplete and failed operations.

### Open-item edits/additions

- **OQ5 edit:** add outage/retry evidence proving failed metadata upserts and removals eventually converge, with archived/revoked/deleted hits suppressed throughout and removed after recovery.
- **OQ10 edit:** require the calibration plan to classify each evidence lane as real-production-seam, real-infrastructure, fake/mock, structural, or documentation-only; only the approved first two may establish runtime capability and performance.
- **Add OQ11 — Durable Product Data Plane:** block product release until exact-byte add/change/read-back, authoritative task completion, provider-confirmed real Git commit, restart/replay survival, and multi-replica correctness pass through production composition for both supported providers. Suggested evidence: `docs/exit-criteria/durable-product-round-trip.md`; owner Persistence + Git/Delivery; PM, Architecture, Security, and Test approvers.
- **Add OQ12 — Edge Security:** block release until HTTPS-or-loopback bearer handling, provider-endpoint SSRF controls, fallback authentication/authorization, service-event provenance, and safe local credential permissions pass positive and negative tests. Suggested evidence: `docs/exit-criteria/edge-security-evidence.md`; owner Security + Client/Provider Delivery; Security, Architecture, and PM approvers.
- **Add OQ13 — Operational Truth:** block release until current `main`/release gates are green, Production composition boots, readiness reflects real dependency/projection health, required durable stores/resiliency exist, UI/server health endpoints align with deployment, and release-critical alerts fire under injected faults. Suggested evidence: `docs/exit-criteria/production-operational-readiness.md`; owner Operations/SRE + Delivery; Operations, Architecture, Security, and Test approvers.

### New FRs

Do not add or renumber FRs solely to restate implementation ports. The core data-plane behavior belongs in strengthened FR2/FR32/FR34–FR35 and the new evidence gates. Any future MCP annotation, provenance resolver, reconciliation-resolution command, or lifecycle-history feature should receive the next stable FR ID only after explicit PM approval.

## Qualitative ideas the FR structure might otherwise drop

The source contains important product-governance insights worth preserving:

- **Control-plane quality can hide data-plane absence.** Rigorous contracts and adapters are valuable but not the product outcome.
- **A `202 Accepted` must never imply that content was preserved.** If bytes cannot be durably staged, the operation needs an explicit inspectable failure rather than silent disposal.
- **“Final PRD” is not “released product.”** Artifact maturity and implementation readiness require separate labels.
- **Real evidence must cross the load-bearing seams.** Substituting fakes for persistence, Git, content, readiness, or broker behavior proves orchestration logic only.
- **Healthy means actually serving.** Readiness and alerts must be driven by real dependency and projection state.
- **Restart and replica behavior are first-class product behavior.** Process-local correctness is insufficient for a tenant control plane.
- **Repository content is untrusted data for an LLM.** Retrieval must not confer instruction authority.
- **Security redaction must not make evidence unusable.** Authorized outputs need enough stable provenance to cite and act safely without exposing forbidden data.
- **Governance acceptance must name its bounded scope.** Contract/control-plane milestones can remain done while product-shippability remains no-go.

The audit already preserves its technical depth, options, findings, and file-level evidence. A separate PRD addendum would duplicate it; the PRD should link/list it as an input and distill only the product decisions and release gates.

## Concise disposition

**Apply targeted PRD changes.** Keep the Option-B real repository-backed MVP and FR1–FR58 numbering. Add an explicit no-go/current-readiness note, strengthen FR2/FR32/FR34–FR35 and acceptance evidence around exact bytes, real Git, task completion, restart/replay, replicas, and production seams; add missing edge-security/operational-truth NFRs; extend OQ5/OQ10; and append OQ11–OQ13 without renumbering. Do not import implementation class names, unratified lifecycle APIs, or repair mechanisms, and do not create an addendum solely to duplicate the audit.
