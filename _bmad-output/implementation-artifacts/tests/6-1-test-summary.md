# Test Automation Summary — Story 6.1 (Audit and Operation-Timeline Query Endpoints)

**Story:** `6-1-audit-and-operation-timeline-query-endpoints.md` (status: review)
**Date:** 2026-05-28
**Framework:** xUnit v3 + Shouldly (existing project conventions)
**Workflow:** `bmad-qa-generate-e2e-tests` (auto-apply mode)

## Scope

QA gap-fill pass on the audit-family REST endpoints (`ListAuditTrail`, `GetAuditRecord`, `ListOperationTimeline`, `GetOperationTimelineEntry`). The dev shipped a consolidated `AuditEndpointsTests.cs` that covers happy paths plus core conformance, but three dedicated test files explicitly called for by ACs #7 / #8 / #9 were absent. This pass authors the three missing files with the per-AC scenario depth the story specifies.

## Files Authored

| File | Role | Test Count |
|---|---|---|
| `tests/Hexalith.Folders.Server.Tests/AuditEndpointsAuthorizationOrderTests.cs` | AC #9 — authorization-before-observation at HTTP layer | 9 |
| `tests/Hexalith.Folders.Server.Tests/AuditRedactionConsistencyTests.cs` | AC #7 — byte-for-byte single-vs-list redaction invariant | 2 |
| `tests/Hexalith.Folders.Server.Tests/AuditEndpointsSentinelTests.cs` | AC #8 — per-channel sentinel sweep across the leakage corpus | 13 (12 theory rows + 1 channel-inventory check) |

## Gaps Closed

### AC #9 — Authorization-before-observation (HTTP layer)

The pre-existing `AuditEndpointsTests.cs` only covered the unauthenticated scenario (`UnauthenticatedListAuditTrailDoesNotConsultReadModel`). The story explicitly required a dedicated test class with the full denial-layer matrix proving the read-model is **never** consulted on any authorization-class denial.

New scenarios:

1. `ListAuditTrailWithTenantAccessDeniedMustNotConsultReadModel` — tenant store empty → `GetCount == 0`, 403/404 safe-denial.
2. `ListAuditTrailWithFolderAclDeniedMustNotConsultReadModel` — `DenyingFolderPermissionEvidenceProvider` → `GetCount == 0`.
3. `ListAuditTrailWithMissingClaimTransformEvidenceMustNotConsultReadModel` — `MissingClaimTransformEvidenceAccessor` → `GetCount == 0`.
4. `GetAuditRecordWithFolderAclDeniedMustNotConsultReadModel` — record endpoint short-circuits at ACL layer.
5. `ListOperationTimelineWithTenantAccessDeniedMustNotConsultReadModel` — timeline endpoint short-circuits at tenant layer.
6. `GetOperationTimelineEntryWithFolderAclDeniedMustNotConsultReadModel` — timeline-entry endpoint short-circuits at ACL layer.
7. `ListAuditTrailWithStaleProjectionSurfacesProjectionStale409` — `AuditTrailReadModelResult.Stale` → HTTP 409 + canonical `projection_stale` / retryable.
8. `ListAuditTrailWithUnavailableProjectionSurfacesProjectionUnavailable503` — `Unavailable` → HTTP 503 + canonical `projection_unavailable` / retryable.
9. `ListAuditTrailReadModelExceptionLogsMetadataOnly` — exception message carries sentinel; body must surface `read_model_unavailable` and **not** leak the message text or exception type.

### AC #7 — Single-vs-list redaction byte-for-byte invariant

The pre-existing `SingleAndListResponsesMustCarryByteForByteIdenticalRedactionState` only tested a single record. The story requires a 3-record seeded corpus mixing visibility states (`visible` / `redacted`) with the invariant proven over the full corpus for both audit and timeline pairs.

New scenarios:

1. `EveryAuditRecordRedactionStateIsByteForByteIdenticalBetweenListAndSingleEndpoints` — 3-record corpus; per-record `GetRawText()` comparison on `redaction`, `actorReference`, `operationId`, `evidenceTimestamp` between the entry from `entries[]` and the single-record response.
2. `EveryOperationTimelineEntryRedactionStateIsByteForByteIdenticalBetweenListAndSingleEndpoints` — same pattern on timeline entries: `workspaceReference`, `stateTransition`, `sanitizedResult`.

