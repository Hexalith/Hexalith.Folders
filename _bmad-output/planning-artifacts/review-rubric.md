# PRD Quality Review — Hexalith.Folders

## Overall verdict

This is a strong, unusually rigorous chain-top product contract: its thesis, safety invariants, scope boundary, stakeholder journeys, quantitative outcomes, and release blockers are explicit enough to support planning and governance. It is not yet safe to treat as release-acceptance complete because one stale fallback contradicts the ratified MVP, four binding contract decisions remain open, and a small set of NFRs and capability-only FRs still need sharper standalone completion tests.

## Decision-readiness — adequate

The central decisions are direct and consequential: the MVP is repository-backed rather than local-first (§ Product Scope), all four public contract surfaces are required (§ MVP Contract Summary), the console is read-only, provider readiness is a gate, and authority conflicts block release rather than being resolved ad hoc. The PRD also distinguishes a final product contract from a not-ready implementation (§ Current Delivery Posture), and OQ1–OQ10 name owners, blocking consequences, evidence, and approvers.

One sentence nevertheless reopens the core product decision without the governance discipline applied everywhere else.

### Findings

- **high** Fallback contradicts the ratified MVP (§ Innovation & Novel Patterns → Risk Mitigation; § MVP Contract Summary; § Implementation Considerations) — The proposed fallback to “a simpler Git-backed file API” lists repository and file CRUD, commit, and status while omitting the task lifecycle, readiness, locking, audit, tenant controls, and cross-surface contract that the rest of the PRD calls the non-negotiable MVP. It creates an unauthorized escape hatch from the `durable-repository-round-trip-required` decision and the explicit rule that safety, idempotency, failure visibility, and core-surface parity are not optional cuts. *Fix:* Remove the fallback, or state that any such reduction is a new PM/governance scope decision that cannot ship as this MVP and must preserve the named safety invariants.

## Substance over theater — strong

The document's detail is earned by the product's failure modes. The nine journeys cover distinct actor or operational situations rather than ornamental personas; the Innovation section identifies a specific internal replacement target; the Vision is Hexalith-specific; and NFRs overwhelmingly contain concrete isolation, latency, capacity, retention, reconciliation, accessibility, and parity consequences.

### Findings

- None.

## Strategic coherence — adequate

The thesis is clear: production agents need a tenant-aware workspace lifecycle instead of direct filesystem, Git, credential, and cleanup ownership. The repository-backed platform MVP follows that thesis, and SM1–SM8 test lifecycle completion, isolation, diagnostic trust, context effectiveness, parity, adoption, latency, and capacity while CM1–CM4 prevent unsafe or operationally hollow success.

The contradictory simpler-API fallback identified under Decision-readiness keeps this dimension from being strong; apart from that isolated clause, prioritization and scope logic consistently serve the thesis.

### Findings

- None beyond the high-severity fallback conflict recorded under Decision-readiness.

## Done-ness clarity — adequate

Most FRs carry observable consequences, and the contract/quality gates plus NFRs provide unusually concrete verification expectations. Exact query bounds, performance percentiles, capacity units, retention periods, lifecycle states, idempotency behavior, reconciliation budget, isolation coverage, and accessibility target make substantial portions directly testable.

Two residual classes of ambiguity still matter for acceptance: some binding behavior is intentionally unresolved, and a few NFRs retain qualitative pass conditions.

### Findings

