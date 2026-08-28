---
title: 'Use canonical error mapping in the parity gateway'
type: 'refactor'
created: '2026-08-29'
status: 'done'
baseline_revision: '938a1987d994834b622a30ba288f4b2ee1c79c6c'
review_loop_iteration: 0
followup_review_recommended: true
context: []
warnings: []
deferred:
  - summary: >-
      FolderCommandRejected.Create accepts noncanonical numeric and
      whitespace-padded FolderResultCode strings because its whitelist uses
      permissive Enum.TryParse semantics.
    evidence: >-
      The production factory calls Enum.TryParse<FolderResultCode>(code, out _)
      without an exact ordinal round-trip or Enum.IsDefined check, despite its
      strict-whitelist comment. The parity gateway now defends its own wire
      boundary, but changing the production event factory is outside DW-15.
    location: 'src/Hexalith.Folders.Server/FolderCommandRejected.cs:97'
    severity: medium
  - summary: >-
      Rejection conversion assumes DomainServiceWireResult contains exactly one
      event and throws when a rejected result contains zero or multiple events.
    evidence: >-
      ToGatewayException still selects result.Events.Single(), while the wire
      result contract represents Events as a collection. Defining multi-event
      rejection semantics is broader than replacing the duplicated canonical
      mapping requested by DW-15.
    location: 'tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs:134'
    severity: medium
  - summary: >-
      The Hexalith.Folders.IntegrationTests project has no unfiltered CI lane, so
      most of its classes -- including this story's gateway-boundary suite and the
      pre-existing ArchiveFolderProcessWiringTests -- never execute in CI.
    evidence: |-
      tests/tools/run-contract-parity-ci-gates.ps1 reaches this project only through
      two `--filter` expressions naming GoldenLifecycleParityTests,
      CrossAdapterBehavioralParityTests, and MixedSurfaceHandoffTests;
      run-baseline-ci-gates.ps1 enumerates nine test projects and does not list this
      one. Reverting the canonical mapping fails 33 assertions locally while every CI
      lane stays green. Registering classes changes a CI contract that
      ContractParityCiWorkflowConformanceTests pins, which is broader than DW-15.
    location: 'tests/tools/run-contract-parity-ci-gates.ps1:71-94'
    severity: medium
  - summary: >-
      Roughly twenty result codes now reach a different caller-visible REST status
      through ToArchiveGatewayProblem, and no test drives the REST leg for any of
      them.
    evidence: |-
      FolderCanonicalErrorMapper emits categories that SafeGatewayReasonCode does not
      whitelist (state_transition_invalid, validation_error, not_found, policy_denied,
      already_archived, authentication_failure, repository_conflict), so those
      rejections fall through to the status-only default arm; StateTransitionInvalid
      reaches 422 at the gateway exception but still renders 403 denied_safe at REST.
      This story's acceptance criteria and I/O matrix are all stated at the gateway
      exception boundary, so cross-surface REST coverage is outside its scope. The
      four codes existing suites actually drive are unchanged end-to-end
      (IntegrationTests 667/667 green).
    location: 'src/Hexalith.Folders.Server/FoldersDomainServiceEndpoints.cs:3909'
    severity: medium
  - summary: >-
      The parity double's rejection reason code matches neither spelling the real
      EventStore gateway produces, so its "production fidelity" claim is unverified
      against the actual gateway hop.
    evidence: >-
      In production a Folders rejection reaches EventStoreGatewayException through
      DomainCommandRejectedExceptionHandler, which derives ProblemDetails reasonCode from
      DomainRejectionProblemCatalog.FromRejectionType(rejection.RejectionType) -- a
      kebab-case name derived from the rejection EVENT TYPE (folder-command-rejected),
      with a status drawn from a small set. The FolderResultCode never reaches the
      gateway exception in production at all. The double previously emitted the PascalCase
      enum name and now emits the snake_case canonical category; neither is the production
      wire spelling. Reconciling the two vocabularies would change the EventStore submodule
      or the caller-visible canonical error vocabulary, both of which this story's Block If
      and Never lists forbid.
    location: >-
      references/Hexalith.EventStore/src/Hexalith.EventStore/ErrorHandling/DomainCommandRejectedExceptionHandler.cs:43
    severity: medium
  - summary: >-
      FolderCanonicalErrorMapper.CategoryFor and StatusFor have no production caller, so the
      table this story adopts as canonical is documentation rather than the mapping that runs.
    evidence: >-
      grep over src/ finds exactly one call into the mapper --
      FoldersDomainServiceEndpoints.cs:5733 calls ClientActionFor. CategoryFor and StatusFor
      are called only by their own unit test and, after this change, by the parity double.
      FoldersDomainServiceEndpoints never references FolderResultCode; its caller-visible
      surface is keyed on SafeGatewayReasonCode instead. Wiring the mapper into the endpoint
      path would change caller-visible canonical error vocabulary, which this story's Never
      list forbids.
    location: 'src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs:9'
    severity: medium
