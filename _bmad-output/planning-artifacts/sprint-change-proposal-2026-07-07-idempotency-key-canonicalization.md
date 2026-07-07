# Sprint Change Proposal — Provider-Readiness Idempotency Rejection Code Canonicalization

- **Date:** 2026-07-07
- **Author:** Amelia (Developer) via `bmad-correct-course`
- **Project:** Folders
- **Change scope classification:** **Minor** (direct developer implementation)
- **Status:** Implemented & verified in this session
- **Epic-8 retro action item:** *"Reconcile the remaining Epic 5 provider-readiness `idempotency_key_not_accepted` strings to canonical `idempotency_key_not_allowed`, or document a deliberate exception in contract/parity evidence."* (owner: Amelia, priority: medium) → **status flipped to `done`** in `sprint-status.yaml`.

---

## Section 1 — Issue Summary

**Problem statement.** Two Epic-5 provider-readiness **read** routes emitted the legacy problem-detail code `idempotency_key_not_accepted` when rejecting an `Idempotency-Key` header, whereas **every other read route on the Contract Spine** — and the DD1 canon established in Story 8.1 — emits `idempotency_key_not_allowed`. This is a cross-surface contract-string inconsistency (spine drift), not a behavioral defect: both strings produced the same `400` / `category: validation_error` RFC 9457 problem.

**Discovery.** Flagged during Story 8.1 QA (`bmad-qa-generate-e2e-tests`, 2026-06-23) and its adversarial review, which reconciled op5 `GetProviderBinding` to the canonical string but deliberately left the two remaining Epic-5 routes out of Story 8.1's scope (they are governed by the spine/parity gates and pinned by their own tests). The residue was recorded as a medium-priority Epic-8 retro action item (`epic-8-retro-2026-06-27.md:132`) and as a sprint-status action item.

**Evidence of the drift (pre-change):**

| File | Line | Route | Emitted code |
|---|---|---|---|
| `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs` | 245 | `GetProviderSupportEvidence` | `idempotency_key_not_accepted` |
| `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs` | 303 | `ValidateProviderReadiness` | `idempotency_key_not_accepted` |
| `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs` | 103, 125 | (`ValidateProviderReadiness`) | asserted `idempotency_key_not_accepted` |
| `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs` | 350 | (`GetProviderSupportEvidence`) | asserted `idempotency_key_not_accepted` |

The green tests **locked in** the divergence — they were passing against the wrong string.

---

## Section 2 — Impact Analysis

- **Epic impact:** Epic 5 (provider readiness) surface only; Epic 8 retro action item closed. No epic re-plan.
- **Story impact:** Closes the residual dev-follow-up carried from Story 8.1 (and its `8-1` open-items note at `8-1-implement-bucket-a-missing-rest-server-routes.md:210,213`). No new story required.
- **Artifact conflicts:** None. The **canonical** string `idempotency_key_not_allowed` is already the one documented in `docs/operations/audit-and-redaction.md:27`, Story 5.5 parity notes, Story 6.1, Story 8.1 DD1, and Story 10.5 AC. No doc contradicted the target state; the code was the sole outlier.
- **Parity / contract-oracle impact:** **Parity-neutral.** The transport-parity conformance gate (`TransportParityConformanceTests.cs:206`) validates the problem's **`category`** (`validation_error`) against the oracle row's `error_code_set`, *not* the specific `code` string. No parity oracle or contract fixture pins `idempotency_key_not_accepted` anywhere in `src/`, `tests/`, or the C13 oracle. The change therefore cannot break — and was not required to satisfy — the spine/parity gates; it is a pure canonical-consistency fix.
- **Technical impact:** 5 string edits (2 production, 3 test assertions). No DTO, no route registration, no status-code, no behavioral change. Public wire contract: the rejected-request problem `code` value changes from a legacy alias to the canonical alias for two read routes — safe because both are consumer-visible only on the error path and the canonical value is what all sibling routes already return.

**Reconcile vs. deliberate-exception decision.** The action item offered two branches. The **reconcile** branch is unambiguously correct:

