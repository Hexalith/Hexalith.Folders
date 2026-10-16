---
title: 'Restore the existing Platform AppHost bootstrap for Story 1.17 preparation'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
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

- Platform baseline advanced externally to `1711deba08a4e66b46acfff8d08e7f2ad7f74ba1` before edits; its SDK pin fix is preserved. Folders HEAD advanced externally to `3ece17cfc1bd48723916907c5cac15dbd133aaa6`; all 4,830 initially captured tracked Folders file bytes still matched before this evidence update.
- Implementation also corrects the test seed project path and only the null-check/await analyzer errors exposed by the audited host/test builds; analyzer severity and test expectations are unchanged.
- Final checks: 10 resolver tests and 10 pin tests pass; audited Debug AppHost and composition-test builds pass; three entry/reflection checks pass; seed/writer suite has four passes and two retained Builds-seed failures. Actual AppHost advances beyond restore/compile and refuses at existing workspace admission. This bounded implementation is complete; staging and Story 1.17 remain open.

## Review Triage Log

- medium / patch: explicit relative Builds root previously resolved differently in child projects. Normalize once into HexalithBuildsResolvedRoot; real MSBuild tests exercise both tooling and test-seed paths.
- medium / patch: verifier --root . searched the wrong sibling. Resolve root before parent lookup; regression test passes.
- medium / patch: Debug compatibility-import test could pass without import. Assert the imported sentinel as well as catalog precedence.
- low / patch: test-seed reference lacked direct fixture coverage. Evaluate the real test project for sibling and relative override selections.
- medium / defer: runtime package image lookup retains its pre-existing nested catalog assumption. No package topology or staging qualification is claimed; recorded in deferred-work.md.
- medium / defer: opt-in Builds live smoke retains its pre-existing nested runner assumption. Recorded in deferred-work.md; ordinary admission remains fail-closed.

- The earlier all-file preservation observation preceded an external committed release-workflow action update. The final receipt records that external exception and does not reassert package/workflow qualification for changed inputs. The original Story 1.17 tracker is untouched; done here applies only to the bounded bootstrap implementation.
