---
workflow: bmad-correct-course
date: 2026-05-31
trigger: Systemic test-host DI-composition red surfaced during 2-8b verification
scope: Moderate — reopen Epic 7, add Story 7.18 (no PRD/architecture/UX change)
status: approved-pending-user-confirmation
---

# Sprint Change Proposal — Restore Shared Test-Host Composition Baseline
**Date:** 2026-05-31
**Project:** Hexalith.Folders
**Prepared by:** Developer (bmad-correct-course workflow)

---

## Section 1: Issue Summary

During post-implementation verification of Story 2-8b (`/bmad-code-review`), a **systemic, pre-existing test-host red** was surfaced at HEAD. It is **not** a 2-8b defect, and — importantly — it is **not** the "historical reds" blocker the Epic 7 retrospective named.

**Root cause (confirmed by code inspection):**
- `AddFoldersServer()` registers `FoldersAuthSchemeValidator` as an `IHostedService` (`src/Hexalith.Folders.Server/Authentication/FoldersAuthSchemeValidator.cs:12-13`); its constructor needs `IAuthenticationSchemeProvider`.
- `MapFoldersServerEndpoints()` calls `MapDefaultEndpoints()` (`src/Hexalith.Folders.Server/FoldersServerModule.cs:100`), which needs `HealthCheckService`.
- Both services are supplied by `AddServiceDefaults()` / `AddAuthentication()` + `AddHealthChecks()`. Test hosts that mount the server surface **without** them fail DI validation at `WebApplicationBuilder.Build()` — fail-closed, cascading across the suite.
- Introduced by later stories updating the shared server surface without updating every test host: auth validator (`6e816ce`, 2026-05-18) + ServiceDefaults health checks.

**Magnitude (re-measured 2026-05-31, xUnit v3 in-process runner — the verification path of record in this sandbox):**

```
Hexalith.Folders.Server.Tests   Total: 433, Failed: 339, Passed: 94, Skipped: 0
  → "Unable to resolve service for type 'IAuthenticationSchemeProvider'
     while attempting to activate 'FoldersAuthSchemeValidator'"
```

Plus, documented (not re-run this pass): `Hexalith.Folders.IntegrationTests` ≈11 (Epic 5 Golden/MixedSurface) and `Hexalith.Folders.Tests` ≈2 (Epic 3 provider-boundary guards). **≈352 failures from one root cause.**

**Why this is a real course-correction, not a one-line defer:** the framing inherited from the 2-8b note called this "the documented historical reds blocker." It is not. The Epic 7 retro and the release-readiness record name the historical-reds blocker as the **"4–6 epic-1 CLI negative-scope reds in `Contracts.Tests`"** — a *different, ~50× smaller* root cause. This blocker was never captured by the Epic 7 retro and materially qualifies the "conditionally release-ready" claim: the MVP test suite does not run green at HEAD.

---

## Section 2: Impact Analysis

### Sprint Status at Analysis
- Epics 1–4, 6, 7: all stories `done`. Epic 5: all stories + retro `done` (status field drifted to `in-progress` — housekeeping, see §4).
- Epic 7 (`MVP Release Readiness`) and its retrospective were `done`; this proposal reopens Epic 7.

### Epic Impact
- **Epic 7 — reopened.** A new Story 7.18 owns remediation. `epic-7: done → in-progress` until 7.18 is green. No other epic scope changes.
- **Epics 2–6 — none.** The reds live in test hosts across closed Epic 2–6 stories, but sweeping 350+ failures into per-story reviews would be scope creep; one dedicated remediation story is the correct container.

### Story Impact
- **New: Story 7.18 — Restore shared test-host composition baseline** (`ready-for-dev`).
- **2-8b — none.** It already correctly disclaimed the red and applied the proven fix to its own host.

### PRD Impact
None on requirements or MVP scope. The 401/health behaviors and the `AddServiceDefaults` composition contract are correct as-specified. The impact is on **NFR test-evidence honesty** — the assertion that MVP tests pass at HEAD — which Story 7.18 restores.

### Architecture Impact
None. `AddServiceDefaults` is the documented composition seam; test hosts under-compose it. No architectural contract changes; production `AddFoldersServer` / `MapFoldersServerEndpoints` / `FoldersAuthSchemeValidator` are unchanged.

### UX/Design Impact
None.

