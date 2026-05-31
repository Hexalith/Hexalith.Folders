# Story Automator Learnings

## Run: 2026-05-31T05:14:13Z

**Epic:** Hexalith.Folders - Epic Breakdown
**Stories:** 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 7.9, 7.10, 7.11, 7.12, 7.13, 7.14, 7.15, 7.16, 7.17

### Patterns Observed

- Static release-readiness stories need fail-closed gate reports and explicit fact-count guards; `dotnet test --filter` can return green on zero matched tests.
- VSTest socket restrictions recurred often in this environment; xUnit v3 in-process execution is the reliable fallback for focused lanes.
- Root-level submodule initialization and checkout behavior is load-bearing for CI. Avoid `submodules: false` when sibling projects are required, and never introduce recursive submodule setup.
- Documentation evidence must distinguish implemented behavior from `reference_pending` roadmap intent; Story 7.17 review caught and fixed one overclaim.
- Long child review sessions can stall in tool/probe loops. Parent-side fallback worked when it used source-of-truth story/sprint status and reran the same gates locally.

### Code Review Insights

- Common issues: unwired conformance tests, vacuous-pass hazards, tautological negative controls, metadata-only scanner under-coverage, stale planning-doc references, and story-record/file-list drift.
- Total code review cycles recorded by the final state metrics: 31 across 17 completed stories, with 1 escalation/decision point during the run.
- Adversarial review had high value on safety gates: it found the Story 7.10 hardcoded C2 freshness issue and the Story 7.17 documentation/scanner gaps.

### Timing Estimates

- Detailed per-step timing was not captured consistently because multiple child sessions stalled and parent recovery completed several steps directly.
- Future runs should capture session start/end timestamps in `completedSessions` to make timing estimates reliable.

### Recommendations for Future Runs

- Add a reusable in-process xUnit gate helper so focused conformance lanes do not depend on VSTest sockets in restricted sandboxes.
- Add a gate-script invariant that every new conformance class is wired into at least one executable lane before story review.
- Prefer exact parser/scanner negative controls over string-only assertions for docs and gate reports.
- Keep planning artifacts synchronized during retro wrap-up, especially workflow filenames, dependency consumption mode, and approved exit-criteria values.
