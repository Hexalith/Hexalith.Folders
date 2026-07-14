# Validation Report — Hexalith.Folders

- **PRD:** _bmad-output/planning-artifacts/prd.md
- **Rubric:** .agents/skills/bmad-prd/assets/prd-validation-checklist.md
- **Run at:** 2026-07-14T22:29:30+02:00
- **Grade:** Fair

## Overall verdict

This is a substantive, unusually product-specific platform PRD: it has a clear thesis, an explicit repository-backed MVP cut, credible stakeholder journeys, a sharp content-exclusion boundary, and strong product-specific NFR and verification material. It is not currently a reliable build/release source of truth, however, because approved decisions and implementation-era scope changes have not been reconciled into it; four dimensions are therefore thin, with the greatest risks in decision-readiness, done-ness, scope honesty, and downstream usability.

The adversarial review materially reinforces that conclusion. Beyond the rubric findings, it exposes unresolved authority boundaries, asynchronous authorization revocation, repository/ref locking identity, remote commit durability, privileged operator access, and error-detail leakage risks. The PRD is strong as a product thesis but is not ready to serve as the authoritative chain-top contract until these conflicts are reconciled.

## Dimension verdicts

- Decision-readiness — thin
- Substance over theater — strong
- Strategic coherence — adequate
- Done-ness clarity — thin
- Scope honesty — thin
- Downstream usability — thin
- Shape fit — adequate

## Findings by severity

### Critical (0)

None.

### High (26)

**[Decision-readiness / Rubric]** — Approved C3 and C4 decisions remain marked TBD (§ Deferred Quantitative Targets — Architecture Exit Criteria)

The PRD still delegates approved retention and bounded-input policies to architecture review, leaving teams with an obsolete decision state.

Fix: Replace both TBD rows with the approved values, dates, and canonical artifact links, and update related NFR references.

**[Decision-readiness / Rubric]** — MVP webhook acceptance contradicts the current MVP boundary (§ MVP Acceptance Evidence)

The PRD requires replay-safe webhook evidence while architecture explicitly excludes webhook ingestion from MVP.

Fix: Decide the product posture once; if no-webhook is the MVP cut, move webhook ingestion and duplicate-delivery evidence to Post-MVP.

**[Decision-readiness / Rubric]** — Projection-only console language excludes the approved incident exception (§ Public Surfaces; § Observability, Auditability, and Replay)

Absolute projection-only wording conflicts with the ACL-checked incident-mode event-stream view.

Fix: Add a narrowly bounded degraded-mode exception with authorization, redaction, audit, warning, and read-only requirements.

**[Strategic coherence / Rubric]** — Success measures do not support a product go/no-go decision (§ Success Criteria)

Adoption and task-completion outcomes lack targets, populations, windows, baselines, data sources, and counter-metrics.

Fix: Give strategic outcomes stable IDs and measurable definitions, including counter-metrics for failure, recovery burden, latency, and integration cost.

**[Done-ness clarity / Rubric]** — Many FRs are capability labels rather than done conditions (§ Functional Requirements)

Requirements such as FR13, FR14, FR27, FR31, and FR46 omit testable consequences or stable acceptance references.

Fix: Add at least one verifiable consequence or named acceptance reference to every FR, prioritizing lifecycle, authorization, locking, queries, archive, and failures.

**[Done-ness clarity / Rubric]** — Idempotency scope is internally incomplete (§ Command and Query Contract; § Reliability, Idempotency, and Failure Visibility)

The operation table, journeys, and blanket mutation rule cover different command sets.

Fix: Create one normative matrix for every mutating MVP operation, including replay, conflict, unknown outcome, and retention behavior.

**[Scope honesty / Rubric]** — FR58 has two incompatible product meanings (§ Authorized Search Facade)

“Search the content” is presented as satisfied by metadata-token recall even though full body-text recall is separately gated.

Fix: Split metadata-token search from full-content search and assign each a precise acceptance boundary and release phase.

**[Downstream usability / Rubric]** — FR58 does not round-trip into downstream coverage (§ Authorized Search Facade)

FR58 is current in the PRD but architecture coverage still declares FR1–FR57 and C9 is undefined in the PRD.

Fix: Reconcile PRD, architecture, epics, tests, and release gates against one approved FR58 meaning.

