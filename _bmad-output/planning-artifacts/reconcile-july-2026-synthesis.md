---
title: July 2026 Sprint-Change Reconciliation Synthesis
date: 2026-07-15
source_reconciliations: 19
latest_authority: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md
latest_readiness: _bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md
latest_readiness_result: not-ready
prd_status_to_preserve: final
stable_ids_to_preserve: true
addendum_present: false
disposition: targeted-prd-patch-with-unratified-audit-proposals-retained
---

# July 2026 Reconciliation Synthesis

## Governing chronology and status precedence

This synthesis applies all 19 completed July reconciliation reports without re-ingesting their original proposals. The approved 2026-07-15 authority/delivery proposal governs current product and delivery posture, amends the approved 2026-07-14 structural correction, and supersedes earlier assumptions that safe-empty implementations can complete MVP capabilities or that product projections belong to Workstream 11.

The current PRD remains the authority for the later-approved metadata-token FR58 boundary, mandatory `unknown_provider_outcome` before `reconciliation_required`, canonical serializing identity, all-mutations idempotency, C3/C4/C9 decisions, and OQ1–OQ10. Earlier July sources remain provenance and downstream implementation/governance evidence unless this synthesis explicitly carries a product-level correction forward.

The resulting PRD patch is intentionally narrow: preserve `status: final`, every existing stable ID, and the current product scope; distinguish document finality from implementation readiness; correct UJ2, FR56, and OQ8; and keep implementation mechanics in downstream artifacts. The audit-derived FR/NFR/evidence changes and proposed OQ11–OQ13 are retained below for future PM ratification, but chronology does not permit treating them as current requirements: their source did not approve those exact PRD changes, and the later approved 2026-07-15 authority expressly names OQ1–OQ10 as the release-gate inventory.

## Exact PRD patch plan

### 1. Frontmatter and provenance

Preserve `status: final`, `created`, `completedAt`, and all existing history. Set or add:

```yaml
updated: '2026-07-15'
finalized: '2026-07-15'
lastEdited: '2026-07-15'
implementationReadiness: not-ready
implementationReadinessAssessedAt: '2026-07-15'
implementationReadinessSource: '_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md'
productMvpDecision: durable-repository-round-trip-required
productMvpDecisionRatifiedAt: '2026-07-14'
```

Extend `documentCounts` without changing existing values:

```yaml
  changeProposals: 19
  readinessReports: 2
  reconciliations: 1
```

Replace `inputDocuments` with this exact ordered inventory:

```yaml
inputDocuments:
  - "_bmad-output/planning-artifacts/product-brief-Hexalith.Folders.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-tenants-integration-for-folder-management-application-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-eventstore-domain-aggregates-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-forgejo-and-github-api-research-2026-05-05.md"
  - "_bmad-output/brainstorming/brainstorming-session-20260505-070846.md"
  - "_bmad-output/planning-artifacts/architecture.md"
  - "_bmad-output/project-context.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-06-builds-package-version-centralization.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-081620.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-090742.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-190839.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-193110.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-content-materializer.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-dcp-lane-standup.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-governance-approval-freshness.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-honest-green-gate-baseline.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-idempotency-key-canonicalization.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-memories-facade-dapr-egress.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-nfr-traceability-decoupling.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-07-seed-backed-read-models.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-08-rest-negative-path-coverage.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-incremental-review.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md"
  - "_bmad-output/planning-artifacts/reconcile-july-2026-synthesis.md"
```

Prepend this exact `editHistory` entry:

```yaml
  - date: '2026-07-15'
    changes: 'Reconciled 19 July sprint-change proposals under the approved 2026-07-15 authority; recorded not-ready implementation posture; corrected UJ2, FR56, and OQ8; preserved OQ1-OQ10 and the metadata-token FR58 boundary; retained unratified audit-derived FR/NFR/evidence and OQ11-OQ13 proposals for separate PM approval.'
```

Do not add all 19 `reconcile-*` files individually to `inputDocuments`; the single synthesis file is their audit pointer. The original proposals remain the contributed sources.

