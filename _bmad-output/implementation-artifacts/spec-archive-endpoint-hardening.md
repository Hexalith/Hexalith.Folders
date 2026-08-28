---
title: 'Harden archive endpoint regression coverage'
type: 'refactor'
created: '2026-08-28'
status: 'done'
baseline_revision: '61ed333cbcdcd57b495319a1c5c8db446b32704c'
baseline_commit: '61ed333cbcdcd57b495319a1c5c8db446b32704c'
review_loop_iteration: 0
followup_review_recommended: false
context:
  - '_bmad-output/project-context.md'
warnings:
  - multiple-goals
deferred: []
---

<intent-contract>

## Intent

**Problem:** Archive endpoint regression coverage samples only part of the gateway 5xx range and does not prove that hostile gateway-returned correlation identifiers are discarded before response-header reflection. These gaps leave the safe-unavailable catch-all and header-injection boundary vulnerable to unnoticed regression.

**Approach:** Extend the existing in-process archive endpoint theories with representative 5xx statuses and hostile gateway correlation values, asserting only the caller-visible HTTP response, metadata-only problem shape, gateway invocation, and safe correlation fallback.

## Boundaries & Constraints

**Always:** Keep the change test-only in `ArchiveFolderEndpointTests`; exercise the real mapped HTTP endpoint and its recording gateway double; retain the canonical caller correlation ID as the fallback; assert ordinal response values and metadata-only failure fields; cover 503, 505, 507, and 599 plus unsafe-character, oversized, CR/LF, and other control-character gateway identifiers.

**Block If:** The named endpoint tests or production safety arms no longer exist, or satisfying the requested behavior requires changing the public response contract or production implementation.

**Never:** Edit `_bmad-output/implementation-artifacts/deferred-work.md` or any other deferred-work ledger; weaken `GatewayCorrelationRegex`, the 128-character limit, response redaction, or the 5xx catch-all; reflect a hostile gateway value in a response header or body; broaden the work to other endpoints.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Representative gateway failures | Gateway exception status 503, 505, 507, or 599 with internal detail | HTTP 503; `read_model_unavailable` / `evidence_unavailable`; `retryable=true`; one gateway request | Internal detail is absent from the response |
| Unsafe gateway correlation | Successful gateway result with a space or other regex-invalid character in its correlation ID | HTTP 202; `X-Correlation-Id` and response `correlationId` use caller value `correlation-a`; one gateway request | Hostile value is absent from response headers and body |
| Oversized gateway correlation | Successful gateway result with 129 characters | Same safe caller-ID fallback and accepted response | Oversized value is not reflected |
| Header-injection/control input | Successful gateway result containing CR, LF, CR/LF, or a non-CR/LF control character | Same safe caller-ID fallback and accepted response | Control-bearing value is not reflected and does not affect headers |

</intent-contract>

## Code Map

- `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs:427` -- Existing `ArchiveFolderEndpointShouldMapGatewayServerErrorsToSafeUnavailable` theory owns the 5xx regression samples; extend its inline data and add the hostile-correlation theory beside it. Reuse `StartAppAsync`, `CreateValidArchiveRequest`, and `RecordingEventStoreGatewayClient`; this is the only implementation file to edit.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:62` -- Read-only evidence: `GatewayCorrelationRegex` permits only ASCII letters, digits, dot, underscore, and hyphen.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:1535` -- Read-only evidence: accepted gateway correlations are revalidated before `X-Correlation-Id` reflection and otherwise fall back to the request correlation.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3549` -- Read-only evidence: `IsSafeGatewayCorrelationId` combines the regex with `MaxCanonicalIdentifierLength`.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3639` -- Read-only evidence: gateway exception correlations receive the same safety check before entering Problem Details.
- `src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3892` -- Read-only evidence: the catch-all maps every gateway status from 500 through 599 to safe HTTP 503 unavailable metadata.
- `src/Hexalith.Folders.Server/FoldersServerModule.cs:61` -- Read-only evidence: maximum canonical identifier length is 128.

