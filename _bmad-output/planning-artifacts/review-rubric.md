# PRD Quality Review — Hexalith.Folders

## Overall verdict

This is a substantive, unusually product-specific platform PRD: it has a clear thesis, an explicit repository-backed MVP cut, credible stakeholder journeys, a sharp content-exclusion boundary, and strong product-specific NFR and verification material. It is not currently a reliable build/release source of truth, however, because approved decisions and implementation-era scope changes have not been reconciled into it; four dimensions are therefore thin, with the greatest risks in decision-readiness, done-ness, scope honesty, and downstream usability.

## Decision-readiness — thin

The PRD makes real decisions rather than merely listing considerations. The repository-backed-first cut is explicit (§ Product Scope, lines 127–139), the Product Brief departure is acknowledged with rationale (line 133), the operations console is intentionally diagnostic-only (lines 135 and 564–577), and the fallback order under resource pressure preserves the core contract and safety properties (lines 585–591). Those are useful, defensible trade-offs.

The document is nevertheless stale at decisions that now govern release behavior. It still presents approved C3/C4 policies as unresolved, simultaneously includes webhook acceptance evidence and a no-webhook MVP architecture, and states an absolute projection-only console boundary that the approved incident-mode design deliberately qualifies. Several other explicitly unresolved items remain buried as prose even though the PRD claims completion and contains no Open Questions or `[NOTE FOR PM]` callouts.

### Findings

- **[high]** Approved C3 and C4 decisions remain marked TBD (§ Deferred Quantitative Targets — Architecture Exit Criteria, lines 504–516) — The rows say “TBD by architecture review” and line 516 requires a PRD update, but `docs/exit-criteria/c3-retention.md` and `docs/exit-criteria/c4-input-limits.md` now record approved, binding values. Teams using the PRD alone will implement or validate against an obsolete decision state. *Fix:* Replace both TBD rows with the approved values, approval dates, and canonical artifact links; update the associated retention and query-bound NFRs to point to them.
- **[high]** MVP webhook acceptance contradicts the current MVP boundary (§ MVP Acceptance Evidence, line 561) — “Provider callbacks and webhooks are tenant-bound, replay-safe, and covered by duplicate-delivery tests” makes webhook ingestion an MVP acceptance obligation, while the architecture explicitly defines no webhook ingestion in MVP. This changes scope and release tests, not merely mechanics. *Fix:* Decide the product posture and state it once: if no-webhook is the accepted MVP cut, move webhook ingestion and duplicate-delivery acceptance to Post-MVP and retain only tenant-scope requirements for any non-webhook provider interaction.
- **[high]** The console source-of-truth boundary is absolute where the approved product behavior has an incident exception (§ Public Surfaces, line 330; § Observability, Auditability, and Replay, line 759) — “must consume projections only” and “must be read-model–based” exclude the ACL-checked incident-mode event-stream view now specified for projection outages. Operators and security reviewers cannot tell whether that view is allowed product behavior. *Fix:* Add a narrowly bounded degraded-mode exception with authorization, metadata-only/redaction, audit, UX-warning, and read-only requirements; keep projections as the normal path.
- **[medium]** Material unresolved decisions are not surfaced as open decisions (§ Data Schemas, line 397; § Workspace State and Concurrency, lines 431–437; § Architecture Decisions Needed Next, lines 500–502) — File transport, lock defaults, batch atomicity, and related choices are described with “must define” or “should define,” yet the completed PRD has no Open Questions, owners, deadlines, or `[NOTE FOR PM]` callouts. *Fix:* Reconcile decisions already made downstream; for anything genuinely unresolved, add an explicit open-item register with decision owner, deadline/revisit condition, and the product behavior blocked by it.

## Substance over theater — strong

The Vision and differentiation are specific to managed agent workspaces, not category-generic language: the PRD bets on narrow task-lifecycle primitives replacing direct filesystem, Git, credential, and recovery ownership (§ Executive Summary and What Makes This Special, lines 52–66). The nine journeys carry concrete protagonists or operational roles, end states, failure modes, and revealed requirements; even the role-only journeys concern concurrency, parity, and audit reconstruction rather than decorative personas.

The NFRs are likewise earned. They name concrete latency budgets, zero-tolerance isolation behavior, idempotency outcomes, provider failure categories, redaction surfaces, accessibility targets, and verification gates (§ Non-Functional Requirements, lines 690–787). The Innovation section avoids claiming external novelty and honestly frames the comparison as an internal Hexalith operating pattern (lines 282–286). No substantive theater finding is warranted.

## Strategic coherence — adequate

