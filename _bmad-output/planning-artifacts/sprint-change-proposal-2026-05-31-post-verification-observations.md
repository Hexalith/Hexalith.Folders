---
workflow: bmad-correct-course
date: 2026-05-31
trigger: Post-implementation verification observations (3 items)
scope: No sprint change required
status: approved-pending-user-confirmation
---

# Sprint Change Proposal — Post-Verification Observations
**Date:** 2026-05-31
**Project:** Hexalith.Folders
**Prepared by:** Developer (correct-course workflow)

---

## Section 1: Issue Summary

Three observations were surfaced during post-implementation manual verification of the running application. The user explicitly categorized these as "remaining issues (not bugs in the code)."

| # | Label | Observed Location | Discovery Context |
|---|-------|------------------|-------------------|
| A | `internal_error` label for unauthenticated access | `/providers/support` page | Verification run, no OIDC login |
| B | Home page links appear unclickable | Home page navigation links | Chrome MCP extension session |
| C | `Hexalith.FrontComposer.Shell.styles.css` 404 | Browser network panel | Static-asset load during UI verification |

### Issue A — `internal_error` label for 401 (unauthenticated access)
When no user is logged in, the Folders server correctly returns HTTP 401. The auth context header shows `Tenant Unknown / Principal Unknown`, which is the expected fail-closed behavior from Story 2.1 and Story 2.6. The UI renders the API error as `internal_error`. The underlying security posture is correct; the label is opaque — `authentication_failure` or a login-redirect pattern would be more descriptive.

### Issue B — Home page links unclickable
Blazor enhanced-navigation (`Blazor.navigateTo('/folders')`) and direct URL navigation both work correctly. The Chrome MCP extension sends synthetic DOM click events that do not trigger the Blazor enhanced-navigation interceptor. This is a test-tooling constraint, not an application defect. Normal browser clicks work fine.

### Issue C — `Hexalith.FrontComposer.Shell.styles.css` 404
A static CSS asset from the FrontComposer submodule is not served by the running Aspire AppHost. The density and projection CSS (from other sources) still loads, so the impact is cosmetic — layout density and projection-specific visual styles may differ from the intended shell design. The source is likely a missing static-file publish step for the FrontComposer submodule's build output.

---

## Section 2: Impact Analysis

### Sprint Status at Analysis
- **Epics 1, 5, 6, 7:** all stories `done`
- **Epic 2:** `2-8b-wire-folder-domain-processor` in `review`; all others `done`
- **Epics 3, 4:** all stories `done`, epic closure pending
- **Overall:** Sprint is in final closure with no blocking work remaining

### Epic Impact
None of the three observations block any in-flight story. No epic scope modifications are required. No new epics are needed.

### PRD Impact
None. FR44 (authentication failure as a distinct error category) is satisfied at the server layer — the 401 is returned and no data is leaked. The `internal_error` UI label is a presentation-layer detail not governed by the PRD. MVP scope is intact.

### Architecture Impact
None.
- AR-MCP-02 explicitly lists `internal_error` as a valid MCP failure kind. The mapping from 401 to a more specific label is a rendering-layer concern, not an architectural contract.
- Issue C (CSS 404) is consistent with a known submodule static-asset publishing detail, not an architecture decision gap.

### UX/Design Impact
None blocking. UX-DR13 (canonical state vocabulary) suggests `authentication_failure` or a login-redirect pattern would be more aligned with canonical vocabulary for Issue A, but this is post-MVP UX polish. UX-DR21 (safe denied states) is already satisfied. No UX specification changes are required.

### Artifact Conflicts
None detected across PRD, epics, architecture, UX spec, or CI artifacts.

---

## Section 3: Recommended Approach

**Recommendation: No sprint course correction. Close the three observations with the following dispositions.**

### Issue A — `internal_error` label
**Disposition: Post-MVP UX backlog item.**

The server behavior is correct. Adding a login-redirect or more descriptive `authentication_failure` label to the UI is a polish item that does not block MVP acceptance. Add to post-MVP backlog when Epic 6 or Story 6.2 refinement is planned.

- Effort: Low
- Risk: Low
- Timeline impact: None to current sprint

### Issue B — Chrome MCP home page links
**Disposition: Closed — working as intended.**

The Chrome MCP extension's synthetic click events are a known constraint that cannot trigger Blazor's enhanced-navigation interceptor. Real browser clicks work. This is not an application defect and requires no story, backlog item, or documentation change.

- Effort: None
- Risk: None
- Timeline impact: None

### Issue C — `Hexalith.FrontComposer.Shell.styles.css` 404
**Disposition: Post-sprint technical investigation note.**

The missing shell CSS is cosmetic and does not affect functional correctness or the MVP acceptance criteria. It is worth a brief investigation into the FrontComposer submodule's static-asset publish pipeline — likely a `StaticWebAssets` or `dotnet publish` step for the submodule's build output that is not wired into the Aspire AppHost. Track separately as a developer investigation item, not a story.

- Effort: Low (investigation)
- Risk: Low (cosmetic)
- Timeline impact: None to current sprint

---

## Section 4: Detailed Change Proposals

**No story or artifact changes are proposed.** All three issues fall outside the scope of sprint story artifacts.

| Artifact | Change | Rationale |
|----------|--------|-----------|
| `epics.md` | None | Sprint is complete; no epic scope affected |
| `prd.md` | None | MVP intact; FR44 satisfied at server layer |
| `architecture.md` | None | AR-MCP-02 is compliant; no contract gap |
| `ux-design-specification.md` | None | UX-DR13/21 satisfied; label polish is post-MVP |
| `sprint-status.yaml` | Mark epics 3 and 4 as `done` (housekeeping) | All stories already `done`; epic status not yet closed — not related to the three issues but worth updating |

---

## Section 5: Implementation Handoff

**Change scope: Minor / No action on sprint artifacts.**

| Role | Responsibility |
|------|---------------|
| Developer | No implementation required for any of the three observations in the current sprint |
| Product Owner | Review and approve this proposal; optionally add Issue A to post-MVP backlog |
| Architect | No action required |

### Post-Sprint Suggestions (not sprint artifacts)

1. **Issue A — Auth label polish:** When Story 6.2 is next revisited or a UX polish sprint is planned, add OIDC login-redirect behavior or map HTTP 401 to `authentication_failure` instead of `internal_error` in the UI error handler.

2. **Issue C — FrontComposer CSS investigation:** Run `dotnet publish` for `Hexalith.Folders.UI` and inspect whether `Hexalith.FrontComposer.Shell.styles.css` appears in `wwwroot` — if not, check the FrontComposer submodule's `StaticWebAssets` target. This is a one-person, sub-hour investigation and fix.

### Success Criteria for Current Sprint
The sprint is complete when:
- Story `2-8b-wire-folder-domain-processor` transitions from `review` to `done`
- Epics 2, 3, and 4 are marked `done` in `sprint-status.yaml`
- Epic 7 retrospective confirms release-readiness evidence is complete

---

*Generated by: bmad-correct-course workflow | Hexalith.Folders | 2026-05-31*