---

<intent-contract>

## Intent

**Problem:** `InProcessRejectionPropagatingGatewayClient.ToGatewayException` duplicates a partial `FolderResultCode`-to-HTTP-status switch, allowing the parity gateway to diverge from the canonical production error surface and to expose non-canonical reason-code spellings.

**Approach:** Resolve the rejection payload's `FolderResultCode` through the existing `FolderCanonicalErrorMapper`, and add focused gateway-boundary tests that pin representative canonical statuses and reason codes, including rows the old local switch handled incorrectly.

## Boundaries & Constraints

**Always:** Preserve the real in-process `/process` round trip, correlation propagation, result-payload behavior, and metadata-only rejection handling; use ordinal, exact enum-name parsing and the server's existing canonical category/status mapper; keep tests hermetic and cancellation-aware.

**Block If:** The production mapper cannot represent a current `FolderResultCode`, or consuming it would require changing the EventStore submodule/API, public Contract Spine, or caller-visible canonical error vocabulary.

**Never:** Edit the deferred-work ledger; introduce another result-code/status table; hand-edit generated clients; initialize nested submodules; weaken safe-denial behavior; echo an unknown wire value as a reason code.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Canonical rejection | `/process` returns one rejection carrying a known `FolderResultCode` | Gateway exception uses `FolderCanonicalErrorMapper`'s HTTP status and canonical snake-case category as `ReasonCode` | No alternate local mapping |
| Former fallback row | Rejection code such as `LockExpired`, `StateTransitionInvalid`, or `ProviderUnavailable` | Gateway exception surfaces 410, 422, or 503 respectively with the matching canonical reason code | Must not fall through to the old generic 403 |
| Malformed code | Rejection code is absent, blank, or not a defined enum member | Treat as `MalformedEvidence` and map through the canonical surface without echoing input | Deterministic metadata-only failure |

</intent-contract>

## Code Map

- `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs:18-160` -- linked integration-test gateway; replace `ToGatewayException`'s local switch, update its fidelity documentation, and preserve its `/process` and successful-payload behavior.
- `src/Hexalith.Folders.Server/FolderCanonicalErrorMapper.cs:7-89` -- read-only production reuse point for `FolderResultCode` category and status mapping; integration tests can access it through the existing `InternalsVisibleTo` grant.
- `src/Hexalith.Folders.Server/FolderCommandRejected.cs:78-142` -- read-only evidence for production rejection serialization; its permissive `Enum.TryParse` whitelist is recorded as deferred, so the parity gateway independently enforces exact defined names at its wire boundary.
- `tests/Hexalith.Folders.IntegrationTests/Parity/InProcessRejectionPropagatingGatewayClientTests.cs` -- new focused gateway-boundary theory using a hermetic test server to assert status, canonical reason code, and correlation propagation.
- `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj:18-46` -- read-only composition evidence: references Server and links the shared parity gateway source.

## Tasks & Acceptance