The PRD has a strong thesis and a coherent platform-MVP arc. The core bet—one tenant-scoped canonical lifecycle for repository-backed agent file work—appears consistently in the Executive Summary, MVP Contract Summary, scope cut, journeys, and feature set. Priority under resource pressure also follows that thesis rather than ease of implementation (§ Risk Mitigation Strategy, lines 585–591).

Success Criteria do not yet provide an equally strong test of the thesis. Most outcomes prove feature presence or a demo flow, while adoption and task completion lack target values, cohorts, time windows, baselines, or counter-metrics. As written, the team could ship all named surfaces and declare success without proving that chatbot teams actually stopped building bespoke orchestration or that the control plane improved safe task completion at acceptable operational cost.

### Findings

- **[high]** Success measures do not support a product go/no-go decision (§ Success Criteria, lines 88–123) — “Task completion rate is tracked” has no target or denominator; twelve-month success is “adoption” and becoming “the default” without a measurable cohort; several measurable outcomes are binary feature-existence checks. No counter-metrics constrain latency, operator burden, failed/dirty-workspace accumulation, or integration cost. *Fix:* Give each strategic outcome an ID, population, baseline, target, measurement window, and data source; add counter-metrics that would reveal a hollow win, such as reduced bespoke integrations accompanied by excessive task failure, recovery burden, or integration time.

## Done-ness clarity — thin

The PRD contains more acceptance material than the FR list alone suggests: operation-specific idempotency outcomes, canonical error fields/categories, explicit quality gates, security negatives, latency thresholds, and a detailed MVP acceptance-evidence section provide valuable test anchors (§ Command and Query Contract through § Contract and Quality Gates, lines 347–488; § MVP Acceptance Evidence, lines 553–562).

However, the globally numbered FR inventory—the artifact downstream story creation will most often extract—mostly states capabilities as “can” without a testable consequence or resolvable acceptance reference. Several critical behaviors are defined more precisely elsewhere, but the FR does not point to that definition. Operation-level idempotency coverage also disagrees across the PRD, leaving implementers to decide which mutating commands need stable duplicate behavior.

### Findings

- **[high]** Many FRs are capability labels rather than done conditions (§ Functional Requirements, lines 593–688) — Examples include “archive folders when policy allows” (FR13), “preserve audit and status evidence” (FR14), “deny competing operations when … unsafe” (FR27), “inspect whether … current, delayed, failed, or unavailable” (FR31), and “explain final state” (FR46). The relevant policy, preservation period, state threshold, denial outcome, or mandatory fields are not carried by the FR or a stable acceptance cross-reference. *Fix:* Add at least one verifiable consequence or named acceptance reference to every FR, prioritizing lifecycle, authorization, lock, context-query, archive, and failure-state requirements; keep implementation mechanics outside the FRs.
- **[high]** Idempotency scope is internally incomplete (§ Command and Query Contract, lines 367–379; § Reliability, Idempotency, and Failure Visibility, lines 705–716) — The PRD says mutating commands require idempotency and later includes cleanup, but the operation table omits folder creation, repository binding, lock release, archive/access-control mutation, and cleanup; the Journey Summary names a different subset again (line 254). This leaves duplicate side-effect behavior undefined for multiple MVP mutations. *Fix:* Create one normative operation matrix covering every mutating MVP command, with key scope, payload-equivalence rule, replay result, conflicting-payload result, unknown-outcome behavior, and retention tier.

## Scope honesty — thin

The original MVP cut is honest and well explained. The PRD explicitly excludes local-only mode, repair automation, brownfield adoption, simultaneous writers, console file browsing/editing, and a broad provider framework (§ Explicit MVP Non-Goals, lines 564–577). It also preserves a clear distinction between the control plane and content/Git/identity/sandbox responsibilities (lines 306–320).

Later edits have blurred that honesty. FR58 mixes a current product requirement, a partial metadata-token implementation, an undefined approval gate, a future full-content capability, and live Epic status. Combined with the webhook conflict and silent resolved decisions above, the PRD no longer cleanly tells a reader which behavior is required for MVP acceptance versus deferred.

### Findings

- **[high]** FR58 has two incompatible product meanings and no clear MVP acceptance boundary (§ Authorized Search Facade, lines 684–688) — The FR promises users can “search the content,” while the scope note says current recall is derived only from mutation metadata and full body-text recall is a gated follow-up. It also references undefined `C9`, a reopened story, live DCP evidence, and server wiring rather than stating product-level done conditions. *Fix:* Split the requirement: define metadata-token search with precise user-visible behavior as MVP only if that is the accepted product value; state full-content search as a separate gated post-MVP requirement/non-goal; define the approval gate in the PRD or link to a canonical decision; move story progress to implementation artifacts.

## Downstream usability — thin