### AC #8 — Sentinel sweep per channel

The pre-existing inline sentinel test in `AuditEndpointsTests.cs` covered 6 endpoint paths against the corpus, but only the response body — not response headers — and was a single `[Fact]` rather than a per-path theory. The story requires per-channel iteration with the inventory enrollment check.

New scenarios:

1. `EveryAuditEndpointResponseChannelMustBeSentinelClean` — `[Theory]` over 12 paths × N sentinels from `audit-leakage-corpus.json`, sweeping both response body **and** all response headers. Each violation reports the offending sentinel (truncated to 32 chars) and the failing channel.
2. `SafetyChannelInventoryEnrollsEveryAuditFamilyOperation` — concern #6 enforcement: confirms `safety-channel-inventory.json` enumerates all four operation IDs as enrolled channels (no surface gets a free pass).

## Test Run Results

```text
# Build (Windows SDK via WSL, per repo build env policy)
/mnt/c/Program\ Files/dotnet/dotnet.exe build tests/Hexalith.Folders.Server.Tests/Hexalith.Folders.Server.Tests.csproj --nologo
Build succeeded. 0 Warning(s) / 0 Error(s).

# Focused audit suite
dotnet.exe test tests/Hexalith.Folders.Server.Tests --no-build --filter "FullyQualifiedName~Audit"
Passed!  - Failed: 0, Passed: 51, Skipped: 0, Total: 51

# Full Hexalith.Folders.Server.Tests
dotnet.exe test tests/Hexalith.Folders.Server.Tests --no-build
Failed!  - Failed: 1, Passed: 414, Skipped: 0, Total: 415
```

The single remaining failure (`BranchRefPolicyEndpointTests.GetBranchRefPolicyShouldUseSafeDenialEnvelopeForTenantMismatch`) is the documented Epic 5 retro carry-over red, explicitly out of scope for Story 6.1 per Dev Notes. **No new regressions introduced.**

Audit-test count progression: 27 → 51 (+24 new tests).

## Validation Against Checklist (`.agents/skills/bmad-qa-generate-e2e-tests/checklist.md`)

- [x] API tests generated — three new API/integration test classes.
- [x] E2E tests generated — in-process host on `127.0.0.1:0` drives full request → response cycles.
- [x] Tests use standard test framework APIs — xUnit v3 `[Fact]` / `[Theory]` / `MemberData`; Shouldly assertions.
- [x] Tests cover happy path — projection-stale / projection-unavailable / read-model-exception success-after-auth scenarios.
- [x] Tests cover critical error cases — every authorization-class denial path; sentinel-leak path; redaction-state-mismatch path.
- [x] All generated tests run successfully — 24/24 new tests green.
- [x] Tests use proper locators — semantic JSON property names (`GetProperty("category")`, etc.); no string-index hacks.
- [x] Tests have clear descriptions — names assert behaviors not implementations.
- [x] No hardcoded waits or sleeps — `TestContext.Current.CancellationToken` everywhere; `WebApplication.StartAsync` awaited.
- [x] Tests are independent — each test builds its own ephemeral `WebApplication` on port 0 with isolated read-models.
- [x] Test summary created — this file.
- [x] Tests saved to appropriate directories — `tests/Hexalith.Folders.Server.Tests/`.
- [x] Summary includes coverage metrics — above.

## Notes & Trade-offs

- **Helper duplication.** Each new test class re-declares its small set of `Static*` / `Allowing*` provider stubs rather than promoting them to a shared helper file. The same pattern is used in the dev's `AuditEndpointsTests.cs` and the broader Server.Tests project (per `CommitWorkspaceProcessEndpointTests.cs:200+`); keeping the same convention avoids a cross-cutting refactor in a QA pass. A future cleanup story may promote `AuditEndpointTestHost.cs` once 4+ files share the pattern (the `AuditEndpoints.cs` itself flagged this maintainability concern internally).
- **No corpus extension.** Per AC #8 / concern #6, audit-leakage corpus extensions require a separate PR with reviewer sign-off. This pass reads the existing corpus and inventory unchanged.
- **No edits to story-marked "no edit" files.** Generated client, OpenAPI spec, parity oracle, and CLI/MCP/Client adapter tests left untouched.
