---
title: '11.4 consolidate server transport envelope and route helper duplication'
type: 'refactor'
created: '2026-09-08'
status: 'done'
baseline_commit: 'aa0b76cf39e11aed5b4299fd4c7a1d3435642faa'
route: 'dispatch'
review_loop_iteration: 0
story_key: '11-4-consolidate-server-transport-envelope-and-route-helper-dupli'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-11-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Four Server endpoint files copy SafeProblem, header/query readers, canonical-id checks, error-status mapping, and a secret-filter. Copies drifted: two ID grammars, and OpsConsole's filter is a weaker subset.

**Approach:** Extract shared Server helpers and table-driven Domain status mapping. Rewire only those four files. Close OpsConsole secret-filter drift. Leave REST wire unchanged.

## Decisions

- Keep both ID grammars: segment `^[a-z0-9._-]+$` / 128 and path `^[A-Za-z0-9][A-Za-z0-9_-]{0,255}$` / 256.
- Shared detector equals ProviderReadiness HTTP `IsSensitiveDiagnosticValue`. Do not adopt `FolderAuditSanitizer`.
- Domain `FolderResultCode` uses `FolderCanonicalErrorMapper`. Audit/OpsConsole keep `projection_stale` → 409 (not mapper 503).
- New helpers in `Hexalith.Folders.Server`, one type per file. Do not split the 37-route file.
- Keep typed success `ToHttpResult`. Share ProblemDetails, readers, IDs, detector, and Domain error status lookup.
- Leave dirty Builds/FrontComposer gitlinks and uncommitted `epic-11-context.md` untouched. Flip only the `11-4-*` key.
- Deferred: `/process` segment-ID copies and shared `JsonSerializerOptions`.

## Boundaries & Constraints

**Always:** Preserve routes, `.WithName`, envelopes, ProblemDetails extras, status codes (OpsConsole redaction matches Provider HTTP), 49-op parity, generated clients, metadata-only diagnostics.

**Never:** Edit OpenAPI, `parity-contract.yaml`, or `Client/Generated/**`. Do not touch `FolderDomainProcessor`, `FoldersDomainServiceRequestHandler`, Domain sanitizers, EventStore admission, 11.5–11.7 / 11.16 / 11.17, CI gates, or submodule SHAs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Readers | Same header/query names | First non-empty safe value; reject CR/LF/control | Invalid header not echoed |
| Segment ID | `folder_1` vs `Folder_1` / 129 chars | Accept lowercase-safe ≤128; reject case/length/charset | 400 `validation_error` |
| Path ID | Mixed-case ≤256 vs invalid | Provider/OpsConsole keep 256 grammar | Same 400/404 as today |
| Secret filter | `repo_`, `repository`, `diff --git`, `installation` on OpsConsole | Sensitive, matching Provider HTTP | Correlation → `correlation_{guid}` |
| ProblemDetails | Existing error codes | Same core extensions plus extras (`taskId`, `evidenceSource`, `todoRef`, `retryAfterSeconds`) | No gateway leak |
| Stale map | `projection_stale` | Domain mapper 503; Audit/OpsConsole 409 | Per-surface titles/messages |
| Wire count | 49 REST ops | Count, paths, names unchanged | Halt if registration tests fail |

</frozen-after-approval>

## Code Map

