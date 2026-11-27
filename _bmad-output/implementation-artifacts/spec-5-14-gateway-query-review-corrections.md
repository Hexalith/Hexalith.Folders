---
title: '5.14 Apply Gateway query review corrections'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
baseline_commit: '65a0291b25fb3697fef2f5f949f82486036ae833'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-5-14-serve-folders-reads-through-gateway-query-handlers.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Story 5.14’s first review recorded 20 open corrections to the 17 Gateway reads. Apply those corrections so signed reads preserve REST scope, freshness, and response contracts, and search callers can distinguish a verified end of results from a failed continuation check.

## Boundaries & Constraints

Carry forward the original Story 5.14 scope and the owner’s Q34 partial-page decision. Preserve deny-before-access and the internal owner callback contract. Keep deferred operations, upstream Gateway ingress, and planning manifests outside this patch.

## I/O & Edge-Case Matrix

| Scenario | Input or state | Expected outcome | Failure handling |
| --- | --- | --- | --- |
| Q34 continuation | Raw tail after a full window of visible items plus duplicates | Probe within the remaining two-second budget. Emit a cursor only for a distinct, in-scope hit with visible authoritative hydration. Partial windows skip the probe. | Deadline or source timeout yields `QueryTimeout`. Probe failure yields `ReadModelUnavailable`. An oversized response yields `InvalidEnvelope` at the callback. |
| Q02 projection | Missing epoch at head zero, absent policy in a current checkpoint, or valid checkpoint within five minutes of clock skew | Missing policy or empty stream yields `NotFound`. A current policy uses the committed checkpoint. | A lagging checkpoint or missing epoch at positive head yields `Stale`. Inconsistent evidence fails closed. |
| Signed scope | Folder aggregate, workspace, task, or declared secondary entity | Accept matching exact identifiers. | Reject folder aggregate mismatch and mismatched backing evidence with safe denial. Reject unrelated entity IDs before source access. |
| Provider readiness | Existing binding has profile drift, mismatched scope, or provider failure | Verify scope before provider egress. Preserve the existing safe drift disposition. | Transient, unavailable, and rate-limited outcomes map to the retryable callback sentinel. Legacy callbacks retain HTTP 404 safe denial for that sentinel. |
| Response safety | Schema-bound payload and nested metadata | Emit closed freshness objects, source observation time for bounded freshness, and REST-shaped workspace data. Remove secret-shaped scalars recursively. | Diagnostics carry exception type only. |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Folders.Server/Queries/` contains the typed Gateway pipeline. `FoldersDomainServiceEndpoints.cs` supplies the REST shape and callback mapping. `FolderSensitiveDiagnosticDetector.cs` has JWT and PEM patterns. Keep its broader provider diagnostic rules out of ordinary query values.
- `src/Hexalith.Folders/Queries/` contains the bounded index probe and committed operational reader. Reuse the operational reader’s five-minute skew policy and the current `ProviderReadinessValidationService` binding path.

## Tasks & Acceptance

Paths below are relative to their project directories.

**Execution**

- [x] `Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs` and `Hexalith.Folders.Server/Queries/SearchFolderIndexedFilesGatewayQueryHandler.cs` gate the continuation probe, propagate typed probe failures, and classify oversized response failures as invalid input.
- [x] `Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs` distinguishes stale from missing and unavailable evidence, applies existing clock tolerance, and keeps other current-scope reads fail-closed.
- [x] `Hexalith.Folders.Server/Queries/GatewayQueryExecutionSupport.cs` and `Queries/GatewayQueryHandler.cs` use observed time for `ServedAt`, project four-field freshness, detect PATs, JWTs, private PEMs, authorization headers, and credential-bearing URLs, and validate secondary entities through typed handlers. Wire the selectors in `GetCommitEvidence`, `GetProviderOutcome`, `GetReconciliationStatus`, `GetRepositoryBinding`, `GetProviderBinding`, and `ValidateProviderReadiness` Gateway handlers.
- [x] `Hexalith.Folders.Server/Authentication/VerifiedReceiptEventStoreAuthorizationValidator.cs` rejects public delegation markers and preserves internal delegation.
- [x] `Hexalith.Folders.Server/Queries/GetEffectivePermissionsGatewayQueryHandler.cs`, `GetWorkspaceLockGatewayQueryHandler.cs`, `GetWorkspaceStatusGatewayQueryHandler.cs`, `GetWorkspaceRetryEligibilityGatewayQueryHandler.cs`, and `GetBranchRefPolicyGatewayQueryHandler.cs` align the closed response shape, classification, required projection guards, optional patterns, and retry correlation fallback with REST.
- [x] `Hexalith.Folders.Server.Tests/McpCliGatewayQueryHandlersTests.cs` and `McpCliGatewayQueryFixture.cs` cover signed aggregate and entity scope, secondary evidence, bad ACL cursor, provider failures, freshness, and redaction. `FoldersCallbackBoundaryTests.cs` covers legacy sentinel mapping.
- [x] `Hexalith.Folders.Tests/Queries/ContextSearch/ContextSearchQueryHandlerTests.cs`, `Projections/OperationalStatus/OperationalStatusPersistenceTests.cs`, and `Providers/Readiness/ProviderReadinessValidationServiceTests.cs` cover probe tails and failures, checkpoint states, and existing-binding failures. Use a recording logger provider for context search, indexing status, ACL listing, branch policy, workspace cleanup, lock and status, and provider binding. Assert the exception type is present, the exception argument is null, and the message is absent.
- [x] Update the original spec’s review checklist with verified outcomes. Register any new covered C# files in `scripts/pd10-v2-story-owned-paths.txt` and regenerate the pending conformance manifest.

**Acceptance Criteria**

- Given a signed, authorized read, when issued through `/query`, then its existing projection route returns REST-compatible metadata or the appropriate closed failure.
- Given an invalid or unauthorized scope, when submitted to a public query, then protected reads and egress respect the existing authorization boundaries and the response discloses no protected data.

### Review Findings

_Code review of `65a0291b..b2e41ee4` (38 files) — reviewed 2026-11-28 across all 4 layers (`blind-hunter`, `edge-case-hunter`, `verification-gap`, `acceptance-auditor`)._

- [ ] [Review][Patch] Trim trailing delimiters before `Uri.TryCreate`, split SPA fragment-route (`#/route?key=val`) and semicolon-delimited parameters in `HasCredentialParameter`, and add missing GitLab/cloud credential keys (`private_token`, `job_token`, `auth_token`, `session_token`, `awsaccesskeyid`) in `FolderSensitiveDiagnosticDetector.ContainsCredentialUrl` (`high`, `blind-hunter+edge-case-hunter`) [src/Hexalith.Folders.Server/FolderSensitiveDiagnosticDetector.cs:48-86]
- [ ] [Review][Patch] Sanitize secret-shaped `correlationId` values before emitting them in `GetWorkspaceRetryEligibilityGatewayQueryHandler`, `GetWorkspaceLockGatewayQueryHandler`, and `GetWorkspaceCleanupStatusGatewayQueryHandler` so `Redact` does not strip required `correlationId` from `WorkspaceRetryEligibility` responses (`high`, `blind-hunter+edge-case-hunter`) [src/Hexalith.Folders.Server/Queries/GetWorkspaceRetryEligibilityGatewayQueryHandler.cs:45]
- [ ] [Review][Patch] Add regression tests exercising `EventStoreOperationalStatusReadModel.MatchesCurrentScopeAsync` with a lagging checkpoint (`head > checkpoint.Sequence`) via signed `SearchFolderIndexedFiles` and task-scoped `GetFolderIndexingStatus` (asserting `ReadModelUnavailable` and zero downstream search/bridge calls), plus `head < 0` and missing-checkpoint-under-existing-epoch cases in `OperationalStatusPersistenceTests` (`high`, `verification-gap+blind-hunter`) [src/Hexalith.Folders/Queries/Folders/EventStoreOperationalStatusReadModel.cs:63-76]
- [ ] [Review][Patch] Support multiline/trailing-newline bare `Bearer`/`Basic` credentials, opaque tokens containing `:`/`%`, and `-----BEGIN PGP PRIVATE KEY BLOCK-----` in `FolderSensitiveDiagnosticDetector.AuthorizationHeaderPattern` and `PemPattern` (`medium`, `blind-hunter+edge-case-hunter`) [src/Hexalith.Folders.Server/FolderSensitiveDiagnosticDetector.cs:77-92]
- [ ] [Review][Patch] Observe faults on timed-out `WaitAsync` tasks, classify `TaskCanceledException` wrapping `TimeoutException` as `QueryTimeout`, and log the continuation timeout warning when `remaining <= TimeSpan.Zero` in `ContextSearchQueryHandler.ProbeContinuationAsync` (`medium`, `blind-hunter`) [src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:287-327]
- [ ] [Review][Patch] Add boundary tests exercising `BranchRefPolicyQueryHandler.HandleAsync` directly at `+5 minutes` (`Allowed`) and `+5 minutes + 1 tick` (`ReadModelUnavailable` / `"freshness_observed_in_future"`), plus a signed `/query` case past the 5-minute tolerance (`medium`, `verification-gap+blind-hunter`) [src/Hexalith.Folders/Queries/Folders/BranchRefPolicyQueryHandler.cs:137-140]
- [ ] [Review][Patch] Add a `"budget-exhausted-before-probe"` test case to `ContextSearchQueryHandlerTests.GatewayContinuationFailureDoesNotReturnAPartialSuccess` delaying first-page work past `_gatewaySearchBudget` (2s) and asserting `QueryTimeout` with zero continuation probe requests (`medium`, `verification-gap+blind-hunter`) [src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:287-290]
- [ ] [Review][Patch] Add a signed `/query` test supplying non-null canonical `Freshness.ReasonCode` snapshots to `GetWorkspaceLock` and `GetWorkspaceStatus`, asserting `retryEligibility.freshness` and `providerOutcome.freshness` emit only the 4 closed fields without `reasonCode` (`medium`, `verification-gap`) [src/Hexalith.Folders.Server/Queries/GetWorkspaceLockGatewayQueryHandler.cs:47-56]
- [ ] [Review][Patch] Add a signed `"delegated-owner-query"` `ReadModelUnavailable` test case to `FoldersCallbackBoundaryTests` asserting HTTP 404 `safe-denial` so relaxing `query.CallerAuthority?.Origin == "authenticated-caller"` cannot go undetected (`medium`, `blind-hunter`) [tests/Hexalith.Folders.Server.Tests/FoldersCallbackBoundaryTests.cs:102-145]
- [ ] [Review][Patch] Extend `SignedQueryFailuresLogOnlyTheExceptionType` (or add a companion test) to exercise `ContextSearchQueryHandler`'s hydration catch (`:207`) and continuation-probe catch (`:324`) and assert exception-type-only logging with `exception == null` and no private message (`medium`, `blind-hunter`) [src/Hexalith.Folders/Queries/ContextSearch/ContextSearchQueryHandler.cs:207,324]
- [x] [Review][Defer] Align sibling operational query handlers (`FolderLifecycleStatusQueryHandler`, `WorkspaceLockStatusQueryHandler`, `WorkspaceStatusQueryHandler`, `WorkspaceCleanupStatusQueryHandler`) with `EventStoreOperationalStatusReadModel.ClockSkewTolerance` (5 min) [src/Hexalith.Folders/Queries/Folders/FolderLifecycleStatusQueryHandler.cs:109-111] — deferred: pre-existing across those four untouched handlers (only Q02 `BranchRefPolicyQueryHandler` was in scope of these review corrections). (`medium`, `blind-hunter`)