- **medium** Four binding behavior decisions are still open (§ Deferred Quantitative Targets — Architecture Exit Criteria; § Open Release Items OQ1–OQ4) — C7 timing, exact file-policy allow/reject behavior, the authorization-matrix denominator, and the supported-provider compatibility catalog are product-visible acceptance inputs, not merely missing test evidence. The PRD honestly blocks release on them, but engineers cannot yet close the affected lock/revocation, FR32–FR35, authorization, or provider-readiness behavior. *Fix:* Approve and version OQ1–OQ4, then add the binding outcomes or precise versioned references beside every affected FR/NFR before treating those stories as acceptance-complete.
- **medium** Several NFR pass conditions remain qualitative (§ Non-Functional Requirements → Reliability, Idempotency, and Failure Visibility; Scalability and Capacity; Operations Console Accessibility) — “remain queryable as folder history grows,” “large batches ... without making routine ... queries unusable,” “common browser zoom levels,” and provider timeout/retry/backoff requirements do not specify the history size, batch size, zoom range, or budgets that distinguish pass from fail. *Fix:* Add bounded release-calibration workloads and explicit provider/console thresholds, or point each clause to a named, approved calibration artifact with those values.

## Scope honesty — strong

Scope is unusually candid. The PRD explicitly records the repository-backed narrowing from the Product Brief, names extensive MVP non-goals, separates live-workspace body search from metadata-token indexing, identifies future pressure points, and states that every open release item must close. Ten open items are proportionate to a high-stakes brownfield contract because each is classified as a bounded decision or implementation-evidence gap and the document does not pretend the implementation is ready.

### Findings

- None.

## Downstream usability — adequate

The Glossary, stable state vocabulary, named UJs, contiguous FR/UJ/SM/CM/OQ identifiers, Contract Spine authority, and explicit cross-references give architecture and story workflows a solid extraction base. The PRD is weakest when a downstream workflow extracts an individual short FR or must choose among repeated normative descriptions.

### Findings

- **medium** Some FRs are capability labels rather than standalone test contracts (§ FR11, FR12, FR18, FR20, FR47–FR50) — Statements such as “can create logical folders,” “can inspect folder lifecycle,” or “can use the versioned REST transport” do not themselves name the accepted result, decisive denial cases, or a specific acceptance gate. Their testability emerges only after reading distant scope, API, authorization, and quality-gate prose. *Fix:* Add the minimum success consequence and critical denial/result shape to each thin FR, or attach a precise canonical gate/contract reference that remains meaningful when the FR block is source-extracted.
- **medium** Normative behavior is repeated across too many PRD layers (§ MVP Contract Summary; § API Backend Specific Requirements; § Project Scoping & Phased Development; § Functional Requirements; § Non-Functional Requirements) — Lifecycle states, idempotency, search boundaries, surface parity, and failure semantics are restated several times. The PRD defines authority between itself and the Contract Spine but not precedence among its own repeated clauses, so later edits can create internal drift even when IDs stay stable. *Fix:* Designate one canonical PRD subsection per behavior family and turn summaries, journeys, and scoping repetitions into concise references plus user-value context.

## Shape fit — strong

The capability-spec shape fits a high-complexity, brownfield API platform that feeds architecture, contracts, UX, testing, and stories. Named journeys are load-bearing because they distinguish tenant administrators, developers, agents, scoped operators, integrators, and auditors across positive and failure paths; the detailed authorization, provider, lifecycle, and evidence sections are proportionate to the safety stakes rather than template ornament.

### Findings

- None.

## Mechanical notes

- FR1–FR58, UJ1–UJ9, SM1–SM8, CM1–CM4, and OQ1–OQ10 are contiguous and unique; cited FR and UJ ranges resolve.
- Every UJ has a named protagonist carrying role context inline.
- No `[ASSUMPTION]` or `[NOTE FOR PM]` tags appear, so there is no Assumptions Index roundtrip to reconcile; unresolved items are instead centralized in OQ1–OQ10.
- Open-item artifact paths that do not yet exist are explicitly presented as evidence to publish, not as completed references. The C-series summary is sparse (C8 and C10–C12 are absent); a one-line note that only PRD-relevant C criteria are listed would prevent readers from mistaking deliberate omission for ID loss.
- **low** Frontmatter source counts drift from the source list (frontmatter `documentCounts`) — `inputDocuments` includes `_bmad-output/project-context.md`, while `documentCounts.projectContext` remains `0`. *Fix:* Set the count to `1`, or document why that listed input is excluded from the count.
