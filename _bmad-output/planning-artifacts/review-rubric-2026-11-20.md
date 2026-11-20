# PRD quality review — Hexalith.Folders, November 20, 2026

## Overall verdict

The revised PRD identifies what the audit actually tested, why release is blocked, and what evidence would justify the first live-provider claim. The review found conflicts in the C6 lifecycle guidance and the treatment of injected failures. The final edit corrected C6 and assigned the scoring decision to OQ10. The findings below describe the reviewed draft.

## Decision-readiness — adequate

Current Delivery Posture assigns the disposable provider choice to Jerome and the provider maintainer. It distinguishes the published packages, the `V1Only` production control and operational entry. The historical approval table bounds the September 22 decision to the reviewed artifacts. [Story 1.17 owner intake](../implementation-artifacts/story-1-17-closure-owner-intake-2026-11-16.md) · [September 22 decision result](relock-section9-result-2026-09-22-post-a8-corrective.yaml)

- **High.** The C6 row in *Deferred Quantitative Targets* says the transition matrix rejects retryable failure to `dirty` and is approved “for the state vocabularies only.” The governing mapping explicitly requires retryable failure with no confirmed remote effect to preserve staged work in `dirty`. Refer the row to the A7/A7b target and give Story 4.22 responsibility for runtime evidence. [C6 mapping](../../docs/exit-criteria/c6-transition-matrix-mapping.md)

## Substance over theater — strong

The audit paragraph names revision `5f39a1b8` and the substituted commit executor. The evidence table directs a reviewer to interrupt after provider acceptance, replay, then observe the actual ref, bytes, commit count and terminal task state. A passing test with a synthetic executor cannot satisfy that check. [Repository audit](sprint-change-proposal-2026-11-20.md)

## Strategic coherence — strong

The durable repository round trip drives the MVP, SM1 and the first provider slice. CM1 rejects an unsafe or unconfirmed success. The FR58 boundary limits cross-workspace search to authorized metadata while preserving live-workspace context queries. The November workload-identity fixture has its own test purpose and conveys no production enrollment claim. [November 13 proposal](sprint-change-proposal-2026-11-13.md)

## Done-ness clarity — adequate

The outcome table adds observable acceptance conditions for repository writes, index removals, audit projection rebuild and readiness recovery. The caller must still see both providers through all four qualifying surfaces for SM1. The hosted failures belong to the audited revision and do not establish the result of a newer build.

- **High.** *Measurable Outcomes* requires SM6 to cover “100% of injected lifecycle failures.” Its population paragraph excludes deliberately injected failures from both numerator and denominator for SM6 and SM1. That instruction leaves SM6 unscorable and can omit SM1's required failure paths. Count planned negative cases for SM1 and injected cases for SM6. Reserve routine-cohort exclusions for SM3, CM2 and CM3, or resolve an alternative population through OQ10 before scoring.
- **Medium.** The OQ12 row assigns a multi-replica convergence bound for NFR80, while the delivery posture assigns NFR79–NFR81 to OQ11. Have OQ11 own the bound and its proof. OQ12 can reference that evidence when exercising the deployment profile.

## Scope honesty — strong

OQ11 makes one restart-safe provider slice a foundation. Live writes need qualification for each provider. Product selection of the first disposable fixture, current host evidence and a recovery drill each remain open. The scope paragraph and the source inventory distinguish declared McpCli operations from runtime authorization, parity and legacy retirement. [McpCli decision](sprint-change-proposal-2026-11-10.md)

## Downstream usability — adequate

The glossary, error fields and stable IDs support extraction into stories. Two local references would help teams reading only one section.

- **Medium.** The pinned NFR under *Integration and Contract Compatibility* still says “CLI and MCP wrap it.” The banner and *SDK Requirements* correctly describe McpCli's Gateway dispatch. Place a cross-reference adjacent to the pinned section so an excerpt carries the approved interpretation. Preserve the requirement bytes.
- **Low.** The Open Release Items header and future OQ rows still name “accountable approvers.” The preamble assigns material decisions to Jerome and describes the roles as evidence owners. Carry that terminology into the table so an extracted row points to the correct owner. [Project decision policy](../../docs/governance/approval-policy.md)

## Shape fit — strong

Nine named journeys cover developer, agent, administrator, operator and reviewer actions. UJ3 and UJ8 give the interrupted task and metadata-only investigation distinct acceptance consequences. The evidence table is short enough for a release decision, with the implementation work, dependencies and detailed diagnostics in the audit proposal.

## Mechanical notes

The 58 FR bullets match the canonical epic inventory. All 84 NFR bullets match the pre-update PRD. UJ1–UJ9, SM1–SM8, CM1–CM5 and OQ1–OQ13 are each contiguous. The assumptions index preserves withdrawn A3 and A4. The release blockers in this review concern evidence and decisions. No deployment was exercised during this document review.

## Final disposition

The PRD now cites the approved C6 transition model, assigns the NFR80 convergence evidence to OQ11, explains McpCli at the pinned CLI/MCP requirement, and identifies evidence owners in the release table. OQ10 records the remaining calibration decision. Jerome and the Test owner must settle the SM1 and SM6 denominators before scoring either metric.