### 2. Current Delivery Posture

Insert this subsection immediately after the final paragraph of **MVP Contract Summary** and before **Success Criteria**:

```markdown
### Current Delivery Posture

This PRD is final as a product contract; the implementation is not ready for product release as of the 2026-07-15 implementation-readiness assessment. Completed contract, adapter, authorization, governance, accessibility, topology, and fail-safe foundations remain valid increments, but they do not complete or release the product MVP. Release remains blocked until the durable repository-backed lifecycle and OQ1–OQ10 close with approved production evidence. Safe-empty, seed-only, unavailable, no-op, fake-backed, structurally asserted, documentation-only, or numerically mapped behavior is not positive capability evidence.
```

Do not copy the time-bound 42/58 coverage count, issue counts, epic numbers, story counts, or current implementation class names into this subsection.

### 3. Replace UJ2 without changing its stable ID

Replace the complete current UJ2 section with:

```markdown
### UJ2: Tenant Administrator Establishes Provider Readiness

Ravi is a tenant administrator responsible for making his tenant ready for Git-backed agent work. Before an agent can create a repository-backed folder or workspace, he configures the provider binding, non-secret credential reference, repository naming/default-ref policy, and minimum provider-capability policy for his tenant.

Ravi runs readiness validation and sees a clear state rather than a generic failure. A scoped platform engineer may help validate or diagnose the configured provider profile, but cannot silently change Ravi's tenant policy. Wrong-tenant, operator-only, stale, revoked, and hidden-resource attempts fail before provider, repository, or credential observation and return a safe metadata-only denial.

If a credential reference is missing, permission is insufficient, a provider is unavailable, or branch/ref policy is invalid, the system reports a stable machine-readable reason and safe diagnosis without displaying secrets. The value moment is confidence before runtime: Ravi can correct tenant-owned configuration before an agent task fails partway through repository creation or commit.

This journey reveals requirements for tenant-administrator provider configuration, credential references, repository/default-ref and capability policy, scoped platform validation, provider readiness, safe reason codes, metadata-only denial evidence, and secret-safe diagnostics.
```

### 4. Strengthen existing functional requirements without renumbering

Replace **FR2** with:

```markdown
- FR2: Each required surface documents and demonstrates the ordered canonical lifecycle from provider readiness through binding, preparation, lock, mutations, one provider-confirmed durable commit, authoritative context/status/audit, and cleanup visibility, including failure transitions. Release demonstration must use Production composition, authoritative durable stores, and a real supported provider, and must prove restart/replay survival; fake, mock, in-memory, structural, or documentation-only demonstrations may supplement but cannot satisfy runtime acceptance.
```

Replace **FR32** with:

```markdown
- FR32: Authorized actors can apply one or many add/change/remove mutations as one validated change-set command within a prepared, freshly authorized, locked task workspace without auto-commit. The complete request is validated before execution; a first-class move/rename is not MVP and is represented by add plus remove under the same task, lock, change set, and commit. Accepted add/change intent preserves the supplied bytes and verified hash/version in governed authoritative workspace state until provider-confirmed durable commit or an explicit inspectable failure; acknowledgement may not silently discard content.
```

Replace **FR34** with:

```markdown
- FR34: Authorized actors can request policy-filtered live-workspace context through tree, metadata, glob, bounded range, and supported text-body search with at most 100 requested paths, 2,000 tree entries, 500 search/glob results, a 262,144-byte bounded range, a 1,048,576-byte aggregate response, and 2 seconds of server execution. Range and body-search results come from authoritative committed content or, only for the freshly authorized originating task, its governed staged content; they carry the Contract Spine's C9-safe repository/ref/commit-or-task provenance and content version/hash metadata. Seed-only or test-only read sources cannot satisfy capability acceptance.
```

Replace **FR35** with:

