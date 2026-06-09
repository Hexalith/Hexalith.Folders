# Sprint Change Proposal — Package Currency Update (Conservative / Stable-First)

- **Date:** 2026-06-09
- **Author:** Jerome (with Developer agent / Correct Course workflow)
- **Trigger type:** Maintenance — dependency currency at MVP release-hardening
- **Change scope classification:** **Minor** (central config + AI-rules doc sync; no functional/scope change) — implemented and validated in-session
- **Status:** Applied + validated; awaiting final approval to close

---

## Section 1 — Issue Summary

The central package pins in `Directory.Packages.props` had drifted behind their latest releases. With Epic 7 (MVP Release Readiness) effectively complete (all stories `done`, including 7-18 and the retro), this is the right moment to refresh dependencies before release.

**Problem statement:** Bring all centrally-managed package pins current — latest **stable** release where one exists, latest **preview/RC** only where no stable replacement exists — **without** violating the deliberate Aspire / Fluent UI / Dapr compatibility guardrails recorded in `project-context.md`.

**Discovery / evidence:** Live NuGet flat-container query of all 53 pins on 2026-06-09. 16 packages had a newer stable; 3 deliberate preview/RC pins had a newer preview; the remainder were already at latest stable. Several pins (Dapr `1.17.7→1.17.9`, Aspire `13.3.3→13.3.5`, Playwright/YamlDotNet) had already been bumped in the props file but never reflected in the version docs — pre-existing drift this change also corrects.

**User decisions (captured up front):**
- Update policy: **Conservative (stable-first)** — latest stable everywhere a stable exists; keep deliberate RC/preview pins, advancing them to their latest preview/RC.
- Review mode: **Incremental** (per-group approval).
- Validation depth: **Restore + build + targeted tests**.
- Judgment call 1 (Aspire): **Full 13.4.x normalization**.
- Judgment call 2 (Fluent UI): **Bump rc.2 → rc.3**.

---

## Section 2 — Impact Analysis

- **Epic impact:** None to scope/order. Epic 7 (release readiness) is the natural home; this is release-hardening within it. No epic added, removed, deferred, or resequenced.
- **Story impact:** No story scope changes. Optionally trackable as a maintenance note under Epic 7; no new story file required for a config-level bump.
- **PRD:** No conflict — no functional or MVP-scope change.
- **Architecture (`architecture.md`):** Contains **planning-time** version snapshots in decision-record prose (e.g. "Aspire.Hosting.AppHost 13.3.0", "ModelContextProtocol 1.3.0"). These were already drifted before this change. Per the established precedence rule in `project-context.md` — *"Repository configuration and project files are authoritative when planning artifacts drift: prefer … `Directory.Packages.props` … over older architecture text"* — `architecture.md` is **intentionally superseded by the props file** and is left as a historical record. No edits made; not a conflict.
- **UX:** No conflict.
- **Other artifacts:**
  - `Directory.Packages.props` — the change itself (19 version edits).
  - `project-context.md` "Technology Stack & Versions" — **synced** to the new pins (this was stale and is the authoritative AI-rules doc).
  - CI gates — exercised by the restore/build/test validation below; no gate-script edits needed.
- **Technical impact:** Recompiled clean under warnings-as-errors; no source changes required.

---

## Section 3 — Recommended Approach

**Option 1 — Direct Adjustment (SELECTED).** Edit the central props file, verify with restore + build + targeted hermetic tests, and sync the authoritative version doc.
- Effort: **Low.** Risk: **Low–Medium** (Aspire normalization + Fluent UI RC were the only real risks; both passed the gate).
- Option 2 (Rollback): Not viable — nothing to revert.
- Option 3 (MVP review): Not viable — MVP scope unaffected.

The "intentionally mixed Aspire" guardrail permits change *with compatibility verification*; the restore + build + test gate **is** that verification, so normalization is justified and now recorded as the new baseline.

---

## Section 4 — Detailed Change Proposals

### 4.1 `Directory.Packages.props` — 19 version edits (APPLIED)

**Group A — latest stable (low risk):**

| Package | Old → New |
|---|---|
| Microsoft.Extensions.Configuration.Binder | 10.0.5 → 10.0.8 |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.5 → 10.0.8 |
| Microsoft.Extensions.Http | 10.0.5 → 10.0.8 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.5 → 10.0.8 |
| Microsoft.AspNetCore.OpenApi | 10.0.5 → 10.0.8 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.5 → 10.0.8 |
| Microsoft.FluentUI.AspNetCore.Components.Icons | 4.14.0 → 4.14.2 |
| Swashbuckle.AspNetCore.SwaggerUI | 10.1.7 → 10.2.1 |
| ModelContextProtocol | 1.3.0 → 1.4.0 |
| Microsoft.NET.Test.Sdk | 18.5.1 → 18.6.0 |
| Testcontainers | 4.10.0 → 4.12.0 |

**Group C — Aspire full 13.4.x normalization + Fluent UI RC (guardrail-flagged):**