- `FoldersDomainServiceEndpoints.cs` — `SafeProblem` :5576, readers :6067+, segment ID :3557. Rewire only; keep typed success mappers.
- `AuditEndpoints.cs` — `SafeProblem` :547 (`evidenceSource`/`todoRef`); segment ID :679; stale 409.
- `ProviderReadinessEndpoints.cs` — detector source :717; path ID :782.
- `OpsConsoleDiagnosticsEndpoints.cs` — weaker detector :562; path ID :512; stale 409.
- `FolderCanonicalErrorMapper.cs` — Domain table; do not change 409/503 values.
- `FoldersServerModule.cs` — `MaxCanonicalIdentifierLength = 128`; keep route constants.
- New internal types (one file each): header/query reader, segment ID, path ID, sensitive detector, ProblemDetails factory.
- Leave: `FolderDomainProcessor.cs`, `FoldersDomainServiceRequestHandler.cs`, OpenAPI, `parity-contract.yaml`, `Client/Generated/**`.
- Green (DW-341 assembly `-class`): `TransportParityConformanceTests` (49), `ServerEndpointRegistrationTests`, `MutationEnvelopeEndpointMatrixTests`, `AuditEndpointsTests`, `ProviderReadinessEndpointTests`, `OpsConsoleDiagnosticsEndpointTests`, `FolderCanonicalErrorMapperTests`, plus new helper tests.
- `sprint-status.yaml` — `11-4-consolidate-server-transport-envelope-and-route-helper-dupli` only.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Folders.Server/FolderHttpHeaderReader.cs` (+ segment ID, path ID, detector, ProblemDetails factory files) -- add internal helpers matching current endpoint behavior.
- [x] `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs`, `AuditEndpoints.cs`, `ProviderReadinessEndpoints.cs`, `OpsConsoleDiagnosticsEndpoints.cs` -- delete private copies; call shared types; keep typed success mappers and extras.
- [x] `src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs` -- Domain `FolderResultCode` table only; do not retarget Audit/OpsConsole stale mapping.
- [x] `tests/Hexalith.Folders.Server.Tests/FolderSensitiveDiagnosticDetectorTests.cs` (+ ID and ProblemDetails factory tests) -- cover the I/O matrix, including OpsConsole-missing tokens.
- [x] `_bmad-output/implementation-artifacts/sprint-status.yaml` -- set `11-4-consolidate-server-transport-envelope-and-route-helper-dupli` to `in-progress` only.

**Acceptance Criteria:**
- Given the four endpoint files each define SafeProblem, readers, and ID/detector helpers, when this story completes, then those private copies are gone and callers use the shared Server types.
- Given OpsConsole omitted `repository` / `repo_` / `diff --git` / `providerpayload` / `installation`, when the shared detector is used, then those values are sensitive on OpsConsole as on Provider HTTP.
- Given Audit/OpsConsole map `projection_stale` to 409 and Domain REST uses the mapper (503), when status mapping is shared, then those codes and ProblemDetails extras stay the same.
- Given OpenAPI, parity fixtures, generated clients, and 49 `/api/v1` operations exist, when helpers are extracted, then those artifacts and counts are unchanged.

## Implementation Notes

- Shared types added: `FolderHttpHeaderReader`, `FolderCanonicalSegmentIdentifier`, `FolderCanonicalPathIdentifier`, `FolderSensitiveDiagnosticDetector`, `FolderProblemDetailsFactory`. Four endpoint files call them; typed success `ToHttpResult` methods remain. `ClientTenantIds` / `ClientPrincipalIds` copies remain (not in the extraction list).
- `FolderCanonicalErrorMapper.ForDomain` added; table values unchanged. Gateway mapping uses `StatusFor` only when the gateway status already matches. Audit/OpsConsole stale stays 409.
- Verification (DW-341 assembly `-class`): TransportParity 10, Registration 5, MutationEnvelope 13, Audit 22, ProviderReadiness 23, OpsConsole 43, ErrorMapper 21, Detector 20, Identifiers 18, HeaderReader 6, ProblemDetails 5 — all 0 failed. Spec `rg` clean. OpenAPI/parity/Generated diff empty.
- Left untouched: `FolderDomainProcessor`, `FoldersDomainServiceRequestHandler`, Domain sanitizers. Dirty gitlinks were committed on `472c690` by a concurrent workspace commit, not this story's file list.

## Spec Change Log

## Review Triage Log

- `false` — Blind: patch moves Builds/EventStore/FrontComposer SHAs. Those gitlinks are in `472c690` (jpiquot), not in the 11.4 Server file list. Working-tree FrontComposer remains dirty and unstaged by this story.
- `false` — Blind: hygiene rewrote `epic-11-context.md`. Same `472c690` concurrent commit; 11.4 implementation did not edit that file.
- `false` — Blind: Story 10.8/10.9 artifacts mixed into the baseline diff. Concurrent `472c690` plus an untracked 10.9 spec; not authored by the 11.4 helper extract.
- `low` — Blind: `FolderCanonicalErrorMapper.ForDomain` is unused on live query `ToHttpResult` paths. Those switches are not `FolderResultCode`; gateway already uses `StatusFor`/`RetryableFor`. Wiring every query mapper through `ForDomain` is more than a direct correction.
- `false` — Blind: `StatusFor` has no `folder_acl_denied` arm and the Story 8.3 comment left `ToArchiveGatewayProblem`. Default 403 still matches 403 + `folder_acl_denied` (same as the deleted explicit arm). The Story 8.3 comment remains on `SafeGatewayReasonCode`.
- `low` — Blind: gateway comment mentions Audit/OpsConsole 409. Comment-only; those surfaces never call this method.
- `medium` — Blind: OpsConsole AC tokens are unit-tested only. Real: HTTP suites still use pre-change `secret-token-aaaa` / `_invalid`. Grouped with the verification-gap finding.
- `medium` — Blind: identifier tests omit `IsSafeDiagnosticId` / `SanitizeCorrelationId`. Same missing HTTP lock as the row above. Query CR/LF is already rejected by shared `FirstNonEmpty` covered by `ReadHeaderShouldRejectControlCharactersWithoutEchoingThem`.
- `false` — Blind: `DomainTitleFor` lacks 410/429 and `DomainMessageFor` omits some gateway categories. Copied from the previous Domain `SafeProblem`; the extract did not change those titles/messages.
- `false` — Blind: empty Verification/Change/Triage logs and notes vs `references` diff. Fix would edit this spec. Workflow status `in-review` vs sprint `in-progress` is expected mid-review.
- `false` — Blind: Code Map still cites pre-extract line numbers. Fix would edit this spec.
- `false` — Blind: spec `in-review` vs sprint `in-progress` vs notes. Workflow-normal; not a runtime defect.
- `maybe-false` — Edge: 403 + `commit_failed`/`provider_failure_known` now hits `StatusFor` default 403 and echoes those codes instead of `denied_safe`. The 422 arms are unchanged. Unsettled whether any gateway emits 403 with those reasons.
- `maybe-false` — Edge: same 403 pairing claimed as a REST wire change. Same evidence as the previous row.
- `false` — Edge: `10-8-…` flipped to `done` in `sprint-status.yaml`. That flip is in `472c690`, not the 11.4-only key edit.
- `medium` — VG: OpsConsole HTTP gates never observe the newly shared tokens. Pre-verified: `IsSensitive` / `IsSafeDiagnosticId` are used at `OpsConsoleDiagnosticsEndpoints.cs:309-345` and `:452-456`, but `DiagnosticsShouldRejectSensitiveCorrelationId` / `DiagnosticsShouldRejectNonCanonicalPathId` still use values the old filter already rejected.

## Verification

**Commands:**
- `git diff -- src/Hexalith.Folders.Contracts/openapi tests/fixtures/parity-contract.yaml src/Hexalith.Folders.Client/Generated references` -- expected: empty (leave dirty gitlinks unstaged).
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --configuration Debug` -- expected: 0 warnings/errors.
- `./tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -noLogo -noColor -class Hexalith.Folders.Server.Tests.TransportParityConformanceTests` -- expected: green (DW-341; repeat `-class` for the Code Map suites).
- `rg -n "private static (IResult SafeProblem|string\\? ReadHeader|bool IsCanonicalIdentifier|bool IsSensitiveDiagnosticValue)" src/Hexalith.Folders.Server/{FoldersDomainServiceEndpoints,AuditEndpoints,ProviderReadinessEndpoints,OpsConsoleDiagnosticsEndpoints}.cs` -- expected: no matches.
