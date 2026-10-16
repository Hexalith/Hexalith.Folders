---
title: 'Isolate the Builds release deadline test from inherited stdin'
type: 'bugfix'
created: '2026-10-16'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="authorized publication prerequisite; preserve user-owned intent">

## Intent

**Problem:** The authorized Builds release run 38924860974 passes its initial complete source/package gate but its real-version pre-tag gate fails ElapsedStartupDeadlineReportsStallWithoutWaitingAsync with HXR028. The same committed test deterministically fails when its parent retains an open stdin pipe and passes with immediate EOF. A startup-deadline assertion is inadvertently coupled to the publishing process's input lifetime.

**Approach:** Allow the internal runner/listener boundary to receive an owned input stream while retaining actual standard input by default. Give the deadline test explicit EOF input, retain all its HXR021, exit-code and elapsed-time expectations, and add controlled in-flight reader cases proving cooperative cancellation and the existing bounded HXR028 refusal for a reader that ignores cancellation. Do not suppress or extend the production shutdown bound.

This is one bounded prerequisite to the already authorized publication. Preserve upstream startup/security work, actual-package subscription checks, synthetic acceptance expectations, dependency selections, catalog authority, clean-source eligibility, source validation, provenance and required controls. Platform inputs/assertions, historical evidence, V1Only, unset T0, Story tracker and all 14 unexecuted staging checks remain unchanged. Fresh source qualification and exact-head CI are required after committing this input change, before a fresh guarded release dispatch. Semantic-release alone selects the actual version and tag; the failed run's 4.32.0 selection was not a published release. Platform acceptance, deployment and activation remain separately authorized.

</frozen-after-approval>

## Implementation Notes

- Investigation: StopSignal opens raw Console.OpenStandardInput; its cancellation and close workers and listener are bounded together to three seconds. Stream cancellation can be noncooperative. A real composition controller can send the stop protocol command; the isolated deadline unit test supplies no controller and must own its input fixture.
- Owning files: Builds src/hosts/Hexalith.Builds.Module.AppHost/{AppHostRunner,StopSignal}.cs; test/Hexalith.Builds.Module.Tests/Workspace/AppHostRunnerValidationTests.cs; new controlled input and StopSignal test types, one per C# file.
- The source failure and open/closed-stdin controls remain in artifacts/story117-builds-publication-20261016. No tag or upload was reached in the failed pre-tag attempt.
- No intent gap or new irreversible action in this bounded fix. Git and eventual publication are already authorized; no further permission checkpoint is introduced. The parent Story key is deliberately unchanged under the user's preservation requirement.

- Reviewed build passes with zero warnings/errors. All 10 focused validation/listener tests pass with parent stdin deliberately retained open, including the original deadline test and the three new listener controls. Follow-up review confirms all four fixes.

## Review Triage Log

- medium / patch: No elapsed assertion or independent bound. Refusal is bounded independently at 10 seconds, must take 2.5 to 6 seconds, and must retain the exact HXR028 exception.
- medium / patch: Owned-stream disposal was unobserved. ControlledStdinStream tracks disposal; both tests assert it before fixture cleanup.
- medium / patch: Default stdin binding was untested. An isolated child invokes the omitted-stream path; a real stop command must cancel it while the parent keeps stdin open and persistent EOF is enabled.
- medium / patch: Failure report was checked by substrings. The typed document must match exact schema, run ID, rule, resource, app ID and status.

- Fresh clean-source/package qualification, exact-head CI and successful guarded publication remain required downstream; this bounded test fix alone does not close Story 1.17.

- Bounded implementation committed as `ed80c1c89659391c91c14df1afd20405b6c9aa58` after pinned commitlint validation; push succeeded. GitHub reported account-permitted pull-request/status rule bypass on the normal main push. Fresh exact-head CI and package qualification are required before publication and are recorded separately.

- Concurrent upstream `abdfca89ed82f388b54664825a8a79fed73e7ccf` descends from the reviewed fix, reports expired startup before creating the listener, and adds its own open-pipe subprocess regression. Preserve it and rerun full qualification on that combined source; no earlier source pass is reused for this change.

- Full fresh G-4 source/package qualification on clean combined upstream `abdfca89ed82f388b54664825a8a79fed73e7ccf` passes: 954 passing tests, 13 declared opt-out skips, 55 retained artifacts, both configured/declarative packaged-seed modes, source validation and required controls executed. Diagnostic inventory SHA-256 `1f9833db540f801e948cb6a16e9988c89b1a9680a24e4ce09ab6176e29537f83`; no diagnostic bytes published. Exact-head CI and actual publication are separate downstream evidence.

- Downstream combined-source exact-head CI 38932641377 and actual release 4.32.0 at abdfca89ed82f388b54664825a8a79fed73e7ccf are verified in evidence/story-1-17-builds-publication-2026-10-16.json. Both signed installed tools, all 37 exact synthetic controls and both packaged seed modes pass. This records publication of the bounded prerequisite; Platform acceptance, staging and Story closure remain open.
