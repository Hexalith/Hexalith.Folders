---
title: 'Resolve five deferred rejection metadata items'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve five deferred items around metadata-only command results and rejection boundaries:

1. DW-314: Require an exact declared FolderResultCode name in FolderCommandRejected.Create. Reject numeric, padded, composite and case-mismatched spellings; retain all currently declared canonical names.
2. DW-315: Fail closed with the existing bounded MalformedEvidence gateway mapping when a rejected wire result carries zero or multiple events. Preserve single-event mappings and correlation metadata.
3. DW-324: Scan the complete serialized FolderArchived event for forbidden metadata, including its fingerprint, timestamp and future properties, without printing hostile values in assertion failures.
4. DW-325: Reject trailing bare LF and other control characters in whole-value canonical identifier, reference, path, cursor and metadata validation throughout Folders. Tighten full-match regex anchors; preserve charsets, limits, prefix/search patterns and existing explicit trim policies.
5. DW-331: Cover both specialized workspace rejection factories and processor argument mapping through public processing behavior. Prove fixed codes, canonical command/evidence preservation, dropped unsafe evidence, and that envelope actor data does not replace aggregate-sanitized actor metadata.

Reuse existing result mappings and test fixtures. Add focused behavioral regressions, reconcile these five ledger entries with evidence, review the change and run relevant Debug/source checks. Commit with validated Conventional Commits and push main as requested. Preserve the pre-existing EventStore edits. No dependency updates, submodule pointer changes, deployment or public API activation.

</frozen-after-approval>

## Implementation Notes

- Investigation found strict enum parsing already implemented in tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs; reuse the same rules in the production rejection factory. The gateway still calls Events.Single().
- Whole-value regular expressions span 24 Folders production files across domain, Server and Client. Change only whole-value anchors, preserving prefix detectors and code-generation multiline parsers.
- Tests live in Server.Tests (rejection canonicalization/processor), IntegrationTests/Parity (wire collection shape), core Tests (archive serialization and domain validators), and Client.Tests (request metadata validation).
- DW-331 has newer replay/classification tests but still lacks factory and processor argument-mapping coverage. Preserve that historical evidence when resolving it.
- Main baseline: a9f5d9b2a4e265fc45fb362149d1aea7ebbfdc6f. Folders itself started clean; references/Hexalith.EventStore contains pre-existing uncommitted edits, excluded from this task and commit.
- Baseline runtime command: aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj failed with CS1704 duplicate Hexalith.Commons.UniqueIds source/package assemblies (MSB3243 warning), before startup. aspire describe --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj confirmed no running AppHost. Follow the focused validation ladder and retain this broad blocker separately.

- Implemented strict rejection-code names, bounded rejection collection handling, complete archive-event scanning and 51 absolute-anchor corrections. Added behavioral regressions across core, Server, Client and integration tests; the production metadata policies and single-event mappings remain intact.
- The workspace-content fixture supplies an in-memory repository. Processor tests seed its real five-event state, then require the same repository state object and append count after every rejection. Early tests correctly exposed an unseeded fixture; the fixture was fixed without weakening expected outcomes.
- Newtonsoft serialization callbacks wrap JsonSerializationException in TargetInvocationException. The new serialization tests assert both wrapper and inner exception; deserialization directly asserts JsonSerializationException.
- During verification, an external pull fast-forwarded main to 41358c834c19796bc5017cdc1726eff5c7eb5bfe. Its four upstream commits touch only package.json/package-lock.json (commitlint 21.2.4). The Folders source base and this work were preserved. npm ci --ignore-scripts --no-audit --no-fund --loglevel=error installed the updated locked commit-validation tooling without tracked edits.

## Verification

- Debug source builds for Server.Tests, core Tests, IntegrationTests and Client.Tests passed with zero warnings/errors using dotnet build <project> -c Debug -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:NuGetAudit=false -m:1 -nr:false -v:q. Incremental rebuilds used --no-restore. Logs are /tmp/folders-five-rejection-{server,core,integration,client}-build.log.
- dotnet tests/Hexalith.Folders.Tests/bin/Debug/net10.0/Hexalith.Folders.Tests.dll: 2236 passed, 2 skipped (Forgejo operator evidence and isolated smart-HTTP fixture), zero failures.
- dotnet tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests.dll -class '*InProcessRejectionPropagatingGatewayClientTests' -class '*ArchiveFolderProcessWiringTests': 65/65 passed.
- dotnet tests/Hexalith.Folders.Client.Tests/bin/Debug/net10.0/Hexalith.Folders.Client.Tests.dll: 348/348 passed.
- The baseline AppHost startup blocker above remains separate from these passing source/unit/in-process HTTP lanes; no live topology success is claimed.

