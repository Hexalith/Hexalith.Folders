---
title: 'Update .NET, packages, and root submodules'
type: 'chore'
created: '2026-08-29'
status: 'done'
review_loop_iteration: 2
baseline_commit: 'f1305dced4fd00f2ac528ec63ccb5c852121dddc'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The repository pins unavailable .NET SDK `10.0.302`, so root `dotnet` commands fail on the installed and current .NET 10 SDK `10.0.400`. The AppHost SDK and active version guidance also lag the already-current Hexalith.Builds package catalog, while all eight root submodules currently match their remote `main` heads.

**Approach:** Move the root to the latest stable .NET 10 SDK, align `Aspire.AppHost.Sdk` with the governed Aspire `13.5.3` family, refresh active version guidance, and re-verify package and root-submodule currency. Advance a root gitlink only if its declared submodule has moved upstream before implementation.

## Boundaries & Constraints

**Always:** Keep `net10.0`, `rollForward: latestPatch`, central package management, and the Hexalith.Builds stable-first audit policy. Limit submodule operations to the eight paths declared by the root `.gitmodules`; fetch and compare exact `origin/main` commits before changing any gitlink. Preserve compatibility-retained preview/RC pins and validate Debug/source plus Release/package consumption.

**Ask First:** Editing content or creating commits inside a submodule; adopting .NET 11 preview or changing any governed prerelease/compatibility exception; changing application/runtime behavior to accommodate an upgrade.

**Never:** Run recursive or `--remote` submodule updates, initialize nested submodules, manufacture gitlink/package edits when upstream is already current, replace the audited catalog with highest-version guesses, change target framework/container major, or rewrite historical story/audit artifacts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| SDK upgrade | Root pin `10.0.302`; installed/latest stable SDK `10.0.400` | Root selects `10.0.400`; target remains `net10.0` | Stop if official .NET 10 metadata or installed SDK no longer supports the selected pin |
| Governed package audit | 285 imported central pins plus AppHost SDK exception | Catalog remains unchanged when audit has no accepted updates; AppHost SDK becomes `13.5.3` | Retain documented exceptions; do not upgrade unresolved/dormant pins from incomplete source evidence |
| Submodules unchanged | Parent gitlinks equal fetched `origin/main` | No gitlink diff | Report verified no-op |
| Submodule advances | A declared `origin/main` moves before implementation | Advance only that root gitlink and record old/new full commits | Stop on dirty worktree, non-fast-forward/unreachable head, or compatibility failure |

</frozen-after-approval>

## Code Map

- `global.json:3-5` -- root stable-only SDK pin; `latestPatch` cannot cross from feature band 302 to installed 400, and `allowPrerelease=false` prevents preview SDK selection.
- `Directory.Build.props:31-35` -- authoritative `net10.0`, C# latest, and warnings-as-errors policy; keep unchanged.
- `Directory.Packages.props:3-13` -- version-free CPM wrapper importing the Hexalith.Builds catalog.
- `references/Hexalith.Builds/Props/Directory.Packages.props:3-321` -- read-only authority for 285 central pins; currently audit-current.
- `references/Hexalith.Builds/Tools/package-version-exceptions.json:35-43` -- requires Folders AppHost SDK `13.5.3` aligned with Aspire Hosting.
- `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj:1-9` -- non-CPM `Aspire.AppHost.Sdk/13.4.6` pin to align; preserve NuGet-backed DCP/dashboard dependencies with explicit `AspireUseCliBundle=false` and suppress only the SDK's documented `ASPIRE010` migration reminder.
- `_bmad-output/project-context.md:35-48,122,213` -- active SDK/package inventory, root submodule list, and update date to synchronize.
- `docs/ux/ops-console-accessibility-and-no-mutation-verification.md:242` -- operational SDK-pin reference to synchronize.
- `.gitmodules` and eight `references/Hexalith.*` gitlinks -- root-only submodule inventory; currently all equal remote `main`.
- `tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs:288-306` -- root TFM/CPM/no-inline-version contract; extend it to verify the stable-only selected SDK, exact AppHost-to-catalog relationship, unconditional explicit NuGet-backed Aspire mode, unconditional exact warning token, and unique Folders exception inventory row.
- `tests/tools/run-baseline-ci-gates.ps1:230-259` -- documents the focused Debug/source and Release/package commands; its umbrella invocation currently stops at unrelated whitespace drift in `src/Hexalith.Folders/Providers/Abstractions/ProviderOperationSourceResolutionResult.cs`.
- `tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj` and `AspireFoldersAppHostFixture.cs:82` -- AppHost verification is opt-in; build and execute this host in both Debug/source and Release/package modes so the package-backed topology is constructed, not merely compiled.
- `tests/Hexalith.Folders.AppHost.Tests/FoldersTopologyCrossProcessTests.cs:279-287` -- boot checks run in both dependency modes, while the two existing Dapr endpoint probes may skip when hidden sidecar HTTP endpoints cannot be resolved.