**Execution:**
- `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` -- parse the wire code into a defined `FolderResultCode`, fall back safely to `MalformedEvidence`, derive category/status from `FolderCanonicalErrorMapper`, and pass the canonical category as `EventStoreGatewayException.ReasonCode`.
- `tests/Hexalith.Folders.IntegrationTests/Parity/InProcessRejectionPropagatingGatewayClientTests.cs` -- drive `SubmitCommandAsync` against rejection wire responses and pin representative 400/403/409/410/422/429/503 mappings plus malformed-code handling and correlation preservation.

**Acceptance Criteria:**
- Given any defined rejection code covered by the focused matrix, when the in-process parity gateway converts the `/process` rejection, then its exception status and reason code equal the canonical production mapping literals.
- Given a code that the former switch sent to generic 403, when the same rejection is converted, then the canonical non-403 status and snake-case reason code are observed at the gateway boundary.
- Given an absent, blank, numeric, or unknown rejection code, when conversion runs, then the input is not echoed and the canonical `MalformedEvidence` mapping is returned.
- Given a caller correlation ID, when a rejection is converted, then the same safe correlation ID remains on the gateway exception.

## Spec Change Log

## Review Triage Log

### 2026-08-29 — Review pass

- intent_gap: 0
- bad_spec: 0
- patch: 10: (high 0, medium 6, low 4)
- defer: 2: (high 0, medium 2, low 0)
- reject: 7: (high 0, medium 4, low 3)
- addressed_findings:
  - `[low]` `[patch]` Narrowed the gateway fidelity documentation to the gateway-exception boundary.
  - `[medium]` `[patch]` Rejected success-only `FolderResultCode` values as malformed rejection evidence.
  - `[medium]` `[patch]` Mapped non-object JSON roots to canonical malformed evidence.
  - `[medium]` `[patch]` Mapped conflicting `code` and `Code` properties to canonical malformed evidence.
  - `[low]` `[patch]` Added coverage for the supported lowercase `code` property.
  - `[medium]` `[patch]` Added exact-name coverage for numeric, wrong-case, and whitespace-padded values.
  - `[low]` `[patch]` Made test-server startup and cleanup safe across startup, cancellation, and stop failures.
  - `[medium]` `[patch]` Added the impacted archive process-wiring regression suite to verification.
  - `[medium]` `[patch]` Added the reachable `LockNotOwned` canonical mapping row.
  - `[low]` `[patch]` Corrected the Code Map to describe the production factory's permissive parse accurately.

### 2026-08-29 — Review pass (follow-up)

- intent_gap: 0
- bad_spec: 0
- patch: 9: (high 0, medium 5, low 4)
- defer: 2: (high 0, medium 2, low 0)
- reject: 9: (high 0, medium 4, low 5)
- addressed_findings:
  - `[medium]` `[patch]` Removed the duplicated success-code list from `ParseRejectionCode`; success membership now resolves through `FolderCanonicalErrorMapper.CategoryFor(...) == "success"`, so the boundary keeps no result-code table of its own.
  - `[medium]` `[patch]` Guarded `JsonDocument.Parse` so an empty, truncated, or non-JSON rejection body degrades to the canonical malformed-evidence mapping instead of letting a `JsonException` replace the rejection; added rows for `""`, `not-json`, and a truncated object.
  - `[medium]` `[patch]` Added discriminating strict-parse rows (`lockexpired`, `LOCKEXPIRED`, `" LockExpired "`). The previous wrong-case and whitespace rows could not fail, because `MalformedEvidence` and `ValidationFailed` share the `validation_error` category; relaxing the parse to `ignoreCase: true` without the ordinal round-trip now fails 4 assertions where it previously failed none.
  - `[medium]` `[patch]` Added `{"Code":"999"}` to pin the load-bearing `Enum.IsDefined` guard — undefined ordinals do round-trip through `ToString()`, so without it a numeric wire value would map to `internal_error`/503.
  - `[medium]` `[patch]` Widened verification from two classes to the whole `Hexalith.Folders.IntegrationTests` project: the shared double is linked by four suites (`ArchiveFolderProcessWiringTests`, `GoldenLifecycleParityTests`, `MixedSurfaceHandoffTests`, `ContextSearchFacadeWiringTests`) the prior command list did not run.
  - `[low]` `[patch]` Added the rows the removed switch already handled correctly, which no test pinned: `FolderNotFound` → 404 `not_found` and `StaleProjection` → 503 `projection_stale`.
  - `[low]` `[patch]` Replaced the assertion that could not fail (`ReasonCode.ShouldNotBe("NotAResultCode")`, dead after the preceding `ShouldBe`) with a metadata-only message pin.
  - `[low]` `[patch]` Aligned the test host with the sibling suites' hermetic `WebApplicationOptions` instead of the bare `CreateSlimBuilder()` overload, which admitted ambient environment and content-root configuration.
  - `[low]` `[patch]` Corrected the class remark, which claimed drift-proofing that the local success list contradicted.

