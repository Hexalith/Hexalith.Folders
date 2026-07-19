---
source: sprint-change-proposal-2026-07-07-idempotency-key-canonicalization.md
source_date: 2026-07-07
source_status: implemented-and-verified
reconciled_against:
  - prd.md
  - .memlog.md
addendum_present: false
disposition: already-required-no-prd-body-change-contract-evidence-follow-up
---

# Reconciliation — Provider-Readiness Idempotency Rejection Code Canonicalization

## Source purpose and status

This implemented and verified `bmad-correct-course` proposal corrects two provider-readiness read routes that rejected an `Idempotency-Key` header with the legacy error code `idempotency_key_not_accepted`. The routes and their three endpoint-test assertions now use the canonical sibling-route value `idempotency_key_not_allowed`; the Epic 8 retrospective action item was marked done, and historical delivery records were intentionally left unchanged.

The source classifies the change as minor and direct. It reports targeted and full server-test lanes green, a warnings-as-errors build clean, and no remaining legacy occurrences in production or test source. No commit was made in that session.

## PRD-relevant product decisions

1. **Read operations reject idempotency keys.** Provider readiness and provider-support evidence are read-only operations and must reject an `Idempotency-Key` rather than silently accept or interpret it.
2. **The rejection must be canonical and cross-surface consistent.** Equivalent read operations should expose one stable error identity, not provider-readiness-specific aliases.
3. **Error `code` is consumer-visible contract data.** Even when HTTP status, category, retryability, and high-level behavior remain unchanged, changing the emitted `code` corrects a public wire-contract value.
4. **Tests can preserve drift.** A green endpoint test that asserts a legacy alias is evidence of implementation consistency, not evidence that the asserted value is canonical.
5. **Historical evidence should remain historical.** Past stories, retrospectives, and test summaries can retain the old value when they explain the discovered drift and its resolution; current source and authoritative contract evidence must use the canonical value.
6. **A deliberate exception is rejected.** There is no product reason for provider-readiness reads to use a different idempotency-key rejection code from sibling reads.

## Already covered in the current PRD

### Exact identifiers and sections

- **FR3** requires every Contract Spine operation to declare mutation/read classification; mutations follow the idempotency contract and reads reject idempotency keys.
- **FR42** explicitly requires non-mutating operations to reject idempotency keys.
- **FR43** requires every supported surface to expose the Contract Spine error taxonomy with `category`, `code`, safe message, correlation ID, retryability, client action, and closed metadata-only visibility.
- **FR44** requires a stable machine-readable error taxonomy that distinguishes validation and idempotency failures.
- **FR47–FR51** require REST/SDK/CLI/MCP contract equivalence for operation identity, errors, authorization, status, and audit behavior.
- **API Backend Specific Requirements → Rate Limits, Throttling, and Idempotency** already states that provider-readiness and other read-only operations reject idempotency keys.
- **API Backend Specific Requirements → Error Codes** requires stable, machine-readable, cross-surface error mapping and closed bounded details.
- **API Backend Specific Requirements → Contract and Quality Gates** requires golden schema/error-mapping tests and complete idempotency coverage.
- **Non-Functional Requirements → Reliability, Idempotency, and Failure Visibility** repeats that every mutation requires a key and every non-mutation rejects one.
- **Non-Functional Requirements → Integration and Contract Compatibility** requires shared/generated contract tests across REST, CLI, MCP, and SDK.
- **OQ8** specifically blocks idempotency completeness until architecture, Contract Spine, SDK, and C13 evidence prove the all-mutations rule and read-key rejection for every cell.

### Memlog alignment

- The memlog records the change making idempotency normative for every mutation and prohibiting keys on reads.
- It records OQ8 as the open architecture/contract evidence gate until every mutation and read cell passes the rule.
- It records the Contract Spine as machine-contract authority and generated/adapted surfaces as subordinate. Canonical wire-code ownership therefore belongs in the Contract Spine/error evidence, not a route-local convention alone.
- No memlog decision endorses `idempotency_key_not_accepted`; the implemented correction conflicts with no prior product decision.

## Genuine PRD gaps

There is **no product-body gap**. The PRD already requires read-key rejection, stable error codes, cross-surface error semantics, and an OQ8 closure gate. Adding a new FR for the exact literal `idempotency_key_not_allowed` would duplicate a Contract Spine-owned wire value and weaken the PRD/Contract Spine authority boundary.

