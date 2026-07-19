---
source: sprint-change-proposal-2026-07-07-193110.md
source_date: 2026-07-07
source_status: pending-approval
reconciled_against:
  - prd.md
  - .memlog.md
addendum_present: false
disposition: no-prd-body-change-implementation-governance-only
---

# Reconciliation — Sprint Change Proposal 2026-07-07 19:31:10

## Source purpose and status

This `bmad-correct-course` proposal addresses false-green acceptance/parity evidence caused by in-process `IEventStoreGatewayClient` test doubles that always return success and discard `DomainServiceWireResult` rejection identity. It proposes extending Epic 11 Story 11.7 to consolidate gateway doubles into `Hexalith.Folders.Testing`, add an allowlist source-scan guard, and add a positive behavioral test proving canonical `FolderResultCode` propagation through `EventStoreGatewayException`.

The source does not carry approved frontmatter. Its detailed changes are labeled “applied on approval,” and its checklist says final approval, handoff, and status update are still in progress. Its correct source status is therefore **pending approval**, not approved or delivered.

The proposal classifies itself as a moderate backlog reorganization with test-infrastructure-only impact and no product or public wire behavior change.

## PRD-relevant product decisions

1. **False-green contract evidence is not acceptable evidence.** A test path used to claim acceptance or parity must preserve the canonical rejection outcome and reason code rather than manufacturing success.
2. **Canonical rejection behavior remains product behavior.** Safe denial, stable reason codes, HTTP/error mapping, and cross-surface equivalence must remain observable through the real gateway path.
3. **The change is wire-preserving.** Consolidating test doubles and adding conformance guards must not change REST, OpenAPI, SDK, CLI, MCP, lifecycle, audit, or production behavior.
4. **Negative-path doubles are legitimate when their purpose is explicit.** Infrastructure-failure and never-called doubles need not propagate domain rejections, but they must not be used as acceptance/parity evidence.
5. **Evidence must trace to the canonical path.** Request-recording doubles can prove request shape; they cannot independently prove end-to-end acceptance, authorization, or rejection propagation.

These are verification implications of existing requirements, not new user capabilities.

## Already covered in the current PRD

### Exact identifiers and sections

- **CM4 — Surface drift** requires zero material authorization, state, error, or audit divergence for the same scenario.
- **FR9–FR10** require safe authorization denial before protected information is exposed and metadata-only authorization evidence for both allowed and denied operations.
- **FR27** requires deterministic lock denial with no business side effect while still emitting one metadata-only denial audit record.
- **FR40** requires stable status and audit evidence for failed, incomplete, duplicate, retried, and conflicting operations.
- **FR43–FR44** define the canonical cross-surface error fields, categories, codes, retryability, and client action, including tenant/folder denial, conflict, and infrastructure failure.
- **FR46** requires callers to receive the resulting lifecycle/lock state, safe cause category, retry eligibility, client action, correlation ID, and available evidence after provider, authorization, or read-model failure.
- **FR47–FR51** require REST/SDK/CLI/MCP operation, status, error, authorization, audit, and provider-capability equivalence.
- **API Backend Specific Requirements → Contract and Quality Gates** already requires canonical authorization coverage, golden DTO/error tests, provider failure tests, shared parity evidence, zero protected-data leakage, and failure-path verification.
- **Non-Functional Requirements → Integration and Contract Compatibility** requires shared/generated contract tests to validate the same golden lifecycle scenarios across REST, CLI, MCP, and SDK.
- **Non-Functional Requirements → Verification Expectations** requires automated security, tenant-isolation, idempotency, provider-contract, read-model-determinism, and cross-surface compatibility tests.
- **MVP Contract Summary** states that tests and parity oracles have no authority to override the PRD or Contract Spine; drift is a conformance defect. A success-flattening double therefore cannot redefine an expected rejection as product success.

### Memlog alignment

- The memlog records the Contract Spine as machine-contract authority, the generated SDK as typed canonical client, and REST/CLI/MCP as constrained implementations. This supports rejecting false-green evidence from a noncanonical test path.
- The memlog records zero-tolerance isolation, safe denial, complete C13 parity, and explicit state/error semantics. Preserving domain rejection identity is consistent with all of those decisions.
- There is no memlog decision requiring a particular test-double class, allowlist, source scan, or test project. Those mechanisms remain downstream implementation choices.

## Genuine PRD gaps