## Tasks & Acceptance

**Execution:**
- [x] `global.json` -- pin stable SDK `10.0.400` while preserving `latestPatch` and setting `allowPrerelease=false`.
- [x] `src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` -- align AppHost SDK to governed `13.5.3`, unconditionally retain NuGet-backed orchestration with `AspireUseCliBundle=false`, and unconditionally suppress exactly `ASPIRE010` while leaving every other diagnostic blocking; boot the AppHost topology in both Debug/source and Release/package modes.
- [x] `_bmad-output/project-context.md`, `docs/ux/ops-console-accessibility-and-no-mutation-verification.md` -- synchronize active version and root-submodule guidance with effective configuration while preserving the UX document's exact historical Windows SDK pin separately from current reproduction guidance.
- [x] `.gitmodules`, `references/Hexalith.Builds/Tools/*`, root gitlinks -- run governed package/submodule audits; change no catalog row or gitlink unless fresh evidence identifies an accepted update. Verify each changed gitlink is a fast-forward to its recorded fetched `origin/main`, and verify the EventStore commit's nested FrontComposer object matches the independently selected root FrontComposer commit without initializing it.
- [x] `_bmad-output/implementation-artifacts/spec-update-dotnet-packages-submodules.md` -- include the untracked spec in whitespace verification with an explicit no-index check; treat exit `1` with no output as the expected content-difference result and any diagnostic output as failure.

**Acceptance Criteria:**
- Given the repository root, when `dotnet --version` runs, then it selects `10.0.400` and projects still target `net10.0`.
- Given the imported catalog and exception inventory, when authority/exception validations run, then all pins pass and AppHost SDK exactly matches Aspire `13.5.3`.
- Given live NuGet/outdated checks, when current package references are evaluated stable-first, then no accepted outdated package remains.
- Given fetched root-declared remotes, when each gitlink is compared with `origin/main`, then every pointer is current and no nested submodule was initialized or updated.
- Given Debug/source and Release/package modes, when restore/build and focused host/integration gates run, then they complete with zero warnings and errors and all tests pass.

## Spec Change Log

- 2026-08-29, review loop 2: Review found that Release/package validation built and ran only the UI consumer while the upgraded publishable AppHost topology was booted only in Debug/source mode. Amended the Code Map, tasks, Design Notes, and Verification to restore, build, and execute the opt-in AppHost test host in `Configuration=Release` with `UseNuGetDeps=true`, proving the package-backed topology reaches the same six-service boot contract. Avoid the known-bad state where package references compile but fail when the AppHost constructs or starts. KEEP: every review-loop-1 constraint and result; SDK/catalog/AppHost authority; explicit NuGet-backed Aspire mode and exact warning suppression; historical/current guidance split; four root-only fast-forward gitlinks and nested-pointer coherence; focused Debug tests; Release/package UI coverage; transparent formatter and Dapr-probe limitations.
- 2026-08-29, review loop 1: Review found that the exception validator's workspace-wide mode cannot pass from this consumer checkout, the documented `dotnet test` form is incompatible with its Microsoft Testing Platform executables, and the umbrella baseline gate stops on unrelated pre-existing formatting drift. Amended the Code Map, tasks, Design Notes, and Verification to use inventory-only exception validation, direct executable test commands, an explicit NuGet-backed Aspire mode with exact `ASPIRE010` suppression, focused Debug/Release builds, and transparent recording of the unrelated formatter and optional Dapr-probe limitations. Added fetched-ref and nested-pointer coherence evidence to the submodule checks. Avoid the known-bad state of claiming that `-WorkspaceRoot .`, project-level `dotnet test`, or the entire baseline script passes in this checkout. KEEP: SDK `10.0.400` with `latestPatch`; governed Aspire `13.5.3`; stable-first catalog decisions; current root-only fast-forward gitlinks; active guidance synchronization; exact authority relationships; Debug/source and Release/package builds; direct integration, UI, and isolated AppHost boot coverage.