```markdown
- FR35: Live-workspace context queries establish fresh tenant/folder/task authority and path policy before scanning, lookup, filtering, ranking, counting, snippet generation, truncation, or shaping. Body-search results contain only authorized C9-wrapped relative identity, authoritative content provenance/version, line/byte location, match classification, and a bounded live snippet. Supported truncation sets `isTruncated`; range/file content is never silently truncated; unsupported excess returns the stable input/response-limit result without logging raw queries, path lists, content, snippets, or hidden existence.
```

Replace **FR56** with:

```markdown
- FR56: Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only after the same actor holds incident-admin permission and fresh current tenant/folder authorization before stream lookup, event counting, checkpoint lookup, filtering, or shaping. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path. Missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.
```

No other FR changes are required. Preserve FR1, FR3–FR31, FR33, FR36–FR55, and FR57–FR58 exactly as currently written. In particular, keep FR58 as metadata-token recall; body-content indexing/recall remains outside MVP pending a future stable FR and separate Security/PM approval.

### 5. Add mechanism-neutral NFR bullets

Append these bullets to **Security and Tenant Isolation**:

```markdown
- Bearer credentials may be emitted only to HTTPS endpoints or explicitly recognized loopback development endpoints; non-HTTPS non-loopback destinations are rejected before any credential is attached or transmitted.
- Caller-configurable provider endpoints fail closed against loopback, private, link-local, metadata-service, multicast, unspecified, and other prohibited destinations unless an explicitly governed provider profile authorizes the destination after canonical resolution and redirect validation.
- Every protected endpoint and internal service/event boundary defaults to authenticated, deny-by-default behavior even when a local handler omits an authorization declaration; missing or invalid service identity, provenance, tenant scope, or policy evidence cannot invoke, publish, consume, or project protected behavior.
- CLI and MCP credential material stored locally uses owner-only access where the platform supports it; unsafe permissions are rejected or surfaced as a blocking warning before use.
- Repository and workspace content returned to an AI or agent is untrusted data. Content, filenames, metadata, provider text, or retrieved instructions cannot acquire system, policy, tool, or authorization authority merely because Folders returned them.
```

Append these bullets to **Reliability, Idempotency, and Failure Visibility**:

```markdown
- Accepted mutation intent, governed file bytes, task/operation state, idempotency replay or consumed-key evidence, authorization-critical projections, and required read models survive process restart without converting accepted work into loss, success, or an opaque pending state.
- Multiple service replicas observing the same authoritative history must converge on equivalent authorization, idempotency, lifecycle, task-terminal, cleanup, and projection outcomes; process-local state cannot be the sole authority for a released capability.
```

Append these bullets to **Observability, Auditability, and Replay**:

```markdown
- Production readiness derives from the actual health and freshness of required durable stores, providers, internal service/event paths, projections, and background reconciliation; a constant, seed-only, declared-only, or locally assumed healthy snapshot cannot report the product ready.
- Every release-critical metric and alert has a production emission path, an accountable response owner, and injected-fault evidence proving that the signal fires and identifies the affected dependency or product state.
```

Append these bullets to **Verification Expectations**:

```markdown
- Runtime-capability and performance evidence must identify whether each lane uses Production composition and real load-bearing seams, real infrastructure with approved substitutions, fake/mock seams, structural/source checks, or documentation assertions. Only evidence classes approved by OQ10 may establish runtime capability or performance; supporting classes cannot silently substitute for them.
- Edge-security verification covers bearer transport, provider-endpoint destination controls and redirects, fail-safe authentication/authorization defaults, internal service/event provenance, local credential permissions, and untrusted-content marking through positive and negative scenarios.
```

### 6. Add MVP acceptance evidence

Append these bullets to **MVP Acceptance Evidence** after the existing bullets:

