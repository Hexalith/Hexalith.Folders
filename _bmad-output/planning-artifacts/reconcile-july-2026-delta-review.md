---
reviewed_sources:
  - reconcile-sprint-change-proposal-2026-07-14-implementation-readiness-incremental-review.md
  - reconcile-sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md
  - reconcile-sprint-change-proposal-2026-07-14.md
  - reconcile-sprint-change-proposal-2026-07-15.md
verified_against:
  - prd.md
  - .memlog.md
latest_authority: reconcile-sprint-change-proposal-2026-07-15.md
disposition: apply-latest-authorized-delta-and-resolve-oq11-oq13-authority
---

# July 2026 Product-Level Delta Review

## Verification result

The current PRD already contains the durable repository-backed MVP, current FR58 metadata-token boundary, C3/C4 corrections, operator-state refinement, and OQ1–OQ10 release-gate structure. The latest approved July 15 extract leaves six definite product-document changes: UJ2 authority, FR56 dual authorization, OQ8 expired-key evidence, latest readiness metadata, a delivery-posture clarification, and provenance/edit-history/memlog updates.

The July 14 production audit additionally proposes stronger durability, edge-security, and operational-truth gates, including OQ11–OQ13. Those are not present in the PRD. However, the audit's broader application was pending, while the latest approved July 15 extract says FR1–FR55 and OQ1–OQ7/OQ9–OQ10 need no edits and frames OQ1–OQ10 as the explicit release-gate set. OQ11–OQ13 therefore have usable proposed wording below but are **not safely classifiable as approved requirements without an explicit authority decision**.

## Exact required changes under the latest approved authority

### 1. Frontmatter: separate document finality from implementation readiness

Retain `status: final`. Add:

```yaml
implementationReadiness: not-ready
implementationReadinessAssessedAt: '2026-07-15'
implementationReadinessSource: '_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md'
productMvpDecision: durable-repository-round-trip-required
productMvpDecisionRatifiedAt: '2026-07-14'
```

The July 15 assessment supersedes the July 14 date/source for current readiness. It does not supersede the durable-MVP ratification date.

### 2. Current Delivery Posture: add one non-normative paragraph

Add near **MVP Contract Summary**:

> **Current delivery posture.** Completed contract, adapter, authorization, governance, accessibility, topology, and fail-safe foundations remain valid increments, but they do not complete or release the product MVP. Release remains blocked until the durable repository-backed lifecycle and every Open Release Item close with approved production evidence. Safe-empty, seed-only, unavailable, no-op, fake-backed, numerically mapped, structural, or documentation-only evidence may prove safety or contract shape but does not prove positive runtime capability.

“Every Open Release Item” avoids hard-coding OQ1–OQ10 while the OQ11–OQ13 authority question is resolved.

### 3. UJ2: correct tenant-policy authority without renumbering

Replace the current UJ2 with:

```markdown
### UJ2: Tenant Administrator Establishes Provider Readiness

Ravi is a tenant administrator responsible for making his tenant ready for Git-backed agent work. Before an agent can create or bind a repository-backed folder, he configures the provider binding, credential reference, repository naming and default-ref policy, and minimum capability policy for that tenant.

A scoped platform engineer may validate and diagnose the resulting readiness but cannot silently change Ravi's tenant policy. If a credential reference is missing, permissions are insufficient, the provider is unavailable, or repository/default-ref policy is invalid, readiness reports a stable safe reason and remediation category without exposing secrets or hidden resources. An operator-only, wrong-tenant, stale, revoked, or otherwise unauthorized attempt fails before configuration or protected-state observation.

The value moment is controlled readiness before runtime: Ravi owns tenant policy, while the platform engineer can prove whether the configured tenant is ready without acquiring tenant-policy mutation authority.

This journey reveals requirements for tenant-owned provider and credential-reference configuration, repository/default-ref and capability policy, scoped readiness validation, actionable safe diagnostics, and fail-closed authority boundaries.
```

**Stable ID affected:** UJ2 only. FR4 and FR15 already carry the correct normative authority.

### 4. FR56: make dual authorization complete at clause level

Replace FR56 with:

> **FR56:** Normal operation timelines come from projections. During projection degradation, bounded redacted event evidence is available only after the same actor holds incident-admin permission and fresh current tenant/folder authorization before stream lookup, event counting, checkpoint lookup, filtering, or shaping. The view remains metadata-only and read-only, shows a persistent degraded warning, last checkpoint, correlation ID, and time window, and exposes no mutation or repair path; missing-admin, wrong-tenant, revoked, stale, hidden-resource, and folder-denied attempts fail before observation and emit one safe denial audit record.