## Design Notes

The package catalog belongs to Hexalith.Builds, not Folders. Its latest commit already carries the audited `13.5.3` Aspire family, Dapr `1.18.5`, Fluent UI V5 RC5, MCP `2.2.0`, xUnit v3 `4.0.0`, and other accepted versions. This task consumes that authority and fixes only Folders-owned skew; it does not duplicate or override catalog rows. The exception validator must run in inventory-only mode here because its optional workspace mode expects every sibling owner named by the shared inventory to exist in the consumer checkout.

Aspire 13.5.3 defaults `AspireUseCliBundle` to `false` in its SDK targets but emits `ASPIRE010` to encourage migration. Declare `false` explicitly so imported-property drift cannot change the chosen NuGet-backed mode, and suppress the exact documented reminder through a semicolon-delimited `NoWarn` token; do not weaken any other warning. This matches the freshly selected EventStore precedent.

Fresh root-only fetches completed before implementation. Four fetched `origin/main` refs advanced and are fast-forward reachable from the baseline gitlinks: EventStore `62d28510f3c11904b6b2ce22edc075d55878924b` to `b34f252c6fbd103acd0e45168ff238a6badd726c`; FrontComposer `66c5a17104a9d58a4e6933705709d688854bc48a` to `85216682495f8cae26cd0883e2e84a538450af4a`; Memories `e0ecafe6560b2961d8c2b85bdeedff4b697122e8` to `7b3f29ce447275de28d643ca903df5d2d8a68865`; Tenants `eb965727329c7d7335be4cd341db4e2f9bf57b56` to `70027ecb39aa0e16d81a8e0f7e275fba1db05147`. AI.Tools, Builds, Commons, and PolymorphicSerializations remain unchanged. EventStore's selected commit records nested FrontComposer object `85216682495f8cae26cd0883e2e84a538450af4a`, matching the selected root gitlink without initializing the nested worktree.

Microsoft Testing Platform project-level `dotnet test` is not a valid runner shape in this checkout. Build first, then execute the generated xUnit v3 test hosts directly. Run the opt-in AppHost host once from the Debug/source build and again from a separately restored and built Release test project with `UseNuGetDeps=true`; each mode must record its environment switch and six-service boot result. The two existing Dapr HTTP probes remain environment-limited when Aspire does not expose hidden sidecar endpoints. The umbrella baseline script is diagnostic evidence only for this change because it stops at pre-existing whitespace in an unchanged source file; run and report the focused build/test and Release/package commands independently instead of claiming that umbrella gate is green.

## Verification