#### Rejected

- **False / Low (`blind-hunter`, `edge-case-hunter`)** — Nested `retryEligibility.freshness.Stale` (Q23) and `providerOutcome.freshness.Stale` (Q25) bypass `!Stale`: Q23 is `false` because `WorkspaceLockStatusQueryHandler.cs:118-120,142-145,153` reuses the exact top-level `freshness` already verified `!Stale` at `:208` and `GatewayQueryExecutionSupport.Success:196`; Q25 is `low` because production `EventStoreOperationalStatusReadModel.cs:199,205` overwrites `ProviderOutcome.Freshness = snapshot.Freshness` after rejecting stale snapshots, so adding a guard only covers synthetic stubs.
- **False (`blind-hunter`)** — `SearchFolderIndexedFilesGatewayQueryHandler` maps `InputLimitExceeded`/`ValidationFailed` to `Denied`: every branch of `ContextSearchQueryHandler.ValidateC4Bounds` (blank/overlong `QueryText`, out-of-range `Limit`, overlong/malformed `Cursor`) is already rejected upstream by `GatewayQueryExecutionSupport.Deserialize<SearchFolderIndexedFiles>` and `ValidatePaging` before `HandleGatewayAsync` runs.
- **False (`blind-hunter`)** — `EventStoreOperationalStatusReadModel.ReadCurrentCheckpointAsync` turns `selected.IsStale` into `Unavailable` and uses an unchecked `selected?.Value?[key]` indexer: `SharedProjectionEpochCoordinator.ReadManyAsync` (`:2770-2779`) and `ReadSelectedAsync` (`:469-484`) always insert every requested key (with `null` value when absent), so `KeyNotFoundException` cannot occur; throwing `OperationalStatusUnavailableException` on `selected.IsStale` (`:93`) was untouched by this diff and matches the intentional policy across all six read paths (`:335,356,448,467,486`).
- **False / Low (`blind-hunter`, `edge-case-hunter`)** — Missing REST guards in `GetWorkspaceCleanupStatusGatewayQueryHandler` (`FolderId`/`WorkspaceId`) and `GetWorkspaceLockGatewayQueryHandler` (`LockState`, `retry.CurrentState`, `retry.ReasonCode`): `WorkspaceCleanupStatusQueryHandler.cs:43-44,161-166` already guarantees non-whitespace `FolderId`/`WorkspaceId` on `Allowed` (`false`); `retry.ReasonCode` is always a hardcoded non-empty literal at `WorkspaceLockStatusQueryHandler.cs:142-144` (`false`); REST `ToHttpResult` (`FoldersDomainServiceEndpoints.cs:4679`) itself guards only `WorkspaceId`, and production `OperationalStatusFold.cs:294-295` always emits non-empty `LockState`/`WorkspaceState` (`low`, adding guards for synthetic-stub-only state).
- **False (`blind-hunter`)** — `FolderLifecycleStatusQueryHandler` omitted from `SignedQueryFailuresLogOnlyTheExceptionType` and `McpCliGatewayQueryFailureLoggerProvider` missing `IsEnabled`/structured-state checks: `FolderLifecycleStatusQueryHandler` was not among Story 5.14's updated handlers (fixed earlier in DW-339) and the test matches the spec's eight specified handlers; `LoggerFactory` checks `IsEnabled` before invoking provider `Log`; `FormattedLogValues` renders all template placeholders into `formatter(state, exception)`.
- **Low / Spec-edit (`blind-hunter`)** — `OrganizationId` mismatch in `HydrateAsync`, mismatched `EntityId` on Q03/Q14/Q18/Q19, and post-patch verification wording in `spec-5-14-gateway-query-review-corrections.md`: `OrganizationId` checking in `HydrateAsync` was pre-existing (all three newly added identity checks — tenant, folder, fileVersion — are covered); `GatewayQueryExecutionSupport.ReadAsync:146-149` is already exercised with four mismatched-`EntityId` cases including `SecondaryEntity`-overriding `GetProviderBinding` alongside positive `EntityId` tests on all four handlers; editing the spec under review is rejected per review rules.

