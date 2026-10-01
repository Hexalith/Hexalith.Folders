---
title: 'Fix test and CI failures before release'
type: 'bugfix'
created: '2026-10-01'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Run every Folders test project and the configured CI/CD checks, repair demonstrated failures, and validate the corrections before releasing through the existing workflow. Preserve test assertions and publication controls. The user has authorized completing fixes and creating the release without additional confirmation; the signed-in release reviewer may satisfy the existing environment approval.

The first demonstrated failure is the sample lifecycle handler returning a consumer readiness payload where the generated client requires the operator variant. Correct the fixture to include the operator audience and all required contract metadata. Investigate other failures from the full test and CI lanes and make the smallest supported corrections.

</frozen-after-approval>

## Implementation Notes

- Initial source: `1f4bf82bcaa9668022493d53bc44884a156dad73`; clean main after fetching origin. No user changes to preserve in the working tree.
- Python CI/CD tooling tests pass 11/11. Earlier CI run `36922021657` fails three sample lifecycle tests while parsing the mock readiness response.
- Relevant contract: `ProviderReadinessOperator` in `src/Hexalith.Folders.Contracts/openapi/hexalith.folders.v1.yaml`; SDK and endpoint contracts remain unchanged.
- Local development uses Debug/source references; hosted CI and release use Release/NuGet dependencies. Run test projects individually, including integration, sample and Playwright suites.
- Existing release path publishes the five IDs in `tools/release-packages.json`, seals package/symbol pairs, validates isolated consumers, and checks the exact green main source. Do not modify environment policies or create substitute approval evidence.
- Corrected the sample readiness payload and regenerated its deterministic candidate inventory without changing pending approval or production exposure.
- Corrected nightly class selection, configuration-specific assembly/solution selection, and the native dependency catalog path. Both scheduled workflows now use the CI solution in Release/package mode.
- Independent review exposed stale child-policy reports masking process failures and ambiguous nightly stdout counters. The wrappers now require fresh successful evidence, honor nonzero exits, and parse one complete successful assembly summary. Behavioral tooling tests pass 25 methods, including negative-control scenarios.
- Local Debug and Release builds pass with zero warnings/errors. Contract/parity and security/redaction gates pass. Dry-run package validation seals five package/symbol pairs and builds two package-only consumers.
- The isolated Alpine smart-HTTP test passes against Forgejo 16.0.3 and 15.0.7. The Aspire topology starts successfully from built output, and three opt-in AppHost cases pass; two lack resolvable sidecar endpoints. The FR58 public mutation/search acceptance case remains deliberately blocked by unfinished Task 0 event-emission and production-authorization work. No assertion or runtime authority gate was weakened.
- Capacity smoke/calibration, retention/deletion, NFR traceability, safety and governance gates all pass. Local Chromium hangs on a standalone canvas/axe probe; Firefox executes the same complete 63-case browser suite successfully. The optional `FOLDERS_PLAYWRIGHT_BROWSER=firefox` selector preserves Chromium as the default hosted gate and retains every WCAG rule. No package dependency pins changed.

## Review Triage Log

- medium, patched: browser origin guards must reject redirect chains before another origin receives the fixture bearer header; the temporary two-host probe rejects direct and multi-hop redirects with zero destination requests.
- low, patched: authenticated context creation preserves caller-supplied HTTP headers while overriding the fixture Authorization value. Mutation of the consumed options object is retained because every caller creates fresh options.
- high, patched: nightly selection requires one complete expected-assembly summary and binds all unsuccessful counters to that same summary; diagnostic totals and duplicate summaries cannot manufacture an exact count.
- medium, patched: behavioral tooling regressions cover malformed/absent/duplicate summaries, nonzero error/failure/unrun counters, and a failing GitHub runner after a successful Forgejo runner.
- low, verified boundaries / deferred interaction coverage: the browser probe covers identity/header scoping and blocked direct/multi-hop redirects, and the full browser suite passes in Firefox. Extending existing prerendered DOM tests to prove server circuit interactions is a broader test-design improvement; the local Chromium canvas failure reproduces without any host or circuit. No production authorization change follows from fixture authentication.
- high, patched: the scheduled-policy wrapper discards its generated prior child report before invocation, honors the actual child exit, and requires fresh passing evidence.

## Final Local Verification

- Final `dotnet restore/build Hexalith.Folders.CI.slnx` in Release/package mode passed with zero warnings/errors. Full contract suite passed 342/342; the complete baseline gate passed formatting, analyzers, selected unit lanes and package-mode validation.
- All 15 default test projects were executed: 6,334 cases, 6,328 passed and six documented opt-in skips. Corrected contracts and browser suites were rerun successfully. Debug/source and Release/package browser suites each passed 63/63 in Firefox. Hosted Chromium remains required before release.
- Independent review completed and every finding is recorded above. The circuit-interaction coverage improvement is recorded in the deferred-work ledger.
- The separately enabled Aspire acceptance lane remains 3 passed, 2 skipped and 1 intentionally blocked FR58 case; this is unfinished feature scope rather than a repaired fixture or CI failure.