**Commands:**
- `dotnet --version` -- expected: `10.0.400`.
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-consumer-package-authority.ps1 -RepositoryRoot . -CatalogPath references/Hexalith.Builds/Props/Directory.Packages.props` -- expected: valid consumer authority.
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-package-version-exceptions.ps1 -InventoryPath references/Hexalith.Builds/Tools/package-version-exceptions.json -CatalogPath references/Hexalith.Builds/Props/Directory.Packages.props` -- expected: all 15 governed inventory entries valid; omit `-WorkspaceRoot` because sibling owners are intentionally absent.
- `pwsh -NoProfile -File references/Hexalith.Builds/Tools/validate-package-version-audit.ps1 -CatalogPath references/Hexalith.Builds/Props/Directory.Packages.props -AuditPath references/Hexalith.Builds/Tools/package-version-audit.json` -- expected: all 285 pins and 140 families current against the recorded audit source.
- `dotnet package list --project Hexalith.Folders.slnx --outdated` -- expected: no accepted updates.
- `dotnet restore Hexalith.Folders.slnx -m:1 -p:NuGetAudit=false`, then `dotnet build Hexalith.Folders.slnx --no-restore -m:1 --nologo` -- expected: Debug/source build has zero emitted warnings and errors; `ASPIRE010` alone is intentionally suppressed for the explicit NuGet-backed AppHost mode.
- `./tests/Hexalith.Folders.Testing.Tests/bin/Debug/net10.0/Hexalith.Folders.Testing.Tests` and `./tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests` -- expected: direct xUnit v3 hosts pass; do not substitute incompatible project-level `dotnet test`.
- `dotnet restore tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj -p:Configuration=Release -p:UseNuGetDeps=true --force -m:1 -p:NuGetAudit=false`, `dotnet build tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj -c Release -p:UseNuGetDeps=true --no-restore -m:1 --nologo`, and `./tests/Hexalith.Folders.UI.Tests/bin/Release/net10.0/Hexalith.Folders.UI.Tests` -- expected: Release/package consumption has zero emitted warnings/errors and all UI tests pass.
- `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true ./tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests` -- expected: Debug/source AppHost starts all six named services; report the two existing Dapr endpoint probes as skipped if hidden sidecar HTTP endpoints remain unresolved rather than representing them as passes.
- `dotnet restore tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -p:Configuration=Release -p:UseNuGetDeps=true --force -m:1 -p:NuGetAudit=false`, `dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Release -p:UseNuGetDeps=true --no-restore -m:1 --nologo`, and `HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true ./tests/Hexalith.Folders.AppHost.Tests/bin/Release/net10.0/Hexalith.Folders.AppHost.Tests` -- expected: Release/package build emits zero warnings/errors and the package-backed AppHost starts the same six named services; report the two existing Dapr endpoint probes as skipped if hidden sidecar HTTP endpoints remain unresolved.
- `pwsh -NoProfile -File tests/tools/run-baseline-ci-gates.ps1` -- diagnostic only: expected to stop at the unchanged `ProviderOperationSourceResolutionResult.cs` whitespace drift; do not modify that unrelated file or claim the umbrella gate passed.
- Compare all eight root worktree HEADs to the already-fetched local `origin/main` refs, prove every changed baseline gitlink is an ancestor of its selected commit with `git merge-base --is-ancestor`, inspect EventStore's nested FrontComposer object with `git -C references/Hexalith.EventStore ls-tree HEAD references/Hexalith.FrontComposer`, and inspect root-only submodule status without `--recursive` -- expected: four recorded fast-forwards, four no-ops, nested object matches the root FrontComposer commit, and no nested worktree is initialized.
- `git diff --check` -- expected: no whitespace errors in tracked changes.
- `git diff --no-index --check /dev/null _bmad-output/implementation-artifacts/spec-update-dotnet-packages-submodules.md` -- expected: exit `1` with no output because the untracked spec differs from `/dev/null`; any output is a whitespace failure.

## Suggested Review Order

**Intent and toolchain**

- Start with the approved scope, invariants, and validation boundaries.
  [`spec-update-dotnet-packages-submodules.md:12`](spec-update-dotnet-packages-submodules.md#L12)

- Pin stable .NET 10 explicitly and prevent prerelease SDK resolution.
  [`global.json:3`](../../global.json#L3)

- Align Aspire while retaining explicit NuGet-backed orchestration.
  [`Hexalith.Folders.AppHost.csproj:1`](../../src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj#L1)

**Governance and dependency pointers**

- Review the four root-only fast-forwards and their exact selected commits.
  [`Hexalith.EventStore:1`](../../references/Hexalith.EventStore#L1)

- Confirm the independently selected FrontComposer pointer matches EventStore's nested object.
  [`Hexalith.FrontComposer:1`](../../references/Hexalith.FrontComposer#L1)

- Check synchronized effective package and root-submodule guidance.
  [`project-context.md:35`](../project-context.md#L35)

**Verification and follow-up**

- Inspect the configuration contract for SDK, Aspire, warning, and exception authority.
  [`ScaffoldContractTests.cs:290`](../../tests/Hexalith.Folders.Testing.Tests/ScaffoldContractTests.cs#L290)

- Preserve historical SDK evidence separately from current reproduction guidance.
  [`ops-console-accessibility-and-no-mutation-verification.md:242`](../../docs/ux/ops-console-accessibility-and-no-mutation-verification.md#L242)

- Review the two pre-existing AppHost verification gaps recorded for follow-up.
  [`deferred-work.md:2566`](deferred-work.md#L2566)