## Implementation Notes

- All 20 original review corrections have corresponding source changes or regression assertions. The Gateway publishes the projection observation time as `ServedAt` because EventStore uses producer metadata to enforce its caller-supplied age bound.
- Q23 lock retry freshness uses the same closed projection as Q25 provider outcome freshness. The shared search hydration check now verifies every identity field that anchors the hit to the authorized scope.
- The auth, paging, and legacy callback assertions use signed envelopes. Diagnostic logging captures the typed loggers’ arguments before the receiver’s request-level log filter.
- Independent review led to a shared Q02 clock tolerance at the read model and query handler, sanitized Q35 correlation, and expanded recognition of authorization values and credential-bearing URLs. Regression cases exercise the SDK-dispatched validator, pending continuation hydration, mismatched bridge identities, and populated protected patterns. The five pre-existing findings are recorded in [deferred work](deferred-work.md).

## Spec Change Log

## Review Triage Log

| Reviewer | Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| Blind | Q02 rejects allowed clock skew at the handler | high | patch | The reader accepts observations up to five minutes ahead. The handler still compares directly to `clock.UtcNow`. |
| Blind | Initial Q34 work can exceed the continuation budget | medium | defer | The baseline also passed the request token to first-page hydration. This spec preserves the first-page path and bounds the continuation. |
| Blind | Q34 byte estimate differs from callback serialization | medium | defer | The baseline already measured the core estimate with its own JSON options before Gateway redaction. Exact final-wire accounting requires work across the boundary. |
| Blind | Redaction can remove required response data | medium | patch | The production bridge derives `fv-` identifiers from SHA-256. A signed JWT-shaped correlation is reachable in Q35 and the adapter currently emits the unsanitized input. |
| Blind | Public watermarks use short decimal sequence values | medium | defer | The baseline already exposed the sequence as a string. Setting a public opaque watermark representation needs a contract decision. |
| Blind | Q02 can serialize retained raw Git refs | medium | defer | The existing checkpoint mapper copies legacy policy values and the contract has `branch_ref_` constraints. The correction covers empty pattern omission. |
| Blind | Memories source logs a full remote exception | medium | defer | The existing source adapter catches and logs the exception before the covered query handler can receive it. |
| Blind | Signed URL credentials and encoded keys survive | high | patch | The parameter matcher omits Google signature keys and only checks literal parameter names. |
| Blind | Short Basic or Bearer credentials survive | high | patch | The bare scheme matcher imposes an eight-character minimum absent from the authorization token grammar. |
| Blind | Existing signed tests bypass the validator guard | medium | patch | `HasVerifiedCaller` rejects both public delegation markers before layered authorization can invoke the validator. |
| Blind | Scope regression uses an already-guarded workspace field | medium | patch | The test bridge filters tenant, folder, and file version before returning a record. Inject mismatched authoritative rows directly. |
| Verification | Validator guard lacks independent signed evidence | medium | patch | A mutation removing the validator guard survives the matrix because the earlier handler denies the same requests. |
| Verification | Probe deadline test stalls only the source | medium | patch | A pending source exercises its own await. All tested bridge results currently complete synchronously. |
| Verification | Q02 tests only empty protected patterns | medium | patch | The signed fixture seeds an empty array. The populated REST test uses a separate mapper. |
| Verification | Q02 clock-skew test stops at the reader | high | patch | The persistence test uses the read model directly and the signed fixture observes the checkpoint at the current clock. |
| Edge | In-tolerance Q02 observation becomes unavailable | high | patch | The durable reader and the handler use different limits for the same timestamp. |
| Edge | Q35 emits the raw correlation after provider validation sanitizes it | high | patch | The shared query grammar permits periods. The readiness service generates a safe replacement for JWT-shaped correlations, but the adapter currently echoes the input. |
| Edge | Credential values in URL fragments survive | high | patch | The current matcher excludes fragments from its selected credentials check. |

## Verification

- Build the focused Core and Server test projects in Debug using the README source graph. Run signed callbacks, callback boundary, search, operational persistence, provider readiness, and exception logging tests. Run `Pd10ConformanceSetTests` and `git diff --check`.

The Debug source builds for Core and Server passed with zero warnings and errors. The final full Core run passed 2,622 tests with two existing Forgejo environment skips. The final focused Server run passed 53 tests and PD10 passed 21. The final signed 17-handler matrix also passed against Dapr 1.19.1 backed by a disposable SQLite database with 187 persisted rows. The full Server run before the review patches passed 1,602 tests, with 11 explicit cases outside its default selection.

The full topology baseline command `run-with-temp aspire start --non-interactive --apphost src/Hexalith.Folders.AppHost/Hexalith.Folders.AppHost.csproj` exited 2 during the build. Tenants reported `Catalog SDK selection '5.5.1' is not qualified.`
