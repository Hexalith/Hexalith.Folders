---
title: 'Resolve five deferred internal callback boundary items'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve five deferred entries in `deferred-work.md` around the Folders internal query and callback boundary:

1. Validate the delegated folder-owner query's empty-object payload, exact entity ID and absent paging before current authorization can return stale/unavailable. Caller verification still precedes shape validation.
2. Return HTTP 400 for the SDK's `InvalidPage` query failure, alongside invalid envelope/cursor.
3. Prove the production metadata route rejects unsupported projection source-guard versions; reuse the existing SDK route.
4. Settle the unverified query-status concern with HTTP tests: successful owner stale/unavailable outcomes retain their payloads; protected denial, missing handlers and other failure details retain the nondisclosing 404 response.
5. Exercise actual production callback authorization requests for `/process`, `/project`, `/query`, and `/admin/operational-index-metadata`, including absent credentials and authenticated wrong-operation assertions. Strengthen the archive-principal test's successful row to assert the accepted archive event.

Use the existing production host, signed workload assertion conventions and authorization fixtures. Add focused regressions for the affected behavior, reconcile these five ledger entries, review the diff and run relevant suites. Commit with a validated Conventional Commit message and push main as requested. Preserve pre-existing EventStore edits and the existing local commit. No dependency updates, submodule pointer changes, deployment or public v2 activation.

</frozen-after-approval>

## Implementation Notes

- Investigation: `FolderOwnerReadQueryHandler.ExecuteAsync` validates shape after its first authorization pass; `/query` omits `InvalidPage`. Production metadata delegates to `MapEventStoreDomainProjectionMetadataEndpoint`, whose SDK implementation already rejects versions other than null/1. Query dispatcher failures carry internal diagnostics; the endpoint's existing 404 sanitization is intentional and needs HTTP coverage.
- Relevant files: `src/Hexalith.Folders.Server/Queries/FolderOwnerReadQueryHandler.cs`, `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, Server query/production-composition tests, and `tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs`.
- The owning Folders worktree started clean; only the root-declared EventStore dependency has pre-existing uncommitted edits. Main starts at `d6fda13a` (abbreviation for navigation only); full revisions will be recorded during verification.
- Baseline: `aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` failed with MSB4166 child nodes 13/14 exiting prematurely; `aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj --format Json` confirms no Folders AppHost is running. Focused serialized Debug build is the fallback.
- Base revision: `d6fda13ad625e6485fa75c073a904230480e075b`; selected EventStore gitlink: `995117f9b899a2994e99257091b0ea35cfbe5090`. The EventStore working tree includes the user's modified `RecordingDaprClient.cs` and untracked content-authority source/docs/tests; the Debug verification uses that source tree and does not establish clean packaged CI compatibility.
- Explicit source mode (`-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false`) is required across the graph. The initial implicit-mode Server build failed CS1704 due to mixed Commons.UniqueIds package/source references; no package selection was changed.
- The stronger archive accepted-row assertion initially exposed an invalid `org-a` fixture. Updating its folder seed and dedicated permission provider to the managed tenant makes the row prove an accepted `FolderArchived` event and exactly one append. The archive class passes 26/26.
- Initial focused Server checks passed 62/62. Review corrections add direct handler verification precedence, private failure-field sanitization, successful metadata preservation, and a correctly signed unauthorized workload caller.
- Narrow formatting verification found six pre-existing whitespace diagnostics in the touched endpoint file. Apply formatting only to that file, preserving behavior.
- The unfiltered Integration suite reports 795 total, 794 passed and one failure: `GoldenLifecycleParityTests.GoldenLifecycleAllNineStepsDriveOverRealRestTransportToTransportTerminalClass`, AddFile step expected 202, got 400. `git show HEAD` confirms the unchanged fixture supplies `hashref-a` instead of a content SHA-256 reference and omits the already-required file-version/lock-fence fields. This path does not call either changed query component; retain the broad blocker separately from the passing 26/26 archive class.
- Final unfiltered Server suite: 1150/1150 passed, including all review additions. Both relevant Debug/source test-project builds succeed with zero warnings/errors. Touched-file formatting verification, `git diff --check` and repository-configured commitlint pass. One separate EventStore transport follow-up is recorded in the ledger; none of the five selected Folders entries remains open.

## Verification

- Initial implicit-mode blocker: `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug -m:1 -p:NuGetAudit=false -v:q` failed CS1704 (Commons.UniqueIds package/source collision). The explicit source mode below resolves the environment selection without dependency edits.
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -m:1 -nr:false -v:q`: passed, zero warnings/errors. Its initial explicit-mode build performed restore; fresh checkouts must omit `--no-restore` once.
- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -m:1 -nr:false -v:q`: passed, zero warnings/errors; repeated with `--no-restore` after correcting the archive fixture.
- `dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll`: passed 1150/1150.
- `dotnet tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll -class Hexalith.Folders.IntegrationTests.ArchiveFolderProcessWiringTests`: passed 26/26.
- Broad gate: `dotnet tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll`: 794/795 passed; unchanged AddFile fixture blocker detailed above.
- `DOTNET_PROCESSOR_COUNT=2 dotnet format whitespace Hexalith.Folders.slnx --no-restore --verify-no-changes --include src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs src/Hexalith.Folders.Server/Queries/FolderOwnerReadQueryHandler.cs tests/Hexalith.Folders.Server.Tests/FolderOwnerReadQueryHandlerTests.cs tests/Hexalith.Folders.Server.Tests/CallbackQueryResultHandler.cs tests/Hexalith.Folders.Server.Tests/FoldersCallbackBoundaryTests.cs tests/Hexalith.Folders.IntegrationTests/ArchiveFolderProcessWiringTests.cs tests/Hexalith.Folders.IntegrationTests/RecordingArchivePrincipalEvidenceProvider.cs --verbosity quiet`: passed.
- `node_modules/.bin/commitlint --edit /tmp/folders-five-deferred-commit-message.txt`: passed with the repository's `commitlint.config.mjs`; installed only locked tooling using `npm ci --ignore-scripts --no-audit --no-fund --loglevel=error`.

## Review Triage Log

- Medium, patched: five ledger resolutions were not yet recorded at review time. Each original entry now retains its evidence and adds a dated resolution pointing to the implementation or production HTTP regression.
- Medium, patched: unverified dispatcher tests did not reach the handler's guard. A direct malformed-query handler regression now requires safe denial before shape validation, with zero evidence reads.
- Medium, patched: denied handler results originally carried only an error. Failure regressions now include private payload, projection and metadata sentinels and require their complete removal in the shared 404 result.
- Low, patched: successful HTTP regression results omitted metadata. Allowed/stale/unavailable cases now carry and assert nondefault projection/freshness metadata.
- False as a guard-advertisement requirement: this Folders host has no owner-proof enrollment, and SDK `BindMetadata` deliberately returns no source-guard capabilities. Supported-version positive controls now also inspect the real domain/host response and verify no unowned capability is advertised.
- Medium, patched: validly signed disallowed workload callers were not tested. Each actual callback route now rejects the unauthorized caller before the post-authorization admission probe.