```markdown
- Exact-byte durability evidence performs authorized add and change operations, reads the authoritative staged and committed content back with matching bytes and approved hash/version metadata, and proves that acknowledgement never discards accepted content.
- Real-provider evidence completes a provider-confirmed Git commit against both supported providers and verifies the returned repository/ref/commit identity against the authoritative remote rather than a fake, no-op, or local-only executor.
- Restart/replay evidence accepts work, restarts the relevant Production-composed processes, reconstructs task/lifecycle/idempotency/projection state from authoritative history, and reaches the same inspectable terminal or governed non-terminal outcome.
- Multi-replica evidence proves equivalent tenant authorization, canonical lock collision, idempotency, task completion, cleanup eligibility, and read-model results across concurrent service replicas.
- Production-composition evidence crosses the load-bearing persistence, content, provider, event, projection, and reconciliation seams. Fake/mock, in-memory, source-text, structural, screenshot, and documentation-only evidence may supplement diagnosis but cannot establish runtime product capability or performance.
```

### 7. Edit existing Open Release Items

In the **Open Release Items** introductory paragraph, replace `OQ5–OQ10` with `OQ5–OQ13` and state that all OQ1–OQ13 close before release acceptance.

Replace **OQ5** with this row:

```markdown
| OQ5 | Replace the fail-safe but functionally empty FR58 search/status facade with evidence for authorized non-empty metadata-token results, indexing status, stale/unauthorized hit removal, unavailable behavior, and durable convergence after failed metadata upsert/removal or broker/Memories outage. Archived, revoked, deleted, and hidden hits remain suppressed throughout failure and are removed after recovery. | Search/Delivery | Blocks FR58 implementation readiness; close when coverage and tests round-trip FR58 and both C13 operations through positive, outage, retry, removal/archive/revocation, unavailable, and recovery scenarios. | `docs/exit-criteria/fr58-search-evidence.md`; PM, Security, and Test approvers. |
```

Replace **OQ8** with this row:

```markdown
| OQ8 | Align architecture, Contract Spine, SDK, C13, storage/retention evidence, and tests to the all-mutations idempotency rule, read-key rejection, expired-key precedence, and minimal metadata-only consumed-key digest/tombstone persistence after replay-result expiry so an old key cannot appear unused. | Architecture + Contract/Delivery | Blocks idempotency completeness; close when every mutation/read cell and equivalent/conflicting live/expired-key case passes and Architecture, Security, and Test approve the retention/persistence model. | `docs/contract/idempotency-and-parity-rules.md` plus the versioned C13 snapshot and approved consumed-key retention evidence; Architecture, Security, and Test approvers. |
```

Replace **OQ10** with this row:

```markdown
| OQ10 | Publish the release-calibration plan with frozen populations, exclusions, environments, scenarios, methods, evidence owners, approval rules, and an evidence classification for every lane: Production composition/real seam, real infrastructure with approved substitution, fake/mock, structural/source, or documentation-only. The plan states which classes may establish runtime capability and performance; supporting classes cannot substitute for them. | PM + Test/Quality | Blocks use of SM1–SM8 and CM1–CM4 results for release acceptance; close before calibration evidence is collected. | `docs/exit-criteria/release-calibration-plan.md`; PM and Test/Quality approvers. |
```

Preserve OQ1–OQ4, OQ6–OQ7, and OQ9 unchanged.

### 8. Add OQ11–OQ13 without renumbering existing items

Append these rows after OQ10:

```markdown
| OQ11 | Prove the durable product data plane through exact-byte add/change/authorized read-back with approved hash/version metadata, authoritative task completion, provider-confirmed real Git commit for GitHub and Forgejo, restart/replay survival, deterministic production read-model rebuild, and multi-replica correctness through Production composition without fake, no-op, unavailable, seed-only, or process-local substitutions. | Persistence + Git/Delivery | Blocks product-MVP and release acceptance; close when the canonical durable round trip passes for both providers and the evidence is approved. | `docs/exit-criteria/durable-product-round-trip.md`; PM, Architecture, Security, and Test approvers. |
| OQ12 | Prove edge security through HTTPS-or-loopback bearer handling, provider-endpoint SSRF and redirect controls, fail-safe endpoint and internal service/event authentication/authorization, trusted provenance and tenant scope, safe local CLI/MCP credential permissions, and explicit untrusted-content handling. | Security + Client/Provider Delivery | Blocks release safety acceptance; close when positive and negative edge, bypass, redirect, fallback, provenance, permission, and untrusted-content scenarios pass. | `docs/exit-criteria/edge-security-evidence.md`; Security, Architecture, and PM approvers. |
| OQ13 | Prove operational truth: the current `main`/release gates are green; Production composition boots with required durable stores, resiliency, providers, internal paths, and projections; readiness reflects actual dependency/projection health; UI/server health endpoints align with deployment; and every release-critical alert fires under injected faults. | Operations/SRE + Delivery | Blocks production-operational readiness; close when boot, dependency degradation, projection lag/failure, endpoint, recovery, and alert evidence is approved. | `docs/exit-criteria/production-operational-readiness.md`; Operations, Architecture, Security, and Test approvers. |
```