There is, however, a **downstream contract-evidence gap exposed by the source**: the proposal says the transport-parity oracle checks only `category`, and no Contract Spine/parity fixture pinned either the legacy or canonical `code`. Route tests now assert the corrected literal, but they do not by themselves prove every C13-supported surface/cell emits the canonical code. OQ8 and the golden error-mapping gate should close this gap by pinning the canonical read-key rejection outcome in authoritative contract evidence.

The proposal is absent from PRD `inputDocuments` and has no source-specific memlog reconciliation entry. If the July update enumerates all supplied sprint-change inputs, record it as an implemented contract-consistency source with no PRD-body edit.

## Conflicts and supersession

### “No public wire contract change” is too strong

The source says public wire behavior did not change, but it also states that two consumer-visible ProblemDetails `code` values changed. Status `400`, category `validation_error`, retryability, and rejection semantics stayed the same; nevertheless, the literal wire value changed from a legacy alias to the canonical value.

Disposition: describe this accurately as a **wire-contract conformance correction with unchanged behavior/category/status**, not as no wire change at all. Because the target value already governs sibling reads and no authoritative artifact depended on the legacy alias, no PRD scope/version change is indicated.

### “Residual: none” overlooks the authoritative-evidence weakness

Production and route tests are corrected, but the source itself says parity/contract evidence would not have caught the drift because it checked category only and did not pin the `code`. That leaves a recurrence risk until the canonical outcome is covered through Contract Spine/golden/C13 evidence.

Disposition: keep the implementation action item done, but route the evidence follow-up through existing OQ8 and Contract and Quality Gates rather than reopening product scope.

### Historical evidence is correctly preserved

Old delivery records mentioning `idempotency_key_not_accepted` are not current requirements and should not be mechanically rewritten. The current PRD and memlog supersede them for product/contract intent.

## Implementation and tracking detail that stays out of the PRD

- The two endpoint method/file locations and five literal source/test edits.
- The route-local canonical comments and exact line numbers.
- The three endpoint-test method names and serialized JSON assertion strings.
- The `grep` result, 34-test targeted lane, 561-test full lane, and warnings-as-errors build counts.
- The exact list of sibling endpoint files using the canonical string.
- Story 8.1 DD1 lineage, Epic 5/8 retrospective tracking, sprint-status edit, and maintainer commit discipline.
- The current `TransportParityConformanceTests` assertion implementation and its category-only oracle behavior.

No `addendum.md` exists. The exact literal and enforcement mechanism belong in the Contract Spine, error catalog, tests, and implementation history; the PRD should retain the already-present capability and consistency requirements.

## Recommended stable-ID edits and additions

- **New FRs:** none.
- **FR edits:** none.
- **Renumbering:** none.
- **Downstream traceability:** cite FR3, FR42–FR44, FR47–FR51, Rate Limits/Throttling/Idempotency, Error Codes, Contract and Quality Gates, Reliability/Idempotency/Failure Visibility, Integration/Contract Compatibility, and OQ8.
- **Contract evidence:** pin the canonical read-key rejection code through authoritative Contract Spine/golden/C13 evidence under OQ8; do not place the literal in a new PRD requirement.
- **Metadata:** optionally list the implemented proposal as a reconciled input and log that it confirms existing requirements without changing stable IDs.

## Qualitative ideas at risk

- Consistency includes the exact machine-readable error identity, not only HTTP status and broad category.
- Passing tests can lock in drift when they derive expected values from the implementation rather than the canonical contract.
- Provider-readiness reads do not earn a special alias; one rule and one canonical outcome should apply to every read cell.
- Historical records should explain past divergence without being rewritten as though it never occurred.
- Route-local tests are necessary but not sufficient when cross-surface/Contract Spine evidence owns the release claim.
- This was a small implementation correction that should strengthen, not expand, the product contract.

## Concise disposition

**No PRD body or stable-ID change.** The implementation now matches FR3/FR42 read-key rejection and the stable error-contract intent. Preserve historical evidence, correct the proposal's “no wire change” shorthand, and use existing OQ8 plus golden Contract Spine/C13 evidence to ensure the exact canonical `code` cannot drift again.