**[Adversarial review]** — C3 and C4 remain falsely open

Binding retention and bounded-input policies are approved downstream, contradicting the PRD’s TBD state and its own update requirement.

Fix: Reconcile the PRD with the approved governance artifacts and make those artifacts the named source of quantitative truth.

**[Adversarial review]** — Webhook acceptance creates a forbidden MVP ingress surface

The PRD does not distinguish inbound provider webhooks, outbound callbacks, and asynchronous worker retries.

Fix: Define each mechanism and explicitly state which are in MVP, deferred, or forbidden.

**[Adversarial review]** — The console has two incompatible source-of-truth rules

Projection-only requirements conflict with the event-stream incident view and leave its product permissions and safety behavior undefined.

Fix: Either remove the incident surface or specify its actor, scope, redaction, audit, degraded-state warning, and acceptance evidence.

**[Adversarial review]** — Contract authority is internally unstable

The PRD alternates among REST, SDK, and OpenAPI authority without a tie-breaker for schema, transport, generated client, and adapter semantics.

Fix: Define the authority hierarchy and versioning/parity obligations in product terms.

**[Adversarial review]** — Minimum SDK, CLI, and MCP scope is not decidable

The SDK is “MVP or near-MVP,” CLI and MCP are must-have surfaces, yet resource fallback permits them to slip.

Fix: Name the minimum release surface set and the evidence required for every included adapter.

**[Adversarial review]** — Existing-repository binding and brownfield exclusion conflict

The central MVP flow binds repositories, while brownfield adoption is post-MVP and “where supported” does not define the allowed repository state.

Fix: Bound MVP repository eligibility, history/default-branch behavior, and whether only Folders-created repositories are supported.

**[Adversarial review]** — Tenant-administration capabilities are disconnected from MVP acceptance

Journeys and FR4–FR14 require grant/revoke, effective permissions, archive, and retained evidence, but the MVP feature and evidence sections omit them.

Fix: Either include these capabilities in MVP scope and acceptance or phase them explicitly; add revoke and archived-folder denial behavior.

**[Adversarial review]** — FR58’s headline is semantically false for the current implementation boundary

Metadata-token recall is materially different from content search and could pass an acceptance test that users would reasonably interpret otherwise.

Fix: Rename and redefine the MVP capability around searchable authorized metadata; reserve “content search” for body-content behavior.

**[Adversarial review]** — FR58 is not integrated into the product case

It lacks a journey, success measure, MVP feature, acceptance scenario, error/freshness behavior, and clear phase, while embedding delivery-ledger details.

Fix: Add product-level behavior and traceability, and move story status and wiring evidence to implementation artifacts.

**[Adversarial review]** — Workspace state conflates independent dimensions

Lock ownership, working-copy condition, availability, and lifecycle outcome are mixed with multiple incompatible state lists.

Fix: Define orthogonal state dimensions, composition rules, precedence, and one cross-surface vocabulary.

**[Adversarial review]** — Authorization revocation during asynchronous work is undefined

The PRD does not say what happens when membership, ACL, delegated authority, provider binding, or credential permission changes after prepare or lock.

Fix: Require freshness/revalidation checkpoints, fail-closed behavior, and an explicit terminal/reconciliation outcome.

**[Adversarial review]** — Platform-operator cross-tenant authority is missing

Operator journeys expose tenant, credential-reference, provider, lock, failure, and audit metadata without defining privileged role scope, consent, redaction, or audit.

Fix: Define operator authority, need-to-know constraints, tenant boundaries, visible fields, and privileged-read audit requirements.

**[Adversarial review]** — Lock collision identity is unspecified

The current lock phrase may allow aliases, folders, or branches to mutate the same underlying repository/ref concurrently.

Fix: Define canonical repository/ref identity, alias detection, and the precise serialization key.

**[Adversarial review]** — “Commit” does not establish remote durability

A local SHA and clean working tree could satisfy the written demo without proving provider-backed persistence after cleanup.

Fix: Define local commit, push/sync, remote confirmation, and durability acceptance outcomes.

**[Adversarial review]** — Error details are an unconstrained leakage surface

The mandatory details field lacks a safe-field schema, size bound, per-code visibility rule, and unauthorized/nonexistent equivalence rule.