Update the shared closing paragraph from “An open item” to “An OQ1–OQ13 item” while retaining the existing identity/date/version/digest and reopening rule.

## Explicit no-ops and superseded material

| Reconciled source | Synthesis disposition |
| --- | --- |
| 2026-07-06 Builds package centralization | PRD no-op; retain package provenance/verification NFRs, keep MSBuild mechanics downstream. |
| 2026-07-07 canonical `.slnx` inventory | Approved no-op; do not add solution counts or confuse build inventory with C13. |
| 2026-07-07 08:16 domain-focus refactor | Growth/platform-boundary wording is already present; static `47/47`, `chore(deps)`, AppHost exception, and Story 11.10 product ownership are not imported. |
| 2026-07-07 09:07 readiness remediation | PRD no-op; current FR58/OQ5 supersede “implemented through Epic 10.” |
| 2026-07-07 19:08 bridge-read deferral | Fail-safe unavailable remains required; limitation-only release closure and Story 11.10 product ownership are superseded by OQ5/product ownership. |
| 2026-07-07 19:31 gateway-double guard | Pending source is non-normative; guard remains downstream, with later focused ownership superseding broad Story 11.7 placement. |
| 2026-07-07 content materializer | PRD no-op; current metadata-token FR58 and OQ5 govern; body indexing remains a future product decision. |
| 2026-07-07 DCP lane stand-up | Partial live evidence only; skipped round trips do not close OQ5/OQ11/OQ13. |
| 2026-07-07 approval freshness | PRD no-op; 365-day mechanics remain governance evidence, while current C3/C4 and shared closure rule remain authoritative. |
| 2026-07-07 honest-green UI/accessibility baseline | PRD no-op; preserve full blocking/no-narrowing principle downstream, not historical `63` or job/class names. |
| 2026-07-07 idempotency-code canonicalization | Exact error literal stays in Contract Spine/error catalog; OQ8 now owns exact-code and expired-key evidence. |
| 2026-07-07 Memories Dapr egress | Dapr URL mechanics stay downstream; production bypass/override evidence is absorbed by OQ12, FR58 behavior by OQ5. |
| 2026-07-07 NFR traceability decoupling | PRD no-op; criterion approval and downstream evidence status remain independently governed. |
| 2026-07-07 seed-backed read models | Earlier accepted-MVP-limitation posture is superseded; safe-empty is interim fail-safe behavior and OQ6 blocks capability completion. |
| 2026-07-08 REST negative coverage | PRD no-op; OQ3 must resolve authenticated cross-tenant versus absent-resource equivalence before route tests pin 403/404. |
| 2026-07-14 incremental readiness review | Amended by 2026-07-15; retain durable-MVP and final-versus-not-ready distinction, use latest assessment date. |
| 2026-07-14 structural correction | Amended by 2026-07-15; keep planning-authority/product-ownership corrections, discard its broader FR58 body-content interpretation. |
| 2026-07-14 production-readiness audit | Carry forward FR2/FR32/FR34–FR35, NFR, acceptance, OQ5/OQ10, and OQ11–OQ13 corrections; keep code inventory and unratified lifecycle APIs out. |
| 2026-07-15 authority/delivery reconciliation | Governing source for UJ2, FR56, OQ8, readiness metadata, current delivery posture, product projection ownership, and supersession. |

