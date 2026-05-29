# Operations Console Performance Budget — Release Evidence (Story 6.10 / F-7)

**Status:** Release-validation evidence artifact. Records the F-7 console performance-budget contract and its
measurement method. This is **documentation, not a CI gate** — there is **no NBomber console-page-load harness
and no CI perf gate** in Epic 6. Measured p95/p99 numbers are `reference_pending` (captured during release
validation, not pinned by this story). No numbers are fabricated.

## Document control

| Field | Value |
| --- | --- |
| Workstream | Story 6.10 (enforce console performance and perceived-wait UX) |
| Owns | F-7 perceived-wait UX (shipped); F-7 budget evidence (this doc) |
| Related | architecture.md F-7 (L551), performance budgets (L109), latency-not-a-parity-dimension (L107); prd.md NFR-Performance (L714–718), release-validation (L778–780); docs/ux/ops-console-wireflows.md §3.7 |
| Defers to | Workstream 7 / Story 7.10 (capacity calibration, C1/C2/C5 numbers, any load harness) |
| Method | Release validation (manual/observed), **not** a CI gate |

---

## 1. The budget contract (verbatim, F-7)

The authoritative budget is architecture.md **F-7 (L551)** and the NFR-aligned budgets at **L109**, mirrored by
prd.md **NFR-Performance (L714–718)**:

| Flow class | Target |
| --- | --- |
| Console **primary diagnostic** flows — page load | **< 1.5 s p95** |
| Console **primary diagnostic** flows — page load | **< 3 s p99** |
| **Degraded / incident** flows (e.g. incident-stream, F-6) — page load | **≤ 5 s p95** |
| **Status / audit summary** reads (server-side, NFR-Performance) | **< 500 ms p95** |

These are **release-validation targets, not CI gates** (architecture.md L109; prd.md L778–780). Console primary
budgets are **distinct from end-user product budgets** (prd.md L717). Latency is intentionally **not** a parity
dimension (architecture.md L107) — the parity oracle covers semantic equivalence (categories, states,
idempotency), never timing.

## 2. Measurement method

- **Method:** Release validation — page-load timings are observed against a representative deployment during
  release verification, not asserted by an automated PR gate. Per prd.md L778–780, "Performance/accessibility/
  console-usability NFRs are validated via release evidence, not CI gates."
- **Scope boundary:** The NBomber load lane (`tests/load/`) is scoped to **C1 capacity (lifecycle)**, **not** the
  F-7 console page-load budget (architecture.md L202, L1298). This story adds **no** console perf harness and
  **no** CI perf gate; capacity/number calibration (C1/C2/C5) is owned by **Workstream 7 / Story 7.10**.
- **Recorded numbers:** `reference_pending` — the p95/p99 page-load figures are captured during release
  validation and recorded against the exit-criteria governance evidence
  (`docs/exit-criteria/c0-c13-governance-evidence.yaml`), consistent with the C1/C2/C5 `reference_pending`
  entries there. No p95/p99 values are fabricated in this doc.

```yaml
# Release-validation evidence (to be populated during release verification — not a CI gate).
console_performance_budget:
  status: reference_pending
  reference_pending: true
  note: "TODO(reference-pending): record observed p95/p99 page-load during release validation (Workstream 7 / Story 7.10 owns capacity calibration)."
  targets:
    primary_page_load_p95_ms: 1500
    primary_page_load_p99_ms: 3000
    degraded_incident_page_load_p95_ms: 5000
    status_audit_summary_read_p95_ms: 500
  measured:
    primary_page_load_p95_ms: reference_pending
    primary_page_load_p99_ms: reference_pending
    degraded_incident_page_load_p95_ms: reference_pending
    status_audit_summary_read_p95_ms: reference_pending
```

## 3. Shipped mitigation — the perceived-wait UX (Story 6.10)

While the budget is release-verified, the **perceived-wait UX is the shipped, testable mitigation** this story
delivers (the half of the AC tied to skeleton-at-400 ms + cancel-at-2 s). It implements the
`docs/ux/ops-console-wireflows.md` **§3.7** timing-band contract, driven by the BCL `TimeProvider`:

| Band | Elapsed | Affordance (component) |
| --- | --- | --- |
| Idle | 0–400 ms | No spinner, no skeleton — only a labelled `aria-busy` region (`SkeletonState`). |
| Skeleton | 400 ms – 2 s | Layout-stable skeleton matching the eventual content shape (`SkeletonState`). |
| Still-loading | ≥ 2 s | Skeleton + "still loading…" + **Cancel** (`StillLoadingCancel`). |
| Resolved / Cancelled | — | Content / empty / error, or the neutral cancelled-with-reload view. |

Cancel returns to the prior stable view (page root + heading + banners), **never an error state**; it cancels
the in-flight **read only**, never a domain mutation. Applied across the seven in-flight-read pages: Workspace,
FolderDetail, AuditTrail, OperationTimeline, Provider, ProviderSupport, IncidentStream. The degraded/incident
path (IncidentStream, F-6) honours the ≤ 5 s p95 band; its degraded-mode banner and redaction are **not**
relaxed during loading.

## 4. Out of scope (deferred)

- **No CI perf gate and no NBomber console page-load harness** — release-validated; Workstream 7 / Story 7.10
  owns capacity calibration.
- **No pinned C1/C2/C5 numbers** — `reference_pending` (Workstream 7).
- **No formal WCAG 2.2 AA / no-mutation release audit** — Story 6.11 (build accessibly here; the audit is 6.11).
