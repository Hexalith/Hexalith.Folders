---
title: 'Restore the existing Platform AppHost bootstrap for Story 1.17 preparation'
type: 'bugfix'
created: '2026-10-16'
status: 'in-progress'
route: 'oneshot'
review_loop_iteration: 0
baseline_commit: '1030c50e39b54a30ef554f10662bea2225f17ed3'
context: []
---

<frozen-after-approval reason="user authorized reversible first-install prerequisite implementation">

## Intent

**Problem:** The inspected Platform file AppHost fails before composition: its project graph assumes an uninitialized nested Builds checkout and its SDK/EventStore file pins drift from the existing central catalog. A broken bootstrap prevents meaningful host qualification even before module enrollment.

**Approach:** Repair the existing Platform bootstrap to consume the available Builds checkout consistently and align its file-host pins with that catalog. Verify the resolver, pin guard, audited Debug build and actual entry-point probe. Reuse unchanged Folders/Projects evidence. First-install preparation is authorized for the inspected target; infrastructure writes, publication, deployment, Git staging/commit/push and production activation remain separately gated. Preserve V1Only, unset T0, historical approvals and every Story 1.17 tracker field.

</frozen-after-approval>

## Implementation Notes

- Scope: Platform `Directory.Build.props`, `Directory.Packages.props`, composition project, `apphost.cs`, pin verifier/tests and a focused MSBuild resolver test; qualification evidence in Folders. No domain or authentication implementation changes.
- Reuse the established sibling-first Builds catalog selection; explicitly configured roots remain authoritative. Do not initialize nested submodules or bypass catalog pin refusal. Retain the runner-provided `BuildsToolingAssemblyPath` branch.
- Expected outcomes: available sibling or standalone nested Builds resolves consistently for catalog and tooling; missing roots refuse; exact file pins pass; drift still fails. Actual AppHost invocation must advance beyond MSB4025 and NU1605. Record any later mapping/profile/hosting blocker without claiming a working topology or staging qualification.
- Investigation excluded adapter extraction: AD-13 requires a provider-free extension, while the existing ACL adapter depends on Folders core/provider SDKs. Full release/chart/executor and token exchange are larger prerequisites.
- No unresolved user choice and no irreversible action. This bounded prerequisite has no sprint tracker key; the parent Story 1.17 spec and lifecycle remain unchanged.