No `addendum.md` should be created. All technical mechanisms, alternatives, file/class inventories, story numbering, estimates, test counts, and governance schemas remain in their existing proposals and downstream artifacts.

## Exact memlog entries to append

Append these entries in this order through `memlog.py`; do not edit `.memlog.md` by hand:

| Type | Exact text |
| --- | --- |
| event | Reconciled all 19 July 2026 sprint-change inputs through reconcile-july-2026-synthesis.md; chronology and status precedence use the approved 2026-07-15 authority. |
| override | The approved 2026-07-15 authority amends the 2026-07-14 structural correction and supersedes earlier safe-empty-as-MVP and Workstream-11-product-projection ownership assumptions. |
| change | PRD artifact status remains final while implementation readiness is recorded as not-ready from implementation-readiness-report-2026-07-15.md; the durable repository round trip remains the ratified product MVP. |
| change | Added Current Delivery Posture: completed control-plane and fail-safe foundations remain valid increments but cannot establish product-MVP or release acceptance. |
| decision | Tenant administrators own provider, credential-reference, repository/default-ref, capability, ACL, and archive policy; scoped platform engineers validate and diagnose without silently mutating tenant policy. |
| change | Corrected UJ2 under its stable ID to reflect tenant-administrator configuration authority and safe scoped platform validation. |
| change | Strengthened FR2, FR32, and FR34-FR35 under their existing IDs to require authoritative bytes/content provenance, Production composition, real-provider durability, and restart/replay evidence. |
| decision | Incident evidence requires incident-admin permission plus fresh current tenant/folder authorization before any stream, count, checkpoint, filter, or shape observation. |
| change | Strengthened FR56 under its stable ID with explicit dual authorization, pre-observation safe denial, bounded C9-redacted evidence, and denial audit. |
| decision | Expired idempotency keys retain minimal metadata-only consumed-key evidence after replay-result expiry so equivalent or conflicting reuse returns idempotency_key_expired and never appears unused. |
| change | Expanded OQ8 under its stable ID to require Contract Spine, SDK, C13, storage/retention, and live/expired-key evidence for the consumed-key persistence model. |
| change | Added restart/multi-replica reliability, edge-security, untrusted-content, actual-readiness, production-alert, verification-classification, and durable MVP acceptance evidence without adding functional requirements. |
| change | Expanded OQ5 for outage/removal convergence and OQ10 for evidence-classification rules while preserving their stable IDs. |
| decision | OQ11 blocks release on the durable exact-byte, authoritative task, real Git, restart/replay, deterministic projection, and multi-replica product round trip for GitHub and Forgejo. |
| decision | OQ12 blocks release on bearer transport, provider SSRF/redirect controls, fail-safe endpoint and internal-boundary authorization/provenance, local credential permissions, and untrusted-content evidence. |
| decision | OQ13 blocks release on truthful Production boot/readiness, durable dependency/projection health, deployment-aligned endpoints, current release gates, and injected-fault alert evidence. |
| override | FR58 remains authorized metadata-token recall; cross-workspace indexed body content or snippets remain outside MVP pending a future stable FR and separate Security/PM approval. |
| event | July 2026 reconciliation patch plan completed with stable IDs preserved, PRD status final preserved, no addendum created, and OQ1-OQ13 designated as release blockers. |

## Unresolved ambiguity

The only material approval ambiguity is the **naming of OQ11–OQ13**. Their underlying durable-data-plane, edge-security, and operational-truth scope is carried by the ratified durable-MVP decision and the later approved Epic 12/13 planning, and no existing OQ1–OQ10 fully owns those release outcomes. However, the approved 2026-07-15 proposal repeatedly calls OQ1–OQ10 the explicit release gates and does not itself assign the OQ11–OQ13 identifiers. This synthesis recommends adding the three rows above to prevent those ratified blockers from remaining story-only. If governance interprets the 2026-07-15 OQ1–OQ10 wording as an exhaustive closed inventory, PM must explicitly ratify the three new IDs before the PRD patch is applied; all other patch items are unambiguous.
