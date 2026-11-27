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