## Tasks & Acceptance

**Execution:**
- [x] `tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs` -- add 503/505/507/599 cases to the existing server-error theory and add a hostile gateway-correlation theory covering regex-invalid, 129-character, CR, LF, CR/LF, and control-character values; assert safe caller correlation in both header and JSON, absence of the hostile value, accepted/unavailable canonical shapes as applicable, and exactly one gateway submission.

**Acceptance Criteria:**
- Given the archive endpoint receives an `EventStoreGatewayException` with status 503, 505, 507, or 599, when the request completes, then the HTTP surface returns 503 with category `read_model_unavailable`, code `evidence_unavailable`, `retryable=true`, no internal detail, and exactly one gateway request.
- Given the archive gateway returns an unsafe, oversized, CR/LF-bearing, or other control-character correlation identifier after a valid caller request, when the archive endpoint builds its accepted response, then the HTTP surface returns 202 and reflects only `correlation-a` in `X-Correlation-Id` and the JSON `correlationId`, with the hostile value absent and exactly one gateway request.
- Given the focused archive endpoint test class and full server test project, when verification runs, then all tests pass without production-source or deferred-ledger changes.

## Spec Change Log

## Review Triage Log

### 2026-08-28 — Review pass
- intent_gap: 0
- bad_spec: 0
- patch: 2: (high 0, medium 0, low 2)
- defer: 0
- reject: 18: (high 0, medium 4, low 14)
- addressed_findings:
  - `[low]` `[patch]` Disposed the hostile-correlation theory's `HttpResponseMessage` deterministically for every data row.
  - `[low]` `[patch]` Corrected the stale archive server-error theory line anchor in the Code Map.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- expected: Debug build succeeds with zero warnings and errors.
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.ArchiveFolderEndpointTests` -- expected: every archive endpoint test passes.
- `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- expected: the complete server test project passes.
- `git diff --check` -- expected: no whitespace errors.

## Auto Run Result

Summary: Extended archive endpoint regression coverage for the requested gateway 5xx statuses and hostile gateway-returned correlation identifiers. The endpoint now has executable evidence that representative 5xx responses remain canonical and metadata-only, while unsafe gateway correlation values fall back to the caller's safe identifier before header and body reflection.

Files changed:
- `../../tests/Hexalith.Folders.Server.Tests/ArchiveFolderEndpointTests.cs` -- added four gateway 5xx theory rows, six hostile-correlation cases, canonical response assertions, gateway invocation assertions, and deterministic response disposal.
- `spec-archive-endpoint-hardening.md` -- recorded the plan, baseline, review triage, verification, and completion result.

Review findings breakdown: 2 low patches applied, 0 items deferred, and 18 reviewer suggestions rejected after deduplication because they exceeded the verbatim ledger scope, duplicated direct parsed-field assertions, targeted a non-reflecting error surface, or evaluated an in-progress workflow artifact before finalization.

Follow-up review recommendation: `false`. Patched findings: high 0, medium 0, low 2; score `3 × 0 + 1 × 2 = 2`, below the threshold of 5.

Verification performed:
- `dotnet build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- passed with 0 warnings and 0 errors.
- `tests/Hexalith.Folders.Server.Tests/bin/Debug/net10.0/Hexalith.Folders.Server.Tests -parallel none -noLogo -class Hexalith.Folders.Server.Tests.ArchiveFolderEndpointTests` -- passed 35/35, with every I/O matrix row executed.
- `dotnet test tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --no-restore` -- passed 575/575 with 0 skipped and 0 failed.
- `git diff --check` -- passed.

Residual risks: Coverage intentionally samples the requested 5xx and hostile-identifier classes rather than exhaustively enumerating every status or character. Production behavior and the deferred-work ledger were not changed.