### Technical Impact
Mechanical, test-only. A shared `AddAuthentication()`+`AddHealthChecks()` test-host helper applied across 19 `Server.Tests` hosts + the IntegrationTests/Folders.Tests host-build paths, plus one central composition smoke test. No production code behavior change.

### Artifact Conflicts
One **record correction**: deferred-work.md and the Epic 7 retro conflated this with the epic-1 CLI-reds item. Both are corrected by this proposal (§4).

---

## Section 3: Recommended Approach

**Recommended path: Direct Adjustment** — add one remediation story under a reopened Epic 7. No rollback, no MVP-scope reduction.

| Option | Verdict |
|---|---|
| Fix inline in 2-8b | ❌ Rejected by user — scope creep across closed Epic 2–6 stories. |
| Document-only defer | ❌ Insufficient — a ≈352-red suite is a GO/NO-GO release blocker, not background noise. |
| **Reopen Epic 7 + Story 7.18** | ✅ **Selected** — owns the release-readiness remediation where it belongs; keeps the audit trail honest. |

**Helper design (a) selected:** a minimal shared `AddAuthentication()`+`AddHealthChecks()` test helper (matches the proven `GoldenLifecycleParityTests`/`ArchiveFolderProcessWiringTests` pattern), not production `AddServiceDefaults()` in test hosts (avoids dragging OTel/service-discovery into slim unit hosts).

- **Effort:** Low–Medium (mechanical, broad). **Risk:** Low (test-only, fail-closed today). **Timeline:** One focused story; gates the release.

---

## Section 4: Detailed Change Proposals

| # | Artifact | Change |
|---|----------|--------|
| ① | `implementation-artifacts/7-18-restore-test-host-composition-baseline.md` | **New** story spec; helper design (a); ACs pin `Server.Tests` 433/0 + IntegrationTests/Folders.Tests green + central smoke test + no prod change. Status `ready-for-dev`. |
| ② | `implementation-artifacts/sprint-status.yaml` | `epic-7: done → in-progress`; add `7-18-…: ready-for-dev`; comment recording the reopen reason; `last_updated` bumped. |
| ③ | `implementation-artifacts/sprint-status.yaml` | Housekeeping (unrelated to issue): `epic-5: in-progress → done` (all 5-* + retro already `done`). |
| ④ | `implementation-artifacts/deferred-work.md` | Reframe the 2-8b "SYSTEMIC TEST-HOST RED" entry: distinct from CLI-reds, re-measured `433/339/94`, owned by Story 7.18. |
| ⑤ | `implementation-artifacts/epic-7-retro-2026-05-31.md` | New action item (Jerome/Platform, High); corrected "Known reds" row; dated **Post-Retro Addendum** correcting the historical-reds conflation and the readiness caveat. |
| ⑥ | `planning-artifacts/epics.md` | Append Story 7.18 to the Epic 7 section. |

Old→new highlights:
- `sprint-status.yaml`: `epic-7: done` → `epic-7: in-progress`; `epic-5: in-progress` → `epic-5: done`.
- `deferred-work.md`: `Own under Epic 7 release-readiness.` → `Resolution owned by Story 7.18 … CORRECTION: distinct, ~50× larger than the epic-1 CLI-reds item.`
- Retro "Known reds": single CLI-reds class → **two distinct classes**, with the test-host class owned by 7.18.

---

## Section 5: Implementation Handoff

**Change scope: Moderate** (backlog reorganization — reopen epic + one story; no replan).

| Role | Responsibility |
|------|---------------|
| Product Owner / Jerome | Approve this proposal; confirm Story 7.18 priority and GO/NO-GO blocker status. |
| Developer (Story 7.18) | Implement helper design (a) across all affected hosts; add the central composition smoke test; record observed-vs-expected counts; flip 7-18 → `done` and `epic-7` → `done` when green. |
| Architect | No action — no architectural contract change. |
| Murat | Keep the **separate** epic-1 CLI-reds quarantine item (retro action item) distinct from Story 7.18. |

### Success Criteria
- `Hexalith.Folders.Server.Tests`: Total 433, Failed 0, Skipped 0.
- `Hexalith.Folders.IntegrationTests` and `Hexalith.Folders.Tests`: 0 failures from this composition cause.
- A central host-composition smoke test (`ValidateOnBuild`) exists and passes.
- No production code behavior change.
- On green: `7-18` → `done`, `epic-7` → `done`; release GO/NO-GO updated.

---

*Generated by: bmad-correct-course workflow | Hexalith.Folders | 2026-05-31*