1. The codebase's *own comment* (added when op5 was reconciled, `ProviderReadinessEndpoints.cs:113-114`) declares the canonical code *"must match every other read route (`idempotency_key_not_allowed`), not the legacy provider-readiness variant."* Documenting an exception would directly contradict shipped code.
2. Every sibling read route (7+ across `FoldersDomainServiceEndpoints.cs`, `AuditEndpoints.cs`, `OpsConsoleDiagnosticsEndpoints.cs`) already uses the canonical string.
3. No parity/contract evidence depends on the legacy string. There is no cost to reconciling and a standing consistency cost to keeping the divergence.

There is **no defensible deliberate-exception rationale**; the exception branch is a dead option and is explicitly rejected here.

---

## Section 3 — Recommended Approach

**Direct Adjustment** — reconcile the two production routes and their three test assertions to `idempotency_key_not_allowed`, mirroring the self-documenting canonical comment that op5 already carries.

- **Effort:** trivial (5 line edits).
- **Risk:** minimal — parity-neutral, behavior-preserving, covered by existing tests.
- **Timeline:** completed in-session.

---

## Section 4 — Detailed Change Proposals

### 4.1 Production — `src/Hexalith.Folders.Server/ProviderReadinessEndpoints.cs`

**Route `GetProviderSupportEvidence` (was line 245):**

```
OLD:
        if (httpContext.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "idempotency_key_not_accepted",
                retryable: false,
                correlationId);
        }

NEW:
        if (httpContext.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            // Canonical read-op rejection code per Story 8.1 DD1 / AC3 — must match every other
            // read route (idempotency_key_not_allowed), not the legacy provider-readiness variant.
            return SafeProblem(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "idempotency_key_not_allowed",
                retryable: false,
                correlationId);
        }
```

**Route `ValidateProviderReadiness` (was line 303):** identical string change (`idempotency_key_not_accepted` → `idempotency_key_not_allowed`) plus the same canonical comment, on the `ReadHeader(httpContext, "Idempotency-Key") is not null` guard.

**Rationale:** aligns the last two Epic-5 read routes with the DD1 canon and the comment op5 already carries; makes the reconciliation self-documenting for future readers.

### 4.2 Tests — `tests/Hexalith.Folders.Server.Tests/ProviderReadinessEndpointTests.cs`

Three assertions updated from `"\"code\":\"idempotency_key_not_accepted\""` to `"\"code\":\"idempotency_key_not_allowed\""`:

- `ProviderReadinessRouteShouldRejectIdempotencyKeyBeforeObservation` (was line 103)
- `ProviderReadinessRouteShouldSanitizeUnsafeCorrelationOnPreServiceValidationFailure` (was line 125)
- `ProviderSupportEvidenceRouteShouldRejectPreAuthorizationHeadersBeforeReadModelObservation` (was line 350)

**Rationale:** the tests were pinning the divergent legacy string green; they now assert the canonical contract.

### 4.3 Tracking — `_bmad-output/implementation-artifacts/sprint-status.yaml`

Epic-8 action item `status: open` → `status: done`.

> **Note — historical evidence untouched.** The remaining `idempotency_key_not_accepted` occurrences under `_bmad-output/implementation-artifacts/**` (Story 8.1 dev record, `tests/8-1-test-summary.md`, `epic-8-retro-2026-06-27.md`) are **historical evidence of the drift and its resolution** and are intentionally left as-is.

---

## Section 5 — Implementation Handoff & Verification

**Handoff category:** Minor — implemented directly in this Correct Course session; no PO/PM/Architect escalation required.

**Verification performed (this session):**

- `grep` confirms **zero** `idempotency_key_not_accepted` remain in `src/` or `tests/` (only historical `_bmad-output` evidence retains it, by design).
- Targeted lane (`ProviderReadiness` + `ProviderSupportEvidence` + `TransportParity`): **34 passed / 0 failed**.
- Full `Hexalith.Folders.Server.Tests` lane: **561 passed / 0 failed**.
- Build clean under warnings-as-errors (0 errors, 0 warnings).

**Success criteria (met):** Provider-readiness routes and their tests use the canonical `idempotency_key_not_allowed`; spine-wide read-op idempotency-rejection code is now uniform; parity/contract gates unaffected.

**Residual / not in scope:** none. Commit of this change is left to the maintainer's normal per-story commit discipline (this session made no commits).