There is **no substantive product-requirement gap** from this source. The PRD already specifies the product behavior whose evidence was being flattened. Adding a product FR that names `IEventStoreGatewayClient`, `DomainServiceWireResult`, `EventStoreGatewayException`, `FolderResultCode`, an allowlist, or a source scanner would violate the PRD's capability-over-implementation discipline.

The only possible metadata gap is source provenance: this proposal is not listed in `inputDocuments` and has no source-specific memlog entry. Because the proposal is pending approval and contributes no product-body change, provenance should be recorded only as a **reconciled implementation-governance input**, not as an approved product decision. If the wider July reconciliation lists all user-supplied proposals, annotate this source's pending status rather than implying approval.

## Conflicts and supersession

### “MVP already delivered” conflicts with current release state

The proposal says an MVP review is not applicable because “the MVP is already delivered.” The current PRD says OQ1–OQ10 must all close before release acceptance, with each open item carrying a blocking consequence. A finalized PRD is not evidence that the product release has been accepted or delivered.

Disposition: retain the proposal's narrow point that no MVP scope review is needed, but do not copy its delivery claim. Use “no product-scope review is needed for this test-governance change.”

### Approval status is not complete

The source's implementation changes are conditional on user approval, and the appendix leaves approval/handoff in progress. It must not be represented in the PRD or memlog as an approved decision unless a separate approval record exists.

Disposition: treat as pending; implementation artifacts own any later approval/status transition.

### HEAD-specific evidence is time-bound

Counts such as nine flattening doubles, one propagating helper, 57 enum members, exact file/line locations, and Story 11.7 backlog state are audit evidence from the proposal's baseline. They are not durable product facts and may already have changed.

Disposition: keep in the proposal and implementation verification; do not place in the PRD.

### No conflict with product semantics

The recommended guard preserves the current product contract. It does not supersede any memlog decision or stable PRD requirement.

## Implementation and architecture detail that stays out of the PRD

- The class names `InProcessRejectionPropagatingGatewayClient`, `RecordingEventStoreGatewayClient`, `ThrowingEventStoreGatewayClient`, and `UnsupportedGatewayClient`.
- The decision to promote helpers into `Hexalith.Folders.Testing`, compile-link shared sources, or host the guard in `Hexalith.Folders.Testing.Tests` versus `Hexalith.Folders.Contracts.Tests`.
- The `tests/**/*.cs` source scan, repository-root walking, `.slnx` discovery, allowlist entries, error messages, and use of the `ScaffoldContractTests` idiom.
- Deduplicating nine copies, defining exactly one shared propagating or recording double, and documenting request-shape-only evidence.
- The positive test's chosen example (`FolderAclDenied`) and mechanics for extracting the reason code from serialized rejection payloads.
- Story 11.7 acceptance-criteria edits, Epic 8 action-item re-homing, sprint-status comments, owners, sequencing, grandfather fallback, and test-count rules.
- Exact REST-to-gateway-to-processor call-chain names and all file/line evidence.
- Project-context and optional architecture narrative updates.

No `addendum.md` exists. The proposal itself is the appropriate record for this technical depth; an addendum would at most carry a short verification-principle pointer, not the class inventory or test design.

## Recommended stable-ID edits and additions

- **New FRs:** none.
- **FR edits:** none.
- **Renumbering:** none.
- **Downstream traceability:** cite CM4, FR9–FR10, FR27, FR40, FR43–FR51, Contract and Quality Gates, Integration and Contract Compatibility, and Verification Expectations.
- **Metadata:** optionally list this source as a reconciled pending implementation-governance input; do not attribute approval or delivery without evidence.

## Qualitative ideas at risk

- Green tests are not trustworthy when the double bypasses the production rejection path.
- Request-shape evidence and acceptance/parity evidence are different claims and should remain visibly distinguished.
- The canonical shared helper must remain rejection-propagating; consolidation alone is insufficient without a positive regression test.
- A durable guard should reject future ad-hoc copies while explicitly allowing purposeful infrastructure-failure and never-called doubles.
- The work is governance closure and test-harness hardening, not a user-visible product change.
- The change should remain offline-safe and hermetic so it runs in the same contract/governance lanes that it protects.

## Concise disposition

**No PRD body change.** Existing product requirements already mandate safe denial, canonical error identity, and cross-surface parity. Keep all class, guard, story, and sprint-status mechanics downstream; treat the proposal as pending approval, and correct its “MVP delivered” shorthand if referenced because current OQ1–OQ10 still block release acceptance.