| Package | Old → New | Note |
|---|---|---|
| Aspire.Hosting | 13.3.5 → 13.4.3 | stable |
| Aspire.Hosting.Azure.AppContainers | 13.2.2 → 13.4.3 | stable |
| Aspire.Hosting.Docker | 13.2.2 → 13.4.3 | stable |
| Aspire.Hosting.Redis | 13.3.5 → 13.4.3 | stable |
| Aspire.Hosting.Testing | 13.2.1 → 13.4.3 | stable |
| Aspire.Hosting.Keycloak | 13.2.2-preview.1.26207.2 → 13.4.3-preview.1.26305.13 | no stable exists → latest preview |
| Aspire.Hosting.Kubernetes | 13.2.2-preview.1.26207.2 → 13.4.3-preview.1.26305.13 | no stable exists → latest preview |
| Microsoft.FluentUI.AspNetCore.Components | 5.0.0-rc.2-26098.1 → 5.0.0-rc.3-26138.1 | stay on 5.0 RC line (4.14.x stable is previous major) |

**Deliberately unchanged (already latest stable; preview-only newer versions rejected under stable-first):**
Dapr 1.17.9 (1.18 = rc), CommunityToolkit.Aspire.Hosting.Dapr 13.0.0 (no 13.4 stable), OpenTelemetry 1.15.x family, all Microsoft.Extensions.* already on 10.0.8, System.CommandLine 2.0.8 (3.0 = preview), MediatR 14.1.0, Newtonsoft.Json 13.0.4 (13.0.5 = beta), NSwag.MSBuild 14.7.1, Octokit 14.0.0, FluentValidation 12.1.1, coverlet 10.0.1, Microsoft.AspNetCore.TestHost 10.0.8, NBomber 6.4.1 (6.5 = beta), xunit.v3 3.2.2 (4.0 = pre), xunit.runner.visualstudio 3.1.5, Shouldly 4.3.0, NSubstitute 5.3.0 (6.0 = rc), YamlDotNet 18.0.0, Microsoft.Playwright 1.60.0, bunit 2.7.2 (2.8 = preview).

**Contingency (NOT triggered):** If Aspire 13.4.3 had conflicted with CommunityToolkit 13.0.0, the fallback was CommunityToolkit 13.4.0-preview. Restore produced no `NU1605` downgrade and the build was clean, so 13.0.0 is retained (honors stable-first).

### 4.2 `project-context.md` "Technology Stack & Versions" — version inventory sync (APPLIED)
Updated the Dapr, Aspire, Fluent UI, MCP, and testing bullets to the new pins (also corrected pre-existing drift: Dapr 1.17.9, YamlDotNet 18.0.0, Playwright 1.60.0), reworded the Aspire bullet from "intentionally mixed" to "aligned on 13.4.x", and bumped `Last Updated` to 2026-06-09.

### 4.3 `architecture.md` — no change
Left intact as a historical decision record; superseded by `Directory.Packages.props` per the existing precedence rule (Section 2).

---

## Validation Evidence (Restore + Build + Targeted Tests)

- **Restore:** `dotnet restore Hexalith.Folders.slnx` — clean, **no `NU1605` downgrade** errors. Aspire 13.4.3 and CommunityToolkit 13.0.0 coexist.
- **Build:** `dotnet build Hexalith.Folders.slnx` — **0 warnings, 0 errors** across all 45 projects (warnings-as-errors active; clean Fluent UI rc.2→rc.3 compile ⇒ no API breaks in compiled UI code). *(One transient `GenerateDepsFile` file-lock race on first parallel run; cleared on a `-maxcpucount:1` rebuild — environmental, not version-related.)*
- **Targeted hermetic tests — 4215 passed, 0 failed:**

  | Lane | Result |
  |---|---|
  | UI.Tests (bunit / Fluent UI rc.3) | 521 ✓ |
  | Tests (core unit) | 1314 ✓ |
  | Contracts.Tests | 250 ✓ |
  | Client.Tests | 280 ✓ |
  | Cli.Tests | 691 ✓ |
  | Mcp.Tests (MCP 1.4.0) | 646 ✓ |
  | Testing.Tests | 60 ✓ |
  | Server.Tests (7.18 baseline) | 434 ✓ |
  | Workers.Tests | 19 ✓ |

- **Not run (require Dapr/Docker/Playwright/network, excluded per gate rules):** IntegrationTests, UI.E2E.Tests, LoadTests. **Residual risk** lives here — Aspire AppHost/sidecar wiring and the Aspire.Hosting.Testing 13.4.3 integration host are only fully exercised by the integration lane against live infrastructure.

---

## Section 5 — Implementation Handoff

- **Scope classification:** **Minor** — implemented directly by the Developer agent; no backlog reorganization or replan.
- **Done in-session:** props edits, restore/build/targeted tests, `project-context.md` sync, this proposal.
- **Recommended follow-ups (not blockers):**
  1. Run the **integration / Aspire AppHost smoke** lane against live Dapr/Docker before tagging the release, to close the residual risk on Aspire 13.4.x sidecar wiring (AppHost/Aspire topology changes require an Aspire app restart to trust resource wiring).
  2. Track Dapr `1.18` GA, Fluent UI `5.0` GA, System.CommandLine `3.0` GA, and bunit `2.8` for a future stable-only follow-up.
- **Success criteria:** restore/build clean under warnings-as-errors (met); hermetic test lanes green (met); version docs consistent with `Directory.Packages.props` (met).