Fix: Define a metadata-only details schema per error category with bounds and non-enumeration invariants.

**[Adversarial review]** — “100%” quality gates have no stable denominator

DTO and operation inventories are partial and omit administration, archive, effective permission, search, and diagnostics.

Fix: Reference a canonical, versioned inventory for parity, ACL, and command/query coverage calculations.

**[Adversarial review]** — File policy outcomes remain product-ambiguous

Traversal, symlink, normalization, collision, encoding, binary, large-file, and filtering behavior are required but not normatively resolved.

Fix: State user-visible outcomes or reference a canonical approved policy artifact that all surfaces must follow.

**[Adversarial review]** — Sensitive metadata defaults remain open

Paths, commit messages, repository/branch names, and provider diagnostics are protected only “where appropriate,” despite downstream C9 classification.

Fix: Bring the approved default classification, override behavior, and permitted views into the product contract or a canonical linked decision.

### Medium (8)

**[Decision-readiness / Rubric]** — Unresolved decisions are buried rather than registered (§ Data Schemas; § Workspace State and Concurrency; § Architecture Decisions Needed Next)

File transport, lock defaults, batch atomicity, and related choices have no open-item owner or deadline.

Fix: Reconcile resolved decisions and register genuinely open product decisions with owner, revisit condition, and blocked behavior.

**[Downstream usability / Rubric]** — Core domain vocabulary has no glossary (§ Capability Contract Terms; § Workspace State and Concurrency)

Load-bearing nouns and state terms drift in casing and granularity.

Fix: Add a concise glossary and a normative state-vocabulary table.

**[Downstream usability / Rubric]** — Journeys and success measures lack stable IDs (§ Success Criteria; § User Journeys)

Only FRs have stable IDs, weakening extraction and cross-reference quality.

Fix: Assign UJ1–UJ9 and SM1–SMn identifiers and use minimal cross-references.

**[Shape fit / Rubric]** — Historical classification and live delivery status are mixed (§ Project Classification; § Authorized Search Facade)

The document combines a greenfield inception label with active story and release-readiness status.

Fix: Declare the current posture and move delivery status to sprint/implementation artifacts.

**[Adversarial review]** — The PRD lacks a trustworthy current-baseline marker

May frontmatter coexists with later implementation references, and the input list omits sources that now qualify the contract.

Fix: Refresh status/dates and enumerate the authoritative source set used for the current baseline.

**[Adversarial review]** — Greenfield framing now hides brownfield constraints

The label encourages readers to ignore compatibility, migration, and regression realities of the active implementation.

Fix: Mark greenfield as historical inception context and state the present brownfield/living-contract posture.

**[Adversarial review]** — Success is mostly feature existence

Task completion, adoption, reliability, and operator burden lack targets or counter-metrics.

Fix: Define measurable outcomes, cohorts, windows, and failure/cost counter-metrics.

**[Adversarial review]** — Product decisions lack a durable rationale trail

No memlog or addendum recovers the reasoning behind major scope cuts, lifecycle additions, FR58, or later architecture boundary changes.

Fix: Bootstrap an append-only decision log during the next Update and preserve rejected-alternative rationale in an addendum where appropriate.

### Low (0)

None.

## Mechanical notes

- FR IDs are contiguous and unique from FR1 through FR58.
- Journey headings are contiguous and unique from Journey 1 through Journey 9, but they are not stable UJ IDs; success measures have no IDs.
- C9 is referenced by FR58 but is not defined in the PRD; the exit-criteria table defines only C1–C5.
- No glossary exists for load-bearing distinctions among folder, repository, repository binding, workspace, task, lock, context query, status, and audit.
- State vocabulary drifts across lowercase product states, PascalCase lifecycle states, provider/workspace milestones, and reconciliation states without a mapping.
- There are no inline assumptions, PM notes, Open Questions, or Assumptions Index despite unresolved/deferred wording.
- Frontmatter says greenfield and complete with May dates, while the body contains later implementation-era status.
- No .memlog.md or addendum.md exists in the PRD workspace.

## Reviewer files

- _bmad-output/planning-artifacts/review-rubric.md
- _bmad-output/planning-artifacts/review-adversarial-general.md