**Stable ID affected:** FR56 only. This aligns clause-level extraction with Public Surfaces and OQ9.

### 5. OQ8: add expired-key precedence and minimal consumed-key persistence

Replace the OQ8 row with:

| ID | Decision/evidence still open | Delivery owner | Blocking consequence and revisit condition | Canonical evidence and accountable approvers |
| --- | --- | --- | --- | --- |
| OQ8 | Align architecture, Contract Spine, SDK, C13, storage/retention evidence, and tests to the all-mutations idempotency rule, read-key rejection, expired-key precedence, and minimal metadata-only consumed-key digest/tombstone evidence that keeps an expired key recognizable after replay-result expiry. | Architecture + Contract/Delivery | Blocks idempotency completeness; close when every mutating operation and read cell passes the rule and equivalent or conflicting reuse of an expired key deterministically returns `idempotency_key_expired` without re-execution or protected prior-intent disclosure. | `docs/contract/idempotency-and-parity-rules.md` plus versioned C13 snapshot and canonical consumed-key retention evidence; Architecture, Security, and Test approvers. |

**Stable ID affected:** OQ8 only. The storage mechanism remains architecture-owned; FR41–FR42 already fix the product outcome.

### 6. Provenance, edit history, and memlog

Add to `inputDocuments`:

```yaml
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-15.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-15.md"
  - "_bmad-output/planning-artifacts/implementation-readiness-report-2026-07-14.md"
  - "_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14-implementation-readiness-structural-correction.md"
```

If any July 14 audit recommendations below are approved, also add `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-14.md`.

Add an edit-history entry dated 2026-07-15 stating that the latest authority/readiness proposal amended the July 14 structural correction, updated UJ2/FR56/OQ8, preserved metadata-only FR58, and separated PRD finality from implementation readiness.

Append memlog entries through `memlog.py`, not manually:

- decision: the July 15 approved proposal is the latest authority/delivery posture and amends the July 14 structural correction;
- change: UJ2 now separates tenant-policy ownership from scoped platform readiness validation;
- change: FR56 now requires incident-admin plus fresh current tenant/folder authorization before any incident-stream observation;
- change: OQ8 now includes expired-key precedence and minimal metadata-only consumed-key persistence/retention;
- event/change: current implementation readiness is not-ready per the immutable 2026-07-15 report while the PRD document remains final;
- override: FR58 remains metadata-token recall and body-content recall remains separately gated future scope.

## Stable-ID delta

| Stable ID/section | Required action | Authority |
| --- | --- | --- |
| UJ2 | Replace actor/authority journey wording; retain ID. | July 15 approved |
| FR56 | Add pre-observation dual authorization and safe denial audit; retain ID. | July 15 approved |
| OQ8 | Expand expired-key/consumed-key evidence; retain ID. | July 15 approved |
| FR58 | No edit; preserve metadata-token scope. | July 15 supersedes July 14 ambiguity |
| FR1–FR55, FR57 | No edit from the latest authority. | July 15 approved |
| OQ1–OQ7, OQ9–OQ10 | No edit from the latest authority. | July 15 approved |
| Frontmatter / MVP Contract Summary | Add readiness fields and delivery posture; no stable ID. | July 15 approved |
| OQ11–OQ13 | Proposed below; approval status unresolved. | July 14 audit recommendation not expressly ratified July 15 |

## Proposed OQ11–OQ13

These rows are ready for insertion **if** Administrator confirms that the July 14 production-audit gate recommendations survive the July 15 amendment.

