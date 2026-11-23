---
title: 'Make the shared test harness reliable'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close DW-186, DW-187, DW-189, DW-191, and DW-341 so contributors can build valid fixtures and run focused tests reliably. Classify polling cancellation consistently, validate fixture header identifiers at construction, preserve positional and JSON use of fixtures, and use the pinned Microsoft.Testing.Platform runner in the local test entry points.

</frozen-after-approval>

## Implementation Notes

- The poller checks cancellation before invoking a probe. A timeout takes precedence when the observed cancellation carries the polling token and both sources have fired. Cancellation reported with a different token keeps that token, including when a probe creates a linked child token.
- `TestFolderContext` checks stream segments and header identifiers at construction. The record restores six-value deconstruction in constructor order. Its single public constructor handles JSON round trips.
- The local runner uses `dotnet test --project` for each test project. Full and coverage modes select the xUnit projects from the repository-owned solution entries. Coverage uses per-module Cobertura reports. The runner visits every selected project and returns a failing native exit code if any project fails.

## Review Triage Log

- Medium, fixed. A pre-canceled token now prevents a successful synchronous probe from running. The public polling test asserts zero probe invocations.
- Medium, verified and documented. Timeout classification follows the token carried by the cancellation exception. Tests cover both a privately canceled token and a linked child token.
- Medium, fixed. The runner sets PowerShell's native error preference locally. A stubbed failure returned exit code 7 after all 15 project commands were invoked, even with the caller's preference enabled.
- Medium, deferred. The existing fixture task header differs from the OpenAPI wire name. The existing character policy also permits identifiers that the default HTTP client cannot send. Both have individual ledger entries.
- Medium, deferred. Specialized conformance gates still depend on their fallback for VSTest-style options. Their native runner migration has a ledger entry.
- Ledger and dependency verification complete. All five selected entries cite their tests, and the eight refreshed submodule revisions were verified on their remotes and committed separately.

## Verification

- `pwsh tests/run-tests.ps1 -Mode Testing` passed all 141 Debug tests on the pinned .NET 11 SDK. It also ran successfully when invoked from outside the repository.
- The native class filter passed all seven polling cases. The coverage extension produced a nonempty Cobertura report under the test module's result directory.
- The runner's five modes were checked with a stub `dotnet`. All and Coverage selected 15 projects each. The three individual modes selected their named projects. Coverage preserved a failing exit code while dispatching every project.
- `dotnet test --project tests/Hexalith.Folders.Tests/Hexalith.Folders.Tests.csproj --configuration Release` passed 2,497 tests with two operator-lane skips.
- `dotnet test --project tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --configuration Release` passed 1,438 tests with 11 skips. A focused native class filter also passed all eight lifecycle endpoint cases.
- Focused `dotnet format whitespace --verify-no-changes` and `git diff --check` passed.
- The source-mode `aspire run --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --isolated --non-interactive` stopped at `ValidateCatalogSdkSelection`. The Tenants source checkout rejected EventStore 5.5.1. Both the original and refreshed Builds revisions select 5.5.1, and the Tenants revision is identical.