- dotnet tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests.dll: 1194/1194 passed. A final incremental build/focused run refreshes the matching test-only command-type characterization regex after its anchors were synchronized.
- DOTNET_PROCESSOR_COUNT=2 dotnet format whitespace Hexalith.Folders.slnx --no-restore --verify-no-changes --include <all 39 changed C# paths> --verbosity quiet: passed. The file list was derived from git diff --name-only plus untracked source paths; prefix/search regexes and client Generation sources were excluded from anchor replacement. git diff --check also passed.

- Review follow-ups add a contaminated-fingerprint negative control, all four workspace transition command branches, response date/problem metadata LF cases, URL-encoded audit/readiness query cases, branch/commit reference validation and explicit sanitizer/readiness trimming compatibility. These are test-only additions; production behavior is unchanged from the complete passing suites above. Focused checks cover the final additions.

## Review Triage Log

- Medium, deferred: null wire collections and null event entries can throw in the shared parity gateway. Verified positional wire records permit null and baseline already dereferences Count/Single().Payload without guarding these shapes. This pre-existing test-support defect is outside the selected zero/multiple-event decision and is recorded as one new ledger entry.
- Medium, patched: the full archive scan had no fingerprint negative control. The scan now has a private assertion helper and a contaminated-fingerprint case that fails when fingerprint is omitted, while requiring the assertion message to omit its value.
- Medium, patched: processor coverage omitted PrepareWorkspace and CommitWorkspace transition-selection predicates. Both now join the existing lock/release matrix through public processing with canonical evidence and unchanged persisted repository state.
- Medium, patched: response date/problem metadata anchors lacked LF regressions. Added response deserialization tests for observedAt, code and correlationId, with canonical Z/+00:00/-00:00 positive controls.
- False as a rejection requirement: FolderHttpHeaderReader explicitly trims decoded query values before validating them, so trailing LF never reaches audit/readiness regexes. An HTTP 400/zero-read expectation would change the preserved trimming policy. FolderHttpHeaderReaderTests now characterizes outer trimming and embedded-control rejection; no production reader change was needed.
- Low, patched: branch/commit reference tests omitted LF. Added independently valid command controls and LF variants for default/allowed/protected branches, author/target/digest/classification, commit references and reconciliation references.
- Low, patched: explicit trimming compatibility was unpinned. Audit identifier/category tests preserve outer whitespace trimming and reject embedded LF; a readiness handler regression preserves its trimmed correlation. Rejection factory tests retain their distinct no-trim policy.

- HTTP query review correction: the new percent-encoded-LF rejection expectations were invalid. FolderHttpHeaderReader.FirstNonEmpty explicitly trims before checking control characters, so audit/readiness regexes never receive a trailing LF. Those rejection assertions were removed; FolderHttpHeaderReaderTests now pins decoded-query outer trimming and embedded-LF rejection. The no-trim rejection/stream/identifier boundaries retain their LF rejection tests. No production trimming behavior changed.
- Review verdict correction for query-gate finding: false as a requirement for HTTP 400/zero reads on trailing LF; the existing reader deliberately canonicalizes those requests. Direct decoded-query characterization and existing HTTP routes establish the intended boundary. The earlier proposed rejection test entry records the investigation, not the final behavior.
- The new commit transition row initially omitted required taskId/auditMetadataKeys and reached malformed JSON handling. Its payload now supplies both fields so the test reaches the intended transition rejection.

- Final review additions: core focused classes passed 173/173 (archive scanning, branch policy, commit metadata/outcomes, audit sanitizer and readiness query); Client FileUploadConvenienceTests/ClientGenerationTests passed 87/87, including valid UTC spellings and rejected LF response metadata. Rebuilds passed with zero warnings/errors. Final server processing/header/identifier/archive regressions and final changed-file whitespace verification are recorded below after completion.

- Final server regressions passed 140/140 after the complete commit payload was applied directly. The final test-only build reused already-built project references with -p:BuildProjectReferences=false, -c Debug --no-restore and the same source-mode flags; it passed with zero warnings/errors. Full source/dependency builds and the complete 1194-test server lane had already passed earlier; the final additions required only affected-class reruns.
- Final whitespace verification passed for all 45 changed C# files, followed by a focused --verify-no-changes check on the corrected processor test payload. git diff --check and repository commitlint 21.2.4 validation of /tmp/folders-five-rejection-commit-message.txt passed.
- Five original ledger entries are resolved: DW-314, DW-315, DW-324, DW-325 and DW-331. One pre-existing null-wire test-support issue is deferred with evidence. EventStore's pre-existing changes are excluded from the commit.