The document is organized into extractable capability groups, and FR1–FR58 and Journey 1–9 are contiguous and unique. The journey narratives carry named protagonists where a human role is central, while role/system journeys appropriately focus on concurrency and contract parity. Detailed error, security, provider, and verification sections give architecture and test-design workflows unusually rich source material.

The PRD lacks the stable vocabulary and identity scheme expected of a chain-top artifact, though, and it has already drifted from downstream artifacts. There is no glossary for its many overlapping domain nouns, success measures have no IDs, journey references are informal headings rather than stable UJ IDs, and `C9` is referenced without definition. Most importantly, FR58 claims to be in the current inventory while the architecture’s own requirements-coverage conclusion still covers only FR1–FR57, showing that source extraction no longer round-trips.

### Findings

- **[high]** The current requirements inventory does not round-trip into downstream coverage (§ Authorized Search Facade, lines 684–688) — FR58 says it is current and implementation-bound, but it depends on undefined C9 and the architecture coverage declaration still maps FR1–FR57. This makes it impossible to know whether architecture, epics, tests, and release gates cover the current PRD. *Fix:* Reconcile PRD, architecture, epics, and test traceability against one approved FR58 meaning; update the downstream coverage inventory and remove implementation-completion claims from the PRD.
- **[medium]** Core domain vocabulary has no glossary and state terms drift in casing and granularity (§ Capability Contract Terms, lines 597–601; § Workspace State and Concurrency, lines 427–439; § Reliability, Idempotency, and Failure Visibility, lines 705–716) — Terms such as logical folder, repository, repository binding, workspace, task, context query, status record, audit record, and cleanup status are load-bearing but not defined. Product-visible states are lowercase in one section and PascalCase lifecycle states appear in another without a declared relationship. *Fix:* Add a concise glossary and one normative state-vocabulary table distinguishing product-visible state, operation state, lock state, and operator disposition; use exact terms and casing throughout.
- **[medium]** Journeys and success measures lack stable downstream IDs (§ Success Criteria, lines 88–123; § User Journeys, lines 151–268) — Numbered headings are readable, but only FRs have explicit stable IDs; most FRs and quality gates cannot cite a stable UJ/SM identity, and only Journey 9 directly references FR IDs. *Fix:* Assign UJ1–UJ9 and SM1–SMn IDs, then add minimal cross-references from capability groups or acceptance evidence without introducing a heavyweight traceability matrix.

## Shape fit — adequate

The overall shape fits a high-stakes, chain-top developer-infrastructure product. A capability-oriented API/backend contract is primary, while journeys are used to expose multi-stakeholder needs—developer, platform engineer, operator, tenant administrator, automation consumer, and audit reviewer—rather than forcing a consumer-product persona template. The document also adapts in provider, security, concurrency, audit, accessibility, and public-contract concerns appropriate to the product.

Its current form has begun to blend PRD and implementation-status artifacts. That does not invalidate the capability-spec shape, but it weakens the artifact as a durable product contract and makes the “greenfield” classification misleading now that stories and implementation evidence are cited as current state.

### Findings

- **[medium]** Historical classification and live delivery status are mixed into a supposedly current product contract (§ Project Classification, lines 68–76; § Authorized Search Facade, line 688) — “Greenfield product” describes the May 2026 creation context, while FR58 discusses reopened Epic 10 work and release-readiness closure. Readers cannot tell whether the PRD is a historical baseline, a living brownfield contract, or a delivery status report. *Fix:* Mark the original greenfield classification as historical and declare the present document posture; keep current product requirements here and move story status, wiring progress, and evidence closure to sprint/implementation artifacts.

## Mechanical notes

- FR IDs are contiguous and unique from FR1 through FR58.
- Journey headings are contiguous and unique from Journey 1 through Journey 9, but they are not expressed as stable `UJ` IDs; Success Metrics have no IDs.
- `C9` is referenced in the FR58 scope note (line 688) but is not defined in the PRD; the only exit-criteria table defines C1–C5.
- No Glossary is present despite load-bearing distinctions among folder, repository, repository binding, workspace, task, lock, context query, status, and audit concepts.
- State vocabulary drifts between lowercase product states (`ready`, `locked`, `dirty`, `committed`, `failed`, `inaccessible`), PascalCase lifecycle states (`Pending`, `InProgress`, `Succeeded`, `Failed`, `Cancelled`), and additional names such as `ProviderReady` and `WorkspacePrepared` without a mapping.
- No inline `[ASSUMPTION]` tags, `[NOTE FOR PM]` callouts, Open Questions, or Assumptions Index are present, despite explicit unresolved/deferred wording.
- Frontmatter still classifies the project as greenfield and records `status: complete`; the body contains later implementation-era status that is not reflected in the document metadata.