| ID | Decision/evidence still open | Delivery owner | Blocking consequence and revisit condition | Canonical evidence and accountable approvers |
| --- | --- | --- | --- | --- |
| OQ11 | Prove the durable repository-backed product round trip through production composition for GitHub and Forgejo: exact-byte add/change and authoritative read-back with verified hash/version, authoritative accepted-task completion, provider-confirmed real Git commit, restart/replay survival, deterministic rebuild, and multi-replica consistency for authorization, idempotency, lifecycle, and projections. | Persistence + Git/Delivery | Blocks product-MVP implementation readiness and release; close only when positive, failure, restart/replay, and replica scenarios pass through authoritative durable stores and real supported-provider seams. Fakes, mocks, in-memory stores, structural checks, and documentation assertions may supplement but cannot close the gate. | `docs/exit-criteria/durable-product-round-trip.md`; PM, Architecture, Security, and Test approvers. |
| OQ12 | Prove edge-security safety for public/client/provider boundaries: bearer credentials use HTTPS or loopback-only transport; caller-configurable provider egress denies loopback, private, link-local, cloud-metadata, and other prohibited destinations unless explicitly governed; protected endpoints authenticate and deny by default; service-originated events carry verifiable provenance; local CLI/MCP credential storage enforces or safely rejects unsafe permissions; and retrieved repository content is marked untrusted data without instruction authority. | Security + Client/Provider Delivery | Blocks security acceptance and production release; close when positive and negative tests cover permitted/denied transports and destinations, omitted-handler fallback, forged provenance, unsafe local permissions, and untrusted-content handling without leaking protected values. | `docs/exit-criteria/edge-security-evidence.md`; Security, Architecture, and PM approvers. |
| OQ13 | Prove operational truth in production composition: the release build boots with required durable stores and resiliency, readiness derives from actual required dependency and projection health, UI/server health endpoints agree with deployed state, current release gates are green, and every declared release-critical metric/alert has a production emission path and fires under injected faults. | Operations/SRE + Delivery | Blocks production operational readiness and release; close when healthy, degraded, unavailable, restart, dependency-loss, projection-lag, and alert-injection scenarios produce truthful status and bounded evidence; constant/declaration-only health cannot close the gate. | `docs/exit-criteria/production-operational-readiness.md`; Operations, Architecture, Security, and Test approvers. |

If inserted, update the Open Release Items introduction and delivery-posture text from OQ1–OQ10 to **OQ1–OQ13**, while preserving the distinction that OQ1–OQ4 close bounded parameters/inventories and OQ5–OQ13 close implementation/release evidence.

## July 15 supersession over July 14

1. **Readiness date/source:** use the 2026-07-15 NOT READY report, not the July 14 report, for current frontmatter. Preserve July 14 as amended provenance.
2. **FR58:** July 15 definitively preserves metadata-token recall as current FR58. July 14 “content search” or Story 10.9 body-materialization framing cannot broaden it; indexed body recall requires separately approved future scope.
3. **Document status:** retain `status: final`; do not apply July 14 `status: complete`. Readiness is a separate axis.
4. **Operator disposition:** retain current `auto-reconciling` for `unknown_provider_outcome`; do not collapse it into July 14's five-value shorthand.
5. **Unresolved architecture decisions:** retain OQ1–OQ4 and **Architecture Decisions Needed Next**; July 14 blanket “resolved” wording is superseded.
6. **Stable FR edits:** July 15 says FR1–FR55 and FR57–FR58 need no further edits except FR56. This supersedes the July 14 audit extract's proposed FR2, FR32, and FR34–FR35 edits unless separately re-approved.
7. **Existing OQ edits:** July 15 requires OQ8 only and says OQ1–OQ7/OQ9–OQ10 are covered. This supersedes the July 14 audit extract's proposed OQ5 convergence and OQ10 evidence-lane edits unless separately re-approved.
8. **Edge-security/operational NFR additions:** July 15 does not ratify the July 14 audit's plaintext bearer, SSRF, local credential-permission, untrusted-content, readiness-health, and alert-emission NFR additions. They remain candidate decisions represented by proposed OQ12/OQ13, not mandatory current PRD text.
9. **Planning ownership:** July 15's latest authority amends July 14 story/ownership assumptions; epic/story/manifest changes remain outside the PRD.

## Unresolved ambiguity

The July 14 production-audit reconciliation recommends OQ11–OQ13 and calls the associated product/NFR gaps release-blocking, but its source says broad application was pending and only the durable-product Option B/Epic 12 decisions were explicitly ratified. The latest approved July 15 reconciliation neither explicitly accepts nor explicitly rejects OQ11–OQ13; it instead says OQ1–OQ10 are the explicit release gates and recommends no new items.

Therefore:

- UJ2, FR56, OQ8, latest readiness metadata/posture, and provenance are unambiguously required.
- OQ11 has strong support from the ratified durable-product decision but still conflicts procedurally with July 15's stated OQ1–OQ10 gate set.
- OQ12 and OQ13 remain unratified audit recommendations.
- Do not append OQ11–OQ13 or the superseded FR/OQ edits until Administrator clarifies whether July 15 intended to cap the gate inventory at OQ10 or merely omitted the independent July 14 audit recommendations.