### 2026-08-29 — Review pass (follow-up 2)

- intent_gap: 0
- bad_spec: 0
- patch: 5: (high 0, medium 2, low 3)
- defer: 2: (high 0, medium 2, low 0)
- reject: 19: (high 0, medium 7, low 12)
- addressed_findings:
  - `[medium]` `[patch]` Added `EveryDeclaredRejectionCodeNameShouldResolveThroughTheCanonicalMapper`, a drift guard that drives every declared `FolderResultCode` name (52 rejection codes, not the 10 the focused theory pins) through the boundary and asserts the mapper's own category and status. This turns the class remark's drift-proof claim into an enforced property and covers the previously untested 401 and 403-default status classes. Mutation-verified: degrading a single uncovered code (`PathPolicyDenied`) to `MalformedEvidence` fails exactly this test and no other.
  - `[medium]` `[patch]` Guarded a null or empty rejection payload in `ParseRejectionCode`. A JSON-null `Payload` binds straight through the positional wire record, and `JsonDocument.Parse(null)` throws `ArgumentNullException`, which the newly added `JsonException` catch cannot absorb -- the exact failure mode that catch exists to prevent. Added `NullRejectionPayloadShouldUseCanonicalMalformedEvidenceMapping`.
  - `[low]` `[patch]` Corrected the strict-parse comment, which claimed `Enum.TryParse` accepts wrong casing. With `ignoreCase: false` it does not; the round-trip guard's real jobs are numeric strings, whitespace, comma-separated member lists, and alias names.
  - `[low]` `[patch]` Documented the `hasCamelCode == hasPascalCode` guard, whose meaning ("neither present, or both present") and deliberate rejection of agreeing duplicates were unstated.
  - `[low]` `[patch]` Recorded at the csproj link site that the shared double now needs an `InternalsVisibleTo` grant from `Hexalith.Folders.Server`; only `Hexalith.Folders.IntegrationTests` and `Hexalith.Folders.Server.Tests` hold one, so a future link into another test project would break the build.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --no-restore -m:1 -v:m` -- expected: zero warnings and errors.
- `tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests -noLogo -parallel none -class Hexalith.Folders.IntegrationTests.Parity.InProcessRejectionPropagatingGatewayClientTests` -- expected: every focused mapping row passes.
- `tests/Hexalith.Folders.IntegrationTests/bin/Debug/net10.0/Hexalith.Folders.IntegrationTests -noLogo -parallel none` -- expected: the whole project passes, covering all four suites that link the shared parity double.
- `dotnet format whitespace tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj --no-restore --verify-no-changes --include tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs tests/Hexalith.Folders.IntegrationTests/Parity/InProcessRejectionPropagatingGatewayClientTests.cs` -- expected: no formatting changes required.
- `git diff --check -- tests/ src/` -- expected: no whitespace errors.

## Auto Run Result

Status: done

### Implemented change

`InProcessRejectionPropagatingGatewayClient.ToGatewayException` no longer keeps its own partial
`FolderResultCode` → HTTP-status switch. It parses the `/process` rejection payload's code with exact
ordinal enum-name semantics and resolves both the HTTP status and the snake-case `reasonCode` through the
production `FolderCanonicalErrorMapper`. Non-object roots, non-string `Code` values, simultaneous
`code`/`Code` properties, numeric or undefined ordinals, wrong casing, surrounding whitespace, success
codes, non-JSON bodies, and null or empty payload bytes all degrade to the canonical `MalformedEvidence`
mapping without echoing the wire value. Success membership is asked of the mapper rather than restated
locally, so the boundary now holds no result-code table at all, and an exhaustive drift guard pins that
every declared rejection-code name resolves to the mapper's own category and status.

### Files changed

- `tests/shared/Parity/InProcessRejectionPropagatingGatewayClient.cs` -- replaced the local switch with
  `FolderCanonicalErrorMapper`, added the strict wire-code parse, guarded `JsonDocument.Parse` and the
  null/empty payload, and narrowed the fidelity documentation to the gateway-exception boundary.
- `tests/Hexalith.Folders.IntegrationTests/Parity/InProcessRejectionPropagatingGatewayClientTests.cs` --
  hermetic gateway-boundary suite (35 tests) pinning 400/403/404/409/410/422/429/503 canonical mappings,
  malformed-evidence handling, correlation propagation, and an all-codes drift guard over a shared host.
- `tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj` -- recorded the
  `InternalsVisibleTo` constraint the linked shared source now carries.

### Review findings breakdown

- Patches applied this pass: 5 (high 0, medium 2, low 3).
- Items deferred this pass: 2 (the double's reason-code spelling matches neither production gateway form;
  `FolderCanonicalErrorMapper.CategoryFor`/`StatusFor` have no production caller). Four earlier deferrals
  are preserved.
- Items rejected this pass: 19 -- chiefly re-reports of the four already-deferred issues (no CI lane for
  this project, REST-leg divergence through `SafeGatewayReasonCode`, `result.Events.Single()`, the
  production factory's permissive parse), plus the proposal to validate `rejection.EventTypeName` (would
  misclassify differently-typed production rejections, rejected in an earlier pass), the claim that
  `code is null` should be removed (harmless nullability guard), and test-organization and
  host-reuse-performance nits.

### Follow-up review recommendation

`true`. Patched findings this pass: high 0, medium 2, low 3. Score = (3 × 2) + (1 × 3) = 9, at or above the
threshold of 5.

### Verification performed

- `dotnet build tests/Hexalith.Folders.IntegrationTests/Hexalith.Folders.IntegrationTests.csproj` -- 0
  warnings, 0 errors.
- Focused suite `InProcessRejectionPropagatingGatewayClientTests` -- 35 tests, 0 failed.
- Whole project -- 669 tests, 0 failed, 0 skipped, covering the four suites that link the shared double.
- Mutation check on the new drift guard: degrading `PathPolicyDenied` (a code no focused row covers) to
  `MalformedEvidence` fails exactly 1 of the 35 tests -- the drift guard -- proving it is not vacuous.
  Source restored and rebuilt clean.
- `dotnet format whitespace --verify-no-changes` on both touched sources: no changes required.
- `git diff --check -- tests/ src/`: clean.

### Residual risks

- The gateway-boundary suite still runs in no CI lane (deferred): a regression in this mapping is caught
  only by a local or full-project run.
- The canonical categories this boundary emits are wider than `SafeGatewayReasonCode`'s whitelist, so
  several codes reach a different REST status than before. No existing suite drives those codes, so the
  change is observably inert end-to-end today, but the cross-surface behaviour is unpinned (deferred).
- The reason-code vocabulary this double emits is not the one the real EventStore gateway produces
  (deferred), so parity assertions built on it prove internal consistency, not production fidelity.
- `result.Events.Single()` still throws for a zero- or multi-event rejection (previously deferred).
